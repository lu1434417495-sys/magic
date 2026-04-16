class_name BattleAiScoreInput
extends RefCounted

const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattlePreview = preload("res://scripts/systems/battle_preview.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

var command: BattleCommand = null
var skill_def: SkillDef = null
var preview: BattlePreview = null
var primary_coord: Vector2i = Vector2i(-1, -1)
var target_unit_ids: Array[StringName] = []
var target_coords: Array[Vector2i] = []
var target_count := 0
var estimated_damage := 0
var estimated_healing := 0
var estimated_status_count := 0
var estimated_terrain_effect_count := 0
var estimated_height_delta := 0
var estimated_hit_rate_percent := 100
var hit_payoff_score := 0
var ap_cost := 0
var mp_cost := 0
var stamina_cost := 0
var aura_cost := 0
var cooldown_tu := 0
var resource_cost_score := 0
var position_objective_kind: StringName = &"cast_distance"
var desired_min_distance := -1
var desired_max_distance := -1
var distance_to_primary_coord := -1
var position_objective_score := 0
var total_score := 0
