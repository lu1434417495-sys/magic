class_name BattleSimFormalCombatFixture
extends RefCounted

const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const UNIT_PROFESSION_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_profession_progress.gd")
const UNIT_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_skill_progress.gd")
const EQUIPMENT_STATE_SCRIPT = preload("res://scripts/player/equipment/equipment_state.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const CHARACTER_CREATION_SERVICE_SCRIPT = preload("res://scripts/systems/progression/character_creation_service.gd")
const CHARACTER_MANAGEMENT_MODULE_SCRIPT = preload("res://scripts/systems/progression/character_management_module.gd")
const PROGRESSION_SERVICE_SCRIPT = preload("res://scripts/systems/progression/progression_service.gd")
const ATTRIBUTE_GROWTH_SERVICE_SCRIPT = preload("res://scripts/systems/progression/attribute_growth_service.gd")
const CHARACTER_PROGRESSION_DELTA_SCRIPT = preload("res://scripts/systems/progression/character_progression_delta.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")

const ROSTER_MIXED_2S1A: StringName = &"mixed_2sword_1arch_mirror_simulation"
const ROSTER_MIXED_6V12: StringName = &"mixed_6v12_mirror_simulation"
const ROSTER_OPTION_MAIN_CHARACTER_MEMBER_ID := "main_character_member_id"
const ROSTER_OPTION_LEADER_MEMBER_ID := "leader_member_id"
const ROSTER_OPTION_MAIN_CHARACTER_REROLL_COUNT := "main_character_reroll_count"
const ROSTER_OPTION_ATTRIBUTE_ROLL_SEED := "attribute_roll_seed"
const HP_ROLL_SEED_OFFSET := 104729
const ATTRIBUTE_ROLL_DICE_COUNT := 5
const ATTRIBUTE_ROLL_DICE_SIDES := 3
const ATTRIBUTE_ROLL_OFFSET := -1
const ATTRIBUTE_ROLL_VALUE_FLOOR := 4
const DEFAULT_ATTRIBUTE_ROLL_SEED := 101
const USE_DEFAULT_ACTION_THRESHOLD := -1
const WARRIOR_BODY_ARMOR_ITEM_ID: StringName = &"iron_scale_mail"
const ARCHER_BODY_ARMOR_ITEM_ID: StringName = &"leather_jerkin"
const ATTRIBUTE_ROLL_IDS: Array[StringName] = [
	UnitBaseAttributes.STRENGTH,
	UnitBaseAttributes.AGILITY,
	UnitBaseAttributes.CONSTITUTION,
	UnitBaseAttributes.PERCEPTION,
	UnitBaseAttributes.INTELLIGENCE,
	UnitBaseAttributes.WILLPOWER,
]

var party_state = null
var character_management = null
var ally_member_ids: Array[StringName] = []
var hostile_member_ids: Array[StringName] = []

var charge_mastery = 0
var heavy_mastery = 0
var aimed_mastery = 0
var multishot_mastery = 0
var basic_mastery = 0

var _skill_defs: Dictionary = {}
var _profession_defs: Dictionary = {}
var _achievement_defs: Dictionary = {}
var _item_defs: Dictionary = {}
var _progression_content_bundle: Dictionary = {}
var _ai_brain_by_member_id: Dictionary = {}
var _ai_state_by_member_id: Dictionary = {}
var _roster_options: Dictionary = {}
var _attribute_roll_rng := RandomNumberGenerator.new()
var _hp_roll_rng := RandomNumberGenerator.new()


func setup_content(content: Dictionary) -> void:
	_skill_defs = content.get("skill_defs", {}) if content.get("skill_defs", {}) is Dictionary else {}
	_profession_defs = content.get("profession_defs", {}) if content.get("profession_defs", {}) is Dictionary else {}
	_achievement_defs = content.get("achievement_defs", {}) if content.get("achievement_defs", {}) is Dictionary else {}
	_item_defs = content.get("item_defs", {}) if content.get("item_defs", {}) is Dictionary else {}
	_progression_content_bundle = content.get("progression_content_bundle", {}) if content.get("progression_content_bundle", {}) is Dictionary else {}
	_setup_character_management()


func build_roster(roster_id: StringName, options: Dictionary = {}) -> bool:
	_reset_roster()
	_roster_options = options.duplicate(true) if options != null else {}
	_setup_attribute_roll_rng()
	match roster_id:
		ROSTER_MIXED_2S1A:
			_build_mixed_2s1a_roster()
		ROSTER_MIXED_6V12:
			_build_mixed_6v12_roster()
		_:
			return false
	_finalize_roster_identity()
	_setup_character_management()
	_restore_all_members_to_full_hp()
	return true


func build_runtime_context(runtime, base_context: Dictionary) -> Dictionary:
	_restore_all_members_to_full_hp()
	var context = base_context.duplicate(true)
	context["battle_party"] = []
	context["ally_member_ids"] = ally_member_ids.duplicate()
	context["validate_spawn_reachability"] = true
	context["validate_bidirectional_spawn_reachability"] = true
	context["enforce_opposing_spawn_sides"] = true
	var saved_active_ids: Array = party_state.active_member_ids.duplicate()
	party_state.active_member_ids = hostile_member_ids.duplicate()
	var hostile_context = context.duplicate(true)
	hostile_context["battle_party"] = []
	hostile_context["ally_member_ids"] = hostile_member_ids.duplicate()
	var hostile_units: Array = runtime._unit_factory.build_ally_units(party_state, hostile_context) if runtime != null else []
	for unit_state in hostile_units:
		_apply_unit_runtime_metadata(unit_state, &"hostile")
	context["enemy_units"] = hostile_units
	party_state.active_member_ids = ally_member_ids.duplicate()
	if party_state.active_member_ids.is_empty():
		party_state.active_member_ids = saved_active_ids
	return context


func apply_started_battle_metadata(state) -> void:
	if state == null:
		return
	for unit_id in state.units.keys():
		var unit_state = state.units.get(unit_id)
		if unit_state == null:
			continue
		if ally_member_ids.has(unit_state.source_member_id):
			_apply_unit_runtime_metadata(unit_state, &"player")
		elif hostile_member_ids.has(unit_state.source_member_id):
			_apply_unit_runtime_metadata(unit_state, &"hostile")


func get_party_state():
	return party_state


func get_member_state(member_id: StringName):
	return party_state.get_member_state(member_id) if party_state != null else null


func get_item_defs() -> Dictionary:
	return _item_defs


func get_member_attribute_snapshot_for_equipment_view(member_id: StringName, equipment_view: Variant):
	return character_management.get_member_attribute_snapshot_for_equipment_view(member_id, equipment_view)


func get_member_weapon_projection_for_equipment_view(member_id: StringName, equipment_view: Variant) -> Dictionary:
	return character_management.get_member_weapon_projection_for_equipment_view(member_id, equipment_view)


func build_passive_source_context(member_id: StringName, progression_state = null):
	return character_management.build_passive_source_context(member_id, progression_state)


func grant_battle_mastery(member_id: StringName, skill_id: StringName, amount: int):
	_record_mastery(skill_id, amount)
	var delta = CHARACTER_PROGRESSION_DELTA_SCRIPT.new()
	delta.member_id = member_id
	delta.mastery_changes.append({"skill_id": skill_id, "amount": amount, "source_type": &"battle"})
	return delta


func grant_skill_mastery_from_source(
	member_id: StringName,
	skill_id: StringName,
	amount: int,
	source_type: StringName,
	source_label: String = "",
	reason_text: String = "",
	emit_achievement_event: bool = true
):
	_record_mastery(skill_id, amount)
	var delta = CHARACTER_PROGRESSION_DELTA_SCRIPT.new()
	delta.member_id = member_id
	delta.mastery_changes.append({"skill_id": skill_id, "amount": amount, "source_type": source_type})
	return delta


func record_achievement_event(
	member_id: StringName,
	event_type: StringName,
	amount: int = 1,
	subject_id: StringName = &"",
	meta: Dictionary = {}
) -> Array[StringName]:
	return []


func _reset_roster() -> void:
	party_state = PARTY_STATE_SCRIPT.new()
	party_state.version = 3
	party_state.gold = 0
	ally_member_ids.clear()
	hostile_member_ids.clear()
	_ai_brain_by_member_id.clear()
	_ai_state_by_member_id.clear()
	_roster_options.clear()
	charge_mastery = 0
	heavy_mastery = 0
	aimed_mastery = 0
	multishot_mastery = 0
	basic_mastery = 0
	_attribute_roll_rng = RandomNumberGenerator.new()


func _build_mixed_2s1a_roster() -> void:
	var sword_attrs = _attrs(14, 12, 14, 10, 8, 10)
	var archer_attrs = _attrs(10, 16, 12, 14, 8, 10)
	var sword_skills: Array = [
		{"skill_id": &"charge", "level": 1, "is_core": false},
		{"skill_id": &"warrior_heavy_strike", "level": 1, "is_core": false},
	]
	var archer_skills: Array = [
		{"skill_id": &"basic_attack", "level": 1, "is_core": false},
		{"skill_id": &"archer_aimed_shot", "level": 1, "is_core": false},
		{"skill_id": &"archer_multishot", "level": 1, "is_core": false},
	]
	_add_member(&"ally_longsword_01", "盟军长剑手01", &"player", sword_attrs, 30, sword_skills, &"", 0, &"steel_longsword", WARRIOR_BODY_ARMOR_ITEM_ID, &"melee_aggressor", &"engage")
	_add_member(&"ally_longsword_02", "盟军长剑手02", &"player", sword_attrs, 30, sword_skills, &"", 0, &"steel_longsword", WARRIOR_BODY_ARMOR_ITEM_ID, &"melee_aggressor", &"engage")
	_add_member(&"ally_archer_01", "盟军弓箭手", &"player", archer_attrs, 30, archer_skills, &"", 0, &"ash_longbow", ARCHER_BODY_ARMOR_ITEM_ID, &"ranged_archer", &"pressure")
	_add_member(&"enemy_longsword_01", "敌军长剑手01", &"hostile", sword_attrs, 30, sword_skills, &"", 0, &"steel_longsword", WARRIOR_BODY_ARMOR_ITEM_ID, &"melee_aggressor", &"engage")
	_add_member(&"enemy_longsword_02", "敌军长剑手02", &"hostile", sword_attrs, 30, sword_skills, &"", 0, &"steel_longsword", WARRIOR_BODY_ARMOR_ITEM_ID, &"melee_aggressor", &"engage")
	_add_member(&"enemy_archer_01", "敌军弓箭手", &"hostile", archer_attrs, 30, archer_skills, &"", 0, &"ash_longbow", ARCHER_BODY_ARMOR_ITEM_ID, &"ranged_archer", &"pressure")


func _build_mixed_6v12_roster() -> void:
	var elite_sword_skills: Array = [
		{"skill_id": &"basic_attack", "level": 1, "is_core": false},
		{"skill_id": &"charge", "level": 7, "is_core": true},
		{"skill_id": &"warrior_heavy_strike", "level": 5, "is_core": true},
	]
	var elite_archer_skills: Array = [
		{"skill_id": &"basic_attack", "level": 1, "is_core": false},
		{"skill_id": &"archer_aimed_shot", "level": 3, "is_core": true},
		{"skill_id": &"archer_multishot", "level": 7, "is_core": true},
	]
	var elite_mage_skills: Array = [
		{"skill_id": &"basic_attack", "level": 1, "is_core": false},
		{"skill_id": &"mage_fireball", "level": 7, "is_core": true},
		{"skill_id": &"mage_cone_of_cold", "level": 7, "is_core": true},
		{"skill_id": &"mage_blink", "level": 7, "is_core": true},
		{"skill_id": &"mage_gust_of_wind", "level": 7, "is_core": true},
		{"skill_id": &"mage_chain_lightning", "level": 7, "is_core": true},
	]
	var hostile_sword_skills: Array = [
		{"skill_id": &"basic_attack", "level": 1, "is_core": false},
		{"skill_id": &"charge", "level": 1, "is_core": false},
		{"skill_id": &"warrior_heavy_strike", "level": 1, "is_core": false},
	]
	var hostile_archer_skills: Array = [
		{"skill_id": &"basic_attack", "level": 1, "is_core": false},
		{"skill_id": &"archer_aimed_shot", "level": 1, "is_core": false},
		{"skill_id": &"archer_multishot", "level": 1, "is_core": false},
	]
	for index in range(4):
		_add_member(StringName("elite_sword_%d" % index), "Elite Sword %d" % index, &"player", _roll_creation_attributes(), USE_DEFAULT_ACTION_THRESHOLD, elite_sword_skills, &"warrior", 2, &"steel_longsword", WARRIOR_BODY_ARMOR_ITEM_ID, &"melee_aggressor", &"engage")
	_add_member(StringName("elite_archer_0"), "Elite Archer 0", &"player", _roll_creation_attributes(), USE_DEFAULT_ACTION_THRESHOLD, elite_archer_skills, &"archer", 2, &"ash_longbow", ARCHER_BODY_ARMOR_ITEM_ID, &"ranged_archer", &"pressure")
	_add_member(StringName("elite_mage_0"), "Elite Mage 0", &"player", _roll_creation_attributes(), USE_DEFAULT_ACTION_THRESHOLD, elite_mage_skills, &"mage", 5, &"", ARCHER_BODY_ARMOR_ITEM_ID, &"mage_controller", &"pressure")
	_set_member_mp_max(StringName("elite_mage_0"), 1000)
	for index in range(6):
		_add_member(StringName("hostile_sword_%d" % index), "Hostile Elite Sword %d" % index, &"hostile", _roll_creation_attributes(), USE_DEFAULT_ACTION_THRESHOLD, elite_sword_skills, &"warrior", 2, &"steel_longsword", WARRIOR_BODY_ARMOR_ITEM_ID, &"melee_aggressor", &"engage")
	for index in range(6):
		_add_member(StringName("hostile_archer_%d" % index), "Hostile Archer %d" % index, &"hostile", _roll_creation_attributes(), USE_DEFAULT_ACTION_THRESHOLD, hostile_archer_skills, &"", 0, &"ash_longbow", ARCHER_BODY_ARMOR_ITEM_ID, &"ranged_archer", &"pressure")


func _add_member(
	member_id: StringName,
	display_name: String,
	faction_id: StringName,
	attrs: Dictionary,
	action_threshold: int,
	skill_configs: Array,
	profession_id: StringName,
	profession_rank: int,
	weapon_item_id: StringName,
	body_armor_item_id: StringName,
	ai_brain_id: StringName,
	ai_state_id: StringName
) -> void:
	var payload = _build_creation_payload(display_name, attrs, action_threshold)
	var member_state = CHARACTER_CREATION_SERVICE_SCRIPT.create_member_from_character_creation_payload(
		member_id,
		payload,
		_progression_content_bundle
	)
	member_state.faction_id = faction_id
	member_state.control_mode = &"ai"
	_apply_skills(member_state, skill_configs)
	_apply_profession_rank(member_state, profession_id, profession_rank, _collect_core_skill_ids(skill_configs))
	_equip_member(member_state, weapon_item_id, body_armor_item_id)
	party_state.set_member_state(member_state)
	if faction_id == &"hostile":
		hostile_member_ids.append(member_id)
	else:
		ally_member_ids.append(member_id)
	_ai_brain_by_member_id[member_id] = ai_brain_id
	_ai_state_by_member_id[member_id] = ai_state_id


func _set_member_mp_max(member_id: StringName, mp_max: int) -> void:
	var member_state = party_state.get_member_state(member_id)
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return
	member_state.progression.unit_base_attributes.set_attribute_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, mp_max)
	member_state.current_mp = mp_max


