## 文件说明：该脚本属于战斗伤害解析器相关的解析脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleDamageResolver
extends RefCounted

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle_status_effect_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const STATUS_ATTACK_UP: StringName = &"attack_up"
const STATUS_DAMAGE_REDUCTION_UP: StringName = &"damage_reduction_up"
const STATUS_GUARDING: StringName = &"guarding"
const STATUS_MARKED: StringName = &"marked"
const STATUS_ARMOR_BREAK: StringName = &"armor_break"
const STATUS_ARCHER_PRE_AIM: StringName = &"archer_pre_aim"
const BONUS_CONDITION_TARGET_LOW_HP: StringName = &"target_low_hp"


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
	var status_effect_ids: Array[StringName] = []
	var terrain_effect_ids: Array[StringName] = []
	var total_height_delta := 0
	var applied := false

	for effect_def in effect_defs:
		if effect_def == null:
			continue
		match effect_def.effect_type:
			&"damage":
				var damage := _resolve_damage_amount(source_unit, target_unit, effect_def)
				target_unit.current_hp = maxi(target_unit.current_hp - damage, 0)
				total_damage += damage
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
		"healing": total_healing,
		"status_effect_ids": status_effect_ids,
		"terrain_effect_ids": terrain_effect_ids,
		"height_delta": total_height_delta,
	}


func _resolve_damage_amount(source_unit: BattleUnitState, target_unit: BattleUnitState, effect_def) -> int:
	var attack_attribute_id: StringName = effect_def.scaling_attribute_id if effect_def.scaling_attribute_id != &"" else ATTRIBUTE_SERVICE_SCRIPT.PHYSICAL_ATTACK
	var defense_attribute_id: StringName = effect_def.defense_attribute_id if effect_def.defense_attribute_id != &"" else ATTRIBUTE_SERVICE_SCRIPT.PHYSICAL_DEFENSE
	var resistance_attribute_id: StringName = effect_def.resistance_attribute_id

	var attack_value := 0
	if source_unit.attribute_snapshot != null:
		attack_value = source_unit.attribute_snapshot.get_value(attack_attribute_id)

	var defense_value := 0
	if target_unit.attribute_snapshot != null:
		defense_value = target_unit.attribute_snapshot.get_value(defense_attribute_id)

	var damage := maxi(effect_def.power + int(floor(float(attack_value) * 0.35)) - defense_value, 1)
	damage = maxi(int(round(float(damage) * _get_pre_resistance_damage_multiplier(effect_def))), 1)
	if _has_bonus_condition(effect_def, target_unit):
		damage = maxi(int(round(float(damage) * _get_damage_ratio_multiplier(effect_def))), 1)
	if _has_status_effect(source_unit, STATUS_ATTACK_UP):
		var attack_up_strength := _get_status_strength(source_unit, STATUS_ATTACK_UP)
		damage = maxi(int(round(float(damage) * (1.0 + 0.10 * float(attack_up_strength)))), 1)
	if source_unit.has_status_effect(STATUS_ARCHER_PRE_AIM):
		damage = maxi(int(round(float(damage) * 1.15)), 1)
	if target_unit.has_status_effect(STATUS_ARMOR_BREAK):
		damage = maxi(int(round(float(damage) * 1.15)), 1)
	if target_unit.has_status_effect(STATUS_MARKED):
		damage = maxi(int(round(float(damage) * 1.10)), 1)
	if target_unit.attribute_snapshot != null and resistance_attribute_id != &"":
		var resistance_value := maxi(target_unit.attribute_snapshot.get_value(resistance_attribute_id), 0)
		var reduction_ratio := clampf(float(resistance_value) / 100.0, 0.0, 0.85)
		damage = maxi(int(round(float(damage) * (1.0 - reduction_ratio))), 1)
	if _has_status_effect(target_unit, STATUS_DAMAGE_REDUCTION_UP):
		var damage_reduction_strength := _get_status_strength(target_unit, STATUS_DAMAGE_REDUCTION_UP)
		var damage_reduction_ratio := clampf(0.10 * float(damage_reduction_strength), 0.0, 0.30)
		damage = maxi(int(round(float(damage) * (1.0 - damage_reduction_ratio))), 1)
	if _has_status_effect(target_unit, STATUS_GUARDING):
		var guarding_strength := _get_status_strength(target_unit, STATUS_GUARDING)
		var guarding_ratio := clampf(0.15 * float(guarding_strength), 0.0, 0.45)
		damage = maxi(int(round(float(damage) * (1.0 - guarding_ratio))), 1)
	return damage


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
		"healing": 0,
		"status_effect_ids": [],
		"terrain_effect_ids": [],
		"height_delta": 0,
	}


func resolve_fall_damage(target_unit: BattleUnitState, fall_layers: int) -> int:
	if target_unit == null or fall_layers <= 0 or not target_unit.is_alive:
		return 0

	var max_hp := 0
	if target_unit.attribute_snapshot != null:
		max_hp = target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)
	if max_hp <= 0:
		max_hp = maxi(target_unit.current_hp, 1)

	var damage_per_layer := maxi(int(ceil(float(max_hp) * 0.05)), 1)
	var total_damage := damage_per_layer * fall_layers
	target_unit.current_hp = maxi(target_unit.current_hp - total_damage, 0)
	target_unit.is_alive = target_unit.current_hp > 0
	return total_damage


func resolve_collision_damage(target_unit: BattleUnitState, source_body_size: int, target_body_size: int) -> int:
	if target_unit == null or not target_unit.is_alive:
		return 0

	var size_gap := maxi(source_body_size - target_body_size, 0)
	var total_damage := 10 + size_gap * 10
	target_unit.current_hp = maxi(target_unit.current_hp - total_damage, 0)
	target_unit.is_alive = target_unit.current_hp > 0
	return total_damage


func _apply_status_effect(target_unit: BattleUnitState, source_unit: BattleUnitState, effect_def) -> void:
	if target_unit == null or effect_def == null or effect_def.status_id == &"":
		return

	var status_entry = target_unit.get_status_effect(effect_def.status_id)
	var previous_stacks := 0
	if status_entry == null:
		status_entry = BATTLE_STATUS_EFFECT_STATE_SCRIPT.new()
		status_entry.status_id = effect_def.status_id
	else:
		previous_stacks = int(status_entry.stacks)

	var params := {}
	if effect_def.params != null:
		params = effect_def.params.duplicate(true)

	status_entry.source_unit_id = source_unit.unit_id if source_unit != null else &""
	status_entry.power = int(effect_def.power)
	status_entry.params = params
	status_entry.stacks = maxi(previous_stacks + 1, 1)
	if effect_def.params != null and effect_def.params.has("duration"):
		status_entry.duration = maxi(int(effect_def.params.get("duration", 1)), 1)
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
