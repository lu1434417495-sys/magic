extends SceneTree

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const COMBAT_SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/combat_skill_def.gd")
const PASSIVE_SOURCE_CONTEXT_SCRIPT = preload("res://scripts/systems/progression/passive_source_context.gd")
const RACE_DEF_SCRIPT = preload("res://scripts/player/progression/race_def.gd")
const RACE_TRAIT_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/race_trait_resolver.gd")
const RACIAL_GRANTED_SKILL_SCRIPT = preload("res://scripts/player/progression/racial_granted_skill.gd")
const SKILL_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/skill_content_registry.gd")
const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/skill_def.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const DRAGON_BREATH_FIRE_CONE: StringName = &"dragon_breath_fire_cone"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_official_dragon_breath_skill_resources_are_schema_stable()
	_test_racial_skill_per_battle_charge_blocks_and_consumes()
	_test_racial_skill_per_turn_charge_refreshes_from_identity_projection()
	if _failures.is_empty():
		print("Dragon breath regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Dragon breath regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_official_dragon_breath_skill_resources_are_schema_stable() -> void:
	var registry = SKILL_CONTENT_REGISTRY_SCRIPT.new()
	_assert_true(registry.validate().is_empty(), "official skill registry should validate after dragon breath resources are loaded: %s" % str(registry.validate()))
	var expected_specs := {
		&"dragon_breath_fire_cone": {"damage_tag": &"fire", "area_pattern": &"cone"},
		&"dragon_breath_fire_line": {"damage_tag": &"fire", "area_pattern": &"line"},
		&"dragon_breath_freeze_cone": {"damage_tag": &"freeze", "area_pattern": &"cone"},
		&"dragon_breath_poison_cone": {"damage_tag": &"poison", "area_pattern": &"cone"},
		&"dragon_breath_acid_line": {"damage_tag": &"acid", "area_pattern": &"line"},
		&"dragon_breath_lightning_line": {"damage_tag": &"lightning", "area_pattern": &"line"},
	}
	var skill_defs: Dictionary = registry.get_skill_defs()
	for skill_id in expected_specs.keys():
		var skill_def := skill_defs.get(skill_id) as SKILL_DEF_SCRIPT
		var spec: Dictionary = expected_specs.get(skill_id, {})
		_assert_true(skill_def != null, "%s should be registered as official skill content." % String(skill_id))
		if skill_def == null:
			continue
		_assert_eq(skill_def.learn_source, &"subrace", "%s should be granted by Dragonborn subrace content." % String(skill_id))
		_assert_true(skill_def.combat_profile != null, "%s should declare a combat profile." % String(skill_id))
		if skill_def.combat_profile == null:
			continue
		_assert_eq(skill_def.combat_profile.target_mode, &"ground", "%s should use ground targeting." % String(skill_id))
		_assert_eq(skill_def.combat_profile.area_pattern, spec.get("area_pattern", &""), "%s should keep its configured area pattern." % String(skill_id))
		_assert_eq(skill_def.combat_profile.ap_cost, 1, "%s should cost 1 AP before charge gating." % String(skill_id))
		_assert_true(not skill_def.combat_profile.effect_defs.is_empty(), "%s should declare a damage effect." % String(skill_id))
		if skill_def.combat_profile.effect_defs.is_empty():
			continue
		var effect_def := skill_def.combat_profile.effect_defs[0] as COMBAT_EFFECT_DEF_SCRIPT
		_assert_true(effect_def != null, "%s damage effect should be a CombatEffectDef." % String(skill_id))
		if effect_def == null:
			continue
		_assert_eq(effect_def.effect_type, &"damage", "%s should use the normal damage effect pipeline." % String(skill_id))
		_assert_eq(effect_def.damage_tag, spec.get("damage_tag", &""), "%s should keep its damage tag." % String(skill_id))
		_assert_eq(effect_def.save_dc, 12, "%s should declare a dragon breath save DC." % String(skill_id))
		_assert_eq(effect_def.save_ability, UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, "%s should use constitution saves." % String(skill_id))
		_assert_eq(effect_def.save_tag, &"dragon_breath", "%s should use the dragon_breath save tag." % String(skill_id))
		_assert_true(effect_def.save_partial_on_success, "%s should keep half damage on successful save." % String(skill_id))


func _test_racial_skill_per_battle_charge_blocks_and_consumes() -> void:
	var skill_def := _build_dragon_breath_skill(DRAGON_BREATH_FIRE_CONE, &"fire", &"cone")
	var runtime := _build_runtime({skill_def.skill_id: skill_def})
	var state := _build_state(Vector2i(5, 3))
	runtime._state = state
	var caster := _build_unit(&"dragon_breath_user", &"player", Vector2i(1, 1), [skill_def.skill_id], 2)
	var target := _build_unit(&"dragon_breath_target", &"enemy", Vector2i(2, 1), [], 0)
	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, target)
	state.active_unit_id = caster.unit_id

	var command := _build_ground_skill_command(caster.unit_id, skill_def.skill_id, Vector2i(2, 1))
	caster.per_battle_charges[_racial_skill_charge_key(skill_def.skill_id)] = 0
	var blocked_preview = runtime.preview_command(command)
	_assert_true(blocked_preview != null and not blocked_preview.allowed, "dragon breath should be blocked when per-battle charge is 0.")
	_assert_log_contains(blocked_preview.log_lines, "次数已用尽", "blocked preview should report spent identity skill charges.")

	caster.per_battle_charges[_racial_skill_charge_key(skill_def.skill_id)] = 1
	var allowed_preview = runtime.preview_command(command)
	_assert_true(allowed_preview != null and allowed_preview.allowed, "dragon breath should preview as allowed while charge remains.")
	var hp_before := target.current_hp
	var batch = runtime.issue_command(command)
	_assert_true(target.current_hp < hp_before, "dragon breath should resolve through the normal ground skill damage path.")
	_assert_eq(int(caster.per_battle_charges.get(_racial_skill_charge_key(skill_def.skill_id), -1)), 0, "dragon breath should consume its per-battle identity skill charge after execution starts.")
	_assert_true(batch != null and batch.changed_unit_ids.has(caster.unit_id), "charge consumption should mark the caster changed through the normal skill command path.")

	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	caster.current_ap = 1
	var second_preview = runtime.preview_command(command)
	_assert_true(second_preview != null and not second_preview.allowed, "spent dragon breath should block the second cast.")
	_assert_log_contains(second_preview.log_lines, "次数已用尽", "second preview should keep the charge block reason.")


