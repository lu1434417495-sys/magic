using System.Collections.Generic;
using Godot;

internal sealed class QuestAcceptContext
{
    public PartyState PartyState { get; init; }
    public PartyWarehouseService WarehouseService { get; init; }
    public int PartyGold { get; init; }
    public int WorldStep { get; init; }
    public string SettlementId { get; init; } = "";
    public int SettlementTier { get; init; }
    public IReadOnlyDictionary<StringName, QuestDefinition> QuestDefs { get; init; }
}

internal sealed class QuestAcceptAvailabilityResult
{
    public bool CanAccept { get; init; }
    public StringName LockReasonId { get; init; } = "";
    public string DisabledReason { get; init; } = "";

    public static QuestAcceptAvailabilityResult Accept() =>
        new() { CanAccept = true };

    public static QuestAcceptAvailabilityResult Reject(StringName lockReasonId, string disabledReason) =>
        new() { CanAccept = false, LockReasonId = lockReasonId, DisabledReason = disabledReason };
}

internal sealed class QuestAcceptRequirementEvaluator
{
    internal QuestAcceptAvailabilityResult Evaluate(
        QuestDefinition questDef,
        QuestAcceptContext context
    )
    {
        if (questDef.AcceptRequirements.Count == 0)
            return QuestAcceptAvailabilityResult.Accept();

        foreach (QuestAcceptRequirementDefinition requirement in questDef.AcceptRequirements)
        {
            QuestAcceptAvailabilityResult result = requirement.RequirementKind switch
            {
                QuestAcceptRequirementKind.QuestCompleted =>
                    EvaluateQuestCompleted(requirement, context),
                QuestAcceptRequirementKind.QuestActive =>
                    EvaluateQuestActive(requirement, context),
                QuestAcceptRequirementKind.QuestNotCompleted =>
                    EvaluateQuestNotCompleted(requirement, context),
                _ => QuestAcceptAvailabilityResult.Reject(
                    "unknown_requirement",
                    $"未知需求类型：{requirement.RequirementType}"
                ),
            };

            if (!result.CanAccept)
                return result;
        }

        return QuestAcceptAvailabilityResult.Accept();
    }

    private static QuestAcceptAvailabilityResult EvaluateQuestCompleted(
        QuestAcceptRequirementDefinition requirement,
        QuestAcceptContext context
    )
    {
        if (context.PartyState == null)
            return QuestAcceptAvailabilityResult.Reject("missing_party_state", "PartyState 不存在，无法评估任务接取条件。");

        StringName questId = requirement.QuestId;
        if (questId == "")
            return QuestAcceptAvailabilityResult.Reject("missing_quest_id", "quest_completed 需求缺少 quest_id。");

        if (context.PartyState.HasCompletedQuest(questId))
            return QuestAcceptAvailabilityResult.Accept();

        return QuestAcceptAvailabilityResult.Reject(
            "quest_not_completed",
            $"需先完成任务：{GetQuestDisplayName(questId, context)}"
        );
    }

    private static QuestAcceptAvailabilityResult EvaluateQuestActive(
        QuestAcceptRequirementDefinition requirement,
        QuestAcceptContext context
    )
    {
        if (context.PartyState == null)
            return QuestAcceptAvailabilityResult.Reject("missing_party_state", "PartyState 不存在，无法评估任务接取条件。");

        StringName questId = requirement.QuestId;
        if (questId == "")
            return QuestAcceptAvailabilityResult.Reject("missing_quest_id", "quest_active 需求缺少 quest_id。");

        if (context.PartyState.HasActiveQuest(questId))
            return QuestAcceptAvailabilityResult.Accept();

        return QuestAcceptAvailabilityResult.Reject(
            "quest_not_active",
            $"需先接取任务：{GetQuestDisplayName(questId, context)}"
        );
    }

    private static QuestAcceptAvailabilityResult EvaluateQuestNotCompleted(
        QuestAcceptRequirementDefinition requirement,
        QuestAcceptContext context
    )
    {
        if (context.PartyState == null)
            return QuestAcceptAvailabilityResult.Reject("missing_party_state", "PartyState 不存在，无法评估任务接取条件。");

        StringName questId = requirement.QuestId;
        if (questId == "")
            return QuestAcceptAvailabilityResult.Reject("missing_quest_id", "quest_not_completed 需求缺少 quest_id。");

        if (!context.PartyState.HasCompletedQuest(questId))
            return QuestAcceptAvailabilityResult.Accept();

        return QuestAcceptAvailabilityResult.Reject(
            "quest_already_completed",
            $"不能重复完成该任务线：{GetQuestDisplayName(questId, context)}"
        );
    }

    private static string GetQuestDisplayName(StringName questId, QuestAcceptContext context)
    {
        if (context.QuestDefs.TryGetValue(questId, out QuestDefinition questDef))
            return questDef.DisplayName;
        return questId.ToString();
    }
}
