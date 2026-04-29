class_name BattleSimProfileDef
extends Resource

const BATTLE_AI_SCORE_PROFILE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_profile.gd")
const BattleAiScoreProfile = preload("res://scripts/systems/battle/ai/battle_ai_score_profile.gd")

@export var profile_id: StringName = &"baseline"
@export var display_name: String = "Baseline"
@export_multiline var description: String = ""
@export var ai_score_profile: BattleAiScoreProfile = BATTLE_AI_SCORE_PROFILE_SCRIPT.new()
@export var override_patches: Array = []


func to_dict() -> Dictionary:
	return {
		"profile_id": String(profile_id),
		"display_name": display_name,
		"description": description,
		"ai_score_profile": ai_score_profile.to_dict() if ai_score_profile != null else {},
		"override_patch_count": override_patches.size(),
	}
