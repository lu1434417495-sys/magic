extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const AttributeService = preload("res://scripts/systems/attributes/attribute_service.gd")
const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")
const PartyEquipmentService = preload("res://scripts/systems/inventory/party_equipment_service.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyWarehouseService = preload("res://scripts/systems/inventory/party_warehouse_service.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")
const UnitProgress = preload("res://scripts/player/progression/unit_progress.gd")
const EquipmentRequirement = preload("res://scripts/player/equipment/equipment_requirement.gd")
const EquipmentState = preload("res://scripts/player/equipment/equipment_state.gd")
const EquipmentEntryState = preload("res://scripts/player/equipment/equipment_entry_state.gd")
const EquipmentDurabilityRules = preload("res://scripts/player/equipment/equipment_durability_rules.gd")
const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const WeaponProfileDef = preload("res://scripts/player/warehouse/weapon_profile_def.gd")

const BG3_WEAPON_SEED_ITEMS := {
	&"club": &"oak_club",
	&"dagger": &"iron_dagger",
	&"handaxe": &"militia_axe",
	&"javelin": &"hunting_javelin",
	&"light_hammer": &"smith_light_hammer",
	&"mace": &"watchman_mace",
	&"sickle": &"farmer_sickle",
	&"quarterstaff": &"oak_quarterstaff",
	&"spear": &"militia_spear",
	&"greatclub": &"iron_greatclub",
	&"light_crossbow": &"militia_light_crossbow",
	&"shortbow": &"ash_shortbow",
	&"flail": &"iron_flail",
	&"morningstar": &"iron_morningstar",
	&"rapier": &"duelist_rapier",
	&"scimitar": &"curved_scimitar",
	&"shortsword": &"bronze_sword",
	&"war_pick": &"iron_war_pick",
	&"battleaxe": &"soldier_battleaxe",
	&"longsword": &"steel_longsword",
	&"trident": &"guard_trident",
	&"warhammer": &"iron_warhammer",
	&"glaive": &"soldier_glaive",
	&"greataxe": &"raider_greataxe",
	&"greatsword": &"iron_greatsword",
	&"halberd": &"steel_halberd",
	&"maul": &"stone_maul",
	&"pike": &"soldier_pike",
	&"hand_crossbow": &"compact_hand_crossbow",
	&"heavy_crossbow": &"siege_heavy_crossbow",
	&"longbow": &"ash_longbow",
}

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_item_registry_accepts_equipment_seed_data()
	_test_all_bg3_weapon_types_are_registered_as_weapon_equipment()
	_test_melee_weapons_declare_exactly_one_physical_damage_tag()
	_test_equipment_service_moves_items_between_warehouse_and_slots()
	_test_equipment_modifiers_change_attribute_snapshot_and_round_trip()
	_test_equipment_state_requires_canonical_payload()
	_test_equipment_entry_rejects_bad_schema()
	_test_two_handed_weapon_occupies_both_slots()
	_test_two_handed_weapon_displaces_existing_main_and_off_hand()
	_test_two_handed_weapon_attribute_not_double_counted()
	_test_atomic_rollback_when_warehouse_full()
	_test_preview_equip_returns_displaced_entries()
	_test_armor_max_dex_bonus_caps_positive_agility_ac()
	_test_requirement_profession_check()
	_test_equip_creates_instance_id_in_slot()
	_test_instance_id_preserved_through_unequip_and_reequip()
	_test_two_items_of_same_type_get_different_instance_ids()
	_test_weapon_profile_equipment_entry_round_trip()
	_test_equipped_instance_fields_survive_round_trip_and_unequip()
	_test_equipment_instance_rarity_round_trip_and_strict_schema()
	_test_duplicate_same_item_instance_id_selection()
	_test_party_state_rejects_duplicate_equipment_instance_ids()
	_finish()


func _test_item_registry_accepts_equipment_seed_data() -> void:
	var registry := ItemContentRegistry.new()
	_assert_true(registry.validate().is_empty(), "装备种子物品定义应通过 ItemContentRegistry 校验。")

	var item_defs := registry.get_item_defs()
	var bronze_sword = item_defs.get(&"bronze_sword")
	var leather_cap = item_defs.get(&"leather_cap")
	var leather_jerkin = item_defs.get(&"leather_jerkin")
	var iron_scale_mail = item_defs.get(&"iron_scale_mail")
	var scout_charm = item_defs.get(&"scout_charm")
	var iron_greatsword = item_defs.get(&"iron_greatsword")
	var militia_axe = item_defs.get(&"militia_axe")
	var watchman_mace = item_defs.get(&"watchman_mace")

	_assert_true(bronze_sword != null and bronze_sword.is_equipment(), "青铜短剑应注册为可装备物品。")
	_assert_true(leather_cap != null and leather_cap.is_equipment(), "皮革护帽应注册为可装备物品。")
	_assert_true(leather_jerkin != null and leather_jerkin.is_equipment(), "皮革短甲应注册为可装备物品。")
	_assert_true(iron_scale_mail != null and iron_scale_mail.is_equipment(), "铁鳞甲应注册为可装备物品。")
	_assert_true(scout_charm != null and scout_charm.is_equipment(), "斥候护符应注册为可装备物品。")
	_assert_true(iron_greatsword != null and iron_greatsword.is_equipment(), "铁制大剑应注册为可装备物品。")
	_assert_true(militia_axe != null and militia_axe.is_equipment(), "民兵手斧应注册为可装备物品。")
	_assert_true(watchman_mace != null and watchman_mace.is_equipment(), "卫兵钉锤应注册为可装备物品。")
	_assert_true(not item_defs.has(&"scout_dagger"), "斥候匕首应从正式装备种子中移除。")
	_assert_eq(bronze_sword.get_equipment_type_id_normalized(), &"weapon", "青铜短剑应归类为 weapon。")
	_assert_eq(leather_cap.get_equipment_type_id_normalized(), &"armor", "皮革护帽应归类为 armor。")
	_assert_eq(leather_jerkin.get_equipment_type_id_normalized(), &"armor", "皮革短甲应归类为 armor。")
	_assert_eq(iron_scale_mail.get_equipment_type_id_normalized(), &"armor", "铁鳞甲应归类为 armor。")
	_assert_eq(scout_charm.get_equipment_type_id_normalized(), &"accessory", "斥候护符应归类为 accessory。")
	_assert_eq(iron_greatsword.get_equipment_type_id_normalized(), &"weapon", "铁制大剑应归类为 weapon。")
	_assert_eq(militia_axe.get_equipment_type_id_normalized(), &"weapon", "民兵手斧应归类为 weapon。")
	_assert_eq(watchman_mace.get_equipment_type_id_normalized(), &"weapon", "卫兵钉锤应归类为 weapon。")
	_assert_true(bronze_sword.is_weapon(), "青铜短剑应通过 is_weapon()。")
	_assert_true(leather_cap.is_armor(), "皮革护帽应通过 is_armor()。")
	_assert_true(leather_jerkin.is_armor(), "皮革短甲应通过 is_armor()。")
	_assert_true(iron_scale_mail.is_armor(), "铁鳞甲应通过 is_armor()。")
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
	_assert_eq(leather_cap.get_max_dex_bonus(), -1, "皮革护帽不应声明身体护甲敏捷上限。")
	_assert_eq(
		leather_jerkin.get_tags(),
		[&"armor", &"body", &"leather", &"light_armor"],
		"皮革短甲应补齐身体皮甲标签。"
	)
	_assert_eq(leather_jerkin.get_equipment_slot_ids(), [&"body"], "皮革短甲应声明身体槽位。")
	_assert_eq(leather_jerkin.get_final_occupied_slot_ids(&"body"), [&"body"], "皮革短甲应只占用身体槽。")
	_assert_eq(leather_jerkin.get_max_dex_bonus(), 6, "皮革短甲应声明 3.5E 软皮甲敏捷上限。")
	_assert_eq(
		iron_scale_mail.get_tags(),
		[&"armor", &"body", &"metal", &"medium_armor", &"scale_mail"],
		"铁鳞甲应补齐中甲标签。"
	)
	_assert_eq(iron_scale_mail.get_buy_price(), 180, "铁鳞甲应声明购买价格。")
	_assert_eq(iron_scale_mail.get_sell_price(), 90, "铁鳞甲应声明出售价格。")
	_assert_eq(iron_scale_mail.get_equipment_slot_ids(), [&"body"], "铁鳞甲应声明身体槽位。")
	_assert_eq(iron_scale_mail.get_final_occupied_slot_ids(&"body"), [&"body"], "铁鳞甲应只占用身体槽。")
	_assert_eq(iron_scale_mail.get_max_dex_bonus(), 3, "铁鳞甲应声明鳞甲敏捷上限。")
	_assert_eq(
		bronze_sword.get_tags(),
		[&"weapon", &"melee", &"one_handed", &"shortsword", &"sword", &"weapon_class_sword", &"weapon_type_shortsword"],
		"青铜短剑应补齐 Shortsword 标签。"
	)
	_assert_eq(
		militia_axe.get_tags(),
		[&"weapon", &"melee", &"one_handed", &"handaxe", &"axe", &"weapon_class_axe", &"weapon_type_handaxe"],
		"民兵手斧应补齐 Handaxe 标签。"
	)
	_assert_eq(militia_axe.get_buy_price(), 145, "民兵手斧应声明购买价格。")
	_assert_eq(militia_axe.get_sell_price(), 70, "民兵手斧应声明出售价格。")
	_assert_eq(militia_axe.get_equipment_slot_ids(), [&"main_hand"], "民兵手斧应声明主手槽位。")
	_assert_eq(militia_axe.get_final_occupied_slot_ids(&"main_hand"), [&"main_hand"], "民兵手斧应只占用主手槽。")
	_assert_eq(
		watchman_mace.get_tags(),
		[&"weapon", &"melee", &"one_handed", &"mace", &"weapon_class_mace", &"weapon_type_mace"],
		"卫兵钉锤应补齐单手钉锤标签。"
	)
	_assert_eq(watchman_mace.get_buy_price(), 175, "卫兵钉锤应声明购买价格。")
	_assert_eq(watchman_mace.get_sell_price(), 85, "卫兵钉锤应声明出售价格。")

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
		one_handed_weapon_classes.size() >= 3,
		"当前单手武器种子至少应覆盖 3 个 weapon_class_* 标签。"
	)
	_assert_true(
		covered_equipment_slots.has(&"head"),
		"正式装备种子至少应覆盖 head 槽位。"
	)
	_assert_true(
		covered_equipment_slots.has(&"body"),
		"正式装备种子至少应覆盖 body 槽位。"
	)


