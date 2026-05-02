class_name GameRuntimeBattleLootCommitService
extends RefCounted

const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const BATTLE_LOOT_DROP_TYPE_ITEM: StringName = &"item"
const BATTLE_LOOT_DROP_TYPE_RANDOM_EQUIPMENT: StringName = &"random_equipment"
const BATTLE_LOOT_DROP_TYPE_EQUIPMENT_INSTANCE: StringName = &"equipment_instance"
const BATTLE_LOOT_SOURCE_KIND_CALAMITY_CONVERSION: StringName = &"calamity_conversion"
const BATTLE_LOOT_SOURCE_ID_ORDINARY_BATTLE: StringName = &"ordinary_battle"
const BATTLE_LOOT_SOURCE_ID_ELITE_BOSS_BATTLE: StringName = &"elite_boss_battle"
const BATTLE_LOOT_CALAMITY_SHARD_ITEM_ID: StringName = &"calamity_shard"
const ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP := 4
const CALAMITY_SHARD_CHAPTER_FLAG_PREFIX := "calamity_shard_chapter_slot_"

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null


func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null


func commit_battle_loot_to_shared_warehouse(battle_resolution_result) -> Dictionary:
	return _commit_battle_loot_to_shared_warehouse(battle_resolution_result)


func clear_regular_battle_calamity_shard_flags() -> void:
	_clear_regular_battle_calamity_shard_flags()


func build_battle_resolution_status_message(
	battle_name: String,
	winner_faction_id: String,
	loot_commit_result: Dictionary,
	persisted_ok: bool
) -> String:
	return _build_battle_resolution_status_message(battle_name, winner_faction_id, loot_commit_result, persisted_ok)


func build_last_battle_loot_snapshot(
	battle_name: String,
	winner_faction_id: String,
	battle_resolution_result,
	loot_commit_result: Dictionary
) -> Dictionary:
	return _build_last_battle_loot_snapshot(battle_name, winner_faction_id, battle_resolution_result, loot_commit_result)


func format_battle_drop_entries(drop_entry_variants: Array) -> String:
	return _format_battle_drop_entries(drop_entry_variants)

