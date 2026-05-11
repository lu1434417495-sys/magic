## 文件说明：该脚本属于战斗结算结果相关的业务脚本，集中维护胜利方、奖励队列与战后变更等正式 contract 字段。
## 审查重点：重点核对字段命名、结果快照是否保持稳定，以及奖励队列与任务事件的契约是否一致。
## 备注：这是 battle 结算的 canonical helper，不承载保存系统本体，只作为运行时结算结果容器。

class_name BattleResolutionResult
extends RefCounted

const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/progression/pending_character_reward.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const BattleLootConstants = preload("res://scripts/systems/battle/core/battle_loot_constants.gd")
const PendingCharacterReward = PENDING_CHARACTER_REWARD_SCRIPT
const TOP_LEVEL_FIELDS := [
	"battle_id",
	"seed",
	"world_coord",
	"encounter_anchor_id",
	"terrain_profile_id",
	"winner_faction_id",
	"encounter_resolution",
	"loot_entries",
	"overflow_entries",
	"pending_character_rewards",
	"quest_progress_events",
	"world_mutations",
	"party_resource_commit",
]
const REQUIRED_STRING_FIELDS := [
	"battle_id",
	"encounter_anchor_id",
	"terrain_profile_id",
	"winner_faction_id",
	"encounter_resolution",
]
const REQUIRED_ARRAY_FIELDS := [
	"loot_entries",
	"overflow_entries",
	"pending_character_rewards",
	"quest_progress_events",
	"world_mutations",
]

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


static func from_dict(data: Variant):
	if data is not Dictionary:
		return null
	var payload := data as Dictionary
	if not _has_valid_top_level_payload(payload):
		return null

	var result = BATTLE_RESOLUTION_RESULT_SCRIPT.new()
	result.battle_id = ProgressionDataUtils.to_string_name(payload["battle_id"])
	result.seed = int(payload["seed"])
	result.world_coord = payload["world_coord"]
	result.encounter_anchor_id = ProgressionDataUtils.to_string_name(payload["encounter_anchor_id"])
	result.terrain_profile_id = ProgressionDataUtils.to_string_name(payload["terrain_profile_id"])
	result.winner_faction_id = ProgressionDataUtils.to_string_name(payload["winner_faction_id"])
	result.encounter_resolution = ProgressionDataUtils.to_string_name(payload["encounter_resolution"])
	var parsed_loot_entries = _drop_entry_dicts_from_payload(payload["loot_entries"])
	if parsed_loot_entries == null:
		return null
	var parsed_overflow_entries = _drop_entry_dicts_from_payload(payload["overflow_entries"])
	if parsed_overflow_entries == null:
		return null
	var parsed_pending_character_rewards = _reward_variants_from_dicts(payload["pending_character_rewards"])
	if parsed_pending_character_rewards == null:
		return null
	var parsed_quest_progress_events = _dictionary_array_from_payload(payload["quest_progress_events"])
	if parsed_quest_progress_events == null:
		return null
	var parsed_world_mutations = _dictionary_array_from_payload(payload["world_mutations"])
	if parsed_world_mutations == null:
		return null
	result.loot_entries = parsed_loot_entries
	result.overflow_entries = parsed_overflow_entries
	result.pending_character_rewards = parsed_pending_character_rewards
	result.quest_progress_events = parsed_quest_progress_events
	result.world_mutations = parsed_world_mutations
	result.party_resource_commit = (payload["party_resource_commit"] as Dictionary).duplicate(true)
	return result


static func _has_valid_top_level_payload(payload: Dictionary) -> bool:
	if not _has_exact_fields(payload, TOP_LEVEL_FIELDS):
		return false
	for field_name in REQUIRED_STRING_FIELDS:
		if not payload.has(field_name):
			return false
		if not _is_non_empty_string(payload[field_name]):
			return false
	if not payload.has("seed") or payload["seed"] is not int:
		return false
	if not payload.has("world_coord") or payload["world_coord"] is not Vector2i:
		return false
	for field_name in REQUIRED_ARRAY_FIELDS:
		if not payload.has(field_name):
			return false
		if payload[field_name] is not Array:
			return false
	if not payload.has("party_resource_commit") or payload["party_resource_commit"] is not Dictionary:
		return false
	return true


static func _has_exact_fields(payload: Dictionary, expected_fields: Array) -> bool:
	if payload.size() != expected_fields.size():
		return false
	var expected_lookup: Dictionary = {}
	var seen_lookup: Dictionary = {}
	for field_name in expected_fields:
		expected_lookup[field_name] = true
	for key_variant in payload.keys():
		if key_variant is not String:
			return false
		if not expected_lookup.has(key_variant):
			return false
		if seen_lookup.has(key_variant):
			return false
		seen_lookup[key_variant] = true
	return seen_lookup.size() == expected_lookup.size()


static func _is_non_empty_string(value: Variant) -> bool:
	return value is String and not String(value).strip_edges().is_empty()


static func _is_non_empty_string_name_value(value: Variant) -> bool:
	var value_type := typeof(value)
	if value_type != TYPE_STRING and value_type != TYPE_STRING_NAME:
		return false
	return not String(value).strip_edges().is_empty()


static func _drop_entry_dicts_from_payload(values: Variant):
	if values is not Array:
		return null
	var parsed_entries: Array[Dictionary] = []
	for entry_variant in values:
		if entry_variant is not Dictionary:
			return null
		var parsed_entry = _drop_entry_from_payload(entry_variant)
		if parsed_entry == null:
			return null
		parsed_entries.append(parsed_entry)
	return parsed_entries


