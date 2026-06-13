using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleSimTerrainGenerator
{
    public GDictionary GenerateTyped(
        EncounterAnchorData _encounter_anchor,
        int _seed,
        GDictionary context = null
    )
    {
        var cells = new GDictionary();

        if (context != null)
        {
            Variant cells_option = ReadContextValue(context, "cells");

            if (cells_option.VariantType == Variant.Type.Dictionary)
                cells = cells_option.AsGodotDictionary();
        }

        if (cells.Count == 0)
            return new GDictionary();

        var map_size = _resolve_map_size(cells, context ?? new GDictionary());

        return new GDictionary
        {
            ["map_size"] = map_size,

            ["cells"] = cells.Duplicate(true),

            ["cell_columns"] = BattleCellState.BuildColumnsFromSurfaceCells(cells),

            ["terrain_profile_id"] = ProgressionDataUtils.to_string_name(
                context != null
                    ? ReadContextValue(context, "battle_terrain_profile", Variant.From("default"))
                    : "default"
            ),

            ["ally_spawns"] = _duplicate_vector2i_array(
                context != null ? ReadContextValue(context, "ally_spawns") : default
            ),

            ["enemy_spawns"] = _duplicate_vector2i_array(
                context != null ? ReadContextValue(context, "enemy_spawns") : default
            ),
        };
    }

    private Vector2I _resolve_map_size(GDictionary cells, GDictionary context)
    {
        var explicit_size = Vector2I.Zero;

        if (context != null)
        {
            Variant battle_map_size = ReadContextValue(context, "battle_map_size");

            if (battle_map_size.VariantType == Variant.Type.Vector2I)
                explicit_size = battle_map_size.AsVector2I();
            else
            {
                Variant map_size = ReadContextValue(context, "map_size");

                if (map_size.VariantType == Variant.Type.Vector2I)
                    explicit_size = map_size.AsVector2I();
            }
        }

        if (explicit_size != Vector2I.Zero)
            return explicit_size;

        int max_x = -1;

        int max_y = -1;

        foreach (var coord_option in cells.Keys)
        {
            if (coord_option.VariantType != Variant.Type.Vector2I)
                continue;

            var coord = coord_option.AsVector2I();

            max_x = Mathf.Max(max_x, coord.X);

            max_y = Mathf.Max(max_y, coord.Y);
        }

        return max_x >= 0 && max_y >= 0 ? new Vector2I(max_x + 1, max_y + 1) : Vector2I.Zero;
    }

    private Godot.Collections.Array<Vector2I> _duplicate_vector2i_array(object rawValues)
    {
        var result = new Godot.Collections.Array<Vector2I>();

        Godot.Collections.Array values = rawValues switch
        {
            Variant value when value.VariantType == Variant.Type.Array => value.AsGodotArray(),
            Godot.Collections.Array array => array,
            _ => null,
        };
        if (values == null)
            return result;

        foreach (var value in values)
        {
            if (value.VariantType == Variant.Type.Vector2I)
                result.Add(value.AsVector2I());
        }

        return result;
    }

    private static Variant ReadContextValue(
        GDictionary context,
        string key,
        Variant fallback = default
    )
    {
        if (context == null || string.IsNullOrEmpty(key) || !context.ContainsKey(key))
            return fallback;
        return context[key];
    }

}
