using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_ai_vs_ai_simulation_regression : SceneTree
{
    private const string AiVsAiScenarioPath =
        "res://data/configs/battle_sim/scenarios/ai_vs_ai_duel_example.tres";
    private const string BaselineProfilePath =
        "res://data/configs/battle_sim/profiles/baseline.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(exitCode);
    }

    private int Run()
    {
        BattleSimScenarioDef scenario = ResourceLoader.Load<BattleSimScenarioDef>(AiVsAiScenarioPath);
        BattleSimProfileDef baselineProfile = ResourceLoader.Load<BattleSimProfileDef>(
            BaselineProfilePath
        );
        _test.True(
            scenario != null && scenario.BuildStartContext() != null,
            "AI vs AI 示例场景资源应能被 BattleSimScenarioDef 正常加载。"
        );
        _test.True(
            baselineProfile != null,
            "AI vs AI regression 应能加载 baseline profile。"
        );
        if (scenario == null || baselineProfile == null)
            return _test.Finish("Battle AI vs AI simulation regression");

        var runner = new BattleSimRunner();
        BattleSimScenarioReport report = runner.RunScenario(
            scenario,
            new List<BattleSimProfileDef> { baselineProfile }
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
                _test.True(
                    run != null,
                    "AI vs AI 示例场次结果应保留 battle_ended 标记。"
                );
                IReadOnlyList<BattleAiTurnTraceProjection> aiTurnTraces =
                    run?.AiTurnTraces ?? System.Array.Empty<BattleAiTurnTraceProjection>();
                _test.True(
                    aiTurnTraces.Count > 0,
                    "AI vs AI 示例场次应至少收集到 1 条 AI turn trace。"
                );
                foreach (BattleAiTurnTraceProjection trace in aiTurnTraces)
                {
                    string factionId = trace?.FactionId ?? "";
                    if (factionId == "player")
                        sawPlayerAiTrace = true;
                    else if (factionId == "hostile")
                        sawHostileAiTrace = true;
                }

                GArray finalUnits = run?.FinalUnits ?? new GArray();
                foreach (Variant unitEntryValue in finalUnits)
                {
                    GDictionary unitEntry = GetSelfDict(unitEntryValue);
                    _test.Eq(
                        GetString(unitEntry, "control_mode"),
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
            }
            if (summary.FactionMetricTotals.ContainsKey("hostile"))
            {
                _test.True(
                    summary.FactionMetricTotals["hostile"].TurnCount > 0,
                    "hostile AI 在 AI vs AI 示例中应至少行动 1 次。"
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

        _test.True(sawPlayerAiTrace, "AI vs AI 示例应记录到 player 阵营的 AI turn trace。");
        _test.True(sawHostileAiTrace, "AI vs AI 示例应记录到 hostile 阵营的 AI turn trace。");
        return _test.Finish("Battle AI vs AI simulation regression");
    }

    private static GDictionary GetSelfDict(Variant value)
    {
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
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
