extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GameRuntimeFacade = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const GameRuntimeSettlementCommandHandler = preload("res://scripts/systems/game_runtime/game_runtime_settlement_command_handler.gd")
const SettlementShopService = preload("res://scripts/systems/settlement/settlement_shop_service.gd")
const GameSessionScript = preload("res://scripts/systems/persistence/game_session.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PendingCharacterReward = preload("res://scripts/systems/progression/pending_character_reward.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")
const UnitSkillProgress = preload("res://scripts/player/progression/unit_skill_progress.gd")

const TEST_CONFIG_PATH := "res://data/configs/world_map/test_world_map_config.tres"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


class MockAttributeSnapshot:
	extends RefCounted

	var values: Dictionary = {}

	func get_value(attribute_id: StringName) -> int:
		return int(values.get(attribute_id, 0))


class MockFogSystem:
	extends RefCounted

	var visible := true

	func is_visible(_coord: Vector2i, _faction_id: String) -> bool:
		return visible


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
	var _active_forge_context: Dictionary = {}
	var _active_stagecoach_context: Dictionary = {}
	var _fog_system := MockFogSystem.new()
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
	var clear_settlement_entry_context_calls := 0

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
		if _active_settlement_id.is_empty():
			return String(_selected_settlement.get("settlement_id", ""))
		return _active_settlement_id

	func set_active_settlement_id(settlement_id: String) -> void:
		_active_settlement_id = settlement_id

	func set_settlement_feedback_text(feedback_text: String) -> void:
		_active_settlement_feedback_text = feedback_text

	func set_runtime_active_modal_id(modal_id: String) -> void:
		_active_modal_id = modal_id

	func get_active_modal_id() -> String:
		return _active_modal_id

	func set_player_coord(coord: Vector2i) -> void:
		_player_coord = coord

	func set_selected_coord(coord: Vector2i) -> void:
		_selected_coord = coord

	func clear_settlement_entry_context(_reset_selected: bool = true) -> void:
		clear_settlement_entry_context_calls += 1

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

	func set_active_forge_context(context: Dictionary) -> void:
		_active_forge_context = context.duplicate(true)

	func clear_active_forge_context() -> void:
		_active_forge_context.clear()

	func get_active_forge_context() -> Dictionary:
		return _active_forge_context.duplicate(true)

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

	func get_item_display_name(item_id: StringName) -> String:
		match String(item_id):
			"iron_ore":
				return "铁矿石"
			_:
				return String(item_id)

	func get_quest_defs() -> Dictionary:
		return _quest_defs.duplicate(true)

	func get_world_step() -> int:
		return world_step

	func get_fog_system():
		return _fog_system

	func get_player_faction_id() -> String:
		return "player"

	func advance_world_time_by_steps(delta_steps: int) -> void:
		world_step += maxi(delta_steps, 0)

	func command_accept_quest(quest_id: StringName, allow_reaccept: bool = false) -> Dictionary:
		accepted_quest_calls.append({
			"quest_id": String(quest_id),
			"allow_reaccept": allow_reaccept,
		})
		var quest_data: Dictionary = _quest_defs.get(quest_id, {}).duplicate(true)
		if quest_data.is_empty():
			_current_status_message = "未找到任务 %s。" % String(quest_id)
			return build_command_error(_current_status_message)
		var quest_label := String(quest_data["display_name"])
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
		var quest_data: Dictionary = _quest_defs.get(quest_id, {}).duplicate(true)
		if quest_data.is_empty():
			_current_status_message = "未找到任务 %s。" % String(quest_id)
			return build_command_error(_current_status_message)
		var quest_label := String(quest_data["display_name"])
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
		var quest_data: Dictionary = _quest_defs.get(quest_id, {}).duplicate(true)
		if quest_data.is_empty():
			_current_status_message = "当前没有进行中的任务《%s》。" % String(quest_id)
			return build_command_error(_current_status_message)
		var quest_label := String(quest_data["display_name"])
		var active_quest: QuestState = _party_state.get_active_quest_state(quest_id)
		if active_quest == null:
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
		for reward_variant in reward_variants:
			var reward = PendingCharacterReward.from_variant(reward_variant)
			if reward == null:
				continue
			_party_state.enqueue_pending_character_reward(reward)

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


class MockShopItemDef:
	extends RefCounted

	var display_name := ""
	var description := ""
	var icon := ""
	var sellable := true
	var buy_price := 0
	var sell_price := 0

	func get_buy_price(price_basis_points: int = 10000) -> int:
		return int((maxi(int(buy_price), 0) * maxi(int(price_basis_points), 0) + 5000) / 10000)

	func get_sell_price(_price_basis_points: int = 10000) -> int:
		return maxi(int(sell_price), 0)


class MockShopWarehouseService:
	extends RefCounted

	var inventory_entries: Array[Dictionary] = []
	var counts: Dictionary = {}
	var added_items: Array[Dictionary] = []
	var removed_items: Array[Dictionary] = []

	func get_inventory_entries() -> Array[Dictionary]:
		var entries: Array[Dictionary] = []
		for entry in inventory_entries:
			entries.append(entry.duplicate(true))
		return entries

	func preview_add_item(item_id: StringName, quantity: int) -> Dictionary:
		return {
			"item_id": String(item_id),
			"requested_quantity": quantity,
			"added_quantity": quantity,
			"remaining_quantity": 0,
		}

	func add_item(item_id: StringName, quantity: int) -> Dictionary:
		added_items.append({
			"item_id": String(item_id),
			"quantity": quantity,
		})
		counts[item_id] = int(counts.get(item_id, 0)) + quantity
		return {
			"item_id": String(item_id),
			"requested_quantity": quantity,
			"added_quantity": quantity,
			"remaining_quantity": 0,
		}

	func count_item(item_id: StringName) -> int:
		return int(counts.get(item_id, 0))

	func remove_item(item_id: StringName, quantity: int) -> Dictionary:
		var available_quantity := count_item(item_id)
		var removed_quantity := mini(maxi(quantity, 0), available_quantity)
		counts[item_id] = available_quantity - removed_quantity
		removed_items.append({
			"item_id": String(item_id),
			"quantity": removed_quantity,
		})
		return {
			"item_id": String(item_id),
			"requested_quantity": quantity,
			"removed_quantity": removed_quantity,
			"remaining_quantity": maxi(quantity - removed_quantity, 0),
		}


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_facade_delegates_settlement_surface_to_handler()
	_test_settlement_handler_routes_research_service()
	_test_settlement_handler_routes_actions_and_modal_state()
	_test_settlement_shop_service_rejects_bad_entry_schema()
	_test_settlement_handler_rejects_invalid_or_spoofed_actions()
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

	runtime._party_state.gold = 250
	var reenabled_window_data := handler.get_settlement_window_data("graystone_town_01")
	var reenabled_research_service := _find_service_entry(reenabled_window_data.get("available_services", []), "service:research")
	_assert_true(bool(reenabled_research_service.get("is_enabled", false)), "首条 research 奖励尚未确认时，也应切到下一条可研究内容，而不是重复给野外手册。")

	var second_research_result := handler.command_execute_settlement_action("service:research")
	_assert_true(bool(second_research_result.get("ok", false)), "第二次 research 服务应继续走正式 settlement action dispatch。")
	_assert_eq(runtime.pending_rewards.size(), 2, "第二次 research 服务应继续把奖励排入队列。")
	_assert_eq(runtime._party_state.pending_character_rewards.size(), 2, "research 正式链路应把待领奖励同步写回 party_state。")
	var second_research_reward: Dictionary = runtime.pending_rewards[1].duplicate(true) if runtime.pending_rewards.size() > 1 and runtime.pending_rewards[1] is Dictionary else {}
	_assert_eq(String(second_research_reward.get("source_type", "")), "npc_teach", "技能型 research 奖励也应沿用正式 source_type 命名。")
	_assert_eq(String(second_research_reward.get("source_id", "")), "research_guard_break", "技能型 research 奖励应写入具体来源 ID。")
	_assert_eq(String(second_research_reward.get("source_label", "")), "大图书官·研究", "技能型 research 奖励应保留统一来源标签。")
	var second_reward_entry := _get_first_reward_entry(second_research_reward)
	_assert_eq(String(second_reward_entry.get("entry_type", "")), "skill_unlock", "第二条 research 奖励应构造成技能奖励。")
	_assert_eq(String(second_reward_entry.get("target_id", "")), "warrior_guard_break", "第二条 research 奖励应指向裂甲斩技能。")

	runtime._party_state.gold = 250
	var exhausted_window_data := handler.get_settlement_window_data("graystone_town_01")
	var exhausted_research_service := _find_service_entry(exhausted_window_data.get("available_services", []), "service:research")
	_assert_true(not bool(exhausted_research_service.get("is_enabled", true)), "同成员两条 research 奖励都已挂入 pending 队列后，服务应禁用。")
	_assert_eq(String(exhausted_research_service.get("disabled_reason", "")), "暂无可研究内容", "research 已被 pending 队列占满时应给出明确禁用原因。")


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
		&"contract_missing_display_name": {
			"quest_id": "contract_missing_display_name",
			"description": "缺少 display_name 的坏契约不应显示。",
			"provider_interaction_id": "service_contract_board",
			"objective_defs": [
				{
					"objective_id": "bad_missing_name",
					"objective_type": "defeat_enemy",
					"target_id": "",
					"target_value": 1,
				},
			],
			"reward_entries": [
				{"reward_type": "gold", "amount": 1},
			],
		},
		&"contract_missing_description": {
			"quest_id": "contract_missing_description",
			"display_name": "缺说明契约",
			"provider_interaction_id": "service_contract_board",
			"objective_defs": [
				{
					"objective_id": "bad_missing_description",
					"objective_type": "defeat_enemy",
					"target_id": "",
					"target_value": 1,
				},
			],
			"reward_entries": [
				{"reward_type": "gold", "amount": 1},
			],
		},
		&"contract_missing_objectives": {
			"quest_id": "contract_missing_objectives",
			"display_name": "缺目标契约",
			"description": "缺少 objective_defs 的坏契约不应显示。",
			"provider_interaction_id": "service_contract_board",
			"reward_entries": [
				{"reward_type": "gold", "amount": 1},
			],
		},
		&"contract_missing_objective_target": {
			"quest_id": "contract_missing_objective_target",
			"display_name": "缺目标对象契约",
			"description": "据点事务目标缺少 target_id 时不应回退成未命名。",
			"provider_interaction_id": "service_contract_board",
			"objective_defs": [
				{
					"objective_id": "bad_missing_target",
					"objective_type": "settlement_action",
					"target_id": "",
					"target_value": 1,
				},
			],
			"reward_entries": [
				{"reward_type": "gold", "amount": 1},
			],
		},
		&"contract_unknown_objective_type": {
			"quest_id": "contract_unknown_objective_type",
			"display_name": "未知目标契约",
			"description": "未知 objective_type 不应直接显示 objective_id。",
			"provider_interaction_id": "service_contract_board",
			"objective_defs": [
				{
					"objective_id": "bad_unknown_objective",
					"objective_type": "legacy_custom",
					"target_id": "legacy_target",
					"target_value": 1,
				},
			],
			"reward_entries": [
				{"reward_type": "gold", "amount": 1},
			],
		},
		&"contract_missing_rewards": {
			"quest_id": "contract_missing_rewards",
			"display_name": "缺奖励契约",
			"description": "缺少 reward_entries 的坏契约不应显示。",
			"provider_interaction_id": "service_contract_board",
			"objective_defs": [
				{
					"objective_id": "bad_missing_rewards",
					"objective_type": "defeat_enemy",
					"target_id": "",
					"target_value": 1,
				},
			],
		},
		&"contract_invalid_reward_amount": {
			"quest_id": "contract_invalid_reward_amount",
			"display_name": "坏奖励契约",
			"description": "非法奖励数值不应回退成奖励待定。",
			"provider_interaction_id": "service_contract_board",
			"objective_defs": [
				{
					"objective_id": "bad_reward_amount",
					"objective_type": "defeat_enemy",
					"target_id": "",
					"target_value": 1,
				},
			],
			"reward_entries": [
				{"reward_type": "gold", "amount": 0},
			],
		},
		"contract_string_key_only": {
			"quest_id": "contract_string_key_only",
			"display_name": "旧 String key 契约",
			"description": "用于验证任务板不再按 String key 恢复契约。",
			"provider_interaction_id": "service_contract_board",
			"objective_defs": [
				{
					"objective_id": "string_key_objective",
					"objective_type": "settlement_action",
					"target_id": "service:training",
					"target_value": 1,
				},
			],
			"reward_entries": [
				{"reward_type": "gold", "amount": 1},
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
	runtime._active_modal_id = "settlement"
	runtime._active_settlement_id = "spring_village_01"

	var contract_board_result := handler.command_execute_settlement_action("service:contract_board")
	var contract_board_window_data := handler.get_contract_board_window_data()
	var contract_board_entry_ids := _extract_contract_board_entry_ids(contract_board_window_data.get("entries", []))
	_assert_true(bool(contract_board_result.get("ok", false)), "任务板服务应能切换到 contract_board modal。")
	_assert_eq(runtime._active_modal_id, "contract_board", "任务板服务后应切换到 contract_board modal。")
	_assert_eq(String(contract_board_window_data.get("action_id", "")), "service:contract_board", "任务板 modal 应保留原始 action_id。")
	_assert_eq(String(contract_board_window_data.get("provider_interaction_id", "")), "service_contract_board", "任务板 modal 应记录当前 provider_interaction_id。")
	_assert_eq(contract_board_entry_ids, ["contract_first_hunt", "contract_manual_drill", "contract_repeatable_patrol", "contract_supply_drop"], "任务板 modal 只应按 provider_interaction_id 暴露当前服务的契约条目。")
	var missing_name_entry := _find_contract_board_entry(contract_board_window_data.get("entries", []), "contract_missing_display_name")
	var missing_description_entry := _find_contract_board_entry(contract_board_window_data.get("entries", []), "contract_missing_description")
	var missing_objectives_entry := _find_contract_board_entry(contract_board_window_data.get("entries", []), "contract_missing_objectives")
	var missing_objective_target_entry := _find_contract_board_entry(contract_board_window_data.get("entries", []), "contract_missing_objective_target")
	var unknown_objective_type_entry := _find_contract_board_entry(contract_board_window_data.get("entries", []), "contract_unknown_objective_type")
	var missing_rewards_entry := _find_contract_board_entry(contract_board_window_data.get("entries", []), "contract_missing_rewards")
	var invalid_reward_amount_entry := _find_contract_board_entry(contract_board_window_data.get("entries", []), "contract_invalid_reward_amount")
	var string_key_only_entry := _find_contract_board_entry(contract_board_window_data.get("entries", []), "contract_string_key_only")
	_assert_true(missing_name_entry.is_empty(), "缺少 display_name 的契约不应回退成 quest_id 出现在任务板。")
	_assert_true(missing_description_entry.is_empty(), "缺少 description 的契约不应回退成暂无说明出现在任务板。")
	_assert_true(missing_objectives_entry.is_empty(), "缺少 objective_defs 的契约不应回退成暂无目标说明出现在任务板。")
	_assert_true(missing_objective_target_entry.is_empty(), "缺少 target_id 的据点事务目标不应回退成未命名出现在任务板。")
	_assert_true(unknown_objective_type_entry.is_empty(), "未知 objective_type 不应回退成 objective_id 出现在任务板。")
	_assert_true(missing_rewards_entry.is_empty(), "缺少 reward_entries 的契约不应回退成奖励待定出现在任务板。")
	_assert_true(invalid_reward_amount_entry.is_empty(), "非法 reward amount 的契约不应回退成奖励待定出现在任务板。")
	_assert_true(string_key_only_entry.is_empty(), "String key-only 契约不应被任务板恢复。")
	var bad_schema_submission := handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"quest_id": "contract_missing_display_name",
		"provider_interaction_id": "service_contract_board",
	})
	_assert_true(not bool(bad_schema_submission.get("ok", true)), "缺少 display_name 的坏契约即使被构造提交也应拒绝。")
	_assert_eq(runtime.accepted_quest_calls.size(), 0, "坏契约 schema 不应触发 quest accept。")
	var bad_objective_submission := handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"quest_id": "contract_missing_objectives",
		"provider_interaction_id": "service_contract_board",
	})
	_assert_true(not bool(bad_objective_submission.get("ok", true)), "缺少 objective_defs 的坏契约即使被构造提交也应拒绝。")
	_assert_eq(runtime.accepted_quest_calls.size(), 0, "坏 objective schema 不应触发 quest accept。")
	var legacy_panel_submission := handler.command_execute_settlement_action("service:contract_board", {
		"panel_kind": "contract_board",
		"quest_id": "contract_manual_drill",
		"provider_interaction_id": "service_contract_board",
	})
	_assert_true(not bool(legacy_panel_submission.get("ok", false)), "旧 panel_kind 字段不应再被识别为任务板提交或普通据点动作。")
	_assert_eq(runtime.accepted_quest_calls.size(), 0, "旧 panel_kind 字段不应触发 quest accept。")
	var legacy_entry_submission := handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"entry_id": "contract_manual_drill",
		"provider_interaction_id": "service_contract_board",
	})
	_assert_true(not bool(legacy_entry_submission.get("ok", true)), "旧 entry_id 字段不应回退成 quest_id。")
	_assert_eq(runtime.accepted_quest_calls.size(), 0, "旧 entry_id 字段不应触发 quest accept。")
	_assert_eq(runtime._current_status_message, "当前契约条目缺少 quest_id，无法接取。", "旧 entry_id 提交应返回缺 quest_id 的反馈。")
	var legacy_provider_submission := handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"quest_id": "contract_manual_drill",
		"interaction_script_id": "service_contract_board",
	})
	_assert_true(not bool(legacy_provider_submission.get("ok", true)), "旧 interaction_script_id 字段不应回退成 provider_interaction_id。")
	_assert_eq(runtime.accepted_quest_calls.size(), 0, "旧 interaction_script_id 字段不应触发 quest accept。")
	_assert_eq(runtime._current_status_message, "当前契约条目缺少 provider_interaction_id，无法匹配任务板。", "旧 interaction_script_id 提交应返回缺 provider_interaction_id 的反馈。")
	var string_key_submission := handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"quest_id": "contract_string_key_only",
		"provider_interaction_id": "service_contract_board",
	})
	_assert_true(not bool(string_key_submission.get("ok", true)), "String key-only 契约即使被构造提交也应拒绝。")
	_assert_eq(runtime.accepted_quest_calls.size(), 0, "String key-only 契约不应触发 quest accept。")
	_assert_eq(runtime._current_status_message, "当前任务板未找到契约 contract_string_key_only。", "String key-only 提交应按未找到任务处理。")
	var mismatched_contract_submission := handler.command_execute_settlement_action("service:bounty_registry", {
		"submission_source": "contract_board",
		"quest_id": "contract_manual_drill",
		"provider_interaction_id": "service_contract_board",
	})
	_assert_true(not bool(mismatched_contract_submission.get("ok", true)), "任务板提交不应允许切到其他 action_id。")
	_assert_eq(runtime.accepted_quest_calls.size(), 0, "action_id 与当前任务板不一致时不应触发 quest accept。")
	_assert_eq(runtime._current_status_message, "当前任务板与请求的服务入口不一致。", "任务板 action_id 不匹配时应返回明确反馈。")
	var accept_contract_result := handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"quest_id": "contract_manual_drill",
		"provider_interaction_id": "service_contract_board",
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
		"provider_interaction_id": "service_contract_board",
	})
	_assert_eq(runtime.accepted_quest_calls.size(), 2, "重复提交同一契约时仍应调用正式 quest accept 命令。")
	_assert_eq(runtime._current_status_message, "任务《训练记录》已在进行中，不能重复接取。", "重复接取时应返回明确反馈。")

	_assert_true(runtime._party_state.mark_quest_completed(&"contract_manual_drill", runtime.world_step), "测试前置：普通契约应能标记完成。")
	var manual_claim_gold_before: int = runtime._party_state.get_gold()
	handler.command_execute_settlement_action("service:contract_board", {
		"submission_source": "contract_board",
		"quest_id": "contract_manual_drill",
		"provider_interaction_id": "service_contract_board",
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
		"provider_interaction_id": "service_contract_board",
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
		"provider_interaction_id": "service_contract_board",
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

	var legacy_reward_source_result := handler.execute_settlement_action("spring_village_01", "service:training", {
		"interaction_script_id": "training_service",
		"facility_name": "训练场",
		"npc_name": "教官",
		"service_type": "训练",
		"member_id": "hero",
		"mastery_source_type": "legacy_mastery",
		"pending_character_rewards": [
			{
				"member_id": "hero",
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
	var legacy_reward_entries: Array = legacy_reward_source_result.get("pending_character_rewards", [])
	var legacy_reward: Dictionary = legacy_reward_entries[0] if not legacy_reward_entries.is_empty() and legacy_reward_entries[0] is Dictionary else {}
	_assert_eq(String(legacy_reward.get("source_type", "")), "training", "旧 mastery_source_type 不应回退成奖励 source_type。")
	_assert_eq(String(legacy_reward.get("source_id", "")), "training", "旧 mastery_source_type 不应回退成奖励 source_id。")

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


func _test_settlement_shop_service_rejects_bad_entry_schema() -> void:
	var shop_service := SettlementShopService.new()
	var item_defs := _make_shop_item_defs()
	var settlement_record := {
		"settlement_id": "spring_village_01",
		"display_name": "春泉村",
	}
	var valid_warehouse := MockShopWarehouseService.new()
	valid_warehouse.inventory_entries = [
		_make_formal_inventory_entry("travel_ration", 3),
	]
	valid_warehouse.counts = {
		&"travel_ration": 3,
	}
	var valid_window_data := shop_service.build_window_data(
		"service_basic_supply",
		settlement_record,
		_make_shop_state([
			{"item_id": "healing_herb", "quantity": 2, "unit_price": 12, "sold_out": false},
		]),
		item_defs,
		valid_warehouse,
		100
	)
	_assert_eq((valid_window_data.get("buy_entries", []) as Array).size(), 1, "正式 shop stock entry 应生成可购买条目。")
	_assert_eq((valid_window_data.get("sell_entries", []) as Array).size(), 1, "正式 sell inventory entry 应生成可出售条目。")

	var invalid_stock_cases: Array[Dictionary] = [
		{
			"label": "字符串 quantity",
			"entry": {"item_id": "healing_herb", "quantity": "2", "unit_price": 12, "sold_out": false},
		},
		{
			"label": "字符串 unit_price",
			"entry": {"item_id": "healing_herb", "quantity": 2, "unit_price": "12", "sold_out": false},
		},
		{
			"label": "缺 unit_price",
			"entry": {"item_id": "healing_herb", "quantity": 2, "sold_out": false},
		},
		{
			"label": "空 item_id",
			"entry": {"item_id": "", "quantity": 2, "unit_price": 12, "sold_out": false},
		},
		{
			"label": "旧 price 字段",
			"entry": {"item_id": "healing_herb", "quantity": 2, "unit_price": 12, "price": 1, "sold_out": false},
		},
		{
			"label": "字符串 sold_out",
			"entry": {"item_id": "healing_herb", "quantity": 2, "unit_price": 12, "sold_out": "false"},
		},
		{
			"label": "非正 quantity",
			"entry": {"item_id": "healing_herb", "quantity": 0, "unit_price": 12, "sold_out": false},
		},
	]
	for case_data in invalid_stock_cases:
		var label := String(case_data.get("label", "坏库存"))
		var warehouse := MockShopWarehouseService.new()
		var party_state := _make_party_state()
		party_state.gold = 100
		var settlement_state := _make_shop_state([
			(case_data.get("entry", {}) as Dictionary).duplicate(true),
		])
		var window_data := shop_service.build_window_data(
			"service_basic_supply",
			settlement_record,
			settlement_state,
			item_defs,
			warehouse,
			party_state.gold
		)
		_assert_eq((window_data.get("buy_entries", []) as Array).size(), 0, "%s 的坏 shop stock 不应生成购买窗口条目。" % label)
		var gold_before := party_state.gold
		var buy_result := shop_service.buy(
			"service_basic_supply",
			settlement_record,
			settlement_state,
			item_defs,
			warehouse,
			party_state,
			&"healing_herb",
			1
		)
		_assert_true(not bool(buy_result.get("success", true)), "%s 的坏 shop stock 不应允许购买交易。" % label)
		_assert_eq(party_state.gold, gold_before, "%s 的坏 shop stock 不应扣除金币。" % label)
		_assert_eq(warehouse.added_items.size(), 0, "%s 的坏 shop stock 不应写入仓库。" % label)

	var invalid_sell_cases: Array[Dictionary] = [
		{
			"label": "旧 quantity 回退",
			"entry": {"item_id": "travel_ration", "quantity": 2},
		},
		{
			"label": "字符串 total_quantity",
			"entry": {"item_id": "travel_ration", "total_quantity": "2"},
		},
		{
			"label": "空 item_id",
			"entry": {"item_id": "", "total_quantity": 2},
		},
	]
	for case_data in invalid_sell_cases:
		var label := String(case_data.get("label", "坏出售条目"))
		var warehouse := MockShopWarehouseService.new()
		warehouse.inventory_entries = [
			(case_data.get("entry", {}) as Dictionary).duplicate(true),
		]
		var window_data := shop_service.build_window_data(
			"service_basic_supply",
			settlement_record,
			_make_shop_state([]),
			item_defs,
			warehouse,
			100
		)
		_assert_eq((window_data.get("sell_entries", []) as Array).size(), 0, "%s 的坏 sell entry 不应生成出售窗口条目。" % label)

	var no_price_item := _make_shop_item_def("无价样品", "没有正式回收价。", 10, 0, true)
	var no_price_item_defs := item_defs.duplicate()
	no_price_item_defs[&"no_price_sample"] = no_price_item
	var no_price_warehouse := MockShopWarehouseService.new()
	no_price_warehouse.inventory_entries = [
		_make_formal_inventory_entry("no_price_sample", 1),
	]
	no_price_warehouse.counts = {
		&"no_price_sample": 1,
	}
	var no_price_window_data := shop_service.build_window_data(
		"service_basic_supply",
		settlement_record,
		_make_shop_state([]),
		no_price_item_defs,
		no_price_warehouse,
		100
	)
	_assert_eq((no_price_window_data.get("sell_entries", []) as Array).size(), 0, "缺少正式 sell_price 的物品不应补默认回收价。")
	var no_price_party := _make_party_state()
	var no_price_sell_result := shop_service.sell(
		"service_basic_supply",
		settlement_record,
		_make_shop_state([]),
		no_price_item_defs,
		no_price_warehouse,
		no_price_party,
		&"no_price_sample",
		1
	)
	_assert_true(not bool(no_price_sell_result.get("success", true)), "缺少正式 sell_price 的物品不应允许出售交易。")
	_assert_eq(no_price_party.gold, 0, "缺少正式 sell_price 的出售失败不应增加金币。")
	_assert_eq(no_price_warehouse.removed_items.size(), 0, "缺少正式 sell_price 的出售失败不应移除仓库物品。")


func _test_settlement_handler_rejects_invalid_or_spoofed_actions() -> void:
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
					"action_id": "service:basic_supply",
					"facility_name": "补给铺",
					"npc_name": "行商",
					"service_type": "补给",
					"interaction_script_id": "service_basic_supply",
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
	}

	var handler := GameRuntimeSettlementCommandHandler.new()
	handler.setup(runtime)

	runtime._active_modal_id = ""
	var closed_modal_result := handler.command_execute_settlement_action("service:basic_supply")
	_assert_true(not bool(closed_modal_result.get("ok", true)), "未打开据点窗口时不应执行据点服务。")
	_assert_eq(String(closed_modal_result.get("message", "")), "当前没有打开对应的据点窗口。", "未打开据点窗口应返回明确错误。")
	runtime._active_modal_id = "settlement"

	runtime._fog_system.visible = false
	var hidden_settlement_result := handler.command_execute_settlement_action("service:basic_supply")
	_assert_true(not bool(hidden_settlement_result.get("ok", true)), "不可见据点不应执行据点服务。")
	_assert_eq(String(hidden_settlement_result.get("message", "")), "当前据点不在视野中，不能执行据点服务。", "不可见据点应返回明确错误。")
	runtime._fog_system.visible = true

	var missing_action_result := handler.command_execute_settlement_action("service:missing")
	_assert_true(not bool(missing_action_result.get("ok", true)), "未开放的 action_id 应被直接拒绝。")
	_assert_true(String(missing_action_result.get("message", "")).find("未开放该服务") >= 0, "未开放 action_id 的错误信息应明确指出未开放。")
	_assert_eq(runtime._active_modal_id, "settlement", "未开放 action_id 失败后不应切换 modal。")

	var disabled_stagecoach_result := handler.command_execute_settlement_action("service:stagecoach")
	_assert_true(not bool(disabled_stagecoach_result.get("ok", true)), "禁用的据点服务不应继续执行。")
	_assert_eq(String(disabled_stagecoach_result.get("message", "")), "驿站 当前不可用：暂无已访问路线。", "禁用服务应返回明确 disabled_reason。")
	_assert_eq(runtime._active_modal_id, "settlement", "禁用服务失败后不应切换 modal。")

	handler.on_settlement_action_requested("spring_village_01", "service:basic_supply", {
		"interaction_script_id": "service_research",
		"facility_name": "伪造图书馆",
		"npc_name": "伪造导师",
		"service_type": "研究",
	})
	var signal_shop_window_data := handler.get_shop_window_data()
	_assert_eq(runtime._active_modal_id, "shop", "UI 信号入口收到伪造 interaction_script_id 时仍应按真实商店入口打开 shop modal。")
	_assert_eq(String(signal_shop_window_data.get("interaction_script_id", "")), "service_basic_supply", "UI 信号入口应使用真实服务 interaction_script_id。")
	_assert_eq(runtime._current_status_message, "已打开 补给铺 的商店。", "UI 信号入口应使用真实服务 facility_name。")
	runtime._active_modal_id = "settlement"

	var spoofed_shop_result := handler.command_execute_settlement_action("service:basic_supply", {
		"interaction_script_id": "service_research",
		"facility_name": "伪造图书馆",
		"npc_name": "伪造导师",
		"service_type": "研究",
	})
	_assert_true(bool(spoofed_shop_result.get("ok", false)), "合法 action_id 仍应按真实服务入口执行。")
	_assert_eq(runtime._active_modal_id, "shop", "伪造 interaction_script_id 时仍应按真实商店入口打开 shop modal。")
	_assert_true(not handler.get_shop_window_data().is_empty(), "按真实商店入口执行后应能读取 shop window data。")


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


func _make_shop_item_defs() -> Dictionary:
	return {
		&"healing_herb": _make_shop_item_def("治疗草药", "恢复少量生命。", 12, 6, true),
		&"travel_ration": _make_shop_item_def("旅行口粮", "野外行军口粮。", 14, 7, true),
	}


func _make_shop_item_def(
	display_name: String,
	description: String,
	buy_price: int,
	sell_price: int,
	sellable: bool
) -> MockShopItemDef:
	var item_def := MockShopItemDef.new()
	item_def.display_name = display_name
	item_def.description = description
	item_def.icon = ""
	item_def.buy_price = buy_price
	item_def.sell_price = sell_price
	item_def.sellable = sellable
	return item_def


func _make_shop_state(current_inventory: Array) -> Dictionary:
	return {
		"visited": true,
		"reputation": 0,
		"active_conditions": [],
		"cooldowns": {},
		"world_step": 0,
		"shop_inventory_seed": 0,
		"shop_last_refresh_step": 0,
		"shop_states": {
			"village_basic_supply": {
				"shop_id": "village_basic_supply",
				"current_inventory": current_inventory.duplicate(true),
				"seed": 1,
				"last_refresh_step": 0,
			},
		},
	}


func _make_formal_inventory_entry(item_id: String, total_quantity: int) -> Dictionary:
	return {
		"item_id": item_id,
		"display_name": item_id,
		"description": "",
		"icon": "",
		"quantity": total_quantity,
		"total_quantity": total_quantity,
		"is_stackable": true,
		"stack_limit": 99,
		"item_category": "misc",
		"is_skill_book": false,
		"granted_skill_id": "",
		"storage_mode": "stack",
	}


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
		result.append(String(entry.get("quest_id", "")))
	return result


func _find_contract_board_entry(entry_variants, quest_id: String) -> Dictionary:
	if entry_variants is not Array:
		return {}
	for entry_variant in entry_variants:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("quest_id", "")) == quest_id:
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
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
