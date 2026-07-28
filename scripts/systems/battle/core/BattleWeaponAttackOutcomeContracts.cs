using System;
using System.Collections.Generic;
using Godot;

internal enum BattleImmediateWeaponAttackMode
{
    Counterattack = 0,
    EquipmentReaction,
}

internal enum BattleWeaponAttackOutcomeKind
{
    Unknown = 0,
    StandardWeaponSkillAttack,
    Counterattack,
    EquipmentReaction,
}

internal abstract class BattleWeaponAttackOutcomeRequest
{
    protected BattleWeaponAttackOutcomeRequest(
        BattleWeaponAttackOutcomeKind kind,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult resolution,
        BattleEventBatch batch
    )
    {
        if (
            kind == BattleWeaponAttackOutcomeKind.Unknown
            || !Enum.IsDefined(kind)
        )
            throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        Kind = kind;
        SourceUnit = sourceUnit
            ?? throw new ArgumentNullException(nameof(sourceUnit));
        TargetUnit = targetUnit
            ?? throw new ArgumentNullException(nameof(targetUnit));
        Resolution = resolution;
        Batch = batch ?? throw new ArgumentNullException(nameof(batch));
    }

    internal BattleWeaponAttackOutcomeKind Kind { get; }
    internal BattleUnitState SourceUnit { get; }
    internal BattleUnitState TargetUnit { get; }
    internal AttackEffectResolutionResult Resolution { get; }
    internal BattleEventBatch Batch { get; }
}

internal sealed class BattleWeaponAttackResolverSurfaceRequest
    : BattleWeaponAttackOutcomeRequest
{
    internal BattleWeaponAttackResolverSurfaceRequest(
        BattleWeaponAttackOutcomeKind kind,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult resolution,
        BattleEventBatch batch
    ) : base(kind, sourceUnit, targetUnit, resolution, batch) { }
}

internal sealed class BattleWeaponAttackPostProducerHookRequest
    : BattleWeaponAttackOutcomeRequest
{
    internal BattleWeaponAttackPostProducerHookRequest(
        BattleWeaponAttackOutcomeKind kind,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult resolution,
        BattleEventBatch batch,
        int previousTargetHp,
        IReadOnlyList<StringName> appliedStatusIds,
        StringName sourceEventId
    ) : base(kind, sourceUnit, targetUnit, resolution, batch)
    {
        AppliedStatusIds = appliedStatusIds
            ?? throw new ArgumentNullException(nameof(appliedStatusIds));
        if (sourceEventId == new StringName(""))
            throw new ArgumentException(
                "source event id is required",
                nameof(sourceEventId)
            );
        PreviousTargetHp = previousTargetHp;
        SourceEventId = sourceEventId;
    }

    internal int PreviousTargetHp { get; }
    internal IReadOnlyList<StringName> AppliedStatusIds { get; }
    internal StringName SourceEventId { get; }
}

internal sealed class BattleWeaponAttackUnappliedResultSurfaceRequest
    : BattleWeaponAttackOutcomeRequest
{
    internal BattleWeaponAttackUnappliedResultSurfaceRequest(
        BattleWeaponAttackOutcomeKind kind,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult resolution,
        BattleEventBatch batch
    ) : base(kind, sourceUnit, targetUnit, resolution, batch) { }
}

internal sealed class BattleWeaponAttackAppliedResultSurfaceRequest
    : BattleWeaponAttackOutcomeRequest
{
    internal BattleWeaponAttackAppliedResultSurfaceRequest(
        BattleWeaponAttackOutcomeKind kind,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult resolution,
        BattleEventBatch batch,
        string subjectLabel,
        string targetDisplayLabel
    ) : base(kind, sourceUnit, targetUnit, resolution, batch)
    {
        if (string.IsNullOrWhiteSpace(subjectLabel))
            throw new ArgumentException(
                "subject label is required",
                nameof(subjectLabel)
            );
        SubjectLabel = subjectLabel;
        TargetDisplayLabel = targetDisplayLabel ?? "";
    }

    internal string SubjectLabel { get; }
    internal string TargetDisplayLabel { get; }
}

internal sealed class BattleWeaponAttackTerminalOutcomeRequest
    : BattleWeaponAttackOutcomeRequest
{
    internal BattleWeaponAttackTerminalOutcomeRequest(
        BattleWeaponAttackOutcomeKind kind,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult resolution,
        BattleEventBatch batch,
        StringName skillId,
        BattleKillProvenance killProvenance
    ) : base(kind, sourceUnit, targetUnit, resolution, batch)
    {
        if (skillId == new StringName(""))
            throw new ArgumentException("skill id is required", nameof(skillId));
        if (
            killProvenance.WeaponAttackOutcomeKind
                != BattleWeaponAttackOutcomeKind.Unknown
            && killProvenance.WeaponAttackOutcomeKind != kind
        )
        {
            throw new ArgumentException(
                "kill provenance kind does not match request kind",
                nameof(killProvenance)
            );
        }
        SkillId = skillId;
        KillProvenance = killProvenance;
    }

    internal StringName SkillId { get; }
    internal BattleKillProvenance KillProvenance { get; }
}

