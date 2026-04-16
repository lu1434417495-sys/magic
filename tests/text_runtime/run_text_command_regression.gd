# End-to-end regression for the headless text command chain.
# It protects runtime flows while the main game remains UI-driven.
extends SceneTree

const EquipmentRequirement = preload("res://scripts/player/equipment/equipment_requirement.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const SkillBookItemFactory = preload("res://scripts/player/warehouse/skill_book_item_factory.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")
const GAME_TEXT_COMMAND_RUNNER_SCRIPT = preload("res://scripts/systems/game_text_command_runner.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var runner = GAME_TEXT_COMMAND_RUNNER_SCRIPT.new()
	await runner.initialize()

	await _run_command(runner, "game new test")
	_assert_log_snapshot_available(runner)
	_assert_new_game_random_book_skill_grant(runner)
	var book_skill := _pick_unlearned_book_skill_for_member(runner.get_session().get_game_session(), &"player_sword_01")
	_assert_true(not book_skill.is_empty(), "文本命令回归前置：应能为主角找到一个尚未学会且可生成技能书的技能。")
	if book_skill.is_empty():
		await runner.dispose(true)
		_finish()
		return
	var skill_book_skill_id := String(book_skill.get("skill_id", ""))
	var skill_book_item_id := String(book_skill.get("item_id", ""))
	await _run_command(runner, "world open")
	await _run_command(runner, "settlement action service:basic_supply")
	await _run_command(runner, "shop buy healing_herb")
	_assert_shop_purchase_applied(runner.get_session().build_snapshot())
	await _run_command(runner, "close")
	await _run_command(runner, "close")
	await _run_command(runner, "warehouse add bronze_sword 1")
	await _run_command(runner, "warehouse add %s 1" % skill_book_item_id)
	var before_equip_snapshot: Dictionary = runner.get_session().build_snapshot()
	await _run_command(runner, "party equip player_sword_01 bronze_sword")
	_assert_equipment_command_applied(before_equip_snapshot, runner.get_session().build_snapshot())
	await _run_command(runner, "party unequip player_sword_01 main_hand")
	_assert_equipment_command_reverted(before_equip_snapshot, runner.get_session().build_snapshot())
	await _assert_equipment_requirement_error_message(runner)
	await _run_command(runner, "party open")
	await _run_command(runner, "party select player_sword_01")
	await _run_command(runner, "party warehouse")
	await _run_command(runner, "warehouse use %s player_sword_01" % skill_book_item_id)
	_assert_skill_book_command_applied(runner.get_session().build_snapshot(), skill_book_skill_id, skill_book_item_id)
	await _run_command(runner, "close")
	await _run_command(runner, "settlement action service:warehouse")
	_assert_settlement_reward_queued_while_warehouse_open(runner.get_session().build_snapshot())
	await _run_command(runner, "close")
	_assert_settlement_reward_presented_after_modal_close(runner.get_session().build_snapshot())
	await _run_command(runner, "reward confirm")
	_assert_settlement_reward_confirmed(runner.get_session().build_snapshot())
	await _run_command(runner, "quest accept contract_manual_drill")
	await _run_command(runner, "quest progress contract_manual_drill train_once 1 target_value=2 action_id=service:training")
	_assert_quest_progress_command_applied(runner.get_session().build_snapshot(), runner.get_session().build_text_snapshot(), "contract_manual_drill", "train_once", 1, "action_id", "service:training")
	await _run_command(runner, "quest complete contract_manual_drill")
	_assert_quest_complete_command_applied(runner.get_session().build_snapshot(), runner.get_session().build_text_snapshot(), "contract_manual_drill")
	await _run_command(runner, "quest accept contract_settlement_warehouse")
	await _run_command(runner, "world open")
	await _run_command(runner, "settlement action service:warehouse")
	_assert_settlement_quest_event_applied(runner.get_session().build_snapshot(), runner.get_session().build_text_snapshot())
	await _run_command(runner, "close")

	var target_coord: Dictionary = _find_nearest_encounter_coord(runner)
	_assert_true(not target_coord.is_empty(), "测试世界应至少存在一个可到达的遭遇。")
	if target_coord.is_empty():
		await runner.dispose(true)
		_finish()
		return
	await _walk_to_coord(runner, target_coord.get("coord", {}))
	await _exercise_battle_flow(runner)
	await _exercise_generic_forge_flow(runner)
	await runner.dispose(true)
	_finish()


func _find_nearest_encounter_coord(runner) -> Dictionary:
	var runtime = runner.get_session().get_runtime_facade()
	if runtime == null:
		return {}
	var world_data: Dictionary = runtime.get_world_data()
	var player_coord: Vector2i = runtime.get_player_coord()
	var nearest_encounter: ENCOUNTER_ANCHOR_DATA_SCRIPT = null
	var nearest_distance := 2147483647
	for encounter_variant in world_data.get("encounter_anchors", []):
		var encounter := encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter == null or encounter.is_cleared:
			continue
		var delta: Vector2i = encounter.world_coord - player_coord
		var distance := absi(delta.x) + absi(delta.y)
		if distance >= nearest_distance:
			continue
		nearest_distance = distance
		nearest_encounter = encounter
	if nearest_encounter == null:
		return {}
	return {
		"coord": {
			"x": nearest_encounter.world_coord.x,
			"y": nearest_encounter.world_coord.y,
		},
	}


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
	_assert_true(bool(battle_snapshot.get("start_confirm_visible", false)), "进入战斗后应先弹出开始战斗确认。")
	await _run_command(runner, "battle confirm")
	_assert_battle_command_log_contains_post_state(runner.get_session().build_snapshot(), "battle.confirm_start")
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


func _exercise_generic_forge_flow(runner) -> void:
	await _run_command(runner, "game new ashen_intersection")
	var forge_save_id := String(runner.get_session().get_game_session().get_active_save_id())
	_assert_true(not forge_save_id.is_empty(), "通用 forge 回归前置：新存档 ID 应可读取。")
	await _run_command(runner, "warehouse add bronze_sword 1")
	await _run_command(runner, "warehouse add iron_ore 3")
	await _run_command(runner, "world open")
	await _run_command(runner, "settlement action service:repair_gear")
	_assert_generic_forge_modal_open(runner.get_session().build_snapshot(), runner.get_session().build_text_snapshot())
	await _run_command(runner, "settlement action service:repair_gear submission_source=forge recipe_id=forge_smith_iron_greatsword")
	_assert_generic_forge_command_applied(runner.get_session().build_snapshot(), runner.get_session().build_text_snapshot())
	await _run_command(runner, "game load %s" % forge_save_id)
	_assert_generic_forge_persisted_after_load(runner.get_session().build_snapshot())


func _assert_settlement_reward_queued_while_warehouse_open(snapshot: Dictionary) -> void:
	var party_snapshot: Dictionary = snapshot.get("party", {})
	var reward_snapshot: Dictionary = snapshot.get("reward", {})
	var member: Dictionary = _find_party_member(party_snapshot.get("members", []), "player_sword_01")
	var achievement_summary: Dictionary = member.get("achievement_summary", {})
	_assert_eq(int(party_snapshot.get("pending_reward_count", 0)), 1, "据点动作成功后应先把成就奖励加入待处理队列。")
	_assert_true(not bool(reward_snapshot.get("visible", false)), "共享仓库打开时奖励弹窗应等待，不应抢占当前模态。")
	_assert_eq(int(achievement_summary.get("unlocked_count", 0)), 1, "据点动作应推进当前角色的据点成就。")
	_assert_eq(String(achievement_summary.get("recent_unlocked_name", "")), "行路借火", "最近解锁成就应记录据点成就。")


func _assert_generic_forge_modal_open(snapshot: Dictionary, text_snapshot: String) -> void:
	var forge_snapshot: Dictionary = snapshot.get("forge", {})
	var window_data: Dictionary = forge_snapshot.get("window_data", {})
	_assert_true(bool(forge_snapshot.get("visible", false)), "通用 forge 打开后应切换到 forge modal。")
	_assert_eq(String(snapshot.get("modal", {}).get("id", "")), "forge", "通用 forge 打开后 modal 应为 forge。")
	_assert_eq(String(window_data.get("action_id", "")), "service:repair_gear", "通用 forge modal 应保留原始 action_id。")
	_assert_true(not String(window_data.get("title", "")).is_empty(), "通用 forge modal 应提供标题。")
	_assert_true(String(window_data.get("title", "")).find("重铸") == -1, "通用 forge modal 标题不应回退成大师重铸。")
	_assert_true((window_data.get("entries", []) as Array).size() > 0, "通用 forge modal 应暴露至少一个配方条目。")
	_assert_true(text_snapshot.contains("熔炉锻打：铁制大剑"), "文本快照应渲染通用 forge 配方名称。")


func _assert_generic_forge_command_applied(snapshot: Dictionary, text_snapshot: String) -> void:
	var forge_snapshot: Dictionary = snapshot.get("forge", {})
	var settlement_snapshot: Dictionary = snapshot.get("settlement", {})
	_assert_true(bool(forge_snapshot.get("visible", false)), "通用 forge 执行后应继续停留在 forge modal。")
	_assert_eq(_count_warehouse_item(snapshot, "bronze_sword"), 0, "通用 forge 成功后应消耗青铜短剑。")
	_assert_eq(_count_warehouse_item(snapshot, "iron_ore"), 0, "通用 forge 成功后应消耗三份铁矿石。")
	_assert_eq(_count_warehouse_item(snapshot, "iron_greatsword"), 1, "通用 forge 成功后共享仓库应新增铁制大剑。")
	_assert_true(String(settlement_snapshot.get("feedback_text", "")).find("铁制大剑") >= 0, "通用 forge 完成后据点反馈应包含产物名称。")
	var log_entry := _find_log_entry(snapshot, "settlement.execute_action")
	_assert_true(not log_entry.is_empty(), "通用 forge 完成后应写入 settlement.execute_action 日志。")
	if not log_entry.is_empty():
		_assert_true(String(log_entry.get("message", "")).find("铁制大剑") >= 0, "通用 forge 日志文案应包含产物名称。")
		var log_context: Dictionary = log_entry.get("context", {})
		var after_state: Dictionary = log_context.get("after", {})
		_assert_eq(String(after_state.get("active_modal_id", "")), "forge", "通用 forge 日志后态应保留 forge modal。")
	_assert_true(text_snapshot.contains("[FORGE]"), "通用 forge 文本快照应包含 forge 分段。")


func _assert_generic_forge_persisted_after_load(snapshot: Dictionary) -> void:
	_assert_eq(_count_warehouse_item(snapshot, "bronze_sword"), 0, "重新载入后不应恢复已消耗的青铜短剑。")
	_assert_eq(_count_warehouse_item(snapshot, "iron_ore"), 0, "重新载入后不应恢复已消耗的铁矿石。")
	_assert_eq(_count_warehouse_item(snapshot, "iron_greatsword"), 1, "重新载入后应保留通用 forge 产出的铁制大剑。")


func _assert_shop_purchase_applied(snapshot: Dictionary) -> void:
	_assert_true(bool(snapshot.get("shop", {}).get("visible", false)), "商店购买后应继续停留在商店 modal。")
	_assert_eq(int(snapshot.get("party", {}).get("gold", 0)), 168, "购买 1 份治疗草后金币应扣减 12。")
	_assert_eq(_count_warehouse_item(snapshot, "healing_herb"), 1, "购买后共享仓库应新增治疗草。")


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


func _assert_quest_progress_command_applied(snapshot: Dictionary, text_snapshot: String, quest_id: String, objective_id: String, expected_progress: int, context_key: String, context_value: String) -> void:
	var quests_snapshot: Dictionary = snapshot.get("party", {}).get("quests", {})
	var active_quests: Array = quests_snapshot.get("active_quests", [])
	_assert_true((quests_snapshot.get("active_quest_ids", []) as Array).has(quest_id), "quest accept/progress 后快照应暴露激活任务 ID。")
	_assert_eq(active_quests.size(), 1, "quest accept/progress 后应存在 1 条激活任务。")
	if not active_quests.is_empty():
		var quest_entry: Dictionary = active_quests[0]
		var objective_progress: Dictionary = quest_entry.get("objective_progress", {})
		_assert_eq(int(objective_progress.get(objective_id, 0)), expected_progress, "quest progress 命令应写入目标进度。")
		_assert_eq(String((quest_entry.get("last_progress_context", {}) as Dictionary).get(context_key, "")), context_value, "quest progress 命令应保留上下文。")
	_assert_true(text_snapshot.contains("active_quest_ids=%s" % quest_id), "文本快照应渲染激活任务 ID。")
	_assert_true(text_snapshot.contains("quest=%s" % quest_id), "文本快照应渲染任务明细。")


func _assert_quest_complete_command_applied(snapshot: Dictionary, text_snapshot: String, quest_id: String) -> void:
	var quests_snapshot: Dictionary = snapshot.get("party", {}).get("quests", {})
	_assert_true(not (quests_snapshot.get("active_quest_ids", []) as Array).has(quest_id), "quest complete 后激活任务列表应移除该任务。")
	_assert_true((quests_snapshot.get("completed_quest_ids", []) as Array).has(quest_id), "quest complete 后完成任务列表应包含该任务。")
	_assert_true(text_snapshot.contains("completed_quest_ids=%s" % quest_id), "文本快照应渲染已完成任务 ID。")


func _assert_settlement_quest_event_applied(snapshot: Dictionary, text_snapshot: String) -> void:
	var quests_snapshot: Dictionary = snapshot.get("party", {}).get("quests", {})
	_assert_true((quests_snapshot.get("completed_quest_ids", []) as Array).has("contract_settlement_warehouse"), "真实据点动作应自动完成仓储巡查任务。")
	_assert_true(text_snapshot.contains("completed_quest_ids=contract_manual_drill contract_settlement_warehouse") or text_snapshot.contains("completed_quest_ids=contract_settlement_warehouse contract_manual_drill"), "文本快照应渲染真实据点动作完成后的任务列表。")


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
	_assert_eq(_count_warehouse_item(after_snapshot, "bronze_sword"), 1, "命令行卸装后，仓库快照中应能看到回仓的装备。")
	_assert_eq(_count_session_warehouse_item("bronze_sword"), 1, "卸装后物品应回到共享仓库。")


func _assert_skill_book_command_applied(snapshot: Dictionary, skill_id: String, item_id: String) -> void:
	var member: Dictionary = _find_party_member(snapshot.get("party", {}).get("members", []), "player_sword_01")
	var learned_skill_ids: Array = member.get("learned_skill_ids", [])
	_assert_true(
		learned_skill_ids.has(skill_id),
		"命令行使用技能书后，角色快照中应出现本次技能书对应的已学会技能 ID。"
	)
	_assert_eq(_count_warehouse_item(snapshot, item_id), 0, "命令行使用技能书后，仓库中的技能书应被消耗。")


func _assert_new_game_random_book_skill_grant(runner) -> void:
	var game_session = runner.get_session().get_game_session()
	_assert_true(game_session != null, "新游戏随机技能回归前置：GameSession 应可访问。")
	if game_session == null:
		return

	var party_state = game_session.get_party_state()
	var member_state = party_state.get_member_state(&"player_sword_01") if party_state != null else null
	_assert_true(member_state != null and member_state.progression != null, "新游戏后应能读取主角成长数据。")
	if member_state == null or member_state.progression == null:
		return

	var extra_learned_book_skill_ids: Array[StringName] = []
	var skill_defs: Dictionary = game_session.get_skill_defs()
	for skill_key in member_state.progression.skills.keys():
		var skill_id := StringName(String(skill_key))
		if skill_id == &"warrior_heavy_strike":
			continue
		var skill_progress = member_state.progression.get_skill_progress(skill_id)
		if skill_progress == null or not skill_progress.is_learned:
			continue
		var skill_def = skill_defs.get(skill_id)
		if skill_def == null or skill_def.learn_source != &"book":
			continue
		extra_learned_book_skill_ids.append(skill_id)

	_assert_eq(extra_learned_book_skill_ids.size(), 1, "新游戏后主角应额外随机学会 1 个可由技能书学习的技能。")
	if extra_learned_book_skill_ids.size() != 1:
		return

	var granted_skill_id := extra_learned_book_skill_ids[0]
	var granted_skill_def = skill_defs.get(granted_skill_id)
	var granted_skill_progress = member_state.progression.get_skill_progress(granted_skill_id)
	_assert_true(granted_skill_progress != null and granted_skill_progress.is_learned, "随机技能应真正写入主角成长数据。")
	_assert_eq(
		int(granted_skill_progress.skill_level),
		game_session._resolve_random_start_skill_initial_level(granted_skill_def),
		"随机技能等级应按技能层级规则初始化。"
	)


func _pick_unlearned_book_skill_for_member(game_session, member_id: StringName) -> Dictionary:
	if game_session == null:
		return {}
	var party_state = game_session.get_party_state()
	var member_state = party_state.get_member_state(member_id) if party_state != null else null
	if member_state == null or member_state.progression == null:
		return {}
	var skill_defs: Dictionary = game_session.get_skill_defs()
	var item_defs: Dictionary = game_session.get_item_defs()
	for skill_key in ProgressionDataUtils.sorted_string_keys(skill_defs):
		var skill_id := StringName(skill_key)
		var skill_def = skill_defs.get(skill_id)
		if skill_def == null or skill_def.learn_source != &"book":
			continue
		var skill_progress = member_state.progression.get_skill_progress(skill_id)
		if skill_progress != null and skill_progress.is_learned:
			continue
		var item_id := SkillBookItemFactory.build_item_id_for_skill(skill_id)
		if not item_defs.has(item_id):
			continue
		return {
			"skill_id": skill_id,
			"item_id": item_id,
		}
	return {}


func _assert_log_snapshot_available(runner) -> void:
	var snapshot: Dictionary = runner.get_session().build_snapshot()
	var logs_snapshot: Dictionary = snapshot.get("logs", {})
	var entries: Array = logs_snapshot.get("entries", [])
	_assert_true(not String(logs_snapshot.get("file_path", "")).is_empty(), "headless 快照应暴露当前日志文件路径。")
	_assert_true(not entries.is_empty(), "headless 快照应包含最近日志条目。")
	_assert_true(runner.get_session().build_text_snapshot().contains("[LOG]"), "headless 文本快照应包含日志分段。")


func _assert_battle_command_log_contains_post_state(snapshot: Dictionary, event_id: String) -> void:
	var entry := _find_log_entry(snapshot, event_id)
	_assert_true(not entry.is_empty(), "战斗命令 %s 应写入日志。" % event_id)
	if entry.is_empty():
		return
	var context: Dictionary = entry.get("context", {})
	var before_state: Dictionary = context.get("before", {})
	var after_state: Dictionary = context.get("after", {})
	var after_battle: Dictionary = after_state.get("battle", {})
	_assert_true(bool(before_state.get("battle_active", false)), "战斗命令日志应保留命令执行前的战斗态。")
	_assert_true(bool(after_state.get("battle_active", false)), "战斗命令日志应保留命令执行后的战斗态。")
	_assert_true(after_battle.has("seed"), "战斗命令日志后态应包含战斗 seed。")
	_assert_true(not String(after_battle.get("terrain_profile_id", "")).is_empty(), "战斗命令日志后态应包含 terrain profile。")
	_assert_true(not (after_battle.get("units", []) as Array).is_empty(), "战斗命令日志后态应包含单位状态摘要。")


func _find_log_entry(snapshot: Dictionary, event_id: String) -> Dictionary:
	var entries: Array = snapshot.get("logs", {}).get("entries", [])
	for index in range(entries.size() - 1, -1, -1):
		var entry_variant = entries[index]
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("event_id", "")) == event_id:
			return entry
	return {}


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
	var entries: Array = snapshot.get("warehouse", {}).get("window_data", {}).get("entries", [])
	for entry_variant in entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("item_id", "")) == item_id:
			return int(entry.get("total_quantity", 0))
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
