extends SceneTree

const BattleBoard2D = preload("res://scripts/ui/battle_board_2d.gd")
const BattleBoardScene = preload("res://scenes/ui/battle_board_2d.tscn")
const BattleHudAdapter = preload("res://scripts/ui/battle_hud_adapter.gd")
const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle_runtime_module.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const BattleMapPanel = preload("res://scripts/ui/battle_map_panel.gd")
const BattlePanelScene = preload("res://scenes/ui/battle_map_panel.tscn")
const RuntimeLogDock = preload("res://scripts/ui/runtime_log_dock.gd")
const RuntimeLogDockScene = preload("res://scenes/ui/runtime_log_dock.tscn")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")

const VIEWPORT_SIZE := Vector2(1280.0, 720.0)
const ULTRAWIDE_PANEL_SIZE := Vector2i(3857, 786)
const BLACK_CONTRACT_PUSH_SKILL_ID: StringName = &"black_contract_push"
const WARRIOR_HEAVY_STRIKE_SKILL_ID: StringName = &"warrior_heavy_strike"
const ARCHER_MULTISHOT_SKILL_ID: StringName = &"archer_multishot"
const ARCHER_MULTISHOT_VARIANT_ID: StringName = &"multishot_volley"
const ACTION_TITHE_VARIANT_ID: StringName = &"action_tithe"

var _failures: Array[String] = []


class MockGameSession:
	extends Node

	var skill_defs: Dictionary = {}

	func get_skill_defs() -> Dictionary:
		return skill_defs


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_multi_unit_hud_copy_and_selection_state()
	await _test_repeat_attack_hud_preview_matches_runtime_resolver()
	await _test_single_hit_hud_preview_matches_runtime_resolver()
	await _test_battle_panel_hover_target_surfaces_hit_preview()
	await _test_repeat_attack_hud_preview_uses_fate_aware_success_rate()
	await _test_skill_slot_surfaces_stamina_and_cooldown_blockers()
	await _test_multi_unit_board_highlights_confirm_state()
	await _test_multi_unit_board_confirm_halo_follows_active_unit()
	await _test_multi_unit_board_highlights_continue_state()
	await _test_movement_mode_uses_classic_srpg_style_markers()
	await _test_fate_preview_badges_surface_high_threat_and_mercy_states()
	await _test_force_hit_no_crit_skill_hides_standard_fate_badges()
	await _test_hybrid_multi_unit_skill_uses_shared_fate_policy()
	_test_battle_state_log_buffer_enforces_entry_cap()
	await _test_runtime_log_dock_syncs_battle_entries()
	await _test_battle_panel_flushes_to_ultrawide_edges()
	await _test_battle_panel_loading_overlay_waits_for_first_presented_frame()
	if _failures.is_empty():
		print("Battle UI regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle UI regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_multi_unit_hud_copy_and_selection_state() -> void:
	var game_session := await _install_mock_game_session()
	game_session.skill_defs = {
		&"archer_multishot": _build_multi_unit_skill_def(),
	}
	var adapter := BattleHudAdapter.new()
	var state := _build_state()
	var snapshot := adapter.build_snapshot(
		state,
		Vector2i(0, 0),
		&"archer_multishot",
		"连珠箭",
		"",
		[Vector2i(1, 1), Vector2i(2, 1)],
		3
	)
	_assert_eq(snapshot.get("selected_skill_target_selection_mode", ""), "multi_unit", "multi_unit 技能应暴露目标选择模式。")
	_assert_eq(int(snapshot.get("selected_skill_target_min_count", 0)), 2, "multi_unit 技能应暴露最小目标数量。")
	_assert_eq(int(snapshot.get("selected_skill_target_max_count", 0)), 3, "multi_unit 技能应暴露最大目标数量。")
	_assert_true(String(snapshot.get("skill_subtitle", "")).contains("已满足最小数量"), "multi_unit HUD 副标题应提示确认态。")
	_assert_true(String(snapshot.get("skill_subtitle", "")).contains("点击自己或空地确认"), "multi_unit HUD 副标题应说明确认路径。")
	game_session.queue_free()
	await process_frame


func _test_repeat_attack_hud_preview_matches_runtime_resolver() -> void:
	var skill_def := _get_repeat_attack_skill_def()
	_assert_true(skill_def != null and skill_def.combat_profile != null, "连斩命中预览前置：saint_blade_combo 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		return

	var game_session := await _install_mock_game_session()
	game_session.skill_defs = {
		skill_def.skill_id: skill_def,
	}

	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, game_session.skill_defs, {}, {}, null)
	var state := _build_repeat_attack_state()
	var attacker := _build_repeat_attack_unit(
		&"saint_blade_ui_user",
		"圣剑使",
		&"player",
		Vector2i(1, 1),
		[skill_def.skill_id],
		2,
		4
	)
	attacker.current_aura = 6
	attacker.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 90)
	var defender := _build_repeat_attack_unit(
		&"saint_blade_ui_target",
		"训练木桩",
		&"enemy",
		Vector2i(2, 1),
		[],
		2,
		0
	)
	defender.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	_add_unit_to_runtime_state(runtime, state, attacker, false)
	_add_unit_to_runtime_state(runtime, state, defender, true)
	state.phase = &"unit_acting"
	state.active_unit_id = attacker.unit_id
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = attacker.unit_id
	command.skill_id = skill_def.skill_id
	command.target_unit_id = defender.unit_id
	command.target_coord = defender.coord
	var preview := runtime.preview_command(command)
	var hit_preview_text := String(preview.hit_preview.get("summary_text", ""))
	_assert_true(preview != null and not preview.hit_preview.is_empty(), "repeat_attack 预览应暴露共享的命中摘要。")
	_assert_true(hit_preview_text.begins_with("预计命中率 "), "repeat_attack 预览摘要应使用统一的 resolver 文案前缀。")
	_assert_true(hit_preview_text.contains("需 "), "repeat_attack 预览摘要应暴露 required roll。")
	_assert_eq((preview.hit_preview.get("stage_hit_rates", []) as Array).size(), 2, "repeat_attack 预览应按当前 Aura 只展示可支付的最大段数。")
	var stage_required_rolls := preview.hit_preview.get("stage_required_rolls", []) as Array
	var stage_preview_texts := preview.hit_preview.get("stage_preview_texts", []) as Array
	_assert_eq(stage_required_rolls.size(), 2, "repeat_attack 预览应按当前 Aura 上限暴露每段 required roll。")
	_assert_eq(stage_preview_texts.size(), 2, "repeat_attack 预览应按当前 Aura 上限输出 resolver 阶段摘要。")
	for stage_preview_text in stage_preview_texts:
		_assert_true(String(stage_preview_text).contains("需 "), "repeat_attack 每段预览文案都应包含 required roll。")

	var adapter := BattleHudAdapter.new()
	var snapshot := adapter.build_snapshot(
		state,
		defender.coord,
		skill_def.skill_id,
		skill_def.display_name,
		"",
		[],
		1,
		[]
	)
	_assert_eq(
		String(snapshot.get("selected_skill_hit_preview_text", "")),
		hit_preview_text,
		"HUD snapshot 应复用 runtime preview 的命中摘要。"
	)
	_assert_eq(
		snapshot.get("selected_skill_hit_stage_rates", []),
		preview.hit_preview.get("stage_hit_rates", []),
		"HUD snapshot 应复用 runtime preview 的阶段命中率数组。"
	)
	_assert_true(String(snapshot.get("skill_subtitle", "")).contains(hit_preview_text), "HUD 副标题应显示 resolver 命中摘要。")

	game_session.queue_free()
	await process_frame


