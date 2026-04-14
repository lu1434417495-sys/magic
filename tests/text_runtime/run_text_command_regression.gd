extends SceneTree

const EquipmentRequirement = preload("res://scripts/player/equipment/equipment_requirement.gd")
const GAME_TEXT_COMMAND_RUNNER_SCRIPT = preload("res://scripts/systems/game_text_command_runner.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var runner = GAME_TEXT_COMMAND_RUNNER_SCRIPT.new()
	await runner.initialize()

	await _run_command(runner, "game new test")
	await _run_command(runner, "warehouse add bronze_sword 1")
	await _run_command(runner, "warehouse add skill_book_archer_aimed_shot 1")
	var before_equip_snapshot: Dictionary = runner.get_session().build_snapshot()
	await _run_command(runner, "party equip player_sword_01 bronze_sword")
	_assert_equipment_command_applied(before_equip_snapshot, runner.get_session().build_snapshot())
	await _run_command(runner, "party unequip player_sword_01 main_hand")
	_assert_equipment_command_reverted(before_equip_snapshot, runner.get_session().build_snapshot())
	await _assert_equipment_requirement_error_message(runner)
	await _run_command(runner, "party open")
	await _run_command(runner, "party select player_sword_01")
	await _run_command(runner, "party warehouse")
	await _run_command(runner, "warehouse use skill_book_archer_aimed_shot player_sword_01")
	_assert_skill_book_command_applied(runner.get_session().build_snapshot())
	await _run_command(runner, "close")
	await _run_command(runner, "world open")
	await _run_command(runner, "settlement action service:warehouse")
	_assert_settlement_reward_queued_while_warehouse_open(runner.get_session().build_snapshot())
	await _run_command(runner, "close")
	_assert_settlement_reward_presented_after_modal_close(runner.get_session().build_snapshot())
	await _run_command(runner, "reward confirm")
	_assert_settlement_reward_confirmed(runner.get_session().build_snapshot())

	var snapshot: Dictionary = runner.get_session().build_snapshot()
	var nearby_encounters: Array = snapshot.get("world", {}).get("nearby_encounters", [])
	_assert_true(not nearby_encounters.is_empty(), "测试世界应暴露至少一个附近遭遇。")
	if nearby_encounters.is_empty():
		_finish()
		return

	var target_coord: Dictionary = nearby_encounters[0]
	await _walk_to_coord(runner, target_coord.get("coord", {}))
	await _exercise_battle_flow(runner)
	_finish()


func _walk_to_coord(runner, coord_data: Dictionary) -> void:
	var target_x := int(coord_data.get("x", 0))
	var target_y := int(coord_data.get("y", 0))
	var guard := 0
	while guard < 256:
		var snapshot: Dictionary = runner.get_session().build_snapshot()
		if bool(snapshot.get("battle", {}).get("active", false)):
			return
		var player_coord: Dictionary = snapshot.get("world", {}).get("player_coord", {})
		var current_x := int(player_coord.get("x", 0))
		var current_y := int(player_coord.get("y", 0))
		if current_x == target_x and current_y == target_y:
			return
		if current_x < target_x:
			await _run_command(runner, "world move right")
		elif current_x > target_x:
			await _run_command(runner, "world move left")
		elif current_y < target_y:
			await _run_command(runner, "world move down")
		else:
			await _run_command(runner, "world move up")
		guard += 1
	_assert_true(false, "走向遭遇点时超出保护步数。")


func _exercise_battle_flow(runner) -> void:
	var guard := 0
	while guard < 64:
		var snapshot: Dictionary = runner.get_session().build_snapshot()
		if bool(snapshot.get("battle", {}).get("active", false)):
			break
		await _run_command(runner, "battle tick 1.0")
		guard += 1
	_assert_true(bool(runner.get_session().build_snapshot().get("battle", {}).get("active", false)), "进入遭遇后应切入战斗。")
	if not bool(runner.get_session().build_snapshot().get("battle", {}).get("active", false)):
		return

	var battle_snapshot: Dictionary = runner.get_session().build_snapshot().get("battle", {})
	_assert_true(not (battle_snapshot.get("units", []) as Array).is_empty(), "战斗快照应包含单位列表。")
	await _run_command(runner, "battle tick 1.0")
	await _run_command(runner, "battle wait")
	var units: Array = runner.get_session().build_snapshot().get("battle", {}).get("units", [])
	if not units.is_empty():
		var unit: Variant = units[0]
		if unit is Dictionary:
			var coord: Dictionary = unit.get("coord", {})
			await _run_command(runner, "battle inspect %d %d" % [
				int(coord.get("x", 0)),
				int(coord.get("y", 0)),
			])
			await _run_command(runner, "close")


func _assert_settlement_reward_queued_while_warehouse_open(snapshot: Dictionary) -> void:
	var party_snapshot: Dictionary = snapshot.get("party", {})
	var reward_snapshot: Dictionary = snapshot.get("reward", {})
	var member: Dictionary = _find_party_member(party_snapshot.get("members", []), "player_sword_01")
	var achievement_summary: Dictionary = member.get("achievement_summary", {})
	_assert_eq(int(party_snapshot.get("pending_reward_count", 0)), 1, "据点动作成功后应先把成就奖励加入待处理队列。")
	_assert_true(not bool(reward_snapshot.get("visible", false)), "共享仓库打开时奖励弹窗应等待，不应抢占当前模态。")
	_assert_eq(int(achievement_summary.get("unlocked_count", 0)), 1, "据点动作应推进当前角色的据点成就。")
	_assert_eq(String(achievement_summary.get("recent_unlocked_name", "")), "行路借火", "最近解锁成就应记录据点成就。")


func _assert_settlement_reward_presented_after_modal_close(snapshot: Dictionary) -> void:
	var reward_snapshot: Dictionary = snapshot.get("reward", {})
	var reward: Dictionary = reward_snapshot.get("reward", {})
	_assert_true(bool(reward_snapshot.get("visible", false)), "关闭共享仓库后应立即展示待确认的角色奖励。")
	_assert_eq(String(reward.get("member_id", "")), "player_sword_01", "据点动作奖励应绑定到当前执行角色。")
	_assert_eq(String(reward.get("source_label", "")), "行路借火", "奖励来源名称应显示解锁的成就名。")


func _assert_settlement_reward_confirmed(snapshot: Dictionary) -> void:
	var party_snapshot: Dictionary = snapshot.get("party", {})
	var reward_snapshot: Dictionary = snapshot.get("reward", {})
	var member: Dictionary = _find_party_member(party_snapshot.get("members", []), "player_sword_01")
	var achievement_summary: Dictionary = member.get("achievement_summary", {})
	_assert_eq(int(party_snapshot.get("pending_reward_count", 0)), 0, "确认奖励后待处理队列应被清空。")
	_assert_true(not bool(reward_snapshot.get("visible", false)), "奖励确认后弹窗应关闭。")
	_assert_eq(int(achievement_summary.get("unlocked_count", 0)), 1, "奖励确认后成就解锁状态应继续保留。")


func _assert_equipment_command_applied(before_snapshot: Dictionary, after_snapshot: Dictionary) -> void:
	var before_member: Dictionary = _find_party_member(before_snapshot.get("party", {}).get("members", []), "player_sword_01")
	var after_member: Dictionary = _find_party_member(after_snapshot.get("party", {}).get("members", []), "player_sword_01")
	var before_attributes: Dictionary = before_member.get("attributes", {})
	var after_attributes: Dictionary = after_member.get("attributes", {})
	var equipped_entry := _find_equipped_item(after_member.get("equipment", []), "main_hand")

	_assert_true(not equipped_entry.is_empty(), "命令行装备后，队员快照中应出现主手装备条目。")
	_assert_eq(String(equipped_entry.get("item_id", "")), "bronze_sword", "主手槽应装备青铜短剑。")
	_assert_eq(
		int(after_attributes.get("physical_attack", 0)) - int(before_attributes.get("physical_attack", 0)),
		4,
		"命令行装备后，物攻快照应同步提升。"
	)


func _assert_equipment_command_reverted(before_snapshot: Dictionary, after_snapshot: Dictionary) -> void:
	var before_member: Dictionary = _find_party_member(before_snapshot.get("party", {}).get("members", []), "player_sword_01")
	var after_member: Dictionary = _find_party_member(after_snapshot.get("party", {}).get("members", []), "player_sword_01")
	var before_attributes: Dictionary = before_member.get("attributes", {})
	var after_attributes: Dictionary = after_member.get("attributes", {})

	_assert_true(
		_find_equipped_item(after_member.get("equipment", []), "main_hand").is_empty(),
		"命令行卸装后，主手槽应被清空。"
	)
	_assert_eq(
		int(after_attributes.get("physical_attack", 0)),
		int(before_attributes.get("physical_attack", 0)),
		"命令行卸装后，物攻快照应回到基线。"
	)
	_assert_eq(_count_session_warehouse_item("bronze_sword"), 1, "卸装后物品应回到共享仓库。")


func _assert_skill_book_command_applied(snapshot: Dictionary) -> void:
	var member: Dictionary = _find_party_member(snapshot.get("party", {}).get("members", []), "player_sword_01")
	var learned_skill_ids: Array = member.get("learned_skill_ids", [])
	_assert_true(
		learned_skill_ids.has("archer_aimed_shot"),
		"命令行使用技能书后，角色快照中应出现已学会的技能 ID。"
	)
	_assert_eq(_count_warehouse_item(snapshot, "skill_book_archer_aimed_shot"), 0, "命令行使用技能书后，仓库中的技能书应被消耗。")


func _assert_equipment_requirement_error_message(runner) -> void:
	var item_defs: Dictionary = runner.get_session().get_game_session().get_item_defs()
	var bronze_sword = item_defs.get(&"bronze_sword")
	_assert_true(bronze_sword != null, "文本回归前置：bronze_sword 定义应存在。")
	if bronze_sword == null:
		return

	var blocked_sword = bronze_sword.duplicate()
	var requirement := EquipmentRequirement.new()
	requirement.required_profession_ids = ["__blocked_profession__"]
	blocked_sword.equip_requirement = requirement
	item_defs[&"bronze_sword"] = blocked_sword

	var result = await _run_command_expect_fail(runner, "party equip player_sword_01 bronze_sword")
	_assert_true(result.message.contains("职业不满足"), "资格失败时应返回职业要求文案。")
	_assert_true(result.message.contains("青铜短剑"), "资格失败文案应包含物品名称。")
	_assert_eq(_count_session_warehouse_item("bronze_sword"), 1, "资格失败时不应消耗仓库中的装备。")

	item_defs[&"bronze_sword"] = bronze_sword


func _find_party_member(members: Array, member_id: String) -> Dictionary:
	for member_variant in members:
		if member_variant is not Dictionary:
			continue
		var member: Dictionary = member_variant
		if String(member.get("member_id", "")) == member_id:
			return member
	return {}


func _find_equipped_item(equipment_entries: Array, slot_id: String) -> Dictionary:
	for entry_variant in equipment_entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("slot_id", "")) == slot_id:
			return entry
	return {}


