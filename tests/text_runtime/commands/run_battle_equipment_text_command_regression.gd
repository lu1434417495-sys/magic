extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_TEXT_COMMAND_RUNNER_SCRIPT = preload("res://scripts/systems/game_runtime/headless/game_text_command_runner.gd")
const EquipmentRequirement = preload("res://scripts/player/equipment/equipment_requirement.gd")
const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")
const WeaponDamageDiceDef = preload("res://scripts/player/warehouse/weapon_damage_dice_def.gd")
const WeaponProfileDef = preload("res://scripts/player/warehouse/weapon_profile_def.gd")

const VERSATILE_TEST_WEAPON_ID: StringName = &"wpndice_versatile_longsword"
const OFFHAND_TEST_ITEM_ID: StringName = &"wpndice_offhand_focus"
const RESTRICTED_TEST_HELM_ID: StringName = &"wpndice_restricted_helm"
const STRING_KEY_ONLY_TEST_HELM_ID: StringName = &"wpndice_string_key_only_helm"
const STRING_KEY_ONLY_TEST_HELM_INSTANCE_ID: StringName = &"wpndice_string_key_only_helm_001"
const DUPLICATE_TEST_CHARM_ID: StringName = &"wpndice_duplicate_charm"
const DUPLICATE_TEST_CHARM_COMMON_INSTANCE_ID: StringName = &"wpndice_duplicate_charm_common_001"
const DUPLICATE_TEST_CHARM_RARE_INSTANCE_ID: StringName = &"wpndice_duplicate_charm_rare_001"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var runner = GAME_TEXT_COMMAND_RUNNER_SCRIPT.new()
	await runner.initialize()

	_install_battle_equipment_test_items(runner)
	await _run_command(runner, "game new test")
	await _run_command(runner, "warehouse capacity 10")
	await _run_command(runner, "warehouse add bronze_sword 1")
	await _run_command(runner, "warehouse add leather_cap 1")
	await _run_command(runner, "warehouse add leather_jerkin 1")
	await _run_command(runner, "warehouse add iron_greatsword 1")
	await _run_command(runner, "warehouse add %s 1" % String(VERSATILE_TEST_WEAPON_ID))
	await _run_command(runner, "warehouse add %s 1" % String(OFFHAND_TEST_ITEM_ID))
	await _run_command(runner, "warehouse add %s 1" % String(RESTRICTED_TEST_HELM_ID))
	await _run_command(runner, "battle start settlement")
	await _run_command(runner, "battle confirm")
	await _advance_to_manual_battle_turn(runner)
	var active_unit_state = _get_active_unit_state(runner)
	var active_member_id := String(active_unit_state.source_member_id) if active_unit_state != null else ""
	_assert_true(not active_member_id.is_empty(), "战斗换装回归前置：手动单位应关联队伍成员。")

	_install_string_key_only_battle_item_instance(runner)
	_prime_active_unit_ap(runner, 2)
	var string_key_result = await _run_command_expect_fail(
		runner,
		"battle equip head %s instance_id=%s" % [String(STRING_KEY_ONLY_TEST_HELM_ID), String(STRING_KEY_ONLY_TEST_HELM_INSTANCE_ID)]
	)
	_assert_battle_equip_string_key_only_item_not_found(string_key_result.snapshot, string_key_result.snapshot_text)

	_prime_active_unit_ap(runner, 2)
	var requirement_result = await _run_command_expect_fail(
		runner,
		"battle equip head %s" % String(RESTRICTED_TEST_HELM_ID)
	)
	_assert_battle_equip_requirement_failure(requirement_result.snapshot, requirement_result.snapshot_text)

	_install_duplicate_battle_item_instances(runner)
	_prime_active_unit_ap(runner, 4)
	var duplicate_item_only_result = await _run_command_expect_fail(
		runner,
		"battle equip necklace %s" % String(DUPLICATE_TEST_CHARM_ID)
	)
	_assert_battle_duplicate_item_only_requires_instance(duplicate_item_only_result)

	_prime_active_unit_ap(runner, 4)
	var duplicate_explicit_result = await _run_command(
		runner,
		"battle equip necklace %s instance_id=%s" % [String(DUPLICATE_TEST_CHARM_ID), String(DUPLICATE_TEST_CHARM_RARE_INSTANCE_ID)]
	)
	_assert_battle_duplicate_explicit_equip(duplicate_explicit_result.snapshot)

	_prime_active_unit_ap(runner, 4)
	var duplicate_unequip_result = await _run_command(
		runner,
		"battle unequip necklace instance_id=%s" % String(DUPLICATE_TEST_CHARM_RARE_INSTANCE_ID)
	)
	_assert_battle_duplicate_unequip_round_trip(duplicate_unequip_result.snapshot, runner)

	_prime_active_unit_ap(runner, 4)
	var equip_result = await _run_command(runner, "battle equip head leather_cap")
	_assert_successful_battle_equip(equip_result.snapshot, equip_result.snapshot_text)

	_prime_active_unit_ap(runner, 1)
	var ap_result = await _run_command_expect_fail(runner, "battle equip main_hand bronze_sword")
	_assert_battle_equip_ap_failure(ap_result.snapshot, ap_result.snapshot_text)

	_prime_active_unit_ap(runner, 2)
	var other_unit_id := _find_other_battle_unit_id(runner.get_session().build_snapshot())
	var target_result = await _run_command_expect_fail(
		runner,
		"battle equip main_hand bronze_sword target_unit_id=%s" % other_unit_id
	)
	_assert_battle_equip_self_only_failure(target_result.snapshot, target_result.snapshot_text)

	await _run_command(runner, "warehouse capacity 1")
	_prime_active_unit_ap(runner, 2)
	var full_result = await _run_command_expect_fail(runner, "battle unequip head")
	_assert_battle_unequip_backpack_full_failure(full_result.snapshot, full_result.snapshot_text)

	await _run_command(runner, "warehouse capacity 10")
	_prime_active_unit_ap(runner, 6)
	var two_handed_result = await _run_command(runner, "battle equip main_hand iron_greatsword")
	_assert_battle_two_handed_weapon_linkage(two_handed_result.snapshot)

	_prime_active_unit_ap(runner, 6)
	var versatile_result = await _run_command(
		runner,
		"battle equip main_hand %s" % String(VERSATILE_TEST_WEAPON_ID)
	)
	_assert_battle_versatile_free_offhand_linkage(versatile_result.snapshot)

	_prime_active_unit_ap(runner, 6)
	var offhand_result = await _run_command(
		runner,
		"battle equip off_hand %s" % String(OFFHAND_TEST_ITEM_ID)
	)
	_assert_battle_offhand_forces_versatile_one_handed(offhand_result.snapshot)
	_assert_party_equipment_not_updated_during_battle(offhand_result.snapshot, active_member_id)

	_prime_active_unit_ap(runner, 4)
	var body_armor_equip_result = await _run_command(runner, "battle equip body leather_jerkin")
	_assert_battle_body_armor_equipped_without_hp_bonus(body_armor_equip_result.snapshot)
	var body_armor_equip_report := _find_latest_change_equipment_report(body_armor_equip_result.snapshot)
	_prime_active_unit_hp_and_ap(runner, int(body_armor_equip_report.get("hp_max_after", 0)), 2)
	var body_armor_unequip_result = await _run_command(runner, "battle unequip body")
	_assert_battle_unequip_body_armor_turn_end_without_hp_clamp(body_armor_unequip_result.snapshot, body_armor_unequip_result.snapshot_text)

	var finish_result = await _run_command(runner, "battle finish player")
	_assert_party_equipment_written_back_after_battle(finish_result.snapshot, active_member_id)

	await runner.dispose(true)
	_finish()


