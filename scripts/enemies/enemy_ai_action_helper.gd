class_name EnemyAiActionHelper
extends RefCounted

const BATTLE_AI_DECISION_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_decision.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleAiDecision = preload("res://scripts/systems/battle/ai/battle_ai_decision.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")


static func create_decision(action_id: StringName, score_bucket_id: StringName, command, reason_text: String = "") -> BattleAiDecision:
	var decision = BATTLE_AI_DECISION_SCRIPT.new()
	decision.command = command
	decision.action_id = action_id
	decision.reason_text = reason_text
	decision.score_bucket_id = score_bucket_id
	return decision


static func create_scored_decision(action_id: StringName, score_bucket_id: StringName, command, score_input, reason_text: String = "") -> BattleAiDecision:
	var decision = create_decision(action_id, score_bucket_id, command, reason_text)
	decision.skill_score_input = score_input
	decision.score_input = score_input
	return decision


static func build_wait_command(context):
	if context == null or context.unit_state == null:
		return null
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BattleCommand.TYPE_WAIT
	command.unit_id = context.unit_state.unit_id
	return command


static func build_move_command(context, target_coord: Vector2i):
	if context == null or context.unit_state == null:
		return null
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BattleCommand.TYPE_MOVE
	command.unit_id = context.unit_state.unit_id
	command.target_coord = target_coord
	return command


static func build_unit_skill_command(context, skill_id: StringName, target_unit, skill_variant_id: StringName = &""):
	if context == null or context.unit_state == null or target_unit == null:
		return null
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = context.unit_state.unit_id
	command.skill_id = skill_id
	command.skill_variant_id = skill_variant_id
	command.target_unit_id = target_unit.unit_id
	command.target_coord = target_unit.coord
	return command


static func build_ground_skill_command(context, skill_id: StringName, skill_variant_id: StringName, target_coords: Array):
	if context == null or context.unit_state == null:
		return null
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = context.unit_state.unit_id
	command.skill_id = skill_id
	command.skill_variant_id = skill_variant_id
	command.target_coords = sort_coords(target_coords)
	if not command.target_coords.is_empty():
		command.target_coord = command.target_coords[0]
	return command


static func sort_coords(coords: Array) -> Array[Vector2i]:
	var sorted_coords: Array[Vector2i] = []
	for coord_variant in coords:
		if coord_variant is Vector2i:
			sorted_coords.append(coord_variant)
	sorted_coords.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		return left.y < right.y or (left.y == right.y and left.x < right.x)
	)
	return sorted_coords


static func coord_set_key(coords: Array[Vector2i]) -> String:
	var parts: Array[String] = []
	for coord in sort_coords(coords):
		parts.append("%d:%d" % [coord.x, coord.y])
	return "|".join(parts)


static func begin_action_trace(action_id: StringName, score_bucket_id: StringName, context, metadata: Dictionary = {}) -> Dictionary:
	var trace_id: StringName = context.next_action_trace_id(action_id) if context != null and context.has_method("next_action_trace_id") else action_id
	return {
		"trace_id": trace_id,
		"action_id": String(action_id),
		"score_bucket_id": String(score_bucket_id),
		"metadata": metadata.duplicate(true),
		"evaluation_count": 0,
		"blocked_count": 0,
		"preview_reject_count": 0,
		"candidate_count": 0,
		"block_reasons": {},
		"top_candidates": [],
		"chosen": false,
	}


static func trace_count_increment(action_trace: Dictionary, key: String, amount: int = 1) -> void:
	if action_trace.is_empty() or key.is_empty():
		return
	action_trace[key] = int(action_trace.get(key, 0)) + amount


static func trace_add_block_reason(action_trace: Dictionary, reason_key: String) -> void:
	if action_trace.is_empty() or reason_key.is_empty():
		return
	trace_count_increment(action_trace, "blocked_count", 1)
	var block_reasons: Dictionary = action_trace.get("block_reasons", {})
	block_reasons[reason_key] = int(block_reasons.get(reason_key, 0)) + 1
	action_trace["block_reasons"] = block_reasons


static func trace_offer_candidate(action_trace: Dictionary, candidate_summary: Dictionary, keep_count: int = 5) -> void:
	if action_trace.is_empty() or candidate_summary.is_empty():
		return
	trace_count_increment(action_trace, "candidate_count", 1)
	var top_candidates = action_trace.get("top_candidates", [])
	if top_candidates is not Array:
		top_candidates = []
	top_candidates.append(candidate_summary.duplicate(true))
	top_candidates.sort_custom(func(left: Dictionary, right: Dictionary) -> bool:
		return int(left.get("total_score", -999999)) > int(right.get("total_score", -999999))
	)
	while top_candidates.size() > keep_count:
		top_candidates.pop_back()
	action_trace["top_candidates"] = top_candidates


static func finalize_action_trace(context, action_trace: Dictionary, best_decision: BattleAiDecision = null) -> StringName:
	if action_trace.is_empty():
		return &""
	if best_decision != null:
		action_trace["best_reason_text"] = best_decision.reason_text
		action_trace["best_command"] = build_command_summary(best_decision.command)
		var score_input = best_decision.score_input if best_decision.score_input != null else best_decision.skill_score_input
		action_trace["best_score_input"] = score_input.to_dict() if score_input != null else {}
		best_decision.action_trace_id = ProgressionDataUtils.to_string_name(action_trace.get("trace_id", ""))
	if context != null and context.has_method("record_action_trace"):
		context.record_action_trace(action_trace)
	return ProgressionDataUtils.to_string_name(action_trace.get("trace_id", ""))


static func build_candidate_summary(label: String, command, score_input = null, extra: Dictionary = {}) -> Dictionary:
	var summary := {
		"label": label,
		"command": build_command_summary(command),
		"total_score": int(score_input.total_score) if score_input != null else int(extra.get("total_score", 0)),
		"score_input": score_input.to_dict() if score_input != null else {},
	}
	for key in extra.keys():
		summary[key] = extra.get(key)
	return summary


static func format_skill_variant_label(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> String:
	if skill_def == null:
		return ""
	if cast_variant == null or cast_variant.display_name.is_empty():
		return skill_def.display_name
	return "%s·%s" % [skill_def.display_name, cast_variant.display_name]


static func build_command_summary(command) -> Dictionary:
	if command == null:
		return {}
	return {
		"command_type": String(command.command_type),
		"unit_id": String(command.unit_id),
		"skill_id": String(command.skill_id),
		"skill_variant_id": String(command.skill_variant_id),
		"target_unit_id": String(command.target_unit_id),
		"target_unit_ids": command.target_unit_ids.duplicate(),
		"target_coord": command.target_coord,
		"target_coords": command.target_coords.duplicate(),
	}
