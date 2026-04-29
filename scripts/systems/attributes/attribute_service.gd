## 文件说明：该脚本属于属性服务相关的服务脚本，集中维护单位进度、技能定义集合、职业定义集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name AttributeService
extends RefCounted

const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const DERIVED_ATTRIBUTE_RULE_SCRIPT = preload("res://scripts/player/progression/derived_attribute_rule.gd")

const HP_MAX: StringName = &"hp_max"
const MP_MAX: StringName = &"mp_max"
const STAMINA_MAX: StringName = &"stamina_max"
const AURA_MAX: StringName = &"aura_max"
const ACTION_POINTS: StringName = &"action_points"
const ACTION_THRESHOLD: StringName = UnitBaseAttributes.ACTION_THRESHOLD
const ATTACK_BONUS: StringName = &"attack_bonus"
const WEAPON_ATTACK_RANGE: StringName = &"weapon_attack_range"
const ARMOR_CLASS: StringName = &"armor_class"
const ARMOR_AC_BONUS: StringName = &"armor_ac_bonus"
const SHIELD_AC_BONUS: StringName = &"shield_ac_bonus"
const DODGE_BONUS: StringName = &"dodge_bonus"
const DEFLECTION_BONUS: StringName = &"deflection_bonus"
const CRIT_RATE: StringName = &"crit_rate"
const CRIT_DAMAGE: StringName = &"crit_damage"

const DEFAULT_CHARACTER_ACTION_THRESHOLD := 30
const ACTION_THRESHOLD_GRANULARITY := 5

const RESOURCE_ATTRIBUTE_IDS := [
	HP_MAX,
	MP_MAX,
	STAMINA_MAX,
	AURA_MAX,
	ACTION_POINTS,
	ACTION_THRESHOLD,
]

const COMBAT_ATTRIBUTE_IDS := [
	ATTACK_BONUS,
	ARMOR_CLASS,
	ARMOR_AC_BONUS,
	SHIELD_AC_BONUS,
	DODGE_BONUS,
	DEFLECTION_BONUS,
	CRIT_RATE,
	CRIT_DAMAGE,
]

const AC_COMPONENT_ATTRIBUTE_IDS := [
	ARMOR_AC_BONUS,
	SHIELD_AC_BONUS,
	DODGE_BONUS,
	DEFLECTION_BONUS,
]

const PROTECTED_CUSTOM_STAT_KEYS: Array[StringName] = [
	UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH,
]

const PROTECTED_CUSTOM_STAT_SOURCE_CHARACTER_CREATION: StringName = &"character_creation"
const PROTECTED_CUSTOM_STAT_SOURCE_STORY_SCRIPT: StringName = &"story_script"
const PROTECTED_CUSTOM_STAT_WRITE_FLAG: StringName = &"allow_protected_custom_stat_write"

## 字段说明：保存单位进度，便于顺序遍历、批量展示、批量运算和整体重建。
var _unit_progress: UnitProgress = null
## 字段说明：缓存技能定义集合字典，集中保存可按键查询的运行时数据。
var _skill_defs: Dictionary = {}
## 字段说明：缓存职业定义集合字典，集中保存可按键查询的运行时数据。
var _profession_defs: Dictionary = {}
## 字段说明：记录装备状态，会参与运行时状态流转、系统协作和存档恢复。
var _equipment_state = null
## 字段说明：记录被动状态对象，会参与运行时状态流转、系统协作和存档恢复。
var _passive_state = null
## 字段说明：保存临时效果集合，便于顺序遍历、批量展示、批量运算和整体重建。
var _temporary_effects = null
## 字段说明：缓存派生规则集合字典，集中保存可按键查询的运行时数据。
var _derived_rules: Dictionary = {}


func _init() -> void:
	_derived_rules = _build_default_rules()


func setup(
	unit_progress: UnitProgress,
	skill_defs: Variant = null,
	profession_defs: Variant = null,
	equipment_state: Variant = null,
	passive_state: Variant = null,
	temporary_effects: Variant = null
) -> void:
	_unit_progress = unit_progress
	_skill_defs = _index_skill_defs(skill_defs)
	_profession_defs = _index_profession_defs(profession_defs)
	_equipment_state = equipment_state
	_passive_state = passive_state
	_temporary_effects = temporary_effects


