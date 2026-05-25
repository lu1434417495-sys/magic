using Godot;

[GlobalClass]
public partial class BattleAiUnitSnapshot : RefCounted
{
    public StringName unit_id = "";
    public string display_name = "";
    public StringName faction_id = "";
    public Vector2I coord = new(-1, -1);
    public Vector2I footprint_size = Vector2I.One;
    public Godot.Collections.Array<Vector2I> occupied_coords = new();
    public bool is_alive;
    public int current_hp;
    public int current_ap;
    public int current_mp;
    public int current_stamina;
    public int current_aura;
    public int current_move_points;
    public Godot.Collections.Array<StringName> known_active_skill_ids = new();
    public Godot.Collections.Dictionary known_skill_level_map = new();
    public Godot.Collections.Dictionary cooldowns = new();
    public Godot.Collections.Dictionary ai_blackboard = new();
    public Godot.Collections.Array<StringName> status_ids = new();

    public static BattleAiUnitSnapshot from_unit(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            BattleAiPayloadGuard.FailLoud("BattleAiUnitSnapshot.from_unit requires BattleUnitState.",
                new Godot.Collections.Dictionary { { "source", "BattleAiUnitSnapshot" } });
            return null;
        }
        var snapshot = new BattleAiUnitSnapshot();
        snapshot.unit_id = ProgressionDataUtils.to_string_name(unitState.unit_id);
        snapshot.display_name = unitState.display_name;
        snapshot.faction_id = ProgressionDataUtils.to_string_name(unitState.faction_id);
        snapshot.coord = unitState.coord;
        snapshot.footprint_size = unitState.footprint_size;
        snapshot.occupied_coords = _copy_vector2i_array(unitState.occupied_coords);
        snapshot.is_alive = unitState.is_alive;
        snapshot.current_hp = unitState.current_hp;
        snapshot.current_ap = unitState.current_ap;
        snapshot.current_mp = unitState.current_mp;
        snapshot.current_stamina = unitState.current_stamina;
        snapshot.current_aura = unitState.current_aura;
        snapshot.current_move_points = unitState.current_move_points;
        snapshot.known_active_skill_ids = _copy_string_name_array(unitState.known_active_skill_ids);
        snapshot.known_skill_level_map = unitState.known_skill_level_map.Duplicate(true);
        snapshot.cooldowns = unitState.cooldowns.Duplicate(true);
        snapshot.ai_blackboard = unitState.ai_blackboard.Duplicate(true);
        snapshot.status_ids = _copy_status_ids(unitState.status_effects);
        if (!BattleAiPayloadGuard.ValidateNoForbiddenObject(snapshot.to_payload(), "BattleAiUnitSnapshot"))
            return null;
        return snapshot;
    }

    public Godot.Collections.Dictionary to_payload()
    {
        return new Godot.Collections.Dictionary
        {
            { "unit_id", unit_id },
            { "display_name", display_name },
            { "faction_id", faction_id },
            { "coord", coord },
            { "footprint_size", footprint_size },
            { "occupied_coords", occupied_coords.Duplicate() },
            { "is_alive", is_alive },
            { "current_hp", current_hp },
            { "current_ap", current_ap },
            { "current_mp", current_mp },
            { "current_stamina", current_stamina },
            { "current_aura", current_aura },
            { "current_move_points", current_move_points },
            { "known_active_skill_ids", known_active_skill_ids.Duplicate() },
            { "known_skill_level_map", known_skill_level_map.Duplicate(true) },
            { "cooldowns", cooldowns.Duplicate(true) },
            { "ai_blackboard", ai_blackboard.Duplicate(true) },
            { "status_ids", status_ids.Duplicate() },
        };
    }

    private static Godot.Collections.Array<Vector2I> _copy_vector2i_array(Godot.Collections.Array source)
    {
        var result = new Godot.Collections.Array<Vector2I>();
        foreach (var value in source)
        {
            if (value.VariantType == Variant.Type.Vector2I)
                result.Add(value.AsVector2I());
        }
        return result;
    }


    private static Godot.Collections.Array<Vector2I> _copy_vector2i_array(Godot.Collections.Array<Vector2I> source)
    {
        var result = new Godot.Collections.Array<Vector2I>();
        foreach (Vector2I value in source ?? new Godot.Collections.Array<Vector2I>())
        {
            result.Add(value);
        }
        return result;
    }
    private static Godot.Collections.Array<StringName> _copy_string_name_array(Godot.Collections.Array source)
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (var value in source)
        {
            var normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "")
                result.Add(normalized);
        }
        return result;
    }


    private static Godot.Collections.Array<StringName> _copy_string_name_array(Godot.Collections.Array<StringName> source)
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (StringName value in source ?? new Godot.Collections.Array<StringName>())
        {
            var normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "")
                result.Add(normalized);
        }
        return result;
    }
    private static Godot.Collections.Array<StringName> _copy_status_ids(Godot.Collections.Dictionary source)
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (var key in source.Keys)
        {
            var statusId = ProgressionDataUtils.to_string_name(key);
            if (statusId != "")
                result.Add(statusId);
        }
        result.Sort();
        return result;
    }
}
