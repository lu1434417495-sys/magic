## 文件说明：该脚本属于战斗棋盘回归执行相关的回归测试脚本，集中维护失败信息、网格服务等顶层字段。
## 审查重点：重点核对测试数据、字段用途、断言条件和失败提示是否仍然覆盖目标回归场景。
## 备注：后续如果业务规则变化，需要同步更新测试夹具、预期结果和失败信息。

extends SceneTree

const BattleBoard2D = preload("res://scripts/ui/battle_board_2d.gd")
const BattleBoardScene = preload("res://scenes/ui/battle_board_2d.tscn")
const BattleBoardPropCatalog = preload("res://scripts/utils/battle_board_prop_catalog.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleEdgeFeatureState = preload("res://scripts/systems/battle/core/battle_edge_feature_state.gd")
const BattleEdgeService = preload("res://scripts/systems/battle/terrain/battle_edge_service.gd")
const BattleGridService = preload("res://scripts/systems/battle/terrain/battle_grid_service.gd")
const BattleBoardRenderProfile = preload("res://scripts/ui/battle_board_render_profile.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTerrainRules = preload("res://scripts/systems/battle/terrain/battle_terrain_rules.gd")
const BattleTerrainGenerator = preload("res://scripts/systems/battle/terrain/battle_terrain_generator.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const EDGE_DROP_EAST_TEXTURE_PATHS: Array[String] = [
	"res://assets/main/battle/terrain/canyon/cliff_east_01.png",
	"res://assets/main/battle/terrain/canyon/cliff_east_02.png",
	"res://assets/main/battle/terrain/canyon/cliff_east_03.png",
]
const EDGE_DROP_SOUTH_TEXTURE_PATHS: Array[String] = [
	"res://assets/main/battle/terrain/canyon/cliff_south_01.png",
	"res://assets/main/battle/terrain/canyon/cliff_south_02.png",
	"res://assets/main/battle/terrain/canyon/cliff_south_03.png",
]
const WALL_EAST_TEXTURE_PATHS: Array[String] = [
	"res://assets/main/battle/terrain/canyon/wall_east_01.png",
	"res://assets/main/battle/terrain/canyon/wall_east_02.png",
	"res://assets/main/battle/terrain/canyon/wall_east_03.png",
]
const WALL_SOUTH_TEXTURE_PATHS: Array[String] = [
	"res://assets/main/battle/terrain/canyon/wall_south_01.png",
	"res://assets/main/battle/terrain/canyon/wall_south_02.png",
	"res://assets/main/battle/terrain/canyon/wall_south_03.png",
]
const TOP_LAND_TEXTURE_PATH := "res://assets/main/battle/terrain/canyon/top_land_01.png"

const VIEWPORT_SIZE := Vector2(1280.0, 720.0)
const ULTRAWIDE_VIEWPORT_SIZE := Vector2(3632.0, 510.0)
const TEST_MAP_SIZE := Vector2i(19, 11)
const TEST_WORLD_COORD := Vector2i(7, 11)
const TEST_SEED := 424242
const MAX_RENDER_HEIGHT := 8
const CANYON_MIN_HEIGHT := 4
const GLOBAL_MIN_HEIGHT := 4

## 字段说明：记录测试过程中收集到的失败信息，便于最终集中输出并快速定位回归点。
var _failures: Array[String] = []
## 字段说明：记录网格服务，用于构造测试场景、记录结果并支撑回归断言。
var _grid_service := BattleGridService.new()
## 字段说明：记录边缘面服务，用于统一验证渲染层与规则层读取同一套 edge cache。
var _edge_service := BattleEdgeService.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_canyon_generation_is_deterministic()
	await _test_canyon_generation_builds_true_stacked_columns()
	await _test_canyon_generation_contains_connected_water()
	await _test_narrow_assault_generation_builds_breakthrough_lane()
	await _test_narrow_assault_board_contracts()
	await _test_holdout_push_generation_builds_defender_holdout()
	await _test_holdout_push_board_contracts()
	await _test_default_generation_respects_global_min_height()
	await _test_default_water_height_normalization_is_component_local()
	await _test_generated_spawn_coords_never_use_water_tiles()
	await _test_render_profile_chain_and_source_specs_have_stable_fallbacks()
	await _test_canyon_face_source_specs_use_tall_region()
	await _test_canyon_two_layer_visual_separation_uses_20_step()
	await _test_canyon_layer_offsets_follow_render_profile_after_bind()
	await _test_battle_board_contracts()
	await _test_raised_top_surface_click_maps_to_visual_cell()
	await _test_visual_pick_prefers_visible_higher_top_when_surfaces_overlap()
	await _test_board_initial_camera_fills_ultrawide_width()
	await _test_board_content_bounds_follow_render_profile_for_viewport_sizes()
	await _test_east_face_assets_anchor_to_neighbor_side()
	await _test_south_face_assets_anchor_to_neighbor_side()
	await _test_edge_feature_authoring_roundtrips()
	await _test_wall_faces_render_from_edge_features()
	await _test_flat_plateau_renders_east_boundary_outside_land_cells()
	await _test_flat_plateau_renders_south_boundary_outside_land_cells()
	await _test_board_layer_draw_order_is_explicit()
	await _test_active_unit_marker_uses_opaque_land_cover_blue()
	await _test_skill_valid_target_highlight_renders_above_units()
	await _test_unit_tokens_render_hp_bars_with_numeric_labels()
	await _test_unit_render_depth_uses_positive_height_bias()
	await _test_dynamic_depth_interleaves_with_high_cliff_faces()
	await _test_large_unit_footprint_respects_edge_barriers()
	await _test_large_unit_partial_edge_barriers_block_all_directions()
	await _test_large_unit_partial_height_barriers_block_all_directions()
	await _test_raised_drop_faces_follow_absolute_height_layers()
	if _failures.is_empty():
		print("Battle board regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle board regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_canyon_generation_is_deterministic() -> void:
	var first_layout := _build_canyon_layout(TEST_SEED)
	var second_layout := _build_canyon_layout(TEST_SEED)
	_assert_true(
		_capture_layout_signature(first_layout) == _capture_layout_signature(second_layout),
		"同 seed 的 canyon 生成结果应保持稳定。"
	)
	_assert_eq(
		_count_layout_prop(first_layout, BattleBoardPropCatalog.PROP_OBJECTIVE_MARKER),
		1,
		"canyon 地图应恰好生成一个 objective marker。"
	)
	_assert_true(
		_count_layout_prop(first_layout, BattleBoardPropCatalog.PROP_TENT) >= 2,
		"canyon 地图应为双方营地各生成一个 tent。"
	)
	_assert_true(
		_count_layout_prop(first_layout, BattleBoardPropCatalog.PROP_TORCH) >= 2,
		"canyon 地图应至少生成两处 torch。"
	)
	_assert_canyon_height_range(first_layout)


func _test_canyon_generation_builds_true_stacked_columns() -> void:
	var layout := _build_canyon_layout(TEST_SEED)
	var columns_variant: Variant = layout.get("cell_columns", {})
	_assert_true(columns_variant is Dictionary, "canyon 生成结果应包含真实堆叠列数据 cell_columns。")
	if columns_variant is not Dictionary:
		return
	var columns: Dictionary = columns_variant
	var found_multi_layer_column := false
	for coord_variant in columns.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		var column_variant: Variant = columns.get(coord, [])
		if column_variant is not Array:
			continue
		var column := column_variant as Array
		var surface_cell := layout.get("cells", {}).get(coord) as BattleCellState
		if surface_cell == null:
			continue
		var expected_stack_size := maxi(int(surface_cell.current_height), 0) + 1
		_assert_eq(
			column.size(),
			expected_stack_size,
			"同一 (x, y) 应保存真实堆叠 cell 列，层数应与顶层高度一致：%s" % str(coord)
		)
		if column.size() > 1:
			found_multi_layer_column = true
	_assert_true(found_multi_layer_column, "canyon 地图应至少生成一列真实多层 cell。")


func _test_canyon_generation_contains_connected_water() -> void:
	for seed in [TEST_SEED, TEST_SEED + 17, TEST_SEED + 29]:
		var layout := _build_canyon_layout(seed)
		var water_coords := _collect_terrain_coords(layout, BattleCellState.TERRAIN_WATER)
		_assert_true(
			water_coords.size() >= 4,
			"canyon 地图应稳定生成可见水域，不能退化回零水域：seed=%d" % seed
		)
		if not water_coords.is_empty():
			_assert_eq(
				_count_connected_components(water_coords),
				1,
				"canyon 水域应保持空间连通，不能退化成随机散点：seed=%d" % seed
			)


func _test_narrow_assault_generation_builds_breakthrough_lane() -> void:
	var first_layout := _build_narrow_assault_layout(TEST_SEED)
	var second_layout := _build_narrow_assault_layout(TEST_SEED)
	_assert_true(
		_capture_layout_signature(first_layout) == _capture_layout_signature(second_layout),
		"同 seed 的 narrow_assault 生成结果应保持稳定。"
	)
	_assert_eq(
		String(first_layout.get("terrain_profile_id", "")),
		"narrow_assault",
		"narrow_assault 地图应回写正式 terrain_profile_id。"
	)
	var gate_info := _find_narrow_assault_gate_info(first_layout)
	_assert_true(not gate_info.is_empty(), "narrow_assault 地图应在中线附近形成一个可识别的突破口。")
	if gate_info.is_empty():
		return

	var map_size: Vector2i = first_layout.get("map_size", Vector2i.ZERO)
	var gate_x := int(gate_info.get("gate_x", -1))
	var opening_count := int(gate_info.get("opening_count", 0))
	var player_coord: Vector2i = first_layout.get("player_coord", Vector2i.ZERO)
	var enemy_coord: Vector2i = first_layout.get("enemy_coord", Vector2i.ZERO)
	_assert_true(opening_count >= 1 and opening_count <= 2, "narrow_assault 的突破口应保持 1-2 格宽，而不是退化成开阔平推。")
	_assert_true(absi(gate_x - int(map_size.x / 2)) <= 2, "narrow_assault 的突破口应位于战场中段附近，形成明确攻坚线。")
	_assert_true(player_coord.x <= gate_x and enemy_coord.x > gate_x, "narrow_assault 的出生点应分列突破口两侧。")
	_assert_true(int(gate_info.get("left_reachable_count", 0)) >= 10, "突破口左侧应保留足够 staging 区域供进攻方展开。")
	_assert_true(int(gate_info.get("right_reachable_count", 0)) >= 10, "突破口右侧应保留足够 staging 区域供防守方站位。")

	var objective_coords := _collect_layout_prop_coords(first_layout, BattleBoardPropCatalog.PROP_OBJECTIVE_MARKER)
	_assert_eq(objective_coords.size(), 1, "narrow_assault 地图应恰好生成一个突破目标点。")
	if objective_coords.size() == 1:
		_assert_true(absi(objective_coords[0].x - gate_x) <= 1, "突破目标点应贴近狭道中线，而不是刷到远端 staging 区。")

	_assert_true(
		_count_terrain_cells_in_x_range(first_layout, BattleCellState.TERRAIN_MUD, gate_x - 2, gate_x - 1) >= 2,
		"突破口前方应保留泥地区，形成进攻减速带。"
	)
	_assert_true(
		_count_terrain_cells_in_x_range(first_layout, BattleCellState.TERRAIN_SPIKE, gate_x + 1, gate_x + 2) >= 1,
		"突破口后方应保留地刺 kill-zone，体现防守反突意图。"
	)

	var ally_spawns := _extract_layout_coords(first_layout.get("ally_spawns", []))
	var enemy_spawns := _extract_layout_coords(first_layout.get("enemy_spawns", []))
	_assert_true(ally_spawns.size() >= 2, "narrow_assault 的 ally_spawns 应至少保留起点外的额外部署位。")
	_assert_true(enemy_spawns.size() >= 2, "narrow_assault 的 enemy_spawns 应至少保留起点外的额外部署位。")
	for coord in ally_spawns:
		_assert_true(coord.x <= gate_x, "narrow_assault 的 ally_spawns 应全部落在突破口左侧。")
	for coord in enemy_spawns:
		_assert_true(coord.x > gate_x, "narrow_assault 的 enemy_spawns 应全部落在突破口右侧。")
	for coord in ally_spawns:
		_assert_true(not enemy_spawns.has(coord), "narrow_assault 的双方部署位不应重叠：%s" % str(coord))

	var tent_coords := _collect_layout_prop_coords(first_layout, BattleBoardPropCatalog.PROP_TENT)
	var torch_coords := _collect_layout_prop_coords(first_layout, BattleBoardPropCatalog.PROP_TORCH)
	_assert_eq(tent_coords.size(), 2, "narrow_assault 地图应显式放置双方 tent。")
	_assert_eq(torch_coords.size(), 2, "narrow_assault 地图应显式放置左右 torch。")
	_assert_eq(_count_coords_on_or_left_of_x(tent_coords, gate_x), 1, "narrow_assault 的 tent 应在突破口左侧保留一处营地。")
	_assert_eq(_count_coords_strictly_right_of_x(tent_coords, gate_x), 1, "narrow_assault 的 tent 应在突破口右侧保留一处营地。")
	_assert_eq(_count_coords_on_or_left_of_x(torch_coords, gate_x), 1, "narrow_assault 的 torch 应在突破口左侧保留一处灯火。")
	_assert_eq(_count_coords_strictly_right_of_x(torch_coords, gate_x), 1, "narrow_assault 的 torch 应在突破口右侧保留一处灯火。")
	var explicit_prop_coords: Array[Vector2i] = []
	explicit_prop_coords.append_array(objective_coords)
	explicit_prop_coords.append_array(tent_coords)
	explicit_prop_coords.append_array(torch_coords)
	for coord in explicit_prop_coords:
		var cell := first_layout.get("cells", {}).get(coord) as BattleCellState
		_assert_true(cell != null and cell.passable, "narrow_assault 的显式 prop 必须放在可通行地格上：%s" % str(coord))
		_assert_true(
			not ally_spawns.has(coord) and not enemy_spawns.has(coord),
			"narrow_assault 的显式 prop 不应覆盖部署位：%s" % str(coord)
		)
	_assert_layout_uses_supported_props(first_layout)


func _test_narrow_assault_board_contracts() -> void:
	var layout := _build_narrow_assault_layout(TEST_SEED)
	var board := await _instantiate_board(_build_state(layout))
	_assert_prop_and_unit_sorting(board)


func _test_holdout_push_generation_builds_defender_holdout() -> void:
	var first_layout := _build_holdout_push_layout(TEST_SEED)
	var second_layout := _build_holdout_push_layout(TEST_SEED)
	_assert_true(
		_capture_layout_signature(first_layout) == _capture_layout_signature(second_layout),
		"同 seed 的 holdout_push 生成结果应保持稳定。"
	)
	_assert_eq(
		String(first_layout.get("terrain_profile_id", "")),
		"holdout_push",
		"holdout_push 地图应回写正式 terrain_profile_id。"
	)
	var line_info := _find_holdout_push_line_info(first_layout)
	_assert_true(not line_info.is_empty(), "holdout_push 地图应形成带 wall opening 的防守线。")
	if line_info.is_empty():
		return

	var map_size: Vector2i = first_layout.get("map_size", Vector2i.ZERO)
	var hold_line_x := int(line_info.get("hold_line_x", -1))
	var opening_count := int(line_info.get("opening_count", 0))
	var wall_count := int(line_info.get("wall_count", 0))
	var player_coord: Vector2i = first_layout.get("player_coord", Vector2i.ZERO)
	var enemy_coord: Vector2i = first_layout.get("enemy_coord", Vector2i.ZERO)
	var cells: Dictionary = first_layout.get("cells", {})
	var player_cell := cells.get(player_coord) as BattleCellState
	var enemy_cell := cells.get(enemy_coord) as BattleCellState
	_assert_true(hold_line_x >= int(map_size.x * 0.55), "holdout_push 的防守线应落在战场右半区，体现守点纵深。")
	_assert_true(wall_count >= 2, "holdout_push 的防守线应至少包含两段 wall。")
	_assert_true(opening_count >= 1 and opening_count <= 2, "holdout_push 的防守线 opening 应保持 1-2 处，形成可预判的推进入口。")
	_assert_true(player_coord.x < hold_line_x and enemy_coord.x > hold_line_x, "holdout_push 的出生点应分列防守线两侧。")
	_assert_true(
		int(line_info.get("left_reachable_count", 0)) > int(line_info.get("right_reachable_count", 0)),
		"holdout_push 的推进侧应比守点侧拥有更大的机动展开空间。"
	)
	_assert_true(
		player_cell != null and enemy_cell != null and int(enemy_cell.current_height) >= int(player_cell.current_height) + 1,
		"holdout_push 的守点出生位应至少高出推进方一层。"
	)

	var objective_coords := _collect_layout_prop_coords(first_layout, BattleBoardPropCatalog.PROP_OBJECTIVE_MARKER)
	_assert_eq(objective_coords.size(), 1, "holdout_push 地图应恰好生成一个守点目标。")
	if objective_coords.size() == 1:
		_assert_true(objective_coords[0].x > hold_line_x, "holdout_push 的目标点应落在防守线之后的 holdout 内部。")

	_assert_true(
		_count_terrain_cells_in_x_range(first_layout, BattleCellState.TERRAIN_MUD, hold_line_x - 2, hold_line_x - 1) >= 2,
		"holdout_push 的推进侧在防线前应保留泥地减速带。"
	)
	_assert_true(
		_count_terrain_cells_in_x_range(first_layout, BattleCellState.TERRAIN_SPIKE, hold_line_x + 1, hold_line_x + 1) >= 2,
		"holdout_push 的守点正面应布置 spike barricade 区域。"
	)

	var ally_spawns := _extract_layout_coords(first_layout.get("ally_spawns", []))
	var enemy_spawns := _extract_layout_coords(first_layout.get("enemy_spawns", []))
	_assert_true(ally_spawns.size() >= 2, "holdout_push 的 ally_spawns 应至少保留起点外的额外推进站位。")
	_assert_true(enemy_spawns.size() >= 2, "holdout_push 的 enemy_spawns 应至少保留起点外的额外守点站位。")
	for coord in ally_spawns:
		_assert_true(coord.x < hold_line_x, "holdout_push 的 ally_spawns 应全部落在推进侧。")
	for coord in enemy_spawns:
		_assert_true(coord.x > hold_line_x, "holdout_push 的 enemy_spawns 应全部落在守点侧。")

	var tent_coords := _collect_layout_prop_coords(first_layout, BattleBoardPropCatalog.PROP_TENT)
	var torch_coords := _collect_layout_prop_coords(first_layout, BattleBoardPropCatalog.PROP_TORCH)
	_assert_eq(tent_coords.size(), 2, "holdout_push 地图应显式放置双方 tent。")
	_assert_eq(torch_coords.size(), 2, "holdout_push 地图应显式放置双方 torch。")
	_assert_eq(_count_coords_on_or_left_of_x(tent_coords, hold_line_x), 1, "holdout_push 的 tent 应在推进侧保留一处集结营地。")
	_assert_eq(_count_coords_strictly_right_of_x(tent_coords, hold_line_x), 1, "holdout_push 的 tent 应在守点侧保留一处固守营地。")
	_assert_eq(_count_coords_on_or_left_of_x(torch_coords, hold_line_x), 1, "holdout_push 的 torch 应在推进侧保留一处灯火。")
	_assert_eq(_count_coords_strictly_right_of_x(torch_coords, hold_line_x), 1, "holdout_push 的 torch 应在守点侧保留一处灯火。")
	var explicit_prop_coords: Array[Vector2i] = []
	explicit_prop_coords.append_array(objective_coords)
	explicit_prop_coords.append_array(tent_coords)
	explicit_prop_coords.append_array(torch_coords)
	for coord in explicit_prop_coords:
		var cell := first_layout.get("cells", {}).get(coord) as BattleCellState
		_assert_true(cell != null and cell.passable, "holdout_push 的显式 prop 必须放在可通行地格上：%s" % str(coord))
		_assert_true(
			not ally_spawns.has(coord) and not enemy_spawns.has(coord),
			"holdout_push 的显式 prop 不应覆盖部署位：%s" % str(coord)
		)
	_assert_layout_uses_supported_props(first_layout)


func _test_holdout_push_board_contracts() -> void:
	var layout := _build_holdout_push_layout(TEST_SEED)
	var board := await _instantiate_board(_build_state(layout))
	_assert_prop_and_unit_sorting(board)


func _test_default_generation_respects_global_min_height() -> void:
	var layout := _build_default_layout(TEST_SEED)
	for cell_variant in layout.get("cells", {}).values():
		var cell := cell_variant as BattleCellState
		if cell == null:
			continue
		_assert_true(
			int(cell.current_height) >= GLOBAL_MIN_HEIGHT,
			"default profile 的每个地格顶层高度都应不低于 %d。" % GLOBAL_MIN_HEIGHT
		)
	var columns_variant: Variant = layout.get("cell_columns", {})
	if columns_variant is Dictionary:
		for column_variant in (columns_variant as Dictionary).values():
			if column_variant is not Array:
				continue
			var column := column_variant as Array
			_assert_true(
				column.size() >= GLOBAL_MIN_HEIGHT + 1,
				"default profile 的每列真实堆叠 cell 数量都应不低于 %d。" % (GLOBAL_MIN_HEIGHT + 1)
			)


func _test_default_water_height_normalization_is_component_local() -> void:
	var generator := BattleTerrainGenerator.new()
	var heights := {
		Vector2i(0, 0): 5,
		Vector2i(0, 1): 4,
		Vector2i(2, 0): 6,
		Vector2i(2, 1): 7,
	}
	var water_cells := {
		Vector2i(0, 0): true,
		Vector2i(0, 1): true,
		Vector2i(2, 0): true,
		Vector2i(2, 1): true,
	}
	generator._normalize_water_heights(heights, water_cells)
	_assert_eq(int(heights.get(Vector2i(0, 0), -1)), 4, "左侧水域应压平到自身连通区域的最低水位。")
	_assert_eq(int(heights.get(Vector2i(0, 1), -1)), 4, "左侧水域的每个格子都应共享同一水位。")
	_assert_eq(int(heights.get(Vector2i(2, 0), -1)), 6, "右侧独立水域不应被错误拉低到另一片湖的水位。")
	_assert_eq(int(heights.get(Vector2i(2, 1), -1)), 6, "独立水域应按各自连通分区单独归一化。")


func _test_generated_spawn_coords_never_use_water_tiles() -> void:
	var cases := [
		{
			"label": "canyon(seed=%d)" % TEST_SEED,
			"layout": _build_canyon_layout(TEST_SEED),
		},
		{
			"label": "canyon(seed=%d)" % (TEST_SEED + 17),
			"layout": _build_canyon_layout(TEST_SEED + 17),
		},
		{
			"label": "canyon(seed=%d)" % (TEST_SEED + 29),
			"layout": _build_canyon_layout(TEST_SEED + 29),
		},
		{
			"label": "default(seed=%d)" % TEST_SEED,
			"layout": _build_default_layout(TEST_SEED),
		},
		{
			"label": "default(seed=%d)" % (TEST_SEED + 17),
			"layout": _build_default_layout(TEST_SEED + 17),
		},
		{
			"label": "default(seed=%d)" % (TEST_SEED + 29),
			"layout": _build_default_layout(TEST_SEED + 29),
		},
		{
			"label": "narrow_assault(seed=%d)" % TEST_SEED,
			"layout": _build_narrow_assault_layout(TEST_SEED),
		},
		{
			"label": "holdout_push(seed=%d)" % TEST_SEED,
			"layout": _build_holdout_push_layout(TEST_SEED),
		},
	]
	for case_data in cases:
		_assert_layout_spawn_coords_avoid_water(
			case_data.get("layout", {}),
			String(case_data.get("label", "unknown"))
		)


func _test_render_profile_chain_and_source_specs_have_stable_fallbacks() -> void:
	var terrain_ids := [
		&"default",
		&"canyon",
		&"narrow_assault",
		&"holdout_push",
		&"unknown_fixture_profile",
	]
	for terrain_id in terrain_ids:
		var profile := BattleBoardRenderProfile.for_terrain_profile_id(terrain_id)
		_assert_true(profile != null, "[profile] terrain profile 应始终能解析到 battle board render profile：%s" % String(terrain_id))
		if profile == null:
			continue
		_assert_true(
			profile.render_profile_id != &"",
			"[profile] terrain profile -> render profile 解析链应保留稳定 render_profile_id：%s" % String(terrain_id)
		)
		_assert_true(
			not profile.asset_dir.is_empty(),
			"[profile] render profile 应提供稳定 asset_dir fallback：%s" % String(terrain_id)
		)
		_assert_eq(
			profile.asset_dir,
			BattleBoardRenderProfile.DEFAULT_ASSET_DIR,
			"[profile] 所有 terrain profile 应解析到统一的 canyon 资产目录：%s" % String(terrain_id)
		)
		_assert_eq(
			profile.visual_height_step,
			BattleBoardRenderProfile.DEFAULT_VISUAL_HEIGHT_STEP,
			"[profile] 所有 terrain profile 应使用统一的 20px 视觉高度步长：%s" % String(terrain_id)
		)
		_assert_eq(
			profile.board_tile_size,
			Vector2i(64, 32),
			"[profile] render profile 应显式持有棋盘 tile 尺寸。"
		)
		_assert_eq(
			profile.tile_half_size,
			Vector2(32.0, 16.0),
			"[pick] render profile 应显式持有点击面半尺寸。"
		)
		_assert_true(
			profile.surface_pick_shape == &"diamond",
			"[pick] render profile 应显式持有 surface_pick_shape。"
		)
		_assert_true(
			profile.camera_margin.y >= 72.0,
			"[profile] render profile 应显式持有相机边界 margin。"
		)

		var specs := profile.get_source_specs()
		_assert_true(not specs.is_empty(), "[source] render profile 应以 source spec 表驱动 TileSet 注册：%s" % String(terrain_id))
		for source_spec in specs:
			_assert_true(source_spec.has("atlas_region_size"), "[source] source spec 应包含 atlas_region_size。")
			_assert_true(source_spec.has("board_tile_size"), "[source] source spec 应包含 board_tile_size。")
			_assert_true(
				source_spec.has("texture_origin") or source_spec.has("visual_origin"),
				"[source] source spec 应包含 texture_origin 或等价 visual_origin。"
			)
			_assert_true(source_spec.has("layer_role"), "[source] source spec 应包含 layer_role。")


func _test_canyon_face_source_specs_use_tall_region() -> void:
	var profile := BattleBoardRenderProfile.for_terrain_profile_id(&"canyon")
	_assert_eq(profile.render_profile_id, BattleBoardRenderProfile.RENDER_PROFILE_CANYON_ISO64, "canyon 应解析到统一的 canyon iso64 render profile。")
	_assert_eq(profile.asset_dir, BattleBoardRenderProfile.DEFAULT_ASSET_DIR, "canyon render profile 应指向统一 canyon 资产目录。")
	for source_spec in profile.get_source_specs():
		var files := source_spec.get("files", []) as Array
		_assert_true(not files.is_empty(), "[source] source spec 应显式列出贴图文件：%s" % String(source_spec.get("key", &"")))
		for file_name_variant in files:
			var path := "%s/%s" % [profile.asset_dir, String(file_name_variant)]
			_assert_true(FileAccess.file_exists(path), "[source] source 贴图必须存在：%s" % path)
	var face_keys := [
		BattleBoardRenderProfile.SOURCE_EDGE_DROP_EAST,
		BattleBoardRenderProfile.SOURCE_EDGE_DROP_SOUTH,
		BattleBoardRenderProfile.SOURCE_WALL_EAST,
		BattleBoardRenderProfile.SOURCE_WALL_SOUTH,
	]
	for source_key in face_keys:
		var spec := _find_source_spec(profile, source_key)
		_assert_true(not spec.is_empty(), "[source] 应包含 cliff/wall source spec：%s" % String(source_key))
		if spec.is_empty():
			continue
		var atlas_region_size: Vector2i = spec.get("atlas_region_size", Vector2i.ZERO)
		_assert_eq(
			atlas_region_size,
			BattleBoardRenderProfile.DEFAULT_FACE_REGION_SIZE,
			"[source] cliff/wall source 应使用 64×36 face region（20px 崖面 + 8px 上下切角）：%s" % String(source_key)
		)


func _test_canyon_two_layer_visual_separation_uses_20_step() -> void:
	var state := BattleState.new()
	state.battle_id = &"canyon_two_layer_visual_separation"
	state.seed = TEST_SEED
	state.map_size = Vector2i(2, 1)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = &"canyon"
	state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0), 2),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0), 0),
	}
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	var board := await _instantiate_board(state)
	var plane_anchor := (board.get_node("InputLayer") as TileMapLayer).map_to_local(Vector2i(0, 0))
	var raised_anchor: Vector2 = board._get_coord_anchor(Vector2i(0, 0))
	var two_layer_separation := plane_anchor.y - raised_anchor.y
	_assert_approx(
		two_layer_separation,
		BattleBoardRenderProfile.DEFAULT_VISUAL_HEIGHT_STEP * 2.0,
		0.01,
		"[profile] canyon 两层高地视觉分离应由 render profile 的 20px visual_height_step 驱动。"
	)

	board.queue_free()
	await process_frame


