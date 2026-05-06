## 文件说明：该脚本属于战斗单位工厂边界相关的回归脚本，集中覆盖正式入口切换与单位刷新桥接。
## 审查重点：重点核对 BattleUnitFactory 是否成为战斗单位构建的正式入口，以及 runtime / gateway 之间的刷新桥接是否仍然稳定。
## 备注：后续若 battle runtime 的单位构建入口再次分流，需要同步更新此脚本的断言。

extends SceneTree

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleUnitFactory = preload("res://scripts/systems/battle/runtime/battle_unit_factory.gd")
const BattleUnitFactoryRuntime = preload("res://scripts/systems/battle/runtime/battle_unit_factory_runtime.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CHARACTER_MANAGEMENT_MODULE_SCRIPT = preload("res://scripts/systems/progression/character_management_module.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")
const EQUIPMENT_STATE_SCRIPT = preload("res://scripts/player/equipment/equipment_state.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const ITEM_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/warehouse/item_content_registry.gd")
const ITEM_DEF_SCRIPT = preload("res://scripts/player/warehouse/item_def.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PROGRESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/progression_content_registry.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const UNIT_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_skill_progress.gd")
const WAREHOUSE_STACK_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_stack_state.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const WEAPON_DAMAGE_DICE_DEF_SCRIPT = preload("res://scripts/player/warehouse/weapon_damage_dice_def.gd")
const WEAPON_PROFILE_DEF_SCRIPT = preload("res://scripts/player/warehouse/weapon_profile_def.gd")

var _failures: Array[String] = []


class FakeCharacterGateway:
	extends RefCounted

	var party_state = null
	var member_state = null
	var attribute_snapshot = null
	var weapon_projection: Dictionary = {}

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

	func get_member_attribute_snapshot_for_equipment_view(member_id: StringName, _equipment_view):
		if member_state != null and member_state.member_id == member_id:
			return attribute_snapshot
		return null

	func get_member_weapon_projection(member_id: StringName) -> Dictionary:
		if member_state != null and member_state.member_id == member_id:
			return weapon_projection.duplicate(true)
		return {}

	func get_member_weapon_projection_for_equipment_view(member_id: StringName, _equipment_view) -> Dictionary:
		if member_state != null and member_state.member_id == member_id:
			return weapon_projection.duplicate(true)
		return {}


class FakeTerrainGenerator:
	extends RefCounted

	var last_context: Dictionary = {}

	func generate(_encounter_anchor, _seed: int, context: Dictionary) -> Dictionary:
		last_context = context.duplicate(true)
		return {}


class FakeRuntime:
	extends BattleUnitFactoryRuntime

	var _character_gateway: Object = null
	var _skill_defs: Dictionary = {}
	var _terrain_generator: Object = null
	var _min_battle_surface_height := 4

	func get_character_gateway() -> Object:
		return _character_gateway

	func get_skill_defs() -> Dictionary:
		return _skill_defs

	func get_terrain_generator():
		return _terrain_generator

	func get_min_battle_surface_height() -> int:
		return _min_battle_surface_height


class RuntimeUnitFactoryStub:
	extends RefCounted

	var ally_units: Array = []
	var enemy_units: Array = []
	var terrain_data: Dictionary = {}

	func setup(_runtime) -> void:
		pass

	func dispose() -> void:
		pass

	func build_ally_units(_party_state, _context: Dictionary) -> Array:
		return ally_units

	func build_enemy_units(_encounter_anchor, _context: Dictionary) -> Array:
		return enemy_units

	func build_terrain_data(_encounter_anchor, _seed: int, _context: Dictionary) -> Dictionary:
		return terrain_data.duplicate(true)

	func refresh_battle_unit(_unit_state) -> void:
		pass


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_attribute_service_exposes_default_character_action_threshold()
	_test_battle_unit_factory_context_uses_only_ally_member_ids()
	_test_runtime_start_battle_uses_battle_unit_factory_without_character_party_builder()
	_test_runtime_start_battle_clones_party_backpack_view()
	_test_battle_local_views_write_back_to_party_state()
	_test_battle_local_writeback_detects_instance_conflict_invariant()
	_test_battle_unit_factory_refreshes_from_character_gateway_snapshot()
	_test_battle_unit_factory_clones_explicit_unit_charge_state()
	_test_battle_unit_factory_projects_player_equipment_weapon_profiles()
	_test_battle_unit_factory_uses_battle_local_equipment_view_for_refresh()
	_test_battle_unit_factory_fallback_enemy_seeds_six_base_attributes()
	_test_enemy_resource_sync_handles_missing_attribute_snapshot()
	_test_battle_unit_factory_no_longer_builds_manual_fallback_terrain()
	_test_runtime_requires_formal_terrain_profile_id_from_generator()
	if _failures.is_empty():
		print("Battle unit factory regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle unit factory regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_attribute_service_exposes_default_character_action_threshold() -> void:
	var member_state := _make_member_state(&"hero")
	var service := ATTRIBUTE_SERVICE_SCRIPT.new()
	service.setup(member_state.progression, {}, {})
	var snapshot = service.get_snapshot()
	_assert_eq(
		snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD),
		ATTRIBUTE_SERVICE_SCRIPT.DEFAULT_CHARACTER_ACTION_THRESHOLD,
		"角色属性快照应暴露默认 action_threshold。"
	)
	_assert_true(
		not snapshot.has_value(ATTRIBUTE_SERVICE_SCRIPT.WEAPON_ATTACK_RANGE),
		"角色属性快照不应再默认暴露旧 weapon_attack_range 战斗字段。"
	)


func _test_battle_unit_factory_context_uses_only_ally_member_ids() -> void:
	var factory := BattleUnitFactory.new()
	var legacy_units: Array = factory.build_ally_units(null, {
		"battle_member_ids": [&"legacy_battle_member"],
		"member_ids": [&"legacy_member"],
	})
	_assert_eq(legacy_units.size(), 0, "旧 battle_member_ids / member_ids-only context 不应再生成友方单位。")

	var official_units: Array = factory.build_ally_units(null, {
		"ally_member_ids": [&"hero"],
		"battle_member_ids": [&"legacy_battle_member"],
		"member_ids": [&"legacy_member"],
	})
	_assert_eq(official_units.size(), 1, "正式 ally_member_ids context 应继续生成友方单位。")
	if official_units.is_empty():
		return
	var unit = official_units[0]
	_assert_eq(unit.unit_id, &"hero", "正式 ally_member_ids 应决定生成的友方 unit_id。")
	_assert_eq(unit.source_member_id, &"hero", "正式 ally_member_ids 应决定生成的 source_member_id。")


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
		"battle_map_size": Vector2i(7, 7),
	})
	_assert_true(state != null and not state.is_empty(), "战斗应能在没有 build_battle_party() 的 gateway 上正常创建。")
	_assert_eq(state.ally_unit_ids.size(), 1, "战斗应从 BattleUnitFactory 构建出 1 个友方单位。")
	if state != null and not state.is_empty() and not state.ally_unit_ids.is_empty():
		var unit = state.units.get(state.ally_unit_ids[0])
		_assert_true(unit != null, "友方单位应成功落入 battle state。")
		if unit != null:
			_assert_eq(unit.source_member_id, &"hero", "友方单位应保留原始 member_id。")
			_assert_eq(unit.display_name, "Hero", "友方单位应从 party_state 读取显示名。")
			_assert_eq(
				unit.action_threshold,
				ATTRIBUTE_SERVICE_SCRIPT.DEFAULT_CHARACTER_ACTION_THRESHOLD,
				"友方单位应从角色属性读取默认 action_threshold。"
			)


