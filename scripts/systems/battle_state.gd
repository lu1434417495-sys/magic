## 文件说明：该脚本属于战斗状态相关的状态数据脚本，集中维护战斗唯一标识、随机种子、阶段等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleState
extends RefCounted

const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle_timeline_state.gd")

## 字段说明：记录战斗唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var battle_id: StringName = &""
## 字段说明：记录随机种子，会参与运行时状态流转、系统协作和存档恢复。
var seed := 0
## 字段说明：记录阶段，会参与运行时状态流转、系统协作和存档恢复。
var phase: StringName = &"timeline_running"
## 字段说明：记录地图尺寸，用于布局、碰撞、绘制或程序化生成时的尺寸计算。
var map_size: Vector2i = Vector2i.ZERO
## 字段说明：记录对象在世界地图中的坐标，供探索定位、遭遇生成和存档恢复复用。
var world_coord: Vector2i = Vector2i.ZERO
## 字段说明：记录遭遇锚点唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var encounter_anchor_id: StringName = &""
## 字段说明：记录地形配置档唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var terrain_profile_id: StringName = &"default"
## 字段说明：缓存格子集合字典，集中保存可按键查询的运行时数据。
var cells: Dictionary = {}
## 字段说明：缓存同一 (x, y) 下真实堆叠的格子列集合，作为战场垂直结构的 source-of-truth。
var cell_columns: Dictionary = {}
## 字段说明：缓存单位集合字典，集中保存可按键查询的运行时数据。
var units: Dictionary = {}
## 字段说明：保存友方单位标识列表，便于批量遍历、交叉查找和界面展示。
var ally_unit_ids: Array[StringName] = []
## 字段说明：保存敌方单位标识列表，便于批量遍历、交叉查找和界面展示。
var enemy_unit_ids: Array[StringName] = []
## 字段说明：记录时间轴，会参与运行时状态流转、系统协作和存档恢复。
var timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
## 字段说明：记录激活单位唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var active_unit_id: StringName = &""
## 字段说明：记录胜利方阵营唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var winner_faction_id: StringName = &""
## 字段说明：保存日志条目列表，便于顺序遍历、批量展示、批量运算和整体重建。
var log_entries: Array[String] = []
## 字段说明：缓存晋升队列字典，集中保存可按键查询的运行时数据。
var promotion_queue: Array[Dictionary] = []
## 字段说明：记录模态状态，会参与运行时状态流转、系统协作和存档恢复。
var modal_state: StringName = &""
## 字段说明：缓存运行时边缘面集合，由边服务按需重建，不直接参与存档恢复。
var runtime_edge_faces: Dictionary = {}
## 字段说明：用于标记运行时边缓存是否脏，供边服务延迟重建和跨系统共享。
var runtime_edges_dirty := true


func is_empty() -> bool:
	return battle_id == &"" and cells.is_empty() and units.is_empty() and ally_unit_ids.is_empty() and enemy_unit_ids.is_empty()


func mark_runtime_edges_dirty() -> void:
	runtime_edges_dirty = true


func clear_runtime_edge_faces() -> void:
	runtime_edge_faces.clear()
	runtime_edges_dirty = true