func _test_all_bg3_weapon_types_are_registered_as_weapon_equipment() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	_assert_eq(BG3_WEAPON_SEED_ITEMS.size(), 31, "装备种子应覆盖 31 类 BG3 基础武器类型。")
	for weapon_type_id in BG3_WEAPON_SEED_ITEMS.keys():
		var item_id := ProgressionDataUtils.to_string_name(BG3_WEAPON_SEED_ITEMS[weapon_type_id])
		var item_def := item_defs.get(item_id) as ItemDef
		_assert_true(item_def != null, "BG3 weapon_type %s 应有正式装备实例 %s。" % [String(weapon_type_id), String(item_id)])
		if item_def == null:
			continue
		_assert_true(item_def.is_equipment(), "%s 应注册为装备。" % String(item_id))
		_assert_true(item_def.is_weapon(), "%s 应注册为武器。" % String(item_id))
		_assert_eq(item_def.get_equipment_slot_ids(), [&"main_hand"], "%s 应通过主手槽装备。" % String(item_id))
		_assert_true(item_def.get_weapon_attack_range() >= 1, "%s 应投影有效武器射程。" % String(item_id))
		_assert_true(
			ItemDef.get_valid_weapon_physical_damage_tags().has(item_def.get_weapon_physical_damage_tag()),
			"%s 应投影有效物理伤害类型。" % String(item_id)
		)
		var profile := item_def.get("weapon_profile") as WeaponProfileDef
		_assert_true(profile != null, "%s 应持有 WeaponProfileDef。" % String(item_id))
		if profile != null:
			_assert_eq(String(profile.weapon_type_id), String(weapon_type_id), "%s 应映射到指定 BG3 weapon_type_id。" % String(item_id))


