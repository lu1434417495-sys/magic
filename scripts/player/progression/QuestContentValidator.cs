using System.Collections.Generic;
using Godot;

public static class QuestContentValidator
{
    public static List<string> ValidateTyped(
        IReadOnlyDictionary<StringName, QuestDef> questDefs,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates,
        IEnumerable<string> registrationErrors = null
    )
    {
        var errors = new List<string>();
        AppendErrors(errors, registrationErrors);

        const string label = "quest_defs";
        if (itemDefs == null || itemDefs.Count == 0)
            errors.Add(
                $"{label} validation requires non-empty item_defs (pass allow_missing_reference_tables=true to skip)."
            );
        if (skillDefinitions == null || skillDefinitions.Count == 0)
            errors.Add(
                $"{label} validation requires non-empty skill_defs (pass allow_missing_reference_tables=true to skip)."
            );
        if (enemyTemplates == null || enemyTemplates.Count == 0)
            errors.Add(
                $"{label} validation requires non-empty enemy_templates (pass allow_missing_reference_tables=true to skip)."
            );

        var seenQuestIds = new HashSet<StringName>();
        var supportedProviderIds = ResolveProviderIdsTyped();
        foreach (StringName questId in SortKeys(questDefs))
        {
            QuestDef questDef = GetContentObject(questDefs, questId);
            if (questDef == null)
            {
                errors.Add($"Quest entry {label}::{questId} failed to cast to QuestDef.");
                continue;
            }

            if (questDef.quest_id == "")
            {
                errors.Add($"Quest entry {label}::{questId} is missing quest_id.");
                continue;
            }

            if (!seenQuestIds.Add(questDef.quest_id))
            {
                errors.Add($"Duplicate quest_id registered: {questDef.quest_id}");
                continue;
            }

            foreach (string schemaError in questDef.ValidateSchema())
                errors.Add($"Quest {questDef.quest_id}: {schemaError}");

            AppendProviderReferenceErrors(errors, questDef, supportedProviderIds);
            AppendProviderKindErrors(errors, questDef);
            AppendListingChannelErrors(errors, questDef);
            AppendObjectiveReferenceErrors(errors, questDef, itemDefs, enemyTemplates);
            AppendRewardReferenceErrors(errors, questDef, itemDefs, skillDefinitions);
        }

        return errors;
    }

    private static void AppendProviderReferenceErrors(
        ICollection<string> errors,
        QuestDef questDef,
        ISet<StringName> supportedProviderIds
    )
    {
        if (questDef.provider_interaction_id == "")
        {
            errors.Add($"Quest {questDef.quest_id} is missing provider_interaction_id.");
            return;
        }

        if (!supportedProviderIds.Contains(questDef.provider_interaction_id))
            errors.Add(
                $"Quest {questDef.quest_id} references missing provider_interaction_id {questDef.provider_interaction_id}."
            );
    }

    public static void AppendProviderKindErrors(
        List<string> errors,
        QuestDef questDef
    )
    {
        QuestProviderKind kind = QuestProviderContentRules.ToProviderKind(questDef);
        if (kind == QuestProviderKind.Unknown)
        {
            errors.Add($"Quest {questDef.quest_id}: 未知 provider_kind '{questDef.provider_kind}'。");
            return;
        }

        StringName expectedInteractionId = kind switch
        {
            QuestProviderKind.ServiceContractBoard => "service_contract_board",
            QuestProviderKind.ServiceBountyRegistry => "service_bounty_registry",
            QuestProviderKind.Npc => questDef.provider_interaction_id,
            _ => "",
        };

        if (kind == QuestProviderKind.ServiceContractBoard || kind == QuestProviderKind.ServiceBountyRegistry)
        {
            if (questDef.provider_interaction_id != expectedInteractionId)
                errors.Add($"Quest {questDef.quest_id}: provider_kind '{questDef.provider_kind}' 要求 provider_interaction_id 为 '{expectedInteractionId}'。");
        }
        else if (kind == QuestProviderKind.Npc)
        {
            if (questDef.provider_interaction_id == "")
                errors.Add($"Quest {questDef.quest_id}: provider_kind 'npc' 需要非空的 provider_interaction_id。");
        }
    }

    public static void AppendListingChannelErrors(
        List<string> errors,
        QuestDef questDef
    )
    {
        if (questDef.listing_channels == null || questDef.listing_channels.Count == 0)
        {
            errors.Add($"Quest {questDef.quest_id}: listing_channels 不能为空。");
            return;
        }

        foreach (QuestListingChannel channel in QuestProviderContentRules.ToListingChannels(questDef))
        {
            if (channel == QuestListingChannel.Unknown)
                errors.Add($"Quest {questDef.quest_id}: listing_channels 包含未知渠道。");
        }
    }

