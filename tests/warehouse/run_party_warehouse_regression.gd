## 文件说明：该脚本属于队伍仓库回归执行相关的回归测试脚本，集中维护失败信息、游戏会话等顶层字段。
## 审查重点：重点核对测试数据、字段用途、断言条件和失败提示是否仍然覆盖目标回归场景。
## 备注：后续如果业务规则变化，需要同步更新测试夹具、预期结果和失败信息。

extends SceneTree

const GameSessionScript = preload("res://scripts/systems/game_session.gd")
const WorldMapScene = preload("res://scenes/main/world_map.tscn")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const UnitProgress = preload("res://scripts/player/progression/unit_progress.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")
const WarehouseState = preload("res://scripts/player/warehouse/warehouse_state.gd")
const WarehouseStackState = preload("res://scripts/player/warehouse/warehouse_stack_state.gd")
const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const SkillBookItemFactory = preload("res://scripts/player/warehouse/skill_book_item_factory.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const PartyWarehouseService = preload("res://scripts/systems/party_warehouse_service.gd")
const CharacterManagementModule = preload("res://scripts/systems/character_management_module.gd")
const PartyItemUseService = preload("res://scripts/systems/party_item_use_service.gd")

const TEST_CONFIG_PATH := "res://data/configs/world_map/test_world_map_config.tres"
## 字段说明：记录测试过程中收集到的失败信息，便于最终集中输出并快速定位回归点。
var _failures: Array[String] = []
## 字段说明：记录游戏会话，用于构造测试场景、记录结果并支撑回归断言。
var _game_session = null


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _ensure_game_session()
	await _test_warehouse_service_rules()
	await _test_inventory_entries_include_equipment_instances()
	await _test_batch_swap_commit_is_atomic()
	await _test_quest_reward_item_materializer()
	await _test_skill_book_generation_and_use_rules()
	await _test_party_state_requires_current_schema()
	await _test_item_registry_validation()
	await _test_save_round_trip()
	await _test_world_map_entry_paths()
	await _cleanup()

	if _failures.is_empty():
		print("Party warehouse regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Party warehouse regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_warehouse_service_rules() -> void:
	var item_defs: Dictionary = _game_session.get_item_defs()
	var empty_service := PartyWarehouseService.new()
	empty_service.setup(PartyState.new(), item_defs)
	_assert_eq(empty_service.get_total_capacity(), 0, "空队伍仓库容量应为 0。")

	var preview_party := _build_party_with_members([
		_build_member_state(&"preview_only", "预览成员", 2),
	])
	preview_party.warehouse_state = null
	var preview_service := PartyWarehouseService.new()
	preview_service.setup(preview_party, item_defs)
	var preview_result := preview_service.preview_add_item(&"healing_herb", 4)
	_assert_eq(int(preview_result.get("added_quantity", 0)), 4, "预览加入应返回可加入数量。")
	_assert_true(preview_party.warehouse_state == null, "preview_add_item 不应为缺失仓库状态的队伍创建仓库。")

	var missing_attr_party := _build_party_with_members([
		_build_member_state(&"missing_attr", "无容量成员", 0, false),
	])
	var missing_attr_service := PartyWarehouseService.new()
	missing_attr_service.setup(missing_attr_party, item_defs)
	_assert_eq(missing_attr_service.get_total_capacity(), 0, "缺少 storage_space 的成员不应贡献容量。")

	var negative_attr_party := _build_party_with_members([
		_build_member_state(&"negative_attr", "负容量成员", -3, true),
	])
	var negative_attr_service := PartyWarehouseService.new()
	negative_attr_service.setup(negative_attr_party, item_defs)
	_assert_eq(negative_attr_service.get_total_capacity(), 0, "负 storage_space 应按 0 处理。")

	var party := _build_party_with_members([
		_build_member_state(&"frontline", "前卫", 2),
		_build_member_state(&"reserve", "后备", 4),
	])
	party.active_member_ids = [ &"frontline" ]
	party.reserve_member_ids = [ &"reserve" ]
	party.leader_member_id = &"frontline"

	var service := PartyWarehouseService.new()
	service.setup(party, item_defs)
	_assert_eq(service.get_total_capacity(), 6, "仓库容量应累计全部成员的 storage_space。")
	party.active_member_ids = [ &"reserve" ]
	party.reserve_member_ids = [ &"frontline" ]
	_assert_eq(service.get_total_capacity(), 6, "上阵与替补切换不应影响容量统计范围。")

	var sword_result: Dictionary = service.add_item(&"bronze_sword", 3)
	_assert_eq(int(sword_result.get("added_quantity", 0)), 3, "不可堆叠物品应能正常加入。")
	_assert_eq(service.get_used_slots(), 3, "不可堆叠物品应始终一件一堆。")

	var herb_preview := service.preview_add_item(&"healing_herb", 25)
	var herb_add := service.add_item(&"healing_herb", 25)
	_assert_eq(
		int(herb_preview.get("remaining_quantity", -1)),
		int(herb_add.get("remaining_quantity", -2)),
		"preview_add_item 与 add_item 的剩余数量计算应一致。"
	)
	_assert_eq(service.count_item(&"healing_herb"), 25, "可堆叠物品加入后应正确累计数量。")
	_assert_eq(service.get_used_slots(), 5, "25 份治疗草应占用两个堆栈。")

	var second_preview := service.preview_add_item(&"healing_herb", 30)
	var second_add := service.add_item(&"healing_herb", 30)
	_assert_eq(
		int(second_preview.get("remaining_quantity", -1)),
		int(second_add.get("remaining_quantity", -2)),
		"二次补堆时 preview_add_item 与 add_item 的剩余数量仍应一致。"
	)
	_assert_eq(service.count_item(&"healing_herb"), 55, "补堆后治疗草总数应正确。")
	_assert_eq(service.get_used_slots(), 6, "补堆并开新堆后应正确占满剩余格位。")

	var over_capacity_party := _build_party_with_members([
		_build_member_state(&"porter", "搬运员", 2),
	])
	var over_capacity_service := PartyWarehouseService.new()
	over_capacity_service.setup(over_capacity_party, item_defs)
	var cap_result := over_capacity_service.add_item(&"healing_herb", 45)
	_assert_eq(int(cap_result.get("added_quantity", 0)), 40, "容量不足时应允许部分加入。")
	_assert_eq(int(cap_result.get("remaining_quantity", 0)), 5, "容量不足时应返回剩余未加入数量。")

	var porter_attributes: UnitBaseAttributes = over_capacity_party.get_member_state(&"porter").progression.unit_base_attributes
	porter_attributes.custom_stats[&"storage_space"] = 1
	_assert_true(over_capacity_service.is_over_capacity(), "容量下降后 used_slots > total_capacity 应进入超容状态。")

	var blocked_stack_preview := over_capacity_service.preview_add_item(&"bronze_sword", 1)
	_assert_eq(int(blocked_stack_preview.get("added_quantity", 0)), 0, "超容状态下不应继续新增堆栈。")

	var remove_result := over_capacity_service.remove_item(&"healing_herb", 1)
	_assert_eq(int(remove_result.get("removed_quantity", 0)), 1, "超容状态下仍应允许移除物品。")

	var refill_preview := over_capacity_service.preview_add_item(&"healing_herb", 2)
	var refill_add := over_capacity_service.add_item(&"healing_herb", 2)
	_assert_eq(
		int(refill_preview.get("remaining_quantity", -1)),
		int(refill_add.get("remaining_quantity", -2)),
		"超容状态下补已有堆栈时，preview_add_item 与 add_item 应保持一致。"
	)
	_assert_eq(int(refill_add.get("added_quantity", 0)), 1, "超容状态下只能补已有未满堆栈，不能新增堆栈。")
	_assert_true(over_capacity_service.is_over_capacity(), "补已有堆栈后若仍超容，状态应保持超容。")


func _test_party_state_requires_current_schema() -> void:
	var party_state = PartyState.from_dict({
		"version": 2,
		"leader_member_id": "guard_member",
		"active_member_ids": ["guard_member"],
		"reserve_member_ids": [],
		"member_states": {},
		"warehouse_state": null,
	})
	_assert_true(party_state == null, "缺少当前 warehouse_state schema 的 PartyState 不再支持。")


func _test_batch_swap_commit_is_atomic() -> void:
	var item_defs: Dictionary = _game_session.get_item_defs()
	var party := _build_party_with_members([
		_build_member_state(&"porter", "搬运员", 1),
	])
	var service := PartyWarehouseService.new()
	service.setup(party, item_defs)
	service.add_item(&"iron_greatsword", 1)

	var before_signature := _stack_signature(party)
	var commit_result := service.commit_batch_swap(
		[&"iron_greatsword"],
		[&"bronze_sword", &"scout_charm"]
	)
	_assert_true(not bool(commit_result.get("allowed", false)), "容量不足时 commit_batch_swap 应整体失败。")
	_assert_eq(commit_result.get("error_code", ""), "warehouse_blocked_swap", "失败错误码应为 warehouse_blocked_swap。")
	_assert_eq(_stack_signature(party), before_signature, "commit_batch_swap 失败后仓库状态应完整回滚。")
	_assert_eq(service.count_item(&"iron_greatsword"), 1, "失败后原始待装备物不应被吞掉。")
	_assert_eq(service.count_item(&"bronze_sword"), 0, "失败后不应写入部分回仓物。")
	_assert_eq(service.count_item(&"scout_charm"), 0, "失败后不应写入部分回仓物。")
	_assert_eq(service.get_free_slots(), 0, "失败后仓库占用格数应保持不变。")


func _test_quest_reward_item_materializer() -> void:
	var item_defs: Dictionary = _game_session.get_item_defs()
	var quest_defs := {
		&"contract_supply_receipt": _build_test_quest_def(
			&"contract_supply_receipt",
			"补给签收",
			[
				{"reward_type": QuestDef.REWARD_GOLD, "amount": 12},
				{"reward_type": QuestDef.REWARD_ITEM, "item_id": "iron_ore", "quantity": 2},
			]
		),
		&"contract_reward_overflow": _build_test_quest_def(
			&"contract_reward_overflow",
			"仓储超额",
			[
				{"reward_type": QuestDef.REWARD_ITEM, "item_id": "bronze_sword", "quantity": 1},
			]
		),
	}

	var party := _build_party_with_members([
		_build_member_state(&"porter", "搬运员", 3),
	])
	var character_management := CharacterManagementModule.new()
	character_management.setup(
		party,
		_game_session.get_skill_defs(),
		_game_session.get_profession_defs(),
		_game_session.get_achievement_defs(),
		item_defs,
		quest_defs
	)
	var claimable_quest := QuestState.new()
	claimable_quest.quest_id = &"contract_supply_receipt"
	claimable_quest.mark_accepted(4)
	claimable_quest.mark_completed(6)
	party.set_claimable_quest_state(claimable_quest)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party, item_defs)

	var claim_result := character_management.claim_quest_reward(&"contract_supply_receipt", 8)
	_assert_true(bool(claim_result.get("ok", false)), "item reward 任务应能正式写入共享仓库。")
	_assert_eq(int(claim_result.get("gold_delta", 0)), 12, "item reward 任务应继续暴露 gold_delta。")
	_assert_eq(_extract_item_reward_quantity(claim_result.get("item_rewards", []), "iron_ore"), 2, "item reward 结果应暴露写入仓库的物品条目。")
	_assert_eq(warehouse_service.count_item(&"iron_ore"), 2, "item reward claim 后共享仓库应新增铁矿石。")
	_assert_eq(party.get_gold(), 12, "item reward claim 后金币奖励应继续写入 PartyState。")
	_assert_true(not party.has_claimable_quest(&"contract_supply_receipt"), "item reward claim 后任务应离开 claimable_quests。")
	_assert_true(party.has_completed_quest(&"contract_supply_receipt"), "item reward claim 后任务应进入 completed_quest_ids。")

	var overflow_party := _build_party_with_members([
		_build_member_state(&"porter", "搬运员", 1),
	])
	var overflow_warehouse_service := PartyWarehouseService.new()
	overflow_warehouse_service.setup(overflow_party, item_defs)
	overflow_warehouse_service.add_item(&"bronze_sword", 1)
	var overflow_character_management := CharacterManagementModule.new()
	overflow_character_management.setup(
		overflow_party,
		_game_session.get_skill_defs(),
		_game_session.get_profession_defs(),
		_game_session.get_achievement_defs(),
		item_defs,
		quest_defs
	)
	var overflow_quest := QuestState.new()
	overflow_quest.quest_id = &"contract_reward_overflow"
	overflow_quest.mark_accepted(5)
	overflow_quest.mark_completed(7)
	overflow_party.set_claimable_quest_state(overflow_quest)

	var overflow_result := overflow_character_management.claim_quest_reward(&"contract_reward_overflow", 9)
	_assert_true(not bool(overflow_result.get("ok", true)), "容量不足时 quest item reward claim 应正式失败。")
	_assert_eq(String(overflow_result.get("error_code", "")), "reward_overflow", "容量不足时 quest item reward claim 应返回 reward_overflow。")
	_assert_true(overflow_party.has_claimable_quest(&"contract_reward_overflow"), "容量不足时任务应继续停留在 claimable_quests。")
	_assert_true(not overflow_party.has_completed_quest(&"contract_reward_overflow"), "容量不足时任务不应误写入 completed_quest_ids。")
	_assert_eq(overflow_warehouse_service.count_item(&"bronze_sword"), 1, "容量不足时不应静默吞掉或部分写入奖励物品。")


func _test_inventory_entries_include_equipment_instances() -> void:
	var item_defs: Dictionary = _game_session.get_item_defs()
	var party := _build_party_with_members([
		_build_member_state(&"porter", "搬运员", 4),
	])
	var service := PartyWarehouseService.new()
	service.setup(party, item_defs)
	service.add_item(&"bronze_sword", 2)
	service.add_item(&"healing_herb", 7)

	var entries := service.get_inventory_entries()
	var sword_entry := _find_inventory_entry(entries, "bronze_sword")
	var herb_entry := _find_inventory_entry(entries, "healing_herb")

	_assert_true(not sword_entry.is_empty(), "展示条目中应包含装备实例聚合项。")
	_assert_eq(int(sword_entry.get("quantity", 0)), 2, "装备实例聚合项应反映实例数量。")
	_assert_eq(String(sword_entry.get("storage_mode", "")), "instance", "装备条目应标记为 instance 存储模式。")
	_assert_true(not bool(sword_entry.get("is_stackable", true)), "装备条目不应被误标为可堆叠。")

	_assert_true(not herb_entry.is_empty(), "展示条目中应保留普通堆叠物品。")
	_assert_eq(int(herb_entry.get("quantity", 0)), 7, "普通堆叠物品应保留堆叠数量。")
	_assert_eq(String(herb_entry.get("storage_mode", "")), "stack", "普通物品条目应标记为 stack 存储模式。")


func _test_skill_book_generation_and_use_rules() -> void:
	var item_defs: Dictionary = _game_session.get_item_defs()
	var skill_book_item_id := SkillBookItemFactory.build_item_id_for_skill(&"archer_aimed_shot")
	var item_def = item_defs.get(skill_book_item_id)
	_assert_true(item_def != null, "book 来源技能应自动生成对应技能书物品。")
	if item_def == null:
		return
	_assert_true(item_def.is_skill_book(), "自动生成的技能书物品应带有技能书分类。")
	_assert_eq(item_def.granted_skill_id, &"archer_aimed_shot", "技能书物品应指向正确的技能 ID。")

	var party := _build_party_with_members([
		_build_member_state(&"reader", "读者", 3),
	])
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party, item_defs)
	var add_result := warehouse_service.add_item(skill_book_item_id, 1)
	_assert_eq(int(add_result.get("added_quantity", 0)), 1, "技能书应能加入共享仓库。")

	var character_management := CharacterManagementModule.new()
	character_management.setup(
		party,
		_game_session.get_skill_defs(),
		_game_session.get_profession_defs(),
		_game_session.get_achievement_defs(),
		item_defs
	)
	var item_use_service := PartyItemUseService.new()
	item_use_service.setup(
		party,
		item_defs,
		_game_session.get_skill_defs(),
		warehouse_service,
		character_management
	)

	var first_use_result := item_use_service.use_item(skill_book_item_id, &"reader")
	_assert_true(bool(first_use_result.get("success", false)), "技能书首次使用应成功。")
	_assert_eq(warehouse_service.count_item(skill_book_item_id), 0, "技能书成功使用后应消耗 1 本。")
	var skill_progress = party.get_member_state(&"reader").progression.get_skill_progress(&"archer_aimed_shot")
	_assert_true(skill_progress != null and skill_progress.is_learned, "技能书应让目标角色真正学会对应技能。")

	warehouse_service.add_item(skill_book_item_id, 1)
	var second_use_result := item_use_service.use_item(skill_book_item_id, &"reader")
	_assert_true(not bool(second_use_result.get("success", false)), "已学会同技能后再次使用技能书应失败。")
	_assert_eq(
		ProgressionDataUtils.to_string_name(second_use_result.get("reason", "")),
		&"learn_failed",
		"重复学习失败时应返回 learn_failed，便于上层给出明确提示。"
	)
	_assert_eq(warehouse_service.count_item(skill_book_item_id), 1, "重复学习失败时不应吞掉技能书库存。")


