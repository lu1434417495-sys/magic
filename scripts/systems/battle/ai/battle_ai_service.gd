## 文件说明：该脚本属于战斗自动决策服务相关的服务脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleAiService
extends RefCounted

const BATTLE_AI_DECISION_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_decision.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BATTLE_AI_SCORE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_service.gd")
const BATTLE_AI_SCORE_PROFILE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_profile.gd")
const BATTLE_AI_MUTATION_GUARD_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_mutation_guard.gd")
const BATTLE_AI_STATE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_state_resolver.gd")
const AI_TRACE_RECORDER = preload("res://scripts/dev_tools/ai_trace_recorder.gd")
const BattleAiDecision = preload("res://scripts/systems/battle/ai/battle_ai_decision.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleGridService = preload("res://scripts/systems/battle/terrain/battle_grid_service.gd")
const BattleDamageResolver = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BattleAiScoreService = preload("res://scripts/systems/battle/ai/battle_ai_score_service.gd")
const BattleAiScoreProfile = preload("res://scripts/systems/battle/ai/battle_ai_score_profile.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

var _enemy_ai_brains: Dictionary = {}
var _score_service: BattleAiScoreService = BATTLE_AI_SCORE_SERVICE_SCRIPT.new()
var _state_resolver = BATTLE_AI_STATE_RESOLVER_SCRIPT.new()
## 字段说明：是否启用 AI mutation guard。
## - true（默认）：每次 choose_command 都 capture/validate，越权写入直接 assert 中断，强迫修复根因。
## - false：跳过 guard，AI 决策吞吐回到 baseline。供 balance / AI-vs-AI 大规模 sim runner 显式关闭。
var enable_mutation_guard: bool = true


func setup(enemy_ai_brains: Dictionary = {}, damage_resolver: BattleDamageResolver = null) -> void:
	_enemy_ai_brains = enemy_ai_brains if enemy_ai_brains != null else {}
	_score_service.setup(damage_resolver)


func set_score_profile(profile: BattleAiScoreProfile) -> void:
	_score_service.set_profile(profile if profile != null else BATTLE_AI_SCORE_PROFILE_SCRIPT.new())


func get_score_profile() -> BattleAiScoreProfile:
	return _score_service.get_profile()


func choose_command(context) -> BattleAiDecision:
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return null
	if context.has_method("get") and context.get("mutation_guard_violations") is Array:
		context.mutation_guard_violations.clear()
	# enable_mutation_guard=false 时完全跳过 capture/validate；大规模 sim 走这条路径换吞吐。
	if not enable_mutation_guard:
		AI_TRACE_RECORDER.enter(&"choose:impl")
		var decision_no_guard := _choose_command_impl(context)
		AI_TRACE_RECORDER.exit(&"choose:impl")
		return decision_no_guard
	var mutation_guard = BATTLE_AI_MUTATION_GUARD_SCRIPT.new()
	AI_TRACE_RECORDER.enter(&"choose:mutation_guard_capture")
	mutation_guard.capture(context)
	AI_TRACE_RECORDER.exit(&"choose:mutation_guard_capture")
	AI_TRACE_RECORDER.enter(&"choose:impl")
	var decision := _choose_command_impl(context)
	AI_TRACE_RECORDER.exit(&"choose:impl")
	AI_TRACE_RECORDER.enter(&"choose:mutation_guard_validate")
	var violations: Array[String] = mutation_guard.validate_and_restore(context)
	AI_TRACE_RECORDER.exit(&"choose:mutation_guard_validate")
	if violations.is_empty():
		return decision
	context.mutation_guard_violations = violations.duplicate()
	for violation in violations:
		push_error("AI mutation guard blocked decision: %s" % violation)
	# 开关打开时检测到 mutation 必须立即终止——AI 决策路径偷偷写 state 是契约级 bug，
	# 必须看到、必须修，不能靠 wait fallback 蒙混过去。
	# assert 走 debug build 的 breakpoint；OS.crash 兜底 release build（assert 被剥离也能崩）。
	var unit_label: String = String(context.unit_state.display_name) if context.unit_state != null else "unknown"
	var crash_message: String = "AI mutation guard blocked %s 的决策；越权写入：%s" % [unit_label, "; ".join(violations)]
	assert(false, crash_message)
	OS.crash(crash_message)
	return null


func _choose_command_impl(context) -> BattleAiDecision:
	if not context.skill_score_input_callback.is_valid():
		context.skill_score_input_callback = Callable(self, "build_skill_score_input")
	if not context.action_score_input_callback.is_valid():
		context.action_score_input_callback = Callable(self, "build_action_score_input")

	var unit_state: BattleUnitState = context.unit_state
	var brain = _resolve_brain(unit_state.ai_brain_id)
	if brain == null:
		var missing_brain_decision = _build_wait_decision(
			context,
			&"",
			&"",
			&"wait_missing_brain",
			"%s 缺少正式 AI brain，改为待机。" % unit_state.display_name
		)
		_commit_decision(unit_state, missing_brain_decision)
		if context.has_method("mark_action_trace_chosen"):
			context.mark_action_trace_chosen(missing_brain_decision.action_trace_id, missing_brain_decision)
		return missing_brain_decision

	unit_state.ai_brain_id = brain.brain_id
	var transition_result: Dictionary = _state_resolver.resolve(context, brain)
	var next_state_id: StringName = ProgressionDataUtils.to_string_name(transition_result.get("state_id", brain.default_state_id))
	unit_state.ai_state_id = next_state_id
	var state_def = brain.get_state(next_state_id)
	if state_def == null:
		var missing_state_decision = _build_wait_decision(
			context,
			brain.brain_id,
			next_state_id,
			&"wait_missing_state",
			"%s 找不到 AI 状态 %s，改为待机。" % [unit_state.display_name, String(next_state_id)]
		)
		_prepare_decision(missing_state_decision, brain.brain_id, next_state_id, transition_result)
		_commit_decision(unit_state, missing_state_decision)
		if context.has_method("mark_action_trace_chosen"):
			context.mark_action_trace_chosen(missing_state_decision.action_trace_id, missing_state_decision)
		return missing_state_decision

	var best_scored_decision: BattleAiDecision = null
	var best_scored_action_index := 999999
	var fallback_decision: BattleAiDecision = null
	var action_resolution := _resolve_runtime_actions(context, brain, next_state_id, state_def)
	if action_resolution.get("wait_action_id", &"") != &"":
		var runtime_wait_decision = _build_wait_decision(
			context,
			brain.brain_id,
			next_state_id,
			ProgressionDataUtils.to_string_name(action_resolution.get("wait_action_id", &"")),
			String(action_resolution.get("wait_reason_text", ""))
		)
		_prepare_decision(runtime_wait_decision, brain.brain_id, next_state_id, transition_result)
		_commit_decision(unit_state, runtime_wait_decision)
		if context.has_method("mark_action_trace_chosen"):
			context.mark_action_trace_chosen(runtime_wait_decision.action_trace_id, runtime_wait_decision)
		return runtime_wait_decision
	var actions: Array = action_resolution.get("actions", [])
	for action_index in range(actions.size()):
		var action = actions[action_index]
		if action == null or not action.has_method("decide"):
			continue
		var action_metadata: Dictionary = context.get_runtime_action_metadata(action) if context.has_method("get_runtime_action_metadata") else {}
		if context.has_method("push_action_metadata"):
			context.push_action_metadata(action_metadata)
		var decision = action.decide(context) as BattleAiDecision
		if context.has_method("pop_action_metadata"):
			context.pop_action_metadata()
		if decision == null or decision.command == null:
			continue
		_prepare_decision(decision, brain.brain_id, next_state_id, transition_result)
		_apply_action_metadata_to_decision(decision, action_metadata)
		if _get_decision_score_input(decision) != null:
			if _should_replace_scored_decision(decision, action_index, best_scored_decision, best_scored_action_index):
				best_scored_decision = decision
				best_scored_action_index = action_index
			continue
		if fallback_decision == null:
			fallback_decision = decision

	var resolved_decision := best_scored_decision if best_scored_decision != null else fallback_decision
	if resolved_decision != null:
		_commit_decision(unit_state, resolved_decision)
		if context.has_method("mark_action_trace_chosen"):
			context.mark_action_trace_chosen(resolved_decision.action_trace_id, resolved_decision)
		return resolved_decision

	var wait_decision = _build_wait_decision(
		context,
		brain.brain_id,
		next_state_id,
		&"wait_fallback",
		"%s 在状态 %s 下没有找到合法指令，改为待机。" % [unit_state.display_name, String(next_state_id)]
	)
	_prepare_decision(wait_decision, brain.brain_id, next_state_id, transition_result)
	_commit_decision(unit_state, wait_decision)
	if context.has_method("mark_action_trace_chosen"):
		context.mark_action_trace_chosen(wait_decision.action_trace_id, wait_decision)
	return wait_decision


func build_skill_score_input(context, skill_def: SkillDef, command, preview, effect_defs: Array = [], metadata: Dictionary = {}):
	return _score_service.build_skill_score_input(context, skill_def, command, preview, effect_defs, metadata)


func build_action_score_input(
	context,
	action_kind: StringName,
	action_label: String,
	score_bucket_id: StringName,
	command,
	preview,
	metadata: Dictionary = {}
):
	return _score_service.build_action_score_input(
		context,
		action_kind,
		action_label,
		score_bucket_id,
		command,
		preview,
		metadata
	)


func _prepare_decision(
	decision: BattleAiDecision,
	brain_id: StringName,
	state_id: StringName,
	transition_result: Dictionary = {}
) -> void:
	if decision == null:
		return
	decision.brain_id = brain_id
	decision.state_id = state_id
	decision.transition = transition_result.duplicate(true) if transition_result is Dictionary else {}
	if decision.action_id == &"":
		decision.action_id = &"anonymous_action"
	var score_input = _get_decision_score_input(decision)
	if decision.score_bucket_id == &"" and score_input != null:
		decision.score_bucket_id = score_input.score_bucket_id


func _resolve_runtime_actions(context, brain, state_id: StringName, state_def) -> Dictionary:
	if context == null:
		return {"actions": []}
	var has_runtime_plan := context.runtime_action_plan != null
	if has_runtime_plan:
		if context.has_method("is_runtime_action_plan_stale") and context.is_runtime_action_plan_stale(brain):
			return {
				"wait_action_id": &"wait_stale_runtime_plan",
				"wait_reason_text": "%s 的 AI runtime plan 已过期，改为待机。" % context.unit_state.display_name,
			}
		if not context.has_method("has_runtime_action_state") or not context.has_runtime_action_state(state_id):
			return {
				"wait_action_id": &"wait_missing_runtime_plan",
				"wait_reason_text": "%s 缺少状态 %s 的 AI runtime plan，改为待机。" % [context.unit_state.display_name, String(state_id)],
			}
		var runtime_actions: Array = context.get_runtime_actions(state_id) if context.has_method("get_runtime_actions") else []
		if runtime_actions.is_empty():
			return {
				"wait_action_id": &"wait_empty_runtime_state",
				"wait_reason_text": "%s 的 AI runtime state %s 没有可评估 action，改为待机。" % [context.unit_state.display_name, String(state_id)],
			}
		return {"actions": runtime_actions}
	if bool(context.allow_authored_action_fallback_for_tests):
		return {"actions": state_def.get_actions()}
	return {
		"wait_action_id": &"wait_missing_runtime_plan",
		"wait_reason_text": "%s 缺少 AI runtime plan，改为待机。" % context.unit_state.display_name,
	}


func _apply_action_metadata_to_decision(decision: BattleAiDecision, metadata: Dictionary) -> void:
	if decision == null or metadata.is_empty():
		return
	var metadata_bucket_id := ProgressionDataUtils.to_string_name(metadata.get("score_bucket_id", &""))
	if metadata_bucket_id != &"":
		decision.score_bucket_id = metadata_bucket_id
	var score_input = _get_decision_score_input(decision)
	if score_input != null:
		if metadata_bucket_id != &"":
			score_input.score_bucket_id = metadata_bucket_id
			score_input.score_bucket_priority = _score_service.get_bucket_priority(metadata_bucket_id)
		if score_input.runtime_action_metadata.is_empty() and metadata.get("runtime_action_metadata", {}) is Dictionary:
			score_input.runtime_action_metadata = (metadata.get("runtime_action_metadata", {}) as Dictionary).duplicate(true)


func _should_replace_scored_decision(
	candidate: BattleAiDecision,
	candidate_action_index: int,
	best_candidate: BattleAiDecision,
	best_action_index: int
) -> bool:
	var candidate_score = _get_decision_score_input(candidate)
	if candidate_score == null:
		return false
	var best_score = _get_decision_score_input(best_candidate)
	if best_score == null:
		return true
	if _is_better_score_input(candidate_score, best_score):
		return true
	if _is_better_score_input(best_score, candidate_score):
		return false
	return candidate_action_index < best_action_index


func _is_better_score_input(candidate, best_candidate) -> bool:
	if candidate == null:
		return false
	if best_candidate == null:
		return true
	if int(candidate.estimated_friendly_lethal_target_count) != int(best_candidate.estimated_friendly_lethal_target_count):
		return int(candidate.estimated_friendly_lethal_target_count) < int(best_candidate.estimated_friendly_lethal_target_count)
	if int(candidate.estimated_friendly_fire_target_count) != int(best_candidate.estimated_friendly_fire_target_count):
		return int(candidate.estimated_friendly_fire_target_count) < int(best_candidate.estimated_friendly_fire_target_count)
	if int(candidate.friendly_fire_penalty_score) != int(best_candidate.friendly_fire_penalty_score):
		return int(candidate.friendly_fire_penalty_score) < int(best_candidate.friendly_fire_penalty_score)
	var survival_risk_comparison := _compare_post_action_survival_risk(candidate, best_candidate)
	if survival_risk_comparison != 0:
		return survival_risk_comparison > 0
	if int(candidate.estimated_lethal_threat_target_count) != int(best_candidate.estimated_lethal_threat_target_count):
		return int(candidate.estimated_lethal_threat_target_count) > int(best_candidate.estimated_lethal_threat_target_count)
	if int(candidate.estimated_lethal_target_count) != int(best_candidate.estimated_lethal_target_count):
		return int(candidate.estimated_lethal_target_count) > int(best_candidate.estimated_lethal_target_count)
	var candidate_is_emergency_survival := _is_emergency_survival_score_input(candidate)
	var best_is_emergency_survival := _is_emergency_survival_score_input(best_candidate)
	if candidate_is_emergency_survival != best_is_emergency_survival:
		return candidate_is_emergency_survival
	if int(candidate.estimated_lethal_target_count) > 0 and int(best_candidate.estimated_lethal_target_count) > 0:
		if int(candidate.total_score) != int(best_candidate.total_score):
			return int(candidate.total_score) > int(best_candidate.total_score)
		if int(candidate.hit_payoff_score) != int(best_candidate.hit_payoff_score):
			return int(candidate.hit_payoff_score) > int(best_candidate.hit_payoff_score)
		if int(candidate.effective_target_count) != int(best_candidate.effective_target_count):
			return int(candidate.effective_target_count) > int(best_candidate.effective_target_count)
		var lethal_nonfatal_risk_comparison := _compare_nonfatal_post_action_survival_risk(candidate, best_candidate)
		if lethal_nonfatal_risk_comparison != 0:
			return lethal_nonfatal_risk_comparison > 0
		if int(candidate.resource_cost_score) != int(best_candidate.resource_cost_score):
			return int(candidate.resource_cost_score) < int(best_candidate.resource_cost_score)
	if int(candidate.score_bucket_priority) != int(best_candidate.score_bucket_priority):
		return int(candidate.score_bucket_priority) > int(best_candidate.score_bucket_priority)
	if int(candidate.total_score) != int(best_candidate.total_score):
		return int(candidate.total_score) > int(best_candidate.total_score)
	if int(candidate.hit_payoff_score) != int(best_candidate.hit_payoff_score):
		return int(candidate.hit_payoff_score) > int(best_candidate.hit_payoff_score)
	if int(candidate.effective_target_count) != int(best_candidate.effective_target_count):
		return int(candidate.effective_target_count) > int(best_candidate.effective_target_count)
	if int(candidate.target_count) != int(best_candidate.target_count):
		return int(candidate.target_count) > int(best_candidate.target_count)
	var nonfatal_risk_comparison := _compare_nonfatal_post_action_survival_risk(candidate, best_candidate)
	if nonfatal_risk_comparison != 0:
		return nonfatal_risk_comparison > 0
	if int(candidate.position_objective_score) != int(best_candidate.position_objective_score):
		return int(candidate.position_objective_score) > int(best_candidate.position_objective_score)
	return int(candidate.resource_cost_score) < int(best_candidate.resource_cost_score)


func _is_emergency_survival_score_input(score_input) -> bool:
	if score_input == null:
		return false
	if score_input.score_bucket_id != &"archer_survival":
		return false
	if bool(score_input.has_post_action_threat_projection):
		if bool(score_input.pre_action_is_lethal_survival_risk) and not bool(score_input.post_action_is_lethal_survival_risk):
			return true
		if int(score_input.pre_action_threat_expected_damage) > int(score_input.post_action_remaining_threat_expected_damage) \
				and int(score_input.post_action_survival_margin) >= 0:
			return true
	if int(score_input.target_count) > 0 or int(score_input.effective_target_count) > 0:
		return false
	if int(score_input.enemy_target_count) > 0 or int(score_input.ally_target_count) > 0:
		return false
	if int(score_input.estimated_damage) != 0 or int(score_input.estimated_control_count) != 0:
		return false
	if int(score_input.position_current_distance) >= 0 and int(score_input.position_safe_distance) > 0:
		var current_gap := int(score_input.position_safe_distance) - int(score_input.position_current_distance)
		if current_gap < 2:
			return false
		if int(score_input.distance_to_primary_coord) >= 0:
			return int(score_input.distance_to_primary_coord) >= int(score_input.position_safe_distance)
		return int(score_input.position_objective_score) > 0
	return int(score_input.position_objective_score) > 0


func _compare_post_action_survival_risk(candidate, best_candidate) -> int:
	if candidate == null or best_candidate == null:
		return 0
	if not bool(candidate.has_post_action_threat_projection) or not bool(best_candidate.has_post_action_threat_projection):
		return 0
	var candidate_fatal := bool(candidate.post_action_is_lethal_survival_risk)
	var best_fatal := bool(best_candidate.post_action_is_lethal_survival_risk)
	if candidate_fatal != best_fatal:
		return -1 if candidate_fatal else 1
	return 0


func _compare_nonfatal_post_action_survival_risk(candidate, best_candidate) -> int:
	if candidate == null or best_candidate == null:
		return 0
	if not bool(candidate.has_post_action_threat_projection) or not bool(best_candidate.has_post_action_threat_projection):
		return 0
	if bool(candidate.post_action_is_lethal_survival_risk) or bool(best_candidate.post_action_is_lethal_survival_risk):
		return 0
	var candidate_threat_free := int(candidate.post_action_remaining_threat_count) <= 0
	var best_threat_free := int(best_candidate.post_action_remaining_threat_count) <= 0
	if candidate_threat_free != best_threat_free:
		return 1 if candidate_threat_free else -1
	var candidate_damage := int(candidate.post_action_remaining_threat_expected_damage)
	var best_damage := int(best_candidate.post_action_remaining_threat_expected_damage)
	if candidate_damage != best_damage:
		return 1 if candidate_damage < best_damage else -1
	var candidate_count := int(candidate.post_action_remaining_threat_count)
	var best_count := int(best_candidate.post_action_remaining_threat_count)
	if candidate_count != best_count:
		return 1 if candidate_count < best_count else -1
	var candidate_margin := int(candidate.post_action_survival_margin)
	var best_margin := int(best_candidate.post_action_survival_margin)
	if candidate_margin != best_margin:
		return 1 if candidate_margin > best_margin else -1
	return 0


func _get_decision_score_input(decision: BattleAiDecision):
	if decision == null:
		return null
	if decision.score_input != null:
		return decision.score_input
	return decision.skill_score_input


func _resolve_brain(brain_id: StringName):
	if brain_id == &"":
		return null
	return _enemy_ai_brains.get(brain_id)


func _build_wait_decision(
	context,
	brain_id: StringName,
	state_id: StringName,
	action_id: StringName,
	reason_text: String
) -> BattleAiDecision:
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BattleCommand.TYPE_WAIT
	command.unit_id = context.unit_state.unit_id
	var decision = BATTLE_AI_DECISION_SCRIPT.new()
	decision.command = command
	decision.brain_id = brain_id
	decision.state_id = state_id
	decision.action_id = action_id
	decision.reason_text = reason_text
	return decision


func _commit_decision(unit_state: BattleUnitState, decision: BattleAiDecision) -> void:
	if unit_state == null or decision == null:
		return
	unit_state.ai_blackboard["last_brain_id"] = String(decision.brain_id)
	unit_state.ai_blackboard["last_state_id"] = String(decision.state_id)
	unit_state.ai_blackboard["last_action_id"] = String(decision.action_id)
	unit_state.ai_blackboard["last_reason_text"] = decision.reason_text
	if decision.transition is Dictionary and not decision.transition.is_empty():
		unit_state.ai_blackboard["last_transition_previous_state_id"] = String(decision.transition.get("previous_state_id", &""))
		unit_state.ai_blackboard["last_transition_state_id"] = String(decision.transition.get("state_id", &""))
		unit_state.ai_blackboard["last_transition_rule_id"] = String(decision.transition.get("rule_id", &""))
		unit_state.ai_blackboard["last_transition_reason"] = String(decision.transition.get("reason", &""))
	unit_state.ai_blackboard["turn_decision_count"] = int(unit_state.ai_blackboard.get("turn_decision_count", 0)) + 1