func _test_canyon_layer_offsets_follow_render_profile_after_bind() -> void:
	var state := BattleState.new()
	state.battle_id = &"canyon_layer_offsets_after_bind"
	state.seed = TEST_SEED
	state.map_size = Vector2i(2, 1)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = &"canyon"
	state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0), 2),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0), 0),
	}
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	var board := await _instantiate_board(state)
	var profile := BattleBoardRenderProfile.for_terrain_profile_id(&"canyon")

	_assert_approx(
		(board.get_node("TopH2") as TileMapLayer).position.y,
		-profile.visual_height_step * 2.0,
		0.01,
		"[profile] bind_layers 后 TopH2 偏移应按 20px visual_height_step 应用。"
	)
	_assert_approx(
		(board.get_node("EdgeDropEastH2") as TileMapLayer).position.y,
		-profile.visual_height_step * 2.0,
		0.01,
		"[profile] bind_layers 后 EdgeDropEastH2 偏移应按 20px visual_height_step 应用。"
	)
	_assert_approx(
		(board.get_node("MarkerH2") as TileMapLayer).position.y,
		-profile.visual_height_step * 2.0,
		0.01,
		"[profile] bind_layers 后 MarkerH2 偏移应按 20px visual_height_step 应用。"
	)

	board.queue_free()
	await process_frame


