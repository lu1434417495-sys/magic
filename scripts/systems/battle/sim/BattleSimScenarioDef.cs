using System.Collections.Generic;
using Godot;

public partial class BattleSimScenarioDef : Resource
{
    [Export]
    public StringName scenario_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string description { get; set; } = "";

    [Export]
    public Vector2I map_size { get; set; } = new(7, 5);

    [Export]
    public StringName terrain_profile_id { get; set; } = "default";

    [Export]
    public bool use_formal_terrain_generation { get; set; }

    [Export]
    public Vector2I world_coord { get; set; } = Vector2I.Zero;

    [Export]
    public Godot.Collections.Array ally_units { get; set; } = new();

    [Export]
    public Godot.Collections.Array enemy_units { get; set; } = new();

    [Export]
    public Godot.Collections.Array<Godot.Collections.Dictionary> cell_overrides { get; set; } =
        new();

    [Export]
    public int timeline_ticks_per_step { get; set; } = 1;

    [Export]
    public int tu_per_tick { get; set; } = 5;

    [Export]
    public int max_iterations { get; set; } = 200;

    [Export]
    public StringName manual_policy { get; set; } = "wait";

    [Export]
    public bool trace_enabled { get; set; } = true;

    [Export]
    public int[] seeds { get; set; } = { 101 };

    internal BattleSimScenarioDefinition ToDefinition()
    {
        List<BattleSimScenarioUnitEntry> allyEntries = BuildUnitEntries(
            ally_units,
            "ally_units",
            "player",
            "manual"
        );
        List<BattleSimScenarioUnitEntry> enemyEntries = BuildUnitEntries(
            enemy_units,
            "enemy_units",
            "hostile",
            "ai"
        );
        var resolvedSeeds = new List<int>();
        if (seeds != null)
        {
            foreach (int seed in seeds)
                resolvedSeeds.Add(seed);
        }
        if (resolvedSeeds.Count == 0)
            resolvedSeeds.Add(101);

        IReadOnlyDictionary<Vector2I, IReadOnlyDictionary<string, object>> cells =
            use_formal_terrain_generation
                ? new Dictionary<Vector2I, IReadOnlyDictionary<string, object>>()
                : BuildCellsPlain();

        return new BattleSimScenarioDefinition(
            scenario_id,
            display_name,
            description,
            map_size,
            terrain_profile_id,
            use_formal_terrain_generation,
            world_coord,
            allyEntries,
            enemyEntries,
            ally_units?.Count ?? 0,
            enemy_units?.Count ?? 0,
            cells,
            timeline_ticks_per_step,
            tu_per_tick,
            max_iterations,
            manual_policy,
            trace_enabled,
            resolvedSeeds
        );
    }

    private static List<BattleSimScenarioUnitEntry> BuildUnitEntries(
        Godot.Collections.Array unitSpecs,
        string sourceLabel,
        StringName defaultFaction,
        StringName defaultControlMode
    )
    {
        var entries = new List<BattleSimScenarioUnitEntry>();
        if (unitSpecs == null)
            return entries;
        for (int index = 0; index < unitSpecs.Count; index++)
        {
            Variant value = unitSpecs[index];
            BattleSimScenarioUnitEntry entry = BattleSimScenarioUnitEntry.FromVariant(
                value,
                $"{sourceLabel}[{index}]",
                defaultFaction,
                defaultControlMode
            );
            if (entry != null)
            {
                entries.Add(entry);
            }
        }
        return entries;
    }

    private Dictionary<Vector2I, IReadOnlyDictionary<string, object>> BuildCellsPlain()
    {
        var cells = new Dictionary<Vector2I, BattleCellState>();

        for (int y = 0; y < map_size.Y; y++)
        for (int x = 0; x < map_size.X; x++)
        {
            var cs = new BattleCellState();
            cs.SetCoord(new Vector2I(x, y));
            cs.SetTerrain("land");
            cs.SetBaseHeight(4);
            cs.SetHeightOffset(0);
            cells[cs.coord] = cs;
        }

        foreach (var oe in cell_overrides)
        {
            var coord = _resolve_override_coord(oe);
            if (coord == new Vector2I(-1, -1))
                continue;
            BattleCellState cs = null;
            cells.TryGetValue(coord, out cs);
            cs ??= new BattleCellState();
            cs.SetCoord(coord);
            _apply_cell_override(cs, oe);
            cs.RecalculateRuntimeValues();
            cells[coord] = cs;
        }

        var snapshots =
            new Dictionary<Vector2I, IReadOnlyDictionary<string, object>>();
        foreach (KeyValuePair<Vector2I, BattleCellState> entry in cells)
            snapshots[entry.Key] = ContentValueNormalizer.NormalizeDictionary(
                RuntimePlainPayload.CloneDictionary(entry.Value.BuildSnapshotPlain()),
                $"BattleSimScenarioDef[{scenario_id}].cells[{entry.Key}]"
            );
        return snapshots;
    }

    private static Vector2I _resolve_override_coord(Godot.Collections.Dictionary oe)
    {
        var cv = oe.ContainsKey("coord") ? oe["coord"] : Variant.From(new Vector2I(-1, -1));
        if (cv.VariantType == Variant.Type.Vector2I)
            return cv.AsVector2I();
        if (cv.VariantType == Variant.Type.Dictionary)
        {
            var d = cv.AsGodotDictionary();
            return new Vector2I(
                d.ContainsKey("x") ? d["x"].AsInt32() : -1,
                d.ContainsKey("y") ? d["y"].AsInt32() : -1
            );
        }
        return new Vector2I(-1, -1);
    }

    private static void _apply_cell_override(BattleCellState cs, Godot.Collections.Dictionary oe)
    {
        if (oe.ContainsKey("base_terrain"))
            cs.SetTerrain(ProgressionDataUtils.to_string_name(oe["base_terrain"]));

        if (oe.ContainsKey("base_height"))
            cs.SetBaseHeight(oe["base_height"].AsInt32());

        if (oe.ContainsKey("height_offset"))
            cs.SetHeightOffset(oe["height_offset"].AsInt32());

        if (
            oe.ContainsKey("flow_direction")
            && oe["flow_direction"].VariantType == Variant.Type.Vector2I
        )
            cs.flow_direction = oe["flow_direction"].AsVector2I();

        if (
            oe.ContainsKey("terrain_effect_ids")
            && oe["terrain_effect_ids"].VariantType == Variant.Type.Array
        )
        {
            cs.terrain_effect_ids.Clear();
            foreach (var ei in oe["terrain_effect_ids"].AsGodotArray())
                cs.terrain_effect_ids.Add(ProgressionDataUtils.to_string_name(ei));
        }

        if (oe.ContainsKey("prop_ids") && oe["prop_ids"].VariantType == Variant.Type.Array)
        {
            cs.prop_ids.Clear();
            foreach (var pi in oe["prop_ids"].AsGodotArray())
                cs.prop_ids.Add(ProgressionDataUtils.to_string_name(pi));
        }
    }
}
