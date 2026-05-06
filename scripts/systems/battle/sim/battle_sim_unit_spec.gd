class_name BattleSimUnitSpec
extends Resource

const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const CHARACTER_CREATION_SERVICE_SCRIPT = preload("res://scripts/systems/progression/character_creation_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const UNIT_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_progress.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

@export var unit_id: StringName = &""
@export var source_member_id: StringName = &""
@export var display_name: String = ""
@export var faction_id: StringName = &""
@export var control_mode: StringName = &"manual"
@export var ai_brain_id: StringName = &""
@export var ai_state_id: StringName = &""
@export var coord: Vector2i = Vector2i.ZERO
@export var body_size := 2
@export var body_size_category: StringName = &"medium"
@export var current_hp := 30
@export var current_mp := 0
@export var current_stamina := 0
@export var current_aura := 0
@export var current_ap := 1
@export var current_move_points := BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN
@export var action_threshold := ATTRIBUTE_SERVICE_SCRIPT.DEFAULT_CHARACTER_ACTION_THRESHOLD
@export var attribute_overrides: Dictionary = {}
@export var skill_ids: Array = []
@export var skill_level_map: Dictionary = {}
@export var movement_tags: Array = []
@export var status_effects: Array[Dictionary] = []
@export var weapon_projection: Dictionary = {}
@export var base_attributes: Dictionary = {}


func to_battle_unit_state(default_faction_id: StringName = &"", default_control_mode: StringName = &"manual") -> BattleUnitState:
	var unit_state: BattleUnitState = BATTLE_UNIT_STATE_SCRIPT.new()
	unit_state.unit_id = unit_id
	unit_state.source_member_id = source_member_id
	unit_state.display_name = display_name if not display_name.is_empty() else String(unit_id)
	unit_state.faction_id = faction_id if faction_id != &"" else default_faction_id
	unit_state.control_mode = control_mode if control_mode != &"" else default_control_mode
	unit_state.ai_brain_id = ai_brain_id
	unit_state.ai_state_id = ai_state_id
	if not unit_state.set_body_size_category(body_size_category):
		unit_state.body_size = maxi(body_size, 1)
		unit_state.sync_body_size_category_from_body_size()
	unit_state.set_anchor_coord(coord)
	_apply_attribute_defaults(unit_state)
	_apply_attribute_overrides(unit_state)
	unit_state.current_hp = clampi(current_hp, 0, maxi(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX), 1))
	unit_state.current_mp = clampi(current_mp, 0, maxi(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX), 0))
	unit_state.current_stamina = clampi(current_stamina, 0, maxi(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX), 0))
	unit_state.current_aura = clampi(current_aura, 0, maxi(unit_state.attribute_snapshot.get_value(&"aura_max"), 0))
	unit_state.current_ap = clampi(current_ap, 0, maxi(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS), 1))
	unit_state.current_move_points = clampi(current_move_points, 0, BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN)
	unit_state.action_threshold = _resolve_action_threshold(unit_state)
	unit_state.known_active_skill_ids.clear()
	for raw_skill_id in skill_ids:
		var skill_id := ProgressionDataUtils.to_string_name(raw_skill_id)
		if skill_id == &"":
			continue
		unit_state.known_active_skill_ids.append(skill_id)
		unit_state.known_skill_level_map[skill_id] = int(skill_level_map.get(String(skill_id), skill_level_map.get(skill_id, 1)))
	for raw_tag in movement_tags:
		var tag := ProgressionDataUtils.to_string_name(raw_tag)
		if tag == &"" or unit_state.movement_tags.has(tag):
			continue
		unit_state.movement_tags.append(tag)
	for status_entry in status_effects:
		if status_entry is not Dictionary:
			continue
		var status_id := ProgressionDataUtils.to_string_name(status_entry.get("status_id", ""))
		if status_id == &"":
			continue
		unit_state.status_effects[status_id] = status_entry.duplicate(true)
	if not weapon_projection.is_empty():
		unit_state.apply_weapon_projection(weapon_projection)
	unit_state.is_alive = unit_state.current_hp > 0
	return unit_state


