class_name BattleSkillExecutionOrchestrator
extends RefCounted

const BATTLE_DAMAGE_PREVIEW_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_preview_range_service.gd")

const BATTLE_REPORT_FORMATTER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_report_formatter.gd")

const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")

const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")

const BattlePreview = preload("res://scripts/systems/battle/core/battle_preview.gd")

const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")

const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

const BODY_SIZE_RULES_SCRIPT = preload("res://scripts/systems/progression/body_size_rules.gd")

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")

const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")

const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const BodySizeRules = BODY_SIZE_RULES_SCRIPT

const BODY_SIZE_CATEGORY_OVERRIDE_EFFECT_TYPE: StringName = &"body_size_category_override"

const CHAIN_DAMAGE_EFFECT_TYPE: StringName = &"chain_damage"

const EQUIPMENT_DURABILITY_DAMAGE_EFFECT_TYPE: StringName = &"equipment_durability_damage"

const STATUS_GUARDING: StringName = &"guarding"

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null

func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null


func _append_result_report_entry(batch: BattleEventBatch, result: Dictionary) -> void:
	if _runtime == null:
		return
	_runtime._append_result_report_entry(batch, result)

func _append_report_entry_to_batch(batch: BattleEventBatch, report_entry: Dictionary) -> void:
	if _runtime == null:
		return
	_runtime._append_report_entry_to_batch(batch, report_entry)

func mark_applied_statuses_for_turn_timing(target_unit: BattleUnitState, status_effect_ids: Variant) -> void:
	if _runtime == null:
		return
	_runtime.mark_applied_statuses_for_turn_timing(target_unit, status_effect_ids)

func append_result_source_status_effects(batch: BattleEventBatch, source_unit: BattleUnitState, result: Dictionary) -> void:
	if _runtime == null:
		return
	_runtime.append_result_source_status_effects(batch, source_unit, result)

func _record_action_issued(unit_state: BattleUnitState, command_type: StringName, ap_cost: int = 0) -> void:
	if _runtime == null:
		return
	_runtime._record_action_issued(unit_state, command_type, ap_cost)

func _record_skill_attempt(unit_state: BattleUnitState, skill_id: StringName) -> void:
	if _runtime == null:
		return
	_runtime._record_skill_attempt(unit_state, skill_id)

func _record_effect_metrics(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	damage: int,
	healing: int,
	kill_count: int
) -> void:
	if _runtime == null:
		return
	_runtime._record_effect_metrics(source_unit, target_unit, damage, healing, kill_count)

func _record_unit_defeated(unit_state: BattleUnitState) -> void:
	if _runtime == null:
		return
	_runtime._record_unit_defeated(unit_state)

func _apply_on_kill_gain_resources_effects(
	source_unit: BattleUnitState,
	defeated_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch
) -> void:
	if _runtime == null:
		return
	_runtime._apply_on_kill_gain_resources_effects(source_unit, defeated_unit, skill_def, effect_defs, batch)

func _apply_unit_skill_special_effects(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch,
	forced_move_context: Dictionary = {}
) -> Dictionary:
	if _runtime == null:
		return {}
	return _runtime._apply_unit_skill_special_effects(active_unit, target_unit, skill_def, cast_variant, effect_defs, batch, forced_move_context)

func _is_doom_shift_skill(skill_id: StringName) -> bool:
	if _runtime == null:
		return false
	return _runtime._is_doom_shift_skill(skill_id)

func _is_black_crown_seal_skill(skill_id: StringName) -> bool:
	if _runtime == null:
		return false
	return _runtime._is_black_crown_seal_skill(skill_id)