func _test_battle_board_contracts() -> void:
	var layout := _build_canyon_layout(TEST_SEED)
	var board_a := await _instantiate_board(_build_state(layout))
	var board_b := await _instantiate_board(_build_state(layout))

	_assert_tile_variant_variety(board_a)
	_assert_input_mapping(board_a, layout.get("player_coord", Vector2i.ZERO))
	_assert_drop_face_stacking(board_a, layout)
	_assert_prop_and_unit_sorting(board_a)
	_assert_true(
		_capture_board_signature(board_a) == _capture_board_signature(board_b),
		"相同 battle state 的 board 渲染签名应保持稳定。"
	)

	board_a.queue_free()
	board_b.queue_free()
	await process_frame


func _test_raised_top_surface_click_maps_to_visual_cell() -> void:
	var state := BattleState.new()
	state.battle_id = &"raised_top_surface_pick"
	state.seed = TEST_SEED
	state.map_size = Vector2i(3, 3)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = &"default"
	state.cells = {}
	for y in range(3):
		for x in range(3):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y), 0)
	_set_cell_height(state, Vector2i(1, 1), 3)
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []

	var board := await _instantiate_board(state)
	var raised_coord := Vector2i(1, 1)
	var raised_anchor: Vector2 = board._get_coord_anchor(raised_coord)
	var viewport_position := board.to_global(raised_anchor)
	var mapped_coord := board._viewport_position_to_board_coord(viewport_position)

	_assert_eq(
		mapped_coord,
		raised_coord,
		"[pick] 抬高顶面的视觉中心点击应命中对应高地格，而不是回落到底层平面格。"
	)

	board.queue_free()
	await process_frame


func _test_visual_pick_prefers_visible_higher_top_when_surfaces_overlap() -> void:
	var state := BattleState.new()
	state.battle_id = &"raised_top_surface_overlap_pick"
	state.seed = TEST_SEED
	state.map_size = Vector2i(3, 3)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = &"canyon"
	state.cells = {}
	for y in range(3):
		for x in range(3):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y), 0)
	var raised_coord := Vector2i(1, 1)
	var lower_coord := Vector2i(1, 0)
	_set_cell_height(state, raised_coord, 1)
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var board := await _instantiate_board(state)
	var raised_anchor: Vector2 = board._get_coord_anchor(raised_coord)
	var lower_anchor: Vector2 = board._get_coord_anchor(lower_coord)
	var overlap_point := _find_top_surface_overlap_point(board, raised_anchor, lower_anchor)
	_assert_true(
		overlap_point != Vector2.INF,
		"[pick] 测试夹具应找到高地顶面与低地顶面视觉重叠的点击点。"
	)
	if overlap_point != Vector2.INF:
		_assert_eq(
			board._pick_visual_surface_coord(overlap_point),
			raised_coord,
			"[pick] 高低地顶面重叠处应稳定命中更高的可见顶面，而不是低地平面。"
		)

	board.queue_free()
	await process_frame


func _test_board_initial_camera_fills_ultrawide_width() -> void:
	var layout := _build_canyon_layout(TEST_SEED)
	var board := await _instantiate_board(_build_state(layout), [], [], ULTRAWIDE_VIEWPORT_SIZE)
	var content_bounds: Rect2 = board.get("_content_bounds")
	var zoom := board.scale.x
	var left_edge := board.position.x + content_bounds.position.x * zoom
	var right_edge := board.position.x + (content_bounds.position.x + content_bounds.size.x) * zoom
	_assert_approx(
		left_edge,
		0.0,
		1.0,
		"超宽视口下战斗棋盘左边缘应贴齐视口，不能留下额外横向留白。"
	)
	_assert_approx(
		right_edge,
		ULTRAWIDE_VIEWPORT_SIZE.x,
		1.0,
		"超宽视口下战斗棋盘右边缘应贴齐视口，不能留下额外横向留白。"
	)
	board.queue_free()
	await process_frame


func _test_board_content_bounds_follow_render_profile_for_viewport_sizes() -> void:
	var layout := _build_canyon_layout(TEST_SEED)
	for viewport_size in [VIEWPORT_SIZE, ULTRAWIDE_VIEWPORT_SIZE]:
		var state := _build_state(layout)
		var board := await _instantiate_board(state, [], [], viewport_size)
		var actual_bounds: Rect2 = board.get("_content_bounds")
		var expected_bounds := _compute_expected_profile_content_bounds(board, state)
		_assert_rect_approx(
			actual_bounds,
			expected_bounds,
			0.01,
			"content bounds 应在视口 %s 下按当前 render profile 的视觉高度和 margin 计算。" % str(viewport_size)
		)

		board.position += viewport_size * 4.0
		board._clamp_camera_position()
		_assert_camera_edges_cover_viewport(board, viewport_size, "正向拖拽边界")

		board.position -= viewport_size * 8.0
		board._clamp_camera_position()
		_assert_camera_edges_cover_viewport(board, viewport_size, "反向拖拽边界")

		board.queue_free()
		await process_frame


func _test_east_face_assets_anchor_to_neighbor_side() -> void:
	for path in EDGE_DROP_EAST_TEXTURE_PATHS:
		_assert_east_asset_neighbor_anchored(path, "east cliff")
	for path in WALL_EAST_TEXTURE_PATHS:
		_assert_east_asset_neighbor_anchored(path, "east wall")


func _test_south_face_assets_anchor_to_neighbor_side() -> void:
	for path in EDGE_DROP_SOUTH_TEXTURE_PATHS:
		_assert_south_asset_neighbor_anchored(path, "south cliff")
	for path in WALL_SOUTH_TEXTURE_PATHS:
		_assert_south_asset_neighbor_anchored(path, "south wall")


func _test_edge_feature_authoring_roundtrips() -> void:
	var cell := _build_cell(Vector2i(2, 3), 1)
	cell.set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())
	cell.set_edge_feature(Vector2i.DOWN, BattleEdgeFeatureState.make_toggle_door(true))
	var restored := BattleCellState.from_dict(cell.to_dict())
	_assert_true(
		restored.edge_feature_east != null and restored.edge_feature_east.feature_kind == BattleEdgeFeatureState.FEATURE_WALL,
		"edge feature east 应能通过 to_dict()/from_dict() 保留 richer authoring 类型。"
	)
	_assert_true(
		restored.edge_feature_south != null and restored.edge_feature_south.feature_kind == BattleEdgeFeatureState.FEATURE_DOOR,
		"edge feature south 应能通过 to_dict()/from_dict() 保留 richer authoring 类型。"
	)
	_assert_true(
		restored.edge_feature_south != null and not restored.edge_feature_south.blocks_move,
		"door open 这类 richer edge feature 应能保留与 wall 不同的阻挡语义。"
	)


