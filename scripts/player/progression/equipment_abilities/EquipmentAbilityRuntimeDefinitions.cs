using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public enum EquipmentAbilityBindingOverrideMode
{
    Add,
    ReplaceBinding,
}

public enum EquipmentAbilityTriggerKind
{
    OnHit,
    OnKill,
    OnBattleEnd,
    OnGrantedSkillUsed,
    OnTurnEnd,
    OnDamageRoll,
    OnDamageApplied,
    OnHitReceived,
    OnAttackCheck,
    OnTargetMarkExpired,
}

public enum EquipmentAbilityTimingKind
{
    BeforeHit,
    AfterHit,
    AfterKill,
    AfterBattle,
    AfterSkill,
    AfterTurn,
    BeforeDamage,
    AfterDamage,
    AfterHitReceived,
    AfterAttackCheck,
    AfterStatusExpired,
}

public enum EquipmentGrantedActionKind
{
    Skill,
}

public enum EquipmentAbilityUsagePeriodKind
{
    None,
    PerBattle,
    PerWorldDay,
    PerWorldMonth,
}

internal static class EquipmentAbilityUsagePeriodKinds
{
    internal static readonly StringName PerBattle = "per_battle";
    internal static readonly StringName PerWorldDay = "per_world_day";
    internal static readonly StringName PerWorldMonth = "per_world_month";

    internal static bool IsLimited(EquipmentAbilityUsagePeriodKind kind) =>
        kind != EquipmentAbilityUsagePeriodKind.None;

    internal static StringName ToStringName(EquipmentAbilityUsagePeriodKind kind) =>
        kind switch
        {
            EquipmentAbilityUsagePeriodKind.PerBattle => PerBattle,
            EquipmentAbilityUsagePeriodKind.PerWorldDay => PerWorldDay,
            EquipmentAbilityUsagePeriodKind.PerWorldMonth => PerWorldMonth,
            _ => new StringName(""),
        };

    internal static bool TryParse(
        StringName value,
        out EquipmentAbilityUsagePeriodKind kind
    )
    {
        StringName normalized = ProgressionDataUtils.to_string_name(value);
        if (normalized == "")
        {
            kind = EquipmentAbilityUsagePeriodKind.None;
            return true;
        }
        if (normalized == PerWorldDay)
        {
            kind = EquipmentAbilityUsagePeriodKind.PerWorldDay;
            return true;
        }
        if (normalized == PerBattle)
        {
            kind = EquipmentAbilityUsagePeriodKind.PerBattle;
            return true;
        }
        if (normalized == PerWorldMonth)
        {
            kind = EquipmentAbilityUsagePeriodKind.PerWorldMonth;
            return true;
        }
        kind = EquipmentAbilityUsagePeriodKind.None;
        return false;
    }
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
        EquipmentAbilityReadOnlySet<StringName>.Empty;
    public IReadOnlySet<StringName> RequiredTraitCategories { get; init; } =
        EquipmentAbilityReadOnlySet<StringName>.Empty;
    public IReadOnlySet<StringName> RequiredItemTags { get; init; } =
        EquipmentAbilityReadOnlySet<StringName>.Empty;
    public IReadOnlySet<StringName> SupportedEquipmentTypeIds { get; init; } =
        EquipmentAbilityReadOnlySet<StringName>.Empty;
    public IReadOnlyList<EquipmentAbilityStateSchemaDefinition> StateSchemas { get; init; } =
        Array.Empty<EquipmentAbilityStateSchemaDefinition>();
    public IReadOnlyList<EquipmentAbilityReactionDefinition> Reactions { get; init; } =
        Array.Empty<EquipmentAbilityReactionDefinition>();
    public IReadOnlyList<EquipmentGrantedActionDefinition> GrantedActions { get; init; } =
        Array.Empty<EquipmentGrantedActionDefinition>();
    public IReadOnlyList<EquipmentTemporalProgressModifierDefinition> TemporalProgressModifiers { get; init; } =
        Array.Empty<EquipmentTemporalProgressModifierDefinition>();
    public IReadOnlyList<EquipmentWeaponProfileOverlayDefinition> WeaponProfileOverlays { get; init; } =
        Array.Empty<EquipmentWeaponProfileOverlayDefinition>();
    public IReadOnlyList<EquipmentWorldEffectDefinition> WorldEffects { get; init; } =
        Array.Empty<EquipmentWorldEffectDefinition>();
    public string ResourcePath { get; init; } = "";
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
    public StringName BindingId { get; init; } = "";
    public StringName StateKey { get; init; } = "";
    public StringName StatusId { get; init; } = "";
    public bool RequireSourceUnitMatch { get; init; }
    public StringName AttributeId { get; init; } = "";
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
    public bool Subtract { get; init; }
    public IReadOnlyList<StringName> DamageTags { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<StringName> MitigationBypassDamageTags { get; init; } =
        Array.Empty<StringName>();
    public IReadOnlyList<StringName> MitigationBypassTiers { get; init; } =
        Array.Empty<StringName>();
}

public sealed class ImmediateWeaponAttackActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName AnchorSelector { get; init; } = "";
    public StringName TargetTeamFilter { get; init; } = "";
    public int Radius { get; init; }
    public int MaxAttacks { get; init; }
    public StringName SkillId { get; init; } = "";
    public bool RequireWeaponRange { get; init; }
}

