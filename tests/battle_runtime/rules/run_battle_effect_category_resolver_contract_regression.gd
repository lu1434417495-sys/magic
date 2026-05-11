extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_category_fields_are_formal_schema()
	_test_resolver_uses_explicit_delivery_and_effect_categories()
	_test_resolver_ignores_legacy_params_barrier_categories()
	_test_resolver_does_not_guess_from_skill_id_or_tags()
	_test.finish(self, "Battle effect category resolver contract regression")


func _test_category_fields_are_formal_schema() -> void:
	var combat_profile := CombatSkillDef.new()
	var effect := CombatEffectDef.new()
	_assert_has_property(combat_profile, "delivery_categories", "CombatSkillDef must expose delivery_categories as the formal delivery category schema.")
	_assert_has_property(effect, "effect_categories", "CombatEffectDef must expose effect_categories as the formal payload category schema.")


func _test_resolver_uses_explicit_delivery_and_effect_categories() -> void:
	var resolver = _new_resolver()
	if resolver == null or not _assert_has_method(resolver, "resolve_categories", "BattleEffectCategoryResolver must expose resolve_categories(skill_def, effect_defs)."):
		return
	var skill := _build_skill(&"contract_explicit_categories", [&"spell", &"projectile"])
	var effect := CombatEffectDef.new()
	if _has_property(effect, "effect_categories"):
		effect.set("effect_categories", [&"force_effect", &"mental_attack"])
	var categories: Array = resolver.resolve_categories(skill, [effect])
	_assert_true(categories.has(&"spell"), "Resolver must include explicit delivery category spell.")
	_assert_true(categories.has(&"projectile"), "Resolver must include explicit delivery category projectile.")
	_assert_true(categories.has(&"force_effect"), "Resolver must include explicit effect category force_effect.")
	_assert_true(categories.has(&"mental_attack"), "Resolver must include explicit effect category mental_attack.")


func _test_resolver_ignores_legacy_params_barrier_categories() -> void:
	var resolver = _new_resolver()
	if resolver == null or not resolver.has_method("resolve_categories"):
		return
	var skill := _build_skill(&"contract_legacy_params", [])
	var effect := CombatEffectDef.new()
	effect.params = {
		"barrier_categories": [&"spell", &"force_effect"],
	}
	var categories: Array = resolver.resolve_categories(skill, [effect])
	_assert_true(not categories.has(&"spell"), "Resolver must not read legacy params.barrier_categories.")
	_assert_true(not categories.has(&"force_effect"), "Resolver must not read legacy params.barrier_categories.")


func _test_resolver_does_not_guess_from_skill_id_or_tags() -> void:
	var resolver = _new_resolver()
	if resolver == null or not resolver.has_method("resolve_categories"):
		return
	var skill := SkillDef.new()
	skill.skill_id = &"mage_arcane_missile_detect_breath"
	skill.display_name = "Misleading Contract Skill"
	skill.tags = [&"mage", &"magic", &"missile", &"breath", &"psychic"]
	skill.combat_profile = CombatSkillDef.new()
	var categories: Array = resolver.resolve_categories(skill, [])
	_assert_true(not categories.has(&"magical_missile"), "Resolver must not infer magical_missile from skill_id text.")
	_assert_true(not categories.has(&"detection"), "Resolver must not infer detection from skill_id text.")
	_assert_true(not categories.has(&"breath_weapon"), "Resolver must not infer breath_weapon from tags without formal categories.")
	_assert_true(not categories.has(&"mental_attack"), "Resolver must not infer mental_attack from tags without formal categories.")


func _build_skill(skill_id: StringName, delivery_categories: Array[StringName]) -> SkillDef:
	var skill := SkillDef.new()
	skill.skill_id = skill_id
	skill.display_name = String(skill_id)
	skill.combat_profile = CombatSkillDef.new()
	skill.combat_profile.skill_id = skill_id
	if _has_property(skill.combat_profile, "delivery_categories"):
		skill.combat_profile.set("delivery_categories", delivery_categories)
	return skill


func _new_resolver():
	var resolver_path := "res://scripts/systems/battle/rules/battle_effect_category_resolver.gd"
	if not FileAccess.file_exists(resolver_path):
		_failures.append("BattleEffectCategoryResolver script is missing.")
		return null
	var resolver_script = load(resolver_path)
	if resolver_script == null:
		_failures.append("BattleEffectCategoryResolver script is missing.")
		return null
	return resolver_script.new()


func _assert_has_method(object, method_name: String, message: String) -> bool:
	if object == null or not object.has_method(method_name):
		_failures.append(message)
		return false
	return true


func _assert_has_property(object, property_name: String, message: String) -> bool:
	if not _has_property(object, property_name):
		_failures.append(message)
		return false
	return true


func _has_property(object, property_name: String) -> bool:
	if object == null:
		return false
	for property in object.get_property_list():
		if String(property.get("name", "")) == property_name:
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	_test.assert_true(condition, message)
