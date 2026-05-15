class_name BattleAiActionAssembler
extends RefCounted

const BATTLE_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_range_service.gd")
const BATTLE_TARGET_TEAM_RULES_SCRIPT = preload("res://scripts/systems/battle/rules/battle_target_team_rules.gd")
const BATTLE_AI_RUNTIME_ACTION_PLAN_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_runtime_action_plan.gd")
const BATTLE_AI_SKILL_AFFORDANCE_CLASSIFIER_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_skill_affordance_classifier.gd")
const MOVE_TO_RANGE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/move_to_range_action.gd")
const USE_CHARGE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_charge_action.gd")
const USE_CHARGE_PATH_AOE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_charge_path_aoe_action.gd")
const USE_GROUND_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_ground_skill_action.gd")
const USE_MULTI_UNIT_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_multi_unit_skill_action.gd")
const USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_random_chain_skill_action.gd")
const USE_UNIT_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_unit_skill_action.gd")
const MOVE_TO_MULTI_UNIT_SKILL_POSITION_ACTION_SCRIPT = preload("res://scripts/enemies/actions/move_to_multi_unit_skill_position_action.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

const PATH_STEP_AOE_EFFECT_TYPE: StringName = &"path_step_aoe"

var _classifier = BATTLE_AI_SKILL_AFFORDANCE_CLASSIFIER_SCRIPT.new()


func build_unit_action_plan(unit_state: BattleUnitState, brain, skill_defs: Dictionary):
	var plan = BATTLE_AI_RUNTIME_ACTION_PLAN_SCRIPT.new()
	if unit_state == null or brain == null:
		return plan
	plan.set_source(unit_state, brain, skill_defs)
	var skill_records := _classify_known_active_skills(unit_state, skill_defs)
	for record in skill_records:
		plan.set_skill_affordance_record(ProgressionDataUtils.to_string_name(record.get("skill_id", &"")), record)
	for state_def in brain.get_states():
		if state_def == null:
			continue
		var actions: Array = state_def.get_actions()
		plan.add_state_actions(state_def.state_id, actions)
		var generation_slots := _get_generation_slots(state_def)
		if generation_slots.is_empty():
			continue
		for slot in generation_slots:
			if slot == null:
				continue
			for record in skill_records:
				if not bool(record.get("is_generatable", false)):
					continue
				var skill_id := ProgressionDataUtils.to_string_name(record.get("skill_id", &""))
				var skill_def = skill_defs.get(skill_id) as SkillDef
				if skill_def == null:
					continue
				for family in record.get("action_families", []):
					var action_family := ProgressionDataUtils.to_string_name(family)
					if action_family == &"" or not slot.matches_affordance(record, action_family):
						continue
					if _is_generation_suppressed(plan, state_def, actions, slot, skill_id, action_family):
						continue
					var generated_action = _build_skill_action_for_family(unit_state, state_def, actions, skill_def, action_family)
					if generated_action == null:
						continue
					_apply_slot_overrides(generated_action, slot, actions)
					generated_action.action_id = _build_runtime_action_id(state_def.state_id, slot.slot_id, skill_id, action_family)
					var metadata := _build_generated_action_metadata(state_def.state_id, slot, skill_id, action_family, generated_action)
					plan.add_action(state_def.state_id, generated_action, metadata)
	return plan


func _classify_known_active_skills(unit_state: BattleUnitState, skill_defs: Dictionary) -> Array:
	var records: Array = []
	for raw_skill_id in unit_state.known_active_skill_ids:
		var skill_id := ProgressionDataUtils.to_string_name(raw_skill_id)
		if skill_id == &"":
			continue
		var skill_def = skill_defs.get(skill_id) as SkillDef
		if skill_def == null:
			continue
		records.append(_classifier.classify_skill(skill_def, _get_skill_level(unit_state, skill_id)))
	return records


func _get_generation_slots(state_def) -> Array:
	if state_def == null:
		return []
	if state_def.has_method("get_generation_slots"):
		var slots = state_def.get_generation_slots()
		_sort_generation_slots(slots)
		return slots
	var slots_variant = state_def.get("generation_slots")
	if slots_variant is not Array:
		return []
	var slots: Array = []
	for slot in slots_variant:
		if slot != null:
			slots.append(slot)
	_sort_generation_slots(slots)
	return slots


func _sort_generation_slots(slots: Array) -> void:
	slots.sort_custom(func(left, right) -> bool:
		var left_order := int(left.get("order")) if left != null else 0
		var right_order := int(right.get("order")) if right != null else 0
		if left_order != right_order:
			return left_order < right_order
		return String(left.get("slot_id")) < String(right.get("slot_id"))
	)


func _build_skill_action_for_family(
	unit_state: BattleUnitState,
	state_def,
	state_actions: Array,
	skill_def: SkillDef,
	action_family: StringName
):
	if unit_state == null or state_def == null or skill_def == null or skill_def.combat_profile == null:
		return null
	match action_family:
		&"use_charge_path_aoe":
			return _build_charge_path_aoe_action(unit_state, state_def, state_actions, skill_def)
		&"use_charge":
			return _build_charge_action(unit_state, state_def, state_actions, skill_def)
		&"use_random_chain_skill":
			return _build_random_chain_action(unit_state, state_def, state_actions, skill_def)
		&"move_to_range":
			return _build_move_to_range_action(unit_state, state_def, state_actions, skill_def)
		&"use_multi_unit_skill":
			return _build_multi_unit_action(unit_state, state_def, state_actions, skill_def)
		&"move_to_multi_unit_skill_position":
			return _build_move_to_multi_unit_action(unit_state, state_def, state_actions, skill_def)
		&"use_ground_skill":
			return _build_ground_action(unit_state, state_def, state_actions, skill_def)
		&"use_unit_skill":
			return _build_unit_action(unit_state, state_def, state_actions, skill_def)
	return null


func _is_generation_suppressed(
	plan,
	state_def,
	state_actions: Array,
	slot,
	skill_id: StringName,
	action_family: StringName
) -> bool:
	if slot != null and ProgressionDataUtils.to_string_name(slot.suppression_policy) == &"manual_only":
		return true
	var identity_key: String = _build_identity_key(state_def.state_id, slot.slot_id, skill_id, action_family)
	for existing_action in plan.get_actions(state_def.state_id):
		var metadata: Dictionary = plan.get_action_metadata(existing_action)
		if String(metadata.get("identity_key", "")) == identity_key:
			return true
	for authored_action in state_actions:
		if authored_action == null or not authored_action.has_method("get_declared_skill_ids"):
			continue
		if not authored_action.get_declared_skill_ids().has(skill_id):
			continue
		if _get_action_family_for_action(authored_action) == action_family:
			return true
	return false


func _get_action_family_for_action(action) -> StringName:
	if action == null:
		return &""
	var script_resource = action.get_script()
	if script_resource == USE_UNIT_SKILL_ACTION_SCRIPT:
		return &"use_unit_skill"
	if script_resource == USE_GROUND_SKILL_ACTION_SCRIPT:
		return &"use_ground_skill"
	if script_resource == USE_MULTI_UNIT_SKILL_ACTION_SCRIPT:
		return &"use_multi_unit_skill"
	if script_resource == USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT:
		return &"use_random_chain_skill"
	if script_resource == USE_CHARGE_ACTION_SCRIPT:
		return &"use_charge"
	if script_resource == USE_CHARGE_PATH_AOE_ACTION_SCRIPT:
		return &"use_charge_path_aoe"
	if script_resource == MOVE_TO_RANGE_ACTION_SCRIPT:
		return &"move_to_range"
	if script_resource == MOVE_TO_MULTI_UNIT_SKILL_POSITION_ACTION_SCRIPT:
		return &"move_to_multi_unit_skill_position"
	return &""


func _apply_slot_overrides(action, slot, state_actions: Array) -> void:
	if action == null or slot == null:
		return
	var template_action = _find_action_by_id(state_actions, ProgressionDataUtils.to_string_name(slot.style_template_action_id))
	var slot_bucket := ProgressionDataUtils.to_string_name(slot.score_bucket_id)
	if slot_bucket != &"":
		action.score_bucket_id = slot_bucket
	elif action.score_bucket_id == &"" and template_action != null:
		action.score_bucket_id = ProgressionDataUtils.to_string_name(template_action.get("score_bucket_id"))
	var slot_selector := ProgressionDataUtils.to_string_name(slot.target_selector)
	if slot_selector != &"" and _has_property(action, "target_selector"):
		action.target_selector = slot_selector
	elif template_action != null and _has_property(action, "target_selector"):
		var template_selector := ProgressionDataUtils.to_string_name(template_action.get("target_selector"))
		if template_selector != &"":
			action.target_selector = template_selector
	if int(slot.desired_min_distance) >= 0 and _has_property(action, "desired_min_distance"):
		action.desired_min_distance = int(slot.desired_min_distance)
	if int(slot.desired_max_distance) >= 0 and _has_property(action, "desired_max_distance"):
		action.desired_max_distance = int(slot.desired_max_distance)
	var distance_reference := ProgressionDataUtils.to_string_name(slot.distance_reference)
	if distance_reference != &"" and _has_property(action, "distance_reference"):
		action.distance_reference = distance_reference


func _build_generated_action_metadata(
	state_id: StringName,
	slot,
	skill_id: StringName,
	action_family: StringName,
	action
) -> Dictionary:
	var metadata := {
		"generated": true,
		"state_id": state_id,
		"slot_id": ProgressionDataUtils.to_string_name(slot.slot_id),
		"slot_role": ProgressionDataUtils.to_string_name(slot.slot_role),
		"skill_id": skill_id,
		"variant_id": &"",
		"action_family": action_family,
		"source_action_id": ProgressionDataUtils.to_string_name(slot.style_template_action_id),
		"score_bucket_id": ProgressionDataUtils.to_string_name(action.get("score_bucket_id")),
		"action_id": ProgressionDataUtils.to_string_name(action.get("action_id")),
		"identity_key": _build_identity_key(state_id, slot.slot_id, skill_id, action_family),
	}
	metadata["runtime_action_metadata"] = {
		"generated": true,
		"state_id": state_id,
		"slot_id": metadata["slot_id"],
		"slot_role": metadata["slot_role"],
		"skill_id": skill_id,
		"variant_id": &"",
		"action_family": action_family,
		"source_action_id": metadata["source_action_id"],
		"identity_key": metadata["identity_key"],
	}
	return metadata


func _find_action_by_id(state_actions: Array, action_id: StringName):
	for action in state_actions:
		if action != null and ProgressionDataUtils.to_string_name(action.get("action_id")) == action_id:
			return action
	return null


func _has_property(object, property_name: String) -> bool:
	if object == null:
		return false
	for property_info in object.get_property_list():
		if String(property_info.get("name", "")) == property_name:
			return true
	return false


func _build_runtime_action_id(state_id: StringName, slot_id: StringName, skill_id: StringName, action_family: StringName) -> StringName:
	return StringName("auto_%s_%s_%s_%s" % [String(state_id), String(slot_id), String(skill_id), String(action_family)])


func _build_identity_key(state_id: StringName, slot_id: StringName, skill_id: StringName, action_family: StringName) -> String:
	return "%s/%s/%s/%s" % [String(state_id), String(slot_id), String(skill_id), String(action_family)]


func _collect_declared_skill_ids(actions: Array) -> Dictionary:
	var declared_skill_ids: Dictionary = {}
	for action in actions:
		if action == null or not action.has_method("get_declared_skill_ids"):
			continue
		for skill_id in action.get_declared_skill_ids():
			var normalized_skill_id := ProgressionDataUtils.to_string_name(skill_id)
			if normalized_skill_id != &"":
				declared_skill_ids[normalized_skill_id] = true
	return declared_skill_ids


func _build_skill_actions(
	unit_state: BattleUnitState,
	state_def,
	state_actions: Array,
	skill_def: SkillDef
) -> Array:
	var actions: Array = []
	if unit_state == null or state_def == null or skill_def == null or skill_def.combat_profile == null:
		return actions
	if skill_def.skill_type != &"active":
		return actions
	if not _is_offensive_or_enemy_skill(skill_def):
		return actions
	var skill_id := skill_def.skill_id
	var skill_level := _get_skill_level(unit_state, skill_id)
	var charge_path_variant = _find_charge_path_step_aoe_variant(skill_def, skill_level)
	if charge_path_variant != null:
		actions.append(_build_charge_path_aoe_action(unit_state, state_def, state_actions, skill_def))
		return actions
	var charge_variant = _find_charge_variant(skill_def, skill_level)
	if charge_variant != null:
		actions.append(_build_charge_action(unit_state, state_def, state_actions, skill_def))
		return actions
	if _is_random_chain_skill(skill_def):
		actions.append(_build_random_chain_action(unit_state, state_def, state_actions, skill_def))
		actions.append(_build_move_to_range_action(unit_state, state_def, state_actions, skill_def))
		return actions
	if _is_multi_unit_skill(skill_def):
		actions.append(_build_multi_unit_action(unit_state, state_def, state_actions, skill_def))
		actions.append(_build_move_to_multi_unit_action(unit_state, state_def, state_actions, skill_def))
		return actions
	if skill_def.combat_profile.target_mode == &"ground":
		actions.append(_build_ground_action(unit_state, state_def, state_actions, skill_def))
		return actions
	if skill_def.combat_profile.target_mode == &"unit":
		actions.append(_build_unit_action(unit_state, state_def, state_actions, skill_def))
	return actions


func _build_charge_path_aoe_action(
	unit_state: BattleUnitState,
	state_def,
	state_actions: Array,
	skill_def: SkillDef
):
	var action = USE_CHARGE_PATH_AOE_ACTION_SCRIPT.new()
	action.action_id = _build_action_id(state_def.state_id, skill_def.skill_id, &"path_aoe")
	action.score_bucket_id = _resolve_generated_score_bucket_id(state_actions, USE_CHARGE_ACTION_SCRIPT)
	var skill_ids: Array[StringName] = [skill_def.skill_id]
	action.skill_ids = skill_ids
	action.target_selector = _resolve_target_selector(state_actions, &"nearest_enemy")
	action.minimum_hit_count = 1
	action.desired_min_distance = 1
	action.desired_max_distance = 1
	return action


func _build_charge_action(
	unit_state: BattleUnitState,
	state_def,
	state_actions: Array,
	skill_def: SkillDef
):
	var action = USE_CHARGE_ACTION_SCRIPT.new()
	action.action_id = _build_action_id(state_def.state_id, skill_def.skill_id, &"charge")
	action.score_bucket_id = _resolve_generated_score_bucket_id(state_actions, USE_CHARGE_ACTION_SCRIPT)
	action.skill_id = skill_def.skill_id
	action.target_selector = _resolve_target_selector(state_actions, &"nearest_enemy")
	action.minimum_charge_move_distance = 3
	return action


func _build_ground_action(
	unit_state: BattleUnitState,
	state_def,
	state_actions: Array,
	skill_def: SkillDef
):
	var action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	action.action_id = _build_action_id(state_def.state_id, skill_def.skill_id, &"ground")
	action.score_bucket_id = _resolve_generated_score_bucket_id(state_actions, USE_GROUND_SKILL_ACTION_SCRIPT)
	var skill_ids: Array[StringName] = [skill_def.skill_id]
	action.skill_ids = skill_ids
	action.minimum_hit_count = maxi(int(skill_def.combat_profile.min_target_count), 1)
	_apply_ground_distance_style(action, unit_state, state_actions, skill_def)
	return action


func _build_unit_action(
	unit_state: BattleUnitState,
	state_def,
	state_actions: Array,
	skill_def: SkillDef
):
	var action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	action.action_id = _build_action_id(state_def.state_id, skill_def.skill_id, &"unit")
	action.score_bucket_id = _resolve_generated_score_bucket_id(state_actions, USE_UNIT_SKILL_ACTION_SCRIPT)
	var skill_ids: Array[StringName] = [skill_def.skill_id]
	action.skill_ids = skill_ids
	action.target_selector = _resolve_target_selector(state_actions, &"nearest_enemy")
	_apply_unit_distance_style(action, unit_state, state_actions, skill_def)
	return action


func _build_multi_unit_action(
	unit_state: BattleUnitState,
	state_def,
	state_actions: Array,
	skill_def: SkillDef
):
	var action = USE_MULTI_UNIT_SKILL_ACTION_SCRIPT.new()
	action.action_id = _build_action_id(state_def.state_id, skill_def.skill_id, &"multi")
	action.score_bucket_id = _resolve_generated_score_bucket_id(state_actions, USE_MULTI_UNIT_SKILL_ACTION_SCRIPT)
	var skill_ids: Array[StringName] = [skill_def.skill_id]
	action.skill_ids = skill_ids
	action.target_selector = _resolve_target_selector(state_actions, &"nearest_enemy")
	_apply_unit_distance_style(action, unit_state, state_actions, skill_def)
	return action


func _build_random_chain_action(
	unit_state: BattleUnitState,
	state_def,
	state_actions: Array,
	skill_def: SkillDef
):
	var action = USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT.new()
	action.action_id = _build_action_id(state_def.state_id, skill_def.skill_id, &"random_chain")
	action.score_bucket_id = _resolve_generated_score_bucket_id(state_actions, USE_UNIT_SKILL_ACTION_SCRIPT)
	var skill_ids: Array[StringName] = [skill_def.skill_id]
	action.skill_ids = skill_ids
	action.target_selector = _resolve_target_selector(state_actions, &"nearest_enemy")
	_apply_random_chain_distance_style(action, unit_state, state_actions, skill_def)
	return action


func _build_move_to_range_action(
	unit_state: BattleUnitState,
	state_def,
	state_actions: Array,
	skill_def: SkillDef
):
	var action = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	action.action_id = _build_action_id(state_def.state_id, skill_def.skill_id, &"range_move")
	action.score_bucket_id = _resolve_generated_score_bucket_id(state_actions, MOVE_TO_RANGE_ACTION_SCRIPT)
	action.target_selector = _resolve_target_selector(state_actions, &"nearest_enemy")
	var range_skill_ids: Array[StringName] = [skill_def.skill_id]
	action.range_skill_ids = range_skill_ids
	var effective_range := BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_threat_range(unit_state, skill_def)
	action.desired_min_distance = mini(1, effective_range) if effective_range > 0 else 0
	action.desired_max_distance = maxi(effective_range, action.desired_min_distance)
	return action


func _build_move_to_multi_unit_action(
	unit_state: BattleUnitState,
	state_def,
	state_actions: Array,
	skill_def: SkillDef
):
	var action = MOVE_TO_MULTI_UNIT_SKILL_POSITION_ACTION_SCRIPT.new()
	action.action_id = _build_action_id(state_def.state_id, skill_def.skill_id, &"multi_move")
	action.score_bucket_id = _resolve_generated_score_bucket_id(state_actions, MOVE_TO_MULTI_UNIT_SKILL_POSITION_ACTION_SCRIPT)
	var skill_ids: Array[StringName] = [skill_def.skill_id]
	action.skill_ids = skill_ids
	action.target_selector = _resolve_target_selector(state_actions, &"nearest_enemy")
	_apply_unit_distance_style(action, unit_state, state_actions, skill_def)
	return action


func _apply_unit_distance_style(action, unit_state: BattleUnitState, state_actions: Array, skill_def: SkillDef) -> void:
	var template_action = _find_action_by_script(state_actions, USE_UNIT_SKILL_ACTION_SCRIPT)
	if template_action == null:
		template_action = _find_action_by_script(state_actions, USE_MULTI_UNIT_SKILL_ACTION_SCRIPT)
	if template_action != null:
		action.desired_min_distance = int(template_action.get("desired_min_distance"))
		action.desired_max_distance = int(template_action.get("desired_max_distance"))
		action.distance_reference = ProgressionDataUtils.to_string_name(template_action.get("distance_reference"))
		return
	var effective_range := BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_threat_range(unit_state, skill_def)
	action.desired_min_distance = mini(1, effective_range) if effective_range > 0 else 0
	action.desired_max_distance = maxi(effective_range, action.desired_min_distance)
	action.distance_reference = USE_UNIT_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_UNIT


func _apply_random_chain_distance_style(action, unit_state: BattleUnitState, state_actions: Array, skill_def: SkillDef) -> void:
	var template_action = _find_action_by_script(state_actions, USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT)
	if template_action == null:
		template_action = _find_action_by_script(state_actions, USE_UNIT_SKILL_ACTION_SCRIPT)
	if template_action == null:
		template_action = _find_action_by_script(state_actions, USE_MULTI_UNIT_SKILL_ACTION_SCRIPT)
	if template_action != null:
		action.desired_min_distance = int(template_action.get("desired_min_distance"))
		action.desired_max_distance = int(template_action.get("desired_max_distance"))
		var resolved_distance_reference := ProgressionDataUtils.to_string_name(template_action.get("distance_reference"))
		action.distance_reference = resolved_distance_reference \
			if resolved_distance_reference in [USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT.DISTANCE_REF_CANDIDATE_POOL, USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT.DISTANCE_REF_ENEMY_FRONTLINE] \
			else USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT.DISTANCE_REF_CANDIDATE_POOL
		return
	var effective_range := BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_threat_range(unit_state, skill_def)
	action.desired_min_distance = mini(1, effective_range) if effective_range > 0 else 0
	action.desired_max_distance = maxi(effective_range, action.desired_min_distance)
	action.distance_reference = USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT.DISTANCE_REF_CANDIDATE_POOL


func _apply_ground_distance_style(action, unit_state: BattleUnitState, state_actions: Array, skill_def: SkillDef) -> void:
	var template_action = _find_action_by_script(state_actions, USE_GROUND_SKILL_ACTION_SCRIPT)
	if template_action != null:
		action.desired_min_distance = int(template_action.get("desired_min_distance"))
		action.desired_max_distance = int(template_action.get("desired_max_distance"))
		action.distance_reference = ProgressionDataUtils.to_string_name(template_action.get("distance_reference"))
		return
	var effective_range := BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_threat_range(unit_state, skill_def)
	action.desired_min_distance = 0
	action.desired_max_distance = maxi(effective_range, 0)
	action.distance_reference = USE_GROUND_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_COORD


func _resolve_generated_score_bucket_id(state_actions: Array, preferred_script) -> StringName:
	var preferred_action = _find_action_by_script(state_actions, preferred_script)
	if preferred_action != null and preferred_action.score_bucket_id != &"":
		return preferred_action.score_bucket_id
	for action in state_actions:
		if action == null:
			continue
		var bucket_id := ProgressionDataUtils.to_string_name(action.get("score_bucket_id"))
		if bucket_id != &"" and action.has_method("get_declared_skill_ids") and not action.get_declared_skill_ids().is_empty():
			return bucket_id
	return &""


func _resolve_target_selector(state_actions: Array, fallback: StringName) -> StringName:
	for action in state_actions:
		if action == null:
			continue
		var selector := ProgressionDataUtils.to_string_name(action.get("target_selector"))
		if selector != &"":
			return selector
	return fallback


func _find_action_by_script(state_actions: Array, script_resource):
	for action in state_actions:
		if action != null and action.get_script() == script_resource:
			return action
	return null


func _build_action_id(state_id: StringName, skill_id: StringName, suffix: StringName) -> StringName:
	return StringName("auto_%s_%s_%s" % [String(state_id), String(skill_id), String(suffix)])


func _is_offensive_or_enemy_skill(skill_def: SkillDef) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	if ProgressionDataUtils.to_string_name(skill_def.combat_profile.special_resolution_profile_id) == &"meteor_swarm":
		return true
	var target_filter := ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_team_filter)
	if BATTLE_TARGET_TEAM_RULES_SCRIPT.is_enemy_filter(target_filter):
		return true
	for effect_def in skill_def.combat_profile.effect_defs:
		if _is_offensive_effect(skill_def, effect_def):
			return true
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant == null:
			continue
		for effect_def in cast_variant.effect_defs:
			if _is_offensive_effect(skill_def, effect_def):
				return true
	return false


