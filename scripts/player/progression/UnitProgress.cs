using System.Collections;
using System.Collections.Generic;
using Godot;

public partial class UnitProgress
{
    private static readonly string[] TO_DICT_FIELDS =
    {
        "version",
        "unit_id",
        "display_name",
        "character_level",
        "unit_base_attributes",
        "reputation_state",
        "skills",
        "professions",
        "known_knowledge_ids",
        "active_core_skill_ids",
        "attribute_growth_progress",
        "achievement_progress",
        "blocked_relearn_skill_ids",
        "merged_skill_source_map",
        "unlocked_combat_resource_ids",
    };
    private readonly Dictionary<StringName, UnitSkillProgress> _skills = new();
    private readonly List<StringName> _knownKnowledgeIds = new();
    private readonly List<StringName> _activeCoreSkillIds = new();
    private readonly Dictionary<StringName, int> _attributeGrowthProgress = new();
    private readonly Dictionary<StringName, AchievementProgressState> _achievementProgress =
        new();
    private readonly Dictionary<StringName, UnitProfessionProgress> _professions = new();
    private readonly List<StringName> _blockedRelearnSkillIds = new();
    private readonly Dictionary<StringName, List<StringName>> _mergedSkillSourceMap = new();
    private readonly List<StringName> _unlockedCombatResourceIds =
        new(CombatResourceIds.DefaultUnlocked);

    public int version = 2;
    public StringName unit_id = "";
    public string display_name = "";
    public int character_level;
    public UnitBaseAttributes unit_base_attributes = new UnitBaseAttributes();
    public UnitReputationState reputation_state = new UnitReputationState();
    public Godot.Collections.Dictionary skills
    {
        get => BuildSkillDictionary();
        set => SetSkillProgressStates(value);
    }
    public Godot.Collections.Dictionary professions
    {
        get => BuildProfessionDictionary();
        set => SetProfessionProgressStates(value);
    }
    public StringNameList known_knowledge_ids
    {
        get => new(_knownKnowledgeIds);
        set => SetKnownKnowledgeIds(value);
    }
    public StringNameList active_core_skill_ids
    {
        get => new(_activeCoreSkillIds);
        set => SetActiveCoreSkillIds(value);
    }
    public Godot.Collections.Dictionary attribute_growth_progress
    {
        get => ProgressionDataUtils.string_name_int_map_to_string_dict(_attributeGrowthProgress);
        set => SetAttributeGrowthProgress(value);
    }
    public Godot.Collections.Dictionary achievement_progress
    {
        get => BuildAchievementProgressDictionary();
        set => SetAchievementProgressStates(value);
    }
    public StringNameList blocked_relearn_skill_ids
    {
        get => new(_blockedRelearnSkillIds);
        set => SetBlockedRelearnSkillIds(value);
    }
    public Godot.Collections.Dictionary merged_skill_source_map
    {
        get => BuildMergedSkillSourceMapDictionary();
        set => SetMergedSkillSourceMap(value);
    }
    public StringNameList unlocked_combat_resource_ids
    {
        get => new(_unlockedCombatResourceIds);
        set => SetUnlockedCombatResourceIds(value);
    }
    internal IReadOnlyList<StringName> KnownKnowledgeIdsTyped => _knownKnowledgeIds;
    internal IReadOnlyList<StringName> ActiveCoreSkillIdsTyped => _activeCoreSkillIds;
    internal IReadOnlyDictionary<StringName, UnitSkillProgress> SkillsTyped => _skills;
    internal IReadOnlyDictionary<StringName, int> AttributeGrowthProgressTyped =>
        _attributeGrowthProgress;
    internal IReadOnlyDictionary<StringName, AchievementProgressState> AchievementProgressTyped =>
        _achievementProgress;
    internal IReadOnlyDictionary<StringName, UnitProfessionProgress> ProfessionsTyped =>
        _professions;
    internal IReadOnlyList<StringName> BlockedRelearnSkillIdsTyped => _blockedRelearnSkillIds;
    internal IReadOnlyDictionary<StringName, List<StringName>> MergedSkillSourceMapTyped =>
        _mergedSkillSourceMap;
    internal IReadOnlyList<StringName> UnlockedCombatResourceIdsTyped => _unlockedCombatResourceIds;

    public void SetSkillProgress(UnitSkillProgress sp)
    {
        if (sp == null || sp.skill_id == "")
            return;
        _skills[sp.skill_id] = sp;
        if (sp.merged_from_skill_ids.Count > 0)
            RememberMergeSources(sp.skill_id, sp.merged_from_skill_ids);
        SyncActiveCoreSkillIds();
    }

