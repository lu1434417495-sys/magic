using System;
using Godot;
using System.Collections.Generic;

public sealed class BattleAiUnitBlackboardSnapshot
{
    public bool madness_ai_control;
    public bool madness_target_any_team;
    public bool low_luck_reverse_fate_used;
    public bool low_luck_black_star_wedge_used;
    public bool meteor_protected_ally;
    public bool protected_ally;
    public bool summoned;
    public bool temporary_unit;
    public StringName summon_source_unit_id = "";

    internal static BattleAiUnitBlackboardSnapshot FromBlackboard(BattleAiBlackboard blackboard)
    {
        if (blackboard == null)
            return new BattleAiUnitBlackboardSnapshot();

        return new BattleAiUnitBlackboardSnapshot
        {
            madness_ai_control = blackboard.madness_ai_control,
            madness_target_any_team = blackboard.madness_target_any_team,
            low_luck_reverse_fate_used = blackboard.low_luck_reverse_fate_used,
            low_luck_black_star_wedge_used = blackboard.low_luck_black_star_wedge_used,
            meteor_protected_ally = blackboard.meteor_protected_ally,
            protected_ally = blackboard.protected_ally,
            summoned = blackboard.summoned,
            temporary_unit = blackboard.temporary_unit,
            summon_source_unit_id = ProgressionDataUtils.to_string_name(
                blackboard.summon_source_unit_id
            ),
        };
    }

    internal Godot.Collections.Dictionary ToPayload()
    {
        var result = new Godot.Collections.Dictionary();
        AddBool(result, "madness_ai_control", madness_ai_control);
        AddBool(result, "madness_target_any_team", madness_target_any_team);
        AddBool(result, "low_luck_reverse_fate_used", low_luck_reverse_fate_used);
        AddBool(result, "low_luck_black_star_wedge_used", low_luck_black_star_wedge_used);
        AddBool(result, "meteor_protected_ally", meteor_protected_ally);
        AddBool(result, "protected_ally", protected_ally);
        AddBool(result, "summoned", summoned);
        AddBool(result, "temporary_unit", temporary_unit);
        if (summon_source_unit_id != "")
            result["summon_source_unit_id"] = summon_source_unit_id;
        return result;
    }

    private static void AddBool(Godot.Collections.Dictionary result, string key, bool value)
    {
        if (value)
            result[key] = true;
    }
}

public sealed class BattleAiUnitSnapshot
{
    public StringName unit_id = "";

    public string display_name = "";

    public StringName faction_id = "";

    public Vector2I coord = new(-1, -1);

    public Vector2I footprint_size = Vector2I.One;

    public List<Vector2I> occupied_coords = new();

    public bool is_alive;

    public int current_hp;

    public int current_ap;

    public int current_mp;

    public int current_stamina;

    public int current_aura;

    public int current_move_points;

    public bool has_taken_action_this_turn;

    public bool has_moved_this_turn;

    public bool can_use_locked_move_points_this_turn;

    public List<StringName> known_active_skill_ids = new();

    public Dictionary<StringName, int> known_skill_level_map = new();

    public Dictionary<StringName, int> cooldowns = new();

    public BattleAiUnitBlackboardSnapshot ai_blackboard = new();

    public List<StringName> status_ids = new();

