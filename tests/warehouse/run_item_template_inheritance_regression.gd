extends SceneTree

const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const AttributeModifier = preload("res://scripts/player/progression/attribute_modifier.gd")
const WeaponProfileDef = preload("res://scripts/player/warehouse/weapon_profile_def.gd")

const WEAPON_INSTANCE_EXPECTATIONS := {
	&"bronze_sword": {
		"base_item_id": &"weapon_sword_one_handed_base",
		"tags": [&"weapon", &"melee", &"one_handed", &"sword", &"weapon_class_sword"],
		"damage_tag": &"physical_slash",
		"equipment_slot_ids": ["main_hand"],
		"occupied_slot_ids": [],
		"attack_range": 1,
		"modifier_count": 2,
		"base_price": 120,
		"buy_price": 0,
		"sell_price": 0,
	},
	&"iron_greatsword": {
		"base_item_id": &"weapon_greatsword_two_handed_base",
		"tags": [&"weapon", &"melee", &"two_handed", &"greatsword", &"weapon_class_sword"],
		"damage_tag": &"physical_slash",
		"equipment_slot_ids": ["main_hand"],
		"occupied_slot_ids": ["main_hand", "off_hand"],
		"attack_range": 1,
		"modifier_count": 1,
		"base_price": 240,
		"buy_price": 0,
		"sell_price": 0,
	},
	&"militia_axe": {
		"base_item_id": &"weapon_axe_one_handed_base",
		"tags": [&"weapon", &"melee", &"one_handed", &"axe", &"weapon_class_axe"],
		"damage_tag": &"physical_slash",
		"equipment_slot_ids": ["main_hand"],
		"occupied_slot_ids": [],
		"attack_range": 1,
		"modifier_count": 2,
		"base_price": 135,
		"buy_price": 145,
		"sell_price": 70,
	},
	&"scout_dagger": {
		"base_item_id": &"weapon_dagger_one_handed_base",
		"tags": [&"weapon", &"melee", &"one_handed", &"dagger", &"weapon_class_dagger"],
		"damage_tag": &"physical_pierce",
		"equipment_slot_ids": ["main_hand"],
		"occupied_slot_ids": [],
		"attack_range": 1,
		"modifier_count": 2,
		"base_price": 120,
		"buy_price": 130,
		"sell_price": 65,
	},
	&"watchman_mace": {
		"base_item_id": &"weapon_mace_one_handed_base",
		"tags": [&"weapon", &"melee", &"one_handed", &"mace", &"weapon_class_mace"],
		"damage_tag": &"physical_blunt",
		"equipment_slot_ids": ["main_hand"],
		"occupied_slot_ids": [],
		"attack_range": 1,
		"modifier_count": 2,
		"base_price": 165,
		"buy_price": 175,
		"sell_price": 85,
	},
}

const TEMPLATE_IDS := [
	&"weapon_melee_base",
	&"weapon_melee_one_handed_base",
	&"weapon_melee_two_handed_base",
	&"weapon_sword_one_handed_base",
	&"weapon_greatsword_two_handed_base",
	&"weapon_axe_one_handed_base",
	&"weapon_dagger_one_handed_base",
	&"weapon_mace_one_handed_base",
]

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_real_registry_no_validation_errors()
	_test_weapon_instances_resolve_against_templates()
	_test_attribute_modifier_source_id_rewritten()
	_test_templates_excluded_from_item_defs()
	_test_standalone_item_without_template_unchanged()
	_test_item_def_exposes_only_weapon_profile_runtime_source()
	_test_scalar_fallback()
	_test_weapon_profile_merge_delegates_property_rules()
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


func _build_weapon_profile(attack_range: int, damage_tag: StringName) -> WeaponProfileDef:
	var profile := WeaponProfileDef.new()
	profile.attack_range = attack_range
	profile.damage_tag = damage_tag
	return profile


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
