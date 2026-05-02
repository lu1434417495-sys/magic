extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const BATTLE_BOARD_SCENE = preload("res://scenes/ui/battle_board_2d.tscn")
const BattleBoard2D = preload("res://scripts/ui/battle_board_2d.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_EVENT_BATCH_SCRIPT = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BATTLE_DAMAGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/skill_def.gd")
const UNIT_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_skill_progress.gd")
const COMBAT_SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/combat_skill_def.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


class AlwaysMissDamageResolver extends BattleDamageResolver:
	func _roll_true_random_attack_range(min_value: int, max_value: int, battle_state) -> int:
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		return clampi(1, mini(min_value, max_value), maxi(min_value, max_value))


class RecordingCharacterGateway:
	extends RefCounted

	var achievement_event_count := 0
	var battle_mastery_count := 0
	var source_mastery_count := 0

	func record_achievement_event(_member_id: StringName, _event_type: StringName, _value: int = 1, _detail_id: StringName = &""):
		achievement_event_count += 1
		return null

	func grant_battle_mastery(_member_id: StringName, _skill_id: StringName, _amount: int):
		battle_mastery_count += 1
		return null

	func grant_skill_mastery_from_source(
		member_id: StringName,
		skill_id: StringName,
		amount: int,
		source_type: StringName,
		source_label: String = "",
		reason_text: String = "",
		_emit_achievement_event: bool = true
	):
		source_mastery_count += 1
		var delta = CharacterProgressionDelta.new()
		delta.member_id = member_id
		delta.mastery_changes.append({
			"skill_id": skill_id,
			"mastery_amount": amount,
			"source_type": source_type,
			"source_label": source_label,
			"reason_text": reason_text,
		})
		return delta

	func get_member_state(_member_id: StringName):
		return null


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_battle_unit_state_serialization_exposes_aura()
	_test_battle_unit_state_from_dict_rejects_missing_resource_schema()
	_test_battle_unit_state_serialization_exposes_shield()
	_test_battle_unit_state_serialization_exposes_weapon_projection()
	_test_damage_resolver_reports_hp_damage_after_shield_absorption()
	_test_environmental_damage_helpers_report_shield_absorption()
	_test_damage_resolver_uses_mitigation_tier_for_damage_type_defense()
	_test_damage_resolver_reports_mitigation_sources()
	_test_death_ward_without_last_stand_does_not_block_fatal_physical_damage()
	_test_last_stand_triggers_heal_and_consumes_death_ward_on_fatal_damage()
	_test_damage_resolver_trigger_event_filters_conditional_effects()
	_test_vajra_body_reduces_all_damage_tags_and_blocks_enemy_forced_move()
	_test_forced_move_requires_formal_fields()
	_test_damage_resolver_guarding_only_reduces_physical_damage()
	_test_damage_resolver_damage_reduction_up_uses_fixed_value()
	_test_content_skill_magic_shield_halves_generic_magic_damage()
	_test_content_skills_prismatic_barrier_and_spellward_map_half_and_immune()
	_test_content_skill_hex_of_frailty_applies_double_and_cancels_with_half()
	_test_shield_dice_roll_is_random_and_shared_per_cast()
	_test_facade_shield_skill_writes_shield_and_does_not_decay_on_tu_tick()
	_test_preview_reports_shield_absorption_and_break()
	_test_runtime_logs_zero_hp_damage_when_shield_absorbs_everything()
	_test_guard_incoming_physical_hit_mastery_writes_batch_after_damage_resolution()
	_test_guard_mastery_requires_physical_hp_damage()
	_test_ground_weapon_attack_all_miss_is_not_cast_success()
	_test_runtime_preview_and_logs_include_mitigation_sources()
	_test_facade_clicking_active_unit_casts_self_skill()
	_test_facade_multi_unit_selection_tracks_target_unit_ids()
	_test_facade_ground_aoe_selection_highlight_preview_and_execution_share_range()
	_test_facade_stamina_skill_updates_battle_state_snapshot_and_logs()
	_test_facade_aura_skill_updates_battle_state_snapshot_and_logs()
	_test_facade_selected_aura_skill_returns_formal_error_after_aura_drops()
	_test_facade_direct_skill_issue_keeps_queued_targets_after_runtime_rejection()
	_test_facade_cooldown_skill_reduces_after_battle_tick()
	_test_facade_auto_battle_advance_marks_overlay_refresh_for_tu_only_updates()
	_test_stamina_recovers_on_5tu_ticks_and_rest_doubles_progress()
	if _failures.is_empty():
		print("Battle skill protocol regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle skill protocol regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_facade_clicking_active_unit_casts_self_skill() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"self_cast_user",
		"自施法者",
		&"player",
		Vector2i(1, 0),
		[&"mage_arcane_orbit"],
		2,
		6
	)
	var enemy: BattleUnitState = _build_manual_unit(
		&"self_cast_enemy",
		"敌人",
		&"enemy",
		Vector2i(3, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var before_hp: int = int(caster.current_hp)
	var before_mp: int = int(caster.current_mp)
	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择自施法技能应返回成功结果。")
	var cast_result: Dictionary = facade.command_battle_move_to(caster.coord)
	_assert_true(bool(cast_result.get("ok", false)), "点击自身坐标施法应返回成功结果。")
	var battle_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})

	_assert_eq(caster.current_mp, before_mp - 2, "点击自身后应真正施放自施法技能并扣除法力。")
	_assert_true(caster.current_hp < before_hp, "当前自施法回归夹具应能观测到技能已真实结算。")
	_assert_eq(
		_extract_coord_pairs(battle_snapshot.get("selected_target_coords", [])),
		[],
		"自施法结算后不应残留已选目标坐标。"
	)

	_cleanup_test_session(game_session)


