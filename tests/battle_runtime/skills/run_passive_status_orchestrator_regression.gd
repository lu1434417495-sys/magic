extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ASCENSION_DEF_SCRIPT = preload("res://scripts/player/progression/ascension_def.gd")
const BATTLE_UNIT_FACTORY_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_unit_factory.gd")
const BATTLE_UNIT_FACTORY_RUNTIME_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_unit_factory_runtime.gd")
const BATTLE_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_range_service.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CHARACTER_MANAGEMENT_MODULE_SCRIPT = preload("res://scripts/systems/progression/character_management_module.gd")
const COMBAT_SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/combat_skill_def.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PASSIVE_SOURCE_CONTEXT_SCRIPT = preload("res://scripts/systems/progression/passive_source_context.gd")
const PASSIVE_STATUS_ORCHESTRATOR_SCRIPT = preload("res://scripts/systems/battle/runtime/passive_status_orchestrator.gd")
const PROGRESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/progression_content_registry.gd")
const RACE_DEF_SCRIPT = preload("res://scripts/player/progression/race_def.gd")
const RACIAL_GRANTED_SKILL_SCRIPT = preload("res://scripts/player/progression/racial_granted_skill.gd")
const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/skill_def.gd")
const SUBRACE_DEF_SCRIPT = preload("res://scripts/player/progression/subrace_def.gd")
const UNIT_PROFESSION_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_profession_progress.gd")
const UNIT_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_progress.gd")
const UNIT_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_skill_progress.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


