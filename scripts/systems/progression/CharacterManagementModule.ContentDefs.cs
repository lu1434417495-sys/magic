using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// Partial slice of CharacterManagementModule — content-def getters + quest-submit preview + index/clone/project helpers.
// Pure physical split: same class, no behavior change. See CharacterManagementModule.cs.
public sealed partial class CharacterManagementModule
{

    private SkillDefinition GetSkillDefinition(StringName skillId) =>
        skillId != "" && _skill_definition_index.TryGetValue(skillId, out var skillDefinition)
            ? skillDefinition
            : null;

    private AchievementDefinition GetAchievementDef(StringName achievementId) =>
        achievementId != ""
        && _achievement_def_index.TryGetValue(achievementId, out var achievementDef)
            ? achievementDef
            : null;

    public ItemDefinition GetItemDef(StringName itemId) =>
        itemId != "" && _item_def_index.TryGetValue(itemId, out var itemDef) ? itemDef : null;

    private RaceDefinition GetRaceDef(StringName raceId) =>
        raceId != "" && _race_def_index.TryGetValue(raceId, out var raceDef) ? raceDef : null;

    private SubraceDefinition GetSubraceDef(StringName subraceId) =>
        subraceId != "" && _subrace_def_index.TryGetValue(subraceId, out var subraceDef)
            ? subraceDef
            : null;

    private AgeProfileDefinition GetAgeProfileDef(StringName profileId) =>
        profileId != "" && _age_profile_def_index.TryGetValue(profileId, out var ageProfileDef)
            ? ageProfileDef
            : null;

    private BloodlineDefinition GetBloodlineDef(StringName bloodlineId) =>
        bloodlineId != ""
        && _bloodline_def_index.TryGetValue(bloodlineId, out var bloodlineDef)
            ? bloodlineDef
            : null;

    private BloodlineStageDefinition GetBloodlineStageDef(StringName stageId) =>
        stageId != ""
        && _bloodline_stage_def_index.TryGetValue(stageId, out var bloodlineStageDef)
            ? bloodlineStageDef
            : null;

    private AscensionDefinition GetAscensionDef(StringName ascensionId) =>
        ascensionId != ""
        && _ascension_def_index.TryGetValue(ascensionId, out var ascensionDef)
            ? ascensionDef
            : null;

    private AscensionStageDefinition GetAscensionStageDef(StringName stageId) =>
        stageId != ""
        && _ascension_stage_def_index.TryGetValue(stageId, out var ascensionStageDef)
            ? ascensionStageDef
            : null;

    private static string _identity_def_label(object definition, StringName fallback_id)
    {
        if (definition != null)
        {
            string displayName = definition switch
            {
                RaceDefinition raceDef => raceDef.DisplayName,
                SubraceDefinition subraceDef => subraceDef.DisplayName,
                BloodlineDefinition bloodlineDef => bloodlineDef.DisplayName,
                BloodlineStageDefinition bloodlineStageDef => bloodlineStageDef.DisplayName,
                AscensionDefinition ascensionDef => ascensionDef.DisplayName,
                AscensionStageDefinition ascensionStageDef => ascensionStageDef.DisplayName,
                _ => "",
            };
            displayName = displayName.StripEdges();
            if (displayName.Length > 0)
                return displayName;
        }
        return fallback_id != "" ? (string)fallback_id : "";
    }

    private string _get_age_stage_display_label(StringName age_profile_id, StringName stage_id)
    {
        if (stage_id == "")
            return "";
        var age_profile = GetAgeProfileDef(age_profile_id);
        if (age_profile != null)
        {
            foreach (AgeStageRuleDefinition stage_rule in age_profile.StageRules)
            {
                if (stage_rule == null || stage_rule.StageId != stage_id)
                    continue;
                if (!string.IsNullOrEmpty(stage_rule.DisplayName))
                    return stage_rule.DisplayName;
                break;
            }
        }
        return (string)stage_id;
    }

