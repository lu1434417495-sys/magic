class_name BattleMeteorSwarmResolver
extends RefCounted

const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattlePreview = preload("res://scripts/systems/battle/core/battle_preview.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const BattleReportFormatter = preload("res://scripts/systems/battle/rules/battle_report_formatter.gd")
const BattleDamageResolver = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BattleSaveResolver = preload("res://scripts/systems/battle/rules/battle_save_resolver.gd")
const MeteorSwarmCastContext = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_cast_context.gd")
const MeteorSwarmCommitResult = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_commit_result.gd")
const MeteorSwarmImpactComponent = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_impact_component.gd")
const MeteorSwarmPreviewFacts = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_preview_facts.gd")
const MeteorSwarmProfile = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd")
const MeteorSwarmTargetOutcome = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_target_outcome.gd")
const MeteorSwarmTargetPlan = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_target_plan.gd")

const PROFILE_ID: StringName = &"meteor_swarm"
const COVERAGE_SHAPE_ID: StringName = &"square_7x7"
const DEFAULT_SKILL_ID: StringName = &"mage_meteor_swarm"
const STATUS_METEOR_CONCUSSED: StringName = &"meteor_concussed"
const DAMAGE_TAG_PHYSICAL_BLUNT: StringName = &"physical_blunt"
const MITIGATION_TIER_NORMAL: StringName = &"normal"
const MITIGATION_TIER_HALF: StringName = &"half"
const MITIGATION_TIER_DOUBLE: StringName = &"double"
const MITIGATION_TIER_IMMUNE: StringName = &"immune"
const SAVE_PROFILE_METEOR_DEX_HALF: StringName = &"meteor_dex_half"

var _report_formatter := BattleReportFormatter.new()

var _runtime = null
var _attack_check_policy_service = null


func setup(runtime, attack_check_policy_service = null) -> void:
	_runtime = runtime
	_attack_check_policy_service = attack_check_policy_service


func dispose() -> void:
	_runtime = null
	_attack_check_policy_service = null


func populate_preview(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	preview: BattlePreview
) -> void:
	if preview == null:
		return
	preview.allowed = false
	if _runtime == null or _runtime.get_state() == null:
		preview.log_lines.append("技能或目标无效。")
		return
	var cast_variant = _resolve_ground_cast_variant(active_unit, command, skill_def)
	var validation: Dictionary = _runtime._validate_ground_skill_command(active_unit, skill_def, cast_variant, command)
	if not bool(validation.get("allowed", false)):
		preview.log_lines.append(String(validation.get("message", "技能或目标无效。")))
		return
	var target_coords := _extract_target_coords(validation)
	if target_coords.is_empty():
		preview.log_lines.append("技能或目标无效。")
		return
	var anchor_coord := target_coords[0]
	var context := build_cast_context(active_unit, command, skill_def, cast_variant, anchor_coord, anchor_coord)
	var facts := build_preview_facts(context)
	preview.allowed = true
	preview.resolved_anchor_coord = anchor_coord
	preview.target_coords = facts.target_coords.duplicate()
	preview.target_unit_ids = facts.target_unit_ids.duplicate()
	preview.special_profile_preview_facts = facts
	preview.hit_preview = {
		"summary_text": "陨星雨影响 %d 格、预计波及 %d 个单位。" % [facts.impact_count, facts.expected_target_count],
		"modifier_breakdown": facts.attack_roll_modifier_breakdown.duplicate(true),
		"source": "special_profile_preview_facts",
	}
	preview.log_lines.append("可施放陨星雨：影响 %d 格，预计波及 %d 个单位。" % [
		facts.impact_count,
		facts.expected_target_count,
	])


func build_cast_context(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	nominal_anchor_coord: Vector2i,
	final_anchor_coord: Vector2i,
	spell_control_context: Dictionary = {},
	drift_context: Dictionary = {}
) -> MeteorSwarmCastContext:
	var context := MeteorSwarmCastContext.new()
	context.active_unit = active_unit
	context.command = command
	context.skill_def = skill_def
	context.cast_variant = cast_variant
	context.profile = _resolve_profile()
	context.nominal_anchor_coord = nominal_anchor_coord
	context.final_anchor_coord = final_anchor_coord
	context.spell_control_context = spell_control_context.duplicate(true)
	context.drift_context = drift_context.duplicate(true)
	return context


func build_preview_facts(context: MeteorSwarmCastContext) -> MeteorSwarmPreviewFacts:
	var plan := build_target_plan(context)
	var facts := MeteorSwarmPreviewFacts.new()
	facts.profile_id = PROFILE_ID
	facts.skill_id = plan.skill_id
	facts.preview_fact_id = StringName("meteor_swarm:%s" % plan.nominal_plan_signature)
	facts.nominal_plan_signature = plan.nominal_plan_signature
	facts.final_plan_signature = plan.final_plan_signature
	facts.resolved_anchor_coord = plan.final_anchor_coord
	facts.target_unit_ids = plan.target_unit_ids.duplicate()
	facts.target_coords = plan.affected_coords.duplicate()
	facts.terrain_summary = _build_terrain_summary(plan)
	facts.target_numeric_summary = _build_target_numeric_summary(plan)
	facts.friendly_fire_numeric_summary = _build_friendly_fire_numeric_summary(plan)
	facts.attack_roll_modifier_breakdown = _build_future_attack_roll_modifier_breakdown(plan)
	facts.impact_count = plan.affected_coords.size()
	facts.expected_target_count = plan.target_unit_ids.size()
	facts.expected_terrain_effect_count = _count_expected_terrain_effects(plan)
	facts.friendly_fire_risk_percent = _resolve_friendly_fire_risk_percent(facts.friendly_fire_numeric_summary)
	facts.component_preview = _build_component_preview(plan)
	return facts


func build_target_plan(context: MeteorSwarmCastContext) -> MeteorSwarmTargetPlan:
	var plan := MeteorSwarmTargetPlan.new()
	var profile := context.profile if context != null and context.profile != null else _resolve_profile()
	plan.profile = profile
	plan.coverage_shape_id = profile.coverage_shape_id if profile != null else COVERAGE_SHAPE_ID
	plan.radius = int(profile.radius) if profile != null else 3
	plan.source_unit = context.active_unit if context != null else null
	plan.source_unit_id = plan.source_unit.unit_id if plan.source_unit != null else &""
	plan.skill_def = context.skill_def if context != null else null
	plan.skill_id = plan.skill_def.skill_id if plan.skill_def != null else DEFAULT_SKILL_ID
	plan.nominal_anchor_coord = context.nominal_anchor_coord if context != null else Vector2i(-1, -1)
	plan.final_anchor_coord = context.final_anchor_coord if context != null else Vector2i(-1, -1)
	plan.drift_applied = context != null and context.has_drift()
	plan.drift_from_coord = plan.nominal_anchor_coord if plan.drift_applied else Vector2i(-1, -1)
	if _runtime == null or _runtime.get_state() == null or plan.final_anchor_coord == Vector2i(-1, -1):
		plan.nominal_plan_signature = _build_plan_signature(plan, plan.nominal_anchor_coord)
		plan.final_plan_signature = _build_plan_signature(plan, plan.final_anchor_coord)
		return plan

	var state = _runtime.get_state()
	var grid_service = _runtime.get_grid_service()
	var seen_unit_ids: Dictionary = {}
	for dy in range(-plan.radius, plan.radius + 1):
		for dx in range(-plan.radius, plan.radius + 1):
			var coord := plan.final_anchor_coord + Vector2i(dx, dy)
			if not grid_service.is_inside(state, coord):
				continue
			var ring := maxi(absi(dx), absi(dy))
			plan.affected_coords.append(coord)
			plan.ring_by_coord[coord] = ring
			var cell = grid_service.get_cell(state, coord)
			if cell == null or cell.occupant_unit_id == &"" or seen_unit_ids.has(cell.occupant_unit_id):
				continue
			var unit_state := state.units.get(cell.occupant_unit_id) as BattleUnitState
			if unit_state == null or not unit_state.is_alive:
				continue
			seen_unit_ids[cell.occupant_unit_id] = true
			plan.target_unit_ids.append(cell.occupant_unit_id)
			plan.unit_primary_coord_by_id[cell.occupant_unit_id] = coord
	plan.affected_coords.sort_custom(_sort_coord_ascending)
	plan.target_unit_ids.sort_custom(func(left: StringName, right: StringName) -> bool:
		var left_coord := plan.get_primary_coord_for_unit(left)
		var right_coord := plan.get_primary_coord_for_unit(right)
		return _sort_coord_ascending(left_coord, right_coord)
	)
	_populate_unit_distances(plan)
	plan.nominal_plan_signature = _build_plan_signature_for_anchor(plan, plan.nominal_anchor_coord)
	plan.final_plan_signature = _build_plan_signature_for_anchor(plan, plan.final_anchor_coord)
	return plan


func resolve(plan: MeteorSwarmTargetPlan) -> MeteorSwarmCommitResult:
	var result := MeteorSwarmCommitResult.new()
	if plan == null or plan.profile == null or _runtime == null or _runtime.get_state() == null:
		return result
	result.plan = plan
	result.add_changed_unit_id(plan.source_unit_id)
	var terrain_effects := _apply_terrain_effects(plan)
	for terrain_effect in terrain_effects:
		result.terrain_effects.append(terrain_effect.duplicate(true))
		var terrain_coord = terrain_effect.get("coord", Vector2i(-1, -1))
		if terrain_coord is Vector2i:
			result.add_changed_coord(terrain_coord)
	var component_totals: Dictionary = {}
	for target_unit_id in plan.target_unit_ids:
		var target_unit := _runtime.get_state().units.get(target_unit_id) as BattleUnitState
		if target_unit == null or not target_unit.is_alive:
			continue
		var target_outcome := _resolve_target(plan, target_unit, component_totals)
		result.target_outcomes.append(target_outcome)
		result.total_damage += target_outcome.total_damage
		result.total_healing += target_outcome.total_healing
		result.add_changed_unit_id(target_unit.unit_id)
		for occupied_coord in target_unit.occupied_coords:
			result.add_changed_coord(occupied_coord)
		if target_outcome.defeated:
			result.add_defeated_unit_id(target_unit.unit_id)
	for terrain_coord in plan.affected_coords:
		result.add_changed_coord(terrain_coord)
	result.report_entries.append(_build_report_entry(plan, result, component_totals))
	result.log_lines.append("%s 施放陨星雨，灾害区覆盖 %d 格，波及 %d 个单位，造成 %d 点总伤害。" % [
		plan.source_unit.display_name if plan.source_unit != null else "单位",
		plan.affected_coords.size(),
		result.target_outcomes.size(),
		result.total_damage,
	])
	var terrain_summary := _build_terrain_summary(plan)
	result.log_lines.append("陨击留下地形：陨坑 %d 格，碎石 %d 格，尘土 %d 格。" % [
		int(terrain_summary.get("crater_count", 0)),
		int(terrain_summary.get("rubble_count", 0)),
		int(terrain_summary.get("dust_count", 0)),
	])
	return result


func _resolve_target(
	plan: MeteorSwarmTargetPlan,
	target_unit: BattleUnitState,
	component_totals: Dictionary
) -> MeteorSwarmTargetOutcome:
	var outcome := MeteorSwarmTargetOutcome.new()
	outcome.target_unit_id = target_unit.unit_id
	outcome.target_coord = plan.get_primary_coord_for_unit(target_unit.unit_id)
	outcome.target_faction_id = target_unit.faction_id
	outcome.distance_from_anchor = plan.get_distance_for_unit(target_unit.unit_id)
	var covers_center := _unit_covers_coord(target_unit, plan.final_anchor_coord)
	for component_variant in plan.profile.get_impact_components():
		var component := component_variant as MeteorSwarmImpactComponent
		if component == null or not component.applies_to_distance(outcome.distance_from_anchor, covers_center):
			continue
		var effect_def := _build_damage_effect_def(component, outcome.distance_from_anchor)
		var damage_context := {
			"skill_id": plan.skill_id,
			"meteor_component_id": component.component_id,
			"meteor_role_label": component.role_label,
			"dispatch_events": false,
		}
		var damage_result: Dictionary = _runtime.get_damage_resolver().resolve_effects(plan.source_unit, target_unit, [effect_def], damage_context)
		_tag_damage_events(damage_result, component, outcome.distance_from_anchor)
		outcome.add_component(component)
		outcome.total_damage += int(damage_result.get("damage", 0))
		outcome.total_healing += int(damage_result.get("healing", 0))
		for damage_event in damage_result.get("damage_events", []):
			if damage_event is Dictionary:
				outcome.damage_events.append((damage_event as Dictionary).duplicate(true))
		var component_fact := _build_component_report_fact(component, damage_result, outcome.distance_from_anchor)
		outcome.report_component_breakdown.append(component_fact)
		_add_component_total(component_totals, component_fact)
	if outcome.distance_from_anchor <= 1 and plan.profile.concussed_status_id != &"" and target_unit.is_alive:
		var status_result := _apply_concussed_status(plan, target_unit)
		for status_id_variant in status_result.get("status_effect_ids", []):
			var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
			outcome.add_status_effect_id(status_id)
	target_unit.is_alive = target_unit.current_hp > 0
	outcome.defeated = not target_unit.is_alive
	return outcome


func _apply_terrain_effects(plan: MeteorSwarmTargetPlan) -> Array[Dictionary]:
	var effects: Array[Dictionary] = []
	var state = _runtime.get_state()
	var terrain_system = _runtime._terrain_effect_system
	if state == null or terrain_system == null:
		return effects
	for coord in plan.affected_coords:
		var ring := plan.get_ring_for_coord(coord)
		for terrain_profile in plan.profile.get_terrain_profiles_for_ring(ring):
			var effect_def := _build_terrain_effect_def(terrain_profile)
			var field_instance_id: StringName = _runtime._build_terrain_effect_instance_id(effect_def.terrain_effect_id)
			if not terrain_system.upsert_timed_terrain_effect(coord, plan.source_unit, plan.skill_def, effect_def, field_instance_id):
				continue
			effects.append({
				"coord": coord,
				"ring": ring,
				"terrain_profile_id": String(terrain_profile.get("terrain_profile_id", terrain_profile.get(&"terrain_profile_id", ""))),
				"terrain_effect_id": String(effect_def.terrain_effect_id),
				"lifetime_policy": String(effect_def.params.get("lifetime_policy", "")),
				"move_cost_delta": int(effect_def.params.get("move_cost_delta", 0)),
				"render_overlay_id": String(effect_def.params.get("render_overlay_id", "")),
			})
	return effects


func _build_damage_effect_def(component: MeteorSwarmImpactComponent, distance_from_anchor: int) -> CombatEffectDef:
	var effect := CombatEffectDef.new()
	effect.effect_type = &"damage"
	effect.damage_tag = component.damage_tag
	effect.effect_target_team_filter = &"any"
	effect.power = component.base_power
	effect.params = {
		"dice_count": component.dice_count,
		"dice_sides": component.dice_sides,
		"runtime_pre_resistance_damage_multiplier": component.get_damage_scale(distance_from_anchor),
		"meteor_component_id": String(component.component_id),
		"meteor_role_label": String(component.role_label),
	}
	_apply_save_profile_to_damage_effect(effect, component)
	return effect


func _build_terrain_effect_def(terrain_profile: Dictionary) -> CombatEffectDef:
	var effect := CombatEffectDef.new()
	var terrain_profile_id := ProgressionDataUtils.to_string_name(terrain_profile.get("terrain_profile_id", terrain_profile.get(&"terrain_profile_id", "")))
	effect.effect_type = &"terrain_effect"
	effect.tick_effect_type = ProgressionDataUtils.to_string_name(terrain_profile.get("tick_effect_type", terrain_profile.get(&"tick_effect_type", &"none")))
	effect.terrain_effect_id = terrain_profile_id
	effect.duration_tu = int(terrain_profile.get("duration_tu", terrain_profile.get(&"duration_tu", 0)))
	effect.tick_interval_tu = int(terrain_profile.get("tick_interval_tu", terrain_profile.get(&"tick_interval_tu", 0)))
	effect.stack_behavior = &"refresh"
	effect.effect_target_team_filter = &"any"
	effect.params = {
		"lifetime_policy": terrain_profile.get("lifetime_policy", terrain_profile.get(&"lifetime_policy", &"timed")),
		"move_cost_delta": int(terrain_profile.get("move_cost_delta", terrain_profile.get(&"move_cost_delta", 0))),
		"move_cost_stack_key": terrain_profile.get("move_cost_stack_key", terrain_profile.get(&"move_cost_stack_key", &"")),
		"move_cost_stack_mode": terrain_profile.get("move_cost_stack_mode", terrain_profile.get(&"move_cost_stack_mode", &"")),
		"render_overlay_id": String(terrain_profile.get("render_overlay_id", terrain_profile.get(&"render_overlay_id", ""))),
		"overlay_priority": int(terrain_profile.get("overlay_priority", terrain_profile.get(&"overlay_priority", 0))),
		"display_name": _terrain_profile_display_name(terrain_profile_id),
	}
	var accuracy_spec = terrain_profile.get("accuracy_modifier_spec", terrain_profile.get(&"accuracy_modifier_spec", null))
	if accuracy_spec is Dictionary:
		effect.params["accuracy_modifier_spec"] = (accuracy_spec as Dictionary).duplicate(true)
	return effect


func _apply_concussed_status(plan: MeteorSwarmTargetPlan, target_unit: BattleUnitState) -> Dictionary:
	var effect := CombatEffectDef.new()
	effect.effect_type = &"apply_status"
	effect.status_id = plan.profile.concussed_status_id
	effect.power = 1
	effect.duration_tu = 60
	effect.params = {
		"duration_tu": 60,
		"attack_roll_penalty": 2,
	}
	return _runtime.get_damage_resolver().resolve_effects(plan.source_unit, target_unit, [effect], {
		"skill_id": plan.skill_id,
		"meteor_component_id": STATUS_METEOR_CONCUSSED,
	})


func _build_report_entry(
	plan: MeteorSwarmTargetPlan,
	result: MeteorSwarmCommitResult,
	component_totals: Dictionary
) -> Dictionary:
	var component_breakdown: Array[Dictionary] = []
	for component_key in ProgressionDataUtils.sorted_string_keys(component_totals):
		var entry := (component_totals.get(StringName(component_key), component_totals.get(component_key, {})) as Dictionary).duplicate(true)
		component_breakdown.append(entry)
	var target_summaries: Array[Dictionary] = []
	for target_outcome in result.target_outcomes:
		if target_outcome != null:
			target_summaries.append(target_outcome.to_summary_dict())
	var entry := {
		"entry_type": "meteor_swarm_impact_summary",
		"skill_id": String(plan.skill_id),
		"source_unit_id": String(plan.source_unit_id),
		"anchor_coord": plan.final_anchor_coord,
		"nominal_anchor_coord": plan.nominal_anchor_coord,
		"nominal_plan_signature": plan.nominal_plan_signature,
		"final_plan_signature": plan.final_plan_signature,
		"target_count": result.target_outcomes.size(),
		"terrain_effect_count": result.terrain_effects.size(),
		"total_damage": result.total_damage,
		"defeated_count": result.defeated_unit_ids.size(),
		"component_breakdown": component_breakdown,
		"target_summaries": target_summaries,
		"terrain_summary": _build_terrain_summary(plan),
	}
	var summary_lines := _report_formatter.format_meteor_swarm_summary(entry)
	if not summary_lines.is_empty():
		entry["text"] = summary_lines[0]
	return entry


func _build_component_report_fact(
	component: MeteorSwarmImpactComponent,
	damage_result: Dictionary,
	distance_from_anchor: int
) -> Dictionary:
	return {
		"component_id": String(component.component_id),
		"role_label": String(component.role_label),
		"damage_tag": String(component.damage_tag),
		"distance_from_anchor": distance_from_anchor,
		"damage": int(damage_result.get("damage", 0)),
		"healing": int(damage_result.get("healing", 0)),
		"damage_events": (damage_result.get("damage_events", []) as Array).duplicate(true) if damage_result.get("damage_events", []) is Array else [],
	}


func _add_component_total(component_totals: Dictionary, component_fact: Dictionary) -> void:
	var component_id := ProgressionDataUtils.to_string_name(component_fact.get("component_id", ""))
	if component_id == &"":
		return
	var existing: Dictionary = component_totals.get(component_id, {
		"component_id": String(component_id),
		"role_label": String(component_fact.get("role_label", "")),
		"damage_tag": String(component_fact.get("damage_tag", "")),
		"damage": 0,
		"healing": 0,
	})
	existing["damage"] = int(existing.get("damage", 0)) + int(component_fact.get("damage", 0))
	existing["healing"] = int(existing.get("healing", 0)) + int(component_fact.get("healing", 0))
	component_totals[component_id] = existing


func _tag_damage_events(damage_result: Dictionary, component: MeteorSwarmImpactComponent, distance_from_anchor: int) -> void:
	var damage_events = damage_result.get("damage_events", [])
	if damage_events is not Array:
		return
	for event_variant in damage_events:
		if event_variant is not Dictionary:
			continue
		var event := event_variant as Dictionary
		event["meteor_component_id"] = String(component.component_id)
		event["role_label"] = String(component.role_label)
		event["distance_from_anchor"] = distance_from_anchor


func _build_terrain_summary(plan: MeteorSwarmTargetPlan) -> Dictionary:
	var crater_count := 0
	var rubble_count := 0
	var dust_count := 0
	var terrain_effect_count := 0
	if plan == null or plan.profile == null:
		return {}
	for coord in plan.affected_coords:
		var ring := plan.get_ring_for_coord(coord)
		for terrain_profile in plan.profile.get_terrain_profiles_for_ring(ring):
			terrain_effect_count += 1
			var profile_id := String(terrain_profile.get("terrain_profile_id", terrain_profile.get(&"terrain_profile_id", "")))
			if profile_id.contains("crater"):
				crater_count += 1
			if profile_id.contains("rubble"):
				rubble_count += 1
			if profile_id.contains("dust"):
				dust_count += 1
	return {
		"coverage_shape_id": String(plan.coverage_shape_id),
		"radius": plan.radius,
		"affected_coord_count": plan.affected_coords.size(),
		"terrain_effect_count": terrain_effect_count,
		"crater_count": crater_count,
		"rubble_count": rubble_count,
		"dust_count": dust_count,
	}


func _build_friendly_fire_numeric_summary(plan: MeteorSwarmTargetPlan) -> Array[Dictionary]:
	var summaries: Array[Dictionary] = []
	if plan == null or _runtime == null or _runtime.get_state() == null:
		return summaries
	for target_unit_id in plan.target_unit_ids:
		var target_unit := _runtime.get_state().units.get(target_unit_id) as BattleUnitState
		if target_unit == null or plan.source_unit == null:
			continue
		if target_unit.faction_id != plan.source_unit.faction_id:
			continue
		summaries.append(_build_friendly_fire_summary_for_unit(plan, target_unit))
	return summaries


func _build_target_numeric_summary(plan: MeteorSwarmTargetPlan) -> Array[Dictionary]:
	var summaries: Array[Dictionary] = []
	if plan == null or _runtime == null or _runtime.get_state() == null:
		return summaries
	for target_unit_id in plan.target_unit_ids:
		var target_unit := _runtime.get_state().units.get(target_unit_id) as BattleUnitState
		if target_unit == null:
			continue
		summaries.append(_build_friendly_fire_summary_for_unit(plan, target_unit))
	return summaries


func _build_friendly_fire_summary_for_unit(plan: MeteorSwarmTargetPlan, target_unit: BattleUnitState) -> Dictionary:
	var distance := plan.get_distance_for_unit(target_unit.unit_id)
	var covers_center := _unit_covers_coord(target_unit, plan.final_anchor_coord)
	var component_breakdown: Array[Dictionary] = []
	var expected_damage := 0
	var worst_case_damage := 0
	var expected_source_preview := plan.source_unit.clone() if plan.source_unit != null else null
	var worst_source_preview := plan.source_unit.clone() if plan.source_unit != null else null
	var expected_target_preview := target_unit.clone()
	var worst_target_preview := target_unit.clone()
	var resistance_tiers: Dictionary = {}
	var guard_block_estimate := 0
	for component_variant in plan.profile.get_impact_components():
		var component := component_variant as MeteorSwarmImpactComponent
		if component == null or not component.applies_to_distance(distance, covers_center):
			continue
		var effect_def := _build_damage_effect_def(component, distance)
		var expected_preview := _build_component_damage_preview(
			plan,
			expected_source_preview,
			expected_target_preview,
			effect_def,
			BattleDamageResolver.DAMAGE_PREVIEW_ROLL_MODE_AVERAGE,
			BattleDamageResolver.DAMAGE_PREVIEW_SAVE_MODE_EXPECTED
		)
		var worst_preview := _build_component_damage_preview(
			plan,
			worst_source_preview,
			worst_target_preview,
			effect_def,
			BattleDamageResolver.DAMAGE_PREVIEW_ROLL_MODE_MAXIMUM,
			BattleDamageResolver.DAMAGE_PREVIEW_SAVE_MODE_WORST
		)
		var expected_outcome := expected_preview.get("damage_outcome", {}) as Dictionary
		var worst_outcome := worst_preview.get("damage_outcome", {}) as Dictionary
		var resistance_tier := ProgressionDataUtils.to_string_name(expected_outcome.get("mitigation_tier", MITIGATION_TIER_NORMAL))
		resistance_tiers[String(component.damage_tag)] = String(resistance_tier)
		guard_block_estimate = maxi(guard_block_estimate, int(expected_outcome.get("guard_block", 0)))
		var pre_save_expected_damage := int(expected_preview.get("pre_save_damage", 0))
		var pre_save_worst_damage := int(worst_preview.get("pre_save_damage", 0))
		var expected_component_damage := int(expected_preview.get("post_save_damage", pre_save_expected_damage))
		var worst_component_damage := int(worst_preview.get("post_save_damage", pre_save_worst_damage))
		var expected_after_shield := int(expected_preview.get("hp_damage", expected_component_damage))
		var worst_after_shield := int(worst_preview.get("hp_damage", worst_component_damage))
		expected_damage += expected_after_shield
		worst_case_damage += worst_after_shield
		var next_expected_source := expected_preview.get("source_preview_after", null) as BattleUnitState
		var next_expected_target := expected_preview.get("target_preview_after", null) as BattleUnitState
		var next_worst_source := worst_preview.get("source_preview_after", null) as BattleUnitState
		var next_worst_target := worst_preview.get("target_preview_after", null) as BattleUnitState
		if next_expected_source != null:
			expected_source_preview = next_expected_source
		if next_expected_target != null:
			expected_target_preview = next_expected_target
		if next_worst_source != null:
			worst_source_preview = next_worst_source
		if next_worst_target != null:
			worst_target_preview = next_worst_target
		component_breakdown.append({
			"component_id": String(component.component_id),
			"role_label": String(component.role_label),
			"damage_tag": String(component.damage_tag),
			"expected_damage": expected_after_shield,
			"worst_case_damage": worst_after_shield,
			"post_save_expected_damage": expected_component_damage,
			"post_save_worst_case_damage": worst_component_damage,
			"pre_save_expected_damage": pre_save_expected_damage,
			"pre_save_worst_case_damage": pre_save_worst_damage,
			"resistance_tier": String(resistance_tier),
			"save_profile_id": String(component.save_profile_id),
			"save_estimate": (expected_preview.get("save_estimate", {}) as Dictionary).duplicate(true),
			"worst_save_estimate": (worst_preview.get("save_estimate", {}) as Dictionary).duplicate(true),
			"mitigation_sources": expected_outcome.get("mitigation_sources", []),
			"fixed_mitigation_sources": expected_outcome.get("fixed_mitigation_sources", []),
			"shield_absorbed_estimate": int(expected_preview.get("shield_absorbed", 0)),
			"shield_absorbed_worst": int(worst_preview.get("shield_absorbed", 0)),
		})
	var status_effect_ids: Array[StringName] = []
	var ap_penalty := 0
	if distance <= 1:
		status_effect_ids.append(plan.profile.concussed_status_id)
		ap_penalty = 1
	var max_hp := _get_unit_max_hp(target_unit)
	var current_hp := maxi(int(target_unit.current_hp), 1)
	var expected_hp_percent := int(round(float(expected_damage) * 100.0 / float(maxi(max_hp, 1))))
	var worst_hp_percent := int(round(float(worst_case_damage) * 100.0 / float(maxi(max_hp, 1))))
	var hard_reject := worst_case_damage >= current_hp \
		or expected_hp_percent >= int(plan.profile.friendly_fire_hard_expected_hp_percent) \
		or worst_hp_percent >= int(plan.profile.friendly_fire_hard_worst_case_hp_percent)
	var is_ally := plan.source_unit != null and target_unit.faction_id == plan.source_unit.faction_id
	return {
		"candidate_anchor_coord": plan.final_anchor_coord,
		"target_unit_id": String(target_unit.unit_id),
		"ally_unit_id": String(target_unit.unit_id),
		"target_faction_id": String(target_unit.faction_id),
		"is_ally": is_ally,
		"distance_from_anchor": distance,
		"component_expected_damage": expected_damage,
		"component_worst_case_damage": worst_case_damage,
		"component_breakdown": component_breakdown,
		"lethal_probability_percent": 100 if worst_case_damage >= current_hp else 0,
		"save_profile_ids": _collect_component_save_profile_ids(component_breakdown),
		"resistance_tiers_by_damage_tag": resistance_tiers,
		"shield_hp": int(target_unit.current_shield_hp),
		"guard_block_estimate": guard_block_estimate,
		"status_effect_ids": status_effect_ids.duplicate(),
		"ap_penalty": ap_penalty,
		"hostile_terrain_consequence": _build_hostile_terrain_consequence(plan, distance),
		"expected_damage_hp_percent": expected_hp_percent,
		"worst_case_damage_hp_percent": worst_hp_percent,
		"hard_reject": hard_reject,
		"soft_penalty": not hard_reject and expected_hp_percent > int(plan.profile.friendly_fire_soft_expected_hp_percent),
	}


func _build_future_attack_roll_modifier_breakdown(plan: MeteorSwarmTargetPlan) -> Array[Dictionary]:
	var breakdown: Array[Dictionary] = []
	if plan == null or plan.profile == null:
		return breakdown
	for terrain_profile in plan.profile.terrain_profiles:
		var accuracy_spec = terrain_profile.get("accuracy_modifier_spec", terrain_profile.get(&"accuracy_modifier_spec", null)) if terrain_profile is Dictionary else null
		if accuracy_spec is not Dictionary:
			continue
		var spec := (accuracy_spec as Dictionary).duplicate(true)
		spec["source_instance_id"] = String(terrain_profile.get("terrain_profile_id", terrain_profile.get(&"terrain_profile_id", "")))
		spec["effective_modifier_delta"] = int(spec.get("modifier_delta", 0))
		breakdown.append(spec)
	return breakdown


func _build_component_preview(plan: MeteorSwarmTargetPlan) -> Array[Dictionary]:
	var preview: Array[Dictionary] = []
	if plan == null or plan.profile == null:
		return preview
	for component_variant in plan.profile.get_impact_components():
		var component := component_variant as MeteorSwarmImpactComponent
		if component == null:
			continue
		preview.append(component.to_component_fact(0))
	return preview


func _count_expected_terrain_effects(plan: MeteorSwarmTargetPlan) -> int:
	return int(_build_terrain_summary(plan).get("terrain_effect_count", 0))


func _resolve_friendly_fire_risk_percent(summaries: Array[Dictionary]) -> int:
	if summaries.is_empty():
		return 0
	var hard_count := 0
	for summary in summaries:
		if bool(summary.get("hard_reject", false)):
			hard_count += 1
	return int(round(float(hard_count) * 100.0 / float(summaries.size())))


func _build_hostile_terrain_consequence(plan: MeteorSwarmTargetPlan, distance_from_anchor: int) -> Dictionary:
	var consequence := {
		"move_cost_delta": 0,
		"creates_dust": false,
		"creates_crater": false,
		"creates_rubble": false,
	}
	for terrain_profile in plan.profile.get_terrain_profiles_for_ring(distance_from_anchor):
		consequence["move_cost_delta"] = maxi(int(consequence.get("move_cost_delta", 0)), int(terrain_profile.get("move_cost_delta", 0)))
		var profile_id := String(terrain_profile.get("terrain_profile_id", terrain_profile.get(&"terrain_profile_id", "")))
		if profile_id.contains("dust"):
			consequence["creates_dust"] = true
		if profile_id.contains("crater"):
			consequence["creates_crater"] = true
		if profile_id.contains("rubble"):
			consequence["creates_rubble"] = true
	return consequence


func _collect_component_save_profile_ids(component_breakdown: Array[Dictionary]) -> Array[String]:
	var ids: Array[String] = []
	for component in component_breakdown:
		var save_profile_id := String(component.get("save_profile_id", ""))
		if not save_profile_id.is_empty() and not ids.has(save_profile_id):
			ids.append(save_profile_id)
	return ids


func _apply_save_profile_to_damage_effect(effect: CombatEffectDef, component: MeteorSwarmImpactComponent) -> void:
	if effect == null or component == null:
		return
	match component.save_profile_id:
		SAVE_PROFILE_METEOR_DEX_HALF:
			effect.save_dc_mode = BattleSaveResolver.SAVE_DC_MODE_CASTER_SPELL
			effect.save_dc_source_ability = &"intelligence"
			effect.save_ability = &"agility"
			effect.save_partial_on_success = true
			effect.save_tag = BattleSaveResolver.SAVE_TAG_MAGIC
		_:
			pass


func _build_component_damage_preview(
	plan: MeteorSwarmTargetPlan,
	source_preview: BattleUnitState,
	target_preview: BattleUnitState,
	effect_def: CombatEffectDef,
	roll_mode: StringName,
	save_mode: StringName
) -> Dictionary:
	if _runtime == null or source_preview == null or target_preview == null or effect_def == null:
		return {}
	var damage_resolver = _runtime.get_damage_resolver() if _runtime.has_method("get_damage_resolver") else null
	if damage_resolver == null or not damage_resolver.has_method("preview_damage_effect"):
		return {}
	return damage_resolver.preview_damage_effect(
		source_preview,
		target_preview,
		effect_def,
		{
			"battle_state": _runtime.get_state(),
			"skill_id": plan.skill_id if plan != null else DEFAULT_SKILL_ID,
		},
		roll_mode,
		save_mode
	)


func _populate_unit_distances(plan: MeteorSwarmTargetPlan) -> void:
	if _runtime == null or _runtime.get_state() == null:
		return
	var grid_service = _runtime.get_grid_service()
	for target_unit_id in plan.target_unit_ids:
		var target_unit := _runtime.get_state().units.get(target_unit_id) as BattleUnitState
		if target_unit == null:
			continue
		target_unit.refresh_footprint()
		var best_distance := 999999
		var best_coord := plan.get_primary_coord_for_unit(target_unit_id)
		var occupied_coords := target_unit.occupied_coords
		if occupied_coords.is_empty():
			occupied_coords = grid_service.get_unit_target_coords(target_unit, target_unit.coord)
		for coord in occupied_coords:
			if not plan.ring_by_coord.has(coord):
				continue
			var distance := plan.get_ring_for_coord(coord)
			if distance < best_distance:
				best_distance = distance
				best_coord = coord
		plan.unit_distance_by_id[target_unit_id] = best_distance
		plan.unit_primary_coord_by_id[target_unit_id] = best_coord


func _build_plan_signature_for_anchor(plan: MeteorSwarmTargetPlan, anchor_coord: Vector2i) -> String:
	if anchor_coord == plan.final_anchor_coord:
		return _build_plan_signature(plan, anchor_coord)
	var affected_count := 0
	if _runtime != null and _runtime.get_state() != null:
		var state = _runtime.get_state()
		var grid_service = _runtime.get_grid_service()
		for dy in range(-plan.radius, plan.radius + 1):
			for dx in range(-plan.radius, plan.radius + 1):
				var coord := anchor_coord + Vector2i(dx, dy)
				if grid_service.is_inside(state, coord):
					affected_count += 1
	return "%s:%s:r%d:%d,%d:%d" % [
		String(plan.skill_id),
		String(plan.coverage_shape_id),
		plan.radius,
		anchor_coord.x,
		anchor_coord.y,
		affected_count,
	]


func _build_plan_signature(plan: MeteorSwarmTargetPlan, anchor_coord: Vector2i) -> String:
	var unit_parts: Array[String] = []
	for unit_id in plan.target_unit_ids:
		unit_parts.append(String(unit_id))
	return "%s:%s:r%d:%d,%d:%d:%s" % [
		String(plan.skill_id),
		String(plan.coverage_shape_id),
		plan.radius,
		anchor_coord.x,
		anchor_coord.y,
		plan.affected_coords.size(),
		",".join(unit_parts),
	]


func _extract_target_coords(validation: Dictionary) -> Array[Vector2i]:
	var target_coords: Array[Vector2i] = []
	for coord_variant in validation.get("target_coords", []):
		if coord_variant is Vector2i:
			target_coords.append(coord_variant)
	return target_coords


func _resolve_profile() -> MeteorSwarmProfile:
	if _runtime == null:
		return null
	var snapshot: Dictionary = _runtime.get_special_profile_registry_snapshot()
	var profiles = snapshot.get("profiles", {})
	if profiles is not Dictionary:
		return null
	var meteor_profile_snapshot = (profiles as Dictionary).get("meteor_swarm", {})
	if meteor_profile_snapshot is not Dictionary:
		return null
	return (meteor_profile_snapshot as Dictionary).get("profile_resource", null) as MeteorSwarmProfile


func _resolve_ground_cast_variant(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef
):
	if _runtime == null or skill_def == null:
		return null
	if _runtime.has_method("_resolve_ground_cast_variant"):
		var cast_variant = _runtime._resolve_ground_cast_variant(skill_def, active_unit, command)
		if cast_variant != null:
			return cast_variant
	if _runtime.has_method("_build_implicit_ground_cast_variant") \
			and skill_def.combat_profile != null \
			and skill_def.combat_profile.target_mode == &"ground":
		return _runtime._build_implicit_ground_cast_variant(skill_def)
	return null


func _unit_covers_coord(unit_state: BattleUnitState, coord: Vector2i) -> bool:
	if unit_state == null:
		return false
	unit_state.refresh_footprint()
	for occupied_coord in unit_state.occupied_coords:
		if occupied_coord == coord:
			return true
	return unit_state.coord == coord


func _get_unit_max_hp(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 1
	if unit_state.attribute_snapshot != null:
		var max_hp := int(unit_state.attribute_snapshot.get_value(&"hp_max"))
		if max_hp > 0:
			return max_hp
	return maxi(int(unit_state.current_hp), 1)


func _terrain_profile_display_name(terrain_profile_id: StringName) -> String:
	match terrain_profile_id:
		&"meteor_swarm_crater_core":
			return "陨坑"
		&"meteor_swarm_crater_rim":
			return "陨坑边缘"
		&"meteor_swarm_rubble", &"meteor_swarm_edge_rubble":
			return "碎石"
		&"meteor_swarm_dust":
			return "尘土"
		_:
			return String(terrain_profile_id)


func _sort_coord_ascending(left: Vector2i, right: Vector2i) -> bool:
	return left.y < right.y or (left.y == right.y and left.x < right.x)
