extends SceneTree

const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BATTLE_REPORT_FORMATTER_SCRIPT = preload("res://scripts/systems/battle_report_formatter.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle_runtime_module.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle_status_effect_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const GameRuntimeBattleSelection = preload("res://scripts/systems/game_runtime_battle_selection.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const DOOM_SENTENCE_SKILL_ID: StringName = &"doom_sentence"
const STATUS_DOOM_SENTENCE_VERDICT: StringName = &"doom_sentence_verdict"
const STATUS_PINNED: StringName = &"pinned"
const STATUS_SLOW: StringName = &"slow"
const WARRIOR_HEAVY_STRIKE_SKILL_ID: StringName = &"warrior_heavy_strike"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"

var _failures: Array[String] = []


class SelectionRuntimeProxy:
	extends RefCounted

	var runtime: BattleRuntimeModule = null
	var skill_defs: Dictionary = {}
	var selected_skill_id: StringName = &""
	var selected_skill_variant_id: StringName = &""
	var last_manual_unit_id: StringName = &""
	var target_coords_state: Array[Vector2i] = []
	var target_unit_ids_state: Array[StringName] = []
	var selected_coord: Vector2i = Vector2i(-1, -1)
	var status_text := ""


	func _init(battle_runtime: BattleRuntimeModule, battle_skill_defs: Dictionary) -> void:
		runtime = battle_runtime
		skill_defs = battle_skill_defs if battle_skill_defs != null else {}


	func get_manual_battle_unit() -> BattleUnitState:
		var active_unit := get_runtime_battle_active_unit()
		return active_unit if active_unit != null and active_unit.control_mode == &"manual" else null


	func get_runtime_battle_active_unit() -> BattleUnitState:
		var state := get_battle_state()
		if state == null:
			return null
		return state.units.get(state.active_unit_id) as BattleUnitState


	func get_runtime_battle_unit_at_coord(coord: Vector2i) -> BattleUnitState:
		if runtime == null:
			return null
		return runtime.get_grid_service().get_unit_at_coord(runtime.get_state(), coord)


	func get_runtime_battle_unit_by_id(unit_id: StringName) -> BattleUnitState:
		var state := get_battle_state()
		if state == null:
			return null
		return state.units.get(unit_id) as BattleUnitState


	func get_battle_state() -> BattleState:
		return runtime.get_state() if runtime != null else null


	func get_battle_grid_service():
		return runtime.get_grid_service() if runtime != null else null


	func preview_battle_command(command):
		return runtime.preview_command(command) if runtime != null else null


	func issue_battle_command(command) -> StringName:
		if runtime != null:
			runtime.issue_command(command)
		return &"full"


	func refresh_battle_selection_state() -> void:
		pass


	func update_status(message: String) -> void:
		status_text = message


	func format_coord(coord: Vector2i) -> String:
		return "(%d,%d)" % [coord.x, coord.y]


	func is_battle_active() -> bool:
		return runtime != null and runtime.is_battle_active()


	func get_selected_battle_skill_id() -> StringName:
		return selected_skill_id


	func set_battle_selection_skill_id(skill_id: StringName) -> void:
		selected_skill_id = skill_id


	func get_selected_battle_skill_variant_id() -> StringName:
		return selected_skill_variant_id


	func set_battle_selection_skill_variant_id(variant_id: StringName) -> void:
		selected_skill_variant_id = variant_id


	func get_battle_selection_last_manual_unit_id() -> StringName:
		return last_manual_unit_id


	func set_battle_selection_last_manual_unit_id(unit_id: StringName) -> void:
		last_manual_unit_id = unit_id


	func get_battle_selection_target_coords_state() -> Array[Vector2i]:
		return target_coords_state.duplicate()


	func set_battle_selection_target_coords_state(target_coords: Array[Vector2i]) -> void:
		target_coords_state = target_coords.duplicate()


	func get_battle_selection_target_unit_ids_state() -> Array[StringName]:
		return target_unit_ids_state.duplicate()


	func set_battle_selection_target_unit_ids_state(target_unit_ids: Array[StringName]) -> void:
		target_unit_ids_state = target_unit_ids.duplicate()


	func set_runtime_battle_selected_coord(coord: Vector2i) -> void:
		selected_coord = coord


	func get_skill_defs() -> Dictionary:
		return skill_defs


	func get_battle_skill_cast_block_reason(active_unit: BattleUnitState, skill_def) -> String:
		return runtime.get_skill_cast_block_reason(active_unit, skill_def) if runtime != null else ""


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_doom_sentence_applies_verdict_and_teamwide_damage_amp()
	_test_doom_sentence_locks_main_skill_only_after_two_other_debuffs()
	_test_doom_sentence_is_limited_to_once_per_battle()
	_test_doom_sentence_is_not_selectable_when_calamity_cap_cannot_pay_cost()

	if _failures.is_empty():
		print("Doom sentence regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Doom sentence regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_doom_sentence_applies_verdict_and_teamwide_damage_amp() -> void:
	var runtime := _build_runtime()

	var state := _build_skill_test_state(&"doom_sentence_success", Vector2i(7, 3))
	var caster := _build_unit(&"doom_sentence_caster", "黑冕使徒", &"player", Vector2i(1, 1), 3, &"hero")
	_enable_doom_sentence_cap(caster)
	caster.known_active_skill_ids = [DOOM_SENTENCE_SKILL_ID]
	caster.known_skill_level_map = {DOOM_SENTENCE_SKILL_ID: 1}
	var ally_attacker := _build_unit(&"doom_sentence_ally", "协同输出", &"player", Vector2i(1, 2), 2)
	var boss := _build_unit(&"doom_sentence_boss", "章末 Boss", &"enemy", Vector2i(2, 1), 2, &"", true)
	boss.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 25)
	boss.known_active_skill_ids = [WARRIOR_HEAVY_STRIKE_SKILL_ID]
	boss.known_skill_level_map = {WARRIOR_HEAVY_STRIKE_SKILL_ID: 1}
	var ally_target := _build_unit(&"doom_sentence_victim", "被打击者", &"player", Vector2i(3, 1), 2)

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, ally_attacker)
	_add_unit(runtime, state, boss)
	_add_unit(runtime, state, ally_target)
	state.ally_unit_ids = [caster.unit_id, ally_attacker.unit_id, ally_target.unit_id]
	state.enemy_unit_ids = [boss.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)
	runtime.calamity_by_member_id[&"hero"] = 5

	var baseline_damage_result: Dictionary = runtime.get_damage_resolver().resolve_effects(
		ally_attacker,
		BattleUnitState.from_dict(boss.to_dict()),
		[_build_damage_effect()]
	)
	var command := _build_unit_skill_command(caster.unit_id, DOOM_SENTENCE_SKILL_ID, boss)
	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "满足条件时，厄命宣判预览应允许。")
	var batch := runtime.issue_command(command)
	_assert_true(boss.has_status_effect(STATUS_DOOM_SENTENCE_VERDICT), "厄命宣判成功后应写入 doom_sentence_verdict。")
	_assert_eq(runtime.get_member_calamity(&"hero"), 0, "厄命宣判成功施放后应扣除 5 点 calamity。")
	_assert_true(runtime.is_unit_counterattack_locked(boss), "厄命宣判应封锁目标反击。")
	_assert_true(
		batch != null and batch.log_lines.any(func(line): return String(line).contains("doom_sentence_verdict")),
		"厄命宣判成功后应在 battle log 中回传状态写入。 log=%s" % [str(batch.log_lines if batch != null else [])]
	)
	_assert_true(
		batch != null and _has_report_entry_with_tag(
			batch.report_entries if batch != null else [],
			BATTLE_REPORT_FORMATTER_SCRIPT.REASON_DOOM_SENTENCE_APPLIED,
			BATTLE_REPORT_FORMATTER_SCRIPT.TAG_DOOM_SENTENCE
		),
		"厄命宣判成功后应补出带 doom_sentence 标签的结构化战报条目。 reports=%s" % [str(batch.report_entries if batch != null else [])]
	)

	var amplified_damage_result: Dictionary = runtime.get_damage_resolver().resolve_effects(
		ally_attacker,
		BattleUnitState.from_dict(boss.to_dict()),
		[_build_damage_effect()]
	)
	_assert_true(
		int(amplified_damage_result.get("damage", 0)) > int(baseline_damage_result.get("damage", 0)),
		"厄命宣判应令全队对目标造成更高伤害。 baseline=%s amplified=%s" % [
			str(baseline_damage_result),
			str(amplified_damage_result),
		]
	)


