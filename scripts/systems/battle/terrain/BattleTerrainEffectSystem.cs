using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal readonly record struct BattleTerrainMovementContactResult(
    bool Checked,
    bool MovementBlocked,
    StringName FieldInstanceId
)
{
    internal static BattleTerrainMovementContactResult None => new(false, false, "");
}

internal sealed class BattleTerrainEffectSystem : IDisposable
{
    private static readonly StringName StackBehaviorRefresh = "refresh";
    private static readonly StringName StackBehaviorStack = "stack";
    private static readonly StringName StackBehaviorIgnoreExisting = "ignore_existing";
    private const int TuGranularity = 5;

    private WeakReference<IBattleTerrainEffectRuntime> _runtimeRef = null;
    private readonly Queue<int> _movementContactSaveRollOverridesForTests = new();

    private IBattleTerrainEffectRuntime _ResolveRuntime()
    {
        if (_runtimeRef == null)
            return null;
        _runtimeRef.TryGetTarget(out var runtime);
        return runtime;
    }

    public void Setup(IBattleTerrainEffectRuntime runtime)
    {
        _runtimeRef =
            runtime != null
                ? new WeakReference<IBattleTerrainEffectRuntime>(runtime)
                : null;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _runtimeRef = null;
        _movementContactSaveRollOverridesForTests.Clear();
    }

