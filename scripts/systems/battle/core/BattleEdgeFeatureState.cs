using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal enum BattleEdgeFeatureKind
{
    Unknown = 0,
    None,
    Wall,
    LowWall,
    Door,
    Gate,
}

internal enum BattleEdgeRenderKind
{
    Unknown = 0,
    None,
    Wall,
}

internal enum BattleEdgeInteractionKind
{
    Unknown = 0,
    None,
    Toggle,
    Break,
}

// 战斗边缘 authoring 特征数据。
// 翻译自 battle_edge_feature_state.gd（2026-05-24，数据层 C# 迁移）。
public partial class BattleEdgeFeatureState : RefCounted
{
    private static readonly StringName _FEATURE_NONE = "none";
    private static readonly StringName _FEATURE_WALL = "wall";
    private static readonly StringName _FEATURE_LOW_WALL = "low_wall";
    private static readonly StringName _FEATURE_DOOR = "door";
    private static readonly StringName _FEATURE_GATE = "gate";

    private static readonly StringName _RENDER_NONE = "none";
    private static readonly StringName _RENDER_WALL = "wall";

    private static readonly StringName _INTERACT_NONE = "none";
    private static readonly StringName _INTERACT_TOGGLE = "toggle";
    private static readonly StringName _INTERACT_BREAK = "break";

    private static readonly string[] SchemaFields =
    {
        "feature_kind",
        "render_kind",
        "render_layers",
        "blocks_move",
        "blocks_occupancy",
        "blocks_los",
        "interaction_kind",
        "state_tag",
    };

    public StringName feature_kind { get; set; } = _FEATURE_NONE;
    public StringName render_kind { get; set; } = _RENDER_NONE;
    public int render_layers { get; set; }
    public bool blocks_move { get; set; }
    public bool blocks_occupancy { get; set; }
    public bool blocks_los { get; set; }
    public StringName interaction_kind { get; set; } = _INTERACT_NONE;
    public StringName state_tag { get; set; } = "";

    internal BattleEdgeFeatureKind FeatureKind
    {
        get => ToFeatureKind(feature_kind);
        set => feature_kind = ToStringName(value);
    }

    internal BattleEdgeRenderKind RenderKind
    {
        get => ToRenderKind(render_kind);
        set => render_kind = ToStringName(value);
    }

    internal BattleEdgeInteractionKind InteractionKind
    {
        get => ToInteractionKind(interaction_kind);
        set => interaction_kind = ToStringName(value);
    }

    public bool IsEmpty()
    {
        return FeatureKind == BattleEdgeFeatureKind.None
            && RenderKind == BattleEdgeRenderKind.None
            && render_layers <= 0;
    }

    public bool DuplicatesRenderOf(StringName other_feature_kind)
    {
        return RenderKind == ToRenderKind(other_feature_kind);
    }

    internal static StringName ToStringName(BattleEdgeFeatureKind kind)
    {
        return kind switch
        {
            BattleEdgeFeatureKind.None => _FEATURE_NONE,
            BattleEdgeFeatureKind.Wall => _FEATURE_WALL,
            BattleEdgeFeatureKind.LowWall => _FEATURE_LOW_WALL,
            BattleEdgeFeatureKind.Door => _FEATURE_DOOR,
            BattleEdgeFeatureKind.Gate => _FEATURE_GATE,
            _ => new StringName(""),
        };
    }

    internal static BattleEdgeFeatureKind ToFeatureKind(StringName value)
    {
        if (value == _FEATURE_NONE)
            return BattleEdgeFeatureKind.None;
        if (value == _FEATURE_WALL)
            return BattleEdgeFeatureKind.Wall;
        if (value == _FEATURE_LOW_WALL)
            return BattleEdgeFeatureKind.LowWall;
        if (value == _FEATURE_DOOR)
            return BattleEdgeFeatureKind.Door;
        if (value == _FEATURE_GATE)
            return BattleEdgeFeatureKind.Gate;
        return BattleEdgeFeatureKind.Unknown;
    }

    internal static StringName ToStringName(BattleEdgeRenderKind kind)
    {
        return kind switch
        {
            BattleEdgeRenderKind.None => _RENDER_NONE,
            BattleEdgeRenderKind.Wall => _RENDER_WALL,
            _ => new StringName(""),
        };
    }

    internal static BattleEdgeRenderKind ToRenderKind(StringName value)
    {
        if (value == _RENDER_NONE)
            return BattleEdgeRenderKind.None;
        if (value == _RENDER_WALL)
            return BattleEdgeRenderKind.Wall;
        return BattleEdgeRenderKind.Unknown;
    }

    internal static StringName ToStringName(BattleEdgeInteractionKind kind)
    {
        return kind switch
        {
            BattleEdgeInteractionKind.None => _INTERACT_NONE,
            BattleEdgeInteractionKind.Toggle => _INTERACT_TOGGLE,
            BattleEdgeInteractionKind.Break => _INTERACT_BREAK,
            _ => new StringName(""),
        };
    }

    internal static BattleEdgeInteractionKind ToInteractionKind(StringName value)
    {
        if (value == _INTERACT_NONE)
            return BattleEdgeInteractionKind.None;
        if (value == _INTERACT_TOGGLE)
            return BattleEdgeInteractionKind.Toggle;
        if (value == _INTERACT_BREAK)
            return BattleEdgeInteractionKind.Break;
        return BattleEdgeInteractionKind.Unknown;
    }

    public BattleEdgeFeatureState DuplicateFeature()
    {
        return new BattleEdgeFeatureState
        {
            feature_kind = feature_kind,
            render_kind = render_kind,
            render_layers = render_layers,
            blocks_move = blocks_move,
            blocks_occupancy = blocks_occupancy,
            blocks_los = blocks_los,
            interaction_kind = interaction_kind,
            state_tag = state_tag,
        };
    }

