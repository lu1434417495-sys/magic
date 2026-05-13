class_name BattleUnitFactory
extends RefCounted

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleUnitFactoryRuntime = preload("res://scripts/systems/battle/runtime/battle_unit_factory_runtime.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const BODY_SIZE_RULES_SCRIPT = preload("res://scripts/systems/progression/body_size_rules.gd")
const PASSIVE_SOURCE_CONTEXT_SCRIPT = preload("res://scripts/systems/progression/passive_source_context.gd")
const PASSIVE_STATUS_ORCHESTRATOR_SCRIPT = preload("res://scripts/systems/battle/runtime/passive_status_orchestrator.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BATTLE_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_range_service.gd")
const EQUIPMENT_STATE_SCRIPT = preload("res://scripts/player/equipment/equipment_state.gd")
const EquipmentRules = preload("res://scripts/player/equipment/equipment_rules.gd")
const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")
const BodySizeRules = BODY_SIZE_RULES_SCRIPT
const PassiveSourceContext = PASSIVE_SOURCE_CONTEXT_SCRIPT
const BASIC_ATTACK_SKILL_ID: StringName = &"basic_attack"
const DEFAULT_ENEMY_MELEE_DAMAGE_TAG: StringName = &"physical_slash"

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
	var equipment_view = _ensure_unit_equipment_view(unit_state, member_state)
	var snapshot = _build_member_attribute_snapshot(member_state, {}, equipment_view)
	if snapshot == null:
		snapshot = ATTRIBUTE_SNAPSHOT_SCRIPT.new()

	_apply_member_identity_projection(unit_state, member_state)
	unit_state.attribute_snapshot = snapshot
	refresh_weapon_projection(unit_state)
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
	unit_state.current_ap = clampi(
		unit_state.current_ap,
		0,
		maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS), 1)
	)
	unit_state.action_threshold = _resolve_action_threshold_from_snapshot(snapshot)
	unit_state.current_move_points = clampi(
		unit_state.current_move_points,
		0,
		BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN
	)
	unit_state.known_active_skill_ids = _collect_known_active_skill_ids(member_state.progression)
	unit_state.known_skill_level_map = _collect_known_skill_level_map(member_state.progression)
	unit_state.known_skill_lock_hit_bonus_map = _collect_known_skill_lock_hit_bonus_map(member_state.progression)
	_sync_unlocked_resources_from_progression(unit_state, member_state.progression)
	_filter_skills_by_equipment_requirements(unit_state)
	_ensure_basic_attack_skill(unit_state)
	_sync_passive_battle_statuses(unit_state, member_state.progression, member_state)
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
	unit_state.known_skill_lock_hit_bonus_map = _collect_known_skill_lock_hit_bonus_map(member_state.progression)
	_sync_unlocked_resources_from_progression(unit_state, member_state.progression)
	_filter_skills_by_equipment_requirements(unit_state)
	_ensure_basic_attack_skill(unit_state)
	_sync_passive_battle_statuses(unit_state, member_state.progression, member_state)


