using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GBattleUnitArray = System.Collections.Generic.List<BattleUnitState>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of BattleRuntimeModule — ground/shield/skill-mastery runtime effect application + loot collection.
// Pure physical split: same class, no behavior change. See BattleRuntimeModule.cs.
public sealed partial class BattleRuntimeModule
{

    internal bool ApplyGroundPrecastSpecialEffectsTyped(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> target_coords,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.ApplyGroundPrecastSpecialEffects(
            active_unit,
            skillDefinition,
            castVariantDefinition,
            target_coords ?? Array.Empty<Vector2I>(),
            batch
        );
    }

    internal bool ApplyGroundJumpRelocationTyped(
        BattleUnitState active_unit,
        IReadOnlyList<Vector2I> target_coords,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.ApplyGroundJumpRelocation(
            active_unit,
            target_coords ?? Array.Empty<Vector2I>(),
            batch
        );
    }

    internal IReadOnlyList<Vector2I> BuildGroundEffectCoordsTyped(
        SkillDefinition skillDefinition,
        IReadOnlyList<Vector2I> target_coords,
        Vector2I source_coord = default,
        BattleUnitState active_unit = null,
        CombatCastVariantDefinition castVariantDefinition = null
    )
    {
        _ensure_sidecars_ready();
        if (source_coord == default)
            source_coord = new Vector2I(-1, -1);
        return _ground_effect_service.BuildGroundEffectCoords(
            skillDefinition,
            target_coords ?? Array.Empty<Vector2I>(),
            source_coord,
            active_unit,
            castVariantDefinition
        );
    }

    internal IReadOnlyList<Vector2I> BuildGroundEffectCoordsTyped(
        SkillDefinition skillDefinition,
        IReadOnlyList<Vector2I> target_coords,
        Vector2I source_coord,
        BattleUnitReadView active_unit,
        CombatCastVariantDefinition castVariantDefinition = null
    )
    {
        _ensure_sidecars_ready();
        if (source_coord == default)
            source_coord = new Vector2I(-1, -1);
        return _ground_effect_service.BuildGroundEffectCoords(
            skillDefinition,
            target_coords ?? Array.Empty<Vector2I>(),
            source_coord,
            active_unit,
            castVariantDefinition
        );
    }

    internal IReadOnlyList<CombatEffectDefinition> CollectGroundUnitEffectDefinitionsTyped(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitState active_unit = null
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.CollectGroundUnitEffectDefinitions(
            skillDefinition,
            castVariantDefinition,
            active_unit
        );
    }

    internal IReadOnlyList<CombatEffectDefinition> CollectGroundUnitEffectDefinitionsTyped(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitReadView active_unit
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.CollectGroundUnitEffectDefinitions(
            skillDefinition,
            castVariantDefinition,
            active_unit
        );
    }

    internal IReadOnlyList<CombatEffectDefinition> CollectGroundTerrainEffectDefinitionsTyped(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitState active_unit = null
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.CollectGroundTerrainEffectDefinitions(
            skillDefinition,
            castVariantDefinition,
            active_unit
        );
    }

    internal IReadOnlyList<StringName> CollectGroundPreviewUnitIdsTyped(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> effectCoords
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.CollectGroundPreviewUnitIds(
            sourceUnit,
            skillDefinition,
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            effectCoords ?? Array.Empty<Vector2I>()
        );
    }

    internal IReadOnlyList<StringName> CollectGroundPreviewUnitIdsTyped(
        BattleUnitReadView sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> effectCoords
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.CollectGroundPreviewUnitIds(
            sourceUnit,
            skillDefinition,
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            effectCoords ?? Array.Empty<Vector2I>()
        );
    }

    internal BattleGroundUnitEffectsResult ApplyGroundUnitEffectsResultTyped(
        BattleUnitState source_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> effect_coords,
        BattleEventBatch batch,
        IReadOnlyList<Vector2I> target_coords = null
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._apply_ground_unit_effects_result(
            source_unit,
            skillDefinition,
            castVariantDefinition,
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            effect_coords ?? Array.Empty<Vector2I>(),
            batch,
            target_coords ?? Array.Empty<Vector2I>()
        );
    }

    internal BattleGroundTerrainEffectsResult ApplyGroundTerrainEffectsResultTyped(
        BattleUnitState source_unit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> effect_coords,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._apply_ground_terrain_effects_result(
            source_unit,
            skillDefinition,
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            effect_coords ?? Array.Empty<Vector2I>(),
            batch
        );
    }