    private static void AppendObjectiveReferenceErrors(
        ICollection<string> errors,
        QuestDef questDef,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates
    )
    {
        foreach (QuestDef.ObjectiveEntryData objective in questDef.GetObjectiveEntriesTyped())
        {
            var objectiveId = objective.ObjectiveId;
            var objectiveType = objective.ObjectiveType;
            var targetId = objective.TargetId;

            if (QuestDef.ToObjectiveKind(objectiveType) == QuestObjectiveKind.SubmitItem)
            {
                if (targetId != "" && itemDefs.Count > 0 && !itemDefs.ContainsKey(targetId))
                    errors.Add(
                        $"Quest {questDef.quest_id} submit_item objective {objectiveId} references missing item {targetId}."
                    );
            }
            else if (QuestDef.ToObjectiveKind(objectiveType) == QuestObjectiveKind.DefeatEnemy)
            {
                if (
                    targetId != ""
                    && enemyTemplates.Count > 0
                    && !enemyTemplates.ContainsKey(targetId)
                )
                    errors.Add(
                        $"Quest {questDef.quest_id} defeat_enemy objective {objectiveId} references missing enemy {targetId}."
                    );
            }
        }
    }

    private static void AppendRewardReferenceErrors(
        ICollection<string> errors,
        QuestDef questDef,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        foreach (QuestDef.RewardEntryData reward in questDef.GetRewardEntriesTyped())
        {
            var rewardType = reward.RewardType;
            if (QuestDef.ToRewardKind(rewardType) == QuestRewardKind.Item)
            {
                var rewardItemId = reward.ItemId;
                if (rewardItemId != "" && itemDefs.Count > 0 && !itemDefs.ContainsKey(rewardItemId))
                    errors.Add(
                        $"Quest {questDef.quest_id} reward references missing item {rewardItemId}."
                    );
            }
            else if (QuestDef.ToRewardKind(rewardType) == QuestRewardKind.PendingCharacterReward)
            {
                AppendPendingCharacterRewardReferenceErrors(
                    errors,
                    questDef,
                    reward,
                    skillDefinitions
                );
            }
        }
    }

    private static void AppendPendingCharacterRewardReferenceErrors(
        ICollection<string> errors,
        QuestDef questDef,
        QuestDef.RewardEntryData reward,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        foreach (QuestDef.PendingRewardEntryData entry in reward.PendingRewardEntries)
        {
            if (!entry.IsDictionaryEntry)
                continue;
            var entryType = entry.EntryType;
            var targetId = entry.TargetId;

            if (
                entryType == ""
                || !PendingCharacterRewardContentRules.IsSupportedEntryType(entryType)
            )
                continue;

            if (PendingCharacterRewardContentRules.RequiresSkillTarget(entryType))
            {
                if (
                    targetId != ""
                    && skillDefinitions != null
                    && skillDefinitions.Count > 0
                    && !skillDefinitions.ContainsKey(targetId)
                )
                    errors.Add(
                        $"Quest {questDef.quest_id} pending_character_reward references missing skill {targetId}."
                    );
            }

            if (
                PendingCharacterRewardContentRules.IsAttributeProgressEntry(entryType)
                && targetId != ""
                && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(targetId)
            )
            {
                errors.Add(
                    $"Quest {questDef.quest_id} pending_character_reward attribute_progress references unsupported attribute {targetId}."
                );
            }

            if (
                PendingCharacterRewardContentRules.IsAttributeDeltaEntry(entryType)
                && targetId != ""
                && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(targetId)
                && targetId != "hp_max"
            )
            {
                errors.Add(
                    $"Quest {questDef.quest_id} pending_character_reward attribute_delta references unsupported attribute {targetId}."
                );
            }
        }
    }

    private static T GetContentObject<T>(
        IReadOnlyDictionary<StringName, T> source,
        StringName contentId
    )
        where T : class
    {
        if (source == null)
            return null;
        source.TryGetValue(contentId, out T value);
        return value;
    }

    private static HashSet<StringName> ResolveProviderIdsTyped()
    {
        var result = new HashSet<StringName>();
        foreach (StringName providerId in QuestProviderContentRules.SupportedProviderIds())
        {
            if (providerId != "")
                result.Add(providerId);
        }
        return result;
    }

    private static void AppendErrors(ICollection<string> target, IEnumerable<string> errors)
    {
        if (target == null || errors == null)
            return;
        foreach (string error in errors)
        {
            if (!string.IsNullOrEmpty(error))
                target.Add(error);
        }
    }

    private static List<StringName> SortKeys<T>(IReadOnlyDictionary<StringName, T> source)
    {
        var keys = new List<StringName>();
        if (source == null)
            return keys;
        foreach (StringName key in source.Keys)
        {
            if (key != "")
                keys.Add(key);
        }
        keys.Sort(static (left, right) =>
            string.CompareOrdinal(left.ToString(), right.ToString())
        );
        return keys;
    }
}