    internal GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["feature_kind"] = feature_kind.ToString(),
            ["render_kind"] = render_kind.ToString(),
            ["render_layers"] = render_layers,
            ["blocks_move"] = blocks_move,
            ["blocks_occupancy"] = blocks_occupancy,
            ["blocks_los"] = blocks_los,
            ["interaction_kind"] = interaction_kind.ToString(),
            ["state_tag"] = state_tag.ToString(),
        };
    }

    internal static BattleEdgeFeatureState FromDictionary(GDictionary featureDict)
    {
        if (featureDict == null)
            return null;
        if (!HasExactSchemaFields(featureDict))
        {
            return null;
        }

        if (!TryGetStringLike(featureDict, "feature_kind", out string featureKindText)
            || string.IsNullOrEmpty(featureKindText))
            return null;
        if (!TryGetStringLike(featureDict, "render_kind", out string renderKindText)
            || string.IsNullOrEmpty(renderKindText))
            return null;
        if (!TryGetStringLike(featureDict, "interaction_kind", out string interactionKindText)
            || string.IsNullOrEmpty(interactionKindText))
            return null;
        BattleEdgeFeatureKind featureKind = ToFeatureKind(new StringName(featureKindText));
        BattleEdgeRenderKind renderKind = ToRenderKind(new StringName(renderKindText));
        BattleEdgeInteractionKind interactionKind = ToInteractionKind(
            new StringName(interactionKindText)
        );
        if (
            featureKind == BattleEdgeFeatureKind.Unknown
            || renderKind == BattleEdgeRenderKind.Unknown
            || interactionKind == BattleEdgeInteractionKind.Unknown
        )
            return null;
        if (!TryGetStringLike(featureDict, "state_tag", out string stateTag))
            return null;
        if (!TryGetStrictInt(featureDict, "render_layers", out int renderLayers) || renderLayers < 0)
            return null;
        if (!TryReadBoolField(featureDict, "blocks_move", out bool blocksMove)
            || !TryReadBoolField(featureDict, "blocks_occupancy", out bool blocksOccupancy)
            || !TryReadBoolField(featureDict, "blocks_los", out bool blocksLos))
        {
            return null;
        }

        return new BattleEdgeFeatureState
        {
            FeatureKind = featureKind,
            RenderKind = renderKind,
            render_layers = renderLayers,
            blocks_move = blocksMove,
            blocks_occupancy = blocksOccupancy,
            blocks_los = blocksLos,
            InteractionKind = interactionKind,
            state_tag = new StringName(stateTag),
        };
    }

    public static BattleEdgeFeatureState MakeNone()
    {
        return new BattleEdgeFeatureState();
    }

    public static BattleEdgeFeatureState MakeWall()
    {
        return new BattleEdgeFeatureState
        {
            FeatureKind = BattleEdgeFeatureKind.Wall,
            RenderKind = BattleEdgeRenderKind.Wall,
            render_layers = 1,
            blocks_move = true,
            blocks_occupancy = true,
            blocks_los = true,
        };
    }

    public static BattleEdgeFeatureState MakeLowWall()
    {
        return new BattleEdgeFeatureState
        {
            FeatureKind = BattleEdgeFeatureKind.LowWall,
            RenderKind = BattleEdgeRenderKind.Wall,
            render_layers = 1,
        };
    }

    public static BattleEdgeFeatureState MakeToggleDoor(bool is_open = false)
    {
        return new BattleEdgeFeatureState
        {
            FeatureKind = BattleEdgeFeatureKind.Door,
            RenderKind = is_open ? BattleEdgeRenderKind.None : BattleEdgeRenderKind.Wall,
            render_layers = is_open ? 0 : 1,
            blocks_move = !is_open,
            blocks_occupancy = !is_open,
            blocks_los = !is_open,
            InteractionKind = BattleEdgeInteractionKind.Toggle,
            state_tag = is_open ? "open" : "closed",
        };
    }

    private static bool HasExactSchemaFields(GDictionary featureDict)
    {
        if (featureDict.Count != SchemaFields.Length)
        {
            return false;
        }
        foreach (var keyValue in featureDict.Keys)
        {
            string key = keyValue.ToString();
            if (!HasString(SchemaFields, key))
            {
                return false;
            }
        }
        foreach (string field in SchemaFields)
        {
            if (!featureDict.ContainsKey(field))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryGetStringLike(GDictionary data, string key, out string value)
    {
        if (data != null && data.ContainsKey(key) && IsStringLikeField(data, key))
        {
            value = data[key].ToString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetStrictInt(GDictionary data, string key, out int value)
    {
        if (data != null && data.ContainsKey(key) && IsFieldType(data, key, "Int"))
        {
            value = data[key].AsInt32();
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryReadBoolField(GDictionary data, string key, out bool value)
    {
        if (data != null && data.ContainsKey(key) && IsFieldType(data, key, "Bool"))
        {
            value = data[key].AsBool();
            return true;
        }
        value = false;
        return false;
    }

    private static bool IsStringLikeField(GDictionary data, string key)
    {
        string typeName = data[key].VariantType.ToString();
        return typeName == "String" || typeName == "StringName";
    }

    private static bool IsFieldType(GDictionary data, string key, string expectedTypeName)
    {
        return data[key].VariantType.ToString() == expectedTypeName;
    }

    private static bool HasString(string[] values, string value)
    {
        foreach (string entry in values)
        {
            if (entry == value)
            {
                return true;
            }
        }
        return false;
    }

}
