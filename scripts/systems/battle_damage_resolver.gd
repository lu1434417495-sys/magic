## 文件说明：该脚本属于战斗伤害解析器相关的解析脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleDamageResolver
extends RefCounted

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const BATTLE_STATUS_SEMANTIC_TABLE_SCRIPT = preload("res://scripts/systems/battle_status_semantic_table.gd")
const BATTLE_FATE_EVENT_BUS_SCRIPT = preload("res://scripts/systems/battle_fate_event_bus.gd")
const BATTLE_REPORT_FORMATTER_SCRIPT = preload("res://scripts/systems/battle_report_formatter.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle_status_effect_state.gd")
const BATTLE_FATE_ATTACK_RULES_SCRIPT = preload("res://scripts/systems/battle_fate_attack_rules.gd")
const FATE_ATTACK_FORMULA_SCRIPT = preload("res://scripts/systems/fate_attack_formula.gd")
const LOW_LUCK_RELIC_RULES_SCRIPT = preload("res://scripts/systems/low_luck_relic_rules.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleFateEventBus = preload("res://scripts/systems/battle_fate_event_bus.gd")
const BattleReportFormatter = preload("res://scripts/systems/battle_report_formatter.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle_status_effect_state.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const STATUS_ATTACK_UP: StringName = &"attack_up"
const STATUS_DAMAGE_REDUCTION_UP: StringName = &"damage_reduction_up"
const STATUS_GUARDING: StringName = &"guarding"
const STATUS_MARKED: StringName = &"marked"
const STATUS_ARMOR_BREAK: StringName = &"armor_break"
const STATUS_ARCHER_PRE_AIM: StringName = &"archer_pre_aim"
const BONUS_CONDITION_TARGET_LOW_HP: StringName = &"target_low_hp"
const MIN_DAMAGE_FLOOR := 0
const DAMAGE_TAG_PHYSICAL_SLASH: StringName = &"physical_slash"
const DAMAGE_TAG_PHYSICAL_PIERCE: StringName = &"physical_pierce"
const DAMAGE_TAG_PHYSICAL_BLUNT: StringName = &"physical_blunt"
const DAMAGE_TAG_FIRE: StringName = &"fire"
const DAMAGE_TAG_FREEZE: StringName = &"freeze"
const DAMAGE_TAG_LIGHTNING: StringName = &"lightning"
const DAMAGE_TAG_NEGATIVE_ENERGY: StringName = &"negative_energy"
const DAMAGE_TAG_MAGIC: StringName = &"magic"
const MITIGATION_TIER_NORMAL: StringName = &"normal"
const MITIGATION_TIER_HALF: StringName = &"half"
const MITIGATION_TIER_DOUBLE: StringName = &"double"
const MITIGATION_TIER_IMMUNE: StringName = &"immune"
const DAMAGE_REDUCTION_UP_FIXED_PER_POWER := 2
const GUARDING_PHYSICAL_REDUCTION_PER_POWER := 4
const ATTACK_CHECK_TARGET := 21
const NATURAL_HIT_ROLL := 20
const ATTACK_RESOLUTION_HIT: StringName = &"hit"
const ATTACK_RESOLUTION_MISS: StringName = &"miss"
const ATTACK_RESOLUTION_CRITICAL_HIT: StringName = &"critical_hit"
const ATTACK_RESOLUTION_CRITICAL_FAIL: StringName = &"critical_fail"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"
const STATUS_BLACK_STAR_BRAND_ELITE: StringName = &"black_star_brand_elite"
const STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW: StringName = &"black_star_brand_elite_guard_window"
const STATUS_CROWN_BREAK_BROKEN_FANG: StringName = &"crown_break_broken_fang"
const STATUS_CROWN_BREAK_BROKEN_HAND: StringName = &"crown_break_broken_hand"
const STATUS_CROWN_BREAK_BLINDED_EYE: StringName = &"crown_break_blinded_eye"
const BLACK_STAR_BRAND_GUARD_IGNORE_FLAT := 4

var _fate_event_bus: BattleFateEventBus = BATTLE_FATE_EVENT_BUS_SCRIPT.new()
var _fate_attack_rules = BATTLE_FATE_ATTACK_RULES_SCRIPT.new()
var _report_formatter: BattleReportFormatter = BATTLE_REPORT_FORMATTER_SCRIPT.new()


class SeededAttackRng:
	extends RefCounted

	var _resolver: Variant = null
	var _battle_state: BattleState = null


	func _init(resolver: Variant, battle_state: BattleState) -> void:
		_resolver = resolver
		_battle_state = battle_state


	func randi_range(min_value: int, max_value: int) -> int:
		if _resolver == null or _battle_state == null:
			return min_value
		return int(_resolver._roll_seeded_attack_range(_battle_state, min_value, max_value))


func resolve_skill(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	attack_check: Dictionary = {},
	attack_context: Dictionary = {}
) -> Dictionary:
	if source_unit == null or target_unit == null or skill_def == null or skill_def.combat_profile == null:
		return _build_empty_result()
	if not attack_check.is_empty():
		return resolve_attack_effects(
			source_unit,
			target_unit,
			skill_def.combat_profile.effect_defs,
			attack_check,
			attack_context
		)
	return resolve_effects(source_unit, target_unit, skill_def.combat_profile.effect_defs)


func resolve_attack_effects(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	effect_defs: Array,
	attack_check: Dictionary,
	attack_context: Dictionary = {}
) -> Dictionary:
	if source_unit == null or target_unit == null:
		return _build_attack_metadata_result(_build_empty_result(), {})

	var attack_metadata := _resolve_attack_metadata(source_unit, target_unit, attack_check, attack_context)
	if not bool(attack_metadata.get("attack_success", false)):
		var failed_result := _build_attack_metadata_result(_build_empty_result(), attack_metadata)
		_attach_attack_report_entry(failed_result, source_unit, target_unit)
		_dispatch_attack_resolution_events(source_unit, target_unit, attack_metadata, attack_context)
		return failed_result

	var resolved_result := _build_attack_metadata_result(resolve_effects(source_unit, target_unit, effect_defs), attack_metadata)
	_attach_attack_report_entry(resolved_result, source_unit, target_unit)
	_dispatch_attack_resolution_events(source_unit, target_unit, attack_metadata, attack_context)
	return resolved_result


func resolve_effects(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	effect_defs: Array
) -> Dictionary:
	if source_unit == null or target_unit == null:
		return _build_empty_result()

	var total_damage := 0
	var total_healing := 0
	var total_shield_absorbed := 0
	var damage_events: Array[Dictionary] = []
	var status_effect_ids: Array[StringName] = []
	var source_status_effect_ids: Array[StringName] = []
	var terrain_effect_ids: Array[StringName] = []
	var total_height_delta := 0
	var shield_broken := false
	var applied := false
	var black_star_wedge_triggered := false

	for effect_def in effect_defs:
		if effect_def == null:
			continue
		match effect_def.effect_type:
			&"damage":
				var damage_outcome := _resolve_damage_outcome(source_unit, target_unit, effect_def)
				var damage_result := _apply_damage_to_target(target_unit, damage_outcome)
				var hp_damage := int(damage_result.get("damage", 0))
				total_damage += hp_damage
				total_shield_absorbed += int(damage_result.get("shield_absorbed", 0))
				damage_events.append(damage_result.duplicate(true))
				black_star_wedge_triggered = black_star_wedge_triggered or bool(
					damage_result.get("low_luck_black_star_wedge_triggered", false)
				)
				shield_broken = shield_broken or bool(damage_result.get("shield_broken", false))
				applied = true
			&"heal":
				var heal_amount := maxi(effect_def.power, 1)
				var max_hp := 0
				if target_unit.attribute_snapshot != null:
					max_hp = target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)
				if max_hp > 0:
					target_unit.current_hp = mini(target_unit.current_hp + heal_amount, max_hp)
				else:
					target_unit.current_hp += heal_amount
				total_healing += heal_amount
				applied = true
			&"status", &"apply_status":
				if effect_def.status_id != &"":
					_apply_status_effect(target_unit, source_unit, effect_def)
					if not status_effect_ids.has(effect_def.status_id):
						status_effect_ids.append(effect_def.status_id)
					applied = true
			&"terrain", &"terrain_effect":
				if effect_def.terrain_effect_id != &"":
					if not terrain_effect_ids.has(effect_def.terrain_effect_id):
						terrain_effect_ids.append(effect_def.terrain_effect_id)
					applied = true
			&"height", &"height_delta":
				if effect_def.height_delta != 0:
					total_height_delta += int(effect_def.height_delta)
					applied = true
			_:
				pass

	target_unit.is_alive = target_unit.current_hp > 0
	if black_star_wedge_triggered and target_unit.is_alive:
		if _apply_low_luck_black_star_wedge_exposed(source_unit):
			source_status_effect_ids.append(LOW_LUCK_RELIC_RULES_SCRIPT.STATUS_BLACK_STAR_WEDGE_EXPOSED)
	return {
		"applied": applied,
		"damage": total_damage,
		"hp_damage": total_damage,
		"healing": total_healing,
		"shield_absorbed": total_shield_absorbed,
		"shield_broken": shield_broken,
		"damage_events": damage_events,
		"status_effect_ids": status_effect_ids,
		"source_status_effect_ids": source_status_effect_ids,
		"terrain_effect_ids": terrain_effect_ids,
		"height_delta": total_height_delta,
	}


func _resolve_damage_amount(source_unit: BattleUnitState, target_unit: BattleUnitState, effect_def) -> int:
	return int(_resolve_damage_outcome(source_unit, target_unit, effect_def).get("resolved_damage", 0))


func _resolve_attack_metadata(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	attack_check: Dictionary,
	attack_context: Dictionary
) -> Dictionary:
	var hidden_luck_at_birth := _get_hidden_luck_at_birth(source_unit)
	var faith_luck_bonus := _get_faith_luck_bonus(source_unit)
	var effective_luck := _get_effective_luck(source_unit)
	var is_disadvantage := _resolve_attack_disadvantage(source_unit, target_unit, attack_context)
	var crit_gate_die := FATE_ATTACK_FORMULA_SCRIPT.calc_crit_gate_die_size(effective_luck, is_disadvantage)
	var force_hit_no_crit := bool(attack_context.get("force_hit_no_crit", false))
	var crit_locked := _fate_attack_rules.is_attack_crit_locked(source_unit) or force_hit_no_crit
	var required_roll := int(attack_check.get("required_roll", ATTACK_CHECK_TARGET))
	var metadata := {
		"attack_resolution": ATTACK_RESOLUTION_MISS,
		"attack_success": false,
		"critical_hit": false,
		"critical_fail": false,
		"ordinary_miss": false,
		"is_disadvantage": is_disadvantage,
		"hidden_luck_at_birth": hidden_luck_at_birth,
		"faith_luck_bonus": faith_luck_bonus,
		"effective_luck": effective_luck,
		"crit_locked": crit_locked,
		"crit_gate_die": crit_gate_die,
		"crit_gate_roll": 0,
		"hit_roll": 0,
		"fumble_low_end": FATE_ATTACK_FORMULA_SCRIPT.calc_fumble_low_end(effective_luck),
		"crit_threshold": FATE_ATTACK_FORMULA_SCRIPT.calc_crit_threshold(hidden_luck_at_birth, faith_luck_bonus),
		"required_roll": required_roll,
		"display_required_roll": int(attack_check.get("display_required_roll", clampi(required_roll, 2, NATURAL_HIT_ROLL))),
		"hit_rate_percent": int(attack_check.get("hit_rate_percent", 0)),
	}
	if force_hit_no_crit:
		metadata["attack_resolution"] = ATTACK_RESOLUTION_HIT
		metadata["attack_success"] = true
		return metadata

	# Step 1: when crit_gate_die > d20, resolve the critical gate before the hit d20.
	if crit_gate_die > NATURAL_HIT_ROLL:
		var crit_gate_roll := _roll_attack_die(crit_gate_die, is_disadvantage, attack_context)
		metadata["crit_gate_roll"] = crit_gate_roll
		if _fate_attack_rules.does_gate_die_crit(crit_gate_roll, crit_gate_die, crit_locked):
			metadata["attack_resolution"] = ATTACK_RESOLUTION_CRITICAL_HIT
			metadata["attack_success"] = true
			metadata["critical_hit"] = true
			return metadata

	# Step 2: roll the hit d20 after the gate die.
	var hit_roll := _roll_attack_die(NATURAL_HIT_ROLL, is_disadvantage, attack_context)
	metadata["hit_roll"] = hit_roll

	# Step 3: low-end rolls immediately become critical failures.
	if hit_roll <= int(metadata.get("fumble_low_end", 1)):
		if _try_apply_reverse_fate_amulet(source_unit):
			metadata["attack_resolution"] = ATTACK_RESOLUTION_MISS
			metadata["ordinary_miss"] = true
			metadata["reverse_fate_downgraded"] = true
			return metadata
		metadata["attack_resolution"] = ATTACK_RESOLUTION_CRITICAL_FAIL
		metadata["critical_fail"] = true
		return metadata

	# Step 4: when crit_gate_die == d20, high-end rolls upgrade directly to crits.
	if _fate_attack_rules.is_high_threat_crit_roll(
		hit_roll,
		crit_locked,
		crit_gate_die,
		int(metadata.get("crit_threshold", NATURAL_HIT_ROLL))
	):
		metadata["attack_resolution"] = ATTACK_RESOLUTION_CRITICAL_HIT
		metadata["attack_success"] = true
		metadata["critical_hit"] = true
		return metadata

	# Step 5: middle-band rolls use the ordinary attack-total-vs-AC check.
	if _fate_attack_rules.does_attack_roll_hit(hit_roll, attack_check):
		metadata["attack_resolution"] = ATTACK_RESOLUTION_HIT
		metadata["attack_success"] = true
		return metadata

	metadata["ordinary_miss"] = true
	return metadata


func _resolve_attack_disadvantage(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	attack_context: Dictionary
) -> bool:
	if attack_context.has("is_disadvantage"):
		return bool(attack_context.get("is_disadvantage", false))
	var battle_state := attack_context.get("battle_state", null) as BattleState
	if battle_state == null:
		return false
	return battle_state.is_attack_disadvantage(source_unit, target_unit)


func _roll_attack_die(die_size: int, is_disadvantage: bool, attack_context: Dictionary) -> int:
	var explicit_rng = attack_context.get("rng", null)
	if explicit_rng != null and explicit_rng.has_method("randi_range"):
		return int(FATE_ATTACK_FORMULA_SCRIPT.roll_die_with_disadvantage_rule(die_size, is_disadvantage, explicit_rng))
	var battle_state := attack_context.get("battle_state", null) as BattleState
	if battle_state != null:
		var seeded_rng := SeededAttackRng.new(self, battle_state)
		return int(FATE_ATTACK_FORMULA_SCRIPT.roll_die_with_disadvantage_rule(die_size, is_disadvantage, seeded_rng))
	return int(FATE_ATTACK_FORMULA_SCRIPT.roll_die_with_disadvantage_rule(die_size, is_disadvantage))


func _roll_seeded_attack_range(battle_state: BattleState, min_value: int, max_value: int) -> int:
	if battle_state == null:
		return min_value
	var nonce := maxi(int(battle_state.attack_roll_nonce), 0)
	var roll_seed_source := "%s:%d:%d" % [String(battle_state.battle_id), int(battle_state.seed), nonce]
	var rng := RandomNumberGenerator.new()
	rng.seed = int(roll_seed_source.hash())
	battle_state.attack_roll_nonce = nonce + 1
	return int(rng.randi_range(min_value, max_value))


func _does_attack_roll_hit(hit_roll: int, attack_check: Dictionary) -> bool:
	return _fate_attack_rules.does_attack_roll_hit(hit_roll, attack_check)


func _build_attack_metadata_result(result: Dictionary, attack_metadata: Dictionary) -> Dictionary:
	var merged := result.duplicate(true)
	merged["attack_resolution"] = ProgressionDataUtils.to_string_name(attack_metadata.get("attack_resolution", ""))
	merged["attack_success"] = bool(attack_metadata.get("attack_success", false))
	merged["critical_hit"] = bool(attack_metadata.get("critical_hit", false))
	merged["critical_fail"] = bool(attack_metadata.get("critical_fail", false))
	merged["ordinary_miss"] = bool(attack_metadata.get("ordinary_miss", false))
	merged["critical_source"] = _resolve_critical_source(attack_metadata)
	merged["is_disadvantage"] = bool(attack_metadata.get("is_disadvantage", false))
	merged["hidden_luck_at_birth"] = int(attack_metadata.get("hidden_luck_at_birth", 0))
	merged["faith_luck_bonus"] = int(attack_metadata.get("faith_luck_bonus", 0))
	merged["effective_luck"] = int(attack_metadata.get("effective_luck", 0))
	merged["crit_locked"] = bool(attack_metadata.get("crit_locked", false))
	merged["crit_gate_die"] = int(attack_metadata.get("crit_gate_die", 0))
	merged["crit_gate_roll"] = int(attack_metadata.get("crit_gate_roll", 0))
	merged["hit_roll"] = int(attack_metadata.get("hit_roll", 0))
	merged["fumble_low_end"] = int(attack_metadata.get("fumble_low_end", 0))
	merged["crit_threshold"] = int(attack_metadata.get("crit_threshold", 0))
	merged["required_roll"] = int(attack_metadata.get("required_roll", ATTACK_CHECK_TARGET))
	merged["display_required_roll"] = int(attack_metadata.get("display_required_roll", 0))
	merged["hit_rate_percent"] = int(attack_metadata.get("hit_rate_percent", 0))
	merged["reverse_fate_downgraded"] = bool(attack_metadata.get("reverse_fate_downgraded", false))
	merged["fate_event_tags"] = ProgressionDataUtils.string_name_array_to_string_array(
		_build_attack_event_tags(attack_metadata)
	)
	return merged


func get_fate_event_bus() -> BattleFateEventBus:
	return _fate_event_bus


func _attach_attack_report_entry(result: Dictionary, source_unit: BattleUnitState, target_unit: BattleUnitState) -> void:
	if result.is_empty() or _report_formatter == null:
		return
	var report_entry := _report_formatter.build_attack_report_entry(source_unit, target_unit, result)
	if report_entry.is_empty():
		return
	result["report_entry"] = report_entry.duplicate(true)


func _dispatch_attack_resolution_events(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	attack_metadata: Dictionary,
	attack_context: Dictionary = {}
) -> void:
	if _fate_event_bus == null or attack_metadata.is_empty():
		return
	var payload := _build_attack_event_payload(source_unit, target_unit, attack_metadata, attack_context)
	for event_type in _build_attack_event_tags(attack_metadata):
		_fate_event_bus.dispatch(event_type, payload)


func _build_attack_event_payload(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	attack_metadata: Dictionary,
	attack_context: Dictionary = {}
) -> Dictionary:
	var battle_state := attack_context.get("battle_state", null) as BattleState
	return {
		"battle_id": battle_state.battle_id if battle_state != null else &"",
		"attacker_id": source_unit.unit_id if source_unit != null else &"",
		"attacker_member_id": source_unit.source_member_id if source_unit != null else &"",
		"attacker_low_hp_hardship": _is_low_hp_hardship(source_unit),
		"attacker_strong_attack_debuff_ids": _get_strong_attack_debuff_ids(source_unit),
		"defender_id": target_unit.unit_id if target_unit != null else &"",
		"defender_member_id": target_unit.source_member_id if target_unit != null else &"",
		"defender_is_elite_or_boss": _is_elite_or_boss(target_unit),
		"attack_resolution": ProgressionDataUtils.to_string_name(attack_metadata.get("attack_resolution", "")),
		"critical_source": _resolve_critical_source(attack_metadata),
		"is_disadvantage": bool(attack_metadata.get("is_disadvantage", false)),
		"crit_gate_die": int(attack_metadata.get("crit_gate_die", 0)),
		"crit_gate_roll": int(attack_metadata.get("crit_gate_roll", 0)),
		"hit_roll": int(attack_metadata.get("hit_roll", 0)),
		"luck_snapshot": _build_attack_luck_snapshot(attack_metadata),
	}


func _build_attack_luck_snapshot(attack_metadata: Dictionary) -> Dictionary:
	return {
		"hidden_luck_at_birth": int(attack_metadata.get("hidden_luck_at_birth", 0)),
		"faith_luck_bonus": int(attack_metadata.get("faith_luck_bonus", 0)),
		"effective_luck": int(attack_metadata.get("effective_luck", 0)),
		"fumble_low_end": int(attack_metadata.get("fumble_low_end", 0)),
		"crit_threshold": int(attack_metadata.get("crit_threshold", 0)),
	}


func _resolve_critical_source(attack_metadata: Dictionary) -> StringName:
	if not bool(attack_metadata.get("critical_hit", false)):
		return &""
	return &"high_threat" if _is_high_threat_critical_hit(attack_metadata) else &"gate_die"


func _is_high_threat_critical_hit(attack_metadata: Dictionary) -> bool:
	return bool(attack_metadata.get("critical_hit", false)) \
		and int(attack_metadata.get("crit_gate_die", 0)) == NATURAL_HIT_ROLL


func _is_low_hp_hardship(unit_state: BattleUnitState) -> bool:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return false
	var max_hp := maxi(int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)), 0)
	if max_hp <= 0:
		return false
	return int(unit_state.current_hp) * 100 <= max_hp * int(BattleState.LOW_HP_ATTACK_DISADVANTAGE_PERCENT)


