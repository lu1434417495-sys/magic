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
    public int WeaponHpDamage { get; init; }
    public BattleEventBatch Batch { get; init; }
    public BattleSaveContext SaveContext { get; init; } = BattleSaveContext.Empty;
}

internal sealed class BattleEquipmentAbilityAttackCheckContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public bool AttackSucceeded { get; init; }
    public bool CriticalHit { get; init; }
    public StringName SkillId { get; init; } = "";
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
    public BattleKillProvenance KillProvenance { get; init; } = BattleKillProvenance.None;
}

internal sealed class BattleEquipmentAbilityTargetMarkExpiredContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public BattleEventBatch Batch { get; init; }
    public BattleEquipmentTargetMarkState Mark { get; init; }
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
    public int MovedTargetCount { get; init; }
    public int UnmovedTargetCount { get; init; }
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

internal sealed class BattleEquipmentAbilityDamageReductionContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public StringName DamageTag { get; init; } = "";
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
    internal readonly int SkillMovedTargetCount;
    internal readonly int SkillUnmovedTargetCount;
    internal readonly BattleKillProvenance KillProvenance;
    internal readonly BattleEquipmentTargetMarkState ExpiredTargetMark;

    private EquipmentAbilityFactContext(
        bool criticalHit,
        int currentTu,
        BattleState battleState,
        int hpDamage = 0,
        int skillDamagedTargetCount = 0,
        int skillKilledTargetCount = 0,
        int skillHpDamageDealt = 0,
        int skillMovedTargetCount = 0,
        int skillUnmovedTargetCount = 0,
        BattleKillProvenance killProvenance = default,
        BattleEquipmentTargetMarkState expiredTargetMark = null
    )
    {
        CriticalHit = criticalHit;
        CurrentTu = currentTu;
        BattleState = battleState;
        HpDamage = Math.Max(hpDamage, 0);
        SkillDamagedTargetCount = Math.Max(skillDamagedTargetCount, 0);
        SkillKilledTargetCount = Math.Max(skillKilledTargetCount, 0);
        SkillHpDamageDealt = Math.Max(skillHpDamageDealt, 0);
        SkillMovedTargetCount = Math.Max(skillMovedTargetCount, 0);
        SkillUnmovedTargetCount = Math.Max(skillUnmovedTargetCount, 0);
        KillProvenance = killProvenance;
        ExpiredTargetMark = expiredTargetMark;
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

    internal static EquipmentAbilityFactContext FromDamageReduction(
        BattleEquipmentAbilityDamageReductionContext context
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
        context?.BattleState,
        context?.WeaponHpDamage ?? 0
    );

    internal static EquipmentAbilityFactContext FromAttackCheck(
        BattleEquipmentAbilityAttackCheckContext context
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
            skillHpDamageDealt: outcome.HpDamageDealt,
            skillMovedTargetCount: outcome.MovedTargetCount,
            skillUnmovedTargetCount: outcome.UnmovedTargetCount
        );
    }

    internal static EquipmentAbilityFactContext FromOnKill(
        BattleEquipmentAbilityOnKillContext context
    ) => new(
        false,
        Math.Max(context?.BattleState?.timeline?.current_tu ?? -1, -1),
        context?.BattleState,
        killProvenance: context?.KillProvenance ?? BattleKillProvenance.None
    );

    internal static EquipmentAbilityFactContext FromTargetMarkExpired(
        BattleEquipmentAbilityTargetMarkExpiredContext context
    ) => new(
        false,
        Math.Max(context?.BattleState?.timeline?.current_tu ?? -1, -1),
        context?.BattleState,
        expiredTargetMark: context?.Mark
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
    public bool Subtract { get; init; }
    public StringName DamageType { get; init; } = "";
    public IReadOnlyList<StringName> DamageTags { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<StringName> MitigationBypassDamageTags { get; init; } =
        Array.Empty<StringName>();
    public IReadOnlyList<StringName> MitigationBypassTiers { get; init; } =
        Array.Empty<StringName>();
}

internal sealed class BattleEquipmentAbilityDamageReductionResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public int Amount { get; init; }
    public string Label { get; init; } = "";
}

internal sealed class BattleEquipmentAbilityCriticalHitOverrideResult
{
    internal static readonly BattleEquipmentAbilityCriticalHitOverrideResult None = new();

    public bool ForceCriticalOnHit { get; init; }
    public StringName SourceEquipmentInstanceId { get; init; } = "";
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
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
    private readonly List<BattleEquipmentAbilityTriggeredSkillResult> _triggeredSkillResults = new();

    public bool Resolved =>
        _rolls.Count > 0
        || _durabilityResults.Count > 0
        || _bonusDamageDice.Count > 0
        || _statusResults.Count > 0
        || _triggeredSkillResults.Count > 0;

    public IReadOnlyList<BattleEquipmentAbilityRollResult> Rolls => _rolls;
    public IReadOnlyList<BattleEquipmentAbilityDurabilityResult> DurabilityResults =>
        _durabilityResults;
    public IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> BonusDamageDice =>
        _bonusDamageDice;
    public IReadOnlyList<BattleEquipmentAbilityStatusActionResult> StatusResults =>
        _statusResults;
    public IReadOnlyList<BattleEquipmentAbilityTriggeredSkillResult> TriggeredSkillResults =>
        _triggeredSkillResults;

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

    internal void AddTriggeredSkillResult(BattleEquipmentAbilityTriggeredSkillResult result)
    {
        if (result != null)
            _triggeredSkillResults.Add(result);
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
    private readonly List<BattleEquipmentAbilityTriggeredSkillResult> _triggeredSkillResults = new();

    public bool Resolved =>
        _lootMultipliers.Count > 0
        || _statusResults.Count > 0
        || _summonResults.Count > 0
        || _immediateWeaponAttackResults.Count > 0
        || _triggeredSkillResults.Count > 0;
    public IReadOnlyList<BattleEquipmentAbilityLootQuantityMultiplierResult> LootMultipliers =>
        _lootMultipliers;
    public IReadOnlyList<BattleEquipmentAbilityStatusActionResult> StatusResults =>
        _statusResults;
    public IReadOnlyList<BattleEquipmentAbilitySummonResult> SummonResults => _summonResults;
    public IReadOnlyList<BattleEquipmentAbilityImmediateWeaponAttackResult>
        ImmediateWeaponAttackResults => _immediateWeaponAttackResults;
    public IReadOnlyList<BattleEquipmentAbilityTriggeredSkillResult> TriggeredSkillResults =>
        _triggeredSkillResults;

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

    internal void AddTriggeredSkillResult(BattleEquipmentAbilityTriggeredSkillResult result)
    {
        if (result != null)
            _triggeredSkillResults.Add(result);
    }
}

internal sealed class BattleEquipmentAbilityTriggeredSkillResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public StringName TargetUnitId { get; init; } = "";
    public bool MergeIntoParentResult { get; init; }
    public AttackEffectResolutionResult Resolution { get; init; }
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
    private static readonly StringName ActionKindHealFromFact = "heal_from_fact";
    private static readonly StringName ActionKindAttackRollBonus = "attack_roll_bonus";
    private static readonly StringName ActionKindAttackRollAdvantage = "attack_roll_advantage";
    private static readonly StringName ActionKindCriticalHitOverride = "critical_hit_override";
    private static readonly StringName ActionKindAttackDefenseModifier =
        "attack_defense_modifier";
    private static readonly StringName ActionKindDamageRollModeOverride =
        "damage_roll_mode_override";
    private static readonly StringName ActionKindDamageReduction =
        "damage_reduction";
    private static readonly StringName ActionKindApplyStatus = "apply_status";
    private static readonly StringName ActionKindModifyActionPoints = "modify_action_points";
    private static readonly StringName ActionKindModifyAbilityState = "modify_ability_state";
    private static readonly StringName ActionKindMarkTarget = "mark_target";
    private static readonly StringName ActionKindClearStatus = "clear_status";
    private static readonly StringName ActionKindTriggerSkill = "trigger_skill";
    private static readonly StringName ActionKindScheduleAreaEffect = "schedule_area_effect";
    private static readonly StringName ActionKindApplyBattleTerrainEffectAfterCheck =
        "apply_battle_terrain_effect_after_check";
    private static readonly StringName ActionKindApplyEdgeFeature = "apply_edge_feature";
    private static readonly StringName ActionKindEquipmentDurabilityDamage =
        "equipment_durability_damage";
    private static readonly StringName ActionKindLootQuantityMultiplier =
        "loot_quantity_multiplier";
    private static readonly StringName ActionKindSummonUnits = "summon_units";
    private static readonly StringName ActionKindConsumeSummonedUnits =
        "consume_summoned_units";
    private static readonly StringName ActionKindConsumeStatusStacks =
        "consume_status_stacks";
    private static readonly StringName ActionKindSummonedUnitAttackRollModifier =
        "summoned_unit_attack_roll_modifier";
    private static readonly StringName ConditionKindCompareFact = "compare_fact";
    private static readonly StringName ConditionKindHasStatus = "has_status";
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
    private static readonly StringName FactSkillMovedTargetCount =
        "skill_moved_target_count";
    private static readonly StringName FactSkillUnmovedTargetCount =
        "skill_unmoved_target_count";
    private static readonly StringName FactBodySize = "body_size";
    private static readonly StringName FactAttributeValue = "attribute_value";
    private static readonly StringName FactCurrentTu = "current_tu";
    private static readonly StringName FactCurrentActionPoints = "current_action_points";
    private static readonly StringName FactEquipmentAbilityState = "equipment_ability_state";
    private static readonly StringName FactKillSourceIsAttack = "kill_source_is_attack";
    private static readonly StringName FactKillSourceEquipmentInstanceMatches =
        "kill_source_equipment_instance_matches";
    private static readonly StringName FactKillSourceBindingMatches =
        "kill_source_binding_matches";
    private static readonly StringName FactEquipmentTargetMarkMatches =
        "equipment_target_mark_matches";
    private static readonly StringName FactEquipmentTargetMarkStacks =
        "equipment_target_mark_stacks";
    private static readonly StringName FactExpiredTargetMarkMatches =
        "expired_target_mark_matches";
    private static readonly StringName FactStatusStacks = "status_stacks";
    private static readonly StringName FactNearbyEnemyCount = "nearby_enemy_count";
    private static readonly StringName FactNearbyUnitCount = "nearby_unit_count";
    private static readonly StringName FactNearbyAllyCount = "nearby_ally_count";
    private static readonly StringName FactSummonedUnitCount = "summoned_unit_count";
    private static readonly StringName FactSourceStatusTotalStacks =
        "source_status_total_stacks";
    private static readonly StringName FactUnitDistance = "unit_distance";
    private static readonly StringName FactWeaponRangeType = "weapon_range_type";
    private static readonly StringName DropKindItem = "item";
    private static readonly StringName StackModeMax = "max";
    private static readonly StringName QueryKindFact = "fact";
    private static readonly StringName QueryKindLiteral = "literal";
    private static readonly StringName OnceScopeTurn = "turn";
    private static readonly StringName ResetTimingPerBattle = "per_battle";
    private static readonly StringName ResetTimingBattle = "battle";
    private static readonly StringName ResetTimingPersistentCounter = "persistent_counter";

