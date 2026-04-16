extends SceneTree

const GameRuntimeFacade = preload("res://scripts/systems/game_runtime_facade.gd")
const GameRuntimeSettlementCommandHandler = preload("res://scripts/systems/game_runtime_settlement_command_handler.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")

var _failures: Array[String] = []


class MockAttributeSnapshot:
	extends RefCounted

	var values: Dictionary = {}

	func get_value(attribute_id: StringName) -> int:
		return int(values.get(attribute_id, 0))


class MockSettlementHandler:
	extends RefCounted

	var calls: Array[Dictionary] = []

	func get_settlement_window_data(settlement_id: String = "") -> Dictionary:
		calls.append({"method": "get_settlement_window_data", "args": [settlement_id]})
		return {"settlement_id": "spring_village_01"}

	func command_execute_settlement_action(action_id: String, payload: Dictionary = {}) -> Dictionary:
		calls.append({"method": "command_execute_settlement_action", "args": [action_id, payload.duplicate(true)]})
		return {
			"ok": true,
			"message": action_id,
			"battle_refresh_mode": "",
		}

	func resolve_command_settlement_id() -> String:
		calls.append({"method": "resolve_command_settlement_id", "args": []})
		return "spring_village_01"

	func on_settlement_action_requested(settlement_id: String, action_id: String, payload: Dictionary) -> void:
		calls.append({
			"method": "on_settlement_action_requested",
			"args": [settlement_id, action_id, payload.duplicate(true)],
		})

	func on_settlement_window_closed() -> void:
		calls.append({"method": "on_settlement_window_closed", "args": []})