func _test_runtime_start_battle_clones_party_backpack_view() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var party_state := _make_party_state([&"hero"])
	var member_state = party_state.get_member_state(&"hero")
	member_state.equipment_state = EQUIPMENT_STATE_SCRIPT.new()
	member_state.equipment_state.set_equipped_entry(
		&"main_hand",
		&"bronze_sword",
		_slot_ids([&"main_hand"]),
		_make_equipment_instance(&"bronze_sword", &"equipped_bronze_001")
	)
	party_state.warehouse_state.stacks = [_make_stack(&"healing_herb", 2)]
	party_state.warehouse_state.equipment_instances = [EQUIPMENT_INSTANCE_STATE_SCRIPT.create(&"bronze_sword", &"backpack_bronze_001")]
	var gateway := FakeCharacterGateway.new()
	gateway.party_state = party_state

	var runtime := BattleRuntimeModule.new()
	runtime.setup(gateway, registry.get_skill_defs(), {}, {})

	var encounter_anchor := ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = &"backpack_view_smoke"
	encounter_anchor.display_name = "背包 view 测试"
	encounter_anchor.world_coord = Vector2i(8, 2)
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.region_tag = &"north_wilds"

	var state = runtime.start_battle(encounter_anchor, 1902, {
		"battle_map_size": Vector2i(7, 7),
	})
	_assert_true(state != null and not state.is_empty(), "战斗开始应能创建 battle state 以承载队伍共享背包 view。")
	if state == null or state.is_empty():
		return

	var battle_backpack_view = state.get_party_backpack_view()
	_assert_true(battle_backpack_view != party_state.warehouse_state, "battle-local 队伍共享背包 view 不应直接引用 PartyState.warehouse_state。")
	_assert_eq(_backpack_stack_signature(battle_backpack_view), ["healing_herb:2"], "battle-local 队伍共享背包 view 应复制开战前普通堆叠。")
	_assert_eq(_backpack_instance_signature(battle_backpack_view), ["bronze_sword"], "battle-local 队伍共享背包 view 应复制开战前装备实例。")

	battle_backpack_view.stacks[0].quantity = 7
	battle_backpack_view.equipment_instances.clear()
	_assert_eq(_backpack_stack_signature(party_state.warehouse_state), ["healing_herb:2"], "修改 battle-local 堆叠不应回写 PartyState 仓库。")
	_assert_eq(_backpack_instance_signature(party_state.warehouse_state), ["bronze_sword"], "修改 battle-local 装备实例不应回写 PartyState 仓库。")

	var unit = state.units.get(state.ally_unit_ids[0]) if not state.ally_unit_ids.is_empty() else null
	_assert_true(unit != null, "战斗 state 应持有友方单位以承载 battle-local equipment view。")
	if unit == null:
		return
	var equipment_view = state.get_unit_equipment_view(unit.unit_id)
	_assert_true(equipment_view != null, "BattleState 应能读取单位 battle-local equipment view。")
	_assert_true(equipment_view != member_state.equipment_state, "单位 battle-local equipment view 不应直接引用 PartyMemberState.equipment_state。")
	_assert_eq(
		String(equipment_view.get_equipped_instance_id(&"main_hand")),
		"equipped_bronze_001",
		"单位 battle-local equipment view 应保留装备实例 ID。"
	)
	equipment_view.clear_entry_slot(&"main_hand")
	_assert_eq(
		String(member_state.equipment_state.get_equipped_instance_id(&"main_hand")),
		"equipped_bronze_001",
		"修改单位 battle-local equipment view 不应回写 PartyMemberState.equipment_state。"
	)


