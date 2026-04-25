extends SceneTree

const AttributeService = preload("res://scripts/systems/attribute_service.gd")
const CharacterManagementModule = preload("res://scripts/systems/character_management_module.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")
const PartyEquipmentService = preload("res://scripts/systems/party_equipment_service.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyWarehouseService = preload("res://scripts/systems/party_warehouse_service.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")
const UnitProgress = preload("res://scripts/player/progression/unit_progress.gd")
const EquipmentRequirement = preload("res://scripts/player/equipment/equipment_requirement.gd")
const EquipmentState = preload("res://scripts/player/equipment/equipment_state.gd")
const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_item_registry_accepts_equipment_seed_data()
	_test_melee_weapons_declare_exactly_one_physical_damage_tag()
	_test_equipment_service_moves_items_between_warehouse_and_slots()
	_test_equipment_modifiers_change_attribute_snapshot_and_round_trip()
	_test_equipment_state_requires_canonical_payload()
	_test_two_handed_weapon_occupies_both_slots()
	_test_two_handed_weapon_displaces_existing_main_and_off_hand()
	_test_two_handed_weapon_attribute_not_double_counted()
	_test_atomic_rollback_when_warehouse_full()
	_test_preview_equip_returns_displaced_entries()
	_test_requirement_profession_check()
	_test_equip_creates_instance_id_in_slot()
	_test_instance_id_preserved_through_unequip_and_reequip()
	_test_two_items_of_same_type_get_different_instance_ids()
	_test_equipment_instance_rarity_round_trip_and_legacy_fallback()
	_finish()


func _test_item_registry_accepts_equipment_seed_data() -> void:
	var registry := ItemContentRegistry.new()
	_assert_true(registry.validate().is_empty(), "装备种子物品定义应通过 ItemContentRegistry 校验。")

	var item_defs := registry.get_item_defs()
	var bronze_sword = item_defs.get(&"bronze_sword")
	var leather_cap = item_defs.get(&"leather_cap")
	var leather_jerkin = item_defs.get(&"leather_jerkin")
	var scout_charm = item_defs.get(&"scout_charm")
	var iron_greatsword = item_defs.get(&"iron_greatsword")
	var militia_axe = item_defs.get(&"militia_axe")
	var watchman_mace = item_defs.get(&"watchman_mace")
	var scout_dagger = item_defs.get(&"scout_dagger")

	_assert_true(bronze_sword != null and bronze_sword.is_equipment(), "青铜短剑应注册为可装备物品。")
	_assert_true(leather_cap != null and leather_cap.is_equipment(), "皮革护帽应注册为可装备物品。")
	_assert_true(leather_jerkin != null and leather_jerkin.is_equipment(), "皮革短甲应注册为可装备物品。")
	_assert_true(scout_charm != null and scout_charm.is_equipment(), "斥候护符应注册为可装备物品。")
	_assert_true(iron_greatsword != null and iron_greatsword.is_equipment(), "铁制大剑应注册为可装备物品。")
	_assert_true(militia_axe != null and militia_axe.is_equipment(), "民兵手斧应注册为可装备物品。")
	_assert_true(watchman_mace != null and watchman_mace.is_equipment(), "卫兵钉锤应注册为可装备物品。")
	_assert_true(scout_dagger != null and scout_dagger.is_equipment(), "斥候匕首应注册为可装备物品。")
	_assert_eq(bronze_sword.get_equipment_type_id_normalized(), &"weapon", "青铜短剑应归类为 weapon。")
	_assert_eq(leather_cap.get_equipment_type_id_normalized(), &"armor", "皮革护帽应归类为 armor。")
	_assert_eq(leather_jerkin.get_equipment_type_id_normalized(), &"armor", "皮革短甲应归类为 armor。")
	_assert_eq(scout_charm.get_equipment_type_id_normalized(), &"accessory", "斥候护符应归类为 accessory。")
	_assert_eq(iron_greatsword.get_equipment_type_id_normalized(), &"weapon", "铁制大剑应归类为 weapon。")
	_assert_eq(militia_axe.get_equipment_type_id_normalized(), &"weapon", "民兵手斧应归类为 weapon。")
	_assert_eq(watchman_mace.get_equipment_type_id_normalized(), &"weapon", "卫兵钉锤应归类为 weapon。")
	_assert_eq(scout_dagger.get_equipment_type_id_normalized(), &"weapon", "斥候匕首应归类为 weapon。")
	_assert_true(bronze_sword.is_weapon(), "青铜短剑应通过 is_weapon()。")
	_assert_true(leather_cap.is_armor(), "皮革护帽应通过 is_armor()。")
	_assert_true(leather_jerkin.is_armor(), "皮革短甲应通过 is_armor()。")
	_assert_true(scout_charm.is_accessory(), "斥候护符应通过 is_accessory()。")
	_assert_eq(iron_greatsword.get_final_occupied_slot_ids(&"main_hand").size(), 2, "铁制大剑应声明占用 2 个槽位。")
	_assert_eq(
		leather_cap.get_tags(),
		[&"armor", &"head", &"leather", &"light_armor"],
		"皮革护帽应补齐头部护具标签。"
	)
	_assert_eq(leather_cap.get_buy_price(), 100, "皮革护帽应声明购买价格。")
	_assert_eq(leather_cap.get_sell_price(), 50, "皮革护帽应声明出售价格。")
	_assert_eq(leather_cap.get_equipment_slot_ids(), [&"head"], "皮革护帽应声明头部槽位。")
	_assert_eq(leather_cap.get_final_occupied_slot_ids(&"head"), [&"head"], "皮革护帽应只占用头部槽。")
	_assert_eq(
		bronze_sword.get_tags(),
		[&"weapon", &"melee", &"one_handed", &"sword", &"weapon_class_sword"],
		"青铜短剑应补齐单手剑标签。"
	)
	_assert_eq(
		militia_axe.get_tags(),
		[&"weapon", &"melee", &"one_handed", &"axe", &"weapon_class_axe"],
		"民兵手斧应补齐单手斧标签。"
	)
	_assert_eq(militia_axe.get_buy_price(), 145, "民兵手斧应声明购买价格。")
	_assert_eq(militia_axe.get_sell_price(), 70, "民兵手斧应声明出售价格。")
	_assert_eq(militia_axe.get_equipment_slot_ids(), [&"main_hand"], "民兵手斧应声明主手槽位。")
	_assert_eq(militia_axe.get_final_occupied_slot_ids(&"main_hand"), [&"main_hand"], "民兵手斧应只占用主手槽。")
	_assert_eq(
		watchman_mace.get_tags(),
		[&"weapon", &"melee", &"one_handed", &"mace", &"weapon_class_mace"],
		"卫兵钉锤应补齐单手钉锤标签。"
	)
	_assert_eq(
		scout_dagger.get_tags(),
		[&"weapon", &"melee", &"one_handed", &"dagger", &"weapon_class_dagger"],
		"斥候匕首应补齐单手匕首标签。"
	)
	_assert_eq(watchman_mace.get_buy_price(), 175, "卫兵钉锤应声明购买价格。")
	_assert_eq(watchman_mace.get_sell_price(), 85, "卫兵钉锤应声明出售价格。")
	_assert_eq(scout_dagger.get_buy_price(), 130, "斥候匕首应声明购买价格。")
	_assert_eq(scout_dagger.get_sell_price(), 65, "斥候匕首应声明出售价格。")

	var one_handed_weapon_classes: Dictionary = {}
	var covered_equipment_slots: Dictionary = {}
	for item_def_variant in item_defs.values():
		if item_def_variant is not ItemDef:
			continue
		var item_def: ItemDef = item_def_variant
		if item_def.is_equipment():
			for slot_id in item_def.get_equipment_slot_ids():
				covered_equipment_slots[slot_id] = true
		if not item_def.is_weapon():
			continue
		if item_def.get_equipment_slot_ids() != [&"main_hand"]:
			continue
		if item_def.get_final_occupied_slot_ids(&"main_hand") != [&"main_hand"]:
			continue
		for tag in item_def.get_tags():
			if not String(tag).begins_with("weapon_class_"):
				continue
			one_handed_weapon_classes[tag] = true
	_assert_true(
		one_handed_weapon_classes.size() >= 4,
		"当前单手武器种子至少应覆盖 4 个 weapon_class_* 标签。"
	)
	_assert_true(
		covered_equipment_slots.has(&"head"),
		"正式装备种子至少应覆盖 head 槽位。"
	)


func _test_melee_weapons_declare_exactly_one_physical_damage_tag() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var expected_weapon_tags := {
		&"bronze_sword": ItemDef.DAMAGE_TAG_PHYSICAL_SLASH,
		&"iron_greatsword": ItemDef.DAMAGE_TAG_PHYSICAL_SLASH,
		&"militia_axe": ItemDef.DAMAGE_TAG_PHYSICAL_SLASH,
		&"watchman_mace": ItemDef.DAMAGE_TAG_PHYSICAL_BLUNT,
		&"scout_dagger": ItemDef.DAMAGE_TAG_PHYSICAL_PIERCE,
	}
	var covered_melee_weapon_count := 0
	for item_def_variant in item_defs.values():
		var item_def := item_def_variant as ItemDef
		if item_def == null or not item_def.is_weapon() or not item_def.get_tags().has(&"melee"):
			continue
		covered_melee_weapon_count += 1
		var damage_tag := item_def.get_weapon_physical_damage_tag()
		_assert_true(
			ItemDef.get_valid_weapon_physical_damage_tags().has(damage_tag),
			"近战武器 %s 必须声明唯一有效的物理伤害类型。" % String(item_def.item_id)
		)
	for item_id in expected_weapon_tags.keys():
		var item_def := item_defs.get(item_id) as ItemDef
		_assert_true(item_def != null, "正式近战武器 %s 应存在。" % String(item_id))
		if item_def == null:
			continue
		_assert_eq(
			item_def.get_weapon_physical_damage_tag(),
			expected_weapon_tags.get(item_id),
			"近战武器 %s 应映射到指定的唯一物理伤害类型。" % String(item_id)
		)
	_assert_true(covered_melee_weapon_count >= expected_weapon_tags.size(), "正式近战武器种子应全部纳入伤害类型约束。")


func _test_equipment_service_moves_items_between_warehouse_and_slots() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)

	warehouse_service.add_item(&"bronze_sword", 1)
	warehouse_service.add_item(&"leather_cap", 1)
	warehouse_service.add_item(&"scout_charm", 2)

	var sword_result := equipment_service.equip_item(&"hero", &"bronze_sword")
	_assert_true(bool(sword_result.get("success", false)), "共享仓库中的武器应能装备到角色主手。")
	_assert_eq(String(sword_result.get("slot_id", "")), "main_hand", "武器应进入主手槽。")
	_assert_eq(warehouse_service.count_item(&"bronze_sword"), 0, "装备武器后，共享仓库中的对应库存应扣减。")

	var cap_result := equipment_service.equip_item(&"hero", &"leather_cap")
	_assert_true(bool(cap_result.get("success", false)), "头部护具应能自动装备到 head 槽。")
	_assert_eq(String(cap_result.get("slot_id", "")), "head", "头部护具应进入 head 槽。")
	_assert_eq(warehouse_service.count_item(&"leather_cap"), 0, "装备头部护具后，共享仓库中的对应库存应扣减。")

	var first_charm_result := equipment_service.equip_item(&"hero", &"scout_charm")
	_assert_true(bool(first_charm_result.get("success", false)), "第一枚饰品应能自动装备。")
	_assert_eq(String(first_charm_result.get("slot_id", "")), "accessory_1", "第一枚饰品应优先进入饰品一槽。")

	var second_charm_result := equipment_service.equip_item(&"hero", &"scout_charm")
	_assert_true(bool(second_charm_result.get("success", false)), "第二枚饰品应能继续自动装备。")
	_assert_eq(String(second_charm_result.get("slot_id", "")), "accessory_2", "第二枚饰品应自动进入空闲饰品槽。")
	_assert_eq(warehouse_service.count_item(&"scout_charm"), 0, "两枚饰品都装备后，仓库中不应残留对应库存。")

	var equipment_state = party_state.get_member_state(&"hero").equipment_state
	_assert_eq(String(equipment_state.get_equipped_item_id(&"main_hand")), "bronze_sword", "角色主手状态应记录已装备武器。")
	_assert_eq(String(equipment_state.get_equipped_item_id(&"head")), "leather_cap", "头部槽应记录皮革护帽。")
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
	warehouse_service.add_item(&"leather_cap", 1)
	warehouse_service.add_item(&"leather_jerkin", 1)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)
	equipment_service.equip_item(&"hero", &"bronze_sword")
	equipment_service.equip_item(&"hero", &"leather_cap")
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
		after_snapshot.get_value(AttributeService.ATTACK_BONUS) - before_snapshot.get_value(AttributeService.ATTACK_BONUS),
		2,
		"青铜短剑应提供攻击检定加值。"
	)
	_assert_eq(
		after_snapshot.get_value(AttributeService.ARMOR_AC_BONUS) - before_snapshot.get_value(AttributeService.ARMOR_AC_BONUS),
		3,
		"皮革短甲与皮革护帽应合计提供护甲 AC 加值。"
	)
	_assert_eq(
		after_snapshot.get_value(AttributeService.ARMOR_CLASS) - before_snapshot.get_value(AttributeService.ARMOR_CLASS),
		4,
		"皮革短甲、皮革护帽与闪避加值应合计提高 AC。"
	)
	_assert_eq(
		after_snapshot.get_value(AttributeService.HP_MAX) - before_snapshot.get_value(AttributeService.HP_MAX),
		6,
		"皮革短甲应为生命上限提供固定加值。"
	)
	_assert_eq(
		after_snapshot.get_value(AttributeService.DODGE_BONUS) - before_snapshot.get_value(AttributeService.DODGE_BONUS),
		1,
		"皮革护帽应提供闪避加值。"
	)

	var restored_party_state = PartyState.from_dict(party_state.to_dict())
	var restored_equipment_state = restored_party_state.get_member_state(&"hero").equipment_state
	_assert_eq(String(restored_equipment_state.get_equipped_item_id(&"main_hand")), "bronze_sword", "序列化往返后应保留主手装备。")
	_assert_eq(String(restored_equipment_state.get_equipped_item_id(&"head")), "leather_cap", "序列化往返后应保留头部装备。")
	_assert_eq(String(restored_equipment_state.get_equipped_item_id(&"body")), "leather_jerkin", "序列化往返后应保留身躯装备。")