func _is_crown_break_target_eligible(active_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	if _runtime == null:
		return false
	return _runtime._is_crown_break_target_eligible(active_unit, target_unit)

func _is_crown_break_skill(skill_id: StringName) -> bool:
	if _runtime == null:
		return false
	return _runtime._is_crown_break_skill(skill_id)

func _is_doom_sentence_target_eligible(active_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	if _runtime == null:
		return false
	return _runtime._is_doom_sentence_target_eligible(active_unit, target_unit)

func _is_black_crown_seal_target_eligible(active_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	if _runtime == null:
		return false
	return _runtime._is_black_crown_seal_target_eligible(active_unit, target_unit)

func _is_doom_sentence_skill(skill_id: StringName) -> bool:
	if _runtime == null:
		return false
	return _runtime._is_doom_sentence_skill(skill_id)

func _record_vajra_body_mastery_from_incoming_damage(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	result: Dictionary,
	batch: BattleEventBatch = null
) -> void:
	if _runtime == null:
		return
	_runtime._record_vajra_body_mastery_from_incoming_damage(source_unit, target_unit, skill_def, result, batch)

func _resolve_ground_spell_control_after_cost(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	spent_mp: int,
	batch: BattleEventBatch
) -> Dictionary:
	if _runtime == null:
		return {}
	return _runtime._resolve_ground_spell_control_after_cost(active_unit, skill_def, spent_mp, batch)

func _resolve_unit_spell_control_after_cost(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	batch: BattleEventBatch
) -> Dictionary:
	if _runtime == null:
		return {}
	return _runtime._resolve_unit_spell_control_after_cost(active_unit, skill_def, batch)

func _apply_ground_precast_special_effects(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	target_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> bool:
	if _runtime == null:
		return false
	return _runtime._apply_ground_precast_special_effects(active_unit, skill_def, cast_variant, target_coords, batch)

func _build_ground_effect_coords(
	skill_def: SkillDef,
	target_coords: Array,
	source_coord: Vector2i = Vector2i(-1, -1),
	active_unit: BattleUnitState = null,
	cast_variant = null
) -> Array[Vector2i]:
	if _runtime == null:
		return []
	return _runtime._build_ground_effect_coords(skill_def, target_coords, source_coord, active_unit, cast_variant)

func _collect_ground_unit_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	if _runtime == null:
		return []
	return _runtime._collect_ground_unit_effect_defs(skill_def, cast_variant, active_unit)

func _collect_ground_terrain_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	if _runtime == null:
		return []
	return _runtime._collect_ground_terrain_effect_defs(skill_def, cast_variant, active_unit)

func _collect_ground_preview_unit_ids(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i]
) -> Array[StringName]:
	if _runtime == null:
		return []
	return _runtime._collect_ground_preview_unit_ids(source_unit, skill_def, effect_defs, effect_coords)

func _apply_ground_unit_effects(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i],
	batch: BattleEventBatch,
	target_coords: Array[Vector2i] = []
) -> Dictionary:
	if _runtime == null:
		return {}
	return _runtime._apply_ground_unit_effects(source_unit, skill_def, effect_defs, effect_coords, batch, target_coords)

func _apply_ground_terrain_effects(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> Dictionary:
	if _runtime == null:
		return {}
	return _runtime._apply_ground_terrain_effects(source_unit, skill_def, effect_defs, effect_coords, batch)

func _apply_unit_shield_effects(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	shield_roll_context: Dictionary = {}
) -> Dictionary:
	if _runtime == null:
		return {}
	return _runtime._apply_unit_shield_effects(source_unit, target_unit, skill_def, effect_defs, shield_roll_context)

func _grant_skill_mastery_if_needed(active_unit: BattleUnitState, skill_def: SkillDef, batch: BattleEventBatch) -> void:
	if _runtime == null:
		return
	_runtime._grant_skill_mastery_if_needed(active_unit, skill_def, batch)

func _apply_skill_mastery_grant(unit_state: BattleUnitState, grant: Dictionary, batch: BattleEventBatch) -> void:
	if _runtime == null:
		return
	_runtime._apply_skill_mastery_grant(unit_state, grant, batch)

func _flush_last_stand_mastery_records(batch: BattleEventBatch) -> void:
	if _runtime == null:
		return
	_runtime._flush_last_stand_mastery_records(batch)

func _validate_ground_skill_command(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	command: BattleCommand
) -> Dictionary:
	if _runtime == null:
		return {}
	return _runtime._validate_ground_skill_command(active_unit, skill_def, cast_variant, command)

func _get_ground_special_effect_validation_message(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	target_coords: Array[Vector2i]
) -> String:
	if _runtime == null:
		return ""
	return _runtime._get_ground_special_effect_validation_message(active_unit, skill_def, cast_variant, target_coords)

func _append_changed_coord(batch: BattleEventBatch, coord: Vector2i) -> void:
	if _runtime == null:
		return
	_runtime._append_changed_coord(batch, coord)

func _append_changed_unit_id(batch: BattleEventBatch, unit_id: StringName) -> void:
	if _runtime == null:
		return
	_runtime._append_changed_unit_id(batch, unit_id)

func _append_changed_unit_coords(batch: BattleEventBatch, unit_state: BattleUnitState) -> void:
	if _runtime == null:
		return
	_runtime._append_changed_unit_coords(batch, unit_state)

func _collect_defeated_unit_loot(unit_state: BattleUnitState, killer_unit: BattleUnitState = null) -> void:
	if _runtime == null:
		return
	_runtime._collect_defeated_unit_loot(unit_state, killer_unit)

func _clear_defeated_unit(unit_state: BattleUnitState, batch: BattleEventBatch = null) -> void:
	if _runtime == null:
		return
	_runtime._clear_defeated_unit(unit_state, batch)

func _sort_coords(target_coords: Variant) -> Array[Vector2i]:
	if _runtime == null:
		return []
	return _runtime._sort_coords(target_coords)

func _get_skill_command_block_reason(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef
) -> String:
	if _runtime == null:
		return ""
	return _runtime._get_skill_command_block_reason(active_unit, skill_def, cast_variant)

func _consume_skill_costs(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null,
	batch: BattleEventBatch = null
) -> bool:
	if _runtime == null:
		return false
	return _runtime._consume_skill_costs(active_unit, skill_def, cast_variant, batch)

func _get_effective_skill_costs(active_unit: BattleUnitState, skill_def: SkillDef) -> Dictionary:
	if _runtime == null:
		return {}
	return _runtime._get_effective_skill_costs(active_unit, skill_def)

func _get_effective_skill_range(active_unit: BattleUnitState, skill_def: SkillDef) -> int:
	if _runtime == null:
		return 0
	return _runtime._get_effective_skill_range(active_unit, skill_def)


func _handle_skill_command(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> void:
	var skill_def = _runtime._skill_defs.get(command.skill_id) as SkillDef
	if skill_def == null or skill_def.combat_profile == null:
		return
	var unit_cast_variant = _resolve_unit_cast_variant(skill_def, active_unit, command)
	var ground_cast_variant = _resolve_ground_cast_variant(skill_def, active_unit, command)
	var command_cast_variant = ground_cast_variant if ground_cast_variant != null else unit_cast_variant
	var unit_execution_cast_variant = unit_cast_variant if unit_cast_variant != null else ground_cast_variant
	var block_reason = _get_skill_command_block_reason(active_unit, skill_def, command_cast_variant)
	if not block_reason.is_empty():
		batch.log_lines.append(block_reason)
		return

	_record_skill_attempt(active_unit, command.skill_id)
	_runtime._skill_mastery_service.clear()
	var applied = false
	if _should_route_skill_command_to_unit_targeting(skill_def, command):
		applied = _handle_unit_skill_command(active_unit, command, skill_def, unit_execution_cast_variant, batch)
	else:
		if ground_cast_variant != null:
			applied = _handle_ground_skill_command(active_unit, command, skill_def, ground_cast_variant, batch)
		else:
			applied = _handle_unit_skill_command(active_unit, command, skill_def, null, batch)
	if applied:
		_grant_skill_mastery_if_needed(active_unit, skill_def, batch)
	_runtime._skill_mastery_service.clear()

func _preview_skill_command(active_unit: BattleUnitState, command: BattleCommand, preview: BattlePreview) -> void:
	var skill_def = _runtime._skill_defs.get(command.skill_id) as SkillDef
	if skill_def == null or skill_def.combat_profile == null:
		preview.log_lines.append("技能或目标无效。")
		return
	var unit_cast_variant = _resolve_unit_cast_variant(skill_def, active_unit, command)
	var ground_cast_variant = _resolve_ground_cast_variant(skill_def, active_unit, command)
	var unit_execution_cast_variant = unit_cast_variant if unit_cast_variant != null else ground_cast_variant

	if _should_route_skill_command_to_unit_targeting(skill_def, command):
		_preview_unit_skill_command(active_unit, command, skill_def, unit_execution_cast_variant, preview)
		return

	if ground_cast_variant != null:
		_preview_ground_skill_command(active_unit, command, skill_def, ground_cast_variant, preview)
		return

	_preview_unit_skill_command(active_unit, command, skill_def, null, preview)

func _preview_unit_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	preview: BattlePreview
) -> void:
	var block_reason = _get_skill_command_block_reason(active_unit, skill_def, cast_variant)
	if not block_reason.is_empty():
		preview.log_lines.append(block_reason)
		return

	var validation = _validate_unit_skill_targets(active_unit, command, skill_def, cast_variant)
	preview.allowed = bool(validation.get("allowed", false))
	preview.target_unit_ids.clear()
	for target_unit_id_variant in validation.get("target_unit_ids", []):
		preview.target_unit_ids.append(ProgressionDataUtils.to_string_name(target_unit_id_variant))
	preview.target_coords.clear()
	for preview_coord_variant in validation.get("preview_coords", []):
		if preview_coord_variant is Vector2i:
			preview.target_coords.append(preview_coord_variant)
	if preview.allowed:
		var target_units = validation.get("target_units", []) as Array
		preview.hit_preview = _build_unit_skill_hit_preview(active_unit, target_units, skill_def, cast_variant)
		preview.damage_preview = _build_unit_skill_damage_preview(active_unit, skill_def, cast_variant)
		var skill_label = _format_skill_variant_label(skill_def, cast_variant)
		if target_units.size() == 1:
			var target_unit = target_units[0] as BattleUnitState
			if target_unit != null:
				preview.log_lines.append("%s 可对 %s 使用 %s。" % [active_unit.display_name, target_unit.display_name, skill_label])
				if not preview.hit_preview.is_empty():
					preview.log_lines.append(String(preview.hit_preview.get("summary_text", "")))
				_append_damage_preview_line(preview)
				return
		preview.log_lines.append("%s 可对 %d 个单位使用 %s。" % [
			active_unit.display_name,
			preview.target_unit_ids.size(),
			skill_label,
		])
		if not preview.hit_preview.is_empty():
			preview.log_lines.append(String(preview.hit_preview.get("summary_text", "")))
		_append_damage_preview_line(preview)
		return
	preview.log_lines.append(String(validation.get("message", "技能或目标无效。")))

func _preview_ground_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	preview: BattlePreview
) -> void:
	var block_reason = _get_skill_command_block_reason(active_unit, skill_def, cast_variant)
	if not block_reason.is_empty():
		preview.log_lines.append(block_reason)
		return
	var validation = _validate_ground_skill_command(active_unit, skill_def, cast_variant, command)
	preview.target_coords.clear()
	var preview_coords: Array[Vector2i] = validation.get(
		"preview_coords",
		_build_ground_effect_coords(skill_def, validation.get("target_coords", []), active_unit.coord if active_unit != null else Vector2i(-1, -1), active_unit, cast_variant)
	)
	preview.resolved_anchor_coord = validation.get("resolved_anchor_coord", Vector2i(-1, -1))
	if bool(validation.get("allowed", false)):
		var path_step_aoe_effect = _runtime._charge_resolver.get_charge_path_step_aoe_effect_def(cast_variant, skill_def, active_unit)
		if path_step_aoe_effect != null:
			preview_coords = _runtime._charge_resolver.build_charge_step_aoe_preview_coords(
				active_unit,
				validation.get("direction", Vector2i.ZERO),
				int(validation.get("distance", 0)),
				path_step_aoe_effect
			)
	for target_coord in preview_coords:
		preview.target_coords.append(target_coord)
	preview.target_unit_ids = _collect_ground_preview_unit_ids(
		active_unit,
		skill_def,
		_collect_ground_unit_effect_defs(skill_def, cast_variant, active_unit),
		preview.target_coords
	)
	if bool(validation.get("allowed", false)):
		var path_step_aoe_effect = _runtime._charge_resolver.get_charge_path_step_aoe_effect_def(cast_variant, skill_def, active_unit)
		if path_step_aoe_effect != null:
			var path_step_target_filter = _resolve_effect_target_filter(skill_def, path_step_aoe_effect)
			for target_unit in _collect_units_in_coords(preview.target_coords):
				if not _is_unit_valid_for_effect(active_unit, target_unit, path_step_target_filter):
					continue
				if preview.target_unit_ids.has(target_unit.unit_id):
					continue
				preview.target_unit_ids.append(target_unit.unit_id)
	preview.allowed = bool(validation.get("allowed", false))
	if preview.allowed:
		preview.log_lines.append("%s 可使用 %s，预计影响 %d 个地格、%d 个单位。" % [
			active_unit.display_name,
			_format_skill_variant_label(skill_def, cast_variant),
			preview.target_coords.size(),
			preview.target_unit_ids.size(),
		])
	else:
		preview.log_lines.append(String(validation.get("message", "地面技能目标无效。")))

func _build_unit_skill_hit_preview(
	active_unit: BattleUnitState,
	target_units: Array,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef
) -> Dictionary:
	if active_unit == null or skill_def == null or target_units.size() != 1:
		return {}
	var target_unit = target_units[0] as BattleUnitState
	if target_unit == null:
		return {}
	var effect_defs = _collect_unit_skill_effect_defs(skill_def, cast_variant, active_unit)
	var repeat_attack_effect = _runtime._repeat_attack_resolver.get_repeat_attack_effect_def(
		effect_defs
	)
	if repeat_attack_effect == null:
		if not _runtime._skill_resolution_rules.should_resolve_unit_skill_as_fate_attack(
			active_unit,
			target_unit,
			skill_def,
			effect_defs
		):
			return {}
		return _runtime._hit_resolver.build_skill_attack_preview(
			_runtime._state,
			active_unit,
			target_unit,
			skill_def,
			_runtime._skill_resolution_rules.is_force_hit_no_crit_skill(skill_def)
		)
	return _runtime._hit_resolver.build_repeat_attack_preview(_runtime._state, active_unit, target_unit, skill_def, repeat_attack_effect)

func _build_unit_skill_damage_preview(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef
) -> Dictionary:
	if active_unit == null or skill_def == null:
		return {}
	var effect_defs = _collect_unit_skill_effect_defs(skill_def, cast_variant, active_unit)
	return BATTLE_DAMAGE_PREVIEW_RANGE_SERVICE_SCRIPT.build_skill_damage_preview(active_unit, effect_defs)

func _append_damage_preview_line(preview: BattlePreview) -> void:
	if preview == null or preview.damage_preview.is_empty():
		return
	var damage_preview_text = String(preview.damage_preview.get("summary_text", ""))
	if damage_preview_text.is_empty():
		return
	preview.log_lines.append(damage_preview_text)

func summarize_damage_result(result: Dictionary) -> Dictionary:
	return _runtime._report_formatter.summarize_damage_result(result)

func build_damage_absorb_reason_text(summary: Dictionary) -> String:
	return _runtime._report_formatter.build_damage_absorb_reason_text(summary)

func append_damage_result_log_lines(
	batch: BattleEventBatch,
	subject_label: String,
	target_display_name: String,
	result: Dictionary
) -> void:
	_runtime._report_formatter.append_damage_result_log_lines(batch, subject_label, target_display_name, result)

func _build_unit_skill_resolution_preview_lines(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef
) -> Array[String]:
	var lines: Array[String] = []
	if active_unit == null or target_unit == null or skill_def == null:
		return lines
	var damage_preview = _build_unit_skill_damage_preview(active_unit, skill_def, cast_variant)
	var damage_preview_text = String(damage_preview.get("summary_text", ""))
	if not damage_preview_text.is_empty():
		lines.append(damage_preview_text)
	return lines

func _build_skill_log_subject_label(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> String:
	var actor_label = source_unit.display_name if source_unit != null and not source_unit.display_name.is_empty() else "未知单位"
	var skill_label = _format_skill_variant_label(skill_def, cast_variant)
	if skill_label.is_empty() and skill_def != null:
		skill_label = skill_def.display_name
	if skill_label.is_empty():
		skill_label = "技能"
	return "%s 使用 %s" % [actor_label, skill_label]

func _handle_unit_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	batch: BattleEventBatch
) -> bool:
	var validation = _validate_unit_skill_targets(active_unit, command, skill_def, cast_variant)
	if not bool(validation.get("allowed", false)):
		return false

	var target_units = validation.get("target_units", []) as Array
	var is_random_chain := StringName(skill_def.combat_profile.target_selection_mode) == &"random_chain"
	if target_units.is_empty() and not is_random_chain:
		return false

	if not _consume_skill_costs(active_unit, skill_def, cast_variant, batch):
		return false
	var costs = _get_effective_skill_costs(active_unit, skill_def)
	_record_action_issued(active_unit, BattleCommand.TYPE_SKILL, int(costs.get("ap_cost", skill_def.combat_profile.ap_cost)))
	_append_changed_unit_id(batch, active_unit.unit_id)

	var spell_control_context = _resolve_unit_spell_control_after_cost(active_unit, skill_def, batch)
	if bool(spell_control_context.get("skip_effects", false)):
		return true

	var applied = false
	var effect_defs = _collect_unit_skill_effect_defs(skill_def, cast_variant, active_unit)
	var repeat_attack_effect = _runtime._repeat_attack_resolver.get_repeat_attack_effect_def(effect_defs)

	if is_random_chain:
		return _handle_random_chain_unit_skill_command(
			active_unit,
			skill_def,
			cast_variant,
			batch,
			effect_defs,
			repeat_attack_effect,
			spell_control_context
		)

	for target_unit_variant in target_units:
		var target_unit = target_unit_variant as BattleUnitState
		if target_unit == null:
			continue
		if repeat_attack_effect != null:
			if _runtime._repeat_attack_resolver.apply_repeat_attack_skill_result(active_unit, target_unit, skill_def, effect_defs, repeat_attack_effect, batch):
				applied = true
			continue
		if _apply_unit_skill_result(active_unit, target_unit, skill_def, cast_variant, effect_defs, batch, spell_control_context):
			applied = true
	return applied

func _handle_random_chain_unit_skill_command(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	batch: BattleEventBatch,
	effect_defs: Array[CombatEffectDef],
	repeat_attack_effect: CombatEffectDef,
	spell_control_context: Dictionary
) -> bool:
	var max_hits_per_target := maxi(int(skill_def.combat_profile.max_hits_per_target), 1)
	var chain_hit_counts: Dictionary = {}
	var applied := false
	var attempt_count := 0
	var max_attempts := maxi(_runtime._state.units.size() * max_hits_per_target, 1)
	while attempt_count < max_attempts:
		var chain_pool := _build_random_chain_target_pool(active_unit, skill_def, chain_hit_counts, max_hits_per_target)
		if chain_pool.is_empty():
			break
		_shuffle_random_chain_pool(chain_pool)
		var target_unit := chain_pool[0] as BattleUnitState
		if target_unit == null:
			break
		chain_hit_counts[target_unit.unit_id] = int(chain_hit_counts.get(target_unit.unit_id, 0)) + 1
		attempt_count += 1
		var stage_applied := false
		if repeat_attack_effect != null:
			stage_applied = _runtime._repeat_attack_resolver.apply_repeat_attack_skill_result(
				active_unit,
				target_unit,
				skill_def,
				effect_defs,
				repeat_attack_effect,
				batch
			)
		else:
			stage_applied = _apply_unit_skill_result(
				active_unit,
				target_unit,
				skill_def,
				cast_variant,
				effect_defs,
				batch,
				spell_control_context
			)
		if stage_applied:
			applied = true
		else:
			break
	if attempt_count > 0:
		var skill_label := _format_skill_variant_label(skill_def, cast_variant)
		batch.log_lines.append("%s 的%s执行了 %d 次攻击链判定。" % [active_unit.display_name, skill_label, attempt_count])
	return applied


func _build_random_chain_target_pool(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	chain_hit_counts: Dictionary,
	max_hits_per_target: int
) -> Array:
	var chain_pool: Array = []
	if _runtime._state == null:
		return chain_pool
	for unit_variant in _runtime._state.units.values():
		var candidate := unit_variant as BattleUnitState
		if candidate == null or candidate == active_unit or not candidate.is_alive:
			continue
		var candidate_id := ProgressionDataUtils.to_string_name(candidate.unit_id)
		if candidate_id == &"" or int(chain_hit_counts.get(candidate_id, 0)) >= max_hits_per_target:
			continue
		if not _can_skill_target_unit(active_unit, candidate, skill_def, false):
			continue
		chain_pool.append(candidate)
	return chain_pool


func _shuffle_random_chain_pool(chain_pool: Array) -> void:
	if chain_pool.size() <= 1:
		return
	for index in range(chain_pool.size() - 1, 0, -1):
		var swap_index := TRUE_RANDOM_SEED_SERVICE_SCRIPT.randi_range(0, index)
		if swap_index == index:
			continue
		var temp = chain_pool[index]
		chain_pool[index] = chain_pool[swap_index]
		chain_pool[swap_index] = temp

func _handle_ground_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	batch: BattleEventBatch
) -> bool:
	var validation = _validate_ground_skill_command(active_unit, skill_def, cast_variant, command)
	if not bool(validation.get("allowed", false)):
		return false

	var target_coords: Array[Vector2i] = []
	for target_coord_variant in validation.get("target_coords", []):
		if target_coord_variant is Vector2i:
			target_coords.append(target_coord_variant)
	var precast_validation_message = _get_ground_special_effect_validation_message(
		active_unit,
		skill_def,
		cast_variant,
		target_coords
	)
	if not precast_validation_message.is_empty():
		batch.log_lines.append(precast_validation_message)
		return false

	var mp_before_cost = int(active_unit.current_mp)
	if not _consume_skill_costs(active_unit, skill_def, null, batch):
		return false
	var spent_mp = maxi(mp_before_cost - int(active_unit.current_mp), 0)
	var costs = _get_effective_skill_costs(active_unit, skill_def)
	_record_action_issued(active_unit, BattleCommand.TYPE_SKILL, int(costs.get("ap_cost", skill_def.combat_profile.ap_cost)))
	_append_changed_unit_id(batch, active_unit.unit_id)
	if _runtime._charge_resolver.is_charge_variant(cast_variant):
		return _runtime._charge_resolver.handle_charge_skill_command(active_unit, skill_def, cast_variant, validation, batch)
	var spell_control_context = _resolve_ground_spell_control_after_cost(
		active_unit,
		skill_def,
		spent_mp,
		batch
	)
	if bool(spell_control_context.get("skip_effects", false)):
		return false
	if not _apply_ground_precast_special_effects(active_unit, skill_def, cast_variant, target_coords, batch):
		return false

	var drift_context = _runtime._magic_backlash_resolver.build_ground_backlash_target_coords(
		skill_def,
		target_coords,
		_runtime._state,
		_runtime._grid_service,
		spell_control_context
	)
	if bool(drift_context.get("backlash_triggered", false)):
		var drift_target_coords: Array[Vector2i] = []
		for drift_coord_variant in drift_context.get("target_coords", target_coords):
			if drift_coord_variant is Vector2i:
				drift_target_coords.append(drift_coord_variant)
		if not drift_target_coords.is_empty():
			target_coords = drift_target_coords
		_runtime._magic_backlash_resolver.append_ground_backlash_log(active_unit, skill_def, drift_context, batch)
	var effect_coords = _build_ground_effect_coords(skill_def, target_coords, active_unit.coord if active_unit != null else Vector2i(-1, -1), active_unit, cast_variant)
	var unit_result = _apply_ground_unit_effects(
		active_unit,
		skill_def,
		_collect_ground_unit_effect_defs(skill_def, cast_variant, active_unit),
		effect_coords,
		batch,
		target_coords
	)
	var terrain_result = _apply_ground_terrain_effects(
		active_unit,
		skill_def,
		_collect_ground_terrain_effect_defs(skill_def, cast_variant, active_unit),
		effect_coords,
		batch
	)
	var applied = bool(unit_result.get("applied", false)) or bool(terrain_result.get("applied", false))

	if applied:
		batch.log_lines.append("%s 使用 %s，影响了 %d 个地格、%d 个单位。" % [
			active_unit.display_name,
			_format_skill_variant_label(skill_def, cast_variant),
			effect_coords.size(),
			int(unit_result.get("affected_unit_count", 0)),
		])
	return applied

func _should_route_skill_command_to_unit_targeting(skill_def: SkillDef, command: BattleCommand) -> bool:
	var allow_repeat = skill_def != null and skill_def.combat_profile != null and skill_def.combat_profile.allow_repeat_target
	return _runtime._skill_resolution_rules.should_route_skill_command_to_unit_targeting(
		skill_def,
		_normalize_target_unit_ids(command, allow_repeat)
	)

func _validate_unit_skill_targets(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> Dictionary:
	var result = {
		"allowed": false,
		"message": "技能或目标无效。",
		"target_unit_ids": [],
		"target_units": [],
		"preview_coords": [],
	}
	if _runtime._state == null or active_unit == null or command == null or skill_def == null or skill_def.combat_profile == null:
		return result

	var allow_repeat = skill_def != null and skill_def.combat_profile != null and skill_def.combat_profile.allow_repeat_target
	var target_unit_ids = _normalize_target_unit_ids(command, allow_repeat)
	var skill_level = _get_unit_skill_level(active_unit, skill_def.skill_id)
	var min_target_count = 1
	var max_target_count = 1
	if _is_multi_unit_skill(skill_def):
		min_target_count = maxi(int(skill_def.combat_profile.min_target_count), 1)
		max_target_count = maxi(int(skill_def.combat_profile.get_effective_max_target_count(skill_level)), min_target_count)
	var is_random_chain: bool = StringName(skill_def.combat_profile.target_selection_mode) == &"random_chain"
	if target_unit_ids.is_empty() and not is_random_chain:
		return result
	if is_random_chain:
		result.allowed = true
		result.message = ""
		result.target_unit_ids = []
		result.target_units = []
		return result
	if target_unit_ids.size() < min_target_count:
		result.message = "至少需要选择 %d 个单位目标。" % min_target_count
		return result
	if target_unit_ids.size() > max_target_count:
		result.message = "最多只能选择 %d 个单位目标。" % max_target_count
		return result
	if not _is_multi_unit_skill(skill_def) and target_unit_ids.size() != 1:
		result.message = "当前技能只允许选择 1 个单位目标。"
		return result
	if StringName(skill_def.combat_profile.selection_order_mode) != &"manual":
		target_unit_ids = _sort_target_unit_ids_for_execution(target_unit_ids)

	var target_units: Array = []
	for target_unit_id in target_unit_ids:
		var target_unit = _runtime._state.units.get(target_unit_id) as BattleUnitState
		var special_validation_message = _get_unit_skill_target_validation_message(active_unit, target_unit, skill_def, cast_variant)
		if not special_validation_message.is_empty():
			result.message = special_validation_message
			return result
		if target_unit == null or not _can_skill_target_unit(active_unit, target_unit, skill_def):
			result.message = "技能目标超出范围或不满足筛选条件。"
			return result
		target_units.append(target_unit)

	result.allowed = true
	result.message = ""
	result.target_unit_ids = target_unit_ids
	result.target_units = target_units
	var empty_target_coords: Array[Vector2i] = []
	var collected_target_coords = _runtime._target_collection_service.collect_combat_profile_target_coords(
		_runtime._state,
		_runtime._grid_service,
		active_unit.coord if active_unit != null else Vector2i(-1, -1),
		skill_def.combat_profile,
		empty_target_coords,
		active_unit,
		target_units,
		skill_level
	)
	result.preview_coords = _sort_coords(collected_target_coords.get("target_coords", []))
	return result

func _normalize_target_unit_ids(command: BattleCommand, allow_repeat: bool = false) -> Array[StringName]:
	var target_unit_ids: Array[StringName] = []
	if command == null:
		return target_unit_ids
	var seen_ids: Dictionary = {}
	var single_target_id = ProgressionDataUtils.to_string_name(command.target_unit_id)
	if single_target_id != &"":
		seen_ids[single_target_id] = true
		target_unit_ids.append(single_target_id)
	for target_unit_id_variant in command.target_unit_ids:
		var target_unit_id = ProgressionDataUtils.to_string_name(target_unit_id_variant)
		if target_unit_id == &"" or (not allow_repeat and seen_ids.has(target_unit_id)):
			continue
		seen_ids[target_unit_id] = true
		target_unit_ids.append(target_unit_id)
	return target_unit_ids

func _sort_target_unit_ids_for_execution(target_unit_ids: Array[StringName]) -> Array[StringName]:
	var sorted_ids = target_unit_ids.duplicate()
	if _runtime._state == null:
		return sorted_ids
	sorted_ids.sort_custom(func(a: StringName, b: StringName) -> bool:
		var unit_a = _runtime._state.units.get(a) as BattleUnitState
		var unit_b = _runtime._state.units.get(b) as BattleUnitState
		if unit_a == null or unit_b == null:
			return String(a) < String(b)
		return unit_a.coord.y < unit_b.coord.y \
			or (unit_a.coord.y == unit_b.coord.y and (unit_a.coord.x < unit_b.coord.x \
			or (unit_a.coord.x == unit_b.coord.x and String(a) < String(b))))
	)
	return sorted_ids

func _is_multi_unit_skill(skill_def: SkillDef) -> bool:
	return skill_def != null \
		and skill_def.combat_profile != null \
		and StringName(skill_def.combat_profile.target_selection_mode) in [&"multi_unit", &"random_chain"]

func _can_skill_target_unit(active_unit: BattleUnitState, target_unit: BattleUnitState, skill_def: SkillDef, require_ap: bool = true) -> bool:
	if active_unit == null or target_unit == null or skill_def == null or skill_def.combat_profile == null:
		return false
	var costs = _get_effective_skill_costs(active_unit, skill_def)
	if require_ap and active_unit.current_ap < int(costs.get("ap_cost", skill_def.combat_profile.ap_cost)):
		return false
	if not _is_unit_valid_for_effect(active_unit, target_unit, skill_def.combat_profile.target_team_filter):
		return false
	if not _get_unit_skill_target_validation_message(active_unit, target_unit, skill_def, null).is_empty():
		return false
	active_unit.refresh_footprint()
	target_unit.refresh_footprint()
	return _runtime._grid_service.get_distance_between_units(active_unit, target_unit) <= _get_effective_skill_range(active_unit, skill_def)

func _resolve_unit_skill_effect_result(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef]
) -> Dictionary:
	if _should_resolve_unit_skill_as_fate_attack(active_unit, target_unit, skill_def, effect_defs):
		var attack_check = _runtime._hit_resolver.build_skill_attack_check(active_unit, target_unit, skill_def)
		var attack_context = {
			"battle_state": _runtime._state,
			"skill_id": skill_def.skill_id,
		}
		if _runtime._skill_resolution_rules.is_force_hit_no_crit_skill(skill_def):
			attack_context["force_hit_no_crit"] = true
		var result = _runtime._damage_resolver.resolve_attack_effects(
			active_unit,
			target_unit,
			effect_defs,
			attack_check,
			attack_context
		)
		if _runtime._skill_resolution_rules.is_force_hit_no_crit_skill(skill_def):
			result["custom_log_lines"] = [
				"黑契推进压低了命运摆幅：这次攻击必定命中，且不会触发暴击。",
			]
		return result
	return _runtime._damage_resolver.resolve_effects(
		active_unit,
		target_unit,
		effect_defs,
		{"skill_id": skill_def.skill_id}
	) if not effect_defs.is_empty() else _runtime._damage_resolver.resolve_skill(active_unit, target_unit, skill_def)

func _should_resolve_unit_skill_as_fate_attack(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef]
) -> bool:
	return _runtime._skill_resolution_rules.should_resolve_unit_skill_as_fate_attack(
		active_unit,
		target_unit,
		skill_def,
		effect_defs
	)

