using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

// Read-only projection of the canonical journal. Opening/browsing never accepts or claims quests.
internal static class GameRuntimeQuestJournalBuilder
{
    internal static QuestJournalWindowData Build(PartyState party, GameContentCatalog catalog,
        WorldGenerationDefinition generation)
    {
        if (party == null || catalog == null)
            return QuestJournalWindowData.Empty;
        var entries = new List<QuestJournalEntryData>();
        foreach (QuestState state in party.GetClaimableQuestsTyped())
            Add(state.quest_id, state, QuestJournalStage.Claimable);
        foreach (QuestState state in party.GetActiveQuestsTyped())
            Add(state.quest_id, state, QuestJournalStage.Active);
        foreach (StringName id in party.GetCompletedQuestIdsTyped())
            Add(id, null, QuestJournalStage.Completed);
        foreach (QuestState state in party.GetFailedQuestsTyped())
            Add(state.quest_id, state, QuestJournalStage.Failed);
        return new QuestJournalWindowData(entries.AsReadOnly());

        void Add(StringName id, QuestState state, QuestJournalStage stage)
        {
            QuestDefinition quest = catalog.GetQuestDefTyped(id);
            if (quest == null)
                throw new InvalidOperationException($"Journal quest definition missing: {id}");
            string provider = ResolveProviderName(quest, generation);
            string nextStep = stage switch
            {
                QuestJournalStage.Claimable => $"目标已完成，回到{provider}处交谈并交付。",
                QuestJournalStage.Completed => "任务已完成，奖励已领取。",
                QuestJournalStage.Failed => quest.CanRestartAfterFailure
                    ? $"本次任务已失败，可返回{provider}处重新接取。" : "本次任务已失败。",
                _ => quest.Objectives.Any(o => o.ObjectiveKind == QuestObjectiveKind.SubmitItem)
                    ? $"收集所需物资后，回到{provider}处交谈并提交。" : $"完成下列目标后，回到{provider}处交谈。",
            };
            var objectives = quest.Objectives.Select(o => new QuestJournalObjectiveData(
                DescribeObjective(o, catalog),
                stage is QuestJournalStage.Completed or QuestJournalStage.Claimable
                    ? o.TargetValue : state?.GetObjectiveProgress(o.ObjectiveId) ?? 0,
                o.TargetValue)).ToArray();
            var rewards = new List<string>();
            foreach (QuestRewardDefinition reward in quest.Rewards)
            {
                switch (reward.RewardKind)
                {
                    case QuestRewardKind.Gold: rewards.Add($"{reward.GoldAmount} 金"); break;
                    case QuestRewardKind.Item:
                        rewards.Add($"{ItemName(reward.ItemId, catalog)} × {reward.ItemQuantity}"); break;
                    case QuestRewardKind.PendingCharacterReward:
                        string member = party.GetMemberState(reward.PendingRewardMemberId)?.display_name ?? "角色";
                        foreach (QuestPendingRewardEntryDefinition entry in reward.PendingRewardEntries)
                            rewards.Add($"{member} · {DescribeCharacterReward(entry, catalog)}");
                        break;
                }
            }
            entries.Add(new QuestJournalEntryData(id, quest.DisplayName, quest.Description, stage,
                provider, nextStep, Array.AsReadOnly(objectives), rewards.AsReadOnly()));
        }
    }

    internal static string ResolveProviderName(QuestDefinition quest, WorldGenerationDefinition generation)
    {
        foreach (FacilityDefinition facility in generation?.EffectiveFacilityLibrary ?? Array.Empty<FacilityDefinition>())
            foreach (FacilityNpcDefinition npc in facility.BoundServiceNpcs)
                if (npc.InteractionScriptId == quest.ProviderInteractionId.ToString())
                    return npc.DisplayName;
        return quest.ProviderKind == "npc" ? "委托人" : "任务板";
    }

    private static string DescribeObjective(QuestObjectiveDefinition objective, GameContentCatalog catalog)
    {
        string enemy = catalog.GetEnemyTemplateDefinitions().TryGetValue(objective.TargetId, out var target)
            ? target.DisplayName : "敌对目标";
        return objective.ObjectiveKind switch
        {
            QuestObjectiveKind.DefeatEnemy => $"击败{enemy}",
            QuestObjectiveKind.DefeatEnemyInSingleBattle => $"在同一场战斗中击败{enemy}",
            QuestObjectiveKind.SubmitItem => $"提交{ItemName(objective.TargetId, catalog)}",
            QuestObjectiveKind.SettlementAction => "完成据点事务",
            _ => throw new InvalidOperationException($"Unsupported journal objective: {objective.ObjectiveType}"),
        };
    }

    private static string ItemName(StringName id, GameContentCatalog catalog) =>
        catalog.GetItemDefsTyped().TryGetValue(id, out var item) ? item.DisplayName : "物资";

    private static string DescribeCharacterReward(QuestPendingRewardEntryDefinition entry, GameContentCatalog catalog)
    {
        string skill = catalog.GetSkillDefinitionsTyped().TryGetValue(entry.TargetId, out var definition)
            ? definition.DisplayName : "技能";
        string attribute = entry.TargetId.ToString() switch
        {
            "strength" => "力量", "agility" => "敏捷", "constitution" => "体质",
            "perception" => "感知", "intelligence" => "智力", "willpower" => "意志",
            "hp_max" => "生命上限", _ => "属性",
        };
        return entry.EntryKind switch
        {
            PendingCharacterRewardEntryKind.SkillMastery => $"{skill}熟练度 +{entry.Amount}",
            PendingCharacterRewardEntryKind.SkillUnlock => $"习得{skill}",
            PendingCharacterRewardEntryKind.KnowledgeUnlock => "解锁知识",
            PendingCharacterRewardEntryKind.AttributeDelta => $"{attribute} +{entry.Amount}",
            PendingCharacterRewardEntryKind.AttributeProgress => $"{attribute}成长 +{entry.Amount}",
            _ => throw new InvalidOperationException($"Unsupported journal reward: {entry.EntryType}"),
        };
    }
}