func _test_single_hit_hud_preview_matches_runtime_resolver() -> void:
	var skill_def := _get_skill_def(WARRIOR_HEAVY_STRIKE_SKILL_ID)
	_assert_true(skill_def != null and skill_def.combat_profile != null, "单段技能命中预览前置：warrior_heavy_strike 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		return

	var game_session := await _install_mock_game_session()
	game_session.skill_defs = {
		skill_def.skill_id: skill_def,
	}

	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, game_session.skill_defs, {}, {}, null)
	var state := _build_repeat_attack_state()
	var attacker := _build_repeat_attack_unit(
		&"heavy_strike_ui_user",
		"重击战士",
		&"player",
		Vector2i(1, 1),
		[skill_def.skill_id],
		3,
		4
	)
	attacker.current_stamina = 30
	attacker.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 80)
	var defender := _build_repeat_attack_unit(
		&"heavy_strike_ui_target",
		"高闪避木桩",
		&"enemy",
		Vector2i(2, 1),
		[],
		2,
		0
	)
	defender.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 70)
	_add_unit_to_runtime_state(runtime, state, attacker, false)
	_add_unit_to_runtime_state(runtime, state, defender, true)
	state.phase = &"unit_acting"
	state.active_unit_id = attacker.unit_id
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = attacker.unit_id
	command.skill_id = skill_def.skill_id
	command.target_unit_id = defender.unit_id
	command.target_coord = defender.coord
	var preview := runtime.preview_command(command)
	var hit_preview_text := String(preview.hit_preview.get("summary_text", ""))
	_assert_true(preview != null and not preview.hit_preview.is_empty(), "普通单段技能 runtime preview 应暴露命中摘要。")
	_assert_true(hit_preview_text.begins_with("预计命中率 "), "普通单段技能命中预览摘要应使用统一 resolver 文案前缀。")
	_assert_eq((preview.hit_preview.get("stage_hit_rates", []) as Array).size(), 1, "普通单段技能命中预览应暴露单段命中率。")

	var adapter := BattleHudAdapter.new()
	var snapshot := adapter.build_snapshot(
		state,
		defender.coord,
		skill_def.skill_id,
		skill_def.display_name,
		"",
		[],
		1,
		[]
	)
	_assert_eq(
		String(snapshot.get("selected_skill_hit_preview_text", "")),
		hit_preview_text,
		"HUD snapshot 应保留普通单段技能的 runtime 命中摘要。"
	)
	_assert_eq(
		snapshot.get("selected_skill_hit_stage_rates", []),
		preview.hit_preview.get("stage_hit_rates", []),
		"HUD snapshot 应保留普通单段技能的阶段命中率数组。"
	)
	_assert_true(String(snapshot.get("skill_subtitle", "")).contains(hit_preview_text), "普通单段技能 HUD 副标题应显示 resolver 命中摘要。")

	game_session.queue_free()
	await process_frame


