## 文件说明：该脚本属于成长测试执行相关的回归测试脚本，集中维护失败信息等顶层字段。
## 审查重点：重点核对测试数据、字段用途、断言条件和失败提示是否仍然覆盖目标回归场景。
## 备注：后续如果业务规则变化，需要同步更新测试夹具、预期结果和失败信息。

extends SceneTree

const AchievementDef = preload("res://scripts/player/progression/achievement_def.gd")
const AchievementProgressState = preload("res://scripts/player/progression/achievement_progress_state.gd")
const AchievementRewardDef = preload("res://scripts/player/progression/achievement_reward_def.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleUnitFactory = preload("res://scripts/systems/battle/runtime/battle_unit_factory.gd")
const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const AttributeService = preload("res://scripts/systems/attributes/attribute_service.gd")
const GameSession = preload("res://scripts/systems/persistence/game_session.gd")
const PARTY_WAREHOUSE_SERVICE_SCRIPT = preload("res://scripts/systems/inventory/party_warehouse_service.gd")
const PartyManagementWindowScene = preload("res://scenes/ui/party_management_window.tscn")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const ProgressionService = preload("res://scripts/systems/progression/progression_service.gd")
const ProgressionSerialization = preload("res://scripts/systems/persistence/progression_serialization.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const WorldMapSystem = preload("res://scripts/systems/game_runtime/world_map_system.gd")

## 字段说明：记录测试过程中收集到的失败信息，便于最终集中输出并快速定位回归点。
var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_seed_achievement_registry_validates()
	_test_seed_profession_catalog_includes_class_archetypes()
	_test_archer_book_skill_catalog_registers_and_is_learnable()
	_test_manual_skill_learning_rejects_grant_only_sources()
	_test_new_game_random_skill_tier_mapping_uses_representative_defs()
	_test_random_start_skill_pool_excludes_composite_upgrade_skills()
	_test_vajra_body_requires_attributes_and_achievement_and_syncs_battle_status()
	_test_seed_growth_achievement_events_unlock_via_real_progression_actions()
	_test_stamina_max_uses_constitution_strength_and_agility_formula()
	_test_attribute_progress_rewards_convert_below_twenty_and_accumulate_after_cap()
	_test_core_max_skill_queues_attribute_progress_once()
	_test_core_max_skill_ignores_string_name_attribute_growth_key()
	_test_non_core_skill_max_level_cap_lifts_when_core()
	_test_aura_slash_max_level_uses_transformation_count()
	_test_attribute_growth_progress_round_trip_persists()
	_test_unit_progress_from_dict_requires_top_level_schema_fields()
	_test_unit_progress_from_dict_rejects_attribute_and_reputation_schema_defaults()
	_test_unit_progress_from_dict_rejects_pending_profession_choice_schema_defaults()
	_test_unit_progress_from_dict_rejects_child_id_fallbacks()
	_test_unit_progress_from_dict_rejects_child_schema_defaults()
	_test_combat_resource_unlocks_follow_learned_skill_costs()
	_test_starting_and_random_skill_refresh_unlocks_combat_resources()
	_test_combat_skill_level_overrides_accumulate_minimum_level_patches()
	_test_min_only_requirements_ignore_zero_max_value()
	_test_saint_blade_combo_unlock_chain_requires_knowledge_levels_and_achievement()
	_test_composite_upgrade_replace_sources_with_result_keeps_sources_and_transitions_core()
	_test_achievement_progress_is_member_scoped_and_unlocks_once()
	_test_single_event_can_unlock_multiple_achievements_in_queue_order()
	_test_pending_character_reward_applies_in_stable_order()
	_test_pending_character_reward_round_trip_persists()
	_test_party_state_from_dict_rejects_pending_character_reward_schema_defaults()
	_test_quest_reward_pending_character_materializer()
	_test_research_pending_character_reward_preserves_queue_naming_and_triggers_growth_events()
	_test_submit_item_objective_materializer_tracks_progress_and_failures()
	_test_quest_progress_events_require_formal_progress_schema()
	_test_party_state_quest_round_trip_persists()
	_test_party_state_quest_buckets_stay_mutually_exclusive()
	_test_battle_achievement_only_queues_reward_without_mutating_runtime_unit()
	await _test_party_management_window_renders_achievement_summary()
	await _test_party_management_window_ignores_legacy_equipment_state_dictionary()
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


func _test_manual_skill_learning_rejects_grant_only_sources() -> void:
	var skill_defs: Dictionary = {
		&"book_manual_skill": _make_test_learn_source_skill(&"book_manual_skill", &"book"),
		&"innate_manual_skill": _make_test_learn_source_skill(&"innate_manual_skill", &"innate"),
		&"profession_grant_skill": _make_test_learn_source_skill(&"profession_grant_skill", &"profession"),
		&"race_grant_skill": _make_test_learn_source_skill(&"race_grant_skill", &"race"),
		&"subrace_grant_skill": _make_test_learn_source_skill(&"subrace_grant_skill", &"subrace"),
		&"ascension_grant_skill": _make_test_learn_source_skill(&"ascension_grant_skill", &"ascension"),
		&"bloodline_grant_skill": _make_test_learn_source_skill(&"bloodline_grant_skill", &"bloodline"),
	}
	var progress := UnitProgress.new()
	var service := ProgressionService.new()
	service.setup(progress, skill_defs, {})

	_assert_true(service.learn_skill(&"book_manual_skill"), "book 来源技能应仍可走手动学习链。")
	_assert_true(service.learn_skill(&"innate_manual_skill"), "既有 innate 来源技能应保持原手动学习行为。")

	for blocked_skill_id in [
		&"profession_grant_skill",
		&"race_grant_skill",
		&"subrace_grant_skill",
		&"ascension_grant_skill",
		&"bloodline_grant_skill",
	]:
		_assert_true(
			not service.learn_skill(blocked_skill_id),
			"%s 不应能通过手动学习链写入 learned 状态。" % String(blocked_skill_id)
		)
		_assert_true(
			progress.get_skill_progress(blocked_skill_id) == null,
			"%s 被手动学习拒绝后不应创建技能进度。" % String(blocked_skill_id)
		)


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
	var gated_passive_skill := skill_defs.get(&"vajra_body") as SkillDef

	_assert_true(
		session._is_random_start_book_skill_candidate(standard_book_skill, progression),
		"普通技能书技能应保留在随机起始技能池中。"
	)
	_assert_true(
		not session._is_random_start_book_skill_candidate(composite_book_skill, progression),
		"复合升级技能不应进入随机起始技能池。"
	)
	_assert_true(
		not session._is_random_start_book_skill_candidate(gated_passive_skill, progression),
		"随机开局技能池不应绕过金刚不坏的属性与成就门槛。"
	)
	session.free()


func _test_vajra_body_requires_attributes_and_achievement_and_syncs_battle_status() -> void:
	var registry := ProgressionContentRegistry.new()
	var skill_def = registry.get_skill_defs().get(&"vajra_body") as SkillDef
	_assert_true(skill_def != null, "金刚不坏技能资源应注册。")
	if skill_def == null:
		return
	_assert_eq(skill_def.skill_type, &"passive", "金刚不坏应是被动技能。")
	_assert_eq(skill_def.max_level, 10, "金刚不坏应支持最高 10 级。")
	_assert_eq(skill_def.non_core_max_level, 9, "金刚不坏非核心状态应最多升到 9 级。")
	_assert_eq(int(skill_def.attribute_requirements.get("strength", 0)), 13, "金刚不坏应要求力量 13。")
	_assert_eq(int(skill_def.attribute_requirements.get("constitution", 0)), 14, "金刚不坏应要求体质 14。")
	_assert_eq(int(skill_def.attribute_requirements.get("willpower", 0)), 14, "金刚不坏应要求意志 14。")
	_assert_true(skill_def.achievement_requirements.has(&"near_death_unbroken"), "金刚不坏应要求濒死未倒成就。")

	var party_state := _make_party_state([&"hero"])
	var member_state: PartyMemberState = party_state.get_member_state(&"hero")
	var attributes: UnitBaseAttributes = member_state.progression.unit_base_attributes
	attributes.set_attribute_value(UnitBaseAttributes.STRENGTH, 13)
	attributes.set_attribute_value(UnitBaseAttributes.CONSTITUTION, 14)
	attributes.set_attribute_value(UnitBaseAttributes.WILLPOWER, 13)
	var manager := _setup_manager(party_state, registry.get_achievement_defs())
	_assert_true(not manager.learn_skill(&"hero", &"vajra_body"), "意志不足时不应学会金刚不坏。")

	attributes.set_attribute_value(UnitBaseAttributes.WILLPOWER, 14)
	_assert_true(not manager.learn_skill(&"hero", &"vajra_body"), "未达成濒死未倒时不应学会金刚不坏。")
	_assert_true(manager.unlock_achievement(&"hero", &"near_death_unbroken"), "测试前置应能解锁濒死未倒。")
	_assert_true(manager.learn_skill(&"hero", &"vajra_body"), "满足属性与成就后应能学会金刚不坏。")

	var skill_progress = member_state.progression.get_skill_progress(&"vajra_body")
	_assert_true(skill_progress != null and skill_progress.is_learned, "金刚不坏学习结果应写入角色成长。")
	var factory := BattleUnitFactory.new()
	var units: Array = factory.build_ally_units(party_state, {})
	var unit_state: BattleUnitState = (units[0] as BattleUnitState) if not units.is_empty() else null
	var status_entry = unit_state.get_status_effect(&"vajra_body") if unit_state != null else null
	_assert_true(status_entry != null, "学会金刚不坏后，战斗单位应同步 vajra_body 状态。")
	if status_entry != null:
		_assert_eq(int(status_entry.params.get("passive_reduction", -1)), 1, "0 级金刚不坏应减少 1 点伤害。")
		_assert_true(not bool(status_entry.params.get("forced_move_immune", false)), "0 级金刚不坏不应免疫强制位移。")

	skill_progress.skill_level = 7
	units = factory.build_ally_units(party_state, {})
	unit_state = (units[0] as BattleUnitState) if not units.is_empty() else null
	status_entry = unit_state.get_status_effect(&"vajra_body") if unit_state != null else null
	if status_entry != null:
		_assert_eq(int(status_entry.params.get("passive_reduction", -1)), 5, "7 级金刚不坏应减少 5 点伤害。")
		_assert_eq(int(status_entry.params.get("control_save_bonus", -1)), 1, "7 级金刚不坏应记录 1 点控制检定加值。")

	var progression_service := ProgressionService.new()
	progression_service.setup(member_state.progression, registry.get_skill_defs(), registry.get_profession_defs())
	_assert_true(
		progression_service.grant_skill_mastery(&"vajra_body", 20000, &"heavy_hit_taken"),
		"非核心金刚不坏应能继续获得熟练度。"
	)
	_assert_eq(int(skill_progress.skill_level), 9, "非核心金刚不坏最多只能升到 9 级。")
	units = factory.build_ally_units(party_state, {})
	unit_state = (units[0] as BattleUnitState) if not units.is_empty() else null
	status_entry = unit_state.get_status_effect(&"vajra_body") if unit_state != null else null
	if status_entry != null:
		_assert_eq(int(status_entry.params.get("passive_reduction", -1)), 6, "9 级金刚不坏应减少 6 点伤害。")
		_assert_eq(int(status_entry.params.get("control_save_bonus", -1)), 2, "9 级金刚不坏应记录 2 点控制检定加值。")
		_assert_true(not bool(status_entry.params.get("forced_move_immune", false)), "非核心 9 级金刚不坏不应免疫强制位移。")

	_assert_true(progression_service.set_skill_core(&"vajra_body", true), "金刚不坏设为核心后应解锁 10 级上限。")
	_assert_true(
		progression_service.grant_skill_mastery(&"vajra_body", 3000, &"heavy_hit_taken"),
		"核心金刚不坏应能继续获得熟练度。"
	)
	_assert_eq(int(skill_progress.skill_level), 10, "核心金刚不坏应能升到 10 级。")
	units = factory.build_ally_units(party_state, {})
	unit_state = (units[0] as BattleUnitState) if not units.is_empty() else null
	status_entry = unit_state.get_status_effect(&"vajra_body") if unit_state != null else null
	if status_entry != null:
		_assert_eq(int(status_entry.params.get("passive_reduction", -1)), 6, "10 级金刚不坏应保持减少 6 点伤害。")
		_assert_eq(int(status_entry.params.get("control_save_bonus", -1)), 2, "10 级金刚不坏应记录 2 点控制检定加值。")
		_assert_true(bool(status_entry.params.get("forced_move_immune", false)), "核心 10 级金刚不坏应免疫敌方强制位移。")


func _test_seed_growth_achievement_events_unlock_via_real_progression_actions() -> void:
	var party_state := _make_party_state([&"hero"])
	var manager := _setup_manager(party_state)
	var member_state: PartyMemberState = party_state.get_member_state(&"hero")
	var attributes: UnitBaseAttributes = member_state.progression.unit_base_attributes
	var agility_before: int = attributes.get_attribute_value(UnitBaseAttributes.AGILITY)
	var perception_before: int = attributes.get_attribute_value(UnitBaseAttributes.PERCEPTION)
	var willpower_before: int = attributes.get_attribute_value(UnitBaseAttributes.WILLPOWER)

	attributes.set_attribute_value(UnitBaseAttributes.STRENGTH, 12)
	attributes.set_attribute_value(UnitBaseAttributes.AGILITY, 14)
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
		15,
		"确认熟练度成就奖励后，应提高敏捷。"
	)


func _test_stamina_max_uses_constitution_strength_and_agility_formula() -> void:
	var progress := UnitProgress.new()
	progress.unit_base_attributes.set_attribute_value(UnitBaseAttributes.CONSTITUTION, 3)
	progress.unit_base_attributes.set_attribute_value(UnitBaseAttributes.STRENGTH, 4)
	progress.unit_base_attributes.set_attribute_value(UnitBaseAttributes.AGILITY, 2)
	var service := AttributeService.new()
	service.setup(progress)
	_assert_eq(
		service.get_total_value(AttributeService.STAMINA_MAX),
		45,
		"体力上限应使用 24 + 5*体质 + 力量 + 敏捷。"
	)


func _test_attribute_progress_rewards_convert_below_twenty_and_accumulate_after_cap() -> void:
	var party_state := _make_party_state([&"hero"])
	var manager := CharacterManagementModule.new()
	manager.setup(party_state, {}, {}, {})
	var member_state: PartyMemberState = party_state.get_member_state(&"hero")
	var attributes: UnitBaseAttributes = member_state.progression.unit_base_attributes
	attributes.set_attribute_value(UnitBaseAttributes.AGILITY, 2)

	var first_reward = manager.build_pending_character_reward(
		&"hero",
		&"agility_progress_60",
		&"skill_core_max",
		&"test_basic_skill",
		"测试基础技能",
		[{
			"entry_type": "attribute_progress",
			"target_id": String(UnitBaseAttributes.AGILITY),
			"amount": 60,
			"reason_text": "测试属性进度",
		}],
		"测试属性进度"
	)
	manager.apply_pending_character_reward(first_reward)
	_assert_eq(attributes.get_attribute_value(UnitBaseAttributes.AGILITY), 2, "60 点敏捷进度不应直接提高属性。")
	_assert_eq(int(member_state.progression.attribute_growth_progress.get(UnitBaseAttributes.AGILITY, 0)), 60, "60 点敏捷进度应被保存。")

	var second_reward = manager.build_pending_character_reward(
		&"hero",
		&"agility_progress_50",
		&"skill_core_max",
		&"test_intermediate_skill",
		"测试中级技能",
		[{
			"entry_type": "attribute_progress",
			"target_id": String(UnitBaseAttributes.AGILITY),
			"amount": 50,
			"reason_text": "测试属性进度转化",
		}],
		"测试属性进度转化"
	)
	manager.apply_pending_character_reward(second_reward)
	_assert_eq(attributes.get_attribute_value(UnitBaseAttributes.AGILITY), 3, "累计达到 100 后敏捷应 +1。")
	_assert_eq(int(member_state.progression.attribute_growth_progress.get(UnitBaseAttributes.AGILITY, 0)), 10, "转化后应保留 10 点敏捷进度。")

	attributes.set_attribute_value(UnitBaseAttributes.AGILITY, 19)
	member_state.progression.attribute_growth_progress[UnitBaseAttributes.AGILITY] = 90
	var cap_reward = manager.build_pending_character_reward(
		&"hero",
		&"agility_progress_240",
		&"skill_core_max",
		&"test_ultimate_skill",
		"测试终极技能",
		[{
			"entry_type": "attribute_progress",
			"target_id": String(UnitBaseAttributes.AGILITY),
			"amount": 240,
			"reason_text": "测试 20 后累计",
		}],
		"测试 20 后累计"
	)
	manager.apply_pending_character_reward(cap_reward)
	_assert_eq(attributes.get_attribute_value(UnitBaseAttributes.AGILITY), 20, "属性低于 20 时应最多转化到 20。")
	_assert_eq(int(member_state.progression.attribute_growth_progress.get(UnitBaseAttributes.AGILITY, 0)), 230, "达到 20 后剩余进度应继续保存。")

	var over_cap_reward = manager.build_pending_character_reward(
		&"hero",
		&"agility_progress_after_20",
		&"skill_core_max",
		&"test_after_cap_skill",
		"测试 20 后继续累计",
		[{
			"entry_type": "attribute_progress",
			"target_id": String(UnitBaseAttributes.AGILITY),
			"amount": 120,
			"reason_text": "测试 20 后继续累计",
		}],
		"测试 20 后继续累计"
	)
	manager.apply_pending_character_reward(over_cap_reward)
	_assert_eq(attributes.get_attribute_value(UnitBaseAttributes.AGILITY), 20, "属性达到 20 后不应继续自动提高。")
	_assert_eq(int(member_state.progression.attribute_growth_progress.get(UnitBaseAttributes.AGILITY, 0)), 350, "属性达到 20 后进度应无上限继续累计。")


func _test_core_max_skill_queues_attribute_progress_once() -> void:
	var party_state := _make_party_state([&"hero"])
	var member_state: PartyMemberState = party_state.get_member_state(&"hero")
	var skill_def := _make_test_growth_skill(
		&"test_growth_core",
		&"basic",
		{
			"agility": 60,
		}
	)
	var manager := CharacterManagementModule.new()
	manager.setup(party_state, {skill_def.skill_id: skill_def}, {}, {})

	_assert_true(manager.learn_skill(&"hero", skill_def.skill_id), "测试技能应能学会。")
	var skill_progress = member_state.progression.get_skill_progress(skill_def.skill_id)
	skill_progress.is_core = true
	member_state.progression.set_skill_progress(skill_progress)

	var first_delta = manager.grant_battle_mastery(&"hero", skill_def.skill_id, 999)
	_assert_true(first_delta.mastery_changes.size() == 1, "核心技能满级时熟练度应正常入账。")
	_assert_eq(int(skill_progress.skill_level), 3, "测试技能应提升到满级。")
	_assert_eq(party_state.pending_character_rewards.size(), 1, "核心技能首次满级应入队一条属性进度奖励。")
	_assert_true(bool(skill_progress.core_max_growth_claimed), "核心满级成长入队后应标记已领取。")

	manager.grant_battle_mastery(&"hero", skill_def.skill_id, 999)
	_assert_eq(party_state.pending_character_rewards.size(), 1, "同一技能重复获得熟练度不应重复入队满级成长奖励。")

	manager.apply_pending_character_reward(party_state.get_next_pending_character_reward())
	_assert_eq(int(member_state.progression.attribute_growth_progress.get(UnitBaseAttributes.AGILITY, 0)), 60, "确认奖励后应写入技能配置的 60 点敏捷进度。")


func _test_core_max_skill_ignores_string_name_attribute_growth_key() -> void:
	var party_state := _make_party_state([&"hero"])
	var member_state: PartyMemberState = party_state.get_member_state(&"hero")
	var skill_def := _make_test_growth_skill(
		&"test_legacy_growth_core",
		&"basic",
		{
			UnitBaseAttributes.AGILITY: 60,
		}
	)
	var manager := CharacterManagementModule.new()
	manager.setup(party_state, {skill_def.skill_id: skill_def}, {}, {})

	_assert_true(manager.learn_skill(&"hero", skill_def.skill_id), "旧 StringName key 测试技能应能学会。")
	var skill_progress = member_state.progression.get_skill_progress(skill_def.skill_id)
	skill_progress.is_core = true
	member_state.progression.set_skill_progress(skill_progress)

	var mastery_delta = manager.grant_battle_mastery(&"hero", skill_def.skill_id, 999)
	_assert_true(mastery_delta.mastery_changes.size() == 1, "旧 StringName key 技能仍应正常获得熟练度。")
	_assert_eq(int(skill_progress.skill_level), 3, "旧 StringName key 技能应提升到满级。")
	_assert_eq(party_state.pending_character_rewards.size(), 0, "旧 StringName key attribute_growth_progress 不应产生属性成长奖励。")
	_assert_true(not bool(skill_progress.core_max_growth_claimed), "未产生正式属性成长奖励时不应标记 core_max_growth_claimed。")
	_assert_eq(int(member_state.progression.attribute_growth_progress.get(UnitBaseAttributes.AGILITY, 0)), 0, "旧 StringName key attribute_growth_progress 不应写入敏捷进度。")


func _test_non_core_skill_max_level_cap_lifts_when_core() -> void:
	var progress := UnitProgress.new()
	progress.unit_id = &"hero"
	var skill_def := SkillDef.new()
	skill_def.skill_id = &"test_core_lift"
	skill_def.display_name = "test_core_lift"
	skill_def.icon_id = &"test_core_lift"
	skill_def.max_level = 5
	skill_def.non_core_max_level = 3
	skill_def.mastery_curve = PackedInt32Array([1, 1, 1, 1, 1])

	var service := ProgressionService.new()
	service.setup(progress, {skill_def.skill_id: skill_def}, {})
	_assert_true(service.learn_skill(skill_def.skill_id), "测试技能应能学习。")
	service.grant_skill_mastery(skill_def.skill_id, 99, &"training")
	var skill_progress = progress.get_skill_progress(skill_def.skill_id)
	_assert_eq(int(skill_progress.skill_level), 3, "非核心技能应被 non_core_max_level 限制在 3 级。")

	_assert_true(service.set_skill_core(skill_def.skill_id, true), "测试技能应能锁定为核心。")
	service.grant_skill_mastery(skill_def.skill_id, 99, &"training")
	_assert_eq(int(skill_progress.skill_level), 5, "锁定为核心后应允许提升到 max_level 5。")


func _test_aura_slash_max_level_uses_transformation_count() -> void:
	var progress := UnitProgress.new()
	progress.unit_id = &"hero"
	var skill_def := SkillDef.new()
	skill_def.skill_id = &"warrior_aura_slash"
	skill_def.display_name = "斗气斩"
	skill_def.icon_id = &"warrior_aura_slash"
	skill_def.max_level = 7
	skill_def.non_core_max_level = 5
	skill_def.dynamic_max_level_stat_id = &"aura_transformation_count"
	skill_def.dynamic_max_level_base = 7
	skill_def.dynamic_max_level_per_stat = 2
	skill_def.mastery_curve = PackedInt32Array([1, 1, 1, 1, 1, 1, 1])

	var service := ProgressionService.new()
	service.setup(progress, {skill_def.skill_id: skill_def}, {})
	_assert_true(service.learn_skill(skill_def.skill_id), "斗气斩测试技能应能学习。")
	service.grant_skill_mastery(skill_def.skill_id, 99, &"training")
	var skill_progress = progress.get_skill_progress(skill_def.skill_id)
	_assert_eq(int(skill_progress.skill_level), 5, "斗气斩非核心状态应被限制在 5 级。")

	_assert_true(service.set_skill_core(skill_def.skill_id, true), "斗气斩应能锁定为核心。")
	service.grant_skill_mastery(skill_def.skill_id, 99, &"training")
	_assert_eq(int(skill_progress.skill_level), 7, "斗气斩核心状态默认最大等级应为 7。")

	progress.unit_base_attributes.set_attribute_value(&"aura_transformation_count", 2)
	service.refresh_runtime_state()
	service.grant_skill_mastery(skill_def.skill_id, 99, &"training")
	_assert_eq(int(skill_progress.skill_level), 11, "斗气斩每次斗气质变应将核心最大等级提高 2。")


func _test_attribute_growth_progress_round_trip_persists() -> void:
	var progress := UnitProgress.new()
	progress.unit_id = &"hero"
	progress.display_name = "Hero"
	progress.attribute_growth_progress[UnitBaseAttributes.STRENGTH] = 80
	progress.attribute_growth_progress[UnitBaseAttributes.AGILITY] = 240

	var skill_progress := UnitSkillProgress.new()
	skill_progress.skill_id = &"test_growth_core"
	skill_progress.is_learned = true
	skill_progress.is_core = true
	skill_progress.skill_level = 3
	skill_progress.core_max_growth_claimed = true
	progress.set_skill_progress(skill_progress)

	var restored_progress = UnitProgress.from_dict(progress.to_dict())
	var restored_skill_progress = restored_progress.get_skill_progress(&"test_growth_core")
	_assert_eq(
		int(restored_progress.attribute_growth_progress.get(UnitBaseAttributes.STRENGTH, 0)),
		80,
		"基础属性成长进度应通过 UnitProgress 存档往返保留。"
	)
	_assert_eq(
		int(restored_progress.attribute_growth_progress.get(UnitBaseAttributes.AGILITY, 0)),
		240,
		"超过 100 的属性成长进度应通过 UnitProgress 存档往返保留。"
	)
	_assert_true(
		restored_skill_progress != null and bool(restored_skill_progress.core_max_growth_claimed),
		"核心满级成长已领取标记应通过 UnitSkillProgress 存档往返保留。"
	)


func _test_unit_progress_from_dict_requires_top_level_schema_fields() -> void:
	for field_name in [
		"version",
		"unit_id",
		"display_name",
		"character_level",
		"unit_base_attributes",
		"reputation_state",
		"skills",
		"professions",
		"known_knowledge_ids",
		"active_core_skill_ids",
		"attribute_growth_progress",
		"achievement_progress",
		"pending_profession_choices",
		"blocked_relearn_skill_ids",
		"merged_skill_source_map",
		"unlocked_combat_resource_ids",
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		payload.erase(field_name)
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"缺少 UnitProgress.%s 的 payload 应直接拒绝。" % field_name
		)

	for field_case in [
		{"field": "version", "value": "1"},
		{"field": "version", "value": 2},
		{"field": "unit_id", "value": ""},
		{"field": "unit_id", "value": 123},
		{"field": "display_name", "value": ""},
		{"field": "display_name", "value": 123},
		{"field": "character_level", "value": "1"},
		{"field": "character_level", "value": -1},
		{"field": "known_knowledge_ids", "value": [""]},
		{"field": "known_knowledge_ids", "value": ["lore", "lore"]},
		{"field": "active_core_skill_ids", "value": [""]},
		{"field": "active_core_skill_ids", "value": ["test_strict_skill", "test_strict_skill"]},
		{"field": "attribute_growth_progress", "value": {"": 1}},
		{"field": "attribute_growth_progress", "value": {"strength": "1"}},
		{"field": "attribute_growth_progress", "value": {"strength": -1}},
		{"field": "blocked_relearn_skill_ids", "value": [""]},
		{"field": "blocked_relearn_skill_ids", "value": ["test_strict_skill", "test_strict_skill"]},
		{"field": "merged_skill_source_map", "value": {"": ["source_skill"]}},
		{"field": "merged_skill_source_map", "value": {"test_strict_skill": "source_skill"}},
		{"field": "merged_skill_source_map", "value": {"test_strict_skill": [""]}},
		{"field": "merged_skill_source_map", "value": {"test_strict_skill": ["source_skill", "source_skill"]}},
		{"field": "unlocked_combat_resource_ids", "value": [""]},
		{"field": "unlocked_combat_resource_ids", "value": ["hp", "hp"]},
		{"field": "unlocked_combat_resource_ids", "value": ["hp"]},
		{"field": "unlocked_combat_resource_ids", "value": ["hp", "stamina", "unknown"]},
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		payload[String(field_case.get("field", ""))] = field_case.get("value")
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"UnitProgress.%s 非法值不应被转换、丢弃或补默认。" % String(field_case.get("field", ""))
		)

	for field_case in [
		{"field": "unit_base_attributes", "value": []},
		{"field": "reputation_state", "value": []},
		{"field": "skills", "value": []},
		{"field": "professions", "value": []},
		{"field": "known_knowledge_ids", "value": {}},
		{"field": "active_core_skill_ids", "value": {}},
		{"field": "attribute_growth_progress", "value": []},
		{"field": "achievement_progress", "value": []},
		{"field": "pending_profession_choices", "value": {}},
		{"field": "blocked_relearn_skill_ids", "value": {}},
		{"field": "merged_skill_source_map", "value": []},
		{"field": "unlocked_combat_resource_ids", "value": {}},
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		payload[String(field_case.get("field", ""))] = field_case.get("value")
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"UnitProgress.%s 类型错误的 payload 应直接拒绝。" % String(field_case.get("field", ""))
		)

	var invalid_pending_choice_payload := _build_unit_progress_payload_with_child_entries()
	invalid_pending_choice_payload["pending_profession_choices"] = ["invalid"]
	_assert_true(
		UnitProgress.from_dict(invalid_pending_choice_payload) == null,
		"pending_profession_choices 中包含非字典条目时应直接拒绝。"
	)


func _test_unit_progress_from_dict_rejects_attribute_and_reputation_schema_defaults() -> void:
	for field_name in [
		"strength",
		"agility",
		"constitution",
		"perception",
		"intelligence",
		"willpower",
		"custom_stats",
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		var attributes_payload: Dictionary = (payload.get("unit_base_attributes", {}) as Dictionary).duplicate(true)
		attributes_payload.erase(field_name)
		payload["unit_base_attributes"] = attributes_payload
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"缺少 UnitBaseAttributes.%s 的 payload 应直接拒绝。" % field_name
		)

	var invalid_custom_stats_payload := _build_unit_progress_payload_with_child_entries()
	var invalid_attributes_payload: Dictionary = (invalid_custom_stats_payload.get("unit_base_attributes", {}) as Dictionary).duplicate(true)
	invalid_attributes_payload["custom_stats"] = []
	invalid_custom_stats_payload["unit_base_attributes"] = invalid_attributes_payload
	_assert_true(
		UnitProgress.from_dict(invalid_custom_stats_payload) == null,
		"UnitBaseAttributes.custom_stats 类型错误的 payload 应直接拒绝。"
	)

	for field_case in [
		{"field": "strength", "value": "3"},
		{"field": "agility", "value": "3"},
		{"field": "constitution", "value": "3"},
		{"field": "perception", "value": "3"},
		{"field": "intelligence", "value": "3"},
		{"field": "willpower", "value": "3"},
		{"field": "custom_stats", "value": {"": 1}},
		{"field": "custom_stats", "value": {123: 1}},
		{"field": "custom_stats", "value": {"hidden_luck_at_birth": "1"}},
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		var attributes_payload: Dictionary = (payload.get("unit_base_attributes", {}) as Dictionary).duplicate(true)
		attributes_payload[String(field_case.get("field", ""))] = field_case.get("value")
		payload["unit_base_attributes"] = attributes_payload
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"UnitBaseAttributes.%s 非法值不应被转换、丢弃或补默认。" % String(field_case.get("field", ""))
		)

	for field_name in ["morality", "custom_states"]:
		var payload := _build_unit_progress_payload_with_child_entries()
		var reputation_payload: Dictionary = (payload.get("reputation_state", {}) as Dictionary).duplicate(true)
		reputation_payload.erase(field_name)
		payload["reputation_state"] = reputation_payload
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"缺少 UnitReputationState.%s 的 payload 应直接拒绝。" % field_name
		)

	var invalid_custom_states_payload := _build_unit_progress_payload_with_child_entries()
	var invalid_reputation_payload: Dictionary = (invalid_custom_states_payload.get("reputation_state", {}) as Dictionary).duplicate(true)
	invalid_reputation_payload["custom_states"] = []
	invalid_custom_states_payload["reputation_state"] = invalid_reputation_payload
	_assert_true(
		UnitProgress.from_dict(invalid_custom_states_payload) == null,
		"UnitReputationState.custom_states 类型错误的 payload 应直接拒绝。"
	)

	for field_case in [
		{"field": "morality", "value": "0"},
		{"field": "custom_states", "value": {"": 1}},
		{"field": "custom_states", "value": {123: 1}},
		{"field": "custom_states", "value": {"guild_fame": "1"}},
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		var reputation_payload: Dictionary = (payload.get("reputation_state", {}) as Dictionary).duplicate(true)
		reputation_payload[String(field_case.get("field", ""))] = field_case.get("value")
		payload["reputation_state"] = reputation_payload
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"UnitReputationState.%s 非法值不应被转换、丢弃或补默认。" % String(field_case.get("field", ""))
		)