func _commit_battle_loot_to_shared_warehouse(battle_resolution_result) -> Dictionary:
	if battle_resolution_result == null:
		return {
			"ok": false,
			"error_code": "missing_battle_resolution_result",
			"blocked_item_id": "",
			"committed_item_count": 0,
			"overflow_entries": [],
			"overflow_entry_count": 0,
		}
	battle_resolution_result.set_overflow_entries([])
	if String(battle_resolution_result.winner_faction_id) != "player":
		return {
			"ok": true,
			"error_code": "",
			"blocked_item_id": "",
			"committed_item_count": 0,
			"overflow_entries": [],
			"overflow_entry_count": 0,
		}
	if _runtime._party_state == null or _runtime._party_warehouse_service == null or _runtime._game_session == null:
		return {
			"ok": false,
			"error_code": "warehouse_service_unavailable",
			"blocked_item_id": "",
			"committed_item_count": 0,
			"overflow_entries": [],
			"overflow_entry_count": 0,
		}

	_runtime._setup_party_warehouse_service(_runtime._party_warehouse_service, _runtime._party_state, _runtime._game_session.get_item_defs())
	var warehouse_state_before = _runtime._party_state.warehouse_state.duplicate_state() if _runtime._party_state.warehouse_state != null else null
	var fate_run_flags_before: Dictionary = _runtime._party_state.get_fate_run_flags() if _runtime._party_state != null and _runtime._party_state.has_method("get_fate_run_flags") else {}
	var overflow_entries: Array[Dictionary] = []
	var committed_item_count := 0
	battle_resolution_result.set_loot_entries(_resolve_effective_battle_loot_entries_for_commit(battle_resolution_result))
	for loot_entry_variant in battle_resolution_result.loot_entries:
		if loot_entry_variant is not Dictionary:
			continue
		var loot_entry_data := loot_entry_variant as Dictionary
		var drop_type := ProgressionDataUtils.to_string_name(loot_entry_data.get("drop_type", BATTLE_LOOT_DROP_TYPE_ITEM))
		if drop_type == BATTLE_LOOT_DROP_TYPE_EQUIPMENT_INSTANCE:
			var instance_commit_result := _commit_equipment_instance_loot_entry(loot_entry_data)
			if not bool(instance_commit_result.get("ok", false)):
				_runtime._party_state.warehouse_state = warehouse_state_before
				_runtime._party_state.set_fate_run_flags(fate_run_flags_before)
				_runtime._setup_party_warehouse_service(_runtime._party_warehouse_service, _runtime._party_state, _runtime._game_session.get_item_defs())
				return {
					"ok": false,
					"error_code": String(instance_commit_result.get("error_code", "battle_loot_equipment_instance_failed")),
					"blocked_item_id": String(instance_commit_result.get("blocked_item_id", "")),
					"committed_item_count": 0,
					"overflow_entries": [],
					"overflow_entry_count": 0,
				}
			committed_item_count += int(instance_commit_result.get("committed_item_count", 0))
			for overflow_entry_variant in instance_commit_result.get("overflow_entries", []):
				if overflow_entry_variant is Dictionary:
					overflow_entries.append((overflow_entry_variant as Dictionary).duplicate(true))
			continue
		if drop_type == BATTLE_LOOT_DROP_TYPE_RANDOM_EQUIPMENT:
			var equipment_commit_result := _commit_random_equipment_loot_entry(loot_entry_data)
			if not bool(equipment_commit_result.get("ok", false)):
				_runtime._party_state.warehouse_state = warehouse_state_before
				_runtime._party_state.set_fate_run_flags(fate_run_flags_before)
				_runtime._setup_party_warehouse_service(_runtime._party_warehouse_service, _runtime._party_state, _runtime._game_session.get_item_defs())
				return {
					"ok": false,
					"error_code": String(equipment_commit_result.get("error_code", "battle_loot_random_equipment_failed")),
					"blocked_item_id": String(equipment_commit_result.get("blocked_item_id", "")),
					"committed_item_count": 0,
					"overflow_entries": [],
					"overflow_entry_count": 0,
				}
			committed_item_count += int(equipment_commit_result.get("committed_item_count", 0))
			for overflow_entry_variant in equipment_commit_result.get("overflow_entries", []):
				if overflow_entry_variant is Dictionary:
					overflow_entries.append((overflow_entry_variant as Dictionary).duplicate(true))
			continue

		var item_commit_result := _commit_fixed_item_loot_entry(loot_entry_data)
		if not bool(item_commit_result.get("ok", false)):
			_runtime._party_state.warehouse_state = warehouse_state_before
			_runtime._party_state.set_fate_run_flags(fate_run_flags_before)
			_runtime._setup_party_warehouse_service(_runtime._party_warehouse_service, _runtime._party_state, _runtime._game_session.get_item_defs())
			return {
				"ok": false,
				"error_code": String(item_commit_result.get("error_code", "battle_loot_item_missing_def")),
				"blocked_item_id": String(item_commit_result.get("blocked_item_id", "")),
				"committed_item_count": 0,
				"overflow_entries": [],
				"overflow_entry_count": 0,
			}
		committed_item_count += int(item_commit_result.get("committed_item_count", 0))
		if _is_ordinary_battle_calamity_conversion_entry(loot_entry_data):
			_mark_regular_battle_calamity_shards_committed(int(item_commit_result.get("committed_item_count", 0)))
		for overflow_entry_variant in item_commit_result.get("overflow_entries", []):
			if overflow_entry_variant is Dictionary:
				overflow_entries.append((overflow_entry_variant as Dictionary).duplicate(true))
	battle_resolution_result.set_overflow_entries(overflow_entries)
	var overflow_item_id := ""
	if not battle_resolution_result.overflow_entries.is_empty() and battle_resolution_result.overflow_entries[0] is Dictionary:
		overflow_item_id = String((battle_resolution_result.overflow_entries[0] as Dictionary).get("item_id", ""))
	return {
		"ok": true,
		"error_code": "",
		"blocked_item_id": overflow_item_id,
		"committed_item_count": committed_item_count,
		"overflow_entries": battle_resolution_result.overflow_entries.duplicate(true),
		"overflow_entry_count": battle_resolution_result.overflow_entries.size(),
	}