func _test_battle_local_views_write_back_to_party_state() -> void:
	var party_state := _make_party_state([&"hero"])
	var member_state = party_state.get_member_state(&"hero")
	member_state.equipment_state = EQUIPMENT_STATE_SCRIPT.new()
	member_state.equipment_state.set_equipped_entry(
		&"main_hand",
		&"bronze_sword",
		_slot_ids([&"main_hand"]),
		_make_equipment_instance(&"bronze_sword", &"party_sword_001")
	)
	party_state.warehouse_state.stacks = [_make_stack(&"healing_herb", 2)]
	party_state.warehouse_state.equipment_instances = [_make_equipment_instance(&"iron_greatsword", &"backpack_greatsword_001")]

	var battle_state := BATTLE_STATE_SCRIPT.new()
	battle_state.phase = &"battle_ended"
	battle_state.set_party_backpack_view(party_state.warehouse_state)
	var unit_state := BATTLE_UNIT_STATE_SCRIPT.new()
	unit_state.unit_id = &"hero"
	unit_state.source_member_id = &"hero"
	unit_state.faction_id = &"player"
	unit_state.set_equipment_view(member_state.equipment_state)
	battle_state.units[unit_state.unit_id] = unit_state
	battle_state.ally_unit_ids.append(unit_state.unit_id)

	var battle_backpack = battle_state.get_party_backpack_view()
	battle_backpack.stacks[0].quantity = 5
	battle_backpack.equipment_instances = [_make_equipment_instance(&"bronze_sword", &"party_sword_001")]
	unit_state.get_equipment_view().set_equipped_entry(
		&"main_hand",
		&"iron_greatsword",
		_slot_ids([&"main_hand", &"off_hand"]),
		_make_equipment_instance(&"iron_greatsword", &"backpack_greatsword_001")
	)

	_assert_eq(String(member_state.equipment_state.get_equipped_item_id(&"main_hand")), "bronze_sword", "提交前 battle-local 换装不应直接改 PartyMemberState。")
	_assert_eq(_backpack_stack_signature(party_state.warehouse_state), ["healing_herb:2"], "提交前 battle-local 背包数量不应直接改 PartyState。")
	_assert_eq(_backpack_instance_id_signature(party_state.warehouse_state), ["backpack_greatsword_001"], "提交前 battle-local 背包实例不应直接改 PartyState。")

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade._party_state = party_state
	var commit_result: Dictionary = facade._commit_battle_local_views_to_party_state(battle_state, party_state)
	_assert_true(bool(commit_result.get("ok", false)), "battle end writeback 应成功提交 battle-local 装备与背包 view。")

	var committed_party = facade._party_state
	var committed_member = committed_party.get_member_state(&"hero")
	_assert_eq(String(committed_member.equipment_state.get_equipped_item_id(&"main_hand")), "iron_greatsword", "writeback 后主手应使用 battle-local 装备。")
	_assert_eq(String(committed_member.equipment_state.get_equipped_item_id(&"off_hand")), "iron_greatsword", "writeback 后副手应被双手武器占用。")
	_assert_eq(String(committed_member.equipment_state.get_equipped_instance_id(&"main_hand")), "backpack_greatsword_001", "writeback 后应保留战中换装实例 ID。")
	_assert_eq(_backpack_stack_signature(committed_party.warehouse_state), ["healing_herb:5"], "writeback 后应提交 battle-local 背包堆叠。")
	_assert_eq(_backpack_instance_id_signature(committed_party.warehouse_state), ["party_sword_001"], "writeback 后应提交 battle-local 背包装备实例。")

	var restored_party = PARTY_STATE_SCRIPT.from_dict(committed_party.to_dict())
	_assert_true(restored_party != null, "battle-local writeback 后 PartyState 应能 round-trip。")
	if restored_party != null:
		var restored_member = restored_party.get_member_state(&"hero")
		_assert_eq(String(restored_member.equipment_state.get_equipped_instance_id(&"main_hand")), "backpack_greatsword_001", "round-trip 后应保留写回的装备实例 ID。")
		_assert_eq(_backpack_instance_id_signature(restored_party.warehouse_state), ["party_sword_001"], "round-trip 后应保留写回的背包实例 ID。")


func _test_battle_local_writeback_detects_instance_conflict_invariant() -> void:
	var party_state := _make_party_state([&"hero"])
	var member_state = party_state.get_member_state(&"hero")
	member_state.equipment_state = EQUIPMENT_STATE_SCRIPT.new()
	member_state.equipment_state.set_equipped_entry(
		&"main_hand",
		&"bronze_sword",
		_slot_ids([&"main_hand"]),
		_make_equipment_instance(&"bronze_sword", &"party_sword_001")
	)
	party_state.warehouse_state.equipment_instances = [_make_equipment_instance(&"iron_greatsword", &"shared_conflict_001")]

	var battle_state := BATTLE_STATE_SCRIPT.new()
	battle_state.phase = &"battle_ended"
	battle_state.set_party_backpack_view(party_state.warehouse_state)
	var unit_state := BATTLE_UNIT_STATE_SCRIPT.new()
	unit_state.unit_id = &"hero"
	unit_state.source_member_id = &"hero"
	unit_state.faction_id = &"player"
	unit_state.set_equipment_view(member_state.equipment_state)
	unit_state.get_equipment_view().set_equipped_entry(
		&"main_hand",
		&"iron_greatsword",
		_slot_ids([&"main_hand"]),
		_make_equipment_instance(&"iron_greatsword", &"shared_conflict_001")
	)
	battle_state.units[unit_state.unit_id] = unit_state
	battle_state.ally_unit_ids.append(unit_state.unit_id)

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade._party_state = party_state
	var commit_result: Dictionary = facade._commit_battle_local_views_to_party_state(battle_state, party_state)
	_assert_true(not bool(commit_result.get("ok", true)), "重复实例 ID 应被 battle-local writeback 内部不变量校验检测出来。")
	_assert_eq(String(commit_result.get("error_code", "")), "battle_local_writeback_instance_conflict", "重复实例 ID 应暴露稳定内部不变量错误码。")
	_assert_eq(String(member_state.equipment_state.get_equipped_instance_id(&"main_hand")), "party_sword_001", "writeback 不变量失败不应修改 PartyMemberState 装备。")
	_assert_eq(_backpack_instance_id_signature(party_state.warehouse_state), ["shared_conflict_001"], "writeback 不变量失败不应修改 PartyState 背包。")


