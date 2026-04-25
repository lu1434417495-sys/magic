class_name TrueRandomSeedService
extends RefCounted

const SEED_BYTE_COUNT := 7
const MAX_CRYPTO_VALUE := 72057594037927936


static func generate_seed() -> int:
	var seed := _seed_from_crypto_bytes()
	if seed > 0:
		return seed
	return _seed_from_fallback_rng()


static func randi_range(min_value: int, max_value: int) -> int:
	var lower := mini(min_value, max_value)
	var upper := maxi(min_value, max_value)
	var span := upper - lower + 1
	if span <= 1:
		return lower

	var limit := MAX_CRYPTO_VALUE - (MAX_CRYPTO_VALUE % span)
	for _attempt in range(16):
		var raw_value := _seed_from_crypto_bytes()
		if raw_value >= 0 and raw_value < limit:
			return lower + int(raw_value % span)
	return _fallback_rng_range(lower, upper)


static func _seed_from_crypto_bytes() -> int:
	var crypto := Crypto.new()
	var bytes := crypto.generate_random_bytes(SEED_BYTE_COUNT)
	if bytes.size() < SEED_BYTE_COUNT:
		return -1

	var seed := 0
	for byte_value in bytes:
		seed = (seed << 8) | int(byte_value)
	return seed


static func _seed_from_fallback_rng() -> int:
	var rng := RandomNumberGenerator.new()
	rng.randomize()
	return maxi(int(rng.randi()), 1)


static func _fallback_rng_range(min_value: int, max_value: int) -> int:
	var rng := RandomNumberGenerator.new()
	rng.randomize()
	return int(rng.randi_range(min_value, max_value))
