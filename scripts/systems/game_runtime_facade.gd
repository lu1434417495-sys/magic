## 文件说明：该脚本属于世界地图系统相关的系统脚本，集中维护世界地图视图、世界地图背景、战斗地图面板等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name GameRuntimeFacade
extends RefCounted

const WORLD_MAP_GRID_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_grid_system.gd")
const WORLD_MAP_FOG_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_fog_system.gd")
const WORLD_MAP_SPAWN_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_spawn_system.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle_cell_state.gd")
const BATTLE_GRID_SERVICE_SCRIPT = preload("res://scripts/systems/battle_grid_service.gd")
const CHARACTER_MANAGEMENT_MODULE_SCRIPT = preload("res://scripts/systems/character_management_module.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle_runtime_module.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle_command.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")
const ENCOUNTER_ROSTER_BUILDER_SCRIPT = preload("res://scripts/systems/encounter_roster_builder.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle_resolution_result.gd")
const PARTY_WAREHOUSE_SERVICE_SCRIPT = preload("res://scripts/systems/party_warehouse_service.gd")
const PARTY_ITEM_USE_SERVICE_SCRIPT = preload("res://scripts/systems/party_item_use_service.gd")
const PARTY_EQUIPMENT_SERVICE_SCRIPT = preload("res://scripts/systems/party_equipment_service.gd")
const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/pending_character_reward.gd")
const WORLD_TIME_SYSTEM_SCRIPT = preload("res://scripts/systems/world_time_system.gd")
const WILD_ENCOUNTER_GROWTH_SYSTEM_SCRIPT = preload("res://scripts/systems/wild_encounter_growth_system.gd")
const BATTLE_SESSION_FACADE_SCRIPT = preload("res://scripts/systems/battle_session_facade.gd")
const GAME_RUNTIME_BATTLE_SELECTION_SCRIPT = preload("res://scripts/systems/game_runtime_battle_selection.gd")
const GAME_RUNTIME_BATTLE_SELECTION_STATE_SCRIPT = preload("res://scripts/systems/game_runtime_battle_selection_state.gd")
const GAME_RUNTIME_SETTLEMENT_COMMAND_HANDLER_SCRIPT = preload("res://scripts/systems/game_runtime_settlement_command_handler.gd")
const GAME_RUNTIME_WAREHOUSE_HANDLER_SCRIPT = preload("res://scripts/systems/game_runtime_warehouse_handler.gd")
const GAME_RUNTIME_PARTY_COMMAND_HANDLER_SCRIPT = preload("res://scripts/systems/game_runtime_party_command_handler.gd")
const GAME_RUNTIME_REWARD_FLOW_HANDLER_SCRIPT = preload("res://scripts/systems/game_runtime_reward_flow_handler.gd")
const GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT = preload("res://scripts/systems/game_runtime_snapshot_builder.gd")
const SETTLEMENT_SHOP_SERVICE_SCRIPT = preload("res://scripts/systems/settlement_shop_service.gd")
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
var shop_window = null
var stagecoach_window = null
var promotion_choice_window = null
var character_reward_window = null

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
## 字段说明：缓存当前激活地图数据字典，集中保存可按键查询的运行时数据。
var _active_world_data: Dictionary = {}
## 字段说明：记录当前激活地图唯一标识，供世界切换、快照和返回链复用。
var _active_map_id := ""
## 字段说明：记录当前激活地图名称，供状态文案、提示和调试观察使用。
var _active_map_display_name := ""
## 字段说明：记录当前激活地图的生成配置，会参与运行时状态流转、系统协作和存档恢复。
var _active_generation_config = null
## 字段说明：按键缓存当前地图中的世界事件查找表，便于在较多对象中快速定位目标并减少重复遍历。
var _world_event_by_coord: Dictionary = {}
## 字段说明：缓存待确认的子地图进入提示字典，集中保存可按键查询的运行时数据。
var _pending_submap_prompt: Dictionary = {}
## 字段说明：缓存待确认的战斗开始提示字典，集中保存可按键查询的运行时数据。
var _pending_battle_start_prompt: Dictionary = {}
## 字段说明：缓存子地图生成配置集合字典，供切换地图时按标识复用加载结果。
var _submap_generation_configs: Dictionary = {}
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
var _snapshot_builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
var _battle_session_facade = BATTLE_SESSION_FACADE_SCRIPT.new()
var _battle_selection = GAME_RUNTIME_BATTLE_SELECTION_SCRIPT.new()
var _battle_selection_state = GAME_RUNTIME_BATTLE_SELECTION_STATE_SCRIPT.new()
var _settlement_command_handler = GAME_RUNTIME_SETTLEMENT_COMMAND_HANDLER_SCRIPT.new()
var _warehouse_handler = GAME_RUNTIME_WAREHOUSE_HANDLER_SCRIPT.new()
var _party_command_handler = GAME_RUNTIME_PARTY_COMMAND_HANDLER_SCRIPT.new()
var _reward_flow_handler = GAME_RUNTIME_REWARD_FLOW_HANDLER_SCRIPT.new()
## 字段说明：记录战斗选中坐标，用于定位对象、绘制内容或执行网格计算。
var _battle_selected_coord: Vector2i = Vector2i(-1, -1):
	get:
		return _battle_selection_state.battle_selected_coord
	set(value):
		_battle_selection_state.battle_selected_coord = value
## 字段说明：记录当前激活的战斗遭遇唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _active_battle_encounter_id: StringName = &""
## 字段说明：记录当前激活的战斗遭遇名称，会参与运行时状态流转、系统协作和存档恢复。
var _active_battle_encounter_name := ""
## 字段说明：缓存待处理的晋升提示字典，集中保存可按键查询的运行时数据。
var _pending_promotion_prompt: Dictionary = {}
## 字段说明：记录当前选中的战斗技能唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _selected_battle_skill_id: StringName = &"":
	get:
		return _battle_selection_state.selected_skill_id
	set(value):
		_battle_selection_state.selected_skill_id = value
## 字段说明：记录当前选中的战斗技能变体唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _selected_battle_skill_variant_id: StringName = &"":
	get:
		return _battle_selection_state.selected_skill_variant_id
	set(value):
		_battle_selection_state.selected_skill_variant_id = value
## 字段说明：缓存已排队的战斗技能目标坐标集合，供技能确认和结算前的预览流程复用。
var _queued_battle_skill_target_coords: Array[Vector2i] = []:
	get:
		return _battle_selection_state.queued_target_coords
	set(value):
		_battle_selection_state.queued_target_coords = value
## 字段说明：缓存已排队的战斗技能目标单位集合，供单位多选、状态提示和稳定快照复用。
var _queued_battle_skill_target_unit_ids: Array[StringName] = []:
	get:
		return _battle_selection_state.queued_target_unit_ids
	set(value):
		_battle_selection_state.queued_target_unit_ids = value
## 字段说明：记录上一次手动操作的战斗单位唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _last_manual_battle_unit_id: StringName = &"":
	get:
		return _battle_selection_state.last_manual_unit_id
	set(value):
		_battle_selection_state.last_manual_unit_id = value
