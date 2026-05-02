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
	for field_name in [
		"new_rank",
		"consumed_skill_ids",
		"qualifier_skill_ids",
		"snapshot_unit_base_attributes",
		"timestamp",
	]:
		if not data.has(field_name):
			return null
	var consumed_skill_ids_variant: Variant = data["consumed_skill_ids"]
	var qualifier_skill_ids_variant: Variant = data["qualifier_skill_ids"]
	var snapshot_unit_base_attributes_variant: Variant = data["snapshot_unit_base_attributes"]
	if consumed_skill_ids_variant is not Array:
		return null
	if qualifier_skill_ids_variant is not Array:
		return null
	if snapshot_unit_base_attributes_variant is not Dictionary:
		return null
	var new_rank_variant: Variant = data["new_rank"]
	if new_rank_variant is not int or int(new_rank_variant) < 0:
		return null
	var consumed_skill_ids = _parse_unique_string_name_array(consumed_skill_ids_variant)
	if consumed_skill_ids == null:
		return null
	var qualifier_skill_ids = _parse_unique_string_name_array(qualifier_skill_ids_variant)
	if qualifier_skill_ids == null:
		return null
	var timestamp_variant: Variant = data["timestamp"]
	if timestamp_variant is not int or int(timestamp_variant) < 0:
		return null

	var record := ProfessionPromotionRecord.new()
	record.new_rank = int(new_rank_variant)
	record.consumed_skill_ids = consumed_skill_ids
	record.qualifier_skill_ids = qualifier_skill_ids
	record.snapshot_unit_base_attributes = snapshot_unit_base_attributes_variant.duplicate(true)
	record.timestamp = int(timestamp_variant)
	return record


static func _parse_string_name_field(value: Variant):
	var value_type := typeof(value)
	if value_type != TYPE_STRING and value_type != TYPE_STRING_NAME:
		return null
	var parsed_value := ProgressionDataUtils.to_string_name(value)
	if parsed_value == &"":
		return null
	return parsed_value


static func _parse_unique_string_name_array(values: Array):
	var parsed_values: Array[StringName] = []
	var seen_values: Dictionary = {}
	for raw_value in values:
		var parsed_value = _parse_string_name_field(raw_value)
		if parsed_value == null or seen_values.has(parsed_value):
			return null
		seen_values[parsed_value] = true
		parsed_values.append(parsed_value)
	return parsed_values
