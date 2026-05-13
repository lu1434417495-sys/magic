class_name BattleRuntimeSkillTurnResolver
extends RefCounted

const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BattleStatusSemanticTable = preload("res://scripts/systems/battle/rules/battle_status_semantic_table.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleSaveResolver = preload("res://scripts/systems/battle/rules/battle_save_resolver.gd")
const BodySizeRules = preload("res://scripts/systems/progression/body_size_rules.gd")
const BATTLE_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_range_service.gd")
const MISFORTUNE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/fate/misfortune_service.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const STATUS_PINNED: StringName = &"pinned"
const STATUS_ROOTED: StringName = &"rooted"
const STATUS_TENDON_CUT: StringName = &"tendon_cut"
const STATUS_STAGGERED: StringName = &"staggered"
const STATUS_METEOR_CONCUSSED: StringName = &"meteor_concussed"
const STATUS_PETRIFIED: StringName = &"petrified"
const STATUS_MADNESS: StringName = &"madness"
const STATUS_GUARDING: StringName = &"guarding"
const STATUS_BLACK_STAR_BRAND_NORMAL: StringName = &"black_star_brand_normal"
const STATUS_CROWN_BREAK_BROKEN_HAND: StringName = &"crown_break_broken_hand"
const BLACK_CONTRACT_PUSH_SKILL_ID: StringName = &"black_contract_push"
const BLACK_CONTRACT_PUSH_VARIANT_BLOOD: StringName = &"blood_tithe"
const BLACK_CONTRACT_PUSH_VARIANT_GUARD: StringName = &"guard_tithe"
const BLACK_CONTRACT_PUSH_VARIANT_ACTION: StringName = &"action_tithe"
const DOOM_SHIFT_SELF_DEBUFF_DURATION_TU := 60
const BLACK_CONTRACT_PUSH_HP_COST := 10
const IDENTITY_SKILL_LEARN_SOURCES := {
	&"race": true,
	&"subrace": true,
	&"ascension": true,
	&"bloodline": true,
}
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
	&"meteor_concussed": true,
	&"pinned": true,
	&"petrified": true,
	&"rooted": true,
	&"shocked": true,
	&"slow": true,
	&"staggered": true,
	&"taunted": true,
	&"tendon_cut": true,
}
const TU_GRANULARITY := 5
const STATUS_PARAM_BODY_SIZE_CATEGORY_OVERRIDE := "body_size_category_override"
const STATUS_PARAM_PREVIOUS_BODY_SIZE_CATEGORY := "previous_body_size_category"
const AI_BLACKBOARD_TURN_OVERRIDE := "madness_ai_control"
const AI_BLACKBOARD_ANY_UNIT_TARGETING := "madness_target_any_team"

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


func resolve_turn_control_status(unit_state: BattleUnitState, batch: BattleEventBatch) -> Dictionary:
	var result := {
		"skip_turn": false,
		"changed": false,
		"ai_controlled": false,
		"ai_target_policy": "",
		"cleanup_on_turn_end": false,
		"status_removed": false,
	}
	if unit_state == null or not unit_state.is_alive:
		return result
	if unit_state.has_status_effect(STATUS_PETRIFIED):
		var petrified_entry := unit_state.get_status_effect(STATUS_PETRIFIED) as BattleStatusEffectState
		var petrified_save := _resolve_status_self_save(unit_state, petrified_entry, UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION)
		if bool(petrified_save.get("success", false)):
			unit_state.erase_status_effect(STATUS_PETRIFIED)
			_append_changed_unit(batch, unit_state)
			_append_log(batch, "%s 通过体质检定，解除石化并立刻恢复行动。" % unit_state.display_name)
			result["changed"] = true
			result["status_removed"] = true
			return result
		unit_state.current_ap = 0
		unit_state.current_move_points = 0
		_append_changed_unit(batch, unit_state)
		_append_log(batch, "%s 石化未解除，无法行动。" % unit_state.display_name)
		result["skip_turn"] = true
		result["changed"] = true
		return result
	if unit_state.has_status_effect(STATUS_MADNESS):
		var madness_entry := unit_state.get_status_effect(STATUS_MADNESS) as BattleStatusEffectState
		var madness_save := _resolve_status_self_save(unit_state, madness_entry, UNIT_BASE_ATTRIBUTES_SCRIPT.WILLPOWER, UNIT_BASE_ATTRIBUTES_SCRIPT.WILLPOWER)
		if bool(madness_save.get("success", false)):
			unit_state.erase_status_effect(STATUS_MADNESS)
			clear_turn_ai_override(unit_state)
			_append_changed_unit(batch, unit_state)
			_append_log(batch, "%s 通过意志检定，摆脱疯狂并立刻恢复行动。" % unit_state.display_name)
			result["changed"] = true
			result["status_removed"] = true
			return result
		unit_state.ai_blackboard[AI_BLACKBOARD_TURN_OVERRIDE] = true
		unit_state.ai_blackboard[AI_BLACKBOARD_ANY_UNIT_TARGETING] = true
		_append_changed_unit(batch, unit_state)
		_append_log(batch, "%s 疯狂未解除，本次行动由 AI 接管且不区分敌我。" % unit_state.display_name)
		result["changed"] = true
		result["ai_controlled"] = true
		result["ai_target_policy"] = "any_unit"
		result["cleanup_on_turn_end"] = true
	return result