var _held_world_move_keys: Array[int] = []
var _world_move_repeat_timer := 0.0
## 字段说明：缓存当前激活的角色奖励实例，会参与运行时状态流转、系统协作和存档恢复。
var _active_reward: PendingCharacterReward = null
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
## 字段说明：缓存当前任务板窗口上下文，供 UI 与 headless 统一读取。
var _active_contract_board_context: Dictionary = {}
## 字段说明：缓存当前商店窗口上下文，供 UI 与 headless 统一读取。
var _active_shop_context: Dictionary = {}
## 字段说明：缓存当前重铸窗口上下文，供 UI 与 headless 统一读取。
var _active_forge_context: Dictionary = {}
## 字段说明：缓存当前驿站窗口上下文，供 UI 与 headless 统一读取。
var _active_stagecoach_context: Dictionary = {}
## 字段说明：缓存最近一次状态文本，供 headless 文本测试直接读取。
var _current_status_message := ""
var _active_command_log_scope: Dictionary = {}
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
	_world_data = _game_session.get_world_data()
	_wild_encounter_rosters = _game_session.get_wild_encounter_rosters()
	_encounter_roster_builder.setup(_wild_encounter_rosters)
	_party_state = _game_session.get_party_state()
	_player_coord = _game_session.get_player_coord()
	_player_faction_id = _game_session.get_player_faction_id()
	_character_management.setup(
		_party_state,
		_game_session.get_skill_defs(),
		_game_session.get_profession_defs(),
		_game_session.get_achievement_defs(),
		_game_session.get_item_defs(),
		_game_session.get_quest_defs()
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
	_snapshot_builder.setup(self)
	_battle_session_facade.setup(self)
	_battle_selection_state.reset_for_battle_end()
	_battle_selection.setup(self)
	_settlement_command_handler.setup(self)
	_warehouse_handler.setup(self)
	_party_command_handler.setup(self)
	_reward_flow_handler.setup(self)
	_sync_active_world_context()
	_selected_coord = _player_coord
	_refresh_fog()
	_active_modal_id = ""
	_active_settlement_id = ""
	_active_settlement_feedback_text = ""
	_active_contract_board_context.clear()
	_active_shop_context.clear()
	_active_forge_context.clear()
	_active_stagecoach_context.clear()
	_active_character_info_context.clear()
	_party_selected_member_id = &""
	_active_warehouse_entry_label = ""
	_pending_submap_prompt.clear()
	_pending_battle_start_prompt.clear()
	if is_submap_active():
		_update_status("已载入 %s。%s" % [get_active_map_display_name(), get_submap_return_hint_text()])
		return
	var start_settlement_name: String = _active_world_data.get("player_start_settlement_name", "")
	if start_settlement_name.is_empty():
		_update_status("大地图已载入。方向键/WASD 可按住持续移动，点击可见据点或按 Enter 打开据点窗口，按 P 打开队伍管理，右键人物可查看信息。")
	else:
		_update_status("大地图已载入，初始村庄为 %s。方向键/WASD 可按住持续移动，点击可见据点或按 Enter 打开据点窗口，按 P 打开队伍管理，右键人物可查看信息。" % start_settlement_name)


func dispose() -> void:
	if _battle_runtime != null:
		_battle_runtime.dispose()
	if _snapshot_builder != null and _snapshot_builder.has_method("dispose"):
		_snapshot_builder.dispose()
	if _battle_session_facade != null and _battle_session_facade.has_method("dispose"):
		_battle_session_facade.dispose()
	if _battle_selection != null and _battle_selection.has_method("dispose"):
		_battle_selection.dispose()
	if _settlement_command_handler != null and _settlement_command_handler.has_method("dispose"):
		_settlement_command_handler.dispose()
	if _warehouse_handler != null and _warehouse_handler.has_method("dispose"):
		_warehouse_handler.dispose()
	if _party_command_handler != null and _party_command_handler.has_method("dispose"):
		_party_command_handler.dispose()
	if _reward_flow_handler != null and _reward_flow_handler.has_method("dispose"):
		_reward_flow_handler.dispose()

	_game_session = null
	_generation_config = null
	_world_data.clear()
	_active_world_data.clear()
	_active_map_id = ""
	_active_map_display_name = ""
	_active_generation_config = null
	_world_event_by_coord.clear()
	_pending_submap_prompt.clear()
	_pending_battle_start_prompt.clear()
	_submap_generation_configs.clear()
	_settlement_by_coord.clear()
	_world_npc_by_coord.clear()
	_encounter_anchor_by_coord.clear()
	_settlements_by_id.clear()
	_wild_encounter_rosters.clear()
	_party_state = null
	_battle_state = null
	_pending_promotion_prompt.clear()
	_pending_world_promotion_prompt.clear()
	_active_character_info_context.clear()
	_active_contract_board_context.clear()
	_active_shop_context.clear()
	_active_forge_context.clear()
	_active_stagecoach_context.clear()
	_battle_selection_state.reset_for_battle_end()
	_held_world_move_keys.clear()
	_active_reward = null

	world_map_view = null
	world_map_background = null
	battle_map_panel = null
	status_label = null
	settlement_window = null
	character_info_window = null
	party_management_window = null
	party_warehouse_window = null
	shop_window = null
	stagecoach_window = null
	promotion_choice_window = null
	character_reward_window = null


func get_status_text() -> String:
	return _current_status_message


func get_log_snapshot(limit: int = 30) -> Dictionary:
	return _game_session.get_log_snapshot(limit) if _game_session != null else {}


func get_recent_logs(limit: int = 30) -> Array[Dictionary]:
	return _game_session.get_recent_logs(limit) if _game_session != null else []


func get_active_log_file_path() -> String:
	return _game_session.get_active_log_file_path() if _game_session != null else ""


func get_active_modal_id() -> String:
	return _active_modal_id


func get_active_settlement_id() -> String:
	return _active_settlement_id


func get_active_map_id() -> String:
	return _active_map_id


func get_active_map_display_name() -> String:
	return _active_map_display_name


func get_submap_return_hint_text() -> String:
	if not is_submap_active():
		return ""
	var submap_entry := _get_mounted_submap_entry(_active_map_id)
	return String(submap_entry.get("return_hint_text", "点击任意地点返回原位置。"))


func get_pending_submap_prompt() -> Dictionary:
	return _pending_submap_prompt.duplicate(true)


func get_pending_battle_start_prompt() -> Dictionary:
	return _pending_battle_start_prompt.duplicate(true)


func is_submap_active() -> bool:
	return not _active_map_id.is_empty()


func get_world_step() -> int:
	return int(_active_world_data.get("world_step", 0))


func get_selected_settlement() -> Dictionary:
	var settlement: Dictionary = _get_settlement_at(_selected_coord)
	return settlement.duplicate(true) if not settlement.is_empty() else {}


func get_selected_world_npc() -> Dictionary:
	var npc: Dictionary = _get_world_npc_at(_selected_coord)
	return npc.duplicate(true) if not npc.is_empty() else {}


func get_selected_encounter_anchor():
	return _get_encounter_anchor_at(_selected_coord)


func get_selected_world_event() -> Dictionary:
	var world_event := _get_world_event_at(_selected_coord)
	return world_event.duplicate(true) if not world_event.is_empty() else {}


func get_nearby_encounter_entries(limit: int = 8) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	var max_entries := maxi(limit, 0)
	if max_entries <= 0:
		return entries
	for encounter_variant in _active_world_data.get("encounter_anchors", []):
		var encounter = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter == null or encounter.is_cleared:
			continue
		var delta: Vector2i = encounter.world_coord - _player_coord
		entries.append({
			"entity_id": String(encounter.entity_id),
			"display_name": String(encounter.display_name),
			"coord": {
				"x": encounter.world_coord.x,
				"y": encounter.world_coord.y,
			},
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
	if entries.size() > max_entries:
		entries.resize(max_entries)
	return entries


func get_nearby_world_event_entries(limit: int = 8) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	var max_entries := maxi(limit, 0)
	if max_entries <= 0:
		return entries
	for world_event_variant in _active_world_data.get("world_events", []):
		if world_event_variant is not Dictionary:
			continue
		var world_event: Dictionary = world_event_variant
		if not bool(world_event.get("is_discovered", false)):
			continue
		var event_coord: Vector2i = world_event.get("world_coord", Vector2i.ZERO)
		var delta: Vector2i = event_coord - _player_coord
		entries.append({
			"event_id": String(world_event.get("event_id", "")),
			"display_name": String(world_event.get("display_name", "")),
			"coord": {
				"x": event_coord.x,
				"y": event_coord.y,
			},
			"distance": absi(delta.x) + absi(delta.y),
			"event_type": String(world_event.get("event_type", "")),
			"target_submap_id": String(world_event.get("target_submap_id", "")),
		})
	entries.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		var distance_a := int(a.get("distance", 0))
		var distance_b := int(b.get("distance", 0))
		if distance_a == distance_b:
			return String(a.get("event_id", "")) < String(b.get("event_id", ""))
		return distance_a < distance_b
	)
	if entries.size() > max_entries:
		entries.resize(max_entries)
	return entries


func get_resolved_settlement_id() -> String:
	return _resolve_command_settlement_id()


func get_grid_system():
	return _grid_system


func get_fog_system():
	return _fog_system


func get_world_data() -> Dictionary:
	return _active_world_data


func get_generation_config():
	return _active_generation_config


func get_player_coord() -> Vector2i:
	return _player_coord


func get_selected_coord() -> Vector2i:
	return _selected_coord


func get_player_faction_id() -> String:
	return _player_faction_id


func get_party_state():
	return _party_state


func get_active_quest_states() -> Array:
	return _character_management.get_active_quest_states() if _character_management != null else []


func get_claimable_quest_states() -> Array:
	return _character_management.get_claimable_quest_states() if _character_management != null else []


func get_claimable_quest_ids() -> Array[StringName]:
	return _character_management.get_claimable_quest_ids() if _character_management != null else []


func get_completed_quest_ids() -> Array[StringName]:
	return _character_management.get_completed_quest_ids() if _character_management != null else []


func get_member_achievement_summary(member_id: StringName) -> Dictionary:
	return _character_management.get_member_achievement_summary(member_id) if _character_management != null else {}


func get_member_attribute_snapshot(member_id: StringName):
	return _character_management.get_member_attribute_snapshot(member_id) if _character_management != null else null


func get_member_equipped_entries(member_id: StringName) -> Array[Dictionary]:
	return _party_equipment_service.get_equipped_entries(member_id) if _party_equipment_service != null else []


func get_member_display_name(member_id: StringName) -> String:
	return _get_member_display_name(member_id)


func get_party_selected_member_id() -> StringName:
	return _party_selected_member_id


func set_party_selected_member_id(member_id: StringName) -> void:
	_party_selected_member_id = member_id


func get_item_display_name(item_id: StringName) -> String:
	return _get_item_display_name(item_id)


func get_settlement_window_data(settlement_id: String = "") -> Dictionary:
	return _settlement_command_handler.get_settlement_window_data(settlement_id)


func get_settlement_feedback_text() -> String:
	return _active_settlement_feedback_text


func set_active_settlement_id(settlement_id: String) -> void:
	_active_settlement_id = settlement_id


func set_settlement_feedback_text(feedback_text: String) -> void:
	_active_settlement_feedback_text = feedback_text


func get_settlement_record(settlement_id: String) -> Dictionary:
	return _settlements_by_id.get(settlement_id, {}).duplicate(true)


func get_all_settlement_records() -> Array[Dictionary]:
	var settlements: Array[Dictionary] = []
	for settlement_variant in _active_world_data.get("settlements", []):
		if settlement_variant is not Dictionary:
			continue
		settlements.append((settlement_variant as Dictionary).duplicate(true))
	return settlements


func get_character_info_context() -> Dictionary:
	return _active_character_info_context.duplicate(true)


func get_active_warehouse_entry_label() -> String:
	return _active_warehouse_entry_label


func set_active_warehouse_entry_label(entry_label: String) -> void:
	_active_warehouse_entry_label = entry_label


func get_shop_window_data() -> Dictionary:
	return _settlement_command_handler.get_shop_window_data()


func get_contract_board_window_data() -> Dictionary:
	return _settlement_command_handler.get_contract_board_window_data()


func get_forge_window_data() -> Dictionary:
	return _settlement_command_handler.get_forge_window_data()


func set_active_contract_board_context(context: Dictionary) -> void:
	_active_contract_board_context = context.duplicate(true)


func set_active_shop_context(context: Dictionary) -> void:
	_active_shop_context = context.duplicate(true)


func set_active_forge_context(context: Dictionary) -> void:
	_active_forge_context = context.duplicate(true)


func clear_active_contract_board_context() -> void:
	_active_contract_board_context.clear()


func clear_active_shop_context() -> void:
	_active_shop_context.clear()


func clear_active_forge_context() -> void:
	_active_forge_context.clear()


func get_active_contract_board_context() -> Dictionary:
	return _active_contract_board_context.duplicate(true)


func get_active_shop_context() -> Dictionary:
	return _active_shop_context.duplicate(true)


func get_active_forge_context() -> Dictionary:
	return _active_forge_context.duplicate(true)


func get_stagecoach_window_data() -> Dictionary:
	return _settlement_command_handler.get_stagecoach_window_data()


func set_active_stagecoach_context(context: Dictionary) -> void:
	_active_stagecoach_context = context.duplicate(true)


func clear_active_stagecoach_context() -> void:
	_active_stagecoach_context.clear()


func get_active_stagecoach_context() -> Dictionary:
	return _active_stagecoach_context.duplicate(true)


func get_warehouse_window_data() -> Dictionary:
	return _warehouse_handler.get_warehouse_window_data() if _party_state != null else {}


func get_battle_state() -> BattleState:
	return _battle_state


func get_battle_runtime():
	return _battle_runtime


func get_battle_grid_service():
	return _battle_grid_service


func get_battle_selection():
	return _battle_selection


func get_game_session():
	return _game_session


func get_settlement_shop_service():
	return SETTLEMENT_SHOP_SERVICE_SCRIPT.new()


func get_character_management():
	return _character_management


func get_party_warehouse_service():
	return _party_warehouse_service


func get_party_item_use_service():
	return _party_item_use_service


func get_active_battle_encounter_id() -> StringName:
	return _active_battle_encounter_id


func get_active_battle_encounter_name() -> String:
	return _active_battle_encounter_name


func get_battle_selected_coord() -> Vector2i:
	return _battle_selected_coord


func get_selected_battle_skill_id() -> StringName:
	return _selected_battle_skill_id


func get_selected_battle_skill_variant_id() -> StringName:
	return _selected_battle_skill_variant_id


func set_battle_selection_skill_id(skill_id: StringName) -> void:
	_selected_battle_skill_id = skill_id


func set_battle_selection_skill_variant_id(variant_id: StringName) -> void:
	_selected_battle_skill_variant_id = variant_id


func get_battle_selection_last_manual_unit_id() -> StringName:
	return _last_manual_battle_unit_id


func set_battle_selection_last_manual_unit_id(unit_id: StringName) -> void:
	_last_manual_battle_unit_id = unit_id


func get_battle_selection_target_coords_state() -> Array[Vector2i]:
	return _queued_battle_skill_target_coords


func set_battle_selection_target_coords_state(target_coords: Array[Vector2i]) -> void:
	_queued_battle_skill_target_coords = target_coords


func get_battle_selection_target_unit_ids_state() -> Array[StringName]:
	return _queued_battle_skill_target_unit_ids


func set_battle_selection_target_unit_ids_state(target_unit_ids: Array[StringName]) -> void:
	_queued_battle_skill_target_unit_ids = target_unit_ids


func get_manual_battle_unit() -> BattleUnitState:
	return _get_manual_active_unit()


func get_runtime_battle_active_unit() -> BattleUnitState:
	return _get_runtime_active_unit()


func get_runtime_battle_unit_at_coord(coord: Vector2i) -> BattleUnitState:
	return _get_runtime_unit_at_coord(coord)


func get_runtime_battle_unit_by_id(unit_id: StringName) -> BattleUnitState:
	return _get_battle_unit_by_id(unit_id)


func preview_battle_command(command):
	return _battle_runtime.preview_command(command) if _battle_runtime != null else null


func issue_battle_command(command) -> StringName:
	return _issue_battle_command(command)


func refresh_battle_selection_state() -> void:
	_refresh_battle_selection_state()


func build_command_ok(message: String = "", battle_refresh_mode: String = "") -> Dictionary:
	return _command_ok(message, battle_refresh_mode)


func build_command_error(message: String) -> Dictionary:
	return _command_error(message)


func batch_has_updates(batch) -> bool:
	return _batch_has_updates(batch)


func try_open_character_info_at_battle_coord(coord: Vector2i) -> bool:
	return _try_open_character_info_at_battle_coord(coord)


func update_status(message: String) -> void:
	_update_status(message)


func close_settlement_modal() -> void:
	_settlement_command_handler.on_settlement_window_closed()


func close_contract_board_modal() -> void:
	_settlement_command_handler.on_contract_board_window_closed()


func close_shop_modal() -> void:
	_settlement_command_handler.on_shop_window_closed()


func close_forge_modal() -> void:
	_settlement_command_handler.on_forge_window_closed()


func close_stagecoach_modal() -> void:
	_settlement_command_handler.on_stagecoach_window_closed()


func format_coord(coord: Vector2i) -> String:
	return _format_coord(coord)


func get_skill_defs() -> Dictionary:
	return _game_session.get_skill_defs() if _game_session != null else {}


func get_selected_battle_skill_name() -> String:
	return _battle_session_facade.get_selected_battle_skill_name()


func get_selected_battle_skill_variant_name() -> String:
	return _battle_session_facade.get_selected_battle_skill_variant_name()


func get_selected_battle_skill_target_coords() -> Array[Vector2i]:
	return _battle_session_facade.get_selected_battle_skill_target_coords()


func get_selected_battle_skill_target_unit_ids() -> Array[StringName]:
	return _battle_session_facade.get_selected_battle_skill_target_unit_ids()


func get_selected_battle_skill_valid_target_coords() -> Array[Vector2i]:
	return _battle_session_facade.get_selected_battle_skill_valid_target_coords()


func get_battle_movement_reachable_coords() -> Array[Vector2i]:
	return _battle_session_facade.get_battle_movement_reachable_coords()


func get_battle_overlay_target_coords() -> Array[Vector2i]:
	return _battle_session_facade.get_battle_overlay_target_coords()


func get_selected_battle_skill_required_coord_count() -> int:
	return _battle_session_facade.get_selected_battle_skill_required_coord_count()


func get_battle_active_unit_name() -> String:
	return _battle_session_facade.get_battle_active_unit_name()


func get_battle_terrain_counts() -> Dictionary:
	return _battle_session_facade.get_battle_terrain_counts()


func get_active_reward():
	return _active_reward


func get_snapshot_reward():
	if _active_reward != null:
		return _active_reward
	return _party_state.get_next_pending_character_reward() if _party_state != null else null


func get_pending_reward_count() -> int:
	return _party_state.pending_character_rewards.size() if _party_state != null else 0


func get_current_promotion_prompt() -> Dictionary:
	return _reward_flow_handler.get_current_promotion_prompt() if _reward_flow_handler != null else {}


func get_pending_promotion_prompt() -> Dictionary:
	return _pending_promotion_prompt


func get_pending_world_promotion_prompt_state() -> Dictionary:
	return _pending_world_promotion_prompt


func get_active_reward_state():
	return _active_reward


func is_battle_active() -> bool:
	return _is_battle_active()


func is_modal_window_open() -> bool:
	return _is_modal_window_open()


func set_runtime_battle_state(state: BattleState) -> void:
	_battle_state = state


func set_runtime_battle_selected_coord(coord: Vector2i) -> void:
	_battle_selected_coord = coord


func set_runtime_active_modal_id(modal_id: String) -> void:
	_active_modal_id = modal_id


func set_pending_promotion_prompt(prompt: Dictionary) -> void:
	_pending_promotion_prompt = prompt.duplicate(true)


func clear_pending_promotion_prompt() -> void:
	_pending_promotion_prompt.clear()


func set_pending_world_promotion_prompt_state(prompt: Dictionary) -> void:
	_pending_world_promotion_prompt = prompt.duplicate(true)


func clear_pending_world_promotion_prompt_state() -> void:
	_pending_world_promotion_prompt.clear()


func set_active_reward_state(reward) -> void:
	_active_reward = reward


func clear_active_reward_state() -> void:
	_active_reward = null


func clear_active_character_info_context() -> void:
	_active_character_info_context.clear()


func clear_battle_selection_targets() -> void:
	_battle_selection_state.clear_targets()


func close_party_management_modal() -> void:
	if _party_command_handler != null:
		_party_command_handler._on_party_management_window_closed()


func close_party_warehouse_modal() -> void:
	if _warehouse_handler != null:
		_warehouse_handler.on_party_warehouse_window_closed()


func open_party_warehouse_window(entry_label: String) -> void:
	if _warehouse_handler != null:
		_warehouse_handler.open_party_warehouse_window(entry_label)


func submit_battle_promotion_choice(member_id: StringName, profession_id: StringName, selection: Dictionary):
	return _battle_runtime.submit_promotion_choice(member_id, profession_id, selection) if _battle_runtime != null else null


func apply_battle_batch(batch) -> void:
	_apply_battle_batch(batch)


func promote_profession(member_id: StringName, profession_id: StringName, selection: Dictionary):
	return _character_management.promote_profession(member_id, profession_id, selection) if _character_management != null else null


func apply_pending_character_reward_to_party(reward):
	return _character_management.apply_pending_character_reward(reward) if _character_management != null else null


func enqueue_character_rewards(reward_variants: Array) -> void:
	if _character_management == null:
		return
	_character_management.enqueue_pending_character_rewards(reward_variants)
	_party_state = _character_management.get_party_state()


func apply_quest_progress_events_to_party(event_variants: Array, source_domain: String = "quest") -> Dictionary:
	if _character_management == null:
		return {
			"accepted_quest_ids": [],
			"progressed_quest_ids": [],
			"claimable_quest_ids": [],
			"completed_quest_ids": [],
		}
	var summary := _character_management.apply_quest_progress_events(event_variants, get_world_step())
	_party_state = _character_management.get_party_state()
	if _has_quest_progress_summary_changes(summary):
		_log_runtime_event("info", source_domain, "%s.quest_progress" % source_domain, _format_quest_progress_summary(summary), {
			"runtime": _build_runtime_log_state(),
			"quest_progress_summary": _quest_progress_summary_to_string_dict(summary),
		})
	return summary


func sync_party_state_from_character_management() -> void:
	if _character_management != null:
		_party_state = _character_management.get_party_state()


func persist_party_state() -> int:
	return _persist_party_state()


func build_runtime_promotion_prompt(delta, selection_hint: String = "确认后将在战斗中立即生效。") -> Dictionary:
	return _build_promotion_prompt(delta, selection_hint)


func equip_party_item(member_id: StringName, item_id: StringName, slot_id: StringName) -> Dictionary:
	return _party_equipment_service.equip_item(member_id, item_id, slot_id) if _party_equipment_service != null else {}


func unequip_party_item(member_id: StringName, slot_id: StringName) -> Dictionary:
	return _party_equipment_service.unequip_item(member_id, slot_id) if _party_equipment_service != null else {}


func present_pending_reward_if_ready() -> bool:
	return _present_pending_reward_if_ready()


func sync_character_management_party_state() -> void:
	if _character_management != null:
		_character_management.set_party_state(_party_state)


func enqueue_pending_character_rewards(reward_variants: Array) -> void:
	_enqueue_pending_character_rewards(reward_variants)


func record_member_achievement_event(
	member_id: StringName,
	event_id: StringName,
	value: int,
	detail_id: StringName = &""
) -> void:
	if _character_management != null:
		_character_management.record_achievement_event(member_id, event_id, value, detail_id)


func prepare_battle_start(encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT) -> void:
	if encounter_anchor == null:
		return
	_active_battle_encounter_id = encounter_anchor.entity_id
	_active_battle_encounter_name = encounter_anchor.display_name
	_pending_battle_start_prompt.clear()
	_pending_promotion_prompt.clear()
	_battle_selection.clear_battle_skill_selection()
	_character_management.set_party_state(_party_state)


func handle_battle_start_failure() -> void:
	var failed_encounter_id := String(_active_battle_encounter_id)
	var failed_encounter_name := _active_battle_encounter_name
	_active_battle_encounter_id = &""
	_active_battle_encounter_name = ""
	_pending_battle_start_prompt.clear()
	_active_modal_id = ""
	_battle_state = null
	_battle_selected_coord = Vector2i(-1, -1)
	_update_status("遭遇战生成失败。")
	_log_runtime_event("error", "battle", "battle.start_failed", "遭遇战生成失败。", {
		"encounter_id": failed_encounter_id,
		"encounter_name": failed_encounter_name,
		"runtime": _build_runtime_log_state(),
	})


func present_battle_start_confirmation() -> void:
	if not _is_battle_active() or _battle_state == null:
		return
	_pending_battle_start_prompt = {
		"title": "开始战斗",
		"description": "是否开始战斗？确认后 TU 将按每秒 5 点推进。",
		"confirm_text": "开始战斗",
		"cancel_visible": false,
		"dismiss_on_shade": false,
	}
	_active_modal_id = "battle_start_confirm"
	_battle_state.modal_state = &"start_confirm"
	if _battle_state.timeline != null:
		_battle_state.timeline.frozen = true
		_battle_state.timeline.tick_interval_seconds = 1.0
		_battle_state.timeline.tu_per_tick = 5
		_battle_state.timeline.delta_remainder = 0.0
	_update_status("战斗地图已载入，请确认开始战斗。")
	_log_runtime_event("info", "battle", "battle.start_prepared", "战斗地图已载入，请确认开始战斗。", {
		"runtime": _build_runtime_log_state(),
	})


func finalize_battle_resolution(battle_resolution_result) -> void:
	if battle_resolution_result == null:
		return
	var winner_faction_id := String(battle_resolution_result.winner_faction_id)
	var resolved_pending_rewards: Array = battle_resolution_result.get_pending_character_rewards_copy()
	var resolved_quest_progress_events: Array = battle_resolution_result.quest_progress_events.duplicate(true)
	var battle_summary := _build_battle_log_state()
	_battle_runtime.end_battle({"commit_progression": true})
	_character_management.enqueue_pending_character_rewards(resolved_pending_rewards)
	var merged_quest_progress_events: Array = resolved_quest_progress_events
	merged_quest_progress_events.append_array(_build_default_battle_quest_progress_events(winner_faction_id))
	var quest_summary := _character_management.apply_quest_progress_events(merged_quest_progress_events, get_world_step())
	_party_state = _character_management.get_party_state()
	var party_persist_error: int = int(_game_session.set_party_state(_party_state))
	_resolve_world_encounter_after_battle(winner_faction_id)
	var world_persist_error: int = int(_game_session.set_world_data(_world_data))
	_game_session.set_battle_save_lock(false)
	var flush_error: int = int(_game_session.flush_game_state())

	_active_modal_id = ""
	_pending_battle_start_prompt.clear()
	_pending_promotion_prompt.clear()
	_battle_selection.clear_battle_skill_selection()
	_battle_state = null
	_battle_selected_coord = Vector2i(-1, -1)
	var battle_name: String = _active_battle_encounter_name if not _active_battle_encounter_name.is_empty() else "遭遇"
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
	_log_runtime_event(
		"info" if party_persist_error == OK and world_persist_error == OK and flush_error == OK else "warn",
		"battle",
		"battle.resolved",
		_current_status_message,
		{
			"battle": battle_summary,
			"winner_faction_id": winner_faction_id,
			"pending_reward_count": resolved_pending_rewards.size(),
			"loot_entry_count": battle_resolution_result.loot_entries.size(),
			"overflow_entry_count": battle_resolution_result.overflow_entries.size(),
			"quest_progress_summary": _quest_progress_summary_to_string_dict(quest_summary),
			"party_persist_error": party_persist_error,
			"world_persist_error": world_persist_error,
			"flush_error": flush_error,
		}
	)
	_present_pending_reward_if_ready()


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
	return _present_pending_reward_if_ready()


func build_headless_snapshot() -> Dictionary:
	return _snapshot_builder.build_headless_snapshot()


func build_text_snapshot() -> String:
	return _snapshot_builder.build_text_snapshot()


func advance_world_time_by_steps(delta_steps: int) -> void:
	_advance_world_time_by_steps(delta_steps)


func refresh_world_visibility() -> void:
	_refresh_world_event_discovery()
	_refresh_fog()


func persist_world_data() -> int:
	return _persist_world_data()


func persist_player_coord() -> int:
	return int(_game_session.set_player_coord(_player_coord)) if _game_session != null else ERR_UNAVAILABLE


func set_player_coord(coord: Vector2i) -> void:
	_player_coord = coord


func set_selected_coord(coord: Vector2i) -> void:
	_selected_coord = coord


func set_active_settlement_state(settlement_id: String, settlement_state: Dictionary) -> bool:
	var settlements_variant = _active_world_data.get("settlements", [])
	if settlements_variant is not Array:
		return false
	for index in range(settlements_variant.size()):
		var settlement_variant = settlements_variant[index]
		if settlement_variant is not Dictionary:
			continue
		var settlement_data: Dictionary = settlement_variant
		if String(settlement_data.get("settlement_id", "")) != settlement_id:
			continue
		settlement_data["settlement_state"] = settlement_state.duplicate(true)
		settlements_variant[index] = settlement_data
		_active_world_data["settlements"] = settlements_variant
		_rebuild_world_coord_lookups()
		return true
	return false


func get_settlement_state(settlement_id: String) -> Dictionary:
	var settlement: Dictionary = _settlements_by_id.get(settlement_id, {})
	if settlement is Dictionary:
		return (settlement as Dictionary).get("settlement_state", {}).duplicate(true)
	return {}


func command_world_move(direction: Vector2i, count: int = 1) -> Dictionary:
	return _execute_logged_command("world.move", "world", {
		"direction": direction,
		"count": count,
	}, func() -> Dictionary:
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
	)


func command_world_select(coord: Vector2i) -> Dictionary:
	return _execute_logged_command("world.select", "world", {
		"coord": coord,
	}, func() -> Dictionary:
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
	)


func command_open_settlement(coord: Vector2i = Vector2i(-1, -1)) -> Dictionary:
	return _execute_logged_command("settlement.open", "settlement", {
		"coord": coord,
	}, func() -> Dictionary:
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
	)


func command_world_inspect(coord: Vector2i) -> Dictionary:
	return _execute_logged_command("world.inspect", "world", {
		"coord": coord,
	}, func() -> Dictionary:
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
	)


func command_open_party() -> Dictionary:
	return _execute_logged_command("party.open", "party", {}, func() -> Dictionary:
		return _party_command_handler.command_open_party()
	)


func command_accept_quest(quest_id: StringName, allow_reaccept: bool = false) -> Dictionary:
	return _execute_logged_command("quest.accept", "quest", {
		"quest_id": quest_id,
		"allow_reaccept": allow_reaccept,
	}, func() -> Dictionary:
		if _character_management == null:
			return _command_error("运行时尚未初始化。")
		if quest_id == &"":
			return _command_error("任务 ID 不能为空。")
		var quest_data := _get_quest_def_data(quest_id)
		if quest_data.is_empty():
			return _command_error("未找到任务 %s。" % String(quest_id))
		var quest_label := _resolve_quest_label(quest_id, quest_data)
		if _party_state != null and _party_state.has_active_quest(quest_id):
			return _command_error("任务《%s》已在进行中，不能重复接取。" % quest_label)
		if _party_state != null and _party_state.has_claimable_quest(quest_id):
			return _command_error("任务《%s》已完成，奖励待领取，当前不可再次接取。" % quest_label)
		var has_completed: bool = _party_state != null and _party_state.has_completed_quest(quest_id)
		var is_repeatable := bool(quest_data.get("is_repeatable", false))
		var effective_allow_reaccept: bool = allow_reaccept or (has_completed and is_repeatable)
		if has_completed and not effective_allow_reaccept:
			return _command_error("任务《%s》已完成，当前不可再次接取。" % quest_label)
		if not _character_management.accept_quest(quest_id, get_world_step(), effective_allow_reaccept):
			return _command_error("当前无法接取任务《%s》。" % quest_label)
		_party_state = _character_management.get_party_state()
		var persist_error := _persist_party_state()
		var message := "已重新接取任务《%s》。" % quest_label if has_completed and effective_allow_reaccept else "已接取任务《%s》。" % quest_label
		if persist_error != OK:
			message = "%s 但队伍状态持久化失败。" % message
			_update_status(message)
			return _command_error(message)
		_update_status(message)
		return _command_ok(message)
	)


func command_progress_quest(quest_id: StringName, objective_id: StringName, progress_delta: int = 1, payload: Dictionary = {}) -> Dictionary:
	return _execute_logged_command("quest.progress", "quest", {
		"quest_id": quest_id,
		"objective_id": objective_id,
		"progress_delta": progress_delta,
		"payload": payload.duplicate(true),
	}, func() -> Dictionary:
		if _character_management == null:
			return _command_error("运行时尚未初始化。")
		if quest_id == &"" or objective_id == &"":
			return _command_error("任务 ID 和目标 ID 不能为空。")
		var event_data := {
			"event_type": "progress",
			"quest_id": String(quest_id),
			"objective_id": String(objective_id),
			"progress_delta": maxi(progress_delta, 0),
		}
		for key in payload.keys():
			event_data[key] = payload[key]
		var summary := apply_quest_progress_events_to_party([event_data], "quest")
		if not (summary.get("progressed_quest_ids", []) as Array).has(quest_id):
			return _command_error("当前无法推进任务 %s 的目标 %s。" % [String(quest_id), String(objective_id)])
		var persist_error := _persist_party_state()
		var message := "已推进任务 %s 的目标 %s。" % [String(quest_id), String(objective_id)]
		if persist_error != OK:
			message = "%s 但队伍状态持久化失败。" % message
			_update_status(message)
			return _command_error(message)
		_update_status(message)
		return _command_ok(message)
	)


func command_complete_quest(quest_id: StringName) -> Dictionary:
	return _execute_logged_command("quest.complete", "quest", {
		"quest_id": quest_id,
	}, func() -> Dictionary:
		if _character_management == null:
			return _command_error("运行时尚未初始化。")
		if quest_id == &"":
			return _command_error("任务 ID 不能为空。")
		var quest_data := _get_quest_def_data(quest_id)
		var quest_label := _resolve_quest_label(quest_id, quest_data)
		if not _character_management.complete_quest(quest_id, get_world_step()):
			return _command_error("当前无法完成任务《%s》。" % quest_label)
		_party_state = _character_management.get_party_state()
		var persist_error := _persist_party_state()
		var message := "已完成任务《%s》，奖励待领取。" % quest_label
		if persist_error != OK:
			message = "%s 但队伍状态持久化失败。" % message
			_update_status(message)
			return _command_error(message)
		_update_status(message)
		return _command_ok(message)
	)


func command_submit_quest_item(quest_id: StringName, objective_id: StringName = &"") -> Dictionary:
	return _execute_logged_command("quest.submit_item", "quest", {
		"quest_id": quest_id,
		"objective_id": objective_id,
	}, func() -> Dictionary:
		if _character_management == null:
			return _command_error("运行时尚未初始化。")
		if quest_id == &"":
			return _command_error("任务 ID 不能为空。")
		var quest_data := _get_quest_def_data(quest_id)
		if quest_data.is_empty():
			return _command_error("未找到任务 %s。" % String(quest_id))
		var quest_label := _resolve_quest_label(quest_id, quest_data)
		var submit_result := _character_management.submit_item_objective(quest_id, objective_id, get_world_step())
		if not bool(submit_result.get("ok", false)):
			var item_id := ProgressionDataUtils.to_string_name(submit_result.get("item_id", ""))
			var item_label := _get_item_display_name(item_id)
			var required_quantity := maxi(int(submit_result.get("required_quantity", 0)), 0)
			match String(submit_result.get("error_code", "")):
				"invalid_quest_id":
					return _command_error("任务 ID 不能为空。")
				"quest_not_active":
					return _command_error("当前没有进行中的任务《%s》。" % quest_label)
				"quest_def_missing":
					return _command_error("任务《%s》缺少目标配置，当前无法提交。" % quest_label)
				"invalid_submit_item_objective":
					return _command_error("任务《%s》包含无效的物资提交目标，当前无法提交。" % quest_label)
				"objective_already_complete":
					return _command_error("任务《%s》的物资目标已完成，无需重复提交。" % quest_label)
				"submit_item_missing_inventory":
					return _command_error("共享仓库缺少%s x%d，无法提交给任务《%s》。" % [
						item_label,
						required_quantity,
						quest_label,
					])
				"submit_item_commit_failed":
					return _command_error("当前无法从共享仓库扣除任务《%s》所需物资。" % quest_label)
				"quest_progress_failed":
					return _command_error("共享仓库扣除已回滚，当前无法推进任务《%s》。" % quest_label)
				_:
					return _command_error("任务《%s》当前没有可提交的物资目标。" % quest_label)
		_party_state = _character_management.get_party_state()
		var item_id := ProgressionDataUtils.to_string_name(submit_result.get("item_id", ""))
		var item_label := _get_item_display_name(item_id)
		var submitted_quantity := maxi(int(submit_result.get("submitted_quantity", 0)), 0)
		var claimable_quest_ids: Array = submit_result.get("claimable_quest_ids", [])
		var message := "已为任务《%s》提交 %s x%d。" % [quest_label, item_label, submitted_quantity]
		if claimable_quest_ids.has(quest_id):
			message = "已为任务《%s》提交 %s x%d，奖励待领取。" % [quest_label, item_label, submitted_quantity]
		var persist_error := _persist_party_state()
		if persist_error != OK:
			message = "%s 但队伍状态持久化失败。" % message
			_update_status(message)
			return _command_error(message)
		_update_status(message)
		var result := _command_ok(message)
		result["objective_id"] = String(submit_result.get("objective_id", ""))
		result["item_id"] = String(item_id)
		result["submitted_quantity"] = submitted_quantity
		return result
	)


func command_claim_quest(quest_id: StringName) -> Dictionary:
	return _execute_logged_command("quest.claim", "quest", {
		"quest_id": quest_id,
	}, func() -> Dictionary:
		if _character_management == null:
			return _command_error("运行时尚未初始化。")
		if quest_id == &"":
			return _command_error("任务 ID 不能为空。")
		var quest_data := _get_quest_def_data(quest_id)
		if quest_data.is_empty():
			return _command_error("未找到任务 %s。" % String(quest_id))
		var quest_label := _resolve_quest_label(quest_id, quest_data)
		var claim_result := _character_management.claim_quest_reward(quest_id, get_world_step())
		if not bool(claim_result.get("ok", false)):
			var error_code := String(claim_result.get("error_code", ""))
			match error_code:
				"quest_not_claimable":
					return _command_error("当前没有可领取的任务《%s》奖励。" % quest_label)
				"quest_def_missing":
					return _command_error("任务《%s》缺少奖励配置，当前无法领取。" % quest_label)
				"invalid_gold_amount":
					return _command_error("任务《%s》包含无效的金币奖励配置，当前无法领取。" % quest_label)
				"invalid_item_reward":
					return _command_error("任务《%s》包含无效的物品奖励配置，当前无法领取。" % quest_label)
				"invalid_pending_character_reward":
					return _command_error("任务《%s》包含无效的角色奖励配置，当前无法领取。" % quest_label)
				"item_reward_missing_def":
					return _command_error("任务《%s》引用了缺失的物品奖励配置，当前无法领取。" % quest_label)
				"reward_overflow":
					return _command_error("共享仓库空间不足，领取任务《%s》奖励会溢出，当前无法领取。" % quest_label)
				"quest_reward_commit_failed":
					return _command_error("任务《%s》奖励写入共享仓库失败，当前无法领取。" % quest_label)
				"unsupported_reward_types":
					var unsupported_types := _string_name_array_to_string_array(claim_result.get("unsupported_reward_types", []))
					var unsupported_text := "、".join(unsupported_types) if not unsupported_types.is_empty() else "未知奖励"
					return _command_error("任务《%s》包含暂不支持的奖励类型：%s。" % [quest_label, unsupported_text])
				_:
					return _command_error("当前无法领取任务《%s》奖励。" % quest_label)
		_party_state = _character_management.get_party_state()
		var persist_error := _persist_party_state()
		var gold_delta := int(claim_result.get("gold_delta", 0))
		var reward_summary := _build_quest_claim_reward_summary_text(claim_result)
		var message := "已领取任务《%s》奖励。" % quest_label
		if not reward_summary.is_empty():
			message = "已领取任务《%s》奖励，获得 %s。" % [quest_label, reward_summary]
		if persist_error != OK:
			message = "%s 但队伍状态持久化失败。" % message
			_update_status(message)
			return _command_error(message)
		_update_status(message)
		var result := _command_ok(message)
		result["gold_delta"] = gold_delta
		result["item_rewards"] = (claim_result.get("item_rewards", []) as Array).duplicate(true)
		result["pending_character_rewards"] = (claim_result.get("pending_character_rewards", []) as Array).duplicate(true)
		return result
	)


func command_select_party_member(member_id: StringName) -> Dictionary:
	return _execute_logged_command("party.select_member", "party", {
		"member_id": member_id,
	}, func() -> Dictionary:
		return _party_command_handler.command_select_party_member(member_id)
	)


func command_set_party_leader(member_id: StringName) -> Dictionary:
	return _execute_logged_command("party.set_leader", "party", {
		"member_id": member_id,
	}, func() -> Dictionary:
		return _party_command_handler.command_set_party_leader(member_id)
	)


func command_move_member_to_active(member_id: StringName) -> Dictionary:
	return _execute_logged_command("party.move_member_to_active", "party", {
		"member_id": member_id,
	}, func() -> Dictionary:
		return _party_command_handler.command_move_member_to_active(member_id)
	)


func command_move_member_to_reserve(member_id: StringName) -> Dictionary:
	return _execute_logged_command("party.move_member_to_reserve", "party", {
		"member_id": member_id,
	}, func() -> Dictionary:
		return _party_command_handler.command_move_member_to_reserve(member_id)
	)


func command_party_equip_item(member_id: StringName, item_id: StringName, slot_id: StringName = &"") -> Dictionary:
	return _execute_logged_command("party.equip_item", "party", {
		"member_id": member_id,
		"item_id": item_id,
		"slot_id": slot_id,
	}, func() -> Dictionary:
		return _party_command_handler.command_party_equip_item(member_id, item_id, slot_id)
	)


func command_party_unequip_item(member_id: StringName, slot_id: StringName) -> Dictionary:
	return _execute_logged_command("party.unequip_item", "party", {
		"member_id": member_id,
		"slot_id": slot_id,
	}, func() -> Dictionary:
		return _party_command_handler.command_party_unequip_item(member_id, slot_id)
	)


func command_open_party_warehouse() -> Dictionary:
	return _execute_logged_command("warehouse.open", "warehouse", {}, func() -> Dictionary:
		return _warehouse_handler.command_open_party_warehouse()
	)


func command_warehouse_discard_one(item_id: StringName) -> Dictionary:
	return _execute_logged_command("warehouse.discard_one", "warehouse", {
		"item_id": item_id,
	}, func() -> Dictionary:
		return _warehouse_handler.command_discard_one(item_id)
	)


func command_warehouse_discard_all(item_id: StringName) -> Dictionary:
	return _execute_logged_command("warehouse.discard_all", "warehouse", {
		"item_id": item_id,
	}, func() -> Dictionary:
		return _warehouse_handler.command_discard_all(item_id)
	)


func command_warehouse_use_item(item_id: StringName, member_id: StringName = &"") -> Dictionary:
	return _execute_logged_command("warehouse.use_item", "warehouse", {
		"item_id": item_id,
		"member_id": member_id,
	}, func() -> Dictionary:
		return _warehouse_handler.command_use_item(item_id, member_id)
	)


func command_warehouse_add_item(item_id: StringName, quantity: int) -> Dictionary:
	return _execute_logged_command("warehouse.add_item", "warehouse", {
		"item_id": item_id,
		"quantity": quantity,
	}, func() -> Dictionary:
		return _warehouse_handler.command_add_item(item_id, quantity)
	)


func command_execute_settlement_action(action_id: String, payload: Dictionary = {}) -> Dictionary:
	return _execute_logged_command("settlement.execute_action", "settlement", {
		"action_id": action_id,
		"payload": payload,
	}, func() -> Dictionary:
		return _settlement_command_handler.command_execute_settlement_action(action_id, payload)
	)


func command_shop_buy(item_id: StringName, quantity: int) -> Dictionary:
	return _execute_logged_command("shop.buy", "shop", {
		"item_id": item_id,
		"quantity": quantity,
	}, func() -> Dictionary:
		return _settlement_command_handler.command_shop_buy(item_id, quantity)
	)


func command_shop_sell(item_id: StringName, quantity: int) -> Dictionary:
	return _execute_logged_command("shop.sell", "shop", {
		"item_id": item_id,
		"quantity": quantity,
	}, func() -> Dictionary:
		return _settlement_command_handler.command_shop_sell(item_id, quantity)
	)


func command_stagecoach_travel(settlement_id: String) -> Dictionary:
	return _execute_logged_command("stagecoach.travel", "stagecoach", {
		"settlement_id": settlement_id,
	}, func() -> Dictionary:
		return _settlement_command_handler.command_stagecoach_travel(settlement_id)
	)


func command_battle_tick(total_seconds: float, step_seconds: float = 1.0 / 60.0) -> Dictionary:
	return _execute_logged_command("battle.tick", "battle", {
		"total_seconds": total_seconds,
		"step_seconds": step_seconds,
	}, func() -> Dictionary:
		return _battle_session_facade.command_battle_tick(total_seconds, step_seconds)
	)


func command_battle_select_skill(slot_index: int) -> Dictionary:
	return _execute_logged_command("battle.select_skill", "battle", {
		"slot_index": slot_index,
	}, func() -> Dictionary:
		return _battle_session_facade.command_battle_select_skill(slot_index)
	)


func command_battle_cycle_variant(step: int) -> Dictionary:
	return _execute_logged_command("battle.cycle_variant", "battle", {
		"step": step,
	}, func() -> Dictionary:
		return _battle_session_facade.command_battle_cycle_variant(step)
	)


func command_battle_clear_skill() -> Dictionary:
	return _execute_logged_command("battle.clear_skill", "battle", {}, func() -> Dictionary:
		return _battle_session_facade.command_battle_clear_skill()
	)


func command_battle_move_to(target_coord: Vector2i) -> Dictionary:
	return _execute_logged_command("battle.move_to", "battle", {
		"target_coord": target_coord,
	}, func() -> Dictionary:
		return _battle_session_facade.command_battle_move_to(target_coord)
	)


func command_battle_move_direction(direction: Vector2i) -> Dictionary:
	return _execute_logged_command("battle.move_direction", "battle", {
		"direction": direction,
	}, func() -> Dictionary:
		return _battle_session_facade.command_battle_move_direction(direction)
	)


func command_battle_wait_or_resolve() -> Dictionary:
	return _execute_logged_command("battle.wait_or_resolve", "battle", {}, func() -> Dictionary:
		return _battle_session_facade.command_battle_wait_or_resolve()
	)


func command_battle_inspect(coord: Vector2i) -> Dictionary:
	return _execute_logged_command("battle.inspect", "battle", {
		"coord": coord,
	}, func() -> Dictionary:
		return _battle_session_facade.command_battle_inspect(coord)
	)


func command_confirm_pending_reward() -> Dictionary:
	return _execute_logged_command("reward.confirm_pending", "reward", {}, func() -> Dictionary:
		return _reward_flow_handler.command_confirm_pending_reward() if _reward_flow_handler != null else _command_error("运行时尚未初始化。")
	)


func command_choose_promotion(profession_id: StringName) -> Dictionary:
	return _execute_logged_command("promotion.choose", "promotion", {
		"profession_id": profession_id,
	}, func() -> Dictionary:
		return _reward_flow_handler.command_choose_promotion(profession_id) if _reward_flow_handler != null else _command_error("运行时尚未初始化。")
	)


func command_confirm_submap_entry() -> Dictionary:
	return _execute_logged_command("submap.confirm_entry", "submap", {
		"target_submap_id": String(_pending_submap_prompt.get("target_submap_id", "")),
	}, func() -> Dictionary:
		if _pending_submap_prompt.is_empty():
			return _command_error("当前没有待确认的子地图入口。")
		return _confirm_pending_submap_entry()
	)


func command_cancel_submap_entry() -> Dictionary:
	return _execute_logged_command("submap.cancel_entry", "submap", {
		"target_submap_id": String(_pending_submap_prompt.get("target_submap_id", "")),
	}, func() -> Dictionary:
		if _pending_submap_prompt.is_empty():
			return _command_error("当前没有待确认的子地图入口。")
		var target_name := String(_pending_submap_prompt.get("target_display_name", "子地图"))
		_pending_submap_prompt.clear()
		_active_modal_id = ""
		_update_status("已取消进入 %s。" % target_name)
		return _command_ok()
	)


func command_confirm_battle_start() -> Dictionary:
	return _execute_logged_command("battle.confirm_start", "battle", {
		"encounter_id": _active_battle_encounter_id,
	}, func() -> Dictionary:
		if _pending_battle_start_prompt.is_empty():
			return _command_error("当前没有待确认的战斗开始提示。")
		if not _is_battle_active() or _battle_state == null:
			return _command_error("当前没有待开始的战斗。")
		_pending_battle_start_prompt.clear()
		_active_modal_id = ""
		_battle_state.modal_state = &""
		if _battle_state.timeline != null:
			_battle_state.timeline.frozen = false
		_update_status("战斗开始，TU 现在按每秒 5 点推进。")
		return _command_ok()
	)


func command_return_from_submap() -> Dictionary:
	return _execute_logged_command("submap.return", "submap", {
		"active_map_id": _active_map_id,
	}, func() -> Dictionary:
		if not is_submap_active():
			return _command_error("当前不在子地图中。")
		return _return_from_active_submap()
	)


func command_close_active_modal() -> Dictionary:
	return _execute_logged_command("modal.close_active", "ui", {
		"modal_id": _active_modal_id,
	}, func() -> Dictionary:
		return _reward_flow_handler.command_close_active_modal() if _reward_flow_handler != null else _command_error("运行时尚未初始化。")
	)


func apply_party_roster(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName]) -> Dictionary:
	return _execute_logged_command("party.apply_roster", "party", {
		"active_member_ids": active_member_ids,
		"reserve_member_ids": reserve_member_ids,
	}, func() -> Dictionary:
		return _party_command_handler.apply_party_roster(active_member_ids, reserve_member_ids)
	)


func submit_promotion_choice(member_id: StringName, profession_id: StringName, selection: Dictionary) -> Dictionary:
	return _execute_logged_command("promotion.submit_choice", "promotion", {
		"member_id": member_id,
		"profession_id": profession_id,
		"selection": selection,
	}, func() -> Dictionary:
		return _reward_flow_handler.submit_promotion_choice(member_id, profession_id, selection) if _reward_flow_handler != null else _command_error("运行时尚未初始化。")
	)


func cancel_promotion_choice() -> Dictionary:
	return _execute_logged_command("promotion.cancel_choice", "promotion", {}, func() -> Dictionary:
		return _reward_flow_handler.cancel_promotion_choice() if _reward_flow_handler != null else _command_error("运行时尚未初始化。")
	)


func confirm_active_reward() -> Dictionary:
	return _execute_logged_command("reward.confirm_active", "reward", {}, func() -> Dictionary:
		return _reward_flow_handler.confirm_active_reward() if _reward_flow_handler != null else _command_error("运行时尚未初始化。")
	)


func reset_battle_focus() -> Dictionary:
	return _execute_logged_command("battle.reset_focus", "battle", {}, func() -> Dictionary:
		return _battle_session_facade.reset_battle_focus()
	)


func select_world_cell(coord: Vector2i) -> Dictionary:
	return _execute_logged_command("world.click_select", "world", {
		"coord": coord,
	}, func() -> Dictionary:
		if is_submap_active() and not _is_battle_active() and not _is_modal_window_open():
			return _return_from_active_submap()
		_on_world_map_cell_clicked(coord)
		return _command_ok()
	)


func inspect_world_cell(coord: Vector2i) -> Dictionary:
	return _execute_logged_command("world.click_inspect", "world", {
		"coord": coord,
	}, func() -> Dictionary:
		_on_world_map_cell_right_clicked(coord)
		return _command_ok()
	)


func select_battle_cell(coord: Vector2i) -> Dictionary:
	return _execute_logged_command("battle.click_select", "battle", {
		"coord": coord,
	}, func() -> Dictionary:
		return _battle_session_facade.command_battle_move_to(coord)
	)


func inspect_battle_cell(coord: Vector2i) -> Dictionary:
	return _execute_logged_command("battle.click_inspect", "battle", {
		"coord": coord,
	}, func() -> Dictionary:
		_on_battle_cell_right_clicked(coord)
		return _command_ok()
	)


func _command_ok(message: String = "", battle_refresh_mode: String = "") -> Dictionary:
	var resolved_message := message if not message.is_empty() else _current_status_message
	var result := {
		"ok": true,
		"message": resolved_message,
		"battle_refresh_mode": battle_refresh_mode,
	}
	_log_active_command_scope_result(result)
	return result


func _command_error(message: String) -> Dictionary:
	if not message.is_empty():
		_update_status(message)
	var result := {
		"ok": false,
		"message": message,
	}
	_log_active_command_scope_result(result)
	return result


func _execute_logged_command(event_id: String, domain: String, context: Dictionary, action: Callable) -> Dictionary:
	var previous_scope := _active_command_log_scope.duplicate(true)
	var command_args: Dictionary = _normalize_log_variant(context)
	_active_command_log_scope = {
		"event_id": event_id,
		"domain": domain,
		"context": {
			"command_args": command_args,
			"before": _build_runtime_log_state(),
		},
		"logged": false,
	}
	var result_variant = action.call()
	var result: Dictionary = result_variant if result_variant is Dictionary else {}
	if not bool(_active_command_log_scope.get("logged", false)):
		_log_command_result(_active_command_log_scope, result)
	_active_command_log_scope = previous_scope
	return result


func _log_active_command_scope_result(result: Dictionary) -> void:
	if _active_command_log_scope.is_empty():
		return
	if bool(_active_command_log_scope.get("logged", false)):
		return
	_log_command_result(_active_command_log_scope, result)


func _log_command_result(scope: Dictionary, result: Dictionary) -> void:
	if scope.is_empty():
		return
	var resolved_result: Dictionary = result if result != null else {}
	var ok := bool(resolved_result.get("ok", false))
	var message := String(resolved_result.get("message", _current_status_message))
	var log_context: Dictionary = (scope.get("context", {}) as Dictionary).duplicate(true)
	log_context["after"] = _build_runtime_log_state()
	log_context["ok"] = ok
	if not message.is_empty():
		log_context["result_message"] = message
	var battle_refresh_mode := String(resolved_result.get("battle_refresh_mode", ""))
	if not battle_refresh_mode.is_empty():
		log_context["battle_refresh_mode"] = battle_refresh_mode
	_log_runtime_event(
		"info" if ok else "warn",
		String(scope.get("domain", "runtime")),
		String(scope.get("event_id", "runtime.command")),
		message if not message.is_empty() else ("命令成功。" if ok else "命令失败。"),
		log_context
	)
	_active_command_log_scope["logged"] = true


func _build_runtime_log_state() -> Dictionary:
	var context := {
		"save_id": _game_session.get_active_save_id() if _game_session != null else "",
		"map_id": _active_map_id,
		"map_display_name": _active_map_display_name,
		"player_coord": _player_coord,
		"selected_coord": _selected_coord,
		"active_modal_id": _active_modal_id,
		"battle_active": _is_battle_active(),
	}
	if _is_battle_active():
		context["battle"] = _build_battle_log_state()
	return context


func _log_runtime_event(level: String, domain: String, event_id: String, message: String, context: Dictionary = {}) -> void:
	if _game_session == null or not _game_session.has_method("log_event"):
		return
	_game_session.log_event(level, domain, event_id, message, context)


func _log_battle_batch_entries(batch) -> void:
	if batch == null or batch.log_lines.is_empty():
		return
	var base_context := {
		"runtime": _build_runtime_log_state(),
		"phase_changed": bool(batch.phase_changed),
		"battle_ended": bool(batch.battle_ended),
		"modal_requested": bool(batch.modal_requested),
		"changed_unit_count": batch.changed_unit_ids.size(),
		"changed_coord_count": batch.changed_coords.size(),
		"changed_coords": _normalize_log_variant(batch.changed_coords),
		"changed_unit_ids": _normalize_log_variant(batch.changed_unit_ids),
		"changed_units": _build_battle_unit_log_entries(batch.changed_unit_ids),
	}
	for log_line in batch.log_lines:
		_log_runtime_event("info", "battle", "battle.log", String(log_line), base_context)


func _build_battle_log_state() -> Dictionary:
	if not _is_battle_active() or _battle_state == null:
		return {}
	return {
		"encounter_id": String(_active_battle_encounter_id),
		"encounter_name": _active_battle_encounter_name,
		"battle_id": String(_battle_state.battle_id),
		"seed": int(_battle_state.seed),
		"terrain_profile_id": String(_battle_state.terrain_profile_id),
		"map_size": _battle_state.map_size,
		"phase": String(_battle_state.phase),
		"modal_state": String(_battle_state.modal_state),
		"winner_faction_id": String(_battle_state.winner_faction_id),
		"active_unit_id": String(_battle_state.active_unit_id),
		"active_unit_name": _get_battle_active_unit_name(),
		"selected_coord": _battle_selected_coord,
		"selected_skill_id": String(_selected_battle_skill_id),
		"selected_skill_variant_id": String(_selected_battle_skill_variant_id),
		"selected_target_coords": _queued_battle_skill_target_coords,
		"selected_target_unit_ids": _queued_battle_skill_target_unit_ids,
		"terrain_counts": _count_battle_terrain_types(),
		"units": _build_battle_unit_log_entries(),
	}


func _build_battle_unit_log_entries(unit_ids: Array = []) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if _battle_state == null:
		return result
	var normalized_ids: Array[StringName] = []
	if unit_ids.is_empty():
		for unit_key in ProgressionDataUtils.sorted_string_keys(_battle_state.units):
			normalized_ids.append(StringName(unit_key))
	else:
		for unit_id_variant in unit_ids:
			var normalized_unit_id := ProgressionDataUtils.to_string_name(unit_id_variant)
			if normalized_unit_id == &"":
				continue
			if normalized_ids.has(normalized_unit_id):
				continue
			normalized_ids.append(normalized_unit_id)
	for unit_id in normalized_ids:
		var unit_state := _battle_state.units.get(unit_id) as BattleUnitState
		if unit_state == null:
			continue
		result.append({
			"unit_id": String(unit_state.unit_id),
			"display_name": unit_state.display_name if not unit_state.display_name.is_empty() else String(unit_state.unit_id),
			"faction_id": String(unit_state.faction_id),
			"control_mode": String(unit_state.control_mode),
			"is_alive": bool(unit_state.is_alive),
			"coord": unit_state.coord,
			"current_hp": int(unit_state.current_hp),
			"current_mp": int(unit_state.current_mp),
			"current_ap": int(unit_state.current_ap),
		})
	return result


func _normalize_log_variant(value):
	match typeof(value):
		TYPE_STRING_NAME:
			return String(value)
		TYPE_VECTOR2I:
			var coord: Vector2i = value
			return {
				"x": coord.x,
				"y": coord.y,
			}
		TYPE_VECTOR2:
			var float_coord: Vector2 = value
			return {
				"x": float_coord.x,
				"y": float_coord.y,
			}
		TYPE_DICTIONARY:
			var normalized_dict: Dictionary = {}
			for key in value.keys():
				normalized_dict[String(key)] = _normalize_log_variant(value.get(key))
			return normalized_dict
		TYPE_ARRAY:
			var normalized_array: Array = []
			for entry in value:
				normalized_array.append(_normalize_log_variant(entry))
			return normalized_array
		TYPE_OBJECT:
			if value == null:
				return null
			if value.has_method("to_dict"):
				return _normalize_log_variant(value.to_dict())
			return str(value)
		_:
			return value


func _resolve_command_settlement_id() -> String:
	return _settlement_command_handler.resolve_command_settlement_id()


func _get_current_promotion_prompt() -> Dictionary:
	return _reward_flow_handler.get_current_promotion_prompt() if _reward_flow_handler != null else {}

func _move_player(direction: Vector2i) -> void:
	var previous_settlement := _get_settlement_at(_player_coord)
	var target_coord := _player_coord + direction
	if not _grid_system.is_cell_walkable(target_coord):
		_update_status("已到达大地图边界。")
		return

	_player_coord = target_coord
	_selected_coord = _player_coord
	_advance_world_time_by_steps(1)
	_refresh_world_event_discovery()
	_refresh_fog()

	var triggered_event := _get_triggerable_world_event_at(_player_coord)
	if not triggered_event.is_empty():
		var player_persist_error := int(_game_session.set_player_coord(_player_coord))
		var world_persist_error := int(_game_session.set_world_data(_world_data))
		_open_world_event_prompt(triggered_event)
		if player_persist_error != OK or world_persist_error != OK:
			_update_status("%s 已显现，但当前位置或世界状态持久化失败。" % String(triggered_event.get("display_name", "事件入口")))
		return

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
	var advance_result := _world_time_system.advance(_active_world_data, delta_steps)
	_wild_encounter_growth_system.apply_step_advance(
		_active_world_data,
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
			int(_active_world_data.get("world_step", 0)),
			_wild_encounter_rosters
		)
		return
	_remove_active_battle_encounter_anchor()


func _start_battle(encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT) -> void:
	_battle_session_facade.start_battle(encounter_anchor)


func _build_battle_start_context(encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT) -> Dictionary:
	return _battle_session_facade._build_battle_start_context(encounter_anchor)


func _resolve_battle_terrain_profile(encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT) -> StringName:
	return _battle_session_facade._resolve_battle_terrain_profile(encounter_anchor)


func _resolve_active_battle() -> void:
	_battle_session_facade.resolve_active_battle()


func _attempt_battle_move(direction: Vector2i) -> StringName:
	return _battle_session_facade.attempt_battle_move(direction)


func _refresh_fog() -> void:
	if _active_generation_config == null:
		return
	var leader_member_id := "player_main"
	if _party_state != null and _party_state.leader_member_id != &"":
		leader_member_id = String(_party_state.leader_member_id)
	var sources: Array = [
		VISION_SOURCE_DATA_SCRIPT.new(leader_member_id, _player_coord, _active_generation_config.player_vision_range, _player_faction_id),
	]
	_fog_system.rebuild_visibility_for_faction(_player_faction_id, sources)


func _on_world_map_cell_clicked(coord: Vector2i) -> void:
	if _is_battle_active():
		return
	if _is_modal_window_open():
		return
	if is_submap_active():
		_return_from_active_submap()
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
	_battle_session_facade.on_battle_cell_clicked(coord)


func _on_battle_cell_right_clicked(coord: Vector2i) -> void:
	_battle_session_facade.on_battle_cell_right_clicked(coord)


func _on_battle_skill_slot_selected(index: int) -> void:
	_battle_session_facade.on_battle_skill_slot_selected(index)


func _on_battle_skill_variant_cycle_requested(step: int) -> void:
	_battle_session_facade.on_battle_skill_variant_cycle_requested(step)


func _on_battle_skill_clear_requested() -> void:
	_battle_session_facade.on_battle_skill_clear_requested()


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
	if coord == _player_coord:
		_mark_settlement_visited(_active_settlement_id)
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
	for encounter_variant in _active_world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		if encounter_anchor.entity_id == entity_id:
			return encounter_anchor
	return null


func _refresh_battle_panel() -> void:
	if not _is_battle_active():
		return
	if not is_instance_valid(battle_map_panel):
		return
	battle_map_panel.refresh(
		_battle_state,
		_battle_selected_coord,
		_selected_battle_skill_id,
		get_selected_battle_skill_name(),
		get_selected_battle_skill_variant_name(),
		get_selected_battle_skill_target_coords(),
		get_battle_overlay_target_coords(),
		get_selected_battle_skill_required_coord_count(),
		get_selected_battle_skill_target_unit_ids()
	)


func _refresh_battle_panel_overlay() -> void:
	if not _is_battle_active():
		return
	if not is_instance_valid(battle_map_panel):
		return
	battle_map_panel.refresh_overlay(
		_battle_state,
		_battle_selected_coord,
		_selected_battle_skill_id,
		get_selected_battle_skill_name(),
		get_selected_battle_skill_variant_name(),
		get_selected_battle_skill_target_coords(),
		get_battle_overlay_target_coords(),
		get_selected_battle_skill_required_coord_count(),
		get_selected_battle_skill_target_unit_ids()
	)


func _refresh_battle_selection_state() -> void:
	if not _is_battle_active():
		return
	_battle_selection.sync_selected_battle_skill_state()
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
	world_map_view.refresh_world(_active_world_data)
	world_map_view.set_runtime_state(_player_coord, _selected_coord)


func _set_battle_view_active() -> void:
	if world_map_background != null:
		world_map_background.visible = false
	world_map_view.visible = false
	if not is_instance_valid(battle_map_panel):
		return
	battle_map_panel.show_battle(
		_battle_state,
		_battle_selected_coord,
		_selected_battle_skill_id,
		get_selected_battle_skill_name(),
		get_selected_battle_skill_variant_name(),
		get_selected_battle_skill_target_coords(),
		get_battle_overlay_target_coords(),
		get_selected_battle_skill_required_coord_count(),
		get_selected_battle_skill_target_unit_ids()
	)


func _remove_active_battle_encounter_anchor() -> void:
	if _active_battle_encounter_id == &"":
		return

	var remaining_anchors: Array = []
	for encounter_anchor_data in _active_world_data.get("encounter_anchors", []):
		var encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT = encounter_anchor_data as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		if encounter_anchor.entity_id == _active_battle_encounter_id:
			continue
		remaining_anchors.append(encounter_anchor)

	_active_world_data["encounter_anchors"] = remaining_anchors
	_rebuild_world_coord_lookups()


func _on_settlement_action_requested(settlement_id: String, action_id: String, payload: Dictionary) -> void:
	_settlement_command_handler.on_settlement_action_requested(settlement_id, action_id, payload)


func _on_settlement_window_closed() -> void:
	_settlement_command_handler.on_settlement_window_closed()


func _on_character_info_window_closed() -> void:
	if _reward_flow_handler != null:
		_reward_flow_handler.on_character_info_window_closed()


func _open_party_management_window() -> void:
	_party_command_handler._open_party_management_window()


func _on_party_leader_change_requested(member_id: StringName) -> void:
	_party_command_handler._on_party_leader_change_requested(member_id)


func _on_party_roster_change_requested(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName]) -> void:
	_party_command_handler._on_party_roster_change_requested(active_member_ids, reserve_member_ids)


