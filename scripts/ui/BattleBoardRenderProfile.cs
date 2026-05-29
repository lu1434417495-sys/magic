using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;

[GlobalClass]
public partial class BattleBoardRenderProfile : RefCounted
{
    private static readonly StringName TerrainProfileDefault = "default";
    private static readonly StringName TerrainProfileCanyon = "canyon";
    private static readonly StringName TerrainProfileNarrowAssault = "narrow_assault";
    private static readonly StringName TerrainProfileHoldoutPush = "holdout_push";
    private static readonly StringName RenderProfileCanyonIso64 = "canyon_iso64";

    private static readonly StringName SourceLand = "land";
    private static readonly StringName SourceWater = "water";
    private static readonly StringName SourceMud = "mud";
    private static readonly StringName SourceEdgeDropEast = "edge_drop_east";
    private static readonly StringName SourceEdgeDropSouth = "edge_drop_south";
    private static readonly StringName SourceWallEast = "wall_east";
    private static readonly StringName SourceWallSouth = "wall_south";
    private static readonly StringName SourceScrub = "scrub";
    private static readonly StringName SourceRubble = "rubble";
    private static readonly StringName SourceMeteorCrater = "meteor_crater_core";
    private static readonly StringName SourceMeteorRubble = "meteor_rubble";
    private static readonly StringName SourceMeteorDust = "meteor_dust_cloud";
    private static readonly StringName SourceSelected = "selected";
    private static readonly StringName SourceActiveSelected = "active_selected";
    private static readonly StringName SourceMoveReachable = "move_reachable";
    private static readonly StringName SourcePreview = "preview";

    private static readonly StringName LayerRoleTop = "top";
    private static readonly StringName LayerRoleEdgeEast = "edge_east";
    private static readonly StringName LayerRoleEdgeSouth = "edge_south";
    private static readonly StringName LayerRoleOverlay = "overlay";
    private static readonly StringName LayerRoleMarker = "marker";

    private const string DefaultAssetDir = "res://assets/main/battle/terrain/canyon";
    private static readonly Vector2I DefaultBoardTileSize = new(64, 32);
    private static readonly Vector2 DefaultTileHalfSize = new(32.0f, 16.0f);
    private const float DefaultVisualHeightStep = 20.0f;
    private static readonly Vector2 DefaultCameraMargin = new(0.0f, 96.0f);
    private static readonly Vector4 DefaultContentBoundsMargin = new(64.0f, 104.0f, 64.0f, 144.0f);
    private static readonly Vector2 DefaultUnitAnchorBias = new(0.0f, -10.0f);
    private static readonly Vector2 DefaultPropAnchorBias = new(0.0f, -3.0f);
    private static readonly Vector2I DefaultFaceRegionSize = new(64, 36);

    public static StringName TERRAIN_PROFILE_DEFAULT() => TerrainProfileDefault;

    public static StringName TERRAIN_PROFILE_CANYON() => TerrainProfileCanyon;

    public static StringName TERRAIN_PROFILE_NARROW_ASSAULT() => TerrainProfileNarrowAssault;

    public static StringName TERRAIN_PROFILE_HOLDOUT_PUSH() => TerrainProfileHoldoutPush;

    public static StringName RENDER_PROFILE_CANYON_ISO64() => RenderProfileCanyonIso64;

    public static StringName SOURCE_LAND() => SourceLand;

    public static StringName SOURCE_WATER() => SourceWater;

    public static StringName SOURCE_MUD() => SourceMud;

    public static StringName SOURCE_EDGE_DROP_EAST() => SourceEdgeDropEast;

    public static StringName SOURCE_EDGE_DROP_SOUTH() => SourceEdgeDropSouth;

    public static StringName SOURCE_WALL_EAST() => SourceWallEast;

    public static StringName SOURCE_WALL_SOUTH() => SourceWallSouth;

    public static StringName SOURCE_SCRUB() => SourceScrub;

    public static StringName SOURCE_RUBBLE() => SourceRubble;

    public static StringName SOURCE_METEOR_CRATER() => SourceMeteorCrater;

    public static StringName SOURCE_METEOR_RUBBLE() => SourceMeteorRubble;

    public static StringName SOURCE_METEOR_DUST() => SourceMeteorDust;

    public static StringName SOURCE_SELECTED() => SourceSelected;

    public static StringName SOURCE_ACTIVE_SELECTED() => SourceActiveSelected;

    public static StringName SOURCE_MOVE_REACHABLE() => SourceMoveReachable;

    public static StringName SOURCE_PREVIEW() => SourcePreview;

    public static StringName LAYER_ROLE_TOP() => LayerRoleTop;

    public static StringName LAYER_ROLE_EDGE_EAST() => LayerRoleEdgeEast;

    public static StringName LAYER_ROLE_EDGE_SOUTH() => LayerRoleEdgeSouth;

