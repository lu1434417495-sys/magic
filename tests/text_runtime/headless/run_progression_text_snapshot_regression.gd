extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GameTextSnapshotRenderer = preload("res://scripts/utils/game_text_snapshot_renderer.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_party_text_snapshot_renders_progression_state()

	if _failures.is_empty():
		print("Progression text snapshot regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Progression text snapshot regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_party_text_snapshot_renders_progression_state() -> void:
	var text_snapshot := GameTextSnapshotRenderer.render_full_snapshot({
		"party": {
			"gold": 0,
			"leader_member_id": "player_sword_01",
			"active_member_ids": ["player_sword_01"],
			"reserve_member_ids": [],
			"selected_member_id": "player_sword_01",
			"pending_reward_count": 0,
			"members": [
				{
					"member_id": "player_sword_01",
					"roster_role": "active",
					"is_leader": true,
					"current_hp": 14,
					"current_mp": 20,
					"current_aura": 2,
					"achievement_summary": {},
					"attributes": {"armor_class": 8},
					"equipment": [],
					"unlocked_combat_resource_ids": ["hp", "stamina", "mp", "aura"],
					"active_core_skill_ids": ["warrior_heavy_strike"],
					"active_level_trigger_core_skill_id": "warrior_heavy_strike",
					"locked_level_trigger_skill_ids": ["mage_blink"],
					"blocked_relearn_skill_ids": ["old_focus"],
					"skill_entries": [
						{
							"skill_id": "warrior_heavy_strike",
							"level": 3,
							"is_core": true,
							"assigned_profession_id": "warrior",
							"is_level_trigger_active": true,
							"is_level_trigger_locked": false,
							"core_max_growth_claimed": false,
						},
						{
							"skill_id": "mage_blink",
							"level": 1,
							"is_core": false,
							"assigned_profession_id": "",
							"is_level_trigger_active": false,
							"is_level_trigger_locked": true,
							"core_max_growth_claimed": true,
						},
					],
					"profession_entries": [
						{
							"profession_id": "warrior",
							"rank": 2,
							"is_active": true,
							"core_skill_ids": ["warrior_heavy_strike"],
							"granted_skill_ids": ["warrior_guard_break"],
						},
					],
				},
			],
		},
	})

	_assert_contains(
		text_snapshot,
		"member_progression=player_sword_01 | resources=hp stamina mp aura | aura=2 | active_core=warrior_heavy_strike | active_trigger=warrior_heavy_strike | locked_trigger=mage_blink | blocked_relearn=old_focus",
		"文本快照应渲染成员 progression 资源、核心和触发状态。"
	)
	_assert_contains(
		text_snapshot,
		"member_skill=player_sword_01 | warrior_heavy_strike | lv=3 | core=true | trigger_active=true | trigger_locked=false | growth_claimed=false | profession=warrior",
		"文本快照应渲染核心技能等级和 active trigger 状态。"
	)
	_assert_contains(
		text_snapshot,
		"member_skill=player_sword_01 | mage_blink | lv=1 | core=false | trigger_active=false | trigger_locked=true | growth_claimed=true | profession=",
		"文本快照应渲染 locked trigger 技能状态。"
	)
	_assert_contains(
		text_snapshot,
		"member_profession=player_sword_01 | warrior | rank=2 | active=true | core=warrior_heavy_strike | granted=warrior_guard_break",
		"文本快照应渲染职业 rank、核心位和授予技能。"
	)


func _assert_contains(text: String, fragment: String, message: String) -> void:
	if not text.contains(fragment):
		_test.fail("%s | missing=%s | text=%s" % [message, fragment, text])