func _test_facade_multi_unit_selection_tracks_target_unit_ids() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_def = game_session.get_skill_defs().get(&"mage_arcane_missile")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "多目标协议回归前置：mage_arcane_missile 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		_cleanup_test_session(game_session)
		return
	skill_def.combat_profile.min_target_count = 2
	skill_def.combat_profile.max_target_count = 2

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(5, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"multi_unit_user",
		"多目标施法者",
		&"player",
		Vector2i(0, 0),
		[&"mage_arcane_missile"],
		2,
		6
	)
	var enemy_a: BattleUnitState = _build_manual_unit(&"enemy_a", "敌人A", &"enemy", Vector2i(2, 0), [], 2, 0)
	var enemy_b: BattleUnitState = _build_manual_unit(&"enemy_b", "敌人B", &"enemy", Vector2i(3, 0), [], 2, 0)
	var enemy_c: BattleUnitState = _build_manual_unit(&"enemy_c", "敌人C", &"enemy", Vector2i(4, 0), [], 2, 0)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy_a, true)
	_add_unit_to_state(facade, state, enemy_b, true)
	_add_unit_to_state(facade, state, enemy_c, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择多目标技能应返回成功结果。")
	facade.command_battle_move_to(enemy_b.coord)
	var queued_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	_assert_eq(
		_extract_string_array(queued_snapshot.get("selected_target_unit_ids", [])),
		["enemy_b"],
		"首个单位目标应按点击顺序写入 battle snapshot。"
	)
	_assert_eq(
		_extract_coord_pairs(queued_snapshot.get("selected_target_coords", [])),
		[[enemy_b.coord.x, enemy_b.coord.y]],
		"单位多选阶段应把已选单位坐标同步暴露给界面快照。"
	)

	facade.command_battle_move_to(enemy_a.coord)
	var after_cast_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	var move_log := _find_last_log_entry(facade.get_log_snapshot(), "battle.move_to")
	_assert_eq(
		_extract_unit_ids_from_entries(move_log.get("context", {}).get("battle_changed_units", [])),
		["multi_unit_user", "enemy_b", "enemy_a"],
		"多目标技能结算应按选择顺序依次解析目标，即使天然 1 导致未造成伤害。"
	)
	_assert_eq(enemy_c.current_hp, 30, "未被选中的单位不应受到多目标技能影响。")
	_assert_eq(
		_extract_string_array(after_cast_snapshot.get("selected_target_unit_ids", [])),
		[],
		"多目标技能结算后，battle snapshot 不应残留已选单位目标。"
	)

	_cleanup_test_session(game_session)


func _test_facade_ground_aoe_selection_highlight_preview_and_execution_share_range() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(5, 5))
	var caster: BattleUnitState = _build_manual_unit(
		&"radius_skill_user",
		"范围施法者",
		&"player",
		Vector2i(2, 2),
		[&"mage_cold_snap"],
		2,
		6
	)
	var enemy_top: BattleUnitState = _build_manual_unit(&"radius_enemy_top", "敌人上", &"enemy", Vector2i(2, 0), [], 2, 0)
	var enemy_left: BattleUnitState = _build_manual_unit(&"radius_enemy_left", "敌人左", &"enemy", Vector2i(1, 1), [], 2, 0)
	var enemy_center: BattleUnitState = _build_manual_unit(&"radius_enemy_center", "敌人中", &"enemy", Vector2i(2, 1), [], 2, 0)
	var enemy_right: BattleUnitState = _build_manual_unit(&"radius_enemy_right", "敌人右", &"enemy", Vector2i(3, 1), [], 2, 0)
	var enemy_far: BattleUnitState = _build_manual_unit(&"radius_enemy_far", "敌人远", &"enemy", Vector2i(4, 4), [], 2, 0)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy_top, true)
	_add_unit_to_state(facade, state, enemy_left, true)
	_add_unit_to_state(facade, state, enemy_center, true)
	_add_unit_to_state(facade, state, enemy_right, true)
	_add_unit_to_state(facade, state, enemy_far, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择 radius 范围技能应返回成功结果。")
	var target_coord := Vector2i(2, 1)
	facade.set_runtime_battle_selected_coord(target_coord)
	facade.set_battle_selection_target_coords_state([target_coord])

	var selected_target_coords := facade.get_selected_battle_skill_target_coords()
	_assert_true(
		selected_target_coords.size() > 1 and selected_target_coords.has(target_coord),
		"radius 范围技能的 selection 读面应暴露包含目标中心在内的正式多格范围。"
	)

	var battle_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	_assert_eq(
		_extract_coord_pairs(battle_snapshot.get("selected_target_coords", [])),
		_extract_vector2i_pairs(selected_target_coords),
		"battle snapshot 应把同一范围结果原样暴露给 HUD/棋盘高亮。"
	)

	var preview_command = BATTLE_COMMAND_SCRIPT.new()
	preview_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	preview_command.unit_id = caster.unit_id
	preview_command.skill_id = &"mage_cold_snap"
	preview_command.target_coord = target_coord
	var preview = facade.preview_battle_command(preview_command)
	_assert_true(preview != null and preview.allowed, "radius 范围技能前置：preview_command 应允许测试目标。")
	if preview != null:
		_assert_eq(
			_extract_vector2i_pairs(preview.target_coords),
			_extract_vector2i_pairs(selected_target_coords),
			"合法性校验 / preview 应复用与 selection 相同的范围结果。"
		)

	var board := await _instantiate_battle_board()
	board.configure(
		state,
		target_coord,
		selected_target_coords,
		facade.get_battle_overlay_target_coords()
	)
	await process_frame
	_assert_eq(
		_extract_vector2i_pairs(_collect_marker_used_coords(board)),
		_extract_vector2i_pairs(selected_target_coords),
		"棋盘高亮应使用与 selection 相同的范围结果。"
	)
	board.queue_free()
	await process_frame

	var caster_hp_before := caster.current_hp
	var enemy_top_hp_before := enemy_top.current_hp
	var enemy_left_hp_before := enemy_left.current_hp
	var enemy_center_hp_before := enemy_center.current_hp
	var enemy_right_hp_before := enemy_right.current_hp
	var enemy_far_hp_before := enemy_far.current_hp
	var execute_refresh := String(facade.issue_battle_command(preview_command))
	_assert_eq(execute_refresh, "full", "执行范围技能命令后应触发完整战斗刷新。")
	_assert_true(enemy_top.current_hp < enemy_top_hp_before, "范围内顶部敌人应受到实际结算影响。")
	_assert_true(enemy_left.current_hp < enemy_left_hp_before, "范围内左侧敌人应受到实际结算影响。")
	_assert_true(enemy_center.current_hp < enemy_center_hp_before, "范围内中心敌人应受到实际结算影响。")
	_assert_true(enemy_right.current_hp < enemy_right_hp_before, "范围内右侧敌人应受到实际结算影响。")
	_assert_eq(enemy_far.current_hp, enemy_far_hp_before, "范围外敌人不应被误伤。")
	_assert_eq(caster.current_hp, caster_hp_before, "敌对范围技能不应误伤施法者自身。")
	_assert_eq(
		_extract_coord_pairs(facade.build_headless_snapshot().get("battle", {}).get("selected_target_coords", [])),
		[],
		"范围技能结算后不应残留已选范围坐标。"
	)

	_cleanup_test_session(game_session)


func _test_battle_unit_state_serialization_exposes_aura() -> void:
	var unit := _build_manual_unit(&"aura_state_user", "Aura State User", &"player", Vector2i.ZERO, [], 2, 6)
	unit.current_aura = 3
	unit.attribute_snapshot.set_value(&"aura_max", 5)

	var payload := unit.to_dict()
	var restored = BATTLE_UNIT_STATE_SCRIPT.from_dict(payload) as BattleUnitState

	_assert_eq(int(payload.get("current_aura", -1)), 3, "BattleUnitState.to_dict() 应稳定暴露 current_aura。")
	_assert_eq(int(payload.get("aura_max", -1)), 5, "BattleUnitState.to_dict() 应稳定暴露 aura_max。")
	_assert_true(restored != null, "BattleUnitState.from_dict() 应能恢复 Aura 字段。")
	_assert_eq(restored.current_aura if restored != null else -1, 3, "BattleUnitState.from_dict() 应恢复 current_aura。")
	_assert_eq(restored.get_aura_max() if restored != null else -1, 5, "BattleUnitState.from_dict() 应恢复 aura_max。")


func _test_battle_unit_state_from_dict_rejects_missing_resource_schema() -> void:
	var unit := _build_manual_unit(&"missing_resource_schema_user", "Missing Resource Schema User", &"player", Vector2i.ZERO, [], 2, 6)
	var payload := unit.to_dict()
	payload.erase("unlocked_combat_resource_ids")

	_assert_true(
		BATTLE_UNIT_STATE_SCRIPT.from_dict(payload) == null,
		"缺少 unlocked_combat_resource_ids 的 BattleUnitState shape 应直接拒绝。"
	)


func _test_battle_unit_state_serialization_exposes_shield() -> void:
	var unit := _build_manual_unit(&"shield_state_user", "Shield State User", &"player", Vector2i.ZERO, [], 2, 6)
	unit.current_shield_hp = 6
	unit.shield_max_hp = 9
	unit.shield_duration = 60
	unit.shield_family = &"holy_barrier"
	unit.shield_source_unit_id = &"priest_guardian"
	unit.shield_source_skill_id = &"priest_guardian_barrier"
	unit.shield_params = {"fx": "holy"}

	var payload := unit.to_dict()
	var restored = BATTLE_UNIT_STATE_SCRIPT.from_dict(payload) as BattleUnitState

	_assert_eq(int(payload.get("current_shield_hp", -1)), 6, "BattleUnitState.to_dict() 应稳定暴露 current_shield_hp。")
	_assert_eq(int(payload.get("shield_max_hp", -1)), 9, "BattleUnitState.to_dict() 应稳定暴露 shield_max_hp。")
	_assert_eq(int(payload.get("shield_duration", -2)), 60, "BattleUnitState.to_dict() 应稳定暴露 shield_duration。")
	_assert_eq(String(payload.get("shield_family", "")), "holy_barrier", "BattleUnitState.to_dict() 应稳定暴露 shield_family。")
	_assert_true(restored != null, "BattleUnitState.from_dict() 应能恢复护盾字段。")
	_assert_eq(restored.current_shield_hp if restored != null else -1, 6, "BattleUnitState.from_dict() 应恢复 current_shield_hp。")
	_assert_eq(restored.shield_max_hp if restored != null else -1, 9, "BattleUnitState.from_dict() 应恢复 shield_max_hp。")
	_assert_eq(restored.shield_duration if restored != null else -2, 60, "BattleUnitState.from_dict() 应恢复 shield_duration。")
	_assert_eq(String(restored.shield_family if restored != null else &""), "holy_barrier", "BattleUnitState.from_dict() 应恢复 shield_family。")
	_assert_eq(String(restored.shield_source_skill_id if restored != null else &""), "priest_guardian_barrier", "BattleUnitState.from_dict() 应恢复 shield_source_skill_id。")


func _test_battle_unit_state_serialization_exposes_weapon_projection() -> void:
	var no_weapon := _build_manual_unit(&"no_weapon_state_user", "No Weapon State User", &"player", Vector2i.ZERO, [], 2, 6)
	no_weapon.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.WEAPON_ATTACK_RANGE, 9)
	var no_weapon_payload := no_weapon.to_dict()
	var restored_no_weapon = BATTLE_UNIT_STATE_SCRIPT.from_dict(no_weapon_payload) as BattleUnitState
	_assert_eq(String(no_weapon_payload.get("weapon_profile_kind", "")), "none", "默认 BattleUnitState 应显式表达无武器。")
	_assert_eq(int(no_weapon_payload.get("weapon_attack_range", -1)), 0, "无武器不应从 attribute_snapshot.weapon_attack_range 回填攻击范围。")
	_assert_eq(restored_no_weapon.weapon_attack_range if restored_no_weapon != null else -1, 0, "无武器 round-trip 后攻击范围仍应为 0。")

	var unarmed := _build_manual_unit(&"unarmed_state_user", "Unarmed State User", &"player", Vector2i.ZERO, [], 2, 6)
	unarmed.set_unarmed_weapon_projection()
	var unarmed_payload := unarmed.to_dict()
	var unarmed_dice: Dictionary = unarmed_payload.get("weapon_one_handed_dice", {})
	_assert_eq(String(unarmed_payload.get("weapon_profile_kind", "")), "unarmed", "空手投影应能通过 kind 表达。")
	_assert_eq(String(unarmed_payload.get("weapon_profile_type_id", "")), "unarmed", "空手投影应保留 type id。")
	_assert_eq(String(unarmed_payload.get("weapon_family", "")), "unarmed", "空手投影应保留 weapon family。")
	_assert_eq(int(unarmed_dice.get("dice_sides", -1)), 4, "空手投影应提供 1D4 伤害骰。")
	_assert_true(not bool(unarmed_payload.get("weapon_uses_two_hands", true)), "空手投影不应标记双手握法。")

	var natural := _build_manual_unit(&"natural_weapon_state_user", "Natural Weapon State User", &"player", Vector2i.ZERO, [], 2, 6)
	natural.set_natural_weapon_projection(
		&"wolf_bite",
		&"physical_pierce",
		1,
		{"dice_count": 1, "dice_sides": 4, "flat_bonus": 0}
	)
	var natural_payload := natural.to_dict()
	_assert_eq(String(natural_payload.get("weapon_profile_kind", "")), "natural", "天生武器投影应能通过 kind 表达。")
	_assert_eq(String(natural_payload.get("weapon_profile_type_id", "")), "wolf_bite", "天生武器应保留 profile type id。")

	var equipped := _build_manual_unit(&"equipped_weapon_state_user", "Equipped Weapon State User", &"player", Vector2i.ZERO, [], 2, 6)
	equipped.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "training_longsword",
		"weapon_profile_type_id": "longsword",
		"weapon_family": "sword",
		"weapon_current_grip": "two_handed",
		"weapon_attack_range": 2,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 8, "flat_bonus": 0},
		"weapon_two_handed_dice": {"dice_count": 1, "dice_sides": 10, "flat_bonus": 0},
		"weapon_is_versatile": true,
		"weapon_uses_two_hands": true,
		"weapon_physical_damage_tag": "physical_slash",
	})
	var payload := equipped.to_dict()
	var restored = BATTLE_UNIT_STATE_SCRIPT.from_dict(payload) as BattleUnitState
	var one_handed_dice: Dictionary = payload.get("weapon_one_handed_dice", {})
	var two_handed_dice: Dictionary = payload.get("weapon_two_handed_dice", {})
	_assert_eq(String(payload.get("weapon_profile_kind", "")), "equipped", "装备武器投影应进入序列化 payload。")
	_assert_eq(String(payload.get("weapon_item_id", "")), "training_longsword", "装备武器 item id 应进入序列化 payload。")
	_assert_eq(String(payload.get("weapon_profile_type_id", "")), "longsword", "weapon profile type id 应进入序列化 payload。")
	_assert_eq(String(payload.get("weapon_family", "")), "sword", "weapon family 应进入序列化 payload。")
	_assert_eq(String(payload.get("weapon_current_grip", "")), "two_handed", "当前握法应进入序列化 payload。")
	_assert_eq(int(payload.get("weapon_attack_range", -1)), 2, "weapon_attack_range 应进入序列化 payload。")
	_assert_eq(int(one_handed_dice.get("dice_sides", -1)), 8, "一手骰应进入序列化 payload。")
	_assert_eq(int(two_handed_dice.get("dice_sides", -1)), 10, "双手骰应进入序列化 payload。")
	_assert_true(bool(payload.get("weapon_is_versatile", false)), "versatile 标记应进入序列化 payload。")
	_assert_true(bool(payload.get("weapon_uses_two_hands", false)), "当前双手握法应进入序列化 payload。")
	_assert_true(restored != null, "BattleUnitState.from_dict() 应能恢复武器投影字段。")
	_assert_eq(String(restored.weapon_profile_kind if restored != null else &""), "equipped", "round-trip 后应恢复武器投影 kind。")
	_assert_eq(String(restored.weapon_profile_type_id if restored != null else &""), "longsword", "round-trip 后应恢复 weapon profile type id。")
	_assert_eq(String(restored.weapon_family if restored != null else &""), "sword", "round-trip 后应恢复 weapon family。")
	_assert_eq(String(restored.weapon_current_grip if restored != null else &""), "two_handed", "round-trip 后应恢复当前握法。")
	_assert_true(restored.weapon_uses_two_hands if restored != null else false, "round-trip 后应恢复双手握法布尔值。")
	_assert_eq(restored.weapon_attack_range if restored != null else -1, 2, "round-trip 后应恢复 weapon_attack_range。")
	_assert_eq(int(restored.weapon_two_handed_dice.get("dice_sides", -1)) if restored != null else -1, 10, "round-trip 后应恢复双手骰。")
	_assert_eq(String(restored.weapon_physical_damage_tag if restored != null else &""), "physical_slash", "round-trip 后应恢复武器伤害类型。")


func _test_damage_resolver_reports_hp_damage_after_shield_absorption() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source := BATTLE_UNIT_STATE_SCRIPT.new()
	source.unit_id = &"shield_contract_source"
	source.current_hp = 30
	source.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	var target := BATTLE_UNIT_STATE_SCRIPT.new()
	target.unit_id = &"shield_contract_target"
	target.current_hp = 30
	target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	target.current_shield_hp = 6
	target.shield_max_hp = 6
	target.shield_duration = 60
	target.shield_family = &"holy_barrier"
	target.shield_source_skill_id = &"priest_guardian_barrier"

	var damage_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = 10

	var result: Dictionary = resolver.resolve_effects(source, target, [damage_effect])

	_assert_eq(int(result.get("damage", -1)), 4, "damage 应表示实际进入 HP 的伤害。")
	_assert_eq(int(result.get("hp_damage", -1)), 4, "hp_damage 应与 damage 保持一致。")
	_assert_eq(int(result.get("shield_absorbed", -1)), 6, "shield_absorbed 应记录被护盾吃掉的伤害。")
	_assert_true(bool(result.get("shield_broken", false)), "护盾被完全耗尽时应返回 shield_broken=true。")
	_assert_eq(target.current_hp, 26, "护盾吸收后仅剩余伤害进入 HP。")
	_assert_eq(target.current_shield_hp, 0, "护盾耗尽后 current_shield_hp 应归零。")
	_assert_true(not target.has_shield(), "护盾耗尽后 BattleUnitState 不应继续视为有护盾。")


func _test_environmental_damage_helpers_report_shield_absorption() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()

	var fall_target := BATTLE_UNIT_STATE_SCRIPT.new()
	fall_target.unit_id = &"fall_damage_target"
	fall_target.current_hp = 30
	fall_target.current_shield_hp = 4
	fall_target.shield_max_hp = 4
	fall_target.shield_duration = 60
	fall_target.shield_family = &"stone_barrier"
	var fall_result: Dictionary = resolver.resolve_fall_damage(fall_target, 3)
	_assert_eq(int(fall_result.get("damage", -1)), 2, "坠落伤害在护盾吸收后应只把剩余伤害写入 HP。")
	_assert_eq(int(fall_result.get("shield_absorbed", -1)), 4, "坠落伤害应显式返回被护盾吸收的数值。")
	_assert_true(bool(fall_result.get("shield_broken", false)), "坠落伤害打空护盾时应返回 shield_broken=true。")
	_assert_eq(int((fall_result.get("damage_events", []) as Array).size()), 1, "坠落伤害 helper 应返回结构化 damage_events。")

	var collision_target := BATTLE_UNIT_STATE_SCRIPT.new()
	collision_target.unit_id = &"collision_damage_target"
	collision_target.current_hp = 30
	collision_target.current_shield_hp = 5
	collision_target.shield_max_hp = 5
	collision_target.shield_duration = 60
	collision_target.shield_family = &"holy_barrier"
	var collision_result: Dictionary = resolver.resolve_collision_damage(collision_target, 3, 1)
	_assert_eq(int(collision_result.get("damage", -1)), 25, "碰撞伤害在护盾吸收后应只把剩余伤害写入 HP。")
	_assert_eq(int(collision_result.get("shield_absorbed", -1)), 5, "碰撞伤害应显式返回被护盾吸收的数值。")
	_assert_true(bool(collision_result.get("shield_broken", false)), "碰撞伤害打空护盾时应返回 shield_broken=true。")
	_assert_eq(int((collision_result.get("damage_events", []) as Array).size()), 1, "碰撞伤害 helper 应返回结构化 damage_events。")


func _test_damage_resolver_uses_mitigation_tier_for_damage_type_defense() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source := BATTLE_UNIT_STATE_SCRIPT.new()
	source.unit_id = &"tier_resistance_source"
	source.current_hp = 30
	source.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	var target := BATTLE_UNIT_STATE_SCRIPT.new()
	target.unit_id = &"tier_resistance_target"
	target.current_hp = 30
	target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)

	var damage_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = 10
	damage_effect.damage_tag = &"fire"

	var baseline_result: Dictionary = resolver.resolve_effects(source, target, [damage_effect])
	_assert_eq(
		int(baseline_result.get("damage", -1)),
		10,
		"没有 mitigation_tier 状态时，火焰伤害不应被人物属性派生值减少。"
	)

	target.current_hp = 30
	_set_test_status(target, &"fire_half", {
		"damage_tag": &"fire",
		"mitigation_tier": &"half",
	})
	var half_result: Dictionary = resolver.resolve_effects(source, target, [damage_effect])
	_assert_eq(int(half_result.get("damage", -1)), 5, "火焰减免应通过 mitigation_tier=half 状态生效。")


