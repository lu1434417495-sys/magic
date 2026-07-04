using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEquipmentAbilityAfterHitContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public bool AttackSucceeded { get; init; }
    public bool CriticalHit { get; init; }
    public bool ApplyDamageDiceActions { get; init; } = true;
    public int ForcedRollValue { get; init; }
    public BattleSaveContext SaveContext { get; init; } = BattleSaveContext.Empty;
}

internal sealed class BattleEquipmentAbilityBonusDamageDiceContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public bool AttackSucceeded { get; init; }
    public bool CriticalHit { get; init; }
}

internal sealed class BattleEquipmentAbilityOnKillContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState DefeatedUnit { get; init; }
    public BattleState BattleState { get; init; }
    public BattleEventBatch Batch { get; init; }
    public BattleSaveContext SaveContext { get; init; } = BattleSaveContext.Empty;
}

internal sealed class BattleEquipmentAbilityGrantedSkillUsedContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public BattleEventBatch Batch { get; init; }
    public StringName BindingId { get; init; } = "";
    public StringName GrantedActionId { get; init; } = "";
    public StringName SkillId { get; init; } = "";
    public StringName SkillEntryId { get; init; } = "";
    public BattleEquipmentSkillUseOutcome SkillOutcome { get; init; } =
        BattleEquipmentSkillUseOutcome.Empty;
}

internal sealed class BattleEquipmentSkillUseOutcome
{
    public static readonly BattleEquipmentSkillUseOutcome Empty = new();

    public IReadOnlyList<StringName> TargetUnitIds { get; init; } = Array.Empty<StringName>();
    public int DamagedTargetCount { get; init; }
    public int KilledTargetCount { get; init; }
    public int HpDamageDealt { get; init; }
}

internal sealed class BattleEquipmentAbilityDamageRollModeContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public StringName CurrentRollMode { get; init; } = "";
    public bool AttackSucceeded { get; init; }
    public bool CriticalHit { get; init; }
}

internal sealed class BattleEquipmentAbilityTurnEndContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleState BattleState { get; init; }
}

internal sealed class BattleEquipmentAbilityDamageAppliedContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public int HpDamage { get; init; }
    public BattleSaveContext SaveContext { get; init; } = BattleSaveContext.Empty;
}

internal readonly struct EquipmentAbilityFactContext
{
    internal readonly bool CriticalHit;
    internal readonly int CurrentTu;
    internal readonly BattleState BattleState;
    internal readonly int HpDamage;
    internal readonly int SkillDamagedTargetCount;
    internal readonly int SkillKilledTargetCount;
    internal readonly int SkillHpDamageDealt;

    private EquipmentAbilityFactContext(
        bool criticalHit,
        int currentTu,
        BattleState battleState,
        int hpDamage = 0,
        int skillDamagedTargetCount = 0,
        int skillKilledTargetCount = 0,
        int skillHpDamageDealt = 0
    )
    {
        CriticalHit = criticalHit;
        CurrentTu = currentTu;
        BattleState = battleState;
        HpDamage = Math.Max(hpDamage, 0);
        SkillDamagedTargetCount = Math.Max(skillDamagedTargetCount, 0);
        SkillKilledTargetCount = Math.Max(skillKilledTargetCount, 0);
        SkillHpDamageDealt = Math.Max(skillHpDamageDealt, 0);
    }

    internal static EquipmentAbilityFactContext Empty => new(false, -1, null);

    internal static EquipmentAbilityFactContext FromBonusDamageDice(
        BattleEquipmentAbilityBonusDamageDiceContext context
    ) => new(
        context?.CriticalHit == true,
        Math.Max(context?.BattleState?.timeline?.current_tu ?? -1, -1),
        context?.BattleState
    );

    internal static EquipmentAbilityFactContext FromDamageRollMode(
        BattleEquipmentAbilityDamageRollModeContext context
    ) => new(
        context?.CriticalHit == true,
        Math.Max(context?.BattleState?.timeline?.current_tu ?? -1, -1),
        context?.BattleState
    );

    internal static EquipmentAbilityFactContext FromBattleState(BattleState state) =>
        new(false, Math.Max(state?.timeline?.current_tu ?? -1, -1), state);

    internal static EquipmentAbilityFactContext FromAfterHit(
        BattleEquipmentAbilityAfterHitContext context
    ) => new(
        context?.CriticalHit == true,
        Math.Max(context?.BattleState?.timeline?.current_tu ?? -1, -1),
        context?.BattleState
    );

    internal static EquipmentAbilityFactContext FromDamageApplied(
        BattleEquipmentAbilityDamageAppliedContext context
    ) => new(
        false,
        Math.Max(context?.BattleState?.timeline?.current_tu ?? -1, -1),
        context?.BattleState,
        context?.HpDamage ?? 0
    );

    internal static EquipmentAbilityFactContext FromGrantedSkillUsed(
        BattleEquipmentAbilityGrantedSkillUsedContext context
    )
    {
        BattleEquipmentSkillUseOutcome outcome =
            context?.SkillOutcome ?? BattleEquipmentSkillUseOutcome.Empty;
        return new(
            false,
            Math.Max(context?.BattleState?.timeline?.current_tu ?? -1, -1),
            context?.BattleState,
            skillDamagedTargetCount: outcome.DamagedTargetCount,
            skillKilledTargetCount: outcome.KilledTargetCount,
            skillHpDamageDealt: outcome.HpDamageDealt
        );
    }
}

internal sealed class BattleEquipmentAbilityRollResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ReactionId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public int RolledValue { get; init; }
    public StringName Compare { get; init; } = "";
    public int Threshold { get; init; }
    public bool Passed { get; init; }
}

internal sealed class BattleEquipmentAbilityDurabilityResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public EquipmentDurabilityCommitResult CommitResult { get; init; }
}

internal sealed class BattleEquipmentAbilityBonusDamageDiceResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public int DiceCount { get; init; }
    public int DiceSides { get; init; }
    public int FlatBonus { get; init; }
    public bool Subtract { get; init; }
    public StringName DamageType { get; init; } = "";
    public IReadOnlyList<StringName> DamageTags { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<StringName> MitigationBypassDamageTags { get; init; } =
        Array.Empty<StringName>();
    public IReadOnlyList<StringName> MitigationBypassTiers { get; init; } =
        Array.Empty<StringName>();
}

internal sealed class BattleEquipmentAbilityLootQuantityMultiplierResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public LootQuantityMultiplierActionPayloadDefinition Payload { get; init; }
}

internal sealed class BattleEquipmentAbilityStatusActionResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public StringName TargetUnitId { get; init; } = "";
    public StringName StatusId { get; init; } = "";
    public bool Applied { get; init; }
    public BattleSaveResult SaveResult { get; init; }
}

internal sealed class BattleEquipmentAbilityAfterHitResult
{
    private readonly List<BattleEquipmentAbilityRollResult> _rolls = new();
    private readonly List<BattleEquipmentAbilityDurabilityResult> _durabilityResults = new();
    private readonly List<BattleEquipmentAbilityBonusDamageDiceResult> _bonusDamageDice = new();
    private readonly List<BattleEquipmentAbilityStatusActionResult> _statusResults = new();

    public bool Resolved =>
        _rolls.Count > 0
        || _durabilityResults.Count > 0
        || _bonusDamageDice.Count > 0
        || _statusResults.Count > 0;

    public IReadOnlyList<BattleEquipmentAbilityRollResult> Rolls => _rolls;
    public IReadOnlyList<BattleEquipmentAbilityDurabilityResult> DurabilityResults =>
        _durabilityResults;
    public IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> BonusDamageDice =>
        _bonusDamageDice;
    public IReadOnlyList<BattleEquipmentAbilityStatusActionResult> StatusResults =>
        _statusResults;

    internal void AddRoll(BattleEquipmentAbilityRollResult result)
    {
        if (result != null)
            _rolls.Add(result);
    }

    internal void AddDurabilityResult(BattleEquipmentAbilityDurabilityResult result)
    {
        if (result != null)
            _durabilityResults.Add(result);
    }

    internal void AddBonusDamageDice(BattleEquipmentAbilityBonusDamageDiceResult result)
    {
        if (result != null)
            _bonusDamageDice.Add(result);
    }

    internal void AddStatusResult(BattleEquipmentAbilityStatusActionResult result)
    {
        if (result != null)
            _statusResults.Add(result);
    }

    public bool HasRoll(StringName bindingId)
    {
        foreach (BattleEquipmentAbilityRollResult roll in _rolls)
            if (roll.BindingId == bindingId)
                return true;
        return false;
    }

    public bool HasRoll(StringName bindingId, int rolledValue, bool passed)
    {
        foreach (BattleEquipmentAbilityRollResult roll in _rolls)
            if (
                roll.BindingId == bindingId
                && roll.RolledValue == rolledValue
                && roll.Passed == passed
            )
                return true;
        return false;
    }

    public bool HasDestroyedEquipment(StringName bindingId)
    {
        foreach (BattleEquipmentAbilityDurabilityResult result in _durabilityResults)
            if (result.BindingId == bindingId && result.CommitResult?.Destroyed == true)
                return true;
        return false;
    }

    public bool HasDestroyedEquipment(
        StringName bindingId,
        StringName targetUnitId,
        StringName slotId,
        StringName equipmentInstanceId
    )
    {
        foreach (BattleEquipmentAbilityDurabilityResult result in _durabilityResults)
        {
            EquipmentDurabilityCommitResult commit = result.CommitResult;
            if (
                result.BindingId == bindingId
                && commit?.Destroyed == true
                && commit.TargetUnitId == targetUnitId
                && commit.SlotId == slotId
                && commit.EquipmentInstanceId == equipmentInstanceId
            )
            {
                return true;
            }
        }
        return false;
    }

    public bool HasBonusDamageDice(StringName bindingId)
    {
        foreach (BattleEquipmentAbilityBonusDamageDiceResult dice in _bonusDamageDice)
            if (dice.BindingId == bindingId)
                return true;
        return false;
    }

    public bool HasBonusDamageDice(StringName bindingId, int diceCount, int diceSides)
    {
        foreach (BattleEquipmentAbilityBonusDamageDiceResult dice in _bonusDamageDice)
            if (
                dice.BindingId == bindingId
                && dice.DiceCount == diceCount
                && dice.DiceSides == diceSides
            )
                return true;
        return false;
    }
}

internal sealed class BattleEquipmentAbilityOnKillResult
{
    private readonly List<BattleEquipmentAbilityLootQuantityMultiplierResult> _lootMultipliers =
        new();
    private readonly List<BattleEquipmentAbilityStatusActionResult> _statusResults = new();
    private readonly List<BattleEquipmentAbilitySummonResult> _summonResults = new();
    private readonly List<BattleEquipmentAbilityImmediateWeaponAttackResult>
        _immediateWeaponAttackResults = new();

    public bool Resolved =>
        _lootMultipliers.Count > 0
        || _statusResults.Count > 0
        || _summonResults.Count > 0
        || _immediateWeaponAttackResults.Count > 0;
    public IReadOnlyList<BattleEquipmentAbilityLootQuantityMultiplierResult> LootMultipliers =>
        _lootMultipliers;
    public IReadOnlyList<BattleEquipmentAbilityStatusActionResult> StatusResults =>
        _statusResults;
    public IReadOnlyList<BattleEquipmentAbilitySummonResult> SummonResults => _summonResults;
    public IReadOnlyList<BattleEquipmentAbilityImmediateWeaponAttackResult>
        ImmediateWeaponAttackResults => _immediateWeaponAttackResults;

    internal void AddLootMultiplier(BattleEquipmentAbilityLootQuantityMultiplierResult result)
    {
        if (result != null)
            _lootMultipliers.Add(result);
    }

    internal void AddStatusResult(BattleEquipmentAbilityStatusActionResult result)
    {
        if (result != null)
            _statusResults.Add(result);
    }

    internal void AddSummonResult(BattleEquipmentAbilitySummonResult result)
    {
        if (result != null)
            _summonResults.Add(result);
    }

    internal void AddImmediateWeaponAttackResult(
        BattleEquipmentAbilityImmediateWeaponAttackResult result
    )
    {
        if (result != null)
            _immediateWeaponAttackResults.Add(result);
    }
}

internal sealed class BattleEquipmentAbilitySummonResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public int RequestedCount { get; init; }
    public int CreatedCount { get; init; }
}

internal sealed class BattleEquipmentAbilityImmediateWeaponAttackResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public StringName TargetUnitId { get; init; } = "";
    public bool Applied { get; init; }
    public int Damage { get; init; }
}

internal sealed class BattleEquipmentAbilityRuntimeService
{
    private static readonly StringName ActionKindAddDamageDice = "add_damage_dice";
    private static readonly StringName ActionKindImmediateWeaponAttack =
        "immediate_weapon_attack";
    private static readonly StringName ActionKindDealDamage = "deal_damage";
    private static readonly StringName ActionKindHeal = "heal";
    private static readonly StringName ActionKindAttackRollBonus = "attack_roll_bonus";
    private static readonly StringName ActionKindAttackRollAdvantage = "attack_roll_advantage";
    private static readonly StringName ActionKindAttackDefenseModifier =
        "attack_defense_modifier";
    private static readonly StringName ActionKindDamageRollModeOverride =
        "damage_roll_mode_override";
    private static readonly StringName ActionKindApplyStatus = "apply_status";
    private static readonly StringName ActionKindModifyAbilityState = "modify_ability_state";
    private static readonly StringName ActionKindMarkTarget = "mark_target";
    private static readonly StringName ActionKindClearStatus = "clear_status";
    private static readonly StringName ActionKindScheduleAreaEffect = "schedule_area_effect";
    private static readonly StringName ActionKindEquipmentDurabilityDamage =
        "equipment_durability_damage";
    private static readonly StringName ActionKindLootQuantityMultiplier =
        "loot_quantity_multiplier";
    private static readonly StringName ActionKindSummonUnits = "summon_units";
    private static readonly StringName ActionKindConsumeSummonedUnits =
        "consume_summoned_units";
    private static readonly StringName ActionKindSummonedUnitAttackRollModifier =
        "summoned_unit_attack_roll_modifier";
    private static readonly StringName ConditionKindCompareFact = "compare_fact";
    private static readonly StringName ConditionKindHasEquipmentTag = "has_equipment_tag";
    private static readonly StringName FactCreatureTypeTags = "creature_type_tags";
    private static readonly StringName FactBattleEnvironmentTag = "battle_environment_tag";
    private static readonly StringName FactHpPercentBp = "hp_percent_bp";
    private static readonly StringName FactCriticalHit = "critical_hit";
    private static readonly StringName FactHpDamage = "hp_damage";
    private static readonly StringName FactSkillDamagedTargetCount =
        "skill_damaged_target_count";
    private static readonly StringName FactSkillKilledTargetCount =
        "skill_killed_target_count";
    private static readonly StringName FactSkillHpDamageDealt =
        "skill_hp_damage_dealt";
    private static readonly StringName FactBodySize = "body_size";
    private static readonly StringName FactAttributeValue = "attribute_value";
    private static readonly StringName FactCurrentTu = "current_tu";
    private static readonly StringName FactEquipmentAbilityState = "equipment_ability_state";
    private static readonly StringName FactEquipmentTargetMarkMatches =
        "equipment_target_mark_matches";
    private static readonly StringName FactEquipmentTargetMarkStacks =
        "equipment_target_mark_stacks";
    private static readonly StringName FactStatusStacks = "status_stacks";
    private static readonly StringName FactNearbyEnemyCount = "nearby_enemy_count";
    private static readonly StringName FactNearbyUnitCount = "nearby_unit_count";
    private static readonly StringName FactSummonedUnitCount = "summoned_unit_count";
    private static readonly StringName DropKindItem = "item";
    private static readonly StringName StackModeMax = "max";
    private static readonly StringName QueryKindFact = "fact";
    private static readonly StringName QueryKindLiteral = "literal";
    private static readonly StringName OnceScopeTurn = "turn";
    private static readonly StringName ResetTimingPerBattle = "per_battle";
    private static readonly StringName ResetTimingBattle = "battle";

    private BattleRuntimeModule _runtime;
    private BattleDamageResolver _damageResolver;
    private readonly Queue<int> _forcedRollGateValuesForTests = new();

    internal void Setup(BattleRuntimeModule runtime, BattleDamageResolver damageResolver)
    {
        _runtime = runtime;
        _damageResolver = damageResolver;
    }

    internal void Dispose()
    {
        _runtime = null;
        _damageResolver = null;
        _forcedRollGateValuesForTests.Clear();
    }