func _apply_unit_skill_result(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch,
	spell_control_context: Dictionary = {}
) -> bool:
	var barrier_result: Dictionary = _runtime._layered_barrier_service.resolve_skill_barrier_interaction(
		active_unit,
		target_unit,
		skill_def,
		effect_defs,
		batch
	) if _runtime._layered_barrier_service != null else {}
	if bool(barrier_result.get("blocked", false)):
		return bool(barrier_result.get("applied", false))
	var result = _resolve_unit_skill_effect_result(active_unit, target_unit, skill_def, effect_defs)
	_runtime._skill_mastery_service.record_target_result(active_unit, target_unit, skill_def, result, effect_defs)
	_flush_last_stand_mastery_records(batch)
	var guard_mastery_grant = _runtime._skill_mastery_service.build_guard_mastery_grant_from_incoming_hit(
		active_unit,
		target_unit,
		effect_defs,
		result,
		_runtime._skill_defs
	)
	var shield_roll_context = {}
	var shield_result = _apply_unit_shield_effects(
		active_unit,
		target_unit,
		skill_def,
		effect_defs,
		shield_roll_context
	)
	mark_applied_statuses_for_turn_timing(target_unit, result.get("status_effect_ids", []))
	_append_changed_unit_id(batch, target_unit.unit_id)
	_append_changed_unit_coords(batch, target_unit)
	append_result_source_status_effects(batch, active_unit, result)
	var special_result = _apply_unit_skill_special_effects(active_unit, target_unit, skill_def, cast_variant, effect_defs, batch)
	mark_applied_statuses_for_turn_timing(target_unit, special_result.get("status_effect_ids", []))
	var applied = bool(result.get("applied", false)) \
		or bool(shield_result.get("applied", false)) \
		or bool(special_result.get("applied", false))
	if not applied:
		_append_result_report_entry(batch, result)
		for custom_line_variant in result.get("custom_log_lines", []):
			var custom_line = String(custom_line_variant)
			if not custom_line.is_empty():
				batch.log_lines.append(custom_line)
		for special_line_variant in special_result.get("log_lines", []):
			var special_line = String(special_line_variant)
			if not special_line.is_empty():
				batch.log_lines.append(special_line)
		return false

	var skill_label = _format_skill_variant_label(skill_def, cast_variant)
	var skill_subject = _build_skill_log_subject_label(active_unit, skill_def, cast_variant)
	var damage = int(result.get("damage", 0))
	var healing = int(result.get("healing", 0))
	var moved_steps = int(special_result.get("moved_steps", 0))
	_record_vajra_body_mastery_from_incoming_damage(active_unit, target_unit, skill_def, result, batch)
	if moved_steps > 0:
		batch.log_lines.append("%s 使用 %s，向更安全位置移动 %d 格。" % [
			active_unit.display_name,
			skill_label,
			moved_steps,
		])
	append_damage_result_log_lines(
		batch,
		skill_subject,
		target_unit.display_name,
		result
	)
	_apply_equipment_durability_result(target_unit, result, batch)
	_append_result_report_entry(batch, result)
	if _is_doom_sentence_skill(skill_def.skill_id):
		var doom_sentence_report_tags: Array[StringName] = [BATTLE_REPORT_FORMATTER_SCRIPT.TAG_DOOM_SENTENCE]
		_append_report_entry_to_batch(
			batch,
			_runtime._report_formatter.build_skill_event_entry(
				active_unit,
				target_unit,
				skill_def.skill_id,
				BATTLE_REPORT_FORMATTER_SCRIPT.REASON_DOOM_SENTENCE_APPLIED,
				doom_sentence_report_tags
			)
		)
	if healing > 0:
		batch.log_lines.append("%s 为 %s 恢复 %d 点生命。" % [
			skill_subject,
			target_unit.display_name,
			healing,
		])
	if bool(shield_result.get("applied", false)):
		batch.log_lines.append("%s 使 %s 的护盾值变为 %d。" % [
			skill_subject,
			target_unit.display_name,
			int(shield_result.get("current_shield_hp", 0)),
		])
	for status_id in result.get("status_effect_ids", []):
		batch.log_lines.append("%s 获得状态 %s。" % [target_unit.display_name, String(status_id)])
	_append_dispel_result_log_lines(batch, skill_subject, target_unit, result)
	_apply_chain_damage_effects(active_unit, target_unit, skill_def, effect_defs, result, batch, skill_subject, spell_control_context)
	for custom_line_variant in result.get("custom_log_lines", []):
		var custom_line = String(custom_line_variant)
		if not custom_line.is_empty():
			batch.log_lines.append(custom_line)
	for special_line_variant in special_result.get("log_lines", []):
		var special_line = String(special_line_variant)
		if not special_line.is_empty():
			batch.log_lines.append(special_line)
	var terrain_effect_ids: Array = result.get("terrain_effect_ids", [])
	if not terrain_effect_ids.is_empty():
		for terrain_effect_id in terrain_effect_ids:
			var target_cell = _runtime._grid_service.get_cell(_runtime._state, target_unit.coord)
			if target_cell != null and not target_cell.terrain_effect_ids.has(terrain_effect_id):
				target_cell.terrain_effect_ids.append(terrain_effect_id)
				_append_changed_coord(batch, target_unit.coord)
				batch.log_lines.append("%s 使 %s 所在的地格附加效果 %s。" % [
					skill_subject,
					target_unit.display_name,
					String(terrain_effect_id),
				])
	var height_delta = int(result.get("height_delta", 0))
	var target_coord = target_unit.coord
	var target_cell_before = _runtime._grid_service.get_cell(_runtime._state, target_coord)
	var before_height = int(target_cell_before.current_height) if target_cell_before != null else 0
	if height_delta != 0 and _runtime._grid_service.apply_height_delta(_runtime._state, target_coord, height_delta):
		_append_changed_coord(batch, target_coord)
		var target_cell_after = _runtime._grid_service.get_cell(_runtime._state, target_coord)
		var after_height = int(target_cell_after.current_height) if target_cell_after != null else before_height + height_delta
		batch.log_lines.append("%s 使 (%d, %d) 的高度由 %d 变为 %d。" % [
			skill_subject,
			target_coord.x,
			target_coord.y,
			before_height,
			after_height,
		])
	if not target_unit.is_alive:
		_apply_on_kill_gain_resources_effects(active_unit, target_unit, skill_def, effect_defs, batch)
		_collect_defeated_unit_loot(target_unit, active_unit)
		_clear_defeated_unit(target_unit, batch)
		batch.log_lines.append("%s 被击倒。" % target_unit.display_name)
		_runtime._battle_rating_system.record_enemy_defeated_achievement(active_unit, target_unit)
		_record_unit_defeated(target_unit)
	if active_unit != null and target_unit != null:
		_record_effect_metrics(active_unit, target_unit, damage, healing, 1 if not target_unit.is_alive else 0)
	_runtime._battle_rating_system.record_skill_effect_result(active_unit, damage, healing, 1 if not target_unit.is_alive else 0)
	_apply_skill_mastery_grant(target_unit, guard_mastery_grant, batch)
	return true