func _on_party_management_window_closed() -> void:
	_party_command_handler._on_party_management_window_closed()


func _on_party_management_warehouse_requested() -> void:
	_party_command_handler._on_party_management_warehouse_requested()


func _on_promotion_choice_submitted(member_id: StringName, profession_id: StringName, selection: Dictionary) -> void:
	if _reward_flow_handler != null:
		_reward_flow_handler.on_promotion_choice_submitted(member_id, profession_id, selection)


func _on_promotion_choice_cancelled() -> void:
	if _reward_flow_handler != null:
		_reward_flow_handler.on_promotion_choice_cancelled()


func _on_character_reward_confirmed() -> void:
	if _reward_flow_handler != null:
		_reward_flow_handler.on_character_reward_confirmed()


func _apply_party_state_to_runtime(success_message: String) -> void:
	_party_command_handler._apply_party_state_to_runtime(success_message)


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
	_battle_session_facade.apply_battle_batch(batch)
	_log_battle_batch_entries(batch)


func _refresh_battle_runtime_state() -> void:
	_battle_session_facade.refresh_battle_runtime_state()


func _build_battle_seed(encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT) -> int:
	return _battle_session_facade.build_battle_seed(encounter_anchor)


func _get_runtime_battle_state() -> BattleState:
	return _battle_session_facade.get_runtime_battle_state()


