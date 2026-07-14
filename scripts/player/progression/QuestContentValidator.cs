using System.Collections.Generic;
using Godot;

public static class QuestContentValidator
{
    internal static List<string> ValidateTyped(
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates,
        IEnumerable<string> registrationErrors = null
    )
    {
        var errors = new List<string>();
        AppendErrors(errors, registrationErrors);

        const string label = "quest_defs";
        if (itemDefs == null || itemDefs.Count == 0)
            errors.Add($"{label} validation requires non-empty item_defs.");
        if (skillDefinitions == null || skillDefinitions.Count == 0)
            errors.Add($"{label} validation requires non-empty skill_defs.");
        if (enemyTemplates == null || enemyTemplates.Count == 0)
            errors.Add($"{label} validation requires non-empty enemy_templates.");

        var seenQuestIds = new HashSet<StringName>();
        var supportedProviderIds = ResolveProviderIdsTyped();
        foreach (StringName questId in SortKeys(questDefs))
        {
            QuestDefinition questDef = GetContentObject(questDefs, questId);
            if (questDef == null)
            {
                errors.Add($"Quest entry {label}::{questId} is missing a QuestDefinition.");
                continue;
            }

            if (questDef.QuestId == "")
            {
                errors.Add($"Quest entry {label}::{questId} is missing quest_id.");
                continue;
            }

            if (!seenQuestIds.Add(questDef.QuestId))
            {
                errors.Add($"Duplicate quest_id registered: {questDef.QuestId}");
                continue;
            }

            AppendSchemaErrors(errors, questDef);

            AppendProviderReferenceErrors(errors, questDef, supportedProviderIds);
            AppendProviderKindErrors(errors, questDef);
            AppendListingChannelErrors(errors, questDef);
            AppendAcceptRequirementErrors(errors, questDef, questDefs);
            AppendObjectiveReferenceErrors(errors, questDef, itemDefs, enemyTemplates);
            AppendRewardReferenceErrors(errors, questDef, itemDefs, skillDefinitions);
        }

        return errors;
    }

    private static void AppendProviderReferenceErrors(
        ICollection<string> errors,
        QuestDefinition questDef,
        ISet<StringName> supportedProviderIds
    )
    {
        if (questDef.ProviderInteractionId == "")
        {
            errors.Add($"Quest {questDef.QuestId} is missing provider_interaction_id.");
            return;
        }

        // NPC provider_interaction_id values are interaction_script_ids, not service-modal IDs.
        if (QuestProviderContentRules.ToProviderKind(questDef) == QuestProviderKind.Npc)
            return;

        if (!supportedProviderIds.Contains(questDef.ProviderInteractionId))
            errors.Add(
                $"Quest {questDef.QuestId} references missing provider_interaction_id {questDef.ProviderInteractionId}."
            );
    }

    public static void AppendProviderKindErrors(
        List<string> errors,
        QuestDefinition questDef
    )
    {
        QuestProviderKind kind = QuestProviderContentRules.ToProviderKind(questDef);
        if (kind == QuestProviderKind.Unknown)
        {
            errors.Add($"Quest {questDef.QuestId}: 未知 provider_kind '{questDef.ProviderKind}'。");
            return;
        }

        StringName expectedInteractionId = kind switch
        {
            QuestProviderKind.ServiceContractBoard => "service_contract_board",
            QuestProviderKind.ServiceBountyRegistry => "service_bounty_registry",
            QuestProviderKind.Npc => questDef.ProviderInteractionId,
            _ => "",
        };

        if (kind == QuestProviderKind.ServiceContractBoard || kind == QuestProviderKind.ServiceBountyRegistry)
        {
            if (questDef.ProviderInteractionId != expectedInteractionId)
                errors.Add($"Quest {questDef.QuestId}: provider_kind '{questDef.ProviderKind}' 要求 provider_interaction_id 为 '{expectedInteractionId}'。");
        }
        else if (kind == QuestProviderKind.Npc)
        {
            if (questDef.ProviderInteractionId == "")
                errors.Add($"Quest {questDef.QuestId}: provider_kind 'npc' 需要非空的 provider_interaction_id。");
        }
    }

    public static void AppendListingChannelErrors(
        List<string> errors,
        QuestDefinition questDef
    )
    {
        if (questDef.ListingChannels.Count == 0)
        {
            errors.Add($"Quest {questDef.QuestId}: listing_channels 不能为空。");
            return;
        }

        foreach (QuestListingChannel channel in QuestProviderContentRules.ToListingChannels(questDef))
        {
            if (channel == QuestListingChannel.Unknown)
                errors.Add($"Quest {questDef.QuestId}: listing_channels 包含未知渠道。");
        }
    }

