class_name BattleSimContentProvider
extends RefCounted

const PROGRESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/progression_content_registry.gd")
const ENEMY_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/enemies/enemy_content_registry.gd")

var _progression_content_registry = PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
var _enemy_content_registry = ENEMY_CONTENT_REGISTRY_SCRIPT.new()


func get_skill_defs() -> Dictionary:
	return _progression_content_registry.get_skill_defs()


func get_enemy_templates() -> Dictionary:
	return _enemy_content_registry.get_enemy_templates()


func get_enemy_ai_brains() -> Dictionary:
	return _enemy_content_registry.get_enemy_ai_brains()
