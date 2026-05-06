## 文件说明：该脚本属于队伍成员状态相关的状态数据脚本，集中维护成员唯一标识、显示名称、阵营唯一标识等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name PartyMemberState
extends RefCounted

const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const UNIT_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_progress.gd")
const EQUIPMENT_STATE_SCRIPT = preload("res://scripts/player/equipment/equipment_state.gd")

const TO_DICT_FIELDS: Array[String] = [
	"member_id",
	"display_name",
	"faction_id",
	"portrait_id",
	"progression",
	"equipment_state",
	"control_mode",
	"current_hp",
	"current_mp",
	"is_dead",
	"race_id",
	"subrace_id",
	"age_years",
	"birth_at_world_step",
	"age_profile_id",
	"natural_age_stage_id",
	"effective_age_stage_id",
	"effective_age_stage_source_type",
	"effective_age_stage_source_id",
	"body_size",
	"body_size_category",
	"versatility_pick",
	"active_stage_advancement_modifier_ids",
	"bloodline_id",
	"bloodline_stage_id",
	"ascension_id",
	"ascension_stage_id",
	"ascension_started_at_world_step",
	"original_race_id_before_ascension",
	"biological_age_years",
	"astral_memory_years",
]

## 字段说明：记录成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var member_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
var display_name: String = ""
## 字段说明：记录阵营唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var faction_id: StringName = &"player"
## 字段说明：记录头像唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var portrait_id: StringName = &""
## 字段说明：记录成长，会参与成长规则判定、序列化和界面展示。
var progression = UNIT_PROGRESS_SCRIPT.new()
## 字段说明：缓存装备状态字典，集中保存可按键查询的运行时数据。
var equipment_state = EQUIPMENT_STATE_SCRIPT.new()
## 字段说明：记录控制模式，用于在不同处理分支之间切换规则或交互方式。
var control_mode: StringName = &"manual"
## 字段说明：记录当前生命值，会参与成长规则判定、序列化和界面展示。
var current_hp := 1
## 字段说明：记录当前法力值，会参与成长规则判定、序列化和界面展示。
var current_mp := 0
## 字段说明：记录该成员是否已经永久死亡；死亡后不再参与队伍编成与战斗。
var is_dead := false
## 字段说明：记录角色种族，是人物身份与后续属性 / 被动投影的持久真相源。
var race_id: StringName = &"human"
## 字段说明：记录角色亚种，是人物身份与后续属性 / 被动投影的持久真相源。
var subrace_id: StringName = &"common_human"
## 字段说明：记录角色当前自然年龄。
var age_years := 24
## 字段说明：记录角色出生时的世界步数。
var birth_at_world_step := 0
## 字段说明：记录角色使用的年龄曲线配置。
var age_profile_id: StringName = &"human_age_profile"
## 字段说明：记录自然年龄阶段。
var natural_age_stage_id: StringName = &"adult"
## 字段说明：记录受长期修正后的有效年龄阶段。
var effective_age_stage_id: StringName = &"adult"
## 字段说明：记录有效年龄阶段修正来源类型；无修正时为空。
var effective_age_stage_source_type: StringName = &""
## 字段说明：记录有效年龄阶段修正来源 id；无修正时为空。
var effective_age_stage_source_id: StringName = &""
## 字段说明：记录体型尺寸，用于布局、碰撞、绘制或程序化生成时的尺寸计算。
var body_size := 2
## 字段说明：记录身份层体型分类；body_size 是当前战斗 / 布局链路的派生缓存。
var body_size_category: StringName = &"medium"
## 字段说明：记录建卡或身份特性带来的自选项；无自选时为空。
var versatility_pick: StringName = &""
## 字段说明：记录长期生效的阶段提升来源 id。
var active_stage_advancement_modifier_ids: Array[StringName] = []
## 字段说明：记录血脉身份 id；无血脉时为空。
var bloodline_id: StringName = &""
## 字段说明：记录血脉阶段 id；无血脉时为空。
var bloodline_stage_id: StringName = &""
## 字段说明：记录剧情升华身份 id；未升华时为空。
var ascension_id: StringName = &""
## 字段说明：记录剧情升华阶段 id；未升华时为空。
var ascension_stage_id: StringName = &""
## 字段说明：记录升华开始的世界步数；-1 表示未开始。
var ascension_started_at_world_step := -1
## 字段说明：记录升华前原始种族 id；未升华时为空。
var original_race_id_before_ascension: StringName = &""
## 字段说明：记录特殊时间规则下的身体年龄。
var biological_age_years := 24
## 字段说明：记录星界 / 记忆类年龄。
var astral_memory_years := 0


