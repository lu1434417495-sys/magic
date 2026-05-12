extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleAttackCheckPolicyService = preload("res://scripts/systems/battle/rules/battle_attack_check_policy_service.gd")
const BattleHitResolver = preload("res://scripts/systems/battle/rules/battle_hit_resolver.gd")
const BattleRepeatAttackResolver = preload("res://scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var hit_resolver := BattleHitResolver.new()
	var policy := BattleAttackCheckPolicyService.new()
	policy.setup(null, hit_resolver, null)
	var battle_state := BattleState.new()
	var active_unit := BattleUnitState.new()
	active_unit.unit_id = &"caster"
	active_unit.coord = Vector2i(1, 1)
	active_unit.known_skill_level_map[&"parity_skill"] = 3
	var target_unit := BattleUnitState.new()
	target_unit.unit_id = &"target"
	target_unit.coord = Vector2i(3, 1)
	var skill_def := _build_parity_skill()
	var repeat_effect := _build_repeat_effect()
	var repeat_stage_specs := BattleRepeatAttackResolver.build_stage_specs_from_repeat_attack_effect(
		active_unit,
		skill_def,
		repeat_effect,
		-1,
		true
	)
	var repeat_preview_context := policy.build_repeat_attack_stage_context(
		battle_state,
		active_unit,
		target_unit,
		skill_def,
		null,
		&"repeat_attack_preview",
		&"hud_preview"
	)
	var stage_spec := BattleRepeatAttackResolver.build_stage_spec_from_repeat_attack_effect(
		active_unit,
		skill_def,
		repeat_effect,
		2,
		0,
		true
	)
	var stage_context := policy.build_repeat_attack_stage_context(
		battle_state,
		active_unit,
		target_unit,
		skill_def,
		stage_spec
	)
	var attack_context := policy.build_attack_context(
		battle_state,
		active_unit,
		target_unit,
		skill_def
	)
	var preview_context := policy.build_attack_context(
		battle_state,
		active_unit,
		target_unit,
		skill_def,
		&"skill_attack_preview",
		&"hud_preview"
	)

	_assert_dict_eq(
		policy.build_attack_check(attack_context),
		hit_resolver.build_skill_attack_check(active_unit, target_unit, skill_def),
		"policy build_attack_check 应与 BattleHitResolver 零漂移。"
	)
	_assert_dict_eq(
		policy.build_attack_preview(preview_context),
		hit_resolver.build_skill_attack_preview(battle_state, active_unit, target_unit, skill_def),
		"policy build_attack_preview 应与 BattleHitResolver 零漂移。"
	)
	_assert_dict_eq(
		policy.build_repeat_attack_preview(repeat_preview_context, repeat_stage_specs),
		hit_resolver.build_repeat_attack_preview(battle_state, active_unit, target_unit, skill_def, repeat_effect),
		"policy build_repeat_attack_preview 应与 BattleHitResolver 零漂移。"
	)
	_assert_dict_eq(
		policy.build_fate_aware_repeat_attack_stage_hit_check(stage_context),
		hit_resolver.build_fate_aware_repeat_attack_stage_hit_check(battle_state, active_unit, target_unit, skill_def, repeat_effect, 2),
		"policy repeat stage fate-aware check 应与 BattleHitResolver 零漂移。"
	)

	if _failures.is_empty():
		print("Attack policy parity regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Attack policy parity regression: FAIL (%d)" % _failures.size())
	quit(1)


func _build_parity_skill() -> SkillDef:
	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = &"parity_skill"
	combat_profile.attack_roll_bonus = -2
	var skill_def := SkillDef.new()
	skill_def.skill_id = &"parity_skill"
	skill_def.combat_profile = combat_profile
	return skill_def


func _build_repeat_effect() -> CombatEffectDef:
	var effect := CombatEffectDef.new()
	effect.effect_type = &"repeat_attack_until_fail"
	effect.params = {
		"base_attack_bonus": 1,
		"follow_up_attack_penalty": 2,
		"penalty_free_stages_by_level": {3: 1},
	}
	return effect


func _assert_dict_eq(actual: Dictionary, expected: Dictionary, message: String) -> void:
	if actual != expected:
		_test.fail("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
