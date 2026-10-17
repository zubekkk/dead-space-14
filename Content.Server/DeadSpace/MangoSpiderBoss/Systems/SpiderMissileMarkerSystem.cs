using Content.Shared.DeadSpace.MangoSpiderBoss.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server.DeadSpace.MangoSpiderBoss.Systems;

public sealed class SpiderMissileMarkerSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly HashSet<EntityUid> _targets = new();
    private readonly List<EntityUid> _valid = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpiderMissileMarkerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SpiderMissileMarkerComponent> ent, ref MapInitEvent args)
    {
        var target = FindTarget(ent);
        if (target is { } targetUid)
        {
            ent.Comp.Target = targetUid;
            _transform.SetParent(ent.Owner, targetUid);
            _transform.SetLocalPosition(ent.Owner, Vector2.Zero);
        }

        ent.Comp.Phase = SpiderMissileMarkerPhase.Following;
        ent.Comp.NextPhaseTime = _timing.CurTime + ent.Comp.AttachDuration;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SpiderMissileMarkerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (curTime < comp.NextPhaseTime)
                continue;

            switch (comp.Phase)
            {
                case SpiderMissileMarkerPhase.Following:
                    Detach(uid, comp);
                    comp.Phase = SpiderMissileMarkerPhase.Spawning;
                    comp.NextPhaseTime = curTime + comp.SpawnDelay;
                    break;

                case SpiderMissileMarkerPhase.Spawning:
                    SpawnPayload(uid, comp);
                    comp.Phase = SpiderMissileMarkerPhase.Deleting;
                    comp.NextPhaseTime = curTime + comp.DeleteDelay;
                    break;

                case SpiderMissileMarkerPhase.Deleting:
                    QueueDel(uid);
                    break;
            }
        }
    }

    private EntityUid? FindTarget(Entity<SpiderMissileMarkerComponent> ent)
    {
        _targets.Clear();
        _lookup.GetEntitiesInRange(ent.Owner, ent.Comp.Radius, _targets, LookupFlags.Dynamic | LookupFlags.Sundries);

        _valid.Clear();
        foreach (var target in _targets)
        {
            if (!HasComp<MobStateComponent>(target))
                continue;

            if (!_mobState.IsAlive(target))
                continue;

            if (_whitelist.IsWhitelistPass(ent.Comp.Blacklist, target))
                continue;

            _valid.Add(target);
        }

        return _valid.Count == 0 ? null : _random.Pick(_valid);
    }

    private void Detach(EntityUid uid, SpiderMissileMarkerComponent comp)
    {
        comp.Target = null;

        if (TerminatingOrDeleted(uid) || !TryComp(uid, out TransformComponent? xform))
            return;

        if (xform.ParentUid.IsValid())
            _transform.AttachToGridOrMap(uid, xform);
    }

    private void SpawnPayload(EntityUid uid, SpiderMissileMarkerComponent comp)
    {
        if (comp.SpawnPrototype is not { } proto || TerminatingOrDeleted(uid))
            return;

        Spawn(proto, _transform.GetMapCoordinates(uid));
    }
}
