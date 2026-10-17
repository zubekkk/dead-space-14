using Content.Shared.DeadSpace.MangoSpiderBoss.Components;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Map;

namespace Content.Shared.DeadSpace.MangoSpiderBoss.Systems;

public sealed class SpiderMissilesLauncherSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpiderMissilesLauncherComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SpiderMissilesLauncherComponent, LaunchSpiderMissilesActionEvent>(OnLaunchSpiderMissilesAction);
        SubscribeLocalEvent<SpiderMissilesLauncherComponent, LaunchSpiderNapalmMissilesActionEvent>(OnLaunchSpiderNapalmMissilesAction);
    }

    private void OnMapInit(Entity<SpiderMissilesLauncherComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.MissileLaunchActionEntity, ent.Comp.MissileLaunchAction);
        _actions.AddAction(ent.Owner, ref ent.Comp.NapalmMissileLaunchActionEntity, ent.Comp.NapalmMissileLaunchAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SpiderMissilesLauncherComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            switch (comp.Phase)
            {
                case SpiderLauncherPhase.Idle:
                    continue;

                case SpiderLauncherPhase.Starting:
                    if (curTime <= comp.EndStartLauncherTime)
                        break;

                    comp.Phase = SpiderLauncherPhase.Firing;
                    comp.NextLaunchTime = curTime + comp.MissileLaunchInterval;
                    _appearance.SetData(uid, SpiderBossMissiles.Animation, SpiderLauncherPhase.Firing);
                    Dirty(uid, comp);
                    break;

                case SpiderLauncherPhase.Firing:
                    if (comp.MissileLaunchRemaining > 0)
                    {
                        if (curTime <= comp.NextLaunchTime)
                            break;

                        var effects = comp.IsLaunchingNapalm ? comp.NapalmMissilesEffects : comp.MissilesEffects;
                        foreach (var effect in effects)
                            SpawnEffect(uid, effect);

                        SpawnMissile(uid, comp);

                        comp.MissileLaunchRemaining--;
                        comp.NextLaunchTime = curTime + comp.MissileLaunchInterval;
                        Dirty(uid, comp);
                        break;
                    }

                    comp.Phase = SpiderLauncherPhase.Closing;
                    comp.EndCloseLauncherTime = curTime + comp.CloseLauncherTime;
                    _appearance.SetData(uid, SpiderBossMissiles.Animation, SpiderLauncherPhase.Closing);
                    _audioSystem.PlayPvs(comp.MissilesCloseSound, uid);
                    Dirty(uid, comp);
                    break;

                case SpiderLauncherPhase.Closing:
                    if (curTime <= comp.EndCloseLauncherTime)
                        break;

                    comp.Phase = SpiderLauncherPhase.Idle;
                    _appearance.SetData(uid, SpiderBossMissiles.Animation, SpiderLauncherPhase.Idle);
                    Dirty(uid, comp);
                    break;
            }
        }
    }

    private void OnLaunchSpiderMissilesAction(Entity<SpiderMissilesLauncherComponent> ent, ref LaunchSpiderMissilesActionEvent args)
    {
        if (!TryStartLaunch(ent, args.Performer, false))
            return;

        args.Handled = true;
    }

    private void OnLaunchSpiderNapalmMissilesAction(Entity<SpiderMissilesLauncherComponent> ent, ref LaunchSpiderNapalmMissilesActionEvent args)
    {
        if (!TryStartLaunch(ent, args.Performer, true))
            return;

        args.Handled = true;
    }

    private bool TryStartLaunch(Entity<SpiderMissilesLauncherComponent> ent, EntityUid performer, bool isNapalm)
    {
        if (ent.Comp.Phase != SpiderLauncherPhase.Idle)
        {
            _popup.PopupClient("Запуск уже происходит.", ent.Owner, performer, PopupType.MediumCaution);
            return false;
        }

        if (_net.IsClient)
            return true;

        var comp = ent.Comp;
        comp.Phase = SpiderLauncherPhase.Starting;
        comp.IsLaunchingNapalm = isNapalm;
        comp.MissileLaunchRemaining = isNapalm ? comp.NapalmMissileLaunchCount : comp.MissileLaunchCount;
        comp.EndStartLauncherTime = _timing.CurTime + comp.StartLauncherTime;

        _appearance.SetData(ent.Owner, SpiderBossMissiles.Animation, SpiderLauncherPhase.Starting);
        _audioSystem.PlayPvs(comp.MissilesStartLaunchSound, ent.Owner);
        Dirty(ent);
        return true;
    }

    private void SpawnMissile(EntityUid uid, SpiderMissilesLauncherComponent comp)
    {
        var prototype = comp.IsLaunchingNapalm ? comp.NapalmMissilesPrototype : comp.MissilesPrototype;
        var offset = new EntityCoordinates(uid, _random.NextVector2(comp.MissileSpawnRadius));
        Spawn(prototype, _transform.ToMapCoordinates(offset));
    }

    private void SpawnEffect(EntityUid uid, MissilesEffectData effectData)
    {
        var offset = new EntityCoordinates(uid, effectData.Offset);

        EntityUid effect;

        if (effectData.ParentToLauncher)
            effect = SpawnAttachedTo(effectData.Prototype, offset);
        else
            effect = Spawn(effectData.Prototype, _transform.ToMapCoordinates(offset));

        _transform.SetWorldRotation(effect, _transform.GetWorldRotation(Transform(uid)) + Angle.FromDegrees(effectData.Rotation));
    }
}