class FakeRuntime:
	extends BATTLE_UNIT_FACTORY_RUNTIME_SCRIPT

	var character_gateway: Object = null
	var skill_defs: Dictionary = {}
	var item_defs: Dictionary = {}

	func get_character_gateway() -> Object:
		return character_gateway

	func get_skill_defs() -> Dictionary:
		return skill_defs

	func get_item_defs() -> Dictionary:
		return item_defs


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_factory_projects_identity_passives_from_character_gateway()
	_test_orchestrator_projects_race_and_subrace_passives()
	_test_orchestrator_suppresses_original_race_passives_for_ascension()
	_test_orchestrator_projects_shooting_specialization_bow_only_range_bonus()
	if _failures.is_empty():
		print("Passive status orchestrator regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Passive status orchestrator regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_factory_projects_identity_passives_from_character_gateway() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var party_state = _make_party_state([&"hero"])
	var gateway = CHARACTER_MANAGEMENT_MODULE_SCRIPT.new()
	gateway.setup(
		party_state,
		registry.get_skill_defs(),
		registry.get_profession_defs(),
		{},
		{},
		{},
		Callable(),
		registry.get_bundle()
	)

	var runtime := FakeRuntime.new()
	runtime.character_gateway = gateway
	runtime.skill_defs = registry.get_skill_defs()
	var factory = BATTLE_UNIT_FACTORY_SCRIPT.new()
	factory.setup(runtime)

	var units: Array = factory.build_ally_units(party_state, {})
	_assert_eq(units.size(), 1, "factory should build one ally for passive projection.")
	if units.is_empty():
		return
	var unit = units[0]
	_assert_true(unit.race_trait_ids.has(&"human_versatility"), "race trait ids should include human_versatility from RaceDef.")
	_assert_true(unit.race_trait_ids.has(&"civil_militia"), "race trait ids should include civil_militia from RaceDef.")
	_assert_true(not unit.race_trait_ids.has(&"darkvision"), "humans should not project darkvision.")
	_assert_true(unit.subrace_trait_ids.is_empty(), "common_human should not add placeholder subrace traits.")
	_assert_true(unit.vision_tags.has(&"normal_vision"), "vision tags should include normal_vision from RaceDef.")
	_assert_true(unit.proficiency_tags.has(&"civilian"), "proficiency tags should include civilian from RaceDef.")
	_assert_true(unit.proficiency_tags.has(&"weapon_type_spear"), "civil_militia should project spear proficiency tag.")


func _test_orchestrator_projects_race_and_subrace_passives() -> void:
	var unit = _make_battle_unit(&"race_projection_unit")
	var context = PASSIVE_SOURCE_CONTEXT_SCRIPT.new()
	context.race_def = _make_race_def()
	context.subrace_def = _make_subrace_def()

	PASSIVE_STATUS_ORCHESTRATOR_SCRIPT.apply_to_unit(unit, context, {})

	_assert_true(unit.race_trait_ids.has(&"test_race_trait"), "race trait should be projected.")
	_assert_true(unit.subrace_trait_ids.has(&"test_subrace_trait"), "subrace trait should be projected.")
	_assert_true(unit.vision_tags.has(&"darkvision"), "race vision tag should be projected.")
	_assert_true(unit.save_advantage_tags.has(&"poison"), "subrace save advantage tag should be projected.")
	_assert_eq(unit.damage_resistances.get(&"fire", &""), &"half", "race damage resistance should be projected.")
	_assert_eq(unit.per_battle_charges.get(&"racial_skill_dragon_breath_test", 0), 2, "race per-battle charge should be initialized.")
	_assert_eq(unit.per_turn_charges.get(&"racial_skill_nimble_escape_test", 0), 1, "subrace per-turn charge should be initialized.")


func _test_orchestrator_suppresses_original_race_passives_for_ascension() -> void:
	var unit = _make_battle_unit(&"ascension_projection_unit")
	var context = PASSIVE_SOURCE_CONTEXT_SCRIPT.new()
	context.race_def = _make_race_def()
	context.subrace_def = _make_subrace_def()
	context.ascension_def = _make_ascension_def(true)

	PASSIVE_STATUS_ORCHESTRATOR_SCRIPT.apply_to_unit(unit, context, {})

	_assert_true(not unit.race_trait_ids.has(&"test_race_trait"), "suppressed race trait should not be projected.")
	_assert_true(not unit.subrace_trait_ids.has(&"test_subrace_trait"), "suppressed subrace trait should not be projected.")
	_assert_true(not unit.per_battle_charges.has(&"racial_skill_dragon_breath_test"), "suppressed race charge should not be initialized.")
	_assert_true(not unit.per_turn_charges.has(&"racial_skill_nimble_escape_test"), "suppressed subrace charge should not be initialized.")
	_assert_true(unit.ascension_trait_ids.has(&"ascended_trait"), "ascension trait should still be projected.")
	_assert_eq(unit.per_battle_charges.get(&"racial_skill_ascension_ray_test", 0), 3, "ascension charge should be initialized.")


func _test_orchestrator_projects_shooting_specialization_bow_only_range_bonus() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var unit = _make_battle_unit(&"shooting_specialization_unit")
	var context = PASSIVE_SOURCE_CONTEXT_SCRIPT.new()
	context.unit_progress = UNIT_PROGRESS_SCRIPT.new()
	var skill_progress = UNIT_SKILL_PROGRESS_SCRIPT.new()
	skill_progress.skill_id = &"archer_shooting_specialization"
	skill_progress.is_learned = true
	skill_progress.skill_level = 0
	skill_progress.profession_granted_by = &"archer"
	skill_progress.granted_source_type = UNIT_SKILL_PROGRESS_SCRIPT.GRANTED_SOURCE_PROFESSION
	skill_progress.granted_source_id = &"archer"
	context.unit_progress.set_skill_progress(skill_progress)
	var profession_progress = UNIT_PROFESSION_PROGRESS_SCRIPT.new()
	profession_progress.profession_id = &"archer"
	profession_progress.rank = 1
	profession_progress.is_active = true
	context.unit_progress.set_profession_progress(profession_progress)

	PASSIVE_STATUS_ORCHESTRATOR_SCRIPT.apply_to_unit(unit, context, registry.get_skill_defs())

	var status = unit.get_status_effect(&"archer_shooting_specialization")
	_assert_true(status != null, "shooting specialization should project a battle status.")
	if status != null:
		_assert_eq(int(status.params.get("skill_level", -1)), 0, "shooting specialization status should keep learned level 0.")
		_assert_eq(int(status.params.get("range_bonus", 0)), 1, "shooting specialization status should carry range_bonus=1.")

	var weapon_skill = _make_weapon_range_skill()
	unit.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "test_shortbow",
		"weapon_profile_type_id": "shortbow",
		"weapon_family": "bow",
		"weapon_current_grip": "two_handed",
		"weapon_attack_range": 4,
		"weapon_two_handed_dice": {"dice_count": 1, "dice_sides": 6, "flat_bonus": 0},
		"weapon_uses_two_hands": true,
		"weapon_physical_damage_tag": "physical_pierce",
	})
	_assert_eq(
		BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_range(unit, weapon_skill),
		5,
		"shooting specialization should add +1 range for bow weapons."
	)
	unit.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "test_crossbow",
		"weapon_profile_type_id": "light_crossbow",
		"weapon_family": "crossbow",
		"weapon_current_grip": "two_handed",
		"weapon_attack_range": 5,
		"weapon_two_handed_dice": {"dice_count": 1, "dice_sides": 8, "flat_bonus": 0},
		"weapon_uses_two_hands": true,
		"weapon_physical_damage_tag": "physical_pierce",
	})
	_assert_eq(
		BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_range(unit, weapon_skill),
		5,
		"shooting specialization must not add range for crossbows."
	)