func _test_unit_progress_from_dict_rejects_pending_profession_choice_schema_defaults() -> void:
	for field_name in [
		"trigger_skill_ids",
		"candidate_profession_ids",
		"target_rank_map",
		"qualifier_skill_pool_ids",
		"assignable_skill_candidate_ids",
		"required_qualifier_count",
		"required_assigned_core_count",
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		_erase_pending_profession_choice_field(payload, field_name)
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"缺少 PendingProfessionChoice.%s 的 payload 应直接拒绝。" % field_name
		)

	for field_case in [
		{"field": "trigger_skill_ids", "value": {}},
		{"field": "candidate_profession_ids", "value": {}},
		{"field": "target_rank_map", "value": []},
		{"field": "qualifier_skill_pool_ids", "value": {}},
		{"field": "assignable_skill_candidate_ids", "value": {}},
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		_set_pending_profession_choice_field_value(
			payload,
			String(field_case.get("field", "")),
			field_case.get("value")
		)
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"PendingProfessionChoice.%s 类型错误的 payload 应直接拒绝。" % String(field_case.get("field", ""))
		)

	for field_case in [
		{"field": "trigger_skill_ids", "value": [""]},
		{"field": "trigger_skill_ids", "value": [123]},
		{"field": "trigger_skill_ids", "value": ["test_strict_skill", "test_strict_skill"]},
		{"field": "candidate_profession_ids", "value": [""]},
		{"field": "candidate_profession_ids", "value": [123]},
		{"field": "candidate_profession_ids", "value": ["test_strict_profession", "test_strict_profession"]},
		{"field": "target_rank_map", "value": {"": 2}},
		{"field": "target_rank_map", "value": {123: 2}},
		{"field": "target_rank_map", "value": {"test_strict_profession": "2"}},
		{"field": "target_rank_map", "value": {"test_strict_profession": -1}},
		{"field": "qualifier_skill_pool_ids", "value": [""]},
		{"field": "qualifier_skill_pool_ids", "value": [123]},
		{"field": "qualifier_skill_pool_ids", "value": ["test_strict_skill", "test_strict_skill"]},
		{"field": "assignable_skill_candidate_ids", "value": [""]},
		{"field": "assignable_skill_candidate_ids", "value": [123]},
		{"field": "assignable_skill_candidate_ids", "value": ["test_strict_skill", "test_strict_skill"]},
		{"field": "required_qualifier_count", "value": "1"},
		{"field": "required_qualifier_count", "value": -1},
		{"field": "required_assigned_core_count", "value": "1"},
		{"field": "required_assigned_core_count", "value": -1},
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		_set_pending_profession_choice_field_value(
			payload,
			String(field_case.get("field", "")),
			field_case.get("value")
		)
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"PendingProfessionChoice.%s 非法值不应被转换、丢弃或补默认。" % String(field_case.get("field", ""))
		)


