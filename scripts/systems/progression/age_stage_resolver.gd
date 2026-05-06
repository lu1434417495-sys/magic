class_name AgeStageResolver
extends RefCounted

const SOURCE_TYPE_ASCENSION: StringName = &"ascension"
const SOURCE_TYPE_STAGE_ADVANCEMENT: StringName = &"stage_advancement"


static func resolve_effective_stage(
	member_state,
	age_profile: AgeProfileDef,
	stage_advancement_modifiers: Array = [],
	_bloodline_def: BloodlineDef = null,
	_bloodline_stage_def: BloodlineStageDef = null,
	ascension_def: AscensionDef = null,
	ascension_stage_def: AscensionStageDef = null
) -> Dictionary:
	var base_stage_id := _resolve_base_stage_id(member_state)
	if ascension_def != null \
			and ascension_stage_def != null \
			and bool(ascension_def.replaces_age_growth) \
			and ascension_stage_def.stage_id != &"":
		return _build_result(
			ascension_stage_def.stage_id,
			SOURCE_TYPE_ASCENSION,
			ascension_stage_def.stage_id
		)

	var resolved_result := _build_result(base_stage_id, &"", &"")
	var stage_order := _collect_age_stage_order(age_profile)
	var base_stage_index := stage_order.find(base_stage_id)
	var best_stage_index := base_stage_index
	for modifier_variant in stage_advancement_modifiers:
		var modifier := modifier_variant as StageAdvancementModifier
		if modifier == null:
			continue
		if not _modifier_applies_to_member(modifier, member_state):
			continue
		var modifier_result := _resolve_modifier_stage_result(
			modifier,
			base_stage_id,
			base_stage_index,
			stage_order
		)
		var modifier_stage_id := ProgressionDataUtils.to_string_name(modifier_result.get("stage_id", ""))
		if modifier_stage_id == &"" or modifier_stage_id == base_stage_id:
			continue
		var modifier_stage_index := int(modifier_result.get("stage_index", -1))
		if modifier_stage_index >= 0 and best_stage_index >= 0 and modifier_stage_index < best_stage_index:
			continue
		best_stage_index = modifier_stage_index
		resolved_result = _build_result(
			modifier_stage_id,
			SOURCE_TYPE_STAGE_ADVANCEMENT,
			modifier.modifier_id
		)

	return resolved_result


static func _resolve_base_stage_id(member_state) -> StringName:
	if member_state == null:
		return &"adult"
	var natural_stage_id: StringName = ProgressionDataUtils.to_string_name(member_state.natural_age_stage_id)
	if natural_stage_id != &"":
		return natural_stage_id
	var effective_stage_id: StringName = ProgressionDataUtils.to_string_name(member_state.effective_age_stage_id)
	return effective_stage_id if effective_stage_id != &"" else &"adult"


static func _collect_age_stage_order(age_profile: AgeProfileDef) -> Array[StringName]:
	var stage_order: Array[StringName] = []
	if age_profile == null:
		return stage_order
	for stage_rule in age_profile.stage_rules:
		if stage_rule == null or stage_rule.stage_id == &"":
			continue
		if stage_order.has(stage_rule.stage_id):
			continue
		stage_order.append(stage_rule.stage_id)
	return stage_order


static func _resolve_modifier_stage_result(
	modifier: StageAdvancementModifier,
	base_stage_id: StringName,
	base_stage_index: int,
	stage_order: Array[StringName]
) -> Dictionary:
	if modifier == null or int(modifier.stage_offset) <= 0:
		return {
			"stage_id": base_stage_id,
			"stage_index": base_stage_index,
		}
	if _uses_identity_stage_axis(modifier.target_axis):
		return {
			"stage_id": modifier.max_stage_id,
			"stage_index": -1,
		}
	if base_stage_index < 0 or stage_order.is_empty():
		return {
			"stage_id": modifier.max_stage_id if modifier.max_stage_id != &"" else base_stage_id,
			"stage_index": -1,
		}

	var target_index := mini(base_stage_index + int(modifier.stage_offset), stage_order.size() - 1)
	if modifier.max_stage_id != &"":
		var max_stage_index := stage_order.find(modifier.max_stage_id)
		if max_stage_index >= 0:
			target_index = mini(target_index, max_stage_index)
	return {
		"stage_id": stage_order[target_index],
		"stage_index": target_index,
	}


static func _uses_identity_stage_axis(target_axis: StringName) -> bool:
	return target_axis == StageAdvancementModifier.TARGET_AXIS_BLOODLINE \
		or target_axis == StageAdvancementModifier.TARGET_AXIS_DIVINE


static func _modifier_applies_to_member(modifier: StageAdvancementModifier, member_state) -> bool:
	if modifier == null or member_state == null:
		return false
	if not modifier.applies_to_race_ids.is_empty() and not modifier.applies_to_race_ids.has(member_state.race_id):
		return false
	if not modifier.applies_to_subrace_ids.is_empty() and not modifier.applies_to_subrace_ids.has(member_state.subrace_id):
		return false
	if not modifier.applies_to_bloodline_ids.is_empty() and not modifier.applies_to_bloodline_ids.has(member_state.bloodline_id):
		return false
	if not modifier.applies_to_ascension_ids.is_empty() and not modifier.applies_to_ascension_ids.has(member_state.ascension_id):
		return false
	return true


static func _build_result(stage_id: StringName, source_type: StringName, source_id: StringName) -> Dictionary:
	return {
		"stage_id": stage_id,
		"source_type": source_type,
		"source_id": source_id,
	}