func get_base_value(attribute_id: StringName) -> int:
	var unit_base_attributes := _get_unit_base_attributes()
	if unit_base_attributes == null:
		return 0
	return unit_base_attributes.get_attribute_value(attribute_id)


func get_total_value(attribute_id: StringName) -> int:
	return get_snapshot().get_value(attribute_id)


func get_action_points() -> int:
	return get_total_value(ACTION_POINTS)


func get_snapshot() -> AttributeSnapshot:
	var snapshot: AttributeSnapshot = ATTRIBUTE_SNAPSHOT_SCRIPT.new()
	var modifier_entries: Array = _collect_all_modifier_entries()
	var resolved_base_values := _resolve_base_attribute_values(modifier_entries)

	for attribute_id in UnitBaseAttributes.BASE_ATTRIBUTE_IDS:
		snapshot.set_value(attribute_id, int(resolved_base_values.get(attribute_id, 0)))

	for attribute_id in _get_known_non_base_attribute_ids():
		var derived_value := _get_persistent_base_value(attribute_id)
		if _derived_rules.has(attribute_id):
			var rule: DerivedAttributeRule = _derived_rules.get(attribute_id) as DerivedAttributeRule
			if rule != null:
				derived_value += rule.evaluate(resolved_base_values)

		snapshot.set_value(attribute_id, _apply_modifier_pipeline(attribute_id, derived_value, modifier_entries))

	for attribute_id in _get_additional_attribute_ids(modifier_entries):
		if snapshot.has_value(attribute_id):
			continue
		snapshot.set_value(
			attribute_id,
			_apply_modifier_pipeline(attribute_id, _get_persistent_base_value(attribute_id), modifier_entries)
		)

	return snapshot


func apply_permanent_attribute_change(attribute_id: StringName, delta: int, source_context: Dictionary = {}) -> bool:
	var unit_base_attributes := _get_unit_base_attributes()
	if unit_base_attributes == null:
		return false

	if _is_protected_custom_stat(attribute_id) and not _can_write_protected_custom_stat(source_context):
		push_warning(_build_protected_custom_stat_rejection_message(attribute_id, delta, source_context))
		return false

	if not UnitBaseAttributes.BASE_ATTRIBUTE_IDS.has(attribute_id) and not _can_write_custom_stat(attribute_id):
		push_warning("AttributeService: refuse permanent change for unsupported attribute %s." % String(attribute_id))
		return false

	unit_base_attributes.set_attribute_value(attribute_id, unit_base_attributes.get_attribute_value(attribute_id) + delta)
	return true


func _get_unit_base_attributes() -> UnitBaseAttributes:
	if _unit_progress == null:
		return null
	return _unit_progress.unit_base_attributes


func _is_protected_custom_stat(attribute_id: StringName) -> bool:
	return not UnitBaseAttributes.BASE_ATTRIBUTE_IDS.has(attribute_id) and PROTECTED_CUSTOM_STAT_KEYS.has(attribute_id)


func _can_write_custom_stat(attribute_id: StringName) -> bool:
	if attribute_id == &"" or UnitBaseAttributes.BASE_ATTRIBUTE_IDS.has(attribute_id):
		return false
	var unit_base_attributes := _get_unit_base_attributes()
	if unit_base_attributes == null:
		return false
	return unit_base_attributes.custom_stats.has(attribute_id) or PROTECTED_CUSTOM_STAT_KEYS.has(attribute_id)


func _can_write_protected_custom_stat(source_context: Dictionary) -> bool:
	var source_type := ProgressionDataUtils.to_string_name(source_context.get("source_type", ""))
	if source_type == PROTECTED_CUSTOM_STAT_SOURCE_CHARACTER_CREATION:
		return true
	if source_type != PROTECTED_CUSTOM_STAT_SOURCE_STORY_SCRIPT:
		return false
	return bool(source_context.get(PROTECTED_CUSTOM_STAT_WRITE_FLAG, false))