internal sealed class BattleCounterattackImmediateWeaponAttackRequest
{
    internal BattleCounterattackImmediateWeaponAttackRequest(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleCounterattackCapability capability
    )
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        SourceUnit = sourceUnit
            ?? throw new ArgumentNullException(nameof(sourceUnit));
        TargetUnit = targetUnit
            ?? throw new ArgumentNullException(nameof(targetUnit));
        if (capability.InstanceId == new StringName(""))
            throw new ArgumentException("capability instance id is required");
        Capability = capability;
    }

    internal BattleState State { get; }
    internal BattleUnitState SourceUnit { get; }
    internal BattleUnitState TargetUnit { get; }
    internal BattleCounterattackCapability Capability { get; }
}

internal sealed class BattleEquipmentImmediateWeaponAttackRequest
{
    internal BattleEquipmentImmediateWeaponAttackRequest(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        StringName traitId,
        StringName bindingId,
        StringName actionId,
        StringName sourceEquipmentInstanceId
    )
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        SourceUnit = sourceUnit
            ?? throw new ArgumentNullException(nameof(sourceUnit));
        TargetUnit = targetUnit
            ?? throw new ArgumentNullException(nameof(targetUnit));
        SkillDefinition = skillDefinition
            ?? throw new ArgumentNullException(nameof(skillDefinition));
        if (bindingId == new StringName(""))
            throw new ArgumentException("binding id is required", nameof(bindingId));
        if (actionId == new StringName(""))
            throw new ArgumentException("action id is required", nameof(actionId));
        TraitId = traitId;
        BindingId = bindingId;
        ActionId = actionId;
        SourceEquipmentInstanceId = sourceEquipmentInstanceId;
    }

    internal BattleState State { get; }
    internal BattleUnitState SourceUnit { get; }
    internal BattleUnitState TargetUnit { get; }
    internal SkillDefinition SkillDefinition { get; }
    internal StringName TraitId { get; }
    internal StringName BindingId { get; }
    internal StringName ActionId { get; }
    internal StringName SourceEquipmentInstanceId { get; }
}

internal sealed class BattleImmediateWeaponAttackDefinition
{
    internal BattleImmediateWeaponAttackDefinition(
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        int staminaCost
    )
    {
        SkillDefinition = skillDefinition
            ?? throw new ArgumentNullException(nameof(skillDefinition));
        if (effectDefinitions == null || effectDefinitions.Count == 0)
            throw new ArgumentException("weapon attack effects are required");
        if (!BattleAttackDeliveryRules.IncludesWeaponDamage(effectDefinitions))
            throw new ArgumentException("definition must include weapon damage");
        if (staminaCost < 0)
            throw new ArgumentOutOfRangeException(nameof(staminaCost));
        EffectDefinitions =
            new List<CombatEffectDefinition>(effectDefinitions).ToArray();
        StaminaCost = staminaCost;
    }

    internal SkillDefinition SkillDefinition { get; }
    internal IReadOnlyList<CombatEffectDefinition> EffectDefinitions { get; }
    internal int StaminaCost { get; }
}

internal readonly record struct
    BattleImmediateWeaponAttackEquipmentAttribution(
        StringName TraitId,
        StringName BindingId,
        StringName ActionId,
        StringName SourceEquipmentInstanceId
    );

internal abstract class BattleImmediateWeaponAttackPlan
{
    protected BattleImmediateWeaponAttackPlan(
        BattleImmediateWeaponAttackMode mode,
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        bool definitionAvailable,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleAttackDeliveryKind deliveryKind,
        int staminaCost,
        int attackRollBonus,
        StringName traceSource,
        StringName sourceActionId
    )
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        SourceUnit = sourceUnit
            ?? throw new ArgumentNullException(nameof(sourceUnit));
        TargetUnit = targetUnit
            ?? throw new ArgumentNullException(nameof(targetUnit));
        if (traceSource == new StringName(""))
            throw new ArgumentException("trace source is required", nameof(traceSource));
        if (sourceActionId == new StringName(""))
            throw new ArgumentException("source action id is required", nameof(sourceActionId));
        if (staminaCost < 0)
            throw new ArgumentOutOfRangeException(nameof(staminaCost));
        if (definitionAvailable)
        {
            if (
                skillDefinition == null
                || effectDefinitions == null
                || effectDefinitions.Count == 0
                || (
                    deliveryKind != BattleAttackDeliveryKind.MeleeWeapon
                    && deliveryKind != BattleAttackDeliveryKind.RangedWeapon
                )
            )
            {
                throw new ArgumentException(
                    "available plan requires skill/effects/delivery"
                );
            }
        }
        else if (
            skillDefinition != null
            || (effectDefinitions?.Count ?? 0) != 0
            || deliveryKind != BattleAttackDeliveryKind.Unknown
        )
        {
            throw new ArgumentException(
                "unavailable plan cannot carry attack definition"
            );
        }

