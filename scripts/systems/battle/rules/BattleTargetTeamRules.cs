using System.Collections.Generic;
using Godot;
using static GdInterop;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleTargetTeamRules : RefCounted
{
    private static readonly StringName EmptyStringName = "";

    private readonly record struct TargetFilterOptions(
        bool AllowDeadTargets,
        bool MadnessTargetAnyTeam,
        HashSet<StringName> MadnessTargetFilters
    );

    public static StringName resolve_effect_target_filter(GodotObject skill_def, GodotObject effect_def)
    {
        StringName effectTargetFilter = GetStringName(effect_def, "effect_target_team_filter");
        if (!IsEmpty(effectTargetFilter))
        {
            return effectTargetFilter;
        }
        GodotObject combatProfile = GetObject(skill_def, "combat_profile");
        return combatProfile != null ? GetStringName(combatProfile, "target_team_filter") : EmptyStringName;
    }

    public static bool is_unit_valid_for_filter(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_team_filter,
        GDictionary options = null)
    {
        TargetFilterOptions parsedOptions = ParseOptions(options);
        if (target_unit == null)
        {
            return false;
        }
        if (!parsedOptions.AllowDeadTargets && !target_unit.is_alive)
        {
            return false;
        }
        if (source_unit != null && parsedOptions.MadnessTargetAnyTeam)
        {
            if (parsedOptions.MadnessTargetFilters.Contains(target_team_filter))
            {
                return target_unit.unit_id != source_unit.unit_id;
            }
        }
        BattleTargetFilter filter = BattleTypedNames.ToTargetFilter(target_team_filter);
        if (filter == BattleTargetFilter.Any)
        {
            return true;
        }
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

    private static TargetFilterOptions ParseOptions(GDictionary options)
    {
        return new TargetFilterOptions(
            GetBool(options, "allow_dead_targets"),
            GetBool(options, "madness_target_any_team"),
            ResolveMadnessTargetFilters(options)
        );
    }

    private static HashSet<StringName> ResolveMadnessTargetFilters(GDictionary options)
    {
        var filters = new HashSet<StringName>();
        if (TryGet(options, "madness_target_filters", out Variant filtersValue)
            && filtersValue.VariantType == Variant.Type.Array)
        {
            foreach (Variant filterValue in filtersValue.AsGodotArray())
            {
                StringName filter = new(filterValue.ToString());
                if (!IsEmpty(filter))
                {
                    filters.Add(filter);
                }
            }
            return filters;
        }
        filters.Add(BattleTypedNames.TargetFilterAlly);
        filters.Add(BattleTypedNames.TargetFilterEnemy);
        return filters;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

}
