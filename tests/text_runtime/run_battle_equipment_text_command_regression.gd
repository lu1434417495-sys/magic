extends SceneTree

const GAME_TEXT_COMMAND_RUNNER_SCRIPT = preload("res://scripts/systems/game_text_command_runner.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var runner = GAME_TEXT_COMMAND_RUNNER_SCRIPT.new()
	await runner.initialize()

	await _run_command(runner, "game new test")
	await _run_command(runner, "warehouse capacity 10")
	await _run_command(runner, "warehouse add bronze_sword 1")
	await _run_command(runner, "warehouse add leather_cap 1")
	await _run_command(runner, "warehouse add leather_jerkin 1")
	await _run_command(runner, "battle start settlement")
	await _run_command(runner, "battle confirm")
	await _advance_to_manual_battle_turn(runner)

	_prime_active_unit_ap(runner, 4)
	var equip_result = await _run_command(runner, "battle equip head leather_cap")
	_assert_successful_battle_equip(equip_result.snapshot, equip_result.snapshot_text)

	_prime_active_unit_ap(runner, 1)
	var ap_result = await _run_command_expect_fail(runner, "battle equip main_hand bronze_sword")
	_assert_battle_equip_ap_failure(ap_result.snapshot, ap_result.snapshot_text)

	_prime_active_unit_ap(runner, 2)
	var other_unit_id := _find_other_battle_unit_id(runner.get_session().build_snapshot())
	var target_result = await _run_command_expect_fail(
		runner,
		"battle equip main_hand bronze_sword target_unit_id=%s" % other_unit_id
	)
	_assert_battle_equip_self_only_failure(target_result.snapshot, target_result.snapshot_text)

	await _run_command(runner, "warehouse capacity 1")
	_prime_active_unit_ap(runner, 2)
	var full_result = await _run_command_expect_fail(runner, "battle unequip head")
	_assert_battle_unequip_backpack_full_failure(full_result.snapshot, full_result.snapshot_text)

	await _run_command(runner, "warehouse capacity 10")
	_prime_active_unit_ap(runner, 4)
	var hp_equip_result = await _run_command(runner, "battle equip body leather_jerkin")
	_assert_battle_hp_item_equipped(hp_equip_result.snapshot)
	var hp_equip_report := _find_latest_change_equipment_report(hp_equip_result.snapshot)
	_prime_active_unit_hp_and_ap(runner, int(hp_equip_report.get("hp_max_after", 0)), 2)
	var hp_unequip_result = await _run_command(runner, "battle unequip body")
	_assert_battle_unequip_hp_clamp_and_turn_end(hp_unequip_result.snapshot, hp_unequip_result.snapshot_text)

	await runner.dispose(true)
	_finish()


func _advance_to_manual_battle_turn(runner, max_ticks: int = 64) -> void:
	for _index in range(max_ticks):
		var battle_snapshot: Dictionary = runner.get_session().build_snapshot().get("battle", {})
		if not bool(battle_snapshot.get("active", false)):
			break
		var active_unit_id := String(battle_snapshot.get("active_unit_id", ""))
		var active_unit := _find_battle_unit(battle_snapshot, active_unit_id)
		if String(active_unit.get("control_mode", "")) == "manual":
			return
		await _run_command(runner, "battle tick 1.0")
	_assert_true(false, "战斗换装文本回归未能进入手动单位回合。")


func _prime_active_unit_ap(runner, current_ap: int) -> void:
	var active_unit = _get_active_unit_state(runner)
	_assert_true(active_unit != null, "战斗换装文本回归前置：应存在当前行动单位。")
	if active_unit == null:
		return
	active_unit.current_ap = current_ap
	if active_unit.attribute_snapshot != null:
		active_unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 1))


func _prime_active_unit_hp_and_ap(runner, current_hp: int, current_ap: int) -> void:
	var active_unit = _get_active_unit_state(runner)
	_assert_true(active_unit != null, "HP clamp 文本回归前置：应存在当前行动单位。")
	if active_unit == null:
		return
	active_unit.current_hp = current_hp
	active_unit.current_ap = current_ap


func _get_active_unit_state(runner):
	var runtime = runner.get_session().get_runtime_facade()
	var battle_state = runtime.get_battle_state() if runtime != null else null
	if battle_state == null or battle_state.active_unit_id == &"":
		return null
	return battle_state.units.get(battle_state.active_unit_id)


