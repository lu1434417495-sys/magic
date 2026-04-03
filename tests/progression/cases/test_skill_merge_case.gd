# 测试内容：验证核心技能合并后的职业解绑、结果技能重挂接、来源追溯和禁止重学。
# 输入：
# - 战士职业已有 3 个核心技能：`slash_a`、`slash_b`、`slash_d`
# - 首次合并：`slash_a + slash_b => slash_c`
# - 二次合并：`slash_c + slash_d => slash_e`
# 输出：
# - 被吞并技能会从职业核心列表中移除，并进入禁止重学列表
# - 合并结果技能会挂接回战士职业
# - 查询 `slash_e` 的递归来源时返回 `slash_a, slash_b, slash_c, slash_d`
# - 递归来源结果不包含 `slash_e` 自身
extends "res://tests/progression/helpers/progression_test_case.gd"

const FIXTURES = preload("res://tests/progression/helpers/progression_test_fixtures.gd")


func get_case_name() -> String:
	return "Skill Merge Case"


func run(failures: Array[String]) -> void:
	var slash_a := FIXTURES.create_skill_def(&"slash_a", 2, PackedInt32Array([10, 20]), [&"melee"])
	var slash_b := FIXTURES.create_skill_def(&"slash_b", 2, PackedInt32Array([10, 20]), [&"melee"])
	var slash_c := FIXTURES.create_skill_def(&"slash_c", 2, PackedInt32Array([10, 20]), [&"melee"])
	var slash_d := FIXTURES.create_skill_def(&"slash_d", 2, PackedInt32Array([10, 20]), [&"melee"])
	var slash_e := FIXTURES.create_skill_def(&"slash_e", 2, PackedInt32Array([10, 20]), [&"melee"])
	var warrior := FIXTURES.create_profession_def(
		&"warrior",
		3,
		FIXTURES.create_unlock_requirement([], [FIXTURES.create_tag_requirement(&"melee", 1)])
	)

	var player_progress := FIXTURES.create_player_progress()
	player_progress.set_skill_progress(FIXTURES.create_skill_progress(&"slash_a", true, 2, true, &"warrior"))
	player_progress.set_skill_progress(FIXTURES.create_skill_progress(&"slash_b", true, 2, true, &"warrior"))
	player_progress.set_skill_progress(FIXTURES.create_skill_progress(&"slash_d", true, 2, true, &"warrior"))
	player_progress.set_profession_progress(
		FIXTURES.create_profession_progress(&"warrior", 1, [&"slash_a", &"slash_b", &"slash_d"])
	)

	var services := FIXTURES.create_services(
		player_progress,
		[slash_a, slash_b, slash_c, slash_d, slash_e],
		[warrior]
	)
	var merge = services.get("merge") as SkillMergeService

	assert_true(
		failures,
		merge.merge_skills([&"slash_a", &"slash_b"], &"slash_c", true, &"warrior"),
		"第一次核心技能合并应成功"
	)
	var warrior_progress := player_progress.get_profession_progress(&"warrior")
	assert_true(failures, player_progress.get_skill_progress(&"slash_a") == null, "被吞并技能 slash_a 应从当前技能表移除")
	assert_true(failures, player_progress.get_skill_progress(&"slash_b") == null, "被吞并技能 slash_b 应从当前技能表移除")
	assert_true(failures, player_progress.is_skill_relearn_blocked(&"slash_a"), "slash_a 应进入禁止重学列表")
	assert_true(failures, player_progress.is_skill_relearn_blocked(&"slash_b"), "slash_b 应进入禁止重学列表")
	assert_has(failures, warrior_progress.core_skill_ids, &"slash_c", "合并结果 slash_c 应重新挂接到战士")

	assert_true(
		failures,
		merge.merge_skills([&"slash_c", &"slash_d"], &"slash_e", true, &"warrior"),
		"第二次核心技能合并应成功"
	)
	assert_true(failures, player_progress.is_skill_relearn_blocked(&"slash_c"), "二次合并后 slash_c 应进入禁止重学列表")
	assert_true(failures, player_progress.is_skill_relearn_blocked(&"slash_d"), "二次合并后 slash_d 应进入禁止重学列表")
	assert_has(failures, warrior_progress.core_skill_ids, &"slash_e", "最终结果 slash_e 应挂接到战士")
	assert_equal(failures, warrior_progress.core_skill_ids.size(), 1, "二次合并后战士核心技能列表应只保留最终技能")

	var recursive_sources := merge.get_merged_source_skill_ids_recursive(&"slash_e")
	assert_equal(
		failures,
		recursive_sources,
		[&"slash_a", &"slash_b", &"slash_c", &"slash_d"],
		"递归来源应按稳定顺序返回完整来源链"
	)
	assert_false(failures, recursive_sources.has(&"slash_e"), "递归来源结果不应包含当前查询技能本身")
