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

    public static BattleEdgeFeatureState from_dict(GDictionary featureDict)
    {
        if (featureDict == null)
            return null;
        if (!HasExactSchemaFields(featureDict))
        {
            return null;
        }

        if (!TryGetStringLike(featureDict, "feature_kind", out string featureKind)
            || string.IsNullOrEmpty(featureKind))
            return null;
        if (!TryGetStringLike(featureDict, "render_kind", out string renderKind)
            || string.IsNullOrEmpty(renderKind))
            return null;
        if (!TryGetStringLike(featureDict, "interaction_kind", out string interactionKind)
            || string.IsNullOrEmpty(interactionKind))
            return null;
        if (!TryGetStringLike(featureDict, "state_tag", out string stateTag))
            return null;
        if (!TryGetStrictInt(featureDict, "render_layers", out int renderLayers) || renderLayers < 0)
            return null;
        if (!TryGetBool(featureDict, "blocks_move", out bool blocksMove)
            || !TryGetBool(featureDict, "blocks_occupancy", out bool blocksOccupancy)
            || !TryGetBool(featureDict, "blocks_los", out bool blocksLos))
        {
            return null;
        }

        return new BattleEdgeFeatureState
        {
            feature_kind = new StringName(featureKind),
            render_kind = new StringName(renderKind),
            render_layers = renderLayers,
            blocks_move = blocksMove,
            blocks_occupancy = blocksOccupancy,
            blocks_los = blocksLos,
            interaction_kind = new StringName(interactionKind),
            state_tag = new StringName(stateTag),
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
        foreach (var keyValue in featureDict.Keys)
        {
            if (!TryAsStrictStringKey(keyValue, out string key))
            {
                return false;
            }
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
        if (TryGetExactValue(data, key, out Variant rawValue)
            && TryAsStringLike(rawValue, out value))
        {
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetStrictInt(GDictionary data, string key, out int value)
    {
        if (TryGetExactValue(data, key, out Variant rawValue)
            && TryAsStrictInt(rawValue, out value))
        {
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryGetBool(GDictionary data, string key, out bool value)
    {
        if (TryGetExactValue(data, key, out Variant rawValue) && TryAsBool(rawValue, out value))
        {
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryAsStrictStringKey(Variant rawValue, out string value)
    {
        if (rawValue.VariantType == Variant.Type.String)
        {
            value = rawValue.AsString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStringLike(Variant rawValue, out string value)
    {
        if (rawValue.VariantType == Variant.Type.String)
        {
            value = rawValue.AsString();
            return true;
        }
        if (rawValue.VariantType == Variant.Type.StringName)
        {
            value = rawValue.AsStringName().ToString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStrictInt(Variant rawValue, out int value)
    {
        if (rawValue.VariantType == Variant.Type.Int)
        {
            value = rawValue.AsInt32();
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryAsBool(Variant rawValue, out bool value)
    {
        if (rawValue.VariantType == Variant.Type.Bool)
        {
            value = rawValue.AsBool();
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryGetExactValue(GDictionary data, string key, out Variant value)
    {
        if (data != null && data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        value = default;
        return false;
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