func _is_battle_finished() -> bool:
	return _battle_session_facade.is_battle_finished()


func _get_runtime_active_unit() -> BattleUnitState:
	return _battle_session_facade.get_runtime_active_unit()


func _get_manual_active_unit() -> BattleUnitState:
	return _battle_session_facade.get_manual_active_unit()


func _get_runtime_unit_at_coord(coord: Vector2i) -> BattleUnitState:
	return _battle_session_facade.get_runtime_unit_at_coord(coord)


func _build_wait_command():
	return _battle_session_facade.build_wait_command()


func _issue_battle_command(command) -> StringName:
	return _battle_session_facade.issue_battle_command(command)


func _capture_pending_promotion_prompt(progression_deltas: Array) -> void:
	_battle_session_facade.capture_pending_promotion_prompt(progression_deltas)


func _build_promotion_prompt(delta, selection_hint: String = "确认后将在战斗中立即生效。") -> Dictionary:
	return _battle_session_facade.build_promotion_prompt(delta, selection_hint)


func _get_default_battle_selected_coord() -> Vector2i:
	return _battle_session_facade.get_default_battle_selected_coord()


func _get_battle_unit_by_id(unit_id: StringName) -> BattleUnitState:
	return _battle_session_facade.get_battle_unit_by_id(unit_id)


