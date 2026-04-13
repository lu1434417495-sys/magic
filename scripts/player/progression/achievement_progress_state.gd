## 文件说明：该脚本属于成就进度集合状态相关的状态数据脚本，集中维护成就唯一标识、当前数值、是否已解锁等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name AchievementProgressState
extends RefCounted

const ACHIEVEMENT_PROGRESS_STATE_SCRIPT = preload("res://scripts/player/progression/achievement_progress_state.gd")

## 字段说明：记录成就唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var achievement_id: StringName = &""
## 字段说明：记录当前数值，作为当前计算、显示或结算时使用的核心数值。
var current_value := 0
## 字段说明：用于标记当前是否处于已解锁状态，避免在不合适的时机重复触发流程，会参与成长规则判定、序列化和界面展示。
var is_unlocked := false
## 字段说明：记录已解锁时间戳时间，会参与成长规则判定、序列化和界面展示。
var unlocked_at_unix_time := 0


func to_dict() -> Dictionary:
	return {
		"achievement_id": String(achievement_id),
		"current_value": current_value,
		"is_unlocked": is_unlocked,
		"unlocked_at_unix_time": unlocked_at_unix_time,
	}


static func from_dict(data: Dictionary):
	var state = ACHIEVEMENT_PROGRESS_STATE_SCRIPT.new()
	state.achievement_id = ProgressionDataUtils.to_string_name(data.get("achievement_id", ""))
	state.current_value = int(data.get("current_value", 0))
	state.is_unlocked = bool(data.get("is_unlocked", false))
	state.unlocked_at_unix_time = int(data.get("unlocked_at_unix_time", 0))
	return state
