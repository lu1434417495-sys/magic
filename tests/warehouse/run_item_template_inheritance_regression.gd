extends SceneTree

const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const AttributeModifier = preload("res://scripts/player/progression/attribute_modifier.gd")
const WeaponProfileDef = preload("res://scripts/player/warehouse/weapon_profile_def.gd")
const WeaponDamageDiceDef = preload("res://scripts/player/warehouse/weapon_damage_dice_def.gd")

const LEGACY_WEAPON_FIELDS_FIXTURE := "res://tests/fixtures/resource_validation/item_registry_invalid/legacy_weapon_fields_item.tres"

const WEAPON_INSTANCE_EXPECTATIONS := {
	&"bronze_sword": {
		"base_item_id": &"weapon_type_shortsword_base",
		"tags": [&"weapon", &"melee", &"one_handed", &"shortsword", &"sword", &"weapon_class_sword", &"weapon_type_shortsword"],
		"weapon_type_id": &"shortsword",
		"training_group": &"martial",
		"range_type": &"melee",
		"family": &"sword",
		"damage_tag": &"physical_pierce",
		"equipment_slot_ids": ["main_hand"],
		"occupied_slot_ids": [],
		"attack_range": 1,
		"one_handed_dice": [1, 6, 0],
		"two_handed_dice": [],
		"properties": [&"finesse", &"light"],
		"modifier_count": 2,
		"base_price": 120,
		"buy_price": 120,
		"sell_price": 60,
	},
	&"iron_greatsword": {
		"base_item_id": &"weapon_type_greatsword_base",
		"tags": [&"weapon", &"melee", &"two_handed", &"greatsword", &"sword", &"weapon_class_sword", &"weapon_type_greatsword"],
		"weapon_type_id": &"greatsword",
		"training_group": &"martial",
		"range_type": &"melee",
		"family": &"sword",
		"damage_tag": &"physical_slash",
		"equipment_slot_ids": ["main_hand"],
		"occupied_slot_ids": ["main_hand", "off_hand"],
		"attack_range": 1,
		"one_handed_dice": [],
		"two_handed_dice": [2, 6, 0],
		"properties": [&"two_handed"],
		"modifier_count": 1,
		"base_price": 240,
		"buy_price": 240,
		"sell_price": 120,
	},
	&"militia_axe": {
		"base_item_id": &"weapon_type_handaxe_base",
		"tags": [&"weapon", &"melee", &"one_handed", &"handaxe", &"axe", &"weapon_class_axe", &"weapon_type_handaxe"],
		"weapon_type_id": &"handaxe",
		"training_group": &"simple",
		"range_type": &"melee",
		"family": &"axe",
		"damage_tag": &"physical_slash",
		"equipment_slot_ids": ["main_hand"],
		"occupied_slot_ids": [],
		"attack_range": 1,
		"one_handed_dice": [1, 6, 0],
		"two_handed_dice": [],
		"properties": [&"light", &"thrown"],
		"modifier_count": 2,
		"base_price": 135,
		"buy_price": 145,
		"sell_price": 70,
	},
	&"watchman_mace": {
		"base_item_id": &"weapon_type_mace_base",
		"tags": [&"weapon", &"melee", &"one_handed", &"mace", &"weapon_class_mace", &"weapon_type_mace"],
		"weapon_type_id": &"mace",
		"training_group": &"simple",
		"range_type": &"melee",
		"family": &"mace",
		"damage_tag": &"physical_blunt",
		"equipment_slot_ids": ["main_hand"],
		"occupied_slot_ids": [],
		"attack_range": 1,
		"one_handed_dice": [1, 6, 0],
		"two_handed_dice": [],
		"properties": [],
		"modifier_count": 2,
		"base_price": 165,
		"buy_price": 175,
		"sell_price": 85,
	},
}

const TEMPLATE_IDS := [
	&"weapon_type_shortsword_base",
	&"weapon_type_greatsword_base",
	&"weapon_type_handaxe_base",
	&"weapon_type_mace_base",
]

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

