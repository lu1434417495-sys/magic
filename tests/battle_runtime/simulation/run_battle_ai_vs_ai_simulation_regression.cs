using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_ai_vs_ai_simulation_regression : LifecycleTestSceneTree
{
    private const string AiVsAiScenarioPath =
        "res://data/configs/battle_sim/scenarios/ai_vs_ai_duel_example.tres";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(RunDeferred);
    }

    private void RunDeferred()
    {
        TestResult exitCode = Run();
        RequestTestExit(exitCode);
    }

    private TestResult Run()
    {
        using var loader = new TestContentResourceLoader();
        BattleSimScenarioDef scenarioResource = loader.LoadCanonical<BattleSimScenarioDef>(
            AiVsAiScenarioPath
        );
        AssertAuthoredScenarioFacts(scenarioResource);
        BattleSimScenarioDefinition scenario = scenarioResource.ToDefinition();
        BattleSimProfileDefinition baselineProfile = GameSessionTestFactory
            .GetProcessSnapshot()
            .BattleSimProfiles["baseline"];
        bool scenarioContextBuilt = false;
        if (scenario != null)
        {
            using GodotProjectionLease<GDictionary> contextLease =
                scenario.BuildStartContextLease();
            scenarioContextBuilt = contextLease.Value != null;
        }
        _test.True(
            scenarioContextBuilt,
            "AI vs AI 示例场景资源应能被 BattleSimScenarioDef 正常加载。"
        );
        _test.True(
            baselineProfile != null,
            "AI vs AI regression 应能加载 baseline profile。"
        );
        if (scenario == null || baselineProfile == null)
            return _test.Finish("Battle AI vs AI simulation regression");

        var runner = new BattleSimRunner(
            new BattleSimContentProvider(GameSessionTestFactory.GetProcessSnapshot())
        );
        BattleSimScenarioReport report = runner.RunScenario(
            scenario,
            new List<BattleSimProfileDefinition> { baselineProfile }
        );
        _test.True(
            report.IsComplete,
            "AI vs AI 示例的全部场次都应以正式 battle-ended sample 完成。"
        );
        _test.Eq(report.RunCount, 2, "AI vs AI 示例应保留场景声明的 2 个 run。");
        _test.Eq(
            report.CompletedRunCount,
            2,
            "AI vs AI 示例的两个 run 都应包含正式 final decision。"
        );
        _test.Eq(
            report.UnfinishedRunCount,
            0,
            "AI vs AI 示例不应把 idle stall、迭代预算耗尽或 invalid runtime 当作通过。"
        );
        _test.Eq(report.ProfileEntries.Count, 1, "单 profile 的 AI vs AI 示例应只产出 1 个 profile entry。");
        _test.Eq(report.Comparisons.Count, 0, "单 profile 的 AI vs AI 示例不应生成 comparison。");
        _test.True(
            !string.IsNullOrEmpty(report.OutputFiles.ReportJson),
            "AI vs AI simulation 应写出主 report json。"
        );
        _test.True(
            !string.IsNullOrEmpty(report.OutputFiles.TurnTraceJsonl),
            "AI vs AI simulation 应写出 AI trace jsonl。"
        );

        bool sawPlayerAiTrace = false;
        bool sawHostileAiTrace = false;
        var completedSeeds = new HashSet<long>();
        if (report.ProfileEntries.Count > 0)
        {
            BattleSimProfileReportEntry baselineEntry = report.ProfileEntries[0];
            _test.Eq(
                baselineEntry.Runs.Count,
                2,
                "AI vs AI 示例应按场景中的 2 个 seeds 跑满 2 场战斗。"
            );
            foreach (BattleSimRunReport run in baselineEntry.Runs)
            {
                if (run != null)
                    completedSeeds.Add(run.Seed);
                _test.Eq(
                    run?.TerminationKind ?? BattleSimTerminationKind.InvalidRuntime,
                    BattleSimTerminationKind.BattleEnded,
                    "AI vs AI 示例的每个 run 都应以正式 BattleEnded 终止。"
                );
                _test.True(
                    run != null && run.EndReason != BattleEndReasonKind.None,
                    "AI vs AI 示例的每个 run 都应保留正式 battle end reason。"
                );
                IReadOnlyList<BattleAiTurnTraceProjection> aiTurnTraces =
                    run?.AiTurnTraces ?? System.Array.Empty<BattleAiTurnTraceProjection>();
                _test.True(
                    aiTurnTraces.Count > 0,
                    "AI vs AI 示例场次应至少收集到 1 条 AI turn trace。"
                );
                bool runSawPlayerAiTrace = false;
                bool runSawHostileAiTrace = false;
                foreach (BattleAiTurnTraceProjection trace in aiTurnTraces)
                {
                    string factionId = trace?.FactionId ?? "";
                    if (factionId == "player")
                    {
                        sawPlayerAiTrace = true;
                        runSawPlayerAiTrace = true;
                    }
                    else if (factionId == "hostile")
                    {
                        sawHostileAiTrace = true;
                        runSawHostileAiTrace = true;
                    }
                    _test.True(
                        trace != null
                            && !string.IsNullOrEmpty(trace.ActionId)
                            && trace.ExecutionResult != null
                            && !string.IsNullOrEmpty(trace.ExecutionResult.CommandType),
                        "AI vs AI 示例的 turn trace 应来自已执行的正式 AI action。"
                    );
                }
                _test.True(
                    runSawPlayerAiTrace,
                    $"AI vs AI 示例 seed {run?.Seed ?? 0} 应记录 player 的真实 AI turn。"
                );
                _test.True(
                    runSawHostileAiTrace,
                    $"AI vs AI 示例 seed {run?.Seed ?? 0} 应记录 hostile 的真实 AI turn。"
                );

                IReadOnlyList<Dictionary<string, object>> finalUnits =
                    run?.FinalUnits ?? System.Array.Empty<Dictionary<string, object>>();
                foreach (Dictionary<string, object> unitEntry in finalUnits)
                {
                    _test.Eq(
                        unitEntry.TryGetValue("control_mode", out object controlMode)
                            ? controlMode as string ?? ""
                            : "",
                        "ai",
                        "AI vs AI 示例中的最终单位快照不应出现 manual control_mode。"
                    );
                }
            }

            BattleSimProfileSummary summary = baselineEntry.Summary;
            _test.True(
                summary != null,
                "AI vs AI 示例应保留 wins_by_faction 汇总字段。"
            );
            _test.True(
                summary.FactionMetricTotals.ContainsKey("player"),
                "AI vs AI summary 应包含 player 阵营 metrics。"
            );
            _test.True(
                summary.FactionMetricTotals.ContainsKey("hostile"),
                "AI vs AI summary 应包含 hostile 阵营 metrics。"
            );
            if (summary.FactionMetricTotals.ContainsKey("player"))
            {
                _test.True(
                    summary.FactionMetricTotals["player"].TurnCount > 0,
                    "player AI 在 AI vs AI 示例中应至少行动 1 次。"
                );
                _test.True(
                    summary.FactionMetricTotals["player"].TotalDamageDone > 0,
                    "player AI 在 AI vs AI 示例中应通过正式技能执行造成伤害。"
                );
            }
            if (summary.FactionMetricTotals.ContainsKey("hostile"))
            {
                _test.True(
                    summary.FactionMetricTotals["hostile"].TurnCount > 0,
                    "hostile AI 在 AI vs AI 示例中应至少行动 1 次。"
                );
                _test.True(
                    summary.FactionMetricTotals["hostile"].TotalDamageDone > 0,
                    "hostile AI 在 AI vs AI 示例中应通过正式技能执行造成伤害。"
                );
            }
            _test.True(
                summary.ActionChoiceCounts.Count > 0,
                "AI vs AI summary 应包含 action_choice_counts。"
            );
            _test.True(
                summary.WinsByFaction.Count == 0
                    || summary.WinsByFaction.ContainsKey("player")
                    || summary.WinsByFaction.ContainsKey("hostile"),
                "wins_by_faction 若非空，应只包含正式 faction key。"
            );
        }

        _test.True(
            completedSeeds.SetEquals(new long[] { 301, 302 }),
            "AI vs AI 示例必须直接跑完 authored seeds 301/302。"
        );
        _test.True(sawPlayerAiTrace, "AI vs AI 示例应记录到 player 阵营的 AI turn trace。");
        _test.True(sawHostileAiTrace, "AI vs AI 示例应记录到 hostile 阵营的 AI turn trace。");
        return _test.Finish("Battle AI vs AI simulation regression");
    }

    private void AssertAuthoredScenarioFacts(BattleSimScenarioDef scenarioResource)
    {
        _test.True(scenarioResource != null, "AI vs AI authored resource 应通过正式测试 loader 加载。");
        if (scenarioResource == null)
            return;

        _test.Eq(
            scenarioResource.scenario_id.ToString(),
            "ai_vs_ai_duel_example",
            "AI vs AI regression 应直接消费正式 authored scenario。"
        );
        _test.Eq(
            scenarioResource.map_size,
            new Vector2I(6, 3),
            "AI vs AI authored scenario 应保留 6x3 duel map。"
        );
        _test.Eq(
            scenarioResource.max_iterations,
            400,
            "AI vs AI authored scenario 应依靠可执行配置完成，而不是放大迭代预算。"
        );
        _test.Eq(scenarioResource.seeds.Length, 2, "AI vs AI authored scenario 应声明两个 seeds。");
        if (scenarioResource.seeds.Length == 2)
        {
            _test.Eq(scenarioResource.seeds[0], 301, "AI vs AI authored seed[0] 应为 301。");
            _test.Eq(scenarioResource.seeds[1], 302, "AI vs AI authored seed[1] 应为 302。");
        }

        BattleSimUnitSpec playerUnit = ReadUnitSpec(scenarioResource.ally_units, 0);
        BattleSimUnitSpec hostileUnit = ReadUnitSpec(scenarioResource.enemy_units, 0);
        _test.Eq(
            scenarioResource.ally_units.Count,
            1,
            "AI vs AI authored scenario 应包含一个 player 单位。"
        );
        _test.Eq(
            scenarioResource.enemy_units.Count,
            1,
            "AI vs AI authored scenario 应包含一个 hostile 单位。"
        );
        _test.True(
            playerUnit != null && hostileUnit != null,
            "AI vs AI 示例应包含双方各一个 typed unit spec。"
        );
        if (playerUnit == null || hostileUnit == null)
            return;

        AssertAuthoredAiUnit(
            playerUnit,
            "player_vanguard_ai",
            "player",
            "melee_aggressor",
            "engage",
            "sword"
        );
        AssertAuthoredAiUnit(
            hostileUnit,
            "enemy_harrier_ai",
            "hostile",
            "ranged_suppressor",
            "pressure",
            "bow"
        );
        _test.True(
            ContainsStringName(playerUnit.skill_ids, "basic_attack")
                && ContainsStringName(playerUnit.skill_ids, "warrior_heavy_strike"),
            "player vanguard authored fixture 应具备实际可执行的近战攻击技能。"
        );
        _test.True(
            ContainsStringName(hostileUnit.skill_ids, "basic_attack")
                && ContainsStringName(hostileUnit.skill_ids, "archer_pinning_shot"),
            "hostile harrier authored fixture 应具备实际可执行的弓箭攻击技能。"
        );
    }

    private static BattleSimUnitSpec ReadUnitSpec(GArray source, int index)
    {
        if (
            source == null
            || index < 0
            || index >= source.Count
            || source[index].VariantType != Variant.Type.Object
        )
        {
            return null;
        }
        return source[index].AsGodotObject() as BattleSimUnitSpec;
    }

    private void AssertAuthoredAiUnit(
        BattleSimUnitSpec unit,
        string expectedUnitId,
        string expectedFactionId,
        string expectedBrainId,
        string expectedStateId,
        string expectedWeaponFamily
    )
    {
        _test.Eq(unit.unit_id.ToString(), expectedUnitId, "AI vs AI authored unit id 应保持稳定。");
        _test.Eq(
            unit.faction_id.ToString(),
            expectedFactionId,
            $"AI vs AI authored unit {expectedUnitId} 应属于预期阵营。"
        );
        _test.Eq(
            unit.control_mode.ToString(),
            "ai",
            $"AI vs AI authored unit {expectedUnitId} 应直接声明 AI control mode。"
        );
        _test.Eq(
            unit.ai_brain_id.ToString(),
            expectedBrainId,
            $"AI vs AI authored unit {expectedUnitId} 应绑定预期 brain。"
        );
        _test.Eq(
            unit.ai_state_id.ToString(),
            expectedStateId,
            $"AI vs AI authored unit {expectedUnitId} 应绑定预期初始 state。"
        );
        _test.Eq(
            GetString(unit.weapon_projection, "weapon_family"),
            expectedWeaponFamily,
            $"AI vs AI authored unit {expectedUnitId} 应携带匹配技能的正式武器投影。"
        );
        _test.True(
            unit.current_stamina >= 120,
            $"AI vs AI authored unit {expectedUnitId} 应有足够资源执行其正常技能组合。"
        );
    }

    private static bool ContainsStringName(GArray source, string expected)
    {
        if (source == null)
            return false;
        foreach (Variant value in source)
        {
            string actual = value.VariantType switch
            {
                Variant.Type.String => value.AsString(),
                Variant.Type.StringName => value.AsStringName().ToString(),
                _ => "",
            };
            if (actual == expected)
                return true;
        }
        return false;
    }

    private static string GetString(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return "";
        Variant value = source[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => value.ToString(),
        };
    }

}
