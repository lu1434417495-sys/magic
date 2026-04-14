## 文件说明：该脚本属于世界地图系统相关的系统脚本，集中维护世界地图视图、世界地图背景、战斗地图面板等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name GameRuntimeFacade
extends RefCounted

const WORLD_MAP_GRID_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_grid_system.gd")
const WORLD_MAP_FOG_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_fog_system.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle_cell_state.gd")
const BATTLE_GRID_SERVICE_SCRIPT = preload("res://scripts/systems/battle_grid_service.gd")
const CHARACTER_MANAGEMENT_MODULE_SCRIPT = preload("res://scripts/systems/character_management_module.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle_runtime_module.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle_command.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")
const ENCOUNTER_ROSTER_BUILDER_SCRIPT = preload("res://scripts/systems/encounter_roster_builder.gd")
const PARTY_WAREHOUSE_SERVICE_SCRIPT = preload("res://scripts/systems/party_warehouse_service.gd")
const PARTY_ITEM_USE_SERVICE_SCRIPT = preload("res://scripts/systems/party_item_use_service.gd")
const PARTY_EQUIPMENT_SERVICE_SCRIPT = preload("res://scripts/systems/party_equipment_service.gd")
const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/pending_character_reward.gd")
const WORLD_TIME_SYSTEM_SCRIPT = preload("res://scripts/systems/world_time_system.gd")
const WILD_ENCOUNTER_GROWTH_SYSTEM_SCRIPT = preload("res://scripts/systems/wild_encounter_growth_system.gd")
const BATTLE_HUD_ADAPTER_SCRIPT = preload("res://scripts/ui/battle_hud_adapter.gd")
const GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT = preload("res://scripts/utils/game_text_snapshot_renderer.gd")
const VISION_SOURCE_DATA_SCRIPT = preload("res://scripts/utils/vision_source_data.gd")
const WORLD_MOVE_REPEAT_INTERVAL := 0.5
const PARTY_WAREHOUSE_INTERACTION_ID := "party_warehouse"
const PendingCharacterReward = PENDING_CHARACTER_REWARD_SCRIPT

var world_map_view = null
var world_map_background = null
var battle_map_panel = null
var status_label = null
var settlement_window = null
var character_info_window = null
var party_management_window = null
var party_warehouse_window = null
var promotion_choice_window = null
var mastery_reward_window = null
var settlement_window_system = null

## 字段说明：记录生成配置，会参与运行时状态流转、系统协作和存档恢复。
var _generation_config
## 字段说明：记录游戏会话，会参与运行时状态流转、系统协作和存档恢复。
var _game_session = null
## 字段说明：记录网格系统，会参与运行时状态流转、系统协作和存档恢复。
var _grid_system = WORLD_MAP_GRID_SYSTEM_SCRIPT.new()
## 字段说明：记录迷雾系统，会参与运行时状态流转、系统协作和存档恢复。
var _fog_system = WORLD_MAP_FOG_SYSTEM_SCRIPT.new()
## 字段说明：记录战斗网格服务，会参与运行时状态流转、系统协作和存档恢复。
var _battle_grid_service = BATTLE_GRID_SERVICE_SCRIPT.new()
## 字段说明：记录角色管理，会参与运行时状态流转、系统协作和存档恢复。
var _character_management = CHARACTER_MANAGEMENT_MODULE_SCRIPT.new()
## 字段说明：记录队伍仓库服务，会参与运行时状态流转、系统协作和存档恢复。
var _party_warehouse_service = PARTY_WAREHOUSE_SERVICE_SCRIPT.new()
## 字段说明：记录队伍物品使用服务，会参与运行时状态流转、系统协作和存档恢复。
var _party_item_use_service = PARTY_ITEM_USE_SERVICE_SCRIPT.new()
## 字段说明：记录队伍装备服务，会参与运行时状态流转、系统协作和存档恢复。
var _party_equipment_service = PARTY_EQUIPMENT_SERVICE_SCRIPT.new()
## 字段说明：记录遭遇编队构建器，会参与运行时状态流转、系统协作和存档恢复。
var _encounter_roster_builder = ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
## 字段说明：记录世界时间推进服务，会参与运行时状态流转、系统协作和存档恢复。
var _world_time_system = WORLD_TIME_SYSTEM_SCRIPT.new()
## 字段说明：记录野外遭遇成长服务，会参与运行时状态流转、系统协作和存档恢复。
var _wild_encounter_growth_system = WILD_ENCOUNTER_GROWTH_SYSTEM_SCRIPT.new()
## 字段说明：记录战斗运行时，会参与运行时状态流转、系统协作和存档恢复。
var _battle_runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
## 字段说明：记录玩家坐标，用于定位对象、绘制内容或执行网格计算。
var _player_coord := Vector2i.ZERO
## 字段说明：记录选中坐标，用于定位对象、绘制内容或执行网格计算。
var _selected_coord := Vector2i.ZERO
## 字段说明：记录玩家阵营唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _player_faction_id := "player"
## 字段说明：缓存世界数据字典，集中保存可按键查询的运行时数据。
var _world_data: Dictionary = {}
## 字段说明：按键缓存按坐标索引的聚落查找表，便于在较多对象中快速定位目标并减少重复遍历。
var _settlement_by_coord: Dictionary = {}
## 字段说明：按键缓存按坐标索引的世界NPC查找表，便于在较多对象中快速定位目标并减少重复遍历。
var _world_npc_by_coord: Dictionary = {}
## 字段说明：按键缓存按坐标索引的遭遇锚点查找表，便于在较多对象中快速定位目标并减少重复遍历。
var _encounter_anchor_by_coord: Dictionary = {}
## 字段说明：记录队伍状态，会参与运行时状态流转、系统协作和存档恢复。
var _party_state = null
## 字段说明：缓存战斗状态实例，会参与运行时状态流转、系统协作和存档恢复。
var _battle_state: BattleState = null
## 字段说明：记录战斗选中坐标，用于定位对象、绘制内容或执行网格计算。
var _battle_selected_coord := Vector2i(-1, -1)
## 字段说明：记录当前激活的战斗遭遇唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _active_battle_encounter_id: StringName = &""
## 字段说明：记录当前激活的战斗遭遇名称，会参与运行时状态流转、系统协作和存档恢复。
var _active_battle_encounter_name := ""
## 字段说明：缓存待处理的晋升提示字典，集中保存可按键查询的运行时数据。
var _pending_promotion_prompt: Dictionary = {}
## 字段说明：记录当前选中的战斗技能唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _selected_battle_skill_id: StringName = &""
## 字段说明：记录当前选中的战斗技能变体唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _selected_battle_skill_variant_id: StringName = &""
## 字段说明：缓存已排队的战斗技能目标坐标集合，供技能确认和结算前的预览流程复用。
var _queued_battle_skill_target_coords: Array[Vector2i] = []
## 字段说明：记录上一次手动操作的战斗单位唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _last_manual_battle_unit_id: StringName = &""
var _held_world_move_keys: Array[int] = []
var _world_move_repeat_timer := 0.0
## 字段说明：缓存激活熟练度奖励实例，会参与运行时状态流转、系统协作和存档恢复。
var _active_mastery_reward: PendingCharacterReward = null
## 字段说明：缓存待处理的世界晋升提示字典，集中保存可按键查询的运行时数据。
var _pending_world_promotion_prompt: Dictionary = {}
## 字段说明：记录当前激活的窗口标识，供 UI 与 headless 统一读取。
var _active_modal_id := ""
## 字段说明：记录激活仓库条目标签，会参与运行时状态流转、系统协作和存档恢复。
var _active_warehouse_entry_label := ""
## 字段说明：记录当前打开的据点唯一标识，供 headless 指令与稳定快照复用。
var _active_settlement_id := ""
## 字段说明：记录当前据点反馈文本，供 UI 与 headless 统一读取。
var _active_settlement_feedback_text := ""
## 字段说明：缓存最近一次状态文本，供 headless 文本测试直接读取。
var _current_status_message := ""
## 字段说明：缓存当前人物信息窗的结构化上下文，供 headless 文本测试稳定读取。
var _active_character_info_context: Dictionary = {}
## 字段说明：记录当前选中的队伍成员，供 UI 与 headless 统一读取。
var _party_selected_member_id: StringName = &""
## 字段说明：按标识缓存据点字典，供无 UI 模式直接查询窗口数据与服务动作。
var _settlements_by_id: Dictionary = {}
## 字段说明：缓存野外遭遇编队配置集合字典，供世界推进与战斗编队统一查表。
var _wild_encounter_rosters: Dictionary = {}


func setup(game_session) -> void:
	_game_session = game_session
	if _game_session == null:
		return
	if not _game_session.has_active_world():
		return
	_generation_config = _game_session.get_generation_config()
	if _generation_config == null:
		return
	_grid_system.setup(_generation_config.world_size_in_chunks, _generation_config.chunk_size)
	_fog_system.setup(_generation_config.get_world_size_cells())
	_world_data = _game_session.get_world_data()
	_wild_encounter_rosters = _game_session.get_wild_encounter_rosters()
	_encounter_roster_builder.setup(_wild_encounter_rosters)
	_rebuild_world_coord_lookups()
	_party_state = _game_session.get_party_state()
	_player_coord = _game_session.get_player_coord()
	_player_faction_id = _game_session.get_player_faction_id()
	_character_management.setup(
		_party_state,
		_game_session.get_skill_defs(),
		_game_session.get_profession_defs(),
		_game_session.get_achievement_defs(),
		_game_session.get_item_defs()
	)
	_party_warehouse_service.setup(_party_state, _game_session.get_item_defs())
	_party_item_use_service.setup(
		_party_state,
		_game_session.get_item_defs(),
		_game_session.get_skill_defs(),
		_party_warehouse_service,
		_character_management
	)
	_party_equipment_service.setup(_party_state, _game_session.get_item_defs(), _party_warehouse_service)
	_battle_runtime.setup(
		_character_management,
		_game_session.get_skill_defs(),
		_game_session.get_enemy_templates(),
		_game_session.get_enemy_ai_brains(),
		_encounter_roster_builder
	)
	_register_settlement_footprints()
	_selected_coord = _player_coord
	_refresh_fog()
	_active_modal_id = ""
	_active_settlement_id = ""
	_active_settlement_feedback_text = ""
	_active_character_info_context.clear()
	_party_selected_member_id = &""
	_active_warehouse_entry_label = ""

	var start_settlement_name: String = _world_data.get("player_start_settlement_name", "")
	if start_settlement_name.is_empty():
		_update_status("大地图已载入。方向键/WASD 可按住持续移动，点击可见据点或按 Enter 打开据点窗口，按 P 打开队伍管理，右键人物可查看信息。")
	else:
		_update_status("大地图已载入，初始村庄为 %s。方向键/WASD 可按住持续移动，点击可见据点或按 Enter 打开据点窗口，按 P 打开队伍管理，右键人物可查看信息。" % start_settlement_name)


func get_viewport():
	return null


func get_status_text() -> String:
	return _current_status_message


func get_active_modal_id() -> String:
	return _active_modal_id


func get_active_settlement_id() -> String:
	return _active_settlement_id


func get_grid_system():
	return _grid_system


func get_fog_system():
	return _fog_system


func get_world_data() -> Dictionary:
	return _world_data


func get_generation_config():
	return _generation_config


func get_player_coord() -> Vector2i:
	return _player_coord


func get_selected_coord() -> Vector2i:
	return _selected_coord


func get_player_faction_id() -> String:
	return _player_faction_id


func get_party_state():
	return _party_state


func get_party_selected_member_id() -> StringName:
	return _party_selected_member_id


func get_settlement_window_data(settlement_id: String = "") -> Dictionary:
	var target_id := settlement_id if not settlement_id.is_empty() else _resolve_command_settlement_id()
	var settlement: Dictionary = _settlements_by_id.get(target_id, {})
	if settlement.is_empty():
		return {}
	return {
		"settlement_id": settlement.get("settlement_id", ""),
		"display_name": settlement.get("display_name", ""),
		"tier_name": settlement.get("tier_name", ""),
		"footprint_size": settlement.get("footprint_size", Vector2i.ONE),
		"faction_id": settlement.get("faction_id", "neutral"),
		"facilities": settlement.get("facilities", []),
		"available_services": settlement.get("available_services", []),
		"service_npcs": settlement.get("service_npcs", []),
	}


func get_settlement_feedback_text() -> String:
	return _active_settlement_feedback_text


func get_character_info_context() -> Dictionary:
	return _active_character_info_context.duplicate(true)


func get_active_warehouse_entry_label() -> String:
	return _active_warehouse_entry_label


func get_warehouse_window_data() -> Dictionary:
	return _build_party_warehouse_window_data() if _party_state != null else {}


func get_battle_state() -> BattleState:
	return _battle_state


func get_battle_selected_coord() -> Vector2i:
	return _battle_selected_coord


func get_selected_battle_skill_id() -> StringName:
	return _selected_battle_skill_id


func get_selected_battle_skill_name() -> String:
	return _get_selected_battle_skill_name()


func get_selected_battle_skill_variant_name() -> String:
	return _get_selected_battle_skill_variant_name()


func get_selected_battle_skill_target_coords() -> Array[Vector2i]:
	return _queued_battle_skill_target_coords.duplicate()


func get_selected_battle_skill_required_coord_count() -> int:
	return _get_selected_battle_skill_required_coord_count()


func get_active_reward():
	return _active_mastery_reward


func get_pending_reward_count() -> int:
	return _party_state.pending_character_rewards.size() if _party_state != null else 0


func get_current_promotion_prompt() -> Dictionary:
	return _get_current_promotion_prompt()