func _test_unit_progress_from_dict_rejects_child_id_fallbacks() -> void:
	for case in [
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "skill_id", "erase": true},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "skill_id", "value": ""},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "skill_id", "value": 123},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "skill_id", "value": "other_skill"},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "profession_id", "erase": true},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "profession_id", "value": ""},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "profession_id", "value": 123},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "profession_id", "value": "other_profession"},
		{"bucket": "achievement_progress", "entry_id": "test_strict_achievement", "field": "achievement_id", "erase": true},
		{"bucket": "achievement_progress", "entry_id": "test_strict_achievement", "field": "achievement_id", "value": ""},
		{"bucket": "achievement_progress", "entry_id": "test_strict_achievement", "field": "achievement_id", "value": 123},
		{"bucket": "achievement_progress", "entry_id": "test_strict_achievement", "field": "achievement_id", "value": "other_achievement"},
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		var bucket := String(case.get("bucket", ""))
		var entry_id := String(case.get("entry_id", ""))
		var field_name := String(case.get("field", ""))
		var bucket_payload: Dictionary = (payload.get(bucket, {}) as Dictionary).duplicate(true)
		var entry_payload: Dictionary = (bucket_payload.get(entry_id, {}) as Dictionary).duplicate(true)
		if bool(case.get("erase", false)):
			entry_payload.erase(field_name)
		else:
			entry_payload[field_name] = case.get("value")
		bucket_payload[entry_id] = entry_payload
		payload[bucket] = bucket_payload

		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"%s.%s 缺失或与 map key 错配时，UnitProgress.from_dict 应直接拒绝。" % [bucket, field_name]
		)


