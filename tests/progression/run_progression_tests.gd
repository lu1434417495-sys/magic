## 文件说明：该脚本属于成长测试执行相关的回归测试脚本，集中维护失败信息等顶层字段。
## 审查重点：重点核对测试数据、字段用途、断言条件和失败提示是否仍然覆盖目标回归场景。
## 备注：后续如果业务规则变化，需要同步更新测试夹具、预期结果和失败信息。

extends SceneTree

const AchievementDef = preload("res://scripts/player/progression/achievement_def.gd")
const AchievementProgressState = preload("res://scripts/player/progression/achievement_progress_state.gd")
const AchievementRewardDef = preload("res://scripts/player/progression/achievement_reward_def.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle_runtime_module.gd")
const BattleUnitFactory = preload("res://scripts/systems/battle_unit_factory.gd")
const CharacterManagementModule = preload("res://scripts/systems/character_management_module.gd")
const GameSession = preload("res://scripts/systems/game_session.gd")
const PARTY_WAREHOUSE_SERVICE_SCRIPT = preload("res://scripts/systems/party_warehouse_service.gd")
const PartyManagementWindowScene = preload("res://scenes/ui/party_management_window.tscn")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const ProgressionSerialization = preload("res://scripts/systems/progression_serialization.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")
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
	_test_new_game_random_skill_tier_mapping_uses_representative_defs()
	_test_random_start_skill_pool_excludes_composite_upgrade_skills()
	_test_seed_growth_achievement_events_unlock_via_real_progression_actions()
	_test_saint_blade_combo_unlock_chain_requires_knowledge_levels_and_achievement()
	_test_composite_upgrade_replace_sources_with_result_keeps_sources_and_transitions_core()
	_test_achievement_progress_is_member_scoped_and_unlocks_once()
	_test_single_event_can_unlock_multiple_achievements_in_queue_order()
	_test_pending_character_reward_applies_in_stable_order()
	_test_pending_character_reward_round_trip_persists()
	_test_quest_reward_pending_character_materializer()
	_test_research_pending_character_reward_preserves_queue_naming_and_triggers_growth_events()
	_test_submit_item_objective_materializer_tracks_progress_and_failures()
	_test_party_state_quest_round_trip_persists()
	_test_party_state_quest_buckets_stay_mutually_exclusive()
	_test_battle_achievement_only_queues_reward_without_mutating_runtime_unit()
	await _test_party_management_window_renders_achievement_summary()
	await _test_party_management_window_keeps_main_character_active()

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


func _test_new_game_random_skill_tier_mapping_uses_representative_defs() -> void:
	var session := GameSession.new()
	var skill_defs := session.get_skill_defs()
	_assert_eq(
		session._resolve_random_start_skill_initial_level(skill_defs.get(&"warrior_heavy_strike")),
		3,
		"基础技能在新开局随机授予时应初始化为 3 级。"
	)
	_assert_eq(
		session._resolve_random_start_skill_initial_level(skill_defs.get(&"mage_molten_burst")),
		2,
		"带有中段信号的技能在新开局随机授予时应初始化为 2 级。"
	)
	_assert_eq(
		session._resolve_random_start_skill_initial_level(skill_defs.get(&"mage_comet_drop")),
		1,
		"带有高阶信号的技能在新开局随机授予时应初始化为 1 级。"
	)
	_assert_eq(
		session._resolve_random_start_skill_initial_level(skill_defs.get(&"warrior_true_dragon_slash")),
		0,
		"终极技能在新开局随机授予时应初始化为 0 级。"
	)
	session.free()


func _test_random_start_skill_pool_excludes_composite_upgrade_skills() -> void:
	var session := GameSession.new()
	var skill_defs := session.get_skill_defs()
	var progression := UnitProgress.new()
	var standard_book_skill := skill_defs.get(&"warrior_heavy_strike") as SkillDef
	var composite_book_skill := skill_defs.get(&"saint_blade_combo") as SkillDef

	_assert_true(
		session._is_random_start_book_skill_candidate(standard_book_skill, progression),
		"普通技能书技能应保留在随机起始技能池中。"
	)
	_assert_true(
		not session._is_random_start_book_skill_candidate(composite_book_skill, progression),
		"复合升级技能不应进入随机起始技能池。"
	)
	session.free()


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