    public static void AppendAcceptRequirementErrors(
        List<string> errors,
        QuestDefinition questDef,
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs
    )
    {
        if (questDef.AcceptRequirements.Count == 0)
            return;

        foreach (QuestAcceptRequirementDefinition requirement in questDef.AcceptRequirements)
        {
            StringName requirementType = requirement.RequirementType;
            if (
                requirementType != "quest_completed"
                && requirementType != "quest_active"
                && requirementType != "quest_not_completed"
            )
            {
                errors.Add(
                    $"Quest {questDef.QuestId}: accept_requirements 包含不支持的 requirement_type '{requirementType}'。"
                );
                continue;
            }

            StringName requiredQuestId = requirement.QuestId;
            if (requiredQuestId == "")
            {
                errors.Add(
                    $"Quest {questDef.QuestId}: accept_requirements 中 '{requirementType}' 缺少 quest_id。"
                );
                continue;
            }

            if (questDefs == null || !questDefs.ContainsKey(requiredQuestId))
            {
                errors.Add(
                    $"Quest {questDef.QuestId}: accept_requirements 引用了不存在的 quest_id '{requiredQuestId}'。"
                );
            }
        }
    }

    private static void AppendObjectiveReferenceErrors(
        ICollection<string> errors,
        QuestDefinition questDef,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates
    )
    {
        foreach (QuestObjectiveDefinition objective in questDef.Objectives)
        {
            var objectiveId = objective.ObjectiveId;
            var objectiveType = objective.ObjectiveType;
            var targetId = objective.TargetId;

            if (objective.ObjectiveKind == QuestObjectiveKind.SubmitItem)
            {
                if (targetId != "" && itemDefs.Count > 0 && !itemDefs.ContainsKey(targetId))
                    errors.Add(
                        $"Quest {questDef.QuestId} submit_item objective {objectiveId} references missing item {targetId}."
                    );
            }
            else if (objective.ObjectiveKind == QuestObjectiveKind.DefeatEnemy)
            {
                if (
                    targetId != ""
                    && enemyTemplates.Count > 0
                    && !enemyTemplates.ContainsKey(targetId)
                )
                    errors.Add(
                        $"Quest {questDef.QuestId} defeat_enemy objective {objectiveId} references missing enemy {targetId}."
                    );
            }
        }
    }

