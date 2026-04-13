class_name BattleAiDecision
extends RefCounted

const BattleCommand = preload("res://scripts/systems/battle_command.gd")

var command: BattleCommand = null
var brain_id: StringName = &""
var state_id: StringName = &""
var action_id: StringName = &""
var reason_text: String = ""
