using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleSimTestUnitBuilder
{
    internal StringName unit_id = "";
    internal StringName source_member_id = "";
    internal string display_name = "";
    internal StringName faction_id = "";
    internal StringName control_mode = "manual";
    internal StringName ai_brain_id = "";
    internal StringName ai_state_id = "";
    internal Vector2I coord = Vector2I.Zero;
    internal int body_size = 2;
    internal StringName body_size_category = "medium";
    internal int current_hp = 30;
    internal int current_mp;
    internal int current_stamina;
    internal int current_aura = 0;
    internal int current_ap = 1;
    internal int current_move_points = BattleUnitState.DefaultMovePointsPerTurn;
    internal int action_threshold = AttributeService.DEFAULT_CHARACTER_ACTION_THRESHOLD;
    internal GDictionary attribute_overrides = new();
    internal GArray skill_ids = new();
    internal GDictionary skill_level_map = new();
    internal GArray movement_tags = new();
    internal GArray unlocked_combat_resource_ids = new();
    internal GDictionary weapon_projection = new();
    internal GDictionary base_attributes = new();

    internal BattleSimUnitDefinition ToDefinition(
        StringName defaultFactionId = default,
        StringName defaultControlMode = default
    ) => BattleSimContentDefinitionProjector.ProjectUnitDefinition(
        ToImport(),
        defaultFactionId,
        defaultControlMode,
        $"BattleSimTestUnitBuilder[{unit_id}]"
    );

    internal BattleSimUnitImportModel ToImport() => new(
        unit_id.ToString(),
        source_member_id.ToString(),
        display_name,
        faction_id.ToString(),
        control_mode.ToString(),
        ai_brain_id.ToString(),
        ai_state_id.ToString(),
        new BattleSimCoordImportModel(coord.X, coord.Y),
        body_size,
        body_size_category.ToString(),
        current_hp,
        current_mp,
        current_stamina,
        current_aura,
        current_ap,
        current_move_points,
        action_threshold,
        IntMap(attribute_overrides),
        Names(skill_ids),
        IntMap(skill_level_map),
        Names(movement_tags),
        Names(unlocked_combat_resource_ids),
        IntMap(base_attributes),
        Weapon(weapon_projection)
    );

    private static IReadOnlyDictionary<string, int> IntMap(GDictionary source)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Variant key in source.Keys)
            result[key.AsString()] = source[key].AsInt32();
        return result;
    }

    private static IReadOnlyList<string> Names(GArray source)
    {
        var result = new List<string>();
        foreach (Variant value in source) result.Add(value.AsString());
        return result;
    }

    private static BattleSimWeaponProjectionImportModel Weapon(GDictionary source)
    {
        if (source == null || source.Count == 0) return null;
        GDictionary one = Dict(source, "weapon_one_handed_dice");
        GDictionary two = Dict(source, "weapon_two_handed_dice");
        return new BattleSimWeaponProjectionImportModel(
            Text(source, "weapon_profile_kind"),
            Text(source, "weapon_item_id"),
            Text(source, "weapon_profile_type_id"),
            Text(source, "weapon_range_type"),
            Text(source, "weapon_family"),
            Text(source, "weapon_current_grip"),
            Int(source, "weapon_attack_range"),
            Int(one, "dice_count"),
            Int(one, "dice_sides"),
            Int(two, "dice_count"),
            Int(two, "dice_sides"),
            Bool(source, "weapon_is_versatile"),
            Bool(source, "weapon_uses_two_hands"),
            Text(source, "weapon_physical_damage_tag")
        );
    }

    private static string Text(GDictionary source, string key) => source.ContainsKey(key) ? source[key].AsString() : "";
    private static int Int(GDictionary source, string key) => source.ContainsKey(key) ? source[key].AsInt32() : 0;
    private static bool Bool(GDictionary source, string key) => source.ContainsKey(key) && source[key].AsBool();
    private static GDictionary Dict(GDictionary source, string key) =>
        source.ContainsKey(key) && source[key].VariantType == Variant.Type.Dictionary
            ? source[key].AsGodotDictionary()
            : new GDictionary();
}

internal sealed class BattleSimTestScenarioBuilder
{
    internal StringName scenario_id = "";
    internal string display_name = "";
    internal string description = "";
    internal Vector2I map_size = new(7, 5);
    internal StringName terrain_profile_id = "default";
    internal bool use_formal_terrain_generation;
    internal Vector2I world_coord = Vector2I.Zero;
    internal List<object> ally_units = new();
    internal List<object> enemy_units = new();
    internal List<GDictionary> cell_overrides = new();
    internal int timeline_ticks_per_step = 1;
    internal int tu_per_tick = 5;
    internal int max_iterations = 200;
    internal StringName manual_policy = "wait";
    internal bool trace_enabled = true;
    internal int[] seeds = { 101 };

    internal BattleSimScenarioDefinition ToDefinition()
    {
        var allies = Units(ally_units, "ally_units");
        var enemies = Units(enemy_units, "enemy_units");
        var cells = new List<BattleSimCellOverrideImportModel>();
        foreach (GDictionary value in cell_overrides ?? new List<GDictionary>()) cells.Add(Cell(value));
        return BattleSimContentDefinitionProjector.ProjectScenario(
            new BattleSimScenarioImportModel(
                scenario_id.ToString(), display_name, description,
                new BattleSimCoordImportModel(map_size.X, map_size.Y),
                terrain_profile_id.ToString(), use_formal_terrain_generation,
                new BattleSimCoordImportModel(world_coord.X, world_coord.Y),
                allies, enemies, cells, timeline_ticks_per_step, tu_per_tick,
                max_iterations, manual_policy.ToString(), trace_enabled, seeds ?? Array.Empty<int>()
            ),
            $"BattleSimTestScenarioBuilder[{scenario_id}]"
        );
    }

    private static IReadOnlyList<BattleSimUnitImportModel> Units(List<object> source, string label)
    {
        var result = new List<BattleSimUnitImportModel>();
        if (source == null) return result;
        for (int index = 0; index < source.Count; index++)
        {
            if (source[index] is BattleSimTestUnitBuilder builder) result.Add(builder.ToImport());
            else if (source[index] is BattleUnitState state)
                throw new InvalidOperationException($"{label}[{index}] must use a plain BattleSim test import builder, not runtime state.");
            else throw new InvalidOperationException($"{label}[{index}] must be a BattleSimTestUnitBuilder.");
        }
        return result;
    }

    private static BattleSimCellOverrideImportModel Cell(GDictionary value)
    {
        Vector2I coord = value.ContainsKey("coord") ? value["coord"].AsVector2I() : new Vector2I(-1, -1);
        return new BattleSimCellOverrideImportModel(
            new BattleSimCoordImportModel(coord.X, coord.Y),
            value.ContainsKey("base_terrain") ? value["base_terrain"].AsString() : null,
            value.ContainsKey("base_height") ? value["base_height"].AsInt32() : null,
            value.ContainsKey("height_offset") ? value["height_offset"].AsInt32() : null,
            value.ContainsKey("flow_direction")
                ? new BattleSimCoordImportModel(value["flow_direction"].AsVector2I().X, value["flow_direction"].AsVector2I().Y)
                : null,
            value.ContainsKey("terrain_effect_ids") ? Names(value["terrain_effect_ids"].AsGodotArray()) : null,
            value.ContainsKey("prop_ids") ? Names(value["prop_ids"].AsGodotArray()) : null
        );
    }

    private static IReadOnlyList<string> Names(GArray source)
    {
        var result = new List<string>();
        foreach (Variant value in source) result.Add(value.AsString());
        return result;
    }
}
