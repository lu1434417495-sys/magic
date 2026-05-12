class_name MeteorSwarmPreviewFacts
extends "res://scripts/systems/battle/core/battle_special_profile_preview_facts.gd"

var impact_count := 0
var expected_target_count := 0
var expected_terrain_effect_count := 0
var friendly_fire_risk_percent := 0
var component_preview: Array[Dictionary] = []
var target_numeric_summary: Array[Dictionary] = []


func to_dict() -> Dictionary:
	var payload := super.to_dict()
	payload["impact_count"] = impact_count
	payload["expected_target_count"] = expected_target_count
	payload["expected_terrain_effect_count"] = expected_terrain_effect_count
	payload["friendly_fire_risk_percent"] = friendly_fire_risk_percent
	payload["component_preview"] = component_preview.duplicate(true)
	payload["target_numeric_summary"] = target_numeric_summary.duplicate(true)
	return payload