    private GArray _build_identity_trait_summary_lines(
        RaceDefinition race_def,
        SubraceDefinition subrace_def,
        AgeStageRuleDefinition age_stage_rule,
        BloodlineDefinition bloodline_def,
        BloodlineStageDefinition bloodline_stage_def,
        AscensionDefinition ascension_def,
        AscensionStageDefinition ascension_stage_def
    )
    {
        var lines = new GArray();
        if (race_def != null)
            _append_identity_text_lines(lines, race_def.RacialTraitSummary);
        if (subrace_def != null)
            _append_identity_text_lines(lines, subrace_def.RacialTraitSummary);
        if (age_stage_rule != null)
            _append_identity_text_lines(lines, age_stage_rule.TraitSummary);
        if (bloodline_def != null)
            _append_identity_text_lines(lines, bloodline_def.TraitSummary);
        if (bloodline_stage_def != null)
            _append_identity_text_lines(lines, bloodline_stage_def.TraitSummary);
        if (ascension_def != null)
            _append_identity_text_lines(lines, ascension_def.TraitSummary);
        if (ascension_stage_def != null)
            _append_identity_text_lines(lines, ascension_stage_def.TraitSummary);
        return lines;
    }

    private static void _append_identity_text_lines(
        GArray target,
        IReadOnlyList<string> values
    )
    {
        foreach (var value in values)
        {
            var text = (value ?? "").StripEdges();
            if (text.Length == 0 || target.Contains(text))
                continue;
            target.Add(text);
        }
    }

    private static GDictionary _collect_identity_damage_resistances(
        RaceDefinition race_def,
        SubraceDefinition subrace_def
    )
    {
        var result = new GDictionary();
        if (race_def != null)
            _merge_identity_string_name_map(result, race_def.DamageResistances);
        if (subrace_def != null)
            _merge_identity_string_name_map(result, subrace_def.DamageResistances);
        return result;
    }

    private static void _merge_identity_string_name_map(
        GDictionary target,
        IReadOnlyDictionary<StringName, StringName> source
    )
    {
        foreach ((StringName key, StringName value) in source)
        {
            if (key == "" || value == "")
                continue;
            target[key] = value;
        }
    }

    private static GStringNameArray _collect_identity_save_advantage_tags(
        RaceDefinition race_def,
        SubraceDefinition subrace_def
    )
    {
        var tags = new GStringNameArray();
        if (race_def != null)
            _append_unique_string_names(tags, race_def.SaveAdvantageTags);
        if (subrace_def != null)
            _append_unique_string_names(tags, subrace_def.SaveAdvantageTags);
        return tags;
    }

    private GArray _build_identity_granted_skill_lines(
        RaceDefinition race_def,
        SubraceDefinition subrace_def,
        BloodlineDefinition bloodline_def,
        BloodlineStageDefinition bloodline_stage_def,
        AscensionDefinition ascension_def,
        AscensionStageDefinition ascension_stage_def
    )
    {
        var lines = new GArray();
        if (race_def != null)
            _append_identity_granted_skill_lines(
                lines,
                race_def.RacialGrantedSkills,
                _identity_def_label(race_def, race_def.RaceId)
            );
        if (subrace_def != null)
            _append_identity_granted_skill_lines(
                lines,
                subrace_def.RacialGrantedSkills,
                _identity_def_label(subrace_def, subrace_def.SubraceId)
            );
        if (bloodline_def != null)
            _append_identity_granted_skill_lines(
                lines,
                bloodline_def.RacialGrantedSkills,
                _identity_def_label(bloodline_def, bloodline_def.BloodlineId)
            );
        if (bloodline_stage_def != null)
            _append_identity_granted_skill_lines(
                lines,
                bloodline_stage_def.RacialGrantedSkills,
                _identity_def_label(bloodline_stage_def, bloodline_stage_def.StageId)
            );
        if (ascension_def != null)
            _append_identity_granted_skill_lines(
                lines,
                ascension_def.RacialGrantedSkills,
                _identity_def_label(ascension_def, ascension_def.AscensionId)
            );
        if (ascension_stage_def != null)
            _append_identity_granted_skill_lines(
                lines,
                ascension_stage_def.RacialGrantedSkills,
                _identity_def_label(ascension_stage_def, ascension_stage_def.StageId)
            );
        return lines;
    }

