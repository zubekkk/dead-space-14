using Robust.Shared.Prototypes;
using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using Content.Shared.Physics;
using Content.Shared.DeadSpace.MangoSpiderBoss.Systems;

namespace Content.Shared.DeadSpace.MangoSpiderBoss.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpiderLegsComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<SpiderLegData> Legs = new();

    [DataField]
    public List<float> BoneLengths = new() { 1.0f, 1.4f };

    [DataField]
    public List<SpiderBoneVisual> BoneVisuals = new();

    [DataField]
    public SpiderBoneVisual FootVisual = new();

    public List<SpiderBoneVisual> ResolveBones(SpiderLegData leg)
        => leg.Bones ?? BoneVisuals;

    public SpiderBoneVisual ResolveFoot(SpiderLegData leg)
        => leg.FootVisual ?? FootVisual;

    [DataField]
    public int
    FabrikIterations = 8;

    [ViewVariables]
    public FabrikChain[]? Chains;

    [DataField]
    public float StepThreshold = 1.5f;

    [DataField]
    public float StepDuration = 0.25f;

    [DataField]
    public SoundSpecifier? FootstepSound;

    [DataField]
    public float FootstepVolume = 0f;

    [DataField]
    public float FootstepVariation = 0.15f;

    [ViewVariables]
    public TimeSpan[]? LastFootstep;

    [DataField]
    public float Lookahead = 0.25f;

    [DataField]
    public float FootDamageShare = 0.3f;

    [DataField]
    public float MaxLookaheadOffset = 0.25f;

    [DataField]
    public float MinFootGap = 0.3f;

    [DataField]
    public float TurnSpeed = 6f;

    [DataField]
    public CollisionGroup FootBlockMask = CollisionGroup.MobMask;

    [DataField]
    public int FootSearchRings = 3;

    [DataField]
    public int FootSearchSamples = 12;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SpiderLegData
{
    [DataField]
    public Vector2 Position;

    [DataField]
    public Vector2 Rest;

    [DataField]
    public int Group;

    [DataField]
    public List<float>? BoneLengths;

    [DataField]
    public List<SpiderBoneVisual>? Bones;

    [DataField]
    public SpiderBoneVisual? FootVisual;

    [DataField]
    public NetEntity Foot = NetEntity.Invalid;

    [DataField]
    public float LegMaxHealth = 40f;

    [DataField]
    public float LegHealth = 40f;

    [DataField]
    public bool Severed;

    [DataField]
    public Vector2 From;

    [DataField]
    public Vector2 To;

    [DataField]
    public TimeSpan StepStart;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SpiderBoneVisual
{
    [DataField]
    public List<SpiderLegVisualLayer> Layers = new();
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SpiderLegVisualLayer
{
    [DataField]
    public SpriteSpecifier Sprite = SpriteSpecifier.Invalid;

    [DataField]
    public Vector2 Offset = Vector2.Zero;

    [DataField]
    public Vector2 Scale = Vector2.One;

    [DataField]
    public bool Visible = true;

    [DataField]
    public string? Shader;
}

[RegisterComponent]
public sealed partial class SpiderFootComponent : Component
{
    public EntityUid Spider;
    public int LegIndex;
}
