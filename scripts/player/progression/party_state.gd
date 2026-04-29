## 文件说明：该脚本属于队伍状态相关的状态数据脚本，集中维护版本、队长成员唯一标识、激活成员标识列表等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name PartyState
extends RefCounted

const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const UNIT_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_progress.gd")
const WAREHOUSE_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_state.gd")
const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/progression/pending_character_reward.gd")
const QUEST_STATE_SCRIPT = preload("res://scripts/player/progression/quest_state.gd")
const PendingCharacterReward = PENDING_CHARACTER_REWARD_SCRIPT
const QuestState = QUEST_STATE_SCRIPT

## 字段说明：记录版本，会参与成长规则判定、序列化和界面展示。
var version := 3
## 字段说明：记录队伍持有金币，用于据点消费、商店结算和通用财富判定。
var gold: int = 0
## 字段说明：记录队长成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var leader_member_id: StringName = &""
## 字段说明：记录主角成员唯一标识，作为 GameOver 与主角专属规则的正式真相源；在合法编队中主角必须保持为上阵成员。
var main_character_member_id: StringName = &""
## 字段说明：记录本周目的命运事件触发锁和流程标记，供后续命运系统与存档读取。
var fate_run_flags: Dictionary = {}
## 字段说明：记录本周目的通用剧情 / 事件去重标记，供低 luck 事件池与后续剧情脚本复用。
var meta_flags: Dictionary = {}
## 字段说明：保存激活成员标识列表，便于批量遍历、交叉查找和界面展示。
var active_member_ids: Array = []
## 字段说明：保存预备成员标识列表，便于批量遍历、交叉查找和界面展示。
var reserve_member_ids: Array = []
## 字段说明：缓存成员状态集合字典，集中保存可按键查询的运行时数据。
var member_states: Dictionary = {}
## 字段说明：保存待处理角色奖励列表，便于顺序遍历、批量展示、批量运算和整体重建。
var pending_character_rewards: Array[PendingCharacterReward] = []
## 字段说明：保存当前正在进行中的任务状态列表，供后续任务系统、存档与快照读取。
var active_quests: Array[QuestState] = []
## 字段说明：保存已完成目标、待领取奖励的任务状态列表，供后续任务系统、存档与快照读取。
var claimable_quests: Array[QuestState] = []
## 字段说明：保存已完成任务标识列表，供后续任务系统、存档与快照读取。
var completed_quest_ids: Array[StringName] = []
## 字段说明：记录仓库状态，会参与成长规则判定、序列化和界面展示。
var warehouse_state = WAREHOUSE_STATE_SCRIPT.new()


func get_member_state(member_id: StringName):
	return member_states.get(member_id)


func has_member_state(member_id: StringName) -> bool:
	return get_member_state(member_id) != null


func is_member_dead(member_id: StringName) -> bool:
	var member_state = get_member_state(member_id)
	return member_state != null and bool(member_state.is_dead)


func get_resolved_main_character_member_id() -> StringName:
	if main_character_member_id != &"" and has_member_state(main_character_member_id):
		return main_character_member_id
	return &""


func get_fate_run_flags() -> Dictionary:
	return _normalize_fate_run_flags(fate_run_flags)


func set_fate_run_flags(values: Dictionary) -> void:
	fate_run_flags = _normalize_fate_run_flags(values)


func get_fate_run_flag(flag_id: StringName, default_value: bool = false) -> bool:
	if flag_id == &"":
		return default_value
	var normalized_flags := _normalize_fate_run_flags(fate_run_flags)
	return bool(normalized_flags.get(flag_id, default_value))


func has_fate_run_flag(flag_id: StringName) -> bool:
	return get_fate_run_flag(flag_id, false)


func set_fate_run_flag(flag_id: StringName, enabled: bool = true) -> void:
	if flag_id == &"":
		return
	fate_run_flags[flag_id] = bool(enabled)


func clear_fate_run_flag(flag_id: StringName) -> void:
	if flag_id == &"":
		return
	fate_run_flags.erase(flag_id)


func get_meta_flags() -> Dictionary:
	return _normalize_meta_flags(meta_flags)


func set_meta_flags(values: Dictionary) -> void:
	meta_flags = _normalize_meta_flags(values)


func get_meta_flag(flag_id: StringName, default_value: bool = false) -> bool:
	if flag_id == &"":
		return default_value
	var normalized_flags := _normalize_meta_flags(meta_flags)
	return bool(normalized_flags.get(flag_id, default_value))


func has_meta_flag(flag_id: StringName) -> bool:
	return get_meta_flag(flag_id, false)


func set_meta_flag(flag_id: StringName, enabled: bool = true) -> void:
	if flag_id == &"":
		return
	meta_flags[flag_id] = bool(enabled)


