class_name WildEncounterRosterDef
extends Resource

@export var profile_id: StringName = &""
@export var display_name: String = ""
@export var initial_stage := 0
@export var growth_step_interval := 1
@export var suppression_steps_on_victory := 0
@export var stages: Array[Dictionary] = []


func get_max_stage() -> int:
	var max_stage := initial_stage
	for stage_data in stages:
		if stage_data is not Dictionary:
			continue
		max_stage = maxi(max_stage, int(stage_data.get("stage", initial_stage)))
	return max_stage


func get_stage_unit_entries(stage: int) -> Array[Dictionary]:
	var best_stage := -1
	var best_entries: Array[Dictionary] = []
	for stage_variant in stages:
		if stage_variant is not Dictionary:
			continue
		var stage_data: Dictionary = stage_variant
		var stage_index := int(stage_data.get("stage", initial_stage))
		if stage_index > stage or stage_index < best_stage:
			continue
		var entries: Array[Dictionary] = []
		for entry_variant in stage_data.get("unit_entries", []):
			if entry_variant is not Dictionary:
				continue
			entries.append((entry_variant as Dictionary).duplicate(true))
		best_stage = stage_index
		best_entries = entries
	return best_entries

func validate_schema(known_templates: Dictionary = {}) -> Array[String]:
	var errors: Array[String] = []
	if profile_id == &"":
		errors.append("Wild encounter roster is missing profile_id.")
		return errors
	if display_name.strip_edges().is_empty():
		errors.append("Wild encounter roster %s is missing display_name." % String(profile_id))
	if initial_stage < 0:
		errors.append("Wild encounter roster %s initial_stage must be >= 0." % String(profile_id))
	if growth_step_interval <= 0:
		errors.append("Wild encounter roster %s growth_step_interval must be >= 1." % String(profile_id))
	if suppression_steps_on_victory < 0:
		errors.append("Wild encounter roster %s suppression_steps_on_victory must be >= 0." % String(profile_id))
	if stages.is_empty():
		errors.append("Wild encounter roster %s must declare at least one stage." % String(profile_id))
		return errors

	var seen_stage_ids: Dictionary = {}
	for stage_variant in stages:
		if stage_variant is not Dictionary:
			errors.append("Wild encounter roster %s contains a non-Dictionary stage." % String(profile_id))
			continue
		var stage_data := stage_variant as Dictionary
		var stage_index := int(stage_data.get("stage", -1))
		if stage_index < 0:
			errors.append("Wild encounter roster %s declares an invalid stage index." % String(profile_id))
		elif seen_stage_ids.has(stage_index):
			errors.append("Wild encounter roster %s declares duplicate stage %d." % [String(profile_id), stage_index])
		else:
			seen_stage_ids[stage_index] = true
		var unit_entries_variant: Variant = stage_data.get("unit_entries", [])
		if unit_entries_variant is not Array or (unit_entries_variant as Array).is_empty():
			errors.append("Wild encounter roster %s stage %d must declare at least one unit entry." % [String(profile_id), stage_index])
			continue
		for entry_variant in unit_entries_variant:
			if entry_variant is not Dictionary:
				errors.append("Wild encounter roster %s stage %d contains a non-Dictionary unit entry." % [String(profile_id), stage_index])
				continue
			var entry_data := entry_variant as Dictionary
			var template_id := ProgressionDataUtils.to_string_name(entry_data.get("template_id", ""))
			var count := int(entry_data.get("count", 0))
			if template_id == &"":
				errors.append("Wild encounter roster %s stage %d contains a unit entry without template_id." % [String(profile_id), stage_index])
			elif not known_templates.has(template_id):
				errors.append(
					"Wild encounter roster %s stage %d references missing template %s." % [
						String(profile_id),
						stage_index,
						String(template_id),
					]
				)
			if count <= 0:
				errors.append("Wild encounter roster %s stage %d template %s must have count >= 1." % [
					String(profile_id),
					stage_index,
					String(template_id),
				])

	return errors