func _test_melee_weapons_declare_exactly_one_physical_damage_tag() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var expected_weapon_tags := {
		&"bronze_sword": ItemDef.DAMAGE_TAG_PHYSICAL_PIERCE,
		&"iron_greatsword": ItemDef.DAMAGE_TAG_PHYSICAL_SLASH,
		&"militia_axe": ItemDef.DAMAGE_TAG_PHYSICAL_SLASH,
		&"watchman_mace": ItemDef.DAMAGE_TAG_PHYSICAL_BLUNT,
	}
	var expected_weapon_profiles := {
		&"bronze_sword": {
			"weapon_type_id": &"shortsword",
			"one_handed_dice": [1, 6, 0],
			"two_handed_dice": [],
			"properties": [&"finesse", &"light"],
		},
		&"iron_greatsword": {
			"weapon_type_id": &"greatsword",
			"one_handed_dice": [],
			"two_handed_dice": [2, 6, 0],
			"properties": [&"two_handed"],
		},
		&"militia_axe": {
			"weapon_type_id": &"handaxe",
			"one_handed_dice": [1, 6, 0],
			"two_handed_dice": [],
			"properties": [&"light", &"thrown"],
		},
		&"watchman_mace": {
			"weapon_type_id": &"mace",
			"one_handed_dice": [1, 6, 0],
			"two_handed_dice": [],
			"properties": [],
		},
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
		var profile := item_def.get("weapon_profile") as WeaponProfileDef
		_assert_true(profile != null, "近战武器 %s 应持有 WeaponProfileDef。" % String(item_id))
		if profile == null:
			continue
		var profile_expectation: Dictionary = expected_weapon_profiles.get(item_id, {})
		_assert_eq(String(profile.weapon_type_id), String(profile_expectation.get("weapon_type_id", &"")), "近战武器 %s 应映射到指定 BG3 weapon_type_id。" % String(item_id))
		_assert_eq(_dice_to_list(profile.one_handed_dice), profile_expectation.get("one_handed_dice", []), "近战武器 %s one_handed_dice 应符合 BG3 模板。" % String(item_id))
		_assert_eq(_dice_to_list(profile.two_handed_dice), profile_expectation.get("two_handed_dice", []), "近战武器 %s two_handed_dice 应符合 BG3 模板。" % String(item_id))
		_assert_eq(profile.get_properties(), profile_expectation.get("properties", []), "近战武器 %s properties 应符合 BG3 模板。" % String(item_id))
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
	warehouse_service.add_item(&"scout_charm", 1)
	var charm_instance_ids := _get_instance_ids_for_item(party_state, &"scout_charm")
	_assert_eq(charm_instance_ids.size(), 1, "前置：一枚饰品应生成一个实例 ID。")
	if charm_instance_ids.size() < 1:
		return

	var sword_result := equipment_service.equip_item(&"hero", &"bronze_sword")
	_assert_true(bool(sword_result.get("success", false)), "共享仓库中的武器应能装备到角色主手。")
	_assert_eq(String(sword_result.get("slot_id", "")), "main_hand", "武器应进入主手槽。")
	_assert_eq(warehouse_service.count_item(&"bronze_sword"), 0, "装备武器后，共享仓库中的对应库存应扣减。")

	var cap_result := equipment_service.equip_item(&"hero", &"leather_cap")
	_assert_true(bool(cap_result.get("success", false)), "头部护具应能自动装备到 head 槽。")
	_assert_eq(String(cap_result.get("slot_id", "")), "head", "头部护具应进入 head 槽。")
	_assert_eq(warehouse_service.count_item(&"leather_cap"), 0, "装备头部护具后，共享仓库中的对应库存应扣减。")

	var first_charm_result := equipment_service.equip_item(&"hero", &"scout_charm", &"", StringName(charm_instance_ids[0]))
	_assert_true(bool(first_charm_result.get("success", false)), "饰品应能自动装备。")
	_assert_eq(String(first_charm_result.get("slot_id", "")), "necklace", "饰品应优先进入项链槽。")
	_assert_eq(warehouse_service.count_item(&"scout_charm"), 0, "饰品装备后，仓库中不应残留对应库存。")

	var equipment_state = party_state.get_member_state(&"hero").equipment_state
	_assert_eq(String(equipment_state.get_equipped_item_id(&"main_hand")), "bronze_sword", "角色主手状态应记录已装备武器。")
	_assert_eq(String(equipment_state.get_equipped_item_id(&"head")), "leather_cap", "头部槽应记录皮革护帽。")
	_assert_eq(String(equipment_state.get_equipped_item_id(&"necklace")), "scout_charm", "项链槽应记录饰品。")

	var unequip_result := equipment_service.unequip_item(&"hero", &"necklace")
	_assert_true(bool(unequip_result.get("success", false)), "已装备饰品应能卸回共享仓库。")
	_assert_eq(warehouse_service.count_item(&"scout_charm"), 1, "卸装后物品应回到共享仓库。")
	_assert_eq(String(equipment_state.get_equipped_item_id(&"necklace")), "", "卸装后的槽位应清空。")


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
		0,
		"皮革短甲不应提供生命上限加值。"
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

	var valid_state = EquipmentState.from_dict({
		"equipped_slots": {
			"main_hand": _make_equipment_entry_payload(&"bronze_sword", &"eq_schema_valid_bronze_sword", ["main_hand"]),
		},
	})
	_assert_true(valid_state != null, "当前 equipped_slots + equipment_instance payload 应可读取。")

	var extra_top_level_state = EquipmentState.from_dict({
		"equipped_slots": {},
		"legacy_equipped_items": {},
	})
	_assert_true(extra_top_level_state == null, "equipment_state 含额外旧顶层字段应拒绝。")

	var invalid_slot_state = EquipmentState.from_dict({
		"equipped_slots": {
			"weapon": _make_equipment_entry_payload(&"bronze_sword", &"eq_schema_invalid_slot", ["main_hand"]),
		},
	})
	_assert_true(invalid_slot_state == null, "equipment_state 遇到非法 slot key 应拒绝，而不是跳过。")

	var invalid_entry_state = EquipmentState.from_dict({
		"equipped_slots": {
			"main_hand": {
				"occupied_slot_ids": ["main_hand"],
			},
		},
	})
	_assert_true(invalid_entry_state == null, "equipment_state 遇到坏 entry payload 应拒绝，而不是跳过。")

	var mismatched_entry_slot_state = EquipmentState.from_dict({
		"equipped_slots": {
			"main_hand": _make_equipment_entry_payload(&"scout_charm", &"eq_schema_mismatched_slot", ["necklace"]),
		},
	})
	_assert_true(mismatched_entry_slot_state == null, "entry key 必须包含在 occupied_slot_ids 内。")

	var overlapping_slot_state = EquipmentState.from_dict({
		"equipped_slots": {
			"main_hand": _make_equipment_entry_payload(&"bronze_sword", &"eq_schema_overlap_sword", ["main_hand", "off_hand"]),
			"off_hand": _make_equipment_entry_payload(&"scout_charm", &"eq_schema_overlap_charm", ["off_hand"]),
		},
	})
	_assert_true(overlapping_slot_state == null, "equipment_state 遇到 occupied slot 重叠应拒绝，而不是后写覆盖。")


func _test_equipment_entry_rejects_bad_schema() -> void:
	var valid_entry = EquipmentEntryState.from_dict(
		_make_equipment_entry_payload(&"bronze_sword", &"eq_schema_entry_valid", ["main_hand"])
	)
	_assert_true(valid_entry != null, "当前装备 entry payload 应可读取。")

	var missing_instance_payload := _make_equipment_entry_payload(&"bronze_sword", &"eq_schema_missing_instance", ["main_hand"])
	missing_instance_payload.erase("equipment_instance")
	_assert_true(
		EquipmentEntryState.from_dict(missing_instance_payload) == null,
		"equipment entry 缺少 equipment_instance 应拒绝。"
	)

	var extra_entry_payload := _make_equipment_entry_payload(&"bronze_sword", &"eq_schema_extra_entry", ["main_hand"])
	extra_entry_payload["legacy_item_id"] = "bronze_sword"
	_assert_true(
		EquipmentEntryState.from_dict(extra_entry_payload) == null,
		"equipment entry 含额外旧字段应拒绝。"
	)

	_assert_true(
		EquipmentEntryState.from_dict(_make_equipment_entry_payload(&"bronze_sword", &"eq_schema_empty_slot", [""])) == null,
		"equipment entry 的空 slot id 应拒绝。"
	)
	_assert_true(
		EquipmentEntryState.from_dict(_make_equipment_entry_payload(&"bronze_sword", &"eq_schema_bad_slot", ["weapon"])) == null,
		"equipment entry 的非法 slot id 应拒绝。"
	)
	_assert_true(
		EquipmentEntryState.from_dict(_make_equipment_entry_payload(&"bronze_sword", &"eq_schema_duplicate_slot", ["main_hand", "main_hand"])) == null,
		"equipment entry 的重复 slot id 应拒绝。"
	)
	_assert_true(
		EquipmentEntryState.from_dict(_make_equipment_entry_payload(&"bronze_sword", &"eq_schema_numeric_slot", [123])) == null,
		"equipment entry 的非字符串 slot id 应拒绝。"
	)


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
	eq_state.set_equipped_entry(
		&"main_hand",
		&"bronze_sword",
		occ_main,
		EquipmentInstanceState.create(&"bronze_sword", &"eq_fixture_bronze_sword")
	)
	eq_state.set_equipped_entry(
		&"off_hand",
		&"scout_charm",
		occ_off,
		EquipmentInstanceState.create(&"scout_charm", &"eq_fixture_scout_charm")
	)

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


func _test_armor_max_dex_bonus_caps_positive_agility_ac() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var progression_registry := ProgressionContentRegistry.new()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	party_state.get_member_state(&"hero").progression.unit_base_attributes.set_attribute_value(UnitBaseAttributes.AGILITY, 18)

	var baseline_manager := CharacterManagementModule.new()
	baseline_manager.setup(
		party_state,
		progression_registry.get_skill_defs(),
		progression_registry.get_profession_defs(),
		progression_registry.get_achievement_defs(),
		item_defs
	)
	var baseline_snapshot = baseline_manager.get_member_attribute_snapshot(&"hero")
	_assert_eq(baseline_snapshot.get_value(AttributeService.ARMOR_CLASS), 12, "敏捷 18 且无护甲时 AC 应为 8 + 敏捷调整值 4。")
	_assert_eq(baseline_snapshot.get_value(AttributeService.ARMOR_MAX_DEX_BONUS), -1, "无护甲时敏捷上限应为 -1。")

	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	warehouse_service.add_item(&"leather_jerkin", 1)
	warehouse_service.add_item(&"iron_scale_mail", 1)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)

	var leather_result := equipment_service.equip_item(&"hero", &"leather_jerkin")
	_assert_true(bool(leather_result.get("success", false)), "皮革短甲应能装备到身体槽。")
	var leather_manager := CharacterManagementModule.new()
	leather_manager.setup(
		party_state,
		progression_registry.get_skill_defs(),
		progression_registry.get_profession_defs(),
		progression_registry.get_achievement_defs(),
		item_defs
	)
	var leather_snapshot = leather_manager.get_member_attribute_snapshot(&"hero")
	_assert_eq(leather_snapshot.get_value(AttributeService.ARMOR_MAX_DEX_BONUS), 6, "皮革短甲应提供敏捷上限 6。")
	_assert_eq(leather_snapshot.get_value(AttributeService.ARMOR_CLASS), 14, "皮革短甲不应限制敏捷 18 的 +4 AC。")

	var scale_result := equipment_service.equip_item(&"hero", &"iron_scale_mail")
	_assert_true(bool(scale_result.get("success", false)), "铁鳞甲应能替换身体槽护甲。")
	var scale_manager := CharacterManagementModule.new()
	scale_manager.setup(
		party_state,
		progression_registry.get_skill_defs(),
		progression_registry.get_profession_defs(),
		progression_registry.get_achievement_defs(),
		item_defs
	)
	var scale_snapshot = scale_manager.get_member_attribute_snapshot(&"hero")
	_assert_eq(scale_snapshot.get_value(AttributeService.ARMOR_MAX_DEX_BONUS), 3, "铁鳞甲应提供敏捷上限 3。")
	_assert_eq(scale_snapshot.get_value(AttributeService.ARMOR_CLASS), 15, "铁鳞甲应把敏捷 AC 从 +4 限制为 +3，再叠加护甲 +4。")


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

	warehouse_service.add_item(&"scout_charm", 1)
	var charm_instance_ids := _get_instance_ids_for_item(party_state, &"scout_charm")
	_assert_eq(charm_instance_ids.size(), 1, "前置：同种饰品应生成一个实例 ID。")
	if charm_instance_ids.size() < 1:
		return
	equipment_service.equip_item(&"hero", &"scout_charm", &"", StringName(charm_instance_ids[0]))

	var equipment_state = party_state.get_member_state(&"hero").equipment_state
	var id1: StringName = equipment_state.get_equipped_instance_id(&"necklace")
	_assert_true(id1 != &"", "项链槽应有 instance_id。")