func _build_protected_custom_stat_rejection_message(attribute_id: StringName, delta: int, source_context: Dictionary) -> String:
	var source_type := ProgressionDataUtils.to_string_name(source_context.get("source_type", ""))
	var source_id := ProgressionDataUtils.to_string_name(source_context.get("source_id", ""))
	return "AttributeService: reject protected custom stat write %s delta=%d source_type=%s source_id=%s." % [
		String(attribute_id),
		delta,
		String(source_type),
		String(source_id),
	]


func _index_skill_defs(skill_defs: Variant) -> Dictionary:
	var indexed_defs: Dictionary = {}

	if skill_defs is Dictionary:
		for key in skill_defs.keys():
			var skill_def = skill_defs[key]
			if skill_def is SkillDef:
				var indexed_id: StringName = skill_def.skill_id if skill_def.skill_id != &"" else ProgressionDataUtils.to_string_name(key)
				indexed_defs[indexed_id] = skill_def
	elif skill_defs is Array:
		for skill_def in skill_defs:
			if skill_def is SkillDef and skill_def.skill_id != &"":
				indexed_defs[skill_def.skill_id] = skill_def

	return indexed_defs


func _index_profession_defs(profession_defs: Variant) -> Dictionary:
	var indexed_defs: Dictionary = {}

	if profession_defs is Dictionary:
		for key in profession_defs.keys():
			var profession_def = profession_defs[key]
			if profession_def is ProfessionDef:
				var indexed_id: StringName = profession_def.profession_id if profession_def.profession_id != &"" else ProgressionDataUtils.to_string_name(key)
				indexed_defs[indexed_id] = profession_def
	elif profession_defs is Array:
		for profession_def in profession_defs:
			if profession_def is ProfessionDef and profession_def.profession_id != &"":
				indexed_defs[profession_def.profession_id] = profession_def

	return indexed_defs


func _resolve_base_attribute_values(modifier_entries: Array) -> Dictionary:
	var resolved_values: Dictionary = {}
	for attribute_id in UnitBaseAttributes.BASE_ATTRIBUTE_IDS:
		resolved_values[attribute_id] = _apply_modifier_pipeline(attribute_id, get_base_value(attribute_id), modifier_entries)
	return resolved_values


func _collect_all_modifier_entries() -> Array:
	var entries: Array = []
	_append_profession_modifier_entries(entries)
	_append_skill_modifier_entries(entries)
	_append_external_modifier_entries(entries, _equipment_state, &"equipment")
	_append_external_modifier_entries(entries, _passive_state, &"passive")
	_append_external_modifier_entries(entries, _temporary_effects, &"temporary")
	return entries


func _append_profession_modifier_entries(entries: Array) -> void:
	if _unit_progress == null:
		return

	for profession_key in _unit_progress.professions.keys():
		var profession_id := ProgressionDataUtils.to_string_name(profession_key)
		var profession_progress: Variant = _unit_progress.get_profession_progress(profession_id)
		if profession_progress == null:
			continue
		if profession_progress.rank <= 0:
			continue
		if not profession_progress.is_active or profession_progress.is_hidden:
			continue

		var profession_def := _profession_defs.get(profession_id) as ProfessionDef
		if profession_def == null:
			continue

		_append_modifier_entries(entries, profession_def.attribute_modifiers, &"profession", profession_id, profession_progress.rank)


func _append_skill_modifier_entries(entries: Array) -> void:
	if _unit_progress == null:
		return

	for skill_key in _unit_progress.skills.keys():
		var skill_id := ProgressionDataUtils.to_string_name(skill_key)
		var skill_progress: Variant = _unit_progress.get_skill_progress(skill_id)
		if skill_progress == null or not skill_progress.is_learned:
			continue
		if not _is_skill_modifier_active(skill_progress):
			continue

		var skill_def := _skill_defs.get(skill_id) as SkillDef
		if skill_def == null:
			continue

		var effective_rank := maxi(skill_progress.skill_level, 1)
		_append_modifier_entries(entries, skill_def.attribute_modifiers, &"skill", skill_id, effective_rank)


