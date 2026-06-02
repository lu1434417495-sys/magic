using Godot;

// 战斗边缘面运行时缓存。
// 翻译自 battle_edge_face_state.gd（2026-05-24，数据层 C# 迁移）。
[GlobalClass]
public partial class BattleEdgeFaceState : RefCounted
{
    private static readonly StringName _FEATURE_NONE = "none";
    private static readonly StringName _FEATURE_WALL = "wall";
    private static readonly StringName _RENDER_NONE = "none";
    private static readonly StringName _RENDER_WALL = "wall";

    public static StringName FEATURE_NONE() => _FEATURE_NONE;

    public static StringName FEATURE_WALL() => _FEATURE_WALL;

    public static StringName RENDER_NONE() => _RENDER_NONE;

    public static StringName RENDER_WALL() => _RENDER_WALL;

    public Vector2I origin_coord { get; set; } = Vector2I.Zero;
    public Vector2I neighbor_coord { get; set; } = Vector2I.Zero;
    public Vector2I direction { get; set; } = Vector2I.Right;
    public int from_height { get; set; }
    public int to_height { get; set; }
    public int height_difference { get; set; }
    public int drop_layers { get; set; }
    public Godot.Collections.Array<int> drop_face_layer_heights { get; set; } = new();
    public StringName feature_kind { get; set; } = _FEATURE_NONE;
    public StringName feature_render_kind { get; set; } = _RENDER_NONE;
    public int feature_layers { get; set; }
    public bool feature_blocks_move { get; set; }
    public bool feature_blocks_occupancy { get; set; }
    public bool feature_blocks_los { get; set; }
    public StringName feature_interaction_kind { get; set; } = "none";
    public StringName feature_state_tag { get; set; } = "";

    public bool has_drop_face()
    {
        return drop_layers > 0 || drop_face_layer_heights.Count > 0;
    }

    public bool has_feature_face()
    {
        return feature_kind != _FEATURE_NONE
            && feature_render_kind != _RENDER_NONE
            && feature_layers > 0;
    }

    public bool has_any_face()
    {
        return has_drop_face() || has_feature_face();
    }

    public bool blocks_move()
    {
        return feature_blocks_move;
    }

    public bool blocks_occupancy()
    {
        return feature_blocks_occupancy;
    }
}