func _test_saint_blade_combo_unlock_chain_requires_knowledge_levels_and_achievement() -> void:
	var party_state := _make_party_state([&"hero"])
	var achievement_defs := {
		&"six_hit_combo": _make_achievement(
			&"six_hit_combo",
			"六击连斩",
			&"skill_used",
			6,
			[
				_make_reward(
					AchievementRewardDef.TYPE_ATTRIBUTE_DELTA,
					UnitBaseAttributes.STRENGTH,
					1,
					"力量"
				),
			],
			&"warrior_combo_strike"
		),
	}
	var manager := _setup_manager(party_state, achievement_defs)
	var progression: UnitProgress = party_state.get_member_state(&"hero").progression

	_assert_true(manager.learn_skill(&"hero", &"charge"), "前置条件：hero 应能学会冲锋。")
	_assert_true(manager.learn_skill(&"hero", &"warrior_combo_strike"), "前置条件：hero 应能学会连击。")
	_assert_true(manager.learn_skill(&"hero", &"warrior_aura_slash"), "前置条件：hero 应能学会斗气斩。")
	_assert_true(
		manager.grant_battle_mastery(&"hero", &"warrior_combo_strike", 999).mastery_changes.size() > 0,
		"连击应能通过真实熟练度成长提升到满级。"
	)
	_assert_true(
		manager.grant_battle_mastery(&"hero", &"warrior_aura_slash", 999).mastery_changes.size() > 0,
		"斗气斩应能通过真实熟练度成长提升到满级。"
	)
	_assert_eq(int(progression.get_skill_progress(&"warrior_combo_strike").skill_level), 5, "连击应达到 5 级。")
	_assert_eq(int(progression.get_skill_progress(&"warrior_aura_slash").skill_level), 5, "斗气斩应达到 5 级。")

	_assert_true(
		not manager.learn_skill(&"hero", &"saint_blade_combo"),
		"缺少知识、等级与成就时不应提前解锁圣剑连斩。"
	)

	_assert_true(
		manager.learn_knowledge(&"hero", &"compania_family_legacy"),
		"真实成长链应允许 hero 学会圣剑连斩前置知识。"
	)
	_assert_true(
		not manager.learn_skill(&"hero", &"saint_blade_combo"),
		"缺少成就条件时不应提前解锁圣剑连斩。"
	)

	var achievement_unlocks := manager.record_achievement_event(&"hero", &"skill_used", 6, &"warrior_combo_strike")
	_assert_eq(achievement_unlocks.size(), 1, "真实技能使用事件应能解锁测试成就。")
	_assert_true(
		progression.get_achievement_progress_state(&"six_hit_combo").is_unlocked,
		"测试成就应在真实事件后解锁。"
	)

	_assert_true(
		manager.learn_skill(&"hero", &"saint_blade_combo"),
		"满足知识、双技能等级与成就条件后应能解锁圣剑连斩。"
	)

	var combo_progress = progression.get_skill_progress(&"saint_blade_combo")
	_assert_true(combo_progress != null and combo_progress.is_learned, "圣剑连斩应被真正写入成长进度。")
	_assert_eq(
		progression.get_merged_source_skill_ids(&"saint_blade_combo"),
		[&"warrior_combo_strike", &"warrior_aura_slash"],
		"圣剑连斩应保留来源技能血缘。"
	)
	_assert_true(
		progression.get_skill_progress(&"warrior_combo_strike").is_learned
		and progression.get_skill_progress(&"warrior_aura_slash").is_learned,
		"解锁圣剑连斩后不应删除来源技能。"
	)


