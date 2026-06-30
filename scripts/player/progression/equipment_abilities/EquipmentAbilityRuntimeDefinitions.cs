using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public enum EquipmentAbilityBindingOverrideMode
{
    Add,
    ReplaceBinding,
}

public enum EquipmentAbilitySourceTraceKind
{
    ByFamily,
}

public enum EquipmentAbilityCoverageStatus
{
    Bound,
    Deferred,
    ContentCut,
}

public enum EquipmentAbilityContentPhase
{
    V1,
    V2,
    V3,
}

public enum EquipmentAbilityTriggerKind
{
    OnHit,
    OnBattleEnd,
}

public enum EquipmentAbilityTimingKind
{
    BeforeHit,
    AfterHit,
    AfterBattle,
}

public enum EquipmentGrantedActionKind
{
    Skill,
}

public enum EquipmentAbilityHandlerKind
{
    Condition,
    Action,
}

public enum EquipmentAbilityHandlerOriginKind
{
    Builtin,
}

public enum EquipmentAbilityConsumerKind
{
    Execution,
    Preview,
    AiScoring,
    Snapshot,
    Trace,
}

public enum EquipmentAbilityConsumerSupportKind
{
    Exact,
    Approximate,
    TraceOnly,
    UnsupportedBlocking,
    UnsupportedIgnored,
}

public enum EquipmentAbilityPreviewRollPolicyKind
{
    None,
    ExpectedValue,
}

public enum EquipmentAbilityUnsupportedConsumerPolicyKind
{
    RejectContent,
    Ignore,
}

public enum EquipmentAbilityMutationPolicyKind
{
    None,
    Mutating,
}

public enum EquipmentAbilityStateOwnerKind
{
    BindingState,
    TargetMark,
}

public enum EquipmentAbilityStateValueKind
{
    Int,
    Flag,
}

public enum EquipmentAbilityStateLifetimeKind
{
    Battle,
    PersistentEquipmentInstance,
}