func _apply_equipment_durability_result(
	target_unit: BattleUnitState,
	result: Dictionary,
	batch: BattleEventBatch
) -> void:
	if target_unit == null or batch == null:
		return
	var events = result.get("equipment_durability_events", [])
	if events is not Array:
		return
	var destroyed_any := false
	for event_variant in events:
		if event_variant is not Dictionary:
			continue
		var event := event_variant as Dictionary
		var item_id := String(event.get("item_id", ""))
		if item_id.is_empty():
			item_id = "装备"
		var save_result = event.get("save_result", {})
		if save_result is Dictionary and bool(save_result.get("has_save", false)) and bool(save_result.get("success", false)):
			batch.log_lines.append("%s 的 %s 抵抗了裂解术。" % [target_unit.display_name, item_id])
			continue
		var durability_loss := int(event.get("durability_loss", 0))
		if durability_loss <= 0:
			continue
		if bool(event.get("destroyed", false)):
			destroyed_any = true
			batch.log_lines.append("%s 的 %s 被裂解为尘埃。" % [target_unit.display_name, item_id])
		else:
			batch.log_lines.append("%s 的 %s 被裂解，耐久 %d -> %d。" % [
				target_unit.display_name,
				item_id,
				int(event.get("durability_before", 0)),
				int(event.get("durability_after", 0)),
			])
	if destroyed_any:
		_refresh_target_after_equipment_destruction(target_unit)
		_append_changed_unit_id(batch, target_unit.unit_id)
		_append_changed_unit_coords(batch, target_unit)