func _test_battle_unit_factory_refreshes_from_character_gateway_snapshot() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var gateway := FakeCharacterGateway.new()
	gateway.member_state = _make_member_state(&"hero")
	gateway.attribute_snapshot = _make_attribute_snapshot()
	gateway.weapon_projection = {
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "factory_blade",
		"weapon_profile_type_id": "shortsword",
		"weapon_current_grip": "one_handed",
		"weapon_attack_range": 2,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 6, "flat_bonus": 0},
		"weapon_physical_damage_tag": "physical_pierce",
	}

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
	_assert_eq(String(unit.body_size_category), "large", "刷新桥接应从 member_state 回写 body_size_category。")
	_assert_eq(unit.current_hp, 8, "刷新桥接应按属性快照上限回写 hp。")
	_assert_eq(unit.current_mp, 5, "刷新桥接应按属性快照上限回写 mp。")
	_assert_eq(unit.current_stamina, 7, "刷新桥接应按属性快照上限回写 stamina。")
	_assert_eq(unit.current_aura, 6, "刷新桥接应按属性快照上限回写 aura。")
	_assert_eq(unit.current_ap, 9, "刷新桥接应按属性快照回写 action points。")
	_assert_eq(unit.action_threshold, 30, "刷新桥接应按属性快照回写 action_threshold。")
	_assert_eq(String(unit.weapon_profile_kind), "equipped", "刷新桥接应从角色网关回写武器投影 kind。")
	_assert_eq(String(unit.weapon_profile_type_id), "shortsword", "刷新桥接应从角色网关回写 weapon profile type id。")
	_assert_eq(unit.weapon_attack_range, 2, "刷新桥接应从角色网关回写 weapon_attack_range。")
	_assert_eq(int(unit.weapon_one_handed_dice.get("dice_sides", 0)), 6, "刷新桥接应从角色网关回写一手骰。")
	_assert_eq(String(unit.weapon_physical_damage_tag), "physical_pierce", "刷新桥接应从角色网关回写武器伤害类型。")
	_assert_true(unit.known_active_skill_ids.has(&"warrior_heavy_strike"), "刷新桥接应从成长进度重建可用主动技能。")
	_assert_true(unit.known_active_skill_ids.has(&"basic_attack"), "刷新桥接应补入内建基础攻击。")
	_assert_eq(int(unit.known_skill_level_map.get(&"warrior_heavy_strike", 0)), 2, "刷新桥接应从成长进度重建技能等级。")
	_assert_eq(int(unit.known_skill_level_map.get(&"basic_attack", 0)), 1, "内建基础攻击应按 1 级进入战斗单位。")

	unit.current_hp = 3
	unit.known_active_skill_ids = []
	unit.known_skill_level_map.clear()
	factory.refresh_known_skills(unit)
	_assert_eq(unit.current_hp, 3, "仅刷新 known skills 时不应触碰运行时 HP。")
	_assert_true(unit.known_active_skill_ids.has(&"warrior_heavy_strike"), "known skills 刷新也应走同一 runtime bridge。")
	_assert_true(unit.known_active_skill_ids.has(&"basic_attack"), "known skills 刷新也应保留内建基础攻击。")


func _test_battle_unit_factory_clones_explicit_unit_charge_state() -> void:
	var factory := BattleUnitFactory.new()
	var explicit_unit = BATTLE_UNIT_STATE_SCRIPT.new()
	explicit_unit.unit_id = &"explicit_charge_unit"
	explicit_unit.display_name = "Explicit Charge Unit"
	explicit_unit.faction_id = &"player"
	explicit_unit.control_mode = &"manual"
	explicit_unit.body_size = 2
	explicit_unit.body_size_category = &"medium"
	explicit_unit.current_hp = 10
	explicit_unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 10)
	explicit_unit.per_battle_charges = {&"dragon_breath": 1}
	explicit_unit.per_turn_charges = {&"nimble_escape": 1}
	explicit_unit.per_turn_charge_limits = {&"nimble_escape": 1}

	var units: Array = factory.build_ally_units(null, {"battle_party": [explicit_unit]})
	_assert_eq(units.size(), 1, "显式 BattleUnitState 输入应被正规化为 1 个单位。")
	if units.is_empty():
		return
	var cloned_unit = units[0]
	_assert_true(cloned_unit != explicit_unit, "显式 BattleUnitState 输入应通过 clone 复制，避免共享运行态。")
	_assert_eq(int(cloned_unit.per_battle_charges.get(&"dragon_breath", -1)), 1, "显式 BattleUnitState clone 应保留 per_battle charge。")
	_assert_eq(int(cloned_unit.per_turn_charges.get(&"nimble_escape", -1)), 1, "显式 BattleUnitState clone 应保留 per_turn charge。")
	_assert_eq(int(cloned_unit.per_turn_charge_limits.get(&"nimble_escape", -1)), 1, "显式 BattleUnitState clone 应保留 per_turn charge limit。")


