using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_shared_test_fixture_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestHarnessRecordsFailures();
        TestHarnessPreservesExplicitExitCodes();
        TestLocalBattleFixtureBuildsStateAndUnits();
        TestFixedResolversUseInjectedRolls();
        RequestTestExit(_test.Finish("Shared test fixture regression"));
    }

    private void TestHarnessRecordsFailures()
    {
        var localHarness = new TestHarness();
        localHarness.Eq(1, 2, "local failure should be recorded");
        TestResult result = localHarness.Finish("Local harness failure contract");

        _test.Eq(localHarness.Failures.Count, 1, "TestHarness 应记录失败数量。");
        _test.True(localHarness.Failures.Count > 0, "TestHarness 应能报告存在失败。");
        _test.False(result.Passed, "TestHarness.Finish 应把已记录断言失败映射为 failed result。");
        _test.Eq(result.ExitCode, 1, "TestHarness.Finish 应为失败结果返回非零退出码。");
        _test.Eq(
            result.Label,
            "Local harness failure contract",
            "TestHarness.Finish 应保留调用方标签。"
        );
        _test.Eq(
            result.Failures.Count,
            1,
            "TestHarness.Finish 应把已记录失败复制到不可变结果快照。"
        );
        _test.Eq(
            result.Failures[0],
            localHarness.Failures[0],
            "TestHarness.Finish 的结果应保留具体失败诊断。"
        );
    }

    private void TestHarnessPreservesExplicitExitCodes()
    {
        TestResult successResult = new TestHarness().Finish("Local harness success contract");
        _test.True(successResult.Passed, "无断言失败且退出码为 0 时应保持 PASS。");
        _test.Eq(successResult.ExitCode, 0, "成功结果应保留退出码 0。");

        TestResult incompleteResult = new TestHarness().Finish(
            "Local harness incomplete contract",
            2
        );
        _test.False(incompleteResult.Passed, "显式退出码 2 不应被标记为 PASS。");
        _test.Eq(incompleteResult.ExitCode, 2, "无断言失败时应保留显式退出码 2。");

        var failedHarness = new TestHarness();
        failedHarness.Fail("local assertion failure");
        TestResult failedResult = failedHarness.Finish(
            "Local harness assertion priority contract",
            2
        );
        _test.False(failedResult.Passed, "断言失败应保持 failed result。");
        _test.Eq(failedResult.ExitCode, 1, "断言失败应优先映射为退出码 1。");
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
        var resolver = new FixedRollDamageResolver(
            new GArray { 2 },
            new GArray { 3, 7, 11 }
        );

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

        var attackState = new BattleState();
        int[] expectedAttackRolls = { 3, 7, 11 };
        foreach (int expectedRoll in expectedAttackRolls)
        {
            AttackEffectResolutionResult injectedAttack = resolver.ResolveAttackEffects(
                source,
                target,
                new[] { effect },
                new AttackCheckInput(
                    requiredRoll: 21,
                    naturalOneAutoMiss: false,
                    naturalTwentyAutoHit: false
                ),
                new AttackContext { BattleState = attackState }
            );
            _test.Eq(
                injectedAttack.HitRoll,
                expectedRoll,
                "FixedRollDamageResolver 应按顺序消费注入 attack roll。"
            );
            _test.False(
                injectedAttack.AttackSuccess,
                "required roll 21 且关闭 natural-20 auto-hit 时，注入骰应保持普通 miss。"
            );
        }
        _test.Eq(
            (int)attackState.attack_roll_nonce,
            expectedAttackRolls.Length,
            "每次固定攻击骰仍应推进正式 attack-roll nonce。"
        );

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