func _is_offensive_effect(skill_def: SkillDef, effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	var effect_filter := ProgressionDataUtils.to_string_name(effect_def.effect_target_team_filter)
	var skill_filter := ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_team_filter if skill_def != null and skill_def.combat_profile != null else &"")
	if BATTLE_TARGET_TEAM_RULES_SCRIPT.is_enemy_filter(effect_filter):
		return true
	if BATTLE_TARGET_TEAM_RULES_SCRIPT.is_beneficial_filter(effect_filter):
		return false
	if effect_filter == &"" and BATTLE_TARGET_TEAM_RULES_SCRIPT.is_beneficial_filter(skill_filter):
		return false
	if effect_def.effect_type == &"damage" or effect_def.effect_type == PATH_STEP_AOE_EFFECT_TYPE:
		return not BATTLE_TARGET_TEAM_RULES_SCRIPT.is_beneficial_filter(skill_filter)
	if effect_def.effect_type == &"status" or effect_def.effect_type == &"apply_status" or effect_def.effect_type == &"forced_move":
		return true
	if effect_def.status_id != &"" or effect_def.save_failure_status_id != &"":
		return true
	return false


func _is_multi_unit_skill(skill_def: SkillDef) -> bool:
	return skill_def != null \
		and skill_def.combat_profile != null \
		and ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_selection_mode) == &"multi_unit"


