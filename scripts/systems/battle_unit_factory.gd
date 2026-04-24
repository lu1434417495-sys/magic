class_name BattleUnitFactory
extends RefCounted

const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleUnitFactoryRuntime = preload("res://scripts/systems/battle_unit_factory_runtime.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

var _runtime_ref: WeakRef = null
var _runtime: BattleUnitFactoryRuntime = null:
	get:
		return _runtime_ref.get_ref() as BattleUnitFactoryRuntime if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null


func setup(runtime: BattleUnitFactoryRuntime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null


func build_ally_units(party_state, context: Dictionary) -> Array:
	if context.has("battle_party"):
		var battle_party: Variant = context.get("battle_party", [])
		if battle_party is Array and not battle_party.is_empty():
			return _normalize_unit_payloads(battle_party)

	var member_ids: Array = []
	if party_state != null and party_state.active_member_ids is Array:
		member_ids = party_state.active_member_ids
	if member_ids.is_empty():
		member_ids = _extract_ally_member_ids(context)

	var units: Array = []
	for index in range(member_ids.size()):
		var member_id := StringName(String(member_ids[index]))
		var member_state = party_state.get_member_state(member_id) if party_state != null else null
		if member_state != null and member_state.progression == null:
			continue
		var unit_state: BattleUnitState = _build_runtime_ally_unit(member_id, member_state, index, context)
		if unit_state != null:
			units.append(unit_state)
	return units


func refresh_battle_unit(unit_state: BattleUnitState) -> void:
	if unit_state == null or unit_state.source_member_id == &"" or _runtime == null:
		return
	var character_gateway: Object = _runtime.get_character_gateway()
	if character_gateway == null:
		return

	var member_state = character_gateway.get_member_state(unit_state.source_member_id)
	if member_state == null:
		return
	var snapshot = character_gateway.get_member_attribute_snapshot(unit_state.source_member_id)
	if snapshot == null:
		snapshot = ATTRIBUTE_SNAPSHOT_SCRIPT.new()

	unit_state.body_size = maxi(int(member_state.body_size), 1)
	unit_state.attribute_snapshot = snapshot
	unit_state.current_hp = clampi(unit_state.current_hp, 0, maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX), 1))
	unit_state.current_mp = clampi(unit_state.current_mp, 0, maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX), 0))
	unit_state.current_stamina = clampi(
		unit_state.current_stamina,
		0,
		maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX), 0)
	)
	unit_state.current_aura = clampi(
		unit_state.current_aura,
		0,
		maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX), 0)
	)
	unit_state.current_ap = maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS), 1)
	unit_state.current_move_points = clampi(
		unit_state.current_move_points,
		0,
		BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN
	)
	unit_state.known_active_skill_ids = _collect_known_active_skill_ids(member_state.progression)
	unit_state.known_skill_level_map = _collect_known_skill_level_map(member_state.progression)
	unit_state.refresh_footprint()


func refresh_known_skills(unit_state: BattleUnitState) -> void:
	if unit_state == null or unit_state.source_member_id == &"" or _runtime == null:
		return
	var character_gateway: Object = _runtime.get_character_gateway()
	if character_gateway == null:
		return
	var member_state = character_gateway.get_member_state(unit_state.source_member_id)
	if member_state == null:
		return
	unit_state.known_active_skill_ids = _collect_known_active_skill_ids(member_state.progression)
	unit_state.known_skill_level_map = _collect_known_skill_level_map(member_state.progression)


func build_enemy_units(encounter_anchor, context: Dictionary) -> Array:
	if context.has("enemy_units"):
		var explicit_enemy_units: Variant = context.get("enemy_units", [])
		if explicit_enemy_units is Array and not explicit_enemy_units.is_empty():
			return _normalize_unit_payloads(explicit_enemy_units)
	var enemy_count := maxi(int(context.get("enemy_unit_count", 1)), 1)
	var monster_name := String(context.get("monster_display_name", encounter_anchor.display_name if encounter_anchor != null else "敌人"))
	var units: Array = []
	for index in range(enemy_count):
		units.append(_build_runtime_enemy_unit(encounter_anchor, monster_name, index, context))
	return units


func _normalize_unit_payloads(payloads: Array) -> Array:
	var results: Array = []
	for payload in payloads:
		if payload == null:
			continue
		if payload is Dictionary:
			results.append(BattleUnitState.from_dict(payload))
		elif payload.has_method("to_dict"):
			results.append(BattleUnitState.from_dict(payload.to_dict()))
		else:
			results.append(payload)
	return results


