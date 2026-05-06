## 文件说明：该脚本属于建卡服务相关的服务脚本，集中维护 reroll 次数到出生幸运的烘焙规则。
## 审查重点：重点核对档位边界、溢出输入回退以及通过 AttributeService 写入受保护 custom stat 的方式。
## 备注：reroll -> hidden_luck_at_birth 烘焙必须由调用方显式 opt-in；队友模板写入或剧情改写不走该入口。

class_name CharacterCreationService
extends RefCounted

const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const UNIT_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_progress.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BODY_SIZE_RULES_SCRIPT = preload("res://scripts/systems/progression/body_size_rules.gd")

const HIDDEN_LUCK_AT_BIRTH_MAX := 2
const HIDDEN_LUCK_AT_BIRTH_MIN := -6
const INITIAL_HP_BASE := 14
const DEFAULT_SOURCE_ID: StringName = &"birth_roll"
const MAXIMUM_REROLL_TIER_MINIMUM := 10_000_000
const CREATION_OPTION_BAKE_REROLL_LUCK := "bake_reroll_luck"


static func calculate_initial_hp_max(constitution_value: int) -> int:
	return maxi(1, INITIAL_HP_BASE + ProgressionService.calculate_constitution_modifier(constitution_value) * 2)


static func create_member_from_character_creation_payload(
	member_id: StringName,
	payload: Dictionary,
	progression_content_source: Variant = null,
	options: Dictionary = {}
):
	var member_state = PARTY_MEMBER_STATE_SCRIPT.new()
	member_state.member_id = member_id
	member_state.progression = UNIT_PROGRESS_SCRIPT.new()
	member_state.progression.unit_id = member_id
	member_state.progression.unit_base_attributes = UNIT_BASE_ATTRIBUTES_SCRIPT.new()
	apply_character_creation_payload_to_member(member_state, payload, progression_content_source, options)
	return member_state


static func apply_character_creation_payload_to_member(
	member_state,
	payload: Dictionary,
	progression_content_source: Variant = null,
	options: Dictionary = {}
) -> bool:
	if member_state == null or payload == null or payload.is_empty():
		return false
	if member_state.progression == null:
		member_state.progression = UNIT_PROGRESS_SCRIPT.new()
	if member_state.progression.unit_id == &"":
		member_state.progression.unit_id = member_state.member_id
	if member_state.progression.unit_base_attributes == null:
		member_state.progression.unit_base_attributes = UNIT_BASE_ATTRIBUTES_SCRIPT.new()

	var display_name := String(payload.get("display_name", member_state.display_name)).strip_edges()
	if not display_name.is_empty():
		member_state.display_name = display_name
		member_state.progression.display_name = display_name

	var base_attributes = member_state.progression.unit_base_attributes
	for attribute_id in UnitBaseAttributes.BASE_ATTRIBUTE_IDS:
		if payload.has(String(attribute_id)):
			base_attributes.set_attribute_value(attribute_id, int(payload[String(attribute_id)]))

	_apply_identity_payload_to_member(member_state, payload, progression_content_source)
	if payload.has(String(ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD)):
		base_attributes.set_attribute_value(
			ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD,
			int(payload[String(ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD)])
		)

	if bool(options.get(CREATION_OPTION_BAKE_REROLL_LUCK, false)):
		var attribute_service = ATTRIBUTE_SERVICE_SCRIPT.new()
		attribute_service.setup(member_state.progression)
		var creation_service := CharacterCreationService.new()
		creation_service.bake_hidden_luck_at_birth(attribute_service, payload.get("reroll_count", 0))

	var constitution := int(base_attributes.get_attribute_value(UnitBaseAttributes.CONSTITUTION))
	var initial_hp_max := calculate_initial_hp_max(constitution)
	base_attributes.set_attribute_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, initial_hp_max)
	member_state.current_hp = initial_hp_max
	return true


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


