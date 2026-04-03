class_name ProgressionSerialization
extends RefCounted

const PlayerReputationState = preload("res://scripts/player/progression/player_reputation_state.gd")


static func serialize_player_progress(progress: PlayerProgress) -> Dictionary:
	if progress == null:
		return {}
	return progress.to_dict()


static func deserialize_player_progress(data: Dictionary) -> PlayerProgress:
	return PlayerProgress.from_dict(data)


static func serialize_player_base_attributes(attributes: PlayerBaseAttributes) -> Dictionary:
	if attributes == null:
		return {}
	return attributes.to_dict()


static func deserialize_player_base_attributes(data: Dictionary) -> PlayerBaseAttributes:
	return PlayerBaseAttributes.from_dict(data)


static func serialize_player_reputation_state(state: PlayerReputationState) -> Dictionary:
	if state == null:
		return {}
	return state.to_dict()


static func deserialize_player_reputation_state(data: Dictionary) -> PlayerReputationState:
	return PlayerReputationState.from_dict(data)


static func serialize_skill_progress(skill_progress: PlayerSkillProgress) -> Dictionary:
	if skill_progress == null:
		return {}
	return skill_progress.to_dict()


static func deserialize_skill_progress(data: Dictionary) -> PlayerSkillProgress:
	return PlayerSkillProgress.from_dict(data)


static func serialize_profession_progress(profession_progress: PlayerProfessionProgress) -> Dictionary:
	if profession_progress == null:
		return {}
	return profession_progress.to_dict()


static func deserialize_profession_progress(data: Dictionary) -> PlayerProfessionProgress:
	return PlayerProfessionProgress.from_dict(data)
