extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const AttributeSnapshot = preload("res://scripts/player/progression/attribute_snapshot.gd")
const AttributeService = preload("res://scripts/systems/attributes/attribute_service.gd")
const ProfessionDef = preload("res://scripts/player/progression/profession_def.gd")
const UnitProfessionProgress = preload("res://scripts/player/progression/unit_profession_progress.gd")
const UnitProgress = preload("res://scripts/player/progression/unit_progress.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_single_class_full_bab_table()
	_test_single_class_three_quarter_bab_table()
	_test_single_class_half_bab_table()
	_test_multi_class_accumulates_numerator_before_floor()
	_test_total_rank_capped_at_twenty_keeps_bab_at_or_below_ten()
	_test_unknown_progression_falls_back_to_half()
	_test_attribute_service_writes_base_attack_bonus_for_full_warrior()
	_test_attribute_service_excludes_inactive_and_hidden_professions()
	_test_attribute_service_multi_class_matches_static_calculation()

	if _failures.is_empty():
		print("Base attack bonus regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Base attack bonus regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_single_class_full_bab_table() -> void:
	var expected_bab: Array = [
		[1, 0], [2, 1], [3, 1], [4, 2], [5, 2],
		[6, 3], [7, 3], [8, 4], [9, 4], [10, 5],
		[11, 5], [12, 6], [13, 6], [14, 7], [15, 7],
		[16, 8], [17, 8], [18, 9], [19, 9], [20, 10],
	]
	for entry in expected_bab:
		var rank: int = entry[0]
		var expected: int = entry[1]
		var actual := AttributeSnapshot.calculate_base_attack_bonus([[rank, AttributeSnapshot.BAB_PROGRESSION_FULL]])
		_assert_eq(actual, expected, "Full BAB rank %d 应为 %d。" % [rank, expected])


func _test_single_class_three_quarter_bab_table() -> void:
	var expected_bab: Array = [
		[1, 0], [3, 1], [5, 1], [6, 2], [8, 3], [10, 3], [11, 4], [14, 5], [18, 6], [20, 7],
	]
	for entry in expected_bab:
		var rank: int = entry[0]
		var expected: int = entry[1]
		var actual := AttributeSnapshot.calculate_base_attack_bonus([[rank, AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER]])
		_assert_eq(actual, expected, "¾ BAB rank %d 应为 %d。" % [rank, expected])


func _test_single_class_half_bab_table() -> void:
	var expected_bab: Array = [
		[1, 0], [3, 0], [4, 1], [7, 1], [8, 2], [11, 2], [12, 3], [15, 3], [16, 4], [20, 5],
	]
	for entry in expected_bab:
		var rank: int = entry[0]
		var expected: int = entry[1]
		var actual := AttributeSnapshot.calculate_base_attack_bonus([[rank, AttributeSnapshot.BAB_PROGRESSION_HALF]])
		_assert_eq(actual, expected, "½ BAB rank %d 应为 %d。" % [rank, expected])


func _test_multi_class_accumulates_numerator_before_floor() -> void:
	# 法师 7 + 牧师 5：每职业 floor 早算只得 2，先乘后除得 3，差 1 BAB。
	var pairs := [
		[7, AttributeSnapshot.BAB_PROGRESSION_HALF],
		[5, AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER],
	]
	_assert_eq(
		AttributeSnapshot.calculate_base_attack_bonus(pairs),
		3,
		"法师 7 + 牧师 5 应得 BAB 3（per-prof floor 会丢精度变成 2）。"
	)

	# 战士 1 + 法师 1 + 牧师 1：三职 rank 1 各自 floor 都是 0，累加分子才能凑出 BAB 1。
	var trio := [
		[1, AttributeSnapshot.BAB_PROGRESSION_FULL],
		[1, AttributeSnapshot.BAB_PROGRESSION_HALF],
		[1, AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER],
	]
	_assert_eq(
		AttributeSnapshot.calculate_base_attack_bonus(trio),
		1,
		"战士 1 + 法师 1 + 牧师 1 应得 BAB 1（per-prof floor 会全归 0）。"
	)

	# 战士 3 + 法师 3 + 牧师 3：先乘后除 = 27/8 = 3，per-prof floor 只得 2。
	var triple_three := [
		[3, AttributeSnapshot.BAB_PROGRESSION_FULL],
		[3, AttributeSnapshot.BAB_PROGRESSION_HALF],
		[3, AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER],
	]
	_assert_eq(
		AttributeSnapshot.calculate_base_attack_bonus(triple_three),
		3,
		"战士 3 + 法师 3 + 牧师 3 应得 BAB 3（per-prof floor 会丢精度变成 2）。"
	)


func _test_total_rank_capped_at_twenty_keeps_bab_at_or_below_ten() -> void:
	# 多职业上限 = 总 rank 20 时，无论怎么分配都不会超过 +10。
	var distributions: Array = [
		[[20, AttributeSnapshot.BAB_PROGRESSION_FULL]],
		[[10, AttributeSnapshot.BAB_PROGRESSION_FULL], [10, AttributeSnapshot.BAB_PROGRESSION_FULL]],
		[[15, AttributeSnapshot.BAB_PROGRESSION_FULL], [5, AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER]],
		[[10, AttributeSnapshot.BAB_PROGRESSION_FULL], [10, AttributeSnapshot.BAB_PROGRESSION_HALF]],
		[[5, AttributeSnapshot.BAB_PROGRESSION_FULL], [5, AttributeSnapshot.BAB_PROGRESSION_FULL], [5, AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER], [5, AttributeSnapshot.BAB_PROGRESSION_HALF]],
	]
	for pairs in distributions:
		var bab: int = AttributeSnapshot.calculate_base_attack_bonus(pairs)
		_assert_true(bab <= 10, "总 rank ≤ 20 时 BAB 不应超 +10，得到 %d，分布 %s。" % [bab, str(pairs)])
	# rank 20 全 Full 时恰好顶上限。
	_assert_eq(
		AttributeSnapshot.calculate_base_attack_bonus([[20, AttributeSnapshot.BAB_PROGRESSION_FULL]]),
		10,
		"纯 Full BAB rank 20 应为 +10。"
	)


func _test_unknown_progression_falls_back_to_half() -> void:
	# 未知 progression 字符串应回退到 half，避免空字段意外把法师变成战士。
	var unknown_pair := [10, &"unknown_value"]
	var half_pair := [10, AttributeSnapshot.BAB_PROGRESSION_HALF]
	_assert_eq(
		AttributeSnapshot.calculate_base_attack_bonus([unknown_pair]),
		AttributeSnapshot.calculate_base_attack_bonus([half_pair]),
		"未知 BAB progression 应安全回退到 half。"
	)


func _test_attribute_service_writes_base_attack_bonus_for_full_warrior() -> void:
	var warrior := _make_profession(&"warrior", AttributeSnapshot.BAB_PROGRESSION_FULL)
	var progress := _make_progress(&"hero")
	progress.set_profession_progress(_make_profession_progress(&"warrior", 5, true, false))

	var snapshot = _build_snapshot(progress, [warrior])
	_assert_eq(snapshot.get_value(AttributeService.BASE_ATTACK_BONUS), 2, "战士 rank 5 在 snapshot 中应写入 BAB 2。")


func _test_attribute_service_excludes_inactive_and_hidden_professions() -> void:
	var warrior := _make_profession(&"warrior", AttributeSnapshot.BAB_PROGRESSION_FULL)
	var mage := _make_profession(&"mage", AttributeSnapshot.BAB_PROGRESSION_HALF)

	var inactive_progress := _make_progress(&"inactive_hero")
	inactive_progress.set_profession_progress(_make_profession_progress(&"warrior", 10, false, false))
	inactive_progress.set_profession_progress(_make_profession_progress(&"mage", 4, true, false))

	var inactive_snapshot = _build_snapshot(inactive_progress, [warrior, mage])
	_assert_eq(
		inactive_snapshot.get_value(AttributeService.BASE_ATTACK_BONUS),
		1,
		"未激活的战士 rank 10 不应贡献 BAB；仅法师 rank 4 (½) = 1。"
	)

	var hidden_progress := _make_progress(&"hidden_hero")
	hidden_progress.set_profession_progress(_make_profession_progress(&"warrior", 10, true, true))
	hidden_progress.set_profession_progress(_make_profession_progress(&"mage", 4, true, false))

	var hidden_snapshot = _build_snapshot(hidden_progress, [warrior, mage])
	_assert_eq(
		hidden_snapshot.get_value(AttributeService.BASE_ATTACK_BONUS),
		1,
		"被隐藏的战士不应贡献 BAB；仅法师 rank 4 (½) = 1。"
	)


func _test_attribute_service_multi_class_matches_static_calculation() -> void:
	var warrior := _make_profession(&"warrior", AttributeSnapshot.BAB_PROGRESSION_FULL)
	var mage := _make_profession(&"mage", AttributeSnapshot.BAB_PROGRESSION_HALF)
	var priest := _make_profession(&"priest", AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER)

	var progress := _make_progress(&"multi_hero")
	progress.set_profession_progress(_make_profession_progress(&"warrior", 3, true, false))
	progress.set_profession_progress(_make_profession_progress(&"mage", 3, true, false))
	progress.set_profession_progress(_make_profession_progress(&"priest", 3, true, false))

	var snapshot = _build_snapshot(progress, [warrior, mage, priest])
	# 静态计算：(3*4 + 3*2 + 3*3)/8 = 27/8 = 3
	_assert_eq(
		snapshot.get_value(AttributeService.BASE_ATTACK_BONUS),
		3,
		"战士 3 + 法师 3 + 牧师 3 在 service 应得 BAB 3，与静态算法一致。"
	)


func _build_snapshot(progress: UnitProgress, profession_defs: Array):
	var service = AttributeService.new()
	service.setup(progress, null, profession_defs)
	return service.get_snapshot()


func _make_profession(profession_id: StringName, progression: StringName) -> ProfessionDef:
	var profession := ProfessionDef.new()
	profession.profession_id = profession_id
	profession.display_name = String(profession_id)
	profession.description = "Fixture profession."
	profession.max_rank = 20
	profession.bab_progression = progression
	return profession


func _make_profession_progress(profession_id: StringName, rank: int, is_active: bool, is_hidden: bool) -> UnitProfessionProgress:
	var profession_progress := UnitProfessionProgress.new()
	profession_progress.profession_id = profession_id
	profession_progress.rank = rank
	profession_progress.is_active = is_active
	profession_progress.is_hidden = is_hidden
	return profession_progress


func _make_progress(unit_id: StringName) -> UnitProgress:
	var progress := UnitProgress.new()
	progress.unit_id = unit_id
	progress.display_name = String(unit_id).capitalize()
	for attribute_id in UnitBaseAttributes.BASE_ATTRIBUTE_IDS:
		progress.unit_base_attributes.set_attribute_value(attribute_id, 10)
	return progress


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
