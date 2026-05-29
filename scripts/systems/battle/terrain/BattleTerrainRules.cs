using Godot;
using GArray = Godot.Collections.Array;

[GlobalClass]
public partial class BattleTerrainRules : RefCounted
{
    private static readonly StringName TerrainLand = "land";
    private static readonly StringName TerrainForest = "forest";
    private static readonly StringName TerrainWater = "water";
    private static readonly StringName TerrainShallowWater = "shallow_water";
    private static readonly StringName TerrainFlowingWater = "flowing_water";
    private static readonly StringName TerrainDeepWater = "deep_water";
    private static readonly StringName TerrainMud = "mud";
    private static readonly StringName TerrainSpike = "spike";

    private static readonly StringName TagWade = "wade";
    private static readonly StringName TagAmphibious = "amphibious";
    private static readonly StringName TagFly = "fly";

    public static StringName TERRAIN_LAND() => TerrainLand;

    public static StringName TERRAIN_FOREST() => TerrainForest;

    public static StringName TERRAIN_WATER() => TerrainWater;

    public static StringName TERRAIN_SHALLOW_WATER() => TerrainShallowWater;

    public static StringName TERRAIN_FLOWING_WATER() => TerrainFlowingWater;

    public static StringName TERRAIN_DEEP_WATER() => TerrainDeepWater;

    public static StringName TERRAIN_MUD() => TerrainMud;

    public static StringName TERRAIN_SPIKE() => TerrainSpike;

    public static StringName TAG_WADE() => TagWade;

    public static StringName TAG_AMPHIBIOUS() => TagAmphibious;

    public static StringName TAG_FLY() => TagFly;

    public static Vector2I STILL_FLOW() => Vector2I.Zero;

    public static StringName normalize_terrain_id(StringName terrain_id)
    {
        if (terrain_id == null || terrain_id == "" || terrain_id == TerrainLand)
        {
            return TerrainLand;
        }
        return terrain_id;
    }

    public static bool is_water_terrain(StringName terrain_id)
    {
        StringName normalized = normalize_terrain_id(terrain_id);
        return normalized == TerrainWater
            || normalized == TerrainShallowWater
            || normalized == TerrainFlowingWater
            || normalized == TerrainDeepWater;
    }

    public static bool get_global_passable(StringName terrain_id)
    {
        return normalize_terrain_id(terrain_id) != TerrainDeepWater;
    }

    public static int get_base_move_cost(StringName terrain_id)
    {
        StringName normalized = normalize_terrain_id(terrain_id);
        if (
            normalized == TerrainMud
            || normalized == TerrainSpike
            || normalized == TerrainShallowWater
        )
        {
            return 2;
        }
        if (normalized == TerrainFlowingWater)
        {
            return 3;
        }
        if (normalized == TerrainDeepWater)
        {
            return 2;
        }
        return 1;
    }

    public static bool can_unit_enter_terrain(
        StringName terrain_id,
        GArray movement_tags = null
    )
    {
        if (has_movement_tag(movement_tags, TagFly))
        {
            return true;
        }
        return normalize_terrain_id(terrain_id) != TerrainDeepWater
            || has_movement_tag(movement_tags, TagAmphibious);
    }

    public static int get_unit_move_cost(StringName terrain_id, GArray movement_tags = null)
    {
        StringName normalized = normalize_terrain_id(terrain_id);
        if (has_movement_tag(movement_tags, TagFly) && is_water_terrain(normalized))
        {
            return 1;
        }

        if (normalized == TerrainShallowWater)
        {
            if (
                has_movement_tag(movement_tags, TagAmphibious)
                || has_movement_tag(movement_tags, TagWade)
            )
            {
                return 1;
            }
            return 2;
        }
        if (normalized == TerrainFlowingWater)
        {
            if (has_movement_tag(movement_tags, TagAmphibious))
            {
                return 1;
            }
            if (has_movement_tag(movement_tags, TagWade))
            {
                return 2;
            }
            return 3;
        }
        if (normalized == TerrainDeepWater)
        {
            return has_movement_tag(movement_tags, TagAmphibious) ? 2 : 999999;
        }
        return get_base_move_cost(normalized);
    }

    public static string get_display_name(StringName terrain_id)
    {
        StringName normalized = normalize_terrain_id(terrain_id);
        if (normalized == TerrainLand)
        {
            return "陆地";
        }
        if (normalized == TerrainForest)
        {
            return "森林";
        }
        if (normalized == TerrainShallowWater)
        {
            return "浅水";
        }
        if (normalized == TerrainFlowingWater)
        {
            return "流水";
        }
        if (normalized == TerrainDeepWater)
        {
            return "深水";
        }
        if (normalized == TerrainMud)
        {
            return "泥沼";
        }
        if (normalized == TerrainSpike)
        {
            return "地刺";
        }
        return terrain_id?.ToString() ?? "";
    }

    public static bool can_host_tent(StringName terrain_id)
    {
        StringName normalized = normalize_terrain_id(terrain_id);
        return normalized == TerrainLand || normalized == TerrainForest;
    }

    public static bool can_host_torch(StringName terrain_id)
    {
        StringName normalized = normalize_terrain_id(terrain_id);
        return !is_water_terrain(normalized) && normalized != TerrainSpike;
    }

    public static bool is_safe_terrain(StringName terrain_id)
    {
        StringName normalized = normalize_terrain_id(terrain_id);
        return normalized == TerrainLand || normalized == TerrainForest;
    }

    public static bool has_movement_tag(GArray movement_tags, StringName tag)
    {
        return movement_tags != null && movement_tags.Contains(tag);
    }
}
