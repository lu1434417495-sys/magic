## 文件说明：该脚本属于队伍状态相关的状态数据脚本，集中维护版本、队长成员唯一标识、激活成员标识列表等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name PartyState
extends RefCounted

const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const UNIT_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_progress.gd")
const WAREHOUSE_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_state.gd")
const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/pending_character_reward.gd")
const PendingCharacterReward = PENDING_CHARACTER_REWARD_SCRIPT

## 字段说明：记录版本，会参与成长规则判定、序列化和界面展示。
var version := 2
## 字段说明：记录队长成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var leader_member_id: StringName = &""
## 字段说明：保存激活成员标识列表，便于批量遍历、交叉查找和界面展示。
var active_member_ids: Array = []
## 字段说明：保存预备成员标识列表，便于批量遍历、交叉查找和界面展示。
var reserve_member_ids: Array = []
## 字段说明：缓存成员状态集合字典，集中保存可按键查询的运行时数据。
var member_states: Dictionary = {}
## 字段说明：保存待处理角色奖励列表，便于顺序遍历、批量展示、批量运算和整体重建。
var pending_character_rewards: Array[PendingCharacterReward] = []
## 字段说明：记录仓库状态，会参与成长规则判定、序列化和界面展示。
var warehouse_state = WAREHOUSE_STATE_SCRIPT.new()


func get_member_state(member_id: StringName):
	return member_states.get(member_id)


func set_member_state(member_state) -> void:
	if member_state == null or member_state.member_id == &"":
		return
	member_states[member_state.member_id] = member_state


func remove_member_state(member_id: StringName) -> void:
	member_states.erase(member_id)


func enqueue_pending_character_reward(reward) -> void:
	if reward == null or reward is not PendingCharacterReward:
		return
	var typed_reward: PendingCharacterReward = reward
	if typed_reward.is_empty():
		return
	pending_character_rewards.append(typed_reward)


func get_pending_character_reward(reward_id: StringName):
	for reward in pending_character_rewards:
		if reward != null and reward.reward_id == reward_id:
			return reward
	return null


func get_next_pending_character_reward():
	if pending_character_rewards.is_empty():
		return null
	return pending_character_rewards[0]


func remove_pending_character_reward(reward_id: StringName) -> bool:
	for index in range(pending_character_rewards.size()):
		var reward = pending_character_rewards[index]
		if reward == null or reward.reward_id != reward_id:
			continue
		pending_character_rewards.remove_at(index)
		return true
	return false


func to_dict() -> Dictionary:
	var member_states_data: Dictionary = {}
	for key in ProgressionDataUtils.sorted_string_keys(member_states):
		var member_id = StringName(key)
		var member_state = get_member_state(member_id)
		if member_state != null:
			member_states_data[key] = member_state.to_dict()

	var pending_reward_data: Array[Dictionary] = []
	for reward in pending_character_rewards:
		if reward == null:
			continue
		pending_reward_data.append(reward.to_dict())

	return {
		"version": version,
		"leader_member_id": String(leader_member_id),
		"active_member_ids": ProgressionDataUtils.string_name_array_to_string_array(
			ProgressionDataUtils.to_string_name_array(active_member_ids)
		),
		"reserve_member_ids": ProgressionDataUtils.string_name_array_to_string_array(
			ProgressionDataUtils.to_string_name_array(reserve_member_ids)
		),
		"member_states": member_states_data,
		"pending_character_rewards": pending_reward_data,
		"warehouse_state": warehouse_state.to_dict() if warehouse_state != null else {},
	}


static func from_dict(data: Dictionary):
	var party_state := PARTY_STATE_SCRIPT.new()
	party_state.version = maxi(int(data.get("version", 1)), 2)
	party_state.leader_member_id = ProgressionDataUtils.to_string_name(data.get("leader_member_id", ""))
	party_state.active_member_ids = ProgressionDataUtils.to_string_name_array(data.get("active_member_ids", []))
	party_state.reserve_member_ids = ProgressionDataUtils.to_string_name_array(data.get("reserve_member_ids", []))
	var warehouse_state_data: Variant = data.get("warehouse_state", {})
	if warehouse_state_data is Dictionary:
		party_state.warehouse_state = WAREHOUSE_STATE_SCRIPT.from_dict(warehouse_state_data)
	else:
		party_state.warehouse_state = WAREHOUSE_STATE_SCRIPT.new()

	var member_states_data: Variant = data.get("member_states", {})
	if member_states_data is Dictionary:
		for key in member_states_data.keys():
			var member_state = PARTY_MEMBER_STATE_SCRIPT.from_dict(member_states_data[key])
			if member_state.member_id == &"":
				member_state.member_id = ProgressionDataUtils.to_string_name(key)
			if member_state.progression == null:
				member_state.progression = UNIT_PROGRESS_SCRIPT.new()
			if member_state.progression.unit_id == &"":
				member_state.progression.unit_id = member_state.member_id
			if member_state.progression.display_name.is_empty():
				member_state.progression.display_name = member_state.display_name
			party_state.member_states[member_state.member_id] = member_state

	var pending_rewards_variant: Variant = data.get("pending_character_rewards", data.get("pending_mastery_rewards", []))
	if pending_rewards_variant is Array:
		for reward_variant in pending_rewards_variant:
			var reward = PENDING_CHARACTER_REWARD_SCRIPT.from_legacy(reward_variant)
			if reward == null or reward.is_empty():
				continue
			party_state.pending_character_rewards.append(reward)

	if party_state.warehouse_state == null:
		party_state.warehouse_state = WAREHOUSE_STATE_SCRIPT.new()

	return party_state
