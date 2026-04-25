extends BattleHitResolver


func _roll_battle_d20(battle_state: BattleState) -> int:
	if battle_state == null:
		return NATURAL_MISS_ROLL

	var nonce := maxi(int(battle_state.attack_roll_nonce), 0)
	var roll_seed_source := "%s:%d:%d" % [String(battle_state.battle_id), int(battle_state.seed), nonce]
	var rng := RandomNumberGenerator.new()
	rng.seed = int(roll_seed_source.hash())
	battle_state.attack_roll_nonce = nonce + 1
	return rng.randi_range(NATURAL_MISS_ROLL, NATURAL_HIT_ROLL)
