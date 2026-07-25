using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal class BattleGroundEffectService
{
    private static readonly StringName Empty = "";
    private static readonly StringName FeatureWall = "wall";
    private static readonly StringName FeatureDoor = "door";
    private static readonly StringName FeatureGate = "gate";

    private readonly record struct GroundEffectRuntimeParameters(bool ResolveAsWeaponAttack)
    {
        internal static GroundEffectRuntimeParameters FromEffect(
            CombatEffectDefinition effectDefinition
        )
        {
            return new GroundEffectRuntimeParameters(
                effectDefinition?.ResolveAsWeaponAttack ?? false
            );
        }
    }

    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private readonly BattleGroundRelocationService _relocationService = new();
    private readonly BattleGroundSkillValidationService _validationService = new();
    private readonly BattleGroundEffectCoordService _coordService = new();

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
        _coordService.Setup(runtime, this);
        _relocationService.Setup(runtime, this, _coordService);
        _validationService.Setup(runtime, this, _relocationService, _coordService);
    }

    internal int ActiveDependencyCount =>
        (_runtime != null ? 1 : 0)
        + _coordService.ActiveDependencyCount
        + _relocationService.ActiveDependencyCount
        + _validationService.ActiveDependencyCount;

    internal void Dispose()
    {
        Exception firstFailure = null;
        BattleRuntimeModule.RunTeardownStep(
            ref firstFailure,
            _validationService.DisposeRuntime
        );
        BattleRuntimeModule.RunTeardownStep(
            ref firstFailure,
            _relocationService.DisposeRuntime
        );
        BattleRuntimeModule.RunTeardownStep(
            ref firstFailure,
            _coordService.DisposeRuntime
        );
        BattleRuntimeModule.RunTeardownStep(ref firstFailure, () => _runtime = null);
        if (firstFailure != null)
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
    }

    internal void append_result_report_entry(
        BattleEventBatch batch,
        AttackEffectResolutionResult result
    )
    {
        Runtime?.AppendResultReportEntry(batch, result);
    }

    internal void MarkAppliedStatusesForTurnTiming(
        BattleUnitState target_unit,
        GArray status_effect_ids
    )
    {
        Runtime?.MarkAppliedStatusesForTurnTiming(target_unit, status_effect_ids);
    }

    internal void MarkAppliedStatusesForTurnTiming(
        BattleUnitState target_unit,
        GStringNameArray status_effect_ids
    )
    {
        Runtime?.MarkAppliedStatusesForTurnTiming(target_unit, status_effect_ids);
    }

    internal void MarkAppliedStatusesForTurnTiming(
        BattleUnitState target_unit,
        IReadOnlyList<StringName> status_effect_ids
    )
    {
        Runtime?.MarkAppliedStatusesForTurnTiming(target_unit, status_effect_ids);
    }

    internal void append_result_source_status_effects(
        BattleEventBatch batch,
        BattleUnitState source_unit,
        AttackEffectResolutionResult result
    )
    {
        Runtime?.AppendResultSourceStatusEffects(
            batch,
            source_unit,
            result
        );
    }

    internal void _record_effect_metrics(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        int damage,
        int healing,
        int kill_count
    )
    {
        Runtime?._record_effect_metrics(
            source_unit,
            target_unit,
            damage,
            healing,
            kill_count
        );
    }

    internal void _record_unit_defeated(BattleUnitState unit_state)
    {
        Runtime?._record_unit_defeated(unit_state);
    }

    internal void append_damage_result_log_lines(
        BattleEventBatch batch,
        string subject_label,
        string target_display_name,
        AttackEffectResolutionResult result
    )
    {
        Runtime?.AppendDamageResultLogLines(
            batch,
            subject_label,
            target_display_name,
            result
        );
    }

    internal string _build_skill_log_subject_label(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition = null
    )
    {
        if (sourceUnit != null && skillDefinition == null)
        {
            return sourceUnit.display_name;
        }
        string displayName = skillDefinition?.DisplayName;
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = skillDefinition?.SkillId.ToString();
        }
        string variantName = castVariantDefinition?.DisplayName;
        string skillLabel = string.IsNullOrEmpty(variantName)
            ? displayName
            : $"{displayName}·{variantName}";
        if (sourceUnit == null)
        {
            return skillLabel ?? "";
        }
        return string.IsNullOrEmpty(skillLabel)
            ? sourceUnit.display_name
            : $"{sourceUnit.display_name} 的 {skillLabel}";
    }

    internal void _apply_on_kill_gain_resources_effects(
        BattleUnitState sourceUnit,
        BattleUnitState defeatedUnit,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        Runtime?._apply_on_kill_gain_resources_effects(
            sourceUnit,
            defeatedUnit,
            skillDefinition,
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            batch
        );
    }

    internal bool _is_crown_break_target_eligible(BattleUnitState active_unit, BattleUnitState target_unit)
    {
        return _runtime != null
            && Runtime._is_crown_break_target_eligible(
                active_unit,
                target_unit
            );
    }

    internal bool _is_crown_break_target_eligible(
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit
    )
    {
        return target_unit.IsValid
            && _is_unit_valid_for_effect(active_unit, target_unit, BattleTypedNames.TargetFilterEnemy)
            && target_unit.HasStatusEffect("black_star_brand_elite");
    }

    internal bool _is_crown_break_skill(StringName skill_id)
    {
        return _runtime != null && Runtime._is_crown_break_skill(skill_id);
    }

    private void RecordVajraBodyMasteryFromIncomingDamageTyped(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        AttackEffectResolutionResult result,
        BattleEventBatch batch = null
    )
    {
        Runtime?.RecordVajraBodyMasteryFromIncomingDamageTyped(
            sourceUnit,
            targetUnit,
            skillDefinition,
            result,
            batch
        );
    }

    internal BattleShieldApplyResult ApplyUnitShieldEffectsResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        Dictionary<long, int> shieldRollContext = null
    )
    {
        if (_runtime == null)
        {
            return new BattleShieldApplyResult(false, 0, 0, -1, Empty);
        }
        return Runtime.ApplyUnitShieldEffectsResult(
            sourceUnit,
            targetUnit,
            skillDefinition,
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            shieldRollContext ?? new Dictionary<long, int>()
        );
    }

    internal StringName ResolveEffectTargetFilter(
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition
    )
    {
        return SkillResolutionRules?.ResolveEffectTargetFilter(
                skillDefinition,
                effectDefinition
            ) ?? Empty;
    }

    internal bool _is_unit_valid_for_effect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_team_filter
    )
    {
        return _runtime != null
            && Runtime._is_unit_valid_for_effect(
                source_unit,
                target_unit,
                target_team_filter
            );
    }

    internal bool _is_unit_valid_for_effect(
        BattleUnitReadView source_unit,
        BattleUnitReadView target_unit,
        StringName target_team_filter
    )
    {
        return _runtime != null
            && Runtime._is_unit_valid_for_effect(
                source_unit,
                target_unit,
                target_team_filter
            );
    }

    internal void _flush_last_stand_mastery_records(BattleEventBatch batch)
    {
        Runtime?._flush_last_stand_mastery_records(batch);
    }

    internal void _append_changed_coord(BattleEventBatch batch, Vector2I coord)
    {
        Runtime?._append_changed_coord(batch, coord);
    }

    internal void AppendChangedCoords(BattleEventBatch batch, IReadOnlyList<Vector2I> coords)
    {
        if (coords == null)
        {
            return;
        }
        foreach (Vector2I coord in coords)
        {
            _append_changed_coord(batch, coord);
        }
    }

    internal void _append_changed_unit_id(BattleEventBatch batch, StringName unit_id)
    {
        Runtime?._append_changed_unit_id(batch, unit_id);
    }

    internal void _append_changed_unit_coords(BattleEventBatch batch, BattleUnitState unit_state)
    {
        Runtime?._append_changed_unit_coords(batch, unit_state);
    }

    internal void _collect_defeated_unit_loot(
        BattleUnitState unit_state,
        BattleUnitState killer_unit = null,
        BattleEventBatch batch = null
    )
    {
        Runtime?._collect_defeated_unit_loot(unit_state, killer_unit, batch);
    }

    internal void _clear_defeated_unit(BattleUnitState unit_state, BattleEventBatch batch = null)
    {
        Runtime?._clear_defeated_unit(unit_state, batch);
    }

    internal int _get_unit_skill_level(BattleUnitState unit_state, StringName skill_id)
    {
        return _runtime == null
            ? 0
            : Runtime._get_unit_skill_level(unit_state, skill_id);
    }

    internal BattleSkillCastBlockReasonKind _get_skill_cast_block_reason(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition
    )
    {
        return _runtime == null
            ? BattleSkillCastBlockReasonKind.SkillCastCheckUnbound
            : Runtime._get_skill_cast_block_reason(active_unit, skillDefinition);
    }

    internal int _get_effective_skill_range(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition
    )
    {
        return _runtime == null
            ? 0
            : Runtime._get_effective_skill_range(active_unit, skillDefinition);
    }

    internal int _get_effective_skill_range(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition
    )
    {
        return _runtime == null
            ? 0
            : Runtime._get_effective_skill_range(active_unit, skillDefinition);
    }

    internal bool _is_movement_blocked(BattleUnitState unit_state)
    {
        return _runtime != null && Runtime._is_movement_blocked(unit_state);
    }

    internal bool _is_movement_blocked(BattleUnitReadView unitView)
    {
        return _runtime != null && Runtime._movement_service.IsMovementBlocked(unitView);
    }

    internal bool ApplyGroundPrecastSpecialEffects(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        BattleEventBatch batch
    ) => _relocationService.ApplyGroundPrecastSpecialEffects(activeUnit, skillDefinition, castVariantDefinition, targetCoords, batch);

    internal bool ApplyGroundJumpRelocation(
        BattleUnitState active_unit,
        IReadOnlyList<Vector2I> target_coords,
        BattleEventBatch batch
    ) => _relocationService.ApplyGroundJumpRelocation(active_unit, target_coords, batch);

    internal BattleGroundWindPushResult _apply_ground_wind_push_effects_result(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> windPushEffects,
        IReadOnlyList<Vector2I> effectCoords,
        IReadOnlyList<Vector2I> targetCoords,
        BattleEventBatch batch
    ) => _relocationService._apply_ground_wind_push_effects_result(sourceUnit, skillDefinition, windPushEffects, effectCoords, targetCoords, batch);

    internal string GetGroundSpecialEffectValidationMessage(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> targetCoords
    ) => _validationService.GetGroundSpecialEffectValidationMessage(activeUnit, skillDefinition, castVariantDefinition, targetCoords);

    internal string GetGroundSpecialEffectValidationMessage(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> targetCoords
    ) => _validationService.GetGroundSpecialEffectValidationMessage(activeUnit, skillDefinition, castVariantDefinition, targetCoords);

    internal BattleGroundSkillValidationResult _validate_ground_skill_command_result(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleCommand command
    ) => _validationService._validate_ground_skill_command_result(activeUnit, skillDefinition, castVariantDefinition, command);

    internal BattleGroundSkillValidationResult _validate_ground_skill_command_result(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleCommand command
    ) => _validationService._validate_ground_skill_command_result(activeUnit, skillDefinition, castVariantDefinition, command);

    internal bool _validate_target_coords_shape(
        CombatCastFootprintPattern footprint_pattern,
        IReadOnlyList<Vector2I> target_coords
    ) => _validationService._validate_target_coords_shape(footprint_pattern, target_coords);

    internal Godot.Collections.Array<Vector2I> _normalize_target_coords(BattleCommand command) => _validationService._normalize_target_coords(command);

    internal IReadOnlyList<Vector2I> BuildGroundEffectCoords(
        SkillDefinition skillDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        Vector2I sourceCoord,
        BattleUnitState activeUnit,
        CombatCastVariantDefinition castVariantDefinition
    ) => _coordService.BuildGroundEffectCoords(skillDefinition, targetCoords, sourceCoord, activeUnit, castVariantDefinition);

    internal IReadOnlyList<Vector2I> BuildGroundEffectCoords(
        SkillDefinition skillDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        Vector2I sourceCoord,
        BattleUnitReadView activeUnit,
        CombatCastVariantDefinition castVariantDefinition
    ) => _coordService.BuildGroundEffectCoords(skillDefinition, targetCoords, sourceCoord, activeUnit, castVariantDefinition);

    internal IReadOnlyList<CombatEffectDefinition> CollectGroundUnitEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitState activeUnit
    ) => _coordService.CollectGroundUnitEffectDefinitions(skillDefinition, castVariantDefinition, activeUnit);

    internal IReadOnlyList<CombatEffectDefinition> CollectGroundUnitEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitReadView activeUnit
    ) => _coordService.CollectGroundUnitEffectDefinitions(skillDefinition, castVariantDefinition, activeUnit);

    internal IReadOnlyList<CombatEffectDefinition> CollectGroundTerrainEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitState activeUnit
    ) => _coordService.CollectGroundTerrainEffectDefinitions(skillDefinition, castVariantDefinition, activeUnit);

    internal IReadOnlyList<CombatEffectDefinition> CollectGroundTerrainEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitReadView activeUnit
    ) => _coordService.CollectGroundTerrainEffectDefinitions(skillDefinition, castVariantDefinition, activeUnit);

    internal IReadOnlyList<StringName> CollectGroundPreviewUnitIds(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> effectCoords
    ) => _coordService.CollectGroundPreviewUnitIds(sourceUnit, skillDefinition, effectDefinitions, effectCoords);

    internal IReadOnlyList<StringName> CollectGroundPreviewUnitIds(
        BattleUnitReadView sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> effectCoords
    ) => _coordService.CollectGroundPreviewUnitIds(sourceUnit, skillDefinition, effectDefinitions, effectCoords);

    internal BattleSpellControlResult ResolveGroundSpellControlAfterCostResult(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        int spent_mp,
        BattleEventBatch batch
    )
    {
        BattleDamageResolver damageResolver = Runtime?.GetDamageResolver();
        BattleMagicBacklashResolver magicBacklashResolver = Runtime?._magic_backlash_resolver;
        if (
            damageResolver == null
            || magicBacklashResolver == null
            || !magicBacklashResolver.ShouldResolveSpellControl(skillDefinition)
        )
        {
            return BattleSpellControlResult.None();
        }
        StringName skillId = skillDefinition?.SkillId ?? Empty;
        int skillLevel = _get_unit_skill_level(active_unit, skillId);
        BattleSpellControlMetadata controlMetadata = damageResolver.ResolveSpellControlCheckTyped(
            active_unit,
            State,
            skillId
        );
        BattleSpellControlResult controlContext =
            magicBacklashResolver.ApplySpellControlAfterCostResult(
                active_unit,
                skillDefinition,
                skillLevel,
                spent_mp,
                controlMetadata,
                batch
            );
        _append_changed_unit_id(batch, active_unit?.unit_id ?? Empty);
        return controlContext;
    }

    internal BattleSpellControlResult ResolveUnitSpellControlAfterCostResult(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        BattleEventBatch batch
    )
    {
        BattleDamageResolver damageResolver = Runtime?.GetDamageResolver();
        BattleMagicBacklashResolver magicBacklashResolver = Runtime?._magic_backlash_resolver;
        if (
            damageResolver == null
            || magicBacklashResolver == null
            || !magicBacklashResolver.ShouldResolveSpellControl(skillDefinition)
        )
        {
            return BattleSpellControlResult.None();
        }
        StringName skillId = skillDefinition?.SkillId ?? Empty;
        int skillLevel = _get_unit_skill_level(active_unit, skillId);
        CombatSkillResourceCosts costs =
            skillDefinition?.CombatProfile?.GetEffectiveResourceCostValues(skillLevel)
            ?? CombatSkillResourceCosts.Zero;
        int spentMp = costs.MpCost;
        BattleSpellControlMetadata controlMetadata = damageResolver.ResolveSpellControlCheckTyped(
            active_unit,
            State,
            skillId
        );
        BattleSpellControlResult controlContext =
            magicBacklashResolver.ApplySpellControlAfterCostResult(
                active_unit,
                skillDefinition,
                skillLevel,
                spentMp,
                controlMetadata,
                batch
            );
        _append_changed_unit_id(batch, active_unit?.unit_id ?? Empty);
        return controlContext;
    }

    internal BattleGroundUnitEffectsResult _apply_ground_unit_effects_result(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> effectCoords,
        BattleEventBatch batch,
        IReadOnlyList<Vector2I> targetCoords,
        IReadOnlyList<Vector2I> contingencyEffectCoords = null
    )
    {
        bool applied = false;
        int totalDamage = 0;
        int totalHealing = 0;
        int totalKillCount = 0;
        var affectedUnitIds = new HashSet<StringName>();
        var shieldRollContext = new Dictionary<long, int>();
        BattleForcedMoveContext forcedMoveContext = BattleGroundRelocationService.BuildGroundForcedMoveContext(
            sourceUnit,
            targetCoords
        );
        IReadOnlyList<CombatEffectDefinition> effectDefinitionList =
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>();
        IReadOnlyList<Vector2I> normalizedEffectCoords = effectCoords ?? Array.Empty<Vector2I>();
        IReadOnlyList<Vector2I> normalizedContingencyEffectCoords =
            contingencyEffectCoords ?? normalizedEffectCoords;
        IReadOnlyList<CombatEffectDefinition> windPushEffects =
            BattleGroundRelocationService.CollectWindPushEffectDefinitions(effectDefinitionList);
        HashSet<int> windPushEffectIds = BattleGroundEffectCoordService.BuildEffectInstanceIdSet(windPushEffects);
        StringName sourceEventId =
            Runtime?.AllocateContingencySourceEventId("ground_spell") ?? Empty;
        IReadOnlyList<StringName> spellAffectedUnitIds = CollectGroundPreviewUnitIds(
            sourceUnit,
            skillDefinition,
            effectDefinitionList,
            normalizedEffectCoords
        );
        if (spellAffectedUnitIds.Count > 0 || normalizedContingencyEffectCoords.Count > 0)
        {
            Runtime?.EmitContingencySpellAffected(
                sourceUnit,
                null,
                spellAffectedUnitIds,
                sourceEventId,
                normalizedContingencyEffectCoords
            );
        }

        foreach (BattleUnitState targetUnit in _coordService.CollectUnitsInCoords(normalizedEffectCoords))
        {
            if (targetUnit == null || !targetUnit.IsAlive())
            {
                continue;
            }
            var applicableEffects = new List<CombatEffectDefinition>();
            foreach (CombatEffectDefinition effectDefinition in effectDefinitionList)
            {
                if (
                    effectDefinition == null
                    || windPushEffectIds.Contains(RuntimeHelpers.GetHashCode(effectDefinition))
                )
                {
                    continue;
                }
                if (
                    _is_unit_valid_for_effect(
                        sourceUnit,
                        targetUnit,
                        ResolveEffectTargetFilter(skillDefinition, effectDefinition)
                    )
                )
                {
                    applicableEffects.Add(effectDefinition);
                }
            }
            if (applicableEffects.Count == 0)
            {
                continue;
            }

            int previousTargetHp = targetUnit.GetCurrentHp();
            GroundUnitEffectResolution effectResolution =
                _resolve_ground_unit_effect_resolution(
                    sourceUnit,
                    targetUnit,
                    skillDefinition,
                    applicableEffects,
                    batch
                );
            AttackEffectResolutionResult damageResult = effectResolution.Result;
            BattleShieldApplyResult shieldResult = ApplyUnitShieldEffectsResult(
                sourceUnit,
                targetUnit,
                skillDefinition,
                applicableEffects,
                shieldRollContext
            );
            BattleSpecialSkillResult specialResult =
                Runtime.ApplyUnitSkillSpecialEffectsResult(
                    sourceUnit,
                    targetUnit,
                    skillDefinition,
                    castVariantDefinition,
                    applicableEffects,
                    batch,
                    forcedMoveContext
                );
            Runtime?._skill_mastery_service?.RecordTargetResult(
                sourceUnit,
                targetUnit,
                skillDefinition,
                damageResult,
                applicableEffects,
                additionalEffectApplied: shieldResult.Applied || specialResult.Applied
            );
            RecordVajraBodyMasteryFromIncomingDamageTyped(
                sourceUnit,
                targetUnit,
                skillDefinition,
                damageResult,
                batch
            );
            MarkAppliedStatusesForTurnTiming(targetUnit, damageResult.StatusEffectIds);
            var appliedStatusIds = new List<StringName>();
            if (damageResult.StatusEffectIds != null)
            {
                foreach (StringName statusId in damageResult.StatusEffectIds)
                {
                    if (statusId != Empty && !appliedStatusIds.Contains(statusId))
                    {
                        appliedStatusIds.Add(statusId);
                    }
                }
            }
            foreach (StringName statusId in specialResult.StatusEffectIds ?? Array.Empty<StringName>())
            {
                if (statusId != Empty && !appliedStatusIds.Contains(statusId))
                {
                    appliedStatusIds.Add(statusId);
                }
            }
            Runtime?.EmitContingencyHpAndStatusHooks(
                sourceUnit,
                targetUnit,
                previousTargetHp,
                appliedStatusIds,
                sourceEventId
            );
            bool attackResolved =
                damageResult.AttackResolution != AttackResolutionKind.None
                || damageResult.AttackSuccess
                || damageResult.CriticalHit
                || damageResult.CriticalFail;
            bool attackHit = attackResolved && damageResult.AttackSuccess;
            bool unitApplied =
                damageResult.Applied
                || shieldResult.Applied
                || specialResult.Applied
                || attackHit;
            if (!unitApplied)
            {
                if (attackResolved)
                {
                    append_result_report_entry(batch, damageResult);
                }
                continue;
            }

            applied = true;
            BattleGroundRelocationService.AppendAffectedUnitId(affectedUnitIds, targetUnit);
            _append_changed_unit_id(batch, sourceUnit != null ? sourceUnit.unit_id : Empty);
            _append_changed_unit_id(batch, targetUnit.unit_id);
            _append_changed_unit_coords(batch, targetUnit);
            append_result_source_status_effects(batch, sourceUnit, damageResult);

            int damage = damageResult.Damage;
            int healing = damageResult.Healing;
            totalDamage += damage;
            totalHealing += healing;
            append_damage_result_log_lines(
                batch,
                _build_skill_log_subject_label(
                    sourceUnit,
                    skillDefinition,
                    castVariantDefinition
                ),
                DisplayName(targetUnit),
                damageResult
            );
            if (attackResolved && !damageResult.Applied)
            {
                append_result_report_entry(batch, damageResult);
            }
            if (healing > 0)
            {
                AppendLog(
                    batch,
                    $"{_build_skill_log_subject_label(sourceUnit, skillDefinition, castVariantDefinition)} 为 {DisplayName(targetUnit)} 恢复 {healing} 点生命。"
                );
            }
            if (shieldResult.Applied)
            {
                AppendLog(
                    batch,
                    $"{_build_skill_log_subject_label(sourceUnit, skillDefinition, castVariantDefinition)} 使 {DisplayName(targetUnit)} 的护盾值变为 {shieldResult.CurrentShieldHp}。"
                );
            }
            foreach (StringName statusId in damageResult.StatusEffectIds)
            {
                AppendLog(batch, $"{DisplayName(targetUnit)} 获得状态 {statusId}。");
            }

            if (!targetUnit.IsAlive())
            {
                totalKillCount += 1;
                _apply_on_kill_gain_resources_effects(
                    sourceUnit,
                    targetUnit,
                    skillDefinition,
                    effectDefinitionList,
                    batch
                );
                Runtime.HandleUnitDefeatedByRuntimeEffect(
                    targetUnit,
                    sourceUnit,
                    batch,
                    $"{DisplayName(targetUnit)} 被击倒。",
                    new BattleDefeatHandlingOptions(
                        recordEnemyDefeatedAchievement: true,
                        killProvenance: BattleKillProvenance.FromWeaponAttackResult(
                            sourceUnit,
                            damageResult,
                            skillDefinition.SkillId
                        )
                    )
                );
            }
            if (sourceUnit != null && targetUnit != null)
            {
                _record_effect_metrics(
                    sourceUnit,
                    targetUnit,
                    damage,
                    healing,
                    targetUnit.IsAlive() ? 0 : 1
                );
                Runtime?._battle_rating_system?.RecordContributionFromUnits(
                    sourceUnit,
                    targetUnit,
                    damage,
                    healing,
                    !targetUnit.IsAlive(),
                    new StringName("skill"),
                    skillDefinition != null ? skillDefinition.SkillId : Empty
                );
            }
        }

        BattleGroundWindPushResult windPushResult = _apply_ground_wind_push_effects_result(
            sourceUnit,
            skillDefinition,
            windPushEffects,
            normalizedEffectCoords,
            targetCoords,
            batch
        );
        if (windPushResult.Applied)
        {
            applied = true;
            _append_changed_unit_id(batch, sourceUnit != null ? sourceUnit.unit_id : Empty);
        }
        foreach (StringName affectedUnitId in windPushResult.AffectedUnitIds)
        {
            affectedUnitIds.Add(affectedUnitId);
        }

        _flush_last_stand_mastery_records(batch);
        return new BattleGroundUnitEffectsResult(
            applied,
            affectedUnitIds.Count,
            totalDamage,
            totalHealing,
            totalKillCount
        );
    }

    internal AttackEffectResolutionResult ResolveGroundUnitEffectResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch = null
    )
    {
        return _resolve_ground_unit_effect_resolution(
            sourceUnit,
            targetUnit,
            skillDefinition,
            effectDefinitions,
            batch
        ).Result;
    }

    private GroundUnitEffectResolution _resolve_ground_unit_effect_resolution(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch = null
    )
    {
        IReadOnlyList<CombatEffectDefinition> normalizedEffectDefinitions =
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>();
        if (ShouldResolveGroundEffectsAsAttack(normalizedEffectDefinitions))
        {
            IReadOnlyList<CombatEffectDefinition> attackEffectDefinitions =
                DedupeEffectDefinitionsByIdentityTyped(normalizedEffectDefinitions);
            BattleRuntimeModule runtime = _runtime as BattleRuntimeModule;
            BattleAttackCheckPolicyService attackPolicy =
                runtime?.GetAttackCheckPolicyService();
            BattleDamageResolver damageResolver = runtime?.GetDamageResolver();
            if (attackPolicy == null || damageResolver == null)
            {
                return GroundUnitEffectResolution.FromResult(
                    BattleDamageResolver.BuildEmptyResolutionResult(
                        skillDefinition != null ? skillDefinition.SkillId : Empty
                    )
                );
            }
            BattleAttackCheckPolicyContext attackContext =
                attackPolicy.BuildSkillDefinitionAttackContext(
                    State,
                    sourceUnit,
                    targetUnit,
                    skillDefinition,
                    new StringName("skill_attack_check"),
                    new StringName("execute"),
                    false
                );
            AttackCheckInput attackCheck = attackPolicy.BuildAttackCheck(attackContext, 0, 0);
            return GroundUnitEffectResolution.FromResult(
                damageResolver.ResolveAttackEffects(
                    sourceUnit,
                    targetUnit,
                    attackEffectDefinitions,
                    attackCheck,
                    new AttackContext
                    {
                        BattleState = State,
                        SkillId = skillDefinition != null ? skillDefinition.SkillId : Empty,
                        EventBatch = batch,
                    }
                )
            );
        }
        StringName skillId = skillDefinition != null ? skillDefinition.SkillId : Empty;
        return GroundUnitEffectResolution.FromResult(
            Runtime.GetDamageResolver()
                .ResolveEffects(
                    sourceUnit,
                    targetUnit,
                    normalizedEffectDefinitions,
                    DamageResolutionContext
                        .ForSkill(skillId)
                        .WithBattleState(State)
                        .WithDamageApplicationHookContext(
                            batch,
                            Runtime?.CurrentEffectOriginForContingency
                                ?? BattleEffectOrigin.PlayerCommand()
                        )
                )
        );
    }

    internal static bool ShouldResolveGroundEffectsAsAttack(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (GroundEffectRuntimeParameters.FromEffect(effectDefinition).ResolveAsWeaponAttack)
            {
                return true;
            }
        }
        return false;
    }

    internal IReadOnlyList<CombatEffectDefinition> DedupeEffectDefinitionsByIdentityTyped(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        var deduped = new List<CombatEffectDefinition>();
        var seen = new HashSet<int>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition != null && seen.Add(RuntimeHelpers.GetHashCode(effectDefinition)))
            {
                deduped.Add(effectDefinition);
            }
        }
        return deduped;
    }

    internal BattleGroundTerrainEffectsResult _apply_ground_terrain_effects_result(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> effectCoords,
        BattleEventBatch batch
    )
    {
        bool applied = false;
        bool requiresTopologyReconcile = false;
        IReadOnlyList<CombatEffectDefinition> normalizedEffectDefinitions =
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>();
        IReadOnlyList<Vector2I> normalizedEffectCoords = effectCoords ?? Array.Empty<Vector2I>();
        foreach (CombatEffectDefinition effectDefinition in normalizedEffectDefinitions)
        {
            if (effectDefinition == null)
            {
                continue;
            }
            BattleEffectKind effectKind = effectDefinition.EffectKind;
            if (IsGroundCellTopologyEffect(effectKind))
            {
                requiresTopologyReconcile = true;
                foreach (Vector2I effectCoord in normalizedEffectCoords)
                {
                    if (
                        _apply_ground_cell_effect(
                            sourceUnit,
                            skillDefinition,
                            effectCoord,
                            effectDefinition,
                            batch
                        )
                    )
                    {
                        applied = true;
                    }
                }
            }
            else if (effectKind == BattleEffectKind.TerrainEffect)
            {
                if (
                    effectDefinition.DurationTu > 0
                    && effectDefinition.TickIntervalTu > 0
                )
                {
                    StringName fieldInstanceId = _build_terrain_effect_instance_id(
                        effectDefinition.TerrainEffectId
                    );
                    int appliedCoordCount = 0;
                    foreach (Vector2I effectCoord in normalizedEffectCoords)
                    {
                        if (
                            Runtime._terrain_effect_system.UpsertTimedTerrainEffectFromDefinition(
                                effectCoord,
                                sourceUnit,
                                skillDefinition,
                                effectDefinition,
                                fieldInstanceId
                            )
                        )
                        {
                            applied = true;
                            appliedCoordCount += 1;
                            _append_changed_coord(batch, effectCoord);
                        }
                    }
                    if (appliedCoordCount > 0)
                    {
                        AppendLog(
                            batch,
                            $"{_build_skill_log_subject_label(sourceUnit, skillDefinition)} 在 {appliedCoordCount} 个地格留下 {_get_terrain_effect_display_name(effectDefinition)}。"
                        );
                    }
                }
                else if (!IsEmpty(effectDefinition.TerrainEffectId))
                {
                    int taggedCoordCount = 0;
                    foreach (Vector2I effectCoord in normalizedEffectCoords)
                    {
                        BattleCellState cell = GridService.GetCellState(State, effectCoord);
                        if (cell == null)
                        {
                            continue;
                        }
                        List<StringName> terrainEffectIds = cell.terrain_effect_ids;
                        StringName terrainEffectId = effectDefinition.TerrainEffectId;
                        if (terrainEffectIds.Contains(terrainEffectId))
                        {
                            continue;
                        }
                        terrainEffectIds.Add(terrainEffectId);
                        _append_changed_coord(batch, effectCoord);
                        taggedCoordCount += 1;
                        applied = true;
                    }
                    if (taggedCoordCount > 0)
                    {
                        AppendLog(
                            batch,
                            $"{_build_skill_log_subject_label(sourceUnit, skillDefinition)} 使 {taggedCoordCount} 个地格附加效果 {_get_terrain_effect_display_name(effectDefinition)}。"
                        );
                    }
                }
            }
            else if (effectKind == BattleEffectKind.EdgeClear)
            {
                if (
                    _apply_ground_edge_clear_effect(
                        sourceUnit,
                        skillDefinition,
                        normalizedEffectCoords,
                        effectDefinition,
                        batch
                    )
                )
                {
                    applied = true;
                }
            }
        }
        if (requiresTopologyReconcile && ReconcileWaterTopology(normalizedEffectCoords, batch))
        {
            applied = true;
        }
        return new BattleGroundTerrainEffectsResult(applied);
    }

    private static bool IsGroundCellTopologyEffect(BattleEffectKind effectKind)
    {
        return effectKind switch
        {
            BattleEffectKind.Terrain
            or BattleEffectKind.TerrainReplace
            or BattleEffectKind.TerrainReplaceTo
            or BattleEffectKind.Height
            or BattleEffectKind.HeightDelta => true,
            _ => false,
        };
    }

    internal bool _apply_ground_edge_clear_effect(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<Vector2I> effectCoords,
        CombatEffectDefinition effectDefinition,
        BattleEventBatch batch
    )
    {
        if (_runtime == null || State == null || effectCoords == null || effectCoords.Count < 2)
        {
            return false;
        }
        IReadOnlyList<Vector2I> edgeCoords = BattleGroundEffectCoordService.SortCoordsTyped(effectCoords);
        Vector2I first = edgeCoords[0];
        Vector2I second = edgeCoords[1];
        if (GridService.GetDistance(first, second) != 1)
        {
            return false;
        }
        EdgeAuthoringReference edgeRef = BuildEdgeAuthoringReference(first, second);
        if (!edgeRef.IsValid)
        {
            return false;
        }
        Vector2I edgeCoord = edgeRef.Coord;
        Vector2I edgeDirection = edgeRef.Direction;
        BattleCellState cell = GridService.GetCellState(State, edgeCoord);
        if (cell == null)
        {
            return false;
        }
        BattleEdgeFeatureState featureState = cell.GetEdgeFeature(edgeDirection);
        if (featureState == null || featureState.IsEmpty())
        {
            return false;
        }
        if (!CanEdgeClearRemoveFeature(effectDefinition, featureState))
        {
            return false;
        }
        if (
            !(
                featureState.blocks_move
                || featureState.blocks_occupancy
                || featureState.blocks_los
            )
        )
        {
            return false;
        }
        if (!GridService.ClearEdgeFeature(State, edgeCoord, edgeDirection))
        {
            return false;
        }
        _append_changed_coord(batch, first);
        _append_changed_coord(batch, second);
        AppendLog(
            batch,
            $"{_build_skill_log_subject_label(sourceUnit, skillDefinition)} 在 ({first.X}, {first.Y}) 与 ({second.X}, {second.Y}) 之间开辟通道，移除了{_get_edge_feature_display_name(featureState)}。"
        );
        return true;
    }

    private EdgeAuthoringReference BuildEdgeAuthoringReference(Vector2I from_coord, Vector2I to_coord)
    {
        Vector2I delta = to_coord - from_coord;
        if (delta == Vector2I.Right)
        {
            return new EdgeAuthoringReference(true, from_coord, Vector2I.Right);
        }
        if (delta == Vector2I.Left)
        {
            return new EdgeAuthoringReference(true, to_coord, Vector2I.Right);
        }
        if (delta == Vector2I.Down)
        {
            return new EdgeAuthoringReference(true, from_coord, Vector2I.Down);
        }
        if (delta == Vector2I.Up)
        {
            return new EdgeAuthoringReference(true, to_coord, Vector2I.Down);
        }
        return default;
    }

    private bool CanEdgeClearRemoveFeature(
        CombatEffectDefinition effectDefinition,
        BattleEdgeFeatureState featureState
    )
    {
        return BuildEdgeClearFeatureKindSet(effectDefinition)
            .Contains(featureState?.feature_kind ?? Empty);
    }

    private HashSet<StringName> BuildEdgeClearFeatureKindSet(
        CombatEffectDefinition effectDefinition
    )
    {
        var allowed = new HashSet<StringName>();
        foreach (
            StringName rawKind in effectDefinition?.GetStringNameListParamTyped(
                "clear_feature_kinds"
            ) ?? Array.Empty<StringName>()
        )
        {
            if (!IsEmpty(rawKind))
            {
                allowed.Add(rawKind);
            }
        }
        if (allowed.Count == 0)
        {
            allowed.Add(FeatureWall);
            allowed.Add(FeatureDoor);
            allowed.Add(FeatureGate);
        }
        return allowed;
    }

    internal string _get_edge_feature_display_name(BattleEdgeFeatureState feature_state)
    {
        if (feature_state == null)
        {
            return "阻挡边界";
        }
        StringName featureKind = feature_state?.feature_kind ?? Empty;
        if (featureKind == FeatureWall)
        {
            return "墙体";
        }
        if (featureKind == FeatureDoor)
        {
            return "门";
        }
        if (featureKind == FeatureGate)
        {
            return "闸门";
        }
        return "阻挡边界";
    }

    internal bool _apply_ground_cell_effect(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        Vector2I targetCoord,
        CombatEffectDefinition effectDefinition,
        BattleEventBatch batch
    )
    {
        BattleState state = State;
        BattleCellState cell = GridService.GetCellState(state, targetCoord);
        if (cell == null || effectDefinition == null)
        {
            return false;
        }
        bool cellApplied = false;
        StringName beforeTerrain = cell.base_terrain;
        int beforeHeight = cell.current_height;
        StringName occupantUnitId = cell.occupant_unit_id;
        BattleUnitState occupantUnit = null;
        if (!IsEmpty(occupantUnitId) && state != null)
        {
            state.TryGetUnitTyped(occupantUnitId, out occupantUnit);
        }
        BattleEffectKind effectKind = effectDefinition.EffectKind;
        if (
            effectKind == BattleEffectKind.Terrain
            || effectKind == BattleEffectKind.TerrainReplace
            || effectKind == BattleEffectKind.TerrainReplaceTo
        )
        {
            StringName terrainReplaceTo = effectDefinition.TerrainReplaceTo;
            if (!IsEmpty(terrainReplaceTo) && cell.base_terrain != terrainReplaceTo)
            {
                if (GridService.SetBaseTerrain(state, targetCoord, terrainReplaceTo))
                {
                    cellApplied = true;
                }
            }
        }
        else if (
            (
                effectKind == BattleEffectKind.Height
                || effectKind == BattleEffectKind.HeightDelta
            )
            && effectDefinition.HeightDelta != 0
        )
        {
            BattleHeightDeltaResult heightResult = GridService.ApplyHeightDeltaResult(
                state,
                targetCoord,
                effectDefinition.HeightDelta
            );
            if (heightResult.Changed)
            {
                cellApplied = true;
            }
        }

        int afterHeight = cell.current_height;
        if (beforeTerrain != cell.base_terrain || beforeHeight != afterHeight)
        {
            _append_changed_coord(batch, targetCoord);
        }
        if (beforeTerrain != cell.base_terrain)
        {
            AppendLog(
                batch,
                $"{_build_skill_log_subject_label(sourceUnit, skillDefinition)} 使 ({targetCoord.X}, {targetCoord.Y}) 的地形由 {GridService.GetTerrainDisplayName(beforeTerrain.ToString())} 变为 {GridService.GetTerrainDisplayName(cell.base_terrain.ToString())}。"
            );
        }
        if (beforeHeight != afterHeight)
        {
            AppendLog(
                batch,
                $"{_build_skill_log_subject_label(sourceUnit, skillDefinition)} 使 ({targetCoord.X}, {targetCoord.Y}) 的高度由 {beforeHeight} 变为 {afterHeight}。"
            );
        }

        BattleUnitState occupantUnitState = occupantUnit;
        if (occupantUnitState != null && occupantUnitState.IsAlive() && afterHeight < beforeHeight)
        {
            int fallLayers = beforeHeight - afterHeight;
            AttackEffectResolutionResult fallDamageResult =
                Runtime.GetDamageResolver().ResolveFallDamageResult(
                    occupantUnitState,
                    fallLayers,
                    State
                );
            int fallDamage = fallDamageResult.Damage;
            int shieldAbsorbed = fallDamageResult.ShieldAbsorbed;
            if (fallDamage > 0 || shieldAbsorbed > 0)
            {
                cellApplied = true;
                _append_changed_coord(batch, targetCoord);
                _append_changed_unit_id(batch, occupantUnitState.unit_id);
                if (fallDamage > 0)
                {
                    AppendLog(
                        batch,
                        $"{_build_skill_log_subject_label(sourceUnit, skillDefinition)} 使 ({targetCoord.X}, {targetCoord.Y}) 的高度下降 {fallLayers} 层，导致 {DisplayName(occupantUnit)} 坠落并受到 {fallDamage} 点伤害。"
                    );
                    if (shieldAbsorbed > 0)
                    {
                        AppendLog(
                            batch,
                            $"{DisplayName(occupantUnit)} 的护盾吸收了 {shieldAbsorbed} 点坠落伤害。"
                        );
                    }
                }
                else
                {
                    AppendLog(
                        batch,
                        $"{_build_skill_log_subject_label(sourceUnit, skillDefinition)} 使 ({targetCoord.X}, {targetCoord.Y}) 的高度下降 {fallLayers} 层，导致 {DisplayName(occupantUnit)} 坠落，但被护盾吸收了 {shieldAbsorbed} 点坠落伤害。"
                    );
                }
                if (fallDamageResult.ShieldBroken)
                {
                    AppendLog(batch, $"{DisplayName(occupantUnit)} 的护盾被击碎。");
                }
                if (!occupantUnitState.IsAlive())
                {
                    Runtime.HandleUnitDefeatedByRuntimeEffect(
                        occupantUnitState,
                        sourceUnit,
                        batch,
                        $"{DisplayName(occupantUnit)} 被击倒。",
                        new BattleDefeatHandlingOptions(recordEnemyDefeatedAchievement: true)
                    );
                }
            }
        }
        _flush_last_stand_mastery_records(batch);
        return cellApplied;
    }

    internal bool ReconcileWaterTopology(
        IReadOnlyList<Vector2I> effectCoords,
        BattleEventBatch batch
    )
    {
        BattleState state = State;
        if (
            state == null
            || state.map_size == Vector2I.Zero
            || effectCoords == null
            || effectCoords.Count == 0
        )
        {
            return false;
        }
        IReadOnlyList<BattleTerrainTopologyChange> changes =
            Runtime._terrain_topology_service.ReclassifyWaterTerrainNearCoords(
                state,
                effectCoords
            );
        bool applied = false;
        foreach (BattleTerrainTopologyChange change in changes)
        {
            Vector2I coord = change.Coord;
            BattleCellState cell = GridService.GetCellState(state, coord);
            if (cell == null)
            {
                continue;
            }
            StringName beforeTerrain = cell.base_terrain;
            Vector2I beforeFlowDirection = cell.flow_direction;
            StringName afterTerrain = change.AfterTerrain;
            Vector2I afterFlowDirection = change.AfterFlowDirection;
            if (beforeTerrain != afterTerrain)
            {
                GridService.SetBaseTerrain(state, coord, afterTerrain);
                cell = GridService.GetCellState(state, coord);
                if (cell == null)
                {
                    continue;
                }
            }
            if (cell.flow_direction != afterFlowDirection)
            {
                cell.flow_direction = afterFlowDirection;
                GridService.RecalculateCell(cell);
                GridService.SyncColumnFromSurfaceCell(state, coord);
            }
            if (
                beforeTerrain != cell.base_terrain
                || beforeFlowDirection != cell.flow_direction
            )
            {
                applied = true;
                _append_changed_coord(batch, coord);
            }
            if (beforeTerrain != cell.base_terrain)
            {
                AppendLog(
                    batch,
                    $"相邻水域在 ({coord.X}, {coord.Y}) 重分类为 {GridService.GetTerrainDisplayName(cell.base_terrain.ToString())}。"
                );
            }
        }
        return applied;
    }

    internal StringName _build_terrain_effect_instance_id(StringName effect_id)
    {
        if (Runtime == null)
        {
            return Empty;
        }
        int nonce = Runtime._terrain_effect_nonce + 1;
        Runtime._terrain_effect_nonce = nonce;
        BattleState state = State;
        int currentTu = state?.timeline != null ? state.timeline.current_tu : 0;
        return new StringName($"{effect_id}_{currentTu}_{nonce}");
    }

    internal string _get_terrain_effect_display_name(CombatEffectDefinition effectDefinition)
    {
        if (effectDefinition != null && !string.IsNullOrEmpty(effectDefinition.DisplayName))
        {
            return effectDefinition.DisplayName;
        }
        return effectDefinition != null
            ? effectDefinition.TerrainEffectId.ToString()
            : "地格效果";
    }

    private BattleState State => Runtime?._state;
    private BattleGridService GridService => Runtime?._grid_service;
    private BattleTargetCollectionService TargetCollectionService =>
        Runtime?._target_collection_service;
    private BattleSkillResolutionRules SkillResolutionRules => Runtime?._skill_resolution_rules;
    private BattleRuntimeModule Runtime => _runtime;
    private BattleLayeredBarrierService LayeredBarrierService => Runtime?._layered_barrier_service;

    private static bool IsArrayEmpty(GArray array)
    {
        return array == null || array.Count == 0;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || value.ToString().Length == 0;
    }

    internal static string ReadString(GDictionary source, string key, string fallback = "")
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = source[key];
        string result = value.ToString();
        return string.IsNullOrEmpty(result) || result == "<null>" ? fallback : result;
    }

    internal static bool HasParameter(IReadOnlyDictionary<string, object> source, string key)
    {
        return source != null && !string.IsNullOrEmpty(key) && source.ContainsKey(key);
    }

    internal static string ReadString(
        IReadOnlyDictionary<string, object> source,
        string key,
        string fallback = ""
    )
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.TryGetValue(key, out object value))
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

    private static GArray ReadArray(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
        {
            return new GArray();
        }
        Variant value = source[key];
        return value.AsGodotArray();
    }

    private static GArray ToUntypedStringNameArray(Godot.Collections.Array<StringName> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            result.Add(value);
        }
        return result;
    }

    internal static StringName ToStringName(object rawValue) =>
        ProgressionDataUtils.to_string_name(rawValue);

    private static GArray KeysArray(GDictionary dictionary)
    {
        var keys = new GArray();
        foreach (var key in dictionary.Keys)
        {
            keys.Add(key);
        }
        return keys;
    }

    internal static List<StringName> KeysStringNameList(GDictionary dictionary)
    {
        var keys = new List<StringName>();
        foreach (var key in dictionary.Keys)
        {
            keys.Add(ToStringName(key));
        }
        return keys;
    }

    internal static List<StringName> KeysStringNameList(HashSet<StringName> values)
    {
        return values != null ? new List<StringName>(values) : new List<StringName>();
    }

    internal static void AppendLog(BattleEventBatch batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
        {
            return;
        }
        batch.AddLogLine(line);
    }

    internal static string DisplayName(object value)
    {
        return value switch
        {
            BattleUnitState unitState => unitState.display_name,
            _ => "",
        };
    }

    private readonly struct GroundUnitEffectResolution
    {
        internal readonly AttackEffectResolutionResult Result;

        private GroundUnitEffectResolution(AttackEffectResolutionResult result)
        {
            Result = result;
        }

        internal static GroundUnitEffectResolution FromResult(AttackEffectResolutionResult result)
        {
            return new GroundUnitEffectResolution(result);
        }
    }

    private readonly struct EdgeAuthoringReference
    {
        internal readonly bool IsValid;
        internal readonly Vector2I Coord;
        internal readonly Vector2I Direction;

        internal EdgeAuthoringReference(bool isValid, Vector2I coord, Vector2I direction)
        {
            IsValid = isValid;
            Coord = coord;
            Direction = direction;
        }
    }

    private static BattleRuntimeModule ResolveWeakRef(WeakReference<BattleRuntimeModule> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out BattleRuntimeModule target))
        {
            return null;
        }
        return target;
    }
}