func get_hidden_luck_at_birth() -> int:
	var unit_base_attributes := _get_unit_base_attributes()
	if unit_base_attributes == null:
		return 0
	return unit_base_attributes.get_hidden_luck_at_birth()


func get_faith_luck_bonus() -> int:
	var unit_base_attributes := _get_unit_base_attributes()
	if unit_base_attributes == null:
		return 0
	return unit_base_attributes.get_faith_luck_bonus()


func get_effective_luck() -> int:
	var unit_base_attributes := _get_unit_base_attributes()
	if unit_base_attributes == null:
		return 0
	return unit_base_attributes.get_effective_luck()


func get_combat_luck_score() -> int:
	var unit_base_attributes := _get_unit_base_attributes()
	if unit_base_attributes == null:
		return 0
	return unit_base_attributes.get_combat_luck_score()


func get_drop_luck() -> int:
	var unit_base_attributes := _get_unit_base_attributes()
	if unit_base_attributes == null:
		return 0
	return unit_base_attributes.get_drop_luck()


func to_dict() -> Dictionary:
	return {
		"member_id": String(member_id),
		"display_name": display_name,
		"faction_id": String(faction_id),
		"portrait_id": String(portrait_id),
		"progression": progression.to_dict() if progression != null else {},
		"equipment_state": equipment_state.to_dict() if equipment_state is Object and equipment_state.has_method("to_dict") else {},
		"control_mode": String(control_mode),
		"current_hp": current_hp,
		"current_mp": current_mp,
		"is_dead": is_dead,
		"race_id": String(race_id),
		"subrace_id": String(subrace_id),
		"age_years": age_years,
		"birth_at_world_step": birth_at_world_step,
		"age_profile_id": String(age_profile_id),
		"natural_age_stage_id": String(natural_age_stage_id),
		"effective_age_stage_id": String(effective_age_stage_id),
		"effective_age_stage_source_type": String(effective_age_stage_source_type),
		"effective_age_stage_source_id": String(effective_age_stage_source_id),
		"body_size": body_size,
		"body_size_category": String(body_size_category),
		"versatility_pick": String(versatility_pick),
		"active_stage_advancement_modifier_ids": ProgressionDataUtils.string_name_array_to_string_array(active_stage_advancement_modifier_ids),
		"bloodline_id": String(bloodline_id),
		"bloodline_stage_id": String(bloodline_stage_id),
		"ascension_id": String(ascension_id),
		"ascension_stage_id": String(ascension_stage_id),
		"ascension_started_at_world_step": ascension_started_at_world_step,
		"original_race_id_before_ascension": String(original_race_id_before_ascension),
		"biological_age_years": biological_age_years,
		"astral_memory_years": astral_memory_years,
	}


