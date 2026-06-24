using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleTerrainEffectSystem : IDisposable
{
    private static readonly StringName StackBehaviorRefresh = "refresh";
    private static readonly StringName StackBehaviorStack = "stack";
    private static readonly StringName StackBehaviorIgnoreExisting = "ignore_existing";
    private const int TuGranularity = 5;

    private WeakReference<BattleRuntimeModule> _runtimeRef = null;
    private readonly GodotTransientResourceScope _transientScope =
        new("BattleTerrainEffectSystem");
    private readonly RuntimeSkillDefFactory _runtimeSkillFactory;

    internal BattleTerrainEffectSystem()
    {
        _runtimeSkillFactory = new RuntimeSkillDefFactory(
            _transientScope,
            "BattleTerrainEffectSystem"
        );
    }

    private BattleRuntimeModule _ResolveRuntime()
    {
        if (_runtimeRef == null)
            return null;
        _runtimeRef.TryGetTarget(out var runtime);
        return runtime;
    }

    public void Setup(BattleRuntimeModule runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _transientScope.Drain();
        _runtimeRef = null;
    }

    public int GetMoveCostDeltaForUnitTarget(BattleUnitState unitState, Vector2I targetCoord)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || unitState == null)
            return 0;

        BattleState state = runtime.GetState();
        BattleGridService gridService = runtime.GetGridService();
        if (state == null || gridService == null)
            return 0;

        int maxDelta = 0;
        var targetCoords = gridService.GetUnitTargetCoords(unitState, targetCoord);
        foreach (Vector2I coord in targetCoords)
        {
            BattleCellState cell = gridService.GetCellState(state, coord);
            if (cell == null || cell.timed_terrain_effects.Count == 0)
                continue;

            foreach (var effectState in cell.timed_terrain_effects)
            {
                int moveCostDelta = _GetTimedTerrainMoveCostDelta(effectState);
                if (moveCostDelta <= 0)
                    continue;

                var sourceUnit =
                    effectState.source_unit_id != ""
                        ? GetUnit(state, effectState.source_unit_id)
                        : null;
                if (
                    !BattleTargetTeamRules.IsUnitValidForFilter(
                        sourceUnit,
                        unitState,
                        effectState.target_team_filter
                    )
                )
                    continue;
                if (_IsBlockedByNonstackingStatus(unitState, effectState))
                    continue;

                maxDelta = Math.Max(maxDelta, moveCostDelta);
            }
        }
        return maxDelta;
    }

    public int GetMoveCostDeltaForUnitTarget(BattleUnitReadView unitView, Vector2I targetCoord)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || !unitView.IsValid)
            return 0;

        BattleState state = runtime.GetState();
        BattleGridService gridService = runtime.GetGridService();
        if (state == null || gridService == null)
            return 0;

        int maxDelta = 0;
        var targetCoords = gridService.GetUnitTargetCoords(unitView, targetCoord);
        foreach (Vector2I coord in targetCoords)
        {
            BattleCellState cell = gridService.GetCellState(state, coord);
            if (cell == null || cell.timed_terrain_effects.Count == 0)
                continue;

            foreach (var effectState in cell.timed_terrain_effects)
            {
                int moveCostDelta = _GetTimedTerrainMoveCostDelta(effectState);
                if (moveCostDelta <= 0)
                    continue;

                var sourceUnit =
                    effectState.source_unit_id != ""
                        ? GetUnit(state, effectState.source_unit_id)
                        : null;
                if (
                    !BattleTargetTeamRules.IsUnitValidForFilter(
                        sourceUnit,
                        unitView,
                        effectState.target_team_filter
                    )
                )
                    continue;
                if (_IsBlockedByNonstackingStatus(unitView, effectState))
                    continue;

                maxDelta = Math.Max(maxDelta, moveCostDelta);
            }
        }
        return maxDelta;
    }

    public bool UpsertTimedTerrainEffect(
        Vector2I effectCoord,
        BattleUnitState sourceUnit,
        SkillDef skillDef,
        CombatEffectDef effectDef,
        StringName fieldInstanceId
    )
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return false;

        BattleState state = runtime.GetState();
        BattleGridService gridService = runtime.GetGridService();
        if (
            state == null
            || gridService == null
            || effectDef == null
            || effectDef.terrain_effect_id == ""
        )
            return false;

        BattleCellState cell = gridService.GetCellState(state, effectCoord);
        if (cell == null)
            return false;

        var normalizedBehavior = _NormalizeStackBehavior(effectDef.stack_behavior);
        int existingIndex = -1;
        for (int i = 0; i < cell.timed_terrain_effects.Count; i++)
        {
            var existingEffect = cell.timed_terrain_effects[i];
            if (existingEffect != null && existingEffect.effect_id == effectDef.terrain_effect_id)
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            if (normalizedBehavior == StackBehaviorIgnoreExisting)
                return false;
            if (normalizedBehavior == StackBehaviorRefresh)
            {
                var refreshedEffect = _BuildTimedTerrainEffect(
                    sourceUnit,
                    skillDef,
                    effectDef,
                    fieldInstanceId
                );
                if (refreshedEffect == null)
                    return false;
                cell.timed_terrain_effects[existingIndex] = refreshedEffect;
                return true;
            }
        }

        var newEffect = _BuildTimedTerrainEffect(sourceUnit, skillDef, effectDef, fieldInstanceId);
        if (newEffect == null)
            return false;
        cell.timed_terrain_effects.Add(newEffect);
        return true;
    }

    public void ProcessTimedTerrainEffects(BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;

        BattleState state = runtime.GetState();
        BattleGridService gridService = runtime.GetGridService();
        BattleTimelineState timeline = state?.timeline;
        if (state == null || timeline == null || gridService == null)
            return;

        var processedTickKeys = new HashSet<string>();
        foreach (BattleState.BattleCellEntry entry in state.CellEntries())
        {
            var coord = entry.Coord;
            var cell = entry.Cell;
            if (cell == null || cell.timed_terrain_effects.Count == 0)
                continue;

            var retainedEffects = new Godot.Collections.Array<BattleTerrainEffectState>();
            bool cellChanged = false;
            foreach (var effectState in cell.timed_terrain_effects)
            {
                if (effectState == null)
                {
                    cellChanged = true;
                    continue;
                }
                if (_IsBattleLifetimeEffect(effectState))
                {
                    retainedEffects.Add(effectState);
                    continue;
                }

                int currentTu = timeline.current_tu;
                while (
                    effectState.remaining_tu > 0
                    && effectState.tick_interval_tu > 0
                    && currentTu >= effectState.next_tick_at_tu
                )
                {
                    ApplyTimedTerrainEffectTick(coord, effectState, processedTickKeys, batch);
                    effectState.remaining_tu = Math.Max(
                        effectState.remaining_tu - effectState.tick_interval_tu,
                        0
                    );
                    effectState.next_tick_at_tu += effectState.tick_interval_tu;
                    cellChanged = true;
                }

                if (effectState.remaining_tu > 0)
                    retainedEffects.Add(effectState);
                else
                    cellChanged = true;
            }

            if (cellChanged)
            {
                cell.timed_terrain_effects = retainedEffects;
                runtime.AppendChangedCoord(batch, coord);
            }
        }
    }

    public void ApplyTimedTerrainEffectTick(
        Vector2I targetCoord,
        BattleTerrainEffectState effectState,
        HashSet<string> processedTickKeys,
        BattleEventBatch batch
    )
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;

        if (
            effectState != null
            && (
                effectState.RuntimeEffectKind == BattleTerrainEffectRuntimeKind.MovementCost
                || effectState.RuntimeEffectKind == BattleTerrainEffectRuntimeKind.None
            )
        )
            return;

        BattleState state = runtime.GetState();
        BattleGridService gridService = runtime.GetGridService();
        BattleDamageResolver damageResolver = runtime.GetDamageResolver();
        if (
            state == null
            || effectState == null
            || processedTickKeys == null
            || gridService == null
            || damageResolver == null
        )
            return;

        BattleCellState cell = gridService.GetCellState(state, targetCoord);
        if (cell == null || cell.occupant_unit_id == "")
            return;

        var targetUnit = GetUnit(state, cell.occupant_unit_id);
        if (targetUnit == null || !targetUnit.is_alive)
            return;
        // 静滞单位不结算普通 terrain tick。
        if (BattleTemporalStatusService.HasTimeStasis(targetUnit))
            return;

        var sourceUnit =
            effectState.source_unit_id != ""
                ? GetUnit(state, effectState.source_unit_id)
                : null;
        if (
            !BattleTargetTeamRules.IsUnitValidForFilter(
                sourceUnit,
                targetUnit,
                effectState.target_team_filter
            )
        )
            return;

        var tickKey =
            $"{effectState.field_instance_id}|{targetUnit.unit_id}|{effectState.next_tick_at_tu}";
        if (processedTickKeys.Contains(tickKey))
            return;
        processedTickKeys.Add(tickKey);

        CombatEffectDef tempEffect = _runtimeSkillFactory.NewEffect(
            effect =>
            {
                effect.effect_type = effectState.effect_type;
                effect.power = effectState.power;
                effect.damage_tag = effectState.damage_tag;
                effect.status_id = effectState.applied_status_id;
                effect.applied_status_duration_tu = effectState.applied_status_duration_tu;
                effect.duration_tu = effectState.applied_status_duration_tu;
                effect.@params = BattleTerrainEffectState.CopyResidualParams(effectState.@params);
            },
            $"tick_effect:{effectState.effect_id}"
        );

        AttackEffectResolutionResult damageResult = damageResolver.ResolveEffects(
            sourceUnit,
            targetUnit,
            new GArray { tempEffect },
            new GDictionary { ["skill_id"] = effectState.source_skill_id }
        );
        if (!damageResult.Applied)
            return;

        var statusEffectIds = damageResult.StatusEffectIds;
        runtime.MarkAppliedStatusesForTurnTiming(targetUnit, statusEffectIds);
        runtime.AppendResultSourceStatusEffects(batch, sourceUnit, damageResult);
        runtime.AppendChangedUnitId(batch, targetUnit.unit_id);
        runtime.AppendChangedUnitCoords(batch, targetUnit);

        int damage = damageResult.Damage;
        int healing = damageResult.Healing;
        int killCount = 0;

        if (damageResult.HasDamageEvent)
        {
            if (damage > 0)
            {
                var damageLine =
                    $"{targetUnit.display_name} 受到 {_GetTimedTerrainEffectDisplayName(effectState)} 的 {damage} 点伤害";
                if (damageResult.AnyDouble)
                    damageLine += "（触发易伤）";
                else if (damageResult.AnyHalf)
                    damageLine += "（减半后结算）";
                runtime.AppendBatchLog(batch, $"{damageLine}。");
                if (damageResult.ShieldAbsorbed > 0)
                {
                    runtime.AppendBatchLog(
                        batch,
                        $"{targetUnit.display_name} 的护盾吸收了 {damageResult.ShieldAbsorbed} 点伤害。"
                    );
                }
            }
            else
            {
                if (damageResult.AnyImmune)
                {
                    runtime.AppendBatchLog(
                        batch,
                        $"{_GetTimedTerrainEffectDisplayName(effectState)} 命中，但 {targetUnit.display_name} 免疫该伤害。"
                    );
                }
                else if (damageResult.ShieldAbsorbed > 0)
                {
                    runtime.AppendBatchLog(
                        batch,
                        $"{_GetTimedTerrainEffectDisplayName(effectState)} 命中，但被 {targetUnit.display_name} 的护盾吸收了 {damageResult.ShieldAbsorbed} 点伤害。"
                    );
                }
                else
                {
                    runtime.AppendBatchLog(
                        batch,
                        $"{_GetTimedTerrainEffectDisplayName(effectState)} 命中，但 {targetUnit.display_name} 的伤害被{damageResult.AbsorbReasonText}完全吸收。"
                    );
                }
            }
            if (damageResult.ShieldBroken)
            {
                runtime.AppendBatchLog(
                    batch,
                    $"{targetUnit.display_name} 的护盾被击碎。"
                );
            }
        }
        if (healing > 0)
        {
            runtime.AppendBatchLog(
                batch,
                $"{targetUnit.display_name} 受到 {_GetTimedTerrainEffectDisplayName(effectState)} 影响，恢复 {healing} 点生命。"
            );
        }
        foreach (var statusId in statusEffectIds)
        {
            runtime.AppendBatchLog(
                batch,
                $"{targetUnit.display_name} 获得状态 {statusId}。"
            );
        }

        if (!targetUnit.is_alive)
        {
            killCount = 1;
            runtime.ClearDefeatedUnit(targetUnit, batch);
            runtime.AppendBatchLog(batch, $"{targetUnit.display_name} 被击倒。");
            runtime.RecordEnemyDefeatedAchievement(sourceUnit, targetUnit);
        }

        if (sourceUnit != null)
        {
            runtime.RecordBattleContributionResult(
                sourceUnit,
                targetUnit,
                damage,
                healing,
                killCount > 0,
                new StringName("terrain"),
                effectState.source_skill_id
            );
        }
    }

    private int _GetTimedTerrainMoveCostDelta(BattleTerrainEffectState effectState)
    {
        if (effectState == null)
            return 0;
        if (effectState.remaining_tu <= 0 && !_IsBattleLifetimeEffect(effectState))
            return 0;
        return Math.Max(effectState.move_cost_delta, 0);
    }

    private bool _IsBlockedByNonstackingStatus(
        BattleUnitState unitState,
        BattleTerrainEffectState effectState
    )
    {
        if (unitState == null || effectState == null)
            return false;
        if (
            effectState.does_not_stack_with_status_id != ""
            && unitState.HasStatusEffect(effectState.does_not_stack_with_status_id)
        )
            return true;
        return _UnitHasAnyStatus(unitState, effectState.does_not_stack_with_status_ids);
    }

    private bool _IsBlockedByNonstackingStatus(
        BattleUnitReadView unitView,
        BattleTerrainEffectState effectState
    )
    {
        if (!unitView.IsValid || effectState == null)
            return false;
        if (
            effectState.does_not_stack_with_status_id != ""
            && unitView.HasStatusEffect(effectState.does_not_stack_with_status_id)
        )
            return true;
        return _UnitHasAnyStatus(unitView, effectState.does_not_stack_with_status_ids);
    }

    private bool _UnitHasAnyStatus(
        BattleUnitState unitState,
        IEnumerable<StringName> statusIds
    )
    {
        if (unitState == null)
            return false;
        if (statusIds == null)
        {
            return false;
        }
        foreach (StringName statusId in statusIds)
        {
            if (statusId != "" && unitState.HasStatusEffect(statusId))
                return true;
        }
        return false;
    }

    private bool _UnitHasAnyStatus(
        BattleUnitReadView unitView,
        IEnumerable<StringName> statusIds
    )
    {
        if (!unitView.IsValid)
            return false;
        if (statusIds == null)
        {
            return false;
        }
        foreach (StringName statusId in statusIds)
        {
            if (statusId != "" && unitView.HasStatusEffect(statusId))
                return true;
        }
        return false;
    }

    private BattleTerrainEffectState _BuildTimedTerrainEffect(
        BattleUnitState sourceUnit,
        SkillDef skillDef,
        CombatEffectDef effectDef,
        StringName fieldInstanceId
    )
    {
        CombatEffectLifetimePolicy lifetimePolicy = _ResolveLifetimePolicy(effectDef);
        if (lifetimePolicy == CombatEffectLifetimePolicy.Unknown)
            return null;
        int tickIntervalTu = 0;
        int durationTu = 0;
        if (lifetimePolicy == CombatEffectLifetimePolicy.Battle)
        {
            tickIntervalTu = 0;
            durationTu = 0;
        }
        else
        {
            tickIntervalTu = _NormalizePositiveTuValue(
                effectDef.tick_interval_tu,
                "terrain effect tick_interval_tu"
            );
            durationTu = _NormalizePositiveTuValue(
                effectDef.duration_tu,
                "terrain effect duration_tu"
            );
            if (tickIntervalTu <= 0 || durationTu <= 0)
                return null;
        }

        var effectState = new BattleTerrainEffectState();
        effectState.field_instance_id = fieldInstanceId;
        effectState.effect_id = effectDef.terrain_effect_id;
        BattleTerrainEffectRuntimeKind tickEffectKind = effectDef.TerrainTickEffectKind;
        effectState.RuntimeEffectKind =
            tickEffectKind != BattleTerrainEffectRuntimeKind.Unknown
                ? tickEffectKind
                : (
                    lifetimePolicy == CombatEffectLifetimePolicy.Battle
                        ? BattleTerrainEffectRuntimeKind.None
                        : BattleTerrainEffectRuntimeKind.Damage
                );
        effectState.applied_status_id = effectDef.status_id;
        effectState.applied_status_duration_tu = effectDef.applied_status_duration_tu;
        effectState.lifetime_policy = CombatEffectDef.ToStringName(lifetimePolicy);
        effectState.move_cost_delta = effectDef.move_cost_delta;
        effectState.render_overlay_id = effectDef.render_overlay_id;
        effectState.overlay_priority = effectDef.overlay_priority;
        effectState.display_name = effectDef.display_name;
        effectState.accuracy_modifier_spec = effectDef.accuracy_modifier_spec?.Clone();
        effectState.does_not_stack_with_status_id = effectDef.does_not_stack_with_status_id;
        effectState.does_not_stack_with_status_ids = BuildStringNameList(
            effectDef.does_not_stack_with_status_ids
        );
        effectState.source_unit_id =
            sourceUnit != null ? sourceUnit.unit_id : "";
        effectState.source_skill_id =
            skillDef?.skill_id ?? new StringName("");
        effectState.target_team_filter = BattleTargetTeamRules.ResolveEffectTargetFilter(
            skillDef,
            effectDef
        );
        effectState.power = effectDef.power;
        effectState.damage_tag = effectDef.damage_tag;
        effectState.tick_interval_tu = tickIntervalTu;
        effectState.remaining_tu =
            lifetimePolicy == CombatEffectLifetimePolicy.Battle
                ? 0
                : Math.Max(durationTu, tickIntervalTu);

        var runtime = _ResolveRuntime();
        if (lifetimePolicy == CombatEffectLifetimePolicy.Battle)
        {
            effectState.next_tick_at_tu = 0;
        }
        else if (runtime != null)
        {
            BattleState state = runtime.GetState();
            BattleTimelineState timeline = state?.timeline;
            effectState.next_tick_at_tu =
                timeline != null
                    ? timeline.current_tu + tickIntervalTu
                    : tickIntervalTu;
        }
        else
        {
            effectState.next_tick_at_tu = tickIntervalTu;
        }

        effectState.stack_behavior = _NormalizeStackBehavior(effectDef.stack_behavior);
        effectState.@params = BattleTerrainEffectState.CopyResidualParams(effectDef.@params);
        return effectState;
    }

    public static bool IsTerrainEffectActive(BattleTerrainEffectState effectState)
    {
        if (effectState == null)
            return false;
        if (_IsBattleLifetimeEffectStatic(effectState))
            return true;
        return effectState.remaining_tu > 0;
    }

    private static bool _IsBattleLifetimeEffectStatic(BattleTerrainEffectState effectState)
    {
        if (effectState == null)
            return false;
        return CombatEffectDef.ToLifetimePolicy(effectState.lifetime_policy)
            == CombatEffectLifetimePolicy.Battle;
    }

    private bool _IsBattleLifetimeEffect(BattleTerrainEffectState effectState)
    {
        return _IsBattleLifetimeEffectStatic(effectState);
    }

    private CombatEffectLifetimePolicy _ResolveLifetimePolicy(CombatEffectDef effectDef)
    {
        if (effectDef == null)
            return CombatEffectLifetimePolicy.Unknown;
        return effectDef.LifetimePolicyKind;
    }

    private StringName _NormalizeStackBehavior(StringName stackBehavior)
    {
        if (stackBehavior == StackBehaviorStack || stackBehavior == StackBehaviorIgnoreExisting)
            return stackBehavior;
        return StackBehaviorRefresh;
    }

    private static List<StringName> BuildStringNameList(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }

    private int _NormalizePositiveTuValue(int value, string fieldLabel)
    {
        if (value <= 0)
        {
            GameLog.Error(
                $"{fieldLabel} must be positive and use {TuGranularity} TU steps, got {value}; skipping effect.",
                "battle.terrain.invalid_tu_positive",
                "battle"
            );
            return -1;
        }
        if (value % TuGranularity != 0)
        {
            GameLog.Error(
                $"{fieldLabel} must use {TuGranularity} TU steps, got {value}; skipping effect.",
                "battle.terrain.invalid_tu_granularity",
                "battle"
            );
            return -1;
        }
        return value;
    }

    private string _GetTimedTerrainEffectDisplayName(BattleTerrainEffectState effectState)
    {
        if (effectState == null)
            return "地格效果";
        return !string.IsNullOrEmpty(effectState.display_name)
            ? effectState.display_name
            : effectState.effect_id.ToString();
    }

    private static BattleUnitState GetUnit(BattleState state, StringName unitId)
    {
        return state?.GetUnit(unitId);
    }

}