public sealed class DealDamageActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public DiceExpressionDefinition Dice { get; init; }
    public StringName DamageType { get; init; } = "";
    public IReadOnlyList<StringName> DamageTags { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<StringName> MitigationBypassDamageTags { get; init; } =
        Array.Empty<StringName>();
    public IReadOnlyList<StringName> MitigationBypassTiers { get; init; } =
        Array.Empty<StringName>();
}

public sealed class HealActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public DiceExpressionDefinition Dice { get; init; }
}

public sealed class HealFromFactActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public EquipmentAbilityFactQueryDefinition AmountFact { get; init; }
    public int MultiplierPercent { get; init; }
    public int MaxAmount { get; init; }
}

public sealed class AttackRollBonusActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public int Bonus { get; init; }
    public StringName AttributeModifierId { get; init; } = "";
    public StringName StackMode { get; init; } = "";
    public string Label { get; init; } = "";
    public bool RequireWeaponDamage { get; init; }
}

public sealed class AttackRollAdvantageActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public StringName Mode { get; init; } = "";
    public StringName StackMode { get; init; } = "";
    public string Label { get; init; } = "";
}

public sealed class CriticalHitOverrideActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public bool RequireWeaponDamage { get; init; }
    public string Label { get; init; } = "";
}

public sealed class EquipmentAttackDefenseModifierDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName ModifierId { get; init; } = "";
    public IReadOnlyList<StringName> IgnoredAcComponents { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<EquipmentAcComponentMultiplierDefinition> AcComponentMultipliers { get; init; } =
        Array.Empty<EquipmentAcComponentMultiplierDefinition>();
    public bool LockDodgeBonus { get; init; }
    public StringName RequiredTargetEquipmentSelector { get; init; } = "";
    public IReadOnlyList<StringName> RequiredTargetItemTags { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<StringName> RequiredTargetEquipmentTypeIds { get; init; } =
        Array.Empty<StringName>();
    public StringName CoverPolicy { get; init; } = "";
    public StringName ProjectileObstaclePolicy { get; init; } = "";
    public StringName TraceLabel { get; init; } = "";
}

public sealed class EquipmentAcComponentMultiplierDefinition
{
    public StringName AcComponentId { get; init; } = "";
    public int MultiplierPercent { get; init; }
    public StringName StackMode { get; init; } = "";
}

public sealed class DamageRollModeOverrideActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public StringName RollMode { get; init; } = "";
    public StringName StackMode { get; init; } = "";
    public string Label { get; init; } = "";
}

public sealed class DamageReductionActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public int Amount { get; init; }
    public IReadOnlyList<StringName> DamageTags { get; init; } = Array.Empty<StringName>();
    public string Label { get; init; } = "";
}

public sealed class LootQuantityMultiplierActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public int MultiplierPercent { get; init; }
    public IReadOnlyList<StringName> AffectedDropKinds { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<StringName> AnyItemTags { get; init; } = Array.Empty<StringName>();
}

