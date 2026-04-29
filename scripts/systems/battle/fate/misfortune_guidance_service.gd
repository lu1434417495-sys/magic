## 文件说明：该脚本属于 Misfortune guidance 成就订阅层，负责把战斗结算与工坊结果翻译成一次性 achievement 解锁。
## 审查重点：重点核对 seal/source 归属、battle -> forge 跨回调 flag，以及是否只消费现有 canonical 结果而不额外引入计数器。
## 备注：当前 runtime 只暴露 `boss_target`，因此 blessed 先按“被 `doom_sentence` 终结的 boss”作为章末 boss 的稳定代理条件。

class_name MisfortuneGuidanceService
extends RefCounted

const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const MISFORTUNE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/fate/misfortune_service.gd")
const ITEM_DEF_SCRIPT = preload("res://scripts/player/warehouse/item_def.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const BattleState = BATTLE_STATE_SCRIPT
const BattleResolutionResult = BATTLE_RESOLUTION_RESULT_SCRIPT
const BattleStatusEffectState = BATTLE_STATUS_EFFECT_STATE_SCRIPT
const BattleUnitState = BATTLE_UNIT_STATE_SCRIPT
const ItemDef = ITEM_DEF_SCRIPT
const PartyMemberState = PARTY_MEMBER_STATE_SCRIPT
const PartyState = PARTY_STATE_SCRIPT

const ACHIEVEMENT_GUIDANCE_TRUE: StringName = &"misfortune_guidance_true"
const ACHIEVEMENT_GUIDANCE_DEVOUT: StringName = &"misfortune_guidance_devout"
const ACHIEVEMENT_GUIDANCE_EXALTED: StringName = &"misfortune_guidance_exalted"
const ACHIEVEMENT_GUIDANCE_BLESSED: StringName = &"misfortune_guidance_blessed"

const DOOM_MARKED_STAT_ID: StringName = &"doom_marked"
const DOOM_AUTHORITY_STAT_ID: StringName = &"doom_authority"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"
const BOSS_TARGET_STAT_ID: StringName = &"boss_target"
const STATUS_BLACK_STAR_BRAND_ELITE: StringName = &"black_star_brand_elite"
const STATUS_CROWN_BREAK_BROKEN_FANG: StringName = &"crown_break_broken_fang"
const STATUS_CROWN_BREAK_BROKEN_HAND: StringName = &"crown_break_broken_hand"
const STATUS_CROWN_BREAK_BLINDED_EYE: StringName = &"crown_break_blinded_eye"
const STATUS_DOOM_SENTENCE_VERDICT: StringName = &"doom_sentence_verdict"
const CALAMITY_SHARD_ITEM_ID: StringName = &"calamity_shard"
const BLACK_CROWN_CORE_ITEM_ID: StringName = &"black_crown_core"
const EXALTED_READY_FLAG_PREFIX := "misfortune_guidance_exalted_ready:"
const CALAMITY_REASON_CRITICAL_FAIL: StringName = MISFORTUNE_SERVICE_SCRIPT.CALAMITY_REASON_CRITICAL_FAIL
const CALAMITY_REASON_STRONG_DEBUFF: StringName = MISFORTUNE_SERVICE_SCRIPT.CALAMITY_REASON_STRONG_DEBUFF

var _character_gateway: Object = null
var _battle_runtime_gateway: Object = null


func setup(character_gateway: Object = null, battle_runtime_gateway: Object = null) -> void:
	_character_gateway = character_gateway
	_battle_runtime_gateway = battle_runtime_gateway


func bind_battle_runtime_gateway(battle_runtime_gateway: Object = null) -> void:
	_battle_runtime_gateway = battle_runtime_gateway


func dispose() -> void:
	_character_gateway = null
	_battle_runtime_gateway = null


func handle_battle_resolution(
	battle_state: BattleState,
	battle_resolution_result: BattleResolutionResult
) -> Array[StringName]:
	var unlocked_ids: Array[StringName] = []
	var party_state := _get_party_state()
	if party_state == null or battle_state == null or battle_resolution_result == null:
		return unlocked_ids
	if battle_resolution_result.winner_faction_id != &"player":
		return unlocked_ids

	_mark_exalted_ready_flags(battle_resolution_result)

	for enemy_unit_id in battle_state.enemy_unit_ids:
		var defeated_unit := battle_state.units.get(enemy_unit_id) as BattleUnitState
		if defeated_unit == null or defeated_unit.is_alive:
			continue

		var sealed_member_id := _resolve_elite_seal_source_member_id(battle_state, defeated_unit)
		if sealed_member_id != &"":
			var sealed_member_state := _get_member_state(sealed_member_id)
			if _is_doom_marked(sealed_member_state) and _unlock_achievement(sealed_member_id, ACHIEVEMENT_GUIDANCE_TRUE):
				_append_unique_string_name(unlocked_ids, ACHIEVEMENT_GUIDANCE_TRUE)
			if _is_misfortune_devotee(sealed_member_state) \
			and _member_had_devout_adversity(sealed_member_id) \
			and _unlock_achievement(sealed_member_id, ACHIEVEMENT_GUIDANCE_DEVOUT):
				_append_unique_string_name(unlocked_ids, ACHIEVEMENT_GUIDANCE_DEVOUT)

		var verdict_member_id := _resolve_status_source_member_id(battle_state, defeated_unit, STATUS_DOOM_SENTENCE_VERDICT)
		if verdict_member_id == &"" or not _is_boss_target(defeated_unit):
			continue
		if _is_misfortune_devotee(_get_member_state(verdict_member_id)) \
		and _unlock_achievement(verdict_member_id, ACHIEVEMENT_GUIDANCE_BLESSED):
			_append_unique_string_name(unlocked_ids, ACHIEVEMENT_GUIDANCE_BLESSED)

	return unlocked_ids


func handle_forge_result(
	member_id: StringName,
	result: Dictionary,
	item_defs: Dictionary = {}
) -> Array[StringName]:
	var unlocked_ids: Array[StringName] = []
	var normalized_member_id := ProgressionDataUtils.to_string_name(member_id)
	if normalized_member_id == &"":
		return unlocked_ids
	if not bool(result.get("success", result.get("ok", false))):
		return unlocked_ids
	if not _has_exalted_ready_flag(normalized_member_id):
		return unlocked_ids

	var member_state := _get_member_state(normalized_member_id)
	if not _is_misfortune_devotee(member_state):
		return unlocked_ids
	if not _forge_result_uses_fixed_material(result):
		return unlocked_ids
	if not _forge_result_outputs_dark_equipment(result, item_defs):
		return unlocked_ids
	if _unlock_achievement(normalized_member_id, ACHIEVEMENT_GUIDANCE_EXALTED):
		_append_unique_string_name(unlocked_ids, ACHIEVEMENT_GUIDANCE_EXALTED)
	clear_exalted_ready_flags([normalized_member_id])
	return unlocked_ids


func clear_exalted_ready_flags(member_ids: Array[StringName] = []) -> void:
	var party_state := _get_party_state()
	if party_state == null:
		return
	if member_ids.is_empty():
		for flag_key in party_state.get_fate_run_flags().keys():
			var flag_id := ProgressionDataUtils.to_string_name(flag_key)
			if flag_id == &"" or not String(flag_id).begins_with(EXALTED_READY_FLAG_PREFIX):
				continue
			party_state.clear_fate_run_flag(flag_id)
		return
	for member_id in member_ids:
		party_state.clear_fate_run_flag(_build_exalted_ready_flag_id(member_id))


func _mark_exalted_ready_flags(battle_resolution_result: BattleResolutionResult) -> void:
	if battle_resolution_result == null:
		return
	var converted_shards := int(battle_resolution_result.party_resource_commit.get("converted_calamity_shards", 0))
	if converted_shards <= 0:
		return
	var party_state := _get_party_state()
	if party_state == null:
		return
	var calamity_by_member_id := _get_calamity_by_member_id()
	for member_key in calamity_by_member_id.keys():
		var member_id := ProgressionDataUtils.to_string_name(member_key)
		if member_id == &"":
			continue
		if maxi(int(calamity_by_member_id.get(member_key, 0)), 0) <= 0:
			continue
		party_state.set_fate_run_flag(_build_exalted_ready_flag_id(member_id), true)


func _member_had_devout_adversity(member_id: StringName) -> bool:
	return _has_misfortune_reason(member_id, CALAMITY_REASON_CRITICAL_FAIL) \
		or _has_misfortune_reason(member_id, CALAMITY_REASON_STRONG_DEBUFF)


func _has_misfortune_reason(member_id: StringName, reason_id: StringName) -> bool:
	if _battle_runtime_gateway == null or member_id == &"" or reason_id == &"":
		return false
	if not _battle_runtime_gateway.has_method("has_misfortune_reason"):
		return false
	return bool(_battle_runtime_gateway.has_misfortune_reason(member_id, reason_id))


func _get_calamity_by_member_id() -> Dictionary:
	if _battle_runtime_gateway == null or not _battle_runtime_gateway.has_method("get_calamity_by_member_id"):
		return {}
	var calamity_map = _battle_runtime_gateway.get_calamity_by_member_id()
	return calamity_map.duplicate(true) if calamity_map is Dictionary else {}


func _resolve_elite_seal_source_member_id(battle_state: BattleState, defeated_unit: BattleUnitState) -> StringName:
	if battle_state == null or defeated_unit == null or not _is_elite_or_boss_target(defeated_unit):
		return &""
	for status_id in [
		STATUS_CROWN_BREAK_BROKEN_FANG,
		STATUS_CROWN_BREAK_BROKEN_HAND,
		STATUS_CROWN_BREAK_BLINDED_EYE,
		STATUS_BLACK_STAR_BRAND_ELITE,
	]:
		var source_member_id := _resolve_status_source_member_id(battle_state, defeated_unit, status_id)
		if source_member_id != &"":
			return source_member_id
	return &""


func _resolve_status_source_member_id(
	battle_state: BattleState,
	target_unit: BattleUnitState,
	status_id: StringName
) -> StringName:
	if battle_state == null or target_unit == null or status_id == &"":
		return &""
	var effect_state := target_unit.get_status_effect(status_id) as BattleStatusEffectState
	if effect_state == null or effect_state.source_unit_id == &"":
		return &""
	var source_unit := battle_state.units.get(effect_state.source_unit_id) as BattleUnitState
	return ProgressionDataUtils.to_string_name(source_unit.source_member_id) if source_unit != null else &""


func _forge_result_uses_fixed_material(result: Dictionary) -> bool:
	var inventory_delta_variant: Variant = result.get("inventory_delta", {})
	if inventory_delta_variant is not Dictionary:
		return false
	for removed_entry_variant in (inventory_delta_variant as Dictionary).get("removed_entries", []):
		if removed_entry_variant is not Dictionary:
			continue
		var item_id := ProgressionDataUtils.to_string_name((removed_entry_variant as Dictionary).get("item_id", ""))
		if item_id == CALAMITY_SHARD_ITEM_ID or item_id == BLACK_CROWN_CORE_ITEM_ID:
			return true
	return false


func _forge_result_outputs_dark_equipment(result: Dictionary, item_defs: Dictionary) -> bool:
	var output_item_id := _resolve_forge_output_item_id(result)
	if output_item_id == &"":
		return false
	var item_def := _get_item_def(item_defs, output_item_id)
	if item_def == null or not item_def.is_equipment():
		return false
	for tag in item_def.get_tags():
		if tag == &"dark" or tag == &"misfortune" or tag == &"doom":
			return true
	for group in item_def.get_crafting_groups():
		if group == &"misfortune" or group == &"dark":
			return true
	return false


func _resolve_forge_output_item_id(result: Dictionary) -> StringName:
	var service_side_effects_variant: Variant = result.get("service_side_effects", {})
	if service_side_effects_variant is Dictionary:
		var output_from_side_effects := ProgressionDataUtils.to_string_name(
			(service_side_effects_variant as Dictionary).get("output_item_id", "")
		)
		if output_from_side_effects != &"":
			return output_from_side_effects
	var inventory_delta_variant: Variant = result.get("inventory_delta", {})
	if inventory_delta_variant is not Dictionary:
		return &""
	for added_entry_variant in (inventory_delta_variant as Dictionary).get("added_entries", []):
		if added_entry_variant is not Dictionary:
			continue
		var item_id := ProgressionDataUtils.to_string_name((added_entry_variant as Dictionary).get("item_id", ""))
		if item_id != &"":
			return item_id
	return &""


func _get_item_def(item_defs: Dictionary, item_id: StringName) -> ItemDef:
	if item_defs.is_empty() or item_id == &"":
		return null
	var direct_match = item_defs.get(item_id)
	if direct_match is ItemDef:
		return direct_match as ItemDef
	var string_match = item_defs.get(String(item_id))
	return string_match as ItemDef if string_match is ItemDef else null


func _unlock_achievement(member_id: StringName, achievement_id: StringName) -> bool:
	if _character_gateway == null or member_id == &"" or achievement_id == &"":
		return false
	if not _character_gateway.has_method("unlock_achievement"):
		return false
	return bool(_character_gateway.unlock_achievement(member_id, achievement_id, {
		"summary_text": _build_summary_text(achievement_id),
	}))


func _build_summary_text(achievement_id: StringName) -> String:
	match achievement_id:
		ACHIEVEMENT_GUIDANCE_TRUE:
			return "黑冕第一次确认这名角色能把厄运压成封印。"
		ACHIEVEMENT_GUIDANCE_DEVOUT:
			return "吃下坏事之后仍能封喉，才算真正懂得 Misfortune。"
		ACHIEVEMENT_GUIDANCE_EXALTED:
			return "这名角色开始把灾厄余烬锻成真正属于黑冕的装备。"
		ACHIEVEMENT_GUIDANCE_BLESSED:
			return "一次宣判击杀，让黑冕认定了最终的裁决资格。"
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


func _is_doom_marked(member_state: PartyMemberState) -> bool:
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return false
	return member_state.progression.unit_base_attributes.get_attribute_value(DOOM_MARKED_STAT_ID) > 0


func _is_misfortune_devotee(member_state: PartyMemberState) -> bool:
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return false
	return member_state.progression.unit_base_attributes.get_attribute_value(DOOM_AUTHORITY_STAT_ID) > 0


func _has_exalted_ready_flag(member_id: StringName) -> bool:
	var party_state := _get_party_state()
	if party_state == null or member_id == &"":
		return false
	return party_state.has_fate_run_flag(_build_exalted_ready_flag_id(member_id))


func _build_exalted_ready_flag_id(member_id: StringName) -> StringName:
	return ProgressionDataUtils.to_string_name("%s%s" % [EXALTED_READY_FLAG_PREFIX, String(member_id)])


func _is_elite_or_boss_target(unit_state: BattleUnitState) -> bool:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return false
	return int(unit_state.attribute_snapshot.get_value(BOSS_TARGET_STAT_ID)) > 0 \
		or int(unit_state.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 0


func _is_boss_target(unit_state: BattleUnitState) -> bool:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return false
	return int(unit_state.attribute_snapshot.get_value(BOSS_TARGET_STAT_ID)) > 0


func _append_unique_string_name(values: Array[StringName], value: StringName) -> void:
	if value != &"" and not values.has(value):
		values.append(value)