func _test_damage_resolver_reports_mitigation_sources() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source := BATTLE_UNIT_STATE_SCRIPT.new()
	source.unit_id = &"mitigation_source_report_source"
	source.current_hp = 30
	source.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	var target := BATTLE_UNIT_STATE_SCRIPT.new()
	target.unit_id = &"mitigation_source_report_target"
	target.current_hp = 30
	target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)

	var magic_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	magic_effect.effect_type = &"damage"
	magic_effect.power = 10
	magic_effect.damage_tag = &"magic"
	_set_test_status(target, &"magic_shield", {
		"damage_category": &"magic",
		"mitigation_tier": &"half",
	})

	var half_result: Dictionary = resolver.resolve_effects(source, target, [magic_effect])
	var half_events = half_result.get("damage_events", [])
	_assert_true(half_events is Array and not (half_events as Array).is_empty(), "mitigation_tier 来源回归前置：应返回 damage_events。")
	if half_events is Array and not (half_events as Array).is_empty():
		var half_event := (half_events as Array)[0] as Dictionary
		var mitigation_sources = half_event.get("mitigation_sources", [])
		_assert_true(mitigation_sources is Array and not (mitigation_sources as Array).is_empty(), "damage_event 应记录 mitigation_tier 来源。")
		if mitigation_sources is Array and not (mitigation_sources as Array).is_empty():
			var source_entry := (mitigation_sources as Array)[0] as Dictionary
			_assert_eq(String(source_entry.get("status_id", "")), "magic_shield", "mitigation_tier 来源应保留状态 id。")
			_assert_eq(String(source_entry.get("tier", "")), "half", "mitigation_tier 来源应保留命中的 tier。")

	target.current_hp = 30
	target.status_effects.clear()
	_set_test_status(target, &"guarding", {}, 4)
	var physical_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	physical_effect.effect_type = &"damage"
	physical_effect.power = 10

	var guarded_result: Dictionary = resolver.resolve_effects(source, target, [physical_effect])
	var guarded_events = guarded_result.get("damage_events", [])
	_assert_true(guarded_events is Array and not (guarded_events as Array).is_empty(), "fixed mitigation 来源回归前置：应返回 damage_events。")
	if guarded_events is Array and not (guarded_events as Array).is_empty():
		var guarded_event := (guarded_events as Array)[0] as Dictionary
		var fixed_sources = guarded_event.get("fixed_mitigation_sources", [])
		_assert_true(fixed_sources is Array and not (fixed_sources as Array).is_empty(), "damage_event 应记录 fixed mitigation 来源。")
		if fixed_sources is Array and not (fixed_sources as Array).is_empty():
			var fixed_source := (fixed_sources as Array)[0] as Dictionary
			_assert_eq(String(fixed_source.get("status_id", "")), "guarding", "fixed mitigation 来源应保留状态 id。")
			_assert_eq(String(fixed_source.get("type", "")), "stance_reduction", "guarding 应以 stance_reduction 来源记录。")
			_assert_eq(int(fixed_source.get("value", -1)), 4, "guarding 来源应记录实际固定减伤值。")


func _test_death_ward_without_last_stand_does_not_block_fatal_physical_damage() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source := _build_manual_unit(&"plain_fatal_source", "普通致命攻击者", &"enemy", Vector2i.ZERO, [], 2, 0)
	var target := _build_manual_unit(&"spellward_only_target", "仅有负能量免疫目标", &"player", Vector2i(1, 0), [], 2, 0)
	target.current_hp = 8
	_set_test_status(target, &"death_ward", {
		"damage_tag": "negative_energy",
		"mitigation_tier": "immune",
	})

	var damage_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = 99
	damage_effect.damage_tag = &"physical_slash"
	var result: Dictionary = resolver.resolve_effects(source, target, [damage_effect])
	_assert_eq(int(result.get("damage", -1)), 99, "非 Last Stand 来源的 death_ward 不应吞掉普通致命 HP 伤害。")
	_assert_eq(target.current_hp, 0, "非 Last Stand 来源的 death_ward 遭遇普通致命伤害时应正常归零。")
	_assert_true(not target.is_alive, "非 Last Stand 来源的 death_ward 不应阻止死亡状态。")


func _test_last_stand_triggers_heal_and_consumes_death_ward_on_fatal_damage() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var skill_defs: Dictionary = game_session.get_skill_defs()
	var last_stand_skill := skill_defs.get(&"warrior_last_stand") as SkillDef
	_assert_true(last_stand_skill != null and last_stand_skill.combat_profile != null, "不屈回归需要 warrior_last_stand 技能资源。")
	if last_stand_skill == null or last_stand_skill.combat_profile == null:
		_cleanup_test_session(game_session)
		return

	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	resolver.set_skill_defs(skill_defs)
	var source := _build_manual_unit(&"last_stand_fatal_source", "不屈致命攻击者", &"enemy", Vector2i.ZERO, [], 2, 0)
	var target := _build_manual_unit(&"last_stand_target", "不屈目标", &"player", Vector2i(1, 0), [], 2, 0)
	target.current_hp = 8
	_set_test_status(target, &"death_ward", {
		"source_skill_id": "warrior_last_stand",
		"skill_level": 7,
	})
	_set_test_status(target, &"staggered", {}, 1)

	var damage_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = 99
	damage_effect.damage_tag = &"physical_slash"
	var result: Dictionary = resolver.resolve_effects(source, target, [damage_effect])
	_assert_eq(int(result.get("damage", -1)), 99, "不屈触发时仍应记录本次致命 HP 伤害。")
	_assert_true(target.current_hp > 0, "不屈触发后应通过 heal_fatal 把目标救回正 HP。")
	_assert_true(target.is_alive, "不屈触发后目标应保持存活。")
	_assert_true(not target.has_status_effect(&"death_ward"), "不屈触发后应消耗 death_ward。")
	_assert_true(not target.has_status_effect(&"staggered"), "Lv5+ 不屈触发后应清理负面状态。")
	var last_stand_active = target.get_status_effect(&"last_stand_active")
	_assert_true(last_stand_active != null, "Lv7 不屈触发后应获得 last_stand_active。")
	_assert_eq(int(last_stand_active.duration) if last_stand_active != null else -1, 90, "last_stand_active 应保留 90 TU 持续时间。")
	_cleanup_test_session(game_session)


func _test_damage_resolver_trigger_event_filters_conditional_effects() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source := _build_manual_unit(&"trigger_source", "触发测试者", &"player", Vector2i.ZERO, [], 2, 0)
	var normal_target := _build_manual_unit(&"trigger_normal_target", "普通目标", &"enemy", Vector2i(1, 0), [], 2, 0)
	var critical_target := _build_manual_unit(&"trigger_critical_target", "大成功目标", &"enemy", Vector2i(1, 0), [], 2, 0)
	var unsupported_target := _build_manual_unit(&"trigger_unsupported_target", "未知触发目标", &"enemy", Vector2i(1, 0), [], 2, 0)

	var armor_break_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	armor_break_effect.effect_type = &"status"
	armor_break_effect.status_id = &"armor_break"
	armor_break_effect.power = 1
	armor_break_effect.duration_tu = 90

	var staggered_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	staggered_effect.effect_type = &"status"
	staggered_effect.status_id = &"staggered"
	staggered_effect.power = 1
	staggered_effect.duration_tu = 60
	staggered_effect.trigger_event = &"critical_hit"

	var normal_result: Dictionary = resolver.resolve_effects(source, normal_target, [armor_break_effect, staggered_effect])
	_assert_true(normal_target.has_status_effect(&"armor_break"), "无触发条件的状态仍应正常生效。")
	_assert_true(not normal_target.has_status_effect(&"staggered"), "未大成功时 trigger_event=critical_hit 的状态不应生效。")
	_assert_true(not (normal_result.get("status_effect_ids", []) as Array).has(&"staggered"), "未触发的状态不应写入 result.status_effect_ids。")

	var critical_result: Dictionary = resolver.resolve_effects(
		source,
		critical_target,
		[armor_break_effect, staggered_effect],
		{"critical_hit": true}
	)
	_assert_true(critical_target.has_status_effect(&"armor_break"), "大成功时普通状态仍应生效。")
	_assert_true(critical_target.has_status_effect(&"staggered"), "大成功时 trigger_event=critical_hit 的状态应生效。")
	_assert_true((critical_result.get("status_effect_ids", []) as Array).has(&"staggered"), "触发后的状态应写入 result.status_effect_ids。")

	var unsupported_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	unsupported_effect.effect_type = &"status"
	unsupported_effect.status_id = &"unsupported_trigger_status"
	unsupported_effect.power = 1
	unsupported_effect.duration_tu = 30
	unsupported_effect.trigger_event = &"ordinary_hit"

	var unsupported_result: Dictionary = resolver.resolve_effects(source, unsupported_target, [unsupported_effect])
	_assert_true(not unsupported_target.has_status_effect(&"unsupported_trigger_status"), "未知 trigger_event 的状态不应静默生效。")
	_assert_true(not bool(unsupported_result.get("applied", true)), "未知 trigger_event 的效果不应把 result.applied 置为 true。")
	_assert_true(not (unsupported_result.get("status_effect_ids", []) as Array).has(&"unsupported_trigger_status"), "未知 trigger_event 不应写入 result.status_effect_ids。")


func _test_vajra_body_reduces_all_damage_tags_and_blocks_enemy_forced_move() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var damage_tags: Array[StringName] = [
		&"physical_slash",
		&"fire",
		&"negative_energy",
	]
	for damage_tag in damage_tags:
		var source := _build_manual_unit(&"vajra_attacker", "攻击者", &"enemy", Vector2i.ZERO, [], 2, 0)
		var target := _build_manual_unit(&"vajra_target", "金刚目标", &"player", Vector2i.ZERO, [], 2, 0)
		_set_test_status(target, &"vajra_body", {
			"passive_reduction": 3,
		})
		var effect_def = COMBAT_EFFECT_DEF_SCRIPT.new()
		effect_def.effect_type = &"damage"
		effect_def.power = 10
		effect_def.damage_tag = damage_tag
		var effect_defs: Array[CombatEffectDef] = [effect_def]
		var result: Dictionary = resolver.resolve_effects(source, target, effect_defs)
		_assert_eq(int(result.get("damage", -1)), 7, "金刚不坏应对 %s 生效并减少 3 点伤害。" % String(damage_tag))
		_assert_eq(target.current_hp, 23, "金刚不坏减伤后目标 HP 应只扣除实际伤害。")
		var damage_events: Array = result.get("damage_events", [])
		_assert_true(not damage_events.is_empty(), "金刚不坏减伤应保留 damage event。")
		if not damage_events.is_empty():
			var event: Dictionary = damage_events[0]
			_assert_eq(int(event.get("passive_reduction", -1)), 3, "damage event 应记录 passive_reduction。")
			_assert_eq(int(event.get("fixed_mitigation_total", -1)), 3, "fixed mitigation total 应包含 passive_reduction。")
			_assert_true(
				_fixed_sources_include(event.get("fixed_mitigation_sources", []), "vajra_body", "passive_reduction"),
				"fixed_mitigation_sources 应标记金刚不坏来源。"
			)

	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var enemy := _build_manual_unit(&"vajra_pusher", "推动者", &"enemy", Vector2i(2, 0), [], 2, 0)
	var target := _build_manual_unit(&"vajra_anchor", "金刚锚点", &"player", Vector2i(1, 0), [], 2, 0)
	var member_id: StringName = game_session.get_party_state().get_resolved_main_character_member_id()
	var member_state = game_session.get_party_state().get_member_state(member_id)
	var vajra_progress = UNIT_SKILL_PROGRESS_SCRIPT.new()
	vajra_progress.skill_id = &"vajra_body"
	vajra_progress.is_learned = true
	vajra_progress.skill_level = 10
	vajra_progress.is_core = true
	member_state.progression.set_skill_progress(vajra_progress)
	_set_test_status(target, &"vajra_body", {
		"forced_move_immune": true,
		"passive_reduction": 6,
	})
	_add_unit_to_state(facade, state, enemy, true)
	_add_unit_to_state(facade, state, target, false)
	_apply_battle_state(facade, state)
	target.source_member_id = member_id
	target.current_hp = 9
	enemy.attribute_snapshot.set_value(&"fortune_mark_target", 1)
	facade._battle_runtime._battle_rating_system.initialize_battle_rating_stats()
	var batch = BATTLE_EVENT_BATCH_SCRIPT.new()
	facade._battle_runtime._record_vajra_body_mastery_from_incoming_damage(
		enemy,
		target,
		_build_test_damage_skill(&"wolf_heavy_crush", "狼王重击", 10, &"", &""),
		{
			"critical_hit": true,
			"damage_events": [
				{"damage": 3, "hp_damage": 3, "damage_dice_high_total_roll": true},
				{"damage": 4, "hp_damage": 4, "damage_dice_high_total_roll": true},
				{"damage": 0, "hp_damage": 0, "shield_absorbed": 5, "damage_dice_high_total_roll": true},
				{"damage": 2, "hp_damage": 2, "damage_dice_high_total_roll": false},
			],
		},
		batch
	)
	var updated_vajra_progress = member_state.progression.get_skill_progress(&"vajra_body")
	_assert_eq(
		int(updated_vajra_progress.total_mastery_earned),
		8,
		"金刚不坏熟练度应按多段命中、精英来源与低血量倍率实时入账。"
	)
	_assert_eq(batch.progression_deltas.size(), 1, "金刚不坏受击熟练度应写入当前 battle batch。")
	facade._battle_runtime._record_vajra_body_mastery_from_incoming_damage(
		enemy,
		target,
		_build_test_damage_skill(&"wolf_heavy_crush", "狼王重击", 10, &"", &""),
		{
			"critical_hit": true,
			"damage_events": [
				{"damage": 0, "hp_damage": 0, "shield_absorbed": 5, "damage_dice_high_total_roll": true},
			],
		},
		batch
	)
	_assert_eq(
		int(updated_vajra_progress.total_mastery_earned),
		8,
		"金刚不坏熟练度不应因护盾完全吸收的 high-total 事件入账。"
	)
	var forced_move_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	forced_move_effect.effect_type = &"forced_move"
	forced_move_effect.forced_move_distance = 1
	forced_move_effect.forced_move_mode = &"retreat"
	var moved_steps := int(facade._battle_runtime._apply_forced_move_effect(enemy, target, forced_move_effect, null))
	_assert_eq(moved_steps, 0, "金刚不坏 10 级状态应阻止敌方强制位移。")
	_assert_eq(target.coord, Vector2i(1, 0), "被金刚不坏固定的目标坐标不应改变。")
	_cleanup_test_session(game_session)