func _finalize_roster_identity() -> void:
	if party_state == null:
		return
	party_state.active_member_ids = ally_member_ids.duplicate()
	var fallback_main_id := _first_ally_member_id()
	if fallback_main_id == &"":
		return
	var main_member_id := _resolve_roster_option_ally_member_id(
		ROSTER_OPTION_MAIN_CHARACTER_MEMBER_ID,
		fallback_main_id
	)
	party_state.main_character_member_id = main_member_id
	party_state.leader_member_id = _resolve_roster_option_ally_member_id(
		ROSTER_OPTION_LEADER_MEMBER_ID,
		main_member_id
	)
	_bake_main_character_reroll_luck()


func _first_ally_member_id() -> StringName:
	return ally_member_ids[0] if not ally_member_ids.is_empty() else &""


func _resolve_roster_option_ally_member_id(option_key: String, fallback_member_id: StringName) -> StringName:
	var option_value = _get_roster_option(option_key, &"")
	var member_id := ProgressionDataUtils.to_string_name(option_value)
	if member_id == &"":
		return fallback_member_id
	if ally_member_ids.has(member_id):
		return member_id
	push_warning(
		"BattleSimFormalCombatFixture: roster option %s=%s is not a valid ally member; using %s." % [
			option_key,
			String(member_id),
			String(fallback_member_id),
		]
	)
	return fallback_member_id


