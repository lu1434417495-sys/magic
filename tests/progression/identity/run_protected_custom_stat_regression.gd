extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const AttributeService = preload("res://scripts/systems/attributes/attribute_service.gd")
const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const PartyWarehouseService = preload("res://scripts/systems/inventory/party_warehouse_service.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")
const UnitProgress = preload("res://scripts/player/progression/unit_progress.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_protected_custom_stat_keys_stay_minimal()
	_test_non_whitelisted_sources_cannot_write_hidden_luck_at_birth()
	_test_pending_reward_flow_rejects_protected_hidden_luck_writes()
	_test_character_creation_and_explicit_story_scripts_can_write_hidden_luck_at_birth()
	_test_unprotected_custom_stats_remain_writable()

	if _failures.is_empty():
		print("Protected custom stat regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Protected custom stat regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_protected_custom_stat_keys_stay_minimal() -> void:
	_assert_true(
		AttributeService.PROTECTED_CUSTOM_STAT_KEYS.has(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH),
		"PROTECTED_CUSTOM_STAT_KEYS 应默认包含 hidden_luck_at_birth。"
	)
	_assert_true(
		not AttributeService.PROTECTED_CUSTOM_STAT_KEYS.has(PartyWarehouseService.STORAGE_SPACE_ATTRIBUTE_ID),
		"PROTECTED_CUSTOM_STAT_KEYS 不应顺手保护其他 custom stat。"
	)


func _test_non_whitelisted_sources_cannot_write_hidden_luck_at_birth() -> void:
	var cases := [
		{
			"label": "成就奖励",
			"source_type": &"achievement",
			"source_id": &"battle_won_first",
		},
		{
			"label": "普通 rank 奖励",
			"source_type": &"profession_rank_reward",
			"source_id": &"warrior_rank_2",
		},
		{
			"label": "道具效果",
			"source_type": &"item_effect",
			"source_id": &"lucky_incense",
		},
	]

	for case in cases:
		var service := _build_attribute_service(2)
		var applied := service.apply_permanent_attribute_change(
			UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH,
			3,
			{
				"source_type": case.get("source_type", &""),
				"source_id": case.get("source_id", &""),
			}
		)
		_assert_true(
			not applied,
			"%s 不应能写入 hidden_luck_at_birth。" % String(case.get("label", "未知来源"))
		)
		_assert_eq(
			service.get_base_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH),
			2,
			"%s 被拒绝后不应改写 hidden_luck_at_birth。" % String(case.get("label", "未知来源"))
		)


func _test_character_creation_and_explicit_story_scripts_can_write_hidden_luck_at_birth() -> void:
	var creation_service := _build_attribute_service(1)
	var creation_applied := creation_service.apply_permanent_attribute_change(
		UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH,
		2,
		{
			"source_type": AttributeService.PROTECTED_CUSTOM_STAT_SOURCE_CHARACTER_CREATION,
			"source_id": &"birth_roll",
		}
	)
	_assert_true(creation_applied, "CharacterCreationService 来源应能写入 hidden_luck_at_birth。")
	_assert_eq(
		creation_service.get_base_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH),
		3,
		"CharacterCreationService 来源应真正累计 hidden_luck_at_birth。"
	)

	var unmarked_story_service := _build_attribute_service(1)
	var unmarked_story_applied := unmarked_story_service.apply_permanent_attribute_change(
		UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH,
		2,
		{
			"source_type": AttributeService.PROTECTED_CUSTOM_STAT_SOURCE_STORY_SCRIPT,
			"source_id": &"chapter_intro",
		}
	)
	_assert_true(not unmarked_story_applied, "未显式标记的剧情脚本不应写入 hidden_luck_at_birth。")
	_assert_eq(
		unmarked_story_service.get_base_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH),
		1,
		"未显式标记的剧情脚本被拒绝后不应改写 hidden_luck_at_birth。"
	)

	var marked_story_service := _build_attribute_service(1)
	var story_source_context := {
		"source_type": AttributeService.PROTECTED_CUSTOM_STAT_SOURCE_STORY_SCRIPT,
		"source_id": &"chapter_intro",
	}
	story_source_context[AttributeService.PROTECTED_CUSTOM_STAT_WRITE_FLAG] = true
	var marked_story_applied := marked_story_service.apply_permanent_attribute_change(
		UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH,
		2,
		story_source_context
	)
	_assert_true(marked_story_applied, "显式标记的剧情脚本应能写入 hidden_luck_at_birth。")
	_assert_eq(
		marked_story_service.get_base_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH),
		3,
		"显式标记的剧情脚本应真正累计 hidden_luck_at_birth。"
	)


func _test_pending_reward_flow_rejects_protected_hidden_luck_writes() -> void:
	var party_state := PartyState.new()
	var member_state := PartyMemberState.new()
	member_state.member_id = &"hero"
	member_state.display_name = "Hero"
	member_state.progression = UnitProgress.new()
	member_state.progression.unit_id = &"hero"
	member_state.progression.display_name = "Hero"
	member_state.progression.unit_base_attributes.set_attribute_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH, 2)
	party_state.set_member_state(member_state)

	var manager := CharacterManagementModule.new()
	manager.setup(party_state, {}, {}, {})

	var reward = manager.build_pending_character_reward(
		&"hero",
		&"protected_hidden_luck_reward",
		&"achievement",
		&"battle_won_first",
		"首战成就",
		[
			{
				"entry_type": "attribute_delta",
				"target_id": String(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH),
				"amount": 3,
				"reason_text": "测试保护写入",
			},
		],
		"成就奖励"
	)
	_assert_true(reward != null, "测试前置：应能构造 attribute_delta 奖励。")
	if reward == null:
		return

	var delta = manager.apply_pending_character_reward(reward)
	_assert_true(delta.attribute_changes.is_empty(), "受保护 custom stat 被拒绝时不应记录 attribute delta。")
	_assert_eq(
		member_state.progression.unit_base_attributes.get_attribute_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH),
		2,
		"受保护 custom stat 通过成就奖励链路写入时应保持原值。"
	)


func _test_unprotected_custom_stats_remain_writable() -> void:
	var service := _build_attribute_service(1)
	var applied := service.apply_permanent_attribute_change(
		PartyWarehouseService.STORAGE_SPACE_ATTRIBUTE_ID,
		2,
		{
			"source_type": &"achievement",
			"source_id": &"pack_master",
		}
	)
	_assert_true(applied, "未受保护的 custom stat 仍应允许通过正式写入点更新。")
	_assert_eq(
		service.get_base_value(PartyWarehouseService.STORAGE_SPACE_ATTRIBUTE_ID),
		3,
		"未受保护的 custom stat 应正常累计。"
	)


func _build_attribute_service(hidden_luck_at_birth: int, storage_space: int = 1) -> AttributeService:
	var progression := UnitProgress.new()
	progression.unit_id = &"hero"
	progression.display_name = "Hero"
	progression.unit_base_attributes.set_attribute_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH, hidden_luck_at_birth)
	progression.unit_base_attributes.set_attribute_value(PartyWarehouseService.STORAGE_SPACE_ATTRIBUTE_ID, storage_space)

	var service := AttributeService.new()
	service.setup(progression)
	return service


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
