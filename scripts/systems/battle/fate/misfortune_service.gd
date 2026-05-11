## 文件说明：该脚本属于 Misfortune 战斗资源 sidecar，负责监听战斗内坏运事件并维护临时 calamity 资源与逆运标记。
## 审查重点：重点核对 per-battle 首次触发锁、上限计算、以及是否只依赖 battle-local 数据而不写入永久进度。
## 备注：当前 boss 相变通过 runtime 显式 hook 接入；后续正式 boss phase 系统可直接复用这条入口。

class_name MisfortuneService
extends RefCounted

const BATTLE_FATE_EVENT_BUS_SCRIPT = preload("res://scripts/systems/battle/fate/battle_fate_event_bus.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const BattleFateEventBus = preload("res://scripts/systems/battle/fate/battle_fate_event_bus.gd")
const BattleState = BATTLE_STATE_SCRIPT
const BattleStatusEffectState = BATTLE_STATUS_EFFECT_STATE_SCRIPT
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

const CALAMITY_REASON_ORDINARY_MISS: StringName = &"ordinary_miss"
const CALAMITY_REASON_CRITICAL_FAIL: StringName = &"critical_fail"
const CALAMITY_REASON_STRONG_DEBUFF: StringName = &"strong_debuff"
const CALAMITY_REASON_ADJACENT_ALLY_DEFEATED: StringName = &"adjacent_ally_defeated"
const CALAMITY_REASON_LOW_HP_END_TURN: StringName = &"low_hp_end_turn"
const CALAMITY_REASON_BOSS_PHASE_CHANGED: StringName = &"boss_phase_changed"

const CALAMITY_CAPACITY_BONUS_STAT_ID: StringName = &"calamity_capacity_bonus"
const REVERSE_FORTUNE_STATUS_ID: StringName = &"reverse_fortune"
const MISSTEP_TO_SCHEME_SKILL_ID: StringName = &"misstep_to_scheme"
const BLACK_STAR_BRAND_SKILL_ID: StringName = &"black_star_brand"
const CROWN_BREAK_SKILL_ID: StringName = &"crown_break"
const DOOM_SENTENCE_SKILL_ID: StringName = &"doom_sentence"
const BLACK_CROWN_SEAL_SKILL_ID: StringName = &"black_crown_seal"
const BASE_CALAMITY_CAP := 3
const MAX_CALAMITY_CAPACITY_BONUS := 2
const REVERSE_FORTUNE_DURATION_TU := 60
const BLACK_STAR_BRAND_REPEAT_CALAMITY_COST := 1
const CROWN_BREAK_CALAMITY_COST := 2
const DOOM_SENTENCE_CALAMITY_COST := 5
const MISFORTUNE_SKILL_GATE_TYPE_BLACK_STAR_BRAND: StringName = &"black_star_brand"
const MISFORTUNE_SKILL_GATE_TYPE_CROWN_BREAK: StringName = &"crown_break"
const MISFORTUNE_SKILL_GATE_TYPE_DOOM_SENTENCE: StringName = &"doom_sentence"
const MISFORTUNE_SKILL_GATE_TYPE_BLACK_CROWN_SEAL: StringName = &"black_crown_seal"
const MISFORTUNE_SKILL_GATE_RULES := {
	&"black_star_brand": {
		"gate_type": MISFORTUNE_SKILL_GATE_TYPE_BLACK_STAR_BRAND,
		"sidecar_missing_message": "黑星烙印的 calamity sidecar 未初始化。",
		"default_block_message": "calamity 不足，无法施放黑星烙印。",
	},
	&"crown_break": {
		"gate_type": MISFORTUNE_SKILL_GATE_TYPE_CROWN_BREAK,
		"sidecar_missing_message": "折冠的 calamity sidecar 未初始化。",
		"default_block_message": "calamity 不足，无法施放折冠。",
	},
	&"doom_sentence": {
		"gate_type": MISFORTUNE_SKILL_GATE_TYPE_DOOM_SENTENCE,
		"sidecar_missing_message": "厄命宣判的 calamity sidecar 未初始化。",
		"default_block_message": "calamity 不足，无法施放厄命宣判。",
	},
	&"black_crown_seal": {
		"gate_type": MISFORTUNE_SKILL_GATE_TYPE_BLACK_CROWN_SEAL,
		"sidecar_missing_message": "黑冠封印的 battle sidecar 未初始化。",
		"default_block_message": "黑冠封印每战只能施放 1 次。",
	},
}

var _fate_event_bus: BattleFateEventBus = null
var _unit_by_member_id_callback: Callable = Callable()
var _calamity_by_member_id: Dictionary = {}
var _reason_flags_by_member_id: Dictionary = {}
var _processed_adjacent_defeat_unit_ids: Dictionary = {}
var _misstep_to_scheme_used_by_member_id: Dictionary = {}
var _black_star_brand_free_used_by_member_id: Dictionary = {}
var _black_crown_seal_used_by_member_id: Dictionary = {}
var _doom_sentence_used_by_member_id: Dictionary = {}


static func is_misfortune_gated_skill(skill_id: StringName) -> bool:
	return MISFORTUNE_SKILL_GATE_RULES.has(ProgressionDataUtils.to_string_name(skill_id))


static func get_skill_sidecar_missing_message(skill_id: StringName) -> String:
	var rule := _get_skill_gate_rule(skill_id)
	return String(rule.get("sidecar_missing_message", "Misfortune battle sidecar 未初始化。"))


static func get_skill_default_block_message(skill_id: StringName) -> String:
	var rule := _get_skill_gate_rule(skill_id)
	return String(rule.get("default_block_message", "calamity 不足，无法施放该技能。"))


static func _get_skill_gate_rule(skill_id: StringName) -> Dictionary:
	var normalized_skill_id := ProgressionDataUtils.to_string_name(skill_id)
	var rule_variant: Variant = MISFORTUNE_SKILL_GATE_RULES.get(normalized_skill_id, {})
	return rule_variant if rule_variant is Dictionary else {}


func setup(
	fate_event_bus: BattleFateEventBus = null,
	unit_by_member_id_callback: Callable = Callable()
) -> void:
	_unit_by_member_id_callback = unit_by_member_id_callback if unit_by_member_id_callback.is_valid() else Callable()
	bind_fate_event_bus(fate_event_bus)


func begin_battle(calamity_store: Dictionary = {}) -> void:
	_reason_flags_by_member_id.clear()
	_processed_adjacent_defeat_unit_ids.clear()
	_misstep_to_scheme_used_by_member_id.clear()
	_black_star_brand_free_used_by_member_id.clear()
	_black_crown_seal_used_by_member_id.clear()
	_doom_sentence_used_by_member_id.clear()
	_calamity_by_member_id = calamity_store if calamity_store != null else {}
	_calamity_by_member_id.clear()


func bind_fate_event_bus(fate_event_bus: BattleFateEventBus = null) -> void:
	if _fate_event_bus != null and _fate_event_bus.event_dispatched.is_connected(_on_fate_event):
		_fate_event_bus.event_dispatched.disconnect(_on_fate_event)
	_fate_event_bus = fate_event_bus
	if _fate_event_bus != null and not _fate_event_bus.event_dispatched.is_connected(_on_fate_event):
		_fate_event_bus.event_dispatched.connect(_on_fate_event)


func dispose() -> void:
	bind_fate_event_bus(null)
	_unit_by_member_id_callback = Callable()
	_calamity_by_member_id = {}
	_reason_flags_by_member_id.clear()
	_processed_adjacent_defeat_unit_ids.clear()
	_misstep_to_scheme_used_by_member_id.clear()
	_black_star_brand_free_used_by_member_id.clear()
	_black_crown_seal_used_by_member_id.clear()
	_doom_sentence_used_by_member_id.clear()


func get_calamity_by_member_id() -> Dictionary:
	return ProgressionDataUtils.to_string_name_int_map(_calamity_by_member_id).duplicate(true)


func get_member_calamity(member_id: StringName) -> int:
	var normalized_member_id := ProgressionDataUtils.to_string_name(member_id)
	if normalized_member_id == &"":
		return 0
	return maxi(int(_calamity_by_member_id.get(normalized_member_id, 0)), 0)


func get_member_calamity_cap(member_id: StringName) -> int:
	return _calculate_calamity_cap(_resolve_unit_by_member_id(member_id))


func get_black_star_brand_calamity_cost(member_id: StringName) -> int:
	var normalized_member_id := ProgressionDataUtils.to_string_name(member_id)
	if normalized_member_id == &"":
		return BLACK_STAR_BRAND_REPEAT_CALAMITY_COST
	return 0 if not bool(_black_star_brand_free_used_by_member_id.get(normalized_member_id, false)) else BLACK_STAR_BRAND_REPEAT_CALAMITY_COST


func can_cast_black_star_brand(unit_state: BattleUnitState) -> bool:
	if unit_state == null:
		return false
	var member_id := ProgressionDataUtils.to_string_name(unit_state.source_member_id)
	if member_id == &"":
		return false
	var calamity_cost := get_black_star_brand_calamity_cost(member_id)
	return calamity_cost <= 0 or get_member_calamity(member_id) >= calamity_cost


func get_skill_cast_block_reason(unit_state: BattleUnitState, skill_id: StringName) -> String:
	var rule := _get_skill_gate_rule(skill_id)
	if rule.is_empty():
		return ""
	var gate_type := ProgressionDataUtils.to_string_name(rule.get("gate_type", ""))
	match gate_type:
		MISFORTUNE_SKILL_GATE_TYPE_BLACK_STAR_BRAND:
			if can_cast_black_star_brand(unit_state):
				return ""
			return get_skill_default_block_message(skill_id)
		MISFORTUNE_SKILL_GATE_TYPE_CROWN_BREAK:
			if can_cast_crown_break(unit_state):
				return ""
			return get_skill_default_block_message(skill_id)
		MISFORTUNE_SKILL_GATE_TYPE_DOOM_SENTENCE:
			return get_doom_sentence_cast_block_reason(unit_state)
		MISFORTUNE_SKILL_GATE_TYPE_BLACK_CROWN_SEAL:
			return get_black_crown_seal_cast_block_reason(unit_state)
		_:
			return ""


func consume_skill_cast(unit_state: BattleUnitState, skill_id: StringName) -> Dictionary:
	var rule := _get_skill_gate_rule(skill_id)
	if rule.is_empty():
		return {
			"ok": true,
			"gated": false,
			"member_id": String(ProgressionDataUtils.to_string_name(unit_state.source_member_id)) if unit_state != null else "",
		}
	var gate_type := ProgressionDataUtils.to_string_name(rule.get("gate_type", ""))
	match gate_type:
		MISFORTUNE_SKILL_GATE_TYPE_BLACK_STAR_BRAND:
			return consume_black_star_brand_cast(unit_state)
		MISFORTUNE_SKILL_GATE_TYPE_CROWN_BREAK:
			return consume_crown_break_cast(unit_state)
		MISFORTUNE_SKILL_GATE_TYPE_DOOM_SENTENCE:
			return consume_doom_sentence_cast(unit_state)
		MISFORTUNE_SKILL_GATE_TYPE_BLACK_CROWN_SEAL:
			return consume_black_crown_seal_cast(unit_state)
		_:
			return {
				"ok": true,
				"gated": false,
				"member_id": String(ProgressionDataUtils.to_string_name(unit_state.source_member_id)) if unit_state != null else "",
			}


func consume_black_star_brand_cast(unit_state: BattleUnitState) -> Dictionary:
	if unit_state == null:
		return {
			"ok": false,
			"message": "技能施放者无效。",
			"calamity_cost": BLACK_STAR_BRAND_REPEAT_CALAMITY_COST,
		}

	var member_id := ProgressionDataUtils.to_string_name(unit_state.source_member_id)
	if member_id == &"":
		return {
			"ok": false,
			"message": "黑星烙印只能由正式成员施放。",
			"calamity_cost": BLACK_STAR_BRAND_REPEAT_CALAMITY_COST,
		}

	var calamity_cost := get_black_star_brand_calamity_cost(member_id)
	var current_calamity := get_member_calamity(member_id)
	if calamity_cost > 0 and current_calamity < calamity_cost:
		return {
			"ok": false,
			"message": "calamity 不足，无法施放黑星烙印。",
			"calamity_cost": calamity_cost,
			"remaining_calamity": current_calamity,
		}

	if calamity_cost > 0:
		_calamity_by_member_id[member_id] = maxi(current_calamity - calamity_cost, 0)
	_black_star_brand_free_used_by_member_id[member_id] = true
	return {
		"ok": true,
		"member_id": String(member_id),
		"calamity_cost": calamity_cost,
		"free_cast": calamity_cost <= 0,
		"remaining_calamity": get_member_calamity(member_id),
	}


func can_cast_crown_break(unit_state: BattleUnitState) -> bool:
	if unit_state == null:
		return false
	var member_id := ProgressionDataUtils.to_string_name(unit_state.source_member_id)
	if member_id == &"":
		return false
	return get_member_calamity(member_id) >= CROWN_BREAK_CALAMITY_COST


func consume_crown_break_cast(unit_state: BattleUnitState) -> Dictionary:
	if unit_state == null:
		return {
			"ok": false,
			"message": "技能施放者无效。",
			"calamity_cost": CROWN_BREAK_CALAMITY_COST,
		}

	var member_id := ProgressionDataUtils.to_string_name(unit_state.source_member_id)
	if member_id == &"":
		return {
			"ok": false,
			"message": "折冠只能由正式成员施放。",
			"calamity_cost": CROWN_BREAK_CALAMITY_COST,
		}

	var current_calamity := get_member_calamity(member_id)
	if current_calamity < CROWN_BREAK_CALAMITY_COST:
		return {
			"ok": false,
			"message": "calamity 不足，无法施放折冠。",
			"calamity_cost": CROWN_BREAK_CALAMITY_COST,
			"remaining_calamity": current_calamity,
		}

	_calamity_by_member_id[member_id] = maxi(current_calamity - CROWN_BREAK_CALAMITY_COST, 0)
	return {
		"ok": true,
		"member_id": String(member_id),
		"calamity_cost": CROWN_BREAK_CALAMITY_COST,
		"remaining_calamity": get_member_calamity(member_id),
	}


func get_doom_sentence_cast_block_reason(unit_state: BattleUnitState) -> String:
	if unit_state == null:
		return "技能施放者无效。"

	var member_id := ProgressionDataUtils.to_string_name(unit_state.source_member_id)
	if member_id == &"":
		return "厄命宣判只能由正式成员施放。"
	if bool(_doom_sentence_used_by_member_id.get(member_id, false)):
		return "厄命宣判每战只能施放 1 次。"
	if get_member_calamity_cap(member_id) < DOOM_SENTENCE_CALAMITY_COST:
		return "本战 calamity 上限不足 5，无法施放厄命宣判。"
	if get_member_calamity(member_id) < DOOM_SENTENCE_CALAMITY_COST:
		return "calamity 不足，无法施放厄命宣判。"
	return ""


func can_cast_doom_sentence(unit_state: BattleUnitState) -> bool:
	return get_doom_sentence_cast_block_reason(unit_state).is_empty()


func consume_doom_sentence_cast(unit_state: BattleUnitState) -> Dictionary:
	var block_reason := get_doom_sentence_cast_block_reason(unit_state)
	var member_id := ProgressionDataUtils.to_string_name(unit_state.source_member_id) if unit_state != null else &""
	if not block_reason.is_empty():
		return {
			"ok": false,
			"message": block_reason,
			"calamity_cost": DOOM_SENTENCE_CALAMITY_COST,
			"remaining_calamity": get_member_calamity(member_id),
		}

	var current_calamity := get_member_calamity(member_id)
	_calamity_by_member_id[member_id] = maxi(current_calamity - DOOM_SENTENCE_CALAMITY_COST, 0)
	_doom_sentence_used_by_member_id[member_id] = true
	return {
		"ok": true,
		"member_id": String(member_id),
		"calamity_cost": DOOM_SENTENCE_CALAMITY_COST,
		"remaining_calamity": get_member_calamity(member_id),
	}


func get_black_crown_seal_cast_block_reason(unit_state: BattleUnitState) -> String:
	if unit_state == null:
		return "技能施放者无效。"

	var member_id := ProgressionDataUtils.to_string_name(unit_state.source_member_id)
	if member_id == &"":
		return "黑冠封印只能由正式成员施放。"
	if bool(_black_crown_seal_used_by_member_id.get(member_id, false)):
		return "黑冠封印每战只能施放 1 次。"
	return ""


func can_cast_black_crown_seal(unit_state: BattleUnitState) -> bool:
	return get_black_crown_seal_cast_block_reason(unit_state).is_empty()


func consume_black_crown_seal_cast(unit_state: BattleUnitState) -> Dictionary:
	var block_reason := get_black_crown_seal_cast_block_reason(unit_state)
	var member_id := ProgressionDataUtils.to_string_name(unit_state.source_member_id) if unit_state != null else &""
	if not block_reason.is_empty():
		return {
			"ok": false,
			"message": block_reason,
			"member_id": String(member_id),
		}

	_black_crown_seal_used_by_member_id[member_id] = true
	return {
		"ok": true,
		"member_id": String(member_id),
	}


func has_triggered_reason(member_id: StringName, reason_id: StringName) -> bool:
	var normalized_member_id := ProgressionDataUtils.to_string_name(member_id)
	var normalized_reason_id := ProgressionDataUtils.to_string_name(reason_id)
	if normalized_member_id == &"" or normalized_reason_id == &"":
		return false
	var member_reason_flags: Dictionary = {}
	var member_reason_flags_variant: Variant = _reason_flags_by_member_id.get(normalized_member_id, {})
	if member_reason_flags_variant is Dictionary:
		member_reason_flags = member_reason_flags_variant
	return bool(member_reason_flags.get(normalized_reason_id, false))


func handle_trigger(reason_id: StringName, payload: Dictionary = {}) -> Variant:
	var normalized_reason_id := ProgressionDataUtils.to_string_name(reason_id)
	match normalized_reason_id:
		CALAMITY_REASON_STRONG_DEBUFF:
			return _handle_strong_debuff_trigger(payload)
		CALAMITY_REASON_ADJACENT_ALLY_DEFEATED:
			return _handle_adjacent_ally_defeat_trigger(payload)
		CALAMITY_REASON_LOW_HP_END_TURN:
			return _handle_low_hp_turn_end_trigger(payload)
		CALAMITY_REASON_BOSS_PHASE_CHANGED:
			return _handle_boss_phase_changed_trigger(payload)
		CALAMITY_REASON_ORDINARY_MISS, CALAMITY_REASON_CRITICAL_FAIL:
			return _handle_member_reason_trigger(payload, normalized_reason_id)
		_:
			return {}


func handle_applied_statuses(target_unit: BattleUnitState, status_effect_ids: Variant) -> Dictionary:
	var result = handle_trigger(CALAMITY_REASON_STRONG_DEBUFF, {
		"target_unit": target_unit,
		"status_effect_ids": status_effect_ids,
	})
	return result if result is Dictionary else {}


func handle_adjacent_ally_defeat(defeated_unit: BattleUnitState, adjacent_units: Array) -> Array[Dictionary]:
	var result = handle_trigger(CALAMITY_REASON_ADJACENT_ALLY_DEFEATED, {
		"defeated_unit": defeated_unit,
		"adjacent_units": adjacent_units,
	})
	var typed_results: Array[Dictionary] = []
	if result is Array:
		for result_entry in result:
			if result_entry is Dictionary:
				typed_results.append(result_entry)
	return typed_results


func handle_low_hp_turn_end(unit_state: BattleUnitState) -> Dictionary:
	var result = handle_trigger(CALAMITY_REASON_LOW_HP_END_TURN, {
		"unit_state": unit_state,
	})
	return result if result is Dictionary else {}


func handle_boss_phase_changed(unit_state: BattleUnitState, phase_id: StringName = &"") -> Dictionary:
	var result = handle_trigger(CALAMITY_REASON_BOSS_PHASE_CHANGED, {
		"unit_state": unit_state,
		"phase_id": phase_id,
	})
	return result if result is Dictionary else {}


func _handle_strong_debuff_trigger(payload: Dictionary) -> Dictionary:
	var target_unit := payload.get("target_unit", null) as BattleUnitState
	if target_unit == null:
		return {}
	var strong_status_ids := _extract_strong_attack_debuff_ids(payload.get("status_effect_ids", []))
	if strong_status_ids.is_empty():
		return {}
	return _register_reason(target_unit, CALAMITY_REASON_STRONG_DEBUFF, {
		"status_ids": ProgressionDataUtils.string_name_array_to_string_array(strong_status_ids),
	})


func _handle_adjacent_ally_defeat_trigger(payload: Dictionary) -> Array[Dictionary]:
	var defeated_unit := payload.get("defeated_unit", null) as BattleUnitState
	var adjacent_units: Array = payload.get("adjacent_units", [])
	var results: Array[Dictionary] = []
	if defeated_unit == null or defeated_unit.unit_id == &"":
		return results
	if _processed_adjacent_defeat_unit_ids.has(defeated_unit.unit_id):
		return results
	_processed_adjacent_defeat_unit_ids[defeated_unit.unit_id] = true
	for unit_variant in adjacent_units:
		var observer_unit := unit_variant as BattleUnitState
		if observer_unit == null:
			continue
		var result := _register_reason(observer_unit, CALAMITY_REASON_ADJACENT_ALLY_DEFEATED, {
			"defeated_unit_id": String(defeated_unit.unit_id),
		})
		if not result.is_empty():
			results.append(result)
	return results


func _handle_low_hp_turn_end_trigger(payload: Dictionary) -> Dictionary:
	var unit_state := payload.get("unit_state", null) as BattleUnitState
	if unit_state == null or not unit_state.is_alive or not _is_low_hp_hardship(unit_state):
		return {}
	return _register_reason(unit_state, CALAMITY_REASON_LOW_HP_END_TURN)


func _handle_boss_phase_changed_trigger(payload: Dictionary) -> Dictionary:
	var unit_state := payload.get("unit_state", null) as BattleUnitState
	var phase_id := ProgressionDataUtils.to_string_name(payload.get("phase_id", ""))
	return _register_reason(unit_state, CALAMITY_REASON_BOSS_PHASE_CHANGED, {
		"phase_id": String(ProgressionDataUtils.to_string_name(phase_id)),
	})


func _handle_member_reason_trigger(payload: Dictionary, reason_id: StringName) -> Dictionary:
	var unit_state := payload.get("unit_state", null) as BattleUnitState
	if unit_state != null:
		return _register_reason(unit_state, reason_id)
	var member_id := ProgressionDataUtils.to_string_name(payload.get("member_id", ""))
	if member_id == &"":
		return {}
	var resolved_unit := _resolve_unit_by_member_id(member_id)
	if resolved_unit == null:
		return {}
	return _register_reason(resolved_unit, reason_id)


func _on_fate_event(event_type: StringName, payload: Dictionary) -> void:
	match event_type:
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_ORDINARY_MISS:
			_handle_fate_payload_reason(payload, CALAMITY_REASON_ORDINARY_MISS)
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_FAIL:
			_handle_fate_payload_reason(payload, CALAMITY_REASON_CRITICAL_FAIL)
		_:
			return


func _handle_fate_payload_reason(payload: Dictionary, reason_id: StringName) -> void:
	var member_id := ProgressionDataUtils.to_string_name(payload.get("attacker_member_id", ""))
	if member_id == &"":
		return
	handle_trigger(reason_id, {"member_id": member_id})


func _register_reason(unit_state: BattleUnitState, reason_id: StringName, metadata: Dictionary = {}) -> Dictionary:
	var normalized_reason_id := ProgressionDataUtils.to_string_name(reason_id)
	if unit_state == null or normalized_reason_id == &"":
		return {}
	var member_id := ProgressionDataUtils.to_string_name(unit_state.source_member_id)
	if member_id == &"":
		return {}

	var member_reason_flags := _ensure_member_reason_flags(member_id)
	var was_first_reason := member_reason_flags.is_empty()
	if bool(member_reason_flags.get(normalized_reason_id, false)):
		return {
			"member_id": String(member_id),
			"reason_id": String(normalized_reason_id),
			"granted": false,
			"already_triggered": true,
			"calamity": get_member_calamity(member_id),
			"cap": _calculate_calamity_cap(unit_state),
		}

	member_reason_flags[normalized_reason_id] = true
	var previous_calamity := get_member_calamity(member_id)
	var calamity_cap := _calculate_calamity_cap(unit_state)
	var intended_gain := 1 + _get_bonus_calamity_for_reason(unit_state, normalized_reason_id)
	var next_calamity := mini(previous_calamity + intended_gain, calamity_cap)
	var granted_calamity := maxi(next_calamity - previous_calamity, 0)
	var bonus_calamity := maxi(granted_calamity - 1, 0)
	_calamity_by_member_id[member_id] = next_calamity
	var reverse_fortune_granted := false
	if was_first_reason and normalized_reason_id == CALAMITY_REASON_CRITICAL_FAIL:
		reverse_fortune_granted = _grant_reverse_fortune(unit_state)

	return {
		"member_id": String(member_id),
		"reason_id": String(normalized_reason_id),
		"granted": granted_calamity > 0,
		"already_triggered": false,
		"calamity": next_calamity,
		"bonus_calamity": bonus_calamity,
		"cap": calamity_cap,
		"reverse_fortune_granted": reverse_fortune_granted,
		"metadata": metadata.duplicate(true),
	}


func _grant_reverse_fortune(unit_state: BattleUnitState) -> bool:
	if unit_state == null:
		return false
	var status_entry := BattleStatusEffectState.new()
	status_entry.status_id = REVERSE_FORTUNE_STATUS_ID
	status_entry.source_unit_id = unit_state.unit_id
	status_entry.power = 1
	status_entry.stacks = 1
	status_entry.duration = REVERSE_FORTUNE_DURATION_TU
	unit_state.set_status_effect(status_entry)
	return true


func _calculate_calamity_cap(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return BASE_CALAMITY_CAP
	var calamity_capacity_bonus := mini(
		maxi(int(unit_state.attribute_snapshot.get_value(CALAMITY_CAPACITY_BONUS_STAT_ID)), 0),
		MAX_CALAMITY_CAPACITY_BONUS
	)
	var hidden_luck_at_birth := int(unit_state.attribute_snapshot.get_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH))
	return BASE_CALAMITY_CAP + calamity_capacity_bonus + (1 if hidden_luck_at_birth <= -5 else 0)


func _extract_strong_attack_debuff_ids(status_effect_ids: Variant) -> Array[StringName]:
	var strong_status_ids: Array[StringName] = []
	if status_effect_ids is not Array:
		return strong_status_ids
	for status_id_variant in status_effect_ids:
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		if status_id == &"":
			continue
		if not BattleState.STRONG_ATTACK_DISADVANTAGE_STATUS_IDS.has(status_id):
			continue
		if strong_status_ids.has(status_id):
			continue
		strong_status_ids.append(status_id)
	return strong_status_ids


func _is_low_hp_hardship(unit_state: BattleUnitState) -> bool:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return false
	var max_hp := maxi(int(unit_state.attribute_snapshot.get_value(&"hp_max")), 0)
	if max_hp <= 0:
		return false
	return int(unit_state.current_hp) * 100 <= max_hp * int(BattleState.LOW_HP_ATTACK_DISADVANTAGE_PERCENT)


func _ensure_member_reason_flags(member_id: StringName) -> Dictionary:
	var normalized_member_id := ProgressionDataUtils.to_string_name(member_id)
	if normalized_member_id == &"":
		return {}
	if not _reason_flags_by_member_id.has(normalized_member_id):
		_reason_flags_by_member_id[normalized_member_id] = {}
	var member_reason_flags_variant: Variant = _reason_flags_by_member_id.get(normalized_member_id, {})
	return member_reason_flags_variant as Dictionary


func _resolve_unit_by_member_id(member_id: StringName) -> BattleUnitState:
	var normalized_member_id := ProgressionDataUtils.to_string_name(member_id)
	if normalized_member_id == &"" or not _unit_by_member_id_callback.is_valid():
		return null
	var unit_variant = _unit_by_member_id_callback.call(normalized_member_id)
	return unit_variant as BattleUnitState


func _get_bonus_calamity_for_reason(unit_state: BattleUnitState, reason_id: StringName) -> int:
	if unit_state == null or reason_id != CALAMITY_REASON_CRITICAL_FAIL:
		return 0
	var member_id := ProgressionDataUtils.to_string_name(unit_state.source_member_id)
	if member_id == &"":
		return 0
	if bool(_misstep_to_scheme_used_by_member_id.get(member_id, false)):
		return 0
	if not _unit_has_skill(unit_state, MISSTEP_TO_SCHEME_SKILL_ID):
		return 0
	_misstep_to_scheme_used_by_member_id[member_id] = true
	return 1


func _unit_has_skill(unit_state: BattleUnitState, skill_id: StringName) -> bool:
	if unit_state == null or skill_id == &"":
		return false
	if unit_state.known_active_skill_ids.has(skill_id):
		return true
	return int(unit_state.known_skill_level_map.get(skill_id, 0)) > 0
