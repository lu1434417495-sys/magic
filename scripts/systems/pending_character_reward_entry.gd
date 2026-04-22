## 文件说明：该脚本属于待处理角色奖励条目相关的业务脚本，集中维护条目类型、目标唯一标识、目标标签等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name PendingCharacterRewardEntry
extends RefCounted

const PENDING_CHARACTER_REWARD_ENTRY_SCRIPT = preload("res://scripts/systems/pending_character_reward_entry.gd")
const SKILL_MASTERY_ENTRY_TYPE: StringName = &"skill_mastery"

## 字段说明：记录条目类型，用于区分不同规则、资源类别或行为分支。
var entry_type: StringName = &""
## 字段说明：记录目标唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var target_id: StringName = &""
## 字段说明：记录目标标签，会参与运行时状态流转、系统协作和存档恢复。
var target_label := ""
## 字段说明：记录数量，会参与运行时状态流转、系统协作和存档恢复。
var amount := 0
## 字段说明：记录原因文本，会参与运行时状态流转、系统协作和存档恢复。
var reason_text := ""


func is_empty() -> bool:
	return entry_type == &"" or target_id == &"" or amount == 0


func to_dict() -> Dictionary:
	return {
		"entry_type": String(entry_type),
		"target_id": String(target_id),
		"target_label": target_label,
		"amount": amount,
		"reason_text": reason_text,
	}


static func from_dict(data: Dictionary):
	var entry_type := ProgressionDataUtils.to_string_name(data.get("entry_type", ""))
	var target_id := ProgressionDataUtils.to_string_name(data.get("target_id", ""))
	var amount := int(data.get("amount", 0))
	if entry_type == &"" or target_id == &"" or amount == 0:
		return null

	var entry = PENDING_CHARACTER_REWARD_ENTRY_SCRIPT.new()
	entry.entry_type = entry_type
	entry.target_id = target_id
	entry.target_label = String(data.get("target_label", ""))
	entry.amount = amount
	entry.reason_text = String(data.get("reason_text", ""))
	return entry


static func from_variant(entry_variant):
	if entry_variant == null:
		return null
	if entry_variant is PendingCharacterRewardEntry:
		return from_dict((entry_variant as PendingCharacterRewardEntry).to_dict())
	if entry_variant is Dictionary:
		return from_dict(entry_variant)
	return null