static func from_dict(data: Dictionary):
	if data.is_empty():
		return null
	if not _has_exact_fields(data, TO_DICT_FIELDS):
		return null
	var progression_data: Variant = data.get("progression", null)
	var equipment_state_data: Variant = data.get("equipment_state", null)
	if progression_data is not Dictionary or equipment_state_data is not Dictionary:
		return null
	var member_id = _parse_string_name_field(data.get("member_id", null), false)
	if member_id == null:
		return null
	var display_name_variant: Variant = data.get("display_name", null)
	if display_name_variant is not String:
		return null
	var display_name := String(display_name_variant)
	if display_name.strip_edges().is_empty():
		return null
	var faction_id = _parse_string_name_field(data.get("faction_id", null), false)
	if faction_id == null:
		return null
	var portrait_id = _parse_string_name_field(data.get("portrait_id", null), true)
	if portrait_id == null:
		return null
	var control_mode = _parse_string_name_field(data.get("control_mode", null), false)
	if control_mode == null:
		return null
	var current_hp_variant: Variant = data.get("current_hp", null)
	if current_hp_variant is not int or int(current_hp_variant) < 0:
		return null
	var current_mp_variant: Variant = data.get("current_mp", null)
	if current_mp_variant is not int or int(current_mp_variant) < 0:
		return null
	var is_dead_variant: Variant = data.get("is_dead", null)
	if is_dead_variant is not bool:
		return null
	var race_id = _parse_string_name_field(data.get("race_id", null), false)
	if race_id == null:
		return null
	var subrace_id = _parse_string_name_field(data.get("subrace_id", null), false)
	if subrace_id == null:
		return null
	var age_years_variant: Variant = data.get("age_years", null)
	if age_years_variant is not int or int(age_years_variant) < 0:
		return null
	var birth_at_world_step_variant: Variant = data.get("birth_at_world_step", null)
	if birth_at_world_step_variant is not int or int(birth_at_world_step_variant) < 0:
		return null
	var age_profile_id = _parse_string_name_field(data.get("age_profile_id", null), false)
	if age_profile_id == null:
		return null
	var natural_age_stage_id = _parse_string_name_field(data.get("natural_age_stage_id", null), false)
	if natural_age_stage_id == null:
		return null
	var effective_age_stage_id = _parse_string_name_field(data.get("effective_age_stage_id", null), false)
	if effective_age_stage_id == null:
		return null
	var effective_age_stage_source_type = _parse_string_name_field(data.get("effective_age_stage_source_type", null), true)
	if effective_age_stage_source_type == null:
		return null
	var effective_age_stage_source_id = _parse_string_name_field(data.get("effective_age_stage_source_id", null), true)
	if effective_age_stage_source_id == null:
		return null
	var body_size_variant: Variant = data.get("body_size", null)
	if body_size_variant is not int:
		return null
	var body_size_value := int(body_size_variant)
	if body_size_value < 1:
		return null
	var body_size_category = _parse_string_name_field(data.get("body_size_category", null), false)
	if body_size_category == null:
		return null
	var versatility_pick = _parse_string_name_field(data.get("versatility_pick", null), true)
	if versatility_pick == null:
		return null
	var active_stage_advancement_modifier_ids_variant: Variant = data.get("active_stage_advancement_modifier_ids", null)
	if active_stage_advancement_modifier_ids_variant is not Array:
		return null
	var active_stage_advancement_modifier_ids = _parse_unique_string_name_array(active_stage_advancement_modifier_ids_variant)
	if active_stage_advancement_modifier_ids == null:
		return null
	var bloodline_id = _parse_string_name_field(data.get("bloodline_id", null), true)
	if bloodline_id == null:
		return null
	var bloodline_stage_id = _parse_string_name_field(data.get("bloodline_stage_id", null), true)
	if bloodline_stage_id == null:
		return null
	var ascension_id = _parse_string_name_field(data.get("ascension_id", null), true)
	if ascension_id == null:
		return null
	var ascension_stage_id = _parse_string_name_field(data.get("ascension_stage_id", null), true)
	if ascension_stage_id == null:
		return null
	var ascension_started_at_world_step_variant: Variant = data.get("ascension_started_at_world_step", null)
	if ascension_started_at_world_step_variant is not int or int(ascension_started_at_world_step_variant) < -1:
		return null
	var original_race_id_before_ascension = _parse_string_name_field(data.get("original_race_id_before_ascension", null), true)
	if original_race_id_before_ascension == null:
		return null
	var biological_age_years_variant: Variant = data.get("biological_age_years", null)
	if biological_age_years_variant is not int or int(biological_age_years_variant) < 0:
		return null
	var astral_memory_years_variant: Variant = data.get("astral_memory_years", null)
	if astral_memory_years_variant is not int or int(astral_memory_years_variant) < 0:
		return null

	var member_state := PARTY_MEMBER_STATE_SCRIPT.new()
	member_state.member_id = member_id
	member_state.display_name = display_name
	member_state.faction_id = faction_id
	member_state.portrait_id = portrait_id
	member_state.progression = UNIT_PROGRESS_SCRIPT.from_dict(progression_data)
	member_state.equipment_state = EQUIPMENT_STATE_SCRIPT.from_dict(equipment_state_data)
	member_state.control_mode = control_mode
	member_state.current_hp = int(current_hp_variant)
	member_state.current_mp = int(current_mp_variant)
	member_state.is_dead = bool(is_dead_variant)
	member_state.race_id = race_id
	member_state.subrace_id = subrace_id
	member_state.age_years = int(age_years_variant)
	member_state.birth_at_world_step = int(birth_at_world_step_variant)
	member_state.age_profile_id = age_profile_id
	member_state.natural_age_stage_id = natural_age_stage_id
	member_state.effective_age_stage_id = effective_age_stage_id
	member_state.effective_age_stage_source_type = effective_age_stage_source_type
	member_state.effective_age_stage_source_id = effective_age_stage_source_id
	member_state.body_size = body_size_value
	member_state.body_size_category = body_size_category
	member_state.versatility_pick = versatility_pick
	member_state.active_stage_advancement_modifier_ids = active_stage_advancement_modifier_ids
	member_state.bloodline_id = bloodline_id
	member_state.bloodline_stage_id = bloodline_stage_id
	member_state.ascension_id = ascension_id
	member_state.ascension_stage_id = ascension_stage_id
	member_state.ascension_started_at_world_step = int(ascension_started_at_world_step_variant)
	member_state.original_race_id_before_ascension = original_race_id_before_ascension
	member_state.biological_age_years = int(biological_age_years_variant)
	member_state.astral_memory_years = int(astral_memory_years_variant)

	if member_state.progression == null or member_state.equipment_state == null:
		return null
	if member_state.progression.unit_id == &"" or member_state.progression.unit_id != member_state.member_id:
		return null
	if member_state.progression.display_name.strip_edges().is_empty():
		return null

	return member_state


