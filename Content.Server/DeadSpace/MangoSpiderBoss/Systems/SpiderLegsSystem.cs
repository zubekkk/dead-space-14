using Content.Shared.DeadSpace.MangoSpiderBoss.Systems;
using Content.Shared.DeadSpace.MangoSpiderBoss.Components;
using Content.Shared.Damage.Systems;

namespace Content.Server.DeadSpace.MangoSpiderBoss.Systems;

public sealed class SpiderLegsSystem : SharedSpiderLegsSystem
{
    private const string FootPrototype = "SpiderFoot";
    private const string DebugHipMarkerPrototype = "SpiderDebugHipMarker";
    private const string DebugRestMarkerPrototype = "SpiderDebugRestMarker";

    [Dependency] private readonly DamageableSystem _damageable = default!;

    private readonly Dictionary<EntityUid, List<EntityUid>> _debugMarkers = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpiderLegsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SpiderFootComponent, DamageChangedEvent>(OnFootDamaged);
        SubscribeLocalEvent<SpiderFootComponent, EntityTerminatingEvent>(OnFootTerminating);
        SubscribeLocalEvent<SpiderLegsComponent, EntityTerminatingEvent>(OnSpiderTerminating);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_debugMarkers.Count == 0)
            return;

        var query = EntityQueryEnumerator<SpiderLegsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_debugMarkers.TryGetValue(uid, out var markers))
                continue;

            var (pos, rotA) = Xform.GetWorldPositionRotation(uid);
            var rot = (float)rotA.Theta;
            for (var i = 0; i < comp.Legs.Count && i * 2 + 1 < markers.Count; i++)
            {
                var leg = comp.Legs[i];
                Xform.SetWorldPosition(markers[i * 2], pos + Rotate(leg.Position, rot));
                Xform.SetWorldPosition(markers[i * 2 + 1], pos + Rotate(leg.Rest, rot));
            }
        }
    }

    public bool IsLegDebugEnabled(EntityUid spider)
        => _debugMarkers.ContainsKey(spider);

    public void SetDebugLegPoints(EntityUid spider, bool enabled)
    {
        if (enabled)
        {
            if (_debugMarkers.ContainsKey(spider) ||
                !TryComp<SpiderLegsComponent>(spider, out var legs) ||
                legs.Legs.Count == 0)
                return;

            var markers = new List<EntityUid>(legs.Legs.Count * 2);
            var coords = Transform(spider).Coordinates;
            for (var i = 0; i < legs.Legs.Count; i++)
            {
                markers.Add(Spawn(DebugHipMarkerPrototype, coords));
                markers.Add(Spawn(DebugRestMarkerPrototype, coords));
            }

            _debugMarkers[spider] = markers;
        }
        else
        {
            RemoveDebug(spider);
        }
    }

    private void RemoveDebug(EntityUid spider)
    {
        if (!_debugMarkers.Remove(spider, out var markers))
            return;

        foreach (var marker in markers)
            Del(marker);
    }

    private void OnMapInit(Entity<SpiderLegsComponent> ent, ref MapInitEvent args)
    {
        var (pos, rotA) = Xform.GetWorldPositionRotation(ent);
        var rot = (float)rotA.Theta;
        var coords = Transform(ent).Coordinates;

        for (var i = 0; i < ent.Comp.Legs.Count; i++)
        {
            var leg = ent.Comp.Legs[i];
            leg.LegHealth = leg.LegMaxHealth;

            var foot = SpawnAtPosition(FootPrototype, coords);
            leg.Foot = GetNetEntity(foot);

            if (TryComp<SpiderFootComponent>(foot, out var footComp))
            {
                footComp.Spider = ent;
                footComp.LegIndex = i;
            }

            leg.From = leg.To = pos + Rotate(leg.Rest, rot);
            Xform.SetWorldPosition(foot, leg.To);
        }

        Dirty(ent);
    }

    private void OnFootDamaged(Entity<SpiderFootComponent> foot, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        var spider = foot.Comp.Spider;
        if (!TryComp<SpiderLegsComponent>(spider, out var legs))
            return;

        if (foot.Comp.LegIndex < 0 || foot.Comp.LegIndex >= legs.Legs.Count)
            return;

        var leg = legs.Legs[foot.Comp.LegIndex];
        if (leg.Severed)
            return;

        var delta = args.DamageDelta.GetTotal().Float();

        _damageable.TryChangeDamage(spider,
            args.DamageDelta * legs.FootDamageShare,
            origin: args.Origin);

        leg.LegHealth -= delta * (1f - legs.FootDamageShare);

        if (leg.LegHealth <= 0f)
            Sever(foot.Owner, spider, legs, leg);

        Dirty(spider, legs);
    }

    private void Sever(EntityUid foot, EntityUid spider, SpiderLegsComponent legs, SpiderLegData leg)
    {
        leg.Severed = true;
        leg.LegHealth = 0f;
        leg.Foot = NetEntity.Invalid;

        if (Exists(foot))
            QueueDel(foot);
    }

    private void OnFootTerminating(Entity<SpiderFootComponent> foot, ref EntityTerminatingEvent args)
    {
        var spider = foot.Comp.Spider;

        if (TerminatingOrDeleted(spider) || !TryComp<SpiderLegsComponent>(spider, out var legs))
            return;

        if (foot.Comp.LegIndex < 0 || foot.Comp.LegIndex >= legs.Legs.Count)
            return;

        var leg = legs.Legs[foot.Comp.LegIndex];
        if (leg.Severed)
            return;

        leg.Severed = true;
        leg.Foot = NetEntity.Invalid;
        Dirty(spider, legs);
    }

    private void OnSpiderTerminating(Entity<SpiderLegsComponent> ent, ref EntityTerminatingEvent args)
    {
        RemoveDebug(ent.Owner);

        foreach (var leg in ent.Comp.Legs)
        {
            if (leg.Foot == NetEntity.Invalid)
                continue;

            if (TryGetEntity(leg.Foot, out var foot) && Exists(foot))
                QueueDel(foot);

            leg.Foot = NetEntity.Invalid;
        }
    }
}
