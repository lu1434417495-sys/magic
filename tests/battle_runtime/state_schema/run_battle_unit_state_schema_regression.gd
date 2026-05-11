extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BodySizeRules = preload("res://scripts/systems/progression/body_size_rules.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_valid_roundtrip_preserves_current_payload()
	_test_clone_preserves_ephemeral_charge_state()
	_test_extended_body_size_categories_roundtrip()
	_test_rejects_non_dictionary_empty_missing_and_extra_fields()
	_test_rejects_wrong_top_level_types()
	_test_rejects_string_numeric_values()
	_test_rejects_bad_string_name_arrays()
	_test_rejects_bad_identity_projection_fields()
	_test_rejects_bad_combat_resource_unlocks()
	_test_rejects_bad_status_effect_entries()
	_test_rejects_equipment_view_bad_payload()
	_test_rejects_bad_weapon_dice_payloads()
	if _failures.is_empty():
		print("Battle unit state schema regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle unit state schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_valid_roundtrip_preserves_current_payload() -> void:
	var unit := _build_unit()
	var payload := unit.to_dict()
	var restored = BattleUnitState.from_dict(payload) as BattleUnitState
	_assert_true(restored != null, "当前 to_dict payload 应可由 from_dict 恢复。")
	_assert_eq(restored.current_move_points if restored != null else -1, 5, "current_move_points 应保留大于默认值的 int。")
	_assert_eq(String(restored.body_size_category if restored != null else &""), "large", "body_size_category 应随 body_size round-trip。")
	_assert_eq(restored.vision_tags if restored != null else [], [&"darkvision"], "vision_tags 应 round-trip。")
	_assert_eq(String(restored.damage_resistances.get(&"fire", &"") if restored != null else &""), "half", "damage_resistances 应 round-trip。")
	_assert_eq(restored.to_dict() if restored != null else {}, payload, "BattleUnitState 应保持 to_dict/from_dict round-trip。")


func _test_clone_preserves_ephemeral_charge_state() -> void:
	var unit := _build_unit()
	unit.per_battle_charges = {&"dragon_breath": 1}
	unit.per_turn_charges = {&"nimble_escape": 1}
	unit.per_turn_charge_limits = {&"nimble_escape": 1}
	var cloned := unit.clone()
	_assert_true(cloned != null, "BattleUnitState.clone() 应返回可用副本。")
	if cloned == null:
		return
	_assert_eq(cloned.to_dict(), unit.to_dict(), "clone 应保留序列化字段。")
	_assert_eq(int(cloned.per_battle_charges.get(&"dragon_breath", -1)), 1, "clone 应深拷贝 per_battle_charges。")
	_assert_eq(int(cloned.per_turn_charges.get(&"nimble_escape", -1)), 1, "clone 应深拷贝 per_turn_charges。")
	_assert_eq(int(cloned.per_turn_charge_limits.get(&"nimble_escape", -1)), 1, "clone 应深拷贝 per_turn_charge_limits。")
	cloned.per_battle_charges[&"dragon_breath"] = 0
	cloned.per_turn_charges[&"nimble_escape"] = 0
	cloned.per_turn_charge_limits[&"nimble_escape"] = 0
	_assert_eq(int(unit.per_battle_charges.get(&"dragon_breath", -1)), 1, "clone 不应共享 per_battle_charges 字典。")
	_assert_eq(int(unit.per_turn_charges.get(&"nimble_escape", -1)), 1, "clone 不应共享 per_turn_charges 字典。")
	_assert_eq(int(unit.per_turn_charge_limits.get(&"nimble_escape", -1)), 1, "clone 不应共享 per_turn_charge_limits 字典。")


func _test_extended_body_size_categories_roundtrip() -> void:
	var tiny := _build_unit()
	_assert_true(tiny.set_body_size_category(BodySizeRules.BODY_SIZE_CATEGORY_TINY), "tiny category 应可设置。")
	var tiny_payload := tiny.to_dict()
	_assert_eq(String(tiny_payload.get("body_size_category", "")), "tiny", "to_dict 应保留 tiny category。")
	_assert_eq(int(tiny_payload.get("body_size", 0)), BodySizeRules.BODY_SIZE_TINY, "tiny 应映射到 BodySizeRules 的 int。")
	_assert_eq(tiny_payload.get("footprint_size", Vector2i.ZERO), Vector2i.ONE, "tiny footprint 应为 1x1。")
	_assert_true(BattleUnitState.from_dict(tiny_payload) != null, "tiny payload 应可 round-trip。")

	var gargantuan := _build_unit()
	_assert_true(gargantuan.set_body_size_category(BodySizeRules.BODY_SIZE_CATEGORY_GARGANTUAN), "gargantuan category 应可设置。")
	var gargantuan_payload := gargantuan.to_dict()
	_assert_eq(int(gargantuan_payload.get("body_size", 0)), BodySizeRules.BODY_SIZE_GARGANTUAN, "gargantuan 应映射到 BodySizeRules 的 int。")
	_assert_eq(gargantuan_payload.get("footprint_size", Vector2i.ZERO), Vector2i(3, 3), "gargantuan footprint 应为 3x3。")
	_assert_eq((gargantuan_payload.get("occupied_coords", []) as Array).size(), 9, "gargantuan 应占 9 格。")
	_assert_true(BattleUnitState.from_dict(gargantuan_payload) != null, "gargantuan payload 应可 round-trip。")

	var boss := _build_unit()
	_assert_true(boss.set_body_size_category(BodySizeRules.BODY_SIZE_CATEGORY_BOSS), "boss category 应可设置。")
	var boss_payload := boss.to_dict()
	_assert_eq(int(boss_payload.get("body_size", 0)), BodySizeRules.BODY_SIZE_BOSS, "boss 应映射到 BodySizeRules 的 int。")
	_assert_eq(boss_payload.get("footprint_size", Vector2i.ZERO), Vector2i(3, 3), "boss footprint 应为 3x3。")
	_assert_true(BattleUnitState.from_dict(boss_payload) != null, "boss payload 应可 round-trip。")


func _test_rejects_non_dictionary_empty_missing_and_extra_fields() -> void:
	_assert_true(BattleUnitState.from_dict("not a dict") == null, "非 Dictionary payload 应拒绝。")
	_assert_true(BattleUnitState.from_dict({}) == null, "空 Dictionary payload 应拒绝。")

	var missing := _payload()
	missing.erase("footprint_size")
	_assert_rejected(missing, "缺少当前 to_dict 字段应拒绝。")

	var extra := _payload()
	extra["legacy_body_size"] = 1
	_assert_rejected(extra, "包含额外旧字段应拒绝。")


func _test_rejects_wrong_top_level_types() -> void:
	var bad_coord := _payload()
	bad_coord["coord"] = "0,0"
	_assert_rejected(bad_coord, "coord 非 Vector2i 应拒绝。")

	var bad_footprint := _payload()
	bad_footprint["footprint_size"] = Vector2i.ONE
	_assert_rejected(bad_footprint, "footprint_size 与 body_size 刷新结果不一致应拒绝。")

	var bad_occupied := _payload()
	bad_occupied["occupied_coords"] = [Vector2i(9, 9)]
	_assert_rejected(bad_occupied, "occupied_coords 与 coord/body_size 刷新结果不一致应拒绝。")

	var bad_bool := _payload()
	bad_bool["is_alive"] = "true"
	_assert_rejected(bad_bool, "bool 字段使用字符串应拒绝。")

	var bad_required_id := _payload()
	bad_required_id["unit_id"] = ""
	_assert_rejected(bad_required_id, "必填 String/StringName 为空应拒绝。")

	var bad_weapon_family := _payload()
	bad_weapon_family["weapon_family"] = 7
	_assert_rejected(bad_weapon_family, "weapon_family 非 String/StringName 应拒绝。")


func _test_rejects_string_numeric_values() -> void:
	for field_name in ["current_hp", "current_ap", "aura_max", "weapon_attack_range", "last_turn_tu"]:
		var payload := _payload()
		payload[field_name] = "7"
		_assert_rejected(payload, "%s 使用字符串数字应拒绝。" % field_name)

	var bad_move_points := _payload()
	bad_move_points["current_move_points"] = -1
	_assert_rejected(bad_move_points, "current_move_points 负数应拒绝。")

	var bad_attribute := _payload()
	bad_attribute["attribute_snapshot"]["strength"] = "3"
	_assert_rejected(bad_attribute, "attribute_snapshot value 非 int 应拒绝。")

	var bad_skill_level := _payload()
	bad_skill_level["known_skill_level_map"]["slash"] = "2"
	_assert_rejected(bad_skill_level, "known_skill_level_map value 非 int 应拒绝。")


func _test_rejects_bad_string_name_arrays() -> void:
	var empty_skill_id := _payload()
	empty_skill_id["known_active_skill_ids"] = ["slash", ""]
	_assert_rejected(empty_skill_id, "known_active_skill_ids 空元素应拒绝。")

	var duplicate_skill_id := _payload()
	duplicate_skill_id["known_active_skill_ids"] = ["slash", "slash"]
	_assert_rejected(duplicate_skill_id, "known_active_skill_ids 重复元素应拒绝。")

	var bad_movement_tag := _payload()
	bad_movement_tag["movement_tags"] = ["grounded", 3]
	_assert_rejected(bad_movement_tag, "movement_tags 非 String/StringName 元素应拒绝。")

	var duplicate_trait_id := _payload()
	duplicate_trait_id["race_trait_ids"] = ["brave", "brave"]
	_assert_rejected(duplicate_trait_id, "race_trait_ids 重复元素应拒绝。")

	var bad_save_advantage_tag := _payload()
	bad_save_advantage_tag["save_advantage_tags"] = ["charm", ""]
	_assert_rejected(bad_save_advantage_tag, "save_advantage_tags 空元素应拒绝。")


func _test_rejects_bad_identity_projection_fields() -> void:
	var category_mismatch := _payload()
	category_mismatch["body_size_category"] = "medium"
	_assert_rejected(category_mismatch, "body_size_category 与 body_size 不一致应拒绝。")

	var invalid_category := _payload()
	invalid_category["body_size_category"] = "colossal"
	_assert_rejected(invalid_category, "非法 body_size_category 应拒绝。")

	var bad_damage_key := _payload()
	bad_damage_key["damage_resistances"][3] = "half"
	_assert_rejected(bad_damage_key, "damage_resistances 非字符串 key 应拒绝。")

	var bad_damage_value := _payload()
	bad_damage_value["damage_resistances"]["fire"] = "quarter"
	_assert_rejected(bad_damage_value, "damage_resistances 非法 mitigation tier 应拒绝。")


func _test_rejects_bad_combat_resource_unlocks() -> void:
	var missing_hp := _payload()
	missing_hp["unlocked_combat_resource_ids"] = ["stamina"]
	_assert_rejected(missing_hp, "unlocked_combat_resource_ids 缺 hp 应拒绝。")

	var missing_stamina := _payload()
	missing_stamina["unlocked_combat_resource_ids"] = ["hp"]
	_assert_rejected(missing_stamina, "unlocked_combat_resource_ids 缺 stamina 应拒绝。")

	var illegal_resource := _payload()
	illegal_resource["unlocked_combat_resource_ids"] = ["hp", "stamina", "rage"]
	_assert_rejected(illegal_resource, "unlocked_combat_resource_ids 含非法资源应拒绝。")

	var duplicate_resource := _payload()
	duplicate_resource["unlocked_combat_resource_ids"] = ["hp", "stamina", "hp"]
	_assert_rejected(duplicate_resource, "unlocked_combat_resource_ids 重复资源应拒绝。")


func _test_rejects_bad_status_effect_entries() -> void:
	var bad_entry := _payload()
	bad_entry["status_effects"]["burning"] = "bad"
	_assert_rejected(bad_entry, "status_effects 坏 entry 应拒绝整份 unit payload。")

	var key_mismatch := _payload()
	key_mismatch["status_effects"]["burning"]["status_id"] = "slow"
	_assert_rejected(key_mismatch, "status_effects key 与 payload.status_id 不一致应拒绝。")

	var empty_key := _payload()
	empty_key["status_effects"][""] = empty_key["status_effects"]["burning"]
	empty_key["status_effects"].erase("burning")
	_assert_rejected(empty_key, "status_effects 空 key 应拒绝。")


func _test_rejects_equipment_view_bad_payload() -> void:
	var payload := _payload()
	payload["equipment_view"].erase("equipped_slots")
	_assert_rejected(payload, "equipment_view 无法由 EquipmentState.from_dict 恢复时应拒绝整份 payload。")


func _test_rejects_bad_weapon_dice_payloads() -> void:
	var string_dice := _payload()
	string_dice["weapon_one_handed_dice"]["dice_count"] = "1"
	_assert_rejected(string_dice, "weapon dice 字符串数字应拒绝。")

	var missing_dice_field := _payload()
	missing_dice_field["weapon_one_handed_dice"].erase("flat_bonus")
	_assert_rejected(missing_dice_field, "weapon dice 缺字段应拒绝。")

	var extra_dice_field := _payload()
	extra_dice_field["weapon_one_handed_dice"]["legacy_bonus"] = 1
	_assert_rejected(extra_dice_field, "weapon dice 旧额外字段应拒绝。")

	var invalid_sides := _payload()
	invalid_sides["weapon_two_handed_dice"]["dice_sides"] = 0
	_assert_rejected(invalid_sides, "weapon dice dice_sides <= 0 应拒绝。")

	var invalid_kind := _payload()
	invalid_kind["weapon_profile_kind"] = "legacy_weapon"
	_assert_rejected(invalid_kind, "非法 weapon_profile_kind 应拒绝。")

	var invalid_grip := _payload()
	invalid_grip["weapon_current_grip"] = "legacy_grip"
	_assert_rejected(invalid_grip, "非法 weapon_current_grip 应拒绝。")


func _build_unit() -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = &"schema_unit"
	unit.source_member_id = &"member_1"
	unit.display_name = "Schema Unit"
	unit.faction_id = &"player"
	unit.control_mode = &"manual"
	unit.ai_blackboard = {"target": "enemy_1"}
	unit.coord = Vector2i(3, 4)
	unit.body_size = 3
	unit.body_size_category = &"large"
	unit.current_hp = 21
	unit.current_mp = 4
	unit.current_stamina = 13
	unit.current_aura = 2
	unit.attribute_snapshot.set_value(&"strength", 3)
	unit.attribute_snapshot.set_value(&"aura_max", 6)
	unit.current_ap = 1
	unit.current_move_points = 5
	unit.unlocked_combat_resource_ids = [&"hp", &"stamina", &"aura"]
	unit.stamina_recovery_progress = 7
	unit.current_shield_hp = 4
	unit.shield_max_hp = 8
	unit.shield_duration = 30
	unit.shield_family = &"ward"
	unit.shield_source_unit_id = &"schema_unit"
	unit.shield_source_skill_id = &"ward_skill"
	unit.shield_params = {"kind": "test"}
	unit.action_progress = 20
	unit.action_threshold = 140
	unit.known_active_skill_ids = [&"slash"]
	unit.known_skill_level_map = {&"slash": 2}
	unit.movement_tags = [&"grounded"]
	unit.vision_tags = [&"darkvision"]
	unit.proficiency_tags = [&"light_armor"]
	unit.save_advantage_tags = [&"charm"]
	unit.damage_resistances = {&"fire": &"half"}
	unit.race_trait_ids = [&"brave"]
	unit.subrace_trait_ids = [&"fleet_of_foot"]
	unit.ascension_trait_ids = [&"dragon_breath"]
	unit.bloodline_trait_ids = [&"draconic_resilience"]
	unit.versatility_pick = &"strength"
	unit.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "training_longsword",
		"weapon_profile_type_id": "longsword",
		"weapon_family": "sword",
		"weapon_current_grip": "two_handed",
		"weapon_attack_range": 2,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 8, "flat_bonus": 0},
		"weapon_two_handed_dice": {"dice_count": 1, "dice_sides": 10, "flat_bonus": 1},
		"weapon_is_versatile": true,
		"weapon_uses_two_hands": true,
		"weapon_physical_damage_tag": "physical_slash",
	})
	unit.cooldowns = {"slash": 12}
	unit.last_turn_tu = 50
	var effect := BattleStatusEffectState.new()
	effect.status_id = &"burning"
	effect.source_unit_id = &"source"
	effect.power = 3
	effect.params = {"element": "fire"}
	effect.stacks = 2
	effect.duration = 20
	unit.set_status_effect(effect)
	unit.combo_state = {"chain": 1}
	return unit


func _payload() -> Dictionary:
	return _build_unit().to_dict()


func _assert_rejected(payload: Variant, message: String) -> void:
	_assert_true(BattleUnitState.from_dict(payload) == null, message)


func _assert_true(value: bool, message: String) -> void:
	if not value:
		_test.fail(message)


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_test.fail("%s Actual=%s Expected=%s" % [message, str(actual), str(expected)])
