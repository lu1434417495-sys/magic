using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// 战斗格子状态数据。
// 翻译自 battle_cell_state.gd（2026-05-24，数据层 C# 迁移）。
[GlobalClass]
public partial class BattleCellState : RefCounted
{
    private static readonly StringName _TERRAIN_LAND = "land";
    private static readonly StringName _TERRAIN_FOREST = "forest";
    private static readonly StringName _TERRAIN_WATER = "water";
    private static readonly StringName _TERRAIN_SHALLOW_WATER = "shallow_water";
    private static readonly StringName _TERRAIN_FLOWING_WATER = "flowing_water";
    private static readonly StringName _TERRAIN_DEEP_WATER = "deep_water";
    private static readonly StringName _TERRAIN_MUD = "mud";
    private static readonly StringName _TERRAIN_SPIKE = "spike";
    private const int _MIN_RUNTIME_HEIGHT = -5;
    private const int _MAX_RUNTIME_HEIGHT = 8;

    private static readonly string[] RequiredDictKeys =
    {
        "coord",
        "stack_layer",
        "base_terrain",
        "base_height",
        "height_offset",
        "current_height",
        "passable",
        "move_cost",
        "occupant_unit_id",
        "prop_ids",
        "terrain_effect_ids",
        "timed_terrain_effects",
        "flow_direction",
        "edge_feature_east",
        "edge_feature_south",
    };

    public static StringName TERRAIN_LAND() => _TERRAIN_LAND;
    public static StringName TERRAIN_FOREST() => _TERRAIN_FOREST;
    public static StringName TERRAIN_WATER() => _TERRAIN_WATER;
    public static StringName TERRAIN_SHALLOW_WATER() => _TERRAIN_SHALLOW_WATER;
    public static StringName TERRAIN_FLOWING_WATER() => _TERRAIN_FLOWING_WATER;
    public static StringName TERRAIN_DEEP_WATER() => _TERRAIN_DEEP_WATER;
    public static StringName TERRAIN_MUD() => _TERRAIN_MUD;
    public static StringName TERRAIN_SPIKE() => _TERRAIN_SPIKE;
    public static int MIN_RUNTIME_HEIGHT() => _MIN_RUNTIME_HEIGHT;
    public static int MAX_RUNTIME_HEIGHT() => _MAX_RUNTIME_HEIGHT;

    public Vector2I coord = Vector2I.Zero;
    public int stack_layer;
    public StringName base_terrain = _TERRAIN_LAND;
    public int base_height;
    public int height_offset;
    public int current_height;
    public bool passable = true;
    public int move_cost = 1;
    public StringName occupant_unit_id = "";
    public Godot.Collections.Array<StringName> prop_ids = new();
    public Godot.Collections.Array<StringName> terrain_effect_ids = new();
    public Godot.Collections.Array<BattleTerrainEffectState> timed_terrain_effects = new();
    public Vector2I flow_direction = Vector2I.Zero;
    public BattleEdgeFeatureState edge_feature_east = BattleEdgeFeatureState.make_none();
    public BattleEdgeFeatureState edge_feature_south = BattleEdgeFeatureState.make_none();

    public void clear_occupant()
    {
        occupant_unit_id = "";
    }

    public void recalculate_runtime_values()
    {
        base_terrain = BattleTerrainRules.normalize_terrain_id(base_terrain);
        if (base_terrain != _TERRAIN_FLOWING_WATER)
        {
            flow_direction = Vector2I.Zero;
        }
        current_height = Mathf.Clamp(base_height + height_offset, _MIN_RUNTIME_HEIGHT, _MAX_RUNTIME_HEIGHT);
        stack_layer = current_height;
        passable = BattleTerrainRules.get_global_passable(base_terrain);
        move_cost = BattleTerrainRules.get_base_move_cost(base_terrain);
    }

    public void set_base_terrain(StringName terrain)
    {
        base_terrain = terrain;
        recalculate_runtime_values();
    }

    public void set_height_offset(int offset)
    {
        height_offset = offset;
        recalculate_runtime_values();
    }

