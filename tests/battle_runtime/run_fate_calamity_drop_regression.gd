extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle_resolution_result.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle_runtime_module.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle_unit_state.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle_status_effect_state.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const WAREHOUSE_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_state.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"
const DROP_TYPE_ITEM: StringName = &"item"
const DROP_SOURCE_KIND_CALAMITY_CONVERSION: StringName = &"calamity_conversion"
const DROP_SOURCE_KIND_FATE_STATUS_DROP: StringName = &"fate_status_drop"
const DROP_SOURCE_ID_ORDINARY_BATTLE: StringName = &"ordinary_battle"
const DROP_SOURCE_ID_ELITE_BOSS_BATTLE: StringName = &"elite_boss_battle"
const CALAMITY_SHARD_ITEM_ID: StringName = &"calamity_shard"
const BLACK_CROWN_CORE_ITEM_ID: StringName = &"black_crown_core"
const STATUS_BLACK_STAR_BRAND_ELITE: StringName = &"black_star_brand_elite"
const STATUS_DOOM_SENTENCE_VERDICT: StringName = &"doom_sentence_verdict"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"
const BOSS_TARGET_STAT_ID: StringName = &"boss_target"
const ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP := 4
const CALAMITY_SHARD_CHAPTER_FLAG_PREFIX := "calamity_shard_chapter_slot_"

var _failures: Array[String] = []