func refresh_weapon_projection(unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	_apply_member_weapon_projection(unit_state, unit_state.source_member_id, unit_state.get_equipment_view())


func refresh_equipment_projection(unit_state: BattleUnitState) -> void:
	if unit_state == null or unit_state.source_member_id == &"" or _runtime == null:
		return
	var character_gateway: Object = _runtime.get_character_gateway()
	if character_gateway == null:
		return
	var member_state = character_gateway.get_member_state(unit_state.source_member_id)
	if member_state == null:
		return
	var snapshot = _build_member_attribute_snapshot(member_state, {}, unit_state.get_equipment_view())
	if snapshot == null:
		snapshot = ATTRIBUTE_SNAPSHOT_SCRIPT.new()
	var previous_snapshot = unit_state.attribute_snapshot
	var previous_hp_max := int(previous_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)) if previous_snapshot != null else maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX), 1)
	var previous_mp_max := int(previous_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX)) if previous_snapshot != null else maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX), 0)
	var previous_stamina_max := int(previous_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX)) if previous_snapshot != null else maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX), 0)
	var previous_aura_max := int(previous_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX)) if previous_snapshot != null else maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX), 0)
	unit_state.attribute_snapshot = snapshot
	refresh_weapon_projection(unit_state)
	var hp_max := maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX), 1)
	var mp_max := maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX), 0)
	var stamina_max := maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX), 0)
	var aura_max := maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX), 0)
	if hp_max < previous_hp_max:
		unit_state.current_hp = clampi(unit_state.current_hp, 0, hp_max)
	else:
		unit_state.current_hp = maxi(unit_state.current_hp, 0)
	if mp_max < previous_mp_max:
		unit_state.current_mp = clampi(unit_state.current_mp, 0, mp_max)
	else:
		unit_state.current_mp = maxi(unit_state.current_mp, 0)
	if stamina_max < previous_stamina_max:
		unit_state.current_stamina = clampi(unit_state.current_stamina, 0, stamina_max)
	else:
		unit_state.current_stamina = maxi(unit_state.current_stamina, 0)
	if aura_max < previous_aura_max:
		unit_state.current_aura = clampi(unit_state.current_aura, 0, aura_max)
	else:
		unit_state.current_aura = maxi(unit_state.current_aura, 0)
	unit_state.action_threshold = _resolve_action_threshold_from_snapshot(snapshot)
	unit_state.known_active_skill_ids = _collect_known_active_skill_ids(member_state.progression)
	unit_state.known_skill_level_map = _collect_known_skill_level_map(member_state.progression)
	unit_state.known_skill_lock_hit_bonus_map = _collect_known_skill_lock_hit_bonus_map(member_state.progression)
	_sync_unlocked_resources_from_progression(unit_state, member_state.progression)
	_filter_skills_by_equipment_requirements(unit_state)
	_ensure_basic_attack_skill(unit_state)
	_sync_passive_battle_statuses(unit_state, member_state.progression, member_state)
	unit_state.refresh_footprint()