func _get_battle_unit_at_coord(coord: Vector2i) -> BattleUnitState:
	return _battle_session_facade.get_battle_unit_at_coord(coord)




func _get_battle_active_unit() -> BattleUnitState:
	return _battle_session_facade.get_battle_active_unit()


func _get_battle_active_unit_name() -> String:
	return _battle_session_facade.get_battle_active_unit_name()


func _get_battle_unit_type_label(unit_id: String) -> String:
	return _battle_session_facade.get_battle_unit_type_label(unit_id)


func _count_battle_terrain_types() -> Dictionary:
	return _battle_session_facade.get_battle_terrain_counts()


func _format_optional_text(value: String) -> String:
	return value if not value.is_empty() else "无"


func _build_default_battle_quest_progress_events(winner_faction_id: String) -> Array[Dictionary]:
	if winner_faction_id != "player":
		return []
	var encounter_anchor := _get_encounter_anchor_by_id(_active_battle_encounter_id)
	if encounter_anchor == null:
		return []
	return [{
		"event_type": "progress",
		"objective_type": "defeat_enemy",
		"target_id": String(encounter_anchor.enemy_roster_template_id),
		"progress_delta": 1,
		"enemy_template_id": String(encounter_anchor.enemy_roster_template_id),
		"encounter_id": String(encounter_anchor.entity_id),
		"encounter_kind": String(encounter_anchor.encounter_kind),
	}]


