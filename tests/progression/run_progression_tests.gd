## 文件说明：该脚本属于成长测试执行相关的回归测试脚本，集中维护失败信息等顶层字段。
## 审查重点：重点核对测试数据、字段用途、断言条件和失败提示是否仍然覆盖目标回归场景。
## 备注：后续如果业务规则变化，需要同步更新测试夹具、预期结果和失败信息。

extends SceneTree

const AchievementDef = preload("res://scripts/player/progression/achievement_def.gd")
const AchievementProgressState = preload("res://scripts/player/progression/achievement_progress_state.gd")
const AchievementRewardDef = preload("res://scripts/player/progression/achievement_reward_def.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle_runtime_module.gd")
const CharacterManagementModule = preload("res://scripts/systems/character_management_module.gd")
const PartyManagementWindowScene = preload("res://scenes/ui/party_management_window.tscn")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const ProgressionSerialization = preload("res://scripts/systems/progression_serialization.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const WorldMapSystem = preload("res://scripts/systems/world_map_system.gd")

## 字段说明：记录测试过程中收集到的失败信息，便于最终集中输出并快速定位回归点。
var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_seed_achievement_registry_validates()
	_test_seed_profession_catalog_includes_class_archetypes()
	_test_archer_book_skill_catalog_registers_and_is_learnable()
	_test_seed_growth_achievement_events_unlock_via_real_progression_actions()
	_test_achievement_progress_is_member_scoped_and_unlocks_once()
	_test_single_event_can_unlock_multiple_achievements_in_queue_order()
	_test_pending_character_reward_applies_in_stable_order()
	_test_pending_character_reward_round_trip_persists()
	_test_legacy_pending_mastery_rewards_still_convert()
	_test_battle_achievement_only_queues_reward_without_mutating_runtime_unit()
	await _test_party_management_window_renders_achievement_summary()

	if _failures.is_empty():
		print("Progression achievement tests: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Progression achievement tests: FAIL (%d)" % _failures.size())
	quit(1)


func _test_seed_achievement_registry_validates() -> void:
	var registry := ProgressionContentRegistry.new()
	_assert_true(BattleRuntimeModule != null, "BattleRuntimeModule 脚本应能成功编译。")
	_assert_true(WorldMapSystem != null, "WorldMapSystem 脚本应能成功编译。")
	_assert_true(
		registry.validate().is_empty(),
		"ProgressionContentRegistry 的种子成就定义应通过校验。"
	)
	_assert_true(
		registry.get_achievement_defs().size() >= 8,
		"种子成就定义数量应至少覆盖战斗、据点与成长事件的首批内容。"
	)


func _test_seed_profession_catalog_includes_class_archetypes() -> void:
	var registry := ProgressionContentRegistry.new()
	var profession_defs := registry.get_profession_defs()
	var skill_defs := registry.get_skill_defs()
	var profession_ids: Array[StringName] = [
		&"warrior",
		&"priest",
		&"rogue",
		&"berserker",
		&"paladin",
		&"mage",
		&"archer",
	]
	var representative_skill_ids: Array[StringName] = [
		&"charge",
		&"warrior_shield_bash",
		&"mage_fireball",
		&"mage_temporal_rewind",
		&"archer_arrow_rain",
	]

	for profession_id in profession_ids:
		var profession_def = profession_defs.get(profession_id)
		_assert_true(profession_def != null, "应注册职业 %s。" % String(profession_id))

	for skill_id in representative_skill_ids:
		var skill_def = skill_defs.get(skill_id)
		_assert_true(skill_def != null, "应注册技能 %s。" % String(skill_id))
		_assert_true(
			skill_def != null and skill_def.can_use_in_combat(),
			"代表技能 %s 应具备战斗配置。" % String(skill_id)
		)


func _test_archer_book_skill_catalog_registers_and_is_learnable() -> void:
	var registry := ProgressionContentRegistry.new()
	var skill_defs := registry.get_skill_defs()
	var archer_skill_ids: Array[StringName] = []
	for skill_def_variant in skill_defs.values():
		var skill_def = skill_def_variant as SkillDef
		if skill_def == null or not skill_def.tags.has(&"archer"):
			continue
		archer_skill_ids.append(skill_def.skill_id)

	_assert_eq(archer_skill_ids.size(), 32, "弓箭手技能目录应完整注册 32 个主动技能。")

	var party_state := _make_party_state([&"hero"])
	var manager := _setup_manager(party_state, {})
	for skill_id in _build_archer_design_skill_ids():
		var skill_def = skill_defs.get(skill_id)
		_assert_true(skill_def != null, "应注册弓箭手技能 %s。" % String(skill_id))
		_assert_eq(
			skill_def.learn_source,
			&"book",
			"弓箭手技能 %s 应按技能书技能接入。" % String(skill_id)
		)
		_assert_true(
			manager.learn_skill(&"hero", skill_id),
			"hero 应能通过当前技能书学习链路学会 %s。" % String(skill_id)
		)
		if skill_def.skill_type == &"active":
			_assert_true(skill_def.can_use_in_combat(), "主动技能 %s 应具备战斗配置。" % String(skill_id))


func _test_seed_growth_achievement_events_unlock_via_real_progression_actions() -> void:
	var party_state := _make_party_state([&"hero"])
	var manager := _setup_manager(party_state)
	var member_state: PartyMemberState = party_state.get_member_state(&"hero")
	var attributes: UnitBaseAttributes = member_state.progression.unit_base_attributes
	var agility_before: int = attributes.get_attribute_value(UnitBaseAttributes.AGILITY)
	var perception_before: int = attributes.get_attribute_value(UnitBaseAttributes.PERCEPTION)
	var willpower_before: int = attributes.get_attribute_value(UnitBaseAttributes.WILLPOWER)

	_assert_true(manager.learn_skill(&"hero", &"warrior_guard_break"), "成长链路应允许 hero 学会裂甲斩。")
	_assert_true(manager.learn_knowledge(&"hero", &"field_manual"), "成长链路应允许 hero 学会测试知识。")
	_assert_true(manager.learn_skill(&"hero", &"charge"), "成长链路应允许 hero 学会冲锋。")

	var mastery_delta := manager.grant_battle_mastery(&"hero", &"charge", 20)
	_assert_eq(mastery_delta.mastery_changes.size(), 1, "冲锋熟练度应先正常入账，再推进成就。")

	var progression: UnitProgress = member_state.progression
	var learned_skill_progress = progression.get_achievement_progress_state(&"skill_learned_guard_break")
	var learned_knowledge_progress = progression.get_achievement_progress_state(&"knowledge_learned_field_manual")
	var mastery_progress = progression.get_achievement_progress_state(&"skill_mastery_charge_stride")

	_assert_true(
		learned_skill_progress != null and learned_skill_progress.is_unlocked,
		"真实 learn_skill() 应能推进 seed 的 skill_learned 成就。"
	)
	_assert_true(
		learned_knowledge_progress != null and learned_knowledge_progress.is_unlocked,
		"真实 learn_knowledge() 应能推进 seed 的 knowledge_learned 成就。"
	)
	_assert_true(
		mastery_progress != null and mastery_progress.is_unlocked,
		"真实 grant_battle_mastery() 应能推进 seed 的 skill_mastery_gained 成就。"
	)
	_assert_eq(party_state.pending_character_rewards.size(), 3, "三条成长事件成就都应各自产生待领奖励。")
	_assert_eq(party_state.pending_character_rewards[0].source_id, &"skill_learned_guard_break", "技能学习成就奖励应先入队。")
	_assert_eq(party_state.pending_character_rewards[1].source_id, &"knowledge_learned_field_manual", "知识学习成就奖励应随后入队。")
	_assert_eq(party_state.pending_character_rewards[2].source_id, &"skill_mastery_charge_stride", "熟练度成就奖励应按触发顺序入队。")

	while not party_state.pending_character_rewards.is_empty():
		manager.apply_pending_character_reward(party_state.get_next_pending_character_reward())

	_assert_eq(
		attributes.get_attribute_value(UnitBaseAttributes.PERCEPTION),
		perception_before + 1,
		"确认技能学习成就奖励后，应提高感知。"
	)
	_assert_eq(
		attributes.get_attribute_value(UnitBaseAttributes.WILLPOWER),
		willpower_before + 1,
		"确认知识学习成就奖励后，应提高意志。"
	)
	_assert_eq(
		attributes.get_attribute_value(UnitBaseAttributes.AGILITY),
		agility_before + 1,
		"确认熟练度成就奖励后，应提高敏捷。"
	)


func _test_achievement_progress_is_member_scoped_and_unlocks_once() -> void:
	var party_state := _make_party_state([&"hero_a", &"hero_b"])
	var achievement_defs := {
		&"skill_use_counter": _make_achievement(
			&"skill_use_counter",
			"挥砍计数",
			&"skill_used",
			2,
			[
				_make_reward(
					AchievementRewardDef.TYPE_ATTRIBUTE_DELTA,
					UnitBaseAttributes.STRENGTH,
					1,
					"力量"
				),
			],
			&"warrior_heavy_strike"
		),
	}
	var manager := _setup_manager(party_state, achievement_defs)

	var first_unlocks := manager.record_achievement_event(&"hero_a", &"skill_used", 1, &"warrior_heavy_strike")
	var progress_a = party_state.get_member_state(&"hero_a").progression.get_achievement_progress_state(&"skill_use_counter")
	var progress_b = party_state.get_member_state(&"hero_b").progression.get_achievement_progress_state(&"skill_use_counter")
	_assert_eq(first_unlocks.size(), 0, "首次未达阈值时不应返回已解锁成就。")
	_assert_true(progress_a != null and int(progress_a.current_value) == 1, "hero_a 的成就进度应累计到 1。")
	_assert_true(progress_b == null, "hero_b 不应被 hero_a 的事件推进。")

	var second_unlocks := manager.record_achievement_event(&"hero_a", &"skill_used", 1, &"warrior_heavy_strike")
	progress_a = party_state.get_member_state(&"hero_a").progression.get_achievement_progress_state(&"skill_use_counter")
	_assert_eq(second_unlocks.size(), 1, "达到阈值时应只解锁一次成就。")
	_assert_eq(second_unlocks[0], &"skill_use_counter", "解锁结果应返回对应的 achievement_id。")
	_assert_true(progress_a.is_unlocked, "达到阈值后成就状态应标记为已解锁。")
	_assert_eq(int(progress_a.current_value), 2, "已解锁成就的累计值应停留在触发阈值时的值。")
	_assert_eq(party_state.pending_character_rewards.size(), 1, "达成成就后应只入队一份奖励。")

	var third_unlocks := manager.record_achievement_event(&"hero_a", &"skill_used", 1, &"warrior_heavy_strike")
	progress_a = party_state.get_member_state(&"hero_a").progression.get_achievement_progress_state(&"skill_use_counter")
	_assert_true(third_unlocks.is_empty(), "已解锁成就再次收到事件时不应重复解锁。")
	_assert_eq(int(progress_a.current_value), 2, "已解锁成就再次收到事件时不应继续累计。")
	_assert_eq(party_state.pending_character_rewards.size(), 1, "已解锁成就再次收到事件时不应重复入队奖励。")

	manager.record_achievement_event(&"hero_b", &"skill_used", 1, &"warrior_heavy_strike")
	progress_b = party_state.get_member_state(&"hero_b").progression.get_achievement_progress_state(&"skill_use_counter")
	_assert_true(progress_b != null and int(progress_b.current_value) == 1, "另一个成员应维护独立的成就进度。")


func _test_single_event_can_unlock_multiple_achievements_in_queue_order() -> void:
	var party_state := _make_party_state([&"hero"])
	var achievement_defs := {
		&"a_first": _make_achievement(
			&"a_first",
			"先解锁",
			&"battle_won",
			1,
			[_make_reward(AchievementRewardDef.TYPE_ATTRIBUTE_DELTA, UnitBaseAttributes.STRENGTH, 1, "力量")]
		),
		&"b_second": _make_achievement(
			&"b_second",
			"后解锁",
			&"battle_won",
			1,
			[_make_reward(AchievementRewardDef.TYPE_ATTRIBUTE_DELTA, UnitBaseAttributes.AGILITY, 1, "敏捷")]
		),
	}
	var manager := _setup_manager(party_state, achievement_defs)

	var unlocked_ids := manager.record_achievement_event(&"hero", &"battle_won", 1)
	_assert_eq(unlocked_ids.size(), 2, "一次事件应能同时解锁多条成就。")
	_assert_eq(unlocked_ids[0], &"a_first", "解锁顺序应与排序后的 achievement_id 一致。")
	_assert_eq(unlocked_ids[1], &"b_second", "解锁顺序应与排序后的 achievement_id 一致。")
	_assert_eq(party_state.pending_character_rewards.size(), 2, "每条解锁成就都应各自产生一条待领奖励。")
	_assert_eq(party_state.pending_character_rewards[0].source_id, &"a_first", "奖励队列顺序应与解锁顺序一致。")
	_assert_eq(party_state.pending_character_rewards[1].source_id, &"b_second", "奖励队列顺序应与解锁顺序一致。")


func _test_pending_character_reward_applies_in_stable_order() -> void:
	var party_state := _make_party_state([&"hero"])
	var manager := _setup_manager(party_state, {})
	var member_state: PartyMemberState = party_state.get_member_state(&"hero")
	var progression: UnitProgress = member_state.progression
	var strength_before: int = progression.unit_base_attributes.get_attribute_value(UnitBaseAttributes.STRENGTH)

	var reward = manager.build_pending_character_reward(
		&"hero",
		&"combo_reward",
		&"achievement",
		&"combo_reward",
		"组合奖励",
		[
			{
				"entry_type": String(AchievementRewardDef.TYPE_KNOWLEDGE_UNLOCK),
				"target_id": "wayfarer_notes",
				"target_label": "旅途见闻",
				"amount": 1,
				"reason_text": "先解锁知识",
			},
			{
				"entry_type": String(AchievementRewardDef.TYPE_SKILL_UNLOCK),
				"target_id": "charge",
				"target_label": "冲锋",
				"amount": 1,
				"reason_text": "再解锁技能",
			},
			{
				"entry_type": String(AchievementRewardDef.TYPE_SKILL_MASTERY),
				"target_id": "charge",
				"target_label": "冲锋",
				"amount": 20,
				"reason_text": "随后结算熟练度",
			},
			{
				"entry_type": String(AchievementRewardDef.TYPE_ATTRIBUTE_DELTA),
				"target_id": String(UnitBaseAttributes.STRENGTH),
				"target_label": "力量",
				"amount": 2,
				"reason_text": "最后补基础属性",
			},
		],
		"顺序测试奖励"
	)
	_assert_true(reward != null, "测试奖励应成功构建。")
	manager.enqueue_pending_character_rewards([reward])
	_assert_eq(party_state.pending_character_rewards.size(), 1, "测试奖励应先进入待处理队列。")

	var delta = manager.apply_pending_character_reward(party_state.get_next_pending_character_reward())
	var charge_progress = progression.get_skill_progress(&"charge")
	_assert_true(progression.has_knowledge(&"wayfarer_notes"), "知识奖励应被成功入账。")
	_assert_true(charge_progress != null and charge_progress.is_learned, "技能奖励应先于熟练度生效。")
	_assert_eq(int(charge_progress.total_mastery_earned), 20, "技能熟练度奖励应在技能解锁后成功入账。")
	_assert_eq(int(charge_progress.skill_level), 1, "20 点冲锋熟练度应将技能提升到 1 级。")
	_assert_eq(
		progression.unit_base_attributes.get_attribute_value(UnitBaseAttributes.STRENGTH),
		strength_before + 2,
		"属性奖励应在最后稳定落到角色基础属性上。"
	)
	_assert_eq(delta.knowledge_changes.size(), 1, "delta 应记录知识变化。")
	_assert_eq(delta.mastery_changes.size(), 1, "delta 应记录熟练度变化。")
	_assert_eq(delta.attribute_changes.size(), 1, "delta 应记录属性变化。")
	_assert_true(party_state.pending_character_rewards.is_empty(), "奖励确认后应从待处理队列移除。")


func _test_pending_character_reward_round_trip_persists() -> void:
	var party_state := _make_party_state([&"hero"])
	var manager := _setup_manager(party_state, {})
	var reward = manager.build_pending_character_reward(
		&"hero",
		&"persist_reward",
		&"achievement",
		&"persist_reward",
		"持久化奖励",
		[
			{
				"entry_type": String(AchievementRewardDef.TYPE_SKILL_UNLOCK),
				"target_id": "charge",
				"target_label": "冲锋",
				"amount": 1,
			},
		],
		"用于序列化测试"
	)
	party_state.enqueue_pending_character_reward(reward)

	var progress_state := AchievementProgressState.new()
	progress_state.achievement_id = &"battle_won_first"
	progress_state.current_value = 1
	progress_state.is_unlocked = true
	progress_state.unlocked_at_unix_time = 123456
	party_state.get_member_state(&"hero").progression.set_achievement_progress_state(progress_state)

	var serialized_progress := ProgressionSerialization.serialize_achievement_progress_state(progress_state)
	var round_trip_progress = ProgressionSerialization.deserialize_achievement_progress_state(serialized_progress)
	_assert_eq(round_trip_progress.achievement_id, &"battle_won_first", "成就进度序列化后应保留 achievement_id。")
	_assert_true(round_trip_progress.is_unlocked, "成就进度序列化后应保留解锁状态。")

	var serialized_reward := ProgressionSerialization.serialize_pending_character_reward(reward)
	var round_trip_reward = ProgressionSerialization.deserialize_pending_character_reward(serialized_reward)
	_assert_eq(round_trip_reward.member_id, &"hero", "奖励序列化后应保留 member_id。")
	_assert_eq(round_trip_reward.entries.size(), 1, "奖励序列化后应保留条目数量。")

	var restored_party_state = PartyState.from_dict(party_state.to_dict())
	var restored_member = restored_party_state.get_member_state(&"hero")
	var restored_progress = restored_member.progression.get_achievement_progress_state(&"battle_won_first")
	_assert_eq(restored_party_state.pending_character_rewards.size(), 1, "未确认奖励应通过 PartyState 存档往返恢复。")
	_assert_eq(restored_party_state.pending_character_rewards[0].source_id, &"persist_reward", "恢复后的奖励应保留来源 ID。")
	_assert_true(restored_progress != null and restored_progress.is_unlocked, "成就进度应随 PartyState 一并恢复。")


func _test_legacy_pending_mastery_rewards_still_convert() -> void:
	var party_state := _make_party_state([&"hero"])
	var manager := _setup_manager(party_state, {})
	_assert_true(manager.learn_skill(&"hero", &"warrior_heavy_strike"), "前置条件：hero 应能学会重击。")

	var raw_data := party_state.to_dict()
	raw_data.erase("pending_character_rewards")
	raw_data["pending_mastery_rewards"] = [
		{
			"member_id": "hero",
			"member_name": "Hero",
			"source_type": "training",
			"source_label": "旧版训练奖励",
			"summary_text": "旧版数据兼容",
			"mastery_entries": [
				{
					"skill_id": "warrior_heavy_strike",
					"skill_name": "重击",
					"mastery_amount": 20,
					"reason_text": "legacy",
				},
			],
		},
	]

	var restored_party_state = PartyState.from_dict(raw_data)
	_assert_eq(restored_party_state.pending_character_rewards.size(), 1, "旧 pending_mastery_rewards 应自动转换为新奖励队列。")
	_assert_eq(
		restored_party_state.pending_character_rewards[0].entries[0].entry_type,
		AchievementRewardDef.TYPE_SKILL_MASTERY,
		"旧 mastery_entries 应转换成 skill_mastery 条目。"
	)

	var restored_manager := _setup_manager(restored_party_state, {})
	var delta = restored_manager.apply_pending_character_reward(restored_party_state.get_next_pending_character_reward())
	var basic_sword_progress = restored_party_state.get_member_state(&"hero").progression.get_skill_progress(&"warrior_heavy_strike")
	_assert_eq(delta.mastery_changes.size(), 1, "旧版奖励转换后仍应能正确结算熟练度变化。")
	_assert_eq(int(basic_sword_progress.total_mastery_earned), 20, "旧版奖励转换后应正确写入技能熟练度。")
	_assert_true(restored_party_state.pending_character_rewards.is_empty(), "旧版奖励转换后消费完成也应正确出队。")


func _test_battle_achievement_only_queues_reward_without_mutating_runtime_unit() -> void:
	var party_state := _make_party_state([&"hero"])
	var achievement_defs := {
		&"battle_unlock_charge": _make_achievement(
			&"battle_unlock_charge",
			"战后冲锋",
			&"battle_won",
			1,
			[
				_make_reward(
					AchievementRewardDef.TYPE_SKILL_UNLOCK,
					&"charge",
					1,
					"冲锋"
				),
			]
		),
	}
	var manager := _setup_manager(party_state, achievement_defs)
	_assert_true(manager.learn_skill(&"hero", &"warrior_heavy_strike"), "前置条件：hero 应能学会重击。")

	var runtime_unit = manager.build_battle_party([&"hero"])[0]
	var skill_ids_before = runtime_unit.known_active_skill_ids.duplicate()

	var unlocked_ids := manager.record_achievement_event(&"hero", &"battle_won", 1)
	var future_unit = manager.build_battle_party([&"hero"])[0]
	var charge_progress = party_state.get_member_state(&"hero").progression.get_skill_progress(&"charge")

	_assert_eq(unlocked_ids.size(), 1, "战斗成就应被正确解锁。")
	_assert_eq(party_state.pending_character_rewards.size(), 1, "战斗成就解锁后应只进入待领奖励队列。")
	_assert_true(charge_progress == null or not charge_progress.is_learned, "奖励确认前不应把技能直接写入角色成长。")
	_assert_true(
		not runtime_unit.known_active_skill_ids.has(&"charge"),
		"当前 BattleUnitState 不应被战斗中达成的成就即时改写。"
	)
	_assert_eq(runtime_unit.known_active_skill_ids, skill_ids_before, "当前 BattleUnitState 的技能列表应保持原样。")
	_assert_true(
		not future_unit.known_active_skill_ids.has(&"charge"),
		"奖励确认前，即使重新构造战斗单位也不应提前拿到成就奖励技能。"
	)


func _test_party_management_window_renders_achievement_summary() -> void:
	var registry := ProgressionContentRegistry.new()
	var party_state := _make_party_state([&"hero"])
	var member_state: PartyMemberState = party_state.get_member_state(&"hero")

	var unlocked_progress := AchievementProgressState.new()
	unlocked_progress.achievement_id = &"battle_won_first"
	unlocked_progress.current_value = 1
	unlocked_progress.is_unlocked = true
	unlocked_progress.unlocked_at_unix_time = 500
	member_state.progression.set_achievement_progress_state(unlocked_progress)

	var in_progress := AchievementProgressState.new()
	in_progress.achievement_id = &"enemy_defeated_apprentice"
	in_progress.current_value = 2
	in_progress.is_unlocked = false
	member_state.progression.set_achievement_progress_state(in_progress)

	var window = PartyManagementWindowScene.instantiate()
	root.add_child(window)
	await process_frame
	window.set_achievement_defs(registry.get_achievement_defs())
	window.show_party(party_state)
	await process_frame

	var details_text: String = String(window.details_label.text)
	_assert_text_contains(details_text, "成就摘要：", "队伍管理窗口应显示成就摘要标题。")
	_assert_text_contains(details_text, "已解锁：1", "队伍管理窗口应显示已解锁成就数。")
	_assert_text_contains(details_text, "进行中：1", "队伍管理窗口应显示进行中成就数。")
	_assert_text_contains(details_text, "最近解锁：首战归来", "队伍管理窗口应显示最近解锁成就名。")
	_assert_text_contains(details_text, "- 开刃 2 / 3", "队伍管理窗口应显示进行中的成就进度。")

	window.queue_free()
	await process_frame


func _build_archer_design_skill_ids() -> Array[StringName]:
	return [
		&"archer_aimed_shot",
		&"archer_armor_piercer",
		&"archer_heartseeker",
		&"archer_long_draw",
		&"archer_split_bolt",
		&"archer_execution_arrow",
		&"archer_double_nock",
		&"archer_far_horizon",
		&"archer_skirmish_step",
		&"archer_backstep_shot",
		&"archer_sidewind_slide",
		&"archer_running_shot",
		&"archer_grapple_redeploy",
		&"archer_evasive_roll",
		&"archer_highground_claim",
		&"archer_hunter_feint",
		&"archer_pinning_shot",
		&"archer_tendon_splitter",
		&"archer_disrupting_arrow",
		&"archer_flash_whistle",
		&"archer_tripwire_arrow",
		&"archer_shield_breaker",
		&"archer_fearsignal_shot",
		&"archer_harrier_mark",
		&"archer_multishot",
		&"archer_arrow_rain",
		&"archer_fan_volley",
		&"archer_suppressive_fire",
		&"archer_breach_barrage",
		&"archer_blast_arrow",
		&"archer_hunting_grid",
		&"archer_killing_field",
	]


func _setup_manager(party_state: PartyState, achievement_defs = null) -> CharacterManagementModule:
	var registry := ProgressionContentRegistry.new()
	var manager := CharacterManagementModule.new()
	manager.setup(
		party_state,
		registry.get_skill_defs(),
		registry.get_profession_defs(),
		achievement_defs if achievement_defs != null else registry.get_achievement_defs()
	)
	return manager


func _make_party_state(member_ids: Array[StringName]) -> PartyState:
	var party_state := PartyState.new()
	for member_id in member_ids:
		var member_state := PartyMemberState.new()
		member_state.member_id = member_id
		member_state.display_name = String(member_id).capitalize()
		member_state.progression.unit_id = member_id
		member_state.progression.display_name = member_state.display_name
		member_state.progression.character_level = 1
		member_state.progression.unit_base_attributes.set_attribute_value(UnitBaseAttributes.STRENGTH, 3)
		member_state.progression.unit_base_attributes.set_attribute_value(UnitBaseAttributes.AGILITY, 2)
		member_state.current_hp = 18
		member_state.current_mp = 6
		party_state.set_member_state(member_state)
		party_state.active_member_ids.append(member_id)
		if party_state.leader_member_id == &"":
			party_state.leader_member_id = member_id
	return party_state


func _make_achievement(
	achievement_id: StringName,
	display_name: String,
	event_type: StringName,
	threshold: int,
	rewards: Array[AchievementRewardDef],
	subject_id: StringName = &""
) -> AchievementDef:
	var achievement := AchievementDef.new()
	achievement.achievement_id = achievement_id
	achievement.display_name = display_name
	achievement.description = "%s 的测试定义" % display_name
	achievement.event_type = event_type
	achievement.subject_id = subject_id
	achievement.threshold = threshold
	achievement.rewards = rewards.duplicate()
	return achievement


func _make_reward(
	reward_type: StringName,
	target_id: StringName,
	amount: int,
	target_label: String = ""
) -> AchievementRewardDef:
	var reward := AchievementRewardDef.new()
	reward.reward_type = reward_type
	reward.target_id = target_id
	reward.target_label = target_label if not target_label.is_empty() else String(target_id)
	reward.amount = amount
	reward.reason_text = "测试奖励"
	return reward


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_text_contains(text: String, expected_fragment: String, message: String) -> void:
	if text.contains(expected_fragment):
		return
	_failures.append("%s | missing=%s text=%s" % [message, expected_fragment, text])