func _commit_fixed_item_loot_entry(loot_entry_data: Dictionary) -> Dictionary:
	var item_id := ProgressionDataUtils.to_string_name(loot_entry_data.get("item_id", ""))
	var quantity := maxi(int(loot_entry_data.get("quantity", 0)), 0)
	if item_id == &"" or quantity <= 0:
		return {
			"ok": true,
			"error_code": "",
			"blocked_item_id": "",
			"committed_item_count": 0,
			"overflow_entries": [],
		}
	var add_result: Dictionary = _runtime._party_warehouse_service.add_item(item_id, quantity)
	if not bool(add_result.get("item_found", false)):
		return {
			"ok": false,
			"error_code": "battle_loot_item_missing_def",
			"blocked_item_id": String(item_id),
			"committed_item_count": 0,
			"overflow_entries": [],
		}
	var overflow_entries: Array[Dictionary] = []
	var remaining_quantity := int(add_result.get("remaining_quantity", 0))
	if remaining_quantity > 0:
		overflow_entries.append(_build_battle_overflow_entry(loot_entry_data, remaining_quantity))
	return {
		"ok": true,
		"error_code": "",
		"blocked_item_id": "",
		"committed_item_count": int(add_result.get("added_quantity", 0)),
		"overflow_entries": overflow_entries,
	}


func _commit_random_equipment_loot_entry(loot_entry_data: Dictionary) -> Dictionary:
	var item_id := ProgressionDataUtils.to_string_name(loot_entry_data.get("item_id", ""))
	var quantity := maxi(int(loot_entry_data.get("quantity", 0)), 0)
	var drop_luck := clampi(
		int(loot_entry_data.get("drop_luck", 0)),
		UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MIN,
		UNIT_BASE_ATTRIBUTES_SCRIPT.DROP_LUCK_MAX
	)
	if item_id == &"" or quantity <= 0:
		return {
			"ok": true,
			"error_code": "",
			"blocked_item_id": "",
			"committed_item_count": 0,
			"overflow_entries": [],
		}
	var item_def = _runtime._game_session.get_item_defs().get(item_id)
	if item_def == null:
		return {
			"ok": false,
			"error_code": "battle_loot_item_missing_def",
			"blocked_item_id": String(item_id),
			"committed_item_count": 0,
			"overflow_entries": [],
		}
	if not item_def.is_equipment():
		return {
			"ok": false,
			"error_code": "battle_loot_random_equipment_invalid_item",
			"blocked_item_id": String(item_id),
			"committed_item_count": 0,
			"overflow_entries": [],
		}
	var rolled_instances: Array = _runtime._equipment_drop_service.roll_item_instances(item_id, quantity, drop_luck)
	var committed_item_count := 0
	var overflow_quantity := 0
	for rolled_instance_variant in rolled_instances:
		if rolled_instance_variant == null:
			continue
		var rolled_item_id := ProgressionDataUtils.to_string_name(rolled_instance_variant.item_id)
		var add_result: Dictionary = _runtime._party_warehouse_service.add_equipment_instance(rolled_instance_variant)
		if not bool(add_result.get("item_found", false)):
			return {
				"ok": false,
				"error_code": "battle_loot_item_missing_def",
				"blocked_item_id": String(rolled_item_id),
				"committed_item_count": 0,
				"overflow_entries": [],
			}
		if not bool(add_result.get("is_equipment", false)):
			return {
				"ok": false,
				"error_code": "battle_loot_random_equipment_invalid_item",
				"blocked_item_id": String(rolled_item_id),
				"committed_item_count": 0,
				"overflow_entries": [],
			}
		if int(add_result.get("remaining_quantity", 0)) > 0:
			overflow_quantity += 1
			continue
		committed_item_count += 1
	var overflow_entries: Array[Dictionary] = []
	if overflow_quantity > 0:
		overflow_entries.append(_build_battle_overflow_entry(loot_entry_data, overflow_quantity))
	return {
		"ok": true,
		"error_code": "",
		"blocked_item_id": "",
		"committed_item_count": committed_item_count,
		"overflow_entries": overflow_entries,
	}


