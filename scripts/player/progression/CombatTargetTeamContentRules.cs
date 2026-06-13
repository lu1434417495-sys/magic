using Godot;

internal enum CombatTargetTeamFilterKind
{
    Unknown = 0,
    Inherit,
    Enemy,
    Ally,
    Self,
    Any,
}

// Combat target-team content rule constants and validators.
// 翻译自 combat_target_team_content_rules.gd（2026-05-24，数据层 C# 迁移）。
public static class CombatTargetTeamContentRules
{
    private static readonly StringName _TARGET_TEAM_FILTER_ENEMY = "enemy";
    private static readonly StringName _TARGET_TEAM_FILTER_ALLY = "ally";
    private static readonly StringName _TARGET_TEAM_FILTER_SELF = "self";
    private static readonly StringName _TARGET_TEAM_FILTER_ANY = "any";
    private static readonly StringName _EFFECT_TARGET_TEAM_FILTER_INHERIT = "";

    internal static bool IsValidSkillTargetTeamFilter(StringName target_team_filter)
    {
        return IsSkillTargetTeamFilterKind(ToTargetTeamFilterKind(target_team_filter));
    }

    internal static bool IsValidEffectTargetTeamFilter(StringName effect_target_team_filter)
    {
        return ToTargetTeamFilterKind(effect_target_team_filter) != CombatTargetTeamFilterKind.Unknown;
    }

    internal static CombatTargetTeamFilterKind ToTargetTeamFilterKind(StringName value)
    {
        if (value == _EFFECT_TARGET_TEAM_FILTER_INHERIT)
            return CombatTargetTeamFilterKind.Inherit;
        if (value == _TARGET_TEAM_FILTER_ENEMY)
            return CombatTargetTeamFilterKind.Enemy;
        if (value == _TARGET_TEAM_FILTER_ALLY)
            return CombatTargetTeamFilterKind.Ally;
        if (value == _TARGET_TEAM_FILTER_SELF)
            return CombatTargetTeamFilterKind.Self;
        if (value == _TARGET_TEAM_FILTER_ANY)
            return CombatTargetTeamFilterKind.Any;
        return CombatTargetTeamFilterKind.Unknown;
    }

    internal static bool IsSkillTargetTeamFilterKind(CombatTargetTeamFilterKind kind)
    {
        return kind
            is CombatTargetTeamFilterKind.Enemy
                or CombatTargetTeamFilterKind.Ally
                or CombatTargetTeamFilterKind.Self
                or CombatTargetTeamFilterKind.Any;
    }

    internal static StringName ToStringName(CombatTargetTeamFilterKind kind)
    {
        return kind switch
        {
            CombatTargetTeamFilterKind.Inherit => _EFFECT_TARGET_TEAM_FILTER_INHERIT,
            CombatTargetTeamFilterKind.Enemy => _TARGET_TEAM_FILTER_ENEMY,
            CombatTargetTeamFilterKind.Ally => _TARGET_TEAM_FILTER_ALLY,
            CombatTargetTeamFilterKind.Self => _TARGET_TEAM_FILTER_SELF,
            CombatTargetTeamFilterKind.Any => _TARGET_TEAM_FILTER_ANY,
            _ => "",
        };
    }

    public static string ValidSkillTargetTeamFilterLabel()
    {
        return "enemy, ally, self, any";
    }

    public static string ValidEffectTargetTeamFilterLabel()
    {
        return "<inherit>, enemy, ally, self, any";
    }
}
