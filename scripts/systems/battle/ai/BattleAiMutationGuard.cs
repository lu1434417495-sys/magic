using Godot;
using static GdInterop;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiMutationGuard : RefCounted
{
    private const int MaxReportedViolations = 64;

    private static readonly GDictionary AllowedActiveUnitFields = new()
    {
        ["ai_brain_id"] = true,
        ["ai_state_id"] = true,
    };

    private static readonly GDictionary AllowedActiveBlackboardKeys = new()
    {
        ["last_brain_id"] = true,
        ["last_state_id"] = true,
        ["last_action_id"] = true,
        ["last_reason_text"] = true,
        ["last_transition_previous_state_id"] = true,
        ["last_transition_state_id"] = true,
        ["last_transition_rule_id"] = true,
        ["last_transition_reason"] = true,
        ["turn_decision_count"] = true,
    };

    private static readonly string[] StateFieldNames =
    {
        "battle_id",
        "seed",
        "attack_roll_nonce",
        "phase",
        "map_size",
        "world_coord",
        "encounter_anchor_id",
        "terrain_profile_id",
        "attack_disadvantage_tags",
        "ally_unit_ids",
        "enemy_unit_ids",
        "active_unit_id",
        "winner_faction_id",
        "log_entries",
        "report_entries",
        "promotion_queue",
        "modal_state",
        "layered_barrier_fields",
    };

    private static readonly string[] UnitFieldNames =
    {
        "unit_id",
        "source_member_id",
        "enemy_template_id",
        "display_name",
        "faction_id",
        "control_mode",
        "ai_brain_id",
        "ai_state_id",
        "ai_blackboard",
        "coord",
        "body_size",
        "body_size_category",
        "footprint_size",
        "occupied_coords",
        "is_alive",
        "current_hp",
        "current_mp",
        "current_stamina",
        "current_aura",
        "current_ap",
        "current_move_points",
        "unlocked_combat_resource_ids",
        "stamina_recovery_progress",
        "is_resting",
        "has_taken_action_this_turn",
        "has_moved_this_turn",
        "can_use_locked_move_points_this_turn",
        "current_shield_hp",
        "shield_max_hp",
        "shield_duration",
        "shield_family",
        "shield_source_unit_id",
        "shield_source_skill_id",
        "shield_params",
        "action_progress",
        "action_threshold",
        "known_active_skill_ids",
        "known_skill_level_map",
        "known_skill_lock_hit_bonus_map",
        "movement_tags",
        "vision_tags",
        "proficiency_tags",
        "save_advantage_tags",
        "damage_resistances",
        "race_trait_ids",
        "subrace_trait_ids",
        "ascension_trait_ids",
        "bloodline_trait_ids",
        "versatility_pick",
        "weapon_profile_kind",
        "weapon_item_id",
        "weapon_profile_type_id",
        "weapon_family",
        "weapon_current_grip",
        "weapon_attack_range",
        "weapon_one_handed_dice",
        "weapon_two_handed_dice",
        "weapon_is_versatile",
        "weapon_uses_two_hands",
        "weapon_physical_damage_tag",
        "cooldowns",
        "last_turn_tu",
        "combo_state",
        "per_battle_charges",
        "per_turn_charges",
        "per_turn_charge_limits",
        "fumble_protection_used",
    };

    private GDictionary _before_raw = new();
    private GDictionary _before_stable = new();
    private StringName _active_unit_id = "";

    public bool capture(GodotObject context)
    {
        if (!TryGetContextState(context, out _, out BattleUnitState unitState))
        {
            return false;
        }

        _active_unit_id = unitState.unit_id;
        _before_raw = CaptureRawSnapshot(context);
        _before_stable = ToStableDictionary(_before_raw);
        return true;
    }

    public GArray validate_and_restore(GodotObject context)
    {
        if (_before_raw.Count == 0 || !TryGetContextState(context, out _, out _))
        {
            return new GArray();
        }

        GDictionary afterRaw = CaptureRawSnapshot(context);
        GDictionary afterStable = ToStableDictionary(afterRaw);
        GDictionary expectedStable = _before_stable.Duplicate(true);
        ApplyAllowedAiBookkeeping(expectedStable, afterStable);

        GArray violations = new();
        CollectDiffs(expectedStable, afterStable, "ai_decision", violations);
        if (violations.Count == 0)
        {
            return new GArray();
        }
        if (violations.Count >= MaxReportedViolations)
        {
            violations.Add($"(report capped at {MaxReportedViolations} violations; additional differences may exist)");
        }

        RestoreRawSnapshot(context, _before_raw);
        return violations;
    }

    private static GDictionary CaptureRawSnapshot(GodotObject context)
    {
        BattleState state = GetObject(context, "state") as BattleState;
        return new GDictionary
        {
            ["state_fields"] = CaptureFieldMap(state, StateFieldNames),
            ["timeline"] = CaptureTimeline(state?.timeline),
            ["party_backpack_view"] = CloneRestoreValue(Variant.From(state?.party_backpack_view)),
            ["cells"] = CloneCellDict(state?.cells ?? new GDictionary()),
            ["cell_columns"] = BattleCellState.clone_columns(state?.cell_columns ?? new GDictionary()),
            ["units"] = CaptureUnits(state?.units ?? new GDictionary()),
            ["skill_defs"] = GetDictionary(context, "skill_defs").Duplicate(),
        };
    }

    private static GDictionary CaptureFieldMap(GodotObject source, string[] fieldNames)
    {
        GDictionary result = new();
        if (source == null)
        {
            return result;
        }

        foreach (string fieldName in fieldNames)
        {
            result[fieldName] = CloneRestoreValue(source.Get(fieldName));
        }
        return result;
    }

    private static GDictionary CaptureTimeline(BattleTimelineState timeline)
    {
        if (timeline == null)
        {
            return new GDictionary();
        }
        return timeline.to_dict();
    }

    private static GDictionary CloneCellDict(GDictionary cells)
    {
        GDictionary result = new();
        foreach (Variant coord in cells.Keys)
        {
            Variant cell = cells[coord];
            BattleCellState cellState = cell.VariantType == Variant.Type.Object
                ? cell.AsGodotObject() as BattleCellState
                : null;
            result[coord] = cellState != null ? cellState.duplicate_cell() : CloneRestoreValue(cell);
        }
        return result;
    }

    private static GDictionary CaptureUnits(GDictionary units)
    {
        GDictionary result = new();
        foreach (Variant unitId in units.Keys)
        {
            BattleUnitState unit = units[unitId].AsGodotObject() as BattleUnitState;
            if (unit == null)
            {
                continue;
            }
            MaterializeLazyStatusEffects(unit);
            result[unitId] = new GDictionary
            {
                ["unit_ref"] = unit,
                ["fields"] = CaptureFieldMap(unit, UnitFieldNames),
                ["attribute_snapshot_values"] = CaptureAttributeSnapshotValues(unit.attribute_snapshot),
                ["equipment_view"] = CloneRestoreValue(Variant.From(unit.equipment_view)),
                ["status_effects"] = CloneStatusEffects(unit.status_effects),
            };
        }
        return result;
    }

    private static void MaterializeLazyStatusEffects(BattleUnitState unit)
    {
        if (unit == null || unit.status_effects == null)
        {
            return;
        }

        GArray staleKeys = new();
        foreach (Variant statusId in unit.status_effects.Keys)
        {
            Variant effect = unit.status_effects[statusId];
            if (effect.VariantType == Variant.Type.Object && effect.AsGodotObject() is BattleStatusEffectState)
            {
                continue;
            }

            BattleStatusEffectState effectState = BattleStatusEffectState.from_dict(effect);
            if (effectState == null || effectState.is_empty())
            {
                staleKeys.Add(statusId);
            }
            else
            {
                unit.status_effects[statusId] = effectState;
            }
        }

        foreach (Variant staleKey in staleKeys)
        {
            unit.status_effects.Remove(staleKey);
        }
    }

    private static GDictionary CloneStatusEffects(GDictionary statusEffects)
    {
        GDictionary result = new();
        foreach (Variant statusId in statusEffects.Keys)
        {
            Variant effect = statusEffects[statusId];
            if (effect.VariantType == Variant.Type.Object && effect.AsGodotObject() is BattleStatusEffectState effectState)
            {
                result[statusId] = effectState.duplicate_state();
            }
            else
            {
                result[statusId] = CloneRestoreValue(effect);
            }
        }
        return result;
    }

    private static Variant CloneRestoreValue(Variant value)
    {
        if (value.VariantType == Variant.Type.Nil)
        {
            return default;
        }
        if (value.VariantType == Variant.Type.Dictionary)
        {
            return value.AsGodotDictionary().Duplicate(true);
        }
        if (value.VariantType == Variant.Type.Array)
        {
            return value.AsGodotArray().Duplicate(true);
        }
        if (value.VariantType == Variant.Type.Object)
        {
            GodotObject obj = value.AsGodotObject();
            if (obj == null)
            {
                return default;
            }
            if (obj.HasMethod("duplicate_state"))
            {
                return obj.Call("duplicate_state");
            }
            if (obj is Resource resource)
            {
                return resource.Duplicate(true);
            }
            GD.PushWarning($"BattleAiMutationGuard cannot deep-clone Object of type {obj.GetClass()}; mutation detection on its internals may misfire.");
        }
        return value;
    }

    private static void RestoreRawSnapshot(GodotObject context, GDictionary snapshot)
    {
        BattleState state = GetObject(context, "state") as BattleState;
        if (state == null)
        {
            return;
        }

        RestoreFieldMap(state, GetDictionary(snapshot, "state_fields"));
        RestoreTimeline(state, GetDictionary(snapshot, "timeline"));
        if (snapshot.ContainsKey("party_backpack_view"))
        {
            Variant restoredBackpack = CloneRestoreValue(snapshot["party_backpack_view"]);
            state.party_backpack_view = restoredBackpack.VariantType == Variant.Type.Object
                ? restoredBackpack.AsGodotObject() as WarehouseState
                : null;
        }
        state.cells = CloneCellDict(GetDictionary(snapshot, "cells"));
        state.cell_columns = BattleCellState.clone_columns(GetDictionary(snapshot, "cell_columns"));
        state.units = RestoreUnits(GetDictionary(snapshot, "units"));
        context.Set("skill_defs", GetDictionary(snapshot, "skill_defs").Duplicate());
    }

    private static void RestoreFieldMap(GodotObject target, GDictionary fields)
    {
        if (target == null)
        {
            return;
        }
        foreach (Variant fieldName in fields.Keys)
        {
            target.Set(fieldName.AsString(), CloneRestoreValue(fields[fieldName]));
        }
    }

    private static void RestoreTimeline(BattleState state, GDictionary timelineSnapshot)
    {
        if (timelineSnapshot.Count == 0)
        {
            state.timeline = null;
            return;
        }

        BattleTimelineState rebuilt = BattleTimelineState.from_dict(timelineSnapshot);
        if (rebuilt != null)
        {
            state.timeline = rebuilt;
            return;
        }

        BattleTimelineState timeline = state.timeline ?? new BattleTimelineState();
        timeline.current_tu = GetInt(timelineSnapshot, "current_tu", 0);
        timeline.tu_per_tick = GetInt(timelineSnapshot, "tu_per_tick", BattleTimelineState.TU_GRANULARITY());
        timeline.frozen = GetBool(timelineSnapshot, "frozen", false);
        Godot.Collections.Array<StringName> readyUnitIds = new();
        foreach (Variant rawUnitId in GetArray(timelineSnapshot, "ready_unit_ids"))
        {
            readyUnitIds.Add(ToStringName(rawUnitId));
        }
        timeline.ready_unit_ids = readyUnitIds;
        state.timeline = timeline;
    }

    private static GDictionary RestoreUnits(GDictionary unitSnapshots)
    {
        GDictionary restoredUnits = new();
        foreach (Variant unitId in unitSnapshots.Keys)
        {
            GDictionary unitSnapshot = GetDictionary(unitSnapshots, unitId);
            BattleUnitState unit = GetObject(unitSnapshot, "unit_ref") as BattleUnitState;
            if (unit == null)
            {
                continue;
            }

            RestoreFieldMap(unit, GetDictionary(unitSnapshot, "fields"));
            AttributeSnapshot attributeSnapshot = new();
            GDictionary values = GetDictionary(unitSnapshot, "attribute_snapshot_values");
            foreach (Variant rawAttributeId in values.Keys)
            {
                attributeSnapshot.set_value(ToStringName(rawAttributeId), values[rawAttributeId].AsInt32());
            }
            unit.attribute_snapshot = attributeSnapshot;
            Variant equipmentView = CloneRestoreValue(unitSnapshot["equipment_view"]);
            unit.equipment_view = equipmentView.VariantType == Variant.Type.Object ? equipmentView.AsGodotObject() : null;
            unit.status_effects = CloneStatusEffects(GetDictionary(unitSnapshot, "status_effects"));
            restoredUnits[unitId] = unit;
        }
        return restoredUnits;
    }

    private static GDictionary CaptureAttributeSnapshotValues(GodotObject attributeSnapshot)
    {
        if (attributeSnapshot == null || !attributeSnapshot.HasMethod("get_all_values"))
        {
            return new GDictionary();
        }
        Variant values = attributeSnapshot.Call("get_all_values");
        return values.VariantType == Variant.Type.Dictionary ? values.AsGodotDictionary() : new GDictionary();
    }

    private void ApplyAllowedAiBookkeeping(GDictionary expectedStable, GDictionary afterStable)
    {
        GDictionary expectedUnits = GetDictionary(expectedStable, "units");
        GDictionary afterUnits = GetDictionary(afterStable, "units");
        string activeKey = StableKey(Variant.From(_active_unit_id));
        if (!expectedUnits.ContainsKey(activeKey) || !afterUnits.ContainsKey(activeKey))
        {
            return;
        }

        GDictionary expectedUnit = GetDictionary(expectedUnits, activeKey);
        GDictionary afterUnit = GetDictionary(afterUnits, activeKey);
        GDictionary expectedFields = GetDictionary(expectedUnit, "fields");
        GDictionary afterFields = GetDictionary(afterUnit, "fields");
        foreach (Variant fieldName in AllowedActiveUnitFields.Keys)
        {
            if (afterFields.ContainsKey(fieldName))
            {
                expectedFields[fieldName] = afterFields[fieldName];
            }
        }

        GDictionary expectedBlackboard = GetDictionary(expectedFields, "ai_blackboard");
        GDictionary afterBlackboard = GetDictionary(afterFields, "ai_blackboard");
        foreach (Variant key in AllowedActiveBlackboardKeys.Keys)
        {
            if (afterBlackboard.ContainsKey(key))
            {
                expectedBlackboard[key] = afterBlackboard[key];
            }
            else if (expectedBlackboard.ContainsKey(key))
            {
                expectedBlackboard.Remove(key);
            }
        }

        expectedFields["ai_blackboard"] = expectedBlackboard;
        expectedUnit["fields"] = expectedFields;
        expectedUnits[activeKey] = expectedUnit;
        expectedStable["units"] = expectedUnits;
    }

    private static GDictionary ToStableDictionary(GDictionary value)
    {
        Variant stable = ToStableValue(value);
        return stable.VariantType == Variant.Type.Dictionary ? stable.AsGodotDictionary() : new GDictionary();
    }

    private static Variant ToStableValue(Variant value)
    {
        if (value.VariantType == Variant.Type.Nil)
        {
            return default;
        }
        if (value.VariantType == Variant.Type.StringName)
        {
            return value.AsStringName().ToString();
        }
        if (value.VariantType == Variant.Type.Dictionary)
        {
            GDictionary result = new();
            foreach (Variant key in value.AsGodotDictionary().Keys)
            {
                string stableKey = StableKey(key);
                if (stableKey == "unit_ref")
                {
                    continue;
                }
                result[stableKey] = ToStableValue(value.AsGodotDictionary()[key]);
            }
            return result;
        }
        if (value.VariantType == Variant.Type.Array)
        {
            GArray result = new();
            foreach (Variant item in value.AsGodotArray())
            {
                result.Add(ToStableValue(item));
            }
            return result;
        }
        if (value.VariantType == Variant.Type.Object)
        {
            GodotObject obj = value.AsGodotObject();
            if (obj == null)
            {
                return default;
            }
            if (obj.HasMethod("to_dict"))
            {
                return ToStableValue(obj.Call("to_dict"));
            }
            return (long)obj.GetInstanceId();
        }
        return value;
    }

    private static string StableKey(Variant key)
    {
        if (key.VariantType == Variant.Type.String)
        {
            return key.AsString();
        }
        if (key.VariantType == Variant.Type.StringName)
        {
            return key.AsStringName().ToString();
        }
        if (key.VariantType == Variant.Type.Vector2I)
        {
            Vector2I coord = key.AsVector2I();
            return $"Vector2i({coord.X},{coord.Y})";
        }
        return $"type{(int)key.VariantType}({key})";
    }

    private static void CollectDiffs(Variant expected, Variant actual, string path, GArray violations)
    {
        if (violations.Count >= MaxReportedViolations)
        {
            return;
        }
        if (expected.VariantType == Variant.Type.Dictionary && actual.VariantType == Variant.Type.Dictionary)
        {
            CollectDictionaryDiffs(expected.AsGodotDictionary(), actual.AsGodotDictionary(), path, violations);
            return;
        }
        if (expected.VariantType == Variant.Type.Array && actual.VariantType == Variant.Type.Array)
        {
            CollectArrayDiffs(expected.AsGodotArray(), actual.AsGodotArray(), path, violations);
            return;
        }
        if (!StableScalarsEqual(expected, actual))
        {
            violations.Add($"{path} changed from {expected} to {actual}");
        }
    }

    private static bool StableScalarsEqual(Variant expected, Variant actual)
    {
        if (IsStringLike(expected) && IsStringLike(actual))
        {
            return expected.AsString() == actual.AsString();
        }
        if (expected.VariantType != actual.VariantType)
        {
            return false;
        }
        return expected.VariantType switch
        {
            Variant.Type.Nil => true,
            Variant.Type.Bool => expected.AsBool() == actual.AsBool(),
            Variant.Type.Int => expected.AsInt32() == actual.AsInt32(),
            Variant.Type.Float => Mathf.IsEqualApprox((float)expected.AsDouble(), (float)actual.AsDouble()),
            Variant.Type.String => expected.AsString() == actual.AsString(),
            Variant.Type.StringName => expected.AsStringName() == actual.AsStringName(),
            Variant.Type.Vector2I => expected.AsVector2I() == actual.AsVector2I(),
            Variant.Type.Vector2 => expected.AsVector2() == actual.AsVector2(),
            Variant.Type.Vector3I => expected.AsVector3I() == actual.AsVector3I(),
            Variant.Type.Vector3 => expected.AsVector3() == actual.AsVector3(),
            _ => expected.ToString() == actual.ToString(),
        };
    }

    private static bool IsStringLike(Variant value)
    {
        return value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName;
    }

    private static void CollectDictionaryDiffs(GDictionary expected, GDictionary actual, string path, GArray violations)
    {
        foreach (Variant key in expected.Keys)
        {
            if (violations.Count >= MaxReportedViolations)
            {
                return;
            }
            string childPath = $"{path}.{key}";
            if (!actual.ContainsKey(key))
            {
                violations.Add($"{childPath} was removed");
                continue;
            }
            CollectDiffs(expected[key], actual[key], childPath, violations);
        }

        foreach (Variant key in actual.Keys)
        {
            if (violations.Count >= MaxReportedViolations)
            {
                return;
            }
            if (expected.ContainsKey(key))
            {
                continue;
            }
            violations.Add($"{path}.{key} was added with {actual[key]}");
        }
    }

    private static void CollectArrayDiffs(GArray expected, GArray actual, string path, GArray violations)
    {
        if (expected.Count != actual.Count)
        {
            violations.Add($"{path} size changed from {expected.Count} to {actual.Count}");
            return;
        }
        for (int i = 0; i < expected.Count; i += 1)
        {
            if (violations.Count >= MaxReportedViolations)
            {
                return;
            }
            CollectDiffs(expected[i], actual[i], $"{path}[{i}]", violations);
        }
    }

    private static bool TryGetContextState(GodotObject context, out BattleState state, out BattleUnitState unitState)
    {
        state = GetObject(context, "state") as BattleState;
        unitState = GetObject(context, "unit_state") as BattleUnitState;
        return context != null && state != null && unitState != null;
    }
}