static func _drop_entry_from_payload(entry_data: Dictionary):
	var required_fields := [
		"drop_type",
		"drop_source_kind",
		"drop_source_id",
		"drop_source_label",
		"drop_entry_id",
		"item_id",
		"quantity",
	]
	for field_name in required_fields:
		if not entry_data.has(field_name):
			return null
	for field_name in [
		"drop_type",
		"drop_source_kind",
		"drop_source_id",
		"drop_source_label",
		"drop_entry_id",
		"item_id",
	]:
		if not _is_non_empty_string_name_value(entry_data[field_name]):
			return null
	if entry_data["quantity"] is not int:
		return null
	var quantity := int(entry_data["quantity"])
	if quantity <= 0:
		return null

	var drop_type := ProgressionDataUtils.to_string_name(entry_data["drop_type"])
	var allowed_field_count := required_fields.size()
	if drop_type == BattleLootConstants.DROP_TYPE_EQUIPMENT_INSTANCE:
		allowed_field_count += 1
		if entry_data.size() != allowed_field_count:
			return null
		if not entry_data.has("equipment_instance"):
			return null
		if quantity != 1:
			return null
		var equipment_error := EQUIPMENT_INSTANCE_STATE_SCRIPT.get_payload_validation_error(entry_data["equipment_instance"])
		if not equipment_error.is_empty():
			return null
		var equipment_instance = EQUIPMENT_INSTANCE_STATE_SCRIPT.from_dict(entry_data["equipment_instance"])
		if equipment_instance == null:
			return null
		var entry_item_id := ProgressionDataUtils.to_string_name(entry_data["item_id"])
		if equipment_instance.item_id != entry_item_id:
			return null
		var normalized_equipment_entry := _duplicate_formal_drop_entry(entry_data)
		normalized_equipment_entry["equipment_instance"] = equipment_instance.to_dict()
		return normalized_equipment_entry

	if entry_data.size() != allowed_field_count:
		return null
	if entry_data.has("equipment_instance"):
		return null
	return _duplicate_formal_drop_entry(entry_data)


static func _duplicate_formal_drop_entry(entry_data: Dictionary) -> Dictionary:
	return {
		"drop_type": String(ProgressionDataUtils.to_string_name(entry_data["drop_type"])),
		"drop_source_kind": String(ProgressionDataUtils.to_string_name(entry_data["drop_source_kind"])),
		"drop_source_id": String(ProgressionDataUtils.to_string_name(entry_data["drop_source_id"])),
		"drop_source_label": String(entry_data["drop_source_label"]).strip_edges(),
		"drop_entry_id": String(ProgressionDataUtils.to_string_name(entry_data["drop_entry_id"])),
		"item_id": String(ProgressionDataUtils.to_string_name(entry_data["item_id"])),
		"quantity": int(entry_data["quantity"]),
	}


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
		var drop_entry_id := ProgressionDataUtils.to_string_name(loot_entry_data.get("drop_entry_id", ""))
		var item_id := ProgressionDataUtils.to_string_name(loot_entry_data.get("item_id", ""))
		var quantity := maxi(int(loot_entry_data.get("quantity", 0)), 0)
		var drop_source_label := String(loot_entry_data.get("drop_source_label", "")).strip_edges()
		if drop_type == &"" or drop_source_kind == &"" or drop_source_id == &"" or drop_source_label.is_empty() or drop_entry_id == &"" or item_id == &"" or quantity <= 0:
			continue
		var normalized_entry := {
			"drop_type": String(drop_type),
			"drop_source_kind": String(drop_source_kind),
			"drop_source_id": String(drop_source_id),
			"drop_source_label": drop_source_label,
			"drop_entry_id": String(drop_entry_id),
			"item_id": String(item_id),
			"quantity": quantity,
		}
		if drop_type == BattleLootConstants.DROP_TYPE_EQUIPMENT_INSTANCE:
			if not loot_entry_data.has("equipment_instance"):
				continue
			var equipment_instance_data := _normalize_equipment_instance_data(
				loot_entry_data.get("equipment_instance", {})
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


static func _reward_variants_from_dicts(values: Variant):
	var rewards: Array = []
	if values is not Array:
		return null
	for reward_variant in values:
		if reward_variant is not Dictionary:
			return null
		var reward_from_dict = PENDING_CHARACTER_REWARD_SCRIPT.from_variant(reward_variant)
		if reward_from_dict == null:
			return null
		rewards.append(reward_from_dict)
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


static func _dictionary_array_from_payload(values: Variant):
	if values is not Array:
		return null
	var result: Array[Dictionary] = []
	for value in values:
		if value is not Dictionary:
			return null
		result.append((value as Dictionary).duplicate(true))
	return result


static func _normalize_equipment_instance_data(value: Variant) -> Dictionary:
	if value == null:
		return {}
	if value is Dictionary:
		if not EQUIPMENT_INSTANCE_STATE_SCRIPT.get_payload_validation_error(value).is_empty():
			return {}
		var instance = EQUIPMENT_INSTANCE_STATE_SCRIPT.from_dict(value)
		if instance == null or instance.item_id == &"":
			return {}
		return instance.to_dict()
	if value.has_method("to_dict"):
		var instance_dict: Variant = value.to_dict()
		if instance_dict is Dictionary:
			return _normalize_equipment_instance_data(instance_dict)
	return {}