func _test_weapon_profile_equipment_entry_round_trip() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var bronze_sword := item_defs.get(&"bronze_sword") as ItemDef
	_assert_true(bronze_sword != null, "weapon profile 装备回归前置：应能加载 bronze_sword。")
	if bronze_sword == null:
		return
	var profile := bronze_sword.get("weapon_profile") as WeaponProfileDef
	_assert_true(profile != null, "bronze_sword 应通过 weapon_profile 提供武器运行时字段。")
	_assert_eq(int(bronze_sword.get_weapon_attack_range()), 1, "weapon profile 不应影响装备前读取攻击距离。")
	_assert_eq(String(bronze_sword.get_weapon_physical_damage_tag()), "physical_pierce", "weapon profile 不应影响装备前读取伤害类型。")

	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)

	warehouse_service.add_item(&"bronze_sword", 1)
	var equip_result := equipment_service.equip_item(&"hero", &"bronze_sword")
	_assert_true(bool(equip_result.get("success", false)), "带 weapon_profile 的武器应能正常装备。")
	_assert_eq(warehouse_service.count_item(&"bronze_sword"), 0, "装备后 weapon_profile 武器不应残留在仓库。")

	var equipment_state = party_state.get_member_state(&"hero").equipment_state
	var instance_id: StringName = equipment_state.get_equipped_instance_id(&"main_hand")
	_assert_true(instance_id != &"", "带 weapon_profile 的武器装备后应拥有实例 ID。")
	var equipment_payload: Dictionary = equipment_state.to_dict()
	var slot_payload: Dictionary = equipment_payload.get("equipped_slots", {}).get("main_hand", {})
	var slot_instance_payload: Dictionary = slot_payload.get("equipment_instance", {})
	_assert_eq(String(slot_instance_payload.get("item_id", "")), "bronze_sword", "装备 payload 应通过 equipment_instance 记录 item_id。")
	_assert_eq(String(slot_instance_payload.get("instance_id", "")), String(instance_id), "装备 payload 应通过 equipment_instance 记录同一个 instance_id。")
	_assert_true(not slot_payload.has("item_id"), "装备 entry payload 不应再保留顶层 item_id 投影。")
	_assert_true(not slot_payload.has("instance_id"), "装备 entry payload 不应再保留顶层 instance_id 投影。")
	_assert_true(not slot_payload.has("weapon_profile"), "装备 entry payload 不应序列化 weapon_profile 静态资源。")
	_assert_true(not slot_payload.has("weapon_attack_range"), "装备 entry payload 不应写入旧 weapon_attack_range 字段。")
	_assert_true(not slot_payload.has("weapon_physical_damage_tag"), "装备 entry payload 不应写入旧 weapon_physical_damage_tag 字段。")

	var restored_party_state = PartyState.from_dict(party_state.to_dict())
	_assert_true(restored_party_state != null, "带 weapon_profile 武器的 PartyState round-trip 应成功。")
	if restored_party_state == null:
		return
	var restored_equipment_state = restored_party_state.get_member_state(&"hero").equipment_state
	_assert_eq(String(restored_equipment_state.get_equipped_item_id(&"main_hand")), "bronze_sword", "round-trip 后应保留 weapon_profile 武器 item_id。")
	_assert_eq(String(restored_equipment_state.get_equipped_instance_id(&"main_hand")), String(instance_id), "round-trip 后应保留 weapon_profile 武器 instance_id。")

	var restored_warehouse := PartyWarehouseService.new()
	restored_warehouse.setup(restored_party_state, item_defs)
	var restored_equipment_service := PartyEquipmentService.new()
	restored_equipment_service.setup(restored_party_state, item_defs, restored_warehouse)
	var unequip_result := restored_equipment_service.unequip_item(&"hero", &"main_hand")
	_assert_true(bool(unequip_result.get("success", false)), "round-trip 后带 weapon_profile 的武器应能卸回仓库。")
	_assert_eq(restored_warehouse.count_item(&"bronze_sword"), 1, "卸装后带 weapon_profile 的武器应作为装备实例回仓。")

	var restored_instances: Array = restored_party_state.warehouse_state.get_non_empty_instances()
	_assert_eq(restored_instances.size(), 1, "卸装后仓库中应只有 1 件 weapon_profile 武器实例。")
	if not restored_instances.is_empty():
		var instance_payload: Dictionary = restored_instances[0].to_dict()
		_assert_eq(String(instance_payload.get("instance_id", "")), String(instance_id), "卸回仓库的实例应保留原 instance_id。")
		_assert_true(not instance_payload.has("weapon_profile"), "装备实例 payload 不应序列化 weapon_profile 静态资源。")
		_assert_true(not instance_payload.has("weapon_attack_range"), "装备实例 payload 不应写入旧 weapon_attack_range 字段。")
		_assert_true(not instance_payload.has("weapon_physical_damage_tag"), "装备实例 payload 不应写入旧 weapon_physical_damage_tag 字段。")


