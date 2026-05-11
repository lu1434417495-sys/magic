extends SceneTree

const BattleDamageResolver = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const FateAttackFormula = preload("res://scripts/systems/battle/fate/fate_attack_formula.gd")
const BattleTestFixture = preload("res://tests/shared/battle_test_fixture.gd")
const BattleRuntimeTestHelpers = preload("res://tests/shared/battle_runtime_test_helpers.gd")
const SharedDamageResolvers = preload("res://tests/shared/stub_damage_resolvers.gd")
const SharedHitResolvers = preload("res://tests/shared/stub_hit_resolvers.gd")
const StubBattleMasteryGateway = preload("res://tests/shared/stub_battle_mastery_gateway.gd")
const StubRng = preload("res://tests/shared/stub_rng.gd")
const TestRunner = preload("res://tests/shared/test_runner.gd")

var _test := TestRunner.new()
var _fixture := BattleTestFixture.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_test_runner_records_failures()
	_test_stub_rng_rolls_are_clamped_and_counted()
	_test_battle_fixture_builds_state_and_units()
	_test_battle_fixture_installs_and_places_runtime_state()
	_test_fixed_roll_damage_resolver_uses_injected_rolls()
	_test_fixed_combat_helpers_install_shared_resolvers()
	_test_battle_mastery_gateway_records_grants()
	_test.finish(self, "Shared test fixture regression")


func _test_test_runner_records_failures() -> void:
	var local_runner := TestRunner.new()
	local_runner.assert_eq(1, 2, "local failure should be recorded")
	_test.assert_eq(local_runner.failure_count(), 1, "TestRunner 应记录失败数量。")
	_test.assert_true(local_runner.has_failures(), "TestRunner 应能报告存在失败。")


func _test_stub_rng_rolls_are_clamped_and_counted() -> void:
	var rng := StubRng.new([25, 4])
	_test.assert_eq(FateAttackFormula.roll_die_with_disadvantage_rule(20, true, rng), 4, "StubRng 应注入并 clamp 掷骰。")
	_test.assert_eq(rng.call_count, 2, "StubRng 应记录调用次数。")
	_test.assert_eq(rng.remaining_count(), 0, "StubRng 应暴露剩余 roll 数。")


func _test_battle_fixture_builds_state_and_units() -> void:
	var state = _fixture.build_state({"battle_id": &"shared_fixture_contract", "map_size": Vector2i(2, 1)})
	var player = _fixture.build_unit(&"hero", {"coord": Vector2i(0, 0), "current_ap": 3}) as BattleUnitState
	var enemy = _fixture.build_enemy_unit(&"enemy", {"coord": Vector2i(1, 0)}) as BattleUnitState
	_fixture.add_units(state, [player], [enemy])
	_test.assert_eq(state.cells.size(), 2, "BattleTestFixture 应按地图尺寸生成格子。")
	_test.assert_eq(state.active_unit_id, &"hero", "BattleTestFixture 应默认首个友军为 active unit。")
	_test.assert_eq(player.current_ap, 3, "BattleTestFixture 应应用 unit options。")
	_test.assert_eq(enemy.faction_id, &"enemy", "BattleTestFixture enemy helper 应设置敌方阵营。")


func _test_battle_fixture_installs_and_places_runtime_state() -> void:
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, {}, {}, {})
	var state = _fixture.build_state({"battle_id": &"shared_fixture_runtime_contract", "map_size": Vector2i(2, 1)})
	var player = _fixture.build_unit(&"runtime_hero", {"coord": Vector2i(0, 0)}) as BattleUnitState
	var enemy = _fixture.build_enemy_unit(&"runtime_enemy", {"coord": Vector2i(1, 0)}) as BattleUnitState
	_fixture.add_units(state, [player], [enemy], player.unit_id)
	_test.assert_true(_fixture.place_unit(runtime, state, player), "BattleTestFixture 应能通过 runtime public grid service 放置友军。")
	_test.assert_true(_fixture.place_unit(runtime, state, enemy), "BattleTestFixture 应能通过 runtime public grid service 放置敌军。")
	_fixture.install_state(runtime, state)
	_test.assert_true(runtime.get_state() == state, "BattleTestFixture install_state 应安装 runtime battle state。")


func _test_fixed_roll_damage_resolver_uses_injected_rolls() -> void:
	var resolver := SharedDamageResolvers.FixedRollDamageResolver.new([2], [20])
	_test.assert_true(resolver is BattleDamageResolver, "FixedRollDamageResolver 应继承 BattleDamageResolver。")
	var source = _fixture.build_unit(&"source") as BattleUnitState
	var target = _fixture.build_enemy_unit(&"target") as BattleUnitState
	var effect := CombatEffectDef.new()
	effect.effect_type = &"damage"
	effect.power = 1
	effect.params = {"dice_count": 1, "dice_sides": 6}
	var result: Dictionary = resolver.resolve_effects(source, target, [effect])
	_test.assert_eq(int(result.get("damage", 0)), 3, "FixedRollDamageResolver 应使用注入 damage roll。")


func _test_fixed_combat_helpers_install_shared_resolvers() -> void:
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, {}, {}, {})
	BattleRuntimeTestHelpers.configure_fixed_combat(runtime)
	_test.assert_true(runtime.get_hit_resolver() is SharedHitResolvers.FixedHitResolver, "固定战斗 helper 应安装 fixed hit resolver。")
	_test.assert_true(runtime.get_damage_resolver() is SharedDamageResolvers.FixedSuccessOneDamageResolver, "固定战斗 helper 应安装 fixed success damage resolver。")


func _test_battle_mastery_gateway_records_grants() -> void:
	var gateway := StubBattleMasteryGateway.new()
	gateway.record_achievement_event(&"hero", &"skill_used")
	var delta = gateway.grant_battle_mastery(&"hero", &"slash", 7)
	_test.assert_eq(gateway.skill_used_events, 1, "Battle mastery gateway stub 应记录 skill_used。")
	_test.assert_eq(gateway.grants.size(), 1, "Battle mastery gateway stub 应记录 mastery grant。")
	_test.assert_eq(delta.member_id, &"hero", "Battle mastery gateway stub 应返回 delta。")