func build_terrain_data(encounter_anchor, seed: int, context: Dictionary) -> Dictionary:
	var terrain_context := context.duplicate(true)
	terrain_context.erase("map_size")
	var terrain_data: Dictionary = {}
	if _runtime != null and _runtime.get_terrain_generator() != null:
		terrain_data = _runtime.get_terrain_generator().generate(encounter_anchor, seed, terrain_context)
	return _apply_terrain_generation_overrides(terrain_data, terrain_context)


func _apply_terrain_generation_overrides(terrain_data: Dictionary, context: Dictionary) -> Dictionary:
	if terrain_data.is_empty():
		return {}
	var terrain_result := terrain_data.duplicate(true)
	var ally_spawns: Variant = context.get("ally_spawns", null)
	if ally_spawns is Array and not ally_spawns.is_empty():
		terrain_result["ally_spawns"] = (ally_spawns as Array).duplicate(true)
	var enemy_spawns: Variant = context.get("enemy_spawns", null)
	if enemy_spawns is Array and not enemy_spawns.is_empty():
		terrain_result["enemy_spawns"] = (enemy_spawns as Array).duplicate(true)
	return terrain_result


func _build_runtime_ally_unit(member_id: StringName, member_state, index: int, context: Dictionary):
	var unit_state = BattleUnitState.new()
	unit_state.unit_id = member_id if member_id != &"" else StringName("ally_%d" % [index + 1])
	unit_state.source_member_id = member_id
	if member_state != null and String(member_state.display_name) != "":
		unit_state.display_name = String(member_state.display_name)
	else:
		unit_state.display_name = "队员%d" % [index + 1]
	unit_state.faction_id = &"player"
	unit_state.control_mode = member_state.control_mode if member_state != null and member_state.control_mode != &"" else &"manual"
	unit_state.body_size = maxi(int(member_state.body_size), 1) if member_state != null else 1
	unit_state.refresh_footprint()
	var snapshot := _build_member_attribute_snapshot(member_state, context)
	unit_state.attribute_snapshot = snapshot
	var hp_max := maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX), 1)
	var mp_max := maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX), 0)
	var stamina_max := maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX), 0)
	var action_points := maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS), 1)
	unit_state.current_hp = clampi(int(member_state.current_hp) if member_state != null else hp_max, 0, hp_max)
	unit_state.current_mp = clampi(int(member_state.current_mp) if member_state != null else mp_max, 0, mp_max)
	unit_state.current_stamina = stamina_max
	unit_state.current_aura = maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX), 0)
	unit_state.current_ap = action_points
	unit_state.current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN
	unit_state.action_threshold = int(context.get("default_ally_action_threshold", BattleUnitState.DEFAULT_ACTION_THRESHOLD))
	unit_state.known_active_skill_ids = _collect_known_active_skill_ids(member_state.progression if member_state != null else null)
	unit_state.known_skill_level_map = _collect_known_skill_level_map(member_state.progression if member_state != null else null)
	unit_state.movement_tags = _extract_movement_tags(context.get("ally_movement_tags", []))
	if unit_state.known_active_skill_ids.is_empty():
		var default_skills: Variant = context.get("default_active_skill_ids", [])
		if default_skills is Array:
			for skill_id in default_skills:
				var normalized_skill_id := StringName(String(skill_id))
				unit_state.known_active_skill_ids.append(normalized_skill_id)
				unit_state.known_skill_level_map[normalized_skill_id] = 1
	unit_state.is_alive = unit_state.current_hp > 0
	return unit_state