func _test_unit_progress_from_dict_rejects_child_schema_defaults() -> void:
	for field_name in [
		"is_learned",
		"skill_level",
		"current_mastery",
		"total_mastery_earned",
		"is_core",
		"assigned_profession_id",
		"merged_from_skill_ids",
		"mastery_from_training",
		"mastery_from_battle",
		"profession_granted_by",
		"core_max_growth_claimed",
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		_erase_unit_progress_child_field(payload, "skills", "test_strict_skill", field_name)
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"缺少 UnitSkillProgress.%s 的 payload 应直接拒绝。" % field_name
		)

	for field_name in [
		"rank",
		"is_active",
		"is_hidden",
		"core_skill_ids",
		"granted_skill_ids",
		"promotion_history",
		"inactive_reason",
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		_erase_unit_progress_child_field(payload, "professions", "test_strict_profession", field_name)
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"缺少 UnitProfessionProgress.%s 的 payload 应直接拒绝。" % field_name
		)

	for field_name in ["current_value", "is_unlocked", "unlocked_at_unix_time"]:
		var payload := _build_unit_progress_payload_with_child_entries()
		_erase_unit_progress_child_field(payload, "achievement_progress", "test_strict_achievement", field_name)
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"缺少 AchievementProgressState.%s 的 payload 应直接拒绝。" % field_name
		)

	for case in [
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "merged_from_skill_ids"},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "core_skill_ids"},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "granted_skill_ids"},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "promotion_history"},
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		_set_unit_progress_child_field_value(
			payload,
			String(case.get("bucket", "")),
			String(case.get("entry_id", "")),
			String(case.get("field", "")),
			{}
		)
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"%s.%s 类型错误的 payload 应直接拒绝。" % [String(case.get("bucket", "")), String(case.get("field", ""))]
		)

	for case in [
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "is_learned", "value": 1},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "skill_level", "value": "1"},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "skill_level", "value": -1},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "current_mastery", "value": "0"},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "current_mastery", "value": -1},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "total_mastery_earned", "value": "0"},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "total_mastery_earned", "value": -1},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "is_core", "value": 0},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "assigned_profession_id", "value": 123},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "merged_from_skill_ids", "value": [""]},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "merged_from_skill_ids", "value": [123]},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "merged_from_skill_ids", "value": ["source_skill", "source_skill"]},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "mastery_from_training", "value": "0"},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "mastery_from_training", "value": -1},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "mastery_from_battle", "value": "0"},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "mastery_from_battle", "value": -1},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "profession_granted_by", "value": 123},
		{"bucket": "skills", "entry_id": "test_strict_skill", "field": "core_max_growth_claimed", "value": 0},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "rank", "value": "1"},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "rank", "value": -1},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "is_active", "value": 1},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "is_hidden", "value": 0},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "core_skill_ids", "value": [""]},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "core_skill_ids", "value": [123]},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "core_skill_ids", "value": ["test_strict_skill", "test_strict_skill"]},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "granted_skill_ids", "value": [""]},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "granted_skill_ids", "value": [123]},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "granted_skill_ids", "value": ["test_strict_skill", "test_strict_skill"]},
		{"bucket": "professions", "entry_id": "test_strict_profession", "field": "inactive_reason", "value": 123},
		{"bucket": "achievement_progress", "entry_id": "test_strict_achievement", "field": "current_value", "value": "1"},
		{"bucket": "achievement_progress", "entry_id": "test_strict_achievement", "field": "current_value", "value": -1},
		{"bucket": "achievement_progress", "entry_id": "test_strict_achievement", "field": "is_unlocked", "value": 1},
		{"bucket": "achievement_progress", "entry_id": "test_strict_achievement", "field": "unlocked_at_unix_time", "value": "1"},
		{"bucket": "achievement_progress", "entry_id": "test_strict_achievement", "field": "unlocked_at_unix_time", "value": -1},
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		_set_unit_progress_child_field_value(
			payload,
			String(case.get("bucket", "")),
			String(case.get("entry_id", "")),
			String(case.get("field", "")),
			case.get("value")
		)
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"%s.%s 非法值不应被转换、丢弃或补默认。" % [String(case.get("bucket", "")), String(case.get("field", ""))]
		)

	var invalid_promotion_history_payload := _build_unit_progress_payload_with_child_entries()
	_set_unit_progress_child_field_value(
		invalid_promotion_history_payload,
		"professions",
		"test_strict_profession",
		"promotion_history",
		["invalid"]
	)
	_assert_true(
		UnitProgress.from_dict(invalid_promotion_history_payload) == null,
		"promotion_history 中包含非字典条目时应直接拒绝。"
	)

	for field_name in [
		"new_rank",
		"consumed_skill_ids",
		"qualifier_skill_ids",
		"snapshot_unit_base_attributes",
		"timestamp",
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		_erase_promotion_record_field(payload, field_name)
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"缺少 ProfessionPromotionRecord.%s 的 payload 应直接拒绝。" % field_name
		)

	for field_case in [
		{"field": "new_rank", "value": "2"},
		{"field": "new_rank", "value": -1},
		{"field": "consumed_skill_ids", "value": {}},
		{"field": "consumed_skill_ids", "value": [""]},
		{"field": "consumed_skill_ids", "value": [123]},
		{"field": "consumed_skill_ids", "value": ["test_strict_skill", "test_strict_skill"]},
		{"field": "qualifier_skill_ids", "value": {}},
		{"field": "qualifier_skill_ids", "value": [""]},
		{"field": "qualifier_skill_ids", "value": [123]},
		{"field": "qualifier_skill_ids", "value": ["test_strict_skill", "test_strict_skill"]},
		{"field": "snapshot_unit_base_attributes", "value": []},
		{"field": "timestamp", "value": "123"},
		{"field": "timestamp", "value": -1},
	]:
		var payload := _build_unit_progress_payload_with_child_entries()
		_set_promotion_record_field_value(
			payload,
			String(field_case.get("field", "")),
			field_case.get("value")
		)
		_assert_true(
			UnitProgress.from_dict(payload) == null,
			"ProfessionPromotionRecord.%s 类型错误的 payload 应直接拒绝。" % String(field_case.get("field", ""))
		)


