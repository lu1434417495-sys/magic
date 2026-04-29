## 文件说明：该脚本属于 Fortuna guidance 成就订阅层，负责把命运事件与章节完成回调翻译成一次性 achievement 解锁。
## 审查重点：重点核对事件只读 payload 的消费边界、一次性 achievement 写入时机，以及 battle/chapter 临时 flag 的清理。
## 备注：该服务必须先于 FortuneService 绑定到同一条 fate bus，才能保证第一次 fortune_mark 事件不会误解锁 guidance_true。

class_name FortunaGuidanceService
extends RefCounted

const BATTLE_FATE_EVENT_BUS_SCRIPT = preload("res://scripts/systems/battle/fate/battle_fate_event_bus.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const BattleFateEventBus = preload("res://scripts/systems/battle/fate/battle_fate_event_bus.gd")
const BattleState = BATTLE_STATE_SCRIPT
const BattleResolutionResult = BATTLE_RESOLUTION_RESULT_SCRIPT
const BattleUnitState = BATTLE_UNIT_STATE_SCRIPT
const PartyState = PARTY_STATE_SCRIPT
const PartyMemberState = PARTY_MEMBER_STATE_SCRIPT

const ACHIEVEMENT_GUIDANCE_TRUE: StringName = &"fortuna_guidance_true"
const ACHIEVEMENT_GUIDANCE_DEVOUT: StringName = &"fortuna_guidance_devout"
const ACHIEVEMENT_GUIDANCE_EXALTED: StringName = &"fortuna_guidance_exalted"
const ACHIEVEMENT_GUIDANCE_BLESSED: StringName = &"fortuna_guidance_blessed"
const FORTUNE_MARKED_STAT_ID: StringName = &"fortune_marked"
const CHAPTER_EVENT_FLAG_PREFIX := "fortuna_guidance_chapter_seen:"
const DEVOUT_BATTLE_FLAG_PREFIX := "fortuna_guidance_devout_battle:"

var _character_gateway: Object = null
var _fate_event_bus: BattleFateEventBus = null


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


func handle_battle_resolution(
	battle_state: BattleState,
	battle_resolution_result: BattleResolutionResult
) -> Array[StringName]:
	var unlocked_ids: Array[StringName] = []
	var party_state := _get_party_state()
	if party_state == null or battle_state == null or battle_resolution_result == null:
		return unlocked_ids

	var battle_id := battle_resolution_result.battle_id
	if battle_id == &"":
		battle_id = battle_state.battle_id
	if battle_id == &"":
		return unlocked_ids

	var player_won := battle_resolution_result.winner_faction_id == &"player"
	for ally_unit_id in battle_state.ally_unit_ids:
		var unit_state := battle_state.units.get(ally_unit_id) as BattleUnitState
		if unit_state == null or unit_state.source_member_id == &"":
			continue
		var flag_id := _build_devout_battle_flag_id(battle_id, unit_state.source_member_id)
		if not party_state.has_fate_run_flag(flag_id):
			continue
		if player_won and unit_state.is_alive and _unlock_achievement(unit_state.source_member_id, ACHIEVEMENT_GUIDANCE_DEVOUT):
			_append_unique_string_name(unlocked_ids, ACHIEVEMENT_GUIDANCE_DEVOUT)
		party_state.clear_fate_run_flag(flag_id)

	return unlocked_ids


func handle_chapter_completed(payload: Dictionary) -> Array[StringName]:
	var unlocked_ids: Array[StringName] = []
	var party_state := _get_party_state()
	if party_state == null:
		return unlocked_ids

	var member_ids := _resolve_chapter_member_ids(payload, party_state)
	if member_ids.is_empty():
		return unlocked_ids

	var had_permanent_death := bool(payload.get(
		"had_permanent_death",
		payload.get("has_permanent_death", false)
	))
	for member_id in member_ids:
		var flag_id := _build_chapter_event_flag_id(member_id)
		var should_unlock := not had_permanent_death \
			and party_state.has_fate_run_flag(flag_id) \
			and _is_fortuna_devotee(_get_member_state(member_id))
		if should_unlock and _unlock_achievement(member_id, ACHIEVEMENT_GUIDANCE_BLESSED):
			_append_unique_string_name(unlocked_ids, ACHIEVEMENT_GUIDANCE_BLESSED)
		party_state.clear_fate_run_flag(flag_id)

	return unlocked_ids


func _on_fate_event(event_type: StringName, payload: Dictionary) -> void:
	match event_type:
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE:
			_handle_critical_success_under_disadvantage(payload)
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_HIGH_THREAT_CRITICAL_HIT:
			_handle_high_threat_critical_hit(payload)
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_HARDSHIP_SURVIVAL:
			_handle_hardship_survival(payload)
		_:
			return


func _handle_critical_success_under_disadvantage(payload: Dictionary) -> void:
	if not bool(payload.get("defender_is_elite_or_boss", false)):
		return
	var member_id := _resolve_attacker_member_id(payload)
	if member_id == &"":
		return
	_mark_chapter_event_seen(member_id)
	if _is_fortuna_marked(_get_member_state(member_id)):
		_unlock_achievement(member_id, ACHIEVEMENT_GUIDANCE_TRUE)


func _handle_high_threat_critical_hit(payload: Dictionary) -> void:
	if not bool(payload.get("defender_is_elite_or_boss", false)):
		return
	var member_id := _resolve_attacker_member_id(payload)
	if member_id == &"":
		return
	var member_state := _get_member_state(member_id)
	if not _is_fortuna_devotee(member_state):
		return
	_mark_chapter_event_seen(member_id)
	_unlock_achievement(member_id, ACHIEVEMENT_GUIDANCE_EXALTED)


func _handle_hardship_survival(payload: Dictionary) -> void:
	var battle_id := ProgressionDataUtils.to_string_name(payload.get("battle_id", ""))
	var member_id := _resolve_attacker_member_id(payload)
	if battle_id == &"" or member_id == &"":
		return
	var member_state := _get_member_state(member_id)
	if not _is_fortuna_devotee(member_state):
		return
	if not bool(payload.get("attacker_low_hp_hardship", false)):
		return
	var strong_debuff_ids := ProgressionDataUtils.to_string_name_array(payload.get("attacker_strong_attack_debuff_ids", []))
	if strong_debuff_ids.is_empty():
		return
	_mark_chapter_event_seen(member_id)
	var party_state := _get_party_state()
	if party_state == null:
		return
	party_state.set_fate_run_flag(_build_devout_battle_flag_id(battle_id, member_id), true)


func _resolve_attacker_member_id(payload: Dictionary) -> StringName:
	return ProgressionDataUtils.to_string_name(payload.get("attacker_member_id", ""))


func _resolve_chapter_member_ids(payload: Dictionary, party_state: PartyState) -> Array[StringName]:
	var explicit_member_ids := ProgressionDataUtils.to_string_name_array(payload.get("member_ids", []))
	if not explicit_member_ids.is_empty():
		return explicit_member_ids
	var member_ids: Array[StringName] = []
	for member_key in ProgressionDataUtils.sorted_string_keys(party_state.member_states):
		member_ids.append(StringName(member_key))
	return member_ids


func _mark_chapter_event_seen(member_id: StringName) -> void:
	var party_state := _get_party_state()
	if party_state == null or member_id == &"":
		return
	party_state.set_fate_run_flag(_build_chapter_event_flag_id(member_id), true)


func _unlock_achievement(member_id: StringName, achievement_id: StringName) -> bool:
	if _character_gateway == null or member_id == &"" or achievement_id == &"":
		return false
	if not _character_gateway.has_method("unlock_achievement"):
		return false
	return bool(_character_gateway.unlock_achievement(member_id, achievement_id, {
		"summary_text": _build_summary_text(achievement_id),
	}))


func _is_fortuna_marked(member_state: PartyMemberState) -> bool:
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return false
	return member_state.progression.unit_base_attributes.get_attribute_value(FORTUNE_MARKED_STAT_ID) > 0


func _is_fortuna_devotee(member_state: PartyMemberState) -> bool:
	return member_state != null and member_state.get_faith_luck_bonus() > 0


func _build_summary_text(achievement_id: StringName) -> String:
	match achievement_id:
		ACHIEVEMENT_GUIDANCE_TRUE:
			return "Fortuna 再次看见了这名角色。"
		ACHIEVEMENT_GUIDANCE_DEVOUT:
			return "逆境中的胜利让 Fortuna 的怜悯有了回应。"
		ACHIEVEMENT_GUIDANCE_EXALTED:
			return "好运不再只是门骰，而是被抬进了真正的高位威胁区间。"
		ACHIEVEMENT_GUIDANCE_BLESSED:
			return "整章旅程都被 Fortuna 的影子护住了。"
		_:
			return ""


func _get_party_state() -> PartyState:
	if _character_gateway == null or not _character_gateway.has_method("get_party_state"):
		return null
	return _character_gateway.get_party_state() as PartyState


func _get_member_state(member_id: StringName) -> PartyMemberState:
	if _character_gateway == null or member_id == &"" or not _character_gateway.has_method("get_member_state"):
		return null
	return _character_gateway.get_member_state(member_id) as PartyMemberState


func _build_chapter_event_flag_id(member_id: StringName) -> StringName:
	return ProgressionDataUtils.to_string_name("%s%s" % [CHAPTER_EVENT_FLAG_PREFIX, String(member_id)])


func _build_devout_battle_flag_id(battle_id: StringName, member_id: StringName) -> StringName:
	return ProgressionDataUtils.to_string_name(
		"%s%s:%s" % [DEVOUT_BATTLE_FLAG_PREFIX, String(battle_id), String(member_id)]
	)


func _append_unique_string_name(values: Array[StringName], value: StringName) -> void:
	if value != &"" and not values.has(value):
		values.append(value)