static func _apply_identity_payload_to_member(member_state, payload: Dictionary, progression_content_source: Variant) -> void:
	member_state.race_id = _read_payload_string_name(payload, "race_id", member_state.race_id, false)
	member_state.subrace_id = _read_payload_string_name(payload, "subrace_id", member_state.subrace_id, false)
	member_state.age_years = _read_payload_nonnegative_int(payload, "age_years", member_state.age_years)
	member_state.birth_at_world_step = _read_payload_nonnegative_int(payload, "birth_at_world_step", member_state.birth_at_world_step)
	member_state.age_profile_id = _read_payload_string_name(payload, "age_profile_id", member_state.age_profile_id, false)
	member_state.natural_age_stage_id = _read_payload_string_name(payload, "natural_age_stage_id", member_state.natural_age_stage_id, false)
	member_state.effective_age_stage_id = _read_payload_string_name(payload, "effective_age_stage_id", member_state.effective_age_stage_id, false)
	member_state.effective_age_stage_source_type = _read_payload_string_name(payload, "effective_age_stage_source_type", member_state.effective_age_stage_source_type, true)
	member_state.effective_age_stage_source_id = _read_payload_string_name(payload, "effective_age_stage_source_id", member_state.effective_age_stage_source_id, true)
	member_state.body_size = maxi(_read_payload_nonnegative_int(payload, "body_size", member_state.body_size), 1)
	member_state.body_size_category = _read_payload_string_name(payload, "body_size_category", member_state.body_size_category, false)
	member_state.versatility_pick = _read_payload_string_name(payload, "versatility_pick", member_state.versatility_pick, true)
	if payload.has("active_stage_advancement_modifier_ids") and payload["active_stage_advancement_modifier_ids"] is Array:
		member_state.active_stage_advancement_modifier_ids = ProgressionDataUtils.to_string_name_array(payload["active_stage_advancement_modifier_ids"])
	member_state.bloodline_id = _read_payload_string_name(payload, "bloodline_id", member_state.bloodline_id, true)
	member_state.bloodline_stage_id = _read_payload_string_name(payload, "bloodline_stage_id", member_state.bloodline_stage_id, true)
	member_state.ascension_id = _read_payload_string_name(payload, "ascension_id", member_state.ascension_id, true)
	member_state.ascension_stage_id = _read_payload_string_name(payload, "ascension_stage_id", member_state.ascension_stage_id, true)
	if payload.has("ascension_started_at_world_step") and payload["ascension_started_at_world_step"] is int:
		member_state.ascension_started_at_world_step = maxi(int(payload["ascension_started_at_world_step"]), -1)
	member_state.original_race_id_before_ascension = _read_payload_string_name(payload, "original_race_id_before_ascension", member_state.original_race_id_before_ascension, true)
	member_state.biological_age_years = _read_payload_nonnegative_int(payload, "biological_age_years", member_state.biological_age_years)
	member_state.astral_memory_years = _read_payload_nonnegative_int(payload, "astral_memory_years", member_state.astral_memory_years)
	_refresh_member_body_size_from_identity(member_state, progression_content_source)


static func _refresh_member_body_size_from_identity(member_state, progression_content_source: Variant) -> bool:
	var category := _resolve_body_size_category_for_member(member_state, progression_content_source)
	if category == &"":
		return false
	var resolved_body_size := BODY_SIZE_RULES_SCRIPT.get_body_size_for_category(category)
	if member_state.body_size_category == category and int(member_state.body_size) == resolved_body_size:
		return false
	member_state.body_size_category = category
	member_state.body_size = resolved_body_size
	return true


static func _resolve_body_size_category_for_member(member_state, progression_content_source: Variant) -> StringName:
	if member_state == null:
		return &""
	if member_state.ascension_stage_id != &"":
		var ascension_stage_def = _get_content_def(progression_content_source, "get_ascension_stage_defs", "ascension_stage_defs", member_state.ascension_stage_id)
		if ascension_stage_def != null \
			and ascension_stage_def.body_size_category_override != &"" \
			and BODY_SIZE_RULES_SCRIPT.is_valid_body_size_category(ascension_stage_def.body_size_category_override):
			return ascension_stage_def.body_size_category_override
	var subrace_def = _get_content_def(progression_content_source, "get_subrace_defs", "subrace_defs", member_state.subrace_id)
	if subrace_def != null \
		and subrace_def.body_size_category_override != &"" \
		and BODY_SIZE_RULES_SCRIPT.is_valid_body_size_category(subrace_def.body_size_category_override):
		return subrace_def.body_size_category_override
	var race_def = _get_content_def(progression_content_source, "get_race_defs", "race_defs", member_state.race_id)
	if race_def != null and BODY_SIZE_RULES_SCRIPT.is_valid_body_size_category(race_def.body_size_category):
		return race_def.body_size_category
	return &""


static func _get_content_def(source: Variant, method_name: String, bucket_name: String, def_id: StringName):
	if source == null or def_id == &"":
		return null
	var bucket: Variant = {}
	if source is Dictionary:
		bucket = source.get(bucket_name, {})
	elif source is Object and source.has_method(method_name):
		bucket = source.call(method_name)
	if bucket is Dictionary:
		return bucket.get(def_id)
	return null


static func _read_payload_string_name(payload: Dictionary, field_name: String, fallback: StringName, allow_empty: bool) -> StringName:
	if not payload.has(field_name):
		return fallback
	var value: Variant = payload[field_name]
	var value_type := typeof(value)
	if value_type != TYPE_STRING and value_type != TYPE_STRING_NAME:
		return fallback
	var parsed := ProgressionDataUtils.to_string_name(value)
	if parsed == &"" and not allow_empty:
		return fallback
	return parsed


static func _read_payload_nonnegative_int(payload: Dictionary, field_name: String, fallback: int) -> int:
	if not payload.has(field_name) or payload[field_name] is not int:
		return fallback
	return maxi(int(payload[field_name]), 0)
