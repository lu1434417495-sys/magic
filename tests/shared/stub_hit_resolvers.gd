extends RefCounted

const BattleHitResolver = preload("res://scripts/systems/battle/rules/battle_hit_resolver.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")


class FixedHitResolver extends BattleHitResolver:
	var fixed_roll := 10

	func _init(p_fixed_roll: int = 10) -> void:
		fixed_roll = clampi(p_fixed_roll, NATURAL_MISS_ROLL, NATURAL_HIT_ROLL)

	func resolve_attack_metadata(
		_source_unit,
		_target_unit,
		attack_check: Dictionary,
		attack_context: Dictionary
	) -> Dictionary:
		return _build_fixed_attack_metadata(
			attack_check,
			attack_context,
			ATTACK_RESOLUTION_HIT,
			true,
			false,
			false
		)

	func resolve_spell_control_metadata(_source_unit, _attack_context: Dictionary) -> Dictionary:
		var attack_resolution := ATTACK_RESOLUTION_HIT
		var spell_control_resolution: StringName = &"normal"
		var attack_success := true
		var critical_hit := false
		var critical_fail := false
		if fixed_roll <= NATURAL_MISS_ROLL:
			attack_resolution = ATTACK_RESOLUTION_CRITICAL_FAIL
			spell_control_resolution = &"critical_fail"
			attack_success = false
			critical_fail = true
		elif fixed_roll >= NATURAL_HIT_ROLL:
			attack_resolution = ATTACK_RESOLUTION_CRITICAL_HIT
			spell_control_resolution = &"critical_success"
			critical_hit = true
		return {
			"attack_resolution": attack_resolution,
			"spell_control_resolution": spell_control_resolution,
			"attack_success": attack_success,
			"critical_hit": critical_hit,
			"critical_fail": critical_fail,
			"ordinary_miss": false,
			"hit_roll": fixed_roll,
		}

	func roll_attack_check(battle_state: BattleState, attack_check: Dictionary) -> Dictionary:
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		var result := attack_check.duplicate(true)
		result["roll"] = fixed_roll
		result["roll_disposition"] = ROLL_DISPOSITION_THRESHOLD_HIT
		result["success"] = true
		result["hit_rate_percent"] = 100
		result["success_rate_percent"] = 100
		result["preview_text"] = "100%（测试固定命中）"
		result["resolution_text"] = "100%（测试固定命中），d20=" + str(fixed_roll)
		return result

	func roll_hit_rate(battle_state: BattleState, _hit_rate_percent: int) -> Dictionary:
		return roll_attack_check(battle_state, {
			"required_roll": fixed_roll,
			"display_required_roll": fixed_roll,
			"natural_one_auto_miss": false,
			"natural_twenty_auto_hit": false,
		})

	func roll_attack_die(die_size: int, _is_disadvantage: bool, _attack_context: Dictionary) -> int:
		return clampi(fixed_roll, 1, maxi(die_size, 1))

	func _roll_true_random_attack_range(min_value: int, max_value: int, battle_state: BattleState) -> int:
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		return clampi(fixed_roll, mini(min_value, max_value), maxi(min_value, max_value))

	func _build_fixed_attack_metadata(
		attack_check: Dictionary,
		attack_context: Dictionary,
		attack_resolution: StringName,
		attack_success: bool,
		critical_hit: bool,
		ordinary_miss: bool
	) -> Dictionary:
		var required_roll := int(attack_check.get("required_roll", fixed_roll))
		return {
			"attack_resolution": attack_resolution,
			"attack_success": attack_success,
			"critical_hit": critical_hit,
			"critical_fail": false,
			"ordinary_miss": ordinary_miss,
			"is_disadvantage": bool(attack_context.get("is_disadvantage", false)),
			"hidden_luck_at_birth": 0,
			"faith_luck_bonus": 0,
			"effective_luck": 0,
			"crit_locked": bool(attack_context.get("force_hit_no_crit", false)),
			"crit_gate_die": 20,
			"crit_gate_roll": 0,
			"hit_roll": fixed_roll,
			"fumble_low_end": 1,
			"crit_threshold": NATURAL_HIT_ROLL,
			"required_roll": required_roll,
			"display_required_roll": int(attack_check.get("display_required_roll", clampi(required_roll, 2, NATURAL_HIT_ROLL))),
			"hit_rate_percent": 100 if attack_success else 0,
			"success_rate_percent": 100 if attack_success else 0,
			"trait_trigger_results": [],
		}


