class_name BattleRuntimeLootResolver
extends RefCounted

const BattleResolutionResult = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")

const STATUS_BLACK_STAR_BRAND_ELITE: StringName = &"black_star_brand_elite"
const STATUS_CROWN_BREAK_BROKEN_FANG: StringName = &"crown_break_broken_fang"
const STATUS_CROWN_BREAK_BROKEN_HAND: StringName = &"crown_break_broken_hand"
const STATUS_CROWN_BREAK_BLINDED_EYE: StringName = &"crown_break_blinded_eye"
const STATUS_DOOM_SENTENCE_VERDICT: StringName = &"doom_sentence_verdict"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"
const BOSS_TARGET_STAT_ID: StringName = &"boss_target"
const CALAMITY_SHARD_ITEM_ID: StringName = &"calamity_shard"
const BLACK_CROWN_CORE_ITEM_ID: StringName = &"black_crown_core"
const LOOT_DROP_TYPE_ITEM: StringName = &"item"
const LOOT_DROP_TYPE_RANDOM_EQUIPMENT: StringName = &"random_equipment"
const LOOT_DROP_TYPE_EQUIPMENT_INSTANCE: StringName = &"equipment_instance"
const LOOT_SOURCE_KIND_ENEMY_UNIT: StringName = &"enemy_unit"
const LOOT_SOURCE_KIND_CALAMITY_CONVERSION: StringName = &"calamity_conversion"
const LOOT_SOURCE_KIND_FATE_STATUS_DROP: StringName = &"fate_status_drop"
const LOOT_SOURCE_ID_ORDINARY_BATTLE: StringName = &"ordinary_battle"
const LOOT_SOURCE_ID_ELITE_BOSS_BATTLE: StringName = &"elite_boss_battle"
const CALAMITY_PER_SHARD := 2
const DOOM_SENTENCE_REFUND_CALAMITY := 5

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


func collect_defeated_unit_loot(unit_state: BattleUnitState, killer_unit: BattleUnitState = null) -> void:
	_collect_defeated_unit_loot(unit_state, killer_unit)


func build_battle_resolution_result():
	return _build_battle_resolution_result()