func _advance_to_manual_battle_turn(runner, max_ticks: int = 64) -> void:
	for _index in range(max_ticks):
		var battle_snapshot: Dictionary = runner.get_session().build_snapshot().get("battle", {})
		if not bool(battle_snapshot.get("active", false)):
			break
		var active_unit_id := String(battle_snapshot.get("active_unit_id", ""))
		var active_unit := _find_battle_unit(battle_snapshot, active_unit_id)
		if String(active_unit.get("control_mode", "")) == "manual":
			return
		await _run_command(runner, "battle tick 1")
	_assert_true(false, "战斗换装文本回归未能进入手动单位回合。")


func _prime_active_unit_ap(runner, current_ap: int) -> void:
	var active_unit = _get_active_unit_state(runner)
	_assert_true(active_unit != null, "战斗换装文本回归前置：应存在当前行动单位。")
	if active_unit == null:
		return
	active_unit.current_ap = current_ap
	if active_unit.attribute_snapshot != null:
		active_unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 1))


func _prime_active_unit_hp_and_ap(runner, current_hp: int, current_ap: int) -> void:
	var active_unit = _get_active_unit_state(runner)
	_assert_true(active_unit != null, "HP clamp 文本回归前置：应存在当前行动单位。")
	if active_unit == null:
		return
	active_unit.current_hp = current_hp
	active_unit.current_ap = current_ap


func _get_active_unit_state(runner):
	var runtime = runner.get_session().get_runtime_facade()
	var battle_state = runtime.get_battle_state() if runtime != null else null
	if battle_state == null or battle_state.active_unit_id == &"":
		return null
	return battle_state.units.get(battle_state.active_unit_id)


