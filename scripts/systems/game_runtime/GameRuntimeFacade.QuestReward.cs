using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of GameRuntimeFacade — battle quest-progress events, pending-reward queueing, summary formatting.
// Pure physical split: same class, no behavior change. See GameRuntimeFacade.cs.
public sealed partial class GameRuntimeFacade
{

    private List<QuestProgressService.QuestProgressEventData> BuildDefaultBattleQuestProgressEventsTyped(
        string winner_faction_id
    )
    {
        List<QuestProgressService.QuestProgressEventData> result = new();
        if (winner_faction_id != "player")
            return result;
        var encounterAnchor = _get_encounter_anchor_by_id(_active_battle_encounter_id);
        if (encounterAnchor == null)
            return result;
        QuestProgressService.QuestProgressEventData eventData =
            QuestProgressService.QuestProgressEventData.CreateProgressByObjectiveTarget(
                "defeat_enemy",
                encounterAnchor.enemy_roster_template_id,
                1,
                GetWorldStep(),
                encounterAnchor.enemy_roster_template_id,
                encounterAnchor.entity_id,
                encounterAnchor.encounter_kind
            );
        if (eventData != null && eventData.IsValid)
            result.Add(eventData);
        return result;
    }

    private static List<PendingCharacterReward> DuplicatePendingCharacterRewards(
        IEnumerable<PendingCharacterReward> rewards
    )
    {
        List<PendingCharacterReward> result = new();
        if (rewards == null)
            return result;
        foreach (PendingCharacterReward reward in rewards)
        {
            if (reward != null && !reward.IsEmpty())
                result.Add(reward.DuplicateState());
        }
        return result;
    }

    private List<PendingCharacterReward> FilterBattlePendingCharacterRewardsForQueue(
        IEnumerable<PendingCharacterReward> rewards,
        GDictionary battleSummary,
        string winnerFactionId
    )
    {
        List<PendingCharacterReward> result = new();
        if (rewards == null)
            return result;
        PartyState partyState = _character_management?.GetPartyState() ?? _party_state;
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            GetSkillDefinitionsTyped();
        foreach (PendingCharacterReward reward in rewards)
        {
            if (
                IsBattlePendingCharacterRewardQueueable(
                    reward,
                    partyState,
                    skillDefinitions,
                    out string errorCode,
                    out PendingCharacterRewardEntry invalidEntry
                )
            )
            {
                result.Add(reward);
                continue;
            }
            LogDroppedBattlePendingCharacterReward(
                reward,
                invalidEntry,
                errorCode,
                battleSummary,
                winnerFactionId
            );
        }
        return result;
    }

    private static bool IsBattlePendingCharacterRewardQueueable(
        PendingCharacterReward reward,
        PartyState partyState,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        out string errorCode,
        out PendingCharacterRewardEntry invalidEntry
    )
    {
        errorCode = "";
        invalidEntry = null;
        if (reward == null || reward.IsEmpty())
        {
            errorCode = "empty_reward";
            return false;
        }
        if (partyState == null || partyState.GetMemberState(reward.member_id) == null)
        {
            errorCode = "missing_member";
            return false;
        }
        bool hasValidEntry = false;
        foreach (PendingCharacterRewardEntry entry in reward.entries)
        {
            if (entry == null)
            {
                errorCode = "null_entry";
                return false;
            }
            invalidEntry = entry;
            PendingCharacterRewardEntryKind entryKind = entry.EntryKind;
            if (entryKind == PendingCharacterRewardEntryKind.Unknown)
            {
                errorCode = "unsupported_entry_type";
                return false;
            }
            if (entry.target_id == "")
            {
                errorCode = "missing_target";
                return false;
            }
            if (entry.amount == 0)
            {
                errorCode = "zero_amount";
                return false;
            }
            if (
                PendingCharacterRewardContentRules.RequiresSkillTarget(entry.entry_type)
                && skillDefinitions != null
                && skillDefinitions.Count > 0
                && !skillDefinitions.ContainsKey(entry.target_id)
            )
            {
                errorCode = "missing_skill_def";
                return false;
            }
            if (
                PendingCharacterRewardContentRules.IsAttributeProgressEntry(entry.entry_type)
                && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(entry.target_id)
            )
            {
                errorCode = "invalid_attribute_target";
                return false;
            }
            if (
                PendingCharacterRewardContentRules.IsAttributeDeltaEntry(entry.entry_type)
                && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(entry.target_id)
                && entry.target_id != "hp_max"
            )
            {
                errorCode = "invalid_attribute_target";
                return false;
            }
            hasValidEntry = true;
        }
        invalidEntry = null;
        if (!hasValidEntry)
        {
            errorCode = "empty_entries";
            return false;
        }
        return true;
    }

