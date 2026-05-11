extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_snapshot_builder.gd")
const GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT = preload("res://scripts/utils/game_text_snapshot_renderer.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_snapshot_builder_matches_facade_outputs()
	_test_text_snapshot_redacts_host_log_paths()
	_test_snapshot_builder_exposes_party_quest_snapshot()
	_test_snapshot_builder_rejects_legacy_quest_container_shapes()
	_test_text_snapshot_requires_explicit_quest_stage_id()
	_test_text_snapshot_rejects_legacy_window_and_report_fields()
	_test_snapshot_builder_cross_references_quest_items_in_text_snapshot()
	_test_snapshot_builder_exposes_contract_board_modal_snapshot()
	_test_snapshot_builder_exposes_forge_modal_snapshot()
	_test_snapshot_builder_exposes_generic_forge_modal_snapshot()
	_test_snapshot_builder_requires_panel_kind_for_forge_modal()
	_test_snapshot_builder_exposes_game_over_snapshot()
	_test_snapshot_builder_exposes_battle_loot_snapshot()
	_test_snapshot_builder_omits_loot_section_when_empty()

	if _failures.is_empty():
		print("Game runtime snapshot builder regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Game runtime snapshot builder regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_snapshot_builder_matches_facade_outputs() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(facade)

	var facade_snapshot: Dictionary = facade.build_headless_snapshot()
	var builder_snapshot: Dictionary = builder.build_headless_snapshot()
	var facade_text := facade.build_text_snapshot()
	var builder_text := builder.build_text_snapshot()

	_assert_eq(builder_snapshot, facade_snapshot, "Snapshot builder 输出应与 facade.build_headless_snapshot() 保持一致。")
	_assert_eq(builder_text, facade_text, "Snapshot builder 文本快照应与 facade.build_text_snapshot() 保持一致。")
	_assert_true(not builder_text.is_empty(), "Snapshot builder 文本快照不应为空。")
	_assert_true(builder_snapshot.has("logs"), "运行时快照应包含日志段。")
	_assert_eq(String(builder_snapshot.get("logs", {}).get("file_path", "")), "", "运行时快照默认不应暴露日志文件路径。")
	_assert_eq(bool(builder_snapshot.get("logs", {}).get("file_output_enabled", true)), false, "运行时日志文件输出默认应关闭。")
	_assert_true(not (builder_snapshot.get("logs", {}).get("entries", []) as Array).is_empty(), "运行时快照应包含最近日志条目。")
	_assert_true(builder_text.contains("[LOG]"), "运行时文本快照应包含日志分段。")

	builder.dispose()
	facade.dispose()
	_cleanup_test_session(game_session)


func _test_text_snapshot_redacts_host_log_paths() -> void:
	var memory_text_snapshot := GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT.render_full_snapshot({
		"logs": {
			"file_path": "",
			"virtual_path": "",
			"entry_count": 1,
			"buffer_limit": 3,
			"entries": [
				{
					"seq": 1,
					"level": "info",
					"domain": "session",
					"event_id": "session.memory_only",
					"message": "memory only",
				},
			],
		},
	})
	_assert_true(memory_text_snapshot.contains("[LOG]"), "内存日志仍应渲染 LOG 分段。")
	_assert_true(not memory_text_snapshot.contains("file_name="), "内存日志文本快照不应渲染文件名。")
	_assert_true(not memory_text_snapshot.contains("file_path="), "文本快照不应继续渲染绝对 file_path 标签。")
	_assert_true(not memory_text_snapshot.contains("virtual_path="), "文本快照不应继续渲染 virtual_path 标签。")

	var file_path := "C:/tmp/magic/session_redaction.jsonl"
	var virtual_path := "user://logs/session_redaction.jsonl"
	var file_text_snapshot := GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT.render_full_snapshot({
		"logs": {
			"file_path": file_path,
			"virtual_path": virtual_path,
			"entry_count": 0,
			"buffer_limit": 3,
			"entries": [],
		},
	})
	_assert_true(file_text_snapshot.contains("file_name=session_redaction.jsonl"), "文本快照应只渲染稳定日志文件名。")
	_assert_true(not file_text_snapshot.contains(file_path), "文本快照不应泄漏宿主绝对日志路径。")
	_assert_true(not file_text_snapshot.contains(virtual_path), "文本快照不应泄漏 session 级虚拟日志路径。")


func _test_snapshot_builder_exposes_party_quest_snapshot() -> void:
	var runtime := FakeQuestRuntime.new()
	var party_state := FakeQuestPartyState.new()
	var quest_state := QuestState.new()
	quest_state.quest_id = &"contract_wolf_pack"
	quest_state.mark_accepted(12)
	quest_state.record_objective_progress(&"defeat_wolves", 2, 3, {"enemy_template_id": "wolf_raider"})
	quest_state.record_objective_progress(&"defeat_wolves", 2, 3, {"enemy_template_id": "wolf_raider"})
	quest_state.record_objective_progress(&"report_back", 1, 1, {"settlement_id": "spring_village_01"})
	var claimable_quest := QuestState.new()
	claimable_quest.quest_id = &"contract_settlement_warehouse"
	claimable_quest.mark_accepted(9)
	claimable_quest.mark_completed(15)
	party_state.active_quests = [quest_state]
	party_state.claimable_quests = [claimable_quest]
	party_state.completed_quest_ids = [&"contract_intro"]
	runtime.party_state = party_state

	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()
	var text_snapshot := builder.build_text_snapshot()

	var quests_snapshot: Dictionary = snapshot.get("party", {}).get("quests", {})
	_assert_true(not bool(snapshot.get("world", {}).get("player_visible_on_map", true)), "快照应暴露世界地图人物显隐状态。")
	_assert_true(text_snapshot.contains("player_visible_on_map=false"), "文本快照应渲染世界地图人物显隐状态。")
	_assert_true(not quests_snapshot.is_empty(), "当 PartyState 暴露 quest schema 时，headless snapshot 应在 party 段包含 quests。")
	_assert_eq(
		quests_snapshot.get("active_quest_ids", []),
		["contract_wolf_pack"],
		"active_quest_ids 应稳定暴露当前激活任务 ID。"
	)
	_assert_eq(
		quests_snapshot.get("claimable_quest_ids", []),
		["contract_settlement_warehouse"],
		"claimable_quest_ids 应稳定暴露待领奖励任务 ID。"
	)
	_assert_eq(
		quests_snapshot.get("completed_quest_ids", []),
		["contract_intro"],
		"completed_quest_ids 应稳定暴露已完成任务 ID。"
	)
	var active_quests: Array = quests_snapshot.get("active_quests", [])
	var claimable_quests: Array = quests_snapshot.get("claimable_quests", [])
	_assert_eq(active_quests.size(), 1, "active_quests 应保留当前任务详情。")
	_assert_eq(claimable_quests.size(), 1, "claimable_quests 应保留待领奖励任务详情。")
	if not active_quests.is_empty():
		var quest_entry: Dictionary = active_quests[0]
		_assert_eq(String(quest_entry.get("quest_id", "")), "contract_wolf_pack", "任务快照应保留 quest_id。")
		_assert_eq(String(quest_entry.get("stage_id", "")), "active", "激活任务快照应标记 active stage。")
		_assert_eq(int(quest_entry.get("accepted_at_world_step", -1)), 12, "任务快照应保留接取时间。")
		_assert_eq(
			int((quest_entry.get("objective_progress", {}) as Dictionary).get("defeat_wolves", 0)),
			3,
			"任务快照应保留封顶后的目标进度。"
		)
		_assert_eq(
			String((quest_entry.get("last_progress_context", {}) as Dictionary).get("settlement_id", "")),
			"spring_village_01",
			"任务快照应保留最近进度上下文。"
		)
	if not claimable_quests.is_empty():
		var claimable_entry: Dictionary = claimable_quests[0]
		_assert_eq(String(claimable_entry.get("quest_id", "")), "contract_settlement_warehouse", "待领奖励任务快照应保留 quest_id。")
		_assert_eq(String(claimable_entry.get("stage_id", "")), "claimable", "待领奖励任务快照应标记 claimable stage。")
		_assert_eq(int(claimable_entry.get("completed_at_world_step", -1)), 15, "待领奖励任务快照应保留完成时间。")
	_assert_true(text_snapshot.contains("[QUEST]"), "文本快照应在 PartyState 含任务时渲染 QUEST 分段。")
	_assert_true(text_snapshot.contains("active_quest_ids=contract_wolf_pack"), "文本快照应渲染激活任务 ID。")
	_assert_true(text_snapshot.contains("claimable_quest_ids=contract_settlement_warehouse"), "文本快照应渲染待领奖励任务 ID。")
	_assert_true(text_snapshot.contains("completed_quest_ids=contract_intro"), "文本快照应渲染完成任务 ID。")
	_assert_true(text_snapshot.contains("quest=contract_wolf_pack | stage=active"), "文本快照应渲染激活任务明细。")
	_assert_true(text_snapshot.contains("quest=contract_settlement_warehouse | stage=claimable"), "文本快照应渲染待领奖励任务明细。")

	builder.dispose()


func _test_snapshot_builder_rejects_legacy_quest_container_shapes() -> void:
	var legacy_map_party := FakeLegacyQuestPartyState.new()
	legacy_map_party.active_quests_variant = {
		"contract_key_backfill": _build_snapshot_quest_payload("contract_key_backfill"),
	}
	legacy_map_party.claimable_quests_variant = []
	legacy_map_party.completed_quest_ids_variant = {
		"contract_completed_by_map": true,
	}
	var legacy_map_snapshot := _build_party_quest_snapshot(legacy_map_party)

	_assert_eq(legacy_map_snapshot.get("active_quest_ids", []), [], "字典形 active_quests 不应再被 snapshot builder 接受。")
	_assert_eq(legacy_map_snapshot.get("active_quests", []), [], "quest 字典 key 不应再回填 quest_id 生成任务明细。")
	_assert_eq(legacy_map_snapshot.get("completed_quest_ids", []), [], "字典形 completed_quest_ids 不应再按 key 兼容渲染。")

	var missing_id_party := FakeLegacyQuestPartyState.new()
	var missing_id_payload := _build_snapshot_quest_payload("contract_missing_id")
	missing_id_payload.erase("quest_id")
	missing_id_party.active_quests_variant = [missing_id_payload]
	missing_id_party.claimable_quests_variant = []
	missing_id_party.completed_quest_ids_variant = []
	var missing_id_snapshot := _build_party_quest_snapshot(missing_id_party)

	_assert_eq(missing_id_snapshot.get("active_quest_ids", []), [], "缺 quest_id 的任务条目不应再被恢复。")
	_assert_eq(missing_id_snapshot.get("active_quests", []), [], "缺必需字段的任务条目不应进入任务明细。")


func _test_text_snapshot_requires_explicit_quest_stage_id() -> void:
	var snapshot: Dictionary = {
		"party": {
			"quests": {
				"active_quest_ids": ["contract_missing_stage", "contract_numeric_stage", "contract_empty_stage"],
				"claimable_quest_ids": ["contract_valid_stage"],
				"completed_quest_ids": [],
				"active_quests": [
					{
						"quest_id": "contract_missing_stage",
						"status_id": "active",
						"objective_progress": {},
						"accepted_at_world_step": 1,
						"completed_at_world_step": -1,
						"reward_claimed_at_world_step": -1,
						"last_progress_context": {},
					},
					{
						"quest_id": "contract_numeric_stage",
						"stage_id": 1,
						"status_id": "active",
						"objective_progress": {},
						"accepted_at_world_step": 1,
						"completed_at_world_step": -1,
						"reward_claimed_at_world_step": -1,
						"last_progress_context": {},
					},
					{
						"quest_id": "contract_empty_stage",
						"stage_id": "",
						"status_id": "active",
						"objective_progress": {},
						"accepted_at_world_step": 1,
						"completed_at_world_step": -1,
						"reward_claimed_at_world_step": -1,
						"last_progress_context": {},
					},
				],
				"claimable_quests": [
					{
						"quest_id": "contract_valid_stage",
						"stage_id": "claimable",
						"status_id": "completed",
						"objective_progress": {},
						"accepted_at_world_step": 1,
						"completed_at_world_step": 2,
						"reward_claimed_at_world_step": -1,
						"last_progress_context": {},
					},
				],
			},
		},
	}
	var text_snapshot := GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT.render_full_snapshot(snapshot)

	_assert_true(text_snapshot.contains("active_quest_ids=contract_missing_stage contract_numeric_stage contract_empty_stage"), "文本快照应保留 quest ID 汇总。")
	_assert_true(text_snapshot.contains("quest=contract_valid_stage | stage=claimable"), "文本快照应渲染显式 stage_id 的任务明细。")
	_assert_true(not text_snapshot.contains("quest=contract_missing_stage"), "缺 stage_id 的任务明细不应按 active 兜底渲染。")
	_assert_true(not text_snapshot.contains("quest=contract_numeric_stage"), "非字符串 stage_id 的任务明细不应渲染。")
	_assert_true(not text_snapshot.contains("quest=contract_empty_stage"), "空 stage_id 的任务明细不应渲染。")


func _test_text_snapshot_rejects_legacy_window_and_report_fields() -> void:
	var snapshot: Dictionary = {
		"shop": {
			"visible": true,
			"window_data": {
				"title": "旧商店字段",
				"settlement_id": "settlement_legacy_schema",
				"entries": [
					{
						"entry_id": "old_shop_entry_id",
						"state_label": "状态：旧字段",
						"cost_label": "价格：旧字段",
					},
				],
			},
		},
		"contract_board": {
			"visible": true,
			"window_data": {
				"title": "旧任务板字段",
				"settlement_id": "settlement_legacy_schema",
				"interaction_script_id": "old_contract_provider",
				"entries": [
					{
						"entry_id": "old_contract_entry_id",
						"quest_id": "contract_current_id",
						"state_label": "状态：旧字段",
						"cost_label": "奖励：旧字段",
					},
				],
			},
		},
		"stagecoach": {
			"visible": true,
			"window_data": {
				"title": "旧驿站字段",
				"settlement_id": "settlement_legacy_schema",
				"entries": [
					{
						"entry_id": "old_stagecoach_entry_id",
						"state_label": "状态：旧字段",
						"cost_label": "车费：旧字段",
					},
				],
			},
		},
		"forge": {
			"visible": true,
			"window_data": {
				"title": "旧锻造字段",
				"settlement_id": "settlement_legacy_schema",
				"entries": [
					{
						"entry_id": "old_forge_entry_id",
						"state_label": "状态：旧字段",
						"cost_label": "材料：旧字段",
					},
				],
			},
		},
		"battle": {
			"active": true,
			"report_entry_count": 2,
			"report_entries": [
				{
					"entry_type": "change_equipment",
					"ok": true,
					"reason_id": "equip",
					"current_ap": 9,
					"text": "old entry_type equipment report",
				},
				{
					"type": "change_equipment",
					"entry_type": "change_equipment",
					"ok": true,
					"reason_id": "equip",
					"unit_id": "player_sword_01",
					"target_unit_id": "player_sword_01",
					"slot_id": "head",
					"item_id": "leather_cap",
					"instance_id": "eq_legacy_schema",
					"ap_before": 4,
					"current_ap": 2,
					"text": "formal type without formal operation or ap_after",
				},
			],
		},
		"reward": {
			"visible": true,
			"remaining_count": 1,
			"reward": {
				"reward_id": "reward_legacy_schema",
				"member_id": "player_sword_01",
				"member_name": "剑士",
				"source_label": "测试",
				"summary_text": "旧奖励字段",
				"entries": [
					{
						"entry_type": "attribute_delta",
						"target_label": "old_reward_target_label",
						"amount": 1,
						"reason_text": "旧字段奖励",
					},
				],
			},
		},
	}
	var text_snapshot := GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT.render_full_snapshot(snapshot)

	_assert_true(text_snapshot.contains("provider_interaction_id="), "缺 provider_interaction_id 时文本快照只应渲染空正式字段。")
	_assert_true(not text_snapshot.contains("old_contract_provider"), "旧 interaction_script_id 不应回填 provider_interaction_id。")
	_assert_true(not text_snapshot.contains("old_shop_entry_id"), "shop 条目缺 display_name 时不应回退 entry_id。")
	_assert_true(not text_snapshot.contains("old_contract_entry_id"), "contract board 条目缺 display_name 时不应回退 entry_id。")
	_assert_true(not text_snapshot.contains("old_stagecoach_entry_id"), "stagecoach 条目缺 display_name 时不应回退 entry_id。")
	_assert_true(not text_snapshot.contains("old_forge_entry_id"), "forge 条目缺 display_name 时不应回退 entry_id。")
	_assert_true(not text_snapshot.contains("old entry_type equipment report"), "缺 type 的 change_equipment 战报不应靠 entry_type 渲染为有效装备战报。")
	_assert_true(text_snapshot.contains("report=change_equipment | ok=true | error= | op= | unit=player_sword_01"), "正式 type 存在但缺 operation 时不应从 reason_id 回填 op。")
	_assert_true(text_snapshot.contains("ap=4>0"), "正式 type 存在但缺 ap_after 时不应从 current_ap 回填 AP。")
	_assert_true(not text_snapshot.contains("op=equip"), "旧 reason_id 不应回填 change_equipment operation。")
	_assert_true(not text_snapshot.contains("ap=4>2"), "旧 current_ap 不应回填 change_equipment ap_after。")
	_assert_true(text_snapshot.contains("entry=attribute_delta |  | amount=1"), "奖励条目缺 target_id 时应渲染空正式目标字段。")
	_assert_true(not text_snapshot.contains("old_reward_target_label"), "奖励条目缺 target_id 时不应回退 target_label。")


func _test_snapshot_builder_cross_references_quest_items_in_text_snapshot() -> void:
	var runtime := FakeQuestRuntime.new()
	var party_state := FakeQuestPartyState.new()
	var quest_state := QuestState.new()
	quest_state.quest_id = &"contract_archive_delivery"
	quest_state.mark_accepted(7)
	quest_state.record_objective_progress(&"deliver_dispatch", 1, 1, {
		"item_id": "sealed_dispatch",
		"submitted_quantity": 1,
	})
	party_state.active_quests = [quest_state]
	runtime.party_state = party_state
	runtime.active_modal_id = "warehouse"
	runtime.warehouse_window_data = {
		"title": "共享仓库",
		"entries": [
			{
				"item_id": "sealed_dispatch",
				"quantity": 1,
				"total_quantity": 1,
				"is_stackable": true,
				"stack_limit": 20,
				"storage_mode": "stack",
			},
			{
				"item_id": "bandit_insignia",
				"quantity": 3,
				"total_quantity": 3,
				"is_stackable": true,
				"stack_limit": 20,
				"storage_mode": "stack",
			},
			{
				"item_id": "moonfern_sample",
				"quantity": 2,
				"total_quantity": 2,
				"is_stackable": true,
				"stack_limit": 20,
				"storage_mode": "stack",
			},
		],
	}

	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()
	var text_snapshot := builder.build_text_snapshot()
	var warehouse_entry_ids := _extract_window_entry_value_strings(
		snapshot.get("warehouse", {}).get("window_data", {}).get("entries", []),
		"item_id"
	)

	_assert_true(bool(snapshot.get("warehouse", {}).get("visible", false)), "任务物品交叉引用回归中仓库快照应保持可见。")
	_assert_eq(
		warehouse_entry_ids,
		["sealed_dispatch", "bandit_insignia", "moonfern_sample"],
		"仓库快照应稳定暴露正式任务物品条目。"
	)
	_assert_true(text_snapshot.contains("context=item_id=sealed_dispatch submitted_quantity=1"), "文本快照应在 QUEST 分段保留任务物品上下文。")
	_assert_true(text_snapshot.contains("entry=sealed_dispatch | qty=1 | total=1 | stackable=true"), "文本快照应在 WAREHOUSE 分段渲染封缄急件。")
	_assert_true(text_snapshot.contains("entry=bandit_insignia | qty=3 | total=3 | stackable=true"), "文本快照应在 WAREHOUSE 分段渲染匪徒纹章。")
	_assert_true(text_snapshot.contains("entry=moonfern_sample | qty=2 | total=2 | stackable=true"), "文本快照应在 WAREHOUSE 分段渲染月蕨样本。")

	builder.dispose()


func _test_snapshot_builder_exposes_contract_board_modal_snapshot() -> void:
	var runtime := FakeQuestRuntime.new()
	runtime.active_modal_id = "contract_board"
	runtime.contract_board_window_data = {
		"title": "春泉村 · 任务板",
		"settlement_id": "spring_village_01",
		"provider_interaction_id": "service_contract_board",
		"entries": [
			{
				"entry_id": "contract_first_hunt",
				"quest_id": "contract_first_hunt",
				"display_name": "首轮狩猎",
				"state_label": "状态：可查看",
				"cost_label": "奖励：80 金",
			},
			{
				"entry_id": "contract_manual_drill",
				"quest_id": "contract_manual_drill",
				"display_name": "训练记录",
				"state_label": "状态：可查看",
				"cost_label": "奖励：30 金",
			},
		],
	}

	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()
	var text_snapshot := builder.build_text_snapshot()
	var contract_board_snapshot: Dictionary = snapshot.get("contract_board", {})
	var entry_ids := _extract_window_entry_value_strings(contract_board_snapshot.get("window_data", {}).get("entries", []), "quest_id")

	_assert_true(bool(contract_board_snapshot.get("visible", false)), "contract board modal 激活时快照应暴露 contract_board.visible。")
	_assert_eq(String(contract_board_snapshot.get("window_data", {}).get("provider_interaction_id", "")), "service_contract_board", "contract board 快照应保留当前 provider_interaction_id。")
	_assert_eq(entry_ids, ["contract_first_hunt", "contract_manual_drill"], "contract board 快照应稳定暴露当前任务板条目列表。")
	_assert_true(text_snapshot.contains("[CONTRACT_BOARD]"), "文本快照应渲染 CONTRACT_BOARD 分段。")
	_assert_true(text_snapshot.contains("provider_interaction_id=service_contract_board"), "文本快照应渲染当前任务板 provider_interaction_id。")
	_assert_true(text_snapshot.contains("首轮狩猎"), "文本快照应渲染首轮狩猎条目。")
	_assert_true(text_snapshot.contains("训练记录"), "文本快照应渲染训练记录条目。")

	builder.dispose()


func _test_snapshot_builder_exposes_forge_modal_snapshot() -> void:
	var runtime := FakeQuestRuntime.new()
	runtime.active_modal_id = "forge"
	runtime.forge_window_data = {
		"title": "灰烬镇 · 大师重铸",
		"settlement_id": "forge_town",
		"entries": [
			{
				"display_name": "大师重铸：铁制大剑",
				"state_label": "状态：可重铸",
				"cost_label": "材料：1 件 青铜短剑、2 件 铁矿石",
			},
		],
	}

	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()
	var text_snapshot := builder.build_text_snapshot()

	_assert_true(bool(snapshot.get("forge", {}).get("visible", false)), "forge modal 激活时快照应暴露 forge.visible。")
	_assert_eq(String(snapshot.get("forge", {}).get("window_data", {}).get("title", "")), "灰烬镇 · 大师重铸", "forge 快照应保留窗口标题。")
	_assert_true(text_snapshot.contains("[FORGE]"), "文本快照应渲染 FORGE 分段。")
	_assert_true(text_snapshot.contains("大师重铸：铁制大剑"), "文本快照应渲染 forge 配方名称。")

	builder.dispose()


func _test_snapshot_builder_exposes_generic_forge_modal_snapshot() -> void:
	var runtime := FakeQuestRuntime.new()
	runtime.active_modal_id = "forge"
	runtime.active_shop_context = {
		"title": "灰烬镇 · 熔炉整备",
		"settlement_id": "forge_town",
		"panel_kind": "forge",
		"submission_source": "forge",
		"entries": [
			{
				"entry_id": "forge:temper_edge",
				"display_name": "刃口淬火",
				"state_label": "状态：可执行",
				"cost_label": "材料：1 件 铁矿石、1 件 皮革护衣",
			},
		],
	}

	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()
	var text_snapshot := builder.build_text_snapshot()

	_assert_eq(String(snapshot.get("forge", {}).get("window_data", {}).get("title", "")), "灰烬镇 · 熔炉整备", "通用 forge modal 应从共享窗口上下文进入 forge 快照。")
	_assert_true(text_snapshot.contains("刃口淬火"), "文本快照应渲染通用 forge 条目名称。")
	_assert_true((snapshot.get("shop", {}).get("window_data", {}) as Dictionary).is_empty(), "forge panel_kind 不应继续出现在 shop 快照中。")

	builder.dispose()


func _test_snapshot_builder_requires_panel_kind_for_forge_modal() -> void:
	var runtime := FakeQuestRuntime.new()
	runtime.active_modal_id = "forge"
	runtime.active_shop_context = {
		"title": "旧 forge 来源",
		"settlement_id": "forge_town",
		"submission_source": "forge",
		"entries": [],
	}

	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()

	_assert_true((snapshot.get("forge", {}).get("window_data", {}) as Dictionary).is_empty(), "只有 submission_source=forge 的旧窗口上下文不应再被识别为 forge modal。")

	builder.dispose()


func _test_snapshot_builder_exposes_game_over_snapshot() -> void:
	var runtime := FakeQuestRuntime.new()
	runtime.active_modal_id = "game_over"
	runtime.game_over_context = {
		"title": "Game Over",
		"description": "主角已阵亡，本次旅程结束。",
		"confirm_text": "返回标题",
		"main_character_member_id": "player_sword_01",
		"main_character_name": "剑士",
		"main_character_dead": true,
	}

	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()
	var text_snapshot := builder.build_text_snapshot()
	var game_over_snapshot: Dictionary = snapshot.get("game_over", {})

	_assert_eq(String(game_over_snapshot.get("title", "")), "Game Over", "game_over 快照应暴露标题。")
	_assert_eq(String(game_over_snapshot.get("main_character_member_id", "")), "player_sword_01", "game_over 快照应暴露主角成员 ID。")
	_assert_true(bool(game_over_snapshot.get("main_character_dead", false)), "game_over 快照应标记主角死亡。")
	_assert_true(text_snapshot.contains("[GAME_OVER]"), "文本快照应渲染 GAME_OVER 分段。")
	_assert_true(text_snapshot.contains("main_character_member_id=player_sword_01"), "文本快照应渲染主角成员 ID。")
	_assert_true(text_snapshot.contains("main_character_dead=true"), "文本快照应渲染主角死亡标记。")

	builder.dispose()


func _test_snapshot_builder_exposes_battle_loot_snapshot() -> void:
	var runtime := FakeQuestRuntime.new()
	runtime.last_battle_loot_snapshot = {
		"battle_name": "荒狼巢穴",
		"winner_faction_id": "player",
		"loot_entries": [
			{
				"item_id": "beast_hide",
				"quantity": 2,
			},
		],
		"loot_entry_count": 1,
		"loot_summary_text": "兽皮 x2",
		"overflow_entries": [
			{
				"item_id": "beast_hide",
				"quantity": 1,
			},
		],
		"overflow_entry_count": 1,
		"overflow_summary_text": "兽皮 x1",
	}

	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()
	var text_snapshot := builder.build_text_snapshot()
	var loot_snapshot: Dictionary = snapshot.get("loot", {})

	_assert_eq(String(loot_snapshot.get("battle_name", "")), "荒狼巢穴", "loot 快照应保留最近一次战斗名称。")
	_assert_eq(String(loot_snapshot.get("loot_summary_text", "")), "兽皮 x2", "loot 快照应暴露稳定 loot 摘要。")
	_assert_eq(String(loot_snapshot.get("overflow_summary_text", "")), "兽皮 x1", "loot 快照应暴露稳定 overflow 摘要。")
	_assert_true(text_snapshot.contains("[LOOT]"), "文本快照应为最近一次战斗掉落渲染 LOOT 分段。")
	_assert_true(text_snapshot.contains("loot_summary=兽皮 x2"), "文本快照应渲染 loot 摘要。")
	_assert_true(text_snapshot.contains("overflow_summary=兽皮 x1"), "文本快照应渲染 overflow 摘要。")

	builder.dispose()


func _test_snapshot_builder_omits_loot_section_when_empty() -> void:
	var runtime := FakeQuestRuntime.new()

	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()
	var text_snapshot := builder.build_text_snapshot()

	_assert_true((snapshot.get("loot", {}) as Dictionary).is_empty(), "没有最近掉落时 headless snapshot 不应强行生成 loot 段。")
	_assert_true(not text_snapshot.contains("[LOOT]"), "没有最近掉落时文本快照不应插入 LOOT 分段。")
	_assert_true(text_snapshot.find("[BATTLE]") >= 0 and text_snapshot.find("[REWARD]") > text_snapshot.find("[BATTLE]"), "没有 loot 时既有 BATTLE -> REWARD 顺序应保持稳定。")

	builder.dispose()


func _create_test_session():
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "GameSession 应能基于测试世界配置创建新存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return null
	return game_session


func _cleanup_test_session(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _build_party_quest_snapshot(party_state) -> Dictionary:
	var runtime := FakeQuestRuntime.new()
	runtime.party_state = party_state
	var builder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT.new()
	builder.setup(runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()
	builder.dispose()
	return snapshot.get("party", {}).get("quests", {})


func _build_snapshot_quest_payload(quest_id: String) -> Dictionary:
	return {
		"quest_id": quest_id,
		"status_id": "active",
		"objective_progress": {},
		"accepted_at_world_step": 1,
		"completed_at_world_step": -1,
		"reward_claimed_at_world_step": -1,
		"last_progress_context": {},
	}


class FakeQuestPartyState:
	extends RefCounted

	var gold := 0
	var leader_member_id: StringName = &""
	var active_member_ids: Array = []
	var reserve_member_ids: Array = []
	var active_quests: Array = []
	var claimable_quests: Array = []
	var completed_quest_ids: Array = []

	func get_member_state(_member_id: StringName):
		return null

	func get_active_quests():
		return active_quests

	func get_claimable_quests():
		return claimable_quests

	func get_completed_quest_ids():
		return completed_quest_ids


class FakeLegacyQuestPartyState:
	extends FakeQuestPartyState

	var active_quests_variant = null
	var claimable_quests_variant = null
	var completed_quest_ids_variant = null

	func get_active_quests():
		return active_quests_variant

	func get_claimable_quests():
		return claimable_quests_variant

	func get_completed_quest_ids():
		return completed_quest_ids_variant


class FakeQuestRuntime:
	extends RefCounted

	var party_state: FakeQuestPartyState = null
	var active_modal_id := ""
	var contract_board_window_data: Dictionary = {}
	var forge_window_data: Dictionary = {}
	var active_shop_context: Dictionary = {}
	var warehouse_window_data: Dictionary = {}
	var last_battle_loot_snapshot: Dictionary = {}
	var game_over_context: Dictionary = {}

	func is_battle_active() -> bool:
		return false

	func get_status_text() -> String:
		return ""

	func get_active_modal_id() -> String:
		return active_modal_id

	func get_selected_settlement() -> Dictionary:
		return {}

	func get_selected_world_npc() -> Dictionary:
		return {}

	func get_selected_encounter_anchor():
		return null

	func get_selected_world_event() -> Dictionary:
		return {}

	func get_active_map_id() -> String:
		return ""

	func get_active_map_display_name() -> String:
		return ""

	func is_submap_active() -> bool:
		return false

	func get_world_step() -> int:
		return 0

	func get_player_coord() -> Vector2i:
		return Vector2i.ZERO

	func is_player_visible_on_world_map() -> bool:
		return false

	func get_selected_coord() -> Vector2i:
		return Vector2i.ZERO

	func get_pending_submap_prompt() -> Dictionary:
		return {}

	func get_submap_return_hint_text() -> String:
		return ""

	func get_game_over_context() -> Dictionary:
		return game_over_context.duplicate(true)

	func get_party_state():
		return party_state

	func get_party_selected_member_id() -> StringName:
		return &""

	func get_pending_reward_count() -> int:
		return 0

	func get_member_achievement_summary(_member_id: StringName) -> Dictionary:
		return {}

	func get_member_attribute_snapshot(_member_id: StringName):
		return null

	func get_member_equipped_entries(_member_id: StringName) -> Array:
		return []

	func get_resolved_settlement_id() -> String:
		return ""

	func get_settlement_window_data(_settlement_id: String = "") -> Dictionary:
		return {}

	func get_settlement_feedback_text() -> String:
		return ""

	func get_shop_window_data() -> Dictionary:
		return {}

	func get_contract_board_window_data() -> Dictionary:
		return contract_board_window_data.duplicate(true)

	func get_active_contract_board_context() -> Dictionary:
		return contract_board_window_data.duplicate(true)

	func get_active_shop_context() -> Dictionary:
		return active_shop_context.duplicate(true)

	func get_forge_window_data() -> Dictionary:
		return forge_window_data.duplicate(true)

	func get_stagecoach_window_data() -> Dictionary:
		return {}

	func get_character_info_context() -> Dictionary:
		return {}

	func get_active_warehouse_entry_label() -> String:
		return ""

	func get_warehouse_window_data() -> Dictionary:
		return warehouse_window_data.duplicate(true)

	func get_battle_state():
		return null

	func get_snapshot_reward():
		return null

	func get_last_battle_loot_snapshot() -> Dictionary:
		return last_battle_loot_snapshot.duplicate(true)

	func get_current_promotion_prompt() -> Dictionary:
		return {}

	func get_log_snapshot(_limit: int = 30) -> Dictionary:
		return {}

	func get_nearby_encounter_entries(_limit: int = 8) -> Array[Dictionary]:
		return []

	func get_nearby_world_event_entries(_limit: int = 8) -> Array[Dictionary]:
		return []


func _extract_window_entry_value_strings(entry_variants, key: String) -> Array[String]:
	var result: Array[String] = []
	if entry_variants is not Array:
		return result
	for entry_variant in entry_variants:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		result.append(String(entry.get(key, "")))
	return result
