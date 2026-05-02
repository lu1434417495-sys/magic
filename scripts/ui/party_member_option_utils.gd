class_name PartyMemberOptionUtils
extends RefCounted


static func get_dictionary_array(value: Variant) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if value is not Array:
		return result
	for entry_variant in value:
		if entry_variant is Dictionary:
			result.append(entry_variant)
	return result


static func get_party_state(window_data: Dictionary):
	var party_state_variant: Variant = window_data.get("party_state", null)
	if party_state_variant is Object and party_state_variant.has_method("get_member_state"):
		return party_state_variant
	return null


static func build_member_options(window_data: Dictionary) -> Array[Dictionary]:
	if window_data.has("member_options"):
		return _build_explicit_member_options(window_data["member_options"])

	var party_state = get_party_state(window_data)
	if party_state == null:
		return []

	var options_from_party: Array[Dictionary] = []
	var seen_ids: Dictionary = {}
	for member_id_variant in party_state.active_member_ids:
		_append_member_option(
			options_from_party,
			seen_ids,
			party_state,
			ProgressionDataUtils.to_string_name(member_id_variant),
			"上阵"
		)
	for member_id_variant in party_state.reserve_member_ids:
		_append_member_option(
			options_from_party,
			seen_ids,
			party_state,
			ProgressionDataUtils.to_string_name(member_id_variant),
			"替补"
		)
	return options_from_party


static func build_member_option_map(options: Array[Dictionary]) -> Dictionary:
	var member_map: Dictionary = {}
	for option_variant in options:
		var option: Dictionary = option_variant
		if get_member_option_display_name(option).is_empty():
			continue
		var member_id := ProgressionDataUtils.to_string_name(option.get("member_id", ""))
		if member_id != &"":
			member_map[member_id] = option
	return member_map


static func build_member_option_label(member_option: Dictionary) -> String:
	var display_name := get_member_option_display_name(member_option)
	if display_name.is_empty():
		return ""
	var roster_role := String(member_option.get("roster_role", ""))
	var is_leader := bool(member_option.get("is_leader", false))
	var current_hp := int(member_option.get("current_hp", 0))
	var current_mp := int(member_option.get("current_mp", 0))
	var prefix := "队长 · " if is_leader else ""
	var role_suffix := " · %s" % roster_role if not roster_role.is_empty() else ""
	return "%s%s%s  |  HP %d  MP %d" % [prefix, display_name, role_suffix, current_hp, current_mp]


static func resolve_default_member_id(
	window_data: Dictionary,
	member_option_map: Dictionary,
	member_options: Array[Dictionary]
) -> StringName:
	var explicit_default := ProgressionDataUtils.to_string_name(window_data.get("default_member_id", ""))
	if explicit_default != &"" and member_option_map.has(explicit_default):
		return explicit_default

	var selected_member_id := ProgressionDataUtils.to_string_name(window_data.get("selected_member_id", ""))
	if selected_member_id != &"" and member_option_map.has(selected_member_id):
		return selected_member_id

	var party_state = get_party_state(window_data)
	if party_state != null:
		if party_state.leader_member_id != &"" and member_option_map.has(party_state.leader_member_id):
			return party_state.leader_member_id
		for member_id_variant in party_state.active_member_ids:
			var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
			if member_id != &"" and member_option_map.has(member_id):
				return member_id
		for member_id_variant in party_state.reserve_member_ids:
			var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
			if member_id != &"" and member_option_map.has(member_id):
				return member_id

	for member_option_variant in member_options:
		var member_option: Dictionary = member_option_variant
		var member_id := ProgressionDataUtils.to_string_name(member_option.get("member_id", ""))
		if member_id != &"":
			return member_id
	return &""


static func _append_member_option(
	options: Array[Dictionary],
	seen_ids: Dictionary,
	party_state,
	member_id: StringName,
	default_role: String
) -> void:
	if member_id == &"" or seen_ids.has(member_id):
		return
	var member_state = party_state.get_member_state(member_id)
	if member_state == null:
		return
	var display_name := String(member_state.display_name).strip_edges()
	if display_name.is_empty():
		return
	seen_ids[member_id] = true
	options.append({
		"member_id": String(member_id),
		"display_name": display_name,
		"roster_role": default_role,
		"is_leader": party_state.leader_member_id == member_id,
		"current_hp": int(member_state.current_hp),
		"current_mp": int(member_state.current_mp),
	})


static func get_member_option_display_name(member_option: Dictionary) -> String:
	if not member_option.has("display_name") or member_option["display_name"] is not String:
		return ""
	return String(member_option["display_name"]).strip_edges()


static func _build_explicit_member_options(value: Variant) -> Array[Dictionary]:
	var options: Array[Dictionary] = []
	if value is not Array:
		return options
	for option_variant in value:
		if option_variant is not Dictionary:
			continue
		var option := (option_variant as Dictionary).duplicate(true)
		var display_name := get_member_option_display_name(option)
		if display_name.is_empty():
			continue
		option["display_name"] = display_name
		options.append(option)
	return options
