extends SceneTree

const EquipmentDropService = preload("res://scripts/systems/equipment_drop_service.gd")
const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")

var _failures: Array[String] = []


class FixedRollRng:
	extends RefCounted

	var _rolls: Array[int] = []
	var _cursor := 0


	func _init(rolls: Array[int]) -> void:
		_rolls = rolls.duplicate()


	func randi_range(min_value: int, max_value: int) -> int:
		if _cursor >= _rolls.size():
			return min_value
		var value := int(_rolls[_cursor])
		_cursor += 1
		return clampi(value, min_value, max_value)


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_roll_drop_rarity_hits_all_threshold_tiers()
	_test_roll_drop_rarity_accepts_caller_clamped_extremes()
	_test_roll_drops_keeps_empty_main_path_stable()
	_finish()


func _test_roll_drop_rarity_hits_all_threshold_tiers() -> void:
	_assert_rarity_roll(
		"COMMON 档位上界应落在 9",
		[3, 3, 3],
		0,
		EquipmentInstanceState.RarityTier.COMMON
	)
	_assert_rarity_roll(
		"UNCOMMON 档位门槛应落在 10",
		[4, 3, 3],
		0,
		EquipmentInstanceState.RarityTier.UNCOMMON
	)
	_assert_rarity_roll(
		"RARE 档位门槛应落在 13",
		[5, 4, 4],
		0,
		EquipmentInstanceState.RarityTier.RARE
	)
	_assert_rarity_roll(
		"EPIC 档位门槛应落在 16",
		[6, 5, 5],
		0,
		EquipmentInstanceState.RarityTier.EPIC
	)
	_assert_rarity_roll(
		"LEGENDARY 档位门槛应落在 18",
		[6, 6, 6],
		0,
		EquipmentInstanceState.RarityTier.LEGENDARY
	)


func _test_roll_drop_rarity_accepts_caller_clamped_extremes() -> void:
	_assert_rarity_roll(
		"最低 drop_luck=-6 应直接参与 3d6 结果",
		[6, 6, 6],
		-6,
		EquipmentInstanceState.RarityTier.UNCOMMON
	)
	_assert_rarity_roll(
		"最高 drop_luck=+5 应直接参与 3d6 结果",
		[1, 1, 1],
		5,
		EquipmentInstanceState.RarityTier.COMMON
	)


func _test_roll_drops_keeps_empty_main_path_stable() -> void:
	var service := EquipmentDropService.new(FixedRollRng.new([6, 6, 6]))
	var drops := service.roll_drops(&"starter_equipment", 0)
	_assert_true(drops is Array, "roll_drops 当前应返回稳定的 Array。")
	_assert_eq(drops.size(), 0, "正式掉落表尚未接入前，roll_drops 应返回空数组。")


func _assert_rarity_roll(label: String, rolls: Array[int], drop_luck: int, expected_rarity: int) -> void:
	var service := EquipmentDropService.new(FixedRollRng.new(rolls))
	var actual_rarity := int(service.roll_drop_rarity(drop_luck))
	_assert_eq(actual_rarity, expected_rarity, "%s。", [label])


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	_failures.append(message)


func _assert_eq(actual, expected, message: String, format_args: Array = []) -> void:
	if actual == expected:
		return
	var resolved_message := message % format_args if not format_args.is_empty() else message
	_failures.append("%s | actual=%s expected=%s" % [resolved_message, str(actual), str(expected)])


func _finish() -> void:
	if _failures.is_empty():
		print("Equipment drop service regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Equipment drop service regression: FAIL (%d)" % _failures.size())
	quit(1)