func _install_battle_equipment_test_items(runner) -> void:
	var game_session = runner.get_session().get_game_session()
	_assert_true(game_session != null, "战斗换装回归前置：应存在 GameSession。")
	if game_session == null:
		return
	_assert_eq(
		game_session.install_test_content_def(&"item", VERSATILE_TEST_WEAPON_ID, _build_versatile_test_weapon_def()),
		OK,
		"应能注册战斗换装测试用 versatile 武器。"
	)
	_assert_eq(
		game_session.install_test_content_def(&"item", OFFHAND_TEST_ITEM_ID, _build_offhand_test_item_def()),
		OK,
		"应能注册战斗换装测试用副手物品。"
	)
	_assert_eq(
		game_session.install_test_content_def(&"item", RESTRICTED_TEST_HELM_ID, _build_restricted_test_helm_def()),
		OK,
		"应能注册战斗换装测试用受限头盔。"
	)
	_assert_eq(
		game_session.install_test_content_def(&"item", String(STRING_KEY_ONLY_TEST_HELM_ID), _build_string_key_only_test_helm_def()),
		OK,
		"应能以 String key 注册战斗换装测试用头盔。"
	)
	_assert_eq(
		game_session.install_test_content_def(&"item", DUPLICATE_TEST_CHARM_ID, _build_duplicate_test_charm_def()),
		OK,
		"应能注册战斗换装测试用重复饰品。"
	)


func _install_string_key_only_battle_item_instance(runner) -> void:
	var runtime = runner.get_session().get_runtime_facade()
	var battle_state = runtime.get_battle_state() if runtime != null else null
	_assert_true(battle_state != null, "String-key-only 战斗换装回归前置：应存在战斗状态。")
	if battle_state == null:
		return
	var backpack = battle_state.get_party_backpack_view()
	_assert_true(backpack != null, "String-key-only 战斗换装回归前置：应存在 battle-local 背包。")
	if backpack == null:
		return
	backpack.equipment_instances.append(
		EquipmentInstanceState.create(STRING_KEY_ONLY_TEST_HELM_ID, STRING_KEY_ONLY_TEST_HELM_INSTANCE_ID)
	)


func _install_duplicate_battle_item_instances(runner) -> void:
	var runtime = runner.get_session().get_runtime_facade()
	var battle_state = runtime.get_battle_state() if runtime != null else null
	_assert_true(battle_state != null, "重复实例战斗换装回归前置：应存在战斗状态。")
	if battle_state == null:
		return
	var backpack = battle_state.get_party_backpack_view()
	_assert_true(backpack != null, "重复实例战斗换装回归前置：应存在 battle-local 背包。")
	if backpack == null:
		return
	var common_instance := EquipmentInstanceState.create(DUPLICATE_TEST_CHARM_ID, DUPLICATE_TEST_CHARM_COMMON_INSTANCE_ID)
	common_instance.rarity = EquipmentInstanceState.RarityTier.COMMON
	common_instance.current_durability = 10
	var rare_instance := EquipmentInstanceState.create(DUPLICATE_TEST_CHARM_ID, DUPLICATE_TEST_CHARM_RARE_INSTANCE_ID)
	rare_instance.rarity = EquipmentInstanceState.RarityTier.RARE
	rare_instance.current_durability = 24
	backpack.equipment_instances.append(common_instance)
	backpack.equipment_instances.append(rare_instance)


func _build_versatile_test_weapon_def() -> ItemDef:
	var item_def := ItemDef.new()
	item_def.item_id = VERSATILE_TEST_WEAPON_ID
	item_def.display_name = "WPNDICE Versatile Longsword"
	item_def.is_stackable = false
	item_def.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	item_def.equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON
	item_def.equipment_slot_ids = ["main_hand"]
	item_def.tags = [&"weapon", &"melee", &"versatile", &"test"]

	var profile := WeaponProfileDef.new()
	profile.weapon_type_id = &"wpndice_longsword"
	profile.damage_tag = ItemDef.DAMAGE_TAG_PHYSICAL_SLASH
	profile.attack_range = 1
	profile.one_handed_dice = _build_weapon_dice(1, 8, 0)
	profile.two_handed_dice = _build_weapon_dice(1, 10, 0)
	profile.properties_mode = WeaponProfileDef.PropertyMergeMode.REPLACE
	profile.properties = [&"versatile"]
	item_def.weapon_profile = profile
	return item_def


func _build_offhand_test_item_def() -> ItemDef:
	var item_def := ItemDef.new()
	item_def.item_id = OFFHAND_TEST_ITEM_ID
	item_def.display_name = "WPNDICE Offhand Focus"
	item_def.is_stackable = false
	item_def.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	item_def.equipment_type_id = ItemDef.EQUIPMENT_TYPE_ACCESSORY
	item_def.equipment_slot_ids = ["off_hand"]
	item_def.tags = [&"off_hand", &"focus", &"test"]
	return item_def


func _build_duplicate_test_charm_def() -> ItemDef:
	var item_def := ItemDef.new()
	item_def.item_id = DUPLICATE_TEST_CHARM_ID
	item_def.display_name = "WPNDICE Duplicate Charm"
	item_def.is_stackable = false
	item_def.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	item_def.equipment_type_id = ItemDef.EQUIPMENT_TYPE_ACCESSORY
	item_def.equipment_slot_ids = ["necklace"]
	item_def.tags = [&"accessory", &"test"]
	return item_def


