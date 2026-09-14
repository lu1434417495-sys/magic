public static class UiDisplayLabels
{
    public static string ContingencyTrigger(ContingencyTriggerDefinition trigger) => trigger.TriggerKind switch
    {
        ContingencyTriggerKind.CombatStarted => "战斗开始时",
        ContingencyTriggerKind.HpBelowPercent => $"生命低于 {trigger.Percent}% 时",
        ContingencyTriggerKind.IncomingDamagePercent => $"伤害阈值触发（{trigger.DamagePercent}%）",
        ContingencyTriggerKind.FatalDamageIncoming => "致命伤害到来前",
        ContingencyTriggerKind.StatusApplied => "被施加指定状态时",
        ContingencyTriggerKind.EnemyEnterRadius => $"敌人进入 {trigger.Radius} 格范围时",
        ContingencyTriggerKind.AffectedBySpell => "受到法术影响时",
        ContingencyTriggerKind.OwnerTurnStarted => "自身回合开始时",
        _ => "未配置触发条件",
    };

    public static string ContingencyRelease(string mode) => mode switch
    {
        "burst_release" => "集中释放",
        "sequential_release" => "依次释放",
        _ => "未配置释放方式",
    };

    public static string ContingencyTarget(ContingencyTargetResolverKind target) => target switch
    {
        ContingencyTargetResolverKind.Self => "自身",
        ContingencyTargetResolverKind.TriggerSource => "触发来源",
        ContingencyTargetResolverKind.TriggerTarget => "触发目标",
        ContingencyTargetResolverKind.NearestEnemyToOwner => "自身附近最近的敌人",
        ContingencyTargetResolverKind.NearestEnemyToTriggerCell => "触发位置附近最近的敌人",
        ContingencyTargetResolverKind.OwnerCenteredArea => "以自身为中心的区域",
        ContingencyTargetResolverKind.AttackerCell => "攻击者所在位置",
        ContingencyTargetResolverKind.EmptyCellNearOwner => "自身附近的空格",
        _ => "未配置目标",
    };

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
