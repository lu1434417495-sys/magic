## 文件说明：该脚本集中持有 battle fate sidecars 与战后/据点 fate 奖励入口。
## 审查重点：重点核对 fate event bus 绑定顺序、battle-local calamity 生命周期以及战后奖励合并时机。
## 备注：Fortuna guidance 必须先于 FortuneService 绑定到同一条 event bus。

class_name FateRuntimeModule
extends RefCounted

const FORTUNE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/fate/fortune_service.gd")
const FORTUNA_GUIDANCE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/fate/fortuna_guidance_service.gd")
const LOW_LUCK_EVENT_SERVICE_SCRIPT = preload("res://scripts/systems/battle/fate/low_luck_event_service.gd")
const MISFORTUNE_GUIDANCE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/fate/misfortune_guidance_service.gd")
const MISFORTUNE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/fate/misfortune_service.gd")
const BATTLE_FATE_EVENT_BUS_SCRIPT = preload("res://scripts/systems/battle/fate/battle_fate_event_bus.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleFateEventBus = BATTLE_FATE_EVENT_BUS_SCRIPT
const BattleResolutionResult = BATTLE_RESOLUTION_RESULT_SCRIPT
const BattleState = BATTLE_STATE_SCRIPT

var _character_gateway: Object = null
var _battle_runtime_gateway: Object = null
var _unit_by_member_id_callback: Callable = Callable()
var _fortune_service = FORTUNE_SERVICE_SCRIPT.new()
var _fortuna_guidance_service = FORTUNA_GUIDANCE_SERVICE_SCRIPT.new()
var _low_luck_event_service = LOW_LUCK_EVENT_SERVICE_SCRIPT.new()
var _misfortune_guidance_service = MISFORTUNE_GUIDANCE_SERVICE_SCRIPT.new()
var _misfortune_service = MISFORTUNE_SERVICE_SCRIPT.new()


func setup(
	character_gateway: Object = null,
	fate_event_bus: BattleFateEventBus = null,
	battle_runtime_gateway: Object = null,
	unit_by_member_id_callback: Callable = Callable()
) -> void:
	_character_gateway = character_gateway
	_battle_runtime_gateway = battle_runtime_gateway
	_unit_by_member_id_callback = unit_by_member_id_callback if unit_by_member_id_callback.is_valid() else Callable()
	# Guidance must see the pre-mark state before FortuneService mutates fortune_marked on the same bus event.
	_fortuna_guidance_service.setup(_character_gateway, fate_event_bus)
	_fortune_service.setup(_character_gateway, fate_event_bus)
	_misfortune_service.setup(fate_event_bus, _unit_by_member_id_callback)
	_low_luck_event_service.setup(_character_gateway, fate_event_bus)
	_misfortune_guidance_service.setup(_character_gateway, _battle_runtime_gateway)


func dispose() -> void:
	if _fortuna_guidance_service != null:
		_fortuna_guidance_service.dispose()
	if _fortune_service != null:
		_fortune_service.dispose()
	if _misfortune_service != null:
		_misfortune_service.dispose()
	if _low_luck_event_service != null:
		_low_luck_event_service.dispose()
	if _misfortune_guidance_service != null:
		_misfortune_guidance_service.dispose()
	_character_gateway = null
	_battle_runtime_gateway = null
	_unit_by_member_id_callback = Callable()


func begin_battle(calamity_store: Dictionary = {}) -> void:
	if _misfortune_service != null:
		_misfortune_service.begin_battle(calamity_store)


func get_calamity_by_member_id() -> Dictionary:
	return _misfortune_service.get_calamity_by_member_id() if _misfortune_service != null else {}


func get_member_calamity(member_id: StringName) -> int:
	return _misfortune_service.get_member_calamity(member_id) if _misfortune_service != null else 0


func get_member_calamity_cap(member_id: StringName) -> int:
	return _misfortune_service.get_member_calamity_cap(member_id) if _misfortune_service != null else MISFORTUNE_SERVICE_SCRIPT.BASE_CALAMITY_CAP


func get_black_star_brand_cast_cost(member_id: StringName) -> int:
	return _misfortune_service.get_black_star_brand_calamity_cost(member_id) if _misfortune_service != null else MISFORTUNE_SERVICE_SCRIPT.BLACK_STAR_BRAND_REPEAT_CALAMITY_COST


func has_misfortune_reason(member_id: StringName, reason_id: StringName) -> bool:
	return _misfortune_service.has_triggered_reason(member_id, reason_id) if _misfortune_service != null else false


func get_misfortune_skill_cast_block_reason(unit_state, skill_id: StringName) -> String:
	if _misfortune_service == null:
		return MISFORTUNE_SERVICE_SCRIPT.get_skill_sidecar_missing_message(skill_id)
	return _misfortune_service.get_skill_cast_block_reason(unit_state, skill_id)


func consume_misfortune_skill_cast(unit_state, skill_id: StringName) -> Dictionary:
	if _misfortune_service == null:
		return {
			"ok": false,
			"message": MISFORTUNE_SERVICE_SCRIPT.get_skill_sidecar_missing_message(skill_id),
		}
	return _misfortune_service.consume_skill_cast(unit_state, skill_id)


func handle_misfortune_trigger(reason_id: StringName, payload: Dictionary = {}) -> Variant:
	return _misfortune_service.handle_trigger(reason_id, payload) if _misfortune_service != null else {}


func handle_member_boss_phase_changed(member_id: StringName, phase_id: StringName = &"") -> Dictionary:
	var unit_state = _resolve_unit_by_member_id(member_id)
	if unit_state == null:
		return {}
	var result = handle_misfortune_trigger(
		MISFORTUNE_SERVICE_SCRIPT.CALAMITY_REASON_BOSS_PHASE_CHANGED,
		{
			"unit_state": unit_state,
			"phase_id": phase_id,
		}
	)
	return result if result is Dictionary else {}


func handle_applied_statuses(target_unit, status_effect_ids: Variant) -> Dictionary:
	var result = handle_misfortune_trigger(
		MISFORTUNE_SERVICE_SCRIPT.CALAMITY_REASON_STRONG_DEBUFF,
		{
			"target_unit": target_unit,
			"status_effect_ids": status_effect_ids,
		}
	)
	return result if result is Dictionary else {}


func handle_battle_resolution(
	battle_state: BattleState,
	battle_resolution_result: BattleResolutionResult
) -> Dictionary:
	var low_luck_event_result: Dictionary = {}
	if _low_luck_event_service != null:
		low_luck_event_result = _low_luck_event_service.handle_battle_resolution(battle_state, battle_resolution_result)
		_merge_low_luck_battle_result_into_resolution(battle_resolution_result, low_luck_event_result)
	var fortuna_guidance_unlocks: Array[StringName] = []
	if _fortuna_guidance_service != null:
		fortuna_guidance_unlocks = _fortuna_guidance_service.handle_battle_resolution(battle_state, battle_resolution_result)
	var misfortune_guidance_unlocks: Array[StringName] = []
	if _misfortune_guidance_service != null:
		misfortune_guidance_unlocks = _misfortune_guidance_service.handle_battle_resolution(battle_state, battle_resolution_result)
	return {
		"fortuna_guidance_unlocks": fortuna_guidance_unlocks,
		"misfortune_guidance_unlocks": misfortune_guidance_unlocks,
		"low_luck_event_result": low_luck_event_result,
	}


func handle_fortuna_chapter_completed(payload: Dictionary) -> Array[StringName]:
	return _fortuna_guidance_service.handle_chapter_completed(payload) if _fortuna_guidance_service != null else []


func handle_misfortune_forge_result(
	member_id: StringName,
	result: Dictionary,
	item_defs: Dictionary = {}
) -> Array[StringName]:
	if _misfortune_guidance_service == null:
		return []
	return _misfortune_guidance_service.handle_forge_result(member_id, result, item_defs)


func resolve_low_luck_settlement_event_rewards(context: Dictionary) -> Dictionary:
	return _low_luck_event_service.handle_settlement_action(context) if _low_luck_event_service != null else {}


func clear_misfortune_exalted_ready_flags(member_ids: Array[StringName] = []) -> void:
	if _misfortune_guidance_service != null:
		_misfortune_guidance_service.clear_exalted_ready_flags(member_ids)


func set_fortune_confirmation_rng_for_testing(rng: Variant = null) -> void:
	if _fortune_service != null:
		_fortune_service.set_confirmation_rng_for_testing(rng)


func _resolve_unit_by_member_id(member_id: StringName):
	var normalized_member_id := ProgressionDataUtils.to_string_name(member_id)
	if normalized_member_id == &"" or not _unit_by_member_id_callback.is_valid():
		return null
	return _unit_by_member_id_callback.call(normalized_member_id)


func _merge_low_luck_battle_result_into_resolution(
	battle_resolution_result: BattleResolutionResult,
	low_luck_event_result: Dictionary
) -> void:
	if battle_resolution_result == null or low_luck_event_result.is_empty():
		return
	var extra_loot_variant: Variant = low_luck_event_result.get("loot_entries", [])
	if extra_loot_variant is Array and not (extra_loot_variant as Array).is_empty():
		var merged_loot_entries: Array = battle_resolution_result.loot_entries.duplicate(true)
		merged_loot_entries.append_array((extra_loot_variant as Array).duplicate(true))
		battle_resolution_result.set_loot_entries(merged_loot_entries)
	var extra_reward_variant: Variant = low_luck_event_result.get("pending_character_rewards", [])
	if extra_reward_variant is Array and not (extra_reward_variant as Array).is_empty():
		var merged_rewards: Array = battle_resolution_result.get_pending_character_rewards_copy()
		merged_rewards.append_array((extra_reward_variant as Array).duplicate(true))
		battle_resolution_result.set_pending_character_rewards(merged_rewards)