func _build_restricted_test_helm_def() -> ItemDef:
	var item_def := ItemDef.new()
	item_def.item_id = RESTRICTED_TEST_HELM_ID
	item_def.display_name = "WPNDICE Restricted Helm"
	item_def.is_stackable = false
	item_def.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	item_def.equipment_type_id = ItemDef.EQUIPMENT_TYPE_ARMOR
	item_def.equipment_slot_ids = ["head"]
	item_def.tags = [&"head", &"armor", &"test"]
	var requirement := EquipmentRequirement.new()
	requirement.min_body_size = 99
	item_def.equip_requirement = requirement
	return item_def


func _build_string_key_only_test_helm_def() -> ItemDef:
	var item_def := ItemDef.new()
	item_def.item_id = STRING_KEY_ONLY_TEST_HELM_ID
	item_def.display_name = "WPNDICE String Key Only Helm"
	item_def.is_stackable = false
	item_def.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	item_def.equipment_type_id = ItemDef.EQUIPMENT_TYPE_ARMOR
	item_def.equipment_slot_ids = ["head"]
	item_def.tags = [&"head", &"armor", &"test"]
	return item_def


func _build_weapon_dice(dice_count: int, dice_sides: int, flat_bonus: int) -> WeaponDamageDiceDef:
	var dice := WeaponDamageDiceDef.new()
	dice.dice_count = dice_count
	dice.dice_sides = dice_sides
	dice.flat_bonus = flat_bonus
	return dice


func _assert_successful_battle_equip(snapshot: Dictionary, text_snapshot: String) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(not report.is_empty(), "成功换装后快照应包含 change_equipment report。")
	_assert_true(bool(report.get("ok", false)), "成功换装 report 应标记 ok=true。")
	_assert_eq(String(report.get("operation", "")), "equip", "成功换装 report 应记录 equip 操作。")
	_assert_eq(String(report.get("slot_id", "")), "head", "成功换装 report 应记录 head 槽。")
	_assert_eq(String(report.get("item_id", "")), "leather_cap", "成功换装 report 应记录装备物品。")
	_assert_eq(int(report.get("ap_before", 0)), 4, "成功换装 report 应记录换装前 AP。")
	_assert_eq(int(report.get("ap_after", 0)), 2, "成功换装 report 应记录换装后 AP。")
	var unit := _find_battle_unit(snapshot.get("battle", {}), String(report.get("unit_id", "")))
	var equipped := _find_equipped_entry(unit.get("equipment", []), "head")
	_assert_eq(String(equipped.get("item_id", "")), "leather_cap", "成功换装后单位 battle-local 装备快照应显示 head 皮革护帽。")
	_assert_eq(_count_battle_backpack_item(snapshot, "leather_cap"), 0, "成功换装后 battle-local 背包中不应残留该装备。")
	_assert_true(text_snapshot.contains("report=change_equipment | ok=true"), "文本快照应渲染成功换装 report。")
	_assert_true(text_snapshot.contains("head:leather_cap"), "文本快照应渲染单位 battle-local 装备。")
	_assert_true(text_snapshot.contains("backpack_used_slots="), "文本快照应渲染 battle-local 背包摘要。")


func _assert_battle_equip_ap_failure(snapshot: Dictionary, text_snapshot: String) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(not bool(report.get("ok", true)), "AP 不足时 change_equipment report 应标记失败。")
	_assert_eq(String(report.get("error_code", "")), "ap_insufficient", "AP 不足时 report 应暴露稳定错误码。")
	_assert_eq(int(report.get("ap_after", -1)), 1, "AP 不足失败时不应扣 AP。")
	_assert_eq(_count_battle_backpack_item(snapshot, "bronze_sword"), 1, "AP 不足失败时装备实例应留在 battle-local 背包。")
	_assert_true(text_snapshot.contains("error=ap_insufficient"), "文本快照应渲染 AP 不足失败原因。")


func _assert_battle_equip_self_only_failure(snapshot: Dictionary, text_snapshot: String) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(not bool(report.get("ok", true)), "指定其他目标时 change_equipment report 应标记失败。")
	_assert_eq(String(report.get("error_code", "")), "target_not_self", "指定其他目标时应暴露 self-only 错误码。")
	_assert_eq(int(report.get("ap_after", -1)), 2, "self-only 失败时不应扣 AP。")
	_assert_eq(_count_battle_backpack_item(snapshot, "bronze_sword"), 1, "self-only 失败时装备实例应留在 battle-local 背包。")
	_assert_true(text_snapshot.contains("只能为当前行动单位自己换装"), "文本快照应保留 self-only 失败文案。")


