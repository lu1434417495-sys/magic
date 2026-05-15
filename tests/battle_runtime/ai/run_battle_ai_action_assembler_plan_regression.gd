extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BATTLE_AI_ACTION_ASSEMBLER_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_action_assembler.gd")
const ENEMY_AI_BRAIN_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_brain_def.gd")
const ENEMY_AI_STATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_state_def.gd")
const ENEMY_AI_GENERATION_SLOT_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_generation_slot_def.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/skill_def.gd")
const COMBAT_SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/combat_skill_def.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const USE_UNIT_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_unit_skill_action.gd")
const USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_random_chain_skill_action.gd")
const MOVE_TO_RANGE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/move_to_range_action.gd")

var _test := TestRunner.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_assembler_returns_runtime_plan_without_mutating_state()
	_test_generation_is_slot_family_scoped_not_global_skill_suppressed()
	_test_generated_metadata_contains_stable_runtime_identity()
	_test.finish(self, "Battle AI action assembler plan regression")


func _test_assembler_returns_runtime_plan_without_mutating_state() -> void:
	var fixture := _build_fixture()
	var original_action_count: int = fixture.state_def.get_actions().size()
	var plan = fixture.assembler.build_unit_action_plan(fixture.unit, fixture.brain, fixture.skill_defs)
	var actions: Array = plan.get_actions(&"engage")
	_test.assert_true(plan.has_state(&"engage"), "assembler 应为 brain state 创建 plan state。")
	_test.assert_true(actions.size() > original_action_count, "runtime plan 应包含 authored + generated actions。")
	_test.assert_eq(fixture.state_def.get_actions().size(), original_action_count, "assembler 不应把 generated action 写回 state resource。")


func _test_generation_is_slot_family_scoped_not_global_skill_suppressed() -> void:
	var fixture := _build_fixture()
	var plan = fixture.assembler.build_unit_action_plan(fixture.unit, fixture.brain, fixture.skill_defs)
	var actions: Array = plan.get_actions(&"engage")
	_test.assert_true(_has_action_script_for_skill(actions, USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT, &"chain_arc"), "random_chain 技能应生成 use_random_chain_skill action。")
	_test.assert_true(_has_move_action_for_skill(actions, &"chain_arc"), "同一 random_chain 技能还应生成 move_to_range companion action，不能被全局 skill suppression 吃掉。")


func _test_generated_metadata_contains_stable_runtime_identity() -> void:
	var fixture := _build_fixture()
	var plan = fixture.assembler.build_unit_action_plan(fixture.unit, fixture.brain, fixture.skill_defs)
	for action in plan.get_actions(&"engage"):
		var metadata: Dictionary = plan.get_action_metadata(action)
		if not bool(metadata.get("generated", false)):
			continue
		if ProgressionDataUtils.to_string_name(metadata.get("skill_id", "")) != &"bolt":
			continue
		_test.assert_eq(metadata.get("state_id", &""), &"engage", "generated metadata 应包含 state_id。")
		_test.assert_eq(metadata.get("slot_id", &""), &"offense", "generated metadata 应包含 slot_id。")
		_test.assert_eq(metadata.get("action_family", &""), &"use_unit_skill", "generated metadata 应包含 action_family。")
		_test.assert_eq(action.score_bucket_id, &"harrier_pressure", "slot score_bucket_id 应覆盖生成 action 的评分桶。")
		return
	_test.fail("未找到 bolt 的 generated action metadata。")


func _build_fixture() -> Dictionary:
	var state_def = ENEMY_AI_STATE_DEF_SCRIPT.new()
	state_def.state_id = &"engage"
	var unit_template = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	unit_template.action_id = &"template_unit"
	unit_template.score_bucket_id = &"frontline_pressure"
	unit_template.target_selector = &"nearest_enemy"
	var move_template = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	move_template.action_id = &"template_move"
	move_template.score_bucket_id = &"archer_survival"
	move_template.target_selector = &"nearest_enemy"
	state_def.actions = [unit_template, move_template]
	state_def.generation_slots = [
		_slot(&"offense", 10, [&"unit_hostile.damage"], [&"use_unit_skill"], &"template_unit", &"harrier_pressure"),
		_slot(&"chain_cast", 20, [&"random_chain"], [&"use_random_chain_skill"], &"template_unit", &"frontline_pressure"),
		_slot(&"chain_move", 30, [&"random_chain"], [&"move_to_range"], &"template_move", &"archer_survival"),
	]
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"plan_brain"
	brain.default_state_id = &"engage"
	brain.states = [state_def]
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = &"actor"
	unit.ai_brain_id = brain.brain_id
	unit.known_active_skill_ids.append(&"bolt")
	unit.known_active_skill_ids.append(&"chain_arc")
	unit.known_skill_level_map = {&"bolt": 1, &"chain_arc": 1}
	return {
		"assembler": BATTLE_AI_ACTION_ASSEMBLER_SCRIPT.new(),
		"brain": brain,
		"state_def": state_def,
		"unit": unit,
		"skill_defs": {
			&"bolt": _skill(&"bolt", &"unit", &"enemy", &"damage"),
			&"chain_arc": _chain_skill(),
		},
	}


func _slot(slot_id: StringName, order: int, affordances: Array, families: Array, template_action_id: StringName, bucket_id: StringName):
	var slot = ENEMY_AI_GENERATION_SLOT_DEF_SCRIPT.new()
	slot.slot_id = slot_id
	slot.order = order
	for affordance in affordances:
		slot.allowed_affordances.append(ProgressionDataUtils.to_string_name(affordance))
	for family in families:
		slot.action_families.append(ProgressionDataUtils.to_string_name(family))
	slot.style_template_action_id = template_action_id
	slot.score_bucket_id = bucket_id
	slot.target_selector = &"nearest_enemy"
	return slot


func _skill(skill_id: StringName, target_mode: StringName, target_filter: StringName, effect_type: StringName):
	var skill = SKILL_DEF_SCRIPT.new()
	skill.skill_id = skill_id
	skill.display_name = String(skill_id)
	skill.skill_type = &"active"
	var combat = COMBAT_SKILL_DEF_SCRIPT.new()
	combat.target_mode = target_mode
	combat.target_team_filter = target_filter
	combat.range_pattern = &"fixed"
	combat.range_value = 4
	combat.effect_defs.append(_effect(effect_type))
	skill.combat_profile = combat
	return skill


func _chain_skill():
	var skill = _skill(&"chain_arc", &"unit", &"enemy", &"chain_damage")
	skill.combat_profile.target_selection_mode = &"random_chain"
	skill.combat_profile.max_hits_per_target = 2
	return skill


func _effect(effect_type: StringName):
	var effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect.effect_type = effect_type
	return effect


func _has_action_script_for_skill(actions: Array, script_resource, skill_id: StringName) -> bool:
	for action in actions:
		if action != null and action.get_script() == script_resource and action.get_declared_skill_ids().has(skill_id):
			return true
	return false


func _has_move_action_for_skill(actions: Array, skill_id: StringName) -> bool:
	for action in actions:
		if action != null and action.get_script() == MOVE_TO_RANGE_ACTION_SCRIPT and action.range_skill_ids.has(skill_id):
			return true
	return false