func _has_quest_progress_summary_changes(summary: Dictionary) -> bool:
	return not (summary.get("accepted_quest_ids", []) as Array).is_empty() \
		or not (summary.get("progressed_quest_ids", []) as Array).is_empty() \
		or not (summary.get("claimable_quest_ids", []) as Array).is_empty() \
		or not (summary.get("completed_quest_ids", []) as Array).is_empty()


func _format_quest_progress_summary(summary: Dictionary) -> String:
	var parts: Array[String] = []
	var accepted_ids: Array = summary.get("accepted_quest_ids", [])
	var progressed_ids: Array = summary.get("progressed_quest_ids", [])
	var claimable_ids: Array = summary.get("claimable_quest_ids", [])
	var completed_ids: Array = summary.get("completed_quest_ids", [])
	if not accepted_ids.is_empty():
		parts.append("接取 %s" % _format_string_name_list(accepted_ids))
	if not progressed_ids.is_empty():
		parts.append("推进 %s" % _format_string_name_list(progressed_ids))
	if not claimable_ids.is_empty():
		parts.append("待领奖励 %s" % _format_string_name_list(claimable_ids))
	if not completed_ids.is_empty():
		parts.append("完成 %s" % _format_string_name_list(completed_ids))
	return "任务进度已更新：%s。" % "；".join(parts) if not parts.is_empty() else "任务进度未变化。"


