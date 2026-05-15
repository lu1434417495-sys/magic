class_name PracticeGrowthService
extends RefCounted

const SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT = preload("res://scripts/systems/progression/skill_effective_max_level_rules.gd")

const TRACK_MEDITATION: StringName = &"meditation"
const TRACK_CULTIVATION: StringName = &"cultivation"
const PRACTICE_TRACKS := [TRACK_MEDITATION, TRACK_CULTIVATION]

const TIER_BASIC := 0
const TIER_INTERMEDIATE := 1
const TIER_ADVANCED := 2
const TIER_ULTIMATE := 3

const TIER_NAME_TO_VALUE := {
	&"basic": TIER_BASIC,
	&"intermediate": TIER_INTERMEDIATE,
	&"advanced": TIER_ADVANCED,
	&"ultimate": TIER_ULTIMATE,
}

const TIER_VALUE_TO_NAME := {
	TIER_BASIC: &"basic",
	TIER_INTERMEDIATE: &"intermediate",
	TIER_ADVANCED: &"advanced",
	TIER_ULTIMATE: &"ultimate",
}

const MP_MAX_ATTR: StringName = &"mp_max"
const AURA_MAX_ATTR: StringName = &"aura_max"

var _skill_defs: Dictionary = {}
var _profession_defs: Dictionary = {}


func setup(skill_defs: Dictionary, profession_defs: Dictionary) -> void:
	_skill_defs = skill_defs
	_profession_defs = profession_defs


func get_track_type_for_skill(skill_id: StringName) -> StringName:
	var skill_def: SkillDef = _skill_defs.get(skill_id) as SkillDef
	if skill_def == null:
		return &""
	return _get_exclusive_practice_track(skill_def)


func _get_exclusive_practice_track(skill_def: SkillDef) -> StringName:
	if skill_def == null:
		return &""
	var matched_track: StringName = &""
	var matched_count := 0
	for track_type in PRACTICE_TRACKS:
		if skill_def.tags.has(track_type):
			matched_track = track_type
			matched_count += 1
	if matched_count != 1:
		return &""
	if skill_def.tags.size() != 1:
		return &""
	return matched_track


func get_practice_tier(skill_id: StringName) -> int:
	var skill_def: SkillDef = _skill_defs.get(skill_id) as SkillDef
	if skill_def == null:
		return -1
	return int(TIER_NAME_TO_VALUE.get(skill_def.practice_tier, -1))


func _has_valid_practice_tier(skill_id: StringName) -> bool:
	var skill_def: SkillDef = _skill_defs.get(skill_id) as SkillDef
	return skill_def != null and TIER_NAME_TO_VALUE.has(skill_def.practice_tier)


func get_active_practice_skill(unit_progress: UnitProgress, track_type: StringName) -> StringName:
	var active_skill_ids := _get_active_practice_skill_ids(unit_progress, track_type)
	if active_skill_ids.size() != 1:
		return &""
	return active_skill_ids[0]


func _get_active_practice_skill_ids(unit_progress: UnitProgress, track_type: StringName) -> Array[StringName]:
	var active_skill_ids: Array[StringName] = []
	if unit_progress == null:
		return active_skill_ids
	if not PRACTICE_TRACKS.has(track_type):
		return active_skill_ids
	for skill_key in ProgressionDataUtils.sorted_string_keys(unit_progress.skills):
		var skill_id := StringName(skill_key)
		var skill_progress: UnitSkillProgress = unit_progress.get_skill_progress(skill_id)
		if skill_progress == null or not skill_progress.is_learned:
			continue
		if get_track_type_for_skill(skill_id) == track_type:
			active_skill_ids.append(skill_id)
	return active_skill_ids


func can_learn_practice_skill(skill_id: StringName, unit_progress: UnitProgress) -> Dictionary:
	var track_type := get_track_type_for_skill(skill_id)
	if track_type == &"":
		return {"can_learn": false, "needs_replacement": false, "existing_skill_id": &""}
	if not _has_valid_practice_tier(skill_id):
		return {"can_learn": false, "needs_replacement": false, "existing_skill_id": &""}
	var existing_skill_ids := _get_active_practice_skill_ids(unit_progress, track_type)
	if existing_skill_ids.size() > 1:
		return {"can_learn": false, "needs_replacement": false, "existing_skill_id": &"", "error_code": &"ambiguous_existing_practice_track"}
	var existing_skill_id: StringName = existing_skill_ids[0] if existing_skill_ids.size() == 1 else &""
	if existing_skill_id == &"":
		return {"can_learn": true, "needs_replacement": false, "existing_skill_id": &""}
	if existing_skill_id == skill_id:
		return {"can_learn": false, "needs_replacement": false, "existing_skill_id": existing_skill_id}
	return {"can_learn": false, "needs_replacement": true, "existing_skill_id": existing_skill_id}