func _is_elite_or_boss_target(unit_state: BattleUnitState) -> bool:
	return unit_state != null \
		and unit_state.attribute_snapshot != null \
		and int(unit_state.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 0

func _collect_defeated_unit_loot(unit_state: BattleUnitState, killer_unit: BattleUnitState = null) -> void:
	if unit_state == null or unit_state.is_alive or unit_state.faction_id == &"player":
		return
	var defeated_unit_id := ProgressionDataUtils.to_string_name(unit_state.unit_id)
	if defeated_unit_id == &"" or _runtime._looted_defeated_unit_ids.has(defeated_unit_id):
		return
	_runtime._looted_defeated_unit_ids[defeated_unit_id] = true
	var enemy_template = _resolve_enemy_template_for_unit(unit_state)
	if enemy_template == null:
		return
	var drop_luck := _resolve_drop_luck_for_killer_unit(killer_unit)
	for loot_entry_variant in _build_defeated_unit_loot_entries(unit_state, enemy_template, drop_luck):
		if loot_entry_variant is not Dictionary:
			continue
		_runtime._active_loot_entries.append((loot_entry_variant as Dictionary).duplicate(true))


func _resolve_enemy_template_for_unit(unit_state: BattleUnitState):
	if unit_state == null:
		return null
	var template_id := ProgressionDataUtils.to_string_name(unit_state.enemy_template_id)
	if template_id == &"" or _runtime._enemy_templates == null or _runtime._enemy_templates.is_empty():
		return null
	return _runtime._enemy_templates.get(template_id)


func _build_defeated_unit_loot_entries(unit_state: BattleUnitState, enemy_template, drop_luck: int) -> Array:
	var loot_entries: Array = []
	if unit_state == null or enemy_template == null:
		return loot_entries
	var source_label := unit_state.display_name if not unit_state.display_name.is_empty() else String(unit_state.unit_id)
	var normalized_drop_luck := clampi(
		int(drop_luck),
		UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MIN,
		UNIT_BASE_ATTRIBUTES_SCRIPT.DROP_LUCK_MAX
	)
	var drop_entries: Array = enemy_template.get_drop_entries() if enemy_template.has_method("get_drop_entries") else []
	for drop_entry_variant in drop_entries:
		if drop_entry_variant is not Dictionary:
			continue
		var drop_entry_data := drop_entry_variant as Dictionary
		var drop_id := ProgressionDataUtils.to_string_name(drop_entry_data.get("drop_id", ""))
		var drop_type := ProgressionDataUtils.to_string_name(drop_entry_data.get("drop_type", ""))
		var item_id := ProgressionDataUtils.to_string_name(drop_entry_data.get("item_id", ""))
		var quantity := maxi(int(drop_entry_data.get("quantity", 0)), 0)
		if drop_id == &"" or drop_type == &"" or item_id == &"" or quantity <= 0:
			continue
		if drop_type == LOOT_DROP_TYPE_RANDOM_EQUIPMENT:
			if _runtime._equipment_drop_service != null and _runtime._equipment_drop_service.has_method("roll_item_instances"):
				var rolled_instances: Array = _runtime._equipment_drop_service.roll_item_instances(item_id, quantity, normalized_drop_luck)
				for instance_index in range(rolled_instances.size()):
					var loot_entry := _build_equipment_instance_loot_entry(
						LOOT_SOURCE_KIND_ENEMY_UNIT,
						unit_state.unit_id,
						source_label,
						"%s_%s_%d" % [String(unit_state.unit_id), String(drop_id), instance_index + 1],
						rolled_instances[instance_index]
					)
					if not loot_entry.is_empty():
						loot_entries.append(loot_entry)
				continue
			var fallback_entry := _build_formal_loot_entry(
				LOOT_SOURCE_KIND_ENEMY_UNIT,
				unit_state.unit_id,
				source_label,
				"%s_%s" % [String(unit_state.enemy_template_id if unit_state.enemy_template_id != &"" else unit_state.unit_id), String(drop_id)],
				item_id,
				quantity
			)
			if fallback_entry.is_empty():
				continue
			fallback_entry["drop_type"] = String(LOOT_DROP_TYPE_RANDOM_EQUIPMENT)
			fallback_entry["drop_luck"] = normalized_drop_luck
			loot_entries.append(fallback_entry)
			continue
		var fixed_entry := _build_formal_loot_entry(
			LOOT_SOURCE_KIND_ENEMY_UNIT,
			unit_state.unit_id,
			source_label,
			"%s_%s" % [String(unit_state.enemy_template_id if unit_state.enemy_template_id != &"" else unit_state.unit_id), String(drop_id)],
			item_id,
			quantity
		)
		if not fixed_entry.is_empty():
			loot_entries.append(fixed_entry)
	return loot_entries


func _build_equipment_instance_loot_entry(
	drop_source_kind: StringName,
	drop_source_id: StringName,
	drop_source_label: String,
	drop_entry_suffix: String,
	rolled_instance_variant: Variant
) -> Dictionary:
	var equipment_instance_data := _normalize_equipment_instance_loot_data(rolled_instance_variant)
	var item_id := ProgressionDataUtils.to_string_name(equipment_instance_data.get("item_id", ""))
	if equipment_instance_data.is_empty() or item_id == &"":
		return {}
	var source_label := drop_source_label.strip_edges()
	if source_label.is_empty():
		source_label = String(drop_source_id)
	var entry_suffix := drop_entry_suffix.strip_edges()
	if entry_suffix.is_empty():
		entry_suffix = "equipment_instance"
	return {
		"drop_type": String(LOOT_DROP_TYPE_EQUIPMENT_INSTANCE),
		"drop_source_kind": String(drop_source_kind),
		"drop_source_id": String(drop_source_id),
		"drop_source_label": source_label,
		"drop_entry_id": "%s_%s_%s" % [String(drop_source_kind), String(drop_source_id), entry_suffix],
		"item_id": String(item_id),
		"quantity": 1,
		"equipment_instance": equipment_instance_data,
	}


func _normalize_equipment_instance_loot_data(value: Variant) -> Dictionary:
	if value == null:
		return {}
	if value is EquipmentInstanceState:
		return (value as EquipmentInstanceState).to_dict()
	if value is Dictionary:
		var equipment_instance := EquipmentInstanceState.from_transient_loot_dict(value)
		if equipment_instance == null or equipment_instance.item_id == &"":
			return {}
		return equipment_instance.to_dict()
	if value.has_method("to_dict"):
		var instance_dict: Variant = value.to_dict()
		if instance_dict is Dictionary:
			return _normalize_equipment_instance_loot_data(instance_dict)
	return {}


func _resolve_drop_luck_for_killer_unit(killer_unit: BattleUnitState = null) -> int:
	if killer_unit == null or killer_unit.source_member_id == &"":
		return 0
	if _runtime._character_gateway == null or not _runtime._character_gateway.has_method("get_member_state"):
		return 0
	var member_state = _runtime._character_gateway.get_member_state(killer_unit.source_member_id)
	if member_state == null or not member_state.has_method("get_effective_luck"):
		return 0
	return clampi(
		int(member_state.get_effective_luck()),
		UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MIN,
		UNIT_BASE_ATTRIBUTES_SCRIPT.DROP_LUCK_MAX
	)



func _build_battle_resolution_result():
	var resolution_result = BATTLE_RESOLUTION_RESULT_SCRIPT.new()
	if _runtime._state == null:
		return resolution_result
	resolution_result.battle_id = _runtime._state.battle_id
	resolution_result.seed = int(_runtime._state.seed)
	resolution_result.world_coord = _runtime._state.world_coord
	resolution_result.encounter_anchor_id = _runtime._state.encounter_anchor_id
	resolution_result.terrain_profile_id = _runtime._state.terrain_profile_id
	resolution_result.winner_faction_id = _runtime._state.winner_faction_id
	resolution_result.encounter_resolution = _resolve_encounter_resolution()
	if resolution_result.winner_faction_id == &"player":
		resolution_result.set_loot_entries(_build_player_victory_loot_entries())
		resolution_result.party_resource_commit = _build_battle_party_resource_commit()
	else:
		resolution_result.set_loot_entries([])
		resolution_result.party_resource_commit = {}
	resolution_result.set_pending_character_rewards(_runtime._pending_post_battle_character_rewards)
	return resolution_result


func _build_player_victory_loot_entries() -> Array:
	var loot_entries: Array = []
	for loot_entry_variant in _runtime._active_loot_entries:
		if loot_entry_variant is Dictionary:
			loot_entries.append((loot_entry_variant as Dictionary).duplicate(true))
	loot_entries.append_array(_build_status_reward_loot_entries())
	loot_entries.append_array(_build_calamity_conversion_loot_entries())
	return loot_entries


func _build_battle_party_resource_commit() -> Dictionary:
	var returned_calamity := _get_doom_sentence_refund_calamity_total()
	var unused_calamity := _get_total_unused_calamity()
	var converted_shards := _calculate_calamity_conversion_shard_count()
	if returned_calamity <= 0 and unused_calamity <= 0 and converted_shards <= 0:
		return {}
	return {
		"unused_calamity": unused_calamity,
		"returned_calamity": returned_calamity,
		"converted_calamity_shards": converted_shards,
	}


func _build_status_reward_loot_entries() -> Array:
	var loot_entries: Array = []
	for defeated_unit in _get_defeated_enemy_units():
		if _should_grant_status_calamity_shard(defeated_unit):
			loot_entries.append(_build_formal_loot_entry(
				LOOT_SOURCE_KIND_FATE_STATUS_DROP,
				defeated_unit.unit_id,
				defeated_unit.display_name if not defeated_unit.display_name.is_empty() else String(defeated_unit.unit_id),
				"status_calamity_shard",
				CALAMITY_SHARD_ITEM_ID,
				1
			))
		if _should_grant_black_crown_core(defeated_unit):
			loot_entries.append(_build_formal_loot_entry(
				LOOT_SOURCE_KIND_FATE_STATUS_DROP,
				defeated_unit.unit_id,
				defeated_unit.display_name if not defeated_unit.display_name.is_empty() else String(defeated_unit.unit_id),
				"doom_sentence_black_crown_core",
				BLACK_CROWN_CORE_ITEM_ID,
				1
			))
	return loot_entries


func _build_calamity_conversion_loot_entries() -> Array:
	var shard_count := _calculate_calamity_conversion_shard_count()
	if shard_count <= 0:
		return []
	var battle_source_id := LOOT_SOURCE_ID_ELITE_BOSS_BATTLE if _battle_has_elite_or_boss_enemy() else LOOT_SOURCE_ID_ORDINARY_BATTLE
	var battle_source_label := "elite/boss 战未消耗 calamity 结算" if battle_source_id == LOOT_SOURCE_ID_ELITE_BOSS_BATTLE else "普通战未消耗 calamity 结算"
	return [_build_formal_loot_entry(
		LOOT_SOURCE_KIND_CALAMITY_CONVERSION,
		battle_source_id,
		battle_source_label,
		"calamity_conversion",
		CALAMITY_SHARD_ITEM_ID,
		shard_count
	)]


func _calculate_calamity_conversion_shard_count() -> int:
	var total_calamity := _get_total_unused_calamity() + _get_doom_sentence_refund_calamity_total()
	return maxi(int(total_calamity / CALAMITY_PER_SHARD), 0)


func _get_total_unused_calamity() -> int:
	var total_calamity := 0
	for calamity_variant in _runtime.calamity_by_member_id.values():
		total_calamity += maxi(int(calamity_variant), 0)
	return total_calamity


func _get_doom_sentence_refund_calamity_total() -> int:
	var refund_total := 0
	for defeated_unit in _get_defeated_enemy_units():
		if _should_grant_black_crown_core(defeated_unit):
			refund_total += DOOM_SENTENCE_REFUND_CALAMITY
	return refund_total


func _get_defeated_enemy_units() -> Array[BattleUnitState]:
	var defeated_units: Array[BattleUnitState] = []
	if _runtime._state == null:
		return defeated_units
	for enemy_unit_id in _runtime._state.enemy_unit_ids:
		var unit_state := _runtime._state.units.get(enemy_unit_id) as BattleUnitState
		if unit_state == null or unit_state.is_alive:
			continue
		defeated_units.append(unit_state)
	return defeated_units


func _should_grant_status_calamity_shard(unit_state: BattleUnitState) -> bool:
	return _is_elite_or_boss_target(unit_state) and (
		unit_state.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE)
		or _has_crown_break_seal(unit_state)
	)