const BG3_WEAPON_PROFILE_EXPECTATIONS := {
	&"club": {"training_group": &"simple", "range_type": &"melee", "family": &"club", "damage_tag": &"physical_blunt", "attack_range": 1, "one_handed_dice": [1, 4, 0], "two_handed_dice": [], "properties": [&"light"]},
	&"dagger": {"training_group": &"simple", "range_type": &"melee", "family": &"dagger", "damage_tag": &"physical_pierce", "attack_range": 1, "one_handed_dice": [1, 4, 0], "two_handed_dice": [], "properties": [&"finesse", &"light", &"thrown"]},
	&"handaxe": {"training_group": &"simple", "range_type": &"melee", "family": &"axe", "damage_tag": &"physical_slash", "attack_range": 1, "one_handed_dice": [1, 6, 0], "two_handed_dice": [], "properties": [&"light", &"thrown"]},
	&"javelin": {"training_group": &"simple", "range_type": &"melee", "family": &"spear", "damage_tag": &"physical_pierce", "attack_range": 1, "one_handed_dice": [1, 6, 0], "two_handed_dice": [], "properties": [&"thrown"]},
	&"light_hammer": {"training_group": &"simple", "range_type": &"melee", "family": &"hammer", "damage_tag": &"physical_blunt", "attack_range": 1, "one_handed_dice": [1, 4, 0], "two_handed_dice": [], "properties": [&"light", &"thrown"]},
	&"mace": {"training_group": &"simple", "range_type": &"melee", "family": &"mace", "damage_tag": &"physical_blunt", "attack_range": 1, "one_handed_dice": [1, 6, 0], "two_handed_dice": [], "properties": []},
	&"sickle": {"training_group": &"simple", "range_type": &"melee", "family": &"sickle", "damage_tag": &"physical_slash", "attack_range": 1, "one_handed_dice": [1, 4, 0], "two_handed_dice": [], "properties": [&"light"]},
	&"quarterstaff": {"training_group": &"simple", "range_type": &"melee", "family": &"staff", "damage_tag": &"physical_blunt", "attack_range": 1, "one_handed_dice": [1, 6, 0], "two_handed_dice": [1, 8, 0], "properties": [&"versatile"]},
	&"spear": {"training_group": &"simple", "range_type": &"melee", "family": &"spear", "damage_tag": &"physical_pierce", "attack_range": 1, "one_handed_dice": [1, 6, 0], "two_handed_dice": [1, 8, 0], "properties": [&"versatile", &"thrown"]},
	&"greatclub": {"training_group": &"simple", "range_type": &"melee", "family": &"club", "damage_tag": &"physical_blunt", "attack_range": 1, "one_handed_dice": [], "two_handed_dice": [1, 8, 0], "properties": [&"two_handed"]},
	&"light_crossbow": {"training_group": &"simple", "range_type": &"ranged", "family": &"crossbow", "damage_tag": &"physical_pierce", "attack_range": 3, "one_handed_dice": [], "two_handed_dice": [1, 8, 0], "properties": [&"two_handed"]},
	&"shortbow": {"training_group": &"simple", "range_type": &"ranged", "family": &"bow", "damage_tag": &"physical_pierce", "attack_range": 3, "one_handed_dice": [], "two_handed_dice": [1, 6, 0], "properties": [&"two_handed"]},
	&"flail": {"training_group": &"martial", "range_type": &"melee", "family": &"mace", "damage_tag": &"physical_blunt", "attack_range": 1, "one_handed_dice": [1, 8, 0], "two_handed_dice": [], "properties": []},
	&"morningstar": {"training_group": &"martial", "range_type": &"melee", "family": &"mace", "damage_tag": &"physical_pierce", "attack_range": 1, "one_handed_dice": [1, 8, 0], "two_handed_dice": [], "properties": []},
	&"rapier": {"training_group": &"martial", "range_type": &"melee", "family": &"sword", "damage_tag": &"physical_pierce", "attack_range": 1, "one_handed_dice": [1, 8, 0], "two_handed_dice": [], "properties": [&"finesse"]},
	&"scimitar": {"training_group": &"martial", "range_type": &"melee", "family": &"sword", "damage_tag": &"physical_slash", "attack_range": 1, "one_handed_dice": [1, 6, 0], "two_handed_dice": [], "properties": [&"finesse", &"light"]},
	&"shortsword": {"training_group": &"martial", "range_type": &"melee", "family": &"sword", "damage_tag": &"physical_pierce", "attack_range": 1, "one_handed_dice": [1, 6, 0], "two_handed_dice": [], "properties": [&"finesse", &"light"]},
	&"war_pick": {"training_group": &"martial", "range_type": &"melee", "family": &"pick", "damage_tag": &"physical_pierce", "attack_range": 1, "one_handed_dice": [1, 8, 0], "two_handed_dice": [], "properties": []},
	&"battleaxe": {"training_group": &"martial", "range_type": &"melee", "family": &"axe", "damage_tag": &"physical_slash", "attack_range": 1, "one_handed_dice": [1, 8, 0], "two_handed_dice": [1, 10, 0], "properties": [&"versatile"]},
	&"longsword": {"training_group": &"martial", "range_type": &"melee", "family": &"sword", "damage_tag": &"physical_slash", "attack_range": 1, "one_handed_dice": [1, 8, 0], "two_handed_dice": [1, 10, 0], "properties": [&"versatile"]},
	&"trident": {"training_group": &"martial", "range_type": &"melee", "family": &"spear", "damage_tag": &"physical_pierce", "attack_range": 1, "one_handed_dice": [1, 6, 0], "two_handed_dice": [1, 8, 0], "properties": [&"versatile", &"thrown"]},
	&"warhammer": {"training_group": &"martial", "range_type": &"melee", "family": &"hammer", "damage_tag": &"physical_blunt", "attack_range": 1, "one_handed_dice": [1, 8, 0], "two_handed_dice": [1, 10, 0], "properties": [&"versatile"]},
	&"glaive": {"training_group": &"martial", "range_type": &"melee", "family": &"polearm", "damage_tag": &"physical_slash", "attack_range": 2, "one_handed_dice": [], "two_handed_dice": [1, 10, 0], "properties": [&"two_handed", &"reach"]},
	&"greataxe": {"training_group": &"martial", "range_type": &"melee", "family": &"axe", "damage_tag": &"physical_slash", "attack_range": 1, "one_handed_dice": [], "two_handed_dice": [1, 12, 0], "properties": [&"two_handed"]},
	&"greatsword": {"training_group": &"martial", "range_type": &"melee", "family": &"sword", "damage_tag": &"physical_slash", "attack_range": 1, "one_handed_dice": [], "two_handed_dice": [2, 6, 0], "properties": [&"two_handed"]},
	&"halberd": {"training_group": &"martial", "range_type": &"melee", "family": &"polearm", "damage_tag": &"physical_slash", "attack_range": 2, "one_handed_dice": [], "two_handed_dice": [1, 10, 0], "properties": [&"two_handed", &"reach"]},
	&"maul": {"training_group": &"martial", "range_type": &"melee", "family": &"hammer", "damage_tag": &"physical_blunt", "attack_range": 1, "one_handed_dice": [], "two_handed_dice": [2, 6, 0], "properties": [&"two_handed"]},
	&"pike": {"training_group": &"martial", "range_type": &"melee", "family": &"polearm", "damage_tag": &"physical_pierce", "attack_range": 2, "one_handed_dice": [], "two_handed_dice": [1, 10, 0], "properties": [&"two_handed", &"reach"]},
	&"hand_crossbow": {"training_group": &"martial", "range_type": &"ranged", "family": &"crossbow", "damage_tag": &"physical_pierce", "attack_range": 2, "one_handed_dice": [1, 6, 0], "two_handed_dice": [], "properties": [&"light"]},
	&"heavy_crossbow": {"training_group": &"martial", "range_type": &"ranged", "family": &"crossbow", "damage_tag": &"physical_pierce", "attack_range": 4, "one_handed_dice": [], "two_handed_dice": [1, 10, 0], "properties": [&"two_handed"]},
	&"longbow": {"training_group": &"martial", "range_type": &"ranged", "family": &"bow", "damage_tag": &"physical_pierce", "attack_range": 4, "one_handed_dice": [], "two_handed_dice": [1, 8, 0], "properties": [&"two_handed"]},
}

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_real_registry_no_validation_errors()
	_test_weapon_instances_resolve_against_templates()
	_test_all_bg3_weapon_types_have_seed_items()
	_test_attribute_modifier_source_id_rewritten()
	_test_templates_excluded_from_item_defs()
	_test_standalone_item_without_template_unchanged()
	_test_item_def_exposes_only_weapon_profile_runtime_source()
	_test_legacy_weapon_fields_are_not_runtime_fallback()
	_test_scalar_fallback()
	_test_weapon_profile_merge_delegates_property_rules()
	_test_weapon_profile_inheritance_override_and_property_modes()
	_test_item_category_inherits_from_template()
	_test_item_category_normalized_helper()
	_test_string_name_array_merge_dedup_order()
	_test_equipment_slot_ids_override_not_merge()
	_test_modifier_deep_copy_and_source_id_rewrite()
	_test_cycle_detection()
	_test_missing_template_detection()
	_test_template_id_collision_with_instance()

	if _failures.is_empty():
		print("Item template inheritance regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Item template inheritance regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_real_registry_no_validation_errors() -> void:
	var registry := ItemContentRegistry.new()
	var errors := registry.validate()
	_assert_true(errors.is_empty(), "ItemContentRegistry 在加入模板继承后不应产生新校验错误。错误：%s" % str(errors))


func _test_weapon_instances_resolve_against_templates() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	for item_id in WEAPON_INSTANCE_EXPECTATIONS.keys():
		var expectation: Dictionary = WEAPON_INSTANCE_EXPECTATIONS[item_id]
		var item_def: ItemDef = item_defs.get(item_id) as ItemDef
		_assert_true(item_def != null, "应能加载武器实例 %s。" % String(item_id))
		if item_def == null:
			continue
		_assert_eq(item_def.base_item_id, &"", "合并后 %s 的 base_item_id 应被清空。" % String(item_id))
		_assert_eq(item_def.get_tags(), expectation["tags"], "%s 合并后 tags 应与原始一致。" % String(item_id))
		_assert_eq(String(item_def.get_weapon_physical_damage_tag()), String(expectation["damage_tag"]), "%s 合并后 damage_tag 应来自 weapon_profile 模板。" % String(item_id))
		_assert_eq(int(item_def.get_weapon_attack_range()), int(expectation["attack_range"]), "%s 合并后 attack_range 应来自 weapon_profile 模板。" % String(item_id))
		_assert_true(not _modifiers_include_attribute(item_def.get_attribute_modifiers(), &"weapon_attack_range"), "%s 的 weapon_profile.attack_range 不应注入 attribute modifiers。" % String(item_id))
		var weapon_profile := item_def.get("weapon_profile") as WeaponProfileDef
		_assert_true(weapon_profile != null, "%s 合并后应保留 WeaponProfileDef。" % String(item_id))
		if weapon_profile != null:
			_assert_eq(String(weapon_profile.weapon_type_id), String(expectation["weapon_type_id"]), "%s 应声明 BG3 weapon_type_id。" % String(item_id))
			_assert_eq(String(weapon_profile.training_group), String(expectation["training_group"]), "%s 应声明 BG3 training_group。" % String(item_id))
			_assert_eq(String(weapon_profile.range_type), String(expectation["range_type"]), "%s 应声明 BG3 range_type。" % String(item_id))
			_assert_eq(String(weapon_profile.family), String(expectation["family"]), "%s 应声明 BG3 family。" % String(item_id))
			_assert_eq(_dice_to_list(weapon_profile.one_handed_dice), expectation["one_handed_dice"], "%s one_handed_dice 应符合 BG3 模板。" % String(item_id))
			_assert_eq(_dice_to_list(weapon_profile.two_handed_dice), expectation["two_handed_dice"], "%s two_handed_dice 应符合 BG3 模板。" % String(item_id))
			_assert_eq(weapon_profile.get_properties(), expectation["properties"], "%s weapon properties 应符合 BG3 模板。" % String(item_id))
		_assert_eq(String(item_def.equipment_type_id), "weapon", "%s 合并后 equipment_type_id 应为 weapon。" % String(item_id))
		_assert_eq(String(item_def.item_category), "equipment", "%s 合并后 item_category 应为 equipment。" % String(item_id))
		_assert_eq(item_def.icon, "res://icon.svg", "%s 合并后 icon 应来自模板。" % String(item_id))
		_assert_eq(_array_to_string_list(item_def.equipment_slot_ids), expectation["equipment_slot_ids"], "%s 合并后 equipment_slot_ids 应来自模板入口槽。" % String(item_id))
		_assert_eq(_array_to_string_list(item_def.occupied_slot_ids), expectation["occupied_slot_ids"], "%s 合并后 occupied_slot_ids 应符合模板设定。" % String(item_id))
		_assert_eq(int(item_def.attribute_modifiers.size()), int(expectation["modifier_count"]), "%s 合并后 modifier 数量应与原始一致。" % String(item_id))
		_assert_eq(int(item_def.base_price), int(expectation["base_price"]), "%s 合并后 base_price 应保留实例值。" % String(item_id))
		_assert_eq(int(item_def.buy_price), int(expectation["buy_price"]), "%s 合并后 buy_price 应保留实例值。" % String(item_id))
		_assert_eq(int(item_def.sell_price), int(expectation["sell_price"]), "%s 合并后 sell_price 应保留实例值。" % String(item_id))
		_assert_true(item_def.is_equipment(), "%s 合并后应通过 is_equipment() 判定。" % String(item_id))
		_assert_true(item_def.is_weapon(), "%s 合并后应通过 is_weapon() 判定。" % String(item_id))


func _test_all_bg3_weapon_types_have_seed_items() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	_assert_eq(
		BG3_WEAPON_PROFILE_EXPECTATIONS.size(),
		31,
		"BG3 基础武器类型回归应覆盖 docs/design/weapon_types_damage.md 的 31 类。"
	)
	for weapon_type_id in BG3_WEAPON_PROFILE_EXPECTATIONS.keys():
		var item_id := ProgressionDataUtils.to_string_name(BG3_WEAPON_SEED_ITEMS.get(weapon_type_id, &""))
		_assert_true(item_id != &"", "BG3 weapon_type %s 应声明正式 seed item。" % String(weapon_type_id))
		if item_id == &"":
			continue
		var item_def: ItemDef = item_defs.get(item_id) as ItemDef
		_assert_true(item_def != null, "BG3 weapon_type %s 应能通过 seed item %s 加载。" % [String(weapon_type_id), String(item_id)])
		if item_def == null:
			continue
		_assert_true(item_def.is_equipment(), "%s 应注册为装备。" % String(item_id))
		_assert_true(item_def.is_weapon(), "%s 应注册为武器装备。" % String(item_id))
		_assert_true(not item_def.is_stackable, "%s 作为武器实例应不可堆叠。" % String(item_id))
		_assert_eq(item_def.get_effective_max_stack(), 1, "%s 作为武器实例应只允许单件实例。" % String(item_id))
		_assert_true(
			item_def.get_tags().has(StringName("weapon_type_%s" % String(weapon_type_id))),
			"%s tags 应包含 weapon_type_%s。" % [String(item_id), String(weapon_type_id)]
		)
		var expectation: Dictionary = BG3_WEAPON_PROFILE_EXPECTATIONS[weapon_type_id]
		var profile := item_def.get("weapon_profile") as WeaponProfileDef
		_assert_true(profile != null, "%s 应合并出 WeaponProfileDef。" % String(item_id))
		if profile == null:
			continue
		_assert_eq(String(profile.weapon_type_id), String(weapon_type_id), "%s 应映射到指定 BG3 weapon_type_id。" % String(item_id))
		_assert_eq(String(profile.training_group), String(expectation["training_group"]), "%s training_group 应符合 BG3 分类。" % String(item_id))
		_assert_eq(String(profile.range_type), String(expectation["range_type"]), "%s range_type 应符合 BG3 分类。" % String(item_id))
		_assert_eq(String(profile.family), String(expectation["family"]), "%s family 应符合战斗技能 family 口径。" % String(item_id))
		_assert_eq(String(profile.damage_tag), String(expectation["damage_tag"]), "%s damage_tag 应符合基础伤害类型。" % String(item_id))
		_assert_eq(int(profile.attack_range), int(expectation["attack_range"]), "%s attack_range 应符合当前战棋投影。" % String(item_id))
		_assert_eq(_dice_to_list(profile.one_handed_dice), expectation["one_handed_dice"], "%s one_handed_dice 应符合 BG3 基础骰。" % String(item_id))
		_assert_eq(_dice_to_list(profile.two_handed_dice), expectation["two_handed_dice"], "%s two_handed_dice 应符合 BG3 基础骰。" % String(item_id))
		_assert_eq(profile.get_properties(), expectation["properties"], "%s properties 应符合 BG3 基础属性。" % String(item_id))


func _test_attribute_modifier_source_id_rewritten() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	for item_id in WEAPON_INSTANCE_EXPECTATIONS.keys():
		var item_def: ItemDef = item_defs.get(item_id) as ItemDef
		if item_def == null:
			continue
		for modifier in item_def.attribute_modifiers:
			_assert_true(modifier != null, "%s 的 modifier 不应为 null。" % String(item_id))
			if modifier == null:
				continue
			_assert_eq(modifier.source_id, item_id, "%s 的 modifier.source_id 应被重写为实例 item_id。" % String(item_id))


func _test_templates_excluded_from_item_defs() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	for template_id in TEMPLATE_IDS:
		_assert_true(not item_defs.has(template_id), "模板 %s 不应出现在 get_item_defs() 中。" % String(template_id))
	for weapon_type_id in BG3_WEAPON_PROFILE_EXPECTATIONS.keys():
		var template_id := StringName("weapon_type_%s_base" % String(weapon_type_id))
		_assert_true(not item_defs.has(template_id), "BG3 武器模板 %s 不应出现在 get_item_defs() 中。" % String(template_id))


func _test_standalone_item_without_template_unchanged() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	var standalone := item_defs.get(&"healing_herb") as ItemDef
	_assert_true(standalone != null, "未引用模板的物品 healing_herb 应仍能加载。")
	if standalone == null:
		return
	_assert_eq(standalone.base_item_id, &"", "未引用模板的物品 base_item_id 应保持空。")
	_assert_true(standalone.is_stackable, "未引用模板的消耗品应保持可堆叠属性。")


func _test_item_def_exposes_only_weapon_profile_runtime_source() -> void:
	var property_names: Dictionary = {}
	for property_data in ItemDef.new().get_property_list():
		if property_data is not Dictionary:
			continue
		property_names[String((property_data as Dictionary).get("name", ""))] = true
	_assert_true(property_names.has("weapon_profile"), "ItemDef 应暴露 weapon_profile 作为武器运行时真相源。")
	_assert_true(not property_names.has("weapon_attack_range"), "ItemDef 不应继续暴露旧 weapon_attack_range 字段。")
	_assert_true(not property_names.has("weapon_physical_damage_tag"), "ItemDef 不应继续暴露旧 weapon_physical_damage_tag 字段。")


func _test_legacy_weapon_fields_are_not_runtime_fallback() -> void:
	var no_profile_weapon := ItemDef.new()
	no_profile_weapon.item_id = &"_fixture_no_profile_weapon"
	no_profile_weapon.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	no_profile_weapon.equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON
	no_profile_weapon.equipment_slot_ids = ["main_hand"]
	no_profile_weapon.tags = [&"weapon", &"melee"]

	_assert_eq(int(no_profile_weapon.get_weapon_attack_range()), 0, "缺少 weapon_profile 时不应从旧 weapon_attack_range 字段回退。")
	_assert_eq(String(no_profile_weapon.get_weapon_physical_damage_tag()), "", "缺少 weapon_profile 时不应从旧 weapon_physical_damage_tag 字段回退。")
	_assert_true(not _modifiers_include_attribute(no_profile_weapon.get_attribute_modifiers(), &"weapon_attack_range"), "缺少 weapon_profile.attack_range 时不应生成 weapon_attack_range 属性修正。")

	var legacy_fixture := load(LEGACY_WEAPON_FIELDS_FIXTURE) as ItemDef
	_assert_true(legacy_fixture != null, "旧武器裸字段夹具应能作为无效资源加载，用于验证无运行时 fallback。")
	if legacy_fixture == null:
		return
	_assert_eq(int(legacy_fixture.get_weapon_attack_range()), 0, "旧资源中的 weapon_attack_range 不应作为运行时 fallback。")
	_assert_eq(String(legacy_fixture.get_weapon_physical_damage_tag()), "", "旧资源中的 weapon_physical_damage_tag 不应作为运行时 fallback。")
	_assert_true(not _modifiers_include_attribute(legacy_fixture.get_attribute_modifiers(), &"weapon_attack_range"), "旧资源裸 weapon_attack_range 不应注入属性快照。")


func _test_scalar_fallback() -> void:
	var template := ItemDef.new()
	template.item_id = &"_fixture_template"
	template.display_name = "TemplateName"
	template.description = "TemplateDesc"
	template.icon = "res://template_icon.svg"
	template.item_category = &"equipment"
	template.equipment_type_id = &"weapon"
	template.set("weapon_profile", _build_weapon_profile(3, &"physical_slash"))
	template.base_price = 50
	template.buy_price = 60

	var instance := ItemDef.new()
	instance.item_id = &"_fixture_instance"
	instance.display_name = ""
	instance.buy_price = 0

	var merged: ItemDef = ItemContentRegistry.merge_with_template(template, instance)
	_assert_eq(merged.item_id, &"_fixture_instance", "合并应使用 instance.item_id，永不继承模板 item_id。")
	_assert_eq(merged.display_name, "TemplateName", "instance 空字符串字段应回退模板。")
	_assert_eq(merged.icon, "res://template_icon.svg", "instance 空 icon 应回退模板。")
	_assert_eq(int(merged.get_weapon_attack_range()), 3, "instance 未设 weapon_profile.attack_range 时应回退模板。")
	_assert_eq(int(merged.buy_price), 60, "instance 0 buy_price 应回退模板。")
	_assert_eq(int(merged.base_price), 50, "instance 未设 base_price 应回退模板。")
	_assert_eq(String(merged.get_weapon_physical_damage_tag()), "physical_slash", "instance 未设 weapon_profile.damage_tag 时应回退模板。")

	var override_instance := ItemDef.new()
	override_instance.item_id = &"_fixture_override"
	override_instance.display_name = "OverrideName"
	override_instance.set("weapon_profile", _build_weapon_profile(5, &"physical_pierce"))
	override_instance.buy_price = 999
	var override_merged: ItemDef = ItemContentRegistry.merge_with_template(template, override_instance)
	_assert_eq(override_merged.display_name, "OverrideName", "instance 非空字符串应覆盖模板。")
	_assert_eq(int(override_merged.get_weapon_attack_range()), 5, "instance weapon_profile.attack_range 应覆盖模板。")
	_assert_eq(String(override_merged.get_weapon_physical_damage_tag()), "physical_pierce", "instance weapon_profile.damage_tag 应覆盖模板。")
	_assert_eq(int(override_merged.buy_price), 999, "instance 显式 buy_price 应覆盖模板。")


func _test_weapon_profile_merge_delegates_property_rules() -> void:
	var template := ItemDef.new()
	template.item_id = &"_fixture_profile_template"
	template.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	template.equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON
	var template_profile := _build_weapon_profile(2, &"physical_slash")
	template_profile.properties = [&"versatile"]
	template.set("weapon_profile", template_profile)

	var instance := ItemDef.new()
	instance.item_id = &"_fixture_profile_instance"
	var instance_profile := WeaponProfileDef.new()
	instance_profile.damage_tag = &"physical_blunt"
	instance_profile.properties_mode = WeaponProfileDef.PropertyMergeMode.ADD
	instance_profile.properties = [&"shield_breaker", &"versatile"]
	instance.set("weapon_profile", instance_profile)

	var merged: ItemDef = ItemContentRegistry.merge_with_template(template, instance)
	_assert_eq(int(merged.get_weapon_attack_range()), 2, "weapon_profile.attack_range 应由 WeaponProfileDef 的继承哨兵处理。")
	_assert_eq(String(merged.get_weapon_physical_damage_tag()), "physical_blunt", "weapon_profile.damage_tag 应由 WeaponProfileDef 覆盖。")
	var merged_profile := merged.get("weapon_profile") as WeaponProfileDef
	_assert_true(merged_profile != null, "合并后应保留 weapon_profile。")
	if merged_profile != null:
		_assert_eq(merged_profile.get_properties(), [&"versatile", &"shield_breaker"], "weapon_profile.properties 应由 WeaponProfileDef merge mode 处理。")


func _test_weapon_profile_inheritance_override_and_property_modes() -> void:
	var template_profile := _build_weapon_profile(1, ItemDef.DAMAGE_TAG_PHYSICAL_SLASH)
	template_profile.weapon_type_id = &"longsword"
	template_profile.training_group = &"martial"
	template_profile.range_type = &"melee"
	template_profile.family = &"sword"
	template_profile.one_handed_dice = _build_weapon_dice(1, 8, 0)
	template_profile.two_handed_dice = _build_weapon_dice(1, 10, 0)
	template_profile.properties_mode = WeaponProfileDef.PropertyMergeMode.REPLACE
	template_profile.properties = [&"finesse", &"light", &"versatile"]
	var template := _build_weapon_item(&"_fixture_profile_template_full", template_profile)

	var inherit_instance := _build_weapon_item(&"_fixture_profile_inherit", WeaponProfileDef.new())
	var inherit_merged: ItemDef = ItemContentRegistry.merge_with_template(template, inherit_instance)
	var inherit_profile := inherit_merged.get("weapon_profile") as WeaponProfileDef
	_assert_true(inherit_profile != null, "空 instance weapon_profile 应继承模板 profile。")
	if inherit_profile != null:
		_assert_eq(String(inherit_profile.weapon_type_id), "longsword", "weapon_profile.weapon_type_id 空值应继承模板。")
		_assert_eq(String(inherit_profile.training_group), "martial", "weapon_profile.training_group 空值应继承模板。")
		_assert_eq(String(inherit_profile.range_type), "melee", "weapon_profile.range_type 空值应继承模板。")
		_assert_eq(String(inherit_profile.family), "sword", "weapon_profile.family 空值应继承模板。")
		_assert_eq(String(inherit_profile.damage_tag), "physical_slash", "weapon_profile.damage_tag 空值应继承模板。")
		_assert_eq(int(inherit_profile.attack_range), 1, "weapon_profile.attack_range 继承哨兵应继承模板。")
		_assert_eq(_dice_to_list(inherit_profile.one_handed_dice), [1, 8, 0], "one_handed_dice 空值应继承模板。")
		_assert_eq(_dice_to_list(inherit_profile.two_handed_dice), [1, 10, 0], "two_handed_dice 空值应继承模板。")
		_assert_eq(inherit_profile.get_properties(), [&"finesse", &"light", &"versatile"], "properties_mode=INHERIT 应继承模板 properties。")
		inherit_profile.one_handed_dice.dice_sides = 99
		_assert_eq(_dice_to_list(template_profile.one_handed_dice), [1, 8, 0], "继承得到的 dice 应是深拷贝，不应污染模板。")

	var override_profile := WeaponProfileDef.new()
	override_profile.weapon_type_id = &"spear"
	override_profile.damage_tag = ItemDef.DAMAGE_TAG_PHYSICAL_PIERCE
	override_profile.attack_range = 2
	override_profile.one_handed_dice = _build_weapon_dice(1, 6, 1)
	override_profile.properties_mode = WeaponProfileDef.PropertyMergeMode.REPLACE
	override_profile.properties = [&"thrown"]
	var override_instance := _build_weapon_item(&"_fixture_profile_override", override_profile)
	var override_merged: ItemDef = ItemContentRegistry.merge_with_template(template, override_instance)
	var merged_override_profile := override_merged.get("weapon_profile") as WeaponProfileDef
	_assert_true(merged_override_profile != null, "覆盖 instance weapon_profile 应合并成功。")
	if merged_override_profile != null:
		_assert_eq(String(merged_override_profile.weapon_type_id), "spear", "非空 weapon_type_id 应覆盖模板。")
		_assert_eq(String(merged_override_profile.training_group), "martial", "未覆盖字段仍应继承模板。")
		_assert_eq(String(merged_override_profile.damage_tag), "physical_pierce", "非空 damage_tag 应覆盖模板。")
		_assert_eq(int(merged_override_profile.attack_range), 2, "非继承哨兵 attack_range 应覆盖模板。")
		_assert_eq(_dice_to_list(merged_override_profile.one_handed_dice), [1, 6, 1], "非空 one_handed_dice 应覆盖模板。")
		_assert_eq(_dice_to_list(merged_override_profile.two_handed_dice), [1, 10, 0], "空 two_handed_dice 应继续继承模板。")

	var property_cases := [
		{
			"label": "inherit",
			"mode": WeaponProfileDef.PropertyMergeMode.INHERIT,
			"properties": [&"ignored"],
			"expected": [&"finesse", &"light", &"versatile"],
		},
		{
			"label": "replace",
			"mode": WeaponProfileDef.PropertyMergeMode.REPLACE,
			"properties": [&"heavy", &"heavy"],
			"expected": [&"heavy"],
		},
		{
			"label": "add",
			"mode": WeaponProfileDef.PropertyMergeMode.ADD,
			"properties": [&"reach", &"light"],
			"expected": [&"finesse", &"light", &"versatile", &"reach"],
		},
		{
			"label": "remove",
			"mode": WeaponProfileDef.PropertyMergeMode.REMOVE,
			"properties": [&"light", &"missing"],
			"expected": [&"finesse", &"versatile"],
		},
	]
	for case_data in property_cases:
		var profile := WeaponProfileDef.new()
		profile.properties_mode = int(case_data.get("mode", WeaponProfileDef.PropertyMergeMode.INHERIT))
		profile.properties = _to_string_name_array(case_data.get("properties", []))
		var instance := _build_weapon_item(StringName("_fixture_profile_%s" % String(case_data.get("label", ""))), profile)
		var merged: ItemDef = ItemContentRegistry.merge_with_template(template, instance)
		var merged_profile := merged.get("weapon_profile") as WeaponProfileDef
		_assert_true(merged_profile != null, "properties_mode=%s 应产出 weapon_profile。" % String(case_data.get("label", "")))
		if merged_profile != null:
			_assert_eq(merged_profile.get_properties(), _to_string_name_array(case_data.get("expected", [])), "properties_mode=%s 应按规则合并 properties。" % String(case_data.get("label", "")))
			_assert_eq(int(merged_profile.properties_mode), int(WeaponProfileDef.PropertyMergeMode.REPLACE), "合并后的 properties_mode 应归一为 REPLACE，避免运行时再次解释合并模式。")


func _test_item_category_inherits_from_template() -> void:
	# 防回归：模板声明 equipment、实例只填 base_item_id 时不能静默退化为 misc。
	# 旧行为下 has_equipment_category() 会返回 false，导致整段装备校验被跳过、武器在战斗里失效。
	var template := ItemDef.new()
	template.item_id = &"_fixture_category_template"
	template.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	template.equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON
	template.equipment_slot_ids = ["main_hand"]
	template.set("weapon_profile", _build_weapon_profile(1, ItemDef.DAMAGE_TAG_PHYSICAL_SLASH))
	template.tags = [&"weapon", &"melee"]
	template.icon = "res://template_icon.svg"

	var instance := ItemDef.new()
	instance.item_id = &"_fixture_category_instance"
	# 关键：不显式填 item_category，模拟"忘了写 item_category=equipment"的常见疏漏。
	instance.is_stackable = false
	instance.max_stack = 1

	var merged: ItemDef = ItemContentRegistry.merge_with_template(template, instance)
	_assert_eq(String(merged.item_category), "equipment", "实例未填 item_category 时应继承模板分类。")
	_assert_eq(String(merged.get_item_category_normalized()), "equipment", "归一化口径应同样得到 equipment。")
	_assert_true(merged.has_equipment_category(), "继承到 equipment 后 has_equipment_category() 应为 true。")
	_assert_true(merged.is_equipment(), "继承到 equipment 后 is_equipment() 应为 true。")
	_assert_true(merged.is_weapon(), "继承到 equipment + weapon 后 is_weapon() 应为 true。")

	# 显式覆盖路径：实例填了 misc 应推翻模板。
	var override_instance := ItemDef.new()
	override_instance.item_id = &"_fixture_category_override"
	override_instance.item_category = ItemDef.ITEM_CATEGORY_MISC
	var override_merged: ItemDef = ItemContentRegistry.merge_with_template(template, override_instance)
	_assert_eq(String(override_merged.item_category), "misc", "实例显式 misc 应覆盖模板 equipment。")
	_assert_true(not override_merged.is_equipment(), "实例显式 misc 后 is_equipment() 应为 false。")


func _test_item_category_normalized_helper() -> void:
	var unset_def := ItemDef.new()
	_assert_eq(String(unset_def.item_category), "", "新建 ItemDef 的 item_category 默认应为空字符串。")
	_assert_eq(String(unset_def.get_item_category_normalized()), "misc", "未填 item_category 经归一化后应视为 misc。")
	_assert_true(not unset_def.has_equipment_category(), "未填 item_category 不应被视为装备。")
	_assert_true(not unset_def.is_skill_book(), "未填 item_category 不应被视为技能书。")

	var explicit_misc := ItemDef.new()
	explicit_misc.item_category = ItemDef.ITEM_CATEGORY_MISC
	_assert_eq(String(explicit_misc.get_item_category_normalized()), "misc", "显式 misc 经归一化后仍为 misc。")

	var explicit_equipment := ItemDef.new()
	explicit_equipment.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	_assert_true(explicit_equipment.has_equipment_category(), "显式 equipment 应被视为装备。")


func _test_string_name_array_merge_dedup_order() -> void:
	var template := ItemDef.new()
	template.item_id = &"_fixture_tags_template"
	template.tags = [&"weapon", &"melee"]
	template.crafting_groups = [&"forge"]

	var instance := ItemDef.new()
	instance.item_id = &"_fixture_tags_instance"
	instance.tags = [&"melee", &"sword", &"weapon_class_sword"]
	instance.crafting_groups = [&"smithing"]

	var merged: ItemDef = ItemContentRegistry.merge_with_template(template, instance)
	_assert_eq(merged.get_tags(), [&"weapon", &"melee", &"sword", &"weapon_class_sword"], "tags 应模板优先去重合并并保留顺序。")
	_assert_eq(merged.get_crafting_groups(), [&"forge", &"smithing"], "crafting_groups 应模板优先合并。")


func _test_equipment_slot_ids_override_not_merge() -> void:
	var template := ItemDef.new()
	template.item_id = &"_fixture_slot_template"
	template.equipment_slot_ids = ["main_hand"]
	template.occupied_slot_ids = ["main_hand", "off_hand"]

	var fallback_instance := ItemDef.new()
	fallback_instance.item_id = &"_fixture_slot_fallback"
	var fallback_merged: ItemDef = ItemContentRegistry.merge_with_template(template, fallback_instance)
	_assert_eq(_array_to_string_list(fallback_merged.equipment_slot_ids), ["main_hand"], "instance 空 equipment_slot_ids 应回退模板。")
	_assert_eq(_array_to_string_list(fallback_merged.occupied_slot_ids), ["main_hand", "off_hand"], "instance 空 occupied_slot_ids 应回退模板。")

	var override_instance := ItemDef.new()
	override_instance.item_id = &"_fixture_slot_override"
	override_instance.equipment_slot_ids = ["off_hand"]
	override_instance.occupied_slot_ids = ["off_hand"]
	var override_merged: ItemDef = ItemContentRegistry.merge_with_template(template, override_instance)
	_assert_eq(_array_to_string_list(override_merged.equipment_slot_ids), ["off_hand"], "instance 非空 equipment_slot_ids 应覆盖模板，不合并以防多入口槽。")
	_assert_eq(_array_to_string_list(override_merged.occupied_slot_ids), ["off_hand"], "instance 非空 occupied_slot_ids 应覆盖模板。")


func _test_modifier_deep_copy_and_source_id_rewrite() -> void:
	var template := ItemDef.new()
	template.item_id = &"_fixture_mod_template"
	var template_mod := AttributeModifier.new()
	template_mod.attribute_id = &"attack_bonus"
	template_mod.mode = AttributeModifier.MODE_FLAT
	template_mod.value = 1
	template_mod.source_type = &"equipment"
	template_mod.source_id = &"_fixture_mod_template"
	template.attribute_modifiers = [template_mod]

	var instance := ItemDef.new()
	instance.item_id = &"_fixture_mod_instance"
	var instance_mod := AttributeModifier.new()
	instance_mod.attribute_id = &"hit_bonus"
	instance_mod.mode = AttributeModifier.MODE_FLAT
	instance_mod.value = 2
	instance_mod.source_type = &"equipment"
	instance_mod.source_id = &"_fixture_mod_instance"
	instance.attribute_modifiers = [instance_mod]

	var merged: ItemDef = ItemContentRegistry.merge_with_template(template, instance)
	_assert_eq(int(merged.attribute_modifiers.size()), 2, "modifier 应为模板 + instance 合并。")
	for modifier in merged.attribute_modifiers:
		_assert_eq(modifier.source_id, instance.item_id, "合并后所有 modifier.source_id 应被重写为 instance item_id。")
	# Mutate the merged copy and verify original template/instance modifiers untouched.
	merged.attribute_modifiers[0].value = 999
	_assert_eq(int(template_mod.value), 1, "合并修改不应影响模板原始 modifier。")


func _test_cycle_detection() -> void:
	var a := ItemDef.new()
	a.item_id = &"_fixture_cycle_a"
	a.base_item_id = &"_fixture_cycle_b"
	var b := ItemDef.new()
	b.item_id = &"_fixture_cycle_b"
	b.base_item_id = &"_fixture_cycle_a"
	var template_defs := {
		&"_fixture_cycle_a": a,
		&"_fixture_cycle_b": b,
	}
	var errors: Array = []
	var cache: Dictionary = {}
	var visited: Array = []
	var resolved: ItemDef = ItemContentRegistry.resolve_with_template_chain(a, template_defs, visited, cache, errors)
	_assert_true(resolved == null, "循环继承应返回 null。")
	_assert_true(errors.size() >= 1, "循环继承应记录错误。")
	var cycle_error_present := false
	for error in errors:
		if String(error).find("cycle") != -1:
			cycle_error_present = true
			break
	_assert_true(cycle_error_present, "错误信息应明确指出循环继承。")


func _test_missing_template_detection() -> void:
	var instance := ItemDef.new()
	instance.item_id = &"_fixture_missing_instance"
	instance.base_item_id = &"_fixture_template_does_not_exist"
	var errors: Array = []
	var cache: Dictionary = {}
	var visited: Array = []
	var resolved: ItemDef = ItemContentRegistry.resolve_with_template_chain(instance, {}, visited, cache, errors)
	_assert_true(resolved == null, "缺失模板应返回 null。")
	_assert_true(errors.size() >= 1, "缺失模板应记录错误。")


func _test_template_id_collision_with_instance() -> void:
	# Real-registry scan should reject any future instance that reuses a template id.
	# Verified indirectly: TEMPLATE_IDS must not appear in get_item_defs().
	var item_defs := ItemContentRegistry.new().get_item_defs()
	for template_id in TEMPLATE_IDS:
		_assert_true(not item_defs.has(template_id), "模板 id %s 不应被作为正式 item 注册。" % String(template_id))


func _array_to_string_list(values: Array) -> Array:
	var result: Array = []
	for value in values:
		result.append(String(value))
	return result


func _dice_to_list(dice_resource) -> Array:
	if dice_resource == null:
		return []
	return [
		int(dice_resource.get("dice_count")),
		int(dice_resource.get("dice_sides")),
		int(dice_resource.get("flat_bonus")),
	]


func _build_weapon_profile(attack_range: int, damage_tag: StringName) -> WeaponProfileDef:
	var profile := WeaponProfileDef.new()
	profile.attack_range = attack_range
	profile.damage_tag = damage_tag
	return profile


func _build_weapon_dice(dice_count: int, dice_sides: int, flat_bonus: int = 0) -> WeaponDamageDiceDef:
	var dice := WeaponDamageDiceDef.new()
	dice.dice_count = dice_count
	dice.dice_sides = dice_sides
	dice.flat_bonus = flat_bonus
	return dice


func _build_weapon_item(item_id: StringName, weapon_profile: WeaponProfileDef) -> ItemDef:
	var item := ItemDef.new()
	item.item_id = item_id
	item.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	item.equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON
	item.equipment_slot_ids = ["main_hand"]
	item.set("weapon_profile", weapon_profile)
	return item


func _modifiers_include_attribute(modifiers: Array, attribute_id: StringName) -> bool:
	for modifier in modifiers:
		if modifier == null:
			continue
		if modifier.attribute_id == attribute_id:
			return true
	return false


func _to_string_name_array(values: Variant) -> Array[StringName]:
	var result: Array[StringName] = []
	if values is not Array:
		return result
	for raw_value in values:
		var normalized := StringName(str(raw_value))
		if normalized == &"":
			continue
		result.append(normalized)
	return result


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
