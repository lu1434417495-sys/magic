extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleTestFixture = preload("res://tests/shared/battle_test_fixture.gd")
const BattleRuntimeTestHelpers = preload("res://tests/shared/battle_runtime_test_helpers.gd")

const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const EquipmentRequirement = preload("res://scripts/player/equipment/equipment_requirement.gd")
const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")
const UnitProfessionProgress = preload("res://scripts/player/progression/unit_profession_progress.gd")
const UnitProgress = preload("res://scripts/player/progression/unit_progress.gd")

const RESTRICTED_HELM_ID: StringName = &"requirement_test_restricted_helm"
const RESTRICTED_HELM_INSTANCE_ID: StringName = &"requirement_test_restricted_helm_001"
const DUPLICATE_HELM_ID: StringName = &"duplicate_test_helm"
const DUPLICATE_HELM_COMMON_INSTANCE_ID: StringName = &"duplicate_test_helm_common_001"
const DUPLICATE_HELM_RARE_INSTANCE_ID: StringName = &"duplicate_test_helm_rare_001"

var _test := TestRunner.new()
var _battle_fixture := BattleTestFixture.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_battle_change_equipment_enforces_item_requirement()
	_test_duplicate_same_item_battle_equip_and_unequip_preserves_instance()
	if _failures.is_empty():
		print("Battle change equipment requirement regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle change equipment requirement regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_battle_change_equipment_enforces_item_requirement() -> void:
	var item_defs := {
		RESTRICTED_HELM_ID: _build_restricted_helm_item(RESTRICTED_HELM_ID),
	}
	var party := _build_party(&"requirement_hero", 2)
	var member: PartyMemberState = party.get_member_state(&"requirement_hero")
	var gateway := CharacterManagementModule.new()
	gateway.setup(party, {}, {}, {}, item_defs)

	var runtime := BattleRuntimeModule.new()
	runtime.setup(gateway, {}, {}, {}, null, null, item_defs)
	BattleRuntimeTestHelpers.configure_fixed_combat(runtime)
	var state := _build_state()
	var unit := _build_unit(&"requirement_hero", Vector2i(0, 0), 2)
	unit.source_member_id = &"requirement_hero"
	unit.set_equipment_view(member.equipment_state)
	var enemy := _build_unit(&"requirement_enemy", Vector2i(2, 0), 0)
	enemy.faction_id = &"enemy"
	state.units = {
		unit.unit_id: unit,
		enemy.unit_id: enemy,
	}
	state.ally_unit_ids = [unit.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = unit.unit_id
	state.active_unit_id = unit.unit_id
	state.get_party_backpack_view().equipment_instances = [
		_make_equipment_instance(RESTRICTED_HELM_INSTANCE_ID, RESTRICTED_HELM_ID),
	]
	_assert_true(runtime._grid_service.place_unit(state, unit, unit.coord, true), "需求装备测试单位应能放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy, enemy.coord, true), "需求装备测试敌方应能放入战场。")
	runtime._state = state

	var command := _build_equip_command(unit.unit_id, &"head", RESTRICTED_HELM_INSTANCE_ID, RESTRICTED_HELM_ID)
	var preview := runtime.preview_command(command)
	_assert_true(preview != null and not preview.allowed, "需求不满足时战斗换装 preview 应失败。")
	_assert_true(
		preview != null and preview.log_lines.any(func(line): return String(line).contains("当前无法装备")),
		"需求不满足时 preview 应显示泛化失败原因。 log=%s" % [str(preview.log_lines if preview != null else [])]
	)
	_assert_true(
		preview != null
			and not str(preview.log_lines).contains("missing_profession")
			and not str(preview.log_lines).contains("body_size_too_small")
			and not str(preview.log_lines).contains("缺少所需职业")
			and not str(preview.log_lines).contains("体型过小"),
		"需求不满足时 preview 不应泄露隐藏需求。 log=%s" % [str(preview.log_lines if preview != null else [])]
	)

	var backpack_before := _backpack_instance_id_signature(state.get_party_backpack_view())
	var blocked_batch := runtime.issue_command(command)
	var blocked_report := _find_change_equipment_report(blocked_batch.report_entries)
	_assert_eq(String(blocked_report.get("error_code", "")), "item_not_equippable", "需求失败应只暴露泛化错误码。")
	_assert_true(not blocked_report.has("blockers"), "需求失败 report 不应透出隐藏 blocker 列表。")
	_assert_eq(unit.current_ap, 2, "需求失败不应扣 AP。")
	_assert_eq(String(unit.get_equipment_view().get_equipped_instance_id(&"head")), "", "需求失败不应写入 battle-local 装备 view。")
	_assert_eq(_backpack_instance_id_signature(state.get_party_backpack_view()), backpack_before, "需求失败不应移动背包实例。")

	member.body_size = 3
	var profession_progress := UnitProfessionProgress.new()
	profession_progress.profession_id = &"helmet_training"
	member.progression.set_profession_progress(profession_progress)
	var allowed_preview := runtime.preview_command(command)
	_assert_true(
		allowed_preview != null and allowed_preview.allowed,
		"成员满足需求后同一 battle-local 装备 preview 应通过。 log=%s" % [str(allowed_preview.log_lines if allowed_preview != null else [])]
	)
	var success_batch := runtime.issue_command(command)
	var success_report := _find_change_equipment_report(success_batch.report_entries)
	_assert_true(bool(success_report.get("ok", false)), "成员满足需求后换装应成功。 report=%s" % [str(success_report)])
	_assert_eq(unit.current_ap, 0, "需求满足后成功换装应扣 2 AP。")
	_assert_eq(
		String(unit.get_equipment_view().get_equipped_instance_id(&"head")),
		String(RESTRICTED_HELM_INSTANCE_ID),
		"需求满足后应写入 battle-local 装备 view。"
	)
	_assert_eq(_backpack_instance_id_signature(state.get_party_backpack_view()), [], "需求满足后应从 battle-local 背包移除实例。")


func _test_duplicate_same_item_battle_equip_and_unequip_preserves_instance() -> void:
	var item_defs := {
		DUPLICATE_HELM_ID: _build_plain_helm_item(DUPLICATE_HELM_ID),
	}
	var party := _build_party(&"duplicate_hero", 2)
	var member: PartyMemberState = party.get_member_state(&"duplicate_hero")
	var gateway := CharacterManagementModule.new()
	gateway.setup(party, {}, {}, {}, item_defs)

	var runtime := BattleRuntimeModule.new()
	runtime.setup(gateway, {}, {}, {}, null, null, item_defs)
	BattleRuntimeTestHelpers.configure_fixed_combat(runtime)
	var state := _build_state()
	state.battle_id = &"change_equipment_duplicate_regression"
	var unit := _build_unit(&"duplicate_hero", Vector2i(0, 0), 4)
	unit.source_member_id = &"duplicate_hero"
	unit.set_equipment_view(member.equipment_state)
	var enemy := _build_unit(&"duplicate_enemy", Vector2i(2, 0), 0)
	enemy.faction_id = &"enemy"
	state.units = {
		unit.unit_id: unit,
		enemy.unit_id: enemy,
	}
	state.ally_unit_ids = [unit.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = unit.unit_id
	var common_instance = _make_equipment_instance(DUPLICATE_HELM_COMMON_INSTANCE_ID, DUPLICATE_HELM_ID)
	common_instance.rarity = EquipmentInstanceState.RarityTier.COMMON
	common_instance.current_durability = 12
	var rare_instance = _make_equipment_instance(DUPLICATE_HELM_RARE_INSTANCE_ID, DUPLICATE_HELM_ID)
	rare_instance.rarity = EquipmentInstanceState.RarityTier.RARE
	rare_instance.current_durability = 29
	state.get_party_backpack_view().equipment_instances = [common_instance, rare_instance]
	_assert_true(runtime._grid_service.place_unit(state, unit, unit.coord, true), "重复实例测试单位应能放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy, enemy.coord, true), "重复实例测试敌方应能放入战场。")
	runtime._state = state

	var missing_instance_command := _build_equip_command(unit.unit_id, &"head", &"", DUPLICATE_HELM_ID)
	var missing_instance_batch := runtime.issue_command(missing_instance_command)
	var missing_report := _find_change_equipment_report(missing_instance_batch.report_entries)
	_assert_eq(String(missing_report.get("error_code", "")), "equipment_instance_required", "战斗换装正式命令缺少 instance_id 应拒绝。")
	_assert_eq(_backpack_instance_id_signature(state.get_party_backpack_view()), [String(DUPLICATE_HELM_COMMON_INSTANCE_ID), String(DUPLICATE_HELM_RARE_INSTANCE_ID)], "缺少 instance_id 失败后两个重复实例都应留在背包。")

	var equip_command := _build_equip_command(unit.unit_id, &"head", DUPLICATE_HELM_RARE_INSTANCE_ID, DUPLICATE_HELM_ID)
	var equip_batch := runtime.issue_command(equip_command)
	var equip_report := _find_change_equipment_report(equip_batch.report_entries)
	_assert_true(bool(equip_report.get("ok", false)), "指定 rare instance_id 的 battle-local 装备应成功。 report=%s" % [str(equip_report)])
	_assert_eq(String(unit.get_equipment_view().get_equipped_instance_id(&"head")), String(DUPLICATE_HELM_RARE_INSTANCE_ID), "battle-local 装备位应写入指定 rare instance_id。")
	_assert_eq(_backpack_instance_id_signature(state.get_party_backpack_view()), [String(DUPLICATE_HELM_COMMON_INSTANCE_ID)], "装备 rare 后 common 实例应留在背包。")
	var equipped_instance = unit.get_equipment_view().get_equipped_instance(&"head")
	_assert_true(equipped_instance != null, "battle-local 装备位应保留完整 rare 实例。")
	if equipped_instance != null:
		_assert_eq(int(equipped_instance.rarity), int(EquipmentInstanceState.RarityTier.RARE), "battle-local 装备位应保留 rare 品质。")
		_assert_eq(int(equipped_instance.current_durability), 29, "battle-local 装备位应保留 rare 耐久。")

	unit.current_ap = 2
	var unequip_command := _build_unequip_command(unit.unit_id, &"head", DUPLICATE_HELM_RARE_INSTANCE_ID)
	var unequip_batch := runtime.issue_command(unequip_command)
	var unequip_report := _find_change_equipment_report(unequip_batch.report_entries)
	_assert_true(bool(unequip_report.get("ok", false)), "指定 rare instance_id 的 battle-local 卸装应成功。 report=%s" % [str(unequip_report)])
	_assert_eq(String(unit.get_equipment_view().get_equipped_instance_id(&"head")), "", "卸装后 head 槽应清空。")
	_assert_eq(_backpack_instance_id_signature(state.get_party_backpack_view()), [String(DUPLICATE_HELM_COMMON_INSTANCE_ID), String(DUPLICATE_HELM_RARE_INSTANCE_ID)], "卸装后 common 与 rare 实例都应在背包。")
	var returned_instance = _find_backpack_instance(state.get_party_backpack_view(), DUPLICATE_HELM_RARE_INSTANCE_ID)
	_assert_true(returned_instance != null, "卸回背包后应能按 instance_id 找到 rare 实例。")
	if returned_instance != null:
		_assert_eq(int(returned_instance.rarity), int(EquipmentInstanceState.RarityTier.RARE), "卸回背包的 rare 实例应保留品质。")
		_assert_eq(int(returned_instance.current_durability), 29, "卸回背包的 rare 实例应保留耐久。")


func _build_restricted_helm_item(item_id: StringName) -> ItemDef:
	var item_def := ItemDef.new()
	item_def.item_id = item_id
	item_def.display_name = "Requirement Test Helm"
	item_def.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	item_def.equipment_type_id = ItemDef.EQUIPMENT_TYPE_ARMOR
	item_def.equipment_slot_ids = ["head"]
	item_def.is_stackable = false
	item_def.max_stack = 1
	var requirement := EquipmentRequirement.new()
	requirement.required_profession_ids = ["helmet_training"]
	requirement.min_body_size = 3
	item_def.equip_requirement = requirement
	return item_def


func _build_plain_helm_item(item_id: StringName) -> ItemDef:
	var item_def := ItemDef.new()
	item_def.item_id = item_id
	item_def.display_name = "Duplicate Test Helm"
	item_def.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	item_def.equipment_type_id = ItemDef.EQUIPMENT_TYPE_ARMOR
	item_def.equipment_slot_ids = ["head"]
	item_def.is_stackable = false
	item_def.max_stack = 1
	return item_def


func _build_party(member_id: StringName, body_size: int) -> PartyState:
	var party := PartyState.new()
	var member := PartyMemberState.new()
	member.member_id = member_id
	member.display_name = "Requirement Hero"
	member.body_size = body_size
	member.current_hp = 20
	member.current_mp = 5
	member.progression = UnitProgress.new()
	member.progression.unit_id = member_id
	member.progression.display_name = member.display_name
	var unit_base_attributes := UnitBaseAttributes.new()
	unit_base_attributes.custom_stats[&"storage_space"] = 4
	member.progression.unit_base_attributes = unit_base_attributes
	party.set_member_state(member)
	party.active_member_ids = [member_id]
	party.leader_member_id = member_id
	party.main_character_member_id = member_id
	return party


func _build_state() -> BattleState:
	return _battle_fixture.build_state({
		"battle_id": &"change_equipment_requirement_regression",
		"map_size": Vector2i(3, 1),
		"base_height": 4,
	})


func _build_unit(unit_id: StringName, coord: Vector2i, current_ap: int) -> BattleUnitState:
	return _battle_fixture.build_unit(unit_id, {
		"coord": coord,
		"current_ap": current_ap,
		"current_hp": 20,
	})


func _build_equip_command(
	unit_id: StringName,
	slot_id: StringName,
	instance_id: StringName,
	item_id: StringName
) -> BattleCommand:
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_CHANGE_EQUIPMENT
	command.unit_id = unit_id
	command.target_unit_id = unit_id
	command.equipment_operation = BattleCommand.EQUIPMENT_OPERATION_EQUIP
	command.equipment_slot_id = slot_id
	command.equipment_item_id = item_id
	command.equipment_instance_id = instance_id
	command.equipment_instance = {
		"instance_id": String(instance_id),
		"item_id": String(item_id),
	}
	return command


func _build_unequip_command(
	unit_id: StringName,
	slot_id: StringName,
	instance_id: StringName
) -> BattleCommand:
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_CHANGE_EQUIPMENT
	command.unit_id = unit_id
	command.target_unit_id = unit_id
	command.equipment_operation = BattleCommand.EQUIPMENT_OPERATION_UNEQUIP
	command.equipment_slot_id = slot_id
	command.equipment_instance_id = instance_id
	return command


func _make_equipment_instance(instance_id: StringName, item_id: StringName):
	var instance := EquipmentInstanceState.new()
	instance.instance_id = ProgressionDataUtils.to_string_name(instance_id)
	instance.item_id = ProgressionDataUtils.to_string_name(item_id)
	return instance


func _find_change_equipment_report(report_entries: Array) -> Dictionary:
	for entry_variant in report_entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("type", entry.get("entry_type", ""))) == "change_equipment":
			return entry
	return {}


func _backpack_instance_id_signature(backpack_view) -> Array[String]:
	var result: Array[String] = []
	if backpack_view == null:
		return result
	for instance in backpack_view.equipment_instances:
		if instance == null:
			continue
		result.append(String(instance.instance_id))
	result.sort()
	return result


func _find_backpack_instance(backpack_view, instance_id: StringName):
	if backpack_view == null:
		return null
	for instance in backpack_view.equipment_instances:
		if instance == null:
			continue
		if String(instance.instance_id) == String(instance_id):
			return instance
	return null


func _assert_true(value: bool, message: String) -> void:
	if not value:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