func _test_combat_resource_unlocks_follow_learned_skill_costs() -> void:
	var progress := UnitProgress.new()
	progress.unit_id = &"hero"
	progress.display_name = "Hero"
	var mp_skill := _make_test_combat_resource_skill(&"test_mp_spell", 3, 0)
	var aura_skill := _make_test_combat_resource_skill(&"test_aura_slash", 0, 2)
	var service := ProgressionService.new()
	service.setup(progress, {
		mp_skill.skill_id: mp_skill,
		aura_skill.skill_id: aura_skill,
	}, {})

	_assert_true(progress.has_combat_resource_unlocked(UnitProgress.COMBAT_RESOURCE_HP), "角色初始应解锁 HP 资源。")
	_assert_true(progress.has_combat_resource_unlocked(UnitProgress.COMBAT_RESOURCE_STAMINA), "角色初始应解锁体力资源。")
	_assert_true(not progress.has_combat_resource_unlocked(UnitProgress.COMBAT_RESOURCE_MP), "学习耗蓝技能前不应显示 MP 资源。")
	_assert_true(not progress.has_combat_resource_unlocked(UnitProgress.COMBAT_RESOURCE_AURA), "学习耗斗气技能前不应显示斗气资源。")

	_assert_true(service.learn_skill(mp_skill.skill_id), "测试耗蓝技能应能学习。")
	_assert_true(progress.has_combat_resource_unlocked(UnitProgress.COMBAT_RESOURCE_MP), "学习耗蓝技能后应正式解锁 MP 资源。")
	_assert_true(not progress.has_combat_resource_unlocked(UnitProgress.COMBAT_RESOURCE_AURA), "只学习耗蓝技能不应解锁斗气资源。")

	_assert_true(service.learn_skill(aura_skill.skill_id), "测试耗斗气技能应能学习。")
	_assert_true(progress.has_combat_resource_unlocked(UnitProgress.COMBAT_RESOURCE_AURA), "学习耗斗气技能后应正式解锁斗气资源。")

	var restored_progress = UnitProgress.from_dict(progress.to_dict())
	_assert_true(restored_progress.has_combat_resource_unlocked(UnitProgress.COMBAT_RESOURCE_MP), "MP 解锁状态应通过 UnitProgress 存档往返保留。")
	_assert_true(restored_progress.has_combat_resource_unlocked(UnitProgress.COMBAT_RESOURCE_AURA), "斗气解锁状态应通过 UnitProgress 存档往返保留。")


func _test_starting_and_random_skill_refresh_unlocks_combat_resources() -> void:
	var session := GameSession.new()
	var starting_skill := _make_test_combat_resource_skill(&"test_basic_start", 0, 0)
	var random_aura_skill := _make_test_combat_resource_skill(&"test_random_aura_start", 0, 2)
	session._skill_defs = {
		starting_skill.skill_id: starting_skill,
		random_aura_skill.skill_id: random_aura_skill,
	}
	var member_state: PartyMemberState = session._build_default_member_state(
		&"test_starting_resource_member",
		"资源刷新测试",
		starting_skill.skill_id,
		&"portrait_test",
		18,
		6,
		4,
		2,
		3,
		1,
		1,
		1,
		24,
		0
	)
	_assert_true(
		member_state.progression.has_combat_resource_unlocked(UnitProgress.COMBAT_RESOURCE_AURA),
		"新建角色随机起始书技能耗 Aura 时，应在创建链路刷新 runtime state 并解锁 Aura 资源。"
	)
	session.free()


