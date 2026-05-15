## 文件说明：该脚本属于身份 payload 运行时校验服务，负责校验角色身份字段与 progression 内容定义之间的组合关系。
## 审查重点：重点核对 race/subrace、bloodline/stage、ascension/stage 与 allowed identity 约束是否在建卡和读档入口统一执行。
## 备注：该服务不做旧档兼容、隐式推断或 fallback；非法身份组合应由调用入口拒绝。

class_name IdentityPayloadValidator
extends RefCounted

const BODY_SIZE_RULES_SCRIPT = preload("res://scripts/systems/progression/body_size_rules.gd")


static func validate_party_identity(party_state, content_source: Variant) -> Array[String]:
	var errors: Array[String] = []
	if party_state == null:
		errors.append("party identity payload is null")
		return errors
	var member_states_variant = _read_property(party_state, "member_states")
	if member_states_variant is not Dictionary:
		errors.append("party identity payload has no member_states dictionary")
		return errors
	var member_states: Dictionary = member_states_variant
	for member_state in member_states.values():
		errors.append_array(validate_member_identity(member_state, content_source))
	return errors


static func validate_member_identity(member_state, content_source: Variant) -> Array[String]:
	var errors: Array[String] = []
	if member_state == null:
		errors.append("member identity payload is null")
		return errors
	if content_source == null:
		errors.append("member %s identity validation requires content source" % _member_label(member_state))
		return errors

	var label := _member_label(member_state)
	var race_id := _member_string_name(member_state, "race_id")
	var subrace_id := _member_string_name(member_state, "subrace_id")
	var bloodline_id := _member_string_name(member_state, "bloodline_id")
	var bloodline_stage_id := _member_string_name(member_state, "bloodline_stage_id")
	var ascension_id := _member_string_name(member_state, "ascension_id")
	var ascension_stage_id := _member_string_name(member_state, "ascension_stage_id")

	var race_def = _validate_race(errors, label, race_id, content_source)
	var subrace_def = _validate_subrace(errors, label, subrace_id, content_source)
	_validate_race_subrace_pair(errors, label, race_id, subrace_id, race_def, subrace_def)
	_validate_bloodline_pair(errors, label, bloodline_id, bloodline_stage_id, content_source)
	_validate_ascension_pair(errors, label, race_id, subrace_id, bloodline_id, ascension_id, ascension_stage_id, content_source)
	return errors


static func resolve_body_size_category_for_member(member_state, content_source: Variant) -> StringName:
	if member_state == null:
		return &""
	var ascension_stage_id := _member_string_name(member_state, "ascension_stage_id")
	if ascension_stage_id != &"":
		var ascension_stage_def = _get_content_def(
			content_source,
			"get_ascension_stage_defs",
			"ascension_stage_defs",
			"ascension_stage",
			ascension_stage_id
		)
		var ascension_body_size := _def_string_name(ascension_stage_def, "body_size_category_override")
		if ascension_body_size != &"" and BODY_SIZE_RULES_SCRIPT.is_valid_body_size_category(ascension_body_size):
			return ascension_body_size

	var subrace_id := _member_string_name(member_state, "subrace_id")
	var subrace_def = _get_content_def(
		content_source,
		"get_subrace_defs",
		"subrace_defs",
		"subrace",
		subrace_id
	)
	var subrace_body_size := _def_string_name(subrace_def, "body_size_category_override")
	if subrace_body_size != &"" and BODY_SIZE_RULES_SCRIPT.is_valid_body_size_category(subrace_body_size):
		return subrace_body_size

	var race_id := _member_string_name(member_state, "race_id")
	var race_def = _get_content_def(
		content_source,
		"get_race_defs",
		"race_defs",
		"race",
		race_id
	)
	var race_body_size := _def_string_name(race_def, "body_size_category")
	if race_body_size != &"" and BODY_SIZE_RULES_SCRIPT.is_valid_body_size_category(race_body_size):
		return race_body_size
	return &""


static func refresh_member_body_size_from_identity(member_state, content_source: Variant) -> bool:
	var category := resolve_body_size_category_for_member(member_state, content_source)
	if category == &"":
		return false
	member_state.body_size_category = category
	member_state.body_size = BODY_SIZE_RULES_SCRIPT.get_body_size_for_category(category)
	return true


static func _validate_race(errors: Array[String], label: String, race_id: StringName, content_source: Variant):
	if race_id == &"":
		errors.append("member %s must have race_id" % label)
		return null
	var race_def = _get_content_def(content_source, "get_race_defs", "race_defs", "race", race_id)
	if race_def == null:
		errors.append("member %s references missing race %s" % [label, String(race_id)])
	return race_def