func _get_roster_option(option_key: String, fallback_value: Variant = null) -> Variant:
	if _roster_options.has(option_key):
		return _roster_options[option_key]
	var string_name_key := StringName(option_key)
	if _roster_options.has(string_name_key):
		return _roster_options[string_name_key]
	return fallback_value


func _setup_attribute_roll_rng() -> void:
	_attribute_roll_rng.seed = int(_get_roster_option(
		ROSTER_OPTION_ATTRIBUTE_ROLL_SEED,
		DEFAULT_ATTRIBUTE_ROLL_SEED
	))
	_hp_roll_rng.seed = _attribute_roll_rng.seed + HP_ROLL_SEED_OFFSET


func _roll_creation_attributes() -> Dictionary:
	var attrs: Dictionary = {}
	for attribute_id in ATTRIBUTE_ROLL_IDS:
		attrs[String(attribute_id)] = _roll_creation_attribute_value()
	return attrs


func _roll_creation_attribute_value() -> int:
	var total := ATTRIBUTE_ROLL_OFFSET
	for _roll_index in range(ATTRIBUTE_ROLL_DICE_COUNT):
		total += _attribute_roll_rng.randi_range(1, ATTRIBUTE_ROLL_DICE_SIDES)
	return maxi(ATTRIBUTE_ROLL_VALUE_FLOOR, total)