static func _parse_string_name_field(value: Variant, allow_empty: bool):
	var value_type := typeof(value)
	if value_type != TYPE_STRING and value_type != TYPE_STRING_NAME:
		return null
	var parsed_value := ProgressionDataUtils.to_string_name(value)
	if parsed_value == &"" and not allow_empty:
		return null
	return parsed_value


static func _parse_unique_string_name_array(values: Array):
	var parsed_values: Array[StringName] = []
	var seen_values: Dictionary = {}
	for raw_value in values:
		var parsed_value = _parse_string_name_field(raw_value, false)
		if parsed_value == null or seen_values.has(parsed_value):
			return null
		seen_values[parsed_value] = true
		parsed_values.append(parsed_value)
	return parsed_values


static func _has_exact_fields(data: Dictionary, expected_fields: Array[String]) -> bool:
	if data.size() != expected_fields.size():
		return false
	var expected_lookup: Dictionary = {}
	var seen_lookup: Dictionary = {}
	for field_name in expected_fields:
		expected_lookup[field_name] = true
	for key in data.keys():
		var key_type := typeof(key)
		if key_type != TYPE_STRING and key_type != TYPE_STRING_NAME:
			return false
		var key_string := String(key)
		if not expected_lookup.has(key_string):
			return false
		if seen_lookup.has(key_string):
			return false
		seen_lookup[key_string] = true
	return seen_lookup.size() == expected_lookup.size()


func _get_unit_base_attributes() -> UnitBaseAttributes:
	if progression == null:
		return null
	return progression.unit_base_attributes