func _test_racial_skill_per_turn_charge_refreshes_from_identity_projection() -> void:
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	var grant = RACIAL_GRANTED_SKILL_SCRIPT.new()
	grant.skill_id = &"dragon_breath_freeze_cone"
	grant.charge_kind = RACIAL_GRANTED_SKILL_SCRIPT.CHARGE_KIND_PER_TURN
	grant.charges = 2
	var race = RACE_DEF_SCRIPT.new()
	race.race_id = &"dragon_fixture"
	var grants: Array[RACIAL_GRANTED_SKILL_SCRIPT] = []
	grants.append(grant)
	race.racial_granted_skills = grants
	var context = PASSIVE_SOURCE_CONTEXT_SCRIPT.new()
	context.race_def = race
	RACE_TRAIT_RESOLVER_SCRIPT.apply_to_unit(unit, context)
	var charge_key := _racial_skill_charge_key(grant.skill_id)
	_assert_eq(int(unit.per_turn_charges.get(charge_key, -1)), 2, "race projection should initialize current per-turn racial skill charges.")
	_assert_eq(int(unit.per_turn_charge_limits.get(charge_key, -1)), 2, "race projection should initialize per-turn racial skill charge limits.")
	unit.per_turn_charges[charge_key] = 0
	unit.reset_per_turn_charges()
	_assert_eq(int(unit.per_turn_charges.get(charge_key, -1)), 2, "turn start reset should refresh per-turn racial skill charges from limits.")