func _test_battle_panel_hover_target_surfaces_hit_preview() -> void:
	var skill_def := _get_repeat_attack_skill_def()
	_assert_true(skill_def != null and skill_def.combat_profile != null, "悬停命中预览前置：saint_blade_combo 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		return

	var game_session := await _install_mock_game_session()
	game_session.skill_defs = {
		skill_def.skill_id: skill_def,
	}

	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, game_session.skill_defs, {}, {}, null)
	var state := _build_repeat_attack_state()
	var attacker := _build_repeat_attack_unit(
		&"hover_preview_user",
		"悬停战士",
		&"player",
		Vector2i(1, 1),
		[skill_def.skill_id],
		3,
		4
	)
	attacker.current_aura = 6
	attacker.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 80)
	var defender := _build_repeat_attack_unit(
		&"hover_preview_target",
		"悬停木桩",
		&"enemy",
		Vector2i(2, 1),
		[],
		2,
		0
	)
	defender.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 70)
	_add_unit_to_runtime_state(runtime, state, attacker, false)
	_add_unit_to_runtime_state(runtime, state, defender, true)
	state.phase = &"unit_acting"
	state.active_unit_id = attacker.unit_id
	runtime._state = state

	var panel := BattlePanelScene.instantiate() as BattleMapPanel
	root.add_child(panel)
	await process_frame
	panel.size = VIEWPORT_SIZE
	panel.refresh(
		state,
		attacker.coord,
		skill_def.skill_id,
		skill_def.display_name,
		"",
		[],
		[defender.coord],
		1,
		[]
	)
	await process_frame
	_assert_true(
		not panel.skill_subtitle_label.text.contains("预计命中率"),
		"技能刚选中且还未悬停目标时，不应错误展示当前行动单位的命中率。"
	)
	var initial_highlight_layer := panel._battle_board.get_node("TargetHighlightLayer") if panel._battle_board != null else null
	_assert_true(
		initial_highlight_layer != null and not _collect_node_names(initial_highlight_layer).has("HitBadge_%d_%d" % [defender.coord.x, defender.coord.y]),
		"技能刚选中且还未悬停目标时，棋盘目标上方不应显示命中率浮标。"
	)

	panel.refresh_overlay(
		state,
		defender.coord,
		skill_def.skill_id,
		skill_def.display_name,
		"",
		[],
		[defender.coord],
		1,
		[]
	)
	await process_frame
	_assert_true(
		panel.skill_subtitle_label.text.contains("预计命中率") and panel.skill_subtitle_label.text.contains("需 "),
		"选中技能后悬停到合法目标时，BattleMapPanel 应在技能副标题展示命中率预览。"
	)
	var highlight_layer := panel._battle_board.get_node("TargetHighlightLayer") if panel._battle_board != null else null
	var badge_name := "HitBadge_%d_%d" % [defender.coord.x, defender.coord.y]
	var hit_badge := highlight_layer.get_node_or_null(badge_name) if highlight_layer != null else null
	_assert_true(hit_badge != null, "选中技能后悬停到合法目标时，目标上方应显示命中率浮标。")
	var hit_badge_label := _find_first_label(hit_badge)
	_assert_true(
		hit_badge_label != null and hit_badge_label.text.begins_with("命中 "),
		"目标上方命中率浮标应显示简短命中百分比。"
	)

	panel.queue_free()
	runtime.dispose()
	game_session.queue_free()
	await process_frame


func _test_skill_slot_surfaces_stamina_and_cooldown_blockers() -> void:
	var skill_def := _get_skill_def(&"archer_long_draw")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "UI blocker 回归前置：archer_long_draw 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		return

	var game_session := await _install_mock_game_session()
	game_session.skill_defs = {
		skill_def.skill_id: skill_def,
	}
	var adapter := BattleHudAdapter.new()
	var state := _build_state()
	var active_unit := state.units.get(state.active_unit_id) as BattleUnitState
	if active_unit == null:
		_assert_true(false, "UI blocker 回归前置：测试状态应存在当前行动单位。")
		game_session.queue_free()
		await process_frame
		return
	active_unit.known_active_skill_ids = [skill_def.skill_id]
	active_unit.known_skill_level_map[skill_def.skill_id] = 1
	active_unit.current_ap = 2
	active_unit.current_stamina = 1
	active_unit.attribute_snapshot.set_value(&"stamina_max", 2)

	var stamina_snapshot := adapter.build_snapshot(state, Vector2i(0, 0))
	var stamina_slots: Array = stamina_snapshot.get("skill_slots", [])
	var stamina_slot: Dictionary = stamina_slots[0] if not stamina_slots.is_empty() and stamina_slots[0] is Dictionary else {}
	_assert_true(bool(stamina_slot.get("is_disabled", false)), "体力不足时 HUD skill slot 应保持禁用。")
	_assert_eq(String(stamina_slot.get("footer_text", "")), "ST不足", "体力不足时 HUD skill slot footer 应显示 ST不足。")
	_assert_eq(String(stamina_slot.get("disabled_reason", "")), "体力不足", "体力不足时 HUD skill slot 应暴露明确的禁用原因。")

	active_unit.current_stamina = 4
	active_unit.attribute_snapshot.set_value(&"stamina_max", 4)
	active_unit.cooldowns[skill_def.skill_id] = 10
	var cooldown_snapshot := adapter.build_snapshot(state, Vector2i(0, 0))
	var cooldown_slots: Array = cooldown_snapshot.get("skill_slots", [])
	var cooldown_slot: Dictionary = cooldown_slots[0] if not cooldown_slots.is_empty() and cooldown_slots[0] is Dictionary else {}
	_assert_true(bool(cooldown_slot.get("is_disabled", false)), "冷却未结束时 HUD skill slot 应保持禁用。")
	_assert_eq(String(cooldown_slot.get("footer_text", "")), "CD 10", "冷却未结束时 HUD skill slot footer 应显示剩余 CD。")
	_assert_true(String(cooldown_slot.get("disabled_reason", "")).contains("冷却"), "冷却未结束时 HUD skill slot 应暴露冷却禁用原因。")

	game_session.queue_free()
	await process_frame


func _test_multi_unit_board_highlights_confirm_state() -> void:
	var board := await _instantiate_board()
	var state := _build_state()
	board.configure(
		state,
		Vector2i(0, 0),
		[Vector2i(1, 1), Vector2i(2, 1)],
		[Vector2i(3, 1)],
		&"multi_unit",
		2,
		3
	)
	await process_frame
	var highlight_layer := board.get_node("TargetHighlightLayer")
	var highlight_names := _collect_node_names(highlight_layer)
	_assert_true(highlight_names.has("LockedTarget_1_1"), "锁定目标应有独立高亮节点。")
	_assert_true(highlight_names.has("LockedTarget_2_1"), "第二个锁定目标应有独立高亮节点。")
	_assert_true(highlight_names.has("ValidTarget_3_1"), "可选目标应有独立高亮节点。")
	_assert_true(highlight_names.has("ConfirmReady_0_0"), "满足最小数量时应显示确认态高亮。")
	board.queue_free()
	await process_frame


