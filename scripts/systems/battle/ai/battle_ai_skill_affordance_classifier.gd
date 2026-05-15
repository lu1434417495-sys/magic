class_name BattleAiSkillAffordanceClassifier
extends RefCounted

const BATTLE_TARGET_TEAM_RULES_SCRIPT = preload("res://scripts/systems/battle/rules/battle_target_team_rules.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const PATH_STEP_AOE_EFFECT_TYPE: StringName = &"path_step_aoe"
const METEOR_SWARM_PROFILE_ID: StringName = &"meteor_swarm"


func classify_skill(skill_def: SkillDef, skill_level: int = 1) -> Dictionary:
	var record := _empty_record(skill_def)
	if skill_def == null or skill_def.combat_profile == null or skill_def.skill_type != &"active":
		record["skip_reason"] = "passive_or_no_combat"
		return record

	var combat_profile = skill_def.combat_profile
	record["target_mode"] = ProgressionDataUtils.to_string_name(combat_profile.target_mode)
	record["target_filter"] = ProgressionDataUtils.to_string_name(combat_profile.target_team_filter)
	record["selection_mode"] = ProgressionDataUtils.to_string_name(combat_profile.target_selection_mode)
	record["team_intent"] = _resolve_team_intent(skill_def)

	_classify_variants(record, skill_def, skill_level)
	_classify_selection_mode(record, skill_def)
	_classify_effects_and_target_mode(record, skill_def)

	if not (record.get("affordances", []) as Array).is_empty() and not (record.get("action_families", []) as Array).is_empty():
		record["is_generatable"] = true
		record["skip_reason"] = ""
	else:
		record["is_generatable"] = false
		record["skip_reason"] = "unsupported_or_special"
	record["requires_positioning_action"] = _requires_positioning_action(record)
	return record


func _empty_record(skill_def: SkillDef) -> Dictionary:
	return {
		"skill_id": skill_def.skill_id if skill_def != null else &"",
		"is_generatable": false,
		"skip_reason": "",
		"team_intent": &"",
		"target_mode": &"",
		"target_filter": &"",
		"selection_mode": &"",
		"effect_roles": [],
		"affordances": [],
		"action_families": [],
		"requires_positioning_action": false,
		"variant_ids": [],
		"blocked_reason": "",
	}


func _classify_variants(record: Dictionary, skill_def: SkillDef, skill_level: int) -> void:
	var combat_profile = skill_def.combat_profile
	if ProgressionDataUtils.to_string_name(combat_profile.special_resolution_profile_id) == METEOR_SWARM_PROFILE_ID:
		_add_unique(record["affordances"], &"special_ground")
		_add_unique(record["affordances"], &"ground_hostile.aoe")
		_add_unique(record["action_families"], &"use_ground_skill")
	for cast_variant in combat_profile.get_unlocked_cast_variants(skill_level):
		var variant := cast_variant as CombatCastVariantDef
		if variant == null:
			continue
		if variant.variant_id != &"":
			_add_unique(record["variant_ids"], variant.variant_id)
		var has_charge := _variant_has_effect(variant, &"charge")
		var has_path_aoe := _variant_has_effect(variant, PATH_STEP_AOE_EFFECT_TYPE)
		if has_charge and has_path_aoe:
			_add_unique(record["effect_roles"], &"charge")
			_add_unique(record["effect_roles"], &"path_step_aoe")
			_add_unique(record["affordances"], &"charge_path_aoe")
			_add_unique(record["action_families"], &"use_charge_path_aoe")
		elif has_charge:
			_add_unique(record["effect_roles"], &"charge")
			_add_unique(record["affordances"], &"charge_engage")
			_add_unique(record["action_families"], &"use_charge")


func _classify_selection_mode(record: Dictionary, skill_def: SkillDef) -> void:
	var selection_mode := ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_selection_mode)
	if selection_mode == &"random_chain":
		_add_unique(record["affordances"], &"random_chain")
		_add_unique(record["action_families"], &"use_random_chain_skill")
		_add_unique(record["action_families"], &"move_to_range")
	elif selection_mode == &"multi_unit":
		_add_unique(record["affordances"], &"multi_unit")
		_add_unique(record["action_families"], &"use_multi_unit_skill")
		_add_unique(record["action_families"], &"move_to_multi_unit_skill_position")


