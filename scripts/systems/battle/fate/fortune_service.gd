## 文件说明：该脚本属于 Fortuna 命运标记相关的服务脚本，负责订阅 battle fate event 并在满足条件时写入 fortune_marked。
## 审查重点：重点核对 per-run 尝试锁、二次确认骰规则，以及只通过 readonly payload 驱动而不回读可变 battle 对象。
## 备注：fortune_marked 当前只依赖命运事件与确认骰；目标是否为 elite / boss 由更高阶 guidance 成就单独处理，不在这里判定。

class_name FortuneService
extends RefCounted

const BATTLE_FATE_EVENT_BUS_SCRIPT = preload("res://scripts/systems/battle/fate/battle_fate_event_bus.gd")
const FATE_ATTACK_FORMULA_SCRIPT = preload("res://scripts/systems/battle/fate/fate_attack_formula.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const BattleFateEventBus = preload("res://scripts/systems/battle/fate/battle_fate_event_bus.gd")
const PartyState = PARTY_STATE_SCRIPT
const PartyMemberState = PARTY_MEMBER_STATE_SCRIPT

const FORTUNE_MARKED_STAT_ID: StringName = &"fortune_marked"
const FORTUNE_MARK_ATTEMPT_FLAG_PREFIX := "fortune_mark_attempted:"

var _character_gateway: Object = null
var _fate_event_bus: BattleFateEventBus = null
var _confirmation_rng_override: Variant = null


func setup(character_gateway: Object = null, fate_event_bus: BattleFateEventBus = null) -> void:
	_character_gateway = character_gateway
	bind_fate_event_bus(fate_event_bus)


func bind_fate_event_bus(fate_event_bus: BattleFateEventBus = null) -> void:
	if _fate_event_bus != null and _fate_event_bus.event_dispatched.is_connected(_on_fate_event):
		_fate_event_bus.event_dispatched.disconnect(_on_fate_event)
	_fate_event_bus = fate_event_bus
	if _fate_event_bus != null and not _fate_event_bus.event_dispatched.is_connected(_on_fate_event):
		_fate_event_bus.event_dispatched.connect(_on_fate_event)


func dispose() -> void:
	bind_fate_event_bus(null)
	_character_gateway = null
	_confirmation_rng_override = null


func set_confirmation_rng_for_testing(rng: Variant = null) -> void:
	_confirmation_rng_override = rng if rng != null and rng.has_method("randi_range") else null


func has_attempted_fortune_mark(member_id: StringName) -> bool:
	var party_state := _get_party_state()
	if party_state == null or member_id == &"":
		return false
	return party_state.has_fate_run_flag(_build_fortune_mark_attempt_flag_id(member_id))


func try_grant_fortune_mark_from_payload(payload: Dictionary) -> bool:
	var attacker_member_id := ProgressionDataUtils.to_string_name(payload.get("attacker_member_id", ""))
	if attacker_member_id == &"":
		return false

	var party_state := _get_party_state()
	if party_state == null:
		return false
	if party_state.has_fate_run_flag(_build_fortune_mark_attempt_flag_id(attacker_member_id)):
		return false

	var member_state := _get_member_state(attacker_member_id)
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return false
	if _get_custom_stat_value(member_state, FORTUNE_MARKED_STAT_ID) >= 1:
		return false

	party_state.set_fate_run_flag(_build_fortune_mark_attempt_flag_id(attacker_member_id), true)

	var crit_gate_die := maxi(int(payload.get("crit_gate_die", 0)), 1)
	var is_disadvantage := bool(payload.get("is_disadvantage", false))
	var confirmation_roll := int(FATE_ATTACK_FORMULA_SCRIPT.roll_die_with_disadvantage_rule(
		crit_gate_die,
		is_disadvantage,
		_resolve_confirmation_rng(payload)
	))
	if confirmation_roll < crit_gate_die:
		return false

	member_state.progression.unit_base_attributes.set_attribute_value(FORTUNE_MARKED_STAT_ID, 1)
	return true


func _on_fate_event(event_type: StringName, payload: Dictionary) -> void:
	if event_type != BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE:
		return
	try_grant_fortune_mark_from_payload(payload)


func _resolve_confirmation_rng(payload: Dictionary) -> Variant:
	if _confirmation_rng_override != null and _confirmation_rng_override.has_method("randi_range"):
		return _confirmation_rng_override
	var rng := RandomNumberGenerator.new()
	rng.seed = int(_build_confirmation_seed_source(payload).hash())
	return rng


func _build_confirmation_seed_source(payload: Dictionary) -> String:
	return "%s:%s:%s:%s:%d:%d" % [
		String(payload.get("battle_id", "")),
		String(payload.get("attacker_member_id", "")),
		String(payload.get("attacker_id", "")),
		String(payload.get("defender_id", "")),
		int(payload.get("crit_gate_die", 0)),
		1 if bool(payload.get("is_disadvantage", false)) else 0,
	]


func _get_party_state() -> PartyState:
	if _character_gateway == null or not _character_gateway.has_method("get_party_state"):
		return null
	return _character_gateway.get_party_state() as PartyState


func _get_member_state(member_id: StringName) -> PartyMemberState:
	if _character_gateway == null or member_id == &"" or not _character_gateway.has_method("get_member_state"):
		return null
	return _character_gateway.get_member_state(member_id) as PartyMemberState


func _get_custom_stat_value(member_state: PartyMemberState, stat_id: StringName) -> int:
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return 0
	return member_state.progression.unit_base_attributes.get_attribute_value(stat_id)


func _build_fortune_mark_attempt_flag_id(member_id: StringName) -> StringName:
	return ProgressionDataUtils.to_string_name("%s%s" % [FORTUNE_MARK_ATTEMPT_FLAG_PREFIX, String(member_id)])