func _assert_battle_equip_requirement_failure(snapshot: Dictionary, text_snapshot: String) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(not bool(report.get("ok", true)), "装备需求失败时 change_equipment report 应标记失败。")
	_assert_eq(String(report.get("error_code", "")), "item_not_equippable", "装备需求失败应只暴露泛化错误码。")
	_assert_true(not report.has("blockers"), "装备需求失败不应在 report 中暴露隐藏 blocker。")
	_assert_eq(int(report.get("ap_after", -1)), 2, "装备需求失败时不应扣 AP。")
	_assert_eq(_count_battle_backpack_item(snapshot, String(RESTRICTED_TEST_HELM_ID)), 1, "装备需求失败时实例应留在 battle-local 背包。")
	var hud_entry := _find_battle_hud_backpack_entry(snapshot, String(RESTRICTED_TEST_HELM_ID))
	_assert_true(not hud_entry.is_empty(), "HUD 快照应包含需求受限装备。")
	_assert_true(not bool(hud_entry.get("can_equip", true)), "HUD 应把需求不满足的装备标记为不可装备。")
	_assert_true(
		String(hud_entry.get("disabled_reason", "")).contains("当前无法装备"),
		"HUD 禁用原因应来自 runtime preview 的泛化文案。 entry=%s" % [str(hud_entry)]
	)
	_assert_true(text_snapshot.contains("error=item_not_equippable"), "文本快照应渲染泛化装备失败原因。")
	_assert_true(not text_snapshot.contains("body_size_too_small"), "文本快照不应泄露体型 blocker。")
	_assert_true(not text_snapshot.contains("missing_profession"), "文本快照不应泄露职业 blocker。")
	_assert_true(not text_snapshot.contains("体型过小"), "文本快照不应泄露体型要求。")


func _assert_battle_equip_string_key_only_item_not_found(snapshot: Dictionary, text_snapshot: String) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(not bool(report.get("ok", true)), "String-key-only 装备定义不应允许战斗换装。")
	_assert_eq(String(report.get("error_code", "")), "item_not_found", "String-key-only 装备定义不应被恢复为正式 item_def。")
	_assert_eq(int(report.get("ap_after", -1)), 2, "String-key-only 装备定义失败时不应扣 AP。")
	_assert_eq(
		_count_battle_backpack_item(snapshot, String(STRING_KEY_ONLY_TEST_HELM_ID)),
		1,
		"String-key-only 装备定义失败后实例应留在 battle-local 背包。"
	)
	var unit := _find_battle_unit(snapshot.get("battle", {}), String(report.get("unit_id", "")))
	_assert_true(_find_equipped_entry(unit.get("equipment", []), "head").is_empty(), "String-key-only 装备定义失败后 head 槽应保持清空。")
	_assert_true(text_snapshot.contains("error=item_not_found"), "文本快照应渲染 String-key-only 装备定义缺失原因。")


func _assert_battle_duplicate_item_only_requires_instance(result) -> void:
	_assert_true(String(result.message).contains("请指定 instance_id"), "同 item_id 多 battle-local 实例的 headless 便利命令应要求 instance_id。 message=%s" % result.message)
	_assert_eq(_count_battle_backpack_item(result.snapshot, String(DUPLICATE_TEST_CHARM_ID)), 2, "item_id-only 失败后两个重复实例都应留在 battle-local 背包。")


func _assert_battle_duplicate_explicit_equip(snapshot: Dictionary) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(bool(report.get("ok", false)), "指定 duplicate rare instance_id 的战斗装备应成功。")
	_assert_eq(String(report.get("item_id", "")), String(DUPLICATE_TEST_CHARM_ID), "duplicate explicit equip report 应记录测试饰品。")
	_assert_eq(String(report.get("instance_id", "")), String(DUPLICATE_TEST_CHARM_RARE_INSTANCE_ID), "duplicate explicit equip report 应记录指定实例。")
	_assert_eq(_count_battle_backpack_item(snapshot, String(DUPLICATE_TEST_CHARM_ID)), 1, "装备指定实例后 battle-local 背包中应只剩另一个重复实例。")
	var unit := _find_battle_unit(snapshot.get("battle", {}), String(report.get("unit_id", "")))
	var equipped := _find_equipped_entry(unit.get("equipment", []), "necklace")
	_assert_eq(String(equipped.get("instance_id", "")), String(DUPLICATE_TEST_CHARM_RARE_INSTANCE_ID), "battle-local 饰品槽应写入指定 rare instance_id。")


func _assert_battle_duplicate_unequip_round_trip(snapshot: Dictionary, runner) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(bool(report.get("ok", false)), "指定 duplicate rare instance_id 的战斗卸装应成功。")
	_assert_eq(String(report.get("operation", "")), "unequip", "duplicate round-trip report 应来自卸装。")
	_assert_eq(String(report.get("instance_id", "")), String(DUPLICATE_TEST_CHARM_RARE_INSTANCE_ID), "duplicate unequip report 应记录指定实例。")
	_assert_eq(_count_battle_backpack_item(snapshot, String(DUPLICATE_TEST_CHARM_ID)), 2, "卸装后两个重复实例都应回到 battle-local 背包。")
	var runtime = runner.get_session().get_runtime_facade()
	var battle_state = runtime.get_battle_state() if runtime != null else null
	var returned_instance = _find_battle_backpack_instance(battle_state.get_party_backpack_view() if battle_state != null else null, DUPLICATE_TEST_CHARM_RARE_INSTANCE_ID)
	_assert_true(returned_instance != null, "卸装 round-trip 后应能在 battle-local 背包找到 rare 实例。")
	if returned_instance != null:
		_assert_eq(int(returned_instance.rarity), int(EquipmentInstanceState.RarityTier.RARE), "卸装 round-trip 后 rare 品质应保留。")
		_assert_eq(int(returned_instance.current_durability), 24, "卸装 round-trip 后 rare 耐久应保留。")