func _commit_equipment_instance_loot_entry(loot_entry_data: Dictionary) -> Dictionary:
	if not loot_entry_data.has("equipment_instance") or loot_entry_data.get("equipment_instance") is not Dictionary:
		return {
			"ok": false,
			"error_code": "battle_loot_equipment_instance_missing_payload",
			"blocked_item_id": String(loot_entry_data.get("item_id", "")),
			"committed_item_count": 0,
			"overflow_entries": [],
		}
	var equipment_instance_variant: Variant = loot_entry_data.get("equipment_instance")
	var equipment_instance = EQUIPMENT_INSTANCE_STATE_SCRIPT.from_transient_loot_dict(equipment_instance_variant)
	if equipment_instance == null:
		return {
			"ok": false,
			"error_code": "battle_loot_equipment_instance_invalid_payload",
			"blocked_item_id": String(loot_entry_data.get("item_id", "")),
			"committed_item_count": 0,
			"overflow_entries": [],
		}
	var item_id := ProgressionDataUtils.to_string_name(equipment_instance.item_id)
	if item_id == &"":
		item_id = ProgressionDataUtils.to_string_name(loot_entry_data.get("item_id", ""))
		equipment_instance.item_id = item_id
	if item_id == &"":
		return {
			"ok": false,
			"error_code": "battle_loot_equipment_instance_invalid_payload",
			"blocked_item_id": "",
			"committed_item_count": 0,
			"overflow_entries": [],
		}
	var item_def = _runtime._game_session.get_item_defs().get(item_id)
	if item_def == null:
		return {
			"ok": false,
			"error_code": "battle_loot_item_missing_def",
			"blocked_item_id": String(item_id),
			"committed_item_count": 0,
			"overflow_entries": [],
		}
	if not item_def.is_equipment():
		return {
			"ok": false,
			"error_code": "battle_loot_random_equipment_invalid_item",
			"blocked_item_id": String(item_id),
			"committed_item_count": 0,
			"overflow_entries": [],
		}
	var add_result: Dictionary = _runtime._party_warehouse_service.add_equipment_instance(equipment_instance, true)
	if int(add_result.get("remaining_quantity", 0)) > 0:
		return {
			"ok": true,
			"error_code": "",
			"blocked_item_id": "",
			"committed_item_count": 0,
			"overflow_entries": [_build_battle_overflow_entry(loot_entry_data, 1)],
		}
	return {
		"ok": true,
		"error_code": "",
		"blocked_item_id": "",
		"committed_item_count": 1,
		"overflow_entries": [],
	}


func _build_battle_overflow_entry(loot_entry_data: Dictionary, overflow_quantity: int) -> Dictionary:
	var overflow_entry := loot_entry_data.duplicate(true)
	overflow_entry["quantity"] = maxi(overflow_quantity, 0)
	return overflow_entry


func _resolve_effective_battle_loot_entries_for_commit(battle_resolution_result) -> Array[Dictionary]:
	var adjusted_entries: Array[Dictionary] = []
	if battle_resolution_result == null:
		return adjusted_entries
	var remaining_regular_cap := _get_remaining_regular_battle_calamity_shard_cap()
	var merge_index_by_key: Dictionary = {}
	for loot_entry_variant in battle_resolution_result.loot_entries:
		if loot_entry_variant is not Dictionary:
			continue
		var loot_entry := (loot_entry_variant as Dictionary).duplicate(true)
		if _is_ordinary_battle_calamity_conversion_entry(loot_entry):
			var allowed_quantity := mini(maxi(int(loot_entry.get("quantity", 0)), 0), remaining_regular_cap)
			remaining_regular_cap = maxi(remaining_regular_cap - allowed_quantity, 0)
			if allowed_quantity <= 0:
				continue
			loot_entry["quantity"] = allowed_quantity
		var merge_key := _build_battle_loot_merge_key(loot_entry)
		if not merge_key.is_empty() and merge_index_by_key.has(merge_key):
			var entry_index := int(merge_index_by_key.get(merge_key, -1))
			if entry_index >= 0 and entry_index < adjusted_entries.size():
				var merged_entry := adjusted_entries[entry_index].duplicate(true)
				merged_entry["quantity"] = int(merged_entry.get("quantity", 0)) + int(loot_entry.get("quantity", 0))
				adjusted_entries[entry_index] = merged_entry
				continue
		if not merge_key.is_empty():
			merge_index_by_key[merge_key] = adjusted_entries.size()
		adjusted_entries.append(loot_entry)
	return adjusted_entries


