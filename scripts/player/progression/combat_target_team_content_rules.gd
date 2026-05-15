class_name CombatTargetTeamContentRules
extends RefCounted

const TARGET_TEAM_FILTER_ENEMY: StringName = &"enemy"
const TARGET_TEAM_FILTER_ALLY: StringName = &"ally"
const TARGET_TEAM_FILTER_SELF: StringName = &"self"
const TARGET_TEAM_FILTER_ANY: StringName = &"any"
const EFFECT_TARGET_TEAM_FILTER_INHERIT: StringName = &""

const VALID_SKILL_TARGET_TEAM_FILTERS := {
	TARGET_TEAM_FILTER_ENEMY: true,
	TARGET_TEAM_FILTER_ALLY: true,
	TARGET_TEAM_FILTER_SELF: true,
	TARGET_TEAM_FILTER_ANY: true,
}

const VALID_EFFECT_TARGET_TEAM_FILTERS := {
	EFFECT_TARGET_TEAM_FILTER_INHERIT: true,
	TARGET_TEAM_FILTER_ENEMY: true,
	TARGET_TEAM_FILTER_ALLY: true,
	TARGET_TEAM_FILTER_SELF: true,
	TARGET_TEAM_FILTER_ANY: true,
}


static func is_valid_skill_target_team_filter(target_team_filter: StringName) -> bool:
	return VALID_SKILL_TARGET_TEAM_FILTERS.has(target_team_filter)


static func is_valid_effect_target_team_filter(effect_target_team_filter: StringName) -> bool:
	return VALID_EFFECT_TARGET_TEAM_FILTERS.has(effect_target_team_filter)


static func valid_skill_target_team_filter_label() -> String:
	return "enemy, ally, self, any"


static func valid_effect_target_team_filter_label() -> String:
	return "<inherit>, enemy, ally, self, any"
