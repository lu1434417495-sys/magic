class_name AscensionTraitResolver
extends RefCounted

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const RacialGrantedSkill = preload("res://scripts/player/progression/racial_granted_skill.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const PassiveSourceContext = preload("res://scripts/systems/progression/passive_source_context.gd")


static func apply_to_unit(unit_state: BattleUnitState, context: PassiveSourceContext) -> void:
	if unit_state == null or context == null:
		return
	_apply_identity_def_projection(unit_state, context.bloodline_def, unit_state.bloodline_trait_ids)
	_apply_identity_def_projection(unit_state, context.bloodline_stage_def, unit_state.bloodline_trait_ids)
	_apply_identity_def_projection(unit_state, context.ascension_def, unit_state.ascension_trait_ids)
	_apply_identity_def_projection(unit_state, context.ascension_stage_def, unit_state.ascension_trait_ids)


static func _apply_identity_def_projection(unit_state: BattleUnitState, identity_def, trait_target: Array[StringName]) -> void:
	if identity_def == null:
		return
	_append_unique_string_names(trait_target, _get_array_property(identity_def, "trait_ids"))
	_append_unique_string_names(unit_state.vision_tags, _get_array_property(identity_def, "vision_tags"))
	_append_unique_string_names(unit_state.proficiency_tags, _get_array_property(identity_def, "proficiency_tags"))
	_append_unique_string_names(unit_state.save_advantage_tags, _get_array_property(identity_def, "save_advantage_tags"))
	_merge_damage_resistances(unit_state.damage_resistances, _get_dictionary_property(identity_def, "damage_resistances"))
	_initialize_racial_skill_charges(unit_state, _get_array_property(identity_def, "racial_granted_skills"))


static func _initialize_racial_skill_charges(unit_state: BattleUnitState, grants: Array) -> void:
	for grant_variant in grants:
		var grant := grant_variant as RacialGrantedSkill
		if grant == null or grant.skill_id == &"":
			continue
		var charge_key := StringName("racial_skill_%s" % String(grant.skill_id))
		match grant.charge_kind:
			RacialGrantedSkill.CHARGE_KIND_PER_BATTLE:
				if not unit_state.per_battle_charges.has(charge_key):
					unit_state.per_battle_charges[charge_key] = maxi(int(grant.charges), 1)
			RacialGrantedSkill.CHARGE_KIND_PER_TURN:
				var charge_count := maxi(int(grant.charges), 1)
				unit_state.per_turn_charge_limits[charge_key] = charge_count
				if not unit_state.per_turn_charges.has(charge_key):
					unit_state.per_turn_charges[charge_key] = charge_count
				else:
					unit_state.per_turn_charges[charge_key] = clampi(int(unit_state.per_turn_charges.get(charge_key, 0)), 0, charge_count)
			_:
				pass


static func _append_unique_string_names(target: Array[StringName], values: Array) -> void:
	for raw_value in values:
		var value := ProgressionDataUtils.to_string_name(raw_value)
		if value == &"" or target.has(value):
			continue
		target.append(value)


static func _merge_damage_resistances(target: Dictionary, values: Dictionary) -> void:
	for raw_key in values.keys():
		var damage_tag := ProgressionDataUtils.to_string_name(raw_key)
		var mitigation_tier := ProgressionDataUtils.to_string_name(values.get(raw_key, ""))
		if damage_tag == &"" or mitigation_tier == &"":
			continue
		target[damage_tag] = mitigation_tier


static func _get_array_property(source, property_name: String) -> Array:
	if source == null or not (source is Object):
		return []
	var raw_value: Variant = source.get(property_name)
	return raw_value if raw_value is Array else []


static func _get_dictionary_property(source, property_name: String) -> Dictionary:
	if source == null or not (source is Object):
		return {}
	var raw_value: Variant = source.get(property_name)
	return raw_value if raw_value is Dictionary else {}