    public static StringName LAYER_ROLE_OVERLAY() => LayerRoleOverlay;

    public static StringName LAYER_ROLE_MARKER() => LayerRoleMarker;

    public static string DEFAULT_ASSET_DIR() => DefaultAssetDir;

    public static Vector2I DEFAULT_BOARD_TILE_SIZE() => DefaultBoardTileSize;

    public static Vector2 DEFAULT_TILE_HALF_SIZE() => DefaultTileHalfSize;

    public static float DEFAULT_VISUAL_HEIGHT_STEP() => DefaultVisualHeightStep;

    public static Vector2 DEFAULT_CAMERA_MARGIN() => DefaultCameraMargin;

    public static Vector4 DEFAULT_CONTENT_BOUNDS_MARGIN() => DefaultContentBoundsMargin;

    public static Vector2 DEFAULT_UNIT_ANCHOR_BIAS() => DefaultUnitAnchorBias;

    public static Vector2 DEFAULT_PROP_ANCHOR_BIAS() => DefaultPropAnchorBias;

    public static Vector2I DEFAULT_FACE_REGION_SIZE() => DefaultFaceRegionSize;

    public StringName terrain_profile_id = TerrainProfileDefault;
    public StringName render_profile_id = RenderProfileCanyonIso64;
    public string asset_dir = DefaultAssetDir;
    public float visual_height_step = DefaultVisualHeightStep;
    public Vector2I board_tile_size = DefaultBoardTileSize;
    public Vector2 tile_half_size = DefaultTileHalfSize;
    public StringName surface_pick_shape = "diamond";
    public Vector2 camera_margin = DefaultCameraMargin;
    public Vector4 content_bounds_margin = DefaultContentBoundsMargin;
    public Vector2 unit_anchor_bias = DefaultUnitAnchorBias;
    public Vector2 prop_anchor_bias = DefaultPropAnchorBias;
    public GDictionaryArray source_specs = new();

    public static BattleBoardRenderProfile for_terrain_profile_id(StringName raw_terrain_profile_id)
    {
        StringName terrainId = normalize_terrain_profile_id(raw_terrain_profile_id);
        StringName renderId = resolve_render_profile_id(terrainId);
        return _build_profile(terrainId, renderId);
    }

    public static StringName normalize_terrain_profile_id(StringName raw_terrain_profile_id)
    {
        string normalized = raw_terrain_profile_id.ToString().StripEdges().ToLower(System.Globalization.CultureInfo.GetCultureInfo(""));
        return normalized switch
        {
            "" or "default" => TerrainProfileDefault,
            "canyon" => TerrainProfileCanyon,
            "narrow_assault" => TerrainProfileNarrowAssault,
            "holdout_push" => TerrainProfileHoldoutPush,
            _ => TerrainProfileDefault,
        };
    }

    public static StringName resolve_render_profile_id(StringName terrain_id)
    {
        StringName normalizedId = normalize_terrain_profile_id(terrain_id);
        if (
            normalizedId == TerrainProfileDefault
            || normalizedId == TerrainProfileCanyon
            || normalizedId == TerrainProfileNarrowAssault
            || normalizedId == TerrainProfileHoldoutPush
        )
        {
            return RenderProfileCanyonIso64;
        }
        return RenderProfileCanyonIso64;
    }

    public StringName get_cache_key()
    {
        return new StringName($"{render_profile_id}|{asset_dir}");
    }

    public GDictionaryArray get_source_specs()
    {
        return (GDictionaryArray)source_specs.Duplicate(true);
    }

    public string get_primary_land_file()
    {
        return GetFirstSourceFile(SourceLand, "top_land_01.png");
    }

    public string get_selected_marker_file()
    {
        return GetFirstSourceFile(SourceSelected, "marker_selected.png");
    }

    public Vector2 get_prop_anchor_bias(StringName prop_id, float side_sign)
    {
        if (prop_id == "tent")
            return new Vector2(side_sign * 11.0f, 0.0f);
        if (prop_id == "torch")
            return new Vector2(side_sign * 14.0f, -2.0f);
        if (prop_id == "objective_marker")
            return new Vector2(0.0f, -4.0f);
        return prop_anchor_bias;
    }

    public bool point_hits_top_surface(Vector2 point, Vector2 anchor)
    {
        Vector2 delta = point - anchor;
        if (surface_pick_shape == "diamond")
        {
            float normalizedX = Mathf.Abs(delta.X) / Mathf.Max(tile_half_size.X, 1.0f);
            float normalizedY = Mathf.Abs(delta.Y) / Mathf.Max(tile_half_size.Y, 1.0f);
            return normalizedX + normalizedY <= 1.0f;
        }
        return new Rect2(anchor - tile_half_size, tile_half_size * 2.0f).HasPoint(point);
    }

