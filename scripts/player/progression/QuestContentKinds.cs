using Godot;

internal enum QuestObjectiveKind
{
    Unknown = 0,
    SubmitItem,
    DefeatEnemy,
    DefeatEnemyInSingleBattle,
    SettlementAction,
}

internal enum QuestRewardKind
{
    Unknown = 0,
    Gold,
    Item,
    PendingCharacterReward,
}

internal static class QuestContentKinds
{
    internal static StringName ToStringName(QuestObjectiveKind kind) => kind switch
    {
        QuestObjectiveKind.SubmitItem => "submit_item",
        QuestObjectiveKind.DefeatEnemy => "defeat_enemy",
        QuestObjectiveKind.DefeatEnemyInSingleBattle => "defeat_enemy_in_single_battle",
        QuestObjectiveKind.SettlementAction => "settlement_action",
        _ => "",
    };

    internal static QuestObjectiveKind ToObjectiveKind(StringName value)
    {
        if (value == new StringName("submit_item")) return QuestObjectiveKind.SubmitItem;
        if (value == new StringName("defeat_enemy")) return QuestObjectiveKind.DefeatEnemy;
        if (value == new StringName("defeat_enemy_in_single_battle")) return QuestObjectiveKind.DefeatEnemyInSingleBattle;
        if (value == new StringName("settlement_action")) return QuestObjectiveKind.SettlementAction;
        return QuestObjectiveKind.Unknown;
    }

    internal static bool IsEnemyDefeatObjectiveKind(QuestObjectiveKind kind) =>
        kind is QuestObjectiveKind.DefeatEnemy or QuestObjectiveKind.DefeatEnemyInSingleBattle;

    internal static StringName ToStringName(QuestRewardKind kind) => kind switch
    {
        QuestRewardKind.Gold => "gold",
        QuestRewardKind.Item => "item",
        QuestRewardKind.PendingCharacterReward => "pending_character_reward",
        _ => "",
    };

    internal static QuestRewardKind ToRewardKind(StringName value)
    {
        if (value == new StringName("gold")) return QuestRewardKind.Gold;
        if (value == new StringName("item")) return QuestRewardKind.Item;
        if (value == new StringName("pending_character_reward")) return QuestRewardKind.PendingCharacterReward;
        return QuestRewardKind.Unknown;
    }
}