public sealed class ApplyStatusActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public StringName StatusId { get; init; } = "";
    public int DurationTurns { get; init; }
    public int DurationTu { get; init; }
    public int StackDelta { get; init; }
    public StringName StackBehavior { get; init; } = "";
    public int StackLimit { get; init; }
    public string DisplayLabel { get; init; } = "";
    public int AttackRollPenalty { get; init; } = -1;
    public int SourceBoundAttackRollPenalty { get; init; }
    public int SourceBoundAttackRollPenaltyMinStacks { get; init; } = 1;
    public int SourceBoundIncomingAttackRollBonusPerStack { get; init; }
    public int SourceBoundIncomingAttackRollBonusMinStacks { get; init; } = 1;
    public bool OverrideHealMultiplierPercent { get; init; }
    public int HealMultiplierPercent { get; init; } = 100;
    public int MovePointCapacityDelta { get; init; }
    public bool ForcedMoveImmune { get; init; }
    public bool CountsAsDebuffOverride { get; init; }
    public bool CountsAsDebuff { get; init; }
    public bool Undispellable { get; init; }
    public bool DispellableMagic { get; init; }
    public bool DispellableHarmfulMagic { get; init; }
    public bool DispellableBeneficialMagic { get; init; }
    public bool LockCounterattack { get; init; }
    public bool LockGuard { get; init; }
    public bool LockDodgeBonus { get; init; }
    public int TickIntervalTu { get; init; }
    public int TimelineDamageDiceCount { get; init; }
    public int TimelineDamageDiceSides { get; init; }
    public int TimelineDamageFlatBonus { get; init; }
    public int SaveDc { get; init; }
    public StringName SaveAbility { get; init; } = "";
    public StringName SaveTag { get; init; } = "";
    public bool ApplyOnSaveFailure { get; init; }
}

public sealed class ModifyActionPointsActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public StringName Mode { get; init; } = "";
    public int Amount { get; init; }
    public StringName StatusId { get; init; } = "";
    public string DisplayLabel { get; init; } = "";
}

public sealed class ScheduleAreaEffectActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName AnchorSelector { get; init; } = "";
    public int DelayTu { get; init; }
    public StringName TerrainEffectId { get; init; } = "";
    public StringName AreaPattern { get; init; } = "";
    public int AreaValue { get; init; }
    public StringName LifetimePolicy { get; init; } = "";
    public StringName EffectType { get; init; } = "";
    public StringName TargetTeamFilter { get; init; } = "";
    public StringName StackBehavior { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public StringName RenderOverlayId { get; init; } = "";
    public int OverlayPriority { get; init; }
    public StringName ContactStatusId { get; init; } = "";
    public int ContactStatusDurationTu { get; init; }
    public StringName ContactStackBehavior { get; init; } = "";
    public int ContactStackLimit { get; init; }
    public string ContactStatusDisplayLabel { get; init; } = "";
    public bool ContactCountsAsDebuffOverride { get; init; }
    public bool ContactCountsAsDebuff { get; init; }
    public bool ContactUndispellable { get; init; }
    public bool ContactDispellableMagic { get; init; }
    public bool ContactDispellableHarmfulMagic { get; init; }
    public bool ContactDispellableBeneficialMagic { get; init; }
    public int ContactSaveDc { get; init; }
    public StringName ContactSaveAbility { get; init; } = "";
    public StringName ContactSaveTag { get; init; } = "";
    public bool ContactApplyOnSaveFailure { get; init; }
    public int ContactTickIntervalTu { get; init; }
    public int ContactTimelineDamageDiceCount { get; init; }
    public int ContactTimelineDamageDiceSides { get; init; }
    public int ContactTimelineDamageFlatBonus { get; init; }
    public StringName ContactBlockedByTraitId { get; init; } = "";
}

public sealed class ApplyBattleTerrainEffectAfterCheckActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName AnchorSelector { get; init; } = "";
    public StringName TerrainEffectId { get; init; } = "";
    public int MoveCostDelta { get; init; }
    public StringName TargetTeamFilter { get; init; } = "";
    public StringName StackBehavior { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public StringName RenderOverlayId { get; init; } = "";
    public int OverlayPriority { get; init; }
    public StringName CheckAttributeModifierId { get; init; } = "";
    public StringName CheckCompare { get; init; } = "";
    public int CheckThreshold { get; init; }
    public bool NaturalTwentyAutoSuccess { get; init; }
    public bool NaturalOneAutoFailure { get; init; }
}

public sealed class ApplyEdgeFeatureActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName FromSelector { get; init; } = "";
    public StringName ToSelector { get; init; } = "";
    public int DurationTu { get; init; }
    public int MaxActiveEdges { get; init; }
    public bool RefreshExisting { get; init; }
    public bool RequireAdjacent { get; init; }
    public StringName FeatureKind { get; init; } = "";
    public StringName RenderKind { get; init; } = "";
    public int RenderLayers { get; init; }
    public bool BlocksMove { get; init; }
    public bool BlocksOccupancy { get; init; }
    public bool BlocksLos { get; init; }
    public StringName InteractionKind { get; init; } = "";
    public StringName StateTag { get; init; } = "";
}

public sealed class ModifyAbilityStateActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public StringName BindingId { get; init; } = "";
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
    public bool RemoveOnTargetDefeated { get; init; }
    public bool UniquePerSource { get; init; }
    public StringName MirrorStatusId { get; init; } = "";
    public int MirrorStatusDurationTu { get; init; }
    public StringName MirrorStatusStackBehavior { get; init; } = "";
    public int MirrorStatusStackLimit { get; init; }
    public string MirrorStatusDisplayLabel { get; init; } = "";
    public IReadOnlyList<StringName> ClearStatusIdsOnReplace { get; init; } =
        Array.Empty<StringName>();
}

public sealed class ClearStatusActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public StringName StatusId { get; init; } = "";
    public StringName MarkBindingId { get; init; } = "";
    public StringName MarkStateKey { get; init; } = "";
    public bool RequireSourceUnitMatch { get; init; }
    public bool ClearTargetMark { get; init; }
}

public sealed class TriggerSkillActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName SkillId { get; init; } = "";
    public int SkillLevel { get; init; }
    public StringName TargetSelector { get; init; } = "";
    public bool MergeIntoParentResult { get; init; }
    public bool HandleTargetDefeat { get; init; }
    public string ActivationLog { get; init; } = "";
    public string SaveLogLabel { get; init; } = "";
}

public sealed class ConsumeStatusStacksActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public StringName StatusId { get; init; } = "";
    public int Count { get; init; }
    public bool RequireSourceUnitMatch { get; init; } = true;
    public StringName SelectionMode { get; init; } = "highest_stacks";
}

public sealed class GrantSkillActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName SkillId { get; init; } = "";
    public int SkillLevel { get; init; }
    public StringName AvailabilityStateKey { get; init; } = "";
}

public sealed class SummonUnitsActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName AnchorSelector { get; init; } = "";
    public StringName StateKey { get; init; } = "";
    public DiceExpressionDefinition CountDice { get; init; }
    public int MaxLivingUnits { get; init; }
    public int DurationTu { get; init; }
    public int SpawnRadius { get; init; }
    public StringName UnitIdPrefix { get; init; } = "";
    public string UnitDisplayName { get; init; } = "";
    public StringName BodySizeCategory { get; init; } = "";
    public StringName ControlMode { get; init; } = "";
    public StringName AiBrainId { get; init; } = "";
    public StringName AiStateId { get; init; } = "";
    public int HpMax { get; init; }
    public int ArmorClass { get; init; }
    public int AttackBonus { get; init; }
    public int BaseAttackBonus { get; init; }
    public int ActionPoints { get; init; }
    public int MovePoints { get; init; }
    public IReadOnlyList<StringName> KnownActiveSkillIds { get; init; } = Array.Empty<StringName>();
    public StringName NaturalWeaponProfileTypeId { get; init; } = "";
    public StringName NaturalWeaponDamageTag { get; init; } = "";
    public int NaturalWeaponAttackRange { get; init; }
    public DiceExpressionDefinition NaturalWeaponDamageDice { get; init; }
    public StringName NaturalWeaponFamily { get; init; } = "";
    public IReadOnlyList<StringName> CreatureTypeTags { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<StringName> MovementTags { get; init; } = Array.Empty<StringName>();
}

public sealed class ConsumeSummonedUnitsActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName SourceBindingId { get; init; } = "";
    public StringName StateKey { get; init; } = "";
    public int Count { get; init; }
    public StringName SelectionMode { get; init; } = "";
}

public sealed class SummonedUnitAttackRollModifierActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public StringName SourceBindingId { get; init; } = "";
    public StringName StateKey { get; init; } = "";
    public int Radius { get; init; }
    public int BonusPerUnit { get; init; }
    public int MaxAbsoluteBonus { get; init; }
    public int MinUnits { get; init; }
    public StringName StackMode { get; init; } = "";
    public string Label { get; init; } = "";
}

public sealed class EquipmentTemporalProgressModifierDefinition
{
    public StringName ModifierId { get; init; } = "";
    public StringName BindingId { get; init; } = "";
    public bool AppliesToActionProgress { get; init; }
    public bool AppliesToCastProgress { get; init; }
    public int SaveDc { get; init; }
    public StringName AttributeModifierId { get; init; } = "";
    public int SuccessRatePercent { get; init; }
    public int FailureRatePercent { get; init; }
    public string Label { get; init; } = "";
}

public sealed class EquipmentDurabilityDamageActionPayloadDefinition
    : EquipmentAbilityActionPayloadDefinition
{
    public StringName TargetSelector { get; init; } = "";
    public IReadOnlyList<StringName> TargetSlots { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<EquipmentSlotWeightDefinition> SlotWeights { get; init; } =
        Array.Empty<EquipmentSlotWeightDefinition>();
    public IReadOnlyList<StringName> RequiredItemTags { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<StringName> RequiredEquipmentTypeIds { get; init; } =
        Array.Empty<StringName>();
    public int DurabilityLoss { get; init; }
    public StringName SaveTag { get; init; } = "";
    public int SaveDc { get; init; }
    public bool RequireAttackSuccess { get; init; }
    public int MaxDamagedItems { get; init; }
    public int MaxTargetRarity { get; init; } = -1;
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
    public EquipmentAbilityUsagePeriodKind UsagePeriodKind { get; init; } =
        EquipmentAbilityUsagePeriodKind.None;
    public int MaxUsesPerPeriod { get; init; }
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
        EquipmentAbilityReadOnlySet<StringName>.Empty;
    public IReadOnlySet<StringName> RequiredWeaponTypeIds { get; init; } =
        EquipmentAbilityReadOnlySet<StringName>.Empty;
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
    public StringName SyncSourceStateKey { get; init; } = "";
    public StringName SyncAggregation { get; init; } = "";
    public int SyncIntLiteral { get; init; }
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
    public IReadOnlySet<StringName> KnownTraitIds { get; init; } =
        EquipmentAbilityReadOnlySet<StringName>.Empty;
    public IReadOnlySet<StringName> KnownItemIds { get; init; } =
        EquipmentAbilityReadOnlySet<StringName>.Empty;
    public IReadOnlySet<StringName> KnownSkillIds { get; init; } =
        EquipmentAbilityReadOnlySet<StringName>.Empty;
    public IReadOnlySet<StringName> KnownCreatureTypeTags { get; init; } =
        EquipmentAbilityReadOnlySet<StringName>.Empty;
    public IReadOnlySet<StringName> KnownBattleEnvironmentTags { get; init; } =
        EquipmentAbilityReadOnlySet<StringName>.Empty;
    public IReadOnlySet<StringName> KnownStatusIds { get; init; } =
        EquipmentAbilityReadOnlySet<StringName>.Empty;
    public IReadOnlySet<StringName> KnownDamageTypes { get; init; } =
        EquipmentAbilityReadOnlySet<StringName>.Empty;
    public IReadOnlySet<StringName> KnownEquipmentSlotIds { get; init; } =
        EquipmentAbilityReadOnlySet<StringName>.Empty;
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
    public IReadOnlyList<EquipmentAbilityConsumerSupportSpec> ConsumerSupport { get; init; } =
        Array.Empty<EquipmentAbilityConsumerSupportSpec>();
    public EquipmentAbilityStateAccessSpec StateAccess { get; init; } =
        EquipmentAbilityStateAccessSpec.Empty;

    public bool SupportsConsumer(EquipmentAbilityConsumerKind consumer) =>
        GetConsumerSupport(consumer) != null;

    public EquipmentAbilityConsumerSupportSpec GetConsumerSupport(
        EquipmentAbilityConsumerKind consumer
    )
    {
        foreach (EquipmentAbilityConsumerSupportSpec support in ConsumerSupport)
            if (support != null && support.Consumer == consumer)
                return support;
        return null;
    }
}

public sealed class EquipmentAbilityTriggerTimingSpec
{
    public EquipmentAbilityTriggerKind Trigger { get; init; }
    public IReadOnlySet<EquipmentAbilityTimingKind> AllowedTimings { get; init; } =
        EquipmentAbilityReadOnlySet<EquipmentAbilityTimingKind>.Empty;
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
    public string StateKeyPayloadMemberName { get; init; } = "";
    public bool StateKeyMustBeDeclaredInBinding { get; init; }
    public bool SourceLifecycleCleanupRequired { get; init; }
}

internal static class EquipmentAbilityBindingMatcher
{
    public static IReadOnlyList<EquipmentAbilityBindingDefinition> FindBindings(
        IEnumerable<EquipmentAbilityBindingDefinition> candidates,
        StringName traitId,
        TraitSourceKind sourceKind,
        IReadOnlySet<StringName> traitCategories,
        ItemDef sourceItem
    )
    {
        if (traitId == "" || sourceKind == TraitSourceKind.Unknown || candidates == null)
            return Array.Empty<EquipmentAbilityBindingDefinition>();

        var result = new List<EquipmentAbilityBindingDefinition>();
        HashSet<StringName> categories = traitCategories != null
            ? new HashSet<StringName>(traitCategories)
            : new HashSet<StringName>();
        HashSet<StringName> itemTags = sourceItem != null
            ? new HashSet<StringName>(sourceItem.GetTagsTyped())
            : new HashSet<StringName>();
        StringName equipmentType = sourceItem?.GetEquipmentTypeIdNormalized() ?? "";

        foreach (EquipmentAbilityBindingDefinition binding in candidates)
        {
            if (binding == null || binding.TraitId != traitId)
                continue;
            if (!binding.AllowedSourceKinds.Contains(TraitContentRules.ToStringName(sourceKind)))
                continue;
            if (!IsSubset(binding.RequiredTraitCategories, categories))
                continue;
            if (!IsSubset(binding.RequiredItemTags, itemTags))
                continue;
            if (binding.SupportedEquipmentTypeIds.Count > 0)
            {
                if (equipmentType == "" || !binding.SupportedEquipmentTypeIds.Contains(equipmentType))
                    continue;
            }
            result.Add(binding);
        }

        return result;
    }

    private static bool IsSubset(
        IReadOnlySet<StringName> required,
        IReadOnlySet<StringName> actual
    )
    {
        if (required == null || required.Count == 0)
            return true;
        if (actual == null || actual.Count == 0)
            return false;
        foreach (StringName value in required)
            if (!actual.Contains(value))
                return false;
        return true;
    }
}

internal sealed class EquipmentAbilityReadOnlySet<T> : IReadOnlySet<T>
{
    public static readonly EquipmentAbilityReadOnlySet<T> Empty = new(Array.Empty<T>());

    private readonly HashSet<T> _values;

    public EquipmentAbilityReadOnlySet(IEnumerable<T> values)
    {
        _values = values != null ? new HashSet<T>(values) : new HashSet<T>();
    }

    public int Count => _values.Count;

    public bool Contains(T item) => _values.Contains(item);

    public bool IsProperSubsetOf(IEnumerable<T> other) => _values.IsProperSubsetOf(other);

    public bool IsProperSupersetOf(IEnumerable<T> other) => _values.IsProperSupersetOf(other);

    public bool IsSubsetOf(IEnumerable<T> other) => _values.IsSubsetOf(other);

    public bool IsSupersetOf(IEnumerable<T> other) => _values.IsSupersetOf(other);

    public bool Overlaps(IEnumerable<T> other) => _values.Overlaps(other);

    public bool SetEquals(IEnumerable<T> other) => _values.SetEquals(other);

    public IEnumerator<T> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static EquipmentAbilityReadOnlySet<T> From(IEnumerable<T> values) => new(values);
}