func _get_strong_attack_debuff_ids(unit_state: BattleUnitState) -> Array[StringName]:
	var strong_status_ids: Array[StringName] = []
	if unit_state == null:
		return strong_status_ids
	for status_key in ProgressionDataUtils.sorted_string_keys(BattleState.STRONG_ATTACK_DISADVANTAGE_STATUS_IDS):
		var status_id := StringName(status_key)
		if status_id == &"" or not unit_state.has_status_effect(status_id):
			continue
		strong_status_ids.append(status_id)
	return strong_status_ids


func _is_elite_or_boss(unit_state: BattleUnitState) -> bool:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return false
	return int(unit_state.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 0


func _get_hidden_luck_at_birth(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return int(unit_state.attribute_snapshot.get_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH))


func _get_faith_luck_bonus(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return int(unit_state.attribute_snapshot.get_value(UNIT_BASE_ATTRIBUTES_SCRIPT.FAITH_LUCK_BONUS))


func _get_effective_luck(unit_state: BattleUnitState) -> int:
	return clampi(
		_get_hidden_luck_at_birth(unit_state) + _get_faith_luck_bonus(unit_state),
		UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MIN,
		UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MAX
	)


func _resolve_damage_outcome(source_unit: BattleUnitState, target_unit: BattleUnitState, effect_def) -> Dictionary:
	var base_damage := maxi(int(effect_def.power) if effect_def != null else 0, 0)
	var offense_multiplier := _build_offense_multiplier(source_unit, target_unit, effect_def)
	var rolled_damage := maxi(int(round(float(base_damage) * offense_multiplier)), 0)
	var damage_tag := _resolve_damage_tag(effect_def)
	var mitigation_tier := _resolve_mitigation_tier(target_unit, damage_tag)
	var tier_adjusted_damage := rolled_damage
	match mitigation_tier:
		MITIGATION_TIER_IMMUNE:
			tier_adjusted_damage = 0
		MITIGATION_TIER_HALF:
			tier_adjusted_damage = int(floor(float(tier_adjusted_damage) / 2.0))
		MITIGATION_TIER_DOUBLE:
			tier_adjusted_damage *= 2

	var mitigation := _build_fixed_mitigation(target_unit, effect_def, damage_tag)
	_apply_black_star_brand_guard_ignore(mitigation, target_unit)
	_apply_low_luck_black_star_wedge_guard_ignore(mitigation, source_unit)
	var typed_resistance := int(mitigation.get("typed_resistance", 0))
	var buff_reduction := int(mitigation.get("buff_reduction", 0))
	var stance_reduction := int(mitigation.get("stance_reduction", 0))
	var content_dr := int(mitigation.get("content_dr", 0))
	var guard_block := int(mitigation.get("guard_block", 0))
	var guard_ignore_applied := int(mitigation.get("guard_ignore_applied", 0))
	var fixed_mitigation_total := typed_resistance + buff_reduction + stance_reduction + content_dr + guard_block
	var final_damage := tier_adjusted_damage - fixed_mitigation_total
	var resolved_damage := maxi(final_damage, MIN_DAMAGE_FLOOR)
	return {
		"damage_tag": damage_tag,
		"mitigation_tier": mitigation_tier,
		"base_damage": base_damage,
		"offense_multiplier": offense_multiplier,
		"rolled_damage": rolled_damage,
		"tier_adjusted_damage": tier_adjusted_damage,
		"resolved_damage": resolved_damage,
		"typed_resistance": typed_resistance,
		"buff_reduction": buff_reduction,
		"stance_reduction": stance_reduction,
		"content_dr": content_dr,
		"guard_block": guard_block,
		"guard_ignore_applied": guard_ignore_applied,
		"low_luck_black_star_wedge_triggered": bool(mitigation.get("low_luck_black_star_wedge_triggered", false)),
		"fixed_mitigation_total": fixed_mitigation_total,
		"fully_absorbed_by_mitigation": resolved_damage <= 0 \
			and mitigation_tier != MITIGATION_TIER_IMMUNE \
			and tier_adjusted_damage > 0,
	}


func _build_offense_multiplier(source_unit: BattleUnitState, target_unit: BattleUnitState, effect_def) -> float:
	var multiplier := _get_pre_resistance_damage_multiplier(effect_def)
	if _has_bonus_condition(effect_def, target_unit):
		multiplier *= _get_damage_ratio_multiplier(effect_def)
	if _has_status_effect(source_unit, STATUS_ATTACK_UP):
		var attack_up_strength := _get_status_strength(source_unit, STATUS_ATTACK_UP)
		multiplier *= 1.0 + 0.10 * float(attack_up_strength)
	if source_unit != null and source_unit.has_status_effect(STATUS_ARCHER_PRE_AIM):
		multiplier *= 1.15
	if target_unit != null and target_unit.has_status_effect(STATUS_ARMOR_BREAK):
		multiplier *= 1.15
	if target_unit != null and target_unit.has_status_effect(STATUS_MARKED):
		multiplier *= 1.10
	multiplier *= _get_low_luck_blood_debt_multiplier(target_unit)
	multiplier *= _get_source_outgoing_damage_multiplier(source_unit)
	multiplier *= _get_target_incoming_damage_multiplier(target_unit)
	return maxf(multiplier, 0.0)


func _resolve_damage_tag(effect_def) -> StringName:
	var resistance_attribute_id: StringName = effect_def.resistance_attribute_id if effect_def != null else &""
	match resistance_attribute_id:
		ATTRIBUTE_SERVICE_SCRIPT.FIRE_RESISTANCE:
			return DAMAGE_TAG_FIRE
		ATTRIBUTE_SERVICE_SCRIPT.FREEZE_RESISTANCE:
			return DAMAGE_TAG_FREEZE
		ATTRIBUTE_SERVICE_SCRIPT.LIGHTNING_RESISTANCE:
			return DAMAGE_TAG_LIGHTNING
		ATTRIBUTE_SERVICE_SCRIPT.NEGATIVE_ENERGY_RESISTANCE:
			return DAMAGE_TAG_NEGATIVE_ENERGY
	var explicit_effect_tag := ProgressionDataUtils.to_string_name(effect_def.damage_tag if effect_def != null else &"")
	if explicit_effect_tag != &"":
		return explicit_effect_tag
	if effect_def != null and effect_def.params != null:
		var explicit_damage_tag := ProgressionDataUtils.to_string_name(effect_def.params.get("damage_tag", ""))
		if explicit_damage_tag != &"":
			return explicit_damage_tag
	return DAMAGE_TAG_PHYSICAL_SLASH


func _resolve_mitigation_tier(target_unit: BattleUnitState, damage_tag: StringName) -> StringName:
	if target_unit == null:
		return MITIGATION_TIER_NORMAL
	var has_half := false
	var has_double := false
	for status_id_variant in target_unit.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = target_unit.get_status_effect(status_id)
		if status_entry == null or status_entry.params == null:
			continue
		if not _status_params_apply_to_damage_tag(status_entry.params, damage_tag):
			continue
		var mitigation_tier := ProgressionDataUtils.to_string_name(status_entry.params.get("mitigation_tier", ""))
		match mitigation_tier:
			MITIGATION_TIER_IMMUNE:
				return MITIGATION_TIER_IMMUNE
			MITIGATION_TIER_HALF:
				has_half = true
			MITIGATION_TIER_DOUBLE:
				has_double = true
	if has_half and has_double:
		return MITIGATION_TIER_NORMAL
	if has_half:
		return MITIGATION_TIER_HALF
	if has_double:
		return MITIGATION_TIER_DOUBLE
	return MITIGATION_TIER_NORMAL


func _status_params_apply_to_damage_tag(params: Dictionary, damage_tag: StringName) -> bool:
	if params == null or damage_tag == &"":
		return true
	var explicit_damage_tag := ProgressionDataUtils.to_string_name(params.get("damage_tag", params.get("tag", "")))
	if explicit_damage_tag != &"":
		return explicit_damage_tag == damage_tag
	var damage_tags_variant = params.get("damage_tags", [])
	if damage_tags_variant is Array and not (damage_tags_variant as Array).is_empty():
		for tag_variant in damage_tags_variant:
			if ProgressionDataUtils.to_string_name(tag_variant) == damage_tag:
				return true
		return false
	var damage_category := ProgressionDataUtils.to_string_name(params.get("damage_category", ""))
	match damage_category:
		&"physical":
			return _is_physical_damage_tag(damage_tag)
		&"spell", &"magic", &"energy":
			return not _is_physical_damage_tag(damage_tag)
	return true


func _is_physical_damage_tag(damage_tag: StringName) -> bool:
	return damage_tag == DAMAGE_TAG_PHYSICAL_SLASH \
		or damage_tag == DAMAGE_TAG_PHYSICAL_PIERCE \
		or damage_tag == DAMAGE_TAG_PHYSICAL_BLUNT


func _build_fixed_mitigation(target_unit: BattleUnitState, effect_def, damage_tag: StringName) -> Dictionary:
	return {
		"typed_resistance": _resolve_typed_resistance(target_unit, effect_def),
		"buff_reduction": _resolve_buff_reduction(target_unit),
		"stance_reduction": _resolve_stance_reduction(target_unit, damage_tag),
		"content_dr": _resolve_content_dr(target_unit, effect_def, damage_tag),
		"guard_block": _resolve_guard_block(target_unit, damage_tag),
		"guard_ignore_applied": 0,
	}


func _apply_black_star_brand_guard_ignore(mitigation: Dictionary, target_unit: BattleUnitState) -> void:
	if mitigation == null or target_unit == null:
		return
	if not target_unit.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW):
		return
	var remaining_ignore := BLACK_STAR_BRAND_GUARD_IGNORE_FLAT
	if remaining_ignore <= 0:
		return
	var ignored_total := 0
	var guard_block := maxi(int(mitigation.get("guard_block", 0)), 0)
	if guard_block > 0:
		var guard_ignored := mini(guard_block, remaining_ignore)
		guard_block -= guard_ignored
		remaining_ignore -= guard_ignored
		ignored_total += guard_ignored
		mitigation["guard_block"] = guard_block
	if remaining_ignore > 0:
		var stance_reduction := maxi(int(mitigation.get("stance_reduction", 0)), 0)
		if stance_reduction > 0:
			var stance_ignored := mini(stance_reduction, remaining_ignore)
			stance_reduction -= stance_ignored
			remaining_ignore -= stance_ignored
			ignored_total += stance_ignored
			mitigation["stance_reduction"] = stance_reduction
	mitigation["guard_ignore_applied"] = ignored_total
	target_unit.erase_status_effect(STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW)


func _resolve_typed_resistance(target_unit: BattleUnitState, effect_def) -> int:
	if target_unit == null or target_unit.attribute_snapshot == null or effect_def == null:
		return 0
	var resistance_attribute_id: StringName = effect_def.resistance_attribute_id
	if resistance_attribute_id == &"":
		return 0
	return maxi(int(target_unit.attribute_snapshot.get_value(resistance_attribute_id)), 0)


func _resolve_buff_reduction(target_unit: BattleUnitState) -> int:
	if not _has_status_effect(target_unit, STATUS_DAMAGE_REDUCTION_UP):
		return 0
	var damage_reduction_strength := _get_status_strength(target_unit, STATUS_DAMAGE_REDUCTION_UP)
	return maxi(damage_reduction_strength, 0) * DAMAGE_REDUCTION_UP_FIXED_PER_POWER


func _resolve_stance_reduction(target_unit: BattleUnitState, damage_tag: StringName) -> int:
	if not _is_physical_damage_tag(damage_tag):
		return 0
	if not _has_status_effect(target_unit, STATUS_GUARDING):
		return 0
	var guarding_strength := _get_status_strength(target_unit, STATUS_GUARDING)
	return maxi(guarding_strength, 0) * GUARDING_PHYSICAL_REDUCTION_PER_POWER


func _resolve_content_dr(target_unit: BattleUnitState, effect_def, damage_tag: StringName) -> int:
	if target_unit == null or not _is_physical_damage_tag(damage_tag):
		return 0
	var max_content_dr := 0
	for status_id_variant in target_unit.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = target_unit.get_status_effect(status_id)
		if status_entry == null or status_entry.params == null:
			continue
		if not _status_params_apply_to_damage_tag(status_entry.params, damage_tag):
			continue
		var content_dr := maxi(int(status_entry.params.get("content_dr", 0)), 0)
		if content_dr <= 0:
			continue
		var bypass_tag := ProgressionDataUtils.to_string_name(status_entry.params.get("dr_bypass_tag", status_entry.params.get("bypass_tag", "")))
		if bypass_tag != &"" and _effect_has_bypass_tag(effect_def, bypass_tag):
			continue
		max_content_dr = maxi(max_content_dr, content_dr)
	return max_content_dr


func _resolve_guard_block(target_unit: BattleUnitState, damage_tag: StringName) -> int:
	if target_unit == null:
		return 0
	var max_guard_block := 0
	for status_id_variant in target_unit.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = target_unit.get_status_effect(status_id)
		if status_entry == null or status_entry.params == null:
			continue
		if not _status_params_apply_to_damage_tag(status_entry.params, damage_tag):
			continue
		max_guard_block = maxi(max_guard_block, maxi(int(status_entry.params.get("guard_block", 0)), 0))
	return max_guard_block


func _is_attack_crit_locked(unit_state: BattleUnitState) -> bool:
	return _fate_attack_rules.is_attack_crit_locked(unit_state)


func _effect_has_bypass_tag(effect_def, bypass_tag: StringName) -> bool:
	if effect_def == null or effect_def.params == null or bypass_tag == &"":
		return false
	var explicit_bypass_tag := ProgressionDataUtils.to_string_name(effect_def.params.get("bypass_tag", ""))
	if explicit_bypass_tag == bypass_tag:
		return true
	var bypass_tags_variant = effect_def.params.get("bypass_tags", [])
	if bypass_tags_variant is not Array:
		return false
	for tag_variant in bypass_tags_variant:
		if ProgressionDataUtils.to_string_name(tag_variant) == bypass_tag:
			return true
	return false


func _has_bonus_condition(effect_def, target_unit: BattleUnitState) -> bool:
	if effect_def == null or target_unit == null:
		return false
	match effect_def.bonus_condition:
		BONUS_CONDITION_TARGET_LOW_HP:
			return _is_target_low_hp(effect_def, target_unit)
		_:
			return false


func _is_target_low_hp(effect_def, target_unit: BattleUnitState) -> bool:
	var max_hp := 0
	if target_unit.attribute_snapshot != null:
		max_hp = target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)
	if max_hp <= 0:
		max_hp = maxi(target_unit.current_hp, 1)

	var threshold_ratio := 0.5
	if effect_def != null and effect_def.params != null:
		if effect_def.params.has("hp_ratio_threshold"):
			threshold_ratio = clampf(float(effect_def.params.get("hp_ratio_threshold", threshold_ratio)), 0.0, 1.0)
		elif effect_def.params.has("low_hp_ratio"):
			threshold_ratio = clampf(float(effect_def.params.get("low_hp_ratio", threshold_ratio)), 0.0, 1.0)

	return float(target_unit.current_hp) <= float(max_hp) * threshold_ratio


