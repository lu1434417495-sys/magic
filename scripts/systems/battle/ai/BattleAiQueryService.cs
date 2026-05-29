using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiQueryService : RefCounted
{
    private BattleState _state;
    private BattleGridService _grid_service;
    private StringName _actor_unit_id = "";
    private Func<
        BattleAiQueryService,
        StringName,
        string,
        StringName,
        BattleCommand,
        BattlePreview,
        GDictionary,
        BattleAiScoreInput
    > _action_score_input_callback;
    private Func<
        BattleAiQueryService,
        StringName,
        BattleCommand,
        BattlePreview,
        GArray,
        GDictionary,
        BattleAiScoreInput
    > _skill_score_input_callback;
    private BattleMovementQueryService _movement_query_service;
    private Func<StringName, bool> _movement_blocked_callback;
    private readonly Dictionary<StringName, BattleAiUnitSnapshot> _snapshotCache = new();
    private readonly Dictionary<StringName, List<BattleAiUnitSnapshot>> _livingSnapshotCache = new();
    private Dictionary<StringName, SkillRecord> _skillRecords = new();

    public void setup(
        BattleState state,
        BattleGridService grid_service,
        StringName actor_unit_id,
        GDictionary skill_defs,
        Func<
            BattleAiQueryService,
            StringName,
            string,
            StringName,
            BattleCommand,
            BattlePreview,
            GDictionary,
            BattleAiScoreInput
        > action_score_input_callback,
        Func<
            BattleAiQueryService,
            StringName,
            BattleCommand,
            BattlePreview,
            GArray,
            GDictionary,
            BattleAiScoreInput
        > skill_score_input_callback,
        BattleMovementQueryService movement_query_service,
        Func<StringName, bool> movement_blocked_callback = null
    )
    {
        _state = state;
        _grid_service = grid_service;
        _actor_unit_id = ProgressionDataUtils.to_string_name(actor_unit_id);
        _action_score_input_callback = action_score_input_callback;
        _skill_score_input_callback = skill_score_input_callback;

        _movement_query_service = movement_query_service;
        _movement_blocked_callback = movement_blocked_callback;

        _snapshotCache.Clear();
        _livingSnapshotCache.Clear();
        _skillRecords = ExtractSkillRecords(skill_defs ?? new GDictionary());
    }

    public void setup_readonly(
        BattleState state,
        BattleGridService grid_service,
        StringName actor_unit_id,
        GDictionary skill_defs
    )
    {
        setup(
            state,
            grid_service,
            actor_unit_id,
            skill_defs,
            null,
            null,
            null
        );
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
        if (_snapshotCache.TryGetValue(normalized, out BattleAiUnitSnapshot cachedSnapshot))
        {
            return cachedSnapshot;
        }
        if (!TryGetUnit(_state, normalized, out BattleUnitState unitState))
        {
            return null;
        }
        BattleAiUnitSnapshot snapshot = BattleAiUnitSnapshot.from_unit(unitState);
        if (snapshot != null)
        {
            _snapshotCache[normalized] = snapshot;
        }
        return snapshot;
    }

    public Godot.Collections.Array<BattleAiUnitSnapshot> get_living_unit_snapshots(
        StringName target_filter
    )
    {
        var results = new Godot.Collections.Array<BattleAiUnitSnapshot>();
        foreach (BattleAiUnitSnapshot snapshot in GetLivingUnitSnapshotsTyped(target_filter))
        {
            results.Add(snapshot);
        }
        return results;
    }

    internal IReadOnlyList<BattleAiUnitSnapshot> GetLivingUnitSnapshotsTyped(
        StringName target_filter
    )
    {
        StringName normalizedFilter = ProgressionDataUtils.to_string_name(target_filter);
        if (_livingSnapshotCache.TryGetValue(normalizedFilter, out List<BattleAiUnitSnapshot> cached))
        {
            return cached;
        }

        var results = new List<BattleAiUnitSnapshot>();
        if (
            normalizedFilter != "enemy"
            && normalizedFilter != "ally"
            && normalizedFilter != "self"
            && normalizedFilter != "any"
        )
        {
            BattleAiPayloadGuard.FailLoud(
                $"Unsupported AI target_filter {target_filter}.",
                new GDictionary { ["source"] = "BattleAiQueryService" }
            );
            return results;
        }

        BattleAiUnitSnapshot actorSnapshot = get_actor_snapshot();
        if (actorSnapshot == null || _state == null)
        {
            return results;
        }

        List<StringName> sortedIds = _state.GetUnitIdsTyped(sorted: true);

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
                if (
                    snapshot.unit_id != actorSnapshot.unit_id
                    && snapshot.faction_id == actorSnapshot.faction_id
                )
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
        _livingSnapshotCache[normalizedFilter] = results;
        return results;
    }

    public GDictionary get_skill_record(StringName skill_id)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(skill_id);
        if (TryGetSkillRecordTyped(normalized, out SkillRecord record))
        {
            return record.ToDictionary();
        }
        return new GDictionary();
    }

    internal bool TryGetSkillRecordTyped(StringName skill_id, out SkillRecord record)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(skill_id);
        return _skillRecords.TryGetValue(normalized, out record);
    }

    internal bool is_unit_movement_blocked(StringName unit_id)
    {
        return _movement_blocked_callback?.Invoke(ProgressionDataUtils.to_string_name(unit_id))
            == true;
    }

    public Vector2I get_map_size()
    {
        return _state != null ? _state.map_size : Vector2I.Zero;
    }

    public Godot.Collections.Array<Vector2I> get_area_coords(
        Vector2I center_coord,
        StringName area_pattern,
        int area_value,
        Vector2I facing_direction
    )
    {
        if (_state == null || _grid_service == null)
        {
            return new Godot.Collections.Array<Vector2I>();
        }
        return _grid_service.get_area_coords(
            _state,
            center_coord,
            area_pattern,
            area_value,
            facing_direction
        );
    }

    public Godot.Collections.Array<StringName> get_actor_known_skill_ids(
        Godot.Collections.Array<StringName> preferred_skill_ids = null
    )
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

        var knownLookup = new HashSet<StringName>();
        foreach (StringName skillId in actorSnapshot.known_active_skill_ids)
        {
            knownLookup.Add(skillId);
        }
        var seen = new HashSet<StringName>();
        foreach (StringName rawSkillId in preferred_skill_ids)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (IsEmpty(skillId))
            {
                BattleAiPayloadGuard.FailLoud(
                    "preferred_skill_ids must not contain empty skill id.",
                    new GDictionary { ["source"] = "BattleAiQueryService" }
                );
                return new Godot.Collections.Array<StringName>();
            }
            if (!seen.Add(skillId))
            {
                continue;
            }
            if (knownLookup.Contains(skillId))
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
            GameLog.Error("distance_between_units received missing unit.", "ai.query.missing_unit", "ai");
            return -1;
        }
        return _grid_service.get_distance_between_units(first, second);
    }

    public int distance_from_anchor_to_target(
        Vector2I anchor_coord,
        Vector2I anchor_footprint_size,
        StringName target_unit_id
    )
    {
        if (_grid_service == null)
        {
            return -1;
        }
        BattleAiUnitSnapshot target = get_unit_snapshot(target_unit_id);
        if (target == null)
        {
            GameLog.Error("distance_from_anchor_to_target received missing target.", "ai.query.missing_target", "ai");
            return -1;
        }

        int bestDistance = int.MaxValue;
        foreach (
            Vector2I sourceCoord in _grid_service.get_footprint_coords(
                anchor_coord,
                anchor_footprint_size
            )
        )
        {
            foreach (Vector2I targetCoord in target.occupied_coords)
            {
                bestDistance = Mathf.Min(
                    bestDistance,
                    _grid_service.get_distance(sourceCoord, targetCoord)
                );
            }
        }
        return bestDistance < int.MaxValue ? bestDistance : -1;
    }

    public BattleAiScoreInput build_action_score_input(
        StringName action_kind,
        string action_label,
        StringName score_bucket_id,
        BattleCommand command,
        BattlePreview preview,
        GDictionary metadata = null
    )
    {
        metadata ??= new GDictionary();
        if (_action_score_input_callback == null)
        {
            return null;
        }
        return _action_score_input_callback.Invoke(
            this,
            action_kind,
            action_label,
            score_bucket_id,
            command,
            preview,
            metadata
        );
    }

    public BattleAiScoreInput build_skill_score_input(
        StringName skill_id,
        BattleCommand command,
        BattlePreview preview,
        GArray effect_defs = null,
        GDictionary metadata = null
    )
    {
        effect_defs ??= new GArray();
        metadata ??= new GDictionary();
        if (_skill_score_input_callback == null)
        {
            return null;
        }
        return _skill_score_input_callback.Invoke(
            this,
            ProgressionDataUtils.to_string_name(skill_id),
            command,
            preview,
            effect_defs,
            metadata
        );
    }

    public BattleMovementQueryService get_movement_query_service()
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
        return TryGetUnit(_state, normalized, out BattleUnitState unitState) ? unitState : null;
    }

    private Dictionary<StringName, SkillRecord> ExtractSkillRecords(GDictionary skillDefs)
    {
        var records = new Dictionary<StringName, SkillRecord>();
        foreach (KeyValuePair<StringName, SkillDef> entry in ExtractSkillDefs(skillDefs))
        {
            if (IsEmpty(entry.Key) || entry.Value == null)
            {
                FailLoud($"BattleAiQueryService.setup received invalid SkillDef for {entry.Key}.");
                return new Dictionary<StringName, SkillRecord>();
            }
            records[entry.Key] = ExtractSkillRecord(entry.Value);
        }
        return records;
    }

    private SkillRecord ExtractSkillRecord(SkillDef skillDef)
    {
        var combat = skillDef.combat_profile as CombatSkillDef;
        BattleUnitState actor = GetLiveUnit(_actor_unit_id);
        int skillLevel = actor != null ? GetUnitSkillLevel(actor, skillDef.skill_id) : 0;
        var record = new SkillRecord
        {
            skill_id = ProgressionDataUtils.to_string_name(skillDef.skill_id),
            display_name = skillDef.display_name,
            skill_type = ProgressionDataUtils.to_string_name(skillDef.skill_type),
            icon_id = ProgressionDataUtils.to_string_name(skillDef.icon_id),
            target_mode = ProgressionDataUtils.to_string_name(
                combat != null ? combat.target_mode : new StringName("")
            ),
            target_team_filter = ProgressionDataUtils.to_string_name(
                combat != null ? combat.target_team_filter : new StringName("")
            ),
            range_pattern = ProgressionDataUtils.to_string_name(
                combat != null ? combat.range_pattern : new StringName("")
            ),
            range_value = combat != null ? combat.get_effective_range_value(skillLevel) : 0,
            actor_effective_cast_range =
                actor != null ? BattleRangeService.get_effective_skill_range(actor, skillDef) : 0,
            actor_effective_range =
                actor != null
                    ? BattleRangeService.get_effective_skill_threat_range(actor, skillDef)
                    : 0,
            area_pattern = ProgressionDataUtils.to_string_name(
                combat != null ? combat.get_effective_area_pattern(skillLevel) : new StringName("")
            ),
            area_value = combat != null ? combat.get_effective_area_value(skillLevel) : 0,
            target_selection_mode = ProgressionDataUtils.to_string_name(
                combat != null ? combat.target_selection_mode : new StringName("")
            ),
            min_target_count = combat != null ? combat.min_target_count : 0,
            max_target_count = combat != null ? combat.max_target_count : 0,
            ai_tags = CopyStringNameList(combat?.ai_tags),
            delivery_categories = CopyStringNameList(combat?.delivery_categories),
            required_weapon_families = CopyStringNameList(combat?.required_weapon_families),
            excluded_weapon_families = CopyStringNameList(combat?.excluded_weapon_families),
            excluded_weapon_type_ids = CopyStringNameList(combat?.excluded_weapon_type_ids),
        };
        BattleAiPayloadGuard.ValidateNoForbiddenObject(record.ToDictionary(), "skill_record");
        return record;
    }

    private static List<KeyValuePair<StringName, SkillDef>> ExtractSkillDefs(GDictionary skillDefs)
    {
        var results = new List<KeyValuePair<StringName, SkillDef>>();
        if (skillDefs == null)
        {
            return results;
        }
        foreach (var rawSkillId in skillDefs.Keys)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            SkillDef skillDef = skillDefs[rawSkillId].AsGodotObject() as SkillDef;
            if (IsEmpty(skillId) || skillDef == null)
            {
                results.Add(new KeyValuePair<StringName, SkillDef>(skillId, null));
                continue;
            }
            results.Add(new KeyValuePair<StringName, SkillDef>(skillId, skillDef));
        }
        results.Sort(
            (left, right) => string.CompareOrdinal(left.Key.ToString(), right.Key.ToString())
        );
        return results;
    }

    private static int GetUnitSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || IsEmpty(skillId))
        {
            return 0;
        }
        if (unitState.known_skill_level_map.ContainsKey(skillId))
        {
            return unitState.known_skill_level_map[skillId].AsInt32();
        }
        return unitState.known_active_skill_ids.Contains(skillId) ? 1 : 0;
    }

    private static bool TryGetUnit(
        BattleState state,
        StringName unitId,
        out BattleUnitState unitState
    )
    {
        unitState = null;
        StringName normalized = ProgressionDataUtils.to_string_name(unitId);
        if (state == null || IsEmpty(normalized))
        {
            return false;
        }
        return state.TryGetUnitTyped(normalized, out unitState);
    }

    private static List<StringName> CopyStringNameList(
        Godot.Collections.Array<StringName> source
    )
    {
        var result = new List<StringName>();
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

    private static Godot.Collections.Array<StringName> ToStringNameArray(
        IEnumerable<StringName> values
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (!IsEmpty(normalized))
            {
                result.Add(normalized);
            }
        }
        return result;
    }

    internal sealed class SkillRecord
    {
        public StringName skill_id = "";
        public string display_name = "";
        public StringName skill_type = "";
        public StringName icon_id = "";
        public StringName target_mode = "";
        public StringName target_team_filter = "";
        public StringName range_pattern = "";
        public int range_value;
        public int actor_effective_cast_range;
        public int actor_effective_range;
        public StringName area_pattern = "";
        public int area_value;
        public StringName target_selection_mode = "";
        public int min_target_count;
        public int max_target_count;
        public List<StringName> ai_tags = new();
        public List<StringName> delivery_categories = new();
        public List<StringName> required_weapon_families = new();
        public List<StringName> excluded_weapon_families = new();
        public List<StringName> excluded_weapon_type_ids = new();

        public GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["skill_id"] = skill_id,
                ["display_name"] = display_name,
                ["skill_type"] = skill_type,
                ["icon_id"] = icon_id,
                ["target_mode"] = target_mode,
                ["target_team_filter"] = target_team_filter,
                ["range_pattern"] = range_pattern,
                ["range_value"] = range_value,
                ["actor_effective_cast_range"] = actor_effective_cast_range,
                ["actor_effective_range"] = actor_effective_range,
                ["area_pattern"] = area_pattern,
                ["area_value"] = area_value,
                ["target_selection_mode"] = target_selection_mode,
                ["min_target_count"] = min_target_count,
                ["max_target_count"] = max_target_count,
                ["ai_tags"] = ToStringNameArray(ai_tags),
                ["delivery_categories"] = ToStringNameArray(delivery_categories),
                ["required_weapon_families"] = ToStringNameArray(required_weapon_families),
                ["excluded_weapon_families"] = ToStringNameArray(excluded_weapon_families),
                ["excluded_weapon_type_ids"] = ToStringNameArray(excluded_weapon_type_ids),
            };
        }
    }

    private static bool FailLoud(string message)
    {
        return BattleAiPayloadGuard.FailLoud(
            message,
            new GDictionary { ["source"] = "BattleAiQueryService" }
        );
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