    public UnitSkillProgress GetSkillProgress(StringName sid) =>
        _skills.TryGetValue(sid, out UnitSkillProgress progress) ? progress : null;

    public void RemoveSkillProgress(StringName sid)
    {
        _skills.Remove(sid);
        SyncActiveCoreSkillIds();
    }

    public void SetProfessionProgress(UnitProfessionProgress pp)
    {
        if (pp != null && pp.profession_id != "")
            _professions[pp.profession_id] = pp;
    }

    public UnitProfessionProgress GetProfessionProgress(StringName pid) =>
        _professions.TryGetValue(pid, out UnitProfessionProgress progress) ? progress : null;

    public void RemoveProfessionProgress(StringName pid)
    {
        if (pid != "" && (!_professions.TryGetValue(pid, out var value) || value.promotion_history.Count == 0))
            _professions.Remove(pid);
    }

    public void SetAchievementProgressState(AchievementProgressState aps)
    {
        if (aps != null && aps.achievement_id != "")
            _achievementProgress[aps.achievement_id] = aps.DuplicateState();
    }

    public AchievementProgressState GetAchievementProgressState(StringName aid) =>
        _achievementProgress.TryGetValue(aid, out AchievementProgressState progressState)
            ? progressState
            : null;

    public bool HasKnowledge(StringName kid) => kid != "" && HasStringName(_knownKnowledgeIds, kid);

    public bool LearnKnowledge(StringName kid)
    {
        if (kid == "" || HasKnowledge(kid))
            return false;
        AddUniqueStringName(_knownKnowledgeIds, kid);
        return true;
    }

    public void SyncActiveCoreSkillIds()
    {
        var next = new List<StringName>();
        foreach (StringName sid in GetSortedSkillIdsTyped())
        {
            var sp = GetSkillProgress(sid);
            if (sp != null && sp.is_learned && sp.is_core)
                next.Add(sid);
        }
        SetActiveCoreSkillIds(next);
    }

    public bool IsSkillRelearnBlocked(StringName sid) =>
        HasStringName(_blockedRelearnSkillIds, sid);

    public void BlockSkillRelearn(StringName sid)
    {
        AddUniqueStringName(_blockedRelearnSkillIds, sid);
    }

    public void RememberMergeSources(
        StringName sid,
        IEnumerable<StringName> sourceIds
    )
    {
        var deduped = new List<StringName>();
        var seen = new HashSet<StringName>();
        foreach (var s in sourceIds)
        {
            if (s == sid || s == "" || !seen.Add(s))
                continue;
            deduped.Add(s);
        }
        if (sid != "")
            _mergedSkillSourceMap[sid] = new List<StringName>(deduped);
        var sp = GetSkillProgress(sid);
        if (sp != null)
            sp.merged_from_skill_ids = new StringNameList(deduped);
    }

    internal List<StringName> GetMergedSourceSkillIdsTyped(StringName sid)
    {
        if (_mergedSkillSourceMap.TryGetValue(sid, out List<StringName> sourceIds))
            return new List<StringName>(sourceIds);
        var sp = GetSkillProgress(sid);
        if (sp != null && sp.merged_from_skill_ids.Count > 0)
            return new List<StringName>(sp.merged_from_skill_ids);
        return new List<StringName>();
    }

    // 当前没有生产调用；本实现依赖 merge source 图保持无环。
    // 若未来接入生产路径，须先在递归下降前标记访问节点，并在写入/读档边界拒绝环，
    // 否则 A -> B -> A 会持续递归直至栈溢出。
    internal List<StringName> GetMergedSourceSkillIdsRecursiveTyped(StringName sid)
    {
        var r = new List<StringName>();
        var visited = new HashSet<StringName>();
        foreach (var s in GetMergedSourceSkillIdsTyped(sid))
            _append_recursive_merge_source(s, r, visited);
        return r;
    }

    private void _append_recursive_merge_source(
        StringName sid,
        List<StringName> results,
        HashSet<StringName> visited
    )
    {
        if (visited.Contains(sid))
            return;
        foreach (var ns in GetMergedSourceSkillIdsTyped(sid))
            _append_recursive_merge_source(ns, results, visited);
        if (visited.Contains(sid))
            return;
        visited.Add(sid);
        results.Add(sid);
    }

    public void SyncDefaultCombatResourceUnlocks()
    {
        foreach (var rid in CombatResourceIds.DefaultUnlocked)
            UnlockCombatResource(rid);
    }