    private void _append_identity_granted_skill_lines(
        GArray target,
        IReadOnlyList<RacialGrantedSkillDefinition> grants,
        string source_label
    )
    {
        foreach (RacialGrantedSkillDefinition grant in grants)
        {
            if (grant == null || grant.SkillId == "")
                continue;
            var line =
                $"{_resolve_skill_label(grant.SkillId)}（{source_label}，{_format_identity_grant_charges(grant)}）";
            if (!target.Contains(line))
                target.Add(line);
        }
    }

    private static string _format_identity_grant_charges(
        RacialGrantedSkillDefinition grant
    )
    {
        if (grant == null)
            return "无次数";
        return grant.ChargeKindKind switch
        {
            RacialSkillChargeKind.AtWill => "随意",
            RacialSkillChargeKind.PerTurn => $"每回合 {Mathf.Max(grant.Charges, 0)} 次",
            RacialSkillChargeKind.PerBattle => $"每场战斗 {Mathf.Max(grant.Charges, 0)} 次",
            _ => $"{(string)grant.ChargeKind} {Mathf.Max(grant.Charges, 0)}",
        };
    }

    private AttributeService _build_attribute_service(PartyMemberState member_state) =>
        _build_attribute_service(member_state, null);

    private AttributeService _build_attribute_service(
        PartyMemberState member_state,
        EquipmentState equipment_state_override
    )
    {
        var attribute_service = new AttributeService();
        attribute_service.SetupContext(
            build_attribute_source_context(member_state.member_id, equipment_state_override)
        );
        return attribute_service;
    }

    private CharacterProgressionDelta _grant_skill_mastery_internal(
        StringName member_id,
        StringName skill_id,
        int amount,
        StringName source_type,
        string source_label,
        string reason_text,
        bool emit_achievement_event
    )
    {
        var member_state = GetMemberState(member_id);
        var delta = _new_delta(member_id);
        if (
            member_state == null
            || member_state.progression is not UnitProgress progression
            || amount <= 0
        )
            return delta;

        var before_skill_levels = _capture_skill_levels(progression);
        var before_granted_skill_ids = _capture_granted_skill_ids(progression);
        var before_profession_ranks = _capture_profession_ranks(progression);
        delta.character_level_before = progression.character_level;

        var progression_service = BuildProgressionService(progression);
        var mastery_source_type = _resolve_mastery_source_type(source_type);
        if (!progression_service.GrantSkillMastery(skill_id, amount, mastery_source_type))
        {
            delta.character_level_after = delta.character_level_before;
            return delta;
        }

        delta.AddMasteryChange(
            new CharacterMasteryChangeFact(
                skill_id,
                _resolve_skill_label(skill_id),
                amount,
                source_type,
                !string.IsNullOrEmpty(source_label)
                    ? source_label
                    : _build_default_source_label(source_type),
                reason_text
            )
        );
        _fill_delta_from_progression(
            delta,
            progression,
            before_skill_levels,
            before_granted_skill_ids,
            before_profession_ranks
        );
        if (emit_achievement_event)
            delta.AppendUnlockedAchievementIds(
                RecordAchievementEvent(member_id, "skill_mastery_gained", amount, skill_id)
            );
        return delta;
    }

    private static Dictionary<StringName, int> _capture_skill_levels(UnitProgress progression)
    {
        var skill_levels = new Dictionary<StringName, int>();
        if (progression == null)
            return skill_levels;
        foreach (var skill_id in progression.GetSortedSkillIdsTyped())
        {
            var skill_progress = progression.GetSkillProgress(skill_id);
            if (skill_progress != null)
                skill_levels[skill_id] = skill_progress.skill_level;
        }
        return skill_levels;
    }

