extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const PARTY_MANAGEMENT_WINDOW_SCENE = preload("res://scenes/ui/party_management_window.tscn")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const UnitSkillProgress = preload("res://scripts/player/progression/unit_skill_progress.gd")
const AttributeSnapshot = preload("res://scripts/player/progression/attribute_snapshot.gd")
const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


class SnapshotProvider:
	extends RefCounted

	var snapshot = null
	var requests: Array[Dictionary] = []

	func get_member_attribute_snapshot_for_equipment_view(member_id: StringName, equipment_view: Variant):
		requests.append({
			"member_id": member_id,
			"equipment_view": equipment_view,
		})
		return snapshot


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_window_uses_half_viewport_with_minimum_size()
	await _test_leader_to_reserve_emits_roster_before_leader()
	await _test_member_details_tolerate_missing_skill_and_occupied_slots()
	await _test_member_details_use_injected_character_management_snapshot()

	if _failures.is_empty():
		print("Party management window regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Party management window regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_window_uses_half_viewport_with_minimum_size() -> void:
	root.size = Vector2i(1920, 1080)
	var window = PARTY_MANAGEMENT_WINDOW_SCENE.instantiate()
	root.add_child(window)
	window.anchor_right = 0.0
	window.anchor_bottom = 0.0
	window.size = Vector2(1920, 1080)
	await process_frame
	window.show_party(_build_party_state([&"hero"]))
	await process_frame

	var panel := window.get_node("%Panel") as Control
	_assert_vector2_near(panel.custom_minimum_size, Vector2(960, 540), 0.1, "1920x1080 下队伍管理窗口应使用半屏尺寸。")
	_assert_true(window.get_node_or_null("CenterContainer/Panel/MarginContainer/Content/Body/DetailsTabs/概览/OverviewLabel") != null, "概览应在右侧详情标签页内。")
	_assert_true(window.get_node_or_null("CenterContainer/Panel/MarginContainer/Content/Body/DetailsTabs/属性/AttributesLabel") != null, "属性标签页应保留。")
	_assert_true(window.get_node_or_null("CenterContainer/Panel/MarginContainer/Content/Body/DetailsTabs/装备/EquipmentLabel") != null, "装备标签页应保留。")
	_assert_true(window.get_node_or_null("CenterContainer/Panel/MarginContainer/Content/Body/DetailsTabs/技能/SkillsLabel") != null, "技能标签页应保留。")
	_assert_true(window.get_node_or_null("CenterContainer/Panel/MarginContainer/Content/Body/DetailsTabs/职业/ProfessionsLabel") != null, "职业标签页应保留。")

	window.queue_free()
	await process_frame

	root.size = Vector2i(1000, 700)
	window = PARTY_MANAGEMENT_WINDOW_SCENE.instantiate()
	root.add_child(window)
	window.anchor_right = 0.0
	window.anchor_bottom = 0.0
	window.size = Vector2(1000, 700)
	await process_frame
	window.show_party(_build_party_state([&"hero"]))
	await process_frame

	panel = window.get_node("%Panel") as Control
	_assert_vector2_near(panel.custom_minimum_size, Vector2(860, 540), 0.1, "小窗口下队伍管理窗口应使用可读保底尺寸。")
	_assert_true(panel.custom_minimum_size.x <= 1000.0 - 96.0, "保底宽度不应超过横向安全区域。")
	_assert_true(panel.custom_minimum_size.y <= 700.0 - 60.0, "保底高度不应超过纵向安全区域。")

	window.queue_free()
	await process_frame
	root.size = Vector2i(1280, 720)


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


func _test_member_details_use_injected_character_management_snapshot() -> void:
	var window = PARTY_MANAGEMENT_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	var snapshot := AttributeSnapshot.new()
	snapshot.set_value(&"hp_max", 123)
	snapshot.set_value(&"mp_max", 17)
	snapshot.set_value(&"strength", 15)
	var provider := SnapshotProvider.new()
	provider.snapshot = snapshot
	window.set_character_management(provider)

	var party_state := _build_party_state([&"hero"])
	var hero: PartyMemberState = party_state.get_member_state(&"hero")
	hero.current_hp = 12
	hero.current_mp = 3
	window.show_party(party_state)
	await process_frame
	_assert_true(window.select_member(&"hero"), "测试应能选中主角。")
	await process_frame

	var overview_text := String(window.overview_label.text)
	var attributes_text := String(window.attributes_label.text)
	_assert_true(provider.requests.size() > 0, "队伍管理窗口应通过注入的角色管理桥请求属性快照。")
	_assert_true(overview_text.contains("HP 12 / 123  MP 3 / 17"), "概览资源值应来自注入快照。")
	_assert_true(attributes_text.contains("力量：15"), "属性页基础属性应来自注入快照。")

	window.queue_free()
	await process_frame


func _build_party_state(member_ids: Array[StringName]) -> PartyState:
	var party_state := PartyState.new()
	for member_id in member_ids:
		party_state.set_member_state(_make_member(member_id, String(member_id)))
	party_state.leader_member_id = member_ids[0] if not member_ids.is_empty() else &""
	party_state.active_member_ids = member_ids.duplicate()
	party_state.reserve_member_ids = []
	return party_state


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
		_test.fail(message)


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_test.fail("%s (actual=%s expected=%s)" % [message, str(actual), str(expected)])


func _assert_vector2_near(actual: Vector2, expected: Vector2, tolerance: float, message: String) -> void:
	if absf(actual.x - expected.x) > tolerance or absf(actual.y - expected.y) > tolerance:
		_test.fail("%s (actual=%s expected=%s)" % [message, str(actual), str(expected)])