func _test_multi_unit_board_confirm_halo_follows_active_unit() -> void:
	var board := await _instantiate_board()
	var state := _build_state()
	board.configure(
		state,
		Vector2i(2, 1),
		[Vector2i(1, 1), Vector2i(2, 1)],
		[Vector2i(3, 1)],
		&"multi_unit",
		2,
		3
	)
	await process_frame
	var highlight_layer := board.get_node("TargetHighlightLayer")
	var highlight_names := _collect_node_names(highlight_layer)
	_assert_true(highlight_names.has("ConfirmReady_0_0"), "确认态 halo 应始终指向当前行动单位，而不是最后一个锁定目标。")
	_assert_true(not highlight_names.has("ConfirmReady_2_1"), "确认态 halo 不应继续画在会触发取消选择的锁定目标上。")
	board.queue_free()
	await process_frame


func _test_multi_unit_board_highlights_continue_state() -> void:
	var board := await _instantiate_board()
	var state := _build_state()
	board.configure(
		state,
		Vector2i(0, 0),
		[Vector2i(1, 1)],
		[Vector2i(2, 1)],
		&"multi_unit",
		2,
		3
	)
	await process_frame
	var highlight_layer := board.get_node("TargetHighlightLayer")
	var highlight_names := _collect_node_names(highlight_layer)
	_assert_true(highlight_names.has("LockedTarget_1_1"), "继续选目标时仍应显示已锁定目标。")
	_assert_true(highlight_names.has("ValidTarget_2_1"), "继续选目标时仍应显示可选目标。")
	_assert_true(not highlight_names.has("ConfirmReady_0_0"), "未达到最小数量时不应显示确认态高亮。")
	board.queue_free()
	await process_frame


func _test_movement_mode_uses_classic_srpg_style_markers() -> void:
	var board := await _instantiate_board()
	var state := _build_state()
	board.configure(
		state,
		Vector2i(0, 0),
		[],
		[Vector2i(1, 0), Vector2i(0, 1)],
		&"movement",
		1,
		1
	)
	await process_frame

	var highlight_layer := board.get_node("TargetHighlightLayer")
	_assert_eq(
		_collect_node_names(highlight_layer).size(),
		0,
		"movement 模式不应复用顶层红色目标高亮节点。"
	)
	var marker_layer := board.get_node("MarkerH0") as TileMapLayer
	var reachable_image := _get_layer_cell_image(marker_layer, Vector2i(1, 0))
	_assert_true(reachable_image != null, "movement 模式应在 Marker 层渲染可达地格。")
	if reachable_image != null:
		var center := reachable_image.get_pixel(reachable_image.get_width() / 2, reachable_image.get_height() / 2)
		_assert_true(center.a >= 0.28 and center.a <= 0.52, "可达地格中心像素应保持更清晰的半透明高亮。")
		_assert_true(
			center.b > center.g and center.g > center.r,
			"可达地格应保持偏青蓝的可行走提示色。"
		)

	board.queue_free()
	await process_frame


