class_name WaitAction
extends "res://scripts/enemies/enemy_ai_action.gd"


func decide(context):
	var action_trace := _begin_action_trace(context, {"action_kind": "wait"})
	var command = _build_wait_command(context)
	var score_input = _build_action_score_input(
		context,
		&"wait",
		String(action_id),
		command,
		null,
		{
			"position_objective_kind": &"none",
		}
	)
	var decision = _create_scored_decision(
		command,
		score_input,
		"%s 没有更优动作，选择待机。" % [context.unit_state.display_name]
	)
	_trace_offer_candidate(action_trace, _build_candidate_summary("wait", command, score_input))
	_finalize_action_trace(context, action_trace, decision)
	return decision


func validate_schema() -> Array[String]:
	return _collect_base_validation_errors()
