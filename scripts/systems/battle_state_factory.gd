## 文件说明：该脚本属于战斗状态工厂相关的工厂脚本，集中维护角色网关、技能定义集合、敌方模板集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleStateFactory
extends RefCounted

const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle_unit_state.gd")
const BATTLE_TERRAIN_GENERATOR_SCRIPT = preload("res://scripts/systems/battle_terrain_generator.gd")
const ENCOUNTER_ROSTER_BUILDER_SCRIPT = preload("res://scripts/systems/encounter_roster_builder.gd")

## 字段说明：缓存角色网关实例，会参与运行时状态流转、系统协作和存档恢复。
var _character_gateway: Object = null
## 字段说明：缓存技能定义集合字典，集中保存可按键查询的运行时数据。
var _skill_defs: Dictionary = {}
## 字段说明：缓存敌方模板集合字典，集中保存可按键查询的运行时数据。
var _enemy_templates: Dictionary = {}
## 字段说明：缓存遭遇构建器实例，会参与运行时状态流转、系统协作和存档恢复。
var _encounter_builder: Object = null
## 字段说明：记录编队构建器，会参与运行时状态流转、系统协作和存档恢复。
var _roster_builder = ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()


func setup(
	character_gateway: Object = null,
	skill_defs: Dictionary = {},
	enemy_templates: Dictionary = {},
	encounter_builder: Object = null
) -> void:
	_character_gateway = character_gateway
	_skill_defs = skill_defs.duplicate(true)
	_enemy_templates = enemy_templates.duplicate(true)
	_encounter_builder = encounter_builder


func create_state(encounter_anchor, seed: int, context: Dictionary = {}):
	var battle_generation_system = BATTLE_TERRAIN_GENERATOR_SCRIPT.new()
	var generation_context := _build_generation_context(encounter_anchor, seed, context)
	var generated_battle: Dictionary = battle_generation_system.generate(generation_context)

	var battle_state := BATTLE_STATE_SCRIPT.new()
	battle_state.battle_id = _build_battle_id(encounter_anchor, seed)
	battle_state.seed = seed
	battle_state.phase = &"timeline_running"
	battle_state.map_size = generated_battle.get("map_size", Vector2i.ZERO)
	battle_state.world_coord = _read_vector2i(encounter_anchor, "world_coord", context.get("world_coord", Vector2i.ZERO))
	battle_state.encounter_anchor_id = StringName(String(_read_value(encounter_anchor, "entity_id", "")))
	battle_state.terrain_profile_id = StringName(String(
		context.get(
			"battle_terrain_profile",
			_read_value(encounter_anchor, "region_tag", generated_battle.get("terrain_profile_id", "default"))
		)
	))

	_populate_cells(battle_state, generated_battle.get("cells", {}))

	var roster_context := context.duplicate(true)
	roster_context["monster"] = generation_context.get("monster", {})
	roster_context["monster_display_name"] = String(generation_context.get("monster", {}).get("display_name", "敌人"))
	roster_context["player_coord"] = generated_battle.get("player_coord", Vector2i.ZERO)
	roster_context["enemy_coord"] = generated_battle.get("enemy_coord", Vector2i.ZERO)

	var ally_units := _build_ally_units(roster_context)
	var enemy_units := _build_enemy_units(encounter_anchor, roster_context)

	_place_units(battle_state, ally_units, generated_battle.get("player_coord", Vector2i.ZERO), context.get("ally_spawn_coords", []))
	_place_units(battle_state, enemy_units, generated_battle.get("enemy_coord", Vector2i.ZERO), context.get("enemy_spawn_coords", []))

	battle_state.log_entries.append("Battle runtime created.")
	battle_state.log_entries.append("Allies: %d, Enemies: %d" % [battle_state.ally_unit_ids.size(), battle_state.enemy_unit_ids.size()])
	if not battle_state.ally_unit_ids.is_empty():
		battle_state.active_unit_id = battle_state.ally_unit_ids[0]
	elif not battle_state.enemy_unit_ids.is_empty():
		battle_state.active_unit_id = battle_state.enemy_unit_ids[0]

	return battle_state


