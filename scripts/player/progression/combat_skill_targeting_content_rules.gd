class_name CombatSkillTargetingContentRules
extends RefCounted

const TARGET_MODE_UNIT: StringName = &"unit"
const TARGET_MODE_GROUND: StringName = &"ground"

const VALID_COMBAT_TARGET_MODES := {
	TARGET_MODE_UNIT: true,
	TARGET_MODE_GROUND: true,
}

const VALID_CAST_VARIANT_TARGET_MODES := {
	TARGET_MODE_UNIT: true,
	TARGET_MODE_GROUND: true,
}

const VALID_TARGET_SELECTION_MODES := {
	&"single_unit": true,
	&"multi_unit": true,
	&"random_chain": true,
	&"self": true,
	&"single_coord": true,
	&"coord_pair": true,
}

const VALID_SELECTION_ORDER_MODES := {
	&"stable": true,
	&"manual": true,
}

const VALID_AREA_PATTERNS := {
	&"single": true,
	&"self": true,
	&"diamond": true,
	&"square": true,
	&"radius": true,
	&"cross": true,
	&"line": true,
	&"cone": true,
	&"narrow_cone": true,
	&"front_arc": true,
}

const VALID_FOOTPRINT_PATTERNS := {
	&"single": true,
	&"line2": true,
	&"square2": true,
	&"unordered": true,
}


static func is_valid_combat_target_mode(value: Variant) -> bool:
	return VALID_COMBAT_TARGET_MODES.has(_normalize_string_name(value))


static func is_valid_cast_variant_target_mode(value: Variant) -> bool:
	return VALID_CAST_VARIANT_TARGET_MODES.has(_normalize_string_name(value))


static func is_valid_target_selection_mode(value: Variant) -> bool:
	return VALID_TARGET_SELECTION_MODES.has(_normalize_string_name(value))


static func is_valid_selection_order_mode(value: Variant) -> bool:
	return VALID_SELECTION_ORDER_MODES.has(_normalize_string_name(value))


static func is_valid_area_pattern(value: Variant) -> bool:
	return VALID_AREA_PATTERNS.has(_normalize_string_name(value))


static func is_valid_footprint_pattern(value: Variant) -> bool:
	return VALID_FOOTPRINT_PATTERNS.has(_normalize_string_name(value))


static func valid_combat_target_mode_label() -> String:
	return _sorted_key_label(VALID_COMBAT_TARGET_MODES)


static func valid_cast_variant_target_mode_label() -> String:
	return _sorted_key_label(VALID_CAST_VARIANT_TARGET_MODES)


static func valid_target_selection_mode_label() -> String:
	return _sorted_key_label(VALID_TARGET_SELECTION_MODES)


static func valid_selection_order_mode_label() -> String:
	return _sorted_key_label(VALID_SELECTION_ORDER_MODES)


static func valid_area_pattern_label() -> String:
	return _sorted_key_label(VALID_AREA_PATTERNS)


static func valid_footprint_pattern_label() -> String:
	return _sorted_key_label(VALID_FOOTPRINT_PATTERNS)


static func _normalize_string_name(value: Variant) -> StringName:
	if value is StringName:
		return value
	if value is String:
		var text := (value as String).strip_edges()
		return StringName(text) if not text.is_empty() else &""
	return &""


static func _sorted_key_label(source: Dictionary) -> String:
	var labels: Array[String] = []
	for key in source.keys():
		labels.append(String(key))
	labels.sort()
	return ", ".join(labels)