func build_enemy_units(encounter_anchor, context: Dictionary) -> Array:
	if context.has("enemy_units"):
		var explicit_enemy_units: Variant = context.get("enemy_units", [])
		if explicit_enemy_units is Array and not explicit_enemy_units.is_empty():
			return _normalize_unit_payloads(explicit_enemy_units)
	var enemy_count := maxi(int(context.get("enemy_unit_count", 1)), 1)
	var monster_name := String(encounter_anchor.display_name if encounter_anchor != null else "敌人")
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
		elif payload is BattleUnitState:
			results.append((payload as BattleUnitState).clone())
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
	_apply_member_identity_projection(unit_state, member_state)
	unit_state.set_equipment_view(_get_member_equipment_state(member_state))
	var snapshot := _build_member_attribute_snapshot(member_state, context, unit_state.get_equipment_view())
	unit_state.attribute_snapshot = snapshot
	_apply_member_weapon_projection(unit_state, member_id, unit_state.get_equipment_view())
	var hp_max := maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX), 1)
	var mp_max := maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX), 0)
	var stamina_max := maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX), 0)
	var aura_max := maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX), 0)
	var action_points := maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS), 1)
	unit_state.current_hp = clampi(int(member_state.current_hp) if member_state != null else hp_max, 0, hp_max)
	unit_state.current_mp = clampi(int(member_state.current_mp) if member_state != null else mp_max, 0, mp_max)
	unit_state.current_stamina = stamina_max
	unit_state.current_aura = clampi(int(member_state.current_aura) if member_state != null else aura_max, 0, aura_max)
	unit_state.current_ap = action_points
	unit_state.current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN
	unit_state.action_threshold = _resolve_action_threshold_from_snapshot(
		snapshot,
		int(context.get("default_ally_action_threshold", BattleUnitState.DEFAULT_ACTION_THRESHOLD))
	)
	unit_state.known_active_skill_ids = _collect_known_active_skill_ids(member_state.progression if member_state != null else null)
	unit_state.known_skill_level_map = _collect_known_skill_level_map(member_state.progression if member_state != null else null)
	unit_state.known_skill_lock_hit_bonus_map = _collect_known_skill_lock_hit_bonus_map(member_state.progression if member_state != null else null)
	_sync_unlocked_resources_from_progression(unit_state, member_state.progression if member_state != null else null)
	_sync_passive_battle_statuses(unit_state, member_state.progression if member_state != null else null, member_state)
	_filter_skills_by_equipment_requirements(unit_state)
	unit_state.movement_tags = _extract_movement_tags(context.get("ally_movement_tags", []))
	if unit_state.known_active_skill_ids.is_empty():
		var default_skills: Variant = context.get("default_active_skill_ids", [])
		if default_skills is Array:
			for skill_id in default_skills:
				var normalized_skill_id := StringName(String(skill_id))
				unit_state.known_active_skill_ids.append(normalized_skill_id)
				unit_state.known_skill_level_map[normalized_skill_id] = 1
	_ensure_basic_attack_skill(unit_state)
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
	unit_state.body_size = BattleUnitState.BODY_SIZE_MEDIUM
	unit_state.body_size_category = BodySizeRules.BODY_SIZE_CATEGORY_MEDIUM
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
	unit_state.attribute_snapshot.set_value(
		ATTRIBUTE_SERVICE_SCRIPT.SPELL_PROFICIENCY_BONUS,
		int(context.get(
			"default_enemy_spell_proficiency_bonus",
			ATTRIBUTE_SNAPSHOT_SCRIPT.calculate_spell_proficiency_bonus(int(context.get("default_enemy_character_level", 0)))
		))
	)
	if _has_explicit_default_enemy_weapon_context(context):
		var enemy_weapon_attack_range := maxi(int(context.get("default_enemy_weapon_attack_range", 1)), 0)
		_apply_enemy_natural_weapon_projection(
			unit_state,
			ProgressionDataUtils.to_string_name(context.get("default_enemy_weapon_profile_type_id", "natural_weapon")),
			ProgressionDataUtils.to_string_name(context.get("default_enemy_weapon_physical_damage_tag", DEFAULT_ENEMY_MELEE_DAMAGE_TAG)),
			enemy_weapon_attack_range,
			ProgressionDataUtils.to_string_name(context.get("default_enemy_weapon_family", ""))
		)
	else:
		unit_state.set_unarmed_weapon_projection()
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
	_ensure_basic_attack_skill(unit_state)
	_ensure_enemy_basic_attack_affordability(unit_state)
	_sync_enemy_unlocked_resources(unit_state)
	return unit_state


