extends RefCounted

const StubDamageResolvers = preload("res://tests/shared/stub_damage_resolvers.gd")
const StubHitResolvers = preload("res://tests/shared/stub_hit_resolvers.gd")


static func configure_fixed_combat(runtime) -> void:
	if runtime == null:
		return
	if runtime.has_method("configure_hit_resolver_for_tests"):
		runtime.configure_hit_resolver_for_tests(StubHitResolvers.FixedHitResolver.new())
	if runtime.has_method("configure_damage_resolver_for_tests"):
		var damage_resolver := StubDamageResolvers.FixedSuccessOneDamageResolver.new()
		if runtime.has_method("get_skill_defs") and damage_resolver.has_method("set_skill_defs"):
			var skill_defs = runtime.get_skill_defs()
			if skill_defs is Dictionary:
				damage_resolver.set_skill_defs(skill_defs)
		runtime.configure_damage_resolver_for_tests(damage_resolver)


static func configure_fixed_combat_for_facade(facade) -> void:
	if facade == null:
		return
	if facade is Object and facade.has_method("get_battle_runtime"):
		configure_fixed_combat(facade.get_battle_runtime())
		return
	if facade is Object:
		configure_fixed_combat(facade.get("_battle_runtime"))
