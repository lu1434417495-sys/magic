extends SceneTree

const GameRuntimeFacade = preload("res://scripts/systems/game_runtime_facade.gd")
const GameRuntimePartyCommandHandler = preload("res://scripts/systems/game_runtime_party_command_handler.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")

var _failures: Array[String] = []


class MockPartyCommandHandler:
	extends RefCounted

	var calls: Array[Dictionary] = []

	func command_open_party() -> Dictionary:
		return _record("command_open_party")

	func command_select_party_member(member_id: StringName) -> Dictionary:
		return _record("command_select_party_member", [member_id])

	func command_set_party_leader(member_id: StringName) -> Dictionary:
		return _record("command_set_party_leader", [member_id])

	func command_move_member_to_active(member_id: StringName) -> Dictionary:
		return _record("command_move_member_to_active", [member_id])

	func command_move_member_to_reserve(member_id: StringName) -> Dictionary:
		return _record("command_move_member_to_reserve", [member_id])

	func command_party_equip_item(member_id: StringName, item_id: StringName, slot_id: StringName = &"") -> Dictionary:
		return _record("command_party_equip_item", [member_id, item_id, slot_id])

	func command_party_unequip_item(member_id: StringName, slot_id: StringName) -> Dictionary:
		return _record("command_party_unequip_item", [member_id, slot_id])

	func apply_party_roster(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName]) -> Dictionary:
		return _record("apply_party_roster", [active_member_ids.duplicate(), reserve_member_ids.duplicate()])

	func open_party_management_window() -> void:
		_record_void("open_party_management_window")

	func on_party_leader_change_requested(member_id: StringName) -> void:
		_record_void("on_party_leader_change_requested", [member_id])

	func on_party_roster_change_requested(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName]) -> void:
		_record_void("on_party_roster_change_requested", [active_member_ids.duplicate(), reserve_member_ids.duplicate()])

	func on_party_management_window_closed() -> void:
		_record_void("on_party_management_window_closed")

	func on_party_management_warehouse_requested() -> void:
		_record_void("on_party_management_warehouse_requested")

	func apply_party_state_to_runtime(success_message: String) -> void:
		_record_void("apply_party_state_to_runtime", [success_message])

	func open_party_warehouse_window(entry_label: String) -> void:
		_record_void("open_party_warehouse_window", [entry_label])

	func _record(method_name: String, args: Array = []) -> Dictionary:
		var entry := {
			"method": method_name,
			"args": args.duplicate(true),
		}
		calls.append(entry)
		return {
			"ok": true,
			"message": method_name,
			"battle_refresh_mode": "",
		}

	func _record_void(method_name: String, args: Array = []) -> void:
		calls.append({
			"method": method_name,
			"args": args.duplicate(true),
		})


class MockItemDef:
	extends RefCounted

	var display_name := ""

	func _init(p_display_name: String = "") -> void:
		display_name = p_display_name


class MockWarehouseService:
	extends RefCounted

	var setup_calls: Array[Dictionary] = []
	var item_defs: Dictionary = {}

	func setup(party_state, item_defs_in: Dictionary) -> void:
		setup_calls.append({
			"party_state": party_state,
			"item_defs": item_defs_in.duplicate(true),
		})
		item_defs = item_defs_in.duplicate(true)

	func get_item_def(item_id: StringName):
		return item_defs.get(item_id)


class MockItemUseService:
	extends RefCounted

	var setup_calls: Array[Dictionary] = []

	func setup(party_state, item_defs: Dictionary, skill_defs: Dictionary, warehouse_service, character_management) -> void:
		setup_calls.append({
			"party_state": party_state,
			"item_defs": item_defs.duplicate(true),
			"skill_defs": skill_defs.duplicate(true),
		})