func _test_equipment_state_requires_canonical_payload() -> void:
	var legacy_state = EquipmentState.from_dict({
		"main_hand": "bronze_sword",
		"body": {
			"item_id": "leather_jerkin",
		},
	})
	_assert_true(legacy_state == null, "旧版裸字典 equipment_state 不再支持。")


func _test_two_handed_weapon_occupies_both_slots() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)

	warehouse_service.add_item(&"iron_greatsword", 1)
	var result := equipment_service.equip_item(&"hero", &"iron_greatsword")
	_assert_true(bool(result.get("success", false)), "铁制大剑应能装备成功。")
	_assert_eq(String(result.get("slot_id", "")), "main_hand", "铁制大剑入口槽应为 main_hand。")

	var equipment_state = party_state.get_member_state(&"hero").equipment_state
	_assert_eq(String(equipment_state.get_equipped_item_id(&"main_hand")), "iron_greatsword", "主手应记录铁制大剑。")
	_assert_eq(String(equipment_state.get_equipped_item_id(&"off_hand")), "iron_greatsword", "副手也应被铁制大剑占用。")
	_assert_eq(equipment_state.get_equipped_count(), 1, "双手武器只算 1 件装备。")
	_assert_eq(equipment_state.get_filled_slot_ids().size(), 2, "双手武器应使 2 个槽位显示为已占用。")

	# round-trip
	var restored = PartyState.from_dict(party_state.to_dict())
	var restored_eq = restored.get_member_state(&"hero").equipment_state
	_assert_eq(String(restored_eq.get_equipped_item_id(&"main_hand")), "iron_greatsword", "序列化往返后主手应保留大剑。")
	_assert_eq(String(restored_eq.get_equipped_item_id(&"off_hand")), "iron_greatsword", "序列化往返后副手也应保持占用。")
	_assert_eq(restored_eq.get_equipped_count(), 1, "序列化往返后双手武器仍只算 1 件。")