func clear_meta_flag(flag_id: StringName) -> void:
	if flag_id == &"":
		return
	meta_flags.erase(flag_id)


func remove_member_from_rosters(member_id: StringName) -> void:
	if member_id == &"":
		return
	active_member_ids = ProgressionDataUtils.to_string_name_array(active_member_ids)
	reserve_member_ids = ProgressionDataUtils.to_string_name_array(reserve_member_ids)
	active_member_ids.erase(member_id)
	reserve_member_ids.erase(member_id)
	if leader_member_id == member_id:
		leader_member_id = active_member_ids[0] if not active_member_ids.is_empty() else &""


func get_active_quests() -> Array[QuestState]:
	return active_quests.duplicate()


func get_claimable_quests() -> Array[QuestState]:
	return claimable_quests.duplicate()


func get_completed_quest_ids() -> Array[StringName]:
	return completed_quest_ids.duplicate()


func get_gold() -> int:
	return maxi(int(gold), 0)


func set_gold(value: int) -> void:
	gold = maxi(int(value), 0)


func add_gold(amount: int) -> int:
	set_gold(get_gold() + int(amount))
	return gold


func can_afford(amount: int) -> bool:
	return get_gold() >= maxi(int(amount), 0)


func spend_gold(amount: int) -> bool:
	var cost := maxi(int(amount), 0)
	if cost == 0:
		return true
	if not can_afford(cost):
		return false
	set_gold(get_gold() - cost)
	return true


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


func get_active_quest_state(quest_id: StringName) -> QuestState:
	for quest_state in active_quests:
		if quest_state != null and quest_state.quest_id == quest_id:
			return quest_state
	return null


func has_active_quest(quest_id: StringName) -> bool:
	return get_active_quest_state(quest_id) != null


func get_claimable_quest_state(quest_id: StringName) -> QuestState:
	for quest_state in claimable_quests:
		if quest_state != null and quest_state.quest_id == quest_id:
			return quest_state
	return null


func has_claimable_quest(quest_id: StringName) -> bool:
	return get_claimable_quest_state(quest_id) != null


func set_active_quest_state(quest_state) -> void:
	if quest_state == null or quest_state is not QuestState:
		return
	var typed_quest_state: QuestState = quest_state
	if typed_quest_state.quest_id == &"":
		return
	remove_claimable_quest(typed_quest_state.quest_id)
	completed_quest_ids.erase(typed_quest_state.quest_id)
	for index in range(active_quests.size()):
		var existing_quest_state := active_quests[index]
		if existing_quest_state == null or existing_quest_state.quest_id != typed_quest_state.quest_id:
			continue
		active_quests[index] = typed_quest_state
		return
	active_quests.append(typed_quest_state)


func set_claimable_quest_state(quest_state) -> void:
	if quest_state == null or quest_state is not QuestState:
		return
	var typed_quest_state: QuestState = quest_state
	if typed_quest_state.quest_id == &"":
		return
	remove_active_quest(typed_quest_state.quest_id)
	completed_quest_ids.erase(typed_quest_state.quest_id)
	for index in range(claimable_quests.size()):
		var existing_quest_state := claimable_quests[index]
		if existing_quest_state == null or existing_quest_state.quest_id != typed_quest_state.quest_id:
			continue
		claimable_quests[index] = typed_quest_state
		return
	claimable_quests.append(typed_quest_state)


func remove_active_quest(quest_id: StringName) -> bool:
	for index in range(active_quests.size()):
		var quest_state := active_quests[index]
		if quest_state == null or quest_state.quest_id != quest_id:
			continue
		active_quests.remove_at(index)
		return true
	return false


func remove_claimable_quest(quest_id: StringName) -> bool:
	for index in range(claimable_quests.size()):
		var quest_state := claimable_quests[index]
		if quest_state == null or quest_state.quest_id != quest_id:
			continue
		claimable_quests.remove_at(index)
		return true
	return false


func get_active_quest_ids() -> Array[StringName]:
	var quest_ids: Array[StringName] = []
	for quest_state in active_quests:
		if quest_state == null or quest_state.quest_id == &"":
			continue
		quest_ids.append(quest_state.quest_id)
	return quest_ids


func get_claimable_quest_ids() -> Array[StringName]:
	var quest_ids: Array[StringName] = []
	for quest_state in claimable_quests:
		if quest_state == null or quest_state.quest_id == &"":
			continue
		quest_ids.append(quest_state.quest_id)
	return quest_ids


func has_completed_quest(quest_id: StringName) -> bool:
	return completed_quest_ids.has(quest_id)


func add_completed_quest_id(quest_id: StringName) -> void:
	if quest_id == &"" or completed_quest_ids.has(quest_id):
		return
	remove_active_quest(quest_id)
	remove_claimable_quest(quest_id)
	completed_quest_ids.append(quest_id)


