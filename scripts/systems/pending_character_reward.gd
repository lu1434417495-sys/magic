## 文件说明：该脚本属于待处理角色奖励相关的业务脚本，集中维护奖励对象唯一标识、成员唯一标识、成员名称等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name PendingCharacterReward
extends RefCounted

const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/pending_character_reward.gd")
const PENDING_CHARACTER_REWARD_ENTRY_SCRIPT = preload("res://scripts/systems/pending_character_reward_entry.gd")
const PendingCharacterRewardEntry = PENDING_CHARACTER_REWARD_ENTRY_SCRIPT

## 字段说明：记录奖励对象唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var reward_id: StringName = &""
## 字段说明：记录成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var member_id: StringName = &""
## 字段说明：记录成员名称，会参与运行时状态流转、系统协作和存档恢复。
var member_name := ""
## 字段说明：记录来源类型，用于区分不同规则、资源类别或行为分支。
var source_type: StringName = &""
## 字段说明：记录来源唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var source_id: StringName = &""
## 字段说明：记录来源标签，会参与运行时状态流转、系统协作和存档恢复。
var source_label := ""
## 字段说明：记录摘要文本，会参与运行时状态流转、系统协作和存档恢复。
var summary_text := ""
## 字段说明：保存条目列表，便于顺序遍历、批量展示、批量运算和整体重建。
var entries: Array[PendingCharacterRewardEntry] = []


func is_empty() -> bool:
	if member_id == &"" or entries.is_empty():
		return true
	for entry in entries:
		if entry != null and not entry.is_empty():
			return false
	return true


func to_dict() -> Dictionary:
	var entry_data: Array[Dictionary] = []
	for entry in entries:
		if entry == null:
			continue
		entry_data.append(entry.to_dict())

	return {
		"reward_id": String(reward_id),
		"member_id": String(member_id),
		"member_name": member_name,
		"source_type": String(source_type),
		"source_id": String(source_id),
		"source_label": source_label,
		"summary_text": summary_text,
		"entries": entry_data,
	}


static func from_dict(data: Dictionary):
	var member_id := ProgressionDataUtils.to_string_name(data.get("member_id", ""))
	if member_id == &"":
		return null

	var entries_variant: Variant = data.get("entries", [])
	if entries_variant is not Array:
		return null

	var parsed_entries: Array[PendingCharacterRewardEntry] = []
	for entry_data in entries_variant:
		if entry_data is not Dictionary:
			continue
		var parsed_entry = PENDING_CHARACTER_REWARD_ENTRY_SCRIPT.from_dict(entry_data)
		if parsed_entry == null:
			continue
		parsed_entries.append(parsed_entry)

	if parsed_entries.is_empty():
		return null

	var reward = PENDING_CHARACTER_REWARD_SCRIPT.new()
	reward.reward_id = ProgressionDataUtils.to_string_name(data.get("reward_id", ""))
	reward.member_id = member_id
	reward.member_name = String(data.get("member_name", ""))
	reward.source_type = ProgressionDataUtils.to_string_name(data.get("source_type", ""))
	reward.source_id = ProgressionDataUtils.to_string_name(data.get("source_id", ""))
	reward.source_label = String(data.get("source_label", ""))
	reward.summary_text = String(data.get("summary_text", ""))
	reward.entries = parsed_entries
	return reward


static func from_variant(reward_variant):
	if reward_variant == null:
		return null
	if reward_variant is PendingCharacterReward:
		return from_dict((reward_variant as PendingCharacterReward).to_dict())
	if reward_variant is Dictionary:
		return from_dict(reward_variant)
	return null
