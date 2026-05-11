## 文件说明：该脚本属于满级技能属性进度结算服务，负责累计基础属性进度并在 20 点前转化为属性点。
## 审查重点：属性 id 只能是六项基础属性；20 点后进度继续累计但不得自动转化。

class_name AttributeGrowthService
extends RefCounted

const ATTRIBUTE_GROWTH_CONTENT_RULES = preload("res://scripts/player/progression/attribute_growth_content_rules.gd")

const ATTRIBUTE_PROGRESS_THRESHOLD := 100
const BASE_ATTRIBUTE_PROGRESS_CONVERSION_CAP := 20

const VALID_GROWTH_TIERS := ATTRIBUTE_GROWTH_CONTENT_RULES.VALID_GROWTH_TIERS

var _unit_progress: UnitProgress = null


func setup(unit_progress: UnitProgress) -> void:
	_unit_progress = unit_progress


static func get_tier_budget(growth_tier: StringName) -> int:
	return ATTRIBUTE_GROWTH_CONTENT_RULES.get_tier_budget(growth_tier)


static func is_valid_growth_tier(growth_tier: StringName) -> bool:
	return ATTRIBUTE_GROWTH_CONTENT_RULES.is_valid_growth_tier(growth_tier)


static func is_valid_attribute_id(attribute_id: StringName) -> bool:
	return ATTRIBUTE_GROWTH_CONTENT_RULES.is_valid_attribute_id(attribute_id)


func apply_attribute_progress(attribute_id: StringName, amount: int, reason_text: String = "") -> Dictionary:
	var result := {
		"attribute_id": attribute_id,
		"progress_delta": 0,
		"progress_before": 0,
		"progress_after": 0,
		"attribute_before": 0,
		"attribute_after": 0,
		"attribute_delta": 0,
		"reason_text": reason_text,
		"applied": false,
	}
	if _unit_progress == null or _unit_progress.unit_base_attributes == null:
		return result
	if not is_valid_attribute_id(attribute_id) or amount <= 0:
		return result

	var before_progress := int(_unit_progress.attribute_growth_progress.get(attribute_id, 0))
	var before_attribute := int(_unit_progress.unit_base_attributes.get_attribute_value(attribute_id))
	var next_progress := before_progress + amount
	var next_attribute := before_attribute

	while next_attribute < BASE_ATTRIBUTE_PROGRESS_CONVERSION_CAP and next_progress >= ATTRIBUTE_PROGRESS_THRESHOLD:
		next_attribute += 1
		next_progress -= ATTRIBUTE_PROGRESS_THRESHOLD

	_unit_progress.attribute_growth_progress[attribute_id] = next_progress
	_unit_progress.unit_base_attributes.set_attribute_value(attribute_id, next_attribute)

	result["progress_delta"] = amount
	result["progress_before"] = before_progress
	result["progress_after"] = next_progress
	result["attribute_before"] = before_attribute
	result["attribute_after"] = next_attribute
	result["attribute_delta"] = next_attribute - before_attribute
	result["applied"] = true
	return result
