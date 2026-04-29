class_name BattleSkillMasteryService
extends RefCounted

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")

const BATTLE_RATING_SOURCE_TYPE: StringName = &"battle_rating"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"
const BOSS_TARGET_STAT_ID: StringName = &"boss_target"
const STATUS_VAJRA_BODY: StringName = &"vajra_body"
const VAJRA_BODY_SKILL_ID: StringName = &"vajra_body"
const WARRIOR_GUARD_SKILL_ID: StringName = &"warrior_guard"
const MASTERY_SOURCE_HEAVY_HIT_TAKEN: StringName = &"heavy_hit_taken"
const MASTERY_SOURCE_MAX_DAMAGE_DIE_TAKEN: StringName = &"max_damage_die_taken"
const MASTERY_SOURCE_ELITE_OR_BOSS_DAMAGE_TAKEN: StringName = &"elite_or_boss_damage_taken"

var _resolution_events: Array[Dictionary] = []


func clear() -> void:
	_resolution_events.clear()


func record_target_result(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	result: Dictionary,
	_effect_defs: Array = []
) -> void:
	if source_unit == null or target_unit == null or result == null:
		return
	if source_unit.source_member_id == &"":
		return
	if not _is_skill_mastery_qualifying_result(result, skill_def):
		return
	var amount := _resolve_skill_mastery_target_amount(source_unit, target_unit, skill_def)
	if amount <= 0:
		return
	_resolution_events.append({
		"target_unit_id": target_unit.unit_id,
		"amount": amount,
		"critical_hit": bool(result.get("critical_hit", false)),
		"skill_damage_dice_is_max": _result_has_skill_damage_die_event(result),
		"weapon_damage_dice_is_max": _result_has_weapon_dice_max_event(result),
	})


func record_bonus(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	base_amount: int
) -> void:
	if base_amount <= 0 or source_unit == null or target_unit == null or skill_def == null:
		return
	var amount := base_amount * _resolve_skill_mastery_target_amount(source_unit, target_unit, skill_def)
	if amount <= 0:
		return
	_resolution_events.append({
		"skill_id": skill_def.skill_id,
		"amount": amount,
	})


func resolve_active_skill_mastery_amount() -> int:
	var total := 0
	for event_variant in _resolution_events:
		if event_variant is not Dictionary:
			continue
		total += maxi(int((event_variant as Dictionary).get("amount", 0)), 0)
	return total


func build_vajra_body_mastery_grant(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	result: Dictionary,
	skill_defs: Dictionary
) -> Dictionary:
	if source_unit == null or target_unit == null or result == null:
		return {}
	if target_unit.source_member_id == &"" or not target_unit.is_alive:
		return {}
	if String(source_unit.faction_id) == String(target_unit.faction_id):
		return {}
	var status_entry = target_unit.get_status_effect(STATUS_VAJRA_BODY)
	if status_entry == null:
		return {}
	var mastery_source_ids := _collect_vajra_body_mastery_source_ids(source_unit, skill_def, result)
	var mastery_source_id := _resolve_first_allowed_skill_mastery_source(
		VAJRA_BODY_SKILL_ID,
		mastery_source_ids,
		skill_defs
	)
	if mastery_source_id == &"":
		return {}
	var qualifying_hits := _count_vajra_body_mastery_hits(result)
	if qualifying_hits <= 0:
		return {}
	var multiplier := _resolve_vajra_body_mastery_multiplier(source_unit, target_unit)
	var mastery_amount := qualifying_hits * multiplier
	if mastery_amount <= 0:
		return {}
	return {
		"member_id": target_unit.source_member_id,
		"skill_id": VAJRA_BODY_SKILL_ID,
		"amount": mastery_amount,
		"source_type": mastery_source_id,
		"source_label": "战斗受击",
		"reason_text": "金刚不坏：承受重击或高威胁命中",
		"allow_unlocks": true,
		"record_near_death_unbroken_manual": _is_vajra_body_low_hp_training_window(target_unit),
	}


func build_guard_mastery_grant_from_incoming_hit(
	attacker_unit: BattleUnitState,
	target_unit: BattleUnitState,
	effect_defs: Array,
	skill_defs: Dictionary
) -> Dictionary:
	if attacker_unit == null or target_unit == null or effect_defs.is_empty():
		return {}
	if target_unit.source_member_id == &"":
		return {}
	if not target_unit.status_effects.has(&"guarding"):
		return {}
	if not _effect_defs_have_physical_damage(effect_defs):
		return {}
	var guard_def := skill_defs.get(WARRIOR_GUARD_SKILL_ID) as SkillDef
	if guard_def == null:
		return {}
	if _get_skill_mastery_trigger_mode(guard_def) != &"incoming_physical_hit":
		return {}
	var amount := _resolve_incoming_skill_mastery_source_amount(attacker_unit, target_unit, guard_def)
	if amount <= 0:
		return {}
	return {
		"member_id": target_unit.source_member_id,
		"skill_id": WARRIOR_GUARD_SKILL_ID,
		"amount": amount,
		"source_type": &"battle",
		"source_label": "战斗",
		"reason_text": "",
		"allow_unlocks": true,
	}


