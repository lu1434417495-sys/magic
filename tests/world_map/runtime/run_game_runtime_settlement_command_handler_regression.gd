extends SceneTree

const GameRuntimeFacade = preload("res://scripts/systems/game_runtime_facade.gd")
const GameRuntimeSettlementCommandHandler = preload("res://scripts/systems/game_runtime_settlement_command_handler.gd")
const GameSessionScript = preload("res://scripts/systems/game_session.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")
const UnitSkillProgress = preload("res://scripts/player/progression/unit_skill_progress.gd")

const TEST_CONFIG_PATH := "res://data/configs/world_map/test_world_map_config.tres"

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
	var claimed_quest_calls: Array[Dictionary] = []
	var submitted_quest_item_calls: Array[Dictionary] = []
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
		if _party_state.has_claimable_quest(quest_id):
			_current_status_message = "任务《%s》已完成，奖励待领取，当前不可再次接取。" % quest_label
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

	func command_claim_quest(quest_id: StringName) -> Dictionary:
		claimed_quest_calls.append({
			"quest_id": String(quest_id),
		})
		var quest_data: Dictionary = _quest_defs.get(quest_id, _quest_defs.get(String(quest_id), {})).duplicate(true)
		var quest_label := String(quest_data.get("display_name", quest_id))
		if quest_data.is_empty():
			_current_status_message = "未找到任务 %s。" % String(quest_id)
			return build_command_error(_current_status_message)
		if not _party_state.has_claimable_quest(quest_id):
			_current_status_message = "当前没有可领取的任务《%s》奖励。" % quest_label
			return build_command_error(_current_status_message)
		var gold_delta := 0
		var reward_entries_variant = quest_data.get("reward_entries", [])
		if reward_entries_variant is Array:
			for reward_variant in reward_entries_variant:
				if reward_variant is not Dictionary:
					continue
				var reward_data := reward_variant as Dictionary
				if String(reward_data.get("reward_type", "")) != "gold":
					continue
				gold_delta += maxi(int(reward_data.get("amount", 0)), 0)
		if not _party_state.mark_quest_reward_claimed(quest_id, world_step):
			_current_status_message = "当前无法领取任务《%s》奖励。" % quest_label
			return build_command_error(_current_status_message)
		if gold_delta > 0:
			_party_state.add_gold(gold_delta)
			_current_status_message = "已领取任务《%s》奖励，获得 %d 金。" % [quest_label, gold_delta]
		else:
			_current_status_message = "已领取任务《%s》奖励。" % quest_label
		var result := build_command_ok(_current_status_message)
		result["gold_delta"] = gold_delta
		return result

	func command_submit_quest_item(quest_id: StringName, objective_id: StringName = &"") -> Dictionary:
		submitted_quest_item_calls.append({
			"quest_id": String(quest_id),
			"objective_id": String(objective_id),
		})
		var quest_data: Dictionary = _quest_defs.get(quest_id, _quest_defs.get(String(quest_id), {})).duplicate(true)
		var quest_label := String(quest_data.get("display_name", quest_id))
		var active_quest: QuestState = _party_state.get_active_quest_state(quest_id)
		if quest_data.is_empty() or active_quest == null:
			_current_status_message = "当前没有进行中的任务《%s》。" % quest_label
			return build_command_error(_current_status_message)
		var target_value := 1
		var item_id := &""
		for objective_variant in quest_data.get("objective_defs", []):
			if objective_variant is not Dictionary:
				continue
			var objective_data := objective_variant as Dictionary
			if ProgressionDataUtils.to_string_name(objective_data.get("objective_id", "")) != objective_id:
				continue
			target_value = maxi(int(objective_data.get("target_value", 1)), 1)
			item_id = ProgressionDataUtils.to_string_name(objective_data.get("target_id", ""))
			active_quest.record_objective_progress(objective_id, target_value, target_value, {
				"item_id": String(item_id),
			})
			break
		_party_state.mark_quest_completed(quest_id, world_step)
		_current_status_message = "已为任务《%s》提交 铁矿石 x2，奖励待领取。" % quest_label
		var result := build_command_ok(_current_status_message)
		result["objective_id"] = String(objective_id)
		result["item_id"] = String(item_id)
		result["submitted_quantity"] = target_value
		return result

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
			"claimable_quest_ids": [],
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
						(summary["claimable_quest_ids"] as Array).append(quest_id)
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
	_test_settlement_handler_routes_research_service()
	_test_settlement_handler_routes_actions_and_modal_state()
	await _test_world_generation_exposes_research_service()

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