func _test_forced_move_requires_formal_fields() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var enemy := _build_manual_unit(&"forced_move_pusher", "推动者", &"enemy", Vector2i(2, 0), [], 2, 0)
	var target := _build_manual_unit(&"forced_move_target", "目标", &"player", Vector2i(1, 0), [], 2, 0)
	_add_unit_to_state(facade, state, enemy, true)
	_add_unit_to_state(facade, state, target, false)
	_apply_battle_state(facade, state)

	var legacy_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	legacy_effect.effect_type = &"forced_move"
	legacy_effect.params = {
		"mode": "retreat",
		"distance": 1,
	}
	var legacy_moved_steps := int(facade._battle_runtime._apply_forced_move_effect(enemy, target, legacy_effect, null))
	_assert_eq(legacy_moved_steps, 0, "旧 params.mode / params.distance 不应再驱动 forced_move。")
	_assert_eq(target.coord, Vector2i(1, 0), "只提供旧 forced_move params 时目标坐标不应改变。")

	var formal_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	formal_effect.effect_type = &"forced_move"
	formal_effect.forced_move_mode = &"retreat"
	formal_effect.forced_move_distance = 1
	var formal_moved_steps := int(facade._battle_runtime._apply_forced_move_effect(enemy, target, formal_effect, null))
	_assert_eq(formal_moved_steps, 1, "正式 forced_move_mode / forced_move_distance 应继续驱动位移。")
	_assert_eq(target.coord, Vector2i(0, 0), "正式 retreat 位移应把目标推到更远格。")
	_cleanup_test_session(game_session)


func _test_damage_resolver_guarding_only_reduces_physical_damage() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source := BATTLE_UNIT_STATE_SCRIPT.new()
	source.unit_id = &"guarding_source"
	source.current_hp = 30
	source.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	source.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)

	var physical_target := BATTLE_UNIT_STATE_SCRIPT.new()
	physical_target.unit_id = &"guarding_physical_target"
	physical_target.current_hp = 30
	physical_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	_set_test_status(physical_target, &"guarding", {}, 4)

	var magic_target := BATTLE_UNIT_STATE_SCRIPT.new()
	magic_target.unit_id = &"guarding_magic_target"
	magic_target.current_hp = 30
	magic_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	_set_test_status(magic_target, &"guarding", {}, 4)

	var physical_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	physical_effect.effect_type = &"damage"
	physical_effect.power = 10

	var magic_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	magic_effect.effect_type = &"damage"
	magic_effect.power = 10
	magic_effect.damage_tag = &"magic"

	var physical_result: Dictionary = resolver.resolve_effects(source, physical_target, [physical_effect])
	var magic_result: Dictionary = resolver.resolve_effects(source, magic_target, [magic_effect])

	_assert_eq(int(physical_result.get("damage", -1)), 6, "guarding 应按固定值减少物理伤害。")
	_assert_eq(int(magic_result.get("damage", -1)), 10, "guarding 不应减少法术 / 能量伤害。")


func _test_damage_resolver_damage_reduction_up_uses_fixed_value() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source := BATTLE_UNIT_STATE_SCRIPT.new()
	source.unit_id = &"damage_reduction_up_source"
	source.current_hp = 30
	source.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	var target := BATTLE_UNIT_STATE_SCRIPT.new()
	target.unit_id = &"damage_reduction_up_target"
	target.current_hp = 30
	target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	_set_test_status(target, &"damage_reduction_up")

	var damage_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = 10

	var result: Dictionary = resolver.resolve_effects(source, target, [damage_effect])

	_assert_eq(int(result.get("damage", -1)), 8, "damage_reduction_up 应改为固定减伤；power=1 当前应减 2。")


func _test_content_skill_magic_shield_halves_generic_magic_damage() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_defs: Dictionary = game_session.get_skill_defs()
	var magic_shield_skill := skill_defs.get(&"mage_magic_shield") as SkillDef
	var arcane_missile_skill := skill_defs.get(&"mage_arcane_missile") as SkillDef
	_assert_true(
		magic_shield_skill != null and magic_shield_skill.combat_profile != null,
		"资源回归前置：mage_magic_shield 应已正式挂上 combat_profile。"
	)
	_assert_true(
		arcane_missile_skill != null and arcane_missile_skill.combat_profile != null,
		"资源回归前置：mage_arcane_missile 应存在可用伤害效果。"
	)
	if magic_shield_skill == null or magic_shield_skill.combat_profile == null or arcane_missile_skill == null or arcane_missile_skill.combat_profile == null:
		_cleanup_test_session(game_session)
		return

	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var support_unit := _build_manual_unit(&"magic_shield_support", "魔力护盾施法者", &"player", Vector2i.ZERO, [], 2, 6)
	var attacker := _build_manual_unit(&"magic_shield_attacker", "奥术攻击者", &"enemy", Vector2i(1, 0), [], 2, 6)
	attacker.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	var baseline_target := _build_manual_unit(&"magic_shield_baseline", "魔力护盾基线目标", &"player", Vector2i(0, 0), [], 2, 6)
	baseline_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var target := _build_manual_unit(&"magic_shield_target", "魔力护盾目标", &"player", Vector2i(0, 1), [], 2, 6)
	target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var baseline_result: Dictionary = resolver.resolve_effects(attacker, baseline_target, arcane_missile_skill.combat_profile.effect_defs)
	var baseline_damage := int(baseline_result.get("damage", 0))

	var apply_result: Dictionary = resolver.resolve_skill(support_unit, target, magic_shield_skill)
	_assert_true(bool(apply_result.get("applied", false)), "mage_magic_shield 施放后应正式写入状态。")
	_assert_true(target.has_status_effect(&"magic_shield"), "mage_magic_shield 应在目标身上留下 magic_shield 状态。")

	var result: Dictionary = resolver.resolve_effects(attacker, target, arcane_missile_skill.combat_profile.effect_defs)
	_assert_eq(
		int(result.get("damage", -1)),
		int(floor(float(baseline_damage) / 2.0)),
		"magic_shield 应按 HALF 规则结算通用奥术伤害。"
	)

	_cleanup_test_session(game_session)


func _test_content_skills_prismatic_barrier_and_spellward_map_half_and_immune() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_defs: Dictionary = game_session.get_skill_defs()
	var prismatic_barrier_skill := skill_defs.get(&"mage_prismatic_barrier") as SkillDef
	var spellward_skill := skill_defs.get(&"mage_spellward") as SkillDef
	var fireball_skill := skill_defs.get(&"mage_fireball") as SkillDef
	var arcane_missile_skill := skill_defs.get(&"mage_arcane_missile") as SkillDef
	var shadow_bolt_skill := skill_defs.get(&"mage_shadow_bolt") as SkillDef
	_assert_true(
		prismatic_barrier_skill != null and prismatic_barrier_skill.combat_profile != null,
		"资源回归前置：mage_prismatic_barrier 应已正式挂上 combat_profile。"
	)
	_assert_true(
		spellward_skill != null and spellward_skill.combat_profile != null,
		"资源回归前置：mage_spellward 应已正式挂上 combat_profile。"
	)
	_assert_true(
		fireball_skill != null and fireball_skill.combat_profile != null and arcane_missile_skill != null and arcane_missile_skill.combat_profile != null and shadow_bolt_skill != null and shadow_bolt_skill.combat_profile != null,
		"资源回归前置：火焰 / 奥术 / 负能量攻击技能应存在。"
	)
	if prismatic_barrier_skill == null or prismatic_barrier_skill.combat_profile == null or spellward_skill == null or spellward_skill.combat_profile == null or fireball_skill == null or fireball_skill.combat_profile == null or arcane_missile_skill == null or arcane_missile_skill.combat_profile == null or shadow_bolt_skill == null or shadow_bolt_skill.combat_profile == null:
		_cleanup_test_session(game_session)
		return

	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var support_unit := _build_manual_unit(&"mitigation_support", "减伤施法者", &"player", Vector2i.ZERO, [], 2, 6)
	var attacker := _build_manual_unit(&"mitigation_attacker", "法术攻击者", &"enemy", Vector2i(1, 0), [], 2, 6)
	attacker.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)

	var fire_baseline_target := _build_manual_unit(&"prismatic_fire_baseline", "棱彩火焰基线目标", &"player", Vector2i(0, 0), [], 2, 6)
	fire_baseline_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var prismatic_target := _build_manual_unit(&"prismatic_target", "棱彩目标", &"player", Vector2i(0, 1), [], 2, 6)
	prismatic_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var fire_baseline_result: Dictionary = resolver.resolve_effects(attacker, fire_baseline_target, fireball_skill.combat_profile.effect_defs)
	var fire_baseline_damage := int(fire_baseline_result.get("damage", 0))
	resolver.resolve_skill(support_unit, prismatic_target, prismatic_barrier_skill)
	_assert_true(prismatic_target.has_status_effect(&"prismatic_barrier"), "mage_prismatic_barrier 应在目标身上留下 prismatic_barrier 状态。")
	var fire_result: Dictionary = resolver.resolve_effects(attacker, prismatic_target, fireball_skill.combat_profile.effect_defs)
	_assert_eq(
		int(fire_result.get("damage", -1)),
		int(floor(float(fire_baseline_damage) / 2.0)),
		"prismatic_barrier 应按 HALF 规则结算元素伤害。"
	)

	var generic_baseline_target := _build_manual_unit(&"prismatic_generic_baseline", "棱彩泛法术基线目标", &"player", Vector2i(0, 0), [], 2, 6)
	generic_baseline_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var prismatic_generic_target := _build_manual_unit(&"prismatic_generic_target", "棱彩泛法术目标", &"player", Vector2i(0, 2), [], 2, 6)
	prismatic_generic_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var generic_baseline_result: Dictionary = resolver.resolve_effects(attacker, generic_baseline_target, arcane_missile_skill.combat_profile.effect_defs)
	var generic_baseline_damage := int(generic_baseline_result.get("damage", 0))
	resolver.resolve_skill(support_unit, prismatic_generic_target, prismatic_barrier_skill)
	var generic_result: Dictionary = resolver.resolve_effects(attacker, prismatic_generic_target, arcane_missile_skill.combat_profile.effect_defs)
	_assert_eq(int(generic_result.get("damage", -1)), generic_baseline_damage, "prismatic_barrier 不应错误覆盖无元素 tag 的通用奥术伤害。")

	var spellward_baseline_target := _build_manual_unit(&"spellward_arcane_baseline", "结界泛法术基线目标", &"player", Vector2i(0, 0), [], 2, 6)
	spellward_baseline_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var spellward_target := _build_manual_unit(&"spellward_target", "结界目标", &"player", Vector2i(0, 3), [], 2, 6)
	spellward_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var spellward_baseline_result: Dictionary = resolver.resolve_effects(attacker, spellward_baseline_target, arcane_missile_skill.combat_profile.effect_defs)
	var spellward_baseline_damage := int(spellward_baseline_result.get("damage", 0))
	resolver.resolve_skill(support_unit, spellward_target, spellward_skill)
	_assert_true(spellward_target.has_status_effect(&"spellward"), "mage_spellward 应在目标身上留下 spellward 状态。")
	_assert_true(spellward_target.has_status_effect(&"death_ward"), "mage_spellward 应额外在目标身上留下 death_ward 状态。")
	var spellward_arcane_result: Dictionary = resolver.resolve_effects(attacker, spellward_target, arcane_missile_skill.combat_profile.effect_defs)
	_assert_eq(
		int(spellward_arcane_result.get("damage", -1)),
		int(floor(float(spellward_baseline_damage) / 2.0)),
		"spellward 应按 HALF 规则结算通用法术伤害。"
	)

	var immune_target := _build_manual_unit(&"death_ward_target", "负能量免疫目标", &"player", Vector2i(0, 4), [], 2, 6)
	immune_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	resolver.resolve_skill(support_unit, immune_target, spellward_skill)
	var immune_result: Dictionary = resolver.resolve_effects(attacker, immune_target, shadow_bolt_skill.combat_profile.effect_defs)
	_assert_eq(int(immune_result.get("damage", -1)), 0, "death_ward 应让负能量伤害直接归零。")

	_cleanup_test_session(game_session)


