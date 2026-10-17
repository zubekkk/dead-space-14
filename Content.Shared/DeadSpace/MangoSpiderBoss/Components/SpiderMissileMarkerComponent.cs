using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.MangoSpiderBoss.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpiderMissileMarkerComponent : Component
{
    [DataField]
    public float Radius = 6f;

    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public EntProtoId? SpawnPrototype;

    [DataField]
    public TimeSpan AttachDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan SpawnDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan DeleteDelay = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public SpiderMissileMarkerPhase Phase = SpiderMissileMarkerPhase.Following;

    [ViewVariables]
    public TimeSpan NextPhaseTime;
}

[Serializable, NetSerializable]
public enum SpiderMissileMarkerPhase : byte
{
    Following,
    Spawning,
    Deleting,
}