func _append_dispel_result_log_lines(
	batch: BattleEventBatch,
	skill_subject: String,
	target_unit: BattleUnitState,
	result: Dictionary
) -> void:
	if batch == null or target_unit == null:
		return
	var dispel_events = result.get("dispel_events", [])
	if dispel_events is not Array:
		return
	for event_variant in dispel_events:
		if event_variant is not Dictionary:
			continue
		var removed_ids: Array = (event_variant as Dictionary).get("removed_status_ids", [])
		if removed_ids.is_empty():
			continue
		var labels: PackedStringArray = []
		for status_id_variant in removed_ids:
			labels.append(String(ProgressionDataUtils.to_string_name(status_id_variant)))
		batch.log_lines.append("%s 解除 %s 身上的 %s。" % [
			skill_subject,
			target_unit.display_name,
			"、".join(labels),
		])


func _refresh_target_after_equipment_destruction(target_unit: BattleUnitState) -> void:
	if target_unit == null or _runtime == null or _runtime._unit_factory == null:
		return
	if target_unit.source_member_id != &"":
		_runtime._unit_factory.refresh_equipment_projection(target_unit)
	_clamp_target_resources_after_equipment_projection(target_unit)


func _clamp_target_resources_after_equipment_projection(target_unit: BattleUnitState) -> void:
	if target_unit == null or target_unit.attribute_snapshot == null:
		return
	target_unit.current_hp = clampi(
		target_unit.current_hp,
		0,
		maxi(int(target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)), 1)
	)
	target_unit.current_mp = clampi(
		target_unit.current_mp,
		0,
		maxi(int(target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX)), 0)
	)
	target_unit.current_stamina = clampi(
		target_unit.current_stamina,
		0,
		maxi(int(target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX)), 0)
	)
	target_unit.current_aura = clampi(
		target_unit.current_aura,
		0,
		maxi(int(target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX)), 0)
	)
	target_unit.is_alive = target_unit.current_hp > 0


