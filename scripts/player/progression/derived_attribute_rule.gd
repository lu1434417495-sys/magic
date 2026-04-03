class_name DerivedAttributeRule
extends RefCounted

var target_attribute_id: StringName = &""
var base_value := 0
var coefficients: Dictionary = {}
var divisor := 1
var min_value := 0
var max_value := 0


func _init(
	p_target_attribute_id: StringName = &"",
	p_base_value: int = 0,
	p_coefficients: Dictionary = {},
	p_divisor: int = 1,
	p_min_value: int = 0,
	p_max_value: int = 0
) -> void:
	target_attribute_id = p_target_attribute_id
	base_value = p_base_value
	coefficients = p_coefficients.duplicate(true)
	divisor = maxi(p_divisor, 1)
	min_value = p_min_value
	max_value = p_max_value


func evaluate(source_values: Dictionary) -> int:
	var scaled_total := 0
	for key in coefficients.keys():
		scaled_total += int(coefficients[key]) * int(source_values.get(key, 0))

	var result := base_value + int(floor(float(scaled_total) / float(divisor)))
	if max_value > min_value:
		return clampi(result, min_value, max_value)
	return maxi(result, min_value)
