## 文件说明：该脚本属于战斗棋盘视觉配置，集中维护 terrain profile 到渲染 profile 与素材 source 规格的映射。
## 审查重点：重点核对视觉高度、点击面、镜头边距、TileSet source 规格与素材目录 fallback 是否保持稳定。
## 备注：该脚本只描述展示协议，不应参与战斗逻辑高度、移动规则或存档 schema。

class_name BattleBoardRenderProfile
extends RefCounted

const TERRAIN_PROFILE_DEFAULT := &"default"
const TERRAIN_PROFILE_CANYON := &"canyon"
const TERRAIN_PROFILE_NARROW_ASSAULT := &"narrow_assault"
const TERRAIN_PROFILE_HOLDOUT_PUSH := &"holdout_push"
const RENDER_PROFILE_CANYON_ISO64 := &"canyon_iso64"

const SOURCE_LAND := &"land"
const SOURCE_WATER := &"water"
const SOURCE_MUD := &"mud"
const SOURCE_EDGE_DROP_EAST := &"edge_drop_east"
const SOURCE_EDGE_DROP_SOUTH := &"edge_drop_south"
const SOURCE_WALL_EAST := &"wall_east"
const SOURCE_WALL_SOUTH := &"wall_south"
const SOURCE_SCRUB := &"scrub"
const SOURCE_RUBBLE := &"rubble"
const SOURCE_METEOR_CRATER := &"meteor_crater_core"
const SOURCE_METEOR_RUBBLE := &"meteor_rubble"
const SOURCE_METEOR_DUST := &"meteor_dust_cloud"
const SOURCE_SELECTED := &"selected"
const SOURCE_ACTIVE_SELECTED := &"active_selected"
const SOURCE_MOVE_REACHABLE := &"move_reachable"
const SOURCE_PREVIEW := &"preview"

const LAYER_ROLE_TOP := &"top"
const LAYER_ROLE_EDGE_EAST := &"edge_east"
const LAYER_ROLE_EDGE_SOUTH := &"edge_south"
const LAYER_ROLE_OVERLAY := &"overlay"
const LAYER_ROLE_MARKER := &"marker"

const DEFAULT_ASSET_DIR := "res://assets/main/battle/terrain/canyon"
const DEFAULT_BOARD_TILE_SIZE := Vector2i(64, 32)
const DEFAULT_TILE_HALF_SIZE := Vector2(32.0, 16.0)
const DEFAULT_VISUAL_HEIGHT_STEP := 20.0
const DEFAULT_CAMERA_MARGIN := Vector2(0.0, 96.0)
const DEFAULT_CONTENT_BOUNDS_MARGIN := Vector4(64.0, 104.0, 64.0, 144.0)
const DEFAULT_UNIT_ANCHOR_BIAS := Vector2(0.0, -10.0)
const DEFAULT_PROP_ANCHOR_BIAS := Vector2(0.0, -3.0)
const DEFAULT_FACE_REGION_SIZE := Vector2i(64, 36)

const TERRAIN_TO_RENDER_PROFILE := {
	TERRAIN_PROFILE_DEFAULT: RENDER_PROFILE_CANYON_ISO64,
	TERRAIN_PROFILE_CANYON: RENDER_PROFILE_CANYON_ISO64,
	TERRAIN_PROFILE_NARROW_ASSAULT: RENDER_PROFILE_CANYON_ISO64,
	TERRAIN_PROFILE_HOLDOUT_PUSH: RENDER_PROFILE_CANYON_ISO64,
}