func _count_warehouse_item(snapshot: Dictionary, item_id: String) -> int:
	var stacks: Array = snapshot.get("warehouse", {}).get("window_data", {}).get("stacks", [])
	for stack_variant in stacks:
		if stack_variant is not Dictionary:
			continue
		var stack: Dictionary = stack_variant
		if String(stack.get("item_id", "")) == item_id:
			return int(stack.get("total_quantity", 0))
	return 0


func _count_session_warehouse_item(item_id: String) -> int:
	var scene_tree := Engine.get_main_loop() as SceneTree
	if scene_tree == null:
		return 0
	var session = scene_tree.root.get_node_or_null("GameSession")
	if session == null:
		return 0
	var party_state = session.get_party_state()
	if party_state == null or party_state.warehouse_state == null:
		return 0
	var total := 0
	for stack in party_state.warehouse_state.stacks:
		if stack == null:
			continue
		if String(stack.item_id) != item_id:
			continue
		total += int(stack.quantity)
	for instance in party_state.warehouse_state.equipment_instances:
		if instance == null:
			continue
		if String(instance.item_id) != item_id:
			continue
		total += 1
	return total


func _find_unit(units: Array, unit_id: String) -> Dictionary:
	for unit_variant in units:
		if unit_variant is not Dictionary:
			continue
		var unit: Dictionary = unit_variant
		if String(unit.get("unit_id", "")) == unit_id:
			return unit
	return {}


