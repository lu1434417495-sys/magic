extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT = preload("res://scripts/systems/game_runtime_snapshot_builder.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_snapshot_builder_matches_facade_outputs()
	_test_snapshot_builder_exposes_party_quest_snapshot()
	_test_snapshot_builder_exposes_forge_modal_snapshot()
	_test_snapshot_builder_exposes_generic_forge_modal_snapshot()

	if _failures.is_empty():
		print("Game runtime snapshot builder regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Game runtime snapshot builder regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_snapshot_builder_matches_facade_outputs() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(facade)

	var facade_snapshot: Dictionary = facade.build_headless_snapshot()
	var builder_snapshot: Dictionary = builder.build_headless_snapshot()
	var facade_text := facade.build_text_snapshot()
	var builder_text := builder.build_text_snapshot()

	_assert_eq(builder_snapshot, facade_snapshot, "Snapshot builder 输出应与 facade.build_headless_snapshot() 保持一致。")
	_assert_eq(builder_text, facade_text, "Snapshot builder 文本快照应与 facade.build_text_snapshot() 保持一致。")
	_assert_true(not builder_text.is_empty(), "Snapshot builder 文本快照不应为空。")
	_assert_true(builder_snapshot.has("logs"), "运行时快照应包含日志段。")
	_assert_true(not String(builder_snapshot.get("logs", {}).get("file_path", "")).is_empty(), "运行时快照应暴露日志文件路径。")
	_assert_true(not (builder_snapshot.get("logs", {}).get("entries", []) as Array).is_empty(), "运行时快照应包含最近日志条目。")
	_assert_true(builder_text.contains("[LOG]"), "运行时文本快照应包含日志分段。")

	builder.dispose()
	facade.dispose()
	_cleanup_test_session(game_session)


func _test_snapshot_builder_exposes_party_quest_snapshot() -> void:
	var runtime := FakeQuestRuntime.new()
	var party_state := FakeQuestPartyState.new()
	var quest_state := QuestState.new()
	quest_state.quest_id = &"contract_wolf_pack"
	quest_state.mark_accepted(12)
	quest_state.record_objective_progress(&"defeat_wolves", 2, 3, {"enemy_template_id": "wolf_raider"})
	quest_state.record_objective_progress(&"defeat_wolves", 2, 3, {"enemy_template_id": "wolf_raider"})
	quest_state.record_objective_progress(&"report_back", 1, 1, {"settlement_id": "spring_village_01"})
	party_state.active_quests = [quest_state]
	party_state.completed_quest_ids = [&"contract_intro"]
	runtime.party_state = party_state

	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()
	var text_snapshot := builder.build_text_snapshot()

	var quests_snapshot: Dictionary = snapshot.get("party", {}).get("quests", {})
	_assert_true(not quests_snapshot.is_empty(), "当 PartyState 暴露 quest schema 时，headless snapshot 应在 party 段包含 quests。")
	_assert_eq(
		quests_snapshot.get("active_quest_ids", []),
		["contract_wolf_pack"],
		"active_quest_ids 应稳定暴露当前激活任务 ID。"
	)
	_assert_eq(
		quests_snapshot.get("completed_quest_ids", []),
		["contract_intro"],
		"completed_quest_ids 应稳定暴露已完成任务 ID。"
	)
	var active_quests: Array = quests_snapshot.get("active_quests", [])
	_assert_eq(active_quests.size(), 1, "active_quests 应保留当前任务详情。")
	if not active_quests.is_empty():
		var quest_entry: Dictionary = active_quests[0]
		_assert_eq(String(quest_entry.get("quest_id", "")), "contract_wolf_pack", "任务快照应保留 quest_id。")
		_assert_eq(int(quest_entry.get("accepted_at_world_step", -1)), 12, "任务快照应保留接取时间。")
		_assert_eq(
			int((quest_entry.get("objective_progress", {}) as Dictionary).get("defeat_wolves", 0)),
			3,
			"任务快照应保留封顶后的目标进度。"
		)
		_assert_eq(
			String((quest_entry.get("last_progress_context", {}) as Dictionary).get("settlement_id", "")),
			"spring_village_01",
			"任务快照应保留最近进度上下文。"
		)
	_assert_true(text_snapshot.contains("[QUEST]"), "文本快照应在 PartyState 含任务时渲染 QUEST 分段。")
	_assert_true(text_snapshot.contains("active_quest_ids=contract_wolf_pack"), "文本快照应渲染激活任务 ID。")
	_assert_true(text_snapshot.contains("completed_quest_ids=contract_intro"), "文本快照应渲染完成任务 ID。")
	_assert_true(text_snapshot.contains("quest=contract_wolf_pack"), "文本快照应渲染任务明细。")

	builder.dispose()


func _test_snapshot_builder_exposes_forge_modal_snapshot() -> void:
	var runtime := FakeQuestRuntime.new()
	runtime.active_modal_id = "forge"
	runtime.forge_window_data = {
		"title": "灰烬镇 · 大师重铸",
		"settlement_id": "forge_town",
		"entries": [
			{
				"display_name": "大师重铸：铁制大剑",
				"state_label": "状态：可重铸",
				"cost_label": "材料：1 件 青铜短剑、2 件 铁矿石",
			},
		],
	}

	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()
	var text_snapshot := builder.build_text_snapshot()

	_assert_true(bool(snapshot.get("forge", {}).get("visible", false)), "forge modal 激活时快照应暴露 forge.visible。")
	_assert_eq(String(snapshot.get("forge", {}).get("window_data", {}).get("title", "")), "灰烬镇 · 大师重铸", "forge 快照应保留窗口标题。")
	_assert_true(text_snapshot.contains("[FORGE]"), "文本快照应渲染 FORGE 分段。")
	_assert_true(text_snapshot.contains("大师重铸：铁制大剑"), "文本快照应渲染 forge 配方名称。")

	builder.dispose()


func _test_snapshot_builder_exposes_generic_forge_modal_snapshot() -> void:
	var runtime := FakeQuestRuntime.new()
	runtime.active_modal_id = "forge"
	runtime.active_shop_context = {
		"title": "灰烬镇 · 熔炉整备",
		"settlement_id": "forge_town",
		"panel_kind": "forge",
		"submission_source": "forge",
		"entries": [
			{
				"entry_id": "forge:temper_edge",
				"display_name": "刃口淬火",
				"state_label": "状态：可执行",
				"cost_label": "材料：1 件 铁矿石、1 件 皮革护衣",
			},
		],
	}

	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()
	var text_snapshot := builder.build_text_snapshot()

	_assert_eq(String(snapshot.get("forge", {}).get("window_data", {}).get("title", "")), "灰烬镇 · 熔炉整备", "通用 forge modal 应从共享窗口上下文进入 forge 快照。")
	_assert_true(text_snapshot.contains("刃口淬火"), "文本快照应渲染通用 forge 条目名称。")
	_assert_true((snapshot.get("shop", {}).get("window_data", {}) as Dictionary).is_empty(), "forge panel_kind 不应继续出现在 shop 快照中。")

	builder.dispose()


func _create_test_session():
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "GameSession 应能基于测试世界配置创建新存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return null
	return game_session


func _cleanup_test_session(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


class FakeQuestPartyState:
	extends RefCounted

	var gold := 0
	var leader_member_id: StringName = &""
	var active_member_ids: Array = []
	var reserve_member_ids: Array = []
	var active_quests: Array = []
	var completed_quest_ids: Array = []

	func get_member_state(_member_id: StringName):
		return null

	func get_active_quests():
		return active_quests

	func get_completed_quest_ids():
		return completed_quest_ids


class FakeQuestRuntime:
	extends RefCounted

	var party_state: FakeQuestPartyState = null
	var active_modal_id := ""
	var forge_window_data: Dictionary = {}
	var active_shop_context: Dictionary = {}

	func is_battle_active() -> bool:
		return false

	func get_status_text() -> String:
		return ""

	func get_active_modal_id() -> String:
		return active_modal_id

	func get_selected_settlement() -> Dictionary:
		return {}

	func get_selected_world_npc() -> Dictionary:
		return {}

	func get_selected_encounter_anchor():
		return null

	func get_selected_world_event() -> Dictionary:
		return {}

	func get_active_map_id() -> String:
		return ""

	func get_active_map_display_name() -> String:
		return ""

	func is_submap_active() -> bool:
		return false

	func get_world_step() -> int:
		return 0

	func get_player_coord() -> Vector2i:
		return Vector2i.ZERO

	func get_selected_coord() -> Vector2i:
		return Vector2i.ZERO

	func get_pending_submap_prompt() -> Dictionary:
		return {}

	func get_submap_return_hint_text() -> String:
		return ""

	func get_party_state():
		return party_state

	func get_party_selected_member_id() -> StringName:
		return &""

	func get_pending_reward_count() -> int:
		return 0

	func get_member_achievement_summary(_member_id: StringName) -> Dictionary:
		return {}

	func get_member_attribute_snapshot(_member_id: StringName):
		return null

	func get_member_equipped_entries(_member_id: StringName) -> Array:
		return []

	func get_resolved_settlement_id() -> String:
		return ""

	func get_settlement_window_data(_settlement_id: String = "") -> Dictionary:
		return {}

	func get_settlement_feedback_text() -> String:
		return ""

	func get_shop_window_data() -> Dictionary:
		return {}

	func get_active_shop_context() -> Dictionary:
		return active_shop_context.duplicate(true)

	func get_forge_window_data() -> Dictionary:
		return forge_window_data.duplicate(true)

	func get_stagecoach_window_data() -> Dictionary:
		return {}

	func get_character_info_context() -> Dictionary:
		return {}

	func get_active_warehouse_entry_label() -> String:
		return ""

	func get_warehouse_window_data() -> Dictionary:
		return {}

	func get_battle_state():
		return null

	func get_snapshot_reward():
		return null

	func get_current_promotion_prompt() -> Dictionary:
		return {}

	func get_log_snapshot(_limit: int = 30) -> Dictionary:
		return {}

	func get_nearby_encounter_entries(_limit: int = 8) -> Array[Dictionary]:
		return []

	func get_nearby_world_event_entries(_limit: int = 8) -> Array[Dictionary]:
		return []