func _test_battle_unit_factory_projects_player_equipment_weapon_profiles() -> void:
	var progression_registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var item_defs := ITEM_CONTENT_REGISTRY_SCRIPT.new().get_item_defs().duplicate()
	item_defs[&"training_longsword"] = _make_weapon_item_def(
		&"training_longsword",
		&"longsword",
		&"physical_slash",
		1,
		_make_weapon_dice(1, 8, 0),
		_make_weapon_dice(1, 10, 0),
		[&"versatile"]
	)

	var party_state := _make_party_state([&"hero"])
	var member_state = party_state.get_member_state(&"hero")
	var character_gateway := CHARACTER_MANAGEMENT_MODULE_SCRIPT.new()
	character_gateway.setup(
		party_state,
		progression_registry.get_skill_defs(),
		progression_registry.get_profession_defs(),
		{},
		item_defs
	)
	var runtime := FakeRuntime.new()
	runtime._character_gateway = character_gateway
	runtime._skill_defs = progression_registry.get_skill_defs()
	var factory := BattleUnitFactory.new()
	factory.setup(runtime)

	member_state.equipment_state = EQUIPMENT_STATE_SCRIPT.new()
	var unarmed = _build_single_ally_unit(factory, party_state, "空手")
	if unarmed != null:
		_assert_eq(String(unarmed.weapon_profile_kind), "unarmed", "空手玩家单位应投影为空手武器 kind。")
		_assert_eq(String(unarmed.weapon_profile_type_id), "unarmed", "空手玩家单位应投影 unarmed profile type。")
		_assert_eq(_weapon_dice_signature(unarmed.weapon_one_handed_dice), [1, 4, 0], "空手玩家单位应投影 1D4 伤害骰。")
		_assert_eq(String(unarmed.weapon_physical_damage_tag), "physical_blunt", "空手玩家单位应投影钝击 tag。")
		_assert_eq(unarmed.weapon_attack_range, 1, "空手玩家单位应投影 1 格攻击范围。")
		_assert_true(not unarmed.weapon_uses_two_hands, "空手玩家单位不应标记双手握法。")

	member_state.equipment_state = EQUIPMENT_STATE_SCRIPT.new()
	member_state.equipment_state.set_equipped_entry(
		&"main_hand",
		&"bronze_sword",
		_slot_ids([&"main_hand"]),
		_make_equipment_instance(&"bronze_sword", &"weapon_projection_bronze")
	)
	var one_handed = _build_single_ally_unit(factory, party_state, "单手武器")
	if one_handed != null:
		_assert_eq(String(one_handed.weapon_profile_kind), "equipped", "单手武器玩家单位应投影装备武器 kind。")
		_assert_eq(String(one_handed.weapon_item_id), "bronze_sword", "单手武器玩家单位应投影当前主手 item id。")
		_assert_eq(String(one_handed.weapon_profile_type_id), "shortsword", "单手武器玩家单位应投影 shortsword profile。")
		_assert_eq(_weapon_dice_signature(one_handed.weapon_one_handed_dice), [1, 6, 0], "单手武器玩家单位应投影 1D6 一手骰。")
		_assert_true(one_handed.weapon_two_handed_dice.is_empty(), "单手武器玩家单位不应投影双手骰。")
		_assert_eq(String(one_handed.weapon_physical_damage_tag), "physical_pierce", "单手武器玩家单位应投影穿刺 tag。")
		_assert_eq(one_handed.weapon_attack_range, 1, "单手武器玩家单位应投影 profile range。")
		_assert_true(not one_handed.weapon_uses_two_hands, "单手武器玩家单位不应标记双手握法。")

	member_state.equipment_state = EQUIPMENT_STATE_SCRIPT.new()
	member_state.equipment_state.set_equipped_entry(
		&"main_hand",
		&"iron_greatsword",
		_slot_ids([&"main_hand", &"off_hand"]),
		_make_equipment_instance(&"iron_greatsword", &"weapon_projection_greatsword")
	)
	var two_handed = _build_single_ally_unit(factory, party_state, "双手武器")
	if two_handed != null:
		_assert_eq(String(two_handed.weapon_profile_type_id), "greatsword", "双手武器玩家单位应投影 greatsword profile。")
		_assert_true(two_handed.weapon_one_handed_dice.is_empty(), "双手武器玩家单位不应投影一手骰。")
		_assert_eq(_weapon_dice_signature(two_handed.weapon_two_handed_dice), [2, 6, 0], "双手武器玩家单位应投影 2D6 双手骰。")
		_assert_eq(String(two_handed.weapon_physical_damage_tag), "physical_slash", "双手武器玩家单位应投影挥砍 tag。")
		_assert_true(two_handed.weapon_uses_two_hands, "双手武器玩家单位应标记双手握法。")
		_assert_eq(String(two_handed.weapon_current_grip), "two_handed", "双手武器玩家单位当前握法应为 two_handed。")

	member_state.equipment_state = EQUIPMENT_STATE_SCRIPT.new()
	member_state.equipment_state.set_equipped_entry(
		&"main_hand",
		&"training_longsword",
		_slot_ids([&"main_hand"]),
		_make_equipment_instance(&"training_longsword", &"weapon_projection_longsword")
	)
	var versatile = _build_single_ally_unit(factory, party_state, "两用武器空副手")
	if versatile != null:
		_assert_true(versatile.weapon_is_versatile, "两用武器玩家单位应保留 versatile 标记。")
		_assert_eq(_weapon_dice_signature(versatile.weapon_one_handed_dice), [1, 8, 0], "两用武器玩家单位应保留一手骰。")
		_assert_eq(_weapon_dice_signature(versatile.weapon_two_handed_dice), [1, 10, 0], "两用武器玩家单位应保留双手骰。")
		_assert_true(versatile.weapon_uses_two_hands, "两用武器空副手时应动态使用双手握法。")
		_assert_eq(String(versatile.weapon_current_grip), "two_handed", "两用武器空副手时当前握法应为 two_handed。")

		versatile.get_equipment_view().set_equipped_entry(
			&"off_hand",
			&"training_shield",
			_slot_ids([&"off_hand"]),
			_make_equipment_instance(&"training_shield", &"weapon_projection_shield")
		)
		factory.refresh_weapon_projection(versatile)
		_assert_true(not versatile.weapon_uses_two_hands, "两用武器副手被占用后重新投影应改为单手握法。")
		_assert_eq(String(versatile.weapon_current_grip), "one_handed", "两用武器副手被占用后当前握法应为 one_handed。")
		_assert_eq(_weapon_dice_signature(versatile.weapon_two_handed_dice), [1, 10, 0], "两用武器重新投影仍应保留双手骰供后续切换。")


