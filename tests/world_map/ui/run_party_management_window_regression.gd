extends SceneTree

const PARTY_MANAGEMENT_WINDOW_SCENE = preload("res://scenes/ui/party_management_window.tscn")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const UnitSkillProgress = preload("res://scripts/player/progression/unit_skill_progress.gd")
const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_leader_to_reserve_emits_roster_before_leader()
	await _test_member_details_tolerate_missing_skill_and_occupied_slots()

	if _failures.is_empty():
		print("Party management window regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Party management window regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_leader_to_reserve_emits_roster_before_leader() -> void:
	var window = PARTY_MANAGEMENT_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	var party_state := PartyState.new()
	var leader := _make_member(&"leader", "队长")
	var ally := _make_member(&"ally", "队友")
	party_state.leader_member_id = &"leader"
	party_state.active_member_ids = [&"leader", &"ally"]
	party_state.reserve_member_ids = []
	party_state.set_member_state(leader)
	party_state.set_member_state(ally)

	var event_order: Array[String] = []
	var roster_payloads: Array[Dictionary] = []
	var leader_payloads: Array[StringName] = []
	window.roster_change_requested.connect(func(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName]) -> void:
		event_order.append("roster")
		roster_payloads.append({
			"active": active_member_ids.duplicate(),
			"reserve": reserve_member_ids.duplicate(),
		})
	)
	window.leader_change_requested.connect(func(member_id: StringName) -> void:
		event_order.append("leader")
		leader_payloads.append(member_id)
	)

	window.show_party(party_state)
	await process_frame
	_assert_true(window.select_member(&"leader"), "测试应能选中当前队长。")
	window._on_move_to_reserve_button_pressed()
	await process_frame

	_assert_eq(event_order, ["roster", "leader"], "队长移入替补时应先发 roster_change，再发 leader_change。")
	_assert_eq(leader_payloads.size(), 1, "队长移入替补应只发一次 leader_change。")
	if not leader_payloads.is_empty():
		_assert_eq(leader_payloads[0], &"ally", "队长移入替补后应选择剩余上阵成员为新队长。")
	_assert_eq(roster_payloads.size(), 1, "队长移入替补应只发一次 roster_change。")
	if not roster_payloads.is_empty():
		var roster_payload := roster_payloads[0]
		var active_ids: Array = roster_payload.get("active", [])
		var reserve_ids: Array = roster_payload.get("reserve", [])
		_assert_true(active_ids.has(&"ally"), "roster_change active payload 应包含新队长。")
		_assert_true(not active_ids.has(&"leader"), "roster_change active payload 不应继续包含已下阵队长。")
		_assert_true(reserve_ids.has(&"leader"), "roster_change reserve payload 应包含已下阵队长。")

	window.queue_free()
	await process_frame


func _test_member_details_tolerate_missing_skill_and_occupied_slots() -> void:
	var window = PARTY_MANAGEMENT_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	var party_state := PartyState.new()
	var hero := _make_member(&"hero", "主角")
	var missing_skill := UnitSkillProgress.new()
	missing_skill.skill_id = &"missing_skill"
	missing_skill.is_learned = true
	missing_skill.skill_level = 2
	hero.progression.set_skill_progress(missing_skill)

	var occupied_slots: Array[StringName] = [&"main_hand", &"off_hand"]
	var equipment_instance := EquipmentInstanceState.create(&"iron_greatsword", &"eq_party_window_001")
	_assert_true(
		hero.equipment_state.set_equipped_entry(&"main_hand", &"iron_greatsword", occupied_slots, equipment_instance),
		"测试装备状态应能写入双手武器。"
	)
	party_state.leader_member_id = &"hero"
	party_state.active_member_ids = [&"hero"]
	party_state.reserve_member_ids = []
	party_state.set_member_state(hero)

	window.show_party(party_state)
	await process_frame
	_assert_true(window.select_member(&"hero"), "测试应能选中主角。")
	await process_frame

	var equipment_text := String(window.equipment_label.text)
	var skills_text := String(window.skills_label.text)
	_assert_true(equipment_text.contains("已装备：1"), "双手占位不应把副手占位重复计为装备。")
	_assert_true(equipment_text.contains("副手：由主手占用"), "副手占位应显示为被主手占用。")
	_assert_true(skills_text.contains("missing_skill"), "缺失 skill_def 时仍应展示技能 ID。")
	_assert_true(skills_text.contains("技能定义缺失"), "缺失 skill_def 时应显示缺失提示而不是崩溃。")

	window.queue_free()
	await process_frame


func _make_member(member_id: StringName, display_name: String) -> PartyMemberState:
	var member := PartyMemberState.new()
	member.member_id = member_id
	member.display_name = display_name
	member.progression.unit_id = member_id
	member.progression.display_name = display_name
	member.progression.character_level = 1
	member.current_hp = 30
	member.current_mp = 8
	return member


func _assert_true(value: bool, message: String) -> void:
	if not value:
		_failures.append(message)


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_failures.append("%s (actual=%s expected=%s)" % [message, str(actual), str(expected)])