    private static void AppendRewardReferenceErrors(
        ICollection<string> errors,
        QuestDefinition questDef,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        foreach (QuestRewardDefinition reward in questDef.Rewards)
        {
            var rewardType = reward.RewardType;
            if (reward.RewardKind == QuestRewardKind.Item)
            {
                var rewardItemId = reward.ItemId;
                if (rewardItemId != "" && itemDefs.Count > 0 && !itemDefs.ContainsKey(rewardItemId))
                    errors.Add(
                        $"Quest {questDef.QuestId} reward references missing item {rewardItemId}."
                    );
            }
            else if (reward.RewardKind == QuestRewardKind.PendingCharacterReward)
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
        QuestDefinition questDef,
        QuestRewardDefinition reward,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        foreach (QuestPendingRewardEntryDefinition entry in reward.PendingRewardEntries)
        {
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
                        $"Quest {questDef.QuestId} pending_character_reward references missing skill {targetId}."
                    );
            }

            if (
                PendingCharacterRewardContentRules.IsAttributeProgressEntry(entryType)
                && targetId != ""
                && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(targetId)
            )
            {
                errors.Add(
                    $"Quest {questDef.QuestId} pending_character_reward attribute_progress references unsupported attribute {targetId}."
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
                    $"Quest {questDef.QuestId} pending_character_reward attribute_delta references unsupported attribute {targetId}."
                );
            }
        }
    }

    private static void AppendSchemaErrors(
        ICollection<string> errors,
        QuestDefinition questDef
    )
    {
        if (questDef == null)
            return;
        string prefix = $"Quest {questDef.QuestId}: ";
        if (questDef.QuestId == "")
            errors.Add(prefix + "QuestDef 缺少 quest_id。");
        if (string.IsNullOrWhiteSpace(questDef.DisplayName))
            errors.Add(prefix + $"QuestDef {questDef.QuestId} 缺少 display_name。");
        if (questDef.ProviderKind == "")
            errors.Add(prefix + $"QuestDef {questDef.QuestId} 的 provider_kind 不能为空。");
        if (questDef.ListingChannels.Count == 0)
            errors.Add(prefix + $"QuestDef {questDef.QuestId} 的 listing_channels 不能为空数组。");
        foreach (StringName channel in questDef.ListingChannels)
        {
            if (channel == "")
                errors.Add(prefix + $"QuestDef {questDef.QuestId} 的 listing_channels 包含空值。");
        }

        if (questDef.Objectives.Count == 0)
            errors.Add(prefix + $"QuestDef {questDef.QuestId} 至少需要一个 objective_def。");
        var seenObjectiveIds = new HashSet<StringName>();
        foreach (QuestObjectiveDefinition objective in questDef.Objectives)
        {
            if (objective == null || objective.ObjectiveId == "")
            {
                errors.Add(prefix + $"QuestDef {questDef.QuestId} 存在空 objective_id。");
                continue;
            }
            if (!seenObjectiveIds.Add(objective.ObjectiveId))
            {
                errors.Add(
                    prefix
                    + $"QuestDef {questDef.QuestId} 存在重复 objective_id {objective.ObjectiveId}。"
                );
                continue;
            }
            if (objective.ObjectiveKind == QuestObjectiveKind.Unknown)
            {
                errors.Add(
                    prefix
                    + $"QuestDef {questDef.QuestId} 的 objective {objective.ObjectiveId} 使用了不支持的 objective_type {objective.ObjectiveType}。"
                );
            }
            if (objective.TargetValue <= 0)
            {
                errors.Add(
                    prefix
                    + $"QuestDef {questDef.QuestId} 的 objective {objective.ObjectiveId} 必须有正 target_value。"
                );
            }
            if (
                objective.ObjectiveKind
                    is QuestObjectiveKind.SubmitItem or QuestObjectiveKind.SettlementAction
                && objective.TargetId == ""
            )
            {
                errors.Add(
                    prefix
                    + $"QuestDef {questDef.QuestId} 的 {objective.ObjectiveType} objective {objective.ObjectiveId} 缺少 target_id。"
                );
            }
        }

        foreach (QuestRewardDefinition reward in questDef.Rewards)
        {
            if (reward == null || reward.RewardKind == QuestRewardKind.Unknown)
            {
                errors.Add(
                    prefix
                    + $"QuestDef {questDef.QuestId} 使用了不支持的 reward_type {reward?.RewardType}。"
                );
                continue;
            }
            if (reward.RewardKind == QuestRewardKind.Gold && reward.GoldAmount <= 0)
                errors.Add(prefix + $"QuestDef {questDef.QuestId} 的 gold reward 必须有正 amount。");
            else if (reward.RewardKind == QuestRewardKind.Item)
            {
                if (reward.ItemId == "")
                    errors.Add(prefix + $"QuestDef {questDef.QuestId} 的 item reward 缺少 item_id。");
                if (reward.ItemQuantity <= 0)
                    errors.Add(prefix + $"QuestDef {questDef.QuestId} 的 item reward 必须有正 quantity。");
            }
            else if (reward.RewardKind == QuestRewardKind.PendingCharacterReward)
            {
                if (reward.PendingRewardMemberId == "")
                    errors.Add(
                        prefix
                        + $"QuestDef {questDef.QuestId} 的 pending_character_reward 缺少 member_id。"
                    );
                if (reward.PendingRewardEntries.Count == 0)
                    errors.Add(
                        prefix
                        + $"QuestDef {questDef.QuestId} 的 pending_character_reward 至少需要一条 entries。"
                    );
                foreach (QuestPendingRewardEntryDefinition entry in reward.PendingRewardEntries)
                {
                    if (entry.EntryKind == PendingCharacterRewardEntryKind.Unknown)
                        errors.Add(
                            prefix
                            + $"QuestDef {questDef.QuestId} has unsupported pending_character_reward entry_type {entry.EntryType}."
                        );
                    if (entry.TargetId == "")
                        errors.Add(
                            prefix
                            + $"QuestDef {questDef.QuestId} 的 pending_character_reward entry 缺少 target_id。"
                        );
                    if (entry.Amount == 0)
                        errors.Add(
                            prefix
                            + $"QuestDef {questDef.QuestId} 的 pending_character_reward entry amount 不能为 0。"
                        );
                }
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