func is_battle_active() -> bool:
	return _is_battle_active()


func is_modal_window_open() -> bool:
	return _is_modal_window_open()


func advance(delta: float) -> bool:
	if _generation_config == null:
		return false
	if _is_battle_active():
		if _is_battle_finished() or _active_modal_id == "promotion":
			return false
		var batch = _battle_runtime.advance(delta)
		if _batch_has_updates(batch):
			_apply_battle_batch(batch)
			return true
		return false
	if _is_modal_window_open():
		return false
	return _present_pending_mastery_reward_if_ready()


func build_headless_snapshot() -> Dictionary:
	return {
		"status": {
			"view": "battle" if _is_battle_active() else "world",
			"text": _current_status_message,
		},
		"modal": {
			"id": get_active_modal_id(),
		},
		"world": _build_world_snapshot(),
		"party": _build_party_snapshot(),
		"settlement": _build_settlement_snapshot(),
		"character_info": _build_character_info_snapshot(),
		"warehouse": _build_warehouse_snapshot(),
		"battle": _build_battle_snapshot(),
		"reward": _build_reward_snapshot(),
		"promotion": _build_promotion_snapshot(),
	}


func build_text_snapshot() -> String:
	return GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT.render_world_snapshot(build_headless_snapshot())


func command_world_move(direction: Vector2i, count: int = 1) -> Dictionary:
	if _generation_config == null:
		return _command_error("世界地图尚未初始化。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能执行大地图移动。")
	if _is_modal_window_open():
		return _command_error("当前有窗口打开，不能执行大地图移动。")
	if direction == Vector2i.ZERO:
		return _command_error("移动方向不能为空。")
	var move_count := maxi(count, 1)
	for _index in range(move_count):
		_move_player(direction)
		if _is_battle_active() or _is_modal_window_open():
			break
	return _command_ok()


func command_world_select(coord: Vector2i) -> Dictionary:
	if _generation_config == null:
		return _command_error("世界地图尚未初始化。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能选择大地图坐标。")
	if _is_modal_window_open():
		return _command_error("当前有窗口打开，不能切换大地图选择。")
	if not _grid_system.is_cell_walkable(coord):
		return _command_error("该大地图格超出当前世界范围。")
	_selected_coord = coord
	_update_status("已选中格子 %s。" % _format_coord(coord))
	return _command_ok()


func command_open_settlement(coord: Vector2i = Vector2i(-1, -1)) -> Dictionary:
	if _generation_config == null:
		return _command_error("世界地图尚未初始化。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能打开据点。")
	if _is_modal_window_open():
		return _command_error("当前有窗口打开，不能打开新的据点窗口。")
	var target_coord := _selected_coord if coord == Vector2i(-1, -1) else coord
	if _try_open_settlement_at(target_coord):
		return _command_ok()
	return _command_error(_current_status_message if not _current_status_message.is_empty() else "据点打开失败。")


func command_world_inspect(coord: Vector2i) -> Dictionary:
	if _generation_config == null:
		return _command_error("世界地图尚未初始化。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能查看大地图人物。")
	if _is_modal_window_open():
		return _command_error("当前有窗口打开，不能查看大地图人物。")
	if not _fog_system.is_visible(coord, _player_faction_id):
		_update_status("该格当前不在视野中。")
		return _command_error(_current_status_message)
	if _try_open_character_info_at_world_coord(coord):
		return _command_ok()
	_update_status("当前格没有可查看人物。")
	return _command_error(_current_status_message)


func command_open_party() -> Dictionary:
	if _generation_config == null:
		return _command_error("世界地图尚未初始化。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能打开队伍管理。")
	if _is_modal_window_open():
		return _command_error("当前有窗口打开，不能打开队伍管理。")
	_active_modal_id = "party"
	if _party_selected_member_id == &"" and _party_state != null and not _party_state.active_member_ids.is_empty():
		_party_selected_member_id = _party_state.active_member_ids[0]
	_update_status("已打开队伍管理窗口。")
	return _command_ok()


func command_select_party_member(member_id: StringName) -> Dictionary:
	if _party_state == null:
		return _command_error("当前不存在队伍数据。")
	if _party_state.get_member_state(member_id) == null:
		return _command_error("未找到队伍成员 %s。" % String(member_id))
	if _active_modal_id == "":
		_active_modal_id = "party"
	_party_selected_member_id = member_id
	_update_status("已选中队员 %s。" % _get_member_display_name(member_id))
	return _command_ok()


func command_set_party_leader(member_id: StringName) -> Dictionary:
	if _party_state == null:
		return _command_error("当前不存在队伍数据。")
	if not _party_state.active_member_ids.has(member_id):
		return _command_error("只有上阵成员才能成为队长。")
	_on_party_leader_change_requested(member_id)
	_party_selected_member_id = member_id
	return _command_ok()


func command_move_member_to_active(member_id: StringName) -> Dictionary:
	if _party_state == null:
		return _command_error("当前不存在队伍数据。")
	if not _party_state.reserve_member_ids.has(member_id):
		return _command_error("%s 当前不在替补列表中。" % _get_member_display_name(member_id))
	if _party_state.active_member_ids.size() >= 4:
		return _command_error("上阵人数已达到上限。")
	var active_member_ids: Array[StringName] = _party_state.active_member_ids.duplicate()
	var reserve_member_ids: Array[StringName] = _party_state.reserve_member_ids.duplicate()
	reserve_member_ids.erase(member_id)
	active_member_ids.append(member_id)
	_on_party_roster_change_requested(active_member_ids, reserve_member_ids)
	_party_selected_member_id = member_id
	return _command_ok()


func command_move_member_to_reserve(member_id: StringName) -> Dictionary:
	if _party_state == null:
		return _command_error("当前不存在队伍数据。")
	if not _party_state.active_member_ids.has(member_id):
		return _command_error("%s 当前不在上阵列表中。" % _get_member_display_name(member_id))
	if _party_state.active_member_ids.size() <= 1:
		return _command_error("队伍至少需要保留一名上阵成员。")
	var active_member_ids: Array[StringName] = _party_state.active_member_ids.duplicate()
	var reserve_member_ids: Array[StringName] = _party_state.reserve_member_ids.duplicate()
	active_member_ids.erase(member_id)
	reserve_member_ids.append(member_id)
	_on_party_roster_change_requested(active_member_ids, reserve_member_ids)
	_party_selected_member_id = member_id
	return _command_ok()


func command_party_equip_item(member_id: StringName, item_id: StringName, slot_id: StringName = &"") -> Dictionary:
	if _party_state == null:
		return _command_error("当前不存在队伍数据。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能调整装备。")
	if _active_modal_id == "reward" or _active_modal_id == "promotion" or _active_modal_id == "settlement" or _active_modal_id == "character_info":
		return _command_error("当前窗口会阻止装备切换。")

	var result := _party_equipment_service.equip_item(member_id, item_id, slot_id)
	if not bool(result.get("success", false)):
		return _command_error(_build_equipment_error_message(result, true))

	_party_selected_member_id = member_id
	var success_message := "已为 %s 装备 %s（%s）。" % [
		_get_member_display_name(member_id),
		_get_item_display_name(ProgressionDataUtils.to_string_name(result.get("item_id", ""))),
		String(result.get("slot_label", "")),
	]
	var previous_item_id := ProgressionDataUtils.to_string_name(result.get("previous_item_id", ""))
	if previous_item_id != &"":
		success_message = "已为 %s 装备 %s（%s），并卸下 %s。" % [
			_get_member_display_name(member_id),
			_get_item_display_name(ProgressionDataUtils.to_string_name(result.get("item_id", ""))),
			String(result.get("slot_label", "")),
			_get_item_display_name(previous_item_id),
		]

	var persist_error := _persist_party_state()
	if persist_error == OK:
		_update_status(success_message)
	else:
		_update_status("%s 但队伍状态持久化失败。" % success_message)
	return _command_ok()


func command_party_unequip_item(member_id: StringName, slot_id: StringName) -> Dictionary:
	if _party_state == null:
		return _command_error("当前不存在队伍数据。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能调整装备。")
	if _active_modal_id == "reward" or _active_modal_id == "promotion" or _active_modal_id == "settlement" or _active_modal_id == "character_info":
		return _command_error("当前窗口会阻止装备切换。")

	var result := _party_equipment_service.unequip_item(member_id, slot_id)
	if not bool(result.get("success", false)):
		return _command_error(_build_equipment_error_message(result, false))

	_party_selected_member_id = member_id
	var success_message := "已从 %s 的 %s 卸下 %s。" % [
		_get_member_display_name(member_id),
		String(result.get("slot_label", "")),
		_get_item_display_name(ProgressionDataUtils.to_string_name(result.get("item_id", ""))),
	]
	var persist_error := _persist_party_state()
	if persist_error == OK:
		_update_status(success_message)
	else:
		_update_status("%s 但队伍状态持久化失败。" % success_message)
	return _command_ok()


func command_open_party_warehouse() -> Dictionary:
	if _party_state == null:
		return _command_error("当前不存在队伍数据。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能打开共享仓库。")
	if _active_modal_id == "settlement":
		_open_party_warehouse_window("据点服务")
		_update_status("已从据点窗口打开共享仓库。")
		return _command_ok()
	_open_party_warehouse_window("队伍管理")
	_update_status("已打开共享仓库。")
	return _command_ok()


func command_warehouse_discard_one(item_id: StringName) -> Dictionary:
	if _active_modal_id != "warehouse":
		return _command_error("共享仓库当前未打开。")
	_on_party_warehouse_discard_one_requested(item_id)
	return _command_ok()


func command_warehouse_discard_all(item_id: StringName) -> Dictionary:
	if _active_modal_id != "warehouse":
		return _command_error("共享仓库当前未打开。")
	_on_party_warehouse_discard_all_requested(item_id)
	return _command_ok()


func command_warehouse_use_item(item_id: StringName, member_id: StringName = &"") -> Dictionary:
	if _active_modal_id != "warehouse":
		return _command_error("共享仓库当前未打开。")
	var resolved_member_id := _resolve_warehouse_target_member_id(member_id)
	if resolved_member_id == &"":
		return _command_error("当前没有可使用技能书的目标角色。")
	var use_result := _on_party_warehouse_use_requested(item_id, resolved_member_id)
	if not bool(use_result.get("success", false)):
		return _command_error(String(use_result.get("message", "当前无法使用该物品。")))
	return _command_ok()


func command_warehouse_add_item(item_id: StringName, quantity: int) -> Dictionary:
	if _party_state == null:
		return _command_error("当前不存在队伍数据。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能直接改动共享仓库。")
	if quantity <= 0:
		return _command_error("加入数量必须大于 0。")

	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var result := _party_warehouse_service.add_item(normalized_item_id, quantity)
	var added_quantity := int(result.get("added_quantity", 0))
	if added_quantity <= 0:
		return _command_error("%s 当前无法加入共享仓库。" % _get_item_display_name(normalized_item_id))

	var success_message := "已向共享仓库加入 %d 件 %s。" % [
		added_quantity,
		_get_item_display_name(normalized_item_id),
	]
	var remaining_quantity := int(result.get("remaining_quantity", 0))
	if remaining_quantity > 0:
		success_message = "已向共享仓库加入 %d 件 %s，仍有 %d 件未能放入。" % [
			added_quantity,
			_get_item_display_name(normalized_item_id),
			remaining_quantity,
		]

	var persist_error := _persist_party_state()
	if persist_error == OK:
		_update_status(success_message)
	else:
		_update_status("%s 但队伍状态持久化失败。" % success_message)
	return _command_ok()


func command_execute_settlement_action(action_id: String, payload: Dictionary = {}) -> Dictionary:
	if action_id.is_empty():
		return _command_error("据点动作 ID 不能为空。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能执行据点动作。")
	var settlement_id := _resolve_command_settlement_id()
	if settlement_id.is_empty():
		return _command_error("当前没有可执行动作的据点。")
	var merged_payload := _build_settlement_action_payload(settlement_id, action_id, payload)
	_on_settlement_action_requested(settlement_id, action_id, merged_payload)
	return _command_ok()


func command_battle_tick(total_seconds: float, step_seconds: float = 1.0 / 60.0) -> Dictionary:
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	if total_seconds <= 0.0:
		return _command_error("推进时间必须大于 0。")
	var remaining_seconds := total_seconds
	var delta_seconds := maxf(step_seconds, 1.0 / 60.0)
	while remaining_seconds > 0.0 and _is_battle_active():
		var runtime_state = _get_runtime_battle_state()
		if runtime_state != null and String(runtime_state.modal_state) != "":
			break
		var step := minf(remaining_seconds, delta_seconds)
		var batch = _battle_runtime.advance(step)
		if _batch_has_updates(batch):
			_apply_battle_batch(batch)
		remaining_seconds -= step
	return _command_ok()


func command_battle_select_skill(slot_index: int) -> Dictionary:
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	_select_battle_skill_slot(slot_index)
	return _command_ok("", "overlay")


func command_battle_cycle_variant(step: int) -> Dictionary:
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	_cycle_selected_battle_skill_variant(step)
	return _command_ok("", "overlay")


func command_battle_clear_skill() -> Dictionary:
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	_clear_battle_skill_selection(true)
	return _command_ok("", "overlay")


func command_battle_move_to(target_coord: Vector2i) -> Dictionary:
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	return _command_ok("", String(_attempt_battle_move_to(target_coord)))


func command_battle_move_direction(direction: Vector2i) -> Dictionary:
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	if direction == Vector2i.ZERO:
		return _command_error("战斗移动方向不能为空。")
	return _command_ok("", String(_attempt_battle_move(direction)))