func _get_quest_def_data(quest_id: StringName) -> Dictionary:
	if _game_session == null or quest_id == &"":
		return {}
	var quest_defs: Dictionary = _game_session.get_quest_defs()
	if quest_defs == null:
		return {}
	var quest_variant = quest_defs.get(quest_id, quest_defs.get(String(quest_id), null))
	if quest_variant is Dictionary:
		return (quest_variant as Dictionary).duplicate(true)
	if quest_variant is Object and quest_variant.has_method("to_dict"):
		var quest_data_variant = quest_variant.to_dict()
		if quest_data_variant is Dictionary:
			return (quest_data_variant as Dictionary).duplicate(true)
	return {}


func _resolve_quest_label(quest_id: StringName, quest_data: Dictionary) -> String:
	var display_name := String(quest_data.get("display_name", "")).strip_edges()
	return display_name if not display_name.is_empty() else String(quest_id)


func _quest_progress_summary_to_string_dict(summary: Dictionary) -> Dictionary:
	return {
		"accepted_quest_ids": _string_name_array_to_string_array(summary.get("accepted_quest_ids", [])),
		"progressed_quest_ids": _string_name_array_to_string_array(summary.get("progressed_quest_ids", [])),
		"claimable_quest_ids": _string_name_array_to_string_array(summary.get("claimable_quest_ids", [])),
		"completed_quest_ids": _string_name_array_to_string_array(summary.get("completed_quest_ids", [])),
	}


func _format_string_name_list(values: Array) -> String:
	var labels := _string_name_array_to_string_array(values)
	return "、".join(labels)


func _string_name_array_to_string_array(values: Array) -> Array[String]:
	var labels: Array[String] = []
	for value in ProgressionDataUtils.to_string_name_array(values):
		labels.append(String(value))
	return labels


func _build_quest_claim_reward_summary_text(claim_result: Dictionary) -> String:
	var reward_parts: Array[String] = []
	var gold_delta := int(claim_result.get("gold_delta", 0))
	if gold_delta > 0:
		reward_parts.append("%d 金" % gold_delta)
	for reward_variant in claim_result.get("item_rewards", []):
		if reward_variant is not Dictionary:
			continue
		var reward_data := reward_variant as Dictionary
		var reward_quantity := int(reward_data.get("quantity", 0))
		if reward_quantity <= 0:
			continue
		var reward_label := String(reward_data.get("display_name", reward_data.get("item_id", ""))).strip_edges()
		if reward_label.is_empty():
			reward_label = String(reward_data.get("item_id", ""))
		reward_parts.append("%s x%d" % [reward_label, reward_quantity])
	for reward_variant in claim_result.get("pending_character_rewards", []):
		if reward_variant is not Dictionary:
			continue
		var reward_data := reward_variant as Dictionary
		var member_name := String(reward_data.get("member_name", "")).strip_edges()
		reward_parts.append("%s的角色奖励" % member_name if not member_name.is_empty() else "角色奖励")
	return "、".join(PackedStringArray(reward_parts))


func _update_status(message: String) -> void:
	_current_status_message = message
	if status_label != null:
		status_label.text = message


func _is_modal_window_open() -> bool:
	return _active_modal_id != ""


func _enqueue_pending_character_rewards(reward_variants: Array) -> void:
	if _reward_flow_handler != null:
		_reward_flow_handler.enqueue_pending_character_rewards(reward_variants)


func _present_pending_reward_if_ready() -> bool:
	return _reward_flow_handler.present_pending_reward_if_ready() if _reward_flow_handler != null else false


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


func _persist_world_data() -> int:
	if _game_session == null:
		return ERR_UNAVAILABLE
	return int(_game_session.set_world_data(_world_data))


