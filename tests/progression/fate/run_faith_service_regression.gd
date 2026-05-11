extends SceneTree

const AchievementProgressState = preload("res://scripts/player/progression/achievement_progress_state.gd")
const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const FaithDeityDef = preload("res://scripts/player/progression/faith_deity_def.gd")
const FaithRankDef = preload("res://scripts/player/progression/faith_rank_def.gd")
const FaithService = preload("res://scripts/systems/progression/faith_service.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const UnitProfessionProgress = preload("res://scripts/player/progression/unit_profession_progress.gd")

const FORTUNA_DEITY_ID: StringName = &"fortuna"
const MISFORTUNE_DEITY_ID: StringName = &"misfortune_black_crown"
const FAITH_LUCK_BONUS_STAT_ID: StringName = &"faith_luck_bonus"
const FORTUNE_MARKED_STAT_ID: StringName = &"fortune_marked"
const DOOM_MARKED_STAT_ID: StringName = &"doom_marked"
const DOOM_AUTHORITY_STAT_ID: StringName = &"doom_authority"
const CALAMITY_CAPACITY_BONUS_STAT_ID: StringName = &"calamity_capacity_bonus"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_fortuna_config_matches_story_acceptance()
	_test_fortuna_rank_up_applies_faith_luck_bonus_until_cap()
	_test_misfortune_config_matches_story_acceptance()
	_test_misfortune_rank_up_applies_doom_authority_until_cap()

	if _failures.is_empty():
		print("FaithService regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("FaithService regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_fortuna_config_matches_story_acceptance() -> void:
	var faith_service := FaithService.new()
	_assert_true(faith_service.validate().is_empty(), "FaithService 默认配置应能通过基础校验。")

	var fortuna_def: FaithDeityDef = faith_service.get_faith_deity_def(FORTUNA_DEITY_ID)
	_assert_true(fortuna_def != null, "应能加载 Fortuna FaithDeityDef。")
	if fortuna_def == null:
		return

	var expected_gold := [500, 2000, 4500, 8000, 14000]
	var expected_level := [0, 8, 14, 20, 28]
	var expected_achievements: Array[StringName] = [
		&"",
		&"fortuna_guidance_true",
		&"fortuna_guidance_devout",
		&"fortuna_guidance_exalted",
		&"fortuna_guidance_blessed",
	]

	_assert_eq(fortuna_def.get_max_rank(), 5, "Fortuna 应保留 5 阶骨架。")
	for index in range(5):
		var rank_index := index + 1
		var rank_def: FaithRankDef = fortuna_def.get_rank_def(rank_index)
		_assert_true(rank_def != null, "Fortuna 应存在 rank %d 配置。" % rank_index)
		if rank_def == null:
			continue

		_assert_eq(rank_def.required_gold, expected_gold[index], "Fortuna rank %d required_gold 错误。" % rank_index)
		_assert_eq(rank_def.required_level, expected_level[index], "Fortuna rank %d required_level 错误。" % rank_index)
		if rank_index == 1:
			_assert_eq(
				rank_def.required_custom_stat_id,
				FORTUNE_MARKED_STAT_ID,
				"Fortuna rank 1 应使用 fortune_marked 占位门票。"
			)
			_assert_eq(rank_def.required_custom_stat_min_value, 1, "Fortuna rank 1 应要求 fortune_marked == 1。")
		else:
			_assert_eq(
				rank_def.required_achievement_id,
				expected_achievements[index],
				"Fortuna rank %d guidance achievement 占位 id 错误。" % rank_index
			)

		_assert_eq(rank_def.reward_entries.size(), 1, "Fortuna rank %d 应只有一条骨架奖励。" % rank_index)
		if rank_def.reward_entries.is_empty():
			continue
		var reward_entry: Variant = rank_def.reward_entries[0]
		_assert_true(reward_entry is Dictionary, "Fortuna rank %d 奖励条目应保持 Dictionary shape。" % rank_index)
		if reward_entry is not Dictionary:
			continue
		_assert_eq(
			ProgressionDataUtils.to_string_name((reward_entry as Dictionary).get("target_id", "")),
			FAITH_LUCK_BONUS_STAT_ID,
			"Fortuna rank %d 奖励应写入 faith_luck_bonus。" % rank_index
		)
		_assert_eq(int((reward_entry as Dictionary).get("amount", 0)), 1, "Fortuna rank %d 奖励应固定 +1。" % rank_index)


func _test_fortuna_rank_up_applies_faith_luck_bonus_until_cap() -> void:
	var party_state := _build_party_state()
	var manager := CharacterManagementModule.new()
	manager.setup(party_state, {}, {}, {})
	var faith_service := FaithService.new()

	for target_rank in range(1, 6):
		var devotion_result := faith_service.execute_devotion(party_state, &"hero", FORTUNA_DEITY_ID)
		_assert_true(devotion_result.get("ok", false), "Fortuna rank %d 应能成功进入 pending reward 队列。" % target_rank)
		if not devotion_result.get("ok", false):
			return
		_assert_eq(int(devotion_result.get("target_rank", 0)), target_rank, "Fortuna 每次只应提升 1 阶。")

		var pending_reward = party_state.get_next_pending_character_reward()
		_assert_true(pending_reward != null, "Fortuna rank %d 成功后应排入 pending reward。" % target_rank)
		if pending_reward == null:
			return

		var delta = manager.apply_pending_character_reward(pending_reward)
		_assert_eq(
			int(party_state.get_member_state(&"hero").get_faith_luck_bonus()),
			target_rank,
			"Fortuna rank %d 结算后应把 faith_luck_bonus 写到正确值。" % target_rank
		)
		_assert_eq(
			delta.attribute_changes.size(),
			1,
			"Fortuna rank %d 只应产生一条 attribute delta。" % target_rank
		)
		if not delta.attribute_changes.is_empty():
			_assert_eq(
				ProgressionDataUtils.to_string_name(delta.attribute_changes[0].get("attribute_id", "")),
				FAITH_LUCK_BONUS_STAT_ID,
				"Fortuna rank %d 的 delta 应指向 faith_luck_bonus。" % target_rank
			)

	var cap_result := faith_service.execute_devotion(party_state, &"hero", FORTUNA_DEITY_ID)
	_assert_true(not cap_result.get("ok", false), "达到 rank 5 后不应继续升级。")
	_assert_eq(String(cap_result.get("error_code", "")), "max_rank_reached", "达到上限后应返回 max_rank_reached。")
	_assert_eq(
		int(party_state.get_member_state(&"hero").get_faith_luck_bonus()),
		5,
		"达到上限后 faith_luck_bonus 不应继续增加。"
	)
	_assert_true(party_state.get_next_pending_character_reward() == null, "达到上限后不应新增 pending reward。")


func _test_misfortune_config_matches_story_acceptance() -> void:
	var faith_service := FaithService.new()
	var misfortune_def: FaithDeityDef = faith_service.get_faith_deity_def(MISFORTUNE_DEITY_ID)
	_assert_true(misfortune_def != null, "应能加载 Misfortune FaithDeityDef。")
	if misfortune_def == null:
		return

	var expected_gold := [500, 2000, 4500, 8000, 14000]
	var expected_level := [0, 8, 14, 20, 28]
	var expected_achievements: Array[StringName] = [
		&"",
		&"misfortune_guidance_true",
		&"misfortune_guidance_devout",
		&"misfortune_guidance_exalted",
		&"misfortune_guidance_blessed",
	]
	var expected_placeholders: Array[StringName] = [
		&"black_star_brand",
		&"calamity_capacity_bonus",
		&"crown_break",
		&"calamity_capacity_bonus",
		&"doom_sentence",
	]

	_assert_eq(misfortune_def.get_max_rank(), 5, "Misfortune 应保留 5 阶骨架。")
	_assert_eq(misfortune_def.rank_progress_stat_id, DOOM_AUTHORITY_STAT_ID, "Misfortune 应使用 doom_authority 作为 rank progress stat。")
	for index in range(5):
		var rank_index := index + 1
		var rank_def: FaithRankDef = misfortune_def.get_rank_def(rank_index)
		_assert_true(rank_def != null, "Misfortune 应存在 rank %d 配置。" % rank_index)
		if rank_def == null:
			continue

		_assert_eq(rank_def.required_gold, expected_gold[index], "Misfortune rank %d required_gold 错误。" % rank_index)
		_assert_eq(rank_def.required_level, expected_level[index], "Misfortune rank %d required_level 错误。" % rank_index)
		if rank_index == 1:
			_assert_eq(
				rank_def.required_custom_stat_id,
				DOOM_MARKED_STAT_ID,
				"Misfortune rank 1 应使用 doom_marked 占位门票。"
			)
			_assert_eq(rank_def.required_custom_stat_min_value, 1, "Misfortune rank 1 应要求 doom_marked == 1。")
		else:
			_assert_eq(
				rank_def.required_achievement_id,
				expected_achievements[index],
				"Misfortune rank %d guidance achievement 占位 id 错误。" % rank_index
			)

		_assert_eq(rank_def.reward_entries.size(), 2, "Misfortune rank %d 应保留 1 条属性奖励和 1 条占位奖励。" % rank_index)
		_assert_true(
			_has_reward_entry(rank_def, &"attribute_delta", DOOM_AUTHORITY_STAT_ID, 1),
			"Misfortune rank %d 应包含 doom_authority +1。" % rank_index
		)

		var placeholder_entry_type := &"attribute_delta" if expected_placeholders[index] == CALAMITY_CAPACITY_BONUS_STAT_ID else &"knowledge_unlock"
		_assert_true(
			_has_reward_entry(rank_def, placeholder_entry_type, expected_placeholders[index], 1),
			"Misfortune rank %d 的占位奖励配置错误。" % rank_index
		)


func _test_misfortune_rank_up_applies_doom_authority_until_cap() -> void:
	var party_state := _build_party_state()
	var manager := CharacterManagementModule.new()
	manager.setup(party_state, {}, {}, {})
	var faith_service := FaithService.new()
	var expected_knowledge_unlocks: Dictionary = {
		1: &"black_star_brand",
		3: &"crown_break",
		5: &"doom_sentence",
	}
	var expected_calamity_bonus_by_rank: Dictionary = {
		2: 1,
		4: 2,
	}

	for target_rank in range(1, 6):
		var devotion_result := faith_service.execute_devotion(party_state, &"hero", MISFORTUNE_DEITY_ID)
		_assert_true(devotion_result.get("ok", false), "Misfortune rank %d 应能成功进入 pending reward 队列。" % target_rank)
		if not devotion_result.get("ok", false):
			return
		_assert_eq(int(devotion_result.get("target_rank", 0)), target_rank, "Misfortune 每次只应提升 1 阶。")

		var pending_reward = party_state.get_next_pending_character_reward()
		_assert_true(pending_reward != null, "Misfortune rank %d 成功后应排入 pending reward。" % target_rank)
		if pending_reward == null:
			return

		manager.apply_pending_character_reward(pending_reward)
		_assert_eq(
			_get_custom_stat(party_state, DOOM_AUTHORITY_STAT_ID),
			target_rank,
			"Misfortune rank %d 结算后应把 doom_authority 写到正确值。" % target_rank
		)
		if expected_knowledge_unlocks.has(target_rank):
			var placeholder_knowledge := ProgressionDataUtils.to_string_name(expected_knowledge_unlocks.get(target_rank, ""))
			_assert_true(
				party_state.get_member_state(&"hero").progression.has_knowledge(placeholder_knowledge),
				"Misfortune rank %d 应把技能占位写入 known_knowledge_ids。" % target_rank
			)
		if expected_calamity_bonus_by_rank.has(target_rank):
			_assert_eq(
				_get_custom_stat(party_state, CALAMITY_CAPACITY_BONUS_STAT_ID),
				int(expected_calamity_bonus_by_rank.get(target_rank, 0)),
				"Misfortune rank %d 结算后应累计 calamity 上限占位。" % target_rank
			)

	var cap_result := faith_service.execute_devotion(party_state, &"hero", MISFORTUNE_DEITY_ID)
	_assert_true(not cap_result.get("ok", false), "达到 Misfortune rank 5 后不应继续升级。")
	_assert_eq(String(cap_result.get("error_code", "")), "max_rank_reached", "Misfortune 达到上限后应返回 max_rank_reached。")
	_assert_eq(_get_custom_stat(party_state, DOOM_AUTHORITY_STAT_ID), 5, "达到上限后 doom_authority 不应继续增加。")
	_assert_true(party_state.get_next_pending_character_reward() == null, "Misfortune 达到上限后不应新增 pending reward。")


func _build_party_state() -> PartyState:
	var party_state := PartyState.new()
	party_state.leader_member_id = &"hero"
	party_state.main_character_member_id = &"hero"
	party_state.active_member_ids = [&"hero"]
	party_state.set_gold(50000)
	party_state.set_member_state(_build_party_member_state())
	return party_state


func _build_party_member_state() -> PartyMemberState:
	var member_state := PartyMemberState.new()
	member_state.member_id = &"hero"
	member_state.display_name = "Hero"
	member_state.progression.unit_id = &"hero"
	member_state.progression.display_name = "Hero"
	member_state.progression.character_level = 30
	var level_anchor := UnitProfessionProgress.new()
	level_anchor.profession_id = &"faith_test_level_anchor"
	level_anchor.rank = 30
	member_state.progression.set_profession_progress(level_anchor)
	member_state.progression.unit_base_attributes.custom_stats[FORTUNE_MARKED_STAT_ID] = 1
	member_state.progression.unit_base_attributes.custom_stats[FAITH_LUCK_BONUS_STAT_ID] = 0
	member_state.progression.unit_base_attributes.custom_stats[DOOM_MARKED_STAT_ID] = 1
	member_state.progression.unit_base_attributes.custom_stats[DOOM_AUTHORITY_STAT_ID] = 0
	member_state.progression.unit_base_attributes.custom_stats[CALAMITY_CAPACITY_BONUS_STAT_ID] = 0

	for achievement_id in [
		&"fortuna_guidance_true",
		&"fortuna_guidance_devout",
		&"fortuna_guidance_exalted",
		&"fortuna_guidance_blessed",
		&"misfortune_guidance_true",
		&"misfortune_guidance_devout",
		&"misfortune_guidance_exalted",
		&"misfortune_guidance_blessed",
	]:
		var progress_state := AchievementProgressState.new()
		progress_state.achievement_id = achievement_id
		progress_state.current_value = 1
		progress_state.is_unlocked = true
		member_state.progression.set_achievement_progress_state(progress_state)

	return member_state


func _has_reward_entry(rank_def: FaithRankDef, entry_type: StringName, target_id: StringName, amount: int) -> bool:
	if rank_def == null:
		return false
	for reward_entry_variant in rank_def.reward_entries:
		if reward_entry_variant is not Dictionary:
			continue
		var reward_entry := reward_entry_variant as Dictionary
		if ProgressionDataUtils.to_string_name(reward_entry.get("entry_type", "")) != entry_type:
			continue
		if ProgressionDataUtils.to_string_name(reward_entry.get("target_id", "")) != target_id:
			continue
		if int(reward_entry.get("amount", 0)) != amount:
			continue
		return true
	return false


func _get_custom_stat(party_state: PartyState, stat_id: StringName) -> int:
	var member_state := party_state.get_member_state(&"hero") as PartyMemberState
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return 0
	return member_state.progression.unit_base_attributes.get_attribute_value(stat_id)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