func _get_damage_ratio_multiplier(effect_def) -> float:
	if effect_def == null:
		return 1.0
	return maxf(float(effect_def.damage_ratio_percent) / 100.0, 0.0)


func _get_pre_resistance_damage_multiplier(effect_def) -> float:
	if effect_def == null or effect_def.params == null:
		return 1.0
	return maxf(float(effect_def.params.get("runtime_pre_resistance_damage_multiplier", 1.0)), 0.0)


func _build_empty_result() -> Dictionary:
	return {
		"applied": false,
		"damage": 0,
		"hp_damage": 0,
		"healing": 0,
		"shield_absorbed": 0,
		"shield_broken": false,
		"damage_events": [],
		"status_effect_ids": [],
		"source_status_effect_ids": [],
		"terrain_effect_ids": [],
		"height_delta": 0,
	}


func _build_attack_event_tags(attack_metadata: Dictionary) -> Array[StringName]:
	var event_tags: Array[StringName] = []
	if bool(attack_metadata.get("critical_fail", false)):
		event_tags.append(BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_FAIL)
	if _is_high_threat_critical_hit(attack_metadata):
		event_tags.append(BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_HIGH_THREAT_CRITICAL_HIT)
	if bool(attack_metadata.get("critical_hit", false)) and bool(attack_metadata.get("is_disadvantage", false)):
		event_tags.append(BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE)
	if bool(attack_metadata.get("ordinary_miss", false)):
		event_tags.append(BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_ORDINARY_MISS)
	if bool(attack_metadata.get("attack_success", false)) \
		and bool(attack_metadata.get("is_disadvantage", false)) \
		and not bool(attack_metadata.get("critical_hit", false)):
		event_tags.append(BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_HARDSHIP_SURVIVAL)
	return event_tags


