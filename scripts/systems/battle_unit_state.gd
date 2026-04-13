## 文件说明：该脚本属于战斗单位状态相关的状态数据脚本，集中维护单位唯一标识、来源成员唯一标识、显示名称等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleUnitState
extends RefCounted

const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle_unit_state.gd")
const AttributeSnapshot = preload("res://scripts/player/progression/attribute_snapshot.gd")

## 字段说明：记录单位唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var unit_id: StringName = &""
## 字段说明：记录来源成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var source_member_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
var display_name: String = ""
## 字段说明：记录阵营唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var faction_id: StringName = &""
## 字段说明：记录控制模式，用于在不同处理分支之间切换规则或交互方式。
var control_mode: StringName = &"manual"
## 字段说明：记录单位绑定的 AI brain 标识，供战斗运行时选择状态机与 action 集合。
var ai_brain_id: StringName = &""
## 字段说明：记录单位当前 AI 状态标识，供同一场战斗内持续保留战术状态。
var ai_state_id: StringName = &""
## 字段说明：缓存 AI 临时黑板字典，供单场战斗内的决策链路共享运行时上下文。
var ai_blackboard: Dictionary = {}
## 字段说明：记录对象当前使用的网格坐标，供绘制、寻路或占位计算使用。
var coord: Vector2i = Vector2i.ZERO
## 字段说明：记录体型尺寸，用于布局、碰撞、绘制或程序化生成时的尺寸计算。
var body_size := 1
## 字段说明：记录占位尺寸，用于布局、碰撞、绘制或程序化生成时的尺寸计算。
var footprint_size: Vector2i = Vector2i.ONE
## 字段说明：保存占用坐标列表，供范围判定、占位刷新、批量渲染或目标选择复用。
var occupied_coords: Array[Vector2i] = []
## 字段说明：用于标记当前是否处于存活状态，避免在不合适的时机重复触发流程，会参与运行时状态流转、系统协作和存档恢复。
var is_alive := true
## 字段说明：缓存属性快照实例，会参与运行时状态流转、系统协作和存档恢复。
var attribute_snapshot: AttributeSnapshot = AttributeSnapshot.new()
## 字段说明：记录当前生命值，会参与运行时状态流转、系统协作和存档恢复。
var current_hp := 0
## 字段说明：记录当前法力值，会参与运行时状态流转、系统协作和存档恢复。
var current_mp := 0
## 字段说明：记录当前体力值，会参与运行时状态流转、系统协作和存档恢复。
var current_stamina := 0
## 字段说明：记录当前斗气值，会参与运行时状态流转、系统协作和存档恢复。
var current_aura := 0
## 字段说明：记录当前行动点，会参与运行时状态流转、系统协作和存档恢复。
var current_ap := 0
## 字段说明：记录当前回合免费移动额度，用于承接击杀刷新等临时机动收益。
var current_free_move_points := 0
## 字段说明：保存行动进度，便于顺序遍历、批量展示、批量运算和整体重建。
var action_progress := 0
## 字段说明：保存已知激活技能标识列表，便于批量遍历、交叉查找和界面展示。
var known_active_skill_ids: Array[StringName] = []
## 字段说明：按键缓存已知技能等级映射表，便于在较多对象中快速定位目标并减少重复遍历。
var known_skill_level_map: Dictionary = {}
## 字段说明：缓存冷却表字典，集中保存可按键查询的运行时数据。
var cooldowns: Dictionary = {}
## 字段说明：缓存状态效果集合字典，集中保存可按键查询的运行时数据。
var status_effects: Dictionary = {}
## 字段说明：缓存连击态字典，集中保存可按键查询的运行时数据。
var combo_state: Dictionary = {}


func _init() -> void:
	refresh_footprint()


func set_anchor_coord(anchor_coord: Vector2i) -> void:
	coord = anchor_coord
	refresh_footprint()


func refresh_footprint() -> void:
	footprint_size = get_footprint_size_for_body_size(body_size)
	occupied_coords = []
	for y in range(footprint_size.y):
		for x in range(footprint_size.x):
			occupied_coords.append(coord + Vector2i(x, y))


func occupies_coord(target_coord: Vector2i) -> bool:
	return occupied_coords.has(target_coord)


