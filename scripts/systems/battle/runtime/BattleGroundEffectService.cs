using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
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
        internal static GroundEffectRuntimeParameters FromEffect(CombatEffectDef effectDef)
        {
            return new GroundEffectRuntimeParameters(effectDef?.resolve_as_weapon_attack ?? false);
        }
    }

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    internal void Dispose()
    {
        _runtime = null;
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
        Runtime?.MarkAppliedStatusesForTurnTiming(
            target_unit,
            status_effect_ids ?? new GArray()
        );
    }

    internal void MarkAppliedStatusesForTurnTiming(
        BattleUnitState target_unit,
        GStringNameArray status_effect_ids
    )
    {
        Runtime?.MarkAppliedStatusesForTurnTiming(
            target_unit,
            status_effect_ids ?? new GStringNameArray()
        );
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
        BattleUnitState source_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null
    )
    {
        return _runtime == null
            ? ""
            : Runtime._build_skill_log_subject_label(
                source_unit,
                skill_def,
                cast_variant
            );
    }

    internal void _apply_on_kill_gain_resources_effects(
        BattleUnitState source_unit,
        BattleUnitState defeated_unit,
        SkillDef skill_def,
        GArray effect_defs,
        BattleEventBatch batch
    )
    {
        Runtime?._apply_on_kill_gain_resources_effects(
            source_unit,
            defeated_unit,
            skill_def,
            ToCombatEffectDefArray(effect_defs),
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
        SkillDef skillDef,
        AttackEffectResolutionResult result,
        BattleEventBatch batch = null
    )
    {
        Runtime?.RecordVajraBodyMasteryFromIncomingDamageTyped(
            sourceUnit,
            targetUnit,
            skillDef,
            result,
            batch
        );
    }

    internal BattleShieldApplyResult ApplyUnitShieldEffectsResult(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GArray effect_defs,
        Dictionary<long, int> shield_roll_context = null
    )
    {
        if (_runtime == null)
        {
            return new BattleShieldApplyResult(false, 0, 0, -1, Empty);
        }
        return Runtime.ApplyUnitShieldEffectsResult(
                source_unit,
                target_unit,
                skill_def,
                ToCombatEffectDefArray(effect_defs),
                shield_roll_context ?? new Dictionary<long, int>()
        );
    }

    internal StringName _resolve_effect_target_filter(SkillDef skill_def, CombatEffectDef effect_def)
    {
        return _runtime == null
            ? Empty
            : ToStringName(
                Runtime._resolve_effect_target_filter(skill_def, effect_def)
            );
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

    private void AppendChangedCoords(BattleEventBatch batch, IReadOnlyList<Vector2I> coords)
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

    internal void _collect_defeated_unit_loot(BattleUnitState unit_state, BattleUnitState killer_unit = null)
    {
        Runtime?._collect_defeated_unit_loot(unit_state, killer_unit);
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
        SkillDef skill_def
    )
    {
        return _runtime == null
            ? BattleSkillCastBlockReasonKind.SkillCastCheckUnbound
            : Runtime._get_skill_cast_block_reason(active_unit, skill_def);
    }

    internal CombatSkillResourceCosts _get_effective_skill_resource_costs(
        BattleUnitState active_unit,
        SkillDef skill_def
    )
    {
        return _runtime == null
            ? CombatSkillResourceCosts.Zero
            : Runtime._get_effective_skill_resource_costs(active_unit, skill_def);
    }

    internal int _get_effective_skill_range(BattleUnitState active_unit, SkillDef skill_def)
    {
        return _runtime == null
            ? 0
            : Runtime._get_effective_skill_range(active_unit, skill_def);
    }

    internal int _get_effective_skill_range(BattleUnitReadView active_unit, SkillDef skill_def)
    {
        return _runtime == null
            ? 0
            : Runtime._get_effective_skill_range(active_unit, skill_def);
    }

    internal bool _is_movement_blocked(BattleUnitState unit_state)
    {
        return _runtime != null && Runtime._is_movement_blocked(unit_state);
    }

    internal bool _is_movement_blocked(BattleUnitReadView unitView)
    {
        return _runtime != null && Runtime._movement_service.IsMovementBlocked(unitView);
    }

    internal BattleSpellControlResult ResolveGroundSpellControlAfterCostResult(
        BattleUnitState active_unit,
        SkillDef skill_def,
        int spent_mp,
        BattleEventBatch batch
    )
    {
        BattleDamageResolver damageResolver = Runtime?.GetDamageResolver();
        BattleMagicBacklashResolver magicBacklashResolver = Runtime?._magic_backlash_resolver;
        if (
            damageResolver == null
            || magicBacklashResolver == null
            || !magicBacklashResolver.ShouldResolveSpellControl(skill_def as SkillDef)
        )
        {
            return BattleSpellControlResult.None();
        }
        StringName skillId = skill_def?.skill_id ?? Empty;
        int skillLevel = _get_unit_skill_level(active_unit, skillId);
        BattleSpellControlMetadata controlMetadata = damageResolver.ResolveSpellControlCheckTyped(
            active_unit,
            State,
            skillId
        );
        BattleSpellControlResult controlContext =
            magicBacklashResolver.ApplySpellControlAfterCostResult(
                active_unit,
                skill_def,
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
        SkillDef skill_def,
        BattleEventBatch batch
    )
    {
        BattleDamageResolver damageResolver = Runtime?.GetDamageResolver();
        BattleMagicBacklashResolver magicBacklashResolver = Runtime?._magic_backlash_resolver;
        if (
            damageResolver == null
            || magicBacklashResolver == null
            || !magicBacklashResolver.ShouldResolveSpellControl(skill_def as SkillDef)
        )
        {
            return BattleSpellControlResult.None();
        }
        StringName skillId = skill_def?.skill_id ?? Empty;
        int skillLevel = _get_unit_skill_level(active_unit, skillId);
        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(active_unit, skill_def);
        int spentMp = costs.MpCost;
        BattleSpellControlMetadata controlMetadata = damageResolver.ResolveSpellControlCheckTyped(
            active_unit,
            State,
            skillId
        );
        BattleSpellControlResult controlContext =
            magicBacklashResolver.ApplySpellControlAfterCostResult(
                active_unit,
                skill_def,
                skillLevel,
                spentMp,
                controlMetadata,
                batch
            );
        _append_changed_unit_id(batch, active_unit?.unit_id ?? Empty);
        return controlContext;
    }

    internal bool ApplyGroundPrecastSpecialEffects(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        IReadOnlyList<Vector2I> target_coords,
        BattleEventBatch batch
    )
    {
        return _get_ground_relocation_effect_def(skill_def, cast_variant) == null
            || ApplyGroundRelocation(active_unit, skill_def, cast_variant, target_coords, batch);
    }

    private bool ApplyGroundRelocation(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        IReadOnlyList<Vector2I> target_coords,
        BattleEventBatch batch
    )
    {
        if (State == null || active_unit == null || target_coords == null || target_coords.Count == 0)
        {
            return false;
        }
        CombatEffectDef effectDef = _get_ground_relocation_effect_def(skill_def, cast_variant);
        return effectDef != null
            && ApplyGroundRelocationWithMode(
                active_unit,
                target_coords,
                batch,
                effectDef.ForcedMoveModeKind
            );
    }

    private bool ApplyGroundRelocationWithMode(
        BattleUnitState active_unit,
        IReadOnlyList<Vector2I> target_coords,
        BattleEventBatch batch,
        BattleForcedMoveMode move_mode
    )
    {
        BattleState state = State;
        BattleGridService gridService = GridService;
        if (
            state == null
            || gridService == null
            || active_unit == null
            || target_coords == null
            || target_coords.Count == 0
        )
        {
            return false;
        }
        Vector2I landingCoord = target_coords[0];
        if (active_unit.coord == landingCoord)
        {
            return true;
        }
        Vector2I previousAnchor = active_unit.coord;
        List<Vector2I> previousCoords = ToVector2IList(active_unit.occupied_coords);
        BattleLayeredBarrierService layeredBarrierService = LayeredBarrierService;
        if (layeredBarrierService != null)
        {
            BattleBarrierInteractionResult barrierResult =
                layeredBarrierService.ResolveUnitBoundaryCrossingResult(
                    active_unit,
                    previousAnchor,
                    landingCoord,
                    batch
                );
            if (
                barrierResult.Blocked
                || !active_unit.is_alive
                || active_unit.coord != previousAnchor
            )
            {
                return false;
            }
        }
        if (!gridService.MoveUnitForce(state, active_unit, landingCoord))
        {
            return false;
        }
        AppendChangedCoords(batch, previousCoords);
        _append_changed_unit_coords(batch, active_unit);
        _append_changed_unit_id(batch, active_unit.unit_id);
        string moveLabel = move_mode == BattleForcedMoveMode.Blink ? "闪现至" : "跳至";
        AppendLog(
            batch,
            $"{DisplayName(active_unit)} 从 ({previousAnchor.X}, {previousAnchor.Y}) {moveLabel} ({landingCoord.X}, {landingCoord.Y})。"
        );
        return true;
    }

    internal bool ApplyGroundJumpRelocation(
        BattleUnitState active_unit,
        IReadOnlyList<Vector2I> target_coords,
        BattleEventBatch batch
    )
    {
        return ApplyGroundRelocationWithMode(
            active_unit,
            target_coords,
            batch,
            BattleForcedMoveMode.Jump
        );
    }

    internal CombatEffectDef _get_ground_relocation_effect_def(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        if (cast_variant != null)
        {
            foreach (CombatEffectDef effectDef in cast_variant.effect_defs ?? new GCombatEffectArray())
            {
                if (_is_ground_relocation_effect(effectDef))
                {
                    return effectDef;
                }
            }
        }
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (combatProfile != null)
        {
            foreach (CombatEffectDef effectDef in combatProfile.effect_defs ?? new GCombatEffectArray())
            {
                if (_is_ground_relocation_effect(effectDef))
                {
                    return effectDef;
                }
            }
        }
        return null;
    }

    internal CombatEffectDef _get_ground_jump_effect_def(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        CombatEffectDef effectDef = _get_ground_relocation_effect_def(skill_def, cast_variant);
        return effectDef?.ForcedMoveModeKind == BattleForcedMoveMode.Jump ? effectDef : null;
    }

    internal bool _is_ground_jump_effect(CombatEffectDef effect_def)
    {
        return effect_def != null
            && effect_def.EffectKind == BattleEffectKind.ForcedMove
            && effect_def.ForcedMoveModeKind == BattleForcedMoveMode.Jump;
    }

    internal bool _is_ground_relocation_effect(CombatEffectDef effect_def)
    {
        return effect_def != null
            && effect_def.EffectKind == BattleEffectKind.ForcedMove
            && _is_ground_relocation_mode(effect_def.ForcedMoveModeKind);
    }

    internal bool _is_ground_relocation_mode(BattleForcedMoveMode mode)
    {
        return mode == BattleForcedMoveMode.Jump || mode == BattleForcedMoveMode.Blink;
    }

    internal bool _can_use_ground_relocation(
        BattleUnitState active_unit,
        Vector2I landing_coord,
        CombatEffectDef effect_def
    )
    {
        if (effect_def == null || GridService == null)
        {
            return false;
        }
        BattleForcedMoveMode mode = effect_def.ForcedMoveModeKind;
        if (mode == BattleForcedMoveMode.Jump)
        {
            return GridService.CanJumpArc(
                State,
                active_unit,
                landing_coord,
                effect_def
            );
        }
        if (mode == BattleForcedMoveMode.Blink)
        {
            return GridService.CanBlinkToCoord(
                State,
                active_unit,
                landing_coord,
                effect_def
            );
        }
        return false;
    }

    internal bool _can_use_ground_relocation(
        BattleUnitReadView active_unit,
        Vector2I landing_coord,
        CombatEffectDef effect_def
    )
    {
        if (effect_def == null || GridService == null)
        {
            return false;
        }
        BattleForcedMoveMode mode = effect_def.ForcedMoveModeKind;
        if (mode == BattleForcedMoveMode.Jump)
        {
            return GridService.CanJumpArc(
                State,
                active_unit,
                landing_coord,
                effect_def
            );
        }
        if (mode == BattleForcedMoveMode.Blink)
        {
            return GridService.CanBlinkToCoord(
                State,
                active_unit,
                landing_coord,
                effect_def
            );
        }
        return false;
    }

    internal IReadOnlyList<Vector2I> BuildGroundEffectCoords(
        SkillDef skill_def,
        IReadOnlyList<Vector2I> target_coords,
        Vector2I source_coord,
        BattleUnitState active_unit,
        CombatCastVariantDef cast_variant
    )
    {
        var normalizedTargetCoords = new List<Vector2I>(target_coords ?? System.Array.Empty<Vector2I>());
        GDictionary castVariantParams = cast_variant?.@params ?? new GDictionary();
        if (
            cast_variant != null
            && castVariantParams.ContainsKey("square2_corner")
            && normalizedTargetCoords.Count == 1
        )
        {
            Vector2I center = normalizedTargetCoords[0];
            var expanded = new List<Vector2I>(4);
            string corner = ReadString(castVariantParams, "square2_corner");
            if (corner == "top_left")
            {
                expanded.Add(center);
                expanded.Add(new Vector2I(center.X + 1, center.Y));
                expanded.Add(new Vector2I(center.X, center.Y + 1));
                expanded.Add(new Vector2I(center.X + 1, center.Y + 1));
            }
            else if (corner == "top_right")
            {
                expanded.Add(new Vector2I(center.X - 1, center.Y));
                expanded.Add(center);
                expanded.Add(new Vector2I(center.X - 1, center.Y + 1));
                expanded.Add(new Vector2I(center.X, center.Y + 1));
            }
            else if (corner == "bottom_left")
            {
                expanded.Add(new Vector2I(center.X, center.Y - 1));
                expanded.Add(new Vector2I(center.X + 1, center.Y - 1));
                expanded.Add(center);
                expanded.Add(new Vector2I(center.X + 1, center.Y));
            }
            else if (corner == "bottom_right")
            {
                expanded.Add(new Vector2I(center.X - 1, center.Y - 1));
                expanded.Add(new Vector2I(center.X, center.Y - 1));
                expanded.Add(new Vector2I(center.X - 1, center.Y));
                expanded.Add(center);
            }
            var valid = new List<Vector2I>(expanded.Count);
            foreach (Vector2I coord in expanded)
            {
                if (
                    State != null
                    && GridService != null
                    && GridService.IsInside(State, coord)
                )
                {
                    valid.Add(coord);
                }
            }
            if (valid.Count > 0)
            {
                return SortCoordsTyped(valid);
            }
        }
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (State == null || skill_def == null || combatProfile == null)
        {
            return SortCoordsTyped(normalizedTargetCoords);
        }
        int skillLevel = _get_unit_skill_level(
            active_unit,
            skill_def.skill_id
        );
        BattleTargetCollectionResult collectedTargetCoords =
            TargetCollectionService.CollectCombatProfileTargetCoords(
                State,
                GridService,
                source_coord,
                combatProfile,
                normalizedTargetCoords,
                null,
                System.Array.Empty<BattleUnitState>(),
                skillLevel
            );
        if (collectedTargetCoords.Handled)
        {
            return SortCoordsTyped(collectedTargetCoords.TargetCoords);
        }
        return SortCoordsTyped(normalizedTargetCoords);
    }

    internal IReadOnlyList<Vector2I> BuildGroundEffectCoords(
        SkillDef skill_def,
        IReadOnlyList<Vector2I> target_coords,
        Vector2I source_coord,
        BattleUnitReadView active_unit,
        CombatCastVariantDef cast_variant
    )
    {
        var normalizedTargetCoords = new List<Vector2I>(target_coords ?? System.Array.Empty<Vector2I>());
        GDictionary castVariantParams = cast_variant?.@params ?? new GDictionary();
        if (
            cast_variant != null
            && castVariantParams.ContainsKey("square2_corner")
            && normalizedTargetCoords.Count == 1
        )
        {
            Vector2I center = normalizedTargetCoords[0];
            var expanded = new List<Vector2I>(4);
            string corner = ReadString(castVariantParams, "square2_corner");
            if (corner == "top_left")
            {
                expanded.Add(center);
                expanded.Add(new Vector2I(center.X + 1, center.Y));
                expanded.Add(new Vector2I(center.X, center.Y + 1));
                expanded.Add(new Vector2I(center.X + 1, center.Y + 1));
            }
            else if (corner == "top_right")
            {
                expanded.Add(new Vector2I(center.X - 1, center.Y));
                expanded.Add(center);
                expanded.Add(new Vector2I(center.X - 1, center.Y + 1));
                expanded.Add(new Vector2I(center.X, center.Y + 1));
            }
            else if (corner == "bottom_left")
            {
                expanded.Add(new Vector2I(center.X, center.Y - 1));
                expanded.Add(new Vector2I(center.X + 1, center.Y - 1));
                expanded.Add(center);
                expanded.Add(new Vector2I(center.X + 1, center.Y));
            }
            else if (corner == "bottom_right")
            {
                expanded.Add(new Vector2I(center.X - 1, center.Y - 1));
                expanded.Add(new Vector2I(center.X, center.Y - 1));
                expanded.Add(new Vector2I(center.X - 1, center.Y));
                expanded.Add(center);
            }
            var valid = new List<Vector2I>(expanded.Count);
            foreach (Vector2I coord in expanded)
            {
                if (
                    State != null
                    && GridService != null
                    && GridService.IsInside(State, coord)
                )
                {
                    valid.Add(coord);
                }
            }
            if (valid.Count > 0)
            {
                return SortCoordsTyped(valid);
            }
        }
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (State == null || skill_def == null || combatProfile == null)
        {
            return SortCoordsTyped(normalizedTargetCoords);
        }
        int skillLevel = active_unit.GetKnownSkillLevel(skill_def.skill_id);
        BattleTargetCollectionResult collectedTargetCoords =
            TargetCollectionService.CollectCombatProfileTargetCoords(
                State,
                GridService,
                source_coord,
                combatProfile,
                normalizedTargetCoords,
                active_unit,
                System.Array.Empty<BattleUnitReadView>(),
                skillLevel
            );
        if (collectedTargetCoords.Handled)
        {
            return SortCoordsTyped(collectedTargetCoords.TargetCoords);
        }
        return SortCoordsTyped(normalizedTargetCoords);
    }

    internal IReadOnlyList<CombatEffectDef> CollectGroundUnitEffectDefs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit
    )
    {
        return SkillResolutionRules?.CollectGroundUnitEffectDefs(
                skill_def,
                cast_variant,
                active_unit
            ) ?? new List<CombatEffectDef>();
    }

    internal IReadOnlyList<CombatEffectDef> CollectGroundUnitEffectDefs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitReadView active_unit
    )
    {
        return SkillResolutionRules?.CollectGroundUnitEffectDefs(
                skill_def,
                cast_variant,
                active_unit
            ) ?? new List<CombatEffectDef>();
    }

    internal IReadOnlyList<CombatEffectDef> CollectGroundTerrainEffectDefs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit
    )
    {
        return SkillResolutionRules?.CollectGroundTerrainEffectDefs(
                skill_def,
                cast_variant,
                active_unit
            ) ?? new List<CombatEffectDef>();
    }

    internal IReadOnlyList<StringName> CollectGroundPreviewUnitIds(
        BattleUnitState source_unit,
        SkillDef skill_def,
        IReadOnlyList<CombatEffectDef> effect_defs,
        IReadOnlyList<Vector2I> effect_coords
    )
    {
        var targetUnitIds = new List<StringName>();
        foreach (BattleUnitState targetUnit in CollectUnitsInCoords(effect_coords))
        {
            foreach (CombatEffectDef effectDef in effect_defs ?? Array.Empty<CombatEffectDef>())
            {
                if (
                    _is_unit_valid_for_effect(
                        source_unit,
                        targetUnit,
                        _resolve_effect_target_filter(skill_def, effectDef)
                    )
                )
                {
                    if (targetUnit != null)
                    {
                        targetUnitIds.Add(targetUnit.unit_id);
                    }
                    break;
                }
            }
        }
        return targetUnitIds;
    }

    internal IReadOnlyList<StringName> CollectGroundPreviewUnitIds(
        BattleUnitReadView source_unit,
        SkillDef skill_def,
        IReadOnlyList<CombatEffectDef> effect_defs,
        IReadOnlyList<Vector2I> effect_coords
    )
    {
        var targetUnitIds = new List<StringName>();
        foreach (BattleUnitState targetUnit in CollectUnitsInCoords(effect_coords))
        {
            BattleUnitReadView targetView = new(targetUnit);
            foreach (CombatEffectDef effectDef in effect_defs ?? Array.Empty<CombatEffectDef>())
            {
                if (
                    _is_unit_valid_for_effect(
                        source_unit,
                        targetView,
                        _resolve_effect_target_filter(skill_def, effectDef)
                    )
                )
                {
                    if (targetView.IsValid)
                    {
                        targetUnitIds.Add(targetView.UnitId);
                    }
                    break;
                }
            }
        }
        return targetUnitIds;
    }

    private List<BattleUnitState> CollectUnitsInCoords(IReadOnlyList<Vector2I> effectCoords)
    {
        return _runtime == null
            ? new List<BattleUnitState>()
            : new List<BattleUnitState>(Runtime._skill_orchestrator.CollectUnitsInCoords(effectCoords));
    }

    private static BattleForcedMoveContext BuildGroundForcedMoveContext(
        BattleUnitState sourceUnit,
        IReadOnlyList<Vector2I> targetCoords
    )
    {
        if (sourceUnit == null || targetCoords == null || targetCoords.Count == 0)
        {
            return BattleForcedMoveContext.Empty;
        }
        return BattleForcedMoveContext.FromDirection(targetCoords[0] - sourceUnit.coord);
    }

    internal Vector2I _normalize_axis_direction(Vector2I direction)
    {
        if (direction == Vector2I.Zero)
        {
            return Vector2I.Zero;
        }
        int absX = Math.Abs(direction.X);
        int absY = Math.Abs(direction.Y);
        if (absX >= absY && absX > 0)
        {
            return new Vector2I(direction.X > 0 ? 1 : -1, 0);
        }
        if (absY > 0)
        {
            return new Vector2I(0, direction.Y > 0 ? 1 : -1);
        }
        return Vector2I.Zero;
    }

    internal bool _is_wind_push_effect(CombatEffectDef effect_def)
    {
        return effect_def != null
            && effect_def.EffectKind == BattleEffectKind.ForcedMove
            && effect_def.ForcedMoveModeKind == BattleForcedMoveMode.WindPush;
    }

    private static IReadOnlyList<CombatEffectDef> CollectWindPushEffects(
        IReadOnlyList<CombatEffectDef> effectDefs
    )
    {
        var windPushEffects = new List<CombatEffectDef>();
        var seen = new HashSet<ulong>();
        foreach (CombatEffectDef effectDef in effectDefs ?? Array.Empty<CombatEffectDef>())
        {
            if (
                effectDef == null
                || effectDef.EffectKind != BattleEffectKind.ForcedMove
                || effectDef.ForcedMoveModeKind != BattleForcedMoveMode.WindPush
            )
            {
                continue;
            }
            ulong instanceId = effectDef.GetInstanceId();
            if (seen.Add(instanceId))
            {
                windPushEffects.Add(effectDef);
            }
        }
        return windPushEffects;
    }

    private static HashSet<ulong> BuildEffectInstanceIdSet(
        IReadOnlyList<CombatEffectDef> effectDefs
    )
    {
        var result = new HashSet<ulong>();
        foreach (CombatEffectDef effectDef in effectDefs ?? Array.Empty<CombatEffectDef>())
        {
            if (effectDef != null)
            {
                result.Add(effectDef.GetInstanceId());
            }
        }
        return result;
    }

    internal int _dot_coord(Vector2I coord, Vector2I direction) =>
        coord.X * direction.X + coord.Y * direction.Y;

    internal int _perpendicular_coord(Vector2I coord, Vector2I direction) =>
        direction.X != 0 ? coord.Y : coord.X;

    private List<BattleUnitState> SortWindPushUnitsNearToFar(
        IReadOnlyList<BattleUnitState> units,
        Vector2I direction
    )
    {
        var sorted = new List<BattleUnitState>();
        foreach (BattleUnitState unitState in units ?? Array.Empty<BattleUnitState>())
        {
            if (unitState != null && unitState.is_alive)
            {
                sorted.Add(unitState);
            }
        }
        sorted.Sort(
            (left, right) =>
            {
                int leftProjection = _dot_coord(left.coord, direction);
                int rightProjection = _dot_coord(right.coord, direction);
                if (leftProjection != rightProjection)
                {
                    return leftProjection.CompareTo(rightProjection);
                }
                int leftSide = _perpendicular_coord(left.coord, direction);
                int rightSide = _perpendicular_coord(right.coord, direction);
                if (leftSide != rightSide)
                {
                    return leftSide.CompareTo(rightSide);
                }
                return string.Compare(
                    left.unit_id.ToString(),
                    right.unit_id.ToString(),
                    StringComparison.Ordinal
                );
            }
        );
        return sorted;
    }

    private static void AppendAffectedUnitId(
        HashSet<StringName> affectedUnitIds,
        BattleUnitState unitState
    )
    {
        if (unitState != null)
        {
            affectedUnitIds?.Add(unitState.unit_id);
        }
    }

    private List<BattleUnitState> CollectWindPushTargetUnits(
        BattleUnitState source_unit,
        SkillDef skill_def,
        CombatEffectDef effect_def,
        IReadOnlyList<Vector2I> effect_coords,
        BattleEventBatch batch,
        HashSet<StringName> affected_unit_ids,
        out bool applied
    )
    {
        applied = false;
        var units = new List<BattleUnitState>();
        if (effect_def == null)
        {
            return units;
        }
        StringName targetFilter = _resolve_effect_target_filter(skill_def, effect_def);
        var barrierEffects = new GCombatEffectArray { effect_def };
        BattleLayeredBarrierService layeredBarrierService = LayeredBarrierService;
        foreach (BattleUnitState targetUnit in CollectUnitsInCoords(ToVector2IList(effect_coords)))
        {
            if (targetUnit == null || !targetUnit.is_alive)
            {
                continue;
            }
            if (!_is_unit_valid_for_effect(source_unit, targetUnit, targetFilter))
            {
                continue;
            }
            BattleBarrierInteractionResult barrierResult =
                layeredBarrierService != null
                    ? layeredBarrierService.ResolveSkillBarrierInteractionResult(
                        source_unit,
                        targetUnit,
                        skill_def,
                        barrierEffects,
                        batch
                    )
                    : new BattleBarrierInteractionResult(false, false);
            if (barrierResult.Blocked)
            {
                if (barrierResult.Applied)
                {
                    applied = true;
                    AppendAffectedUnitId(affected_unit_ids, targetUnit);
                }
                continue;
            }
            units.Add(targetUnit);
        }
        return units;
    }

    private bool TryWindPushUnitOneStep(
        BattleUnitState source_unit,
        SkillDef skill_def,
        CombatEffectDef effect_def,
        BattleUnitState unit_state,
        Vector2I direction,
        HashSet<StringName> moved_this_step,
        HashSet<StringName> affected_unit_ids,
        HashSet<StringName> recursion_stack,
        BattleEventBatch batch
    )
    {
        BattleState state = State;
        BattleGridService gridService = GridService;
        BattleUnitState unitState = unit_state;
        if (
            Runtime == null
            || state == null
            || gridService == null
            || unitState == null
            || !unitState.is_alive
            || direction == Vector2I.Zero
        )
        {
            return false;
        }
        StringName unitId = unitState.unit_id;
        if (moved_this_step.Contains(unitId))
        {
            return false;
        }
        if (
            Runtime._blocks_enemy_forced_move(
                source_unit,
                unitState
            )
        )
        {
            AppendLog(batch, $"{unitState.display_name} 稳如金刚，未被强制位移。");
            return false;
        }
        if (recursion_stack.Contains(unitId))
        {
            return false;
        }
        Vector2I currentCoord = unitState.coord;
        Vector2I nextCoord = currentCoord + direction;
        if (!gridService.IsInside(state, nextCoord))
        {
            return false;
        }
        var nextStack = new HashSet<StringName>(recursion_stack) { unitId };
        StringName targetFilter = _resolve_effect_target_filter(skill_def, effect_def);
        foreach (
            var rawBlockingUnitId in gridService.CollectBlockingUnitIds(
                state,
                unitState,
                nextCoord
            )
        )
        {
            StringName blockingUnitId = ToStringName(rawBlockingUnitId);
            if (blockingUnitId == unitId)
            {
                continue;
            }
            if (
                !state.TryGetUnitTyped(blockingUnitId, out BattleUnitState blockingUnit)
                || !blockingUnit.is_alive
            )
            {
                return false;
            }
            if (!_is_unit_valid_for_effect(source_unit, blockingUnit, targetFilter))
            {
                return false;
            }
            if (
                !TryWindPushUnitOneStep(
                    source_unit,
                    skill_def,
                    effect_def,
                    blockingUnit,
                    direction,
                    moved_this_step,
                    affected_unit_ids,
                    nextStack,
                    batch
                )
            )
            {
                return false;
            }
        }
        if (
            !gridService.CanTraverse(
                state,
                currentCoord,
                nextCoord,
                unitState
            )
        )
        {
            return false;
        }
        BattleLayeredBarrierService layeredBarrierService = LayeredBarrierService;
        BattleBarrierInteractionResult barrierResult =
            layeredBarrierService != null
                ? layeredBarrierService.ResolveUnitBoundaryCrossingResult(
                    unitState,
                    currentCoord,
                    nextCoord,
                    batch
                )
                : new BattleBarrierInteractionResult(false, false);
        if (barrierResult.Blocked || !unitState.is_alive)
        {
            AppendAffectedUnitId(affected_unit_ids, unit_state);
            return false;
        }
        List<Vector2I> previousCoords = ToVector2IList(unitState.occupied_coords);
        if (!gridService.MoveUnit(state, unitState, nextCoord))
        {
            return false;
        }
        moved_this_step.Add(unitId);
        AppendAffectedUnitId(affected_unit_ids, unit_state);
        AppendChangedCoords(batch, previousCoords);
        _append_changed_unit_coords(batch, unit_state);
        _append_changed_unit_id(batch, unitId);
        return true;
    }

    internal BattleGroundWindPushResult _apply_ground_wind_push_effects_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        IReadOnlyList<CombatEffectDef> wind_push_effects,
        IReadOnlyList<Vector2I> effect_coords,
        IReadOnlyList<Vector2I> target_coords,
        BattleEventBatch batch
    )
    {
        bool applied = false;
        if (
            source_unit == null
            || wind_push_effects == null
            || wind_push_effects.Count == 0
        )
        {
            return new BattleGroundWindPushResult(false, System.Array.Empty<StringName>());
        }
        BattleForcedMoveContext forcedMoveContext = BuildGroundForcedMoveContext(
            source_unit,
            target_coords
        );
        Vector2I direction = forcedMoveContext.Direction;
        if (direction == Vector2I.Zero)
        {
            return new BattleGroundWindPushResult(false, System.Array.Empty<StringName>());
        }
        var affectedUnitIds = new HashSet<StringName>();
        foreach (CombatEffectDef effectDef in wind_push_effects)
        {
            if (effectDef == null)
            {
                continue;
            }
            List<BattleUnitState> targetUnits = CollectWindPushTargetUnits(
                source_unit,
                skill_def,
                effectDef,
                effect_coords,
                batch,
                affectedUnitIds,
                out bool barrierApplied
            );
            applied = applied || barrierApplied;
                if (targetUnits.Count == 0)
                {
                    continue;
                }
                int moveDistance = Math.Max(effectDef.forced_move_distance, 0);
                for (int stepIndex = 0; stepIndex < moveDistance; stepIndex++)
                {
                    var movedThisStep = new HashSet<StringName>();
                    bool movedAny = false;
                    List<BattleUnitState> orderedUnits = SortWindPushUnitsNearToFar(
                        targetUnits,
                        direction
                    );
                    foreach (BattleUnitState targetUnit in orderedUnits)
                    {
                        if (targetUnit == null || !targetUnit.is_alive)
                        {
                            continue;
                        }
                    if (movedThisStep.Contains(targetUnit.unit_id))
                    {
                        continue;
                    }
                    if (
                        TryWindPushUnitOneStep(
                            source_unit,
                            skill_def,
                            effectDef,
                            targetUnit,
                            direction,
                            movedThisStep,
                            affectedUnitIds,
                            new HashSet<StringName>(),
                            batch
                        )
                    )
                    {
                        movedAny = true;
                        applied = true;
                    }
                }
                if (!movedAny)
                {
                    break;
                }
            }
        }
        return new BattleGroundWindPushResult(applied, KeysStringNameList(affectedUnitIds));
    }

    internal BattleGroundUnitEffectsResult _apply_ground_unit_effects_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        IReadOnlyList<CombatEffectDef> effect_defs,
        IReadOnlyList<Vector2I> effect_coords,
        BattleEventBatch batch,
        IReadOnlyList<Vector2I> target_coords
    )
    {
        BattleUnitState sourceUnit = source_unit;
        SkillDef skillDef = skill_def;
        bool applied = false;
        int totalDamage = 0;
        int totalHealing = 0;
        int totalKillCount = 0;
        var affectedUnitIds = new HashSet<StringName>();
        var shieldRollContext = new Dictionary<long, int>();
        BattleForcedMoveContext forcedMoveContext = BuildGroundForcedMoveContext(
            sourceUnit,
            target_coords
        );
        IReadOnlyList<CombatEffectDef> effectDefs = effect_defs ?? Array.Empty<CombatEffectDef>();
        IReadOnlyList<Vector2I> effectCoords = effect_coords ?? Array.Empty<Vector2I>();
        IReadOnlyList<CombatEffectDef> windPushEffects = CollectWindPushEffects(effectDefs);
        HashSet<ulong> windPushEffectIds = BuildEffectInstanceIdSet(windPushEffects);
        StringName sourceEventId = Runtime?.AllocateContingencySourceEventId("ground_spell") ?? Empty;
        var spellAffectedUnitIds = new List<StringName>();
        foreach (BattleUnitState affectedUnit in CollectUnitsInCoords(effectCoords))
            if (affectedUnit != null && affectedUnit.is_alive && !spellAffectedUnitIds.Contains(affectedUnit.unit_id))
                spellAffectedUnitIds.Add(affectedUnit.unit_id);
        Runtime?.EmitContingencySpellAffected(
            source_unit,
            null,
            spellAffectedUnitIds,
            sourceEventId,
            effectCoords
        );

        foreach (BattleUnitState targetUnit in CollectUnitsInCoords(effectCoords))
        {
            if (targetUnit == null || !targetUnit.is_alive)
            {
                continue;
            }
            var applicableEffects = new GCombatEffectArray();
            foreach (CombatEffectDef effectDef in effectDefs)
            {
                if (effectDef == null || windPushEffectIds.Contains(effectDef.GetInstanceId()))
                {
                    continue;
                }
                if (
                    _is_unit_valid_for_effect(
                        source_unit,
                        targetUnit,
                        _resolve_effect_target_filter(skill_def, effectDef)
                    )
                )
                {
                    applicableEffects.Add(effectDef);
                }
            }
            if (applicableEffects.Count == 0)
            {
                continue;
            }

            BattleLayeredBarrierService layeredBarrierService = LayeredBarrierService;
            BattleBarrierInteractionResult barrierResult =
                layeredBarrierService != null
                    ? layeredBarrierService.ResolveSkillBarrierInteractionResult(
                        source_unit,
                        targetUnit,
                        skill_def,
                        applicableEffects,
                        batch
                    )
                    : new BattleBarrierInteractionResult(false, false);
            if (barrierResult.Blocked)
            {
                applied = applied || barrierResult.Applied;
                if (barrierResult.Applied)
                {
                    AppendAffectedUnitId(affectedUnitIds, targetUnit);
                }
                continue;
            }

            int previousTargetHp = targetUnit.current_hp;
            GArray applicableEffectPayload = ToUntypedEffectArray(applicableEffects);
            IReadOnlyList<CombatEffectDef> applicableEffectDefs = ToCombatEffectDefList(
                applicableEffectPayload
            );
            GroundUnitEffectResolution effectResolution = _resolve_ground_unit_effect_resolution(
                source_unit,
                targetUnit,
                skill_def,
                applicableEffectDefs
            );
            AttackEffectResolutionResult damageResult = effectResolution.Result;
            Runtime?._skill_mastery_service?.RecordTargetResult(
                source_unit,
                targetUnit,
                skill_def,
                damageResult,
                applicableEffects
            );
            BattleShieldApplyResult shieldResult = ApplyUnitShieldEffectsResult(
                source_unit,
                targetUnit,
                skill_def,
                applicableEffectPayload,
                shieldRollContext
            );
            BattleSpecialSkillResult specialResult =
                Runtime.ApplyUnitSkillSpecialEffectsResult(
                    source_unit,
                    targetUnit,
                    skill_def,
                    null,
                    applicableEffects,
                    batch,
                    forcedMoveContext
                );
            RecordVajraBodyMasteryFromIncomingDamageTyped(
                source_unit,
                targetUnit,
                skill_def,
                damageResult,
                batch
            );
            MarkAppliedStatusesForTurnTiming(
                targetUnit,
                damageResult.StatusEffectIds
            );
            var appliedStatusIds = new List<StringName>();
            foreach (StringName statusId in damageResult.StatusEffectIds ?? new GStringNameArray())
                if (statusId != Empty && !appliedStatusIds.Contains(statusId))
                    appliedStatusIds.Add(statusId);
            foreach (StringName statusId in specialResult.StatusEffectIds ?? Array.Empty<StringName>())
                if (statusId != Empty && !appliedStatusIds.Contains(statusId))
                    appliedStatusIds.Add(statusId);
            Runtime?.EmitContingencyHpAndStatusHooks(
                source_unit,
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
            AppendAffectedUnitId(affectedUnitIds, targetUnit);
            _append_changed_unit_id(
                batch,
                sourceUnit != null ? sourceUnit.unit_id : Empty
            );
            _append_changed_unit_id(batch, targetUnit.unit_id);
            _append_changed_unit_coords(batch, targetUnit);
            append_result_source_status_effects(batch, source_unit, damageResult);

            int damage = damageResult.Damage;
            int healing = damageResult.Healing;
            totalDamage += damage;
            totalHealing += healing;
            append_damage_result_log_lines(
                batch,
                _build_skill_log_subject_label(source_unit, skill_def),
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
                    $"{_build_skill_log_subject_label(source_unit, skill_def)} 为 {DisplayName(targetUnit)} 恢复 {healing} 点生命。"
                );
            }
            if (shieldResult.Applied)
            {
                AppendLog(
                    batch,
                    $"{_build_skill_log_subject_label(source_unit, skill_def)} 使 {DisplayName(targetUnit)} 的护盾值变为 {shieldResult.CurrentShieldHp}。"
                );
            }
            foreach (StringName statusId in damageResult.StatusEffectIds)
            {
                AppendLog(batch, $"{DisplayName(targetUnit)} 获得状态 {statusId}。");
            }

            if (!targetUnit.is_alive)
            {
                totalKillCount += 1;
                _apply_on_kill_gain_resources_effects(
                    source_unit,
                    targetUnit,
                    skill_def,
                    ToUntypedEffectArray(effectDefs),
                    batch
                );
                Runtime.HandleUnitDefeatedByRuntimeEffect(
                    targetUnit,
                    sourceUnit,
                    batch,
                    $"{DisplayName(targetUnit)} 被击倒。",
                    new BattleDefeatHandlingOptions(recordEnemyDefeatedAchievement: true)
                );
            }
            if (source_unit != null && targetUnit != null)
            {
                _record_effect_metrics(
                    source_unit,
                    targetUnit,
                    damage,
                    healing,
                    targetUnit.is_alive ? 0 : 1
                );
                Runtime?._battle_rating_system?.RecordContributionFromUnits(
                    sourceUnit,
                    targetUnit,
                    damage,
                    healing,
                    !targetUnit.is_alive,
                    new StringName("skill"),
                    skillDef != null ? skillDef.skill_id : Empty
                );
            }
        }

        BattleGroundWindPushResult windPushResult = _apply_ground_wind_push_effects_result(
            source_unit,
            skill_def,
            windPushEffects,
            effect_coords,
            target_coords,
            batch
        );
        if (windPushResult.Applied)
        {
            applied = true;
            _append_changed_unit_id(
                batch,
                sourceUnit != null ? sourceUnit.unit_id : Empty
            );
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
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        IReadOnlyList<CombatEffectDef> effect_defs
    )
    {
        return _resolve_ground_unit_effect_resolution(
            source_unit,
            target_unit,
            skill_def,
            effect_defs
        ).Result;
    }

    private GroundUnitEffectResolution _resolve_ground_unit_effect_resolution(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        IReadOnlyList<CombatEffectDef> effect_defs
    )
    {
        SkillDef skillDef = skill_def;
        IReadOnlyList<CombatEffectDef> effectDefs = effect_defs ?? Array.Empty<CombatEffectDef>();
        if (ShouldResolveGroundEffectsAsAttack(effectDefs))
        {
            IReadOnlyList<CombatEffectDef> attackEffectDefs = DedupeEffectDefsByInstanceTyped(
                effectDefs
            );
            BattleRuntimeModule runtime = _runtime as BattleRuntimeModule;
            BattleAttackCheckPolicyService attackPolicy =
                runtime?.GetAttackCheckPolicyService();
            BattleDamageResolver damageResolver = runtime?.GetDamageResolver();
            BattleUnitState sourceUnit = source_unit as BattleUnitState;
            BattleUnitState targetUnit = target_unit as BattleUnitState;
            if (attackPolicy == null || damageResolver == null)
            {
                return GroundUnitEffectResolution.FromResult(
                    BattleDamageResolver.BuildEmptyResolutionResult(
                        skillDef != null ? skillDef.skill_id : Empty
                    )
                );
            }
            BattleAttackCheckPolicyContext attackContext = attackPolicy.BuildAttackContext(
                State,
                sourceUnit,
                targetUnit,
                skillDef,
                new StringName("skill_attack_check"),
                new StringName("execute"),
                false
            );
            AttackCheckInput attackCheck = attackPolicy.BuildAttackCheck(attackContext, 0, 0);
            return GroundUnitEffectResolution.FromResult(
                damageResolver.ResolveAttackEffects(
                    sourceUnit,
                    targetUnit,
                    ToUntypedEffectArray(attackEffectDefs),
                    attackCheck,
                    new AttackContext
                    {
                        BattleState = State,
                        SkillId = skillDef != null ? skillDef.skill_id : Empty,
                    }
                )
            );
        }
        StringName skillId = skillDef != null ? skillDef.skill_id : Empty;
        return GroundUnitEffectResolution.FromResult(
            Runtime.GetDamageResolver()
                .ResolveEffects(
                    source_unit,
                    target_unit,
                    ToUntypedEffectArray(effectDefs),
                    MarkRuntimeDictionary(
                        new GDictionary { ["skill_id"] = skillId },
                        $"ground_effect:{skillId}:damage_context"
                    )
                )
        );
    }

    internal static bool ShouldResolveGroundEffectsAsAttack(
        IReadOnlyList<CombatEffectDef> effectDefs
    )
    {
        foreach (CombatEffectDef effectDef in effectDefs ?? Array.Empty<CombatEffectDef>())
        {
            if (GroundEffectRuntimeParameters.FromEffect(effectDef).ResolveAsWeaponAttack)
            {
                return true;
            }
        }
        return false;
    }

    internal IReadOnlyList<CombatEffectDef> DedupeEffectDefsByInstanceTyped(
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        return DedupeEffectDefsByInstanceCore(effectDefs);
    }

    private static IReadOnlyList<CombatEffectDef> DedupeEffectDefsByInstanceCore(
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        var deduped = new List<CombatEffectDef>();
        var seen = new HashSet<ulong>();
        foreach (CombatEffectDef effectDef in effectDefs ?? Array.Empty<CombatEffectDef>())
        {
            if (effectDef != null && seen.Add(effectDef.GetInstanceId()))
            {
                deduped.Add(effectDef);
            }
        }
        return deduped;
    }

    internal BattleGroundTerrainEffectsResult _apply_ground_terrain_effects_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        IReadOnlyList<CombatEffectDef> effect_defs,
        IReadOnlyList<Vector2I> effect_coords,
        BattleEventBatch batch
    )
    {
        bool applied = false;
        bool requiresTopologyReconcile = false;
        IReadOnlyList<CombatEffectDef> effectDefs = effect_defs ?? Array.Empty<CombatEffectDef>();
        IReadOnlyList<Vector2I> effectCoords = effect_coords ?? Array.Empty<Vector2I>();
        BattleLayeredBarrierService layeredBarrierService = LayeredBarrierService;
        GCombatEffectArray barrierEffectDefs = ToCombatEffectDefArray(effectDefs);
        foreach (CombatEffectDef effectDef in effectDefs)
        {
            if (effectDef == null)
            {
                continue;
            }
            CombatEffectDef combatEffectDef = effectDef;
            BattleEffectKind effectKind = combatEffectDef?.EffectKind ?? BattleEffectKind.Unknown;
            if (IsGroundCellTopologyEffect(effectKind))
            {
                requiresTopologyReconcile = true;
                foreach (Vector2I effectCoord in effectCoords)
                {
                    BattleBarrierInteractionResult barrierResult =
                        layeredBarrierService != null
                            ? layeredBarrierService.ResolveGroundBarrierInteractionResult(
                                source_unit,
                                effectCoord,
                                skill_def,
                                barrierEffectDefs,
                                batch
                            )
                            : new BattleBarrierInteractionResult(false, false);
                    if (barrierResult.Blocked)
                    {
                        applied = applied || barrierResult.Applied;
                        continue;
                    }
                    if (
                        _apply_ground_cell_effect(
                            source_unit,
                            skill_def,
                            effectCoord,
                            effectDef,
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
                    combatEffectDef != null
                    && combatEffectDef.duration_tu > 0
                    && combatEffectDef.tick_interval_tu > 0
                )
                {
                    StringName fieldInstanceId = _build_terrain_effect_instance_id(
                        combatEffectDef.terrain_effect_id
                    );
                    int appliedCoordCount = 0;
                    foreach (Vector2I effectCoord in effectCoords)
                    {
                        BattleBarrierInteractionResult barrierResult =
                            layeredBarrierService != null
                                ? layeredBarrierService.ResolveGroundBarrierInteractionResult(
                                    source_unit,
                                    effectCoord,
                                    skill_def,
                                    barrierEffectDefs,
                                    batch
                                )
                                : new BattleBarrierInteractionResult(false, false);
                        if (barrierResult.Blocked)
                        {
                            applied = applied || barrierResult.Applied;
                            continue;
                        }
                        if (
                            Runtime._terrain_effect_system.UpsertTimedTerrainEffect(
                                effectCoord,
                                source_unit,
                                skill_def,
                                effectDef,
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
                            $"{_build_skill_log_subject_label(source_unit, skill_def)} 在 {appliedCoordCount} 个地格留下 {_get_terrain_effect_display_name(effectDef)}。"
                        );
                    }
                }
                else if (combatEffectDef != null && !IsEmpty(combatEffectDef.terrain_effect_id))
                {
                    int taggedCoordCount = 0;
                    foreach (Vector2I effectCoord in effectCoords)
                    {
                        BattleBarrierInteractionResult barrierResult =
                            layeredBarrierService != null
                                ? layeredBarrierService.ResolveGroundBarrierInteractionResult(
                                    source_unit,
                                    effectCoord,
                                    skill_def,
                                    barrierEffectDefs,
                                    batch
                                )
                                : new BattleBarrierInteractionResult(false, false);
                        if (barrierResult.Blocked)
                        {
                            applied = applied || barrierResult.Applied;
                            continue;
                        }
                        BattleCellState cell = GridService.GetCellState(State, effectCoord);
                        if (cell == null)
                        {
                            continue;
                        }
                        List<StringName> terrainEffectIds = cell.terrain_effect_ids;
                        StringName terrainEffectId = combatEffectDef.terrain_effect_id;
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
                            $"{_build_skill_log_subject_label(source_unit, skill_def)} 使 {taggedCoordCount} 个地格附加效果 {_get_terrain_effect_display_name(effectDef)}。"
                        );
                    }
                }
            }
            else if (effectKind == BattleEffectKind.EdgeClear)
            {
                if (
                    _apply_ground_edge_clear_effect(
                        source_unit,
                        skill_def,
                        effectCoords,
                        effectDef,
                        batch
                    )
                )
                {
                    applied = true;
                }
            }
        }
        if (requiresTopologyReconcile && ReconcileWaterTopology(effectCoords, batch))
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
        BattleUnitState source_unit,
        SkillDef skill_def,
        IReadOnlyList<Vector2I> effect_coords,
        CombatEffectDef effect_def,
        BattleEventBatch batch
    )
    {
        if (_runtime == null || State == null || effect_coords == null || effect_coords.Count < 2)
        {
            return false;
        }
        IReadOnlyList<Vector2I> edgeCoords = SortCoordsTyped(effect_coords);
        Vector2I first = edgeCoords[0];
        Vector2I second = edgeCoords[1];
        if (GridService.GetDistance(first, second) != 1)
        {
            return false;
        }
        var barrierEffectDefs = new GCombatEffectArray { effect_def };
        BattleLayeredBarrierService layeredBarrierService = LayeredBarrierService;
        foreach (Vector2I barrierCoord in new[] { first, second })
        {
            BattleBarrierInteractionResult barrierResult =
                layeredBarrierService != null
                    ? layeredBarrierService.ResolveGroundBarrierInteractionResult(
                        source_unit,
                        barrierCoord,
                        skill_def,
                        barrierEffectDefs,
                        batch
                    )
                    : new BattleBarrierInteractionResult(false, false);
            if (barrierResult.Blocked)
            {
                return barrierResult.Applied;
            }
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
        if (!CanEdgeClearRemoveFeature(effect_def, featureState))
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
            $"{_build_skill_log_subject_label(source_unit, skill_def)} 在 ({first.X}, {first.Y}) 与 ({second.X}, {second.Y}) 之间开辟通道，移除了{_get_edge_feature_display_name(featureState)}。"
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
        CombatEffectDef effect_def,
        BattleEdgeFeatureState feature_state
    )
    {
        return BuildEdgeClearFeatureKindSet(effect_def)
            .Contains(feature_state?.feature_kind ?? Empty);
    }

    private HashSet<StringName> BuildEdgeClearFeatureKindSet(CombatEffectDef effect_def)
    {
        var allowed = new HashSet<StringName>();
        GDictionary parameters = effect_def?.@params ?? new GDictionary();
        GArray rawKinds = ReadArray(parameters, "clear_feature_kinds");
        if (rawKinds.Count > 0)
        {
            foreach (var rawKind in rawKinds)
            {
                StringName kind = ToStringName(rawKind);
                if (!IsEmpty(kind))
                {
                    allowed.Add(kind);
                }
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
        BattleUnitState source_unit,
        SkillDef skill_def,
        Vector2I target_coord,
        CombatEffectDef effect_def,
        BattleEventBatch batch
    )
    {
        BattleState state = State;
        CombatEffectDef effectDef = effect_def;
        BattleCellState cell = GridService.GetCellState(state, target_coord);
        if (cell == null || effectDef == null)
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
        BattleEffectKind effectKind = effectDef.EffectKind;
        if (
            effectKind == BattleEffectKind.Terrain
            || effectKind == BattleEffectKind.TerrainReplace
            || effectKind == BattleEffectKind.TerrainReplaceTo
        )
        {
            StringName terrainReplaceTo = effectDef.terrain_replace_to;
            if (
                !IsEmpty(terrainReplaceTo)
                && cell.base_terrain != terrainReplaceTo
            )
            {
                if (
                    GridService.SetBaseTerrain(state, target_coord, terrainReplaceTo)
                )
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
            && effectDef.height_delta != 0
        )
        {
            BattleHeightDeltaResult heightResult = GridService.ApplyHeightDeltaResult(
                state,
                target_coord,
                effectDef.height_delta
            );
            if (heightResult.Changed)
            {
                cellApplied = true;
            }
        }

        int afterHeight = cell.current_height;
        if (
            beforeTerrain != cell.base_terrain
            || beforeHeight != afterHeight
        )
        {
            _append_changed_coord(batch, target_coord);
        }
        if (beforeTerrain != cell.base_terrain)
        {
            AppendLog(
                batch,
                $"{_build_skill_log_subject_label(source_unit, skill_def)} 使 ({target_coord.X}, {target_coord.Y}) 的地形由 {GridService.GetTerrainDisplayName(beforeTerrain.ToString())} 变为 {GridService.GetTerrainDisplayName(cell.base_terrain.ToString())}。"
            );
        }
        if (beforeHeight != afterHeight)
        {
            AppendLog(
                batch,
                $"{_build_skill_log_subject_label(source_unit, skill_def)} 使 ({target_coord.X}, {target_coord.Y}) 的高度由 {beforeHeight} 变为 {afterHeight}。"
            );
        }

        BattleUnitState occupantUnitState = occupantUnit;
        if (occupantUnitState != null && occupantUnitState.is_alive && afterHeight < beforeHeight)
        {
            int fallLayers = beforeHeight - afterHeight;
            AttackEffectResolutionResult fallDamageResult =
                Runtime.GetDamageResolver().ResolveFallDamageResult(
                    occupantUnitState,
                    fallLayers
                );
            int fallDamage = fallDamageResult.Damage;
            int shieldAbsorbed = fallDamageResult.ShieldAbsorbed;
            if (fallDamage > 0 || shieldAbsorbed > 0)
            {
                cellApplied = true;
                _append_changed_coord(batch, target_coord);
                _append_changed_unit_id(batch, occupantUnitState.unit_id);
                if (fallDamage > 0)
                {
                    AppendLog(
                        batch,
                        $"{_build_skill_log_subject_label(source_unit, skill_def)} 使 ({target_coord.X}, {target_coord.Y}) 的高度下降 {fallLayers} 层，导致 {DisplayName(occupantUnit)} 坠落并受到 {fallDamage} 点伤害。"
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
                        $"{_build_skill_log_subject_label(source_unit, skill_def)} 使 ({target_coord.X}, {target_coord.Y}) 的高度下降 {fallLayers} 层，导致 {DisplayName(occupantUnit)} 坠落，但被护盾吸收了 {shieldAbsorbed} 点坠落伤害。"
                    );
                }
                if (fallDamageResult.ShieldBroken)
                {
                    AppendLog(batch, $"{DisplayName(occupantUnit)} 的护盾被击碎。");
                }
                if (!occupantUnitState.is_alive)
                {
                    Runtime.HandleUnitDefeatedByRuntimeEffect(
                        occupantUnitState,
                        source_unit,
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

    internal string GetGroundSpecialEffectValidationMessage(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        IReadOnlyList<Vector2I> target_coords
    )
    {
        CombatEffectDef relocationEffectDef = _get_ground_relocation_effect_def(
            skill_def,
            cast_variant
        );
        if (relocationEffectDef == null)
        {
            return "";
        }
        if (active_unit == null || State == null)
        {
            return "位移落点无效。";
        }
        if (_is_movement_blocked(active_unit))
        {
            return "当前状态下无法移动。";
        }
        if (target_coords == null || target_coords.Count == 0)
        {
            return "位移落点无效。";
        }
        return _can_use_ground_relocation(
            active_unit,
            target_coords[0],
            relocationEffectDef
        )
            ? ""
            : "目标地格无法作为位移落点。";
    }

    internal string GetGroundSpecialEffectValidationMessage(
        BattleUnitReadView active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        IReadOnlyList<Vector2I> target_coords
    )
    {
        CombatEffectDef relocationEffectDef = _get_ground_relocation_effect_def(
            skill_def,
            cast_variant
        );
        if (relocationEffectDef == null)
        {
            return "";
        }
        if (!active_unit.IsValid || State == null)
        {
            return "位移落点无效。";
        }
        if (_is_movement_blocked(active_unit))
        {
            return "当前状态下无法移动。";
        }
        if (target_coords == null || target_coords.Count == 0)
        {
            return "位移落点无效。";
        }
        return _can_use_ground_relocation(
            active_unit,
            target_coords[0],
            relocationEffectDef
        )
            ? ""
            : "目标地格无法作为位移落点。";
    }

    internal BattleGroundSkillValidationResult _validate_ground_skill_command_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleCommand command
    )
    {
        var normalizedCoords = _normalize_target_coords(command);
        BattleGroundSkillValidationResult deniedResult =
            BattleGroundSkillValidationResult.Denied(
                "地面技能目标无效。",
                ToVector2IList(normalizedCoords)
            );
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (
            State == null
            || active_unit == null
            || skill_def == null
            || combatProfile == null
            || cast_variant == null
        )
        {
            return deniedResult;
        }
        if (cast_variant.TargetModeKind != BattleTargetMode.Ground)
        {
            return deniedResult with { Message = "该技能形态不是地面施法。" };
        }
        BattleSkillCastBlockReasonKind blockReason = _get_skill_cast_block_reason(
            active_unit,
            skill_def
        );
        if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
        {
            return deniedResult with
            {
                Message =
                    Runtime?._get_skill_cast_block_message(active_unit, skill_def)
                    ?? "正式技能检查未绑定，无法施放该技能。",
            };
        }
        if (normalizedCoords.Count != cast_variant.required_coord_count)
        {
            return deniedResult
                with
                {
                    Message = $"该技能形态需要选择 {cast_variant.required_coord_count} 个地格。",
                };
        }
        BattleChargeResolver chargeResolver = Runtime?._charge_resolver;
        if (chargeResolver != null && chargeResolver.IsChargeOption(cast_variant))
        {
            return chargeResolver.ValidateChargeCommandResult(
                active_unit,
                skill_def,
                cast_variant,
                new Godot.Collections.Array<Vector2I>(normalizedCoords),
                deniedResult
            );
        }

        CombatEffectDef relocationEffectDef = _get_ground_relocation_effect_def(
            skill_def,
            cast_variant
        );
        int effectiveSkillRange = _get_effective_skill_range(active_unit, skill_def);
        var seenCoords = new HashSet<Vector2I>();
        foreach (var rawCoord in normalizedCoords)
        {
            Vector2I coord = rawCoord;
            if (!seenCoords.Add(coord))
            {
                return deniedResult with { Message = "同一地格不能重复选择。" };
            }
            if (!GridService.IsInside(State, coord))
            {
                return deniedResult with { Message = "存在超出战场范围的目标地格。" };
            }
            int targetDistance =
                relocationEffectDef != null
                    ? GridService.GetChebyshevDistance(
                        active_unit.coord,
                        coord
                    )
                    : GridService.GetDistanceFromUnitToCoord(
                        active_unit,
                        coord
                    );
            if (targetDistance > effectiveSkillRange)
            {
                return deniedResult with { Message = "目标地格超出技能施放距离。" };
            }
            if (!GridService.HasCell(State, coord))
            {
                return deniedResult with { Message = "目标地格数据不可用。" };
            }
            if (cast_variant.allowed_base_terrains.Count > 0)
            {
                bool normalizedAllowed = false;
                StringName normalizedCellTerrain = BattleTerrainRules.NormalizeTerrainId(
                    GridService.GetCellBaseTerrainId(State, coord)
                );
                foreach (StringName rawAllowedTerrain in cast_variant.allowed_base_terrains)
                {
                    if (
                        BattleTerrainRules.NormalizeTerrainId(rawAllowedTerrain)
                        == normalizedCellTerrain
                    )
                    {
                        normalizedAllowed = true;
                        break;
                    }
                }
                if (!normalizedAllowed)
                {
                    return deniedResult with { Message = "目标地格地形不符合该技能形态的要求。" };
                }
            }
            if (_is_crown_break_skill(skill_def.skill_id))
            {
                BattleUnitState targetUnit = GridService.GetUnitAtCoord(State, coord);
                if (!_is_crown_break_target_eligible(active_unit, targetUnit))
                {
                    return deniedResult
                        with
                        {
                            Message = "折冠只能对已被黑星烙印的 elite / boss 施放。",
                        };
                }
            }
        }
        if (
            !_validate_target_coords_shape(
                cast_variant.FootprintPatternKind,
                normalizedCoords
            )
        )
        {
            return deniedResult with { Message = "目标地格排布不符合该技能形态。" };
        }
        IReadOnlyList<Vector2I> sortedTargetCoords = SortCoordsTyped(normalizedCoords);
        string groundExecuteMessage = GetGroundExecuteValidationMessage(
            skill_def,
            cast_variant,
            active_unit
        );
        if (!string.IsNullOrEmpty(groundExecuteMessage))
        {
            return deniedResult with { Message = groundExecuteMessage };
        }
        string specialValidationMessage = GetGroundSpecialEffectValidationMessage(
            active_unit,
            skill_def,
            cast_variant,
            sortedTargetCoords
        );
        if (!string.IsNullOrEmpty(specialValidationMessage))
        {
            return deniedResult with { Message = specialValidationMessage };
        }
        return BattleGroundSkillValidationResult.AllowedResult(
            "可施放。",
            new List<Vector2I>(sortedTargetCoords)
        );
    }

    internal BattleGroundSkillValidationResult _validate_ground_skill_command_result(
        BattleUnitReadView active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleCommand command
    )
    {
        var normalizedCoords = _normalize_target_coords(command);
        BattleGroundSkillValidationResult deniedResult =
            BattleGroundSkillValidationResult.Denied(
                "地面技能目标无效。",
                ToVector2IList(normalizedCoords)
            );
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (
            State == null
            || !active_unit.IsValid
            || skill_def == null
            || combatProfile == null
            || cast_variant == null
        )
        {
            return deniedResult;
        }
        if (cast_variant.TargetModeKind != BattleTargetMode.Ground)
        {
            return deniedResult with { Message = "该技能形态不是地面施法。" };
        }
        string blockReason = Runtime?._get_skill_command_block_reason(
            active_unit,
            skill_def,
            cast_variant
        ) ?? "正式技能检查未绑定，无法施放该技能。";
        if (!string.IsNullOrEmpty(blockReason))
        {
            return deniedResult with { Message = blockReason };
        }
        if (normalizedCoords.Count != cast_variant.required_coord_count)
        {
            return deniedResult
                with
                {
                    Message = $"该技能形态需要选择 {cast_variant.required_coord_count} 个地格。",
                };
        }
        BattleChargeResolver chargeResolver = Runtime?._charge_resolver;
        if (chargeResolver != null && chargeResolver.IsChargeOption(cast_variant))
        {
            return chargeResolver.ValidateChargeCommandResult(
                active_unit,
                skill_def,
                cast_variant,
                normalizedCoords,
                deniedResult
            );
        }

        CombatEffectDef relocationEffectDef = _get_ground_relocation_effect_def(
            skill_def,
            cast_variant
        );
        int effectiveSkillRange = _get_effective_skill_range(active_unit, skill_def);
        var seenCoords = new HashSet<Vector2I>();
        foreach (var rawCoord in normalizedCoords)
        {
            Vector2I coord = rawCoord;
            if (!seenCoords.Add(coord))
            {
                return deniedResult with { Message = "同一地格不能重复选择。" };
            }
            if (!GridService.IsInside(State, coord))
            {
                return deniedResult with { Message = "存在超出战场范围的目标地格。" };
            }
            int targetDistance =
                relocationEffectDef != null
                    ? GridService.GetChebyshevDistance(
                        active_unit.Coord,
                        coord
                    )
                    : GridService.GetDistanceFromUnitToCoord(
                        active_unit,
                        coord
                    );
            if (targetDistance > effectiveSkillRange)
            {
                return deniedResult with { Message = "目标地格超出技能施放距离。" };
            }
            if (!GridService.HasCell(State, coord))
            {
                return deniedResult with { Message = "目标地格数据不可用。" };
            }
            if (cast_variant.allowed_base_terrains.Count > 0)
            {
                bool normalizedAllowed = false;
                StringName normalizedCellTerrain = BattleTerrainRules.NormalizeTerrainId(
                    GridService.GetCellBaseTerrainId(State, coord)
                );
                foreach (StringName rawAllowedTerrain in cast_variant.allowed_base_terrains)
                {
                    if (
                        BattleTerrainRules.NormalizeTerrainId(rawAllowedTerrain)
                        == normalizedCellTerrain
                    )
                    {
                        normalizedAllowed = true;
                        break;
                    }
                }
                if (!normalizedAllowed)
                {
                    return deniedResult with { Message = "目标地格地形不符合该技能形态的要求。" };
                }
            }
            if (_is_crown_break_skill(skill_def.skill_id))
            {
                BattleUnitState targetUnit = GridService.GetUnitAtCoord(State, coord);
                if (!_is_crown_break_target_eligible(active_unit, new BattleUnitReadView(targetUnit)))
                {
                    return deniedResult
                        with
                        {
                            Message = "折冠只能对已被黑星烙印的 elite / boss 施放。",
                        };
                }
            }
        }
        if (
            !_validate_target_coords_shape(
                cast_variant.FootprintPatternKind,
                normalizedCoords
            )
        )
        {
            return deniedResult with { Message = "目标地格排布不符合该技能形态。" };
        }
        IReadOnlyList<Vector2I> sortedTargetCoords = SortCoordsTyped(normalizedCoords);
        string groundExecuteMessage = GetGroundExecuteValidationMessage(
            skill_def,
            cast_variant,
            active_unit
        );
        if (!string.IsNullOrEmpty(groundExecuteMessage))
        {
            return deniedResult with { Message = groundExecuteMessage };
        }
        string specialValidationMessage = GetGroundSpecialEffectValidationMessage(
            active_unit,
            skill_def,
            cast_variant,
            sortedTargetCoords
        );
        if (!string.IsNullOrEmpty(specialValidationMessage))
        {
            return deniedResult with { Message = specialValidationMessage };
        }
        return BattleGroundSkillValidationResult.AllowedResult(
            "可施放。",
            new List<Vector2I>(sortedTargetCoords)
        );
    }

    private string GetGroundExecuteValidationMessage(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitState activeUnit
    )
    {
        foreach (CombatEffectDef effectDef in CollectGroundUnitEffectDefs(
            skillDef,
            castVariant,
            activeUnit
        ))
        {
            if (effectDef?.EffectKind == BattleEffectKind.Execute)
            {
                return "地面技能不能携带律令死亡。";
            }
        }
        return "";
    }

    private string GetGroundExecuteValidationMessage(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitReadView activeUnit
    )
    {
        foreach (CombatEffectDef effectDef in CollectGroundUnitEffectDefs(
            skillDef,
            castVariant,
            activeUnit
        ))
        {
            if (effectDef?.EffectKind == BattleEffectKind.Execute)
            {
                return "地面技能不能携带律令死亡。";
            }
        }
        return "";
    }

    internal bool _validate_target_coords_shape(
        CombatCastFootprintPattern footprint_pattern,
        Godot.Collections.Array<Vector2I> target_coords
    )
    {
        if (footprint_pattern == CombatCastFootprintPattern.Single)
        {
            return target_coords != null && target_coords.Count == 1;
        }
        if (footprint_pattern == CombatCastFootprintPattern.Line2)
        {
            if (target_coords == null || target_coords.Count != 2)
            {
                return false;
            }
            Vector2I first = target_coords[0];
            Vector2I second = target_coords[1];
            return (first.X == second.X && Math.Abs(first.Y - second.Y) == 1)
                || (first.Y == second.Y && Math.Abs(first.X - second.X) == 1);
        }
        if (footprint_pattern == CombatCastFootprintPattern.Square2)
        {
            if (target_coords == null || target_coords.Count != 4)
            {
                return false;
            }
            Vector2I firstCoord = target_coords[0];
            int minX = firstCoord.X;
            int maxX = firstCoord.X;
            int minY = firstCoord.Y;
            int maxY = firstCoord.Y;
            var coordSet = new HashSet<Vector2I>();
            foreach (Vector2I coord in target_coords)
            {
                minX = Math.Min(minX, coord.X);
                maxX = Math.Max(maxX, coord.X);
                minY = Math.Min(minY, coord.Y);
                maxY = Math.Max(maxY, coord.Y);
                coordSet.Add(coord);
            }
            if (maxX - minX != 1 || maxY - minY != 1)
            {
                return false;
            }
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!coordSet.Contains(new Vector2I(x, y)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        if (footprint_pattern == CombatCastFootprintPattern.Unordered)
        {
            return target_coords != null && target_coords.Count > 0;
        }
        return false;
    }

    internal Godot.Collections.Array<Vector2I> _normalize_target_coords(BattleCommand command)
    {
        var coords = new Godot.Collections.Array<Vector2I>();
        if (command == null)
        {
            return coords;
        }
        foreach (Vector2I targetCoord in command.TargetCoordsTyped)
        {
            coords.Add(targetCoord);
        }
        if (coords.Count == 0 && command.target_coord != new Vector2I(-1, -1))
        {
            coords.Add(command.target_coord);
        }
        return coords;
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

    internal string _get_terrain_effect_display_name(CombatEffectDef effect_def)
    {
        if (effect_def != null && !string.IsNullOrEmpty(effect_def.display_name))
        {
            return effect_def.display_name;
        }
        return effect_def != null
            ? effect_def.terrain_effect_id.ToString()
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

    private static string ReadString(GDictionary source, string key, string fallback = "")
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = source[key];
        string result = value.ToString();
        return string.IsNullOrEmpty(result) || result == "<null>" ? fallback : result;
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

    private static Godot.Collections.Array<CombatEffectDef> ToCombatEffectDefArray(GArray values)
    {
        var typedValues = new Godot.Collections.Array<CombatEffectDef>();
        if (values == null)
        {
            return typedValues;
        }
        foreach (var rawValue in values)
        {
            var effectDef = rawValue.As<CombatEffectDef>();
            if (effectDef != null)
            {
                typedValues.Add(effectDef);
            }
        }
        return typedValues;
    }

    private static Godot.Collections.Array<CombatEffectDef> ToCombatEffectDefArray(
        IEnumerable<CombatEffectDef> values
    )
    {
        var typedValues = new Godot.Collections.Array<CombatEffectDef>();
        if (values == null)
        {
            return typedValues;
        }
        foreach (CombatEffectDef effectDef in values)
        {
            if (effectDef != null)
            {
                typedValues.Add(effectDef);
            }
        }
        return typedValues;
    }

    private static List<CombatEffectDef> ToCombatEffectDefList(GArray values)
    {
        var typedValues = new List<CombatEffectDef>();
        if (values == null)
        {
            return typedValues;
        }
        foreach (var rawValue in values)
        {
            CombatEffectDef effectDef = rawValue.As<CombatEffectDef>();
            if (effectDef != null)
            {
                typedValues.Add(effectDef);
            }
        }
        return typedValues;
    }

    private GArray ToUntypedEffectArray(IEnumerable<CombatEffectDef> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return MarkRuntimeArray(result, "ground_effect.empty_effects");
        }
        foreach (CombatEffectDef effectDef in values)
        {
            if (effectDef != null)
            {
                result.Add(effectDef);
            }
        }
        return MarkRuntimeArray(result, "ground_effect.effects");
    }

    private static GArray MarkRuntimeArray(GArray array, string reason)
    {
        GArray result = array ?? new GArray();
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }

    private static GDictionary MarkRuntimeDictionary(GDictionary dictionary, string reason)
    {
        GDictionary result = dictionary ?? new GDictionary();
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }

    private static GArray ToUntypedVector2IArray(Godot.Collections.Array<Vector2I> values)
    {
        return ToUntypedVector2IArray(values as IEnumerable<Vector2I>);
    }

    private static GArray ToUntypedVector2IArray(IEnumerable<Vector2I> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (Vector2I coord in values)
        {
            result.Add(coord);
        }
        return result;
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

    private static List<Vector2I> ToVector2IList(IEnumerable<Vector2I> values)
    {
        var result = new List<Vector2I>();
        if (values == null)
        {
            return result;
        }
        foreach (Vector2I coord in values)
        {
            result.Add(coord);
        }
        return result;
    }

    private static List<Vector2I> ToVector2IList(GArray values)
    {
        var result = new List<Vector2I>();
        foreach (var value in values ?? new GArray())
        {
            result.Add(value.AsVector2I());
        }
        return result;
    }

    private static IReadOnlyList<Vector2I> SortCoordsTyped(IEnumerable<Vector2I> values)
    {
        var result = new List<Vector2I>(values ?? System.Array.Empty<Vector2I>());
        result.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        return result;
    }

    private static GArray ToUntypedBattleUnitArray(IEnumerable<BattleUnitState> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (BattleUnitState unitState in values)
        {
            if (unitState != null)
            {
                result.Add(unitState.ToDictionary());
            }
        }
        return result;
    }

    private static StringName ToStringName(object rawValue) =>
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

    private static List<StringName> KeysStringNameList(GDictionary dictionary)
    {
        var keys = new List<StringName>();
        foreach (var key in dictionary.Keys)
        {
            keys.Add(ToStringName(key));
        }
        return keys;
    }

    private static List<StringName> KeysStringNameList(HashSet<StringName> values)
    {
        return values != null ? new List<StringName>(values) : new List<StringName>();
    }

    private static void AppendLog(BattleEventBatch batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
        {
            return;
        }
        batch.AddLogLine(line);
    }

    private static string DisplayName(object value)
    {
        return value switch
        {
            BattleUnitState unitState => unitState.display_name,
            SkillDef skillDef => skillDef.display_name,
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