func _find_first_enemy(units: Array, active_faction_id: String) -> Dictionary:
	for unit_variant in units:
		if unit_variant is not Dictionary:
			continue
		var unit: Dictionary = unit_variant
		if not bool(unit.get("is_alive", false)):
			continue
		if String(unit.get("faction_id", "")) == active_faction_id:
			continue
		return unit
	return {}


func _get_first_promotion_choice_id(snapshot: Dictionary) -> String:
	var choices: Array = snapshot.get("promotion", {}).get("prompt", {}).get("choices", [])
	if choices.is_empty():
		return ""
	var first_choice = choices[0]
	if first_choice is not Dictionary:
		return ""
	return String(first_choice.get("profession_id", ""))


func _run_command(runner, command_text: String) -> void:
	var result = await runner.execute_line(command_text)
	if result.skipped:
		return
	print(result.render())
	_assert_true(result.ok, "命令失败：%s | %s" % [command_text, result.message])


func _run_command_expect_fail(runner, command_text: String):
	var result = await runner.execute_line(command_text)
	if result.skipped:
		return result
	print(result.render())
	_assert_true(not result.ok, "命令本应失败：%s" % command_text)
	return result


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _finish() -> void:
	if _failures.is_empty():
		print("Text command regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Text command regression: FAIL (%d)" % _failures.size())
	quit(1)
