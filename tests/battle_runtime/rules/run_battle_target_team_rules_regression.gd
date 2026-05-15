extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BattleTargetTeamRules = preload("res://scripts/systems/battle/rules/battle_target_team_rules.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_canonical_filters_match_relative_to_source()
	_test_alias_and_unknown_filters_fail_closed()
	_test_effect_filter_empty_inherits_skill_filter()
	_test_madness_option_only_relaxes_canonical_team_filters()

	if _failures.is_empty():
		print("Battle target team rules regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Battle target team rules regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_canonical_filters_match_relative_to_source() -> void:
	var source := _make_unit(&"source", &"player")
	var ally := _make_unit(&"ally", &"player")
	var enemy := _make_unit(&"enemy", &"hostile")

	_assert_true(BattleTargetTeamRules.is_unit_valid_for_filter(source, enemy, &"enemy"), "enemy 应命中不同阵营单位。")
	_assert_true(not BattleTargetTeamRules.is_unit_valid_for_filter(source, ally, &"enemy"), "enemy 不应命中同阵营单位。")
	_assert_true(BattleTargetTeamRules.is_unit_valid_for_filter(source, ally, &"ally"), "ally 应命中同阵营单位。")
	_assert_true(BattleTargetTeamRules.is_unit_valid_for_filter(source, source, &"self"), "self 应只命中来源单位。")
	_assert_true(BattleTargetTeamRules.is_unit_valid_for_filter(source, enemy, &"any"), "any 应命中敌方单位。")
	_assert_true(BattleTargetTeamRules.is_unit_valid_for_filter(source, ally, &"any"), "any 应命中友方单位。")


func _test_alias_and_unknown_filters_fail_closed() -> void:
	var source := _make_unit(&"source", &"player")
	var ally := _make_unit(&"ally", &"player")
	var enemy := _make_unit(&"enemy", &"hostile")

	_assert_true(not BattleTargetTeamRules.is_unit_valid_for_filter(source, enemy, &"hostile"), "hostile 是 faction_id，不应作为 target filter 命中。")
	_assert_true(not BattleTargetTeamRules.is_unit_valid_for_filter(source, ally, &"friendly"), "friendly 不应作为 target filter 命中。")
	_assert_true(not BattleTargetTeamRules.is_unit_valid_for_filter(source, enemy, &"all"), "all 别名不应作为 target filter 命中。")
	_assert_true(not BattleTargetTeamRules.is_unit_valid_for_filter(source, enemy, &"enmey"), "未知 target filter 应 fail closed。")


func _test_effect_filter_empty_inherits_skill_filter() -> void:
	var skill_def := SkillDef.new()
	skill_def.skill_id = &"inherit_filter_skill"
	skill_def.combat_profile = CombatSkillDef.new()
	skill_def.combat_profile.skill_id = skill_def.skill_id
	skill_def.combat_profile.target_team_filter = &"enemy"

	var inherited_effect := CombatEffectDef.new()
	inherited_effect.effect_target_team_filter = &""
	var ally_effect := CombatEffectDef.new()
	ally_effect.effect_target_team_filter = &"ally"

	_assert_eq(BattleTargetTeamRules.resolve_effect_target_filter(skill_def, inherited_effect), &"enemy", "空 effect_target_team_filter 应继承 skill filter。")
	_assert_eq(BattleTargetTeamRules.resolve_effect_target_filter(skill_def, ally_effect), &"ally", "非空 effect_target_team_filter 应覆盖 skill filter。")
	_assert_eq(BattleTargetTeamRules.resolve_effect_target_filter(null, inherited_effect), &"", "缺少 skill filter 时空 effect filter 不应隐藏回退成 any。")


func _test_madness_option_only_relaxes_canonical_team_filters() -> void:
	var source := _make_unit(&"source", &"player")
	source.ai_blackboard["madness_target_any_team"] = true
	var ally := _make_unit(&"ally", &"player")
	var enemy := _make_unit(&"enemy", &"hostile")

	_assert_true(
		BattleTargetTeamRules.is_unit_valid_for_filter(source, ally, &"enemy", {"madness_target_any_team": true}),
		"madness_target_any_team 应允许 enemy/ally 队伍过滤命中任意非自身单位。"
	)
	_assert_true(
		BattleTargetTeamRules.is_unit_valid_for_filter(source, enemy, &"ally", {"madness_target_any_team": true}),
		"madness_target_any_team 应允许 ally 队伍过滤命中敌方单位。"
	)
	_assert_true(
		not BattleTargetTeamRules.is_unit_valid_for_filter(source, source, &"enemy", {"madness_target_any_team": true}),
		"madness_target_any_team 不应允许命中自己。"
	)
	_assert_true(
		not BattleTargetTeamRules.is_unit_valid_for_filter(source, ally, &"hostile", {"madness_target_any_team": true}),
		"madness_target_any_team 不应复活 hostile 这类别名。"
	)


func _make_unit(unit_id: StringName, faction_id: StringName) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.faction_id = faction_id
	unit.is_alive = true
	return unit


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