    internal bool _reconcile_water_topology(GVector2IArray effect_coords, BattleEventBatch batch)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.ReconcileWaterTopology(ToVector2IList(effect_coords), batch);
    }

    internal BattleShieldApplyResult ApplyUnitShieldEffectsResult(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDefinition skill_definition,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        Dictionary<long, int> shield_roll_context = null
    )
    {
        _ensure_sidecars_ready();
        return _shield_service.ApplyUnitShieldEffectsResult(
            source_unit,
            target_unit,
            skill_definition,
            effect_definitions ?? Array.Empty<CombatEffectDefinition>(),
            shield_roll_context ?? new Dictionary<long, int>()
        );
    }

    internal void _write_unit_shield(
        BattleUnitState target_unit,
        int shield_hp,
        int shield_duration,
        StringName shield_family,
        StringName shield_source_unit_id,
        StringName shield_source_skill_id
    )
    {
        _ensure_sidecars_ready();
        _shield_service._write_unit_shield(
            target_unit,
            shield_hp,
            shield_duration,
            shield_family,
            shield_source_unit_id,
            shield_source_skill_id
        );
    }

    internal int _roll_battle_effect_die(int dice_sides)
    {
        _ensure_sidecars_ready();
        return _shield_service._roll_battle_effect_die(dice_sides);
    }

    internal bool _is_unit_valid_for_effect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_team_filter
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_unit_valid_for_effect(
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
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_unit_valid_for_effect(
            source_unit,
            target_unit,
            target_team_filter
        );
    }

    internal StringName _build_terrain_effect_instance_id(StringName effect_id)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._build_terrain_effect_instance_id(effect_id);
    }

    internal void _append_batch_log(BattleEventBatch batch, string message)
    {
        if (batch == null || string.IsNullOrEmpty(message))
            return;
        batch.AddLogLine(message);
        _state?.AppendLogEntry(message);
    }

    internal void _grant_skill_mastery_if_needed(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        BattleEventBatch batch
    )
    {
        _grant_skill_mastery_if_needed(
            active_unit,
            skillDefinition?.SkillId ?? new StringName(""),
            batch
        );
    }

    internal void _grant_skill_mastery_if_needed(
        BattleUnitState active_unit,
        StringName skillId,
        BattleEventBatch batch
    )
    {
        if (skillId == "")
            return;
        _record_skill_success(active_unit, skillId);
        if (
            active_unit == null
            || IsEmpty(active_unit.source_member_id)
            || _characterGateway == null
        )
            return;
        _battle_rating_system.RecordSkillSuccess(active_unit, skillId);
        _characterGateway.RecordAchievementEvent(
            active_unit.source_member_id,
            "skill_used",
            1,
            skillId,
            new GDictionary()
        );
        int masteryAmount = _skill_mastery_service.ResolveActiveSkillMasteryAmount();
        if (masteryAmount <= 0)
            return;
        StringName masterySkillId = _skill_mastery_service.ResolveMasteryRewardSkillId(
            active_unit,
            skillId
        );
        CharacterProgressionDelta delta = _characterGateway.GrantBattleMastery(
            active_unit.source_member_id,
            masterySkillId,
            masteryAmount
        );
        _append_progression_delta_to_batch(active_unit, delta, batch);
    }

    internal void _apply_skill_mastery_grant(
        BattleUnitState unit_state,
        GDictionary grant,
        BattleEventBatch batch
    )
    {
        ApplySkillMasteryGrantTyped(
            unit_state,
            BattleSkillMasteryGrant.FromDictionary(grant),
            batch
        );
    }

    internal void _apply_source_bound_weapon_bonus_mastery_grants(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult result,
        BattleEventBatch batch
    )
    {
        if (_skill_mastery_service == null)
            return;
        IReadOnlyList<BattleSkillMasteryGrant> grants =
            _skill_mastery_service.BuildSourceBoundWeaponBonusMasteryGrants(
                sourceUnit,
                targetUnit,
                result,
                GetSkillDefinitionIndexTyped()
            );
        foreach (BattleSkillMasteryGrant grant in grants ?? Array.Empty<BattleSkillMasteryGrant>())
        {
            ApplySkillMasteryGrantTyped(sourceUnit, grant, batch);
        }
    }

    internal void ApplySkillMasteryGrantTyped(
        BattleUnitState unitState,
        BattleSkillMasteryGrant grant,
        BattleEventBatch batch
    )
    {
        if (grant?.IsValid != true || _characterGateway == null)
            return;
        if (grant.RecordNearDeathUnbrokenManual)
            _characterGateway.RecordAchievementEvent(
                grant.MemberId,
                "near_death_unbroken_manual",
                1,
                "",
                new GDictionary()
            );
        CharacterProgressionDelta delta = _characterGateway.GrantSkillMasteryFromSource(
            grant.MemberId,
            grant.SkillId,
            grant.Amount,
            grant.SourceType,
            grant.SourceLabel,
            grant.ReasonText,
            grant.AllowUnlocks
        );
        _append_progression_delta_to_batch(unitState, delta, batch);
    }

    internal void _flush_last_stand_mastery_records(BattleEventBatch batch)
    {
        if (_damage_resolver == null)
            return;
        List<BattleSkillMasteryGrant> records =
            _damage_resolver.GetAndClearLastStandMasteryRecordsTyped();
        foreach (BattleSkillMasteryGrant record in records)
        {
            StringName memberId = record?.MemberId ?? "";
            BattleUnitState unitState = !IsEmpty(memberId)
                ? _find_unit_by_member_id(memberId)
                : null;
            ApplySkillMasteryGrantTyped(unitState, record, batch);
        }
    }

    internal void _append_progression_delta_to_batch(
        BattleUnitState unit_state,
        CharacterProgressionDelta delta,
        BattleEventBatch batch
    )
    {
        if (unit_state == null || delta == null)
            return;
        if (_progression_delta_is_empty(delta))
            return;
        batch?.AddProgressionDelta(delta);
        _unit_factory.RefreshKnownSkills(unit_state);
        if (delta.needs_promotion_modal)
        {
            if (_state == null)
                return;
            _state.ModalStateKind = BattleModalStateKind.PromotionChoice;
            if (_state.timeline != null)
                _state.timeline.frozen = true;
            if (batch != null)
            {
                batch.modal_requested = true;
                batch.AddLogLine($"{unit_state.display_name} 触发职业晋升选择。");
            }
        }
    }

    internal bool _progression_delta_is_empty(CharacterProgressionDelta delta)
    {
        if (delta == null)
            return true;
        return delta.MasteryChangesTyped.Count == 0
            && delta.LeveledSkillIdsTyped.Count == 0
            && delta.GrantedSkillIdsTyped.Count == 0
            && delta.ChangedProfessionIdsTyped.Count == 0
            && delta.KnowledgeChangesTyped.Count == 0
            && delta.AttributeChangesTyped.Count == 0
            && delta.UnlockedAchievementIdsTyped.Count == 0
            && !delta.needs_promotion_modal;
    }

    internal BattleGroundSkillValidationResult ValidateGroundSkillCommandResultTyped(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleCommand command
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._validate_ground_skill_command_result(
            active_unit,
            skillDefinition,
            castVariantDefinition,
            command
        );
    }

    internal BattleGroundSkillValidationResult ValidateGroundSkillCommandResultTyped(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleCommand command
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._validate_ground_skill_command_result(
            active_unit,
            skillDefinition,
            castVariantDefinition,
            command
        );
    }

    internal string GetGroundSpecialEffectValidationMessageTyped(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> target_coords
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.GetGroundSpecialEffectValidationMessage(
            active_unit,
            skillDefinition,
            castVariantDefinition,
            target_coords ?? Array.Empty<Vector2I>()
        );
    }

    internal string GetGroundSpecialEffectValidationMessageTyped(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> target_coords
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.GetGroundSpecialEffectValidationMessage(
            active_unit,
            skillDefinition,
            castVariantDefinition,
            target_coords ?? Array.Empty<Vector2I>()
        );
    }

    internal bool _validate_target_coords_shape(
        StringName footprint_pattern,
        GVector2IArray target_coords
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._validate_target_coords_shape(
            CombatSkillTargetingContentRules.ToFootprintPattern(footprint_pattern),
            target_coords
        );
    }

    internal GVector2IArray _normalize_target_coords(BattleCommand command)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._normalize_target_coords(command);
    }

    internal void _append_changed_coord(BattleEventBatch batch, Vector2I coord)
    {
        if (batch == null || batch.ContainsChangedCoord(coord))
            return;
        batch.AddChangedCoord(coord);
    }

    internal void _append_changed_coords(BattleEventBatch batch, GArray coords)
    {
        foreach (Vector2I coord in ToVector2IList(coords))
        {
            _append_changed_coord(batch, coord);
        }
    }

    internal void _append_changed_coords(BattleEventBatch batch, GVector2IArray coords)
    {
        if (coords == null)
            return;
        foreach (Vector2I coord in coords)
        {
            _append_changed_coord(batch, coord);
        }
    }

    internal void _append_changed_coords_typed(
        BattleEventBatch batch,
        IEnumerable<Vector2I> coords
    )
    {
        if (coords == null)
            return;
        foreach (Vector2I coord in coords)
        {
            _append_changed_coord(batch, coord);
        }
    }

    internal void _append_changed_unit_id(BattleEventBatch batch, StringName unit_id)
    {
        if (batch == null || IsEmpty(unit_id) || batch.ContainsChangedUnitId(unit_id))
            return;
        batch.AddChangedUnitId(unit_id);
    }

    internal void _append_changed_unit_coords(BattleEventBatch batch, BattleUnitState unit_state)
    {
        if (unit_state == null)
            return;
        unit_state.RefreshFootprint();
        _append_changed_coords(batch, unit_state.occupied_coords);
    }

    internal void _collect_defeated_unit_loot(
        BattleUnitState unit_state,
        BattleUnitState killer_unit = null,
        BattleEventBatch batch = null,
        BattleKillProvenance killProvenance = default
    ) => _loot_resolver.CollectDefeatedUnitLoot(
        unit_state,
        killer_unit,
        batch,
        killProvenance
    );
}
