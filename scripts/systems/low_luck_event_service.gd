## Owns the low-luck fixed event pool skeleton and per-run dedupe flags.
## Notes:
## - Rewards must stay on fixed paths: fixed loot entries or pending character rewards only.
## - This service never writes hidden_luck_at_birth, faith_luck_bonus, or drop-luck state.
## - Battle-local trigger candidates are cached here and only converted into rewards on battle resolution.

class_name LowLuckEventService
extends RefCounted

const BATTLE_FATE_EVENT_BUS_SCRIPT = preload("res://scripts/systems/battle_fate_event_bus.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle_state.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle_resolution_result.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle_unit_state.gd")
const LOW_LUCK_RELIC_RULES_SCRIPT = preload("res://scripts/systems/low_luck_relic_rules.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const BattleFateEventBus = BATTLE_FATE_EVENT_BUS_SCRIPT
const BattleState = BATTLE_STATE_SCRIPT
const BattleResolutionResult = BATTLE_RESOLUTION_RESULT_SCRIPT
const BattleUnitState = BATTLE_UNIT_STATE_SCRIPT
const PartyState = PARTY_STATE_SCRIPT
const PartyMemberState = PARTY_MEMBER_STATE_SCRIPT

const EVENT_BROKEN_BRIDGE_SURVIVAL: StringName = &"broken_bridge_survival"
const EVENT_LAMP_WITHOUT_WITNESS: StringName = &"lamp_without_witness"
const EVENT_BORROWED_ROAD: StringName = &"borrowed_road"
const EVENT_REVERSE_FATE_AMULET_REWARD: StringName = &"reverse_fate_amulet_reward"
const EVENT_BLACK_STAR_WEDGE_REWARD: StringName = &"black_star_wedge_reward"
const EVENT_BLOOD_DEBT_SHAWL_REWARD: StringName = &"blood_debt_shawl_reward"
const EVENT_DEAD_ROAD_LANTERN_REWARD: StringName = &"dead_road_lantern_reward"

const META_FLAG_PREFIX := "low_luck_event:"
const LOW_LUCK_THRESHOLD := -4
const SOURCE_TYPE_STORY_EVENT: StringName = &"story_event"
const SOURCE_KIND_LOW_LUCK_EVENT: StringName = &"low_luck_event"
const DROP_TYPE_ITEM: StringName = &"item"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"
const CALAMITY_SHARD_ITEM_ID: StringName = &"calamity_shard"
const KNOWLEDGE_LAMP_WITHOUT_WITNESS: StringName = &"low_luck_black_market_hint"
const KNOWLEDGE_BORROWED_ROAD: StringName = &"low_luck_borrowed_road"

const REST_FACILITY_KEYWORDS: Array[String] = [
	"inn",
	"shrine",
	"gambl",
	"旅店",
	"旅舍",
	"神龛",
	"赌坊",
]

var _character_gateway: Object = null
var _fate_event_bus: BattleFateEventBus = null
var _hardship_survival_by_battle_id: Dictionary = {}
var _critical_fail_by_battle_id: Dictionary = {}


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
	_hardship_survival_by_battle_id.clear()
	_critical_fail_by_battle_id.clear()


func handle_battle_resolution(
	battle_state: BattleState,
	battle_resolution_result: BattleResolutionResult
) -> Dictionary:
	var result := _new_result()
	var battle_id := _resolve_battle_id(battle_state, battle_resolution_result)
	if battle_id == &"":
		return result

	var player_won := battle_resolution_result != null and battle_resolution_result.winner_faction_id == &"player"
	if player_won:
		var hardship_members := _get_battle_member_ids(_hardship_survival_by_battle_id, battle_id)
		for member_id in hardship_members:
			if not _is_battle_member_alive(battle_state, member_id):
				continue
			if not _mark_meta_flag_if_first(_build_event_meta_flag_id(EVENT_BROKEN_BRIDGE_SURVIVAL, member_id)):
				continue
			_append_unique_string_name(result["triggered_event_ids"], EVENT_BROKEN_BRIDGE_SURVIVAL)
			(result["loot_entries"] as Array).append(
				_build_fixed_item_loot_entry(
					EVENT_BROKEN_BRIDGE_SURVIVAL,
					member_id,
					CALAMITY_SHARD_ITEM_ID,
					1,
					"断桥生还"
				)
			)

		var critical_fail_members := _get_battle_member_ids(_critical_fail_by_battle_id, battle_id)
		for member_id in critical_fail_members:
			if not _is_battle_member_alive(battle_state, member_id):
				continue
			if not _mark_meta_flag_if_first(_build_event_meta_flag_id(EVENT_BORROWED_ROAD, member_id)):
				continue
			var reward := _build_pending_reward(
				member_id,
				EVENT_BORROWED_ROAD,
				"死里借来的路",
				[{
					"entry_type": "knowledge_unlock",
					"target_id": String(KNOWLEDGE_BORROWED_ROAD),
					"target_label": "借来的路",
					"amount": 1,
					"reason_text": "这名角色学会了如何从坏运留下的裂缝里继续前进。",
				}],
				"一次大失败之后仍把整场战斗赢了下来。"
			)
			if reward.is_empty():
				_clear_meta_flag(_build_event_meta_flag_id(EVENT_BORROWED_ROAD, member_id))
				continue
			_append_unique_string_name(result["triggered_event_ids"], EVENT_BORROWED_ROAD)
			(result["pending_character_rewards"] as Array).append(reward)

		var battle_has_elite_or_boss := _battle_has_elite_or_boss_enemy(battle_state)
		if battle_has_elite_or_boss:
			var reverse_fate_member_id := _find_first_alive_member_id_in_battle(critical_fail_members, battle_state)
			if reverse_fate_member_id != &"" \
				and _mark_meta_flag_if_first(_build_event_meta_flag_id(EVENT_REVERSE_FATE_AMULET_REWARD)):
				_append_unique_string_name(result["triggered_event_ids"], EVENT_REVERSE_FATE_AMULET_REWARD)
				(result["loot_entries"] as Array).append(
					_build_fixed_item_loot_entry(
						EVENT_REVERSE_FATE_AMULET_REWARD,
						reverse_fate_member_id,
						LOW_LUCK_RELIC_RULES_SCRIPT.ITEM_REVERSE_FATE_AMULET,
						1,
						"逆命护符"
					)
				)
			var black_star_member_id := _find_first_alive_member_id_in_battle(hardship_members, battle_state)
			if black_star_member_id != &"" \
				and _mark_meta_flag_if_first(_build_event_meta_flag_id(EVENT_BLACK_STAR_WEDGE_REWARD)):
				_append_unique_string_name(result["triggered_event_ids"], EVENT_BLACK_STAR_WEDGE_REWARD)
				(result["loot_entries"] as Array).append(
					_build_fixed_item_loot_entry(
						EVENT_BLACK_STAR_WEDGE_REWARD,
						black_star_member_id,
						LOW_LUCK_RELIC_RULES_SCRIPT.ITEM_BLACK_STAR_WEDGE,
						1,
						"黑星楔钉"
					)
				)

		var blood_debt_member_id := _find_first_blood_debt_candidate_id(battle_state)
		if blood_debt_member_id != &"" \
			and _mark_meta_flag_if_first(_build_event_meta_flag_id(EVENT_BLOOD_DEBT_SHAWL_REWARD)):
			_append_unique_string_name(result["triggered_event_ids"], EVENT_BLOOD_DEBT_SHAWL_REWARD)
			(result["loot_entries"] as Array).append(
				_build_fixed_item_loot_entry(
					EVENT_BLOOD_DEBT_SHAWL_REWARD,
					blood_debt_member_id,
					LOW_LUCK_RELIC_RULES_SCRIPT.ITEM_BLOOD_DEBT_SHAWL,
					1,
					"血债披肩"
				)
			)

		var lantern_member_id := _find_first_alive_member_id_in_battle(
			_intersect_member_ids(hardship_members, critical_fail_members),
			battle_state
		)
		if lantern_member_id != &"" \
			and _mark_meta_flag_if_first(_build_event_meta_flag_id(EVENT_DEAD_ROAD_LANTERN_REWARD)):
			_append_unique_string_name(result["triggered_event_ids"], EVENT_DEAD_ROAD_LANTERN_REWARD)
			(result["loot_entries"] as Array).append(
				_build_fixed_item_loot_entry(
					EVENT_DEAD_ROAD_LANTERN_REWARD,
					lantern_member_id,
					LOW_LUCK_RELIC_RULES_SCRIPT.ITEM_DEAD_ROAD_LANTERN,
					1,
					"亡途灯笼"
				)
			)
	_clear_battle_tracking(battle_id)
	return result


func handle_settlement_action(context: Dictionary) -> Dictionary:
	var result := _new_result()
	if not _is_lamp_without_witness_context(context):
		return result
	if not _mark_meta_flag_if_first(_build_event_meta_flag_id(EVENT_LAMP_WITHOUT_WITNESS)):
		return result

	var party_state := _get_party_state()
	var member_id := _find_first_low_luck_member_id(party_state)
	if member_id == &"":
		_clear_meta_flag(_build_event_meta_flag_id(EVENT_LAMP_WITHOUT_WITNESS))
		return result

	var reward := _build_pending_reward(
		member_id,
		EVENT_LAMP_WITHOUT_WITNESS,
		"灯下无人",
		[{
			"entry_type": "knowledge_unlock",
			"target_id": String(KNOWLEDGE_LAMP_WITHOUT_WITNESS),
			"target_label": "黑市知识",
			"amount": 1,
			"reason_text": "灯下空出来的位置，让这名角色先一步看懂了黑市留下的暗号。",
		}],
		"神龛 / 旅舍 / 赌坊的休整没有带来安慰，却留下了固定线索。"
	)
	if reward.is_empty():
		_clear_meta_flag(_build_event_meta_flag_id(EVENT_LAMP_WITHOUT_WITNESS))
		return result

	_append_unique_string_name(result["triggered_event_ids"], EVENT_LAMP_WITHOUT_WITNESS)
	(result["pending_character_rewards"] as Array).append(reward)
	return result


func _on_fate_event(event_type: StringName, payload: Dictionary) -> void:
	match event_type:
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_HARDSHIP_SURVIVAL:
			_track_hardship_survival(payload)
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_FAIL:
			_track_critical_fail(payload)
		_:
			return


func _track_hardship_survival(payload: Dictionary) -> void:
	var battle_id := ProgressionDataUtils.to_string_name(payload.get("battle_id", ""))
	var member_id := ProgressionDataUtils.to_string_name(payload.get("attacker_member_id", ""))
	if battle_id == &"" or member_id == &"":
		return
	if not bool(payload.get("attacker_low_hp_hardship", false)):
		return
	var strong_debuff_ids := ProgressionDataUtils.to_string_name_array(payload.get("attacker_strong_attack_debuff_ids", []))
	if strong_debuff_ids.is_empty():
		return
	if not _is_low_luck_member_payload(member_id, payload):
		return
	_mark_battle_member(_hardship_survival_by_battle_id, battle_id, member_id)


func _track_critical_fail(payload: Dictionary) -> void:
	var battle_id := ProgressionDataUtils.to_string_name(payload.get("battle_id", ""))
	var member_id := ProgressionDataUtils.to_string_name(payload.get("attacker_member_id", ""))
	if battle_id == &"" or member_id == &"":
		return
	if not _is_low_luck_member_payload(member_id, payload):
		return
	_mark_battle_member(_critical_fail_by_battle_id, battle_id, member_id)


func _is_low_luck_member_payload(member_id: StringName, payload: Dictionary) -> bool:
	var luck_snapshot_variant: Variant = payload.get("luck_snapshot", {})
	if luck_snapshot_variant is Dictionary:
		var hidden_luck := int((luck_snapshot_variant as Dictionary).get("hidden_luck_at_birth", 0))
		if hidden_luck <= LOW_LUCK_THRESHOLD:
			return true
	var member_state := _get_member_state(member_id)
	return member_state != null and member_state.get_hidden_luck_at_birth() <= LOW_LUCK_THRESHOLD


func _is_lamp_without_witness_context(context: Dictionary) -> bool:
	var action_id := String(context.get("action_id", "")).to_lower()
	var interaction_script_id := String(context.get("interaction_script_id", "")).to_lower()
	var facility_id := String(context.get("facility_id", "")).to_lower()
	var facility_name := String(context.get("facility_name", ""))
	var service_type := String(context.get("service_type", ""))
	if interaction_script_id == "service_rest_basic" or interaction_script_id == "service_rest_full":
		return true
	if action_id.contains("rest") or action_id.contains("gambl") or action_id.contains("shrine"):
		return true
	if facility_id.contains("inn") or facility_id.contains("shrine") or facility_id.contains("gambl"):
		return true
	var haystack := ("%s %s" % [facility_name, service_type]).to_lower()
	for keyword in REST_FACILITY_KEYWORDS:
		if haystack.contains(keyword):
			return true
	return false


func _build_pending_reward(
	member_id: StringName,
	event_id: StringName,
	source_label: String,
	entry_variants: Array,
	summary_text: String
) -> Dictionary:
	if _character_gateway == null or member_id == &"":
		return {}
	if not _character_gateway.has_method("build_pending_character_reward"):
		return {}
	var reward = _character_gateway.build_pending_character_reward(
		member_id,
		_build_reward_id(event_id, member_id),
		SOURCE_TYPE_STORY_EVENT,
		event_id,
		source_label,
		entry_variants,
		summary_text
	)
	if reward == null or not reward.has_method("to_dict"):
		return {}
	return reward.to_dict()


func _build_fixed_item_loot_entry(
	event_id: StringName,
	member_id: StringName,
	item_id: StringName,
	quantity: int,
	source_label: String
) -> Dictionary:
	return {
		"drop_type": String(DROP_TYPE_ITEM),
		"drop_source_kind": String(SOURCE_KIND_LOW_LUCK_EVENT),
		"drop_source_id": String(event_id),
		"drop_source_label": source_label,
		"drop_entry_id": "%s:%s" % [String(event_id), String(member_id)],
		"item_id": String(item_id),
		"quantity": maxi(quantity, 0),
	}


func _resolve_battle_id(
	battle_state: BattleState,
	battle_resolution_result: BattleResolutionResult
) -> StringName:
	if battle_resolution_result != null and battle_resolution_result.battle_id != &"":
		return battle_resolution_result.battle_id
	return battle_state.battle_id if battle_state != null else &""


func _mark_battle_member(store: Dictionary, battle_id: StringName, member_id: StringName) -> void:
	if battle_id == &"" or member_id == &"":
		return
	if not store.has(battle_id):
		store[battle_id] = {}
	var battle_members_variant: Variant = store.get(battle_id, {})
	if battle_members_variant is not Dictionary:
		return
	(battle_members_variant as Dictionary)[member_id] = true


func _get_battle_member_ids(store: Dictionary, battle_id: StringName) -> Array[StringName]:
	var member_ids: Array[StringName] = []
	if battle_id == &"":
		return member_ids
	var battle_members_variant: Variant = store.get(battle_id, {})
	if battle_members_variant is not Dictionary:
		return member_ids
	for member_key in ProgressionDataUtils.sorted_string_keys(battle_members_variant):
		var member_id := StringName(member_key)
		if bool((battle_members_variant as Dictionary).get(member_id, (battle_members_variant as Dictionary).get(member_key, false))):
			member_ids.append(member_id)
	return member_ids


func _clear_battle_tracking(battle_id: StringName) -> void:
	if battle_id == &"":
		return
	_hardship_survival_by_battle_id.erase(battle_id)
	_critical_fail_by_battle_id.erase(battle_id)


func _find_first_low_luck_member_id(party_state: PartyState) -> StringName:
	if party_state == null:
		return &""
	for member_id in _build_ordered_member_ids(party_state):
		var member_state := party_state.get_member_state(member_id) as PartyMemberState
		if member_state == null or member_state.is_dead:
			continue
		if member_state.get_hidden_luck_at_birth() <= LOW_LUCK_THRESHOLD:
			return member_id
	return &""


func _build_ordered_member_ids(party_state: PartyState) -> Array[StringName]:
	var ordered_member_ids: Array[StringName] = []
	if party_state == null:
		return ordered_member_ids
	_append_unique_member_ids(ordered_member_ids, ProgressionDataUtils.to_string_name_array(party_state.active_member_ids))
	_append_unique_member_ids(ordered_member_ids, ProgressionDataUtils.to_string_name_array(party_state.reserve_member_ids))
	for member_key in ProgressionDataUtils.sorted_string_keys(party_state.member_states):
		_append_unique_member_id(ordered_member_ids, StringName(member_key))
	return ordered_member_ids


func _append_unique_member_ids(target: Array[StringName], values: Array[StringName]) -> void:
	for value in values:
		_append_unique_member_id(target, value)


func _append_unique_member_id(target: Array[StringName], value: StringName) -> void:
	if value == &"" or target.has(value):
		return
	target.append(value)


func _is_battle_member_alive(battle_state: BattleState, member_id: StringName) -> bool:
	if battle_state == null or member_id == &"":
		return false
	for unit_variant in battle_state.units.values():
		var unit_state = unit_variant as BattleUnitState
		if unit_state == null:
			continue
		var source_member_id := ProgressionDataUtils.to_string_name(unit_state.source_member_id)
		if source_member_id != member_id:
			continue
		return bool(unit_state.is_alive)
	var member_state := _get_member_state(member_id)
	return member_state != null and not member_state.is_dead


func _find_first_alive_member_id_in_battle(member_ids: Array[StringName], battle_state: BattleState) -> StringName:
	for member_id in member_ids:
		if _is_battle_member_alive(battle_state, member_id):
			return member_id
	return &""


func _intersect_member_ids(first: Array[StringName], second: Array[StringName]) -> Array[StringName]:
	var intersected: Array[StringName] = []
	for member_id in first:
		if member_id == &"" or intersected.has(member_id) or not second.has(member_id):
			continue
		intersected.append(member_id)
	return intersected


func _battle_has_elite_or_boss_enemy(battle_state: BattleState) -> bool:
	if battle_state == null:
		return false
	var target_unit_ids: Array[StringName] = battle_state.enemy_unit_ids.duplicate()
	if target_unit_ids.is_empty():
		for unit_variant in battle_state.units.values():
			var unit_state := unit_variant as BattleUnitState
			if unit_state == null or unit_state.faction_id == &"player":
				continue
			target_unit_ids.append(unit_state.unit_id)
	for unit_id in target_unit_ids:
		var unit_state := battle_state.units.get(unit_id) as BattleUnitState
		if unit_state == null or unit_state.attribute_snapshot == null:
			continue
		if int(unit_state.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 0:
			return true
	return false


func _find_first_blood_debt_candidate_id(battle_state: BattleState) -> StringName:
	var party_state := _get_party_state()
	if battle_state == null or party_state == null:
		return &""
	for member_id in _build_ordered_member_ids(party_state):
		var member_state := party_state.get_member_state(member_id) as PartyMemberState
		if member_state == null or member_state.get_hidden_luck_at_birth() > LOW_LUCK_THRESHOLD:
			continue
		if not _is_battle_member_alive(battle_state, member_id):
			continue
		if _battle_has_fallen_player_ally(battle_state, member_id):
			return member_id
	return &""


func _battle_has_fallen_player_ally(battle_state: BattleState, surviving_member_id: StringName) -> bool:
	if battle_state == null:
		return false
	for unit_variant in battle_state.units.values():
		var unit_state := unit_variant as BattleUnitState
		if unit_state == null or unit_state.faction_id != &"player":
			continue
		var member_id := ProgressionDataUtils.to_string_name(unit_state.source_member_id)
		if member_id == &"" or member_id == surviving_member_id:
			continue
		if not bool(unit_state.is_alive):
			return true
	return false


func _get_party_state() -> PartyState:
	if _character_gateway == null or not _character_gateway.has_method("get_party_state"):
		return null
	return _character_gateway.get_party_state() as PartyState


func _get_member_state(member_id: StringName) -> PartyMemberState:
	if _character_gateway == null or member_id == &"" or not _character_gateway.has_method("get_member_state"):
		return null
	return _character_gateway.get_member_state(member_id) as PartyMemberState


func _mark_meta_flag_if_first(flag_id: StringName) -> bool:
	var party_state := _get_party_state()
	if party_state == null or flag_id == &"":
		return false
	if party_state.has_meta_flag(flag_id):
		return false
	party_state.set_meta_flag(flag_id, true)
	return true


func _clear_meta_flag(flag_id: StringName) -> void:
	var party_state := _get_party_state()
	if party_state == null or flag_id == &"":
		return
	party_state.clear_meta_flag(flag_id)


func _build_event_meta_flag_id(event_id: StringName, member_id: StringName = &"") -> StringName:
	if event_id == &"":
		return &""
	if member_id == &"":
		return ProgressionDataUtils.to_string_name("%s%s" % [META_FLAG_PREFIX, String(event_id)])
	return ProgressionDataUtils.to_string_name("%s%s:%s" % [META_FLAG_PREFIX, String(event_id), String(member_id)])


func _build_reward_id(event_id: StringName, member_id: StringName) -> StringName:
	return ProgressionDataUtils.to_string_name("%sreward:%s:%s" % [META_FLAG_PREFIX, String(event_id), String(member_id)])


func _new_result() -> Dictionary:
	return {
		"triggered_event_ids": [],
		"loot_entries": [],
		"pending_character_rewards": [],
	}


func _append_unique_string_name(values_variant, value: StringName) -> void:
	if value == &"" or values_variant is not Array:
		return
	var values: Array = values_variant as Array
	if values.has(value):
		return
	values.append(value)