func _test_equipped_instance_fields_survive_round_trip_and_unequip() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)

	var epic_instance := EquipmentInstanceState.create(&"bronze_sword", &"eq_epic_equipped_bronze_sword")
	epic_instance.rarity = EquipmentInstanceState.RarityTier.EPIC
	epic_instance.current_durability = 17
	party_state.warehouse_state.equipment_instances = [epic_instance]

	var equip_result := equipment_service.equip_item(&"hero", &"bronze_sword")
	_assert_true(bool(equip_result.get("success", false)), "带实例字段的装备应能从仓库装备。")
	var equipment_state = party_state.get_member_state(&"hero").equipment_state
	var equipped_instance = equipment_state.get_equipped_instance(&"main_hand")
	_assert_equipment_instance_fields(
		equipped_instance,
		"eq_epic_equipped_bronze_sword",
		EquipmentInstanceState.RarityTier.EPIC,
		17,
		"装备位应持有完整装备实例字段。"
	)

	var restored_party_state = PartyState.from_dict(party_state.to_dict())
	_assert_true(restored_party_state != null, "完整装备实例字段应能穿过 PartyState round-trip。")
	if restored_party_state == null:
		return
	var restored_warehouse_service := PartyWarehouseService.new()
	restored_warehouse_service.setup(restored_party_state, item_defs)
	var restored_equipment_service := PartyEquipmentService.new()
	restored_equipment_service.setup(restored_party_state, item_defs, restored_warehouse_service)
	var restored_equipment_state = restored_party_state.get_member_state(&"hero").equipment_state
	_assert_equipment_instance_fields(
		restored_equipment_state.get_equipped_instance(&"main_hand"),
		"eq_epic_equipped_bronze_sword",
		EquipmentInstanceState.RarityTier.EPIC,
		17,
		"round-trip 后装备位应保留完整装备实例字段。"
	)

	var unequip_result := restored_equipment_service.unequip_item(&"hero", &"main_hand")
	_assert_true(bool(unequip_result.get("success", false)), "完整实例装备应能卸回仓库。")
	var restored_instances: Array = restored_party_state.warehouse_state.get_non_empty_instances()
	_assert_eq(restored_instances.size(), 1, "卸装后完整实例应回到仓库。")
	if not restored_instances.is_empty():
		_assert_equipment_instance_fields(
			restored_instances[0],
			"eq_epic_equipped_bronze_sword",
			EquipmentInstanceState.RarityTier.EPIC,
			17,
			"卸装回仓后应保留完整装备实例字段。"
		)


