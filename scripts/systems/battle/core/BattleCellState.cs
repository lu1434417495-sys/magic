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
        current_height = Mathf.Clamp(
            base_height + height_offset,
            _MIN_RUNTIME_HEIGHT,
            _MAX_RUNTIME_HEIGHT
        );
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
            ["timed_terrain_effects"] = BattleTerrainEffectState.to_dict_array(
                timed_terrain_effects
            ),
            ["flow_direction"] = flow_direction,
            ["edge_feature_east"] = EdgeFeatureToDict(edge_feature_east),
            ["edge_feature_south"] = EdgeFeatureToDict(edge_feature_south),
        };
    }

    public static BattleCellState from_dict(GDictionary payload)
    {
        if (payload == null)
            return null;
        if (!HasExactRequiredKeys(payload))
        {
            return null;
        }
        if (!TryGetExactValue(payload, "coord", out object coordValue)
            || !TryAsVector2I(coordValue, out Vector2I coord))
        {
            return null;
        }
        if (!TryGetExactValue(payload, "flow_direction", out object flowDirectionValue)
            || !TryAsVector2I(flowDirectionValue, out Vector2I flowDirection))
        {
            return null;
        }
        if (!TryGetStrictInt(payload, "stack_layer", out int stackLayer))
        {
            return null;
        }
        if (!TryGetStrictInt(payload, "base_height", out int baseHeight))
        {
            return null;
        }
        if (!TryGetStrictInt(payload, "height_offset", out int heightOffset))
        {
            return null;
        }
        if (!TryGetStrictInt(payload, "current_height", out int currentHeight))
        {
            return null;
        }
        if (!TryGetStrictInt(payload, "move_cost", out int moveCost))
        {
            return null;
        }
        if (moveCost <= 0)
        {
            return null;
        }
        if (!TryGetExactValue(payload, "base_terrain", out object baseTerrainValue)
            || !TryAsStringLike(baseTerrainValue, out string baseTerrainText)
            || string.IsNullOrEmpty(baseTerrainText))
        {
            return null;
        }
        StringName normalizedTerrain = BattleTerrainRules.normalize_terrain_id(
            new StringName(baseTerrainText)
        );
        if (string.IsNullOrEmpty(normalizedTerrain.ToString()))
        {
            return null;
        }
        if (!TryGetExactValue(payload, "occupant_unit_id", out object occupantUnitIdValue)
            || !TryAsStringLike(occupantUnitIdValue, out string occupantUnitId))
        {
            return null;
        }
        if (!TryGetExactValue(payload, "passable", out object passableValue)
            || !TryAsBool(passableValue, out bool isPassable))
        {
            return null;
        }

        Godot.Collections.Array<StringName> parsedPropIds = StringsToStringNameArray(
            GetExactValueOrNull(payload, "prop_ids")
        );
        if (parsedPropIds == null)
        {
            return null;
        }
        Godot.Collections.Array<StringName> parsedTerrainEffectIds = StringsToStringNameArray(
            GetExactValueOrNull(payload, "terrain_effect_ids")
        );
        if (parsedTerrainEffectIds == null)
        {
            return null;
        }
        Godot.Collections.Array<BattleTerrainEffectState> parsedTimedTerrainEffects =
            TerrainEffectsFromPayload(GetExactValueOrNull(payload, "timed_terrain_effects"));
        if (parsedTimedTerrainEffects == null)
        {
            return null;
        }
        if (!TryGetExactValue(payload, "edge_feature_east", out object eastFeatureValue)
            || !TryAsDictionary(eastFeatureValue, out GDictionary eastFeaturePayload))
        {
            return null;
        }
        if (!TryGetExactValue(payload, "edge_feature_south", out object southFeatureValue)
            || !TryAsDictionary(southFeatureValue, out GDictionary southFeaturePayload))
        {
            return null;
        }
        BattleEdgeFeatureState eastFeature = BattleEdgeFeatureState.from_dict(
            eastFeaturePayload
        );
        if (eastFeature == null)
        {
            return null;
        }
        BattleEdgeFeatureState southFeature = BattleEdgeFeatureState.from_dict(
            southFeaturePayload
        );
        if (southFeature == null)
        {
            return null;
        }

        var cellState = new BattleCellState
        {
            coord = coord,
            stack_layer = stackLayer,
            base_terrain = normalizedTerrain,
            base_height = baseHeight,
            height_offset = heightOffset,
            current_height = currentHeight,
            passable = isPassable,
            move_cost = moveCost,
            occupant_unit_id = new StringName(occupantUnitId),
            prop_ids = parsedPropIds,
            terrain_effect_ids = parsedTerrainEffectIds,
            timed_terrain_effects = parsedTimedTerrainEffects,
            flow_direction = flowDirection,
            edge_feature_east = NormalizeEdgeFeature(eastFeature),
            edge_feature_south = NormalizeEdgeFeature(southFeature),
        };
        cellState.recalculate_runtime_values();
        return cellState;
    }

    public static GDictionary build_columns_from_surface_cells(GDictionary surface_cells)
    {
        GDictionary columns = new();
        if (surface_cells == null)
        {
            return columns;
        }
        // 直接遍历键值对，省掉旧实现里对每个坐标的二次字典查表（Keys + 索引）。
        foreach (var entry in surface_cells)
        {
            if (!TryAsVector2I(entry.Key, out Vector2I coord))
            {
                continue;
            }
            if (!TryAsCellState(entry.Value, out BattleCellState surfaceCell))
            {
                continue;
            }
            columns[coord] = build_stacked_cells_from_surface_cell(surfaceCell);
        }
        return columns;
    }

    public static GDictionary clone_columns(GDictionary columns)
    {
        GDictionary cloned = new();
        foreach (object coordValue in columns?.Keys ?? new GArray())
        {
            if (!TryAsVector2I(coordValue, out Vector2I coord))
            {
                continue;
            }
            var clonedColumn = new Godot.Collections.Array<BattleCellState>();
            if (TryGetExactValue(columns, coordValue, out object columnValue)
                && TryRawArray(columnValue, out GArray rawColumn))
            {
                foreach (object layerValue in rawColumn)
                {
                    if (!TryAsCellState(layerValue, out BattleCellState layerCell))
                    {
                        continue;
                    }
                    clonedColumn.Add(layerCell.duplicate_cell());
                }
            }
            cloned[coord] = clonedColumn;
        }
        return cloned;
    }

    public static Godot.Collections.Array<BattleCellState> build_stacked_cells_from_surface_cell(
        BattleCellState surface_cell
    )
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

    private static Godot.Collections.Array<string> StringNameArrayToStrings(
        Godot.Collections.Array<StringName> values
    )
    {
        var results = new Godot.Collections.Array<string>();
        foreach (StringName value in values ?? new Godot.Collections.Array<StringName>())
        {
            results.Add(value.ToString());
        }
        return results;
    }

    private static Godot.Collections.Array<StringName> StringsToStringNameArray(object values)
    {
        var results = new Godot.Collections.Array<StringName>();
        if (!TryRawArray(values, out GArray rawValues))
        {
            return null;
        }
        foreach (object value in rawValues)
        {
            if (!TryAsStringLike(value, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            results.Add(new StringName(text));
        }
        return results;
    }

    private static Godot.Collections.Array<BattleTerrainEffectState> TerrainEffectsFromPayload(
        object values
    )
    {
        if (!TryRawArray(values, out GArray rawValues))
        {
            return null;
        }
        var effectPayloads = new Godot.Collections.Array<GDictionary>();
        foreach (object value in rawValues)
        {
            if (!TryAsDictionary(value, out GDictionary effectPayload))
            {
                return null;
            }
            effectPayloads.Add(effectPayload);
        }
        Godot.Collections.Array<BattleTerrainEffectState> effectStates =
            BattleTerrainEffectState.from_dict_array(effectPayloads);
        if (effectStates == null)
        {
            return null;
        }
        if (effectStates.Count != rawValues.Count)
        {
            return null;
        }
        foreach (BattleTerrainEffectState effectState in effectStates)
        {
            if (effectState == null)
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

    private static bool TryGetStrictInt(GDictionary data, string key, out int value)
    {
        if (TryGetExactValue(data, key, out object rawValue)
            && TryAsStrictInt(rawValue, out value))
        {
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryAsVector2I(object rawValue, out Vector2I value)
    {
        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.AsVector2I();
            return true;
        }
        catch
        {
        }
        if (rawValue is Vector2I coord)
        {
            value = coord;
            return true;
        }
        value = Vector2I.Zero;
        return false;
    }

    private static bool TryAsStrictInt(object rawValue, out int value)
    {
        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.AsInt32();
            return true;
        }
        catch
        {
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryAsBool(object rawValue, out bool value)
    {
        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.AsBool();
            return true;
        }
        catch
        {
        }
        if (rawValue is bool boolValue)
        {
            value = boolValue;
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryAsDictionary(object rawValue, out GDictionary value)
    {
        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.AsGodotDictionary();
            return true;
        }
        catch
        {
        }
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryAsCellState(object rawValue, out BattleCellState value)
    {
        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.As<BattleCellState>();
            return value != null;
        }
        catch
        {
        }
        if (rawValue is BattleCellState typedValue)
        {
            value = typedValue;
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryAsStringLike(object rawValue, out string value)
    {
        value = rawValue?.ToString() ?? "";
        return !string.IsNullOrEmpty(value);
    }

    private static BattleEdgeFeatureState NormalizeEdgeFeature(BattleEdgeFeatureState featureState)
    {
        if (featureState == null)
        {
            return BattleEdgeFeatureState.make_none();
        }
        return featureState.duplicate_feature();
    }

    private static Godot.Collections.Array<StringName> DuplicateStringNameArray(
        Godot.Collections.Array<StringName> values
    )
    {
        var duplicated = new Godot.Collections.Array<StringName>();
        foreach (StringName value in values ?? new Godot.Collections.Array<StringName>())
        {
            duplicated.Add(new StringName(value.ToString()));
        }
        return duplicated;
    }

    private static object GetExactValueOrNull(GDictionary data, string key)
    {
        return TryGetExactValue(data, key, out object value) ? value : null;
    }

    private static bool TryGetExactValue(GDictionary data, string key, out object value)
    {
        if (data != null && data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryGetExactValue(GDictionary data, object key, out object value)
    {
        if (data == null || key == null)
        {
            value = null;
            return false;
        }
        try
        {
            dynamic dynamicKey = key;
            if (data.ContainsKey(dynamicKey))
            {
                value = data[dynamicKey];
                return true;
            }
        }
        catch
        {
        }
        if (key is Vector2I coordKey && data.ContainsKey(coordKey))
        {
            value = data[coordKey];
            return true;
        }
        else if (key is string stringKey && data.ContainsKey(stringKey))
        {
            value = data[stringKey];
            return true;
        }
        else if (key is StringName stringNameKey && data.ContainsKey(stringNameKey))
        {
            value = data[stringNameKey];
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryRawArray(object rawValue, out GArray values)
    {
        try
        {
            dynamic dynamicValue = rawValue;
            values = dynamicValue.AsGodotArray();
            return true;
        }
        catch
        {
        }
        if (rawValue is GArray array)
        {
            values = array;
            return true;
        }
        values = new GArray();
        return false;
    }

}
