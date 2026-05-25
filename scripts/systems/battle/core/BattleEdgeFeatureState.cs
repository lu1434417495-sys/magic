using Godot;
using GDictionary = Godot.Collections.Dictionary;

// 战斗边缘 authoring 特征数据。
// 翻译自 battle_edge_feature_state.gd（2026-05-24，数据层 C# 迁移）。
[GlobalClass]
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

    public static StringName FEATURE_NONE() => _FEATURE_NONE;
    public static StringName FEATURE_WALL() => _FEATURE_WALL;
    public static StringName FEATURE_LOW_WALL() => _FEATURE_LOW_WALL;
    public static StringName FEATURE_DOOR() => _FEATURE_DOOR;
    public static StringName FEATURE_GATE() => _FEATURE_GATE;
    public static StringName RENDER_NONE() => _RENDER_NONE;
    public static StringName RENDER_WALL() => _RENDER_WALL;
    public static StringName INTERACT_NONE() => _INTERACT_NONE;
    public static StringName INTERACT_TOGGLE() => _INTERACT_TOGGLE;
    public static StringName INTERACT_BREAK() => _INTERACT_BREAK;

    public StringName feature_kind { get; set; } = _FEATURE_NONE;
    public StringName render_kind { get; set; } = _RENDER_NONE;
    public int render_layers { get; set; }
    public bool blocks_move { get; set; }
    public bool blocks_occupancy { get; set; }
    public bool blocks_los { get; set; }
    public StringName interaction_kind { get; set; } = _INTERACT_NONE;
    public StringName state_tag { get; set; } = "";

    public bool is_empty()
    {
        return feature_kind == _FEATURE_NONE && render_kind == _RENDER_NONE && render_layers <= 0;
    }

    public bool duplicates_render_of(StringName other_feature_kind)
    {
        return render_kind == other_feature_kind;
    }

    public BattleEdgeFeatureState duplicate_feature()
    {
        return from_dict(to_dict());
    }

    public GDictionary to_dict()
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

    public static BattleEdgeFeatureState from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        GDictionary featureDict = data.AsGodotDictionary();
        if (!HasExactSchemaFields(featureDict))
        {
            return null;
        }

        Variant featureKind = Get(featureDict, "feature_kind");
        Variant renderKind = Get(featureDict, "render_kind");
        Variant renderLayers = Get(featureDict, "render_layers");
        Variant blocksMoveValue = Get(featureDict, "blocks_move");
        Variant blocksOccupancyValue = Get(featureDict, "blocks_occupancy");
        Variant blocksLosValue = Get(featureDict, "blocks_los");
        Variant interactionKind = Get(featureDict, "interaction_kind");
        Variant stateTag = Get(featureDict, "state_tag");

        if (!IsNonEmptyStringLike(featureKind))
            return null;
        if (!IsNonEmptyStringLike(renderKind))
            return null;
        if (!IsNonEmptyStringLike(interactionKind))
            return null;
        if (!IsStringLike(stateTag))
            return null;
        if (renderLayers.VariantType != Variant.Type.Int || renderLayers.AsInt32() < 0)
            return null;
        if (blocksMoveValue.VariantType != Variant.Type.Bool ||
            blocksOccupancyValue.VariantType != Variant.Type.Bool ||
            blocksLosValue.VariantType != Variant.Type.Bool)
        {
            return null;
        }

        return new BattleEdgeFeatureState
        {
            feature_kind = ToStringName(featureKind),
            render_kind = ToStringName(renderKind),
            render_layers = renderLayers.AsInt32(),
            blocks_move = blocksMoveValue.AsBool(),
            blocks_occupancy = blocksOccupancyValue.AsBool(),
            blocks_los = blocksLosValue.AsBool(),
            interaction_kind = ToStringName(interactionKind),
            state_tag = ToStringName(stateTag),
        };
    }

    public static BattleEdgeFeatureState make_none()
    {
        return new BattleEdgeFeatureState();
    }

    public static BattleEdgeFeatureState make_wall()
    {
        return new BattleEdgeFeatureState
        {
            feature_kind = _FEATURE_WALL,
            render_kind = _RENDER_WALL,
            render_layers = 1,
            blocks_move = true,
            blocks_occupancy = true,
            blocks_los = true,
        };
    }

    public static BattleEdgeFeatureState make_low_wall()
    {
        return new BattleEdgeFeatureState
        {
            feature_kind = _FEATURE_LOW_WALL,
            render_kind = _RENDER_WALL,
            render_layers = 1,
        };
    }

    public static BattleEdgeFeatureState make_toggle_door(bool is_open = false)
    {
        return new BattleEdgeFeatureState
        {
            feature_kind = _FEATURE_DOOR,
            render_kind = is_open ? _RENDER_NONE : _RENDER_WALL,
            render_layers = is_open ? 0 : 1,
            blocks_move = !is_open,
            blocks_occupancy = !is_open,
            blocks_los = !is_open,
            interaction_kind = _INTERACT_TOGGLE,
            state_tag = is_open ? "open" : "closed",
        };
    }

    private static bool HasExactSchemaFields(GDictionary featureDict)
    {
        if (featureDict.Count != SchemaFields.Length)
        {
            return false;
        }
        foreach (Variant keyVariant in featureDict.Keys)
        {
            if (keyVariant.VariantType != Variant.Type.String)
            {
                return false;
            }
            if (!HasString(SchemaFields, keyVariant.AsString()))
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

    private static bool IsStringLike(Variant value)
    {
        return value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName;
    }

    private static bool IsNonEmptyStringLike(Variant value)
    {
        return IsStringLike(value) && !string.IsNullOrEmpty(value.AsString());
    }

    private static StringName ToStringName(Variant value)
    {
        return IsStringLike(value) ? new StringName(value.AsString()) : "";
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

    private static Variant Get(GDictionary payload, string key)
    {
        return payload.ContainsKey(key) ? payload[key] : default;
    }
}