func _assert_successful_battle_equip(snapshot: Dictionary, text_snapshot: String) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(not report.is_empty(), "成功换装后快照应包含 change_equipment report。")
	_assert_true(bool(report.get("ok", false)), "成功换装 report 应标记 ok=true。")
	_assert_eq(String(report.get("operation", "")), "equip", "成功换装 report 应记录 equip 操作。")
	_assert_eq(String(report.get("slot_id", "")), "head", "成功换装 report 应记录 head 槽。")
	_assert_eq(String(report.get("item_id", "")), "leather_cap", "成功换装 report 应记录装备物品。")
	_assert_eq(int(report.get("ap_before", 0)), 4, "成功换装 report 应记录换装前 AP。")
	_assert_eq(int(report.get("ap_after", 0)), 2, "成功换装 report 应记录换装后 AP。")
	var unit := _find_battle_unit(snapshot.get("battle", {}), String(report.get("unit_id", "")))
	var equipped := _find_equipped_entry(unit.get("equipment", []), "head")
	_assert_eq(String(equipped.get("item_id", "")), "leather_cap", "成功换装后单位 battle-local 装备快照应显示 head 皮革护帽。")
	_assert_eq(_count_battle_backpack_item(snapshot, "leather_cap"), 0, "成功换装后 battle-local 背包中不应残留该装备。")
	_assert_true(text_snapshot.contains("report=change_equipment | ok=true"), "文本快照应渲染成功换装 report。")
	_assert_true(text_snapshot.contains("equip=head:leather_cap#"), "文本快照应渲染单位 battle-local 装备。")
	_assert_true(text_snapshot.contains("backpack_used_slots="), "文本快照应渲染 battle-local 背包摘要。")


func _assert_battle_equip_ap_failure(snapshot: Dictionary, text_snapshot: String) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(not bool(report.get("ok", true)), "AP 不足时 change_equipment report 应标记失败。")
	_assert_eq(String(report.get("error_code", "")), "ap_insufficient", "AP 不足时 report 应暴露稳定错误码。")
	_assert_eq(int(report.get("current_ap", -1)), 1, "AP 不足失败时不应扣 AP。")
	_assert_eq(_count_battle_backpack_item(snapshot, "bronze_sword"), 1, "AP 不足失败时装备实例应留在 battle-local 背包。")
	_assert_true(text_snapshot.contains("error=ap_insufficient"), "文本快照应渲染 AP 不足失败原因。")


func _assert_battle_equip_self_only_failure(snapshot: Dictionary, text_snapshot: String) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(not bool(report.get("ok", true)), "指定其他目标时 change_equipment report 应标记失败。")
	_assert_eq(String(report.get("error_code", "")), "target_not_self", "指定其他目标时应暴露 self-only 错误码。")
	_assert_eq(int(report.get("current_ap", -1)), 2, "self-only 失败时不应扣 AP。")
	_assert_eq(_count_battle_backpack_item(snapshot, "bronze_sword"), 1, "self-only 失败时装备实例应留在 battle-local 背包。")
	_assert_true(text_snapshot.contains("只能为当前行动单位自己换装"), "文本快照应保留 self-only 失败文案。")


func _assert_battle_unequip_backpack_full_failure(snapshot: Dictionary, text_snapshot: String) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(not bool(report.get("ok", true)), "背包满卸装时 change_equipment report 应标记失败。")
	_assert_eq(String(report.get("error_code", "")), "backpack_capacity_exceeded", "背包满卸装应暴露容量错误码。")
	_assert_eq(int(report.get("current_ap", -1)), 2, "背包满卸装失败时不应扣 AP。")
	var unit := _find_battle_unit(snapshot.get("battle", {}), String(report.get("unit_id", "")))
	var equipped := _find_equipped_entry(unit.get("equipment", []), "head")
	_assert_eq(String(equipped.get("item_id", "")), "leather_cap", "背包满卸装失败后 head 装备应保持不变。")
	_assert_eq(_count_battle_backpack_item(snapshot, "leather_cap"), 0, "背包满卸装失败后装备不应进入 battle-local 背包。")
	_assert_true(text_snapshot.contains("error=backpack_capacity_exceeded"), "文本快照应渲染背包满失败原因。")


func _assert_battle_hp_item_equipped(snapshot: Dictionary) -> void:
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(bool(report.get("ok", false)), "HP 装备换装应成功。")
	_assert_eq(String(report.get("item_id", "")), "leather_jerkin", "HP 装备换装 report 应记录皮革短甲。")
	_assert_true(int(report.get("hp_max_after", 0)) > int(report.get("hp_max_before", 0)), "装备 HP 加成装备后 hp_max_after 应上升。")