    private string GetFirstSourceFile(StringName sourceKey, string fallback)
    {
        foreach (GDictionary spec in source_specs)
        {
            if (GdInterop.GetStringName(spec, "key") != sourceKey)
                continue;

            GArray files = GdInterop.GetArray(spec, "files");
            if (files.Count > 0)
                return files[0].AsString();
        }
        return fallback;
    }

    private static BattleBoardRenderProfile _build_profile(
        StringName terrainId,
        StringName renderId
    )
    {
        var profile = new BattleBoardRenderProfile
        {
            terrain_profile_id = normalize_terrain_profile_id(terrainId),
            render_profile_id = renderId != "" ? renderId : RenderProfileCanyonIso64,
            asset_dir = DefaultAssetDir,
            visual_height_step = DefaultVisualHeightStep,
            board_tile_size = DefaultBoardTileSize,
            tile_half_size = DefaultTileHalfSize,
            surface_pick_shape = "diamond",
            camera_margin = DefaultCameraMargin,
            content_bounds_margin = DefaultContentBoundsMargin,
            unit_anchor_bias = DefaultUnitAnchorBias,
            prop_anchor_bias = DefaultPropAnchorBias,
        };
        profile.source_specs = _build_default_source_specs(profile);
        return profile;
    }

    private static GDictionaryArray _build_default_source_specs(BattleBoardRenderProfile profile)
    {
        var normalizedSpecs = new GDictionaryArray
        {
            BuildSourceSpec(
                SourceLand,
                new[] { "top_land_01.png", "top_land_02.png", "top_land_03.png" },
                LayerRoleTop,
                profile
            ),
            BuildSourceSpec(
                SourceWater,
                new[] { "top_water_01.png", "top_water_02.png", "top_water_03.png" },
                LayerRoleTop,
                profile
            ),
            BuildSourceSpec(
                SourceMud,
                new[] { "top_mud_01.png", "top_mud_02.png", "top_mud_03.png" },
                LayerRoleTop,
                profile
            ),
            BuildSourceSpec(
                SourceEdgeDropEast,
                new[] { "cliff_east_01.png", "cliff_east_02.png", "cliff_east_03.png" },
                LayerRoleEdgeEast,
                profile,
                DefaultFaceRegionSize
            ),
            BuildSourceSpec(
                SourceEdgeDropSouth,
                new[] { "cliff_south_01.png", "cliff_south_02.png", "cliff_south_03.png" },
                LayerRoleEdgeSouth,
                profile,
                DefaultFaceRegionSize
            ),
            BuildSourceSpec(
                SourceWallEast,
                new[] { "wall_east_01.png", "wall_east_02.png", "wall_east_03.png" },
                LayerRoleEdgeEast,
                profile,
                DefaultFaceRegionSize
            ),
            BuildSourceSpec(
                SourceWallSouth,
                new[] { "wall_south_01.png", "wall_south_02.png", "wall_south_03.png" },
                LayerRoleEdgeSouth,
                profile,
                DefaultFaceRegionSize
            ),
            BuildSourceSpec(
                SourceScrub,
                new[] { "overlay_scrub_01.png", "overlay_scrub_02.png", "overlay_scrub_03.png" },
                LayerRoleOverlay,
                profile
            ),
            BuildSourceSpec(
                SourceRubble,
                new[] { "overlay_rubble_01.png", "overlay_rubble_02.png", "overlay_rubble_03.png" },
                LayerRoleOverlay,
                profile
            ),
            BuildSourceSpec(
                SourceMeteorCrater,
                new[] { "overlay_rubble_03.png" },
                LayerRoleOverlay,
                profile
            ),
            BuildSourceSpec(
                SourceMeteorRubble,
                new[] { "overlay_rubble_02.png" },
                LayerRoleOverlay,
                profile
            ),
            BuildSourceSpec(
                SourceMeteorDust,
                new[] { "overlay_scrub_03.png" },
                LayerRoleOverlay,
                profile
            ),
            BuildSourceSpec(
                SourceSelected,
                new[] { "marker_selected.png" },
                LayerRoleMarker,
                profile
            ),
            BuildSourceSpec(
                SourcePreview,
                new[] { "marker_preview.png" },
                LayerRoleMarker,
                profile
            ),
        };
        return normalizedSpecs;
    }

    private static GDictionary BuildSourceSpec(
        StringName key,
        string[] files,
        StringName layerRole,
        BattleBoardRenderProfile profile,
        Vector2I? atlasRegionSize = null
    )
    {
        var fileArray = new GArray();
        foreach (string file in files)
            fileArray.Add(file);

        return new GDictionary
        {
            ["key"] = key,
            ["files"] = fileArray,
            ["layer_role"] = layerRole,
            ["atlas_region_size"] = atlasRegionSize ?? profile.board_tile_size,
            ["board_tile_size"] = profile.board_tile_size,
            ["texture_origin"] = Vector2I.Zero,
            ["visual_origin"] = Vector2I.Zero,
            ["allow_generated_fallback"] = true,
        };
    }
}
