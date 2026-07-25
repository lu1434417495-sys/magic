using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

internal enum BattleTerrainKind
{
    Unknown = 0,
    Land,
    Forest,
    Water,
    ShallowWater,
    FlowingWater,
    DeepWater,
    Ice,
    Mud,
    Spike,
}

public static class BattleTerrainRules
{
    private static readonly StringName TerrainLand = "land";
    private static readonly StringName TerrainForest = "forest";
    private static readonly StringName TerrainWater = "water";
    private static readonly StringName TerrainShallowWater = "shallow_water";
    private static readonly StringName TerrainFlowingWater = "flowing_water";
    private static readonly StringName TerrainDeepWater = "deep_water";
    private static readonly StringName TerrainIce = "ice";
    private static readonly StringName TerrainMud = "mud";
    private static readonly StringName TerrainSpike = "spike";

    private static readonly StringName TagWade = "wade";
    private static readonly StringName TagAmphibious = "amphibious";
    private static readonly StringName TagFly = "fly";

    internal static StringName ToStringName(BattleTerrainKind kind)
    {
        return kind switch
        {
            BattleTerrainKind.Land => TerrainLand,
            BattleTerrainKind.Forest => TerrainForest,
            BattleTerrainKind.Water => TerrainWater,
            BattleTerrainKind.ShallowWater => TerrainShallowWater,
            BattleTerrainKind.FlowingWater => TerrainFlowingWater,
            BattleTerrainKind.DeepWater => TerrainDeepWater,
            BattleTerrainKind.Ice => TerrainIce,
            BattleTerrainKind.Mud => TerrainMud,
            BattleTerrainKind.Spike => TerrainSpike,
            _ => new StringName(""),
        };
    }

    internal static BattleTerrainKind ToTerrainKind(StringName terrainId)
    {
        if (terrainId == null || terrainId == "" || terrainId == TerrainLand)
            return BattleTerrainKind.Land;
        if (terrainId == TerrainForest)
            return BattleTerrainKind.Forest;
        if (terrainId == TerrainWater)
            return BattleTerrainKind.Water;
        if (terrainId == TerrainShallowWater)
            return BattleTerrainKind.ShallowWater;
        if (terrainId == TerrainFlowingWater)
            return BattleTerrainKind.FlowingWater;
        if (terrainId == TerrainDeepWater)
            return BattleTerrainKind.DeepWater;
        if (terrainId == TerrainIce)
            return BattleTerrainKind.Ice;
        if (terrainId == TerrainMud)
            return BattleTerrainKind.Mud;
        if (terrainId == TerrainSpike)
            return BattleTerrainKind.Spike;
        return BattleTerrainKind.Unknown;
    }

    public static StringName NormalizeTerrainId(StringName terrain_id)
    {
        BattleTerrainKind kind = ToTerrainKind(terrain_id);
        return kind == BattleTerrainKind.Unknown ? new StringName("") : ToStringName(kind);
    }

    public static bool IsWaterTerrain(StringName terrain_id)
    {
        BattleTerrainKind kind = ToTerrainKind(terrain_id);
        return kind == BattleTerrainKind.Water
            || kind == BattleTerrainKind.ShallowWater
            || kind == BattleTerrainKind.FlowingWater
            || kind == BattleTerrainKind.DeepWater;
    }

    public static bool GetGlobalPassable(StringName terrain_id)
    {
        return ToTerrainKind(terrain_id) != BattleTerrainKind.DeepWater;
    }

    public static int GetBaseMoveCost(StringName terrain_id)
    {
        BattleTerrainKind kind = ToTerrainKind(terrain_id);
        if (
            kind == BattleTerrainKind.Mud
            || kind == BattleTerrainKind.Spike
            || kind == BattleTerrainKind.ShallowWater
        )
        {
            return 2;
        }
        if (kind == BattleTerrainKind.FlowingWater)
        {
            return 3;
        }
        if (kind == BattleTerrainKind.DeepWater)
        {
            return 2;
        }
        return 1;
    }

    public static bool CanUnitEnterTerrain(
        StringName terrain_id,
        GArray movement_tags = null
    )
    {
        if (HasMovementTag(movement_tags, TagFly))
        {
            return true;
        }
        return ToTerrainKind(terrain_id) != BattleTerrainKind.DeepWater
            || HasMovementTag(movement_tags, TagAmphibious);
    }

    public static bool CanUnitEnterTerrain(
        StringName terrain_id,
        IEnumerable<StringName> movement_tags
    )
    {
        if (HasMovementTag(movement_tags, TagFly))
        {
            return true;
        }
        return ToTerrainKind(terrain_id) != BattleTerrainKind.DeepWater
            || HasMovementTag(movement_tags, TagAmphibious);
    }

    internal static bool CanUnitEnterTerrain(
        StringName terrain_id,
        BattleMovementTagReadView movementTags
    )
    {
        if (movementTags.Contains(TagFly))
        {
            return true;
        }
        return ToTerrainKind(terrain_id) != BattleTerrainKind.DeepWater
            || movementTags.Contains(TagAmphibious);
    }

