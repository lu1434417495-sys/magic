extends SceneTree

const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const FaithService = preload("res://scripts/systems/progression/faith_service.gd")
const MisfortuneGuidanceService = preload("res://scripts/systems/battle/fate/misfortune_guidance_service.gd")
const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const UnitProfessionProgress = preload("res://scripts/player/progression/unit_profession_progress.gd")
const BattleResolutionResult = BATTLE_RESOLUTION_RESULT_SCRIPT
const BattleState = BATTLE_STATE_SCRIPT
const BattleUnitState = BATTLE_UNIT_STATE_SCRIPT

const HERO_ID: StringName = &"hero"
const MISFORTUNE_DEITY_ID: StringName = &"misfortune_black_crown"
const DOOM_MARKED_STAT_ID: StringName = &"doom_marked"
const DOOM_AUTHORITY_STAT_ID: StringName = &"doom_authority"
const BOSS_TARGET_STAT_ID: StringName = &"boss_target"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"
const STATUS_BLACK_STAR_BRAND_ELITE: StringName = &"black_star_brand_elite"
const STATUS_CROWN_BREAK_BROKEN_HAND: StringName = &"crown_break_broken_hand"
const STATUS_DOOM_SENTENCE_VERDICT: StringName = &"doom_sentence_verdict"
const CALAMITY_SHARD_ITEM_ID: StringName = &"calamity_shard"

var _failures: Array[String] = []


