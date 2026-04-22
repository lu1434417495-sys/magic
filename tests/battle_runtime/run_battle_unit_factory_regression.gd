## 文件说明：该脚本属于战斗单位工厂边界相关的回归脚本，集中覆盖正式入口切换与单位刷新桥接。
## 审查重点：重点核对 BattleUnitFactory 是否成为战斗单位构建的正式入口，以及 runtime / gateway 之间的刷新桥接是否仍然稳定。
## 备注：后续若 battle runtime 的单位构建入口再次分流，需要同步更新此脚本的断言。

extends SceneTree

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle_runtime_module.gd")
const BattleUnitFactory = preload("res://scripts/systems/battle_unit_factory.gd")
const BattleUnitFactoryRuntime = preload("res://scripts/systems/battle_unit_factory_runtime.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle_unit_state.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PROGRESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/progression_content_registry.gd")
const UNIT_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_skill_progress.gd")

var _failures: Array[String] = []


class FakeCharacterGateway:
	extends RefCounted

	var party_state = null
	var member_state = null
	var attribute_snapshot = null

	func get_party_state():
		return party_state

	func get_member_state(member_id: StringName):
		if member_state != null and member_state.member_id == member_id:
			return member_state
		return null

	func get_member_attribute_snapshot(member_id: StringName):
		if member_state != null and member_state.member_id == member_id:
			return attribute_snapshot
		return null