    private static HashSet<StringName> _capture_granted_skill_ids(UnitProgress progression)
    {
        var granted_skill_ids = new HashSet<StringName>();
        if (progression == null)
            return granted_skill_ids;
        foreach (var skill_id in progression.GetSortedSkillIdsTyped())
        {
            var skill_progress = progression.GetSkillProgress(skill_id);
            if (skill_progress != null && skill_progress.profession_granted_by != "")
                granted_skill_ids.Add(skill_id);
        }
        return granted_skill_ids;
    }

    private static Dictionary<StringName, int> _capture_profession_ranks(UnitProgress progression)
    {
        var ranks = new Dictionary<StringName, int>();
        if (progression == null)
            return ranks;
        foreach (var profession_id in progression.GetSortedProfessionIdsTyped())
        {
            var progress = progression.GetProfessionProgress(profession_id);
            if (progress != null)
                ranks[profession_id] = progress.rank;
        }
        return ranks;
    }

    private List<PendingCharacterRewardEntry> _normalize_pending_skill_mastery_entries(
        UnitProgress progression,
        IEnumerable<PendingCharacterRewardEntry> entry_options,
        StringName source_type
    )
    {
        var normalized_entries = new List<PendingCharacterRewardEntry>();
        if (progression == null)
            return normalized_entries;
        var entry_map = new Dictionary<StringName, PendingCharacterRewardEntry>();
        if (entry_options == null)
            return normalized_entries;
        foreach (PendingCharacterRewardEntry entry_option in entry_options)
        {
            PendingCharacterRewardEntryData entry_data =
                PendingCharacterRewardEntryData.FromEntry(
                    entry_option,
                    PendingCharacterRewardContentRules.ToStringName(
                        PendingCharacterRewardEntryKind.SkillMastery
                    ),
                    source_type
                );
            if (
                !entry_data.Exists
                || PendingCharacterRewardContentRules.ToEntryKind(entry_data.EntryType)
                    != PendingCharacterRewardEntryKind.SkillMastery
            )
                continue;
            var skill_id = entry_data.TargetId;
            var mastery_amount = entry_data.Amount;
            if (skill_id == "" || mastery_amount <= 0)
                continue;
            var mastery_source_type = _resolve_mastery_source_type(entry_data.MasterySourceType);
            var skill_progress = progression.GetSkillProgress(skill_id);
            var skill_definition = GetSkillDefinition(skill_id);
            if (skill_progress == null || skill_definition == null || !skill_progress.is_learned)
                continue;
            if (
                skill_definition.MasterySources.Count > 0
                && !HasStringName(skill_definition.MasterySources, mastery_source_type)
            )
                continue;
            if (!entry_map.TryGetValue(skill_id, out var reward_entry))
            {
                reward_entry = new PendingCharacterRewardEntry
                {
                    EntryKind = PendingCharacterRewardEntryKind.SkillMastery,
                    target_id = skill_id,
                    target_label = _resolve_skill_label(skill_id),
                    reason_text = entry_data.ReasonText,
                };
                entry_map[skill_id] = reward_entry;
                normalized_entries.Add(reward_entry);
            }
            reward_entry.amount += mastery_amount;
            if (string.IsNullOrEmpty(reward_entry.reason_text))
                reward_entry.reason_text = entry_data.ReasonText;
        }
        return normalized_entries;
    }

    private PendingCharacterReward _normalize_pending_character_reward_option(
        PendingCharacterReward raw_reward_option,
        bool allow_unsupported_entries = false
    )
    {
        if (raw_reward_option == null)
            return null;
        if (
            !allow_unsupported_entries
            && _has_unsupported_pending_character_entry_object(raw_reward_option.entries)
        )
            return null;
        if (raw_reward_option.reward_id == "")
            raw_reward_option.reward_id = _build_reward_id(
                raw_reward_option.member_id,
                raw_reward_option.source_id != ""
                    ? raw_reward_option.source_id
                    : raw_reward_option.source_type
            );
        return raw_reward_option.IsEmpty() ? null : raw_reward_option;
    }

