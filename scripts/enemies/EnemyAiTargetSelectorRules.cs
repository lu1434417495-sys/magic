using Godot;

internal enum EnemyAiTargetSelector
{
    Unknown,
    None,
    NearestEnemy,
    LowestHpEnemy,
    NearestRoleThreatEnemy,
    NearestAlly,
    LowestHpAlly,
    Self,
}

internal static class EnemyAiTargetSelectorRules
{
    private static readonly StringName NearestEnemyName = "nearest_enemy";
    private static readonly StringName LowestHpEnemyName = "lowest_hp_enemy";
    private static readonly StringName NearestRoleThreatEnemyName = "nearest_role_threat_enemy";
    private static readonly StringName NearestAllyName = "nearest_ally";
    private static readonly StringName LowestHpAllyName = "lowest_hp_ally";
    private static readonly StringName SelfName = "self";

    internal static StringName NearestEnemy => NearestEnemyName;
    internal static StringName LowestHpEnemy => LowestHpEnemyName;
    internal static StringName NearestRoleThreatEnemy => NearestRoleThreatEnemyName;
    internal static StringName NearestAlly => NearestAllyName;
    internal static StringName LowestHpAlly => LowestHpAllyName;
    internal static StringName Self => SelfName;

    internal static EnemyAiTargetSelector ToKind(StringName selector)
    {
        return selector.ToString() switch
        {
            "" => EnemyAiTargetSelector.None,
            "nearest_enemy" => EnemyAiTargetSelector.NearestEnemy,
            "lowest_hp_enemy" => EnemyAiTargetSelector.LowestHpEnemy,
            "nearest_role_threat_enemy" => EnemyAiTargetSelector.NearestRoleThreatEnemy,
            "nearest_ally" => EnemyAiTargetSelector.NearestAlly,
            "lowest_hp_ally" => EnemyAiTargetSelector.LowestHpAlly,
            "self" => EnemyAiTargetSelector.Self,
            _ => EnemyAiTargetSelector.Unknown,
        };
    }

    internal static StringName ToStringName(EnemyAiTargetSelector selector) =>
        selector switch
        {
            EnemyAiTargetSelector.NearestEnemy => NearestEnemyName,
            EnemyAiTargetSelector.LowestHpEnemy => LowestHpEnemyName,
            EnemyAiTargetSelector.NearestRoleThreatEnemy => NearestRoleThreatEnemyName,
            EnemyAiTargetSelector.NearestAlly => NearestAllyName,
            EnemyAiTargetSelector.LowestHpAlly => LowestHpAllyName,
            EnemyAiTargetSelector.Self => SelfName,
            _ => "",
        };

    internal static bool IsSupportedSelector(StringName selector, bool allowEmpty = false)
    {
        EnemyAiTargetSelector selectorKind = ToKind(selector);
        return selectorKind != EnemyAiTargetSelector.Unknown
            && (allowEmpty || selectorKind != EnemyAiTargetSelector.None);
    }

    internal static bool IsEnemyFocusSelector(StringName selector) =>
        IsEnemyFocusSelector(ToKind(selector));

    internal static bool IsEnemyFocusSelector(EnemyAiTargetSelector selector) =>
        selector
            is EnemyAiTargetSelector.NearestEnemy
                or EnemyAiTargetSelector.LowestHpEnemy
                or EnemyAiTargetSelector.NearestRoleThreatEnemy;
}