func _test_doom_sentence_locks_main_skill_only_after_two_other_debuffs() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(&"doom_sentence_main_skill_lock", Vector2i(7, 3))
	var caster := _build_unit(&"doom_sentence_lock_caster", "黑冕使徒", &"player", Vector2i(1, 1), 3, &"hero")
	_enable_doom_sentence_cap(caster)
	caster.known_active_skill_ids = [DOOM_SENTENCE_SKILL_ID]
	caster.known_skill_level_map = {DOOM_SENTENCE_SKILL_ID: 1}
	var boss := _build_unit(&"doom_sentence_lock_target", "受宣判精英", &"enemy", Vector2i(2, 1), 2, &"", true)
	boss.control_mode = &"manual"
	boss.known_active_skill_ids = [WARRIOR_HEAVY_STRIKE_SKILL_ID]
	boss.known_skill_level_map = {WARRIOR_HEAVY_STRIKE_SKILL_ID: 1}
	var ally_target := _build_unit(&"doom_sentence_lock_ally", "我方目标", &"player", Vector2i(3, 1), 2)

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, boss)
	_add_unit(runtime, state, ally_target)
	state.ally_unit_ids = [caster.unit_id, ally_target.unit_id]
	state.enemy_unit_ids = [boss.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)
	runtime.calamity_by_member_id[&"hero"] = 5
	runtime.issue_command(_build_unit_skill_command(caster.unit_id, DOOM_SENTENCE_SKILL_ID, boss))

	state.active_unit_id = boss.unit_id
	state.phase = &"unit_acting"
	boss.current_ap = 2
	_set_status(boss, STATUS_SLOW, 60, caster.unit_id)
	var main_skill_command := _build_unit_skill_command(boss.unit_id, WARRIOR_HEAVY_STRIKE_SKILL_ID, ally_target)
	var one_debuff_preview := runtime.preview_command(main_skill_command)
	_assert_true(one_debuff_preview != null and one_debuff_preview.allowed, "只有 1 个其他 debuff 时，主技能仍应允许。")

	_set_status(boss, STATUS_PINNED, 60, caster.unit_id)
	var blocked_preview := runtime.preview_command(main_skill_command)
	_assert_true(blocked_preview != null and not blocked_preview.allowed, "累计到 2 个其他 debuff 后，主技能应被宣判封锁。")
	_assert_true(
		blocked_preview != null and blocked_preview.log_lines.any(func(line): return String(line).contains("主技能")),
		"主技能被封锁时，preview 应明确说明原因。 log=%s" % [str(blocked_preview.log_lines if blocked_preview != null else [])]
	)

	var ap_before_issue := boss.current_ap
	var blocked_batch := runtime.issue_command(main_skill_command)
	_assert_eq(boss.current_ap, ap_before_issue, "主技能被厄命宣判封锁时不应扣除 AP。")
	_assert_true(
		blocked_batch != null and blocked_batch.log_lines.any(func(line): return String(line).contains("主技能")),
		"主技能被封锁时，issue 应沿用 preview 的阻断原因。 log=%s" % [str(blocked_batch.log_lines if blocked_batch != null else [])]
	)