func _apply_chain_damage_effects(
	source_unit: BattleUnitState,
	primary_target: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	primary_result: Dictionary,
	batch: BattleEventBatch,
	skill_subject: String,
	spell_control_context: Dictionary = {}
) -> void:
	if _runtime._state == null or source_unit == null or primary_target == null or skill_def == null or batch == null:
		return
	if not bool(primary_result.get("applied", false)):
		return
	var chain_effects = _collect_chain_damage_effect_defs(effect_defs)
	if chain_effects.is_empty():
		return

	for chain_effect in chain_effects:
		var chain_target_effects = _build_chain_target_effect_defs(effect_defs, chain_effect)
		if chain_target_effects.is_empty():
			continue
		var chain_targets = _collect_chain_damage_targets(source_unit, primary_target, skill_def, chain_effect, spell_control_context)
		if chain_targets.is_empty():
			continue

		var total_damage = 0
		var total_healing = 0
		var total_kill_count = 0
		for chain_target in chain_targets:
			if chain_target == null or not chain_target.is_alive:
				continue
			var chain_result = _runtime._damage_resolver.resolve_effects(
				source_unit,
				chain_target,
				chain_target_effects,
				{"skill_id": skill_def.skill_id}
			)
			_runtime._skill_mastery_service.record_target_result(source_unit, chain_target, skill_def, chain_result, chain_target_effects)
			mark_applied_statuses_for_turn_timing(chain_target, chain_result.get("status_effect_ids", []))
			if not bool(chain_result.get("applied", false)):
				continue

			_append_changed_unit_id(batch, source_unit.unit_id)
			_append_changed_unit_id(batch, chain_target.unit_id)
			_append_changed_unit_coords(batch, chain_target)
			append_result_source_status_effects(batch, source_unit, chain_result)
			append_damage_result_log_lines(batch, "%s 的连锁闪电" % skill_subject, chain_target.display_name, chain_result)
			for status_id in chain_result.get("status_effect_ids", []):
				batch.log_lines.append("%s 获得状态 %s。" % [chain_target.display_name, String(status_id)])

			var chain_damage = int(chain_result.get("damage", 0))
			var chain_healing = int(chain_result.get("healing", 0))
			total_damage += chain_damage
			total_healing += chain_healing
			if not chain_target.is_alive:
				total_kill_count += 1
				_apply_on_kill_gain_resources_effects(source_unit, chain_target, skill_def, chain_target_effects, batch)
				_collect_defeated_unit_loot(chain_target, source_unit)
				_clear_defeated_unit(chain_target, batch)
				batch.log_lines.append("%s 被击倒。" % chain_target.display_name)
				_runtime._battle_rating_system.record_enemy_defeated_achievement(source_unit, chain_target)
				_record_unit_defeated(chain_target)
			_record_effect_metrics(source_unit, chain_target, chain_damage, chain_healing, 1 if not chain_target.is_alive else 0)

		if total_damage > 0 or total_healing > 0 or total_kill_count > 0:
			_runtime._battle_rating_system.record_skill_effect_result(source_unit, total_damage, total_healing, total_kill_count)

