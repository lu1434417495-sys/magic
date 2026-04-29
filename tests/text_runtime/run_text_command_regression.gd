# End-to-end regression for the headless text command chain.
# It protects runtime flows while the main game remains UI-driven.
extends SceneTree

const EquipmentRequirement = preload("res://scripts/player/equipment/equipment_requirement.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const SkillBookItemFactory = preload("res://scripts/player/warehouse/skill_book_item_factory.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")
const GAME_TEXT_COMMAND_RUNNER_SCRIPT = preload("res://scripts/systems/game_runtime/headless/game_text_command_runner.gd")
const BATTLE_LOOT_COMMIT_SCENARIO_PATH := "res://tests/text_runtime/scenarios/battle_loot_commit.txt"
const BATTLE_LOOT_OVERFLOW_SCENARIO_PATH := "res://tests/text_runtime/scenarios/battle_loot_overflow.txt"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var runner = GAME_TEXT_COMMAND_RUNNER_SCRIPT.new()
	await runner.initialize()

	await _run_command(runner, "game new test")
	runner.get_session().get_runtime_facade().get_party_state().set_gold(600)
	_inject_extended_test_settlement_services(runner)
	await _run_command(runner, "world open")
	var invalid_settlement_action_result = await _run_command_expect_fail(runner, "settlement action service:missing")
	_assert_true(invalid_settlement_action_result.message.contains("未开放该服务"), "未开放的 settlement action 应返回明确错误。")
	await _run_command(runner, "settlement action service:basic_supply interaction_script_id=service_research facility_name=伪造图书馆 npc_name=伪造导师 service_type=研究")
	_assert_eq(String(runner.get_session().build_snapshot().get("modal", {}).get("id", "")), "shop", "伪造 interaction_script_id 时文本命令仍应按真实服务入口落到商店 modal。")
	await _run_command(runner, "close")
	await _run_command(runner, "settlement action service:research interaction_script_id=service_research facility_name=大图书馆 npc_name=大图书官 service_type=研究")
	_assert_research_reward_queued_while_settlement_open(runner.get_session().build_snapshot(), runner.get_session().build_text_snapshot())
	await _run_command(runner, "settlement action service:research interaction_script_id=service_research facility_name=大图书馆 npc_name=大图书官 service_type=研究")
	_assert_second_research_reward_queued_while_settlement_open(runner.get_session().build_snapshot(), runner.get_session().build_text_snapshot())
	await _run_command(runner, "close")
	_assert_research_reward_presented_after_modal_close(runner.get_session().build_snapshot(), runner.get_session().build_text_snapshot())
	await _assert_eventually_present_research_reward(runner, "裂甲斩", "entry=skill_unlock | warrior_guard_break")
	await _drain_visible_rewards(runner)

	await _run_command(runner, "game new test")
	_inject_extended_test_settlement_services(runner)
	_inject_submit_item_contract(runner.get_session().get_game_session())
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
	await _run_command(runner, "warehouse add iron_ore 2")
	await _run_command(runner, "world open")
	await _run_command(runner, "settlement action service:contract_board interaction_script_id=service_contract_board facility_name=公告板 npc_name=告示书记员 service_type=任务")
	_assert_contract_board_modal_open(runner.get_session().build_snapshot(), runner.get_session().build_text_snapshot())
	await _run_command(runner, "settlement action service:contract_board submission_source=contract_board quest_id=contract_manual_drill interaction_script_id=service_contract_board facility_name=公告板 npc_name=告示书记员 service_type=任务 provider_interaction_id=service_contract_board")
	_assert_contract_board_accept_applied(runner.get_session().build_snapshot(), runner.get_session().build_text_snapshot(), "contract_manual_drill", "训练记录")
	await _run_command(runner, "settlement action service:contract_board submission_source=contract_board quest_id=contract_supply_drop interaction_script_id=service_contract_board facility_name=公告板 npc_name=告示书记员 service_type=任务 provider_interaction_id=service_contract_board")
	_assert_contract_board_accept_applied(runner.get_session().build_snapshot(), runner.get_session().build_text_snapshot(), "contract_supply_drop", "物资缴纳")
	await _run_command(runner, "settlement action service:contract_board submission_source=contract_board quest_id=contract_supply_drop interaction_script_id=service_contract_board facility_name=公告板 npc_name=告示书记员 service_type=任务 provider_interaction_id=service_contract_board")
	_assert_contract_board_submit_item_applied(runner.get_session().build_snapshot(), runner.get_session().build_text_snapshot(), "contract_supply_drop", "iron_ore")
	var duplicate_contract_result = await runner.execute_line("settlement action service:contract_board submission_source=contract_board quest_id=contract_manual_drill interaction_script_id=service_contract_board facility_name=公告板 npc_name=告示书记员 service_type=任务 provider_interaction_id=service_contract_board")
	if not duplicate_contract_result.skipped:
		print(duplicate_contract_result.render())
	_assert_contract_board_duplicate_feedback(runner.get_session().build_snapshot())
	await _run_command(runner, "close")
	_assert_contract_board_closed_to_settlement(runner.get_session().build_snapshot())
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
	await _run_expect_scenario_file(BATTLE_LOOT_COMMIT_SCENARIO_PATH)
	await _run_expect_scenario_file(BATTLE_LOOT_OVERFLOW_SCENARIO_PATH)
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
	_assert_battle_start_prompt_present_in_text_snapshot(runner.get_session().build_text_snapshot())
	await _run_command(runner, "battle confirm")
	_assert_battle_command_log_contains_post_state(runner.get_session().build_snapshot(), "battle.confirm_start")
	await _advance_to_manual_battle_turn(runner)
	await _assert_battle_skill_selection_blockers_in_text_runtime(runner)
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


func _advance_to_manual_battle_turn(runner, max_ticks: int = 64) -> void:
	for _index in range(max_ticks):
		var battle_snapshot: Dictionary = runner.get_session().build_snapshot().get("battle", {})
		if not bool(battle_snapshot.get("active", false)):
			break
		var active_unit_id := String(battle_snapshot.get("active_unit_id", ""))
		var active_unit := _find_unit(battle_snapshot.get("units", []), active_unit_id)
		if String(active_unit.get("control_mode", "")) == "manual":
			return
		await _run_command(runner, "battle tick 1.0")
	_assert_true(false, "文本 battle blocker 回归未能进入手动单位回合。")


func _assert_battle_skill_selection_blockers_in_text_runtime(runner) -> void:
	_prime_active_manual_skill_blocker(runner, 1, 0)
	var stamina_result = await _run_command_expect_fail(runner, "battle skill 1")
	_assert_true(stamina_result.message.contains("体力不足"), "文本 battle skill 选择在耐力不足时应返回明确错误。")
	_assert_eq(String(stamina_result.snapshot.get("battle", {}).get("selected_skill_id", "")), "", "耐力不足时不应写入 selected_skill_id。")
	var stamina_hud: Dictionary = stamina_result.snapshot.get("battle", {}).get("hud", {})
	var stamina_slots: Array = stamina_hud.get("skill_slots", [])
	var stamina_slot: Dictionary = stamina_slots[0] if not stamina_slots.is_empty() and stamina_slots[0] is Dictionary else {}
	_assert_eq(String(stamina_slot.get("footer_text", "")), "ST不足", "耐力不足时 headless HUD skill slot footer 应显示 ST不足。")
	_assert_eq(String(stamina_slot.get("disabled_reason", "")), "体力不足", "耐力不足时 headless HUD skill slot 应暴露体力不足原因。")
	_assert_true(stamina_result.snapshot_text.contains("体力不足"), "耐力不足时文本快照应保留阻断文案。")

	_prime_active_manual_skill_blocker(runner, 12, 10)
	var cooldown_result = await _run_command_expect_fail(runner, "battle skill 1")
	_assert_true(cooldown_result.message.contains("冷却"), "文本 battle skill 选择在冷却未结束时应返回明确错误。")
	_assert_eq(String(cooldown_result.snapshot.get("battle", {}).get("selected_skill_id", "")), "", "冷却未结束时不应写入 selected_skill_id。")
	var cooldown_hud: Dictionary = cooldown_result.snapshot.get("battle", {}).get("hud", {})
	var cooldown_slots: Array = cooldown_hud.get("skill_slots", [])
	var cooldown_slot: Dictionary = cooldown_slots[0] if not cooldown_slots.is_empty() and cooldown_slots[0] is Dictionary else {}
	_assert_eq(String(cooldown_slot.get("footer_text", "")), "CD 10", "冷却未结束时 headless HUD skill slot footer 应显示剩余 CD。")
	_assert_true(String(cooldown_slot.get("disabled_reason", "")).contains("冷却"), "冷却未结束时 headless HUD skill slot 应暴露冷却原因。")
	_assert_true(cooldown_result.snapshot_text.contains("冷却"), "冷却未结束时文本快照应保留阻断文案。")


func _assert_battle_start_prompt_present_in_text_snapshot(text_snapshot: String) -> void:
	var battle_section_index := text_snapshot.find("[BATTLE]\n")
	var selected_target_index := text_snapshot.find("selected_target_unit_count=", battle_section_index)
	var confirm_visible_index := text_snapshot.find("start_confirm_visible=true", battle_section_index)
	var prompt_title_index := text_snapshot.find("start_prompt_title=开始战斗", battle_section_index)
	var prompt_description_index := text_snapshot.find("start_prompt_description=是否开始战斗？确认后 TU 将按每秒 5 点推进。", battle_section_index)
	var prompt_confirm_index := text_snapshot.find("start_prompt_confirm_text=开始战斗", battle_section_index)
	var hud_header_index := text_snapshot.find("hud_header=", battle_section_index)
	_assert_true(battle_section_index >= 0, "文本快照应包含 BATTLE 分段。")
	_assert_true(confirm_visible_index >= 0, "battle-start confirm 打开时文本快照应渲染 start_confirm_visible。")
	_assert_true(prompt_title_index >= 0, "battle-start confirm 打开时文本快照应渲染提示标题。")
	_assert_true(prompt_description_index >= 0, "battle-start confirm 打开时文本快照应渲染提示说明。")
	_assert_true(prompt_confirm_index >= 0, "battle-start confirm 打开时文本快照应渲染确认按钮文案。")
	_assert_true(selected_target_index >= 0 and selected_target_index < confirm_visible_index, "新增 battle start confirm 字段不应打乱前置目标字段顺序。")
	_assert_true(confirm_visible_index < prompt_title_index, "start_confirm_visible 应先于 start_prompt_title 输出。")
	_assert_true(prompt_title_index < prompt_description_index, "start_prompt_title 应先于 start_prompt_description 输出。")
	_assert_true(prompt_description_index < prompt_confirm_index, "start_prompt_description 应先于 start_prompt_confirm_text 输出。")
	_assert_true(hud_header_index < 0 or prompt_confirm_index < hud_header_index, "新增 battle start prompt 字段不应打乱 HUD 字段顺序。")


func _prime_active_manual_skill_blocker(runner, current_stamina: int, cooldown: int) -> void:
	var facade = runner.get_session().get_runtime_facade()
	_assert_true(facade != null, "文本 battle blocker 回归前置：runtime facade 应存在。")
	if facade == null:
		return
	var battle_state = facade.get_battle_state()
	_assert_true(battle_state != null and not battle_state.is_empty(), "文本 battle blocker 回归前置：battle state 应存在。")
	if battle_state == null or battle_state.is_empty():
		return
	var active_unit = battle_state.units.get(battle_state.active_unit_id)
	_assert_true(active_unit != null, "文本 battle blocker 回归前置：当前行动单位应存在。")
	if active_unit == null:
		return
	active_unit.known_active_skill_ids = ProgressionDataUtils.to_string_name_array(["archer_long_draw"])
	active_unit.known_skill_level_map.clear()
	active_unit.known_skill_level_map[&"archer_long_draw"] = 1
	active_unit.current_ap = 2
	active_unit.current_stamina = current_stamina
	active_unit.cooldowns.clear()
	if cooldown > 0:
		active_unit.cooldowns[&"archer_long_draw"] = cooldown
	if active_unit.attribute_snapshot != null:
		active_unit.attribute_snapshot.set_value(&"action_points", 2)
		active_unit.attribute_snapshot.set_value(&"stamina_max", maxi(current_stamina, 2))
	facade.command_battle_clear_skill()
	facade.refresh_battle_selection_state()


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


func _run_expect_scenario_file(scenario_path: String) -> void:
	var file := FileAccess.open(scenario_path, FileAccess.READ)
	_assert_true(file != null, "应能读取文本场景 %s。" % scenario_path)
	if file == null:
		return
	var lines: PackedStringArray = file.get_as_text().split("\n", false)
	file.close()
	var runner = GAME_TEXT_COMMAND_RUNNER_SCRIPT.new()
	await runner.initialize()
	for line_index in range(lines.size()):
		var command_text := String(lines[line_index])
		var result = await runner.execute_line(command_text)
		if result.skipped:
			continue
		print("SCENARIO %s LINE %d\n%s" % [scenario_path.get_file(), line_index + 1, result.render()])
		_assert_true(result.ok, "文本场景失败：%s:%d | %s | %s" % [
			scenario_path,
			line_index + 1,
			command_text,
			result.message,
		])
		if not result.ok:
			await runner.dispose(true)
			return
	await runner.dispose(true)


func _assert_settlement_reward_queued_while_warehouse_open(snapshot: Dictionary) -> void:
	var party_snapshot: Dictionary = snapshot.get("party", {})
	var reward_snapshot: Dictionary = snapshot.get("reward", {})
	var member: Dictionary = _find_party_member(party_snapshot.get("members", []), "player_sword_01")
	var achievement_summary: Dictionary = member.get("achievement_summary", {})
	_assert_eq(int(party_snapshot.get("pending_reward_count", 0)), 1, "据点动作成功后应先把成就奖励加入待处理队列。")
	_assert_true(not bool(reward_snapshot.get("visible", false)), "共享仓库打开时奖励弹窗应等待，不应抢占当前模态。")
	_assert_eq(int(achievement_summary.get("unlocked_count", 0)), 1, "据点动作应推进当前角色的据点成就。")
	_assert_eq(String(achievement_summary.get("recent_unlocked_name", "")), "行路借火", "最近解锁成就应记录据点成就。")


func _assert_research_reward_queued_while_settlement_open(snapshot: Dictionary, text_snapshot: String) -> void:
	var settlement_snapshot: Dictionary = snapshot.get("settlement", {})
	var party_snapshot: Dictionary = snapshot.get("party", {})
	var reward_snapshot: Dictionary = snapshot.get("reward", {})
	var reward_data: Dictionary = reward_snapshot.get("reward", {})
	_assert_eq(String(snapshot.get("modal", {}).get("id", "")), "settlement", "research 完成后应先保留 settlement modal。")
	_assert_true(bool(settlement_snapshot.get("visible", false)), "research 完成后据点窗口应继续保持打开。")
	_assert_true(int(party_snapshot.get("pending_reward_count", 0)) >= 1, "research 完成后应先把奖励加入待处理队列。")
	_assert_true(not bool(reward_snapshot.get("visible", false)), "settlement modal 打开时 research 奖励不应提前弹窗。")
	_assert_eq(String(reward_data.get("source_label", "")), "大图书官·研究", "research 奖励快照应保留正式来源标签。")
	_assert_eq(String(reward_data.get("member_id", "")), "player_sword_01", "research 奖励快照应保留当前成员。")
	_assert_true(String(reward_data.get("summary_text", "")).find("野外手册") >= 0, "research 奖励快照应保留成果摘要。")
	_assert_true(String(settlement_snapshot.get("feedback_text", "")).find("野外手册") >= 0, "据点反馈应包含 research 成果名称。")
	_assert_true(text_snapshot.contains("[REWARD]"), "文本快照应包含 reward 分段。")
	_assert_true(text_snapshot.contains("[REWARD]\nvisible=false"), "文本快照应标记 research 奖励尚未弹出。")
	_assert_true(text_snapshot.contains("source_label=大图书官·研究"), "文本快照应渲染 research 奖励来源。")
	_assert_true(text_snapshot.contains("entry=knowledge_unlock | field_manual"), "文本快照应渲染 research 奖励条目。")


func _assert_research_reward_presented_after_modal_close(snapshot: Dictionary, text_snapshot: String) -> void:
	var reward_snapshot: Dictionary = snapshot.get("reward", {})
	var reward_data: Dictionary = reward_snapshot.get("reward", {})
	_assert_eq(String(snapshot.get("modal", {}).get("id", "")), "reward", "关闭 settlement 后 research 奖励应进入正式 reward modal。")
	_assert_true(bool(reward_snapshot.get("visible", false)), "关闭 settlement 后 research 奖励应立即显示。")
	_assert_true(int(snapshot.get("party", {}).get("pending_reward_count", 0)) >= 1, "research 奖励展示时待处理数量应至少保留 research 奖励本身。")
	_assert_eq(String(reward_data.get("source_label", "")), "大图书官·研究", "research 奖励弹窗应保留正式来源标签。")
	_assert_true(String(reward_data.get("summary_text", "")).find("野外手册") >= 0, "research 奖励弹窗应保留成果摘要。")
	_assert_true(text_snapshot.contains("[REWARD]\nvisible=true"), "文本快照应标记 research 奖励已经弹出。")
	_assert_true(text_snapshot.contains("entry=knowledge_unlock | field_manual"), "文本快照应继续渲染 research 奖励条目。")


func _assert_second_research_reward_queued_while_settlement_open(snapshot: Dictionary, text_snapshot: String) -> void:
	var settlement_snapshot: Dictionary = snapshot.get("settlement", {})
	var party_snapshot: Dictionary = snapshot.get("party", {})
	var reward_snapshot: Dictionary = snapshot.get("reward", {})
	_assert_eq(String(snapshot.get("modal", {}).get("id", "")), "settlement", "第二次 research 完成后仍应保留 settlement modal。")
	_assert_true(bool(settlement_snapshot.get("visible", false)), "第二次 research 完成后据点窗口应继续保持打开。")
	_assert_true(int(party_snapshot.get("pending_reward_count", 0)) >= 2, "连续两次 research 后待处理队列里至少应累积两条待确认奖励。")
	_assert_true(not bool(reward_snapshot.get("visible", false)), "settlement modal 打开时第二条 research 奖励也不应抢先弹窗。")
	_assert_true(String(settlement_snapshot.get("feedback_text", "")).find("裂甲斩") >= 0, "第二次 research 应直接切到下一条成果，而不是重复野外手册。")
	_assert_true(text_snapshot.contains("[REWARD]\nvisible=false"), "第二次 research 时文本快照仍应标记 reward 未显示。")


func _assert_eventually_present_research_reward(
	runner,
	summary_fragment: String,
	text_entry_fragment: String,
	max_confirms: int = 6
) -> void:
	for _index in range(max_confirms):
		var snapshot: Dictionary = runner.get_session().build_snapshot()
		var reward_snapshot: Dictionary = snapshot.get("reward", {})
		var reward_data: Dictionary = reward_snapshot.get("reward", {})
		var summary_text := String(reward_data.get("summary_text", ""))
		var text_snapshot: String = runner.get_session().build_text_snapshot()
		if bool(reward_snapshot.get("visible", false)) and summary_text.find(summary_fragment) >= 0:
			_assert_eq(String(snapshot.get("modal", {}).get("id", "")), "reward", "目标 research 奖励显示时 modal 应为 reward。")
			_assert_true(text_snapshot.contains(text_entry_fragment), "目标 research 奖励文本快照应渲染对应条目。")
			return
		if not bool(reward_snapshot.get("visible", false)):
			break
		await _run_command(runner, "reward confirm")
	_assert_true(false, "未能在保护次数内等到目标 research 奖励：%s。" % summary_fragment)


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
		var after_state: Dictionary = log_context.get("runtime", {})
		_assert_eq(String(after_state.get("active_modal_id", "")), "forge", "通用 forge 日志后态应保留 forge modal。")
	_assert_true(text_snapshot.contains("[FORGE]"), "通用 forge 文本快照应包含 forge 分段。")


func _assert_generic_forge_persisted_after_load(snapshot: Dictionary) -> void:
	_assert_eq(_count_warehouse_item(snapshot, "bronze_sword"), 0, "重新载入后不应恢复已消耗的青铜短剑。")
	_assert_eq(_count_warehouse_item(snapshot, "iron_ore"), 0, "重新载入后不应恢复已消耗的铁矿石。")
	_assert_eq(_count_warehouse_item(snapshot, "iron_greatsword"), 1, "重新载入后应保留通用 forge 产出的铁制大剑。")


func _assert_contract_board_modal_open(snapshot: Dictionary, text_snapshot: String) -> void:
	var contract_board_snapshot: Dictionary = snapshot.get("contract_board", {})
	var window_data: Dictionary = contract_board_snapshot.get("window_data", {})
	_assert_true(bool(contract_board_snapshot.get("visible", false)), "任务板服务打开后应切换到 contract_board modal。")
	_assert_true(not bool(snapshot.get("settlement", {}).get("visible", false)), "任务板打开时 settlement modal 应隐藏。")
	_assert_eq(String(snapshot.get("modal", {}).get("id", "")), "contract_board", "任务板打开后 modal 应为 contract_board。")
	_assert_eq(String(window_data.get("action_id", "")), "service:contract_board", "任务板 modal 应保留原始 action_id。")
	_assert_true((window_data.get("entries", []) as Array).size() >= 3, "任务板 modal 应至少渲染首批 contract quest。")
	_assert_true(text_snapshot.contains("[CONTRACT_BOARD]"), "文本快照应包含 contract board 分段。")
	_assert_true(text_snapshot.contains("首轮狩猎"), "文本快照应渲染任务板中的契约名称。")


func _assert_bounty_registry_modal_open(snapshot: Dictionary, text_snapshot: String) -> void:
	var contract_board_snapshot: Dictionary = snapshot.get("contract_board", {})
	var window_data: Dictionary = contract_board_snapshot.get("window_data", {})
	var bounty_entry := _find_contract_board_entry(window_data.get("entries", []), "contract_regional_bounty")
	_assert_true(bool(contract_board_snapshot.get("visible", false)), "悬赏署打开后应复用 contract_board modal。")
	_assert_eq(String(snapshot.get("modal", {}).get("id", "")), "contract_board", "悬赏署打开后当前 modal 仍应为 contract_board。")
	_assert_eq(String(window_data.get("action_id", "")), "service:bounty_registry", "悬赏署 modal 应保留原始 action_id。")
	_assert_eq(String(window_data.get("provider_interaction_id", "")), "service_bounty_registry", "悬赏署 modal 应记录自己的 provider_interaction_id。")
	_assert_eq((window_data.get("entries", []) as Array).size(), 1, "悬赏署 modal 只应渲染自己的 bounty 条目。")
	_assert_true(not bounty_entry.is_empty(), "悬赏署 modal 应渲染悬赏 provider 对应的契约。")
	_assert_true(_find_contract_board_entry(window_data.get("entries", []), "contract_manual_drill").is_empty(), "悬赏署 modal 不应混入 contract board 契约。")
	_assert_true(text_snapshot.contains("provider_interaction_id=service_bounty_registry"), "文本快照应标记悬赏署 provider_interaction_id。")
	_assert_true(text_snapshot.contains("地区悬赏"), "文本快照应渲染悬赏 provider 的契约名称。")


func _assert_contract_board_closed_to_settlement(snapshot: Dictionary) -> void:
	_assert_true(bool(snapshot.get("settlement", {}).get("visible", false)), "关闭任务板后应恢复 settlement modal。")
	_assert_true(not bool(snapshot.get("contract_board", {}).get("visible", false)), "关闭任务板后 contract_board modal 应隐藏。")
	_assert_eq(String(snapshot.get("modal", {}).get("id", "")), "settlement", "关闭任务板后当前 modal 应回到 settlement。")


func _assert_contract_board_accept_applied(snapshot: Dictionary, text_snapshot: String, quest_id: String, quest_label: String) -> void:
	var status_snapshot: Dictionary = snapshot.get("status", {})
	var contract_board_snapshot: Dictionary = snapshot.get("contract_board", {})
	var window_data: Dictionary = contract_board_snapshot.get("window_data", {})
	var accepted_entry := _find_contract_board_entry(window_data.get("entries", []), quest_id)
	_assert_true(bool(contract_board_snapshot.get("visible", false)), "任务板接取后应继续停留在 contract_board modal。")
	_assert_true((snapshot.get("party", {}).get("quests", {}).get("active_quest_ids", []) as Array).has(quest_id), "任务板接取后 PartyState.active_quests 应包含该任务。")
	_assert_eq(String(status_snapshot.get("text", "")), "已接取任务《%s》。" % quest_label, "任务板接取后状态栏应显示正式 quest accept 成功反馈。")
	_assert_eq(String(window_data.get("summary_text", "")), "已接取任务《%s》。" % quest_label, "任务板接取后 contract board summary_text 应刷新为最新反馈。")
	_assert_eq(String(accepted_entry.get("state_id", "")), "active", "任务板接取后条目应刷新为 active。")
	_assert_true(text_snapshot.contains("quest=%s | stage=active" % quest_id), "文本快照应渲染任务板接取后的 active 任务明细。")


func _assert_contract_board_submit_item_applied(snapshot: Dictionary, text_snapshot: String, quest_id: String, item_id: String) -> void:
	var status_snapshot: Dictionary = snapshot.get("status", {})
	var contract_board_snapshot: Dictionary = snapshot.get("contract_board", {})
	var window_data: Dictionary = contract_board_snapshot.get("window_data", {})
	var submitted_entry := _find_contract_board_entry(window_data.get("entries", []), quest_id)
	_assert_true(bool(contract_board_snapshot.get("visible", false)), "submit_item 提交后应继续停留在 contract_board modal。")
	_assert_eq(String(status_snapshot.get("text", "")), "已为任务《物资缴纳》提交 铁矿石 x2，奖励待领取。", "submit_item 提交后状态栏应显示正式扣料反馈。")
	_assert_eq(String(window_data.get("summary_text", "")), "已为任务《物资缴纳》提交 铁矿石 x2，奖励待领取。", "submit_item 提交后 contract board summary_text 应同步反馈。")
	_assert_eq(String(submitted_entry.get("state_id", "")), "claimable", "submit_item 提交后条目应刷新为 claimable。")
	_assert_eq(_count_warehouse_item(snapshot, item_id), 0, "submit_item 提交成功后共享仓库应扣除对应物资。")
	_assert_true(text_snapshot.contains("quest=%s | stage=claimable" % quest_id), "文本快照应渲染 submit_item 任务进入 claimable。")


func _assert_contract_board_duplicate_feedback(snapshot: Dictionary) -> void:
	var status_snapshot: Dictionary = snapshot.get("status", {})
	var window_data: Dictionary = snapshot.get("contract_board", {}).get("window_data", {})
	_assert_eq(String(status_snapshot.get("text", "")), "任务《训练记录》已在进行中，不能重复接取。", "任务板重复接取时应显示明确反馈。")
	_assert_eq(String(window_data.get("summary_text", "")), "任务《训练记录》已在进行中，不能重复接取。", "任务板重复接取后 summary_text 应同步反馈。")


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
	_assert_true((quests_snapshot.get("claimable_quest_ids", []) as Array).has(quest_id), "quest complete 后待领奖励任务列表应包含该任务。")
	_assert_true(not (quests_snapshot.get("completed_quest_ids", []) as Array).has(quest_id), "quest complete 后不应直接进入 completed_quest_ids。")
	_assert_true(text_snapshot.contains("claimable_quest_ids=%s" % quest_id), "文本快照应渲染待领奖励任务 ID。")
	_assert_true(text_snapshot.contains("quest=%s | stage=claimable" % quest_id), "文本快照应渲染待领奖励任务明细。")


func _assert_settlement_quest_event_applied(snapshot: Dictionary, text_snapshot: String) -> void:
	var quests_snapshot: Dictionary = snapshot.get("party", {}).get("quests", {})
	_assert_true((quests_snapshot.get("claimable_quest_ids", []) as Array).has("contract_settlement_warehouse"), "真实据点动作应自动把仓储巡查任务推进到待领奖励。")
	_assert_true((quests_snapshot.get("claimable_quest_ids", []) as Array).has("contract_manual_drill"), "文本回归中的手动完成任务应继续停留在待领奖励列表。")
	_assert_true(text_snapshot.contains("claimable_quest_ids=contract_manual_drill contract_settlement_warehouse") or text_snapshot.contains("claimable_quest_ids=contract_settlement_warehouse contract_manual_drill"), "文本快照应渲染真实据点动作完成后的待领奖励任务列表。")


func _assert_equipment_command_applied(before_snapshot: Dictionary, after_snapshot: Dictionary) -> void:
	var before_member: Dictionary = _find_party_member(before_snapshot.get("party", {}).get("members", []), "player_sword_01")
	var after_member: Dictionary = _find_party_member(after_snapshot.get("party", {}).get("members", []), "player_sword_01")
	var before_attributes: Dictionary = before_member.get("attributes", {})
	var after_attributes: Dictionary = after_member.get("attributes", {})
	var equipped_entry := _find_equipped_item(after_member.get("equipment", []), "main_hand")

	_assert_true(not equipped_entry.is_empty(), "命令行装备后，队员快照中应出现主手装备条目。")
	_assert_eq(String(equipped_entry.get("item_id", "")), "bronze_sword", "主手槽应装备青铜短剑。")
	_assert_eq(
		int(after_attributes.get("attack_bonus", 0)) - int(before_attributes.get("attack_bonus", 0)),
		2,
		"命令行装备后，攻击检定加值快照应同步提升。"
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
		int(after_attributes.get("attack_bonus", 0)),
		int(before_attributes.get("attack_bonus", 0)),
		"命令行卸装后，攻击检定加值快照应回到基线。"
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
	var after_state: Dictionary = context.get("runtime", {})
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


func _find_contract_board_entry(entry_variants, quest_id: String) -> Dictionary:
	if entry_variants is not Array:
		return {}
	for entry_variant in entry_variants:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("quest_id", entry.get("entry_id", ""))) == quest_id:
			return entry.duplicate(true)
	return {}


func _inject_submit_item_contract(game_session) -> void:
	if game_session == null:
		return
	var submit_item_quest := QuestDef.new()
	submit_item_quest.quest_id = &"contract_supply_drop"
	submit_item_quest.display_name = "物资缴纳"
	submit_item_quest.description = "向任务板提交两份铁矿石。"
	submit_item_quest.provider_interaction_id = &"service_contract_board"
	submit_item_quest.objective_defs = [
		{
			"objective_id": "deliver_ore",
			"objective_type": QuestDef.OBJECTIVE_SUBMIT_ITEM,
			"target_id": "iron_ore",
			"target_value": 2,
		},
	]
	submit_item_quest.reward_entries = [
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 18},
	]
	game_session.get_quest_defs()[submit_item_quest.quest_id] = submit_item_quest