    private QuestRewardData _resolve_quest_reward_data(StringName quest_id)
    {
        QuestDefinition questDefinition = GetQuestDef(quest_id);
        if (questDefinition == null)
            return QuestRewardData.Missing();
        return QuestRewardData.FromQuestDefinition(questDefinition);
    }

    private QuestSubmitItemPreviewData PreviewQuestSubmitItemObjective(
        StringName quest_id,
        StringName objective_id = default
    )
    {
        if (_party_state == null || quest_id == "")
        {
            return QuestSubmitItemPreviewData.Failed("invalid_quest_id");
        }
        var quest_state = _party_state.GetActiveQuestState(quest_id);
        if (quest_state == null)
        {
            return QuestSubmitItemPreviewData.Failed("quest_not_active");
        }
        QuestDefinition questDefinition = GetQuestDef(quest_id);
        if (questDefinition == null)
        {
            return QuestSubmitItemPreviewData.Failed("quest_def_missing");
        }

        var requested_objective_id = ProgressionDataUtils.to_string_name(objective_id);
        var found_completed_submit_item_objective = false;
        var completed_preview = QuestSubmitItemPreviewData.Failed("objective_already_complete");
        foreach (QuestObjectiveDefinition objectiveDefinition in questDefinition.Objectives)
        {
            if (
                objectiveDefinition == null
                || objectiveDefinition.ObjectiveKind != QuestObjectiveKind.SubmitItem
            )
                continue;
            var current_objective_id = objectiveDefinition.ObjectiveId;
            if (requested_objective_id != "" && current_objective_id != requested_objective_id)
                continue;
            var item_id = objectiveDefinition.TargetId;
            var target_value = objectiveDefinition.TargetValue;
            var required_quantity = Mathf.Max(
                target_value - quest_state.GetObjectiveProgress(current_objective_id),
                0
            );
            if (current_objective_id == "" || item_id == "" || target_value <= 0)
            {
                return QuestSubmitItemPreviewData.Failed(
                    "invalid_submit_item_objective",
                    current_objective_id,
                    item_id,
                    target_value,
                    required_quantity
                );
            }
            if (quest_state.IsObjectiveComplete(current_objective_id, target_value))
            {
                found_completed_submit_item_objective = true;
                completed_preview = QuestSubmitItemPreviewData.Failed(
                    "objective_already_complete",
                    current_objective_id,
                    item_id,
                    target_value,
                    required_quantity
                );
                if (requested_objective_id != "")
                    return completed_preview;
                continue;
            }
            return QuestSubmitItemPreviewData.Success(
                current_objective_id,
                item_id,
                target_value,
                required_quantity
            );
        }
        return found_completed_submit_item_objective
            ? completed_preview
            : QuestSubmitItemPreviewData.Failed("objective_not_found");
    }

