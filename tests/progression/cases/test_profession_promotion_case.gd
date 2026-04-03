# 测试内容：验证转职、非转职升级、职业技能授予、职业等级记录和人物等级重算。
# 输入：
# - `basic_sword` 与 `guard_stance` 两个 `melee` 标签技能，均可学习并升满
# - 职业 `warrior`：
#   - `0 -> 1` 需要 `basic_sword` 且 `strength >= 10`
#   - `1 -> 2` 需要 `melee x2`
#   - 1 级授予职业技能 `warrior_shout`
# - 玩家基础力量为 12
# 输出：
# - 玩家可先转职为 `warrior 1`
# - 转职时 `basic_sword` 自动挂接到战士职业
# - 战士 1 级时授予 `warrior_shout`
# - 将第二个核心技能挂接到战士后，可继续升到 `warrior 2`
# - 人物等级等于战士职业等级
extends "res://tests/progression/helpers/progression_test_case.gd"

const FIXTURES = preload("res://tests/progression/helpers/progression_test_fixtures.gd")


func get_case_name() -> String:
	return "Profession Promotion Case"


func run(failures: Array[String]) -> void:
	var basic_sword := FIXTURES.create_skill_def(
		&"basic_sword",
		2,
		PackedInt32Array([10, 20]),
		[&"melee"],
		&"book"
	)
	var guard_stance := FIXTURES.create_skill_def(
		&"guard_stance",
		2,
		PackedInt32Array([10, 20]),
		[&"melee"],
		&"book",
		[&"training", &"battle"],
		&"passive"
	)
	var warrior_shout := FIXTURES.create_skill_def(
		&"warrior_shout",
		1,
		PackedInt32Array([10]),
		[&"battlecry"],
		&"profession"
	)

	var warrior_unlock := FIXTURES.create_unlock_requirement(
		[&"basic_sword"],
		[FIXTURES.create_tag_requirement(&"melee", 1)],
		[],
		[FIXTURES.create_attribute_requirement(&"strength", 10, 999999)]
	)
	var warrior_rank_two := FIXTURES.create_rank_requirement(
		2,
		[FIXTURES.create_tag_requirement(&"melee", 2)]
	)
	var warrior := FIXTURES.create_profession_def(
		&"warrior",
		2,
		warrior_unlock,
		[warrior_rank_two],
		[FIXTURES.create_granted_skill(&"warrior_shout", 1, &"active")]
	)

	var base_attributes := FIXTURES.create_base_attributes(0, 12)
	var player_progress := FIXTURES.create_player_progress(base_attributes)
	var services := FIXTURES.create_services(player_progress, [basic_sword, guard_stance, warrior_shout], [warrior])
	var progression = services.get("progression") as ProgressionService
	var assignment = services.get("assignment") as ProfessionAssignmentService

	assert_true(failures, progression.learn_skill(&"basic_sword"), "应可学习基础剑术")
	assert_true(failures, progression.learn_skill(&"guard_stance"), "应可学习格挡姿态")
	assert_true(failures, progression.grant_skill_mastery(&"basic_sword", 30, &"training"), "应可将基础剑术升满")
	assert_true(failures, progression.grant_skill_mastery(&"guard_stance", 30, &"training"), "应可将格挡姿态升满")
	assert_true(failures, progression.set_skill_core(&"basic_sword", true), "基础剑术应可设为核心")
	assert_true(failures, progression.set_skill_core(&"guard_stance", true), "格挡姿态应可设为核心")

	assert_true(failures, progression.can_promote_profession(&"warrior"), "当前状态应可转职为战士")
	assert_true(failures, progression.promote_profession(&"warrior"), "执行转职应成功")

	var warrior_progress := player_progress.get_profession_progress(&"warrior")
	assert_equal(failures, warrior_progress.rank, 1, "转职后战士等级应为 1")
	assert_equal(failures, player_progress.character_level, 1, "人物等级应等于战士等级 1")
	assert_has(failures, warrior_progress.core_skill_ids, &"basic_sword", "转职后基础剑术应挂接到战士")
	assert_true(
		failures,
		player_progress.get_skill_progress(&"warrior_shout") != null and player_progress.get_skill_progress(&"warrior_shout").is_learned,
		"战士 1 级应授予职业技能 warrior_shout"
	)
	assert_equal(
		failures,
		player_progress.get_skill_progress(&"warrior_shout").profession_granted_by,
		&"warrior",
		"职业授予技能应记录来源职业"
	)

	assert_true(
		failures,
		assignment.assign_core_skill_to_profession(&"guard_stance", &"warrior"),
		"第二个核心技能应可挂接到战士"
	)
	assert_true(failures, progression.can_promote_profession(&"warrior"), "挂接第二个核心技能后应可升到战士 2 级")
	assert_true(failures, progression.promote_profession(&"warrior"), "战士 2 级升级应成功")

	assert_equal(failures, warrior_progress.rank, 2, "升级后战士等级应为 2")
	assert_equal(failures, player_progress.character_level, 2, "人物等级应重算为 2")
	assert_equal(failures, warrior_progress.promotion_history.size(), 2, "职业升级历史应记录两次成长")
	assert_equal(
		failures,
		warrior_progress.promotion_history[1].consumed_skill_ids.size(),
		2,
		"战士 2 级的标签条件应消耗两个核心技能"
	)