func _test_content_skill_hex_of_frailty_applies_double_and_cancels_with_half() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_defs: Dictionary = game_session.get_skill_defs()
	var hex_skill := skill_defs.get(&"mage_hex_of_frailty") as SkillDef
	var magic_shield_skill := skill_defs.get(&"mage_magic_shield") as SkillDef
	var spellward_skill := skill_defs.get(&"mage_spellward") as SkillDef
	var arcane_missile_skill := skill_defs.get(&"mage_arcane_missile") as SkillDef
	var shadow_bolt_skill := skill_defs.get(&"mage_shadow_bolt") as SkillDef
	_assert_true(
		hex_skill != null and hex_skill.combat_profile != null,
		"资源回归前置：mage_hex_of_frailty 应已正式挂上 combat_profile。"
	)
	_assert_true(
		magic_shield_skill != null and magic_shield_skill.combat_profile != null and spellward_skill != null and spellward_skill.combat_profile != null and arcane_missile_skill != null and arcane_missile_skill.combat_profile != null and shadow_bolt_skill != null and shadow_bolt_skill.combat_profile != null,
		"资源回归前置：HALF / IMMUNE / DOUBLE 相关技能都应存在。"
	)
	if hex_skill == null or hex_skill.combat_profile == null or magic_shield_skill == null or magic_shield_skill.combat_profile == null or spellward_skill == null or spellward_skill.combat_profile == null or arcane_missile_skill == null or arcane_missile_skill.combat_profile == null or shadow_bolt_skill == null or shadow_bolt_skill.combat_profile == null:
		_cleanup_test_session(game_session)
		return

	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var curse_caster := _build_manual_unit(&"frailty_caster", "衰弱施法者", &"enemy", Vector2i.ZERO, [], 2, 6)
	curse_caster.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	var support_unit := _build_manual_unit(&"frailty_support", "防护施法者", &"player", Vector2i(1, 0), [], 2, 6)
	var attacker := _build_manual_unit(&"frailty_attacker", "奥术攻击者", &"enemy", Vector2i(2, 0), [], 2, 6)
	attacker.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)

	var double_baseline_target := _build_manual_unit(&"frailty_baseline_target", "法术易伤基线目标", &"player", Vector2i(0, 0), [], 2, 6)
	double_baseline_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var double_target := _build_manual_unit(&"frailty_target", "法术易伤目标", &"player", Vector2i(0, 1), [], 2, 6)
	double_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var double_baseline_result: Dictionary = resolver.resolve_effects(attacker, double_baseline_target, arcane_missile_skill.combat_profile.effect_defs)
	var double_baseline_damage := int(double_baseline_result.get("damage", 0))
	resolver.resolve_skill(curse_caster, double_target, hex_skill)
	double_target.current_hp = 30
	_assert_true(double_target.has_status_effect(&"hex_of_frailty"), "mage_hex_of_frailty 应在目标身上留下 hex_of_frailty 状态。")
	var double_result: Dictionary = resolver.resolve_effects(attacker, double_target, arcane_missile_skill.combat_profile.effect_defs)
	_assert_eq(int(double_result.get("damage", -1)), double_baseline_damage * 2, "hex_of_frailty 应按 DOUBLE 规则放大通用法术伤害。")

	var canceled_target := _build_manual_unit(&"frailty_canceled_target", "半伤抵消目标", &"player", Vector2i(0, 2), [], 2, 6)
	canceled_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	resolver.resolve_skill(curse_caster, canceled_target, hex_skill)
	canceled_target.current_hp = 30
	resolver.resolve_skill(support_unit, canceled_target, magic_shield_skill)
	var canceled_result: Dictionary = resolver.resolve_effects(attacker, canceled_target, arcane_missile_skill.combat_profile.effect_defs)
	_assert_eq(int(canceled_result.get("damage", -1)), double_baseline_damage, "magic_shield 的 HALF 应与 hex_of_frailty 的 DOUBLE 互相抵消，回到 NORMAL。")

	var immune_target := _build_manual_unit(&"frailty_immune_target", "免疫优先目标", &"player", Vector2i(0, 3), [], 2, 6)
	immune_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	resolver.resolve_skill(curse_caster, immune_target, hex_skill)
	immune_target.current_hp = 30
	resolver.resolve_skill(support_unit, immune_target, spellward_skill)
	var immune_result: Dictionary = resolver.resolve_effects(attacker, immune_target, shadow_bolt_skill.combat_profile.effect_defs)
	_assert_eq(int(immune_result.get("damage", -1)), 0, "当 DOUBLE 与 IMMUNE 同时存在时，IMMUNE 必须保持最高优先级。")

	_cleanup_test_session(game_session)


func _test_shield_dice_roll_is_random_and_shared_per_cast() -> void:
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	var state: BattleState = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"shield_dice_protocol"
	state.seed = 20260419
	runtime._state = state

	var skill_def := _build_test_dice_shield_skill(&"test_priest_aid", "测试援助术", 1, 8, 3, 60)
	var effect_defs: Array[CombatEffectDef] = skill_def.combat_profile.effect_defs
	var caster := _build_manual_unit(&"shield_caster", "施法者", &"player", Vector2i.ZERO, [], 2, 8)
	var ally_a := _build_manual_unit(&"shield_ally_a", "友军A", &"player", Vector2i(1, 0), [], 2, 0)
	var ally_b := _build_manual_unit(&"shield_ally_b", "友军B", &"player", Vector2i(0, 1), [], 2, 0)
	var ally_c := _build_manual_unit(&"shield_ally_c", "友军C", &"player", Vector2i(2, 0), [], 2, 0)
	var ally_d := _build_manual_unit(&"shield_ally_d", "友军D", &"player", Vector2i(0, 2), [], 2, 0)

	var first_roll_context := {}
	var first_result_a := runtime._apply_unit_shield_effects(caster, ally_a, skill_def, effect_defs, first_roll_context)
	var first_result_b := runtime._apply_unit_shield_effects(caster, ally_b, skill_def, effect_defs, first_roll_context)
	var second_roll_context := {}
	var second_result_a := runtime._apply_unit_shield_effects(caster, ally_c, skill_def, effect_defs, second_roll_context)
	var second_result_b := runtime._apply_unit_shield_effects(caster, ally_d, skill_def, effect_defs, second_roll_context)

	var first_shield_hp := int(first_result_a.get("current_shield_hp", 0))
	var second_shield_hp := int(second_result_a.get("current_shield_hp", 0))
	_assert_true(bool(first_result_a.get("applied", false)), "骰子护盾对首个友军应成功生效。")
	_assert_true(bool(first_result_b.get("applied", false)), "骰子护盾对第二个友军应成功生效。")
	_assert_eq(first_shield_hp, int(first_result_b.get("current_shield_hp", -1)), "同一次群体施法的所有目标应共享同一组护盾骰值。")
	_assert_eq(second_shield_hp, int(second_result_b.get("current_shield_hp", -1)), "下一次群体施法仍应共享同一组护盾骰值。")
	_assert_true(first_shield_hp >= 4 and first_shield_hp <= 11, "1d8+3 护盾值应落在合法区间内。")
	_assert_true(second_shield_hp >= 4 and second_shield_hp <= 11, "下一次 1d8+3 护盾值也应落在合法区间内。")


