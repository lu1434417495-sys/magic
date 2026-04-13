## 文件说明：该脚本属于待处理熟练度奖励相关的业务脚本，集中维护来源类型、来源标签、摘要文本等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name PendingMasteryReward
extends RefCounted

const PENDING_MASTERY_REWARD_ENTRY_SCRIPT = preload("res://scripts/systems/pending_mastery_reward_entry.gd")
const PendingMasteryRewardEntry = PENDING_MASTERY_REWARD_ENTRY_SCRIPT

## 字段说明：记录来源类型，用于区分不同规则、资源类别或行为分支。
var source_type: StringName = &""
## 字段说明：记录来源标签，会参与运行时状态流转、系统协作和存档恢复。
var source_label := ""
## 字段说明：记录摘要文本，会参与运行时状态流转、系统协作和存档恢复。
var summary_text := ""
## 字段说明：记录成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var member_id: StringName = &""
## 字段说明：记录成员名称，会参与运行时状态流转、系统协作和存档恢复。
var member_name := ""
## 字段说明：保存条目列表，便于顺序遍历、批量展示、批量运算和整体重建。
var entries: Array[PendingMasteryRewardEntry] = []


func is_empty() -> bool:
	if member_id == &"" or entries.is_empty():
		return true
	for entry in entries:
		if entry != null and not entry.is_empty():
			return false
	return true