func _bake_main_character_reroll_luck() -> void:
	if party_state == null or party_state.main_character_member_id == &"":
		return
	var member_state = party_state.get_member_state(party_state.main_character_member_id)
	if member_state == null or member_state.progression == null:
		return
	var attribute_service = ATTRIBUTE_SERVICE_SCRIPT.new()
	attribute_service.setup(member_state.progression)
	var creation_service = CHARACTER_CREATION_SERVICE_SCRIPT.new()
	var reroll_count = _get_roster_option(ROSTER_OPTION_MAIN_CHARACTER_REROLL_COUNT, 0)
	if not creation_service.bake_hidden_luck_at_birth(attribute_service, reroll_count):
		push_warning(
			"BattleSimFormalCombatFixture: failed to bake reroll luck for main character %s." % String(party_state.main_character_member_id)
		)


func _apply_skills(member_state, skill_configs: Array) -> void:
	if member_state == null or member_state.progression == null:
		return
	var progression_service = PROGRESSION_SERVICE_SCRIPT.new()
	progression_service.setup(member_state.progression, _skill_defs, _profession_defs)
	for skill_config in skill_configs:
		if skill_config is not Dictionary:
			continue
		var skill_id = ProgressionDataUtils.to_string_name(skill_config.get("skill_id", ""))
		var target_level = maxi(int(skill_config.get("level", 1)), 1)
		var is_core = bool(skill_config.get("is_core", false))
		if skill_id == &"":
			continue
		var skill_progress = member_state.progression.get_skill_progress(skill_id)
		if skill_progress == null or not bool(skill_progress.is_learned):
			progression_service.learn_skill(skill_id)
		if is_core:
			progression_service.set_skill_core(skill_id, true)
		var skill_def = _skill_defs.get(skill_id)
		var mastery_amount = _calculate_mastery_for_level(skill_def, target_level)
		if mastery_amount > 0:
			progression_service.grant_skill_mastery(skill_id, mastery_amount, &"training")
		if is_core:
			progression_service.set_skill_core(skill_id, true)
			_apply_core_max_growth(member_state, skill_id, target_level)
	progression_service.refresh_runtime_state()