func _make_battle_unit(unit_id: StringName):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.source_member_id = unit_id
	unit.faction_id = &"player"
	unit.control_mode = &"manual"
	return unit


func _make_weapon_range_skill():
	var skill = SKILL_DEF_SCRIPT.new()
	skill.skill_id = &"test_weapon_range_skill"
	skill.skill_type = &"active"
	var tags: Array[StringName] = [&"archer", &"ranged", &"bow"]
	skill.tags = tags
	var combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	combat_profile.skill_id = skill.skill_id
	combat_profile.target_mode = &"unit"
	combat_profile.target_team_filter = &"enemy"
	combat_profile.target_selection_mode = &"single_unit"
	combat_profile.selection_order_mode = &"stable"
	combat_profile.range_value = 1
	skill.combat_profile = combat_profile
	return skill


func _make_race_def():
	var race = RACE_DEF_SCRIPT.new()
	race.race_id = &"test_race"
	race.display_name = "Test Race"
	race.trait_ids.append(&"test_race_trait")
	race.vision_tags.append(&"darkvision")
	race.damage_resistances = {&"fire": &"half"}
	race.racial_granted_skills.append(_make_racial_grant(&"dragon_breath_test", RACIAL_GRANTED_SKILL_SCRIPT.CHARGE_KIND_PER_BATTLE, 2))
	return race


func _make_subrace_def():
	var subrace = SUBRACE_DEF_SCRIPT.new()
	subrace.subrace_id = &"test_subrace"
	subrace.parent_race_id = &"test_race"
	subrace.display_name = "Test Subrace"
	subrace.trait_ids.append(&"test_subrace_trait")
	subrace.save_advantage_tags.append(&"poison")
	subrace.racial_granted_skills.append(_make_racial_grant(&"nimble_escape_test", RACIAL_GRANTED_SKILL_SCRIPT.CHARGE_KIND_PER_TURN, 1))
	return subrace


func _make_ascension_def(suppresses_original_race_traits: bool):
	var ascension = ASCENSION_DEF_SCRIPT.new()
	ascension.ascension_id = &"test_ascension"
	ascension.display_name = "Test Ascension"
	ascension.suppresses_original_race_traits = suppresses_original_race_traits
	ascension.trait_ids.append(&"ascended_trait")
	ascension.racial_granted_skills.append(_make_racial_grant(&"ascension_ray_test", RACIAL_GRANTED_SKILL_SCRIPT.CHARGE_KIND_PER_BATTLE, 3))
	return ascension


func _make_racial_grant(skill_id: StringName, charge_kind: StringName, charges: int):
	var grant = RACIAL_GRANTED_SKILL_SCRIPT.new()
	grant.skill_id = skill_id
	grant.minimum_skill_level = 1
	grant.charge_kind = charge_kind
	grant.charges = charges
	return grant


func _make_party_state(member_ids: Array[StringName]):
	var party_state = PARTY_STATE_SCRIPT.new()
	for member_id in member_ids:
		var member_state = PARTY_MEMBER_STATE_SCRIPT.new()
		member_state.member_id = member_id
		member_state.display_name = String(member_id).capitalize()
		member_state.race_id = &"human"
		member_state.subrace_id = &"common_human"
		member_state.progression.unit_id = member_id
		member_state.progression.display_name = member_state.display_name
		party_state.set_member_state(member_state)
		party_state.active_member_ids.append(member_id)
		if party_state.leader_member_id == &"":
			party_state.leader_member_id = member_id
		if party_state.main_character_member_id == &"":
			party_state.main_character_member_id = member_id
	return party_state


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