func _test_combat_skill_level_overrides_accumulate_minimum_level_patches() -> void:
	var combat_profile := CombatSkillDef.new()
	combat_profile.ap_cost = 2
	combat_profile.stamina_cost = 30
	combat_profile.cooldown_tu = 20
	combat_profile.level_overrides = {
		2: {"stamina_cost": 20},
		4: {"cooldown_tu": 5},
		5: {"ap_cost": 1},
	}

	var level_five_costs := combat_profile.get_effective_resource_costs(5)
	_assert_eq(int(level_five_costs.get("ap_cost", 0)), 1, "5 级 override 应应用本级 AP patch。")
	_assert_eq(int(level_five_costs.get("stamina_cost", 0)), 20, "5 级 override 不应丢失 2 级 stamina patch。")
	_assert_eq(int(level_five_costs.get("cooldown_tu", 0)), 5, "5 级 override 不应丢失 4 级 cooldown patch。")

	var string_key_profile := CombatSkillDef.new()
	string_key_profile.stamina_cost = 30
	string_key_profile.level_overrides = {
		"2": {"stamina_cost": 10},
	}
	var string_key_costs := string_key_profile.get_effective_resource_costs(2)
	_assert_eq(int(string_key_costs.get("stamina_cost", 0)), 30, "字符串等级 key 不应被 combat_profile runtime 当成等级 override。")


