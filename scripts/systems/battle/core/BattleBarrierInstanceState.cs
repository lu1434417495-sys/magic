using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleBarrierInstanceState
{
    private readonly List<BattleBarrierLayerState> _layers = new();

    public StringName BarrierInstanceId { get; set; } = "";
    public StringName ProfileId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public StringName SourceUnitId { get; set; } = "";
    public StringName SourceSkillId { get; set; } = "";
    public StringName AnchorMode { get; set; } = "fixed";
    public Vector2I AnchorCoord { get; set; } = Vector2I.Zero;
    public int RadiusCells { get; set; }
    public StringName AreaPattern { get; set; } = "diamond";
    public int RemainingTu { get; set; }
    public int CreatedTu { get; set; }
    public int SaveDc { get; set; }
    public bool CatchAllProjectedEffects { get; set; }
    public IReadOnlyList<BattleBarrierLayerState> Layers => _layers;

    public bool IsEmpty => BarrierInstanceId == "" && ProfileId == "" && _layers.Count == 0;

    public static BattleBarrierInstanceState FromRuntimeDict(GDictionary source)
    {
        var instance = new BattleBarrierInstanceState();
        if (source == null || source.Count == 0)
        {
            return instance;
        }

        instance.BarrierInstanceId = ProgressionDataUtils.to_string_name(
            ReadStringName(source, "barrier_instance_id")
        );
        instance.ProfileId = ReadStringName(source, "profile_id");
        instance.DisplayName = ReadString(source, "display_name");
        instance.SourceUnitId = ProgressionDataUtils.to_string_name(
            ReadStringName(source, "source_unit_id")
        );
        instance.SourceSkillId = ProgressionDataUtils.to_string_name(
            ReadStringName(source, "source_skill_id")
        );
        instance.AnchorMode = ReadStringName(source, "anchor_mode", "fixed");
        instance.AnchorCoord = ReadVector2I(source, "anchor_coord");
        instance.RadiusCells = ReadInt(source, "radius_cells");
        instance.AreaPattern = ReadStringName(source, "area_pattern", "diamond");
        instance.RemainingTu = ReadInt(source, "remaining_tu");
        instance.CreatedTu = ReadInt(source, "created_tu");
        instance.SaveDc = ReadInt(source, "save_dc");
        instance.CatchAllProjectedEffects = ReadBool(source, "catch_all_projected_effects");
        instance.SetLayers(ReadLayerList(GetArray(source, "layers")));
        return instance;
    }

    public List<BattleBarrierLayerState> GetLayersTyped()
    {
        return new List<BattleBarrierLayerState>(_layers);
    }

    public void SetLayers(IEnumerable<BattleBarrierLayerState> layerStates)
    {
        _layers.Clear();
        if (layerStates == null)
        {
            return;
        }
        foreach (BattleBarrierLayerState layer in layerStates)
        {
            if (layer != null && layer.LayerId != "")
            {
                _layers.Add(layer);
            }
        }
    }

    public GDictionary ToRuntimeDict()
    {
        return new GDictionary
        {
            ["barrier_instance_id"] = BarrierInstanceId.ToString(),
            ["profile_id"] = ProfileId.ToString(),
            ["display_name"] = DisplayName,
            ["source_unit_id"] = SourceUnitId.ToString(),
            ["source_skill_id"] = SourceSkillId.ToString(),
            ["anchor_mode"] = AnchorMode.ToString(),
            ["anchor_coord"] = AnchorCoord,
            ["radius_cells"] = RadiusCells,
            ["area_pattern"] = AreaPattern.ToString(),
            ["remaining_tu"] = RemainingTu,
            ["created_tu"] = CreatedTu,
            ["save_dc"] = SaveDc,
            ["catch_all_projected_effects"] = CatchAllProjectedEffects,
            ["layers"] = LayerArray(_layers),
        };
    }

    private static GArray LayerArray(IEnumerable<BattleBarrierLayerState> layers)
    {
        var result = new GArray();
        if (layers == null)
        {
            return result;
        }
        foreach (BattleBarrierLayerState layer in layers)
        {
            if (layer != null && layer.LayerId != "")
            {
                result.Add(layer.ToRuntimeDict());
            }
        }
        return result;
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
        string fallbackText = fallback == default ? "" : fallback.ToString();
        string text = ReadString(source, key, fallbackText);
        if (string.IsNullOrEmpty(text))
        {
            return fallback == default ? new StringName("") : fallback;
        }
        return new StringName(text);
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

    private static List<BattleBarrierLayerState> ReadLayerList(GArray values)
    {
        var result = new List<BattleBarrierLayerState>();
        if (values == null)
        {
            return result;
        }
        foreach (var rawValue in values)
        {
            BattleBarrierLayerState layer = BattleBarrierLayerState.FromRuntimeDict(
                rawValue.AsGodotDictionary()
            );
            if (layer != null && layer.LayerId != "")
            {
                result.Add(layer);
            }
        }
        return result;
    }
}