func _test_item_registry_validation() -> void:
	var registry := ItemContentRegistry.new()
	registry._validation_errors.clear()
	registry._scan_directory("res://data/configs/__missing_items_registry__")
	var validation_errors := registry.validate()
	var found_missing_dir_error := false
	for validation_error in validation_errors:
		if validation_error.contains("__missing_items_registry__"):
			found_missing_dir_error = true
			break
	_assert_true(
		found_missing_dir_error,
		"物品注册表缺少目录时应记录显式校验错误。"
	)


func _test_save_round_trip() -> void:
	await _reset_session()
	var create_error := int(_game_session.create_new_save(TEST_CONFIG_PATH, &"warehouse_roundtrip", "仓库回写测试"))
	_assert_eq(create_error, OK, "创建仓库存档测试世界应成功。")
	if create_error != OK:
		return

	var party_state: PartyState = _game_session.get_party_state()
	party_state.warehouse_state = WarehouseState.new()
	party_state.warehouse_state.stacks = [
		_build_stack(&"healing_herb", 7),
		_build_stack(&"iron_ore", 12),
	]
	party_state.warehouse_state.equipment_instances = [
		EquipmentInstanceState.create(&"bronze_sword"),
	]

	var persist_error := int(_game_session.set_party_state(party_state))
	_assert_eq(persist_error, OK, "写入带仓库数据的 PartyState 应成功。")
	if persist_error != OK:
		return

	var save_id: String = _game_session.get_active_save_id()
	var load_error := int(_game_session.load_save(save_id))
	_assert_eq(load_error, OK, "带仓库数据的存档应能重新加载。")
	if load_error != OK:
		return

	var loaded_party_state: PartyState = _game_session.get_party_state()
	_assert_eq(
		_stack_signature(loaded_party_state),
		["healing_herb:7", "iron_ore:12"],
		"新存档 round-trip 后，普通仓库堆栈顺序、item_id 与数量应保持一致。"
	)
	_assert_eq(
		_instance_signature(loaded_party_state),
		["bronze_sword"],
		"新存档 round-trip 后，装备实例列表应保持不变。"
	)


