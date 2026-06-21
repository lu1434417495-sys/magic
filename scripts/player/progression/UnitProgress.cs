using System.Collections;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class UnitProgress : RefCounted
{
    private static readonly Godot.Collections.Array<string> TO_DICT_FIELDS = new()
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
        "pending_profession_choices",
        "blocked_relearn_skill_ids",
        "merged_skill_source_map",
        "unlocked_combat_resource_ids",
        "active_level_trigger_core_skill_id",
        "locked_level_trigger_skill_ids",
    };
    private readonly Dictionary<StringName, UnitSkillProgress> _skills = new();
    private readonly List<StringName> _knownKnowledgeIds = new();
    private readonly List<StringName> _activeCoreSkillIds = new();
    private readonly Dictionary<StringName, int> _attributeGrowthProgress = new();
    private readonly Dictionary<StringName, AchievementProgressState> _achievementProgress =
        new();
    private readonly Dictionary<StringName, UnitProfessionProgress> _professions = new();
    private readonly List<PendingProfessionChoice> _pendingProfessionChoices = new();
    private readonly List<StringName> _blockedRelearnSkillIds = new();
    private readonly Dictionary<StringName, List<StringName>> _mergedSkillSourceMap = new();
    private readonly List<StringName> _unlockedCombatResourceIds =
        new(CombatResourceIds.DefaultUnlocked);
    private readonly List<StringName> _lockedLevelTriggerSkillIds = new();

    public int version = 1;
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
    public Godot.Collections.Array<StringName> known_knowledge_ids
    {
        get => BuildStringNameArray(_knownKnowledgeIds);
        set => SetKnownKnowledgeIds(value);
    }
    public Godot.Collections.Array<StringName> active_core_skill_ids
    {
        get => BuildStringNameArray(_activeCoreSkillIds);
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
    public Godot.Collections.Array<PendingProfessionChoice> pending_profession_choices
    {
        get => BuildPendingProfessionChoicesArray();
        set => SetPendingProfessionChoices(value);
    }
    public Godot.Collections.Array<StringName> blocked_relearn_skill_ids
    {
        get => BuildStringNameArray(_blockedRelearnSkillIds);
        set => SetBlockedRelearnSkillIds(value);
    }
    public Godot.Collections.Dictionary merged_skill_source_map
    {
        get => BuildMergedSkillSourceMapDictionary();
        set => SetMergedSkillSourceMap(value);
    }
    public Godot.Collections.Array<StringName> unlocked_combat_resource_ids
    {
        get => BuildStringNameArray(_unlockedCombatResourceIds);
        set => SetUnlockedCombatResourceIds(value);
    }
    public StringName active_level_trigger_core_skill_id = "";
    public Godot.Collections.Array<StringName> locked_level_trigger_skill_ids
    {
        get => BuildStringNameArray(_lockedLevelTriggerSkillIds);
        set => SetLockedLevelTriggerSkillIds(value);
    }
    private bool _disposed;
    internal IReadOnlyList<StringName> KnownKnowledgeIdsTyped => _knownKnowledgeIds;
    internal IReadOnlyList<StringName> ActiveCoreSkillIdsTyped => _activeCoreSkillIds;
    internal IReadOnlyDictionary<StringName, UnitSkillProgress> SkillsTyped => _skills;
    internal IReadOnlyDictionary<StringName, int> AttributeGrowthProgressTyped =>
        _attributeGrowthProgress;
    internal IReadOnlyDictionary<StringName, AchievementProgressState> AchievementProgressTyped =>
        _achievementProgress;
    internal IReadOnlyDictionary<StringName, UnitProfessionProgress> ProfessionsTyped =>
        _professions;
    internal IReadOnlyList<PendingProfessionChoice> PendingProfessionChoicesTyped => _pendingProfessionChoices;
    internal IReadOnlyList<StringName> BlockedRelearnSkillIdsTyped => _blockedRelearnSkillIds;
    internal IReadOnlyDictionary<StringName, List<StringName>> MergedSkillSourceMapTyped =>
        _mergedSkillSourceMap;
    internal IReadOnlyList<StringName> UnlockedCombatResourceIdsTyped => _unlockedCombatResourceIds;
    internal IReadOnlyList<StringName> LockedLevelTriggerSkillIdsTyped => _lockedLevelTriggerSkillIds;

    public new void Dispose()
    {
        if (_disposed)
            return;
        System.GC.SuppressFinalize(this);
        Dispose(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DisposeManagedState();
        base.Dispose(disposing);
    }

    private void DisposeManagedState()
    {
        if (_disposed)
            return;
        _disposed = true;
        GodotRefCountedDisposer.DisposeAll(_skills.Values);
        GodotRefCountedDisposer.DisposeAll(_achievementProgress.Values);
        GodotRefCountedDisposer.DisposeAll(_professions.Values);
        GodotRefCountedDisposer.DisposeAll(_pendingProfessionChoices);
        GodotRefCountedDisposer.DisposeIfValid(unit_base_attributes);
        GodotRefCountedDisposer.DisposeIfValid(reputation_state);
        _skills.Clear();
        _achievementProgress.Clear();
        _professions.Clear();
        _pendingProfessionChoices.Clear();
        _knownKnowledgeIds.Clear();
        _activeCoreSkillIds.Clear();
        _attributeGrowthProgress.Clear();
        _blockedRelearnSkillIds.Clear();
        _mergedSkillSourceMap.Clear();
        _unlockedCombatResourceIds.Clear();
        _lockedLevelTriggerSkillIds.Clear();
        unit_base_attributes = null;
        reputation_state = null;
    }

    public void SetSkillProgress(UnitSkillProgress sp)
    {
        if (sp == null || sp.skill_id == "")
            return;
        if (_skills.TryGetValue(sp.skill_id, out UnitSkillProgress existing) && existing != sp)
            GodotRefCountedDisposer.DisposeIfValid(existing);
        _skills[sp.skill_id] = sp;
        if (sp.merged_from_skill_ids.Count > 0)
            RememberMergeSources(sp.skill_id, sp.merged_from_skill_ids);
        SyncActiveCoreSkillIds();
    }

    public UnitSkillProgress GetSkillProgress(StringName sid) =>
        _skills.TryGetValue(sid, out UnitSkillProgress progress) ? progress : null;

    public void RemoveSkillProgress(StringName sid)
    {
        if (_skills.TryGetValue(sid, out UnitSkillProgress existing))
            GodotRefCountedDisposer.DisposeIfValid(existing);
        _skills.Remove(sid);
        SyncActiveCoreSkillIds();
    }

    public void SetProfessionProgress(UnitProfessionProgress pp)
    {
        if (pp == null || pp.profession_id == "")
            return;
        if (
            _professions.TryGetValue(pp.profession_id, out UnitProfessionProgress existing)
            && existing != pp
        )
            GodotRefCountedDisposer.DisposeIfValid(existing);
        _professions[pp.profession_id] = pp;
    }

    public UnitProfessionProgress GetProfessionProgress(StringName pid) =>
        _professions.TryGetValue(pid, out UnitProfessionProgress progress) ? progress : null;

    public void RemoveProfessionProgress(StringName pid)
    {
        if (pid == "")
            return;
        if (_professions.TryGetValue(pid, out UnitProfessionProgress existing))
            GodotRefCountedDisposer.DisposeIfValid(existing);
        _professions.Remove(pid);
    }

    public void SetAchievementProgressState(AchievementProgressState aps)
    {
        if (aps == null || aps.achievement_id == "")
            return;
        if (
            _achievementProgress.TryGetValue(
                aps.achievement_id,
                out AchievementProgressState existing
            )
        )
            GodotRefCountedDisposer.DisposeIfValid(existing);
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
        Godot.Collections.Array<StringName> sourceIds
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
            sp.merged_from_skill_ids = BuildStringNameArray(deduped);
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
        ClearSkillProgressStates();
        if (values == null)
            return;
        foreach (Variant rawKey in values.Keys)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawKey);
            if (skillId == "")
                continue;
            UnitSkillProgress skillProgress = values[rawKey].AsGodotObject() as UnitSkillProgress;
            if (skillProgress == null || skillProgress.skill_id == "" || skillProgress.skill_id != skillId)
                continue;
            _skills[skillId] = skillProgress.DuplicateState();
        }
    }

    public void SetSkillProgressStates(
        IEnumerable<KeyValuePair<StringName, UnitSkillProgress>> values
    )
    {
        ClearSkillProgressStates();
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

    private void ClearSkillProgressStates()
    {
        GodotRefCountedDisposer.DisposeAll(_skills.Values);
        _skills.Clear();
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

    public void SetPendingProfessionChoices(System.Collections.IEnumerable values)
    {
        ClearPendingProfessionChoices();
        if (values == null)
            return;
        foreach (var value in values)
        {
            if (value is PendingProfessionChoice choice)
                AddPendingProfessionChoice(choice);
        }
    }

    public void AddPendingProfessionChoice(PendingProfessionChoice choice)
    {
        if (choice == null)
            return;
        _pendingProfessionChoices.Add(choice.DuplicateState());
    }

    public void SetBlockedRelearnSkillIds(IEnumerable values) =>
        SetUniqueStringNames(_blockedRelearnSkillIds, values);

    private void SetAchievementProgressStates(Godot.Collections.Dictionary values)
    {
        ClearAchievementProgressStates();
        if (values == null)
            return;
        foreach (Variant rawKey in values.Keys)
        {
            StringName achievementId = ProgressionDataUtils.to_string_name(rawKey);
            if (achievementId == "")
                continue;
            Variant rawValue = values[rawKey];
            AchievementProgressState progressState =
                rawValue.AsGodotObject() as AchievementProgressState;
            if (progressState == null || progressState.achievement_id != achievementId)
                continue;
            _achievementProgress[achievementId] = progressState.DuplicateState();
        }
    }

    public void SetAchievementProgressStates(
        IEnumerable<KeyValuePair<StringName, AchievementProgressState>> values
    )
    {
        ClearAchievementProgressStates();
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
        ClearProfessionProgressStates();
        if (values == null)
            return;
        foreach (Variant rawKey in values.Keys)
        {
            StringName professionId = ProgressionDataUtils.to_string_name(rawKey);
            if (professionId == "")
                continue;
            UnitProfessionProgress professionProgress =
                values[rawKey].AsGodotObject() as UnitProfessionProgress;
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
        ClearProfessionProgressStates();
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

    private void ClearAchievementProgressStates()
    {
        GodotRefCountedDisposer.DisposeAll(_achievementProgress.Values);
        _achievementProgress.Clear();
    }

    private void ClearProfessionProgressStates()
    {
        GodotRefCountedDisposer.DisposeAll(_professions.Values);
        _professions.Clear();
    }

    private void ClearPendingProfessionChoices()
    {
        GodotRefCountedDisposer.DisposeAll(_pendingProfessionChoices);
        _pendingProfessionChoices.Clear();
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

    public bool HasLockedLevelTriggerSkillId(StringName skillId) =>
        HasStringName(_lockedLevelTriggerSkillIds, skillId);

    public void SetLockedLevelTriggerSkillIds(IEnumerable values) =>
        SetUniqueStringNames(_lockedLevelTriggerSkillIds, values);

    public void AddLockedLevelTriggerSkillId(StringName skillId) =>
        AddUniqueStringName(_lockedLevelTriggerSkillIds, skillId);

    public void RemoveLockedLevelTriggerSkillId(StringName skillId) =>
        _lockedLevelTriggerSkillIds.Remove(skillId);

    public UnitProgress DuplicateState()
    {
        SyncActiveCoreSkillIds();
        SyncDefaultCombatResourceUnlocks();

        var copy = new UnitProgress();
        GodotRefCountedDisposer.DisposeIfValid(copy.unit_base_attributes);
        GodotRefCountedDisposer.DisposeIfValid(copy.reputation_state);
        copy.version = version;
        copy.unit_id = unit_id;
        copy.display_name = display_name;
        copy.character_level = character_level;
        copy.unit_base_attributes = unit_base_attributes?.DuplicateState() ?? new UnitBaseAttributes();
        copy.reputation_state = reputation_state?.DuplicateState() ?? new UnitReputationState();
        copy.active_level_trigger_core_skill_id = active_level_trigger_core_skill_id;
        copy.SetKnownKnowledgeIds(_knownKnowledgeIds);
        copy.SetActiveCoreSkillIds(_activeCoreSkillIds);
        copy.SetSkillProgressStates(_skills);
        copy.SetAttributeGrowthProgress(_attributeGrowthProgress);
        copy.SetAchievementProgressStates(_achievementProgress);
        copy.SetProfessionProgressStates(_professions);
        copy.SetBlockedRelearnSkillIds(_blockedRelearnSkillIds);
        copy.SetMergedSkillSourceMap(_mergedSkillSourceMap);
        copy.SetUnlockedCombatResourceIds(_unlockedCombatResourceIds);
        copy.SetLockedLevelTriggerSkillIds(_lockedLevelTriggerSkillIds);

        foreach (StringName skillId in GetSortedSkillIdsTyped())
        {
            var skillProgress = GetSkillProgress(skillId);
            if (skillProgress != null)
                copy.SetSkillProgress(skillProgress.DuplicateState());
        }

        foreach (StringName professionId in GetSortedProfessionIdsTyped())
        {
            var professionProgress = GetProfessionProgress(professionId);
            if (professionProgress != null)
                copy.SetProfessionProgress(professionProgress.DuplicateState());
        }

        foreach (var key in ProgressionDataUtils.sorted_string_keys(BuildAchievementProgressDictionary()))
        {
            var progressState = GetAchievementProgressState(new StringName(key));
            if (progressState != null)
                copy.SetAchievementProgressState(progressState);
        }

        foreach (var pendingChoice in _pendingProfessionChoices)
            if (pendingChoice != null)
                copy.AddPendingProfessionChoice(pendingChoice);

        return copy;
    }

    private static Godot.Collections.Array<StringName> DuplicateStringNameArray(
        Godot.Collections.Array<StringName> values
    )
    {
        return values != null
            ? new Godot.Collections.Array<StringName>(values)
            : new Godot.Collections.Array<StringName>();
    }

    private static Godot.Collections.Dictionary DuplicateDictionary(
        Godot.Collections.Dictionary values
    )
    {
        return values?.Duplicate(true) ?? new Godot.Collections.Dictionary();
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
        var pcd = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var pc in _pendingProfessionChoices)
            if (pc != null)
                pcd.Add(pc.ToDictionary());
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
            { "pending_profession_choices", pcd },
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
            { "active_level_trigger_core_skill_id", (string)active_level_trigger_core_skill_id },
            {
                "locked_level_trigger_skill_ids",
                ProgressionDataUtils.string_name_array_to_string_array(
                    _lockedLevelTriggerSkillIds
                )
            },
        };
    }

    public static UnitProgress FromDictionary(Godot.Collections.Dictionary data)
    {
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
                "pending_profession_choices",
                out Godot.Collections.Array pendingProfessionChoiceValues
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
        if (
            !TryGetArray(
                data,
                "locked_level_trigger_skill_ids",
                out Godot.Collections.Array lockedLevelTriggerSkillIdValues
            )
        )
            return null;
        if (!TryGetStrictInt(data, "version", out int versionValue) || versionValue != 1)
            return null;

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
        var parsedActiveLevelTriggerCoreSkillId = _parse_optional_string_name(
            data,
            "active_level_trigger_core_skill_id",
            out bool activeTriggerOk
        );
        if (!activeTriggerOk)
            return null;
        var parsedLockedLevelTriggerSkillIds = _parse_unique_string_name_array(
            lockedLevelTriggerSkillIdValues
        );
        if (parsedLockedLevelTriggerSkillIds == null)
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
        {
            GodotRefCountedDisposer.DisposeIfValid(unitBaseAttributes);
            GodotRefCountedDisposer.DisposeIfValid(reputationState);
            return null;
        }

        var progress = new UnitProgress();
        GodotRefCountedDisposer.DisposeIfValid(progress.unit_base_attributes);
        GodotRefCountedDisposer.DisposeIfValid(progress.reputation_state);
        progress.version = versionValue;
        progress.unit_id = parsedUnitId;
        progress.display_name = parsedDisplayName;
        progress.character_level = characterLevelValue;
        progress.unit_base_attributes = unitBaseAttributes;
        progress.reputation_state = reputationState;
        progress.active_level_trigger_core_skill_id = parsedActiveLevelTriggerCoreSkillId;
        progress.SetKnownKnowledgeIds(parsedKnownKnowledgeIds);
        progress.SetBlockedRelearnSkillIds(parsedBlockedRelearnSkillIds);
        progress.SetAttributeGrowthProgress(parsedAttributeGrowthProgress);
        progress.SetMergedSkillSourceMap(parsedMergedSkillSourceMap);
        progress.SetUnlockedCombatResourceIds(parsedUnlockedResources);
        progress.SetLockedLevelTriggerSkillIds(parsedLockedLevelTriggerSkillIds);
        progress.SyncDefaultCombatResourceUnlocks();

        foreach (var key in skillsData.Keys)
        {
            var skillId = ProgressionDataUtils.to_string_name(key);
            if (skillId == "" || progress._skills.ContainsKey(skillId))
                return RejectParsedProgress(progress);
            var skillProgressPayload = skillsData[key];
            if (!TryAsDictionary(skillProgressPayload, out Godot.Collections.Dictionary skillData))
                return RejectParsedProgress(progress);
            var skillProgress = UnitSkillProgress.FromDictionary(skillData);
            if (
                skillProgress == null
                || skillProgress.skill_id == ""
                || skillProgress.skill_id != skillId
            )
            {
                GodotRefCountedDisposer.DisposeIfValid(skillProgress);
                return RejectParsedProgress(progress);
            }
            progress.SetSkillProgress(skillProgress);
            if (skillProgress.merged_from_skill_ids.Count > 0)
                progress.RememberMergeSources(
                    skillProgress.skill_id,
                    skillProgress.merged_from_skill_ids
                );
        }

        if (!_has_valid_level_trigger_state(progress))
            return RejectParsedProgress(progress);

        foreach (var key in professionsData.Keys)
        {
            var professionId = ProgressionDataUtils.to_string_name(key);
            if (professionId == "" || progress._professions.ContainsKey(professionId))
                return RejectParsedProgress(progress);
            var professionProgressPayload = professionsData[key];
            if (
                !TryAsDictionary(
                    professionProgressPayload,
                    out Godot.Collections.Dictionary professionData
                )
            )
                return RejectParsedProgress(progress);
            var professionProgress = UnitProfessionProgress.FromDictionary(professionData);
            if (
                professionProgress == null
                || professionProgress.profession_id == ""
                || professionProgress.profession_id != professionId
            )
            {
                GodotRefCountedDisposer.DisposeIfValid(professionProgress);
                return RejectParsedProgress(progress);
            }
            progress.SetProfessionProgress(professionProgress);
        }

        foreach (var key in achievementProgressData.Keys)
        {
            var achievementId = ProgressionDataUtils.to_string_name(key);
            if (achievementId == "" || progress._achievementProgress.ContainsKey(achievementId))
                return RejectParsedProgress(progress);
            var achievementProgressPayload = achievementProgressData[key];
            if (
                !TryAsDictionary(
                    achievementProgressPayload,
                    out Godot.Collections.Dictionary achievementData
                )
            )
                return RejectParsedProgress(progress);
            var progressState = AchievementProgressState.FromDictionary(achievementData);
            if (
                progressState == null
                || progressState.achievement_id == ""
                || progressState.achievement_id != achievementId
            )
            {
                GodotRefCountedDisposer.DisposeIfValid(progressState);
                return RejectParsedProgress(progress);
            }
            progress.SetAchievementProgressState(progressState);
            GodotRefCountedDisposer.DisposeIfValid(progressState);
        }

        foreach (var pendingChoiceData in pendingProfessionChoiceValues)
        {
            if (
                !TryAsDictionary(
                    pendingChoiceData,
                    out Godot.Collections.Dictionary pendingChoicePayload
                )
            )
                return RejectParsedProgress(progress);
            var pendingChoice = PendingProfessionChoice.FromDictionary(pendingChoicePayload);
            if (pendingChoice == null)
                return RejectParsedProgress(progress);
            progress.AddPendingProfessionChoice(pendingChoice);
            GodotRefCountedDisposer.DisposeIfValid(pendingChoice);
        }

        progress.SetActiveCoreSkillIds(parsedActiveCoreSkillIds);
        progress.SyncActiveCoreSkillIds();
        return progress;
    }

    private static UnitProgress RejectParsedProgress(UnitProgress progress)
    {
        GodotRefCountedDisposer.DisposeIfValid(progress);
        return null;
    }

    private static bool _hef(Godot.Collections.Dictionary d, Godot.Collections.Array<string> e)
    {
        if (d.Count != e.Count)
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

    private static Godot.Collections.Array<StringName> _parse_unique_string_name_array(
        Godot.Collections.Array values
    )
    {
        var parsed = new Godot.Collections.Array<StringName>();
        var seen = new Godot.Collections.Dictionary();
        foreach (var raw in values)
        {
            if (!TryAsStringLike(raw, out string rawText))
                return null;
            var value = new StringName(rawText);
            if (value == "" || seen.ContainsKey(value))
                return null;
            seen[value] = true;
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

    private static Godot.Collections.Array<StringName> BuildStringNameArray(
        IEnumerable<StringName> values
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }

    private Godot.Collections.Array<PendingProfessionChoice> BuildPendingProfessionChoicesArray()
    {
        var result = new Godot.Collections.Array<PendingProfessionChoice>();
        foreach (PendingProfessionChoice choice in _pendingProfessionChoices)
            if (choice != null)
                result.Add(choice.DuplicateState());
        return result;
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
                result[skillId] = value?.DuplicateState();
        }
        return result;
    }

    private Godot.Collections.Dictionary BuildProfessionDictionary()
    {
        var result = new Godot.Collections.Dictionary();
        foreach (StringName professionId in GetSortedProfessionIdsTyped())
        {
            if (_professions.TryGetValue(professionId, out UnitProfessionProgress value))
                result[professionId] = value?.DuplicateState();
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
                result[achievementId] = value?.DuplicateState();
        }
        return result;
    }

    private static bool _has_valid_level_trigger_state(UnitProgress progress)
    {
        if (progress == null)
            return false;
        var activeSkillId = progress.active_level_trigger_core_skill_id;
        int activeFlagCount = 0;
        var activeFlagSkillId = new StringName("");
        var lockedFlagLookup = new Godot.Collections.Dictionary();

        foreach (var skillId in progress.GetSortedSkillIdsTyped())
        {
            var skillProgress = progress.GetSkillProgress(skillId);
            if (skillProgress == null)
                return false;
            if (skillProgress.is_level_trigger_active)
            {
                activeFlagCount += 1;
                activeFlagSkillId = skillId;
                if (skillProgress.is_level_trigger_locked)
                    return false;
            }
            if (skillProgress.is_level_trigger_locked)
            {
                lockedFlagLookup[skillId] = true;
                if (skillProgress.is_level_trigger_active)
                    return false;
                if (!skillProgress.is_learned || !skillProgress.is_core)
                    return false;
            }
        }

        if (activeFlagCount > 1)
            return false;
        if (activeSkillId == "")
        {
            if (activeFlagCount != 0)
                return false;
        }
        else
        {
            var activeSkillProgress = progress.GetSkillProgress(activeSkillId);
            if (activeSkillProgress == null)
                return false;
            if (activeFlagCount != 1 || activeFlagSkillId != activeSkillId)
                return false;
            if (!activeSkillProgress.is_learned || !activeSkillProgress.is_core)
                return false;
            if (activeSkillProgress.is_level_trigger_locked)
                return false;
            if (progress.HasLockedLevelTriggerSkillId(activeSkillId))
                return false;
        }

        var lockedListLookup = new Godot.Collections.Dictionary();
        foreach (var lockedSkillId in progress.LockedLevelTriggerSkillIdsTyped)
        {
            if (lockedSkillId == "" || lockedListLookup.ContainsKey(lockedSkillId))
                return false;
            var lockedSkillProgress = progress.GetSkillProgress(lockedSkillId);
            if (lockedSkillProgress == null)
                return false;
            if (!lockedSkillProgress.is_learned || !lockedSkillProgress.is_core)
                return false;
            if (lockedSkillProgress.is_level_trigger_active)
                return false;
            if (!lockedSkillProgress.is_level_trigger_locked)
                return false;
            lockedListLookup[lockedSkillId] = true;
        }

        if (lockedListLookup.Count != lockedFlagLookup.Count)
            return false;
        foreach (var lockedSkillId in lockedFlagLookup.Keys)
            if (!lockedListLookup.ContainsKey(lockedSkillId))
                return false;
        return true;
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
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Int)
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