func _test_fate_preview_badges_surface_high_threat_and_mercy_states() -> void:
	var skill_def := _get_skill_def(WARRIOR_HEAVY_STRIKE_SKILL_ID)
	_assert_true(skill_def != null and skill_def.combat_profile != null, "命运概览前置：warrior_heavy_strike 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		return

	var game_session := await _install_mock_game_session()
	game_session.skill_defs = {
		skill_def.skill_id: skill_def,
	}

	var high_threat_panel := BattlePanelScene.instantiate() as BattleMapPanel
	root.add_child(high_threat_panel)
	await process_frame
	high_threat_panel.size = VIEWPORT_SIZE
	var high_threat_state := _build_fate_preview_state(&"battle_ui_fate_high_threat", 2, 0, skill_def.skill_id)
	var high_threat_target := high_threat_state.units.get(&"fate_enemy") as BattleUnitState
	high_threat_panel.refresh(
		high_threat_state,
		high_threat_target.coord if high_threat_target != null else Vector2i.ZERO,
		skill_def.skill_id,
		skill_def.display_name
	)
	await process_frame

	var high_threat_badge_texts := _collect_badge_texts(high_threat_panel.fate_badge_row)
	_assert_true(high_threat_badge_texts.has("劣势"), "命运概览应明确显示当前处于劣势。")
	_assert_true(high_threat_badge_texts.has("暴击门 d20"), "命运概览应显示 crit_gate_die 尺寸。")
	_assert_true(high_threat_badge_texts.has("大失败 1"), "命运概览应显示大失败区间。")
	_assert_true(high_threat_badge_texts.has("高位大成功 18-20"), "crit_gate_die==20 时应显示高位大成功区间。")
	_assert_true(
		high_threat_panel.skill_subtitle_label.tooltip_text.contains("高位大成功：18-20"),
		"技能副标题悬浮提示应回显高位大成功区间。"
	)
	var fumble_badge := _find_badge_panel(high_threat_panel.fate_badge_row, "大失败 1")
	var high_threat_badge := _find_badge_panel(high_threat_panel.fate_badge_row, "高位大成功 18-20")
	if fumble_badge != null and high_threat_badge != null:
		var fumble_style := fumble_badge.get_theme_stylebox("panel") as StyleBoxFlat
		var high_threat_style := high_threat_badge.get_theme_stylebox("panel") as StyleBoxFlat
		_assert_true(
			fumble_style != null and high_threat_style != null and fumble_style.bg_color != high_threat_style.bg_color,
			"大失败与高位大成功徽标应使用不同语义色。"
		)

	high_threat_panel.queue_free()
	await process_frame

	var mercy_panel := BattlePanelScene.instantiate() as BattleMapPanel
	root.add_child(mercy_panel)
	await process_frame
	mercy_panel.size = VIEWPORT_SIZE
	var mercy_state := _build_fate_preview_state(&"battle_ui_fate_mercy", -5, 0, skill_def.skill_id)
	var mercy_target := mercy_state.units.get(&"fate_enemy") as BattleUnitState
	mercy_panel.refresh(
		mercy_state,
		mercy_target.coord if mercy_target != null else Vector2i.ZERO,
		skill_def.skill_id,
		skill_def.display_name
	)
	await process_frame

	var mercy_badge_texts := _collect_badge_texts(mercy_panel.fate_badge_row)
	_assert_true(mercy_badge_texts.has("暴击门 d40"), "命运怜悯场景应显示放大后的 crit_gate_die。")
	_assert_true(mercy_badge_texts.has("大失败 1-2"), "命运怜悯场景应显示扩大的大失败区间。")
	_assert_true(mercy_badge_texts.has("命运的怜悯"), "effective_luck<=-5 且处于劣势时应显示命运的怜悯徽标。")
	_assert_true(
		not mercy_badge_texts.has("高位大成功 20-20") and not mercy_panel.skill_subtitle_label.tooltip_text.contains("高位大成功"),
		"crit_gate_die!=20 时不应显示高位大成功区间。"
	)
	_assert_true(
		mercy_panel.skill_subtitle_label.tooltip_text.contains("命运的怜悯：已生效"),
		"技能副标题悬浮提示应说明命运的怜悯已生效。"
	)

	mercy_panel.queue_free()
	await process_frame
	game_session.queue_free()
	await process_frame


func _test_repeat_attack_hud_preview_uses_fate_aware_success_rate() -> void:
	var skill_def := SkillDef.new()
	skill_def.skill_id = &"fate_preview_combo"
	skill_def.display_name = "命契连斩"
	skill_def.combat_profile = CombatSkillDef.new()
	skill_def.combat_profile.skill_id = skill_def.skill_id
	skill_def.combat_profile.attack_roll_bonus = 0
	skill_def.combat_profile.aura_cost = 1
	var repeat_attack_effect := CombatEffectDef.new()
	repeat_attack_effect.effect_type = &"repeat_attack_until_fail"
	repeat_attack_effect.params = {
		"base_attack_bonus": 0,
		"follow_up_attack_penalty": 0,
		"follow_up_cost_multiplier": 2.0,
		"cost_resource": &"aura",
	}
	skill_def.combat_profile.effect_defs = [repeat_attack_effect]

	var game_session := await _install_mock_game_session()
	game_session.skill_defs = {
		skill_def.skill_id: skill_def,
	}

	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, game_session.skill_defs, {}, {}, null)
	var state := _build_repeat_attack_state()
	var attacker := _build_repeat_attack_unit(
		&"fate_hit_preview_user",
		"命契高运者",
		&"player",
		Vector2i(1, 1),
		[skill_def.skill_id],
		2,
		4
	)
	attacker.current_aura = 1
	attacker.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 80)
	attacker.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH, 2)
	var defender := _build_repeat_attack_unit(
		&"fate_hit_preview_target",
		"高闪避木桩",
		&"enemy",
		Vector2i(2, 1),
		[],
		2,
		0
	)
	defender.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 70)
	_add_unit_to_runtime_state(runtime, state, attacker, false)
	_add_unit_to_runtime_state(runtime, state, defender, true)
	state.phase = &"unit_acting"
	state.active_unit_id = attacker.unit_id
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = attacker.unit_id
	command.skill_id = skill_def.skill_id
	command.target_unit_id = defender.unit_id
	command.target_coord = defender.coord
	var preview := runtime.preview_command(command)
	var stage_hit_rates := preview.hit_preview.get("stage_hit_rates", []) as Array
	var stage_base_hit_rates := preview.hit_preview.get("stage_base_hit_rates", []) as Array
	var stage_preview_texts := preview.hit_preview.get("stage_preview_texts", []) as Array
	var hit_preview_text := String(preview.hit_preview.get("summary_text", ""))
	_assert_true(preview != null and not preview.hit_preview.is_empty(), "命中预览应暴露 resolver 结果。")
	_assert_eq(stage_base_hit_rates.size(), stage_hit_rates.size(), "命中预览应保留与最终成功率对齐的 raw 命中率数组。")
	_assert_eq(stage_preview_texts.size(), stage_hit_rates.size(), "命中预览应保留与最终成功率对齐的阶段文案数组。")
	_assert_true(hit_preview_text.begins_with("预计命中率 "), "命中摘要应使用统一 resolver 文案前缀。")
	for stage_preview_text in stage_preview_texts:
		_assert_true(String(stage_preview_text).contains("需 "), "命中预览阶段文案应包含 required roll。")

	var adapter := BattleHudAdapter.new()
	var snapshot := adapter.build_snapshot(
		state,
		defender.coord,
		skill_def.skill_id,
		skill_def.display_name,
		"",
		[],
		1,
		[]
	)
	_assert_eq(
		snapshot.get("selected_skill_hit_stage_rates", []),
		stage_hit_rates,
		"HUD snapshot 应复用 resolver 阶段成功率。"
	)
	_assert_eq(
		String(snapshot.get("selected_skill_hit_preview_text", "")),
		hit_preview_text,
		"HUD 命中摘要应复用共享的 resolver success 文案。"
	)

	game_session.queue_free()
	await process_frame


