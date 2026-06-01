using Godot;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleBarrierInstanceState : RefCounted
{
    public StringName barrier_instance_id { get; set; } = "";
    public StringName profile_id { get; set; } = "";
    public string display_name { get; set; } = "";
    public StringName source_unit_id { get; set; } = "";
    public StringName source_skill_id { get; set; } = "";
    public StringName anchor_mode { get; set; } = "fixed";
    public Vector2I anchor_coord { get; set; } = Vector2I.Zero;
    public int radius_cells { get; set; }
    public StringName area_pattern { get; set; } = "diamond";
    public int remaining_tu { get; set; }
    public int created_tu { get; set; }
    public int save_dc { get; set; }
    public bool catch_all_projected_effects { get; set; }
    public GArray layers { get; set; } = new();

    public bool IsEmpty => barrier_instance_id == "" && profile_id == "" && layers.Count == 0;

    public static BattleBarrierInstanceState from_runtime_dict(GDictionary source)
    {
        var instance = new BattleBarrierInstanceState();
        if (source == null || source.Count == 0)
        {
            return instance;
        }

        instance.barrier_instance_id = ProgressionDataUtils.to_string_name(
            ReadStringName(source, "barrier_instance_id")
        );
        instance.profile_id = ReadStringName(source, "profile_id");
        instance.display_name = ReadString(source, "display_name");
        instance.source_unit_id = ProgressionDataUtils.to_string_name(
            ReadStringName(source, "source_unit_id")
        );
        instance.source_skill_id = ProgressionDataUtils.to_string_name(
            ReadStringName(source, "source_skill_id")
        );
        instance.anchor_mode = ReadStringName(source, "anchor_mode", "fixed");
        instance.anchor_coord = ReadVector2I(source, "anchor_coord");
        instance.radius_cells = ReadInt(source, "radius_cells");
        instance.area_pattern = ReadStringName(source, "area_pattern", "diamond");
        instance.remaining_tu = ReadInt(source, "remaining_tu");
        instance.created_tu = ReadInt(source, "created_tu");
        instance.save_dc = ReadInt(source, "save_dc");
        instance.catch_all_projected_effects = ReadBool(source, "catch_all_projected_effects");
        instance.layers = GetArray(source, "layers").Duplicate(true);
        return instance;
    }

    public List<BattleBarrierLayerState> GetLayersTyped()
    {
        var result = new List<BattleBarrierLayerState>();
        foreach (var layerValue in layers ?? new GArray())
        {
            BattleBarrierLayerState layer = BattleBarrierLayerState.from_runtime_dict(
                layerValue.AsGodotDictionary()
            );
            if (layer != null && layer.layer_id != "")
            {
                result.Add(layer);
            }
        }
        return result;
    }

    public void SetLayersTyped(IReadOnlyList<BattleBarrierLayerState> layerStates)
    {
        layers = new GArray();
        if (layerStates == null)
        {
            return;
        }
        foreach (BattleBarrierLayerState layer in layerStates)
        {
            if (layer != null)
            {
                layers.Add(layer.to_runtime_dict());
            }
        }
    }

    public GDictionary to_runtime_dict()
    {
        return new GDictionary
        {
            ["barrier_instance_id"] = barrier_instance_id.ToString(),
            ["profile_id"] = profile_id.ToString(),
            ["display_name"] = display_name,
            ["source_unit_id"] = source_unit_id.ToString(),
            ["source_skill_id"] = source_skill_id.ToString(),
            ["anchor_mode"] = anchor_mode.ToString(),
            ["anchor_coord"] = anchor_coord,
            ["radius_cells"] = radius_cells,
            ["area_pattern"] = area_pattern.ToString(),
            ["remaining_tu"] = remaining_tu,
            ["created_tu"] = created_tu,
            ["save_dc"] = save_dc,
            ["catch_all_projected_effects"] = catch_all_projected_effects,
            ["layers"] = layers.Duplicate(true),
        };
    }

    private static bool HasKey(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        return source.ContainsKey(key) || source.ContainsKey(new StringName(key));
    }

    private static string ReadString(GDictionary source, string key, string fallback = "")
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return fallback;
        }
        if (source.ContainsKey(key))
        {
            return source[key].ToString();
        }
        StringName stringNameKey = new(key);
        return source.ContainsKey(stringNameKey) ? source[stringNameKey].ToString() : fallback;
    }

    private static StringName ReadStringName(
        GDictionary source,
        string key,
        StringName fallback = default
    )
    {
        string text = ReadString(source, key, fallback.ToString());
        return string.IsNullOrEmpty(text) ? fallback : new StringName(text);
    }

    private static int ReadInt(GDictionary source, string key, int fallback = 0)
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return fallback;
        }
        if (source.ContainsKey(key))
        {
            return source[key].AsInt32();
        }
        StringName stringNameKey = new(key);
        return source.ContainsKey(stringNameKey) ? source[stringNameKey].AsInt32() : fallback;
    }

    private static bool ReadBool(GDictionary source, string key, bool fallback = false)
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return fallback;
        }
        if (source.ContainsKey(key))
        {
            return source[key].AsBool();
        }
        StringName stringNameKey = new(key);
        return source.ContainsKey(stringNameKey) ? source[stringNameKey].AsBool() : fallback;
    }

    private static Vector2I ReadVector2I(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return Vector2I.Zero;
        }
        if (source.ContainsKey(key))
        {
            return source[key].AsVector2I();
        }
        StringName stringNameKey = new(key);
        return source.ContainsKey(stringNameKey) ? source[stringNameKey].AsVector2I() : Vector2I.Zero;
    }

    private static GArray GetArray(GDictionary source, string key)
    {
        if (!HasKey(source, key))
        {
            return new GArray();
        }
        if (source.ContainsKey(key))
        {
            return source[key].AsGodotArray();
        }
        return source[new StringName(key)].AsGodotArray();
    }
}
