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
    public IReadOnlyDictionary<StringName, QuestDef> QuestDefs { get; init; }
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
    private static readonly StringName RequirementQuestCompleted = "quest_completed";
    private static readonly StringName RequirementQuestActive = "quest_active";
    private static readonly StringName RequirementQuestNotCompleted = "quest_not_completed";

    internal QuestAcceptAvailabilityResult Evaluate(
        QuestDef questDef,
        QuestAcceptContext context
    )
    {
        if (questDef.accept_requirements == null || questDef.accept_requirements.Count == 0)
            return QuestAcceptAvailabilityResult.Accept();

        foreach (Godot.Collections.Dictionary requirement in questDef.accept_requirements)
        {
            StringName requirementType = ReadStringName(requirement, "requirement_type");
            QuestAcceptAvailabilityResult result = requirementType switch
            {
                _ when requirementType == RequirementQuestCompleted =>
                    EvaluateQuestCompleted(requirement, context),
                _ when requirementType == RequirementQuestActive =>
                    EvaluateQuestActive(requirement, context),
                _ when requirementType == RequirementQuestNotCompleted =>
                    EvaluateQuestNotCompleted(requirement, context),
                _ => QuestAcceptAvailabilityResult.Reject(
                    "unknown_requirement",
                    $"未知需求类型：{requirementType}"
                ),
            };

            if (!result.CanAccept)
                return result;
        }

        return QuestAcceptAvailabilityResult.Accept();
    }

    private static QuestAcceptAvailabilityResult EvaluateQuestCompleted(
        Godot.Collections.Dictionary requirement,
        QuestAcceptContext context
    )
    {
        if (context.PartyState == null)
            return QuestAcceptAvailabilityResult.Reject("missing_party_state", "PartyState 不存在，无法评估任务接取条件。");

        StringName questId = ReadStringName(requirement, "quest_id");
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
        Godot.Collections.Dictionary requirement,
        QuestAcceptContext context
    )
    {
        if (context.PartyState == null)
            return QuestAcceptAvailabilityResult.Reject("missing_party_state", "PartyState 不存在，无法评估任务接取条件。");

        StringName questId = ReadStringName(requirement, "quest_id");
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
        Godot.Collections.Dictionary requirement,
        QuestAcceptContext context
    )
    {
        if (context.PartyState == null)
            return QuestAcceptAvailabilityResult.Reject("missing_party_state", "PartyState 不存在，无法评估任务接取条件。");

        StringName questId = ReadStringName(requirement, "quest_id");
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
        if (context.QuestDefs.TryGetValue(questId, out QuestDef questDef))
            return questDef.display_name;
        return questId.ToString();
    }

    private static StringName ReadStringName(Godot.Collections.Dictionary dict, string key)
    {
        if (dict == null || !dict.ContainsKey(key))
            return "";
        Variant value = dict[key];
        if (value.VariantType == Variant.Type.StringName)
            return (StringName)value;
        if (value.VariantType == Variant.Type.String)
            return new StringName((string)value);
        return "";
    }
}
