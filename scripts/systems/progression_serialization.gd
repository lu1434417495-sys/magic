## 文件说明：该脚本属于成长序列化相关的业务脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name ProgressionSerialization
extends RefCounted

const UNIT_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_progress.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const UNIT_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_skill_progress.gd")
const UNIT_PROFESSION_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_profession_progress.gd")
const UNIT_REPUTATION_STATE_SCRIPT = preload("res://scripts/player/progression/unit_reputation_state.gd")
const ACHIEVEMENT_PROGRESS_STATE_SCRIPT = preload("res://scripts/player/progression/achievement_progress_state.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")
const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/pending_character_reward.gd")
const PENDING_CHARACTER_REWARD_ENTRY_SCRIPT = preload("res://scripts/systems/pending_character_reward_entry.gd")


static func serialize_unit_progress(progress: UnitProgress) -> Dictionary:
	if progress == null:
		return {}
	return progress.to_dict()


static func deserialize_unit_progress(data: Dictionary) -> UnitProgress:
	return UNIT_PROGRESS_SCRIPT.from_dict(data)


static func serialize_unit_base_attributes(attributes: UnitBaseAttributes) -> Dictionary:
	if attributes == null:
		return {}
	return attributes.to_dict()


static func deserialize_unit_base_attributes(data: Dictionary) -> UnitBaseAttributes:
	return UNIT_BASE_ATTRIBUTES_SCRIPT.from_dict(data)


static func serialize_unit_skill_progress(skill_progress: UnitSkillProgress) -> Dictionary:
	if skill_progress == null:
		return {}
	return skill_progress.to_dict()


static func deserialize_unit_skill_progress(data: Dictionary) -> UnitSkillProgress:
	return UNIT_SKILL_PROGRESS_SCRIPT.from_dict(data)


static func serialize_unit_profession_progress(profession_progress: UnitProfessionProgress) -> Dictionary:
	if profession_progress == null:
		return {}
	return profession_progress.to_dict()


static func deserialize_unit_profession_progress(data: Dictionary) -> UnitProfessionProgress:
	return UNIT_PROFESSION_PROGRESS_SCRIPT.from_dict(data)


static func serialize_unit_reputation_state(state: UnitReputationState) -> Dictionary:
	if state == null:
		return {}
	return state.to_dict()


static func deserialize_unit_reputation_state(data: Dictionary) -> UnitReputationState:
	return UNIT_REPUTATION_STATE_SCRIPT.from_dict(data)


static func serialize_achievement_progress_state(state) -> Dictionary:
	if state == null:
		return {}
	return state.to_dict()


static func deserialize_achievement_progress_state(data: Dictionary):
	return ACHIEVEMENT_PROGRESS_STATE_SCRIPT.from_dict(data)


static func serialize_party_member_state(member_state) -> Dictionary:
	if member_state == null:
		return {}
	return member_state.to_dict()


static func deserialize_party_member_state(data: Dictionary):
	return PARTY_MEMBER_STATE_SCRIPT.from_dict(data)


static func serialize_party_state(party_state) -> Dictionary:
	if party_state == null:
		return {}
	return party_state.to_dict()


static func deserialize_party_state(data: Dictionary):
	return PARTY_STATE_SCRIPT.from_dict(data)


static func serialize_pending_character_reward(reward) -> Dictionary:
	if reward == null:
		return {}
	return reward.to_dict()


static func deserialize_pending_character_reward(data: Dictionary):
	return PENDING_CHARACTER_REWARD_SCRIPT.from_dict(data)


static func serialize_pending_character_reward_entry(entry) -> Dictionary:
	if entry == null:
		return {}
	return entry.to_dict()


static func deserialize_pending_character_reward_entry(data: Dictionary):
	return PENDING_CHARACTER_REWARD_ENTRY_SCRIPT.from_dict(data)


static func serialize_encounter_anchor(encounter_anchor) -> Dictionary:
	if encounter_anchor == null:
		return {}
	return encounter_anchor.to_dict()


static func deserialize_encounter_anchor(data: Dictionary):
	return ENCOUNTER_ANCHOR_DATA_SCRIPT.from_dict(data)
