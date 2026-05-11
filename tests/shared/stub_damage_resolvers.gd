extends RefCounted

const BattleDamageResolver = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")


class FixedRollDamageResolver extends BattleDamageResolver:
	var damage_rolls: Array[int] = []
	var attack_rolls: Array[int] = []

	func _init(p_damage_rolls: Array = [], p_attack_rolls: Array = []) -> void:
		damage_rolls.clear()
		for roll in p_damage_rolls:
			damage_rolls.append(int(roll))
		attack_rolls.clear()
		for roll in p_attack_rolls:
			attack_rolls.append(int(roll))

	func _roll_damage_die(dice_sides: int) -> int:
		var normalized_sides := maxi(dice_sides, 1)
		if damage_rolls.is_empty():
			return normalized_sides
		return clampi(int(damage_rolls.pop_front()), 1, normalized_sides)

	func _roll_true_random_attack_range(min_value: int, max_value: int, battle_state) -> int:
		var lower := mini(min_value, max_value)
		var upper := maxi(min_value, max_value)
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		if attack_rolls.is_empty():
			return upper
		return clampi(int(attack_rolls.pop_front()), lower, upper)


class FixedFailedSaveDamageResolver extends FixedRollDamageResolver:
	func _init(p_damage_rolls: Array = [], p_attack_rolls: Array = []) -> void:
		super(p_damage_rolls, p_attack_rolls)

	func resolve_effects(
		source_unit: BattleUnitState,
		target_unit: BattleUnitState,
		effect_defs: Variant,
		damage_context: Dictionary = {}
	) -> Dictionary:
		var fixed_context := damage_context.duplicate(true)
		fixed_context["save_roll_override"] = 1
		return super.resolve_effects(source_unit, target_unit, effect_defs, fixed_context)


class FixedHitMaxDamageResolver extends BattleDamageResolver:
	func _roll_damage_die(dice_sides: int) -> int:
		return maxi(dice_sides, 1)

	func _roll_true_random_attack_range(min_value: int, max_value: int, battle_state: BattleState) -> int:
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		return clampi(10, mini(min_value, max_value), maxi(min_value, max_value))


class FixedHitOneDamageResolver extends BattleDamageResolver:
	func _roll_damage_die(_dice_sides: int) -> int:
		return 1

	func _roll_true_random_attack_range(min_value: int, max_value: int, battle_state: BattleState) -> int:
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		return clampi(10, mini(min_value, max_value), maxi(min_value, max_value))


class FixedMissOneDamageResolver extends BattleDamageResolver:
	func _roll_damage_die(_dice_sides: int) -> int:
		return 1

	func _roll_true_random_attack_range(min_value: int, max_value: int, battle_state: BattleState) -> int:
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		return mini(min_value, max_value)


class FixedSuccessOneDamageResolver extends FixedHitOneDamageResolver:
	func resolve_attack_effects(
		source_unit: BattleUnitState,
		target_unit: BattleUnitState,
		effect_defs: Variant,
		attack_check: Dictionary,
		attack_context: Dictionary = {}
	) -> Dictionary:
		var fixed_context := attack_context.duplicate(true)
		fixed_context["force_hit_no_crit"] = true
		return super.resolve_attack_effects(source_unit, target_unit, effect_defs, attack_check, fixed_context)


class FixedSuccessFailedSecondarySaveOneDamageResolver extends FixedSuccessOneDamageResolver:
	func _roll_true_random_attack_range(min_value: int, max_value: int, battle_state: BattleState) -> int:
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		return mini(min_value, max_value)


class FixedCriticalOneDamageResolver extends FixedHitOneDamageResolver:
	func _roll_true_random_attack_range(min_value: int, max_value: int, battle_state: BattleState) -> int:
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		return maxi(min_value, max_value)


class TrapDamageResolver extends FixedHitMaxDamageResolver:
	var resolve_effects_calls := 0

	func resolve_effects(
		source_unit: BattleUnitState,
		target_unit: BattleUnitState,
		effect_defs: Variant,
		damage_context: Dictionary = {}
	) -> Dictionary:
		resolve_effects_calls += 1
		return super.resolve_effects(source_unit, target_unit, effect_defs, damage_context)
