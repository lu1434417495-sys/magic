using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

[GlobalClass]
public partial class BattleChargeResolver : RefCounted
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
        public static ChargePathStepAoeParameters FromEffect(CombatEffectDef effectDef)
        {
            return new ChargePathStepAoeParameters(
                effectDef?.allow_repeat_hits_across_steps ?? false,
                effectDef?.resolve_as_weapon_attack ?? false
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

    public void setup(BattleRuntimeModule runtime, BattleSkillMasteryService skill_mastery_service)
    {
        Runtime = runtime;
        _skillMasteryService = skill_mastery_service;
    }

    public void dispose()
    {
        Runtime = null;
        _skillMasteryService = null;
    }

    public bool handle_charge_skill_command(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GDictionary validation,
        BattleEventBatch batch
    )
    {
        return handle_charge_skill_command_result(
            active_unit,
            skill_def,
            cast_variant,
            BattleGroundSkillValidationResult.FromDictionary(validation),
            batch
        );
    }

    public bool handle_charge_skill_command_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleGroundSkillValidationResult validation,
        BattleEventBatch batch
    )
    {
        if (
            !HasRuntime()
            || active_unit == null
            || skill_def == null
            || cast_variant == null
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
                chargeBatch.log_lines.Add(
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

            GVector2IArray previousCoords = DuplicateVector2IArray(active_unit.occupied_coords);
            if (!GridService.move_unit(State, active_unit, nextAnchor))
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
                skill_def,
                cast_variant,
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
                    skill_def != null && HasRuntime()
                        ? GetUnitSkillLevel(active_unit, skill_def.skill_id)
                        : 0;
                CombatEffectDef chargeEffect = get_charge_effect_def(cast_variant);
                int trapImmunityLevel =
                    chargeEffect != null
                        ? GetInt(chargeEffect.@params, "trap_immunity_level", 999)
                        : 999;
                AppendChangedCoord(chargeBatch, trapCoord);
                if (skillLevel >= trapImmunityLevel)
                {
                    chargeBatch.log_lines.Add(
                        $"{active_unit.display_name} 在 ({trapCoord.X}, {trapCoord.Y}) 踩中陷阱，但 7 级冲锋免疫中断。"
                    );
                }
                else
                {
                    chargeBatch.log_lines.Add(
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
            CombatEffectDef pathStepAoeEffect = get_charge_path_step_aoe_effect_def(
                cast_variant,
                skill_def,
                active_unit
            );
            batch.log_lines.Add(
                $"{active_unit.display_name} 使用 {FormatSkillVariantLabel(skill_def, cast_variant)}，向{FormatChargeDirection(direction)}冲锋 {movedSteps} 格。"
            );
            if (pathStepTriggerCount > 0)
            {
                batch.log_lines.Add(
                    $"{active_unit.display_name} 沿途触发 {pathStepTriggerCount} 次{GetPathStepLogLabel(pathStepAoeEffect)}，共命中 {pathStepHitCount} 个单位。"
                );
            }
            ApplyRepeatHitStatusEffects(
                active_unit,
                skill_def,
                pathStepAoeEffect,
                totalUnitHitCounts,
                batch
            );
            _skillMasteryService?.record_mastery_amount(skill_def, movedSteps);
            return true;
        }

        if (chargeBatch.log_lines.Count > 0 || !string.IsNullOrEmpty(stopReason))
        {
            batch.log_lines.Add(
                $"{active_unit.display_name} 使用 {FormatSkillVariantLabel(skill_def, cast_variant)}，但在起步时被拦下。"
            );
            return true;
        }
        return false;
    }

    public GDictionary validate_charge_command(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GVector2IArray normalized_coords,
        GDictionary base_result
    )
    {
        return validate_charge_command_result(
                active_unit,
                skill_def,
                cast_variant,
                normalized_coords,
                BattleGroundSkillValidationResult.FromDictionary(base_result)
            )
            .ToDictionary();
    }

    public BattleGroundSkillValidationResult validate_charge_command_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GVector2IArray normalized_coords,
        BattleGroundSkillValidationResult base_result
    )
    {
        if (
            !HasRuntime()
            || active_unit == null
            || skill_def == null
            || cast_variant == null
            || normalized_coords == null
            || normalized_coords.Count == 0
        )
        {
            return base_result;
        }

        Vector2I targetCoord = normalized_coords[0];
        if (!GridService.is_inside(State, targetCoord))
        {
            return base_result with { Message = "目标地格超出战场范围。" };
        }

        ChargeTargetInfo targetInfo = ResolveChargeTarget(active_unit, targetCoord);
        if (!targetInfo.Valid)
        {
            return base_result with { Message = "冲锋只能选择当前单位同一行或同一列的目标地格。" };
        }

        int maxDistance = GetChargeMaxDistance(active_unit, cast_variant);
        int chargeDistance = targetInfo.Distance;
        if (chargeDistance > maxDistance)
        {
            return base_result with { Message = $"目标地格超出当前冲锋距离 {maxDistance}。" };
        }

        Vector2I chargeDirection = targetInfo.Direction;
        return BattleGroundSkillValidationResult.AllowedResult(
            "可施放；若途中受阻会在当前可达位置停下。",
            new[] { targetCoord },
            ToVector2IList(BuildChargePreviewCoords(active_unit, chargeDirection, chargeDistance)),
            chargeDirection,
            chargeDistance,
            ResolvePreviewChargeAnchor(
                active_unit,
                skill_def,
                cast_variant,
                chargeDirection,
                chargeDistance
            )
        );
    }

    public GVector2IArray build_charge_step_aoe_preview_coords(
        BattleUnitState active_unit,
        Vector2I direction,
        int distance,
        CombatEffectDef path_step_aoe_effect
    )
    {
        var coords = new List<Vector2I>();
        if (
            !HasRuntime()
            || active_unit == null
            || direction == Vector2I.Zero
            || distance <= 0
            || path_step_aoe_effect == null
        )
        {
            return new GVector2IArray();
        }

        var coordSet = new HashSet<Vector2I>();
        foreach (
            Vector2I anchorCoord in BuildChargePathAnchorCoords(active_unit, direction, distance)
        )
        {
            foreach (
                Vector2I effectCoord in BuildChargeStepEffectCoordsForAnchor(
                    active_unit,
                    anchorCoord,
                    path_step_aoe_effect
                )
            )
            {
                if (coordSet.Add(effectCoord))
                {
                    coords.Add(effectCoord);
                }
            }
        }
        return SortCoords(coords);
    }

    public CombatEffectDef get_charge_path_step_aoe_effect_def(
        CombatCastVariantDef cast_variant,
        SkillDef skill_def,
        BattleUnitState active_unit
    )
    {
        if (cast_variant == null)
        {
            return null;
        }

        int skillLevel = -1;
        if (skill_def != null && active_unit != null && HasRuntime())
        {
            skillLevel = GetUnitSkillLevel(active_unit, skill_def.skill_id);
        }

        foreach (CombatEffectDef effectDef in cast_variant.effect_defs)
        {
            if (effectDef == null || effectDef.effect_type != PathStepAoeEffectType)
            {
                continue;
            }
            if (skillLevel >= 0 && !IsEffectUnlockedForSkillLevel(effectDef, skillLevel))
            {
                continue;
            }
            return effectDef;
        }
        return null;
    }

    public bool is_charge_option(CombatCastVariantDef cast_variant)
    {
        return get_charge_effect_def(cast_variant) != null;
    }

    public CombatEffectDef get_charge_effect_def(CombatCastVariantDef cast_variant)
    {
        if (cast_variant == null)
        {
            return null;
        }
        foreach (CombatEffectDef effectDef in cast_variant.effect_defs)
        {
            if (effectDef != null && effectDef.effect_type == ChargeEffectType)
            {
                return effectDef;
            }
        }
        return null;
    }

    private void ApplyRepeatHitStatusEffects(
        BattleUnitState activeUnit,
        SkillDef skillDef,
        CombatEffectDef pathStepAoeEffect,
        Dictionary<StringName, int> totalUnitHitCounts,
        BattleEventBatch batch
    )
    {
        if (activeUnit == null || skillDef == null || pathStepAoeEffect == null || batch == null)
        {
            return;
        }

        GDictionary parameters = pathStepAoeEffect.@params ?? new GDictionary();
        StringName statusId = GetStringName(parameters, "repeat_hit_status_id");
        if (IsEmpty(statusId))
        {
            return;
        }

        int minSkillLevel = Math.Max(
            GetInt(parameters, "repeat_hit_status_min_skill_level"),
            0
        );
        int skillLevel = HasRuntime() ? GetUnitSkillLevel(activeUnit, skillDef.skill_id) : 0;
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

        GDictionary extraStatusParams = new();
        if (
            TryGetValue(parameters, "repeat_hit_status_params", out Variant rawExtraParams)
            && rawExtraParams.VariantType == Variant.Type.Dictionary
        )
        {
            extraStatusParams = rawExtraParams.AsGodotDictionary().Duplicate(true);
        }

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

            var statusEffect = new CombatEffectDef
            {
                effect_type = StatusEffectType,
                status_id = statusId,
                power = statusPower,
                duration_tu = statusDurationTu,
                @params = extraStatusParams.Duplicate(true),
            };
            BattleStatusEffectState statusEntry = BattleStatusSemanticTable.merge_status_typed(
                statusEffect,
                activeUnit.unit_id,
                targetUnit.get_status_effect(statusId)
            );
            if (statusEntry == null)
            {
                continue;
            }

            targetUnit.set_status_effect(statusEntry);
            AppendChangedUnitId(batch, targetUnit.unit_id);
            string logLine = FormatRepeatHitStatusLog(
                parameters,
                targetUnit,
                skillDef,
                hitCount,
                statusId
            );
            if (!string.IsNullOrEmpty(logLine))
            {
                batch.log_lines.Add(logLine);
            }
        }
    }

    private string FormatRepeatHitStatusLog(
        GDictionary parameters,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        int hitCount,
        StringName statusId
    )
    {
        if (targetUnit == null || skillDef == null)
        {
            return "";
        }

        string template = GetString(parameters, "repeat_hit_status_log_template", "").StripEdges();
        if (string.IsNullOrEmpty(template))
        {
            return $"{targetUnit.display_name} 被 {skillDef.display_name} 连续命中 {hitCount} 次，受到 {statusId}。";
        }

        return template
            .Replace("{target}", targetUnit.display_name)
            .Replace("{skill}", skillDef.display_name)
            .Replace("{hit_count}", hitCount.ToString())
            .Replace("{status_id}", statusId.ToString());
    }

    private string GetPathStepLogLabel(CombatEffectDef pathStepAoeEffect)
    {
        if (pathStepAoeEffect == null || pathStepAoeEffect.@params == null)
        {
            return "路径攻击";
        }
        string label = GetString(pathStepAoeEffect.@params, "path_step_log_label", "路径攻击")
            .StripEdges();
        return string.IsNullOrEmpty(label) ? "路径攻击" : label;
    }

    private string GetPathStepResultLabel(CombatEffectDef pathStepAoeEffect)
    {
        return $"沿途{GetPathStepLogLabel(pathStepAoeEffect)}";
    }

    private Vector2I ResolvePreviewChargeAnchor(
        BattleUnitState activeUnit,
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        Vector2I direction,
        int requestedDistance
    )
    {
        if (
            !HasRuntime()
            || State == null
            || activeUnit == null
            || skillDef == null
            || castVariant == null
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
            activeUnit.set_anchor_coord(resolvedAnchor);
            if (WouldPreviewChargeStopOnBlocker(activeUnit, nextAnchor, direction))
            {
                break;
            }
            resolvedAnchor = nextAnchor;
        }
        activeUnit.set_anchor_coord(originalAnchor);
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
        activeUnit.set_anchor_coord(currentAnchor);
        bool allowed = CanChargeEnterAnchor(activeUnit, targetAnchor);
        activeUnit.set_anchor_coord(originalAnchor);
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
            Vector2I reservedCoord in GridService.get_unit_target_coords(activeUnit, nextAnchor)
        )
        {
            reservedCoordSet.Add(reservedCoord);
        }

        var seenBlockers = new HashSet<StringName>();
        foreach (Vector2I frontierCoord in GetChargeFrontierCoords(activeUnit, nextAnchor))
        {
            BattleUnitState blocker = GridService.get_unit_at_coord(State, frontierCoord);
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
            if (GridService.collect_blocking_unit_ids(State, blocker, forwardCoord).Count > 0)
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
            clonedState.cells[cellEntry.Coord] = cellEntry.Cell.duplicate_cell();
        }
        clonedState.cell_columns = BattleCellState.build_columns_from_surface_cells(
            clonedState.cells
        );
        foreach (BattleState.BattleUnitEntry unitEntry in state.GetUnitEntriesTyped())
        {
            clonedState.units[unitEntry.UnitId] = unitEntry.Unit.clone();
        }
        clonedState.ally_unit_ids = new Godot.Collections.Array<StringName>(state.ally_unit_ids);
        clonedState.enemy_unit_ids = new Godot.Collections.Array<StringName>(state.enemy_unit_ids);
        clonedState.timeline =
            state.timeline != null
                ? state.timeline.duplicate_state()
                : new BattleTimelineState();
        clonedState.active_unit_id = state.active_unit_id;
        clonedState.winner_faction_id = state.winner_faction_id;
        clonedState.log_entries = new Godot.Collections.Array<string>(state.log_entries);
        clonedState.promotion_queue = state.promotion_queue.Duplicate(true);
        clonedState.modal_state = state.modal_state;
        clonedState.runtime_edge_faces = new GDictionary();
        clonedState.runtime_edges_dirty = true;
        return clonedState;
    }

    private PathStepResult ApplyChargePathStepAoeEffects(
        BattleUnitState activeUnit,
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleEventBatch batch,
        HashSet<StringName> seenUnitIds
    )
    {
        CombatEffectDef pathStepAoeEffect = get_charge_path_step_aoe_effect_def(
            castVariant,
            skillDef,
            activeUnit
        );
        if (activeUnit == null || skillDef == null || pathStepAoeEffect == null)
        {
            return new PathStepResult(false);
        }

        ChargePathStepAoeParameters pathStepParameters =
            ChargePathStepAoeParameters.FromEffect(pathStepAoeEffect);
        GVector2IArray effectCoords = BuildChargeStepEffectCoords(activeUnit, pathStepAoeEffect);
        int hitCount = 0;
        int totalDamage = 0;
        int totalHealing = 0;
        int totalKillCount = 0;
        var unitHitCounts = new Dictionary<StringName, int>();
        StringName targetFilter = ResolveEffectTargetFilter(skillDef, pathStepAoeEffect);
        string pathStepResultLabel = GetPathStepResultLabel(pathStepAoeEffect);
        CombatEffectDef stageEffect = pathStepAoeEffect.duplicate_for_runtime();
        if (stageEffect == null)
        {
            return new PathStepResult(false);
        }
        stageEffect.effect_type = DamageEffectType;

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

            GDictionary result;
            AttackCheckInput attackCheck = new(skillId: skillDef?.skill_id ?? new StringName(""));
            var stageEffects = new GArray { stageEffect };
            if (pathStepParameters.ResolveAsWeaponAttack)
            {
                BattleAttackCheckPolicyService attackPolicy =
                    Runtime.get_attack_check_policy_service();
                BattleAttackCheckPolicyContext attackContext = attackPolicy.build_attack_context(
                    State,
                    activeUnit,
                    targetUnit,
                    skillDef,
                    SkillAttackCheckMode,
                    ExecuteStage,
                    false
                );
                attackCheck = attackPolicy.build_attack_check(
                    attackContext,
                    0,
                    0
                );
                result = DamageResolver.resolve_attack_effects(
                    activeUnit,
                    targetUnit,
                    stageEffects,
                    attackCheck,
                    new AttackContext
                    {
                        BattleState = State,
                        SkillId = skillDef?.skill_id ?? new StringName(""),
                    }
                );
            }
            else
            {
                result = DamageResolver.resolve_effects(
                    activeUnit,
                    targetUnit,
                    stageEffects,
                    new GDictionary { ["skill_id"] = skillDef?.skill_id ?? new StringName("") }
                );
            }
            AttackEffectResolutionResult stageResult =
                AttackEffectResolutionResultReader.ReadLegacyResolverResult(result, attackCheck);
            if (pathStepParameters.ResolveAsWeaponAttack)
            {
                _skillMasteryService?.RecordTargetResult(
                    activeUnit,
                    targetUnit,
                    skillDef,
                    stageResult
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
            Runtime.append_damage_result_log_lines(
                batch,
                $"{activeUnit.display_name} 的 {skillDef.display_name} {pathStepResultLabel}",
                targetUnit.display_name,
                stageResult
            );
            if (healing > 0)
            {
                batch.log_lines.Add(
                    $"{activeUnit.display_name} 的 {skillDef.display_name} {pathStepResultLabel}为 {targetUnit.display_name} 恢复 {healing} 点生命。"
                );
            }
            if (!targetUnit.is_alive)
            {
                totalKillCount += 1;
                Runtime.handle_unit_defeated_by_runtime_effect(
                    targetUnit,
                    activeUnit,
                    batch,
                    $"{targetUnit.display_name} 被击倒。",
                    new BattleDefeatHandlingOptions(recordEnemyDefeatedAchievement: true)
                );
            }
        }

        if (totalDamage > 0 || totalHealing > 0 || totalKillCount > 0)
        {
            Runtime.record_skill_effect_result(
                activeUnit,
                totalDamage,
                totalHealing,
                totalKillCount
            );
        }
        return new PathStepResult(true, hitCount, unitHitCounts);
    }

    private GVector2IArray BuildChargeStepEffectCoords(
        BattleUnitState activeUnit,
        CombatEffectDef pathStepAoeEffect
    )
    {
        return activeUnit == null
            ? new GVector2IArray()
            : BuildChargeStepEffectCoordsForAnchor(activeUnit, activeUnit.coord, pathStepAoeEffect);
    }

    private GVector2IArray BuildChargePathAnchorCoords(
        BattleUnitState activeUnit,
        Vector2I direction,
        int distance
    )
    {
        var anchorCoords = new GVector2IArray();
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

    private GVector2IArray BuildChargeStepEffectCoordsForAnchor(
        BattleUnitState activeUnit,
        Vector2I anchorCoord,
        CombatEffectDef pathStepAoeEffect
    )
    {
        if (!HasRuntime() || activeUnit == null || pathStepAoeEffect == null)
        {
            return new GVector2IArray();
        }

        StringName stepShape = GetStringName(pathStepAoeEffect.@params, "step_shape", "diamond");
        int stepRadius = Math.Max(GetInt(pathStepAoeEffect.@params, "step_radius", 1), 0);
        var coordSet = new HashSet<Vector2I>();
        var effectCoords = new List<Vector2I>();
        foreach (
            Vector2I occupiedCoord in GridService.get_unit_target_coords(activeUnit, anchorCoord)
        )
        {
            foreach (
                Vector2I effectCoord in GridService.get_area_coords(
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

    private static bool IsEffectUnlockedForSkillLevel(CombatEffectDef effectDef, int skillLevel)
    {
        if (effectDef == null)
        {
            return false;
        }
        int minLevel = Math.Max(effectDef.min_skill_level, 0);
        int maxLevel = effectDef.max_skill_level;
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

        activeUnit.refresh_footprint();
        Vector2I delta = targetAnchor - activeUnit.coord;
        if (GridService.get_distance(activeUnit.coord, targetAnchor) != 1)
        {
            return false;
        }

        GVector2IArray targetCoords = GridService.get_unit_target_coords(activeUnit, targetAnchor);
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
            BattleCellState targetCell = GridService.get_cell(State, footprintCoord);
            if (targetCell == null)
            {
                return false;
            }
            Vector2I referenceCoord = footprintCoord - delta;
            BattleCellState referenceCell = GridService.get_cell(State, referenceCoord);
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
        GVector2IArray targetCoords
    )
    {
        var targetLookup = new HashSet<Vector2I>();
        foreach (Vector2I targetCoord in targetCoords)
        {
            targetLookup.Add(targetCoord);
            if (!GridService.is_inside(State, targetCoord))
            {
                return false;
            }
            BattleCellState targetCell = GridService.get_cell(State, targetCoord);
            if (targetCell == null)
            {
                return false;
            }
            if (
                !BattleTerrainRules.can_unit_enter_terrain(
                    targetCell.base_terrain,
                    ToUntypedStringNameArray(activeUnit.movement_tags)
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

    private static GArray ToUntypedStringNameArray(Godot.Collections.Array<StringName> source)
    {
        GArray result = new();
        foreach (StringName value in source ?? new Godot.Collections.Array<StringName>())
            result.Add(value);
        return result;
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
        BattleEdgeFaceState edgeFace = GridService.get_edge_face(State, fromCoord, toCoord);
        if (edgeFace == null)
        {
            return true;
        }
        if (blocksOccupancy)
        {
            if (edgeFace.blocks_occupancy())
            {
                return true;
            }
        }
        else if (edgeFace.blocks_move())
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
            Vector2I reservedCoord in GridService.get_unit_target_coords(activeUnit, nextAnchor)
        )
        {
            reservedCoordSet.Add(reservedCoord);
        }

        var seenBlockers = new HashSet<StringName>();
        foreach (Vector2I frontierCoord in GetChargeFrontierCoords(activeUnit, nextAnchor))
        {
            BattleUnitState blocker = GridService.get_unit_at_coord(State, frontierCoord);
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
                batch.log_lines.Add(
                    $"{activeUnit.display_name} 被更大体型的 {blocker.display_name} 拦住，无法继续冲锋。"
                );
                return new ChargeBlockerResult("stop", "smaller_body");
            }
            if (blocker.footprint_size != Vector2I.One)
            {
                batch.log_lines.Add(
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

    private GVector2IArray GetChargeFrontierCoords(BattleUnitState activeUnit, Vector2I nextAnchor)
    {
        var currentCoords = new HashSet<Vector2I>();
        foreach (Vector2I occupiedCoord in activeUnit.occupied_coords)
        {
            currentCoords.Add(occupiedCoord);
        }

        var frontierCoords = new List<Vector2I>();
        foreach (Vector2I targetCoord in GridService.get_unit_target_coords(activeUnit, nextAnchor))
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
            GVector2IArray previousCoords = DuplicateVector2IArray(blocker.occupied_coords);
            if (GridService.move_unit_force(State, blocker, sidePush.Coord))
            {
                AppendChangedCoords(batch, previousCoords);
                AppendChangedUnitCoords(batch, blocker);
                AppendChangedUnitId(batch, blocker.unit_id);
                batch.log_lines.Add(
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
            GVector2IArray previousCoords = DuplicateVector2IArray(blocker.occupied_coords);
            if (GridService.move_unit(State, blocker, forwardCoord))
            {
                AppendChangedCoords(batch, previousCoords);
                AppendChangedUnitCoords(batch, blocker);
                AppendChangedUnitId(batch, blocker.unit_id);
                batch.log_lines.Add(
                    $"{activeUnit.display_name} 的冲锋将 {blocker.display_name} 向前顶开。"
                );
                int fallLayers = CalculateFallLayersForCoord(blocker, forwardCoord);
                if (fallLayers > 0)
                {
                    ApplyPushFallDamage(activeUnit, blocker, batch, fallLayers, "前推", "坠落");
                }
                return "continue";
            }

            if (GridService.collect_blocking_unit_ids(State, blocker, forwardCoord).Count > 0)
            {
                return "stop";
            }

            BattleCellState forwardCell = GridService.get_cell(State, forwardCoord);
            BattleCellState blockerCell = GridService.get_cell(State, blocker.coord);
            int heightDiff =
                forwardCell != null && blockerCell != null
                    ? Math.Abs(blockerCell.current_height - forwardCell.current_height)
                    : 0;
            GDictionary envResult;
            string envDamageLabel;
            if (heightDiff > 1)
            {
                envResult = DamageResolver.resolve_fall_damage(blocker, heightDiff);
                envDamageLabel = "撞向高地";
            }
            else
            {
                envResult = DamageResolver.resolve_fall_damage(blocker, 1);
                envDamageLabel = "撞向障碍物";
            }
            AttackEffectResolutionResult envDamageResult =
                AttackEffectResolutionResultReader.ReadLegacyResolverResult(
                    envResult,
                    new AttackCheckInput()
                );
            int envDamage = envDamageResult.Damage;
            int envShield = envDamageResult.ShieldAbsorbed;
            if (envDamage > 0 || envShield > 0)
            {
                if (envDamage > 0)
                {
                    batch.log_lines.Add(
                        $"{activeUnit.display_name} 的冲锋将 {blocker.display_name} {envDamageLabel}，受到 {envDamage} 点碰撞伤害。"
                    );
                    if (envShield > 0)
                    {
                        batch.log_lines.Add(
                            $"{blocker.display_name} 的护盾吸收了 {envShield} 点碰撞伤害。"
                        );
                    }
                }
                else
                {
                    batch.log_lines.Add(
                        $"{activeUnit.display_name} 的冲锋将 {blocker.display_name} {envDamageLabel}，但被护盾吸收了 {envShield} 点碰撞伤害。"
                    );
                }
                if (envDamageResult.ShieldBroken)
                {
                    batch.log_lines.Add($"{blocker.display_name} 的护盾被击碎。");
                }
                AppendChangedUnitId(batch, blocker.unit_id);
                if (!blocker.is_alive)
                {
                    Runtime.handle_unit_defeated_by_runtime_effect(
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
        GDictionary fallResult = DamageResolver.resolve_fall_damage(blocker, fallLayers);
        AttackEffectResolutionResult fallDamageResult =
            AttackEffectResolutionResultReader.ReadLegacyResolverResult(
                fallResult,
                new AttackCheckInput()
            );
        int fallDamage = fallDamageResult.Damage;
        int shieldAbsorbed = fallDamageResult.ShieldAbsorbed;
        if (fallDamage <= 0 && shieldAbsorbed <= 0)
        {
            return;
        }

        if (fallDamage > 0)
        {
            batch.log_lines.Add(
                $"{activeUnit.display_name} 的{pushLabel}使 {blocker.display_name} 跌落 {fallLayers} 层，受到 {fallDamage} 点{damageLabel}伤害。"
            );
            if (shieldAbsorbed > 0)
            {
                batch.log_lines.Add(
                    $"{blocker.display_name} 的护盾吸收了 {shieldAbsorbed} 点{damageLabel}伤害。"
                );
            }
        }
        else
        {
            batch.log_lines.Add(
                $"{activeUnit.display_name} 的{pushLabel}使 {blocker.display_name} 跌落 {fallLayers} 层，但被护盾吸收了 {shieldAbsorbed} 点{damageLabel}伤害。"
            );
        }
        if (fallDamageResult.ShieldBroken)
        {
            batch.log_lines.Add($"{blocker.display_name} 的护盾被击碎。");
        }
        AppendChangedUnitId(batch, blocker.unit_id);
        if (!blocker.is_alive)
        {
            Runtime.handle_unit_defeated_by_runtime_effect(
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

        BattleCellState currentCell = GridService.get_cell(State, unit.coord);
        BattleCellState targetCell = GridService.get_cell(State, targetCoord);
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
        BattleCellState blockerCell = GridService.get_cell(State, blocker.coord);
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
                !GridService.can_place_footprint(
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
            BattleCellState sideCell = GridService.get_cell(State, sideCoord);
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
            BattleCellState cell = GridService.get_cell(State, occupiedCoord);
            if (cell == null || cell.terrain_effect_ids.Count == 0)
            {
                continue;
            }
            var removedIds = new Godot.Collections.Array<StringName>();
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
                    removedIds.Add(terrainEffectId);
                }
            }
            if (removedIds.Count > 0)
            {
                return new TrapResult(true, occupiedCoord, removedIds);
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

        activeUnit.refresh_footprint();
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

    private GVector2IArray BuildChargePreviewCoords(
        BattleUnitState activeUnit,
        Vector2I direction,
        int distance
    )
    {
        if (activeUnit == null || direction == Vector2I.Zero || distance <= 0)
        {
            return new GVector2IArray();
        }

        var seenCoords = new HashSet<Vector2I>();
        var previewCoords = new List<Vector2I>();
        Vector2I previewAnchor = activeUnit.coord;
        for (int step = 0; step < distance; step++)
        {
            previewAnchor += direction;
            foreach (
                Vector2I occupiedCoord in GridService.get_unit_target_coords(
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

    private int GetChargeMaxDistance(BattleUnitState activeUnit, CombatCastVariantDef castVariant)
    {
        CombatEffectDef chargeEffect = get_charge_effect_def(castVariant);
        if (chargeEffect == null || !HasRuntime())
        {
            return 0;
        }

        StringName skillId = GetStringName(chargeEffect.@params, "skill_id", "charge");
        int skillLevel = GetUnitSkillLevel(activeUnit, skillId);
        int maxDistance = Math.Max(GetInt(chargeEffect.@params, "base_distance", 3), 0);
        GDictionary distanceByLevel = GetDict(
            chargeEffect.@params,
            "distance_by_level"
        );
        foreach (var breakpointKey in distanceByLevel.Keys)
        {
            if (!int.TryParse(breakpointKey.ToString(), out int levelBreakpoint))
            {
                continue;
            }
            if (skillLevel >= levelBreakpoint)
            {
                maxDistance = Math.Max(
                    maxDistance,
                    GetInt(distanceByLevel, breakpointKey, maxDistance)
                );
            }
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

    private StringName ResolveEffectTargetFilter(SkillDef skillDef, CombatEffectDef effectDef)
    {
        return Runtime?.resolve_effect_target_filter(skillDef, effectDef) ?? new StringName("");
    }

    private bool IsUnitValidForEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        StringName targetFilter
    )
    {
        return Runtime != null
            && Runtime.is_unit_valid_for_effect(sourceUnit, targetUnit, targetFilter);
    }

    private IEnumerable<BattleUnitState> CollectUnitsInCoords(GVector2IArray effectCoords)
    {
        if (Runtime == null)
        {
            yield break;
        }
        foreach (BattleUnitState unit in Runtime.collect_units_in_coords(effectCoords))
        {
            if (unit != null)
            {
                yield return unit;
            }
        }
    }

    private int GetUnitSkillLevel(BattleUnitState unit, StringName skillId)
    {
        return Runtime?.get_unit_skill_level(unit, skillId) ?? 0;
    }

    private string FormatSkillVariantLabel(SkillDef skillDef, CombatCastVariantDef castVariant)
    {
        return Runtime?.format_skill_variant_label(skillDef, castVariant) ?? "";
    }

    private void MarkAppliedStatusesForTurnTiming(
        BattleUnitState targetUnit,
        object statusEffectIds
    )
    {
        if (statusEffectIds is Variant variantStatusEffectIds)
        {
            Runtime?.mark_applied_statuses_for_turn_timing(
                targetUnit,
                variantStatusEffectIds.AsGodotArray()
            );
        }
        else if (statusEffectIds is GArray arrayStatusEffectIds)
        {
            Runtime?.mark_applied_statuses_for_turn_timing(
                targetUnit,
                arrayStatusEffectIds
            );
        }
        else if (statusEffectIds is Godot.Collections.Array<StringName> typedStatusEffectIds)
        {
            Runtime?.mark_applied_statuses_for_turn_timing(
                targetUnit,
                ToUntypedStringNameArray(typedStatusEffectIds)
            );
        }
    }

    private void AppendResultSourceStatusEffects(
        BattleEventBatch batch,
        BattleUnitState sourceUnit,
        GDictionary result
    )
    {
        Runtime?.append_result_source_status_effects(batch, sourceUnit, result);
    }

    private void AppendResultSourceStatusEffects(
        BattleEventBatch batch,
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result
    )
    {
        Runtime?.append_result_source_status_effects(batch, sourceUnit, result);
    }

    private static void MergeBatch(BattleEventBatch targetBatch, BattleEventBatch sourceBatch)
    {
        if (targetBatch == null || sourceBatch == null)
        {
            return;
        }
        AppendChangedCoords(targetBatch, sourceBatch.changed_coords);
        foreach (StringName unitId in sourceBatch.changed_unit_ids)
        {
            AppendChangedUnitId(targetBatch, unitId);
        }
        foreach (string logLine in sourceBatch.log_lines)
        {
            targetBatch.log_lines.Add(logLine);
        }
        foreach (var reportEntryValue in sourceBatch.report_entries)
        {
            if (reportEntryValue.VariantType == Variant.Type.Dictionary)
            {
                targetBatch.report_entries.Add(
                    reportEntryValue.AsGodotDictionary().Duplicate(true)
                );
            }
        }
    }

    private static void AppendChangedCoord(BattleEventBatch batch, Vector2I coord)
    {
        if (batch == null || batch.changed_coords.Contains(coord))
        {
            return;
        }
        batch.changed_coords.Add(coord);
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
        if (batch == null || IsEmpty(unitId) || batch.changed_unit_ids.Contains(unitId))
        {
            return;
        }
        batch.changed_unit_ids.Add(unitId);
    }

    private static void AppendChangedUnitCoords(BattleEventBatch batch, BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return;
        }
        unitState.refresh_footprint();
        AppendChangedCoords(batch, unitState.occupied_coords);
    }

    private static GVector2IArray DuplicateVector2IArray(IEnumerable<Vector2I> values)
    {
        var result = new GVector2IArray();
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

    private static GVector2IArray SortCoords(IEnumerable<Vector2I> coords)
    {
        var sorted = new List<Vector2I>();
        if (coords != null)
        {
            sorted.AddRange(coords);
        }
        sorted.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        var result = new GVector2IArray();
        foreach (Vector2I coord in sorted)
        {
            result.Add(coord);
        }
        return result;
    }

    private static GDictionary GetDict(GDictionary source, object key)
    {
        return TryGetValue(source, key, out Variant value)
            && value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static int GetInt(GDictionary source, object key, int fallback = 0)
    {
        if (!TryGetValue(source, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Float => (int)value.AsDouble(),
            Variant.Type.Bool => value.AsBool() ? 1 : 0,
            Variant.Type.String => int.TryParse(value.AsString(), out int parsed)
                ? parsed
                : fallback,
            Variant.Type.StringName
                => int.TryParse(value.AsStringName().ToString(), out int parsed)
                    ? parsed
                    : fallback,
            _ => fallback,
        };
    }

    private static string GetString(GDictionary source, object key, string fallback = "")
    {
        if (!TryGetValue(source, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            Variant.Type.Int => value.AsInt32().ToString(),
            Variant.Type.Float => value.AsDouble().ToString(
                System.Globalization.CultureInfo.InvariantCulture
            ),
            Variant.Type.Bool => value.AsBool() ? "True" : "False",
            _ => fallback,
        };
    }

    private static StringName GetStringName(GDictionary source, object key, StringName fallback = default)
    {
        if (!TryGetValue(source, key, out Variant value))
        {
            return fallback;
        }
        StringName result = ProgressionDataUtils.to_string_name(value);
        return result != "" ? result : fallback;
    }

    private static Vector2I GetVector2I(GDictionary source, object key, Vector2I fallback)
    {
        return TryGetValue(source, key, out Variant value)
            && value.VariantType == Variant.Type.Vector2I
            ? value.AsVector2I()
            : fallback;
    }

    private static List<Vector2I> ToVector2IList(GVector2IArray values)
    {
        var result = new List<Vector2I>();
        foreach (Vector2I coord in values ?? new GVector2IArray())
        {
            result.Add(coord);
        }
        return result;
    }

    private static bool TryGetValue(GDictionary source, object key, out Variant value)
    {
        if (source == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = ToVariantKey(key);
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return true;
        }
        if (key is StringName stringNameKey)
        {
            string keyText = stringNameKey.ToString();
            if (source.ContainsKey(keyText))
            {
                value = source[keyText];
                return true;
            }
        }
        else if (key is string stringKey)
        {
            var stringName = new StringName(stringKey);
            if (source.ContainsKey(stringName))
            {
                value = source[stringName];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static Variant ToVariantKey(object key)
    {
        return key switch
        {
            Variant variant => variant,
            StringName stringName => Variant.From(stringName),
            string text => Variant.From(text),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            float floatValue => Variant.From(floatValue),
            double doubleValue => Variant.From(doubleValue),
            bool boolValue => Variant.From(boolValue),
            Vector2I coord => Variant.From(coord),
            _ => Variant.From(key?.ToString() ?? ""),
        };
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static T ResolveWeakRef<T>(WeakReference<T> weakRef)
        where T : GodotObject
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out T target)
            || !GodotObject.IsInstanceValid(target)
        )
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
        public static readonly TrapResult NotTriggered = new(
            false,
            Vector2I.Zero,
            new Godot.Collections.Array<StringName>()
        );

        public readonly bool Triggered;
        public readonly Vector2I Coord;
        public readonly Godot.Collections.Array<StringName> TerrainEffectIds;

        public TrapResult(
            bool triggered,
            Vector2I coord,
            Godot.Collections.Array<StringName> terrainEffectIds
        )
        {
            Triggered = triggered;
            Coord = coord;
            TerrainEffectIds = terrainEffectIds;
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