public sealed class EquipmentAbilityContentPackDefinition
{
    public StringName PackId { get; init; } = "";
    public int SchemaVersion { get; init; }
    public int LoadOrder { get; init; }
    public IReadOnlyList<StringName> Dependencies { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<EquipmentAbilityBindingDefinition> Bindings { get; init; } =
        Array.Empty<EquipmentAbilityBindingDefinition>();
    public string ResourcePath { get; init; } = "";
}

public sealed class EquipmentAbilityBindingDefinition
{
    public StringName BindingId { get; init; } = "";
    public StringName TraitId { get; init; } = "";
    public EquipmentAbilityBindingOverrideMode OverrideMode { get; init; }
    public StringName ReplacesBindingId { get; init; } = "";
    public IReadOnlySet<StringName> AllowedSourceKinds { get; init; } =
        new HashSet<StringName>();
    public IReadOnlySet<StringName> RequiredTraitCategories { get; init; } =
        new HashSet<StringName>();
    public IReadOnlySet<StringName> RequiredItemTags { get; init; } = new HashSet<StringName>();
    public IReadOnlySet<StringName> SupportedEquipmentTypeIds { get; init; } =
        new HashSet<StringName>();
    public IReadOnlyList<EquipmentAbilitySourceTraceDefinition> SourceTraces { get; init; } =
        Array.Empty<EquipmentAbilitySourceTraceDefinition>();
    public IReadOnlyList<EquipmentAbilityStateSchemaDefinition> StateSchemas { get; init; } =
        Array.Empty<EquipmentAbilityStateSchemaDefinition>();
    public IReadOnlyList<EquipmentAbilityReactionDefinition> Reactions { get; init; } =
        Array.Empty<EquipmentAbilityReactionDefinition>();
    public IReadOnlyList<EquipmentGrantedActionDefinition> GrantedActions { get; init; } =
        Array.Empty<EquipmentGrantedActionDefinition>();
    public IReadOnlyList<EquipmentWeaponProfileOverlayDefinition> WeaponProfileOverlays { get; init; } =
        Array.Empty<EquipmentWeaponProfileOverlayDefinition>();
    public IReadOnlyList<EquipmentWorldEffectDefinition> WorldEffects { get; init; } =
        Array.Empty<EquipmentWorldEffectDefinition>();
    public string ResourcePath { get; init; } = "";
}

public sealed class EquipmentAbilitySourceTraceDefinition
{
    public EquipmentAbilitySourceTraceKind SourceKind { get; init; }
    public string SourceFile { get; init; } = "";
    public StringName ItemId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int BulletIndex { get; init; }
    public string BulletTitle { get; init; } = "";
    public string BulletText { get; init; } = "";
    public StringName MechanismFamily { get; init; } = "";
    public EquipmentAbilityCoverageStatus CoverageStatus { get; init; }
    public EquipmentAbilityContentPhase Phase { get; init; }
    public StringName TestId { get; init; } = "";
    public string Note { get; init; } = "";
}

public sealed class EquipmentAbilityReactionDefinition
{
    public StringName ReactionId { get; init; } = "";
    public EquipmentAbilityTriggerKind Trigger { get; init; }
    public EquipmentAbilityTimingKind Timing { get; init; }
    public int Priority { get; init; }
    public StringName OnceScope { get; init; } = "";
    public bool RequiresPlayerConfirmation { get; init; }
    public EquipmentConditionGroupDefinition ConditionGroup { get; init; }
    public EquipmentRollGateDefinition RollGate { get; init; }
    public EquipmentOutcomeTableDefinition OutcomeTable { get; init; }
    public IReadOnlyList<EquipmentAbilityActionDefinition> Actions { get; init; } =
        Array.Empty<EquipmentAbilityActionDefinition>();
}

public sealed class EquipmentConditionGroupDefinition
{
    public StringName Mode { get; init; } = "";
    public bool Negate { get; init; }
    public IReadOnlyList<EquipmentAbilityConditionDefinition> Conditions { get; init; } =
        Array.Empty<EquipmentAbilityConditionDefinition>();
    public IReadOnlyList<EquipmentConditionGroupDefinition> Groups { get; init; } =
        Array.Empty<EquipmentConditionGroupDefinition>();
}

public sealed class EquipmentAbilityConditionDefinition
{
    public StringName ConditionId { get; init; } = "";
    public StringName Kind { get; init; } = "";
    public EquipmentAbilityConditionPayloadDefinition PayloadDefinition { get; init; }
}

public abstract class EquipmentAbilityConditionPayloadDefinition { }

public sealed class HasStatusConditionPayloadDefinition
    : EquipmentAbilityConditionPayloadDefinition
{
    public StringName Subject { get; init; } = "";
    public StringName StatusId { get; init; } = "";
}

public sealed class CompareFactConditionPayloadDefinition
    : EquipmentAbilityConditionPayloadDefinition
{
    public EquipmentAbilityFactQueryDefinition Left { get; init; }
    public StringName Compare { get; init; } = "";
    public EquipmentAbilityFactQueryDefinition Right { get; init; }
}

public sealed class HasEquipmentTagConditionPayloadDefinition
    : EquipmentAbilityConditionPayloadDefinition
{
    public StringName Subject { get; init; } = "";
    public StringName EquipmentSelector { get; init; } = "";
    public IReadOnlyList<StringName> AllTags { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<StringName> AnyTags { get; init; } = Array.Empty<StringName>();
}

public sealed class EquipmentAbilityFactQueryDefinition
{
    public StringName QueryKind { get; init; } = "";
    public StringName FactId { get; init; } = "";
    public StringName Subject { get; init; } = "";
    public StringName Aggregation { get; init; } = "";
    public StringName ValueKind { get; init; } = "";
    public bool BoolLiteral { get; init; }
    public int IntLiteral { get; init; }
    public float FloatLiteral { get; init; }
    public StringName StringNameLiteral { get; init; } = "";
}

public sealed class EquipmentAbilityActionDefinition
{
    public StringName ActionId { get; init; } = "";
    public StringName Kind { get; init; } = "";
    public EquipmentAbilityActionPayloadDefinition PayloadDefinition { get; init; }
    public EquipmentConditionGroupDefinition ConditionGroup { get; init; }
    public EquipmentRollGateDefinition RollGate { get; init; }
}

public abstract class EquipmentAbilityActionPayloadDefinition { }

public sealed class AddDamageDiceActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public DiceExpressionDefinition Dice { get; init; }
    public StringName DamageType { get; init; } = "";
    public IReadOnlyList<StringName> DamageTags { get; init; } = Array.Empty<StringName>();
}

public sealed class ApplyStatusActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public StringName StatusId { get; init; } = "";
    public int DurationTurns { get; init; }
    public int StackDelta { get; init; }
}

public sealed class ModifyAbilityStateActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public StringName StateKey { get; init; } = "";
    public StringName Operation { get; init; } = "";
    public int IntDelta { get; init; }
}

public sealed class MarkTargetActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public StringName StateKey { get; init; } = "";
    public int StackDelta { get; init; }
    public bool RemoveOnSourceMissing { get; init; }
}

public sealed class GrantSkillActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName SkillId { get; init; } = "";
    public int SkillLevel { get; init; }
    public StringName AvailabilityStateKey { get; init; } = "";
}

public sealed class EquipmentDurabilityDamageActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public IReadOnlyList<StringName> TargetSlots { get; init; } = Array.Empty<StringName>();
    public IReadOnlyDictionary<StringName, int> SlotWeightMap { get; init; } =
        new ReadOnlyDictionary<StringName, int>(new Dictionary<StringName, int>());
    public IReadOnlyList<StringName> RequiredItemTags { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<StringName> RequiredEquipmentTypeIds { get; init; } =
        Array.Empty<StringName>();
    public int DurabilityLoss { get; init; }
    public StringName SaveTag { get; init; } = "";
    public int SaveDc { get; init; }
    public bool RequireAttackSuccess { get; init; }
    public int MaxDamagedItems { get; init; }
}

public sealed class DiceExpressionDefinition
{
    public IReadOnlyList<DiceExpressionTermDefinition> Terms { get; init; } =
        Array.Empty<DiceExpressionTermDefinition>();
    public int FlatBonus { get; init; }
    public StringName PreviewPolicy { get; init; } = "";
}

public sealed class DiceExpressionTermDefinition
{
    public int DiceCount { get; init; }
    public int DiceSides { get; init; }
    public EquipmentAbilityFactQueryDefinition CountBonusFact { get; init; }
    public float CountBonusMultiplier { get; init; }
    public int MaxDiceCount { get; init; }
}

public sealed class EquipmentGrantedActionDefinition
{
    public StringName GrantedActionId { get; init; } = "";
    public EquipmentGrantedActionKind GrantedKind { get; init; }
    public StringName SkillId { get; init; } = "";
    public int SkillLevel { get; init; }
    public StringName DisplayCategory { get; init; } = "";
    public int DisplayPriority { get; init; }
    public EquipmentConditionGroupDefinition AvailabilityConditions { get; init; }
    public string ResourcePath { get; init; } = "";
}

public sealed class EquipmentWeaponProfileOverlayDefinition
{
    public StringName OverlayId { get; init; } = "";
    public int Priority { get; init; }
    public EquipmentConditionGroupDefinition ConditionGroup { get; init; }
    public bool RequireEquippedWeapon { get; init; }
    public IReadOnlySet<StringName> RequiredWeaponFamilies { get; init; } =
        new HashSet<StringName>();
    public IReadOnlySet<StringName> RequiredWeaponTypeIds { get; init; } =
        new HashSet<StringName>();
    public int AttackRangeDelta { get; init; }
    public int MinAttackRange { get; init; }
    public int MaxAttackRange { get; init; }
    public EquipmentWeaponDiceOverlayDefinition OneHandedDiceOverlay { get; init; }
    public EquipmentWeaponDiceOverlayDefinition TwoHandedDiceOverlay { get; init; }
    public StringName PhysicalDamageTagOverride { get; init; } = "";
    public StringName GripOverride { get; init; } = "";
    public bool UsesTwoHandsOverride { get; init; }
    public bool IsVersatileOverride { get; init; }
    public string ResourcePath { get; init; } = "";
}

public sealed class EquipmentWeaponDiceOverlayDefinition
{
    public StringName Mode { get; init; } = "";
    public int DiceCountDelta { get; init; }
    public int DiceSidesOverride { get; init; }
    public int FlatBonusDelta { get; init; }
    public DiceExpressionDefinition DiceOverride { get; init; }
}

public sealed class EquipmentWorldEffectDefinition
{
    public StringName WorldEffectId { get; init; } = "";
    public EquipmentAbilityTriggerKind Trigger { get; init; }
    public EquipmentAbilityTimingKind Timing { get; init; }
    public EquipmentConditionGroupDefinition ConditionGroup { get; init; }
    public IReadOnlyList<EquipmentAbilityActionDefinition> Actions { get; init; } =
        Array.Empty<EquipmentAbilityActionDefinition>();
}

public sealed class EquipmentAbilityStateSchemaDefinition
{
    public StringName StateKey { get; init; } = "";
    public StringName OwnerScope { get; init; } = "";
    public StringName ValueKind { get; init; } = "";
    public int InitialIntValue { get; init; }
    public int MaxIntValue { get; init; }
    public StringName ResetTiming { get; init; } = "";
    public bool PersistOutsideBattle { get; init; }
    public bool VisibleToUi { get; init; }
}

public sealed class EquipmentRollGateDefinition
{
    public StringName RngStream { get; init; } = "";
    public DiceExpressionDefinition Roll { get; init; }
    public StringName Compare { get; init; } = "";
    public int Threshold { get; init; }
}

