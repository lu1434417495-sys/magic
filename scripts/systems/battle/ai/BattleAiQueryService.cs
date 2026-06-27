using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiQueryService
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
        IReadOnlyDictionary<string, object>,
        BattleAiScoreInput
    > _actionScoreInputCallback;
    private BattleMovementQueryService _movementQueryService;
    private Func<StringName, bool> _movementBlockedCallback;
    private readonly Dictionary<StringName, BattleAiUnitSnapshot> _snapshotCache = new();
    private readonly Dictionary<StringName, List<BattleAiUnitSnapshot>> _livingSnapshotCache = new();
    private Dictionary<StringName, SkillRecord> _skillRecords = new();
    private IReadOnlyDictionary<StringName, SkillDefinition> _cachedSkillDefinitions;
    private ISkillCatalog _skillCatalog;
    private ISkillCatalog _cachedSkillCatalog;
    private long _cachedSkillCatalogRevision = long.MinValue;
    private readonly Dictionary<DistanceFromAnchorToTargetKey, int> _distanceFromAnchorToTargetCache =
        new();
    private readonly Dictionary<SkillRecordCacheKey, Dictionary<StringName, SkillRecord>> _skillRecordCache =
        new();

    private readonly record struct DistanceFromAnchorToTargetKey(
        Vector2I AnchorCoord,
        Vector2I AnchorFootprintSize,
        StringName TargetUnitId
    );

    private readonly record struct SkillRecordCacheKey(long ActorSignature, int SkillDefinitionCount);

    internal void Setup(
        BattleState state,
        BattleGridService gridService,
        StringName actorUnitId,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        Func<
            BattleAiQueryService,
            StringName,
            string,
            StringName,
            BattleCommand,
            BattlePreview,
            IReadOnlyDictionary<string, object>,
            BattleAiScoreInput
        > actionScoreInputCallback,
        BattleMovementQueryService movementQueryService,
        Func<StringName, bool> movementBlockedCallback = null,
        ISkillCatalog skillCatalog = null
    )
    {
        _state = state;
        _gridService = gridService;
        _actorUnitId = ProgressionDataUtils.to_string_name(actorUnitId);
        _actionScoreInputCallback = actionScoreInputCallback;
        _skillCatalog = skillCatalog;

        _movementQueryService = movementQueryService;
        _movementBlockedCallback = movementBlockedCallback;

        AiTraceRecorder.Enter("query_setup:clear_decision_caches");
        _snapshotCache.Clear();
        _livingSnapshotCache.Clear();
        _distanceFromAnchorToTargetCache.Clear();
        AiTraceRecorder.Exit("query_setup:clear_decision_caches");
        if (!ReferenceEquals(_cachedSkillDefinitions, skillDefinitions))
        {
            _cachedSkillDefinitions = skillDefinitions;
            _skillRecordCache.Clear();
        }
        long skillCatalogRevision = skillCatalog?.GetRevision() ?? long.MinValue;
        if (
            !ReferenceEquals(_cachedSkillCatalog, skillCatalog)
            || _cachedSkillCatalogRevision != skillCatalogRevision
        )
        {
            _cachedSkillCatalog = skillCatalog;
            _cachedSkillCatalogRevision = skillCatalogRevision;
            _skillRecordCache.Clear();
        }
        AiTraceRecorder.Enter("query_setup:skill_records");
        _skillRecords = GetCachedSkillRecords(skillDefinitions);
        AiTraceRecorder.Exit("query_setup:skill_records");
    }

    internal void SetupReadOnly(
        BattleState state,
        BattleGridService gridService,
        StringName actorUnitId,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        ISkillCatalog skillCatalog = null
    )
    {
        Setup(
            state,
            gridService,
            actorUnitId,
            skillDefinitions,
            null,
            null,
            null,
            skillCatalog
        );
    }

    internal void ClearRuntimeBindings()
    {
        _state = null;
        _gridService = null;
        _actorUnitId = "";
        _actionScoreInputCallback = null;
        _movementQueryService = null;
        _movementBlockedCallback = null;
        _snapshotCache.Clear();
        _livingSnapshotCache.Clear();
        _skillRecords = new Dictionary<StringName, SkillRecord>();
        _cachedSkillDefinitions = null;
        _skillCatalog = null;
        _cachedSkillCatalog = null;
        _cachedSkillCatalogRevision = long.MinValue;
        _distanceFromAnchorToTargetCache.Clear();
        _skillRecordCache.Clear();
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
        BattleTargetFilter filterKind = BattleTypedNames.ToTargetFilter(targetFilter);
        StringName normalizedFilter = BattleTypedNames.ToStringName(filterKind);
        if (_livingSnapshotCache.TryGetValue(normalizedFilter, out List<BattleAiUnitSnapshot> cached))
        {
            return cached;
        }

        var results = new List<BattleAiUnitSnapshot>();
        if (filterKind == BattleTargetFilter.Unknown)
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

            if (filterKind == BattleTargetFilter.Self)
            {
                if (snapshot.unit_id == actorSnapshot.unit_id)
                {
                    results.Add(snapshot);
                }
            }
            else if (filterKind == BattleTargetFilter.Ally)
            {
                if (
                    snapshot.unit_id != actorSnapshot.unit_id
                    && snapshot.faction_id == actorSnapshot.faction_id
                )
                {
                    results.Add(snapshot);
                }
            }
            else if (filterKind == BattleTargetFilter.Enemy)
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
        if (IsEmpty(normalized))
        {
            record = null;
            return false;
        }
        if (_skillRecords.TryGetValue(normalized, out record))
        {
            return record != null;
        }
        if (
            _cachedSkillDefinitions == null
            || !_cachedSkillDefinitions.TryGetValue(
                normalized,
                out SkillDefinition skillDefinition
            )
        )
        {
            record = null;
            return false;
        }
        if (skillDefinition == null)
        {
            FailLoud($"BattleAiQueryService received invalid SkillDefinition for {normalized}.");
            record = null;
            return false;
        }
        record = ExtractSkillRecord(skillDefinition);
        _skillRecords[normalized] = record;
        return record != null;
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
        StringName normalizedTargetId = ProgressionDataUtils.to_string_name(targetUnitId);
        Vector2I normalizedFootprint = NormalizeFootprint(anchorFootprintSize);
        var cacheKey = new DistanceFromAnchorToTargetKey(
            anchorCoord,
            normalizedFootprint,
            normalizedTargetId
        );
        if (_distanceFromAnchorToTargetCache.TryGetValue(cacheKey, out int cachedDistance))
        {
            return cachedDistance;
        }

        BattleAiUnitSnapshot target = GetUnitSnapshot(normalizedTargetId);
        if (target == null)
        {
            GameLog.Error("DistanceFromAnchorToTarget received missing target.", "ai.query.missing_target", "ai");
            return -1;
        }

        int bestDistance = int.MaxValue;
        for (int y = 0; y < normalizedFootprint.Y; y++)
        {
            for (int x = 0; x < normalizedFootprint.X; x++)
            {
                Vector2I sourceCoord = anchorCoord + new Vector2I(x, y);
                foreach (Vector2I targetCoord in target.occupied_coords)
                {
                    bestDistance = Mathf.Min(
                        bestDistance,
                        _gridService.GetDistance(sourceCoord, targetCoord)
                    );
                }
            }
        }
        int distance = bestDistance < int.MaxValue ? bestDistance : -1;
        _distanceFromAnchorToTargetCache[cacheKey] = distance;
        return distance;
    }

    internal BattleAiScoreInput BuildActionScoreInput(
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
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

    internal ISkillCatalog GetSkillCatalogTyped() => _skillCatalog;

    private BattleUnitState GetLiveUnit(StringName unitId)
    {
        if (_state == null)
        {
            return null;
        }
        StringName normalized = ProgressionDataUtils.to_string_name(unitId);
        return TryGetUnit(_state, normalized, out BattleUnitState unitState) ? unitState : null;
    }

    private Dictionary<StringName, SkillRecord> GetCachedSkillRecords(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        AiTraceRecorder.Enter("query_setup:skill_record_key");
        SkillRecordCacheKey cacheKey = BuildSkillRecordCacheKey(skillDefinitions);
        AiTraceRecorder.Exit("query_setup:skill_record_key");
        if (_skillRecordCache.TryGetValue(cacheKey, out Dictionary<StringName, SkillRecord> cached))
        {
            return cached;
        }

        Dictionary<StringName, SkillRecord> records = new();
        if (_skillRecordCache.Count > 128)
        {
            _skillRecordCache.Clear();
        }
        _skillRecordCache[cacheKey] = records;
        return records;
    }

    private SkillRecordCacheKey BuildSkillRecordCacheKey(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        BattleUnitState actor = GetLiveUnit(_actorUnitId);
        if (actor == null)
        {
            return new SkillRecordCacheKey(0, skillDefinitions?.Count ?? 0);
        }

        unchecked
        {
            long hash = 1469598103934665603;
            hash = Mix(hash, HashStringName(actor.unit_id));
            hash = Mix(hash, HashStringName(actor.weapon_profile_kind));
            hash = Mix(hash, HashStringName(actor.weapon_family));
            hash = Mix(hash, HashStringName(actor.weapon_physical_damage_tag));
            hash = Mix(hash, actor.weapon_attack_range);

            var activeSkillIds = new List<StringName>();
            foreach (StringName skillId in actor.known_active_skill_ids)
            {
                StringName normalized = ProgressionDataUtils.to_string_name(skillId);
                if (!IsEmpty(normalized))
                    activeSkillIds.Add(normalized);
            }
            activeSkillIds.Sort(
                (left, right) => string.CompareOrdinal(left.ToString(), right.ToString())
            );
            foreach (StringName skillId in activeSkillIds)
            {
                hash = Mix(hash, HashStringName(skillId));
            }

            var skillLevels = new List<(StringName SkillId, int Level)>();
            foreach (StringName key in actor.known_skill_level_map.Keys)
            {
                StringName skillId = ProgressionDataUtils.to_string_name(key);
                if (IsEmpty(skillId))
                    continue;
                skillLevels.Add((skillId, actor.known_skill_level_map.Get(key)));
            }
            skillLevels.Sort(
                (left, right) => string.CompareOrdinal(left.SkillId.ToString(), right.SkillId.ToString())
            );
            foreach ((StringName skillId, int level) in skillLevels)
            {
                hash = Mix(hash, HashStringName(skillId));
                hash = Mix(hash, level);
            }

            var statusParts = new List<(StringName StatusId, int Power, int RangeBonus)>();
            foreach (BattleStatusEffectState status in actor.GetStatusEffectsTyped())
            {
                StringName statusId = status?.status_id ?? new StringName("");
                if (IsEmpty(statusId))
                    continue;
                statusParts.Add((statusId, status.power, status.range_bonus));
            }
            statusParts.Sort(
                (left, right) => string.CompareOrdinal(left.StatusId.ToString(), right.StatusId.ToString())
            );
            foreach ((StringName statusId, int power, int rangeBonus) in statusParts)
            {
                hash = Mix(hash, HashStringName(statusId));
                hash = Mix(hash, power);
                hash = Mix(hash, rangeBonus);
            }

            return new SkillRecordCacheKey(hash, skillDefinitions?.Count ?? 0);
        }
    }

    private static long Mix(long hash, int value)
    {
        unchecked
        {
            return (hash ^ value) * 1099511628211;
        }
    }

    private static int HashStringName(StringName value)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(value);
        return IsEmpty(normalized) ? 0 : normalized.GetHashCode();
    }

    private SkillEffectiveCombatDefinition ResolveEffectiveDefinition(
        SkillDefinition skillDefinition,
        int skillLevel
    )
    {
        if (
            _skillCatalog != null
            && skillDefinition != null
            && !IsEmpty(skillDefinition.SkillId)
        )
        {
            return _skillCatalog.GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel);
        }
        return SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
    }

    private SkillRecord ExtractSkillRecord(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combat = skillDefinition.CombatProfile;
        BattleUnitState actor = GetLiveUnit(_actorUnitId);
        int skillLevel = actor != null ? GetUnitSkillLevel(actor, skillDefinition.SkillId) : 0;
        SkillEffectiveCombatDefinition effectiveDefinition =
            ResolveEffectiveDefinition(skillDefinition, skillLevel);
        var record = new SkillRecord
        {
            skill_id = ProgressionDataUtils.to_string_name(skillDefinition.SkillId),
            display_name = skillDefinition.DisplayName,
            skill_type = ProgressionDataUtils.to_string_name(skillDefinition.SkillType),
            icon_id = ProgressionDataUtils.to_string_name(skillDefinition.IconId),
            target_mode = combat != null ? combat.TargetModeKind : BattleTargetMode.Unknown,
            target_team_filter =
                combat != null ? combat.TargetFilterKind : BattleTargetFilter.Unknown,
            range_pattern = ProgressionDataUtils.to_string_name(
                combat != null ? combat.RangePattern : new StringName("")
            ),
            range_value = effectiveDefinition.RangeValue,
            actor_effective_cast_range =
                actor != null
                    ? BattleRangeService.GetEffectiveSkillRange(
                        actor,
                        skillDefinition,
                        _skillCatalog
                    )
                    : 0,
            actor_effective_range =
                actor != null
                    ? BattleRangeService.GetEffectiveSkillThreatRange(
                        actor,
                        skillDefinition,
                        _skillCatalog
                    )
                    : 0,
            area_pattern = BattleTypedNames.ToAreaPattern(
                effectiveDefinition.AreaPattern
            ),
            area_value = effectiveDefinition.AreaValue,
            target_selection_mode =
                combat != null
                    ? combat.TargetSelectionModeKind
                    : BattleTargetSelectionMode.Unknown,
            min_target_count = combat != null ? combat.MinTargetCount : 0,
            max_target_count = effectiveDefinition.MaxTargetCount,
            ai_tags = CopyStringNameList(combat?.AiTags),
            delivery_categories = CopyStringNameList(combat?.DeliveryCategories),
            required_weapon_families = CopyStringNameList(combat?.RequiredWeaponFamilies),
            excluded_weapon_families = CopyStringNameList(combat?.ExcludedWeaponFamilies),
            excluded_weapon_type_ids = CopyStringNameList(combat?.ExcludedWeaponTypeIds),
        };
        return record;
    }

    private static int GetUnitSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || IsEmpty(skillId))
        {
            return 0;
        }
        int knownSkillLevel = unitState.GetKnownSkillLevelTyped(skillId);
        return knownSkillLevel > 0
            ? knownSkillLevel
            : unitState.known_active_skill_ids.Contains(skillId)
                ? 1
                : 0;
    }

    private static Vector2I NormalizeFootprint(Vector2I footprintSize)
    {
        return new Vector2I(Mathf.Max(footprintSize.X, 1), Mathf.Max(footprintSize.Y, 1));
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

    private static List<StringName> CopyStringNameList(IEnumerable<StringName> source)
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
        public BattleTargetMode target_mode = BattleTargetMode.Unknown;
        public BattleTargetFilter target_team_filter = BattleTargetFilter.Unknown;
        public StringName range_pattern = "";
        public int range_value;
        public int actor_effective_cast_range;
        public int actor_effective_range;
        public BattleAreaPattern area_pattern = BattleAreaPattern.Unknown;
        public int area_value;
        public BattleTargetSelectionMode target_selection_mode = BattleTargetSelectionMode.Unknown;
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