func _test_world_map_entry_paths() -> void:
	await _reset_session()
	var create_error := int(_game_session.create_new_save(TEST_CONFIG_PATH, &"warehouse_ui", "仓库入口测试"))
	_assert_eq(create_error, OK, "创建 UI 入口测试世界应成功。")
	if create_error != OK:
		return

	var book_skill := _pick_unlearned_book_skill_for_member(_game_session, &"player_sword_01")
	_assert_true(not book_skill.is_empty(), "UI 仓库回归前置：应能为主角找到一个尚未学会且可生成技能书的技能。")
	if book_skill.is_empty():
		return

	var target_skill_id := ProgressionDataUtils.to_string_name(book_skill.get("skill_id", ""))
	var skill_book_item_id := ProgressionDataUtils.to_string_name(book_skill.get("item_id", ""))
	var party_state: PartyState = _game_session.get_party_state()
	var prefill_service := PartyWarehouseService.new()
	prefill_service.setup(party_state, _game_session.get_item_defs())
	prefill_service.add_item(skill_book_item_id, 1)
	var persist_error := int(_game_session.set_party_state(party_state))
	_assert_eq(persist_error, OK, "UI 测试前写入预置仓库物品应成功。")
	if persist_error != OK:
		return

	var world_map := WorldMapScene.instantiate()
	root.add_child(world_map)
	await process_frame
	await process_frame

	_press_key(KEY_P)
	await process_frame
	await process_frame
	var management = world_map.get_node("PartyManagementWindow")
	var warehouse = world_map.get_node("PartyWarehouseWindow")
	_assert_true(management.visible, "按队伍入口应能打开 PartyManagementWindow。")
	_assert_true(
		not String(management.details_label.text).contains("storage_space"),
		"队伍管理详情不应暴露隐藏属性 storage_space。"
	)

	var warehouse_button := management.get_node(
		"CenterContainer/Panel/MarginContainer/Content/Body/Controls/WarehouseButton"
	) as Button
	warehouse_button.emit_signal("pressed")
	await process_frame
	await process_frame
	_assert_true(warehouse.visible, "队伍管理窗口应能打开共享仓库。")
	_assert_true(not management.visible, "打开共享仓库后不应与队伍管理窗口并存。")
	var use_button := warehouse.get_node(
		"CenterContainer/Panel/MarginContainer/Content/Body/Controls/UseButton"
	) as Button
	_assert_true(use_button != null and not use_button.disabled, "仓库中存在技能书时应允许直接点击使用。")
	if use_button != null and not use_button.disabled:
		use_button.emit_signal("pressed")
		await process_frame
		await process_frame
		var learned_progress = _game_session.get_party_state().get_member_state(&"player_sword_01").progression.get_skill_progress(target_skill_id)
		_assert_true(learned_progress != null and learned_progress.is_learned, "仓库窗口中的使用按钮应让目标成员学会技能书对应技能。")
		var post_use_service := PartyWarehouseService.new()
		post_use_service.setup(_game_session.get_party_state(), _game_session.get_item_defs())
		_assert_eq(post_use_service.count_item(skill_book_item_id), 0, "仓库窗口使用技能书后应同步扣除库存。")

	var warehouse_close_button := warehouse.get_node(
		"CenterContainer/Panel/MarginContainer/Content/Header/CloseButton"
	) as Button
	warehouse_close_button.emit_signal("pressed")
	await process_frame
	await process_frame

	var world_map_view = world_map.get_node("MapViewport/WorldMapView")
	world_map_view.emit_signal("cell_clicked", _game_session.get_player_coord())
	await process_frame
	await process_frame

	var settlement = world_map.get_node("SettlementWindow")
	_assert_true(settlement.visible, "据点入口应能打开 SettlementWindow。")
	var services_container := settlement.get_node(
		"CenterContainer/Panel/MarginContainer/Content/Body/RightColumn/ServicesScroll/ServicesContainer"
	) as VBoxContainer
	var warehouse_service_button: Button = null
	for child in services_container.get_children():
		var button := child as Button
		if button == null:
			continue
		if button.text.contains("仓储"):
			warehouse_service_button = button
			break
	_assert_true(warehouse_service_button != null, "据点窗口中应存在共享仓库服务按钮。")
	if warehouse_service_button != null:
		warehouse_service_button.emit_signal("pressed")
		await process_frame
		await process_frame
		_assert_true(warehouse.visible, "据点服务应能打开同一个共享仓库窗口。")
		_assert_true(not settlement.visible, "打开共享仓库后不应与据点窗口并存。")

	world_map.queue_free()
	await process_frame


