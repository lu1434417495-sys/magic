class_name BattleAiActionAssembler
extends RefCounted

const BATTLE_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_range_service.gd")
const USE_CHARGE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_charge_action.gd")
const USE_CHARGE_PATH_AOE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_charge_path_aoe_action.gd")
const USE_GROUND_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_ground_skill_action.gd")
const USE_MULTI_UNIT_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_multi_unit_skill_action.gd")
const USE_UNIT_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_unit_skill_action.gd")
const MOVE_TO_MULTI_UNIT_SKILL_POSITION_ACTION_SCRIPT = preload("res://scripts/enemies/actions/move_to_multi_unit_skill_position_action.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

const PATH_STEP_AOE_EFFECT_TYPE: StringName = &"path_step_aoe"


func build_unit_action_plan(unit_state: BattleUnitState, brain, skill_defs: Dictionary) -> Dictionary:
	var plan: Dictionary = {}
	if unit_state == null or brain == null:
		return plan
	for state_def in brain.get_states():
		if state_def == null:
			continue
		var actions: Array = state_def.get_actions()
		var declared_skill_ids: Dictionary = _collect_declared_skill_ids(actions)
		for skill_id in unit_state.known_active_skill_ids:
			var normalized_skill_id := ProgressionDataUtils.to_string_name(skill_id)
			if normalized_skill_id == &"" or declared_skill_ids.has(normalized_skill_id):
				continue
			var skill_def = skill_defs.get(normalized_skill_id) as SkillDef
			var generated_actions: Array = _build_skill_actions(unit_state, state_def, actions, skill_def)
			if generated_actions.is_empty():
				continue
			for generated_action in generated_actions:
				if generated_action == null:
					continue
				actions.append(generated_action)
			declared_skill_ids[normalized_skill_id] = true
		plan[state_def.state_id] = actions
	return plan


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
	if target_filter == &"enemy" or target_filter == &"hostile":
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
	if effect_filter == &"enemy" or effect_filter == &"hostile":
		return true
	if effect_filter == &"ally" or effect_filter == &"friendly" or effect_filter == &"self":
		return false
	if effect_filter == &"" and (skill_filter == &"ally" or skill_filter == &"friendly" or skill_filter == &"self"):
		return false
	if effect_def.effect_type == &"damage" or effect_def.effect_type == PATH_STEP_AOE_EFFECT_TYPE:
		return skill_filter != &"ally" and skill_filter != &"friendly" and skill_filter != &"self"
	if effect_def.effect_type == &"status" or effect_def.effect_type == &"apply_status" or effect_def.effect_type == &"forced_move":
		return true
	if effect_def.status_id != &"" or effect_def.save_failure_status_id != &"":
		return true
	return false


func _is_multi_unit_skill(skill_def: SkillDef) -> bool:
	return skill_def != null \
		and skill_def.combat_profile != null \
		and ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_selection_mode) in [&"multi_unit", &"random_chain"]


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