func _assert_battle_unequip_backpack_full_failure(snapshot: Dictionary, text_snapshot: String) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(not bool(report.get("ok", true)), "背包满卸装时 change_equipment report 应标记失败。")
	_assert_eq(String(report.get("error_code", "")), "backpack_capacity_exceeded", "背包满卸装应暴露容量错误码。")
	_assert_eq(int(report.get("ap_after", -1)), 2, "背包满卸装失败时不应扣 AP。")
	var unit := _find_battle_unit(snapshot.get("battle", {}), String(report.get("unit_id", "")))
	var equipped := _find_equipped_entry(unit.get("equipment", []), "head")
	_assert_eq(String(equipped.get("item_id", "")), "leather_cap", "背包满卸装失败后 head 装备应保持不变。")
	_assert_eq(_count_battle_backpack_item(snapshot, "leather_cap"), 0, "背包满卸装失败后装备不应进入 battle-local 背包。")
	_assert_true(text_snapshot.contains("error=backpack_capacity_exceeded"), "文本快照应渲染背包满失败原因。")


func _assert_battle_two_handed_weapon_linkage(snapshot: Dictionary) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(bool(report.get("ok", false)), "双手武器战斗换装应成功。")
	_assert_eq(String(report.get("operation", "")), "equip", "双手武器 report 应来自装备操作。")
	_assert_eq(String(report.get("slot_id", "")), "main_hand", "双手武器入口槽应为 main_hand。")
	_assert_eq(String(report.get("item_id", "")), "iron_greatsword", "双手武器 report 应记录铁制大剑。")
	_assert_eq(String(report.get("weapon_item_id", "")), "iron_greatsword", "双手武器投影应使用铁制大剑。")
	_assert_eq(String(report.get("weapon_current_grip", "")), "two_handed", "双手武器投影应切到 two_handed grip。")
	_assert_true(bool(report.get("weapon_uses_two_hands", false)), "双手武器投影应标记占用双手。")

	var unit := _find_battle_unit(snapshot.get("battle", {}), String(report.get("unit_id", "")))
	var main_entry := _find_equipped_entry(unit.get("equipment", []), "main_hand")
	_assert_eq(String(main_entry.get("item_id", "")), "iron_greatsword", "双手武器装备后 battle-local 主手应显示铁制大剑。")
	_assert_true(_array_has_string(main_entry.get("occupied_slot_ids", []), "off_hand"), "双手武器 battle-local 装备 entry 应联动占用副手。")


func _assert_battle_versatile_free_offhand_linkage(snapshot: Dictionary) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(bool(report.get("ok", false)), "versatile 武器战斗换装应成功。")
	_assert_eq(String(report.get("item_id", "")), String(VERSATILE_TEST_WEAPON_ID), "versatile report 应记录测试武器。")
	_assert_eq(String(report.get("weapon_item_id", "")), String(VERSATILE_TEST_WEAPON_ID), "versatile 投影应使用测试武器。")
	_assert_eq(String(report.get("weapon_current_grip", "")), "two_handed", "副手空闲时 versatile 应自动使用双手握法。")
	_assert_true(bool(report.get("weapon_uses_two_hands", false)), "副手空闲时 versatile 应标记双手使用。")

	var unit := _find_battle_unit(snapshot.get("battle", {}), String(report.get("unit_id", "")))
	var main_entry := _find_equipped_entry(unit.get("equipment", []), "main_hand")
	var offhand_entry := _find_equipped_entry(unit.get("equipment", []), "off_hand")
	_assert_eq(String(main_entry.get("item_id", "")), String(VERSATILE_TEST_WEAPON_ID), "versatile 装备后 battle-local 主手应显示测试武器。")
	_assert_true(offhand_entry.is_empty(), "副手空闲时 versatile 不应写入独立 off_hand 装备 entry。")