func _test_doom_sentence_is_limited_to_once_per_battle() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(&"doom_sentence_once_per_battle", Vector2i(8, 3))
	var caster := _build_unit(&"doom_sentence_once_caster", "黑冕使徒", &"player", Vector2i(1, 1), 3, &"hero")
	_enable_doom_sentence_cap(caster)
	caster.control_mode = &"manual"
	caster.known_active_skill_ids = [DOOM_SENTENCE_SKILL_ID]
	caster.known_skill_level_map = {DOOM_SENTENCE_SKILL_ID: 1}
	var first_elite := _build_unit(&"doom_sentence_once_target_a", "首个精英", &"enemy", Vector2i(2, 1), 2, &"", true)
	var second_elite := _build_unit(&"doom_sentence_once_target_b", "第二个精英", &"enemy", Vector2i(4, 1), 2, &"", true)

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, first_elite)
	_add_unit(runtime, state, second_elite)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [first_elite.unit_id, second_elite.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)
	runtime.calamity_by_member_id[&"hero"] = 10

	runtime.issue_command(_build_unit_skill_command(caster.unit_id, DOOM_SENTENCE_SKILL_ID, first_elite))
	_assert_true(first_elite.has_status_effect(STATUS_DOOM_SENTENCE_VERDICT), "首次施放后应成功命中首个精英。")

	var proxy := SelectionRuntimeProxy.new(runtime, runtime.get_skill_defs())
	var selection := GameRuntimeBattleSelection.new()
	selection.setup(proxy)
	var selection_result := selection.select_battle_skill_slot(0)
	_assert_true(not bool(selection_result.get("ok", false)), "每战 1 次用尽后，技能栏应拒绝再次选中厄命宣判。")
	_assert_true(
		String(selection_result.get("message", "")).contains("每战只能施放 1 次"),
		"再次选中厄命宣判时应说明每战 1 次限制。 message=%s" % [str(selection_result)]
	)

	caster.current_ap = 3
	var second_command := _build_unit_skill_command(caster.unit_id, DOOM_SENTENCE_SKILL_ID, second_elite)
	var blocked_preview := runtime.preview_command(second_command)
	_assert_true(blocked_preview != null and not blocked_preview.allowed, "同战第二次施放厄命宣判应被 preview 拒绝。")
	var calamity_before_issue := runtime.get_member_calamity(&"hero")
	var ap_before_issue := caster.current_ap
	var blocked_batch := runtime.issue_command(second_command)
	_assert_eq(runtime.get_member_calamity(&"hero"), calamity_before_issue, "二次施放被拒绝时不应再扣 calamity。")
	_assert_eq(caster.current_ap, ap_before_issue, "二次施放被拒绝时不应再扣 AP。")
	_assert_true(not second_elite.has_status_effect(STATUS_DOOM_SENTENCE_VERDICT), "二次施放被拒绝后不应污染第二个目标状态。")
	_assert_true(
		blocked_batch != null and blocked_batch.log_lines.any(func(line): return String(line).contains("每战只能施放 1 次")),
		"二次施放被拒绝时应回传每战 1 次说明。 log=%s" % [str(blocked_batch.log_lines if blocked_batch != null else [])]
	)


