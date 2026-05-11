extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleLootConstants = preload("res://scripts/systems/battle/core/battle_loot_constants.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const ENEMY_TEMPLATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_template_def.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const WAREHOUSE_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_state.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


class _SpyEquipmentDropService:
	extends RefCounted

	const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")

	var calls: Array[Dictionary] = []


	func roll_item_instances(item_id: StringName, quantity: int, drop_luck: int) -> Array:
		calls.append({
			"item_id": String(item_id),
			"quantity": int(quantity),
			"drop_luck": int(drop_luck),
		})
		var instances: Array = []
		for _index in range(maxi(int(quantity), 0)):
			var instance = EquipmentInstanceState.create(item_id)
			instance.rarity = EquipmentInstanceState.RarityTier.EPIC if drop_luck >= 5 else EquipmentInstanceState.RarityTier.COMMON
			instances.append(instance)
		return instances


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_per_kill_loot_uses_killer_luck_and_commits_fixed_item()
	_test_per_kill_random_equipment_without_player_killer_uses_neutral_luck()
	_test_per_kill_random_equipment_overflow_is_lost_in_settlement_commit()
	_test_per_kill_attack_equipment_is_not_implicit_loot()
	if _failures.is_empty():
		print("Battle loot drop luck regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle loot drop luck regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_per_kill_loot_uses_killer_luck_and_commits_fixed_item() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var party_state = facade.get_party_state()
	_reset_party_warehouse(party_state)
	_ensure_capacity(party_state, 10)
	var main_member_id: StringName = party_state.get_resolved_main_character_member_id()
	var main_member = party_state.get_member_state(main_member_id)
	_assert_true(main_member != null, "测试前置：应能读取主角成员。")
	if main_member == null:
		_cleanup_test_session(game_session)
		return
	_set_member_luck(main_member, 2, 5)
	var killer_member: PartyMemberState = _add_party_member(party_state, &"low_luck_killer", "Low Luck Killer")
	_set_member_luck(killer_member, -6, 0)
	facade._character_management.set_party_state(party_state)

	var drop_service := _SpyEquipmentDropService.new()
	_inject_drop_services(facade, drop_service)
	facade._battle_runtime._enemy_templates[&"per_kill_loot_wolf"] = _build_enemy_template_with_mixed_loot(&"per_kill_loot_wolf")

	var defeated_enemy = _build_defeated_enemy_unit(&"per_kill_enemy", &"per_kill_loot_wolf", "战利品荒狼")
	var killer_unit = _build_killer_unit(killer_member.member_id, "Low Luck Killer")
	facade._battle_runtime._collect_defeated_unit_loot(defeated_enemy, killer_unit)
	facade._battle_runtime._collect_defeated_unit_loot(defeated_enemy, killer_unit)

	var resolution_result = BATTLE_RESOLUTION_RESULT_SCRIPT.new()
	resolution_result.winner_faction_id = &"player"
	resolution_result.set_loot_entries(facade._battle_runtime._active_loot_entries)
	var equipment_entries := _count_drop_type(resolution_result.loot_entries, BattleLootConstants.DROP_TYPE_EQUIPMENT_INSTANCE)
	var commit_result: Dictionary = facade._commit_battle_loot_to_shared_warehouse(resolution_result)

	_assert_eq(drop_service.calls.size(), 1, "fixed item 掉落不应重复调用 equipment_drop_service。")
	if drop_service.calls.size() > 0:
		var call: Dictionary = drop_service.calls[0]
		_assert_eq(String(call.get("item_id", "")), "bronze_sword", "随机装备掉落应保留稳定装备 item_id。")
		_assert_eq(int(call.get("quantity", 0)), 1, "随机装备掉落应把稳定数量传给 equipment_drop_service。")
		_assert_eq(int(call.get("drop_luck", 0)), -6, "per-kill 方案应读取击杀者的有效幸运值。")
	_assert_eq(equipment_entries, 1, "BattleResolutionResult 应保存击杀时已解析完成的 equipment_instance 条目。")
	_assert_true(bool(commit_result.get("ok", false)), "per-kill 掉落应能成功提交到共享仓库。")
	_assert_eq(int(commit_result.get("overflow_entry_count", -1)), 0, "容量充足时不应产出 overflow entry。")
	_assert_eq(int(commit_result.get("committed_item_count", -1)), 3, "1 件装备实例 + 2 个固定材料应共同计入 committed_item_count。")
	_assert_eq(party_state.warehouse_state.equipment_instances.size(), 1, "equipment_instance 条目应向共享仓库写入 1 个装备实例。")
	if party_state.warehouse_state.equipment_instances.size() > 0:
		var equipment_instance = party_state.warehouse_state.equipment_instances[0]
		_assert_eq(String(equipment_instance.item_id), "bronze_sword", "equipment_instance 提交后应保留稳定 item_id。")
		_assert_eq(int(equipment_instance.rarity), int(EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.COMMON), "低 luck 击杀者应保留击杀时 roll 出的低稀有度。")
	_assert_eq(_count_stack_quantity(party_state, &"beast_hide"), 2, "固定材料掉落应继续按堆叠物品入仓。")
	_cleanup_test_session(game_session)


func _test_per_kill_random_equipment_without_player_killer_uses_neutral_luck() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var party_state = facade.get_party_state()
	_reset_party_warehouse(party_state)
	_ensure_capacity(party_state, 10)
	var main_member_id: StringName = party_state.get_resolved_main_character_member_id()
	var main_member = party_state.get_member_state(main_member_id)
	_assert_true(main_member != null, "测试前置：应能读取主角成员。")
	if main_member == null:
		_cleanup_test_session(game_session)
		return
	_set_member_luck(main_member, 2, 5)
	facade._character_management.set_party_state(party_state)

	var drop_service := _SpyEquipmentDropService.new()
	_inject_drop_services(facade, drop_service)
	facade._battle_runtime._enemy_templates[&"neutral_loot_wolf"] = _build_enemy_template_with_random_equipment_only(&"neutral_loot_wolf")

	var defeated_enemy = _build_defeated_enemy_unit(&"neutral_enemy", &"neutral_loot_wolf", "中立掉落荒狼")
	facade._battle_runtime._collect_defeated_unit_loot(defeated_enemy, null)

	var resolution_result = BATTLE_RESOLUTION_RESULT_SCRIPT.new()
	resolution_result.winner_faction_id = &"player"
	resolution_result.set_loot_entries(facade._battle_runtime._active_loot_entries)
	var commit_result: Dictionary = facade._commit_battle_loot_to_shared_warehouse(resolution_result)

	_assert_true(bool(commit_result.get("ok", false)), "没有玩家击杀归属时，战利品仍应能成功提交。")
	_assert_eq(drop_service.calls.size(), 1, "中立掉落路径应调用一次 equipment_drop_service。")
	if drop_service.calls.size() > 0:
		var call: Dictionary = drop_service.calls[0]
		_assert_eq(int(call.get("drop_luck", 0)), 0, "缺少玩家击杀者时，应按中性 luck=0 结算随机装备。")
	_assert_eq(party_state.warehouse_state.equipment_instances.size(), 1, "中立击杀路径应继续产出 1 个装备实例。")
	if party_state.warehouse_state.equipment_instances.size() > 0:
		var equipment_instance = party_state.warehouse_state.equipment_instances[0]
		_assert_eq(int(equipment_instance.rarity), int(EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.COMMON), "neutral luck=0 时应保留 spy service 返回的默认稀有度。")
	_cleanup_test_session(game_session)


func _test_per_kill_random_equipment_overflow_is_lost_in_settlement_commit() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var party_state = facade.get_party_state()
	_reset_party_warehouse(party_state)
	_ensure_capacity(party_state, 1)
	facade._party_warehouse_service.setup(party_state, game_session.get_item_defs())
	facade._party_warehouse_service.add_item(&"bronze_sword", 1)
	facade._character_management.set_party_state(party_state)

	var drop_service := _SpyEquipmentDropService.new()
	_inject_drop_services(facade, drop_service)
	facade._battle_runtime._enemy_templates[&"overflow_loot_wolf"] = _build_enemy_template_with_random_equipment_only(&"overflow_loot_wolf")

	var defeated_enemy = _build_defeated_enemy_unit(&"overflow_enemy", &"overflow_loot_wolf", "满包掉落荒狼")
	facade._battle_runtime._collect_defeated_unit_loot(defeated_enemy, null)

	var resolution_result = BATTLE_RESOLUTION_RESULT_SCRIPT.new()
	resolution_result.winner_faction_id = &"player"
	resolution_result.set_loot_entries(facade._battle_runtime._active_loot_entries)
	var commit_result: Dictionary = facade._commit_battle_loot_to_shared_warehouse(resolution_result)

	_assert_true(bool(commit_result.get("ok", false)), "随机装备掉落遇到满包时仍应以丢失方式完成结算。")
	_assert_eq(int(commit_result.get("committed_item_count", -1)), 0, "随机装备掉落满包时不应写入新装备实例。")
	_assert_eq(int(commit_result.get("overflow_entry_count", 0)), 1, "随机装备掉落满包时应记录 overflow entry。")
	_assert_eq(resolution_result.overflow_entries.size(), 1, "随机装备掉落满包时应写入 BattleResolutionResult overflow_entries。")
	if resolution_result.overflow_entries.size() > 0 and resolution_result.overflow_entries[0] is Dictionary:
		var overflow_entry: Dictionary = resolution_result.overflow_entries[0]
		_assert_eq(String(overflow_entry.get("item_id", "")), "bronze_sword", "随机装备 overflow entry 应保留掉落装备 item_id。")
		_assert_eq(int(overflow_entry.get("quantity", 0)), 1, "随机装备 overflow entry 应记录丢失件数。")
	_assert_eq(party_state.warehouse_state.equipment_instances.size(), 1, "随机装备满包丢失时只应保留原有占位装备。")
	_cleanup_test_session(game_session)


func _test_per_kill_attack_equipment_is_not_implicit_loot() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	facade._battle_runtime._enemy_templates[&"attack_equipment_only_enemy"] = _build_enemy_template_with_attack_equipment_only(&"attack_equipment_only_enemy")

	var defeated_enemy = _build_defeated_enemy_unit(&"attack_equipment_enemy", &"attack_equipment_only_enemy", "持钉锤敌人")
	facade._battle_runtime._collect_defeated_unit_loot(defeated_enemy, null)

	_assert_true(
		facade._battle_runtime._active_loot_entries.is_empty(),
		"敌人死亡不应因为 attack_equipment_item_id 自动掉落攻击装备；per-kill 掉落只读取 drop_entries。"
	)
	_cleanup_test_session(game_session)


func _create_test_session():
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "GameSession 应能为 per-kill 掉落回归创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return null
	return game_session


func _cleanup_test_session(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _inject_drop_services(facade, drop_service) -> void:
	if facade == null:
		return
	facade._equipment_drop_service = drop_service
	if facade._battle_runtime != null:
		facade._battle_runtime._equipment_drop_service = drop_service


func _reset_party_warehouse(party_state) -> void:
	if party_state == null:
		return
	party_state.warehouse_state = WAREHOUSE_STATE_SCRIPT.new()


func _ensure_capacity(party_state, storage_space: int) -> void:
	if party_state == null:
		return
	var first_member_assigned := false
	for member_variant in party_state.member_states.values():
		var member_state = member_variant
		if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
			continue
		member_state.progression.unit_base_attributes.custom_stats[&"storage_space"] = maxi(storage_space, 0) if not first_member_assigned else 0
		first_member_assigned = true


func _set_member_luck(member_state, hidden_luck_at_birth: int, faith_luck_bonus: int) -> void:
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return
	member_state.progression.unit_base_attributes.custom_stats[UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH] = hidden_luck_at_birth
	member_state.progression.unit_base_attributes.custom_stats[UNIT_BASE_ATTRIBUTES_SCRIPT.FAITH_LUCK_BONUS] = faith_luck_bonus


func _add_party_member(party_state, member_id: StringName, display_name: String) -> PartyMemberState:
	var member_state: PartyMemberState = PARTY_MEMBER_STATE_SCRIPT.new()
	member_state.member_id = member_id
	member_state.display_name = display_name
	member_state.progression.unit_id = member_id
	member_state.progression.display_name = display_name
	party_state.set_member_state(member_state)
	return member_state


func _build_enemy_template_with_mixed_loot(template_id: StringName):
	var template = ENEMY_TEMPLATE_DEF_SCRIPT.new()
	template.template_id = template_id
	template.display_name = "战利品荒狼"
	var drop_entries: Array[Dictionary] = [
		{
			"drop_entry_id": "weapon_roll",
			"drop_type": "random_equipment",
			"item_id": "bronze_sword",
			"quantity": 1,
		},
		{
			"drop_entry_id": "hide_bundle",
			"drop_type": "item",
			"item_id": "beast_hide",
			"quantity": 2,
		},
	]
	template.drop_entries = drop_entries
	return template


func _build_enemy_template_with_random_equipment_only(template_id: StringName):
	var template = ENEMY_TEMPLATE_DEF_SCRIPT.new()
	template.template_id = template_id
	template.display_name = "中立掉落荒狼"
	var drop_entries: Array[Dictionary] = [
		{
			"drop_entry_id": "weapon_roll",
			"drop_type": "random_equipment",
			"item_id": "bronze_sword",
			"quantity": 1,
		},
	]
	template.drop_entries = drop_entries
	return template


func _build_enemy_template_with_attack_equipment_only(template_id: StringName):
	var template = ENEMY_TEMPLATE_DEF_SCRIPT.new()
	template.template_id = template_id
	template.display_name = "持钉锤敌人"
	template.attack_equipment_item_id = &"watchman_mace"
	var drop_entries: Array[Dictionary] = []
	template.drop_entries = drop_entries
	return template


func _build_defeated_enemy_unit(unit_id: StringName, template_id: StringName, display_name: String):
	var unit_state = BATTLE_UNIT_STATE_SCRIPT.new()
	unit_state.unit_id = unit_id
	unit_state.enemy_template_id = template_id
	unit_state.display_name = display_name
	unit_state.faction_id = &"hostile"
	unit_state.control_mode = &"ai"
	unit_state.is_alive = false
	return unit_state


func _build_killer_unit(member_id: StringName, display_name: String):
	var unit_state = BATTLE_UNIT_STATE_SCRIPT.new()
	unit_state.unit_id = StringName("%s_unit" % String(member_id))
	unit_state.source_member_id = member_id
	unit_state.display_name = display_name
	unit_state.faction_id = &"player"
	unit_state.control_mode = &"manual"
	unit_state.is_alive = true
	return unit_state


func _count_drop_type(loot_entries: Array, drop_type: StringName) -> int:
	var total := 0
	for loot_entry_variant in loot_entries:
		if loot_entry_variant is not Dictionary:
			continue
		var loot_entry := loot_entry_variant as Dictionary
		if ProgressionDataUtils.to_string_name(loot_entry.get("drop_type", "")) == drop_type:
			total += 1
	return total


func _count_stack_quantity(party_state, item_id: StringName) -> int:
	if party_state == null or party_state.warehouse_state == null:
		return 0
	var total_quantity := 0
	for stack in party_state.warehouse_state.stacks:
		if stack == null or stack.item_id != item_id:
			continue
		total_quantity += int(stack.quantity)
	return total_quantity


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
