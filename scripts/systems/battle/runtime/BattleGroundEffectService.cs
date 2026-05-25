using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
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

    private WeakReference<GodotObject> _runtimeRef;

    private GodotObject _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    public void setup(GodotObject runtime)
    {
        _runtime = runtime;
    }

    public void dispose()
    {
        _runtime = null;
    }

    public void _append_result_report_entry(GodotObject batch, GDictionary result)
    {
        _runtime?.Call("_append_result_report_entry", batch, result);
    }

    public void mark_applied_statuses_for_turn_timing(GodotObject target_unit, Variant status_effect_ids)
    {
        _runtime?.Call("mark_applied_statuses_for_turn_timing", target_unit, status_effect_ids);
    }

    public void append_result_source_status_effects(GodotObject batch, GodotObject source_unit, GDictionary result)
    {
        _runtime?.Call("append_result_source_status_effects", batch, source_unit, result);
    }

    public void _record_effect_metrics(GodotObject source_unit, GodotObject target_unit, int damage, int healing, int kill_count)
    {
        _runtime?.Call("_record_effect_metrics", source_unit, target_unit, damage, healing, kill_count);
    }

    public void _record_unit_defeated(GodotObject unit_state)
    {
        _runtime?.Call("_record_unit_defeated", unit_state);
    }

    public void append_damage_result_log_lines(GodotObject batch, string subject_label, string target_display_name, GDictionary result)
    {
        _runtime?.Call("append_damage_result_log_lines", batch, subject_label, target_display_name, result);
    }

    public string _build_skill_log_subject_label(GodotObject source_unit, GodotObject skill_def, GodotObject cast_variant = null)
    {
        return _runtime == null ? "" : _runtime.Call("_build_skill_log_subject_label", source_unit, skill_def, cast_variant).AsString();
    }

    public void _apply_on_kill_gain_resources_effects(GodotObject source_unit, GodotObject defeated_unit, GodotObject skill_def, GArray effect_defs, GodotObject batch)
    {
        _runtime?.Call("_apply_on_kill_gain_resources_effects", source_unit, defeated_unit, skill_def, effect_defs, batch);
    }

    public bool _is_crown_break_target_eligible(GodotObject active_unit, GodotObject target_unit)
    {
        return _runtime != null && _runtime.Call("_is_crown_break_target_eligible", active_unit, target_unit).AsBool();
    }

    public bool _is_crown_break_skill(StringName skill_id)
    {
        return _runtime != null && _runtime.Call("_is_crown_break_skill", skill_id).AsBool();
    }

    public void _record_vajra_body_mastery_from_incoming_damage(GodotObject source_unit, GodotObject target_unit, GodotObject skill_def, GDictionary result, GodotObject batch = null)
    {
        _runtime?.Call("_record_vajra_body_mastery_from_incoming_damage", source_unit, target_unit, skill_def, result, batch);
    }

    public GArray _collect_units_in_coords(GArray effect_coords)
    {
        return _runtime == null ? new GArray() : ToArray(_runtime.Call("_collect_units_in_coords", effect_coords));
    }

    public GDictionary _apply_unit_shield_effects(GodotObject source_unit, GodotObject target_unit, GodotObject skill_def, GArray effect_defs, GDictionary shield_roll_context = null)
    {
        if (_runtime == null)
        {
            return new GDictionary();
        }
        return ToDictionary(_runtime.Call("_apply_unit_shield_effects", source_unit, target_unit, skill_def, effect_defs, shield_roll_context ?? new GDictionary()));
    }

    public StringName _resolve_effect_target_filter(GodotObject skill_def, GodotObject effect_def)
    {
        return _runtime == null ? Empty : ToStringName(_runtime.Call("_resolve_effect_target_filter", skill_def, effect_def));
    }

    public bool _is_unit_valid_for_effect(GodotObject source_unit, GodotObject target_unit, StringName target_team_filter)
    {
        return _runtime != null && _runtime.Call("_is_unit_valid_for_effect", source_unit, target_unit, target_team_filter).AsBool();
    }

    public void _flush_last_stand_mastery_records(GodotObject batch)
    {
        _runtime?.Call("_flush_last_stand_mastery_records", batch);
    }

    public void _append_changed_coord(GodotObject batch, Vector2I coord)
    {
        _runtime?.Call("_append_changed_coord", batch, coord);
    }

    public void _append_changed_coords(GodotObject batch, GArray coords)
    {
        _runtime?.Call("_append_changed_coords", batch, coords);
    }

    public void _append_changed_unit_id(GodotObject batch, StringName unit_id)
    {
        _runtime?.Call("_append_changed_unit_id", batch, unit_id);
    }

    public void _append_changed_unit_coords(GodotObject batch, GodotObject unit_state)
    {
        _runtime?.Call("_append_changed_unit_coords", batch, unit_state);
    }

    public void _collect_defeated_unit_loot(GodotObject unit_state, GodotObject killer_unit = null)
    {
        _runtime?.Call("_collect_defeated_unit_loot", unit_state, killer_unit);
    }

    public void _clear_defeated_unit(GodotObject unit_state, GodotObject batch = null)
    {
        _runtime?.Call("_clear_defeated_unit", unit_state, batch);
    }

    public GArray _sort_coords(Variant target_coords)
    {
        return _runtime == null ? new GArray() : ToArray(_runtime.Call("_sort_coords", target_coords));
    }

    public int _get_unit_skill_level(GodotObject unit_state, StringName skill_id)
    {
        return _runtime == null ? 0 : _runtime.Call("_get_unit_skill_level", unit_state, skill_id).AsInt32();
    }

    public string _get_skill_cast_block_reason(GodotObject active_unit, GodotObject skill_def)
    {
        return _runtime == null ? "" : _runtime.Call("_get_skill_cast_block_reason", active_unit, skill_def).AsString();
    }

    public GDictionary _get_effective_skill_costs(GodotObject active_unit, GodotObject skill_def)
    {
        return _runtime == null ? new GDictionary() : ToDictionary(_runtime.Call("_get_effective_skill_costs", active_unit, skill_def));
    }

    public int _get_effective_skill_range(GodotObject active_unit, GodotObject skill_def)
    {
        return _runtime == null ? 0 : _runtime.Call("_get_effective_skill_range", active_unit, skill_def).AsInt32();
    }

    public bool _is_movement_blocked(GodotObject unit_state)
    {
        return _runtime != null && _runtime.Call("_is_movement_blocked", unit_state).AsBool();
    }

    public GDictionary _resolve_ground_spell_control_after_cost(GodotObject active_unit, GodotObject skill_def, int spent_mp, GodotObject batch)
    {
        GodotObject damageResolver = GetRuntimeObject("_damage_resolver");
        GodotObject magicBacklashResolver = GetRuntimeObject("_magic_backlash_resolver");
        if (damageResolver == null || magicBacklashResolver == null || !magicBacklashResolver.Call("should_resolve_spell_control", skill_def).AsBool())
        {
            return new GDictionary();
        }
        StringName skillId = skill_def != null ? GdInterop.GetStringName(skill_def, "skill_id") : Empty;
        int skillLevel = _get_unit_skill_level(active_unit, skillId);
        var controlMetadata = ToDictionary(damageResolver.Call(
            "resolve_spell_control_check",
            active_unit,
            new GDictionary
            {
                ["battle_state"] = State,
                ["skill_id"] = skillId,
            }));
        var controlContext = ToDictionary(magicBacklashResolver.Call(
            "apply_spell_control_after_cost",
            active_unit,
            skill_def,
            skillLevel,
            spent_mp,
            controlMetadata,
            batch));
        _append_changed_unit_id(batch, active_unit != null ? GdInterop.GetStringName(active_unit, "unit_id") : Empty);
        return controlContext;
    }

    public GDictionary _resolve_unit_spell_control_after_cost(GodotObject active_unit, GodotObject skill_def, GodotObject batch)
    {
        GodotObject damageResolver = GetRuntimeObject("_damage_resolver");
        GodotObject magicBacklashResolver = GetRuntimeObject("_magic_backlash_resolver");
        if (damageResolver == null || magicBacklashResolver == null || !magicBacklashResolver.Call("should_resolve_spell_control", skill_def).AsBool())
        {
            return new GDictionary();
        }
        StringName skillId = skill_def != null ? GdInterop.GetStringName(skill_def, "skill_id") : Empty;
        int skillLevel = _get_unit_skill_level(active_unit, skillId);
        GDictionary costs = _get_effective_skill_costs(active_unit, skill_def);
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        int spentMp = GdInterop.GetInt(costs, "mp_cost", combatProfile != null ? GdInterop.GetInt(combatProfile, "mp_cost") : 0);
        var controlMetadata = ToDictionary(damageResolver.Call(
            "resolve_spell_control_check",
            active_unit,
            new GDictionary
            {
                ["battle_state"] = State,
                ["skill_id"] = skillId,
            }));
        var controlContext = ToDictionary(magicBacklashResolver.Call(
            "apply_spell_control_after_cost",
            active_unit,
            skill_def,
            skillLevel,
            spentMp,
            controlMetadata,
            batch));
        _append_changed_unit_id(batch, active_unit != null ? GdInterop.GetStringName(active_unit, "unit_id") : Empty);
        return controlContext;
    }

    public bool _apply_ground_precast_special_effects(GodotObject active_unit, GodotObject skill_def, GodotObject cast_variant, GArray target_coords, GodotObject batch)
    {
        return _get_ground_relocation_effect_def(skill_def, cast_variant) == null
            || _apply_ground_relocation(active_unit, skill_def, cast_variant, target_coords, batch);
    }

    public bool _apply_ground_relocation(GodotObject active_unit, GodotObject skill_def, GodotObject cast_variant, GArray target_coords, GodotObject batch)
    {
        if (State == null || active_unit == null || IsArrayEmpty(target_coords))
        {
            return false;
        }
        GodotObject effectDef = _get_ground_relocation_effect_def(skill_def, cast_variant);
        return effectDef != null && _apply_ground_relocation_with_mode(active_unit, target_coords, batch, _get_effect_forced_move_mode(effectDef));
    }

    public bool _apply_ground_relocation_with_mode(GodotObject active_unit, GArray target_coords, GodotObject batch, StringName move_mode)
    {
        GodotObject state = State;
        GodotObject gridService = GridService;
        if (state == null || gridService == null || active_unit == null || IsArrayEmpty(target_coords))
        {
            return false;
        }
        Vector2I landingCoord = ToVector2I(target_coords[0]);
        if (GdInterop.GetVector2I(active_unit, "coord") == landingCoord)
        {
            return true;
        }
        Vector2I previousAnchor = GdInterop.GetVector2I(active_unit, "coord");
        GArray previousCoords = GdInterop.GetArray(active_unit, "occupied_coords").Duplicate();
        GodotObject layeredBarrierService = GetRuntimeObject("_layered_barrier_service");
        if (layeredBarrierService != null)
        {
            GDictionary barrierResult = ToDictionary(layeredBarrierService.Call("resolve_unit_boundary_crossing", active_unit, previousAnchor, landingCoord, batch));
            if (GdInterop.GetBool(barrierResult, "blocked", false)
                || !GdInterop.GetBool(active_unit, "is_alive")
                || GdInterop.GetVector2I(active_unit, "coord") != previousAnchor)
            {
                return false;
            }
        }
        if (!gridService.Call("move_unit_force", state, active_unit, landingCoord).AsBool())
        {
            return false;
        }
        _append_changed_coords(batch, previousCoords);
        _append_changed_unit_coords(batch, active_unit);
        _append_changed_unit_id(batch, GdInterop.GetStringName(active_unit, "unit_id"));
        string moveLabel = move_mode == BlinkMode ? "闪现至" : "跳至";
        AppendLog(batch, $"{DisplayName(active_unit)} 从 ({previousAnchor.X}, {previousAnchor.Y}) {moveLabel} ({landingCoord.X}, {landingCoord.Y})。");
        return true;
    }

    public bool _apply_ground_jump_relocation(GodotObject active_unit, GArray target_coords, GodotObject batch)
    {
        return _apply_ground_relocation_with_mode(active_unit, target_coords, batch, JumpMode);
    }

    public GodotObject _get_ground_relocation_effect_def(GodotObject skill_def, GodotObject cast_variant)
    {
        if (cast_variant != null)
        {
            foreach (Variant rawEffect in GdInterop.GetArray(cast_variant, "effect_defs"))
            {
                GodotObject effectDef = rawEffect.AsGodotObject();
                if (_is_ground_relocation_effect(effectDef))
                {
                    return effectDef;
                }
            }
        }
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (combatProfile != null)
        {
            foreach (Variant rawEffect in GdInterop.GetArray(combatProfile, "effect_defs"))
            {
                GodotObject effectDef = rawEffect.AsGodotObject();
                if (_is_ground_relocation_effect(effectDef))
                {
                    return effectDef;
                }
            }
        }
        return null;
    }

    public GodotObject _get_ground_jump_effect_def(GodotObject skill_def, GodotObject cast_variant)
    {
        GodotObject effectDef = _get_ground_relocation_effect_def(skill_def, cast_variant);
        return _get_effect_forced_move_mode(effectDef) == JumpMode ? effectDef : null;
    }

    public bool _is_ground_jump_effect(GodotObject effect_def)
    {
        return effect_def != null
            && GdInterop.GetStringName(effect_def, "effect_type") == ForcedMoveEffect
            && _get_effect_forced_move_mode(effect_def) == JumpMode;
    }

    public bool _is_ground_relocation_effect(GodotObject effect_def)
    {
        return effect_def != null
            && GdInterop.GetStringName(effect_def, "effect_type") == ForcedMoveEffect
            && _is_ground_relocation_mode(_get_effect_forced_move_mode(effect_def));
    }

    public bool _is_ground_relocation_mode(StringName mode)
    {
        return mode == JumpMode || mode == BlinkMode;
    }

    public bool _can_use_ground_relocation(GodotObject active_unit, Vector2I landing_coord, GodotObject effect_def)
    {
        if (effect_def == null || GridService == null)
        {
            return false;
        }
        StringName mode = _get_effect_forced_move_mode(effect_def);
        if (mode == JumpMode)
        {
            return GridService.Call("can_jump_arc", State, active_unit, landing_coord, effect_def).AsBool();
        }
        if (mode == BlinkMode)
        {
            return GridService.Call("can_blink_to_coord", State, active_unit, landing_coord, effect_def).AsBool();
        }
        return false;
    }

    public StringName _get_effect_forced_move_mode(GodotObject effect_def)
    {
        if (effect_def == null)
        {
            return Empty;
        }
        StringName forcedMoveMode = GdInterop.GetStringName(effect_def, "forced_move_mode");
        return GdInterop.IsEmpty(forcedMoveMode) ? Empty : forcedMoveMode;
    }

    public GArray _build_ground_effect_coords(GodotObject skill_def, GArray target_coords, Vector2I source_coord, GodotObject active_unit, GodotObject cast_variant)
    {
        var normalizedTargetCoords = new Godot.Collections.Array<Vector2I>();
        foreach (Variant targetCoord in target_coords ?? new GArray())
        {
            normalizedTargetCoords.Add(ToVector2I(targetCoord));
        }
        GDictionary castVariantParams = GdInterop.GetDictionary(cast_variant, "params");
        if (cast_variant != null && castVariantParams.ContainsKey("square2_corner") && normalizedTargetCoords.Count == 1)
        {
            Vector2I center = ToVector2I(normalizedTargetCoords[0]);
            var expanded = new Godot.Collections.Array<Vector2I>();
            string corner = GdInterop.GetString(castVariantParams, "square2_corner", "");
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
                if (State != null && GridService != null && GridService.Call("is_inside", State, coord).AsBool())
                {
                    valid.Add(coord);
                }
            }
            if (valid.Count > 0)
            {
                return _sort_coords(valid);
            }
        }
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (State == null || skill_def == null || combatProfile == null)
        {
            return _sort_coords(normalizedTargetCoords);
        }
        int skillLevel = _get_unit_skill_level(active_unit, GdInterop.GetStringName(skill_def, "skill_id"));
        GDictionary collectedTargetCoords = ToDictionary(TargetCollectionService.Call(
            "collect_combat_profile_target_coords",
            State,
            GridService,
            source_coord,
            combatProfile,
            normalizedTargetCoords,
            default(Variant),
            new GArray(),
            skillLevel));
        if (GdInterop.GetBool(collectedTargetCoords, "handled", false))
        {
            return _sort_coords(GdInterop.GetArray(collectedTargetCoords, "target_coords"));
        }
        return _sort_coords(normalizedTargetCoords);
    }

    public GArray _collect_ground_unit_effect_defs(GodotObject skill_def, GodotObject cast_variant, GodotObject active_unit)
    {
        return _to_combat_effect_defs(SkillResolutionRules.Call("collect_ground_unit_effect_defs", skill_def, cast_variant, active_unit));
    }

    public GArray _collect_ground_terrain_effect_defs(GodotObject skill_def, GodotObject cast_variant, GodotObject active_unit)
    {
        return _to_combat_effect_defs(SkillResolutionRules.Call("collect_ground_terrain_effect_defs", skill_def, cast_variant, active_unit));
    }

    public GArray _collect_ground_effect_defs(GodotObject skill_def, GodotObject cast_variant, GodotObject active_unit)
    {
        return _to_combat_effect_defs(SkillResolutionRules.Call("collect_ground_effect_defs", skill_def, cast_variant, active_unit));
    }

    public GArray _to_combat_effect_defs(Variant effect_defs_variant)
    {
        var effectDefs = new GArray();
        if (effect_defs_variant.VariantType != Variant.Type.Array)
        {
            return effectDefs;
        }
        foreach (Variant rawEffect in effect_defs_variant.AsGodotArray())
        {
            GodotObject effectDef = rawEffect.AsGodotObject();
            if (effectDef != null)
            {
                effectDefs.Add(effectDef);
            }
        }
        return effectDefs;
    }

    public Godot.Collections.Array<StringName> _collect_ground_preview_unit_ids(GodotObject source_unit, GodotObject skill_def, GArray effect_defs, GArray effect_coords)
    {
        var targetUnitIds = new Godot.Collections.Array<StringName>();
        foreach (Variant rawTarget in _collect_units_in_coords(effect_coords))
        {
            GodotObject targetUnit = rawTarget.AsGodotObject();
            foreach (Variant rawEffect in effect_defs ?? new GArray())
            {
                GodotObject effectDef = rawEffect.AsGodotObject();
                if (_is_unit_valid_for_effect(source_unit, targetUnit, _resolve_effect_target_filter(skill_def, effectDef)))
                {
                    targetUnitIds.Add(GdInterop.GetStringName(targetUnit, "unit_id"));
                    break;
                }
            }
        }
        return targetUnitIds;
    }

    public GDictionary _build_ground_forced_move_context(GodotObject source_unit, GArray target_coords)
    {
        if (source_unit == null || IsArrayEmpty(target_coords))
        {
            return new GDictionary();
        }
        Vector2I direction = _normalize_axis_direction(ToVector2I(target_coords[0]) - GdInterop.GetVector2I(source_unit, "coord"));
        return direction == Vector2I.Zero ? new GDictionary() : new GDictionary { ["direction"] = direction };
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

    public bool _is_wind_push_effect(GodotObject effect_def)
    {
        return effect_def != null
            && GdInterop.GetStringName(effect_def, "effect_type") == ForcedMoveEffect
            && GdInterop.GetStringName(effect_def, "forced_move_mode") == WindPushMode;
    }

    public GArray _collect_wind_push_effects(GArray effect_defs)
    {
        var windPushEffects = new GArray();
        var seen = new HashSet<ulong>();
        foreach (Variant rawEffect in effect_defs ?? new GArray())
        {
            GodotObject effectDef = rawEffect.AsGodotObject();
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
        foreach (Variant rawEffect in effect_defs ?? new GArray())
        {
            GodotObject effectDef = rawEffect.AsGodotObject();
            if (effectDef != null)
            {
                lookup[effectDef.GetInstanceId()] = true;
            }
        }
        return lookup;
    }

    public int _dot_coord(Vector2I coord, Vector2I direction) => coord.X * direction.X + coord.Y * direction.Y;

    public int _perpendicular_coord(Vector2I coord, Vector2I direction) => direction.X != 0 ? coord.Y : coord.X;

    public GArray _sort_wind_push_units_near_to_far(GArray units, Vector2I direction)
    {
        var sorted = new List<GodotObject>();
        foreach (Variant rawUnit in units ?? new GArray())
        {
            GodotObject unitState = rawUnit.AsGodotObject();
            if (unitState != null && GdInterop.GetBool(unitState, "is_alive"))
            {
                sorted.Add(unitState);
            }
        }
        sorted.Sort((left, right) =>
        {
            int leftProjection = _dot_coord(GdInterop.GetVector2I(left, "coord"), direction);
            int rightProjection = _dot_coord(GdInterop.GetVector2I(right, "coord"), direction);
            if (leftProjection != rightProjection)
            {
                return leftProjection.CompareTo(rightProjection);
            }
            int leftSide = _perpendicular_coord(GdInterop.GetVector2I(left, "coord"), direction);
            int rightSide = _perpendicular_coord(GdInterop.GetVector2I(right, "coord"), direction);
            if (leftSide != rightSide)
            {
                return leftSide.CompareTo(rightSide);
            }
            return string.Compare(
                GdInterop.GetStringName(left, "unit_id").ToString(),
                GdInterop.GetStringName(right, "unit_id").ToString(),
                StringComparison.Ordinal);
        });
        var result = new GArray();
        foreach (GodotObject unit in sorted)
        {
            result.Add(unit);
        }
        return result;
    }

    public void _append_affected_unit_id(GDictionary affected_unit_ids, GodotObject unit_state)
    {
        if (unit_state != null)
        {
            affected_unit_ids[GdInterop.GetStringName(unit_state, "unit_id")] = true;
        }
    }

    public GArray _collect_wind_push_target_units(
        GodotObject source_unit,
        GodotObject skill_def,
        GodotObject effect_def,
        GArray effect_coords,
        GodotObject batch,
        GDictionary result,
        GDictionary affected_unit_ids)
    {
        var units = new GArray();
        if (effect_def == null)
        {
            return units;
        }
        StringName targetFilter = _resolve_effect_target_filter(skill_def, effect_def);
        var barrierEffects = new GArray { effect_def };
        GodotObject layeredBarrierService = GetRuntimeObject("_layered_barrier_service");
        foreach (Variant rawTarget in _collect_units_in_coords(effect_coords))
        {
            GodotObject targetUnit = rawTarget.AsGodotObject();
            if (targetUnit == null || !GdInterop.GetBool(targetUnit, "is_alive"))
            {
                continue;
            }
            if (!_is_unit_valid_for_effect(source_unit, targetUnit, targetFilter))
            {
                continue;
            }
            GDictionary barrierResult = layeredBarrierService != null
                ? ToDictionary(layeredBarrierService.Call("resolve_skill_barrier_interaction", source_unit, targetUnit, skill_def, barrierEffects, batch))
                : new GDictionary();
            if (GdInterop.GetBool(barrierResult, "blocked", false))
            {
                if (GdInterop.GetBool(barrierResult, "applied", false))
                {
                    result["applied"] = true;
                    _append_affected_unit_id(affected_unit_ids, targetUnit);
                }
                continue;
            }
            units.Add(targetUnit);
        }
        return units;
    }

    public bool _try_wind_push_unit_one_step(
        GodotObject source_unit,
        GodotObject skill_def,
        GodotObject effect_def,
        GodotObject unit_state,
        Vector2I direction,
        GDictionary moved_this_step,
        GDictionary affected_unit_ids,
        GDictionary recursion_stack,
        GodotObject batch)
    {
        GodotObject state = State;
        GodotObject gridService = GridService;
        if (_runtime == null || state == null || gridService == null || unit_state == null || !GdInterop.GetBool(unit_state, "is_alive") || direction == Vector2I.Zero)
        {
            return false;
        }
        StringName unitId = GdInterop.GetStringName(unit_state, "unit_id");
        if (moved_this_step.ContainsKey(unitId))
        {
            return false;
        }
        if (_runtime.Call("_blocks_enemy_forced_move", source_unit, unit_state).AsBool())
        {
            AppendLog(batch, $"{DisplayName(unit_state)} 稳如金刚，未被强制位移。");
            return false;
        }
        if (recursion_stack.ContainsKey(unitId))
        {
            return false;
        }
        Vector2I currentCoord = GdInterop.GetVector2I(unit_state, "coord");
        Vector2I nextCoord = currentCoord + direction;
        if (!gridService.Call("is_inside", state, nextCoord).AsBool())
        {
            return false;
        }
        GDictionary nextStack = recursion_stack.Duplicate();
        nextStack[unitId] = true;
        StringName targetFilter = _resolve_effect_target_filter(skill_def, effect_def);
        foreach (Variant rawBlockingUnitId in ToArray(gridService.Call("collect_blocking_unit_ids", state, unit_state, nextCoord)))
        {
            StringName blockingUnitId = ToStringName(rawBlockingUnitId);
            if (blockingUnitId == unitId)
            {
                continue;
            }
            GodotObject blockingUnit = GdInterop.GetObject(GdInterop.GetDictionary(state, "units"), blockingUnitId);
            if (blockingUnit == null || !GdInterop.GetBool(blockingUnit, "is_alive"))
            {
                return false;
            }
            if (!_is_unit_valid_for_effect(source_unit, blockingUnit, targetFilter))
            {
                return false;
            }
            if (!_try_wind_push_unit_one_step(source_unit, skill_def, effect_def, blockingUnit, direction, moved_this_step, affected_unit_ids, nextStack, batch))
            {
                return false;
            }
        }
        if (!gridService.Call("can_traverse", state, currentCoord, nextCoord, unit_state).AsBool())
        {
            return false;
        }
        GodotObject layeredBarrierService = GetRuntimeObject("_layered_barrier_service");
        GDictionary barrierResult = layeredBarrierService != null
            ? ToDictionary(layeredBarrierService.Call("resolve_unit_boundary_crossing", unit_state, currentCoord, nextCoord, batch))
            : new GDictionary();
        if (GdInterop.GetBool(barrierResult, "blocked", false) || !GdInterop.GetBool(unit_state, "is_alive"))
        {
            _append_affected_unit_id(affected_unit_ids, unit_state);
            return false;
        }
        GArray previousCoords = GdInterop.GetArray(unit_state, "occupied_coords").Duplicate();
        if (!gridService.Call("move_unit", state, unit_state, nextCoord).AsBool())
        {
            return false;
        }
        moved_this_step[unitId] = true;
        _append_affected_unit_id(affected_unit_ids, unit_state);
        _append_changed_coords(batch, previousCoords);
        _append_changed_unit_coords(batch, unit_state);
        _append_changed_unit_id(batch, unitId);
        return true;
    }

    public GDictionary _apply_ground_wind_push_effects(GodotObject source_unit, GodotObject skill_def, GArray wind_push_effects, GArray effect_coords, GArray target_coords, GodotObject batch)
    {
        var result = new GDictionary
        {
            ["applied"] = false,
            ["affected_unit_ids"] = new GArray(),
        };
        if (IsArrayEmpty(wind_push_effects) || source_unit == null)
        {
            return result;
        }
        GDictionary forcedMoveContext = _build_ground_forced_move_context(source_unit, target_coords);
        Vector2I direction = GdInterop.GetVector2I(forcedMoveContext, "direction", Vector2I.Zero);
        if (direction == Vector2I.Zero)
        {
            return result;
        }
        var affectedUnitIds = new GDictionary();
        foreach (Variant rawEffect in wind_push_effects)
        {
            GodotObject effectDef = rawEffect.AsGodotObject();
            if (effectDef == null)
            {
                continue;
            }
            GArray targetUnits = _collect_wind_push_target_units(source_unit, skill_def, effectDef, effect_coords, batch, result, affectedUnitIds);
            if (targetUnits.Count == 0)
            {
                continue;
            }
            int moveDistance = Math.Max(GdInterop.GetInt(effectDef, "forced_move_distance"), 0);
            for (int stepIndex = 0; stepIndex < moveDistance; stepIndex++)
            {
                var movedThisStep = new GDictionary();
                bool movedAny = false;
                GArray orderedUnits = _sort_wind_push_units_near_to_far(targetUnits, direction);
                foreach (Variant rawTarget in orderedUnits)
                {
                    GodotObject targetUnit = rawTarget.AsGodotObject();
                    if (targetUnit == null || !GdInterop.GetBool(targetUnit, "is_alive"))
                    {
                        continue;
                    }
                    if (movedThisStep.ContainsKey(GdInterop.GetStringName(targetUnit, "unit_id")))
                    {
                        continue;
                    }
                    if (_try_wind_push_unit_one_step(source_unit, skill_def, effectDef, targetUnit, direction, movedThisStep, affectedUnitIds, new GDictionary(), batch))
                    {
                        movedAny = true;
                        result["applied"] = true;
                    }
                }
                if (!movedAny)
                {
                    break;
                }
            }
        }
        result["affected_unit_ids"] = KeysArray(affectedUnitIds);
        return result;
    }

    public GDictionary _apply_ground_unit_effects(GodotObject source_unit, GodotObject skill_def, GArray effect_defs, GArray effect_coords, GodotObject batch, GArray target_coords)
    {
        bool applied = false;
        int totalDamage = 0;
        int totalHealing = 0;
        int totalKillCount = 0;
        var affectedUnitIds = new GDictionary();
        var shieldRollContext = new GDictionary();
        GDictionary forcedMoveContext = _build_ground_forced_move_context(source_unit, target_coords);
        GArray windPushEffects = _collect_wind_push_effects(effect_defs);
        GDictionary windPushEffectIds = _build_effect_instance_lookup(windPushEffects);

        foreach (Variant rawTarget in _collect_units_in_coords(effect_coords))
        {
            GodotObject targetUnit = rawTarget.AsGodotObject();
            if (targetUnit == null || !GdInterop.GetBool(targetUnit, "is_alive"))
            {
                continue;
            }
            var applicableEffects = new GArray();
            foreach (Variant rawEffect in effect_defs ?? new GArray())
            {
                GodotObject effectDef = rawEffect.AsGodotObject();
                if (effectDef == null || windPushEffectIds.ContainsKey(effectDef.GetInstanceId()))
                {
                    continue;
                }
                if (_is_unit_valid_for_effect(source_unit, targetUnit, _resolve_effect_target_filter(skill_def, effectDef)))
                {
                    applicableEffects.Add(effectDef);
                }
            }
            if (applicableEffects.Count == 0)
            {
                continue;
            }

            GodotObject layeredBarrierService = GetRuntimeObject("_layered_barrier_service");
            GDictionary barrierResult = layeredBarrierService != null
                ? ToDictionary(layeredBarrierService.Call("resolve_skill_barrier_interaction", source_unit, targetUnit, skill_def, applicableEffects, batch))
                : new GDictionary();
            if (GdInterop.GetBool(barrierResult, "blocked", false))
            {
                applied = applied || GdInterop.GetBool(barrierResult, "applied", false);
                if (GdInterop.GetBool(barrierResult, "applied", false))
                {
                    _append_affected_unit_id(affectedUnitIds, targetUnit);
                }
                continue;
            }

            GDictionary result = _resolve_ground_unit_effect_result(source_unit, targetUnit, skill_def, applicableEffects);
            GetRuntimeObject("_skill_mastery_service")?.Call("record_target_result", source_unit, targetUnit, skill_def, result, applicableEffects);
            GDictionary shieldResult = _apply_unit_shield_effects(source_unit, targetUnit, skill_def, applicableEffects, shieldRollContext);
            GDictionary specialResult = ToDictionary(_runtime.Call(
                "_apply_unit_skill_special_effects",
                source_unit,
                targetUnit,
                skill_def,
                default(Variant),
                applicableEffects,
                batch,
                forcedMoveContext));
            _record_vajra_body_mastery_from_incoming_damage(source_unit, targetUnit, skill_def, result, batch);
            mark_applied_statuses_for_turn_timing(targetUnit, GdInterop.GetArray(result, "status_effect_ids"));
            bool attackResolved = result.ContainsKey("attack_success");
            bool attackHit = attackResolved && GdInterop.GetBool(result, "attack_success", false);
            bool unitApplied = GdInterop.GetBool(result, "applied", false)
                || GdInterop.GetBool(shieldResult, "applied", false)
                || GdInterop.GetBool(specialResult, "applied", false)
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
            _append_affected_unit_id(affectedUnitIds, targetUnit);
            _append_changed_unit_id(batch, source_unit != null ? GdInterop.GetStringName(source_unit, "unit_id") : Empty);
            _append_changed_unit_id(batch, GdInterop.GetStringName(targetUnit, "unit_id"));
            _append_changed_unit_coords(batch, targetUnit);
            append_result_source_status_effects(batch, source_unit, result);

            int damage = GdInterop.GetInt(result, "damage", 0);
            int healing = GdInterop.GetInt(result, "healing", 0);
            totalDamage += damage;
            totalHealing += healing;
            append_damage_result_log_lines(batch, _build_skill_log_subject_label(source_unit, skill_def), DisplayName(targetUnit), result);
            if (attackResolved && !GdInterop.GetBool(result, "applied", false))
            {
                _append_result_report_entry(batch, result);
            }
            if (healing > 0)
            {
                AppendLog(batch, $"{_build_skill_log_subject_label(source_unit, skill_def)} 为 {DisplayName(targetUnit)} 恢复 {healing} 点生命。");
            }
            if (GdInterop.GetBool(shieldResult, "applied", false))
            {
                AppendLog(batch, $"{_build_skill_log_subject_label(source_unit, skill_def)} 使 {DisplayName(targetUnit)} 的护盾值变为 {GdInterop.GetInt(shieldResult, "current_shield_hp", 0)}。");
            }
            foreach (Variant statusId in GdInterop.GetArray(result, "status_effect_ids"))
            {
                AppendLog(batch, $"{DisplayName(targetUnit)} 获得状态 {statusId}。");
            }

            if (!GdInterop.GetBool(targetUnit, "is_alive"))
            {
                totalKillCount += 1;
                _apply_on_kill_gain_resources_effects(source_unit, targetUnit, skill_def, effect_defs, batch);
                _runtime.Call(
                    "handle_unit_defeated_by_runtime_effect",
                    targetUnit,
                    source_unit,
                    batch,
                    $"{DisplayName(targetUnit)} 被击倒。",
                    new GDictionary { ["record_enemy_defeated_achievement"] = true });
            }
            if (source_unit != null && targetUnit != null)
            {
                _record_effect_metrics(source_unit, targetUnit, damage, healing, GdInterop.GetBool(targetUnit, "is_alive") ? 0 : 1);
            }
        }

        GDictionary windPushResult = _apply_ground_wind_push_effects(source_unit, skill_def, windPushEffects, effect_coords, target_coords, batch);
        if (GdInterop.GetBool(windPushResult, "applied", false))
        {
            applied = true;
            _append_changed_unit_id(batch, source_unit != null ? GdInterop.GetStringName(source_unit, "unit_id") : Empty);
        }
        foreach (Variant affectedUnitId in GdInterop.GetArray(windPushResult, "affected_unit_ids"))
        {
            affectedUnitIds[affectedUnitId] = true;
        }

        _flush_last_stand_mastery_records(batch);
        if (applied && source_unit != null)
        {
            GetRuntimeObject("_battle_rating_system")?.Call("record_skill_effect_result", source_unit, totalDamage, totalHealing, totalKillCount);
        }
        return new GDictionary
        {
            ["applied"] = applied,
            ["affected_unit_count"] = affectedUnitIds.Count,
            ["damage"] = totalDamage,
            ["healing"] = totalHealing,
            ["kill_count"] = totalKillCount,
        };
    }

    public GDictionary _resolve_ground_unit_effect_result(GodotObject source_unit, GodotObject target_unit, GodotObject skill_def, GArray effect_defs)
    {
        if (_should_resolve_ground_effects_as_attack(effect_defs))
        {
            GArray attackEffectDefs = _dedupe_effect_defs_by_instance(effect_defs);
            GodotObject attackPolicy = _runtime.Call("get_attack_check_policy_service").AsGodotObject();
            Variant attackContext = attackPolicy.Call("build_attack_context", State, source_unit, target_unit, skill_def, new StringName("skill_attack_check"), new StringName("execute"));
            Variant attackCheck = attackPolicy.Call("build_attack_check", attackContext);
            return ToDictionary(GetRuntimeObject("_damage_resolver").Call(
                "resolve_attack_effects",
                source_unit,
                target_unit,
                attackEffectDefs,
                attackCheck,
                new GDictionary
                {
                    ["battle_state"] = State,
                    ["skill_id"] = skill_def != null ? GdInterop.GetStringName(skill_def, "skill_id") : Empty,
                }));
        }
        return ToDictionary(GetRuntimeObject("_damage_resolver").Call(
            "resolve_effects",
            source_unit,
            target_unit,
            effect_defs,
            new GDictionary { ["skill_id"] = skill_def != null ? GdInterop.GetStringName(skill_def, "skill_id") : Empty }));
    }

    public bool _should_resolve_ground_effects_as_attack(GArray effect_defs)
    {
        foreach (Variant rawEffect in effect_defs ?? new GArray())
        {
            GodotObject effectDef = rawEffect.AsGodotObject();
            if (effectDef == null)
            {
                continue;
            }
            if (GdInterop.GetBool(GdInterop.GetDictionary(effectDef, "params"), "resolve_as_weapon_attack", false))
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
        foreach (Variant rawEffect in effect_defs ?? new GArray())
        {
            GodotObject effectDef = rawEffect.AsGodotObject();
            if (effectDef != null && seen.Add(effectDef.GetInstanceId()))
            {
                deduped.Add(effectDef);
            }
        }
        return deduped;
    }

    public GDictionary _apply_ground_terrain_effects(GodotObject source_unit, GodotObject skill_def, GArray effect_defs, GArray effect_coords, GodotObject batch)
    {
        bool applied = false;
        bool requiresTopologyReconcile = false;
        GodotObject layeredBarrierService = GetRuntimeObject("_layered_barrier_service");
        foreach (Variant rawEffect in effect_defs ?? new GArray())
        {
            GodotObject effectDef = rawEffect.AsGodotObject();
            if (effectDef == null)
            {
                continue;
            }
            StringName effectType = GdInterop.GetStringName(effectDef, "effect_type");
            if (effectType == EffectTerrain || effectType == EffectTerrainReplace || effectType == EffectTerrainReplaceTo || effectType == EffectHeight || effectType == EffectHeightDelta)
            {
                requiresTopologyReconcile = true;
                foreach (Variant rawCoord in effect_coords ?? new GArray())
                {
                    Vector2I effectCoord = ToVector2I(rawCoord);
                    GDictionary barrierResult = layeredBarrierService != null
                        ? ToDictionary(layeredBarrierService.Call("resolve_ground_barrier_interaction", source_unit, effectCoord, skill_def, effect_defs, batch))
                        : new GDictionary();
                    if (GdInterop.GetBool(barrierResult, "blocked", false))
                    {
                        applied = applied || GdInterop.GetBool(barrierResult, "applied", false);
                        continue;
                    }
                    if (_apply_ground_cell_effect(source_unit, skill_def, effectCoord, effectDef, batch))
                    {
                        applied = true;
                    }
                }
            }
            else if (effectType == EffectTerrainEffect)
            {
                if (GdInterop.GetInt(effectDef, "duration_tu") > 0 && GdInterop.GetInt(effectDef, "tick_interval_tu") > 0)
                {
                    StringName fieldInstanceId = _build_terrain_effect_instance_id(GdInterop.GetStringName(effectDef, "terrain_effect_id"));
                    int appliedCoordCount = 0;
                    foreach (Variant rawCoord in effect_coords ?? new GArray())
                    {
                        Vector2I effectCoord = ToVector2I(rawCoord);
                        GDictionary barrierResult = layeredBarrierService != null
                            ? ToDictionary(layeredBarrierService.Call("resolve_ground_barrier_interaction", source_unit, effectCoord, skill_def, effect_defs, batch))
                            : new GDictionary();
                        if (GdInterop.GetBool(barrierResult, "blocked", false))
                        {
                            applied = applied || GdInterop.GetBool(barrierResult, "applied", false);
                            continue;
                        }
                        if (GetRuntimeObject("_terrain_effect_system").Call("upsert_timed_terrain_effect", effectCoord, source_unit, skill_def, effectDef, fieldInstanceId).AsBool())
                        {
                            applied = true;
                            appliedCoordCount += 1;
                            _append_changed_coord(batch, effectCoord);
                        }
                    }
                    if (appliedCoordCount > 0)
                    {
                        AppendLog(batch, $"{_build_skill_log_subject_label(source_unit, skill_def)} 在 {appliedCoordCount} 个地格留下 {_get_terrain_effect_display_name(effectDef)}。");
                    }
                }
                else if (!GdInterop.IsEmpty(GdInterop.GetStringName(effectDef, "terrain_effect_id")))
                {
                    int taggedCoordCount = 0;
                    foreach (Variant rawCoord in effect_coords ?? new GArray())
                    {
                        Vector2I effectCoord = ToVector2I(rawCoord);
                        GDictionary barrierResult = layeredBarrierService != null
                            ? ToDictionary(layeredBarrierService.Call("resolve_ground_barrier_interaction", source_unit, effectCoord, skill_def, effect_defs, batch))
                            : new GDictionary();
                        if (GdInterop.GetBool(barrierResult, "blocked", false))
                        {
                            applied = applied || GdInterop.GetBool(barrierResult, "applied", false);
                            continue;
                        }
                        GodotObject cell = GridService.Call("get_cell", State, effectCoord).AsGodotObject();
                        if (cell == null)
                        {
                            continue;
                        }
                        GArray terrainEffectIds = GdInterop.GetArray(cell, "terrain_effect_ids");
                        StringName terrainEffectId = GdInterop.GetStringName(effectDef, "terrain_effect_id");
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
                        AppendLog(batch, $"{_build_skill_log_subject_label(source_unit, skill_def)} 使 {taggedCoordCount} 个地格附加效果 {_get_terrain_effect_display_name(effectDef)}。");
                    }
                }
            }
            else if (effectType == EffectEdgeClear)
            {
                if (_apply_ground_edge_clear_effect(source_unit, skill_def, effect_coords, effectDef, batch))
                {
                    applied = true;
                }
            }
        }
        if (requiresTopologyReconcile && _reconcile_water_topology(effect_coords, batch))
        {
            applied = true;
        }
        return new GDictionary { ["applied"] = applied };
    }

    public bool _apply_ground_edge_clear_effect(GodotObject source_unit, GodotObject skill_def, GArray effect_coords, GodotObject effect_def, GodotObject batch)
    {
        if (_runtime == null || State == null || effect_coords == null || effect_coords.Count < 2)
        {
            return false;
        }
        GArray edgeCoords = _sort_coords(effect_coords);
        Vector2I first = ToVector2I(edgeCoords[0]);
        Vector2I second = ToVector2I(edgeCoords[1]);
        if (GridService.Call("get_distance", first, second).AsInt32() != 1)
        {
            return false;
        }
        var barrierEffectDefs = new GArray { effect_def };
        GodotObject layeredBarrierService = GetRuntimeObject("_layered_barrier_service");
        foreach (Vector2I barrierCoord in new[] { first, second })
        {
            GDictionary barrierResult = layeredBarrierService != null
                ? ToDictionary(layeredBarrierService.Call("resolve_ground_barrier_interaction", source_unit, barrierCoord, skill_def, barrierEffectDefs, batch))
                : new GDictionary();
            if (GdInterop.GetBool(barrierResult, "blocked", false))
            {
                return GdInterop.GetBool(barrierResult, "applied", false);
            }
        }
        GDictionary edgeRef = _get_edge_authoring_reference(first, second);
        if (edgeRef.Count == 0)
        {
            return false;
        }
        Vector2I edgeCoord = GdInterop.GetVector2I(edgeRef, "coord", new Vector2I(-1, -1));
        Vector2I edgeDirection = GdInterop.GetVector2I(edgeRef, "direction", Vector2I.Zero);
        GodotObject cell = GridService.Call("get_cell", State, edgeCoord).AsGodotObject();
        if (cell == null)
        {
            return false;
        }
        GodotObject featureState = cell.Call("get_edge_feature", edgeDirection).AsGodotObject();
        if (featureState == null || featureState.Call("is_empty").AsBool())
        {
            return false;
        }
        if (!_can_edge_clear_remove_feature(effect_def, featureState))
        {
            return false;
        }
        if (!(GdInterop.GetBool(featureState, "blocks_move") || GdInterop.GetBool(featureState, "blocks_occupancy") || GdInterop.GetBool(featureState, "blocks_los")))
        {
            return false;
        }
        if (!GridService.Call("clear_edge_feature", State, edgeCoord, edgeDirection).AsBool())
        {
            return false;
        }
        _append_changed_coord(batch, first);
        _append_changed_coord(batch, second);
        AppendLog(batch, $"{_build_skill_log_subject_label(source_unit, skill_def)} 在 ({first.X}, {first.Y}) 与 ({second.X}, {second.Y}) 之间开辟通道，移除了{_get_edge_feature_display_name(featureState)}。");
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

    public bool _can_edge_clear_remove_feature(GodotObject effect_def, GodotObject feature_state)
    {
        return _get_edge_clear_feature_kinds(effect_def).ContainsKey(GdInterop.GetStringName(feature_state, "feature_kind"));
    }

    public GDictionary _get_edge_clear_feature_kinds(GodotObject effect_def)
    {
        var allowed = new GDictionary();
        GDictionary parameters = GdInterop.GetDictionary(effect_def, "params");
        Variant rawKinds = GdInterop.TryGet(parameters, "clear_feature_kinds", out Variant value) ? value : new GArray();
        if (rawKinds.VariantType == Variant.Type.Array)
        {
            foreach (Variant rawKind in rawKinds.AsGodotArray())
            {
                if (rawKind.VariantType == Variant.Type.String || rawKind.VariantType == Variant.Type.StringName)
                {
                    StringName kind = ToStringName(rawKind);
                    if (!GdInterop.IsEmpty(kind))
                    {
                        allowed[kind] = true;
                    }
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

    public string _get_edge_feature_display_name(GodotObject feature_state)
    {
        if (feature_state == null)
        {
            return "阻挡边界";
        }
        StringName featureKind = GdInterop.GetStringName(feature_state, "feature_kind");
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

    public bool _apply_ground_cell_effect(GodotObject source_unit, GodotObject skill_def, Vector2I target_coord, GodotObject effect_def, GodotObject batch)
    {
        GodotObject cell = GridService.Call("get_cell", State, target_coord).AsGodotObject();
        if (cell == null)
        {
            return false;
        }
        bool cellApplied = false;
        StringName beforeTerrain = GdInterop.GetStringName(cell, "base_terrain");
        int beforeHeight = GdInterop.GetInt(cell, "current_height");
        StringName occupantUnitId = GdInterop.GetStringName(cell, "occupant_unit_id");
        GodotObject occupantUnit = !GdInterop.IsEmpty(occupantUnitId) ? GdInterop.GetObject(GdInterop.GetDictionary(State, "units"), occupantUnitId) : null;
        StringName effectType = GdInterop.GetStringName(effect_def, "effect_type");
        if (effectType == EffectTerrain || effectType == EffectTerrainReplace || effectType == EffectTerrainReplaceTo)
        {
            StringName terrainReplaceTo = GdInterop.GetStringName(effect_def, "terrain_replace_to");
            if (!GdInterop.IsEmpty(terrainReplaceTo) && GdInterop.GetStringName(cell, "base_terrain") != terrainReplaceTo)
            {
                if (GridService.Call("set_base_terrain", State, target_coord, terrainReplaceTo).AsBool())
                {
                    cellApplied = true;
                }
            }
        }
        else if ((effectType == EffectHeight || effectType == EffectHeightDelta) && GdInterop.GetInt(effect_def, "height_delta") != 0)
        {
            GDictionary heightResult = ToDictionary(GridService.Call("apply_height_delta_result", State, target_coord, GdInterop.GetInt(effect_def, "height_delta")));
            if (GdInterop.GetBool(heightResult, "changed", false))
            {
                cellApplied = true;
            }
        }

        int afterHeight = GdInterop.GetInt(cell, "current_height");
        if (beforeTerrain != GdInterop.GetStringName(cell, "base_terrain") || beforeHeight != afterHeight)
        {
            _append_changed_coord(batch, target_coord);
        }
        if (beforeTerrain != GdInterop.GetStringName(cell, "base_terrain"))
        {
            AppendLog(batch, $"{_build_skill_log_subject_label(source_unit, skill_def)} 使 ({target_coord.X}, {target_coord.Y}) 的地形由 {GridService.Call("get_terrain_display_name", beforeTerrain.ToString()).AsString()} 变为 {GridService.Call("get_terrain_display_name", GdInterop.GetStringName(cell, "base_terrain").ToString()).AsString()}。");
        }
        if (beforeHeight != afterHeight)
        {
            AppendLog(batch, $"{_build_skill_log_subject_label(source_unit, skill_def)} 使 ({target_coord.X}, {target_coord.Y}) 的高度由 {beforeHeight} 变为 {afterHeight}。");
        }

        if (occupantUnit != null && GdInterop.GetBool(occupantUnit, "is_alive") && afterHeight < beforeHeight)
        {
            int fallLayers = beforeHeight - afterHeight;
            GDictionary fallResult = ToDictionary(GetRuntimeObject("_damage_resolver").Call("resolve_fall_damage", occupantUnit, fallLayers));
            int fallDamage = GdInterop.GetInt(fallResult, "damage", 0);
            int shieldAbsorbed = GdInterop.GetInt(fallResult, "shield_absorbed", 0);
            if (fallDamage > 0 || shieldAbsorbed > 0)
            {
                cellApplied = true;
                _append_changed_coord(batch, target_coord);
                _append_changed_unit_id(batch, GdInterop.GetStringName(occupantUnit, "unit_id"));
                if (fallDamage > 0)
                {
                    AppendLog(batch, $"{_build_skill_log_subject_label(source_unit, skill_def)} 使 ({target_coord.X}, {target_coord.Y}) 的高度下降 {fallLayers} 层，导致 {DisplayName(occupantUnit)} 坠落并受到 {fallDamage} 点伤害。");
                    if (shieldAbsorbed > 0)
                    {
                        AppendLog(batch, $"{DisplayName(occupantUnit)} 的护盾吸收了 {shieldAbsorbed} 点坠落伤害。");
                    }
                }
                else
                {
                    AppendLog(batch, $"{_build_skill_log_subject_label(source_unit, skill_def)} 使 ({target_coord.X}, {target_coord.Y}) 的高度下降 {fallLayers} 层，导致 {DisplayName(occupantUnit)} 坠落，但被护盾吸收了 {shieldAbsorbed} 点坠落伤害。");
                }
                if (GdInterop.GetBool(fallResult, "shield_broken", false))
                {
                    AppendLog(batch, $"{DisplayName(occupantUnit)} 的护盾被击碎。");
                }
                if (!GdInterop.GetBool(occupantUnit, "is_alive"))
                {
                    _runtime.Call(
                        "handle_unit_defeated_by_runtime_effect",
                        occupantUnit,
                        source_unit,
                        batch,
                        $"{DisplayName(occupantUnit)} 被击倒。",
                        new GDictionary { ["record_enemy_defeated_achievement"] = true });
                }
            }
        }
        _flush_last_stand_mastery_records(batch);
        return cellApplied;
    }

    public bool _reconcile_water_topology(GArray effect_coords, GodotObject batch)
    {
        GodotObject state = State;
        if (state == null || GdInterop.GetVector2I(state, "map_size") == Vector2I.Zero || IsArrayEmpty(effect_coords))
        {
            return false;
        }
        GArray changes = ToArray(GetRuntimeObject("_terrain_topology_service").Call(
            "reclassify_water_terrain_near_coords",
            GdInterop.GetDictionary(state, "cells"),
            GdInterop.GetVector2I(state, "map_size"),
            effect_coords));
        bool applied = false;
        foreach (Variant rawChange in changes)
        {
            GDictionary change = rawChange.AsGodotDictionary();
            Vector2I coord = GdInterop.GetVector2I(change, "coord", Vector2I.Zero);
            GodotObject cell = GridService.Call("get_cell", state, coord).AsGodotObject();
            if (cell == null)
            {
                continue;
            }
            StringName beforeTerrain = GdInterop.GetStringName(cell, "base_terrain");
            Vector2I beforeFlowDirection = GdInterop.GetVector2I(cell, "flow_direction");
            StringName afterTerrain = GdInterop.GetStringName(change, "after_terrain", beforeTerrain);
            Vector2I afterFlowDirection = GdInterop.GetVector2I(change, "after_flow_direction", beforeFlowDirection);
            if (beforeTerrain != afterTerrain)
            {
                GridService.Call("set_base_terrain", state, coord, afterTerrain);
                cell = GridService.Call("get_cell", state, coord).AsGodotObject();
                if (cell == null)
                {
                    continue;
                }
            }
            if (GdInterop.GetVector2I(cell, "flow_direction") != afterFlowDirection)
            {
                cell.Set("flow_direction", afterFlowDirection);
                GridService.Call("recalculate_cell", cell);
                GridService.Call("sync_column_from_surface_cell", state, coord);
            }
            if (beforeTerrain != GdInterop.GetStringName(cell, "base_terrain") || beforeFlowDirection != GdInterop.GetVector2I(cell, "flow_direction"))
            {
                applied = true;
                _append_changed_coord(batch, coord);
            }
            if (beforeTerrain != GdInterop.GetStringName(cell, "base_terrain"))
            {
                AppendLog(batch, $"相邻水域在 ({coord.X}, {coord.Y}) 重分类为 {GridService.Call("get_terrain_display_name", GdInterop.GetStringName(cell, "base_terrain").ToString()).AsString()}。");
            }
        }
        return applied;
    }

    public string _get_ground_special_effect_validation_message(GodotObject active_unit, GodotObject skill_def, GodotObject cast_variant, GArray target_coords)
    {
        GodotObject relocationEffectDef = _get_ground_relocation_effect_def(skill_def, cast_variant);
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
        return _can_use_ground_relocation(active_unit, ToVector2I(target_coords[0]), relocationEffectDef)
            ? ""
            : "目标地格无法作为位移落点。";
    }

    public GDictionary _validate_ground_skill_command(GodotObject active_unit, GodotObject skill_def, GodotObject cast_variant, BattleCommand command)
    {
        var normalizedCoords = _normalize_target_coords(command);
        var result = new GDictionary
        {
            ["allowed"] = false,
            ["message"] = "地面技能目标无效。",
            ["target_coords"] = normalizedCoords,
        };
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (State == null || active_unit == null || skill_def == null || combatProfile == null || cast_variant == null)
        {
            return result;
        }
        if (GdInterop.GetStringName(cast_variant, "target_mode") != GroundTargetMode)
        {
            result["message"] = "该技能形态不是地面施法。";
            return result;
        }
        string blockReason = _get_skill_cast_block_reason(active_unit, skill_def);
        if (!string.IsNullOrEmpty(blockReason))
        {
            result["message"] = blockReason;
            return result;
        }
        if (normalizedCoords.Count != GdInterop.GetInt(cast_variant, "required_coord_count"))
        {
            result["message"] = $"该技能形态需要选择 {GdInterop.GetInt(cast_variant, "required_coord_count")} 个地格。";
            return result;
        }
        GodotObject chargeResolver = GetRuntimeObject("_charge_resolver");
        if (chargeResolver != null && chargeResolver.Call("is_charge_variant", cast_variant).AsBool())
        {
            return ToDictionary(chargeResolver.Call("validate_charge_command", active_unit, skill_def, cast_variant, normalizedCoords, result));
        }

        GodotObject relocationEffectDef = _get_ground_relocation_effect_def(skill_def, cast_variant);
        int effectiveSkillRange = _get_effective_skill_range(active_unit, skill_def);
        var seenCoords = new HashSet<Vector2I>();
        foreach (Variant rawCoord in normalizedCoords)
        {
            Vector2I coord = ToVector2I(rawCoord);
            if (!seenCoords.Add(coord))
            {
                result["message"] = "同一地格不能重复选择。";
                return result;
            }
            if (!GridService.Call("is_inside", State, coord).AsBool())
            {
                result["message"] = "存在超出战场范围的目标地格。";
                return result;
            }
            int targetDistance = relocationEffectDef != null
                ? GridService.Call("get_chebyshev_distance", GdInterop.GetVector2I(active_unit, "coord"), coord).AsInt32()
                : GridService.Call("get_distance_from_unit_to_coord", active_unit, coord).AsInt32();
            if (targetDistance > effectiveSkillRange)
            {
                result["message"] = "目标地格超出技能施放距离。";
                return result;
            }
            GodotObject cell = GridService.Call("get_cell", State, coord).AsGodotObject();
            if (cell == null)
            {
                result["message"] = "目标地格数据不可用。";
                return result;
            }
            GArray allowedBaseTerrains = GdInterop.GetArray(cast_variant, "allowed_base_terrains");
            if (allowedBaseTerrains.Count > 0)
            {
                bool normalizedAllowed = false;
                StringName normalizedCellTerrain = BattleTerrainRules.normalize_terrain_id(GdInterop.GetStringName(cell, "base_terrain"));
                foreach (Variant rawAllowedTerrain in allowedBaseTerrains)
                {
                    if (BattleTerrainRules.normalize_terrain_id(ToStringName(rawAllowedTerrain)) == normalizedCellTerrain)
                    {
                        normalizedAllowed = true;
                        break;
                    }
                }
                if (!normalizedAllowed)
                {
                    result["message"] = "目标地格地形不符合该技能形态的要求。";
                    return result;
                }
            }
            if (_is_crown_break_skill(GdInterop.GetStringName(skill_def, "skill_id")))
            {
                GodotObject targetUnit = GridService.Call("get_unit_at_coord", State, coord).AsGodotObject();
                if (!_is_crown_break_target_eligible(active_unit, targetUnit))
                {
                    result["message"] = "折冠只能对已被黑星烙印的 elite / boss 施放。";
                    return result;
                }
            }
        }
        if (!_validate_target_coords_shape(GdInterop.GetStringName(cast_variant, "footprint_pattern"), normalizedCoords))
        {
            result["message"] = "目标地格排布不符合该技能形态。";
            return result;
        }
        GArray sortedTargetCoords = _sort_coords(normalizedCoords);
        string specialValidationMessage = _get_ground_special_effect_validation_message(active_unit, skill_def, cast_variant, sortedTargetCoords);
        if (!string.IsNullOrEmpty(specialValidationMessage))
        {
            result["message"] = specialValidationMessage;
            return result;
        }
        result["target_coords"] = sortedTargetCoords;
        result["allowed"] = true;
        result["message"] = "可施放。";
        return result;
    }

    public bool _validate_target_coords_shape(StringName footprint_pattern, GArray target_coords)
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
            Vector2I first = ToVector2I(target_coords[0]);
            Vector2I second = ToVector2I(target_coords[1]);
            return (first.X == second.X && Math.Abs(first.Y - second.Y) == 1)
                || (first.Y == second.Y && Math.Abs(first.X - second.X) == 1);
        }
        if (footprint_pattern == FootprintSquare2)
        {
            if (target_coords == null || target_coords.Count != 4)
            {
                return false;
            }
            Vector2I firstCoord = ToVector2I(target_coords[0]);
            int minX = firstCoord.X;
            int maxX = firstCoord.X;
            int minY = firstCoord.Y;
            int maxY = firstCoord.Y;
            var coordSet = new HashSet<Vector2I>();
            foreach (Variant rawCoord in target_coords)
            {
                Vector2I coord = ToVector2I(rawCoord);
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
        if (_runtime == null)
        {
            return Empty;
        }
        int nonce = GdInterop.GetInt(_runtime, "_terrain_effect_nonce") + 1;
        _runtime.Set("_terrain_effect_nonce", nonce);
        GodotObject state = State;
        GodotObject timeline = GdInterop.GetObject(state, "timeline");
        int currentTu = timeline != null ? GdInterop.GetInt(timeline, "current_tu") : 0;
        return new StringName($"{effect_id}_{currentTu}_{nonce}");
    }

    public string _get_terrain_effect_display_name(GodotObject effect_def)
    {
        GDictionary parameters = GdInterop.GetDictionary(effect_def, "params");
        if (effect_def != null && parameters.ContainsKey("display_name"))
        {
            return GdInterop.GetString(parameters, "display_name", "");
        }
        return effect_def != null ? GdInterop.GetStringName(effect_def, "terrain_effect_id").ToString() : "地格效果";
    }

    private GodotObject State => GetRuntimeObject("_state");
    private GodotObject GridService => GetRuntimeObject("_grid_service");
    private GodotObject TargetCollectionService => GetRuntimeObject("_target_collection_service");
    private GodotObject SkillResolutionRules => GetRuntimeObject("_skill_resolution_rules");

    private GodotObject GetRuntimeObject(string property)
    {
        return GdInterop.GetObject(_runtime, property);
    }

    private static bool IsArrayEmpty(GArray array)
    {
        return array == null || array.Count == 0;
    }

    private static GDictionary ToDictionary(Variant value)
    {
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

    private static GArray ToArray(Variant value)
    {
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static Vector2I ToVector2I(Variant value)
    {
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : Vector2I.Zero;
    }

    private static StringName ToStringName(Variant value)
    {
        return value.VariantType == Variant.Type.StringName ? value.AsStringName() : new StringName(value.ToString());
    }

    private static GArray KeysArray(GDictionary dictionary)
    {
        var keys = new GArray();
        foreach (Variant key in dictionary.Keys)
        {
            keys.Add(key);
        }
        return keys;
    }

    private static void AppendLog(GodotObject batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
        {
            return;
        }
        GdInterop.GetArray(batch, "log_lines").Add(line);
    }

    private static string DisplayName(GodotObject value)
    {
        return GdInterop.GetString(value, "display_name");
    }

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GodotObject target) || !GodotObject.IsInstanceValid(target))
        {
            return null;
        }
        return target;
    }
}