    private BattleRuntimeModule _runtime;
    private BattleDamageResolver _damageResolver;
    private readonly Queue<int> _forcedRollGateValuesForTests = new();
    private readonly Queue<int> _forcedAbilityCheckRollValuesForTests = new();

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
        _forcedAbilityCheckRollValuesForTests.Clear();
    }

    internal BattleState GetBattleState() => _runtime?.GetState();

    internal void ConfigureRollGateValuesForTests(IEnumerable<int> values)
    {
        _forcedRollGateValuesForTests.Clear();
        if (values == null)
            return;
        foreach (int value in values)
            _forcedRollGateValuesForTests.Enqueue(Math.Max(value, 1));
    }

    internal void ConfigureAbilityCheckRollValuesForTests(IEnumerable<int> values)
    {
        _forcedAbilityCheckRollValuesForTests.Clear();
        if (values == null)
            return;
        foreach (int value in values)
            _forcedAbilityCheckRollValuesForTests.Enqueue(Math.Clamp(value, 1, 20));
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

    internal BattleEquipmentAbilityCriticalHitOverrideResult ResolveCriticalHitOverride(
        BattleAttackCheckPolicyContext context
    )
    {
        BattleUnitState attacker = ResolveContextAttacker(context);
        BattleUnitState target = ResolveContextTarget(context);
        if (context == null || attacker == null || target == null || context.force_hit_no_crit)
            return BattleEquipmentAbilityCriticalHitOverrideResult.None;

        foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(attacker))
        {
            EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
            foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                    || reaction.Timing != EquipmentAbilityTimingKind.BeforeHit
                    || !ConditionGroupPasses(
                        reaction.ConditionGroup,
                        attacker,
                        target,
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
                        || action.Kind != ActionKindCriticalHitOverride
                        || action.PayloadDefinition is not CriticalHitOverrideActionPayloadDefinition payload
                        || (payload.RequireWeaponDamage && !ContextIncludesWeaponDamage(context))
                        || !AttackRollPayloadSelectorMatches(payload.TargetSelector, defensiveSource: false)
                        || !ConditionGroupPasses(
                            action.ConditionGroup,
                            attacker,
                            target,
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
                    return new BattleEquipmentAbilityCriticalHitOverrideResult
                    {
                        ForceCriticalOnHit = true,
                        SourceEquipmentInstanceId =
                            activeBinding.Source?.SourceEquipmentInstanceId ?? new StringName(""),
                        BindingId = binding.BindingId,
                        ActionId = action.ActionId,
                    };
                }
            }
        }
        return BattleEquipmentAbilityCriticalHitOverrideResult.None;
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
                    || (payload.RequireWeaponDamage && !ContextIncludesWeaponDamage(context))
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
                int modifierDelta = ResolveAttackRollBonusDelta(payload, sourceUnit);
                if (modifierDelta == 0)
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
                        modifier_delta = modifierDelta,
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

    private static int ResolveAttackRollBonusDelta(
        AttackRollBonusActionPayloadDefinition payload,
        BattleUnitState sourceUnit
    )
    {
        if (payload == null)
            return 0;
        int result = payload.Bonus;
        StringName attributeModifierId =
            ProgressionDataUtils.to_string_name(payload.AttributeModifierId);
        if (attributeModifierId != "" && sourceUnit?.attribute_snapshot != null)
        {
            result += sourceUnit.attribute_snapshot.GetValue(attributeModifierId);
        }
        return result;
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
        ItemDefinition itemDef = ResolveItemDef(itemId);
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

    private static bool ContextIncludesWeaponDamage(BattleAttackCheckPolicyContext context)
    {
        foreach (CombatEffectDefinition effect in context?.skill_definition?.CombatProfile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect != null && (effect.AddWeaponDice || effect.RequiresWeapon))
                return true;
        }
        return false;
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

    internal bool ResolveAttackCheck(BattleEquipmentAbilityAttackCheckContext context)
    {
        if (context == null || context.SourceUnit == null || context.TargetUnit == null)
        {
            return false;
        }

        bool changed = false;
        EquipmentAbilityFactContext factContext =
            EquipmentAbilityFactContext.FromAttackCheck(context);
        foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(context.SourceUnit))
        {
            EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
            if (binding?.Reactions == null)
                continue;
            foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnAttackCheck
                    || reaction.Timing != EquipmentAbilityTimingKind.AfterAttackCheck
                    || !ConditionGroupPasses(
                        reaction.ConditionGroup,
                        context.SourceUnit,
                        context.TargetUnit,
                        factContext,
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
                            factContext,
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
                        action.Kind == ActionKindConsumeStatusStacks
                        && action.PayloadDefinition is ConsumeStatusStacksActionPayloadDefinition consumeStacksPayload
                    )
                    {
                        List<StringName> consumedUnitIds = ResolveConsumeStatusStacksAction(
                            consumeStacksPayload,
                            context.SourceUnit,
                            context.TargetUnit,
                            context.BattleState
                        );
                        changed = (consumedUnitIds != null && consumedUnitIds.Count > 0) || changed;
                    }
                }
            }
        }
        return changed;
    }

    internal BattleEquipmentAbilityAfterHitResult ResolveHitReceived(
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
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnHitReceived
                    || reaction.Timing != EquipmentAbilityTimingKind.AfterHitReceived
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

    internal IReadOnlyList<BattleEquipmentAbilityDamageReductionResult> CollectDamageReductions(
        BattleEquipmentAbilityDamageReductionContext context
    )
    {
        var result = new List<BattleEquipmentAbilityDamageReductionResult>();
        StringName damageTag = ProgressionDataUtils.to_string_name(context?.DamageTag ?? new StringName(""));
        if (context == null || context.SourceUnit == null || context.TargetUnit == null || damageTag == "")
            return result;

        EquipmentAbilityFactContext factContext =
            EquipmentAbilityFactContext.FromDamageReduction(context);
        BattleUnitState holder = context.TargetUnit;
        BattleUnitState attacker = context.SourceUnit;
        foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(holder))
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
                        holder,
                        attacker,
                        factContext,
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
                        || action.Kind != ActionKindDamageReduction
                        || action.PayloadDefinition is not DamageReductionActionPayloadDefinition payload
                        || payload.Amount <= 0
                        || !DamageReductionPayloadSelectorMatches(payload.TargetSelector)
                        || !DamageReductionMatchesTag(payload, damageTag)
                        || !ConditionGroupPasses(
                            action.ConditionGroup,
                            holder,
                            attacker,
                            factContext,
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
                        new BattleEquipmentAbilityDamageReductionResult
                        {
                            BindingId = binding.BindingId,
                            ActionId = action.ActionId,
                            Amount = Math.Max(payload.Amount, 0),
                            Label = ResolveActionLabel(binding, payload.Label),
                        }
                    );
                }
            }
        }
        return result;
    }

    private static bool DamageReductionPayloadSelectorMatches(StringName targetSelector)
    {
        StringName selector = ProgressionDataUtils.to_string_name(targetSelector);
        return selector == ""
            || selector == "self"
            || selector == "holder"
            || selector == "defender"
            || selector == "damage_target";
    }

    private static bool DamageReductionMatchesTag(
        DamageReductionActionPayloadDefinition payload,
        StringName damageTag
    )
    {
        if (payload?.DamageTags == null || payload.DamageTags.Count == 0 || damageTag == "")
            return false;
        foreach (StringName value in payload.DamageTags)
        {
            if (ProgressionDataUtils.to_string_name(value) == damageTag)
                return true;
        }
        return false;
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
                    else if (
                        action.Kind == ActionKindConsumeStatusStacks
                        && action.PayloadDefinition is ConsumeStatusStacksActionPayloadDefinition consumeStacksPayload
                    )
                    {
                        List<StringName> consumedUnitIds = ResolveConsumeStatusStacksAction(
                            consumeStacksPayload,
                            context.SourceUnit,
                            null,
                            context.BattleState
                        );
                        changed = (consumedUnitIds != null && consumedUnitIds.Count > 0) || changed;
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
        EquipmentAbilityFactContext factContext =
            EquipmentAbilityFactContext.FromDamageApplied(context);
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
                        factContext,
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
                            factContext,
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
                        foreach (
                            BattleUnitState actionTarget in ResolveApplyStatusTargets(
                                statusPayload.TargetSelector,
                                context.SourceUnit,
                                context.TargetUnit,
                                context.BattleState
                            )
                        )
                        {
                            ResolveApplyStatusAction(
                                binding,
                                action,
                                statusPayload,
                                context.SourceUnit,
                                actionTarget,
                                context.SaveContext,
                                null
                            );
                        }
                        changed = true;
                    }
                    else if (
                        action.Kind == ActionKindHealFromFact
                        && action.PayloadDefinition is HealFromFactActionPayloadDefinition healFromFactPayload
                    )
                    {
                        BattleUnitState changedUnit = ResolveHealFromFactAction(
                            activeBinding,
                            binding,
                            healFromFactPayload,
                            context.SourceUnit,
                            context.TargetUnit,
                            context.BattleState,
                            factContext
                        );
                        changed = changedUnit != null || changed;
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
                            context.BattleState
                        );
                        changed = changedUnit != null || changed;
                    }
                    else if (
                        action.Kind == ActionKindConsumeStatusStacks
                        && action.PayloadDefinition is ConsumeStatusStacksActionPayloadDefinition consumeStacksPayload
                    )
                    {
                        List<StringName> consumedUnitIds = ResolveConsumeStatusStacksAction(
                            consumeStacksPayload,
                            context.SourceUnit,
                            context.TargetUnit,
                            context.BattleState
                        );
                        changed = (consumedUnitIds != null && consumedUnitIds.Count > 0) || changed;
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
                        action.Kind == ActionKindSummonUnits
                        && action.PayloadDefinition is SummonUnitsActionPayloadDefinition summonPayload
                    )
                    {
                        changed =
                            ResolveSummonUnitsAction(
                                activeBinding,
                                binding,
                                action,
                                summonPayload,
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
                        action.Kind == ActionKindModifyActionPoints
                        && action.PayloadDefinition is ModifyActionPointsActionPayloadDefinition actionPointsPayload
                    )
                    {
                        BattleUnitState changedUnit = ResolveModifyActionPointsAction(
                            activeBinding,
                            binding,
                            action,
                            actionPointsPayload,
                            context.SourceUnit,
                            context.TargetUnit
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
                        foreach (
                            BattleUnitState actionTarget in ResolveApplyStatusTargets(
                                statusPayload.TargetSelector,
                                context.SourceUnit,
                                context.TargetUnit,
                                battleState
                            )
                        )
                        {
                            ResolveApplyStatusAction(
                                binding,
                                action,
                                statusPayload,
                                context.SourceUnit,
                                actionTarget,
                                BattleSaveContext.Empty,
                                null
                            );
                            context.Batch?.AddChangedUnitId(actionTarget?.unit_id ?? "");
                        }
                        changed = true;
                    }
                    else if (
                        action.Kind == ActionKindConsumeStatusStacks
                        && action.PayloadDefinition is ConsumeStatusStacksActionPayloadDefinition consumeStacksPayload
                    )
                    {
                        List<StringName> consumedUnitIds = ResolveConsumeStatusStacksAction(
                            consumeStacksPayload,
                            context.SourceUnit,
                            context.TargetUnit,
                            battleState
                        );
                        if (consumedUnitIds != null && consumedUnitIds.Count > 0)
                        {
                            foreach (StringName consumedUnitId in consumedUnitIds)
                                context.Batch?.AddChangedUnitId(consumedUnitId);
                            changed = true;
                        }
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
            RemainingDurationTu = payload.MirrorStatusDurationTu > 0
                ? payload.MirrorStatusDurationTu
                : -1,
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
                ReconcileTargetMarkStatusesAfterRemoval(
                    battleState,
                    previousTarget,
                    replaced,
                    binding
                );
                context.Batch?.AddChangedUnitId(previousTarget.unit_id);
            }
        }

        if (ApplyMirrorStatus(battleState, targetUnit, payload))
        {
            context.Batch?.AddChangedUnitId(targetUnit.unit_id);
        }
        return changed;
    }

    private static void AddUniqueStatusId(List<StringName> result, StringName statusId)
    {
        if (statusId == "" || result.Contains(statusId))
            return;
        result.Add(statusId);
    }

    private bool ApplyMirrorStatus(
        BattleState battleState,
        BattleUnitState targetUnit,
        MarkTargetActionPayloadDefinition payload
    )
    {
        if (
            battleState == null
            || targetUnit == null
            || payload == null
            || payload.MirrorStatusId == ""
            || !RefreshTargetMarkMirrorStatus(
                battleState,
                targetUnit,
                payload.MirrorStatusId
            )
        )
        {
            return false;
        }
        _runtime?.MarkAppliedStatusesForTurnTiming(
            targetUnit,
            new Godot.Collections.Array<StringName> { payload.MirrorStatusId }
        );
        return true;
    }

    private bool RefreshTargetMarkMirrorStatus(
        BattleState battleState,
        BattleUnitState targetUnit,
        StringName mirrorStatusId,
        bool preserveExistingDuration = false
    )
    {
        if (battleState == null || targetUnit == null || mirrorStatusId == "")
            return false;

        BattleStatusEffectState existing = targetUnit.GetStatusEffect(mirrorStatusId);
        StringName preferredSourceUnitId = ProgressionDataUtils.to_string_name(
            existing?.source_unit_id
        );
        BattleEquipmentTargetMarkState selectedMark = null;
        MarkTargetActionPayloadDefinition selectedPayload = null;
        foreach (BattleEquipmentTargetMarkState candidate in battleState.GetEquipmentTargetMarksTyped())
        {
            if (candidate?.IsValid != true || candidate.TargetUnitId != targetUnit.unit_id)
                continue;
            EquipmentAbilityBindingDefinition candidateBinding = ResolveBindingForTargetMark(
                candidate
            );
            MarkTargetActionPayloadDefinition candidatePayload = ResolveTargetMarkPayload(
                candidateBinding,
                candidate.StateKey,
                mirrorStatusId
            );
            if (
                candidatePayload == null
                || !IsPreferredMirrorMark(candidate, selectedMark, preferredSourceUnitId)
            )
            {
                continue;
            }
            selectedMark = candidate;
            selectedPayload = candidatePayload;
        }
        if (selectedMark == null || selectedPayload == null)
            return false;

        StringName stackBehavior = selectedPayload.MirrorStatusStackBehavior == ""
            ? new StringName("refresh")
            : selectedPayload.MirrorStatusStackBehavior;
        int stackLimit = Math.Max(selectedPayload.MirrorStatusStackLimit, 0);
        int stacks = Math.Max(selectedMark.Stacks, 1);
        if (stackLimit > 0)
            stacks = Math.Min(stacks, stackLimit);

        BattleStatusEffectState status = existing?.DuplicateState() ?? new BattleStatusEffectState();
        status.status_id = mirrorStatusId;
        status.source_unit_id = selectedMark.SourceUnitId;
        status.power = stacks;
        status.stacks = stacks;
        status.duration =
            preserveExistingDuration
                && existing != null
                && preferredSourceUnitId == selectedMark.SourceUnitId
                && existing.duration > 0
            ? existing.duration
            : selectedMark.RemainingDurationTu > 0
                ? selectedMark.RemainingDurationTu
                : -1;
        status.stack_behavior = stackBehavior;
        status.stack_limit = stackLimit;
        if (!string.IsNullOrWhiteSpace(selectedPayload.MirrorStatusDisplayLabel))
            status.display_label = selectedPayload.MirrorStatusDisplayLabel;
        targetUnit.SetStatusEffect(status);
        return true;
    }

    private static bool IsPreferredMirrorMark(
        BattleEquipmentTargetMarkState candidate,
        BattleEquipmentTargetMarkState selected,
        StringName preferredSourceUnitId
    )
    {
        if (candidate?.IsValid != true)
            return false;
        if (selected?.IsValid != true)
            return true;
        int candidateDuration = candidate.RemainingDurationTu < 0
            ? int.MaxValue
            : candidate.RemainingDurationTu;
        int selectedDuration = selected.RemainingDurationTu < 0
            ? int.MaxValue
            : selected.RemainingDurationTu;
        if (candidateDuration != selectedDuration)
            return candidateDuration > selectedDuration;
        return candidate.SourceUnitId == preferredSourceUnitId
            && selected.SourceUnitId != preferredSourceUnitId;
    }

    private static MarkTargetActionPayloadDefinition ResolveTargetMarkPayload(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey,
        StringName mirrorStatusId
    )
    {
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action?.PayloadDefinition is MarkTargetActionPayloadDefinition payload
                    && ProgressionDataUtils.to_string_name(payload.StateKey) == stateKey
                    && ProgressionDataUtils.to_string_name(payload.MirrorStatusId)
                        == mirrorStatusId
                )
                {
                    return payload;
                }
            }
        }
        return null;
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
                        EquipmentAbilityFactContext.FromOnKill(context),
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
                    EquipmentAbilityFactContext.FromOnKill(context),
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
                    activeBinding,
                    binding,
                    action,
                    attackPayload,
                    context,
                    result
                );
            }
            else if (
                action.Kind == ActionKindTriggerSkill
                && action.PayloadDefinition is TriggerSkillActionPayloadDefinition triggerSkillPayload
            )
            {
                ResolveTriggerSkillAction(
                    activeBinding,
                    binding,
                    action,
                    triggerSkillPayload,
                    context.SourceUnit,
                    context.DefeatedUnit,
                    context.BattleState,
                    context.Batch,
                    context.SaveContext,
                    triggeredResult => result?.AddTriggeredSkillResult(triggeredResult)
                );
            }
            else if (
                action.Kind == ActionKindModifyActionPoints
                && action.PayloadDefinition is ModifyActionPointsActionPayloadDefinition actionPointsPayload
            )
            {
                BattleUnitState changedUnit = ResolveModifyActionPointsAction(
                    activeBinding,
                    binding,
                    action,
                    actionPointsPayload,
                    context.SourceUnit,
                    context.DefeatedUnit
                );
                if (changedUnit != null)
                    context.Batch?.AddChangedUnitId(changedUnit.unit_id);
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
                    context.DefeatedUnit
                );
            }
            else if (
                action.Kind == ActionKindConsumeStatusStacks
                && action.PayloadDefinition is ConsumeStatusStacksActionPayloadDefinition consumeStacksPayload
            )
            {
                ResolveConsumeStatusStacksAction(
                    consumeStacksPayload,
                    context.SourceUnit,
                    context.DefeatedUnit,
                    context.BattleState
                );
            }
        }
    }

    private void ResolveImmediateWeaponAttackAction(
        ActiveEquipmentAbilityBinding activeBinding,
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
                    EventBatch = context.Batch,
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
                    new BattleDefeatHandlingOptions(
                        recordEnemyDefeatedAchievement: true,
                        killProvenance: BattleKillProvenance.FromWeaponAttackResult(
                            sourceUnit,
                            attackResult,
                            BattleKillProvenance.ForEquipmentAttack(
                                activeBinding.Source?.SourceEquipmentInstanceId ?? "",
                                binding?.BindingId ?? "",
                                action?.ActionId ?? ""
                            )
                        )
                    )
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

    private bool ResolveSummonUnitsAction(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        SummonUnitsActionPayloadDefinition payload,
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
        )
        {
            return false;
        }

        BattleUnitState anchorUnit = ResolveSubject(
            payload.AnchorSelector == "" ? new StringName("skill_target") : payload.AnchorSelector,
            context.SourceUnit,
            context.TargetUnit
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
            return false;

        int requested = Math.Max(RollDiceExpression(payload.CountDice), 0);
        int toCreate = Math.Min(requested, capacity);
        if (toCreate <= 0)
            return false;

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

        if (created <= 0)
            return false;

        context.Batch?.AddLogLine(
            $"{context.SourceUnit.display_name} 触发 {ResolveActionLabel(binding, action?.ActionId.ToString() ?? "")}。"
        );
        return true;
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
        foreach (StringName skillId in payload.KnownActiveSkillIds ?? Array.Empty<StringName>())
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (normalizedSkillId == "")
                continue;
            unit.AddKnownActiveSkill(normalizedSkillId);
            unit.SetKnownSkillLevelTyped(
                normalizedSkillId,
                normalizedSkillId == "basic_attack" ? 0 : 1,
                preserveZero: true
            );
        }
        WeaponDice naturalWeaponDice = BuildNaturalWeaponDice(payload.NaturalWeaponDamageDice);
        if (
            naturalWeaponDice != null
            && payload.NaturalWeaponProfileTypeId != ""
            && payload.NaturalWeaponDamageTag != ""
        )
        {
            unit.SetNaturalWeaponProjectionTyped(
                payload.NaturalWeaponProfileTypeId,
                payload.NaturalWeaponDamageTag,
                Math.Max(payload.NaturalWeaponAttackRange, 1),
                naturalWeaponDice,
                payload.NaturalWeaponFamily == ""
                    ? new StringName("natural")
                    : payload.NaturalWeaponFamily
            );
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

    private static WeaponDice BuildNaturalWeaponDice(DiceExpressionDefinition dice)
    {
        if (dice?.Terms == null || dice.Terms.Count == 0)
            return null;
        DiceExpressionTermDefinition term = dice.Terms[0];
        if (term == null || term.DiceCount <= 0 || term.DiceSides <= 0)
            return null;
        return new WeaponDice
        {
            dice_count = term.DiceCount,
            dice_sides = term.DiceSides,
            flat_bonus = dice.FlatBonus,
        };
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
        foreach (
            BattleUnitState targetUnit in ResolveApplyStatusTargets(
                payload?.TargetSelector ?? "",
                context.SourceUnit,
                context.DefeatedUnit,
                context.BattleState ?? _runtime?.GetState()
            )
        )
        {
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
        ItemDefinition itemDef = ResolveItemDef(entry.ItemId);
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
        foreach (EquipmentAbilityActionDefinition action in CollectReactionActions(binding, reaction, context, result))
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
                ResolveAddDamageDiceAction(activeBinding, binding, action, dicePayload, context, result);
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
                action.Kind == ActionKindTriggerSkill
                && action.PayloadDefinition is TriggerSkillActionPayloadDefinition triggerSkillPayload
            )
            {
                ResolveTriggerSkillAction(
                    activeBinding,
                    binding,
                    action,
                    triggerSkillPayload,
                    context.SourceUnit,
                    context.TargetUnit,
                    context.BattleState,
                    context.Batch,
                    context.SaveContext,
                    triggeredResult => result?.AddTriggeredSkillResult(triggeredResult)
                );
            }
            else if (
                action.Kind == ActionKindApplyBattleTerrainEffectAfterCheck
                && action.PayloadDefinition is ApplyBattleTerrainEffectAfterCheckActionPayloadDefinition terrainPayload
            )
            {
                ResolveApplyBattleTerrainEffectAfterCheckAction(
                    binding,
                    reaction,
                    action,
                    terrainPayload,
                    context,
                    result
                );
            }
            else if (
                action.Kind == ActionKindApplyEdgeFeature
                && action.PayloadDefinition is ApplyEdgeFeatureActionPayloadDefinition edgePayload
            )
            {
                ResolveApplyEdgeFeatureAction(
                    activeBinding,
                    binding,
                    reaction,
                    action,
                    edgePayload,
                    context
                );
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
                action.Kind == ActionKindModifyActionPoints
                && action.PayloadDefinition is ModifyActionPointsActionPayloadDefinition actionPointsPayload
            )
            {
                ResolveModifyActionPointsAction(
                    activeBinding,
                    binding,
                    action,
                    actionPointsPayload,
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
            else if (
                action.Kind == ActionKindConsumeStatusStacks
                && action.PayloadDefinition is ConsumeStatusStacksActionPayloadDefinition consumeStacksPayload
            )
            {
                ResolveConsumeStatusStacksAction(
                    consumeStacksPayload,
                    context.SourceUnit,
                    context.TargetUnit,
                    context.BattleState
                );
            }
        }
    }

    private IReadOnlyList<EquipmentAbilityActionDefinition> CollectReactionActions(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        BattleEquipmentAbilityAfterHitContext context,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        var actions = new List<EquipmentAbilityActionDefinition>();
        foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
        {
            if (action != null)
                actions.Add(action);
        }
        foreach (EquipmentAbilityActionDefinition action in SelectOutcomeTableActions(
            binding,
            reaction,
            context,
            result
        ))
        {
            if (action != null)
                actions.Add(action);
        }
        return actions;
    }

    private IReadOnlyList<EquipmentAbilityActionDefinition> SelectOutcomeTableActions(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        BattleEquipmentAbilityAfterHitContext context,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        EquipmentOutcomeTableDefinition table = reaction?.OutcomeTable;
        if (table?.Roll == null || table.Entries == null || table.Entries.Count == 0)
            return Array.Empty<EquipmentAbilityActionDefinition>();
        int rolledValue = ResolveRollGateValue(0, table.Roll);
        EquipmentOutcomeEntryDefinition matchedEntry = null;
        foreach (EquipmentOutcomeEntryDefinition entry in table.Entries)
        {
            if (entry == null)
                continue;
            int minRoll = Math.Min(entry.MinRoll, entry.MaxRoll);
            int maxRoll = Math.Max(entry.MinRoll, entry.MaxRoll);
            if (rolledValue >= minRoll && rolledValue <= maxRoll)
            {
                matchedEntry = entry;
                break;
            }
        }
        result?.AddRoll(
            new BattleEquipmentAbilityRollResult
            {
                BindingId = binding?.BindingId ?? "",
                ReactionId = reaction?.ReactionId ?? "",
                ActionId = table.TableId,
                RolledValue = rolledValue,
                Compare = "range",
                Threshold = 0,
                Passed = matchedEntry != null,
            }
        );
        return matchedEntry?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>();
    }

    private void ResolveApplyStatusAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        ApplyStatusActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitContext context,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        foreach (
            BattleUnitState targetUnit in ResolveApplyStatusTargets(
                payload?.TargetSelector ?? "",
                context.SourceUnit,
                context.TargetUnit,
                context.BattleState
            )
        )
        {
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
    }

    private void ResolveApplyBattleTerrainEffectAfterCheckAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        EquipmentAbilityActionDefinition action,
        ApplyBattleTerrainEffectAfterCheckActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitContext context,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        BattleState state = context?.BattleState ?? _runtime?.GetState();
        BattleGridService gridService = _runtime?.GetGridService();
        BattleTerrainEffectSystem terrainEffectSystem = _runtime?._terrain_effect_system;
        BattleUnitState sourceUnit = context?.SourceUnit;
        BattleUnitState anchorUnit = ResolveSubject(
            payload?.AnchorSelector == "" ? new StringName("attack_target") : payload?.AnchorSelector ?? "",
            sourceUnit,
            context?.TargetUnit
        );
        if (
            state == null
            || gridService == null
            || terrainEffectSystem == null
            || sourceUnit == null
            || anchorUnit == null
            || payload == null
            || payload.TerrainEffectId == ""
            || payload.MoveCostDelta <= 0
            || context.WeaponHpDamage <= 0
        )
        {
            return;
        }

        Vector2I coord = anchorUnit.coord;
        BattleCellState cell = gridService.GetCellState(state, coord);
        if (cell == null || CellHasActiveTerrainEffect(cell, payload.TerrainEffectId))
            return;

        int naturalRoll = ResolveAbilityCheckD20();
        int modifier = sourceUnit.attribute_snapshot?.GetValue(payload.CheckAttributeModifierId) ?? 0;
        int total = naturalRoll + modifier;
        bool passed = AbilityCheckPasses(naturalRoll, total, payload);
        result?.AddRoll(
            new BattleEquipmentAbilityRollResult
            {
                BindingId = binding?.BindingId ?? "",
                ReactionId = reaction?.ReactionId ?? "",
                ActionId = action?.ActionId ?? "",
                RolledValue = total,
                Compare = payload.CheckCompare,
                Threshold = payload.CheckThreshold,
                Passed = passed,
            }
        );
        if (!passed)
            return;

        CombatEffectDefinition effectDefinition = BuildBattleTerrainEffectDefinition(payload);
        StringName fieldInstanceId = BuildTerrainEffectInstanceId(
            binding,
            action,
            coord
        );
        if (
            terrainEffectSystem.UpsertTimedTerrainEffectFromDefinition(
                coord,
                sourceUnit,
                null,
                effectDefinition,
                fieldInstanceId
            )
        )
        {
            state.MarkMovementGeometryChanged();
        }
    }

    private int ResolveAbilityCheckD20()
    {
        if (_forcedAbilityCheckRollValuesForTests.Count > 0)
            return _forcedAbilityCheckRollValuesForTests.Dequeue();
        return TrueRandomSeedService.RandiRange(1, 20);
    }

    private static bool AbilityCheckPasses(
        int naturalRoll,
        int total,
        ApplyBattleTerrainEffectAfterCheckActionPayloadDefinition payload
    )
    {
        if (payload?.NaturalTwentyAutoSuccess == true && naturalRoll == 20)
            return true;
        if (payload?.NaturalOneAutoFailure == true && naturalRoll == 1)
            return false;
        return CompareInt(total, payload?.CheckCompare ?? "", payload?.CheckThreshold ?? 0);
    }

    private static bool CellHasActiveTerrainEffect(
        BattleCellState cell,
        StringName terrainEffectId
    )
    {
        foreach (
            BattleTerrainEffectState effectState in cell?.timed_terrain_effects
                ?? new List<BattleTerrainEffectState>()
        )
        {
            if (
                effectState?.effect_id == terrainEffectId
                && BattleTerrainEffectSystem.IsTerrainEffectActive(effectState)
            )
            {
                return true;
            }
        }
        return false;
    }

    private static StringName BuildTerrainEffectInstanceId(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        Vector2I coord
    )
    {
        return new StringName(
            $"{binding?.BindingId ?? new StringName("")}:{action?.ActionId ?? new StringName("")}:{coord.X}:{coord.Y}"
        );
    }

    private static CombatEffectDefinition BuildBattleTerrainEffectDefinition(
        ApplyBattleTerrainEffectAfterCheckActionPayloadDefinition payload
    )
    {
        StringName targetTeamFilter = payload?.TargetTeamFilter == ""
            ? new StringName("any")
            : payload?.TargetTeamFilter ?? "any";
        StringName stackBehavior = payload?.StackBehavior == ""
            ? new StringName("ignore_existing")
            : payload?.StackBehavior ?? "ignore_existing";
        return new CombatEffectDefinition(
            effectType: "terrain_effect",
            effectTargetTeamFilter: targetTeamFilter,
            statusId: "",
            saveFailureStatusId: "",
            terrainEffectId: payload?.TerrainEffectId ?? "",
            terrainReplaceTo: "",
            heightDelta: 0,
            requiresWeapon: false,
            addWeaponDice: false,
            preventRepeatTarget: false,
            forcedMoveMode: "",
            minSkillLevel: 0,
            maxSkillLevel: 0,
            damageTag: "",
            damageRatioPercent: 0,
            preResistanceDamageMultiplier: 1.0,
            bonusCondition: "",
            hpRatioThresholdPercent: 0,
            damageCategory: "",
            drBypassTag: "",
            diceCount: 0,
            diceSides: 0,
            diceBonus: 0,
            bonusDamageDiceCount: 0,
            bonusDamageDiceSides: 0,
            bonusDamageDiceBonus: 0,
            saveDc: 0,
            saveDcMode: "",
            saveDcSourceAbility: "",
            saveAbility: "",
            savePartialOnSuccess: false,
            saveTag: "",
            thresholdBaseValue: 0,
            thresholdLevelAnchor: 0,
            thresholdLevelBonusPerDelta: 0,
            thresholdMaxHpRatioPercent: 0,
            thresholdCapMaxHpRatioPercent: 0,
            soulFractureDurationTu: 0,
            healMultiplierPercent: 0,
            shieldGainMultiplierPercent: 0,
            appliedStatusDurationTu: 0,
            durationTu: 0,
            tickIntervalTu: 0,
            effectTags: Array.Empty<StringName>(),
            tickEffectType: "none",
            lifetimePolicy: "battle",
            moveCostDelta: Math.Max(payload?.MoveCostDelta ?? 0, 0),
            renderOverlayId: payload?.RenderOverlayId ?? "",
            overlayPriority: payload?.OverlayPriority ?? 0,
            displayName: payload?.DisplayName ?? "",
            stackBehavior: stackBehavior,
            parameters: new Dictionary<string, object>(StringComparer.Ordinal)
        );
    }

    private bool ResolveApplyEdgeFeatureAction(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        EquipmentAbilityActionDefinition action,
        ApplyEdgeFeatureActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitContext context
    )
    {
        BattleState state = context?.BattleState ?? _runtime?.GetState();
        BattleGridService gridService = _runtime?.GetGridService();
        BattleUnitState fromUnit = ResolveSubject(
            payload?.FromSelector == "" ? new StringName("source") : payload?.FromSelector ?? "",
            context?.SourceUnit,
            context?.TargetUnit
        );
        BattleUnitState toUnit = ResolveSubject(
            payload?.ToSelector == "" ? new StringName("attack_target") : payload?.ToSelector ?? "",
            context?.SourceUnit,
            context?.TargetUnit
        );
        if (
            state == null
            || gridService == null
            || payload == null
            || payload.DurationTu <= 0
            || fromUnit == null
            || toUnit == null
            || fromUnit.unit_id == toUnit.unit_id
        )
        {
            return false;
        }

        if (
            !TryResolveAdjacentEdgeBetweenUnits(
                fromUnit,
                toUnit,
                out Vector2I fromCoord,
                out Vector2I toCoord
            )
        )
        {
            return false;
        }
        if (
            !BattleTemporaryEdgeFeatureState.TryNormalizeEdge(
                fromCoord,
                toCoord,
                out Vector2I originCoord,
                out Vector2I direction
            )
        )
        {
            return false;
        }
        if (payload.RequireAdjacent && gridService.GetDistance(fromCoord, toCoord) != 1)
        {
            return false;
        }
        BattleEdgeFaceState existingFace = gridService.GetEdgeFace(state, fromCoord, toCoord);
        StringName stateTag = ProgressionDataUtils.to_string_name(payload.StateTag);
        if (
            existingFace != null
            && existingFace.HasFeatureFace()
            && existingFace.feature_state_tag != stateTag
        )
        {
            return false;
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
            return false;
        }

        int currentTu = Math.Max(state.timeline?.current_tu ?? 0, 0);
        BattleEdgeFeatureState featureState = BuildEdgeFeatureState(payload, stateTag);
        if (featureState == null || featureState.IsEmpty())
            return false;

        return state.PutTemporaryEdgeFeature(
            new BattleTemporaryEdgeFeatureState
            {
                OriginCoord = originCoord,
                Direction = direction,
                SourceUnitId = context.SourceUnit?.unit_id ?? "",
                SourceEquipmentInstanceId = activeBinding.Source?.SourceEquipmentInstanceId ?? "",
                BindingId = binding?.BindingId ?? "",
                ActionId = action?.ActionId ?? "",
                CreatedAtTu = currentTu,
                ExpiresAtTu = currentTu + payload.DurationTu,
                Feature = featureState,
            },
            payload.RefreshExisting,
            payload.MaxActiveEdges
        );
    }

    private static BattleEdgeFeatureState BuildEdgeFeatureState(
        ApplyEdgeFeatureActionPayloadDefinition payload,
        StringName stateTag
    )
    {
        if (payload == null)
            return null;
        var featureState = new BattleEdgeFeatureState
        {
            feature_kind = payload.FeatureKind,
            render_kind = payload.RenderKind,
            render_layers = Math.Max(payload.RenderLayers, 0),
            blocks_move = payload.BlocksMove,
            blocks_occupancy = payload.BlocksOccupancy,
            blocks_los = payload.BlocksLos,
            interaction_kind = payload.InteractionKind == "" ? new StringName("none") : payload.InteractionKind,
            state_tag = stateTag,
        };
        if (
            BattleEdgeFeatureState.ToFeatureKind(featureState.feature_kind)
                == BattleEdgeFeatureKind.Unknown
            || BattleEdgeFeatureState.ToRenderKind(featureState.render_kind)
                == BattleEdgeRenderKind.Unknown
            || BattleEdgeFeatureState.ToInteractionKind(featureState.interaction_kind)
                == BattleEdgeInteractionKind.Unknown
        )
        {
            return null;
        }
        return featureState;
    }

    private static bool TryResolveAdjacentEdgeBetweenUnits(
        BattleUnitState fromUnit,
        BattleUnitState toUnit,
        out Vector2I fromCoord,
        out Vector2I toCoord
    )
    {
        fromCoord = Vector2I.Zero;
        toCoord = Vector2I.Zero;
        if (fromUnit == null || toUnit == null)
            return false;
        fromUnit.RefreshFootprint();
        toUnit.RefreshFootprint();
        foreach (Vector2I sourceCoord in fromUnit.occupied_coords)
        {
            foreach (Vector2I targetCoord in toUnit.occupied_coords)
            {
                if (Math.Abs(sourceCoord.X - targetCoord.X) + Math.Abs(sourceCoord.Y - targetCoord.Y) != 1)
                    continue;
                fromCoord = sourceCoord;
                toCoord = targetCoord;
                return true;
            }
        }
        return false;
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

    private void ResolveTriggerSkillAction(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        TriggerSkillActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState contextTarget,
        BattleState battleState,
        BattleEventBatch batch,
        BattleSaveContext saveContext,
        Action<BattleEquipmentAbilityTriggeredSkillResult> addResult
    )
    {
        BattleState state = battleState ?? _runtime?.GetState();
        if (_damageResolver == null || state == null || sourceUnit == null || payload == null)
            return;
        SkillDefinition skillDefinition = _runtime?.GetSkillDefinitionTyped(payload.SkillId);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        BattleUnitState anchorUnit = ResolveEquipmentActionTarget(
            payload.TargetSelector,
            sourceUnit,
            contextTarget,
            activeBinding,
            binding,
            "",
            "",
            state
        );
        if (combatProfile == null || anchorUnit == null)
            return;

        IReadOnlyList<BattleUnitState> targets = CollectTriggeredSkillTargets(
            state,
            sourceUnit,
            anchorUnit,
            skillDefinition,
            payload.SkillLevel
        );
        if (targets.Count == 0)
            return;
        if (!string.IsNullOrWhiteSpace(payload.ActivationLog))
            batch?.AddLogLine(payload.ActivationLog);

        foreach (BattleUnitState targetUnit in targets)
        {
            IReadOnlyList<CombatEffectDefinition> effects = FilterTriggeredSkillEffects(
                skillDefinition,
                sourceUnit,
                targetUnit,
                payload.SkillLevel
            );
            if (effects.Count == 0)
                continue;
            AttackEffectResolutionResult resolution = _damageResolver.ResolveEffects(
                sourceUnit,
                targetUnit,
                effects,
                DamageResolutionContext
                    .Create(
                        criticalHit: false,
                        attackSuccess: false,
                        secondaryHitSuccess: false,
                        skillId: skillDefinition.SkillId,
                        sourceSkillLevel: Math.Max(payload.SkillLevel, 1),
                        saveRollOverrides: saveContext.SaveRollOverrides
                    )
                    .WithDamageApplicationHookContext(
                        batch,
                        _runtime?.CurrentEffectOriginForContingency
                            ?? BattleEffectOrigin.PlayerCommand()
                    )
            );
            addResult?.Invoke(
                new BattleEquipmentAbilityTriggeredSkillResult
                {
                    BindingId = binding?.BindingId ?? new StringName(""),
                    ActionId = action?.ActionId ?? new StringName(""),
                    TargetUnitId = targetUnit.unit_id,
                    MergeIntoParentResult = payload.MergeIntoParentResult,
                    Resolution = resolution,
                }
            );
            batch?.AddChangedUnitId(targetUnit.unit_id);
            foreach (Vector2I coord in targetUnit.GetOccupiedCoordsTyped())
                batch?.AddChangedCoord(coord);
            AppendTriggeredSkillSaveLogs(batch, targetUnit, payload.SaveLogLabel, resolution);

            if (payload.HandleTargetDefeat && targetUnit.is_alive != true)
            {
                _runtime?.HandleUnitDefeatedByRuntimeEffect(
                    targetUnit,
                    sourceUnit,
                    batch,
                    $"{targetUnit.display_name} 被击倒。",
                    new BattleDefeatHandlingOptions(
                        collectLoot: false,
                        recordEnemyDefeatedAchievement: false,
                        killProvenance: BattleKillProvenance.None
                    )
                );
            }
        }
    }

    private IReadOnlyList<BattleUnitState> CollectTriggeredSkillTargets(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState anchorUnit,
        SkillDefinition skillDefinition,
        int skillLevel
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (state == null || sourceUnit == null || anchorUnit == null || combatProfile == null)
            return Array.Empty<BattleUnitState>();
        if (combatProfile.TargetModeKind != BattleTargetMode.Ground)
            return anchorUnit.is_alive
                ? new[] { anchorUnit }
                : Array.Empty<BattleUnitState>();

        BattleTargetCollectionResult collection =
            _runtime?._target_collection_service?.CollectCombatProfileTargetCoords(
                state,
                _runtime.GetGridService(),
                sourceUnit.coord,
                combatProfile,
                new[] { anchorUnit.coord },
                sourceUnit,
                targetUnits: null,
                skillLevel: Math.Max(skillLevel, 1)
            );
        if (collection?.Handled != true || collection.TargetCoords.Count == 0)
            return Array.Empty<BattleUnitState>();
        var affectedCoords = new HashSet<Vector2I>(collection.TargetCoords);
        var targets = new List<BattleUnitState>();
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate?.is_alive != true
                || !BattleTargetTeamRules.IsUnitValidForFilter(
                    sourceUnit,
                    candidate,
                    combatProfile.TargetTeamFilter
                )
            )
            {
                continue;
            }
            bool intersects = false;
            foreach (Vector2I coord in candidate.GetOccupiedCoordsTyped())
            {
                if (affectedCoords.Contains(coord))
                {
                    intersects = true;
                    break;
                }
            }
            if (intersects)
                targets.Add(candidate);
        }
        targets.Sort(
            (left, right) => string.CompareOrdinal(
                left?.unit_id.ToString() ?? "",
                right?.unit_id.ToString() ?? ""
            )
        );
        return targets;
    }

    private static IReadOnlyList<CombatEffectDefinition> FilterTriggeredSkillEffects(
        SkillDefinition skillDefinition,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int skillLevel
    )
    {
        int normalizedLevel = Math.Max(skillLevel, 1);
        var effects = new List<CombatEffectDefinition>();
        foreach (CombatEffectDefinition effect in skillDefinition?.CombatProfile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect == null)
                continue;
            int minLevel = Math.Max(effect.MinSkillLevel, 0);
            int maxLevel = effect.MaxSkillLevel;
            if (normalizedLevel < minLevel || (maxLevel >= 0 && normalizedLevel > maxLevel))
                continue;
            StringName targetFilter = BattleTargetTeamRules.ResolveEffectTargetFilter(
                skillDefinition,
                effect
            );
            if (!BattleTargetTeamRules.IsUnitValidForFilter(sourceUnit, targetUnit, targetFilter))
                continue;
            effects.Add(effect);
        }
        return effects.Count == 0 ? Array.Empty<CombatEffectDefinition>() : effects;
    }

    private static void AppendTriggeredSkillSaveLogs(
        BattleEventBatch batch,
        BattleUnitState targetUnit,
        string label,
        AttackEffectResolutionResult resolution
    )
    {
        if (batch == null || string.IsNullOrWhiteSpace(label))
            return;
        foreach (SaveResolutionResult saveResult in resolution.SaveResults ?? Array.Empty<SaveResolutionResult>())
        {
            if (!saveResult.HasSave)
                continue;
            string outcome = saveResult.Immune ? "免疫" : saveResult.Success ? "成功" : "失败";
            batch.AddLogLine($"{targetUnit?.display_name ?? "目标"} {label}：{outcome}。");
        }
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

    private BattleUnitState ResolveHealFromFactAction(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        HealFromFactActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleState battleState,
        EquipmentAbilityFactContext factContext
    )
    {
        if (payload == null || sourceUnit == null)
            return null;
        if (
            !TryResolveFactInt(
                payload.AmountFact,
                sourceUnit,
                targetUnit,
                factContext,
                activeBinding,
                out int factAmount
            )
        )
        {
            return null;
        }

        int multiplier = Math.Max(payload.MultiplierPercent, 0);
        int healAmount = (int)Math.Max((long)Math.Max(factAmount, 0) * multiplier / 100L, 0L);
        if (payload.MaxAmount > 0)
            healAmount = Math.Min(healAmount, payload.MaxAmount);
        if (healAmount <= 0)
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

        int maxHp = Math.Max(
            resolvedTarget.attribute_snapshot?.GetValue(AttributeService.HP_MAX) ?? 0,
            1
        );
        int healed = resolvedTarget.ApplyHealing(healAmount, maxHp);
        return healed > 0 ? resolvedTarget : null;
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
        bool changed = false;
        BattleStatusEffectState existing = resolvedTarget?.GetStatusEffect(payload.StatusId);
        bool canClearStatus =
            existing != null
            && (!payload.RequireSourceUnitMatch
                || ProgressionDataUtils.to_string_name(existing.source_unit_id)
                    == sourceUnit.unit_id);
        bool mirrorHandledByMarkRemoval = false;
        if (payload.ClearTargetMark)
        {
            BattleState state = battleState ?? _runtime?.GetState();
            EquipmentAbilityBindingDefinition markBinding = ResolveStateBinding(
                activeBinding,
                binding,
                payload.MarkBindingId
            );
            if (
                state != null
                && markBinding != null
                && payload.MarkStateKey != ""
                && state.TryGetEquipmentTargetMark(
                    sourceUnit.unit_id,
                    activeBinding.Source?.SourceEquipmentInstanceId ?? "",
                    markBinding.BindingId,
                    payload.MarkStateKey,
                    out BattleEquipmentTargetMarkState removedMark
                )
                && state.RemoveEquipmentTargetMark(
                    sourceUnit.unit_id,
                    activeBinding.Source?.SourceEquipmentInstanceId ?? "",
                    markBinding.BindingId,
                    payload.MarkStateKey
                )
            )
            {
                changed = true;
                mirrorHandledByMarkRemoval = TargetMarkMirrorsStatus(
                    markBinding,
                    removedMark.StateKey,
                    payload.StatusId
                );
                ReconcileTargetMarkStatusesAfterRemoval(
                    state,
                    resolvedTarget,
                    removedMark,
                    markBinding
                );
            }
        }
        if (canClearStatus && !mirrorHandledByMarkRemoval)
        {
            resolvedTarget.EraseStatusEffect(payload.StatusId);
            changed = true;
        }
        return changed;
    }

    private List<StringName> ResolveConsumeStatusStacksAction(
        ConsumeStatusStacksActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleState battleState
    )
    {
        if (payload == null || sourceUnit == null || payload.StatusId == "" || payload.Count <= 0)
            return null;
        BattleState state = battleState ?? _runtime?.GetState();
        StringName selector = ProgressionDataUtils.to_string_name(payload.TargetSelector);
        var candidates = new List<BattleUnitState>();
        if (selector == "all_units")
        {
            if (state == null)
                return null;
            foreach (BattleUnitState unit in state.GetUnitsTyped())
            {
                if (unit != null)
                    candidates.Add(unit);
            }
        }
        else
        {
            BattleUnitState resolved = ResolveSubject(selector, sourceUnit, targetUnit);
            if (resolved != null)
                candidates.Add(resolved);
        }
        var holders = new List<(BattleUnitState Unit, BattleStatusEffectState Status)>();
        foreach (BattleUnitState unit in candidates)
        {
            BattleStatusEffectState status = unit.GetStatusEffect(payload.StatusId);
            if (status == null || status.stacks <= 0)
                continue;
            if (
                payload.RequireSourceUnitMatch
                && ProgressionDataUtils.to_string_name(status.source_unit_id) != sourceUnit.unit_id
            )
            {
                continue;
            }
            holders.Add((unit, status));
        }
        if (holders.Count == 0)
            return null;
        holders.Sort(
            (left, right) =>
            {
                int byStacks = right.Status.stacks.CompareTo(left.Status.stacks);
                if (byStacks != 0)
                    return byStacks;
                return string.CompareOrdinal(
                    left.Unit.unit_id.ToString(),
                    right.Unit.unit_id.ToString()
                );
            }
        );
        int remaining = payload.Count;
        var changedUnitIds = new List<StringName>();
        foreach ((BattleUnitState unit, BattleStatusEffectState status) in holders)
        {
            if (remaining <= 0)
                break;
            int consumed = Math.Min(status.stacks, remaining);
            remaining -= consumed;
            int stacksLeft = status.stacks - consumed;
            if (stacksLeft > 0)
                status.stacks = stacksLeft;
            else
                unit.EraseStatusEffect(payload.StatusId);
            changedUnitIds.Add(unit.unit_id);
        }
        return changedUnitIds.Count > 0 ? changedUnitIds : null;
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
        StringName stateKey = ProgressionDataUtils.to_string_name(
            payload?.StateKey ?? new StringName("")
        );
        if (owner == null || stateKey == "")
            return;

        StringName operation = ProgressionDataUtils.to_string_name(payload.Operation);
        if (IsPersistentCounterState(stateBinding, stateKey))
        {
            EquipmentInstanceState instance = EquipmentAbilityUsageRuntime.FindEquipmentInstance(
                owner,
                activeBinding.Source?.SourceEquipmentInstanceId ?? new StringName("")
            );
            if (instance == null)
                return;
            long currentCounter = GetPersistentCounterValue(instance, stateBinding, stateKey, 0);
            long nextCounter = ResolveNextAbilityStateValue(
                currentCounter,
                operation,
                payload.IntDelta
            );
            SetPersistentCounterValue(instance, stateBinding, stateKey, nextCounter);
            SyncDerivedAbilityStates(
                activeBinding,
                stateBinding,
                owner,
                stateKey,
                GetPersistentCounterValue(instance, stateBinding, stateKey, 0)
            );
            return;
        }

        StringName chargeKey = BuildBindingStateChargeKey(
            activeBinding.Source,
            stateBinding,
            stateKey
        );
        if (chargeKey == "")
            return;

        int current = GetAbilityStateValue(owner, stateBinding, chargeKey, stateKey, 0);
        SetAbilityStateValue(
            owner,
            stateBinding,
            chargeKey,
            stateKey,
            ClampFactInt(ResolveNextAbilityStateValue(current, operation, payload.IntDelta))
        );
        SyncDerivedAbilityStates(
            activeBinding,
            stateBinding,
            owner,
            stateKey,
            GetAbilityStateValue(owner, stateBinding, chargeKey, stateKey, 0)
        );
    }

    private void SyncDerivedAbilityStates(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition stateBinding,
        BattleUnitState owner,
        StringName sourceStateKey,
        long sourceValue
    )
    {
        sourceStateKey = ProgressionDataUtils.to_string_name(sourceStateKey);
        if (owner == null || stateBinding == null || sourceStateKey == "")
            return;
        foreach (
            EquipmentAbilityStateSchemaDefinition schema in stateBinding.StateSchemas
                ?? Array.Empty<EquipmentAbilityStateSchemaDefinition>()
        )
        {
            SyncDerivedAbilityState(
                activeBinding,
                stateBinding,
                owner,
                sourceStateKey,
                sourceValue,
                schema
            );
        }
    }

    private void SyncDerivedAbilityState(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition stateBinding,
        BattleUnitState owner,
        StringName sourceStateKey,
        long sourceValue,
        EquipmentAbilityStateSchemaDefinition schema
    )
    {
        if (
            schema == null
            || ProgressionDataUtils.to_string_name(schema.SyncSourceStateKey) != sourceStateKey
        )
            return;

        StringName syncStateKey = ProgressionDataUtils.to_string_name(schema.StateKey);
        if (syncStateKey == "" || syncStateKey == sourceStateKey)
            return;

        long syncValue = ApplyStateSyncAggregation(
            sourceValue,
            schema.SyncAggregation,
            schema.SyncIntLiteral
        );
        if (IsPersistentCounterState(stateBinding, syncStateKey))
        {
            EquipmentInstanceState instance = EquipmentAbilityUsageRuntime.FindEquipmentInstance(
                owner,
                activeBinding.Source?.SourceEquipmentInstanceId ?? new StringName("")
            );
            if (instance == null)
                return;
            SetPersistentCounterValue(instance, stateBinding, syncStateKey, syncValue);
            return;
        }

        StringName syncChargeKey = BuildBindingStateChargeKey(
            activeBinding.Source,
            stateBinding,
            syncStateKey
        );
        if (syncChargeKey == "")
            return;
        SetAbilityStateValue(
            owner,
            stateBinding,
            syncChargeKey,
            syncStateKey,
            ClampFactInt(syncValue)
        );
    }

    private static long ResolveNextAbilityStateValue(
        long current,
        StringName operation,
        int intDelta
    )
    {
        return operation == "clear"
            ? 0
            : operation == "add"
                ? current + intDelta
                : intDelta;
    }

    private static long ApplyStateSyncAggregation(
        long rawValue,
        StringName aggregation,
        int intLiteral
    )
    {
        long normalizedValue = Math.Max(rawValue, 0L);
        StringName normalizedAggregation = ProgressionDataUtils.to_string_name(aggregation);
        if (normalizedAggregation == "" || normalizedAggregation == "value")
            return normalizedValue;
        if (normalizedAggregation == "floor_div")
            return normalizedValue / Math.Max(intLiteral, 1);
        return normalizedValue;
    }

    private BattleUnitState ResolveModifyActionPointsAction(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        ModifyActionPointsActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        BattleUnitState target = ResolveSubject(
            payload?.TargetSelector ?? "",
            sourceUnit,
            targetUnit
        );
        if (payload == null || sourceUnit == null || target == null)
            return null;

        StringName mode = ProgressionDataUtils.to_string_name(payload.Mode);
        if (mode == "add_base_action_points")
        {
            int amount = payload.Amount > 0
                ? payload.Amount
                : Math.Max(target.attribute_snapshot?.GetValue(AttributeService.ACTION_POINTS) ?? 1, 1);
            target.SetCurrentAp(target.current_ap + amount);
            return target;
        }
        if (mode == "subtract_current_action_points")
        {
            int amount = payload.Amount > 0 ? payload.Amount : 1;
            target.SetCurrentAp(Math.Max(target.current_ap - amount, 0));
            return target;
        }
        if (mode == "restore_current_action_points_capped")
        {
            int amount = Math.Max(payload.Amount, 0);
            int actionPointCap = Math.Max(
                target.attribute_snapshot?.GetValue(AttributeService.ACTION_POINTS) ?? 0,
                0
            );
            int nextActionPoints = Math.Min(
                target.current_ap + amount,
                Math.Max(actionPointCap, target.current_ap)
            );
            if (nextActionPoints <= target.current_ap)
                return null;
            target.SetCurrentAp(nextActionPoints);
            return target;
        }
        if (mode == "set_next_turn_ap_to_zero")
        {
            StringName statusId = ProgressionDataUtils.to_string_name(payload.StatusId);
            if (statusId == "")
                statusId = BattleStatusSemanticTable.STATUS_TEMPORAL_AP_STOLEN;
            CombatEffectDefinition statusEffect = BattleRuntimeEffectDefinitions.Status(
                statusId,
                1,
                -1,
                stackBehavior: "refresh",
                stackLimit: 1,
                displayName: payload.DisplayLabel
            );
            BattleStatusEffectState statusEntry = BattleStatusSemanticTable.MergeStatus(
                statusEffect,
                sourceUnit.unit_id,
                target.GetStatusEffect(statusId),
                statusId
            );
            if (statusEntry == null)
                return null;
            target.SetStatusEffect(statusEntry);
            _runtime?.MarkAppliedStatusesForTurnTiming(
                target,
                new Godot.Collections.Array<StringName> { statusId }
            );
            return target;
        }
        return null;
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
        if (payload.OverrideHealMultiplierPercent)
            statusEntry.heal_multiplier_percent = Math.Clamp(payload.HealMultiplierPercent, 0, 100);
        else
            statusEntry.heal_multiplier_percent = null;
        if (payload.MovePointCapacityDelta != 0)
            statusEntry.move_point_capacity_delta = payload.MovePointCapacityDelta;
        statusEntry.forced_move_immune = payload.ForcedMoveImmune;
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
        bool resolvedLinkedStateActions = false;
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
            AppendBonusDamageDiceResult(
                activeBinding,
                binding,
                action,
                dicePayload,
                context.SourceUnit,
                context.TargetUnit,
                EquipmentAbilityFactContext.FromBonusDamageDice(context),
                result
            );
            if (!resolvedLinkedStateActions)
            {
                ResolveBonusDamageLinkedSetStateActions(activeBinding, reaction, context);
                resolvedLinkedStateActions = true;
            }
        }
    }

    private void ResolveBonusDamageLinkedSetStateActions(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityReactionDefinition reaction,
        BattleEquipmentAbilityBonusDamageDiceContext context
    )
    {
        EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
        foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
        {
            if (
                action == null
                || action.Kind != ActionKindModifyAbilityState
                || action.PayloadDefinition is not ModifyAbilityStateActionPayloadDefinition statePayload
                || ProgressionDataUtils.to_string_name(statePayload.Operation) != "set"
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
            ResolveModifyAbilityStateAction(
                activeBinding,
                binding,
                statePayload,
                context.SourceUnit,
                context.TargetUnit
            );
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

    private static bool IsPersistentCounterState(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        EquipmentAbilityStateSchemaDefinition schema = FindStateSchema(binding, stateKey);
        StringName resetTiming = ProgressionDataUtils.to_string_name(
            schema?.ResetTiming ?? new StringName("")
        );
        return resetTiming == ResetTimingPersistentCounter;
    }

    private static string BuildPersistentCounterId(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        StringName bindingId = ProgressionDataUtils.to_string_name(
            binding?.BindingId ?? new StringName("")
        );
        StringName normalizedStateKey = ProgressionDataUtils.to_string_name(stateKey);
        if (bindingId == "" || normalizedStateKey == "")
            return "";
        return $"{bindingId}:{normalizedStateKey}";
    }

    private static long GetPersistentCounterValue(
        EquipmentInstanceState instance,
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey,
        long fallback
    )
    {
        string counterId = BuildPersistentCounterId(binding, stateKey);
        if (instance == null || string.IsNullOrEmpty(counterId))
            return Math.Max(fallback, 0L);
        foreach (
            EquipmentAbilityPersistentCounterState counter in instance.ability_persistent_counters
                ?? new List<EquipmentAbilityPersistentCounterState>()
        )
        {
            if (counter != null && counter.CounterId == counterId)
                return Math.Max(counter.Value, 0L);
        }
        EquipmentAbilityStateSchemaDefinition schema = FindStateSchema(binding, stateKey);
        return Math.Max(schema?.InitialIntValue ?? fallback, 0L);
    }

    private static void SetPersistentCounterValue(
        EquipmentInstanceState instance,
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey,
        long value
    )
    {
        string counterId = BuildPersistentCounterId(binding, stateKey);
        if (instance == null || string.IsNullOrEmpty(counterId))
            return;
        EquipmentAbilityStateSchemaDefinition schema = FindStateSchema(binding, stateKey);
        long normalizedValue = Math.Max(value, 0L);
        if (schema != null && schema.MaxIntValue > 0)
            normalizedValue = Math.Min(normalizedValue, schema.MaxIntValue);
        instance.ability_persistent_counters ??= new List<EquipmentAbilityPersistentCounterState>();
        foreach (EquipmentAbilityPersistentCounterState counter in instance.ability_persistent_counters)
        {
            if (counter != null && counter.CounterId == counterId)
            {
                counter.Value = normalizedValue;
                return;
            }
        }
        instance.ability_persistent_counters.Add(
            new EquipmentAbilityPersistentCounterState
            {
                CounterId = counterId,
                Value = normalizedValue,
            }
        );
    }

    private static int ApplyFactIntAggregation(
        EquipmentAbilityFactQueryDefinition query,
        long rawValue
    )
    {
        long normalizedValue = Math.Max(rawValue, 0L);
        StringName aggregation = ProgressionDataUtils.to_string_name(
            query?.Aggregation ?? new StringName("")
        );
        if (aggregation == "" || aggregation == "value")
            return ClampFactInt(normalizedValue);
        if (aggregation == "floor_div")
        {
            int divisor = Math.Max(query?.IntLiteral ?? 0, 1);
            return ClampFactInt(normalizedValue / divisor);
        }
        return ClampFactInt(normalizedValue);
    }

    private static int ClampFactInt(long value) =>
        value > int.MaxValue ? int.MaxValue : (int)Math.Max(value, 0L);

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
        if (commit.Destroyed)
        {
            RefreshEquipmentProjectionAfterDurabilityDestruction(
                context.TargetUnit,
                context.Batch
            );
        }
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
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        AddDamageDiceActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitContext context,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        if (payload?.Dice == null)
            return;
        AppendBonusDamageDiceResult(
            activeBinding,
            binding,
            action,
            payload,
            context?.SourceUnit,
            context?.TargetUnit,
            EquipmentAbilityFactContext.FromAfterHit(context),
            result
        );
    }

    private void AppendBonusDamageDiceResult(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        AddDamageDiceActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        if (result == null)
            return;
        foreach (BattleEquipmentAbilityBonusDamageDiceResult dice in BuildBonusDamageDiceResults(
            activeBinding,
            binding,
            action,
            payload,
            sourceUnit,
            targetUnit,
            factContext
        ))
        {
            result.AddBonusDamageDice(dice);
        }
    }

    private void AppendBonusDamageDiceResult(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        AddDamageDiceActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext,
        List<BattleEquipmentAbilityBonusDamageDiceResult> result
    )
    {
        if (result == null)
            return;
        result.AddRange(
            BuildBonusDamageDiceResults(
                activeBinding,
                binding,
                action,
                payload,
                sourceUnit,
                targetUnit,
                factContext
            )
        );
    }

    private IEnumerable<BattleEquipmentAbilityBonusDamageDiceResult> BuildBonusDamageDiceResults(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        AddDamageDiceActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext
    )
    {
        if (payload?.Dice == null)
            yield break;
        bool emittedTerm = false;
        foreach (DiceExpressionTermDefinition term in payload.Dice.Terms ?? Array.Empty<DiceExpressionTermDefinition>())
        {
            if (term == null || term.DiceSides <= 0)
                continue;
            int diceCount = Math.Max(term.DiceCount, 0);
            if (
                term.CountBonusFact != null
                && TryResolveFactInt(
                    term.CountBonusFact,
                    sourceUnit,
                    targetUnit,
                    factContext,
                    activeBinding,
                    out int factBonus
                )
            )
            {
                diceCount += Mathf.FloorToInt(
                    Math.Max(factBonus, 0) * Math.Max(term.CountBonusMultiplier, 0f)
                );
            }
            if (term.MaxDiceCount > 0)
                diceCount = Math.Min(diceCount, term.MaxDiceCount);
            if (diceCount <= 0)
                continue;
            emittedTerm = true;
            yield return new BattleEquipmentAbilityBonusDamageDiceResult
            {
                BindingId = binding.BindingId,
                ActionId = action.ActionId,
                DiceCount = diceCount,
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
        ItemDefinition itemDef = ResolveItemDef(target.ItemId);
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
            condition.Kind == ConditionKindHasStatus
            && condition.PayloadDefinition is HasStatusConditionPayloadDefinition statusPayload
        )
        {
            return HasStatusConditionPasses(statusPayload, sourceUnit, targetUnit);
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

    private static bool HasStatusConditionPasses(
        HasStatusConditionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        if (payload == null)
            return false;
        BattleUnitState subject = ResolveSubject(payload.Subject, sourceUnit, targetUnit);
        StringName statusId = ProgressionDataUtils.to_string_name(payload.StatusId);
        return subject != null && statusId != "" && subject.HasStatusEffect(statusId);
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
        ItemDefinition itemDef = ResolveItemDef(itemId);
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
        if (query.FactId == FactCurrentActionPoints)
        {
            BattleUnitState apSubject = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            if (apSubject == null)
                return false;
            value = Math.Max(apSubject.current_ap, 0);
            return true;
        }
        if (query.FactId == FactKillSourceIsAttack)
        {
            value =
                factContext.KillProvenance.IsAttack
                && factContext.KillProvenance.IncludesWeaponDamage
                    ? 1
                    : 0;
            return true;
        }
        if (query.FactId == FactKillSourceEquipmentInstanceMatches)
        {
            StringName sourceEquipmentInstanceId =
                activeBinding.Source?.SourceEquipmentInstanceId ?? new StringName("");
            value =
                sourceEquipmentInstanceId != ""
                && factContext.KillProvenance.SourceEquipmentInstanceId
                    == sourceEquipmentInstanceId
                    ? 1
                    : 0;
            return true;
        }
        if (query.FactId == FactKillSourceBindingMatches)
        {
            StringName bindingId =
                activeBinding.Binding?.BindingId ?? new StringName("");
            value =
                bindingId != ""
                && factContext.KillProvenance.SourceBindingId == bindingId
                    ? 1
                    : 0;
            return true;
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
        if (query.FactId == FactSkillMovedTargetCount)
        {
            value = Math.Max(factContext.SkillMovedTargetCount, 0);
            return true;
        }
        if (query.FactId == FactSkillUnmovedTargetCount)
        {
            value = Math.Max(factContext.SkillUnmovedTargetCount, 0);
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
            StringName stateKey = ProgressionDataUtils.to_string_name(query.StateKey);
            if (owner == null || stateBinding == null || stateKey == "")
                return false;
            if (IsPersistentCounterState(stateBinding, stateKey))
            {
                EquipmentInstanceState instance = EquipmentAbilityUsageRuntime.FindEquipmentInstance(
                    owner,
                    activeBinding.Source?.SourceEquipmentInstanceId ?? new StringName("")
                );
                if (instance == null)
                    return false;
                value = ApplyFactIntAggregation(
                    query,
                    GetPersistentCounterValue(instance, stateBinding, stateKey, 0)
                );
                return true;
            }
            StringName chargeKey = BuildBindingStateChargeKey(
                activeBinding.Source,
                stateBinding,
                stateKey
            );
            if (chargeKey == "")
                return false;
            value = ApplyFactIntAggregation(
                query,
                GetAbilityStateValue(owner, stateBinding, chargeKey, stateKey, 0)
            );
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
        if (query.FactId == FactExpiredTargetMarkMatches)
        {
            BattleEquipmentTargetMarkState expiredMark = factContext.ExpiredTargetMark;
            StringName bindingId = ProgressionDataUtils.to_string_name(query.BindingId);
            StringName stateKey = ProgressionDataUtils.to_string_name(query.StateKey);
            BattleUnitState expiredSubject = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            value =
                expiredMark?.IsValid == true
                && expiredMark.SourceUnitId == (sourceUnit?.unit_id ?? new StringName(""))
                && expiredMark.SourceEquipmentInstanceId
                    == (activeBinding.Source?.SourceEquipmentInstanceId ?? new StringName(""))
                && (bindingId == "" || expiredMark.BindingId == bindingId)
                && (stateKey == "" || expiredMark.StateKey == stateKey)
                && (expiredSubject == null || expiredMark.TargetUnitId == expiredSubject.unit_id)
                    ? 1
                    : 0;
            return true;
        }
        if (query.FactId == FactStatusStacks)
        {
            BattleUnitState statusSubject = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            StringName statusId = ProgressionDataUtils.to_string_name(query.StatusId);
            if (statusSubject == null || statusId == "")
                return false;
            BattleStatusEffectState status = statusSubject.GetStatusEffect(statusId);
            if (
                query.RequireSourceUnitMatch
                && (sourceUnit == null || status?.source_unit_id != sourceUnit.unit_id)
            )
            {
                value = 0;
                return true;
            }
            value = Math.Max(status?.stacks ?? 0, 0);
            return true;
        }
        if (
            query.FactId == FactNearbyEnemyCount
            || query.FactId == FactNearbyUnitCount
            || query.FactId == FactNearbyAllyCount
        )
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
            if (query.FactId == FactNearbyUnitCount)
                value = CountNearbyLivingUnits(state, nearbySubject, radius);
            else if (query.FactId == FactNearbyAllyCount)
                value = CountNearbyLivingAllies(state, nearbySubject, radius);
            else
                value = CountNearbyLivingEnemies(state, nearbySubject, radius);
            return true;
        }
        if (query.FactId == FactUnitDistance)
        {
            if (sourceUnit == null || targetUnit == null)
                return false;
            value = BattleGridDistanceService.GetDistanceBetweenUnits(sourceUnit, targetUnit);
            return true;
        }
        if (query.FactId == FactSourceStatusTotalStacks)
        {
            BattleUnitState stacksOwner = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            StringName totalStatusId = ProgressionDataUtils.to_string_name(query.StatusId);
            if (stacksOwner == null || totalStatusId == "")
                return false;
            BattleState totalState =
                factContext.BattleState
                ?? _runtime?.GetState();
            if (totalState == null)
                return false;
            int totalStacks = 0;
            foreach (BattleUnitState unit in totalState.GetUnitsTyped())
            {
                BattleStatusEffectState status = unit?.GetStatusEffect(totalStatusId);
                if (status == null || status.stacks <= 0)
                    continue;
                if (
                    ProgressionDataUtils.to_string_name(status.source_unit_id)
                    != stacksOwner.unit_id
                )
                {
                    continue;
                }
                totalStacks += status.stacks;
            }
            value = totalStacks;
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

    internal bool AdvanceTargetMarkDurations(
        BattleUnitState targetUnit,
        int elapsedTu,
        BattleEventBatch batch = null
    )
    {
        BattleState state = _runtime?.GetState();
        if (state == null || targetUnit == null || elapsedTu <= 0)
            return false;

        bool changed = false;
        foreach (BattleEquipmentTargetMarkState mark in state.GetEquipmentTargetMarksTyped())
        {
            if (
                mark?.IsValid != true
                || mark.TargetUnitId != targetUnit.unit_id
                || mark.RemainingDurationTu <= 0
            )
            {
                continue;
            }

            int remainingDurationTu = Math.Max(mark.RemainingDurationTu - elapsedTu, 0);
            if (remainingDurationTu > 0)
            {
                if (
                    state.SetEquipmentTargetMark(
                        mark.WithRemainingDurationTu(remainingDurationTu),
                        uniquePerSource: true,
                        out _
                    )
                )
                {
                    changed = true;
                }
                continue;
            }

            EquipmentAbilityBindingDefinition markBinding = ResolveBindingForTargetMark(mark);
            ResolveExpiredTargetMark(state, targetUnit, mark, batch);
            ReconcileTargetMarkStatusesAfterRemoval(
                state,
                targetUnit,
                mark,
                markBinding,
                preserveExistingMirrorDuration: true
            );
            changed = true;
        }
        return changed;
    }

    internal bool ResolveTargetMarkExpired(
        BattleUnitState targetUnit,
        BattleStatusEffectState expiredStatus,
        BattleEventBatch batch = null
    )
    {
        BattleState state = _runtime?.GetState();
        if (state == null || targetUnit == null || expiredStatus == null)
            return false;
        bool changed = false;
        foreach (BattleEquipmentTargetMarkState mark in state.GetEquipmentTargetMarksTyped())
        {
            if (
                mark?.IsValid != true
                || mark.TargetUnitId != targetUnit.unit_id
                || mark.SourceUnitId
                    != ProgressionDataUtils.to_string_name(expiredStatus.source_unit_id)
            )
            {
                continue;
            }
            EquipmentAbilityBindingDefinition markBinding = ResolveBindingForTargetMark(mark);
            if (!TargetMarkMirrorsStatus(markBinding, mark.StateKey, expiredStatus.status_id))
                continue;
            changed |= ResolveExpiredTargetMark(state, targetUnit, mark, batch);
        }
        return changed;
    }

    private bool ResolveExpiredTargetMark(
        BattleState state,
        BattleUnitState targetUnit,
        BattleEquipmentTargetMarkState mark,
        BattleEventBatch batch
    )
    {
        if (state == null || targetUnit == null || mark?.IsValid != true)
            return false;

        BattleUnitState sourceUnit = state.GetUnit(mark.SourceUnitId);
        if (sourceUnit?.is_alive == true)
        {
            var context = new BattleEquipmentAbilityTargetMarkExpiredContext
            {
                SourceUnit = sourceUnit,
                TargetUnit = targetUnit,
                BattleState = state,
                Batch = batch,
                Mark = mark,
            };
            foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(sourceUnit))
            {
                if (
                    activeBinding.Source?.SourceEquipmentInstanceId
                    != mark.SourceEquipmentInstanceId
                )
                {
                    continue;
                }
                EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
                foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
                {
                    if (
                        reaction == null
                        || reaction.Trigger != EquipmentAbilityTriggerKind.OnTargetMarkExpired
                        || reaction.Timing != EquipmentAbilityTimingKind.AfterStatusExpired
                        || !ConditionGroupPasses(
                            reaction.ConditionGroup,
                            sourceUnit,
                            targetUnit,
                            EquipmentAbilityFactContext.FromTargetMarkExpired(context),
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
                    ResolveTargetMarkExpiredActions(
                        activeBinding,
                        binding,
                        reaction,
                        context
                    );
                }
            }
        }

        bool removed = state.RemoveEquipmentTargetMark(
            mark.SourceUnitId,
            mark.SourceEquipmentInstanceId,
            mark.BindingId,
            mark.StateKey
        );
        if (removed)
        {
            batch?.AddChangedUnitId(mark.SourceUnitId);
            batch?.AddChangedUnitId(mark.TargetUnitId);
        }
        return removed;
    }

    private void ResolveTargetMarkExpiredActions(
        ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        BattleEquipmentAbilityTargetMarkExpiredContext context
    )
    {
        foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
        {
            if (
                action == null
                || !ConditionGroupPasses(
                    action.ConditionGroup,
                    context.SourceUnit,
                    context.TargetUnit,
                    EquipmentAbilityFactContext.FromTargetMarkExpired(context),
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
                action.Kind == ActionKindTriggerSkill
                && action.PayloadDefinition is TriggerSkillActionPayloadDefinition triggerSkillPayload
            )
            {
                ResolveTriggerSkillAction(
                    activeBinding,
                    binding,
                    action,
                    triggerSkillPayload,
                    context.SourceUnit,
                    context.TargetUnit,
                    context.BattleState,
                    context.Batch,
                    BattleSaveContext.Empty,
                    addResult: null
                );
            }
        }
    }

    private static bool TargetMarkMirrorsStatus(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey,
        StringName statusId
    )
    {
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action?.PayloadDefinition is MarkTargetActionPayloadDefinition payload
                    && ProgressionDataUtils.to_string_name(payload.StateKey) == stateKey
                    && ProgressionDataUtils.to_string_name(payload.MirrorStatusId) == statusId
                )
                {
                    return true;
                }
            }
        }
        return false;
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
            if (
                state.RemoveEquipmentTargetMark(
                    mark.SourceUnitId,
                    mark.SourceEquipmentInstanceId,
                    mark.BindingId,
                    mark.StateKey
                )
            )
            {
                ReconcileTargetMarkStatusesAfterRemoval(state, targetUnit, mark, binding);
                AddUniqueUnitId(changedUnitIds, mark.SourceUnitId);
                AddUniqueUnitId(changedUnitIds, mark.TargetUnitId);
            }
        }
        return changedUnitIds;
    }

    internal IReadOnlyList<StringName> ClearTargetMarksForRemovedEquipmentSources(
        BattleState state,
        BattleUnitState sourceUnit
    )
    {
        var changedUnitIds = new List<StringName>();
        if (state == null || sourceUnit == null || sourceUnit.unit_id == "")
            return changedUnitIds;

        foreach (BattleEquipmentTargetMarkState mark in state.GetEquipmentTargetMarksTyped())
        {
            if (
                mark?.IsValid != true
                || mark.SourceUnitId != sourceUnit.unit_id
                || !mark.RemoveOnSourceMissing
                || HasProjectedEquipmentAbilitySource(sourceUnit, mark)
            )
            {
                continue;
            }

            EquipmentAbilityBindingDefinition binding = ResolveBindingForTargetMark(mark);
            BattleUnitState targetUnit = state.GetUnit(mark.TargetUnitId);
            if (
                !state.RemoveEquipmentTargetMark(
                    mark.SourceUnitId,
                    mark.SourceEquipmentInstanceId,
                    mark.BindingId,
                    mark.StateKey
                )
            )
            {
                continue;
            }

            ReconcileTargetMarkStatusesAfterRemoval(state, targetUnit, mark, binding);
            AddUniqueUnitId(changedUnitIds, mark.SourceUnitId);
            AddUniqueUnitId(changedUnitIds, mark.TargetUnitId);
        }
        return changedUnitIds;
    }

    internal IReadOnlyList<StringName> RefreshEquipmentProjectionAfterDurabilityDestruction(
        BattleUnitState targetUnit,
        BattleEventBatch batch = null
    )
    {
        if (
            targetUnit == null
            || targetUnit.source_member_id == ""
            || _runtime?._unit_factory == null
        )
        {
            return Array.Empty<StringName>();
        }

        IReadOnlyList<StringName> changedUnitIds =
            _runtime._unit_factory.RefreshEquipmentProjection(targetUnit);
        batch?.AddChangedUnitId(targetUnit.unit_id);
        foreach (StringName changedUnitId in changedUnitIds)
            batch?.AddChangedUnitId(changedUnitId);
        return changedUnitIds;
    }

    private bool ClearStaleEquipmentTargetMarkIfNeeded(
        BattleState state,
        EquipmentAbilityBindingDefinition binding,
        BattleEquipmentTargetMarkState mark
    )
    {
        if (state == null || binding == null || mark?.IsValid != true)
            return false;
        BattleUnitState sourceUnit = state.GetUnit(mark.SourceUnitId);
        BattleUnitState targetUnit = state.GetUnit(mark.TargetUnitId);
        bool sourceMissing =
            !IsLivingUnit(sourceUnit) || !HasProjectedEquipmentAbilitySource(sourceUnit, mark);
        bool targetMissing =
            !IsLivingUnit(targetUnit)
            && ShouldRemoveTargetMarkOnTargetDefeated(binding, mark);
        if (!targetMissing && !(mark.RemoveOnSourceMissing && sourceMissing))
            return false;

        bool removed = state.RemoveEquipmentTargetMark(
            mark.SourceUnitId,
            mark.SourceEquipmentInstanceId,
            mark.BindingId,
            mark.StateKey
        );
        if (removed)
            ReconcileTargetMarkStatusesAfterRemoval(state, targetUnit, mark, binding);
        return removed;
    }

    private static bool HasProjectedEquipmentAbilitySource(
        BattleUnitState sourceUnit,
        BattleEquipmentTargetMarkState mark
    )
    {
        if (sourceUnit == null || mark?.IsValid != true)
            return false;
        foreach (
            BattleEquipmentAbilitySourceState source in sourceUnit.equipment_ability_sources
                ?? new List<BattleEquipmentAbilitySourceState>()
        )
        {
            if (
                source != null
                && source.SourceEquipmentInstanceId == mark.SourceEquipmentInstanceId
                && source.AbilityIds?.Contains(mark.BindingId) == true
            )
            {
                return true;
            }
        }
        return false;
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

    private void ReconcileTargetMarkStatusesAfterRemoval(
        BattleState state,
        BattleUnitState targetUnit,
        BattleEquipmentTargetMarkState mark,
        EquipmentAbilityBindingDefinition binding,
        bool preserveExistingMirrorDuration = false
    )
    {
        if (targetUnit == null || mark == null || binding == null)
            return;
        List<StringName> mirrorStatusIds = BuildTargetMarkMirrorStatusIds(
            binding,
            mark.StateKey
        );
        foreach (StringName mirrorStatusId in mirrorStatusIds)
        {
            if (mirrorStatusId == "")
                continue;
            if (
                RefreshTargetMarkMirrorStatus(
                    state,
                    targetUnit,
                    mirrorStatusId,
                    preserveExistingMirrorDuration
                )
            )
                continue;
            targetUnit.EraseStatusEffect(mirrorStatusId);
        }
        foreach (StringName statusId in BuildTargetMarkClearStatusIds(binding, mark.StateKey))
        {
            if (statusId == "" || mirrorStatusIds.Contains(statusId))
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

    private static List<StringName> BuildTargetMarkMirrorStatusIds(
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
            }
        }
        return result;
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

    private static int CountNearbyLivingAllies(
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
                || candidate.faction_id != sourceUnit.faction_id
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
        if (query.QueryKind == QueryKindFact && query.FactId == FactWeaponRangeType)
        {
            BattleUnitState subject = ResolveSubject(query.Subject, sourceUnit, targetUnit);
            return ProgressionDataUtils.to_string_name(subject?.weapon_range_type ?? new StringName(""));
        }
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

    private static IEnumerable<BattleUnitState> ResolveApplyStatusTargets(
        StringName targetSelector,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleState battleState
    )
    {
        StringName selector = ProgressionDataUtils.to_string_name(targetSelector);
        if (
            selector == "adjacent_enemies_of_target"
            || selector == "adjacent_enemies_of_defeated"
            || selector == "adjacent_enemies_of_victim"
        )
        {
            if (battleState == null || sourceUnit == null || targetUnit == null)
                yield break;
            foreach (BattleUnitState candidate in battleState.GetUnitsTyped())
            {
                if (
                    candidate == null
                    || candidate.unit_id == targetUnit.unit_id
                    || !BattleTargetTeamRules.IsUnitValidForFilter(
                        sourceUnit,
                        candidate,
                        BattleTypedNames.TargetFilterEnemy
                    )
                    || BattleGridDistanceService.GetDistanceBetweenUnits(targetUnit, candidate) > 1
                )
                {
                    continue;
                }
                yield return candidate;
            }
            yield break;
        }

        BattleUnitState resolved = ResolveSubject(selector, sourceUnit, targetUnit);
        if (resolved != null)
            yield return resolved;
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

    private ItemDefinition ResolveItemDef(StringName itemId)
    {
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs =
            _runtime?.GetItemDefIndexTyped();
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        return normalizedItemId != ""
            && itemDefs != null
            && itemDefs.TryGetValue(normalizedItemId, out ItemDefinition itemDef)
            ? itemDef
            : null;
    }

    private static bool AllTagsPresent(
        ItemDefinition itemDef,
        IReadOnlyList<StringName> requiredTags
    )
    {
        if (requiredTags == null || requiredTags.Count == 0)
            return true;
        foreach (StringName requiredTag in requiredTags)
            if (!BattleEquipmentRequirementRules.ItemHasTag(itemDef, requiredTag))
                return false;
        return true;
    }

    private static bool AnyTagPresent(
        ItemDefinition itemDef,
        IReadOnlyList<StringName> requiredTags
    )
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
