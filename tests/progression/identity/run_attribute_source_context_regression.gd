extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const AgeProfileDef = preload("res://scripts/player/progression/AgeProfileDef.cs")
const AgeStageRule = preload("res://scripts/player/progression/AgeStageRule.cs")
const AscensionDef = preload("res://scripts/player/progression/AscensionDef.cs")
const AscensionStageDef = preload("res://scripts/player/progression/AscensionStageDef.cs")
const AttributeModifier = preload("res://scripts/player/progression/AttributeModifier.cs")
const AttributeSnapshot = preload("res://scripts/player/progression/AttributeSnapshot.cs")
const AttributeService = preload("res://scripts/systems/attributes/AttributeService.cs")
const AttributeSourceContext = preload("res://scripts/systems/attributes/AttributeSourceContext.cs")
const BloodlineDef = preload("res://scripts/player/progression/BloodlineDef.cs")
const BloodlineStageDef = preload("res://scripts/player/progression/BloodlineStageDef.cs")
const CharacterManagementModule = preload("res://scripts/systems/progression/CharacterManagementModule.cs")
const PartyMemberState = preload("res://scripts/player/progression/PartyMemberState.cs")
const PartyState = preload("res://scripts/player/progression/PartyState.cs")
const RaceDef = preload("res://scripts/player/progression/RaceDef.cs")
const StageAdvancementModifier = preload("res://scripts/player/progression/StageAdvancementModifier.cs")
const SubraceDef = preload("res://scripts/player/progression/SubraceDef.cs")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_attribute_snapshot_exposes_base_attribute_modifiers()
	_test_attribute_service_setup_context_applies_identity_modifiers()
	_test_character_management_builds_attribute_source_context()

	if _failures.is_empty():
		print("Attribute source context regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Attribute source context regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_attribute_snapshot_exposes_base_attribute_modifiers() -> void:
	var direct_snapshot := AttributeSnapshot.new()
	direct_snapshot.set_value(&"strength", 8)
	_assert_eq(direct_snapshot.get_value(AttributeSnapshot.STRENGTH_MODIFIER()), -1, "直接写入 snapshot 六维时应同步调整值。")

	var progress := _make_progress(&"modifier")
	progress.unit_base_attributes.set_attribute_value(&"strength", 8)
	progress.unit_base_attributes.set_attribute_value(&"agility", 9)
	progress.unit_base_attributes.set_attribute_value(&"constitution", 10)
	progress.unit_base_attributes.set_attribute_value(&"perception", 11)
	progress.unit_base_attributes.set_attribute_value(&"intelligence", 12)
	progress.unit_base_attributes.set_attribute_value(&"willpower", 20)

	var service = AttributeService.new()
	service.setup(progress)
	var snapshot = service.get_snapshot()
	_assert_eq(snapshot.get_value(AttributeService.STRENGTH_MODIFIER_ID()), -1, "snapshot 应暴露力量调整值。")
	_assert_eq(snapshot.get_value(AttributeService.AGILITY_MODIFIER_ID()), -1, "snapshot 应暴露敏捷调整值。")
	_assert_eq(snapshot.get_value(AttributeService.CONSTITUTION_MODIFIER_ID()), 0, "snapshot 应暴露体质调整值。")
	_assert_eq(snapshot.get_value(AttributeService.PERCEPTION_MODIFIER_ID()), 0, "snapshot 应暴露感知调整值。")
	_assert_eq(snapshot.get_value(AttributeService.INTELLIGENCE_MODIFIER_ID()), 1, "snapshot 应暴露智力调整值。")
	_assert_eq(snapshot.get_value(AttributeService.WILLPOWER_MODIFIER_ID()), 5, "snapshot 应暴露意志调整值。")
	_assert_eq(int(snapshot.to_dict().get("strength_modifier", 999)), -1, "snapshot 字典应包含力量调整值。")


func _test_attribute_service_setup_context_applies_identity_modifiers() -> void:
	var progress := _make_progress(&"direct")
	var context := AttributeSourceContext.new()
	context.unit_progress = progress
	context.race_def = _make_race([_make_modifier(&"strength", 1)])
	context.subrace_def = _make_subrace([_make_modifier(&"strength", 2)])
	context.age_stage_rule = _make_age_stage_rule(&"old", [_make_modifier(&"constitution", 3)])
	context.age_stage_source_type = &"stage_advancement"
	context.age_stage_source_id = &"growth_boon"
	context.bloodline_def = _make_bloodline(&"titan", [&"titan_awakened"], [_make_modifier(&"willpower", 1)])
	context.bloodline_stage_def = _make_bloodline_stage(&"titan_awakened", &"titan", [_make_modifier(&"strength", 4)])
	context.ascension_def = _make_ascension(&"dragon_ascension", [&"dragon_awakened"])
	context.ascension_stage_def = _make_ascension_stage(&"dragon_awakened", &"dragon_ascension", [
		_make_modifier(&"intelligence", 5),
		_make_modifier(&"perception", 6),
	])
	context.versatility_pick = &"agility"

	var service = AttributeService.new()
	service.call("setup_context", context)
	var snapshot = service.get_snapshot()
	_assert_eq(snapshot.get_value(&"strength"), 17, "race/subrace/bloodline stage 修正应叠加到力量。")
	_assert_eq(snapshot.get_value(&"agility"), 11, "versatility_pick 应作为独立 +1 修正进入敏捷。")
	_assert_eq(snapshot.get_value(&"constitution"), 13, "effective age stage 修正应进入体质。")
	_assert_eq(snapshot.get_value(&"perception"), 16, "ascension stage 修正应进入感知。")
	_assert_eq(snapshot.get_value(&"intelligence"), 15, "ascension 修正应进入智力。")
	_assert_eq(snapshot.get_value(&"willpower"), 11, "bloodline 修正应进入意志。")
	_assert_eq(service.get_modifier(&"strength"), 3, "get_modifier 应使用 5e 属性修正公式。")


func _test_character_management_builds_attribute_source_context() -> void:
	var party_state := PartyState.new()
	var member := PartyMemberState.new()
	member.member_id = &"hero"
	member.display_name = "Hero"
	member.race_id = &"human"
	member.subrace_id = &"high_human"
	member.age_profile_id = &"human_age"
	member.natural_age_stage_id = &"adult"
	member.effective_age_stage_id = &"adult"
	member.versatility_pick = &"perception"
	member.progression = _make_progress(&"hero")
	party_state.set_member_state(member)
	party_state.active_member_ids = [&"hero"]
	party_state.leader_member_id = &"hero"
	party_state.main_character_member_id = &"hero"

	var bundle := _make_content_bundle()
	var manager := CharacterManagementModule.new()
	manager.setup(party_state, {}, {}, {}, {}, {}, Callable(), bundle)
	_assert_true(
		manager.add_stage_advancement_modifier(&"hero", &"growth_boon"),
		"CMM 应通过 stage advancement service 写入长期阶段提升。"
	)
	_assert_true(
		manager.apply_bloodline(&"hero", &"titan", &"titan_awakened"),
		"CMM 应通过 bloodline service 写入血脉身份。"
	)

	var context = manager.build_attribute_source_context(&"hero")
	_assert_true(context.age_stage_rule != null and context.age_stage_rule.stage_id == &"old", "CMM context 应解析 effective age stage rule。")
	_assert_eq(context.age_stage_source_type, &"stage_advancement", "CMM context 应保留 effective stage 来源类型。")
	_assert_eq(context.age_stage_source_id, &"growth_boon", "CMM context 应保留 effective stage 来源 id。")

	var snapshot = manager.get_member_attribute_snapshot(&"hero")
	_assert_eq(snapshot.get_value(&"strength"), 11, "CMM snapshot 应包含 race 属性修正。")
	_assert_eq(snapshot.get_value(&"agility"), 12, "CMM snapshot 应包含 subrace 属性修正。")
	_assert_eq(snapshot.get_value(&"constitution"), 14, "CMM snapshot 应包含 stage advancement 推导出的 age stage 修正。")
	_assert_eq(snapshot.get_value(&"perception"), 11, "CMM snapshot 应包含 versatility 修正且不改写 base。")
	_assert_eq(snapshot.get_value(&"willpower"), 13, "CMM snapshot 应包含 bloodline 属性修正。")
	_assert_eq(member.progression.unit_base_attributes.get_attribute_value(&"perception"), 10, "versatility 不应持久改写基础属性。")


func _make_content_bundle() -> Dictionary:
	var race := _make_race([_make_modifier(&"strength", 1)])
	var subrace := _make_subrace([_make_modifier(&"agility", 2)])
	var age_profile := AgeProfileDef.new()
	age_profile.profile_id = &"human_age"
	age_profile.race_id = &"human"
	var stage_rules: Array[AgeStageRule] = []
	stage_rules.append(_make_age_stage_rule(&"adult", []))
	stage_rules.append(_make_age_stage_rule(&"middle_age", []))
	stage_rules.append(_make_age_stage_rule(&"old", [_make_modifier(&"constitution", 4)]))
	age_profile.stage_rules = stage_rules
	age_profile.creation_stage_ids = [&"adult"]
	age_profile.default_age_by_stage = {"adult": 18}
	var bloodline := _make_bloodline(&"titan", [&"titan_awakened"], [_make_modifier(&"willpower", 3)])
	var bloodline_stage := _make_bloodline_stage(&"titan_awakened", &"titan", [])
	var growth_boon := StageAdvancementModifier.new()
	growth_boon.modifier_id = &"growth_boon"
	growth_boon.display_name = "Growth Boon"
	growth_boon.target_axis = &"full"
	growth_boon.stage_offset = 2
	growth_boon.max_stage_id = &"old"
	growth_boon.applies_to_race_ids = [&"human"]
	return {
		"race_defs": {race.race_id: race},
		"subrace_defs": {subrace.subrace_id: subrace},
		"age_profile_defs": {age_profile.profile_id: age_profile},
		"bloodline_defs": {bloodline.bloodline_id: bloodline},
		"bloodline_stage_defs": {bloodline_stage.stage_id: bloodline_stage},
		"ascension_defs": {},
		"ascension_stage_defs": {},
		"stage_advancement_defs": {growth_boon.modifier_id: growth_boon},
	}


func _make_progress(unit_id: StringName) -> UnitProgress:
	var progress := UnitProgress.new()
	progress.unit_id = unit_id
	progress.display_name = String(unit_id).capitalize()
	for attribute_id in [&"strength", &"agility", &"constitution", &"perception", &"intelligence", &"willpower"]:
		progress.unit_base_attributes.set_attribute_value(attribute_id, 10)
	return progress


func _make_race(modifiers: Array) -> RaceDef:
	var race := RaceDef.new()
	race.race_id = &"human"
	race.display_name = "Human"
	race.description = "Fixture race."
	race.age_profile_id = &"human_age"
	race.default_subrace_id = &"high_human"
	race.subrace_ids = [&"high_human"]
	race.body_size_category = &"medium"
	race.base_speed = 6
	race.attribute_modifiers = _typed_modifiers(modifiers)
	return race


func _make_subrace(modifiers: Array) -> SubraceDef:
	var subrace := SubraceDef.new()
	subrace.subrace_id = &"high_human"
	subrace.parent_race_id = &"human"
	subrace.display_name = "High Human"
	subrace.description = "Fixture subrace."
	subrace.attribute_modifiers = _typed_modifiers(modifiers)
	return subrace


func _make_age_stage_rule(stage_id: StringName, modifiers: Array) -> AgeStageRule:
	var rule := AgeStageRule.new()
	rule.stage_id = stage_id
	rule.display_name = String(stage_id)
	rule.description = "Fixture age stage."
	rule.attribute_modifiers = _typed_modifiers(modifiers)
	return rule


func _make_bloodline(bloodline_id: StringName, stage_ids: Array[StringName], modifiers: Array) -> BloodlineDef:
	var bloodline := BloodlineDef.new()
	bloodline.bloodline_id = bloodline_id
	bloodline.display_name = String(bloodline_id)
	bloodline.description = "Fixture bloodline."
	bloodline.stage_ids = stage_ids
	bloodline.attribute_modifiers = _typed_modifiers(modifiers)
	return bloodline


func _make_bloodline_stage(stage_id: StringName, bloodline_id: StringName, modifiers: Array) -> BloodlineStageDef:
	var stage := BloodlineStageDef.new()
	stage.stage_id = stage_id
	stage.bloodline_id = bloodline_id
	stage.display_name = String(stage_id)
	stage.description = "Fixture bloodline stage."
	stage.attribute_modifiers = _typed_modifiers(modifiers)
	return stage


func _make_ascension(ascension_id: StringName, stage_ids: Array[StringName]) -> AscensionDef:
	var ascension := AscensionDef.new()
	ascension.ascension_id = ascension_id
	ascension.display_name = String(ascension_id)
	ascension.description = "Fixture ascension."
	ascension.stage_ids = stage_ids
	return ascension


func _make_ascension_stage(stage_id: StringName, ascension_id: StringName, modifiers: Array) -> AscensionStageDef:
	var stage := AscensionStageDef.new()
	stage.stage_id = stage_id
	stage.ascension_id = ascension_id
	stage.display_name = String(stage_id)
	stage.description = "Fixture ascension stage."
	stage.attribute_modifiers = _typed_modifiers(modifiers)
	return stage


func _make_modifier(attribute_id: StringName, value: int) -> AttributeModifier:
	var modifier := AttributeModifier.new()
	modifier.attribute_id = attribute_id
	modifier.mode = &"flat"
	modifier.value = value
	return modifier


func _typed_modifiers(values: Array) -> Array[AttributeModifier]:
	var modifiers: Array[AttributeModifier] = []
	for value in values:
		var modifier := value as AttributeModifier
		if modifier == null:
			continue
		modifiers.append(modifier)
	return modifiers


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
