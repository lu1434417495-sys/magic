using System.Collections.Generic;
using Godot;

public static class EnemyAiTargetSelectorRules
{
    public const string NearestEnemyId = "nearest_enemy";
    public const string LowestHpEnemyId = "lowest_hp_enemy";
    public const string NearestRoleThreatEnemyId = "nearest_role_threat_enemy";
    public const string NearestAllyId = "nearest_ally";
    public const string LowestHpAllyId = "lowest_hp_ally";
    public const string SelfId = "self";

    public static readonly StringName NearestEnemy = NearestEnemyId;
    public static readonly StringName LowestHpEnemy = LowestHpEnemyId;
    public static readonly StringName NearestRoleThreatEnemy = NearestRoleThreatEnemyId;
    public static readonly StringName NearestAlly = NearestAllyId;
    public static readonly StringName LowestHpAlly = LowestHpAllyId;
    public static readonly StringName Self = SelfId;

    private static readonly HashSet<string> SupportedSelectors =
        new(System.StringComparer.Ordinal)
        {
            NearestEnemyId,
            LowestHpEnemyId,
            NearestRoleThreatEnemyId,
            NearestAllyId,
            LowestHpAllyId,
            SelfId,
        };

    private static readonly HashSet<string> EnemyFocusSelectors =
        new(System.StringComparer.Ordinal)
        {
            NearestEnemyId,
            LowestHpEnemyId,
            NearestRoleThreatEnemyId,
        };

    public static bool IsSupportedSelector(StringName selector, bool allowEmpty = false)
    {
        string selectorId = selector.ToString();
        if (string.IsNullOrEmpty(selectorId))
        {
            return allowEmpty;
        }
        return SupportedSelectors.Contains(selectorId);
    }

    public static bool IsEnemyFocusSelector(StringName selector) =>
        EnemyFocusSelectors.Contains(selector.ToString());
}
