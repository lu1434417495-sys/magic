## 文件说明：该脚本属于战斗结算结果相关的业务脚本，集中维护胜利方、奖励队列与战后变更等正式 contract 字段。
## 审查重点：重点核对字段命名、结果快照是否保持稳定，以及奖励队列与任务事件的契约是否一致。
## 备注：这是 battle 结算的 canonical helper，不承载保存系统本体，只作为运行时结算结果容器。

class_name BattleResolutionResult
extends RefCounted

const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle_resolution_result.gd")
const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/pending_character_reward.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const PendingCharacterReward = PENDING_CHARACTER_REWARD_SCRIPT
const BATTLE_LOOT_DROP_TYPE_EQUIPMENT_INSTANCE: StringName = &"equipment_instance"

## 字段说明：记录战斗唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var battle_id: StringName = &""
## 字段说明：记录随机种子，会参与运行时状态流转、系统协作和存档恢复。
var seed := 0
## 字段说明：记录对象在世界地图中的坐标，供探索定位、遭遇生成和存档恢复复用。
var world_coord: Vector2i = Vector2i.ZERO
## 字段说明：记录遭遇锚点唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var encounter_anchor_id: StringName = &""
## 字段说明：记录地形配置档唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var terrain_profile_id: StringName = &"default"
## 字段说明：记录胜利方阵营唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var winner_faction_id: StringName = &""
## 字段说明：记录战斗结算结果类型，作为跨系统读取与日志分支的稳定标识。
var encounter_resolution: StringName = &""
## 字段说明：保存掉落条目列表，便于顺序遍历、批量展示、批量运算和整体重建。
var loot_entries: Array = []
## 字段说明：保存溢出条目列表，便于顺序遍历、批量展示、批量运算和整体重建。
var overflow_entries: Array = []
## 字段说明：保存正式待处理角色奖励列表，便于顺序遍历、批量展示、批量运算和整体重建。
var pending_character_rewards: Array = []
## 字段说明：保存任务进度事件列表，便于顺序遍历、批量展示、批量运算和整体重建。
var quest_progress_events: Array = []
## 字段说明：保存世界变更列表，便于顺序遍历、批量展示、批量运算和整体重建。
var world_mutations: Array = []
## 字段说明：缓存角色资源提交信息字典，集中保存可按键查询的运行时数据。
var party_resource_commit: Dictionary = {}


func is_empty() -> bool:
	return battle_id == &"" \
		and winner_faction_id == &"" \
		and encounter_resolution == &"" \
		and loot_entries.is_empty() \
		and overflow_entries.is_empty() \
		and pending_character_rewards.is_empty() \
		and quest_progress_events.is_empty() \
		and world_mutations.is_empty() \
		and party_resource_commit.is_empty()


func has_pending_character_rewards() -> bool:
	return not pending_character_rewards.is_empty()


func get_pending_character_rewards_copy() -> Array:
	return pending_character_rewards.duplicate()


func set_loot_entries(loot_entry_variants: Array) -> void:
	loot_entries = _normalize_drop_entry_variants(loot_entry_variants)


func set_overflow_entries(overflow_entry_variants: Array) -> void:
	overflow_entries = _normalize_drop_entry_variants(overflow_entry_variants)


func set_pending_character_rewards(reward_variants: Array) -> void:
	pending_character_rewards = _normalize_reward_variants(reward_variants)


func to_dict() -> Dictionary:
	return {
		"battle_id": String(battle_id),
		"seed": seed,
		"world_coord": world_coord,
		"encounter_anchor_id": String(encounter_anchor_id),
		"terrain_profile_id": String(terrain_profile_id),
		"winner_faction_id": String(winner_faction_id),
		"encounter_resolution": String(encounter_resolution),
		"loot_entries": _normalize_drop_entry_variants(loot_entries),
		"overflow_entries": _normalize_drop_entry_variants(overflow_entries),
		"pending_character_rewards": _reward_variants_to_dicts(pending_character_rewards),
		"quest_progress_events": _duplicate_variant_array(quest_progress_events),
		"world_mutations": _duplicate_variant_array(world_mutations),
		"party_resource_commit": party_resource_commit.duplicate(true),
	}


static func from_dict(data: Dictionary):
	var result = BATTLE_RESOLUTION_RESULT_SCRIPT.new()
	result.battle_id = ProgressionDataUtils.to_string_name(data.get("battle_id", ""))
	result.seed = int(data.get("seed", 0))
	result.world_coord = data.get("world_coord", Vector2i.ZERO)
	result.encounter_anchor_id = ProgressionDataUtils.to_string_name(data.get("encounter_anchor_id", ""))
	result.terrain_profile_id = ProgressionDataUtils.to_string_name(data.get("terrain_profile_id", "default"))
	result.winner_faction_id = ProgressionDataUtils.to_string_name(data.get("winner_faction_id", ""))
	result.encounter_resolution = ProgressionDataUtils.to_string_name(data.get("encounter_resolution", ""))
	result.set_loot_entries(data.get("loot_entries", []))
	result.set_overflow_entries(data.get("overflow_entries", []))
	result.pending_character_rewards = _reward_variants_from_dicts(data.get("pending_character_rewards", []))
	result.quest_progress_events = _duplicate_variant_array(data.get("quest_progress_events", []))
	result.world_mutations = _duplicate_variant_array(data.get("world_mutations", []))
	result.party_resource_commit = data.get("party_resource_commit", {}).duplicate(true) if data.get("party_resource_commit", {}) is Dictionary else {}
	return result

