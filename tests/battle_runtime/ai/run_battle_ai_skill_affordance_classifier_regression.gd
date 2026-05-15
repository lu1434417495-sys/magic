extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BATTLE_AI_SKILL_AFFORDANCE_CLASSIFIER_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_skill_affordance_classifier.gd")
const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/skill_def.gd")
const COMBAT_SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/combat_skill_def.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const COMBAT_CAST_VARIANT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_cast_variant_def.gd")

var _test := TestRunner.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_unit_damage_skill_maps_to_hostile_unit_affordance()
	_test_ally_heal_skill_maps_to_support_affordance()
	_test_ground_control_skill_maps_to_ground_family()
	_test_random_chain_skill_emits_chain_and_positioning_families()
	_test_multi_unit_skill_emits_skill_and_positioning_families()
	_test_charge_path_variant_emits_charge_path_family()
	_test_passive_skill_is_not_generatable()
	_test.finish(self, "Battle AI skill affordance classifier regression")


func _test_unit_damage_skill_maps_to_hostile_unit_affordance() -> void:
	var record := _classify(_build_skill(
		&"bolt",
		&"unit",
		&"enemy",
		[_effect(&"damage")]
	))
	_test.assert_true(bool(record.get("is_generatable", false)), "敌方单体伤害技能应可生成。")
	_test.assert_true(record.get("affordances", []).has(&"unit_hostile.damage"), "敌方单体伤害技能应标为 unit_hostile.damage。")
	_test.assert_true(record.get("action_families", []).has(&"use_unit_skill"), "敌方单体伤害技能应生成 use_unit_skill family。")


func _test_ally_heal_skill_maps_to_support_affordance() -> void:
	var record := _classify(_build_skill(
		&"mend",
		&"unit",
		&"ally",
		[_effect(&"heal", &"ally")]
	))
	_test.assert_true(bool(record.get("is_generatable", false)), "友方治疗技能应可生成。")
	_test.assert_true(record.get("affordances", []).has(&"ally_heal"), "友方治疗技能应标为 ally_heal。")
	_test.assert_true(record.get("action_families", []).has(&"use_unit_skill"), "友方治疗技能仍应使用 unit skill action family。")


func _test_ground_control_skill_maps_to_ground_family() -> void:
	var record := _classify(_build_skill(
		&"mud_patch",
		&"ground",
		&"enemy",
		[_effect(&"terrain", &"enemy")]
	))
	_test.assert_true(bool(record.get("is_generatable", false)), "地面控制技能应可生成。")
	_test.assert_true(record.get("affordances", []).has(&"ground_control"), "地面控制技能应标为 ground_control。")
	_test.assert_true(record.get("action_families", []).has(&"use_ground_skill"), "地面控制技能应生成 use_ground_skill family。")


func _test_random_chain_skill_emits_chain_and_positioning_families() -> void:
	var skill = _build_skill(&"chain_arc", &"unit", &"enemy", [_effect(&"chain_damage")])
	skill.combat_profile.target_selection_mode = &"random_chain"
	skill.combat_profile.max_hits_per_target = 2
	var record := _classify(skill)
	_test.assert_true(record.get("affordances", []).has(&"random_chain"), "随机链技能应标为 random_chain。")
	_test.assert_true(record.get("action_families", []).has(&"use_random_chain_skill"), "随机链技能应生成 chain action family。")
	_test.assert_true(record.get("action_families", []).has(&"move_to_range"), "随机链技能应可生成 companion range move。")


func _test_multi_unit_skill_emits_skill_and_positioning_families() -> void:
	var skill = _build_skill(&"wide_shot", &"unit", &"enemy", [_effect(&"damage")])
	skill.combat_profile.target_selection_mode = &"multi_unit"
	skill.combat_profile.min_target_count = 2
	var record := _classify(skill)
	_test.assert_true(record.get("affordances", []).has(&"multi_unit"), "多目标技能应标为 multi_unit。")
	_test.assert_true(record.get("action_families", []).has(&"use_multi_unit_skill"), "多目标技能应生成 multi-unit action family。")
	_test.assert_true(record.get("action_families", []).has(&"move_to_multi_unit_skill_position"), "多目标技能应可生成 companion multi-unit move。")


func _test_charge_path_variant_emits_charge_path_family() -> void:
	var skill = _build_skill(&"trample", &"unit", &"enemy", [_effect(&"damage")])
	var variant = COMBAT_CAST_VARIANT_DEF_SCRIPT.new()
	variant.variant_id = &"charge_line"
	variant.min_skill_level = 1
	variant.effect_defs.append(_effect(&"charge"))
	variant.effect_defs.append(_effect(&"path_step_aoe"))
	skill.combat_profile.cast_variants.append(variant)
	var record := _classify(skill)
	_test.assert_true(record.get("affordances", []).has(&"charge_path_aoe"), "带 path_step_aoe 的冲锋变体应标为 charge_path_aoe。")
	_test.assert_true(record.get("action_families", []).has(&"use_charge_path_aoe"), "带 path_step_aoe 的冲锋变体应生成 charge path action family。")


func _test_passive_skill_is_not_generatable() -> void:
	var skill = _build_skill(&"passive_aura", &"unit", &"ally", [_effect(&"status", &"ally")])
	skill.skill_type = &"passive"
	var record := _classify(skill)
	_test.assert_false(bool(record.get("is_generatable", true)), "被动技能不应进入 AI action 生成。")
	_test.assert_eq(record.get("skip_reason", ""), "passive_or_no_combat", "被动技能应给出稳定 skip reason。")


func _classify(skill_def) -> Dictionary:
	var classifier = BATTLE_AI_SKILL_AFFORDANCE_CLASSIFIER_SCRIPT.new()
	return classifier.classify_skill(skill_def, 1)


func _build_skill(skill_id: StringName, target_mode: StringName, target_filter: StringName, effect_defs: Array) -> Resource:
	var skill = SKILL_DEF_SCRIPT.new()
	skill.skill_id = skill_id
	skill.display_name = String(skill_id)
	skill.skill_type = &"active"
	var combat = COMBAT_SKILL_DEF_SCRIPT.new()
	combat.target_mode = target_mode
	combat.target_team_filter = target_filter
	combat.range_pattern = &"fixed"
	combat.range_value = 5
	for effect_def in effect_defs:
		combat.effect_defs.append(effect_def)
	skill.combat_profile = combat
	return skill


func _effect(effect_type: StringName, effect_filter: StringName = &"") -> Resource:
	var effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect.effect_type = effect_type
	effect.effect_target_team_filter = effect_filter
	if effect_type == &"status":
		effect.status_id = &"rooted"
	if effect_type == &"terrain":
		effect.terrain_effect_id = &"mud"
	return effect
