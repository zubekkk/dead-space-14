using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.MangoSpiderBoss.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class SpiderMissilesLauncherComponent : Component
{
    [DataField]
    public List<MissilesEffectData> MissilesEffects = new();

    [DataField]
    public List<MissilesEffectData> NapalmMissilesEffects = new();

    [DataField]
    public SoundSpecifier? MissilesStartLaunchSound;

    [DataField]
    public SoundSpecifier? MissilesCloseSound;

    [DataField]
    public EntProtoId MissilesPrototype = "SpiderMissileMarker";

    [DataField]
    public EntProtoId NapalmMissilesPrototype = "SpiderNapalmMissileMarker";

    [DataField]
    public float MissileSpawnRadius = 1f;

    [DataField]
    public EntProtoId<ActionComponent> MissileLaunchAction = "MissileLaunchAction";

    [DataField]
    public EntProtoId<ActionComponent> NapalmMissileLaunchAction = "NapalmMissileLaunchAction";

    [DataField, AutoNetworkedField]
    public EntityUid? MissileLaunchActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? NapalmMissileLaunchActionEntity;

    [DataField]
    public TimeSpan StartLauncherTime = TimeSpan.FromSeconds(1f);

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan EndStartLauncherTime = TimeSpan.Zero;

    [DataField]
    public TimeSpan CloseLauncherTime = TimeSpan.FromSeconds(1f);

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan EndCloseLauncherTime = TimeSpan.Zero;

    [DataField]
    public TimeSpan MissileLaunchInterval = TimeSpan.FromSeconds(0.25f);

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan NextLaunchTime = TimeSpan.Zero;

    [DataField]
    public int MissileLaunchCount = 4;

    [DataField]
    public int NapalmMissileLaunchCount = 4;

    [DataField]
    public int MissileLaunchRemaining;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public bool IsLaunchingNapalm;

    [DataField, AutoNetworkedField]
    public SpiderLauncherPhase Phase = SpiderLauncherPhase.Idle;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class MissilesEffectData
{
    [DataField]
    public Vector2 Offset;

    [DataField]
    public float Rotation;

    [DataField]
    public EntProtoId Prototype;

    [DataField]
    public bool ParentToLauncher = true;
}

public sealed partial class LaunchSpiderMissilesActionEvent : InstantActionEvent { }

public sealed partial class LaunchSpiderNapalmMissilesActionEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public enum SpiderBossMissiles
{
    Animation,
}

[Serializable, NetSerializable]
public enum SpiderLauncherPhase : byte
{
    Idle,
    Starting,
    Firing,
    Closing
}