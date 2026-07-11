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

    public IReadOnlyList<int> ResolveSeeds()
    {
        var r = new List<int>();
        foreach (int s in seeds)
            r.Add(s);
        if (r.Count == 0)
            r.Add(101);
        return r;
    }

    internal GodotProjectionLease<Godot.Collections.Dictionary> BuildStartContextLease()
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
        var contextPlain = new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["battle_party"] = BuildUnitPayloadsPlain(allyEntries),
            ["enemy_units"] = BuildUnitPayloadsPlain(enemyEntries),
            ["tu_per_tick"] = tu_per_tick,
            ["battle_terrain_profile"] = terrain_profile_id,
            ["world_coord"] = world_coord,
        };

        if (use_formal_terrain_generation)
        {
            if (map_size != Vector2I.Zero)
                contextPlain["battle_map_size"] = map_size;
            return RuntimePlainPayload.ProjectDictionaryLease(
                contextPlain,
                "battle-sim-scenario",
                LifetimeDomain.Request,
                "BattleSimScenarioDef.BuildStartContextLease"
            );
        }

        contextPlain["ally_spawns"] = BuildSpawnCoordsPlain(allyEntries);
        contextPlain["enemy_spawns"] = BuildSpawnCoordsPlain(enemyEntries);
        contextPlain["map_size"] = map_size;
        Dictionary<Vector2I, IReadOnlyDictionary<string, object>> cells = BuildCellsPlain();
        GodotProjectionLease<Godot.Collections.Dictionary> lease =
            RuntimePlainPayload.ProjectDictionaryLease(
                contextPlain,
                "battle-sim-scenario",
                LifetimeDomain.Request,
                "BattleSimScenarioDef.BuildStartContextLease"
            );
        try
        {
            var projectedCells = lease.Own(
                new Godot.Collections.Dictionary(),
                "BattleSimScenarioDef.BuildStartContextLease.cells"
            );
            foreach (
                KeyValuePair<Vector2I, IReadOnlyDictionary<string, object>> entry in cells
            )
            {
                projectedCells[entry.Key] = RuntimePlainPayload.ProjectDictionaryInto(
                    lease,
                    entry.Value,
                    $"BattleSimScenarioDef.BuildStartContextLease.cells[{entry.Key}]"
                );
            }
            lease.Value["cells"] = projectedCells;
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
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

    private static List<object> BuildUnitPayloadsPlain(
        IReadOnlyList<BattleSimScenarioUnitEntry> unitEntries
    )
    {
        var payloads = new List<object>();
        if (unitEntries == null)
            return payloads;
        foreach (BattleSimScenarioUnitEntry entry in unitEntries)
        {
            if (entry?.UnitState == null)
                continue;
            payloads.Add(entry.UnitState.BuildSnapshotPlain());
        }
        return payloads;
    }

    private static List<object> BuildSpawnCoordsPlain(
        IReadOnlyList<BattleSimScenarioUnitEntry> unitEntries
    )
    {
        var coords = new List<object>();
        if (unitEntries == null)
            return coords;
        foreach (BattleSimScenarioUnitEntry entry in unitEntries)
        {
            if (entry == null)
                continue;
            coords.Add(entry.Coord);
        }
        return coords;
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
            snapshots[entry.Key] = entry.Value.BuildSnapshotPlain();
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