func _test_settlement_handler_routes_research_service() -> void:
	var runtime := MockRuntime.new()
	runtime._party_state = _make_party_state()
	runtime._party_state.gold = 250
	runtime._selected_settlement = {
		"settlement_id": "graystone_town_01",
	}
	runtime._settlements_by_id = {
		"graystone_town_01": {
			"settlement_id": "graystone_town_01",
			"display_name": "灰石镇",
			"origin": Vector2i.ZERO,
			"available_services": [
				{
					"action_id": "service:research",
					"facility_name": "大图书馆",
					"npc_name": "大图书官",
					"service_type": "研究",
					"interaction_script_id": "service_research",
				},
			],
		},
	}
	runtime._settlement_states = {
		"graystone_town_01": {
			"visited": true,
			"reputation": 0,
			"active_conditions": [],
			"cooldowns": {},
			"shop_inventory_seed": 0,
			"shop_last_refresh_step": 0,
			"shop_states": {},
		},
	}

	var handler := GameRuntimeSettlementCommandHandler.new()
	handler.setup(runtime)

	var settlement_window_data := handler.get_settlement_window_data("graystone_town_01")
	var research_service := _find_service_entry(settlement_window_data.get("available_services", []), "service:research")
	_assert_true(not research_service.is_empty(), "据点窗口应暴露正式 research 服务入口。")
	_assert_eq(String(research_service.get("interaction_script_id", "")), "service_research", "research 服务应使用正式 interaction_script_id。")
	_assert_true(bool(research_service.get("is_enabled", false)), "金币充足时 research 服务入口应可点击。")
	_assert_eq(String(research_service.get("cost_label", "")), "200 金", "research 服务应暴露正式金币成本。")

	var research_result := handler.command_execute_settlement_action("service:research")
	_assert_true(bool(research_result.get("ok", false)), "research 服务应能走正式 settlement action dispatch。")
	_assert_eq(runtime._party_state.get_gold(), 50, "research 服务成功后应扣除正式研究成本。")
	_assert_eq(runtime._active_modal_id, "settlement", "research 服务不应切走当前 settlement modal。")
	_assert_true(runtime._active_settlement_feedback_text.find("研究") >= 0, "research 服务应写入正式据点反馈。")
	_assert_true(runtime._current_status_message.find("研究") >= 0, "research 服务应刷新状态文案。")
	_assert_true(runtime._current_status_message.find("尚未开放") == -1, "research 服务不应继续使用未开放占位文案。")
	_assert_eq(runtime.persist_calls, 1, "research 服务成功后应持久化队伍状态。")
	_assert_eq(runtime.sync_party_calls, 1, "research 服务成功后应同步角色管理侧队伍状态。")
	_assert_eq(runtime.achievement_events.size(), 1, "research 服务成功后应记录据点动作成就事件。")
	_assert_eq(runtime.achievement_events[0].get("detail_id", ""), "service:research", "research 成就事件应记录正式 action_id。")
	_assert_eq(runtime.applied_quest_event_batches.size(), 1, "research 服务成功后应仍走默认 quest progress 事件链。")
	_assert_eq(runtime.pending_rewards.size(), 1, "research 服务成功后应正式排入 pending_character_rewards。")
	var first_research_reward: Dictionary = runtime.pending_rewards[0].duplicate(true) if runtime.pending_rewards.size() > 0 and runtime.pending_rewards[0] is Dictionary else {}
	_assert_eq(String(first_research_reward.get("member_id", "")), "hero", "research 奖励应写入目标成员。")
	_assert_eq(String(first_research_reward.get("member_name", "")), "Hero", "research 奖励应保留成员显示名。")
	_assert_eq(String(first_research_reward.get("source_type", "")), "npc_teach", "research 奖励应沿用正式 source_type 命名。")
	_assert_eq(String(first_research_reward.get("source_id", "")), "research_field_manual", "知识型 research 奖励应写入具体来源 ID。")
	_assert_eq(String(first_research_reward.get("source_label", "")), "大图书官·研究", "research 奖励应沿用正式 source_label 命名。")
	var first_reward_entry := _get_first_reward_entry(first_research_reward)
	_assert_eq(String(first_reward_entry.get("entry_type", "")), "knowledge_unlock", "首条 research 奖励应先构造成知识奖励。")
	_assert_eq(String(first_reward_entry.get("target_id", "")), "field_manual", "首条 research 奖励应指向野外手册知识。")

	var refreshed_window_data := handler.get_settlement_window_data("graystone_town_01")
	var refreshed_research_service := _find_service_entry(refreshed_window_data.get("available_services", []), "service:research")
	_assert_true(not bool(refreshed_research_service.get("is_enabled", true)), "扣费后金币不足时 research 服务应及时禁用。")
	_assert_eq(String(refreshed_research_service.get("disabled_reason", "")), "金币不足", "research 服务禁用原因应明确显示金币不足。")

	runtime._party_state.get_member_state(&"hero").progression.learn_knowledge(&"field_manual")
	runtime._party_state.gold = 250
	var reenabled_window_data := handler.get_settlement_window_data("graystone_town_01")
	var reenabled_research_service := _find_service_entry(reenabled_window_data.get("available_services", []), "service:research")
	_assert_true(bool(reenabled_research_service.get("is_enabled", false)), "已有下一条研究内容时，补足金币后 research 服务应重新可用。")

	var second_research_result := handler.command_execute_settlement_action("service:research")
	_assert_true(bool(second_research_result.get("ok", false)), "第二次 research 服务应继续走正式 settlement action dispatch。")
	_assert_eq(runtime.pending_rewards.size(), 2, "第二次 research 服务应继续把奖励排入队列。")
	var second_research_reward: Dictionary = runtime.pending_rewards[1].duplicate(true) if runtime.pending_rewards.size() > 1 and runtime.pending_rewards[1] is Dictionary else {}
	_assert_eq(String(second_research_reward.get("source_type", "")), "npc_teach", "技能型 research 奖励也应沿用正式 source_type 命名。")
	_assert_eq(String(second_research_reward.get("source_id", "")), "research_guard_break", "技能型 research 奖励应写入具体来源 ID。")
	_assert_eq(String(second_research_reward.get("source_label", "")), "大图书官·研究", "技能型 research 奖励应保留统一来源标签。")
	var second_reward_entry := _get_first_reward_entry(second_research_reward)
	_assert_eq(String(second_reward_entry.get("entry_type", "")), "skill_unlock", "第二条 research 奖励应构造成技能奖励。")
	_assert_eq(String(second_reward_entry.get("target_id", "")), "warrior_guard_break", "第二条 research 奖励应指向裂甲斩技能。")

	var guard_break_progress := UnitSkillProgress.new()
	guard_break_progress.skill_id = &"warrior_guard_break"
	guard_break_progress.is_learned = true
	runtime._party_state.get_member_state(&"hero").progression.set_skill_progress(guard_break_progress)
	runtime._party_state.gold = 250
	var exhausted_window_data := handler.get_settlement_window_data("graystone_town_01")
	var exhausted_research_service := _find_service_entry(exhausted_window_data.get("available_services", []), "service:research")
	_assert_true(not bool(exhausted_research_service.get("is_enabled", true)), "没有剩余研究内容时 research 服务应禁用。")
	_assert_eq(String(exhausted_research_service.get("disabled_reason", "")), "暂无可研究内容", "研究内容耗尽时应给出明确禁用原因。")


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
		&"contract_supply_drop": {
			"quest_id": "contract_supply_drop",
			"display_name": "物资缴纳",
			"description": "向任务板提交两份铁矿石。",
			"provider_interaction_id": "service_contract_board",
			"objective_defs": [
				{
					"objective_id": "deliver_ore",
					"objective_type": "submit_item",
					"target_id": "iron_ore",
					"target_value": 2,
				},
			],
			"reward_entries": [
				{"reward_type": "gold", "amount": 18},
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
	_assert_eq(contract_board_entry_ids, ["contract_first_hunt", "contract_manual_drill", "contract_repeatable_patrol", "contract_supply_drop"], "任务板 modal 只应按 provider_interaction_id 暴露当前服务的契约条目。")
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
	var manual_claim_gold_before: int = runtime._party_state.get_gold()
	handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"quest_id": "contract_manual_drill",
	})
	var claimed_contract_entry := _find_contract_board_entry(handler.get_contract_board_window_data().get("entries", []), "contract_manual_drill")
	_assert_eq(runtime.accepted_quest_calls.size(), 2, "领取 claimable 契约时不应重复走 quest accept 命令。")
	_assert_eq(runtime.claimed_quest_calls.size(), 1, "claimable 契约提交时应走正式 quest claim 命令。")
	_assert_eq(runtime._current_status_message, "已领取任务《训练记录》奖励，获得 30 金。", "claimable 契约提交时应返回领奖反馈。")
	_assert_eq(runtime._party_state.get_gold(), manual_claim_gold_before + 30, "claimable 契约提交后应把金币奖励写入 PartyState。")
	_assert_true(not runtime._party_state.has_active_quest(&"contract_manual_drill"), "已完成非 repeatable 契约不应重新回到 active_quests。")
	_assert_true(not runtime._party_state.has_claimable_quest(&"contract_manual_drill"), "领奖后的非 repeatable 契约不应继续停留在 claimable_quests。")
	_assert_true(runtime._party_state.has_completed_quest(&"contract_manual_drill"), "领奖后的非 repeatable 契约应进入 completed_quest_ids。")
	_assert_eq(String(claimed_contract_entry.get("state_id", "")), "completed", "领奖后的普通契约条目应刷新为 completed。")

	var repeatable_quest := QuestState.new()
	repeatable_quest.quest_id = &"contract_repeatable_patrol"
	repeatable_quest.mark_accepted(runtime.world_step)
	runtime._party_state.set_active_quest_state(repeatable_quest)
	_assert_true(runtime._party_state.mark_quest_completed(&"contract_repeatable_patrol", runtime.world_step), "测试前置：repeatable 契约应先进入待领奖励状态。")
	var repeatable_claim_gold_before: int = runtime._party_state.get_gold()
	handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"quest_id": "contract_repeatable_patrol",
	})
	var repeatable_entry := _find_contract_board_entry(handler.get_contract_board_window_data().get("entries", []), "contract_repeatable_patrol")
	_assert_eq(runtime.accepted_quest_calls.size(), 2, "repeatable 契约领奖时不应误走 quest accept。")
	_assert_eq(runtime.claimed_quest_calls.size(), 2, "repeatable 契约待领奖励时应走正式 quest claim 命令。")
	_assert_eq(runtime._current_status_message, "已领取任务《巡路值守》奖励，获得 15 金。", "repeatable 契约领奖时应返回明确反馈。")
	_assert_eq(runtime._party_state.get_gold(), repeatable_claim_gold_before + 15, "repeatable 契约领奖后应增加金币。")
	_assert_true(not runtime._party_state.has_active_quest(&"contract_repeatable_patrol"), "repeatable 契约待领奖励时不应重新进入 active_quests。")
	_assert_true(not runtime._party_state.has_claimable_quest(&"contract_repeatable_patrol"), "repeatable 契约领奖后应离开 claimable_quests。")
	_assert_true(runtime._party_state.has_completed_quest(&"contract_repeatable_patrol"), "repeatable 契约领奖后应进入 completed_quest_ids。")
	_assert_eq(String(repeatable_entry.get("state_id", "")), "repeatable", "repeatable 契约领奖后条目应刷新为 repeatable。")

	var submit_item_quest := QuestState.new()
	submit_item_quest.quest_id = &"contract_supply_drop"
	submit_item_quest.mark_accepted(runtime.world_step)
	runtime._party_state.set_active_quest_state(submit_item_quest)
	handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"quest_id": "contract_supply_drop",
	})
	var submit_item_entry := _find_contract_board_entry(handler.get_contract_board_window_data().get("entries", []), "contract_supply_drop")
	_assert_eq(runtime.accepted_quest_calls.size(), 2, "active submit_item 契约提交时不应再走 quest accept。")
	_assert_eq(runtime.submitted_quest_item_calls.size(), 1, "active submit_item 契约提交时应走正式 quest submit_item 命令。")
	_assert_eq(String(runtime.submitted_quest_item_calls[0].get("objective_id", "")), "deliver_ore", "submit_item 路由时应带上未完成 objective_id。")
	_assert_eq(runtime._current_status_message, "已为任务《物资缴纳》提交 铁矿石 x2，奖励待领取。", "submit_item 提交后应刷新正式反馈。")
	_assert_true(not runtime._party_state.has_active_quest(&"contract_supply_drop"), "submit_item 提交完成后任务应离开 active_quests。")
	_assert_true(runtime._party_state.has_claimable_quest(&"contract_supply_drop"), "submit_item 提交完成后任务应进入 claimable_quests。")
	_assert_eq(String(submit_item_entry.get("state_id", "")), "claimable", "submit_item 提交后条目应刷新为 claimable。")

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
	_assert_eq(reopened_contract_entry_ids, ["contract_first_hunt", "contract_manual_drill", "contract_repeatable_patrol", "contract_supply_drop"], "悬赏署 provider 不应污染正式 contract board 列表。")
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