func _build_ally_units(context: Dictionary) -> Array:
	if _character_gateway != null and _character_gateway.has_method("build_battle_party"):
		var member_ids: Array = context.get("battle_member_ids", context.get("member_ids", []))
		if member_ids is Array and not member_ids.is_empty():
			var gateway_result = _character_gateway.call("build_battle_party", member_ids)
			if gateway_result is Array and not gateway_result.is_empty():
				return gateway_result

	if context.has("battle_party"):
		var battle_party: Variant = context.get("battle_party", [])
		if battle_party is Array and not battle_party.is_empty():
			return _normalize_unit_payloads(battle_party)

	return _roster_builder.build_ally_units({
		"ally_member_ids": context.get("ally_member_ids", [context.get("player_unit_id", "player_main")]),
		"player_display_name": context.get("player_display_name", "玩家"),
		"default_active_skill_ids": context.get("default_active_skill_ids", []),
		"default_hp": context.get("default_hp", 24),
		"default_mp": context.get("default_mp", 0),
	})


func _build_enemy_units(encounter_anchor, context: Dictionary) -> Array:
	if _encounter_builder != null and _encounter_builder.has_method("build_enemy_units"):
		var built_units = _encounter_builder.call("build_enemy_units", encounter_anchor, context)
		if built_units is Array and not built_units.is_empty():
			return built_units

	if context.has("enemy_units"):
		var enemy_units: Variant = context.get("enemy_units", [])
		if enemy_units is Array and not enemy_units.is_empty():
			return _normalize_unit_payloads(enemy_units)

	if not _enemy_templates.is_empty():
		var template_id := StringName(String(_read_value(encounter_anchor, "enemy_roster_template_id", "")))
		if _enemy_templates.has(template_id):
			var template_payload: Variant = _enemy_templates.get(template_id, {})
			if template_payload is Dictionary:
				var payload_array: Array = template_payload.get("units", [])
				if payload_array is Array and not payload_array.is_empty():
					return _normalize_unit_payloads(payload_array)

	return _roster_builder.build_enemy_units(encounter_anchor, {
		"monster": context.get("monster", {}),
		"monster_display_name": context.get("monster_display_name", "敌人"),
		"default_enemy_hp": context.get("default_enemy_hp", 12),
		"default_enemy_mp": context.get("default_enemy_mp", 0),
		"default_enemy_stamina": context.get("default_enemy_stamina", 0),
		"default_enemy_ap": context.get("default_enemy_ap", 0),
		"default_attack": context.get("default_attack", 10),
		"default_defense": context.get("default_defense", 5),
		"default_speed": context.get("default_speed", 100),
		"enemy_unit_count": context.get("enemy_unit_count", 1),
		"enemy_skill_ids": context.get("enemy_skill_ids", []),
	})


func _populate_cells(battle_state, cells_data: Dictionary) -> void:
	for key in cells_data.keys():
		var cell_variant = cells_data[key]
		var cell_state = cell_variant as BATTLE_CELL_STATE_SCRIPT
		if cell_state == null and cell_variant is Dictionary:
			cell_state = BATTLE_CELL_STATE_SCRIPT.from_dict(cell_variant)
		if cell_state == null:
			continue
		battle_state.cells[cell_state.coord] = cell_state
	battle_state.cell_columns = BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(battle_state.cells)


func _place_units(battle_state, units: Array, preferred_coord: Vector2i, preferred_spawn_coords: Variant) -> void:
	var coords: Array[Vector2i] = []
	if preferred_spawn_coords is Array:
		for coord_value in preferred_spawn_coords:
			if coord_value is Vector2i:
				coords.append(coord_value)

	if coords.is_empty():
		coords.append(preferred_coord)

	var occupied: Dictionary = {}
	for unit in units:
		if unit == null:
			continue
		var spawn_coord := _pick_spawn_coord(battle_state, coords, occupied)
		if spawn_coord == Vector2i(-1, -1):
			continue
		unit.coord = spawn_coord
		battle_state.units[unit.unit_id] = unit
		if unit.faction_id == &"player":
			battle_state.ally_unit_ids.append(unit.unit_id)
		else:
			battle_state.enemy_unit_ids.append(unit.unit_id)
		occupied[spawn_coord] = true