func _test_facade_shield_skill_writes_shield_and_does_not_decay_on_tu_tick() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_def := _build_test_shield_skill(&"test_priest_guardian_barrier", "测试圣护", 8, 60)
	game_session.get_skill_defs()[skill_def.skill_id] = skill_def

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	state.timeline.tick_interval_seconds = 1.0
	state.timeline.tu_per_tick = 5
	var caster: BattleUnitState = _build_manual_unit(
		&"shield_caster",
		"护盾施法者",
		&"player",
		Vector2i(0, 0),
		[skill_def.skill_id],
		2,
		6
	)
	caster.action_threshold = 100
	var ally: BattleUnitState = _build_manual_unit(
		&"shield_ally",
		"护盾友军",
		&"player",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	ally.action_threshold = 100
	var enemy: BattleUnitState = _build_manual_unit(
		&"shield_enemy",
		"敌人",
		&"enemy",
		Vector2i(2, 0),
		[],
		2,
		0
	)
	enemy.action_threshold = 100
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, ally, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择测试护盾技能应返回成功结果。")
	var cast_result: Dictionary = facade.command_battle_move_to(ally.coord)
	_assert_true(bool(cast_result.get("ok", false)), "执行测试护盾技能应返回成功结果。")

	var runtime_state := facade.get_battle_state()
	var runtime_ally := runtime_state.units.get(ally.unit_id) as BattleUnitState if runtime_state != null else null
	var battle_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	var ally_snapshot := _find_battle_unit_snapshot(battle_snapshot, String(ally.unit_id))

	_assert_true(runtime_ally != null, "护盾回归中应能从 battle state 读取友军单位。")
	_assert_eq(runtime_ally.current_shield_hp if runtime_ally != null else -1, 8, "护盾技能应把 current_shield_hp 正式写入 battle state。")
	_assert_eq(runtime_ally.shield_max_hp if runtime_ally != null else -1, 8, "护盾技能应把 shield_max_hp 正式写入 battle state。")
	_assert_eq(runtime_ally.shield_duration if runtime_ally != null else -1, 60, "护盾技能应把 shield_duration 正式写入 battle state。")
	_assert_eq(String(runtime_ally.shield_family if runtime_ally != null else &""), "holy_barrier", "护盾技能应写入正式 shield_family。")
	_assert_eq(int(ally_snapshot.get("current_shield_hp", -1)), 8, "battle snapshot 应稳定暴露 current_shield_hp。")
	_assert_eq(int(ally_snapshot.get("shield_duration", -1)), 60, "battle snapshot 应稳定暴露 shield_duration。")
	var text_snapshot := facade.build_text_snapshot()
	_assert_true(text_snapshot.contains("shield=8/8"), "battle 文本快照应渲染当前护盾值。")
	_assert_true(text_snapshot.contains("dur=60"), "battle 文本快照应渲染护盾持续时间。")

	if runtime_state != null:
		runtime_state.phase = &"timeline_running"
		runtime_state.active_unit_id = &""
	var changed := facade.advance(1.0)
	_assert_true(changed, "切回 timeline_running 后 facade.advance() 应正式推进 TU。")

	runtime_state = facade.get_battle_state()
	runtime_ally = runtime_state.units.get(ally.unit_id) as BattleUnitState if runtime_state != null else null
	_assert_eq(int(runtime_state.timeline.current_tu) if runtime_state != null and runtime_state.timeline != null else -1, 5, "护盾回归中 battle tick 后 current_tu 应正式推进 5。")
	_assert_eq(runtime_ally.current_shield_hp if runtime_ally != null else -1, 8, "当前版本护盾不应随 TU 推进而递减数值。")
	_assert_eq(runtime_ally.shield_duration if runtime_ally != null else -1, 60, "当前版本 shield_duration 不应随 TU 推进自动递减。")

	_cleanup_test_session(game_session)


func _test_preview_reports_shield_absorption_and_break() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_def := _build_test_damage_skill(
		&"test_preview_shield_break",
		"测试破盾打击",
		10,
		ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS,
		ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS
	)
	game_session.get_skill_defs()[skill_def.skill_id] = skill_def

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	var caster: BattleUnitState = _build_manual_unit(
		&"preview_shield_user",
		"预览施法者",
		&"player",
		Vector2i(0, 0),
		[skill_def.skill_id],
		2,
		0
	)
	caster.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	var enemy: BattleUnitState = _build_manual_unit(
		&"preview_shield_enemy",
		"预览护盾目标",
		&"enemy",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	enemy.current_shield_hp = 6
	enemy.shield_max_hp = 6
	enemy.shield_duration = 60
	enemy.shield_family = &"holy_barrier"
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var command := BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = skill_def.skill_id
	command.target_unit_id = enemy.unit_id
	command.target_coord = enemy.coord
	var preview = facade.preview_battle_command(command)

	_assert_true(preview != null and preview.allowed, "护盾吸收预览回归前置：preview_command 应允许目标。")
	if preview != null:
		_assert_true(
			preview.log_lines.any(func(line): return String(line) == "伤害 10"),
			"preview 应只提示非暴击基础伤害范围，不结算护盾后的 HP 伤害。 log=%s" % [str(preview.log_lines)]
		)
		_assert_true(
			not preview.log_lines.any(func(line): return String(line).contains("护盾会") or String(line).contains("吸收") or String(line).contains("击碎")),
			"preview 不应结算或提示 shield 吸收/破裂。 log=%s" % [str(preview.log_lines)]
		)
		_assert_eq(enemy.current_shield_hp, 6, "preview 不应改变目标护盾值。")

	_cleanup_test_session(game_session)


func _test_runtime_logs_zero_hp_damage_when_shield_absorbs_everything() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_def := _build_test_damage_skill(
		&"test_zero_hp_damage_shield",
		"测试零掉血护盾打击",
		6,
		ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS,
		ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS
	)
	game_session.get_skill_defs()[skill_def.skill_id] = skill_def

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	var caster: BattleUnitState = _build_manual_unit(
		&"shield_log_user",
		"护盾日志施法者",
		&"player",
		Vector2i(0, 0),
		[skill_def.skill_id],
		2,
		0
	)
	caster.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	var enemy: BattleUnitState = _build_manual_unit(
		&"shield_log_enemy",
		"护盾日志目标",
		&"enemy",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	enemy.current_shield_hp = 6
	enemy.shield_max_hp = 6
	enemy.shield_duration = 60
	enemy.shield_family = &"holy_barrier"
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var command := BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = skill_def.skill_id
	command.target_unit_id = enemy.unit_id
	command.target_coord = enemy.coord
	var batch = facade._battle_runtime.issue_command(command)

	_assert_true(batch != null, "零掉血护盾日志回归前置：runtime.issue_command 应返回 batch。")
	_assert_eq(enemy.current_hp, 30, "伤害被护盾完全吸收时，目标 HP 不应下降。")
	_assert_eq(enemy.current_shield_hp, 0, "伤害被护盾完全吸收并打空时，护盾值应归零。")
	_assert_true(
		batch != null and batch.log_lines.any(func(line): return String(line).contains("被护盾吸收了 6 点伤害")),
		"runtime log 应显式提示 0 掉血但被护盾吸收。 log=%s" % [str(batch.log_lines if batch != null else [])]
	)
	_assert_true(
		batch != null and batch.log_lines.any(func(line): return String(line).contains("护盾被击碎")),
		"runtime log 应显式提示护盾被击碎。 log=%s" % [str(batch.log_lines if batch != null else [])]
	)

	_cleanup_test_session(game_session)


func _test_guard_incoming_physical_hit_mastery_writes_batch_after_damage_resolution() -> void:
	var skill_def := _build_test_damage_skill(
		&"test_guard_training_hit",
		"测试格挡训练打击",
		6,
		ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS,
		ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS,
		&"physical_slash"
	)
	var guard_def := SKILL_DEF_SCRIPT.new()
	guard_def.skill_id = &"warrior_guard"
	guard_def.combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	guard_def.combat_profile.skill_id = guard_def.skill_id
	guard_def.combat_profile.mastery_trigger_mode = &"incoming_physical_hit"
	guard_def.combat_profile.mastery_amount_mode = &"per_target_rank"
	var skill_defs := {
		skill_def.skill_id: skill_def,
		guard_def.skill_id: guard_def,
	}

	var gateway := RecordingCharacterGateway.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(gateway, skill_defs, {}, {})

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	var attacker: BattleUnitState = _build_manual_unit(
		&"guard_training_attacker",
		"格挡训练攻击者",
		&"enemy",
		Vector2i(0, 0),
		[skill_def.skill_id],
		2,
		0
	)
	var defender: BattleUnitState = _build_manual_unit(
		&"guard_training_defender",
		"格挡训练目标",
		&"player",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	defender.source_member_id = &"hero"
	_set_test_status(defender, &"guarding", {}, 1)
	_add_unit_to_runtime_state(runtime, state, attacker, true)
	_add_unit_to_runtime_state(runtime, state, defender, false)
	state.phase = &"unit_acting"
	state.active_unit_id = attacker.unit_id
	runtime._state = state

	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var result: Dictionary = resolver.resolve_effects(
		attacker,
		defender,
		skill_def.combat_profile.effect_defs,
		{"attack_success": true}
	)
	result["attack_success"] = true
	var guard_mastery_grant := BattleSkillMasteryService.new().build_guard_mastery_grant_from_incoming_hit(
		attacker,
		defender,
		skill_def.combat_profile.effect_defs,
		result,
		skill_defs
	)
	var batch = BATTLE_EVENT_BATCH_SCRIPT.new()
	runtime._apply_skill_mastery_grant(defender, guard_mastery_grant, batch)

	_assert_eq(defender.current_hp, 25, "格挡受击熟练度入账不应打断本次伤害结算，目标应受到 5 点 HP 伤害。")
	_assert_eq(int(guard_mastery_grant.get("amount", 0)), 1, "格挡应在承受敌方物理命中后生成 1 点熟练度 grant。")
	_assert_eq(
		gateway.source_mastery_count,
		1,
		"格挡受击熟练度应通过 runtime gateway 发放。"
	)
	_assert_eq(batch.progression_deltas.size(), 1, "格挡受击熟练度应写入当前 battle batch。")


func _test_guard_mastery_requires_physical_hp_damage() -> void:
	var skill_defs: Dictionary = {}
	var guard_def := SKILL_DEF_SCRIPT.new()
	guard_def.skill_id = &"warrior_guard"
	guard_def.combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	guard_def.combat_profile.skill_id = guard_def.skill_id
	guard_def.combat_profile.mastery_trigger_mode = &"incoming_physical_hit"
	guard_def.combat_profile.mastery_amount_mode = &"per_target_rank"
	skill_defs[guard_def.skill_id] = guard_def

	var physical_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	physical_effect.effect_type = &"damage"
	physical_effect.damage_tag = &"physical_slash"
	var service := BattleSkillMasteryService.new()
	var attacker := _build_manual_unit(&"guard_mastery_attacker", "攻击者", &"enemy", Vector2i.ZERO, [], 2, 0)
	var defender := _build_manual_unit(&"guard_mastery_defender", "防守者", &"player", Vector2i(1, 0), [], 2, 0)
	defender.source_member_id = &"hero"
	_set_test_status(defender, &"guarding", {}, 1)

	var miss_grant := service.build_guard_mastery_grant_from_incoming_hit(
		attacker,
		defender,
		[physical_effect],
		{"attack_success": false, "damage": 0, "shield_absorbed": 0},
		skill_defs
	)
	_assert_true(miss_grant.is_empty(), "格挡熟练度不应因未命中物理攻击入账。")

	var zero_damage_grant := service.build_guard_mastery_grant_from_incoming_hit(
		attacker,
		defender,
		[physical_effect],
		{"attack_success": true, "damage": 0, "shield_absorbed": 0},
		skill_defs
	)
	_assert_true(zero_damage_grant.is_empty(), "格挡熟练度不应因完全减免的 0 伤害物理命中入账。")

	var shield_block_grant := service.build_guard_mastery_grant_from_incoming_hit(
		attacker,
		defender,
		[physical_effect],
		{"attack_success": true, "damage": 0, "shield_absorbed": 6},
		skill_defs
	)
	_assert_true(shield_block_grant.is_empty(), "格挡熟练度不应因护盾完全吸收的物理命中入账。")

	var damage_grant := service.build_guard_mastery_grant_from_incoming_hit(
		attacker,
		defender,
		[physical_effect],
		{"attack_success": true, "damage": 3, "shield_absorbed": 0},
		skill_defs
	)
	_assert_eq(int(damage_grant.get("amount", 0)), 1, "格挡熟练度只应在敌方物理命中且造成 HP 伤害时入账。")


func _test_ground_weapon_attack_all_miss_is_not_cast_success() -> void:
	var gateway := RecordingCharacterGateway.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	var skill_def := _build_test_ground_weapon_attack_skill(&"test_ground_weapon_miss", "测试地面武器扫击")
	runtime.setup(gateway, {skill_def.skill_id: skill_def}, {}, {})
	runtime.configure_damage_resolver_for_tests(AlwaysMissDamageResolver.new())

	var state: BattleState = _build_flat_state(Vector2i(4, 3))
	state.phase = &"unit_acting"
	var caster := _build_manual_unit(
		&"ground_miss_user",
		"地面扫击者",
		&"player",
		Vector2i(0, 1),
		[skill_def.skill_id],
		2,
		0
	)
	caster.source_member_id = &"hero"
	var enemy_a := _build_manual_unit(&"ground_miss_enemy_a", "敌人A", &"enemy", Vector2i(2, 1), [], 2, 0)
	var enemy_b := _build_manual_unit(&"ground_miss_enemy_b", "敌人B", &"enemy", Vector2i(2, 0), [], 2, 0)
	_add_unit_to_runtime_state(runtime, state, caster, false)
	_add_unit_to_runtime_state(runtime, state, enemy_a, true)
	_add_unit_to_runtime_state(runtime, state, enemy_b, true)
	state.active_unit_id = caster.unit_id
	runtime._state = state
	runtime._initialize_battle_metrics()

	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = skill_def.skill_id
	command.target_coord = enemy_a.coord
	var hp_a_before := enemy_a.current_hp
	var hp_b_before := enemy_b.current_hp
	var batch = runtime.issue_command(command)
	var caster_metrics: Dictionary = (runtime.get_battle_metrics().get("units", {}) as Dictionary).get(String(caster.unit_id), {})
	var success_counts: Dictionary = caster_metrics.get("skill_success_counts", {})

	_assert_true(batch != null, "地面武器 miss 回归前置：issue_command 应返回 batch。")
	_assert_eq(enemy_a.current_hp, hp_a_before, "全 miss 地面武器攻击不应伤害第一个目标。")
	_assert_eq(enemy_b.current_hp, hp_b_before, "全 miss 地面武器攻击不应伤害第二个目标。")
	_assert_true(not batch.changed_unit_ids.has(enemy_a.unit_id), "miss 目标不应被计为发生单位变更。")
	_assert_true(not batch.changed_unit_ids.has(enemy_b.unit_id), "miss 目标不应被计为发生单位变更。")
	_assert_eq(int(success_counts.get(skill_def.skill_id, 0)), 0, "全 miss 地面武器攻击不应计入 cast success。")
	_assert_eq(gateway.achievement_event_count, 0, "全 miss 地面武器攻击不应推进 skill_used achievement。")
	_assert_eq(gateway.battle_mastery_count, 0, "全 miss 地面武器攻击不应授予主动技能熟练度。")


func _test_runtime_preview_and_logs_include_mitigation_sources() -> void:
	var preview_session = _create_test_session()
	if preview_session == null:
		return

	var magic_skill_def := _build_test_damage_skill(
		&"test_mitigation_source_preview",
		"测试来源预览",
		10,
		ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS,
		ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS,
		&"magic"
	)
	preview_session.get_skill_defs()[magic_skill_def.skill_id] = magic_skill_def

	var preview_facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	preview_facade.setup(preview_session)
	var preview_state: BattleState = _build_flat_state(Vector2i(3, 1))
	var preview_caster: BattleUnitState = _build_manual_unit(
		&"mitigation_preview_user",
		"来源预览施法者",
		&"player",
		Vector2i(0, 0),
		[magic_skill_def.skill_id],
		2,
		0
	)
	preview_caster.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	var preview_enemy: BattleUnitState = _build_manual_unit(
		&"mitigation_preview_enemy",
		"来源预览目标",
		&"enemy",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	preview_enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	_set_test_status(preview_enemy, &"magic_shield", {
		"damage_category": &"magic",
		"mitigation_tier": &"half",
	})
	_add_unit_to_state(preview_facade, preview_state, preview_caster, false)
	_add_unit_to_state(preview_facade, preview_state, preview_enemy, true)
	preview_state.phase = &"unit_acting"
	preview_state.active_unit_id = preview_caster.unit_id
	_apply_battle_state(preview_facade, preview_state)

	var preview_command := BATTLE_COMMAND_SCRIPT.new()
	preview_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	preview_command.unit_id = preview_caster.unit_id
	preview_command.skill_id = magic_skill_def.skill_id
	preview_command.target_unit_id = preview_enemy.unit_id
	preview_command.target_coord = preview_enemy.coord
	var preview = preview_facade.preview_battle_command(preview_command)
	_assert_true(preview != null and preview.allowed, "减伤来源预览回归前置：preview_command 应允许目标。")
	if preview != null:
		_assert_true(
			preview.log_lines.any(func(line): return String(line) == "伤害 10"),
			"preview 应只提示非暴击基础伤害，不结算 mitigation_tier 状态。 log=%s" % [str(preview.log_lines)]
		)
		_assert_true(
			not preview.log_lines.any(func(line): return String(line).contains("magic_shield") or String(line).contains("伤害减半")),
			"preview 不应提示 status mitigation 来源。 log=%s" % [str(preview.log_lines)]
		)

	_cleanup_test_session(preview_session)

	var log_session = _create_test_session()
	if log_session == null:
		return

	var death_skill_def := _build_test_damage_skill(
		&"test_mitigation_source_log",
		"测试来源日志",
		10,
		ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS,
		ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS,
		&"negative_energy"
	)
	log_session.get_skill_defs()[death_skill_def.skill_id] = death_skill_def

	var log_facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	log_facade.setup(log_session)
	var log_state: BattleState = _build_flat_state(Vector2i(3, 1))
	var log_caster: BattleUnitState = _build_manual_unit(
		&"mitigation_log_user",
		"来源日志施法者",
		&"player",
		Vector2i(0, 0),
		[death_skill_def.skill_id],
		2,
		0
	)
	log_caster.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	var log_enemy: BattleUnitState = _build_manual_unit(
		&"mitigation_log_enemy",
		"来源日志目标",
		&"enemy",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	log_enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	_set_test_status(log_enemy, &"death_ward", {
		"damage_tag": &"negative_energy",
		"mitigation_tier": &"immune",
	})
	_add_unit_to_state(log_facade, log_state, log_caster, false)
	_add_unit_to_state(log_facade, log_state, log_enemy, true)
	log_state.phase = &"unit_acting"
	log_state.active_unit_id = log_caster.unit_id
	_apply_battle_state(log_facade, log_state)

	var log_command := BATTLE_COMMAND_SCRIPT.new()
	log_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	log_command.unit_id = log_caster.unit_id
	log_command.skill_id = death_skill_def.skill_id
	log_command.target_unit_id = log_enemy.unit_id
	log_command.target_coord = log_enemy.coord
	var batch = log_facade._battle_runtime.issue_command(log_command)
	_assert_true(batch != null, "减伤来源日志回归前置：runtime.issue_command 应返回 batch。")
	_assert_eq(log_enemy.current_hp, 30, "immune 来源生效时目标 HP 不应下降。")
	_assert_true(
		batch != null and batch.log_lines.any(func(line): return String(line).contains("death_ward") and String(line).contains("免疫")),
		"runtime log 应提示免疫来源。 log=%s" % [str(batch.log_lines if batch != null else [])]
	)

	_cleanup_test_session(log_session)


func _test_facade_stamina_skill_updates_battle_state_snapshot_and_logs() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	var caster: BattleUnitState = _build_manual_unit(
		&"stamina_cost_user",
		"耐力施法者",
		&"player",
		Vector2i(0, 0),
		[&"archer_long_draw"],
		2,
		0
	)
	caster.current_stamina = 12
	caster.attribute_snapshot.set_value(&"stamina_max", 12)
	var enemy: BattleUnitState = _build_manual_unit(
		&"stamina_cost_enemy",
		"敌人",
		&"enemy",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择 stamina 技能应返回成功结果。")
	var cast_result: Dictionary = facade.command_battle_move_to(enemy.coord)
	_assert_true(bool(cast_result.get("ok", false)), "执行 stamina 技能应返回成功结果。")

	var runtime_state := facade.get_battle_state()
	var runtime_caster := runtime_state.units.get(caster.unit_id) as BattleUnitState if runtime_state != null else null
	var battle_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	var caster_snapshot := _find_battle_unit_snapshot(battle_snapshot, String(caster.unit_id))
	var text_snapshot := facade.build_text_snapshot()
	var move_log := _find_log_entry(facade.get_log_snapshot(), "battle.move_to")
	var logged_units: Array = move_log.get("context", {}).get("battle_changed_units", [])
	var logged_caster := _find_unit_entry(logged_units, String(caster.unit_id))

	_assert_true(runtime_caster != null, "stamina 回归中应能从 battle state 读取施法者单位。")
	_assert_eq(runtime_caster.current_stamina if runtime_caster != null else -1, 10, "技能释放后 battle state 应正式扣除 stamina。")
	_assert_eq(int(caster_snapshot.get("current_stamina", -1)), 10, "battle snapshot 应稳定暴露扣费后的 current_stamina。")
	_assert_eq(int(caster_snapshot.get("stamina_max", -1)), 12, "battle snapshot 应稳定暴露 stamina_max。")
	_assert_true(text_snapshot.contains("unit=stamina_cost_user |"), "battle 文本快照应渲染 stamina 施法者单位行。")
	_assert_true(text_snapshot.contains("st=10"), "battle 文本快照应渲染扣费后的 stamina。")
	_assert_true(not move_log.get("context", {}).has("after"), "战斗命令日志不应再写整包 after 快照。")
	_assert_eq(int(logged_caster.get("current_stamina", -1)), 10, "战斗命令日志 changed_units 也应暴露扣费后的 current_stamina。")

	_cleanup_test_session(game_session)


func _test_facade_aura_skill_updates_battle_state_snapshot_and_logs() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	var caster: BattleUnitState = _build_manual_unit(
		&"aura_cost_user",
		"斗气施法者",
		&"player",
		Vector2i(0, 0),
		[&"warrior_aura_slash"],
		2,
		0
	)
	caster.current_aura = 2
	caster.attribute_snapshot.set_value(&"aura_max", 2)
	var enemy: BattleUnitState = _build_manual_unit(
		&"aura_cost_enemy",
		"敌人",
		&"enemy",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择 aura 技能应返回成功结果。")
	var cast_result: Dictionary = facade.command_battle_move_to(enemy.coord)
	_assert_true(bool(cast_result.get("ok", false)), "执行 aura 技能应返回成功结果。")

	var runtime_state := facade.get_battle_state()
	var runtime_caster := runtime_state.units.get(caster.unit_id) as BattleUnitState if runtime_state != null else null
	var battle_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	var caster_snapshot := _find_battle_unit_snapshot(battle_snapshot, String(caster.unit_id))
	var text_snapshot := facade.build_text_snapshot()
	var move_log := _find_log_entry(facade.get_log_snapshot(), "battle.move_to")
	var logged_units: Array = move_log.get("context", {}).get("battle_changed_units", [])
	var logged_caster := _find_unit_entry(logged_units, String(caster.unit_id))

	_assert_true(runtime_caster != null, "aura 回归中应能从 battle state 读取施法者单位。")
	_assert_eq(runtime_caster.current_aura if runtime_caster != null else -1, 1, "技能释放后 battle state 应正式扣除 aura。")
	_assert_eq(int(caster_snapshot.get("current_aura", -1)), 1, "battle snapshot 应稳定暴露扣费后的 current_aura。")
	_assert_eq(int(caster_snapshot.get("aura_max", -1)), 2, "battle snapshot 应稳定暴露 aura_max。")
	_assert_true(text_snapshot.contains("unit=aura_cost_user |"), "battle 文本快照应渲染 aura 施法者单位行。")
	_assert_true(text_snapshot.contains("au=1/2"), "battle 文本快照应渲染扣费后的 aura。")
	_assert_true(not move_log.get("context", {}).has("after"), "战斗命令日志不应再写整包 after 快照。")
	_assert_eq(int(logged_caster.get("current_aura", -1)), 1, "战斗命令日志 changed_units 也应暴露扣费后的 current_aura。")

	_cleanup_test_session(game_session)


func _test_facade_selected_aura_skill_returns_formal_error_after_aura_drops() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	var caster: BattleUnitState = _build_manual_unit(
		&"aura_runtime_block_user",
		"Aura 运行时阻断者",
		&"player",
		Vector2i(0, 0),
		[&"warrior_aura_slash"],
		2,
		0
	)
	caster.current_aura = 1
	caster.attribute_snapshot.set_value(&"aura_max", 1)
	var enemy: BattleUnitState = _build_manual_unit(
		&"aura_runtime_block_enemy",
		"敌人",
		&"enemy",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "Aura 运行时阻断回归前置：选择技能应先成功。")
	var enemy_hp_before := enemy.current_hp
	caster.current_aura = 0
	facade.refresh_battle_selection_state()

	var cast_result: Dictionary = facade.command_battle_move_to(enemy.coord)
	_assert_true(not bool(cast_result.get("ok", true)), "Aura 在点击前耗尽时，battle.move_to 应返回正式失败。")
	_assert_true(String(cast_result.get("message", "")).contains("斗气不足"), "Aura 运行时阻断应沿正式命令结果返回明确原因。")
	_assert_eq(enemy.current_hp, enemy_hp_before, "Aura 不足导致施法失败时，不应继续结算伤害。")
	_assert_eq(caster.current_aura, 0, "Aura 不足导致施法失败时，不应继续扣费。")
	_assert_eq(String(facade.get_selected_battle_skill_id()), "warrior_aura_slash", "Aura 运行时阻断后应保留当前技能选择，等待资源恢复或手动清除。")

	_cleanup_test_session(game_session)


func _test_facade_direct_skill_issue_keeps_queued_targets_after_runtime_rejection() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	var caster: BattleUnitState = _build_manual_unit(
		&"queued_skill_user",
		"队列保留施法者",
		&"player",
		Vector2i(0, 0),
		[&"warrior_aura_slash"],
		2,
		0
	)
	caster.current_aura = 1
	caster.attribute_snapshot.set_value(&"aura_max", 1)
	var enemy: BattleUnitState = _build_manual_unit(
		&"queued_skill_enemy",
		"敌人",
		&"enemy",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "直发技能命令回归前置：选择 aura 技能应先成功。")
	facade.set_battle_selection_target_unit_ids_state([enemy.unit_id])
	facade.set_battle_selection_target_coords_state([enemy.coord])
	caster.current_aura = 0
	facade.refresh_battle_selection_state()

	var command := BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = &"warrior_aura_slash"
	command.target_unit_id = enemy.unit_id
	command.target_coord = enemy.coord
	var enemy_hp_before := enemy.current_hp
	facade.issue_battle_command(command)

	_assert_eq(enemy.current_hp, enemy_hp_before, "runtime 拒绝的直发技能命令不应继续结算伤害。")
	_assert_eq(caster.current_aura, 0, "runtime 拒绝的直发技能命令不应继续扣除 Aura。")
	_assert_eq(
		_extract_string_array(facade.get_selected_battle_skill_target_unit_ids()),
		["queued_skill_enemy"],
		"runtime 拒绝的直发技能命令不应清空 queued target unit ids。"
	)
	_assert_eq(
		_extract_vector2i_pairs(facade.get_selected_battle_skill_target_coords()),
		[[enemy.coord.x, enemy.coord.y]],
		"runtime 拒绝的直发技能命令不应清空 queued target coords。"
	)
	_assert_eq(String(facade.get_selected_battle_skill_id()), "warrior_aura_slash", "runtime 拒绝的直发技能命令后应保留当前技能选择。")
	_assert_true(String(facade.get_status_text()).contains("斗气不足"), "runtime 拒绝的直发技能命令应回写正式阻断原因。")

	_cleanup_test_session(game_session)


func _test_facade_cooldown_skill_reduces_after_battle_tick() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	state.timeline.tick_interval_seconds = 1.0
	state.timeline.tu_per_tick = 5
	var caster: BattleUnitState = _build_manual_unit(
		&"aa_cooldown_tick_user",
		"冷却施法者",
		&"player",
		Vector2i(0, 0),
		[&"archer_long_draw"],
		2,
		0
	)
	caster.action_threshold = 5
	caster.current_stamina = 12
	var enemy: BattleUnitState = _build_manual_unit(
		&"zz_cooldown_tick_enemy",
		"敌人",
		&"enemy",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	enemy.action_threshold = 5
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择 cooldown 技能应返回成功结果。")
	var cast_result: Dictionary = facade.command_battle_move_to(enemy.coord)
	_assert_true(bool(cast_result.get("ok", false)), "执行 cooldown 技能应返回成功结果。")
	_assert_eq(int(caster.cooldowns.get(&"archer_long_draw", 0)), 15, "技能释放后应写入基础 cooldown。")

	var tick_result: Dictionary = facade.command_battle_tick(1.0, 1.0)
	_assert_true(bool(tick_result.get("ok", false)), "battle tick 应能成功推进 cooldown。")

	var runtime_state := facade.get_battle_state()
	var runtime_caster := runtime_state.units.get(caster.unit_id) as BattleUnitState if runtime_state != null else null
	var battle_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	var hud: Dictionary = battle_snapshot.get("hud", {})
	var skill_slots: Array = hud.get("skill_slots", [])
	var first_slot: Dictionary = skill_slots[0] if not skill_slots.is_empty() and skill_slots[0] is Dictionary else {}

	_assert_true(runtime_caster != null, "cooldown tick 回归中应能从 battle state 读取施法者单位。")
	_assert_eq(int(runtime_state.timeline.current_tu) if runtime_state != null and runtime_state.timeline != null else -1, 5, "battle tick 后 current_tu 应按配置推进 5。")
	_assert_eq(int(runtime_caster.cooldowns.get(&"archer_long_draw", 0)) if runtime_caster != null else -1, 10, "TU 推进后的下一行动窗口应把 cooldown 正式递减为 10。")
	_assert_eq(String(runtime_state.active_unit_id) if runtime_state != null else "", String(caster.unit_id), "冷却递减后应轮到施法者重新进入行动窗口。")
	_assert_eq(int(first_slot.get("cooldown", -1)), 10, "HUD skill slot 应展示递减后的 cooldown。")
	_assert_eq(String(first_slot.get("footer_text", "")), "CD 10", "HUD skill slot footer 应同步显示新的 cooldown 文案。")
	_assert_true(bool(first_slot.get("is_disabled", false)), "冷却未结束前 HUD skill slot 应保持禁用。")

	_cleanup_test_session(game_session)


func _test_facade_auto_battle_advance_marks_overlay_refresh_for_tu_only_updates() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	state.timeline.tick_interval_seconds = 1.0
	state.timeline.tu_per_tick = 5
	var ally: BattleUnitState = _build_manual_unit(
		&"overlay_refresh_ally",
		"标题栏测试友军",
		&"player",
		Vector2i(0, 0),
		[],
		2,
		0
	)
	ally.action_threshold = 1000
	var enemy: BattleUnitState = _build_manual_unit(
		&"overlay_refresh_enemy",
		"标题栏测试敌军",
		&"enemy",
		Vector2i(2, 0),
		[],
		2,
		0
	)
	enemy.action_threshold = 1000
	_add_unit_to_state(facade, state, ally, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"timeline_running"
	state.active_unit_id = &""
	_apply_battle_state(facade, state)

	var changed := facade.advance(1.0)
	var runtime_state := facade.get_battle_state()
	var battle_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	var hud: Dictionary = battle_snapshot.get("hud", {})

	_assert_true(changed, "仅 TU 推进时 facade.advance() 也应返回 true，以驱动标题栏实时刷新。")
	_assert_eq(facade.get_last_advance_battle_refresh_mode(), "overlay", "仅 TU 推进时应建议场景层走 overlay HUD 刷新。")
	_assert_eq(int(runtime_state.timeline.current_tu) if runtime_state != null and runtime_state.timeline != null else -1, 5, "仅 TU 推进时 battle state 应正式增长 current_tu。")
	_assert_eq(String(hud.get("round_badge", "")), "TU 5\nREADY 0", "HUD round_badge 应同步反映最新的 TU。")
	_assert_eq(String(runtime_state.phase) if runtime_state != null else "", "timeline_running", "仅 TU 推进且未达到阈值时，不应误切换到 unit_acting。")

	_cleanup_test_session(game_session)


func _test_stamina_recovers_on_5tu_ticks_and_rest_doubles_progress() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	state.timeline.tick_interval_seconds = 1.0
	state.timeline.tu_per_tick = 5
	var ally: BattleUnitState = _build_manual_unit(
		&"stamina_recovery_ally",
		"体力恢复友军",
		&"player",
		Vector2i(0, 0),
		[],
		1,
		0
	)
	ally.current_stamina = 10
	ally.stamina_recovery_progress = 0
	ally.action_threshold = 1000
	ally.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, 20)
	ally.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 3)
	var enemy: BattleUnitState = _build_manual_unit(
		&"stamina_recovery_enemy",
		"体力恢复敌人",
		&"enemy",
		Vector2i(2, 0),
		[],
		1,
		0
	)
	enemy.action_threshold = 1000
	_add_unit_to_state(facade, state, ally, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"timeline_running"
	state.active_unit_id = &""
	_apply_battle_state(facade, state)

	facade.advance(1.0)
	_assert_eq(ally.current_stamina, 10, "第一个 5TU tick 只应累积 8 点体力恢复进度。")
	_assert_eq(ally.stamina_recovery_progress, 8, "体质 3 的普通恢复进度应为 5+3。")
	facade.advance(1.0)
	_assert_eq(ally.current_stamina, 11, "第二个 5TU tick 应把 16 点进度转化为 1 点体力。")
	_assert_eq(ally.stamina_recovery_progress, 6, "恢复体力后应保留 10 进制余数进度。")

	state.phase = &"unit_acting"
	state.active_unit_id = ally.unit_id
	ally.current_ap = 1
	ally.current_stamina = 10
	ally.stamina_recovery_progress = 0
	ally.has_taken_action_this_turn = false
	ally.is_resting = false
	var wait_command = BATTLE_COMMAND_SCRIPT.new()
	wait_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_WAIT
	wait_command.unit_id = ally.unit_id
	facade._battle_runtime.issue_command(wait_command)
	_assert_true(ally.is_resting, "单位直接跳过行动后应进入休息状态。")

	facade.advance(1.0)
	_assert_eq(ally.current_stamina, 11, "休息状态下单个 5TU tick 应按翻倍进度恢复 1 点体力。")
	_assert_eq(ally.stamina_recovery_progress, 6, "休息恢复应按 16 点进度转化并保留余数。")

	state.timeline.ready_unit_ids.append(ally.unit_id)
	facade.advance(0.0)
	_assert_eq(String(state.active_unit_id), String(ally.unit_id), "休息单位再次进入行动窗口时应成为当前行动单位。")
	_assert_true(ally.is_resting, "休息状态应持续到实际非等待行动，而不是在行动窗口开始时清除。")

	facade._battle_runtime.issue_command(wait_command)
	_assert_true(ally.is_resting, "连续等待不应打断休息状态。")

	state.timeline.ready_unit_ids.append(ally.unit_id)
	facade.advance(0.0)
	var move_command = BATTLE_COMMAND_SCRIPT.new()
	move_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_MOVE
	move_command.unit_id = ally.unit_id
	move_command.target_coord = Vector2i(1, 0)
	facade._battle_runtime.issue_command(move_command)
	_assert_true(not ally.is_resting, "单位执行非等待行动后应清除休息状态。")

	_cleanup_test_session(game_session)


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


func _instantiate_battle_board() -> BattleBoard2D:
	var board := BATTLE_BOARD_SCENE.instantiate() as BattleBoard2D
	root.add_child(board)
	await process_frame
	board.set_viewport_size(Vector2(1280.0, 720.0))
	return board


func _build_flat_state(map_size: Vector2i) -> BattleState:
	var state: BattleState = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"battle_skill_protocol"
	state.phase = &"timeline_running"
	state.map_size = map_size
	state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
	for y in range(map_size.y):
		for x in range(map_size.x):
			var cell = BATTLE_CELL_STATE_SCRIPT.new()
			cell.coord = Vector2i(x, y)
			cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_LAND
			cell.base_height = 4
			cell.height_offset = 0
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	state.cell_columns = BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(state.cells)
	return state


func _build_manual_unit(
	unit_id: StringName,
	display_name: String,
	faction_id: StringName,
	coord: Vector2i,
	skill_ids: Array[StringName],
	current_ap: int,
	current_mp: int
) -> BattleUnitState:
	var unit: BattleUnitState = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"manual"
	unit.current_hp = 30
	unit.current_mp = current_mp
	unit.current_ap = current_ap
	unit.current_stamina = 20
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 30)
	unit.attribute_snapshot.set_value(&"mp_max", maxi(current_mp, 6))
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 2))
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 10)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	unit.known_active_skill_ids = skill_ids.duplicate()
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 1
	return unit


func _set_test_status(unit: BattleUnitState, status_id: StringName, params: Dictionary = {}, power: int = 1) -> void:
	var status_entry = BATTLE_STATUS_EFFECT_STATE_SCRIPT.new()
	status_entry.status_id = status_id
	status_entry.source_unit_id = unit.unit_id if unit != null else &""
	status_entry.power = maxi(power, 1)
	status_entry.stacks = 1
	status_entry.duration = -1
	status_entry.params = params.duplicate(true)
	unit.set_status_effect(status_entry)


func _build_test_shield_skill(skill_id: StringName, display_name: String, shield_hp: int, duration_tu: int) -> SkillDef:
	var effect_def = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect_def.effect_type = &"shield"
	effect_def.power = shield_hp
	effect_def.duration_tu = duration_tu
	effect_def.params = {
		"shield_family": "holy_barrier",
	}

	var combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"unit"
	combat_profile.target_team_filter = &"ally"
	combat_profile.range_pattern = &"single"
	combat_profile.range_value = 1
	combat_profile.area_pattern = &"single"
	combat_profile.target_selection_mode = &"single_unit"
	combat_profile.min_target_count = 1
	combat_profile.max_target_count = 1
	combat_profile.ap_cost = 1
	var effect_defs: Array[CombatEffectDef] = []
	effect_defs.append(effect_def)
	combat_profile.effect_defs = effect_defs

	var skill_def = SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = display_name
	skill_def.skill_type = &"active"
	skill_def.combat_profile = combat_profile
	return skill_def


func _build_test_dice_shield_skill(
	skill_id: StringName,
	display_name: String,
	dice_count: int,
	dice_sides: int,
	dice_bonus: int,
	duration_tu: int
) -> SkillDef:
	var effect_def = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect_def.effect_type = &"shield"
	effect_def.duration_tu = duration_tu
	effect_def.params = {
		"dice_count": dice_count,
		"dice_sides": dice_sides,
		"dice_bonus": dice_bonus,
		"shield_family": "holy_barrier",
	}

	var combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"ground"
	combat_profile.target_team_filter = &"ally"
	combat_profile.range_pattern = &"single"
	combat_profile.range_value = 0
	combat_profile.area_pattern = &"radius"
	combat_profile.area_value = 1
	combat_profile.target_selection_mode = &"single_unit"
	combat_profile.min_target_count = 1
	combat_profile.max_target_count = 1
	combat_profile.ap_cost = 2
	combat_profile.mp_cost = 2
	var effect_defs: Array[CombatEffectDef] = []
	effect_defs.append(effect_def)
	combat_profile.effect_defs = effect_defs

	var skill_def = SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = display_name
	skill_def.skill_type = &"active"
	skill_def.combat_profile = combat_profile
	return skill_def


func _build_test_damage_skill(
	skill_id: StringName,
	display_name: String,
	power: int,
	_attack_bonus_attribute_id: StringName,
	_armor_class_attribute_id: StringName,
	damage_tag: StringName = &""
) -> SkillDef:
	var effect_def = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect_def.effect_type = &"damage"
	effect_def.power = power
	effect_def.damage_tag = damage_tag

	var combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"unit"
	combat_profile.target_team_filter = &"enemy"
	combat_profile.range_pattern = &"single"
	combat_profile.range_value = 1
	combat_profile.area_pattern = &"single"
	combat_profile.target_selection_mode = &"single_unit"
	combat_profile.min_target_count = 1
	combat_profile.max_target_count = 1
	combat_profile.ap_cost = 1
	var effect_defs: Array[CombatEffectDef] = []
	effect_defs.append(effect_def)
	combat_profile.effect_defs = effect_defs

	var skill_def = SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = display_name
	skill_def.skill_type = &"active"
	skill_def.combat_profile = combat_profile
	return skill_def


func _build_test_ground_weapon_attack_skill(skill_id: StringName, display_name: String) -> SkillDef:
	var effect_def = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect_def.effect_type = &"damage"
	effect_def.power = 0
	effect_def.damage_tag = &"physical_slash"
	effect_def.effect_target_team_filter = &"enemy"
	effect_def.params = {
		"resolve_as_weapon_attack": true,
		"add_weapon_dice": true,
		"use_weapon_physical_damage_tag": true,
	}

	var combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"ground"
	combat_profile.target_team_filter = &"enemy"
	combat_profile.range_pattern = &"single"
	combat_profile.range_value = 4
	combat_profile.area_pattern = &"diamond"
	combat_profile.area_value = 1
	combat_profile.target_selection_mode = &"single_coord"
	combat_profile.min_target_count = 1
	combat_profile.max_target_count = 1
	combat_profile.ap_cost = 1
	var effect_defs: Array[CombatEffectDef] = []
	effect_defs.append(effect_def)
	combat_profile.effect_defs = effect_defs
	combat_profile.mastery_trigger_mode = &"weapon_attack_quality"
	combat_profile.mastery_amount_mode = &"per_target_rank"

	var skill_def = SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = display_name
	skill_def.skill_type = &"active"
	skill_def.combat_profile = combat_profile
	return skill_def


func _add_unit_to_state(facade, state: BattleState, unit: BattleUnitState, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	var placed: bool = bool(facade._battle_runtime._grid_service.place_unit(state, unit, unit.coord, true))
	_assert_true(placed, "测试单位 %s 应能成功放入战场。" % String(unit.unit_id))


func _add_unit_to_runtime_state(runtime, state: BattleState, unit: BattleUnitState, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	var placed: bool = bool(runtime._grid_service.place_unit(state, unit, unit.coord, true))
	_assert_true(placed, "测试单位 %s 应能成功放入战场。" % String(unit.unit_id))


func _apply_battle_state(facade, state: BattleState) -> void:
	facade._battle_runtime._state = state
	facade._battle_state = state
	facade._battle_selected_coord = Vector2i(-1, -1)
	facade._refresh_battle_runtime_state()


func _extract_string_array(values: Array) -> Array[String]:
	var result: Array[String] = []
	for value in values:
		result.append(String(value))
	return result


func _extract_coord_pairs(coord_dicts: Array) -> Array:
	var pairs: Array = []
	for coord_variant in coord_dicts:
		if coord_variant is not Dictionary:
			continue
		var coord: Dictionary = coord_variant
		pairs.append([int(coord.get("x", 0)), int(coord.get("y", 0))])
	return pairs


func _extract_vector2i_pairs(coords: Array) -> Array:
	var pairs: Array = []
	for coord_variant in coords:
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		pairs.append([coord.x, coord.y])
	return pairs


func _extract_unit_ids_from_entries(unit_entries_variant) -> Array[String]:
	var unit_ids: Array[String] = []
	if unit_entries_variant is not Array:
		return unit_ids
	for unit_entry_variant in unit_entries_variant:
		if unit_entry_variant is not Dictionary:
			continue
		var unit_entry := unit_entry_variant as Dictionary
		unit_ids.append(String(unit_entry.get("unit_id", "")))
	return unit_ids


func _fixed_sources_include(sources_variant, status_id: String, source_type: String) -> bool:
	if sources_variant is not Array:
		return false
	for source_variant in sources_variant:
		if source_variant is not Dictionary:
			continue
		var source := source_variant as Dictionary
		if String(source.get("status_id", "")) == status_id and String(source.get("type", "")) == source_type:
			return true
	return false


func _collect_marker_used_coords(board: BattleBoard2D) -> Array[Vector2i]:
	var coord_set: Dictionary = {}
	if board == null:
		return []
	for layer in board.marker_layers:
		if layer == null:
			continue
		for coord in layer.get_used_cells():
			coord_set[coord] = true
	var coords: Array[Vector2i] = []
	for coord_variant in coord_set.keys():
		if coord_variant is Vector2i:
			coords.append(coord_variant)
	coords.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		if a.y == b.y:
			return a.x < b.x
		return a.y < b.y
	)
	return coords


func _find_battle_unit_snapshot(battle_snapshot: Dictionary, unit_id: String) -> Dictionary:
	return _find_unit_entry(battle_snapshot.get("units", []), unit_id)


func _find_unit_entry(unit_variants: Variant, unit_id: String) -> Dictionary:
	if unit_variants is not Array:
		return {}
	for unit_variant in unit_variants:
		if unit_variant is not Dictionary:
			continue
		var unit_entry: Dictionary = unit_variant
		if String(unit_entry.get("unit_id", "")) == unit_id:
			return unit_entry.duplicate(true)
	return {}


func _find_log_entry(log_snapshot: Dictionary, event_id: String) -> Dictionary:
	var entries: Array = log_snapshot.get("entries", [])
	for entry_variant in entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("event_id", "")) == event_id:
			return entry.duplicate(true)
	return {}


func _find_last_log_entry(log_snapshot: Dictionary, event_id: String) -> Dictionary:
	var entries: Array = log_snapshot.get("entries", [])
	for index in range(entries.size() - 1, -1, -1):
		var entry_variant = entries[index]
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("event_id", "")) == event_id:
			return entry.duplicate(true)
	return {}


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