func _test_wall_faces_render_from_edge_features() -> void:
	var state := BattleState.new()
	state.battle_id = &"wall_feature_faces"
	state.seed = TEST_SEED
	state.map_size = Vector2i(2, 2)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = &"default"
	state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0), 0),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0), 0),
		Vector2i(0, 1): _build_cell(Vector2i(0, 1), 0),
		Vector2i(1, 1): _build_cell(Vector2i(1, 1), 0),
	}
	(state.cells.get(Vector2i(0, 0)) as BattleCellState).set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())
	(state.cells.get(Vector2i(0, 0)) as BattleCellState).set_edge_feature(Vector2i.DOWN, BattleEdgeFeatureState.make_wall())
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []
	var east_edge_face = _edge_service.get_edge_face(state, Vector2i(0, 0), Vector2i(1, 0))
	var south_edge_face = _edge_service.get_edge_face(state, Vector2i(0, 0), Vector2i(0, 1))
	_assert_true(
		east_edge_face != null and east_edge_face.has_feature_face(),
		"统一 edge cache 应能把 east wall authoring 解析成 feature face。"
	)
	_assert_true(
		south_edge_face != null and south_edge_face.has_feature_face(),
		"统一 edge cache 应能把 south wall authoring 解析成 feature face。"
	)
	var board := await _instantiate_board(state)

	var east_image := _get_layer_cell_image(
		board.get_node("WallEastH0") as TileMapLayer,
		_get_expected_edge_render_coord(Vector2i.ZERO, Vector2i.RIGHT)
	)
	var south_image := _get_layer_cell_image(
		board.get_node("WallSouthH0") as TileMapLayer,
		_get_expected_edge_render_coord(Vector2i.ZERO, Vector2i.DOWN)
	)
	_assert_true(east_image != null, "带 east wall feature 的边应在 WallEastH0 成功出图。")
	_assert_true(south_image != null, "带 south wall feature 的边应在 WallSouthH0 成功出图。")

	board.queue_free()
	await process_frame


func _test_flat_plateau_renders_south_boundary_outside_land_cells() -> void:
	var state := BattleState.new()
	state.battle_id = &"flat_plateau_south_boundary"
	state.seed = TEST_SEED
	state.map_size = Vector2i(3, 3)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = &"default"
	state.cells = {}
	for y in range(3):
		for x in range(3):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y), 1)
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []
	var board := await _instantiate_board(state)

	var south_h1 := board.get_node("EdgeDropSouthH1") as TileMapLayer
	_assert_true(south_h1 != null, "平坦平台应存在 EdgeDropSouthH1 图层。")
	if south_h1 == null:
		return
	for y in range(3):
		for x in range(3):
			var coord := Vector2i(x, y)
			var expected := -1
			if y == 2:
				expected = south_h1.get_cell_source_id(Vector2i(x, y + 1))
			_assert_true(
				south_h1.get_cell_source_id(coord) < 0,
				"同高平台内部与表面格坐标不应直接承载 south cliff：%s" % str(coord)
			)
			if y == 2:
				_assert_true(
					expected >= 0,
					"平坦平台只有 south 外边界应在下方相邻格坐标出 cliff：%s" % str(Vector2i(x, y + 1))
				)

	board.queue_free()
	await process_frame


func _test_flat_plateau_renders_east_boundary_outside_land_cells() -> void:
	var state := BattleState.new()
	state.battle_id = &"flat_plateau_east_boundary"
	state.seed = TEST_SEED
	state.map_size = Vector2i(3, 3)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = &"default"
	state.cells = {}
	for y in range(3):
		for x in range(3):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y), 1)
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []
	var board := await _instantiate_board(state)

	var east_h1 := board.get_node("EdgeDropEastH1") as TileMapLayer
	_assert_true(east_h1 != null, "平坦平台应存在 EdgeDropEastH1 图层。")
	if east_h1 == null:
		return
	for y in range(3):
		for x in range(3):
			var coord := Vector2i(x, y)
			var boundary_coord := Vector2i(x + 1, y)
			_assert_true(
				east_h1.get_cell_source_id(coord) < 0,
				"同高平台内部与表面格坐标不应直接承载 east cliff：%s" % str(coord)
			)
			if x == 2:
				_assert_true(
					east_h1.get_cell_source_id(boundary_coord) >= 0,
					"平坦平台只有 east 外边界应在右侧相邻格坐标出 cliff：%s" % str(boundary_coord)
				)

	board.queue_free()
	await process_frame


func _test_raised_drop_faces_follow_absolute_height_layers() -> void:
	var state := BattleState.new()
	state.battle_id = &"raised_drop_face_height_layers"
	state.seed = TEST_SEED
	state.map_size = Vector2i(2, 2)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = &"default"
	state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0), 5),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0), 4),
		Vector2i(0, 1): _build_cell(Vector2i(0, 1), 4),
		Vector2i(1, 1): _build_cell(Vector2i(1, 1), 4),
	}
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []
	var board := await _instantiate_board(state)

	var east_h5 := board.get_node("EdgeDropEastH5") as TileMapLayer
	var south_h5 := board.get_node("EdgeDropSouthH5") as TileMapLayer
	var east_h1 := board.get_node("EdgeDropEastH1") as TileMapLayer
	var south_h1 := board.get_node("EdgeDropSouthH1") as TileMapLayer
	var east_render_coord := _get_expected_edge_render_coord(Vector2i.ZERO, Vector2i.RIGHT)
	var south_render_coord := _get_expected_edge_render_coord(Vector2i.ZERO, Vector2i.DOWN)
	_assert_true(
		east_h5 != null and east_h5.get_cell_source_id(east_render_coord) >= 0,
		"高台向东仅下降一级时，east drop face 应落在源高度对应的 H5 层。"
	)
	_assert_true(
		south_h5 != null and south_h5.get_cell_source_id(south_render_coord) >= 0,
		"高台向南仅下降一级时，south drop face 应落在源高度对应的 H5 层。"
	)
	_assert_true(
		east_h1 == null or east_h1.get_cell_source_id(east_render_coord) < 0,
		"高台向东仅下降一级时，不应错误回落到 EdgeDropEastH1。"
	)
	_assert_true(
		south_h1 == null or south_h1.get_cell_source_id(south_render_coord) < 0,
		"高台向南仅下降一级时，不应错误回落到 EdgeDropSouthH1。"
	)

	board.queue_free()
	await process_frame


func _test_board_layer_draw_order_is_explicit() -> void:
	var state := BattleState.new()
	state.battle_id = &"layer_draw_order"
	state.seed = TEST_SEED
	state.map_size = Vector2i(2, 2)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = &"default"
	state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0), 5),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0), 4),
		Vector2i(0, 1): _build_cell(Vector2i(0, 1), 4),
		Vector2i(1, 1): _build_cell(Vector2i(1, 1), 4),
	}
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []
	var board := await _instantiate_board(state)

	var top_h4 := board.get_node("TopH4") as TileMapLayer
	var top_h5 := board.get_node("TopH5") as TileMapLayer
	var east_h5 := board.get_node("EdgeDropEastH5") as TileMapLayer
	var south_h5 := board.get_node("EdgeDropSouthH5") as TileMapLayer
	var wall_east_h5 := board.get_node("WallEastH5") as TileMapLayer
	var wall_south_h5 := board.get_node("WallSouthH5") as TileMapLayer
	var overlay_h5 := board.get_node("OverlayH5") as TileMapLayer
	var marker_h0 := board.get_node("MarkerH0") as TileMapLayer
	var marker_h5 := board.get_node("MarkerH5") as TileMapLayer
	_assert_true(not board.y_sort_enabled, "[depth] BattleBoard2D 根节点不应对全部子层启用 y_sort，否则会破坏地形显式层级。")
	_assert_true(
		top_h4 != null and east_h5 != null and top_h4.z_index < east_h5.z_index,
		"[depth] EdgeDropEastH5 仍应绘制在低一层 TopH4 之上，避免被下层内容压住。"
	)
	_assert_true(
		east_h5 != null and south_h5 != null and east_h5.z_index < south_h5.z_index,
		"[depth] 同高度下 EdgeDropSouthH5 应明确绘制在 EdgeDropEastH5 之上，不能共享相同 z_index。"
	)
	_assert_true(
		south_h5 != null and wall_east_h5 != null and south_h5.z_index < wall_east_h5.z_index,
		"[depth] WallEastH5 应继续绘制在 south drop face layer 之上。"
	)
	_assert_true(
		wall_east_h5 != null and wall_south_h5 != null and wall_east_h5.z_index < wall_south_h5.z_index,
		"[depth] 同高度下 WallSouthH5 应明确绘制在 WallEastH5 之上。"
	)
	_assert_true(
		wall_south_h5 != null and top_h5 != null and wall_south_h5.z_index < top_h5.z_index,
		"[depth] 同高度发生重叠时，land 顶面 TopH5 应绘制在 wall/edge layer 之上。"
	)
	_assert_true(
		top_h5 != null and overlay_h5 != null and top_h5.z_index < overlay_h5.z_index,
		"[depth] OverlayH5 应继续绘制在 TopH5 之上。"
	)
	_assert_true(
		overlay_h5 != null and marker_h5 != null and overlay_h5.z_index < marker_h5.z_index,
		"[depth] MarkerH5 应继续绘制在同高度 terrain/overlay 之上。"
	)
	_assert_true(
		marker_h0 != null and top_h5 != null and marker_h0.z_index < top_h5.z_index,
		"[depth] 低地 MarkerH0 不应越过高地 TopH5，避免低地 marker 穿出高崖前景。"
	)

	board.queue_free()
	await process_frame


func _test_skill_valid_target_highlight_renders_above_units() -> void:
	var layout := _build_canyon_layout(TEST_SEED)
	var state := _build_state(layout)
	var enemy_unit := state.units.get(state.enemy_unit_ids[0]) as BattleUnitState
	_assert_true(enemy_unit != null, "测试夹具应成功创建敌方单位。")
	if enemy_unit == null:
		return
	var board := await _instantiate_board(state, [], [enemy_unit.coord])
	var unit_layer := board.get_node("UnitLayer") as Node2D
	var target_highlight_layer := board.get_node("TargetHighlightLayer") as Node2D
	_assert_true(target_highlight_layer != null, "BattleBoard2D 应存在专用的技能合法目标顶层高亮节点。")
	_assert_true(
		target_highlight_layer != null and unit_layer != null and target_highlight_layer.z_index > unit_layer.z_index,
		"技能合法目标高亮层必须绘制在 UnitLayer 之上，避免半透明红色被人物遮住。"
	)
	if target_highlight_layer != null:
		var highlight := target_highlight_layer.get_node_or_null("ValidTarget_%d_%d" % [enemy_unit.coord.x, enemy_unit.coord.y]) as Polygon2D
		_assert_true(highlight != null, "传入合法目标坐标后，应生成对应的顶层红色高亮。")
		if highlight != null:
			_assert_eq(highlight.get_meta("board_coord", Vector2i(-1, -1)), enemy_unit.coord, "高亮节点应记录正确的战斗坐标。")
			_assert_true(highlight.color.a > 0.0 and highlight.color.a < 1.0, "合法目标高亮应保持半透明。")
			_assert_true(highlight.color.r > highlight.color.g and highlight.color.r > highlight.color.b, "合法目标高亮应为红色主导。")

	board.queue_free()
	await process_frame


func _test_active_unit_marker_uses_opaque_land_cover_blue() -> void:
	var layout := _build_canyon_layout(TEST_SEED)
	var state := _build_state(layout)
	var active_unit := state.units.get(state.active_unit_id) as BattleUnitState
	_assert_true(active_unit != null, "测试夹具应成功创建当前可行动单位。")
	if active_unit == null:
		return
	var cell := state.cells.get(active_unit.coord) as BattleCellState
	_assert_true(cell != null, "当前可行动单位所在格应存在有效 cell。")
	if cell == null:
		return
	var board := await _instantiate_board(state)
	var marker_layer := board.get_node_or_null("MarkerH%d" % int(cell.current_height)) as TileMapLayer
	_assert_true(marker_layer != null, "当前可行动单位所在高度应存在 Marker 图层。")
	if marker_layer == null:
		board.queue_free()
		await process_frame
		return
	var active_marker_image := _get_layer_cell_image(marker_layer, active_unit.coord)
	var land_image := _load_png_image(TOP_LAND_TEXTURE_PATH)
	_assert_true(active_marker_image != null, "当前可行动单位所在格应渲染 active marker。")
	_assert_true(land_image != null, "active marker 回归应能读取 land 顶面贴图作为轮廓基准。")
	if active_marker_image != null and land_image != null:
		_assert_true(
			_image_alpha_mask_matches(land_image, active_marker_image),
			"当前可行动 marker 应完整复用 land 顶面轮廓，不能退回框状选框。"
		)
		var center := active_marker_image.get_pixel(active_marker_image.get_width() / 2, active_marker_image.get_height() / 2)
		_assert_true(center.a >= 0.99, "当前可行动 marker 中心像素应保持不透明。")
		_assert_true(
			center.r <= 0.01 and center.g <= 0.01 and center.b >= 0.99,
			"当前可行动 marker 中心像素应为纯蓝主导。"
		)

	board.queue_free()
	await process_frame


