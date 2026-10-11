using System.Numerics;
using Content.Shared.DeadSpace.MangoSpiderBoss.Components;
using Content.Shared.DeadSpace.MangoSpiderBoss.Systems;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using ContentDrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.DeadSpace.MangoSpiderBoss.Systems;

public sealed class SpiderLegsSystem : SharedSpiderLegsSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> HideContextMenuTag = "HideContextMenu";

    private readonly Dictionary<EntityUid, SpiderRender> _renders = new();

    private const uint LegMaxRenderOrder = 1000;

    private static uint BoneRenderOrder(int segment)
        => LegMaxRenderOrder - (uint)segment;

    private static uint FootRenderOrder(int boneCount)
        => LegMaxRenderOrder - (uint)boneCount - 1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpiderLegsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SpiderLegsComponent, EntityTerminatingEvent>(OnTerminating);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SpiderLegsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            SimulateLegs((uid, comp), now);
            UpdateSpider((uid, comp));
        }
    }

    private void OnShutdown(Entity<SpiderLegsComponent> ent, ref ComponentShutdown args)
    {
        if (_renders.Remove(ent.Owner, out var render))
            DeleteRender(render);
    }

    private void OnTerminating(Entity<SpiderLegsComponent> ent, ref EntityTerminatingEvent args)
    {
        if (_renders.Remove(ent.Owner, out var render))
            DeleteRender(render);
    }

    private void UpdateSpider(Entity<SpiderLegsComponent> ent)
    {
        var comp = ent.Comp;
        var parent = Transform(ent).ParentUid;
        if (!parent.IsValid())
            return;

        var (pos, rotA) = Xform.GetWorldPositionRotation(ent);
        var rot = (float)rotA.Theta;

        EnsureChains(comp, pos, rot);
        var chains = comp.Chains;
        if (chains == null)
            return;

        if (!_renders.TryGetValue(ent.Owner, out var render) || !render.Valid(comp) || !RenderAlive(render, comp))
        {
            if (render != null)
                DeleteRender(render);

            render = BuildRender(ent);
            _renders[ent.Owner] = render;
        }

        for (var i = 0; i < comp.Legs.Count; i++)
        {
            var leg = comp.Legs[i];
            var legRender = render.Legs[i];

            if (leg.Severed)
            {
                if (!legRender.Hidden)
                {
                    legRender.Hidden = true;
                    DeleteLeg(legRender);
                }

                continue;
            }

            var footColor = Color.White;
            if (TryGetEntity(leg.Foot, out var footUid) && Exists(footUid))
            {
                var foot = footUid.Value;
                if (legRender.FootEntity != foot)
                {
                    ConfigureFootSprite(foot, comp.ResolveFoot(leg), comp.ResolveBones(leg).Count);
                    legRender.FootEntity = foot;
                }

                if (TryComp<SpriteComponent>(foot, out var footSprite))
                    footColor = footSprite.Color;
            }

            var footPos = ServerFootTarget(leg, pos, rot);

            var hip = pos + Rotate(leg.Position, rot);
            var bend = leg.Position.X >= 0 ? 1f : -1f;
            var chain = chains[i];
            chain.Solve(hip, footPos, bend);

            var joints = chain.Joints;
            for (var s = 0; s < legRender.Bones.Length; s++)
            {
                var a = joints[s];
                var b = joints[s + 1];
                var mid = (a + b) * 0.5f;
                var angle = (b - a).ToWorldAngle();
                var bone = legRender.Bones[s];
                PlaceSprite(bone, parent, mid, angle);

                if (TryComp<SpriteComponent>(bone, out var boneSprite))
                    _sprite.SetColor((bone, boneSprite), footColor);
            }

            if (Exists(legRender.FootVisual))
            {
                var tip = chain.End;
                var stepDir = leg.To - leg.From;
                if (stepDir.LengthSquared() > 1e-4f)
                    legRender.FootAngle = stepDir.ToWorldAngle();

                PlaceSprite(legRender.FootVisual, parent, tip, legRender.FootAngle);

                if (TryComp<SpriteComponent>(legRender.FootVisual, out var footVisSprite))
                    _sprite.SetColor((legRender.FootVisual, footVisSprite), footColor);
            }
        }
    }

    private void PlaceSprite(EntityUid uid, EntityUid parent, Vector2 worldPos, Angle worldAngle)
    {
        var xform = Transform(uid);
        if (xform.ParentUid != parent)
            Xform.SetParent(uid, xform, parent);

        var parentXform = Transform(parent);
        var local = Vector2.Transform(worldPos, parentXform.InvWorldMatrix);
        var localRot = worldAngle - parentXform.WorldRotation;
        Xform.SetLocalPositionRotation(uid, local, localRot, xform);
    }

    private bool RenderAlive(SpiderRender render, SpiderLegsComponent comp)
    {
        for (var i = 0; i < render.Legs.Length; i++)
        {
            if (i < comp.Legs.Count && comp.Legs[i].Severed)
                continue;

            foreach (var bone in render.Legs[i].Bones)
            {
                if (!Exists(bone))
                    return false;
            }

            if (!Exists(render.Legs[i].FootVisual))
                return false;
        }

        return true;
    }

    private Vector2 ServerFootTarget(SpiderLegData leg, Vector2 pos, float rot)
    {
        if (TryGetEntity(leg.Foot, out var foot) && Exists(foot))
            return Xform.GetWorldPosition(foot.Value);

        return pos + Rotate(leg.Rest, rot);
    }

    private SpiderRender BuildRender(Entity<SpiderLegsComponent> ent)
    {
        var comp = ent.Comp;
        var coords = Transform(ent).Coordinates;
        var render = new SpiderRender
        {
            Legs = new LegRender[comp.Legs.Count],
        };

        for (var i = 0; i < comp.Legs.Count; i++)
        {
            var bones = comp.ResolveBones(comp.Legs[i]);
            var boneEnts = new EntityUid[bones.Count];
            for (var s = 0; s < boneEnts.Length; s++)
                boneEnts[s] = CreateSprite(bones[s], coords, (int)ContentDrawDepth.Mobs, BoneRenderOrder(s));

            render.Legs[i] = new LegRender
            {
                Bones = boneEnts,
                FootVisual = CreateSprite(comp.ResolveFoot(comp.Legs[i]), coords,
                    (int)ContentDrawDepth.BelowMobs, FootRenderOrder(bones.Count)),
            };
        }

        return render;
    }

    private EntityUid CreateSprite(SpiderBoneVisual visual, EntityCoordinates coords, int drawDepth, uint renderOrder)
    {
        var uid = Spawn(null, coords);
        var sprite = EnsureComp<SpriteComponent>(uid);
        _sprite.SetDrawDepth((uid, sprite), drawDepth);
        _tag.AddTag(uid, HideContextMenuTag);
        sprite.RenderOrder = renderOrder;
        ApplyLayers((uid, sprite), visual.Layers);
        return uid;
    }

    private void ConfigureFootSprite(EntityUid foot, SpiderBoneVisual visual, int boneCount)
    {
        var sprite = EnsureComp<SpriteComponent>(foot);
        _sprite.SetDrawDepth((foot, sprite), (int)ContentDrawDepth.BelowMobs);
        sprite.RenderOrder = FootRenderOrder(boneCount);
        ApplyLayers((foot, sprite), visual.Layers);

        sprite.Visible = false;
    }

    private void ApplyLayers(Entity<SpriteComponent?> ent, List<SpiderLegVisualLayer> layers)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        var sprite = ent.Comp;

        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            var index = _sprite.LayerMapReserve(ent, $"spider-layer-{i}");

            if (index < 0)
                continue;

            if (layer.Sprite is SpriteSpecifier.Texture { TexturePath: var path } && path == ResPath.Self)
            {
                _sprite.LayerSetVisible(ent, index, false);
                continue;
            }

            _sprite.LayerSetSprite(ent, index, layer.Sprite);
            _sprite.LayerSetOffset(ent, index, layer.Offset);
            _sprite.LayerSetScale(ent, index, layer.Scale);
            _sprite.LayerSetVisible(ent, index, layer.Visible);

            if (string.IsNullOrEmpty(layer.Shader))
                sprite.LayerSetShader(index, (ShaderInstance?)null);
            else
                sprite.LayerSetShader(index, layer.Shader);
        }
    }

    private void DeleteRender(SpiderRender render)
    {
        foreach (var leg in render.Legs)
            DeleteLeg(leg);
    }

    private void DeleteLeg(LegRender leg)
    {
        foreach (var bone in leg.Bones)
        {
            if (Exists(bone))
                QueueDel(bone);
        }

        if (Exists(leg.FootVisual))
            QueueDel(leg.FootVisual);

        leg.Bones = Array.Empty<EntityUid>();
        leg.FootVisual = EntityUid.Invalid;
        leg.FootEntity = EntityUid.Invalid;
    }

    private sealed class SpiderRender
    {
        public LegRender[] Legs = Array.Empty<LegRender>();

        public bool Valid(SpiderLegsComponent comp)
        {
            if (Legs.Length != comp.Legs.Count)
                return false;

            for (var i = 0; i < Legs.Length; i++)
            {
                if (comp.Legs[i].Severed)
                    continue;

                if (Legs[i].Bones.Length != comp.ResolveBones(comp.Legs[i]).Count)
                    return false;
            }

            return true;
        }
    }

    private sealed class LegRender
    {
        public EntityUid[] Bones = Array.Empty<EntityUid>();
        public EntityUid FootEntity = EntityUid.Invalid;
        public EntityUid FootVisual = EntityUid.Invalid;
        public bool Hidden;
        public Angle FootAngle;
    }
}
