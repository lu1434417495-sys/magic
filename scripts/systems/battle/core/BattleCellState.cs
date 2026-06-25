using System;
using Godot;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// 战斗格子状态数据。
// 翻译自 battle_cell_state.gd（2026-05-24，数据层 C# 迁移）。
public partial class BattleCellState
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

    public Vector2I coord { get; internal set; } = Vector2I.Zero;
    public int stack_layer { get; internal set; }
    public StringName base_terrain { get; internal set; } = BattleTerrainRules.ToStringName(BattleTerrainKind.Land);
    public int base_height { get; internal set; }
    public int height_offset { get; internal set; }
    public int current_height { get; internal set; }
    public bool passable { get; internal set; } = true;
    public int move_cost { get; internal set; } = 1;
    public StringName occupant_unit_id { get; internal set; } = "";
    public List<StringName> prop_ids = new();
    public List<StringName> terrain_effect_ids = new();
    public List<BattleTerrainEffectState> timed_terrain_effects = new();
    public Vector2I flow_direction = Vector2I.Zero;
    public BattleEdgeFeatureState edge_feature_east;
    public BattleEdgeFeatureState edge_feature_south;

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

    public void SetTerrain(StringName terrain)
    {
        SetBaseTerrain(terrain);
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
        base_terrain = BattleTerrainRules.NormalizeTerrainId(terrain);
        RecalculateRuntimeValues();
    }

    public void SetBaseHeight(int height)
    {
        base_height = height;
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
            ReplaceEdgeFeature(ref edge_feature_east, normalizedFeature);
        }
        else if (direction == Vector2I.Down)
        {
            ReplaceEdgeFeature(ref edge_feature_south, normalizedFeature);
        }
        else
        {
            return;
        }
    }

    public void ClearEdgeFeature(Vector2I direction)
    {
        SetEdgeFeature(direction, null);
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
            prop_ids = DuplicateStringNameList(prop_ids),
            terrain_effect_ids = DuplicateStringNameList(terrain_effect_ids),
            timed_terrain_effects = BattleTerrainEffectState.DuplicateList(timed_terrain_effects),
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
            ["prop_ids"] = StringNameListToStrings(prop_ids),
            ["terrain_effect_ids"] = StringNameListToStrings(terrain_effect_ids),
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

        List<StringName> parsedPropIds = StringsToStringNameList(
            GetExactValueOrNull(payload, "prop_ids")
        );
        if (parsedPropIds == null)
        {
            return null;
        }
        List<StringName> parsedTerrainEffectIds = StringsToStringNameList(
            GetExactValueOrNull(payload, "terrain_effect_ids")
        );
        if (parsedTerrainEffectIds == null)
        {
            return null;
        }
        List<BattleTerrainEffectState> parsedTimedTerrainEffects =
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
            edge_feature_east = eastFeature,
            edge_feature_south = southFeature,
        };
        cellState.RecalculateRuntimeValues();
        return cellState;
    }

    internal static Dictionary<Vector2I, List<BattleCellState>> BuildColumnsFromSurfaceCells(
        GDictionary surface_cells
    )
    {
        Dictionary<Vector2I, List<BattleCellState>> columns = new();
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

    internal static Dictionary<Vector2I, List<BattleCellState>> BuildColumnsFromSurfaceCells(
        IReadOnlyDictionary<Vector2I, BattleCellState> surfaceCells
    )
    {
        Dictionary<Vector2I, List<BattleCellState>> columns = new();
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

    internal static Dictionary<Vector2I, List<BattleCellState>> CloneColumns(
        IReadOnlyDictionary<Vector2I, List<BattleCellState>> columns
    )
    {
        Dictionary<Vector2I, List<BattleCellState>> cloned = new();
        if (columns == null)
        {
            return cloned;
        }
        foreach ((Vector2I coord, List<BattleCellState> column) in columns)
        {
            cloned[coord] = DuplicateCellList(column);
        }
        return cloned;
    }

    internal static List<BattleCellState> BuildStackedCellsFromSurfaceCell(
        BattleCellState surface_cell
    )
    {
        var column = new List<BattleCellState>();
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

    internal static GDictionary ProjectCellsToPayload(
        IReadOnlyDictionary<Vector2I, BattleCellState> cells
    )
    {
        GDictionary payload = new();
        if (cells == null)
        {
            return payload;
        }
        foreach ((Vector2I coord, BattleCellState cell) in cells)
        {
            if (cell != null)
            {
                payload[coord] = cell.ToDictionary();
            }
        }
        return payload;
    }

    internal static GDictionary ProjectColumnsToPayload(
        IReadOnlyDictionary<Vector2I, List<BattleCellState>> columns
    )
    {
        GDictionary payload = new();
        if (columns == null)
        {
            return payload;
        }
        foreach ((Vector2I coord, List<BattleCellState> column) in columns)
        {
            GArray columnPayload = new();
            foreach (BattleCellState cell in column ?? new List<BattleCellState>())
            {
                if (cell != null)
                {
                    columnPayload.Add(cell.ToDictionary());
                }
            }
            payload[coord] = columnPayload;
        }
        return payload;
    }

    internal static List<BattleCellState> ParseColumnPayload(object rawColumn)
    {
        if (!TryRawArray(rawColumn, out GArray rawValues))
        {
            return null;
        }
        List<BattleCellState> column = new();
        foreach (object rawValue in rawValues)
        {
            if (!TryAsCellState(rawValue, out BattleCellState cell))
            {
                return null;
            }
            column.Add(cell);
        }
        return column;
    }

    internal static bool TryReadCellPayload(object rawValue, out BattleCellState value) =>
        TryAsCellState(rawValue, out value);

    private static Godot.Collections.Array<string> StringNameListToStrings(
        IEnumerable<StringName> values
    )
    {
        var results = new Godot.Collections.Array<string>();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            results.Add(value.ToString());
        }
        return results;
    }

    private static List<StringName> StringsToStringNameList(object values)
    {
        var results = new List<StringName>();
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

    private static List<BattleTerrainEffectState> TerrainEffectsFromPayload(
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
        List<BattleTerrainEffectState> effectStates =
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
        return featureState != null ? featureState.ToDictionary() : NoneEdgeFeatureToDict();
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
        if (rawValue is BattleCellState typedValue)
        {
            value = typedValue;
            return true;
        }
        if (rawValue is Variant variantValue)
        {
            if (variantValue.VariantType == Variant.Type.Dictionary)
            {
                value = FromDictionary(variantValue.AsGodotDictionary());
                return value != null;
            }
            value = null;
            return false;
        }
        if (rawValue is GDictionary payload)
        {
            value = FromDictionary(payload);
            return value != null;
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
                return true;
            }
            value = "";
            return false;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        if (rawValue is StringName stringNameValue)
        {
            value = stringNameValue.ToString();
            return true;
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

    private static void ReplaceEdgeFeature(
        ref BattleEdgeFeatureState slot,
        BattleEdgeFeatureState replacement
    )
    {
        slot = replacement;
    }

    private static GDictionary NoneEdgeFeatureToDict()
    {
        return new GDictionary
        {
            ["feature_kind"] = BattleEdgeFeatureState
                .ToStringName(BattleEdgeFeatureKind.None)
                .ToString(),
            ["render_kind"] = BattleEdgeFeatureState
                .ToStringName(BattleEdgeRenderKind.None)
                .ToString(),
            ["render_layers"] = 0,
            ["blocks_move"] = false,
            ["blocks_occupancy"] = false,
            ["blocks_los"] = false,
            ["interaction_kind"] = BattleEdgeFeatureState
                .ToStringName(BattleEdgeInteractionKind.None)
                .ToString(),
            ["state_tag"] = "",
        };
    }

    internal static void DisposeRuntimeGraph(BattleCellState cell)
    {
        if (cell == null)
            return;

        if (cell.timed_terrain_effects != null)
        {
            cell.timed_terrain_effects.Clear();
        }
        cell.prop_ids?.Clear();
        cell.terrain_effect_ids?.Clear();

        cell.edge_feature_east = null;
        cell.edge_feature_south = null;
    }

    private static List<StringName> DuplicateStringNameList(IEnumerable<StringName> values)
    {
        var duplicated = new List<StringName>();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            duplicated.Add(new StringName(value.ToString()));
        }
        return duplicated;
    }

    private static List<BattleCellState> DuplicateCellList(IEnumerable<BattleCellState> values)
    {
        var duplicated = new List<BattleCellState>();
        foreach (BattleCellState value in values ?? Array.Empty<BattleCellState>())
        {
            if (value != null)
            {
                duplicated.Add(value.DuplicateCell());
            }
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