func _build_battle_loot_merge_key(loot_entry_data: Dictionary) -> String:
	if loot_entry_data == null or loot_entry_data.is_empty():
		return ""
	var drop_type := ProgressionDataUtils.to_string_name(loot_entry_data.get("drop_type", ""))
	var item_id := ProgressionDataUtils.to_string_name(loot_entry_data.get("item_id", ""))
	if item_id == &"":
		return ""
	if drop_type == BATTLE_LOOT_DROP_TYPE_ITEM:
		return "%s|%s" % [String(drop_type), String(item_id)]
	if drop_type == BATTLE_LOOT_DROP_TYPE_RANDOM_EQUIPMENT:
		return "%s|%s|%d" % [
			String(drop_type),
			String(item_id),
			clampi(
				int(loot_entry_data.get("drop_luck", 0)),
				UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MIN,
				UNIT_BASE_ATTRIBUTES_SCRIPT.DROP_LUCK_MAX
			),
		]
	return ""


func _is_ordinary_battle_calamity_conversion_entry(loot_entry_data: Dictionary) -> bool:
	if loot_entry_data == null or loot_entry_data.is_empty():
		return false
	var item_id := ProgressionDataUtils.to_string_name(loot_entry_data.get("item_id", ""))
	var drop_source_kind := ProgressionDataUtils.to_string_name(loot_entry_data.get("drop_source_kind", ""))
	var drop_source_id := ProgressionDataUtils.to_string_name(loot_entry_data.get("drop_source_id", ""))
	return item_id == BATTLE_LOOT_CALAMITY_SHARD_ITEM_ID \
		and drop_source_kind == BATTLE_LOOT_SOURCE_KIND_CALAMITY_CONVERSION \
		and drop_source_id == BATTLE_LOOT_SOURCE_ID_ORDINARY_BATTLE


func _get_remaining_regular_battle_calamity_shard_cap() -> int:
	return maxi(
		ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP - _get_regular_battle_calamity_shard_count_this_chapter(),
		0
	)


func _get_regular_battle_calamity_shard_count_this_chapter() -> int:
	if _runtime._party_state == null:
		return 0
	var shard_count := 0
	for slot_index in range(ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP):
		var flag_id := _build_regular_battle_calamity_shard_flag_id(slot_index)
		if _runtime._party_state.has_method("get_fate_run_flag") and _runtime._party_state.get_fate_run_flag(flag_id, false):
			shard_count += 1
	return shard_count


func _mark_regular_battle_calamity_shards_committed(quantity: int) -> void:
	if _runtime._party_state == null or quantity <= 0:
		return
	var remaining_to_mark := mini(quantity, _get_remaining_regular_battle_calamity_shard_cap())
	if remaining_to_mark <= 0:
		return
	for slot_index in range(ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP):
		var flag_id := _build_regular_battle_calamity_shard_flag_id(slot_index)
		if _runtime._party_state.has_method("get_fate_run_flag") and _runtime._party_state.get_fate_run_flag(flag_id, false):
			continue
		if _runtime._party_state.has_method("set_fate_run_flag"):
			_runtime._party_state.set_fate_run_flag(flag_id, true)
		remaining_to_mark -= 1
		if remaining_to_mark <= 0:
			return


