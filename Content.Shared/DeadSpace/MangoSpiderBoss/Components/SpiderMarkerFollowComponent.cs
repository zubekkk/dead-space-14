using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Map;

namespace Content.Shared.DeadSpace.MangoSpiderBoss.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpiderMarkerFollowComponent : Component
{
    [DataField]
    public float Speed = 2.5f;

    [DataField]
    public float TurnSpeed = 3f;

    [DataField]
    public float StopDistance = 0.5f;

    [DataField]
    public float TurnGain = 4f;

    [DataField]
    public bool AutoTurnToTarget = false;

    [ViewVariables]
    public bool RotateRight;

    [ViewVariables]
    public bool RotateManually;

    [DataField]
    public EntProtoId MarkerPrototype = "SpiderMarker";

    [ViewVariables] public MapCoordinates? Target;
}

[Serializable, NetSerializable]
public sealed class SpiderSetTargetEvent : EntityEventArgs
{
    public readonly NetCoordinates Target;

    public SpiderSetTargetEvent(NetCoordinates target)
    {
        Target = target;
    }
}

[Serializable, NetSerializable]
public sealed class SpiderStartRotationEvent : EntityEventArgs
{
    public readonly bool Right;

    public SpiderStartRotationEvent(bool right)
    {
        Right = right;
    }
}

[Serializable, NetSerializable]
public sealed class SpiderStopRotationEvent : EntityEventArgs { }