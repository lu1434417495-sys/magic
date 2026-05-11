## 文件说明：该脚本属于队伍仓库回归执行相关的回归测试脚本，集中维护失败信息、游戏会话等顶层字段。
## 审查重点：重点核对测试数据、字段用途、断言条件和失败提示是否仍然覆盖目标回归场景。
## 备注：后续如果业务规则变化，需要同步更新测试夹具、预期结果和失败信息。

extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GameSessionScript = preload("res://scripts/systems/persistence/game_session.gd")
const WorldMapScene = preload("res://scenes/main/world_map.tscn")
const PartyWarehouseWindowScene = preload("res://scenes/ui/party_warehouse_window.tscn")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const UnitProgress = preload("res://scripts/player/progression/unit_progress.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")
const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")
const WarehouseState = preload("res://scripts/player/warehouse/warehouse_state.gd")
const WarehouseStackState = preload("res://scripts/player/warehouse/warehouse_stack_state.gd")
const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const WeaponProfileDef = preload("res://scripts/player/warehouse/weapon_profile_def.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const SkillBookItemFactory = preload("res://scripts/player/warehouse/skill_book_item_factory.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const PartyWarehouseService = preload("res://scripts/systems/inventory/party_warehouse_service.gd")
const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const PartyItemUseService = preload("res://scripts/systems/inventory/party_item_use_service.gd")
const SaveSerializer = preload("res://scripts/systems/persistence/save_serializer.gd")
const SettlementShopService = preload("res://scripts/systems/settlement/settlement_shop_service.gd")

const TEST_CONFIG_PATH := "res://data/configs/world_map/test_world_map_config.tres"
const NEW_CONSUMABLE_ITEM_QUANTITIES := {
	&"bandage_roll": 3,
	&"travel_ration": 5,
	&"torch_bundle": 2,
	&"antidote_herb": 4,
}
const MATERIAL_ITEM_QUANTITIES := {
	&"iron_ore": 6,
	&"beast_hide": 4,
	&"hardwood_lumber": 5,
	&"linen_cloth": 7,
	&"forge_coal": 3,
	&"whetstone": 2,
}
const QUEST_ITEM_QUANTITIES := {
	&"sealed_dispatch": 1,
	&"bandit_insignia": 3,
	&"moonfern_sample": 2,
}
## 字段说明：记录测试过程中收集到的失败信息，便于最终集中输出并快速定位回归点。
var _test := TestRunner.new()
var _failures: Array[String] = _test.failures
## 字段说明：记录游戏会话，用于构造测试场景、记录结果并支撑回归断言。
var _game_session = null


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _ensure_game_session()
	await _test_warehouse_service_rules()
	await _test_world_level_equipment_instance_ids_are_incremental()
	await _test_party_backpack_view_binding_is_battle_local()
	await _test_inventory_entries_include_equipment_instances()
	await _test_equipment_instance_remove_requires_instance_id()
	await _test_weapon_profile_equipment_instances_stack_round_trip()
	await _test_batch_swap_commit_is_atomic()
	await _test_quest_reward_item_materializer()
	await _test_skill_book_generation_and_use_rules()
	await _test_party_state_requires_current_schema()
	await _test_warehouse_state_rejects_bad_schema()
	await _test_party_warehouse_window_display_field_contract()
	await _test_item_registry_validation()
	await _test_new_consumable_seed_warehouse_schema()
	await _test_material_seed_warehouse_schema()
	await _test_quest_item_seed_warehouse_schema()
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

	var equipment_instance_party := _build_party_with_members([
		_build_member_state(&"gear_porter", "装备搬运员", 1),
	])
	var equipment_instance_service := PartyWarehouseService.new()
	equipment_instance_service.setup(equipment_instance_party, item_defs)
	var mace_instance := EquipmentInstanceState.create(&"watchman_mace")
	var instance_add_result: Dictionary = equipment_instance_service.add_equipment_instance(mace_instance)
	_assert_eq(int(instance_add_result.get("added_quantity", 0)), 1, "装备实例容量接口应能写入已有实例。")
	_assert_eq(int(instance_add_result.get("remaining_quantity", 0)), 0, "装备实例容量接口成功时不应返回剩余数量。")
	_assert_eq(equipment_instance_service.count_item(&"watchman_mace"), 1, "装备实例容量接口应保留实例 item_id。")
	var blocked_instance_result: Dictionary = equipment_instance_service.add_equipment_instance(EquipmentInstanceState.create(&"watchman_mace"))
	_assert_eq(int(blocked_instance_result.get("added_quantity", -1)), 0, "背包满时装备实例容量接口不应写入。")
	_assert_eq(int(blocked_instance_result.get("remaining_quantity", 0)), 1, "背包满时装备实例容量接口应返回未放入数量。")
	_assert_eq(equipment_instance_service.count_item(&"watchman_mace"), 1, "背包满时装备实例不应被静默写入。")


func _test_world_level_equipment_instance_ids_are_incremental() -> void:
	await _reset_session()
	var create_error := int(_game_session.create_new_save(TEST_CONFIG_PATH, &"warehouse_world_ids", "装备实例 ID 测试"))
	_assert_eq(create_error, OK, "创建 world-level 装备实例 ID 测试世界应成功。")
	if create_error != OK:
		return

	var party := _build_party_with_members([
		_build_member_state(&"gear_porter", "装备搬运员", 4),
	])
	party.active_member_ids = [&"gear_porter"]
	party.leader_member_id = &"gear_porter"
	party.main_character_member_id = &"gear_porter"
	var seeded_instance := EquipmentInstanceState.create(&"watchman_mace", &"eq_000001")
	party.warehouse_state.equipment_instances = [seeded_instance]
	var persist_error := int(_game_session.set_party_state(party))
	if persist_error == OK:
		persist_error = int(_game_session.commit_runtime_state(&"test.seed_party_equipment"))
	_assert_eq(persist_error, OK, "写入预置装备实例的 PartyState 应成功。")
	if persist_error != OK:
		return

	var allocated_after_collision: StringName = _game_session.allocate_equipment_instance_id()
	_assert_eq(String(allocated_after_collision), "eq_000002", "world-level 分配器应跳过已存在的装备实例 ID。")
	_assert_eq(int(_game_session.get_world_data().get("next_equipment_instance_serial", 0)), 3, "跳过冲突后世界序列应推进到下一号。")

	var invalid_world_data: Dictionary = _game_session.get_world_data().duplicate(true)
	invalid_world_data.erase("next_equipment_instance_serial")
	var invalid_world_error := SaveSerializer.new().get_equipment_instance_serial_validation_error(invalid_world_data)
	_assert_true(
		invalid_world_error.contains("missing required field 'next_equipment_instance_serial'"),
		"缺少装备实例序列的 world_data 应暴露字段级存档损坏诊断。 error=%s" % invalid_world_error
	)
	_assert_eq(int(_game_session.get_world_data().get("next_equipment_instance_serial", 0)), 3, "诊断缺字段 world_data 时不应修改当前世界序列。")

	party = _game_session.get_party_state()
	party.warehouse_state.equipment_instances.clear()
	persist_error = int(_game_session.set_party_state(party))
	if persist_error == OK:
		persist_error = int(_game_session.commit_runtime_state(&"test.clear_party_equipment"))
	_assert_eq(persist_error, OK, "清空预置装备实例后写回 PartyState 应成功。")
	if persist_error != OK:
		return
	party = _game_session.get_party_state()
	_game_session.get_world_data()["next_equipment_instance_serial"] = 1
	var service := PartyWarehouseService.new()
	service.setup(party, _game_session.get_item_defs(), Callable(_game_session, "allocate_equipment_instance_id"))

	var preview_result := service.preview_add_item(&"bronze_sword", 2)
	_assert_eq(int(preview_result.get("added_quantity", 0)), 2, "装备入仓预览应能计算可加入数量。")
	_assert_eq(int(_game_session.get_world_data().get("next_equipment_instance_serial", 0)), 1, "preview_add_item 不应消耗 world-level 装备实例序列。")

	var add_result := service.add_item(&"bronze_sword", 2)
	_assert_eq(int(add_result.get("added_quantity", 0)), 2, "装备正式入仓应写入两个实例。")
	_assert_eq(_instance_id_signature(party), ["eq_000001", "eq_000002"], "装备正式入仓应按 world-level 递增 ID 写入。")
	_assert_eq(int(_game_session.get_world_data().get("next_equipment_instance_serial", 0)), 3, "装备正式入仓后世界序列应推进。")

	var batch_preview := service.preview_batch_swap([], [&"scout_charm"])
	_assert_true(bool(batch_preview.get("allowed", false)), "批量换入预览应允许装备入仓。")
	_assert_eq(int(_game_session.get_world_data().get("next_equipment_instance_serial", 0)), 3, "preview_batch_swap 不应消耗 world-level 装备实例序列。")

	var batch_commit := service.commit_batch_swap([], [&"scout_charm"])
	_assert_true(bool(batch_commit.get("allowed", false)), "批量换入提交应允许装备入仓。")
	_assert_eq(_instance_id_signature(party), ["eq_000001", "eq_000002", "eq_000003"], "批量换入提交应继续使用同一 world-level 序列。")
	_assert_eq(int(_game_session.get_world_data().get("next_equipment_instance_serial", 0)), 4, "批量换入提交后世界序列应推进。")


func _test_party_backpack_view_binding_is_battle_local() -> void:
	var item_defs: Dictionary = _game_session.get_item_defs()
	var party := _build_party_with_members([
		_build_member_state(&"porter", "搬运员", 3),
	])
	var party_service := PartyWarehouseService.new()
	party_service.setup(party, item_defs)
	party_service.add_item(&"healing_herb", 2)

	var battle_backpack_view: WarehouseState = party.warehouse_state.duplicate_state()
	var battle_service := PartyWarehouseService.new()
	battle_service.setup_party_backpack_view(party, battle_backpack_view, item_defs)
	battle_service.add_item(&"healing_herb", 3)
	battle_service.add_item(&"bronze_sword", 1)

	_assert_eq(battle_service.count_item(&"healing_herb"), 5, "battle-local 队伍共享背包 view 应能独立增加堆叠数量。")
	_assert_eq(party_service.count_item(&"healing_herb"), 2, "battle-local 队伍共享背包 view 不应直接修改 PartyState 仓库数量。")
	_assert_eq(battle_service.count_item(&"bronze_sword"), 1, "battle-local 队伍共享背包 view 应能独立保存装备实例。")
	_assert_eq(party_service.count_item(&"bronze_sword"), 0, "battle-local 装备实例不应直接写入 PartyState 仓库。")

	var swap_result := battle_service.commit_batch_swap([&"bronze_sword"], [&"iron_greatsword"])
	_assert_true(bool(swap_result.get("allowed", false)), "battle-local 队伍共享背包 view 应支持原子换入换出。")
	_assert_eq(battle_service.count_item(&"bronze_sword"), 0, "battle-local 原子换出后旧装备实例应离开 view。")
	_assert_eq(battle_service.count_item(&"iron_greatsword"), 1, "battle-local 原子换入后新装备实例应进入 view。")
	_assert_eq(party_service.count_item(&"iron_greatsword"), 0, "battle-local 原子换入不应直接写入 PartyState 仓库。")


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


func _test_warehouse_state_rejects_bad_schema() -> void:
	var valid_state = WarehouseState.from_dict({
		"stacks": [
			_make_stack_payload("healing_herb", 2),
		],
		"equipment_instances": [
			EquipmentInstanceState.create(&"bronze_sword", &"eq_schema_warehouse_bronze").to_dict(),
		],
	})
	_assert_true(valid_state != null, "当前 warehouse_state schema 应可读取。")

	_assert_warehouse_state_rejects(
		{"equipment_instances": []},
		"warehouse_state 缺少 stacks 字段应拒绝。"
	)
	_assert_warehouse_state_rejects(
		{"stacks": []},
		"warehouse_state 缺少 equipment_instances 字段应拒绝。"
	)
	_assert_warehouse_state_rejects(
		{"stacks": {}, "equipment_instances": []},
		"warehouse_state.stacks 错类型应拒绝。"
	)
	_assert_warehouse_state_rejects(
		{"stacks": [], "equipment_instances": {}},
		"warehouse_state.equipment_instances 错类型应拒绝。"
	)
	_assert_warehouse_state_rejects(
		{"stacks": [], "equipment_instances": [], "legacy_capacity": 12},
		"warehouse_state 含额外旧字段应拒绝。"
	)
	_assert_warehouse_state_rejects(
		{"stacks": ["healing_herb"], "equipment_instances": []},
		"warehouse_state.stacks 内非字典条目应拒绝。"
	)
	_assert_warehouse_state_rejects(
		{"stacks": [{"item_id": "healing_herb"}], "equipment_instances": []},
		"warehouse stack 缺少 quantity 应拒绝。"
	)
	_assert_warehouse_state_rejects(
		{"stacks": [_make_stack_payload("healing_herb", "2")], "equipment_instances": []},
		"warehouse stack 的字符串 quantity 不应兼容转换。"
	)
	_assert_warehouse_state_rejects(
		{"stacks": [_make_stack_payload("healing_herb", 0)], "equipment_instances": []},
		"warehouse stack 的空数量不应静默丢弃。"
	)
	_assert_warehouse_state_rejects(
		{"stacks": [_make_stack_payload(17, 2)], "equipment_instances": []},
		"warehouse stack 的非字符串 item_id 不应兼容转换。"
	)
	var extra_stack_field := _make_stack_payload("healing_herb", 2)
	extra_stack_field["legacy_stack_id"] = "stack_1"
	_assert_warehouse_state_rejects(
		{"stacks": [extra_stack_field], "equipment_instances": []},
		"warehouse stack 含额外旧字段应拒绝。"
	)

	var missing_rarity_instance := EquipmentInstanceState.create(&"bronze_sword", &"eq_schema_missing_rarity").to_dict()
	missing_rarity_instance.erase("rarity")
	_assert_warehouse_state_rejects(
		{"stacks": [], "equipment_instances": [missing_rarity_instance]},
		"warehouse_state.equipment_instances 内坏装备实例应拒绝，而不是跳过。"
	)
	var extra_equipment_field := EquipmentInstanceState.create(&"bronze_sword", &"eq_schema_extra_field").to_dict()
	extra_equipment_field["legacy_instance_level"] = 3
	_assert_warehouse_state_rejects(
		{"stacks": [], "equipment_instances": [extra_equipment_field]},
		"equipment instance 含额外旧字段应拒绝。"
	)


func _test_party_warehouse_window_display_field_contract() -> void:
	var warehouse = PartyWarehouseWindowScene.instantiate()
	root.add_child(warehouse)
	await process_frame

	var window_data := {
		"title": "共享仓库",
		"meta": "字段契约测试",
		"summary_text": "测试数据",
		"status_text": "",
		"entries": [
			{
				"item_id": "iron_ore",
				"display_name": "铁矿石",
				"description": "正式展示条目。",
				"quantity": 2,
				"total_quantity": 5,
				"is_stackable": true,
				"stack_limit": 20,
				"storage_mode": "stack",
			},
			{
				"item_id": "legacy_probe_item",
				"description": "缺少 display_name / total_quantity 的坏展示条目。",
				"quantity": 4,
				"is_stackable": true,
				"stack_limit": 20,
				"storage_mode": "stack",
			},
			{
				"item_id": "legacy_skill_book_item",
				"display_name": "旧字段技能书",
				"description": "缺少 granted_skill_name 的技能书展示条目。",
				"quantity": 1,
				"total_quantity": 1,
				"is_stackable": true,
				"stack_limit": 20,
				"storage_mode": "stack",
				"is_skill_book": true,
				"granted_skill_id": "legacy_skill_id_should_not_render",
			},
		],
		"target_members": [
			{"member_id": "reader", "display_name": "读者"},
		],
		"default_target_member_id": "reader",
	}
	warehouse.show_warehouse(window_data)
	await process_frame

	var stack_list := warehouse.get_node("CenterContainer/Panel/MarginContainer/Content/Body/ListColumn/StackList") as ItemList
	var details_label := warehouse.get_node("CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/ItemRow/DetailsLabel") as RichTextLabel
	var target_member_selector := warehouse.get_node(
		"CenterContainer/Panel/MarginContainer/Content/Body/Controls/TargetMemberSelector"
	) as OptionButton
	_assert_true(stack_list != null, "仓库窗口字段契约测试应能读取 StackList。")
	_assert_true(details_label != null, "仓库窗口字段契约测试应能读取 DetailsLabel。")
	_assert_true(target_member_selector != null, "仓库窗口字段契约测试应能读取 TargetMemberSelector。")
	if stack_list == null or details_label == null or target_member_selector == null:
		warehouse.queue_free()
		await process_frame
		return

	_assert_eq(stack_list.get_item_text(0), "铁矿石  x2  |  堆栈 2/20", "正式展示条目应使用显式 display_name 与 quantity。")
	_assert_true(details_label.text.contains("物品：铁矿石"), "正式展示条目详情应显示显式 display_name。")
	_assert_true(details_label.text.contains("同类总数：5"), "正式展示条目详情应显示显式 total_quantity。")

	stack_list.select(1)
	stack_list.emit_signal("item_selected", 1)
	await process_frame
	var missing_display_details := String(details_label.text)
	_assert_true(not stack_list.get_item_text(1).contains("legacy_probe_item"), "缺少 display_name 时列表不应回退显示 item_id。")
	_assert_true(not missing_display_details.contains("物品：legacy_probe_item"), "缺少 display_name 时详情名称不应回退显示 item_id。")
	_assert_true(not missing_display_details.contains("同类总数：4"), "缺少 total_quantity 时详情不应回退显示 quantity。")
	_assert_true(missing_display_details.contains("同类总数：0"), "缺少 total_quantity 时详情应按缺字段显示 0。")

	stack_list.select(2)
	stack_list.emit_signal("item_selected", 2)
	await process_frame
	var missing_skill_name_details := String(details_label.text)
	_assert_true(
		not missing_skill_name_details.contains("legacy_skill_id_should_not_render"),
		"缺少 granted_skill_name 时技能书效果不应回退显示 granted_skill_id。"
	)
	_assert_true(
		missing_skill_name_details.contains("技能书效果：使目标角色学会 。"),
		"缺少 granted_skill_name 时技能书效果应保留为空展示，不用旧字段恢复。"
	)
	_assert_eq(target_member_selector.get_item_text(0), "读者", "正式目标成员应显示显式 display_name。")
	_assert_true(missing_skill_name_details.contains("当前目标：读者"), "正式目标成员详情应显示显式 display_name。")

	var bad_target_member_window_data := {
		"title": "共享仓库",
		"meta": "目标成员字段契约测试",
		"summary_text": "测试数据",
		"status_text": "",
		"entries": [
			{
				"item_id": "skill_book_archer_aimed_shot",
				"display_name": "精准射击 技能书",
				"description": "使角色学会精准射击。",
				"quantity": 1,
				"total_quantity": 1,
				"is_stackable": true,
				"stack_limit": 20,
				"storage_mode": "stack",
				"is_skill_book": true,
				"granted_skill_id": "archer_aimed_shot",
				"granted_skill_name": "精准射击",
			},
		],
		"target_members": [
			{"member_id": "missing_target"},
			{"member_id": "empty_target", "display_name": ""},
			{"member_id": "wrong_type_target", "display_name": 17},
		],
		"default_target_member_id": "missing_target",
	}
	warehouse.show_warehouse(bad_target_member_window_data)
	await process_frame
	_assert_eq(target_member_selector.item_count, 3, "坏目标成员夹具应保留三个可观察选项。")
	if target_member_selector.item_count >= 3:
		_assert_eq(target_member_selector.get_item_text(0), "", "缺少目标成员 display_name 时 selector 不应回退显示 member_id。")
		_assert_eq(target_member_selector.get_item_text(1), "", "目标成员 display_name 为空时 selector 不应回退显示 member_id。")
		_assert_eq(target_member_selector.get_item_text(2), "", "目标成员 display_name 错类型时 selector 不应把错类型值或 member_id 当显示名。")

		var bad_member_ids := ["missing_target", "empty_target", "wrong_type_target"]
		for index in range(bad_member_ids.size()):
			target_member_selector.select(index)
			target_member_selector.emit_signal("item_selected", index)
			await process_frame
			var bad_target_details := String(details_label.text)
			_assert_true(
				not bad_target_details.contains(String(bad_member_ids[index])),
				"坏目标成员 display_name 不应在详情中回退显示 member_id。"
			)

	var formal_skill_window_data := {
		"title": "共享仓库",
		"meta": "正式字段测试",
		"summary_text": "测试数据",
		"status_text": "",
		"entries": [
			{
				"item_id": "skill_book_archer_aimed_shot",
				"display_name": "精准射击 技能书",
				"description": "使角色学会精准射击。",
				"quantity": 1,
				"total_quantity": 1,
				"is_stackable": true,
				"stack_limit": 20,
				"storage_mode": "stack",
				"is_skill_book": true,
				"granted_skill_id": "archer_aimed_shot",
				"granted_skill_name": "精准射击",
			},
		],
		"target_members": [
			{"member_id": "reader", "display_name": "读者"},
		],
		"default_target_member_id": "reader",
	}
	warehouse.show_warehouse(formal_skill_window_data)
	await process_frame
	_assert_eq(stack_list.get_item_text(0), "精准射击 技能书  x1  |  堆栈 1/20", "正式技能书条目应正常显示显式字段。")
	_assert_true(details_label.text.contains("物品：精准射击 技能书"), "正式技能书详情应显示显式 display_name。")
	_assert_true(details_label.text.contains("同类总数：1"), "正式技能书详情应显示显式 total_quantity。")
	_assert_true(
		details_label.text.contains("技能书效果：使目标角色学会 精准射击。"),
		"正式技能书详情应显示显式 granted_skill_name。"
	)
	_assert_eq(target_member_selector.get_item_text(0), "读者", "正式技能书目标成员 selector 应正常显示显式 display_name。")
	_assert_true(details_label.text.contains("当前目标：读者"), "正式技能书目标成员详情应正常显示显式 display_name。")

	warehouse.queue_free()
	await process_frame


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
	var sword_entries := _find_inventory_entries(entries, "bronze_sword")
	var sword_entry := sword_entries[0] if not sword_entries.is_empty() else {}
	var herb_entry := _find_inventory_entry(entries, "healing_herb")

	_assert_eq(sword_entries.size(), 2, "展示条目中应为每个装备实例提供独立条目。")
	_assert_true(sword_entry.has("display_name") and not String(sword_entry.get("display_name", "")).is_empty(), "正式展示条目应显式提供 display_name。")
	_assert_true(sword_entry.has("quantity"), "正式展示条目应显式提供 quantity。")
	_assert_true(sword_entry.has("total_quantity"), "正式展示条目应显式提供 total_quantity。")
	_assert_true(sword_entry.has("instance_id") and not String(sword_entry.get("instance_id", "")).is_empty(), "装备展示条目应显式提供 instance_id。")
	_assert_eq(int(sword_entry.get("quantity", 0)), 1, "装备实例条目的当前数量应为 1。")
	_assert_eq(int(sword_entry.get("total_quantity", 0)), 2, "装备实例条目应保留同类总数。")
	_assert_eq(String(sword_entry.get("storage_mode", "")), "instance", "装备条目应标记为 instance 存储模式。")
	_assert_true(not bool(sword_entry.get("is_stackable", true)), "装备条目不应被误标为可堆叠。")

	_assert_true(not herb_entry.is_empty(), "展示条目中应保留普通堆叠物品。")
	_assert_true(herb_entry.has("display_name") and not String(herb_entry.get("display_name", "")).is_empty(), "普通堆叠展示条目应显式提供 display_name。")
	_assert_true(herb_entry.has("quantity"), "普通堆叠展示条目应显式提供 quantity。")
	_assert_true(herb_entry.has("total_quantity"), "普通堆叠展示条目应显式提供 total_quantity。")
	_assert_eq(int(herb_entry.get("quantity", 0)), 7, "普通堆叠物品应保留堆叠数量。")
	_assert_eq(String(herb_entry.get("storage_mode", "")), "stack", "普通物品条目应标记为 stack 存储模式。")


func _test_equipment_instance_remove_requires_instance_id() -> void:
	var item_defs: Dictionary = _game_session.get_item_defs()
	var party := _build_party_with_members([
		_build_member_state(&"porter", "搬运员", 4),
	])
	var common_instance := EquipmentInstanceState.create(&"bronze_sword", &"eq_remove_common_sword")
	common_instance.rarity = EquipmentInstanceState.RarityTier.COMMON
	common_instance.current_durability = 9
	var rare_instance := EquipmentInstanceState.create(&"bronze_sword", &"eq_remove_rare_sword")
	rare_instance.rarity = EquipmentInstanceState.RarityTier.RARE
	rare_instance.current_durability = 31
	var mismatch_instance := EquipmentInstanceState.create(&"scout_charm", &"eq_remove_wrong_item")
	party.warehouse_state.equipment_instances = [common_instance, rare_instance, mismatch_instance]
	var service := PartyWarehouseService.new()
	service.setup(party, item_defs)

	var item_only_remove := service.remove_item(&"bronze_sword", 1)
	_assert_eq(int(item_only_remove.get("removed_quantity", -1)), 0, "重复装备实例的 item_id-only remove 不应删除任意一件。")
	_assert_eq(String(item_only_remove.get("error_code", "")), "equipment_instance_id_required", "重复装备实例 remove 应要求 instance_id。")
	_assert_true(service.has_equipment_instance(&"eq_remove_common_sword", &"bronze_sword"), "item_id-only remove 失败后 common 实例应保留。")
	_assert_true(service.has_equipment_instance(&"eq_remove_rare_sword", &"bronze_sword"), "item_id-only remove 失败后 rare 实例应保留。")

	var mismatch_remove := service.remove_equipment_instance(&"bronze_sword", &"eq_remove_wrong_item")
	_assert_eq(int(mismatch_remove.get("removed_quantity", -1)), 0, "错 item 的 instance_id 不应删除装备。")
	_assert_eq(String(mismatch_remove.get("error_code", "")), "equipment_instance_item_mismatch", "错 item 的 instance_id 应返回 mismatch。")

	var missing_remove := service.remove_equipment_instance(&"bronze_sword", &"eq_remove_missing")
	_assert_eq(int(missing_remove.get("removed_quantity", -1)), 0, "不存在的 instance_id 不应删除装备。")
	_assert_eq(String(missing_remove.get("error_code", "")), "warehouse_missing_instance", "不存在的 instance_id 应返回 missing_instance。")

	var rare_remove := service.remove_equipment_instance(&"bronze_sword", &"eq_remove_rare_sword")
	_assert_eq(int(rare_remove.get("removed_quantity", 0)), 1, "指定 rare instance_id 应删除对应装备。")
	_assert_true(service.has_equipment_instance(&"eq_remove_common_sword", &"bronze_sword"), "删除 rare 后 common 实例应保留。")
	_assert_true(not service.has_equipment_instance(&"eq_remove_rare_sword", &"bronze_sword"), "删除 rare 后该实例应离开仓库。")

	var sell_party := _build_party_with_members([
		_build_member_state(&"seller", "出售测试员", 4),
	])
	var sell_common := EquipmentInstanceState.create(&"bronze_sword", &"eq_sell_common_sword")
	sell_common.rarity = EquipmentInstanceState.RarityTier.COMMON
	sell_common.current_durability = 8
	var sell_rare := EquipmentInstanceState.create(&"bronze_sword", &"eq_sell_rare_sword")
	sell_rare.rarity = EquipmentInstanceState.RarityTier.RARE
	sell_rare.current_durability = 27
	sell_party.warehouse_state.equipment_instances = [sell_common, sell_rare]
	var sell_service := PartyWarehouseService.new()
	sell_service.setup(sell_party, item_defs)
	var shop_service := SettlementShopService.new()
	var settlement_state := {}
	var ambiguous_sell := shop_service.sell("service_local_trade", {}, settlement_state, item_defs, sell_service, sell_party, &"bronze_sword", 1)
	_assert_true(not bool(ambiguous_sell.get("success", true)), "重复装备实例出售缺少 instance_id 时应失败。")
	_assert_true(String(ambiguous_sell.get("message", "")).contains("请选择要出售"), "重复装备实例出售失败文案应要求选择实例。")
	_assert_true(sell_service.has_equipment_instance(&"eq_sell_common_sword", &"bronze_sword"), "出售失败后 common 实例应保留。")
	_assert_true(sell_service.has_equipment_instance(&"eq_sell_rare_sword", &"bronze_sword"), "出售失败后 rare 实例应保留。")
	var explicit_sell := shop_service.sell("service_local_trade", {}, settlement_state, item_defs, sell_service, sell_party, &"bronze_sword", 1, &"eq_sell_rare_sword")
	_assert_true(bool(explicit_sell.get("success", false)), "指定 rare instance_id 出售应成功。")
	_assert_true(sell_service.has_equipment_instance(&"eq_sell_common_sword", &"bronze_sword"), "出售 rare 后 common 实例应保留。")
	_assert_true(not sell_service.has_equipment_instance(&"eq_sell_rare_sword", &"bronze_sword"), "出售 rare 后该实例应离开仓库。")


func _test_weapon_profile_equipment_instances_stack_round_trip() -> void:
	var item_defs: Dictionary = _game_session.get_item_defs()
	var bronze_sword := item_defs.get(&"bronze_sword") as ItemDef
	_assert_true(bronze_sword != null, "weapon profile 仓库回归前置：应能加载 bronze_sword。")
	if bronze_sword == null:
		return
	var profile := bronze_sword.get("weapon_profile") as WeaponProfileDef
	_assert_true(profile != null, "bronze_sword 应通过 weapon_profile 提供武器运行时字段。")
	_assert_eq(int(bronze_sword.get_weapon_attack_range()), 1, "warehouse 回归应能从 weapon_profile 读取武器攻击距离。")
	_assert_eq(String(bronze_sword.get_weapon_physical_damage_tag()), "physical_pierce", "warehouse 回归应能从 weapon_profile 读取武器伤害类型。")

	var party := _build_party_with_members([
		_build_member_state(&"porter", "搬运员", 6),
	])
	party.main_character_member_id = &"porter"
	var service := PartyWarehouseService.new()
	service.setup(party, item_defs)

	var sword_result := service.add_item(&"bronze_sword", 2)
	var herb_result := service.add_item(&"healing_herb", 7)
	_assert_eq(int(sword_result.get("added_quantity", 0)), 2, "带 weapon_profile 的武器应能按装备实例加入仓库。")
	_assert_eq(int(herb_result.get("added_quantity", 0)), 7, "加入 weapon_profile 武器后普通堆叠物仍应能入仓。")
	_assert_eq(service.count_item(&"bronze_sword"), 2, "带 weapon_profile 的武器库存计数应来自装备实例数。")
	_assert_eq(service.count_item(&"healing_herb"), 7, "普通堆叠物数量不应受 weapon_profile 装备影响。")
	_assert_eq(service.get_used_slots(), 3, "2 件装备实例 + 1 个普通堆叠应占 3 格。")
	_assert_eq(_stack_signature(party), ["healing_herb:7"], "带 weapon_profile 武器不应写入普通 stacks。")
	_assert_eq(_instance_signature(party), ["bronze_sword", "bronze_sword"], "带 weapon_profile 武器应写入 equipment_instances。")

	var entries := service.get_inventory_entries()
	var sword_entries := _find_inventory_entries(entries, "bronze_sword")
	var sword_entry := sword_entries[0] if not sword_entries.is_empty() else {}
	var herb_entry := _find_inventory_entry(entries, "healing_herb")
	_assert_eq(String(sword_entry.get("storage_mode", "")), "instance", "带 weapon_profile 武器展示条目应保持 instance 模式。")
	_assert_eq(sword_entries.size(), 2, "带 weapon_profile 武器应按实例展示为两个条目。")
	_assert_eq(int(sword_entry.get("quantity", 0)), 1, "带 weapon_profile 武器单条展示数量应为 1。")
	_assert_eq(int(sword_entry.get("total_quantity", 0)), 2, "带 weapon_profile 武器展示条目应保留同类总数。")
	_assert_true(not String(sword_entry.get("instance_id", "")).is_empty(), "带 weapon_profile 武器展示条目应提供 instance_id。")
	_assert_eq(String(herb_entry.get("storage_mode", "")), "stack", "普通物品展示条目应保持 stack 模式。")
	_assert_eq(int(herb_entry.get("quantity", 0)), 7, "普通物品展示数量应保持堆叠数量。")

	var restored_party = PartyState.from_dict(party.to_dict())
	_assert_true(restored_party != null, "带 weapon_profile 武器实例和普通堆叠的 PartyState round-trip 应成功。")
	if restored_party == null:
		return
	_assert_eq(_stack_signature(restored_party), ["healing_herb:7"], "round-trip 后普通堆叠应保持。")
	_assert_eq(_instance_signature(restored_party), ["bronze_sword", "bronze_sword"], "round-trip 后 weapon_profile 武器实例应保持。")
	for instance in restored_party.warehouse_state.get_non_empty_instances():
		var payload: Dictionary = instance.to_dict()
		_assert_true(not payload.has("weapon_profile"), "仓库装备实例 payload 不应序列化 weapon_profile 静态资源。")
		_assert_true(not payload.has("weapon_attack_range"), "仓库装备实例 payload 不应写入旧 weapon_attack_range 字段。")
		_assert_true(not payload.has("weapon_physical_damage_tag"), "仓库装备实例 payload 不应写入旧 weapon_physical_damage_tag 字段。")


func _test_skill_book_generation_and_use_rules() -> void:
	var item_defs: Dictionary = _game_session.get_item_defs()
	var skill_book_item_id := SkillBookItemFactory.build_item_id_for_skill(&"archer_aimed_shot")
	var item_def = item_defs.get(skill_book_item_id)
	_assert_true(item_def != null, "book 来源技能应自动生成对应技能书物品。")
	if item_def == null:
		return
	_assert_true(item_def.is_skill_book(), "自动生成的技能书物品应带有技能书分类。")
	_assert_eq(item_def.granted_skill_id, &"archer_aimed_shot", "技能书物品应指向正确的技能 ID。")
	_assert_eq(item_def.display_name, "精准射击 技能书", "技能书文案应使用 SkillDef.display_name。")
	_assert_true(item_def.description.contains("精准射击"), "技能书说明应使用 SkillDef.display_name。")
	_assert_true(not item_def.display_name.contains("archer_aimed_shot"), "技能书显示名不应回退显示 skill_id。")
	_assert_true(not item_def.description.contains("archer_aimed_shot"), "技能书说明不应回退显示 skill_id。")

	var missing_name_skill := SkillDef.new()
	missing_name_skill.skill_id = &"factory_missing_display_name_skill"
	missing_name_skill.display_name = ""
	missing_name_skill.learn_source = &"book"
	missing_name_skill.skill_type = &"passive"
	missing_name_skill.max_level = 1
	missing_name_skill.mastery_curve = PackedInt32Array([1])
	var missing_name_skill_defs := {}
	missing_name_skill_defs[missing_name_skill.skill_id] = missing_name_skill
	var generated_without_name := SkillBookItemFactory.new().build_generated_item_defs(missing_name_skill_defs)
	var missing_name_item_id := SkillBookItemFactory.build_item_id_for_skill(missing_name_skill.skill_id)
	_assert_true(generated_without_name.is_empty(), "缺少 display_name 的 book 技能不应生成技能书物品。")
	_assert_true(
		not generated_without_name.has(missing_name_item_id),
		"缺少 display_name 时不应生成带 skill_id 文案的技能书。"
	)

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


func _test_new_consumable_seed_warehouse_schema() -> void:
	var item_defs: Dictionary = _game_session.get_item_defs()
	var party := _build_party_with_members([
		_build_member_state(&"porter", "搬运员", 8),
	])
	var service := PartyWarehouseService.new()
	service.setup(party, item_defs)

	for item_id in NEW_CONSUMABLE_ITEM_QUANTITIES.keys():
		var item_def: ItemDef = item_defs.get(item_id) as ItemDef
		_assert_true(item_def != null, "共享仓库回归应能加载新消耗品 %s。" % String(item_id))
		if item_def == null:
			continue
		_assert_true(item_def.is_stackable, "新消耗品 %s 应走堆叠仓库流。" % String(item_id))
		_assert_true(not item_def.is_equipment(), "新消耗品 %s 不应进入装备实例流。" % String(item_id))
		_assert_eq(String(item_def.get_item_category_normalized()), "misc", "新消耗品 %s 应保持 misc 分类。" % String(item_id))

		var quantity := int(NEW_CONSUMABLE_ITEM_QUANTITIES.get(item_id, 0))
		var add_result := service.add_item(item_id, quantity)
		_assert_eq(int(add_result.get("added_quantity", 0)), quantity, "新消耗品 %s 应能完整写入共享仓库。" % String(item_id))
		_assert_eq(service.count_item(item_id), quantity, "新消耗品 %s 入仓后数量应正确累计。" % String(item_id))

	var inventory_entries := service.get_inventory_entries()
	for item_id in NEW_CONSUMABLE_ITEM_QUANTITIES.keys():
		var entry := _find_inventory_entry(inventory_entries, String(item_id))
		_assert_true(not entry.is_empty(), "共享仓库展示条目应包含新消耗品 %s。" % String(item_id))
		if entry.is_empty():
			continue
		_assert_eq(String(entry.get("storage_mode", "")), "stack", "新消耗品 %s 应标记为 stack 存储模式。" % String(item_id))
		_assert_eq(String(entry.get("item_category", "")), "misc", "新消耗品 %s 的展示分类应保持 misc。" % String(item_id))
		_assert_true(bool(entry.get("is_stackable", false)), "新消耗品 %s 的展示条目应保持可堆叠。" % String(item_id))


func _test_material_seed_warehouse_schema() -> void:
	var item_defs: Dictionary = _game_session.get_item_defs()
	var party := _build_party_with_members([
		_build_member_state(&"porter", "搬运员", 10),
	])
	var service := PartyWarehouseService.new()
	service.setup(party, item_defs)

	var material_categories: Dictionary = {}
	for item_id in MATERIAL_ITEM_QUANTITIES.keys():
		var item_def: ItemDef = item_defs.get(item_id) as ItemDef
		_assert_true(item_def != null, "共享仓库回归应能加载材料 %s。" % String(item_id))
		if item_def == null:
			continue
		_assert_true(item_def.is_stackable, "材料 %s 应走堆叠仓库流。" % String(item_id))
		_assert_true(not item_def.is_equipment(), "材料 %s 不应进入装备实例流。" % String(item_id))
		_assert_eq(String(item_def.get_item_category_normalized()), "misc", "材料 %s 应保持 misc 分类。" % String(item_id))
		_assert_true(item_def.get_tags().has(&"material"), "材料 %s 应带有 material 标签。" % String(item_id))
		_assert_true(not item_def.get_crafting_groups().is_empty(), "材料 %s 应声明 crafting_groups。" % String(item_id))
		for tag in item_def.get_tags():
			if tag == &"material":
				continue
			material_categories[tag] = true
			break

		var quantity := int(MATERIAL_ITEM_QUANTITIES.get(item_id, 0))
		var add_result := service.add_item(item_id, quantity)
		_assert_eq(int(add_result.get("added_quantity", 0)), quantity, "材料 %s 应能完整写入共享仓库。" % String(item_id))
		_assert_eq(service.count_item(item_id), quantity, "材料 %s 入仓后数量应正确累计。" % String(item_id))

	var inventory_entries := service.get_inventory_entries()
	for item_id in MATERIAL_ITEM_QUANTITIES.keys():
		var entry := _find_inventory_entry(inventory_entries, String(item_id))
		_assert_true(not entry.is_empty(), "共享仓库展示条目应包含材料 %s。" % String(item_id))
		if entry.is_empty():
			continue
		_assert_eq(String(entry.get("storage_mode", "")), "stack", "材料 %s 应标记为 stack 存储模式。" % String(item_id))
		_assert_eq(String(entry.get("item_category", "")), "misc", "材料 %s 的展示分类应保持 misc。" % String(item_id))
		_assert_true(bool(entry.get("is_stackable", false)), "材料 %s 的展示条目应保持可堆叠。" % String(item_id))

	_assert_true(material_categories.size() >= 6, "共享仓库链路中正式材料种类应至少覆盖 6 类。")


func _test_quest_item_seed_warehouse_schema() -> void:
	var item_defs: Dictionary = _game_session.get_item_defs()
	var party := _build_party_with_members([
		_build_member_state(&"porter", "搬运员", 8),
	])
	var service := PartyWarehouseService.new()
	service.setup(party, item_defs)

	var quest_item_categories: Dictionary = {}
	for item_id in QUEST_ITEM_QUANTITIES.keys():
		var item_def: ItemDef = item_defs.get(item_id) as ItemDef
		_assert_true(item_def != null, "共享仓库回归应能加载任务物品 %s。" % String(item_id))
		if item_def == null:
			continue
		_assert_true(item_def.is_stackable, "任务物品 %s 应走堆叠仓库流。" % String(item_id))
		_assert_true(not item_def.is_equipment(), "任务物品 %s 不应进入装备实例流。" % String(item_id))
		_assert_eq(String(item_def.get_item_category_normalized()), "misc", "任务物品 %s 应保持 misc 分类。" % String(item_id))
		_assert_true(item_def.get_tags().has(&"quest_item"), "任务物品 %s 应带有 quest_item 标签。" % String(item_id))
		_assert_true(not item_def.get_quest_groups().is_empty(), "任务物品 %s 应声明 quest_groups。" % String(item_id))
		quest_item_categories[item_def.get_quest_groups()[0]] = true

		var quantity := int(QUEST_ITEM_QUANTITIES.get(item_id, 0))
		var add_result := service.add_item(item_id, quantity)
		_assert_eq(int(add_result.get("added_quantity", 0)), quantity, "任务物品 %s 应能完整写入共享仓库。" % String(item_id))
		_assert_eq(service.count_item(item_id), quantity, "任务物品 %s 入仓后数量应正确累计。" % String(item_id))

	var inventory_entries := service.get_inventory_entries()
	for item_id in QUEST_ITEM_QUANTITIES.keys():
		var entry := _find_inventory_entry(inventory_entries, String(item_id))
		_assert_true(not entry.is_empty(), "共享仓库展示条目应包含任务物品 %s。" % String(item_id))
		if entry.is_empty():
			continue
		_assert_eq(String(entry.get("storage_mode", "")), "stack", "任务物品 %s 应标记为 stack 存储模式。" % String(item_id))
		_assert_eq(String(entry.get("item_category", "")), "misc", "任务物品 %s 的展示分类应保持 misc。" % String(item_id))
		_assert_true(bool(entry.get("is_stackable", false)), "任务物品 %s 的展示条目应保持可堆叠。" % String(item_id))

	_assert_true(QUEST_ITEM_QUANTITIES.size() >= 3, "共享仓库链路中的正式任务物品应至少覆盖 3 种。")
	_assert_true(quest_item_categories.size() >= 3, "共享仓库链路中的正式任务物品种类应至少覆盖 3 类。")


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
		EquipmentInstanceState.create(&"bronze_sword", &"eq_roundtrip_bronze_sword"),
	]

	var persist_error := int(_game_session.set_party_state(party_state))
	if persist_error == OK:
		persist_error = int(_game_session.commit_runtime_state(&"test.warehouse_roundtrip"))
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
	if persist_error == OK:
		persist_error = int(_game_session.commit_runtime_state(&"test.prefill_warehouse"))
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
		not String(management.overview_label.text).contains("storage_space"),
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
	var warehouse_stack_list := warehouse.get_node(
		"CenterContainer/Panel/MarginContainer/Content/Body/ListColumn/StackList"
	) as ItemList
	var warehouse_details_label := warehouse.get_node(
		"CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/ItemRow/DetailsLabel"
	) as RichTextLabel
	var skill_book_list_index := _find_item_list_entry_index_by_item_id(warehouse_stack_list, String(skill_book_item_id))
	_assert_true(skill_book_list_index >= 0, "正式仓库窗口列表应包含技能书展示条目。")
	if skill_book_list_index >= 0:
		warehouse_stack_list.select(skill_book_list_index)
		warehouse_stack_list.emit_signal("item_selected", skill_book_list_index)
		await process_frame
		var skill_book_item_def := _game_session.get_item_defs().get(skill_book_item_id) as ItemDef
		var skill_def := _game_session.get_skill_defs().get(target_skill_id) as SkillDef
		_assert_true(
			skill_book_item_def != null and warehouse_stack_list.get_item_text(skill_book_list_index).contains(skill_book_item_def.display_name),
			"正式仓库窗口列表应显示 entry.display_name。"
		)
		_assert_true(warehouse_details_label.text.contains("同类总数：1"), "正式仓库窗口详情应显示 entry.total_quantity。")
		_assert_true(
			skill_def != null and warehouse_details_label.text.contains("技能书效果：使目标角色学会 %s。" % skill_def.display_name),
			"正式仓库窗口详情应显示 entry.granted_skill_name。"
		)
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


func _make_stack_payload(item_id: Variant, quantity: Variant) -> Dictionary:
	return {
		"item_id": item_id,
		"quantity": quantity,
	}


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


func _instance_id_signature(party_state: PartyState) -> Array[String]:
	var result: Array[String] = []
	if party_state == null or party_state.warehouse_state == null:
		return result
	for instance in party_state.warehouse_state.equipment_instances:
		if instance == null:
			continue
		result.append(String(instance.instance_id))
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


func _find_inventory_entries(entries: Array, item_id: String) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for entry_variant in entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("item_id", "")) == item_id:
			result.append(entry)
	return result


func _find_item_list_entry_index_by_item_id(stack_list: ItemList, item_id: String) -> int:
	if stack_list == null:
		return -1
	for index in range(stack_list.item_count):
		var metadata = stack_list.get_item_metadata(index)
		if metadata is not Dictionary:
			continue
		var entry: Dictionary = metadata
		if String(entry.get("item_id", "")) == item_id:
			return index
	return -1


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
		_test.fail(message)


func _assert_warehouse_state_rejects(payload: Variant, message: String) -> void:
	_assert_true(WarehouseState.from_dict(payload) == null, message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
