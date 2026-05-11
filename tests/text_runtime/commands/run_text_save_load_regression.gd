extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_TEXT_COMMAND_RUNNER_SCRIPT = preload("res://scripts/systems/game_runtime/headless/game_text_command_runner.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_game_load_reopens_newly_created_save()
	await _test_game_load_reopens_forge_persisted_save()

	if _failures.is_empty():
		print("Text save/load regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Text save/load regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_game_load_reopens_newly_created_save() -> void:
	var runner = GAME_TEXT_COMMAND_RUNNER_SCRIPT.new()
	await runner.initialize()

	var create_result = await runner.execute_line("game new ashen_intersection")
	_assert_true(bool(create_result.ok), "game new ashen_intersection 应创建成功。")
	if not bool(create_result.ok):
		await runner.dispose(true)
		return

	var save_id := String(runner.get_session().get_game_session().get_active_save_id())
	_assert_true(not save_id.is_empty(), "新建世界后应能读取 active save id。")
	_assert_true(_save_slots_include_id(runner, save_id), "新建世界后 save slots 应包含 active save id。")
	_assert_ungenerated_submap_placeholder(runner, "ashen_ashlands", "新建 ashen_intersection 后应保留未生成子地图空占位。")

	var load_result = await runner.execute_line("game load %s" % save_id)
	_assert_true(bool(load_result.ok), "game load 刚创建的 save_id 应成功。")
	_assert_eq(
		String(runner.get_session().get_game_session().get_active_save_id()),
		save_id,
		"重新载入后 active save id 应保持不变。"
	)
	_assert_ungenerated_submap_placeholder(runner, "ashen_ashlands", "重新载入后未生成子地图占位仍应是空 world_data。")

	await runner.dispose(true)


func _test_game_load_reopens_forge_persisted_save() -> void:
	var runner = GAME_TEXT_COMMAND_RUNNER_SCRIPT.new()
	await runner.initialize()

	var create_result = await runner.execute_line("game new ashen_intersection")
	_assert_true(bool(create_result.ok), "forge save/load 回归前置：应能创建 ashen_intersection 世界。")
	if not bool(create_result.ok):
		await runner.dispose(true)
		return

	var save_id := String(runner.get_session().get_game_session().get_active_save_id())
	_assert_true(not save_id.is_empty(), "forge save/load 回归前置：应能读取 active save id。")

	await _assert_command_ok(runner, "warehouse add bronze_sword 1")
	await _assert_command_ok(runner, "warehouse add iron_ore 3")
	await _assert_command_ok(runner, "world open")
	await _assert_command_ok(runner, "settlement action service:repair_gear")
	await _assert_command_ok(runner, "settlement action service:repair_gear submission_source=forge recipe_id=forge_smith_iron_greatsword")
	_assert_eq(
		_count_runtime_warehouse_item(runner, "iron_greatsword"),
		1,
		"锻造完成后，当前 runtime 仓库应立即包含铁制大剑。"
	)
	_assert_eq(
		_count_party_state_item(runner.get_session().get_game_session().get_party_state(), "iron_greatsword"),
		1,
		"锻造完成并持久化后，GameSession 内部 PartyState 应包含铁制大剑。"
	)

	_assert_true(_save_slots_include_id(runner, save_id), "forge 持久化后 save slots 应继续包含原始 save id。")
	var load_result = await runner.execute_line("game load %s" % save_id)
	_assert_true(bool(load_result.ok), "game load forge 持久化后的 save_id 应成功。")
	_assert_eq(
		_count_party_state_item(runner.get_session().get_game_session().get_party_state(), "iron_greatsword"),
		1,
		"重新载入后，GameSession 内部 PartyState 应包含铁制大剑。"
	)
	_assert_eq(_count_warehouse_item(load_result.snapshot, "iron_greatsword"), 1, "重新载入后应保留通用 forge 产出的铁制大剑。")

	await runner.dispose(true)


func _save_slots_include_id(runner, save_id: String) -> bool:
	if runner == null or save_id.is_empty():
		return false
	for slot_variant in runner.get_session().get_game_session().list_save_slots():
		if slot_variant is not Dictionary:
			continue
		if String((slot_variant as Dictionary).get("save_id", "")) == save_id:
			return true
	return false


func _count_warehouse_item(snapshot: Dictionary, item_id: String) -> int:
	var warehouse_snapshot: Dictionary = snapshot.get("warehouse", {})
	var window_data: Dictionary = warehouse_snapshot.get("window_data", {})
	var entries: Array = window_data.get("entries", [])
	for entry_variant in entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("item_id", "")) != item_id:
			continue
		return int(entry.get("quantity", entry.get("total_quantity", 0)))
	return 0


func _count_runtime_warehouse_item(runner, item_id: String) -> int:
	if runner == null:
		return 0
	var runtime = runner.get_session().get_runtime_facade()
	if runtime == null:
		return 0
	var window_data: Dictionary = runtime.get_warehouse_window_data()
	for entry_variant in window_data.get("entries", []):
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("item_id", "")) != item_id:
			continue
		return int(entry.get("quantity", entry.get("total_quantity", 0)))
	return 0


func _count_party_state_item(party_state, item_id: String) -> int:
	if party_state == null or party_state.warehouse_state == null:
		return 0
	for stack_variant in party_state.warehouse_state.get_non_empty_stacks():
		if stack_variant == null:
			continue
		if String(stack_variant.item_id) != item_id:
			continue
		return maxi(int(stack_variant.quantity), 0)
	var total := 0
	for inst_variant in party_state.warehouse_state.get_non_empty_instances():
		if inst_variant == null:
			continue
		if String(inst_variant.item_id) != item_id:
			continue
		total += 1
	return total


func _assert_ungenerated_submap_placeholder(runner, submap_id: String, message: String) -> void:
	var game_session = runner.get_session().get_game_session()
	_assert_true(game_session != null, "%s | GameSession 应可访问。" % message)
	if game_session == null:
		return
	var world_data: Dictionary = game_session.get_world_data()
	var submaps_variant: Variant = world_data.get("mounted_submaps", {})
	_assert_true(submaps_variant is Dictionary, "%s | mounted_submaps 应为 Dictionary。" % message)
	if submaps_variant is not Dictionary:
		return
	var submaps := submaps_variant as Dictionary
	var submap_variant: Variant = submaps.get(submap_id, {})
	_assert_true(submap_variant is Dictionary, "%s | 应存在子地图 %s。" % [message, submap_id])
	if submap_variant is not Dictionary:
		return
	var submap := submap_variant as Dictionary
	_assert_true(not bool(submap.get("is_generated", true)), "%s | 子地图不应被标记为已生成。" % message)
	var submap_world_data_variant: Variant = submap.get("world_data", null)
	_assert_true(submap_world_data_variant is Dictionary, "%s | 子地图 world_data 占位应为 Dictionary。" % message)
	if submap_world_data_variant is Dictionary:
		_assert_true((submap_world_data_variant as Dictionary).is_empty(), "%s | 未生成子地图 world_data 必须保持空字典。" % message)


func _assert_command_ok(runner, command_text: String) -> void:
	var result = await runner.execute_line(command_text)
	_assert_true(bool(result.ok), "命令失败：%s | %s" % [command_text, String(result.message)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