func calculate_replacement_level(
	old_skill_id: StringName,
	new_skill_id: StringName,
	unit_progress: UnitProgress
) -> int:
	var old_tier := get_practice_tier(old_skill_id)
	var new_tier := get_practice_tier(new_skill_id)
	if old_tier < 0 or new_tier < 0:
		return -1
	var old_skill_progress: UnitSkillProgress = unit_progress.get_skill_progress(old_skill_id)
	var old_level := 0
	if old_skill_progress != null:
		old_level = old_skill_progress.skill_level
	var new_skill_def: SkillDef = _skill_defs.get(new_skill_id) as SkillDef
	if new_skill_def == null:
		return 0
	var raw_new_level := old_level + (old_tier - new_tier)
	var max_level := maxi(new_skill_def.max_level, 0) if new_skill_def.max_level >= 0 else 999
	if new_skill_def.dynamic_max_level_stat_id != &"":
		var absolute_max := SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT.get_effective_absolute_max_level(new_skill_def, unit_progress)
		if absolute_max > 0:
			max_level = mini(max_level, absolute_max)
	return clampi(raw_new_level, 0, max_level)


func apply_replacement(
	new_skill_id: StringName,
	unit_progress: UnitProgress,
	formal_learning_verified: bool = false
) -> bool:
	if not formal_learning_verified:
		return false
	var track_type := get_track_type_for_skill(new_skill_id)
	if track_type == &"":
		return false
	var learn_result := can_learn_practice_skill(new_skill_id, unit_progress)
	if not learn_result.get("needs_replacement", false):
		return false
	var old_skill_id: StringName = learn_result.get("existing_skill_id", &"")
	if old_skill_id == &"":
		return false

	var predicted_level: int = calculate_replacement_level(old_skill_id, new_skill_id, unit_progress)
	if predicted_level < 0:
		return false

	_clear_replaced_skill_references(unit_progress, old_skill_id)
	unit_progress.remove_skill_progress(old_skill_id)

	var new_skill_progress: UnitSkillProgress = UnitSkillProgress.new()
	new_skill_progress.skill_id = new_skill_id
	new_skill_progress.is_learned = true
	new_skill_progress.skill_level = predicted_level
	unit_progress.set_skill_progress(new_skill_progress)

	return true


func _clear_replaced_skill_references(unit_progress: UnitProgress, old_skill_id: StringName) -> void:
	if unit_progress == null or old_skill_id == &"":
		return
	if unit_progress.active_level_trigger_core_skill_id == old_skill_id:
		unit_progress.active_level_trigger_core_skill_id = &""
	unit_progress.locked_level_trigger_skill_ids.erase(old_skill_id)
	var old_skill_progress := unit_progress.get_skill_progress(old_skill_id) as UnitSkillProgress
	if old_skill_progress != null:
		old_skill_progress.is_level_trigger_active = false
		old_skill_progress.is_level_trigger_locked = false
		unit_progress.set_skill_progress(old_skill_progress)
	for profession_key in ProgressionDataUtils.sorted_string_keys(unit_progress.professions):
		var profession_id := ProgressionDataUtils.to_string_name(profession_key)
		var profession_progress = unit_progress.get_profession_progress(profession_id)
		if profession_progress == null:
			continue
		if profession_progress.core_skill_ids.has(old_skill_id):
			profession_progress.remove_core_skill(old_skill_id)
			unit_progress.set_profession_progress(profession_progress)


func get_skill_learned_status(
	skill_id: StringName,
	unit_progress: UnitProgress
) -> Dictionary:
	var track_type := get_track_type_for_skill(skill_id)
	if track_type == &"":
		return {"is_practice_skill": false, "track_type": &"", "is_learned_direct": false, "needs_replacement": false, "existing_skill_id": &"", "predicted_level": 0}
	var result := can_learn_practice_skill(skill_id, unit_progress)
	result["is_practice_skill"] = true
	result["track_type"] = track_type
	if result.get("needs_replacement", false):
		result["predicted_level"] = calculate_replacement_level(
			result.get("existing_skill_id", &""),
			skill_id,
			unit_progress
		)
	return result