func _test_force_hit_no_crit_skill_hides_standard_fate_badges() -> void:
	var skill_def := _get_skill_def(BLACK_CONTRACT_PUSH_SKILL_ID)
	_assert_true(skill_def != null and skill_def.combat_profile != null, "强制命中 fate HUD 前置：black_contract_push 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		return

	var game_session := await _install_mock_game_session()
	game_session.skill_defs = {
		skill_def.skill_id: skill_def,
	}

	var panel := BattlePanelScene.instantiate() as BattleMapPanel
	root.add_child(panel)
	await process_frame
	panel.size = VIEWPORT_SIZE
	var state := _build_fate_preview_state(&"battle_ui_force_hit_no_crit", -5, 0, skill_def.skill_id)
	var target := state.units.get(&"fate_enemy") as BattleUnitState
	panel.refresh(
		state,
		target.coord if target != null else Vector2i.ZERO,
		skill_def.skill_id,
		skill_def.display_name,
		"行契",
		[],
		[],
		1,
		[],
		ACTION_TITHE_VARIANT_ID
	)
	await process_frame

	var badge_texts := _collect_badge_texts(panel.fate_badge_row)
	_assert_true(badge_texts.has("必定命中"), "force_hit_no_crit 技能应显示必定命中徽标。")
	_assert_true(badge_texts.has("禁暴击"), "force_hit_no_crit 技能应显示禁暴击徽标。")
	_assert_true(badge_texts.has("摆幅压低"), "force_hit_no_crit 技能应显示命运摆幅压低提示。")
	_assert_true(not _badge_texts_contain_prefix(badge_texts, "暴击门"), "force_hit_no_crit 技能不应继续显示标准暴击门徽标。")
	_assert_true(not _badge_texts_contain_prefix(badge_texts, "大失败"), "force_hit_no_crit 技能不应继续显示标准大失败徽标。")
	_assert_true(not _badge_texts_contain_prefix(badge_texts, "高位大成功"), "force_hit_no_crit 技能不应继续显示高位大成功徽标。")
	_assert_true(
		panel.skill_subtitle_label.tooltip_text.contains("强制命中") and panel.skill_subtitle_label.tooltip_text.contains("不再走标准命中/暴击/大失败骰"),
		"force_hit_no_crit 技能的技能副标题提示应说明它已切到特殊 fate 口径。"
	)

	panel.queue_free()
	game_session.queue_free()
	await process_frame


func _test_hybrid_multi_unit_skill_uses_shared_fate_policy() -> void:
	var skill_def := _get_skill_def(ARCHER_MULTISHOT_SKILL_ID)
	_assert_true(skill_def != null and skill_def.combat_profile != null, "混合多目标 fate HUD 前置：archer_multishot 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		return

	var game_session := await _install_mock_game_session()
	game_session.skill_defs = {
		skill_def.skill_id: skill_def,
	}

	var adapter := BattleHudAdapter.new()
	var state := _build_hybrid_multi_unit_fate_preview_state()
	var enemy := state.units.get(&"hybrid_enemy_a") as BattleUnitState
	var target_coords: Array[Vector2i] = []
	var target_unit_ids: Array[StringName] = []
	if enemy != null:
		target_coords.append(enemy.coord)
		target_unit_ids.append(enemy.unit_id)
	var snapshot := adapter.build_snapshot(
		state,
		enemy.coord if enemy != null else Vector2i.ZERO,
		skill_def.skill_id,
		skill_def.display_name,
		"连珠箭",
		target_coords,
		3,
		target_unit_ids,
		ARCHER_MULTISHOT_VARIANT_ID
	)
	var fate_badges := snapshot.get("selected_skill_fate_badges", []) as Array
	_assert_true(not fate_badges.is_empty(), "ground 变体多目标点射在 HUD 上也应复用共享 fate policy。")
	_assert_true(
		String(snapshot.get("selected_skill_fate_preview_text", "")).contains("暴击门"),
		"ground 变体多目标点射的 HUD 命运概览应显示标准 fate 摘要。"
	)

	game_session.queue_free()
	await process_frame


func _test_battle_state_log_buffer_enforces_entry_cap() -> void:
	var state := BattleState.new()
	for index in range(BattleState.LOG_ENTRY_LIMIT + 25):
		state.append_log_entry("log_%d" % index)
	_assert_eq(state.log_entries.size(), BattleState.LOG_ENTRY_LIMIT, "BattleState 日志缓冲应按条数上限裁剪。")
	_assert_eq(String(state.log_entries[0]), "log_25", "BattleState 日志缓冲达到上限后应淘汰最旧条目。")
	_assert_true(
		state.get_log_text_byte_size() <= BattleState.LOG_TEXT_BYTE_LIMIT,
		"BattleState 日志缓冲裁剪后应保持在字节预算内。"
	)


func _test_runtime_log_dock_syncs_battle_entries() -> void:
	root.size = Vector2i(VIEWPORT_SIZE)
	var log_dock := RuntimeLogDockScene.instantiate() as RuntimeLogDock
	root.add_child(log_dock)
	await process_frame
	log_dock.size = VIEWPORT_SIZE
	var state := _build_state()
	state.reset_log_entries([
		"战斗开始：日志窗口回归",
		"轮到 我方 行动。",
		"我方 结束行动。",
	])
	log_dock.show_battle_logs(state)
	await process_frame

	_assert_true(
		log_dock != null and log_dock.log_output.get_parsed_text().contains("战斗开始：日志窗口回归"),
		"右侧日志窗口应显示 battle start 之后的首条日志。"
	)
	_assert_true(
		log_dock != null and log_dock.log_output.get_parsed_text().contains("我方 结束行动。"),
		"右侧日志窗口应显示完整战斗日志，而不只保留底部摘要。"
	)
	_assert_true(
		log_dock != null and log_dock.meta_label.text.contains("3 条"),
		"右侧日志窗口元信息应显示当前日志条数。"
	)
	_assert_eq(log_dock.title_label.text, "战斗日志", "battle feed 应复用统一日志窗口标题。")

	state.append_log_entry("敌方 准备反击。")
	log_dock.show_battle_logs(state)
	await process_frame
	_assert_true(
		log_dock != null and log_dock.log_output.get_parsed_text().contains("敌方 准备反击。"),
		"overlay 刷新时右侧日志窗口也应增量追加新日志。"
	)
	_assert_true(
		log_dock != null and log_dock.meta_label.text.contains("4 条"),
		"增量追加日志后，右侧日志窗口元信息应同步刷新。"
	)

	log_dock.queue_free()
	await process_frame


func _test_battle_panel_flushes_to_ultrawide_edges() -> void:
	root.size = ULTRAWIDE_PANEL_SIZE
	var panel := BattlePanelScene.instantiate() as BattleMapPanel
	root.add_child(panel)
	await process_frame
	panel.size = Vector2(ULTRAWIDE_PANEL_SIZE)
	panel.show_battle(_build_state(), Vector2i(0, 0))
	await process_frame

	var map_frame_rect := panel.map_frame.get_global_rect()
	var top_bar_rect := panel.top_bar.get_global_rect()
	var bottom_panel_rect := panel.bottom_panel.get_global_rect()
	_assert_eq(map_frame_rect.position.x, 0.0, "BattleMapPanel 的 MapFrame 左边界应贴齐父窗口。")
	_assert_eq(map_frame_rect.size.x, float(ULTRAWIDE_PANEL_SIZE.x), "BattleMapPanel 的 MapFrame 宽度应与父窗口一致。")
	_assert_eq(top_bar_rect.position.x, 0.0, "BattleMapPanel 的 TopBar 左边界应贴齐父窗口。")
	_assert_eq(top_bar_rect.size.x, float(ULTRAWIDE_PANEL_SIZE.x), "BattleMapPanel 的 TopBar 宽度应与父窗口一致。")
	_assert_eq(bottom_panel_rect.position.x, 0.0, "BattleMapPanel 的 BottomPanel 左边界应贴齐父窗口。")
	_assert_eq(bottom_panel_rect.size.x, float(ULTRAWIDE_PANEL_SIZE.x), "BattleMapPanel 的 BottomPanel 宽度应与父窗口一致。")

	panel.queue_free()
	await process_frame
	root.size = Vector2i(VIEWPORT_SIZE)


func _test_battle_panel_loading_overlay_waits_for_first_presented_frame() -> void:
	root.size = Vector2i(VIEWPORT_SIZE)
	var panel := BattlePanelScene.instantiate() as BattleMapPanel
	root.add_child(panel)
	await process_frame
	panel.size = VIEWPORT_SIZE
	var state := _build_state()
	state.battle_id = &"battle_ui_loading_overlay"
	panel.show_battle(state, Vector2i(0, 0))
	_assert_true(panel.is_loading_battle(), "loading 遮罩展示期间应保持 battle 输入锁定。")
	_assert_true(panel.get_loading_progress() > 0.0, "新 battle 进入时应推进 loading 进度。")

	await process_frame
	_assert_true(panel.is_battle_render_content_ready(), "loading 期间 battle 棋盘内容应先达到完整渲染态。")

	await process_frame
	_assert_true(panel.is_loading_battle(), "最短 loading 时长内仍应维持 loading 状态。")

	await _wait_seconds(0.5)
	await process_frame
	_assert_true(panel.visible, "首帧渲染完成后才应显示 battle 面板。")
	_assert_true(not panel.is_loading_battle(), "首帧渲染完成后应解除 battle 输入锁定。")

	panel.show_battle(state, Vector2i(0, 0))
	_assert_true(panel.visible, "同一 battle 的后续全量刷新不应重新隐藏 battle 面板。")

	panel.queue_free()
	await process_frame


func _install_mock_game_session() -> MockGameSession:
	for child in root.get_children():
		if child.name == "GameSession":
			child.queue_free()
	await process_frame
	var game_session := MockGameSession.new()
	game_session.name = "GameSession"
	root.add_child(game_session)
	await process_frame
	return game_session


func _build_multi_unit_skill_def() -> SkillDef:
	var skill_def := SkillDef.new()
	skill_def.skill_id = &"archer_multishot"
	skill_def.display_name = "连珠箭"
	skill_def.combat_profile = CombatSkillDef.new()
	skill_def.combat_profile.skill_id = skill_def.skill_id
	skill_def.combat_profile.target_selection_mode = &"multi_unit"
	skill_def.combat_profile.min_target_count = 2
	skill_def.combat_profile.max_target_count = 3
	return skill_def


func _build_state() -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"battle_ui_regression"
	state.map_size = Vector2i(4, 4)
	state.terrain_profile_id = &"default"
	state.cells = {}
	for y in range(4):
		for x in range(4):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.ally_unit_ids = []
	state.enemy_unit_ids = []
	var unit := BattleUnitState.new()
	unit.unit_id = &"ally_ui"
	unit.display_name = "我方"
	unit.faction_id = &"player"
	unit.is_alive = true
	unit.current_hp = 10
	unit.current_mp = 0
	unit.current_ap = 4
	unit.known_active_skill_ids = [&"archer_multishot"]
	unit.refresh_footprint()
	state.units = {
		unit.unit_id: unit,
	}
	state.ally_unit_ids.append(unit.unit_id)
	state.active_unit_id = unit.unit_id
	return state


func _build_repeat_attack_state() -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"battle_ui_repeat_attack"
	state.map_size = Vector2i(4, 3)
	state.terrain_profile_id = &"default"
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(3):
		for x in range(4):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _build_fate_preview_state(
	battle_id: StringName,
	hidden_luck_at_birth: int,
	faith_luck_bonus: int,
	skill_id: StringName = WARRIOR_HEAVY_STRIKE_SKILL_ID
) -> BattleState:
	var state := BattleState.new()
	state.battle_id = battle_id
	state.map_size = Vector2i(4, 4)
	state.terrain_profile_id = &"default"
	state.cells = {}
	for y in range(4):
		for x in range(4):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.ally_unit_ids = []
	state.enemy_unit_ids = []

	var caster := _build_repeat_attack_unit(
		&"fate_caster",
		"命契战士",
		&"player",
		Vector2i(1, 1),
		[skill_id],
		2,
		0
	)
	caster.current_hp = 10
	caster.current_stamina = 30
	caster.attribute_snapshot.set_value(&"hp_max", 40)
	caster.attribute_snapshot.set_value(&"stamina_max", 30)
	caster.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH, hidden_luck_at_birth)
	caster.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.FAITH_LUCK_BONUS, faith_luck_bonus)

	var enemy := _build_repeat_attack_unit(
		&"fate_enemy",
		"高闪避敌人",
		&"enemy",
		Vector2i(2, 1),
		[],
		2,
		0
	)
	enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 999)

	state.units = {
		caster.unit_id: caster,
		enemy.unit_id: enemy,
	}
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = caster.unit_id
	return state


