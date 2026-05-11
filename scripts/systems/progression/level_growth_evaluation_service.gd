class_name LevelGrowthEvaluationService
extends RefCounted

const LOCK_HIT_BONUS_DEFAULT := 1
const SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT = preload("res://scripts/systems/progression/skill_effective_max_level_rules.gd")

var _skill_defs: Dictionary = {}


func setup(skill_defs: Dictionary) -> void:
	_skill_defs = skill_defs


func set_active_trigger_core_skill(member_state: PartyMemberState, skill_id: StringName) -> Dictionary:
	if member_state == null or member_state.progression == null:
		return _fail("invalid_member_state")
	var unit_progress: UnitProgress = member_state.progression
	var skill_progress: UnitSkillProgress = unit_progress.get_skill_progress(skill_id)
	if skill_progress == null or not skill_progress.is_learned:
		return _fail("skill_not_learned")
	if not skill_progress.is_core:
		return _fail("skill_not_core")
	if skill_progress.is_level_trigger_locked:
		return _fail("skill_already_locked")
	if unit_progress.locked_level_trigger_skill_ids.has(skill_id):
		return _fail("skill_already_locked")

	var previous_active := unit_progress.active_level_trigger_core_skill_id
	if previous_active != &"" and previous_active != skill_id:
		var prev_skill_progress: UnitSkillProgress = unit_progress.get_skill_progress(previous_active)
		if prev_skill_progress != null:
			prev_skill_progress.is_level_trigger_active = false

	unit_progress.active_level_trigger_core_skill_id = skill_id
	skill_progress.is_level_trigger_active = true
	return {"ok": true, "skill_id": skill_id, "previous_active": previous_active}


func clear_active_trigger_core_skill(member_state: PartyMemberState) -> Dictionary:
	if member_state == null or member_state.progression == null:
		return _fail("invalid_member_state")
	var unit_progress: UnitProgress = member_state.progression
	var current_active := unit_progress.active_level_trigger_core_skill_id
	if current_active != &"":
		var skill_progress: UnitSkillProgress = unit_progress.get_skill_progress(current_active)
		if skill_progress != null:
			skill_progress.is_level_trigger_active = false
	unit_progress.active_level_trigger_core_skill_id = &""
	return {"ok": true}


func has_active_trigger_core_skill(member_state: PartyMemberState) -> bool:
	if member_state == null or member_state.progression == null:
		return false
	return member_state.progression.active_level_trigger_core_skill_id != &""


func get_trigger_skill_growth_progress(member_state: PartyMemberState) -> Dictionary:
	if member_state == null or member_state.progression == null:
		return {}
	var unit_progress: UnitProgress = member_state.progression
	var trigger_skill_id := unit_progress.active_level_trigger_core_skill_id
	if trigger_skill_id == &"":
		return {}
	var skill_def: SkillDef = _skill_defs.get(trigger_skill_id) as SkillDef
	if skill_def == null:
		return {}
	return skill_def.attribute_growth_progress.duplicate()


func is_active_trigger_ready_for_level_up(member_state: PartyMemberState) -> bool:
	if member_state == null or member_state.progression == null:
		return false
	var unit_progress: UnitProgress = member_state.progression
	var trigger_skill_id := unit_progress.active_level_trigger_core_skill_id
	if trigger_skill_id == &"":
		return false
	var skill_progress: UnitSkillProgress = unit_progress.get_skill_progress(trigger_skill_id)
	var skill_def: SkillDef = _skill_defs.get(trigger_skill_id) as SkillDef
	if skill_progress == null or skill_def == null:
		return false
	if not skill_progress.is_learned or not skill_progress.is_core:
		return false
	if skill_progress.is_level_trigger_locked:
		return false
	return SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT.is_at_effective_max_level(skill_def, skill_progress, unit_progress)


func apply_level_up(member_state: PartyMemberState) -> Dictionary:
	if member_state == null or member_state.progression == null:
		return _fail("invalid_member_state")
	var unit_progress: UnitProgress = member_state.progression

	var trigger_skill_id := unit_progress.active_level_trigger_core_skill_id
	if trigger_skill_id == &"":
		return _fail("no_active_trigger_core_skill")

	var skill_progress: UnitSkillProgress = unit_progress.get_skill_progress(trigger_skill_id)
	if skill_progress == null:
		return _fail("trigger_skill_not_found")
	if skill_progress.is_level_trigger_locked:
		return _fail("trigger_skill_already_locked")
	if not is_active_trigger_ready_for_level_up(member_state):
		return _fail("trigger_skill_not_ready")

	skill_progress.is_level_trigger_active = false
	skill_progress.is_level_trigger_locked = true
	skill_progress.bonus_to_hit_from_lock = LOCK_HIT_BONUS_DEFAULT
	unit_progress.active_level_trigger_core_skill_id = &""
	if not unit_progress.locked_level_trigger_skill_ids.has(trigger_skill_id):
		unit_progress.locked_level_trigger_skill_ids.append(trigger_skill_id)

	return {"ok": true, "trigger_core_skill_id": trigger_skill_id}


func _fail(reason: String) -> Dictionary:
	return {"ok": false, "error": reason}
