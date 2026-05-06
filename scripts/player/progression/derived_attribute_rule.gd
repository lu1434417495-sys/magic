## 文件说明：该脚本属于派生属性规则相关的业务脚本，集中维护目标属性唯一标识、基础数值、系数表等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name DerivedAttributeRule
extends RefCounted

## 字段说明：记录目标属性唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var target_attribute_id: StringName = &""
## 字段说明：记录基础数值，作为当前计算、显示或结算时使用的核心数值。
var base_value := 0
## 字段说明：缓存系数表字典，集中保存可按键查询的运行时数据。
var coefficients: Dictionary = {}
## 字段说明：记录除数，会参与成长规则判定、序列化和界面展示。
var divisor := 1
## 字段说明：记录最小数值，作为当前计算、显示或结算时使用的核心数值。
var min_value := 0
## 字段说明：记录最大数值，作为当前计算、显示或结算时使用的核心数值。
var max_value := 0
## 字段说明：记录源属性偏移量，便于实现 DND 风格的 (属性 - 10) / 2 修正计算。
var source_offset := 0


func _init(
	p_target_attribute_id: StringName = &"",
	p_base_value: int = 0,
	p_coefficients: Dictionary = {},
	p_divisor: int = 1,
	p_min_value: int = 0,
	p_max_value: int = 0,
	p_source_offset: int = 0
) -> void:
	target_attribute_id = p_target_attribute_id
	base_value = p_base_value
	coefficients = p_coefficients.duplicate(true)
	divisor = maxi(p_divisor, 1)
	min_value = p_min_value
	max_value = p_max_value
	source_offset = p_source_offset


func evaluate(source_values: Dictionary) -> int:
	var scaled_total := 0
	for key in coefficients.keys():
		scaled_total += int(coefficients[key]) * (int(source_values.get(key, 0)) - source_offset)

	var result := base_value + int(floor(float(scaled_total) / float(divisor)))
	if max_value > min_value:
		return clampi(result, min_value, max_value)
	return maxi(result, min_value)
