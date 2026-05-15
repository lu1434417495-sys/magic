extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const AttributeService = preload("res://scripts/systems/attributes/attribute_service.gd")
const AscensionDef = preload("res://scripts/player/progression/ascension_def.gd")
const AscensionStageDef = preload("res://scripts/player/progression/ascension_stage_def.gd")
const BodySizeRules = preload("res://scripts/systems/progression/body_size_rules.gd")
const CharacterCreationService = preload("res://scripts/systems/progression/character_creation_service.gd")
const BloodlineDef = preload("res://scripts/player/progression/bloodline_def.gd")
const BloodlineStageDef = preload("res://scripts/player/progression/bloodline_stage_def.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const RaceDef = preload("res://scripts/player/progression/race_def.gd")
const SubraceDef = preload("res://scripts/player/progression/subrace_def.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")
const UnitProgress = preload("res://scripts/player/progression/unit_progress.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_reroll_mapping_covers_all_band_boundaries()
	_test_overflow_inputs_fall_back_to_minus_six()
	_test_initial_hp_max_uses_level_zero_formula()
	_test_bake_hidden_luck_uses_character_creation_write_path()
	_test_creation_payload_rejects_identity_body_size_without_content_source()
	_test_creation_payload_rejects_invalid_ascension_pair_without_mutating_identity()
	_test_creation_payload_rejects_invalid_bloodline_pair_without_mutating_identity()
	_test_creation_payload_rejects_ascension_allowed_identity_without_mutating_identity()
	_test_creation_payload_rejects_invalid_race_subrace_pair_without_mutating_member()
	_test_creation_payload_accepts_string_key_content_source()
	_test_creation_payload_derives_body_size_from_identity_content_source()
	_test_creation_payload_does_not_bake_reroll_luck_by_default()
	_test_creation_payload_can_opt_into_reroll_luck_for_main_character()

	if _failures.is_empty():
		print("CharacterCreationService regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("CharacterCreationService regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_reroll_mapping_covers_all_band_boundaries() -> void:
	var cases := [
		{"label": "0 次 reroll", "reroll_count": 0, "expected_hidden_luck": 2},
		{"label": "1 次 reroll", "reroll_count": 1, "expected_hidden_luck": 1},
		{"label": "9 次 reroll", "reroll_count": 9, "expected_hidden_luck": 1},
		{"label": "10 次 reroll", "reroll_count": 10, "expected_hidden_luck": 0},
		{"label": "99 次 reroll", "reroll_count": 99, "expected_hidden_luck": 0},
		{"label": "100 次 reroll", "reroll_count": 100, "expected_hidden_luck": -1},
		{"label": "999 次 reroll", "reroll_count": 999, "expected_hidden_luck": -1},
		{"label": "1,000 次 reroll", "reroll_count": 1000, "expected_hidden_luck": -2},
		{"label": "9,999 次 reroll", "reroll_count": 9999, "expected_hidden_luck": -2},
		{"label": "10,000 次 reroll", "reroll_count": 10000, "expected_hidden_luck": -3},
		{"label": "99,999 次 reroll", "reroll_count": 99999, "expected_hidden_luck": -3},
		{"label": "100,000 次 reroll", "reroll_count": 100000, "expected_hidden_luck": -4},
		{"label": "999,999 次 reroll", "reroll_count": 999999, "expected_hidden_luck": -4},
		{"label": "1,000,000 次 reroll", "reroll_count": 1000000, "expected_hidden_luck": -5},
		{"label": "9,999,999 次 reroll", "reroll_count": 9999999, "expected_hidden_luck": -5},
		{"label": "10,000,000 次 reroll", "reroll_count": 10000000, "expected_hidden_luck": -6},
		{"label": "10,000,001 次 reroll", "reroll_count": 10000001, "expected_hidden_luck": -6},
	]

	for case in cases:
		var actual_hidden_luck := CharacterCreationService.map_reroll_count_to_hidden_luck_at_birth(case.get("reroll_count"))
		_assert_eq(
			actual_hidden_luck,
			int(case.get("expected_hidden_luck", 0)),
			"%s 映射结果错误。" % String(case.get("label", "未知 case"))
		)


func _test_overflow_inputs_fall_back_to_minus_six() -> void:
	var cases := [
		{"label": "超大 float", "reroll_count": 1.0e30},
		{"label": "超大 decimal string", "reroll_count": "1000000000000000000000000000000"},
		{"label": "超大 StringName", "reroll_count": &"1000000000000000000000000000000"},
	]

	for case in cases:
		var actual_hidden_luck := CharacterCreationService.map_reroll_count_to_hidden_luck_at_birth(case.get("reroll_count"))
		_assert_eq(actual_hidden_luck, -6, "%s 应回退到 -6。" % String(case.get("label", "未知 case")))


func _test_initial_hp_max_uses_level_zero_formula() -> void:
	_assert_eq(CharacterCreationService.calculate_initial_hp_max(10), 14, "10 体质的 0 级初始生命应为 14。")
	_assert_eq(CharacterCreationService.calculate_initial_hp_max(14), 18, "14 体质的 0 级初始生命应为 14 + 2*2。")
	_assert_eq(CharacterCreationService.calculate_initial_hp_max(8), 12, "8 体质的 0 级初始生命应为 14 - 1*2。")


func _test_bake_hidden_luck_uses_character_creation_write_path() -> void:
	var progression := UnitProgress.new()
	progression.unit_id = &"hero"
	progression.display_name = "Hero"
	progression.unit_base_attributes.set_attribute_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH, 1)

	var attribute_service := AttributeService.new()
	attribute_service.setup(progression)

	var creation_service := CharacterCreationService.new()
	var baked := creation_service.bake_hidden_luck_at_birth(attribute_service, 10000)

	_assert_true(baked, "CharacterCreationService 应能通过 character_creation 来源写入 hidden_luck_at_birth。")
	_assert_eq(
		attribute_service.get_base_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH),
		-3,
		"CharacterCreationService 应把 reroll=10000 烘焙为 -3。"
	)


func _test_creation_payload_rejects_identity_body_size_without_content_source() -> void:
	var payload := _build_creation_payload(0)
	payload["body_size"] = 99
	payload["body_size_category"] = &"boss"

	var member_state = CharacterCreationService.create_member_from_character_creation_payload(&"bad_body", payload)

	_assert_true(
		member_state == null,
		"建卡 payload 携带身份/体型字段时必须传内容源，不能保留 payload body_size/body_size_category。"
	)


func _test_creation_payload_derives_body_size_from_identity_content_source() -> void:
	var payload := _build_creation_payload(0)
	payload["body_size"] = 99
	payload["body_size_category"] = &"boss"
	payload["ascension_id"] = &"titan"
	payload["ascension_stage_id"] = &"titan_avatar"

	var member_state = CharacterCreationService.create_member_from_character_creation_payload(
		&"derived_body",
		payload,
		_make_creation_content_source()
	)

	_assert_true(member_state != null, "建卡 payload 有内容源时应能创建角色。")
	if member_state == null:
		return
	_assert_eq(
		member_state.body_size_category,
		&"huge",
		"建卡体型分类应按 ascension stage > subrace > race 派生，不能采用 payload body_size_category。"
	)
	_assert_eq(
		member_state.body_size,
		BodySizeRules.get_body_size_for_category(&"huge"),
		"建卡 body_size 应由 body_size_category 映射得到，不能采用 payload body_size。"
	)


func _test_creation_payload_rejects_invalid_ascension_pair_without_mutating_identity() -> void:
	var member_state := _make_existing_member_state()
	var before_identity := _capture_identity_body(member_state)
	var payload := _build_creation_payload(0)
	payload["body_size"] = 99
	payload["body_size_category"] = &"boss"
	payload["ascension_id"] = &""
	payload["ascension_stage_id"] = &"titan_avatar"

	var applied := CharacterCreationService.apply_character_creation_payload_to_member(
		member_state,
		payload,
		_make_creation_content_source()
	)

	_assert_true(not applied, "建卡替换路径应拒绝半设置 ascension/stage，不应把 stage 当成隐式 ascension。")
	_assert_eq(_capture_identity_body(member_state), before_identity, "非法 ascension payload 不应污染成员身份或派生体型。")


func _test_creation_payload_rejects_invalid_bloodline_pair_without_mutating_identity() -> void:
	var member_state := _make_existing_member_state()
	var before_identity := _capture_identity_body(member_state)
	var payload := _build_creation_payload(0)
	payload["bloodline_id"] = &"titan"
	payload["bloodline_stage_id"] = &"dragon_awakened"

	var applied := CharacterCreationService.apply_character_creation_payload_to_member(
		member_state,
		payload,
		_make_creation_content_source()
	)

	_assert_true(not applied, "建卡替换路径应拒绝不属于该 bloodline 的 stage。")
	_assert_eq(_capture_identity_body(member_state), before_identity, "非法 bloodline payload 不应污染成员身份或派生体型。")


func _test_creation_payload_rejects_ascension_allowed_identity_without_mutating_identity() -> void:
	var member_state := _make_existing_member_state()
	var before_identity := _capture_identity_body(member_state)
	var payload := _build_creation_payload(0)
	payload["ascension_id"] = &"bloodline_locked_ascension"
	payload["ascension_stage_id"] = &"bloodline_locked_awakened"

	var applied := CharacterCreationService.apply_character_creation_payload_to_member(
		member_state,
		payload,
		_make_creation_content_source()
	)

	_assert_true(not applied, "建卡替换路径应拒绝不满足 allowed_bloodline_ids 的 ascension。")
	_assert_eq(_capture_identity_body(member_state), before_identity, "非法 ascension allowed gate 不应污染成员身份或派生体型。")


func _test_creation_payload_rejects_invalid_race_subrace_pair_without_mutating_member() -> void:
	var member_state := _make_existing_member_state()
	var before_surface := _capture_creation_surface(member_state)
	var payload := _build_creation_payload(0)
	payload["display_name"] = "Should Not Apply"
	payload["subrace_id"] = &"wrong_parent"
	payload["body_size"] = 99
	payload["body_size_category"] = &"boss"

	var applied := CharacterCreationService.apply_character_creation_payload_to_member(
		member_state,
		payload,
		_make_creation_content_source()
	)

	_assert_true(not applied, "建卡替换路径应拒绝 race/subrace 双向关系非法的 payload。")
	_assert_eq(_capture_creation_surface(member_state), before_surface, "非法 race/subrace payload 不应污染成员身份、体型、显示名或属性。")


func _test_creation_payload_accepts_string_key_content_source() -> void:
	var payload := _build_creation_payload(0)
	payload["body_size"] = 99
	payload["body_size_category"] = &"boss"

	var member_state = CharacterCreationService.create_member_from_character_creation_payload(
		&"string_key_content",
		payload,
		_make_string_key_creation_content_source()
	)

	_assert_true(member_state != null, "建卡内容源使用 String 字典 key 时，合法身份 payload 仍应通过。")
	if member_state == null:
		return
	_assert_eq(member_state.race_id, &"human", "String key 内容源不应影响 race_id 落地。")
	_assert_eq(member_state.subrace_id, &"common_human", "String key 内容源不应影响 subrace_id 落地。")
	_assert_eq(member_state.body_size_category, &"large", "String key 内容源仍应按 subrace override 派生 body_size_category。")


func _test_creation_payload_does_not_bake_reroll_luck_by_default() -> void:
	var payload := _build_creation_payload(0)
	var member_state = CharacterCreationService.create_member_from_character_creation_payload(
		&"companion",
		payload,
		_make_creation_content_source()
	)

	_assert_eq(
		member_state.get_hidden_luck_at_birth(),
		0,
		"非主角通过正式建卡 payload 创建时，即使 payload 带 reroll_count，也应默认 hidden_luck_at_birth=0。"
	)


func _test_creation_payload_can_opt_into_reroll_luck_for_main_character() -> void:
	var payload := _build_creation_payload(0)
	var member_state = CharacterCreationService.create_member_from_character_creation_payload(
		&"hero",
		payload,
		_make_creation_content_source(),
		{CharacterCreationService.CREATION_OPTION_BAKE_REROLL_LUCK: true}
	)

	_assert_eq(
		member_state.get_hidden_luck_at_birth(),
		2,
		"主角建卡 opt-in 后应按 reroll_count=0 烘焙 hidden_luck_at_birth=+2。"
	)


func _build_creation_payload(reroll_count: int) -> Dictionary:
	return {
		"display_name": "Creation Test",
		"race_id": &"human",
		"subrace_id": &"common_human",
		"age_years": 24,
		"birth_at_world_step": 0,
		"age_profile_id": &"human_age_profile",
		"natural_age_stage_id": &"adult",
		"effective_age_stage_id": &"adult",
		"body_size_category": &"medium",
		"strength": 10,
		"agility": 10,
		"constitution": 10,
		"perception": 10,
		"intelligence": 10,
		"willpower": 10,
		"action_threshold": 30,
		"reroll_count": reroll_count,
	}


func _make_creation_content_source() -> Dictionary:
	var race_def := RaceDef.new()
	race_def.race_id = &"human"
	race_def.body_size_category = &"medium"

	var subrace_def := SubraceDef.new()
	subrace_def.subrace_id = &"common_human"
	subrace_def.parent_race_id = race_def.race_id
	subrace_def.body_size_category_override = &"large"
	race_def.default_subrace_id = subrace_def.subrace_id
	race_def.subrace_ids = [subrace_def.subrace_id]

	var wrong_parent_subrace_def := SubraceDef.new()
	wrong_parent_subrace_def.subrace_id = &"wrong_parent"
	wrong_parent_subrace_def.parent_race_id = &"elf"

	var titan_bloodline_def := BloodlineDef.new()
	titan_bloodline_def.bloodline_id = &"titan"
	titan_bloodline_def.stage_ids = [&"titan_awakened"]

	var titan_bloodline_stage_def := BloodlineStageDef.new()
	titan_bloodline_stage_def.stage_id = &"titan_awakened"
	titan_bloodline_stage_def.bloodline_id = titan_bloodline_def.bloodline_id

	var dragon_bloodline_def := BloodlineDef.new()
	dragon_bloodline_def.bloodline_id = &"dragon"
	dragon_bloodline_def.stage_ids = [&"dragon_awakened"]

	var dragon_bloodline_stage_def := BloodlineStageDef.new()
	dragon_bloodline_stage_def.stage_id = &"dragon_awakened"
	dragon_bloodline_stage_def.bloodline_id = dragon_bloodline_def.bloodline_id

	var ascension_def := AscensionDef.new()
	ascension_def.ascension_id = &"titan"
	ascension_def.stage_ids = [&"titan_avatar"]

	var ascension_stage_def := AscensionStageDef.new()
	ascension_stage_def.stage_id = &"titan_avatar"
	ascension_stage_def.ascension_id = &"titan"
	ascension_stage_def.body_size_category_override = &"huge"

	var bloodline_locked_ascension_def := AscensionDef.new()
	bloodline_locked_ascension_def.ascension_id = &"bloodline_locked_ascension"
	bloodline_locked_ascension_def.stage_ids = [&"bloodline_locked_awakened"]
	bloodline_locked_ascension_def.allowed_bloodline_ids = [&"titan"]

	var bloodline_locked_stage_def := AscensionStageDef.new()
	bloodline_locked_stage_def.stage_id = &"bloodline_locked_awakened"
	bloodline_locked_stage_def.ascension_id = bloodline_locked_ascension_def.ascension_id

	return {
		"race_defs": {race_def.race_id: race_def},
		"subrace_defs": {
			subrace_def.subrace_id: subrace_def,
			wrong_parent_subrace_def.subrace_id: wrong_parent_subrace_def,
		},
		"bloodline_defs": {
			titan_bloodline_def.bloodline_id: titan_bloodline_def,
			dragon_bloodline_def.bloodline_id: dragon_bloodline_def,
		},
		"bloodline_stage_defs": {
			titan_bloodline_stage_def.stage_id: titan_bloodline_stage_def,
			dragon_bloodline_stage_def.stage_id: dragon_bloodline_stage_def,
		},
		"ascension_defs": {
			ascension_def.ascension_id: ascension_def,
			bloodline_locked_ascension_def.ascension_id: bloodline_locked_ascension_def,
		},
		"ascension_stage_defs": {
			ascension_stage_def.stage_id: ascension_stage_def,
			bloodline_locked_stage_def.stage_id: bloodline_locked_stage_def,
		},
	}


func _make_string_key_creation_content_source() -> Dictionary:
	var content_source := _make_creation_content_source()
	var string_key_source := {}
	for bucket_name in content_source.keys():
		var bucket: Dictionary = content_source.get(bucket_name, {})
		var string_key_bucket := {}
		for key in bucket.keys():
			string_key_bucket[String(key)] = bucket.get(key)
		string_key_source[bucket_name] = string_key_bucket
	return string_key_source


func _make_existing_member_state() -> PartyMemberState:
	var member_state := PartyMemberState.new()
	member_state.member_id = &"hero"
	member_state.display_name = "Existing Hero"
	member_state.race_id = &"human"
	member_state.subrace_id = &"common_human"
	member_state.body_size_category = &"large"
	member_state.body_size = BodySizeRules.get_body_size_for_category(member_state.body_size_category)
	member_state.progression = UnitProgress.new()
	member_state.progression.unit_id = member_state.member_id
	member_state.progression.display_name = member_state.display_name
	member_state.progression.unit_base_attributes = UnitBaseAttributes.new()
	return member_state


func _capture_identity_body(member_state: PartyMemberState) -> Dictionary:
	return {
		"race_id": member_state.race_id,
		"subrace_id": member_state.subrace_id,
		"bloodline_id": member_state.bloodline_id,
		"bloodline_stage_id": member_state.bloodline_stage_id,
		"ascension_id": member_state.ascension_id,
		"ascension_stage_id": member_state.ascension_stage_id,
		"body_size_category": member_state.body_size_category,
		"body_size": member_state.body_size,
	}


func _capture_creation_surface(member_state: PartyMemberState) -> Dictionary:
	var base_attributes = member_state.progression.unit_base_attributes if member_state != null and member_state.progression != null else null
	var strength := int(base_attributes.get_attribute_value(UnitBaseAttributes.STRENGTH)) if base_attributes != null else -999
	return {
		"display_name": member_state.display_name,
		"race_id": member_state.race_id,
		"subrace_id": member_state.subrace_id,
		"bloodline_id": member_state.bloodline_id,
		"bloodline_stage_id": member_state.bloodline_stage_id,
		"ascension_id": member_state.ascension_id,
		"ascension_stage_id": member_state.ascension_stage_id,
		"body_size_category": member_state.body_size_category,
		"body_size": member_state.body_size,
		"strength": strength,
	}


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