static func _validate_subrace(errors: Array[String], label: String, subrace_id: StringName, content_source: Variant):
	if subrace_id == &"":
		errors.append("member %s must have subrace_id" % label)
		return null
	var subrace_def = _get_content_def(content_source, "get_subrace_defs", "subrace_defs", "subrace", subrace_id)
	if subrace_def == null:
		errors.append("member %s references missing subrace %s" % [label, String(subrace_id)])
	return subrace_def


static func _validate_race_subrace_pair(
	errors: Array[String],
	label: String,
	race_id: StringName,
	subrace_id: StringName,
	race_def,
	subrace_def
) -> void:
	if race_def == null or subrace_def == null or race_id == &"" or subrace_id == &"":
		return
	var parent_race_id := _def_string_name(subrace_def, "parent_race_id")
	if parent_race_id != race_id:
		errors.append(
			"member %s subrace %s parent_race_id must be %s, got %s" % [
				label,
				String(subrace_id),
				String(race_id),
				String(parent_race_id),
			]
		)
	var race_subrace_ids := _def_string_name_array(race_def, "subrace_ids")
	if not race_subrace_ids.has(subrace_id):
		errors.append(
			"member %s race %s must list subrace %s in subrace_ids" % [
				label,
				String(race_id),
				String(subrace_id),
			]
		)


static func _validate_bloodline_pair(
	errors: Array[String],
	label: String,
	bloodline_id: StringName,
	bloodline_stage_id: StringName,
	content_source: Variant
) -> void:
	if bloodline_id == &"" and bloodline_stage_id == &"":
		return
	if bloodline_id == &"" or bloodline_stage_id == &"":
		errors.append("member %s bloodline_id and bloodline_stage_id must both be empty or both be set" % label)
		return

	var bloodline_def = _get_content_def(content_source, "get_bloodline_defs", "bloodline_defs", "bloodline", bloodline_id)
	var stage_def = _get_content_def(content_source, "get_bloodline_stage_defs", "bloodline_stage_defs", "bloodline_stage", bloodline_stage_id)
	if bloodline_def == null:
		errors.append("member %s references missing bloodline %s" % [label, String(bloodline_id)])
	if stage_def == null:
		errors.append("member %s references missing bloodline stage %s" % [label, String(bloodline_stage_id)])
	if bloodline_def == null or stage_def == null:
		return

	var declared_bloodline_id := _def_string_name(bloodline_def, "bloodline_id")
	var declared_stage_id := _def_string_name(stage_def, "stage_id")
	var stage_parent_bloodline_id := _def_string_name(stage_def, "bloodline_id")
	var bloodline_stage_ids := _def_string_name_array(bloodline_def, "stage_ids")
	if declared_bloodline_id != bloodline_id \
			or declared_stage_id != bloodline_stage_id \
			or stage_parent_bloodline_id != bloodline_id \
			or not bloodline_stage_ids.has(bloodline_stage_id):
		errors.append(
			"member %s bloodline_stage_id %s does not belong to bloodline %s" % [
				label,
				String(bloodline_stage_id),
				String(bloodline_id),
			]
		)


static func _validate_ascension_pair(
	errors: Array[String],
	label: String,
	race_id: StringName,
	subrace_id: StringName,
	bloodline_id: StringName,
	ascension_id: StringName,
	ascension_stage_id: StringName,
	content_source: Variant
) -> void:
	if ascension_id == &"" and ascension_stage_id == &"":
		return
	if ascension_id == &"" or ascension_stage_id == &"":
		errors.append("member %s ascension_id and ascension_stage_id must both be empty or both be set" % label)
		return

	var ascension_def = _get_content_def(content_source, "get_ascension_defs", "ascension_defs", "ascension", ascension_id)
	var stage_def = _get_content_def(content_source, "get_ascension_stage_defs", "ascension_stage_defs", "ascension_stage", ascension_stage_id)
	if ascension_def == null:
		errors.append("member %s references missing ascension %s" % [label, String(ascension_id)])
	if stage_def == null:
		errors.append("member %s references missing ascension stage %s" % [label, String(ascension_stage_id)])
	if ascension_def == null or stage_def == null:
		return

	var declared_ascension_id := _def_string_name(ascension_def, "ascension_id")
	var declared_stage_id := _def_string_name(stage_def, "stage_id")
	var stage_parent_ascension_id := _def_string_name(stage_def, "ascension_id")
	var ascension_stage_ids := _def_string_name_array(ascension_def, "stage_ids")
	if declared_ascension_id != ascension_id \
			or declared_stage_id != ascension_stage_id \
			or stage_parent_ascension_id != ascension_id \
			or not ascension_stage_ids.has(ascension_stage_id):
		errors.append(
			"member %s ascension_stage_id %s does not belong to ascension %s" % [
				label,
				String(ascension_stage_id),
				String(ascension_id),
			]
		)

	_validate_ascension_allowed_identity(errors, label, race_id, subrace_id, bloodline_id, ascension_id, ascension_def)


