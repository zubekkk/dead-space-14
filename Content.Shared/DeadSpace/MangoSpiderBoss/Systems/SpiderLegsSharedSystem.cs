using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using System.Numerics;
using Content.Shared.DeadSpace.MangoSpiderBoss.Components;
using Content.Shared.Maps;

namespace Content.Shared.DeadSpace.MangoSpiderBoss.Systems;

public abstract class SharedSpiderLegsSystem : EntitySystem
{
    [Dependency] protected readonly SharedTransformSystem Xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] protected readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SpiderLegsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            SimulateLegs((uid, comp), now);
        }
    }

    protected void SimulateLegs(Entity<SpiderLegsComponent> ent, TimeSpan now)
    {
        var (pos, rotA) = Xform.GetWorldPositionRotation(ent);
        var rot = (float)rotA.Theta;

        DecideSteps(ent, pos, rot, _physics.GetMapLinearVelocity(ent.Owner), now);
        ApplyFoot(ent, now);
        PlayFootsteps(ent, now);
    }

    protected static void EnsureChains(SpiderLegsComponent comp, Vector2 pos, float rot)
    {
        if (comp.Chains != null && comp.Chains.Length == comp.Legs.Count)
            return;

        comp.Chains = new FabrikChain[comp.Legs.Count];

        for (var i = 0; i < comp.Legs.Count; i++)
        {
            var leg = comp.Legs[i];
            var segments = Math.Max(comp.ResolveBones(leg).Count, 1);
            var source = leg.BoneLengths ?? comp.BoneLengths;

            var lengths = new float[segments];
            for (var s = 0; s < segments; s++)
                lengths[s] = source.Count == 0 ? 1f : source[Math.Min(s, source.Count - 1)];

            var hip = pos + Rotate(leg.Position, rot);
            var dir = Rotate(leg.Rest - leg.Position, rot);

            comp.Chains[i] = new FabrikChain(hip, dir, lengths) { MaxIterations = comp.FabrikIterations };
        }
    }

    protected static float GetReach(SpiderLegsComponent comp, SpiderLegData leg)
    {
        var segments = Math.Max(comp.ResolveBones(leg).Count, 1);
        var source = leg.BoneLengths ?? comp.BoneLengths;
        var total = 0f;
        for (var s = 0; s < segments; s++)
            total += source.Count == 0 ? 1f : source[Math.Min(s, source.Count - 1)];

        return total;
    }

    private void DecideSteps(Entity<SpiderLegsComponent> ent, Vector2 pos, float rot, Vector2 vel, TimeSpan now)
    {
        var comp = ent.Comp;
        var dur = TimeSpan.FromSeconds(comp.StepDuration);
        var mapId = Transform(ent.Owner).MapID;
        var velLocal = Rotate(vel, -rot);
        var offset = velLocal * comp.Lookahead;
        if (offset.Length() > comp.MaxLookaheadOffset)
            offset = Vector2.Normalize(offset) * comp.MaxLookaheadOffset;

        var groupBusy = new bool[2];
        foreach (var leg in comp.Legs)
            if (!leg.Severed && now < leg.StepStart + dur)
                groupBusy[leg.Group] = true;

        var dirty = false;
        for (var i = 0; i < comp.Legs.Count; i++)
        {
            var leg = comp.Legs[i];
            if (leg.Severed || now < leg.StepStart + dur)
                continue;

            var reach = GetReach(comp, leg);
            var hip = pos + Rotate(leg.Position, rot);
            var desired = pos + Rotate(leg.Rest + offset, rot) + vel * (float)dur.TotalSeconds;
            var foot = leg.To;

            var err = Vector2.Distance(foot, desired);
            var overstretched = Vector2.Distance(hip, foot) > reach * 0.98f;
            var blocked = !overstretched && TooCloseToOthers(comp, leg, desired);

            if (!blocked && (err > comp.StepThreshold && !groupBusy[1 - leg.Group] || overstretched))
            {
                if (!TryFindValidFoot(comp, leg, mapId, hip, desired, foot, reach, out var target))
                    continue;

                leg.From = foot;
                leg.To = target;
                leg.StepStart = now;
                groupBusy[leg.Group] = true;
                dirty = true;
            }
        }

        if (dirty)
            Dirty(ent);
    }

    private bool TryFindValidFoot(SpiderLegsComponent comp, SpiderLegData leg, MapId mapId,
        Vector2 hip, Vector2 desired, Vector2 from, float reach, out Vector2 result)
    {
        var maxReach = reach * 0.98f;

        if (IsAcceptableFootSpot(comp, leg, mapId, hip, from, desired, maxReach))
        {
            result = desired;
            return true;
        }

        var rings = Math.Max(1, comp.FootSearchRings);
        var samples = Math.Max(1, comp.FootSearchSamples);
        var bestDist = float.MaxValue;
        result = hip;

        for (var ring = 1; ring <= rings; ring++)
        {
            var radius = maxReach * ring / rings;

            for (var i = 0; i < samples; i++)
            {
                var angle = MathF.Tau * i / samples;
                var candidate = hip + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

                if (!IsAcceptableFootSpot(comp, leg, mapId, hip, from, candidate, maxReach))
                    continue;

                var dist = Vector2.DistanceSquared(desired, candidate);
                if (dist >= bestDist)
                    continue;

                bestDist = dist;
                result = candidate;
            }
        }

        if (bestDist < float.MaxValue)
            return true;

        if (IsValidFootSpot(comp, mapId, hip))
        {
            result = hip;
            return true;
        }

        result = default;
        return false;
    }

    private bool IsAcceptableFootSpot(SpiderLegsComponent comp, SpiderLegData leg, MapId mapId,
        Vector2 hip, Vector2 from, Vector2 candidate, float maxReach)
    {
        if (Vector2.Distance(hip, candidate) > maxReach)
            return false;

        if (!IsValidFootSpot(comp, mapId, candidate))
            return false;

        if (!IsSegmentClear(comp, mapId, hip, candidate))
            return false;

        if (!IsSegmentClear(comp, mapId, from, candidate))
            return false;

        return !TooCloseToOthers(comp, leg, candidate);
    }

    private bool IsSegmentClear(SpiderLegsComponent comp, MapId mapId, Vector2 a, Vector2 b)
    {
        const float step = 0.25f;
        var dist = Vector2.Distance(a, b);
        if (dist <= step)
            return true;

        var count = (int)Math.Ceiling(dist / step);
        for (var i = 1; i < count; i++)
        {
            var t = (float)i / count;
            if (!IsValidFootSpot(comp, mapId, Vector2.Lerp(a, b, t)))
                return false;
        }

        return true;
    }

    private bool IsValidFootSpot(SpiderLegsComponent comp, MapId mapId, Vector2 worldPos)
    {
        if (!_mapManager.TryFindGridAt(mapId, worldPos, out var gridUid, out var grid))
            return false;

        if (!_map.TryGetTileRef(gridUid, grid, worldPos, out var tileRef))
            return false;

        if (tileRef.Tile.IsEmpty)
            return false;

        if (!_turf.GetContentTileDefinition(tileRef).Sturdy)
            return false;

        return !_turf.IsTileBlocked(tileRef, comp.FootBlockMask);
    }

    public Vector2 FootAt(SpiderLegsComponent comp, SpiderLegData leg, TimeSpan now)
    {
        var t = (float)((now - leg.StepStart).TotalSeconds / comp.StepDuration);
        t = Math.Clamp(t, 0f, 1f);
        var eased = t * t * (3f - 2f * t);
        return Vector2.Lerp(leg.From, leg.To, eased);
    }

    private void ApplyFoot(Entity<SpiderLegsComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        foreach (var leg in comp.Legs)
        {
            if (leg.Severed || leg.Foot == NetEntity.Invalid)
                continue;

            var dir = leg.To - leg.From;
            var angle = dir.LengthSquared() > 1e-6f ? dir.ToWorldAngle() : Angle.Zero;
            Place(leg.Foot, ent.Owner, FootAt(comp, leg, now), angle);
        }
    }

    private void PlayFootsteps(Entity<SpiderLegsComponent> ent, TimeSpan now)
    {
        if (_net.IsClient)
            return;

        var comp = ent.Comp;
        if (comp.FootstepSound == null)
            return;

        var dur = TimeSpan.FromSeconds(comp.StepDuration);

        if (comp.LastFootstep == null || comp.LastFootstep.Length != comp.Legs.Count)
            comp.LastFootstep = new TimeSpan[comp.Legs.Count];

        for (var i = 0; i < comp.Legs.Count; i++)
        {
            var leg = comp.Legs[i];
            if (leg.Severed || leg.Foot == NetEntity.Invalid)
                continue;

            if (leg.StepStart == comp.LastFootstep[i] || now < leg.StepStart + dur)
                continue;

            comp.LastFootstep[i] = leg.StepStart;

            if (!TryGetEntity(leg.Foot, out var footUid) || !Exists(footUid) ||
                !TryComp<TransformComponent>(footUid, out var footXform))
                continue;

            var audioParams = comp.FootstepSound.Params
                .WithVolume(comp.FootstepSound.Params.Volume + comp.FootstepVolume)
                .WithVariation(comp.FootstepSound.Params.Variation ?? comp.FootstepVariation);

            _audio.PlayPredicted(comp.FootstepSound, footXform.Coordinates, null, audioParams);
        }
    }

    protected void Place(NetEntity net, EntityUid spider, Vector2 worldPos, Angle worldAngle)
    {
        if (!TryGetEntity(net, out var part) || !Exists(part))
            return;

        if (!_net.IsClient)
        {
            var spiderParent = Transform(spider).ParentUid;
            if (Transform(part.Value).ParentUid != spiderParent)
                Xform.SetParent(part.Value, spiderParent);
        }

        Xform.SetWorldPositionRotation(part.Value, worldPos, worldAngle);
    }

    private static bool TooCloseToOthers(SpiderLegsComponent comp, SpiderLegData self, Vector2 target)
    {
        foreach (var other in comp.Legs)
        {
            if (ReferenceEquals(other, self) || other.Severed)
                continue;

            if (Vector2.Distance(other.To, target) < comp.MinFootGap)
                return true;
        }

        return false;
    }

    protected static Vector2 Rotate(Vector2 v, float a)
    {
        var c = MathF.Cos(a);
        var s = MathF.Sin(a);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }
}