extends SceneTree

const AttributeService = preload("res://scripts/systems/attribute_service.gd")
const CharacterManagementModule = preload("res://scripts/systems/character_management_module.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const PartyEquipmentService = preload("res://scripts/systems/party_equipment_service.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyWarehouseService = preload("res://scripts/systems/party_warehouse_service.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")
const UnitProgress = preload("res://scripts/player/progression/unit_progress.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_item_registry_accepts_equipment_seed_data()
	_test_equipment_service_moves_items_between_warehouse_and_slots()
	_test_equipment_modifiers_change_attribute_snapshot_and_round_trip()
	_test_legacy_equipment_state_dict_is_still_supported()
	_finish()


func _test_item_registry_accepts_equipment_seed_data() -> void:
	var registry := ItemContentRegistry.new()
	_assert_true(registry.validate().is_empty(), "装备种子物品定义应通过 ItemContentRegistry 校验。")

	var item_defs := registry.get_item_defs()
	var bronze_sword = item_defs.get(&"bronze_sword")
	var leather_jerkin = item_defs.get(&"leather_jerkin")
	var scout_charm = item_defs.get(&"scout_charm")

	_assert_true(bronze_sword != null and bronze_sword.is_equipment(), "青铜短剑应注册为可装备物品。")
	_assert_true(leather_jerkin != null and leather_jerkin.is_equipment(), "皮革短甲应注册为可装备物品。")
	_assert_true(scout_charm != null and scout_charm.is_equipment(), "斥候护符应注册为可装备物品。")


func _test_equipment_service_moves_items_between_warehouse_and_slots() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)

	warehouse_service.add_item(&"bronze_sword", 1)
	warehouse_service.add_item(&"scout_charm", 2)

	var sword_result := equipment_service.equip_item(&"hero", &"bronze_sword")
	_assert_true(bool(sword_result.get("success", false)), "共享仓库中的武器应能装备到角色主手。")
	_assert_eq(String(sword_result.get("slot_id", "")), "main_hand", "武器应进入主手槽。")
	_assert_eq(warehouse_service.count_item(&"bronze_sword"), 0, "装备武器后，共享仓库中的对应库存应扣减。")

	var first_charm_result := equipment_service.equip_item(&"hero", &"scout_charm")
	_assert_true(bool(first_charm_result.get("success", false)), "第一枚饰品应能自动装备。")
	_assert_eq(String(first_charm_result.get("slot_id", "")), "accessory_1", "第一枚饰品应优先进入饰品一槽。")

	var second_charm_result := equipment_service.equip_item(&"hero", &"scout_charm")
	_assert_true(bool(second_charm_result.get("success", false)), "第二枚饰品应能继续自动装备。")
	_assert_eq(String(second_charm_result.get("slot_id", "")), "accessory_2", "第二枚饰品应自动进入空闲饰品槽。")
	_assert_eq(warehouse_service.count_item(&"scout_charm"), 0, "两枚饰品都装备后，仓库中不应残留对应库存。")

	var equipment_state = party_state.get_member_state(&"hero").equipment_state
	_assert_eq(String(equipment_state.get_equipped_item_id(&"main_hand")), "bronze_sword", "角色主手状态应记录已装备武器。")
	_assert_eq(String(equipment_state.get_equipped_item_id(&"accessory_1")), "scout_charm", "饰品一槽应记录首个饰品。")
	_assert_eq(String(equipment_state.get_equipped_item_id(&"accessory_2")), "scout_charm", "饰品二槽应记录第二个饰品。")

	var unequip_result := equipment_service.unequip_item(&"hero", &"accessory_1")
	_assert_true(bool(unequip_result.get("success", false)), "已装备饰品应能卸回共享仓库。")
	_assert_eq(warehouse_service.count_item(&"scout_charm"), 1, "卸装后物品应回到共享仓库。")
	_assert_eq(String(equipment_state.get_equipped_item_id(&"accessory_1")), "", "卸装后的槽位应清空。")


func _test_equipment_modifiers_change_attribute_snapshot_and_round_trip() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var progression_registry := ProgressionContentRegistry.new()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)

	var baseline_manager := CharacterManagementModule.new()
	baseline_manager.setup(
		party_state,
		progression_registry.get_skill_defs(),
		progression_registry.get_profession_defs(),
		progression_registry.get_achievement_defs(),
		item_defs
	)
	var before_snapshot = baseline_manager.get_member_attribute_snapshot(&"hero")

	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	warehouse_service.add_item(&"bronze_sword", 1)
	warehouse_service.add_item(&"leather_jerkin", 1)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)
	equipment_service.equip_item(&"hero", &"bronze_sword")
	equipment_service.equip_item(&"hero", &"leather_jerkin")

	var manager := CharacterManagementModule.new()
	manager.setup(
		party_state,
		progression_registry.get_skill_defs(),
		progression_registry.get_profession_defs(),
		progression_registry.get_achievement_defs(),
		item_defs
	)
	var after_snapshot = manager.get_member_attribute_snapshot(&"hero")

	_assert_eq(
		after_snapshot.get_value(AttributeService.PHYSICAL_ATTACK) - before_snapshot.get_value(AttributeService.PHYSICAL_ATTACK),
		4,
		"青铜短剑应为物攻提供固定加值。"
	)
	_assert_eq(
		after_snapshot.get_value(AttributeService.HIT_RATE) - before_snapshot.get_value(AttributeService.HIT_RATE),
		5,
		"青铜短剑应为命中提供固定加值。"
	)
	_assert_eq(
		after_snapshot.get_value(AttributeService.PHYSICAL_DEFENSE) - before_snapshot.get_value(AttributeService.PHYSICAL_DEFENSE),
		3,
		"皮革短甲应为物防提供固定加值。"
	)
	_assert_eq(
		after_snapshot.get_value(AttributeService.HP_MAX) - before_snapshot.get_value(AttributeService.HP_MAX),
		6,
		"皮革短甲应为生命上限提供固定加值。"
	)

	var restored_party_state = PartyState.from_dict(party_state.to_dict())
	var restored_equipment_state = restored_party_state.get_member_state(&"hero").equipment_state
	_assert_eq(String(restored_equipment_state.get_equipped_item_id(&"main_hand")), "bronze_sword", "序列化往返后应保留主手装备。")
	_assert_eq(String(restored_equipment_state.get_equipped_item_id(&"body")), "leather_jerkin", "序列化往返后应保留身躯装备。")


