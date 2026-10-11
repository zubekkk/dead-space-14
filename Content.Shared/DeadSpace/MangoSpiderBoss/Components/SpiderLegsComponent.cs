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

    // Визуал сегментов ноги по умолчанию (по одному на сегмент) — рисуется только клиентом.
    // Отдельная нога может переопределить его через SpiderLegData.Bones.
    [DataField]
    public List<SpiderBoneVisual> BoneVisuals = new();

    // Визуал ступни по умолчанию. Рисуется фут-сущностью (её спрайт краснеет при попадании).
    // Отдельная нога может переопределить его через SpiderLegData.FootVisual.
    [DataField]
    public SpiderBoneVisual FootVisual = new();

    public List<SpiderBoneVisual> ResolveBones(SpiderLegData leg)
        => leg.Bones ?? BoneVisuals;

    public SpiderBoneVisual ResolveFoot(SpiderLegData leg)
        => leg.FootVisual ?? FootVisual;

    [DataField]
    public int
    FabrikIterations = 8;

    // Только сервер, по сети не ходит
    [ViewVariables] public FabrikChain[]? Chains;

    [DataField]
    public float StepThreshold = 1.5f;

    [DataField]
    public float StepDuration = 0.25f;

    // Звук шага при посадке ноги. Если задан, играется от каждой ступни отдельно.
    [DataField]
    public SoundSpecifier? FootstepSound;

    // Громкость шага в дБ, добавляется к громкости звука.
    [DataField]
    public float FootstepVolume = 0f;

    // Разброс громкости/тона шага, если у звука не задан свой.
    [DataField]
    public float FootstepVariation = 0.15f;

    // Только сервер: время последнего озвученного шага для каждой ноги, чтобы не дублировать звук.
    [ViewVariables] public TimeSpan[]? LastFootstep;

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

    // Необязательное переопределение визуала сегментов именно для этой ноги.
    [DataField]
    public List<SpiderBoneVisual>? Bones;

    // Необязательное переопределение визуала ступни именно для этой ноги.
    [DataField]
    public SpiderBoneVisual? FootVisual;

    // Невидимая фут-сущность, несущая фикстуру для попаданий.
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

// Описание спрайта кости/ступни как набора слоёв. Спрайты ног существуют только на клиенте.
[DataDefinition, Serializable, NetSerializable]
public sealed partial class SpiderBoneVisual
{
    [DataField]
    public List<SpiderLegVisualLayer> Layers = new();
}

// Один слой визуала: SpriteSpecifier (RSI+state или текстура) плюс смещение/масштаб.
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

    // Id ShaderPrototype (например "unshaded"). Пусто — шейдер по умолчанию.
    [DataField]
    public string? Shader;
}

[RegisterComponent]
public sealed partial class SpiderFootComponent : Component
{
    public EntityUid Spider;
    public int LegIndex;
}
