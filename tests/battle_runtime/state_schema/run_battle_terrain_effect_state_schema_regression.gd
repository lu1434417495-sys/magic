extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleTerrainEffectState = preload("res://scripts/systems/battle/terrain/battle_terrain_effect_state.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_params_lifetime_policy_roundtrip()
	_test_top_level_lifetime_policy_is_rejected()
	_test_invalid_target_team_filter_is_rejected()
	if _failures.is_empty():
		print("Battle terrain effect state schema regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle terrain effect state schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_params_lifetime_policy_roundtrip() -> void:
	var effect := _build_effect()
	var restored := BattleTerrainEffectState.from_dict(effect.to_dict()) as BattleTerrainEffectState
	_assert_true(restored != null, "terrain effect state roundtrip 应恢复对象。")
	_assert_eq(String(restored.params.get("lifetime_policy", "")) if restored != null else "", "battle", "lifetime_policy 应只通过 params roundtrip。")
	_assert_eq(int(restored.remaining_tu) if restored != null else -1, 0, "battle lifetime terrain effect 应允许 remaining_tu=0。")
	_assert_eq(int(restored.tick_interval_tu) if restored != null else -1, 0, "battle lifetime terrain effect 应允许 tick_interval_tu=0。")


func _test_top_level_lifetime_policy_is_rejected() -> void:
	var payload := _build_effect().to_dict()
	payload["lifetime_policy"] = "battle"
	_assert_true(BattleTerrainEffectState.from_dict(payload) == null, "terrain effect strict schema 应拒绝顶层 lifetime_policy 字段。")


func _test_invalid_target_team_filter_is_rejected() -> void:
	var payload := _build_effect().to_dict()
	payload["target_team_filter"] = "hostile"
	_assert_true(BattleTerrainEffectState.from_dict(payload) == null, "terrain effect state 不应接受 hostile 作为 target_team_filter。")


func _build_effect() -> BattleTerrainEffectState:
	var effect := BattleTerrainEffectState.new()
	effect.field_instance_id = &"meteor_crater_core_1"
	effect.effect_id = &"meteor_swarm_crater_core"
	effect.effect_type = &"none"
	effect.source_unit_id = &"caster"
	effect.source_skill_id = &"mage_meteor_swarm"
	effect.target_team_filter = &"any"
	effect.power = 0
	effect.damage_tag = &""
	effect.remaining_tu = 0
	effect.tick_interval_tu = 0
	effect.next_tick_at_tu = 0
	effect.stack_behavior = &"refresh"
	effect.params = {
		"lifetime_policy": "battle",
		"move_cost_delta": 3,
		"render_overlay_id": "meteor_crater_core",
	}
	return effect


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_test.fail("%s actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
