## 文件说明：该脚本属于属性快照相关的业务脚本，集中维护数值表等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name AttributeSnapshot
extends RefCounted

const STRENGTH_MODIFIER: StringName = &"strength_modifier"
const AGILITY_MODIFIER: StringName = &"agility_modifier"
const CONSTITUTION_MODIFIER: StringName = &"constitution_modifier"
const PERCEPTION_MODIFIER: StringName = &"perception_modifier"
const INTELLIGENCE_MODIFIER: StringName = &"intelligence_modifier"
const WILLPOWER_MODIFIER: StringName = &"willpower_modifier"
const BASE_ATTACK_BONUS: StringName = &"base_attack_bonus"
const SPELL_PROFICIENCY_BONUS: StringName = &"spell_proficiency_bonus"

## BAB 累加档位枚举：用于 ProfessionDef.bab_progression 字段。
const BAB_PROGRESSION_FULL: StringName = &"full"
const BAB_PROGRESSION_THREE_QUARTER: StringName = &"three_quarter"
const BAB_PROGRESSION_HALF: StringName = &"half"

## BAB 整数算法：以 8 为公分母，三档 rate 分别为 4 / 3 / 2。
## 多职业累加规则：必须先把所有职业的 rank × rate 累加成总分子，再除以分母 8。
## 详见 docs/design/dnd35e_combat_system_vision.md §1.1。
const BAB_RATE_FULL := 4
const BAB_RATE_THREE_QUARTER := 3
const BAB_RATE_HALF := 2
const BAB_DENOMINATOR := 8

## 字段说明：缓存数值表字典，集中保存可按键查询的运行时数据。
var _values: Dictionary = {}


func set_value(attribute_id: StringName, value: int) -> void:
	_values[attribute_id] = value
	var modifier_id := get_base_attribute_modifier_id(attribute_id)
	if modifier_id != &"":
		_values[modifier_id] = calculate_score_modifier(value)


func get_value(attribute_id: StringName) -> int:
	return int(_values.get(attribute_id, 0))


func has_value(attribute_id: StringName) -> bool:
	return _values.has(attribute_id)


func get_all_values() -> Dictionary:
	return _values.duplicate(true)


func to_dict() -> Dictionary:
	return ProgressionDataUtils.string_name_int_map_to_string_dict(_values)


static func get_base_attribute_modifier_id(attribute_id: StringName) -> StringName:
	match attribute_id:
		UnitBaseAttributes.STRENGTH:
			return STRENGTH_MODIFIER
		UnitBaseAttributes.AGILITY:
			return AGILITY_MODIFIER
		UnitBaseAttributes.CONSTITUTION:
			return CONSTITUTION_MODIFIER
		UnitBaseAttributes.PERCEPTION:
			return PERCEPTION_MODIFIER
		UnitBaseAttributes.INTELLIGENCE:
			return INTELLIGENCE_MODIFIER
		UnitBaseAttributes.WILLPOWER:
			return WILLPOWER_MODIFIER
		_:
			return &""


static func calculate_score_modifier(score: int) -> int:
	return int(floor(float(score - 10) / 2.0))


## 计算累计基础攻击加值（BAB）。
## active_profession_pairs：每项为 [rank: int, progression: StringName]。
## 调用方应仅传入实际生效（rank > 0、is_active、未被隐藏）的职业条目。
## 实现遵循"先乘后除"约定：累加所有职业的 rank × rate 后一次性除分母，避免逐职业 floor 截断丢精度。
static func calculate_base_attack_bonus(active_profession_pairs: Array) -> int:
	var numerator := 0
	for pair in active_profession_pairs:
		if pair == null or pair.size() < 2:
			continue
		var rank := int(pair[0])
		if rank <= 0:
			continue
		numerator += rank * get_bab_rate_for_progression(pair[1])
	return int(numerator / BAB_DENOMINATOR)


static func calculate_spell_proficiency_bonus(character_level: int) -> int:
	var effective_level := maxi(character_level, 1)
	return clampi(2 + int((effective_level - 1) / 4), 2, 6)


static func get_bab_rate_for_progression(progression: Variant) -> int:
	var normalized: StringName = ProgressionDataUtils.to_string_name(progression) if progression != null else &""
	match normalized:
		BAB_PROGRESSION_FULL:
			return BAB_RATE_FULL
		BAB_PROGRESSION_THREE_QUARTER:
			return BAB_RATE_THREE_QUARTER
		_:
			return BAB_RATE_HALF