func _apply_core_max_growth(member_state, skill_id: StringName, target_level: int) -> void:
	var skill_def = _skill_defs.get(skill_id)
	var skill_progress = member_state.progression.get_skill_progress(skill_id)
	if skill_def == null or skill_progress == null:
		return
	if bool(skill_progress.core_max_growth_claimed):
		return
	if target_level < int(skill_def.max_level):
		return
	var growth: Dictionary = skill_def.attribute_growth_progress if skill_def.attribute_growth_progress is Dictionary else {}
	if growth.is_empty():
		skill_progress.core_max_growth_claimed = true
		member_state.progression.set_skill_progress(skill_progress)
		return
	var growth_service = ATTRIBUTE_GROWTH_SERVICE_SCRIPT.new()
	growth_service.setup(member_state.progression)
	for attr_key in growth.keys():
		var attr_id = ProgressionDataUtils.to_string_name(attr_key)
		growth_service.apply_attribute_progress(attr_id, int(growth.get(attr_key, 0)), "battle_sim_fixture")
	skill_progress.core_max_growth_claimed = true
	member_state.progression.set_skill_progress(skill_progress)


func _apply_profession_rank(member_state, profession_id: StringName, rank: int, core_skill_ids: Array[StringName]) -> void:
	if member_state == null or member_state.progression == null or profession_id == &"" or rank <= 0:
		return
	var profession_progress = UNIT_PROFESSION_PROGRESS_SCRIPT.new()
	profession_progress.profession_id = profession_id
	profession_progress.rank = rank
	profession_progress.is_active = true
	for skill_id in core_skill_ids:
		profession_progress.add_core_skill(skill_id)
		var skill_progress = member_state.progression.get_skill_progress(skill_id)
		if skill_progress != null:
			skill_progress.is_core = true
			skill_progress.assigned_profession_id = profession_id
			member_state.progression.set_skill_progress(skill_progress)
	_apply_profession_granted_skills(member_state, profession_id, rank, profession_progress)
	member_state.progression.set_profession_progress(profession_progress)
	var hp_gain_total = _calculate_profession_hp_gain_total(member_state, profession_id, rank)
	var attributes = member_state.progression.unit_base_attributes
	attributes.set_attribute_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, attributes.get_attribute_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX) + hp_gain_total)
	member_state.current_hp = attributes.get_attribute_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)
	var progression_service = PROGRESSION_SERVICE_SCRIPT.new()
	progression_service.setup(member_state.progression, _skill_defs, _profession_defs)
	progression_service.refresh_runtime_state()