func _test_equipment_instance_rarity_round_trip_and_strict_schema() -> void:
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var epic_instance := EquipmentInstanceState.create(&"bronze_sword", &"eq_epic_bronze_sword")
	epic_instance.rarity = EquipmentInstanceState.RarityTier.EPIC
	epic_instance.current_durability = EquipmentDurabilityRules.get_default_current_durability(epic_instance.rarity)
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

	var missing_rarity_error := EquipmentInstanceState.get_payload_validation_error({
		"instance_id": "eq_missing_rarity_bronze_sword",
		"item_id": "bronze_sword",
		"current_durability": EquipmentDurabilityRules.get_default_current_durability(EquipmentInstanceState.RarityTier.COMMON),
	})
	_assert_true(
		missing_rarity_error.contains("missing required field 'rarity'"),
		"缺少 rarity 的装备实例 payload 应暴露字段级存档损坏诊断。 error=%s" % missing_rarity_error
	)

	var invalid_rarity_error := EquipmentInstanceState.get_payload_validation_error({
		"instance_id": "eq_invalid_rarity_bronze_sword",
		"item_id": "bronze_sword",
		"rarity": 999,
		"current_durability": EquipmentDurabilityRules.get_default_current_durability(EquipmentInstanceState.RarityTier.COMMON),
	})
	_assert_true(
		invalid_rarity_error.contains("invalid rarity 999"),
		"非法 rarity 的装备实例 payload 应暴露字段级存档损坏诊断。 error=%s" % invalid_rarity_error
	)

	var invalid_instance_id_payload := _make_equipment_instance_payload("eq_schema_invalid_instance_id")
	invalid_instance_id_payload["instance_id"] = 17
	_assert_equipment_instance_validation_error(
		invalid_instance_id_payload,
		"instance_id must be String or StringName",
		"装备实例 instance_id 不应从数字兼容转换。"
	)

	var invalid_item_id_payload := _make_equipment_instance_payload("eq_schema_invalid_item_id")
	invalid_item_id_payload["item_id"] = 17
	_assert_equipment_instance_validation_error(
		invalid_item_id_payload,
		"item_id must be String or StringName",
		"装备实例 item_id 不应从数字兼容转换。"
	)

	var string_rarity_payload := _make_equipment_instance_payload("eq_schema_string_rarity")
	string_rarity_payload["rarity"] = "3"
	_assert_equipment_instance_validation_error(
		string_rarity_payload,
		"rarity must be int",
		"装备实例 rarity 不应从字符串兼容转换。"
	)

	var string_durability_payload := _make_equipment_instance_payload("eq_schema_string_durability")
	string_durability_payload["current_durability"] = "17"
	_assert_equipment_instance_validation_error(
		string_durability_payload,
		"current_durability must be int",
		"装备实例 current_durability 不应从字符串兼容转换。"
	)

	var zero_durability_payload := _make_equipment_instance_payload("eq_schema_zero_durability")
	zero_durability_payload["current_durability"] = 0
	_assert_equipment_instance_validation_error(
		zero_durability_payload,
		"invalid current_durability 0",
		"装备实例 current_durability 不允许 0 或负数。"
	)

