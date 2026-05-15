## 文件说明：该脚本集中维护待领取角色奖励 entry_type 的静态白名单与目标类型判定。
## 审查重点：新增 pending reward 类型时，需要同步确认内容校验、入队构造与领取结算都具备明确语义。
## 备注：这里是 Quest、Achievement、Faith 与 runtime pending reward 入口共享的唯一 entry_type 白名单来源。

class_name PendingCharacterRewardContentRules
extends RefCounted

const ATTRIBUTE_GROWTH_CONTENT_RULES = preload("res://scripts/player/progression/attribute_growth_content_rules.gd")

const ENTRY_KNOWLEDGE_UNLOCK: StringName = &"knowledge_unlock"
const ENTRY_SKILL_UNLOCK: StringName = &"skill_unlock"
const ENTRY_SKILL_MASTERY: StringName = &"skill_mastery"
const ENTRY_ATTRIBUTE_DELTA: StringName = &"attribute_delta"
const ENTRY_ATTRIBUTE_PROGRESS: StringName = &"attribute_progress"

const SUPPORTED_ENTRY_TYPES := {
	ENTRY_KNOWLEDGE_UNLOCK: true,
	ENTRY_SKILL_UNLOCK: true,
	ENTRY_SKILL_MASTERY: true,
	ENTRY_ATTRIBUTE_DELTA: true,
	ENTRY_ATTRIBUTE_PROGRESS: true,
}

const SKILL_TARGET_ENTRY_TYPES := {
	ENTRY_SKILL_UNLOCK: true,
	ENTRY_SKILL_MASTERY: true,
}


static func normalize_string_name(value: Variant) -> StringName:
	if value is StringName:
		return value
	if value is String:
		var text := (value as String).strip_edges()
		if text.is_empty():
			return &""
		return StringName(text)
	return &""


static func is_supported_entry_type(value: Variant) -> bool:
	return SUPPORTED_ENTRY_TYPES.has(normalize_string_name(value))


static func requires_skill_target(value: Variant) -> bool:
	return SKILL_TARGET_ENTRY_TYPES.has(normalize_string_name(value))


static func is_attribute_progress_entry(value: Variant) -> bool:
	return normalize_string_name(value) == ENTRY_ATTRIBUTE_PROGRESS


static func is_attribute_delta_entry(value: Variant) -> bool:
	return normalize_string_name(value) == ENTRY_ATTRIBUTE_DELTA


static func is_valid_attribute_progress_target(value: Variant) -> bool:
	return ATTRIBUTE_GROWTH_CONTENT_RULES.is_valid_attribute_id(normalize_string_name(value))


static func valid_entry_type_label() -> String:
	var labels: Array[String] = []
	for entry_type in SUPPORTED_ENTRY_TYPES.keys():
		labels.append(String(entry_type))
	labels.sort()
	return ", ".join(labels)