public sealed class EquipmentOutcomeTableDefinition
{
    public StringName TableId { get; init; } = "";
    public DiceExpressionDefinition Roll { get; init; }
    public IReadOnlyList<EquipmentOutcomeEntryDefinition> Entries { get; init; } =
        Array.Empty<EquipmentOutcomeEntryDefinition>();
}

public sealed class EquipmentOutcomeEntryDefinition
{
    public int MinRoll { get; init; }
    public int MaxRoll { get; init; }
    public IReadOnlyList<EquipmentAbilityActionDefinition> Actions { get; init; } =
        Array.Empty<EquipmentAbilityActionDefinition>();
}

public sealed class EquipmentAbilityRegistryBuildResult
{
    public bool Success { get; init; }
    public int Revision { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class EquipmentAbilityContentValidationContext
{
    public IReadOnlyDictionary<StringName, TraitDef> TraitDefs { get; init; } =
        new ReadOnlyDictionary<StringName, TraitDef>(new Dictionary<StringName, TraitDef>());
    public IReadOnlyDictionary<StringName, ItemDef> ItemDefs { get; init; } =
        new ReadOnlyDictionary<StringName, ItemDef>(new Dictionary<StringName, ItemDef>());
    public IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; init; } =
        new ReadOnlyDictionary<StringName, SkillDefinition>(
            new Dictionary<StringName, SkillDefinition>()
        );
    public IReadOnlySet<StringName> KnownCreatureTypeTags { get; init; } = new HashSet<StringName>();
    public IReadOnlySet<StringName> KnownBattleEnvironmentTags { get; init; } =
        new HashSet<StringName>();
    public IReadOnlySet<StringName> KnownStatusIds { get; init; } = new HashSet<StringName>();
    public IReadOnlySet<StringName> KnownDamageTypes { get; init; } = new HashSet<StringName>();
    public IReadOnlySet<StringName> KnownEquipmentSlotIds { get; init; } = new HashSet<StringName>();
}

public sealed class EquipmentAbilityHandlerSpec
{
    public StringName HandlerId { get; init; } = "";
    public EquipmentAbilityHandlerKind HandlerKind { get; init; }
    public int HandlerVersion { get; init; } = 1;
    public EquipmentAbilityHandlerOriginKind Origin { get; init; } =
        EquipmentAbilityHandlerOriginKind.Builtin;
    public Type PayloadResourceType { get; init; }
    public Type PayloadDefinitionType { get; init; }
    public EquipmentAbilityMutationPolicyKind MutationPolicy { get; init; }
    public IReadOnlyDictionary<EquipmentAbilityConsumerKind, EquipmentAbilityConsumerSupportSpec> ConsumerSupport { get; init; } =
        new ReadOnlyDictionary<
            EquipmentAbilityConsumerKind,
            EquipmentAbilityConsumerSupportSpec
        >(new Dictionary<EquipmentAbilityConsumerKind, EquipmentAbilityConsumerSupportSpec>());
    public EquipmentAbilityStateAccessSpec StateAccess { get; init; } =
        EquipmentAbilityStateAccessSpec.Empty;
}

public sealed class EquipmentAbilityConsumerSupportSpec
{
    public EquipmentAbilityConsumerKind Consumer { get; init; }
    public EquipmentAbilityConsumerSupportKind SupportKind { get; init; }
    public EquipmentAbilityPreviewRollPolicyKind RollPolicy { get; init; }
    public EquipmentAbilityUnsupportedConsumerPolicyKind UnsupportedPolicy { get; init; }
}

public sealed class EquipmentAbilityStateAccessSpec
{
    public static readonly EquipmentAbilityStateAccessSpec Empty = new();

    public IReadOnlyList<EquipmentAbilityStateContract> Reads { get; init; } =
        Array.Empty<EquipmentAbilityStateContract>();
    public IReadOnlyList<EquipmentAbilityStateContract> Writes { get; init; } =
        Array.Empty<EquipmentAbilityStateContract>();
    public IReadOnlyList<EquipmentAbilityStateContract> Creates { get; init; } =
        Array.Empty<EquipmentAbilityStateContract>();
    public IReadOnlyList<EquipmentAbilityStateContract> Clears { get; init; } =
        Array.Empty<EquipmentAbilityStateContract>();
}

public sealed class EquipmentAbilityStateContract
{
    public EquipmentAbilityStateOwnerKind OwnerKind { get; init; }
    public EquipmentAbilityStateValueKind ValueKind { get; init; }
    public EquipmentAbilityStateLifetimeKind LifetimeKind { get; init; }
    public StringName StateKey { get; init; } = "";
    public bool StateKeyMustBeDeclaredInBinding { get; init; }
    public bool SourceLifecycleCleanupRequired { get; init; }
}
