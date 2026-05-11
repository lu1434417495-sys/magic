extends RefCounted

const CharacterProgressionDelta = preload("res://scripts/systems/progression/character_progression_delta.gd")

var grants: Array[Dictionary] = []
var skill_used_events := 0


func record_achievement_event(
	_member_id: StringName,
	event_type: StringName,
	_amount: int = 1,
	_subject_id: StringName = &"",
	_meta: Dictionary = {}
) -> Array[StringName]:
	if event_type == &"skill_used":
		skill_used_events += 1
	return []


func grant_battle_mastery(member_id: StringName, skill_id: StringName, amount: int) -> CharacterProgressionDelta:
	grants.append({
		"member_id": member_id,
		"skill_id": skill_id,
		"amount": amount,
	})
	var delta := CharacterProgressionDelta.new()
	delta.member_id = member_id
	delta.mastery_changes.append({
		"skill_id": skill_id,
		"mastery_amount": amount,
	})
	return delta


func get_member_state(_member_id: StringName):
	return null