static func _normalize_drop_entry_variants(loot_entry_variants: Variant) -> Array[Dictionary]:
	var normalized_entries: Array[Dictionary] = []
	if loot_entry_variants is not Array:
		return normalized_entries
	for loot_entry_variant in loot_entry_variants:
		if loot_entry_variant is not Dictionary:
			continue
		var loot_entry_data := loot_entry_variant as Dictionary
		var drop_type := ProgressionDataUtils.to_string_name(loot_entry_data.get("drop_type", ""))
		var drop_source_kind := ProgressionDataUtils.to_string_name(loot_entry_data.get("drop_source_kind", ""))
		var drop_source_id := ProgressionDataUtils.to_string_name(loot_entry_data.get("drop_source_id", ""))
		var drop_entry_id := ProgressionDataUtils.to_string_name(
			loot_entry_data.get("drop_entry_id", loot_entry_data.get("drop_id", ""))
		)
		var item_id := ProgressionDataUtils.to_string_name(loot_entry_data.get("item_id", ""))
		var quantity := maxi(int(loot_entry_data.get("quantity", 0)), 0)
		if drop_type == &"" or drop_source_kind == &"" or drop_source_id == &"" or drop_entry_id == &"" or item_id == &"" or quantity <= 0:
			continue
		var drop_source_label := String(loot_entry_data.get("drop_source_label", "")).strip_edges()
		if drop_source_label.is_empty():
			drop_source_label = String(drop_source_id)
		var normalized_entry := {
			"drop_type": String(drop_type),
			"drop_source_kind": String(drop_source_kind),
			"drop_source_id": String(drop_source_id),
			"drop_source_label": drop_source_label,
			"drop_entry_id": String(drop_entry_id),
			"item_id": String(item_id),
			"quantity": quantity,
		}
		if drop_type == BATTLE_LOOT_DROP_TYPE_EQUIPMENT_INSTANCE:
			var equipment_instance_data := _normalize_equipment_instance_data(
				loot_entry_data.get("equipment_instance", loot_entry_data.get("equipment_instance_data", {}))
			)
			if equipment_instance_data.is_empty():
				continue
			normalized_entry["equipment_instance"] = equipment_instance_data
			normalized_entry["quantity"] = 1
			normalized_entry["item_id"] = String(ProgressionDataUtils.to_string_name(equipment_instance_data.get("item_id", item_id)))
		normalized_entries.append(normalized_entry)
	return normalized_entries


static func _normalize_reward_variants(reward_variants: Array) -> Array:
	var normalized_rewards: Array = []
	if reward_variants == null:
		return normalized_rewards
	for reward_variant in reward_variants:
		if reward_variant == null:
			continue
		if reward_variant is PendingCharacterReward:
			var typed_reward := reward_variant as PendingCharacterReward
			if typed_reward.is_empty():
				continue
			normalized_rewards.append(typed_reward)
			continue
		if reward_variant is Dictionary:
			var normalized_reward = PENDING_CHARACTER_REWARD_SCRIPT.from_variant(reward_variant)
			if normalized_reward != null and not normalized_reward.is_empty():
				normalized_rewards.append(normalized_reward)
			continue
	return normalized_rewards


static func _reward_variants_to_dicts(reward_variants: Array) -> Array[Dictionary]:
	var rewards: Array[Dictionary] = []
	for reward_variant in reward_variants:
		if reward_variant == null:
			continue
		if reward_variant.has_method("to_dict"):
			rewards.append(reward_variant.to_dict())
			continue
		if reward_variant is Dictionary:
			rewards.append((reward_variant as Dictionary).duplicate(true))
	return rewards


static func _reward_variants_from_dicts(values: Variant) -> Array:
	var rewards: Array = []
	if values is not Array:
		return rewards
	for reward_variant in values:
		if reward_variant is Dictionary:
			var reward_from_dict = PENDING_CHARACTER_REWARD_SCRIPT.from_variant(reward_variant)
			if reward_from_dict != null:
				rewards.append(reward_from_dict)
			continue
		var normalized_reward = PENDING_CHARACTER_REWARD_SCRIPT.from_variant(reward_variant)
		if normalized_reward != null:
			rewards.append(normalized_reward)
	return rewards


static func _duplicate_variant_array(values: Variant) -> Array:
	var result: Array = []
	if values is not Array:
		return result
	for value in values:
		if value is Dictionary:
			result.append((value as Dictionary).duplicate(true))
		elif value is Array:
			result.append((value as Array).duplicate(true))
		else:
			result.append(value)
	return result


static func _normalize_equipment_instance_data(value: Variant) -> Dictionary:
	if value == null:
		return {}
	if value is Dictionary:
		var instance = EQUIPMENT_INSTANCE_STATE_SCRIPT.from_dict(value)
		if instance == null or instance.item_id == &"":
			return {}
		return instance.to_dict()
	if value.has_method("to_dict"):
		var instance_dict: Variant = value.to_dict()
		if instance_dict is Dictionary:
			return _normalize_equipment_instance_data(instance_dict)
	return {}