func _collect_chain_damage_effect_defs(effect_defs: Array[CombatEffectDef]) -> Array[CombatEffectDef]:
	var chain_effects: Array[CombatEffectDef] = []
	for effect_def in effect_defs:
		if effect_def != null and effect_def.effect_type == CHAIN_DAMAGE_EFFECT_TYPE:
			chain_effects.append(effect_def)
	return chain_effects

func _get_effect_params(effect_def: CombatEffectDef) -> Dictionary:
	if effect_def == null or effect_def.params == null:
		return {}
	return effect_def.params

func _build_chain_target_effect_defs(
	effect_defs: Array[CombatEffectDef],
	chain_effect: CombatEffectDef
) -> Array[CombatEffectDef]:
	var chain_target_effects: Array[CombatEffectDef] = []
	for effect_def in effect_defs:
		if effect_def == null or effect_def.effect_type == CHAIN_DAMAGE_EFFECT_TYPE:
			continue
		var runtime_effect = effect_def.duplicate_for_runtime()
		if runtime_effect == null:
			continue
		chain_target_effects.append(runtime_effect)
	return chain_target_effects

func _collect_chain_damage_targets(
	source_unit: BattleUnitState,
	primary_target: BattleUnitState,
	skill_def: SkillDef,
	chain_effect: CombatEffectDef,
	spell_control_context: Dictionary = {}
) -> Array[BattleUnitState]:
	var targets: Array[BattleUnitState] = []
	if _runtime._state == null or source_unit == null or primary_target == null or chain_effect == null:
		return targets

	var max_radius = _resolve_chain_damage_radius(primary_target, chain_effect, spell_control_context)
	var chain_params = _get_effect_params(chain_effect)
	if max_radius <= 0:
		return targets
	var prevent_repeat_target = bool(chain_params.get("prevent_repeat_target", true))
	var target_filter = _resolve_effect_target_filter(skill_def, chain_effect)
	if target_filter == &"":
		target_filter = skill_def.combat_profile.target_team_filter if skill_def != null and skill_def.combat_profile != null else &"enemy"

	var visited: Dictionary = {}
	var queue: Array[BattleUnitState] = []
	visited[primary_target.unit_id] = true
	queue.append(primary_target)

	while not queue.is_empty():
		var current = queue.pop_front() as BattleUnitState

		for unit_variant in _runtime._state.units.values():
			var candidate = unit_variant as BattleUnitState
			if candidate == null or not candidate.is_alive:
				continue
			if prevent_repeat_target and visited.has(candidate.unit_id):
				continue
			if not _is_unit_valid_for_effect(source_unit, candidate, target_filter):
				continue
			# 必须在总传播半径内（以主目标为起点的曼哈顿距离）
			if not _is_within_chain_radius(primary_target, candidate, max_radius):
				continue
			# 路径上不能有高度差 > 1 的阻挡
			if not _is_chain_path_clear(current, candidate):
				continue

			visited[candidate.unit_id] = true
			targets.append(candidate)
			queue.append(candidate)

	targets.sort_custom(func(a: BattleUnitState, b: BattleUnitState) -> bool:
		var distance_a = _runtime._grid_service.get_distance_between_units(primary_target, a)
		var distance_b = _runtime._grid_service.get_distance_between_units(primary_target, b)
		if distance_a != distance_b:
			return distance_a < distance_b
		if a.coord.y != b.coord.y:
			return a.coord.y < b.coord.y
		if a.coord.x != b.coord.x:
			return a.coord.x < b.coord.x
		return String(a.unit_id) < String(b.unit_id)
	)
	return targets

func _resolve_chain_damage_radius(primary_target: BattleUnitState, chain_effect: CombatEffectDef, spell_control_context: Dictionary = {}) -> int:
	var chain_params = _get_effect_params(chain_effect)
	var base_radius = maxi(int(chain_params.get("base_chain_radius", 1)), 0)
	var bonus_effect_id = ProgressionDataUtils.to_string_name(chain_params.get("bonus_terrain_effect_id", ""))
	var radius = base_radius
	if bonus_effect_id != &"" and primary_target != null and _unit_stands_on_terrain_effect(primary_target, bonus_effect_id):
		radius = maxi(int(chain_params.get("wet_chain_radius", base_radius)), base_radius)
	if bool(spell_control_context.get("backlash_triggered", false)):
		radius += 1
	return radius

func _unit_stands_on_terrain_effect(unit_state: BattleUnitState, terrain_effect_id: StringName) -> bool:
	if _runtime._state == null or unit_state == null or terrain_effect_id == &"":
		return false
	unit_state.refresh_footprint()
	for occupied_coord in unit_state.occupied_coords:
		var cell = _runtime._grid_service.get_cell(_runtime._state, occupied_coord) as BattleCellState
		if cell == null:
			continue
		if cell.terrain_effect_ids.has(terrain_effect_id):
			return true
		for effect_state_variant in cell.timed_terrain_effects:
			var effect_state = effect_state_variant as BattleTerrainEffectState
			if effect_state != null and effect_state.effect_id == terrain_effect_id:
				return true
	return false

func _is_unit_in_chain_radius(
	primary_target: BattleUnitState,
	candidate: BattleUnitState,
	radius: int,
	chain_effect: CombatEffectDef
) -> bool:
	if primary_target == null or candidate == null or radius <= 0:
		return false
	var chain_shape = String(_get_effect_params(chain_effect).get("chain_shape", "diamond"))
	primary_target.refresh_footprint()
	candidate.refresh_footprint()
	for primary_coord in primary_target.occupied_coords:
		for candidate_coord in candidate.occupied_coords:
			match chain_shape:
				"square", "radius":
					if _runtime._grid_service.get_chebyshev_distance(primary_coord, candidate_coord) <= radius:
						return true
				_:
					if _runtime._grid_service.get_distance(primary_coord, candidate_coord) <= radius:
						return true
	return false

func _is_within_chain_radius(primary_target: BattleUnitState, candidate: BattleUnitState, max_radius: int) -> bool:
	if primary_target == null or candidate == null or max_radius <= 0:
		return false
	primary_target.refresh_footprint()
	candidate.refresh_footprint()
	for primary_coord in primary_target.occupied_coords:
		for candidate_coord in candidate.occupied_coords:
			if _runtime._grid_service.get_distance(primary_coord, candidate_coord) <= max_radius:
				return true
	return false

func _get_line_coords(from: Vector2i, to: Vector2i) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	var dx = absi(to.x - from.x)
	var dy = absi(to.y - from.y)
	var sx = 1 if from.x < to.x else -1
	var sy = 1 if from.y < to.y else -1
	var err = dx - dy
	var x = from.x
	var y = from.y
	while x != to.x or y != to.y:
		var e2 = 2 * err
		if e2 > -dy:
			err -= dy
			x += sx
		if e2 < dx:
			err += dx
			y += sy
		if x == to.x and y == to.y:
			break
		coords.append(Vector2i(x, y))
	return coords