func _test_doom_sentence_is_not_selectable_when_calamity_cap_cannot_pay_cost() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(&"doom_sentence_cap_block", Vector2i(6, 3))
	var caster := _build_unit(&"doom_sentence_cap_caster", "黑冕新信徒", &"player", Vector2i(1, 1), 3, &"hero")
	caster.control_mode = &"manual"
	caster.known_active_skill_ids = [DOOM_SENTENCE_SKILL_ID]
	caster.known_skill_level_map = {DOOM_SENTENCE_SKILL_ID: 1}
	var elite := _build_unit(&"doom_sentence_cap_target", "精英目标", &"enemy", Vector2i(2, 1), 2, &"", true)

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, elite)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [elite.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)
	runtime.calamity_by_member_id[&"hero"] = 3

	var proxy := SelectionRuntimeProxy.new(runtime, runtime.get_skill_defs())
	var selection := GameRuntimeBattleSelection.new()
	selection.setup(proxy)
	var selection_result := selection.select_battle_skill_slot(0)
	_assert_true(not bool(selection_result.get("ok", false)), "当本战 calamity 上限小于 5 时，厄命宣判不应进入选中态。")
	_assert_true(
		String(selection_result.get("message", "")).contains("上限不足 5"),
		"skill slot 兜底应说明 calamity 上限不足。 message=%s" % [str(selection_result)]
	)

	var command := _build_unit_skill_command(caster.unit_id, DOOM_SENTENCE_SKILL_ID, elite)
	var blocked_preview := runtime.preview_command(command)
	_assert_true(blocked_preview != null and not blocked_preview.allowed, "当本战 calamity 上限不足时，preview 应拒绝厄命宣判。")
	_assert_true(
		blocked_preview != null and blocked_preview.log_lines.any(func(line): return String(line).contains("上限不足 5")),
		"preview 拒绝时应保留上限不足说明。 log=%s" % [str(blocked_preview.log_lines if blocked_preview != null else [])]
	)

	var ap_before_issue := caster.current_ap
	var blocked_batch := runtime.issue_command(command)
	_assert_eq(caster.current_ap, ap_before_issue, "cap 不足时 issue 不应扣除 AP。")
	_assert_true(not elite.has_status_effect(STATUS_DOOM_SENTENCE_VERDICT), "cap 不足时目标不应获得厄命宣判。")
	_assert_true(
		blocked_batch != null and blocked_batch.log_lines.any(func(line): return String(line).contains("上限不足 5")),
		"issue 拒绝时应沿用上限不足说明。 log=%s" % [str(blocked_batch.log_lines if blocked_batch != null else [])]
	)