func _pick_default_enemy_skill_ids() -> Array[StringName]:
	var preferred_skill_ids: Array[StringName] = [
		BASIC_ATTACK_SKILL_ID,
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


func _filter_skills_by_equipment_requirements(unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	var filtered: Array[StringName] = []
	for skill_id in unit_state.known_active_skill_ids:
		var skill_def := _skill_def_from_runtime(skill_id)
		if skill_def == null or skill_def.combat_profile == null:
			continue
		if bool(skill_def.combat_profile.requires_equipped_shield):
			if not _unit_has_equipped_shield(unit_state):
				continue
		if not BATTLE_RANGE_SERVICE_SCRIPT.unit_matches_required_weapon_families(unit_state, skill_def.combat_profile.required_weapon_families):
			continue
		if BATTLE_RANGE_SERVICE_SCRIPT.requires_current_melee_weapon(skill_def) \
				and not BATTLE_RANGE_SERVICE_SCRIPT.unit_has_melee_weapon(unit_state):
			continue
		filtered.append(skill_id)
	unit_state.known_active_skill_ids = filtered


func _unit_has_equipped_shield(unit_state: BattleUnitState) -> bool:
	var equipment_view = unit_state.get_equipment_view()
	if equipment_view == null:
		return false
	var offhand_item_id: StringName = equipment_view.get_equipped_item_id(EquipmentRules.OFF_HAND)
	if offhand_item_id == &"":
		return false
	var item_defs: Dictionary = _runtime.get_item_defs() if _runtime != null else {}
	var item_def := item_defs.get(offhand_item_id) as ItemDef
	if item_def == null:
		return false
	return item_def.get_tags().has(&"shield")


func _extract_ally_member_ids(context: Dictionary) -> Array:
	var member_ids: Variant = context.get("ally_member_ids", [])
	if member_ids is Array:
		return member_ids
	return []


func _skill_def_from_runtime(skill_id: StringName) -> SkillDef:
	if _runtime == null or not (_runtime.get_skill_defs() is Dictionary):
		return null
	return _runtime.get_skill_defs().get(skill_id) as SkillDef


func _build_member_attribute_snapshot(member_state, context: Dictionary, equipment_view: Variant = null) -> AttributeSnapshot:
	var snapshot := ATTRIBUTE_SNAPSHOT_SCRIPT.new()
	if member_state == null:
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, maxi(int(context.get("default_ally_hp", 24)), 1))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, maxi(int(context.get("default_ally_mp", 0)), 0))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, maxi(int(context.get("default_ally_stamina", 0)), 0))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX, maxi(int(context.get("default_ally_aura", 0)), 0))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS, maxi(int(context.get("default_ally_ap", 6)), 1))
		snapshot.set_value(
			ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD,
			maxi(
				int(context.get("default_ally_action_threshold", ATTRIBUTE_SERVICE_SCRIPT.DEFAULT_CHARACTER_ACTION_THRESHOLD)),
				1
			)
		)
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, int(context.get("default_ally_attack_bonus", 4)))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, int(context.get("default_ally_armor_class", 10)))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_AC_BONUS, int(context.get("default_ally_armor_ac_bonus", 0)))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SHIELD_AC_BONUS, int(context.get("default_ally_shield_ac_bonus", 0)))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DODGE_BONUS, int(context.get("default_ally_dodge_bonus", 0)))
		snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DEFLECTION_BONUS, int(context.get("default_ally_deflection_bonus", 0)))
		return snapshot

	if _runtime != null and _runtime.get_character_gateway() != null:
		var character_gateway: Object = _runtime.get_character_gateway()
		var runtime_snapshot = null
		if character_gateway.has_method("get_member_attribute_snapshot_for_equipment_view"):
			runtime_snapshot = character_gateway.call(
				"get_member_attribute_snapshot_for_equipment_view",
				member_state.member_id,
				equipment_view
			)
		if runtime_snapshot != null:
			return runtime_snapshot

	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, maxi(int(member_state.current_hp), 1))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, maxi(int(member_state.current_mp), 0))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, maxi(int(context.get("default_ally_stamina", 0)), 0))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX, maxi(int(context.get("default_ally_aura", 0)), 0))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS, maxi(int(context.get("default_ally_ap", 6)), 1))
	snapshot.set_value(
		ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD,
		maxi(
			int(context.get("default_ally_action_threshold", ATTRIBUTE_SERVICE_SCRIPT.DEFAULT_CHARACTER_ACTION_THRESHOLD)),
			1
		)
	)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, int(context.get("default_ally_attack_bonus", 4)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, int(context.get("default_ally_armor_class", 10)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_AC_BONUS, int(context.get("default_ally_armor_ac_bonus", 0)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SHIELD_AC_BONUS, int(context.get("default_ally_shield_ac_bonus", 0)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DODGE_BONUS, int(context.get("default_ally_dodge_bonus", 0)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DEFLECTION_BONUS, int(context.get("default_ally_deflection_bonus", 0)))
	return snapshot


func _apply_member_weapon_projection(unit_state: BattleUnitState, member_id: StringName, equipment_view: Variant = null) -> void:
	if unit_state == null:
		return
	if member_id == &"" or _runtime == null:
		unit_state.clear_weapon_projection()
		return
	var character_gateway: Object = _runtime.get_character_gateway()
	if character_gateway == null:
		unit_state.clear_weapon_projection()
		return
	if character_gateway.has_method("get_member_weapon_projection_for_equipment_view"):
		var projection = character_gateway.call("get_member_weapon_projection_for_equipment_view", member_id, equipment_view)
		unit_state.apply_weapon_projection(projection if projection is Dictionary else {})
		return
	unit_state.clear_weapon_projection()


func _apply_member_identity_projection(unit_state: BattleUnitState, member_state) -> void:
	if unit_state == null:
		return
	if member_state == null:
		unit_state.set_body_size_category(BodySizeRules.BODY_SIZE_CATEGORY_SMALL)
		unit_state.versatility_pick = &""
		return
	var projected_category := ProgressionDataUtils.to_string_name(member_state.body_size_category)
	if not unit_state.set_body_size_category(projected_category):
		unit_state.body_size = maxi(int(member_state.body_size), 1)
		unit_state.sync_body_size_category_from_body_size()
		unit_state.refresh_footprint()
	unit_state.versatility_pick = ProgressionDataUtils.to_string_name(member_state.versatility_pick)


func _ensure_unit_equipment_view(unit_state: BattleUnitState, member_state):
	if unit_state == null:
		return EQUIPMENT_STATE_SCRIPT.new()
	if not bool(unit_state.equipment_view_initialized):
		unit_state.set_equipment_view(_get_member_equipment_state(member_state))
	return unit_state.get_equipment_view()


func _get_member_equipment_state(member_state):
	if member_state == null:
		return EQUIPMENT_STATE_SCRIPT.new()
	var member_equipment = member_state.equipment_state
	if member_equipment != null \
		and member_equipment is Object \
		and member_equipment.has_method("get_equipped_item_id"):
		return member_equipment
	return EQUIPMENT_STATE_SCRIPT.new()


func _apply_enemy_natural_weapon_projection(
	unit_state: BattleUnitState,
	profile_type_id: StringName,
	damage_tag: StringName,
	attack_range: int,
	family: StringName = &""
) -> void:
	if unit_state == null:
		return
	if attack_range <= 0 and damage_tag == &"":
		unit_state.clear_weapon_projection()
		return
	unit_state.set_natural_weapon_projection(
		profile_type_id if profile_type_id != &"" else &"natural_weapon",
		damage_tag,
		attack_range,
		{},
		family
	)


func _ensure_basic_attack_skill(unit_state: BattleUnitState) -> void:
	if unit_state == null or not _runtime_has_skill(BASIC_ATTACK_SKILL_ID):
		return
	if not unit_state.known_active_skill_ids.has(BASIC_ATTACK_SKILL_ID):
		unit_state.known_active_skill_ids.append(BASIC_ATTACK_SKILL_ID)
	unit_state.known_skill_level_map[BASIC_ATTACK_SKILL_ID] = 0


func _ensure_enemy_basic_attack_affordability(unit_state: BattleUnitState) -> void:
	if unit_state == null or not unit_state.known_active_skill_ids.has(BASIC_ATTACK_SKILL_ID):
		return
	var basic_attack := _skill_def_from_runtime(BASIC_ATTACK_SKILL_ID)
	if basic_attack == null or basic_attack.combat_profile == null:
		return
	var skill_level := maxi(int(unit_state.known_skill_level_map.get(BASIC_ATTACK_SKILL_ID, 0)), 0)
	var costs: Dictionary = basic_attack.combat_profile.get_effective_resource_costs(skill_level)
	var stamina_cost := maxi(int(costs.get("stamina_cost", basic_attack.combat_profile.stamina_cost)), 0)
	if stamina_cost <= 0:
		return
	if unit_state.attribute_snapshot != null:
		var stamina_max := int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX))
		if stamina_max < stamina_cost:
			unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, stamina_cost)
	if int(unit_state.current_stamina) < stamina_cost:
		unit_state.current_stamina = stamina_cost


func _sync_unlocked_resources_from_progression(unit_state: BattleUnitState, progression_state) -> void:
	if unit_state == null:
		return
	if progression_state == null:
		unit_state.set_unlocked_combat_resource_ids(BattleUnitState.DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS)
		return
	if progression_state.has_method("sync_default_combat_resource_unlocks"):
		progression_state.sync_default_combat_resource_unlocks()
	var resource_ids: Array[StringName] = []
	for resource_id in progression_state.unlocked_combat_resource_ids:
		resource_ids.append(resource_id)
	unit_state.set_unlocked_combat_resource_ids(resource_ids)


func _sync_enemy_unlocked_resources(unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	unit_state.sync_default_combat_resource_unlocks()
	var snapshot: AttributeSnapshot = unit_state.attribute_snapshot
	var mp_max := 0
	var aura_max := 0
	if snapshot != null:
		mp_max = int(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX))
		aura_max = int(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX))
	if unit_state.current_mp > 0 or mp_max > 0:
		unit_state.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_MP)
	if unit_state.current_aura > 0 or aura_max > 0:
		unit_state.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_AURA)
	for skill_id in unit_state.known_active_skill_ids:
		var skill_def := _skill_def_from_runtime(skill_id)
		if skill_def == null or skill_def.combat_profile == null:
			continue
		var skill_level := maxi(int(unit_state.known_skill_level_map.get(skill_id, 1)), 1)
		var costs := skill_def.combat_profile.get_effective_resource_costs(skill_level)
		if int(costs.get("mp_cost", 0)) > 0:
			unit_state.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_MP)
		if int(costs.get("aura_cost", 0)) > 0:
			unit_state.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_AURA)