const DEFAULT_SOURCE_SPECS := [
	{"key": SOURCE_LAND, "files": ["top_land_01.png", "top_land_02.png", "top_land_03.png"], "layer_role": LAYER_ROLE_TOP},
	{"key": SOURCE_WATER, "files": ["top_water_01.png", "top_water_02.png", "top_water_03.png"], "layer_role": LAYER_ROLE_TOP},
	{"key": SOURCE_MUD, "files": ["top_mud_01.png", "top_mud_02.png", "top_mud_03.png"], "layer_role": LAYER_ROLE_TOP},
	{"key": SOURCE_EDGE_DROP_EAST, "files": ["cliff_east_01.png", "cliff_east_02.png", "cliff_east_03.png"], "layer_role": LAYER_ROLE_EDGE_EAST, "atlas_region_size": DEFAULT_FACE_REGION_SIZE},
	{"key": SOURCE_EDGE_DROP_SOUTH, "files": ["cliff_south_01.png", "cliff_south_02.png", "cliff_south_03.png"], "layer_role": LAYER_ROLE_EDGE_SOUTH, "atlas_region_size": DEFAULT_FACE_REGION_SIZE},
	{"key": SOURCE_WALL_EAST, "files": ["wall_east_01.png", "wall_east_02.png", "wall_east_03.png"], "layer_role": LAYER_ROLE_EDGE_EAST, "atlas_region_size": DEFAULT_FACE_REGION_SIZE},
	{"key": SOURCE_WALL_SOUTH, "files": ["wall_south_01.png", "wall_south_02.png", "wall_south_03.png"], "layer_role": LAYER_ROLE_EDGE_SOUTH, "atlas_region_size": DEFAULT_FACE_REGION_SIZE},
	{"key": SOURCE_SCRUB, "files": ["overlay_scrub_01.png", "overlay_scrub_02.png", "overlay_scrub_03.png"], "layer_role": LAYER_ROLE_OVERLAY},
	{"key": SOURCE_RUBBLE, "files": ["overlay_rubble_01.png", "overlay_rubble_02.png", "overlay_rubble_03.png"], "layer_role": LAYER_ROLE_OVERLAY},
	{"key": SOURCE_METEOR_CRATER, "files": ["overlay_rubble_03.png"], "layer_role": LAYER_ROLE_OVERLAY},
	{"key": SOURCE_METEOR_RUBBLE, "files": ["overlay_rubble_02.png"], "layer_role": LAYER_ROLE_OVERLAY},
	{"key": SOURCE_METEOR_DUST, "files": ["overlay_scrub_03.png"], "layer_role": LAYER_ROLE_OVERLAY},
	{"key": SOURCE_SELECTED, "files": ["marker_selected.png"], "layer_role": LAYER_ROLE_MARKER},
	{"key": SOURCE_PREVIEW, "files": ["marker_preview.png"], "layer_role": LAYER_ROLE_MARKER},
]

var terrain_profile_id: StringName = TERRAIN_PROFILE_DEFAULT
var render_profile_id: StringName = RENDER_PROFILE_CANYON_ISO64
var asset_dir := DEFAULT_ASSET_DIR
var visual_height_step := DEFAULT_VISUAL_HEIGHT_STEP
var board_tile_size := DEFAULT_BOARD_TILE_SIZE
var tile_half_size := DEFAULT_TILE_HALF_SIZE
var surface_pick_shape: StringName = &"diamond"
var camera_margin := DEFAULT_CAMERA_MARGIN
var content_bounds_margin := DEFAULT_CONTENT_BOUNDS_MARGIN
var unit_anchor_bias := DEFAULT_UNIT_ANCHOR_BIAS
var prop_anchor_bias := DEFAULT_PROP_ANCHOR_BIAS
var source_specs: Array[Dictionary] = []


static func for_terrain_profile_id(raw_terrain_profile_id: StringName) -> BattleBoardRenderProfile:
	var terrain_id := normalize_terrain_profile_id(raw_terrain_profile_id)
	var render_id := resolve_render_profile_id(terrain_id)
	return _build_profile(terrain_id, render_id)


static func normalize_terrain_profile_id(raw_terrain_profile_id: StringName) -> StringName:
	match String(raw_terrain_profile_id).strip_edges().to_lower():
		"", "default":
			return TERRAIN_PROFILE_DEFAULT
		"canyon":
			return TERRAIN_PROFILE_CANYON
		"narrow_assault":
			return TERRAIN_PROFILE_NARROW_ASSAULT
		"holdout_push":
			return TERRAIN_PROFILE_HOLDOUT_PUSH
		_:
			return TERRAIN_PROFILE_DEFAULT


