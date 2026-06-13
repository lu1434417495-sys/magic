using Godot;

// 战斗边缘面运行时缓存。
// 翻译自 battle_edge_face_state.gd（2026-05-24，数据层 C# 迁移）。
public partial class BattleEdgeFaceState : RefCounted
{
    public Vector2I origin_coord { get; set; } = Vector2I.Zero;
    public Vector2I neighbor_coord { get; set; } = Vector2I.Zero;
    public Vector2I direction { get; set; } = Vector2I.Right;
    public int from_height { get; set; }
    public int to_height { get; set; }
    public int height_difference { get; set; }
    public int drop_layers { get; set; }
    public Godot.Collections.Array<int> drop_face_layer_heights { get; set; } = new();
    public StringName feature_kind { get; set; } =
        BattleEdgeFeatureState.ToStringName(BattleEdgeFeatureKind.None);
    public StringName feature_render_kind { get; set; } =
        BattleEdgeFeatureState.ToStringName(BattleEdgeRenderKind.None);
    public int feature_layers { get; set; }
    public bool feature_blocks_move { get; set; }
    public bool feature_blocks_occupancy { get; set; }
    public bool feature_blocks_los { get; set; }
    public StringName feature_interaction_kind { get; set; } =
        BattleEdgeFeatureState.ToStringName(BattleEdgeInteractionKind.None);
    public StringName feature_state_tag { get; set; } = "";

    internal BattleEdgeFeatureKind FeatureKind =>
        BattleEdgeFeatureState.ToFeatureKind(feature_kind);

    internal BattleEdgeRenderKind FeatureRenderKind =>
        BattleEdgeFeatureState.ToRenderKind(feature_render_kind);

    public bool HasDropFace()
    {
        return drop_layers > 0 || drop_face_layer_heights.Count > 0;
    }

    public bool HasFeatureFace()
    {
        return FeatureKind != BattleEdgeFeatureKind.None
            && FeatureRenderKind != BattleEdgeRenderKind.None
            && feature_layers > 0;
    }

    public bool HasAnyFace()
    {
        return HasDropFace() || HasFeatureFace();
    }

    public bool BlocksMove()
    {
        return feature_blocks_move;
    }

    public bool BlocksOccupancy()
    {
        return feature_blocks_occupancy;
    }
}