func build_battle_rating_mastery_reward_entries(
	stats: Dictionary,
	score: int,
	rating_label: String
) -> Array[Dictionary]:
	var mastery_amount := resolve_battle_rating_mastery_amount(score)
	if mastery_amount <= 0:
		return []
	var reward_entries: Array[Dictionary] = []
	var cast_counts: Dictionary = stats.get("cast_counts", {})
	for skill_key in cast_counts.keys():
		var skill_id := ProgressionDataUtils.to_string_name(skill_key)
		if skill_id == &"" or int(cast_counts.get(skill_key, 0)) <= 0:
			continue
		reward_entries.append({
			"entry_type": "skill_mastery",
			"target_id": String(skill_id),
			"target_label": "",
			"amount": mastery_amount,
			"reason_text": "战斗评分 %d · %s" % [score, rating_label],
		})
	return reward_entries


func resolve_battle_rating_mastery_amount(score: int) -> int:
	if score >= 6:
		return 6
	if score >= 4:
		return 4
	if score >= 2:
		return 2
	return 0


func _is_skill_mastery_qualifying_result(result: Dictionary, skill_def: SkillDef = null) -> bool:
	if result == null or result.is_empty():
		return false
	var trigger_mode := _get_skill_mastery_trigger_mode(skill_def)
	match trigger_mode:
		&"weapon_attack_quality":
			return bool(result.get("attack_success", false)) \
				and (bool(result.get("critical_hit", false)) or _result_has_weapon_dice_max_event(result))
		&"damage_dealt":
			return _result_has_effective_damage_or_absorb(result)
		&"status_applied":
			return _result_has_status_applied(result)
		&"effect_applied":
			return bool(result.get("applied", false))
		&"incoming_physical_hit":
			return false
		&"skill_damage_dice_max":
			if not _result_has_effective_damage_or_absorb(result):
				return false
			return _result_has_skill_damage_die_event(result)
		_:
			if not _result_has_effective_damage_or_absorb(result):
				return false
			return _result_has_skill_damage_die_event(result)


func _get_skill_mastery_trigger_mode(skill_def: SkillDef) -> StringName:
	if skill_def == null or skill_def.combat_profile == null:
		return &"skill_damage_dice_max"
	var trigger_mode := ProgressionDataUtils.to_string_name(skill_def.combat_profile.mastery_trigger_mode)
	if trigger_mode == &"":
		return &"skill_damage_dice_max"
	return trigger_mode


func _get_skill_mastery_amount_mode(skill_def: SkillDef) -> StringName:
	if skill_def == null or skill_def.combat_profile == null:
		return &"per_target_rank"
	var amount_mode := ProgressionDataUtils.to_string_name(skill_def.combat_profile.mastery_amount_mode)
	if amount_mode == &"":
		return &"per_target_rank"
	return amount_mode


func _result_has_effective_damage_or_absorb(result: Dictionary) -> bool:
	return int(result.get("damage", result.get("hp_damage", 0))) > 0 or int(result.get("shield_absorbed", 0)) > 0


func _result_has_status_applied(result: Dictionary) -> bool:
	var status_effect_ids = result.get("status_effect_ids", [])
	return status_effect_ids is Array and not (status_effect_ids as Array).is_empty()


func _result_has_skill_damage_die_event(result: Dictionary) -> bool:
	if bool(result.get("skill_damage_dice_is_max", false)):
		return true
	var damage_events = result.get("damage_events", [])
	if damage_events is not Array:
		return false
	for event_variant in damage_events:
		if event_variant is Dictionary and bool((event_variant as Dictionary).get("skill_damage_dice_is_max", false)):
			return true
	return false


func _result_has_weapon_dice_max_event(result: Dictionary) -> bool:
	var damage_events = result.get("damage_events", [])
	if damage_events is not Array:
		return false
	for event_variant in damage_events:
		if event_variant is not Dictionary:
			continue
		var event := event_variant as Dictionary
		if bool(event.get("weapon_damage_dice_is_max", false)) \
			and ProgressionDataUtils.to_string_name(event.get("weapon_damage_dice_is_max_reason", "")) == &"weapon_dice_max":
			return true
	return false


func _effect_defs_have_physical_damage(effect_defs: Array) -> bool:
	for effect_variant in effect_defs:
		if effect_variant == null or not (effect_variant is Object):
			continue
		var effect_def = effect_variant
		if effect_def.effect_type != &"damage":
			continue
		var tag := ProgressionDataUtils.to_string_name(effect_def.damage_tag)
		if tag == &"physical_slash" or tag == &"physical_pierce" or tag == &"physical_blunt":
			return true
	return false


