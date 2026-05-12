class_name PassiveStatusOrchestrator
extends RefCounted

const PASSIVE_SOURCE_CONTEXT_SCRIPT = preload("res://scripts/systems/progression/passive_source_context.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const RACE_TRAIT_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/race_trait_resolver.gd")
const ASCENSION_TRAIT_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/ascension_trait_resolver.gd")
const SKILL_PASSIVE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/skill_passive_resolver.gd")
const BattleUnitState = BATTLE_UNIT_STATE_SCRIPT
const PassiveSourceContext = PASSIVE_SOURCE_CONTEXT_SCRIPT


static func apply_to_unit(unit_state: BattleUnitState, context: PassiveSourceContext = null, skill_defs: Dictionary = {}) -> void:
	if unit_state == null:
		return
	var resolved_context: PassiveSourceContext = context if context != null else PASSIVE_SOURCE_CONTEXT_SCRIPT.new()
	_clear_identity_projection(unit_state)
	if not _suppresses_original_race_traits(resolved_context):
		RACE_TRAIT_RESOLVER_SCRIPT.apply_to_unit(unit_state, resolved_context)
	ASCENSION_TRAIT_RESOLVER_SCRIPT.apply_to_unit(unit_state, resolved_context)
	SKILL_PASSIVE_RESOLVER_SCRIPT.apply_to_unit(unit_state, resolved_context, skill_defs)


static func _clear_identity_projection(unit_state: BattleUnitState) -> void:
	unit_state.vision_tags = []
	unit_state.proficiency_tags = []
	unit_state.save_advantage_tags = []
	unit_state.damage_resistances = {}
	unit_state.race_trait_ids = []
	unit_state.subrace_trait_ids = []
	unit_state.ascension_trait_ids = []
	unit_state.bloodline_trait_ids = []
	_clear_identity_skill_charges(unit_state)


static func _clear_identity_skill_charges(unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	_clear_charge_keys_with_prefix(unit_state.per_battle_charges, "racial_skill_")
	_clear_charge_keys_with_prefix(unit_state.per_turn_charges, "racial_skill_")
	_clear_charge_keys_with_prefix(unit_state.per_turn_charge_limits, "racial_skill_")


static func _clear_charge_keys_with_prefix(charges: Dictionary, prefix: String) -> void:
	for charge_key_variant in charges.keys():
		var charge_key := String(charge_key_variant)
		if charge_key.begins_with(prefix):
			charges.erase(charge_key_variant)


static func _suppresses_original_race_traits(context: PassiveSourceContext) -> bool:
	if context == null or context.ascension_def == null:
		return false
	return bool(context.ascension_def.suppresses_original_race_traits)