class MockPartyEquipmentService:
	extends RefCounted

	var setup_calls: Array[Dictionary] = []
	var next_equip_result: Dictionary = {}
	var next_unequip_result: Dictionary = {}
	var item_defs: Dictionary = {}

	func setup(party_state, item_defs_in: Dictionary, warehouse_service) -> void:
		setup_calls.append({
			"party_state": party_state,
			"item_defs": item_defs_in.duplicate(true),
		})
		item_defs = item_defs_in.duplicate(true)

	func equip_item(member_id: StringName, item_id: StringName, slot_id: StringName) -> Dictionary:
		return next_equip_result.duplicate(true)

	func unequip_item(member_id: StringName, slot_id: StringName) -> Dictionary:
		return next_unequip_result.duplicate(true)

	func get_item_def(item_id: StringName):
		return item_defs.get(item_id)


class MockCharacterManagement:
	extends RefCounted

	var last_party_state = null

	func set_party_state(party_state) -> void:
		last_party_state = party_state


class MockGameSession:
	extends RefCounted

	var _party_state = null
	var _item_defs: Dictionary = {}
	var _skill_defs: Dictionary = {}

	func set_party_state(party_state) -> int:
		_party_state = party_state
		return OK

	func get_party_state():
		return _party_state

	func get_item_defs() -> Dictionary:
		return _item_defs.duplicate(true)

	func get_skill_defs() -> Dictionary:
		return _skill_defs.duplicate(true)