func _runtime_has_skill(skill_id: StringName) -> bool:
	if skill_id == &"" or _runtime == null:
		return false
	var skill_defs = _runtime.get_skill_defs()
	return skill_defs is Dictionary and skill_defs.has(skill_id)


func _has_explicit_default_enemy_weapon_context(context: Dictionary) -> bool:
	return context.has("default_enemy_weapon_attack_range") \
		or context.has("default_enemy_weapon_profile_type_id") \
		or context.has("default_enemy_weapon_physical_damage_tag")


func _resolve_action_threshold_from_snapshot(
	snapshot: AttributeSnapshot,
	fallback_threshold: int = BattleUnitState.DEFAULT_ACTION_THRESHOLD
) -> int:
	if snapshot != null and snapshot.has_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD):
		var snapshot_threshold := int(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD))
		if snapshot_threshold > 0:
			return snapshot_threshold
	return fallback_threshold


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


func _collect_known_skill_lock_hit_bonus_map(progression_state) -> Dictionary:
	var skill_bonuses: Dictionary = {}
	if progression_state == null:
		return skill_bonuses

	for skill_key in ProgressionDataUtils.sorted_string_keys(progression_state.skills):
		var skill_id: StringName = StringName(skill_key)
		var skill_progress: Variant = progression_state.get_skill_progress(skill_id)
		var skill_def: SkillDef = _skill_def_from_runtime(skill_id)
		if skill_progress == null or skill_def == null:
			continue
		if not skill_progress.is_learned:
			continue
		if not bool(skill_progress.is_level_trigger_locked):
			continue
		var bonus := int(skill_progress.bonus_to_hit_from_lock)
		if bonus <= 0:
			continue
		skill_bonuses[skill_id] = bonus

	return skill_bonuses


func _sync_passive_battle_statuses(unit_state: BattleUnitState, progression_state, member_state = null) -> void:
	if unit_state == null:
		return
	var context: PassiveSourceContext = null
	var character_gateway: Object = _runtime.get_character_gateway() if _runtime != null else null
	if character_gateway != null \
			and unit_state.source_member_id != &"" \
			and character_gateway.has_method("build_passive_source_context"):
		context = character_gateway.call("build_passive_source_context", unit_state.source_member_id, progression_state) as PassiveSourceContext
	if context == null:
		context = PASSIVE_SOURCE_CONTEXT_SCRIPT.new()
		context.member_state = member_state
		context.unit_progress = progression_state
		if progression_state != null:
			context.skill_progress_by_id = progression_state.skills
	PASSIVE_STATUS_ORCHESTRATOR_SCRIPT.apply_to_unit(
		unit_state,
		context,
		_runtime.get_skill_defs() if _runtime != null else {}
	)


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