func is_turn_ai_override_active(unit_state: BattleUnitState) -> bool:
	return unit_state != null and bool(unit_state.ai_blackboard.get(AI_BLACKBOARD_TURN_OVERRIDE, false))


func clear_turn_ai_override(unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	unit_state.ai_blackboard.erase(AI_BLACKBOARD_TURN_OVERRIDE)
	unit_state.ai_blackboard.erase(AI_BLACKBOARD_ANY_UNIT_TARGETING)


func build_madness_fallback_command(unit_state: BattleUnitState):
	if _runtime == null or _runtime._state == null or unit_state == null:
		return null
	for raw_skill_id in unit_state.known_active_skill_ids:
		var skill_id := ProgressionDataUtils.to_string_name(raw_skill_id)
		var skill_def = _runtime._skill_defs.get(skill_id) as SkillDef
		if skill_def == null or skill_def.combat_profile == null:
			continue
		if skill_def.combat_profile.target_mode != &"unit":
			continue
		var target_unit: BattleUnitState = _find_madness_unit_target(unit_state, skill_def)
		if target_unit == null:
			continue
		var command := BattleCommand.new()
		command.command_type = BattleCommand.TYPE_SKILL
		command.unit_id = unit_state.unit_id
		command.skill_id = skill_id
		command.target_unit_id = target_unit.unit_id
		command.target_coord = target_unit.coord
		return command
	var wait_command := BattleCommand.new()
	wait_command.command_type = BattleCommand.TYPE_WAIT
	wait_command.unit_id = unit_state.unit_id
	return wait_command


func get_skill_cast_block_reason(active_unit: BattleUnitState, skill_def: SkillDef) -> String:
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return "技能或目标无效。"
	var combat_profile = skill_def.combat_profile
	var costs: Dictionary = get_effective_skill_costs(active_unit, skill_def)
	var cooldown := int(active_unit.cooldowns.get(skill_def.skill_id, 0))
	if cooldown > 0:
		return "%s 仍在冷却中（%d）。" % [skill_def.display_name, cooldown]
	var locked_resource_block_reason := get_locked_combat_resource_block_reason(active_unit, costs)
	if not locked_resource_block_reason.is_empty():
		return locked_resource_block_reason
	if active_unit.current_ap < int(costs.get("ap_cost", combat_profile.ap_cost)):
		return "AP不足，无法施放该技能。"
	if active_unit.current_mp < int(costs.get("mp_cost", combat_profile.mp_cost)):
		return "法力不足，无法施放该技能。"
	if active_unit.current_stamina < int(costs.get("stamina_cost", combat_profile.stamina_cost)):
		return "体力不足，无法施放该技能。"
	if has_status(active_unit, STATUS_PETRIFIED):
		return "当前处于石化状态，无法施放技能。"
	if active_unit.current_aura < int(costs.get("aura_cost", combat_profile.aura_cost)):
		return "斗气不足，无法施放该技能。"
	var racial_charge_block_reason := get_racial_skill_charge_block_reason(active_unit, skill_def)
	if not racial_charge_block_reason.is_empty():
		return racial_charge_block_reason
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
	var misfortune_block_reason := get_misfortune_skill_cast_block_reason(active_unit, skill_def)
	if not misfortune_block_reason.is_empty():
		return misfortune_block_reason
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


func get_misfortune_skill_cast_block_reason(active_unit: BattleUnitState, skill_def: SkillDef) -> String:
	if skill_def == null or not MISFORTUNE_SERVICE_SCRIPT.is_misfortune_gated_skill(skill_def.skill_id):
		return ""
	if _runtime == null or not _runtime.has_method("get_misfortune_skill_cast_block_reason"):
		return MISFORTUNE_SERVICE_SCRIPT.get_skill_sidecar_missing_message(skill_def.skill_id)
	return _runtime.get_misfortune_skill_cast_block_reason(active_unit, skill_def.skill_id)


func consume_skill_costs(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null,
	batch: BattleEventBatch = null
) -> bool:
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return false
	var combat_profile = skill_def.combat_profile
	var costs: Dictionary = get_effective_skill_costs(active_unit, skill_def)
	var locked_resource_block_reason := get_locked_combat_resource_block_reason(active_unit, costs)
	if not locked_resource_block_reason.is_empty():
		if batch != null:
			batch.log_lines.append(locked_resource_block_reason)
		return false
	if _is_black_contract_push_skill(skill_def.skill_id):
		if not consume_black_contract_push_cast(active_unit, cast_variant, batch):
			return false
	if not consume_misfortune_skill_gate(active_unit, skill_def, batch):
		return false
	if not consume_racial_skill_charge(active_unit, skill_def, batch):
		return false
	active_unit.current_ap = maxi(active_unit.current_ap - int(costs.get("ap_cost", combat_profile.ap_cost)), 0)
	active_unit.current_mp = maxi(active_unit.current_mp - int(costs.get("mp_cost", combat_profile.mp_cost)), 0)
	active_unit.current_stamina = maxi(active_unit.current_stamina - int(costs.get("stamina_cost", combat_profile.stamina_cost)), 0)
	active_unit.current_aura = maxi(active_unit.current_aura - int(costs.get("aura_cost", combat_profile.aura_cost)), 0)
	var cooldown := maxi(int(costs.get("cooldown_tu", combat_profile.cooldown_tu)), 0)
	if cooldown > 0:
		active_unit.cooldowns[skill_def.skill_id] = cooldown
	return true


func consume_misfortune_skill_gate(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	batch: BattleEventBatch = null
) -> bool:
	if skill_def == null or not MISFORTUNE_SERVICE_SCRIPT.is_misfortune_gated_skill(skill_def.skill_id):
		return true
	if _runtime == null or not _runtime.has_method("consume_misfortune_skill_cast"):
		if batch != null:
			batch.log_lines.append(MISFORTUNE_SERVICE_SCRIPT.get_skill_sidecar_missing_message(skill_def.skill_id))
		return false
	var consume_result: Dictionary = _runtime.consume_misfortune_skill_cast(active_unit, skill_def.skill_id)
	if not bool(consume_result.get("ok", false)):
		if batch != null:
			batch.log_lines.append(String(consume_result.get("message", MISFORTUNE_SERVICE_SCRIPT.get_skill_default_block_message(skill_def.skill_id))))
		return false
	return true


func get_racial_skill_charge_block_reason(active_unit: BattleUnitState, skill_def: SkillDef) -> String:
	if active_unit == null or not _is_identity_granted_skill(skill_def):
		return ""
	var charge_key := get_racial_skill_charge_key(skill_def.skill_id)
	if active_unit.per_battle_charges.has(charge_key) and int(active_unit.per_battle_charges.get(charge_key, 0)) <= 0:
		return "%s 的身份技能次数已用尽。" % _get_skill_display_name(skill_def)
	if active_unit.per_turn_charges.has(charge_key) and int(active_unit.per_turn_charges.get(charge_key, 0)) <= 0:
		return "%s 本回合无法再次使用。" % _get_skill_display_name(skill_def)
	return ""


func consume_racial_skill_charge(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	batch: BattleEventBatch = null
) -> bool:
	var block_reason := get_racial_skill_charge_block_reason(active_unit, skill_def)
	if not block_reason.is_empty():
		if batch != null:
			batch.log_lines.append(block_reason)
		return false
	if active_unit == null or not _is_identity_granted_skill(skill_def):
		return true
	var charge_key := get_racial_skill_charge_key(skill_def.skill_id)
	if active_unit.per_battle_charges.has(charge_key):
		active_unit.per_battle_charges[charge_key] = maxi(int(active_unit.per_battle_charges.get(charge_key, 0)) - 1, 0)
	if active_unit.per_turn_charges.has(charge_key):
		active_unit.per_turn_charges[charge_key] = maxi(int(active_unit.per_turn_charges.get(charge_key, 0)) - 1, 0)
	return true


func get_racial_skill_charge_key(skill_id: StringName) -> StringName:
	if skill_id == &"":
		return &""
	return StringName("racial_skill_%s" % String(skill_id))


func get_effective_skill_costs(active_unit: BattleUnitState, skill_def: SkillDef) -> Dictionary:
	if skill_def == null or skill_def.combat_profile == null:
		return {}
	var skill_level: int = _runtime._get_unit_skill_level(active_unit, skill_def.skill_id)
	return skill_def.combat_profile.get_effective_resource_costs(skill_level)


func get_locked_combat_resource_block_reason(active_unit: BattleUnitState, costs: Dictionary) -> String:
	if active_unit == null:
		return "技能施放者无效。"
	if int(costs.get("mp_cost", 0)) > 0 and not active_unit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_MP):
		return "法力尚未解锁，无法施放该技能。"
	if int(costs.get("stamina_cost", 0)) > 0 and not active_unit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_STAMINA):
		return "体力尚未解锁，无法施放该技能。"
	if int(costs.get("aura_cost", 0)) > 0 and not active_unit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_AURA):
		return "斗气尚未解锁，无法施放该技能。"
	return ""


