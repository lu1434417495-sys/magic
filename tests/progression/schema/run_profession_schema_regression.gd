extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GameSession = preload("res://scripts/systems/persistence/game_session.gd")
const ProfessionContentRegistry = preload("res://scripts/player/progression/profession_content_registry.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")

const SEED_EXPECTATIONS := {
	&"warrior": {
		"display_name": "战士",
		"unlock_tag": &"melee",
		"hit_die_sides": 10,
		"bab_progression": &"full",
		"rank_counts": [2, 3, 4, 5],
	},
	&"priest": {
		"display_name": "牧师",
		"unlock_tag": &"priest",
		"hit_die_sides": 8,
		"bab_progression": &"three_quarter",
		"rank_counts": [2, 2, 3, 3],
	},
	&"rogue": {
		"display_name": "盗贼",
		"unlock_tag": &"rogue",
		"hit_die_sides": 8,
		"bab_progression": &"three_quarter",
		"rank_counts": [2, 2, 3, 3],
	},
	&"berserker": {
		"display_name": "狂战士",
		"unlock_tag": &"berserker",
		"hit_die_sides": 12,
		"bab_progression": &"full",
		"rank_counts": [2, 2, 3, 3],
	},
	&"paladin": {
		"display_name": "圣武士",
		"unlock_tag": &"paladin",
		"hit_die_sides": 10,
		"bab_progression": &"full",
		"rank_counts": [2, 2, 3, 3],
	},
	&"mage": {
		"display_name": "法师",
		"unlock_tag": &"mage",
		"hit_die_sides": 6,
		"bab_progression": &"half",
		"rank_counts": [2, 2, 3, 3],
	},
	&"archer": {
		"display_name": "弓箭手",
		"unlock_tag": &"archer",
		"hit_die_sides": 8,
		"bab_progression": &"full",
		"rank_counts": [2, 2, 3, 3],
	},
}

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_seed_profession_resources_scan_and_validate()
	_test_progression_registry_and_game_session_cache_scanned_professions()
	_test_profession_registry_reports_missing_id_duplicate_and_illegal_refs()
	_test_profession_gate_reachability_reports_structural_deadlocks()
	_test_profession_gate_reachability_allows_valid_rank_graphs()

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
		_assert_eq(profession_def.bab_progression, expectation.get("bab_progression", &""), "职业 %s 的 BAB 成长档位应稳定。" % String(profession_id))
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
	_assert_current_official_progression_validation_errors(
		progression_registry.validate(),
		"ProgressionContentRegistry 接入 profession resource 后不应报告正式内容校验错误。"
	)

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
	_assert_true(
		_has_error_containing(validation_errors, "uses unsupported bab_progression three-quarter"),
		"职业注册表应显式报告非法 BAB 成长档位。"
	)


func _test_profession_gate_reachability_reports_structural_deadlocks() -> void:
	var rank_one_target := _make_schema_profession(&"rank_one_target", 1)
	var over_max_gate := _make_schema_profession(&"over_max_gate", 1, [&"rank_one_target", 2])
	var self_unlock_gate := _make_schema_profession(&"self_unlock_gate", 1, [&"self_unlock_gate", 1])
	var self_rank_lock := _make_schema_profession(&"self_rank_lock", 2)
	self_rank_lock.rank_requirements = [_make_rank_requirement(2, [&"self_rank_lock", 2])]
	var unlock_cycle_a := _make_schema_profession(&"unlock_cycle_a", 1, [&"unlock_cycle_b", 1])
	var unlock_cycle_b := _make_schema_profession(&"unlock_cycle_b", 1, [&"unlock_cycle_a", 1])
	var rank_cycle_a := _make_schema_profession(&"rank_cycle_a", 2)
	rank_cycle_a.rank_requirements = [_make_rank_requirement(2, [&"rank_cycle_b", 2])]
	var rank_cycle_b := _make_schema_profession(&"rank_cycle_b", 2)
	rank_cycle_b.rank_requirements = [_make_rank_requirement(2, [&"rank_cycle_a", 2])]

	var registry := _make_profession_registry_with_defs([
		rank_one_target,
		over_max_gate,
		self_unlock_gate,
		self_rank_lock,
		unlock_cycle_a,
		unlock_cycle_b,
		rank_cycle_a,
		rank_cycle_b,
	])
	var validation_errors := registry._collect_validation_errors()

	_assert_true(
		_has_error_containing(validation_errors, "requires rank 2 for gate rank_one_target but rank_one_target max_rank is 1"),
		"profession gate 要求超过目标 max_rank 时应被静态拒绝。"
	)
	_assert_true(
		_has_error_containing(validation_errors, "cannot require itself in unlock.required_profession_ranks"),
		"职业 unlock gate 自引用时应被静态拒绝。"
	)
	_assert_true(
		_has_error_containing(validation_errors, "rank_2.required_profession_ranks cannot require self rank 2"),
		"职业 rank-up gate 要求自身达到当前 target_rank 时应被静态拒绝。"
	)
	_assert_true(
		_has_error_containing(validation_errors, "unlock_cycle_a has structurally unreachable profession gate unlock_cycle_b@1"),
		"跨职业 unlock 循环应被 rank 图可达性校验拒绝。"
	)
	_assert_true(
		_has_error_containing(validation_errors, "rank_cycle_a has structurally unreachable profession gate rank_cycle_b@2"),
		"高阶 rank 互锁应被 rank 图可达性校验拒绝。"
	)