func _build_runtime(skill_defs: Dictionary) -> BATTLE_RUNTIME_MODULE_SCRIPT:
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(null, skill_defs)
	return runtime


func _build_state(map_size: Vector2i) -> BATTLE_STATE_SCRIPT:
	var state = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"dragon_breath_regression"
	state.phase = &"unit_acting"
	state.map_size = map_size
	state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
	state.cells = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.cell_columns = BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(state.cells)
	return state


func _build_cell(coord: Vector2i) -> BATTLE_CELL_STATE_SCRIPT:
	var cell = BATTLE_CELL_STATE_SCRIPT.new()
	cell.coord = coord
	cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_LAND
	cell.base_height = 4
	cell.height_offset = 0
	cell.recalculate_runtime_values()
	return cell


func _build_unit(
	unit_id: StringName,
	faction_id: StringName,
	coord: Vector2i,
	skill_ids: Array[StringName],
	current_ap: int
) -> BATTLE_UNIT_STATE_SCRIPT:
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = faction_id
	unit.current_hp = 40
	unit.current_mp = 0
	unit.current_stamina = 20
	unit.current_ap = current_ap
	unit.is_alive = true
	unit.known_active_skill_ids = skill_ids.duplicate()
	for skill_id in skill_ids:
		unit.known_skill_level_map[skill_id] = 1
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 40)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 0)
	unit.set_anchor_coord(coord)
	return unit


func _add_unit(runtime: BATTLE_RUNTIME_MODULE_SCRIPT, state: BATTLE_STATE_SCRIPT, unit: BATTLE_UNIT_STATE_SCRIPT) -> void:
	state.units[unit.unit_id] = unit
	runtime._grid_service.place_unit(state, unit, unit.coord, true)


func _build_dragon_breath_skill(skill_id: StringName, damage_tag: StringName, area_pattern: StringName) -> SKILL_DEF_SCRIPT:
	var effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect.effect_type = &"damage"
	effect.power = 12
	effect.damage_tag = damage_tag
	effect.save_dc = 12
	effect.save_ability = UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION
	effect.save_tag = &"dragon_breath"
	effect.save_partial_on_success = true
	var combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"ground"
	combat_profile.target_team_filter = &"enemy"
	combat_profile.range_value = 3
	combat_profile.area_pattern = area_pattern
	combat_profile.area_value = 1
	combat_profile.ap_cost = 1
	combat_profile.cooldown_tu = 0
	var effect_defs: Array[COMBAT_EFFECT_DEF_SCRIPT] = []
	effect_defs.append(effect)
	combat_profile.effect_defs = effect_defs
	var skill_def = SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = String(skill_id)
	skill_def.icon_id = skill_id
	skill_def.learn_source = &"subrace"
	skill_def.mastery_curve = PackedInt32Array([20])
	skill_def.tags = [&"dragon_breath", damage_tag] as Array[StringName]
	skill_def.combat_profile = combat_profile
	return skill_def


func _build_ground_skill_command(unit_id: StringName, skill_id: StringName, target_coord: Vector2i) -> BATTLE_COMMAND_SCRIPT:
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = unit_id
	command.skill_id = skill_id
	command.target_coord = target_coord
	return command


func _racial_skill_charge_key(skill_id: StringName) -> StringName:
	return StringName("racial_skill_%s" % String(skill_id))


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s Expected %s, got %s." % [message, str(expected), str(actual)])


func _assert_log_contains(lines: Array, needle: String, message: String) -> void:
	for line_variant in lines:
		if String(line_variant).contains(needle):
			return
	_failures.append("%s log=%s" % [message, str(lines)])