func _is_identity_granted_skill(skill_def: SkillDef) -> bool:
	return skill_def != null and IDENTITY_SKILL_LEARN_SOURCES.has(ProgressionDataUtils.to_string_name(skill_def.learn_source))


func _get_skill_display_name(skill_def: SkillDef) -> String:
	if skill_def == null:
		return "身份技能"
	if not skill_def.display_name.strip_edges().is_empty():
		return skill_def.display_name
	if skill_def.skill_id != &"":
		return String(skill_def.skill_id)
	return "身份技能"


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
	var penalty_by_group: Dictionary = {}
	var label_by_group: Dictionary = {}
	var consume_status_ids: Array[StringName] = []
	for status_id_str in ProgressionDataUtils.sorted_string_keys(unit_state.status_effects):
		var status_entry = unit_state.get_status_effect(StringName(status_id_str)) as BattleStatusEffectState
		if status_entry == null:
			continue
		var ap_penalty := BattleStatusSemanticTable.get_turn_start_ap_penalty(status_entry)
		if ap_penalty <= 0:
			continue
		var penalty_group := BattleStatusSemanticTable.get_turn_start_ap_penalty_group(status_entry)
		if penalty_group == &"":
			penalty_group = status_entry.status_id
		if ap_penalty > int(penalty_by_group.get(penalty_group, 0)):
			penalty_by_group[penalty_group] = ap_penalty
			label_by_group[penalty_group] = BattleStatusSemanticTable.get_turn_start_ap_penalty_display_label(status_entry)
		if BattleStatusSemanticTable.should_consume_after_turn_start_ap_penalty(status_entry):
			consume_status_ids.append(status_entry.status_id)
	for group_id_str in ProgressionDataUtils.sorted_string_keys(penalty_by_group):
		var group_id := StringName(group_id_str)
		var group_penalty := int(penalty_by_group.get(group_id, 0))
		if group_penalty <= 0:
			continue
		var previous_ap: int = unit_state.current_ap
		unit_state.current_ap = maxi(unit_state.current_ap - group_penalty, 0)
		var consumed_ap := previous_ap - unit_state.current_ap
		if consumed_ap > 0:
			changed = true
			var label := String(label_by_group.get(group_id, "状态"))
			batch.log_lines.append("%s 受到%s影响，本回合少 %d 点 AP。" % [unit_state.display_name, label, consumed_ap])
	for status_id in consume_status_ids:
		if unit_state.has_status_effect(status_id):
			unit_state.erase_status_effect(status_id)
			changed = true
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