func _collect_vajra_body_mastery_source_ids(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	result: Dictionary
) -> Array[StringName]:
	var source_ids: Array[StringName] = []
	if not _result_has_vajra_body_mastery_event(result):
		return source_ids
	if _is_vajra_body_heavy_hit_skill(skill_def):
		source_ids.append(MASTERY_SOURCE_HEAVY_HIT_TAKEN)
	source_ids.append(MASTERY_SOURCE_MAX_DAMAGE_DIE_TAKEN)
	if _is_elite_or_boss_target(source_unit):
		source_ids.append(MASTERY_SOURCE_ELITE_OR_BOSS_DAMAGE_TAKEN)
	return source_ids


func _resolve_first_allowed_skill_mastery_source(
	skill_id: StringName,
	source_ids: Array[StringName],
	skill_defs: Dictionary
) -> StringName:
	if skill_id == &"" or source_ids.is_empty():
		return &""
	var skill_def := skill_defs.get(skill_id) as SkillDef
	if skill_def == null:
		return &""
	for source_id in source_ids:
		if source_id == &"":
			continue
		if skill_def.mastery_sources.is_empty() or skill_def.mastery_sources.has(source_id):
			return source_id
	return &""


func _count_vajra_body_mastery_hits(result: Dictionary) -> int:
	var damage_events = result.get("damage_events", [])
	if damage_events is not Array:
		return 0
	var count := 0
	for event_variant in damage_events:
		if event_variant is not Dictionary:
			continue
		var event := event_variant as Dictionary
		if not _is_vajra_body_mastery_event(event):
			continue
		count += 1
	return count


func _result_has_vajra_body_mastery_event(result: Dictionary) -> bool:
	if result == null:
		return false
	var damage_events = result.get("damage_events", [])
	if damage_events is not Array:
		return false
	for event_variant in damage_events:
		if event_variant is Dictionary and _is_vajra_body_mastery_event(event_variant as Dictionary):
			return true
	return false


func _is_vajra_body_mastery_event(event: Dictionary) -> bool:
	return bool(event.get("damage_dice_high_total_roll", false)) and int(event.get("hp_damage", 0)) > 0


func _is_vajra_body_heavy_hit_skill(skill_def: SkillDef) -> bool:
	if skill_def == null:
		return false
	if String(skill_def.skill_id).contains("heavy"):
		return true
	if skill_def.display_name.contains("重击"):
		return true
	return skill_def.tags.has(&"heavy")


func _resolve_vajra_body_mastery_multiplier(source_unit: BattleUnitState, target_unit: BattleUnitState) -> int:
	var multiplier := 1
	if _is_boss_target(source_unit):
		multiplier = 3
	elif _is_elite_or_boss_target(source_unit):
		multiplier = 2
	if _is_vajra_body_low_hp_training_window(target_unit):
		multiplier *= 2
	return multiplier


func _is_vajra_body_low_hp_training_window(unit_state: BattleUnitState) -> bool:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return false
	var hp_max := maxi(int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)), 1)
	return unit_state.current_hp > 0 and unit_state.current_hp * 3 < hp_max


func _resolve_skill_mastery_target_amount(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef = null
) -> int:
	if source_unit == null or target_unit == null:
		return 0
	if _get_skill_mastery_amount_mode(skill_def) != &"per_target_rank":
		return 0
	if source_unit.faction_id == target_unit.faction_id:
		return 0
	if target_unit.faction_id != &"enemy":
		return 0
	if _is_boss_target(target_unit):
		return 3
	if _is_elite_or_boss_target(target_unit):
		return 2
	return 1


func _resolve_incoming_skill_mastery_source_amount(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef
) -> int:
	if source_unit == null or target_unit == null:
		return 0
	if _get_skill_mastery_amount_mode(skill_def) != &"per_target_rank":
		return 0
	if source_unit.faction_id == target_unit.faction_id:
		return 0
	if source_unit.faction_id != &"enemy":
		return 0
	if _is_boss_target(source_unit):
		return 3
	if _is_elite_or_boss_target(source_unit):
		return 2
	return 1


func _is_elite_or_boss_target(unit_state: BattleUnitState) -> bool:
	return unit_state != null \
		and unit_state.attribute_snapshot != null \
		and int(unit_state.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 0


func _is_boss_target(unit_state: BattleUnitState) -> bool:
	return unit_state != null \
		and unit_state.attribute_snapshot != null \
		and (
			int(unit_state.attribute_snapshot.get_value(BOSS_TARGET_STAT_ID)) > 0
			or int(unit_state.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 1
		)