class MockRuntime:
	extends RefCounted

	const PARTY_WAREHOUSE_INTERACTION_ID := "party_warehouse"

	var _active_settlement_id := ""
	var _active_modal_id := "settlement"
	var _active_settlement_feedback_text := ""
	var _active_contract_board_context: Dictionary = {}
	var _active_shop_context: Dictionary = {}
	var _active_stagecoach_context: Dictionary = {}
	var _current_status_message := ""
	var _selected_settlement: Dictionary = {}
	var _settlements_by_id: Dictionary = {}
	var _quest_defs: Dictionary = {}
	var _player_coord := Vector2i.ZERO
	var _selected_coord := Vector2i.ZERO
	var _party_state := PartyState.new()
	var _settlement_states: Dictionary = {}
	var opened_warehouse_labels: Array[String] = []
	var pending_rewards: Array = []
	var applied_quest_event_batches: Array = []
	var achievement_events: Array[Dictionary] = []
	var accepted_quest_calls: Array[Dictionary] = []
	var persist_calls := 0
	var world_persist_calls := 0
	var player_persist_calls := 0
	var present_reward_calls := 0
	var sync_party_calls := 0
	var battle_active := false
	var world_step := 0
	var refresh_world_visibility_calls := 0

	func build_command_ok(message: String = "", battle_refresh_mode: String = "") -> Dictionary:
		return {
			"ok": true,
			"message": message,
			"battle_refresh_mode": battle_refresh_mode,
		}

	func build_command_error(message: String) -> Dictionary:
		update_status(message)
		return {
			"ok": false,
			"message": message,
		}

	func is_battle_active() -> bool:
		return battle_active

	func get_active_settlement_id() -> String:
		return _active_settlement_id

	func set_active_settlement_id(settlement_id: String) -> void:
		_active_settlement_id = settlement_id

	func set_settlement_feedback_text(feedback_text: String) -> void:
		_active_settlement_feedback_text = feedback_text

	func set_runtime_active_modal_id(modal_id: String) -> void:
		_active_modal_id = modal_id

	func set_player_coord(coord: Vector2i) -> void:
		_player_coord = coord

	func set_selected_coord(coord: Vector2i) -> void:
		_selected_coord = coord

	func set_active_shop_context(context: Dictionary) -> void:
		_active_shop_context = context.duplicate(true)

	func set_active_contract_board_context(context: Dictionary) -> void:
		_active_contract_board_context = context.duplicate(true)

	func clear_active_contract_board_context() -> void:
		_active_contract_board_context.clear()

	func get_active_contract_board_context() -> Dictionary:
		return _active_contract_board_context.duplicate(true)

	func clear_active_shop_context() -> void:
		_active_shop_context.clear()

	func get_active_shop_context() -> Dictionary:
		return _active_shop_context.duplicate(true)

	func set_active_stagecoach_context(context: Dictionary) -> void:
		_active_stagecoach_context = context.duplicate(true)

	func clear_active_stagecoach_context() -> void:
		_active_stagecoach_context.clear()

	func get_active_stagecoach_context() -> Dictionary:
		return _active_stagecoach_context.duplicate(true)

	func update_status(message: String) -> void:
		_current_status_message = message

	func present_pending_reward_if_ready() -> bool:
		present_reward_calls += 1
		return false

	func get_selected_settlement() -> Dictionary:
		return _selected_settlement.duplicate(true)

	func get_settlement_record(settlement_id: String) -> Dictionary:
		return _settlements_by_id.get(settlement_id, {}).duplicate(true)

	func get_settlement_state(settlement_id: String) -> Dictionary:
		return _settlement_states.get(settlement_id, {}).duplicate(true)

	func set_active_settlement_state(settlement_id: String, settlement_state: Dictionary) -> bool:
		_settlement_states[settlement_id] = settlement_state.duplicate(true)
		return true

	func get_all_settlement_records() -> Array[Dictionary]:
		var settlements: Array[Dictionary] = []
		for settlement_variant in _settlements_by_id.values():
			if settlement_variant is Dictionary:
				settlements.append((settlement_variant as Dictionary).duplicate(true))
		return settlements

	func get_party_state():
		return _party_state

	func get_party_warehouse_service():
		return null

	func get_game_session():
		return self

	func get_item_defs() -> Dictionary:
		return {}

	func get_quest_defs() -> Dictionary:
		return _quest_defs.duplicate(true)

	func get_world_step() -> int:
		return world_step

	func advance_world_time_by_steps(delta_steps: int) -> void:
		world_step += maxi(delta_steps, 0)

	func command_accept_quest(quest_id: StringName, allow_reaccept: bool = false) -> Dictionary:
		accepted_quest_calls.append({
			"quest_id": String(quest_id),
			"allow_reaccept": allow_reaccept,
		})
		var quest_data: Dictionary = _quest_defs.get(quest_id, _quest_defs.get(String(quest_id), {})).duplicate(true)
		var quest_label := String(quest_data.get("display_name", quest_id))
		if quest_data.is_empty():
			_current_status_message = "未找到任务 %s。" % String(quest_id)
			return build_command_error(_current_status_message)
		if _party_state.has_active_quest(quest_id):
			_current_status_message = "任务《%s》已在进行中，不能重复接取。" % quest_label
			return build_command_error(_current_status_message)
		var has_completed := _party_state.has_completed_quest(quest_id)
		var effective_allow_reaccept := allow_reaccept or (has_completed and bool(quest_data.get("is_repeatable", false)))
		if has_completed and not effective_allow_reaccept:
			_current_status_message = "任务《%s》已完成，当前不可再次接取。" % quest_label
			return build_command_error(_current_status_message)
		if has_completed and effective_allow_reaccept:
			_party_state.completed_quest_ids.erase(quest_id)
		var quest_state := QuestState.new()
		quest_state.quest_id = quest_id
		quest_state.mark_accepted(world_step)
		_party_state.set_active_quest_state(quest_state)
		_current_status_message = "已重新接取任务《%s》。" % quest_label if has_completed and effective_allow_reaccept else "已接取任务《%s》。" % quest_label
		return build_command_ok(_current_status_message)

	func refresh_world_visibility() -> void:
		refresh_world_visibility_calls += 1

	func get_member_attribute_snapshot(_member_id: StringName):
		var snapshot := MockAttributeSnapshot.new()
		snapshot.values = {
			&"hp_max": 40,
			&"mp_max": 12,
		}
		return snapshot

	func get_member_display_name(member_id: StringName) -> String:
		var member_state = _party_state.get_member_state(member_id)
		return String(member_state.display_name) if member_state != null else String(member_id)

	func open_party_warehouse_window(entry_label: String) -> void:
		opened_warehouse_labels.append(entry_label)

	func enqueue_pending_character_rewards(reward_variants: Array) -> void:
		pending_rewards.append_array(reward_variants.duplicate(true))

	func apply_quest_progress_events_to_party(event_variants: Array, _source_domain: String = "settlement") -> Dictionary:
		applied_quest_event_batches.append(event_variants.duplicate(true))
		var summary := {
			"accepted_quest_ids": [],
			"progressed_quest_ids": [],
			"completed_quest_ids": [],
		}
		for event_variant in event_variants:
			if event_variant is not Dictionary:
				continue
			var event_data := (event_variant as Dictionary).duplicate(true)
			var quest_id := ProgressionDataUtils.to_string_name(event_data.get("quest_id", ""))
			if quest_id == &"":
				continue
			var event_type := String(event_data.get("event_type", "progress"))
			match event_type:
				"accept":
					var quest_state := QuestState.new()
					quest_state.quest_id = quest_id
					quest_state.mark_accepted(int(event_data.get("world_step", world_step)))
					_party_state.set_active_quest_state(quest_state)
					(summary["accepted_quest_ids"] as Array).append(quest_id)
				"complete":
					if _party_state.mark_quest_completed(quest_id, int(event_data.get("world_step", world_step))):
						(summary["completed_quest_ids"] as Array).append(quest_id)
				_:
					var active_quest: QuestState = _party_state.get_active_quest_state(quest_id)
					if active_quest == null:
						active_quest = QuestState.new()
						active_quest.quest_id = quest_id
						active_quest.mark_accepted(int(event_data.get("world_step", world_step)))
						_party_state.set_active_quest_state(active_quest)
						(summary["accepted_quest_ids"] as Array).append(quest_id)
					active_quest.record_objective_progress(
						ProgressionDataUtils.to_string_name(event_data.get("objective_id", "")),
						int(event_data.get("progress_delta", 1)),
						int(event_data.get("target_value", 1)),
						{"settlement_id": String(event_data.get("settlement_id", ""))},
					)
					(summary["progressed_quest_ids"] as Array).append(quest_id)
		return summary

	func record_member_achievement_event(
		member_id: StringName,
		event_id: StringName,
		value: int,
		detail_id: StringName = &""
	) -> void:
		achievement_events.append({
			"member_id": String(member_id),
			"event_id": String(event_id),
			"value": value,
			"detail_id": String(detail_id),
		})

	func sync_party_state_from_character_management() -> void:
		sync_party_calls += 1

	func persist_party_state() -> int:
		persist_calls += 1
		return OK

	func persist_world_data() -> int:
		world_persist_calls += 1
		return OK

	func persist_player_coord() -> int:
		player_persist_calls += 1
		return OK


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_facade_delegates_settlement_surface_to_handler()
	_test_settlement_handler_routes_actions_and_modal_state()

	if _failures.is_empty():
		print("Game runtime settlement command handler regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Game runtime settlement command handler regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_facade_delegates_settlement_surface_to_handler() -> void:
	var facade := GameRuntimeFacade.new()
	var handler := MockSettlementHandler.new()
	facade._settlement_command_handler = handler

	_assert_eq(
		String(facade.command_execute_settlement_action("service:warehouse").get("message", "")),
		"service:warehouse",
		"command_execute_settlement_action() 应委托给 settlement handler。"
	)
	_assert_eq(
		String(facade.get_settlement_window_data("spring_village_01").get("settlement_id", "")),
		"spring_village_01",
		"get_settlement_window_data() 应委托给 settlement handler。"
	)
	_assert_eq(
		facade.get_resolved_settlement_id(),
		"spring_village_01",
		"get_resolved_settlement_id() 应委托给 settlement handler。"
	)

	facade._on_settlement_action_requested("spring_village_01", "service:warehouse", {})
	facade._on_settlement_window_closed()

	_assert_true(_has_call(handler.calls, "on_settlement_action_requested"), "_on_settlement_action_requested() 应委托给 settlement handler。")
	_assert_true(_has_call(handler.calls, "on_settlement_window_closed"), "_on_settlement_window_closed() 应委托给 settlement handler。")


func _test_settlement_handler_routes_actions_and_modal_state() -> void:
	var runtime := MockRuntime.new()
	runtime._party_state = _make_party_state()
	runtime._selected_settlement = {
		"settlement_id": "spring_village_01",
	}
	runtime._settlements_by_id = {
		"spring_village_01": {
			"settlement_id": "spring_village_01",
			"display_name": "春泉村",
			"origin": Vector2i.ZERO,
			"available_services": [
				{
					"action_id": "service:warehouse",
					"facility_name": "据点服务台",
					"npc_name": "军需官",
					"service_type": "仓储",
					"interaction_script_id": MockRuntime.PARTY_WAREHOUSE_INTERACTION_ID,
				},
				{
					"action_id": "service:training",
					"facility_name": "训练场",
					"npc_name": "教官",
					"service_type": "训练",
					"interaction_script_id": "training_service",
				},
				{
					"action_id": "service:rest_full",
					"facility_name": "旅店",
					"npc_name": "店主",
					"service_type": "整备",
					"interaction_script_id": "service_rest_full",
				},
				{
					"action_id": "service:contract_board",
					"facility_name": "公告台",
					"npc_name": "记录员",
					"service_type": "契约板",
					"interaction_script_id": "service_contract_board",
				},
				{
					"action_id": "service:bounty_registry",
					"facility_name": "悬赏署",
					"npc_name": "悬赏文书",
					"service_type": "悬赏",
					"interaction_script_id": "service_bounty_registry",
				},
				{
					"action_id": "service:stagecoach",
					"facility_name": "驿站",
					"npc_name": "驿夫",
					"service_type": "驿站",
					"interaction_script_id": "service_stagecoach",
				},
			],
		},
		"graystone_town_01": {
			"settlement_id": "graystone_town_01",
			"display_name": "灰石镇",
			"origin": Vector2i(2, 1),
			"available_services": [],
		},
	}
	runtime._settlement_states = {
		"spring_village_01": {
			"visited": true,
			"reputation": 0,
			"active_conditions": [],
			"cooldowns": {},
			"shop_inventory_seed": 0,
			"shop_last_refresh_step": 0,
			"shop_states": {},
		},
		"graystone_town_01": {
			"visited": true,
			"reputation": 0,
			"active_conditions": [],
			"cooldowns": {},
			"shop_inventory_seed": 0,
			"shop_last_refresh_step": 0,
			"shop_states": {},
		}
	}
	runtime._quest_defs = {
		&"contract_first_hunt": {
			"quest_id": "contract_first_hunt",
			"display_name": "首轮狩猎",
			"description": "击败任意一组敌对遭遇。",
			"provider_interaction_id": "service_contract_board",
			"objective_defs": [
				{
					"objective_id": "defeat_enemy_once",
					"objective_type": "defeat_enemy",
					"target_id": "",
					"target_value": 1,
				},
			],
			"reward_entries": [
				{"reward_type": "gold", "amount": 80},
			],
		},
		&"contract_manual_drill": {
			"quest_id": "contract_manual_drill",
			"display_name": "训练记录",
			"description": "在训练场完成两次记录。",
			"provider_interaction_id": "service_contract_board",
			"objective_defs": [
				{
					"objective_id": "train_once",
					"objective_type": "settlement_action",
					"target_id": "service:training",
					"target_value": 2,
				},
			],
			"reward_entries": [
				{"reward_type": "gold", "amount": 30},
			],
		},
		&"contract_repeatable_patrol": {
			"quest_id": "contract_repeatable_patrol",
			"display_name": "巡路值守",
			"description": "完成一次例行巡路，随后可再次接取。",
			"provider_interaction_id": "service_contract_board",
			"objective_defs": [
				{
					"objective_id": "warehouse_visit",
					"objective_type": "settlement_action",
					"target_id": "service:warehouse",
					"target_value": 1,
				},
			],
			"reward_entries": [
				{"reward_type": "gold", "amount": 15},
			],
			"is_repeatable": true,
		},
		&"contract_regional_bounty": {
			"quest_id": "contract_regional_bounty",
			"display_name": "地区悬赏",
			"description": "仅应出现在悬赏署任务板。",
			"provider_interaction_id": "service_bounty_registry",
			"objective_defs": [
				{
					"objective_id": "submit_report",
					"objective_type": "settlement_action",
					"target_id": "service:report_bounty",
					"target_value": 1,
				},
			],
			"reward_entries": [
				{"reward_type": "gold", "amount": 120},
			],
		},
	}

	var handler := GameRuntimeSettlementCommandHandler.new()
	handler.setup(runtime)

	var settlement_window_data := handler.get_settlement_window_data("spring_village_01")
	var contract_service := _find_service_entry(settlement_window_data.get("available_services", []), "service:contract_board")
	var bounty_service := _find_service_entry(settlement_window_data.get("available_services", []), "service:bounty_registry")
	_assert_true(not contract_service.is_empty(), "据点窗口应暴露任务板服务入口。")
	_assert_true(bool(contract_service.get("is_enabled", false)), "任务板服务入口应为可点击状态。")
	_assert_true(not bounty_service.is_empty(), "据点窗口应暴露悬赏署服务入口。")
	_assert_true(bool(bounty_service.get("is_enabled", false)), "悬赏署服务入口应为可点击状态。")

	var warehouse_result := handler.command_execute_settlement_action("service:warehouse")
	_assert_true(bool(warehouse_result.get("ok", false)), "据点仓储动作应执行成功。")
	_assert_eq(runtime._active_settlement_id, "spring_village_01", "仓储动作后应记录当前据点 ID。")
	_assert_eq(runtime._active_modal_id, "", "仓储动作后应让位给共享仓库 modal。")
	_assert_true(runtime.opened_warehouse_labels.size() == 1, "仓储动作后应打开共享仓库。")
	_assert_true(runtime.opened_warehouse_labels[0].find("据点服务") >= 0, "仓储入口标签应包含据点服务来源。")
	_assert_eq(runtime._current_status_message, "已从据点服务打开共享仓库。", "仓储动作后应刷新状态文案。")
	_assert_true(runtime.persist_calls > 0, "成功据点动作后应持久化队伍状态。")
	_assert_true(runtime.sync_party_calls > 0, "成功据点动作后应同步角色管理侧的队伍状态。")
	_assert_true(runtime.achievement_events.size() == 1, "成功据点动作后应记录成就事件。")
	_assert_eq(runtime.achievement_events[0].get("detail_id", ""), "service:warehouse", "成就事件应记录动作 ID。")

	var contract_board_result := handler.command_execute_settlement_action("service:contract_board")
	var contract_board_window_data := handler.get_contract_board_window_data()
	var contract_board_entry_ids := _extract_contract_board_entry_ids(contract_board_window_data.get("entries", []))
	_assert_true(bool(contract_board_result.get("ok", false)), "任务板服务应能切换到 contract_board modal。")
	_assert_eq(runtime._active_modal_id, "contract_board", "任务板服务后应切换到 contract_board modal。")
	_assert_eq(String(contract_board_window_data.get("action_id", "")), "service:contract_board", "任务板 modal 应保留原始 action_id。")
	_assert_eq(String(contract_board_window_data.get("provider_interaction_id", "")), "service_contract_board", "任务板 modal 应记录当前 provider_interaction_id。")
	_assert_eq(contract_board_entry_ids, ["contract_first_hunt", "contract_manual_drill", "contract_repeatable_patrol"], "任务板 modal 只应按 provider_interaction_id 暴露当前服务的契约条目。")
	var accept_contract_result := handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"quest_id": "contract_manual_drill",
	})
	var accepted_contract_entry := _find_contract_board_entry(handler.get_contract_board_window_data().get("entries", []), "contract_manual_drill")
	_assert_true(bool(accept_contract_result.get("ok", false)), "任务板提交应保持据点动作链路可执行。")
	_assert_true(runtime._party_state.has_active_quest(&"contract_manual_drill"), "任务板接取后应把任务写入 PartyState.active_quests。")
	_assert_eq(runtime._active_modal_id, "contract_board", "接取契约后应继续停留在 contract_board modal。")
	_assert_eq(runtime.accepted_quest_calls.size(), 1, "任务板接取应调用正式 quest accept 命令。")
	_assert_true(not bool(runtime.accepted_quest_calls[0].get("allow_reaccept", true)), "普通契约接取不应启用 repeatable 重接参数。")
	_assert_eq(runtime._current_status_message, "已接取任务《训练记录》。", "任务板接取后应更新成功反馈。")
	_assert_eq(String(accepted_contract_entry.get("state_id", "")), "active", "接取后的契约条目应刷新为 active。")
	_assert_eq(String(handler.get_contract_board_window_data().get("summary_text", "")), "已接取任务《训练记录》。", "任务板 summary_text 应刷新为最新反馈。")

	handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"quest_id": "contract_manual_drill",
	})
	_assert_eq(runtime.accepted_quest_calls.size(), 2, "重复提交同一契约时仍应调用正式 quest accept 命令。")
	_assert_eq(runtime._current_status_message, "任务《训练记录》已在进行中，不能重复接取。", "重复接取时应返回明确反馈。")

	_assert_true(runtime._party_state.mark_quest_completed(&"contract_manual_drill", runtime.world_step), "测试前置：普通契约应能标记完成。")
	handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"quest_id": "contract_manual_drill",
	})
	_assert_eq(runtime.accepted_quest_calls.size(), 3, "已完成契约再次提交时仍应经过正式 quest accept 命令。")
	_assert_eq(runtime._current_status_message, "任务《训练记录》已完成，当前不可再次接取。", "已完成非 repeatable 契约应返回明确反馈。")
	_assert_true(not runtime._party_state.has_active_quest(&"contract_manual_drill"), "已完成非 repeatable 契约不应重新回到 active_quests。")

	var repeatable_quest := QuestState.new()
	repeatable_quest.quest_id = &"contract_repeatable_patrol"
	repeatable_quest.mark_accepted(runtime.world_step)
	runtime._party_state.set_active_quest_state(repeatable_quest)
	_assert_true(runtime._party_state.mark_quest_completed(&"contract_repeatable_patrol", runtime.world_step), "测试前置：repeatable 契约应先进入 completed 状态。")
	handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"quest_id": "contract_repeatable_patrol",
	})
	var repeatable_entry := _find_contract_board_entry(handler.get_contract_board_window_data().get("entries", []), "contract_repeatable_patrol")
	_assert_eq(runtime.accepted_quest_calls.size(), 4, "repeatable 契约提交时应复用正式 quest accept 命令。")
	_assert_true(bool(runtime.accepted_quest_calls[3].get("allow_reaccept", false)), "repeatable 契约应启用 allow_reaccept。")
	_assert_eq(runtime._current_status_message, "已重新接取任务《巡路值守》。", "repeatable 契约应给出重新接取反馈。")
	_assert_true(runtime._party_state.has_active_quest(&"contract_repeatable_patrol"), "repeatable 契约应重新进入 active_quests。")
	_assert_true(not runtime._party_state.has_completed_quest(&"contract_repeatable_patrol"), "repeatable 契约重新接取后应移出 completed_quest_ids。")
	_assert_eq(String(repeatable_entry.get("state_id", "")), "active", "repeatable 契约重新接取后条目应刷新为 active。")

	handler.on_contract_board_window_closed()
	_assert_eq(runtime._active_modal_id, "settlement", "关闭任务板后应返回 settlement modal。")
	_assert_eq(runtime._active_settlement_id, "spring_village_01", "关闭任务板后应继续保留当前据点。")

	var bounty_board_result := handler.command_execute_settlement_action("service:bounty_registry")
	var bounty_board_window_data := handler.get_contract_board_window_data()
	var bounty_board_entry_ids := _extract_contract_board_entry_ids(bounty_board_window_data.get("entries", []))
	_assert_true(bool(bounty_board_result.get("ok", false)), "悬赏署服务应复用 contract_board modal。")
	_assert_eq(runtime._active_modal_id, "contract_board", "悬赏署服务后仍应落到 contract_board modal。")
	_assert_eq(String(bounty_board_window_data.get("action_id", "")), "service:bounty_registry", "悬赏署 modal 应保留原始 action_id。")
	_assert_eq(String(bounty_board_window_data.get("provider_interaction_id", "")), "service_bounty_registry", "悬赏署 modal 应记录自己的 provider_interaction_id。")
	_assert_eq(bounty_board_entry_ids, ["contract_regional_bounty"], "悬赏署 modal 只应暴露自己的 bounty quest。")

	handler.on_contract_board_window_closed()
	handler.command_execute_settlement_action("service:contract_board")
	var reopened_contract_entry_ids := _extract_contract_board_entry_ids(handler.get_contract_board_window_data().get("entries", []))
	_assert_eq(reopened_contract_entry_ids, ["contract_first_hunt", "contract_manual_drill", "contract_repeatable_patrol"], "悬赏署 provider 不应污染正式 contract board 列表。")
	handler.on_contract_board_window_closed()

	var training_result := handler.command_execute_settlement_action("service:training", {
		"pending_character_rewards": [
			{
				"member_id": "hero",
				"source_type": "training",
				"source_id": "training",
				"source_label": "训练",
				"entries": [
					{
						"entry_type": "skill_mastery",
						"target_id": "warrior_heavy_strike",
						"amount": 1,
					},
				],
			},
		],
	})
	_assert_true(bool(training_result.get("ok", false)), "普通据点动作应执行成功。")
	_assert_true(runtime._active_settlement_feedback_text.find("训练") >= 0, "普通据点动作后应写入据点反馈文本。")
	_assert_true(runtime.pending_rewards.size() == 1, "带 pending_character_rewards 的据点动作应归并出待领奖励。")
	_assert_true(runtime._current_status_message.find("事务") >= 0, "普通据点动作完成后应刷新状态文案。")

	var quest_apply_count_before := runtime.applied_quest_event_batches.size()
	var quest_training_result := handler.execute_settlement_action("spring_village_01", "service:training", {
		"interaction_script_id": "training_service",
		"facility_name": "训练场",
		"npc_name": "教官",
		"service_type": "训练",
		"member_id": "hero",
		"quest_progress_events": [
			{
				"event_type": "accept",
				"quest_id": "contract_training",
			},
			{
				"event_type": "progress",
				"quest_id": "contract_training",
				"objective_id": "train_once",
				"progress_delta": 1,
				"target_value": 1,
				"settlement_id": "spring_village_01",
			},
		],
	})
	handler.on_settlement_action_requested("spring_village_01", "service:training", {
		"interaction_script_id": "training_service",
		"facility_name": "训练场",
		"npc_name": "教官",
		"service_type": "训练",
		"member_id": "hero",
		"quest_progress_events": quest_training_result.get("quest_progress_events", []),
	})
	var training_quest: QuestState = runtime._party_state.get_active_quest_state(&"contract_training")
	_assert_eq((quest_training_result.get("quest_progress_events", []) as Array).size(), 3, "据点服务结果应包含显式 quest_progress_events 与默认据点动作事件。")
	_assert_eq(runtime.applied_quest_event_batches.size(), quest_apply_count_before + 1, "成功据点动作后应把 quest_progress_events 应用到运行时。")
	_assert_true(training_quest != null, "据点动作应能把 quest_progress_events 写入 PartyState.active_quests。")
	if training_quest != null:
		_assert_eq(training_quest.get_objective_progress(&"train_once"), 1, "据点动作应推进任务目标进度。")

	var canonical_training_result := handler.execute_settlement_action("spring_village_01", "service:training", {
		"interaction_script_id": "training_service",
		"facility_name": "训练场",
		"npc_name": "教官",
		"service_type": "训练",
		"member_id": "hero",
		"pending_character_rewards": [
			{
				"member_id": "hero",
				"source_type": "training",
				"source_id": "training",
				"source_label": "训练",
				"entries": [
					{
						"entry_type": "skill_mastery",
						"target_id": "warrior_heavy_strike",
						"amount": 1,
					},
				],
			},
		],
	})
	_assert_true(bool(canonical_training_result.get("success", false)), "据点服务结果应成功。")
	_assert_true(canonical_training_result.has("pending_character_rewards"), "据点服务结果应包含 canonical pending_character_rewards。")
	_assert_true(canonical_training_result.has("service_side_effects"), "据点服务结果应包含 service_side_effects。")
	_assert_eq((canonical_training_result.get("pending_character_rewards", []) as Array).size(), 1, "据点服务结果应输出 canonical 奖励数组。")
	_assert_true(not canonical_training_result.has("pending_mastery_rewards"), "据点服务结果不应再输出 legacy pending_mastery_rewards。")
	_assert_true(not canonical_training_result.has("effects"), "据点服务结果不应再输出 legacy effects。")
	_assert_eq(int(canonical_training_result.get("gold_delta", 0)), 0, "普通据点服务不应修改金币字段。")

	runtime._party_state.gold = 200
	runtime._party_state.get_member_state(&"hero").current_hp = 10
	var rest_result := handler.execute_settlement_action("spring_village_01", "service:rest_full", {
		"interaction_script_id": "service_rest_full",
		"facility_name": "旅店",
		"npc_name": "店主",
		"service_type": "整备",
		"member_id": "hero",
	})
	_assert_true(bool(rest_result.get("success", false)), "整备服务应执行成功。")
	_assert_eq(runtime._party_state.gold, 150, "整备服务应扣除 50 金。")
	_assert_eq(runtime.world_step, 1, "整备服务应推进 1 点 world_step。")
	_assert_eq(runtime._party_state.get_member_state(&"hero").current_hp, 40, "整备服务应把当前生命恢复到上限。")
	_assert_eq(int(rest_result.get("gold_delta", 0)), -50, "整备服务结果应记录金币变化。")
	_assert_true((rest_result.get("service_side_effects", {}) as Dictionary).has("world_step_advanced"), "整备服务结果应记录 world_step_advanced。")
	_assert_true(not rest_result.has("effects"), "整备服务结果不应再输出 legacy effects。")

	var missing_result := handler.execute_settlement_action("missing_settlement", "service:training", {})
	_assert_true(not bool(missing_result.get("success", true)), "缺失据点时服务结果应失败。")
	_assert_true(missing_result.has("pending_character_rewards"), "失败结果也应包含 canonical pending_character_rewards。")
	_assert_true(missing_result.has("service_side_effects"), "失败结果也应包含 service_side_effects。")
	_assert_true(not missing_result.has("pending_mastery_rewards"), "失败结果也不应保留 legacy pending_mastery_rewards。")
	_assert_true(not missing_result.has("effects"), "失败结果也不应保留 legacy effects。")

	var stagecoach_result := handler.command_execute_settlement_action("service:stagecoach")
	_assert_true(bool(stagecoach_result.get("ok", false)), "驿站服务应能打开路线窗口。")
	_assert_eq(runtime._active_modal_id, "stagecoach", "打开驿站后应切换到驿站 modal。")
	var travel_result := handler.command_stagecoach_travel("graystone_town_01")
	_assert_true(bool(travel_result.get("ok", false)), "驿站换乘应执行成功。")
	_assert_eq(runtime._active_modal_id, "settlement", "驿站换乘后应回到目标据点窗口。")
	_assert_eq(runtime._active_settlement_id, "graystone_town_01", "驿站换乘后应记录目标据点。")
	_assert_eq(runtime._party_state.gold, 120, "驿站换乘应按距离扣除路费。")
	_assert_eq(runtime.player_persist_calls, 1, "驿站换乘后应持久化玩家坐标。")
	_assert_true(runtime.refresh_world_visibility_calls > 0, "驿站换乘后应刷新世界可见状态。")

	handler.on_settlement_window_closed()
	_assert_eq(runtime._active_settlement_id, "", "关闭据点窗口应清空当前据点 ID。")
	_assert_eq(runtime._active_settlement_feedback_text, "", "关闭据点窗口应清空反馈文本。")
	_assert_eq(runtime._active_modal_id, "", "关闭据点窗口应清空 modal。")
	_assert_true(runtime.present_reward_calls > 0, "关闭据点窗口后应尝试恢复待确认奖励。")
	_assert_eq(runtime._current_status_message, "已关闭据点窗口，返回世界地图。", "关闭据点窗口后应刷新状态文案。")