func _test_profession_gate_reachability_allows_valid_rank_graphs() -> void:
	var gate_free_base := _make_schema_profession(&"gate_free_base", 1)
	var rank_self_ok := _make_schema_profession(&"rank_self_ok", 3)
	rank_self_ok.rank_requirements = [
		_make_rank_requirement(2, [&"rank_self_ok", 1]),
		_make_rank_requirement(3, [&"rank_self_ok", 2]),
	]
	var cross_source := _make_schema_profession(&"cross_source", 2)
	cross_source.rank_requirements = [_make_rank_requirement(2, [&"gate_free_base", 1])]
	var long_chain := _make_schema_profession(&"long_chain", 3)
	long_chain.rank_requirements = [
		_make_rank_requirement(2),
		_make_rank_requirement(3, [&"cross_source", 2]),
	]

	var registry := _make_profession_registry_with_defs([
		gate_free_base,
		rank_self_ok,
		cross_source,
		long_chain,
	])

	_assert_true(
		registry._collect_validation_errors().is_empty(),
		"合法旧 rank 自引用、跨职业低阶依赖和长链高阶依赖不应被可达性校验误杀。"
	)


func _make_profession_registry_with_defs(profession_defs: Array) -> ProfessionContentRegistry:
	var registry := ProfessionContentRegistry.new({})
	registry._profession_defs.clear()
	registry._validation_errors.clear()
	for profession_def in profession_defs:
		if profession_def is ProfessionDef:
			registry._profession_defs[profession_def.profession_id] = profession_def
	return registry


func _make_schema_profession(
	profession_id: StringName,
	max_rank: int,
	unlock_gate_data: Array = []
) -> ProfessionDef:
	var profession_def := ProfessionDef.new()
	profession_def.profession_id = profession_id
	profession_def.display_name = String(profession_id)
	profession_def.max_rank = max_rank
	profession_def.hit_die_sides = 8
	profession_def.bab_progression = &"half"
	profession_def.is_initial_profession = true
	profession_def.unlock_requirement = _make_unlock_requirement(unlock_gate_data)
	for target_rank in range(2, max_rank + 1):
		profession_def.rank_requirements.append(_make_rank_requirement(target_rank))
	return profession_def


func _make_unlock_requirement(gate_data: Array = []) -> ProfessionPromotionRequirement:
	var requirement := ProfessionPromotionRequirement.new()
	if not gate_data.is_empty():
		requirement.required_profession_ranks = [_make_profession_gate(gate_data[0], int(gate_data[1]))]
	return requirement


func _make_rank_requirement(target_rank: int, gate_data: Array = []) -> ProfessionRankRequirement:
	var requirement := ProfessionRankRequirement.new()
	requirement.target_rank = target_rank
	if not gate_data.is_empty():
		requirement.required_profession_ranks = [_make_profession_gate(gate_data[0], int(gate_data[1]))]
	return requirement


func _make_profession_gate(profession_id: StringName, min_rank: int) -> ProfessionRankGate:
	var gate := ProfessionRankGate.new()
	gate.profession_id = profession_id
	gate.min_rank = min_rank
	return gate


func _has_error_containing(errors: Array[String], expected_fragment: String) -> bool:
	for validation_error in errors:
		if validation_error.contains(expected_fragment):
			return true
	return false


func _assert_current_official_progression_validation_errors(errors: Array[String], message: String) -> void:
	_assert_true(errors.is_empty(), "%s | errors=%s" % [message, str(errors)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
