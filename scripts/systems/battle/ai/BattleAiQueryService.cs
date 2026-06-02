using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleAiQueryService
{
    private BattleState _state;
    private BattleGridService _gridService;
    private StringName _actorUnitId = "";
    private Func<
        BattleAiQueryService,
        StringName,
        string,
        StringName,
        BattleCommand,
        BattlePreview,
        Godot.Collections.Dictionary,
        BattleAiScoreInput
    > _actionScoreInputCallback;
    private BattleMovementQueryService _movementQueryService;
    private Func<StringName, bool> _movementBlockedCallback;
    private readonly Dictionary<StringName, BattleAiUnitSnapshot> _snapshotCache = new();
    private readonly Dictionary<StringName, List<BattleAiUnitSnapshot>> _livingSnapshotCache = new();
    private Dictionary<StringName, SkillRecord> _skillRecords = new();

    internal void Setup(
        BattleState state,
        BattleGridService gridService,
        StringName actorUnitId,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        Func<
            BattleAiQueryService,
            StringName,
            string,
            StringName,
            BattleCommand,
            BattlePreview,
            Godot.Collections.Dictionary,
            BattleAiScoreInput
        > actionScoreInputCallback,
        BattleMovementQueryService movementQueryService,
        Func<StringName, bool> movementBlockedCallback = null
    )
    {
        _state = state;
        _gridService = gridService;
        _actorUnitId = ProgressionDataUtils.to_string_name(actorUnitId);
        _actionScoreInputCallback = actionScoreInputCallback;

        _movementQueryService = movementQueryService;
        _movementBlockedCallback = movementBlockedCallback;

        _snapshotCache.Clear();
        _livingSnapshotCache.Clear();
        _skillRecords = ExtractSkillRecords(skillDefs);
    }

    internal void SetupReadOnly(
        BattleState state,
        BattleGridService gridService,
        StringName actorUnitId,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        Setup(
            state,
            gridService,
            actorUnitId,
            skillDefs,
            null,
            null
        );
    }

    internal StringName GetActorId() => _actorUnitId;

    internal BattleAiUnitSnapshot GetActorSnapshot()
    {
        return GetUnitSnapshot(_actorUnitId);
    }

    internal BattleAiUnitSnapshot GetUnitSnapshot(StringName unitId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(unitId);
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
        BattleAiUnitSnapshot snapshot = BattleAiUnitSnapshot.FromUnit(unitState);
        if (snapshot != null)
        {
            _snapshotCache[normalized] = snapshot;
        }
        return snapshot;
    }

    internal IReadOnlyList<BattleAiUnitSnapshot> GetLivingUnitSnapshotsTyped(
        StringName targetFilter
    )
    {
        StringName normalizedFilter = ProgressionDataUtils.to_string_name(targetFilter);
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
                $"Unsupported AI target_filter {targetFilter}.",
                new Dictionary<string, string> { ["source"] = "BattleAiQueryService" }
            );
            return results;
        }

        BattleAiUnitSnapshot actorSnapshot = GetActorSnapshot();
        if (actorSnapshot == null || _state == null)
        {
            return results;
        }

        List<StringName> sortedIds = _state.GetUnitIdsTyped(sorted: true);

        foreach (StringName unitId in sortedIds)
        {
            BattleAiUnitSnapshot snapshot = GetUnitSnapshot(unitId);
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

    internal bool TryGetSkillRecordTyped(StringName skillId, out SkillRecord record)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(skillId);
        return _skillRecords.TryGetValue(normalized, out record);
    }

    internal bool IsUnitMovementBlocked(StringName unitId)
    {
        return _movementBlockedCallback?.Invoke(ProgressionDataUtils.to_string_name(unitId))
            == true;
    }

    internal Vector2I GetMapSize()
    {
        return _state != null ? _state.map_size : Vector2I.Zero;
    }

    internal int DistanceFromAnchorToTarget(
        Vector2I anchorCoord,
        Vector2I anchorFootprintSize,
        StringName targetUnitId
    )
    {
        if (_gridService == null)
        {
            return -1;
        }
        BattleAiUnitSnapshot target = GetUnitSnapshot(targetUnitId);
        if (target == null)
        {
            GameLog.Error("DistanceFromAnchorToTarget received missing target.", "ai.query.missing_target", "ai");
            return -1;
        }

        int bestDistance = int.MaxValue;
        foreach (
            Vector2I sourceCoord in _gridService.get_footprint_coords(
                anchorCoord,
                anchorFootprintSize
            )
        )
        {
            foreach (Vector2I targetCoord in target.occupied_coords)
            {
                bestDistance = Mathf.Min(
                    bestDistance,
                    _gridService.get_distance(sourceCoord, targetCoord)
                );
            }
        }
        return bestDistance < int.MaxValue ? bestDistance : -1;
    }

    internal BattleAiScoreInput BuildActionScoreInput(
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        Godot.Collections.Dictionary metadata = null
    )
    {
        metadata ??= new Godot.Collections.Dictionary();
        if (_actionScoreInputCallback == null)
        {
            return null;
        }
        return _actionScoreInputCallback.Invoke(
            this,
            actionKind,
            actionLabel,
            scoreBucketId,
            command,
            preview,
            metadata
        );
    }

    internal BattleMovementQueryService GetMovementQueryService()
    {
        return _movementQueryService;
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

    private Dictionary<StringName, SkillRecord> ExtractSkillRecords(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        var records = new Dictionary<StringName, SkillRecord>();
        foreach (KeyValuePair<StringName, SkillDef> entry in ExtractSkillDefs(skillDefs))
        {
            if (IsEmpty(entry.Key) || entry.Value == null)
            {
                FailLoud($"BattleAiQueryService.Setup received invalid SkillDef for {entry.Key}.");
                return new Dictionary<StringName, SkillRecord>();
            }
            records[entry.Key] = ExtractSkillRecord(entry.Value);
        }
        return records;
    }

    private SkillRecord ExtractSkillRecord(SkillDef skillDef)
    {
        var combat = skillDef.combat_profile as CombatSkillDef;
        BattleUnitState actor = GetLiveUnit(_actorUnitId);
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
                actor != null ? BattleRangeService.GetEffectiveSkillRange(actor, skillDef) : 0,
            actor_effective_range =
                actor != null
                    ? BattleRangeService.GetEffectiveSkillThreatRange(actor, skillDef)
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
        return record;
    }

    private static List<KeyValuePair<StringName, SkillDef>> ExtractSkillDefs(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        var results = new List<KeyValuePair<StringName, SkillDef>>();
        if (skillDefs == null)
        {
            return results;
        }
        foreach (KeyValuePair<StringName, SkillDef> entry in skillDefs)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(entry.Key);
            SkillDef skillDef = entry.Value;
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
    }

    private static bool FailLoud(string message)
    {
        return BattleAiPayloadGuard.FailLoud(
            message,
            new Dictionary<string, string> { ["source"] = "BattleAiQueryService" }
        );
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