func _pick_spawn_coord(battle_state, preferred_coords: Array[Vector2i], occupied: Dictionary) -> Vector2i:
	for preferred_coord in preferred_coords:
		if _is_cell_available(battle_state, preferred_coord, occupied):
			return preferred_coord

	for preferred_coord in preferred_coords:
		var search_result := _find_nearest_available_cell(battle_state, preferred_coord, occupied)
		if search_result != Vector2i(-1, -1):
			return search_result

	return Vector2i(-1, -1)


func _find_nearest_available_cell(battle_state, origin: Vector2i, occupied: Dictionary) -> Vector2i:
	if _is_cell_available(battle_state, origin, occupied):
		return origin

	var frontier: Array[Vector2i] = [origin]
	var visited: Dictionary = {origin: true}
	while not frontier.is_empty():
		var current: Vector2i = frontier.pop_front()
		var directions: Array[Vector2i] = [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]
		for direction in directions:
			var candidate: Vector2i = current + direction
			if visited.has(candidate):
				continue
			visited[candidate] = true
			if _is_cell_available(battle_state, candidate, occupied):
				return candidate
			if battle_state.cells.get(candidate) != null:
				frontier.append(candidate)

	return Vector2i(-1, -1)


func _is_cell_available(battle_state, coord: Vector2i, occupied: Dictionary) -> bool:
	if coord == Vector2i(-1, -1):
		return false
	var cell_state = battle_state.cells.get(coord)
	if cell_state == null:
		return false
	if not cell_state.passable:
		return false
	if occupied.has(coord):
		return false
	return cell_state.occupant_unit_id == &""


func _normalize_unit_payloads(payloads: Array) -> Array:
	var results: Array = []
	for payload in payloads:
		if payload == null:
			continue
		if payload is Dictionary:
			results.append(BATTLE_UNIT_STATE_SCRIPT.from_dict(payload))
		elif payload.has_method("to_dict"):
			results.append(BATTLE_UNIT_STATE_SCRIPT.from_dict(payload.to_dict()))
		else:
			results.append(payload)
	return results


func _build_generation_context(encounter_anchor, seed: int, context: Dictionary) -> Dictionary:
	var result: Dictionary = context.duplicate(true)
	result["world_seed"] = seed
	result["monster"] = _build_monster_payload(encounter_anchor, context)
	result["world_coord"] = _read_vector2i(encounter_anchor, "world_coord", context.get("world_coord", Vector2i.ZERO))
	result["action_points"] = int(context.get("action_points", 6))
	return result


func _build_monster_payload(encounter_anchor, context: Dictionary) -> Dictionary:
	var monster: Variant = context.get("monster", {})
	if monster is Dictionary and not monster.is_empty():
		return monster.duplicate(true)

	var result: Dictionary = {
		"entity_id": "wild",
		"display_name": "野怪",
		"faction_id": "hostile",
		"enemy_roster_template_id": "",
		"region_tag": "",
	}
	if encounter_anchor == null:
		return result

	result["entity_id"] = _read_value(encounter_anchor, "entity_id", result["entity_id"])
	result["display_name"] = _read_value(encounter_anchor, "display_name", result["display_name"])
	result["faction_id"] = _read_value(encounter_anchor, "faction_id", result["faction_id"])
	result["enemy_roster_template_id"] = _read_value(encounter_anchor, "enemy_roster_template_id", result["enemy_roster_template_id"])
	result["region_tag"] = _read_value(encounter_anchor, "region_tag", result["region_tag"])
	return result


func _build_battle_id(encounter_anchor, seed: int) -> StringName:
	var anchor_id := String(_read_value(encounter_anchor, "entity_id", "wild"))
	return StringName("%s_%d" % [anchor_id, seed])


func _read_vector2i(source, key: String, fallback: Vector2i) -> Vector2i:
	var value = _read_value(source, key, fallback)
	if value is Vector2i:
		return value
	return fallback


func _read_value(source, key: String, fallback):
	if source == null:
		return fallback
	if source is Dictionary:
		return source.get(key, fallback)
	if source.has_method("get"):
		return source.get(key, fallback)
	return fallback