    public static BattleAiUnitSnapshot FromUnit(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            BattleAiPayloadGuard.FailLoud(
                "BattleAiUnitSnapshot.FromUnit requires BattleUnitState.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source"] = "BattleAiUnitSnapshot",
                }
            );

            return null;
        }

        var snapshot = new BattleAiUnitSnapshot();

        snapshot.unit_id = ProgressionDataUtils.to_string_name(unitState.unit_id);

        snapshot.display_name = unitState.display_name;

        snapshot.faction_id = ProgressionDataUtils.to_string_name(unitState.faction_id);

        snapshot.coord = unitState.coord;

        snapshot.footprint_size = unitState.footprint_size;

        snapshot.occupied_coords = CopyVector2IArray(unitState.occupied_coords);

        snapshot.is_alive = unitState.is_alive;

        snapshot.current_hp = unitState.current_hp;

        snapshot.current_ap = unitState.current_ap;

        snapshot.current_mp = unitState.current_mp;

        snapshot.current_stamina = unitState.current_stamina;

        snapshot.current_aura = unitState.current_aura;

        snapshot.current_move_points = unitState.current_move_points;

        snapshot.has_taken_action_this_turn = unitState.has_taken_action_this_turn;

        snapshot.has_moved_this_turn = unitState.has_moved_this_turn;

        snapshot.can_use_locked_move_points_this_turn =
            unitState.can_use_locked_move_points_this_turn;

        snapshot.known_active_skill_ids = CopyStringNameArray(unitState.known_active_skill_ids);

        snapshot.known_skill_level_map = unitState.GetKnownSkillLevelsTyped();

        snapshot.cooldowns = unitState.GetCooldownsTyped();

        snapshot.ai_blackboard = BattleAiUnitBlackboardSnapshot.FromBlackboard(unitState.ai_blackboard);

        snapshot.status_ids = CopyStringNameList(unitState.GetSortedStatusEffectIdsTyped());

        return snapshot;
    }

    internal Godot.Collections.Dictionary ToPayload()
    {
        return new Godot.Collections.Dictionary
        {
            { "unit_id", unit_id },
            { "display_name", display_name },
            { "faction_id", faction_id },
            { "coord", coord },
            { "footprint_size", footprint_size },
            { "occupied_coords", ToVector2IArray(occupied_coords) },
            { "is_alive", is_alive },
            { "current_hp", current_hp },
            { "current_ap", current_ap },
            { "current_mp", current_mp },
            { "current_stamina", current_stamina },
            { "current_aura", current_aura },
            { "current_move_points", current_move_points },
            { "has_taken_action_this_turn", has_taken_action_this_turn },
            { "has_moved_this_turn", has_moved_this_turn },
            { "can_use_locked_move_points_this_turn", can_use_locked_move_points_this_turn },
            { "known_active_skill_ids", ToStringNameArray(known_active_skill_ids) },
            { "known_skill_level_map", ToIntDictionary(known_skill_level_map) },
            { "cooldowns", ToIntDictionary(cooldowns) },
            { "ai_blackboard", ai_blackboard?.ToPayload() ?? new Godot.Collections.Dictionary() },
            { "status_ids", ToStringNameArray(status_ids) },
        };
    }

    private static List<Vector2I> CopyVector2IArray(
        Godot.Collections.Array<Vector2I> source
    )
    {
        var result = new List<Vector2I>();
        foreach (Vector2I value in source ?? new Godot.Collections.Array<Vector2I>())
        {
            result.Add(value);
        }
        return result;
    }

    private static List<StringName> CopyStringNameArray(
        Godot.Collections.Array<StringName> source
    )
    {
        var result = new List<StringName>();
        foreach (StringName value in source ?? new Godot.Collections.Array<StringName>())
        {
            var normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "")
                result.Add(normalized);
        }
        return result;
    }

    private static List<StringName> CopyStringNameList(
        IEnumerable<StringName> source
    )
    {
        var result = new List<StringName>();
        foreach (StringName value in source ?? System.Array.Empty<StringName>())
        {
            var statusId = ProgressionDataUtils.to_string_name(value);

            if (statusId != "")
                result.Add(statusId);
        }
        return result;
    }

    private static Godot.Collections.Array<Vector2I> ToVector2IArray(IEnumerable<Vector2I> values)
    {
        var result = new Godot.Collections.Array<Vector2I>();
        foreach (Vector2I value in values ?? System.Array.Empty<Vector2I>())
        {
            result.Add(value);
        }
        return result;
    }

    private static Godot.Collections.Array<StringName> ToStringNameArray(IEnumerable<StringName> values)
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (StringName value in values ?? System.Array.Empty<StringName>())
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "")
                result.Add(normalized);
        }
        return result;
    }

    private static Godot.Collections.Dictionary ToIntDictionary(
        IReadOnlyDictionary<StringName, int> values
    )
    {
        var result = new Godot.Collections.Dictionary();
        if (values == null)
            return result;

        foreach (KeyValuePair<StringName, int> entry in values)
        {
            if (entry.Key != "")
                result[entry.Key] = entry.Value;
        }
        return result;
    }
}