class MockRuntime:
	extends RefCounted

	var _generation_config = {}
	var _party_state: PartyState = null
	var _party_selected_member_id: StringName = &""
	var _active_modal_id := ""
	var _current_status_message := ""
	var _game_session = null
	var _party_warehouse_service = null
	var _party_item_use_service = null
	var _party_equipment_service = null
	var _character_management = null
	var _warehouse_handler = null
	var _present_reward_calls := 0
	var _refresh_fog_calls := 0

	func _is_battle_active() -> bool:
		return false

	func _is_modal_window_open() -> bool:
		return _active_modal_id != ""

	func _update_status(message: String) -> void:
		_current_status_message = message

	func _present_pending_reward_if_ready() -> bool:
		_present_reward_calls += 1
		return false

	func _refresh_fog() -> void:
		_refresh_fog_calls += 1

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

	func get_generation_config():
		return _generation_config

	func is_battle_active() -> bool:
		return _is_battle_active()

	func is_modal_window_open() -> bool:
		return _is_modal_window_open()

	func get_party_state():
		return _party_state

	func set_party_state(party_state) -> void:
		_party_state = party_state

	func get_active_modal_id() -> String:
		return _active_modal_id

	func set_runtime_active_modal_id(modal_id: String) -> void:
		_active_modal_id = modal_id

	func get_party_selected_member_id() -> StringName:
		return _party_selected_member_id

	func set_party_selected_member_id(member_id: StringName) -> void:
		_party_selected_member_id = member_id

	func equip_party_item(member_id: StringName, item_id: StringName, slot_id: StringName) -> Dictionary:
		return _party_equipment_service.equip_item(member_id, item_id, slot_id)

	func unequip_party_item(member_id: StringName, slot_id: StringName) -> Dictionary:
		return _party_equipment_service.unequip_item(member_id, slot_id)

	func sync_character_management_party_state() -> void:
		if _character_management != null:
			_character_management.set_party_state(_party_state)

	func open_party_warehouse_window(entry_label: String) -> void:
		if _warehouse_handler != null:
			_warehouse_handler.open_party_warehouse_window(entry_label)

	func present_pending_reward_if_ready() -> bool:
		return _present_pending_reward_if_ready()

	func update_status(message: String) -> void:
		_update_status(message)

	func get_status_text() -> String:
		return _current_status_message

	func get_game_session():
		return _game_session

	func get_party_warehouse_service():
		return _party_warehouse_service

	func get_party_item_use_service():
		return _party_item_use_service

	func get_party_equipment_service():
		return _party_equipment_service

	func get_character_management():
		return _character_management

	func get_warehouse_handler():
		return _warehouse_handler

	func get_item_display_name(item_id: StringName) -> String:
		var game_session = get_game_session()
		if game_session == null:
			return String(item_id)
		var item_def = game_session.get_item_defs().get(item_id)
		return item_def.display_name if item_def != null and not String(item_def.display_name).is_empty() else String(item_id)

	func get_member_display_name(member_id: StringName) -> String:
		var member_state = _party_state.get_member_state(member_id) if _party_state != null else null
		return String(member_state.display_name) if member_state != null and not String(member_state.display_name).is_empty() else String(member_id)

	func persist_party_state() -> int:
		if _game_session == null:
			return ERR_UNAVAILABLE
		var persist_error := int(_game_session.set_party_state(_party_state))
		_party_state = _game_session.get_party_state()
		sync_character_management_party_state()
		if _party_warehouse_service != null:
			_party_warehouse_service.setup(_party_state, _game_session.get_item_defs())
		if _party_item_use_service != null:
			_party_item_use_service.setup(
				_party_state,
				_game_session.get_item_defs(),
				_game_session.get_skill_defs(),
				_party_warehouse_service,
				_character_management
			)
		if _party_equipment_service != null:
			_party_equipment_service.setup(_party_state, _game_session.get_item_defs(), _party_warehouse_service)
		refresh_fog()
		return persist_error

	func refresh_fog() -> void:
		_refresh_fog()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_facade_delegates_party_surface_to_handler()
	_test_party_handler_updates_runtime_state_and_persists()

	if _failures.is_empty():
		print("Game runtime party command handler regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Game runtime party command handler regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_facade_delegates_party_surface_to_handler() -> void:
	var facade := GameRuntimeFacade.new()
	var handler := MockPartyCommandHandler.new()
	facade._party_command_handler = handler

	_assert_eq(String(facade.command_open_party().get("message", "")), "command_open_party", "command_open_party() 应委托给 party handler。")
	_assert_eq(String(facade.command_select_party_member(&"hero").get("message", "")), "command_select_party_member", "command_select_party_member() 应委托给 party handler。")
	_assert_eq(String(facade.command_set_party_leader(&"hero").get("message", "")), "command_set_party_leader", "command_set_party_leader() 应委托给 party handler。")
	_assert_eq(String(facade.command_move_member_to_active(&"hero").get("message", "")), "command_move_member_to_active", "command_move_member_to_active() 应委托给 party handler。")
	_assert_eq(String(facade.command_move_member_to_reserve(&"hero").get("message", "")), "command_move_member_to_reserve", "command_move_member_to_reserve() 应委托给 party handler。")
	_assert_eq(String(facade.command_party_equip_item(&"hero", &"bronze_sword", &"weapon").get("message", "")), "command_party_equip_item", "command_party_equip_item() 应委托给 party handler。")
	_assert_eq(String(facade.command_party_unequip_item(&"hero", &"weapon").get("message", "")), "command_party_unequip_item", "command_party_unequip_item() 应委托给 party handler。")
	_assert_eq(String(facade.apply_party_roster([&"hero"], [&"mage"]).get("message", "")), "apply_party_roster", "apply_party_roster() 应委托给 party handler。")

	facade._open_party_management_window()
	facade._on_party_leader_change_requested(&"hero")
	facade._on_party_roster_change_requested([&"hero"], [&"mage"])
	facade._on_party_management_window_closed()
	facade._on_party_management_warehouse_requested()
	facade._apply_party_state_to_runtime("队伍状态同步成功。")

	_assert_true(_has_call(handler.calls, "open_party_management_window"), "_open_party_management_window() 应委托给 party handler。")
	_assert_true(_has_call(handler.calls, "on_party_leader_change_requested"), "_on_party_leader_change_requested() 应委托给 party handler。")
	_assert_true(_has_call(handler.calls, "on_party_roster_change_requested"), "_on_party_roster_change_requested() 应委托给 party handler。")
	_assert_true(_has_call(handler.calls, "on_party_management_window_closed"), "_on_party_management_window_closed() 应委托给 party handler。")
	_assert_true(_has_call(handler.calls, "on_party_management_warehouse_requested"), "_on_party_management_warehouse_requested() 应委托给 party handler。")
	_assert_true(_has_call(handler.calls, "apply_party_state_to_runtime"), "_apply_party_state_to_runtime() 应委托给 party handler。")


func _test_party_handler_updates_runtime_state_and_persists() -> void:
	var runtime := MockRuntime.new()
	var party_state := _make_party_state()
	var game_session := MockGameSession.new()
	game_session._party_state = party_state
	game_session._item_defs = {
		&"bronze_sword": MockItemDef.new("青铜剑"),
	}
	game_session._skill_defs = {
		&"slash": RefCounted.new(),
	}
	runtime._party_state = party_state
	runtime._game_session = game_session
	runtime._party_warehouse_service = MockWarehouseService.new()
	runtime._party_item_use_service = MockItemUseService.new()
	runtime._party_equipment_service = MockPartyEquipmentService.new()
	runtime._character_management = MockCharacterManagement.new()
	runtime._warehouse_handler = MockPartyCommandHandler.new()
	runtime._party_equipment_service.next_equip_result = {
		"success": true,
		"member_id": "hero",
		"item_id": "bronze_sword",
		"previous_item_id": "",
		"slot_label": "武器栏",
	}
	runtime._party_equipment_service.next_unequip_result = {
		"success": true,
		"member_id": "hero",
		"item_id": "bronze_sword",
		"slot_label": "武器栏",
	}

	var handler := GameRuntimePartyCommandHandler.new()
	handler.setup(runtime)

	var open_result: Dictionary = handler.command_open_party()
	_assert_true(bool(open_result.get("ok", false)), "打开队伍管理应成功。")
	_assert_eq(runtime._active_modal_id, "party", "打开队伍管理应切换 modal。")
	_assert_eq(runtime._party_selected_member_id, &"hero", "打开队伍管理应默认选中上阵第一人。")

	var select_result: Dictionary = handler.command_select_party_member(&"mage")
	_assert_true(bool(select_result.get("ok", false)), "选中队员应成功。")
	_assert_eq(runtime._party_selected_member_id, &"mage", "选中队员后应同步选中标记。")

	var ghost_select_result: Dictionary = handler.command_select_party_member(&"ghost")
	_assert_true(not bool(ghost_select_result.get("ok", true)), "不在 active/reserve roster 的成员不应允许被选中。")
	_assert_true(String(ghost_select_result.get("message", "")).find("不在队伍编成中") >= 0, "越权选中非 roster 成员时应返回明确错误。")
	_assert_eq(runtime._party_selected_member_id, &"mage", "越权选中失败后不应改写当前选中成员。")

	var leader_result: Dictionary = handler.command_set_party_leader(&"hero")
	_assert_true(bool(leader_result.get("ok", false)), "设置队长应成功。")
	_assert_eq(runtime._party_state.leader_member_id, &"hero", "设置队长后应更新队长成员。")
	_assert_eq(runtime._character_management.last_party_state, runtime._party_state, "设置队长后应同步队伍状态到角色管理。")

	var roster_result: Dictionary = handler.command_move_member_to_active(&"mage")
	_assert_true(bool(roster_result.get("ok", false)), "移动成员到上阵应成功。")
	_assert_true(runtime._party_state.active_member_ids.has(&"mage"), "移动到上阵后应更新 active 列表。")
	_assert_eq(runtime._party_selected_member_id, &"mage", "移动成员后应保持当前选中成员。")
	_assert_true(runtime._party_warehouse_service.setup_calls.size() > 0, "持久化时应刷新仓库服务。")
	_assert_true(runtime._party_item_use_service.setup_calls.size() > 0, "持久化时应刷新物品使用服务。")
	_assert_true(runtime._party_equipment_service.setup_calls.size() > 0, "持久化时应刷新装备服务。")
	_assert_true(runtime._refresh_fog_calls > 0, "持久化后应刷新迷雾。")

	var move_main_to_reserve_result: Dictionary = handler.command_move_member_to_reserve(&"hero")
	_assert_true(not bool(move_main_to_reserve_result.get("ok", true)), "主角不应允许被移到替补。")
	_assert_true(runtime._party_state.active_member_ids.has(&"hero"), "主角被拒绝下阵后仍应保留在 active roster。")
	_assert_true(not runtime._party_state.reserve_member_ids.has(&"hero"), "主角被拒绝下阵后不应进入 reserve roster。")
	_assert_true(String(move_main_to_reserve_result.get("message", "")).find("主角必须保持上阵") >= 0, "下阵主角时应返回明确错误。")

	var invalid_roster_result: Dictionary = handler.apply_party_roster([&"mage"], [&"hero"])
	_assert_true(not bool(invalid_roster_result.get("ok", true)), "非法编成不应通过 apply_party_roster()。")
	_assert_true(runtime._party_state.active_member_ids.has(&"hero"), "非法编成被拒绝后，运行时 active roster 不应丢失主角。")
	_assert_true(not runtime._party_state.reserve_member_ids.has(&"hero"), "非法编成被拒绝后，运行时 reserve roster 不应出现主角。")

	var equip_result: Dictionary = handler.command_party_equip_item(&"hero", &"bronze_sword", &"weapon")
	_assert_true(bool(equip_result.get("ok", false)), "装备物品应成功。")
	_assert_eq(runtime._party_selected_member_id, &"hero", "装备后应更新当前选中成员。")
	_assert_true(String(runtime._current_status_message).find("青铜剑") >= 0, "装备成功消息应包含物品名称。")

	handler.on_party_management_warehouse_requested()
	_assert_eq(runtime._active_modal_id, "", "打开共享仓库时应关闭队伍窗口。")
	_assert_true(runtime._warehouse_handler.calls.size() > 0, "打开共享仓库应委托给仓库入口。")
	var warehouse_call: Dictionary = runtime._warehouse_handler.calls[0]
	var warehouse_args: Array = warehouse_call.get("args", []) as Array
	_assert_eq(String(warehouse_call.get("method", "")), "open_party_warehouse_window", "打开共享仓库时应走正式仓库窗口入口。")
	_assert_eq(String(warehouse_args[0]), "队伍管理", "打开共享仓库时应保留队伍管理入口标签。")

	handler.on_party_management_window_closed()
	_assert_eq(runtime._active_modal_id, "", "关闭队伍窗口应清空 modal。")
	_assert_true(runtime._present_reward_calls > 0, "关闭队伍窗口后应尝试恢复待确认奖励。")


func _make_party_state() -> PartyState:
	var party_state := PartyState.new()
	party_state.leader_member_id = &"hero"
	party_state.main_character_member_id = &"hero"
	party_state.active_member_ids = [&"hero"]
	party_state.reserve_member_ids = [&"mage"]

	var hero := PartyMemberState.new()
	hero.member_id = &"hero"
	hero.display_name = "Hero"
	var mage := PartyMemberState.new()
	mage.member_id = &"mage"
	mage.display_name = "Mage"
	var ghost := PartyMemberState.new()
	ghost.member_id = &"ghost"
	ghost.display_name = "Ghost"
	party_state.member_states = {
		&"hero": hero,
		&"mage": mage,
		&"ghost": ghost,
	}
	return party_state


func _has_call(calls: Array[Dictionary], method_name: String) -> bool:
	for call in calls:
		if String(call.get("method", "")) == method_name:
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s 实际=%s 预期=%s" % [message, str(actual), str(expected)])