func resolve_fall_damage(target_unit: BattleUnitState, fall_layers: int) -> Dictionary:
	if target_unit == null or fall_layers <= 0 or not target_unit.is_alive:
		return _build_empty_result()

	var max_hp := 0
	if target_unit.attribute_snapshot != null:
		max_hp = target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)
	if max_hp <= 0:
		max_hp = maxi(target_unit.current_hp, 1)

	var damage_per_layer := maxi(int(ceil(float(max_hp) * 0.05)), 1)
	var total_damage := damage_per_layer * fall_layers
	var damage_result := _apply_damage_to_target(target_unit, total_damage)
	target_unit.is_alive = target_unit.current_hp > 0
	return _build_environmental_damage_result(damage_result)


func resolve_collision_damage(target_unit: BattleUnitState, source_body_size: int, target_body_size: int) -> Dictionary:
	if target_unit == null or not target_unit.is_alive:
		return _build_empty_result()

	var size_gap := maxi(source_body_size - target_body_size, 0)
	var total_damage := 10 + size_gap * 10
	var damage_result := _apply_damage_to_target(target_unit, total_damage)
	target_unit.is_alive = target_unit.current_hp > 0
	return _build_environmental_damage_result(damage_result)


func _build_environmental_damage_result(damage_result: Dictionary) -> Dictionary:
	var result := _build_empty_result()
	result["applied"] = int(damage_result.get("damage", 0)) > 0 or int(damage_result.get("shield_absorbed", 0)) > 0
	result["damage"] = int(damage_result.get("damage", 0))
	result["hp_damage"] = int(damage_result.get("hp_damage", result["damage"]))
	result["shield_absorbed"] = int(damage_result.get("shield_absorbed", 0))
	result["shield_broken"] = bool(damage_result.get("shield_broken", false))
	result["damage_events"] = [damage_result.duplicate(true)]
	return result


