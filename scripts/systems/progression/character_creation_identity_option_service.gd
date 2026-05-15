class_name CharacterCreationIdentityOptionService
extends RefCounted

const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const IDENTITY_PAYLOAD_VALIDATOR_SCRIPT = preload("res://scripts/systems/progression/identity_payload_validator.gd")


static func collect_creation_race_ids(content_source: Variant) -> Array[StringName]:
	var ids: Array[StringName] = []
	var race_defs := _get_content_bucket(content_source, "get_race_defs", "race_defs", "race")
	for race_id in _sorted_bucket_ids(race_defs):
		if not collect_subrace_ids_for_race(content_source, race_id).is_empty():
			ids.append(race_id)
	return ids


static func collect_subrace_ids_for_race(content_source: Variant, race_id: StringName) -> Array[StringName]:
	var ids: Array[StringName] = []
	if race_id == &"":
		return ids

	var race_def = _get_content_def(content_source, "get_race_defs", "race_defs", "race", race_id)
	if race_def == null:
		return ids

	var subrace_defs := _get_content_bucket(content_source, "get_subrace_defs", "subrace_defs", "subrace")
	for subrace_id in _def_string_name_array(race_def, "subrace_ids"):
		if subrace_id == &"" or ids.has(subrace_id):
			continue
		var subrace_def = _lookup_bucket_entry(subrace_defs, subrace_id)
		if subrace_def == null:
			continue
		if _def_string_name(subrace_def, "parent_race_id") != race_id:
			continue
		if not is_valid_creation_race_subrace_pair(content_source, race_id, subrace_id):
			continue
		ids.append(subrace_id)

	ids.sort_custom(func(a: StringName, b: StringName) -> bool:
		return String(a) < String(b)
	)
	return ids


static func choose_race_id(
	content_source: Variant,
	current_id: StringName,
	default_id: StringName = &"human"
) -> StringName:
	var candidates := collect_creation_race_ids(content_source)
	if current_id != &"" and candidates.has(current_id):
		return current_id
	if default_id != &"" and candidates.has(default_id):
		return default_id
	if not candidates.is_empty():
		return candidates[0]
	return &""


static func choose_subrace_id(content_source: Variant, race_id: StringName, current_id: StringName) -> StringName:
	var candidates := collect_subrace_ids_for_race(content_source, race_id)
	if current_id != &"" and candidates.has(current_id):
		return current_id

	var race_def = _get_content_def(content_source, "get_race_defs", "race_defs", "race", race_id)
	var default_subrace_id := _def_string_name(race_def, "default_subrace_id")
	if default_subrace_id != &"" and candidates.has(default_subrace_id):
		return default_subrace_id
	if not candidates.is_empty():
		return candidates[0]
	return &""


static func is_valid_creation_race_subrace_pair(
	content_source: Variant,
	race_id: StringName,
	subrace_id: StringName
) -> bool:
	if content_source == null or race_id == &"" or subrace_id == &"":
		return false
	var member_state = PARTY_MEMBER_STATE_SCRIPT.new()
	member_state.member_id = &"character_creation_candidate"
	member_state.race_id = race_id
	member_state.subrace_id = subrace_id
	member_state.bloodline_id = &""
	member_state.bloodline_stage_id = &""
	member_state.ascension_id = &""
	member_state.ascension_stage_id = &""
	return IDENTITY_PAYLOAD_VALIDATOR_SCRIPT.validate_member_identity(member_state, content_source).is_empty()


static func _sorted_bucket_ids(bucket: Dictionary) -> Array[StringName]:
	var ids: Array[StringName] = []
	for key in bucket.keys():
		var id := StringName(String(key))
		if id != &"" and not ids.has(id):
			ids.append(id)
	ids.sort_custom(func(a: StringName, b: StringName) -> bool:
		return String(a) < String(b)
	)
	return ids


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