func _test_legacy_equipment_state_dict_is_still_supported() -> void:
	var member_state = PartyMemberState.from_dict({
		"member_id": "legacy_hero",
		"display_name": "Legacy Hero",
		"equipment_state": {
			"main_hand": "bronze_sword",
			"body": {
				"item_id": "leather_jerkin",
			},
		},
	})
	_assert_eq(String(member_state.equipment_state.get_equipped_item_id(&"main_hand")), "bronze_sword", "旧版裸字典结构应恢复主手装备。")
	_assert_eq(String(member_state.equipment_state.get_equipped_item_id(&"body")), "leather_jerkin", "旧版嵌套字典结构应恢复身躯装备。")


func _build_party_with_member(member_id: StringName, display_name: String, storage_space: int) -> PartyState:
	var party_state := PartyState.new()
	var member_state := PartyMemberState.new()
	member_state.member_id = member_id
	member_state.display_name = display_name
	member_state.progression = UnitProgress.new()
	member_state.progression.unit_id = member_id
	member_state.progression.display_name = display_name

	var unit_base_attributes := UnitBaseAttributes.new()
	unit_base_attributes.set_attribute_value(UnitBaseAttributes.STRENGTH, 4)
	unit_base_attributes.set_attribute_value(UnitBaseAttributes.AGILITY, 3)
	unit_base_attributes.set_attribute_value(UnitBaseAttributes.CONSTITUTION, 4)
	unit_base_attributes.set_attribute_value(UnitBaseAttributes.PERCEPTION, 3)
	unit_base_attributes.set_attribute_value(UnitBaseAttributes.INTELLIGENCE, 2)
	unit_base_attributes.set_attribute_value(UnitBaseAttributes.WILLPOWER, 2)
	unit_base_attributes.custom_stats[&"storage_space"] = storage_space
	member_state.progression.unit_base_attributes = unit_base_attributes
	member_state.current_hp = 24
	member_state.current_mp = 8

	party_state.set_member_state(member_state)
	party_state.active_member_ids = [member_id]
	party_state.reserve_member_ids = []
	party_state.leader_member_id = member_id
	return party_state


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _finish() -> void:
	if _failures.is_empty():
		print("Party equipment regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Party equipment regression: FAIL (%d)" % _failures.size())
	quit(1)