func _apply_profession_granted_skills(member_state, profession_id: StringName, rank: int, profession_progress) -> void:
	if member_state == null or member_state.progression == null or profession_id == &"" or profession_progress == null:
		return
	var profession_def = _profession_defs.get(profession_id)
	if profession_def == null:
		return
	for target_rank in range(1, rank + 1):
		for granted_skill in profession_def.get_granted_skills_for_rank(target_rank):
			if granted_skill == null or granted_skill.skill_id == &"":
				continue
			profession_progress.add_granted_skill(granted_skill.skill_id)
			var skill_progress = member_state.progression.get_skill_progress(granted_skill.skill_id)
			if skill_progress == null:
				skill_progress = UNIT_SKILL_PROGRESS_SCRIPT.new()
				skill_progress.skill_id = granted_skill.skill_id
			skill_progress.is_learned = true
			if skill_progress.profession_granted_by == &"":
				skill_progress.profession_granted_by = profession_id
			skill_progress.granted_source_type = UNIT_SKILL_PROGRESS_SCRIPT.GRANTED_SOURCE_PROFESSION
			skill_progress.granted_source_id = profession_id
			member_state.progression.set_skill_progress(skill_progress)


func _calculate_profession_hp_gain_total(member_state, profession_id: StringName, rank: int) -> int:
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return 0
	var profession_def = _profession_defs.get(profession_id)
	if profession_def == null:
		return 0
	var constitution = int(member_state.progression.unit_base_attributes.get_attribute_value(UnitBaseAttributes.CONSTITUTION))
	var hit_die_sides = maxi(int(profession_def.hit_die_sides), 1)
	var total := 0
	for _rank_index in range(maxi(rank, 0)):
		var hp_roll := _hp_roll_rng.randi_range(1, hit_die_sides)
		total += PROGRESSION_SERVICE_SCRIPT.calculate_profession_hit_point_gain(hp_roll, constitution)
	return total


func _equip_member(member_state, weapon_item_id: StringName, body_armor_item_id: StringName) -> void:
	if member_state == null:
		return
	var equipment_state = EQUIPMENT_STATE_SCRIPT.new()
	var equipped_any := false
	equipped_any = _equip_item_into_slot(equipment_state, member_state.member_id, weapon_item_id, EQUIPMENT_RULES_SCRIPT.MAIN_HAND, true, false) or equipped_any
	equipped_any = _equip_item_into_slot(equipment_state, member_state.member_id, body_armor_item_id, EQUIPMENT_RULES_SCRIPT.BODY, false, true) or equipped_any
	if equipped_any:
		member_state.equipment_state = equipment_state


