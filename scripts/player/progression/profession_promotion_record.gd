## 文件说明：该脚本属于职业晋升记录相关的业务脚本，集中维护新阶位、已消耗技能标识列表、资格技能标识列表等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name ProfessionPromotionRecord
extends RefCounted

## 字段说明：记录新阶位，会参与成长规则判定、序列化和界面展示。
var new_rank := 0
## 字段说明：保存已消耗技能标识列表，便于批量遍历、交叉查找和界面展示。
var consumed_skill_ids: Array[StringName] = []
## 字段说明：保存资格技能标识列表，便于批量遍历、交叉查找和界面展示。
var qualifier_skill_ids: Array[StringName] = []
## 字段说明：缓存快照中的单位基础属性字典，集中保存可按键查询的运行时数据。
var snapshot_unit_base_attributes: Dictionary = {}
## 字段说明：记录时间戳，会参与成长规则判定、序列化和界面展示。
var timestamp := 0


func to_dict() -> Dictionary:
	return {
		"new_rank": new_rank,
		"consumed_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(consumed_skill_ids),
		"qualifier_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(qualifier_skill_ids),
		"snapshot_unit_base_attributes": snapshot_unit_base_attributes.duplicate(true),
		"timestamp": timestamp,
	}


static func from_dict(data: Dictionary) -> ProfessionPromotionRecord:
	var record := ProfessionPromotionRecord.new()
	record.new_rank = int(data.get("new_rank", 0))
	record.consumed_skill_ids = ProgressionDataUtils.to_string_name_array(data.get("consumed_skill_ids", []))
	record.qualifier_skill_ids = ProgressionDataUtils.to_string_name_array(data.get("qualifier_skill_ids", []))
	record.snapshot_unit_base_attributes = data.get("snapshot_unit_base_attributes", {}).duplicate(true)
	record.timestamp = int(data.get("timestamp", 0))
	return record