    internal void ConfigureMovementContactSaveRollOverridesForTests(
        IEnumerable<int> values
    )
    {
        _movementContactSaveRollOverridesForTests.Clear();
        foreach (int value in values ?? Array.Empty<int>())
        {
            _movementContactSaveRollOverridesForTests.Enqueue(Math.Clamp(value, 1, 20));
        }
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

    public bool UpsertTimedTerrainEffectFromDefinition(
        Vector2I effectCoord,
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition,
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
            || effectDefinition == null
            || effectDefinition.TerrainEffectId == ""
        )
            return false;

        BattleCellState cell = gridService.GetCellState(state, effectCoord);
        if (cell == null)
            return false;

        var normalizedBehavior = _NormalizeStackBehavior(effectDefinition.StackBehavior);
        int existingIndex = -1;
        for (int i = 0; i < cell.timed_terrain_effects.Count; i++)
        {
            var existingEffect = cell.timed_terrain_effects[i];
            if (existingEffect != null && existingEffect.effect_id == effectDefinition.TerrainEffectId)
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
                    skillDefinition,
                    effectDefinition,
                    fieldInstanceId
                );
                if (refreshedEffect == null)
                    return false;
                cell.timed_terrain_effects[existingIndex] = refreshedEffect;
                return true;
            }
        }

        var newEffect = _BuildTimedTerrainEffect(
            sourceUnit,
            skillDefinition,
            effectDefinition,
            fieldInstanceId
        );
        if (newEffect == null)
            return false;
        cell.timed_terrain_effects.Add(newEffect);
        return true;
    }

    public void PrepareTimedTerrainFieldPlacement(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition,
        StringName newFieldInstanceId,
        BattleEventBatch batch
    )
    {
        if (
            sourceUnit == null
            || effectDefinition == null
            || !effectDefinition.TerrainReplaceExistingFromSource
            || effectDefinition.TerrainMaxActiveInstancesPerSource != 1
        )
        {
            return;
        }
        RemoveTerrainFields(
            effectState =>
                effectState.source_unit_id == sourceUnit.unit_id
                && effectState.effect_id == effectDefinition.TerrainEffectId
                && effectState.field_instance_id != newFieldInstanceId,
            batch
        );
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

            var retainedEffects = new List<BattleTerrainEffectState>();
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
        if (targetUnit == null || !targetUnit.IsAlive())
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

        CombatEffectDefinition tickEffect = BuildTickEffectDefinition(effectState);
        AttackEffectResolutionResult damageResult = damageResolver.ResolveEffects(
            sourceUnit,
            targetUnit,
            new[] { tickEffect },
            DamageResolutionContext
                .ForSkill(effectState.source_skill_id)
                .WithBattleState(runtime.GetState())
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

        if (!targetUnit.IsAlive())
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

    public void ApplyContactEffectsForUnit(
        BattleUnitState targetUnit,
        BattleSaveContext saveContext,
        BattleEventBatch batch,
        HashSet<string> processedContactKeys = null
    )
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || targetUnit == null || !targetUnit.IsAlive())
            return;

        BattleState state = runtime.GetState();
        BattleGridService gridService = runtime.GetGridService();
        if (state == null || gridService == null)
            return;

        List<Vector2I> targetCoords = gridService.GetUnitTargetCoords(
            targetUnit,
            targetUnit.GetAnchorCoord()
        );
        if (targetCoords.Count == 0)
            return;

        processedContactKeys ??= new HashSet<string>();
        foreach (Vector2I coord in targetCoords)
        {
            BattleCellState cell = gridService.GetCellState(state, coord);
            if (cell == null || cell.timed_terrain_effects.Count == 0)
                continue;
            foreach (BattleTerrainEffectState effectState in cell.timed_terrain_effects)
            {
                ApplyContactEffectForUnit(
                    runtime,
                    state,
                    targetUnit,
                    effectState,
                    saveContext,
                    processedContactKeys,
                    batch
                );
            }
        }
    }

    public BattleTerrainMovementContactResult ResolveMovementContactForUnit(
        BattleUnitState targetUnit,
        BattleSaveContext saveContext,
        BattleEventBatch batch,
        HashSet<string> processedFieldInstanceIds = null,
        bool startingInsideCheck = false
    )
    {
        var runtime = _ResolveRuntime();
        if (
            runtime == null
            || targetUnit == null
            || !targetUnit.IsAlive()
        )
        {
            return BattleTerrainMovementContactResult.None;
        }
        BattleState state = runtime.GetState();
        BattleGridService gridService = runtime.GetGridService();
        if (state == null || gridService == null)
        {
            return BattleTerrainMovementContactResult.None;
        }

        processedFieldInstanceIds ??= new HashSet<string>();
        bool checkedAny = false;
        StringName lastCheckedFieldInstanceId = "";
        foreach (
            Vector2I coord in gridService.GetUnitTargetCoords(
                targetUnit,
                targetUnit.GetAnchorCoord()
            )
        )
        {
            BattleCellState cell = gridService.GetCellState(state, coord);
            if (cell == null || cell.timed_terrain_effects.Count == 0)
            {
                continue;
            }
            foreach (BattleTerrainEffectState effectState in cell.timed_terrain_effects)
            {
                if (
                    effectState == null
                    || effectState.TerrainContactModeKind
                        != CombatTerrainContactMode.InterruptMovementOnFailedSave
                    || effectState.terrain_remaining_effective_triggers <= 0
                    || (startingInsideCheck && !effectState.terrain_recheck_from_inside)
                    || (
                        effectState.terrain_requires_ground_contact
                        && targetUnit.HasMovementTag(new StringName("fly"))
                    )
                )
                {
                    continue;
                }
                BattleUnitState sourceUnit =
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
                {
                    continue;
                }
                string fieldKey = effectState.field_instance_id.ToString();
                if (!processedFieldInstanceIds.Add(fieldKey))
                {
                    continue;
                }
                checkedAny = true;
                lastCheckedFieldInstanceId = effectState.field_instance_id;

                CombatEffectDefinition saveEffect = BattleRuntimeEffectDefinitions.StaticSave(
                    effectState.contact_save_dc,
                    effectState.contact_save_ability,
                    effectState.contact_save_tag
                );
                BattleSaveContext resolvedSaveContext =
                    _movementContactSaveRollOverridesForTests.Count > 0
                        ? BattleSaveContext.WithSaveRollOverride(
                            _movementContactSaveRollOverridesForTests.Dequeue()
                        )
                        : saveContext;
                BattleSaveResult saveResult = BattleSaveResolver.ResolveSaveResult(
                    sourceUnit,
                    targetUnit,
                    saveEffect,
                    resolvedSaveContext
                );
                if (saveResult.Success)
                {
                    runtime.AppendBatchLog(
                        batch,
                        $"{targetUnit.display_name} 通过敏捷豁免，越过 {_GetTimedTerrainEffectDisplayName(effectState)}；本次移动不再重复判定。"
                    );
                    continue;
                }

                int remaining = Math.Max(
                    effectState.terrain_remaining_effective_triggers - 1,
                    0
                );
                SetFieldRemainingEffectiveTriggers(
                    effectState.field_instance_id,
                    remaining,
                    batch
                );
                runtime.GrantTerrainEffectiveTriggerMastery(
                    sourceUnit,
                    targetUnit,
                    effectState.source_skill_id,
                    batch
                );
                runtime.AppendBatchLog(
                    batch,
                    remaining > 0
                        ? $"{targetUnit.display_name} 未通过敏捷豁免，被 {_GetTimedTerrainEffectDisplayName(effectState)} 拦停；绊索还可生效 {remaining} 次。"
                        : $"{targetUnit.display_name} 未通过敏捷豁免，被 {_GetTimedTerrainEffectDisplayName(effectState)} 拦停；绊索已经耗尽。"
                );
                return new BattleTerrainMovementContactResult(
                    true,
                    true,
                    effectState.field_instance_id
                );
            }
        }
        return checkedAny
            ? new BattleTerrainMovementContactResult(
                true,
                false,
                lastCheckedFieldInstanceId
            )
            : BattleTerrainMovementContactResult.None;
    }

    private void SetFieldRemainingEffectiveTriggers(
        StringName fieldInstanceId,
        int remaining,
        BattleEventBatch batch
    )
    {
        if (fieldInstanceId == "")
        {
            return;
        }
        if (remaining <= 0)
        {
            RemoveTerrainFields(
                effectState => effectState.field_instance_id == fieldInstanceId,
                batch
            );
            return;
        }
        var runtime = _ResolveRuntime();
        BattleState state = runtime?.GetState();
        if (state == null)
        {
            return;
        }
        foreach (BattleState.BattleCellEntry entry in state.CellEntries())
        {
            bool changed = false;
            foreach (BattleTerrainEffectState effectState in entry.Cell?.timed_terrain_effects
                ?? new List<BattleTerrainEffectState>())
            {
                if (effectState?.field_instance_id != fieldInstanceId)
                {
                    continue;
                }
                effectState.terrain_remaining_effective_triggers = remaining;
                changed = true;
            }
            if (changed)
            {
                runtime.AppendChangedCoord(batch, entry.Coord);
            }
        }
    }

    private void RemoveTerrainFields(
        Func<BattleTerrainEffectState, bool> predicate,
        BattleEventBatch batch
    )
    {
        var runtime = _ResolveRuntime();
        BattleState state = runtime?.GetState();
        if (state == null || predicate == null)
        {
            return;
        }
        foreach (BattleState.BattleCellEntry entry in state.CellEntries())
        {
            BattleCellState cell = entry.Cell;
            if (cell == null || cell.timed_terrain_effects.Count == 0)
            {
                continue;
            }
            int removed = cell.timed_terrain_effects.RemoveAll(
                effectState => effectState != null && predicate(effectState)
            );
            if (removed > 0)
            {
                runtime.AppendChangedCoord(batch, entry.Coord);
            }
        }
    }

    private void ApplyContactEffectForUnit(
        IBattleTerrainEffectRuntime runtime,
        BattleState state,
        BattleUnitState targetUnit,
        BattleTerrainEffectState effectState,
        BattleSaveContext saveContext,
        HashSet<string> processedContactKeys,
        BattleEventBatch batch
    )
    {
        bool hasContactStatus = effectState?.contact_status_id != "";
        bool hasContactDamage =
            effectState?.contact_damage_dice_count > 0
            && effectState.contact_damage_dice_sides > 0
            && effectState.contact_damage_tag != "";
        if (
            runtime == null
            || state == null
            || targetUnit == null
            || effectState == null
            || (!hasContactStatus && !hasContactDamage)
            || processedContactKeys == null
        )
        {
            return;
        }
        if (
            effectState.contact_blocked_by_trait_id != ""
            && targetUnit.HasEffectiveTrait(
                effectState.contact_blocked_by_trait_id
            )
        )
        {
            return;
        }

        BattleUnitState sourceUnit =
            effectState.source_unit_id != "" ? GetUnit(state, effectState.source_unit_id) : null;
        if (
            !BattleTargetTeamRules.IsUnitValidForFilter(
                sourceUnit,
                targetUnit,
                effectState.target_team_filter
            )
        )
        {
            return;
        }

        string contactKindKey = hasContactStatus
            ? effectState.contact_status_id.ToString()
            : $"damage:{effectState.contact_damage_tag}";
        string contactKey =
            $"{effectState.field_instance_id}|{targetUnit.unit_id}|{contactKindKey}";
        if (processedContactKeys.Contains(contactKey))
            return;
        processedContactKeys.Add(contactKey);

        if (effectState.contact_save_dc > 0)
        {
            CombatEffectDefinition saveEffect = BattleRuntimeEffectDefinitions.StaticSave(
                effectState.contact_save_dc,
                effectState.contact_save_ability,
                effectState.contact_save_tag
            );
            BattleSaveResult saveResult = BattleSaveResolver.ResolveSaveResult(
                sourceUnit,
                targetUnit,
                saveEffect,
                saveContext
            );
            if (effectState.contact_apply_on_save_failure && saveResult.Success)
                return;
        }

        if (hasContactDamage)
        {
            BattleDamageResolver damageResolver = runtime.GetDamageResolver();
            if (damageResolver == null)
                return;
            CombatEffectDefinition damageEffect = BattleRuntimeEffectDefinitions.Damage(
                effectState.contact_damage_tag,
                effectState.contact_damage_dice_count,
                effectState.contact_damage_dice_sides,
                effectState.contact_damage_flat_bonus,
                new[] { effectState.contact_damage_tag }
            );
            AttackEffectResolutionResult damageResult = damageResolver.ResolveEffects(
                sourceUnit,
                targetUnit,
                new[] { damageEffect },
                DamageResolutionContext
                    .ForSkill(effectState.source_skill_id)
                    .WithBattleState(state)
                    .WithDamageApplicationHookContext(
                        batch,
                        BattleEffectOrigin.EquipmentAbility()
                    )
            );
            if (damageResult.Applied)
            {
                runtime.AppendResultSourceStatusEffects(batch, sourceUnit, damageResult);
                runtime.AppendChangedUnitId(batch, targetUnit.unit_id);
                runtime.AppendChangedUnitCoords(batch, targetUnit);
                if (damageResult.Damage > 0)
                {
                    runtime.AppendBatchLog(
                        batch,
                        $"{targetUnit.display_name} 踩入 {_GetTimedTerrainEffectDisplayName(effectState)}，受到 {damageResult.Damage} 点伤害。"
                    );
                }
                else if (damageResult.AnyImmune)
                {
                    runtime.AppendBatchLog(
                        batch,
                        $"{targetUnit.display_name} 免疫 {_GetTimedTerrainEffectDisplayName(effectState)} 的伤害。"
                    );
                }
                bool causedDefeat = !targetUnit.IsAlive();
                if (causedDefeat)
                {
                    runtime.ClearDefeatedUnit(targetUnit, batch);
                    runtime.AppendBatchLog(batch, $"{targetUnit.display_name} 被击倒。");
                    runtime.RecordEnemyDefeatedAchievement(sourceUnit, targetUnit);
                }
                if (sourceUnit != null)
                {
                    runtime.RecordBattleContributionResult(
                        sourceUnit,
                        targetUnit,
                        damageResult.Damage,
                        damageResult.Healing,
                        causedDefeat,
                        new StringName("terrain"),
                        effectState.source_skill_id
                    );
                }
            }
        }

        if (!hasContactStatus || !targetUnit.IsAlive())
            return;

        int durationTu = Math.Max(effectState.contact_status_duration_tu, 0);
        CombatEffectDefinition statusEffect = BattleRuntimeEffectDefinitions.Status(
            effectState.contact_status_id,
            1,
            durationTu,
            stackBehavior: effectState.contact_stack_behavior,
            stackLimit: effectState.contact_stack_limit,
            displayName: effectState.contact_status_display_label,
            countsAsDebuffOverride: effectState.contact_counts_as_debuff_override,
            countsAsDebuff: effectState.contact_counts_as_debuff,
            undispellable: effectState.contact_undispellable,
            dispellableMagic: effectState.contact_dispellable_magic,
            dispellableHarmfulMagic: effectState.contact_dispellable_harmful_magic,
            dispellableBeneficialMagic: effectState.contact_dispellable_beneficial_magic
        );
        StringName sourceUnitId = sourceUnit?.unit_id ?? new StringName("");
        BattleStatusSourceIdentity sourceIdentity = effectState.source_skill_id != ""
            ? BattleStatusSourceIdentity.Skill(sourceUnitId, effectState.source_skill_id)
            : BattleStatusSourceIdentity.TerrainEffect(sourceUnitId, effectState.effect_id);
        BattleStatusEffectState statusEntry = BattleStatusSemanticTable.MergeStatus(
            statusEffect,
            sourceUnitId,
            targetUnit.GetStatusEffect(effectState.contact_status_id),
            effectState.contact_status_id,
            sourceIdentity
        );
        if (statusEntry == null)
            return;
        ApplyContactTimelineDamagePayload(statusEntry, effectState);
        BattleStatusSemanticTable.SynchronizeSourceContributionTimelinePayload(
            statusEntry,
            sourceIdentity
        );
        targetUnit.SetStatusEffect(statusEntry);
        runtime.MarkAppliedStatusesForTurnTiming(
            targetUnit,
            new Godot.Collections.Array<StringName> { effectState.contact_status_id }
        );
        runtime.AppendChangedUnitId(batch, targetUnit.unit_id);
        runtime.AppendChangedUnitCoords(batch, targetUnit);
        runtime.AppendBatchLog(
            batch,
            $"{targetUnit.display_name} 感染 {_GetTimedTerrainEffectDisplayName(effectState)}。"
        );
    }

    private static void ApplyContactTimelineDamagePayload(
        BattleStatusEffectState statusEntry,
        BattleTerrainEffectState effectState
    )
    {
        if (statusEntry == null || effectState == null)
            return;
        if (effectState.contact_tick_interval_tu > 0)
            statusEntry.tick_interval_tu = effectState.contact_tick_interval_tu;
        if (effectState.contact_damage_tag != "")
            statusEntry.damage_tag = effectState.contact_damage_tag;
        if (
            effectState.contact_timeline_damage_dice_count > 0
            && effectState.contact_timeline_damage_dice_sides > 0
        )
        {
            statusEntry.timeline_damage_dice_count =
                effectState.contact_timeline_damage_dice_count;
            statusEntry.timeline_damage_dice_sides =
                effectState.contact_timeline_damage_dice_sides;
            statusEntry.timeline_damage_flat_bonus = Math.Max(
                effectState.contact_timeline_damage_flat_bonus,
                0
            );
        }
    }

    private static CombatEffectDefinition BuildTickEffectDefinition(
        BattleTerrainEffectState effectState
    )
    {
        IReadOnlyDictionary<string, object> normalizedParams =
            ContentValueNormalizer.NormalizeDictionary(
                effectState?.ParamsSnapshotPlain
                    ?? new Dictionary<string, object>(System.StringComparer.Ordinal),
                "BattleTerrainEffectSystem.tick_effect.parameters"
            );
        return new CombatEffectDefinition(
            effectType: NormalizeStringName(effectState?.effect_type),
            effectTargetTeamFilter: default,
            statusId: NormalizeStringName(effectState?.applied_status_id),
            saveFailureStatusId: default,
            terrainEffectId: default,
            terrainReplaceTo: default,
            heightDelta: 0,
            requiresWeapon: false,
            addWeaponDice: false,
            preventRepeatTarget: true,
            forcedMoveMode: default,
            minSkillLevel: 0,
            maxSkillLevel: 0,
            damageTag: NormalizeStringName(effectState?.damage_tag),
            damageRatioPercent: 0,
            preResistanceDamageMultiplier: 1.0,
            bonusCondition: default,
            hpRatioThresholdPercent: 0,
            damageCategory: default,
            drBypassTag: default,
            diceCount: 0,
            diceSides: 0,
            diceBonus: 0,
            bonusDamageDiceCount: 0,
            bonusDamageDiceSides: 0,
            bonusDamageDiceBonus: 0,
            saveDc: 0,
            saveDcMode: default,
            saveDcSourceAbility: default,
            saveAbility: default,
            savePartialOnSuccess: false,
            saveTag: default,
            thresholdBaseValue: 0,
            thresholdLevelAnchor: 0,
            thresholdLevelBonusPerDelta: 0,
            thresholdMaxHpRatioPercent: 0,
            thresholdCapMaxHpRatioPercent: 0,
            soulFractureDurationTu: 0,
            healMultiplierPercent: 0,
            shieldGainMultiplierPercent: 0,
            appliedStatusDurationTu: effectState?.applied_status_duration_tu ?? 0,
            durationTu: effectState?.applied_status_duration_tu ?? 0,
            tickIntervalTu: 0,
            effectTags: Array.Empty<StringName>(),
            triggerCondition: new StringName(""),
            power: effectState?.power ?? 0,
            parameters: normalizedParams,
            triggerEvent: new StringName("")
        );
    }

    private static StringName NormalizeStringName(StringName value)
    {
        return value == null ? new StringName("") : value;
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
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition,
        StringName fieldInstanceId
    )
    {
        CombatEffectLifetimePolicy lifetimePolicy = _ResolveLifetimePolicy(effectDefinition);
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
                effectDefinition.TickIntervalTu,
                "terrain effect tick_interval_tu"
            );
            durationTu = _NormalizePositiveTuValue(
                effectDefinition.DurationTu,
                "terrain effect duration_tu"
            );
            if (tickIntervalTu <= 0 || durationTu <= 0)
                return null;
        }

        var effectState = new BattleTerrainEffectState();
        effectState.field_instance_id = fieldInstanceId;
        effectState.effect_id = effectDefinition.TerrainEffectId;
        BattleTerrainEffectRuntimeKind tickEffectKind =
            BattleTypedNames.ToTerrainEffectRuntimeKind(effectDefinition.TickEffectType);
        effectState.RuntimeEffectKind =
            tickEffectKind != BattleTerrainEffectRuntimeKind.Unknown
                ? tickEffectKind
                : (
                    lifetimePolicy == CombatEffectLifetimePolicy.Battle
                        ? BattleTerrainEffectRuntimeKind.None
                        : BattleTerrainEffectRuntimeKind.Damage
                );
        effectState.applied_status_id = effectDefinition.StatusId;
        effectState.applied_status_duration_tu = effectDefinition.AppliedStatusDurationTu;
        effectState.lifetime_policy = CombatEffectContentRules.ToStringName(lifetimePolicy);
        effectState.move_cost_delta = effectDefinition.MoveCostDelta;
        effectState.render_overlay_id = effectDefinition.RenderOverlayId;
        effectState.overlay_priority = effectDefinition.OverlayPriority;
        effectState.display_name = effectDefinition.DisplayName;
        effectState.accuracy_modifier_spec = effectDefinition.AccuracyModifierSpec?.Clone();
        effectState.does_not_stack_with_status_id = effectDefinition.DoesNotStackWithStatusId;
        effectState.does_not_stack_with_status_ids = BuildStringNameList(
            effectDefinition.DoesNotStackWithStatusIds
        );
        effectState.source_unit_id =
            sourceUnit != null ? sourceUnit.unit_id : "";
        effectState.source_skill_id =
            skillDefinition?.SkillId ?? new StringName("");
        effectState.target_team_filter = BattleTargetTeamRules.ResolveEffectTargetFilter(
            skillDefinition,
            effectDefinition
        );
        effectState.power = effectDefinition.Power;
        effectState.damage_tag = effectDefinition.DamageTag;
        effectState.contact_damage_dice_count = Math.Max(
            effectDefinition.GetIntParamTyped("contact_damage_dice_count"),
            0
        );
        effectState.contact_damage_dice_sides = Math.Max(
            effectDefinition.GetIntParamTyped("contact_damage_dice_sides"),
            0
        );
        effectState.contact_damage_flat_bonus = Math.Max(
            effectDefinition.GetIntParamTyped("contact_damage_flat_bonus"),
            0
        );
        effectState.contact_damage_tag = effectDefinition.GetStringNameParamTyped(
            "contact_damage_tag"
        );
        effectState.terrain_contact_mode =
            effectDefinition.TerrainContactMode ?? new StringName("");
        effectState.terrain_remaining_effective_triggers =
            effectDefinition.TerrainEffectiveTriggerCount;
        effectState.terrain_requires_ground_contact =
            effectDefinition.TerrainRequiresGroundContact;
        effectState.terrain_recheck_from_inside = effectDefinition.TerrainRecheckFromInside;
        effectState.terrain_max_active_instances_per_source =
            effectDefinition.TerrainMaxActiveInstancesPerSource;
        effectState.terrain_replace_existing_from_source =
            effectDefinition.TerrainReplaceExistingFromSource;
        effectState.contact_save_dc = effectDefinition.SaveDc;
        effectState.contact_save_ability = effectDefinition.SaveAbility;
        effectState.contact_save_tag = effectDefinition.SaveTag;
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

        effectState.stack_behavior = _NormalizeStackBehavior(effectDefinition.StackBehavior);
        effectState.SetParamsTyped(
            BattleTerrainEffectState.CopyResidualParamsPlain(effectDefinition.Parameters)
        );
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
        return CombatEffectContentRules.ToLifetimePolicy(effectState.lifetime_policy)
            == CombatEffectLifetimePolicy.Battle;
    }

    private bool _IsBattleLifetimeEffect(BattleTerrainEffectState effectState)
    {
        return _IsBattleLifetimeEffectStatic(effectState);
    }

    private CombatEffectLifetimePolicy _ResolveLifetimePolicy(
        CombatEffectDefinition effectDefinition
    )
    {
        if (effectDefinition == null)
            return CombatEffectLifetimePolicy.Unknown;
        return CombatEffectContentRules.ToLifetimePolicy(effectDefinition.LifetimePolicy);
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
