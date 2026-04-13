class_name WaitAction
extends "res://scripts/enemies/enemy_ai_action.gd"


func decide(context):
	return _create_decision(
		_build_wait_command(context),
		"%s 没有更优动作，选择待机。" % [context.unit_state.display_name]
	)