    private void LogDroppedBattlePendingCharacterReward(
        PendingCharacterReward reward,
        PendingCharacterRewardEntry invalidEntry,
        string errorCode,
        GDictionary battleSummary,
        string winnerFactionId
    )
    {
        using GDictionary emptyBattle = battleSummary == null ? new GDictionary() : null;
        using var context = new GDictionary
        {
            ["battle"] = battleSummary ?? emptyBattle,
            ["winner_faction_id"] = winnerFactionId ?? "",
            ["error_code"] = errorCode ?? "",
            ["reward_id"] = reward?.reward_id.ToString() ?? "",
            ["member_id"] = reward?.member_id.ToString() ?? "",
            ["source_type"] = reward?.source_type.ToString() ?? "",
            ["source_id"] = reward?.source_id.ToString() ?? "",
            ["entry_type"] = invalidEntry?.entry_type.ToString() ?? "",
            ["target_id"] = invalidEntry?.target_id.ToString() ?? "",
            ["amount"] = invalidEntry?.amount ?? 0,
        };
        string rewardId = reward?.reward_id.ToString() ?? "";
        string message = string.IsNullOrEmpty(rewardId)
            ? "战斗角色奖励不合法，已丢弃。"
            : $"战斗角色奖励 {rewardId} 不合法，已丢弃。";
        string contextText = Json.Stringify(context);
        GameLog.Warning(message, "battle.pending_reward_dropped", "battle", contextText);
        _log_runtime_event(
            "warn",
            "battle",
            "battle.pending_reward_dropped",
            message,
            contextText
        );
    }

    private bool _has_quest_progress_summary_changes(QuestProgressApplyResultData summary)
    {
        return summary != null
            && (
                summary.CloneAcceptedQuestIds().Count > 0
                || summary.CloneProgressedQuestIds().Count > 0
                || summary.CloneClaimableQuestIds().Count > 0
                || summary.CloneCompletedQuestIds().Count > 0
            );
    }

    private string _format_quest_progress_summary(QuestProgressApplyResultData summary)
    {
        var parts = new System.Collections.Generic.List<string>();
        var acceptedIds = summary?.CloneAcceptedQuestIds() ?? new GStringNameArray();
        var progressedIds = summary?.CloneProgressedQuestIds() ?? new GStringNameArray();
        var claimableIds = summary?.CloneClaimableQuestIds() ?? new GStringNameArray();
        var completedIds = summary?.CloneCompletedQuestIds() ?? new GStringNameArray();
        if (acceptedIds.Count > 0)
            parts.Add($"接取 {_format_string_name_list(acceptedIds)}");
        if (progressedIds.Count > 0)
            parts.Add($"推进 {_format_string_name_list(progressedIds)}");
        if (claimableIds.Count > 0)
            parts.Add($"待领奖励 {_format_string_name_list(claimableIds)}");
        if (completedIds.Count > 0)
            parts.Add($"完成 {_format_string_name_list(completedIds)}");
        return parts.Count > 0
            ? $"任务进度已更新：{string.Join("；", parts)}。"
            : "任务进度未变化。";
    }

    internal QuestDefinition GetQuestDef(StringName quest_id)
    {
        return GetContentCatalogTyped() != null && quest_id != ""
            ? GetContentCatalogTyped().GetQuestDefTyped(quest_id)
            : null;
    }

    private GDictionary _quest_progress_summary_to_string_dict(QuestProgressApplyResultData summary)
    {
        return new GDictionary
        {
            ["accepted_quest_ids"] = _string_name_array_to_string_array(
                summary?.CloneAcceptedQuestIds()
            ),
            ["progressed_quest_ids"] = _string_name_array_to_string_array(
                summary?.CloneProgressedQuestIds()
            ),
            ["claimable_quest_ids"] = _string_name_array_to_string_array(
                summary?.CloneClaimableQuestIds()
            ),
            ["completed_quest_ids"] = _string_name_array_to_string_array(
                summary?.CloneCompletedQuestIds()
            ),
        };
    }

    private string _format_string_name_list(IEnumerable<StringName> values)
    {
        var labels = _string_name_array_to_string_array(values);
        var strings = new string[labels.Count];
        for (int i = 0; i < labels.Count; i++)
            strings[i] = labels[i];
        return string.Join("、", strings);
    }

    private string _format_string_name_list(GArray values)
    {
        var labels = _string_name_array_to_string_array(values);
        var strings = new string[labels.Count];
        for (int i = 0; i < labels.Count; i++)
            strings[i] = labels[i];
        return string.Join("、", strings);
    }

    private Godot.Collections.Array<string> _string_name_array_to_string_array(
        IEnumerable<StringName> values
    )
    {
        var labels = new Godot.Collections.Array<string>();
        if (values == null)
        {
            return labels;
        }
        foreach (StringName value in values)
        {
            if (value != "")
            {
                labels.Add(value.ToString());
            }
        }
        return labels;
    }

    private Godot.Collections.Array<string> _string_name_array_to_string_array(GArray values)
    {
        var labels = new Godot.Collections.Array<string>();
        foreach (StringName value in ProgressionDataUtils.to_string_name_array(values))
            labels.Add(value.ToString());
        return labels;
    }

    private string _build_quest_claim_reward_summary_text(GDictionary claim_result)
    {
        var rewardParts = new List<string>();
        int goldDelta = DictInt(claim_result, "gold_delta", 0);
        if (goldDelta > 0)
            rewardParts.Add($"{goldDelta} 金");
        foreach (GDictionary rewardData in ReadDictionaryItems(DictArray(claim_result, "item_rewards")))
        {
            int quantity = DictInt(rewardData, "quantity", 0);
            string label = DictString(rewardData, "display_name", "").Trim();
            if (quantity <= 0 || label.Length == 0)
                continue;
            rewardParts.Add($"{label} x{quantity}");
        }
        foreach (
            GDictionary rewardData in ReadDictionaryItems(
                DictArray(claim_result, "pending_character_rewards")
            )
        )
        {
            string memberName = DictString(rewardData, "member_name", "").Trim();
            rewardParts.Add(memberName.Length > 0 ? $"{memberName}的角色奖励" : "角色奖励");
        }
        return string.Join("、", rewardParts);
    }
}
