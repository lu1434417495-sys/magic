## 文件说明：该脚本集中维护伤害类型、物理伤害类型、抗性档位与伤害分类的静态白名单。
## 审查重点：新增伤害类型时需要同步确认技能内容、抗性内容与战斗伤害解析是否都具备明确语义。
## 备注：这里是内容校验和 runtime 解析共享的唯一 damage_tag 白名单来源。

class_name DamageTagContentRules
extends RefCounted

const DAMAGE_TAG_PHYSICAL_SLASH: StringName = &"physical_slash"
const DAMAGE_TAG_PHYSICAL_PIERCE: StringName = &"physical_pierce"
const DAMAGE_TAG_PHYSICAL_BLUNT: StringName = &"physical_blunt"

const VALID_DAMAGE_TAGS := {
	DAMAGE_TAG_PHYSICAL_SLASH: true,
	DAMAGE_TAG_PHYSICAL_PIERCE: true,
	DAMAGE_TAG_PHYSICAL_BLUNT: true,
	&"fire": true,
	&"freeze": true,
	&"lightning": true,
	&"negative_energy": true,
	&"force": true,
	&"psychic": true,
	&"radiant": true,
	&"thunder": true,
	&"magic": true,
	&"acid": true,
	&"poison": true,
}

const VALID_PHYSICAL_DAMAGE_TAGS := {
	DAMAGE_TAG_PHYSICAL_SLASH: true,
	DAMAGE_TAG_PHYSICAL_PIERCE: true,
	DAMAGE_TAG_PHYSICAL_BLUNT: true,
}

const VALID_MITIGATION_TIERS := {
	&"normal": true,
	&"half": true,
	&"double": true,
	&"immune": true,
}

const VALID_DAMAGE_CATEGORIES := {
	&"physical": true,
	&"spell": true,
	&"magic": true,
	&"energy": true,
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


static func is_valid_damage_tag(value: Variant) -> bool:
	return VALID_DAMAGE_TAGS.has(normalize_string_name(value))


static func is_valid_physical_damage_tag(value: Variant) -> bool:
	return VALID_PHYSICAL_DAMAGE_TAGS.has(normalize_string_name(value))


static func is_valid_mitigation_tier(value: Variant) -> bool:
	return VALID_MITIGATION_TIERS.has(normalize_string_name(value))


static func is_valid_damage_category(value: Variant) -> bool:
	return VALID_DAMAGE_CATEGORIES.has(normalize_string_name(value))


static func valid_damage_tag_label() -> String:
	return _sorted_key_label(VALID_DAMAGE_TAGS)


static func valid_mitigation_tier_label() -> String:
	return _sorted_key_label(VALID_MITIGATION_TIERS)


static func valid_damage_category_label() -> String:
	return _sorted_key_label(VALID_DAMAGE_CATEGORIES)


static func _sorted_key_label(source: Dictionary) -> String:
	var labels: Array[String] = []
	for key in source.keys():
		labels.append(String(key))
	labels.sort()
	return ", ".join(labels)