func _should_grant_black_crown_core(unit_state: BattleUnitState) -> bool:
	return _is_boss_target(unit_state) and unit_state.has_status_effect(STATUS_DOOM_SENTENCE_VERDICT)


func _has_crown_break_seal(unit_state: BattleUnitState) -> bool:
	return unit_state != null and (
		unit_state.has_status_effect(STATUS_CROWN_BREAK_BROKEN_FANG)
		or unit_state.has_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND)
		or unit_state.has_status_effect(STATUS_CROWN_BREAK_BLINDED_EYE)
	)


func _is_boss_target(unit_state: BattleUnitState) -> bool:
	return unit_state != null \
		and unit_state.attribute_snapshot != null \
		and (
			int(unit_state.attribute_snapshot.get_value(BOSS_TARGET_STAT_ID)) > 0
			or int(unit_state.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 1
		)


func _battle_has_elite_or_boss_enemy() -> bool:
	if _runtime._state == null:
		return false
	for enemy_unit_id in _runtime._state.enemy_unit_ids:
		var unit_state := _runtime._state.units.get(enemy_unit_id) as BattleUnitState
		if _is_elite_or_boss_target(unit_state):
			return true
	return false


func _build_formal_loot_entry(
	drop_source_kind: StringName,
	drop_source_id: StringName,
	drop_source_label: String,
	drop_entry_suffix: String,
	item_id: StringName,
	quantity: int
) -> Dictionary:
	var normalized_source_kind := ProgressionDataUtils.to_string_name(drop_source_kind)
	var normalized_source_id := ProgressionDataUtils.to_string_name(drop_source_id)
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var normalized_quantity := maxi(int(quantity), 0)
	if normalized_source_kind == &"" or normalized_source_id == &"" or normalized_item_id == &"" or normalized_quantity <= 0:
		return {}
	var source_label := drop_source_label.strip_edges()
	if source_label.is_empty():
		source_label = String(normalized_source_id)
	var entry_suffix := drop_entry_suffix.strip_edges()
	if entry_suffix.is_empty():
		entry_suffix = "drop"
	return {
		"drop_type": "item",
		"drop_source_kind": String(normalized_source_kind),
		"drop_source_id": String(normalized_source_id),
		"drop_source_label": source_label,
		"drop_entry_id": "%s_%s_%s" % [String(normalized_source_kind), String(normalized_source_id), entry_suffix],
		"item_id": String(normalized_item_id),
		"quantity": normalized_quantity,
	}


func _resolve_encounter_resolution() -> StringName:
	if _runtime._state == null:
		return &""
	if _runtime._state.winner_faction_id == &"player":
		return &"player_victory"
	if _runtime._state.winner_faction_id == &"hostile":
		return &"hostile_victory"
	if _runtime._state.winner_faction_id == &"draw":
		return &"draw"
	return &"resolved"