func _is_chain_path_clear(source_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	if _runtime._state == null or source_unit == null or target_unit == null or _runtime._grid_service == null:
		return false
	source_unit.refresh_footprint()
	target_unit.refresh_footprint()
	for source_coord in source_unit.occupied_coords:
		var source_cell = _runtime._grid_service.get_cell(_runtime._state, source_coord)
		if source_cell == null:
			continue
		var source_height = int(source_cell.current_height)
		for target_coord in target_unit.occupied_coords:
			var line_coords = _get_line_coords(source_coord, target_coord)
			for mid_coord in line_coords:
				var mid_cell = _runtime._grid_service.get_cell(_runtime._state, mid_coord)
				if mid_cell == null:
					continue
				if absi(int(mid_cell.current_height) - source_height) > 1:
					return false
	return true

func _is_chain_height_valid(from_unit: BattleUnitState, to_unit: BattleUnitState) -> bool:
	if _runtime._state == null or from_unit == null or to_unit == null or _runtime._grid_service == null:
		return false
	from_unit.refresh_footprint()
	to_unit.refresh_footprint()
	for from_coord in from_unit.occupied_coords:
		for to_coord in to_unit.occupied_coords:
			if _runtime._grid_service.get_height_difference(_runtime._state, from_coord, to_coord) <= 1:
				return true
	return false

func _get_unit_skill_target_validation_message(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> String:
	var body_size_override_message = _get_body_size_category_override_validation_message(
		active_unit,
		target_unit,
		skill_def,
		cast_variant
	)
	if not body_size_override_message.is_empty():
		return body_size_override_message
	if _is_black_crown_seal_skill(skill_def.skill_id) and not _is_black_crown_seal_target_eligible(active_unit, target_unit):
		return "黑冠封印只能对 boss 施放。"
	if _is_doom_shift_skill(skill_def.skill_id):
		if target_unit == null or active_unit == null:
			return "断命换位的目标无效。"
		if target_unit.unit_id == active_unit.unit_id:
			return "断命换位不能以自己为目标。"
	if _is_crown_break_skill(skill_def.skill_id) and not _is_crown_break_target_eligible(active_unit, target_unit):
		return "折冠只能对已被黑星烙印的 elite / boss 施放。"
	if _is_doom_sentence_skill(skill_def.skill_id) and not _is_doom_sentence_target_eligible(active_unit, target_unit):
		return "厄命宣判只能对 elite / boss 施放。"
	return ""

func _get_body_size_category_override_validation_message(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> String:
	if _runtime._state == null or target_unit == null or skill_def == null:
		return ""
	for effect_def in _collect_unit_skill_effect_defs(skill_def, cast_variant, active_unit):
		if effect_def == null or effect_def.effect_type != BODY_SIZE_CATEGORY_OVERRIDE_EFFECT_TYPE:
			continue
		var target_category = ProgressionDataUtils.to_string_name(effect_def.body_size_category)
		if not BodySizeRules.is_valid_body_size_category(target_category):
			continue
		var target_footprint = BodySizeRules.get_footprint_for_category(target_category)
		if not _runtime._grid_service.can_place_footprint(_runtime._state, target_unit.coord, target_footprint, target_unit.unit_id, target_unit):
			return "%s 周围空间不足，无法改变体型。" % target_unit.display_name
	return ""

func _skill_grants_guarding(skill_def: SkillDef) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	for effect_def_variant in _collect_unit_skill_effect_defs(skill_def, null):
		var effect_def = effect_def_variant as CombatEffectDef
		if effect_def == null:
			continue
		if effect_def.effect_type in [&"status", &"apply_status"] and effect_def.status_id == STATUS_GUARDING:
			return true
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant == null:
			continue
		for effect_def_variant in cast_variant.effect_defs:
			var effect_def = effect_def_variant as CombatEffectDef
			if effect_def == null:
				continue
			if effect_def.effect_type in [&"status", &"apply_status"] and effect_def.status_id == STATUS_GUARDING:
				return true
	return false

func _collect_unit_skill_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	return _runtime._skill_resolution_rules.collect_unit_skill_effect_defs(skill_def, cast_variant, active_unit)

func _collect_units_in_coords(effect_coords: Array[Vector2i]) -> Array[BattleUnitState]:
	var units: Array[BattleUnitState] = []
	var seen_unit_ids: Dictionary = {}
	for effect_coord in effect_coords:
		var target_unit = _runtime._grid_service.get_unit_at_coord(_runtime._state, effect_coord)
		if target_unit == null or not target_unit.is_alive or seen_unit_ids.has(target_unit.unit_id):
			continue
		seen_unit_ids[target_unit.unit_id] = true
		units.append(target_unit)
	return units

func _is_unit_effect(effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	return effect_def.effect_type == &"damage" \
		or effect_def.effect_type == EQUIPMENT_DURABILITY_DAMAGE_EFFECT_TYPE \
		or effect_def.effect_type == &"dispel_magic" \
		or effect_def.effect_type == &"heal" \
		or effect_def.effect_type == &"shield" \
		or effect_def.effect_type == &"layered_barrier" \
		or effect_def.effect_type == &"status" \
		or effect_def.effect_type == &"apply_status" \
		or effect_def.effect_type == &"forced_move"

func _is_terrain_effect(effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	return effect_def.effect_type == &"terrain" \
		or effect_def.effect_type == &"terrain_replace" \
		or effect_def.effect_type == &"terrain_replace_to" \
		or effect_def.effect_type == &"height" \
		or effect_def.effect_type == &"height_delta" \
		or effect_def.effect_type == &"terrain_effect"

func _resolve_effect_target_filter(skill_def: SkillDef, effect_def: CombatEffectDef) -> StringName:
	if effect_def != null and effect_def.effect_target_team_filter != &"":
		return effect_def.effect_target_team_filter
	if skill_def != null and skill_def.combat_profile != null:
		return skill_def.combat_profile.target_team_filter
	return &"any"

func _is_unit_valid_for_effect(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	target_team_filter: StringName
) -> bool:
	if target_unit == null or not target_unit.is_alive:
		return false
	if source_unit != null \
			and bool(source_unit.ai_blackboard.get("madness_target_any_team", false)) \
			and target_team_filter in [&"ally", &"friendly", &"enemy", &"hostile"]:
		return target_unit.unit_id != source_unit.unit_id
	match target_team_filter:
		&"", &"any":
			return true
		&"self":
			return source_unit != null and target_unit.unit_id == source_unit.unit_id
		&"ally", &"friendly":
			return source_unit != null and target_unit.faction_id == source_unit.faction_id
		&"enemy", &"hostile":
			return source_unit != null and target_unit.faction_id != source_unit.faction_id
		_:
			return true

func _resolve_ground_cast_variant(
	skill_def: SkillDef,
	active_unit: BattleUnitState,
	command: BattleCommand
) -> CombatCastVariantDef:
	return _runtime._skill_resolution_rules.resolve_ground_cast_variant(
		skill_def,
		active_unit,
		command.skill_variant_id if command != null else &""
	)

func _resolve_unit_cast_variant(
	skill_def: SkillDef,
	active_unit: BattleUnitState,
	command: BattleCommand
) -> CombatCastVariantDef:
	return _runtime._skill_resolution_rules.resolve_unit_cast_variant(
		skill_def,
		active_unit,
		command.skill_variant_id if command != null else &""
	)

func _get_cast_variant_target_mode(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> StringName:
	return _runtime._skill_resolution_rules.get_cast_variant_target_mode(skill_def, cast_variant)

func _build_implicit_ground_cast_variant(skill_def: SkillDef) -> CombatCastVariantDef:
	var cast_variant = CombatCastVariantDef.new()
	cast_variant.variant_id = &""
	cast_variant.display_name = ""
	cast_variant.target_mode = &"ground"
	cast_variant.footprint_pattern = &"single"
	cast_variant.required_coord_count = 1
	if skill_def != null and skill_def.combat_profile != null:
		cast_variant.effect_defs = skill_def.combat_profile.effect_defs.duplicate()
	else:
		cast_variant.effect_defs = []
	return cast_variant

func _get_unit_skill_level(unit_state: BattleUnitState, skill_id: StringName) -> int:
	if unit_state == null or skill_id == &"":
		return 0
	if unit_state.known_skill_level_map.has(skill_id):
		return int(unit_state.known_skill_level_map.get(skill_id, 0))
	return 1 if unit_state.known_active_skill_ids.has(skill_id) else 0

func _format_skill_variant_label(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> String:
	if skill_def == null:
		return ""
	if cast_variant == null or cast_variant.display_name.is_empty():
		return skill_def.display_name
	return "%s·%s" % [skill_def.display_name, cast_variant.display_name]