func advance_unit_status_durations(unit_state: BattleUnitState, elapsed_tu: int, batch: BattleEventBatch = null) -> bool:
	if unit_state == null:
		return false
	var changed := false
	var expired_status_ids: Array[StringName] = []
	var expired_status_entries: Dictionary = {}
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
			expired_status_entries[status_id] = status_entry
			changed = true
			continue
		if bool(duration_result.get("changed", false)):
			unit_state.set_status_effect(status_entry)
			changed = true
	for expired_status_id in expired_status_ids:
		var expired_status_entry := expired_status_entries.get(expired_status_id) as BattleStatusEffectState
		var should_erase_status := true
		if _is_body_size_category_override_status(expired_status_entry):
			should_erase_status = false
			if _restore_body_size_category_override_if_needed(unit_state, expired_status_entry, batch):
				changed = true
				should_erase_status = true
		if should_erase_status:
			unit_state.erase_status_effect(expired_status_id)
	return changed


func _is_body_size_category_override_status(status_entry: BattleStatusEffectState) -> bool:
	return status_entry != null \
		and status_entry.params != null \
		and status_entry.params.has(STATUS_PARAM_BODY_SIZE_CATEGORY_OVERRIDE)


func _restore_body_size_category_override_if_needed(
	unit_state: BattleUnitState,
	status_entry: BattleStatusEffectState,
	batch: BattleEventBatch = null
) -> bool:
	if unit_state == null or status_entry == null or status_entry.params == null:
		return false
	var params: Dictionary = status_entry.params
	if not params.has(STATUS_PARAM_BODY_SIZE_CATEGORY_OVERRIDE):
		return false
	var previous_category := ProgressionDataUtils.to_string_name(params.get(STATUS_PARAM_PREVIOUS_BODY_SIZE_CATEGORY, ""))
	if not BodySizeRules.is_valid_body_size_category(previous_category):
		return false
	if unit_state.body_size_category == previous_category:
		return false

	var previous_coords := unit_state.occupied_coords.duplicate()
	var current_category := unit_state.body_size_category
	var runtime = _runtime
	var grid_service = runtime.get_grid_service() if runtime != null and runtime.has_method("get_grid_service") else null
	var state = runtime.get_state() if runtime != null and runtime.has_method("get_state") else null
	if grid_service != null and state != null:
		grid_service.clear_unit_occupancy(state, unit_state)
	unit_state.set_body_size_category(previous_category)
	if grid_service != null and state != null:
		if not grid_service.can_place_unit(state, unit_state, unit_state.coord, true):
			unit_state.set_body_size_category(current_category)
			grid_service.set_occupants(state, previous_coords, unit_state.unit_id)
			return false
		grid_service.set_occupants(state, unit_state.occupied_coords, unit_state.unit_id)
	if runtime != null and batch != null:
		runtime._append_changed_coords(batch, previous_coords)
		runtime._append_changed_unit_coords(batch, unit_state)
		runtime._append_changed_unit_id(batch, unit_state.unit_id)
	return true


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
	return has_status(unit_state, STATUS_PINNED) \
		or has_status(unit_state, STATUS_ROOTED) \
		or has_status(unit_state, STATUS_TENDON_CUT) \
		or has_status(unit_state, STATUS_PETRIFIED)


