using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class EnemyAiTargetSelectorRules : RefCounted
{
    private const string NearestEnemy = "nearest_enemy";
    private const string LowestHpEnemy = "lowest_hp_enemy";
    private const string NearestRoleThreatEnemy = "nearest_role_threat_enemy";
    private const string NearestAlly = "nearest_ally";
    private const string LowestHpAlly = "lowest_hp_ally";
    private const string Self = "self";

    public static string NEAREST_ENEMY() => NearestEnemy;

    public static string LOWEST_HP_ENEMY() => LowestHpEnemy;

    public static string NEAREST_ROLE_THREAT_ENEMY() => NearestRoleThreatEnemy;

    public static string NEAREST_ALLY() => NearestAlly;

    public static string LOWEST_HP_ALLY() => LowestHpAlly;

    public static string SELF() => Self;

    public static GDictionary ANY_TARGET_SELECTORS() =>
        new()
        {
            [NearestEnemy] = true,
            [LowestHpEnemy] = true,
            [NearestRoleThreatEnemy] = true,
            [NearestAlly] = true,
            [LowestHpAlly] = true,
            [Self] = true,
        };

    public static GDictionary ENEMY_TARGET_SELECTORS() =>
        new()
        {
            [NearestEnemy] = true,
            [LowestHpEnemy] = true,
            [NearestRoleThreatEnemy] = true,
        };

    public static bool is_supported_selector(StringName selector, bool allow_empty = false)
    {
        if (selector == (StringName)"")
            return allow_empty;
        return ANY_TARGET_SELECTORS().ContainsKey(selector.ToString());
    }

    public static GArray validate_target_selector(
        string label,
        StringName selector,
        bool allow_empty = false,
        GDictionary allowed_selectors = null
    )
    {
        var errors = new GArray();
        var supportedSelectors = ANY_TARGET_SELECTORS();
        var allowed = allowed_selectors ?? supportedSelectors;
        if (selector == (StringName)"")
        {
            if (!allow_empty)
                errors.Add($"{label} is missing target_selector.");
            return errors;
        }
        var selectorKey = selector.ToString();
        if (
            !supportedSelectors.ContainsKey(selectorKey)
            || (allowed.Count > 0 && !allowed.ContainsKey(selectorKey))
        )
            errors.Add($"{label} declares unsupported target_selector {selectorKey}.");
        return errors;
    }
}