func _test_two_handed_weapon_displaces_existing_main_and_off_hand() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)

	# 直接设置装备状态（模拟已装备主手+副手），这两件物品不在仓库里
	var hero_state = party_state.get_member_state(&"hero")
	if hero_state.equipment_state == null or not (hero_state.equipment_state is Object and hero_state.equipment_state.has_method("set_equipped_entry")):
		hero_state.equipment_state = EquipmentState.new()
	var eq_state = hero_state.equipment_state
	var occ_main: Array[StringName] = [&"main_hand"]
	var occ_off: Array[StringName] = [&"off_hand"]
	eq_state.set_equipped_entry(&"main_hand", &"bronze_sword", occ_main)
	eq_state.set_equipped_entry(&"off_hand", &"scout_charm", occ_off)

	_assert_eq(String(eq_state.get_equipped_item_id(&"main_hand")), "bronze_sword", "前置：主手应有单手剑。")
	_assert_eq(String(eq_state.get_equipped_item_id(&"off_hand")), "scout_charm", "前置：副手应有饰品。")

	# 现在装双手大剑：应把两件都踢回仓库（仓库容量 8，完全可以接收）
	warehouse_service.add_item(&"iron_greatsword", 1)
	var result := equipment_service.equip_item(&"hero", &"iron_greatsword")
	_assert_true(bool(result.get("success", false)), "双手大剑替换主+副手应成功。")
	_assert_eq(String(eq_state.get_equipped_item_id(&"main_hand")), "iron_greatsword", "主手应换为大剑。")
	_assert_eq(String(eq_state.get_equipped_item_id(&"off_hand")), "iron_greatsword", "副手应被大剑占用。")
	_assert_eq(warehouse_service.count_item(&"bronze_sword"), 1, "被替换的单手剑应回仓。")
	_assert_eq(warehouse_service.count_item(&"scout_charm"), 1, "被替换的副手饰品应回仓。")