static func _validate_ascension_allowed_identity(
	errors: Array[String],
	label: String,
	race_id: StringName,
	subrace_id: StringName,
	bloodline_id: StringName,
	ascension_id: StringName,
	ascension_def
) -> void:
	var allowed_race_ids := _def_string_name_array(ascension_def, "allowed_race_ids")
	if not allowed_race_ids.is_empty() and not allowed_race_ids.has(race_id):
		errors.append("member %s ascension %s does not allow race %s" % [label, String(ascension_id), String(race_id)])

	var allowed_subrace_ids := _def_string_name_array(ascension_def, "allowed_subrace_ids")
	if not allowed_subrace_ids.is_empty() and not allowed_subrace_ids.has(subrace_id):
		errors.append("member %s ascension %s does not allow subrace %s" % [label, String(ascension_id), String(subrace_id)])

	var allowed_bloodline_ids := _def_string_name_array(ascension_def, "allowed_bloodline_ids")
	if not allowed_bloodline_ids.is_empty() and not allowed_bloodline_ids.has(bloodline_id):
		errors.append("member %s ascension %s does not allow bloodline %s" % [label, String(ascension_id), String(bloodline_id)])


static func _get_content_def(
	content_source: Variant,
	method_name: String,
	primary_bucket_name: String,
	alias_bucket_name: String,
	def_id: StringName
):
	if content_source == null or def_id == &"":
		return null
	var bucket := _get_content_bucket(content_source, method_name, primary_bucket_name, alias_bucket_name)
	return _lookup_bucket_entry(bucket, def_id)


static func _get_content_bucket(
	content_source: Variant,
	method_name: String,
	primary_bucket_name: String,
	alias_bucket_name: String
) -> Dictionary:
	if content_source is Dictionary:
		var primary_bucket: Variant = content_source.get(primary_bucket_name, {})
		if primary_bucket is Dictionary:
			return primary_bucket
		var alias_bucket: Variant = content_source.get(alias_bucket_name, {})
		if alias_bucket is Dictionary:
			return alias_bucket
	if content_source is Object and content_source.has_method(method_name):
		var method_bucket: Variant = content_source.call(method_name)
		if method_bucket is Dictionary:
			return method_bucket
	return {}


static func _lookup_bucket_entry(bucket: Dictionary, def_id: StringName):
	if bucket.has(def_id):
		return bucket.get(def_id)
	var text_id := String(def_id)
	if bucket.has(text_id):
		return bucket.get(text_id)
	return null


static func _member_label(member_state) -> String:
	var member_id := _member_string_name(member_state, "member_id")
	return String(member_id) if member_id != &"" else "<unknown>"


static func _member_string_name(member_state, property_name: String) -> StringName:
	return _to_string_name(_read_property(member_state, property_name))


static func _def_string_name(def, property_name: String) -> StringName:
	return _to_string_name(_read_property(def, property_name))


static func _def_string_name_array(def, property_name: String) -> Array[StringName]:
	var result: Array[StringName] = []
	var value = _read_property(def, property_name)
	if value is not Array:
		return result
	for item in value:
		var parsed := _to_string_name(item)
		if parsed != &"":
			result.append(parsed)
	return result


static func _read_property(source, property_name: String):
	if source == null:
		return null
	if source is Dictionary:
		var source_dict: Dictionary = source
		if source_dict.has(property_name):
			return source_dict.get(property_name)
		var property_key := StringName(property_name)
		if source_dict.has(property_key):
			return source_dict.get(property_key)
		return null
	if source is Object:
		return source.get(property_name)
	return null


static func _to_string_name(value: Variant) -> StringName:
	match typeof(value):
		TYPE_STRING_NAME:
			return StringName(String(value))
		TYPE_STRING:
			return StringName(String(value))
		_:
			return &""