func _make_party_state() -> PartyState:
	var party_state := PartyState.new()
	party_state.leader_member_id = &"hero"
	party_state.active_member_ids = [&"hero"]

	var hero := PartyMemberState.new()
	hero.member_id = &"hero"
	hero.display_name = "Hero"
	hero.current_hp = 20
	hero.current_mp = 4
	party_state.member_states = {
		&"hero": hero,
	}
	return party_state


func _has_call(calls: Array[Dictionary], method_name: String) -> bool:
	for call in calls:
		if String(call.get("method", "")) == method_name:
			return true
	return false


func _find_service_entry(service_variants, action_id: String) -> Dictionary:
	if service_variants is not Array:
		return {}
	for service_variant in service_variants:
		if service_variant is Dictionary and String(service_variant.get("action_id", "")) == action_id:
			return (service_variant as Dictionary).duplicate(true)
	return {}


func _extract_contract_board_entry_ids(entry_variants) -> Array[String]:
	var result: Array[String] = []
	if entry_variants is not Array:
		return result
	for entry_variant in entry_variants:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		result.append(String(entry.get("quest_id", entry.get("entry_id", ""))))
	return result


func _find_contract_board_entry(entry_variants, quest_id: String) -> Dictionary:
	if entry_variants is not Array:
		return {}
	for entry_variant in entry_variants:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("quest_id", entry.get("entry_id", ""))) == quest_id:
			return entry.duplicate(true)
	return {}


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