func _test_two_handed_weapon_attribute_not_double_counted() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var progression_registry := ProgressionContentRegistry.new()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)

	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	warehouse_service.add_item(&"iron_greatsword", 1)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)
	equipment_service.equip_item(&"hero", &"iron_greatsword")

	var baseline_manager := CharacterManagementModule.new()
	baseline_manager.setup(party_state, progression_registry.get_skill_defs(), progression_registry.get_profession_defs(), {}, item_defs)
	var snapshot = baseline_manager.get_member_attribute_snapshot(&"hero")

	# iron_greatsword 声明 attack_bonus +2，不应因占 2 槽而翻倍
	var empty_party := _build_party_with_member(&"blank", "Blank", 8)
	var empty_manager := CharacterManagementModule.new()
	empty_manager.setup(empty_party, progression_registry.get_skill_defs(), progression_registry.get_profession_defs(), {}, item_defs)
	var empty_snapshot = empty_manager.get_member_attribute_snapshot(&"blank")

	_assert_eq(
		snapshot.get_value(AttributeService.ATTACK_BONUS) - empty_snapshot.get_value(AttributeService.ATTACK_BONUS),
		2,
		"双手大剑攻击检定加值应精确为 +2，不得因占两槽而翻倍。"
	)


func _test_atomic_rollback_when_warehouse_full() -> void:
	# 测试 preview_batch_swap 在仓库只有 1 格但需要回仓 2 件时正确拒绝
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var party_state := _build_party_with_member(&"hero", "Hero", 1)  # capacity = 1
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)

	# 仓库放 1 件大剑（已满）
	warehouse_service.add_item(&"iron_greatsword", 1)
	_assert_eq(warehouse_service.get_free_slots(), 0, "前置：仓库应满。")

	# 批量预览：取出大剑后空出 1 格，但需要回仓 bronze_sword + scout_charm 共 2 件 → 第 2 件放不下
	var items_to_withdraw: Array[StringName] = [&"iron_greatsword"]
	var items_to_deposit: Array[StringName] = [&"bronze_sword", &"scout_charm"]
	var preview := warehouse_service.preview_batch_swap(items_to_withdraw, items_to_deposit)
	_assert_true(not bool(preview.get("allowed", false)), "仓库容量不足时批量回仓预览应失败。")
	_assert_eq(preview.get("error_code", ""), "warehouse_blocked_swap", "错误码应为 warehouse_blocked_swap。")

	# preview 不应修改仓库状态
	_assert_eq(warehouse_service.count_item(&"iron_greatsword"), 1, "preview_batch_swap 不应消耗仓库库存。")
	_assert_eq(warehouse_service.get_free_slots(), 0, "preview_batch_swap 不应改变仓库占用格数。")