func _build_hybrid_multi_unit_fate_preview_state() -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"battle_ui_hybrid_multi_unit_fate"
	state.map_size = Vector2i(5, 3)
	state.terrain_profile_id = &"default"
	state.cells = {}
	for y in range(3):
		for x in range(5):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.ally_unit_ids = []
	state.enemy_unit_ids = []

	var archer := _build_repeat_attack_unit(
		&"hybrid_archer",
		"混合弓手",
		&"player",
		Vector2i(0, 1),
		[ARCHER_MULTISHOT_SKILL_ID],
		3,
		0
	)
	archer.current_stamina = 20
	archer.attribute_snapshot.set_value(&"stamina_max", 20)
	archer.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.WEAPON_ATTACK_RANGE, 4)
	archer.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH, 2)
	var enemy_a := _build_repeat_attack_unit(
		&"hybrid_enemy_a",
		"前排敌人",
		&"enemy",
		Vector2i(2, 1),
		[],
		2,
		0
	)
	enemy_a.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var enemy_b := _build_repeat_attack_unit(
		&"hybrid_enemy_b",
		"后排敌人",
		&"enemy",
		Vector2i(3, 1),
		[],
		2,
		0
	)
	enemy_b.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)

	state.units = {
		archer.unit_id: archer,
		enemy_a.unit_id: enemy_a,
		enemy_b.unit_id: enemy_b,
	}
	state.ally_unit_ids = [archer.unit_id]
	state.enemy_unit_ids = [enemy_a.unit_id, enemy_b.unit_id]
	state.active_unit_id = archer.unit_id
	return state