func has_status(unit_state: BattleUnitState, status_id: StringName) -> bool:
	if unit_state == null or status_id == &"":
		return false
	return unit_state.has_status_effect(status_id)


func _resolve_status_self_save(
	unit_state: BattleUnitState,
	status_entry: BattleStatusEffectState,
	fallback_ability: StringName,
	fallback_tag: StringName
) -> Dictionary:
	var params: Dictionary = status_entry.params if status_entry != null and status_entry.params != null else {}
	var effect := CombatEffectDef.new()
	effect.effect_type = &"status"
	effect.save_dc = maxi(int(params.get("self_save_dc", 16)), 1)
	effect.save_dc_mode = BattleSaveResolver.SAVE_DC_MODE_STATIC
	effect.save_ability = ProgressionDataUtils.to_string_name(params.get("self_save_ability", fallback_ability))
	effect.save_tag = ProgressionDataUtils.to_string_name(params.get("self_save_tag", fallback_tag))
	var context := {}
	if params.has("self_save_roll_override"):
		context["save_roll_override"] = int(params.get("self_save_roll_override", 0))
	var source_unit: BattleUnitState = null
	if _runtime != null and _runtime._state != null and status_entry != null and status_entry.source_unit_id != &"":
		source_unit = _runtime._state.units.get(status_entry.source_unit_id) as BattleUnitState
	return BattleSaveResolver.resolve_save(source_unit, unit_state, effect, context)


func _find_madness_unit_target(unit_state: BattleUnitState, skill_def: SkillDef):
	if _runtime == null or _runtime._state == null or unit_state == null:
		return null
	var best_unit: BattleUnitState = null
	var best_distance := 999999
	var effective_range: int = _runtime._get_effective_skill_range(unit_state, skill_def)
	for unit_variant in _runtime._state.units.values():
		var candidate := unit_variant as BattleUnitState
		if candidate == null or not candidate.is_alive or candidate.unit_id == unit_state.unit_id:
			continue
		var distance: int = _runtime._grid_service.get_distance_between_units(unit_state, candidate)
		if distance > effective_range:
			continue
		if best_unit == null or distance < best_distance:
			best_unit = candidate
			best_distance = distance
	return best_unit


func _append_changed_unit(batch: BattleEventBatch, unit_state: BattleUnitState) -> void:
	if _runtime == null or batch == null or unit_state == null:
		return
	_runtime._append_changed_unit_id(batch, unit_state.unit_id)
	_runtime._append_changed_unit_coords(batch, unit_state)


func _append_log(batch: BattleEventBatch, line: String) -> void:
	if batch == null or line.is_empty():
		return
	batch.log_lines.append(line)


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