    public BattleEdgeFeatureState get_edge_feature(Vector2I direction)
    {
        if (direction == Vector2I.Right)
        {
            return edge_feature_east;
        }
        if (direction == Vector2I.Down)
        {
            return edge_feature_south;
        }
        return null;
    }

    public void set_edge_feature(Vector2I direction, BattleEdgeFeatureState feature_state)
    {
        BattleEdgeFeatureState normalizedFeature = NormalizeEdgeFeature(feature_state);
        if (direction == Vector2I.Right)
        {
            edge_feature_east = normalizedFeature;
        }
        else if (direction == Vector2I.Down)
        {
            edge_feature_south = normalizedFeature;
        }
    }

    public void clear_edge_feature(Vector2I direction)
    {
        set_edge_feature(direction, BattleEdgeFeatureState.make_none());
    }

    public BattleCellState duplicate_cell()
    {
        return new BattleCellState
        {
            coord = coord,
            stack_layer = stack_layer,
            base_terrain = base_terrain,
            base_height = base_height,
            height_offset = height_offset,
            current_height = current_height,
            passable = passable,
            move_cost = move_cost,
            occupant_unit_id = occupant_unit_id,
            prop_ids = DuplicateStringNameArray(prop_ids),
            terrain_effect_ids = DuplicateStringNameArray(terrain_effect_ids),
            timed_terrain_effects = BattleTerrainEffectState.duplicate_array(timed_terrain_effects),
            flow_direction = flow_direction,
            edge_feature_east = NormalizeEdgeFeature(edge_feature_east),
            edge_feature_south = NormalizeEdgeFeature(edge_feature_south),
        };
    }

    public GDictionary to_dict()
    {
        return new GDictionary
        {
            ["coord"] = coord,
            ["stack_layer"] = stack_layer,
            ["base_terrain"] = base_terrain.ToString(),
            ["base_height"] = base_height,
            ["height_offset"] = height_offset,
            ["current_height"] = current_height,
            ["passable"] = passable,
            ["move_cost"] = move_cost,
            ["occupant_unit_id"] = occupant_unit_id.ToString(),
            ["prop_ids"] = StringNameArrayToStrings(prop_ids),
            ["terrain_effect_ids"] = StringNameArrayToStrings(terrain_effect_ids),
            ["timed_terrain_effects"] = BattleTerrainEffectState.to_dict_array(timed_terrain_effects),
            ["flow_direction"] = flow_direction,
            ["edge_feature_east"] = EdgeFeatureToDict(edge_feature_east),
            ["edge_feature_south"] = EdgeFeatureToDict(edge_feature_south),
        };
    }

    public static BattleCellState from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        GDictionary payload = data.AsGodotDictionary();
        if (!HasExactRequiredKeys(payload))
        {
            return null;
        }
        if (Get(payload, "coord").VariantType != Variant.Type.Vector2I)
        {
            return null;
        }
        if (Get(payload, "flow_direction").VariantType != Variant.Type.Vector2I)
        {
            return null;
        }
        if (!IsIntValue(Get(payload, "stack_layer")))
        {
            return null;
        }
        if (!IsIntValue(Get(payload, "base_height")))
        {
            return null;
        }
        if (!IsIntValue(Get(payload, "height_offset")))
        {
            return null;
        }
        if (!IsIntValue(Get(payload, "current_height")))
        {
            return null;
        }
        if (!IsIntValue(Get(payload, "move_cost")))
        {
            return null;
        }
        if (Get(payload, "move_cost").AsInt32() <= 0)
        {
            return null;
        }
        if (!IsNonEmptyStringLike(Get(payload, "base_terrain")))
        {
            return null;
        }
        StringName normalizedTerrain = BattleTerrainRules.normalize_terrain_id(ToStringName(Get(payload, "base_terrain")));
        if (string.IsNullOrEmpty(normalizedTerrain.ToString()))
        {
            return null;
        }
        if (!IsStringLike(Get(payload, "occupant_unit_id")))
        {
            return null;
        }
        if (Get(payload, "passable").VariantType != Variant.Type.Bool)
        {
            return null;
        }

