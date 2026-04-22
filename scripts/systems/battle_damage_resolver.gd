## 文件说明：该脚本属于战斗伤害解析器相关的解析脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleDamageResolver
extends RefCounted

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const BATTLE_STATUS_SEMANTIC_TABLE_SCRIPT = preload("res://scripts/systems/battle_status_semantic_table.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
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


func resolve_skill(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef
) -> Dictionary:
	if source_unit == null or target_unit == null or skill_def == null or skill_def.combat_profile == null:
		return _build_empty_result()
	return resolve_effects(source_unit, target_unit, skill_def.combat_profile.effect_defs)


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
	var terrain_effect_ids: Array[StringName] = []
	var total_height_delta := 0
	var shield_broken := false
	var applied := false

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
	return {
		"applied": applied,
		"damage": total_damage,
		"hp_damage": total_damage,
		"healing": total_healing,
		"shield_absorbed": total_shield_absorbed,
		"shield_broken": shield_broken,
		"damage_events": damage_events,
		"status_effect_ids": status_effect_ids,
		"terrain_effect_ids": terrain_effect_ids,
		"height_delta": total_height_delta,
	}


func _resolve_damage_amount(source_unit: BattleUnitState, target_unit: BattleUnitState, effect_def) -> int:
	return int(_resolve_damage_outcome(source_unit, target_unit, effect_def).get("resolved_damage", 0))


func _resolve_damage_outcome(source_unit: BattleUnitState, target_unit: BattleUnitState, effect_def) -> Dictionary:
	var attack_attribute_id: StringName = effect_def.scaling_attribute_id if effect_def.scaling_attribute_id != &"" else ATTRIBUTE_SERVICE_SCRIPT.PHYSICAL_ATTACK
	var defense_attribute_id: StringName = effect_def.defense_attribute_id if effect_def.defense_attribute_id != &"" else ATTRIBUTE_SERVICE_SCRIPT.PHYSICAL_DEFENSE

	var attack_value := 0
	if source_unit.attribute_snapshot != null:
		attack_value = source_unit.attribute_snapshot.get_value(attack_attribute_id)

	var defense_value := 0
	if target_unit.attribute_snapshot != null:
		defense_value = target_unit.attribute_snapshot.get_value(defense_attribute_id)

	var base_damage := maxi(effect_def.power + int(floor(float(attack_value) * 0.35)) - defense_value, 0)
	var offense_multiplier := _build_offense_multiplier(source_unit, target_unit, effect_def)
	var rolled_damage := maxi(int(round(float(base_damage) * offense_multiplier)), 0)
	var damage_tag := _resolve_damage_tag(effect_def, defense_attribute_id)
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
	var typed_resistance := int(mitigation.get("typed_resistance", 0))
	var buff_reduction := int(mitigation.get("buff_reduction", 0))
	var stance_reduction := int(mitigation.get("stance_reduction", 0))
	var content_dr := int(mitigation.get("content_dr", 0))
	var guard_block := int(mitigation.get("guard_block", 0))
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
	return maxf(multiplier, 0.0)


func _resolve_damage_tag(effect_def, defense_attribute_id: StringName) -> StringName:
	if effect_def != null and effect_def.params != null:
		var explicit_damage_tag := ProgressionDataUtils.to_string_name(effect_def.params.get("damage_tag", ""))
		if explicit_damage_tag != &"":
			return explicit_damage_tag
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
	if defense_attribute_id == ATTRIBUTE_SERVICE_SCRIPT.MAGIC_DEFENSE:
		return DAMAGE_TAG_MAGIC
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
	}


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
		"terrain_effect_ids": [],
		"height_delta": 0,
	}


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

	var status_entry = BATTLE_STATUS_SEMANTIC_TABLE_SCRIPT.merge_status(
		effect_def,
		source_unit.unit_id if source_unit != null else &"",
		target_unit.get_status_effect(effect_def.status_id)
	)
	if status_entry != null:
		target_unit.set_status_effect(status_entry)


func _has_status_effect(unit_state: BattleUnitState, status_id: StringName) -> bool:
	return unit_state != null and unit_state.has_status_effect(status_id)


func _get_status_strength(unit_state: BattleUnitState, status_id: StringName) -> int:
	if unit_state == null:
		return 0
	var status_entry = unit_state.get_status_effect(status_id)
	if status_entry == null:
		return 0
	return maxi(int(status_entry.power), 1)