func _test_unit_tokens_render_hp_bars_with_numeric_labels() -> void:
	var layout := _build_canyon_layout(TEST_SEED)
	var state := _build_state(layout)
	if state.ally_unit_ids.is_empty() or state.enemy_unit_ids.is_empty():
		_assert_true(false, "战斗棋盘回归夹具应至少包含一名我方和一名敌方单位。")
		return

	var ally_unit := state.units.get(state.ally_unit_ids[0]) as BattleUnitState
	var enemy_unit := state.units.get(state.enemy_unit_ids[0]) as BattleUnitState
	_assert_true(ally_unit != null and enemy_unit != null, "测试夹具应成功创建单位用于血条展示检查。")
	if ally_unit == null or enemy_unit == null:
		return

	ally_unit.attribute_snapshot.set_value(&"hp_max", 18)
	ally_unit.current_hp = 12
	enemy_unit.attribute_snapshot.set_value(&"hp_max", 27)
	enemy_unit.current_hp = 9

	var board := await _instantiate_board(state)
	var unit_layer := board.get_node("UnitLayer") as Node2D
	var cases := [
		{"unit": ally_unit, "label": "我方"},
		{"unit": enemy_unit, "label": "敌方"},
	]

	for case_variant in cases:
		var case_data: Dictionary = case_variant
		var unit_state := case_data.get("unit") as BattleUnitState
		var label := String(case_data.get("label", "单位"))
		if unit_state == null:
			continue
		var token := unit_layer.get_node_or_null(String(unit_state.unit_id)) as Node2D
		_assert_true(token != null, "%s单位 token 应成功渲染到 UnitLayer。" % label)
		if token == null:
			continue

		var health_bar := token.get_node_or_null("HealthBarRoot") as Control
		_assert_true(health_bar != null, "%s单位 token 应包含正式血条节点。" % label)
		if health_bar == null:
			continue
		_assert_true(health_bar.size.x <= 64.0, "%s单位血条宽度不能超过单格宽度。" % label)

		var fill := health_bar.get_node_or_null("HealthBarFill") as ColorRect
		_assert_true(fill != null, "%s单位血条应包含填充节点。" % label)
		if fill != null:
			_assert_true(fill.size.x <= health_bar.size.x, "%s单位血条填充宽度不能超过血条容器。" % label)
			_assert_true(fill.size.x > 0.0, "%s单位在非零 HP 时应看到非空血量填充。" % label)

		var value_label := health_bar.get_node_or_null("HealthBarTextLabel") as Label
		_assert_true(value_label != null, "%s单位血条应显示数字文本。" % label)
		if value_label != null:
			_assert_eq(
				value_label.text,
				"%d/%d" % [unit_state.current_hp, int(unit_state.attribute_snapshot.get_value(&"hp_max"))],
				"%s单位血条数字应显示当前 HP / 最大 HP。" % label
			)

	board.queue_free()
	await process_frame


func _test_unit_render_depth_uses_positive_height_bias() -> void:
	var state := BattleState.new()
	state.battle_id = &"unit_render_depth_bias"
	state.seed = TEST_SEED
	state.map_size = Vector2i(2, 2)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = &"default"
	state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0), 0),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0), 0),
		Vector2i(0, 1): _build_cell(Vector2i(0, 1), 3),
		Vector2i(1, 1): _build_cell(Vector2i(1, 1), 0),
	}
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []

	var low_unit := _build_unit(&"low_unit", "低地", &"player")
	var high_unit := _build_unit(&"high_unit", "高地", &"player")
	state.units[low_unit.unit_id] = low_unit
	state.units[high_unit.unit_id] = high_unit
	state.ally_unit_ids.append(low_unit.unit_id)
	state.ally_unit_ids.append(high_unit.unit_id)
	_grid_service.place_unit(state, low_unit, Vector2i(1, 0), true)
	_grid_service.place_unit(state, high_unit, Vector2i(0, 1), true)
	state.active_unit_id = low_unit.unit_id

	var board := await _instantiate_board(state)
	var unit_layer := board.get_node("UnitLayer")
	var low_node := unit_layer.get_node_or_null("low_unit") as Node2D
	var high_node := unit_layer.get_node_or_null("high_unit") as Node2D
	_assert_true(low_node != null and high_node != null, "测试单位应成功挂入 UnitLayer。")
	if low_node != null and high_node != null:
		var low_anchor_y := float(low_node.get_meta("sort_anchor_y", 0.0))
		var high_anchor_y := float(high_node.get_meta("sort_anchor_y", 0.0))
		var low_depth := int(low_node.get_meta("sort_depth", low_node.z_index))
		var high_depth := int(high_node.get_meta("sort_depth", high_node.z_index))
		_assert_true(
			high_anchor_y < low_anchor_y,
			"[depth] 高地单位的屏幕锚点 y 应更小，确保测试夹具覆盖旧 bug 场景。"
		)
		_assert_true(
			high_depth > low_depth,
			"[depth] 高地单位的渲染深度应大于同对角线低地单位，不能再直接使用扣过高度的屏幕 y。"
		)
		_assert_eq(
			high_node.z_index,
			high_depth,
			"[depth] 高地单位 z_index 应直接使用正向高度偏置后的 render depth。"
		)

	board.queue_free()
	await process_frame


func _test_dynamic_depth_interleaves_with_high_cliff_faces() -> void:
	var state := BattleState.new()
	state.battle_id = &"dynamic_depth_high_cliff_faces"
	state.seed = TEST_SEED
	state.map_size = Vector2i(2, 2)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = &"canyon"
	state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0), 5),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0), 0),
		Vector2i(0, 1): _build_cell(Vector2i(0, 1), 0),
		Vector2i(1, 1): _build_cell(Vector2i(1, 1), 0),
	}
	var high_cell := state.cells.get(Vector2i(0, 0)) as BattleCellState
	_assert_true(high_cell != null, "高崖动态深度夹具应创建高地 cell。")
	if high_cell == null:
		return
	high_cell.prop_ids.append(BattleBoardPropCatalog.PROP_TORCH)
	high_cell.set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())
	high_cell.set_edge_feature(Vector2i.DOWN, BattleEdgeFeatureState.make_wall())
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []

	var low_unit := _build_unit(&"low_cliff_unit", "低地", &"player")
	var high_unit := _build_unit(&"high_cliff_unit", "高地", &"player")
	state.units[low_unit.unit_id] = low_unit
	state.units[high_unit.unit_id] = high_unit
	state.ally_unit_ids.append(low_unit.unit_id)
	state.ally_unit_ids.append(high_unit.unit_id)
	_grid_service.place_unit(state, low_unit, Vector2i(1, 0), true)
	_grid_service.place_unit(state, high_unit, Vector2i(0, 0), true)
	state.active_unit_id = high_unit.unit_id
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var board := await _instantiate_board(state)
	var unit_layer := board.get_node("UnitLayer") as Node2D
	var prop_layer := board.get_node("PropLayer") as Node2D
	var high_wall_layer := board.get_node("WallEastH5") as TileMapLayer
	var low_node := unit_layer.get_node_or_null("low_cliff_unit") as Node2D
	var high_node := unit_layer.get_node_or_null("high_cliff_unit") as Node2D
	var high_prop := prop_layer.get_node_or_null("torch_0_0_0") as Node2D
	_assert_true(low_node != null and high_node != null and high_prop != null, "高崖动态深度夹具应渲染低地单位、高地单位和高地 prop。")
	_assert_true(high_wall_layer != null, "高崖动态深度夹具应渲染 H5 wall layer。")
	if low_node != null and high_node != null and high_prop != null and high_wall_layer != null:
		_assert_true(
			low_node.z_index < high_wall_layer.z_index,
			"[depth] 低地单位 z-depth 应低于高崖前景 wall face，不能穿出高崖。"
		)
		_assert_true(
			high_node.z_index > high_wall_layer.z_index,
			"[depth] 同一高地顶面的单位 z-depth 应高于 wall face，不能被同顶面墙面压住。"
		)
		_assert_true(
			high_prop.z_index > high_wall_layer.z_index,
			"[depth] 同一高地顶面的 prop z-depth 应高于 wall face，不能落回旧平面排序。"
		)
		_assert_eq(
			int(high_prop.get_meta("sort_depth", high_prop.z_index)),
			high_prop.z_index,
			"[depth] prop 应记录并使用与单位一致的显式 sort_depth。"
		)

	board.queue_free()
	await process_frame


func _test_large_unit_footprint_respects_edge_barriers() -> void:
	var state := BattleState.new()
	state.battle_id = &"large_unit_edge_barrier"
	state.seed = TEST_SEED
	state.map_size = Vector2i(3, 2)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = &"default"
	state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0), 0),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0), 0),
		Vector2i(2, 0): _build_cell(Vector2i(2, 0), 0),
		Vector2i(0, 1): _build_cell(Vector2i(0, 1), 0),
		Vector2i(1, 1): _build_cell(Vector2i(1, 1), 0),
		Vector2i(2, 1): _build_cell(Vector2i(2, 1), 0),
	}
	(state.cells.get(Vector2i(1, 0)) as BattleCellState).set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())
	(state.cells.get(Vector2i(1, 1)) as BattleCellState).set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []

	var large_unit := _build_unit(&"large_wall_test", "巨像", &"player")
	large_unit.body_size = 3
	large_unit.refresh_footprint()
	state.units[large_unit.unit_id] = large_unit
	state.ally_unit_ids.append(large_unit.unit_id)
	_assert_true(
		_grid_service.place_unit(state, large_unit, Vector2i(0, 0), true),
		"2x2 测试单位应能先放置在无墙阻挡的左侧区域。"
	)
	_assert_true(
		not _grid_service.can_traverse(state, large_unit.coord, Vector2i(1, 0), large_unit),
		"2x2 单位向右移动时应检查整条前沿边，不能穿过只挡前沿列的墙。"
	)


func _test_large_unit_partial_edge_barriers_block_all_directions() -> void:
	for case_data in _get_large_unit_direction_cases():
		var state := _build_large_unit_direction_state(case_data.get("map_size", Vector2i(7, 7)))
		_set_wall_between(state, case_data.get("barrier_from", Vector2i.ZERO), case_data.get("barrier_to", Vector2i.ZERO))
		state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

		var large_unit_id := StringName("large_partial_wall_%s" % String(case_data.get("label", "dir")))
		var large_unit := _build_unit(large_unit_id, "巨像", &"player")
		large_unit.body_size = 3
		large_unit.refresh_footprint()
		state.units[large_unit.unit_id] = large_unit
		state.ally_unit_ids.append(large_unit.unit_id)
		_assert_true(
			_grid_service.place_unit(state, large_unit, case_data.get("start_coord", Vector2i.ZERO), true),
			"2x2 测试单位在%s方向的半前沿墙场景里应能先放在起点。" % case_data.get("label", "未知")
		)
		_assert_true(
			not _grid_service.can_traverse(state, large_unit.coord, case_data.get("next_anchor", Vector2i.ZERO), large_unit),
			"2x2 单位在%s方向移动时，只要半个前沿被墙挡住，也应整体不能通过。" % case_data.get("label", "未知")
		)


func _test_large_unit_partial_height_barriers_block_all_directions() -> void:
	for case_data in _get_large_unit_direction_cases():
		var state := _build_large_unit_direction_state(case_data.get("map_size", Vector2i(7, 7)))
		_set_cell_height(state, case_data.get("partial_landing_coord", Vector2i.ZERO), 3)
		state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

		var large_unit_id := StringName("large_partial_height_%s" % String(case_data.get("label", "dir")))
		var large_unit := _build_unit(large_unit_id, "巨像", &"player")
		large_unit.body_size = 3
		large_unit.refresh_footprint()
		state.units[large_unit.unit_id] = large_unit
		state.ally_unit_ids.append(large_unit.unit_id)
		_assert_true(
			_grid_service.place_unit(state, large_unit, case_data.get("start_coord", Vector2i.ZERO), true),
			"2x2 测试单位在%s方向的半前沿高差场景里应能先放在起点。" % case_data.get("label", "未知")
		)
		var move_result := _grid_service.evaluate_move(state, large_unit.coord, case_data.get("next_anchor", Vector2i.ZERO), large_unit)
		_assert_true(
			not bool(move_result.get("allowed", false)),
			"2x2 单位在%s方向移动时，只要半个落点高差超过 1，也应整体不能通过。" % case_data.get("label", "未知")
		)


func _build_canyon_layout(seed: int) -> Dictionary:
	var generator := BattleTerrainGenerator.new()
	return generator.generate({
		"monster": {
			"entity_id": "battle_board_test",
			"display_name": "测试遭遇",
			"faction_id": "hostile",
			"region_tag": "canyon",
		},
		"world_coord": TEST_WORLD_COORD,
		"world_seed": seed,
		"battle_terrain_profile": "canyon",
		"battle_map_size": TEST_MAP_SIZE,
	})


