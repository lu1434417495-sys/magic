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
    private static readonly StringName CombatResourceHp = "hp";
    private static readonly StringName CombatResourceStamina = "stamina";
    private static readonly StringName CombatResourceMp = "mp";
    private static readonly StringName CombatResourceAura = "aura";

    public static StringName COMBAT_RESOURCE_HP() => CombatResourceHp;

    public static StringName COMBAT_RESOURCE_STAMINA() => CombatResourceStamina;

    public static StringName COMBAT_RESOURCE_MP() => CombatResourceMp;

    public static StringName COMBAT_RESOURCE_AURA() => CombatResourceAura;

    public static Godot.Collections.Array<StringName> DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS() =>
        new() { CombatResourceHp, CombatResourceStamina };

    public static Godot.Collections.Array<StringName> VALID_COMBAT_RESOURCE_IDS() =>
        new() { CombatResourceHp, CombatResourceStamina, CombatResourceMp, CombatResourceAura };

    public int version = 1;
    public StringName unit_id = "";
    public string display_name = "";
    public int character_level;
    public UnitBaseAttributes unit_base_attributes = new UnitBaseAttributes();
    public UnitReputationState reputation_state = new UnitReputationState();
    public Godot.Collections.Dictionary skills = new();
    public Godot.Collections.Dictionary professions = new();
    public Godot.Collections.Array<StringName> known_knowledge_ids = new();
    public Godot.Collections.Array<StringName> active_core_skill_ids = new();
    public Godot.Collections.Dictionary attribute_growth_progress = new();
    public Godot.Collections.Dictionary achievement_progress = new();
    public Godot.Collections.Array<PendingProfessionChoice> pending_profession_choices = new();
    public Godot.Collections.Array<StringName> blocked_relearn_skill_ids = new();
    public Godot.Collections.Dictionary merged_skill_source_map = new();
    public Godot.Collections.Array<StringName> unlocked_combat_resource_ids =
        DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS();
    public StringName active_level_trigger_core_skill_id = "";
    public Godot.Collections.Array<StringName> locked_level_trigger_skill_ids = new();

    public void set_skill_progress(UnitSkillProgress sp)
    {
        if (sp == null)
            return;
        skills[sp.skill_id] = sp;
        if (sp.merged_from_skill_ids.Count > 0)
            remember_merge_sources(sp.skill_id, sp.merged_from_skill_ids);
        sync_active_core_skill_ids();
    }

    public UnitSkillProgress get_skill_progress(StringName sid) =>
        skills.ContainsKey(sid) ? skills[sid].AsGodotObject() as UnitSkillProgress : null;

    public void remove_skill_progress(StringName sid)
    {
        skills.Remove(sid);
        sync_active_core_skill_ids();
    }

    public void set_profession_progress(UnitProfessionProgress pp)
    {
        if (pp != null)
            professions[pp.profession_id] = pp;
    }

    public UnitProfessionProgress get_profession_progress(StringName pid) =>
        professions.ContainsKey(pid)
            ? professions[pid].AsGodotObject() as UnitProfessionProgress
            : null;

    public void set_achievement_progress_state(AchievementProgressState aps)
    {
        if (aps != null && aps.achievement_id != "")
            achievement_progress[aps.achievement_id] = aps;
    }

    public AchievementProgressState get_achievement_progress_state(StringName aid) =>
        achievement_progress.ContainsKey(aid)
            ? achievement_progress[aid].AsGodotObject() as AchievementProgressState
            : null;

    public bool has_knowledge(StringName kid) => kid != "" && known_knowledge_ids.Contains(kid);

    public bool learn_knowledge(StringName kid)
    {
        if (kid == "" || has_knowledge(kid))
            return false;
        known_knowledge_ids.Add(kid);
        return true;
    }

    public void sync_active_core_skill_ids()
    {
        var next = new Godot.Collections.Array<StringName>();
        foreach (var k in ProgressionDataUtils.sorted_string_keys(skills))
        {
            var sid = new StringName(k);
            var sp = get_skill_progress(sid);
            if (sp != null && sp.is_learned && sp.is_core)
                next.Add(sid);
        }
        active_core_skill_ids = next;
    }

    public bool is_skill_relearn_blocked(StringName sid) => blocked_relearn_skill_ids.Contains(sid);

    public void block_skill_relearn(StringName sid)
    {
        if (!blocked_relearn_skill_ids.Contains(sid))
            blocked_relearn_skill_ids.Add(sid);
    }

    public void remember_merge_sources(
        StringName sid,
        Godot.Collections.Array<StringName> sourceIds
    )
    {
        var deduped = new Godot.Collections.Array<StringName>();
        var seen = new Godot.Collections.Dictionary();
        foreach (var s in sourceIds)
        {
            if (s == sid || seen.ContainsKey(s))
                continue;
            seen[s] = true;
            deduped.Add(s);
        }
        merged_skill_source_map[sid] = deduped;
        var sp = get_skill_progress(sid);
        if (sp != null)
            sp.merged_from_skill_ids = new Godot.Collections.Array<StringName>(deduped);
    }

    public Godot.Collections.Array<StringName> get_merged_source_skill_ids(StringName sid)
    {
        if (merged_skill_source_map.ContainsKey(sid))
            return ProgressionDataUtils.to_string_name_array(merged_skill_source_map[sid]);
        var sp = get_skill_progress(sid);
        if (sp != null && sp.merged_from_skill_ids.Count > 0)
            return new Godot.Collections.Array<StringName>(sp.merged_from_skill_ids);
        return new Godot.Collections.Array<StringName>();
    }

    public Godot.Collections.Array<StringName> get_merged_source_skill_ids_recursive(StringName sid)
    {
        var r = new Godot.Collections.Array<StringName>();
        var visited = new Godot.Collections.Dictionary();
        foreach (var s in get_merged_source_skill_ids(sid))
            _append_recursive_merge_source(s, r, visited);
        return r;
    }

    private void _append_recursive_merge_source(
        StringName sid,
        Godot.Collections.Array<StringName> results,
        Godot.Collections.Dictionary visited
    )
    {
        if (visited.ContainsKey(sid))
            return;
        foreach (var ns in get_merged_source_skill_ids(sid))
            _append_recursive_merge_source(ns, results, visited);
        if (visited.ContainsKey(sid))
            return;
        visited[sid] = true;
        results.Add(sid);
    }

    public void sync_default_combat_resource_unlocks()
    {
        foreach (var rid in DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS())
            unlock_combat_resource(rid);
    }

    public bool has_combat_resource_unlocked(StringName rid) =>
        unlocked_combat_resource_ids.Contains(rid);

    public bool unlock_combat_resource(StringName rid)
    {
        if (
            rid == ""
            || !VALID_COMBAT_RESOURCE_IDS().Contains(rid)
            || unlocked_combat_resource_ids.Contains(rid)
        )
            return false;
        unlocked_combat_resource_ids.Add(rid);
        return true;
    }

    public Godot.Collections.Dictionary to_dict()
    {
        sync_active_core_skill_ids();
        sync_default_combat_resource_unlocks();
        var sd = new Godot.Collections.Dictionary();
        foreach (var k in ProgressionDataUtils.sorted_string_keys(skills))
        {
            var sp = get_skill_progress(new StringName(k));
            if (sp != null)
                sd[k] = sp.to_dict();
        }
        var pd = new Godot.Collections.Dictionary();
        foreach (var k in ProgressionDataUtils.sorted_string_keys(professions))
        {
            var pp = get_profession_progress(new StringName(k));
            if (pp != null)
                pd[k] = pp.to_dict();
        }
        var pcd = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var pc in pending_profession_choices)
            if (pc != null)
                pcd.Add(pc.to_dict());
        var ad = new Godot.Collections.Dictionary();
        foreach (var k in ProgressionDataUtils.sorted_string_keys(achievement_progress))
        {
            var ap = get_achievement_progress_state(new StringName(k));
            if (ap != null)
                ad[k] = ap.to_dict();
        }
        return new Godot.Collections.Dictionary
        {
            { "version", version },
            { "unit_id", (string)unit_id },
            { "display_name", display_name },
            { "character_level", character_level },
            {
                "unit_base_attributes",
                unit_base_attributes?.to_dict() ?? new Godot.Collections.Dictionary()
            },
            {
                "reputation_state",
                reputation_state?.to_dict() ?? new Godot.Collections.Dictionary()
            },
            { "skills", sd },
            { "professions", pd },
            {
                "known_knowledge_ids",
                ProgressionDataUtils.string_name_array_to_string_array(known_knowledge_ids)
            },
            {
                "active_core_skill_ids",
                ProgressionDataUtils.string_name_array_to_string_array(active_core_skill_ids)
            },
            {
                "attribute_growth_progress",
                ProgressionDataUtils.string_name_int_map_to_string_dict(attribute_growth_progress)
            },
            { "achievement_progress", ad },
            { "pending_profession_choices", pcd },
            {
                "blocked_relearn_skill_ids",
                ProgressionDataUtils.string_name_array_to_string_array(blocked_relearn_skill_ids)
            },
            {
                "merged_skill_source_map",
                ProgressionDataUtils.string_name_array_map_to_string_dict(merged_skill_source_map)
            },
            {
                "unlocked_combat_resource_ids",
                ProgressionDataUtils.string_name_array_to_string_array(unlocked_combat_resource_ids)
            },
            { "active_level_trigger_core_skill_id", (string)active_level_trigger_core_skill_id },
            {
                "locked_level_trigger_skill_ids",
                ProgressionDataUtils.string_name_array_to_string_array(
                    locked_level_trigger_skill_ids
                )
            },
        };
    }

    public static UnitProgress from_dict(Godot.Collections.Dictionary data)
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
            if (!VALID_COMBAT_RESOURCE_IDS().Contains(resourceId))
                return null;
        foreach (var defaultResourceId in DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS())
            if (!parsedUnlockedResources.Contains(defaultResourceId))
                return null;

        var unitBaseAttributes = UnitBaseAttributes.from_dict(unitBaseAttributesData);
        var reputationState = UnitReputationState.from_dict(reputationStateData);
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
            known_knowledge_ids = parsedKnownKnowledgeIds,
            attribute_growth_progress = parsedAttributeGrowthProgress,
            blocked_relearn_skill_ids = parsedBlockedRelearnSkillIds,
            merged_skill_source_map = parsedMergedSkillSourceMap,
            unlocked_combat_resource_ids = parsedUnlockedResources,
            active_level_trigger_core_skill_id = parsedActiveLevelTriggerCoreSkillId,
            locked_level_trigger_skill_ids = parsedLockedLevelTriggerSkillIds,
        };
        progress.sync_default_combat_resource_unlocks();

        foreach (var key in skillsData.Keys)
        {
            var skillId = ProgressionDataUtils.to_string_name(key);
            if (skillId == "" || progress.skills.ContainsKey(skillId))
                return null;
            var skillProgressPayload = skillsData[key];
            if (!TryAsDictionary(skillProgressPayload, out Godot.Collections.Dictionary skillData))
                return null;
            var skillProgress = UnitSkillProgress.from_dict(skillData);
            if (
                skillProgress == null
                || skillProgress.skill_id == ""
                || skillProgress.skill_id != skillId
            )
                return null;
            progress.skills[skillProgress.skill_id] = skillProgress;
            if (skillProgress.merged_from_skill_ids.Count > 0)
                progress.merged_skill_source_map[skillProgress.skill_id] =
                    new Godot.Collections.Array<StringName>(skillProgress.merged_from_skill_ids);
        }

        if (!_has_valid_level_trigger_state(progress))
            return null;

        foreach (var key in professionsData.Keys)
        {
            var professionId = ProgressionDataUtils.to_string_name(key);
            if (professionId == "" || progress.professions.ContainsKey(professionId))
                return null;
            var professionProgressPayload = professionsData[key];
            if (
                !TryAsDictionary(
                    professionProgressPayload,
                    out Godot.Collections.Dictionary professionData
                )
            )
                return null;
            var professionProgress = UnitProfessionProgress.from_dict(professionData);
            if (
                professionProgress == null
                || professionProgress.profession_id == ""
                || professionProgress.profession_id != professionId
            )
                return null;
            progress.professions[professionProgress.profession_id] = professionProgress;
        }

        foreach (var key in achievementProgressData.Keys)
        {
            var achievementId = ProgressionDataUtils.to_string_name(key);
            if (achievementId == "" || progress.achievement_progress.ContainsKey(achievementId))
                return null;
            var achievementProgressPayload = achievementProgressData[key];
            if (
                !TryAsDictionary(
                    achievementProgressPayload,
                    out Godot.Collections.Dictionary achievementData
                )
            )
                return null;
            var progressState = AchievementProgressState.from_dict(achievementData);
            if (
                progressState == null
                || progressState.achievement_id == ""
                || progressState.achievement_id != achievementId
            )
                return null;
            progress.achievement_progress[progressState.achievement_id] = progressState;
        }

        foreach (var pendingChoiceData in pendingProfessionChoiceValues)
        {
            if (
                !TryAsDictionary(
                    pendingChoiceData,
                    out Godot.Collections.Dictionary pendingChoicePayload
                )
            )
                return null;
            var pendingChoice = PendingProfessionChoice.from_dict(pendingChoicePayload);
            if (pendingChoice == null)
                return null;
            progress.pending_profession_choices.Add(pendingChoice);
        }

        progress.active_core_skill_ids = parsedActiveCoreSkillIds;
        progress.sync_active_core_skill_ids();
        return progress;
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

    private static Godot.Collections.Dictionary _parse_nonnegative_int_map(
        Godot.Collections.Dictionary values
    )
    {
        var parsed = new Godot.Collections.Dictionary();
        var seen = new Godot.Collections.Dictionary();
        foreach (var rawKey in values.Keys)
        {
            if (!TryAsStringLike(rawKey, out string rawKeyText))
                return null;
            var key = new StringName(rawKeyText);
            if (key == "" || seen.ContainsKey(key))
                return null;
            var rawValue = values[rawKey];
            if (!TryAsStrictInt(rawValue, out int parsedValue) || parsedValue < 0)
                return null;
            seen[key] = true;
            parsed[key] = parsedValue;
        }
        return parsed;
    }

    private static Godot.Collections.Dictionary _parse_string_name_array_map(
        Godot.Collections.Dictionary values
    )
    {
        var parsed = new Godot.Collections.Dictionary();
        var seen = new Godot.Collections.Dictionary();
        foreach (var rawKey in values.Keys)
        {
            if (!TryAsStringLike(rawKey, out string rawKeyText))
                return null;
            var key = new StringName(rawKeyText);
            if (key == "" || seen.ContainsKey(key))
                return null;
            var rawValues = values[rawKey];
            if (!TryAsArray(rawValues, out Godot.Collections.Array sourceValues))
                return null;
            var parsedArray = _parse_unique_string_name_array(sourceValues);
            if (parsedArray == null)
                return null;
            seen[key] = true;
            parsed[key] = parsedArray;
        }
        return parsed;
    }

    private static bool _has_valid_level_trigger_state(UnitProgress progress)
    {
        if (progress == null)
            return false;
        var activeSkillId = progress.active_level_trigger_core_skill_id;
        int activeFlagCount = 0;
        var activeFlagSkillId = new StringName("");
        var lockedFlagLookup = new Godot.Collections.Dictionary();

        foreach (var rawSkillId in progress.skills.Keys)
        {
            var skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            var skillProgress = progress.get_skill_progress(skillId);
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
            var activeSkillProgress = progress.get_skill_progress(activeSkillId);
            if (activeSkillProgress == null)
                return false;
            if (activeFlagCount != 1 || activeFlagSkillId != activeSkillId)
                return false;
            if (!activeSkillProgress.is_learned || !activeSkillProgress.is_core)
                return false;
            if (activeSkillProgress.is_level_trigger_locked)
                return false;
            if (progress.locked_level_trigger_skill_ids.Contains(activeSkillId))
                return false;
        }

        var lockedListLookup = new Godot.Collections.Dictionary();
        foreach (var lockedSkillId in progress.locked_level_trigger_skill_ids)
        {
            if (lockedSkillId == "" || lockedListLookup.ContainsKey(lockedSkillId))
                return false;
            var lockedSkillProgress = progress.get_skill_progress(lockedSkillId);
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
