## File role: standalone fate combat formula helpers for crit, fumble, and disadvantage rolls.
## Review focus: keep the API pure and deterministic so future battle integration can call it without hidden state.
## Note: this layer deliberately owns no battle state and only accepts explicit inputs or injected RNGs.

class_name FateAttackFormula
extends RefCounted

const D20_SIZE := 20
const COMBAT_LUCK_SCORE_MAX := 4


static func calc_crit_gate_die_size(effective_luck: int, is_disadvantage: bool) -> int:
	var growth_steps := maxi(0, -effective_luck - 3)
	if is_disadvantage and effective_luck <= -5 and growth_steps > 0:
		growth_steps -= 1
	return D20_SIZE << growth_steps


static func calc_fumble_low_end(effective_luck: int) -> int:
	return 1 + clampi(-effective_luck - 4, 0, 2)


static func calc_combat_luck_score(hidden_luck_at_birth: int, faith_luck_bonus: int) -> int:
	var positive_hidden_luck := maxi(0, hidden_luck_at_birth)
	var positive_faith_luck := maxi(0, faith_luck_bonus)
	return mini(COMBAT_LUCK_SCORE_MAX, positive_hidden_luck + int(positive_faith_luck / 2.0))


static func calc_crit_threshold(hidden_luck_at_birth: int, faith_luck_bonus: int) -> int:
	return D20_SIZE - calc_combat_luck_score(hidden_luck_at_birth, faith_luck_bonus)


static func roll_die_with_disadvantage_rule(die_size: int, is_disadvantage: bool, rng: Variant = null) -> int:
	var normalized_die_size := maxi(die_size, 1)
	var resolved_rng = _resolve_rng(rng)
	var first_roll := int(resolved_rng.randi_range(1, normalized_die_size))
	if not is_disadvantage:
		return first_roll
	var second_roll := int(resolved_rng.randi_range(1, normalized_die_size))
	return mini(first_roll, second_roll)


static func _resolve_rng(rng: Variant) -> Variant:
	if rng != null and rng.has_method("randi_range"):
		return rng
	var fallback_rng := RandomNumberGenerator.new()
	fallback_rng.randomize()
	return fallback_rng