func _apply_damage_to_target(target_unit: BattleUnitState, resolved_damage_input) -> Dictionary:
	var damage_outcome := _coerce_damage_outcome(resolved_damage_input)
	var normalized_damage := maxi(int(damage_outcome.get("resolved_damage", 0)), 0)
	if target_unit == null or normalized_damage <= 0:
		return _build_applied_damage_result(damage_outcome, 0, 0, false)

	target_unit.normalize_shield_state()

	var shield_absorbed := 0
	var shield_broken := false
	if target_unit.has_shield():
		shield_absorbed = mini(normalized_damage, target_unit.current_shield_hp)
		target_unit.current_shield_hp = maxi(target_unit.current_shield_hp - shield_absorbed, 0)
		if target_unit.current_shield_hp <= 0:
			shield_broken = shield_absorbed > 0
			target_unit.clear_shield()
		else:
			target_unit.normalize_shield_state()

	var hp_damage := maxi(normalized_damage - shield_absorbed, 0)
	if hp_damage > 0:
		target_unit.current_hp = maxi(target_unit.current_hp - hp_damage, 0)

	return _build_applied_damage_result(damage_outcome, hp_damage, shield_absorbed, shield_broken)


func _coerce_damage_outcome(resolved_damage_input) -> Dictionary:
	if resolved_damage_input is Dictionary:
		var outcome := (resolved_damage_input as Dictionary).duplicate(true)
		if not outcome.has("resolved_damage"):
			outcome["resolved_damage"] = maxi(int(outcome.get("damage", 0)), 0)
		return outcome
	var normalized_damage := maxi(int(resolved_damage_input), 0)
	return {
		"damage_tag": &"",
		"mitigation_tier": MITIGATION_TIER_NORMAL,
		"base_damage": normalized_damage,
		"offense_multiplier": 1.0,
		"rolled_damage": normalized_damage,
		"tier_adjusted_damage": normalized_damage,
		"resolved_damage": normalized_damage,
		"typed_resistance": 0,
		"buff_reduction": 0,
		"stance_reduction": 0,
		"content_dr": 0,
		"guard_block": 0,
		"fixed_mitigation_total": 0,
		"fully_absorbed_by_mitigation": false,
	}


