using Godot;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// 战斗格子状态数据。
// 翻译自 battle_cell_state.gd（2026-05-24，数据层 C# 迁移）。
public partial class BattleCellState : RefCounted
{
    private const int MinRuntimeHeight = -5;
    private const int MaxRuntimeHeight = 8;

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

    public Vector2I coord = Vector2I.Zero;
    public int stack_layer;
    public StringName base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land);
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
    public BattleEdgeFeatureState edge_feature_east = BattleEdgeFeatureState.MakeNone();
    public BattleEdgeFeatureState edge_feature_south = BattleEdgeFeatureState.MakeNone();

    public void SetCoord(Vector2I value)
    {
        coord = value;
    }

    public void SetOccupant(StringName unitId)
    {
        occupant_unit_id = ProgressionDataUtils.to_string_name(unitId);
    }

    public void ClearOccupant()
    {
        occupant_unit_id = "";
    }

    public void SetPassable(bool value)
    {
        passable = value;
    }

    public void SetMoveCost(int value)
    {
        move_cost = Mathf.Max(value, 1);
    }

    public void RecalculateRuntimeValues()
    {
        base_terrain = BattleTerrainRules.NormalizeTerrainId(base_terrain);
        if (BattleTerrainRules.ToTerrainKind(base_terrain) != BattleTerrainKind.FlowingWater)
        {
            flow_direction = Vector2I.Zero;
        }
        current_height = Mathf.Clamp(
            base_height + height_offset,
            MinRuntimeHeight,
            MaxRuntimeHeight
        );
        stack_layer = current_height;
        passable = BattleTerrainRules.GetGlobalPassable(base_terrain);
        move_cost = BattleTerrainRules.GetBaseMoveCost(base_terrain);
    }

    public void SetBaseTerrain(StringName terrain)
    {
        base_terrain = terrain;
        RecalculateRuntimeValues();
    }

    public void SetHeightOffset(int offset)
    {
        height_offset = offset;
        RecalculateRuntimeValues();
    }

    public BattleEdgeFeatureState GetEdgeFeature(Vector2I direction)
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

    public void SetEdgeFeature(Vector2I direction, BattleEdgeFeatureState feature_state)
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

    public void ClearEdgeFeature(Vector2I direction)
    {
        SetEdgeFeature(direction, BattleEdgeFeatureState.MakeNone());
    }

    public BattleCellState DuplicateCell()
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
            timed_terrain_effects = BattleTerrainEffectState.DuplicateArray(timed_terrain_effects),
            flow_direction = flow_direction,
            edge_feature_east = NormalizeEdgeFeature(edge_feature_east),
            edge_feature_south = NormalizeEdgeFeature(edge_feature_south),
        };
    }

    internal GDictionary ToDictionary()
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
            ["timed_terrain_effects"] = BattleTerrainEffectState.ToDictionaryArray(
                timed_terrain_effects
            ),
            ["flow_direction"] = flow_direction,
            ["edge_feature_east"] = EdgeFeatureToDict(edge_feature_east),
            ["edge_feature_south"] = EdgeFeatureToDict(edge_feature_south),
        };
    }

    internal static BattleCellState FromDictionary(GDictionary payload)
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
        StringName normalizedTerrain = BattleTerrainRules.NormalizeTerrainId(
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
        BattleEdgeFeatureState eastFeature = BattleEdgeFeatureState.FromDictionary(
            eastFeaturePayload
        );
        if (eastFeature == null)
        {
            return null;
        }
        BattleEdgeFeatureState southFeature = BattleEdgeFeatureState.FromDictionary(
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
        cellState.RecalculateRuntimeValues();
        return cellState;
    }

    internal static GDictionary BuildColumnsFromSurfaceCells(GDictionary surface_cells)
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
            columns[coord] = BuildStackedCellsFromSurfaceCell(surfaceCell);
        }
        return columns;
    }

    internal static GDictionary BuildColumnsFromSurfaceCells(
        IReadOnlyDictionary<Vector2I, BattleCellState> surfaceCells
    )
    {
        GDictionary columns = new();
        if (surfaceCells == null)
        {
            return columns;
        }
        foreach ((Vector2I coord, BattleCellState surfaceCell) in surfaceCells)
        {
            if (surfaceCell == null)
            {
                continue;
            }
            columns[coord] = BuildStackedCellsFromSurfaceCell(surfaceCell);
        }
        return columns;
    }

    internal static GDictionary CloneColumns(GDictionary columns)
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
                    clonedColumn.Add(layerCell.DuplicateCell());
                }
            }
            cloned[coord] = clonedColumn;
        }
        return cloned;
    }

    internal static Godot.Collections.Array<BattleCellState> BuildStackedCellsFromSurfaceCell(
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
        BattleCellState topCell = surface_cell.DuplicateCell();
        topCell.SetCoord(surface_cell.coord);
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
            BattleTerrainEffectState.FromDictionaryArray(effectPayloads);
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
        return normalizedFeature.ToDictionary();
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
        if (rawValue is Variant variantValue)
        {
            if (variantValue.VariantType == Variant.Type.Vector2I)
            {
                value = variantValue.AsVector2I();
                return true;
            }
            value = Vector2I.Zero;
            return false;
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
        if (rawValue is Variant variantValue)
        {
            if (variantValue.VariantType == Variant.Type.Int)
            {
                value = variantValue.AsInt32();
                return true;
            }
            value = 0;
            return false;
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
        if (rawValue is Variant variantValue)
        {
            if (variantValue.VariantType == Variant.Type.Bool)
            {
                value = variantValue.AsBool();
                return true;
            }
            value = false;
            return false;
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
        if (rawValue is Variant variantValue)
        {
            if (variantValue.VariantType == Variant.Type.Dictionary)
            {
                value = variantValue.AsGodotDictionary();
                return true;
            }
            value = new GDictionary();
            return false;
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
        if (rawValue is Variant variantValue)
        {
            if (variantValue.VariantType == Variant.Type.String
                || variantValue.VariantType == Variant.Type.StringName)
            {
                value = variantValue.ToString();
                return !string.IsNullOrEmpty(value);
            }
            value = "";
            return false;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return !string.IsNullOrEmpty(value);
        }
        if (rawValue is StringName stringNameValue)
        {
            value = stringNameValue.ToString();
            return !string.IsNullOrEmpty(value);
        }
        value = "";
        return false;
    }

    private static BattleEdgeFeatureState NormalizeEdgeFeature(BattleEdgeFeatureState featureState)
    {
        if (featureState == null)
        {
            return BattleEdgeFeatureState.MakeNone();
        }
        return featureState.DuplicateFeature();
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
        if (rawValue is Variant variantValue)
        {
            if (variantValue.VariantType == Variant.Type.Array)
            {
                values = variantValue.AsGodotArray();
                return true;
            }
            values = new GArray();
            return false;
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