func mark_quest_claimable(quest_id: StringName, world_step: int = -1) -> bool:
	var quest_state := get_active_quest_state(quest_id)
	if quest_state == null:
		return false
	quest_state.mark_completed(world_step)
	remove_active_quest(quest_id)
	set_claimable_quest_state(quest_state)
	return true


func mark_quest_completed(quest_id: StringName, world_step: int = -1) -> bool:
	return mark_quest_claimable(quest_id, world_step)


func mark_quest_reward_claimed(quest_id: StringName, world_step: int = -1) -> bool:
	var quest_state := get_claimable_quest_state(quest_id)
	if quest_state == null:
		return false
	quest_state.mark_reward_claimed(world_step)
	remove_claimable_quest(quest_id)
	add_completed_quest_id(quest_id)
	return true


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

	var active_quest_data := _serialize_quest_state_array(active_quests)
	var claimable_quest_data := _serialize_quest_state_array(claimable_quests)

	return {
		"version": version,
		"gold": get_gold(),
		"leader_member_id": String(leader_member_id),
		"main_character_member_id": String(main_character_member_id),
		"fate_run_flags": _serialize_fate_run_flags(fate_run_flags),
		"meta_flags": _serialize_meta_flags(meta_flags),
		"active_member_ids": ProgressionDataUtils.string_name_array_to_string_array(
			ProgressionDataUtils.to_string_name_array(active_member_ids)
		),
		"reserve_member_ids": ProgressionDataUtils.string_name_array_to_string_array(
			ProgressionDataUtils.to_string_name_array(reserve_member_ids)
		),
		"member_states": member_states_data,
		"pending_character_rewards": pending_reward_data,
		"active_quests": active_quest_data,
		"claimable_quests": claimable_quest_data,
		"completed_quest_ids": ProgressionDataUtils.string_name_array_to_string_array(
			_normalize_unique_string_name_array(completed_quest_ids)
		),
		"warehouse_state": warehouse_state.to_dict() if warehouse_state != null else {},
	}


static func from_dict(data: Dictionary):
	if data.is_empty():
		return null
	if int(data.get("version", 0)) != 3:
		return null
	if not data.has("main_character_member_id"):
		return null
	var warehouse_state_data: Variant = data.get("warehouse_state", null)
	if warehouse_state_data is not Dictionary:
		return null
	var member_states_data: Variant = data.get("member_states", null)
	if member_states_data is not Dictionary:
		return null
	var pending_rewards_variant: Variant = data.get("pending_character_rewards", null)
	if pending_rewards_variant is not Array:
		return null
	var active_quests_variant: Variant = data.get("active_quests", null)
	if active_quests_variant is not Array:
		return null
	var claimable_quests_variant: Variant = data.get("claimable_quests", [])
	if claimable_quests_variant is not Array:
		return null
	var completed_quest_ids_variant: Variant = data.get("completed_quest_ids", null)
	if completed_quest_ids_variant is not Array:
		return null
	if not data.has("fate_run_flags"):
		return null
	var fate_run_flags_variant: Variant = data.get("fate_run_flags", null)
	if fate_run_flags_variant is not Dictionary:
		return null
	if not data.has("meta_flags"):
		return null
	var meta_flags_variant: Variant = data.get("meta_flags", null)
	if meta_flags_variant is not Dictionary:
		return null

	var party_state := PARTY_STATE_SCRIPT.new()
	party_state.version = int(data.get("version", 3))
	party_state.gold = maxi(int(data.get("gold", 0)), 0)
	party_state.leader_member_id = ProgressionDataUtils.to_string_name(data.get("leader_member_id", ""))
	party_state.main_character_member_id = ProgressionDataUtils.to_string_name(data.get("main_character_member_id", ""))
	party_state.set_fate_run_flags(fate_run_flags_variant)
	party_state.set_meta_flags(meta_flags_variant)
	party_state.active_member_ids = ProgressionDataUtils.to_string_name_array(data.get("active_member_ids", []))
	party_state.reserve_member_ids = ProgressionDataUtils.to_string_name_array(data.get("reserve_member_ids", []))
	party_state.warehouse_state = WAREHOUSE_STATE_SCRIPT.from_dict(warehouse_state_data)
	if party_state.warehouse_state == null:
		return null

	for key in member_states_data.keys():
		var member_state = PARTY_MEMBER_STATE_SCRIPT.from_dict(member_states_data[key])
		if member_state == null:
			return null
		if member_state.member_id == &"":
			member_state.member_id = ProgressionDataUtils.to_string_name(key)
		if member_state.progression == null:
			member_state.progression = UNIT_PROGRESS_SCRIPT.new()
		if member_state.progression.unit_id == &"":
			member_state.progression.unit_id = member_state.member_id
		if member_state.progression.display_name.is_empty():
			member_state.progression.display_name = member_state.display_name
		party_state.member_states[member_state.member_id] = member_state

	for reward_variant in pending_rewards_variant:
		var reward = PENDING_CHARACTER_REWARD_SCRIPT.from_variant(reward_variant)
		if reward == null or reward.is_empty():
			continue
		party_state.pending_character_rewards.append(reward)

	for quest_variant in active_quests_variant:
		if quest_variant is not Dictionary:
			return null
		var quest_state: QuestState = QUEST_STATE_SCRIPT.from_dict(quest_variant)
		if quest_state == null or quest_state.quest_id == &"" or party_state.has_active_quest(quest_state.quest_id):
			continue
		party_state.active_quests.append(quest_state)

	for quest_variant in claimable_quests_variant:
		if quest_variant is not Dictionary:
			return null
		var quest_state: QuestState = QUEST_STATE_SCRIPT.from_dict(quest_variant)
		if quest_state == null or quest_state.quest_id == &"" or party_state.has_claimable_quest(quest_state.quest_id):
			continue
		party_state.claimable_quests.append(quest_state)

	party_state.completed_quest_ids = _normalize_unique_string_name_array(
		ProgressionDataUtils.to_string_name_array(completed_quest_ids_variant)
	)
	var active_quest_ids := party_state.get_active_quest_ids()
	var claimable_quest_ids := party_state.get_claimable_quest_ids()
	for quest_id in active_quest_ids:
		if claimable_quest_ids.has(quest_id) or party_state.completed_quest_ids.has(quest_id):
			return null
	for quest_id in claimable_quest_ids:
		if party_state.completed_quest_ids.has(quest_id):
			return null
	if party_state.main_character_member_id == &"" or not party_state.has_member_state(party_state.main_character_member_id):
		return null

	return party_state