class _FakeBattleGateway:
	extends RefCounted

	func record_achievement_event(
		_member_id: StringName,
		_event_type: StringName,
		_amount: int = 1,
		_subject_id: StringName = &"",
		_meta: Dictionary = {}
	) -> Array[StringName]:
		return []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_ordinary_battle_calamity_conversion_respects_chapter_cap()
	_test_elite_boss_loot_paths_bypass_ordinary_chapter_cap()
	_test_branded_elite_grants_fixed_calamity_shard()
	_test_doom_sentence_boss_defeat_returns_calamity_and_core()
	if _failures.is_empty():
		print("Fate calamity drop regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Fate calamity drop regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_ordinary_battle_calamity_conversion_respects_chapter_cap() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var party_state = facade.get_party_state()
	_reset_party_warehouse(party_state)
	_ensure_capacity(party_state, 10)
	_seed_regular_battle_shard_flags(party_state, 2)

	var resolution_result = BATTLE_RESOLUTION_RESULT_SCRIPT.new()
	resolution_result.winner_faction_id = &"player"
	resolution_result.set_loot_entries([
		_build_loot_entry(
			DROP_SOURCE_KIND_CALAMITY_CONVERSION,
			DROP_SOURCE_ID_ORDINARY_BATTLE,
			"ordinary_conversion",
			CALAMITY_SHARD_ITEM_ID,
			3
		),
	])

	var commit_result: Dictionary = facade._commit_battle_loot_to_shared_warehouse(resolution_result)
	_assert_true(bool(commit_result.get("ok", false)), "普通战 calamity 结算应能正常提交。")
	_assert_eq(int(commit_result.get("committed_item_count", -1)), 2, "章节内已拿 2 个碎片后，普通战结算最多还能提交 2 个。")
	_assert_eq(_count_stack_quantity(party_state, CALAMITY_SHARD_ITEM_ID), 2, "普通战结算应只向仓库写入剩余额度内的碎片。")
	_assert_eq(_get_regular_battle_shard_flag_count(party_state), 4, "普通战结算成功后，应补齐本章 4 个碎片上限标记。")
	_assert_eq(
		_count_matching_loot_quantity(resolution_result.loot_entries, CALAMITY_SHARD_ITEM_ID, DROP_SOURCE_KIND_CALAMITY_CONVERSION, DROP_SOURCE_ID_ORDINARY_BATTLE),
		2,
		"结算结果中的普通战碎片数量应在提交前被裁切到章节剩余额度。"
	)
	_cleanup_test_session(game_session)


func _test_elite_boss_loot_paths_bypass_ordinary_chapter_cap() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var party_state = facade.get_party_state()
	_reset_party_warehouse(party_state)
	_ensure_capacity(party_state, 16)
	_seed_regular_battle_shard_flags(party_state, ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP)

	var resolution_result = BATTLE_RESOLUTION_RESULT_SCRIPT.new()
	resolution_result.winner_faction_id = &"player"
	resolution_result.set_loot_entries([
		_build_loot_entry(
			DROP_SOURCE_KIND_CALAMITY_CONVERSION,
			DROP_SOURCE_ID_ELITE_BOSS_BATTLE,
			"elite_boss_conversion",
			CALAMITY_SHARD_ITEM_ID,
			6
		),
		_build_loot_entry(
			DROP_SOURCE_KIND_FATE_STATUS_DROP,
			&"elite_target",
			"elite_fixed_shard",
			CALAMITY_SHARD_ITEM_ID,
			1
		),
	])

	var commit_result: Dictionary = facade._commit_battle_loot_to_shared_warehouse(resolution_result)
	_assert_true(bool(commit_result.get("ok", false)), "elite/boss 旁路掉落应能正常提交。")
	_assert_eq(int(commit_result.get("committed_item_count", -1)), 7, "elite/boss 战结算与固定状态掉落不应受到普通战章节上限影响。")
	_assert_eq(_count_stack_quantity(party_state, CALAMITY_SHARD_ITEM_ID), 7, "elite/boss 旁路路径应完整写入全部碎片。")
	_assert_eq(_get_regular_battle_shard_flag_count(party_state), 4, "elite/boss 旁路路径不应污染普通战章节上限标记。")
	_cleanup_test_session(game_session)


func _test_branded_elite_grants_fixed_calamity_shard() -> void:
	var runtime = _build_runtime()
	var state = _build_finished_battle_state(&"brand_elite_resolution")
	var elite = _build_enemy_unit(&"brand_elite_target", "被烙印精英", true, false)
	_set_status(elite, STATUS_BLACK_STAR_BRAND_ELITE, &"hero")
	elite.is_alive = false
	elite.current_hp = 0
	state.units[elite.unit_id] = elite
	state.enemy_unit_ids.append(elite.unit_id)
	runtime._state = state

	var result = runtime._build_battle_resolution_result()
	_assert_eq(
		_count_matching_loot_quantity(result.loot_entries, CALAMITY_SHARD_ITEM_ID, DROP_SOURCE_KIND_FATE_STATUS_DROP, &"brand_elite_target"),
		1,
		"被黑星烙印终结的 elite 应固定掉落 1 个 calamity_shard。"
	)


func _test_doom_sentence_boss_defeat_returns_calamity_and_core() -> void:
	var runtime = _build_runtime()
	var state = _build_finished_battle_state(&"doom_sentence_boss_resolution")
	var boss = _build_enemy_unit(&"doom_boss_target", "章末 Boss", true, true)
	_set_status(boss, STATUS_DOOM_SENTENCE_VERDICT, &"hero")
	boss.is_alive = false
	boss.current_hp = 0
	state.units[boss.unit_id] = boss
	state.enemy_unit_ids.append(boss.unit_id)
	runtime._state = state

	var result = runtime._build_battle_resolution_result()
	_assert_eq(
		int(result.party_resource_commit.get("returned_calamity", 0)),
		5,
		"boss 在厄命宣判下死亡时应返还 5 点 calamity，用于后续碎片结算。"
	)
	_assert_eq(
		_count_matching_loot_quantity(result.loot_entries, BLACK_CROWN_CORE_ITEM_ID, DROP_SOURCE_KIND_FATE_STATUS_DROP, &"doom_boss_target"),
		1,
		"boss 在厄命宣判下死亡时应固定掉落 1 个 black_crown_core。"
	)
	_assert_eq(
		_count_matching_loot_quantity(result.loot_entries, CALAMITY_SHARD_ITEM_ID, DROP_SOURCE_KIND_CALAMITY_CONVERSION, DROP_SOURCE_ID_ELITE_BOSS_BATTLE),
		2,
		"宣判击杀返还的 calamity 应在战后折算为 2 个 calamity_shard。"
	)


func _build_runtime():
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(_FakeBattleGateway.new(), {}, {}, {}, null)
	return runtime


func _build_finished_battle_state(battle_id: StringName):
	var state = BATTLE_STATE_SCRIPT.new()
	state.battle_id = battle_id
	state.winner_faction_id = &"player"
	state.phase = &"battle_ended"
	return state


func _build_enemy_unit(
	unit_id: StringName,
	display_name: String,
	is_elite_or_boss: bool,
	is_boss: bool
):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = &"enemy"
	unit.current_hp = 60
	unit.is_alive = true
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 60)
	unit.attribute_snapshot.set_value(FORTUNE_MARK_TARGET_STAT_ID, 1 if is_elite_or_boss else 0)
	unit.attribute_snapshot.set_value(BOSS_TARGET_STAT_ID, 1 if is_boss else 0)
	return unit


