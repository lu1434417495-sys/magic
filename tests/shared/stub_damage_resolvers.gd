extends RefCounted

const FixedRollDamageResolver = preload("res://tests/shared/FixedRollDamageResolver.cs")
const FixedFailedSaveDamageResolver = preload("res://tests/shared/FixedFailedSaveDamageResolver.cs")
const FixedHitMaxDamageResolver = preload("res://tests/shared/FixedHitMaxDamageResolver.cs")
const FixedHitOneDamageResolver = preload("res://tests/shared/FixedHitOneDamageResolver.cs")
const FixedMissOneDamageResolver = preload("res://tests/shared/FixedMissOneDamageResolver.cs")
const FixedSuccessOneDamageResolver = preload("res://tests/shared/FixedSuccessOneDamageResolver.cs")
const FixedSuccessFailedSecondarySaveOneDamageResolver = preload("res://tests/shared/FixedSuccessFailedSecondarySaveOneDamageResolver.cs")
const FixedCriticalOneDamageResolver = preload("res://tests/shared/FixedCriticalOneDamageResolver.cs")
const TrapDamageResolver = preload("res://tests/shared/TrapDamageResolver.cs")


static func build_fixed_roll_damage_resolver(damage_rolls: Array = [], attack_rolls: Array = []):
	var resolver = FixedRollDamageResolver.new()
	resolver.set_rolls(damage_rolls, attack_rolls)
	return resolver


static func build_fixed_failed_save_damage_resolver(damage_rolls: Array = [], attack_rolls: Array = []):
	var resolver = FixedFailedSaveDamageResolver.new()
	resolver.set_rolls(damage_rolls, attack_rolls)
	return resolver