func command_battle_wait_or_resolve() -> Dictionary:
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	_resolve_active_battle()
	return _command_ok()


func command_battle_inspect(coord: Vector2i) -> Dictionary:
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	if _try_open_character_info_at_battle_coord(coord):
		return _command_ok()
	_update_status("该战斗格没有可查看单位。")
	return _command_error(_current_status_message)


func command_confirm_pending_reward() -> Dictionary:
	if _active_mastery_reward == null and not _present_pending_mastery_reward_if_ready():
		return _command_error("当前没有待确认的角色奖励。")
	if _active_mastery_reward == null:
		return _command_error("当前没有待确认的角色奖励。")
	_on_mastery_reward_confirmed()
	return _command_ok()


func command_choose_promotion(profession_id: StringName) -> Dictionary:
	var prompt := _get_current_promotion_prompt()
	if prompt.is_empty():
		return _command_error("当前没有待确认的职业晋升选择。")
	var member_id := ProgressionDataUtils.to_string_name(prompt.get("member_id", ""))
	for choice_variant in prompt.get("choices", []):
		if choice_variant is not Dictionary:
			continue
		var choice_data: Dictionary = choice_variant
		var candidate_profession_id := ProgressionDataUtils.to_string_name(choice_data.get("profession_id", ""))
		if candidate_profession_id != profession_id:
			continue
		var selection: Dictionary = choice_data.get("selection", {}).duplicate(true)
		_on_promotion_choice_submitted(member_id, candidate_profession_id, selection)
		return _command_ok()
	return _command_error("当前晋升列表中不存在职业 %s。" % String(profession_id))


func command_close_active_modal() -> Dictionary:
	match _active_modal_id:
		"settlement":
			_on_settlement_window_closed()
			return _command_ok()
		"character_info":
			_on_character_info_window_closed()
			return _command_ok()
		"party":
			_on_party_management_window_closed()
			return _command_ok()
		"warehouse":
			_on_party_warehouse_window_closed()
			return _command_ok()
		"promotion":
			_update_status("当前晋升选择必须确认后才能继续。")
			return _command_error(_current_status_message)
		"reward":
			_update_status("当前角色奖励必须确认后才能继续。")
			return _command_error(_current_status_message)
		_:
			return _command_error("当前没有可关闭的窗口。")


func apply_party_roster(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName]) -> Dictionary:
	if _party_state == null:
		return _command_error("当前不存在队伍数据。")
	_on_party_roster_change_requested(active_member_ids, reserve_member_ids)
	return _command_ok()


func submit_promotion_choice(member_id: StringName, profession_id: StringName, selection: Dictionary) -> Dictionary:
	_on_promotion_choice_submitted(member_id, profession_id, selection)
	return _command_ok()


func cancel_promotion_choice() -> Dictionary:
	_on_promotion_choice_cancelled()
	return _command_ok()


func confirm_active_reward() -> Dictionary:
	_on_mastery_reward_confirmed()
	return _command_ok()


func reset_battle_focus() -> Dictionary:
	return _command_ok("", String(_reset_battle_movement()))


func select_world_cell(coord: Vector2i) -> Dictionary:
	_on_world_map_cell_clicked(coord)
	return _command_ok()


func inspect_world_cell(coord: Vector2i) -> Dictionary:
	_on_world_map_cell_right_clicked(coord)
	return _command_ok()


func select_battle_cell(coord: Vector2i) -> Dictionary:
	return _command_ok("", String(_attempt_battle_move_to(coord)))


func inspect_battle_cell(coord: Vector2i) -> Dictionary:
	_on_battle_cell_right_clicked(coord)
	return _command_ok()


func _command_ok(message: String = "", battle_refresh_mode: String = "") -> Dictionary:
	var resolved_message := message if not message.is_empty() else _current_status_message
	return {
		"ok": true,
		"message": resolved_message,
		"battle_refresh_mode": battle_refresh_mode,
	}


func _command_error(message: String) -> Dictionary:
	if not message.is_empty():
		_update_status(message)
	return {
		"ok": false,
		"message": message,
	}


func _resolve_command_settlement_id() -> String:
	if not _active_settlement_id.is_empty():
		return _active_settlement_id
	var settlement := _get_settlement_at(_selected_coord)
	return String(settlement.get("settlement_id", ""))


func _build_settlement_action_payload(settlement_id: String, action_id: String, overrides: Dictionary) -> Dictionary:
	var payload: Dictionary = {}
	var window_data: Dictionary = get_settlement_window_data(settlement_id)
	for service_variant in window_data.get("available_services", []):
		if service_variant is not Dictionary:
			continue
		var service_data: Dictionary = service_variant
		if String(service_data.get("action_id", "")) != action_id:
			continue
		payload = {
			"facility_id": service_data.get("facility_id", ""),
			"facility_name": service_data.get("facility_name", ""),
			"npc_id": service_data.get("npc_id", ""),
			"npc_name": service_data.get("npc_name", ""),
			"service_type": service_data.get("service_type", ""),
			"interaction_script_id": service_data.get("interaction_script_id", ""),
		}
		break
	for key in overrides.keys():
		payload[key] = overrides[key]
	if String(payload.get("member_id", "")).is_empty():
		var member_id := _resolve_default_settlement_member_id()
		if member_id != &"":
			payload["member_id"] = String(member_id)
	return payload


func _resolve_default_settlement_member_id() -> StringName:
	if _party_state == null:
		return &""
	if _party_state.leader_member_id != &"" and _party_state.get_member_state(_party_state.leader_member_id) != null:
		return _party_state.leader_member_id
	for member_id_variant in _party_state.active_member_ids:
		var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
		if member_id != &"" and _party_state.get_member_state(member_id) != null:
			return member_id
	for member_key in ProgressionDataUtils.sorted_string_keys(_party_state.member_states):
		var member_id := StringName(member_key)
		if _party_state.get_member_state(member_id) != null:
			return member_id
	return &""


func _execute_settlement_action(settlement_id: String, action_id: String, payload: Dictionary) -> Dictionary:
	var settlement: Dictionary = _settlements_by_id.get(settlement_id, {})
	if settlement.is_empty():
		return {
			"success": false,
			"message": "未找到据点数据。",
			"pending_mastery_rewards": [],
		}

	var display_name: String = settlement.get("display_name", settlement_id)
	var npc_name: String = payload.get("npc_name", "值守人员")
	var facility_name: String = payload.get("facility_name", "设施")
	var service_type: String = payload.get("service_type", "服务")
	var pending_mastery_rewards := _extract_pending_mastery_rewards(action_id, payload, facility_name, npc_name, service_type)

	return {
		"success": true,
		"message": "%s 的 %s 在 %s 中为你处理了“%s”事务。首版窗口流程已接通。"
			% [display_name, npc_name, facility_name, service_type],
		"pending_mastery_rewards": pending_mastery_rewards,
	}


func _finalize_successful_settlement_action(action_id: String, payload: Dictionary, pending_reward_variants: Array = []) -> int:
	_enqueue_pending_mastery_rewards(pending_reward_variants)
	var member_id := ProgressionDataUtils.to_string_name(payload.get("member_id", ""))
	if member_id != &"":
		_character_management.record_achievement_event(
			member_id,
			&"settlement_action_completed",
			1,
			ProgressionDataUtils.to_string_name(action_id)
		)
	_party_state = _character_management.get_party_state()
	return _persist_party_state()


func _extract_pending_mastery_rewards(
	action_id: String,
	payload: Dictionary,
	facility_name: String,
	npc_name: String,
	service_type: String
) -> Array[Dictionary]:
	var rewards: Array[Dictionary] = []
	var default_source_type := _resolve_default_mastery_source_type(action_id, service_type, payload)
	var default_source_label := _resolve_default_mastery_source_label(facility_name, npc_name, service_type)
	var explicit_rewards_variant = payload.get("pending_mastery_rewards", [])
	if explicit_rewards_variant is Array:
		for reward_variant in explicit_rewards_variant:
			if reward_variant is not Dictionary:
				continue
			var reward_data: Dictionary = reward_variant.duplicate(true)
			if String(reward_data.get("member_id", "")).is_empty():
				reward_data["member_id"] = String(payload.get("member_id", ""))
			if String(reward_data.get("source_type", "")).is_empty():
				reward_data["source_type"] = String(default_source_type)
			if String(reward_data.get("source_label", "")).is_empty():
				reward_data["source_label"] = default_source_label
			if reward_data.has("mastery_entries") and not reward_data.has("entries"):
				reward_data["entries"] = reward_data.get("mastery_entries", [])
			rewards.append(reward_data)
	var mastery_entries_variant = payload.get("mastery_entries", [])
	if mastery_entries_variant is Array:
		var member_id := String(payload.get("member_id", ""))
		if not member_id.is_empty():
			rewards.append({
				"member_id": member_id,
				"source_type": String(default_source_type),
				"source_label": default_source_label,
				"entries": mastery_entries_variant.duplicate(true),
				"summary_text": String(payload.get("summary_text", "")),
			})
	return rewards


func _resolve_default_mastery_source_type(action_id: String, service_type: String, payload: Dictionary) -> StringName:
	var explicit_source_type := String(payload.get("mastery_source_type", ""))
	if not explicit_source_type.is_empty():
		return StringName(explicit_source_type)
	var combined_label := "%s %s" % [action_id, service_type]
	if combined_label.contains("传授") or combined_label.contains("指点"):
		return &"npc_teach"
	return &"training"


func _resolve_default_mastery_source_label(facility_name: String, npc_name: String, service_type: String) -> String:
	if not service_type.is_empty():
		return "%s·%s" % [npc_name, service_type]
	if not facility_name.is_empty():
		return facility_name
	return "据点服务"


func _build_world_snapshot() -> Dictionary:
	var selected_settlement := _get_settlement_at(_selected_coord)
	var selected_npc := _get_world_npc_at(_selected_coord)
	var selected_encounter := _get_encounter_anchor_at(_selected_coord)
	return {
		"world_step": int(_world_data.get("world_step", 0)),
		"player_coord": _coord_to_dict(_player_coord),
		"selected_coord": _coord_to_dict(_selected_coord),
		"selected_settlement_id": String(selected_settlement.get("settlement_id", "")),
		"selected_npc_name": String(selected_npc.get("display_name", "")),
		"selected_encounter_id": String(selected_encounter.entity_id) if selected_encounter != null else "",
		"selected_encounter_name": String(selected_encounter.display_name) if selected_encounter != null else "",
		"selected_encounter_kind": String(selected_encounter.encounter_kind) if selected_encounter != null else "",
		"selected_encounter_growth_stage": int(selected_encounter.growth_stage) if selected_encounter != null else 0,
		"nearby_encounters": _build_nearby_encounter_entries(),
	}


func _build_party_snapshot() -> Dictionary:
	var members: Array[Dictionary] = []
	if _party_state != null:
		for member_id in _party_state.active_member_ids:
			members.append(_build_party_member_snapshot(member_id, "active"))
		for member_id in _party_state.reserve_member_ids:
			members.append(_build_party_member_snapshot(member_id, "reserve"))
	return {
		"leader_member_id": String(_party_state.leader_member_id) if _party_state != null else "",
		"active_member_ids": _string_name_array_to_string_array(_party_state.active_member_ids if _party_state != null else []),
		"reserve_member_ids": _string_name_array_to_string_array(_party_state.reserve_member_ids if _party_state != null else []),
		"selected_member_id": String(_party_selected_member_id),
		"pending_reward_count": _party_state.pending_character_rewards.size() if _party_state != null else 0,
		"members": members,
	}


func _build_party_member_snapshot(member_id: StringName, roster_role: String) -> Dictionary:
	var member_state = _party_state.get_member_state(member_id) if _party_state != null else null
	var achievement_summary := _character_management.get_member_achievement_summary(member_id) if _character_management != null else {}
	var attribute_snapshot = _character_management.get_member_attribute_snapshot(member_id) if _character_management != null else null
	var equipment_entries := _party_equipment_service.get_equipped_entries(member_id) if _party_equipment_service != null else []
	return {
		"member_id": String(member_id),
		"display_name": _get_member_display_name(member_id),
		"roster_role": roster_role,
		"is_leader": _party_state != null and _party_state.leader_member_id == member_id,
		"current_hp": int(member_state.current_hp) if member_state != null else 0,
		"current_mp": int(member_state.current_mp) if member_state != null else 0,
		"learned_skill_ids": _build_member_learned_skill_ids(member_state),
		"achievement_summary": achievement_summary.duplicate(true) if achievement_summary is Dictionary else {},
		"attributes": attribute_snapshot.to_dict() if attribute_snapshot is Object and attribute_snapshot.has_method("to_dict") else {},
		"equipment": equipment_entries,
		"equipment_count": equipment_entries.size(),
	}


func _build_member_learned_skill_ids(member_state) -> Array[String]:
	var learned_skill_ids: Array[String] = []
	if member_state == null or member_state.progression == null:
		return learned_skill_ids
	for skill_key in member_state.progression.skills.keys():
		var skill_id := ProgressionDataUtils.to_string_name(skill_key)
		var skill_progress = member_state.progression.get_skill_progress(skill_id)
		if skill_progress == null or not skill_progress.is_learned:
			continue
		learned_skill_ids.append(String(skill_id))
	learned_skill_ids.sort()
	return learned_skill_ids


