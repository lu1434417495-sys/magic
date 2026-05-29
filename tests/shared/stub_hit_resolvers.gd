extends RefCounted

const FixedHitResolver = preload("res://tests/shared/FixedHitResolver.cs")
const FixedCriticalHitResolver = preload("res://tests/shared/FixedCriticalHitResolver.cs")
const FixedMissResolver = preload("res://tests/shared/FixedMissResolver.cs")


static func build_fixed_hit_resolver(fixed_roll: int = 10):
	var resolver = FixedHitResolver.new()
	resolver.fixed_roll = clampi(fixed_roll, 1, 20)
	return resolver