class FixedCriticalHitResolver extends FixedHitResolver:
	func _init() -> void:
		super(NATURAL_HIT_ROLL)

	func resolve_attack_metadata(
		_source_unit,
		_target_unit,
		attack_check: Dictionary,
		attack_context: Dictionary
	) -> Dictionary:
		return _build_fixed_attack_metadata(
			attack_check,
			attack_context,
			ATTACK_RESOLUTION_CRITICAL_HIT,
			true,
			true,
			false
		)

	func resolve_spell_control_metadata(_source_unit, _attack_context: Dictionary) -> Dictionary:
		return {
			"attack_resolution": ATTACK_RESOLUTION_CRITICAL_HIT,
			"spell_control_resolution": &"critical_success",
			"attack_success": true,
			"critical_hit": true,
			"critical_fail": false,
			"ordinary_miss": false,
			"hit_roll": NATURAL_HIT_ROLL,
		}

	func _roll_true_random_attack_range(min_value: int, max_value: int, battle_state: BattleState) -> int:
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		return maxi(min_value, max_value)


class FixedMissResolver extends BattleHitResolver:
	func resolve_attack_metadata(
		_source_unit,
		_target_unit,
		attack_check: Dictionary,
		attack_context: Dictionary
	) -> Dictionary:
		var required_roll := int(attack_check.get("required_roll", NATURAL_HIT_ROLL))
		return {
			"attack_resolution": ATTACK_RESOLUTION_MISS,
			"attack_success": false,
			"critical_hit": false,
			"critical_fail": false,
			"ordinary_miss": true,
			"is_disadvantage": bool(attack_context.get("is_disadvantage", false)),
			"hidden_luck_at_birth": 0,
			"faith_luck_bonus": 0,
			"effective_luck": 0,
			"crit_locked": false,
			"crit_gate_die": 20,
			"crit_gate_roll": 0,
			"hit_roll": NATURAL_MISS_ROLL,
			"fumble_low_end": 1,
			"crit_threshold": NATURAL_HIT_ROLL,
			"required_roll": required_roll,
			"display_required_roll": int(attack_check.get("display_required_roll", clampi(required_roll, 2, NATURAL_HIT_ROLL))),
			"hit_rate_percent": 0,
			"success_rate_percent": 0,
			"trait_trigger_results": [],
		}

	func resolve_spell_control_metadata(_source_unit, _attack_context: Dictionary) -> Dictionary:
		return {
			"attack_resolution": ATTACK_RESOLUTION_MISS,
			"spell_control_resolution": &"miss",
			"attack_success": false,
			"critical_hit": false,
			"critical_fail": false,
			"ordinary_miss": true,
			"hit_roll": NATURAL_MISS_ROLL,
		}

	func roll_attack_check(battle_state: BattleState, attack_check: Dictionary) -> Dictionary:
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		var result := attack_check.duplicate(true)
		result["roll"] = NATURAL_MISS_ROLL
		result["roll_disposition"] = ROLL_DISPOSITION_NATURAL_AUTO_MISS
		result["success"] = false
		result["hit_rate_percent"] = 0
		result["success_rate_percent"] = 0
		result["preview_text"] = "0%（测试固定未命中）"
		result["resolution_text"] = "0%（测试固定未命中），d20=1"
		return result

	func roll_attack_die(die_size: int, _is_disadvantage: bool, _attack_context: Dictionary) -> int:
		return clampi(NATURAL_MISS_ROLL, 1, maxi(die_size, 1))

	func _roll_true_random_attack_range(min_value: int, max_value: int, battle_state: BattleState) -> int:
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		return mini(min_value, max_value)