func _build_settlement_snapshot() -> Dictionary:
	var settlement_id := _resolve_command_settlement_id()
	var window_data: Dictionary = get_settlement_window_data(settlement_id) if not settlement_id.is_empty() else {}
	var services: Array[Dictionary] = []
	for service_variant in window_data.get("available_services", []):
		if service_variant is not Dictionary:
			continue
		var service_data: Dictionary = service_variant
		services.append({
			"action_id": String(service_data.get("action_id", "")),
			"facility_name": String(service_data.get("facility_name", "")),
			"npc_name": String(service_data.get("npc_name", "")),
			"service_type": String(service_data.get("service_type", "")),
			"interaction_script_id": String(service_data.get("interaction_script_id", "")),
		})
	return {
		"visible": _active_modal_id == "settlement",
		"settlement_id": settlement_id,
		"display_name": String(window_data.get("display_name", "")),
		"tier_name": String(window_data.get("tier_name", "")),
		"faction_id": String(window_data.get("faction_id", "")),
		"services": services,
		"feedback_text": _active_settlement_feedback_text,
	}


func _build_character_info_snapshot() -> Dictionary:
	var context := _active_character_info_context.duplicate(true)
	context["visible"] = _active_modal_id == "character_info"
	if context.has("coord"):
		context["coord"] = _coord_to_dict(context.get("coord", Vector2i.ZERO))
	return context


func _build_warehouse_snapshot() -> Dictionary:
	return {
		"visible": _active_modal_id == "warehouse",
		"entry_label": _active_warehouse_entry_label,
		"window_data": _build_party_warehouse_window_data() if _party_state != null else {},
	}


func _build_battle_snapshot() -> Dictionary:
	if _battle_state == null or _battle_state.is_empty():
		return {
			"active": false,
		}
	var adapter = BATTLE_HUD_ADAPTER_SCRIPT.new()
	var hud_snapshot := adapter.build_snapshot(
		_battle_state,
		_battle_selected_coord,
		_selected_battle_skill_id,
		_get_selected_battle_skill_name(),
		_get_selected_battle_skill_variant_name(),
		_queued_battle_skill_target_coords,
		_get_selected_battle_skill_required_coord_count()
	)
	var units: Array[Dictionary] = []
	for unit_id_str in ProgressionDataUtils.sorted_string_keys(_battle_state.units):
		var unit_id := StringName(unit_id_str)
		var unit_state := _battle_state.units.get(unit_id) as BattleUnitState
		if unit_state == null:
			continue
		units.append({
			"unit_id": String(unit_state.unit_id),
			"display_name": unit_state.display_name if not unit_state.display_name.is_empty() else String(unit_state.unit_id),
			"coord": _coord_to_dict(unit_state.coord),
			"faction_id": String(unit_state.faction_id),
			"control_mode": String(unit_state.control_mode),
			"is_alive": unit_state.is_alive,
			"current_hp": int(unit_state.current_hp),
			"current_mp": int(unit_state.current_mp),
			"current_ap": int(unit_state.current_ap),
		})
	return {
		"active": true,
		"encounter_id": String(_active_battle_encounter_id),
		"encounter_name": _active_battle_encounter_name,
		"phase": String(_battle_state.phase),
		"active_unit_id": String(_battle_state.active_unit_id),
		"active_unit_name": _get_battle_active_unit_name(),
		"modal_state": String(_battle_state.modal_state),
		"winner_faction_id": String(_battle_state.winner_faction_id),
		"selected_coord": _coord_to_dict(_battle_selected_coord),
		"selected_skill_id": String(_selected_battle_skill_id),
		"selected_skill_variant_id": String(_selected_battle_skill_variant_id),
		"selected_target_coords": _coord_array_to_dict_array(_queued_battle_skill_target_coords),
		"terrain_counts": _count_battle_terrain_types(),
		"hud": hud_snapshot,
		"units": units,
	}


func _build_reward_snapshot() -> Dictionary:
	var reward = _active_mastery_reward if _active_mastery_reward != null else (_party_state.get_next_pending_character_reward() if _party_state != null else null)
	return {
		"visible": _active_modal_id == "reward",
		"remaining_count": _party_state.pending_character_rewards.size() if _party_state != null else 0,
		"reward": reward.to_dict() if reward != null and reward.has_method("to_dict") else {},
	}


func _build_promotion_snapshot() -> Dictionary:
	var prompt := _get_current_promotion_prompt()
	return {
		"visible": _active_modal_id == "promotion",
		"prompt": prompt.duplicate(true),
	}


func _get_current_promotion_prompt() -> Dictionary:
	if not _pending_promotion_prompt.is_empty():
		return _pending_promotion_prompt
	if not _pending_world_promotion_prompt.is_empty():
		return _pending_world_promotion_prompt
	return {}


func _coord_to_dict(coord: Vector2i) -> Dictionary:
	return {
		"x": coord.x,
		"y": coord.y,
	}


func _coord_array_to_dict_array(coords: Array[Vector2i]) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for coord in coords:
		result.append(_coord_to_dict(coord))
	return result


func _string_name_array_to_string_array(values: Array) -> Array[String]:
	var result: Array[String] = []
	for value in values:
		result.append(String(value))
	return result


func _build_nearby_encounter_entries(limit: int = 8) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	for encounter_variant in _world_data.get("encounter_anchors", []):
		var encounter = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter == null or encounter.is_cleared:
			continue
		var delta: Vector2i = encounter.world_coord - _player_coord
		entries.append({
			"entity_id": String(encounter.entity_id),
			"display_name": String(encounter.display_name),
			"coord": _coord_to_dict(encounter.world_coord),
			"distance": absi(delta.x) + absi(delta.y),
			"encounter_kind": String(encounter.encounter_kind),
			"growth_stage": int(encounter.growth_stage),
		})
	entries.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		var distance_a := int(a.get("distance", 0))
		var distance_b := int(b.get("distance", 0))
		if distance_a == distance_b:
			return String(a.get("entity_id", "")) < String(b.get("entity_id", ""))
		return distance_a < distance_b
	)
	if entries.size() > limit:
		entries.resize(limit)
	return entries


func _unhandled_input(event: InputEvent) -> void:
	if _generation_config == null:
		return
	if event is not InputEventKey:
		return

	var key_event := event as InputEventKey

	if _is_battle_active():
		if _is_modal_window_open():
			return
		if not key_event.pressed or key_event.echo:
			return
		if _handle_battle_input(key_event):
			get_viewport().set_input_as_handled()
		return

	if _handle_world_input(key_event):
		get_viewport().set_input_as_handled()


func _handle_world_input(key_event: InputEventKey) -> bool:
	var movement := _get_world_move_direction_for_key(key_event.keycode)
	if not key_event.pressed:
		if movement == Vector2i.ZERO:
			return false
		_release_world_move_key(key_event.keycode)
		return true

	if key_event.echo:
		return movement != Vector2i.ZERO

	if _is_modal_window_open():
		return false

	if movement != Vector2i.ZERO:
		_press_world_move_key(key_event.keycode)
		_move_player(movement)
		if _is_battle_active():
			_clear_world_move_hold()
		return true

	match key_event.keycode:
		KEY_P:
			_open_party_management_window()
			return true
		KEY_ENTER, KEY_KP_ENTER, KEY_SPACE:
			_try_open_settlement_at(_selected_coord)
			return true
		_:
			return false


func _process_world_held_movement(delta: float) -> void:
	if _held_world_move_keys.is_empty():
		_world_move_repeat_timer = 0.0
		return

	_world_move_repeat_timer -= delta
	while _world_move_repeat_timer <= 0.0:
		var movement := _get_active_world_move_direction()
		if movement == Vector2i.ZERO:
			_clear_world_move_hold()
			return

		_move_player(movement)
		if _is_battle_active() or _is_modal_window_open():
			_clear_world_move_hold()
			return
		_world_move_repeat_timer += WORLD_MOVE_REPEAT_INTERVAL


func _get_world_move_direction_for_key(keycode: int) -> Vector2i:
	match keycode:
		KEY_LEFT, KEY_A:
			return Vector2i.LEFT
		KEY_RIGHT, KEY_D:
			return Vector2i.RIGHT
		KEY_UP, KEY_W:
			return Vector2i.UP
		KEY_DOWN, KEY_S:
			return Vector2i.DOWN
		_:
			return Vector2i.ZERO


func _press_world_move_key(keycode: int) -> void:
	_held_world_move_keys.erase(keycode)
	_held_world_move_keys.append(keycode)
	_world_move_repeat_timer = WORLD_MOVE_REPEAT_INTERVAL


func _release_world_move_key(keycode: int) -> void:
	var was_active := keycode == _get_active_world_move_keycode()
	_held_world_move_keys.erase(keycode)
	if _held_world_move_keys.is_empty():
		_world_move_repeat_timer = 0.0
	elif was_active:
		_world_move_repeat_timer = WORLD_MOVE_REPEAT_INTERVAL


func _get_active_world_move_direction() -> Vector2i:
	var keycode := _get_active_world_move_keycode()
	if keycode == KEY_NONE:
		return Vector2i.ZERO
	return _get_world_move_direction_for_key(keycode)


func _get_active_world_move_keycode() -> int:
	if _held_world_move_keys.is_empty():
		return KEY_NONE
	return _held_world_move_keys[-1]


func _clear_world_move_hold() -> void:
	_held_world_move_keys.clear()
	_world_move_repeat_timer = 0.0


func _handle_battle_input(key_event: InputEventKey) -> bool:
	match key_event.keycode:
		KEY_1, KEY_2, KEY_3, KEY_4, KEY_5, KEY_6, KEY_7, KEY_8, KEY_9:
			_select_battle_skill_slot(int(key_event.keycode - KEY_1))
		KEY_Q:
			_cycle_selected_battle_skill_variant(-1)
		KEY_E:
			_cycle_selected_battle_skill_variant(1)
		KEY_ESCAPE:
			_clear_battle_skill_selection(true)
		KEY_LEFT, KEY_A:
			_attempt_battle_move(Vector2i.LEFT)
		KEY_RIGHT, KEY_D:
			_attempt_battle_move(Vector2i.RIGHT)
		KEY_UP, KEY_W:
			_attempt_battle_move(Vector2i.UP)
		KEY_DOWN, KEY_S:
			_attempt_battle_move(Vector2i.DOWN)
		KEY_R:
			_reset_battle_movement()
		KEY_ENTER, KEY_KP_ENTER, KEY_SPACE:
			_resolve_active_battle()
		_:
			return false
	return true


func _move_player(direction: Vector2i) -> void:
	var previous_settlement := _get_settlement_at(_player_coord)
	var target_coord := _player_coord + direction
	if not _grid_system.is_cell_walkable(target_coord):
		_update_status("已到达大地图边界。")
		return

	_player_coord = target_coord
	_selected_coord = _player_coord
	_advance_world_time_by_steps(1)
	_refresh_fog()

	var encountered_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT = _get_encounter_anchor_at(_player_coord)
	if encountered_anchor != null:
		_game_session.set_battle_save_lock(true)
		var player_persist_error: int = int(_game_session.set_player_coord(_player_coord))
		var world_persist_error: int = int(_game_session.set_world_data(_world_data))
		_start_battle(encountered_anchor)
		if not _is_battle_active():
			_game_session.set_battle_save_lock(false)
			var flush_error: int = int(_game_session.flush_game_state())
			if player_persist_error != OK or world_persist_error != OK or flush_error != OK:
				_update_status("遭遇战未能开始，且玩家位置或世界时间持久化失败。")
			else:
				_update_status("遭遇战未能开始，已保留玩家当前位置与世界时间。")
		return

	var player_persist_error: int = int(_game_session.set_player_coord(_player_coord))
	var world_persist_error: int = int(_game_session.set_world_data(_world_data))
	var current_settlement := _get_settlement_at(_player_coord)
	var entered_new_settlement := (
		not current_settlement.is_empty()
		and String(current_settlement.get("settlement_id", "")) != String(previous_settlement.get("settlement_id", ""))
	)
	if entered_new_settlement and _try_open_settlement_at(_player_coord, false):
		if player_persist_error != OK or world_persist_error != OK:
			_update_status("已打开 %s 的据点窗口，但玩家位置或世界时间持久化失败。" % current_settlement.get("display_name", "据点"))
		return
	if player_persist_error == OK and world_persist_error == OK:
		_update_status("玩家移动到 %s，视野与世界时间已刷新。" % _format_coord(_player_coord))
	else:
		_update_status("玩家移动到 %s，但大地图位置或世界时间持久化失败。" % _format_coord(_player_coord))


func _advance_world_time_by_steps(delta_steps: int) -> void:
	var advance_result := _world_time_system.advance(_world_data, delta_steps)
	_wild_encounter_growth_system.apply_step_advance(
		_world_data,
		int(advance_result.get("old_step", 0)),
		int(advance_result.get("new_step", 0)),
		_wild_encounter_rosters
	)


func _resolve_world_encounter_after_battle(winner_faction_id: String) -> void:
	if winner_faction_id != "player":
		return
	var encounter_anchor := _get_encounter_anchor_by_id(_active_battle_encounter_id)
	if encounter_anchor == null:
		return
	if encounter_anchor.encounter_kind == ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT:
		_wild_encounter_growth_system.apply_battle_victory(
			encounter_anchor,
			int(_world_data.get("world_step", 0)),
			_wild_encounter_rosters
		)
		return
	_remove_active_battle_encounter_anchor()


