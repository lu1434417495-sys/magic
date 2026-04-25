extends BattleDamageResolver


func _roll_true_random_attack_range(min_value: int, max_value: int, battle_state: BattleState) -> int:
	var lower := mini(min_value, max_value)
	var upper := maxi(min_value, max_value)
	if battle_state == null:
		return lower

	var nonce := maxi(int(battle_state.attack_roll_nonce), 0)
	var roll_seed_source := "%s:%d:%d" % [String(battle_state.battle_id), int(battle_state.seed), nonce]
	var rng := RandomNumberGenerator.new()
	rng.seed = int(roll_seed_source.hash())
	battle_state.attack_roll_nonce = nonce + 1
	return rng.randi_range(lower, upper)
