using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleGroundEffectService : RefCounted
{
    private static readonly StringName Empty = "";
    private static readonly StringName WindPushMode = "wind_push";
    private static readonly StringName ForcedMoveEffect = "forced_move";
    private static readonly StringName JumpMode = "jump";
    private static readonly StringName BlinkMode = "blink";
    private static readonly StringName GroundTargetMode = "ground";
    private static readonly StringName EffectTerrain = "terrain";
    private static readonly StringName EffectTerrainReplace = "terrain_replace";
    private static readonly StringName EffectTerrainReplaceTo = "terrain_replace_to";
    private static readonly StringName EffectHeight = "height";
    private static readonly StringName EffectHeightDelta = "height_delta";
    private static readonly StringName EffectTerrainEffect = "terrain_effect";
    private static readonly StringName EffectEdgeClear = "edge_clear";
    private static readonly StringName FootprintSingle = "single";
    private static readonly StringName FootprintLine2 = "line2";
    private static readonly StringName FootprintSquare2 = "square2";
    private static readonly StringName FootprintUnordered = "unordered";
    private static readonly StringName FeatureWall = "wall";
    private static readonly StringName FeatureDoor = "door";
    private static readonly StringName FeatureGate = "gate";

    private readonly record struct GroundEffectRuntimeParameters(bool ResolveAsWeaponAttack)
    {
        public static GroundEffectRuntimeParameters FromEffect(CombatEffectDef effectDef)
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

    public void setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    public void dispose()
    {
        _runtime = null;
    }

    public void _append_result_report_entry(BattleEventBatch batch, GDictionary result)
    {
        Runtime?._append_result_report_entry(batch, result);
    }

    public void mark_applied_statuses_for_turn_timing(
        BattleUnitState target_unit,
        GArray status_effect_ids
    )
    {
        Runtime?.mark_applied_statuses_for_turn_timing(
            target_unit,
            status_effect_ids ?? new GArray()
        );
    }

    public void append_result_source_status_effects(
        BattleEventBatch batch,
        BattleUnitState source_unit,
        GDictionary result
    )
    {
        Runtime?.append_result_source_status_effects(
            batch,
            source_unit,
            result
        );
    }

    internal void append_result_source_status_effects(
        BattleEventBatch batch,
        BattleUnitState source_unit,
        AttackEffectResolutionResult result
    )
    {
        Runtime?.append_result_source_status_effects(
            batch,
            source_unit,
            result
        );
    }

    public void _record_effect_metrics(
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

    public void _record_unit_defeated(BattleUnitState unit_state)
    {
        Runtime?._record_unit_defeated(unit_state);
    }

    public void append_damage_result_log_lines(
        BattleEventBatch batch,
        string subject_label,
        string target_display_name,
        GDictionary result
    )
    {
        Runtime?.append_damage_result_log_lines(
            batch,
            subject_label,
            target_display_name,
            result
        );
    }

    internal void append_damage_result_log_lines(
        BattleEventBatch batch,
        string subject_label,
        string target_display_name,
        AttackEffectResolutionResult result
    )
    {
        Runtime?.append_damage_result_log_lines(
            batch,
            subject_label,
            target_display_name,
            result
        );
    }

    public string _build_skill_log_subject_label(
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

    public void _apply_on_kill_gain_resources_effects(
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

    public bool _is_crown_break_target_eligible(BattleUnitState active_unit, BattleUnitState target_unit)
    {
        return _runtime != null
            && Runtime._is_crown_break_target_eligible(
                active_unit,
                target_unit
            );
    }

    public bool _is_crown_break_skill(StringName skill_id)
    {
        return _runtime != null && Runtime._is_crown_break_skill(skill_id);
    }

    public void _record_vajra_body_mastery_from_incoming_damage(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GDictionary result,
        BattleEventBatch batch = null
    )
    {
        Runtime?._record_vajra_body_mastery_from_incoming_damage(
            source_unit,
            target_unit,
            skill_def,
            result,
            batch
        );
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

    public GArray _collect_units_in_coords(GArray effect_coords)
    {
        return _runtime == null
            ? new GArray()
            : ToUntypedBattleUnitArray(
                Runtime._collect_units_in_coords(new Godot.Collections.Array<Vector2I>(effect_coords))
            );
    }

    public GDictionary _apply_unit_shield_effects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GArray effect_defs,
        GDictionary shield_roll_context = null
    )
    {
        Dictionary<long, int> rollContext = BattleShieldService.ReadRollContext(
            shield_roll_context
        );
        BattleShieldApplyResult result = _apply_unit_shield_effects_result(
            source_unit,
            target_unit,
            skill_def,
            effect_defs,
            rollContext
        );
        BattleShieldService.WriteRollContext(shield_roll_context, rollContext);
        return result.ToDictionary();
    }

    public BattleShieldApplyResult _apply_unit_shield_effects_result(
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

    public StringName _resolve_effect_target_filter(SkillDef skill_def, CombatEffectDef effect_def)
    {
        return _runtime == null
            ? Empty
            : ToStringName(
                Runtime._resolve_effect_target_filter(skill_def, effect_def)
            );
    }

    public bool _is_unit_valid_for_effect(
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

    public void _flush_last_stand_mastery_records(BattleEventBatch batch)
    {
        Runtime?._flush_last_stand_mastery_records(batch);
    }

    public void _append_changed_coord(BattleEventBatch batch, Vector2I coord)
    {
        Runtime?._append_changed_coord(batch, coord);
    }

    public void _append_changed_coords(BattleEventBatch batch, GArray coords)
    {
        Runtime?._append_changed_coords(batch, coords);
    }

    public void _append_changed_unit_id(BattleEventBatch batch, StringName unit_id)
    {
        Runtime?._append_changed_unit_id(batch, unit_id);
    }

    public void _append_changed_unit_coords(BattleEventBatch batch, BattleUnitState unit_state)
    {
        Runtime?._append_changed_unit_coords(batch, unit_state);
    }

    public void _collect_defeated_unit_loot(BattleUnitState unit_state, BattleUnitState killer_unit = null)
    {
        Runtime?._collect_defeated_unit_loot(unit_state, killer_unit);
    }

    public void _clear_defeated_unit(BattleUnitState unit_state, BattleEventBatch batch = null)
    {
        Runtime?._clear_defeated_unit(unit_state, batch);
    }

    public GArray _sort_coords(GArray target_coords)
    {
        return _runtime == null
            ? new GArray()
            : ToUntypedVector2IArray(Runtime._sort_coords(target_coords));
    }

    public int _get_unit_skill_level(BattleUnitState unit_state, StringName skill_id)
    {
        return _runtime == null
            ? 0
            : Runtime._get_unit_skill_level(unit_state, skill_id);
    }

    public string _get_skill_cast_block_reason(BattleUnitState active_unit, SkillDef skill_def)
    {
        return _runtime == null
            ? ""
            : Runtime._get_skill_cast_block_reason(active_unit, skill_def);
    }

    public GDictionary _get_effective_skill_costs(BattleUnitState active_unit, SkillDef skill_def)
    {
        return _runtime == null
            ? new GDictionary()
            : Runtime._get_effective_skill_resource_costs(active_unit, skill_def).ToDictionary();
    }

    public CombatSkillResourceCosts _get_effective_skill_resource_costs(
        BattleUnitState active_unit,
        SkillDef skill_def
    )
    {
        return _runtime == null
            ? CombatSkillResourceCosts.Zero
            : Runtime._get_effective_skill_resource_costs(active_unit, skill_def);
    }

    public int _get_effective_skill_range(BattleUnitState active_unit, SkillDef skill_def)
    {
        return _runtime == null
            ? 0
            : Runtime._get_effective_skill_range(active_unit, skill_def);
    }

    public bool _is_movement_blocked(BattleUnitState unit_state)
    {
        return _runtime != null && Runtime._is_movement_blocked(unit_state);
    }

    public GDictionary _resolve_ground_spell_control_after_cost(
        BattleUnitState active_unit,
        SkillDef skill_def,
        int spent_mp,
        BattleEventBatch batch
    )
    {
        return _resolve_ground_spell_control_after_cost_result(
            active_unit,
            skill_def,
            spent_mp,
            batch
        ).ToDictionary();
    }

    public BattleSpellControlResult _resolve_ground_spell_control_after_cost_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        int spent_mp,
        BattleEventBatch batch
    )
    {
        BattleDamageResolver damageResolver = Runtime?.get_damage_resolver();
        BattleMagicBacklashResolver magicBacklashResolver = Runtime?._magic_backlash_resolver;
        if (
            damageResolver == null
            || magicBacklashResolver == null
            || !magicBacklashResolver.should_resolve_spell_control(skill_def as SkillDef)
        )
        {
            return BattleSpellControlResult.None();
        }
        StringName skillId = skill_def?.skill_id ?? Empty;
        int skillLevel = _get_unit_skill_level(active_unit, skillId);
        BattleSpellControlMetadata controlMetadata = damageResolver.resolve_spell_control_check_typed(
            active_unit,
            State,
            skillId
        );
        BattleSpellControlResult controlContext =
            magicBacklashResolver.apply_spell_control_after_cost_result(
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

    public GDictionary _resolve_unit_spell_control_after_cost(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch
    )
    {
        return _resolve_unit_spell_control_after_cost_result(
            active_unit,
            skill_def,
            batch
        ).ToDictionary();
    }

    public BattleSpellControlResult _resolve_unit_spell_control_after_cost_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch
    )
    {
        BattleDamageResolver damageResolver = Runtime?.get_damage_resolver();
        BattleMagicBacklashResolver magicBacklashResolver = Runtime?._magic_backlash_resolver;
        if (
            damageResolver == null
            || magicBacklashResolver == null
            || !magicBacklashResolver.should_resolve_spell_control(skill_def as SkillDef)
        )
        {
            return BattleSpellControlResult.None();
        }
        StringName skillId = skill_def?.skill_id ?? Empty;
        int skillLevel = _get_unit_skill_level(active_unit, skillId);
        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(active_unit, skill_def);
        int spentMp = costs.MpCost;
        BattleSpellControlMetadata controlMetadata = damageResolver.resolve_spell_control_check_typed(
            active_unit,
            State,
            skillId
        );
        BattleSpellControlResult controlContext =
            magicBacklashResolver.apply_spell_control_after_cost_result(
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

    public bool _apply_ground_precast_special_effects(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GArray target_coords,
        BattleEventBatch batch
    )
    {
        return _get_ground_relocation_effect_def(skill_def, cast_variant) == null
            || _apply_ground_relocation(active_unit, skill_def, cast_variant, target_coords, batch);
    }

    public bool _apply_ground_relocation(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GArray target_coords,
        BattleEventBatch batch
    )
    {
        if (State == null || active_unit == null || IsArrayEmpty(target_coords))
        {
            return false;
        }
        CombatEffectDef effectDef = _get_ground_relocation_effect_def(skill_def, cast_variant);
        return effectDef != null
            && _apply_ground_relocation_with_mode(
                active_unit,
                target_coords,
                batch,
                _get_effect_forced_move_mode(effectDef)
            );
    }

    public bool _apply_ground_relocation_with_mode(
        BattleUnitState active_unit,
        GArray target_coords,
        BattleEventBatch batch,
        StringName move_mode
    )
    {
        BattleState state = State;
        BattleGridService gridService = GridService;
        if (
            state == null
            || gridService == null
            || active_unit == null
            || IsArrayEmpty(target_coords)
        )
        {
            return false;
        }
        Vector2I landingCoord = target_coords[0].AsVector2I();
        if (active_unit.coord == landingCoord)
        {
            return true;
        }
        Vector2I previousAnchor = active_unit.coord;
        GArray previousCoords = ToUntypedVector2IArray(active_unit.occupied_coords);
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
        if (!gridService.move_unit_force(state, active_unit, landingCoord))
        {
            return false;
        }
        _append_changed_coords(batch, previousCoords);
        _append_changed_unit_coords(batch, active_unit);
        _append_changed_unit_id(batch, active_unit.unit_id);
        string moveLabel = move_mode == BlinkMode ? "闪现至" : "跳至";
        AppendLog(
            batch,
            $"{DisplayName(active_unit)} 从 ({previousAnchor.X}, {previousAnchor.Y}) {moveLabel} ({landingCoord.X}, {landingCoord.Y})。"
        );
        return true;
    }

    public bool _apply_ground_jump_relocation(
        BattleUnitState active_unit,
        GArray target_coords,
        BattleEventBatch batch
    )
    {
        return _apply_ground_relocation_with_mode(active_unit, target_coords, batch, JumpMode);
    }

    public CombatEffectDef _get_ground_relocation_effect_def(
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

    public CombatEffectDef _get_ground_jump_effect_def(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        CombatEffectDef effectDef = _get_ground_relocation_effect_def(skill_def, cast_variant);
        return _get_effect_forced_move_mode(effectDef) == JumpMode ? effectDef : null;
    }

    public bool _is_ground_jump_effect(CombatEffectDef effect_def)
    {
        return effect_def != null
            && effect_def.effect_type == ForcedMoveEffect
            && _get_effect_forced_move_mode(effect_def) == JumpMode;
    }

    public bool _is_ground_relocation_effect(CombatEffectDef effect_def)
    {
        return effect_def != null
            && effect_def.effect_type == ForcedMoveEffect
            && _is_ground_relocation_mode(_get_effect_forced_move_mode(effect_def));
    }

    public bool _is_ground_relocation_mode(StringName mode)
    {
        return mode == JumpMode || mode == BlinkMode;
    }

    public bool _can_use_ground_relocation(
        BattleUnitState active_unit,
        Vector2I landing_coord,
        CombatEffectDef effect_def
    )
    {
        if (effect_def == null || GridService == null)
        {
            return false;
        }
        StringName mode = _get_effect_forced_move_mode(effect_def);
        if (mode == JumpMode)
        {
            return GridService.can_jump_arc(
                State,
                active_unit,
                landing_coord,
                effect_def
            );
        }
        if (mode == BlinkMode)
        {
            return GridService.can_blink_to_coord(
                State,
                active_unit,
                landing_coord,
                effect_def
            );
        }
        return false;
    }

    public StringName _get_effect_forced_move_mode(CombatEffectDef effect_def)
    {
        if (effect_def == null)
        {
            return Empty;
        }
        StringName forcedMoveMode = effect_def?.forced_move_mode ?? Empty;
        return IsEmpty(forcedMoveMode) ? Empty : forcedMoveMode;
    }

    public GArray _build_ground_effect_coords(
        SkillDef skill_def,
        GArray target_coords,
        Vector2I source_coord,
        BattleUnitState active_unit,
        CombatCastVariantDef cast_variant
    )
    {
        var normalizedTargetCoords = new Godot.Collections.Array<Vector2I>();
        foreach (var targetCoord in target_coords ?? new GArray())
        {
            normalizedTargetCoords.Add(targetCoord.AsVector2I());
        }
        GDictionary castVariantParams = cast_variant?.@params ?? new GDictionary();
        if (
            cast_variant != null
            && castVariantParams.ContainsKey("square2_corner")
            && normalizedTargetCoords.Count == 1
        )
        {
            Vector2I center = normalizedTargetCoords[0];
            var expanded = new Godot.Collections.Array<Vector2I>();
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
            var valid = new Godot.Collections.Array<Vector2I>();
            foreach (Vector2I coord in expanded)
            {
                if (
                    State != null
                    && GridService != null
                    && GridService.is_inside(State, coord)
                )
                {
                    valid.Add(coord);
                }
            }
            if (valid.Count > 0)
            {
                return _sort_coords(ToUntypedVector2IArray(valid));
            }
        }
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (State == null || skill_def == null || combatProfile == null)
        {
            return _sort_coords(ToUntypedVector2IArray(normalizedTargetCoords));
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
            return _sort_coords(ToUntypedVector2IArray(collectedTargetCoords.TargetCoords));
        }
        return _sort_coords(ToUntypedVector2IArray(normalizedTargetCoords));
    }

    public GArray _collect_ground_unit_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit
    )
    {
        return _to_combat_effect_defs(
            ToUntypedEffectArray(
                SkillResolutionRules?.CollectGroundUnitEffectDefs(
                    skill_def,
                    cast_variant,
                    active_unit
                )
            )
        );
    }

    public GArray _collect_ground_terrain_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit
    )
    {
        return _to_combat_effect_defs(
            ToUntypedEffectArray(
                SkillResolutionRules?.CollectGroundTerrainEffectDefs(
                    skill_def,
                    cast_variant,
                    active_unit
                )
            )
        );
    }

    public GArray _collect_ground_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit
    )
    {
        return _to_combat_effect_defs(
            ToUntypedEffectArray(
                SkillResolutionRules?.CollectGroundEffectDefs(
                    skill_def,
                    cast_variant,
                    active_unit
                )
            )
        );
    }

    public GArray _to_combat_effect_defs(GArray effect_defs_option)
    {
        var effectDefs = new GArray();
        if (effect_defs_option == null)
        {
            return effectDefs;
        }
        foreach (var rawEffect in effect_defs_option)
        {
            CombatEffectDef effectDef = rawEffect.As<CombatEffectDef>();
            if (effectDef != null)
            {
                effectDefs.Add(effectDef);
            }
        }
        return effectDefs;
    }

    public Godot.Collections.Array<StringName> _collect_ground_preview_unit_ids(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GArray effect_defs,
        GArray effect_coords
    )
    {
        var targetUnitIds = new Godot.Collections.Array<StringName>();
        foreach (var rawTarget in _collect_units_in_coords(effect_coords))
        {
            BattleUnitState targetUnit = rawTarget.As<BattleUnitState>();
            foreach (var rawEffect in effect_defs ?? new GArray())
            {
                CombatEffectDef effectDef = rawEffect.As<CombatEffectDef>();
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

    public GDictionary _build_ground_forced_move_context(
        BattleUnitState source_unit,
        GArray target_coords
    )
    {
        return BuildGroundForcedMoveContext(source_unit, target_coords).ToDictionary();
    }

    private static BattleForcedMoveContext BuildGroundForcedMoveContext(
        BattleUnitState sourceUnit,
        GArray targetCoords
    )
    {
        if (sourceUnit == null || IsArrayEmpty(targetCoords))
        {
            return BattleForcedMoveContext.Empty;
        }
        return BattleForcedMoveContext.FromDirection(
            targetCoords[0].AsVector2I() - sourceUnit.coord
        );
    }

    public Vector2I _normalize_axis_direction(Vector2I direction)
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

    public bool _is_wind_push_effect(CombatEffectDef effect_def)
    {
        return effect_def != null
            && effect_def.effect_type == ForcedMoveEffect
            && effect_def.forced_move_mode == WindPushMode;
    }

    public GArray _collect_wind_push_effects(GArray effect_defs)
    {
        var windPushEffects = new GArray();
        var seen = new HashSet<ulong>();
        foreach (var rawEffect in effect_defs ?? new GArray())
        {
            CombatEffectDef effectDef = rawEffect.As<CombatEffectDef>();
            if (!_is_wind_push_effect(effectDef))
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

    public GDictionary _build_effect_instance_lookup(GArray effect_defs)
    {
        var lookup = new GDictionary();
        foreach (var rawEffect in effect_defs ?? new GArray())
        {
            CombatEffectDef effectDef = rawEffect.As<CombatEffectDef>();
            if (effectDef != null)
            {
                lookup[effectDef.GetInstanceId()] = true;
            }
        }
        return lookup;
    }

    private static HashSet<ulong> BuildEffectInstanceIdSet(GArray effectDefs)
    {
        var result = new HashSet<ulong>();
        foreach (var rawEffect in effectDefs ?? new GArray())
        {
            CombatEffectDef effectDef = rawEffect.As<CombatEffectDef>();
            if (effectDef != null)
            {
                result.Add(effectDef.GetInstanceId());
            }
        }
        return result;
    }

    public int _dot_coord(Vector2I coord, Vector2I direction) =>
        coord.X * direction.X + coord.Y * direction.Y;

    public int _perpendicular_coord(Vector2I coord, Vector2I direction) =>
        direction.X != 0 ? coord.Y : coord.X;

    public GArray _sort_wind_push_units_near_to_far(GArray units, Vector2I direction)
    {
        var sorted = new List<BattleUnitState>();
        foreach (var rawUnit in units ?? new GArray())
        {
            BattleUnitState unitState = rawUnit.As<BattleUnitState>();
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
        var result = new GArray();
        foreach (BattleUnitState unit in sorted)
        {
            result.Add(unit);
        }
        return result;
    }

    public void _append_affected_unit_id(GDictionary affected_unit_ids, BattleUnitState unit_state)
    {
        if (unit_state != null)
        {
            affected_unit_ids[unit_state.unit_id] = true;
        }
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

    private GArray CollectWindPushTargetUnits(
        BattleUnitState source_unit,
        SkillDef skill_def,
        CombatEffectDef effect_def,
        GArray effect_coords,
        BattleEventBatch batch,
        HashSet<StringName> affected_unit_ids,
        out bool applied
    )
    {
        applied = false;
        var units = new GArray();
        if (effect_def == null)
        {
            return units;
        }
        StringName targetFilter = _resolve_effect_target_filter(skill_def, effect_def);
        var barrierEffects = new GCombatEffectArray { effect_def };
        BattleLayeredBarrierService layeredBarrierService = LayeredBarrierService;
        foreach (var rawTarget in _collect_units_in_coords(effect_coords))
        {
            BattleUnitState targetUnit = rawTarget.As<BattleUnitState>();
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

    public GArray _collect_wind_push_target_units(
        BattleUnitState source_unit,
        SkillDef skill_def,
        CombatEffectDef effect_def,
        GArray effect_coords,
        BattleEventBatch batch,
        GDictionary result,
        GDictionary affected_unit_ids
    )
    {
        HashSet<StringName> affectedUnitIds = ReadStringNameSet(affected_unit_ids);
        GArray units = CollectWindPushTargetUnits(
            source_unit,
            skill_def,
            effect_def,
            effect_coords,
            batch,
            affectedUnitIds,
            out bool applied
        );
        WriteStringNameSet(affected_unit_ids, affectedUnitIds);
        if (applied && result != null)
        {
            result["applied"] = true;
        }
        return units;
    }

    public bool _try_wind_push_unit_one_step(
        BattleUnitState source_unit,
        SkillDef skill_def,
        CombatEffectDef effect_def,
        BattleUnitState unit_state,
        Vector2I direction,
        GDictionary moved_this_step,
        GDictionary affected_unit_ids,
        GDictionary recursion_stack,
        BattleEventBatch batch
    )
    {
        HashSet<StringName> movedThisStep = ReadStringNameSet(moved_this_step);
        HashSet<StringName> affectedUnitIds = ReadStringNameSet(affected_unit_ids);
        HashSet<StringName> recursionStack = ReadStringNameSet(recursion_stack);
        bool moved = TryWindPushUnitOneStep(
            source_unit,
            skill_def,
            effect_def,
            unit_state,
            direction,
            movedThisStep,
            affectedUnitIds,
            recursionStack,
            batch
        );
        WriteStringNameSet(moved_this_step, movedThisStep);
        WriteStringNameSet(affected_unit_ids, affectedUnitIds);
        WriteStringNameSet(recursion_stack, recursionStack);
        return moved;
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
        if (!gridService.is_inside(state, nextCoord))
        {
            return false;
        }
        var nextStack = new HashSet<StringName>(recursion_stack) { unitId };
        StringName targetFilter = _resolve_effect_target_filter(skill_def, effect_def);
        foreach (
            var rawBlockingUnitId in gridService.collect_blocking_unit_ids(
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
            !gridService.can_traverse(
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
        GArray previousCoords = ToUntypedVector2IArray(unitState.occupied_coords);
        if (!gridService.move_unit(state, unitState, nextCoord))
        {
            return false;
        }
        moved_this_step.Add(unitId);
        AppendAffectedUnitId(affected_unit_ids, unit_state);
        _append_changed_coords(batch, previousCoords);
        _append_changed_unit_coords(batch, unit_state);
        _append_changed_unit_id(batch, unitId);
        return true;
    }

    public GDictionary _apply_ground_wind_push_effects(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GArray wind_push_effects,
        GArray effect_coords,
        GArray target_coords,
        BattleEventBatch batch
    )
    {
        return _apply_ground_wind_push_effects_result(
            source_unit,
            skill_def,
            wind_push_effects,
            effect_coords,
            target_coords,
            batch
        ).ToDictionary();
    }

    public BattleGroundWindPushResult _apply_ground_wind_push_effects_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GArray wind_push_effects,
        GArray effect_coords,
        GArray target_coords,
        BattleEventBatch batch
    )
    {
        bool applied = false;
        if (IsArrayEmpty(wind_push_effects) || source_unit == null)
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
        foreach (var rawEffect in wind_push_effects)
        {
            CombatEffectDef effectDef = rawEffect.As<CombatEffectDef>();
            if (effectDef == null)
            {
                continue;
            }
            GArray targetUnits = CollectWindPushTargetUnits(
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
                GArray orderedUnits = _sort_wind_push_units_near_to_far(targetUnits, direction);
                foreach (var rawTarget in orderedUnits)
                {
                    BattleUnitState targetUnit = rawTarget.As<BattleUnitState>();
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

    public GDictionary _apply_ground_unit_effects(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GArray effect_defs,
        GArray effect_coords,
        BattleEventBatch batch,
        GArray target_coords
    )
    {
        return _apply_ground_unit_effects_result(
            source_unit,
            skill_def,
            effect_defs,
            effect_coords,
            batch,
            target_coords
        ).ToDictionary();
    }

    public BattleGroundUnitEffectsResult _apply_ground_unit_effects_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GArray effect_defs,
        GArray effect_coords,
        BattleEventBatch batch,
        GArray target_coords
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
        GArray windPushEffects = _collect_wind_push_effects(effect_defs);
        HashSet<ulong> windPushEffectIds = BuildEffectInstanceIdSet(windPushEffects);

        foreach (var rawTarget in _collect_units_in_coords(effect_coords))
        {
            BattleUnitState targetUnit = rawTarget.As<BattleUnitState>();
            if (targetUnit == null || !targetUnit.is_alive)
            {
                continue;
            }
            var applicableEffects = new GCombatEffectArray();
            foreach (var rawEffect in effect_defs ?? new GArray())
            {
                CombatEffectDef effectDef = rawEffect.As<CombatEffectDef>();
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

            GArray applicableEffectPayload = ToUntypedEffectArray(applicableEffects);
            GroundUnitEffectResolution effectResolution = _resolve_ground_unit_effect_resolution(
                source_unit,
                targetUnit,
                skill_def,
                applicableEffectPayload
            );
            GDictionary result = effectResolution.Payload;
            AttackEffectResolutionResult damageResult = effectResolution.Result;
            Runtime?._skill_mastery_service?.RecordTargetResult(
                source_unit,
                targetUnit,
                skill_def,
                damageResult,
                applicableEffects
            );
            BattleShieldApplyResult shieldResult = _apply_unit_shield_effects_result(
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
            mark_applied_statuses_for_turn_timing(
                targetUnit,
                ToUntypedStringNameArray(damageResult.StatusEffectIds)
            );
            bool attackResolved =
                result.ContainsKey("attack_success")
                || damageResult.AttackResolution != AttackResolutionKind.None;
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
                    _append_result_report_entry(batch, result);
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
                _append_result_report_entry(batch, result);
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
                    effect_defs,
                    batch
                );
                Runtime.handle_unit_defeated_by_runtime_effect(
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

    public GDictionary _resolve_ground_unit_effect_result(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GArray effect_defs
    )
    {
        return _resolve_ground_unit_effect_resolution(
            source_unit,
            target_unit,
            skill_def,
            effect_defs
        ).Payload;
    }

    private GroundUnitEffectResolution _resolve_ground_unit_effect_resolution(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GArray effect_defs
    )
    {
        SkillDef skillDef = skill_def;
        if (_should_resolve_ground_effects_as_attack(effect_defs))
        {
            GArray attackEffectDefs = _dedupe_effect_defs_by_instance(effect_defs);
            BattleRuntimeModule runtime = _runtime as BattleRuntimeModule;
            BattleAttackCheckPolicyService attackPolicy =
                runtime?.get_attack_check_policy_service();
            BattleDamageResolver damageResolver = runtime?.get_damage_resolver();
            BattleUnitState sourceUnit = source_unit as BattleUnitState;
            BattleUnitState targetUnit = target_unit as BattleUnitState;
            if (attackPolicy == null || damageResolver == null)
            {
                return GroundUnitEffectResolution.FromPayload(
                    new GDictionary(),
                    new AttackCheckInput(skillId: skillDef != null ? skillDef.skill_id : Empty)
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
            return GroundUnitEffectResolution.FromPayload(
                damageResolver.resolve_attack_effects(
                    sourceUnit,
                    targetUnit,
                    attackEffectDefs,
                    attackCheck,
                    new AttackContext
                    {
                        BattleState = State,
                        SkillId = skillDef != null ? skillDef.skill_id : Empty,
                    }
                ),
                attackCheck
            );
        }
        StringName skillId = skillDef != null ? skillDef.skill_id : Empty;
        return GroundUnitEffectResolution.FromPayload(
            ToDictionary(
                Runtime.get_damage_resolver()
                    .resolve_effects(
                        source_unit,
                        target_unit,
                        effect_defs,
                        new GDictionary { ["skill_id"] = skillId }
                    )
            ),
            new AttackCheckInput(skillId: skillId)
        );
    }

    public bool _should_resolve_ground_effects_as_attack(GArray effect_defs)
    {
        foreach (var rawEffect in effect_defs ?? new GArray())
        {
            CombatEffectDef effectDef = rawEffect.As<CombatEffectDef>();
            if (GroundEffectRuntimeParameters.FromEffect(effectDef).ResolveAsWeaponAttack)
            {
                return true;
            }
        }
        return false;
    }

    public GArray _dedupe_effect_defs_by_instance(GArray effect_defs)
    {
        var deduped = new GArray();
        var seen = new HashSet<ulong>();
        foreach (var rawEffect in effect_defs ?? new GArray())
        {
            CombatEffectDef effectDef = rawEffect.As<CombatEffectDef>();
            if (effectDef != null && seen.Add(effectDef.GetInstanceId()))
            {
                deduped.Add(effectDef);
            }
        }
        return deduped;
    }

    public GDictionary _apply_ground_terrain_effects(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GArray effect_defs,
        GArray effect_coords,
        BattleEventBatch batch
    )
    {
        return _apply_ground_terrain_effects_result(
            source_unit,
            skill_def,
            effect_defs,
            effect_coords,
            batch
        ).ToDictionary();
    }

    public BattleGroundTerrainEffectsResult _apply_ground_terrain_effects_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GArray effect_defs,
        GArray effect_coords,
        BattleEventBatch batch
    )
    {
        bool applied = false;
        bool requiresTopologyReconcile = false;
        BattleLayeredBarrierService layeredBarrierService = LayeredBarrierService;
        GCombatEffectArray barrierEffectDefs = ToCombatEffectDefArray(effect_defs);
        foreach (var rawEffect in effect_defs ?? new GArray())
        {
            CombatEffectDef effectDef = rawEffect.As<CombatEffectDef>();
            if (effectDef == null)
            {
                continue;
            }
            CombatEffectDef combatEffectDef = effectDef;
            StringName effectType = combatEffectDef?.effect_type ?? Empty;
            if (
                effectType == EffectTerrain
                || effectType == EffectTerrainReplace
                || effectType == EffectTerrainReplaceTo
                || effectType == EffectHeight
                || effectType == EffectHeightDelta
            )
            {
                requiresTopologyReconcile = true;
                foreach (var rawCoord in effect_coords ?? new GArray())
                {
                    Vector2I effectCoord = rawCoord.AsVector2I();
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
            else if (effectType == EffectTerrainEffect)
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
                    foreach (var rawCoord in effect_coords ?? new GArray())
                    {
                        Vector2I effectCoord = rawCoord.AsVector2I();
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
                            Runtime._terrain_effect_system.upsert_timed_terrain_effect(
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
                    foreach (var rawCoord in effect_coords ?? new GArray())
                    {
                        Vector2I effectCoord = rawCoord.AsVector2I();
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
                        BattleCellState cell = GridService.get_cell(State, effectCoord);
                        if (cell == null)
                        {
                            continue;
                        }
                        Godot.Collections.Array<StringName> terrainEffectIds =
                            cell.terrain_effect_ids;
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
            else if (effectType == EffectEdgeClear)
            {
                if (
                    _apply_ground_edge_clear_effect(
                        source_unit,
                        skill_def,
                        effect_coords,
                        effectDef,
                        batch
                    )
                )
                {
                    applied = true;
                }
            }
        }
        if (requiresTopologyReconcile && _reconcile_water_topology(effect_coords, batch))
        {
            applied = true;
        }
        return new BattleGroundTerrainEffectsResult(applied);
    }

    public bool _apply_ground_edge_clear_effect(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GArray effect_coords,
        CombatEffectDef effect_def,
        BattleEventBatch batch
    )
    {
        if (_runtime == null || State == null || effect_coords == null || effect_coords.Count < 2)
        {
            return false;
        }
        GArray edgeCoords = _sort_coords(effect_coords);
        Vector2I first = edgeCoords[0].AsVector2I();
        Vector2I second = edgeCoords[1].AsVector2I();
        if (GridService.get_distance(first, second) != 1)
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
        GDictionary edgeRef = _get_edge_authoring_reference(first, second);
        if (edgeRef.Count == 0)
        {
            return false;
        }
        Vector2I edgeCoord = ReadVector2I(edgeRef, "coord", new Vector2I(-1, -1));
        Vector2I edgeDirection = ReadVector2I(edgeRef, "direction", Vector2I.Zero);
        BattleCellState cell = GridService.get_cell(State, edgeCoord);
        if (cell == null)
        {
            return false;
        }
        BattleEdgeFeatureState featureState = cell.get_edge_feature(edgeDirection);
        if (featureState == null || featureState.is_empty())
        {
            return false;
        }
        if (!_can_edge_clear_remove_feature(effect_def, featureState))
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
        if (!GridService.clear_edge_feature(State, edgeCoord, edgeDirection))
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

    public GDictionary _get_edge_authoring_reference(Vector2I from_coord, Vector2I to_coord)
    {
        Vector2I delta = to_coord - from_coord;
        if (delta == Vector2I.Right)
        {
            return new GDictionary { ["coord"] = from_coord, ["direction"] = Vector2I.Right };
        }
        if (delta == Vector2I.Left)
        {
            return new GDictionary { ["coord"] = to_coord, ["direction"] = Vector2I.Right };
        }
        if (delta == Vector2I.Down)
        {
            return new GDictionary { ["coord"] = from_coord, ["direction"] = Vector2I.Down };
        }
        if (delta == Vector2I.Up)
        {
            return new GDictionary { ["coord"] = to_coord, ["direction"] = Vector2I.Down };
        }
        return new GDictionary();
    }

    public bool _can_edge_clear_remove_feature(
        CombatEffectDef effect_def,
        BattleEdgeFeatureState feature_state
    )
    {
        return _get_edge_clear_feature_kinds(effect_def)
            .ContainsKey(feature_state?.feature_kind ?? Empty);
    }

    public GDictionary _get_edge_clear_feature_kinds(CombatEffectDef effect_def)
    {
        var allowed = new GDictionary();
        GDictionary parameters = effect_def?.@params ?? new GDictionary();
        GArray rawKinds = ReadArray(parameters, "clear_feature_kinds");
        if (rawKinds.Count > 0)
        {
            foreach (var rawKind in rawKinds)
            {
                StringName kind = ToStringName(rawKind);
                if (!IsEmpty(kind))
                {
                    allowed[kind] = true;
                }
            }
        }
        if (allowed.Count == 0)
        {
            allowed[FeatureWall] = true;
            allowed[FeatureDoor] = true;
            allowed[FeatureGate] = true;
        }
        return allowed;
    }

    public string _get_edge_feature_display_name(BattleEdgeFeatureState feature_state)
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

    public bool _apply_ground_cell_effect(
        BattleUnitState source_unit,
        SkillDef skill_def,
        Vector2I target_coord,
        CombatEffectDef effect_def,
        BattleEventBatch batch
    )
    {
        BattleState state = State;
        CombatEffectDef effectDef = effect_def;
        BattleCellState cell = GridService.get_cell(state, target_coord);
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
        StringName effectType = effectDef.effect_type;
        if (
            effectType == EffectTerrain
            || effectType == EffectTerrainReplace
            || effectType == EffectTerrainReplaceTo
        )
        {
            StringName terrainReplaceTo = effectDef.terrain_replace_to;
            if (
                !IsEmpty(terrainReplaceTo)
                && cell.base_terrain != terrainReplaceTo
            )
            {
                if (
                    GridService.set_base_terrain(state, target_coord, terrainReplaceTo)
                )
                {
                    cellApplied = true;
                }
            }
        }
        else if (
            (effectType == EffectHeight || effectType == EffectHeightDelta)
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
                $"{_build_skill_log_subject_label(source_unit, skill_def)} 使 ({target_coord.X}, {target_coord.Y}) 的地形由 {GridService.get_terrain_display_name(beforeTerrain.ToString())} 变为 {GridService.get_terrain_display_name(cell.base_terrain.ToString())}。"
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
            GDictionary fallResult = ToDictionary(
                Runtime.get_damage_resolver().resolve_fall_damage(occupantUnitState, fallLayers)
            );
            AttackEffectResolutionResult fallDamageResult =
                AttackEffectResolutionResultReader.ReadResolverResult(
                    fallResult,
                    new AttackCheckInput()
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
                    Runtime.handle_unit_defeated_by_runtime_effect(
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

    public bool _reconcile_water_topology(GArray effect_coords, BattleEventBatch batch)
    {
        BattleState state = State;
        if (
            state == null
            || state.map_size == Vector2I.Zero
            || IsArrayEmpty(effect_coords)
        )
        {
            return false;
        }
        IReadOnlyList<BattleTerrainTopologyChange> changes =
            Runtime._terrain_topology_service.ReclassifyWaterTerrainNearCoords(
                state,
                ToVector2IList(effect_coords)
            );
        bool applied = false;
        foreach (BattleTerrainTopologyChange change in changes)
        {
            Vector2I coord = change.Coord;
            BattleCellState cell = GridService.get_cell(state, coord);
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
                GridService.set_base_terrain(state, coord, afterTerrain);
                cell = GridService.get_cell(state, coord);
                if (cell == null)
                {
                    continue;
                }
            }
            if (cell.flow_direction != afterFlowDirection)
            {
                cell.flow_direction = afterFlowDirection;
                GridService.recalculate_cell(cell);
                GridService.sync_column_from_surface_cell(state, coord);
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
                    $"相邻水域在 ({coord.X}, {coord.Y}) 重分类为 {GridService.get_terrain_display_name(cell.base_terrain.ToString())}。"
                );
            }
        }
        return applied;
    }

    public string _get_ground_special_effect_validation_message(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GArray target_coords
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
        if (IsArrayEmpty(target_coords))
        {
            return "位移落点无效。";
        }
        return _can_use_ground_relocation(
            active_unit,
            target_coords[0].AsVector2I(),
            relocationEffectDef
        )
            ? ""
            : "目标地格无法作为位移落点。";
    }

    public GDictionary _validate_ground_skill_command(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleCommand command
    )
    {
        return _validate_ground_skill_command_result(
                active_unit,
                skill_def,
                cast_variant,
                command
            )
            .ToDictionary();
    }

    public BattleGroundSkillValidationResult _validate_ground_skill_command_result(
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
        if (cast_variant.target_mode != GroundTargetMode)
        {
            return deniedResult with { Message = "该技能形态不是地面施法。" };
        }
        string blockReason = _get_skill_cast_block_reason(active_unit, skill_def);
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
        if (chargeResolver != null && chargeResolver.is_charge_option(cast_variant))
        {
            return chargeResolver.validate_charge_command_result(
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
            if (!GridService.is_inside(State, coord))
            {
                return deniedResult with { Message = "存在超出战场范围的目标地格。" };
            }
            int targetDistance =
                relocationEffectDef != null
                    ? GridService.get_chebyshev_distance(
                        active_unit.coord,
                        coord
                    )
                    : GridService.get_distance_from_unit_to_coord(
                        active_unit,
                        coord
                    );
            if (targetDistance > effectiveSkillRange)
            {
                return deniedResult with { Message = "目标地格超出技能施放距离。" };
            }
            if (!GridService.has_cell(State, coord))
            {
                return deniedResult with { Message = "目标地格数据不可用。" };
            }
            if (cast_variant.allowed_base_terrains.Count > 0)
            {
                bool normalizedAllowed = false;
                StringName normalizedCellTerrain = BattleTerrainRules.normalize_terrain_id(
                    GridService.get_cell_base_terrain_id(State, coord)
                );
                foreach (StringName rawAllowedTerrain in cast_variant.allowed_base_terrains)
                {
                    if (
                        BattleTerrainRules.normalize_terrain_id(rawAllowedTerrain)
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
                BattleUnitState targetUnit = GridService.get_unit_at_coord(State, coord);
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
                cast_variant.footprint_pattern,
                normalizedCoords
            )
        )
        {
            return deniedResult with { Message = "目标地格排布不符合该技能形态。" };
        }
        GArray sortedTargetCoords = _sort_coords(ToUntypedVector2IArray(normalizedCoords));
        string specialValidationMessage = _get_ground_special_effect_validation_message(
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
            ToVector2IList(sortedTargetCoords)
        );
    }

    public bool _validate_target_coords_shape(
        StringName footprint_pattern,
        Godot.Collections.Array<Vector2I> target_coords
    )
    {
        if (footprint_pattern == FootprintSingle)
        {
            return target_coords != null && target_coords.Count == 1;
        }
        if (footprint_pattern == FootprintLine2)
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
        if (footprint_pattern == FootprintSquare2)
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
        if (footprint_pattern == FootprintUnordered)
        {
            return target_coords != null && target_coords.Count > 0;
        }
        return false;
    }

    public Godot.Collections.Array<Vector2I> _normalize_target_coords(BattleCommand command)
    {
        var coords = new Godot.Collections.Array<Vector2I>();
        if (command == null)
        {
            return coords;
        }
        foreach (Vector2I targetCoord in command.target_coords)
        {
            coords.Add(targetCoord);
        }
        if (coords.Count == 0 && command.target_coord != new Vector2I(-1, -1))
        {
            coords.Add(command.target_coord);
        }
        return coords;
    }

    public StringName _build_terrain_effect_instance_id(StringName effect_id)
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

    public string _get_terrain_effect_display_name(CombatEffectDef effect_def)
    {
        GDictionary parameters = effect_def?.@params ?? new GDictionary();
        if (effect_def != null && parameters.ContainsKey("display_name"))
        {
            return ReadString(parameters, "display_name");
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

    private static bool TryResolveKey(
        GDictionary source,
        object key,
        out StringName stringNameKey,
        out string stringKey,
        out bool useStringName
    )
    {
        stringNameKey = Empty;
        stringKey = "";
        useStringName = false;
        if (source == null || key == null)
        {
            return false;
        }
        if (key is StringName namedKey)
        {
            if (source.ContainsKey(namedKey))
            {
                stringNameKey = namedKey;
                useStringName = true;
                return true;
            }
            string namedKeyText = namedKey.ToString();
            if (source.ContainsKey(namedKeyText))
            {
                stringKey = namedKeyText;
                return true;
            }
            return false;
        }

        string textKey = key.ToString();
        if (string.IsNullOrEmpty(textKey))
            return false;
        if (source.ContainsKey(textKey))
        {
            stringKey = textKey;
            return true;
        }
        StringName normalizedKey = new(textKey);
        if (source.ContainsKey(normalizedKey))
        {
            stringNameKey = normalizedKey;
            useStringName = true;
            return true;
        }
        return false;
    }

    private static string ReadString(GDictionary source, object key, string fallback = "")
    {
        if (!TryResolveKey(source, key, out StringName stringNameKey, out string stringKey, out bool useStringName))
        {
            return fallback;
        }
        string result = useStringName
            ? source[stringNameKey].ToString()
            : source[stringKey].ToString();
        return string.IsNullOrEmpty(result) || result == "<null>" ? fallback : result;
    }

    private static StringName ReadStringName(
        GDictionary source,
        object key,
        StringName fallback = default
    )
    {
        if (!TryResolveKey(source, key, out StringName stringNameKey, out string stringKey, out bool useStringName))
        {
            return fallback ?? Empty;
        }
        StringName result = useStringName
            ? ProgressionDataUtils.to_string_name(source[stringNameKey])
            : ProgressionDataUtils.to_string_name(source[stringKey]);
        return result != Empty ? result : fallback ?? Empty;
    }

    private static Vector2I ReadVector2I(
        GDictionary source,
        object key,
        Vector2I fallback = default
    )
    {
        if (!TryResolveKey(source, key, out StringName stringNameKey, out string stringKey, out bool useStringName))
        {
            return fallback;
        }
        return useStringName
            ? source[stringNameKey].AsVector2I()
            : source[stringKey].AsVector2I();
    }

    private static GArray ReadArray(GDictionary source, object key)
    {
        if (!TryResolveKey(source, key, out StringName stringNameKey, out string stringKey, out bool useStringName))
        {
            return new GArray();
        }
        return useStringName
            ? source[stringNameKey].AsGodotArray()
            : source[stringKey].AsGodotArray();
    }

    private static GDictionary ToDictionary(object rawValue)
    {
        return rawValue as GDictionary ?? new GDictionary();
    }

    private static GArray ToArray(object rawValue)
    {
        return rawValue as GArray ?? new GArray();
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

    private static GArray ToUntypedEffectArray(IEnumerable<CombatEffectDef> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (CombatEffectDef effectDef in values)
        {
            if (effectDef != null)
            {
                result.Add(effectDef);
            }
        }
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

    private static GArray ToUntypedBattleUnitArray(Godot.Collections.Array<BattleUnitState> values)
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
                result.Add(unitState);
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

    private static HashSet<StringName> ReadStringNameSet(GDictionary source)
    {
        var result = new HashSet<StringName>();
        if (source == null)
        {
            return result;
        }
        foreach (var key in source.Keys)
        {
            result.Add(ToStringName(key));
        }
        return result;
    }

    private static void WriteStringNameSet(GDictionary target, HashSet<StringName> source)
    {
        if (target == null)
        {
            return;
        }
        target.Clear();
        if (source == null)
        {
            return;
        }
        foreach (StringName value in source)
        {
            target[value] = true;
        }
    }

    private static void AppendLog(BattleEventBatch batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
        {
            return;
        }
        batch.log_lines.Add(line);
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
        internal readonly GDictionary Payload;
        internal readonly AttackEffectResolutionResult Result;

        private GroundUnitEffectResolution(GDictionary payload, AttackEffectResolutionResult result)
        {
            Payload = payload ?? new GDictionary();
            Result = result;
        }

        internal static GroundUnitEffectResolution FromPayload(
            GDictionary payload,
            AttackCheckInput attackCheck
        )
        {
            payload ??= new GDictionary();
            return new GroundUnitEffectResolution(
                payload,
                AttackEffectResolutionResultReader.ReadResolverResult(payload, attackCheck)
            );
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