func _start_battle(encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT) -> void:
	_active_battle_encounter_id = encounter_anchor.entity_id
	_active_battle_encounter_name = encounter_anchor.display_name
	_pending_promotion_prompt.clear()
	_clear_battle_skill_selection()
	_character_management.set_party_state(_party_state)

	var runtime_state = _battle_runtime.start_battle(
		encounter_anchor,
		_build_battle_seed(encounter_anchor),
		_build_battle_start_context(encounter_anchor)
	)
	if runtime_state == null or runtime_state.is_empty():
		_active_battle_encounter_id = &""
		_active_battle_encounter_name = ""
		_battle_state = null
		_battle_selected_coord = Vector2i(-1, -1)
		_update_status("遭遇战生成失败。")
		return

	_refresh_battle_runtime_state()
	_update_status("遭遇 %s，世界地图停止渲染，已切入正式战斗。" % _active_battle_encounter_name)


func _build_battle_start_context(encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT) -> Dictionary:
	var context := {
		"world_coord": encounter_anchor.world_coord if encounter_anchor != null else _player_coord,
	}
	context["battle_terrain_profile"] = String(_resolve_battle_terrain_profile(encounter_anchor))
	return context


func _resolve_battle_terrain_profile(encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT) -> StringName:
	if encounter_anchor == null:
		return &"default"
	match String(encounter_anchor.region_tag).strip_edges().to_lower():
		"canyon":
			return &"canyon"
		_:
			return &"default"


func _resolve_active_battle() -> void:
	if not _is_battle_active():
		return

	if not _is_battle_finished():
		var wait_command = _build_wait_command()
		if wait_command == null:
			_update_status("当前尚未到可操作单位或战斗结果未结算。")
			return
		_issue_battle_command(wait_command)
		return

	var runtime_state = _get_runtime_battle_state()
	var winner_faction_id := String(runtime_state.winner_faction_id) if runtime_state != null else ""
	var pending_mastery_rewards: Array = _battle_runtime.consume_pending_mastery_rewards()
	_battle_runtime.end_battle({"commit_progression": true})
	_character_management.enqueue_pending_character_rewards(pending_mastery_rewards)
	_party_state = _character_management.get_party_state()
	var party_persist_error: int = int(_game_session.set_party_state(_party_state))
	_resolve_world_encounter_after_battle(winner_faction_id)
	var world_persist_error: int = int(_game_session.set_world_data(_world_data))
	_game_session.set_battle_save_lock(false)
	var flush_error: int = int(_game_session.flush_game_state())

	_active_modal_id = ""
	_pending_promotion_prompt.clear()
	_clear_battle_skill_selection()
	_battle_runtime.dispose()
	_battle_state = null
	_battle_selected_coord = Vector2i(-1, -1)
	var battle_name := _active_battle_encounter_name if not _active_battle_encounter_name.is_empty() else "遭遇"
	_active_battle_encounter_id = &""
	_active_battle_encounter_name = ""
	_selected_coord = _player_coord

	_refresh_fog()

	if party_persist_error == OK and world_persist_error == OK and flush_error == OK:
		_update_status("%s 战斗结束，胜利方：%s。已返回世界地图并统一保存。" % [
			battle_name,
			_format_faction_label(winner_faction_id),
		])
	else:
		_update_status("%s 战斗结束，但战后持久化失败。" % battle_name)
	_present_pending_mastery_reward_if_ready()


func _attempt_battle_move(direction: Vector2i) -> StringName:
	if not _is_battle_active():
		return &"full"
	var active_unit = _get_manual_active_unit()
	if active_unit == null:
		_update_status("当前没有可手动操作的单位。")
		return &"overlay"
	return _attempt_battle_move_to(active_unit.coord + direction)


func _attempt_battle_move_to(target_coord: Vector2i) -> StringName:
	if not _is_battle_active():
		return &"full"

	_battle_selected_coord = target_coord
	if _battle_state == null or not _battle_state.cells.has(target_coord):
		_refresh_battle_selection_state()
		_update_status("该战斗格超出当前战场范围。")
		return &"overlay"

	var active_unit = _get_manual_active_unit()
	if active_unit == null:
		_refresh_battle_selection_state()
		_update_status("等待当前单位进入可操作状态。")
		return &"overlay"

	if _is_selected_ground_skill_ready(active_unit):
		return _handle_selected_ground_skill_click(active_unit, target_coord)

	if active_unit.occupies_coord(target_coord):
		_refresh_battle_selection_state()
		_update_status("已选中当前行动单位。")
		return &"overlay"

	var target_unit = _get_runtime_unit_at_coord(target_coord)
	if target_unit != null and target_unit.unit_id != active_unit.unit_id:
		var skill_command = _build_selected_skill_command(active_unit, target_unit)
		if skill_command != null:
			return _issue_battle_command(skill_command)

		skill_command = _build_skill_command(active_unit, target_unit)
		if skill_command != null:
			return _issue_battle_command(skill_command)

	var move_command = BATTLE_COMMAND_SCRIPT.new()
	move_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_MOVE
	move_command.unit_id = active_unit.unit_id
	move_command.target_coord = target_coord
	var preview = _battle_runtime.preview_command(move_command)
	if preview != null and preview.allowed:
		return _issue_battle_command(move_command)

	_refresh_battle_selection_state()
	if preview != null and not preview.log_lines.is_empty():
		_update_status(String(preview.log_lines[-1]))
	else:
		_update_status("已选中战斗格 %s。" % _format_coord(target_coord))
	return &"overlay"


func _reset_battle_movement() -> StringName:
	if not _is_battle_active():
		return &"full"

	var active_unit = _get_runtime_active_unit()
	if active_unit == null:
		_update_status("当前没有可聚焦的行动单位。")
		return &"overlay"

	_battle_selected_coord = active_unit.coord
	_refresh_battle_selection_state()
	_update_status("已聚焦当前行动单位。")
	return &"overlay"


func _refresh_fog() -> void:
	var leader_member_id := "player_main"
	if _party_state != null and _party_state.leader_member_id != &"":
		leader_member_id = String(_party_state.leader_member_id)
	var sources: Array = [
		VISION_SOURCE_DATA_SCRIPT.new(leader_member_id, _player_coord, _generation_config.player_vision_range, _player_faction_id),
	]
	_fog_system.rebuild_visibility_for_faction(_player_faction_id, sources)


func _on_world_map_cell_clicked(coord: Vector2i) -> void:
	if _is_battle_active():
		return
	if _is_modal_window_open():
		return

	_selected_coord = coord

	if _fog_system.is_visible(coord, _player_faction_id):
		if _try_open_settlement_at(coord):
			return

	_update_status("已选中格子 %s。" % _format_coord(coord))


func _on_world_map_cell_right_clicked(coord: Vector2i) -> void:
	if _is_battle_active():
		return
	if _is_modal_window_open():
		return
	if not _fog_system.is_visible(coord, _player_faction_id):
		_update_status("该格当前不在视野中。")
		return
	if _try_open_character_info_at_world_coord(coord):
		return

	_update_status("当前格没有可查看人物。")


func _on_battle_cell_clicked(coord: Vector2i) -> void:
	if not _is_battle_active():
		return
	if _is_modal_window_open():
		return

	_attempt_battle_move_to(coord)


func _on_battle_cell_right_clicked(coord: Vector2i) -> void:
	if not _is_battle_active():
		return
	if _is_modal_window_open():
		return
	if _try_open_character_info_at_battle_coord(coord):
		return

	_update_status("该战斗格没有可查看单位。")


func _on_battle_skill_slot_selected(index: int) -> void:
	if _is_modal_window_open():
		return
	_select_battle_skill_slot(index)


func _on_battle_skill_variant_cycle_requested(step: int) -> void:
	if _is_modal_window_open():
		return
	_cycle_selected_battle_skill_variant(step)


func _on_battle_skill_clear_requested() -> void:
	if _is_modal_window_open():
		return
	_clear_battle_skill_selection(true)


func _try_open_settlement_at(coord: Vector2i, announce_failure: bool = true) -> bool:
	if _is_battle_active():
		return false
	if not _fog_system.is_visible(coord, _player_faction_id):
		if announce_failure:
			_update_status("该格当前不在视野中。")
		return false

	var settlement := _get_settlement_at(coord)
	if settlement.is_empty():
		if announce_failure:
			_update_status("当前格没有可交互据点。")
		return false

	_active_settlement_id = String(settlement.get("settlement_id", ""))
	_active_settlement_feedback_text = "据点通过窗口交付，不切换到城内地图。"
	_active_modal_id = "settlement"
	_update_status("已打开 %s 的据点窗口。" % settlement.get("display_name", "据点"))
	return true


func _try_open_character_info_at_world_coord(coord: Vector2i) -> bool:
	var npc := _get_world_npc_at(coord)
	if npc.is_empty():
		return false

	var display_name: String = npc.get("display_name", "NPC")
	_active_character_info_context = {
		"display_name": display_name,
		"type_label": "世界 NPC",
		"faction_label": _format_faction_label(String(npc.get("faction_id", "neutral"))),
		"coord": coord,
		"status_label": "可见提示单位",
		"source": "world",
	}
	_active_modal_id = "character_info"
	_update_status("已打开 %s 的人物信息窗。" % display_name)
	return true


func _try_open_character_info_at_battle_coord(coord: Vector2i) -> bool:
	var unit := _get_battle_unit_at_coord(coord)
	if unit == null:
		return false

	var unit_id := String(unit.unit_id)
	var display_name := unit.display_name if not unit.display_name.is_empty() else unit_id
	var faction_id := String(unit.faction_id)
	var status_label := "当前行动单位" if unit.unit_id == _battle_state.active_unit_id else "战斗单位"
	_active_character_info_context = {
		"display_name": display_name,
		"type_label": _get_battle_unit_type_label(unit_id),
		"faction_label": _format_faction_label(faction_id),
		"coord": unit.coord,
		"status_label": status_label,
		"source": "battle",
		"unit_id": unit_id,
	}
	_active_modal_id = "character_info"
	_update_status("已打开 %s 的人物信息窗。" % display_name)
	return true


func _get_settlement_at(coord: Vector2i) -> Dictionary:
	return _settlement_by_coord.get(coord, {})


func _get_world_npc_at(coord: Vector2i) -> Dictionary:
	return _world_npc_by_coord.get(coord, {})


func _get_encounter_anchor_at(coord: Vector2i) -> ENCOUNTER_ANCHOR_DATA_SCRIPT:
	return _encounter_anchor_by_coord.get(coord, null) as ENCOUNTER_ANCHOR_DATA_SCRIPT


func _get_encounter_anchor_by_id(entity_id: StringName) -> ENCOUNTER_ANCHOR_DATA_SCRIPT:
	if entity_id == &"":
		return null
	for encounter_variant in _world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		if encounter_anchor.entity_id == entity_id:
			return encounter_anchor
	return null


func _refresh_battle_panel() -> void:
	if not _is_battle_active():
		return
	battle_map_panel.refresh(
		_battle_state,
		_battle_selected_coord,
		_selected_battle_skill_id,
		_get_selected_battle_skill_name(),
		_get_selected_battle_skill_variant_name(),
		_queued_battle_skill_target_coords,
		_get_selected_battle_skill_required_coord_count()
	)


func _refresh_battle_panel_overlay() -> void:
	if not _is_battle_active():
		return
	battle_map_panel.refresh_overlay(
		_battle_state,
		_battle_selected_coord,
		_selected_battle_skill_id,
		_get_selected_battle_skill_name(),
		_get_selected_battle_skill_variant_name(),
		_queued_battle_skill_target_coords,
		_get_selected_battle_skill_required_coord_count()
	)


func _refresh_battle_selection_state() -> void:
	if not _is_battle_active():
		return
	_sync_selected_battle_skill_state()
	if _battle_state == null or _battle_state.is_empty():
		_refresh_battle_runtime_state()
		return
	if _battle_selected_coord == Vector2i(-1, -1) or not _battle_state.cells.has(_battle_selected_coord):
		_battle_selected_coord = _get_default_battle_selected_coord()
	_refresh_battle_panel_overlay()


func _set_world_view_active() -> void:
	if world_map_background != null:
		world_map_background.visible = true
	world_map_view.visible = true
	battle_map_panel.hide_battle()
	world_map_view.refresh_world(_world_data)
	world_map_view.set_runtime_state(_player_coord, _selected_coord)


func _set_battle_view_active() -> void:
	if world_map_background != null:
		world_map_background.visible = false
	world_map_view.visible = false
	battle_map_panel.show_battle(
		_battle_state,
		_battle_selected_coord,
		_selected_battle_skill_id,
		_get_selected_battle_skill_name(),
		_get_selected_battle_skill_variant_name(),
		_queued_battle_skill_target_coords,
		_get_selected_battle_skill_required_coord_count()
	)


func _remove_active_battle_encounter_anchor() -> void:
	if _active_battle_encounter_id == &"":
		return

	var remaining_anchors: Array = []
	for encounter_anchor_data in _world_data.get("encounter_anchors", []):
		var encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT = encounter_anchor_data as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		if encounter_anchor.entity_id == _active_battle_encounter_id:
			continue
		remaining_anchors.append(encounter_anchor)

	_world_data["encounter_anchors"] = remaining_anchors
	_rebuild_world_coord_lookups()


func _on_settlement_action_requested(settlement_id: String, action_id: String, payload: Dictionary) -> void:
	if String(payload.get("interaction_script_id", "")) == PARTY_WAREHOUSE_INTERACTION_ID:
		var persist_error := _finalize_successful_settlement_action(action_id, payload)
		_active_settlement_id = settlement_id
		_active_modal_id = ""
		_open_party_warehouse_window("据点服务：%s·%s" % [
			String(payload.get("facility_name", "设施")),
			String(payload.get("npc_name", "值守人员")),
		])
		if persist_error == OK:
			_update_status("已从据点服务打开共享仓库。")
		else:
			_update_status("已从据点服务打开共享仓库，但队伍状态持久化失败。")
		return

	var result: Dictionary = _execute_settlement_action(settlement_id, action_id, payload)
	var message: String = result.get("message", "交互已完成。")
	_active_settlement_feedback_text = message
	if bool(result.get("success", false)):
		var persist_error := _finalize_successful_settlement_action(action_id, payload, result.get("pending_mastery_rewards", []))
		if persist_error == OK:
			_update_status(message)
		else:
			_update_status("%s 但队伍状态持久化失败。" % message)
		return

	_update_status(message)


