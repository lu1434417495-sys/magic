using System.Collections.Generic;
using Godot;

public static class BattleTargetTeamRules
{
    private static readonly StringName EmptyStringName = "";

    public readonly record struct TargetFilterOptions(
        bool AllowDeadTargets = false,
        bool MadnessTargetAnyTeam = false
    )
    {
        public static readonly TargetFilterOptions Default = new();
    }

    public static StringName ResolveEffectTargetFilter(
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition
    )
    {
        if (effectDefinition != null && !IsEmpty(effectDefinition.EffectTargetTeamFilter))
            return effectDefinition.EffectTargetTeamFilter;

        return skillDefinition?.CombatProfile?.TargetTeamFilter ?? EmptyStringName;
    }

    public static bool IsUnitValidForFilter(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_team_filter,
        TargetFilterOptions? options = null
    )
    {
        if (target_unit == null)
            return false;

        TargetFilterOptions opts = options ?? TargetFilterOptions.Default;

        if (!opts.AllowDeadTargets && !target_unit.IsAlive())
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

    internal static bool IsUnitValidForFilter(
        BattleUnitState source_unit,
        BattleUnitReadView targetUnit,
        StringName target_team_filter,
        TargetFilterOptions? options = null
    )
    {
        if (!targetUnit.IsValid)
            return false;

        TargetFilterOptions opts = options ?? TargetFilterOptions.Default;

        if (!opts.AllowDeadTargets && !targetUnit.IsAlive)
            return false;

        if (source_unit != null && opts.MadnessTargetAnyTeam)
        {
            if (target_team_filter == BattleTypedNames.TargetFilterAlly
                || target_team_filter == BattleTypedNames.TargetFilterEnemy)
            {
                return targetUnit.UnitId != source_unit.unit_id;
            }
        }

        BattleTargetFilter filter = BattleTypedNames.ToTargetFilter(target_team_filter);
        if (filter == BattleTargetFilter.Any)
            return true;

        switch (filter)
        {
            case BattleTargetFilter.Self:
                return source_unit != null && targetUnit.UnitId == source_unit.unit_id;
            case BattleTargetFilter.Ally:
                return source_unit != null && targetUnit.FactionId == source_unit.faction_id;
            case BattleTargetFilter.Enemy:
                return source_unit != null && targetUnit.FactionId != source_unit.faction_id;
            default:
                return false;
        }
    }

    internal static bool IsUnitValidForFilter(
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        StringName target_team_filter,
        TargetFilterOptions? options = null
    )
    {
        if (!targetUnit.IsValid)
            return false;

        TargetFilterOptions opts = options ?? TargetFilterOptions.Default;

        if (!opts.AllowDeadTargets && !targetUnit.IsAlive)
            return false;

        if (sourceUnit.IsValid && opts.MadnessTargetAnyTeam)
        {
            if (target_team_filter == BattleTypedNames.TargetFilterAlly
                || target_team_filter == BattleTypedNames.TargetFilterEnemy)
            {
                return targetUnit.UnitId != sourceUnit.UnitId;
            }
        }

        BattleTargetFilter filter = BattleTypedNames.ToTargetFilter(target_team_filter);
        if (filter == BattleTargetFilter.Any)
            return true;

        switch (filter)
        {
            case BattleTargetFilter.Self:
                return sourceUnit.IsValid && targetUnit.UnitId == sourceUnit.UnitId;
            case BattleTargetFilter.Ally:
                return sourceUnit.IsValid && targetUnit.FactionId == sourceUnit.FactionId;
            case BattleTargetFilter.Enemy:
                return sourceUnit.IsValid && targetUnit.FactionId != sourceUnit.FactionId;
            default:
                return false;
        }
    }

    public static bool IsBeneficialFilter(StringName target_team_filter)
    {
        BattleTargetFilter filter = BattleTypedNames.ToTargetFilter(target_team_filter);
        return filter is BattleTargetFilter.Ally or BattleTargetFilter.Self;
    }

    public static bool IsEnemyFilter(StringName target_team_filter)
    {
        return BattleTypedNames.ToTargetFilter(target_team_filter) == BattleTargetFilter.Enemy;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