func _test_battle_unit_factory_uses_battle_local_equipment_view_for_refresh() -> void:
	var progression_registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var item_defs := ITEM_CONTENT_REGISTRY_SCRIPT.new().get_item_defs().duplicate()
	var party_state := _make_party_state([&"hero"])
	var member_state = party_state.get_member_state(&"hero")
	member_state.equipment_state = EQUIPMENT_STATE_SCRIPT.new()
	member_state.equipment_state.set_equipped_entry(
		&"main_hand",
		&"bronze_sword",
		_slot_ids([&"main_hand"]),
		_make_equipment_instance(&"bronze_sword", &"battle_start_sword")
	)

	var character_gateway := CHARACTER_MANAGEMENT_MODULE_SCRIPT.new()
	character_gateway.setup(
		party_state,
		progression_registry.get_skill_defs(),
		progression_registry.get_profession_defs(),
		{},
		item_defs
	)
	var runtime := FakeRuntime.new()
	runtime._character_gateway = character_gateway
	runtime._skill_defs = progression_registry.get_skill_defs()
	var factory := BattleUnitFactory.new()
	factory.setup(runtime)

	var unit = _build_single_ally_unit(factory, party_state, "battle-local 装备 view")
	if unit == null:
		return
	_assert_true(unit.get_equipment_view() != member_state.equipment_state, "构建战斗单位时应复制装备 view，而不是引用 PartyMemberState。")
	_assert_eq(String(unit.get_equipment_view().get_equipped_instance_id(&"main_hand")), "battle_start_sword", "battle-local 装备 view 应保留开战装备实例 ID。")
	_assert_eq(String(unit.weapon_item_id), "bronze_sword", "初始武器投影应来自 battle-local 装备 view。")

	var armor_ac_before := int(unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_AC_BONUS))
	unit.get_equipment_view().set_equipped_entry(
		&"head",
		&"leather_cap",
		_slot_ids([&"head"]),
		_make_equipment_instance(&"leather_cap", &"battle_cap")
	)
	factory.refresh_battle_unit(unit)
	_assert_eq(
		int(unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_AC_BONUS)) - armor_ac_before,
		1,
		"刷新战斗单位属性快照应读取 battle-local 装备 view。"
	)
	_assert_eq(String(member_state.equipment_state.get_equipped_item_id(&"head")), "", "battle-local 装备 view 改头部装备不应写回 PartyMemberState。")

	var payload: Dictionary = unit.to_dict()
	var payload_equipment: Dictionary = payload.get("equipment_view", {})
	var payload_slots: Dictionary = payload_equipment.get("equipped_slots", {})
	var payload_main_hand: Dictionary = payload_slots.get("main_hand", {})
	var payload_main_instance: Dictionary = payload_main_hand.get("equipment_instance", {})
	var restored = BATTLE_UNIT_STATE_SCRIPT.from_dict(payload) as BattleUnitState
	_assert_eq(String(payload_main_instance.get("instance_id", "")), "battle_start_sword", "BattleUnitState payload 应通过 equipment_instance 保留装备实例 ID。")
	_assert_eq(
		String(restored.get_equipment_view().get_equipped_instance_id(&"main_hand")) if restored != null else "",
		"battle_start_sword",
		"BattleUnitState round-trip 应恢复 battle-local 装备实例 ID。"
	)

	member_state.equipment_state = EQUIPMENT_STATE_SCRIPT.new()
	member_state.equipment_state.set_equipped_entry(
		&"main_hand",
		&"iron_greatsword",
		_slot_ids([&"main_hand", &"off_hand"]),
		_make_equipment_instance(&"iron_greatsword", &"party_late_greatsword")
	)
	factory.refresh_battle_unit(unit)
	_assert_eq(String(unit.get_equipment_view().get_equipped_instance_id(&"main_hand")), "battle_start_sword", "刷新战斗单位不应从 PartyMemberState 重灌装备 view。")
	_assert_eq(String(unit.weapon_item_id), "bronze_sword", "刷新武器投影应继续读取 battle-local 装备 view。")

	unit.get_equipment_view().set_equipped_entry(
		&"main_hand",
		&"iron_greatsword",
		_slot_ids([&"main_hand", &"off_hand"]),
		_make_equipment_instance(&"iron_greatsword", &"battle_swap_greatsword")
	)
	factory.refresh_battle_unit(unit)
	_assert_eq(String(unit.weapon_item_id), "iron_greatsword", "修改 battle-local 装备 view 后应能重新投影当前武器。")
	_assert_eq(String(unit.get_equipment_view().get_equipped_instance_id(&"main_hand")), "battle_swap_greatsword", "battle-local 换装后的实例 ID 应留在单位 view 内。")
	_assert_eq(
		String(member_state.equipment_state.get_equipped_instance_id(&"main_hand")),
		"party_late_greatsword",
		"修改 battle-local 装备 view 不应直改 PartyMemberState.equipment_state。"
	)


