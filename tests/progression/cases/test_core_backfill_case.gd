# 测试内容：验证核心技能数量不足时，非核心满级技能可补位成为某职业的核心技能。
# 输入：
# - 职业 `warrior` 当前等级为 2，名下已有 1 个核心技能 `basic_sword`
# - 非核心满级技能 `guard_stance`，标签为 `melee`
# - 玩家总人物等级为 2，当前全局核心技能数为 1
# 输出：
# - `guard_stance` 可通过补位规则晋升为核心技能
# - `guard_stance` 会挂接到 `warrior`
# - `warrior.core_skill_ids` 数量增加到 2
extends "res://tests/progression/helpers/progression_test_case.gd"

const FIXTURES = preload("res://tests/progression/helpers/progression_test_fixtures.gd")


func get_case_name() -> String:
	return "Core Backfill Case"


func run(failures: Array[String]) -> void:
	var basic_sword := FIXTURES.create_skill_def(
		&"basic_sword",
		2,
		PackedInt32Array([10, 20]),
		[&"melee"]
	)
	var guard_stance := FIXTURES.create_skill_def(
		&"guard_stance",
		2,
		PackedInt32Array([10, 20]),
		[&"melee"]
	)
	var warrior := FIXTURES.create_profession_def(
		&"warrior",
		3,
		FIXTURES.create_unlock_requirement([], [FIXTURES.create_tag_requirement(&"melee", 1)]),
		[
			FIXTURES.create_rank_requirement(2, [FIXTURES.create_tag_requirement(&"melee", 2)]),
			FIXTURES.create_rank_requirement(3, [FIXTURES.create_tag_requirement(&"melee", 3)]),
		]
	)

	var player_progress := FIXTURES.create_player_progress()
	var sword_progress := FIXTURES.create_skill_progress(&"basic_sword", true, 2, true, &"warrior")
	var guard_progress := FIXTURES.create_skill_progress(&"guard_stance", true, 2, false)
	player_progress.set_skill_progress(sword_progress)
	player_progress.set_skill_progress(guard_progress)

	var warrior_progress := FIXTURES.create_profession_progress(&"warrior", 2, [&"basic_sword"])
	player_progress.set_profession_progress(warrior_progress)

	var services := FIXTURES.create_services(player_progress, [basic_sword, guard_stance], [warrior])
	var progression = services.get("progression") as ProgressionService
	var assignment = services.get("assignment") as ProfessionAssignmentService
	progression.recalculate_character_level()

	assert_equal(failures, player_progress.character_level, 2, "测试前人物等级应由战士 2 级计算为 2")
	assert_equal(failures, player_progress.active_core_skill_ids.size(), 1, "测试前应只有 1 个核心技能")
	assert_true(
		failures,
		assignment.can_promote_non_core_to_core(&"guard_stance", &"warrior"),
		"满足补位条件时应允许把非核心技能补为战士核心技能"
	)
	assert_true(
		failures,
		assignment.promote_non_core_to_core(&"guard_stance", &"warrior"),
		"执行补位应成功"
	)

	assert_true(failures, guard_progress.is_core, "补位后技能应转为核心技能")
	assert_equal(failures, guard_progress.assigned_profession_id, &"warrior", "补位后技能应挂接到战士")
	assert_equal(failures, warrior_progress.core_skill_ids.size(), 2, "战士名下核心技能数应补齐到 2")
	assert_has(failures, warrior_progress.core_skill_ids, &"guard_stance", "补位技能应出现在战士核心技能列表")
