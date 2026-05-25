using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;

[GlobalClass]
public partial class BattleSimTerrainGenerator : RefCounted
{
    public GDictionary generate(GodotObject _encounter_anchor, int _seed, GDictionary context = null)
    {
        var cells = new GDictionary();
        if (context != null)
        {
            var cells_variant = GetValue(context, "cells");
            if (cells_variant.VariantType == Variant.Type.Dictionary)
                cells = cells_variant.AsGodotDictionary();
        }
        if (cells.Count == 0)
            return new GDictionary();
        var map_size = _resolve_map_size(cells, context ?? new GDictionary());
        return new GDictionary
        {
            ["map_size"] = map_size,
            ["cells"] = cells.Duplicate(true),
            ["cell_columns"] = BattleCellState.build_columns_from_surface_cells(cells),
            ["terrain_profile_id"] = ProgressionDataUtils.to_string_name(context != null ? GetValue(context, "battle_terrain_profile", "default") : "default"),
            ["ally_spawns"] = _duplicate_vector2i_array(context != null ? GetValue(context, "ally_spawns") : default),
            ["enemy_spawns"] = _duplicate_vector2i_array(context != null ? GetValue(context, "enemy_spawns") : default),
        };
    }

    private Vector2I _resolve_map_size(GDictionary cells, GDictionary context)
    {
        var explicit_size = Vector2I.Zero;
        if (context != null)
        {
            var battle_map_size = GetValue(context, "battle_map_size");
            if (battle_map_size.VariantType == Variant.Type.Vector2I)
                explicit_size = battle_map_size.AsVector2I();
            else
            {
                var map_size = GetValue(context, "map_size");
                if (map_size.VariantType == Variant.Type.Vector2I)
                    explicit_size = map_size.AsVector2I();
            }
        }
        if (explicit_size != Vector2I.Zero)
            return explicit_size;
        int max_x = -1;
        int max_y = -1;
        foreach (Variant coord_variant in cells.Keys)
        {
            if (coord_variant.VariantType != Variant.Type.Vector2I)
                continue;
            var coord = coord_variant.AsVector2I();
            max_x = Mathf.Max(max_x, coord.X);
            max_y = Mathf.Max(max_y, coord.Y);
        }
        return max_x >= 0 && max_y >= 0 ? new Vector2I(max_x + 1, max_y + 1) : Vector2I.Zero;
    }

    private Godot.Collections.Array<Vector2I> _duplicate_vector2i_array(Variant values)
    {
        var result = new Godot.Collections.Array<Vector2I>();
        if (values.VariantType != Variant.Type.Array)
            return result;
        foreach (Variant value in values.AsGodotArray())
        {
            if (value.VariantType == Variant.Type.Vector2I)
                result.Add(value.AsVector2I());
        }
        return result;
    }

    private static Variant GetValue(GDictionary dictionary, string key, Variant fallback = default)
    {
        return dictionary != null && dictionary.ContainsKey(key) ? dictionary[key] : fallback;
    }
}