    internal static int GetUnitMoveCost(StringName terrain_id, GArray movement_tags = null)
    {
        BattleTerrainKind kind = ToTerrainKind(terrain_id);
        if (HasMovementTag(movement_tags, TagFly) && IsWaterTerrain(terrain_id))
        {
            return 1;
        }

        if (kind == BattleTerrainKind.ShallowWater)
        {
            if (
                HasMovementTag(movement_tags, TagAmphibious)
                || HasMovementTag(movement_tags, TagWade)
            )
            {
                return 1;
            }
            return 2;
        }
        if (kind == BattleTerrainKind.FlowingWater)
        {
            if (HasMovementTag(movement_tags, TagAmphibious))
            {
                return 1;
            }
            if (HasMovementTag(movement_tags, TagWade))
            {
                return 2;
            }
            return 3;
        }
        if (kind == BattleTerrainKind.DeepWater)
        {
            return HasMovementTag(movement_tags, TagAmphibious) ? 2 : 999999;
        }
        return GetBaseMoveCost(ToStringName(kind));
    }

    public static int GetUnitMoveCost(StringName terrain_id, IEnumerable<StringName> movement_tags)
    {
        BattleTerrainKind kind = ToTerrainKind(terrain_id);
        if (HasMovementTag(movement_tags, TagFly) && IsWaterTerrain(terrain_id))
        {
            return 1;
        }

        if (kind == BattleTerrainKind.ShallowWater)
        {
            if (
                HasMovementTag(movement_tags, TagAmphibious)
                || HasMovementTag(movement_tags, TagWade)
            )
            {
                return 1;
            }
            return 2;
        }
        if (kind == BattleTerrainKind.FlowingWater)
        {
            if (HasMovementTag(movement_tags, TagAmphibious))
            {
                return 1;
            }
            if (HasMovementTag(movement_tags, TagWade))
            {
                return 2;
            }
            return 3;
        }
        if (kind == BattleTerrainKind.DeepWater)
        {
            return HasMovementTag(movement_tags, TagAmphibious) ? 2 : 999999;
        }
        return GetBaseMoveCost(ToStringName(kind));
    }

    internal static int GetUnitMoveCost(
        StringName terrain_id,
        BattleMovementTagReadView movementTags
    )
    {
        BattleTerrainKind kind = ToTerrainKind(terrain_id);
        if (movementTags.Contains(TagFly) && IsWaterTerrain(terrain_id))
        {
            return 1;
        }

        if (kind == BattleTerrainKind.ShallowWater)
        {
            if (
                movementTags.Contains(TagAmphibious)
                || movementTags.Contains(TagWade)
            )
            {
                return 1;
            }
            return 2;
        }
        if (kind == BattleTerrainKind.FlowingWater)
        {
            if (movementTags.Contains(TagAmphibious))
            {
                return 1;
            }
            if (movementTags.Contains(TagWade))
            {
                return 2;
            }
            return 3;
        }
        if (kind == BattleTerrainKind.DeepWater)
        {
            return movementTags.Contains(TagAmphibious) ? 2 : 999999;
        }
        return GetBaseMoveCost(ToStringName(kind));
    }

    public static string GetDisplayName(StringName terrain_id)
    {
        BattleTerrainKind kind = ToTerrainKind(terrain_id);
        if (kind == BattleTerrainKind.Land)
        {
            return "陆地";
        }
        if (kind == BattleTerrainKind.Forest)
        {
            return "森林";
        }
        if (kind == BattleTerrainKind.ShallowWater)
        {
            return "浅水";
        }
        if (kind == BattleTerrainKind.FlowingWater)
        {
            return "流水";
        }
        if (kind == BattleTerrainKind.DeepWater)
        {
            return "深水";
        }
        if (kind == BattleTerrainKind.Ice)
        {
            return "冰层";
        }
        if (kind == BattleTerrainKind.Mud)
        {
            return "泥沼";
        }
        if (kind == BattleTerrainKind.Spike)
        {
            return "地刺";
        }
        return terrain_id?.ToString() ?? "";
    }

    public static bool CanHostTent(StringName terrain_id)
    {
        BattleTerrainKind kind = ToTerrainKind(terrain_id);
        return kind == BattleTerrainKind.Land || kind == BattleTerrainKind.Forest;
    }

    public static bool CanHostTorch(StringName terrain_id)
    {
        BattleTerrainKind kind = ToTerrainKind(terrain_id);
        return !IsWaterTerrain(terrain_id) && kind != BattleTerrainKind.Spike;
    }

    public static bool IsSafeTerrain(StringName terrain_id)
    {
        BattleTerrainKind kind = ToTerrainKind(terrain_id);
        return kind == BattleTerrainKind.Land || kind == BattleTerrainKind.Forest;
    }

    private static bool HasMovementTag(GArray movement_tags, StringName tag)
    {
        return movement_tags != null && movement_tags.Contains(tag);
    }

    public static bool HasMovementTag(IEnumerable<StringName> movement_tags, StringName tag)
    {
        if (movement_tags == null)
            return false;
        foreach (StringName movementTag in movement_tags)
        {
            if (movementTag == tag)
                return true;
        }
        return false;
    }
}