func _test_preview_equip_returns_displaced_entries() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)

	warehouse_service.add_item(&"bronze_sword", 1)
	warehouse_service.add_item(&"iron_greatsword", 1)
	equipment_service.equip_item(&"hero", &"bronze_sword")

	var preview := equipment_service.preview_equip(&"hero", &"iron_greatsword")
	_assert_true(bool(preview.get("success", false)), "preview_equip 对合法换装应返回 success=true。")
	_assert_eq(String(preview.get("entry_slot_id", "")), "main_hand", "preview 的入口槽应为 main_hand。")
	_assert_eq(preview.get("occupied_slot_ids", []).size(), 2, "preview 的 occupied_slot_ids 应有 2 个。")
	var displaced: Array = preview.get("displaced_entries", [])
	_assert_eq(displaced.size(), 1, "preview 应识别出 1 条被替换条目。")
	if not displaced.is_empty():
		_assert_eq(String(displaced[0].get("item_id", "")), "bronze_sword", "被替换条目应为青铜短剑。")

	# preview 不应改变状态
	_assert_eq(String(party_state.get_member_state(&"hero").equipment_state.get_equipped_item_id(&"main_hand")), "bronze_sword", "preview_equip 不应修改状态。")
	_assert_eq(warehouse_service.count_item(&"iron_greatsword"), 1, "preview_equip 不应消耗仓库库存。")