func _assert_battle_offhand_forces_versatile_one_handed(snapshot: Dictionary) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(bool(report.get("ok", false)), "副手装备战斗换装应成功。")
	_assert_eq(String(report.get("slot_id", "")), "off_hand", "副手装备 report 应记录 off_hand 槽。")
	_assert_eq(String(report.get("item_id", "")), String(OFFHAND_TEST_ITEM_ID), "副手装备 report 应记录测试副手物品。")
	_assert_eq(String(report.get("weapon_item_id", "")), String(VERSATILE_TEST_WEAPON_ID), "装备副手后主手武器投影仍应来自 versatile 武器。")
	_assert_eq(String(report.get("weapon_current_grip", "")), "one_handed", "副手被占用时 versatile 应自动回到单手握法。")
	_assert_true(not bool(report.get("weapon_uses_two_hands", true)), "副手被占用时 versatile 不应继续标记双手使用。")

	var unit := _find_battle_unit(snapshot.get("battle", {}), String(report.get("unit_id", "")))
	var main_entry := _find_equipped_entry(unit.get("equipment", []), "main_hand")
	var offhand_entry := _find_equipped_entry(unit.get("equipment", []), "off_hand")
	_assert_eq(String(main_entry.get("item_id", "")), String(VERSATILE_TEST_WEAPON_ID), "副手装备后 battle-local 主手应保留 versatile 武器。")
	_assert_eq(String(offhand_entry.get("item_id", "")), String(OFFHAND_TEST_ITEM_ID), "副手装备后 battle-local 副手应显示测试副手物品。")


func _assert_party_equipment_not_updated_during_battle(snapshot: Dictionary, member_id: String) -> void:
	var member := _find_party_member(snapshot.get("party", {}).get("members", []), member_id)
	_assert_true(not member.is_empty(), "战斗中 party 快照应包含当前行动成员。")
	var party_main := _find_equipped_entry(member.get("equipment", []), "main_hand")
	var party_offhand := _find_equipped_entry(member.get("equipment", []), "off_hand")
	_assert_true(String(party_main.get("item_id", "")) != String(VERSATILE_TEST_WEAPON_ID), "战斗中换装不应直接写入 party 主手。")
	_assert_true(String(party_offhand.get("item_id", "")) != String(OFFHAND_TEST_ITEM_ID), "战斗中换装不应直接写入 party 副手。")

	var report := _find_latest_change_equipment_report(snapshot)
	var unit := _find_battle_unit(snapshot.get("battle", {}), String(report.get("unit_id", "")))
	_assert_eq(String(_find_equipped_entry(unit.get("equipment", []), "main_hand").get("item_id", "")), String(VERSATILE_TEST_WEAPON_ID), "同一快照中 battle-local 主手应已经换成 versatile 武器。")
	_assert_eq(String(_find_equipped_entry(unit.get("equipment", []), "off_hand").get("item_id", "")), String(OFFHAND_TEST_ITEM_ID), "同一快照中 battle-local 副手应已经换成测试副手物品。")


func _assert_party_equipment_written_back_after_battle(snapshot: Dictionary, member_id: String) -> void:
	_assert_true(not bool(snapshot.get("battle", {}).get("active", false)), "战斗结算后 battle 应退出 active 状态。")
	var member := _find_party_member(snapshot.get("party", {}).get("members", []), member_id)
	_assert_true(not member.is_empty(), "战后 party 快照应包含当前行动成员。")
	var main_entry := _find_equipped_entry(member.get("equipment", []), "main_hand")
	var offhand_entry := _find_equipped_entry(member.get("equipment", []), "off_hand")
	var head_entry := _find_equipped_entry(member.get("equipment", []), "head")
	var body_entry := _find_equipped_entry(member.get("equipment", []), "body")
	_assert_eq(String(main_entry.get("item_id", "")), String(VERSATILE_TEST_WEAPON_ID), "战后 party 主手才应写回 battle-local versatile 武器。")
	_assert_eq(String(offhand_entry.get("item_id", "")), String(OFFHAND_TEST_ITEM_ID), "战后 party 副手才应写回 battle-local 副手物品。")
	_assert_eq(String(head_entry.get("item_id", "")), "leather_cap", "战后 party 头部应写回 battle-local 皮革护帽。")
	_assert_true(body_entry.is_empty(), "卸装后战后 party body 槽应保持清空。")


func _assert_battle_body_armor_equipped_without_hp_bonus(snapshot: Dictionary) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(bool(report.get("ok", false)), "身体护甲换装应成功。")
	_assert_eq(String(report.get("item_id", "")), "leather_jerkin", "身体护甲换装 report 应记录皮革短甲。")
	_assert_eq(int(report.get("hp_max_after", 0)), int(report.get("hp_max_before", -1)), "皮革短甲不应提高 HP 上限。")


