using System;
using System.Collections.Generic;
using Godot;

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
    public bool IsPreview { get; init; }
    public Action<BattleEquipmentAbilityActionPreviewResult> PreviewActionSink { get; init; }
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

internal sealed class BattleEquipmentAbilityTurnEndContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleState BattleState { get; init; }
}

internal readonly struct EquipmentAbilityFactContext
{
    internal readonly bool CriticalHit;
    internal readonly int CurrentTu;
    internal readonly BattleState BattleState;
    internal readonly int RawDamage;
    internal readonly int HpDamage;
    internal readonly int HpBefore;
    internal readonly StringName DamageTag;
    internal readonly bool IsEquipmentGenerated;
    internal readonly bool IsSelfDamage;
    internal readonly int SkillDamagedTargetCount;
    internal readonly int SkillKilledTargetCount;
    internal readonly int SkillHpDamageDealt;
    internal readonly int SkillMovedTargetCount;
    internal readonly int SkillUnmovedTargetCount;
    internal readonly BattleKillProvenance KillProvenance;
    internal readonly BattleEquipmentTargetMarkState ExpiredTargetMark;
    internal readonly StringName SkillId;
    internal readonly StringName SaveTag;
    internal readonly IReadOnlyList<StringName> EffectCategories;
    internal readonly BattleDamageOriginKind DamageOriginKind;

    private EquipmentAbilityFactContext(
        bool criticalHit,
        int currentTu,
        BattleState battleState,
        int rawDamage = 0,
        int hpDamage = 0,
        int hpBefore = 0,
        StringName damageTag = default,
        bool isEquipmentGenerated = false,
        bool isSelfDamage = false,
        int skillDamagedTargetCount = 0,
        int skillKilledTargetCount = 0,
        int skillHpDamageDealt = 0,
        int skillMovedTargetCount = 0,
        int skillUnmovedTargetCount = 0,
        BattleKillProvenance killProvenance = default,
        BattleEquipmentTargetMarkState expiredTargetMark = null,
        StringName skillId = default,
        StringName saveTag = default,
        IReadOnlyList<StringName> effectCategories = null,
        BattleDamageOriginKind damageOriginKind = BattleDamageOriginKind.Unknown
    )
    {
        CriticalHit = criticalHit;
        CurrentTu = currentTu;
        BattleState = battleState;
        RawDamage = Math.Max(rawDamage, 0);
        HpDamage = Math.Max(hpDamage, 0);
        HpBefore = Math.Max(hpBefore, 0);
        DamageTag = ProgressionDataUtils.to_string_name(damageTag);
        IsEquipmentGenerated = isEquipmentGenerated;
        IsSelfDamage = isSelfDamage;
        SkillDamagedTargetCount = Math.Max(skillDamagedTargetCount, 0);
        SkillKilledTargetCount = Math.Max(skillKilledTargetCount, 0);
        SkillHpDamageDealt = Math.Max(skillHpDamageDealt, 0);
        SkillMovedTargetCount = Math.Max(skillMovedTargetCount, 0);
        SkillUnmovedTargetCount = Math.Max(skillUnmovedTargetCount, 0);
        KillProvenance = killProvenance;
        ExpiredTargetMark = expiredTargetMark;
        SkillId = ProgressionDataUtils.to_string_name(skillId);
        SaveTag = ProgressionDataUtils.to_string_name(saveTag);
        EffectCategories = effectCategories ?? Array.Empty<StringName>();
        DamageOriginKind = damageOriginKind;
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

    internal static EquipmentAbilityFactContext FromMitigationTier(
        BattleEquipmentAbilityMitigationTierContext context
    ) => new(
        false,
        Math.Max(context?.BattleState?.timeline?.current_tu ?? -1, -1),
        context?.BattleState,
        skillId: context?.SkillId ?? new StringName(""),
        saveTag: context?.SaveTag ?? new StringName(""),
        effectCategories: context?.EffectCategories,
        damageOriginKind: context?.DamageOriginKind ?? BattleDamageOriginKind.Unknown
    );

    internal static EquipmentAbilityFactContext FromDirectDamage(
        BattleEquipmentAbilityDirectDamageContext context
    ) => new(
        context?.CriticalHit == true,
        Math.Max(context?.BattleState?.timeline?.current_tu ?? -1, -1),
        context?.BattleState,
        skillId: context?.SkillId ?? new StringName(""),
        saveTag: context?.SaveTag ?? new StringName(""),
        effectCategories: context?.EffectCategories,
        damageOriginKind: context?.DamageOriginKind ?? BattleDamageOriginKind.Unknown
    );

    internal static EquipmentAbilityFactContext FromBattleState(BattleState state) =>
        new(false, Math.Max(state?.timeline?.current_tu ?? -1, -1), state);

    internal static EquipmentAbilityFactContext FromAfterHit(
        BattleEquipmentAbilityAfterHitContext context
    ) => new(
        context?.CriticalHit == true,
        Math.Max(context?.BattleState?.timeline?.current_tu ?? -1, -1),
        context?.BattleState,
        hpDamage: context?.WeaponHpDamage ?? 0,
        skillId: context?.SkillId ?? new StringName(""),
        damageOriginKind: context?.DamageOriginKind ?? BattleDamageOriginKind.Unknown
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
        rawDamage: context?.RawDamage ?? 0,
        hpDamage: context?.HpDamage ?? 0,
        hpBefore: context?.HpBefore ?? 0,
        damageTag: context?.DamageTag ?? new StringName(""),
        isEquipmentGenerated: context?.IsEquipmentGenerated == true,
        isSelfDamage: context?.IsSelfDamage == true
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


internal sealed class BattleEquipmentAbilityLootQuantityMultiplierResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public LootQuantityMultiplierActionPayloadDefinition Payload { get; init; }
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

internal sealed class BattleEquipmentAbilityRuntimeService :
    IBattleEquipmentCombatReactionSink,
    IBattleFatalInterceptArbiter
{
    internal static readonly StringName ActionKindAddDamageDice = "add_damage_dice";
    private static readonly StringName ActionKindImmediateWeaponAttack =
        "immediate_weapon_attack";
    private static readonly StringName ActionKindDealDamage = "deal_damage";
    private static readonly StringName ActionKindHeal = "heal";
    private static readonly StringName ActionKindHealFromFact = "heal_from_fact";
    internal static readonly StringName ActionKindAttackRollBonus = "attack_roll_bonus";
    internal static readonly StringName ActionKindAttackRollAdvantage = "attack_roll_advantage";
    internal static readonly StringName ActionKindCriticalHitOverride = "critical_hit_override";
    internal static readonly StringName ActionKindAttackDefenseModifier =
        "attack_defense_modifier";
    internal static readonly StringName ActionKindDamageRollModeOverride =
        "damage_roll_mode_override";
    internal static readonly StringName ActionKindDamageReduction =
        "damage_reduction";
    internal static readonly StringName ActionKindGrantMitigationTier =
        "grant_mitigation_tier";
    private static readonly StringName ActionKindApplyStatus = "apply_status";
    private static readonly StringName ActionKindModifyActionPoints = "modify_action_points";
    internal static readonly StringName ActionKindModifyAbilityState = "modify_ability_state";
    private static readonly StringName ActionKindMarkTarget = "mark_target";
    private static readonly StringName ActionKindClearStatus = "clear_status";
    internal static readonly StringName ActionKindTriggerSkill = "trigger_skill";
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
    internal static readonly StringName ActionKindSummonedUnitAttackRollModifier =
        "summoned_unit_attack_roll_modifier";
    private static readonly StringName DropKindItem = "item";
    internal static readonly StringName StackModeMax = "max";

    private BattleRuntimeModule _runtime;
    private BattleDamageResolver _damageResolver;
    private readonly BattleEquipmentSummonResolver _summonResolver = new();
    private readonly BattleEquipmentTargetMarkResolver _targetMarkResolver = new();
    private readonly BattleEquipmentAbilityConditionEvaluator _conditionEvaluator = new();
    private readonly BattleEquipmentStatusActionResolver _statusActionResolver = new();
    private readonly BattleEquipmentSkillTriggerActionResolver _skillTriggerActionResolver = new();
    private readonly BattleEquipmentAreaActionResolver _areaActionResolver = new();
    private readonly BattleEquipmentDirectEffectActionResolver _directEffectActionResolver = new();
    private readonly BattleEquipmentAbilityStateResolver _abilityStateResolver = new();
    private readonly BattleEquipmentAttackModifierResolver _attackModifierResolver = new();
    private readonly Queue<int> _forcedRollGateValuesForTests = new();
    private readonly Queue<int> _forcedFatalRecoveryValuesForTests = new();
    private readonly Queue<int> _forcedAbilityCheckRollValuesForTests = new();

    internal void Setup(BattleRuntimeModule runtime, BattleDamageResolver damageResolver)
    {
        _runtime = runtime;
        _damageResolver = damageResolver;
        // 接线顺序：ability state resolver 无内部依赖先行接线；
        // summon / target mark / condition evaluator 依赖 state resolver 与彼此；
        // 各 executor 最后接线（target mark resolver 另依赖 skill trigger executor）。
        _abilityStateResolver.Setup(runtime);
        _summonResolver.Setup(runtime, this, _conditionEvaluator, _abilityStateResolver);
        _targetMarkResolver.Setup(
            runtime,
            this,
            _conditionEvaluator,
            _skillTriggerActionResolver,
            _abilityStateResolver
        );
        _conditionEvaluator.Setup(
            runtime,
            this,
            _summonResolver,
            _targetMarkResolver,
            _abilityStateResolver
        );
        _statusActionResolver.Setup(runtime, this, _targetMarkResolver, _abilityStateResolver);
        _skillTriggerActionResolver.Setup(runtime, this);
        _areaActionResolver.Setup(runtime, this);
        _directEffectActionResolver.Setup(runtime, this, _conditionEvaluator, _abilityStateResolver);
        _attackModifierResolver.Setup(
            this,
            _conditionEvaluator,
            _summonResolver,
            _directEffectActionResolver
        );
    }

    internal bool AdvanceTargetMarkDurations(
        BattleUnitState targetUnit,
        int elapsedTu,
        BattleEventBatch batch = null
    ) => _targetMarkResolver.AdvanceTargetMarkDurations(targetUnit, elapsedTu, batch);

    internal bool ResolveTargetMarkExpired(
        BattleUnitState targetUnit,
        BattleStatusEffectState expiredStatus,
        BattleEventBatch batch = null
    ) => _targetMarkResolver.ResolveTargetMarkExpired(targetUnit, expiredStatus, batch);

    internal IReadOnlyList<StringName> ClearTargetMarksForDefeatedUnit(
        BattleState state,
        BattleUnitState defeatedUnit
    ) => _targetMarkResolver.ClearTargetMarksForDefeatedUnit(state, defeatedUnit);

    internal IReadOnlyList<StringName> ClearTargetMarksForRemovedEquipmentSources(
        BattleState state,
        BattleUnitState sourceUnit
    ) => _targetMarkResolver.ClearTargetMarksForRemovedEquipmentSources(state, sourceUnit);

    internal IReadOnlyList<StringName> ClearSourceBoundStatusesForRemovedEquipmentSources(
        BattleState state,
        BattleUnitState sourceUnit
    ) =>
        _statusActionResolver?.ClearSourceBoundStatusesForRemovedEquipmentSources(
            state,
            sourceUnit
        ) ?? Array.Empty<StringName>();

    internal IReadOnlyList<StringName> RefreshEquipmentProjectionAfterDurabilityDestruction(
        BattleUnitState targetUnit,
        BattleEventBatch batch = null
    ) =>
        _targetMarkResolver.RefreshEquipmentProjectionAfterDurabilityDestruction(
            targetUnit,
            batch
        );

    internal List<BattleAttackRollModifierSpec> CollectAttackRollModifierCandidates(
        BattleAttackCheckPolicyContext context
    ) => _attackModifierResolver.CollectAttackRollModifierCandidates(context);

    internal EquipmentAttackDefenseAdjustment CollectAttackDefenseAdjustment(
        BattleAttackCheckPolicyContext context
    ) => _attackModifierResolver.CollectAttackDefenseAdjustment(context);

    internal BattleEquipmentAbilityCriticalHitOverrideResult ResolveCriticalHitOverride(
        BattleAttackCheckPolicyContext context
    ) => _attackModifierResolver.ResolveCriticalHitOverride(context);

    internal IReadOnlyList<StringName> CollectProjectedWeaponEffectCategories(
        BattleUnitState sourceUnit,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        SkillDefinition skillDefinition = null,
        CombatCastVariantDefinition castVariantDefinition = null
    ) =>
        _attackModifierResolver.CollectProjectedWeaponEffectCategories(
            sourceUnit,
            effectDefinitions,
            skillDefinition,
            castVariantDefinition
        );

    internal IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> CollectBonusDamageDiceOnHit(
        BattleEquipmentAbilityBonusDamageDiceContext context
    ) => _attackModifierResolver.CollectBonusDamageDiceOnHit(context);

    internal StringName ResolveDamageRollModeOverride(
        BattleEquipmentAbilityDamageRollModeContext context
    ) => _attackModifierResolver.ResolveDamageRollModeOverride(context);

    internal IReadOnlyList<BattleEquipmentAbilityDamageReductionResult> CollectDamageReductions(
        BattleEquipmentAbilityDamageReductionContext context
    ) => _attackModifierResolver.CollectDamageReductions(context);

    internal IReadOnlyList<BattleEquipmentAbilityMitigationTierResult> CollectMitigationTiers(
        BattleEquipmentAbilityMitigationTierContext context
    ) => _attackModifierResolver.CollectMitigationTiers(context);

    internal IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> CollectBonusDamageDiceForEffect(
        BattleEquipmentAbilityDirectDamageContext context
    ) => _attackModifierResolver.CollectBonusDamageDiceForEffect(context);
    internal List<BattleLootEntry> ApplyLootQuantityMultipliers(
        IEnumerable<BattleLootEntry> lootEntries,
        BattleEquipmentAbilityOnKillResult onKillResult
    ) => _attackModifierResolver.ApplyLootQuantityMultipliers(lootEntries, onKillResult);

    internal void Dispose()
    {
        _attackModifierResolver.DisposeRuntime();
        _directEffectActionResolver.DisposeRuntime();
        _areaActionResolver.DisposeRuntime();
        _skillTriggerActionResolver.DisposeRuntime();
        _statusActionResolver.DisposeRuntime();
        _conditionEvaluator.DisposeRuntime();
        _targetMarkResolver.DisposeRuntime();
        _summonResolver.DisposeRuntime();
        _abilityStateResolver.DisposeRuntime();
        _damageResolver?.SetEquipmentAbilityPorts(null, null);
        _damageResolver?.SetFatalInterceptArbiter(null);
        _damageResolver = null;
        _runtime = null;
        _forcedRollGateValuesForTests.Clear();
        _forcedFatalRecoveryValuesForTests.Clear();
        _forcedAbilityCheckRollValuesForTests.Clear();
    }

    internal BattleState GetBattleState() => _runtime?.GetState();

    internal int ApplyMovementTrails(
        BattleUnitState sourceUnit,
        IReadOnlyList<Vector2I> executedPath,
        StringName skillId,
        BattleEventBatch batch
    )
    {
        BattleTerrainEffectSystem terrainEffectSystem = _runtime?._terrain_effect_system;
        if (
            sourceUnit?.IsAlive() != true
            || executedPath == null
            || executedPath.Count < 2
            || terrainEffectSystem == null
        )
        {
            return 0;
        }

        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        var ungrouped = new List<ActiveMovementTrailCandidate>();
        var grouped = new Dictionary<StringName, ActiveMovementTrailCandidate>();
        foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(sourceUnit))
        {
            IReadOnlyList<EquipmentMovementTrailDefinition> trails =
                activeBinding.Binding?.MovementTrails
                ?? Array.Empty<EquipmentMovementTrailDefinition>();
            foreach (EquipmentMovementTrailDefinition trail in trails)
            {
                if (
                    trail == null
                    || trail.TrailId == ""
                    || trail.DurationTu <= 0
                    || trail.DamageDice?.Terms?.Count != 1
                    || (
                        trail.RequiredSkillId != ""
                        && trail.RequiredSkillId != normalizedSkillId
                    )
                )
                {
                    continue;
                }

                var candidate = new ActiveMovementTrailCandidate(
                    activeBinding.Source,
                    activeBinding.Binding,
                    trail
                );
                if (trail.ReplacementGroupId == "")
                {
                    ungrouped.Add(candidate);
                    continue;
                }
                if (
                    !grouped.TryGetValue(trail.ReplacementGroupId, out ActiveMovementTrailCandidate current)
                    || CompareMovementTrailCandidate(candidate, current) < 0
                )
                {
                    grouped[trail.ReplacementGroupId] = candidate;
                }
            }
        }

        var selected = new List<ActiveMovementTrailCandidate>(ungrouped);
        selected.AddRange(grouped.Values);
        selected.Sort(CompareMovementTrailCandidate);
        if (selected.Count == 0)
            return 0;

        var trailCoords = new List<Vector2I>();
        var seenCoords = new HashSet<Vector2I>();
        for (int index = 0; index < executedPath.Count - 1; index++)
        {
            Vector2I coord = executedPath[index];
            if (seenCoords.Add(coord))
                trailCoords.Add(coord);
        }
        if (trailCoords.Count == 0)
            return 0;

        int appliedCount = 0;
        foreach (ActiveMovementTrailCandidate candidate in selected)
        {
            EquipmentMovementTrailDefinition trail = candidate.Trail;
            DiceExpressionTermDefinition term = trail.DamageDice.Terms[0];
            CombatEffectDefinition effectDefinition =
                BattleRuntimeEffectDefinitions.TimedTerrainContactDamage(
                    trail.TrailId,
                    trail.DurationTu,
                    trail.TargetTeamFilter,
                    term.DiceCount,
                    term.DiceSides,
                    trail.DamageDice.FlatBonus,
                    trail.DamageTag,
                    trail.DisplayName
                );
            int nonce = _runtime.IncrementTerrainEffectNonce();
            StringName fieldInstanceId = new(
                $"equipment_movement_trail:{sourceUnit.unit_id}:{nonce}:{candidate.Binding.BindingId}:{trail.TrailId}"
            );
            SkillDefinition skillDefinition = normalizedSkillId != ""
                ? _runtime.GetSkillDefinitionTyped(normalizedSkillId)
                : null;
            foreach (Vector2I coord in trailCoords)
            {
                if (
                    !terrainEffectSystem.UpsertTimedTerrainEffectFromDefinition(
                        coord,
                        sourceUnit,
                        skillDefinition,
                        effectDefinition,
                        fieldInstanceId
                    )
                )
                {
                    continue;
                }
                appliedCount += 1;
                batch?.AddChangedCoord(coord);
            }
        }
        return appliedCount;
    }

    internal IBattleEquipmentAttackCheckQuery AttackCheckQuery => _attackModifierResolver;

    internal IBattleEquipmentDamageQuery DamageQuery => _attackModifierResolver;

    internal IBattleEquipmentCombatReactionSink ReactionSink => this;

    internal IBattleFatalInterceptArbiter FatalInterceptArbiter => this;

    bool IBattleEquipmentCombatReactionSink.ResolveAttackCheck(
        BattleEquipmentAbilityAttackCheckContext context
    ) => ResolveAttackCheck(context);

    BattleEquipmentAbilityAfterHitResult IBattleEquipmentCombatReactionSink.ResolveAfterHit(
        BattleEquipmentAbilityAfterHitContext context
    ) => ResolveAfterHit(context);

    BattleEquipmentAbilityAfterHitResult IBattleEquipmentCombatReactionSink.ResolveHitReceived(
        BattleEquipmentAbilityAfterHitContext context
    ) => ResolveHitReceived(context);

    BattleEquipmentAbilityAfterHitResult IBattleEquipmentCombatReactionSink.ResolveAttackHit(
        BattleEquipmentAbilityAfterHitContext context
    ) => ResolveAttackHit(context);

    IReadOnlyList<StringName>
        IBattleEquipmentCombatReactionSink.RefreshEquipmentProjectionAfterDurabilityDestruction(
            BattleUnitState targetUnit,
            BattleEventBatch batch
        ) => RefreshEquipmentProjectionAfterDurabilityDestruction(targetUnit, batch);

    bool IBattleEquipmentCombatReactionSink.ResolveDamageApplied(
        BattleEquipmentAbilityDamageAppliedContext context
    ) => ResolveDamageApplied(context);

    bool IBattleEquipmentCombatReactionSink.ResolveDamageTakenFinalized(
        BattleEquipmentAbilityDamageAppliedContext context
    ) => ResolveDamageTakenFinalized(context);

    BattleFatalInterceptResult IBattleFatalInterceptArbiter.Resolve(
        BattleFatalInterceptContext context
    ) => ResolveFatalIntercept(context);

    BattleFatalInterceptPreviewResult IBattleFatalInterceptArbiter.Preview(
        BattleFatalInterceptContext context
    ) => PreviewFatalIntercept(context);

    internal BattleDamageResolver DamageResolver => _damageResolver;

    internal Queue<int> ForcedAbilityCheckRollValuesForTests =>
        _forcedAbilityCheckRollValuesForTests;

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

    internal void ConfigureFatalRecoveryValuesForTests(IEnumerable<int> values)
    {
        _forcedFatalRecoveryValuesForTests.Clear();
        if (values == null)
            return;
        foreach (int value in values)
            _forcedFatalRecoveryValuesForTests.Enqueue(Math.Max(value, 0));
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
                    || !_conditionEvaluator.ConditionGroupPasses(
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

    // §8.6：通用 attack-hit reaction。真实攻击检定成功后由 canonical resolver 每目标
    // 调用一次，不要求 weapon damage；旧 on_hit 仍保持 weapon-hit 语义。
    internal BattleEquipmentAbilityAfterHitResult ResolveAttackHit(
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
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnAttackHit
                    || reaction.Timing != EquipmentAbilityTimingKind.AfterHit
                    || !_conditionEvaluator.ConditionGroupPasses(
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
                    || !_conditionEvaluator.ConditionGroupPasses(
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
                        || !_conditionEvaluator.ConditionGroupPasses(
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
                        _abilityStateResolver.ResolveModifyAbilityStateAction(
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
                        List<StringName> consumedUnitIds = _statusActionResolver.ResolveConsumeStatusStacksAction(
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
                    || !_conditionEvaluator.ConditionGroupPasses(
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

    internal bool ResolveTurnEnd(BattleEquipmentAbilityTurnEndContext context)
    {
        bool changed = _summonResolver.RemoveExpiredSummonedUnits(
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
                    || !_conditionEvaluator.ConditionGroupPasses(
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
                        || !_conditionEvaluator.ConditionGroupPasses(
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
                        _abilityStateResolver.ResolveModifyAbilityStateAction(
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
                        List<StringName> consumedUnitIds = _statusActionResolver.ResolveConsumeStatusStacksAction(
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

    internal bool ResolveDamageApplied(BattleEquipmentAbilityDamageAppliedContext context) =>
        ResolveFinalizedDamageReaction(
            context,
            EquipmentAbilityTriggerKind.OnDamageApplied,
            requireSourceAlive: false,
            requireTarget: true
        );

    internal bool ResolveDamageTakenFinalized(
        BattleEquipmentAbilityDamageAppliedContext context
    ) => ResolveFinalizedDamageReaction(
        context,
        EquipmentAbilityTriggerKind.OnDamageTakenFinalized,
        requireSourceAlive: true,
        requireTarget: false
    );

    private bool ResolveFinalizedDamageReaction(
        BattleEquipmentAbilityDamageAppliedContext context,
        EquipmentAbilityTriggerKind trigger,
        bool requireSourceAlive,
        bool requireTarget
    )
    {
        if (
            context == null
            || context.SourceUnit == null
            || (requireTarget && context.TargetUnit == null)
            || (requireSourceAlive && !context.SourceUnit.IsAlive())
            || (context.HpDamage <= 0
                && (trigger != EquipmentAbilityTriggerKind.OnDamageTakenFinalized
                    || context.RawDamage <= 0))
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
                bool rawOnlyEvent = context.HpDamage <= 0 && context.RawDamage > 0;
                int reactionPreviewProbabilityBasisPoints = context.IsPreview
                    ? EstimateRollGateSuccessBasisPoints(reaction?.RollGate)
                    : 10000;
                bool reactionReferencesRawDamage =
                    BattleEquipmentAbilityConditionEvaluator.ConditionGroupReferencesFact(
                        reaction?.ConditionGroup,
                        BattleEquipmentAbilityConditionEvaluator.FactRawDamage
                    );
                if (
                    reaction == null
                    || reaction.Trigger != trigger
                    || reaction.Timing != EquipmentAbilityTimingKind.AfterDamage
                    || (rawOnlyEvent
                        && !reactionReferencesRawDamage
                        && !ReactionActionsReferenceFact(
                            reaction,
                            BattleEquipmentAbilityConditionEvaluator.FactRawDamage
                        ))
                    || !_conditionEvaluator.ConditionGroupPasses(
                        reaction.ConditionGroup,
                        context.SourceUnit,
                        context.TargetUnit,
                        factContext,
                        activeBinding
                    )
                    || (context.IsPreview
                        ? reactionPreviewProbabilityBasisPoints <= 0
                        : !RollGatePasses(
                            reaction.RollGate,
                            binding.BindingId,
                            reaction.ReactionId,
                            "",
                            forcedRollValue: 0,
                            result: null
                        ))
                )
                {
                    continue;
                }

                foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
                {
                    int actionPreviewProbabilityBasisPoints = context.IsPreview
                        ? (int)Math.Clamp(
                            (long)reactionPreviewProbabilityBasisPoints
                                * EstimateRollGateSuccessBasisPoints(action?.RollGate)
                                / 10000L,
                            0L,
                            10000L
                        )
                        : 10000;
                    if (
                        action == null
                        || (rawOnlyEvent
                            && !reactionReferencesRawDamage
                            && !BattleEquipmentAbilityConditionEvaluator
                                .ConditionGroupReferencesFact(
                                    action.ConditionGroup,
                                    BattleEquipmentAbilityConditionEvaluator.FactRawDamage
                                ))
                        || !_conditionEvaluator.ConditionGroupPasses(
                            action.ConditionGroup,
                            context.SourceUnit,
                            context.TargetUnit,
                            factContext,
                            activeBinding
                        )
                        || (context.IsPreview
                            ? actionPreviewProbabilityBasisPoints <= 0
                            : !RollGatePasses(
                                action.RollGate,
                                binding.BindingId,
                                reaction.ReactionId,
                                action.ActionId,
                                forcedRollValue: 0,
                                result: null
                            ))
                    )
                    {
                        continue;
                    }

                    if (context.IsPreview && actionPreviewProbabilityBasisPoints < 10000)
                    {
                        if (
                            action.Kind == ActionKindTriggerSkill
                            && action.PayloadDefinition
                                is TriggerSkillActionPayloadDefinition conditionalSkillPayload
                        )
                        {
                            context.PreviewActionSink?.Invoke(
                                _skillTriggerActionResolver.PreviewTriggerSkillAction(
                                    activeBinding,
                                    binding,
                                    action,
                                    conditionalSkillPayload,
                                    context.SourceUnit,
                                    context.TargetUnit,
                                    context.BattleState,
                                    actionPreviewProbabilityBasisPoints
                                )
                            );
                        }
                        else
                        {
                            bool supportedConditionalAction =
                                action.Kind == ActionKindApplyStatus
                                || action.Kind == ActionKindHealFromFact
                                || action.Kind == ActionKindHeal
                                || action.Kind == ActionKindConsumeStatusStacks;
                            context.PreviewActionSink?.Invoke(
                                new BattleEquipmentAbilityActionPreviewResult
                                {
                                    BindingId = binding.BindingId,
                                    ActionId = action.ActionId,
                                    ActionKind = action.Kind,
                                    TriggerProbabilityBasisPoints =
                                        actionPreviewProbabilityBasisPoints,
                                    Guaranteed = false,
                                    Applied = false,
                                    Conditional = true,
                                    Supported = supportedConditionalAction,
                                    UnsupportedReason = supportedConditionalAction
                                        ? ""
                                        : action.Kind == ActionKindModifyAbilityState
                                            ? "finalized_modify_ability_state_preview_not_projected"
                                            : "finalized_action_kind_preview_not_supported",
                                }
                            );
                        }
                        continue;
                    }

                    bool actionApplied = false;
                    if (
                        action.Kind == ActionKindModifyAbilityState
                        && action.PayloadDefinition is ModifyAbilityStateActionPayloadDefinition statePayload
                    )
                    {
                        if (context.IsPreview && !context.IsBranchLocalProjection)
                        {
                            context.PreviewActionSink?.Invoke(
                                new BattleEquipmentAbilityActionPreviewResult
                                {
                                    BindingId = binding.BindingId,
                                    ActionId = action.ActionId,
                                    ActionKind = action.Kind,
                                    TriggerProbabilityBasisPoints =
                                        actionPreviewProbabilityBasisPoints,
                                    Guaranteed = actionPreviewProbabilityBasisPoints >= 10000,
                                    Applied = false,
                                    Supported = false,
                                    UnsupportedReason =
                                        "finalized_modify_ability_state_preview_not_projected",
                                }
                            );
                            continue;
                        }
                        _abilityStateResolver.ResolveModifyAbilityStateAction(
                            activeBinding,
                            binding,
                            statePayload,
                            context.SourceUnit,
                            context.TargetUnit
                        );
                        changed = true;
                        actionApplied = true;
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
                            _statusActionResolver.ResolveApplyStatusAction(
                                activeBinding.Source,
                                binding,
                                action,
                                statusPayload,
                                context.SourceUnit,
                                actionTarget,
                                context.SaveContext,
                                null
                            );
                            actionApplied = true;
                        }
                        changed = actionApplied || changed;
                    }
                    else if (
                        action.Kind == ActionKindTriggerSkill
                        && action.PayloadDefinition
                            is TriggerSkillActionPayloadDefinition triggerSkillPayload
                    )
                    {
                        if (context.IsPreview)
                        {
                            context.PreviewActionSink?.Invoke(
                                _skillTriggerActionResolver.PreviewTriggerSkillAction(
                                    activeBinding,
                                    binding,
                                    action,
                                    triggerSkillPayload,
                                    context.SourceUnit,
                                    context.TargetUnit,
                                    context.BattleState,
                                    actionPreviewProbabilityBasisPoints
                                )
                            );
                            continue;
                        }
                        actionApplied = _skillTriggerActionResolver.ResolveTriggerSkillAction(
                            activeBinding,
                            binding,
                            action,
                            triggerSkillPayload,
                            context.SourceUnit,
                            context.TargetUnit,
                            context.BattleState,
                            context.Batch,
                            context.SaveContext,
                            addResult: null
                        );
                        changed = actionApplied || changed;
                    }
                    else if (
                        action.Kind == ActionKindHealFromFact
                        && action.PayloadDefinition is HealFromFactActionPayloadDefinition healFromFactPayload
                    )
                    {
                        BattleUnitState changedUnit = _directEffectActionResolver.ResolveHealFromFactAction(
                            activeBinding,
                            binding,
                            healFromFactPayload,
                            context.SourceUnit,
                            context.TargetUnit,
                            context.BattleState,
                            factContext
                        );
                        actionApplied = changedUnit != null;
                        changed = actionApplied || changed;
                    }
                    else if (
                        action.Kind == ActionKindHeal
                        && action.PayloadDefinition is HealActionPayloadDefinition healPayload
                    )
                    {
                        BattleUnitState changedUnit = context.IsPreview
                            ? _directEffectActionResolver.ResolveExpectedHealAction(
                                activeBinding,
                                binding,
                                healPayload,
                                context.SourceUnit,
                                context.TargetUnit,
                                context.BattleState
                            )
                            : _directEffectActionResolver.ResolveHealAction(
                                activeBinding,
                                binding,
                                healPayload,
                                context.SourceUnit,
                                context.TargetUnit,
                                context.BattleState
                            );
                        actionApplied = changedUnit != null;
                        changed = actionApplied || changed;
                    }
                    else if (
                        action.Kind == ActionKindConsumeStatusStacks
                        && action.PayloadDefinition is ConsumeStatusStacksActionPayloadDefinition consumeStacksPayload
                    )
                    {
                        List<StringName> consumedUnitIds = _statusActionResolver.ResolveConsumeStatusStacksAction(
                            consumeStacksPayload,
                            context.SourceUnit,
                            context.TargetUnit,
                            context.BattleState
                        );
                        actionApplied = consumedUnitIds != null && consumedUnitIds.Count > 0;
                        changed = actionApplied || changed;
                    }
                    if (context.IsPreview)
                    {
                        context.PreviewActionSink?.Invoke(
                            new BattleEquipmentAbilityActionPreviewResult
                            {
                                BindingId = binding.BindingId,
                                ActionId = action.ActionId,
                                ActionKind = action.Kind,
                                TriggerProbabilityBasisPoints =
                                    actionPreviewProbabilityBasisPoints,
                                Guaranteed = actionPreviewProbabilityBasisPoints >= 10000,
                                Applied = actionApplied,
                                Supported = true,
                            }
                        );
                    }
                }
            }
        }
        return changed;
    }

    private static bool ReactionActionsReferenceFact(
        EquipmentAbilityReactionDefinition reaction,
        StringName factId
    )
    {
        foreach (
            EquipmentAbilityActionDefinition action
            in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>()
        )
        {
            if (
                BattleEquipmentAbilityConditionEvaluator.ConditionGroupReferencesFact(
                    action?.ConditionGroup,
                    factId
                )
            )
            {
                return true;
            }
        }
        return false;
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
                    || !_conditionEvaluator.ConditionGroupPasses(
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
                        || !_conditionEvaluator.ConditionGroupPasses(
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
                    bool actionApplied = false;
                    if (
                        action.Kind == ActionKindModifyAbilityState
                        && action.PayloadDefinition is ModifyAbilityStateActionPayloadDefinition statePayload
                    )
                    {
                        _abilityStateResolver.ResolveModifyAbilityStateAction(
                            activeBinding,
                            binding,
                            statePayload,
                            context.SourceUnit,
                            context.TargetUnit
                        );
                        changed = true;
                        actionApplied = true;
                    }
                    else if (
                        action.Kind == ActionKindMarkTarget
                        && action.PayloadDefinition is MarkTargetActionPayloadDefinition markPayload
                    )
                    {
                        actionApplied = _targetMarkResolver.ResolveMarkTargetAction(
                                activeBinding,
                                binding,
                                action,
                                markPayload,
                                context
                            );
                        changed = actionApplied || changed;
                    }
                    else if (
                        action.Kind == ActionKindSummonUnits
                        && action.PayloadDefinition is SummonUnitsActionPayloadDefinition summonPayload
                    )
                    {
                        actionApplied = _summonResolver.ResolveSummonUnitsAction(
                                activeBinding,
                                binding,
                                action,
                                summonPayload,
                                context
                            );
                        changed = actionApplied || changed;
                    }
                    else if (
                        action.Kind == ActionKindConsumeSummonedUnits
                        && action.PayloadDefinition is ConsumeSummonedUnitsActionPayloadDefinition consumePayload
                    )
                    {
                        actionApplied = _summonResolver.ResolveConsumeSummonedUnitsAction(
                                activeBinding,
                                binding,
                                consumePayload,
                                context
                            );
                        changed = actionApplied || changed;
                    }
                    else if (
                        action.Kind == ActionKindDealDamage
                        && action.PayloadDefinition is DealDamageActionPayloadDefinition dealDamagePayload
                    )
                    {
                        if (context.IsPreview)
                        {
                            context.PreviewActionSink?.Invoke(
                                new BattleEquipmentAbilityActionPreviewResult
                                {
                                    BindingId = binding.BindingId,
                                    ActionId = action.ActionId,
                                    ActionKind = action.Kind,
                                    TriggerProbabilityBasisPoints = 10000,
                                    Guaranteed = true,
                                    Applied = false,
                                    Supported = false,
                                    UnsupportedReason =
                                        "granted_skill_deal_damage_requires_effect_preview",
                                }
                            );
                            continue;
                        }
                        BattleUnitState changedUnit = _directEffectActionResolver.ResolveDealDamageAction(
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
                            actionApplied = true;
                        }
                    }
                    else if (
                        action.Kind == ActionKindHeal
                        && action.PayloadDefinition is HealActionPayloadDefinition healPayload
                    )
                    {
                        BattleUnitState changedUnit = context.IsPreview
                            ? _directEffectActionResolver.ResolveExpectedHealAction(
                                activeBinding,
                                binding,
                                healPayload,
                                context.SourceUnit,
                                context.TargetUnit,
                                battleState
                            )
                            : _directEffectActionResolver.ResolveHealAction(
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
                            actionApplied = true;
                        }
                    }
                    else if (
                        action.Kind == ActionKindModifyActionPoints
                        && action.PayloadDefinition is ModifyActionPointsActionPayloadDefinition actionPointsPayload
                    )
                    {
                        BattleUnitState changedUnit = _directEffectActionResolver.ResolveModifyActionPointsAction(
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
                            actionApplied = true;
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
                        bool cleared = _statusActionResolver.ResolveClearStatusAction(
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
                            actionApplied = true;
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
                            _statusActionResolver.ResolveApplyStatusAction(
                                activeBinding.Source,
                                binding,
                                action,
                                statusPayload,
                                context.SourceUnit,
                                actionTarget,
                                BattleSaveContext.Empty,
                                null
                            );
                            context.Batch?.AddChangedUnitId(actionTarget?.unit_id ?? "");
                            actionApplied = true;
                        }
                        changed = actionApplied || changed;
                    }
                    else if (
                        action.Kind == ActionKindConsumeStatusStacks
                        && action.PayloadDefinition is ConsumeStatusStacksActionPayloadDefinition consumeStacksPayload
                    )
                    {
                        List<StringName> consumedUnitIds = _statusActionResolver.ResolveConsumeStatusStacksAction(
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
                            actionApplied = true;
                        }
                    }
                    if (context.IsPreview)
                    {
                        context.PreviewActionSink?.Invoke(
                            new BattleEquipmentAbilityActionPreviewResult
                            {
                                BindingId = binding.BindingId,
                                ActionId = action.ActionId,
                                ActionKind = action.Kind,
                                TriggerProbabilityBasisPoints = 10000,
                                Guaranteed = true,
                                Applied = actionApplied,
                                Supported = true,
                            }
                        );
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
                    || !_conditionEvaluator.ConditionGroupPasses(
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
                || !_conditionEvaluator.ConditionGroupPasses(
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
                _statusActionResolver.ResolveApplyStatusAction(activeBinding.Source, binding, action, statusPayload, context, result);
            }
            else if (
                action.Kind == ActionKindScheduleAreaEffect
                && action.PayloadDefinition is ScheduleAreaEffectActionPayloadDefinition areaPayload
            )
            {
                _areaActionResolver.ResolveScheduleAreaEffectAction(binding, action, areaPayload, context);
            }
            else if (
                action.Kind == ActionKindSummonUnits
                && action.PayloadDefinition is SummonUnitsActionPayloadDefinition summonPayload
            )
            {
                _summonResolver.ResolveSummonUnitsAction(
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
                _skillTriggerActionResolver.ResolveTriggerSkillAction(
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
                BattleUnitState changedUnit = _directEffectActionResolver.ResolveModifyActionPointsAction(
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
                _abilityStateResolver.ResolveModifyAbilityStateAction(
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
                _statusActionResolver.ResolveConsumeStatusStacksAction(
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
            || sourceUnit?.IsAlive() != true
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
        if (skillDefinition.CombatProfile.Windup != null)
        {
            context.Batch?.AddLogLine("蓄力技能不能通过装备即时攻击自动触发。");
            return;
        }
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
                    DamageOriginKind = BattleDamageOriginKind.EquipmentTriggeredSkill,
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
            if (targetUnit.IsAlive() != true)
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
        BattleWeaponProjectionValues weaponProjection =
            sourceUnit.GetWeaponProjectionReadViewTyped().Values;
        int weaponAttackRange = Math.Max(weaponProjection.AttackRange, 1);
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate == null
                || candidate.IsAlive() != true
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
                    > weaponAttackRange
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
                || !_conditionEvaluator.ConditionGroupPasses(
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
                _directEffectActionResolver.ResolveEquipmentDurabilityAction(binding, action, durabilityPayload, context, result);
            }
            else if (
                context.ApplyDamageDiceActions
                &&
                action.Kind == ActionKindAddDamageDice
                && action.PayloadDefinition is AddDamageDiceActionPayloadDefinition dicePayload
            )
            {
                _directEffectActionResolver.ResolveAddDamageDiceAction(activeBinding, binding, action, dicePayload, context, result);
            }
            else if (
                action.Kind == ActionKindDealDamage
                && action.PayloadDefinition is DealDamageActionPayloadDefinition dealDamagePayload
            )
            {
                _directEffectActionResolver.ResolveDealDamageAction(
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
                _directEffectActionResolver.ResolveHealAction(
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
                _statusActionResolver.ResolveApplyStatusAction(activeBinding.Source, binding, action, statusPayload, context, result);
            }
            else if (
                action.Kind == ActionKindTriggerSkill
                && action.PayloadDefinition is TriggerSkillActionPayloadDefinition triggerSkillPayload
            )
            {
                _skillTriggerActionResolver.ResolveTriggerSkillAction(
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
                _areaActionResolver.ResolveApplyBattleTerrainEffectAfterCheckAction(
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
                _areaActionResolver.ResolveApplyEdgeFeatureAction(
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
                _abilityStateResolver.ResolveModifyAbilityStateAction(
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
                _directEffectActionResolver.ResolveModifyActionPointsAction(
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
                _statusActionResolver.ResolveClearStatusAction(
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
                _statusActionResolver.ResolveConsumeStatusStacksAction(
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

    internal BattleUnitState ResolveEquipmentActionTarget(
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
            return _targetMarkResolver.ResolveMarkedTarget(
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

    internal bool EquipmentTargetMatchesRequirements(
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

    internal BattleFatalInterceptResult ResolveFatalIntercept(
        BattleFatalInterceptContext context
    )
    {
        if (context?.TargetUnit == null)
            return BattleFatalInterceptResult.None;

        var attempts = new List<BattleFatalInterceptAttemptResult>();
        foreach (ActiveFatalInterceptCandidate candidate in CollectFatalInterceptCandidates(
            context.TargetUnit
        ))
        {
            EquipmentFatalInterceptDefinition intercept = candidate.Intercept;
            if (
                !BattleDeathResolutionRules.CanDeathPreventionBlock(
                    context.DeathContext,
                    intercept.ProtectionPriority
                )
            )
            {
                attempts.Add(
                    BuildFatalInterceptAttempt(
                        candidate,
                        BattleFatalInterceptAttemptOutcomeKind.BlockedByPriority
                    )
                );
                continue;
            }

            if (
                !EquipmentAbilityUsageRuntime.IsFatalInterceptAttemptAvailable(
                    context.TargetUnit,
                    candidate.Source,
                    candidate.Binding,
                    intercept,
                    context.WorldStep
                )
            )
            {
                attempts.Add(
                    BuildFatalInterceptAttempt(
                        candidate,
                        BattleFatalInterceptAttemptOutcomeKind.Unavailable
                    )
                );
                continue;
            }

            bool usageConsumed = false;
            if (intercept.ConsumeOnAttempt)
            {
                usageConsumed = EquipmentAbilityUsageRuntime.TryCommitFatalInterceptAttempt(
                    context.TargetUnit,
                    candidate.Source,
                    candidate.Binding,
                    intercept,
                    context.WorldStep
                );
                if (!usageConsumed)
                {
                    attempts.Add(
                        BuildFatalInterceptAttempt(
                            candidate,
                            BattleFatalInterceptAttemptOutcomeKind.Unavailable
                        )
                    );
                    continue;
                }
            }

            int rolledValue = 0;
            bool rollPassed = true;
            if (intercept.RollGate != null)
            {
                rolledValue = ResolveRollGateValue(0, intercept.RollGate.Roll);
                rollPassed = CompareInt(
                    rolledValue,
                    intercept.RollGate.Compare,
                    intercept.RollGate.Threshold
                );
            }
            if (!rollPassed)
            {
                attempts.Add(
                    BuildFatalInterceptAttempt(
                        candidate,
                        BattleFatalInterceptAttemptOutcomeKind.RollFailed,
                        usageConsumed,
                        rolledValue
                    )
                );
                continue;
            }

            if (!intercept.ConsumeOnAttempt)
            {
                usageConsumed = EquipmentAbilityUsageRuntime.TryCommitFatalInterceptAttempt(
                    context.TargetUnit,
                    candidate.Source,
                    candidate.Binding,
                    intercept,
                    context.WorldStep
                );
                if (!usageConsumed)
                {
                    attempts.Add(
                        BuildFatalInterceptAttempt(
                            candidate,
                            BattleFatalInterceptAttemptOutcomeKind.Unavailable,
                            false,
                            rolledValue
                        )
                    );
                    continue;
                }
            }

            int recoveredHp = ResolveFatalRecoveryHp(context.TargetUnit, intercept);
            if (recoveredHp <= 0)
            {
                attempts.Add(
                    BuildFatalInterceptAttempt(
                        candidate,
                        BattleFatalInterceptAttemptOutcomeKind.RecoveryFailed,
                        usageConsumed,
                        rolledValue
                    )
                );
                continue;
            }

            context.TargetUnit.SetCurrentHp(recoveredHp);
            if (!context.TargetUnit.IsAlive())
            {
                attempts.Add(
                    BuildFatalInterceptAttempt(
                        candidate,
                        BattleFatalInterceptAttemptOutcomeKind.RecoveryFailed,
                        usageConsumed,
                        rolledValue
                    )
                );
                continue;
            }

            ResolveFatalInterceptSuccessActions(candidate, context);

            attempts.Add(
                BuildFatalInterceptAttempt(
                    candidate,
                    BattleFatalInterceptAttemptOutcomeKind.Intercepted,
                    usageConsumed,
                    rolledValue,
                    context.TargetUnit.GetCurrentHp()
                )
            );
            return new BattleFatalInterceptResult
            {
                Intercepted = true,
                WinningBindingId = candidate.Binding.BindingId,
                WinningInterceptId = intercept.InterceptId,
                RecoveredHp = context.TargetUnit.GetCurrentHp(),
                Attempts = attempts.AsReadOnly(),
            };
        }

        return attempts.Count == 0
            ? BattleFatalInterceptResult.None
            : new BattleFatalInterceptResult { Attempts = attempts.AsReadOnly() };
    }

    private void ResolveFatalInterceptSuccessActions(
        ActiveFatalInterceptCandidate candidate,
        BattleFatalInterceptContext context
    )
    {
        if (context?.TargetUnit == null || candidate.Binding == null)
            return;
        var activeBinding = new ActiveEquipmentAbilityBinding(
            candidate.Source,
            candidate.Binding
        );
        foreach (
            EquipmentAbilityActionDefinition action
            in candidate.Intercept?.SuccessActions
                ?? Array.Empty<EquipmentAbilityActionDefinition>()
        )
        {
            if (
                action?.Kind == ActionKindApplyStatus
                && action.PayloadDefinition is ApplyStatusActionPayloadDefinition statusPayload
            )
            {
                foreach (
                    BattleUnitState targetUnit
                    in ResolveApplyStatusTargets(
                        statusPayload.TargetSelector,
                        context.TargetUnit,
                        context.SourceUnit,
                        context.BattleState
                    )
                )
                {
                    _statusActionResolver.ResolveApplyStatusAction(
                        candidate.Source,
                        candidate.Binding,
                        action,
                        statusPayload,
                        context.TargetUnit,
                        targetUnit,
                        context.SaveContext,
                        addResult: null
                    );
                    context.Batch?.AddChangedUnitId(targetUnit.unit_id);
                }
            }
            else if (
                action?.Kind == ActionKindTriggerSkill
                && action.PayloadDefinition is TriggerSkillActionPayloadDefinition skillPayload
            )
            {
                _skillTriggerActionResolver.ResolveTriggerSkillAction(
                    activeBinding,
                    candidate.Binding,
                    action,
                    skillPayload,
                    context.TargetUnit,
                    context.SourceUnit,
                    context.BattleState,
                    context.Batch,
                    context.SaveContext,
                    addResult: null
                );
            }
        }
    }

    internal BattleFatalInterceptPreviewResult PreviewFatalIntercept(
        BattleFatalInterceptContext context
    )
    {
        if (context?.TargetUnit == null)
            return BattleFatalInterceptPreviewResult.None;

        var previews = new List<BattleFatalInterceptCandidatePreview>();
        var successActionPreviews = new List<BattleEquipmentAbilityActionPreviewResult>();
        var candidates = new List<ActiveFatalInterceptCandidate>(
            CollectFatalInterceptCandidates(context.TargetUnit)
        );
        long remainingFailureBasisPoints = 10000;
        long weightedRecoveryBasisPoints = 0;
        foreach (ActiveFatalInterceptCandidate candidate in candidates)
        {
            EquipmentFatalInterceptDefinition intercept = candidate.Intercept;
            bool blocks = BattleDeathResolutionRules.CanDeathPreventionBlock(
                context.DeathContext,
                intercept.ProtectionPriority
            );
            bool available = blocks
                && EquipmentAbilityUsageRuntime.IsFatalInterceptAttemptAvailable(
                    context.TargetUnit,
                    candidate.Source,
                    candidate.Binding,
                    intercept,
                    context.WorldStep
                );
            int successBasisPoints = available
                ? EstimateRollGateSuccessBasisPoints(intercept.RollGate)
                : 0;
            int expectedRecoveryHp = EstimateFatalRecoveryHp(
                context.TargetUnit,
                intercept
            );
            if (expectedRecoveryHp <= 0)
                successBasisPoints = 0;
            int reachProbabilityBasisPoints = (int)Math.Clamp(
                remainingFailureBasisPoints,
                0L,
                10000L
            );
            int contributionProbabilityBasisPoints = available
                ? (int)Math.Clamp(
                    remainingFailureBasisPoints * successBasisPoints / 10000,
                    0L,
                    10000L
                )
                : 0;
            IReadOnlyList<BattleEquipmentAbilityActionPreviewResult> candidateActions =
                PreviewFatalInterceptSuccessActions(
                    candidate,
                    context,
                    contributionProbabilityBasisPoints,
                    expectedRecoveryHp
                );
            successActionPreviews.AddRange(candidateActions);
            previews.Add(
                new BattleFatalInterceptCandidatePreview
                {
                    SourceEquipmentInstanceId = candidate.Source.SourceEquipmentInstanceId,
                    BindingId = candidate.Binding.BindingId,
                    InterceptId = intercept.InterceptId,
                    ResolutionOrder = intercept.ResolutionOrder,
                    ProtectionPriority = intercept.ProtectionPriority,
                    Available = available,
                    BlocksDeathSource = blocks,
                    ReachProbabilityBasisPoints = reachProbabilityBasisPoints,
                    SuccessProbabilityBasisPoints = successBasisPoints,
                    ContributionProbabilityBasisPoints =
                        contributionProbabilityBasisPoints,
                    ExpectedRecoveryHp = expectedRecoveryHp,
                    SuccessActionPreviews = candidateActions,
                }
            );
            if (!available || successBasisPoints <= 0)
                continue;

            long successPathBasisPoints =
                remainingFailureBasisPoints * successBasisPoints / 10000;
            weightedRecoveryBasisPoints += successPathBasisPoints * expectedRecoveryHp;
            remainingFailureBasisPoints =
                remainingFailureBasisPoints * (10000 - successBasisPoints) / 10000;
        }

        int interceptProbabilityBasisPoints = (int)Math.Clamp(
            10000 - remainingFailureBasisPoints,
            0L,
            10000L
        );
        return previews.Count == 0
            ? BattleFatalInterceptPreviewResult.None
            : new BattleFatalInterceptPreviewResult
            {
                InterceptProbabilityBasisPoints = interceptProbabilityBasisPoints,
                GuaranteedIntercept = interceptProbabilityBasisPoints >= 10000,
                ExpectedRecoveryHp = (int)Math.Max(
                    Math.Round(weightedRecoveryBasisPoints / 10000.0),
                    0.0
                ),
                ExpectedSurvivalHp = (int)Math.Max(
                    Math.Round(weightedRecoveryBasisPoints / 10000.0),
                    0.0
                ),
                SuccessActionPreviews = successActionPreviews.AsReadOnly(),
                Candidates = previews.AsReadOnly(),
                ContinuationBranches = context.IsDetachedPreview
                    ? BuildFatalInterceptPreviewBranches(context, candidates)
                    : Array.Empty<BattleFatalInterceptPreviewBranch>(),
            };
    }

    private IReadOnlyList<BattleFatalInterceptPreviewBranch>
        BuildFatalInterceptPreviewBranches(
            BattleFatalInterceptContext context,
            IReadOnlyList<ActiveFatalInterceptCandidate> candidates
        )
    {
        BattleFatalInterceptPreviewBranch initial = CloneFatalPreviewBranch(
            context?.BattleState,
            context?.SourceUnit,
            context?.TargetUnit,
            probabilityBasisPoints: 10000
        );
        if (initial == null)
            return Array.Empty<BattleFatalInterceptPreviewBranch>();

        var activeFailureBranches = new List<BattleFatalInterceptPreviewBranch>
        {
            initial,
        };
        var terminalBranches = new List<BattleFatalInterceptPreviewBranch>();
        foreach (
            ActiveFatalInterceptCandidate candidate
            in candidates ?? Array.Empty<ActiveFatalInterceptCandidate>()
        )
        {
            var nextFailureBranches = new List<BattleFatalInterceptPreviewBranch>();
            foreach (BattleFatalInterceptPreviewBranch active in activeFailureBranches)
            {
                BattleUnitState branchTarget = active.TargetUnit;
                EquipmentFatalInterceptDefinition intercept = candidate.Intercept;
                bool blocks = BattleDeathResolutionRules.CanDeathPreventionBlock(
                    context.DeathContext,
                    intercept.ProtectionPriority
                );
                bool available = blocks
                    && EquipmentAbilityUsageRuntime.IsFatalInterceptAttemptAvailable(
                        branchTarget,
                        candidate.Source,
                        candidate.Binding,
                        intercept,
                        context.WorldStep
                    );
                if (!available)
                {
                    nextFailureBranches.Add(active);
                    continue;
                }

                int successProbability = EstimateRollGateSuccessBasisPoints(
                    intercept.RollGate
                );
                int expectedRecoveryHp = EstimateFatalRecoveryHp(
                    branchTarget,
                    intercept
                );
                if (expectedRecoveryHp <= 0)
                    successProbability = 0;
                successProbability = Math.Clamp(successProbability, 0, 10000);
                int successPathProbability = (int)Math.Clamp(
                    Math.Round(
                        active.ProbabilityBasisPoints
                            * successProbability
                            / 10000.0
                    ),
                    0.0,
                    active.ProbabilityBasisPoints
                );
                int failurePathProbability = Math.Max(
                    active.ProbabilityBasisPoints - successPathProbability,
                    0
                );

                if (failurePathProbability > 0)
                {
                    BattleFatalInterceptPreviewBranch failure = CloneFatalPreviewBranch(
                        active.BattleState,
                        active.SourceUnit,
                        active.TargetUnit,
                        failurePathProbability
                    );
                    if (
                        failure != null
                        && (
                            !intercept.ConsumeOnAttempt
                            || TryCommitFatalPreviewAttempt(
                                failure.TargetUnit,
                                candidate,
                                context.WorldStep
                            )
                        )
                    )
                    {
                        nextFailureBranches.Add(failure);
                    }
                    else
                    {
                        nextFailureBranches.Add(active);
                    }
                }

                if (successPathProbability <= 0)
                    continue;
                BattleFatalInterceptPreviewBranch success = CloneFatalPreviewBranch(
                    active.BattleState,
                    active.SourceUnit,
                    active.TargetUnit,
                    successPathProbability
                );
                if (
                    success == null
                    || !TryCommitFatalPreviewAttempt(
                        success.TargetUnit,
                        candidate,
                        context.WorldStep
                    )
                )
                {
                    BattleFatalInterceptPreviewBranch unavailable = CloneFatalPreviewBranch(
                        active.BattleState,
                        active.SourceUnit,
                        active.TargetUnit,
                        successPathProbability
                    );
                    if (unavailable != null)
                        nextFailureBranches.Add(unavailable);
                    continue;
                }

                success.TargetUnit.SetCurrentHp(expectedRecoveryHp);
                var successContext = new BattleFatalInterceptContext
                {
                    SourceUnit = success.SourceUnit,
                    TargetUnit = success.TargetUnit,
                    BattleState = success.BattleState,
                    DeathContext = context.DeathContext,
                    HpBefore = context.HpBefore,
                    HpDamage = context.HpDamage,
                    ProjectedHp = context.ProjectedHp,
                    WorldStep = context.WorldStep,
                    Batch = null,
                    SaveContext = context.SaveContext,
                    IsDetachedPreview = true,
                    DetachedPreviewDepth = context.DetachedPreviewDepth,
                };
                PreviewFatalInterceptSuccessActions(
                    candidate,
                    successContext,
                    triggerProbabilityBasisPoints: 10000,
                    expectedRecoveryHp
                );
                terminalBranches.Add(
                    new BattleFatalInterceptPreviewBranch
                    {
                        ProbabilityBasisPoints = success.ProbabilityBasisPoints,
                        BattleState = success.BattleState,
                        SourceUnit = success.SourceUnit,
                        TargetUnit = success.TargetUnit,
                        Intercepted = true,
                        WinningBindingId = candidate.Binding.BindingId,
                        WinningInterceptId = intercept.InterceptId,
                    }
                );
            }
            activeFailureBranches = nextFailureBranches;
        }

        foreach (BattleFatalInterceptPreviewBranch failure in activeFailureBranches)
        {
            failure.TargetUnit.MarkDead();
            terminalBranches.Add(failure);
        }
        return terminalBranches.AsReadOnly();
    }

    private static BattleFatalInterceptPreviewBranch CloneFatalPreviewBranch(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int probabilityBasisPoints
    )
    {
        if (targetUnit == null || probabilityBasisPoints <= 0)
            return null;
        BattleDetachedPreviewState detached = BattleDetachedPreviewState.Create(
            state,
            sourceUnit,
            targetUnit
        );
        BattleUnitState targetClone = detached.GetUnit(targetUnit.unit_id);
        if (targetClone == null)
            return null;
        BattleUnitState sourceClone = sourceUnit != null
            ? detached.GetUnit(sourceUnit.unit_id)
            : null;
        return new BattleFatalInterceptPreviewBranch
        {
            ProbabilityBasisPoints = Math.Clamp(probabilityBasisPoints, 0, 10000),
            BattleState = detached.State,
            SourceUnit = sourceClone,
            TargetUnit = targetClone,
        };
    }

    private static bool TryCommitFatalPreviewAttempt(
        BattleUnitState targetUnit,
        ActiveFatalInterceptCandidate candidate,
        int worldStep
    ) =>
        EquipmentAbilityUsageRuntime.TryCommitFatalInterceptAttempt(
            targetUnit,
            candidate.Source,
            candidate.Binding,
            candidate.Intercept,
            worldStep
        );

    private IReadOnlyList<BattleEquipmentAbilityActionPreviewResult>
        PreviewFatalInterceptSuccessActions(
            ActiveFatalInterceptCandidate candidate,
            BattleFatalInterceptContext context,
            int triggerProbabilityBasisPoints,
            int expectedRecoveryHp
        )
    {
        var results = new List<BattleEquipmentAbilityActionPreviewResult>();
        int probability = Math.Clamp(triggerProbabilityBasisPoints, 0, 10000);
        bool canApply =
            context?.IsDetachedPreview == true
            && probability >= 10000
            && expectedRecoveryHp > 0;
        if (canApply)
            context.TargetUnit.SetCurrentHp(expectedRecoveryHp);

        var activeBinding = new ActiveEquipmentAbilityBinding(
            candidate.Source,
            candidate.Binding
        );
        foreach (
            EquipmentAbilityActionDefinition action
            in candidate.Intercept?.SuccessActions
                ?? Array.Empty<EquipmentAbilityActionDefinition>()
        )
        {
            if (
                action?.Kind == ActionKindApplyStatus
                && action.PayloadDefinition is ApplyStatusActionPayloadDefinition statusPayload
            )
            {
                bool applied = false;
                if (canApply)
                {
                    foreach (
                        BattleUnitState targetUnit
                        in ResolveApplyStatusTargets(
                            statusPayload.TargetSelector,
                            context.TargetUnit,
                            context.SourceUnit,
                            context.BattleState
                        )
                    )
                    {
                        _statusActionResolver.ResolveApplyStatusAction(
                            candidate.Source,
                            candidate.Binding,
                            action,
                            statusPayload,
                            context.TargetUnit,
                            targetUnit,
                            context.SaveContext,
                            addResult: null
                        );
                        applied = true;
                    }
                }
                results.Add(
                    new BattleEquipmentAbilityActionPreviewResult
                    {
                        BindingId = candidate.Binding.BindingId,
                        ActionId = action.ActionId,
                        ActionKind = action.Kind,
                        TriggerProbabilityBasisPoints = probability,
                        Guaranteed = probability >= 10000,
                        Applied = applied,
                        Conditional = probability > 0 && probability < 10000,
                        Supported = true,
                    }
                );
            }
            else if (
                action?.Kind == ActionKindTriggerSkill
                && action.PayloadDefinition is TriggerSkillActionPayloadDefinition skillPayload
            )
            {
                results.Add(
                    _skillTriggerActionResolver.PreviewTriggerSkillAction(
                        activeBinding,
                        candidate.Binding,
                        action,
                        skillPayload,
                        context.TargetUnit,
                        context.SourceUnit,
                        context.BattleState,
                        probability,
                        context.DetachedPreviewDepth
                    )
                );
            }
            else if (action != null)
            {
                results.Add(
                    new BattleEquipmentAbilityActionPreviewResult
                    {
                        BindingId = candidate.Binding.BindingId,
                        ActionId = action.ActionId,
                        ActionKind = action.Kind,
                        TriggerProbabilityBasisPoints = probability,
                        Guaranteed = probability >= 10000,
                        Conditional = probability > 0 && probability < 10000,
                        Supported = false,
                        UnsupportedReason = "fatal_success_action_kind_not_supported",
                    }
                );
            }
        }
        return results.AsReadOnly();
    }

    private int ResolveFatalRecoveryHp(
        BattleUnitState targetUnit,
        EquipmentFatalInterceptDefinition intercept
    )
    {
        if (targetUnit == null || intercept == null)
            return 0;
        if (intercept.RecoveryKind == EquipmentFatalInterceptRecoveryKind.MaxHpPercent)
            return ResolveMaxHpPercentRecovery(targetUnit, intercept.RecoveryPercentBasisPoints);
        if (_forcedFatalRecoveryValuesForTests.Count > 0)
            return _forcedFatalRecoveryValuesForTests.Dequeue();
        return RollDiceExpression(intercept.RecoveryDice);
    }

    private static int EstimateFatalRecoveryHp(
        BattleUnitState targetUnit,
        EquipmentFatalInterceptDefinition intercept
    )
    {
        if (targetUnit == null || intercept == null)
            return 0;
        if (intercept.RecoveryKind == EquipmentFatalInterceptRecoveryKind.MaxHpPercent)
            return ResolveMaxHpPercentRecovery(targetUnit, intercept.RecoveryPercentBasisPoints);
        if (intercept.RecoveryDice == null)
            return 0;
        double total = Math.Max(intercept.RecoveryDice.FlatBonus, 0);
        foreach (
            DiceExpressionTermDefinition term
            in intercept.RecoveryDice.Terms ?? Array.Empty<DiceExpressionTermDefinition>()
        )
        {
            if (term == null || term.DiceCount <= 0 || term.DiceSides <= 0)
                continue;
            total += term.DiceCount * (term.DiceSides + 1) / 2.0;
        }
        return Math.Max((int)Math.Round(total), 0);
    }

    private static int ResolveMaxHpPercentRecovery(
        BattleUnitState targetUnit,
        int recoveryPercentBasisPoints
    )
    {
        int maxHp = Math.Max(
            targetUnit?.attribute_snapshot?.GetValue(AttributeService.HP_MAX) ?? 0,
            1
        );
        int basisPoints = Math.Clamp(recoveryPercentBasisPoints, 0, 10000);
        long recovered = ((long)maxHp * basisPoints + 9999L) / 10000L;
        return (int)Math.Clamp(recovered, 0L, maxHp);
    }

    private static int EstimateRollGateSuccessBasisPoints(
        EquipmentRollGateDefinition rollGate
    )
    {
        if (rollGate == null)
            return 10000;
        DiceExpressionDefinition dice = rollGate.Roll;
        if (dice == null)
            return 0;

        int totalDice = 0;
        int maximum = Math.Max(dice.FlatBonus, 0);
        foreach (
            DiceExpressionTermDefinition term
            in dice.Terms ?? Array.Empty<DiceExpressionTermDefinition>()
        )
        {
            long proposedMaximum =
                (long)maximum + (long)(term?.DiceCount ?? 0) * (term?.DiceSides ?? 0);
            if (
                term == null
                || term.DiceCount <= 0
                || term.DiceSides <= 0
                || (long)totalDice + term.DiceCount > 64L
                || proposedMaximum > 10000L
            )
            {
                return 0;
            }
            totalDice += term.DiceCount;
            maximum = (int)proposedMaximum;
        }
        if (totalDice <= 0)
            return 0;

        double[] distribution = new double[maximum + 1];
        int baseValue = Math.Max(dice.FlatBonus, 0);
        distribution[baseValue] = 1.0;
        int currentMaximum = baseValue;
        foreach (
            DiceExpressionTermDefinition term
            in dice.Terms ?? Array.Empty<DiceExpressionTermDefinition>()
        )
        {
            for (int dieIndex = 0; dieIndex < term.DiceCount; dieIndex++)
            {
                var next = new double[maximum + 1];
                for (int value = 0; value <= currentMaximum; value++)
                {
                    double probability = distribution[value];
                    if (probability <= 0.0)
                        continue;
                    for (int face = 1; face <= term.DiceSides; face++)
                        next[value + face] += probability / term.DiceSides;
                }
                currentMaximum += term.DiceSides;
                distribution = next;
            }
        }

        double successProbability = 0.0;
        for (int value = 0; value <= currentMaximum; value++)
        {
            if (CompareInt(value, rollGate.Compare, rollGate.Threshold))
                successProbability += distribution[value];
        }
        return Math.Clamp((int)Math.Round(successProbability * 10000.0), 0, 10000);
    }

    private IEnumerable<ActiveFatalInterceptCandidate> CollectFatalInterceptCandidates(
        BattleUnitState targetUnit
    )
    {
        var result = new List<ActiveFatalInterceptCandidate>();
        foreach (ActiveEquipmentAbilityBinding activeBinding in CollectActiveBindings(targetUnit))
        {
            foreach (
                EquipmentFatalInterceptDefinition intercept
                in activeBinding.Binding?.FatalIntercepts
                    ?? Array.Empty<EquipmentFatalInterceptDefinition>()
            )
            {
                if (intercept != null)
                {
                    result.Add(
                        new ActiveFatalInterceptCandidate(
                            activeBinding.Source,
                            activeBinding.Binding,
                            intercept
                        )
                    );
                }
            }
        }
        result.Sort(CompareFatalInterceptCandidates);
        return result;
    }

    private static int CompareFatalInterceptCandidates(
        ActiveFatalInterceptCandidate left,
        ActiveFatalInterceptCandidate right
    )
    {
        int orderCompare = left.Intercept.ResolutionOrder.CompareTo(
            right.Intercept.ResolutionOrder
        );
        if (orderCompare != 0)
            return orderCompare;
        int bindingCompare = string.CompareOrdinal(
            left.Binding.BindingId.ToString(),
            right.Binding.BindingId.ToString()
        );
        if (bindingCompare != 0)
            return bindingCompare;
        int sourceCompare = string.CompareOrdinal(
            left.Source.EffectiveInstanceKey.ToString(),
            right.Source.EffectiveInstanceKey.ToString()
        );
        if (sourceCompare != 0)
            return sourceCompare;
        return string.CompareOrdinal(
            left.Intercept.InterceptId.ToString(),
            right.Intercept.InterceptId.ToString()
        );
    }

    private static BattleFatalInterceptAttemptResult BuildFatalInterceptAttempt(
        ActiveFatalInterceptCandidate candidate,
        BattleFatalInterceptAttemptOutcomeKind outcome,
        bool usageConsumed = false,
        int rolledValue = 0,
        int recoveredHp = 0
    ) =>
        new()
        {
            SourceEquipmentInstanceId = candidate.Source.SourceEquipmentInstanceId,
            BindingId = candidate.Binding.BindingId,
            InterceptId = candidate.Intercept.InterceptId,
            ResolutionOrder = candidate.Intercept.ResolutionOrder,
            ProtectionPriority = candidate.Intercept.ProtectionPriority,
            Outcome = outcome,
            UsageConsumed = usageConsumed,
            RolledValue = rolledValue,
            RecoveredHp = Math.Max(recoveredHp, 0),
        };

    internal bool RollGatePasses(
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

    internal static int RollDiceExpression(DiceExpressionDefinition dice)
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

    internal static string ResolveActionLabel(
        EquipmentAbilityBindingDefinition binding,
        string actionLabel
    )
    {
        string label = actionLabel?.StripEdges() ?? "";
        if (label.Length > 0)
            return label;
        return binding?.BindingId.ToString() ?? "";
    }

    internal static bool CompareInt(int value, StringName compare, int threshold)
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

    internal static BattleUnitState ResolveSubject(
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

    internal static IEnumerable<BattleUnitState> ResolveApplyStatusTargets(
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

    internal IEnumerable<ActiveEquipmentAbilityBinding> CollectActiveBindings(BattleUnitState sourceUnit)
    {
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex =
            _runtime?.GetEquipmentAbilityBindingIndexTyped();
        BattleEquipmentAbilitySourceListReadView sources =
            sourceUnit?.GetEquipmentAbilitySourcesReadViewTyped()
            ?? new BattleEquipmentAbilitySourceListReadView(
                null
            );
        if (sourceUnit == null || bindingIndex == null || bindingIndex.Count == 0)
        {
            yield break;
        }
        var result = new List<ActiveEquipmentAbilityBinding>();
        if (sources.IsPresent)
        {
            foreach (BattleEquipmentAbilitySourceReadView source in sources)
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
                        && binding.ActivationStatusId == ""
                    )
                    {
                        result.Add(new ActiveEquipmentAbilityBinding(source, binding));
                    }
                }
            }
        }

        foreach (EquipmentAbilityBindingDefinition binding in bindingIndex.Values)
        {
            if (
                binding == null
                || binding.BindingId == ""
                || binding.ActivationStatusId == ""
                || !sourceUnit.HasStatusEffect(binding.ActivationStatusId)
            )
            {
                continue;
            }
            var statusSource = new BattleEquipmentAbilitySourceState
            {
                EffectiveInstanceKey = new StringName(
                    $"battle_status:{sourceUnit.unit_id}:{binding.BindingId}"
                ),
                EquipmentDefId = new StringName(
                    $"battle_status:{binding.ActivationStatusId}"
                ),
                SourceEquipmentInstanceId = "",
                SourceKind = EquipmentAbilitySourceKind.BattleStatusDerived,
                AbilityIds = new List<StringName> { binding.BindingId },
            };
            result.Add(
                new ActiveEquipmentAbilityBinding(
                    new BattleEquipmentAbilitySourceReadView(statusSource),
                    binding
                )
            );
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

    internal ItemDefinition ResolveItemDef(StringName itemId)
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

    internal static bool AllTagsPresent(
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

    internal static bool AnyTagPresent(
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

    internal readonly record struct ActiveEquipmentAbilityBinding(
        BattleEquipmentAbilitySourceReadView Source,
        EquipmentAbilityBindingDefinition Binding
    );

    private readonly record struct ActiveMovementTrailCandidate(
        BattleEquipmentAbilitySourceReadView Source,
        EquipmentAbilityBindingDefinition Binding,
        EquipmentMovementTrailDefinition Trail
    );

    private static int CompareMovementTrailCandidate(
        ActiveMovementTrailCandidate left,
        ActiveMovementTrailCandidate right
    )
    {
        int priorityOrder = right.Trail.Priority.CompareTo(left.Trail.Priority);
        if (priorityOrder != 0)
            return priorityOrder;
        int bindingOrder = string.CompareOrdinal(
            left.Binding.BindingId.ToString(),
            right.Binding.BindingId.ToString()
        );
        if (bindingOrder != 0)
            return bindingOrder;
        int trailOrder = string.CompareOrdinal(
            left.Trail.TrailId.ToString(),
            right.Trail.TrailId.ToString()
        );
        if (trailOrder != 0)
            return trailOrder;
        return string.CompareOrdinal(
            left.Source?.EffectiveInstanceKey.ToString() ?? "",
            right.Source?.EffectiveInstanceKey.ToString() ?? ""
        );
    }

    private readonly record struct ActiveFatalInterceptCandidate(
        BattleEquipmentAbilitySourceReadView Source,
        EquipmentAbilityBindingDefinition Binding,
        EquipmentFatalInterceptDefinition Intercept
    );
}
