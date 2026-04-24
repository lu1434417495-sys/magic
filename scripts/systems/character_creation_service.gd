## 文件说明：该脚本属于建卡服务相关的服务脚本，集中维护 reroll 次数到出生幸运的烘焙规则。
## 审查重点：重点核对档位边界、溢出输入回退以及通过 AttributeService 写入受保护 custom stat 的方式。
## 备注：当前只落 reroll -> hidden_luck_at_birth 烘焙，不承载队友模板写入或剧情改写。

class_name CharacterCreationService
extends RefCounted

const HIDDEN_LUCK_AT_BIRTH_MAX := 2
const HIDDEN_LUCK_AT_BIRTH_MIN := -6
const DEFAULT_SOURCE_ID: StringName = &"birth_roll"
const MAXIMUM_REROLL_TIER_MINIMUM := 10_000_000


static func map_reroll_count_to_hidden_luck_at_birth(reroll_count: Variant) -> int:
	match typeof(reroll_count):
		TYPE_INT:
			return _map_integer_reroll_count(int(reroll_count))
		TYPE_FLOAT:
			return _map_float_reroll_count(float(reroll_count))
		TYPE_STRING, TYPE_STRING_NAME:
			return _map_string_reroll_count(String(reroll_count))
		_:
			return HIDDEN_LUCK_AT_BIRTH_MAX


func bake_hidden_luck_at_birth(attribute_service: AttributeService, reroll_count: Variant, source_id: StringName = DEFAULT_SOURCE_ID) -> bool:
	if attribute_service == null:
		return false

	var target_hidden_luck := map_reroll_count_to_hidden_luck_at_birth(reroll_count)
	var current_hidden_luck := attribute_service.get_base_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH)
	var delta := target_hidden_luck - current_hidden_luck
	if delta == 0:
		return true

	return attribute_service.apply_permanent_attribute_change(
		UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH,
		delta,
		{
			"source_type": AttributeService.PROTECTED_CUSTOM_STAT_SOURCE_CHARACTER_CREATION,
			"source_id": source_id,
		}
	)


static func _map_integer_reroll_count(reroll_count: int) -> int:
	if reroll_count <= 0:
		return HIDDEN_LUCK_AT_BIRTH_MAX
	if reroll_count >= MAXIMUM_REROLL_TIER_MINIMUM:
		return HIDDEN_LUCK_AT_BIRTH_MIN
	return 2 - str(reroll_count).length()


static func _map_float_reroll_count(reroll_count: float) -> int:
	if reroll_count != reroll_count:
		return HIDDEN_LUCK_AT_BIRTH_MIN
	if reroll_count <= 0.0:
		return HIDDEN_LUCK_AT_BIRTH_MAX
	if reroll_count >= float(MAXIMUM_REROLL_TIER_MINIMUM):
		return HIDDEN_LUCK_AT_BIRTH_MIN
	return _map_integer_reroll_count(int(floor(reroll_count)))


static func _map_string_reroll_count(reroll_count_text: String) -> int:
	var normalized_text := reroll_count_text.strip_edges()
	if normalized_text.is_empty():
		return HIDDEN_LUCK_AT_BIRTH_MAX
	if normalized_text.begins_with("-"):
		return HIDDEN_LUCK_AT_BIRTH_MAX
	if normalized_text.begins_with("+"):
		normalized_text = normalized_text.substr(1)
	if normalized_text.is_empty():
		return HIDDEN_LUCK_AT_BIRTH_MAX

	var first_non_zero_index := -1
	for index in range(normalized_text.length()):
		var digit := normalized_text[index]
		if digit < "0" or digit > "9":
			return HIDDEN_LUCK_AT_BIRTH_MAX
		if digit != "0" and first_non_zero_index == -1:
			first_non_zero_index = index

	if first_non_zero_index == -1:
		return HIDDEN_LUCK_AT_BIRTH_MAX

	var digit_count := normalized_text.length() - first_non_zero_index
	if digit_count >= 8:
		return HIDDEN_LUCK_AT_BIRTH_MIN
	return 2 - digit_count