func _test_battle_unit_factory_fallback_enemy_seeds_six_base_attributes() -> void:
	var factory := BattleUnitFactory.new()
	var encounter_anchor := ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = &"factory_enemy_defaults"
	encounter_anchor.display_name = "默认敌人"
	encounter_anchor.faction_id = &"hostile"

	var units: Array = factory.build_enemy_units(encounter_anchor, {})
	_assert_eq(units.size(), 1, "fallback enemy builder 应产出 1 个默认敌人。")
	if units.is_empty():
		return
	var unit = units[0]
	for attribute_id in UNIT_BASE_ATTRIBUTES_SCRIPT.BASE_ATTRIBUTE_IDS:
		_assert_eq(
			int(unit.attribute_snapshot.get_value(attribute_id)),
			4,
			"fallback enemy 应补齐基础六维 %s。" % String(attribute_id)
		)
	_assert_true(
		not unit.attribute_snapshot.has_value(ATTRIBUTE_SERVICE_SCRIPT.WEAPON_ATTACK_RANGE),
		"fallback enemy 不应再把武器攻击范围写入 attribute_snapshot。"
	)
	_assert_eq(String(unit.weapon_profile_kind), "unarmed", "fallback enemy 无模板攻击装备时应降级为空手投影。")
	_assert_eq(String(unit.weapon_profile_type_id), "unarmed", "fallback enemy 空手投影应保留 unarmed profile。")
	_assert_eq(_weapon_dice_signature(unit.weapon_one_handed_dice), [1, 4, 0], "fallback enemy 空手投影应使用 1D4。")
	_assert_eq(String(unit.weapon_physical_damage_tag), "physical_blunt", "fallback enemy 空手投影应使用钝击。")
	_assert_eq(unit.weapon_attack_range, 1, "fallback enemy 应把默认攻击范围投影到 BattleUnitState.weapon_attack_range。")


func _test_enemy_resource_sync_handles_missing_attribute_snapshot() -> void:
	var factory := BattleUnitFactory.new()
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.attribute_snapshot = null
	unit.current_mp = 3
	unit.current_aura = 2

	factory._sync_enemy_unlocked_resources(unit)
	_assert_true(unit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_HP), "缺属性快照时仍应保留默认 HP 资源。")
	_assert_true(unit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_STAMINA), "缺属性快照时仍应保留默认 stamina 资源。")
	_assert_true(unit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_MP), "缺属性快照但 current_mp 大于 0 时应解锁 MP。")
	_assert_true(unit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_AURA), "缺属性快照但 current_aura 大于 0 时应解锁 aura。")

	var empty_unit = BATTLE_UNIT_STATE_SCRIPT.new()
	empty_unit.attribute_snapshot = null
	factory._sync_enemy_unlocked_resources(empty_unit)
	_assert_true(not empty_unit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_MP), "缺属性快照且 current_mp 为 0 时不应解锁 MP。")
	_assert_true(not empty_unit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_AURA), "缺属性快照且 current_aura 为 0 时不应解锁 aura。")


func _test_battle_unit_factory_no_longer_builds_manual_fallback_terrain() -> void:
	var runtime := FakeRuntime.new()
	var terrain_generator := FakeTerrainGenerator.new()
	runtime._terrain_generator = terrain_generator
	var factory := BattleUnitFactory.new()
	factory.setup(runtime)

	var terrain_data := factory.build_terrain_data(null, 7, {
		"map_size": Vector2i(1, 1),
	})
	_assert_true(terrain_data.is_empty(), "BattleUnitFactory 不应再为 map_size 手工拼 fallback terrain。")
	_assert_true(
		not terrain_generator.last_context.has("map_size"),
		"BattleUnitFactory 不应再把 legacy map_size 继续透传给正式 terrain generator。"
	)
	_assert_true(
		not terrain_generator.last_context.has("battle_map_size"),
		"BattleUnitFactory 不应再把 legacy map_size 升级成 battle_map_size。"
	)


func _test_runtime_requires_formal_terrain_profile_id_from_generator() -> void:
	var missing_profile_state := _start_battle_with_stubbed_terrain({
		"map_size": Vector2i(3, 3),
		"cells": _build_flat_cells(Vector2i(3, 3)),
		"ally_spawns": [Vector2i(0, 1)],
		"enemy_spawns": [Vector2i(2, 1)],
	})
	_assert_true(
		missing_profile_state != null and missing_profile_state.is_empty(),
		"BattleRuntimeModule 不应再从 context.battle_terrain_profile 回填缺失的 terrain_data.terrain_profile_id。"
	)

	var formal_profile_state := _start_battle_with_stubbed_terrain({
		"map_size": Vector2i(3, 3),
		"terrain_profile_id": "formal_test_profile",
		"cells": _build_flat_cells(Vector2i(3, 3)),
		"ally_spawns": [Vector2i(0, 1)],
		"enemy_spawns": [Vector2i(2, 1)],
	})
	_assert_true(
		formal_profile_state != null and not formal_profile_state.is_empty(),
		"terrain_data 显式提供正式 terrain_profile_id 时应继续启动战斗。"
	)
	if formal_profile_state != null and not formal_profile_state.is_empty():
		_assert_eq(
			String(formal_profile_state.terrain_profile_id),
			"formal_test_profile",
			"BattleState.terrain_profile_id 应来自 terrain generator 输出，而不是 context fallback。"
		)


func _start_battle_with_stubbed_terrain(terrain_data: Dictionary) -> BattleState:
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, {}, {}, {})
	var unit_factory := RuntimeUnitFactoryStub.new()
	unit_factory.ally_units = [_make_runtime_schema_unit(&"terrain_schema_ally", &"player")]
	unit_factory.enemy_units = [_make_runtime_schema_unit(&"terrain_schema_enemy", &"hostile")]
	unit_factory.terrain_data = terrain_data
	runtime._unit_factory = unit_factory

	var encounter_anchor := ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = &"terrain_profile_schema"
	encounter_anchor.display_name = "地形 profile schema"
	encounter_anchor.world_coord = Vector2i.ZERO
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.region_tag = &"default"

	return runtime.start_battle(
		encounter_anchor,
		20260502,
		{
			"battle_terrain_profile": "legacy_context_profile",
			"enemy_units": [unit_factory.enemy_units[0].to_dict()],
			"validate_spawn_reachability": false,
		}
	)