func _ensure_game_session() -> void:
	_game_session = root.get_node_or_null("GameSession")
	if _game_session != null:
		return

	_game_session = GameSessionScript.new()
	_game_session.name = "GameSession"
	root.add_child(_game_session)
	await process_frame


func _reset_session() -> void:
	if _game_session == null:
		return
	_game_session.clear_persisted_game()
	await process_frame


func _cleanup() -> void:
	if _game_session == null:
		return
	_game_session.clear_persisted_game()
	if _game_session.get_parent() != null:
		_game_session.get_parent().remove_child(_game_session)
	_game_session.free()
	_game_session = null
	await process_frame


func _press_key(keycode: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = true
	Input.parse_input_event(event)


func _build_party_with_members(members: Array) -> PartyState:
	var party_state := PartyState.new()
	for member_variant in members:
		var member_state := member_variant as PartyMemberState
		if member_state == null:
			continue
		party_state.set_member_state(member_state)
		if party_state.leader_member_id == &"":
			party_state.leader_member_id = member_state.member_id
		if not party_state.active_member_ids.has(member_state.member_id):
			if party_state.active_member_ids.is_empty():
				party_state.active_member_ids.append(member_state.member_id)
			else:
				party_state.reserve_member_ids.append(member_state.member_id)
	return party_state


func _build_member_state(
	member_id: StringName,
	display_name: String,
	storage_space: int,
	set_storage_attribute: bool = true
) -> PartyMemberState:
	var member_state := PartyMemberState.new()
	member_state.member_id = member_id
	member_state.display_name = display_name

	var progression := UnitProgress.new()
	progression.unit_id = member_id
	progression.display_name = display_name

	var unit_base_attributes := UnitBaseAttributes.new()
	if set_storage_attribute:
		unit_base_attributes.custom_stats[&"storage_space"] = storage_space
	progression.unit_base_attributes = unit_base_attributes
	member_state.progression = progression
	return member_state


func _build_test_quest_def(quest_id: StringName, display_name: String, reward_entries: Array) -> QuestDef:
	var quest_def := QuestDef.new()
	quest_def.quest_id = quest_id
	quest_def.display_name = display_name
	quest_def.objective_defs = [
		{
			"objective_id": "warehouse_visit",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_id": "service:warehouse",
			"target_value": 1,
		},
	]
	var typed_reward_entries: Array[Dictionary] = []
	for reward_variant in reward_entries:
		if reward_variant is Dictionary:
			typed_reward_entries.append((reward_variant as Dictionary).duplicate(true))
	quest_def.reward_entries = typed_reward_entries
	return quest_def


func _build_stack(item_id: StringName, quantity: int) -> WarehouseStackState:
	var stack := WarehouseStackState.new()
	stack.item_id = item_id
	stack.quantity = quantity
	return stack


func _extract_item_reward_quantity(item_reward_variants, item_id: String) -> int:
	if item_reward_variants is not Array:
		return 0
	for reward_variant in item_reward_variants:
		if reward_variant is not Dictionary:
			continue
		var reward_data := reward_variant as Dictionary
		if String(reward_data.get("item_id", "")) != item_id:
			continue
		return int(reward_data.get("quantity", 0))
	return 0


func _stack_signature(party_state: PartyState) -> Array[String]:
	var result: Array[String] = []
	if party_state == null or party_state.warehouse_state == null:
		return result
	for stack in party_state.warehouse_state.stacks:
		if stack == null:
			continue
		result.append("%s:%d" % [String(stack.item_id), int(stack.quantity)])
	return result


func _instance_signature(party_state: PartyState) -> Array[String]:
	var result: Array[String] = []
	if party_state == null or party_state.warehouse_state == null:
		return result
	for instance in party_state.warehouse_state.equipment_instances:
		if instance == null:
			continue
		result.append(String(instance.item_id))
	result.sort()
	return result


func _find_inventory_entry(entries: Array, item_id: String) -> Dictionary:
	for entry_variant in entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("item_id", "")) == item_id:
			return entry
	return {}


func _pick_unlearned_book_skill_for_member(game_session, member_id: StringName) -> Dictionary:
	if game_session == null:
		return {}
	var party_state: PartyState = game_session.get_party_state()
	var member_state: PartyMemberState = party_state.get_member_state(member_id) if party_state != null else null
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


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
