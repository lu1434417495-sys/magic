class_name BattleSimUnitSpec
extends Resource

const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle_unit_state.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")

@export var unit_id: StringName = &""
@export var source_member_id: StringName = &""
@export var display_name: String = ""
@export var faction_id: StringName = &""
@export var control_mode: StringName = &"manual"
@export var ai_brain_id: StringName = &""
@export var ai_state_id: StringName = &""
@export var coord: Vector2i = Vector2i.ZERO
@export var body_size := 1
@export var current_hp := 30
@export var current_mp := 0
@export var current_stamina := 0
@export var current_aura := 0
@export var current_ap := 1
@export var attribute_overrides: Dictionary = {}
@export var skill_ids: Array = []
@export var skill_level_map: Dictionary = {}
@export var movement_tags: Array = []
@export var status_effects: Array[Dictionary] = []


func to_battle_unit_state(default_faction_id: StringName = &"", default_control_mode: StringName = &"manual") -> BattleUnitState:
	var unit_state: BattleUnitState = BATTLE_UNIT_STATE_SCRIPT.new()
	unit_state.unit_id = unit_id
	unit_state.source_member_id = source_member_id
	unit_state.display_name = display_name if not display_name.is_empty() else String(unit_id)
	unit_state.faction_id = faction_id if faction_id != &"" else default_faction_id
	unit_state.control_mode = control_mode if control_mode != &"" else default_control_mode
	unit_state.ai_brain_id = ai_brain_id
	unit_state.ai_state_id = ai_state_id
	unit_state.body_size = maxi(body_size, 1)
	unit_state.set_anchor_coord(coord)
	_apply_attribute_defaults(unit_state)
	for attribute_key in attribute_overrides.keys():
		unit_state.attribute_snapshot.set_value(
			ProgressionDataUtils.to_string_name(attribute_key),
			int(attribute_overrides.get(attribute_key, 0))
		)
	unit_state.current_hp = clampi(current_hp, 0, maxi(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX), 1))
	unit_state.current_mp = clampi(current_mp, 0, maxi(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX), 0))
	unit_state.current_stamina = clampi(current_stamina, 0, maxi(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX), 0))
	unit_state.current_aura = clampi(current_aura, 0, maxi(unit_state.attribute_snapshot.get_value(&"aura_max"), 0))
	unit_state.current_ap = maxi(current_ap, 0)
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
	unit_state.is_alive = unit_state.current_hp > 0
	return unit_state


func to_dict() -> Dictionary:
	return to_battle_unit_state(faction_id, control_mode).to_dict()


func _apply_attribute_defaults(unit_state: BattleUnitState) -> void:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, maxi(current_hp, 1))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, maxi(current_mp, 0))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, maxi(current_stamina, 0))
	unit_state.attribute_snapshot.set_value(&"aura_max", maxi(current_aura, 0))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS, maxi(current_ap, 1))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.PHYSICAL_ATTACK, int(attribute_overrides.get("physical_attack", 10)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.PHYSICAL_DEFENSE, int(attribute_overrides.get("physical_defense", 4)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MAGIC_ATTACK, int(attribute_overrides.get("magic_attack", 8)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MAGIC_DEFENSE, int(attribute_overrides.get("magic_defense", 4)))
	unit_state.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SPEED, int(attribute_overrides.get("speed", 10)))