func _is_skill_modifier_active(skill_progress: Variant) -> bool:
	if skill_progress == null:
		return false
	if skill_progress.profession_granted_by == &"":
		return true
	if _unit_progress == null:
		return false

	var profession_progress: Variant = _unit_progress.get_profession_progress(skill_progress.profession_granted_by)
	if profession_progress == null:
		return false
	return profession_progress.is_active and not profession_progress.is_hidden and profession_progress.rank > 0


func _append_external_modifier_entries(entries: Array, state: Variant, default_source_type: StringName) -> void:
	if state == null:
		return

	if state is Array:
		_append_modifier_entries(entries, state, default_source_type, default_source_type, 1)
		return

	if state is Dictionary:
		if state.get("attribute_modifiers", null) is Array:
			_append_modifier_entries(
				entries,
				state.get("attribute_modifiers", []),
				default_source_type,
				ProgressionDataUtils.to_string_name(state.get("source_id", default_source_type)),
				int(state.get("rank", 1))
			)
			return

		for key in state.keys():
			var modifiers = state[key]
			if modifiers is Array:
				_append_modifier_entries(entries, modifiers, default_source_type, ProgressionDataUtils.to_string_name(key), 1)
		return

	if state.has_method("get_attribute_modifiers"):
		var source_id := default_source_type
		if state.has_method("get_source_id"):
			source_id = ProgressionDataUtils.to_string_name(state.call("get_source_id"))
		_append_modifier_entries(entries, state.call("get_attribute_modifiers"), default_source_type, source_id, 1)


func _append_modifier_entries(
	entries: Array,
	modifiers: Variant,
	source_type: StringName,
	source_id: StringName,
	rank: int
) -> void:
	if modifiers is not Array:
		return

	for modifier in modifiers:
		if modifier is not AttributeModifier:
			continue
		if modifier.attribute_id == &"":
			continue

		entries.append({
			"attribute_id": modifier.attribute_id,
			"mode": modifier.mode,
			"value": modifier.get_value_for_rank(rank),
			"source_type": source_type if source_type != &"" else modifier.source_type,
			"source_id": source_id if source_id != &"" else modifier.source_id,
		})


func _get_persistent_base_value(attribute_id: StringName) -> int:
	var unit_base_attributes := _get_unit_base_attributes()
	if unit_base_attributes == null:
		return 0
	if attribute_id == ACTION_THRESHOLD and not unit_base_attributes.custom_stats.has(ACTION_THRESHOLD):
		return DEFAULT_CHARACTER_ACTION_THRESHOLD
	return unit_base_attributes.get_attribute_value(attribute_id)


func _apply_modifier_pipeline(attribute_id: StringName, base_value: int, modifier_entries: Array) -> int:
	var flat_delta := 0
	var percent_delta := 0

	for entry in modifier_entries:
		var modifier_attribute_id := ProgressionDataUtils.to_string_name(entry.get("attribute_id", ""))
		if not _modifier_entry_applies_to_attribute(attribute_id, modifier_attribute_id):
			continue

		var value := int(entry.get("value", 0))
		var mode := ProgressionDataUtils.to_string_name(entry.get("mode", "flat"))
		if mode == AttributeModifier.MODE_PERCENT:
			percent_delta += value
		else:
			flat_delta += value

	var result := base_value + flat_delta
	if percent_delta != 0:
		result = int(floor(float(result) * float(100 + percent_delta) / 100.0))

	return _clamp_attribute_value(attribute_id, result)


func _modifier_entry_applies_to_attribute(attribute_id: StringName, modifier_attribute_id: StringName) -> bool:
	if modifier_attribute_id == attribute_id:
		return true
	return attribute_id == ARMOR_CLASS and AC_COMPONENT_ATTRIBUTE_IDS.has(modifier_attribute_id)


func _clamp_attribute_value(attribute_id: StringName, value: int) -> int:
	match attribute_id:
		HP_MAX:
			return maxi(value, 1)
		MP_MAX, STAMINA_MAX, AURA_MAX:
			return maxi(value, 0)
		ACTION_POINTS:
			return maxi(value, 1)
		ACTION_THRESHOLD:
			return _normalize_action_threshold(value)
		ATTACK_BONUS:
			return clampi(value, -20, 50)
		ARMOR_CLASS:
			return clampi(value, 1, 99)
		ARMOR_AC_BONUS, SHIELD_AC_BONUS, DODGE_BONUS, DEFLECTION_BONUS:
			return maxi(value, 0)
		CRIT_RATE:
			return clampi(value, 0, 100)
		CRIT_DAMAGE:
			return maxi(value, 100)
		_:
			return value


