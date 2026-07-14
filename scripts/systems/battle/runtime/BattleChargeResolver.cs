using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleChargeResolver
{
    private static readonly StringName ChargeEffectType = "charge";
    private static readonly StringName PathStepAoeEffectType = "path_step_aoe";
    private static readonly StringName DamageEffectType = "damage";
    private static readonly StringName StatusEffectType = "status";
    private static readonly StringName SkillAttackCheckMode = "skill_attack_check";
    private static readonly StringName ExecuteStage = "execute";
    private const string TrapEffectPrefix = "trap_";

    private readonly record struct ChargePathStepAoeParameters(
        bool AllowRepeatHitsAcrossSteps,
        bool ResolveAsWeaponAttack
    )
    {
        public static ChargePathStepAoeParameters FromEffect(
            CombatEffectDefinition effectDefinition
        )
        {
            return new ChargePathStepAoeParameters(
                effectDefinition?.AllowRepeatHitsAcrossSteps ?? false,
                effectDefinition?.ResolveAsWeaponAttack ?? false
            );
        }
    }

    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BattleSkillMasteryService _skillMasteryService;

    private BattleRuntimeModule Runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    internal void Setup(BattleRuntimeModule runtime, BattleSkillMasteryService skillMasteryService)
    {
        Runtime = runtime;
        _skillMasteryService = skillMasteryService;
    }

    internal void DisposeRuntime()
    {
        Runtime = null;
        _skillMasteryService = null;
    }

    internal bool handle_charge_skill_command_result(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleGroundSkillValidationResult validation,
        BattleEventBatch batch
    )
    {
        if (
            !HasRuntime()
            || active_unit == null
            || skillDefinition == null
            || castVariantDefinition == null
            || batch == null
        )
        {
            return false;
        }

        Vector2I direction = validation.Direction;
        int requestedDistance = validation.Distance;
        if (direction == Vector2I.Zero || requestedDistance <= 0)
        {
            return false;
        }

        var chargeBatch = new BattleEventBatch();
        int movedSteps = 0;
        int pathStepTriggerCount = 0;
        int pathStepHitCount = 0;
        var pathStepSeenUnitIds = new HashSet<StringName>();
        var totalUnitHitCounts = new Dictionary<StringName, int>();
        string stopReason = "";

        while (movedSteps < requestedDistance)
        {
            Vector2I nextAnchor = active_unit.coord + direction;
            if (!CanChargeEnterAnchor(active_unit, nextAnchor))
            {
                chargeBatch.AddLogLine(
                    $"{active_unit.display_name} 前方地形无法通过，冲锋被迫停下。"
                );
                stopReason = "terrain";
                break;
            }

            ChargeBlockerResult blockerResult = ResolveChargeStepBlockers(
                active_unit,
                nextAnchor,
                direction,
                chargeBatch
            );
            if (blockerResult.Result == "stop")
            {
                stopReason = string.IsNullOrEmpty(blockerResult.Reason)
                    ? "blocker"
                    : blockerResult.Reason;
                break;
            }

            Vector2I previousAnchor = active_unit.coord;
            BattleBarrierInteractionResult barrierResult =
                Runtime._layered_barrier_service?.ResolveUnitBoundaryCrossingResult(
                    active_unit,
                    previousAnchor,
                    nextAnchor,
                    chargeBatch
                ) ?? new BattleBarrierInteractionResult(false, false);
            if (
                barrierResult.Blocked
                || !active_unit.is_alive
                || active_unit.coord != previousAnchor
            )
            {
                stopReason = "barrier";
                break;
            }

            List<Vector2I> previousCoords = DuplicateVector2IList(active_unit.occupied_coords);
            if (!GridService.MoveUnit(State, active_unit, nextAnchor))
            {
                stopReason = "blocked";
                break;
            }

            movedSteps += 1;
            AppendChangedUnitId(chargeBatch, active_unit.unit_id);
            AppendChangedCoords(chargeBatch, previousCoords);
            AppendChangedUnitCoords(chargeBatch, active_unit);

            PathStepResult stepAoeResult = ApplyChargePathStepAoeEffects(
                active_unit,
                skillDefinition,
                castVariantDefinition,
                chargeBatch,
                pathStepSeenUnitIds
            );
            if (stepAoeResult.Triggered)
            {
                pathStepTriggerCount += 1;
                pathStepHitCount += stepAoeResult.HitCount;
                foreach ((StringName unitId, int count) in stepAoeResult.UnitHitCounts)
                {
                    totalUnitHitCounts.TryGetValue(unitId, out int existingCount);
                    totalUnitHitCounts[unitId] =
                        existingCount + count;
                }
            }

            TrapResult trapResult = TriggerChargeTrap(active_unit);
            if (trapResult.Triggered)
            {
                Vector2I trapCoord = trapResult.Coord;
                int skillLevel =
                    skillDefinition != null && HasRuntime()
                        ? GetUnitSkillLevel(active_unit, skillDefinition.SkillId)
                        : 0;
                CombatEffectDefinition chargeEffect = GetChargeEffectDefinition(
                    castVariantDefinition
                );
                int trapImmunityLevel =
                    chargeEffect != null
                        ? GetInt(chargeEffect.Parameters, "trap_immunity_level", 999)
                        : 999;
                AppendChangedCoord(chargeBatch, trapCoord);
                if (skillLevel >= trapImmunityLevel)
                {
                    chargeBatch.AddLogLine(
                        $"{active_unit.display_name} 在 ({trapCoord.X}, {trapCoord.Y}) 踩中陷阱，但 7 级冲锋免疫中断。"
                    );
                }
                else
                {
                    chargeBatch.AddLogLine(
                        $"{active_unit.display_name} 在 ({trapCoord.X}, {trapCoord.Y}) 触发陷阱，冲锋被中断。"
                    );
                    stopReason = "trap";
                    break;
                }
            }
        }

        MergeBatch(batch, chargeBatch);
        if (movedSteps > 0)
        {
            CombatEffectDefinition pathStepAoeEffect = GetChargePathStepAoeEffectDefinition(
                castVariantDefinition,
                skillDefinition,
                active_unit
            );
            batch.AddLogLine(
                $"{active_unit.display_name} 使用 {FormatSkillVariantLabel(skillDefinition, castVariantDefinition)}，向{FormatChargeDirection(direction)}冲锋 {movedSteps} 格。"
            );
            if (pathStepTriggerCount > 0)
            {
                batch.AddLogLine(
                    $"{active_unit.display_name} 沿途触发 {pathStepTriggerCount} 次{GetPathStepLogLabel(pathStepAoeEffect)}，共命中 {pathStepHitCount} 个单位。"
                );
            }
            ApplyRepeatHitStatusEffects(
                active_unit,
                skillDefinition,
                pathStepAoeEffect,
                totalUnitHitCounts,
                batch
            );
            _skillMasteryService?.RecordMasteryAmount(
                skillDefinition?.SkillId ?? new StringName(""),
                movedSteps
            );
            return true;
        }

        if (chargeBatch.LogLinesTyped.Count > 0 || !string.IsNullOrEmpty(stopReason))
        {
            batch.AddLogLine(
                $"{active_unit.display_name} 使用 {FormatSkillVariantLabel(skillDefinition, castVariantDefinition)}，但在起步时被拦下。"
            );
            return true;
        }
        return false;
    }

    internal BattleGroundSkillValidationResult ValidateChargeCommandResult(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> normalized_coords,
        BattleGroundSkillValidationResult base_result
    )
    {
        if (
            !HasRuntime()
            || active_unit == null
            || skillDefinition == null
            || castVariantDefinition == null
            || normalized_coords == null
            || normalized_coords.Count == 0
        )
        {
            return base_result;
        }

        Vector2I targetCoord = normalized_coords[0];
        if (!GridService.IsInside(State, targetCoord))
        {
            return base_result with { Message = "目标地格超出战场范围。" };
        }

        ChargeTargetInfo targetInfo = ResolveChargeTarget(active_unit, targetCoord);
        if (!targetInfo.Valid)
        {
            return base_result with { Message = "冲锋只能选择当前单位同一行或同一列的目标地格。" };
        }

        int maxDistance = GetChargeMaxDistance(active_unit, castVariantDefinition);
        int chargeDistance = targetInfo.Distance;
        if (chargeDistance > maxDistance)
        {
            return base_result with { Message = $"目标地格超出当前冲锋距离 {maxDistance}。" };
        }

        Vector2I chargeDirection = targetInfo.Direction;
        return BattleGroundSkillValidationResult.AllowedResult(
            "可施放；若途中受阻会在当前可达位置停下。",
            new[] { targetCoord },
            BuildChargePreviewCoords(active_unit, chargeDirection, chargeDistance),
            chargeDirection,
            chargeDistance,
            ResolvePreviewChargeAnchor(
                active_unit,
                skillDefinition,
                castVariantDefinition,
                chargeDirection,
                chargeDistance
            )
        );
    }

    internal BattleGroundSkillValidationResult ValidateChargeCommandResult(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> normalized_coords,
        BattleGroundSkillValidationResult base_result
    )
    {
        if (
            !HasRuntime()
            || !active_unit.IsValid
            || skillDefinition == null
            || castVariantDefinition == null
            || normalized_coords == null
            || normalized_coords.Count == 0
        )
        {
            return base_result;
        }

        Vector2I targetCoord = normalized_coords[0];
        if (!GridService.IsInside(State, targetCoord))
        {
            return base_result with { Message = "目标地格超出战场范围。" };
        }

        ChargeTargetInfo targetInfo = ResolveChargeTarget(active_unit, targetCoord);
        if (!targetInfo.Valid)
        {
            return base_result with { Message = "冲锋只能选择当前单位同一行或同一列的目标地格。" };
        }

        int maxDistance = GetChargeMaxDistance(active_unit, castVariantDefinition);
        int chargeDistance = targetInfo.Distance;
        if (chargeDistance > maxDistance)
        {
            return base_result with { Message = $"目标地格超出当前冲锋距离 {maxDistance}。" };
        }

        Vector2I chargeDirection = targetInfo.Direction;
        return BattleGroundSkillValidationResult.AllowedResult(
            "可施放；若途中受阻会在当前可达位置停下。",
            new[] { targetCoord },
            BuildChargePreviewCoords(active_unit, chargeDirection, chargeDistance),
            chargeDirection,
            chargeDistance,
            ResolvePreviewChargeAnchor(
                active_unit,
                skillDefinition,
                castVariantDefinition,
                chargeDirection,
                chargeDistance
            )
        );
    }

    internal List<Vector2I> BuildChargeStepAoePreviewCoords(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        Vector2I direction,
        int distance,
        CombatEffectDefinition pathStepAoeEffect
    )
    {
        var coords = new List<Vector2I>();
        if (
            !HasRuntime()
            || active_unit == null
            || direction == Vector2I.Zero
            || distance <= 0
            || pathStepAoeEffect == null
        )
        {
            return new List<Vector2I>();
        }

        CombatEffectDefinition stageEffect = pathStepAoeEffect.WithEffectType(DamageEffectType);
        if (stageEffect == null)
        {
            return new List<Vector2I>();
        }
        CombatEffectDefinition[] stageEffects = { stageEffect };
        var coordSet = new HashSet<Vector2I>();
        foreach (
            Vector2I anchorCoord in BuildChargePathAnchorCoords(active_unit, direction, distance)
        )
        {
            List<Vector2I> anchorEffectCoords = BuildChargeStepEffectCoordsForAnchor(
                active_unit,
                anchorCoord,
                pathStepAoeEffect
            );
            IReadOnlyList<Vector2I> allowedEffectCoords = anchorEffectCoords;
            BattleLayeredBarrierService barrierService = Runtime._layered_barrier_service;
            if (barrierService != null)
            {
                allowedEffectCoords = barrierService
                    .PreviewGroundEffectBarrierClipResultAtCoord(
                        active_unit,
                        anchorCoord,
                        skillDefinition,
                        stageEffects,
                        Array.Empty<CombatEffectDefinition>(),
                        anchorEffectCoords
                    )
                    .UnitEffects.AllowedCoords;
            }
            foreach (Vector2I effectCoord in allowedEffectCoords)
            {
                if (coordSet.Add(effectCoord))
                {
                    coords.Add(effectCoord);
                }
            }
        }
        return SortCoords(coords);
    }

    internal List<Vector2I> BuildChargeStepAoePreviewCoords(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition,
        Vector2I direction,
        int distance,
        CombatEffectDefinition pathStepAoeEffect
    )
    {
        var coords = new List<Vector2I>();
        if (
            !HasRuntime()
            || !active_unit.IsValid
            || direction == Vector2I.Zero
            || distance <= 0
            || pathStepAoeEffect == null
        )
        {
            return new List<Vector2I>();
        }

        CombatEffectDefinition stageEffect = pathStepAoeEffect.WithEffectType(DamageEffectType);
        if (stageEffect == null)
        {
            return new List<Vector2I>();
        }
        CombatEffectDefinition[] stageEffects = { stageEffect };
        var coordSet = new HashSet<Vector2I>();
        foreach (
            Vector2I anchorCoord in BuildChargePathAnchorCoords(active_unit, direction, distance)
        )
        {
            List<Vector2I> anchorEffectCoords = BuildChargeStepEffectCoordsForAnchor(
                active_unit,
                anchorCoord,
                pathStepAoeEffect
            );
            IReadOnlyList<Vector2I> allowedEffectCoords = anchorEffectCoords;
            BattleLayeredBarrierService barrierService = Runtime._layered_barrier_service;
            if (barrierService != null)
            {
                allowedEffectCoords = barrierService
                    .PreviewGroundEffectBarrierClipResultAtCoord(
                        active_unit,
                        anchorCoord,
                        skillDefinition,
                        stageEffects,
                        Array.Empty<CombatEffectDefinition>(),
                        anchorEffectCoords
                    )
                    .UnitEffects.AllowedCoords;
            }
            foreach (Vector2I effectCoord in allowedEffectCoords)
            {
                if (coordSet.Add(effectCoord))
                {
                    coords.Add(effectCoord);
                }
            }
        }
        return SortCoords(coords);
    }

    internal CombatEffectDefinition GetChargePathStepAoeEffectDefinition(
        CombatCastVariantDefinition castVariantDefinition,
        SkillDefinition skillDefinition,
        BattleUnitState activeUnit
    )
    {
        if (castVariantDefinition == null)
        {
            return null;
        }

        int skillLevel = -1;
        if (skillDefinition != null && activeUnit != null && HasRuntime())
        {
            skillLevel = GetUnitSkillLevel(activeUnit, skillDefinition.SkillId);
        }

        foreach (CombatEffectDefinition effectDefinition in castVariantDefinition.EffectDefinitions)
        {
            if (effectDefinition == null || effectDefinition.EffectKind != BattleEffectKind.PathStepAoe)
            {
                continue;
            }
            if (skillLevel >= 0 && !IsEffectUnlockedForSkillLevel(effectDefinition, skillLevel))
            {
                continue;
            }
            return effectDefinition;
        }
        return null;
    }

    internal CombatEffectDefinition GetChargePathStepAoeEffectDefinition(
        CombatCastVariantDefinition castVariantDefinition,
        SkillDefinition skillDefinition,
        BattleUnitReadView activeUnit
    )
    {
        if (castVariantDefinition == null)
        {
            return null;
        }

        int skillLevel = -1;
        if (skillDefinition != null && activeUnit.IsValid && HasRuntime())
        {
            skillLevel = activeUnit.GetKnownSkillLevel(skillDefinition.SkillId);
        }

        foreach (CombatEffectDefinition effectDefinition in castVariantDefinition.EffectDefinitions)
        {
            if (effectDefinition == null || effectDefinition.EffectKind != BattleEffectKind.PathStepAoe)
            {
                continue;
            }
            if (skillLevel >= 0 && !IsEffectUnlockedForSkillLevel(effectDefinition, skillLevel))
            {
                continue;
            }
            return effectDefinition;
        }
        return null;
    }

    internal bool IsChargeOption(CombatCastVariantDefinition castVariantDefinition)
    {
        return GetChargeEffectDefinition(castVariantDefinition) != null;
    }

    internal CombatEffectDefinition GetChargeEffectDefinition(
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        if (castVariantDefinition == null)
        {
            return null;
        }
        foreach (CombatEffectDefinition effectDefinition in castVariantDefinition.EffectDefinitions)
        {
            if (effectDefinition != null && effectDefinition.EffectKind == BattleEffectKind.Charge)
            {
                return effectDefinition;
            }
        }
        return null;
    }

    private void ApplyRepeatHitStatusEffects(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatEffectDefinition pathStepAoeEffect,
        Dictionary<StringName, int> totalUnitHitCounts,
        BattleEventBatch batch
    )
    {
        if (activeUnit == null || skillDefinition == null || pathStepAoeEffect == null || batch == null)
        {
            return;
        }

        IReadOnlyDictionary<string, object> parameters = pathStepAoeEffect.Parameters;
        StringName statusId = GetStringName(parameters, "repeat_hit_status_id");
        if (IsEmpty(statusId))
        {
            return;
        }

        int minSkillLevel = Math.Max(
            GetInt(parameters, "repeat_hit_status_min_skill_level"),
            0
        );
        int skillLevel = HasRuntime() ? GetUnitSkillLevel(activeUnit, skillDefinition.SkillId) : 0;
        if (skillLevel < minSkillLevel)
        {
            return;
        }

        int hitThreshold = Math.Max(
            GetInt(parameters, "repeat_hit_status_threshold", 1),
            1
        );
        int statusPower = Math.Max(GetInt(parameters, "repeat_hit_status_power", 1), 1);
        int statusDurationTu = GetInt(parameters, "repeat_hit_status_duration_tu");
        if (statusDurationTu <= 0)
        {
            return;
        }

        IReadOnlyDictionary<string, object> extraStatusParams =
            GetVariantDictionary(parameters, "repeat_hit_status_params");

        foreach ((StringName unitId, int hitCount) in totalUnitHitCounts)
        {
            if (hitCount < hitThreshold)
            {
                continue;
            }
            if (
                State == null
                || !State.TryGetUnitTyped(unitId, out BattleUnitState targetUnit)
                || targetUnit == null
                || !targetUnit.is_alive
            )
            {
                continue;
            }

            CombatEffectDefinition statusEffect = BattleRuntimeEffectDefinitions.Status(
                statusId,
                statusPower,
                statusDurationTu,
                extraStatusParams
            );
            BattleStatusEffectState statusEntry = BattleStatusSemanticTable.MergeStatus(
                statusEffect,
                activeUnit.unit_id,
                targetUnit.GetStatusEffect(statusId)
            );
            if (statusEntry == null)
            {
                continue;
            }

            targetUnit.SetStatusEffect(statusEntry);
            AppendChangedUnitId(batch, targetUnit.unit_id);
            string logLine = FormatRepeatHitStatusLog(
                parameters,
                targetUnit,
                skillDefinition,
                hitCount,
                statusId
            );
            if (!string.IsNullOrEmpty(logLine))
            {
                batch.AddLogLine(logLine);
            }
        }
    }

    private string FormatRepeatHitStatusLog(
        IReadOnlyDictionary<string, object> parameters,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        int hitCount,
        StringName statusId
    )
    {
        if (targetUnit == null || skillDefinition == null)
        {
            return "";
        }

        string template = GetString(parameters, "repeat_hit_status_log_template", "").StripEdges();
        if (string.IsNullOrEmpty(template))
        {
            return $"{targetUnit.display_name} 被 {skillDefinition.DisplayName} 连续命中 {hitCount} 次，受到 {statusId}。";
        }

        return template
            .Replace("{target}", targetUnit.display_name)
            .Replace("{skill}", skillDefinition.DisplayName)
            .Replace("{hit_count}", hitCount.ToString())
            .Replace("{status_id}", statusId.ToString());
    }

    private string GetPathStepLogLabel(CombatEffectDefinition pathStepAoeEffect)
    {
        string label = GetString(
                pathStepAoeEffect?.Parameters,
                "path_step_log_label",
                "路径攻击"
            )
            .StripEdges();
        return string.IsNullOrEmpty(label) ? "路径攻击" : label;
    }

    private string GetPathStepResultLabel(CombatEffectDefinition pathStepAoeEffect)
    {
        return $"沿途{GetPathStepLogLabel(pathStepAoeEffect)}";
    }

    private Vector2I ResolvePreviewChargeAnchor(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        Vector2I direction,
        int requestedDistance
    )
    {
        if (
            !HasRuntime()
            || State == null
            || activeUnit == null
            || skillDefinition == null
            || castVariantDefinition == null
        )
        {
            return activeUnit?.coord ?? new Vector2I(-1, -1);
        }
        if (direction == Vector2I.Zero || requestedDistance <= 0)
        {
            return activeUnit.coord;
        }

        Vector2I originalAnchor = activeUnit.coord;
        Vector2I resolvedAnchor = originalAnchor;
        for (int stepIndex = 0; stepIndex < requestedDistance; stepIndex++)
        {
            Vector2I nextAnchor = resolvedAnchor + direction;
            if (!CanPreviewChargeEnterAnchorFrom(activeUnit, resolvedAnchor, nextAnchor))
            {
                break;
            }
            activeUnit.SetAnchorCoord(resolvedAnchor);
            if (WouldPreviewChargeStopOnBlocker(activeUnit, nextAnchor, direction))
            {
                break;
            }
            resolvedAnchor = nextAnchor;
        }
        activeUnit.SetAnchorCoord(originalAnchor);
        return resolvedAnchor;
    }

    private Vector2I ResolvePreviewChargeAnchor(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        Vector2I direction,
        int requestedDistance
    )
    {
        if (
            !HasRuntime()
            || State == null
            || !activeUnit.IsValid
            || skillDefinition == null
            || castVariantDefinition == null
        )
        {
            return activeUnit.IsValid ? activeUnit.Coord : new Vector2I(-1, -1);
        }
        if (direction == Vector2I.Zero || requestedDistance <= 0)
        {
            return activeUnit.Coord;
        }

        Vector2I resolvedAnchor = activeUnit.Coord;
        for (int stepIndex = 0; stepIndex < requestedDistance; stepIndex++)
        {
            Vector2I nextAnchor = resolvedAnchor + direction;
            if (!GridService.CanUnitStepBetweenAnchors(State, activeUnit, resolvedAnchor, nextAnchor))
            {
                break;
            }
            resolvedAnchor = nextAnchor;
        }
        return resolvedAnchor;
    }

    private bool CanPreviewChargeEnterAnchorFrom(
        BattleUnitState activeUnit,
        Vector2I currentAnchor,
        Vector2I targetAnchor
    )
    {
        if (activeUnit == null)
        {
            return false;
        }
        Vector2I originalAnchor = activeUnit.coord;
        activeUnit.SetAnchorCoord(currentAnchor);
        bool allowed = CanChargeEnterAnchor(activeUnit, targetAnchor);
        activeUnit.SetAnchorCoord(originalAnchor);
        return allowed;
    }

    private bool WouldPreviewChargeStopOnBlocker(
        BattleUnitState activeUnit,
        Vector2I nextAnchor,
        Vector2I direction
    )
    {
        if (activeUnit == null || direction == Vector2I.Zero)
        {
            return true;
        }

        var reservedCoordSet = new HashSet<Vector2I>();
        foreach (
            Vector2I reservedCoord in GridService.GetUnitTargetCoords(activeUnit, nextAnchor)
        )
        {
            reservedCoordSet.Add(reservedCoord);
        }

        var seenBlockers = new HashSet<StringName>();
        foreach (Vector2I frontierCoord in GetChargeFrontierCoords(activeUnit, nextAnchor))
        {
            BattleUnitState blocker = GridService.GetUnitAtCoord(State, frontierCoord);
            if (blocker == null || blocker.unit_id == activeUnit.unit_id || !blocker.is_alive)
            {
                continue;
            }
            if (!seenBlockers.Add(blocker.unit_id))
            {
                continue;
            }
            if (activeUnit.body_size < blocker.body_size)
            {
                return true;
            }
            if (blocker.footprint_size != Vector2I.One)
            {
                return true;
            }
            if (PickChargeSidePush(blocker, direction, reservedCoordSet).Available)
            {
                continue;
            }

            Vector2I forwardCoord = blocker.coord + direction;
            if (reservedCoordSet.Contains(forwardCoord))
            {
                return true;
            }
            if (GridService.CollectBlockingUnitIds(State, blocker, forwardCoord).Count > 0)
            {
                return true;
            }
        }
        return false;
    }

    private BattleState DuplicateStateForPreview(BattleState state)
    {
        if (state == null)
        {
            return null;
        }

        var clonedState = new BattleState
        {
            battle_id = state.battle_id,
            seed = state.seed,
            attack_roll_nonce = state.attack_roll_nonce,
            phase = state.phase,
            map_size = state.map_size,
            world_coord = state.world_coord,
            encounter_anchor_id = state.encounter_anchor_id,
            terrain_profile_id = state.terrain_profile_id,
        };
        foreach (BattleState.BattleCellEntry cellEntry in state.GetCellEntriesTyped())
        {
            clonedState.SetCell(cellEntry.Coord, cellEntry.Cell.DuplicateCell());
        }
        clonedState.RebuildCellColumns();
        foreach (BattleState.BattleUnitEntry unitEntry in state.GetUnitEntriesTyped())
        {
            clonedState.SetUnit(unitEntry.Unit.clone());
        }
        clonedState.ally_unit_ids = new Godot.Collections.Array<StringName>(state.ally_unit_ids);
        clonedState.enemy_unit_ids = new Godot.Collections.Array<StringName>(state.enemy_unit_ids);
        clonedState.timeline =
            state.timeline != null
                ? state.timeline.DuplicateState()
                : new BattleTimelineState();
        clonedState.active_unit_id = state.active_unit_id;
        clonedState.winner_faction_id = state.winner_faction_id;
        clonedState.log_entries = new Godot.Collections.Array<string>(state.log_entries);
        clonedState.SetPromotionQueue(state.PromotionQueueSnapshots);
        clonedState.modal_state = state.modal_state;
        clonedState.ReplaceTemporaryEdgeFeaturesTyped(state.GetTemporaryEdgeFeaturesTyped());
        clonedState.ClearRuntimeEdgeFaces();
        clonedState.runtime_edges_dirty = true;
        return clonedState;
    }

    private PathStepResult ApplyChargePathStepAoeEffects(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleEventBatch batch,
        HashSet<StringName> seenUnitIds
    )
    {
        CombatEffectDefinition pathStepAoeEffect = GetChargePathStepAoeEffectDefinition(
            castVariantDefinition,
            skillDefinition,
            activeUnit
        );
        if (activeUnit == null || skillDefinition == null || pathStepAoeEffect == null)
        {
            return new PathStepResult(false);
        }

        ChargePathStepAoeParameters pathStepParameters =
            ChargePathStepAoeParameters.FromEffect(pathStepAoeEffect);
        List<Vector2I> effectCoords = BuildChargeStepEffectCoords(activeUnit, pathStepAoeEffect);
        int hitCount = 0;
        int totalDamage = 0;
        int totalHealing = 0;
        int totalKillCount = 0;
        var unitHitCounts = new Dictionary<StringName, int>();
        StringName targetFilter = ResolveEffectTargetFilter(skillDefinition, pathStepAoeEffect);
        string pathStepResultLabel = GetPathStepResultLabel(pathStepAoeEffect);
        CombatEffectDefinition stageEffect =
            pathStepAoeEffect.WithEffectType(DamageEffectType);
        if (stageEffect == null)
        {
            return new PathStepResult(false);
        }
        CombatEffectDefinition[] stageEffects = { stageEffect };
        BattleLayeredBarrierService barrierService = Runtime._layered_barrier_service;
        if (barrierService != null)
        {
            effectCoords = new List<Vector2I>(
                barrierService
                    .ResolveGroundEffectBarrierClipResult(
                        activeUnit,
                        skillDefinition,
                        stageEffects,
                        Array.Empty<CombatEffectDefinition>(),
                        effectCoords,
                        batch
                    )
                    .UnitEffects.AllowedCoords
            );
        }

        foreach (BattleUnitState targetUnit in CollectUnitsInCoords(effectCoords))
        {
            if (!IsUnitValidForEffect(activeUnit, targetUnit, targetFilter))
            {
                continue;
            }
            if (
                !pathStepParameters.AllowRepeatHitsAcrossSteps
                && seenUnitIds.Contains(targetUnit.unit_id)
            )
            {
                continue;
            }
            seenUnitIds.Add(targetUnit.unit_id);

            AttackEffectResolutionResult stageResult;
            AttackCheckInput attackCheck = new(skillId: skillDefinition?.SkillId ?? new StringName(""));
            if (pathStepParameters.ResolveAsWeaponAttack)
            {
                BattleAttackCheckPolicyService attackPolicy =
                    Runtime.GetAttackCheckPolicyService();
                BattleAttackCheckPolicyContext attackContext = attackPolicy.BuildSkillDefinitionAttackContext(
                    State,
                    activeUnit,
                    targetUnit,
                    skillDefinition,
                    SkillAttackCheckMode,
                    ExecuteStage,
                    false
                );
                attackCheck = attackPolicy.BuildAttackCheck(
                    attackContext,
                    0,
                    0
                );
                stageResult = DamageResolver.ResolveAttackEffects(
                    activeUnit,
                    targetUnit,
                    stageEffects,
                    attackCheck,
                    new AttackContext
                    {
                        BattleState = State,
                        SkillId = skillDefinition?.SkillId ?? new StringName(""),
                        EventBatch = batch,
                    }
                );
            }
            else
            {
                stageResult = DamageResolver.ResolveEffects(
                    activeUnit,
                    targetUnit,
                    stageEffects,
                    DamageResolutionContext.ForSkill(skillDefinition?.SkillId ?? new StringName(""))
                );
            }
            if (pathStepParameters.ResolveAsWeaponAttack)
            {
                _skillMasteryService?.RecordTargetResult(
                    activeUnit,
                    targetUnit,
                    skillDefinition,
                    stageResult
                );
                Runtime?._apply_source_bound_weapon_bonus_mastery_grants(
                    activeUnit,
                    targetUnit,
                    stageResult,
                    batch
                );
            }

            MarkAppliedStatusesForTurnTiming(
                targetUnit,
                stageResult.StatusEffectIds
            );
            AppendResultSourceStatusEffects(batch, activeUnit, stageResult);
            if (!stageResult.Applied)
            {
                continue;
            }

            hitCount += 1;
            unitHitCounts.TryGetValue(targetUnit.unit_id, out int existingHitCount);
            unitHitCounts[targetUnit.unit_id] =
                existingHitCount + 1;
            AppendChangedUnitId(batch, targetUnit.unit_id);
            AppendChangedUnitCoords(batch, targetUnit);

            int damage = stageResult.Damage;
            int healing = stageResult.Healing;
            totalDamage += damage;
            totalHealing += healing;
            Runtime.AppendDamageResultLogLines(
                batch,
                $"{activeUnit.display_name} 的 {skillDefinition.DisplayName} {pathStepResultLabel}",
                targetUnit.display_name,
                stageResult
            );
            if (healing > 0)
            {
                batch.AddLogLine(
                    $"{activeUnit.display_name} 的 {skillDefinition.DisplayName} {pathStepResultLabel}为 {targetUnit.display_name} 恢复 {healing} 点生命。"
                );
            }
            if (!targetUnit.is_alive)
            {
                totalKillCount += 1;
                Runtime.HandleUnitDefeatedByRuntimeEffect(
                    targetUnit,
                    activeUnit,
                    batch,
                    $"{targetUnit.display_name} 被击倒。",
                    new BattleDefeatHandlingOptions(
                        recordEnemyDefeatedAchievement: true,
                        killProvenance: BattleKillProvenance.FromWeaponAttackResult(
                            activeUnit,
                            stageResult,
                            skillDefinition.SkillId
                        )
                    )
                );
            }
        }

        if (totalDamage > 0 || totalHealing > 0 || totalKillCount > 0)
        {
            Runtime.RecordSkillEffectResult(
                activeUnit,
                totalDamage,
                totalHealing,
                totalKillCount
            );
        }
        return new PathStepResult(true, hitCount, unitHitCounts);
    }

    private List<Vector2I> BuildChargeStepEffectCoords(
        BattleUnitState activeUnit,
        CombatEffectDefinition pathStepAoeEffect
    )
    {
        return activeUnit == null
            ? new List<Vector2I>()
            : BuildChargeStepEffectCoordsForAnchor(activeUnit, activeUnit.coord, pathStepAoeEffect);
    }

    private List<Vector2I> BuildChargePathAnchorCoords(
        BattleUnitState activeUnit,
        Vector2I direction,
        int distance
    )
    {
        var anchorCoords = new List<Vector2I>();
        if (activeUnit == null || direction == Vector2I.Zero || distance <= 0)
        {
            return anchorCoords;
        }

        Vector2I previewAnchor = activeUnit.coord;
        for (int step = 0; step < distance; step++)
        {
            previewAnchor += direction;
            anchorCoords.Add(previewAnchor);
        }
        return anchorCoords;
    }

    private List<Vector2I> BuildChargePathAnchorCoords(
        BattleUnitReadView activeUnit,
        Vector2I direction,
        int distance
    )
    {
        var anchorCoords = new List<Vector2I>();
        if (!activeUnit.IsValid || direction == Vector2I.Zero || distance <= 0)
        {
            return anchorCoords;
        }

        Vector2I previewAnchor = activeUnit.Coord;
        for (int step = 0; step < distance; step++)
        {
            previewAnchor += direction;
            anchorCoords.Add(previewAnchor);
        }
        return anchorCoords;
    }

    private List<Vector2I> BuildChargeStepEffectCoordsForAnchor(
        BattleUnitState activeUnit,
        Vector2I anchorCoord,
        CombatEffectDefinition pathStepAoeEffect
    )
    {
        if (!HasRuntime() || activeUnit == null || pathStepAoeEffect == null)
        {
            return new List<Vector2I>();
        }

        StringName stepShape = GetStringName(pathStepAoeEffect.Parameters, "step_shape", "diamond");
        int stepRadius = Math.Max(GetInt(pathStepAoeEffect.Parameters, "step_radius", 1), 0);
        var coordSet = new HashSet<Vector2I>();
        var effectCoords = new List<Vector2I>();
        foreach (
            Vector2I occupiedCoord in GridService.GetUnitTargetCoords(activeUnit, anchorCoord)
        )
        {
            foreach (
                Vector2I effectCoord in GridService.GetAreaCoords(
                    State,
                    occupiedCoord,
                    stepShape,
                    stepRadius,
                    Vector2I.Zero
                )
            )
            {
                if (coordSet.Add(effectCoord))
                {
                    effectCoords.Add(effectCoord);
                }
            }
        }
        return SortCoords(effectCoords);
    }

    private List<Vector2I> BuildChargeStepEffectCoordsForAnchor(
        BattleUnitReadView activeUnit,
        Vector2I anchorCoord,
        CombatEffectDefinition pathStepAoeEffect
    )
    {
        if (!HasRuntime() || !activeUnit.IsValid || pathStepAoeEffect == null)
        {
            return new List<Vector2I>();
        }

        StringName stepShape = GetStringName(pathStepAoeEffect.Parameters, "step_shape", "diamond");
        int stepRadius = Math.Max(GetInt(pathStepAoeEffect.Parameters, "step_radius", 1), 0);
        var coordSet = new HashSet<Vector2I>();
        var effectCoords = new List<Vector2I>();
        foreach (
            Vector2I occupiedCoord in GridService.GetUnitTargetCoords(activeUnit, anchorCoord)
        )
        {
            foreach (
                Vector2I effectCoord in GridService.GetAreaCoords(
                    State,
                    occupiedCoord,
                    stepShape,
                    stepRadius,
                    Vector2I.Zero
                )
            )
            {
                if (coordSet.Add(effectCoord))
                {
                    effectCoords.Add(effectCoord);
                }
            }
        }
        return SortCoords(effectCoords);
    }

    private static bool IsEffectUnlockedForSkillLevel(
        CombatEffectDefinition effectDefinition,
        int skillLevel
    )
    {
        if (effectDefinition == null)
        {
            return false;
        }
        int minLevel = Math.Max(effectDefinition.MinSkillLevel, 0);
        int maxLevel = effectDefinition.MaxSkillLevel;
        if (skillLevel < minLevel)
        {
            return false;
        }
        return maxLevel < 0 || skillLevel <= maxLevel;
    }

    private bool CanChargeEnterAnchor(BattleUnitState activeUnit, Vector2I targetAnchor)
    {
        if (!HasRuntime() || State == null || activeUnit == null)
        {
            return false;
        }

        activeUnit.RefreshFootprint();
        Vector2I delta = targetAnchor - activeUnit.coord;
        if (GridService.GetDistance(activeUnit.coord, targetAnchor) != 1)
        {
            return false;
        }

        List<Vector2I> targetCoords = GridService.GetUnitTargetCoords(activeUnit, targetAnchor);
        if (!CanChargePlaceFootprintIgnoringOccupants(activeUnit, targetCoords))
        {
            return false;
        }
        if (!CanChargeStepAcrossEdges(activeUnit, delta))
        {
            return false;
        }

        foreach (Vector2I footprintCoord in targetCoords)
        {
            BattleCellState targetCell = GridService.GetCellState(State, footprintCoord);
            if (targetCell == null)
            {
                return false;
            }
            Vector2I referenceCoord = footprintCoord - delta;
            BattleCellState referenceCell = GridService.GetCellState(State, referenceCoord);
            if (referenceCell == null)
            {
                return false;
            }
            if (Math.Abs(referenceCell.current_height - targetCell.current_height) > 1)
            {
                return false;
            }
        }
        return true;
    }

    private bool CanChargePlaceFootprintIgnoringOccupants(
        BattleUnitState activeUnit,
        IEnumerable<Vector2I> targetCoords
    )
    {
        var targetLookup = new HashSet<Vector2I>();
        foreach (Vector2I targetCoord in targetCoords)
        {
            targetLookup.Add(targetCoord);
            if (!GridService.IsInside(State, targetCoord))
            {
                return false;
            }
            BattleCellState targetCell = GridService.GetCellState(State, targetCoord);
            if (targetCell == null)
            {
                return false;
            }
            if (
                !BattleTerrainRules.CanUnitEnterTerrain(
                    targetCell.base_terrain,
                    activeUnit.movement_tags
                )
            )
            {
                return false;
            }
        }

        foreach (Vector2I targetCoord in targetCoords)
        {
            foreach (Vector2I direction in RightDownDirections)
            {
                Vector2I neighborCoord = targetCoord + direction;
                if (!targetLookup.Contains(neighborCoord))
                {
                    continue;
                }
                if (IsChargeEdgeBlocked(targetCoord, neighborCoord, true))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private bool CanChargeStepAcrossEdges(BattleUnitState activeUnit, Vector2I delta)
    {
        var frontierFromCoords = new List<Vector2I>();
        if (delta == Vector2I.Right)
        {
            for (int y = 0; y < activeUnit.footprint_size.Y; y++)
            {
                frontierFromCoords.Add(
                    activeUnit.coord + new Vector2I(activeUnit.footprint_size.X - 1, y)
                );
            }
        }
        else if (delta == Vector2I.Left)
        {
            for (int y = 0; y < activeUnit.footprint_size.Y; y++)
            {
                frontierFromCoords.Add(activeUnit.coord + new Vector2I(0, y));
            }
        }
        else if (delta == Vector2I.Down)
        {
            for (int x = 0; x < activeUnit.footprint_size.X; x++)
            {
                frontierFromCoords.Add(
                    activeUnit.coord + new Vector2I(x, activeUnit.footprint_size.Y - 1)
                );
            }
        }
        else if (delta == Vector2I.Up)
        {
            for (int x = 0; x < activeUnit.footprint_size.X; x++)
            {
                frontierFromCoords.Add(activeUnit.coord + new Vector2I(x, 0));
            }
        }
        else
        {
            return false;
        }

        foreach (Vector2I fromCoord in frontierFromCoords)
        {
            if (IsChargeEdgeBlocked(fromCoord, fromCoord + delta, false))
            {
                return false;
            }
        }
        return true;
    }

    private bool IsChargeEdgeBlocked(Vector2I fromCoord, Vector2I toCoord, bool blocksOccupancy)
    {
        BattleEdgeFaceState edgeFace = GridService.GetEdgeFace(State, fromCoord, toCoord);
        if (edgeFace == null)
        {
            return true;
        }
        if (blocksOccupancy)
        {
            if (edgeFace.BlocksOccupancy())
            {
                return true;
            }
        }
        else if (edgeFace.BlocksMove())
        {
            return true;
        }
        return edgeFace.height_difference > 1;
    }

    private ChargeBlockerResult ResolveChargeStepBlockers(
        BattleUnitState activeUnit,
        Vector2I nextAnchor,
        Vector2I direction,
        BattleEventBatch batch
    )
    {
        var reservedCoordSet = new HashSet<Vector2I>();
        foreach (
            Vector2I reservedCoord in GridService.GetUnitTargetCoords(activeUnit, nextAnchor)
        )
        {
            reservedCoordSet.Add(reservedCoord);
        }

        var seenBlockers = new HashSet<StringName>();
        foreach (Vector2I frontierCoord in GetChargeFrontierCoords(activeUnit, nextAnchor))
        {
            BattleUnitState blocker = GridService.GetUnitAtCoord(State, frontierCoord);
            if (blocker == null || blocker.unit_id == activeUnit.unit_id || !blocker.is_alive)
            {
                continue;
            }
            if (!seenBlockers.Add(blocker.unit_id))
            {
                continue;
            }
            if (activeUnit.body_size < blocker.body_size)
            {
                batch.AddLogLine(
                    $"{activeUnit.display_name} 被更大体型的 {blocker.display_name} 拦住，无法继续冲锋。"
                );
                return new ChargeBlockerResult("stop", "smaller_body");
            }
            if (blocker.footprint_size != Vector2I.One)
            {
                batch.AddLogLine(
                    $"{activeUnit.display_name} 被 {blocker.display_name} 拦住，无法继续冲锋。"
                );
                return new ChargeBlockerResult("stop", "large_blocker");
            }

            string blockerResult = ResolveChargeBlocker(
                activeUnit,
                blocker,
                direction,
                reservedCoordSet,
                batch
            );
            if (blockerResult != "continue")
            {
                return new ChargeBlockerResult(blockerResult, blockerResult);
            }
        }
        return new ChargeBlockerResult("continue", "");
    }

    private List<Vector2I> GetChargeFrontierCoords(BattleUnitState activeUnit, Vector2I nextAnchor)
    {
        var currentCoords = new HashSet<Vector2I>();
        foreach (Vector2I occupiedCoord in activeUnit.occupied_coords)
        {
            currentCoords.Add(occupiedCoord);
        }

        var frontierCoords = new List<Vector2I>();
        foreach (Vector2I targetCoord in GridService.GetUnitTargetCoords(activeUnit, nextAnchor))
        {
            if (!currentCoords.Contains(targetCoord))
            {
                frontierCoords.Add(targetCoord);
            }
        }
        return SortCoords(frontierCoords);
    }

    private string ResolveChargeBlocker(
        BattleUnitState activeUnit,
        BattleUnitState blocker,
        Vector2I direction,
        HashSet<Vector2I> reservedCoordSet,
        BattleEventBatch batch
    )
    {
        SidePushResult sidePush = PickChargeSidePush(blocker, direction, reservedCoordSet);
        if (sidePush.Available)
        {
            Vector2I blockerAnchor = blocker.coord;
            BattleBarrierInteractionResult barrierResult =
                Runtime._layered_barrier_service?.ResolveUnitBoundaryCrossingResult(
                    blocker,
                    blockerAnchor,
                    sidePush.Coord,
                    batch
                ) ?? new BattleBarrierInteractionResult(false, false);
            if (
                barrierResult.Blocked
                || !blocker.is_alive
                || blocker.coord != blockerAnchor
            )
            {
                return "stop";
            }
            List<Vector2I> previousCoords = DuplicateVector2IList(blocker.occupied_coords);
            if (GridService.MoveUnitForce(State, blocker, sidePush.Coord))
            {
                AppendChangedCoords(batch, previousCoords);
                AppendChangedUnitCoords(batch, blocker);
                AppendChangedUnitId(batch, blocker.unit_id);
                batch.AddLogLine(
                    $"{activeUnit.display_name} 的冲锋将 {blocker.display_name} 顶向侧面。"
                );
                if (sidePush.FallLayers > 0)
                {
                    ApplyPushFallDamage(
                        activeUnit,
                        blocker,
                        batch,
                        sidePush.FallLayers,
                        "侧推",
                        "坠落"
                    );
                }
                return "continue";
            }
        }

        Vector2I forwardCoord = blocker.coord + direction;
        if (!reservedCoordSet.Contains(forwardCoord))
        {
            Vector2I blockerAnchor = blocker.coord;
            if (GridService.CanPlaceUnit(State, blocker, forwardCoord))
            {
                BattleBarrierInteractionResult barrierResult =
                    Runtime._layered_barrier_service?.ResolveUnitBoundaryCrossingResult(
                        blocker,
                        blockerAnchor,
                        forwardCoord,
                        batch
                    ) ?? new BattleBarrierInteractionResult(false, false);
                if (
                    barrierResult.Blocked
                    || !blocker.is_alive
                    || blocker.coord != blockerAnchor
                )
                {
                    return "stop";
                }
            }
            List<Vector2I> previousCoords = DuplicateVector2IList(blocker.occupied_coords);
            if (GridService.MoveUnit(State, blocker, forwardCoord))
            {
                AppendChangedCoords(batch, previousCoords);
                AppendChangedUnitCoords(batch, blocker);
                AppendChangedUnitId(batch, blocker.unit_id);
                batch.AddLogLine(
                    $"{activeUnit.display_name} 的冲锋将 {blocker.display_name} 向前顶开。"
                );
                int fallLayers = CalculateFallLayersForCoord(blocker, forwardCoord);
                if (fallLayers > 0)
                {
                    ApplyPushFallDamage(activeUnit, blocker, batch, fallLayers, "前推", "坠落");
                }
                return "continue";
            }

            if (GridService.CollectBlockingUnitIds(State, blocker, forwardCoord).Count > 0)
            {
                return "stop";
            }

            BattleCellState forwardCell = GridService.GetCellState(State, forwardCoord);
            BattleCellState blockerCell = GridService.GetCellState(State, blocker.coord);
            int heightDiff =
                forwardCell != null && blockerCell != null
                    ? Math.Abs(blockerCell.current_height - forwardCell.current_height)
                    : 0;
            AttackEffectResolutionResult envDamageResult;
            string envDamageLabel;
            if (heightDiff > 1)
            {
                envDamageResult = DamageResolver.ResolveFallDamageResult(blocker, heightDiff);
                envDamageLabel = "撞向高地";
            }
            else
            {
                envDamageResult = DamageResolver.ResolveFallDamageResult(blocker, 1);
                envDamageLabel = "撞向障碍物";
            }
            int envDamage = envDamageResult.Damage;
            int envShield = envDamageResult.ShieldAbsorbed;
            if (envDamage > 0 || envShield > 0)
            {
                if (envDamage > 0)
                {
                    batch.AddLogLine(
                        $"{activeUnit.display_name} 的冲锋将 {blocker.display_name} {envDamageLabel}，受到 {envDamage} 点碰撞伤害。"
                    );
                    if (envShield > 0)
                    {
                        batch.AddLogLine(
                            $"{blocker.display_name} 的护盾吸收了 {envShield} 点碰撞伤害。"
                        );
                    }
                }
                else
                {
                    batch.AddLogLine(
                        $"{activeUnit.display_name} 的冲锋将 {blocker.display_name} {envDamageLabel}，但被护盾吸收了 {envShield} 点碰撞伤害。"
                    );
                }
                if (envDamageResult.ShieldBroken)
                {
                    batch.AddLogLine($"{blocker.display_name} 的护盾被击碎。");
                }
                AppendChangedUnitId(batch, blocker.unit_id);
                if (!blocker.is_alive)
                {
                    Runtime.HandleUnitDefeatedByRuntimeEffect(
                        blocker,
                        activeUnit,
                        batch,
                        $"{blocker.display_name} 被击倒。",
                        new BattleDefeatHandlingOptions(recordEnemyDefeatedAchievement: true)
                    );
                }
            }
            return "stop";
        }

        return "stop";
    }

    private void ApplyPushFallDamage(
        BattleUnitState activeUnit,
        BattleUnitState blocker,
        BattleEventBatch batch,
        int fallLayers,
        string pushLabel,
        string damageLabel
    )
    {
        AttackEffectResolutionResult fallDamageResult = DamageResolver.ResolveFallDamageResult(
            blocker,
            fallLayers
        );
        int fallDamage = fallDamageResult.Damage;
        int shieldAbsorbed = fallDamageResult.ShieldAbsorbed;
        if (fallDamage <= 0 && shieldAbsorbed <= 0)
        {
            return;
        }

        if (fallDamage > 0)
        {
            batch.AddLogLine(
                $"{activeUnit.display_name} 的{pushLabel}使 {blocker.display_name} 跌落 {fallLayers} 层，受到 {fallDamage} 点{damageLabel}伤害。"
            );
            if (shieldAbsorbed > 0)
            {
                batch.AddLogLine(
                    $"{blocker.display_name} 的护盾吸收了 {shieldAbsorbed} 点{damageLabel}伤害。"
                );
            }
        }
        else
        {
            batch.AddLogLine(
                $"{activeUnit.display_name} 的{pushLabel}使 {blocker.display_name} 跌落 {fallLayers} 层，但被护盾吸收了 {shieldAbsorbed} 点{damageLabel}伤害。"
            );
        }
        if (fallDamageResult.ShieldBroken)
        {
            batch.AddLogLine($"{blocker.display_name} 的护盾被击碎。");
        }
        AppendChangedUnitId(batch, blocker.unit_id);
        if (!blocker.is_alive)
        {
            Runtime.HandleUnitDefeatedByRuntimeEffect(
                blocker,
                activeUnit,
                batch,
                $"{blocker.display_name} 被击倒。",
                new BattleDefeatHandlingOptions(recordEnemyDefeatedAchievement: true)
            );
        }
    }

    private int CalculateFallLayersForCoord(BattleUnitState unit, Vector2I targetCoord)
    {
        if (unit == null || !HasRuntime())
        {
            return 0;
        }

        BattleCellState currentCell = GridService.GetCellState(State, unit.coord);
        BattleCellState targetCell = GridService.GetCellState(State, targetCoord);
        if (currentCell == null || targetCell == null)
        {
            return 0;
        }
        return Math.Max(currentCell.current_height - targetCell.current_height, 0);
    }

    private SidePushResult PickChargeSidePush(
        BattleUnitState blocker,
        Vector2I direction,
        HashSet<Vector2I> reservedCoordSet
    )
    {
        if (blocker == null)
        {
            return SidePushResult.Unavailable;
        }
        BattleCellState blockerCell = GridService.GetCellState(State, blocker.coord);
        if (blockerCell == null)
        {
            return SidePushResult.Unavailable;
        }

        int currentHeight = blockerCell.current_height;
        var lowerCandidates = new List<SidePushResult>();
        var levelCandidates = new List<SidePushResult>();
        foreach (Vector2I sideDirection in GetSideDirectionsForCharge(direction))
        {
            Vector2I sideCoord = blocker.coord + sideDirection;
            if (reservedCoordSet.Contains(sideCoord))
            {
                continue;
            }
            if (
                !GridService.CanPlaceFootprint(
                    State,
                    sideCoord,
                    blocker.footprint_size,
                    blocker.unit_id,
                    null
                )
            )
            {
                continue;
            }
            BattleCellState sideCell = GridService.GetCellState(State, sideCoord);
            if (sideCell == null)
            {
                continue;
            }
            int sideHeight = sideCell.current_height;
            if (sideHeight > currentHeight)
            {
                continue;
            }

            var candidate = new SidePushResult(
                true,
                sideCoord,
                Math.Max(currentHeight - sideHeight, 0)
            );
            if (sideHeight < currentHeight)
            {
                lowerCandidates.Add(candidate);
            }
            else
            {
                levelCandidates.Add(candidate);
            }
        }
        if (lowerCandidates.Count > 0)
        {
            return lowerCandidates[0];
        }
        if (levelCandidates.Count > 0)
        {
            return levelCandidates[0];
        }
        return SidePushResult.Unavailable;
    }

    private static IEnumerable<Vector2I> GetSideDirectionsForCharge(Vector2I direction)
    {
        return direction.X != 0
            ? new[] { Vector2I.Up, Vector2I.Down }
            : new[] { Vector2I.Left, Vector2I.Right };
    }

    private TrapResult TriggerChargeTrap(BattleUnitState activeUnit)
    {
        if (!HasRuntime() || activeUnit == null)
        {
            return TrapResult.NotTriggered;
        }

        foreach (Vector2I occupiedCoord in SortCoords(activeUnit.occupied_coords))
        {
            BattleCellState cell = GridService.GetCellState(State, occupiedCoord);
            if (cell == null || cell.terrain_effect_ids.Count == 0)
            {
                continue;
            }
            bool removedAny = false;
            foreach (
                StringName terrainEffectId in new Godot.Collections.Array<StringName>(
                    cell.terrain_effect_ids
                )
            )
            {
                if (
                    terrainEffectId
                        .ToString()
                        .StartsWith(TrapEffectPrefix, StringComparison.Ordinal)
                )
                {
                    cell.terrain_effect_ids.Remove(terrainEffectId);
                    removedAny = true;
                }
            }
            if (removedAny)
            {
                return new TrapResult(true, occupiedCoord);
            }
        }
        return TrapResult.NotTriggered;
    }

    private static string FormatChargeDirection(Vector2I direction)
    {
        if (direction == Vector2I.Left)
        {
            return "左";
        }
        if (direction == Vector2I.Right)
        {
            return "右";
        }
        if (direction == Vector2I.Up)
        {
            return "上";
        }
        if (direction == Vector2I.Down)
        {
            return "下";
        }
        return "前";
    }

    private static ChargeTargetInfo ResolveChargeTarget(
        BattleUnitState activeUnit,
        Vector2I targetCoord
    )
    {
        if (activeUnit == null)
        {
            return ChargeTargetInfo.Invalid;
        }

        activeUnit.RefreshFootprint();
        Vector2I footprintSize = activeUnit.footprint_size;
        int minX = activeUnit.coord.X;
        int maxX = activeUnit.coord.X + footprintSize.X - 1;
        int minY = activeUnit.coord.Y;
        int maxY = activeUnit.coord.Y + footprintSize.Y - 1;

        if (targetCoord.Y >= minY && targetCoord.Y <= maxY)
        {
            if (targetCoord.X < minX)
            {
                return new ChargeTargetInfo(true, Vector2I.Left, minX - targetCoord.X);
            }
            if (targetCoord.X > maxX)
            {
                return new ChargeTargetInfo(true, Vector2I.Right, targetCoord.X - maxX);
            }
        }
        if (targetCoord.X >= minX && targetCoord.X <= maxX)
        {
            if (targetCoord.Y < minY)
            {
                return new ChargeTargetInfo(true, Vector2I.Up, minY - targetCoord.Y);
            }
            if (targetCoord.Y > maxY)
            {
                return new ChargeTargetInfo(true, Vector2I.Down, targetCoord.Y - maxY);
            }
        }
        return ChargeTargetInfo.Invalid;
    }

    private static ChargeTargetInfo ResolveChargeTarget(
        BattleUnitReadView activeUnit,
        Vector2I targetCoord
    )
    {
        if (!activeUnit.IsValid)
        {
            return ChargeTargetInfo.Invalid;
        }

        Vector2I footprintSize = activeUnit.FootprintSize;
        int minX = activeUnit.Coord.X;
        int maxX = activeUnit.Coord.X + footprintSize.X - 1;
        int minY = activeUnit.Coord.Y;
        int maxY = activeUnit.Coord.Y + footprintSize.Y - 1;

        if (targetCoord.Y >= minY && targetCoord.Y <= maxY)
        {
            if (targetCoord.X < minX)
            {
                return new ChargeTargetInfo(true, Vector2I.Left, minX - targetCoord.X);
            }
            if (targetCoord.X > maxX)
            {
                return new ChargeTargetInfo(true, Vector2I.Right, targetCoord.X - maxX);
            }
        }
        if (targetCoord.X >= minX && targetCoord.X <= maxX)
        {
            if (targetCoord.Y < minY)
            {
                return new ChargeTargetInfo(true, Vector2I.Up, minY - targetCoord.Y);
            }
            if (targetCoord.Y > maxY)
            {
                return new ChargeTargetInfo(true, Vector2I.Down, targetCoord.Y - maxY);
            }
        }
        return ChargeTargetInfo.Invalid;
    }

    private List<Vector2I> BuildChargePreviewCoords(
        BattleUnitState activeUnit,
        Vector2I direction,
        int distance
    )
    {
        if (activeUnit == null || direction == Vector2I.Zero || distance <= 0)
        {
            return new List<Vector2I>();
        }

        var seenCoords = new HashSet<Vector2I>();
        var previewCoords = new List<Vector2I>();
        Vector2I previewAnchor = activeUnit.coord;
        for (int step = 0; step < distance; step++)
        {
            previewAnchor += direction;
            foreach (
                Vector2I occupiedCoord in GridService.GetUnitTargetCoords(
                    activeUnit,
                    previewAnchor
                )
            )
            {
                if (seenCoords.Add(occupiedCoord))
                {
                    previewCoords.Add(occupiedCoord);
                }
            }
        }
        return SortCoords(previewCoords);
    }

    private List<Vector2I> BuildChargePreviewCoords(
        BattleUnitReadView activeUnit,
        Vector2I direction,
        int distance
    )
    {
        if (!activeUnit.IsValid || direction == Vector2I.Zero || distance <= 0)
        {
            return new List<Vector2I>();
        }

        var seenCoords = new HashSet<Vector2I>();
        var previewCoords = new List<Vector2I>();
        Vector2I previewAnchor = activeUnit.Coord;
        for (int step = 0; step < distance; step++)
        {
            previewAnchor += direction;
            foreach (
                Vector2I occupiedCoord in GridService.GetUnitTargetCoords(
                    activeUnit,
                    previewAnchor
                )
            )
            {
                if (seenCoords.Add(occupiedCoord))
                {
                    previewCoords.Add(occupiedCoord);
                }
            }
        }
        return SortCoords(previewCoords);
    }

    private int GetChargeMaxDistance(
        BattleUnitState activeUnit,
        CombatCastVariantDefinition castVariant
    )
    {
        CombatEffectDefinition chargeEffect = GetChargeEffectDefinition(castVariant);
        if (chargeEffect == null || !HasRuntime())
        {
            return 0;
        }

        StringName skillId = GetStringName(chargeEffect.Parameters, "skill_id", "charge");
        int skillLevel = GetUnitSkillLevel(activeUnit, skillId);
        int maxDistance = Math.Max(GetInt(chargeEffect.Parameters, "base_distance", 3), 0);
        IReadOnlyDictionary<string, object> distanceByLevel = GetVariantDictionary(
            chargeEffect.Parameters,
            "distance_by_level"
        );
        foreach (KeyValuePair<string, object> entry in distanceByLevel)
        {
            if (!int.TryParse(entry.Key, out int levelBreakpoint))
            {
                continue;
            }
            if (skillLevel >= levelBreakpoint && TryReadInt(entry.Value, out int distance))
                maxDistance = Math.Max(maxDistance, distance);
        }
        return maxDistance;
    }

    private int GetChargeMaxDistance(
        BattleUnitReadView activeUnit,
        CombatCastVariantDefinition castVariant
    )
    {
        CombatEffectDefinition chargeEffect = GetChargeEffectDefinition(castVariant);
        if (chargeEffect == null || !HasRuntime())
        {
            return 0;
        }

        StringName skillId = GetStringName(chargeEffect.Parameters, "skill_id", "charge");
        int skillLevel = activeUnit.GetKnownSkillLevel(skillId);
        int maxDistance = Math.Max(GetInt(chargeEffect.Parameters, "base_distance", 3), 0);
        IReadOnlyDictionary<string, object> distanceByLevel = GetVariantDictionary(
            chargeEffect.Parameters,
            "distance_by_level"
        );
        foreach (KeyValuePair<string, object> entry in distanceByLevel)
        {
            if (!int.TryParse(entry.Key, out int levelBreakpoint))
            {
                continue;
            }
            if (skillLevel >= levelBreakpoint && TryReadInt(entry.Value, out int distance))
                maxDistance = Math.Max(maxDistance, distance);
        }
        return maxDistance;
    }

    private bool HasRuntime()
    {
        return Runtime != null;
    }

    private BattleState State => Runtime?._state;

    private BattleGridService GridService => Runtime?._grid_service;

    private BattleDamageResolver DamageResolver => Runtime?._damage_resolver;

    private StringName ResolveEffectTargetFilter(
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition
    )
    {
        return Runtime?._skill_resolution_rules?.ResolveEffectTargetFilter(
            skillDefinition,
            effectDefinition
        ) ?? new StringName("");
    }

    private bool IsUnitValidForEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        StringName targetFilter
    )
    {
        return Runtime != null
            && Runtime.IsUnitValidForEffect(sourceUnit, targetUnit, targetFilter);
    }

    private IEnumerable<BattleUnitState> CollectUnitsInCoords(IReadOnlyList<Vector2I> effectCoords)
    {
        if (Runtime == null)
        {
            yield break;
        }
        Runtime._ensure_sidecars_ready();
        foreach (
            BattleUnitState unit in Runtime._skill_orchestrator.CollectUnitsInCoords(effectCoords)
        )
        {
            if (unit != null)
            {
                yield return unit;
            }
        }
    }

    private int GetUnitSkillLevel(BattleUnitState unit, StringName skillId)
    {
        return Runtime?.GetUnitSkillLevel(unit, skillId) ?? 0;
    }

    private string FormatSkillVariantLabel(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (skillDefinition == null)
        {
            return "";
        }
        if (castVariant == null || string.IsNullOrEmpty(castVariant.DisplayName))
        {
            return skillDefinition.DisplayName;
        }
        return $"{skillDefinition.DisplayName}·{castVariant.DisplayName}";
    }

    private void MarkAppliedStatusesForTurnTiming(
        BattleUnitState targetUnit,
        IReadOnlyList<StringName> statusEffectIds
    )
    {
        Runtime?.MarkAppliedStatusesForTurnTiming(
            targetUnit,
            statusEffectIds
        );
    }

    private void AppendResultSourceStatusEffects(
        BattleEventBatch batch,
        BattleUnitState sourceUnit,
        GDictionary result
    )
    {
        Runtime?.AppendResultSourceStatusEffects(batch, sourceUnit, result);
    }

    private void AppendResultSourceStatusEffects(
        BattleEventBatch batch,
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result
    )
    {
        Runtime?.AppendResultSourceStatusEffects(batch, sourceUnit, result);
    }

    private static void MergeBatch(BattleEventBatch targetBatch, BattleEventBatch sourceBatch)
    {
        if (targetBatch == null || sourceBatch == null)
        {
            return;
        }
        AppendChangedCoords(targetBatch, sourceBatch.changed_coords);
        foreach (StringName unitId in sourceBatch.ChangedUnitIdsTyped)
        {
            AppendChangedUnitId(targetBatch, unitId);
        }
        foreach (string logLine in sourceBatch.LogLinesTyped)
        {
            targetBatch.AddLogLine(logLine);
        }
        foreach (IReadOnlyDictionary<string, object> reportEntry in sourceBatch.ReportEntriesTyped)
        {
            targetBatch.AddReportEntry(reportEntry);
        }
    }

    private static void AppendChangedCoord(BattleEventBatch batch, Vector2I coord)
    {
        if (batch == null || batch.ContainsChangedCoord(coord))
        {
            return;
        }
        batch.AddChangedCoord(coord);
    }

    private static void AppendChangedCoords(BattleEventBatch batch, IEnumerable<Vector2I> coords)
    {
        if (coords == null)
        {
            return;
        }
        foreach (Vector2I coord in coords)
        {
            AppendChangedCoord(batch, coord);
        }
    }

    private static void AppendChangedUnitId(BattleEventBatch batch, StringName unitId)
    {
        if (batch == null || IsEmpty(unitId) || batch.ContainsChangedUnitId(unitId))
        {
            return;
        }
        batch.AddChangedUnitId(unitId);
    }

    private static void AppendChangedUnitCoords(BattleEventBatch batch, BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return;
        }
        unitState.RefreshFootprint();
        AppendChangedCoords(batch, unitState.occupied_coords);
    }

    private static List<Vector2I> DuplicateVector2IList(IEnumerable<Vector2I> values)
    {
        var result = new List<Vector2I>();
        if (values == null)
        {
            return result;
        }
        foreach (Vector2I value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static List<Vector2I> SortCoords(IEnumerable<Vector2I> coords)
    {
        var sorted = new List<Vector2I>();
        if (coords != null)
        {
            sorted.AddRange(coords);
        }
        sorted.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        return sorted;
    }

    private static IReadOnlyDictionary<string, object> GetVariantDictionary(
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        return source != null
            && !string.IsNullOrEmpty(key)
            && source.TryGetValue(key, out object value)
            && value is IReadOnlyDictionary<string, object> dictionary
                ? dictionary
                : EmptyParameters;
    }

    private static int GetInt(
        IReadOnlyDictionary<string, object> source,
        string key,
        int fallback = 0
    )
    {
        if (!TryResolveStringKey(source, key, out object value))
        {
            return fallback;
        }
        return TryReadInt(value, out int result) ? result : fallback;
    }

    private static string GetString(
        IReadOnlyDictionary<string, object> source,
        string key,
        string fallback = ""
    )
    {
        if (!TryResolveStringKey(source, key, out object value))
        {
            return fallback;
        }
        string result = value switch
        {
            string text => text,
            StringName stringName => stringName.ToString(),
            _ => "",
        };
        return string.IsNullOrEmpty(result) ? fallback : result;
    }

    private static StringName GetStringName(
        IReadOnlyDictionary<string, object> source,
        string key,
        StringName fallback = default
    )
    {
        if (!TryResolveStringKey(source, key, out object value))
        {
            return fallback;
        }
        StringName result = value switch
        {
            StringName stringName => stringName,
            string text => new StringName(text),
            _ => "",
        };
        return result != "" ? result : fallback;
    }

    private static bool TryResolveStringKey(
        IReadOnlyDictionary<string, object> source,
        string key,
        out object value
    )
    {
        value = null;
        return source != null
            && !string.IsNullOrEmpty(key)
            && source.TryGetValue(key, out value);
    }

    private static bool TryReadInt(object value, out int result)
    {
        result = 0;
        switch (value)
        {
            case byte byteValue:
                result = byteValue;
                return true;
            case short shortValue:
                result = shortValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                result = (int)longValue;
                return true;
            case float floatValue when floatValue >= int.MinValue && floatValue <= int.MaxValue:
                result = (int)floatValue;
                return true;
            case double doubleValue when doubleValue >= int.MinValue && doubleValue <= int.MaxValue:
                result = (int)doubleValue;
                return true;
            case string text:
                return int.TryParse(text, out result);
            case StringName stringName:
                return int.TryParse(stringName.ToString(), out result);
            default:
                return false;
        }
    }

    private static readonly IReadOnlyDictionary<string, object> EmptyParameters =
        new Dictionary<string, object>(StringComparer.Ordinal);

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static BattleRuntimeModule ResolveWeakRef(
        WeakReference<BattleRuntimeModule> weakRef
    )
    {
        if (weakRef == null || !weakRef.TryGetTarget(out BattleRuntimeModule target))
        {
            return null;
        }
        return target;
    }

    private static readonly Vector2I[] RightDownDirections = { Vector2I.Right, Vector2I.Down };

    private readonly struct PathStepResult
    {
        public readonly bool Triggered;
        public readonly int HitCount;
        public readonly Dictionary<StringName, int> UnitHitCounts;

        public PathStepResult(
            bool triggered,
            int hitCount = 0,
            Dictionary<StringName, int> unitHitCounts = null
        )
        {
            Triggered = triggered;
            HitCount = hitCount;
            UnitHitCounts = unitHitCounts ?? new Dictionary<StringName, int>();
        }
    }

    private readonly struct ChargeBlockerResult
    {
        public readonly string Result;
        public readonly string Reason;

        public ChargeBlockerResult(string result, string reason)
        {
            Result = result;
            Reason = reason;
        }
    }

    private readonly struct SidePushResult
    {
        public static readonly SidePushResult Unavailable = new(false, Vector2I.Zero, 0);

        public readonly bool Available;
        public readonly Vector2I Coord;
        public readonly int FallLayers;

        public SidePushResult(bool available, Vector2I coord, int fallLayers)
        {
            Available = available;
            Coord = coord;
            FallLayers = fallLayers;
        }
    }

    private readonly struct TrapResult
    {
        public static readonly TrapResult NotTriggered = new(false, Vector2I.Zero);

        public readonly bool Triggered;
        public readonly Vector2I Coord;

        public TrapResult(
            bool triggered,
            Vector2I coord
        )
        {
            Triggered = triggered;
            Coord = coord;
        }
    }

    private readonly struct ChargeTargetInfo
    {
        public static readonly ChargeTargetInfo Invalid = new(false, Vector2I.Zero, 0);

        public readonly bool Valid;
        public readonly Vector2I Direction;
        public readonly int Distance;

        public ChargeTargetInfo(bool valid, Vector2I direction, int distance)
        {
            Valid = valid;
            Direction = direction;
            Distance = distance;
        }
    }
}
