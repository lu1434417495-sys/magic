class_name BattleAttackCheckPolicyService
extends RefCounted

const BattleAttackCheckPolicyContext = preload("res://scripts/systems/battle/core/battle_attack_check_policy_context.gd")
const BattleAttackRollModifierBundle = preload("res://scripts/systems/battle/core/battle_attack_roll_modifier_bundle.gd")
const BattleAttackRollModifierSpec = preload("res://scripts/systems/battle/core/battle_attack_roll_modifier_spec.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleHitResolver = preload("res://scripts/systems/battle/rules/battle_hit_resolver.gd")
const BattleRepeatAttackStageSpec = preload("res://scripts/systems/battle/core/battle_repeat_attack_stage_spec.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTerrainEffectState = preload("res://scripts/systems/battle/terrain/battle_terrain_effect_state.gd")
const BattleTerrainEffectSystem = preload("res://scripts/systems/battle/terrain/battle_terrain_effect_system.gd")
const BattleTargetTeamRules = preload("res://scripts/systems/battle/rules/battle_target_team_rules.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const ROUTE_SKILL_ATTACK_CHECK: StringName = &"skill_attack_check"
const ROUTE_SKILL_ATTACK_PREVIEW: StringName = &"skill_attack_preview"
const ROUTE_REPEAT_ATTACK_STAGE_CHECK: StringName = &"repeat_attack_stage_check"
const ROUTE_REPEAT_ATTACK_PREVIEW: StringName = &"repeat_attack_preview"
const ROUTE_FORCE_HIT_NO_CRIT_PREVIEW: StringName = &"force_hit_no_crit_preview"
const ROLL_KIND_SPELL_ATTACK: StringName = &"spell_attack"
const ROLL_KIND_REPEAT_WEAPON_STAGE: StringName = &"repeat_weapon_stage"
const TRACE_EXECUTE: StringName = &"execute"
const TRACE_HUD_PREVIEW: StringName = &"hud_preview"
const DEFAULT_REPEAT_ATTACK_PREVIEW_STAGE_COUNT := 3
const REPEAT_ATTACK_PREVIEW_STAGE_GUARD := 32
const PARAM_ACCURACY_MODIFIER_SPEC := "accuracy_modifier_spec"

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null
var _hit_resolver: BattleHitResolver = null
var _terrain_effect_system: BattleTerrainEffectSystem = null


func setup(runtime, hit_resolver: BattleHitResolver, terrain_effect_system: BattleTerrainEffectSystem = null) -> void:
	_runtime = runtime
	_hit_resolver = hit_resolver
	_terrain_effect_system = terrain_effect_system


func dispose() -> void:
	_runtime = null
	_hit_resolver = null
	_terrain_effect_system = null


func build_modifier_bundle(context: BattleAttackCheckPolicyContext) -> BattleAttackRollModifierBundle:
	var bundle := BattleAttackRollModifierBundle.new()
	if context == null:
		return bundle
	var candidates := _collect_modifier_candidates(context)
	var filtered_specs: Array[BattleAttackRollModifierSpec] = []
	for candidate in candidates:
		if _modifier_applies(candidate, context):
			filtered_specs.append(candidate)
	for spec in _resolve_stacked_specs(filtered_specs):
		bundle.add_spec(spec)
	return bundle


func build_attack_context(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	check_route: StringName = ROUTE_SKILL_ATTACK_CHECK,
	trace_source: StringName = TRACE_EXECUTE,
	force_hit_no_crit: bool = false
) -> BattleAttackCheckPolicyContext:
	var context := _build_context(
		battle_state,
		active_unit,
		target_unit,
		skill_def,
		ROLL_KIND_SPELL_ATTACK,
		check_route,
		trace_source
	)
	context.force_hit_no_crit = force_hit_no_crit
	return context


func build_attack_check(
	context: BattleAttackCheckPolicyContext,
	flat_bonus: int = 0,
	flat_penalty: int = 0
) -> Dictionary:
	if _hit_resolver == null or context == null:
		return {}
	if context.roll_kind == &"":
		context.roll_kind = ROLL_KIND_SPELL_ATTACK
	var modifier_bundle := build_modifier_bundle(context)
	var attack_check := _hit_resolver.build_skill_attack_check(
		context.attacker,
		context.target,
		context.skill_def,
		flat_bonus + int(modifier_bundle.total_bonus),
		flat_penalty + int(modifier_bundle.total_penalty)
	)
	_append_modifier_bundle_payload(attack_check, modifier_bundle)
	return attack_check


func build_attack_preview(context: BattleAttackCheckPolicyContext) -> Dictionary:
	if _hit_resolver == null or context == null:
		return {}
	if context.roll_kind == &"":
		context.roll_kind = ROLL_KIND_SPELL_ATTACK
	if context.force_hit_no_crit:
		var force_preview := _hit_resolver.build_force_hit_no_crit_attack_preview()
		context.check_route = ROUTE_FORCE_HIT_NO_CRIT_PREVIEW
		_append_modifier_bundle_payload(force_preview, build_modifier_bundle(context))
		return force_preview
	if context.check_route == &"":
		context.check_route = ROUTE_SKILL_ATTACK_PREVIEW
	var modifier_bundle := build_modifier_bundle(context)
	if modifier_bundle.is_empty():
		return _hit_resolver.build_skill_attack_preview(
			context.battle_state,
			context.attacker,
			context.target,
			context.skill_def,
			false
		)
	var attack_check := build_attack_check(context)
	var resolved_check := _hit_resolver._build_fate_aware_attack_check_preview(
		context.battle_state,
		context.attacker,
		context.target,
		attack_check
	)
	var success_rate := int(resolved_check.get("success_rate_percent", 0))
	var base_hit_rate := int(resolved_check.get("base_hit_rate_percent", success_rate))
	var preview_text := String(resolved_check.get("preview_text", ""))
	var preview := {
		"summary_text": "预计命中率 %s" % preview_text,
		"stage_checks": [resolved_check.duplicate(true)],
		"stage_hit_rates": [success_rate],
		"stage_success_rates": [success_rate],
		"stage_base_hit_rates": [base_hit_rate],
		"stage_required_rolls": [int(resolved_check.get("display_required_roll", 20))],
		"stage_preview_texts": [preview_text],
		"hit_rate_percent": success_rate,
		"success_rate_percent": success_rate,
		"base_hit_rate_percent": base_hit_rate,
	}
	_append_modifier_bundle_payload(preview, modifier_bundle)
	return preview


func build_repeat_attack_preview(
	context: BattleAttackCheckPolicyContext,
	stage_specs: Array[BattleRepeatAttackStageSpec]
) -> Dictionary:
	if _hit_resolver == null or context == null or context.attacker == null or context.target == null or context.skill_def == null or stage_specs.is_empty():
		return {}
	var normalized_stage_count := mini(maxi(stage_specs.size(), 1), REPEAT_ATTACK_PREVIEW_STAGE_GUARD)
	var stage_checks: Array[Dictionary] = []
	var stage_hit_rates: Array[int] = []
	var stage_success_rates: Array[int] = []
	var stage_base_hit_rates: Array[int] = []
	var stage_required_rolls: Array[int] = []
	var stage_preview_texts: Array[String] = []
	var combined_breakdown: Array[Dictionary] = []
	for stage_index in range(normalized_stage_count):
		var stage_spec := stage_specs[stage_index] as BattleRepeatAttackStageSpec
		if stage_spec == null:
			continue
		stage_spec.fate_aware = true
		var stage_context := _copy_context_for_repeat_stage(context, stage_spec, ROUTE_REPEAT_ATTACK_PREVIEW)
		var attack_check := build_fate_aware_repeat_attack_stage_hit_check(stage_context)
		var stage_success_rate := int(attack_check.get("success_rate_percent", 0))
		stage_checks.append(attack_check.duplicate(true))
		stage_hit_rates.append(stage_success_rate)
		stage_success_rates.append(stage_success_rate)
		stage_base_hit_rates.append(int(attack_check.get("base_hit_rate_percent", 0)))
		stage_required_rolls.append(int(attack_check.get("display_required_roll", 20)))
		stage_preview_texts.append(String(attack_check.get("preview_text", "")))
		for entry in attack_check.get("attack_roll_modifier_breakdown", []):
			if entry is Dictionary:
				combined_breakdown.append((entry as Dictionary).duplicate(true))
	var preview := {
		"summary_text": _hit_resolver._format_repeat_attack_preview_summary(stage_checks),
		"stage_checks": stage_checks,
		"stage_hit_rates": stage_hit_rates,
		"stage_success_rates": stage_success_rates,
		"stage_base_hit_rates": stage_base_hit_rates,
		"stage_required_rolls": stage_required_rolls,
		"stage_preview_texts": stage_preview_texts,
		"hit_rate_percent": int(round(float(_hit_resolver._average_ints(stage_success_rates)))),
		"success_rate_percent": int(round(float(_hit_resolver._average_ints(stage_success_rates)))),
		"base_hit_rate_percent": int(round(float(_hit_resolver._average_ints(stage_base_hit_rates)))),
		"base_attack_bonus": int(stage_specs[0].stage_base_attack_bonus),
		"follow_up_attack_penalty": int(stage_specs[0].follow_up_attack_penalty),
	}
	if not combined_breakdown.is_empty():
		preview["attack_roll_modifier_breakdown"] = combined_breakdown
	return preview


func build_repeat_attack_stage_context(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	stage_spec: BattleRepeatAttackStageSpec = null,
	check_route: StringName = ROUTE_REPEAT_ATTACK_STAGE_CHECK,
	trace_source: StringName = TRACE_EXECUTE
) -> BattleAttackCheckPolicyContext:
	var context := _build_context(
		battle_state,
		active_unit,
		target_unit,
		skill_def,
		ROLL_KIND_REPEAT_WEAPON_STAGE,
		check_route,
		trace_source
	)
	context.repeat_stage_spec = stage_spec
	return context


func build_repeat_attack_stage_hit_check(
	context: BattleAttackCheckPolicyContext
) -> Dictionary:
	if _hit_resolver == null or context == null or context.repeat_stage_spec == null:
		return {}
	context.roll_kind = ROLL_KIND_REPEAT_WEAPON_STAGE
	var resolved_stage_spec := context.repeat_stage_spec
	var modifier_bundle := build_modifier_bundle(context)
	var attack_check := _hit_resolver.build_skill_attack_check(
		context.attacker,
		context.target,
		context.skill_def,
		int(resolved_stage_spec.stage_base_attack_bonus) + int(modifier_bundle.total_bonus),
		int(resolved_stage_spec.resolve_stage_attack_penalty()) + int(modifier_bundle.total_penalty)
	)
	_append_modifier_bundle_payload(attack_check, modifier_bundle)
	return attack_check


func build_fate_aware_repeat_attack_stage_hit_check(context: BattleAttackCheckPolicyContext) -> Dictionary:
	if _hit_resolver == null or context == null or context.repeat_stage_spec == null:
		return {}
	context.repeat_stage_spec.fate_aware = true
	var base_attack_check := build_repeat_attack_stage_hit_check(context)
	return _hit_resolver._build_fate_aware_attack_check_preview(
		context.battle_state,
		context.attacker,
		context.target,
		base_attack_check
	)


func roll_attack_check(battle_state: BattleState, attack_check: Dictionary) -> Dictionary:
	return _hit_resolver.roll_attack_check(battle_state, attack_check) if _hit_resolver != null else {}


func _build_context(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	roll_kind: StringName,
	check_route: StringName,
	trace_source: StringName
) -> BattleAttackCheckPolicyContext:
	var context := BattleAttackCheckPolicyContext.new()
	context.battle_state = battle_state if battle_state != null else _resolve_battle_state()
	context.attacker = active_unit
	context.target = target_unit
	context.skill_def = skill_def
	context.roll_kind = roll_kind
	context.check_route = check_route
	context.trace_source = trace_source
	context.distance = _resolve_distance(active_unit, target_unit)
	context.source_coord = active_unit.coord if active_unit != null else Vector2i(-1, -1)
	context.target_coord = target_unit.coord if target_unit != null else Vector2i(-1, -1)
	return context


func _collect_modifier_candidates(context: BattleAttackCheckPolicyContext) -> Array[BattleAttackRollModifierSpec]:
	var candidates: Array[BattleAttackRollModifierSpec] = []
	candidates.append_array(_collect_terrain_modifier_candidates(context))
	return candidates


func _collect_terrain_modifier_candidates(context: BattleAttackCheckPolicyContext) -> Array[BattleAttackRollModifierSpec]:
	var candidates: Array[BattleAttackRollModifierSpec] = []
	var state := context.battle_state
	if state == null:
		return candidates
	var coords := _collect_endpoint_coords(context)
	for coord in coords:
		var cell := state.cells.get(coord) as BattleCellState
		if cell == null:
			continue
		for effect_variant in cell.timed_terrain_effects:
			var effect_state := effect_variant as BattleTerrainEffectState
			if effect_state == null or not BattleTerrainEffectSystem.is_terrain_effect_active(effect_state):
				continue
			var raw_spec: Variant = _get_param(effect_state.params, PARAM_ACCURACY_MODIFIER_SPEC, null)
			if raw_spec is not Dictionary:
				continue
			var spec := BattleAttackRollModifierSpec.from_partial_dict(raw_spec) as BattleAttackRollModifierSpec
			if spec == null:
				continue
			if not _effect_coord_matches_endpoint_mode(coord, spec, context):
				continue
			if spec.source_domain == &"":
				spec.source_domain = &"terrain"
			if spec.source_id == &"":
				spec.source_id = effect_state.effect_id
			if spec.source_instance_id.is_empty():
				spec.source_instance_id = String(effect_state.field_instance_id)
			candidates.append(spec)
	return candidates


func _modifier_applies(spec: BattleAttackRollModifierSpec, context: BattleAttackCheckPolicyContext) -> bool:
	if spec == null or context == null:
		return false
	if spec.applies_to != &"" and spec.applies_to != &"attack_roll":
		return false
	if spec.modifier_delta == 0:
		return false
	if spec.roll_kind_filter != &"" and spec.roll_kind_filter != context.roll_kind:
		return false
	if spec.distance_min_exclusive >= 0 and context.distance <= spec.distance_min_exclusive:
		return false
	if spec.distance_max_inclusive >= 0 and context.distance > spec.distance_max_inclusive:
		return false
	if not _team_filter_applies(spec.target_team_filter, context.attacker, context.target):
		return false
	return true


func _resolve_stacked_specs(candidates: Array[BattleAttackRollModifierSpec]) -> Array[BattleAttackRollModifierSpec]:
	var grouped: Dictionary = {}
	var order: Array[StringName] = []
	for index in range(candidates.size()):
		var spec := candidates[index]
		if spec == null:
			continue
		var stack_key := spec.stack_key
		if stack_key == &"":
			stack_key = StringName("__unique_%d_%s_%s_%s" % [
				index,
				String(spec.source_domain),
				String(spec.source_id),
				spec.source_instance_id,
			])
		if not grouped.has(stack_key):
			grouped[stack_key] = []
			order.append(stack_key)
		var group: Array = grouped[stack_key]
		group.append(spec)
		grouped[stack_key] = group

	var resolved_specs: Array[BattleAttackRollModifierSpec] = []
	for stack_key in order:
		var group: Array = grouped.get(stack_key, [])
		var resolved := _resolve_stack_group(group)
		if resolved != null:
			resolved_specs.append(resolved)
	resolved_specs.sort_custom(func(a: BattleAttackRollModifierSpec, b: BattleAttackRollModifierSpec) -> bool:
		var a_key := "%s|%s|%s|%s|%s" % [String(a.source_domain), String(a.stack_key), String(a.source_id), a.source_instance_id, a.label]
		var b_key := "%s|%s|%s|%s|%s" % [String(b.source_domain), String(b.stack_key), String(b.source_id), b.source_instance_id, b.label]
		return a_key < b_key
	)
	return resolved_specs


func _resolve_stack_group(group: Array) -> BattleAttackRollModifierSpec:
	if group.is_empty():
		return null
	var first := group[0] as BattleAttackRollModifierSpec
	if first == null:
		return null
	var has_bonus := false
	var has_penalty := false
	for spec_variant in group:
		var spec := spec_variant as BattleAttackRollModifierSpec
		if spec == null:
			continue
		has_bonus = has_bonus or spec.modifier_delta > 0
		has_penalty = has_penalty or spec.modifier_delta < 0
	if has_bonus and has_penalty:
		return null
	match first.stack_mode:
		&"exclusive":
			if group.size() != 1:
				return null
			return first
		&"max":
			return _pick_max_stack_spec(group)
		&"min":
			return _pick_min_stack_spec(group)
		_:
			return _sum_stack_group(group)


func _pick_max_stack_spec(group: Array) -> BattleAttackRollModifierSpec:
	var best := group[0] as BattleAttackRollModifierSpec
	for spec_variant in group:
		var spec := spec_variant as BattleAttackRollModifierSpec
		if spec == null:
			continue
		if best.modifier_delta < 0 or spec.modifier_delta < 0:
			if spec.modifier_delta < best.modifier_delta:
				best = spec
		elif spec.modifier_delta > best.modifier_delta:
			best = spec
	return best


func _pick_min_stack_spec(group: Array) -> BattleAttackRollModifierSpec:
	var best := group[0] as BattleAttackRollModifierSpec
	for spec_variant in group:
		var spec := spec_variant as BattleAttackRollModifierSpec
		if spec == null:
			continue
		if best.modifier_delta > 0 and spec.modifier_delta > 0 and spec.modifier_delta < best.modifier_delta:
			best = spec
		elif best.modifier_delta < 0 and spec.modifier_delta < 0 and spec.modifier_delta > best.modifier_delta:
			best = spec
	return best


func _sum_stack_group(group: Array) -> BattleAttackRollModifierSpec:
	var base := group[0] as BattleAttackRollModifierSpec
	if base == null:
		return null
	var summed := BattleAttackRollModifierSpec.new()
	summed.source_domain = base.source_domain
	summed.source_id = base.source_id
	summed.source_instance_id = base.source_instance_id
	summed.label = base.label
	summed.stack_key = base.stack_key
	summed.stack_mode = base.stack_mode
	summed.roll_kind_filter = base.roll_kind_filter
	summed.endpoint_mode = base.endpoint_mode
	summed.distance_min_exclusive = base.distance_min_exclusive
	summed.distance_max_inclusive = base.distance_max_inclusive
	summed.target_team_filter = base.target_team_filter
	summed.footprint_mode = base.footprint_mode
	summed.applies_to = base.applies_to
	for spec_variant in group:
		var spec := spec_variant as BattleAttackRollModifierSpec
		if spec != null:
			summed.modifier_delta += int(spec.modifier_delta)
	return summed


func _append_modifier_bundle_payload(target: Dictionary, modifier_bundle: BattleAttackRollModifierBundle) -> void:
	if target == null or modifier_bundle == null or modifier_bundle.is_empty():
		return
	target["attack_roll_modifier_breakdown"] = modifier_bundle.get_breakdown_payload()


func _copy_context_for_repeat_stage(
	source_context: BattleAttackCheckPolicyContext,
	stage_spec: BattleRepeatAttackStageSpec,
	check_route: StringName
) -> BattleAttackCheckPolicyContext:
	var context := BattleAttackCheckPolicyContext.new()
	if source_context == null:
		context.repeat_stage_spec = stage_spec
		return context
	context.battle_state = source_context.battle_state
	context.attacker = source_context.attacker
	context.target = source_context.target
	context.skill_def = source_context.skill_def
	context.cast_variant = source_context.cast_variant
	context.roll_kind = ROLL_KIND_REPEAT_WEAPON_STAGE
	context.check_route = check_route if check_route != &"" else source_context.check_route
	context.trace_source = source_context.trace_source
	context.distance = source_context.distance
	context.force_hit_no_crit = source_context.force_hit_no_crit
	context.source_coord = source_context.source_coord
	context.target_coord = source_context.target_coord
	context.repeat_stage_spec = stage_spec
	return context


func _collect_endpoint_coords(context: BattleAttackCheckPolicyContext) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	var include_attacker := context != null and (context.attacker != null) and _endpoint_includes_attacker(context)
	var include_target := context != null and (context.target != null) and _endpoint_includes_target(context)
	if include_attacker:
		_append_unit_coords(coords, context.attacker, context.source_coord)
	if include_target:
		_append_unit_coords(coords, context.target, context.target_coord)
	return coords


func _endpoint_includes_attacker(context: BattleAttackCheckPolicyContext) -> bool:
	return context != null


func _endpoint_includes_target(context: BattleAttackCheckPolicyContext) -> bool:
	return context != null


func _append_unit_coords(coords: Array[Vector2i], unit_state: BattleUnitState, fallback_coord: Vector2i) -> void:
	if unit_state == null:
		return
	unit_state.refresh_footprint()
	if unit_state.occupied_coords.is_empty():
		_append_coord_unique(coords, fallback_coord)
		return
	for coord in unit_state.occupied_coords:
		_append_coord_unique(coords, coord)


func _effect_coord_matches_endpoint_mode(coord: Vector2i, spec: BattleAttackRollModifierSpec, context: BattleAttackCheckPolicyContext) -> bool:
	var attacker_contains := _unit_contains_coord(context.attacker if context != null else null, coord)
	var target_contains := _unit_contains_coord(context.target if context != null else null, coord)
	match spec.endpoint_mode:
		&"attacker":
			return attacker_contains
		&"target":
			return target_contains
		&"both":
			return attacker_contains and target_contains
		_:
			return attacker_contains or target_contains


func _unit_contains_coord(unit_state: BattleUnitState, coord: Vector2i) -> bool:
	if unit_state == null:
		return false
	unit_state.refresh_footprint()
	if unit_state.occupied_coords.is_empty():
		return unit_state.coord == coord
	return unit_state.occupied_coords.has(coord)


func _append_coord_unique(coords: Array[Vector2i], coord: Vector2i) -> void:
	if coord.x < 0 or coord.y < 0:
		return
	if coords.has(coord):
		return
	coords.append(coord)


func _team_filter_applies(filter: StringName, attacker: BattleUnitState, target_unit: BattleUnitState) -> bool:
	return BattleTargetTeamRules.is_unit_valid_for_filter(attacker, target_unit, filter)


func _resolve_distance(active_unit: BattleUnitState, target_unit: BattleUnitState) -> int:
	if active_unit == null or target_unit == null:
		return -1
	return absi(active_unit.coord.x - target_unit.coord.x) + absi(active_unit.coord.y - target_unit.coord.y)


func _resolve_battle_state() -> BattleState:
	if _runtime == null or not _runtime.has_method("get_state"):
		return null
	return _runtime.get_state() as BattleState


func _get_param(params: Dictionary, key: String, fallback: Variant) -> Variant:
	if params == null:
		return fallback
	if params.has(key):
		return params[key]
	var string_name_key := StringName(key)
	if params.has(string_name_key):
		return params[string_name_key]
	return fallback