static func get_footprint_size_for_body_size(size_value: int) -> Vector2i:
	return Vector2i(2, 2) if maxi(size_value, 1) >= 3 else Vector2i.ONE


func to_dict() -> Dictionary:
	refresh_footprint()
	return {
		"unit_id": String(unit_id),
		"source_member_id": String(source_member_id),
		"display_name": display_name,
		"faction_id": String(faction_id),
		"control_mode": String(control_mode),
		"ai_brain_id": String(ai_brain_id),
		"ai_state_id": String(ai_state_id),
		"ai_blackboard": ai_blackboard.duplicate(true),
		"coord": coord,
		"body_size": body_size,
		"footprint_size": footprint_size,
		"occupied_coords": occupied_coords.duplicate(),
		"is_alive": is_alive,
		"attribute_snapshot": attribute_snapshot.to_dict() if attribute_snapshot != null else {},
		"current_hp": current_hp,
		"current_mp": current_mp,
		"current_stamina": current_stamina,
		"current_aura": current_aura,
		"current_ap": current_ap,
		"current_free_move_points": current_free_move_points,
		"action_progress": action_progress,
		"known_active_skill_ids": _string_name_array_to_strings(known_active_skill_ids),
		"known_skill_level_map": ProgressionDataUtils.string_name_int_map_to_string_dict(known_skill_level_map),
		"cooldowns": cooldowns.duplicate(true),
		"status_effects": status_effects.duplicate(true),
		"combo_state": combo_state.duplicate(true),
	}


static func from_dict(data: Dictionary):
	var unit_state = BATTLE_UNIT_STATE_SCRIPT.new()
	unit_state.unit_id = StringName(String(data.get("unit_id", "")))
	unit_state.source_member_id = StringName(String(data.get("source_member_id", "")))
	unit_state.display_name = String(data.get("display_name", ""))
	unit_state.faction_id = StringName(String(data.get("faction_id", "")))
	unit_state.control_mode = StringName(String(data.get("control_mode", "manual")))
	unit_state.ai_brain_id = StringName(String(data.get("ai_brain_id", "")))
	unit_state.ai_state_id = StringName(String(data.get("ai_state_id", "")))
	unit_state.ai_blackboard = data.get("ai_blackboard", {}).duplicate(true)
	unit_state.coord = data.get("coord", Vector2i.ZERO)
	unit_state.body_size = maxi(int(data.get("body_size", 1)), 1)
	unit_state.is_alive = bool(data.get("is_alive", true))
	unit_state.attribute_snapshot = _attribute_snapshot_from_dict(data.get("attribute_snapshot", {}))
	unit_state.current_hp = int(data.get("current_hp", 0))
	unit_state.current_mp = int(data.get("current_mp", 0))
	unit_state.current_stamina = int(data.get("current_stamina", 0))
	unit_state.current_aura = int(data.get("current_aura", 0))
	unit_state.current_ap = int(data.get("current_ap", 0))
	unit_state.current_free_move_points = int(data.get("current_free_move_points", 0))
	unit_state.action_progress = int(data.get("action_progress", 0))
	unit_state.known_active_skill_ids = _strings_to_string_name_array(data.get("known_active_skill_ids", []))
	unit_state.known_skill_level_map = ProgressionDataUtils.to_string_name_int_map(data.get("known_skill_level_map", {}))
	unit_state.cooldowns = data.get("cooldowns", {}).duplicate(true)
	unit_state.status_effects = data.get("status_effects", {}).duplicate(true)
	unit_state.combo_state = data.get("combo_state", {}).duplicate(true)
	unit_state.refresh_footprint()
	return unit_state


static func _attribute_snapshot_from_dict(data: Variant) -> AttributeSnapshot:
	var snapshot = AttributeSnapshot.new()
	if data is Dictionary:
		for key in data.keys():
			snapshot.set_value(StringName(String(key)), int(data[key]))
	return snapshot


static func _string_name_array_to_strings(values: Array[StringName]) -> Array[String]:
	var results: Array[String] = []
	for value in values:
		results.append(String(value))
	return results


static func _strings_to_string_name_array(values: Variant) -> Array[StringName]:
	var results: Array[StringName] = []
	if values is Array:
		for value in values:
			results.append(StringName(String(value)))
	return results
