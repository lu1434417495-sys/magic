## 文件说明：该脚本属于战斗技能定义相关的定义资源脚本，集中维护技能唯一标识、目标模式、目标队伍过滤等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name CombatSkillDef
extends Resource

const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

## 字段说明：在编辑器中暴露技能唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var skill_id: StringName = &""
## 字段说明：在编辑器中暴露目标模式配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var target_mode: StringName = &"unit"
## 字段说明：在编辑器中暴露目标队伍过滤配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var target_team_filter: StringName = &"enemy"
## 字段说明：在编辑器中暴露范围图案参数，便于直接调整尺寸、范围、间距或视图表现。
@export var range_pattern: StringName = &"single"
## 字段说明：在编辑器中暴露范围数值参数，便于直接调整尺寸、范围、间距或视图表现。
@export var range_value := 1
## 字段说明：在编辑器中暴露范围图案配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var area_pattern: StringName = &"single"
## 字段说明：在编辑器中暴露范围数值配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var area_value := 0
## 字段说明：在编辑器中暴露需要视线配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var requires_los := false
## 字段说明：在编辑器中暴露行动点消耗配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var ap_cost := 1
## 字段说明：在编辑器中暴露法力值消耗配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var mp_cost := 0
## 字段说明：在编辑器中暴露体力值消耗配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var stamina_cost := 0
## 字段说明：在编辑器中暴露冷却TU配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var cooldown_tu := 0
## 字段说明：在编辑器中暴露 D20 攻击检定加值配置，供命中结算链读取。
@export var attack_roll_bonus := 0
## 字段说明：在编辑器中暴露斗气消耗配置，便于定义文档中的 aura 资源技能。
@export var aura_cost := 0
## 字段说明：按技能等级覆盖消耗或冷却，键为最低生效等级，值可包含 ap_cost / mp_cost / stamina_cost / aura_cost / cooldown_tu。
@export var level_overrides: Dictionary = {}:
	set(value):
		level_overrides = _normalize_level_overrides(value)
## 字段说明：战斗熟练度触发模式；默认保留旧的技能伤害骰事件规则。
@export var mastery_trigger_mode: StringName = &"skill_damage_dice_max"
## 字段说明：战斗熟练度数值模式；默认按目标阶级逐个目标计入。
@export var mastery_amount_mode: StringName = &"per_target_rank"
## 字段说明：法术控制检定模式；control_roll 表示施放时用施法者幸运做一次独立 D20 控制检定。
@export var spell_fate_mode: StringName = &""
## 字段说明：法术控制大成功奖励模式；mp_refund 表示按本次实际消耗法力返还。
@export var spell_critical_mode: StringName = &""
## 字段说明：法术控制大成功时返还本次实际法力消耗的百分比。
@export var spell_critical_mp_refund_percent := 0
## 字段说明：技能等级到本场大失败保护次数的映射；下标按当前技能等级读取。
@export var fumble_protection_curve: PackedInt32Array = PackedInt32Array()
## 字段说明：大失败被保护压制时，额外吞噬本次实际法力消耗的百分比。
@export var fumble_protection_extra_mp_percent := 100
## 字段说明：无保护大失败后的失控模式；ground_anchor_drift 表示地面范围法术落点偏移。
@export var backlash_mode: StringName = &""
## 字段说明：失控时可选目标过滤覆盖；当前火球友伤由效果自身 any 过滤表达，本字段预留给后续技能。
@export var backlash_target_filter: StringName = &""
## 字段说明：ground_anchor_drift 的切比雪夫偏移半径。
@export var backlash_offset_radius := 0
## 字段说明：在编辑器中暴露影响区域原点模式配置，用于补充 line / cone / self 等范围语义。
@export var area_origin_mode: StringName = &"target"
## 字段说明：在编辑器中暴露影响区域方向模式配置，用于补充 line / cone 等朝向语义。
@export var area_direction_mode: StringName = &"target_vector"
## 字段说明：在编辑器中暴露 AI 标签集合配置，便于后续评分模型读取技能意图。
@export var ai_tags: Array[StringName] = []
## 字段说明：正式投射 / 施放类别；屏障、反制和类似规则只读取此字段，不从技能 ID 或标签猜测。
@export var delivery_categories: Array[StringName] = []
## 字段说明：特殊技能结算 profile；非空时战斗运行时必须通过 profile manifest 的受控 resolver 结算，不读取 executable effect_defs。
@export var special_resolution_profile_id: StringName = &""
## 字段说明：在编辑器中暴露目标选择模式配置，便于表达单体、多单位或其他特殊选目标协议。
@export var target_selection_mode: StringName = &"single_unit"
## 字段说明：在编辑器中暴露最小目标数量配置，便于表达多目标技能的下限要求。
@export var min_target_count := 1
## 字段说明：在编辑器中暴露最大目标数量配置，便于表达多目标技能的上限要求。
@export var max_target_count := 1
## 字段说明：允许多次选择同一单位作为目标（如奥术飞弹可多发射向同一目标）。
@export var allow_repeat_target: bool = false
## 字段说明：同一目标被允许重复选择的最大次数；0 或 1 表示无额外限制（仅受 allow_repeat_target 控制）。
@export var max_hits_per_target := 0
## 字段说明：在编辑器中暴露目标结算顺序模式配置，便于保持预览与实际执行顺序一致。
@export var selection_order_mode: StringName = &"stable"
## 字段说明：在编辑器中暴露效果定义集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var effect_defs: Array[CombatEffectDef] = []
## 字段说明：被动技能的效果定义集合；仅在 skill_type 为 passive 时读取，用于战斗开始时或条件触发时的效果链。
@export var passive_effect_defs: Array[CombatEffectDef] = []
## 字段说明：在编辑器中暴露施放变体集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var cast_variants: Array[CombatCastVariantDef] = []
## 字段说明：必须使用的武器家族列表；非空时单位必须装备对应家族的有效武器才能施放该技能。
@export var required_weapon_families: Array[StringName] = []
## 字段说明：禁止使用的武器家族列表；如果当前武器家族在此列表中，则无法施放该技能。
@export var excluded_weapon_families: Array[StringName] = []
## 字段说明：禁止使用的武器类型ID列表；如果当前武器类型ID在此列表中，则无法施放该技能。
@export var excluded_weapon_type_ids: Array[StringName] = []
## 字段说明：是否需要装备盾牌；为 true 时单位必须在 off_hand 槽位装备带有 shield 标签的物品才能施放该技能。
@export var requires_equipped_shield: bool = false
## 字段说明：目标生命值低于阈值时，该目标提供的熟练度倍率；默认 1 表示无额外加成。
@export var mastery_low_hp_bonus_multiplier: int = 1
## 字段说明：触发低血熟练度加成的生命值百分比阈值；默认 50 表示低于 50% 最大生命时触发。
@export var mastery_low_hp_threshold_percent: int = 50