func _test_world_generation_exposes_research_service() -> void:
	var game_session := GameSessionScript.new()
	game_session.name = "ResearchRouteGameSession"
	root.add_child(game_session)
	await process_frame

	var create_error := int(game_session.create_new_save(TEST_CONFIG_PATH, &"research_route_service", "研究入口验证"))
	_assert_eq(create_error, OK, "创建 research 入口验证世界应成功。")
	if create_error == OK:
		var world_data: Dictionary = game_session.get_world_data()
		var found_research_service: Dictionary = {}
		var found_legacy_unlock_archive := false
		for settlement_variant in world_data.get("settlements", []):
			if settlement_variant is not Dictionary:
				continue
			for service_variant in (settlement_variant as Dictionary).get("available_services", []):
				if service_variant is not Dictionary:
					continue
				var service_data: Dictionary = service_variant
				var interaction_script_id := String(service_data.get("interaction_script_id", ""))
				if interaction_script_id == "service_research" and found_research_service.is_empty():
					found_research_service = service_data.duplicate(true)
				if interaction_script_id == "service_unlock_archive":
					found_legacy_unlock_archive = true
		_assert_true(not found_research_service.is_empty(), "正式 world config 生成结果应包含 research 服务入口。")
		_assert_eq(String(found_research_service.get("action_id", "")), "service:research", "research 服务应映射到正式 action_id。")
		_assert_true(not found_legacy_unlock_archive, "research 服务不应继续使用 legacy service_unlock_archive 入口。")

	var clear_error := int(game_session.clear_persisted_game())
	_assert_eq(clear_error, OK, "清理 research 入口验证存档应成功。")
	if game_session.get_parent() != null:
		game_session.get_parent().remove_child(game_session)
	game_session.free()
	await process_frame


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


func _get_first_reward_entry(reward_data) -> Dictionary:
	if reward_data is not Dictionary:
		return {}
	var entries_variant = (reward_data as Dictionary).get("entries", [])
	if entries_variant is not Array or (entries_variant as Array).is_empty():
		return {}
	var first_entry = (entries_variant as Array)[0]
	return (first_entry as Dictionary).duplicate(true) if first_entry is Dictionary else {}


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