func _make_runtime_schema_unit(unit_id: StringName, faction_id: StringName) -> BattleUnitState:
	var unit_state := BATTLE_UNIT_STATE_SCRIPT.new()
	unit_state.unit_id = unit_id
	unit_state.source_member_id = unit_id
	unit_state.display_name = String(unit_id)
	unit_state.faction_id = faction_id
	unit_state.control_mode = &"manual" if faction_id == &"player" else &"ai"
	unit_state.current_hp = 10
	unit_state.current_ap = 2
	unit_state.current_move_points = BATTLE_UNIT_STATE_SCRIPT.DEFAULT_MOVE_POINTS_PER_TURN
	unit_state.is_alive = true
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 10)
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD, ATTRIBUTE_SERVICE_SCRIPT.DEFAULT_CHARACTER_ACTION_THRESHOLD)
	return unit_state


func _build_flat_cells(map_size: Vector2i) -> Dictionary:
	var cells: Dictionary = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			var coord := Vector2i(x, y)
			var cell = BATTLE_CELL_STATE_SCRIPT.new()
			cell.coord = coord
			cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_LAND
			cell.recalculate_runtime_values()
			cells[coord] = cell
	return cells


func _build_single_ally_unit(factory: BattleUnitFactory, party_state: PartyState, label: String):
	var units: Array = factory.build_ally_units(party_state, {})
	_assert_eq(units.size(), 1, "%s 场景应构建 1 个友方单位。" % label)
	if units.is_empty():
		return null
	return units[0]


func _make_weapon_item_def(
	item_id: StringName,
	weapon_type_id: StringName,
	damage_tag: StringName,
	attack_range: int,
	one_handed_dice,
	two_handed_dice,
	properties: Array[StringName]
):
	var item_def: ItemDef = ITEM_DEF_SCRIPT.new()
	item_def.item_id = item_id
	item_def.item_category = ITEM_DEF_SCRIPT.ITEM_CATEGORY_EQUIPMENT
	item_def.equipment_type_id = ITEM_DEF_SCRIPT.EQUIPMENT_TYPE_WEAPON
	item_def.equipment_slot_ids = ["main_hand"]
	item_def.is_stackable = false
	item_def.max_stack = 1
	item_def.tags = [&"melee"]
	var profile = WEAPON_PROFILE_DEF_SCRIPT.new()
	profile.weapon_type_id = weapon_type_id
	profile.training_group = &"martial"
	profile.range_type = &"melee"
	profile.family = &"sword"
	profile.damage_tag = damage_tag
	profile.attack_range = attack_range
	profile.one_handed_dice = one_handed_dice
	profile.two_handed_dice = two_handed_dice
	profile.properties_mode = WEAPON_PROFILE_DEF_SCRIPT.PropertyMergeMode.REPLACE
	profile.properties = properties
	item_def.weapon_profile = profile
	return item_def


func _make_weapon_dice(dice_count: int, dice_sides: int, flat_bonus: int):
	var dice = WEAPON_DAMAGE_DICE_DEF_SCRIPT.new()
	dice.dice_count = dice_count
	dice.dice_sides = dice_sides
	dice.flat_bonus = flat_bonus
	return dice


func _weapon_dice_signature(dice: Dictionary) -> Array:
	if dice.is_empty():
		return []
	return [
		int(dice.get("dice_count", 0)),
		int(dice.get("dice_sides", 0)),
		int(dice.get("flat_bonus", 0)),
	]


func _slot_ids(values: Array) -> Array[StringName]:
	return ProgressionDataUtils.to_string_name_array(values)


func _make_stack(item_id: StringName, quantity: int):
	var stack = WAREHOUSE_STACK_STATE_SCRIPT.new()
	stack.item_id = item_id
	stack.quantity = quantity
	return stack


func _make_equipment_instance(item_id: StringName, instance_id: StringName):
	return EQUIPMENT_INSTANCE_STATE_SCRIPT.create(item_id, instance_id)


func _backpack_stack_signature(backpack_state) -> Array[String]:
	var result: Array[String] = []
	if backpack_state == null:
		return result
	for stack in backpack_state.get_non_empty_stacks():
		result.append("%s:%d" % [String(stack.item_id), int(stack.quantity)])
	return result


func _backpack_instance_signature(backpack_state) -> Array[String]:
	var result: Array[String] = []
	if backpack_state == null:
		return result
	for instance in backpack_state.get_non_empty_instances():
		result.append(String(instance.item_id))
	result.sort()
	return result


func _backpack_instance_id_signature(backpack_state) -> Array[String]:
	var result: Array[String] = []
	if backpack_state == null:
		return result
	for instance in backpack_state.get_non_empty_instances():
		result.append(String(instance.instance_id))
	result.sort()
	return result


func _make_party_state(member_ids: Array[StringName]) -> PartyState:
	var party_state := PARTY_STATE_SCRIPT.new()
	for member_id in member_ids:
		var member_state := _make_member_state(member_id)
		party_state.set_member_state(member_state)
		party_state.active_member_ids.append(member_id)
		if party_state.leader_member_id == &"":
			party_state.leader_member_id = member_id
		if party_state.main_character_member_id == &"":
			party_state.main_character_member_id = member_id
	return party_state


func _make_member_state(member_id: StringName) -> PartyMemberState:
	var member_state := PARTY_MEMBER_STATE_SCRIPT.new()
	member_state.member_id = member_id
	member_state.display_name = String(member_id).capitalize()
	member_state.body_size = 3
	member_state.body_size_category = &"large"
	member_state.current_hp = 18
	member_state.current_mp = 6
	member_state.control_mode = &"manual"
	member_state.progression.unit_id = member_id
	member_state.progression.display_name = member_state.display_name
	member_state.progression.character_level = 1
	member_state.progression.unit_base_attributes.set_attribute_value(&"storage_space", 8)
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
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD, 30)
	return snapshot


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