func _clear_regular_battle_calamity_shard_flags() -> void:
	if _runtime._party_state == null or not _runtime._party_state.has_method("clear_fate_run_flag"):
		return
	for slot_index in range(ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP):
		_runtime._party_state.clear_fate_run_flag(_build_regular_battle_calamity_shard_flag_id(slot_index))


func _build_regular_battle_calamity_shard_flag_id(slot_index: int) -> StringName:
	return ProgressionDataUtils.to_string_name("%s%d" % [CALAMITY_SHARD_CHAPTER_FLAG_PREFIX, maxi(slot_index, 0)])


func _build_battle_resolution_status_message(
	battle_name: String,
	winner_faction_id: String,
	loot_commit_result: Dictionary,
	persisted_ok: bool
) -> String:
	var message := ""
	if persisted_ok:
		message = "%s 战斗结束，胜利方：%s。已返回世界地图并统一保存。" % [
			battle_name,
			_runtime._format_faction_label(winner_faction_id),
		]
	else:
		message = "%s 战斗结束，但战后持久化失败。" % battle_name
	var loot_status_suffix := _build_battle_loot_status_suffix(loot_commit_result)
	if loot_status_suffix.is_empty():
		return message
	return "%s %s" % [message, loot_status_suffix]


func _build_battle_loot_status_suffix(loot_commit_result: Dictionary) -> String:
	if loot_commit_result.is_empty():
		return ""
	if not bool(loot_commit_result.get("ok", false)):
		var blocked_item_id := ProgressionDataUtils.to_string_name(loot_commit_result.get("blocked_item_id", ""))
		if blocked_item_id != &"":
			return "战斗掉落写入共享仓库失败：%s。" % _runtime._get_item_display_name(blocked_item_id)
		return "战斗掉落写入共享仓库失败。"
	var overflow_text := _format_battle_drop_entries(loot_commit_result.get("overflow_entries", []))
	if overflow_text.is_empty():
		return ""
	return "未装下的掉落：%s。" % overflow_text


func _build_last_battle_loot_snapshot(
	battle_name: String,
	winner_faction_id: String,
	battle_resolution_result,
	loot_commit_result: Dictionary
) -> Dictionary:
	if battle_resolution_result == null:
		return {}
	var loot_entries: Array = battle_resolution_result.loot_entries.duplicate(true)
	var overflow_entries: Array = battle_resolution_result.overflow_entries.duplicate(true)
	if loot_entries.is_empty() and overflow_entries.is_empty():
		return {}
	return {
		"battle_name": battle_name,
		"winner_faction_id": winner_faction_id,
		"loot_entries": loot_entries,
		"loot_entry_count": loot_entries.size(),
		"loot_summary_text": _format_battle_drop_entries(loot_entries),
		"overflow_entries": overflow_entries,
		"overflow_entry_count": overflow_entries.size(),
		"overflow_summary_text": _format_battle_drop_entries(overflow_entries),
		"commit_ok": bool(loot_commit_result.get("ok", false)),
		"commit_error_code": String(loot_commit_result.get("error_code", "")),
	}


func _format_battle_drop_entries(drop_entry_variants: Array) -> String:
	var quantities_by_item: Dictionary = {}
	var ordered_item_ids: Array[StringName] = []
	for drop_entry_variant in drop_entry_variants:
		if drop_entry_variant is not Dictionary:
			continue
		var drop_entry_data := drop_entry_variant as Dictionary
		var item_id := ProgressionDataUtils.to_string_name(drop_entry_data.get("item_id", ""))
		var quantity := maxi(int(drop_entry_data.get("quantity", 0)), 0)
		if item_id == &"" or quantity <= 0:
			continue
		if not quantities_by_item.has(item_id):
			ordered_item_ids.append(item_id)
			quantities_by_item[item_id] = 0
		quantities_by_item[item_id] = int(quantities_by_item.get(item_id, 0)) + quantity
	var parts: Array[String] = []
	for item_id in ordered_item_ids:
		parts.append("%s x%d" % [_runtime._get_item_display_name(item_id), int(quantities_by_item.get(item_id, 0))])
	return "、".join(PackedStringArray(parts))