func _serialize_quest_state_array(quest_states: Array[QuestState]) -> Array[Dictionary]:
	var quest_data: Array[Dictionary] = []
	var quest_entries: Array[Dictionary] = []
	for quest_state in quest_states:
		if quest_state == null or quest_state.quest_id == &"":
			continue
		quest_entries.append({
			"quest_id": String(quest_state.quest_id),
			"data": quest_state.to_dict(),
		})
	quest_entries.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		return String(a.get("quest_id", "")) < String(b.get("quest_id", ""))
	)
	for entry in quest_entries:
		quest_data.append((entry.get("data", {}) as Dictionary).duplicate(true))
	return quest_data


static func _normalize_unique_string_name_array(values: Array) -> Array[StringName]:
	var normalized_values: Array[StringName] = []
	var seen_values: Dictionary = {}
	for raw_value in values:
		var normalized_value := ProgressionDataUtils.to_string_name(raw_value)
		if normalized_value == &"" or seen_values.has(normalized_value):
			continue
		seen_values[normalized_value] = true
		normalized_values.append(normalized_value)
	return normalized_values


static func _normalize_fate_run_flags(values: Variant) -> Dictionary:
	var normalized_flags: Dictionary = {}
	if values is not Dictionary:
		return normalized_flags
	for raw_key in values.keys():
		var flag_id := ProgressionDataUtils.to_string_name(raw_key)
		if flag_id == &"":
			continue
		normalized_flags[flag_id] = bool(values[raw_key])
	return normalized_flags


static func _serialize_fate_run_flags(values: Dictionary) -> Dictionary:
	var serialized_flags: Dictionary = {}
	for key in ProgressionDataUtils.sorted_string_keys(values):
		var flag_id := ProgressionDataUtils.to_string_name(key)
		if flag_id == &"":
			continue
		serialized_flags[String(flag_id)] = bool(values.get(flag_id, values.get(key, false)))
	return serialized_flags


static func _normalize_meta_flags(values: Variant) -> Dictionary:
	var normalized_flags: Dictionary = {}
	if values is not Dictionary:
		return normalized_flags
	for raw_key in values.keys():
		var flag_id := ProgressionDataUtils.to_string_name(raw_key)
		if flag_id == &"":
			continue
		normalized_flags[flag_id] = bool(values[raw_key])
	return normalized_flags


static func _serialize_meta_flags(values: Dictionary) -> Dictionary:
	var serialized_flags: Dictionary = {}
	for key in ProgressionDataUtils.sorted_string_keys(values):
		var flag_id := ProgressionDataUtils.to_string_name(key)
		if flag_id == &"":
			continue
		serialized_flags[String(flag_id)] = bool(values.get(flag_id, values.get(key, false)))
	return serialized_flags