func _test_duplicate_same_item_instance_id_selection() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var party_state := _build_party_with_member(&"hero", "Hero", 8)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	var equipment_service := PartyEquipmentService.new()
	equipment_service.setup(party_state, item_defs, warehouse_service)

	var common_instance := EquipmentInstanceState.create(&"bronze_sword", &"eq_duplicate_common_sword")
	common_instance.rarity = EquipmentInstanceState.RarityTier.COMMON
	common_instance.current_durability = 11
	var rare_instance := EquipmentInstanceState.create(&"bronze_sword", &"eq_duplicate_rare_sword")
	rare_instance.rarity = EquipmentInstanceState.RarityTier.RARE
	rare_instance.current_durability = 23
	var mismatch_instance := EquipmentInstanceState.create(&"scout_charm", &"eq_duplicate_wrong_item")
	party_state.warehouse_state.equipment_instances = [common_instance, rare_instance, mismatch_instance]

	var item_only_result := equipment_service.equip_item(&"hero", &"bronze_sword")
	_assert_true(not bool(item_only_result.get("success", true)), "同 item_id 多装备实例时 item_id-only 装备应被拒绝。")
	_assert_eq(String(item_only_result.get("error_code", "")), "equipment_instance_id_required", "同 item_id 多实例应要求 instance_id。")

	var mismatch_result := equipment_service.equip_item(&"hero", &"bronze_sword", &"", &"eq_duplicate_wrong_item")
	_assert_true(not bool(mismatch_result.get("success", true)), "指定不属于该 item_id 的 instance_id 应被拒绝。")
	_assert_eq(String(mismatch_result.get("error_code", "")), "equipment_instance_item_mismatch", "错 item 的 instance_id 应返回 mismatch。")

	var missing_result := equipment_service.equip_item(&"hero", &"bronze_sword", &"", &"eq_duplicate_missing")
	_assert_true(not bool(missing_result.get("success", true)), "不存在的 instance_id 应被拒绝。")
	_assert_eq(String(missing_result.get("error_code", "")), "warehouse_missing_instance", "不存在的 instance_id 应返回 missing_instance。")

	var equip_result := equipment_service.equip_item(&"hero", &"bronze_sword", &"", &"eq_duplicate_rare_sword")
	_assert_true(bool(equip_result.get("success", false)), "指定 rare instance_id 的装备应成功。")
	var equipped_instance = party_state.get_member_state(&"hero").equipment_state.get_equipped_instance(&"main_hand")
	_assert_equipment_instance_fields(
		equipped_instance,
		"eq_duplicate_rare_sword",
		EquipmentInstanceState.RarityTier.RARE,
		23,
		"指定 instance_id 装备后"
	)
	_assert_true(
		warehouse_service.has_equipment_instance(&"eq_duplicate_common_sword", &"bronze_sword"),
		"未指定的同 item_id 装备实例应保留在仓库。"
	)
	_assert_true(
		not warehouse_service.has_equipment_instance(&"eq_duplicate_rare_sword", &"bronze_sword"),
		"已装备的指定实例应离开仓库。"
	)

	var restored_party_state = PartyState.from_dict(party_state.to_dict())
	_assert_true(restored_party_state != null, "指定 duplicate instance_id 装备后的 PartyState round-trip 应成功。")
	if restored_party_state == null:
		return
	var restored_warehouse_service := PartyWarehouseService.new()
	restored_warehouse_service.setup(restored_party_state, item_defs)
	var restored_equipment_service := PartyEquipmentService.new()
	restored_equipment_service.setup(restored_party_state, item_defs, restored_warehouse_service)
	var restored_equipped_instance = restored_party_state.get_member_state(&"hero").equipment_state.get_equipped_instance(&"main_hand")
	_assert_equipment_instance_fields(
		restored_equipped_instance,
		"eq_duplicate_rare_sword",
		EquipmentInstanceState.RarityTier.RARE,
		23,
		"指定 instance_id 装备 round-trip 后"
	)
	var unequip_result := restored_equipment_service.unequip_item(&"hero", &"main_hand")
	_assert_true(bool(unequip_result.get("success", false)), "指定 duplicate instance_id 装备 round-trip 后应可卸回仓库。")
	_assert_true(restored_warehouse_service.has_equipment_instance(&"eq_duplicate_common_sword", &"bronze_sword"), "round-trip 卸装后 common 实例仍应在仓库。")
	_assert_true(restored_warehouse_service.has_equipment_instance(&"eq_duplicate_rare_sword", &"bronze_sword"), "round-trip 卸装后 rare 实例应回到仓库。")
	var returned_instance = restored_warehouse_service.get_equipment_instance_by_id(&"eq_duplicate_rare_sword", &"bronze_sword")
	_assert_equipment_instance_fields(
		returned_instance,
		"eq_duplicate_rare_sword",
		EquipmentInstanceState.RarityTier.RARE,
		23,
		"指定 duplicate instance_id 卸回仓库后"
	)


