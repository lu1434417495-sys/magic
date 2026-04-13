class_name BattleAiContext
extends RefCounted

const BATTLE_PREVIEW_SCRIPT = preload("res://scripts/systems/battle_preview.gd")
const BattlePreview = preload("res://scripts/systems/battle_preview.gd")

var state = null
var unit_state = null
var grid_service = null
var skill_defs: Dictionary = {}
var preview_callback: Callable = Callable()


func preview_command(command) -> BattlePreview:
	if not preview_callback.is_valid():
		return BATTLE_PREVIEW_SCRIPT.new()
	var preview = preview_callback.call(command)
	return preview if preview is BattlePreview else BATTLE_PREVIEW_SCRIPT.new()