func _on_settlement_window_closed() -> void:
	_active_settlement_id = ""
	_active_settlement_feedback_text = ""
	_active_modal_id = ""
	_update_status("已关闭据点窗口，返回世界地图。")
	_present_pending_mastery_reward_if_ready()


func _on_character_info_window_closed() -> void:
	_active_character_info_context.clear()
	_active_modal_id = ""
	_update_status("已关闭人物信息窗。")
	_present_pending_mastery_reward_if_ready()


func _open_party_management_window() -> void:
	if _is_battle_active():
		return
	_active_modal_id = "party"
	if _party_selected_member_id == &"" and _party_state != null and not _party_state.active_member_ids.is_empty():
		_party_selected_member_id = _party_state.active_member_ids[0]
	_update_status("已打开队伍管理窗口。")


func _open_party_warehouse_window(entry_label: String) -> void:
	if _is_battle_active():
		return
	_active_modal_id = "warehouse"
	_active_warehouse_entry_label = entry_label if not entry_label.is_empty() else "共享入口"
	_party_warehouse_service.setup(_party_state, _game_session.get_item_defs())


func _on_party_leader_change_requested(member_id: StringName) -> void:
	if _party_state == null:
		return
	_party_state.leader_member_id = member_id
	_apply_party_state_to_runtime("队长已切换为 %s。" % String(member_id))


func _on_party_roster_change_requested(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName]) -> void:
	if _party_state == null:
		return
	_party_state.active_member_ids = active_member_ids.duplicate()
	_party_state.reserve_member_ids = reserve_member_ids.duplicate()
	if not _party_state.active_member_ids.has(_party_state.leader_member_id) and not _party_state.active_member_ids.is_empty():
		_party_state.leader_member_id = _party_state.active_member_ids[0]
	_apply_party_state_to_runtime("队伍编成已更新。")


func _on_party_management_window_closed() -> void:
	_active_modal_id = ""
	_update_status("已关闭队伍管理窗口。")
	_present_pending_mastery_reward_if_ready()


func _on_party_management_warehouse_requested() -> void:
	_active_modal_id = ""
	_open_party_warehouse_window("队伍管理")
	_update_status("已从队伍管理打开共享仓库。")


func _on_party_warehouse_discard_one_requested(item_id: StringName) -> void:
	var item_name := _get_item_display_name(item_id)
	var result := _party_warehouse_service.remove_item(item_id, 1)
	if int(result.get("removed_quantity", 0)) <= 0:
		_refresh_party_warehouse_window()
		_update_status("%s 当前没有可丢弃的库存。" % item_name)
		return

	var persist_error := _persist_party_state()
	if persist_error == OK:
		_update_status("已从共享仓库丢弃 1 件 %s。" % item_name)
	else:
		_update_status("已从共享仓库丢弃 1 件 %s，但队伍状态持久化失败。" % item_name)


func _on_party_warehouse_discard_all_requested(item_id: StringName) -> void:
	var item_name := _get_item_display_name(item_id)
	var total_quantity := _party_warehouse_service.count_item(item_id)
	if total_quantity <= 0:
		_refresh_party_warehouse_window()
		_update_status("%s 当前没有可丢弃的库存。" % item_name)
		return

	var result := _party_warehouse_service.remove_item(item_id, total_quantity)
	var removed_quantity := int(result.get("removed_quantity", 0))
	if removed_quantity <= 0:
		_refresh_party_warehouse_window()
		_update_status("%s 当前没有可丢弃的库存。" % item_name)
		return

	var persist_error := _persist_party_state()
	if persist_error == OK:
		_update_status("已从共享仓库丢弃全部 %s，共 %d 件。" % [item_name, removed_quantity])
	else:
		_update_status("已从共享仓库丢弃全部 %s，但队伍状态持久化失败。" % item_name)


func _on_party_warehouse_use_requested(item_id: StringName, member_id: StringName) -> Dictionary:
	var resolved_member_id := _resolve_warehouse_target_member_id(member_id)
	var use_result := _party_item_use_service.use_item(item_id, resolved_member_id)
	if not bool(use_result.get("success", false)):
		var failure_message := _build_warehouse_use_failure_message(use_result)
		use_result["message"] = failure_message
		_refresh_party_warehouse_window()
		_update_status(failure_message)
		return use_result

	_party_selected_member_id = resolved_member_id
	var item_name := _get_item_display_name(item_id)
	var skill_name := _get_skill_display_name(ProgressionDataUtils.to_string_name(use_result.get("skill_id", "")))
	var member_name := _get_member_display_name(resolved_member_id)
	var persist_error := _persist_party_state()
	if persist_error == OK:
		use_result["message"] = "已让 %s 使用 %s，学会 %s。" % [member_name, item_name, skill_name]
	else:
		use_result["message"] = "已让 %s 使用 %s，学会 %s，但队伍状态持久化失败。" % [
			member_name,
			item_name,
			skill_name,
		]
	_update_status(String(use_result.get("message", "")))
	return use_result


func _on_party_warehouse_window_closed() -> void:
	_active_modal_id = ""
	_active_warehouse_entry_label = ""
	_update_status("已关闭共享仓库。")
	_present_pending_mastery_reward_if_ready()


func _on_promotion_choice_submitted(member_id: StringName, profession_id: StringName, selection: Dictionary) -> void:
	if _is_battle_active():
		_pending_promotion_prompt.clear()
		_active_modal_id = ""
		var batch = _battle_runtime.submit_promotion_choice(member_id, profession_id, selection)
		_apply_battle_batch(batch)
		return

	if _pending_world_promotion_prompt.is_empty():
		return
	_pending_world_promotion_prompt.clear()
	_active_modal_id = ""
	var delta = _character_management.promote_profession(member_id, profession_id, selection)
	_party_state = _character_management.get_party_state()
	var persist_error := _persist_party_state()
	if delta.needs_promotion_modal:
		_pending_world_promotion_prompt = _build_promotion_prompt(delta, "确认后将在世界地图立即生效。")
		_active_modal_id = "promotion"
		if persist_error == OK:
			_update_status("%s 完成晋升后还有后续抉择待确认。" % _get_member_display_name(member_id))
		else:
			_update_status("%s 的晋升已应用，但队伍状态持久化失败。" % _get_member_display_name(member_id))
		return

	if persist_error == OK:
		_update_status("%s 完成职业晋升。" % _get_member_display_name(member_id))
	else:
		_update_status("%s 完成职业晋升，但队伍状态持久化失败。" % _get_member_display_name(member_id))
	_present_pending_mastery_reward_if_ready()


func _on_promotion_choice_cancelled() -> void:
	if _is_battle_active():
		if _pending_promotion_prompt.is_empty():
			_update_status("当前晋升选择无法取消。")
			return
		_active_modal_id = "promotion"
		_update_status("当前晋升选择必须确认后才能继续战斗。")
		return

	if _pending_world_promotion_prompt.is_empty():
		_update_status("当前晋升选择无法取消。")
		return
	_active_modal_id = "promotion"
	_update_status("当前晋升选择必须确认后才能继续结算奖励。")


func _on_mastery_reward_confirmed() -> void:
	if _active_mastery_reward == null:
		return
	var reward := _active_mastery_reward
	_active_mastery_reward = null
	_active_modal_id = ""

	var delta = _character_management.apply_pending_character_reward(reward)
	_party_state = _character_management.get_party_state()
	var persist_error := _persist_party_state()
	if delta.needs_promotion_modal:
		_pending_world_promotion_prompt = _build_promotion_prompt(delta, "确认后将在世界地图立即生效。")
		_active_modal_id = "promotion"
		if persist_error == OK:
			_update_status("%s 的角色奖励已入账，职业晋升待确认。" % reward.member_name)
		else:
			_update_status("%s 的角色奖励已入账，但队伍状态持久化失败。" % reward.member_name)
		return

	if delta.mastery_changes.is_empty() and delta.knowledge_changes.is_empty() and delta.attribute_changes.is_empty():
		if persist_error == OK:
			_update_status("%s 的本批奖励当前没有可入账项目。" % reward.member_name)
		else:
			_update_status("%s 的奖励处理完成，但队伍状态持久化失败。" % reward.member_name)
	else:
		if persist_error == OK:
			_update_status("%s 的角色奖励已结算。" % reward.member_name)
		else:
			_update_status("%s 的角色奖励已结算，但队伍状态持久化失败。" % reward.member_name)
	_present_pending_mastery_reward_if_ready()


func _apply_party_state_to_runtime(success_message: String) -> void:
	_character_management.set_party_state(_party_state)
	var persist_error := _persist_party_state()
	if persist_error == OK:
		_update_status(success_message)
	else:
		_update_status("%s 但队伍状态持久化失败。" % success_message)


func _batch_has_updates(batch) -> bool:
	if batch == null:
		return false
	return (
		batch.phase_changed
		or batch.battle_ended
		or batch.modal_requested
		or not batch.changed_unit_ids.is_empty()
		or not batch.changed_coords.is_empty()
		or not batch.log_lines.is_empty()
		or not batch.progression_deltas.is_empty()
	)


func _apply_battle_batch(batch) -> void:
	if batch == null:
		return
	_capture_pending_promotion_prompt(batch.progression_deltas)
	_refresh_battle_runtime_state()
	if not batch.log_lines.is_empty():
		_update_status(String(batch.log_lines[-1]))
	if not _pending_promotion_prompt.is_empty() and _battle_state != null and String(_battle_state.modal_state) == "promotion_choice":
		_active_modal_id = "promotion"
	if _is_battle_finished():
		_resolve_active_battle()


func _refresh_battle_runtime_state() -> void:
	_sync_selected_battle_skill_state()
	_battle_state = _get_runtime_battle_state()
	if _battle_state == null or _battle_state.is_empty():
		_battle_state = null
		_battle_selected_coord = Vector2i(-1, -1)
		return
	if _battle_selected_coord == Vector2i(-1, -1) or not _battle_state.cells.has(_battle_selected_coord):
		_battle_selected_coord = _get_default_battle_selected_coord()


func _build_battle_seed(encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT) -> int:
	var base_seed := int(_generation_config.seed) if _generation_config != null else 0
	return base_seed ^ String(encounter_anchor.entity_id).hash() ^ (_player_coord.x * 73856093) ^ (_player_coord.y * 19349663)


func _get_runtime_battle_state() -> BattleState:
	return _battle_runtime.get_state() if _battle_runtime != null else null


func _is_battle_finished() -> bool:
	var runtime_state = _get_runtime_battle_state()
	return runtime_state != null and String(runtime_state.phase) == "battle_ended"


func _get_runtime_active_unit() -> BattleUnitState:
	var runtime_state = _get_runtime_battle_state()
	if runtime_state == null or runtime_state.active_unit_id == &"":
		return null
	return runtime_state.units.get(runtime_state.active_unit_id) as BattleUnitState


func _get_manual_active_unit() -> BattleUnitState:
	var runtime_state = _get_runtime_battle_state()
	var active_unit: BattleUnitState = _get_runtime_active_unit()
	if runtime_state == null or active_unit == null:
		return null
	if String(runtime_state.phase) != "unit_acting":
		return null
	if String(runtime_state.modal_state) != "":
		return null
	if String(active_unit.control_mode) != "manual":
		return null
	return active_unit


func _get_runtime_unit_at_coord(coord: Vector2i) -> BattleUnitState:
	var runtime_state = _get_runtime_battle_state()
	if runtime_state == null:
		return null
	return _battle_grid_service.get_unit_at_coord(runtime_state, coord)


func _build_skill_command(active_unit, target_unit):
	var skill_defs: Dictionary = _game_session.get_skill_defs()
	for skill_id in active_unit.known_active_skill_ids:
		var skill_def = skill_defs.get(skill_id)
		if skill_def == null or skill_def.combat_profile == null:
			continue
		if skill_def.combat_profile.target_mode != &"unit":
			continue
		if skill_def.combat_profile.target_team_filter != &"enemy":
			continue
		if String(target_unit.faction_id) == String(active_unit.faction_id):
			continue
		if active_unit.current_ap < int(skill_def.combat_profile.ap_cost):
			continue
		if _battle_grid_service.get_distance_between_units(active_unit, target_unit) > int(skill_def.combat_profile.range_value):
			continue

		var skill_command = BATTLE_COMMAND_SCRIPT.new()
		skill_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
		skill_command.unit_id = active_unit.unit_id
		skill_command.skill_id = skill_id
		skill_command.target_unit_id = target_unit.unit_id
		skill_command.target_coord = target_unit.coord
		return skill_command
	return null


func _build_selected_skill_command(active_unit, target_unit):
	if _selected_battle_skill_id == &"":
		return null

	var skill_def = _get_selected_battle_skill_def(active_unit)
	if skill_def == null or skill_def.combat_profile == null:
		return null
	if skill_def.combat_profile.target_mode != &"unit":
		return null
	if target_unit == null or not target_unit.is_alive:
		return null
	if String(target_unit.faction_id) == String(active_unit.faction_id):
		return null
	if active_unit.current_ap < int(skill_def.combat_profile.ap_cost):
		return null
	if _battle_grid_service.get_distance_between_units(active_unit, target_unit) > int(skill_def.combat_profile.range_value):
		return null

	var skill_command = BATTLE_COMMAND_SCRIPT.new()
	skill_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	skill_command.unit_id = active_unit.unit_id
	skill_command.skill_id = _selected_battle_skill_id
	skill_command.target_unit_id = target_unit.unit_id
	skill_command.target_coord = target_unit.coord
	return skill_command


