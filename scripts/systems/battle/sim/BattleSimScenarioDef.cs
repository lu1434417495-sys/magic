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

    public Godot.Collections.Array<int> ResolveSeeds()
    {
        var r = new Godot.Collections.Array<int>();
        foreach (int s in seeds)
            r.Add(s);
        if (r.Count == 0)
            r.Add(101);
        return r;
    }

    internal Godot.Collections.Dictionary BuildStartContext()
    {
        var ctx = new Godot.Collections.Dictionary
        {
            { "battle_party", _build_unit_payloads(ally_units, "player", "manual") },
            { "enemy_units", _build_unit_payloads(enemy_units, "hostile", "ai") },
            { "tu_per_tick", tu_per_tick },
            { "battle_terrain_profile", terrain_profile_id },
            { "world_coord", world_coord },
        };

        if (use_formal_terrain_generation)
        {
            if (map_size != Vector2I.Zero)
                ctx["battle_map_size"] = map_size;
            return ctx;
        }

        ctx["ally_spawns"] = _build_spawn_coords(ally_units);
        ctx["enemy_spawns"] = _build_spawn_coords(enemy_units);
        ctx["map_size"] = map_size;
        ctx["cells"] = _build_cells();
        return ctx;
    }

    internal Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            { "scenario_id", (string)scenario_id },
            { "display_name", display_name },
            { "description", description },
            { "map_size", map_size },
            { "terrain_profile_id", (string)terrain_profile_id },
            { "use_formal_terrain_generation", use_formal_terrain_generation },
            { "world_coord", world_coord },
            { "timeline_ticks_per_step", timeline_ticks_per_step },
            { "tu_per_tick", tu_per_tick },
            { "max_iterations", max_iterations },
            { "manual_policy", (string)manual_policy },
            { "trace_enabled", trace_enabled },
            { "seeds", ResolveSeeds() },
            { "ally_unit_count", ally_units.Count },
            { "enemy_unit_count", enemy_units.Count },
        };
    }

    private Godot.Collections.Array _build_unit_payloads(
        Godot.Collections.Array unitSpecs,
        StringName defaultFaction,
        StringName defaultControlMode
    )
    {
        var p = new Godot.Collections.Array();
        foreach (var us in unitSpecs)
        {
            if (us.VariantType == Variant.Type.Nil)
                continue;
            var unitSpec = us.AsGodotObject() as BattleSimUnitSpec;
            if (unitSpec != null)
            {
                p.Add(unitSpec.ToBattleUnitState(defaultFaction, defaultControlMode).ToDictionary());
                continue;
            }
            var unitState = us.AsGodotObject() as BattleUnitState;
            if (unitState != null)
                p.Add(unitState.ToDictionary());
        }
        return p;
    }

    private Godot.Collections.Array<Vector2I> _build_spawn_coords(Godot.Collections.Array unitSpecs)
    {
        var c = new Godot.Collections.Array<Vector2I>();
        foreach (var us in unitSpecs)
        {
            if (us.VariantType == Variant.Type.Nil)
                continue;
            c.Add(us.AsGodotObject().Get("coord").AsVector2I());
        }
        return c;
    }

    private Godot.Collections.Dictionary _build_cells()
    {
        var cells = new Godot.Collections.Dictionary();

        for (int y = 0; y < map_size.Y; y++)
        for (int x = 0; x < map_size.X; x++)
        {
            var cs = new BattleCellState
            {
                coord = new Vector2I(x, y),
                base_terrain = "land",
                base_height = 4,
                height_offset = 0,
            };
            cs.RecalculateRuntimeValues();
            cells[cs.coord] = cs;
        }

        foreach (var oe in cell_overrides)
        {
            var coord = _resolve_override_coord(oe);
            if (coord == new Vector2I(-1, -1))
                continue;
            var cs = cells.ContainsKey(coord)
                ? cells[coord].AsGodotObject() as BattleCellState
                : new BattleCellState { coord = coord };
            _apply_cell_override(cs, oe);
            cs.RecalculateRuntimeValues();
            cells[coord] = cs;
        }

        return cells;
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
            cs.base_terrain = ProgressionDataUtils.to_string_name(oe["base_terrain"]);

        if (oe.ContainsKey("base_height"))
            cs.base_height = oe["base_height"].AsInt32();

        if (oe.ContainsKey("height_offset"))
            cs.height_offset = oe["height_offset"].AsInt32();

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
