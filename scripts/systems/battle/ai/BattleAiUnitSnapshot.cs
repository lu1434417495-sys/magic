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
    public StringName summon_source_equipment_instance_id = "";
    public StringName summon_binding_id = "";
    public StringName summon_state_key = "";
    public int summon_expires_at_tu = -1;

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
            summon_source_equipment_instance_id = ProgressionDataUtils.to_string_name(
                blackboard.summon_source_equipment_instance_id
            ),
            summon_binding_id = ProgressionDataUtils.to_string_name(
                blackboard.summon_binding_id
            ),
            summon_state_key = ProgressionDataUtils.to_string_name(
                blackboard.summon_state_key
            ),
            summon_expires_at_tu = blackboard.summon_expires_at_tu,
        };
    }

    internal IReadOnlyDictionary<string, object> BuildPayloadPlain()
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
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
        if (summon_source_equipment_instance_id != "")
            result["summon_source_equipment_instance_id"] = summon_source_equipment_instance_id;
        if (summon_binding_id != "")
            result["summon_binding_id"] = summon_binding_id;
        if (summon_state_key != "")
            result["summon_state_key"] = summon_state_key;
        if (summon_expires_at_tu >= 0)
            result["summon_expires_at_tu"] = summon_expires_at_tu;
        return result;
    }

    private static void AddBool(IDictionary<string, object> result, string key, bool value)
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

        BattleUnitGeometryReadView geometry =
            unitState.GetGeometryReadViewTyped();
        snapshot.coord = geometry.AnchorCoord;

        snapshot.footprint_size = geometry.FootprintSize;

        snapshot.occupied_coords =
            CopyVector2IArray(geometry.OccupiedCoords);

        BattleUnitCombatResourceValues combatResources =
            unitState.GetCombatResourcesReadViewTyped().Values;
        snapshot.is_alive = combatResources.IsAlive;

        snapshot.current_hp = combatResources.Hp;

        snapshot.current_ap = combatResources.Ap;

        snapshot.current_mp = combatResources.Mp;

        snapshot.current_stamina = combatResources.Stamina;

        snapshot.current_aura = combatResources.Aura;

        snapshot.current_move_points = combatResources.MovePoints;

        snapshot.has_taken_action_this_turn =
            unitState.HasTakenActionThisTurnTyped();

        snapshot.has_moved_this_turn =
            unitState.HasMovedThisTurnTyped();

        snapshot.can_use_locked_move_points_this_turn =
            unitState.CanUseLockedMovePointsThisTurnTyped();

        snapshot.known_active_skill_ids = CopyStringNameArray(
            unitState.GetKnownActiveSkillsViewTyped()
        );

        snapshot.known_skill_level_map = unitState.GetKnownSkillLevelsTyped();

        snapshot.cooldowns = unitState.GetCooldownsTyped();

        snapshot.ai_blackboard = BattleAiUnitBlackboardSnapshot.FromBlackboard(unitState.ai_blackboard);

        snapshot.status_ids = CopyStringNameList(unitState.GetSortedStatusEffectIdsTyped());

        return snapshot;
    }

    internal IReadOnlyDictionary<string, object> BuildPayloadPlain()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["unit_id"] = unit_id,
            ["display_name"] = display_name,
            ["faction_id"] = faction_id,
            ["coord"] = coord,
            ["footprint_size"] = footprint_size,
            ["occupied_coords"] = new List<Vector2I>(occupied_coords),
            ["is_alive"] = is_alive,
            ["current_hp"] = current_hp,
            ["current_ap"] = current_ap,
            ["current_mp"] = current_mp,
            ["current_stamina"] = current_stamina,
            ["current_aura"] = current_aura,
            ["current_move_points"] = current_move_points,
            ["has_taken_action_this_turn"] = has_taken_action_this_turn,
            ["has_moved_this_turn"] = has_moved_this_turn,
            ["can_use_locked_move_points_this_turn"] = can_use_locked_move_points_this_turn,
            ["known_active_skill_ids"] = new List<StringName>(known_active_skill_ids),
            ["known_skill_level_map"] = new Dictionary<StringName, int>(known_skill_level_map),
            ["cooldowns"] = new Dictionary<StringName, int>(cooldowns),
            ["ai_blackboard"] = ai_blackboard?.BuildPayloadPlain()
                ?? new Dictionary<string, object>(StringComparer.Ordinal),
            ["status_ids"] = new List<StringName>(status_ids),
        };
    }

    private static List<Vector2I> CopyVector2IArray(IEnumerable<Vector2I> source)
    {
        var result = new List<Vector2I>();
        foreach (Vector2I value in source ?? System.Array.Empty<Vector2I>())
        {
            result.Add(value);
        }
        return result;
    }

    private static List<StringName> CopyStringNameArray(
        BattleKnownActiveSkillReadView source
    )
    {
        var result = new List<StringName>();
        foreach (StringName value in source)
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

}
