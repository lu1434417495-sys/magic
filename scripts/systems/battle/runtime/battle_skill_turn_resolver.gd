class_name BattleRuntimeSkillTurnResolver
extends RefCounted

const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BattleStatusSemanticTable = preload("res://scripts/systems/battle/rules/battle_status_semantic_table.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BATTLE_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_range_service.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const STATUS_PINNED: StringName = &"pinned"
const STATUS_ROOTED: StringName = &"rooted"
const STATUS_TENDON_CUT: StringName = &"tendon_cut"
const STATUS_STAGGERED: StringName = &"staggered"
const STATUS_GUARDING: StringName = &"guarding"
const STATUS_BLACK_STAR_BRAND_NORMAL: StringName = &"black_star_brand_normal"
const STATUS_CROWN_BREAK_BROKEN_HAND: StringName = &"crown_break_broken_hand"
const BLACK_CONTRACT_PUSH_SKILL_ID: StringName = &"black_contract_push"
const BLACK_CROWN_SEAL_SKILL_ID: StringName = &"black_crown_seal"
const BLACK_STAR_BRAND_SKILL_ID: StringName = &"black_star_brand"
const CROWN_BREAK_SKILL_ID: StringName = &"crown_break"
const DOOM_SENTENCE_SKILL_ID: StringName = &"doom_sentence"
const BLACK_CONTRACT_PUSH_VARIANT_BLOOD: StringName = &"blood_tithe"
const BLACK_CONTRACT_PUSH_VARIANT_GUARD: StringName = &"guard_tithe"
const BLACK_CONTRACT_PUSH_VARIANT_ACTION: StringName = &"action_tithe"
const DOOM_SHIFT_SELF_DEBUFF_DURATION_TU := 60
const BLACK_CONTRACT_PUSH_HP_COST := 10
const DEBUFF_STATUS_IDS := {
	&"armor_break": true,
	&"black_star_brand_elite": true,
	&"black_star_brand_normal": true,
	&"burning": true,
	&"crown_break_blinded_eye": true,
	&"crown_break_broken_fang": true,
	&"crown_break_broken_hand": true,
	&"frozen": true,
	&"hex_of_frailty": true,
	&"marked": true,
	&"pinned": true,
	&"rooted": true,
	&"shocked": true,
	&"slow": true,
	&"staggered": true,
	&"taunted": true,
	&"tendon_cut": true,
}
const TU_GRANULARITY := 5

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


func get_skill_cast_block_reason(active_unit: BattleUnitState, skill_def: SkillDef) -> String:
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return "技能或目标无效。"
	var combat_profile = skill_def.combat_profile
	var costs := get_effective_skill_costs(active_unit, skill_def)
	var cooldown := int(active_unit.cooldowns.get(skill_def.skill_id, 0))
	if cooldown > 0:
		return "%s 仍在冷却中（%d）。" % [skill_def.display_name, cooldown]
	if active_unit.current_ap < int(costs.get("ap_cost", combat_profile.ap_cost)):
		return "AP不足，无法施放该技能。"
	if active_unit.current_mp < int(costs.get("mp_cost", combat_profile.mp_cost)):
		return "法力不足，无法施放该技能。"
	if active_unit.current_stamina < int(costs.get("stamina_cost", combat_profile.stamina_cost)):
		return "体力不足，无法施放该技能。"
	if active_unit.current_aura < int(costs.get("aura_cost", combat_profile.aura_cost)):
		return "斗气不足，无法施放该技能。"
	if not combat_profile.required_weapon_families.is_empty() \
		and not unit_matches_required_weapon_families(active_unit, combat_profile.required_weapon_families):
		return "需要装备指定武器家族，无法施放该技能。"
	if requires_melee_weapon(skill_def) and not unit_has_melee_weapon(active_unit):
		return "需要装备有效武器，无法施放该技能。"
	if combat_profile.excluded_weapon_families.size() > 0 and active_unit.weapon_family in combat_profile.excluded_weapon_families:
		return "当前武器类型无法施放该技能。"
	if combat_profile.excluded_weapon_type_ids.size() > 0 and active_unit.weapon_profile_type_id in combat_profile.excluded_weapon_type_ids:
		return "当前武器类型无法施放该技能。"
	if is_main_skill_locked_by_status(active_unit, skill_def):
		return "厄命宣判压制了主技能，无法施放该技能。"
	if _is_black_star_brand_skill(skill_def.skill_id):
		if _runtime._misfortune_service == null or not _runtime._misfortune_service.can_cast_black_star_brand(active_unit):
			return "calamity 不足，无法施放黑星烙印。"
	if _is_crown_break_skill(skill_def.skill_id):
		if _runtime._misfortune_service == null or not _runtime._misfortune_service.can_cast_crown_break(active_unit):
			return "calamity 不足，无法施放折冠。"
	if _is_doom_sentence_skill(skill_def.skill_id):
		if _runtime._misfortune_service == null:
			return "厄命宣判的 calamity sidecar 未初始化。"
		var doom_sentence_block_reason: String = _runtime._misfortune_service.get_doom_sentence_cast_block_reason(active_unit)
		if not doom_sentence_block_reason.is_empty():
			return doom_sentence_block_reason
	if _is_black_crown_seal_skill(skill_def.skill_id):
		if _runtime._misfortune_service == null:
			return "黑冠封印的 battle sidecar 未初始化。"
		var black_crown_seal_block_reason: String = _runtime._misfortune_service.get_black_crown_seal_cast_block_reason(active_unit)
		if not black_crown_seal_block_reason.is_empty():
			return black_crown_seal_block_reason
	if has_status(active_unit, STATUS_BLACK_STAR_BRAND_NORMAL) and _runtime._skill_grants_guarding(skill_def):
		return "黑星烙印封锁了格挡，无法施放该技能。"
	return ""


func unit_has_melee_weapon(active_unit: BattleUnitState) -> bool:
	return BATTLE_RANGE_SERVICE_SCRIPT.unit_has_melee_weapon(active_unit)


func unit_matches_required_weapon_families(active_unit: BattleUnitState, required_weapon_families: Array) -> bool:
	return BATTLE_RANGE_SERVICE_SCRIPT.unit_matches_required_weapon_families(active_unit, required_weapon_families)


func requires_melee_weapon(skill_def: SkillDef) -> bool:
	return BATTLE_RANGE_SERVICE_SCRIPT.requires_current_melee_weapon(skill_def)


func effect_uses_weapon_physical_damage_tag(effect_def: CombatEffectDef) -> bool:
	return BATTLE_RANGE_SERVICE_SCRIPT.effect_uses_weapon_physical_damage_tag(effect_def)


func get_skill_command_block_reason(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef
) -> String:
	var block_reason := get_skill_cast_block_reason(active_unit, skill_def)
	if not block_reason.is_empty():
		return block_reason
	if _is_black_contract_push_skill(skill_def.skill_id):
		return get_black_contract_push_variant_block_reason(active_unit, cast_variant)
	return ""


func consume_skill_costs(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null,
	batch: BattleEventBatch = null
) -> bool:
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return false
	if _is_black_contract_push_skill(skill_def.skill_id):
		if not consume_black_contract_push_cast(active_unit, cast_variant, batch):
			return false
	if _is_black_star_brand_skill(skill_def.skill_id):
		if _runtime._misfortune_service == null:
			if batch != null:
				batch.log_lines.append("黑星烙印的 calamity sidecar 未初始化。")
			return false
		var consume_result: Dictionary = _runtime._misfortune_service.consume_black_star_brand_cast(active_unit)
		if not bool(consume_result.get("ok", false)):
			if batch != null:
				batch.log_lines.append(String(consume_result.get("message", "calamity 不足，无法施放黑星烙印。")))
			return false
	if _is_crown_break_skill(skill_def.skill_id):
		if _runtime._misfortune_service == null:
			if batch != null:
				batch.log_lines.append("折冠的 calamity sidecar 未初始化。")
			return false
		var crown_break_consume_result: Dictionary = _runtime._misfortune_service.consume_crown_break_cast(active_unit)
		if not bool(crown_break_consume_result.get("ok", false)):
			if batch != null:
				batch.log_lines.append(String(crown_break_consume_result.get("message", "calamity 不足，无法施放折冠。")))
			return false
	if _is_doom_sentence_skill(skill_def.skill_id):
		if _runtime._misfortune_service == null:
			if batch != null:
				batch.log_lines.append("厄命宣判的 calamity sidecar 未初始化。")
			return false
		var doom_sentence_consume_result: Dictionary = _runtime._misfortune_service.consume_doom_sentence_cast(active_unit)
		if not bool(doom_sentence_consume_result.get("ok", false)):
			if batch != null:
				batch.log_lines.append(String(doom_sentence_consume_result.get("message", "calamity 不足，无法施放厄命宣判。")))
			return false
	if _is_black_crown_seal_skill(skill_def.skill_id):
		if _runtime._misfortune_service == null:
			if batch != null:
				batch.log_lines.append("黑冠封印的 battle sidecar 未初始化。")
			return false
		var black_crown_seal_consume_result: Dictionary = _runtime._misfortune_service.consume_black_crown_seal_cast(active_unit)
		if not bool(black_crown_seal_consume_result.get("ok", false)):
			if batch != null:
				batch.log_lines.append(String(black_crown_seal_consume_result.get("message", "黑冠封印每战只能施放 1 次。")))
			return false
	var combat_profile = skill_def.combat_profile
	var costs := get_effective_skill_costs(active_unit, skill_def)
	active_unit.current_ap = maxi(active_unit.current_ap - int(costs.get("ap_cost", combat_profile.ap_cost)), 0)
	active_unit.current_mp = maxi(active_unit.current_mp - int(costs.get("mp_cost", combat_profile.mp_cost)), 0)
	active_unit.current_stamina = maxi(active_unit.current_stamina - int(costs.get("stamina_cost", combat_profile.stamina_cost)), 0)
	active_unit.current_aura = maxi(active_unit.current_aura - int(costs.get("aura_cost", combat_profile.aura_cost)), 0)
	var cooldown := maxi(int(costs.get("cooldown_tu", combat_profile.cooldown_tu)), 0)
	if cooldown > 0:
		active_unit.cooldowns[skill_def.skill_id] = cooldown
	return true


func get_effective_skill_costs(active_unit: BattleUnitState, skill_def: SkillDef) -> Dictionary:
	if skill_def == null or skill_def.combat_profile == null:
		return {}
	var skill_level: int = _runtime._get_unit_skill_level(active_unit, skill_def.skill_id)
	return skill_def.combat_profile.get_effective_resource_costs(skill_level)


func get_black_contract_push_variant_block_reason(
	active_unit: BattleUnitState,
	cast_variant: CombatCastVariantDef
) -> String:
	if active_unit == null:
		return "技能施放者无效。"
	if cast_variant == null:
		return "黑契推进需要先选择一个代价分支。"
	match cast_variant.variant_id:
		BLACK_CONTRACT_PUSH_VARIANT_BLOOD:
			if active_unit.current_hp <= BLACK_CONTRACT_PUSH_HP_COST:
				return "当前生命不足，无法支付血契代价。"
		BLACK_CONTRACT_PUSH_VARIANT_GUARD:
			if not active_unit.has_status_effect(STATUS_GUARDING):
				return "当前没有 Guard，无法支付护契代价。"
		BLACK_CONTRACT_PUSH_VARIANT_ACTION:
			return ""
		_:
			return "黑契推进的施法形态无效。"
	return ""


func consume_black_contract_push_cast(
	active_unit: BattleUnitState,
	cast_variant: CombatCastVariantDef,
	batch: BattleEventBatch = null
) -> bool:
	var block_reason := get_black_contract_push_variant_block_reason(active_unit, cast_variant)
	if not block_reason.is_empty():
		if batch != null:
			batch.log_lines.append(block_reason)
		return false
	if active_unit == null or cast_variant == null:
		return false
	match cast_variant.variant_id:
		BLACK_CONTRACT_PUSH_VARIANT_BLOOD:
			active_unit.current_hp = maxi(active_unit.current_hp - BLACK_CONTRACT_PUSH_HP_COST, 1)
			if batch != null:
				batch.log_lines.append("%s 以血契推进，先失去 %d 点生命。" % [
					active_unit.display_name,
					BLACK_CONTRACT_PUSH_HP_COST,
				])
		BLACK_CONTRACT_PUSH_VARIANT_GUARD:
			active_unit.erase_status_effect(STATUS_GUARDING)
			if batch != null:
				batch.log_lines.append("%s 拆解了自己的 Guard，换取这次黑契推进。" % active_unit.display_name)
		BLACK_CONTRACT_PUSH_VARIANT_ACTION:
			_runtime._set_runtime_status_effect(
				active_unit,
				STATUS_STAGGERED,
				DOOM_SHIFT_SELF_DEBUFF_DURATION_TU,
				active_unit.unit_id,
				1,
				{"counts_as_debuff": true}
			)
			if batch != null:
				batch.log_lines.append("%s 透支了下一回合的行动力，换取这次黑契推进。" % active_unit.display_name)
	_runtime._append_changed_unit_id(batch, active_unit.unit_id)
	return true


func ensure_unit_turn_anchor(unit_state: BattleUnitState) -> void:
	if unit_state == null or unit_state.last_turn_tu >= 0:
		return
	unit_state.last_turn_tu = int(_runtime._state.timeline.current_tu) if _runtime._state != null and _runtime._state.timeline != null else 0


func advance_unit_cooldowns(unit_state: BattleUnitState, cooldown_delta: int) -> bool:
	if unit_state == null or cooldown_delta <= 0:
		return false
	var previous_cooldowns: Dictionary = unit_state.cooldowns.duplicate(true)
	var retained_cooldowns: Dictionary = {}
	for skill_id_variant in previous_cooldowns.keys():
		var skill_id := ProgressionDataUtils.to_string_name(skill_id_variant)
		var previous_remaining := int(previous_cooldowns.get(skill_id_variant, 0))
		var remaining := maxi(previous_remaining - cooldown_delta, 0)
		if remaining > 0:
			retained_cooldowns[skill_id] = remaining
	unit_state.cooldowns = retained_cooldowns
	return previous_cooldowns != retained_cooldowns


func consume_turn_cooldown_delta(unit_state: BattleUnitState) -> bool:
	if unit_state == null:
		return false
	var current_tu := int(_runtime._state.timeline.current_tu) if _runtime._state != null and _runtime._state.timeline != null else 0
	if unit_state.last_turn_tu < 0:
		unit_state.last_turn_tu = current_tu
		return false
	var elapsed_tu := maxi(current_tu - unit_state.last_turn_tu, 0)
	unit_state.last_turn_tu = current_tu
	if elapsed_tu <= 0:
		return false
	if elapsed_tu % TU_GRANULARITY != 0:
		push_error("Cooldown delta must use %d TU steps, got %d." % [TU_GRANULARITY, elapsed_tu])
		return false
	return advance_unit_cooldowns(unit_state, elapsed_tu)


func advance_unit_turn_timers(unit_state: BattleUnitState, batch: BattleEventBatch) -> void:
	if unit_state == null:
		return
	var changed := consume_turn_cooldown_delta(unit_state)
	for status_id_variant in unit_state.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		if unit_state.get_status_effect(status_id) == null:
			changed = true

	if changed:
		_runtime._append_changed_unit_id(batch, unit_state.unit_id)


func apply_turn_start_statuses(unit_state: BattleUnitState, batch: BattleEventBatch) -> Dictionary:
	if unit_state == null:
		return {"changed": false, "defeat_source_unit_id": ""}
	var changed := false
	for status_id_str in ProgressionDataUtils.sorted_string_keys(unit_state.status_effects):
		var status_entry = unit_state.get_status_effect(StringName(status_id_str))
		if status_entry == null:
			continue
		var ap_penalty := BattleStatusSemanticTable.get_turn_start_ap_penalty(status_entry)
		if ap_penalty > 0:
			var previous_ap: int = unit_state.current_ap
			unit_state.current_ap = maxi(unit_state.current_ap - ap_penalty, 0)
			if unit_state.current_ap != previous_ap:
				changed = true
				batch.log_lines.append("%s 受到踉跄影响，本回合少 %d 点 AP。" % [unit_state.display_name, previous_ap - unit_state.current_ap])
	if changed:
		_runtime._append_changed_unit_id(batch, unit_state.unit_id)
	return {
		"changed": changed,
		"defeat_source_unit_id": "",
	}


func apply_unit_status_periodic_ticks(
	unit_state: BattleUnitState,
	elapsed_tu: int,
	batch: BattleEventBatch
) -> Dictionary:
	if _runtime._state == null or _runtime._state.timeline == null or unit_state == null or elapsed_tu <= 0:
		return {"changed": false, "defeat_source_unit_id": ""}
	var changed := false
	var defeat_source_unit_id: StringName = &""
	var current_tu := int(_runtime._state.timeline.current_tu)
	var previous_tu := maxi(current_tu - elapsed_tu, 0)
	for status_id_str in ProgressionDataUtils.sorted_string_keys(unit_state.status_effects):
		if not unit_state.is_alive:
			break
		var status_entry = unit_state.get_status_effect(StringName(status_id_str))
		if status_entry == null:
			continue
		var tick_damage := BattleStatusSemanticTable.get_timeline_tick_damage(status_entry)
		if tick_damage <= 0:
			continue
		if status_entry.next_tick_at_tu <= previous_tu:
			status_entry.next_tick_at_tu = previous_tu + status_entry.tick_interval_tu
			changed = true
		var tick_limit_tu := current_tu
		if status_entry.has_duration():
			tick_limit_tu = mini(tick_limit_tu, previous_tu + int(status_entry.duration))
		while unit_state.is_alive and status_entry.next_tick_at_tu > 0 and status_entry.next_tick_at_tu <= tick_limit_tu:
			var previous_hp := unit_state.current_hp
			unit_state.current_hp = maxi(unit_state.current_hp - tick_damage, 0)
			unit_state.is_alive = unit_state.current_hp > 0
			status_entry.next_tick_at_tu += status_entry.tick_interval_tu
			if unit_state.current_hp != previous_hp:
				changed = true
				batch.log_lines.append("%s 受到 %s 持续影响，损失 %d 点生命。" % [
					unit_state.display_name,
					String(status_entry.status_id),
					previous_hp - unit_state.current_hp,
				])
				if not unit_state.is_alive and status_entry.source_unit_id != &"":
					defeat_source_unit_id = status_entry.source_unit_id
		if unit_state.is_alive:
			unit_state.set_status_effect(status_entry)
	return {
		"changed": changed,
		"defeat_source_unit_id": String(defeat_source_unit_id),
	}


func advance_unit_status_durations(unit_state: BattleUnitState, elapsed_tu: int) -> bool:
	if unit_state == null:
		return false
	var changed := false
	var expired_status_ids: Array[StringName] = []
	for status_id_str in ProgressionDataUtils.sorted_string_keys(unit_state.status_effects):
		var status_id := StringName(status_id_str)
		var status_entry = unit_state.get_status_effect(status_id)
		if status_entry == null:
			expired_status_ids.append(status_id)
			changed = true
			continue
		var duration_result: Dictionary = BattleStatusSemanticTable.advance_timeline_duration(status_entry, elapsed_tu)
		if bool(duration_result.get("expired", false)):
			expired_status_ids.append(status_id)
			changed = true
			continue
		if bool(duration_result.get("changed", false)):
			unit_state.set_status_effect(status_entry)
			changed = true
	for expired_status_id in expired_status_ids:
		unit_state.erase_status_effect(expired_status_id)
	return changed


func get_effective_skill_range(active_unit: BattleUnitState, skill_def: SkillDef) -> int:
	return BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_range(active_unit, skill_def)


func resolve_base_skill_range(active_unit: BattleUnitState, skill_def: SkillDef) -> int:
	return BATTLE_RANGE_SERVICE_SCRIPT.resolve_base_skill_range(active_unit, skill_def)


func is_weapon_range_skill(skill_def: SkillDef) -> bool:
	return BATTLE_RANGE_SERVICE_SCRIPT.is_weapon_range_skill(skill_def)


func get_weapon_attack_range(active_unit: BattleUnitState) -> int:
	return BATTLE_RANGE_SERVICE_SCRIPT.get_weapon_attack_range(active_unit)


func skill_has_tag(skill_def: SkillDef, expected_tag: StringName) -> bool:
	if skill_def == null or expected_tag == &"":
		return false
	for tag in skill_def.tags:
		if ProgressionDataUtils.to_string_name(tag) == expected_tag:
			return true
	return false


func is_movement_blocked(unit_state: BattleUnitState) -> bool:
	return has_status(unit_state, STATUS_PINNED) or has_status(unit_state, STATUS_ROOTED) or has_status(unit_state, STATUS_TENDON_CUT)


func has_status(unit_state: BattleUnitState, status_id: StringName) -> bool:
	if unit_state == null or status_id == &"":
		return false
	return unit_state.has_status_effect(status_id)


func consume_status_if_present(unit_state: BattleUnitState, status_id: StringName, batch: BattleEventBatch = null) -> void:
	if unit_state == null or status_id == &"" or not unit_state.has_status_effect(status_id):
		return
	unit_state.erase_status_effect(status_id)
	if batch != null:
		_runtime._append_changed_unit_id(batch, unit_state.unit_id)


func is_main_skill_locked_by_status(active_unit: BattleUnitState, skill_def: SkillDef) -> bool:
	if active_unit == null or skill_def == null:
		return false
	if active_unit.known_active_skill_ids.is_empty():
		return false
	if active_unit.known_active_skill_ids[0] != skill_def.skill_id:
		return false
	var required_debuff_count := get_status_param_max_int(active_unit, &"main_skill_lock_other_debuff_count")
	if required_debuff_count <= 0:
		return false
	return count_debuff_statuses(active_unit) >= required_debuff_count


func count_debuff_statuses(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 0
	var debuff_count := 0
	for status_id_variant in unit_state.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = unit_state.get_status_effect(status_id)
		if status_entry == null:
			continue
		if status_counts_as_debuff(status_id, status_entry):
			debuff_count += 1
	return debuff_count


func status_counts_as_debuff(status_id: StringName, status_entry: BattleStatusEffectState) -> bool:
	if status_entry != null and status_entry.params != null:
		var params: Dictionary = status_entry.params
		if _status_params_has_formal_key(params, "counts_as_debuff"):
			return bool(_status_params_get_formal_value(params, "counts_as_debuff", false))
	return bool(DEBUFF_STATUS_IDS.get(status_id, false))


func has_status_param_bool(unit_state: BattleUnitState, param_key: StringName) -> bool:
	if unit_state == null or param_key == &"":
		return false
	for status_id_variant in unit_state.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = unit_state.get_status_effect(status_id)
		if status_entry == null or status_entry.params == null:
			continue
		var params: Dictionary = status_entry.params
		if bool(_status_params_get_formal_value(params, String(param_key), false)):
			return true
	return false


func get_status_param_max_int(unit_state: BattleUnitState, param_key: StringName) -> int:
	if unit_state == null or param_key == &"":
		return 0
	var max_value := 0
	for status_id_variant in unit_state.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = unit_state.get_status_effect(status_id)
		if status_entry == null or status_entry.params == null:
			continue
		var params: Dictionary = status_entry.params
		max_value = maxi(int(_status_params_get_formal_value(params, String(param_key), 0)), max_value)
	return max_value


func _status_params_has_formal_key(params: Dictionary, param_key: String) -> bool:
	for key_variant in params.keys():
		if key_variant is String and key_variant == param_key:
			return true
	return false


func _status_params_get_formal_value(params: Dictionary, param_key: String, default_value: Variant) -> Variant:
	for key_variant in params.keys():
		if key_variant is String and key_variant == param_key:
			return params[key_variant]
	return default_value


func _is_black_contract_push_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == BLACK_CONTRACT_PUSH_SKILL_ID


func _is_black_star_brand_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == BLACK_STAR_BRAND_SKILL_ID


func _is_crown_break_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == CROWN_BREAK_SKILL_ID


func _is_doom_sentence_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == DOOM_SENTENCE_SKILL_ID


func _is_black_crown_seal_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == BLACK_CROWN_SEAL_SKILL_ID
