## 文件说明：该脚本属于单位职业进度相关的业务脚本，集中维护职业唯一标识、阶位、是否激活等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name UnitProfessionProgress
extends RefCounted

const UNIT_PROFESSION_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_profession_progress.gd")

## 字段说明：记录职业唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var profession_id: StringName = &""
## 字段说明：记录阶位，会参与成长规则判定、序列化和界面展示。
var rank := 0
## 字段说明：用于标记当前是否处于激活状态，避免在不合适的时机重复触发流程，会参与成长规则判定、序列化和界面展示。
var is_active := true
## 字段说明：用于标记当前是否处于隐藏状态，避免在不合适的时机重复触发流程，会参与成长规则判定、序列化和界面展示。
var is_hidden := false
## 字段说明：保存核心技能标识列表，便于批量遍历、交叉查找和界面展示。
var core_skill_ids: Array[StringName] = []
## 字段说明：保存授予技能标识列表，便于批量遍历、交叉查找和界面展示。
var granted_skill_ids: Array[StringName] = []
## 字段说明：保存晋升历史，便于顺序遍历、批量展示、批量运算和整体重建。
var promotion_history: Array[ProfessionPromotionRecord] = []
## 字段说明：记录未激活原因，会参与成长规则判定、序列化和界面展示。
var inactive_reason: StringName = &""


func add_core_skill(skill_id: StringName) -> void:
	if core_skill_ids.has(skill_id):
		return
	core_skill_ids.append(skill_id)


func remove_core_skill(skill_id: StringName) -> void:
	core_skill_ids.erase(skill_id)


func add_granted_skill(skill_id: StringName) -> void:
	if granted_skill_ids.has(skill_id):
		return
	granted_skill_ids.append(skill_id)


func add_promotion_record(record: ProfessionPromotionRecord) -> void:
	if record == null:
		return
	promotion_history.append(record)


func to_dict() -> Dictionary:
	var promotion_history_data: Array[Dictionary] = []
	for record in promotion_history:
		if record != null:
			promotion_history_data.append(record.to_dict())

	return {
		"profession_id": String(profession_id),
		"rank": rank,
		"is_active": is_active,
		"is_hidden": is_hidden,
		"core_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(core_skill_ids),
		"granted_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(granted_skill_ids),
		"promotion_history": promotion_history_data,
		"inactive_reason": String(inactive_reason),
	}


static func from_dict(data: Dictionary):
	for field_name in [
		"profession_id",
		"rank",
		"is_active",
		"is_hidden",
		"core_skill_ids",
		"granted_skill_ids",
		"promotion_history",
		"inactive_reason",
	]:
		if not data.has(field_name):
			return null
	var core_skill_ids_variant: Variant = data["core_skill_ids"]
	var granted_skill_ids_variant: Variant = data["granted_skill_ids"]
	var promotion_history_data: Variant = data["promotion_history"]
	if core_skill_ids_variant is not Array:
		return null
	if granted_skill_ids_variant is not Array:
		return null
	if promotion_history_data is not Array:
		return null
	var profession_id = _parse_string_name_field(data["profession_id"], false)
	if profession_id == null:
		return null
	var rank_variant: Variant = data["rank"]
	if rank_variant is not int or int(rank_variant) < 0:
		return null
	if data["is_active"] is not bool or data["is_hidden"] is not bool:
		return null
	var core_skill_ids = _parse_unique_string_name_array(core_skill_ids_variant)
	if core_skill_ids == null:
		return null
	var granted_skill_ids = _parse_unique_string_name_array(granted_skill_ids_variant)
	if granted_skill_ids == null:
		return null
	var inactive_reason = _parse_string_name_field(data["inactive_reason"], true)
	if inactive_reason == null:
		return null

	var progress := UNIT_PROFESSION_PROGRESS_SCRIPT.new()
	progress.profession_id = profession_id
	progress.rank = int(rank_variant)
	progress.is_active = data["is_active"]
	progress.is_hidden = data["is_hidden"]
	progress.core_skill_ids = core_skill_ids
	progress.granted_skill_ids = granted_skill_ids
	progress.inactive_reason = inactive_reason

	for record_data in promotion_history_data:
		if record_data is not Dictionary:
			return null
		var promotion_record := ProfessionPromotionRecord.from_dict(record_data)
		if promotion_record == null:
			return null
		progress.promotion_history.append(promotion_record)

	return progress


static func _parse_string_name_field(value: Variant, allow_empty: bool):
	var value_type := typeof(value)
	if value_type != TYPE_STRING and value_type != TYPE_STRING_NAME:
		return null
	var parsed_value := ProgressionDataUtils.to_string_name(value)
	if parsed_value == &"" and not allow_empty:
		return null
	return parsed_value


static func _parse_unique_string_name_array(values: Array):
	var parsed_values: Array[StringName] = []
	var seen_values: Dictionary = {}
	for raw_value in values:
		var parsed_value = _parse_string_name_field(raw_value, false)
		if parsed_value == null or seen_values.has(parsed_value):
			return null
		seen_values[parsed_value] = true
		parsed_values.append(parsed_value)
	return parsed_values