func _build_applied_damage_result(
	damage_outcome: Dictionary,
	hp_damage: int,
	shield_absorbed: int,
	shield_broken: bool
) -> Dictionary:
	var result := damage_outcome.duplicate(true)
	result["damage"] = hp_damage
	result["hp_damage"] = hp_damage
	result["shield_absorbed"] = shield_absorbed
	result["shield_broken"] = shield_broken
	result["fully_absorbed_by_shield"] = hp_damage <= 0 and shield_absorbed > 0
	return result


func _apply_status_effect(target_unit: BattleUnitState, source_unit: BattleUnitState, effect_def) -> void:
	if target_unit == null or effect_def == null or effect_def.status_id == &"":
		return

	if _is_crown_break_seal_status(effect_def.status_id):
		_clear_other_crown_break_seals(target_unit, effect_def.status_id)
	var status_entry = BATTLE_STATUS_SEMANTIC_TABLE_SCRIPT.merge_status(
		effect_def,
		source_unit.unit_id if source_unit != null else &"",
		target_unit.get_status_effect(effect_def.status_id)
	)
	if status_entry != null:
		target_unit.set_status_effect(status_entry)


func _is_crown_break_seal_status(status_id: StringName) -> bool:
	return status_id == STATUS_CROWN_BREAK_BROKEN_FANG \
		or status_id == STATUS_CROWN_BREAK_BROKEN_HAND \
		or status_id == STATUS_CROWN_BREAK_BLINDED_EYE


