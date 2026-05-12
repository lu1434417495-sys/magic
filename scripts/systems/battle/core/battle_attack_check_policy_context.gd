class_name BattleAttackCheckPolicyContext
extends RefCounted

const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleRepeatAttackStageSpec = preload("res://scripts/systems/battle/core/battle_repeat_attack_stage_spec.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

var battle_state: BattleState = null
var attacker: BattleUnitState = null
var target: BattleUnitState = null
var skill_def: SkillDef = null
var cast_variant: CombatCastVariantDef = null
var roll_kind: StringName = &""
var check_route: StringName = &""
var trace_source: StringName = &""
var distance: int = -1
var force_hit_no_crit := false
var source_coord: Vector2i = Vector2i(-1, -1)
var target_coord: Vector2i = Vector2i(-1, -1)
var repeat_stage_spec: BattleRepeatAttackStageSpec = null
