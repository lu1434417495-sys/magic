using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_shared_test_fixture_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestHarnessRecordsFailures();
        TestStubRngRollsAreClampedAndCounted();
        TestLocalBattleFixtureBuildsStateAndUnits();
        TestFixedResolversUseInjectedRolls();
        RequestTestExit(_test.Finish("Shared test fixture regression"));
    }

    private void TestHarnessRecordsFailures()
    {
        var localHarness = new TestHarness();
        localHarness.Eq(1, 2, "local failure should be recorded");
        _test.Eq(localHarness.Failures.Count, 1, "TestHarness 应记录失败数量。");
        _test.True(localHarness.Failures.Count > 0, "TestHarness 应能报告存在失败。");
    }

    private void TestStubRngRollsAreClampedAndCounted()
    {
        var rng = new StubRng(new[] { 25, 4 });
        _test.Eq(rng.RandiRange(1, 20), 20, "StubRng 应 clamp 过大的注入掷骰。");
        _test.Eq(rng.RandiRange(1, 20), 4, "StubRng 应按顺序返回注入掷骰。");
        _test.Eq(rng.CallCount, 2, "StubRng 应记录调用次数。");
        _test.Eq(rng.RemainingCount(), 0, "StubRng 应暴露剩余 roll 数。");
    }

    private void TestLocalBattleFixtureBuildsStateAndUnits()
    {
        BattleUnitState player = BattleTestFixture.BuildUnit(
            "hero",
            "player",
            Vector2I.Zero,
            currentAp: 3
        );
        BattleUnitState enemy = BattleTestFixture.BuildUnit("enemy", "enemy", new Vector2I(1, 0));
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "shared_fixture_contract",
            new Vector2I(2, 1),
            new[] { player },
            new[] { enemy }
        );
        BattleState state = fixture.State;

        _test.Eq(state.CellCount, 2, "C# fixture 应按地图尺寸生成格子。");
        _test.Eq(state.active_unit_id, new StringName("hero"), "C# fixture 应默认首个友军为 active unit。");
        _test.Eq(player.GetCurrentAp(), 3, "C# fixture 应应用 unit options。");
        _test.Eq(enemy.faction_id, new StringName("enemy"), "C# fixture enemy helper 应设置敌方阵营。");
        _test.True(fixture.Runtime.GetState() == state, "C# fixture 应能安装 runtime battle state。");
        fixture.Dispose();
    }

    private void TestFixedResolversUseInjectedRolls()
    {
        var resolver = new FixedRollDamageResolver(new GArray { 2 }, new GArray { 20 });
        _test.True(resolver is BattleDamageResolver, "FixedRollDamageResolver 应继承 BattleDamageResolver。");

        BattleUnitState source = BattleTestFixture.BuildUnit("source", "player", Vector2I.Zero);
        BattleUnitState target = BattleTestFixture.BuildUnit("target", "enemy", Vector2I.Right);
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "physical_slash",
            power: 1,
            diceCount: 1,
            diceSides: 6
        );

        using GodotProjectionLease<GDictionary> resultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(
                resolver.ResolveEffects(
                    source,
                    target,
                    new[] { effect },
                    DamageResolutionContext.Empty()
                )
            );
        GDictionary result = resultLease.Value;
        _test.Eq(DictInt(result, "damage"), 3, "FixedRollDamageResolver 应使用注入 damage roll。");

        var hitResolver = new FixedHitResolver(17);
        AttackRollResult hit = hitResolver.RollAttackCheck(
            new BattleState(),
            new AttackCheckInput(requiredRoll: 10)
        );
        _test.True(hit.Success, "FixedHitResolver 应返回命中。");
        _test.Eq(hit.Roll, 17, "FixedHitResolver 应使用注入命中骰。");
    }

    private static int DictInt(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsInt32() : 0;
    }
}
