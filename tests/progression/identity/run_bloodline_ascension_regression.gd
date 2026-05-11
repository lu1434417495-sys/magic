extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const AgeProfileDef = preload("res://scripts/player/progression/age_profile_def.gd")
const AgeStageRule = preload("res://scripts/player/progression/age_stage_rule.gd")
const AscensionApplyService = preload("res://scripts/systems/progression/ascension_apply_service.gd")
const AscensionDef = preload("res://scripts/player/progression/ascension_def.gd")
const AscensionStageDef = preload("res://scripts/player/progression/ascension_stage_def.gd")
const BloodlineApplyService = preload("res://scripts/systems/progression/bloodline_apply_service.gd")
const BloodlineDef = preload("res://scripts/player/progression/bloodline_def.gd")
const BloodlineStageDef = preload("res://scripts/player/progression/bloodline_stage_def.gd")
const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const RaceDef = preload("res://scripts/player/progression/race_def.gd")
const RacialGrantedSkill = preload("res://scripts/player/progression/racial_granted_skill.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const StageAdvancementApplyService = preload("res://scripts/systems/progression/stage_advancement_apply_service.gd")
const StageAdvancementModifier = preload("res://scripts/player/progression/stage_advancement_modifier.gd")
const SubraceDef = preload("res://scripts/player/progression/subrace_def.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_apply_services_validate_before_mutation()
	_test_character_management_applies_identity_and_refreshes_grants()
	_test_stage_advancement_refreshes_effective_stage()
	_test_identity_summary_includes_identity_projection()

	if _failures.is_empty():
		print("Bloodline ascension regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Bloodline ascension regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_apply_services_validate_before_mutation() -> void:
	var bundle := _make_identity_bundle()
	var member := _make_member_state(&"hero")
	var bloodline_service := BloodlineApplyService.new()
	bloodline_service.setup(bundle)

	_assert_true(
		bloodline_service.apply_bloodline(member, &"titan", &"titan_awakened"),
		"合法 bloodline/stage 组合应写入成员身份。"
	)
	_assert_eq(member.bloodline_id, &"titan", "apply_bloodline 应写入 bloodline_id。")
	_assert_eq(member.bloodline_stage_id, &"titan_awakened", "apply_bloodline 应写入 bloodline_stage_id。")
	_assert_true(
		not bloodline_service.apply_bloodline(member, &"titan", &"dragon_awakened"),
		"BloodlineApplyService 应拒绝不属于该 bloodline 的 stage。"
	)
	_assert_eq(member.bloodline_stage_id, &"titan_awakened", "非法 bloodline apply 不应污染已存在状态。")

	var ascension_service := AscensionApplyService.new()
	ascension_service.setup(bundle)
	_assert_true(
		ascension_service.apply_ascension(member, &"dragon_ascension", &"dragon_awakened", 42),
		"符合 race/subrace/bloodline 条件时应能应用 ascension。"
	)
	_assert_eq(member.ascension_id, &"dragon_ascension", "apply_ascension 应写入 ascension_id。")
	_assert_eq(member.ascension_stage_id, &"dragon_awakened", "apply_ascension 应写入 ascension_stage_id。")
	_assert_eq(member.original_race_id_before_ascension, &"human", "首次 ascension 应保存原始 race。")
	_assert_eq(member.ascension_started_at_world_step, 42, "apply_ascension 应记录开始 world step。")

	var before_stage := member.ascension_stage_id
	_assert_true(
		not ascension_service.apply_ascension(member, &"elf_ascension", &"elf_awakened", 43),
		"AscensionApplyService 应拒绝不满足 allowed_race_ids 的升华。"
	)
	_assert_eq(member.ascension_stage_id, before_stage, "非法 ascension apply 不应污染已存在状态。")

	member.race_id = &"ascended_dragon"
	_assert_true(ascension_service.revoke_ascension(member), "revoke_ascension 应能清除当前升华。")
	_assert_eq(member.race_id, &"human", "revoke_ascension 默认应恢复原始 race。")
	_assert_eq(member.ascension_id, &"", "revoke_ascension 应清空 ascension_id。")
	_assert_eq(member.ascension_started_at_world_step, -1, "revoke_ascension 应清空开始 world step。")
	_assert_eq(member.original_race_id_before_ascension, &"", "revoke_ascension 应清空原始 race 备份。")

	var stage_service := StageAdvancementApplyService.new()
	stage_service.setup(bundle)
	_assert_true(
		stage_service.add_stage_advancement_modifier(member, &"growth_boon"),
		"符合身份条件时应能添加阶段提升 modifier。"
	)
	_assert_true(
		not stage_service.add_stage_advancement_modifier(member, &"growth_boon"),
		"重复添加阶段提升 modifier 应被拒绝。"
	)
	_assert_eq(member.active_stage_advancement_modifier_ids, [&"growth_boon"], "阶段提升 modifier 应保持去重列表。")
	_assert_true(
		stage_service.remove_stage_advancement_modifier(member, &"growth_boon"),
		"remove_stage_advancement_modifier 应能移除已存在 modifier。"
	)
	_assert_eq(member.active_stage_advancement_modifier_ids, [], "移除 modifier 后列表应为空。")


func _test_character_management_applies_identity_and_refreshes_grants() -> void:
	var bundle := _make_identity_bundle()
	var bloodline_skill := _make_skill(&"bloodline_skill", UnitSkillProgress.GRANTED_SOURCE_BLOODLINE)
	var bloodline_stage_skill := _make_skill(&"bloodline_stage_skill", UnitSkillProgress.GRANTED_SOURCE_BLOODLINE)
	var ascension_skill := _make_skill(&"ascension_skill", UnitSkillProgress.GRANTED_SOURCE_ASCENSION)
	var ascension_stage_skill := _make_skill(&"ascension_stage_skill", UnitSkillProgress.GRANTED_SOURCE_ASCENSION)
	var party_state := _make_party_state()
	var manager := CharacterManagementModule.new()
	manager.setup(
		party_state,
		{
			bloodline_skill.skill_id: bloodline_skill,
			bloodline_stage_skill.skill_id: bloodline_stage_skill,
			ascension_skill.skill_id: ascension_skill,
			ascension_stage_skill.skill_id: ascension_stage_skill,
		},
		{},
		{},
		{},
		{},
		Callable(),
		bundle
	)

	_assert_true(
		manager.apply_bloodline(&"hero", &"titan", &"titan_awakened"),
		"CharacterManagementModule.apply_bloodline 应委托服务并刷新成员。"
	)
	var member: PartyMemberState = party_state.get_member_state(&"hero")
	_assert_identity_granted_skill(member, &"bloodline_skill", UnitSkillProgress.GRANTED_SOURCE_BLOODLINE, &"titan")
	_assert_identity_granted_skill(member, &"bloodline_stage_skill", UnitSkillProgress.GRANTED_SOURCE_BLOODLINE, &"titan_awakened")

	_assert_true(manager.revoke_bloodline(&"hero"), "revoke_bloodline 应清空 bloodline 并触发技能撤销。")
	_assert_true(
		member.progression.get_skill_progress(&"bloodline_skill") == null,
		"revoke_bloodline 后 bloodline 来源技能应被撤销。"
	)
	_assert_true(
		member.progression.get_skill_progress(&"bloodline_stage_skill") == null,
		"revoke_bloodline 后 bloodline stage 来源技能应被撤销。"
	)

	_assert_true(
		manager.apply_ascension(&"hero", &"dragon_ascension", &"dragon_awakened", 11),
		"CharacterManagementModule.apply_ascension 应委托服务并刷新成员。"
	)
	_assert_identity_granted_skill(member, &"ascension_skill", UnitSkillProgress.GRANTED_SOURCE_ASCENSION, &"dragon_ascension")
	_assert_identity_granted_skill(member, &"ascension_stage_skill", UnitSkillProgress.GRANTED_SOURCE_ASCENSION, &"dragon_awakened")
	_assert_eq(member.effective_age_stage_id, &"dragon_awakened", "replaces_age_growth 的升华阶段应接管 effective_age_stage_id。")
	_assert_eq(member.effective_age_stage_source_type, &"ascension", "升华接管年龄阶段时应记录来源类型。")
	_assert_eq(member.body_size_category, &"large", "升华阶段体型 override 应刷新 body_size_category。")
	_assert_eq(member.body_size, 3, "升华阶段体型 override 应通过 BodySizeRules 刷新 body_size。")

	_assert_true(manager.revoke_ascension(&"hero"), "revoke_ascension 应清空 ascension 并触发技能撤销。")
	_assert_eq(member.body_size_category, &"medium", "撤销升华后体型应回到 race/subrace 解析结果。")
	_assert_eq(member.body_size, 2, "撤销升华后 body_size 应从 medium 重新派生。")
	_assert_true(
		member.progression.get_skill_progress(&"ascension_skill") == null,
		"revoke_ascension 后 ascension 来源技能应被撤销。"
	)
	_assert_true(
		member.progression.get_skill_progress(&"ascension_stage_skill") == null,
		"revoke_ascension 后 ascension stage 来源技能应被撤销。"
	)


func _test_stage_advancement_refreshes_effective_stage() -> void:
	var bundle := _make_identity_bundle()
	var party_state := _make_party_state()
	var manager := CharacterManagementModule.new()
	manager.setup(party_state, {}, {}, {}, {}, {}, Callable(), bundle)
	var member: PartyMemberState = party_state.get_member_state(&"hero")
	_assert_eq(member.effective_age_stage_id, &"adult", "测试前置：成员有效阶段应从 adult 开始。")

	_assert_true(
		manager.add_stage_advancement_modifier(&"hero", &"growth_boon"),
		"CMM 添加阶段提升 modifier 后应刷新 effective age stage。"
	)
	_assert_eq(member.active_stage_advancement_modifier_ids, [&"growth_boon"], "CMM 应通过 service 写入 active_stage_advancement_modifier_ids。")
	_assert_eq(member.effective_age_stage_id, &"old", "growth_boon 应把 adult 推进到 old。")
	_assert_eq(member.effective_age_stage_source_type, &"stage_advancement", "阶段提升应记录 effective stage 来源类型。")
	_assert_eq(member.effective_age_stage_source_id, &"growth_boon", "阶段提升应记录 effective stage 来源 id。")

	_assert_true(
		manager.remove_stage_advancement_modifier(&"hero", &"growth_boon"),
		"CMM 移除阶段提升 modifier 后应刷新 effective age stage。"
	)
	_assert_eq(member.effective_age_stage_id, &"adult", "移除 modifier 后 effective stage 应回到 natural stage。")
	_assert_eq(member.effective_age_stage_source_type, &"", "移除 modifier 后 effective stage 来源类型应清空。")


func _test_identity_summary_includes_identity_projection() -> void:
	var bundle := _make_identity_bundle()
	var skill_defs := {}
	for skill_id in [&"bloodline_skill", &"bloodline_stage_skill", &"ascension_skill", &"ascension_stage_skill"]:
		var skill := _make_skill(skill_id, &"bloodline")
		skill_defs[skill.skill_id] = skill
	var party_state := _make_party_state()
	var manager := CharacterManagementModule.new()
	manager.setup(party_state, skill_defs, {}, {}, {}, {}, Callable(), bundle)
	_assert_true(manager.apply_bloodline(&"hero", &"titan", &"titan_awakened"), "身份摘要测试前置：应能应用 bloodline。")
	_assert_true(manager.apply_ascension(&"hero", &"dragon_ascension", &"dragon_awakened", 11), "身份摘要测试前置：应能应用 ascension。")

	var summary := manager.get_identity_summary_for_member(&"hero")
	_assert_eq(String(summary.get("race_label", "")), "Human", "身份摘要应包含 race display_name。")
	_assert_eq(String(summary.get("subrace_label", "")), "High Human", "身份摘要应包含 subrace display_name。")
	_assert_eq(String(summary.get("bloodline_label", "")), "titan", "身份摘要应包含 bloodline display_name。")
	_assert_eq(String(summary.get("ascension_label", "")), "dragon_ascension", "身份摘要应包含 ascension display_name。")
	_assert_eq(String(summary.get("effective_age_stage_label", "")), "dragon_awakened", "身份摘要应读取刷新后的 effective stage。")
	_assert_eq(String(summary.get("body_size_category", "")), "large", "身份摘要应包含当前升华后的 body_size_category。")
	_assert_eq(int(summary.get("body_size", 0)), 3, "身份摘要应包含当前升华后的 body_size。")
	var damage_resistances: Dictionary = summary.get("damage_resistances", {})
	_assert_eq(damage_resistances.get(&"fire", &""), &"half", "身份摘要应合并 race damage_resistances。")
	_assert_eq(damage_resistances.get(&"freeze", &""), &"immune", "身份摘要应合并 subrace damage_resistances。")
	var save_tags: Array = summary.get("save_advantage_tags", [])
	_assert_true(save_tags.has(&"charm"), "身份摘要应包含 race save advantage tag。")
	_assert_true(save_tags.has(&"poison"), "身份摘要应包含 subrace save advantage tag。")
	var trait_lines: Array = summary.get("trait_summary", [])
	_assert_true(trait_lines.has("Human ambition"), "身份摘要应包含 race trait summary。")
	_assert_true(trait_lines.has("Dragon stage"), "身份摘要应包含 ascension stage trait summary。")
	var racial_skill_lines: Array = summary.get("racial_skill_lines", [])
	_assert_true(_array_contains_text(racial_skill_lines, "bloodline_skill"), "身份摘要应包含 bloodline grant 技能。")
	_assert_true(_array_contains_text(racial_skill_lines, "ascension_stage_skill"), "身份摘要应包含 ascension stage grant 技能。")


func _make_party_state() -> PartyState:
	var party_state := PartyState.new()
	var member := _make_member_state(&"hero")
	party_state.set_member_state(member)
	party_state.active_member_ids = [&"hero"]
	party_state.leader_member_id = &"hero"
	party_state.main_character_member_id = &"hero"
	return party_state


func _make_member_state(member_id: StringName) -> PartyMemberState:
	var member := PartyMemberState.new()
	member.member_id = member_id
	member.display_name = "Hero"
	member.race_id = &"human"
	member.subrace_id = &"high_human"
	member.age_profile_id = &"human_age"
	member.natural_age_stage_id = &"adult"
	member.effective_age_stage_id = &"adult"
	member.progression.unit_id = member_id
	member.progression.display_name = member.display_name
	member.progression.character_level = 1
	return member


func _make_identity_bundle() -> Dictionary:
	var bloodline_skill_grant := _make_granted_skill(&"bloodline_skill")
	var bloodline_stage_skill_grant := _make_granted_skill(&"bloodline_stage_skill")
	var ascension_skill_grant := _make_granted_skill(&"ascension_skill")
	var ascension_stage_skill_grant := _make_granted_skill(&"ascension_stage_skill")
	var race := _make_race()
	var subrace := _make_subrace()
	var age_profile := _make_age_profile()
	var bloodline := _make_bloodline(&"titan", [&"titan_awakened"], [bloodline_skill_grant])
	var bloodline_stage := _make_bloodline_stage(&"titan_awakened", &"titan", [bloodline_stage_skill_grant])
	var ascension := _make_ascension(
		&"dragon_ascension",
		[&"dragon_awakened"],
		[ascension_skill_grant],
		[&"human"],
		[&"high_human"],
		[]
	)
	ascension.replaces_age_growth = true
	var ascension_stage := _make_ascension_stage(&"dragon_awakened", &"dragon_ascension", [ascension_stage_skill_grant])
	var elf_ascension := _make_ascension(&"elf_ascension", [&"elf_awakened"], [], [&"elf"], [], [])
	var elf_stage := _make_ascension_stage(&"elf_awakened", &"elf_ascension", [])
	var growth_boon := _make_stage_advancement(&"growth_boon")
	return {
		"race_defs": {race.race_id: race},
		"subrace_defs": {subrace.subrace_id: subrace},
		"age_profile_defs": {age_profile.profile_id: age_profile},
		"bloodline_defs": {bloodline.bloodline_id: bloodline},
		"bloodline_stage_defs": {bloodline_stage.stage_id: bloodline_stage},
		"ascension_defs": {
			ascension.ascension_id: ascension,
			elf_ascension.ascension_id: elf_ascension,
		},
		"ascension_stage_defs": {
			ascension_stage.stage_id: ascension_stage,
			elf_stage.stage_id: elf_stage,
		},
		"stage_advancement_defs": {growth_boon.modifier_id: growth_boon},
	}


func _make_race() -> RaceDef:
	var race := RaceDef.new()
	race.race_id = &"human"
	race.display_name = "Human"
	race.description = "Fixture race."
	race.age_profile_id = &"human_age"
	race.default_subrace_id = &"high_human"
	race.subrace_ids = [&"high_human"]
	race.body_size_category = &"medium"
	race.base_speed = 6
	race.damage_resistances = {&"fire": &"half"}
	race.save_advantage_tags = [&"charm"]
	race.racial_trait_summary = ["Human ambition"]
	return race


func _make_subrace() -> SubraceDef:
	var subrace := SubraceDef.new()
	subrace.subrace_id = &"high_human"
	subrace.parent_race_id = &"human"
	subrace.display_name = "High Human"
	subrace.description = "Fixture subrace."
	subrace.damage_resistances = {&"freeze": &"immune"}
	subrace.save_advantage_tags = [&"poison"]
	subrace.racial_trait_summary = ["High human focus"]
	return subrace


func _make_age_profile() -> AgeProfileDef:
	var age_profile := AgeProfileDef.new()
	age_profile.profile_id = &"human_age"
	age_profile.race_id = &"human"
	var stage_rules: Array[AgeStageRule] = []
	stage_rules.append(_make_age_stage_rule(&"teen"))
	stage_rules.append(_make_age_stage_rule(&"adult"))
	stage_rules.append(_make_age_stage_rule(&"middle_age"))
	stage_rules.append(_make_age_stage_rule(&"old"))
	age_profile.stage_rules = stage_rules
	age_profile.creation_stage_ids = [&"adult"]
	age_profile.default_age_by_stage = {"adult": 18}
	return age_profile


func _make_age_stage_rule(stage_id: StringName) -> AgeStageRule:
	var rule := AgeStageRule.new()
	rule.stage_id = stage_id
	rule.display_name = String(stage_id)
	rule.description = "Fixture age stage."
	rule.trait_summary = ["Age stage %s" % String(stage_id)]
	return rule


func _make_bloodline(bloodline_id: StringName, stage_ids: Array[StringName], grants: Array) -> BloodlineDef:
	var bloodline := BloodlineDef.new()
	bloodline.bloodline_id = bloodline_id
	bloodline.display_name = String(bloodline_id)
	bloodline.description = "Fixture bloodline."
	bloodline.stage_ids = stage_ids
	bloodline.racial_granted_skills = _typed_grants(grants)
	bloodline.trait_summary = ["Bloodline %s" % String(bloodline_id)]
	return bloodline


func _make_bloodline_stage(stage_id: StringName, bloodline_id: StringName, grants: Array) -> BloodlineStageDef:
	var stage := BloodlineStageDef.new()
	stage.stage_id = stage_id
	stage.bloodline_id = bloodline_id
	stage.display_name = String(stage_id)
	stage.description = "Fixture bloodline stage."
	stage.racial_granted_skills = _typed_grants(grants)
	stage.trait_summary = ["Bloodline stage %s" % String(stage_id)]
	return stage


func _make_ascension(
	ascension_id: StringName,
	stage_ids: Array[StringName],
	grants: Array,
	allowed_race_ids: Array[StringName],
	allowed_subrace_ids: Array[StringName],
	allowed_bloodline_ids: Array[StringName]
) -> AscensionDef:
	var ascension := AscensionDef.new()
	ascension.ascension_id = ascension_id
	ascension.display_name = String(ascension_id)
	ascension.description = "Fixture ascension."
	ascension.stage_ids = stage_ids
	ascension.racial_granted_skills = _typed_grants(grants)
	ascension.allowed_race_ids = allowed_race_ids
	ascension.allowed_subrace_ids = allowed_subrace_ids
	ascension.allowed_bloodline_ids = allowed_bloodline_ids
	ascension.trait_summary = ["Ascension %s" % String(ascension_id)]
	return ascension


func _make_ascension_stage(stage_id: StringName, ascension_id: StringName, grants: Array) -> AscensionStageDef:
	var stage := AscensionStageDef.new()
	stage.stage_id = stage_id
	stage.ascension_id = ascension_id
	stage.display_name = String(stage_id)
	stage.description = "Fixture ascension stage."
	stage.racial_granted_skills = _typed_grants(grants)
	stage.body_size_category_override = &"large"
	stage.trait_summary = ["Dragon stage"]
	return stage


func _make_stage_advancement(modifier_id: StringName) -> StageAdvancementModifier:
	var modifier := StageAdvancementModifier.new()
	modifier.modifier_id = modifier_id
	modifier.display_name = String(modifier_id)
	modifier.target_axis = StageAdvancementModifier.TARGET_AXIS_FULL
	modifier.stage_offset = 2
	modifier.max_stage_id = &"old"
	modifier.applies_to_race_ids = [&"human"]
	return modifier


func _make_skill(skill_id: StringName, learn_source: StringName) -> SkillDef:
	var skill := SkillDef.new()
	skill.skill_id = skill_id
	skill.display_name = String(skill_id)
	skill.icon_id = skill_id
	skill.description = "Fixture skill."
	skill.skill_type = &"passive"
	skill.learn_source = learn_source
	skill.max_level = 3
	skill.mastery_curve = PackedInt32Array([10, 20, 30])
	return skill


func _make_granted_skill(skill_id: StringName) -> RacialGrantedSkill:
	var grant := RacialGrantedSkill.new()
	grant.skill_id = skill_id
	grant.minimum_skill_level = 1
	grant.grant_level = 1
	grant.charge_kind = RacialGrantedSkill.CHARGE_KIND_PER_BATTLE
	grant.charges = 1
	return grant


func _typed_grants(values: Array) -> Array[RacialGrantedSkill]:
	var grants: Array[RacialGrantedSkill] = []
	for value in values:
		var grant := value as RacialGrantedSkill
		if grant == null:
			continue
		grants.append(grant)
	return grants


func _assert_identity_granted_skill(
	member: PartyMemberState,
	skill_id: StringName,
	expected_source_type: StringName,
	expected_source_id: StringName
) -> void:
	var skill_progress = member.progression.get_skill_progress(skill_id) if member != null and member.progression != null else null
	_assert_true(skill_progress != null and skill_progress.is_learned, "%s 应已被身份授予。" % String(skill_id))
	if skill_progress == null:
		return
	_assert_eq(skill_progress.granted_source_type, expected_source_type, "%s 身份技能来源类型应匹配。" % String(skill_id))
	_assert_eq(skill_progress.granted_source_id, expected_source_id, "%s 身份技能来源 id 应匹配。" % String(skill_id))


func _array_contains_text(values: Array, needle: String) -> bool:
	for value in values:
		if String(value).contains(needle):
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