func _test_requirement_profession_check() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)

	# 手动构造一个带职业要求的 ItemDef（不写磁盘，运行时构造）
	var sword_def = item_defs.get(&"bronze_sword").duplicate()
	var req := EquipmentRequirement.new()
	req.required_profession_ids = ["warrior"]
	sword_def.equip_requirement = req

	var patched_defs := item_defs.duplicate()
	patched_defs[&"bronze_sword"] = sword_def
	var patched_equipment_service := PartyEquipmentService.new()
	var patched_warehouse := PartyWarehouseService.new()
	patched_warehouse.setup(party_state, patched_defs)
	patched_equipment_service.setup(party_state, patched_defs, patched_warehouse)
	patched_warehouse.add_item(&"bronze_sword", 1)

	# 角色没有 warrior 职业，装备应失败
	var result := patched_equipment_service.equip_item(&"hero", &"bronze_sword")
	_assert_true(not bool(result.get("success", false)), "不满足职业要求时装备应失败。")
	_assert_eq(result.get("error_code", ""), "missing_profession", "错误码应为 missing_profession。")
	_assert_eq(patched_warehouse.count_item(&"bronze_sword"), 1, "资格不符时仓库库存不应被消耗。")

	# preview 也应返回相同失败信息
	var preview := patched_equipment_service.preview_equip(&"hero", &"bronze_sword")
	_assert_true(not bool(preview.get("success", false)), "preview_equip 也应返回失败。")
	_assert_eq(preview.get("error_code", ""), "missing_profession", "preview 错误码应为 missing_profession。")
	_assert_true(("missing_profession" in preview.get("blockers", [])), "blockers 应包含 missing_profession。")


func _test_equip_creates_instance_id_in_slot() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)

	warehouse_service.add_item(&"bronze_sword", 1)
	equipment_service.equip_item(&"hero", &"bronze_sword")

	var equipment_state = party_state.get_member_state(&"hero").equipment_state
	var instance_id: StringName = equipment_state.get_equipped_instance_id(&"main_hand")
	_assert_true(instance_id != &"", "装备后主手槽应有非空的 instance_id。")
	_assert_true(String(instance_id).begins_with("eq_"), "instance_id 应以 eq_ 开头。")

	# 仓库中不应再有该装备的实例或堆叠
	_assert_eq(warehouse_service.count_item(&"bronze_sword"), 0, "装备后仓库中对应物品应为 0。")

	# 序列化往返后 instance_id 不变
	var restored = PartyState.from_dict(party_state.to_dict())
	var restored_eq = restored.get_member_state(&"hero").equipment_state
	_assert_eq(
		String(restored_eq.get_equipped_instance_id(&"main_hand")),
		String(instance_id),
		"序列化往返后 instance_id 应保持不变。"
	)