func _build_narrow_assault_layout(seed: int) -> Dictionary:
	var generator := BattleTerrainGenerator.new()
	return generator.generate({
		"monster": {
			"entity_id": "battle_board_narrow_assault_test",
			"display_name": "狭道突击测试",
			"faction_id": "hostile",
			"region_tag": "narrow_assault",
		},
		"world_coord": TEST_WORLD_COORD,
		"world_seed": seed,
		"battle_terrain_profile": "narrow_assault",
		"battle_map_size": TEST_MAP_SIZE,
	})


func _build_holdout_push_layout(seed: int) -> Dictionary:
	var generator := BattleTerrainGenerator.new()
	return generator.generate({
		"monster": {
			"entity_id": "battle_board_holdout_push_test",
			"display_name": "守点推进测试",
			"faction_id": "hostile",
			"region_tag": "holdout_push",
		},
		"world_coord": TEST_WORLD_COORD,
		"world_seed": seed,
		"battle_terrain_profile": "holdout_push",
		"battle_map_size": TEST_MAP_SIZE,
	})


func _build_default_layout(seed: int) -> Dictionary:
	var generator := BattleTerrainGenerator.new()
	return generator.generate({
		"monster": {
			"entity_id": "battle_board_default_test",
			"display_name": "默认地形测试",
			"faction_id": "hostile",
			"region_tag": "default",
		},
		"world_coord": TEST_WORLD_COORD,
		"world_seed": seed,
		"battle_terrain_profile": "default",
		"battle_map_size": Vector2i(11, 9),
	})


func _build_state(layout: Dictionary) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"battle_board_regression"
	state.seed = TEST_SEED
	state.map_size = layout.get("map_size", Vector2i.ZERO)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = StringName(String(layout.get("terrain_profile_id", "default")))
	state.cells = _clone_cells(layout.get("cells", {}))
	state.cell_columns = _clone_columns(layout.get("cell_columns", BattleCellState.build_columns_from_surface_cells(state.cells)))
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []

	var ally := _build_unit(&"ally_test", "队员", &"player")
	var enemy := _build_unit(&"enemy_test", "敌人", &"hostile")
	state.units[ally.unit_id] = ally
	state.units[enemy.unit_id] = enemy
	state.ally_unit_ids.append(ally.unit_id)
	state.enemy_unit_ids.append(enemy.unit_id)
	_grid_service.place_unit(state, ally, layout.get("player_coord", Vector2i.ZERO), true)
	_grid_service.place_unit(state, enemy, layout.get("enemy_coord", Vector2i.ZERO), true)
	state.active_unit_id = ally.unit_id
	return state


func _build_unit(unit_id: StringName, display_name: String, faction_id: StringName) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.current_hp = 12
	unit.is_alive = true
	unit.refresh_footprint()
	return unit


func _build_cell(coord: Vector2i, height: int, terrain: StringName = BattleCellState.TERRAIN_LAND) -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = coord
	cell.stack_layer = height
	cell.base_height = height
	cell.base_terrain = terrain
	cell.recalculate_runtime_values()
	return cell


func _build_large_unit_direction_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"large_unit_directional_regression"
	state.seed = TEST_SEED
	state.map_size = map_size
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = &"default"
	state.cells = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y), 0)
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _get_large_unit_direction_cases() -> Array[Dictionary]:
	return [
		{
			"label": "向右",
			"map_size": Vector2i(7, 7),
			"start_coord": Vector2i(1, 2),
			"next_anchor": Vector2i(2, 2),
			"barrier_from": Vector2i(2, 2),
			"barrier_to": Vector2i(3, 2),
			"partial_landing_coord": Vector2i(3, 2),
		},
		{
			"label": "向左",
			"map_size": Vector2i(7, 7),
			"start_coord": Vector2i(4, 2),
			"next_anchor": Vector2i(3, 2),
			"barrier_from": Vector2i(4, 2),
			"barrier_to": Vector2i(3, 2),
			"partial_landing_coord": Vector2i(3, 2),
		},
		{
			"label": "向下",
			"map_size": Vector2i(7, 7),
			"start_coord": Vector2i(2, 1),
			"next_anchor": Vector2i(2, 2),
			"barrier_from": Vector2i(2, 2),
			"barrier_to": Vector2i(2, 3),
			"partial_landing_coord": Vector2i(2, 3),
		},
		{
			"label": "向上",
			"map_size": Vector2i(7, 7),
			"start_coord": Vector2i(2, 4),
			"next_anchor": Vector2i(2, 3),
			"barrier_from": Vector2i(2, 4),
			"barrier_to": Vector2i(2, 3),
			"partial_landing_coord": Vector2i(2, 3),
		},
	]


func _set_wall_between(state: BattleState, from_coord: Vector2i, to_coord: Vector2i) -> void:
	var delta := to_coord - from_coord
	match delta:
		Vector2i.RIGHT:
			(state.cells.get(from_coord) as BattleCellState).set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())
		Vector2i.LEFT:
			(state.cells.get(to_coord) as BattleCellState).set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())
		Vector2i.DOWN:
			(state.cells.get(from_coord) as BattleCellState).set_edge_feature(Vector2i.DOWN, BattleEdgeFeatureState.make_wall())
		Vector2i.UP:
			(state.cells.get(to_coord) as BattleCellState).set_edge_feature(Vector2i.DOWN, BattleEdgeFeatureState.make_wall())


func _set_cell_height(state: BattleState, coord: Vector2i, height: int) -> void:
	var cell := state.cells.get(coord) as BattleCellState
	if cell == null:
		return
	cell.base_height = height
	cell.recalculate_runtime_values()
	state.mark_runtime_edges_dirty()


func _clone_cells(cells: Dictionary) -> Dictionary:
	var cloned: Dictionary = {}
	for coord_variant in cells.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		var cell := cells.get(coord_variant) as BattleCellState
		if cell == null:
			continue
		cloned[coord] = BattleCellState.from_dict(cell.to_dict())
	return cloned


func _clone_columns(columns: Dictionary) -> Dictionary:
	return BattleCellState.clone_columns(columns)


func _instantiate_board(
	state: BattleState,
	preview_target_coords: Array[Vector2i] = [],
	valid_target_coords: Array[Vector2i] = [],
	viewport_size: Vector2 = VIEWPORT_SIZE
) -> BattleBoard2D:
	var board := BattleBoardScene.instantiate()
	var board_2d := board as BattleBoard2D
	root.add_child(board)
	await process_frame
	var selected_coord := Vector2i.ZERO
	if not state.ally_unit_ids.is_empty():
		var selected_unit := state.units.get(state.ally_unit_ids[0]) as BattleUnitState
		if selected_unit != null:
			selected_coord = selected_unit.coord
	board_2d.set_viewport_size(viewport_size)
	board_2d.configure(state, selected_coord, preview_target_coords, valid_target_coords)
	await process_frame
	return board_2d


func _capture_layout_signature(layout: Dictionary) -> Array[String]:
	var lines: Array[String] = []
	lines.append("size:%s" % [layout.get("map_size", Vector2i.ZERO)])
	lines.append("player:%s" % [layout.get("player_coord", Vector2i.ZERO)])
	lines.append("enemy:%s" % [layout.get("enemy_coord", Vector2i.ZERO)])
	var coords: Array[Vector2i] = []
	for coord_variant in layout.get("cells", {}).keys():
		if coord_variant is Vector2i:
			coords.append(coord_variant)
	coords.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		if a.y == b.y:
			return a.x < b.x
		return a.y < b.y
	)
	for coord in coords:
		var cell := layout.get("cells", {}).get(coord) as BattleCellState
		if cell == null:
			continue
		var column_variant: Variant = layout.get("cell_columns", {}).get(coord, [])
		var column_size := 0
		if column_variant is Array:
			column_size = (column_variant as Array).size()
		lines.append("%d,%d|%s|%d|%s|%s|%s" % [
			coord.x,
			coord.y,
			str(cell.base_terrain),
			int(cell.current_height),
			"%s|stack=%d" % [",".join(_stringify_prop_ids(cell.prop_ids)), column_size],
			String(cell.edge_feature_east.feature_kind) if cell.edge_feature_east != null else "none",
			String(cell.edge_feature_south.feature_kind) if cell.edge_feature_south != null else "none",
		])
	return lines


func _capture_board_signature(board: BattleBoard2D) -> Array[String]:
	var lines: Array[String] = []
	var profile := board.get("_render_profile") as BattleBoardRenderProfile
	if profile != null:
		lines.append("render_profile|%s|%s|height_step=%.2f|asset_dir=%s" % [
			String(profile.terrain_profile_id),
			String(profile.render_profile_id),
			profile.visual_height_step,
			profile.asset_dir,
		])
	var layer_names: Array[String] = []
	layer_names.append_array(_build_layer_names("TopH", 0, MAX_RENDER_HEIGHT))
	layer_names.append_array(_build_layer_names("EdgeDropEastH", 1, MAX_RENDER_HEIGHT))
	layer_names.append_array(_build_layer_names("EdgeDropSouthH", 1, MAX_RENDER_HEIGHT))
	layer_names.append_array(_build_layer_names("WallEastH", 0, MAX_RENDER_HEIGHT))
	layer_names.append_array(_build_layer_names("WallSouthH", 0, MAX_RENDER_HEIGHT))
	layer_names.append_array(_build_layer_names("OverlayH", 0, MAX_RENDER_HEIGHT))
	layer_names.append_array(_build_layer_names("MarkerH", 0, MAX_RENDER_HEIGHT))
	for layer_name in layer_names:
		var layer := board.get_node(layer_name) as TileMapLayer
		if layer == null:
			continue
		var used_cells := layer.get_used_cells()
		used_cells.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
			if a.y == b.y:
				return a.x < b.x
			return a.y < b.y
		)
		for coord in used_cells:
			lines.append("%s|%d,%d|%d" % [layer_name, coord.x, coord.y, layer.get_cell_source_id(coord)])
	var prop_layer := board.get_node("PropLayer")
	for child in prop_layer.get_children():
		lines.append("prop|%s|%s|%d|%s" % [
			String(child.get_meta("prop_id", "")),
			str(child.get_meta("board_coord", Vector2i.ZERO)),
			int(child.z_index),
			str(child.position),
		])
	var unit_layer := board.get_node("UnitLayer")
	for child in unit_layer.get_children():
		lines.append("unit|%s|%d|%s" % [child.name, int(child.z_index), str(child.position)])
	return lines


func _get_layer_cell_image(layer: TileMapLayer, coord: Vector2i) -> Image:
	if layer == null or layer.tile_set == null:
		return null
	var source_id := layer.get_cell_source_id(coord)
	if source_id < 0:
		return null
	var atlas_source := layer.tile_set.get_source(source_id) as TileSetAtlasSource
	if atlas_source == null or atlas_source.texture == null:
		return null
	return atlas_source.texture.get_image()


func _assert_layer_source_geometry(
	layer: TileMapLayer,
	coord: Vector2i,
	expected_region_size: Vector2i,
	expected_origin: Vector2i,
	label: String
) -> void:
	_assert_true(layer != null, "[source] %s 应存在对应 TileMapLayer。" % label)
	if layer == null or layer.tile_set == null:
		return
	var source_id := layer.get_cell_source_id(coord)
	_assert_true(source_id >= 0, "[source] %s 应在测试坐标注册 source：%s" % [label, str(coord)])
	if source_id < 0:
		return
	var atlas_source := layer.tile_set.get_source(source_id) as TileSetAtlasSource
	_assert_true(atlas_source != null, "[source] %s source 应是 TileSetAtlasSource。" % label)
	if atlas_source == null:
		return
	_assert_eq(atlas_source.texture_region_size, expected_region_size, "[source] %s source 应使用独立 atlas_region_size。" % label)
	var tile_data := atlas_source.get_tile_data(Vector2i.ZERO, 0)
	_assert_true(tile_data != null, "[source] %s source 应创建 atlas tile data。" % label)
	if tile_data != null:
		_assert_eq(tile_data.texture_origin, expected_origin, "[source] %s source 应应用独立 visual_origin。" % label)


func _find_source_spec(profile: BattleBoardRenderProfile, source_key: StringName) -> Dictionary:
	if profile == null:
		return {}
	for source_spec in profile.get_source_specs():
		if StringName(source_spec.get("key", &"")) == source_key:
			return source_spec
	return {}


func _load_png_image(path: String) -> Image:
	if path.is_empty() or not FileAccess.file_exists(path):
		return null
	var file_bytes := FileAccess.get_file_as_bytes(path)
	if file_bytes.is_empty():
		return null
	var image := Image.new()
	if image.load_png_from_buffer(file_bytes) != OK:
		return null
	return image