func to_dict() -> Dictionary:
	return to_battle_unit_state(faction_id, control_mode).to_dict()


func _apply_attribute_defaults(unit_state: BattleUnitState) -> void:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return
	var has_base := not base_attributes.is_empty()
	if has_base:
		unit_state.attribute_snapshot = _build_formal_attribute_snapshot()
		return

	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, maxi(current_hp, 1))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, maxi(current_mp, 0))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, maxi(current_stamina, 0))
	unit_state.attribute_snapshot.set_value(&"aura_max", maxi(current_aura, 0))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS, maxi(current_ap, 1))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD, maxi(action_threshold, 1))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, int(attribute_overrides.get("attack_bonus", 4)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, int(attribute_overrides.get("armor_class", 10)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_AC_BONUS, int(attribute_overrides.get("armor_ac_bonus", 0)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SHIELD_AC_BONUS, int(attribute_overrides.get("shield_ac_bonus", 0)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DODGE_BONUS, int(attribute_overrides.get("dodge_bonus", 0)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DEFLECTION_BONUS, int(attribute_overrides.get("deflection_bonus", 0)))


func _build_formal_attribute_snapshot():
	var unit_progress = UNIT_PROGRESS_SCRIPT.new()
	var constitution := _get_base_attribute_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 10)
	for attribute_id in UNIT_BASE_ATTRIBUTES_SCRIPT.BASE_ATTRIBUTE_IDS:
		var normalized_id := ProgressionDataUtils.to_string_name(attribute_id)
		unit_progress.unit_base_attributes.set_attribute_value(normalized_id, _get_base_attribute_value(normalized_id, 10))

	if _has_attribute_override(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX):
		unit_progress.unit_base_attributes.set_attribute_value(
			ATTRIBUTE_SERVICE_SCRIPT.HP_MAX,
			int(_get_attribute_override(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, current_hp))
		)
	else:
		unit_progress.unit_base_attributes.set_attribute_value(
			ATTRIBUTE_SERVICE_SCRIPT.HP_MAX,
			CHARACTER_CREATION_SERVICE_SCRIPT.calculate_initial_hp_max(constitution)
		)
	if _has_attribute_override(ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD):
		unit_progress.unit_base_attributes.set_attribute_value(
			ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD,
			int(_get_attribute_override(ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD, action_threshold))
		)

	var attribute_service = ATTRIBUTE_SERVICE_SCRIPT.new()
	attribute_service.setup(unit_progress)
	return attribute_service.get_snapshot()


func _apply_attribute_overrides(unit_state: BattleUnitState) -> void:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return
	for attribute_key in attribute_overrides.keys():
		var attribute_id := ProgressionDataUtils.to_string_name(attribute_key)
		if _is_formal_attribute_override(attribute_id):
			continue
		unit_state.attribute_snapshot.set_value(attribute_id, int(attribute_overrides.get(attribute_key, 0)))


func _is_formal_attribute_override(attribute_id: StringName) -> bool:
	if base_attributes.is_empty():
		return false
	return attribute_id == ATTRIBUTE_SERVICE_SCRIPT.HP_MAX \
		or attribute_id == ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD


func _get_base_attribute_value(attribute_id: StringName, default_value: int) -> int:
	if base_attributes.has(attribute_id):
		return int(base_attributes.get(attribute_id, default_value))
	return int(base_attributes.get(String(attribute_id), default_value))


func _has_attribute_override(attribute_id: StringName) -> bool:
	return attribute_overrides.has(attribute_id) or attribute_overrides.has(String(attribute_id))


func _get_attribute_override(attribute_id: StringName, default_value: int) -> int:
	if attribute_overrides.has(attribute_id):
		return int(attribute_overrides.get(attribute_id, default_value))
	return int(attribute_overrides.get(String(attribute_id), default_value))


func _resolve_action_threshold(unit_state: BattleUnitState) -> int:
	if unit_state != null and unit_state.attribute_snapshot != null:
		var snapshot_threshold := int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD))
		if snapshot_threshold > 0:
			return snapshot_threshold
	return maxi(action_threshold, 1)