func _test_instance_id_preserved_through_unequip_and_reequip() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)

	warehouse_service.add_item(&"bronze_sword", 1)
	equipment_service.equip_item(&"hero", &"bronze_sword")

	var equipment_state = party_state.get_member_state(&"hero").equipment_state
	var original_instance_id: StringName = equipment_state.get_equipped_instance_id(&"main_hand")
	_assert_true(original_instance_id != &"", "前置：装备后应有 instance_id。")

	# 卸装 → 仓库里应有同一个实例
	equipment_service.unequip_item(&"hero", &"main_hand")
	_assert_eq(warehouse_service.count_item(&"bronze_sword"), 1, "卸装后物品应回到仓库。")

	var ws = party_state.warehouse_state
	var found_instance: bool = false
	for inst in ws.get_non_empty_instances():
		if String(inst.instance_id) == String(original_instance_id):
			found_instance = true
			break
	_assert_true(found_instance, "卸装后仓库中应能找到原来的 instance_id。")

	# 重新装备 → 同一个实例被拿回来
	equipment_service.equip_item(&"hero", &"bronze_sword")
	var reequip_instance_id: StringName = equipment_state.get_equipped_instance_id(&"main_hand")
	_assert_eq(
		String(reequip_instance_id),
		String(original_instance_id),
		"重新装备后 instance_id 应与首次装备一致。"
	)


func _test_two_items_of_same_type_get_different_instance_ids() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)

	warehouse_service.add_item(&"scout_charm", 2)
	equipment_service.equip_item(&"hero", &"scout_charm")
	equipment_service.equip_item(&"hero", &"scout_charm")

	var equipment_state = party_state.get_member_state(&"hero").equipment_state
	var id1: StringName = equipment_state.get_equipped_instance_id(&"accessory_1")
	var id2: StringName = equipment_state.get_equipped_instance_id(&"accessory_2")
	_assert_true(id1 != &"", "饰品一槽应有 instance_id。")
	_assert_true(id2 != &"", "饰品二槽应有 instance_id。")
	_assert_true(id1 != id2, "同种装备的两个实例应拥有不同的 instance_id。")


func _test_equipment_instance_rarity_round_trip_and_legacy_fallback() -> void:
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var epic_instance := EquipmentInstanceState.create(&"bronze_sword")
	epic_instance.rarity = EquipmentInstanceState.RarityTier.EPIC
	party_state.warehouse_state.equipment_instances = [epic_instance]

	var restored_party_state = PartyState.from_dict(party_state.to_dict())
	_assert_true(restored_party_state != null, "带 rarity 的 PartyState round-trip 应成功。")
	if restored_party_state == null:
		return

	var restored_instances: Array = restored_party_state.warehouse_state.get_non_empty_instances()
	_assert_eq(restored_instances.size(), 1, "带 rarity 的装备实例 round-trip 后应保持 1 条。")
	if restored_instances.is_empty():
		return

	var restored_instance = restored_instances[0]
	_assert_eq(
		int(restored_instance.rarity),
		int(EquipmentInstanceState.RarityTier.EPIC),
		"装备实例 round-trip 后应保留 rarity tier。"
	)

	var legacy_instance = EquipmentInstanceState.from_dict({
		"instance_id": "eq_legacy_bronze_sword",
		"item_id": "bronze_sword",
		"current_durability": -1,
		"armor_wear_progress": 0.0,
		"weapon_wear_progress": 0.0,
	})
	_assert_eq(
		int(legacy_instance.rarity),
		int(EquipmentInstanceState.RarityTier.COMMON),
		"旧存档缺少 rarity 字段时应回退为 COMMON。"
	)
	_assert_eq(
		int(legacy_instance.to_dict().get("rarity", -1)),
		int(EquipmentInstanceState.RarityTier.COMMON),
		"旧存档回填后的实例再次序列化时应带上 rarity 字段。"
	)


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
	party_state.main_character_member_id = member_id
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