func _build_runtime() -> BattleRuntimeModule:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	return runtime


func _begin_runtime_battle(runtime: BattleRuntimeModule) -> void:
	if runtime == null:
		return
	runtime.calamity_by_member_id.clear()
	runtime._misfortune_service.begin_battle(runtime.calamity_by_member_id)


func _build_skill_test_state(battle_id: StringName, map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = battle_id
	state.phase = &"unit_acting"
	state.map_size = map_size
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _build_cell(coord: Vector2i) -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = coord
	cell.base_terrain = BattleCellState.TERRAIN_LAND
	cell.base_height = 4
	cell.height_offset = 0
	cell.recalculate_runtime_values()
	return cell


func _build_unit(
	unit_id: StringName,
	display_name: String,
	faction_id: StringName,
	coord: Vector2i,
	current_ap: int,
	source_member_id: StringName = &"",
	is_elite_or_boss := false
) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.source_member_id = source_member_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"manual"
	unit.current_ap = current_ap
	unit.current_hp = 60
	unit.current_mp = 4
	unit.current_stamina = 4
	unit.current_aura = 0
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 60)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX, 2)
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 1))
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 6)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 60)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH, 0)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.FAITH_LUCK_BONUS, 0)
	unit.attribute_snapshot.set_value(FORTUNE_MARK_TARGET_STAT_ID, 1 if is_elite_or_boss else 0)
	return unit


func _build_unit_skill_command(unit_id: StringName, skill_id: StringName, target_unit: BattleUnitState) -> BattleCommand:
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = unit_id
	command.skill_id = skill_id
	command.target_unit_id = target_unit.unit_id if target_unit != null else &""
	command.target_coord = target_unit.coord if target_unit != null else Vector2i(-1, -1)
	return command


func _build_damage_effect() -> CombatEffectDef:
	var effect := CombatEffectDef.new()
	effect.effect_type = &"damage"
	effect.power = 12
	return effect


func _enable_doom_sentence_cap(unit_state: BattleUnitState) -> void:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return
	unit_state.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH, -5)
	unit_state.attribute_snapshot.set_value(&"calamity_capacity_bonus", 2)


func _set_status(
	unit_state: BattleUnitState,
	status_id: StringName,
	duration_tu: int,
	source_unit_id: StringName = &"",
	power: int = 1
) -> void:
	if unit_state == null or status_id == &"":
		return
	var status_entry := BattleStatusEffectState.new()
	status_entry.status_id = status_id
	status_entry.source_unit_id = source_unit_id
	status_entry.power = maxi(power, 1)
	status_entry.stacks = 1
	status_entry.duration = duration_tu
	unit_state.set_status_effect(status_entry)


func _add_unit(runtime: BattleRuntimeModule, state: BattleState, unit: BattleUnitState) -> void:
	state.units[unit.unit_id] = unit
	runtime._grid_service.place_unit(state, unit, unit.coord, true)


func _has_report_entry_with_tag(entries_variant, reason_id: StringName, event_tag: StringName) -> bool:
	if entries_variant is not Array:
		return false
	for entry_variant in entries_variant:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("reason_id", "")) != String(reason_id):
			continue
		var event_tags = entry.get("event_tags", [])
		if event_tags is Array and event_tags.has(String(event_tag)):
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
