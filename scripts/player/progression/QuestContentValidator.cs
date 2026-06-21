using System.Collections.Generic;
using Godot;

public static class QuestContentValidator
{
    public static List<string> ValidateTyped(
        IReadOnlyDictionary<StringName, QuestDef> questDefs,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
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
        if (skillDefs == null || skillDefs.Count == 0)
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
            AppendObjectiveReferenceErrors(errors, questDef, itemDefs, enemyTemplates);
            AppendRewardReferenceErrors(errors, questDef, itemDefs, skillDefs);
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
                if (targetId != "" && itemDefs.Count > 0 && !ContainsContentId(itemDefs, targetId))
                    errors.Add(
                        $"Quest {questDef.quest_id} submit_item objective {objectiveId} references missing item {targetId}."
                    );
            }
            else if (QuestDef.ToObjectiveKind(objectiveType) == QuestObjectiveKind.DefeatEnemy)
            {
                if (
                    targetId != ""
                    && enemyTemplates.Count > 0
                    && !ContainsContentId(enemyTemplates, targetId)
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
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        foreach (QuestDef.RewardEntryData reward in questDef.GetRewardEntriesTyped())
        {
            var rewardType = reward.RewardType;
            if (QuestDef.ToRewardKind(rewardType) == QuestRewardKind.Item)
            {
                var rewardItemId = reward.ItemId;
                if (
                    rewardItemId != ""
                    && itemDefs.Count > 0
                    && !ContainsContentId(itemDefs, rewardItemId)
                )
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
                    skillDefs
                );
            }
        }
    }

    private static void AppendPendingCharacterRewardReferenceErrors(
        ICollection<string> errors,
        QuestDef questDef,
        QuestDef.RewardEntryData reward,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
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
                if (targetId != "" && skillDefs.Count > 0 && !ContainsContentId(skillDefs, targetId))
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

    private static bool ContainsContentId<T>(
        IReadOnlyDictionary<StringName, T> source,
        StringName contentId
    )
    {
        if (source == null || contentId == "")
            return false;
        if (source.ContainsKey(contentId))
            return true;

        string expected = contentId.ToString();
        foreach (StringName key in source.Keys)
        {
            if (key != "" && key.ToString() == expected)
                return true;
        }
        return false;
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