class FakeRuntime:
	extends BattleUnitFactoryRuntime

	var _character_gateway: Object = null
	var _skill_defs: Dictionary = {}
	var _min_battle_surface_height := 4

	func get_character_gateway() -> Object:
		return _character_gateway

	func get_skill_defs() -> Dictionary:
		return _skill_defs

	func get_min_battle_surface_height() -> int:
		return _min_battle_surface_height


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_runtime_start_battle_uses_battle_unit_factory_without_character_party_builder()
	_test_battle_unit_factory_refreshes_from_character_gateway_snapshot()
	_test_battle_unit_factory_fallback_terrain_uses_runtime_bridge_contract()
	if _failures.is_empty():
		print("Battle unit factory regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle unit factory regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_runtime_start_battle_uses_battle_unit_factory_without_character_party_builder() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var party_state := _make_party_state([&"hero"])
	var gateway := FakeCharacterGateway.new()
	gateway.party_state = party_state

	var runtime := BattleRuntimeModule.new()
	runtime.setup(gateway, registry.get_skill_defs(), {}, {})

	var encounter_anchor := ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = &"factory_entry_smoke"
	encounter_anchor.display_name = "工厂入口测试"
	encounter_anchor.world_coord = Vector2i(8, 2)
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.region_tag = &"north_wilds"

	var state = runtime.start_battle(encounter_anchor, 1901, {
		"map_size": Vector2i(2, 2),
		"ally_spawns": [Vector2i(0, 0)],
		"enemy_spawns": [Vector2i(1, 1)],
	})
	_assert_true(state != null and not state.is_empty(), "战斗应能在没有 build_battle_party() 的 gateway 上正常创建。")
	_assert_eq(state.ally_unit_ids.size(), 1, "战斗应从 BattleUnitFactory 构建出 1 个友方单位。")
	if state != null and not state.is_empty() and not state.ally_unit_ids.is_empty():
		var unit = state.units.get(state.ally_unit_ids[0])
		_assert_true(unit != null, "友方单位应成功落入 battle state。")
		if unit != null:
			_assert_eq(unit.source_member_id, &"hero", "友方单位应保留原始 member_id。")
			_assert_eq(unit.display_name, "Hero", "友方单位应从 party_state 读取显示名。")


func _test_battle_unit_factory_refreshes_from_character_gateway_snapshot() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var gateway := FakeCharacterGateway.new()
	gateway.member_state = _make_member_state(&"hero")
	gateway.attribute_snapshot = _make_attribute_snapshot()

	var runtime := FakeRuntime.new()
	runtime._character_gateway = gateway
	runtime._skill_defs = registry.get_skill_defs()

	var factory := BattleUnitFactory.new()
	factory.setup(runtime)

	var unit := BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = &"hero"
	unit.source_member_id = &"hero"
	unit.display_name = "旧名"
	unit.body_size = 1
	unit.current_hp = 99
	unit.current_mp = 99
	unit.current_stamina = 99
	unit.current_aura = 99
	unit.current_ap = 1
	unit.known_active_skill_ids = [&"obsolete_skill"]
	unit.known_skill_level_map = {&"obsolete_skill": 1}

	factory.refresh_battle_unit(unit)

	_assert_eq(unit.body_size, 3, "刷新桥接应从 member_state 回写 body_size。")
	_assert_eq(unit.current_hp, 8, "刷新桥接应按属性快照上限回写 hp。")
	_assert_eq(unit.current_mp, 5, "刷新桥接应按属性快照上限回写 mp。")
	_assert_eq(unit.current_stamina, 7, "刷新桥接应按属性快照上限回写 stamina。")
	_assert_eq(unit.current_aura, 6, "刷新桥接应按属性快照上限回写 aura。")
	_assert_eq(unit.current_ap, 9, "刷新桥接应按属性快照回写 action points。")
	_assert_true(unit.known_active_skill_ids.has(&"warrior_heavy_strike"), "刷新桥接应从成长进度重建可用主动技能。")
	_assert_eq(int(unit.known_skill_level_map.get(&"warrior_heavy_strike", 0)), 2, "刷新桥接应从成长进度重建技能等级。")

	unit.current_hp = 3
	unit.known_active_skill_ids = []
	unit.known_skill_level_map.clear()
	factory.refresh_known_skills(unit)
	_assert_eq(unit.current_hp, 3, "仅刷新 known skills 时不应触碰运行时 HP。")
	_assert_true(unit.known_active_skill_ids.has(&"warrior_heavy_strike"), "known skills 刷新也应走同一 runtime bridge。")


func _test_battle_unit_factory_fallback_terrain_uses_runtime_bridge_contract() -> void:
	var runtime := FakeRuntime.new()
	runtime._min_battle_surface_height = 9
	var factory := BattleUnitFactory.new()
	factory.setup(runtime)

	var terrain_data := factory.build_terrain_data(null, 7, {
		"map_size": Vector2i(1, 1),
	})
	var cell = terrain_data.get("cells", {}).get(Vector2i.ZERO)
	_assert_true(cell != null, "fallback terrain 应创建默认地格。")
	if cell != null:
		_assert_eq(cell.base_height, 9, "fallback terrain 应读取 runtime bridge 的最小地表高度。")


func _make_party_state(member_ids: Array[StringName]) -> PartyState:
	var party_state := PARTY_STATE_SCRIPT.new()
	for member_id in member_ids:
		var member_state := _make_member_state(member_id)
		party_state.set_member_state(member_state)
		party_state.active_member_ids.append(member_id)
		if party_state.leader_member_id == &"":
			party_state.leader_member_id = member_id
	return party_state


func _make_member_state(member_id: StringName) -> PartyMemberState:
	var member_state := PARTY_MEMBER_STATE_SCRIPT.new()
	member_state.member_id = member_id
	member_state.display_name = String(member_id).capitalize()
	member_state.body_size = 3
	member_state.current_hp = 18
	member_state.current_mp = 6
	member_state.control_mode = &"manual"
	member_state.progression.unit_id = member_id
	member_state.progression.display_name = member_state.display_name
	member_state.progression.character_level = 1
	var skill_progress := UNIT_SKILL_PROGRESS_SCRIPT.new()
	skill_progress.skill_id = &"warrior_heavy_strike"
	skill_progress.is_learned = true
	skill_progress.skill_level = 2
	member_state.progression.set_skill_progress(skill_progress)
	return member_state


func _make_attribute_snapshot() -> AttributeSnapshot:
	var snapshot := ATTRIBUTE_SNAPSHOT_SCRIPT.new()
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 8)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, 5)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, 7)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX, 6)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS, 9)
	return snapshot


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