    internal BattleState GetBattleState() => _runtime?.GetState();

    internal void ConfigureRollGateValuesForTests(IEnumerable<int> values)
    {
        _forcedRollGateValuesForTests.Clear();
        if (values == null)
            return;
        foreach (int value in values)
            _forcedRollGateValuesForTests.Enqueue(Math.Clamp(value, 1, 20));
    }

    internal List<BattleAttackRollModifierSpec> CollectAttackRollModifierCandidates(
        BattleAttackCheckPolicyContext context
    )
    {
        var result = new List<BattleAttackRollModifierSpec>();
        BattleUnitState attacker = ResolveContextAttacker(context);
        BattleUnitState target = ResolveContextTarget(context);
        if (context == null || attacker == null || target == null)
            return result;

        foreach (
            ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(attacker)
        )
        {
            CollectAttackRollBonusActions(
                context,
                activeBinding,
                attacker,
                target,
                defensiveSource: false,
                result
            );
            CollectAttackRollAdvantageActions(
                context,
                activeBinding,
                attacker,
                target,
                defensiveSource: false,
                result
            );
        }
        foreach (
            ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(target)
        )
        {
            CollectAttackRollBonusActions(
                context,
                activeBinding,
                target,
                attacker,
                defensiveSource: true,
                result
            );
        }
        CollectSummonedUnitAttackRollModifierActions(context, attacker, target, result);
        return result;
    }

