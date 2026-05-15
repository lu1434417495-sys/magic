class_name BattleTargetTeamRules
extends RefCounted

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const CombatTargetTeamContentRules = preload("res://scripts/player/progression/combat_target_team_content_rules.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")


static func resolve_effect_target_filter(skill_def: SkillDef, effect_def: CombatEffectDef) -> StringName:
	if effect_def != null \
			and effect_def.effect_target_team_filter != CombatTargetTeamContentRules.EFFECT_TARGET_TEAM_FILTER_INHERIT:
		return effect_def.effect_target_team_filter
	if skill_def != null and skill_def.combat_profile != null:
		return skill_def.combat_profile.target_team_filter
	return &""


static func is_unit_valid_for_filter(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	target_team_filter: StringName,
	options: Dictionary = {}
) -> bool:
	if target_unit == null:
		return false
	if not bool(options.get("allow_dead_targets", false)) and not target_unit.is_alive:
		return false
	if source_unit != null and bool(options.get("madness_target_any_team", false)):
		var madness_filters := _resolve_madness_target_filters(options)
		if madness_filters.has(target_team_filter):
			return target_unit.unit_id != source_unit.unit_id
	match target_team_filter:
		CombatTargetTeamContentRules.TARGET_TEAM_FILTER_ANY:
			return true
		CombatTargetTeamContentRules.TARGET_TEAM_FILTER_SELF:
			return source_unit != null and target_unit.unit_id == source_unit.unit_id
		CombatTargetTeamContentRules.TARGET_TEAM_FILTER_ALLY:
			return source_unit != null and target_unit.faction_id == source_unit.faction_id
		CombatTargetTeamContentRules.TARGET_TEAM_FILTER_ENEMY:
			return source_unit != null and target_unit.faction_id != source_unit.faction_id
		_:
			return false


static func is_beneficial_filter(target_team_filter: StringName) -> bool:
	return target_team_filter == CombatTargetTeamContentRules.TARGET_TEAM_FILTER_ALLY \
		or target_team_filter == CombatTargetTeamContentRules.TARGET_TEAM_FILTER_SELF


static func is_enemy_filter(target_team_filter: StringName) -> bool:
	return target_team_filter == CombatTargetTeamContentRules.TARGET_TEAM_FILTER_ENEMY


static func _resolve_madness_target_filters(options: Dictionary) -> Array:
	var filters_variant = options.get("madness_target_filters", [
		CombatTargetTeamContentRules.TARGET_TEAM_FILTER_ALLY,
		CombatTargetTeamContentRules.TARGET_TEAM_FILTER_ENEMY,
	])
	if filters_variant is Array:
		return filters_variant
	return [
		CombatTargetTeamContentRules.TARGET_TEAM_FILTER_ALLY,
		CombatTargetTeamContentRules.TARGET_TEAM_FILTER_ENEMY,
	]
