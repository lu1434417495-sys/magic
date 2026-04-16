class_name BattleAiContext
extends RefCounted

const BATTLE_PREVIEW_SCRIPT = preload("res://scripts/systems/battle_preview.gd")
const BATTLE_AI_SCORE_INPUT_SCRIPT = preload("res://scripts/systems/battle_ai_score_input.gd")
const BattlePreview = preload("res://scripts/systems/battle_preview.gd")
const BattleAiScoreInput = preload("res://scripts/systems/battle_ai_score_input.gd")

var state = null
var unit_state = null
var grid_service = null
var skill_defs: Dictionary = {}
var preview_callback: Callable = Callable()
var skill_score_input_callback: Callable = Callable()


func preview_command(command) -> BattlePreview:
	if not preview_callback.is_valid():
		return BATTLE_PREVIEW_SCRIPT.new()
	var preview = preview_callback.call(command)
	return preview if preview is BattlePreview else BATTLE_PREVIEW_SCRIPT.new()


func build_skill_score_input(skill_def, command, preview: BattlePreview, effect_defs: Array = [], metadata: Dictionary = {}) -> BattleAiScoreInput:
	if not skill_score_input_callback.is_valid():
		return null
	var score_input = skill_score_input_callback.call(self, skill_def, command, preview, effect_defs, metadata)
	return score_input if score_input is BattleAiScoreInput else null