func _select_battle_skill_slot(index: int) -> void:
	var active_unit = _get_manual_active_unit()
	if active_unit == null:
		_update_status("当前没有可手动操作的单位。")
		return
	if index < 0 or index >= active_unit.known_active_skill_ids.size():
		_update_status("该技能栏当前没有技能。")
		return

	var skill_id: StringName = active_unit.known_active_skill_ids[index]
	var skill_def = _get_skill_def(skill_id)
	if skill_def == null or skill_def.combat_profile == null:
		_update_status("该技能当前不可用于战斗。")
		return

	if _selected_battle_skill_id == skill_id:
		_clear_battle_skill_selection(true)
		return

	_selected_battle_skill_id = skill_id
	_selected_battle_skill_variant_id = &""
	_queued_battle_skill_target_coords.clear()
	var unlocked_variants := _get_unlocked_cast_variants(active_unit, skill_def)
	if not unlocked_variants.is_empty():
		_selected_battle_skill_variant_id = unlocked_variants[0].variant_id
	_refresh_battle_selection_state()
	_update_status(_build_battle_skill_selection_status(skill_def, active_unit))


func _cycle_selected_battle_skill_variant(step: int) -> void:
	var active_unit = _get_manual_active_unit()
	if active_unit == null:
		_update_status("当前没有可手动操作的单位。")
		return
	if _selected_battle_skill_id == &"":
		_update_status("请先用数字键选择一个技能。")
		return

	var skill_def = _get_selected_battle_skill_def(active_unit)
	if skill_def == null or skill_def.combat_profile == null or skill_def.combat_profile.cast_variants.is_empty():
		_update_status("当前技能没有可切换的施法形态。")
		return

	var unlocked_variants := _get_unlocked_cast_variants(active_unit, skill_def)
	if unlocked_variants.is_empty():
		_update_status("当前技能等级尚未解锁任何施法形态。")
		return

	var current_index := 0
	for variant_index in range(unlocked_variants.size()):
		var cast_variant = unlocked_variants[variant_index]
		if cast_variant != null and cast_variant.variant_id == _selected_battle_skill_variant_id:
			current_index = variant_index
			break

	var next_index := posmod(current_index + step, unlocked_variants.size())
	_selected_battle_skill_variant_id = unlocked_variants[next_index].variant_id
	_queued_battle_skill_target_coords.clear()
	_refresh_battle_selection_state()
	_update_status(_build_battle_skill_selection_status(skill_def, active_unit))


func _clear_battle_skill_selection(announce: bool = false) -> void:
	_selected_battle_skill_id = &""
	_selected_battle_skill_variant_id = &""
	_queued_battle_skill_target_coords.clear()
	_last_manual_battle_unit_id = &""
	if _is_battle_active():
		_refresh_battle_selection_state()
	if announce:
		_update_status("已清除当前战斗技能选择。")


func _is_selected_ground_skill_ready(active_unit) -> bool:
	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	return cast_variant != null and cast_variant.target_mode == &"ground"


func _handle_selected_ground_skill_click(active_unit, target_coord: Vector2i) -> StringName:
	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	var skill_def = _get_selected_battle_skill_def(active_unit)
	if cast_variant == null or skill_def == null:
		_refresh_battle_selection_state()
		_update_status("当前地面技能形态不可用。")
		return &"overlay"

	var required_coord_count := maxi(int(cast_variant.required_coord_count), 1)
	var previous_targets := _queued_battle_skill_target_coords.duplicate()
	var existing_index := _queued_battle_skill_target_coords.find(target_coord)
	if existing_index >= 0:
		_queued_battle_skill_target_coords.remove_at(existing_index)
		_refresh_battle_selection_state()
		_update_status("已取消目标格 %s。" % _format_coord(target_coord))
		return &"overlay"

	if required_coord_count == 1:
		_queued_battle_skill_target_coords = [target_coord]
	else:
		if _queued_battle_skill_target_coords.size() >= required_coord_count:
			_update_status("该技能形态最多选择 %d 个地格；点击已选地格可取消。" % required_coord_count)
			return &"overlay"
		_queued_battle_skill_target_coords.append(target_coord)

	if _queued_battle_skill_target_coords.size() < required_coord_count:
		_refresh_battle_selection_state()
		_update_status("%s：已选择 %d / %d 个地格。" % [
			_build_skill_variant_display_name(skill_def, cast_variant),
			_queued_battle_skill_target_coords.size(),
			required_coord_count,
		])
		return &"overlay"

	var skill_command = BATTLE_COMMAND_SCRIPT.new()
	skill_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	skill_command.unit_id = active_unit.unit_id
	skill_command.skill_id = _selected_battle_skill_id
	skill_command.skill_variant_id = cast_variant.variant_id
	skill_command.target_coords = _queued_battle_skill_target_coords.duplicate()
	skill_command.target_coord = target_coord

	var preview = _battle_runtime.preview_command(skill_command)
	if preview != null and preview.allowed:
		return _issue_battle_command(skill_command)

	_queued_battle_skill_target_coords = previous_targets if required_coord_count > 1 else []
	_refresh_battle_selection_state()
	if preview != null and not preview.log_lines.is_empty():
		_update_status(String(preview.log_lines[-1]))
	else:
		_update_status("当前地面技能目标无效。")
	return &"overlay"


func _get_selected_battle_skill_def(active_unit):
	if active_unit == null or _selected_battle_skill_id == &"":
		return null
	if not active_unit.known_active_skill_ids.has(_selected_battle_skill_id):
		return null
	return _get_skill_def(_selected_battle_skill_id)


func _get_selected_battle_skill_variant(active_unit):
	var skill_def = _get_selected_battle_skill_def(active_unit)
	if skill_def == null or skill_def.combat_profile == null or skill_def.combat_profile.cast_variants.is_empty():
		return null
	var unlocked_variants := _get_unlocked_cast_variants(active_unit, skill_def)
	if unlocked_variants.is_empty():
		return null
	if _selected_battle_skill_variant_id == &"":
		return unlocked_variants[0]
	for cast_variant in unlocked_variants:
		if cast_variant != null and cast_variant.variant_id == _selected_battle_skill_variant_id:
			return cast_variant
	return unlocked_variants[0]


func _get_unlocked_cast_variants(active_unit, skill_def) -> Array:
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return []
	var skill_level_map: Dictionary = active_unit.known_skill_level_map
	var default_skill_level := 1 if active_unit.known_active_skill_ids.has(skill_def.skill_id) else 0
	var skill_level := int(skill_level_map.get(skill_def.skill_id, default_skill_level))
	return skill_def.combat_profile.get_unlocked_cast_variants(skill_level)


func _get_skill_def(skill_id: StringName):
	return _game_session.get_skill_defs().get(skill_id)


func _build_battle_skill_selection_status(skill_def, active_unit) -> String:
	if skill_def == null:
		return "当前技能不可用。"
	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	if cast_variant == null:
		return "已选择技能 %s。左键敌方格施放，Esc 清除选择。" % skill_def.display_name
	return "已选择 %s，需目标 %d 格。左键逐格选点，Q/E 切换形态，Esc 清除选择。" % [
		_build_skill_variant_display_name(skill_def, cast_variant),
		int(cast_variant.required_coord_count),
	]


func _build_skill_variant_display_name(skill_def, cast_variant) -> String:
	if skill_def == null:
		return "技能"
	if cast_variant == null or String(cast_variant.display_name).is_empty():
		return skill_def.display_name
	return "%s·%s" % [skill_def.display_name, String(cast_variant.display_name)]


func _get_selected_battle_skill_name() -> String:
	var active_unit: BattleUnitState = _get_manual_active_unit()
	var skill_def = _get_selected_battle_skill_def(active_unit)
	if skill_def == null:
		return ""
	return skill_def.display_name


func _get_selected_battle_skill_variant_name() -> String:
	var active_unit: BattleUnitState = _get_manual_active_unit()
	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	if cast_variant == null:
		return ""
	return String(cast_variant.display_name)


func _get_selected_battle_skill_required_coord_count() -> int:
	var active_unit: BattleUnitState = _get_manual_active_unit()
	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	if cast_variant == null:
		return 0
	return int(cast_variant.required_coord_count)


func _sync_selected_battle_skill_state() -> void:
	var active_unit = _get_manual_active_unit()
	var active_unit_id: StringName = active_unit.unit_id if active_unit != null else &""
	if active_unit_id != _last_manual_battle_unit_id:
		_selected_battle_skill_id = &""
		_selected_battle_skill_variant_id = &""
		_queued_battle_skill_target_coords.clear()
	_last_manual_battle_unit_id = active_unit_id
	if active_unit == null:
		return
	if _selected_battle_skill_id == &"":
		return
	if not active_unit.known_active_skill_ids.has(_selected_battle_skill_id):
		_selected_battle_skill_id = &""
		_selected_battle_skill_variant_id = &""
		_queued_battle_skill_target_coords.clear()
		return

	var skill_def = _get_selected_battle_skill_def(active_unit)
	if skill_def == null or skill_def.combat_profile == null:
		_selected_battle_skill_id = &""
		_selected_battle_skill_variant_id = &""
		_queued_battle_skill_target_coords.clear()
		return

	if skill_def.combat_profile.cast_variants.is_empty():
		_selected_battle_skill_variant_id = &""
		return

	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	if cast_variant == null:
		_selected_battle_skill_id = &""
		_selected_battle_skill_variant_id = &""
		_queued_battle_skill_target_coords.clear()
		return
	_selected_battle_skill_variant_id = cast_variant.variant_id


func _build_wait_command():
	var active_unit = _get_manual_active_unit()
	if active_unit == null:
		return null
	var wait_command = BATTLE_COMMAND_SCRIPT.new()
	wait_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_WAIT
	wait_command.unit_id = active_unit.unit_id
	return wait_command


func _issue_battle_command(command) -> StringName:
	if command == null:
		return &"overlay"
	if command.command_type == BATTLE_COMMAND_SCRIPT.TYPE_SKILL:
		_queued_battle_skill_target_coords.clear()
	var batch = _battle_runtime.issue_command(command)
	_apply_battle_batch(batch)
	return &"full"


func _capture_pending_promotion_prompt(progression_deltas: Array) -> void:
	for delta in progression_deltas:
		if delta == null or not delta.needs_promotion_modal:
			continue
		_pending_promotion_prompt = _build_promotion_prompt(delta)
		if not _pending_promotion_prompt.is_empty():
			return


func _build_promotion_prompt(delta, selection_hint: String = "确认后将在战斗中立即生效。") -> Dictionary:
	if delta == null or delta.pending_profession_choices.is_empty():
		return {}

	var member_state = _party_state.get_member_state(delta.member_id) if _party_state != null else null
	var member_name: String = member_state.display_name if member_state != null else str(delta.member_id)
	var profession_defs: Dictionary = _game_session.get_profession_defs()
	var choice_entries: Array[Dictionary] = []

	for pending_choice in delta.pending_profession_choices:
		if pending_choice == null:
			continue
		for profession_id in pending_choice.candidate_profession_ids:
			var profession_def = profession_defs.get(profession_id)
			var target_rank := int(pending_choice.target_rank_map.get(profession_id, 1))
			var granted_skill_ids: Array[StringName] = []
			if profession_def != null:
				for granted_skill in profession_def.get_granted_skills_for_rank(target_rank):
					if granted_skill != null and granted_skill.skill_id != &"":
						granted_skill_ids.append(granted_skill.skill_id)

			choice_entries.append({
				"profession_id": String(profession_id),
				"display_name": profession_def.display_name if profession_def != null and not profession_def.display_name.is_empty() else String(profession_id),
				"summary": "Rank %d" % target_rank,
				"description": profession_def.description if profession_def != null else "",
				"granted_skill_ids": granted_skill_ids,
				"selection_hint": selection_hint,
				"selection": {},
			})

	if choice_entries.is_empty():
		return {}
	return {
		"member_id": String(delta.member_id),
		"member_name": member_name,
		"choices": choice_entries,
	}


func _get_default_battle_selected_coord() -> Vector2i:
	var active_unit := _get_battle_active_unit()
	if active_unit != null:
		return active_unit.coord

	if _battle_state != null:
		for ally_unit_id in _battle_state.ally_unit_ids:
			var unit := _get_battle_unit_by_id(ally_unit_id)
			if unit != null:
				return unit.coord

	return Vector2i.ZERO


func _get_battle_unit_by_id(unit_id: StringName) -> BattleUnitState:
	if _battle_state == null or unit_id == &"":
		return null
	return _battle_state.units.get(unit_id) as BattleUnitState


func _get_battle_unit_at_coord(coord: Vector2i) -> BattleUnitState:
	if _battle_state == null:
		return null
	return _battle_grid_service.get_unit_at_coord(_battle_state, coord)


func _get_battle_active_unit() -> BattleUnitState:
	if _battle_state == null:
		return null
	return _get_battle_unit_by_id(_battle_state.active_unit_id)


func _get_battle_active_unit_name() -> String:
	var active_unit := _get_battle_active_unit()
	if active_unit == null:
		return "无"
	return active_unit.display_name if not active_unit.display_name.is_empty() else String(active_unit.unit_id)