func _test_min_only_requirements_ignore_zero_max_value() -> void:
	var attribute_requirement := AttributeRequirement.new()
	attribute_requirement.min_value = 5
	attribute_requirement.max_value = 0
	_assert_true(attribute_requirement.matches_value(7), "属性 min-only 条件 max_value=0 时不应形成不可能区间。")
	_assert_true(not attribute_requirement.matches_value(4), "属性 min-only 条件仍应保留下限。")

	var reputation_requirement := ReputationRequirement.new()
	reputation_requirement.min_value = 3
	reputation_requirement.max_value = 0
	_assert_true(reputation_requirement.matches_value(4), "声望 min-only 条件 max_value=0 时不应形成不可能区间。")
	_assert_true(not reputation_requirement.matches_value(2), "声望 min-only 条件仍应保留下限。")

	var active_condition := ProfessionActiveCondition.new()
	active_condition.min_value = 2
	active_condition.max_value = 0
	_assert_true(active_condition.matches_value(9), "职业激活 min-only 条件 max_value=0 时不应形成不可能区间。")
	_assert_true(not active_condition.matches_value(1), "职业激活 min-only 条件仍应保留下限。")


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
	var member_state: PartyMemberState = party_state.get_member_state(&"hero")
	var attributes: UnitBaseAttributes = member_state.progression.unit_base_attributes
	attributes.set_attribute_value(UnitBaseAttributes.STRENGTH, 12)
	attributes.set_attribute_value(UnitBaseAttributes.AGILITY, 14)
	var progression: UnitProgress = member_state.progression

	_assert_true(manager.learn_skill(&"hero", &"charge"), "前置条件：hero 应能学会冲锋。")
	_assert_true(manager.learn_skill(&"hero", &"warrior_combo_strike"), "前置条件：hero 应能学会连击。")
	_assert_true(manager.learn_skill(&"hero", &"warrior_aura_slash"), "前置条件：hero 应能学会斗气斩。")
	var source_combo_progress = progression.get_skill_progress(&"warrior_combo_strike")
	var source_aura_progress = progression.get_skill_progress(&"warrior_aura_slash")
	source_combo_progress.is_core = true
	source_aura_progress.is_core = true
	progression.set_skill_progress(source_combo_progress)
	progression.set_skill_progress(source_aura_progress)
	_assert_true(
		manager.grant_battle_mastery(&"hero", &"warrior_combo_strike", 10000).mastery_changes.size() > 0,
		"核心连击应能通过真实熟练度成长提升到满级。"
	)
	_assert_true(
		manager.grant_battle_mastery(&"hero", &"warrior_aura_slash", 10000).mastery_changes.size() > 0,
		"核心斗气斩应能通过真实熟练度成长提升到满级。"
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
	var attributes: UnitBaseAttributes = progression.unit_base_attributes
	attributes.set_attribute_value(UnitBaseAttributes.STRENGTH, 12)
	attributes.set_attribute_value(UnitBaseAttributes.AGILITY, 14)
	var strength_before: int = attributes.get_attribute_value(UnitBaseAttributes.STRENGTH)

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
				"amount": 100,
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
	_assert_eq(int(charge_progress.total_mastery_earned), 100, "技能熟练度奖励应在技能解锁后成功入账。")
	_assert_eq(int(charge_progress.skill_level), 1, "100 点冲锋熟练度应将技能提升到 1 级。")
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


func _test_party_state_from_dict_rejects_pending_character_reward_schema_defaults() -> void:
	for field_name in [
		"reward_id",
		"member_id",
		"member_name",
		"source_type",
		"source_id",
		"source_label",
		"summary_text",
		"entries",
	]:
		var payload := _build_party_state_payload_with_pending_character_reward()
		_erase_pending_character_reward_field(payload, field_name)
		_assert_true(
			PartyState.from_dict(payload) == null,
			"缺少 PendingCharacterReward.%s 的 PartyState payload 应直接拒绝。" % field_name
		)

	for field_name in [
		"entry_type",
		"target_id",
		"target_label",
		"amount",
		"reason_text",
	]:
		var payload := _build_party_state_payload_with_pending_character_reward()
		_erase_pending_character_reward_entry_field(payload, field_name)
		_assert_true(
			PartyState.from_dict(payload) == null,
			"缺少 PendingCharacterRewardEntry.%s 的 PartyState payload 应直接拒绝。" % field_name
		)

	for field_case in [
		{"field": "reward_id", "value": ""},
		{"field": "reward_id", "value": 123},
		{"field": "member_id", "value": ""},
		{"field": "member_id", "value": 123},
		{"field": "member_name", "value": 123},
		{"field": "source_type", "value": ""},
		{"field": "source_type", "value": 123},
		{"field": "source_id", "value": ""},
		{"field": "source_id", "value": 123},
		{"field": "source_label", "value": 123},
		{"field": "summary_text", "value": 123},
		{"field": "entries", "value": {}},
		{"field": "entries", "value": []},
	]:
		var payload := _build_party_state_payload_with_pending_character_reward()
		_set_pending_character_reward_field_value(
			payload,
			String(field_case.get("field", "")),
			field_case.get("value")
		)
		_assert_true(
			PartyState.from_dict(payload) == null,
			"PendingCharacterReward.%s 非法的 PartyState payload 应直接拒绝。" % String(field_case.get("field", ""))
		)

	for field_case in [
		{"field": "entry_type", "value": ""},
		{"field": "entry_type", "value": 123},
		{"field": "target_id", "value": ""},
		{"field": "target_id", "value": 123},
		{"field": "target_label", "value": 123},
		{"field": "amount", "value": "1"},
		{"field": "amount", "value": 0},
		{"field": "reason_text", "value": 123},
	]:
		var payload := _build_party_state_payload_with_pending_character_reward()
		_set_pending_character_reward_entry_field_value(
			payload,
			String(field_case.get("field", "")),
			field_case.get("value")
		)
		_assert_true(
			PartyState.from_dict(payload) == null,
			"PendingCharacterRewardEntry.%s 非法的 PartyState payload 应直接拒绝。" % String(field_case.get("field", ""))
		)

	var invalid_reward_entry_payload := _build_party_state_payload_with_pending_character_reward()
	invalid_reward_entry_payload["pending_character_rewards"] = ["invalid"]
	_assert_true(
		PartyState.from_dict(invalid_reward_entry_payload) == null,
		"pending_character_rewards 中包含非字典条目时应直接拒绝。"
	)

	var invalid_nested_entry_payload := _build_party_state_payload_with_pending_character_reward()
	_set_pending_character_reward_entries_value(invalid_nested_entry_payload, ["invalid"])
	_assert_true(
		PartyState.from_dict(invalid_nested_entry_payload) == null,
		"PendingCharacterReward.entries 中包含非字典条目时应直接拒绝。"
	)


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
	var submit_item_missing_target_quest := QuestDef.new()
	submit_item_missing_target_quest.quest_id = &"contract_supply_delivery_missing_target"
	submit_item_missing_target_quest.display_name = "物资缴纳缺目标值"
	submit_item_missing_target_quest.objective_defs = [
		{
			"objective_id": "deliver_ore",
			"objective_type": QuestDef.OBJECTIVE_SUBMIT_ITEM,
			"target_id": "iron_ore",
		},
	]

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
			submit_item_missing_target_quest.quest_id: submit_item_missing_target_quest,
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

	var missing_target_submit_state := QuestState.new()
	missing_target_submit_state.quest_id = submit_item_missing_target_quest.quest_id
	missing_target_submit_state.mark_accepted(9)
	party_state.set_active_quest_state(missing_target_submit_state)
	var missing_target_submit_result := manager.submit_item_objective(submit_item_missing_target_quest.quest_id, &"deliver_ore", 10)
	_assert_true(not bool(missing_target_submit_result.get("ok", true)), "submit_item objective 缺 target_value 时应正式失败。")
	_assert_eq(String(missing_target_submit_result.get("error_code", "")), "invalid_submit_item_objective", "缺 target_value 时不应按默认 1 提交任务。")
	var active_missing_target_quest: QuestState = party_state.get_active_quest_state(submit_item_missing_target_quest.quest_id)
	_assert_true(active_missing_target_quest != null, "缺 target_value 时任务应继续停留在 active_quests。")
	if active_missing_target_quest != null:
		_assert_eq(active_missing_target_quest.get_objective_progress(&"deliver_ore"), 0, "缺 target_value 时不应推进 quest objective。")
	_assert_true(not party_state.has_claimable_quest(submit_item_missing_target_quest.quest_id), "缺 target_value 时任务不应进入 claimable_quests。")
	session.free()


func _test_quest_progress_events_require_formal_progress_schema() -> void:
	var quest_def := QuestDef.new()
	quest_def.quest_id = &"contract_formal_progress_event"
	quest_def.display_name = "正式进度事件"
	quest_def.objective_defs = [
		{
			"objective_id": "train_once",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_id": "service:training",
			"target_value": 2,
		},
	]
	var party_state := _make_party_state([&"hero"])
	var manager := CharacterManagementModule.new()
	manager.setup(
		party_state,
		{},
		{},
		{},
		{},
		{quest_def.quest_id: quest_def}
	)
	_assert_true(manager.accept_quest(quest_def.quest_id, 1), "测试任务应可被正式接取。")
	var active_quest: QuestState = party_state.get_active_quest_state(quest_def.quest_id)
	_assert_true(active_quest != null, "接取后应存在 active quest。")
	if active_quest == null:
		return

	var bad_events: Array = [
		{
			"event_type": "progress",
			"quest_id": String(quest_def.quest_id),
			"objective_id": "train_once",
			"world_step": 2,
			"amount": 1,
		},
		{
			"quest_id": String(quest_def.quest_id),
			"objective_id": "train_once",
			"progress_delta": 1,
			"world_step": 2,
		},
		{
			"event_type": "progress",
			"quest_id": String(quest_def.quest_id),
			"objective_id": "train_once",
			"progress_delta": "1",
			"world_step": 2,
		},
		{
			"event_type": "progress",
			"quest_id": String(quest_def.quest_id),
			"objective_id": "train_once",
			"progress_delta": 1,
			"world_step": 2,
			"target_value": "2",
		},
		{
			"event_type": "progress",
			"quest_id": String(quest_def.quest_id),
			"objective_id": "train_once",
			"progress_delta": 1,
		},
		{
			"event_type": "progress",
			"quest_id": String(quest_def.quest_id),
			"objective_id": "train_once",
			"progress_delta": 1,
			"world_step": "2",
		},
		{
			"event_type": "legacy_progress",
			"quest_id": String(quest_def.quest_id),
			"objective_id": "train_once",
			"progress_delta": 1,
			"world_step": 2,
		},
	]
	for bad_event in bad_events:
		var summary := manager.apply_quest_progress_events([bad_event], 2)
		_assert_eq((summary.get("progressed_quest_ids", []) as Array).size(), 0, "坏 quest progress event 不应推进任务。")
	_assert_eq(active_quest.get_objective_progress(&"train_once"), 0, "amount / 缺 event_type / 字符串字段 / 缺 world_step 不应被兼容成任务进度。")
	_assert_true(not party_state.has_claimable_quest(quest_def.quest_id), "坏 progress event 不应把任务推进到 claimable。")

	var formal_summary := manager.apply_quest_progress_events([
		{
			"event_type": "progress",
			"quest_id": String(quest_def.quest_id),
			"objective_id": "train_once",
			"progress_delta": 1,
			"world_step": 3,
		},
	], 3)
	_assert_eq((formal_summary.get("progressed_quest_ids", []) as Array).size(), 1, "正式 progress_delta 应能推进任务。")
	_assert_eq(active_quest.get_objective_progress(&"train_once"), 1, "直接 quest progress event 应从 QuestDef 读取 target_value。")
	_assert_true(not party_state.has_claimable_quest(quest_def.quest_id), "未达到 QuestDef target_value 前不应完成。")

	var matched_summary := manager.apply_quest_progress_events([
		{
			"event_type": "progress",
			"objective_type": String(QuestDef.OBJECTIVE_SETTLEMENT_ACTION),
			"target_id": "service:training",
			"progress_delta": 1,
			"world_step": 4,
		},
	], 4)
	_assert_eq((matched_summary.get("progressed_quest_ids", []) as Array).size(), 1, "按 objective_type/target_id 匹配的正式事件应推进任务。")
	_assert_true(party_state.has_claimable_quest(quest_def.quest_id), "达到正式 objective target_value 后任务应进入 claimable。")

	var missing_target_quest_def := QuestDef.new()
	missing_target_quest_def.quest_id = &"contract_missing_target_value"
	missing_target_quest_def.display_name = "缺目标值"
	missing_target_quest_def.objective_defs = [
		{
			"objective_id": "bad_target",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_id": "service:bad",
		},
	]
	var missing_target_party := _make_party_state([&"hero"])
	var missing_target_manager := CharacterManagementModule.new()
	missing_target_manager.setup(
		missing_target_party,
		{},
		{},
		{},
		{},
		{missing_target_quest_def.quest_id: missing_target_quest_def}
	)
	_assert_true(missing_target_manager.accept_quest(missing_target_quest_def.quest_id, 5), "缺 target_value 的坏夹具仍可用于验证 service 拒绝进度事件。")
	missing_target_manager.apply_quest_progress_events([
		{
			"event_type": "progress",
			"quest_id": String(missing_target_quest_def.quest_id),
			"objective_id": "bad_target",
			"progress_delta": 1,
			"world_step": 6,
		},
	], 6)
	var missing_target_state: QuestState = missing_target_party.get_active_quest_state(missing_target_quest_def.quest_id)
	_assert_true(missing_target_state != null, "缺 target_value 任务应保持 active。")
	if missing_target_state != null:
		_assert_eq(missing_target_state.get_objective_progress(&"bad_target"), 0, "缺正式 target_value 时不应按默认 1 推进任务。")


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

	var details_text: String = String(window.overview_label.text)
	_assert_text_contains(details_text, "成就摘要：", "队伍管理窗口应显示成就摘要标题。")
	_assert_text_contains(details_text, "已解锁：1", "队伍管理窗口应显示已解锁成就数。")
	_assert_text_contains(details_text, "进行中：1", "队伍管理窗口应显示进行中成就数。")
	_assert_text_contains(details_text, "最近解锁：首战归来", "队伍管理窗口应显示最近解锁成就名。")
	_assert_text_contains(details_text, "- 开刃 2 / 3", "队伍管理窗口应显示进行中的成就进度。")

	window.queue_free()
	await process_frame


func _test_party_management_window_ignores_legacy_equipment_state_dictionary() -> void:
	var party_state := _make_party_state([&"legacy", &"formal"])
	var legacy_member: PartyMemberState = party_state.get_member_state(&"legacy")
	var formal_member: PartyMemberState = party_state.get_member_state(&"formal")

	legacy_member.equipment_state = {
		&"main_hand": &"bronze_sword",
		"off_hand": "bronze_sword",
	}

	var formal_equipment := EquipmentState.new()
	var formal_instance := EquipmentInstanceState.create(&"bronze_sword", &"eq_000321")
	_assert_true(
		formal_equipment.set_equipped_entry(&"main_hand", &"bronze_sword", [&"main_hand"], formal_instance),
		"测试前置：正式 EquipmentState 应能写入主手装备。"
	)
	formal_member.equipment_state = formal_equipment

	var bronze_sword := ItemDef.new()
	bronze_sword.item_id = &"bronze_sword"
	bronze_sword.display_name = "青铜剑"
	bronze_sword.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	bronze_sword.equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON
	bronze_sword.equipment_slot_ids = ["main_hand"]

	var window = PartyManagementWindowScene.instantiate()
	root.add_child(window)
	await process_frame
	window.set_item_defs({&"bronze_sword": bronze_sword})
	window.show_party(party_state)
	await process_frame

	_assert_true(window.select_member(&"legacy"), "队伍管理窗口应能选中旧字典装备成员。")
	var legacy_text: String = String(window.equipment_label.text)
	_assert_text_contains(legacy_text, "已装备：0", "旧字典 equipment_state 不应被恢复为已装备物品。")
	_assert_text_contains(legacy_text, "主手：空", "旧字典 equipment_state 的主手槽应按空装备展示。")
	_assert_true(not legacy_text.contains("青铜剑"), "旧字典 equipment_state 不应显示旧 item 展示名。")

	_assert_true(window.select_member(&"formal"), "队伍管理窗口应能选中正式装备成员。")
	var formal_text: String = String(window.equipment_label.text)
	_assert_text_contains(formal_text, "已装备：1", "正式 EquipmentState 应按已装备数量展示。")
	_assert_text_contains(formal_text, "主手：青铜剑", "正式 EquipmentState 应显示当前装备物品。")

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
	_assert_text_contains(String(window.overview_label.text), "主角：是", "主角详情应显式标记主角身份。")

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


func _build_party_state_payload_with_pending_character_reward() -> Dictionary:
	var party_state := _make_party_state([&"hero"])
	var manager := _setup_manager(party_state, {})
	var reward = manager.build_pending_character_reward(
		&"hero",
		&"strict_reward",
		&"achievement",
		&"strict_reward",
		"严格奖励",
		[
			{
				"entry_type": String(AchievementRewardDef.TYPE_SKILL_UNLOCK),
				"target_id": "charge",
				"target_label": "冲锋",
				"amount": 1,
				"reason_text": "严格 schema 测试。",
			},
		],
		"用于严格 schema 测试"
	)
	party_state.enqueue_pending_character_reward(reward)
	return party_state.to_dict()


func _erase_pending_character_reward_field(payload: Dictionary, field_name: String) -> void:
	var reward_payload := _get_first_pending_character_reward_payload(payload)
	reward_payload.erase(field_name)
	_set_first_pending_character_reward_payload(payload, reward_payload)


func _set_pending_character_reward_field_value(payload: Dictionary, field_name: String, value) -> void:
	var reward_payload := _get_first_pending_character_reward_payload(payload)
	reward_payload[field_name] = value
	_set_first_pending_character_reward_payload(payload, reward_payload)


func _erase_pending_character_reward_entry_field(payload: Dictionary, field_name: String) -> void:
	var entry_payload := _get_first_pending_character_reward_entry_payload(payload)
	entry_payload.erase(field_name)
	_set_first_pending_character_reward_entry_payload(payload, entry_payload)


func _set_pending_character_reward_entry_field_value(payload: Dictionary, field_name: String, value) -> void:
	var entry_payload := _get_first_pending_character_reward_entry_payload(payload)
	entry_payload[field_name] = value
	_set_first_pending_character_reward_entry_payload(payload, entry_payload)


func _set_pending_character_reward_entries_value(payload: Dictionary, entries_value) -> void:
	var reward_payload := _get_first_pending_character_reward_payload(payload)
	reward_payload["entries"] = entries_value
	_set_first_pending_character_reward_payload(payload, reward_payload)


func _get_first_pending_character_reward_payload(payload: Dictionary) -> Dictionary:
	var pending_rewards: Array = (payload.get("pending_character_rewards", []) as Array).duplicate(true)
	if pending_rewards.is_empty():
		return {}
	return (pending_rewards[0] as Dictionary).duplicate(true)


func _set_first_pending_character_reward_payload(payload: Dictionary, reward_payload: Dictionary) -> void:
	var pending_rewards: Array = (payload.get("pending_character_rewards", []) as Array).duplicate(true)
	if pending_rewards.is_empty():
		pending_rewards.append(reward_payload)
	else:
		pending_rewards[0] = reward_payload
	payload["pending_character_rewards"] = pending_rewards


func _get_first_pending_character_reward_entry_payload(payload: Dictionary) -> Dictionary:
	var reward_payload := _get_first_pending_character_reward_payload(payload)
	var entries: Array = (reward_payload.get("entries", []) as Array).duplicate(true)
	if entries.is_empty():
		return {}
	return (entries[0] as Dictionary).duplicate(true)


func _set_first_pending_character_reward_entry_payload(payload: Dictionary, entry_payload: Dictionary) -> void:
	var reward_payload := _get_first_pending_character_reward_payload(payload)
	var entries: Array = (reward_payload.get("entries", []) as Array).duplicate(true)
	if entries.is_empty():
		entries.append(entry_payload)
	else:
		entries[0] = entry_payload
	reward_payload["entries"] = entries
	_set_first_pending_character_reward_payload(payload, reward_payload)


func _build_unit_progress_payload_with_child_entries() -> Dictionary:
	var progress := UnitProgress.new()
	progress.unit_id = &"hero"
	progress.display_name = "Hero"
	progress.character_level = 1

	var skill_progress := UnitSkillProgress.new()
	skill_progress.skill_id = &"test_strict_skill"
	skill_progress.is_learned = true
	skill_progress.skill_level = 1
	progress.set_skill_progress(skill_progress)

	var profession_progress := UnitProfessionProgress.new()
	profession_progress.profession_id = &"test_strict_profession"
	profession_progress.rank = 1
	var promotion_record := ProfessionPromotionRecord.new()
	promotion_record.new_rank = 2
	promotion_record.consumed_skill_ids = [&"test_strict_skill"]
	promotion_record.qualifier_skill_ids = [&"test_strict_skill"]
	promotion_record.snapshot_unit_base_attributes = {"strength": 3}
	promotion_record.timestamp = 123
	profession_progress.add_promotion_record(promotion_record)
	progress.set_profession_progress(profession_progress)

	var achievement_progress := AchievementProgressState.new()
	achievement_progress.achievement_id = &"test_strict_achievement"
	achievement_progress.current_value = 1
	progress.set_achievement_progress_state(achievement_progress)

	var pending_choice := PendingProfessionChoice.new()
	pending_choice.trigger_skill_ids = [&"test_strict_skill"]
	pending_choice.candidate_profession_ids = [&"test_strict_profession"]
	pending_choice.target_rank_map[&"test_strict_profession"] = 2
	pending_choice.qualifier_skill_pool_ids = [&"test_strict_skill"]
	pending_choice.assignable_skill_candidate_ids = [&"test_strict_skill"]
	pending_choice.required_qualifier_count = 1
	pending_choice.required_assigned_core_count = 1
	progress.pending_profession_choices.append(pending_choice)

	return progress.to_dict()


func _erase_unit_progress_child_field(payload: Dictionary, bucket: String, entry_id: String, field_name: String) -> void:
	var bucket_payload: Dictionary = (payload.get(bucket, {}) as Dictionary).duplicate(true)
	var entry_payload: Dictionary = (bucket_payload.get(entry_id, {}) as Dictionary).duplicate(true)
	entry_payload.erase(field_name)
	bucket_payload[entry_id] = entry_payload
	payload[bucket] = bucket_payload


func _set_unit_progress_child_field_value(payload: Dictionary, bucket: String, entry_id: String, field_name: String, value) -> void:
	var bucket_payload: Dictionary = (payload.get(bucket, {}) as Dictionary).duplicate(true)
	var entry_payload: Dictionary = (bucket_payload.get(entry_id, {}) as Dictionary).duplicate(true)
	entry_payload[field_name] = value
	bucket_payload[entry_id] = entry_payload
	payload[bucket] = bucket_payload


func _erase_pending_profession_choice_field(payload: Dictionary, field_name: String) -> void:
	var choice_payload := _get_first_pending_profession_choice_payload(payload)
	choice_payload.erase(field_name)
	_set_first_pending_profession_choice_payload(payload, choice_payload)


func _set_pending_profession_choice_field_value(payload: Dictionary, field_name: String, value) -> void:
	var choice_payload := _get_first_pending_profession_choice_payload(payload)
	choice_payload[field_name] = value
	_set_first_pending_profession_choice_payload(payload, choice_payload)


func _get_first_pending_profession_choice_payload(payload: Dictionary) -> Dictionary:
	var pending_choices: Array = (payload.get("pending_profession_choices", []) as Array).duplicate(true)
	if pending_choices.is_empty():
		return {}
	return (pending_choices[0] as Dictionary).duplicate(true)


func _set_first_pending_profession_choice_payload(payload: Dictionary, choice_payload: Dictionary) -> void:
	var pending_choices: Array = (payload.get("pending_profession_choices", []) as Array).duplicate(true)
	if pending_choices.is_empty():
		pending_choices.append(choice_payload)
	else:
		pending_choices[0] = choice_payload
	payload["pending_profession_choices"] = pending_choices


func _erase_promotion_record_field(payload: Dictionary, field_name: String) -> void:
	var record_payload := _get_first_promotion_record_payload(payload)
	record_payload.erase(field_name)
	_set_first_promotion_record_payload(payload, record_payload)


func _set_promotion_record_field_value(payload: Dictionary, field_name: String, value) -> void:
	var record_payload := _get_first_promotion_record_payload(payload)
	record_payload[field_name] = value
	_set_first_promotion_record_payload(payload, record_payload)


func _get_first_promotion_record_payload(payload: Dictionary) -> Dictionary:
	var professions_payload: Dictionary = (payload.get("professions", {}) as Dictionary).duplicate(true)
	var profession_payload: Dictionary = (professions_payload.get("test_strict_profession", {}) as Dictionary).duplicate(true)
	var promotion_history: Array = (profession_payload.get("promotion_history", []) as Array).duplicate(true)
	if promotion_history.is_empty():
		return {}
	return (promotion_history[0] as Dictionary).duplicate(true)


func _set_first_promotion_record_payload(payload: Dictionary, record_payload: Dictionary) -> void:
	var professions_payload: Dictionary = (payload.get("professions", {}) as Dictionary).duplicate(true)
	var profession_payload: Dictionary = (professions_payload.get("test_strict_profession", {}) as Dictionary).duplicate(true)
	var promotion_history: Array = (profession_payload.get("promotion_history", []) as Array).duplicate(true)
	if promotion_history.is_empty():
		promotion_history.append(record_payload)
	else:
		promotion_history[0] = record_payload
	profession_payload["promotion_history"] = promotion_history
	professions_payload["test_strict_profession"] = profession_payload
	payload["professions"] = professions_payload


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


func _make_test_growth_skill(
	skill_id: StringName,
	growth_tier: StringName,
	attribute_growth_progress: Dictionary
) -> SkillDef:
	var skill_def := SkillDef.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = String(skill_id)
	skill_def.icon_id = skill_id
	skill_def.skill_type = &"passive"
	skill_def.learn_source = &"book"
	skill_def.max_level = 3
	skill_def.mastery_curve = PackedInt32Array([1, 1, 1])
	skill_def.growth_tier = growth_tier
	skill_def.attribute_growth_progress = attribute_growth_progress.duplicate(true)
	return skill_def


func _make_test_learn_source_skill(skill_id: StringName, learn_source: StringName) -> SkillDef:
	var skill_def := SkillDef.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = String(skill_id)
	skill_def.icon_id = skill_id
	skill_def.skill_type = &"passive"
	skill_def.learn_source = learn_source
	skill_def.max_level = 1
	skill_def.mastery_curve = PackedInt32Array([10])
	return skill_def


func _make_test_combat_resource_skill(skill_id: StringName, mp_cost: int, aura_cost: int) -> SkillDef:
	var skill_def := SkillDef.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = String(skill_id)
	skill_def.icon_id = skill_id
	skill_def.skill_type = &"active"
	skill_def.learn_source = &"book"
	skill_def.combat_profile = CombatSkillDef.new()
	skill_def.combat_profile.skill_id = skill_id
	skill_def.combat_profile.mp_cost = mp_cost
	skill_def.combat_profile.aura_cost = aura_cost
	return skill_def


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