func _clear_other_crown_break_seals(target_unit: BattleUnitState, kept_status_id: StringName) -> void:
	if target_unit == null:
		return
	var seal_status_ids: Array[StringName] = [
		STATUS_CROWN_BREAK_BROKEN_FANG,
		STATUS_CROWN_BREAK_BROKEN_HAND,
		STATUS_CROWN_BREAK_BLINDED_EYE,
	]
	for seal_status_id in seal_status_ids:
		if seal_status_id == kept_status_id:
			continue
		target_unit.erase_status_effect(seal_status_id)


func _has_status_effect(unit_state: BattleUnitState, status_id: StringName) -> bool:
	return unit_state != null and unit_state.has_status_effect(status_id)


func _get_status_strength(unit_state: BattleUnitState, status_id: StringName) -> int:
	if unit_state == null:
		return 0
	var status_entry = unit_state.get_status_effect(status_id)
	if status_entry == null:
		return 0
	return maxi(int(status_entry.power), 1)


func _get_target_incoming_damage_multiplier(target_unit: BattleUnitState) -> float:
	if target_unit == null:
		return 1.0
	var multiplier := 1.0
	for status_id_variant in target_unit.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = target_unit.get_status_effect(status_id)
		if status_entry == null or status_entry.params == null:
			continue
		var params: Dictionary = status_entry.params
		var status_multiplier := float(params.get(
			"incoming_damage_multiplier",
			params.get(&"incoming_damage_multiplier", 1.0)
		))
		if status_multiplier > multiplier:
			multiplier = status_multiplier
	return maxf(multiplier, 1.0)