func _get_battle_unit_type_label(unit_id: String) -> String:
	if _battle_state == null:
		return "战斗单位"
	for ally_unit_id in _battle_state.ally_unit_ids:
		if String(ally_unit_id) == unit_id:
			return "己方单位"
	for enemy_unit_id in _battle_state.enemy_unit_ids:
		if String(enemy_unit_id) == unit_id:
			return "敌方单位"
	return "战斗单位"


func _count_battle_terrain_types() -> Dictionary:
	var counts := {
		BATTLE_CELL_STATE_SCRIPT.TERRAIN_LAND: 0,
		BATTLE_CELL_STATE_SCRIPT.TERRAIN_FOREST: 0,
		BATTLE_CELL_STATE_SCRIPT.TERRAIN_WATER: 0,
		BATTLE_CELL_STATE_SCRIPT.TERRAIN_MUD: 0,
		BATTLE_CELL_STATE_SCRIPT.TERRAIN_SPIKE: 0,
	}
	if _battle_state == null:
		return counts
	for cell_variant in _battle_state.cells.values():
		var cell_state := cell_variant as BattleCellState
		if cell_state == null:
			continue
		var terrain_id := String(cell_state.base_terrain)
		counts[terrain_id] = int(counts.get(terrain_id, 0)) + 1
	return counts


func _format_optional_text(value: String) -> String:
	return value if not value.is_empty() else "无"


func _update_status(message: String) -> void:
	_current_status_message = message
	if status_label != null:
		status_label.text = message


func _is_modal_window_open() -> bool:
	return _active_modal_id != ""


func _enqueue_pending_mastery_rewards(reward_variants: Array) -> void:
	_character_management.enqueue_pending_character_rewards(reward_variants)
	_party_state = _character_management.get_party_state()


func _present_pending_mastery_reward_if_ready() -> bool:
	if _is_battle_active():
		return false
	if not _pending_world_promotion_prompt.is_empty():
		if _active_modal_id != "promotion":
			_active_modal_id = "promotion"
			return true
		return false
	if _active_mastery_reward != null:
		if _active_modal_id != "reward":
			_active_modal_id = "reward"
			return true
		return false
	if _active_modal_id == "settlement" or _active_modal_id == "character_info" or _active_modal_id == "party" or _active_modal_id == "warehouse":
		return false
	if _party_state == null or _party_state.pending_character_rewards.is_empty():
		return false

	_active_mastery_reward = _party_state.get_next_pending_character_reward()
	if _active_mastery_reward == null:
		return false
	_active_modal_id = "reward"
	return true


func _persist_party_state() -> int:
	var persist_error: int = int(_game_session.set_party_state(_party_state))
	_party_state = _game_session.get_party_state()
	_character_management.set_party_state(_party_state)
	_party_warehouse_service.setup(_party_state, _game_session.get_item_defs())
	_party_item_use_service.setup(
		_party_state,
		_game_session.get_item_defs(),
		_game_session.get_skill_defs(),
		_party_warehouse_service,
		_character_management
	)
	_party_equipment_service.setup(_party_state, _game_session.get_item_defs(), _party_warehouse_service)
	_refresh_fog()
	return persist_error


func _refresh_party_warehouse_window() -> void:
	if party_warehouse_window == null or not party_warehouse_window.visible:
		return
	party_warehouse_window.set_window_data(_build_party_warehouse_window_data())


func _build_party_warehouse_window_data() -> Dictionary:
	var total_capacity := _party_warehouse_service.get_total_capacity()
	var used_slots := _party_warehouse_service.get_used_slots()
	var free_slots := _party_warehouse_service.get_free_slots()
	var is_over_capacity := _party_warehouse_service.is_over_capacity()
	var stack_entries: Array[Dictionary] = []
	var target_members := _build_warehouse_target_member_entries()

	for stack in _party_warehouse_service.get_stacks():
		if stack == null or stack.is_empty():
			continue
		var item_def: ItemDef = _party_warehouse_service.get_item_def(stack.item_id) as ItemDef
		var granted_skill_id := item_def.granted_skill_id if item_def != null else &""
		stack_entries.append({
			"item_id": String(stack.item_id),
			"display_name": item_def.display_name if item_def != null and not item_def.display_name.is_empty() else String(stack.item_id),
			"description": item_def.description if item_def != null else "该物品定义缺失，当前仅保留存档中的 item_id 与数量。",
			"icon": item_def.icon if item_def != null else "",
			"quantity": int(stack.quantity),
			"is_stackable": item_def.is_stackable if item_def != null else int(stack.quantity) > 1,
			"stack_limit": item_def.get_effective_max_stack() if item_def != null else maxi(int(stack.quantity), 1),
			"total_quantity": _party_warehouse_service.count_item(stack.item_id),
			"item_category": String(item_def.item_category) if item_def != null else "",
			"is_skill_book": item_def != null and item_def.is_skill_book(),
			"granted_skill_id": String(granted_skill_id),
			"granted_skill_name": _get_skill_display_name(granted_skill_id),
		})

	var summary_text := "容量 %d 格  |  已用 %d 格  |  空余 %d 格" % [
		total_capacity,
		used_slots,
		free_slots,
	]
	var status_text := "当前版本支持查看、丢弃和让指定角色使用技能书。新增物品会先补满同类未满堆栈，再占用新格子。"
	if is_over_capacity:
		status_text = "仓库当前超容 %d 格。已存物品不会被删除，但此时不能继续新增堆栈，只能整理和移除。" % [
			used_slots - total_capacity
		]

	return {
		"title": "共享仓库",
		"meta": "入口：%s  |  规则：全队共享、按堆栈占格、不计重量。" % _active_warehouse_entry_label,
		"summary_text": summary_text,
		"status_text": status_text,
		"target_members": target_members,
		"default_target_member_id": String(_resolve_warehouse_target_member_id()),
		"stacks": stack_entries,
	}


func _get_item_display_name(item_id: StringName) -> String:
	var item_def = _party_warehouse_service.get_item_def(item_id)
	if item_def != null and not item_def.display_name.is_empty():
		return item_def.display_name
	return String(item_id)


func _get_skill_display_name(skill_id: StringName) -> String:
	var skill_def: SkillDef = null
	if _game_session != null:
		skill_def = _game_session.get_skill_defs().get(skill_id) as SkillDef
	if skill_def != null and not skill_def.display_name.is_empty():
		return skill_def.display_name
	return String(skill_id)


func _get_member_display_name(member_id: StringName) -> String:
	var member_state = _party_state.get_member_state(member_id) if _party_state != null else null
	if member_state != null and not String(member_state.display_name).is_empty():
		return String(member_state.display_name)
	return String(member_id)


func _resolve_warehouse_target_member_id(preferred_member_id: StringName = &"") -> StringName:
	if _party_state == null:
		return &""
	var normalized_member_id := ProgressionDataUtils.to_string_name(preferred_member_id)
	if normalized_member_id != &"" and _party_state.get_member_state(normalized_member_id) != null:
		return normalized_member_id
	if _party_selected_member_id != &"" and _party_state.get_member_state(_party_selected_member_id) != null:
		return _party_selected_member_id
	if _party_state.leader_member_id != &"" and _party_state.get_member_state(_party_state.leader_member_id) != null:
		return _party_state.leader_member_id
	for member_id in _party_state.active_member_ids:
		if _party_state.get_member_state(member_id) != null:
			return member_id
	for member_id in _party_state.reserve_member_ids:
		if _party_state.get_member_state(member_id) != null:
			return member_id
	return &""


func _build_warehouse_target_member_entries() -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	var seen_member_ids: Dictionary = {}
	if _party_state == null:
		return entries

	for member_id in _party_state.active_member_ids:
		if member_id == &"" or seen_member_ids.has(member_id):
			continue
		if _party_state.get_member_state(member_id) == null:
			continue
		seen_member_ids[member_id] = true
		entries.append({
			"member_id": String(member_id),
			"display_name": _get_member_display_name(member_id),
			"roster_role": "active",
		})
	for member_id in _party_state.reserve_member_ids:
		if member_id == &"" or seen_member_ids.has(member_id):
			continue
		if _party_state.get_member_state(member_id) == null:
			continue
		seen_member_ids[member_id] = true
		entries.append({
			"member_id": String(member_id),
			"display_name": _get_member_display_name(member_id),
			"roster_role": "reserve",
		})
	return entries


func _build_warehouse_use_failure_message(use_result: Dictionary) -> String:
	var item_id := ProgressionDataUtils.to_string_name(use_result.get("item_id", ""))
	var member_id := ProgressionDataUtils.to_string_name(use_result.get("member_id", ""))
	var reason := ProgressionDataUtils.to_string_name(use_result.get("reason", ""))
	var item_name := _get_item_display_name(item_id)
	var member_name := _get_member_display_name(member_id)
	match reason:
		&"missing_item_def":
			return "%s 的物品定义缺失，当前无法使用。" % item_name
		&"item_not_usable":
			return "%s 当前不是可使用的技能书。" % item_name
		&"missing_member":
			return "当前找不到可使用 %s 的目标角色。" % item_name
		&"missing_inventory":
			return "%s 当前没有可使用的库存。" % item_name
		&"missing_skill_def":
			return "%s 对应的技能定义缺失，当前无法使用。" % item_name
		&"learn_failed":
			return "%s 当前无法让 %s 学会，可能已学会或未满足前置条件。" % [item_name, member_name]
		&"consume_failed":
			return "%s 已触发学习，但库存扣减失败。" % item_name
		&"service_unavailable":
			return "当前技能书服务尚未准备完成。"
		_:
			return "%s 当前无法使用。" % item_name


func _build_equipment_error_message(result: Dictionary, is_equip_action: bool) -> String:
	var member_id := ProgressionDataUtils.to_string_name(result.get("member_id", ""))
	var slot_label := String(result.get("slot_label", "装备槽"))
	var item_id := ProgressionDataUtils.to_string_name(result.get("item_id", ""))
	match String(result.get("error_code", "")):
		"member_not_found":
			return "未找到队伍成员 %s。" % String(member_id)
		"item_not_found":
			return "未找到物品定义 %s。" % String(item_id)
		"item_not_equipment":
			return "%s 不是可装备物品。" % _get_item_display_name(item_id)
		"slot_unresolved":
			return "%s 当前没有可用装备槽。" % _get_item_display_name(item_id)
		"slot_not_allowed":
			return "%s 不能装备到 %s。" % [_get_item_display_name(item_id), slot_label]
		"warehouse_missing_item":
			return "共享仓库中没有可用于装备的 %s。" % _get_item_display_name(item_id)
		"warehouse_blocked_swap":
			return "%s 当前没有空间接回被替换下来的装备。" % slot_label
		"slot_invalid":
			return "装备槽无效。"
		"slot_empty":
			return "%s 当前没有已装备物品。" % slot_label
		"warehouse_full":
			return "共享仓库空间不足，无法卸下 %s。" % _get_item_display_name(item_id)
		"missing_profession":
			return "%s 当前职业不满足 %s 的装备要求。" % [_get_member_display_name(member_id), _get_item_display_name(item_id)]
		"body_size_too_small":
			return "%s 体型过小，无法装备 %s。" % [_get_member_display_name(member_id), _get_item_display_name(item_id)]
		"body_size_too_large":
			return "%s 体型过大，无法装备 %s。" % [_get_member_display_name(member_id), _get_item_display_name(item_id)]
		"requirement_failed":
			return "%s 不满足装备要求。" % _get_item_display_name(item_id)
		_:
			return "装备操作失败。" if is_equip_action else "卸装操作失败。"


func _format_faction_label(faction_id: String) -> String:
	match faction_id:
		"", "neutral":
			return "中立"
		"player":
			return "玩家"
		"hostile":
			return "敌对"
		_:
			return faction_id


func _get_fog_state_name(fog_state: int) -> String:
	match fog_state:
		WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_VISIBLE:
			return "当前可见"
		WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_EXPLORED:
			return "已探索"
		_:
			return "未探索"


func _is_battle_active() -> bool:
	return _battle_state != null and not _battle_state.is_empty()


func _is_adjacent_4(from_coord: Vector2i, to_coord: Vector2i) -> bool:
	return absi(from_coord.x - to_coord.x) + absi(from_coord.y - to_coord.y) == 1


func _format_coord(coord: Vector2i) -> String:
	return "(%d, %d)" % [coord.x, coord.y]


func _register_settlement_footprints() -> void:
	for settlement in _world_data.get("settlements", []):
		var entity_id: String = settlement.get("entity_id", "")
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		if entity_id.is_empty():
			continue
		if _grid_system.can_place_footprint(origin, size):
			_grid_system.register_footprint(entity_id, origin, size)


func _rebuild_world_coord_lookups() -> void:
	_settlement_by_coord.clear()
	_settlements_by_id.clear()
	_world_npc_by_coord.clear()
	_encounter_anchor_by_coord.clear()

	for settlement in _world_data.get("settlements", []):
		if settlement is not Dictionary:
			continue
		_settlements_by_id[String(settlement.get("settlement_id", ""))] = settlement
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		for y in range(size.y):
			for x in range(size.x):
				_settlement_by_coord[origin + Vector2i(x, y)] = settlement

	for npc in _world_data.get("world_npcs", []):
		if npc is not Dictionary:
			continue
		_world_npc_by_coord[npc.get("coord", Vector2i.ZERO)] = npc

	for encounter_anchor_data in _world_data.get("encounter_anchors", []):
		var encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT = encounter_anchor_data as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		_encounter_anchor_by_coord[encounter_anchor.world_coord] = encounter_anchor
