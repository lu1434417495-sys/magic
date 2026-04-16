class_name BattleAiDecision
extends RefCounted

const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleAiScoreInput = preload("res://scripts/systems/battle_ai_score_input.gd")

var command: BattleCommand = null
var brain_id: StringName = &""
var state_id: StringName = &""
var action_id: StringName = &""
var reason_text: String = ""
var score_bucket_id: StringName = &""
var skill_score_input: BattleAiScoreInput = null