func _is_random_chain_skill(skill_def: SkillDef) -> bool:
	return skill_def != null \
		and skill_def.combat_profile != null \
		and ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_selection_mode) == &"random_chain"


func _find_charge_path_step_aoe_variant(skill_def: SkillDef, skill_level: int):
	for cast_variant in _get_unlocked_variants(skill_def, skill_level):
		if _variant_has_effect(cast_variant, &"charge") and _variant_has_effect(cast_variant, PATH_STEP_AOE_EFFECT_TYPE):
			return cast_variant
	return null


func _find_charge_variant(skill_def: SkillDef, skill_level: int):
	for cast_variant in _get_unlocked_variants(skill_def, skill_level):
		if _variant_has_effect(cast_variant, &"charge"):
			return cast_variant
	return null


func _get_unlocked_variants(skill_def: SkillDef, skill_level: int) -> Array:
	if skill_def == null or skill_def.combat_profile == null:
		return []
	return skill_def.combat_profile.get_unlocked_cast_variants(skill_level)


func _variant_has_effect(cast_variant: CombatCastVariantDef, effect_type: StringName) -> bool:
	if cast_variant == null:
		return false
	for effect_def in cast_variant.effect_defs:
		if effect_def != null and effect_def.effect_type == effect_type:
			return true
	return false


func _get_skill_level(unit_state: BattleUnitState, skill_id: StringName) -> int:
	if unit_state == null or skill_id == &"":
		return 0
	if unit_state.known_skill_level_map.has(skill_id):
		return int(unit_state.known_skill_level_map.get(skill_id, 0))
	return 1 if unit_state.known_active_skill_ids.has(skill_id) else 0