func _classify_effects_and_target_mode(record: Dictionary, skill_def: SkillDef) -> void:
	var target_mode := ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_mode)
	var team_intent := ProgressionDataUtils.to_string_name(record.get("team_intent", &""))
	var has_damage := false
	var has_heal := false
	var has_control := false
	var has_ground_control := false
	var has_reposition := false
	for effect_def in _collect_effect_defs(skill_def):
		if effect_def == null:
			continue
		var effect_type := ProgressionDataUtils.to_string_name(effect_def.effect_type)
		if _is_damage_effect(effect_def):
			has_damage = true
			_add_unique(record["effect_roles"], &"damage")
		if _is_heal_effect(effect_def):
			has_heal = true
			_add_unique(record["effect_roles"], &"heal")
		if _is_control_effect(effect_def):
			has_control = true
			_add_unique(record["effect_roles"], &"control")
		if _is_ground_control_effect(effect_def):
			has_ground_control = true
			_add_unique(record["effect_roles"], &"ground_control")
		if effect_type == &"forced_move":
			has_reposition = true
			_add_unique(record["effect_roles"], &"forced_move")

	if target_mode == &"ground":
		if has_damage and team_intent != &"support":
			_add_unique(record["affordances"], &"ground_hostile.aoe")
		if has_ground_control or has_control:
			_add_unique(record["affordances"], &"ground_control")
			_add_unique(record["affordances"], &"terrain_control")
		if not _has_family(record, &"use_charge_path_aoe"):
			_add_unique(record["action_families"], &"use_ground_skill")
		return

	if target_mode == &"unit":
		if team_intent == &"support":
			if has_heal:
				_add_unique(record["affordances"], &"ally_heal")
			elif has_control or has_reposition:
				_add_unique(record["affordances"], &"self_or_ally_buff")
		elif has_damage:
			_add_unique(record["affordances"], &"unit_hostile.damage")
		elif has_control or has_reposition:
			_add_unique(record["affordances"], &"unit_hostile.control")
			if has_reposition:
				_add_unique(record["affordances"], &"displacement_control")
		if not _has_any_family(record, [&"use_charge", &"use_charge_path_aoe", &"use_random_chain_skill", &"use_multi_unit_skill"]):
			_add_unique(record["action_families"], &"use_unit_skill")


func _resolve_team_intent(skill_def: SkillDef) -> StringName:
	if skill_def == null or skill_def.combat_profile == null:
		return &""
	var filter := ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_team_filter)
	if BATTLE_TARGET_TEAM_RULES_SCRIPT.is_beneficial_filter(filter):
		return &"support"
	if BATTLE_TARGET_TEAM_RULES_SCRIPT.is_enemy_filter(filter):
		return &"hostile"
	for effect_def in _collect_effect_defs(skill_def):
		if effect_def == null:
			continue
		var effect_filter := ProgressionDataUtils.to_string_name(effect_def.effect_target_team_filter)
		if BATTLE_TARGET_TEAM_RULES_SCRIPT.is_enemy_filter(effect_filter):
			return &"hostile"
		if BATTLE_TARGET_TEAM_RULES_SCRIPT.is_beneficial_filter(effect_filter):
			return &"support"
	return &"neutral"


func _collect_effect_defs(skill_def: SkillDef) -> Array:
	var results: Array = []
	if skill_def == null or skill_def.combat_profile == null:
		return results
	for effect_def in skill_def.combat_profile.effect_defs:
		if effect_def != null:
			results.append(effect_def)
	for cast_variant in skill_def.combat_profile.cast_variants:
		var variant := cast_variant as CombatCastVariantDef
		if variant == null:
			continue
		for effect_def in variant.effect_defs:
			if effect_def != null:
				results.append(effect_def)
	return results


func _is_damage_effect(effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	var effect_type := ProgressionDataUtils.to_string_name(effect_def.effect_type)
	return effect_type == &"damage" or effect_type == &"chain_damage" or effect_type == PATH_STEP_AOE_EFFECT_TYPE


func _is_heal_effect(effect_def: CombatEffectDef) -> bool:
	return effect_def != null and ProgressionDataUtils.to_string_name(effect_def.effect_type) == &"heal"


func _is_control_effect(effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	var effect_type := ProgressionDataUtils.to_string_name(effect_def.effect_type)
	if effect_type in [&"status", &"apply_status", &"forced_move", &"terrain", &"height_delta", &"barrier"]:
		return true
	return effect_def.status_id != &"" or effect_def.save_failure_status_id != &""


func _is_ground_control_effect(effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	var effect_type := ProgressionDataUtils.to_string_name(effect_def.effect_type)
	return effect_type in [&"terrain", &"height_delta", &"path_step_aoe"] \
		or effect_def.terrain_effect_id != &"" \
		or int(effect_def.height_delta) != 0


func _variant_has_effect(cast_variant: CombatCastVariantDef, effect_type: StringName) -> bool:
	if cast_variant == null:
		return false
	for effect_def in cast_variant.effect_defs:
		if effect_def != null and ProgressionDataUtils.to_string_name(effect_def.effect_type) == effect_type:
			return true
	return false


func _requires_positioning_action(record: Dictionary) -> bool:
	var families = record.get("action_families", [])
	return families.has(&"move_to_range") or families.has(&"move_to_multi_unit_skill_position")


func _has_family(record: Dictionary, family: StringName) -> bool:
	return (record.get("action_families", []) as Array).has(family)


func _has_any_family(record: Dictionary, families: Array) -> bool:
	for family in families:
		if _has_family(record, ProgressionDataUtils.to_string_name(family)):
			return true
	return false


func _add_unique(target: Array, value: Variant) -> void:
	if value == null or target.has(value):
		return
	target.append(value)