func _test_composite_upgrade_replace_sources_with_result_keeps_sources_and_transitions_core() -> void:
	var registry := ProgressionContentRegistry.new()
	var progress := UnitProgress.new()
	progress.unit_id = &"hero"
	progress.display_name = "Hero"

	var warrior_progress := UnitProfessionProgress.new()
	warrior_progress.profession_id = &"warrior"
	warrior_progress.rank = 1
	progress.set_profession_progress(warrior_progress)

	for source_skill_id in [&"warrior_combo_strike", &"warrior_aura_slash"]:
		var source_progress := UnitSkillProgress.new()
		source_progress.skill_id = source_skill_id
		source_progress.is_learned = true
		source_progress.skill_level = 5
		source_progress.is_core = true
		source_progress.assigned_profession_id = &"warrior"
		progress.set_skill_progress(source_progress)
		warrior_progress.add_core_skill(source_skill_id)

	var merge_service := SkillMergeService.new()
	merge_service.setup(progress, registry.get_skill_defs(), null)

	_assert_true(
		merge_service.apply_composite_upgrade_result(
			&"saint_blade_combo",
			[&"warrior_combo_strike", &"warrior_aura_slash"],
			true,
			&"replace_sources_with_result"
		),
		"replace_sources_with_result 应能在保留来源技能时完成复合升级。"
	)

	var combo_progress = progress.get_skill_progress(&"saint_blade_combo")
	_assert_true(combo_progress != null and combo_progress.is_learned, "复合升级结果应被写入成长进度。")
	_assert_true(combo_progress.is_core, "复合升级结果应接管核心位。")
	_assert_eq(combo_progress.assigned_profession_id, &"warrior", "复合升级结果应继承原职业核心位。")
	_assert_true(
		progress.get_skill_progress(&"warrior_combo_strike").is_learned
		and not progress.get_skill_progress(&"warrior_combo_strike").is_core,
		"来源技能应保留，但不再占用核心位。"
	)
	_assert_true(
		progress.get_skill_progress(&"warrior_aura_slash").is_learned
		and not progress.get_skill_progress(&"warrior_aura_slash").is_core,
		"另一条来源技能也应保留，但不再占用核心位。"
	)
	_assert_true(
		warrior_progress.core_skill_ids.has(&"saint_blade_combo")
		and not warrior_progress.core_skill_ids.has(&"warrior_combo_strike")
		and not warrior_progress.core_skill_ids.has(&"warrior_aura_slash"),
		"职业核心列表应从来源技能切换到结果技能。"
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


func _test_quest_reward_pending_character_materializer() -> void:
	var party_state := _make_party_state([&"hero"])
	var registry := ProgressionContentRegistry.new()
	var quest_def := QuestDef.new()
	quest_def.quest_id = &"contract_growth_drill"
	quest_def.display_name = "成长演练"
	quest_def.objective_defs = [
		{
			"objective_id": "report_back",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_id": "service_contract_board",
			"target_value": 1,
		},
	]
	quest_def.reward_entries = [
		{
			"reward_type": QuestDef.REWARD_PENDING_CHARACTER_REWARD,
			"member_id": "hero",
			"summary_text": "完成演练后获得成长奖励。",
			"entries": [
				{
					"entry_type": String(AchievementRewardDef.TYPE_SKILL_UNLOCK),
					"target_id": "charge",
					"target_label": "冲锋",
					"amount": 1,
				},
				{
					"entry_type": String(AchievementRewardDef.TYPE_SKILL_MASTERY),
					"target_id": "charge",
					"target_label": "冲锋",
					"amount": 10,
				},
			],
		},
	]

	var manager := CharacterManagementModule.new()
	manager.setup(
		party_state,
		registry.get_skill_defs(),
		registry.get_profession_defs(),
		registry.get_achievement_defs(),
		{},
		{
			quest_def.quest_id: quest_def,
		}
	)
	var claimable_quest := QuestState.new()
	claimable_quest.quest_id = quest_def.quest_id
	claimable_quest.mark_accepted(6)
	claimable_quest.mark_completed(9)
	party_state.set_claimable_quest_state(claimable_quest)

	var claim_result := manager.claim_quest_reward(quest_def.quest_id, 12)
	_assert_true(bool(claim_result.get("ok", false)), "quest 的 pending_character_reward 应能正式入队。")
	_assert_eq((claim_result.get("pending_character_rewards", []) as Array).size(), 1, "claim 结果应暴露 materialized 角色奖励。")
	_assert_eq(party_state.pending_character_rewards.size(), 1, "quest 的成长奖励应进入正式 pending_character_rewards。")
	_assert_true(not party_state.has_claimable_quest(quest_def.quest_id), "领奖成功后任务应离开 claimable_quests。")
	_assert_true(party_state.has_completed_quest(quest_def.quest_id), "领奖成功后任务应进入 completed_quest_ids。")

	var queued_reward = party_state.get_next_pending_character_reward()
	_assert_true(queued_reward != null, "materialized reward 应能从正式奖励队列取出。")
	if queued_reward != null:
		_assert_eq(queued_reward.member_id, &"hero", "quest reward 应保留目标成员。")
		_assert_eq(queued_reward.source_id, quest_def.quest_id, "quest reward 应默认把 quest_id 作为来源 ID。")
		_assert_eq(queued_reward.source_label, "成长演练", "quest reward 应默认把 quest 名称作为来源标签。")
		_assert_eq(queued_reward.entries.size(), 2, "quest reward 应保留所有成长条目。")

	var skill_progress = party_state.get_member_state(&"hero").progression.get_skill_progress(&"charge")
	_assert_true(skill_progress == null, "quest claim 后角色奖励应只入队，不应立刻直写成长结果。")


func _test_research_pending_character_reward_preserves_queue_naming_and_triggers_growth_events() -> void:
	var party_state := _make_party_state([&"hero"])
	var manager := _setup_manager(party_state)
	var member_state: PartyMemberState = party_state.get_member_state(&"hero")
	var progression: UnitProgress = member_state.progression
	var perception_before: int = progression.unit_base_attributes.get_attribute_value(UnitBaseAttributes.PERCEPTION)
	var willpower_before: int = progression.unit_base_attributes.get_attribute_value(UnitBaseAttributes.WILLPOWER)

	var knowledge_reward = manager.build_pending_character_reward(
		&"hero",
		&"research_field_manual_reward",
		&"npc_teach",
		&"research_field_manual",
		"大图书官·研究",
		[
			{
				"entry_type": String(AchievementRewardDef.TYPE_KNOWLEDGE_UNLOCK),
				"target_id": "field_manual",
				"target_label": "野外手册",
				"amount": 1,
				"reason_text": "研究员整理出可长期翻阅的野外手册抄本。",
			},
		],
		"研究奖励：野外手册"
	)
	_assert_true(knowledge_reward != null, "知识型 research 奖励应能按正式队列结构构造。")
	manager.enqueue_pending_character_rewards([knowledge_reward])
	var queued_knowledge_reward = party_state.get_next_pending_character_reward()
	_assert_true(queued_knowledge_reward != null, "知识型 research 奖励应进入待处理队列。")
	if queued_knowledge_reward != null:
		_assert_eq(queued_knowledge_reward.source_type, &"npc_teach", "research 奖励应保留正式 source_type。")
		_assert_eq(queued_knowledge_reward.source_id, &"research_field_manual", "research 奖励应保留具体 source_id。")
		_assert_eq(queued_knowledge_reward.source_label, "大图书官·研究", "research 奖励应保留正式 source_label。")

	var knowledge_delta = manager.apply_pending_character_reward(queued_knowledge_reward)
	_assert_true(progression.has_knowledge(&"field_manual"), "知识型 research 奖励确认后应真正学会知识。")
	_assert_eq(knowledge_delta.knowledge_changes.size(), 1, "知识型 research 奖励应记录 knowledge delta。")
	_assert_eq(party_state.pending_character_rewards.size(), 1, "research 学会野外手册后应继续触发正式知识学习成就奖励。")
	_assert_eq(party_state.pending_character_rewards[0].source_id, &"knowledge_learned_field_manual", "研究触发的知识学习成就应沿用正式 achievement source_id。")
	manager.apply_pending_character_reward(party_state.get_next_pending_character_reward())
	_assert_eq(
		progression.unit_base_attributes.get_attribute_value(UnitBaseAttributes.WILLPOWER),
		willpower_before + 1,
		"研究解锁野外手册后，后续知识学习成就奖励应正常结算。"
	)

	var skill_reward = manager.build_pending_character_reward(
		&"hero",
		&"research_guard_break_reward",
		&"npc_teach",
		&"research_guard_break",
		"大图书官·研究",
		[
			{
				"entry_type": String(AchievementRewardDef.TYPE_SKILL_UNLOCK),
				"target_id": "warrior_guard_break",
				"target_label": "裂甲斩",
				"amount": 1,
				"reason_text": "研究记录补全了裂甲斩的动作拆解。",
			},
		],
		"研究奖励：裂甲斩"
	)
	_assert_true(skill_reward != null, "技能型 research 奖励应能按正式队列结构构造。")
	manager.enqueue_pending_character_rewards([skill_reward])
	var queued_skill_reward = party_state.get_next_pending_character_reward()
	_assert_true(queued_skill_reward != null, "技能型 research 奖励应进入待处理队列。")
	if queued_skill_reward != null:
		_assert_eq(queued_skill_reward.source_type, &"npc_teach", "技能型 research 奖励也应保留正式 source_type。")
		_assert_eq(queued_skill_reward.source_id, &"research_guard_break", "技能型 research 奖励应保留具体 source_id。")
		_assert_eq(queued_skill_reward.source_label, "大图书官·研究", "技能型 research 奖励应保留正式 source_label。")

	var skill_delta = manager.apply_pending_character_reward(queued_skill_reward)
	var guard_break_progress = progression.get_skill_progress(&"warrior_guard_break")
	_assert_true(guard_break_progress != null and guard_break_progress.is_learned, "技能型 research 奖励确认后应真正学会裂甲斩。")
	_assert_true(skill_delta.granted_skill_ids.is_empty(), "book 技能 research 奖励不应误记为 profession granted。")
	_assert_eq(party_state.pending_character_rewards.size(), 1, "research 学会裂甲斩后应继续触发正式技能学习成就奖励。")
	_assert_eq(party_state.pending_character_rewards[0].source_id, &"skill_learned_guard_break", "研究触发的技能学习成就应沿用正式 achievement source_id。")
	manager.apply_pending_character_reward(party_state.get_next_pending_character_reward())
	_assert_eq(
		progression.unit_base_attributes.get_attribute_value(UnitBaseAttributes.PERCEPTION),
		perception_before + 1,
		"研究解锁裂甲斩后，后续技能学习成就奖励应正常结算。"
	)


func _test_submit_item_objective_materializer_tracks_progress_and_failures() -> void:
	var party_state := _make_party_state([&"hero"])
	party_state.get_member_state(&"hero").progression.unit_base_attributes.set_attribute_value(
		PARTY_WAREHOUSE_SERVICE_SCRIPT.STORAGE_SPACE_ATTRIBUTE_ID,
		4
	)
	var registry := ProgressionContentRegistry.new()
	var session := GameSession.new()
	var item_defs := session.get_item_defs()

	var submit_item_quest := QuestDef.new()
	submit_item_quest.quest_id = &"contract_supply_delivery"
	submit_item_quest.display_name = "物资缴纳"
	submit_item_quest.objective_defs = [
		{
			"objective_id": "deliver_ore",
			"objective_type": QuestDef.OBJECTIVE_SUBMIT_ITEM,
			"target_id": "iron_ore",
			"target_value": 2,
		},
	]
	var submit_item_shortage_quest := QuestDef.new()
	submit_item_shortage_quest.quest_id = &"contract_supply_delivery_shortage"
	submit_item_shortage_quest.display_name = "物资缴纳缺料"
	submit_item_shortage_quest.objective_defs = submit_item_quest.objective_defs.duplicate(true)
	var submit_item_wrong_item_quest := QuestDef.new()
	submit_item_wrong_item_quest.quest_id = &"contract_supply_delivery_wrong_item"
	submit_item_wrong_item_quest.display_name = "物资缴纳错货"
	submit_item_wrong_item_quest.objective_defs = submit_item_quest.objective_defs.duplicate(true)

	var manager := CharacterManagementModule.new()
	manager.setup(
		party_state,
		registry.get_skill_defs(),
		registry.get_profession_defs(),
		registry.get_achievement_defs(),
		item_defs,
		{
			submit_item_quest.quest_id: submit_item_quest,
			submit_item_shortage_quest.quest_id: submit_item_shortage_quest,
			submit_item_wrong_item_quest.quest_id: submit_item_wrong_item_quest,
		}
	)
	var warehouse_service = PARTY_WAREHOUSE_SERVICE_SCRIPT.new()
	warehouse_service.setup(party_state, item_defs)

	var partial_submit_quest := QuestState.new()
	partial_submit_quest.quest_id = submit_item_quest.quest_id
	partial_submit_quest.mark_accepted(3)
	partial_submit_quest.record_objective_progress(&"deliver_ore", 1, 2, {"item_id": "iron_ore", "submitted_quantity": 1})
	party_state.set_active_quest_state(partial_submit_quest)
	warehouse_service.add_item(&"iron_ore", 1)
	var partial_submit_result := manager.submit_item_objective(submit_item_quest.quest_id, &"deliver_ore", 4)
	warehouse_service.setup(party_state, item_defs)
	_assert_true(bool(partial_submit_result.get("ok", false)), "submit_item 成功时应通过 CharacterManagementModule 推进正式 objective。")
	_assert_eq(int(partial_submit_result.get("submitted_quantity", 0)), 1, "已有部分进度时 submit_item 只应扣除剩余所需数量。")
	_assert_true(not party_state.has_active_quest(submit_item_quest.quest_id), "submit_item 完成后任务应离开 active_quests。")
	_assert_true(party_state.has_claimable_quest(submit_item_quest.quest_id), "submit_item 完成后任务应进入 claimable_quests。")
	_assert_eq(warehouse_service.count_item(&"iron_ore"), 0, "submit_item 成功后共享仓库应只扣除剩余所需的铁矿石。")
	var claimable_submit_item_quest: QuestState = party_state.get_claimable_quest_state(submit_item_quest.quest_id)
	_assert_true(claimable_submit_item_quest != null, "submit_item 完成后应保留可领奖的 QuestState。")
	if claimable_submit_item_quest != null:
		_assert_eq(claimable_submit_item_quest.get_objective_progress(&"deliver_ore"), 2, "submit_item 成功后 objective_progress 应补到目标值。")
		_assert_eq(int(claimable_submit_item_quest.last_progress_context.get("submitted_quantity", 0)), 1, "submit_item 成功后 QuestState 应记录实际扣除数量。")
		_assert_eq(String(claimable_submit_item_quest.last_progress_context.get("item_id", "")), "iron_ore", "submit_item 成功后 QuestState 应记录正式提交物品。")
		_assert_eq(claimable_submit_item_quest.completed_at_world_step, 4, "submit_item 完成后 QuestState 应记录完成 world_step。")

	var submit_item_shortage_state := QuestState.new()
	submit_item_shortage_state.quest_id = submit_item_shortage_quest.quest_id
	submit_item_shortage_state.mark_accepted(5)
	party_state.set_active_quest_state(submit_item_shortage_state)
	var remaining_iron_ore := warehouse_service.count_item(&"iron_ore")
	if remaining_iron_ore > 0:
		warehouse_service.remove_item(&"iron_ore", remaining_iron_ore)
	var shortage_submit_result := manager.submit_item_objective(submit_item_shortage_quest.quest_id, &"deliver_ore", 6)
	_assert_true(not bool(shortage_submit_result.get("ok", true)), "共享仓库缺料时 submit_item 应正式失败。")
	_assert_eq(String(shortage_submit_result.get("error_code", "")), "submit_item_missing_inventory", "缺料时 submit_item 应返回正式缺料错误码。")
	var active_shortage_quest: QuestState = party_state.get_active_quest_state(submit_item_shortage_quest.quest_id)
	_assert_true(active_shortage_quest != null, "缺料时任务应继续停留在 active_quests。")
	if active_shortage_quest != null:
		_assert_eq(active_shortage_quest.get_objective_progress(&"deliver_ore"), 0, "缺料时不应推进 quest objective。")
	_assert_true(not party_state.has_claimable_quest(submit_item_shortage_quest.quest_id), "缺料时任务不应误进入 claimable_quests。")

	var submit_item_wrong_item_state := QuestState.new()
	submit_item_wrong_item_state.quest_id = submit_item_wrong_item_quest.quest_id
	submit_item_wrong_item_state.mark_accepted(7)
	party_state.set_active_quest_state(submit_item_wrong_item_state)
	var remaining_bronze_sword := warehouse_service.count_item(&"bronze_sword")
	if remaining_bronze_sword > 0:
		warehouse_service.remove_item(&"bronze_sword", remaining_bronze_sword)
	warehouse_service.add_item(&"bronze_sword", 1)
	var bronze_sword_count_before_wrong_submit := warehouse_service.count_item(&"bronze_sword")
	var wrong_item_submit_result := manager.submit_item_objective(submit_item_wrong_item_quest.quest_id, &"deliver_ore", 8)
	warehouse_service.setup(party_state, item_defs)
	_assert_true(not bool(wrong_item_submit_result.get("ok", true)), "仓库只有错误物品时 submit_item 应正式失败。")
	_assert_eq(String(wrong_item_submit_result.get("error_code", "")), "submit_item_missing_inventory", "错误物品时 submit_item 仍应返回缺少目标物资。")
	_assert_eq(warehouse_service.count_item(&"bronze_sword"), bronze_sword_count_before_wrong_submit, "错误物品时不应误吞共享仓库中的其他物资。")
	var active_wrong_item_quest: QuestState = party_state.get_active_quest_state(submit_item_wrong_item_quest.quest_id)
	_assert_true(active_wrong_item_quest != null, "错误物品时任务应继续停留在 active_quests。")
	if active_wrong_item_quest != null:
		_assert_eq(active_wrong_item_quest.get_objective_progress(&"deliver_ore"), 0, "错误物品时不应推进 quest objective。")
	_assert_true(not party_state.has_claimable_quest(submit_item_wrong_item_quest.quest_id), "错误物品时任务不应误进入 claimable_quests。")
	session.free()


func _test_party_state_quest_round_trip_persists() -> void:
	var party_state := _make_party_state([&"hero"])
	var quest_def := QuestDef.new()
	quest_def.quest_id = &"contract_wolf_pack"
	quest_def.objective_defs = [
		{
			"objective_id": "defeat_wolves",
			"objective_type": QuestDef.OBJECTIVE_DEFEAT_ENEMY,
			"target_value": 3,
		},
		{
			"objective_id": "report_back",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_value": 1,
		},
	]

	var active_quest := QuestState.new()
	active_quest.quest_id = quest_def.quest_id
	active_quest.mark_accepted(4)
	active_quest.record_objective_progress(&"defeat_wolves", 2, 3, {"enemy_template_id": "wolf_raider"})
	party_state.set_active_quest_state(active_quest)
	var claimable_quest := QuestState.new()
	claimable_quest.quest_id = &"contract_settlement_warehouse"
	claimable_quest.mark_accepted(3)
	claimable_quest.mark_completed(7)
	party_state.set_claimable_quest_state(claimable_quest)
	party_state.add_completed_quest_id(&"intro_contract")

	var serialized_party_state := ProgressionSerialization.serialize_party_state(party_state)
	var restored_party_state = ProgressionSerialization.deserialize_party_state(serialized_party_state)
	var restored_quest: QuestState = restored_party_state.get_active_quest_state(&"contract_wolf_pack")
	var restored_claimable_quest: QuestState = restored_party_state.get_claimable_quest_state(&"contract_settlement_warehouse")
	_assert_true(restored_quest != null, "QuestState 应随 PartyState 一起序列化往返恢复。")
	_assert_eq(restored_party_state.version, 3, "新增 quest schema 后 PartyState.version 应升级到 3。")
	_assert_eq(restored_quest.get_objective_progress(&"defeat_wolves"), 2, "QuestState 进度应在往返后保持稳定。")
	_assert_eq(restored_quest.accepted_at_world_step, 4, "QuestState 接取时间应在往返后保持稳定。")
	_assert_true(restored_claimable_quest != null, "待领奖励 QuestState 应随 PartyState 一起序列化往返恢复。")
	if restored_claimable_quest != null:
		_assert_eq(restored_claimable_quest.completed_at_world_step, 7, "待领奖励 QuestState 完成时间应在往返后保持稳定。")
	_assert_true(restored_party_state.has_completed_quest(&"intro_contract"), "completed_quest_ids 应随 PartyState 一起序列化往返恢复。")

	restored_quest.record_objective_progress(&"defeat_wolves", 1, 3, {"enemy_template_id": "wolf_raider"})
	restored_quest.record_objective_progress(&"report_back", 1, 1, {"settlement_id": "spring_village_01"})
	_assert_true(restored_quest.has_completed_all_objectives(quest_def), "恢复后的 QuestState 应能继续驱动 objective 完成判断。")


func _test_party_state_quest_buckets_stay_mutually_exclusive() -> void:
	var party_state := _make_party_state([&"hero"])
	var active_quest := QuestState.new()
	active_quest.quest_id = &"contract_overlap"
	active_quest.mark_accepted(2)
	party_state.set_active_quest_state(active_quest)
	_assert_true(party_state.has_active_quest(&"contract_overlap"), "active quest 写入后应进入 active bucket。")

	var claimable_quest := QuestState.new()
	claimable_quest.quest_id = &"contract_overlap"
	claimable_quest.mark_accepted(2)
	claimable_quest.mark_completed(5)
	party_state.set_claimable_quest_state(claimable_quest)
	_assert_true(not party_state.has_active_quest(&"contract_overlap"), "切到 claimable bucket 时应自动离开 active bucket。")
	_assert_true(party_state.has_claimable_quest(&"contract_overlap"), "切到 claimable bucket 时应保留 claimable quest。")
	_assert_true(not party_state.has_completed_quest(&"contract_overlap"), "切到 claimable bucket 时不应同时残留 completed id。")

	party_state.add_completed_quest_id(&"contract_overlap")
	_assert_true(not party_state.has_claimable_quest(&"contract_overlap"), "切到 completed bucket 时应自动离开 claimable bucket。")
	_assert_true(party_state.has_completed_quest(&"contract_overlap"), "切到 completed bucket 时应写入 completed id。")
	_assert_true(not party_state.has_active_quest(&"contract_overlap"), "切到 completed bucket 时不应残留 active quest。")

	var reopened_quest := QuestState.new()
	reopened_quest.quest_id = &"contract_overlap"
	reopened_quest.mark_accepted(9)
	party_state.set_active_quest_state(reopened_quest)
	_assert_true(party_state.has_active_quest(&"contract_overlap"), "重新接取 quest 时应回到 active bucket。")
	_assert_true(not party_state.has_claimable_quest(&"contract_overlap"), "重新接取 quest 时不应残留 claimable bucket。")
	_assert_true(not party_state.has_completed_quest(&"contract_overlap"), "重新接取 quest 时应从 completed bucket 移除。")


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
	_assert_true(not manager.has_method("build_battle_party"), "CharacterManagementModule 不应再暴露战斗编队构建 API。")
	_assert_true(not manager.has_method("refresh_battle_unit"), "CharacterManagementModule 不应再暴露战斗单位刷新 API。")

	var unit_factory := BattleUnitFactory.new()
	var runtime_unit = unit_factory.build_ally_units(party_state, {})[0]
	var skill_ids_before = runtime_unit.known_active_skill_ids.duplicate()

	var unlocked_ids := manager.record_achievement_event(&"hero", &"battle_won", 1)
	var future_unit = unit_factory.build_ally_units(party_state, {})[0]
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


func _test_party_management_window_keeps_main_character_active() -> void:
	var party_state := _make_party_state([&"hero", &"mage", &"healer"])
	party_state.main_character_member_id = &"hero"
	party_state.active_member_ids = [&"hero", &"mage"]
	party_state.reserve_member_ids = [&"healer"]

	var window = PartyManagementWindowScene.instantiate()
	root.add_child(window)
	await process_frame
	window.show_party(party_state)
	await process_frame

	_assert_true(window.select_member(&"hero"), "队伍管理窗口应能选中主角。")
	_assert_true(window.move_to_reserve_button.disabled, "主角必须保持上阵时，窗口应禁用下阵按钮。")
	window._on_move_to_reserve_button_pressed()
	_assert_true(window._active_member_ids.has(&"hero"), "点击禁用按钮后，窗口内部 active roster 不应丢失主角。")
	_assert_true(not window._reserve_member_ids.has(&"hero"), "点击禁用按钮后，窗口内部 reserve roster 不应出现主角。")
	_assert_text_contains(String(window.status_label.text), "主角必须保持上阵", "尝试下阵主角时应显示明确提示。")
	_assert_text_contains(String(window.details_label.text), "主角：是", "主角详情应显式标记主角身份。")

	_assert_true(window.select_member(&"mage"), "队伍管理窗口应能选中普通上阵成员。")
	_assert_true(not window.move_to_reserve_button.disabled, "非主角的上阵成员在人数允许时仍可下阵。")

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
		if party_state.main_character_member_id == &"":
			party_state.main_character_member_id = member_id
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