func inject_first_unlock_starting_values(
	member_state: PartyMemberState,
	track_type: StringName,
) -> void:
	if member_state == null or member_state.progression == null:
		return
	var unit_progress: UnitProgress = member_state.progression
	var base_attrs: UnitBaseAttributes = unit_progress.unit_base_attributes
	if base_attrs == null:
		return

	var existing_skill_id := get_active_practice_skill(unit_progress, track_type)
	if existing_skill_id == &"":
		return
	var skill_def: SkillDef = _skill_defs.get(existing_skill_id) as SkillDef
	if skill_def == null:
		return

	var growth: int = _calculate_daily_upper_limit_growth(unit_progress, existing_skill_id, track_type)

	match track_type:
		TRACK_MEDITATION:
			base_attrs.set_attribute_value(MP_MAX_ATTR, growth)
			member_state.current_mp = growth
		TRACK_CULTIVATION:
			base_attrs.set_attribute_value(AURA_MAX_ATTR, growth)
			member_state.current_aura = growth


func apply_daily_growth_to_member(
	member_state: PartyMemberState,
	days_elapsed: int,
) -> void:
	if member_state == null or member_state.progression == null or days_elapsed <= 0:
		return
	var unit_progress: UnitProgress = member_state.progression
	var base_attrs: UnitBaseAttributes = unit_progress.unit_base_attributes
	if base_attrs == null:
		return

	for track_type in PRACTICE_TRACKS:
		var skill_id := get_active_practice_skill(unit_progress, track_type)
		if skill_id == &"":
			continue
		var single_day_growth: int = _calculate_daily_upper_limit_growth(unit_progress, skill_id, track_type)
		var single_day_recovery: int = _calculate_daily_recovery(unit_progress, skill_id, track_type)

		match track_type:
			TRACK_MEDITATION:
				var current_max: int = base_attrs.get_attribute_value(MP_MAX_ATTR)
				base_attrs.set_attribute_value(MP_MAX_ATTR, current_max + single_day_growth * days_elapsed)
				member_state.current_mp = mini(
					member_state.current_mp + single_day_recovery * days_elapsed,
					base_attrs.get_attribute_value(MP_MAX_ATTR)
				)
			TRACK_CULTIVATION:
				var current_max: int = base_attrs.get_attribute_value(AURA_MAX_ATTR)
				base_attrs.set_attribute_value(AURA_MAX_ATTR, current_max + single_day_growth * days_elapsed)
				member_state.current_aura = mini(
					member_state.current_aura + single_day_recovery * days_elapsed,
					base_attrs.get_attribute_value(AURA_MAX_ATTR)
				)


func _calculate_daily_upper_limit_growth(
	unit_progress: UnitProgress,
	skill_id: StringName,
	track_type: StringName,
) -> int:
	var skill_progress: UnitSkillProgress = unit_progress.get_skill_progress(skill_id)
	if skill_progress == null:
		return 0
	var base_attrs: UnitBaseAttributes = unit_progress.unit_base_attributes
	if base_attrs == null:
		return 0

	var skill_level: int = skill_progress.skill_level
	var profession_bonus: int = _get_profession_whitelist_bonus(unit_progress, track_type)
	var knowledge_bonus: int = _get_knowledge_whitelist_bonus(unit_progress, track_type)

	match track_type:
		TRACK_MEDITATION:
			var intelligence := base_attrs.get_attribute_value(UnitBaseAttributes.INTELLIGENCE)
			var willpower := base_attrs.get_attribute_value(UnitBaseAttributes.WILLPOWER)
			return skill_level + (intelligence + willpower) / 4 + profession_bonus + knowledge_bonus
		TRACK_CULTIVATION:
			var strength := base_attrs.get_attribute_value(UnitBaseAttributes.STRENGTH)
			var willpower := base_attrs.get_attribute_value(UnitBaseAttributes.WILLPOWER)
			return skill_level + (strength + willpower) / 4 + profession_bonus + knowledge_bonus

	return 0


func _calculate_daily_recovery(
	unit_progress: UnitProgress,
	skill_id: StringName,
	track_type: StringName,
) -> int:
	var skill_progress: UnitSkillProgress = unit_progress.get_skill_progress(skill_id)
	if skill_progress == null:
		return 0
	var base_attrs: UnitBaseAttributes = unit_progress.unit_base_attributes
	if base_attrs == null:
		return 0

	var skill_level: int = skill_progress.skill_level
	var willpower: int = base_attrs.get_attribute_value(UnitBaseAttributes.WILLPOWER)
	var profession_bonus: int = _get_profession_whitelist_bonus(unit_progress, track_type)
	var knowledge_bonus: int = _get_knowledge_whitelist_bonus(unit_progress, track_type)

	return maxi(skill_level / 2 + willpower / 5 + profession_bonus / 2 + knowledge_bonus / 2, 1)


func _get_profession_whitelist_bonus(_unit_progress: UnitProgress, _track_type: StringName) -> int:
	return 0


func _get_knowledge_whitelist_bonus(_unit_progress: UnitProgress, _track_type: StringName) -> int:
	return 0