func _mark_settlement_visited(settlement_id: String) -> void:
	if settlement_id.is_empty():
		return
	var settlement_state := get_settlement_state(settlement_id)
	if bool(settlement_state.get("visited", false)):
		return
	settlement_state["visited"] = true
	set_active_settlement_state(settlement_id, settlement_state)


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
	for settlement in _active_world_data.get("settlements", []):
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
	_world_event_by_coord.clear()

	for settlement in _active_world_data.get("settlements", []):
		if settlement is not Dictionary:
			continue
		_settlements_by_id[String(settlement.get("settlement_id", ""))] = settlement
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		for y in range(size.y):
			for x in range(size.x):
				_settlement_by_coord[origin + Vector2i(x, y)] = settlement

	for npc in _active_world_data.get("world_npcs", []):
		if npc is not Dictionary:
			continue
		_world_npc_by_coord[npc.get("coord", Vector2i.ZERO)] = npc

	for encounter_anchor_data in _active_world_data.get("encounter_anchors", []):
		var encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT = encounter_anchor_data as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		_encounter_anchor_by_coord[encounter_anchor.world_coord] = encounter_anchor

	for world_event_variant in _active_world_data.get("world_events", []):
		if world_event_variant is not Dictionary:
			continue
		var world_event: Dictionary = world_event_variant
		if not bool(world_event.get("is_discovered", false)):
			continue
		_world_event_by_coord[world_event.get("world_coord", Vector2i.ZERO)] = world_event


func _sync_active_world_context() -> void:
	_active_map_id = String(_world_data.get("active_submap_id", ""))
	if not _active_map_id.is_empty() and _get_mounted_submap_entry(_active_map_id).is_empty():
		_active_map_id = ""
		_world_data["active_submap_id"] = ""
	_active_world_data = _resolve_active_world_data()
	_active_generation_config = _resolve_active_generation_config()
	_active_map_display_name = _resolve_active_map_display_name()
	if _active_generation_config != null:
		_grid_system.setup(_active_generation_config.world_size_in_chunks, _active_generation_config.chunk_size)
		_fog_system.setup(_active_generation_config.get_world_size_cells())
	_refresh_world_event_discovery()
	_rebuild_world_coord_lookups()
	_register_settlement_footprints()
	if not _grid_system.is_cell_inside_world(_player_coord):
		_player_coord = _resolve_active_map_player_coord()
	if not _grid_system.is_cell_inside_world(_selected_coord):
		_selected_coord = _player_coord


func _resolve_active_world_data() -> Dictionary:
	if _active_map_id.is_empty():
		return _world_data
	var submap_entry := _get_mounted_submap_entry(_active_map_id)
	var submap_world_data = submap_entry.get("world_data", {})
	return submap_world_data if submap_world_data is Dictionary else _world_data


func _resolve_active_generation_config():
	if _active_map_id.is_empty():
		return _generation_config
	return _load_submap_generation_config(_active_map_id)


func _resolve_active_map_display_name() -> String:
	if _active_map_id.is_empty():
		return "大地图"
	var submap_entry := _get_mounted_submap_entry(_active_map_id)
	var display_name := String(submap_entry.get("display_name", ""))
	return display_name if not display_name.is_empty() else _active_map_id


func _resolve_active_map_player_coord() -> Vector2i:
	if _active_map_id.is_empty():
		return _world_data.get("player_start_coord", _player_coord)
	var submap_entry := _get_mounted_submap_entry(_active_map_id)
	var stored_coord: Vector2i = submap_entry.get("player_coord", Vector2i(-1, -1))
	if stored_coord != Vector2i(-1, -1):
		return stored_coord
	return _active_world_data.get("player_start_coord", Vector2i.ZERO)


func _refresh_world_event_discovery() -> void:
	var events_variant = _active_world_data.get("world_events", [])
	if events_variant is not Array:
		return
	var changed := false
	for index in range(events_variant.size()):
		var event_variant = events_variant[index]
		if event_variant is not Dictionary:
			continue
		var world_event: Dictionary = event_variant
		if bool(world_event.get("is_discovered", false)):
			continue
		if not _is_world_event_discovery_condition_met(world_event):
			continue
		world_event["is_discovered"] = true
		events_variant[index] = world_event
		changed = true
	if changed:
		_active_world_data["world_events"] = events_variant
		_rebuild_world_coord_lookups()


func _is_world_event_discovery_condition_met(world_event: Dictionary) -> bool:
	var condition_id := String(world_event.get("discovery_condition_id", "")).strip_edges()
	return condition_id.is_empty() or condition_id == "always_true"


func _get_world_event_at(coord: Vector2i) -> Dictionary:
	var world_event_variant = _world_event_by_coord.get(coord, {})
	return world_event_variant.duplicate(true) if world_event_variant is Dictionary else {}


func _get_triggerable_world_event_at(coord: Vector2i) -> Dictionary:
	var world_event := _get_world_event_at(coord)
	if world_event.is_empty():
		return {}
	if not bool(world_event.get("is_discovered", false)):
		return {}
	if String(world_event.get("event_type", "")) != "enter_submap":
		return {}
	if String(world_event.get("target_submap_id", "")).is_empty():
		return {}
	return world_event


func _open_world_event_prompt(world_event: Dictionary) -> void:
	var target_submap_id := String(world_event.get("target_submap_id", ""))
	var submap_entry := _get_mounted_submap_entry(target_submap_id)
	if submap_entry.is_empty():
		_update_status("未找到目标子地图 %s。" % target_submap_id)
		return
	var target_name := String(submap_entry.get("display_name", target_submap_id))
	var prompt_title := String(world_event.get("prompt_title", "进入子地图"))
	if prompt_title.is_empty():
		prompt_title = "进入 %s" % target_name
	var prompt_text := String(world_event.get("prompt_text", ""))
	if prompt_text.is_empty():
		prompt_text = "确认后将进入 %s，返回时会回到当前坐标。" % target_name
	_pending_submap_prompt = {
		"event_id": String(world_event.get("event_id", "")),
		"source_map_id": _active_map_id,
		"source_coord": _player_coord,
		"target_submap_id": target_submap_id,
		"target_display_name": target_name,
		"title": prompt_title,
		"description": prompt_text,
	}
	_active_modal_id = "submap_confirm"
	_update_status("已发现 %s，确认后可进入。" % String(world_event.get("display_name", target_name)))


func _confirm_pending_submap_entry() -> Dictionary:
	var prompt := _pending_submap_prompt.duplicate(true)
	if prompt.is_empty():
		return _command_error("当前没有待确认的子地图入口。")
	var result := _enter_submap(
		String(prompt.get("target_submap_id", "")),
		String(prompt.get("source_map_id", "")),
		prompt.get("source_coord", _player_coord)
	)
	if bool(result.get("ok", false)):
		_pending_submap_prompt.clear()
		_active_modal_id = ""
	return result


func _enter_submap(submap_id: String, source_map_id: String, source_coord: Vector2i) -> Dictionary:
	if submap_id.is_empty():
		return _command_error("子地图标识不能为空。")
	if not _ensure_submap_generated(submap_id):
		return _command_error("子地图生成失败。")
	var submap_entry := _get_mounted_submap_entry(submap_id)
	if submap_entry.is_empty():
		return _command_error("未找到目标子地图。")
	var return_stack: Array = _world_data.get("submap_return_stack", [])
	return_stack.append({
		"map_id": source_map_id,
		"coord": source_coord,
	})
	_world_data["submap_return_stack"] = return_stack
	_world_data["active_submap_id"] = submap_id
	_active_map_id = submap_id
	_active_world_data = submap_entry.get("world_data", {})
	_player_coord = submap_entry.get("player_coord", _active_world_data.get("player_start_coord", Vector2i.ZERO))
	_selected_coord = _player_coord
	_active_settlement_id = ""
	_active_settlement_feedback_text = ""
	_active_character_info_context.clear()
	_sync_active_world_context()
	var player_persist_error := int(_game_session.set_player_coord(_player_coord))
	var world_persist_error := int(_game_session.set_world_data(_world_data))
	var target_name := String(submap_entry.get("display_name", submap_id))
	if player_persist_error != OK or world_persist_error != OK:
		_update_status("已进入 %s，但世界状态持久化失败。" % target_name)
		return _command_error(_current_status_message)
	_update_status("已进入 %s。%s" % [target_name, get_submap_return_hint_text()])
	return _command_ok()


func _return_from_active_submap() -> Dictionary:
	if not is_submap_active():
		return _command_error("当前不在子地图中。")
	var submap_entry := _get_mounted_submap_entry(_active_map_id)
	if not submap_entry.is_empty():
		submap_entry["player_coord"] = _player_coord
		_set_mounted_submap_entry(_active_map_id, submap_entry)
	var return_stack: Array = _world_data.get("submap_return_stack", [])
	if return_stack.is_empty():
		return _command_error("当前没有可返回的原坐标。")
	var return_entry_variant = return_stack.pop_back()
	var return_entry: Dictionary = return_entry_variant if return_entry_variant is Dictionary else {}
	_world_data["submap_return_stack"] = return_stack
	_world_data["active_submap_id"] = String(return_entry.get("map_id", ""))
	_player_coord = return_entry.get("coord", Vector2i.ZERO)
	_selected_coord = _player_coord
	_active_settlement_id = ""
	_active_settlement_feedback_text = ""
	_active_character_info_context.clear()
	_pending_submap_prompt.clear()
	_active_modal_id = ""
	_sync_active_world_context()
	var player_persist_error := int(_game_session.set_player_coord(_player_coord))
	var world_persist_error := int(_game_session.set_world_data(_world_data))
	if player_persist_error != OK or world_persist_error != OK:
		_update_status("已返回原位置，但世界状态持久化失败。")
		return _command_error(_current_status_message)
	_update_status("已返回原位置 %s。" % _format_coord(_player_coord))
	return _command_ok()


func _ensure_submap_generated(submap_id: String) -> bool:
	var submap_entry := _get_mounted_submap_entry(submap_id)
	if submap_entry.is_empty():
		return false
	var current_world_data = submap_entry.get("world_data", {})
	if bool(submap_entry.get("is_generated", false)) and current_world_data is Dictionary and not current_world_data.is_empty():
		return true
	var submap_generation_config = _load_submap_generation_config(submap_id)
	if submap_generation_config == null:
		return false
	var generation_grid = WORLD_MAP_GRID_SYSTEM_SCRIPT.new()
	generation_grid.setup(submap_generation_config.world_size_in_chunks, submap_generation_config.chunk_size)
	var spawn_system = WORLD_MAP_SPAWN_SYSTEM_SCRIPT.new()
	var submap_world_data := spawn_system.build_world(submap_generation_config, generation_grid)
	submap_entry["world_data"] = submap_world_data
	submap_entry["player_coord"] = submap_world_data.get("player_start_coord", submap_generation_config.player_start_coord)
	submap_entry["is_generated"] = true
	_set_mounted_submap_entry(submap_id, submap_entry)
	return true


func _load_submap_generation_config(submap_id: String):
	if _submap_generation_configs.has(submap_id):
		return _submap_generation_configs.get(submap_id)
	var submap_entry := _get_mounted_submap_entry(submap_id)
	var generation_config_path := String(submap_entry.get("generation_config_path", ""))
	if generation_config_path.is_empty():
		return null
	var generation_config = load(generation_config_path)
	if generation_config != null:
		_submap_generation_configs[submap_id] = generation_config
	return generation_config


func _get_mounted_submap_entry(submap_id: String) -> Dictionary:
	var mounted_submaps_variant = _world_data.get("mounted_submaps", {})
	if mounted_submaps_variant is not Dictionary:
		return {}
	var mounted_submaps: Dictionary = mounted_submaps_variant
	var submap_entry_variant = mounted_submaps.get(submap_id, {})
	return submap_entry_variant if submap_entry_variant is Dictionary else {}


func _set_mounted_submap_entry(submap_id: String, submap_entry: Dictionary) -> void:
	var mounted_submaps_variant = _world_data.get("mounted_submaps", {})
	var mounted_submaps: Dictionary = mounted_submaps_variant if mounted_submaps_variant is Dictionary else {}
	mounted_submaps[submap_id] = submap_entry.duplicate(true)
	_world_data["mounted_submaps"] = mounted_submaps