func _assert_east_asset_neighbor_anchored(path: String, label: String) -> void:
	var image := _load_png_image(path)
	_assert_true(image != null, "%s 贴图应存在：%s" % [label, path])
	if image == null:
		return
	var bounds := _get_nontransparent_bounds(image, 48)
	_assert_true(bounds.has_area(), "%s 贴图应包含可见像素：%s" % [label, path])
	if not bounds.has_area():
		return
	_assert_true(
		bounds.position.x <= 1,
		"%s 在 east-neighbor 锚点下应占据 tile 左半边，才能回贴高地右边界：%s" % [label, path]
	)
	if label.contains("cliff"):
		_assert_true(
			bounds.position.y <= 1,
			"%s 应在 east-neighbor 锚点下覆盖上缘斜边，避免继续和 land 脱离：%s" % [label, path]
		)
	else:
		_assert_true(
			bounds.size.x <= 34,
			"%s 应保持半边宽度，不能再次退化成整条正面：%s" % [label, path]
		)


func _assert_south_asset_neighbor_anchored(path: String, label: String) -> void:
	var image := _load_png_image(path)
	_assert_true(image != null, "%s 贴图应存在：%s" % [label, path])
	if image == null:
		return
	var bounds := _get_nontransparent_bounds(image, 48)
	_assert_true(bounds.has_area(), "%s 贴图应包含可见像素：%s" % [label, path])
	if not bounds.has_area():
		return
	_assert_true(
		bounds.position.x >= 31,
		"%s 在 south-neighbor 锚点下应占据 tile 右半边，才能回贴高地左边界：%s" % [label, path]
	)
	if label.contains("cliff"):
		_assert_true(
			bounds.position.y <= 1,
			"%s 应在 south-neighbor 锚点下覆盖上缘斜边，避免继续和 land 脱离：%s" % [label, path]
		)
	else:
		_assert_true(
			bounds.size.x <= 34,
			"%s 应保持半边宽度，不能再次退化成整条正面：%s" % [label, path]
		)


func _get_nontransparent_bounds(image: Image, alpha_threshold: int = 1) -> Rect2i:
	if image == null:
		return Rect2i()
	var min_x := image.get_width()
	var min_y := image.get_height()
	var max_x := -1
	var max_y := -1
	for y in range(image.get_height()):
		for x in range(image.get_width()):
			var alpha := int(round(image.get_pixel(x, y).a * 255.0))
			if alpha < alpha_threshold:
				continue
			min_x = mini(min_x, x)
			min_y = mini(min_y, y)
			max_x = maxi(max_x, x)
			max_y = maxi(max_y, y)
	if max_x < min_x or max_y < min_y:
		return Rect2i()
	return Rect2i(min_x, min_y, max_x - min_x + 1, max_y - min_y + 1)


func _image_alpha_mask_matches(expected: Image, actual: Image, alpha_threshold: int = 1) -> bool:
	if expected == null or actual == null:
		return false
	if expected.get_width() != actual.get_width() or expected.get_height() != actual.get_height():
		return false
	for y in range(expected.get_height()):
		for x in range(expected.get_width()):
			var expected_visible := int(round(expected.get_pixel(x, y).a * 255.0)) >= alpha_threshold
			var actual_visible := int(round(actual.get_pixel(x, y).a * 255.0)) >= alpha_threshold
			if expected_visible != actual_visible:
				return false
	return true


func _get_expected_edge_render_coord(origin_coord: Vector2i, direction: Vector2i) -> Vector2i:
	if direction == Vector2i.RIGHT:
		return origin_coord + Vector2i.RIGHT
	if direction == Vector2i.DOWN:
		return origin_coord + Vector2i.DOWN
	return origin_coord


func _find_top_surface_overlap_point(board: BattleBoard2D, first_anchor: Vector2, second_anchor: Vector2) -> Vector2:
	var search_rect := Rect2(first_anchor, Vector2.ZERO).expand(second_anchor).grow(40.0)
	for y in range(int(floor(search_rect.position.y)), int(ceil(search_rect.end.y)) + 1):
		for x in range(int(floor(search_rect.position.x)), int(ceil(search_rect.end.x)) + 1):
			var point := Vector2(float(x), float(y))
			if board._point_hits_cell_top_surface(point, first_anchor) and board._point_hits_cell_top_surface(point, second_anchor):
				return point
	return Vector2.INF


func _compute_expected_profile_content_bounds(board: BattleBoard2D, state: BattleState) -> Rect2:
	var profile := board.get("_render_profile") as BattleBoardRenderProfile
	var input_layer := board.get_node("InputLayer") as TileMapLayer
	if profile == null or input_layer == null or state == null:
		return Rect2()
	var has_bounds := false
	var bounds := Rect2()
	for cell_variant in state.cells.values():
		var cell := cell_variant as BattleCellState
		if cell == null:
			continue
		var anchor := input_layer.map_to_local(cell.coord)
		anchor.y -= float(clampi(int(cell.current_height), 0, MAX_RENDER_HEIGHT)) * profile.visual_height_step
		var cell_rect := Rect2(anchor - profile.tile_half_size, profile.tile_half_size * 2.0)
		if not has_bounds:
			bounds = cell_rect
			has_bounds = true
		else:
			bounds = bounds.merge(cell_rect)
	if not has_bounds:
		return Rect2()
	var margin := profile.content_bounds_margin
	return bounds.grow_individual(margin.x, margin.y, margin.z, margin.w)


func _assert_camera_edges_cover_viewport(board: BattleBoard2D, viewport_size: Vector2, label: String) -> void:
	var profile := board.get("_render_profile") as BattleBoardRenderProfile
	var content_bounds: Rect2 = board.get("_content_bounds")
	var zoom := board.scale.x
	var left_edge := board.position.x + content_bounds.position.x * zoom
	var right_edge := board.position.x + (content_bounds.position.x + content_bounds.size.x) * zoom
	var top_edge := board.position.y + content_bounds.position.y * zoom
	var bottom_edge := board.position.y + (content_bounds.position.y + content_bounds.size.y) * zoom
	var margin := profile.camera_margin if profile != null else Vector2.ZERO
	_assert_true(
		left_edge <= margin.x + 1.0,
		"%s 后左边缘不应露出超过 render profile margin 的空白。" % label
	)
	_assert_true(
		right_edge >= viewport_size.x - margin.x - 1.0,
		"%s 后右边缘不应露出超过 render profile margin 的空白。" % label
	)
	_assert_true(
		top_edge <= margin.y + 1.0,
		"%s 后上边缘不应露出超过 render profile margin 的空白。" % label
	)
	_assert_true(
		bottom_edge >= viewport_size.y - margin.y - 1.0,
		"%s 后下边缘不应露出超过 render profile margin 的空白。" % label
	)


func _assert_tile_variant_variety(board: BattleBoard2D) -> void:
	var top_layers := _get_board_layers(board, "TopH", 0, MAX_RENDER_HEIGHT)
	var used_source_ids: Dictionary = {}
	for layer in top_layers:
		if layer == null:
			continue
		for coord in layer.get_used_cells():
			used_source_ids[layer.get_cell_source_id(coord)] = true
	_assert_true(
		used_source_ids.size() >= 2,
		"battle board 顶面 tile 应至少出现两个稳定变体。"
	)


func _assert_input_mapping(board: BattleBoard2D, coord: Vector2i) -> void:
	var input_layer := board.get_node("InputLayer") as TileMapLayer
	var viewport_position := input_layer.to_global(input_layer.map_to_local(coord))
	var mapped_coord := board._viewport_position_to_board_coord(viewport_position)
	var handled := board.handle_viewport_mouse_button(viewport_position, MOUSE_BUTTON_LEFT)
	_assert_true(handled, "BattleBoard2D 应处理来自 InputLayer 的左键点击。")
	_assert_eq(mapped_coord, coord, "InputLayer.local_to_map() 应与逻辑坐标一一对应。")


func _assert_drop_face_stacking(board: BattleBoard2D, layout: Dictionary) -> void:
	var cells: Dictionary = layout.get("cells", {})
	var east_layers := _get_board_layers(board, "EdgeDropEastH", 1, MAX_RENDER_HEIGHT)
	var south_layers := _get_board_layers(board, "EdgeDropSouthH", 1, MAX_RENDER_HEIGHT)
	for coord_variant in cells.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		var cell := cells.get(coord_variant) as BattleCellState
		if cell == null:
			continue
		var expected_east := _expected_height_drop(cells, coord, Vector2i.RIGHT, int(cell.current_height))
		var expected_south := _expected_height_drop(cells, coord, Vector2i.DOWN, int(cell.current_height))
		var east_render_coord := _get_expected_edge_render_coord(coord, Vector2i.RIGHT)
		var south_render_coord := _get_expected_edge_render_coord(coord, Vector2i.DOWN)
		_assert_eq(
			_count_used_layers(east_layers, east_render_coord),
			expected_east,
			"东侧 drop face 层数应等于高度差：%s" % str(coord)
		)
		_assert_eq(
			_count_used_layers(south_layers, south_render_coord),
			expected_south,
			"南侧 drop face 层数应等于高度差：%s" % str(coord)
		)


func _assert_prop_and_unit_sorting(board: BattleBoard2D) -> void:
	var prop_counts := {
		BattleBoardPropCatalog.PROP_SPIKE_BARRICADE: 0,
		BattleBoardPropCatalog.PROP_OBJECTIVE_MARKER: 0,
		BattleBoardPropCatalog.PROP_TENT: 0,
		BattleBoardPropCatalog.PROP_TORCH: 0,
	}
	var prop_layer := board.get_node("PropLayer")
	for child in prop_layer.get_children():
		var prop_id := StringName(String(child.get_meta("prop_id", "")))
		if prop_counts.has(prop_id):
			prop_counts[prop_id] = int(prop_counts.get(prop_id, 0)) + 1
		var sort_depth := int(child.get_meta("sort_depth", child.z_index))
		_assert_eq(
			int(child.z_index),
			sort_depth,
			"prop 节点应使用显式 render depth 排序：%s" % child.name
		)
	_assert_eq(
		int(prop_counts.get(BattleBoardPropCatalog.PROP_OBJECTIVE_MARKER, 0)),
		1,
		"PropLayer 应存在一个 objective marker。"
	)
	_assert_true(
		int(prop_counts.get(BattleBoardPropCatalog.PROP_TENT, 0)) >= 2,
		"PropLayer 应存在双方 camp tent。"
	)
	_assert_true(
		int(prop_counts.get(BattleBoardPropCatalog.PROP_TORCH, 0)) >= 2,
		"PropLayer 应存在 canyon torch。"
	)
	_assert_true(
		int(prop_counts.get(BattleBoardPropCatalog.PROP_SPIKE_BARRICADE, 0)) >= 1,
		"PropLayer 应为 spike 地格生成障碍物 scene。"
	)

	var unit_layer := board.get_node("UnitLayer")
	for child in unit_layer.get_children():
		var anchor_y := float(child.get_meta("sort_anchor_y", -1.0))
		var sort_depth := int(child.get_meta("sort_depth", child.z_index))
		_assert_eq(
			int(child.z_index),
			sort_depth,
			"unit 节点应使用显式 render depth 排序：%s" % child.name
		)


func _count_layout_prop(layout: Dictionary, prop_id: StringName) -> int:
	var count := 0
	for cell_variant in layout.get("cells", {}).values():
		var cell := cell_variant as BattleCellState
		if cell == null:
			continue
		if cell.prop_ids.has(prop_id):
			count += 1
	return count


func _collect_layout_prop_coords(layout: Dictionary, prop_id: StringName) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for coord_variant in layout.get("cells", {}).keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		var cell := layout.get("cells", {}).get(coord) as BattleCellState
		if cell == null or not cell.prop_ids.has(prop_id):
			continue
		coords.append(coord)
	return coords


func _extract_layout_coords(coords_variant: Variant) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	if coords_variant is not Array:
		return coords
	for coord_variant in coords_variant:
		if coord_variant is Vector2i:
			coords.append(coord_variant)
	return coords