func _get_source_outgoing_damage_multiplier(source_unit: BattleUnitState) -> float:
	if source_unit == null:
		return 1.0
	var multiplier := 1.0
	for status_id_variant in source_unit.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = source_unit.get_status_effect(status_id)
		if status_entry == null or status_entry.params == null:
			continue
		var params: Dictionary = status_entry.params
		var status_multiplier := float(params.get(
			"outgoing_damage_multiplier",
			params.get(&"outgoing_damage_multiplier", 1.0)
		))
		if status_multiplier <= 0.0:
			continue
		multiplier *= status_multiplier
	return maxf(multiplier, 0.0)


func _get_low_luck_blood_debt_multiplier(target_unit: BattleUnitState) -> float:
	if not LOW_LUCK_RELIC_RULES_SCRIPT.unit_has_flag(target_unit, LOW_LUCK_RELIC_RULES_SCRIPT.ATTR_BLOOD_DEBT_SHAWL):
		return 1.0
	if not _is_unit_below_hp_ratio(target_unit, LOW_LUCK_RELIC_RULES_SCRIPT.BLOOD_DEBT_LOW_HP_THRESHOLD_RATIO):
		return 1.0
	return LOW_LUCK_RELIC_RULES_SCRIPT.BLOOD_DEBT_DAMAGE_MULTIPLIER


func _unit_has_status_bool_param(unit_state: BattleUnitState, param_key: StringName) -> bool:
	if unit_state == null or param_key == &"":
		return false
	for status_id_variant in unit_state.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = unit_state.get_status_effect(status_id)
		if status_entry == null or status_entry.params == null:
			continue
		var params: Dictionary = status_entry.params
		if bool(params.get(String(param_key), params.get(param_key, false))):
			return true
	return false


func _try_apply_reverse_fate_amulet(source_unit: BattleUnitState) -> bool:
	if source_unit == null:
		return false
	if not LOW_LUCK_RELIC_RULES_SCRIPT.unit_has_flag(source_unit, LOW_LUCK_RELIC_RULES_SCRIPT.ATTR_REVERSE_FATE_AMULET):
		return false
	if bool(source_unit.ai_blackboard.get(LOW_LUCK_RELIC_RULES_SCRIPT.BATTLE_FLAG_REVERSE_FATE_USED, false)):
		return false
	source_unit.ai_blackboard[LOW_LUCK_RELIC_RULES_SCRIPT.BATTLE_FLAG_REVERSE_FATE_USED] = true
	_apply_runtime_status(
		source_unit,
		LOW_LUCK_RELIC_RULES_SCRIPT.STATUS_REVERSE_FATE_WEAKENED,
		LOW_LUCK_RELIC_RULES_SCRIPT.REVERSE_FATE_DURATION_TU,
		{
			"outgoing_damage_multiplier": LOW_LUCK_RELIC_RULES_SCRIPT.REVERSE_FATE_DAMAGE_MULTIPLIER,
			"counts_as_debuff": true,
		}
	)
	return true


func _apply_low_luck_black_star_wedge_guard_ignore(mitigation: Dictionary, source_unit: BattleUnitState) -> void:
	if mitigation == null or source_unit == null:
		return
	if not LOW_LUCK_RELIC_RULES_SCRIPT.unit_has_flag(source_unit, LOW_LUCK_RELIC_RULES_SCRIPT.ATTR_BLACK_STAR_WEDGE):
		return
	if bool(source_unit.ai_blackboard.get(LOW_LUCK_RELIC_RULES_SCRIPT.BATTLE_FLAG_BLACK_STAR_WEDGE_USED, false)):
		return
	source_unit.ai_blackboard[LOW_LUCK_RELIC_RULES_SCRIPT.BATTLE_FLAG_BLACK_STAR_WEDGE_USED] = true
	var remaining_ignore := LOW_LUCK_RELIC_RULES_SCRIPT.BLACK_STAR_WEDGE_GUARD_IGNORE_FLAT
	var ignored_total := 0
	var guard_block := maxi(int(mitigation.get("guard_block", 0)), 0)
	if guard_block > 0:
		var guard_ignored := mini(guard_block, remaining_ignore)
		guard_block -= guard_ignored
		remaining_ignore -= guard_ignored
		ignored_total += guard_ignored
		mitigation["guard_block"] = guard_block
	if remaining_ignore > 0:
		var stance_reduction := maxi(int(mitigation.get("stance_reduction", 0)), 0)
		if stance_reduction > 0:
			var stance_ignored := mini(stance_reduction, remaining_ignore)
			stance_reduction -= stance_ignored
			remaining_ignore -= stance_ignored
			ignored_total += stance_ignored
			mitigation["stance_reduction"] = stance_reduction
	mitigation["guard_ignore_applied"] = int(mitigation.get("guard_ignore_applied", 0)) + ignored_total
	mitigation["low_luck_black_star_wedge_triggered"] = true


func _apply_low_luck_black_star_wedge_exposed(source_unit: BattleUnitState) -> bool:
	if source_unit == null:
		return false
	_apply_runtime_status(
		source_unit,
		LOW_LUCK_RELIC_RULES_SCRIPT.STATUS_BLACK_STAR_WEDGE_EXPOSED,
		LOW_LUCK_RELIC_RULES_SCRIPT.BLACK_STAR_WEDGE_EXPOSED_DURATION_TU,
		{
			"incoming_damage_multiplier": LOW_LUCK_RELIC_RULES_SCRIPT.BLACK_STAR_WEDGE_EXPOSED_INCOMING_DAMAGE_MULTIPLIER,
			"counts_as_debuff": true,
		}
	)
	return true


func _apply_runtime_status(
	unit_state: BattleUnitState,
	status_id: StringName,
	duration_tu: int,
	params: Dictionary = {},
	source_unit_id: StringName = &""
) -> void:
	if unit_state == null or status_id == &"":
		return
	var status_entry := BattleStatusEffectState.new()
	status_entry.status_id = status_id
	status_entry.source_unit_id = source_unit_id
	status_entry.power = 1
	status_entry.stacks = 1
	status_entry.duration = maxi(duration_tu, -1)
	status_entry.params = params.duplicate(true)
	unit_state.set_status_effect(status_entry)


func _is_unit_below_hp_ratio(unit_state: BattleUnitState, threshold_ratio: float) -> bool:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return false
	var max_hp := maxi(int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)), 0)
	if max_hp <= 0:
		return false
	return float(unit_state.current_hp) <= float(max_hp) * clampf(threshold_ratio, 0.0, 1.0)