func _get_known_non_base_attribute_ids() -> Array[StringName]:
	var result: Array[StringName] = []

	for attribute_id in RESOURCE_ATTRIBUTE_IDS:
		result.append(attribute_id)
	for attribute_id in COMBAT_ATTRIBUTE_IDS:
		result.append(attribute_id)
	return result


func _normalize_action_threshold(value: int) -> int:
	var threshold := maxi(value, ACTION_THRESHOLD_GRANULARITY)
	return maxi(
		int(round(float(threshold) / float(ACTION_THRESHOLD_GRANULARITY))) * ACTION_THRESHOLD_GRANULARITY,
		ACTION_THRESHOLD_GRANULARITY
	)


func _get_additional_attribute_ids(modifier_entries: Array) -> Array[StringName]:
	var result: Array[StringName] = []
	var seen: Dictionary = {}
	var known_attribute_ids := _get_known_non_base_attribute_ids()

	for attribute_id in UnitBaseAttributes.BASE_ATTRIBUTE_IDS:
		known_attribute_ids.append(attribute_id)
		seen[attribute_id] = true
	for attribute_id in known_attribute_ids:
		seen[attribute_id] = true

	var unit_base_attributes := _get_unit_base_attributes()
	if unit_base_attributes != null:
		for key in unit_base_attributes.custom_stats.keys():
			var attribute_id := ProgressionDataUtils.to_string_name(key)
			if seen.has(attribute_id):
				continue
			seen[attribute_id] = true
			result.append(attribute_id)

	for entry in modifier_entries:
		var attribute_id := ProgressionDataUtils.to_string_name(entry.get("attribute_id", ""))
		if attribute_id == &"" or seen.has(attribute_id):
			continue
		seen[attribute_id] = true
		result.append(attribute_id)

	return result


func _build_default_rules() -> Dictionary:
	var rules: Dictionary = {}

	rules[HP_MAX] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		HP_MAX,
		60,
		{
			UnitBaseAttributes.CONSTITUTION: 8,
			UnitBaseAttributes.STRENGTH: 2,
		},
		1,
		1
	)
	rules[MP_MAX] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		MP_MAX,
		30,
		{
			UnitBaseAttributes.INTELLIGENCE: 6,
			UnitBaseAttributes.WILLPOWER: 4,
		},
		1,
		0
	)
	rules[STAMINA_MAX] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		STAMINA_MAX,
		24,
		{
			UnitBaseAttributes.CONSTITUTION: 5,
			UnitBaseAttributes.STRENGTH: 1,
			UnitBaseAttributes.AGILITY: 1,
		},
		1,
		0
	)
	rules[ACTION_POINTS] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		ACTION_POINTS,
		6,
		{
			UnitBaseAttributes.AGILITY: 2,
			UnitBaseAttributes.PERCEPTION: 1,
			UnitBaseAttributes.WILLPOWER: 1,
		},
		6,
		1
	)
	rules[ATTACK_BONUS] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		ATTACK_BONUS,
		2,
		{
			UnitBaseAttributes.PERCEPTION: 2,
			UnitBaseAttributes.AGILITY: 1,
			UnitBaseAttributes.STRENGTH: 1,
		},
		4,
		0
	)
	rules[ARMOR_CLASS] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		ARMOR_CLASS,
		8,
		{
			UnitBaseAttributes.AGILITY: 1,
		},
		1,
		8,
		14
	)
	rules[CRIT_RATE] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		CRIT_RATE,
		5,
		{
			UnitBaseAttributes.PERCEPTION: 4,
			UnitBaseAttributes.AGILITY: 2,
		},
		6,
		0,
		100
	)
	rules[CRIT_DAMAGE] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		CRIT_DAMAGE,
		150,
		{
			UnitBaseAttributes.STRENGTH: 2,
			UnitBaseAttributes.INTELLIGENCE: 2,
		},
		4,
		100
	)
	return rules