    private QuestRewardPreviewData _preview_quest_reward_claim(
        StringName quest_id,
        QuestRewardData quest_reward_data
    )
    {
        var quest_label = quest_reward_data.DisplayName.StripEdges();
        if (quest_label.Length == 0)
            return QuestRewardPreviewData.Failed("invalid_quest_display_name");
        var unsupported_reward_types = new GStringNameArray();
        var reward_item_entries = new GArray();
        var reward_item_ids = new List<StringName>();
        var pending_character_rewards = new List<PendingCharacterReward>();
        var gold_delta = 0;
        foreach (var reward_data in quest_reward_data.RewardEntries)
        {
            if (!reward_data.Exists)
                return QuestRewardPreviewData.Failed("invalid_reward_entry");
            var reward_type = reward_data.RewardType;
            if (reward_type == "")
                return QuestRewardPreviewData.Failed("invalid_reward_entry");
            if (reward_data.RewardKind == QuestRewardKind.Gold)
            {
                var amount = reward_data.Amount;
                if (amount <= 0)
                    return QuestRewardPreviewData.Failed("invalid_gold_amount");
                gold_delta += amount;
            }
            else if (reward_data.RewardKind == QuestRewardKind.Item)
            {
                var item_reward_result = _preview_quest_item_reward_entry(reward_data);
                if (!item_reward_result.Ok)
                    return QuestRewardPreviewData.Failed(item_reward_result.ErrorCode);
                var reward_item_entry = item_reward_result.CloneItemReward();
                if (reward_item_entry.Count > 0)
                    reward_item_entries.Add(reward_item_entry);
                foreach (var item_id in item_reward_result.CloneWarehouseDepositItemIds())
                    reward_item_ids.Add(item_id);
            }
            else if (reward_data.RewardKind == QuestRewardKind.PendingCharacterReward)
            {
                var pending_reward_result = _preview_quest_pending_character_reward_entry(
                    quest_id,
                    quest_label,
                    reward_data
                );
                if (!pending_reward_result.Ok)
                    return QuestRewardPreviewData.Failed(pending_reward_result.ErrorCode);
                var reward = pending_reward_result.PendingReward;
                if (reward != null && !reward.IsEmpty())
                    pending_character_rewards.Add(reward);
            }
            else
            {
                _append_unique_string_name(unsupported_reward_types, reward_type);
            }
        }
        if (unsupported_reward_types.Count > 0)
        {
            return QuestRewardPreviewData.Failed(
                "unsupported_reward_types",
                unsupportedRewardTypes: unsupported_reward_types
            );
        }
        if (reward_item_ids.Count > 0)
        {
            var warehouse_preview = _party_warehouse_service.PreviewBatchSwapTyped(
                new List<StringName>(),
                reward_item_ids
            );
            if (!warehouse_preview.Allowed)
                return QuestRewardPreviewData.Failed(
                    _resolve_quest_reward_warehouse_error_code(warehouse_preview.ErrorCode)
                );
        }
        return QuestRewardPreviewData.Success(
            gold_delta,
            reward_item_entries,
            reward_item_ids,
            pending_character_rewards
        );
    }

    private QuestItemRewardPreviewData _preview_quest_item_reward_entry(
        QuestRewardEntryData reward_data
    )
    {
        var reward_item_id = reward_data.ItemId;
        var reward_quantity = reward_data.Quantity;
        if (reward_item_id == "" || reward_quantity <= 0)
            return QuestItemRewardPreviewData.Failed("invalid_item_reward");
        var itemDef = GetItemDef(reward_item_id);
        if (itemDef == null)
            return QuestItemRewardPreviewData.Failed("item_reward_missing_def");
        var item_display_name = itemDef.DisplayName.StripEdges();
        if (item_display_name.Length == 0)
            return QuestItemRewardPreviewData.Failed("invalid_item_display_name");
        return QuestItemRewardPreviewData.Success(
            reward_item_id,
            item_display_name,
            reward_quantity,
            _build_repeated_item_ids(reward_item_id, reward_quantity)
        );
    }

    private QuestPendingCharacterRewardPreviewData _preview_quest_pending_character_reward_entry(
        StringName quest_id,
        string quest_label,
        QuestRewardEntryData reward_data
    )
    {
        var member_id = reward_data.MemberId;
        var entry_options = reward_data.CloneEntries();
        if (
            member_id == ""
            || entry_options.Count == 0
        )
            return QuestPendingCharacterRewardPreviewData.Failed(
                "invalid_pending_character_reward"
            );
        var source_type = reward_data.SourceType != "" ? reward_data.SourceType : RewardTypeQuest;
        var source_id = reward_data.SourceId != "" ? reward_data.SourceId : quest_id;
        if (source_id == "")
            source_id = quest_id != "" ? quest_id : source_type;
        var source_label = reward_data.SourceLabel.StripEdges();
        if (source_label.Length == 0)
            source_label = quest_label;
        var summary_text = reward_data.SummaryText.StripEdges();
        var pending_reward = BuildPendingCharacterReward(
            member_id,
            reward_data.RewardId,
            source_type,
            source_id,
            source_label,
            entry_options,
            summary_text
        );
        return pending_reward == null || pending_reward.IsEmpty()
            ? QuestPendingCharacterRewardPreviewData.Failed("invalid_pending_character_reward")
            : QuestPendingCharacterRewardPreviewData.Success(pending_reward);
    }