        Godot.Collections.Array<StringName> parsedPropIds = StringsToStringNameArray(Get(payload, "prop_ids"));
        if (parsedPropIds == null)
        {
            return null;
        }
        Godot.Collections.Array<StringName> parsedTerrainEffectIds = StringsToStringNameArray(Get(payload, "terrain_effect_ids"));
        if (parsedTerrainEffectIds == null)
        {
            return null;
        }
        Godot.Collections.Array<BattleTerrainEffectState> parsedTimedTerrainEffects = TerrainEffectsFromPayload(Get(payload, "timed_terrain_effects"));
        if (parsedTimedTerrainEffects == null)
        {
            return null;
        }
        if (Get(payload, "edge_feature_east").VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        if (Get(payload, "edge_feature_south").VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        BattleEdgeFeatureState eastFeature = BattleEdgeFeatureState.from_dict(Get(payload, "edge_feature_east"));
        if (eastFeature == null)
        {
            return null;
        }
        BattleEdgeFeatureState southFeature = BattleEdgeFeatureState.from_dict(Get(payload, "edge_feature_south"));
        if (southFeature == null)
        {
            return null;
        }

        var cellState = new BattleCellState
        {
            coord = Get(payload, "coord").AsVector2I(),
            stack_layer = Get(payload, "stack_layer").AsInt32(),
            base_terrain = normalizedTerrain,
            base_height = Get(payload, "base_height").AsInt32(),
            height_offset = Get(payload, "height_offset").AsInt32(),
            current_height = Get(payload, "current_height").AsInt32(),
            passable = Get(payload, "passable").AsBool(),
            move_cost = Get(payload, "move_cost").AsInt32(),
            occupant_unit_id = ToStringName(Get(payload, "occupant_unit_id")),
            prop_ids = parsedPropIds,
            terrain_effect_ids = parsedTerrainEffectIds,
            timed_terrain_effects = parsedTimedTerrainEffects,
            flow_direction = Get(payload, "flow_direction").AsVector2I(),
            edge_feature_east = NormalizeEdgeFeature(eastFeature),
            edge_feature_south = NormalizeEdgeFeature(southFeature),
        };
        cellState.recalculate_runtime_values();
        return cellState;
    }

    public static GDictionary build_columns_from_surface_cells(GDictionary surface_cells)
    {
        GDictionary columns = new();
        foreach (Variant coordVariant in surface_cells?.Keys ?? new GArray())
        {
            if (coordVariant.VariantType != Variant.Type.Vector2I)
            {
                continue;
            }
            Variant surfaceCellValue = surface_cells[coordVariant];
            var surfaceCell = surfaceCellValue.AsGodotObject() as BattleCellState;
            if (surfaceCell == null)
            {
                continue;
            }
            Vector2I coord = coordVariant.AsVector2I();
            columns[coord] = build_stacked_cells_from_surface_cell(surfaceCell);
        }
        return columns;
    }

    public static GDictionary clone_columns(GDictionary columns)
    {
        GDictionary cloned = new();
        foreach (Variant coordVariant in columns?.Keys ?? new GArray())
        {
            if (coordVariant.VariantType != Variant.Type.Vector2I)
            {
                continue;
            }
            Variant columnVariant = columns[coordVariant];
            var clonedColumn = new Godot.Collections.Array<BattleCellState>();
            if (columnVariant.VariantType == Variant.Type.Array)
            {
                foreach (Variant layerVariant in columnVariant.AsGodotArray())
                {
                    var layerCell = layerVariant.AsGodotObject() as BattleCellState;
                    if (layerCell == null)
                    {
                        continue;
                    }
                    clonedColumn.Add(layerCell.duplicate_cell());
                }
            }
            cloned[coordVariant.AsVector2I()] = clonedColumn;
        }
        return cloned;
    }

    public static Godot.Collections.Array<BattleCellState> build_stacked_cells_from_surface_cell(BattleCellState surface_cell)
    {
        var column = new Godot.Collections.Array<BattleCellState>();
        if (surface_cell == null)
        {
            return column;
        }
        int topLayer = surface_cell.current_height;
        if (topLayer >= 0)
        {
            for (int layer = 0; layer < topLayer; layer++)
            {
                var supportCell = new BattleCellState
                {
                    coord = surface_cell.coord,
                    stack_layer = layer,
                    base_terrain = surface_cell.base_terrain,
                    base_height = layer,
                    height_offset = 0,
                    current_height = layer,
                    passable = false,
                    move_cost = 1,
                    occupant_unit_id = "",
                    prop_ids = new Godot.Collections.Array<StringName>(),
                    terrain_effect_ids = new Godot.Collections.Array<StringName>(),
                    timed_terrain_effects = new Godot.Collections.Array<BattleTerrainEffectState>(),
                    flow_direction = Vector2I.Zero,
                };
                column.Add(supportCell);
            }
        }
        BattleCellState topCell = surface_cell.duplicate_cell();
        topCell.coord = surface_cell.coord;
        topCell.stack_layer = topLayer;
        topCell.current_height = topLayer;
        column.Add(topCell);
        return column;
    }

    private static Godot.Collections.Array<string> StringNameArrayToStrings(Godot.Collections.Array<StringName> values)
    {
        var results = new Godot.Collections.Array<string>();
        foreach (Variant value in values ?? new Godot.Collections.Array<StringName>())
        {
            results.Add(value.AsString());
        }
        return results;
    }

    private static Godot.Collections.Array<StringName> StringsToStringNameArray(Variant values)
    {
        var results = new Godot.Collections.Array<StringName>();
        if (values.VariantType != Variant.Type.Array)
        {
            return null;
        }
        foreach (Variant value in values.AsGodotArray())
        {
            if (!IsNonEmptyStringLike(value))
            {
                return null;
            }
            results.Add(ToStringName(value));
        }
        return results;
    }

    private static Godot.Collections.Array<BattleTerrainEffectState> TerrainEffectsFromPayload(Variant values)
    {
        if (values.VariantType != Variant.Type.Array)
        {
            return null;
        }
        foreach (Variant value in values.AsGodotArray())
        {
            if (value.VariantType != Variant.Type.Dictionary)
            {
                return null;
            }
        }
        Godot.Collections.Array<BattleTerrainEffectState> effectStates = BattleTerrainEffectState.from_dict_array(values);
        if (effectStates == null)
        {
            return null;
        }
        if (effectStates.Count != values.AsGodotArray().Count)
        {
            return null;
        }
        foreach (Variant effectState in effectStates)
        {
            if (effectState.AsGodotObject() is not BattleTerrainEffectState)
            {
                return null;
            }
        }
        return effectStates;
    }

    private static GDictionary EdgeFeatureToDict(BattleEdgeFeatureState featureState)
    {
        BattleEdgeFeatureState normalizedFeature = NormalizeEdgeFeature(featureState);
        return normalizedFeature.to_dict();
    }

    private static bool HasExactRequiredKeys(GDictionary data)
    {
        if (data.Count != RequiredDictKeys.Length)
        {
            return false;
        }
        foreach (string key in RequiredDictKeys)
        {
            if (!data.ContainsKey(key))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsIntValue(Variant value)
    {
        return value.VariantType == Variant.Type.Int;
    }

    private static bool IsStringLike(Variant value)
    {
        return value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName;
    }

    private static bool IsNonEmptyStringLike(Variant value)
    {
        return IsStringLike(value) && !string.IsNullOrEmpty(value.AsString());
    }

    private static BattleEdgeFeatureState NormalizeEdgeFeature(BattleEdgeFeatureState featureState)
    {
        if (featureState == null)
        {
            return BattleEdgeFeatureState.make_none();
        }
        return featureState.duplicate_feature();
    }

    private static Godot.Collections.Array<StringName> DuplicateStringNameArray(Godot.Collections.Array<StringName> values)
    {
        var duplicated = new Godot.Collections.Array<StringName>();
        foreach (Variant value in values ?? new Godot.Collections.Array<StringName>())
        {
            duplicated.Add(new StringName(value.AsString()));
        }
        return duplicated;
    }

    private static StringName ToStringName(Variant value)
    {
        return IsStringLike(value) ? new StringName(value.AsString()) : "";
    }

    private static Variant Get(GDictionary payload, string key)
    {
        return payload.ContainsKey(key) ? payload[key] : default;
    }
}