func _build_cell(coord: Vector2i) -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = coord
	cell.stack_layer = 0
	cell.base_height = 0
	cell.base_terrain = BattleCellState.TERRAIN_LAND
	cell.recalculate_runtime_values()
	return cell


func _build_repeat_attack_unit(
	unit_id: StringName,
	display_name: String,
	faction_id: StringName,
	coord: Vector2i,
	skill_ids: Array[StringName],
	current_ap: int,
	current_mp: int
) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"manual"
	unit.current_hp = 40
	unit.current_mp = current_mp
	unit.current_ap = current_ap
	unit.current_stamina = 30
	unit.current_aura = 0
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 40)
	unit.attribute_snapshot.set_value(&"mp_max", maxi(current_mp, 4))
	unit.attribute_snapshot.set_value(&"stamina_max", 30)
	unit.attribute_snapshot.set_value(&"aura_max", 8)
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 1))
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 6)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 80)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 5)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.WEAPON_ATTACK_RANGE, 1)
	unit.weapon_physical_damage_tag = &"physical_slash"
	unit.known_active_skill_ids = skill_ids.duplicate()
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 1
	return unit


func _add_unit_to_runtime_state(runtime: BattleRuntimeModule, state: BattleState, unit: BattleUnitState, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	_assert_true(bool(runtime._grid_service.place_unit(state, unit, unit.coord, true)), "UI 命中预览回归中的测试单位应成功放入战场。")


func _get_repeat_attack_skill_def() -> SkillDef:
	var registry := ProgressionContentRegistry.new()
	return registry.get_skill_defs().get(&"saint_blade_combo") as SkillDef


func _get_skill_def(skill_id: StringName) -> SkillDef:
	var registry := ProgressionContentRegistry.new()
	return registry.get_skill_defs().get(skill_id) as SkillDef


func _instantiate_board() -> BattleBoard2D:
	var board := BattleBoardScene.instantiate() as BattleBoard2D
	root.add_child(board)
	await process_frame
	board.set_viewport_size(VIEWPORT_SIZE)
	return board


func _collect_node_names(node: Node) -> Array[String]:
	var names: Array[String] = []
	if node == null:
		return names
	for child in node.get_children():
		names.append(child.name)
	return names


func _collect_badge_texts(container: Node) -> Array[String]:
	var texts: Array[String] = []
	if container == null:
		return texts
	for child in container.get_children():
		if child is not PanelContainer:
			continue
		var label := _find_first_label(child)
		if label != null:
			texts.append(label.text)
	return texts


func _badge_texts_contain_prefix(texts: Array[String], prefix: String) -> bool:
	for text in texts:
		if text.begins_with(prefix):
			return true
	return false


func _find_badge_panel(container: Node, badge_text: String) -> PanelContainer:
	if container == null:
		return null
	for child in container.get_children():
		var panel := child as PanelContainer
		if panel == null:
			continue
		var label := _find_first_label(panel)
		if label != null and label.text == badge_text:
			return panel
	return null


func _find_first_label(node: Node) -> Label:
	if node == null:
		return null
	for child in node.get_children():
		var label := child as Label
		if label != null:
			return label
		var nested := _find_first_label(child)
		if nested != null:
			return nested
	return null


func _get_layer_cell_image(layer: TileMapLayer, coord: Vector2i) -> Image:
	if layer == null or layer.tile_set == null:
		return null
	var source_id := layer.get_cell_source_id(coord)
	if source_id < 0:
		return null
	var atlas_source := layer.tile_set.get_source(source_id) as TileSetAtlasSource
	if atlas_source == null or atlas_source.texture == null:
		return null
	return atlas_source.texture.get_image()


func _wait_seconds(duration_seconds: float) -> void:
	var target_time_msec := Time.get_ticks_msec() + int(round(duration_seconds * 1000.0))
	while Time.get_ticks_msec() < target_time_msec:
		await process_frame


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