func _build_runtime_enemy_unit(encounter_anchor, monster_name: String, index: int, context: Dictionary):
	var unit_state = BattleUnitState.new()
	var anchor_id := String(encounter_anchor.entity_id) if encounter_anchor != null else "wild"
	unit_state.unit_id = StringName("%s_%02d" % [anchor_id, index + 1])
	unit_state.source_member_id = &""
	unit_state.display_name = monster_name if index == 0 else "%s·从属%d" % [monster_name, index + 1]
	unit_state.faction_id = StringName(String(encounter_anchor.faction_id)) if encounter_anchor != null and String(encounter_anchor.faction_id) != "" else &"hostile"
	unit_state.control_mode = &"ai"
	unit_state.body_size = 1
	unit_state.refresh_footprint()
	var hp_max := int(context.get("default_enemy_hp", 12))
	var mp_max := int(context.get("default_enemy_mp", 0))
	var stamina_max := int(context.get("default_enemy_stamina", 0))
	var action_points := int(context.get("default_enemy_ap", 1))
	for attribute_id in UNIT_BASE_ATTRIBUTES_SCRIPT.BASE_ATTRIBUTE_IDS:
		var attribute_key := "default_enemy_%s" % String(attribute_id)
		unit_state.attribute_snapshot.set_value(attribute_id, int(context.get(attribute_key, 4)))
	unit_state.attribute_snapshot.set_value(&"hp_max", maxi(hp_max, 1))
	unit_state.attribute_snapshot.set_value(&"mp_max", maxi(mp_max, 0))
	unit_state.attribute_snapshot.set_value(&"stamina_max", maxi(stamina_max, 0))
	unit_state.attribute_snapshot.set_value(&"action_points", maxi(action_points, 1))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, int(context.get("default_enemy_attack_bonus", 4)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, int(context.get("default_enemy_armor_class", 12)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_AC_BONUS, int(context.get("default_enemy_armor_ac_bonus", 0)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SHIELD_AC_BONUS, int(context.get("default_enemy_shield_ac_bonus", 0)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DODGE_BONUS, int(context.get("default_enemy_dodge_bonus", 0)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DEFLECTION_BONUS, int(context.get("default_enemy_deflection_bonus", 0)))
	unit_state.attribute_snapshot.set_value(&"fire_resistance", int(context.get("default_fire_resistance", 0)))
	unit_state.action_threshold = int(context.get("default_enemy_action_threshold", BattleUnitState.DEFAULT_ACTION_THRESHOLD))
	unit_state.current_hp = hp_max
	unit_state.current_mp = mp_max
	unit_state.current_stamina = stamina_max
	unit_state.current_ap = action_points
	unit_state.current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN
	unit_state.is_alive = unit_state.current_hp > 0
	unit_state.movement_tags = _extract_movement_tags(context.get("enemy_movement_tags", []))
	var enemy_skills: Variant = context.get("enemy_skill_ids", [])
	if enemy_skills is Array:
		unit_state.known_active_skill_ids.clear()
		for skill_id in enemy_skills:
			var normalized_skill_id := StringName(String(skill_id))
			unit_state.known_active_skill_ids.append(normalized_skill_id)
			unit_state.known_skill_level_map[normalized_skill_id] = 1
	if unit_state.known_active_skill_ids.is_empty():
		unit_state.known_active_skill_ids = _pick_default_enemy_skill_ids()
		for skill_id in unit_state.known_active_skill_ids:
			unit_state.known_skill_level_map[skill_id] = 1
	return unit_state


func _pick_default_enemy_skill_ids() -> Array[StringName]:
	var preferred_skill_ids: Array[StringName] = [
		&"warrior_heavy_strike",
		&"warrior_combo_strike",
		&"warrior_guard_break",
	]
	for preferred_skill_id in preferred_skill_ids:
		var preferred_skill := _skill_def_from_runtime(preferred_skill_id)
		if _is_valid_enemy_skill(preferred_skill):
			return [preferred_skill_id]

	for skill_id_str in ProgressionDataUtils.sorted_string_keys(_runtime.get_skill_defs() if _runtime != null and _runtime.get_skill_defs() is Dictionary else {}):
		var skill_id := StringName(skill_id_str)
		var skill_def := _skill_def_from_runtime(skill_id)
		if _is_valid_enemy_skill(skill_def):
			return [skill_id]

	return []


func _is_valid_enemy_skill(skill_def: SkillDef) -> bool:
	if skill_def == null:
		return false
	if skill_def.skill_type != &"active":
		return false
	if not skill_def.can_use_in_combat():
		return false
	if skill_def.combat_profile == null:
		return false
	if skill_def.combat_profile.target_mode != &"unit":
		return false
	return skill_def.combat_profile.target_team_filter == &"enemy"


func _extract_ally_member_ids(context: Dictionary) -> Array:
	var member_ids: Variant = context.get("ally_member_ids", context.get("battle_member_ids", context.get("member_ids", [])))
	if member_ids is Array:
		return member_ids
	return []


func _skill_def_from_runtime(skill_id: StringName) -> SkillDef:
	if _runtime == null or not (_runtime.get_skill_defs() is Dictionary):
		return null
	return _runtime.get_skill_defs().get(skill_id) as SkillDef


func _build_member_attribute_snapshot(member_state, context: Dictionary) -> AttributeSnapshot:
	var snapshot := ATTRIBUTE_SNAPSHOT_SCRIPT.new()
	if member_state == null:
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, maxi(int(context.get("default_ally_hp", 24)), 1))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, maxi(int(context.get("default_ally_mp", 0)), 0))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, maxi(int(context.get("default_ally_stamina", 0)), 0))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX, maxi(int(context.get("default_ally_aura", 0)), 0))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS, maxi(int(context.get("default_ally_ap", 6)), 1))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, int(context.get("default_ally_attack_bonus", 4)))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, int(context.get("default_ally_armor_class", 10)))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_AC_BONUS, int(context.get("default_ally_armor_ac_bonus", 0)))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SHIELD_AC_BONUS, int(context.get("default_ally_shield_ac_bonus", 0)))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DODGE_BONUS, int(context.get("default_ally_dodge_bonus", 0)))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DEFLECTION_BONUS, int(context.get("default_ally_deflection_bonus", 0)))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.FIRE_RESISTANCE, int(context.get("default_ally_fire_resistance", 0)))
		return snapshot

	if _runtime != null and _runtime.get_character_gateway() != null:
		var runtime_snapshot = _runtime.get_character_gateway().get_member_attribute_snapshot(member_state.member_id)
		if runtime_snapshot != null:
			return runtime_snapshot

	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, maxi(int(member_state.current_hp), 1))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, maxi(int(member_state.current_mp), 0))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, maxi(int(context.get("default_ally_stamina", 0)), 0))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX, maxi(int(context.get("default_ally_aura", 0)), 0))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS, maxi(int(context.get("default_ally_ap", 6)), 1))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, int(context.get("default_ally_attack_bonus", 4)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, int(context.get("default_ally_armor_class", 10)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_AC_BONUS, int(context.get("default_ally_armor_ac_bonus", 0)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SHIELD_AC_BONUS, int(context.get("default_ally_shield_ac_bonus", 0)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DODGE_BONUS, int(context.get("default_ally_dodge_bonus", 0)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DEFLECTION_BONUS, int(context.get("default_ally_deflection_bonus", 0)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.FIRE_RESISTANCE, int(context.get("default_ally_fire_resistance", 0)))
	return snapshot


func _collect_known_active_skill_ids(progression_state) -> Array[StringName]:
	var skill_ids: Array[StringName] = []
	if progression_state == null:
		return skill_ids

	for skill_key in ProgressionDataUtils.sorted_string_keys(progression_state.skills):
		var skill_id: StringName = StringName(skill_key)
		var skill_progress: Variant = progression_state.get_skill_progress(skill_id)
		var skill_def: SkillDef = _skill_def_from_runtime(skill_id)
		if skill_progress == null or skill_def == null:
			continue
		if not skill_progress.is_learned:
			continue
		if skill_def.skill_type != &"active":
			continue
		if not skill_def.can_use_in_combat():
			continue
		skill_ids.append(skill_id)

	return skill_ids


func _collect_known_skill_level_map(progression_state) -> Dictionary:
	var skill_levels: Dictionary = {}
	if progression_state == null:
		return skill_levels

	for skill_key in ProgressionDataUtils.sorted_string_keys(progression_state.skills):
		var skill_id: StringName = StringName(skill_key)
		var skill_progress: Variant = progression_state.get_skill_progress(skill_id)
		var skill_def: SkillDef = _skill_def_from_runtime(skill_id)
		if skill_progress == null or skill_def == null:
			continue
		if not skill_progress.is_learned:
			continue
		if skill_def.skill_type != &"active":
			continue
		skill_levels[skill_id] = int(skill_progress.skill_level)

	return skill_levels


func _extract_movement_tags(raw_tags: Variant) -> Array[StringName]:
	var tags: Array[StringName] = []
	if raw_tags is not Array:
		return tags
	for raw_tag in raw_tags:
		var normalized_tag := StringName(String(raw_tag))
		if normalized_tag == &"" or tags.has(normalized_tag):
			continue
		tags.append(normalized_tag)
	return tags