static func resolve_render_profile_id(terrain_id: StringName) -> StringName:
	var normalized_id := normalize_terrain_profile_id(terrain_id)
	return TERRAIN_TO_RENDER_PROFILE.get(normalized_id, RENDER_PROFILE_CANYON_ISO64)


func get_cache_key() -> StringName:
	return StringName("%s|%s" % [String(render_profile_id), asset_dir])


func get_source_specs() -> Array[Dictionary]:
	return source_specs.duplicate(true)


func get_primary_land_file() -> String:
	for spec in source_specs:
		if StringName(spec.get("key", &"")) == SOURCE_LAND:
			var files := spec.get("files", []) as Array
			if not files.is_empty():
				return String(files[0])
	return "top_land_01.png"


func get_selected_marker_file() -> String:
	for spec in source_specs:
		if StringName(spec.get("key", &"")) == SOURCE_SELECTED:
			var files := spec.get("files", []) as Array
			if not files.is_empty():
				return String(files[0])
	return "marker_selected.png"


func get_prop_anchor_bias(prop_id: StringName, side_sign: float) -> Vector2:
	match prop_id:
		&"tent":
			return Vector2(side_sign * 11.0, 0.0)
		&"torch":
			return Vector2(side_sign * 14.0, -2.0)
		&"objective_marker":
			return Vector2(0.0, -4.0)
		_:
			return prop_anchor_bias


func point_hits_top_surface(point: Vector2, anchor: Vector2) -> bool:
	var delta := point - anchor
	match surface_pick_shape:
		&"diamond":
			var normalized_x := absf(delta.x) / maxf(tile_half_size.x, 1.0)
			var normalized_y := absf(delta.y) / maxf(tile_half_size.y, 1.0)
			return normalized_x + normalized_y <= 1.0
		_:
			return Rect2(anchor - tile_half_size, tile_half_size * 2.0).has_point(point)


static func _build_profile(terrain_id: StringName, render_id: StringName) -> BattleBoardRenderProfile:
	var profile = new()
	profile.terrain_profile_id = normalize_terrain_profile_id(terrain_id)
	profile.render_profile_id = render_id if render_id != &"" else RENDER_PROFILE_CANYON_ISO64
	profile.asset_dir = DEFAULT_ASSET_DIR
	profile.visual_height_step = DEFAULT_VISUAL_HEIGHT_STEP
	profile.board_tile_size = DEFAULT_BOARD_TILE_SIZE
	profile.tile_half_size = DEFAULT_TILE_HALF_SIZE
	profile.surface_pick_shape = &"diamond"
	profile.camera_margin = DEFAULT_CAMERA_MARGIN
	profile.content_bounds_margin = DEFAULT_CONTENT_BOUNDS_MARGIN
	profile.unit_anchor_bias = DEFAULT_UNIT_ANCHOR_BIAS
	profile.prop_anchor_bias = DEFAULT_PROP_ANCHOR_BIAS
	profile.source_specs = _build_default_source_specs(profile)
	return profile


static func _build_default_source_specs(profile: BattleBoardRenderProfile) -> Array[Dictionary]:
	var normalized_specs: Array[Dictionary] = []
	for raw_spec in DEFAULT_SOURCE_SPECS:
		var source_spec := (raw_spec as Dictionary).duplicate(true)
		source_spec["atlas_region_size"] = source_spec.get("atlas_region_size", profile.board_tile_size)
		source_spec["board_tile_size"] = source_spec.get("board_tile_size", profile.board_tile_size)
		source_spec["texture_origin"] = source_spec.get("texture_origin", Vector2i.ZERO)
		source_spec["visual_origin"] = source_spec.get("visual_origin", source_spec.get("texture_origin", Vector2i.ZERO))
		source_spec["layer_role"] = StringName(source_spec.get("layer_role", LAYER_ROLE_TOP))
		source_spec["allow_generated_fallback"] = bool(source_spec.get("allow_generated_fallback", true))
		normalized_specs.append(source_spec)
	return normalized_specs