class StubMisfortuneBattleGateway:
	extends RefCounted

	var calamity_by_member_id: Dictionary = {}
	var reason_flags_by_member_id: Dictionary = {}


	func get_calamity_by_member_id() -> Dictionary:
		return ProgressionDataUtils.to_string_name_int_map(calamity_by_member_id).duplicate(true)


	func has_misfortune_reason(member_id: StringName, reason_id: StringName) -> bool:
		var normalized_member_id := ProgressionDataUtils.to_string_name(member_id)
		var normalized_reason_id := ProgressionDataUtils.to_string_name(reason_id)
		if normalized_member_id == &"" or normalized_reason_id == &"":
			return false
		var member_reasons_variant: Variant = reason_flags_by_member_id.get(normalized_member_id, {})
		if member_reasons_variant is not Dictionary:
			return false
		return bool((member_reasons_variant as Dictionary).get(normalized_reason_id, false))


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_misfortune_guidance_unlock_chain_feeds_rank_2_to_5()
	_test_forge_result_rejects_legacy_ok_success_alias()
	_test_forge_result_rejects_string_key_only_dark_equipment_def()

	if _failures.is_empty():
		print("Misfortune guidance regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Misfortune guidance regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_misfortune_guidance_unlock_chain_feeds_rank_2_to_5() -> void:
	var context := _build_context()
	var party_state: PartyState = context.get("party_state") as PartyState
	var manager: CharacterManagementModule = context.get("manager") as CharacterManagementModule
	var guidance: MisfortuneGuidanceService = context.get("guidance") as MisfortuneGuidanceService
	var faith: FaithService = context.get("faith") as FaithService
	var battle_gateway: StubMisfortuneBattleGateway = context.get("battle_gateway") as StubMisfortuneBattleGateway
	var item_defs: Dictionary = context.get("item_defs", {})
	if party_state == null or manager == null or guidance == null or faith == null or battle_gateway == null:
		_assert_true(false, "Misfortune guidance regression 前置构建失败。")
		return

	var rank_1_result := faith.execute_devotion(party_state, HERO_ID, MISFORTUNE_DEITY_ID)
	_assert_true(bool(rank_1_result.get("ok", false)), "doom_marked 写入后应允许进入 Misfortune rank 1。")
	_apply_next_pending_reward(manager, party_state, 1)
	_assert_eq(_get_custom_stat(party_state, DOOM_AUTHORITY_STAT_ID), 1, "rank 1 结算后应写入 doom_authority=1。")

	var blocked_rank_2 := faith.execute_devotion(party_state, HERO_ID, MISFORTUNE_DEITY_ID)
	_assert_true(not bool(blocked_rank_2.get("ok", false)), "guidance_true 未解锁前不应进入 rank 2。")
	_assert_eq(String(blocked_rank_2.get("missing_achievement_id", "")), "misfortune_guidance_true", "rank 2 应明确指出 guidance_true 缺失。")

	var true_unlocks := guidance.handle_battle_resolution(
		_build_battle_state_with_defeated_enemy(&"misfortune_true", STATUS_BLACK_STAR_BRAND_ELITE, false),
		_build_battle_resolution_result(&"misfortune_true")
	)
	_assert_true(true_unlocks.has(MisfortuneGuidanceService.ACHIEVEMENT_GUIDANCE_TRUE), "doom_marked 后封印 elite 应解锁 guidance_true。")
	_assert_true(_is_achievement_unlocked(party_state, MisfortuneGuidanceService.ACHIEVEMENT_GUIDANCE_TRUE), "campaign achievement 记录应保留 guidance_true。")
	_assert_true(party_state.pending_character_rewards.is_empty(), "guidance 成就本身不应排入额外 reward 队列。")

	var rank_2_result := faith.execute_devotion(party_state, HERO_ID, MISFORTUNE_DEITY_ID)
	_assert_true(bool(rank_2_result.get("ok", false)), "guidance_true 达成后应允许进入 rank 2。")
	_apply_next_pending_reward(manager, party_state, 2)

	var blocked_rank_3 := faith.execute_devotion(party_state, HERO_ID, MISFORTUNE_DEITY_ID)
	_assert_true(not bool(blocked_rank_3.get("ok", false)), "guidance_devout 未解锁前不应进入 rank 3。")
	_assert_eq(String(blocked_rank_3.get("missing_achievement_id", "")), "misfortune_guidance_devout", "rank 3 应明确指出 guidance_devout 缺失。")

	battle_gateway.reason_flags_by_member_id = {
		HERO_ID: {
			MisfortuneGuidanceService.CALAMITY_REASON_CRITICAL_FAIL: true,
		},
	}
	var devout_unlocks := guidance.handle_battle_resolution(
		_build_battle_state_with_defeated_enemy(&"misfortune_devout", STATUS_CROWN_BREAK_BROKEN_HAND, false),
		_build_battle_resolution_result(&"misfortune_devout")
	)
	_assert_true(devout_unlocks.has(MisfortuneGuidanceService.ACHIEVEMENT_GUIDANCE_DEVOUT), "大失败后再用封印链赢下 elite 应解锁 guidance_devout。")
	_assert_true(_is_achievement_unlocked(party_state, MisfortuneGuidanceService.ACHIEVEMENT_GUIDANCE_DEVOUT), "campaign achievement 记录应保留 guidance_devout。")

	var rank_3_result := faith.execute_devotion(party_state, HERO_ID, MISFORTUNE_DEITY_ID)
	_assert_true(bool(rank_3_result.get("ok", false)), "guidance_devout 达成后应允许进入 rank 3。")
	_apply_next_pending_reward(manager, party_state, 3)

	var blocked_rank_4 := faith.execute_devotion(party_state, HERO_ID, MISFORTUNE_DEITY_ID)
	_assert_true(not bool(blocked_rank_4.get("ok", false)), "guidance_exalted 未解锁前不应进入 rank 4。")
	_assert_eq(String(blocked_rank_4.get("missing_achievement_id", "")), "misfortune_guidance_exalted", "rank 4 应明确指出 guidance_exalted 缺失。")

	battle_gateway.reason_flags_by_member_id.clear()
	battle_gateway.calamity_by_member_id = {HERO_ID: 2}
	var exalted_battle_unlocks := guidance.handle_battle_resolution(
		_build_battle_state_without_enemies(&"misfortune_exalted"),
		_build_battle_resolution_result(&"misfortune_exalted", {"converted_calamity_shards": 1})
	)
	_assert_true(exalted_battle_unlocks.is_empty(), "仅结算 calamity->shard 不应提前直接解锁 guidance_exalted。")
	var exalted_unlocks := guidance.handle_forge_result(
		HERO_ID,
		_build_forge_result(&"shadow_halberd"),
		item_defs
	)
	_assert_true(exalted_unlocks.has(MisfortuneGuidanceService.ACHIEVEMENT_GUIDANCE_EXALTED), "结算碎片后用固定材料打造黑暗装备应解锁 guidance_exalted。")
	_assert_true(_is_achievement_unlocked(party_state, MisfortuneGuidanceService.ACHIEVEMENT_GUIDANCE_EXALTED), "campaign achievement 记录应保留 guidance_exalted。")

	var rank_4_result := faith.execute_devotion(party_state, HERO_ID, MISFORTUNE_DEITY_ID)
	_assert_true(bool(rank_4_result.get("ok", false)), "guidance_exalted 达成后应允许进入 rank 4。")
	_apply_next_pending_reward(manager, party_state, 4)

	var blocked_rank_5 := faith.execute_devotion(party_state, HERO_ID, MISFORTUNE_DEITY_ID)
	_assert_true(not bool(blocked_rank_5.get("ok", false)), "guidance_blessed 未解锁前不应进入 rank 5。")
	_assert_eq(String(blocked_rank_5.get("missing_achievement_id", "")), "misfortune_guidance_blessed", "rank 5 应明确指出 guidance_blessed 缺失。")

	var blessed_unlocks := guidance.handle_battle_resolution(
		_build_battle_state_with_defeated_enemy(&"misfortune_blessed", STATUS_DOOM_SENTENCE_VERDICT, true),
		_build_battle_resolution_result(&"misfortune_blessed")
	)
	_assert_true(blessed_unlocks.has(MisfortuneGuidanceService.ACHIEVEMENT_GUIDANCE_BLESSED), "用 doom_sentence 终结 boss 应解锁 guidance_blessed。")
	_assert_true(_is_achievement_unlocked(party_state, MisfortuneGuidanceService.ACHIEVEMENT_GUIDANCE_BLESSED), "campaign achievement 记录应保留 guidance_blessed。")

	var rank_5_result := faith.execute_devotion(party_state, HERO_ID, MISFORTUNE_DEITY_ID)
	_assert_true(bool(rank_5_result.get("ok", false)), "guidance_blessed 达成后应允许进入 rank 5。")
	_apply_next_pending_reward(manager, party_state, 5)
	_assert_eq(_get_custom_stat(party_state, DOOM_AUTHORITY_STAT_ID), 5, "完整 guidance 链结算后 doom_authority 应到 rank 5。")


func _test_forge_result_rejects_legacy_ok_success_alias() -> void:
	var context := _build_context()
	var party_state: PartyState = context.get("party_state") as PartyState
	var guidance: MisfortuneGuidanceService = context.get("guidance") as MisfortuneGuidanceService
	var battle_gateway: StubMisfortuneBattleGateway = context.get("battle_gateway") as StubMisfortuneBattleGateway
	var item_defs: Dictionary = context.get("item_defs", {})
	if party_state == null or guidance == null or battle_gateway == null:
		_assert_true(false, "Misfortune legacy ok alias regression 前置构建失败。")
		return

	battle_gateway.calamity_by_member_id = {HERO_ID: 2}
	guidance.handle_battle_resolution(
		_build_battle_state_without_enemies(&"misfortune_legacy_ok_alias"),
		_build_battle_resolution_result(&"misfortune_legacy_ok_alias", {"converted_calamity_shards": 1})
	)
	var legacy_result := _build_forge_result(&"shadow_halberd")
	legacy_result.erase("success")
	legacy_result["ok"] = true
	var legacy_unlocks := guidance.handle_forge_result(HERO_ID, legacy_result, item_defs)
	_assert_true(legacy_unlocks.is_empty(), "forge result 只有 legacy ok=true 时不应解锁 guidance_exalted。")
	_assert_true(
		not _is_achievement_unlocked(party_state, MisfortuneGuidanceService.ACHIEVEMENT_GUIDANCE_EXALTED),
		"forge result 缺正式 success 字段时不应写入 guidance_exalted。"
	)


func _test_forge_result_rejects_string_key_only_dark_equipment_def() -> void:
	var context := _build_context()
	var party_state: PartyState = context.get("party_state") as PartyState
	var guidance: MisfortuneGuidanceService = context.get("guidance") as MisfortuneGuidanceService
	var battle_gateway: StubMisfortuneBattleGateway = context.get("battle_gateway") as StubMisfortuneBattleGateway
	var item_defs: Dictionary = context.get("item_defs", {})
	if party_state == null or guidance == null or battle_gateway == null:
		_assert_true(false, "Misfortune String-key-only item_defs regression 前置构建失败。")
		return

	battle_gateway.calamity_by_member_id = {HERO_ID: 2}
	guidance.handle_battle_resolution(
		_build_battle_state_without_enemies(&"misfortune_string_key_item_defs"),
		_build_battle_resolution_result(&"misfortune_string_key_item_defs", {"converted_calamity_shards": 1})
	)
	var dark_weapon := item_defs.get(&"shadow_halberd") as ItemDef
	if dark_weapon == null:
		_assert_true(false, "Misfortune String-key-only item_defs regression 前置：应存在正式 shadow_halberd。")
		return
	var string_key_only_defs := {
		String(dark_weapon.item_id): dark_weapon,
	}
	var unlocks := guidance.handle_forge_result(
		HERO_ID,
		_build_forge_result(&"shadow_halberd"),
		string_key_only_defs
	)
	_assert_true(unlocks.is_empty(), "forge result 只有 String key 的 dark equipment def 时不应解锁 guidance_exalted。")
	_assert_true(
		not _is_achievement_unlocked(party_state, MisfortuneGuidanceService.ACHIEVEMENT_GUIDANCE_EXALTED),
		"forge result 缺正式 StringName key 时不应写入 guidance_exalted。"
	)


func _build_context() -> Dictionary:
	var item_defs := _build_item_defs()
	var party_state := PartyState.new()
	party_state.leader_member_id = HERO_ID
	party_state.main_character_member_id = HERO_ID
	party_state.active_member_ids = [HERO_ID]
	party_state.set_gold(50000)
	party_state.set_member_state(_build_member_state())

	var manager := CharacterManagementModule.new()
	manager.setup(
		party_state,
		{},
		{},
		ProgressionContentRegistry.new().get_achievement_defs(),
		item_defs
	)

	var battle_gateway := StubMisfortuneBattleGateway.new()
	var guidance := MisfortuneGuidanceService.new()
	guidance.setup(manager, battle_gateway)

	var faith := FaithService.new()
	return {
		"party_state": party_state,
		"manager": manager,
		"guidance": guidance,
		"faith": faith,
		"battle_gateway": battle_gateway,
		"item_defs": item_defs,
	}


func _build_item_defs() -> Dictionary:
	var dark_weapon := ItemDef.new()
	dark_weapon.item_id = &"shadow_halberd"
	dark_weapon.display_name = "Shadow Halberd"
	dark_weapon.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	dark_weapon.equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON
	dark_weapon.equipment_slot_ids = ["main_hand"]
	dark_weapon.tags = [&"dark", &"misfortune"]
	dark_weapon.crafting_groups = [&"dark", &"misfortune"]

	var calamity_shard := ItemDef.new()
	calamity_shard.item_id = CALAMITY_SHARD_ITEM_ID
	calamity_shard.display_name = "灾厄碎片"
	calamity_shard.item_category = ItemDef.ITEM_CATEGORY_MISC
	calamity_shard.is_stackable = true
	calamity_shard.max_stack = 99
	calamity_shard.tags = [&"material", &"misfortune"]
	calamity_shard.crafting_groups = [&"misfortune"]

	return {
		dark_weapon.item_id: dark_weapon,
		calamity_shard.item_id: calamity_shard,
	}


func _build_member_state() -> PartyMemberState:
	var member_state := PartyMemberState.new()
	member_state.member_id = HERO_ID
	member_state.display_name = "Hero"
	member_state.progression.unit_id = HERO_ID
	member_state.progression.display_name = "Hero"
	member_state.progression.character_level = 30
	var level_anchor := UnitProfessionProgress.new()
	level_anchor.profession_id = &"misfortune_guidance_level_anchor"
	level_anchor.rank = 30
	member_state.progression.set_profession_progress(level_anchor)
	member_state.progression.unit_base_attributes.set_attribute_value(DOOM_MARKED_STAT_ID, 1)
	member_state.progression.unit_base_attributes.set_attribute_value(DOOM_AUTHORITY_STAT_ID, 0)
	return member_state


func _build_battle_state_with_defeated_enemy(
	battle_id: StringName,
	status_id: StringName,
	is_boss: bool
) -> BattleState:
	var battle_state := BattleState.new()
	battle_state.battle_id = battle_id
	var hero_unit := BattleUnitState.new()
	hero_unit.unit_id = &"hero_unit"
	hero_unit.source_member_id = HERO_ID
	hero_unit.faction_id = &"player"
	hero_unit.display_name = "Hero"
	hero_unit.is_alive = true
	battle_state.units[hero_unit.unit_id] = hero_unit
	battle_state.ally_unit_ids = [hero_unit.unit_id]

	var enemy_unit := BattleUnitState.new()
	enemy_unit.unit_id = &"enemy_target"
	enemy_unit.display_name = "Elite Target"
	enemy_unit.faction_id = &"enemy"
	enemy_unit.is_alive = false
	enemy_unit.current_hp = 0
	enemy_unit.attribute_snapshot.set_value(FORTUNE_MARK_TARGET_STAT_ID, 1)
	enemy_unit.attribute_snapshot.set_value(BOSS_TARGET_STAT_ID, 1 if is_boss else 0)
	_set_status(enemy_unit, status_id, hero_unit.unit_id)
	battle_state.units[enemy_unit.unit_id] = enemy_unit
	battle_state.enemy_unit_ids = [enemy_unit.unit_id]
	return battle_state


func _build_battle_state_without_enemies(battle_id: StringName) -> BattleState:
	var battle_state := BattleState.new()
	battle_state.battle_id = battle_id
	var hero_unit := BattleUnitState.new()
	hero_unit.unit_id = &"hero_unit"
	hero_unit.source_member_id = HERO_ID
	hero_unit.faction_id = &"player"
	hero_unit.display_name = "Hero"
	hero_unit.is_alive = true
	battle_state.units[hero_unit.unit_id] = hero_unit
	battle_state.ally_unit_ids = [hero_unit.unit_id]
	return battle_state


func _build_battle_resolution_result(battle_id: StringName, party_resource_commit: Dictionary = {}) -> BattleResolutionResult:
	var result := BattleResolutionResult.new()
	result.battle_id = battle_id
	result.winner_faction_id = &"player"
	result.encounter_resolution = &"player_victory"
	result.party_resource_commit = party_resource_commit.duplicate(true)
	return result


func _build_forge_result(output_item_id: StringName) -> Dictionary:
	return {
		"success": true,
		"inventory_delta": {
			"recipe_id": "shadow_halberd_recipe",
			"removed_entries": [{
				"item_id": String(CALAMITY_SHARD_ITEM_ID),
				"quantity": 1,
			}],
			"added_entries": [{
				"item_id": String(output_item_id),
				"quantity": 1,
			}],
		},
		"service_side_effects": {
			"output_item_id": String(output_item_id),
		},
	}


func _set_status(unit_state: BattleUnitState, status_id: StringName, source_unit_id: StringName) -> void:
	var status_entry := BATTLE_STATUS_EFFECT_STATE_SCRIPT.new()
	status_entry.status_id = status_id
	status_entry.source_unit_id = source_unit_id
	status_entry.power = 1
	status_entry.stacks = 1
	status_entry.duration = 60
	unit_state.set_status_effect(status_entry)


func _apply_next_pending_reward(manager: CharacterManagementModule, party_state: PartyState, expected_rank: int) -> void:
	var pending_reward = party_state.get_next_pending_character_reward()
	_assert_true(pending_reward != null, "Misfortune rank %d 应产生 pending reward。" % expected_rank)
	if pending_reward == null:
		return
	manager.apply_pending_character_reward(pending_reward)
	_assert_true(party_state.get_next_pending_character_reward() == null, "Misfortune rank %d 结算后应清空 pending reward。" % expected_rank)


func _is_achievement_unlocked(party_state: PartyState, achievement_id: StringName) -> bool:
	var member_state := party_state.get_member_state(HERO_ID) as PartyMemberState
	if member_state == null or member_state.progression == null:
		return false
	var progress_state = member_state.progression.get_achievement_progress_state(achievement_id)
	return progress_state != null and bool(progress_state.is_unlocked)


func _get_custom_stat(party_state: PartyState, stat_id: StringName) -> int:
	var member_state := party_state.get_member_state(HERO_ID) as PartyMemberState
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return 0
	return member_state.progression.unit_base_attributes.get_attribute_value(stat_id)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