func _equip_item_into_slot(
	equipment_state,
	member_id: StringName,
	item_id: StringName,
	entry_slot_id: StringName,
	require_weapon: bool,
	require_armor: bool
) -> bool:
	if equipment_state == null or item_id == &"":
		return false
	var item_def = _item_defs.get(item_id)
	if item_def == null or not item_def.is_equipment():
		return false
	if require_weapon and not item_def.is_weapon():
		return false
	if require_armor and not item_def.is_armor():
		return false
	if not item_def.get_equipment_slot_ids().has(entry_slot_id):
		return false
	var occupied_slots: Array[StringName] = item_def.get_final_occupied_slot_ids(entry_slot_id)
	var instance_id = StringName("sim_%s_%s" % [String(member_id), String(item_id)])
	var equipment_instance = EQUIPMENT_INSTANCE_STATE_SCRIPT.create(item_id, instance_id)
	return equipment_state.set_equipped_entry(entry_slot_id, item_id, occupied_slots, equipment_instance)


func _setup_character_management() -> void:
	if party_state == null:
		party_state = PARTY_STATE_SCRIPT.new()
	character_management = CHARACTER_MANAGEMENT_MODULE_SCRIPT.new()
	character_management.setup(
		party_state,
		_skill_defs,
		_profession_defs,
		_achievement_defs,
		_item_defs,
		{},
		Callable(),
		_progression_content_bundle
	)


func _restore_all_members_to_full_hp() -> void:
	if party_state == null or character_management == null:
		return
	for member_id_variant in party_state.member_states.keys():
		var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
		var member_state = party_state.get_member_state(member_id)
		if member_state == null:
			continue
		var snapshot = character_management.get_member_attribute_snapshot(member_id)
		member_state.current_hp = maxi(int(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)), 1)


func _apply_unit_runtime_metadata(unit_state, fallback_faction_id: StringName) -> void:
	if unit_state == null:
		return
	var member_id = unit_state.source_member_id
	unit_state.faction_id = fallback_faction_id
	unit_state.control_mode = &"ai"
	unit_state.ai_brain_id = ProgressionDataUtils.to_string_name(_ai_brain_by_member_id.get(member_id, unit_state.ai_brain_id))
	unit_state.ai_state_id = ProgressionDataUtils.to_string_name(_ai_state_by_member_id.get(member_id, unit_state.ai_state_id))


func _record_mastery(skill_id: StringName, amount: int) -> void:
	match String(skill_id):
		"charge":
			charge_mastery += amount
		"warrior_heavy_strike":
			heavy_mastery += amount
		"archer_aimed_shot":
			aimed_mastery += amount
		"archer_multishot":
			multishot_mastery += amount
		"basic_attack":
			basic_mastery += amount


func _calculate_mastery_for_level(skill_def, target_level: int) -> int:
	if skill_def == null or not skill_def.has_method("get_mastery_required_for_level"):
		return 0
	var total = 0
	for level in range(target_level):
		total += maxi(int(skill_def.get_mastery_required_for_level(level)), 0)
	return total


func _collect_core_skill_ids(skill_configs: Array) -> Array[StringName]:
	var result: Array[StringName] = []
	for skill_config in skill_configs:
		if skill_config is not Dictionary or not bool(skill_config.get("is_core", false)):
			continue
		var skill_id = ProgressionDataUtils.to_string_name(skill_config.get("skill_id", ""))
		if skill_id != &"":
			result.append(skill_id)
	return result


func _build_creation_payload(
	display_name: String,
	attrs: Dictionary,
	action_threshold: int
) -> Dictionary:
	var payload := {
		"display_name": display_name,
		"race_id": &"human",
		"subrace_id": &"common_human",
		"age_years": 24,
		"birth_at_world_step": 0,
		"age_profile_id": &"human_age_profile",
		"natural_age_stage_id": &"adult",
		"effective_age_stage_id": &"adult",
		"body_size_category": &"medium",
		"versatility_pick": &"",
		"strength": int(attrs.get("strength", 10)),
		"agility": int(attrs.get("agility", 10)),
		"constitution": int(attrs.get("constitution", 10)),
		"perception": int(attrs.get("perception", 10)),
		"intelligence": int(attrs.get("intelligence", 10)),
		"willpower": int(attrs.get("willpower", 10)),
	}
	if action_threshold > 0:
		payload["action_threshold"] = action_threshold
	return payload


func _attrs(strength: int, agility: int, constitution: int, perception: int, intelligence: int, willpower: int) -> Dictionary:
	return {
		"strength": strength,
		"agility": agility,
		"constitution": constitution,
		"perception": perception,
		"intelligence": intelligence,
		"willpower": willpower,
	}