    private QuestDefinition GetQuestDef(StringName questId)
    {
        return questId != ""
            && _quest_def_index.TryGetValue(
                questId,
                out QuestDefinition questDefinition
            )
            ? questDefinition
            : null;
    }

    private static Dictionary<StringName, T> CloneContentDefIndex<T>(
        IReadOnlyDictionary<StringName, T> source
    )
        where T : class
    {
        Dictionary<StringName, T> result = new();
        if (source == null)
            return result;
        foreach ((StringName key, T value) in source)
        {
            if (key != "" && value != null)
                result[key] = value;
        }
        return result;
    }

    private static List<StringName> SortedContentKeys<T>(IReadOnlyDictionary<StringName, T> source)
    {
        List<StringName> result = new();
        if (source == null)
            return result;
        foreach (StringName key in source.Keys)
            result.Add(key);
        result.Sort((left, right) => string.CompareOrdinal((string)left, (string)right));
        return result;
    }

    private static List<StringName> _build_repeated_item_ids(StringName item_id, int quantity)
    {
        var item_ids = new List<StringName>();
        for (var i = 0; i < Mathf.Max(quantity, 0); i++)
            item_ids.Add(item_id);
        return item_ids;
    }

    private static List<StringName> CloneStringNameList(IEnumerable<StringName> source)
    {
        var result = new List<StringName>();
        if (source == null)
            return result;
        foreach (var value in source)
            result.Add(value);
        return result;
    }

    private static List<PendingCharacterRewardEntry> DuplicatePendingCharacterRewardEntries(
        IEnumerable<PendingCharacterRewardEntry> entries
    )
    {
        var result = new List<PendingCharacterRewardEntry>();
        if (entries == null)
            return result;
        foreach (PendingCharacterRewardEntry entry in entries)
        {
            if (entry == null || entry.IsEmpty())
                continue;
            result.Add(entry.DuplicateState());
        }
        return result;
    }

    private static List<PendingCharacterRewardEntry> ClonePendingCharacterRewardEntryList(
        IEnumerable<PendingCharacterRewardEntry> entries
    )
    {
        var result = new List<PendingCharacterRewardEntry>();
        if (entries == null)
            return result;
        foreach (PendingCharacterRewardEntry entry in entries)
        {
            if (entry == null || entry.IsEmpty())
                continue;
            result.Add(entry.DuplicateState());
        }
        return result;
    }

    private static List<PendingCharacterReward> ClonePendingCharacterRewardList(
        IEnumerable<PendingCharacterReward> rewards
    )
    {
        var result = new List<PendingCharacterReward>();
        if (rewards == null)
            return result;
        foreach (PendingCharacterReward reward in rewards)
        {
            if (reward == null || reward.IsEmpty())
                continue;
            result.Add(reward.DuplicateState());
        }
        return result;
    }

    private static GStringNameArray CloneStringNameArray(GStringNameArray source)
    {
        var result = new GStringNameArray();
        if (source == null)
            return result;
        foreach (var value in source)
            result.Add(value);
        return result;
    }

    private static GStringNameArray ToStringNameArray(IEnumerable<StringName> source)
    {
        var result = new GStringNameArray();
        if (source == null)
            return result;
        foreach (var value in source)
            result.Add(value);
        return result;
    }
}