func _assert_battle_unequip_hp_clamp_and_turn_end(snapshot: Dictionary, text_snapshot: String) -> void:
	var battle_snapshot: Dictionary = snapshot.get("battle", {})
	var report := _find_latest_change_equipment_report(snapshot)
	_assert_true(bool(report.get("ok", false)), "HP 装备卸装应成功。")
	_assert_eq(String(report.get("operation", "")), "unequip", "HP clamp report 应来自卸装。")
	_assert_true(bool(report.get("hp_clamped", false)), "卸下 HP 加成装备时 report 应标记 hp_clamped。")
	_assert_true(int(report.get("hp_before", 0)) > int(report.get("hp_after", 0)), "HP clamp report 应显示 HP 下降。")
	_assert_eq(int(report.get("hp_after", 0)), int(report.get("hp_max_after", -1)), "HP clamp 后 current_hp 应等于新 HP 上限。")
	_assert_eq(String(battle_snapshot.get("phase", "")), "timeline_running", "AP 归零后战斗阶段应回到 timeline_running。")
	_assert_eq(String(battle_snapshot.get("active_unit_id", "")), "", "AP 归零后应清空 active_unit_id。")
	var unit := _find_battle_unit(battle_snapshot, String(report.get("unit_id", "")))
	_assert_true(_find_equipped_entry(unit.get("equipment", []), "body").is_empty(), "HP 装备卸装后 body 槽应清空。")
	_assert_eq(_count_battle_backpack_item(snapshot, "leather_jerkin"), 1, "HP 装备卸装后应回到 battle-local 背包。")
	_assert_true(text_snapshot.contains("hp_clamped=true"), "文本快照应渲染 HP clamp。")
	_assert_true(text_snapshot.contains("active_unit_id="), "文本快照应渲染行动结束后的 active_unit_id。")


func _find_latest_change_equipment_report(snapshot: Dictionary) -> Dictionary:
	var reports: Array = snapshot.get("battle", {}).get("report_entries", [])
	for index in range(reports.size() - 1, -1, -1):
		var report_variant = reports[index]
		if report_variant is not Dictionary:
			continue
		var report: Dictionary = report_variant
		if String(report.get("type", report.get("entry_type", ""))) == "change_equipment":
			return report
	return {}


func _find_battle_unit(battle_snapshot: Dictionary, unit_id: String) -> Dictionary:
	var units: Array = battle_snapshot.get("units", [])
	for unit_variant in units:
		if unit_variant is not Dictionary:
			continue
		var unit: Dictionary = unit_variant
		if String(unit.get("unit_id", "")) == unit_id:
			return unit
	return {}


func _find_other_battle_unit_id(snapshot: Dictionary) -> String:
	var battle_snapshot: Dictionary = snapshot.get("battle", {})
	var active_unit_id := String(battle_snapshot.get("active_unit_id", ""))
	var units: Array = battle_snapshot.get("units", [])
	for unit_variant in units:
		if unit_variant is not Dictionary:
			continue
		var unit: Dictionary = unit_variant
		var unit_id := String(unit.get("unit_id", ""))
		if not unit_id.is_empty() and unit_id != active_unit_id:
			return unit_id
	return "not_current_unit"


func _find_equipped_entry(entries: Array, slot_id: String) -> Dictionary:
	for entry_variant in entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("slot_id", "")) == slot_id:
			return entry
	return {}


func _count_battle_backpack_item(snapshot: Dictionary, item_id: String) -> int:
	var backpack: Dictionary = snapshot.get("battle", {}).get("party_backpack", {})
	var total := 0
	var stacks: Array = backpack.get("stacks", [])
	for stack_variant in stacks:
		if stack_variant is not Dictionary:
			continue
		var stack: Dictionary = stack_variant
		if String(stack.get("item_id", "")) == item_id:
			total += int(stack.get("quantity", 0))
	var instances: Array = backpack.get("equipment_instances", [])
	for instance_variant in instances:
		if instance_variant is not Dictionary:
			continue
		var instance: Dictionary = instance_variant
		if String(instance.get("item_id", "")) == item_id:
			total += 1
	return total


func _run_command(runner, command_text: String):
	var result = await runner.execute_line(command_text)
	if result.skipped:
		return result
	if not result.ok:
		print(result.render())
		_failures.append("命令失败：%s | %s" % [command_text, result.message])
	return result


func _run_command_expect_fail(runner, command_text: String):
	var result = await runner.execute_line(command_text)
	if result.skipped:
		_failures.append("命令被跳过，无法验证失败：%s" % command_text)
		return result
	if result.ok:
		print(result.render())
		_failures.append("命令应失败但成功：%s" % command_text)
	return result


func _assert_true(value: bool, message: String) -> void:
	if value:
		return
	_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _finish() -> void:
	if _failures.is_empty():
		print("Battle equipment text command regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle equipment text command regression: FAIL (%d)" % _failures.size())
	quit(1)
