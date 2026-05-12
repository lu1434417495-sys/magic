class_name MeteorSwarmCastContext
extends RefCounted

const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const MeteorSwarmProfile = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd")

var active_unit: BattleUnitState = null
var command: BattleCommand = null
var skill_def: SkillDef = null
var cast_variant: CombatCastVariantDef = null
var profile: MeteorSwarmProfile = null
var nominal_anchor_coord: Vector2i = Vector2i(-1, -1)
var final_anchor_coord: Vector2i = Vector2i(-1, -1)
var spell_control_context: Dictionary = {}
var drift_context: Dictionary = {}


func has_drift() -> bool:
	return final_anchor_coord != nominal_anchor_coord