        Mode = mode;
        DefinitionAvailable = definitionAvailable;
        SkillDefinition = skillDefinition;
        EffectDefinitions = effectDefinitions != null
            ? new List<CombatEffectDefinition>(effectDefinitions).ToArray()
            : Array.Empty<CombatEffectDefinition>();
        DeliveryKind = deliveryKind;
        StaminaCost = staminaCost;
        AttackRollBonus = attackRollBonus;
        TraceSource = traceSource;
        SourceActionId = sourceActionId;
    }

    internal BattleImmediateWeaponAttackMode Mode { get; }
    internal BattleState State { get; }
    internal BattleUnitState SourceUnit { get; }
    internal BattleUnitState TargetUnit { get; }
    internal bool DefinitionAvailable { get; }
    internal SkillDefinition SkillDefinition { get; }
    internal IReadOnlyList<CombatEffectDefinition> EffectDefinitions { get; }
    internal BattleAttackDeliveryKind DeliveryKind { get; }
    internal int StaminaCost { get; }
    internal int AttackRollBonus { get; }
    internal StringName TraceSource { get; }
    internal StringName SourceActionId { get; }
}

internal sealed class BattleCounterattackWeaponAttackPlan
    : BattleImmediateWeaponAttackPlan
{
    internal BattleCounterattackWeaponAttackPlan(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        bool definitionAvailable,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleAttackDeliveryKind deliveryKind,
        int staminaCost,
        int attackRollBonus,
        StringName capabilityInstanceId,
        StringName weaponTrainingSkillId
    ) : base(
        BattleImmediateWeaponAttackMode.Counterattack,
        state,
        sourceUnit,
        targetUnit,
        definitionAvailable,
        skillDefinition,
        effectDefinitions,
        deliveryKind,
        staminaCost,
        attackRollBonus,
        capabilityInstanceId,
        capabilityInstanceId
    )
    {
        if (capabilityInstanceId == new StringName(""))
            throw new ArgumentException(
                "capability instance id is required",
                nameof(capabilityInstanceId)
            );
        CapabilityInstanceId = capabilityInstanceId;
        WeaponTrainingSkillId =
            ProgressionDataUtils.to_string_name(weaponTrainingSkillId);
        if (
            WeaponTrainingSkillId != new StringName("")
            && !BattleSkillMasteryService.IsWeaponTrainingSkillId(
                WeaponTrainingSkillId
            )
        )
        {
            throw new ArgumentException(
                "counterattack weapon training skill id is invalid",
                nameof(weaponTrainingSkillId)
            );
        }
    }

    internal StringName CapabilityInstanceId { get; }
    internal StringName WeaponTrainingSkillId { get; }
}

internal sealed class BattleEquipmentReactionWeaponAttackPlan
    : BattleImmediateWeaponAttackPlan
{
    internal BattleEquipmentReactionWeaponAttackPlan(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        bool definitionAvailable,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleAttackDeliveryKind deliveryKind,
        BattleImmediateWeaponAttackEquipmentAttribution equipmentAttribution
    ) : base(
        BattleImmediateWeaponAttackMode.EquipmentReaction,
        state,
        sourceUnit,
        targetUnit,
        definitionAvailable,
        definitionAvailable ? skillDefinition : null,
        definitionAvailable
            ? effectDefinitions
            : Array.Empty<CombatEffectDefinition>(),
        definitionAvailable
            ? deliveryKind
            : BattleAttackDeliveryKind.Unknown,
        0,
        0,
        equipmentAttribution.ActionId,
        equipmentAttribution.ActionId
    )
    {
        EquipmentAttribution = equipmentAttribution;
    }

    internal BattleImmediateWeaponAttackEquipmentAttribution
        EquipmentAttribution { get; }
}

internal sealed class BattleImmediateWeaponAttackResult
{
    private BattleImmediateWeaponAttackResult(
        AttackEffectResolutionResult resolution,
        bool countsTowardMaxAttacks,
        BattleEquipmentAbilityImmediateWeaponAttackResult equipmentSummary
    )
    {
        if (countsTowardMaxAttacks && equipmentSummary == null)
            throw new ArgumentNullException(nameof(equipmentSummary));
        if (!countsTowardMaxAttacks && equipmentSummary != null)
            throw new ArgumentException("uncounted result cannot carry summary");
        Resolution = resolution;
        CountsTowardMaxAttacks = countsTowardMaxAttacks;
        EquipmentSummary = equipmentSummary;
    }

    internal AttackEffectResolutionResult Resolution { get; }
    internal bool CountsTowardMaxAttacks { get; }
    internal BattleEquipmentAbilityImmediateWeaponAttackResult
        EquipmentSummary { get; }

    internal static BattleImmediateWeaponAttackResult ForCounterattack(
        AttackEffectResolutionResult resolution
    ) => new(resolution, false, null);

    internal static BattleImmediateWeaponAttackResult ForEquipmentMiss(
        AttackEffectResolutionResult resolution
    ) => new(resolution, false, null);

    internal static BattleImmediateWeaponAttackResult ForEquipment(
        AttackEffectResolutionResult resolution,
        BattleEquipmentAbilityImmediateWeaponAttackResult summary
    ) => new(resolution, true, summary);
}