    public bool HasCombatResourceUnlocked(StringName rid) =>
        HasStringName(_unlockedCombatResourceIds, rid);

    public bool UnlockCombatResource(StringName rid)
    {
        if (
            rid == ""
            || CombatResourceIds.ToResourceKind(rid) == CombatResourceIdKind.Unknown
            || HasStringName(_unlockedCombatResourceIds, rid)
        )
            return false;
        AddUniqueStringName(_unlockedCombatResourceIds, rid);
        return true;
    }

    public void SetKnownKnowledgeIds(IEnumerable values) => SetUniqueStringNames(_knownKnowledgeIds, values);

    public void SetActiveCoreSkillIds(IEnumerable values) =>
        SetUniqueStringNames(_activeCoreSkillIds, values);

    private void SetSkillProgressStates(Godot.Collections.Dictionary values)
    {
        _skills.Clear();
        if (values == null)
            return;
        foreach (Variant rawKey in values.Keys)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawKey);
            if (skillId == "")
                continue;
            object rawValue = values[rawKey];
            if (!TryAsDictionary(rawValue, out Godot.Collections.Dictionary skillPayload))
                continue;
            UnitSkillProgress skillProgress = UnitSkillProgress.FromDictionary(skillPayload);
            if (skillProgress == null || skillProgress.skill_id == "" || skillProgress.skill_id != skillId)
                continue;
            _skills[skillId] = skillProgress.DuplicateState();
        }
    }

    public void SetSkillProgressStates(
        IEnumerable<KeyValuePair<StringName, UnitSkillProgress>> values
    )
    {
        _skills.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<StringName, UnitSkillProgress> pair in values)
        {
            if (
                pair.Key == ""
                || pair.Value == null
                || pair.Value.skill_id == ""
                || pair.Value.skill_id != pair.Key
            )
                continue;
            _skills[pair.Key] = pair.Value.DuplicateState();
        }
    }

    private void SetAttributeGrowthProgress(Godot.Collections.Dictionary values)
    {
        _attributeGrowthProgress.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<StringName, int> pair in ProgressionDataUtils.to_string_name_int_dictionary(values))
            _attributeGrowthProgress[pair.Key] = pair.Value;
    }

    public void SetAttributeGrowthProgress(IEnumerable<KeyValuePair<StringName, int>> values)
    {
        _attributeGrowthProgress.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<StringName, int> pair in values)
        {
            if (pair.Key == "" || pair.Value < 0)
                continue;
            _attributeGrowthProgress[pair.Key] = pair.Value;
        }
    }

    public bool TryGetAttributeGrowthProgressAmount(StringName attributeId, out int amount)
    {
        if (attributeId != "" && _attributeGrowthProgress.TryGetValue(attributeId, out amount))
            return true;
        amount = 0;
        return false;
    }

    public void SetAttributeGrowthProgressAmount(StringName attributeId, int amount)
    {
        if (attributeId == "" || amount < 0)
            return;
        _attributeGrowthProgress[attributeId] = amount;
    }

    public void SetBlockedRelearnSkillIds(IEnumerable values) =>
        SetUniqueStringNames(_blockedRelearnSkillIds, values);

    private void SetAchievementProgressStates(Godot.Collections.Dictionary values)
    {
        _achievementProgress.Clear();
        if (values == null)
            return;
        foreach (Variant rawKey in values.Keys)
        {
            StringName achievementId = ProgressionDataUtils.to_string_name(rawKey);
            if (achievementId == "")
                continue;
            object rawValue = values[rawKey];
            if (!TryAsDictionary(rawValue, out Godot.Collections.Dictionary progressPayload))
                continue;
            AchievementProgressState progressState =
                AchievementProgressState.FromDictionary(progressPayload);
            if (progressState == null || progressState.achievement_id != achievementId)
                continue;
            _achievementProgress[achievementId] = progressState.DuplicateState();
        }
    }

    public void SetAchievementProgressStates(
        IEnumerable<KeyValuePair<StringName, AchievementProgressState>> values
    )
    {
        _achievementProgress.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<StringName, AchievementProgressState> pair in values)
        {
            if (pair.Key == "" || pair.Value == null || pair.Value.achievement_id != pair.Key)
                continue;
            _achievementProgress[pair.Key] = pair.Value.DuplicateState();
        }
    }

    private void SetProfessionProgressStates(Godot.Collections.Dictionary values)
    {
        _professions.Clear();
        if (values == null)
            return;
        foreach (Variant rawKey in values.Keys)
        {
            StringName professionId = ProgressionDataUtils.to_string_name(rawKey);
            if (professionId == "")
                continue;
            object rawValue = values[rawKey];
            if (!TryAsDictionary(rawValue, out Godot.Collections.Dictionary professionPayload))
                continue;
            UnitProfessionProgress professionProgress =
                UnitProfessionProgress.FromDictionary(professionPayload);
            if (
                professionProgress == null
                || professionProgress.profession_id == ""
                || professionProgress.profession_id != professionId
            )
                continue;
            _professions[professionId] = professionProgress.DuplicateState();
        }
    }

    public void SetProfessionProgressStates(
        IEnumerable<KeyValuePair<StringName, UnitProfessionProgress>> values
    )
    {
        _professions.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<StringName, UnitProfessionProgress> pair in values)
        {
            if (
                pair.Key == ""
                || pair.Value == null
                || pair.Value.profession_id == ""
                || pair.Value.profession_id != pair.Key
            )
                continue;
            _professions[pair.Key] = pair.Value.DuplicateState();
        }
    }

    private void SetMergedSkillSourceMap(Godot.Collections.Dictionary values)
    {
        _mergedSkillSourceMap.Clear();
        if (values == null)
            return;
        foreach (var rawKey in values.Keys)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawKey);
            if (skillId == "")
                continue;
            _mergedSkillSourceMap[skillId] = ToUniqueStringNameList(
                ProgressionDataUtils.to_string_name_array(values[rawKey])
            );
        }
    }

    public void SetMergedSkillSourceMap(
        IEnumerable<KeyValuePair<StringName, List<StringName>>> values
    )
    {
        _mergedSkillSourceMap.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<StringName, List<StringName>> pair in values)
        {
            if (pair.Key == "")
                continue;
            _mergedSkillSourceMap[pair.Key] = ToUniqueStringNameList(pair.Value);
        }
    }

    public void SetUnlockedCombatResourceIds(IEnumerable values) =>
        SetUniqueStringNames(_unlockedCombatResourceIds, values);

    public UnitProgress DuplicateState()
    {
        var copy = new UnitProgress
        {
            version = version,
            unit_id = unit_id,
            display_name = display_name,
            character_level = character_level,
            unit_base_attributes = unit_base_attributes?.DuplicateState() ?? new UnitBaseAttributes(),
            reputation_state = reputation_state?.DuplicateState() ?? new UnitReputationState(),
        };
        copy.SetKnownKnowledgeIds(_knownKnowledgeIds);
        copy.SetSkillProgressStates(_skills);
        copy.SetActiveCoreSkillIds(_activeCoreSkillIds);
        copy.SetAttributeGrowthProgress(_attributeGrowthProgress);
        copy.SetAchievementProgressStates(_achievementProgress);
        copy.SetProfessionProgressStates(_professions);
        copy.SetBlockedRelearnSkillIds(_blockedRelearnSkillIds);
        copy.SetMergedSkillSourceMap(_mergedSkillSourceMap);
        copy.SetUnlockedCombatResourceIds(_unlockedCombatResourceIds);
        copy.SyncActiveCoreSkillIds();
        copy.SyncDefaultCombatResourceUnlocks();
        return copy;
    }

    public Godot.Collections.Dictionary ToDictionary()
    {
        SyncActiveCoreSkillIds();
        SyncDefaultCombatResourceUnlocks();
        var sd = new Godot.Collections.Dictionary();
        foreach (StringName skillId in GetSortedSkillIdsTyped())
        {
            var sp = GetSkillProgress(skillId);
            if (sp != null)
                sd[skillId] = sp.ToDictionary();
        }
        var pd = new Godot.Collections.Dictionary();
        foreach (StringName professionId in GetSortedProfessionIdsTyped())
        {
            var pp = GetProfessionProgress(professionId);
            if (pp != null)
                pd[professionId] = pp.ToDictionary();
        }
        var ad = new Godot.Collections.Dictionary();
        foreach (var k in ProgressionDataUtils.sorted_string_keys(BuildAchievementProgressDictionary()))
        {
            var ap = GetAchievementProgressState(new StringName(k));
            if (ap != null)
                ad[k] = ap.ToDictionary();
        }
        return new Godot.Collections.Dictionary
        {
            { "version", version },
            { "unit_id", (string)unit_id },
            { "display_name", display_name },
            { "character_level", character_level },
            {
                "unit_base_attributes",
                unit_base_attributes?.ToDictionary() ?? new Godot.Collections.Dictionary()
            },
            {
                "reputation_state",
                reputation_state?.ToDictionary() ?? new Godot.Collections.Dictionary()
            },
            { "skills", sd },
            { "professions", pd },
            {
                "known_knowledge_ids",
                ProgressionDataUtils.string_name_array_to_string_array(_knownKnowledgeIds)
            },
            {
                "active_core_skill_ids",
                ProgressionDataUtils.string_name_array_to_string_array(_activeCoreSkillIds)
            },
            {
                "attribute_growth_progress",
                ProgressionDataUtils.string_name_int_map_to_string_dict(_attributeGrowthProgress)
            },
            { "achievement_progress", ad },
            {
                "blocked_relearn_skill_ids",
                ProgressionDataUtils.string_name_array_to_string_array(_blockedRelearnSkillIds)
            },
            {
                "merged_skill_source_map",
                ProgressionDataUtils.string_name_array_map_to_string_dict(_mergedSkillSourceMap)
            },
            {
                "unlocked_combat_resource_ids",
                ProgressionDataUtils.string_name_array_to_string_array(_unlockedCombatResourceIds)
            },
        };
    }

    public static UnitProgress FromDictionary(Godot.Collections.Dictionary data) =>
        FromDictionary(data, out _);

    public static UnitProgress FromDictionary(Godot.Collections.Dictionary data, out string failureReason)
    {
        failureReason = "invalid progression fields or values";
        if (!_hef(data, TO_DICT_FIELDS))
            return null;
        if (
            !TryGetDictionary(
                data,
                "unit_base_attributes",
                out Godot.Collections.Dictionary unitBaseAttributesData
            )
        )
            return null;
        if (
            !TryGetDictionary(
                data,
                "reputation_state",
                out Godot.Collections.Dictionary reputationStateData
            )
        )
            return null;
        if (!TryGetDictionary(data, "skills", out Godot.Collections.Dictionary skillsData))
            return null;
        if (
            !TryGetDictionary(data, "professions", out Godot.Collections.Dictionary professionsData)
        )
            return null;
        if (
            !TryGetArray(
                data,
                "known_knowledge_ids",
                out Godot.Collections.Array knownKnowledgeIdValues
            )
        )
            return null;
        if (
            !TryGetArray(
                data,
                "active_core_skill_ids",
                out Godot.Collections.Array activeCoreSkillIdValues
            )
        )
            return null;
        if (
            !TryGetDictionary(
                data,
                "attribute_growth_progress",
                out Godot.Collections.Dictionary attributeGrowthProgressData
            )
        )
            return null;
        if (
            !TryGetDictionary(
                data,
                "achievement_progress",
                out Godot.Collections.Dictionary achievementProgressData
            )
        )
            return null;
        if (
            !TryGetArray(
                data,
                "blocked_relearn_skill_ids",
                out Godot.Collections.Array blockedRelearnSkillIdValues
            )
        )
            return null;
        if (
            !TryGetDictionary(
                data,
                "merged_skill_source_map",
                out Godot.Collections.Dictionary mergedSkillSourceMapData
            )
        )
            return null;
        if (
            !TryGetArray(
                data,
                "unlocked_combat_resource_ids",
                out Godot.Collections.Array unlockedCombatResourceIdValues
            )
        )
            return null;
        if (!TryGetStrictInt(data, "version", out int versionValue) || versionValue != 2)
        {
            failureReason = "version: expected 2";
            return null;
        }

        var parsedUnitId = _parse_required_string_name(data, "unit_id", out bool unitIdOk);
        if (!unitIdOk)
            return null;
        if (!TryGetStrictString(data, "display_name", out string parsedDisplayName))
            return null;
        if (parsedDisplayName.StripEdges().Length == 0)
            return null;
        if (!TryGetStrictInt(data, "character_level", out int characterLevelValue)
            || characterLevelValue < 0)
            return null;

        var parsedKnownKnowledgeIds = _parse_unique_string_name_array(
            knownKnowledgeIdValues
        );
        if (parsedKnownKnowledgeIds == null)
            return null;
        var parsedActiveCoreSkillIds = _parse_unique_string_name_array(
            activeCoreSkillIdValues
        );
        if (parsedActiveCoreSkillIds == null)
            return null;
        var parsedAttributeGrowthProgress = _parse_nonnegative_int_map(
            attributeGrowthProgressData
        );
        if (parsedAttributeGrowthProgress == null)
            return null;
        var parsedBlockedRelearnSkillIds = _parse_unique_string_name_array(
            blockedRelearnSkillIdValues
        );
        if (parsedBlockedRelearnSkillIds == null)
            return null;
        var parsedMergedSkillSourceMap = _parse_string_name_array_map(
            mergedSkillSourceMapData
        );
        if (parsedMergedSkillSourceMap == null)
            return null;
        var parsedUnlockedResources = _parse_unique_string_name_array(
            unlockedCombatResourceIdValues
        );
        if (parsedUnlockedResources == null)
            return null;
        foreach (var resourceId in parsedUnlockedResources)
            if (CombatResourceIds.ToResourceKind(resourceId) == CombatResourceIdKind.Unknown)
                return null;
        foreach (var defaultResourceId in CombatResourceIds.DefaultUnlocked)
            if (!parsedUnlockedResources.Contains(defaultResourceId))
                return null;

        var unitBaseAttributes = UnitBaseAttributes.FromDictionary(unitBaseAttributesData);
        var reputationState = UnitReputationState.FromDictionary(reputationStateData);
        if (unitBaseAttributes == null || reputationState == null)
            return null;

        var progress = new UnitProgress
        {
            version = versionValue,
            unit_id = parsedUnitId,
            display_name = parsedDisplayName,
            character_level = characterLevelValue,
            unit_base_attributes = unitBaseAttributes,
            reputation_state = reputationState,
        };
        progress.SetKnownKnowledgeIds(parsedKnownKnowledgeIds);
        progress.SetBlockedRelearnSkillIds(parsedBlockedRelearnSkillIds);
        progress.SetAttributeGrowthProgress(parsedAttributeGrowthProgress);
        progress.SetMergedSkillSourceMap(parsedMergedSkillSourceMap);
        progress.SetUnlockedCombatResourceIds(parsedUnlockedResources);
        progress.SyncDefaultCombatResourceUnlocks();

        foreach (var key in skillsData.Keys)
        {
            var skillId = ProgressionDataUtils.to_string_name(key);
            if (skillId == "" || progress._skills.ContainsKey(skillId))
                return null;
            var skillProgressPayload = skillsData[key];
            if (!TryAsDictionary(skillProgressPayload, out Godot.Collections.Dictionary skillData))
                return null;
            var skillProgress = UnitSkillProgress.FromDictionary(skillData);
            if (
                skillProgress == null
                || skillProgress.skill_id == ""
                || skillProgress.skill_id != skillId
            )
                return null;
            progress.SetSkillProgress(skillProgress);
            if (skillProgress.merged_from_skill_ids.Count > 0)
                progress.RememberMergeSources(
                    skillProgress.skill_id,
                    skillProgress.merged_from_skill_ids
                );
        }


        foreach (var key in professionsData.Keys)
        {
            var professionId = ProgressionDataUtils.to_string_name(key);
            if (professionId == "" || progress._professions.ContainsKey(professionId))
                return null;
            var professionProgressPayload = professionsData[key];
            if (
                !TryAsDictionary(
                    professionProgressPayload,
                    out Godot.Collections.Dictionary professionData
                )
            )
                return null;
            var professionProgress = UnitProfessionProgress.FromDictionary(professionData);
            if (
                professionProgress == null
                || professionProgress.profession_id == ""
                || professionProgress.profession_id != professionId
            )
                return null;
            progress.SetProfessionProgress(professionProgress);
        }

        foreach (var key in achievementProgressData.Keys)
        {
            var achievementId = ProgressionDataUtils.to_string_name(key);
            if (achievementId == "" || progress._achievementProgress.ContainsKey(achievementId))
                return null;
            var achievementProgressPayload = achievementProgressData[key];
            if (
                !TryAsDictionary(
                    achievementProgressPayload,
                    out Godot.Collections.Dictionary achievementData
                )
            )
                return null;
            var progressState = AchievementProgressState.FromDictionary(achievementData);
            if (
                progressState == null
                || progressState.achievement_id == ""
                || progressState.achievement_id != achievementId
            )
                return null;
            progress.SetAchievementProgressState(progressState);
        }

        if (!progress.HasValidPromotionHistory())
        {
            failureReason = "promotion_history: ranks and unique growth triggers must match character_level";
            return null;
        }
        progress.SetActiveCoreSkillIds(parsedActiveCoreSkillIds);
        progress.SyncActiveCoreSkillIds();
        failureReason = "";
        return progress;
    }

    private static bool _hef(Godot.Collections.Dictionary d, IReadOnlyCollection<string> e)
    {
        if (d == null || d.Count != e.Count)
            return false;
        foreach (string fn in e)
            if (!d.ContainsKey(fn))
                return false;
        return true;
    }

    private static StringName _parse_required_string_name(
        Godot.Collections.Dictionary values,
        string key,
        out bool ok
    )
    {
        ok = false;
        if (values == null || !values.ContainsKey(key))
            return new StringName("");
        if (!TryGetStringLike(values, key, out string rawText))
            return new StringName("");
        var parsed = new StringName(rawText);
        if (parsed == "")
            return new StringName("");
        ok = true;
        return parsed;
    }

    private static StringName _parse_optional_string_name(
        Godot.Collections.Dictionary values,
        string key,
        out bool ok
    )
    {
        ok = false;
        if (values == null || !values.ContainsKey(key))
            return new StringName("");
        if (!TryGetStringLike(values, key, out string rawText))
            return new StringName("");
        ok = true;
        return new StringName(rawText);
    }

    private static StringNameList _parse_unique_string_name_array(
        Godot.Collections.Array values
    )
    {
        var parsed = new StringNameList();
        var seen = new HashSet<StringName>();
        foreach (var raw in values)
        {
            if (!TryAsStringLike(raw, out string rawText))
                return null;
            var value = new StringName(rawText);
            if (value == "" || !seen.Add(value))
                return null;
            parsed.Add(value);
        }
        return parsed;
    }

    private static void SetUniqueStringNames(List<StringName> target, IEnumerable values)
    {
        target.Clear();
        if (values == null)
            return;
        foreach (object value in values)
            AddUniqueStringName(target, ProgressionDataUtils.to_string_name(value));
    }

    private static void AddUniqueStringName(List<StringName> target, StringName value)
    {
        if (value == "" || target.Contains(value))
            return;
        target.Add(value);
    }

    private static bool HasStringName(IReadOnlyList<StringName> values, StringName target)
    {
        if (target == "")
            return false;
        foreach (StringName value in values)
            if (value == target)
                return true;
        return false;
    }

    internal List<StringName> GetSortedSkillIdsTyped()
    {
        var sortedKeys = new List<StringName>(_skills.Keys);
        sortedKeys.Sort(
            static (left, right) =>
                System.StringComparer.Ordinal.Compare(left.ToString(), right.ToString())
        );
        return sortedKeys;
    }

    internal List<StringName> GetSortedProfessionIdsTyped()
    {
        var sortedKeys = new List<StringName>(_professions.Keys);
        sortedKeys.Sort(
            static (left, right) =>
                System.StringComparer.Ordinal.Compare(left.ToString(), right.ToString())
        );
        return sortedKeys;
    }

    private static Dictionary<StringName, int> _parse_nonnegative_int_map(
        Godot.Collections.Dictionary values
    )
    {
        var parsed = new Dictionary<StringName, int>();
        var seen = new HashSet<StringName>();
        foreach (var rawKey in values.Keys)
        {
            if (!TryAsStringLike(rawKey, out string rawKeyText))
                return null;
            var key = new StringName(rawKeyText);
            if (key == "" || !seen.Add(key))
                return null;
            var rawValue = values[rawKey];
            if (!TryAsStrictInt(rawValue, out int parsedValue) || parsedValue < 0)
                return null;
            parsed[key] = parsedValue;
        }
        return parsed;
    }

    private static Dictionary<StringName, List<StringName>> _parse_string_name_array_map(
        Godot.Collections.Dictionary values
    )
    {
        var parsed = new Dictionary<StringName, List<StringName>>();
        var seen = new HashSet<StringName>();
        foreach (var rawKey in values.Keys)
        {
            if (!TryAsStringLike(rawKey, out string rawKeyText))
                return null;
            var key = new StringName(rawKeyText);
            if (key == "" || !seen.Add(key))
                return null;
            var rawValues = values[rawKey];
            if (!TryAsArray(rawValues, out Godot.Collections.Array sourceValues))
                return null;
            var parsedArray = _parse_unique_string_name_array(sourceValues);
            if (parsedArray == null)
                return null;
            parsed[key] = ToUniqueStringNameList(parsedArray);
        }
        return parsed;
    }

    private Godot.Collections.Dictionary BuildMergedSkillSourceMapDictionary()
    {
        return ProgressionDataUtils.string_name_array_map_to_string_dict(_mergedSkillSourceMap);
    }

    private Godot.Collections.Dictionary BuildSkillDictionary()
    {
        var result = new Godot.Collections.Dictionary();
        foreach (StringName skillId in GetSortedSkillIdsTyped())
        {
            if (_skills.TryGetValue(skillId, out UnitSkillProgress value))
                result[skillId] = value?.ToDictionary() ?? new Godot.Collections.Dictionary();
        }
        return result;
    }

    private Godot.Collections.Dictionary BuildProfessionDictionary()
    {
        var result = new Godot.Collections.Dictionary();
        foreach (StringName professionId in GetSortedProfessionIdsTyped())
        {
            if (_professions.TryGetValue(professionId, out UnitProfessionProgress value))
                result[professionId] = value?.ToDictionary() ?? new Godot.Collections.Dictionary();
        }
        return result;
    }

    private static List<StringName> ToUniqueStringNameList(IEnumerable values)
    {
        var result = new List<StringName>();
        if (values == null)
            return result;
        foreach (object rawValue in values)
            AddUniqueStringName(result, ProgressionDataUtils.to_string_name(rawValue));
        return result;
    }

    private Godot.Collections.Dictionary BuildAchievementProgressDictionary()
    {
        var result = new Godot.Collections.Dictionary();
        var sortedKeys = new List<string>();
        foreach (StringName key in _achievementProgress.Keys)
            sortedKeys.Add(key.ToString());
        sortedKeys.Sort(System.StringComparer.Ordinal);
        foreach (string key in sortedKeys)
        {
            var achievementId = new StringName(key);
            if (_achievementProgress.TryGetValue(achievementId, out AchievementProgressState value))
                result[achievementId] = value?.ToDictionary() ?? new Godot.Collections.Dictionary();
        }
        return result;
    }

    private static bool TryGetStringLike(
        Godot.Collections.Dictionary values,
        string key,
        out string value
    )
    {
        if (TryGetExactValue(values, key, out object rawValue)
            && TryAsStringLike(rawValue, out value))
        {
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetStrictString(
        Godot.Collections.Dictionary values,
        string key,
        out string value
    )
    {
        if (TryGetExactValue(values, key, out object rawValue)
            && TryAsStrictString(rawValue, out value))
        {
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetStrictInt(
        Godot.Collections.Dictionary values,
        string key,
        out int value
    )
    {
        if (TryGetExactValue(values, key, out object rawValue)
            && TryAsStrictInt(rawValue, out value))
        {
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryGetDictionary(
        Godot.Collections.Dictionary values,
        string key,
        out Godot.Collections.Dictionary value
    )
    {
        if (TryGetExactValue(values, key, out object rawValue)
            && TryAsDictionary(rawValue, out value))
        {
            return true;
        }
        value = new Godot.Collections.Dictionary();
        return false;
    }

    private static bool TryGetArray(
        Godot.Collections.Dictionary values,
        string key,
        out Godot.Collections.Array value
    )
    {
        if (TryGetExactValue(values, key, out object rawValue) && TryAsArray(rawValue, out value))
        {
            return true;
        }
        value = new Godot.Collections.Array();
        return false;
    }

    private static bool TryAsStringLike(object rawValue, out string value)
    {
        if (rawValue is Variant variant)
        {
            if (variant.VariantType == Variant.Type.String)
            {
                value = variant.AsString();
                return true;
            }
            if (variant.VariantType == Variant.Type.StringName)
            {
                value = variant.AsStringName().ToString();
                return true;
            }
            value = "";
            return false;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        if (rawValue is StringName stringNameValue)
        {
            value = stringNameValue.ToString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStrictString(object rawValue, out string value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.String)
        {
            value = variant.AsString();
            return true;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStrictInt(object rawValue, out int value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Int
            && variant.AsInt64() >= int.MinValue && variant.AsInt64() <= int.MaxValue)
        {
            value = variant.AsInt32();
            return true;
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryAsDictionary(
        object rawValue,
        out Godot.Collections.Dictionary value
    )
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        if (rawValue is Godot.Collections.Dictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        value = new Godot.Collections.Dictionary();
        return false;
    }

    private static bool TryAsArray(object rawValue, out Godot.Collections.Array value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Array)
        {
            value = variant.AsGodotArray();
            return true;
        }
        if (rawValue is Godot.Collections.Array array)
        {
            value = array;
            return true;
        }
        value = new Godot.Collections.Array();
        return false;
    }

    private static bool TryGetExactValue(
        Godot.Collections.Dictionary values,
        string key,
        out object value
    )
    {
        if (values != null && values.ContainsKey(key))
        {
            value = values[key];
            return true;
        }
        value = null;
        return false;
    }
}
