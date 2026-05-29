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
            GetValue(source, "barrier_instance_id", "")
        );
        instance.profile_id = ProgressionDataUtils.to_string_name(GetValue(source, "profile_id", ""));
        instance.display_name = GetValue(source, "display_name", "").AsString();
        instance.source_unit_id = ProgressionDataUtils.to_string_name(
            GetValue(source, "source_unit_id", "")
        );
        instance.source_skill_id = ProgressionDataUtils.to_string_name(
            GetValue(source, "source_skill_id", "")
        );
        instance.anchor_mode = ProgressionDataUtils.to_string_name(
            GetValue(source, "anchor_mode", "fixed")
        );
        var anchorValue = GetValue(source, "anchor_coord", Vector2I.Zero);
        instance.anchor_coord =
            anchorValue.VariantType == Variant.Type.Vector2I
                ? anchorValue.AsVector2I()
                : Vector2I.Zero;
        instance.radius_cells = GetValue(source, "radius_cells", 0).AsInt32();
        instance.area_pattern = ProgressionDataUtils.to_string_name(
            GetValue(source, "area_pattern", "diamond")
        );
        instance.remaining_tu = GetValue(source, "remaining_tu", 0).AsInt32();
        instance.created_tu = GetValue(source, "created_tu", 0).AsInt32();
        instance.save_dc = GetValue(source, "save_dc", 0).AsInt32();
        instance.catch_all_projected_effects = GetValue(
            source,
            "catch_all_projected_effects",
            false
        ).AsBool();
        instance.layers = GetArray(source, "layers").Duplicate(true);
        return instance;
    }

    public List<BattleBarrierLayerState> GetLayersTyped()
    {
        var result = new List<BattleBarrierLayerState>();
        foreach (var layerValue in layers ?? new GArray())
        {
            if (layerValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
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

    private static Variant GetValue(GDictionary source, object key, object fallback)
    {
        if (source == null)
        {
            return GdInterop.GetValueOrDefault(null, "", fallback);
        }
        return source.GetValueOrDefault(key, GdInterop.GetValueOrDefault(null, "", fallback));
    }

    private static GArray GetArray(GDictionary source, object key)
    {
        var value = GetValue(source, key, new GArray());
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }
}