func _set_status(unit_state, status_id: StringName, source_unit_id: StringName = &"") -> void:
	if unit_state == null or status_id == &"":
		return
	var status_entry = BATTLE_STATUS_EFFECT_STATE_SCRIPT.new()
	status_entry.status_id = status_id
	status_entry.source_unit_id = source_unit_id
	status_entry.power = 1
	status_entry.stacks = 1
	status_entry.duration = 60
	unit_state.set_status_effect(status_entry)


func _create_test_session():
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "GameSession 应能为灾厄掉落回归创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return null
	return game_session


func _cleanup_test_session(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _reset_party_warehouse(party_state) -> void:
	if party_state == null:
		return
	party_state.warehouse_state = WAREHOUSE_STATE_SCRIPT.new()


func _ensure_capacity(party_state, storage_space: int) -> void:
	if party_state == null:
		return
	for member_variant in party_state.member_states.values():
		var member_state = member_variant
		if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
			continue
		member_state.progression.unit_base_attributes.custom_stats[&"storage_space"] = maxi(storage_space, 0)
		return


func _seed_regular_battle_shard_flags(party_state, count: int) -> void:
	if party_state == null:
		return
	for slot_index in range(ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP):
		party_state.clear_fate_run_flag(_build_regular_battle_shard_flag_id(slot_index))
	for slot_index in range(mini(maxi(count, 0), ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP)):
		party_state.set_fate_run_flag(_build_regular_battle_shard_flag_id(slot_index), true)


func _get_regular_battle_shard_flag_count(party_state) -> int:
	if party_state == null:
		return 0
	var flag_count := 0
	for slot_index in range(ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP):
		if party_state.get_fate_run_flag(_build_regular_battle_shard_flag_id(slot_index), false):
			flag_count += 1
	return flag_count


func _build_regular_battle_shard_flag_id(slot_index: int) -> StringName:
	return StringName("%s%d" % [CALAMITY_SHARD_CHAPTER_FLAG_PREFIX, maxi(slot_index, 0)])


func _count_stack_quantity(party_state, item_id: StringName) -> int:
	if party_state == null or party_state.warehouse_state == null:
		return 0
	var total_quantity := 0
	for stack in party_state.warehouse_state.stacks:
		if stack == null or stack.item_id != item_id:
			continue
		total_quantity += int(stack.quantity)
	return total_quantity


func _build_loot_entry(
	drop_source_kind: StringName,
	drop_source_id: StringName,
	drop_entry_id: String,
	item_id: StringName,
	quantity: int
) -> Dictionary:
	return {
		"drop_type": String(DROP_TYPE_ITEM),
		"drop_source_kind": String(drop_source_kind),
		"drop_source_id": String(drop_source_id),
		"drop_source_label": String(drop_source_id),
		"drop_entry_id": drop_entry_id,
		"item_id": String(item_id),
		"quantity": quantity,
	}


func _count_matching_loot_quantity(
	loot_entries: Array,
	item_id: StringName,
	drop_source_kind: StringName,
	drop_source_id: StringName
) -> int:
	var total_quantity := 0
	for loot_entry_variant in loot_entries:
		if loot_entry_variant is not Dictionary:
			continue
		var loot_entry: Dictionary = loot_entry_variant
		if ProgressionDataUtils.to_string_name(loot_entry.get("item_id", "")) != item_id:
			continue
		if ProgressionDataUtils.to_string_name(loot_entry.get("drop_source_kind", "")) != drop_source_kind:
			continue
		if ProgressionDataUtils.to_string_name(loot_entry.get("drop_source_id", "")) != drop_source_id:
			continue
		total_quantity += int(loot_entry.get("quantity", 0))
	return total_quantity


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
