class_name SkillPassiveResolver
extends RefCounted

const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT = preload("res://scripts/systems/progression/skill_effective_max_level_rules.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const PassiveSourceContext = preload("res://scripts/systems/progression/passive_source_context.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const VAJRA_BODY_SKILL_ID: StringName = &"vajra_body"
const STATUS_VAJRA_BODY: StringName = &"vajra_body"
const VAJRA_BODY_NON_CORE_MAX_LEVEL := 9
const LAST_STAND_SKILL_ID: StringName = &"warrior_last_stand"
const STATUS_DEATH_WARD: StringName = &"death_ward"
const LAST_STAND_NON_CORE_MAX_LEVEL := 5
const SHOOTING_SPECIALIZATION_SKILL_ID: StringName = &"archer_shooting_specialization"
const STATUS_SHOOTING_SPECIALIZATION: StringName = &"archer_shooting_specialization"


static func apply_to_unit(unit_state: BattleUnitState, context: PassiveSourceContext, skill_defs: Dictionary = {}) -> void:
	if unit_state == null:
		return
	var progression_state = context.unit_progress if context != null else null
	_sync_vajra_body_status(unit_state, progression_state, skill_defs)
	_sync_last_stand_status(unit_state, progression_state, skill_defs)
	_sync_shooting_specialization_status(unit_state, progression_state, skill_defs)


static func _sync_vajra_body_status(unit_state: BattleUnitState, progression_state, skill_defs: Dictionary) -> void:
	var skill_progress = progression_state.get_skill_progress(VAJRA_BODY_SKILL_ID) if progression_state != null else null
	if skill_progress == null or not bool(skill_progress.is_learned):
		unit_state.erase_status_effect(STATUS_VAJRA_BODY)
		return
	var skill_level := _resolve_vajra_body_effective_level(skill_progress, progression_state, skill_defs)
	var passive_reduction := int(floor(float(skill_level + 1) / 2.0)) + 1
	var control_save_bonus := 0
	if skill_level >= 9:
		control_save_bonus = 2
	elif skill_level >= 7:
		control_save_bonus = 1
	var params := {
		"source_skill_id": String(VAJRA_BODY_SKILL_ID),
		"skill_level": skill_level,
		"passive_reduction": passive_reduction,
	}
	if control_save_bonus > 0:
		params["control_save_bonus"] = control_save_bonus
	if skill_level >= 10:
		params["forced_move_immune"] = true
	var status_entry = BATTLE_STATUS_EFFECT_STATE_SCRIPT.new()
	status_entry.status_id = STATUS_VAJRA_BODY
	status_entry.source_unit_id = unit_state.unit_id
	status_entry.power = passive_reduction
	status_entry.stacks = 1
	status_entry.duration = -1
	status_entry.params = params
	unit_state.set_status_effect(status_entry)


static func _resolve_vajra_body_effective_level(skill_progress, progression_state, skill_defs: Dictionary) -> int:
	var raw_level := maxi(int(skill_progress.skill_level), 0)
	var skill_def = skill_defs.get(VAJRA_BODY_SKILL_ID) as SkillDef
	if skill_def != null:
		var effective_max := SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT.get_effective_max_level(
			skill_def,
			skill_progress,
			progression_state
		)
		return clampi(raw_level, 0, effective_max)
	var fallback_max := 10 if bool(skill_progress.is_level_trigger_locked) else VAJRA_BODY_NON_CORE_MAX_LEVEL
	return clampi(raw_level, 0, fallback_max)


static func _sync_shooting_specialization_status(unit_state: BattleUnitState, progression_state, skill_defs: Dictionary) -> void:
	var skill_progress = progression_state.get_skill_progress(SHOOTING_SPECIALIZATION_SKILL_ID) if progression_state != null else null
	if skill_progress == null or not bool(skill_progress.is_learned) or not _is_skill_passive_active(progression_state, skill_progress):
		unit_state.erase_status_effect(STATUS_SHOOTING_SPECIALIZATION)
		return
	var skill_level := maxi(int(skill_progress.skill_level), 0)
	var status_id := STATUS_SHOOTING_SPECIALIZATION
	var status_power := 1
	var status_params := {
		"source_skill_id": String(SHOOTING_SPECIALIZATION_SKILL_ID),
		"skill_level": skill_level,
		"range_bonus": 1,
	}
	var skill_def: SkillDef = skill_defs.get(SHOOTING_SPECIALIZATION_SKILL_ID) as SkillDef
	if skill_def != null and skill_def.combat_profile != null:
		for effect_def in skill_def.combat_profile.passive_effect_defs:
			if effect_def == null:
				continue
			if effect_def.trigger_condition != &"battle_start":
				continue
			if effect_def.effect_type != &"status" and effect_def.effect_type != &"apply_status":
				continue
			if effect_def.status_id == &"":
				continue
			status_id = effect_def.status_id
			status_power = effect_def.power
			if effect_def.params != null:
				status_params = effect_def.params.duplicate(true)
			break

	status_params["source_skill_id"] = String(SHOOTING_SPECIALIZATION_SKILL_ID)
	status_params["skill_level"] = skill_level
	if not status_params.has("range_bonus"):
		status_params["range_bonus"] = 1
	var status_entry = BATTLE_STATUS_EFFECT_STATE_SCRIPT.new()
	status_entry.status_id = status_id
	status_entry.source_unit_id = unit_state.unit_id
	status_entry.power = status_power
	status_entry.stacks = 1
	status_entry.duration = -1
	status_entry.params = status_params
	unit_state.set_status_effect(status_entry)


static func _is_skill_passive_active(progression_state, skill_progress) -> bool:
	if skill_progress == null:
		return false
	if skill_progress.profession_granted_by == &"":
		return true
	if progression_state == null:
		return false
	var profession_progress = progression_state.get_profession_progress(skill_progress.profession_granted_by)
	if profession_progress == null:
		return false
	return profession_progress.is_active and not profession_progress.is_hidden and profession_progress.rank > 0


static func _sync_last_stand_status(unit_state: BattleUnitState, progression_state, skill_defs: Dictionary) -> void:
	var skill_progress = progression_state.get_skill_progress(LAST_STAND_SKILL_ID) if progression_state != null else null
	if skill_progress == null or not bool(skill_progress.is_learned):
		unit_state.erase_status_effect(STATUS_DEATH_WARD)
		return
	var max_status_level := 7 if bool(skill_progress.is_core) else LAST_STAND_NON_CORE_MAX_LEVEL
	var skill_level := clampi(int(skill_progress.skill_level), 0, max_status_level)
	var skill_def: SkillDef = skill_defs.get(LAST_STAND_SKILL_ID) as SkillDef
	if skill_def != null and skill_def.combat_profile != null:
		for effect_def in skill_def.combat_profile.passive_effect_defs:
			if effect_def == null:
				continue
			if effect_def.trigger_condition != &"battle_start":
				continue
			var min_level := maxi(int(effect_def.min_skill_level), 0)
			var max_level := int(effect_def.max_skill_level)
			if skill_level < min_level:
				continue
			if max_level >= 0 and skill_level > max_level:
				continue
			if effect_def.effect_type == &"status" or effect_def.effect_type == &"apply_status":
				if effect_def.status_id == &"":
					continue
				var configured_status = BATTLE_STATUS_EFFECT_STATE_SCRIPT.new()
				configured_status.status_id = effect_def.status_id
				configured_status.source_unit_id = unit_state.unit_id
				configured_status.power = effect_def.power
				configured_status.stacks = 1
				configured_status.duration = -1
				var configured_params := {}
				if effect_def.params != null:
					configured_params = effect_def.params.duplicate(true)
				configured_params["source_skill_id"] = String(LAST_STAND_SKILL_ID)
				configured_params["skill_level"] = skill_level
				configured_status.params = configured_params
				unit_state.set_status_effect(configured_status)
				return

	var params := {
		"source_skill_id": String(LAST_STAND_SKILL_ID),
		"skill_level": skill_level,
	}
	var status_entry = BATTLE_STATUS_EFFECT_STATE_SCRIPT.new()
	status_entry.status_id = STATUS_DEATH_WARD
	status_entry.source_unit_id = unit_state.unit_id
	status_entry.power = skill_level
	status_entry.stacks = 1
	status_entry.duration = -1
	status_entry.params = params
	unit_state.set_status_effect(status_entry)
