using Godot;

// Combat target-team content rule constants and validators.
// 翻译自 combat_target_team_content_rules.gd（2026-05-24，数据层 C# 迁移）。
[GlobalClass]
public partial class CombatTargetTeamContentRules : RefCounted
{
    private static readonly StringName _TARGET_TEAM_FILTER_ENEMY = "enemy";
    private static readonly StringName _TARGET_TEAM_FILTER_ALLY = "ally";
    private static readonly StringName _TARGET_TEAM_FILTER_SELF = "self";
    private static readonly StringName _TARGET_TEAM_FILTER_ANY = "any";
    private static readonly StringName _EFFECT_TARGET_TEAM_FILTER_INHERIT = "";

    public static StringName TARGET_TEAM_FILTER_ENEMY() => _TARGET_TEAM_FILTER_ENEMY;
    public static StringName TARGET_TEAM_FILTER_ALLY() => _TARGET_TEAM_FILTER_ALLY;
    public static StringName TARGET_TEAM_FILTER_SELF() => _TARGET_TEAM_FILTER_SELF;
    public static StringName TARGET_TEAM_FILTER_ANY() => _TARGET_TEAM_FILTER_ANY;
    public static StringName EFFECT_TARGET_TEAM_FILTER_INHERIT() => _EFFECT_TARGET_TEAM_FILTER_INHERIT;

    public static bool is_valid_skill_target_team_filter(StringName target_team_filter)
    {
        return target_team_filter == _TARGET_TEAM_FILTER_ENEMY ||
            target_team_filter == _TARGET_TEAM_FILTER_ALLY ||
            target_team_filter == _TARGET_TEAM_FILTER_SELF ||
            target_team_filter == _TARGET_TEAM_FILTER_ANY;
    }

    public static bool is_valid_effect_target_team_filter(StringName effect_target_team_filter)
    {
        return effect_target_team_filter == _EFFECT_TARGET_TEAM_FILTER_INHERIT ||
            is_valid_skill_target_team_filter(effect_target_team_filter);
    }

    public static string valid_skill_target_team_filter_label()
    {
        return "enemy, ally, self, any";
    }

    public static string valid_effect_target_team_filter_label()
    {
        return "<inherit>, enemy, ally, self, any";
    }
}
