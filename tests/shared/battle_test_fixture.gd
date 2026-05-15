extends RefCounted

const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleRuntimeTestHelpers = preload("res://tests/shared/battle_runtime_test_helpers.gd")


func build_state(options: Dictionary = {}) -> BattleState:
	var state := BattleState.new()
	state.battle_id = _option_string_name(options, "battle_id", &"shared_fixture_battle")
	state.phase = _option_string_name(options, "phase", &"unit_acting")
	state.map_size = _option_vector2i(options, "map_size", Vector2i(8, 8))
	state.timeline = BattleTimelineState.new()
	state.cells = build_cells(state.map_size, options)
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	if options.has("seed"):
		state.seed = int(options.get("seed", 0))
	if options.has("world_coord"):
		state.world_coord = _option_vector2i(options, "world_coord", Vector2i.ZERO)
	return state


func build_cells(map_size: Vector2i, options: Dictionary = {}) -> Dictionary:
	var cells: Dictionary = {}
	for y in range(maxi(map_size.y, 0)):
		for x in range(maxi(map_size.x, 0)):
			var coord := Vector2i(x, y)
			cells[coord] = build_cell(coord, options)
	return cells


func build_cell(coord: Vector2i, options: Dictionary = {}) -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = coord
	cell.base_terrain = _option_string_name(options, "base_terrain", BattleCellState.TERRAIN_LAND)
	cell.base_height = int(options.get("base_height", 4))
	cell.height_offset = int(options.get("height_offset", 0))
	cell.recalculate_runtime_values()
	return cell


func build_unit(unit_id: StringName, options: Dictionary = {}) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(options.get("display_name", String(unit_id)))
	unit.faction_id = _option_string_name(options, "faction_id", &"player")
	unit.current_ap = int(options.get("current_ap", 1))
	unit.current_move_points = int(options.get("current_move_points", BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN))
	unit.current_hp = int(options.get("current_hp", 100))
	unit.current_mp = int(options.get("current_mp", 0))
	unit.current_aura = int(options.get("current_aura", 0))
	unit.is_alive = bool(options.get("is_alive", true))
	unit.control_mode = _option_string_name(options, "control_mode", unit.control_mode)
	unit.source_member_id = _option_string_name(options, "source_member_id", unit.source_member_id)
	unit.attribute_snapshot.set_value(&"hp_max", int(options.get("hp_max", unit.current_hp)))
	if options.has("mp_max"):
		unit.attribute_snapshot.set_value(&"mp_max", int(options.get("mp_max", 0)))
	if options.has("aura_max"):
		unit.attribute_snapshot.set_value(&"aura_max", int(options.get("aura_max", 0)))
	unit.set_anchor_coord(_option_vector2i(options, "coord", Vector2i.ZERO))
	# options["seed_base_attributes"]=true 时补齐 6 维基础属性 + 派生 AC=8+agility_mod。
	# 默认不开是因为旧测试（如 FixedRollDamageResolver 用例）依赖"缺 AC 时 resolver 走另一路径"。
	# 需要走 BattleHitResolver 命中检定的 fixture 显式打开开关或单独调 BattleRuntimeTestHelpers。
	if bool(options.get("seed_base_attributes", false)):
		BattleRuntimeTestHelpers.seed_base_attributes_and_derive_ac(unit)
	return unit


func build_enemy_unit(unit_id: StringName, options: Dictionary = {}) -> BattleUnitState:
	var enemy_options := options.duplicate()
	enemy_options["faction_id"] = enemy_options.get("faction_id", &"enemy")
	enemy_options["current_hp"] = enemy_options.get("current_hp", 30)
	enemy_options["hp_max"] = enemy_options.get("hp_max", enemy_options.get("current_hp", 30))
	return build_unit(unit_id, enemy_options)


func add_units(state: BattleState, ally_units: Array, enemy_units: Array, active_unit_id: StringName = &"") -> void:
	if state == null:
		return
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []
	for unit_variant in ally_units:
		var unit := unit_variant as BattleUnitState
		if unit == null:
			continue
		state.units[unit.unit_id] = unit
		state.ally_unit_ids.append(unit.unit_id)
	for unit_variant in enemy_units:
		var unit := unit_variant as BattleUnitState
		if unit == null:
			continue
		state.units[unit.unit_id] = unit
		state.enemy_unit_ids.append(unit.unit_id)
	state.active_unit_id = active_unit_id
	if state.active_unit_id == &"" and not state.ally_unit_ids.is_empty():
		state.active_unit_id = state.ally_unit_ids[0]


func place_unit(grid_source, state: BattleState, unit: BattleUnitState, allow_occupied: bool = true) -> bool:
	var grid_service = _resolve_grid_service(grid_source)
	if grid_service == null or state == null or unit == null or not grid_service.has_method("place_unit"):
		return false
	return bool(grid_service.place_unit(state, unit, unit.coord, allow_occupied))


func install_state(runtime, state: BattleState) -> BattleState:
	if runtime != null:
		runtime._state = state
	return state


func build_skill_command(
	unit_id: StringName,
	skill_id: StringName,
	target_unit_id: StringName = &"",
	target_coord: Vector2i = Vector2i.ZERO
) -> BattleCommand:
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = unit_id
	command.skill_id = skill_id
	command.target_unit_id = target_unit_id
	command.target_coord = target_coord
	return command


func _resolve_grid_service(grid_source):
	if grid_source == null:
		return null
	if grid_source is Object and grid_source.has_method("get_grid_service"):
		return grid_source.get_grid_service()
	return grid_source


func _option_string_name(options: Dictionary, key: String, default_value: StringName) -> StringName:
	var value = options.get(key, default_value)
	if value is StringName:
		return value
	return StringName(String(value))


func _option_vector2i(options: Dictionary, key: String, default_value: Vector2i) -> Vector2i:
	var value = options.get(key, default_value)
	if value is Vector2i:
		return value
	return default_value