func get_cast_variant(variant_id: StringName) -> CombatCastVariantDef:
	if variant_id == &"":
		return null
	for cast_variant in cast_variants:
		if cast_variant != null and cast_variant.variant_id == variant_id:
			return cast_variant
	return null


func get_unlocked_cast_variants(skill_level: int) -> Array[CombatCastVariantDef]:
	var unlocked_variants: Array[CombatCastVariantDef] = []
	for cast_variant in cast_variants:
		if cast_variant == null:
			continue
		if skill_level < int(cast_variant.min_skill_level):
			continue
		unlocked_variants.append(cast_variant)
	return unlocked_variants


func get_effective_resource_costs(skill_level: int) -> Dictionary:
	var costs := {
		"ap_cost": int(ap_cost),
		"mp_cost": int(mp_cost),
		"stamina_cost": int(stamina_cost),
		"aura_cost": int(aura_cost),
		"cooldown_tu": int(cooldown_tu),
	}
	var override := get_level_override(skill_level)
	for key in costs.keys():
		if override.has(key):
			costs[key] = int(override.get(key, costs[key]))
	return costs


func get_level_override(skill_level: int) -> Dictionary:
	var eligible_overrides: Array[Dictionary] = []
	for level_key in level_overrides.keys():
		if typeof(level_key) != TYPE_INT:
			continue
		var override_level := int(level_key)
		if override_level < 0 or override_level > skill_level:
			continue
		var override_data = level_overrides.get(level_key)
		if override_data is not Dictionary:
			continue
		eligible_overrides.append({
			"level": override_level,
			"data": override_data,
		})
	eligible_overrides.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		return int(a.get("level", 0)) < int(b.get("level", 0))
	)

	var merged_override: Dictionary = {}
	for override_entry in eligible_overrides:
		var override_data = override_entry.get("data", {})
		if override_data is not Dictionary:
			continue
		for key in (override_data as Dictionary).keys():
			merged_override[key] = (override_data as Dictionary).get(key)
	return merged_override


func get_effective_attack_roll_bonus(skill_level: int) -> int:
	var override := get_level_override(skill_level)
	if override.has("attack_roll_bonus"):
		return int(override.get("attack_roll_bonus", attack_roll_bonus))
	return attack_roll_bonus


func get_effective_area_pattern(skill_level: int) -> StringName:
	var override := get_level_override(skill_level)
	if override.has("area_pattern"):
		return ProgressionDataUtils.to_string_name(override.get("area_pattern", area_pattern))
	return area_pattern


func get_effective_area_value(skill_level: int) -> int:
	var override := get_level_override(skill_level)
	if override.has("area_value"):
		return int(override.get("area_value", area_value))
	return area_value


func get_effective_range_value(skill_level: int) -> int:
	var override := get_level_override(skill_level)
	if override.has("range_value"):
		return int(override.get("range_value", range_value))
	return range_value


func get_effective_max_target_count(skill_level: int) -> int:
	var override := get_level_override(skill_level)
	if override.has("max_target_count"):
		return int(override.get("max_target_count", max_target_count))
	return max_target_count


func has_spell_fate_control() -> bool:
	return spell_fate_mode == &"control_roll"


func get_spell_critical_mp_refund_percent() -> int:
	return clampi(int(spell_critical_mp_refund_percent), 0, 100)


func get_fumble_protection_limit(skill_level: int) -> int:
	if fumble_protection_curve.is_empty():
		return 0
	var index := clampi(skill_level, 0, fumble_protection_curve.size() - 1)
	return maxi(int(fumble_protection_curve[index]), 0)


func get_fumble_protection_extra_mp_percent() -> int:
	return maxi(int(fumble_protection_extra_mp_percent), 0)


func uses_ground_anchor_drift_backlash() -> bool:
	return backlash_mode == &"ground_anchor_drift"


func _parse_override_level(level_key) -> int:
	if typeof(level_key) != TYPE_INT:
		return -1
	return int(level_key)


func _normalize_level_overrides(raw_overrides) -> Dictionary:
	if raw_overrides is not Dictionary:
		return {}
	var normalized: Dictionary = {}
	for level_key in (raw_overrides as Dictionary).keys():
		var normalized_key = level_key
		if typeof(level_key) == TYPE_FLOAT:
			var level_float := float(level_key)
			if is_equal_approx(level_float, floor(level_float)):
				normalized_key = int(level_float)
		normalized[normalized_key] = (raw_overrides as Dictionary).get(level_key)
	return normalized