func _assert_battle_unequip_body_armor_turn_end_without_hp_clamp(snapshot: Dictionary, text_snapshot: String) -> void:
	var battle_snapshot: Dictionary = snapshot.get("battle", {})
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(bool(report.get("ok", false)), "身体护甲卸装应成功。")
	_assert_eq(String(report.get("operation", "")), "unequip", "卸装 report 应来自卸装。")
	_assert_true(not bool(report.get("hp_clamped", false)), "卸下无生命加成护甲时不应标记 hp_clamped。")
	_assert_eq(int(report.get("hp_before", 0)), int(report.get("hp_after", -1)), "卸下无生命加成护甲时当前 HP 不应变化。")
	_assert_eq(String(battle_snapshot.get("phase", "")), "timeline_running", "AP 归零后战斗阶段应回到 timeline_running。")
	_assert_eq(String(battle_snapshot.get("active_unit_id", "")), "", "AP 归零后应清空 active_unit_id。")
	var unit := _find_battle_unit(battle_snapshot, String(report.get("unit_id", "")))
	_assert_true(_find_equipped_entry(unit.get("equipment", []), "body").is_empty(), "身体护甲卸装后 body 槽应清空。")
	_assert_eq(_count_battle_backpack_item(snapshot, "leather_jerkin"), 1, "身体护甲卸装后应回到 battle-local 背包。")
	_assert_true(not text_snapshot.contains("hp_clamped=true"), "文本快照不应渲染不存在的 HP clamp。")
	_assert_true(text_snapshot.contains("active_unit_id="), "文本快照应渲染行动结束后的 active_unit_id。")


func _find_latest_change_equipment_report(snapshot: Dictionary) -> Dictionary:
	var reports: Array = snapshot.get("battle", {}).get("report_entries", [])
	for index in range(reports.size() - 1, -1, -1):
		var report_variant = reports[index]
		if report_variant is not Dictionary:
			continue
		var report: Dictionary = report_variant
		if String(report.get("type", "")) == "change_equipment":
			return report
	return {}


func _find_battle_unit(battle_snapshot: Dictionary, unit_id: String) -> Dictionary:
	var units: Array = battle_snapshot.get("units", [])
	for unit_variant in units:
		if unit_variant is not Dictionary:
			continue
		var unit: Dictionary = unit_variant
		if String(unit.get("unit_id", "")) == unit_id:
			return unit
	return {}


func _find_battle_hud_backpack_entry(snapshot: Dictionary, item_id: String) -> Dictionary:
	var entries: Array = snapshot.get("battle", {}).get("hud", {}).get("equipment_panel", {}).get("backpack_entries", [])
	for entry_variant in entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("item_id", "")) == item_id:
			return entry
	return {}


func _find_other_battle_unit_id(snapshot: Dictionary) -> String:
	var battle_snapshot: Dictionary = snapshot.get("battle", {})
	var active_unit_id := String(battle_snapshot.get("active_unit_id", ""))
	var units: Array = battle_snapshot.get("units", [])
	for unit_variant in units:
		if unit_variant is not Dictionary:
			continue
		var unit: Dictionary = unit_variant
		var unit_id := String(unit.get("unit_id", ""))
		if not unit_id.is_empty() and unit_id != active_unit_id:
			return unit_id
	return "not_current_unit"


func _find_party_member(entries: Array, member_id: String) -> Dictionary:
	for entry_variant in entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("member_id", "")) == member_id:
			return entry
	return {}


func _find_equipped_entry(entries: Array, slot_id: String) -> Dictionary:
	for entry_variant in entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("slot_id", "")) == slot_id:
			return entry
	return {}


func _array_has_string(values, expected: String) -> bool:
	if values is not Array:
		return false
	for value in values:
		if String(value) == expected:
			return true
	return false


func _count_battle_backpack_item(snapshot: Dictionary, item_id: String) -> int:
	var backpack: Dictionary = snapshot.get("battle", {}).get("party_backpack", {})
	var total := 0
	var stacks: Array = backpack.get("stacks", [])
	for stack_variant in stacks:
		if stack_variant is not Dictionary:
			continue
		var stack: Dictionary = stack_variant
		if String(stack.get("item_id", "")) == item_id:
			total += int(stack.get("quantity", 0))
	var instances: Array = backpack.get("equipment_instances", [])
	for instance_variant in instances:
		if instance_variant is not Dictionary:
			continue
		var instance: Dictionary = instance_variant
		if String(instance.get("item_id", "")) == item_id:
			total += 1
	return total


func _find_battle_backpack_instance(backpack_view, instance_id: StringName):
	if backpack_view == null:
		return null
	for instance in backpack_view.equipment_instances:
		if instance == null:
			continue
		if String(instance.instance_id) == String(instance_id):
			return instance
	return null


func _run_command(runner, command_text: String):
	var result = await runner.execute_line(command_text)
	if result.skipped:
		return result
	if not result.ok:
		print(result.render())
		_test.fail("命令失败：%s | %s" % [command_text, result.message])
	return result


func _run_command_expect_fail(runner, command_text: String):
	var result = await runner.execute_line(command_text)
	if result.skipped:
		_test.fail("命令被跳过，无法验证失败：%s" % command_text)
		return result
	if result.ok:
		print(result.render())
		_test.fail("命令应失败但成功：%s" % command_text)
	return result


func _assert_true(value: bool, message: String) -> void:
	if value:
		return
	_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _finish() -> void:
	if _failures.is_empty():
		print("Battle equipment text command regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle equipment text command regression: FAIL (%d)" % _failures.size())
	quit(1)
