public static class UiDisplayLabels
{
    public static string SettlementService(string value) => value switch
    {
        "rest" => "休息恢复",
        "shop" => "物品交易",
        "forge" or "blacksmith" => "装备锻造",
        "contract_board" => "任务委托",
        "bounty_board" => "悬赏任务",
        "npc_quest_offer" => "领取任务",
        _ => value,
    };

    public static string Faction(string value) => value switch
    {
        "player" => "友方",
        "enemy" => "敌方",
        "neutral" => "中立",
        _ => value,
    };

    public static string AgeStage(string value) => value switch
    {
        "Child" => "儿童",
        "Teen" => "少年",
        "Young Adult" => "青年",
        "Adult" => "成年",
        "Middle Age" => "中年",
        "Old" => "老年",
        "Venerable" => "高龄",
        _ => value,
    };

    public static string ControlMode(string value) => value switch
    {
        "manual" => "玩家操作",
        "ai" => "自动行动",
        _ => value,
    };

    public static string BodySize(string value) => value switch
    {
        "tiny" => "微型",
        "small" => "小型",
        "medium" => "中型",
        "large" => "大型",
        "huge" => "巨型",
        "gargantuan" => "超巨型",
        _ => value,
    };
}
