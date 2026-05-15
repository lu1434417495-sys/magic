extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BODY_SIZE_RULES_SCRIPT = preload("res://scripts/systems/progression/body_size_rules.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const AttributeModifier = preload("res://scripts/player/progression/attribute_modifier.gd")
const RaceDef = preload("res://scripts/player/progression/race_def.gd")
const SubraceDef = preload("res://scripts/player/progression/subrace_def.gd")
const AgeProfileDef = preload("res://scripts/player/progression/age_profile_def.gd")
const BloodlineDef = preload("res://scripts/player/progression/bloodline_def.gd")
const BloodlineStageDef = preload("res://scripts/player/progression/bloodline_stage_def.gd")
const AscensionDef = preload("res://scripts/player/progression/ascension_def.gd")
const AscensionStageDef = preload("res://scripts/player/progression/ascension_stage_def.gd")
const RacialGrantedSkill = preload("res://scripts/player/progression/racial_granted_skill.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const BodySizeRules = BODY_SIZE_RULES_SCRIPT

const EXPECTED_RACE_IDS := [
	&"dragonborn",
	&"drow",
	&"dwarf",
	&"elf",
	&"githyanki",
	&"gnome",
	&"half_elf",
	&"half_orc",
	&"halfling",
	&"human",
	&"tiefling",
]

const EXPECTED_SUBRACE_IDS := [
	&"asmodeus_tiefling",
	&"black_dragonborn",
	&"blue_dragonborn",
	&"brass_dragonborn",
	&"bronze_dragonborn",
	&"common_human",
	&"copper_dragonborn",
	&"deep_gnome",
	&"drow_half_elf",
	&"duergar",
	&"forest_gnome",
	&"gold_dragonborn",
	&"gold_dwarf",
	&"green_dragonborn",
	&"high_elf",
	&"high_half_elf",
	&"lightfoot_halfling",
	&"lolth_sworn_drow",
	&"mephistopheles_tiefling",
	&"red_dragonborn",
	&"rock_gnome",
	&"seldarine_drow",
	&"shield_dwarf",
	&"silver_dragonborn",
	&"standard_githyanki",
	&"standard_half_orc",
	&"strongheart_halfling",
	&"white_dragonborn",
	&"wood_elf",
	&"wood_half_elf",
	&"zariel_tiefling",
]

const DRAGONBORN_BREATH_SKILLS := {
	&"black_dragonborn": &"dragon_breath_acid_line",
	&"blue_dragonborn": &"dragon_breath_lightning_line",
	&"brass_dragonborn": &"dragon_breath_fire_line",
	&"bronze_dragonborn": &"dragon_breath_lightning_line",
	&"copper_dragonborn": &"dragon_breath_acid_line",
	&"gold_dragonborn": &"dragon_breath_fire_cone",
	&"green_dragonborn": &"dragon_breath_poison_cone",
	&"red_dragonborn": &"dragon_breath_fire_cone",
	&"silver_dragonborn": &"dragon_breath_freeze_cone",
	&"white_dragonborn": &"dragon_breath_freeze_cone",
}

const EXPECTED_RACE_TRAIT_IDS := [
	&"astral_knowledge",
	&"brave",
	&"civil_militia",
	&"damage_resistance",
	&"darkvision",
	&"deep_gnome_camouflage",
	&"draconic_ancestry",
	&"dragon_breath",
	&"drow_magic",
	&"drow_weapon_training",
	&"duergar_magic",
	&"duergar_resilience",
	&"dwarven_combat_training",
	&"dwarven_resilience",
	&"dwarven_toughness",
	&"elven_weapon_training",
	&"fey_ancestry",
	&"fleet_of_foot",
	&"forest_gnome_magic",
	&"githyanki_martial_prodigy",
	&"githyanki_psionics",
	&"gnome_cunning",
	&"halfling_luck",
	&"halfling_nimbleness",
	&"human_versatility",
	&"infernal_legacy",
	&"keen_senses",
	&"mask_of_the_wild",
	&"menacing",
	&"naturally_stealthy",
	&"racial_spell_grant",
	&"relentless_endurance",
	&"savage_attacks",
	&"save_advantage",
	&"shield_dwarf_armor_training",
	&"small_body",
	&"stonecunning",
	&"superior_darkvision",
	&"trance",
	&"artificers_lore",
	&"asmodeus_legacy",
	&"mephistopheles_legacy",
	&"zariel_legacy",
]

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var registry := ProgressionContentRegistry.new()
	_test_official_identity_registry_validates(registry)
	_test_official_race_and_subrace_ids(registry)
	_test_official_race_trait_ids(registry)
	_test_official_identity_graph_edges(registry)
	_test_official_race_traits_are_projected_by_current_system(registry)
	_test_dragonborn_subrace_breath_grants(registry)
	_test_official_titan_bloodline_content(registry)
	_test_official_titan_ascension_content(registry)

	if _failures.is_empty():
		print("Official identity content regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Official identity content regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_official_identity_registry_validates(registry: ProgressionContentRegistry) -> void:
	var errors := registry.validate()
	_assert_true(errors.is_empty(), "Official progression registry should validate cleanly. errors=%s" % str(errors))


func _test_official_race_and_subrace_ids(registry: ProgressionContentRegistry) -> void:
	_assert_id_set(registry.get_race_defs(), EXPECTED_RACE_IDS, "race")
	_assert_id_set(registry.get_subrace_defs(), EXPECTED_SUBRACE_IDS, "subrace")


func _test_official_race_trait_ids(registry: ProgressionContentRegistry) -> void:
	var race_trait_defs := registry.get_race_trait_defs()
	for trait_id in EXPECTED_RACE_TRAIT_IDS:
		_assert_true(race_trait_defs.has(trait_id), "Official race_trait registry should include %s." % String(trait_id))


func _test_official_identity_graph_edges(registry: ProgressionContentRegistry) -> void:
	var race_defs := registry.get_race_defs()
	var subrace_defs := registry.get_subrace_defs()
	var age_profile_defs := registry.get_age_profile_defs()

	for race_id in EXPECTED_RACE_IDS:
		var race_def := race_defs.get(race_id) as RaceDef
		if race_def == null:
			continue
		_assert_true(
			BodySizeRules.is_valid_body_size_category(race_def.body_size_category),
			"Race %s should use a valid BodySizeRules category." % String(race_id)
		)
		_assert_true(
			race_def.default_subrace_id != &"" and race_def.subrace_ids.has(race_def.default_subrace_id),
			"Race %s should list its default subrace." % String(race_id)
		)
		var age_profile := age_profile_defs.get(race_def.age_profile_id) as AgeProfileDef
		_assert_true(age_profile != null, "Race %s should reference an official age profile." % String(race_id))
		if age_profile != null:
			_assert_eq(age_profile.race_id, race_id, "Age profile should point back to race %s." % String(race_id))
			_assert_true(age_profile.creation_stage_ids.has(&"adult"), "Age profile %s should expose adult creation." % String(age_profile.profile_id))
			_assert_true(age_profile.creation_stage_ids.has(&"young_adult"), "Age profile %s should expose young_adult creation." % String(age_profile.profile_id))
		for subrace_id in race_def.subrace_ids:
			var subrace_def := subrace_defs.get(subrace_id) as SubraceDef
			_assert_true(subrace_def != null, "Race %s should reference existing subrace %s." % [String(race_id), String(subrace_id)])
			if subrace_def != null:
				_assert_eq(subrace_def.parent_race_id, race_id, "Subrace %s parent should match race %s." % [String(subrace_id), String(race_id)])


func _test_official_race_traits_are_projected_by_current_system(registry: ProgressionContentRegistry) -> void:
	var race_defs := registry.get_race_defs()
	var subrace_defs := registry.get_subrace_defs()
	var human := race_defs.get(&"human") as RaceDef
	_assert_true(human != null, "Human race content should exist.")
	if human != null:
		_assert_true(human.trait_ids.has(&"human_versatility"), "Human should keep Human Versatility.")
		_assert_true(human.trait_ids.has(&"civil_militia"), "Human should include Civil Militia.")
		_assert_true(not human.trait_ids.has(&"darkvision"), "Human should not have darkvision.")
		_assert_eq(human.base_speed, 6, "Human should use normal base speed.")
		_assert_true(human.proficiency_tags.has(&"weapon_type_spear"), "Human Civil Militia should expose spear proficiency tag.")

	var common_human := subrace_defs.get(&"common_human") as SubraceDef
	_assert_true(common_human != null, "Common Human subrace content should exist.")
	if common_human != null:
		_assert_true(common_human.trait_ids.is_empty(), "Common Human should not carry placeholder Brave.")

	var elf := race_defs.get(&"elf") as RaceDef
	_assert_true(elf != null, "Elf race content should exist.")
	if elf != null:
		for trait_id in [&"fey_ancestry", &"darkvision", &"keen_senses", &"trance", &"elven_weapon_training"]:
			_assert_true(elf.trait_ids.has(trait_id), "Elf should include %s." % String(trait_id))
		_assert_true(elf.save_advantage_tags.has(&"charm"), "Elf Fey Ancestry should use charm save tag.")
		_assert_true(elf.save_advantage_tags.has(&"sleep"), "Elf Fey Ancestry should use sleep save tag.")

	var dwarf := race_defs.get(&"dwarf") as RaceDef
	_assert_true(dwarf != null, "Dwarf race content should exist.")
	if dwarf != null:
		_assert_eq(dwarf.base_speed, 5, "Dwarf should use reduced base speed.")
		for trait_id in [&"darkvision", &"dwarven_resilience", &"dwarven_combat_training", &"stonecunning"]:
			_assert_true(dwarf.trait_ids.has(trait_id), "Dwarf should include %s." % String(trait_id))
		_assert_eq(dwarf.damage_resistances.get(&"poison", &""), &"half", "Dwarf should resist poison damage.")

	var gnome := race_defs.get(&"gnome") as RaceDef
	_assert_true(gnome != null, "Gnome race content should exist.")
	if gnome != null:
		_assert_true(gnome.trait_ids.has(&"darkvision"), "Gnome should include Darkvision.")
		_assert_true(gnome.trait_ids.has(&"gnome_cunning"), "Gnome should include Gnome Cunning.")
		_assert_true(gnome.vision_tags.has(&"darkvision"), "Gnome should project darkvision.")

	var half_orc := race_defs.get(&"half_orc") as RaceDef
	_assert_true(half_orc != null, "Half-Orc race content should exist.")
	if half_orc != null:
		for trait_id in [&"darkvision", &"menacing", &"savage_attacks", &"relentless_endurance"]:
			_assert_true(half_orc.trait_ids.has(trait_id), "Half-Orc should include %s." % String(trait_id))
		_assert_true(half_orc.proficiency_tags.has(&"intimidation"), "Half-Orc Menacing should expose intimidation proficiency tag.")

	var dragonborn := race_defs.get(&"dragonborn") as RaceDef
	_assert_true(dragonborn != null, "Dragonborn race content should exist.")
	if dragonborn != null:
		_assert_true(dragonborn.trait_ids.has(&"draconic_ancestry"), "Dragonborn should include Draconic Ancestry.")

	var strongheart := subrace_defs.get(&"strongheart_halfling") as SubraceDef
	_assert_true(strongheart != null, "Strongheart Halfling subrace content should exist.")
	if strongheart != null:
		_assert_true(strongheart.trait_ids.has(&"damage_resistance"), "Strongheart should include poison resistance marker.")
		_assert_true(strongheart.trait_ids.has(&"save_advantage"), "Strongheart should include save advantage marker.")
		_assert_eq(strongheart.damage_resistances.get(&"poison", &""), &"half", "Strongheart should resist poison damage.")


func _test_dragonborn_subrace_breath_grants(registry: ProgressionContentRegistry) -> void:
	var subrace_defs := registry.get_subrace_defs()
	var skill_defs := registry.get_skill_defs()
	for subrace_id in DRAGONBORN_BREATH_SKILLS.keys():
		var expected_skill_id := StringName(DRAGONBORN_BREATH_SKILLS[subrace_id])
		var subrace_def := subrace_defs.get(subrace_id) as SubraceDef
		_assert_true(subrace_def != null, "Dragonborn subrace %s should exist." % String(subrace_id))
		if subrace_def == null:
			continue
		_assert_eq(subrace_def.racial_granted_skills.size(), 1, "Dragonborn subrace %s should grant one breath skill." % String(subrace_id))
		if subrace_def.racial_granted_skills.is_empty():
			continue
		var grant := subrace_def.racial_granted_skills[0] as RacialGrantedSkill
		_assert_true(grant != null, "Dragonborn subrace %s grant should be a RacialGrantedSkill." % String(subrace_id))
		if grant == null:
			continue
		_assert_eq(grant.skill_id, expected_skill_id, "Dragonborn subrace %s should grant the expected breath skill." % String(subrace_id))
		_assert_eq(grant.charge_kind, RacialGrantedSkill.CHARGE_KIND_PER_BATTLE, "Dragonborn breath should be per battle.")
		_assert_eq(grant.charges, 1, "Dragonborn breath should have one charge.")
		var skill_def := skill_defs.get(grant.skill_id) as SkillDef
		_assert_true(skill_def != null, "Dragonborn breath skill %s should exist." % String(grant.skill_id))
		if skill_def != null:
			_assert_eq(skill_def.learn_source, &"subrace", "Dragonborn breath skill %s should be a subrace grant." % String(grant.skill_id))


func _test_official_titan_bloodline_content(registry: ProgressionContentRegistry) -> void:
	var bloodline_defs := registry.get_bloodline_defs()
	var bloodline_stage_defs := registry.get_bloodline_stage_defs()
	var titan := bloodline_defs.get(&"titan") as BloodlineDef
	_assert_true(titan != null, "Official bloodline registry should include titan.")
	if titan == null:
		return
	_assert_eq(titan.display_name, "Titan Blood", "Titan bloodline should have a stable display name.")
	_assert_eq(titan.stage_ids, [&"titan_awakened"], "Titan bloodline should list titan_awakened as its stage.")
	_assert_eq(titan.racial_granted_skills.size(), 0, "Titan bloodline should not grant placeholder skills before official skills exist.")
	_assert_modifier(titan.attribute_modifiers, UnitBaseAttributes.CONSTITUTION, 1, &"titan", &"bloodline")
	_assert_modifier(titan.attribute_modifiers, UnitBaseAttributes.WILLPOWER, 1, &"titan", &"bloodline")

	var awakened := bloodline_stage_defs.get(&"titan_awakened") as BloodlineStageDef
	_assert_true(awakened != null, "Official bloodline stage registry should include titan_awakened.")
	if awakened == null:
		return
	_assert_eq(awakened.bloodline_id, &"titan", "Titan awakened stage should point back to titan bloodline.")
	_assert_eq(awakened.racial_granted_skills.size(), 0, "Titan awakened should not grant placeholder skills before official skills exist.")
	_assert_modifier(awakened.attribute_modifiers, UnitBaseAttributes.STRENGTH, 2, &"titan_awakened", &"bloodline")
	_assert_modifier(awakened.attribute_modifiers, UnitBaseAttributes.CONSTITUTION, 2, &"titan_awakened", &"bloodline")
	_assert_modifier(awakened.attribute_modifiers, UnitBaseAttributes.WILLPOWER, 1, &"titan_awakened", &"bloodline")
	_assert_true(
		_array_contains_text(awakened.trait_summary, "does not change body size"),
		"Titan awakened summary should state that body size stays out of this bloodline stage."
	)


func _test_official_titan_ascension_content(registry: ProgressionContentRegistry) -> void:
	var ascension_defs := registry.get_ascension_defs()
	var ascension_stage_defs := registry.get_ascension_stage_defs()
	var skill_defs := registry.get_skill_defs()
	var ascension := ascension_defs.get(&"titan_blood_ascension") as AscensionDef
	_assert_true(ascension != null, "Official ascension registry should include titan_blood_ascension.")
	if ascension == null:
		return
	_assert_eq(ascension.stage_ids, [&"titan_avatar"], "Titan ascension should list titan_avatar as its stage.")
	_assert_eq(ascension.allowed_bloodline_ids, [&"titan"], "Titan ascension should require titan bloodline.")
	_assert_eq(ascension.allowed_race_ids.size(), 0, "Titan ascension should not be locked to a normal race.")
	_assert_eq(ascension.allowed_subrace_ids.size(), 0, "Titan ascension should not be locked to a normal subrace.")
	_assert_eq(ascension.racial_granted_skills.size(), 0, "Titan ascension should not grant placeholder skills before official skills exist.")
	_assert_true(not ascension.suppresses_original_race_traits, "Titan ascension should keep original race traits active.")

	var stage := ascension_stage_defs.get(&"titan_avatar") as AscensionStageDef
	_assert_true(stage != null, "Official ascension stage registry should include titan_avatar.")
	if stage == null:
		return
	_assert_eq(stage.ascension_id, &"titan_blood_ascension", "Titan avatar stage should point back to titan ascension.")
	_assert_eq(stage.body_size_category_override, &"large", "Titan avatar should override body size category to large.")
	_assert_eq(
		BodySizeRules.get_body_size_for_category(stage.body_size_category_override),
		BodySizeRules.BODY_SIZE_LARGE,
		"Titan avatar body_size int should derive from BodySizeRules large."
	)
	_assert_modifier(stage.attribute_modifiers, UnitBaseAttributes.STRENGTH, 3, &"titan_avatar", &"ascension")
	_assert_modifier(stage.attribute_modifiers, UnitBaseAttributes.CONSTITUTION, 2, &"titan_avatar", &"ascension")
	_assert_modifier(stage.attribute_modifiers, UnitBaseAttributes.WILLPOWER, 2, &"titan_avatar", &"ascension")
	_assert_eq(stage.racial_granted_skills.size(), 3, "Titan avatar should grant its official ascension combat skills.")
	_assert_granted_skill(stage.racial_granted_skills, &"titan_stomp", RacialGrantedSkill.CHARGE_KIND_PER_BATTLE, 2, "Titan avatar")
	_assert_granted_skill(stage.racial_granted_skills, &"titan_domain_pressure", RacialGrantedSkill.CHARGE_KIND_PER_BATTLE, 1, "Titan avatar")
	_assert_granted_skill(stage.racial_granted_skills, &"titan_colossus_form", RacialGrantedSkill.CHARGE_KIND_PER_BATTLE, 1, "Titan avatar")
	_assert_titan_stomp_skill(skill_defs)
	_assert_titan_domain_pressure_skill(skill_defs)
	_assert_titan_colossus_form_skill(skill_defs)
	_assert_true(
		_array_contains_text(stage.trait_summary, "Body size category becomes large"),
		"Titan avatar summary should expose the body size override."
	)
	_assert_true(
		_array_contains_text(stage.trait_summary, "battle-local temporary body size override"),
		"Titan avatar summary should keep temporary giant form battle-local."
	)


func _assert_granted_skill(
	granted_skills: Array,
	expected_skill_id: StringName,
	expected_charge_kind: StringName,
	expected_charges: int,
	owner_label: String
) -> void:
	for grant_variant in granted_skills:
		var grant := grant_variant as RacialGrantedSkill
		if grant == null or grant.skill_id != expected_skill_id:
			continue
		_assert_eq(grant.minimum_skill_level, 1, "%s %s grant should start from minimum skill level 1." % [owner_label, String(expected_skill_id)])
		_assert_true(not _resource_has_property(grant, "grant_level"), "%s %s grant should not expose legacy grant_level." % [owner_label, String(expected_skill_id)])
		_assert_eq(grant.charge_kind, expected_charge_kind, "%s %s grant should use expected charge kind." % [owner_label, String(expected_skill_id)])
		_assert_eq(grant.charges, expected_charges, "%s %s grant should have expected charges." % [owner_label, String(expected_skill_id)])
		return
	_test.fail("%s should grant %s." % [owner_label, String(expected_skill_id)])


func _assert_titan_stomp_skill(skill_defs: Dictionary) -> void:
	var skill := _assert_titan_ascension_skill(skill_defs, &"titan_stomp")
	if skill == null or skill.combat_profile == null:
		return
	var combat_profile := skill.combat_profile
	_assert_eq(combat_profile.target_mode, &"ground", "Titan Stomp should target ground.")
	_assert_eq(combat_profile.target_team_filter, &"enemy", "Titan Stomp should target enemies.")
	_assert_eq(combat_profile.range_value, 0, "Titan Stomp should originate from the caster.")
	_assert_eq(combat_profile.area_pattern, &"radius", "Titan Stomp should use radius area.")
	_assert_eq(combat_profile.area_value, 2, "Titan Stomp should affect a large radius.")
	_assert_eq(combat_profile.effect_defs.size(), 2, "Titan Stomp should have damage and stagger effects.")
	if combat_profile.effect_defs.size() >= 2:
		var damage_effect := combat_profile.effect_defs[0]
		var stagger_effect := combat_profile.effect_defs[1]
		_assert_eq(damage_effect.effect_type, &"damage", "Titan Stomp first effect should deal damage.")
		_assert_eq(damage_effect.damage_tag, &"physical_blunt", "Titan Stomp damage should be blunt.")
		_assert_eq(damage_effect.power, 10, "Titan Stomp damage should use expected power.")
		_assert_eq(stagger_effect.effect_type, &"status", "Titan Stomp second effect should apply status.")
		_assert_eq(stagger_effect.status_id, &"staggered", "Titan Stomp status should stagger.")
		_assert_eq(stagger_effect.duration_tu, 40, "Titan Stomp stagger should use expected duration.")


func _assert_titan_domain_pressure_skill(skill_defs: Dictionary) -> void:
	var skill := _assert_titan_ascension_skill(skill_defs, &"titan_domain_pressure")
	if skill == null or skill.combat_profile == null:
		return
	var combat_profile := skill.combat_profile
	_assert_eq(combat_profile.target_mode, &"ground", "Titan Domain Pressure should target ground.")
	_assert_eq(combat_profile.target_team_filter, &"enemy", "Titan Domain Pressure should target enemies.")
	_assert_eq(combat_profile.range_value, 3, "Titan Domain Pressure should have ranged placement.")
	_assert_eq(combat_profile.area_pattern, &"radius", "Titan Domain Pressure should use radius area.")
	_assert_eq(combat_profile.area_value, 2, "Titan Domain Pressure should affect a domain-sized area.")
	_assert_eq(combat_profile.effect_defs.size(), 1, "Titan Domain Pressure should have one status effect.")
	if combat_profile.effect_defs.size() >= 1:
		var slow_effect := combat_profile.effect_defs[0]
		_assert_eq(slow_effect.effect_type, &"status", "Titan Domain Pressure effect should apply status.")
		_assert_eq(slow_effect.status_id, &"slow", "Titan Domain Pressure should slow enemies.")
		_assert_eq(slow_effect.duration_tu, 80, "Titan Domain Pressure slow should use expected duration.")


func _assert_titan_colossus_form_skill(skill_defs: Dictionary) -> void:
	var skill := _assert_titan_ascension_skill(skill_defs, &"titan_colossus_form")
	if skill == null or skill.combat_profile == null:
		return
	var combat_profile := skill.combat_profile
	_assert_eq(combat_profile.target_mode, &"unit", "Titan Colossus Form should use unit routing for self targeting.")
	_assert_eq(combat_profile.target_team_filter, &"self", "Titan Colossus Form should only target self.")
	_assert_eq(combat_profile.target_selection_mode, &"self", "Titan Colossus Form should use self target selection.")
	_assert_eq(combat_profile.effect_defs.size(), 1, "Titan Colossus Form should have one body size effect.")
	if combat_profile.effect_defs.size() >= 1:
		var body_size_effect := combat_profile.effect_defs[0]
		_assert_eq(body_size_effect.effect_type, &"body_size_category_override", "Titan Colossus Form should use the body size override effect.")
		_assert_eq(body_size_effect.status_id, &"titan_giant_form", "Titan Colossus Form should track duration with titan_giant_form.")
		_assert_eq(body_size_effect.body_size_category, &"huge", "Titan Colossus Form should temporarily become huge.")
		_assert_eq(
			BodySizeRules.get_body_size_for_category(body_size_effect.body_size_category),
			BodySizeRules.BODY_SIZE_HUGE,
			"Titan Colossus Form body size int should derive from BodySizeRules huge."
		)
		_assert_eq(body_size_effect.duration_tu, 80, "Titan Colossus Form should use expected duration.")


func _assert_titan_ascension_skill(skill_defs: Dictionary, skill_id: StringName) -> SkillDef:
	var skill := skill_defs.get(skill_id) as SkillDef
	_assert_true(skill != null, "Official skill registry should include %s." % String(skill_id))
	if skill == null:
		return null
	_assert_eq(skill.learn_source, &"ascension", "Titan skill %s should be an ascension grant." % String(skill_id))
	_assert_eq(skill.max_level, 1, "Titan skill %s should be a fixed identity skill." % String(skill_id))
	_assert_eq(skill.mastery_curve, PackedInt32Array([20]), "Titan skill %s should use a one-level mastery curve." % String(skill_id))
	_assert_true(skill.tags.has(&"titan"), "Titan skill %s should carry titan tag." % String(skill_id))
	_assert_true(skill.tags.has(&"ascension"), "Titan skill %s should carry ascension tag." % String(skill_id))
	_assert_true(skill.combat_profile != null, "Titan skill %s should have a combat profile." % String(skill_id))
	return skill


func _assert_id_set(registry: Dictionary, expected_ids: Array, label: String) -> void:
	_assert_eq(registry.size(), expected_ids.size(), "Official %s count should match expected seed pool." % label)
	for expected_id in expected_ids:
		_assert_true(registry.has(expected_id), "Official %s registry should include %s." % [label, String(expected_id)])


func _assert_modifier(
	modifiers: Array,
	attribute_id: StringName,
	expected_value: int,
	expected_source_id: StringName,
	expected_source_type: StringName
) -> void:
	for modifier_variant in modifiers:
		var modifier := modifier_variant as AttributeModifier
		if modifier == null:
			continue
		if modifier.attribute_id == attribute_id and modifier.source_id == expected_source_id:
			_assert_eq(modifier.mode, AttributeModifier.MODE_FLAT, "Titan modifier %s should be flat." % String(attribute_id))
			_assert_eq(modifier.value, expected_value, "Titan modifier %s should have expected value." % String(attribute_id))
			_assert_eq(modifier.source_type, expected_source_type, "Titan modifier %s should use expected source type." % String(attribute_id))
			return
	_test.fail("Titan modifiers should include %s=%d from %s." % [String(attribute_id), expected_value, String(expected_source_id)])


func _array_contains_text(values: Array, fragment: String) -> bool:
	for value in values:
		if String(value).contains(fragment):
			return true
	return false


func _resource_has_property(resource: Resource, property_name: String) -> bool:
	if resource == null:
		return false
	for property_info in resource.get_property_list():
		if String(property_info.get("name", "")) == property_name:
			return true
	return false


func _assert_error_contains(errors: Array[String], expected_fragment: String, message: String) -> void:
	for validation_error in errors:
		if validation_error.contains(expected_fragment):
			return
	_test.fail("%s | missing fragment=%s errors=%s" % [message, expected_fragment, str(errors)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_test.fail("%s expected=%s actual=%s" % [message, str(expected), str(actual)])
