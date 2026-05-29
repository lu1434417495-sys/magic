using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

[GlobalClass]
public partial class BattleTargetTeamRules : RefCounted
{
    private static readonly StringName EmptyStringName = "";

    public readonly record struct TargetFilterOptions(
        bool AllowDeadTargets = false,
        bool MadnessTargetAnyTeam = false
    )
    {
        public static readonly TargetFilterOptions Default = new();
    }

    public static StringName resolve_effect_target_filter(
        SkillDef skill_def,
        CombatEffectDef effect_def
    )
    {
        if (effect_def != null && !IsEmpty(effect_def.effect_target_team_filter))
            return effect_def.effect_target_team_filter;

        return skill_def?.combat_profile?.target_team_filter ?? EmptyStringName;
    }

    public static bool is_unit_valid_for_filter(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_team_filter,
        TargetFilterOptions? options = null
    )
    {
        if (target_unit == null)
            return false;

        TargetFilterOptions opts = options ?? TargetFilterOptions.Default;

        if (!opts.AllowDeadTargets && !target_unit.is_alive)
            return false;

        if (source_unit != null && opts.MadnessTargetAnyTeam)
        {
            if (target_team_filter == BattleTypedNames.TargetFilterAlly
                || target_team_filter == BattleTypedNames.TargetFilterEnemy)
            {
                return target_unit.unit_id != source_unit.unit_id;
            }
        }

        BattleTargetFilter filter = BattleTypedNames.ToTargetFilter(target_team_filter);
        if (filter == BattleTargetFilter.Any)
            return true;

        switch (filter)
        {
            case BattleTargetFilter.Self:
                return source_unit != null && target_unit.unit_id == source_unit.unit_id;
            case BattleTargetFilter.Ally:
                return source_unit != null && target_unit.faction_id == source_unit.faction_id;
            case BattleTargetFilter.Enemy:
                return source_unit != null && target_unit.faction_id != source_unit.faction_id;
            default:
                return false;
        }
    }

    public static bool is_beneficial_filter(StringName target_team_filter)
    {
        BattleTargetFilter filter = BattleTypedNames.ToTargetFilter(target_team_filter);
        return filter is BattleTargetFilter.Ally or BattleTargetFilter.Self;
    }

    public static bool is_enemy_filter(StringName target_team_filter)
    {
        return BattleTypedNames.ToTargetFilter(target_team_filter) == BattleTargetFilter.Enemy;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
