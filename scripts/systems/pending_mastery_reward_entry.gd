## 文件说明：该脚本属于待处理熟练度奖励条目相关的业务脚本，集中维护技能唯一标识、技能名称、熟练度数量等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name PendingMasteryRewardEntry
extends RefCounted

## 字段说明：记录技能唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var skill_id: StringName = &""
## 字段说明：记录技能名称，会参与运行时状态流转、系统协作和存档恢复。
var skill_name := ""
## 字段说明：记录熟练度数量，会参与运行时状态流转、系统协作和存档恢复。
var mastery_amount := 0
## 字段说明：记录原因文本，会参与运行时状态流转、系统协作和存档恢复。
var reason_text := ""


func is_empty() -> bool:
	return skill_id == &"" or mastery_amount <= 0
