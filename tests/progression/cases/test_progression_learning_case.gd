# 测试内容：验证普通技能学习、职业技能学习限制、熟练度升级和满级封顶。
# 输入：
# - 普通技能 `fireball`，`max_level=3`，`mastery_curve=[10, 20, 30]`
# - 职业技能 `warrior_shout`，`learn_source=profession`
# - 玩家初始无已学技能
# 输出：
# - `fireball` 可学习，重复学习失败
# - `warrior_shout` 不能通过 `learn_skill` 学习
# - 熟练度累计后 `fireball` 按阈值升级，并在满级时停止继续积累当前等级熟练度
extends "res://tests/progression/helpers/progression_test_case.gd"

const FIXTURES = preload("res://tests/progression/helpers/progression_test_fixtures.gd")


func get_case_name() -> String:
	return "Progression Learning Case"


func run(failures: Array[String]) -> void:
	var fireball := FIXTURES.create_skill_def(
		&"fireball",
		3,
		PackedInt32Array([10, 20, 30]),
		[&"magic"],
		&"book"
	)
	var warrior_shout := FIXTURES.create_skill_def(
		&"warrior_shout",
		1,
		PackedInt32Array([10]),
		[&"battlecry"],
		&"profession"
	)

	var player_progress := FIXTURES.create_player_progress()
	var services := FIXTURES.create_services(player_progress, [fireball, warrior_shout], [])
	var progression = services.get("progression") as ProgressionService

	assert_true(failures, progression.learn_skill(&"fireball"), "普通技能应可通过 learn_skill 学习")
	assert_false(failures, progression.learn_skill(&"fireball"), "重复学习同一个普通技能应失败")
	assert_false(failures, progression.learn_skill(&"warrior_shout"), "职业技能不应通过 learn_skill 学习")

	assert_true(failures, progression.grant_skill_mastery(&"fireball", 15, &"training"), "应可通过训练增加熟练度")
	var fireball_progress := player_progress.get_skill_progress(&"fireball")
	assert_equal(failures, fireball_progress.skill_level, 1, "第一次训练后火球术应升到 1 级")
	assert_equal(failures, fireball_progress.current_mastery, 5, "第一次训练后应保留剩余熟练度")
	assert_equal(failures, fireball_progress.mastery_from_training, 15, "训练熟练度应累计")

	assert_true(failures, progression.grant_skill_mastery(&"fireball", 40, &"battle"), "应可通过战斗增加熟练度")
	assert_equal(failures, fireball_progress.skill_level, 2, "第二次增长后火球术应升到 2 级")
	assert_equal(failures, fireball_progress.current_mastery, 25, "第二次增长后应保留当前等级熟练度")
	assert_equal(failures, fireball_progress.mastery_from_battle, 40, "战斗熟练度应累计")

	assert_true(failures, progression.grant_skill_mastery(&"fireball", 10, &"battle"), "达到最后一级所需熟练度后应完成满级")
	assert_equal(failures, fireball_progress.skill_level, 3, "火球术应封顶到最大等级")
	assert_equal(failures, fireball_progress.current_mastery, 0, "满级后当前熟练度应清零")
	assert_equal(failures, fireball_progress.total_mastery_earned, 65, "总熟练度应保留完整累计值")
