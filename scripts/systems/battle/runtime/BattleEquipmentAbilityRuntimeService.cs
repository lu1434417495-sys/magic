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
    public BattleSaveContext SaveContext { get; init; } = BattleSaveContext.Empty;
}

internal sealed class BattleEquipmentAbilityGrantedSkillUsedContext
{
    public BattleUnitState SourceUnit { get; init; }
    public StringName BindingId { get; init; } = "";
    public StringName GrantedActionId { get; init; } = "";
    public StringName SkillId { get; init; } = "";
    public StringName SkillEntryId { get; init; } = "";
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
}

internal readonly struct EquipmentAbilityFactContext
{
    internal readonly bool CriticalHit;
    internal readonly int CurrentTu;
    internal readonly BattleState BattleState;
    internal readonly int HpDamage;

    private EquipmentAbilityFactContext(
        bool criticalHit,
        int currentTu,
        BattleState battleState,
        int hpDamage = 0
    )
    {
        CriticalHit = criticalHit;
        CurrentTu = currentTu;
        BattleState = battleState;
        HpDamage = Math.Max(hpDamage, 0);
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
    public StringName DamageType { get; init; } = "";
    public IReadOnlyList<StringName> DamageTags { get; init; } = Array.Empty<StringName>();
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

    public bool Resolved => _lootMultipliers.Count > 0 || _statusResults.Count > 0;
    public IReadOnlyList<BattleEquipmentAbilityLootQuantityMultiplierResult> LootMultipliers =>
        _lootMultipliers;
    public IReadOnlyList<BattleEquipmentAbilityStatusActionResult> StatusResults =>
        _statusResults;

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
}

internal sealed class BattleEquipmentAbilityRuntimeService
{
    private static readonly StringName ActionKindAddDamageDice = "add_damage_dice";
    private static readonly StringName ActionKindAttackRollBonus = "attack_roll_bonus";
    private static readonly StringName ActionKindAttackRollAdvantage = "attack_roll_advantage";
    private static readonly StringName ActionKindDamageRollModeOverride =
        "damage_roll_mode_override";
    private static readonly StringName ActionKindApplyStatus = "apply_status";
    private static readonly StringName ActionKindModifyAbilityState = "modify_ability_state";
    private static readonly StringName ActionKindScheduleAreaEffect = "schedule_area_effect";
    private static readonly StringName ActionKindEquipmentDurabilityDamage =
        "equipment_durability_damage";
    private static readonly StringName ActionKindLootQuantityMultiplier =
        "loot_quantity_multiplier";
    private static readonly StringName ConditionKindCompareFact = "compare_fact";
    private static readonly StringName ConditionKindHasEquipmentTag = "has_equipment_tag";
    private static readonly StringName FactCreatureTypeTags = "creature_type_tags";
    private static readonly StringName FactBattleEnvironmentTag = "battle_environment_tag";
    private static readonly StringName FactHpPercentBp = "hp_percent_bp";
    private static readonly StringName FactCriticalHit = "critical_hit";
    private static readonly StringName FactHpDamage = "hp_damage";
    private static readonly StringName FactCurrentTu = "current_tu";
    private static readonly StringName FactEquipmentAbilityState = "equipment_ability_state";
    private static readonly StringName FactStatusStacks = "status_stacks";
    private static readonly StringName FactNearbyEnemyCount = "nearby_enemy_count";
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
        if (context == null || context.attacker == null || context.target == null)
            return result;

        foreach (
            ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(context.attacker)
        )
        {
            CollectAttackRollBonusActions(
                context,
                activeBinding,
                context.attacker,
                context.target,
                defensiveSource: false,
                result
            );
            CollectAttackRollAdvantageActions(
                context,
                activeBinding,
                context.attacker,
                context.target,
                defensiveSource: false,
                result
            );
        }
        foreach (
            ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(context.target)
        )
        {
            CollectAttackRollBonusActions(
                context,
                activeBinding,
                context.target,
                context.attacker,
                defensiveSource: true,
                result
            );
        }
        return result;
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
        if (context == null || context.SourceUnit == null)
            return false;

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
                        null,
                        EquipmentAbilityFactContext.FromBattleState(_runtime?.GetState()),
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
                        || !ConditionGroupPasses(
                            action.ConditionGroup,
                            context.SourceUnit,
                            null,
                            EquipmentAbilityFactContext.FromBattleState(_runtime?.GetState()),
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
                            null
                        );
                        changed = true;
                    }
                }
            }
        }
        return changed;
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
                        context.DefeatedUnit
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
                ResolveOnKillActions(binding, reaction, context, result);
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
                    context.DefeatedUnit
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
        }
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
            countsAsDebuffOverride: payload.CountsAsDebuffOverride,
            countsAsDebuff: payload.CountsAsDebuff,
            undispellable: payload.Undispellable,
            dispellableMagic: payload.DispellableMagic,
            dispellableHarmfulMagic: payload.DispellableHarmfulMagic,
            dispellableBeneficialMagic: payload.DispellableBeneficialMagic
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
        targetUnit.SetStatusEffect(statusEntry);
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
                DamageType = payload.DamageType,
                DamageTags = CopyStringNames(payload.DamageTags),
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
                DamageType = payload.DamageType,
                DamageTags = CopyStringNames(payload.DamageTags),
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
        if (query.FactId == FactStatusStacks)
        {
            BattleUnitState statusSubject = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            StringName statusId = ProgressionDataUtils.to_string_name(query.StatusId);
            if (statusSubject == null || statusId == "")
                return false;
            value = Math.Max(statusSubject.GetStatusEffect(statusId)?.stacks ?? 0, 0);
            return true;
        }
        if (query.FactId == FactNearbyEnemyCount)
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
            value = CountNearbyLivingEnemies(state, nearbySubject, radius);
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
        )
            return targetUnit;
        return null;
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