func _assert_layout_spawn_coords_avoid_water(layout: Dictionary, label: String) -> void:
	var cells: Dictionary = layout.get("cells", {})
	var groups := [
		{
			"field": "player_coord",
			"coords": [layout.get("player_coord", Vector2i(-1, -1))],
		},
		{
			"field": "enemy_coord",
			"coords": [layout.get("enemy_coord", Vector2i(-1, -1))],
		},
		{
			"field": "ally_spawns",
			"coords": _extract_layout_coords(layout.get("ally_spawns", [])),
		},
		{
			"field": "enemy_spawns",
			"coords": _extract_layout_coords(layout.get("enemy_spawns", [])),
		},
	]
	for group_variant in groups:
		var group: Dictionary = group_variant
		var field_name := String(group.get("field", "spawn"))
		var coords: Array[Vector2i] = []
		var coords_variant = group.get("coords", [])
		if coords_variant is Array:
			for coord_variant in coords_variant:
				if coord_variant is Vector2i:
					coords.append(coord_variant)
		_assert_true(not coords.is_empty(), "%s 的 %s 不应为空。" % [label, field_name])
		for coord in coords:
			var cell := cells.get(coord) as BattleCellState
			_assert_true(cell != null, "%s 的 %s 必须指向有效战斗格：%s" % [label, field_name, str(coord)])
			if cell == null:
				continue
			_assert_true(
				not BattleTerrainRules.is_water_terrain(cell.base_terrain),
				"%s 的 %s 不应落在水域上：%s -> %s" % [label, field_name, str(coord), String(cell.base_terrain)]
			)


func _count_coords_on_or_left_of_x(coords: Array[Vector2i], x_limit: int) -> int:
	var count := 0
	for coord in coords:
		if coord.x <= x_limit:
			count += 1
	return count


func _count_coords_strictly_right_of_x(coords: Array[Vector2i], x_limit: int) -> int:
	var count := 0
	for coord in coords:
		if coord.x > x_limit:
			count += 1
	return count


func _assert_layout_uses_supported_props(layout: Dictionary) -> void:
	for cell_variant in layout.get("cells", {}).values():
		var cell := cell_variant as BattleCellState
		if cell == null:
			continue
		for prop_id in cell.prop_ids:
			_assert_true(
				BattleBoardPropCatalog.is_supported(prop_id),
				"战斗布局中的显式 prop_id 必须来自正式 prop catalog：%s" % String(prop_id)
			)


func _count_terrain_cells_in_x_range(layout: Dictionary, terrain_id: StringName, min_x: int, max_x: int) -> int:
	var count := 0
	for coord_variant in layout.get("cells", {}).keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		if coord.x < min_x or coord.x > max_x:
			continue
		var cell := layout.get("cells", {}).get(coord) as BattleCellState
		if cell == null:
			continue
		if cell.base_terrain == terrain_id:
			count += 1
	return count


func _collect_terrain_coords(layout: Dictionary, terrain_id: StringName) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for coord_variant in layout.get("cells", {}).keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		var cell := layout.get("cells", {}).get(coord) as BattleCellState
		if cell == null:
			continue
		if terrain_id == BattleCellState.TERRAIN_WATER:
			if not BattleTerrainRules.is_water_terrain(cell.base_terrain):
				continue
		elif cell.base_terrain != terrain_id:
			continue
		coords.append(coord)
	return coords


func _count_connected_components(coords: Array[Vector2i]) -> int:
	if coords.is_empty():
		return 0
	var coord_set: Dictionary = {}
	for coord in coords:
		coord_set[coord] = true

	var visited: Dictionary = {}
	var component_count := 0
	var neighbor_offsets: Array[Vector2i] = [
		Vector2i.LEFT,
		Vector2i.RIGHT,
		Vector2i.UP,
		Vector2i.DOWN,
	]
	for coord in coords:
		if visited.has(coord):
			continue
		component_count += 1
		var frontier: Array[Vector2i] = [coord]
		while not frontier.is_empty():
			var current: Vector2i = frontier.pop_front()
			if visited.has(current) or not coord_set.has(current):
				continue
			visited[current] = true
			for offset in neighbor_offsets:
				var neighbor := current + offset
				if coord_set.has(neighbor) and not visited.has(neighbor):
					frontier.append(neighbor)
	return component_count


func _find_narrow_assault_gate_info(layout: Dictionary) -> Dictionary:
	var cells: Dictionary = layout.get("cells", {})
	var map_size: Vector2i = layout.get("map_size", Vector2i.ZERO)
	if cells.is_empty() or map_size == Vector2i.ZERO:
		return {}
	var cell_columns_variant: Variant = layout.get("cell_columns", {})
	var cell_columns: Dictionary = cell_columns_variant if cell_columns_variant is Dictionary else BattleCellState.build_columns_from_surface_cells(cells)
	var edge_faces := _edge_service.build_edge_faces_for_cells(cells, map_size, cell_columns)
	var player_coord: Vector2i = layout.get("player_coord", Vector2i.ZERO)
	var enemy_coord: Vector2i = layout.get("enemy_coord", Vector2i.ZERO)
	var min_x := mini(player_coord.x, enemy_coord.x)
	var max_x := maxi(player_coord.x, enemy_coord.x) - 1
	var best_info := {}
	var best_score := 2147483647

	for gate_x in range(min_x, max_x + 1):
		var opening_count := _count_traversable_openings_for_seam(cells, map_size, edge_faces, gate_x)
		if opening_count <= 0:
			continue
		var left_reachable_count := _count_side_reachable_cells(cells, map_size, edge_faces, player_coord, 0, gate_x)
		var right_reachable_count := _count_side_reachable_cells(cells, map_size, edge_faces, enemy_coord, gate_x + 1, map_size.x - 1)
		if left_reachable_count <= 0 or right_reachable_count <= 0:
			continue

		var score := opening_count * 100 + absi(gate_x - int(map_size.x / 2)) * 10
		if score < best_score:
			best_score = score
			best_info = {
				"gate_x": gate_x,
				"opening_count": opening_count,
				"left_reachable_count": left_reachable_count,
				"right_reachable_count": right_reachable_count,
			}
	return best_info


func _find_holdout_push_line_info(layout: Dictionary) -> Dictionary:
	var cells: Dictionary = layout.get("cells", {})
	var map_size: Vector2i = layout.get("map_size", Vector2i.ZERO)
	if cells.is_empty() or map_size == Vector2i.ZERO:
		return {}
	var cell_columns_variant: Variant = layout.get("cell_columns", {})
	var cell_columns: Dictionary = cell_columns_variant if cell_columns_variant is Dictionary else BattleCellState.build_columns_from_surface_cells(cells)
	var edge_faces := _edge_service.build_edge_faces_for_cells(cells, map_size, cell_columns)
	var player_coord: Vector2i = layout.get("player_coord", Vector2i.ZERO)
	var enemy_coord: Vector2i = layout.get("enemy_coord", Vector2i.ZERO)
	var min_x := mini(player_coord.x, enemy_coord.x)
	var max_x := maxi(player_coord.x, enemy_coord.x) - 1
	var best_info := {}
	var best_score := -999999

	for hold_line_x in range(min_x, max_x + 1):
		var wall_count := _count_blocking_wall_segments_for_seam(cells, map_size, hold_line_x)
		if wall_count <= 0:
			continue
		var opening_count := _count_traversable_openings_for_seam(cells, map_size, edge_faces, hold_line_x)
		if opening_count <= 0:
			continue
		var left_reachable_count := _count_side_reachable_cells(cells, map_size, edge_faces, player_coord, 0, hold_line_x)
		var right_reachable_count := _count_side_reachable_cells(cells, map_size, edge_faces, enemy_coord, hold_line_x + 1, map_size.x - 1)
		if left_reachable_count <= 0 or right_reachable_count <= 0:
			continue

		var score := wall_count * 120 - opening_count * 35 - absi(hold_line_x - int(round(float(map_size.x) * 0.62))) * 12
		if score > best_score:
			best_score = score
			best_info = {
				"hold_line_x": hold_line_x,
				"wall_count": wall_count,
				"opening_count": opening_count,
				"left_reachable_count": left_reachable_count,
				"right_reachable_count": right_reachable_count,
			}
	return best_info


func _count_traversable_openings_for_seam(
	cells: Dictionary,
	map_size: Vector2i,
	edge_faces: Dictionary,
	gate_x: int
) -> int:
	var opening_count := 0
	for y in range(map_size.y):
		if _is_layout_edge_traversable(cells, edge_faces, Vector2i(gate_x, y), Vector2i(gate_x + 1, y)):
			opening_count += 1
	return opening_count


func _count_blocking_wall_segments_for_seam(cells: Dictionary, map_size: Vector2i, seam_x: int) -> int:
	var wall_count := 0
	for y in range(map_size.y):
		var cell := cells.get(Vector2i(seam_x, y)) as BattleCellState
		if cell == null:
			continue
		if cell.edge_feature_east != null and cell.edge_feature_east.blocks_occupancy:
			wall_count += 1
	return wall_count


func _count_side_reachable_cells(
	cells: Dictionary,
	map_size: Vector2i,
	edge_faces: Dictionary,
	start_coord: Vector2i,
	min_x: int,
	max_x: int
) -> int:
	if start_coord.x < min_x or start_coord.x > max_x:
		return 0
	var visited: Dictionary = {}
	var frontier: Array[Vector2i] = [start_coord]
	var neighbor_offsets: Array[Vector2i] = [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]
	var count := 0

	while not frontier.is_empty():
		var current: Vector2i = frontier.pop_front()
		if visited.has(current) or current.x < min_x or current.x > max_x:
			continue
		var cell := cells.get(current) as BattleCellState
		if cell == null or not cell.passable:
			continue
		visited[current] = true
		count += 1
		for offset in neighbor_offsets:
			var neighbor := current + offset
			if neighbor.x < min_x or neighbor.x > max_x or neighbor.y < 0 or neighbor.y >= map_size.y:
				continue
			if visited.has(neighbor):
				continue
			if _is_layout_edge_traversable(cells, edge_faces, current, neighbor):
				frontier.append(neighbor)
	return count


func _is_layout_edge_traversable(
	cells: Dictionary,
	edge_faces: Dictionary,
	from_coord: Vector2i,
	to_coord: Vector2i
) -> bool:
	var from_cell := cells.get(from_coord) as BattleCellState
	var to_cell := cells.get(to_coord) as BattleCellState
	if from_cell == null or to_cell == null:
		return false
	if not from_cell.passable or not to_cell.passable:
		return false
	return _edge_service.is_traversable_in_cache(edge_faces, from_coord, to_coord)


func _assert_canyon_height_range(layout: Dictionary) -> void:
	var min_height := 999999
	var max_height := -999999
	for cell_variant in layout.get("cells", {}).values():
		var cell := cell_variant as BattleCellState
		if cell == null:
			continue
		min_height = mini(min_height, int(cell.current_height))
		max_height = maxi(max_height, int(cell.current_height))
	_assert_true(
		min_height >= CANYON_MIN_HEIGHT,
		"canyon 地图最低高度应不低于 %d。" % CANYON_MIN_HEIGHT
	)
	_assert_true(
		max_height <= MAX_RENDER_HEIGHT,
		"canyon 地图最高高度应不超过 %d。" % MAX_RENDER_HEIGHT
	)


func _count_used_layers(layers: Array, coord: Vector2i) -> int:
	var count := 0
	for layer_variant in layers:
		var layer := layer_variant as TileMapLayer
		if layer == null:
			continue
		if layer.get_cell_source_id(coord) >= 0:
			count += 1
	return count


func _expected_height_drop(cells: Dictionary, coord: Vector2i, offset: Vector2i, source_height: int) -> int:
	var neighbor_height := 0
	var neighbor := cells.get(coord + offset) as BattleCellState
	if neighbor != null:
		neighbor_height = int(neighbor.current_height)
	return maxi(source_height - neighbor_height, 0)


func _get_board_layers(board: BattleBoard2D, prefix: String, start_height: int, end_height: int) -> Array[TileMapLayer]:
	var layers: Array[TileMapLayer] = []
	for height in range(start_height, end_height + 1):
		var layer := board.get_node("%s%d" % [prefix, height]) as TileMapLayer
		if layer != null:
			layers.append(layer)
	return layers


func _build_layer_names(prefix: String, start_height: int, end_height: int) -> Array[String]:
	var names: Array[String] = []
	for height in range(start_height, end_height + 1):
		names.append("%s%d" % [prefix, height])
	return names


func _stringify_prop_ids(prop_ids: Array[StringName]) -> Array[String]:
	var values: Array[String] = []
	for prop_id in prop_ids:
		values.append(String(prop_id))
	values.sort()
	return values


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_approx(actual: float, expected: float, tolerance: float, message: String) -> void:
	if absf(actual - expected) > tolerance:
		_failures.append(
			"%s | actual=%s expected=%s tolerance=%s" % [
				message,
				str(actual),
				str(expected),
				str(tolerance),
			]
		)


func _assert_rect_approx(actual: Rect2, expected: Rect2, tolerance: float, message: String) -> void:
	if actual.position.distance_to(expected.position) > tolerance or actual.size.distance_to(expected.size) > tolerance:
		_failures.append(
			"%s | actual=%s expected=%s tolerance=%s" % [
				message,
				str(actual),
				str(expected),
				str(tolerance),
			]
		)