func _inject_extended_test_settlement_services(runner) -> void:
	if runner == null:
		return
	var session = runner.get_session()
	if session == null:
		return
	var runtime = session.get_runtime_facade()
	if runtime == null:
		return
	var selected_settlement: Dictionary = runtime.get_selected_settlement()
	var settlement_id := String(selected_settlement.get("settlement_id", ""))
	if settlement_id.is_empty():
		return
	var world_data: Dictionary = runtime.get_world_data()
	var settlement_variants = world_data.get("settlements", [])
	if settlement_variants is not Array:
		return
	var extra_services: Array[Dictionary] = [
		{
			"action_id": "service:contract_board",
			"facility_id": "notice_board",
			"facility_template_id": "notice_board",
			"facility_name": "公告板",
			"npc_id": "npc_notice_keeper",
			"npc_template_id": "npc_notice_keeper",
			"npc_name": "告示书记员",
			"service_type": "任务",
			"interaction_script_id": "service_contract_board",
		},
		{
			"action_id": "service:bounty_registry",
			"facility_id": "bounty_registry",
			"facility_template_id": "bounty_registry",
			"facility_name": "悬赏署",
			"npc_id": "npc_bounty_clerk",
			"npc_template_id": "npc_bounty_clerk",
			"npc_name": "悬赏文书",
			"service_type": "悬赏",
			"interaction_script_id": "service_bounty_registry",
		},
		{
			"action_id": "service:research",
			"facility_id": "grand_library",
			"facility_template_id": "grand_library",
			"facility_name": "大图书馆",
			"npc_id": "npc_librarian",
			"npc_template_id": "npc_librarian",
			"npc_name": "大图书官",
			"service_type": "研究",
			"interaction_script_id": "service_research",
		},
	]
	for index in range(settlement_variants.size()):
		var settlement_variant = settlement_variants[index]
		if settlement_variant is not Dictionary:
			continue
		var settlement_data: Dictionary = settlement_variant
		if String(settlement_data.get("settlement_id", "")) != settlement_id:
			continue
		var available_services_variant = settlement_data.get("available_services", [])
		var available_services: Array = []
		if available_services_variant is Array:
			available_services = (available_services_variant as Array).duplicate(true)
		for extra_service in extra_services:
			_upsert_test_settlement_service(available_services, extra_service)
		settlement_data["available_services"] = available_services
		settlement_variants[index] = settlement_data
		break
	world_data["settlements"] = settlement_variants


func _upsert_test_settlement_service(service_variants: Array, service_data: Dictionary) -> void:
	var action_id := String(service_data.get("action_id", ""))
	for index in range(service_variants.size()):
		var existing_variant = service_variants[index]
		if existing_variant is not Dictionary:
			continue
		if String((existing_variant as Dictionary).get("action_id", "")) != action_id:
			continue
		service_variants[index] = service_data.duplicate(true)
		return
	service_variants.append(service_data.duplicate(true))


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


func _drain_visible_rewards(runner, max_confirms: int = 6) -> void:
	var guard := 0
	while guard < max_confirms:
		var snapshot: Dictionary = runner.get_session().build_snapshot()
		if not bool(snapshot.get("reward", {}).get("visible", false)):
			return
		await _run_command(runner, "reward confirm")
		guard += 1
	_assert_true(false, "research 奖励链路超出保护确认次数。")


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
