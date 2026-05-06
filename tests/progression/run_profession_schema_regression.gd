extends SceneTree

const GameSession = preload("res://scripts/systems/persistence/game_session.gd")
const ProfessionContentRegistry = preload("res://scripts/player/progression/profession_content_registry.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")

const SEED_EXPECTATIONS := {
	&"warrior": {
		"display_name": "战士",
		"unlock_tag": &"melee",
		"hit_die_sides": 10,
		"rank_counts": [2, 3, 4, 5],
	},
	&"priest": {
		"display_name": "牧师",
		"unlock_tag": &"priest",
		"hit_die_sides": 8,
		"rank_counts": [2, 2, 3, 3],
	},
	&"rogue": {
		"display_name": "盗贼",
		"unlock_tag": &"rogue",
		"hit_die_sides": 8,
		"rank_counts": [2, 2, 3, 3],
	},
	&"berserker": {
		"display_name": "狂战士",
		"unlock_tag": &"berserker",
		"hit_die_sides": 12,
		"rank_counts": [2, 2, 3, 3],
	},
	&"paladin": {
		"display_name": "圣武士",
		"unlock_tag": &"paladin",
		"hit_die_sides": 10,
		"rank_counts": [2, 2, 3, 3],
	},
	&"mage": {
		"display_name": "法师",
		"unlock_tag": &"mage",
		"hit_die_sides": 6,
		"rank_counts": [2, 2, 3, 3],
	},
	&"archer": {
		"display_name": "弓箭手",
		"unlock_tag": &"archer",
		"hit_die_sides": 8,
		"rank_counts": [2, 2, 3, 3],
	},
}

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_seed_profession_resources_scan_and_validate()
	_test_progression_registry_and_game_session_cache_scanned_professions()
	_test_profession_registry_reports_missing_id_duplicate_and_illegal_refs()

	if _failures.is_empty():
		print("Profession schema regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Profession schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_seed_profession_resources_scan_and_validate() -> void:
	var skill_defs := ProgressionContentRegistry.new().get_skill_defs()
	var registry := ProfessionContentRegistry.new(skill_defs)
	var profession_defs := registry.get_profession_defs()

	_assert_true(registry.validate().is_empty(), "ProfessionContentRegistry 的正式职业资源当前不应报告校验错误。")
	_assert_eq(profession_defs.size(), SEED_EXPECTATIONS.size(), "正式职业资源数量应与当前 archetype 集合保持一致。")

	for profession_id in SEED_EXPECTATIONS.keys():
		var expectation: Dictionary = SEED_EXPECTATIONS.get(profession_id, {})
		var profession_def = profession_defs.get(profession_id) as ProfessionDef
		_assert_true(profession_def != null, "应扫描到职业资源 %s。" % String(profession_id))
		if profession_def == null:
			continue
		_assert_eq(profession_def.display_name, expectation.get("display_name", ""), "职业 %s 应保留展示名。" % String(profession_id))
		_assert_true(profession_def.is_initial_profession, "职业 %s 当前应保持初始职业配置。" % String(profession_id))
		_assert_eq(int(profession_def.max_rank), 5, "职业 %s 当前应保持 5 阶上限。" % String(profession_id))
		_assert_eq(int(profession_def.hit_die_sides), int(expectation.get("hit_die_sides", 0)), "职业 %s 的生命骰应稳定。" % String(profession_id))
		if profession_id == &"warrior":
			var granted_skills := profession_def.get_granted_skills_for_rank(1)
			_assert_eq(granted_skills.size(), 1, "战士 1 级应授予一个职业被动。")
			if not granted_skills.is_empty():
				_assert_eq(granted_skills[0].skill_id, &"warrior_toughness", "战士 1 级应授予强健。")
				_assert_eq(granted_skills[0].skill_type, &"passive", "强健授予项应标记为被动。")
		if profession_id == &"archer":
			var archer_granted_skills := profession_def.get_granted_skills_for_rank(1)
			_assert_eq(archer_granted_skills.size(), 1, "弓箭手 1 级应授予一个职业被动。")
			if not archer_granted_skills.is_empty():
				_assert_eq(archer_granted_skills[0].skill_id, &"archer_shooting_specialization", "弓箭手 1 级应授予射击专精。")
				_assert_eq(archer_granted_skills[0].skill_type, &"passive", "射击专精授予项应标记为被动。")
		_assert_true(profession_def.unlock_requirement != null, "职业 %s 应保留正式 unlock_requirement。" % String(profession_id))
		if profession_def.unlock_requirement != null:
			var unlock_rules: Array = profession_def.unlock_requirement.required_tag_rules
			_assert_eq(unlock_rules.size(), 2, "职业 %s 的 unlock_requirement 应保留两条 tag rule。" % String(profession_id))
			if unlock_rules.size() >= 2:
				_assert_eq(unlock_rules[0].tag, expectation.get("unlock_tag", &""), "职业 %s 的 qualifier tag 应稳定。" % String(profession_id))
				_assert_eq(int(unlock_rules[1].count), 1, "职业 %s 的 assigned core 门槛应稳定。" % String(profession_id))

		var rank_counts: Array = expectation.get("rank_counts", [])
		for index in range(rank_counts.size()):
			var target_rank := index + 2
			var rank_requirement := profession_def.get_rank_requirement(target_rank)
			_assert_true(rank_requirement != null, "职业 %s 应保留 rank %d requirement。" % [String(profession_id), target_rank])
			if rank_requirement == null or rank_requirement.required_tag_rules.is_empty():
				continue
			_assert_eq(
				int(rank_requirement.required_tag_rules[0].count),
				int(rank_counts[index]),
				"职业 %s 的 rank %d core 数量门槛应稳定。" % [String(profession_id), target_rank]
			)


func _test_progression_registry_and_game_session_cache_scanned_professions() -> void:
	var progression_registry := ProgressionContentRegistry.new()
	var profession_defs := progression_registry.get_profession_defs()
	_assert_true(profession_defs.has(&"archer"), "ProgressionContentRegistry 应暴露扫描得到的弓箭手职业。")
	_assert_true(progression_registry.validate().is_empty(), "ProgressionContentRegistry 接入 profession resource 后仍应通过静态校验。")

	var session := GameSession.new()
	var session_profession_defs := session.get_profession_defs()
	_assert_true(session_profession_defs.has(&"warrior"), "GameSession 应缓存 profession resource registry 的结果。")
	_assert_true(session_profession_defs.has(&"mage"), "GameSession 应缓存扫描得到的法师职业。")
	session.free()


func _test_profession_registry_reports_missing_id_duplicate_and_illegal_refs() -> void:
	var skill_defs := ProgressionContentRegistry.new().get_skill_defs()
	var registry := ProfessionContentRegistry.new(skill_defs)
	registry._profession_defs.clear()
	registry._validation_errors.clear()
	registry._scan_directory("res://tests/progression/fixtures/profession_registry_invalid")
	registry._validation_errors.append_array(registry._collect_validation_errors())
	var validation_errors := registry.validate()

	_assert_true(
		_has_error_containing(validation_errors, "is missing profession_id"),
		"职业注册表应显式报告缺失 profession_id。"
	)
	_assert_true(
		_has_error_containing(validation_errors, "Duplicate profession_id registered: duplicate_profession"),
		"职业注册表应显式报告重复 profession_id。"
	)
	_assert_true(
		_has_error_containing(validation_errors, "references missing skill missing_skill"),
		"职业注册表应显式报告非法技能引用。"
	)
	_assert_true(
		_has_error_containing(validation_errors, "references missing profession phantom_profession"),
		"职业注册表应显式报告非法职业引用。"
	)


func _has_error_containing(errors: Array[String], expected_fragment: String) -> bool:
	for validation_error in errors:
		if validation_error.contains(expected_fragment):
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