    internal EquipmentAttackDefenseAdjustment CollectAttackDefenseAdjustment(
        BattleAttackCheckPolicyContext context
    )
    {
        var adjustment = new EquipmentAttackDefenseAdjustment();
        BattleUnitState attacker = ResolveContextAttacker(context);
        BattleUnitState target = ResolveContextTarget(context);
        if (context == null || attacker == null || target == null)
            return adjustment;

        foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(attacker))
        {
            CollectAttackDefenseModifierActions(context, activeBinding, attacker, target, adjustment);
        }
        return adjustment;
    }

    private void CollectAttackRollBonusActions(
        BattleAttackCheckPolicyContext context,
        ActiveEquipmentAbilityBinding activeBinding,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        bool defensiveSource,
        List<BattleAttackRollModifierSpec> result
    )
    {
        EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
        if (binding?.Reactions == null || result == null)
            return;
        foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
        {
            if (
                reaction == null
                || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                || reaction.Timing != EquipmentAbilityTimingKind.BeforeHit
                || !ConditionGroupPasses(
                    reaction.ConditionGroup,
                    sourceUnit,
                    targetUnit,
                    EquipmentAbilityFactContext.FromBattleState(context.battle_state),
                    activeBinding
                )
            )
            {
                continue;
            }
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action == null
                    || action.Kind != ActionKindAttackRollBonus
                    || action.PayloadDefinition is not AttackRollBonusActionPayloadDefinition payload
                    || payload.Bonus == 0
                    || !AttackRollPayloadSelectorMatches(payload.TargetSelector, defensiveSource)
                    || !ConditionGroupPasses(
                        action.ConditionGroup,
                        sourceUnit,
                        targetUnit,
                        EquipmentAbilityFactContext.FromBattleState(context.battle_state),
                        activeBinding
                    )
                    || !RollGatePasses(
                        action.RollGate,
                        binding.BindingId,
                        reaction.ReactionId,
                        action.ActionId,
                        forcedRollValue: 0,
                        result: null
                    )
                )
                {
                    continue;
                }
                result.Add(
                    new BattleAttackRollModifierSpec
                    {
                        source_domain = "equipment_ability",
                        source_id = binding.BindingId,
                        source_instance_id =
                            activeBinding.Source?.SourceEquipmentInstanceId.ToString() ?? "",
                        label = ResolveActionLabel(binding, payload.Label),
                        modifier_delta = payload.Bonus,
                        stack_key = action.ActionId != ""
                            ? action.ActionId
                            : binding.BindingId,
                        stack_mode = payload.StackMode == "" ? StackModeMax : payload.StackMode,
                        target_team_filter = defensiveSource ? "any" : "enemy",
                        endpoint_mode = "target",
                        footprint_mode = "any_cell",
                        applies_to = "attack_roll",
                    }
                );
            }
        }
    }

    private void CollectSummonedUnitAttackRollModifierActions(
        BattleAttackCheckPolicyContext context,
        BattleUnitState attacker,
        BattleUnitState target,
        List<BattleAttackRollModifierSpec> result
    )
    {
        BattleState state = context?.battle_state ?? _runtime?.GetState();
        if (state == null || attacker == null || result == null)
            return;

        foreach (BattleUnitState owner in state.GetUnitsTyped())
        {
            if (owner == null || !owner.is_alive || owner.faction_id == attacker.faction_id)
                continue;
            foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(owner))
            {
                EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
                if (binding?.Reactions == null)
                    continue;
                foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
                {
                    if (
                        reaction == null
                        || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                        || reaction.Timing != EquipmentAbilityTimingKind.BeforeHit
                        || !ConditionGroupPasses(
                            reaction.ConditionGroup,
                            owner,
                            attacker,
                            EquipmentAbilityFactContext.FromBattleState(state),
                            activeBinding
                        )
                    )
                    {
                        continue;
                    }
                    foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
                    {
                        if (
                            action == null
                            || action.Kind != ActionKindSummonedUnitAttackRollModifier
                            || action.PayloadDefinition is not SummonedUnitAttackRollModifierActionPayloadDefinition payload
                            || !SummonedUnitModifierSelectorMatches(payload.TargetSelector)
                            || !ConditionGroupPasses(
                                action.ConditionGroup,
                                owner,
                                attacker,
                                EquipmentAbilityFactContext.FromBattleState(state),
                                activeBinding
                            )
                        )
                        {
                            continue;
                        }

                        EquipmentAbilityBindingDefinition sourceBinding = ResolveStateBinding(
                            activeBinding,
                            binding,
                            payload.SourceBindingId
                        );
                        int count = CountLivingSummonedUnits(
                            state,
                            owner,
                            activeBinding.Source,
                            sourceBinding?.BindingId ?? payload.SourceBindingId,
                            payload.StateKey,
                            attacker,
                            Math.Max(payload.Radius, 0)
                        );
                        if (count < Math.Max(payload.MinUnits, 1))
                            continue;

                        int delta = ClampSignedModifier(
                            count * payload.BonusPerUnit,
                            payload.MaxAbsoluteBonus
                        );
                        if (delta == 0)
                            continue;
                        result.Add(
                            new BattleAttackRollModifierSpec
                            {
                                source_domain = "equipment_ability",
                                source_id = binding.BindingId,
                                source_instance_id =
                                    activeBinding.Source?.SourceEquipmentInstanceId.ToString()
                                    ?? "",
                                label = ResolveActionLabel(binding, payload.Label),
                                modifier_delta = delta,
                                stack_key = action.ActionId != ""
                                    ? action.ActionId
                                    : binding.BindingId,
                                stack_mode = payload.StackMode == ""
                                    ? StackModeMax
                                    : payload.StackMode,
                                target_team_filter = "any",
                                endpoint_mode = "target",
                                footprint_mode = "any_cell",
                                applies_to = "attack_roll",
                            }
                        );
                    }
                }
            }
        }
    }

    private static bool SummonedUnitModifierSelectorMatches(StringName selector)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(selector);
        return normalized == ""
            || normalized == "attacker"
            || normalized == "attack_source"
            || normalized == "source_attacker";
    }

    private static int ClampSignedModifier(int value, int maxAbsolute)
    {
        int limit = Math.Max(maxAbsolute, 0);
        if (limit == 0)
            return value;
        return Math.Clamp(value, -limit, limit);
    }

    private void CollectAttackRollAdvantageActions(
        BattleAttackCheckPolicyContext context,
        ActiveEquipmentAbilityBinding activeBinding,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        bool defensiveSource,
        List<BattleAttackRollModifierSpec> result
    )
    {
        EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
        if (binding?.Reactions == null || result == null)
            return;
        foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
        {
            if (
                reaction == null
                || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                || reaction.Timing != EquipmentAbilityTimingKind.BeforeHit
                || !ConditionGroupPasses(
                    reaction.ConditionGroup,
                    sourceUnit,
                    targetUnit,
                    EquipmentAbilityFactContext.FromBattleState(context.battle_state),
                    activeBinding
                )
            )
            {
                continue;
            }
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action == null
                    || action.Kind != ActionKindAttackRollAdvantage
                    || action.PayloadDefinition is not AttackRollAdvantageActionPayloadDefinition payload
                    || payload.Mode != "advantage"
                    || !AttackRollPayloadSelectorMatches(payload.TargetSelector, defensiveSource)
                    || !ConditionGroupPasses(
                        action.ConditionGroup,
                        sourceUnit,
                        targetUnit,
                        EquipmentAbilityFactContext.FromBattleState(context.battle_state),
                        activeBinding
                    )
                )
                {
                    continue;
                }
                result.Add(
                    new BattleAttackRollModifierSpec
                    {
                        source_domain = "equipment_ability",
                        source_id = binding.BindingId,
                        source_instance_id =
                            activeBinding.Source?.SourceEquipmentInstanceId.ToString() ?? "",
                        label = ResolveActionLabel(binding, payload.Label),
                        modifier_delta = 0,
                        stack_key = action.ActionId != ""
                            ? action.ActionId
                            : binding.BindingId,
                        stack_mode = payload.StackMode == "" ? StackModeMax : payload.StackMode,
                        target_team_filter = defensiveSource ? "any" : "enemy",
                        endpoint_mode = "target",
                        footprint_mode = "any_cell",
                        applies_to = "attack_advantage",
                    }
                );
            }
        }
    }

    private void CollectAttackDefenseModifierActions(
        BattleAttackCheckPolicyContext context,
        ActiveEquipmentAbilityBinding activeBinding,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAttackDefenseAdjustment result
    )
    {
        EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
        if (binding?.Reactions == null || result == null)
            return;
        foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
        {
            if (
                reaction == null
                || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                || reaction.Timing != EquipmentAbilityTimingKind.BeforeHit
                || !ConditionGroupPasses(
                    reaction.ConditionGroup,
                    sourceUnit,
                    targetUnit,
                    EquipmentAbilityFactContext.FromBattleState(context.battle_state),
                    activeBinding
                )
            )
            {
                continue;
            }
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action == null
                    || action.Kind != ActionKindAttackDefenseModifier
                    || action.PayloadDefinition is not EquipmentAttackDefenseModifierDefinition payload
                    || !AttackDefensePayloadTargetFiltersPass(payload, targetUnit)
                    || !ConditionGroupPasses(
                        action.ConditionGroup,
                        sourceUnit,
                        targetUnit,
                        EquipmentAbilityFactContext.FromBattleState(context.battle_state),
                        activeBinding
                    )
                    || !RollGatePasses(
                        action.RollGate,
                        binding.BindingId,
                        reaction.ReactionId,
                        action.ActionId,
                        forcedRollValue: 0,
                        result: null
                    )
                )
                {
                    continue;
                }
                foreach (StringName componentId in payload.IgnoredAcComponents ?? Array.Empty<StringName>())
                {
                    result.AddIgnoredAcComponent(componentId);
                }
                foreach (EquipmentAcComponentMultiplierDefinition multiplier in payload.AcComponentMultipliers ?? Array.Empty<EquipmentAcComponentMultiplierDefinition>())
                {
                    if (multiplier != null)
                    {
                        result.AddComponentMultiplier(
                            multiplier.AcComponentId,
                            multiplier.MultiplierPercent
                        );
                    }
                }
                if (payload.LockDodgeBonus)
                {
                    result.AddLockDodgeBonus();
                }
            }
        }
    }

    private bool AttackDefensePayloadTargetFiltersPass(
        EquipmentAttackDefenseModifierDefinition payload,
        BattleUnitState targetUnit
    )
    {
        if (payload == null)
            return false;
        StringName selector = ProgressionDataUtils.to_string_name(
            payload.RequiredTargetEquipmentSelector
        );
        bool hasTagFilter = payload.RequiredTargetItemTags != null
            && payload.RequiredTargetItemTags.Count > 0;
        bool hasTypeFilter = payload.RequiredTargetEquipmentTypeIds != null
            && payload.RequiredTargetEquipmentTypeIds.Count > 0;
        if (!hasTagFilter && !hasTypeFilter && selector == "")
            return true;
        StringName slotId = "";
        if (selector == "target_armor")
            slotId = "body";
        else if (selector == "target_shield")
            slotId = "off_hand";
        if (slotId == "" || targetUnit == null)
            return false;
        StringName itemId = ProgressionDataUtils.to_string_name(
            targetUnit.GetEquipmentView()?.GetEquippedItemId(slotId) ?? ""
        );
        ItemDef itemDef = ResolveItemDef(itemId);
        if (itemDef == null)
            return false;
        if (!AllTagsPresent(itemDef, payload.RequiredTargetItemTags))
            return false;
        if (hasTypeFilter)
        {
            StringName equipmentTypeId = itemDef.GetEquipmentTypeIdNormalized();
            bool matched = false;
            foreach (StringName requiredType in payload.RequiredTargetEquipmentTypeIds)
            {
                if (equipmentTypeId == requiredType)
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
                return false;
        }
        return true;
    }

    private static bool AttackRollPayloadSelectorMatches(
        StringName targetSelector,
        bool defensiveSource
    )
    {
        StringName selector = ProgressionDataUtils.to_string_name(targetSelector);
        if (!defensiveSource)
            return selector == "" || selector == "target" || selector == "attack_target";
        return selector == "attacker" || selector == "source_attacker";
    }

    internal BattleEquipmentAbilityAfterHitResult ResolveAfterHit(
        BattleEquipmentAbilityAfterHitContext context
    )
    {
        var result = new BattleEquipmentAbilityAfterHitResult();
        if (
            context == null
            || context.SourceUnit == null
            || context.TargetUnit == null
            || !context.AttackSucceeded
        )
        {
            return result;
        }

        foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(context.SourceUnit))
        {
            EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
            if (binding?.Reactions == null)
                continue;
            foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                    || reaction.Timing != EquipmentAbilityTimingKind.AfterHit
                    || !ConditionGroupPasses(
                        reaction.ConditionGroup,
                        context.SourceUnit,
                        context.TargetUnit,
                        EquipmentAbilityFactContext.FromAfterHit(context),
                        activeBinding
                    )
                )
                {
                    continue;
                }
                if (
                    !RollGatePasses(
                        reaction.RollGate,
                        binding.BindingId,
                        reaction.ReactionId,
                        "",
                        context.ForcedRollValue,
                        result
                    )
                )
                {
                    continue;
                }
                ResolveActions(activeBinding, binding, reaction, context, result);
            }
        }
        return result;
    }

    internal IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> CollectBonusDamageDiceOnHit(
        BattleEquipmentAbilityBonusDamageDiceContext context
    )
    {
        var result = new List<BattleEquipmentAbilityBonusDamageDiceResult>();
        if (
            context == null
            || context.SourceUnit == null
            || context.TargetUnit == null
            || !context.AttackSucceeded
        )
        {
            return result;
        }

        foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(context.SourceUnit))
        {
            EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
            if (binding?.Reactions == null)
                continue;
            foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                    || reaction.Timing != EquipmentAbilityTimingKind.AfterHit
                    || !HasAddDamageDiceAction(reaction)
                    || !ConditionGroupPasses(
                        reaction.ConditionGroup,
                        context.SourceUnit,
                        context.TargetUnit,
                        EquipmentAbilityFactContext.FromBonusDamageDice(context),
                        activeBinding
                    )
                )
                {
                    continue;
                }
                if (
                    !RollGatePasses(
                        reaction.RollGate,
                        binding.BindingId,
                        reaction.ReactionId,
                        "",
                        forcedRollValue: 0,
                        new BattleEquipmentAbilityAfterHitResult()
                    )
                )
                {
                    continue;
                }
                CollectBonusDamageDiceActions(activeBinding, reaction, context, result);
            }
        }
        return result;
    }

    internal StringName ResolveDamageRollModeOverride(
        BattleEquipmentAbilityDamageRollModeContext context
    )
    {
        StringName rollMode = ProgressionDataUtils.to_string_name(
            context?.CurrentRollMode ?? new StringName("")
        );
        if (context == null || context.SourceUnit == null || context.TargetUnit == null)
            return rollMode;

        foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(context.SourceUnit))
        {
            EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
            if (binding?.Reactions == null)
                continue;
            foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnDamageRoll
                    || reaction.Timing != EquipmentAbilityTimingKind.BeforeDamage
                    || !ConditionGroupPasses(
                        reaction.ConditionGroup,
                        context.SourceUnit,
                        context.TargetUnit,
                        EquipmentAbilityFactContext.FromDamageRollMode(context),
                        activeBinding
                    )
                    || !RollGatePasses(
                        reaction.RollGate,
                        binding.BindingId,
                        reaction.ReactionId,
                        "",
                        forcedRollValue: 0,
                        result: null
                    )
                )
                {
                    continue;
                }

                foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
                {
                    if (
                        action == null
                        || action.Kind != ActionKindDamageRollModeOverride
                        || action.PayloadDefinition is not DamageRollModeOverrideActionPayloadDefinition payload
                        || !DamageRollModePayloadSelectorMatches(payload.TargetSelector)
                        || !ConditionGroupPasses(
                            action.ConditionGroup,
                            context.SourceUnit,
                            context.TargetUnit,
                            EquipmentAbilityFactContext.FromDamageRollMode(context),
                            activeBinding
                        )
                        || !RollGatePasses(
                            action.RollGate,
                            binding.BindingId,
                            reaction.ReactionId,
                            action.ActionId,
                            forcedRollValue: 0,
                            result: null
                        )
                    )
                    {
                        continue;
                    }

                    StringName nextMode = ProgressionDataUtils.to_string_name(payload.RollMode);
                    if (nextMode != "")
                        rollMode = nextMode;
                }
            }
        }
        return rollMode;
    }

    internal bool ResolveTurnEnd(BattleEquipmentAbilityTurnEndContext context)
    {
        bool changed = RemoveExpiredSummonedUnits(
            context?.BattleState ?? _runtime?.GetState(),
            null
        );
        if (context == null || context.SourceUnit == null)
            return changed;

        foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(context.SourceUnit))
        {
            EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
            if (binding?.Reactions == null)
                continue;
            foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnTurnEnd
                    || reaction.Timing != EquipmentAbilityTimingKind.AfterTurn
                    || !ConditionGroupPasses(
                        reaction.ConditionGroup,
                        context.SourceUnit,
                        null,
                        EquipmentAbilityFactContext.FromBattleState(context.BattleState),
                        activeBinding
                    )
                    || !RollGatePasses(
                        reaction.RollGate,
                        binding.BindingId,
                        reaction.ReactionId,
                        "",
                        forcedRollValue: 0,
                        result: null
                    )
                )
                {
                    continue;
                }

                foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
                {
                    if (
                        action == null
                        || !ConditionGroupPasses(
                            action.ConditionGroup,
                            context.SourceUnit,
                            null,
                            EquipmentAbilityFactContext.FromBattleState(context.BattleState),
                            activeBinding
                        )
                        || !RollGatePasses(
                            action.RollGate,
                            binding.BindingId,
                            reaction.ReactionId,
                            action.ActionId,
                            forcedRollValue: 0,
                            result: null
                        )
                    )
                    {
                        continue;
                    }

                    if (
                        action.Kind == ActionKindModifyAbilityState
                        && action.PayloadDefinition is ModifyAbilityStateActionPayloadDefinition statePayload
                    )
                    {
                        ResolveModifyAbilityStateAction(
                            activeBinding,
                            binding,
                            statePayload,
                            context.SourceUnit,
                            null
                        );
                        changed = true;
                    }
                }
            }
        }
        return changed;
    }

    internal bool ResolveDamageApplied(BattleEquipmentAbilityDamageAppliedContext context)
    {
        if (
            context == null
            || context.SourceUnit == null
            || context.TargetUnit == null
            || context.HpDamage <= 0
        )
        {
            return false;
        }

        bool changed = false;
        foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(context.SourceUnit))
        {
            EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
            if (binding?.Reactions == null)
                continue;
            foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnDamageApplied
                    || reaction.Timing != EquipmentAbilityTimingKind.AfterDamage
                    || !ConditionGroupPasses(
                        reaction.ConditionGroup,
                        context.SourceUnit,
                        context.TargetUnit,
                        EquipmentAbilityFactContext.FromDamageApplied(context),
                        activeBinding
                    )
                    || !RollGatePasses(
                        reaction.RollGate,
                        binding.BindingId,
                        reaction.ReactionId,
                        "",
                        forcedRollValue: 0,
                        result: null
                    )
                )
                {
                    continue;
                }

                foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
                {
                    if (
                        action == null
                        || !ConditionGroupPasses(
                            action.ConditionGroup,
                            context.SourceUnit,
                            context.TargetUnit,
                            EquipmentAbilityFactContext.FromDamageApplied(context),
                            activeBinding
                        )
                        || !RollGatePasses(
                            action.RollGate,
                            binding.BindingId,
                            reaction.ReactionId,
                            action.ActionId,
                            forcedRollValue: 0,
                            result: null
                        )
                    )
                    {
                        continue;
                    }

                    if (
                        action.Kind == ActionKindModifyAbilityState
                        && action.PayloadDefinition is ModifyAbilityStateActionPayloadDefinition statePayload
                    )
                    {
                        ResolveModifyAbilityStateAction(
                            activeBinding,
                            binding,
                            statePayload,
                            context.SourceUnit,
                            context.TargetUnit
                        );
                        changed = true;
                    }
                    else if (
                        action.Kind == ActionKindApplyStatus
                        && action.PayloadDefinition is ApplyStatusActionPayloadDefinition statusPayload
                    )
                    {
                        BattleUnitState actionTarget = ResolveSubject(
                            statusPayload.TargetSelector,
                            context.SourceUnit,
                            context.TargetUnit
                        );
                        ResolveApplyStatusAction(
                            binding,
                            action,
                            statusPayload,
                            context.SourceUnit,
                            actionTarget,
                            context.SaveContext,
                            null
                        );
                        changed = true;
                    }
                }
            }
        }
        return changed;
    }

    private static bool DamageRollModePayloadSelectorMatches(StringName targetSelector)
    {
        StringName selector = ProgressionDataUtils.to_string_name(targetSelector);
        return selector == "" || selector == "source" || selector == "attacker" || selector == "owner";
    }

    internal bool ResolveGrantedSkillUsed(BattleEquipmentAbilityGrantedSkillUsedContext context)
    {
        if (
            context == null
            || context.SourceUnit == null
            || context.BindingId == ""
            || context.GrantedActionId == ""
        )
        {
            return false;
        }

        bool changed = false;
        EquipmentAbilityFactContext factContext =
            EquipmentAbilityFactContext.FromGrantedSkillUsed(context);
        foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(context.SourceUnit))
        {
            EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
            if (binding?.BindingId != context.BindingId || binding.Reactions == null)
                continue;
            if (!BindingHasGrantedAction(binding, context.GrantedActionId, context.SkillId))
                continue;

            foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnGrantedSkillUsed
                    || reaction.Timing != EquipmentAbilityTimingKind.AfterSkill
                    || !ConditionGroupPasses(
                        reaction.ConditionGroup,
                        context.SourceUnit,
                        context.TargetUnit,
                        factContext,
                        activeBinding
                    )
                )
                {
                    continue;
                }

                foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
                {
                    BattleState battleState = context.BattleState ?? _runtime?.GetState();
                    if (
                        action == null
                        || !ConditionGroupPasses(
                            action.ConditionGroup,
                            context.SourceUnit,
                            context.TargetUnit,
                            factContext,
                            activeBinding
                        )
                    )
                    {
                        continue;
                    }
                    if (
                        action.Kind == ActionKindModifyAbilityState
                        && action.PayloadDefinition is ModifyAbilityStateActionPayloadDefinition statePayload
                    )
                    {
                        ResolveModifyAbilityStateAction(
                            activeBinding,
                            binding,
                            statePayload,
                            context.SourceUnit,
                            context.TargetUnit
                        );
                        changed = true;
                    }
                    else if (
                        action.Kind == ActionKindMarkTarget
                        && action.PayloadDefinition is MarkTargetActionPayloadDefinition markPayload
                    )
                    {
                        changed =
                            ResolveMarkTargetAction(
                                activeBinding,
                                binding,
                                action,
                                markPayload,
                                context
                            )
                            || changed;
                    }
                    else if (
                        action.Kind == ActionKindConsumeSummonedUnits
                        && action.PayloadDefinition is ConsumeSummonedUnitsActionPayloadDefinition consumePayload
                    )
                    {
                        changed =
                            ResolveConsumeSummonedUnitsAction(
                                activeBinding,
                                binding,
                                consumePayload,
                                context
                            )
                            || changed;
                    }
                    else if (
                        action.Kind == ActionKindDealDamage
                        && action.PayloadDefinition is DealDamageActionPayloadDefinition dealDamagePayload
                    )
                    {
                        BattleUnitState changedUnit = ResolveDealDamageAction(
                            activeBinding,
                            binding,
                            dealDamagePayload,
                            context.SourceUnit,
                            context.TargetUnit,
                            battleState
                        );
                        if (changedUnit != null)
                        {
                            context.Batch?.AddChangedUnitId(changedUnit.unit_id);
                            changed = true;
                        }
                    }
                    else if (
                        action.Kind == ActionKindHeal
                        && action.PayloadDefinition is HealActionPayloadDefinition healPayload
                    )
                    {
                        BattleUnitState changedUnit = ResolveHealAction(
                            activeBinding,
                            binding,
                            healPayload,
                            context.SourceUnit,
                            context.TargetUnit,
                            battleState
                        );
                        if (changedUnit != null)
                        {
                            context.Batch?.AddChangedUnitId(changedUnit.unit_id);
                            changed = true;
                        }
                    }
                    else if (
                        action.Kind == ActionKindClearStatus
                        && action.PayloadDefinition is ClearStatusActionPayloadDefinition clearStatusPayload
                    )
                    {
                        BattleUnitState changedUnit = ResolveEquipmentActionTarget(
                            clearStatusPayload.TargetSelector,
                            context.SourceUnit,
                            context.TargetUnit,
                            activeBinding,
                            binding,
                            clearStatusPayload.MarkBindingId,
                            clearStatusPayload.MarkStateKey,
                            battleState
                        );
                        bool cleared = ResolveClearStatusAction(
                            activeBinding,
                            binding,
                            clearStatusPayload,
                            context.SourceUnit,
                            context.TargetUnit,
                            battleState
                        );
                        if (cleared)
                        {
                            context.Batch?.AddChangedUnitId(changedUnit?.unit_id ?? "");
                            changed = true;
                        }
                    }
                    else if (
                        action.Kind == ActionKindApplyStatus
                        && action.PayloadDefinition is ApplyStatusActionPayloadDefinition statusPayload
                    )
                    {
                        ResolveApplyStatusAction(
                            binding,
                            action,
                            statusPayload,
                            context.SourceUnit,
                            ResolveSubject(
                                statusPayload.TargetSelector,
                                context.SourceUnit,
                                context.TargetUnit
                            ),
                            BattleSaveContext.Empty,
                            null
                        );
                        context.Batch?.AddChangedUnitId(context.TargetUnit?.unit_id ?? "");
                        changed = true;
                    }
                }
            }
        }
        return changed;
    }

    private bool ResolveMarkTargetAction(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        MarkTargetActionPayloadDefinition payload,
        BattleEquipmentAbilityGrantedSkillUsedContext context
    )
    {
        BattleState battleState = context?.BattleState ?? _runtime?.GetState();
        BattleUnitState targetUnit = ResolveSubject(
            payload?.TargetSelector ?? "",
            context?.SourceUnit,
            context?.TargetUnit
        );
        if (
            battleState == null
            || context?.SourceUnit == null
            || targetUnit == null
            || binding == null
            || payload == null
            || payload.StateKey == ""
        )
        {
            return false;
        }

        BattleEquipmentTargetMarkState nextMark = new()
        {
            SourceUnitId = context.SourceUnit.unit_id,
            TargetUnitId = targetUnit.unit_id,
            SourceEquipmentInstanceId = activeBinding.Source?.SourceEquipmentInstanceId ?? "",
            BindingId = binding.BindingId,
            StateKey = payload.StateKey,
            Stacks = Math.Max(payload.StackDelta, 1),
            RemoveOnSourceMissing = payload.RemoveOnSourceMissing,
        };
        if (
            !battleState.SetEquipmentTargetMark(
                nextMark,
                payload.UniquePerSource,
                out BattleEquipmentTargetMarkState replaced
            )
        )
        {
            return false;
        }

        bool changed = true;
        StringName replacedTargetId = replaced != null
            ? ProgressionDataUtils.to_string_name(replaced.TargetUnitId)
            : new StringName("");
        StringName currentTargetId = ProgressionDataUtils.to_string_name(targetUnit.unit_id);
        if (replacedTargetId != "" && replacedTargetId != currentTargetId)
        {
            BattleUnitState previousTarget = battleState.GetUnit(replacedTargetId);
            if (previousTarget != null)
            {
                foreach (StringName statusId in BuildReplacementClearStatusIds(payload))
                {
                    if (statusId == "" || !previousTarget.HasStatusEffect(statusId))
                        continue;
                    previousTarget.EraseStatusEffect(statusId);
                    context.Batch?.AddChangedUnitId(previousTarget.unit_id);
                }
            }
        }

        if (ApplyMirrorStatus(context.SourceUnit, targetUnit, payload))
        {
            context.Batch?.AddChangedUnitId(targetUnit.unit_id);
        }
        return changed;
    }

    private static IReadOnlyList<StringName> BuildReplacementClearStatusIds(
        MarkTargetActionPayloadDefinition payload
    )
    {
        var result = new List<StringName>();
        AddUniqueStatusId(result, payload?.MirrorStatusId ?? "");
        foreach (StringName statusId in payload?.ClearStatusIdsOnReplace ?? Array.Empty<StringName>())
            AddUniqueStatusId(result, statusId);
        return result;
    }

    private static void AddUniqueStatusId(List<StringName> result, StringName statusId)
    {
        if (statusId == "" || result.Contains(statusId))
            return;
        result.Add(statusId);
    }

    private bool ApplyMirrorStatus(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        MarkTargetActionPayloadDefinition payload
    )
    {
        if (sourceUnit == null || targetUnit == null || payload == null || payload.MirrorStatusId == "")
            return false;

        StringName stackBehavior = payload.MirrorStatusStackBehavior == ""
            ? new StringName("refresh")
            : payload.MirrorStatusStackBehavior;
        int stackDelta = Math.Max(payload.StackDelta, 1);
        int stackLimit = Math.Max(payload.MirrorStatusStackLimit, 0);
        BattleStatusEffectState existing = targetUnit.GetStatusEffect(payload.MirrorStatusId);
        int existingStacks = Math.Max(existing?.stacks ?? 0, 0);
        int nextStacks = stackBehavior == "add" ? existingStacks + stackDelta : stackDelta;
        if (stackLimit > 0)
            nextStacks = Math.Min(nextStacks, stackLimit);
        nextStacks = Math.Max(nextStacks, 1);

        BattleStatusEffectState status = existing?.DuplicateState() ?? new BattleStatusEffectState();
        status.status_id = payload.MirrorStatusId;
        status.source_unit_id = sourceUnit.unit_id;
        status.power = nextStacks;
        status.stacks = nextStacks;
        status.duration = payload.MirrorStatusDurationTu > 0
            ? payload.MirrorStatusDurationTu
            : -1;
        status.stack_behavior = stackBehavior;
        status.stack_limit = stackLimit;
        if (!string.IsNullOrWhiteSpace(payload.MirrorStatusDisplayLabel))
            status.display_label = payload.MirrorStatusDisplayLabel;
        targetUnit.SetStatusEffect(status);
        _runtime?.MarkAppliedStatusesForTurnTiming(
            targetUnit,
            new Godot.Collections.Array<StringName> { payload.MirrorStatusId }
        );
        return true;
    }

    private static bool BindingHasGrantedAction(
        EquipmentAbilityBindingDefinition binding,
        StringName grantId,
        StringName skillId
    )
    {
        foreach (EquipmentGrantedActionDefinition grant in binding?.GrantedActions ?? Array.Empty<EquipmentGrantedActionDefinition>())
        {
            if (
                grant != null
                && grant.GrantedActionId == grantId
                && (skillId == "" || grant.SkillId == skillId)
            )
            {
                return true;
            }
        }
        return false;
    }

    internal BattleEquipmentAbilityOnKillResult ResolveOnKill(
        BattleEquipmentAbilityOnKillContext context
    )
    {
        var result = new BattleEquipmentAbilityOnKillResult();
        if (context == null || context.SourceUnit == null || context.DefeatedUnit == null)
            return result;

        foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(context.SourceUnit))
        {
            EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
            if (binding?.Reactions == null)
                continue;
            foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnKill
                    || reaction.Timing != EquipmentAbilityTimingKind.AfterKill
                    || !ConditionGroupPasses(
                        reaction.ConditionGroup,
                        context.SourceUnit,
                        context.DefeatedUnit,
                        EquipmentAbilityFactContext.FromBattleState(
                            context.BattleState ?? _runtime?.GetState()
                        ),
                        activeBinding
                    )
                    || !RollGatePasses(
                        reaction.RollGate,
                        binding.BindingId,
                        reaction.ReactionId,
                        "",
                        forcedRollValue: 0,
                        result: null
                    )
                )
                {
                    continue;
                }
                ResolveOnKillActions(activeBinding, binding, reaction, context, result);
            }
        }
        return result;
    }

    internal List<BattleLootEntry> ApplyLootQuantityMultipliers(
        IEnumerable<BattleLootEntry> lootEntries,
        BattleEquipmentAbilityOnKillResult onKillResult
    )
    {
        var result = new List<BattleLootEntry>();
        foreach (BattleLootEntry entry in lootEntries ?? Array.Empty<BattleLootEntry>())
        {
            BattleLootEntry resolvedEntry = ApplyLootQuantityMultipliers(entry, onKillResult);
            if (resolvedEntry != null)
                result.Add(resolvedEntry);
        }
        return result;
    }

    private void ResolveOnKillActions(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        BattleEquipmentAbilityOnKillContext context,
        BattleEquipmentAbilityOnKillResult result
    )
    {
        foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
        {
            if (
                action == null
                || !ConditionGroupPasses(
                    action.ConditionGroup,
                    context.SourceUnit,
                    context.DefeatedUnit,
                    EquipmentAbilityFactContext.FromBattleState(
                        context.BattleState ?? _runtime?.GetState()
                    ),
                    activeBinding
                )
                || !RollGatePasses(
                    action.RollGate,
                    binding.BindingId,
                    reaction.ReactionId,
                    action.ActionId,
                    forcedRollValue: 0,
                    result: null
                )
            )
            {
                continue;
            }

            if (
                action.Kind == ActionKindLootQuantityMultiplier
                && action.PayloadDefinition is LootQuantityMultiplierActionPayloadDefinition lootPayload
            )
            {
                result.AddLootMultiplier(
                    new BattleEquipmentAbilityLootQuantityMultiplierResult
                    {
                        BindingId = binding.BindingId,
                        ActionId = action.ActionId,
                        Payload = lootPayload,
                    }
                );
            }
            else if (
                action.Kind == ActionKindApplyStatus
                && action.PayloadDefinition is ApplyStatusActionPayloadDefinition statusPayload
            )
            {
                ResolveApplyStatusAction(binding, action, statusPayload, context, result);
            }
            else if (
                action.Kind == ActionKindScheduleAreaEffect
                && action.PayloadDefinition is ScheduleAreaEffectActionPayloadDefinition areaPayload
            )
            {
                ResolveScheduleAreaEffectAction(binding, action, areaPayload, context);
            }
            else if (
                action.Kind == ActionKindSummonUnits
                && action.PayloadDefinition is SummonUnitsActionPayloadDefinition summonPayload
            )
            {
                ResolveSummonUnitsAction(
                    activeBinding,
                    binding,
                    action,
                    summonPayload,
                    context,
                    result
                );
            }
            else if (
                action.Kind == ActionKindImmediateWeaponAttack
                && action.PayloadDefinition is ImmediateWeaponAttackActionPayloadDefinition attackPayload
            )
            {
                ResolveImmediateWeaponAttackAction(
                    binding,
                    action,
                    attackPayload,
                    context,
                    result
                );
            }
        }
    }

    private void ResolveImmediateWeaponAttackAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        ImmediateWeaponAttackActionPayloadDefinition payload,
        BattleEquipmentAbilityOnKillContext context,
        BattleEquipmentAbilityOnKillResult result
    )
    {
        BattleState state = context?.BattleState ?? _runtime?.GetState();
        BattleUnitState sourceUnit = context?.SourceUnit;
        BattleUnitState defeatedUnit = context?.DefeatedUnit;
        BattleUnitState anchorUnit = ResolveSubject(
            payload?.AnchorSelector ?? "",
            sourceUnit,
            defeatedUnit
        );
        if (
            state == null
            || sourceUnit?.is_alive != true
            || anchorUnit == null
            || _damageResolver == null
            || payload == null
            || payload.MaxAttacks <= 0
        )
        {
            return;
        }

        SkillDefinition skillDefinition = _runtime?.GetSkillDefinitionTyped(payload.SkillId);
        if (skillDefinition?.CombatProfile == null)
            return;
        IReadOnlyList<CombatEffectDefinition> effectDefinitions =
            skillDefinition.CombatProfile.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>();
        if (effectDefinitions.Count == 0)
            return;

        int attackCount = 0;
        foreach (BattleUnitState targetUnit in CollectImmediateWeaponAttackTargets(
            state,
            sourceUnit,
            defeatedUnit,
            anchorUnit,
            payload,
            skillDefinition
        ))
        {
            if (attackCount >= payload.MaxAttacks)
                break;
            BattleAttackCheckPolicyService attackPolicy =
                _runtime?.GetAttackCheckPolicyService();
            BattleAttackCheckPolicyContext policyContext =
                attackPolicy?.BuildSkillDefinitionAttackContext(
                    state,
                    sourceUnit,
                    targetUnit,
                    skillDefinition,
                    "skill_attack_check",
                    action?.ActionId ?? new StringName(""),
                    force_hit_no_crit: false
                );
            AttackCheckInput attackCheck =
                attackPolicy != null
                    ? attackPolicy.BuildAttackCheck(policyContext, 0, 0)
                    : new AttackCheckInput(invalid: true);
            AttackEffectResolutionResult attackResult = _damageResolver.ResolveAttackEffects(
                sourceUnit,
                targetUnit,
                effectDefinitions,
                attackCheck,
                new AttackContext
                {
                    BattleState = state,
                    SkillId = skillDefinition.SkillId,
                }
            );
            if (!attackResult.Applied && !attackResult.AttackSuccess)
                continue;

            attackCount++;
            result?.AddImmediateWeaponAttackResult(
                new BattleEquipmentAbilityImmediateWeaponAttackResult
                {
                    BindingId = binding?.BindingId ?? new StringName(""),
                    ActionId = action?.ActionId ?? new StringName(""),
                    TargetUnitId = targetUnit.unit_id,
                    Applied = attackResult.Applied,
                    Damage = Math.Max(attackResult.Damage, 0),
                }
            );
            context.Batch?.AddChangedUnitId(sourceUnit.unit_id);
            context.Batch?.AddChangedUnitId(targetUnit.unit_id);
            foreach (Vector2I coord in targetUnit.GetOccupiedCoordsTyped())
                context.Batch?.AddChangedCoord(coord);
            context.Batch?.AddLogLine(
                $"{sourceUnit.display_name} 借 {binding?.TraitId} 追击 {targetUnit.display_name}。"
            );
            if (targetUnit.is_alive != true)
            {
                _runtime?.HandleUnitDefeatedByRuntimeEffect(
                    targetUnit,
                    sourceUnit,
                    context.Batch,
                    $"{targetUnit.display_name} 被击倒。",
                    new BattleDefeatHandlingOptions(recordEnemyDefeatedAchievement: true)
                );
            }
        }
    }

    private IReadOnlyList<BattleUnitState> CollectImmediateWeaponAttackTargets(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState defeatedUnit,
        BattleUnitState anchorUnit,
        ImmediateWeaponAttackActionPayloadDefinition payload,
        SkillDefinition skillDefinition
    )
    {
        var result = new List<BattleUnitState>();
        if (state == null || sourceUnit == null || anchorUnit == null || payload == null)
            return result;

        int radius = Math.Max(payload.Radius, 0);
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate == null
                || candidate.is_alive != true
                || candidate.unit_id == sourceUnit.unit_id
                || candidate.unit_id == (defeatedUnit?.unit_id ?? new StringName(""))
                || !ImmediateWeaponAttackTeamFilterPasses(sourceUnit, candidate, payload.TargetTeamFilter)
                || BattleGridDistanceService.GetDistanceBetweenUnits(anchorUnit, candidate) > radius
            )
            {
                continue;
            }
            if (
                payload.RequireWeaponRange
                && BattleGridDistanceService.GetDistanceBetweenUnits(sourceUnit, candidate)
                    > Math.Max(sourceUnit.weapon_attack_range, 1)
            )
            {
                continue;
            }
            if (
                skillDefinition?.CombatProfile != null
                && !TargetFilterPasses(sourceUnit, candidate, skillDefinition.CombatProfile.TargetTeamFilter)
            )
            {
                continue;
            }
            result.Add(candidate);
        }
        result.Sort(
            (left, right) =>
            {
                int leftDistance = BattleGridDistanceService.GetDistanceBetweenUnits(anchorUnit, left);
                int rightDistance = BattleGridDistanceService.GetDistanceBetweenUnits(anchorUnit, right);
                if (leftDistance != rightDistance)
                    return leftDistance.CompareTo(rightDistance);
                return string.CompareOrdinal(left.unit_id.ToString(), right.unit_id.ToString());
            }
        );
        return result;
    }

    private static bool ImmediateWeaponAttackTeamFilterPasses(
        BattleUnitState sourceUnit,
        BattleUnitState candidate,
        StringName targetTeamFilter
    )
    {
        StringName filter = ProgressionDataUtils.to_string_name(targetTeamFilter);
        if (filter == "")
            filter = BattleTypedNames.TargetFilterEnemy;
        return BattleTargetTeamRules.IsUnitValidForFilter(sourceUnit, candidate, filter);
    }

    private static bool TargetFilterPasses(
        BattleUnitState sourceUnit,
        BattleUnitState candidate,
        StringName targetTeamFilter
    )
    {
        return BattleTargetTeamRules.IsUnitValidForFilter(
            sourceUnit,
            candidate,
            ProgressionDataUtils.to_string_name(targetTeamFilter)
        );
    }

    private void ResolveScheduleAreaEffectAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        ScheduleAreaEffectActionPayloadDefinition payload,
        BattleEquipmentAbilityOnKillContext context
    )
    {
        BattleUnitState anchorUnit = ResolveSubject(
            payload?.AnchorSelector ?? "",
            context.SourceUnit,
            context.DefeatedUnit
        );
        _runtime?._delayed_area_effect_system?.ScheduleFromEquipmentAction(
            context.SourceUnit,
            anchorUnit,
            binding,
            action,
            payload
        );
    }

    private void ResolveSummonUnitsAction(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        SummonUnitsActionPayloadDefinition payload,
        BattleEquipmentAbilityOnKillContext context,
        BattleEquipmentAbilityOnKillResult result
    )
    {
        BattleState state = context?.BattleState ?? _runtime?.GetState();
        if (
            state == null
            || context?.SourceUnit == null
            || binding == null
            || payload == null
            || payload.StateKey == ""
        )
        {
            return;
        }

        BattleUnitState anchorUnit = ResolveSubject(
            payload.AnchorSelector == "" ? new StringName("defeated") : payload.AnchorSelector,
            context.SourceUnit,
            context.DefeatedUnit
        ) ?? context.SourceUnit;

        int currentCount = CountLivingSummonedUnits(
            state,
            context.SourceUnit,
            activeBinding.Source,
            binding.BindingId,
            payload.StateKey
        );
        int capacity = Math.Max(payload.MaxLivingUnits, 0) - currentCount;
        if (capacity <= 0)
            return;

        int requested = Math.Max(RollDiceExpression(payload.CountDice), 0);
        int toCreate = Math.Min(requested, capacity);
        if (toCreate <= 0)
            return;

        int created = 0;
        foreach (Vector2I coord in CollectSummonSpawnCoords(state, anchorUnit, payload.SpawnRadius))
        {
            if (created >= toCreate)
                break;
            BattleUnitState summoned = BuildSummonedUnit(
                state,
                context.SourceUnit,
                activeBinding.Source,
                binding,
                payload,
                coord
            );
            if (summoned == null)
                continue;
            state.SetUnit(summoned);
            if (!_runtime._grid_service.PlaceUnit(state, summoned, coord, true))
            {
                state.RemoveUnit(summoned.unit_id);
                continue;
            }
            AddSummonedUnitToFactionList(state, context.SourceUnit, summoned);
            context.Batch?.AddChangedUnitId(summoned.unit_id);
            foreach (Vector2I occupiedCoord in summoned.GetOccupiedCoordsTyped())
                context.Batch?.AddChangedCoord(occupiedCoord);
            created++;
        }

        if (created > 0)
        {
            result?.AddSummonResult(
                new BattleEquipmentAbilitySummonResult
                {
                    BindingId = binding.BindingId,
                    ActionId = action?.ActionId ?? "",
                    RequestedCount = requested,
                    CreatedCount = created,
                }
            );
        }
    }

    private bool ResolveConsumeSummonedUnitsAction(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        ConsumeSummonedUnitsActionPayloadDefinition payload,
        BattleEquipmentAbilityGrantedSkillUsedContext context
    )
    {
        BattleState state = context?.BattleState ?? _runtime?.GetState();
        if (
            state == null
            || context?.SourceUnit == null
            || binding == null
            || payload == null
            || payload.StateKey == ""
            || payload.Count <= 0
        )
        {
            return false;
        }

        StringName sourceBindingId = payload.SourceBindingId != ""
            ? payload.SourceBindingId
            : binding.BindingId;
        List<BattleUnitState> summons = CollectLivingSummonedUnits(
            state,
            context.SourceUnit,
            activeBinding.Source,
            sourceBindingId,
            payload.StateKey
        );
        SortSummonsForConsumption(summons, context.TargetUnit);
        int removed = 0;
        foreach (BattleUnitState summon in summons)
        {
            if (removed >= payload.Count)
                break;
            RemoveSummonedUnit(state, summon, context.Batch, "召唤单位被消耗。");
            removed++;
        }
        return removed > 0;
    }

    private bool RemoveExpiredSummonedUnits(BattleState state, BattleEventBatch batch)
    {
        int currentTu = Math.Max(state?.timeline?.current_tu ?? -1, -1);
        if (state == null || currentTu < 0)
            return false;
        bool changed = false;
        foreach (BattleUnitState unit in state.GetUnitsTyped())
        {
            BattleAiBlackboard blackboard = unit?.ai_blackboard;
            if (
                unit == null
                || !unit.is_alive
                || blackboard?.summoned != true
                || blackboard.summon_expires_at_tu < 0
                || currentTu < blackboard.summon_expires_at_tu
            )
            {
                continue;
            }
            RemoveSummonedUnit(state, unit, batch, "召唤单位持续时间结束。");
            changed = true;
        }
        return changed;
    }

    private void RemoveSummonedUnit(
        BattleState state,
        BattleUnitState unit,
        BattleEventBatch batch,
        string logLine
    )
    {
        if (state == null || unit == null)
            return;
        if (_runtime?.GetState() == state)
        {
            _runtime.RemoveSummonedUnitFromBattle(unit, batch, logLine);
            return;
        }
        List<Vector2I> previousCoords = new(unit.GetOccupiedCoordsTyped());
        unit.MarkDead();
        _runtime?._grid_service.ClearUnitOccupancy(state, unit);
        batch?.AddChangedUnitId(unit.unit_id);
        foreach (Vector2I coord in previousCoords)
            batch?.AddChangedCoord(coord);
        if (!string.IsNullOrEmpty(logLine))
            batch?.AddLogLine(logLine);
    }

    private BattleUnitState BuildSummonedUnit(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleEquipmentAbilitySourceState source,
        EquipmentAbilityBindingDefinition binding,
        SummonUnitsActionPayloadDefinition payload,
        Vector2I coord
    )
    {
        if (state == null || sourceUnit == null || binding == null || payload == null)
            return null;
        int hpMax = Math.Max(payload.HpMax, 1);
        int actionPoints = Math.Max(payload.ActionPoints, 0);
        int movePoints = Math.Max(payload.MovePoints, 0);
        StringName unitId = BuildSummonedUnitId(
            state,
            payload.UnitIdPrefix == "" ? new StringName("summoned_unit") : payload.UnitIdPrefix,
            sourceUnit.unit_id,
            binding.BindingId,
            payload.StateKey
        );
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = string.IsNullOrWhiteSpace(payload.UnitDisplayName)
                ? unitId.ToString()
                : payload.UnitDisplayName,
            faction_id = sourceUnit.faction_id,
            control_mode = payload.ControlMode == "" ? new StringName("ai") : payload.ControlMode,
            ai_brain_id = payload.AiBrainId,
            ai_state_id = payload.AiStateId,
            is_alive = true,
            current_hp = hpMax,
            current_ap = actionPoints,
            current_move_points = movePoints,
        };
        unit.SetAnchorCoord(coord);
        unit.SetBodySizeCategory(
            payload.BodySizeCategory == "" ? new StringName("tiny") : payload.BodySizeCategory
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hpMax);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, Math.Max(payload.ArmorClass, 1));
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, payload.AttackBonus);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, payload.BaseAttackBonus);
        unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, actionPoints);
        unit.SetCombatResources(hpMax, 0, 0, 0, actionPoints, movePoints);
        foreach (StringName tag in payload.CreatureTypeTags ?? Array.Empty<StringName>())
        {
            if (tag != "" && !unit.creature_type_tags.Contains(tag))
                unit.creature_type_tags.Add(tag);
        }
        foreach (StringName tag in payload.MovementTags ?? Array.Empty<StringName>())
        {
            if (tag != "" && !unit.movement_tags.Contains(tag))
                unit.movement_tags.Add(tag);
        }
        unit.ai_blackboard.summoned = true;
        unit.ai_blackboard.temporary_unit = true;
        unit.ai_blackboard.summon_source_unit_id = sourceUnit.unit_id;
        unit.ai_blackboard.summon_source_equipment_instance_id =
            source?.SourceEquipmentInstanceId ?? "";
        unit.ai_blackboard.summon_binding_id = binding.BindingId;
        unit.ai_blackboard.summon_state_key = payload.StateKey;
        unit.ai_blackboard.summon_expires_at_tu = payload.DurationTu > 0
            ? Math.Max(state.timeline?.current_tu ?? 0, 0) + payload.DurationTu
            : -1;
        return unit;
    }

    private IEnumerable<Vector2I> CollectSummonSpawnCoords(
        BattleState state,
        BattleUnitState anchorUnit,
        int radius
    )
    {
        if (state == null || anchorUnit == null)
            yield break;
        int resolvedRadius = Math.Max(radius, 0);
        Vector2I anchor = anchorUnit.coord;
        var coords = new List<Vector2I>();
        for (int y = anchor.Y - resolvedRadius; y <= anchor.Y + resolvedRadius; y++)
        {
            for (int x = anchor.X - resolvedRadius; x <= anchor.X + resolvedRadius; x++)
            {
                Vector2I coord = new(x, y);
                if (_runtime._grid_service.GetDistance(anchor, coord) <= resolvedRadius)
                    coords.Add(coord);
            }
        }
        coords.Sort(
            (left, right) =>
            {
                int leftDistance = _runtime._grid_service.GetDistance(anchor, left);
                int rightDistance = _runtime._grid_service.GetDistance(anchor, right);
                if (leftDistance != rightDistance)
                    return leftDistance.CompareTo(rightDistance);
                if (left.Y != right.Y)
                    return left.Y.CompareTo(right.Y);
                return left.X.CompareTo(right.X);
            }
        );
        foreach (Vector2I coord in coords)
            yield return coord;
    }

    private static void AddSummonedUnitToFactionList(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState summoned
    )
    {
        if (state == null || sourceUnit == null || summoned == null)
            return;
        if (state.ally_unit_ids.Contains(sourceUnit.unit_id))
        {
            if (!state.ally_unit_ids.Contains(summoned.unit_id))
                state.ally_unit_ids.Add(summoned.unit_id);
            return;
        }
        if (state.enemy_unit_ids.Contains(sourceUnit.unit_id))
        {
            if (!state.enemy_unit_ids.Contains(summoned.unit_id))
                state.enemy_unit_ids.Add(summoned.unit_id);
            return;
        }
        if (sourceUnit.faction_id == "player")
            state.ally_unit_ids.Add(summoned.unit_id);
        else
            state.enemy_unit_ids.Add(summoned.unit_id);
    }

    private static StringName BuildSummonedUnitId(
        BattleState state,
        StringName prefix,
        StringName sourceUnitId,
        StringName bindingId,
        StringName stateKey
    )
    {
        string baseId = string.Join(
            "_",
            SanitizeIdPart(prefix.ToString()),
            SanitizeIdPart(sourceUnitId.ToString()),
            SanitizeIdPart(bindingId.ToString()),
            SanitizeIdPart(stateKey.ToString())
        );
        for (int suffix = 1; suffix < 10000; suffix++)
        {
            StringName candidate = new($"{baseId}_{suffix}");
            if (state?.GetUnit(candidate) == null)
                return candidate;
        }
        return new StringName($"{baseId}_{Guid.NewGuid():N}");
    }

    private static string SanitizeIdPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "summon";
        char[] chars = value.ToCharArray();
        for (int index = 0; index < chars.Length; index++)
        {
            char c = chars[index];
            if (!char.IsLetterOrDigit(c) && c != '_')
                chars[index] = '_';
        }
        return new string(chars);
    }

    private int CountLivingSummonedUnits(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleEquipmentAbilitySourceState source,
        StringName bindingId,
        StringName stateKey,
        BattleUnitState radiusSubject = null,
        int radius = -1
    ) => CollectLivingSummonedUnits(
        state,
        sourceUnit,
        source,
        bindingId,
        stateKey,
        radiusSubject,
        radius
    ).Count;

    private List<BattleUnitState> CollectLivingSummonedUnits(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleEquipmentAbilitySourceState source,
        StringName bindingId,
        StringName stateKey,
        BattleUnitState radiusSubject = null,
        int radius = -1
    )
    {
        var result = new List<BattleUnitState>();
        if (state == null || sourceUnit == null)
            return result;
        StringName sourceEquipmentInstanceId = source?.SourceEquipmentInstanceId ?? "";
        foreach (BattleUnitState unit in state.GetUnitsTyped())
        {
            if (
                !SummonedUnitMatches(
                    unit,
                    sourceUnit.unit_id,
                    sourceEquipmentInstanceId,
                    bindingId,
                    stateKey
                )
            )
            {
                continue;
            }
            if (
                radiusSubject != null
                && radius >= 0
                && DistanceBetweenUnits(unit, radiusSubject) > radius
            )
            {
                continue;
            }
            result.Add(unit);
        }
        return result;
    }

    private bool SummonedUnitMatches(
        BattleUnitState unit,
        StringName sourceUnitId,
        StringName sourceEquipmentInstanceId,
        StringName bindingId,
        StringName stateKey
    )
    {
        BattleAiBlackboard blackboard = unit?.ai_blackboard;
        if (unit == null || !unit.is_alive || blackboard?.summoned != true)
            return false;
        if (sourceUnitId != "" && blackboard.summon_source_unit_id != sourceUnitId)
            return false;
        if (
            sourceEquipmentInstanceId != ""
            && blackboard.summon_source_equipment_instance_id != sourceEquipmentInstanceId
        )
        {
            return false;
        }
        if (bindingId != "" && blackboard.summon_binding_id != bindingId)
            return false;
        if (stateKey != "" && blackboard.summon_state_key != stateKey)
            return false;
        return true;
    }

    private int DistanceBetweenUnits(BattleUnitState first, BattleUnitState second)
    {
        if (first == null || second == null)
            return 999999;
        int best = 999999;
        foreach (Vector2I firstCoord in first.GetOccupiedCoordsTyped())
        {
            foreach (Vector2I secondCoord in second.GetOccupiedCoordsTyped())
                best = Math.Min(best, _runtime._grid_service.GetDistance(firstCoord, secondCoord));
        }
        return best;
    }

    private void SortSummonsForConsumption(
        List<BattleUnitState> summons,
        BattleUnitState targetUnit
    )
    {
        if (summons == null)
            return;
        summons.Sort(
            (left, right) =>
            {
                int leftDistance = targetUnit != null ? DistanceBetweenUnits(left, targetUnit) : 0;
                int rightDistance = targetUnit != null ? DistanceBetweenUnits(right, targetUnit) : 0;
                if (leftDistance != rightDistance)
                    return leftDistance.CompareTo(rightDistance);
                return string.CompareOrdinal(left?.unit_id.ToString(), right?.unit_id.ToString());
            }
        );
    }

    private void ResolveApplyStatusAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        ApplyStatusActionPayloadDefinition payload,
        BattleEquipmentAbilityOnKillContext context,
        BattleEquipmentAbilityOnKillResult result
    )
    {
        BattleUnitState targetUnit = ResolveSubject(
            payload?.TargetSelector ?? "",
            context.SourceUnit,
            context.DefeatedUnit
        );
        ResolveApplyStatusAction(
            binding,
            action,
            payload,
            context.SourceUnit,
            targetUnit,
            context.SaveContext,
            result != null ? result.AddStatusResult : null
        );
    }

    private BattleLootEntry ApplyLootQuantityMultipliers(
        BattleLootEntry entry,
        BattleEquipmentAbilityOnKillResult onKillResult
    )
    {
        if (entry == null || onKillResult == null || onKillResult.LootMultipliers.Count == 0)
            return entry?.Duplicate();
        int multiplierPercent = 100;
        foreach (BattleEquipmentAbilityLootQuantityMultiplierResult multiplier in onKillResult.LootMultipliers)
        {
            if (LootMultiplierApplies(entry, multiplier?.Payload))
                multiplierPercent = Math.Max(multiplierPercent * multiplier.Payload.MultiplierPercent / 100, 0);
        }
        if (multiplierPercent == 100)
            return entry.Duplicate();
        return entry.WithQuantity(Math.Max(entry.Quantity * multiplierPercent / 100, 1));
    }

    private bool LootMultiplierApplies(
        BattleLootEntry entry,
        LootQuantityMultiplierActionPayloadDefinition payload
    )
    {
        if (entry == null || payload == null || payload.MultiplierPercent <= 0)
            return false;
        if (payload.AffectedDropKinds != null && payload.AffectedDropKinds.Count > 0)
        {
            StringName entryDropKind = BattleLootIds.ToStringName(entry.DropKind);
            bool matchedDropKind = false;
            foreach (StringName affectedDropKind in payload.AffectedDropKinds)
            {
                if (entryDropKind == affectedDropKind)
                {
                    matchedDropKind = true;
                    break;
                }
            }
            if (!matchedDropKind)
                return false;
        }
        if (payload.AnyItemTags == null || payload.AnyItemTags.Count == 0)
            return true;
        ItemDef itemDef = ResolveItemDef(entry.ItemId);
        return AnyTagPresent(itemDef, payload.AnyItemTags);
    }

    private void ResolveActions(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        BattleEquipmentAbilityAfterHitContext context,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
        {
            if (
                action == null
                || !ConditionGroupPasses(
                    action.ConditionGroup,
                    context.SourceUnit,
                    context.TargetUnit,
                    EquipmentAbilityFactContext.FromAfterHit(context),
                    activeBinding
                )
            )
            {
                continue;
            }
            if (
                !RollGatePasses(
                    action.RollGate,
                    binding.BindingId,
                    reaction.ReactionId,
                    action.ActionId,
                    context.ForcedRollValue,
                    result
                )
            )
            {
                continue;
            }

            if (
                action.Kind == ActionKindEquipmentDurabilityDamage
                && action.PayloadDefinition is EquipmentDurabilityDamageActionPayloadDefinition durabilityPayload
            )
            {
                ResolveEquipmentDurabilityAction(binding, action, durabilityPayload, context, result);
            }
            else if (
                context.ApplyDamageDiceActions
                &&
                action.Kind == ActionKindAddDamageDice
                && action.PayloadDefinition is AddDamageDiceActionPayloadDefinition dicePayload
            )
            {
                ResolveAddDamageDiceAction(binding, action, dicePayload, result);
            }
            else if (
                action.Kind == ActionKindDealDamage
                && action.PayloadDefinition is DealDamageActionPayloadDefinition dealDamagePayload
            )
            {
                ResolveDealDamageAction(
                    activeBinding,
                    binding,
                    dealDamagePayload,
                    context
                );
            }
            else if (
                action.Kind == ActionKindHeal
                && action.PayloadDefinition is HealActionPayloadDefinition healPayload
            )
            {
                ResolveHealAction(
                    activeBinding,
                    binding,
                    healPayload,
                    context?.SourceUnit,
                    context?.TargetUnit,
                    context?.BattleState
                );
            }
            else if (
                action.Kind == ActionKindApplyStatus
                && action.PayloadDefinition is ApplyStatusActionPayloadDefinition statusPayload
            )
            {
                ResolveApplyStatusAction(binding, action, statusPayload, context, result);
            }
            else if (
                action.Kind == ActionKindModifyAbilityState
                && action.PayloadDefinition is ModifyAbilityStateActionPayloadDefinition statePayload
            )
            {
                ResolveModifyAbilityStateAction(
                    activeBinding,
                    binding,
                    statePayload,
                    context.SourceUnit,
                    context.TargetUnit
                );
            }
            else if (
                action.Kind == ActionKindClearStatus
                && action.PayloadDefinition is ClearStatusActionPayloadDefinition clearStatusPayload
            )
            {
                ResolveClearStatusAction(
                    activeBinding,
                    binding,
                    clearStatusPayload,
                    context.SourceUnit,
                    context.TargetUnit,
                    context.BattleState
                );
            }
        }
    }

    private void ResolveApplyStatusAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        ApplyStatusActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitContext context,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        BattleUnitState targetUnit = ResolveSubject(
            payload?.TargetSelector ?? "",
            context.SourceUnit,
            context.TargetUnit
        );
        ResolveApplyStatusAction(
            binding,
            action,
            payload,
            context.SourceUnit,
            targetUnit,
            context.SaveContext,
            result != null ? result.AddStatusResult : null
        );
    }

    private void ResolveDealDamageAction(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        DealDamageActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitContext context
    )
    {
        ResolveDealDamageAction(
            activeBinding,
            binding,
            payload,
            context?.SourceUnit,
            context?.TargetUnit,
            context?.BattleState
        );
    }

    private BattleUnitState ResolveDealDamageAction(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        DealDamageActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleState battleState
    )
    {
        if (_damageResolver == null || payload?.Dice == null || sourceUnit == null)
            return null;
        BattleUnitState resolvedTarget = ResolveEquipmentActionTarget(
            payload.TargetSelector,
            sourceUnit,
            targetUnit,
            activeBinding,
            binding,
            "",
            "",
            battleState
        );
        if (resolvedTarget?.is_alive != true)
            return null;
        IReadOnlyList<CombatEffectDefinition> effects = BuildDamageEffects(payload);
        if (effects.Count == 0)
            return null;
        _damageResolver.ResolveEffects(
            sourceUnit,
            resolvedTarget,
            effects,
            DamageResolutionContext.Empty()
        );
        return resolvedTarget;
    }

    private BattleUnitState ResolveHealAction(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        HealActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleState battleState
    )
    {
        if (_damageResolver == null || payload?.Dice == null || sourceUnit == null)
            return null;
        BattleUnitState resolvedTarget = ResolveEquipmentActionTarget(
            payload.TargetSelector,
            sourceUnit,
            targetUnit,
            activeBinding,
            binding,
            "",
            "",
            battleState
        );
        if (resolvedTarget == null)
            return null;
        IReadOnlyList<CombatEffectDefinition> effects = BuildHealEffects(payload);
        if (effects.Count == 0)
            return null;
        int previousHp = resolvedTarget.current_hp;
        bool previousAlive = resolvedTarget.is_alive;
        _damageResolver.ResolveEffects(
            sourceUnit,
            resolvedTarget,
            effects,
            DamageResolutionContext.Empty()
        );
        if (resolvedTarget.current_hp == previousHp && resolvedTarget.is_alive == previousAlive)
            return null;
        return resolvedTarget;
    }

    private static IReadOnlyList<CombatEffectDefinition> BuildDamageEffects(
        DealDamageActionPayloadDefinition payload
    )
    {
        if (payload?.Dice == null || payload.DamageType == "")
            return Array.Empty<CombatEffectDefinition>();

        var result = new List<CombatEffectDefinition>();
        bool usedFlatBonus = false;
        int flatBonus = Math.Max(payload.Dice.FlatBonus, 0);
        foreach (DiceExpressionTermDefinition term in payload.Dice.Terms ?? Array.Empty<DiceExpressionTermDefinition>())
        {
            if (term == null || term.DiceCount <= 0 || term.DiceSides <= 0)
                continue;
            result.Add(
                BattleRuntimeEffectDefinitions.Damage(
                    payload.DamageType,
                    term.DiceCount,
                    term.DiceSides,
                    usedFlatBonus ? 0 : flatBonus,
                    payload.DamageTags,
                    payload.MitigationBypassDamageTags,
                    payload.MitigationBypassTiers
                )
            );
            usedFlatBonus = true;
        }
        if (result.Count == 0 && flatBonus > 0)
        {
            result.Add(
                BattleRuntimeEffectDefinitions.Damage(
                    payload.DamageType,
                    0,
                    0,
                    0,
                    payload.DamageTags,
                    payload.MitigationBypassDamageTags,
                    payload.MitigationBypassTiers,
                    power: flatBonus
                )
            );
        }
        return result;
    }

    private static IReadOnlyList<CombatEffectDefinition> BuildHealEffects(
        HealActionPayloadDefinition payload
    )
    {
        if (payload?.Dice == null)
            return Array.Empty<CombatEffectDefinition>();

        var result = new List<CombatEffectDefinition>();
        bool usedFlatBonus = false;
        int flatBonus = Math.Max(payload.Dice.FlatBonus, 0);
        foreach (DiceExpressionTermDefinition term in payload.Dice.Terms ?? Array.Empty<DiceExpressionTermDefinition>())
        {
            if (term == null || term.DiceCount <= 0 || term.DiceSides <= 0)
                continue;
            result.Add(
                BattleRuntimeEffectDefinitions.Heal(
                    term.DiceCount,
                    term.DiceSides,
                    usedFlatBonus ? 0 : flatBonus
                )
            );
            usedFlatBonus = true;
        }
        if (result.Count == 0 && flatBonus > 0)
        {
            result.Add(BattleRuntimeEffectDefinitions.Heal(0, 0, 0, flatBonus));
        }
        return result;
    }

    private bool ResolveClearStatusAction(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        ClearStatusActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleState battleState
    )
    {
        if (payload == null || sourceUnit == null || payload.StatusId == "")
            return false;
        BattleUnitState resolvedTarget = ResolveEquipmentActionTarget(
            payload.TargetSelector,
            sourceUnit,
            targetUnit,
            activeBinding,
            binding,
            payload.MarkBindingId,
            payload.MarkStateKey,
            battleState
        );
        BattleStatusEffectState existing = resolvedTarget?.GetStatusEffect(payload.StatusId);
        if (existing == null)
            return false;
        if (
            payload.RequireSourceUnitMatch
            && ProgressionDataUtils.to_string_name(existing.source_unit_id) != sourceUnit.unit_id
        )
        {
            return false;
        }
        resolvedTarget.EraseStatusEffect(payload.StatusId);
        return true;
    }

    private BattleUnitState ResolveEquipmentActionTarget(
        StringName targetSelector,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition fallbackBinding,
        StringName markBindingId,
        StringName markStateKey,
        BattleState battleState
    )
    {
        StringName selector = ProgressionDataUtils.to_string_name(targetSelector);
        if (selector == "marked_target" || selector == "equipment_target_mark")
        {
            return ResolveMarkedTarget(
                sourceUnit,
                activeBinding,
                fallbackBinding,
                markBindingId,
                markStateKey,
                battleState
            );
        }
        return ResolveSubject(selector, sourceUnit, targetUnit);
    }

    private BattleUnitState ResolveMarkedTarget(
        BattleUnitState sourceUnit,
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition fallbackBinding,
        StringName markBindingId,
        StringName markStateKey,
        BattleState battleState
    )
    {
        if (sourceUnit == null)
            return null;
        BattleState state = battleState ?? _runtime?.GetState();
        if (state == null)
            return null;
        EquipmentAbilityBindingDefinition markBinding = ResolveStateBinding(
            activeBinding,
            fallbackBinding,
            markBindingId
        );
        StringName stateKey = ProgressionDataUtils.to_string_name(markStateKey);
        if (markBinding == null || markBinding.BindingId == "" || stateKey == "")
            return null;
        if (!state.TryGetEquipmentTargetMark(
            sourceUnit.unit_id,
            activeBinding.Source?.SourceEquipmentInstanceId ?? "",
            markBinding.BindingId,
            stateKey,
            out BattleEquipmentTargetMarkState mark
        ))
            return null;
        if (ClearStaleEquipmentTargetMarkIfNeeded(state, markBinding, mark))
            return null;
        return state.GetUnit(mark.TargetUnitId);
    }

    private void ResolveModifyAbilityStateAction(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        ModifyAbilityStateActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        BattleUnitState owner = ResolveSubject(
            payload?.TargetSelector ?? "",
            sourceUnit,
            targetUnit
        );
        EquipmentAbilityBindingDefinition stateBinding = ResolveStateBinding(
            activeBinding,
            binding,
            payload?.BindingId ?? new StringName("")
        );
        StringName chargeKey = BuildBindingStateChargeKey(
            activeBinding.Source,
            stateBinding,
            payload?.StateKey ?? new StringName("")
        );
        if (owner == null || chargeKey == "")
            return;

        StringName operation = ProgressionDataUtils.to_string_name(payload.Operation);
        int current = GetAbilityStateValue(owner, stateBinding, chargeKey, payload.StateKey, 0);
        if (operation == "clear")
        {
            SetAbilityStateValue(owner, stateBinding, chargeKey, payload.StateKey, 0);
            return;
        }
        if (operation == "add")
        {
            SetAbilityStateValue(
                owner,
                stateBinding,
                chargeKey,
                payload.StateKey,
                current + payload.IntDelta
            );
            return;
        }
        SetAbilityStateValue(owner, stateBinding, chargeKey, payload.StateKey, payload.IntDelta);
    }

    private void ResolveApplyStatusAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        ApplyStatusActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleSaveContext saveContext,
        Action<BattleEquipmentAbilityStatusActionResult> addResult
    )
    {
        if (payload == null || sourceUnit == null || targetUnit == null || payload.StatusId == "")
            return;

        BattleSaveResult saveResult = default;
        if (payload.SaveDc > 0)
        {
            CombatEffectDefinition saveEffect = BattleRuntimeEffectDefinitions.StaticSave(
                payload.SaveDc,
                payload.SaveAbility,
                payload.SaveTag
            );
            saveResult = BattleSaveResolver.ResolveSaveResult(
                sourceUnit,
                targetUnit,
                saveEffect,
                saveContext
            );
            if (payload.ApplyOnSaveFailure && saveResult.Success)
            {
                addResult?.Invoke(
                    new BattleEquipmentAbilityStatusActionResult
                    {
                        BindingId = binding.BindingId,
                        ActionId = action.ActionId,
                        TargetUnitId = targetUnit.unit_id,
                        StatusId = payload.StatusId,
                        Applied = false,
                        SaveResult = saveResult,
                    }
                );
                return;
            }
        }

        int durationTu = ResolveStatusDurationTu(payload);
        CombatEffectDefinition statusEffect = BattleRuntimeEffectDefinitions.Status(
            payload.StatusId,
            Math.Max(payload.StackDelta, 1),
            durationTu,
            stackBehavior: payload.StackBehavior,
            stackLimit: payload.StackLimit,
            displayName: payload.DisplayLabel,
            attackRollPenalty: payload.AttackRollPenalty,
            sourceBoundAttackRollPenalty: payload.SourceBoundAttackRollPenalty,
            sourceBoundAttackRollPenaltyMinStacks: payload.SourceBoundAttackRollPenaltyMinStacks,
            sourceBoundIncomingAttackRollBonusPerStack:
                payload.SourceBoundIncomingAttackRollBonusPerStack,
            sourceBoundIncomingAttackRollBonusMinStacks:
                payload.SourceBoundIncomingAttackRollBonusMinStacks,
            countsAsDebuffOverride: payload.CountsAsDebuffOverride,
            countsAsDebuff: payload.CountsAsDebuff,
            undispellable: payload.Undispellable,
            dispellableMagic: payload.DispellableMagic,
            dispellableHarmfulMagic: payload.DispellableHarmfulMagic,
            dispellableBeneficialMagic: payload.DispellableBeneficialMagic,
            lockCounterattack: payload.LockCounterattack,
            lockGuard: payload.LockGuard,
            lockDodgeBonus: payload.LockDodgeBonus
        );
        BattleStatusEffectState statusEntry = BattleStatusSemanticTable.MergeStatus(
            statusEffect,
            sourceUnit.unit_id,
            targetUnit.GetStatusEffect(payload.StatusId),
            payload.StatusId
        );
        if (statusEntry == null)
            return;
        ApplyStatusTimelineDamagePayload(statusEntry, payload);
        if (payload.MovePointCapacityDelta != 0)
            statusEntry.move_point_capacity_delta = payload.MovePointCapacityDelta;
        targetUnit.SetStatusEffect(statusEntry);
        if (payload.MovePointCapacityDelta != 0)
            targetUnit.ClampCurrentMovePointsToCapacity();
        _runtime?.MarkAppliedStatusesForTurnTiming(
            targetUnit,
            new Godot.Collections.Array<StringName> { payload.StatusId }
        );
        addResult?.Invoke(
            new BattleEquipmentAbilityStatusActionResult
            {
                BindingId = binding.BindingId,
                ActionId = action.ActionId,
                TargetUnitId = targetUnit.unit_id,
                StatusId = payload.StatusId,
                Applied = true,
                SaveResult = saveResult,
            }
        );
    }

    private static void ApplyStatusTimelineDamagePayload(
        BattleStatusEffectState statusEntry,
        ApplyStatusActionPayloadDefinition payload
    )
    {
        if (statusEntry == null || payload == null)
            return;
        if (payload.TickIntervalTu > 0)
            statusEntry.tick_interval_tu = payload.TickIntervalTu;
        if (payload.TimelineDamageDiceCount > 0 && payload.TimelineDamageDiceSides > 0)
        {
            statusEntry.timeline_damage_dice_count = payload.TimelineDamageDiceCount;
            statusEntry.timeline_damage_dice_sides = payload.TimelineDamageDiceSides;
            statusEntry.timeline_damage_flat_bonus = Math.Max(payload.TimelineDamageFlatBonus, 0);
        }
    }

    private void CollectBonusDamageDiceActions(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityReactionDefinition reaction,
        BattleEquipmentAbilityBonusDamageDiceContext context,
        List<BattleEquipmentAbilityBonusDamageDiceResult> result
    )
    {
        EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
        foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
        {
            if (
                action == null
                || action.Kind != ActionKindAddDamageDice
                || action.PayloadDefinition is not AddDamageDiceActionPayloadDefinition dicePayload
                || !ConditionGroupPasses(
                    action.ConditionGroup,
                    context.SourceUnit,
                    context.TargetUnit,
                    EquipmentAbilityFactContext.FromBonusDamageDice(context),
                    activeBinding
                )
            )
            {
                continue;
            }
            if (
                !RollGatePasses(
                    action.RollGate,
                    binding.BindingId,
                    reaction.ReactionId,
                    action.ActionId,
                    forcedRollValue: 0,
                    new BattleEquipmentAbilityAfterHitResult()
                )
            )
            {
                continue;
            }
            if (
                !TryConsumeOnceScope(
                    activeBinding.Source,
                    binding,
                    reaction,
                    action,
                    context.SourceUnit
                )
            )
            {
                continue;
            }
            AppendBonusDamageDiceResult(binding, action, dicePayload, result);
        }
    }

    private static bool TryConsumeOnceScope(
        BattleEquipmentAbilitySourceState source,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        EquipmentAbilityActionDefinition action,
        BattleUnitState owner
    )
    {
        if (reaction?.OnceScope != OnceScopeTurn)
            return true;
        if (owner == null || binding == null || action == null)
            return false;

        StringName chargeKey = BuildOnceScopeTurnChargeKey(source, binding, reaction, action);
        if (chargeKey == "")
            return false;
        if (!owner.HasPerTurnChargeLimitTyped(chargeKey))
        {
            owner.SetPerTurnChargeLimitTyped(chargeKey, 1);
        }
        if (!owner.HasPerTurnChargeTyped(chargeKey))
        {
            owner.SetPerTurnChargeTyped(chargeKey, owner.GetPerTurnChargeLimitTyped(chargeKey, 1));
        }

        int charge = owner.GetPerTurnChargeTyped(chargeKey, 0);
        if (charge <= 0)
            return false;
        owner.SetPerTurnChargeTyped(chargeKey, charge - 1);
        return true;
    }

    private static StringName BuildOnceScopeTurnChargeKey(
        BattleEquipmentAbilitySourceState source,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        EquipmentAbilityActionDefinition action
    )
    {
        StringName ownerSourceKey = source?.EffectiveInstanceKey ?? new StringName("");
        StringName sourceInstanceId = source?.SourceEquipmentInstanceId ?? new StringName("");
        if (ownerSourceKey == "")
        {
            ownerSourceKey = source?.EquipmentDefId ?? new StringName("");
        }
        if (ownerSourceKey == "" && sourceInstanceId == "")
        {
            return "";
        }
        StringName bindingId = ProgressionDataUtils.to_string_name(binding?.BindingId ?? new StringName(""));
        StringName reactionId = ProgressionDataUtils.to_string_name(reaction?.ReactionId ?? new StringName(""));
        StringName actionId = ProgressionDataUtils.to_string_name(action?.ActionId ?? new StringName(""));
        if (bindingId == "" || actionId == "")
        {
            return "";
        }
        return new StringName(
            string.Join(
                "|",
                "equipment_ability",
                "turn",
                ownerSourceKey.ToString(),
                sourceInstanceId.ToString(),
                bindingId.ToString(),
                reactionId.ToString(),
                actionId.ToString()
            )
        );
    }

    private static StringName BuildBindingStateChargeKey(
        BattleEquipmentAbilitySourceState source,
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        StringName normalizedStateKey = ProgressionDataUtils.to_string_name(stateKey);
        StringName ownerSourceKey = source?.SourceEquipmentInstanceId ?? new StringName("");
        if (ownerSourceKey == "")
            ownerSourceKey = source?.EquipmentDefId ?? new StringName("");
        if (ownerSourceKey == "")
            ownerSourceKey = source?.EffectiveInstanceKey ?? new StringName("");
        if (ownerSourceKey == "" || normalizedStateKey == "")
            return "";
        return new StringName(
            string.Join(
                "|",
                "equipment_ability",
                "state",
                ownerSourceKey.ToString(),
                normalizedStateKey.ToString()
            )
        );
    }

    private EquipmentAbilityBindingDefinition ResolveStateBinding(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition fallbackBinding,
        StringName bindingId
    )
    {
        StringName normalizedBindingId = ProgressionDataUtils.to_string_name(bindingId);
        if (normalizedBindingId == "")
            return fallbackBinding;
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex =
            _runtime?.GetEquipmentAbilityBindingIndexTyped();
        if (
            bindingIndex != null
            && bindingIndex.TryGetValue(normalizedBindingId, out EquipmentAbilityBindingDefinition binding)
            && binding != null
        )
        {
            return binding;
        }
        return fallbackBinding;
    }

    private static EquipmentAbilityStateSchemaDefinition FindStateSchema(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        StringName normalizedStateKey = ProgressionDataUtils.to_string_name(stateKey);
        if (binding == null || normalizedStateKey == "")
            return null;
        foreach (EquipmentAbilityStateSchemaDefinition schema in binding.StateSchemas ?? Array.Empty<EquipmentAbilityStateSchemaDefinition>())
        {
            if (schema?.StateKey == normalizedStateKey)
                return schema;
        }
        return null;
    }

    private static bool IsPerBattleState(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        EquipmentAbilityStateSchemaDefinition schema = FindStateSchema(binding, stateKey);
        StringName resetTiming = ProgressionDataUtils.to_string_name(schema?.ResetTiming ?? new StringName(""));
        return resetTiming == ResetTimingPerBattle || resetTiming == ResetTimingBattle;
    }

    private static int GetAbilityStateValue(
        BattleUnitState owner,
        EquipmentAbilityBindingDefinition binding,
        StringName chargeKey,
        StringName stateKey,
        int fallback
    )
    {
        if (owner == null || chargeKey == "")
            return fallback;
        EquipmentAbilityStateSchemaDefinition schema = FindStateSchema(binding, stateKey);
        int initial = schema != null ? Math.Max(schema.InitialIntValue, 0) : Math.Max(fallback, 0);
        return IsPerBattleState(binding, stateKey)
            ? owner.GetPerBattleChargeTyped(chargeKey, initial)
            : owner.GetPerTurnChargeTyped(chargeKey, initial);
    }

    private static void SetAbilityStateValue(
        BattleUnitState owner,
        EquipmentAbilityBindingDefinition binding,
        StringName chargeKey,
        StringName stateKey,
        int value
    )
    {
        if (owner == null || chargeKey == "")
            return;
        EquipmentAbilityStateSchemaDefinition schema = FindStateSchema(binding, stateKey);
        int normalizedValue = Math.Max(value, 0);
        if (schema != null && schema.MaxIntValue > 0)
            normalizedValue = Math.Min(normalizedValue, schema.MaxIntValue);

        if (IsPerBattleState(binding, stateKey))
            owner.SetPerBattleChargeTyped(chargeKey, normalizedValue);
        else
            owner.SetPerTurnChargeTyped(chargeKey, normalizedValue);
    }

    private void ResolveEquipmentDurabilityAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        EquipmentDurabilityDamageActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitContext context,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        if (_damageResolver == null || payload == null)
            return;
        BattleDamageResolver.EquipmentDurabilitySelectionResult selectionResult =
            _damageResolver.SelectEquipmentForDurabilityDamage(
                new BattleDamageResolver.EquipmentDurabilitySelectionQuery
                {
                    TargetUnit = context.TargetUnit,
                    TargetSlots = payload.TargetSlots,
                    SlotWeights = payload.SlotWeights,
                    ConsumeRandom = true,
                }
            );
        if (!selectionResult.HasSelection)
            return;
        EquipmentAbilityEquipmentTargetRef target = selectionResult.SelectedTarget;
        if (!EquipmentTargetMatchesRequirements(target, payload))
            return;

        EquipmentDurabilityCommitResult commit =
            _damageResolver.ApplyEquipmentDurabilityDamageToSelection(
                new EquipmentDurabilityDirectCommitRequest
                {
                    SourceUnit = context.SourceUnit,
                    TargetUnit = context.TargetUnit,
                    TargetEquipment = target,
                    DurabilityLoss = payload.DurabilityLoss,
                    SourceKey = binding.BindingId,
                    ActionId = action.ActionId,
                }
            );
        if (commit.Resolved)
        {
            result.AddDurabilityResult(
                new BattleEquipmentAbilityDurabilityResult
                {
                    BindingId = binding.BindingId,
                    ActionId = action.ActionId,
                    CommitResult = commit,
                }
            );
        }
    }

    private void ResolveAddDamageDiceAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        AddDamageDiceActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        if (payload?.Dice == null)
            return;
        AppendBonusDamageDiceResult(binding, action, payload, result);
    }

    private static void AppendBonusDamageDiceResult(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        AddDamageDiceActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        if (result == null)
            return;
        foreach (BattleEquipmentAbilityBonusDamageDiceResult dice in BuildBonusDamageDiceResults(
            binding,
            action,
            payload
        ))
        {
            result.AddBonusDamageDice(dice);
        }
    }

    private static void AppendBonusDamageDiceResult(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        AddDamageDiceActionPayloadDefinition payload,
        List<BattleEquipmentAbilityBonusDamageDiceResult> result
    )
    {
        if (result == null)
            return;
        result.AddRange(BuildBonusDamageDiceResults(binding, action, payload));
    }

    private static IEnumerable<BattleEquipmentAbilityBonusDamageDiceResult> BuildBonusDamageDiceResults(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        AddDamageDiceActionPayloadDefinition payload
    )
    {
        if (payload?.Dice == null)
            yield break;
        bool emittedTerm = false;
        foreach (DiceExpressionTermDefinition term in payload.Dice.Terms ?? Array.Empty<DiceExpressionTermDefinition>())
        {
            if (term == null || term.DiceCount <= 0 || term.DiceSides <= 0)
                continue;
            emittedTerm = true;
            yield return new BattleEquipmentAbilityBonusDamageDiceResult
            {
                BindingId = binding.BindingId,
                ActionId = action.ActionId,
                DiceCount = term.DiceCount,
                DiceSides = term.DiceSides,
                FlatBonus = Math.Max(payload.Dice.FlatBonus, 0),
                Subtract = payload.Subtract,
                DamageType = payload.DamageType,
                DamageTags = CopyStringNames(payload.DamageTags),
                MitigationBypassDamageTags = CopyStringNames(payload.MitigationBypassDamageTags),
                MitigationBypassTiers = CopyStringNames(payload.MitigationBypassTiers),
            };
        }
        if (!emittedTerm && payload.Dice.FlatBonus > 0)
        {
            yield return new BattleEquipmentAbilityBonusDamageDiceResult
            {
                BindingId = binding.BindingId,
                ActionId = action.ActionId,
                DiceCount = 0,
                DiceSides = 0,
                FlatBonus = payload.Dice.FlatBonus,
                Subtract = payload.Subtract,
                DamageType = payload.DamageType,
                DamageTags = CopyStringNames(payload.DamageTags),
                MitigationBypassDamageTags = CopyStringNames(payload.MitigationBypassDamageTags),
                MitigationBypassTiers = CopyStringNames(payload.MitigationBypassTiers),
            };
        }
    }

    private static IReadOnlyList<StringName> CopyStringNames(IReadOnlyList<StringName> values)
    {
        if (values == null || values.Count == 0)
            return Array.Empty<StringName>();
        StringName[] result = new StringName[values.Count];
        for (int index = 0; index < values.Count; index++)
            result[index] = ProgressionDataUtils.to_string_name(values[index]);
        return result;
    }

    private static bool HasAddDamageDiceAction(EquipmentAbilityReactionDefinition reaction)
    {
        foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
        {
            if (
                action?.Kind == ActionKindAddDamageDice
                && action.PayloadDefinition is AddDamageDiceActionPayloadDefinition
            )
            {
                return true;
            }
        }
        return false;
    }

    private bool EquipmentTargetMatchesRequirements(
        EquipmentAbilityEquipmentTargetRef target,
        EquipmentDurabilityDamageActionPayloadDefinition payload
    )
    {
        if (target == null || payload == null)
            return false;
        if (!EquipmentTargetMatchesRarity(target, payload))
            return false;
        ItemDef itemDef = ResolveItemDef(target.ItemId);
        if (itemDef == null)
            return false;
        if (!AllTagsPresent(itemDef, payload.RequiredItemTags))
            return false;
        if (payload.RequiredEquipmentTypeIds != null && payload.RequiredEquipmentTypeIds.Count > 0)
        {
            StringName equipmentTypeId = itemDef.GetEquipmentTypeIdNormalized();
            bool matchedType = false;
            foreach (StringName requiredTypeId in payload.RequiredEquipmentTypeIds)
            {
                if (equipmentTypeId == ProgressionDataUtils.to_string_name(requiredTypeId))
                {
                    matchedType = true;
                    break;
                }
            }
            if (!matchedType)
                return false;
        }
        return true;
    }

    private static bool EquipmentTargetMatchesRarity(
        EquipmentAbilityEquipmentTargetRef target,
        EquipmentDurabilityDamageActionPayloadDefinition payload
    )
    {
        int maxTargetRarity = payload?.MaxTargetRarity ?? -1;
        if (maxTargetRarity < 0)
            return true;
        return target != null
            && EquipmentInstanceState.IsValidRarity(target.Rarity)
            && target.Rarity <= maxTargetRarity;
    }

    private bool RollGatePasses(
        EquipmentRollGateDefinition rollGate,
        StringName bindingId,
        StringName reactionId,
        StringName actionId,
        int forcedRollValue,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        if (rollGate == null)
            return true;
        int rolledValue = ResolveRollGateValue(forcedRollValue, rollGate.Roll);
        bool passed = CompareInt(rolledValue, rollGate.Compare, rollGate.Threshold);
        result?.AddRoll(
            new BattleEquipmentAbilityRollResult
            {
                BindingId = bindingId,
                ReactionId = reactionId,
                ActionId = actionId,
                RolledValue = rolledValue,
                Compare = rollGate.Compare,
                Threshold = rollGate.Threshold,
                Passed = passed,
            }
        );
        return passed;
    }

    private int ResolveRollGateValue(int forcedRollValue, DiceExpressionDefinition dice)
    {
        if (forcedRollValue > 0)
            return forcedRollValue;
        if (_forcedRollGateValuesForTests.Count > 0)
            return _forcedRollGateValuesForTests.Dequeue();
        return RollDiceExpression(dice);
    }

    private static int RollDiceExpression(DiceExpressionDefinition dice)
    {
        if (dice == null)
            return 0;
        int total = Math.Max(dice.FlatBonus, 0);
        foreach (DiceExpressionTermDefinition term in dice.Terms ?? Array.Empty<DiceExpressionTermDefinition>())
        {
            if (term == null || term.DiceCount <= 0 || term.DiceSides <= 0)
                continue;
            for (int index = 0; index < term.DiceCount; index++)
                total += TrueRandomSeedService.RandiRange(1, term.DiceSides);
        }
        return total;
    }

    private static int ResolveStatusDurationTu(ApplyStatusActionPayloadDefinition payload)
    {
        if (payload == null)
            return 0;
        if (payload.DurationTu > 0)
            return payload.DurationTu;
        return Math.Max(payload.DurationTurns, 0);
    }

    private static string ResolveActionLabel(
        EquipmentAbilityBindingDefinition binding,
        string actionLabel
    )
    {
        string label = actionLabel?.StripEdges() ?? "";
        if (label.Length > 0)
            return label;
        foreach (EquipmentAbilitySourceTraceDefinition trace in binding?.SourceTraces ?? Array.Empty<EquipmentAbilitySourceTraceDefinition>())
        {
            string title = trace?.BulletTitle?.StripEdges() ?? "";
            if (title.Length > 0)
                return title;
            string displayName = trace?.DisplayName?.StripEdges() ?? "";
            if (displayName.Length > 0)
                return displayName;
        }
        return binding?.BindingId.ToString() ?? "";
    }

    private static bool CompareInt(int value, StringName compare, int threshold)
    {
        return ProgressionDataUtils.to_string_name(compare).ToString() switch
        {
            "lte" => value <= threshold,
            "lt" => value < threshold,
            "gte" => value >= threshold,
            "gt" => value > threshold,
            "eq" => value == threshold,
            _ => false,
        };
    }

    private bool ConditionGroupPasses(
        EquipmentConditionGroupDefinition group,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext = default,
        ActiveEquipmentAbilityBinding activeBinding = default
    )
    {
        if (group == null)
            return true;
        bool anyMode = group.Mode == "any";
        bool sawAny = false;
        bool passed = anyMode ? false : true;

        foreach (EquipmentAbilityConditionDefinition condition in group.Conditions ?? Array.Empty<EquipmentAbilityConditionDefinition>())
        {
            sawAny = true;
            bool conditionPassed = ConditionPasses(
                condition,
                sourceUnit,
                targetUnit,
                factContext,
                activeBinding
            );
            if (anyMode)
            {
                passed = passed || conditionPassed;
            }
            else
            {
                passed = passed && conditionPassed;
            }
        }
        foreach (EquipmentConditionGroupDefinition child in group.Groups ?? Array.Empty<EquipmentConditionGroupDefinition>())
        {
            sawAny = true;
            bool childPassed = ConditionGroupPasses(
                child,
                sourceUnit,
                targetUnit,
                factContext,
                activeBinding
            );
            if (anyMode)
            {
                passed = passed || childPassed;
            }
            else
            {
                passed = passed && childPassed;
            }
        }
        if (!sawAny)
            passed = true;
        return group.Negate ? !passed : passed;
    }

    private bool ConditionPasses(
        EquipmentAbilityConditionDefinition condition,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext,
        ActiveEquipmentAbilityBinding activeBinding
    )
    {
        if (condition == null)
            return false;
        if (
            condition.Kind == ConditionKindHasEquipmentTag
            && condition.PayloadDefinition is HasEquipmentTagConditionPayloadDefinition equipmentPayload
        )
        {
            return HasEquipmentTagConditionPasses(equipmentPayload, sourceUnit, targetUnit);
        }
        if (
            condition.Kind == ConditionKindCompareFact
            && condition.PayloadDefinition is CompareFactConditionPayloadDefinition comparePayload
        )
        {
            return CompareFactConditionPasses(
                comparePayload,
                sourceUnit,
                targetUnit,
                factContext,
                activeBinding
            );
        }
        return false;
    }

    private bool HasEquipmentTagConditionPasses(
        HasEquipmentTagConditionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        if (payload == null)
            return false;
        BattleUnitState subject = ResolveSubject(payload.Subject, sourceUnit, targetUnit);
        if (subject == null)
            return false;
        StringName selector = ProgressionDataUtils.to_string_name(payload.EquipmentSelector);
        if (selector == "")
            return false;
        StringName itemId = ProgressionDataUtils.to_string_name(
            subject.GetEquipmentView()?.GetEquippedItemId(selector) ?? ""
        );
        ItemDef itemDef = ResolveItemDef(itemId);
        if (itemDef == null)
            return false;
        bool hasAll = AllTagsPresent(itemDef, payload.AllTags);
        bool hasAny =
            payload.AnyTags == null
            || payload.AnyTags.Count == 0
            || AnyTagPresent(itemDef, payload.AnyTags);
        return hasAll && hasAny;
    }

    private bool CompareFactConditionPasses(
        CompareFactConditionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext,
        ActiveEquipmentAbilityBinding activeBinding
    )
    {
        if (payload == null)
            return false;
        if (payload.Compare == "contains")
        {
            IReadOnlyList<StringName> leftSet =
                ResolveFactStringNameSet(payload.Left, sourceUnit, targetUnit, factContext);
            StringName rightValue = ResolveFactStringName(
                payload.Right,
                sourceUnit,
                targetUnit,
                factContext
            );
            if (rightValue == "" || leftSet == null)
                return false;
            foreach (StringName value in leftSet)
                if (value == rightValue)
                    return true;
            return false;
        }
        if (
            TryResolveFactInt(payload.Left, sourceUnit, targetUnit, factContext, activeBinding, out int leftInt)
            && TryResolveFactInt(payload.Right, sourceUnit, targetUnit, factContext, activeBinding, out int rightInt)
        )
        {
            return CompareInt(leftInt, payload.Compare, rightInt);
        }
        if (payload.Compare == "eq")
        {
            StringName leftValue = ResolveFactStringName(
                payload.Left,
                sourceUnit,
                targetUnit,
                factContext
            );
            StringName rightValue = ResolveFactStringName(
                payload.Right,
                sourceUnit,
                targetUnit,
                factContext
            );
            return leftValue != "" && rightValue != "" && leftValue == rightValue;
        }
        return false;
    }

    private bool TryResolveFactInt(
        EquipmentAbilityFactQueryDefinition query,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext,
        ActiveEquipmentAbilityBinding activeBinding,
        out int value
    )
    {
        value = 0;
        if (query == null)
            return false;
        if (query.QueryKind == QueryKindLiteral)
        {
            value = query.IntLiteral;
            return true;
        }
        if (query.QueryKind != QueryKindFact)
            return false;
        if (query.FactId == FactCriticalHit)
        {
            value = factContext.CriticalHit ? 1 : 0;
            return true;
        }
        if (query.FactId == FactCurrentTu)
        {
            value = factContext.CurrentTu >= 0
                ? factContext.CurrentTu
                : Math.Max(_runtime?.GetState()?.timeline?.current_tu ?? -1, -1);
            return value >= 0;
        }
        if (query.FactId == FactHpDamage)
        {
            value = Math.Max(factContext.HpDamage, 0);
            return true;
        }
        if (query.FactId == FactSkillDamagedTargetCount)
        {
            value = Math.Max(factContext.SkillDamagedTargetCount, 0);
            return true;
        }
        if (query.FactId == FactSkillKilledTargetCount)
        {
            value = Math.Max(factContext.SkillKilledTargetCount, 0);
            return true;
        }
        if (query.FactId == FactSkillHpDamageDealt)
        {
            value = Math.Max(factContext.SkillHpDamageDealt, 0);
            return true;
        }
        if (query.FactId == FactBodySize)
        {
            BattleUnitState bodySizeSubject = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            if (bodySizeSubject == null)
                return false;
            value = Math.Max(bodySizeSubject.body_size, 0);
            return true;
        }
        if (query.FactId == FactAttributeValue)
        {
            BattleUnitState attributeSubject = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            StringName attributeId = ProgressionDataUtils.to_string_name(query.AttributeId);
            if (attributeSubject?.attribute_snapshot == null || attributeId == "")
                return false;
            value = attributeSubject.attribute_snapshot.GetValue(attributeId);
            return true;
        }
        if (query.FactId == FactEquipmentAbilityState)
        {
            BattleUnitState owner = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            EquipmentAbilityBindingDefinition stateBinding = ResolveStateBinding(
                activeBinding,
                activeBinding.Binding,
                query.BindingId
            );
            StringName chargeKey = BuildBindingStateChargeKey(
                activeBinding.Source,
                stateBinding,
                query.StateKey
            );
            if (owner == null || chargeKey == "")
                return false;
            value = GetAbilityStateValue(owner, stateBinding, chargeKey, query.StateKey, 0);
            return true;
        }
        if (
            query.FactId == FactEquipmentTargetMarkMatches
            || query.FactId == FactEquipmentTargetMarkStacks
        )
        {
            if (
                !TryResolveEquipmentTargetMark(
                    query,
                    sourceUnit,
                    targetUnit,
                    factContext,
                    activeBinding,
                    out BattleEquipmentTargetMarkState mark
                )
            )
            {
                return false;
            }
            BattleUnitState markSubject = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            bool subjectMatches = markSubject != null && mark.TargetUnitId == markSubject.unit_id;
            if (query.FactId == FactEquipmentTargetMarkMatches)
            {
                value = subjectMatches ? 1 : 0;
                return true;
            }
            value = markSubject == null || subjectMatches ? Math.Max(mark.Stacks, 0) : 0;
            return true;
        }
        if (query.FactId == FactStatusStacks)
        {
            BattleUnitState statusSubject = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            StringName statusId = ProgressionDataUtils.to_string_name(query.StatusId);
            if (statusSubject == null || statusId == "")
                return false;
            value = Math.Max(statusSubject.GetStatusEffect(statusId)?.stacks ?? 0, 0);
            return true;
        }
        if (query.FactId == FactNearbyEnemyCount || query.FactId == FactNearbyUnitCount)
        {
            BattleUnitState nearbySubject = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            if (nearbySubject == null)
                return false;
            BattleState state =
                factContext.BattleState
                ?? _runtime?.GetState();
            if (state == null)
                return false;
            int radius = Math.Max(query.IntLiteral, 0);
            value =
                query.FactId == FactNearbyUnitCount
                    ? CountNearbyLivingUnits(state, nearbySubject, radius)
                    : CountNearbyLivingEnemies(state, nearbySubject, radius);
            return true;
        }
        if (query.FactId == FactSummonedUnitCount)
        {
            BattleUnitState summonOwner = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            if (summonOwner == null)
                return false;
            BattleState state =
                factContext.BattleState
                ?? _runtime?.GetState();
            if (state == null)
                return false;
            EquipmentAbilityBindingDefinition summonBinding = ResolveStateBinding(
                activeBinding,
                activeBinding.Binding,
                query.BindingId
            );
            BattleUnitState radiusSubject = query.IntLiteral > 0
                ? ResolveSubject(query.ValueKind, sourceUnit, targetUnit)
                : null;
            value = CountLivingSummonedUnits(
                state,
                summonOwner,
                activeBinding.Source,
                summonBinding?.BindingId ?? query.BindingId,
                query.StateKey,
                radiusSubject,
                query.IntLiteral > 0 ? query.IntLiteral : -1
            );
            return true;
        }
        if (query.FactId != FactHpPercentBp)
            return false;
        BattleUnitState subject = ResolveSubject(query.Subject, sourceUnit, targetUnit);
        if (subject == null)
            return false;
        int maxHp = Math.Max(subject.attribute_snapshot?.GetValue(AttributeService.HP_MAX) ?? 0, 1);
        value = Math.Clamp(subject.current_hp * 10000 / maxHp, 0, 10000);
        return true;
    }

    private bool TryResolveEquipmentTargetMark(
        EquipmentAbilityFactQueryDefinition query,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext,
        ActiveEquipmentAbilityBinding activeBinding,
        out BattleEquipmentTargetMarkState mark
    )
    {
        mark = null;
        if (query == null || sourceUnit == null)
            return false;
        BattleState state = factContext.BattleState ?? _runtime?.GetState();
        if (state == null)
            return false;
        EquipmentAbilityBindingDefinition stateBinding = ResolveStateBinding(
            activeBinding,
            activeBinding.Binding,
            query.BindingId
        );
        StringName stateKey = ProgressionDataUtils.to_string_name(query.StateKey);
        if (stateBinding == null || stateBinding.BindingId == "" || stateKey == "")
            return false;
        if (!state.TryGetEquipmentTargetMark(
            sourceUnit.unit_id,
            activeBinding.Source?.SourceEquipmentInstanceId ?? "",
            stateBinding.BindingId,
            stateKey,
            out mark
        ))
        {
            return false;
        }
        if (ClearStaleEquipmentTargetMarkIfNeeded(state, stateBinding, mark))
        {
            mark = null;
            return false;
        }
        return true;
    }

    internal IReadOnlyList<StringName> ClearTargetMarksForDefeatedUnit(
        BattleState state,
        BattleUnitState defeatedUnit
    )
    {
        var changedUnitIds = new List<StringName>();
        if (state == null || defeatedUnit == null || defeatedUnit.unit_id == "")
            return changedUnitIds;

        foreach (BattleEquipmentTargetMarkState mark in state.GetEquipmentTargetMarksTyped())
        {
            if (mark?.IsValid != true)
                continue;
            EquipmentAbilityBindingDefinition binding = ResolveBindingForTargetMark(mark);
            bool targetWasDefeated =
                mark.TargetUnitId == defeatedUnit.unit_id
                && ShouldRemoveTargetMarkOnTargetDefeated(binding, mark);
            bool sourceWasDefeated =
                mark.SourceUnitId == defeatedUnit.unit_id && mark.RemoveOnSourceMissing;
            if (!targetWasDefeated && !sourceWasDefeated)
                continue;

            BattleUnitState targetUnit = targetWasDefeated
                ? defeatedUnit
                : state.GetUnit(mark.TargetUnitId);
            ClearMarkedTargetStatuses(targetUnit, mark, binding);
            if (
                state.RemoveEquipmentTargetMark(
                    mark.SourceUnitId,
                    mark.SourceEquipmentInstanceId,
                    mark.BindingId,
                    mark.StateKey
                )
            )
            {
                AddUniqueUnitId(changedUnitIds, mark.SourceUnitId);
                AddUniqueUnitId(changedUnitIds, mark.TargetUnitId);
            }
        }
        return changedUnitIds;
    }

    private static bool ClearStaleEquipmentTargetMarkIfNeeded(
        BattleState state,
        EquipmentAbilityBindingDefinition binding,
        BattleEquipmentTargetMarkState mark
    )
    {
        if (state == null || binding == null || mark?.IsValid != true)
            return false;
        BattleUnitState sourceUnit = state.GetUnit(mark.SourceUnitId);
        BattleUnitState targetUnit = state.GetUnit(mark.TargetUnitId);
        bool sourceMissing = !IsLivingUnit(sourceUnit);
        bool targetMissing =
            !IsLivingUnit(targetUnit)
            && ShouldRemoveTargetMarkOnTargetDefeated(binding, mark);
        if (!targetMissing && !(mark.RemoveOnSourceMissing && sourceMissing))
            return false;

        ClearMarkedTargetStatuses(targetUnit, mark, binding);
        state.RemoveEquipmentTargetMark(
            mark.SourceUnitId,
            mark.SourceEquipmentInstanceId,
            mark.BindingId,
            mark.StateKey
        );
        return true;
    }

    private EquipmentAbilityBindingDefinition ResolveBindingForTargetMark(
        BattleEquipmentTargetMarkState mark
    )
    {
        if (mark == null || mark.BindingId == "")
            return null;
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex =
            _runtime?.GetEquipmentAbilityBindingIndexTyped();
        return bindingIndex != null
            && bindingIndex.TryGetValue(mark.BindingId, out EquipmentAbilityBindingDefinition binding)
            ? binding
            : null;
    }

    private static bool IsLivingUnit(BattleUnitState unit) =>
        unit != null && unit.is_alive && unit.current_hp > 0;

    private static bool ShouldRemoveTargetMarkOnTargetDefeated(
        EquipmentAbilityBindingDefinition binding,
        BattleEquipmentTargetMarkState mark
    )
    {
        if (binding == null || mark?.IsValid != true)
            return false;
        StringName stateKey = ProgressionDataUtils.to_string_name(mark.StateKey);
        foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (action?.PayloadDefinition is not MarkTargetActionPayloadDefinition payload)
                    continue;
                if (ProgressionDataUtils.to_string_name(payload.StateKey) != stateKey)
                    continue;
                if (payload.RemoveOnTargetDefeated)
                    return true;
            }
        }
        return false;
    }

    private static void ClearMarkedTargetStatuses(
        BattleUnitState targetUnit,
        BattleEquipmentTargetMarkState mark,
        EquipmentAbilityBindingDefinition binding
    )
    {
        if (targetUnit == null || mark == null || binding == null)
            return;
        foreach (StringName statusId in BuildTargetMarkClearStatusIds(binding, mark.StateKey))
        {
            if (statusId == "")
                continue;
            BattleStatusEffectState status = targetUnit.GetStatusEffect(statusId);
            if (
                status == null
                || ProgressionDataUtils.to_string_name(status.source_unit_id) != mark.SourceUnitId
            )
            {
                continue;
            }
            targetUnit.EraseStatusEffect(statusId);
        }
    }

    private static IReadOnlyList<StringName> BuildTargetMarkClearStatusIds(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        var result = new List<StringName>();
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (action?.PayloadDefinition is not MarkTargetActionPayloadDefinition payload)
                    continue;
                if (ProgressionDataUtils.to_string_name(payload.StateKey) != stateKey)
                    continue;
                AddUniqueStatusId(result, payload.MirrorStatusId);
                foreach (StringName statusId in payload.ClearStatusIdsOnReplace ?? Array.Empty<StringName>())
                    AddUniqueStatusId(result, statusId);
            }
        }
        return result;
    }

    private static void AddUniqueUnitId(List<StringName> result, StringName unitId)
    {
        if (unitId == "" || result.Contains(unitId))
            return;
        result.Add(unitId);
    }

    private static int CountNearbyLivingEnemies(
        BattleState state,
        BattleUnitState sourceUnit,
        int radius
    )
    {
        if (state == null || sourceUnit == null || radius < 0)
            return 0;
        int count = 0;
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate == null
                || !candidate.is_alive
                || candidate.unit_id == sourceUnit.unit_id
                || candidate.faction_id == sourceUnit.faction_id
            )
            {
                continue;
            }
            if (BattleGridDistanceService.GetDistanceBetweenUnits(sourceUnit, candidate) <= radius)
                count++;
        }
        return count;
    }

    private static int CountNearbyLivingUnits(
        BattleState state,
        BattleUnitState sourceUnit,
        int radius
    )
    {
        if (state == null || sourceUnit == null || radius < 0)
            return 0;
        int count = 0;
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate == null
                || !candidate.is_alive
                || candidate.unit_id == sourceUnit.unit_id
            )
            {
                continue;
            }
            if (BattleGridDistanceService.GetDistanceBetweenUnits(sourceUnit, candidate) <= radius)
                count++;
        }
        return count;
    }

    private IReadOnlyList<StringName> ResolveFactStringNameSet(
        EquipmentAbilityFactQueryDefinition query,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext
    )
    {
        if (query == null || query.QueryKind != QueryKindFact)
            return Array.Empty<StringName>();
        if (query.FactId == FactCreatureTypeTags)
        {
            BattleUnitState subject = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            return subject != null
                ? subject.creature_type_tags
                : Array.Empty<StringName>();
        }
        if (query.FactId == FactBattleEnvironmentTag)
        {
            return factContext.BattleState?.GetEnvironmentSnapshot()?.GlobalEnvironmentTags
                ?? _runtime?.GetState()?.GetEnvironmentSnapshot()?.GlobalEnvironmentTags
                ?? Array.Empty<StringName>();
        }
        return Array.Empty<StringName>();
    }

    private StringName ResolveFactStringName(
        EquipmentAbilityFactQueryDefinition query,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext
    )
    {
        if (query == null)
            return "";
        if (query.QueryKind == QueryKindLiteral)
            return ProgressionDataUtils.to_string_name(query.StringNameLiteral);
        IReadOnlyList<StringName> values = ResolveFactStringNameSet(
            query,
            sourceUnit,
            targetUnit,
            factContext
        );
        return values.Count > 0 ? values[0] : "";
    }

    private static BattleUnitState ResolveSubject(
        StringName subject,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        if (subject == "source" || subject == "attacker" || subject == "owner")
            return sourceUnit;
        if (
            subject == "target"
            || subject == "attack_target"
            || subject == "defender"
            || subject == "defeated"
            || subject == "victim"
            || subject == "skill_target"
            || subject == "selected_target"
        )
            return targetUnit;
        return null;
    }

    private static BattleUnitState ResolveContextAttacker(BattleAttackCheckPolicyContext context)
    {
        if (context == null)
            return null;
        return context.attacker ?? context.attacker_view.UnsafeUnitForReadOnlyRules;
    }

    private static BattleUnitState ResolveContextTarget(BattleAttackCheckPolicyContext context)
    {
        if (context == null)
            return null;
        return context.target ?? context.target_view.UnsafeUnitForReadOnlyRules;
    }

    private IEnumerable<ActiveEquipmentAbilityBinding> CollectActiveBindings(BattleUnitState sourceUnit)
    {
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex =
            _runtime?.GetEquipmentAbilityBindingIndexTyped();
        if (
            sourceUnit?.equipment_ability_sources == null
            || bindingIndex == null
            || bindingIndex.Count == 0
        )
        {
            yield break;
        }
        var result = new List<ActiveEquipmentAbilityBinding>();
        foreach (BattleEquipmentAbilitySourceState source in sourceUnit.equipment_ability_sources)
        {
            if (source?.AbilityIds == null)
                continue;
            foreach (StringName abilityId in source.AbilityIds)
            {
                StringName normalizedAbilityId = ProgressionDataUtils.to_string_name(abilityId);
                if (
                    normalizedAbilityId != ""
                    && bindingIndex.TryGetValue(
                        normalizedAbilityId,
                        out EquipmentAbilityBindingDefinition binding
                    )
                    && binding != null
                )
                {
                    result.Add(new ActiveEquipmentAbilityBinding(source, binding));
                }
            }
        }
        result.Sort(CompareActiveBindings);
        foreach (ActiveEquipmentAbilityBinding activeBinding in result)
            yield return activeBinding;
    }

    private static int CompareActiveBindings(
        ActiveEquipmentAbilityBinding left,
        ActiveEquipmentAbilityBinding right
    )
    {
        int priorityCompare = BindingPriority(left.Binding).CompareTo(BindingPriority(right.Binding));
        if (priorityCompare != 0)
            return priorityCompare;
        return string.CompareOrdinal(
            left.Binding?.BindingId.ToString() ?? "",
            right.Binding?.BindingId.ToString() ?? ""
        );
    }

    private static int BindingPriority(EquipmentAbilityBindingDefinition binding)
    {
        int priority = 0;
        bool sawReaction = false;
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            if (reaction == null)
                continue;
            if (!sawReaction || reaction.Priority < priority)
            {
                priority = reaction.Priority;
                sawReaction = true;
            }
        }
        return priority;
    }

    private ItemDef ResolveItemDef(StringName itemId)
    {
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = _runtime?.GetItemDefIndexTyped();
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        return normalizedItemId != ""
            && itemDefs != null
            && itemDefs.TryGetValue(normalizedItemId, out ItemDef itemDef)
            ? itemDef
            : null;
    }

    private static bool AllTagsPresent(ItemDef itemDef, IReadOnlyList<StringName> requiredTags)
    {
        if (requiredTags == null || requiredTags.Count == 0)
            return true;
        foreach (StringName requiredTag in requiredTags)
            if (!BattleEquipmentRequirementRules.ItemHasTag(itemDef, requiredTag))
                return false;
        return true;
    }

    private static bool AnyTagPresent(ItemDef itemDef, IReadOnlyList<StringName> requiredTags)
    {
        if (requiredTags == null || requiredTags.Count == 0)
            return true;
        foreach (StringName requiredTag in requiredTags)
            if (BattleEquipmentRequirementRules.ItemHasTag(itemDef, requiredTag))
                return true;
        return false;
    }

    private readonly record struct ActiveEquipmentAbilityBinding(
        BattleEquipmentAbilitySourceState Source,
        EquipmentAbilityBindingDefinition Binding
    );
}
