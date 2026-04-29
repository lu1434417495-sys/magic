## 文件说明：该脚本属于战斗事件批次相关的业务脚本，集中维护阶段已变化、战斗结束、已变化单位标识列表等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleEventBatch
extends RefCounted

const CharacterProgressionDelta = preload("res://scripts/systems/progression/character_progression_delta.gd")

## 字段说明：用于标记阶段已变化当前是否成立或生效，供脚本后续分支判断使用，会参与运行时状态流转、系统协作和存档恢复。
var phase_changed := false
## 字段说明：用于标记战斗结束当前是否成立或生效，供脚本后续分支判断使用，会参与运行时状态流转、系统协作和存档恢复。
var battle_ended := false
## 字段说明：保存已变化单位标识列表，便于批量遍历、交叉查找和界面展示。
var changed_unit_ids: Array[StringName] = []
## 字段说明：保存已变化坐标列表，供范围判定、占位刷新、批量渲染或目标选择复用。
var changed_coords: Array[Vector2i] = []
## 字段说明：保存日志文本行，便于顺序遍历、批量展示、批量运算和整体重建。
var log_lines: Array[String] = []
## 字段说明：保存结构化战报条目，供 UI / headless / 剧情系统订阅稳定字段而不依赖自由文本解析。
var report_entries: Array[Dictionary] = []
## 字段说明：保存成长增量列表，便于顺序遍历、批量展示、批量运算和整体重建。
var progression_deltas: Array[CharacterProgressionDelta] = []
## 字段说明：用于标记模态相关当前是否成立或生效，供脚本后续分支判断使用，会参与运行时状态流转、系统协作和存档恢复。
var modal_requested := false


func clear() -> void:
	phase_changed = false
	battle_ended = false
	changed_unit_ids.clear()
	changed_coords.clear()
	log_lines.clear()
	report_entries.clear()
	progression_deltas.clear()
	modal_requested = false
