using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiQueryService : RefCounted
{
    private BattleState _state;
    private BattleGridService _grid_service;
    private StringName _actor_unit_id = "";
    private Callable _preview_callback = new();
    private Callable _action_score_input_callback = new();
    private Callable _skill_score_input_callback = new();
    private GodotObject _movement_query_service;
    private GDictionary _snapshot_cache = new();
    private GDictionary _skill_records = new();
    private GArray _retained_callback_objects = new();

    public void setup(
        BattleState state,
        BattleGridService grid_service,
        StringName actor_unit_id,
        GDictionary skill_defs,
        Callable preview_callback,
        Callable action_score_input_callback,
        Callable skill_score_input_callback,
        GodotObject movement_query_service)
    {
        _state = state;
        _grid_service = grid_service;
        _actor_unit_id = ProgressionDataUtils.to_string_name(actor_unit_id);
        _preview_callback = preview_callback;
        _action_score_input_callback = action_score_input_callback;
        _skill_score_input_callback = skill_score_input_callback;
        _retained_callback_objects.Clear();
        RetainCallableObject(_preview_callback);
        RetainCallableObject(_action_score_input_callback);
        RetainCallableObject(_skill_score_input_callback);

        if (movement_query_service != null && !movement_query_service.HasMethod("build_path_search_budget"))
        {
            BattleAiPayloadGuard.FailLoud(
                "BattleAiQueryService.setup requires BattleMovementQueryService-compatible movement_query_service.",
                new GDictionary { ["source"] = "BattleAiQueryService" });
            _movement_query_service = null;
        }
        else
        {
            _movement_query_service = movement_query_service;
        }

        _snapshot_cache.Clear();
        _skill_records = ExtractSkillRecords(skill_defs ?? new GDictionary());
        BattleAiPayloadGuard.ValidateNoForbiddenObject(_skill_records, "BattleAiQueryService._skill_records");
    }

    public StringName get_actor_id() => _actor_unit_id;

    public BattleAiUnitSnapshot get_actor_snapshot()
    {
        return get_unit_snapshot(_actor_unit_id);
    }

    public BattleAiUnitSnapshot get_unit_snapshot(StringName unit_id)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(unit_id);
        if (IsEmpty(normalized))
        {
            return null;
        }
        if (_snapshot_cache.ContainsKey(normalized))
        {
            return _snapshot_cache[normalized].AsGodotObject() as BattleAiUnitSnapshot;
        }
        if (_state == null || !_state.units.ContainsKey(normalized))
        {
            return null;
        }
        BattleUnitState unitState = _state.units[normalized].AsGodotObject() as BattleUnitState;
        if (unitState == null)
        {
            return null;
        }
        BattleAiUnitSnapshot snapshot = BattleAiUnitSnapshot.from_unit(unitState);
        if (snapshot != null)
        {
            _snapshot_cache[normalized] = snapshot;
        }
        return snapshot;
    }

    public Godot.Collections.Array<BattleAiUnitSnapshot> get_living_unit_snapshots(StringName target_filter)
    {
        StringName normalizedFilter = ProgressionDataUtils.to_string_name(target_filter);
        var results = new Godot.Collections.Array<BattleAiUnitSnapshot>();
        if (normalizedFilter != "enemy" && normalizedFilter != "ally" && normalizedFilter != "self" && normalizedFilter != "any")
        {
            BattleAiPayloadGuard.FailLoud(
                $"Unsupported AI target_filter {target_filter}.",
                new GDictionary { ["source"] = "BattleAiQueryService" });
            return results;
        }

        BattleAiUnitSnapshot actorSnapshot = get_actor_snapshot();
        if (actorSnapshot == null || _state == null)
        {
            return results;
        }

        var sortedIds = new Godot.Collections.Array<StringName>();
        foreach (Variant unitIdValue in _state.units.Keys)
        {
            StringName unitId = ProgressionDataUtils.to_string_name(unitIdValue);
            if (!IsEmpty(unitId))
            {
                sortedIds.Add(unitId);
            }
        }
        sortedIds.Sort();

        foreach (StringName unitId in sortedIds)
        {
            BattleAiUnitSnapshot snapshot = get_unit_snapshot(unitId);
            if (snapshot == null || !snapshot.is_alive)
            {
                continue;
            }

            if (normalizedFilter == "self")
            {
                if (snapshot.unit_id == actorSnapshot.unit_id)
                {
                    results.Add(snapshot);
                }
            }
            else if (normalizedFilter == "ally")
            {
                if (snapshot.unit_id != actorSnapshot.unit_id && snapshot.faction_id == actorSnapshot.faction_id)
                {
                    results.Add(snapshot);
                }
            }
            else if (normalizedFilter == "enemy")
            {
                if (snapshot.faction_id != actorSnapshot.faction_id)
                {
                    results.Add(snapshot);
                }
            }
            else
            {
                results.Add(snapshot);
            }
        }
        return results;
    }

    public GDictionary get_skill_record(StringName skill_id)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(skill_id);
        if (_skill_records.ContainsKey(normalized) && _skill_records[normalized].VariantType == Variant.Type.Dictionary)
        {
            return _skill_records[normalized].AsGodotDictionary().Duplicate(true);
        }
        return new GDictionary();
    }

    public Godot.Collections.Array<StringName> get_actor_known_skill_ids(Godot.Collections.Array<StringName> preferred_skill_ids = null)
    {
        BattleAiUnitSnapshot actorSnapshot = get_actor_snapshot();
        var results = new Godot.Collections.Array<StringName>();
        if (actorSnapshot == null)
        {
            return results;
        }
        if (preferred_skill_ids == null || preferred_skill_ids.Count == 0)
        {
            foreach (StringName skillId in actorSnapshot.known_active_skill_ids)
            {
                results.Add(skillId);
            }
            return results;
        }

        var knownLookup = new GDictionary();
        foreach (StringName skillId in actorSnapshot.known_active_skill_ids)
        {
            knownLookup[skillId] = true;
        }
        var seen = new GDictionary();
        foreach (StringName rawSkillId in preferred_skill_ids)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (IsEmpty(skillId))
            {
                BattleAiPayloadGuard.FailLoud(
                    "preferred_skill_ids must not contain empty skill id.",
                    new GDictionary { ["source"] = "BattleAiQueryService" });
                return new Godot.Collections.Array<StringName>();
            }
            if (seen.ContainsKey(skillId))
            {
                continue;
            }
            seen[skillId] = true;
            if (knownLookup.ContainsKey(skillId))
            {
                results.Add(skillId);
            }
        }
        return results;
    }

    public int distance_between_units(StringName first_unit_id, StringName second_unit_id)
    {
        if (_grid_service == null)
        {
            return -1;
        }
        BattleUnitState first = GetLiveUnit(first_unit_id);
        BattleUnitState second = GetLiveUnit(second_unit_id);
        if (first == null || second == null)
        {
            GD.PushError("distance_between_units received missing unit.");
            return -1;
        }
        return _grid_service.get_distance_between_units(first, second);
    }

    public int distance_from_anchor_to_target(Vector2I anchor_coord, Vector2I anchor_footprint_size, StringName target_unit_id)
    {
        if (_grid_service == null)
        {
            return -1;
        }
        BattleAiUnitSnapshot target = get_unit_snapshot(target_unit_id);
        if (target == null)
        {
            GD.PushError("distance_from_anchor_to_target received missing target.");
            return -1;
        }

        int bestDistance = int.MaxValue;
        foreach (Vector2I sourceCoord in _grid_service.get_footprint_coords(anchor_coord, anchor_footprint_size))
        {
            foreach (Vector2I targetCoord in target.occupied_coords)
            {
                bestDistance = Mathf.Min(bestDistance, _grid_service.get_distance(sourceCoord, targetCoord));
            }
        }
        return bestDistance < int.MaxValue ? bestDistance : -1;
    }

    public BattlePreview preview_command(GodotObject command)
    {
        var battleCommand = command as BattleCommand;
        if (battleCommand == null)
        {
            BattleAiPayloadGuard.FailLoud(
                "preview_command requires BattleCommand.",
                new GDictionary { ["source"] = "BattleAiQueryService" });
            return null;
        }
        if (!BattleAiPayloadGuard.CommandIsValueObject(battleCommand))
        {
            return null;
        }
        if (!IsCallableValid(_preview_callback))
        {
            return new BattlePreview();
        }
        GodotObject previewObject = _preview_callback.Call(battleCommand).AsGodotObject();
        var preview = previewObject as BattlePreview;
        if (preview == null)
        {
            BattleAiPayloadGuard.FailLoud(
                "preview_callback must return BattlePreview.",
                new GDictionary { ["source"] = "BattleAiQueryService" });
            return null;
        }
        return BattleAiPayloadGuard.PreviewHasNoLiveState(preview) ? preview : null;
    }

    public BattleAiScoreInput build_action_score_input(
        StringName action_kind,
        string action_label,
        StringName score_bucket_id,
        BattleCommand command,
        BattlePreview preview,
        GDictionary metadata = null)
    {
        metadata ??= new GDictionary();
        if (!BattleAiPayloadGuard.CommandIsValueObject(command))
        {
            return null;
        }
        if (!BattleAiPayloadGuard.PreviewHasNoLiveState(preview))
        {
            return null;
        }
        if (!BattleAiPayloadGuard.ValidateNoForbiddenObject(metadata, "action_score_metadata"))
        {
            return null;
        }
        if (!IsCallableValid(_action_score_input_callback))
        {
            return null;
        }
        GodotObject scoreInput = _action_score_input_callback.Call(this, action_kind, action_label, score_bucket_id, command, preview, metadata).AsGodotObject();
        return scoreInput as BattleAiScoreInput;
    }

    public BattleAiScoreInput build_skill_score_input(
        StringName skill_id,
        BattleCommand command,
        BattlePreview preview,
        GArray effect_defs = null,
        GDictionary metadata = null)
    {
        effect_defs ??= new GArray();
        metadata ??= new GDictionary();
        if (!BattleAiPayloadGuard.CommandIsValueObject(command))
        {
            return null;
        }
        if (!BattleAiPayloadGuard.PreviewHasNoLiveState(preview))
        {
            return null;
        }
        if (!BattleAiPayloadGuard.ValidateNoForbiddenObject(metadata, "skill_score_metadata"))
        {
            return null;
        }
        if (!IsCallableValid(_skill_score_input_callback))
        {
            return null;
        }
        GodotObject scoreInput = _skill_score_input_callback.Call(
            this,
            ProgressionDataUtils.to_string_name(skill_id),
            command,
            preview,
            effect_defs,
            metadata).AsGodotObject();
        return scoreInput as BattleAiScoreInput;
    }

    public GodotObject get_movement_query_service()
    {
        return _movement_query_service;
    }

    private BattleUnitState GetLiveUnit(StringName unitId)
    {
        if (_state == null)
        {
            return null;
        }
        StringName normalized = ProgressionDataUtils.to_string_name(unitId);
        return _state.units.ContainsKey(normalized) ? _state.units[normalized].AsGodotObject() as BattleUnitState : null;
    }

    private GDictionary ExtractSkillRecords(GDictionary skillDefs)
    {
        var records = new GDictionary();
        foreach (Variant rawSkillId in skillDefs.Keys)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            SkillDef skillDef = skillDefs[rawSkillId].AsGodotObject() as SkillDef;
            if (IsEmpty(skillId) || skillDef == null)
            {
                FailLoud($"BattleAiQueryService.setup received invalid SkillDef for {rawSkillId}.");
                return new GDictionary();
            }
            records[skillId] = ExtractSkillRecord(skillDef);
        }
        return records;
    }

    private void RetainCallableObject(Callable callable)
    {
        if (!IsCallableValid(callable))
        {
            return;
        }
    }

    private static bool IsCallableValid(Callable callable)
    {
        return !callable.Equals(default(Callable)) && !string.IsNullOrEmpty(callable.Method.ToString());
    }

    private GDictionary ExtractSkillRecord(SkillDef skillDef)
    {
        var combat = skillDef.combat_profile as CombatSkillDef;
        BattleUnitState actor = GetLiveUnit(_actor_unit_id);
        var record = new GDictionary
        {
            ["skill_id"] = ProgressionDataUtils.to_string_name(skillDef.skill_id),
            ["display_name"] = skillDef.display_name,
            ["skill_type"] = ProgressionDataUtils.to_string_name(skillDef.skill_type),
            ["icon_id"] = ProgressionDataUtils.to_string_name(skillDef.icon_id),
            ["target_mode"] = ProgressionDataUtils.to_string_name(combat != null ? combat.target_mode : new StringName("")),
            ["target_team_filter"] = ProgressionDataUtils.to_string_name(combat != null ? combat.target_team_filter : new StringName("")),
            ["range_pattern"] = ProgressionDataUtils.to_string_name(combat != null ? combat.range_pattern : new StringName("")),
            ["range_value"] = combat != null ? combat.range_value : 0,
            ["actor_effective_range"] = actor != null ? BattleRangeService.get_effective_skill_threat_range(actor, skillDef) : 0,
            ["area_pattern"] = ProgressionDataUtils.to_string_name(combat != null ? combat.area_pattern : new StringName("")),
            ["area_value"] = combat != null ? combat.area_value : 0,
            ["target_selection_mode"] = ProgressionDataUtils.to_string_name(combat != null ? combat.target_selection_mode : new StringName("")),
            ["min_target_count"] = combat != null ? combat.min_target_count : 0,
            ["max_target_count"] = combat != null ? combat.max_target_count : 0,
            ["ai_tags"] = CopyStringNameArray(combat?.ai_tags),
            ["delivery_categories"] = CopyStringNameArray(combat?.delivery_categories),
            ["required_weapon_families"] = CopyStringNameArray(combat?.required_weapon_families),
            ["excluded_weapon_families"] = CopyStringNameArray(combat?.excluded_weapon_families),
            ["excluded_weapon_type_ids"] = CopyStringNameArray(combat?.excluded_weapon_type_ids),
        };
        BattleAiPayloadGuard.ValidateNoForbiddenObject(record, "skill_record");
        return record;
    }

    private static Godot.Collections.Array<StringName> CopyStringNameArray(Godot.Collections.Array<StringName> source)
    {
        var result = new Godot.Collections.Array<StringName>();
        if (source == null)
        {
            return result;
        }
        foreach (StringName value in source)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (!IsEmpty(normalized))
            {
                result.Add(normalized);
            }
        }
        return result;
    }

    private static bool FailLoud(string message)
    {
        return BattleAiPayloadGuard.FailLoud(message, new GDictionary { ["source"] = "BattleAiQueryService" });
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