func _test_party_state_rejects_duplicate_equipment_instance_ids() -> void:
	var warehouse_duplicate_party := _build_party_with_member(&"hero", "Hero", 8)
	warehouse_duplicate_party.warehouse_state.equipment_instances = [
		EquipmentInstanceState.create(&"bronze_sword", &"eq_party_duplicate"),
		EquipmentInstanceState.create(&"scout_charm", &"eq_party_duplicate"),
	]
	_assert_true(
		PartyState.from_dict(warehouse_duplicate_party.to_dict()) == null,
		"同一个 instance_id 在仓库中出现两次时，PartyState.from_dict() 应拒绝整份 payload。"
	)

	var warehouse_and_equipped_party := _build_party_with_member(&"hero", "Hero", 8)
	warehouse_and_equipped_party.warehouse_state.equipment_instances = [
		EquipmentInstanceState.create(&"bronze_sword", &"eq_party_shared"),
	]
	var hero_equipment = warehouse_and_equipped_party.get_member_state(&"hero").equipment_state
	hero_equipment.set_equipped_entry(
		&"main_hand",
		&"bronze_sword",
		Array([&"main_hand"], TYPE_STRING_NAME, &"", null),
		EquipmentInstanceState.create(&"bronze_sword", &"eq_party_shared")
	)
	_assert_true(
		PartyState.from_dict(warehouse_and_equipped_party.to_dict()) == null,
		"同一个 instance_id 同时存在于仓库和装备位时，PartyState.from_dict() 应拒绝整份 payload。"
	)

	var same_member_duplicate_party := _build_party_with_member(&"hero", "Hero", 8)
	var same_member_equipment = same_member_duplicate_party.get_member_state(&"hero").equipment_state
	same_member_equipment.set_equipped_entry(
		&"main_hand",
		&"bronze_sword",
		Array([&"main_hand"], TYPE_STRING_NAME, &"", null),
		EquipmentInstanceState.create(&"bronze_sword", &"eq_party_same_member")
	)
	same_member_equipment.set_equipped_entry(
		&"necklace",
		&"scout_charm",
		Array([&"necklace"], TYPE_STRING_NAME, &"", null),
		EquipmentInstanceState.create(&"scout_charm", &"eq_party_same_member")
	)
	_assert_true(
		PartyState.from_dict(same_member_duplicate_party.to_dict()) == null,
		"同一个 member 的多个装备 entry 复用 instance_id 时，PartyState.from_dict() 应拒绝整份 payload。"
	)

	var cross_member_duplicate_party := _build_party_with_member(&"hero", "Hero", 8)
	var ally := PartyMemberState.new()
	ally.member_id = &"ally"
	ally.display_name = "Ally"
	ally.progression = UnitProgress.new()
	ally.progression.unit_id = ally.member_id
	ally.progression.display_name = ally.display_name
	cross_member_duplicate_party.set_member_state(ally)
	cross_member_duplicate_party.reserve_member_ids = [&"ally"]
	cross_member_duplicate_party.get_member_state(&"hero").equipment_state.set_equipped_entry(
		&"main_hand",
		&"bronze_sword",
		Array([&"main_hand"], TYPE_STRING_NAME, &"", null),
		EquipmentInstanceState.create(&"bronze_sword", &"eq_party_cross_member")
	)
	cross_member_duplicate_party.get_member_state(&"ally").equipment_state.set_equipped_entry(
		&"necklace",
		&"scout_charm",
		Array([&"necklace"], TYPE_STRING_NAME, &"", null),
		EquipmentInstanceState.create(&"scout_charm", &"eq_party_cross_member")
	)
	_assert_true(
		PartyState.from_dict(cross_member_duplicate_party.to_dict()) == null,
		"不同 members 的装备位复用 instance_id 时，PartyState.from_dict() 应拒绝整份 payload。"
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


func _make_equipment_instance_payload(instance_id: String, item_id: String = "bronze_sword") -> Dictionary:
	return {
		"instance_id": instance_id,
		"item_id": item_id,
		"rarity": EquipmentInstanceState.RarityTier.COMMON,
		"current_durability": EquipmentDurabilityRules.get_default_current_durability(EquipmentInstanceState.RarityTier.COMMON),
	}


func _make_equipment_entry_payload(item_id: StringName, instance_id: StringName, occupied_slot_ids: Array) -> Dictionary:
	var instance := EquipmentInstanceState.create(item_id, instance_id)
	return {
		"occupied_slot_ids": occupied_slot_ids,
		"equipment_instance": instance.to_dict(),
	}


func _dice_to_list(dice_resource) -> Array:
	if dice_resource == null:
		return []
	return [
		int(dice_resource.get("dice_count")),
		int(dice_resource.get("dice_sides")),
		int(dice_resource.get("flat_bonus")),
	]


func _get_instance_ids_for_item(party_state: PartyState, item_id: StringName) -> Array[String]:
	var result: Array[String] = []
	if party_state == null or party_state.warehouse_state == null:
		return result
	for instance in party_state.warehouse_state.get_non_empty_instances():
		if instance == null:
			continue
		if instance.item_id == item_id:
			result.append(String(instance.instance_id))
	result.sort()
	return result


func _assert_equipment_instance_fields(
	instance,
	expected_instance_id: String,
	expected_rarity: int,
	expected_current_durability: int,
	message_prefix: String
) -> void:
	_assert_true(instance != null, "%s 实例不应为空。" % message_prefix)
	if instance == null:
		return
	_assert_eq(String(instance.instance_id), expected_instance_id, "%s instance_id 应保持。" % message_prefix)
	_assert_eq(int(instance.rarity), expected_rarity, "%s rarity 应保持。" % message_prefix)
	_assert_eq(int(instance.current_durability), expected_current_durability, "%s current_durability 应保持。" % message_prefix)


func _assert_equipment_instance_validation_error(payload: Dictionary, expected_fragment: String, message: String) -> void:
	var validation_error := EquipmentInstanceState.get_payload_validation_error(payload)
	_assert_true(
		validation_error.contains(expected_fragment),
		"%s error=%s" % [message, validation_error]
	)


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _finish() -> void:
	if _failures.is_empty():
		print("Party equipment regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Party equipment regression: FAIL (%d)" % _failures.size())
	quit(1)
