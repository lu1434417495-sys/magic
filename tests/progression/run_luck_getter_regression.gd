extends SceneTree

const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_unit_base_attributes_luck_getters_cover_boundaries()
	_test_party_member_state_luck_getters_delegate_and_stay_null_safe()
	_test_from_dict_missing_luck_keys_fall_back_to_zero()

	if _failures.is_empty():
		print("Luck getter regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Luck getter regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_unit_base_attributes_luck_getters_cover_boundaries() -> void:
	_assert_luck_case(
		"默认值",
		_build_attributes(),
		0,
		0,
		0,
		0,
		0
	)
	_assert_luck_case(
		"低端软封顶",
		_build_attributes(-9, 0),
		-9,
		0,
		-6,
		0,
		-6
	)
	_assert_luck_case(
		"faith 奇数向下取整",
		_build_attributes(0, 1),
		0,
		1,
		1,
		0,
		1
	)
	_assert_luck_case(
		"高端软封顶",
		_build_attributes(2, 5),
		2,
		5,
		7,
		4,
		5
	)
	_assert_luck_case(
		"战斗幸运分数上限",
		_build_attributes(4, 4),
		4,
		4,
		7,
		4,
		5
	)
	_assert_luck_case(
		"负 faith 不参与战斗幸运加成",
		_build_attributes(2, -3),
		2,
		-3,
		-1,
		2,
		-1
	)


func _test_party_member_state_luck_getters_delegate_and_stay_null_safe() -> void:
	var member_state := PartyMemberState.new()
	member_state.progression.unit_base_attributes = _build_attributes(2, 5)
	_assert_member_luck_case(
		"PartyMemberState 读取封装",
		member_state,
		2,
		5,
		7,
		4,
		5
	)

	member_state.progression = null
	_assert_member_luck_case(
		"PartyMemberState 缺少 progression 时回退默认值",
		member_state,
		0,
		0,
		0,
		0,
		0
	)


func _test_from_dict_missing_luck_keys_fall_back_to_zero() -> void:
	var attributes := UnitBaseAttributes.from_dict({
		"strength": 3,
		"custom_stats": {},
	})
	_assert_luck_case(
		"UnitBaseAttributes.from_dict 缺少 luck 键",
		attributes,
		0,
		0,
		0,
		0,
		0
	)

	var member_state = PartyMemberState.from_dict({
		"member_id": "hero",
		"display_name": "Hero",
		"faction_id": "player",
		"portrait_id": "",
		"progression": {
			"version": 1,
			"unit_id": "hero",
			"display_name": "Hero",
			"character_level": 1,
			"unit_base_attributes": {
				"strength": 3,
				"agility": 2,
				"constitution": 3,
				"perception": 2,
				"intelligence": 1,
				"willpower": 1,
			},
			"reputation_state": {},
			"skills": {},
			"professions": {},
			"known_knowledge_ids": [],
			"active_core_skill_ids": [],
			"achievement_progress": {},
			"pending_profession_choices": [],
			"blocked_relearn_skill_ids": [],
			"merged_skill_source_map": {},
		},
		"equipment_state": {
			"equipped_slots": {},
		},
		"control_mode": "manual",
		"current_hp": 18,
		"current_mp": 6,
		"is_dead": false,
		"body_size": 1,
	})
	_assert_true(member_state != null, "缺少 luck 键的旧成员存档 shape 应仍能恢复。")
	if member_state == null:
		return
	_assert_member_luck_case(
		"PartyMemberState.from_dict 缺少 luck 键",
		member_state,
		0,
		0,
		0,
		0,
		0
	)


func _build_attributes(hidden_luck_at_birth: int = 0, faith_luck_bonus: int = 0) -> UnitBaseAttributes:
	var attributes := UnitBaseAttributes.new()
	attributes.custom_stats[UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH] = hidden_luck_at_birth
	attributes.custom_stats[UnitBaseAttributes.FAITH_LUCK_BONUS] = faith_luck_bonus
	return attributes


func _assert_luck_case(
	label: String,
	attributes: UnitBaseAttributes,
	expected_hidden_luck: int,
	expected_faith_luck: int,
	expected_effective_luck: int,
	expected_combat_luck_score: int,
	expected_drop_luck: int
) -> void:
	_assert_true(attributes != null, "%s：UnitBaseAttributes 不应为 null。" % label)
	if attributes == null:
		return
	_assert_eq(attributes.get_hidden_luck_at_birth(), expected_hidden_luck, "%s：hidden_luck_at_birth 读取结果错误。" % label)
	_assert_eq(attributes.get_faith_luck_bonus(), expected_faith_luck, "%s：faith_luck_bonus 读取结果错误。" % label)
	_assert_eq(attributes.get_effective_luck(), expected_effective_luck, "%s：effective_luck 计算结果错误。" % label)
	_assert_eq(attributes.get_combat_luck_score(), expected_combat_luck_score, "%s：combat_luck_score 计算结果错误。" % label)
	_assert_eq(attributes.get_drop_luck(), expected_drop_luck, "%s：drop_luck 计算结果错误。" % label)


func _assert_member_luck_case(
	label: String,
	member_state: PartyMemberState,
	expected_hidden_luck: int,
	expected_faith_luck: int,
	expected_effective_luck: int,
	expected_combat_luck_score: int,
	expected_drop_luck: int
) -> void:
	_assert_true(member_state != null, "%s：PartyMemberState 不应为 null。" % label)
	if member_state == null:
		return
	_assert_eq(member_state.get_hidden_luck_at_birth(), expected_hidden_luck, "%s：hidden_luck_at_birth 读取结果错误。" % label)
	_assert_eq(member_state.get_faith_luck_bonus(), expected_faith_luck, "%s：faith_luck_bonus 读取结果错误。" % label)
	_assert_eq(member_state.get_effective_luck(), expected_effective_luck, "%s：effective_luck 计算结果错误。" % label)
	_assert_eq(member_state.get_combat_luck_score(), expected_combat_luck_score, "%s：combat_luck_score 计算结果错误。" % label)
	_assert_eq(member_state.get_drop_luck(), expected_drop_luck, "%s：drop_luck 计算结果错误。" % label)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
