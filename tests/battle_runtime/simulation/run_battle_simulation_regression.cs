using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_simulation_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(exitCode);
    }

    private int Run()
    {
        try
        {
            TestReadyQueueDoesNotConsumeTimelineTicks();
            TestSimulationReportIncludesTraceAndPatchedSkillFallback();
        }
        catch (System.Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        return _test.Finish("Battle simulation regression");
    }

    private void TestReadyQueueDoesNotConsumeTimelineTicks()
    {
        var runner = new BattleSimRunner();
        BattleSimScenarioReport report = runner.RunScenario(
            BuildReadyQueueScenario(),
            new List<BattleSimProfileDef> { BuildBaselineProfile() }
        );
        _test.Eq(report.ProfileEntries.Count, 1, "ready queue regression 应产出单 profile entry。");
        if (report.ProfileEntries.Count == 0)
            return;

        BattleSimProfileReportEntry profileEntry = report.ProfileEntries[0];
        _test.Eq(profileEntry.Runs.Count, 1, "ready queue regression 应只跑 1 个 seed。");
        if (profileEntry.Runs.Count == 0)
            return;

        IReadOnlyList<BattleAiTurnTraceProjection> aiTurnTraces = profileEntry.Runs[0].AiTurnTraces;
        var firstReadyTurns = new List<int>();
        foreach (BattleAiTurnTraceProjection trace in aiTurnTraces)
        {
            string unitId = trace?.UnitId ?? "";
            if (unitId == "aa_hostile_one" || unitId == "ab_hostile_two")
            {
                firstReadyTurns.Add(trace?.TurnStartedTu ?? -1);
                if (firstReadyTurns.Count >= 2)
                    break;
            }
        }
        _test.Eq(firstReadyTurns.Count, 2, "ready queue regression 应记录到两个同批 ready AI 回合。");
        if (firstReadyTurns.Count < 2)
            return;
        _test.True(
            firstReadyTurns[0] == 5 && firstReadyTurns[1] == 5,
            $"同一批 ready 单位应在同一 TU 被依次激活，不应被模拟循环额外推进到 [{string.Join(", ", firstReadyTurns)}]。"
        );
    }

    private void TestSimulationReportIncludesTraceAndPatchedSkillFallback()
    {
        var runner = new BattleSimRunner();
        BattleSimScenarioReport report = runner.RunScenario(
            BuildScenario(),
            new List<BattleSimProfileDef>
            {
                BuildBaselineProfile(),
                BuildSuppressiveFireBlockedProfile(),
            }
        );

        _test.Eq(report.ProfileEntries.Count, 2, "simulation report 应包含 baseline 与 patch 两组 profile 结果。");
        _test.Eq(report.Comparisons.Count, 1, "两组 profile 应产出 1 组 comparison。");
        _test.True(
            !string.IsNullOrEmpty(report.OutputFiles.ReportJson),
            "simulation runner 应写出主 report json。"
        );
        _test.True(
            !string.IsNullOrEmpty(report.OutputFiles.TurnTraceJsonl),
            "simulation runner 应写出 AI trace jsonl。"
        );
        if (report.ProfileEntries.Count < 2)
            return;

        BattleSimProfileReportEntry baselineEntry = report.ProfileEntries[0];
        BattleSimProfileReportEntry patchedEntry = report.ProfileEntries[1];
        _test.True(
            baselineEntry.Runs.Count == 2 && patchedEntry.Runs.Count == 2,
            "每个 profile 应按 scenario seeds 跑满 2 场战斗。"
        );
        if (baselineEntry.Runs.Count > 0)
        {
            IReadOnlyList<BattleAiTurnTraceProjection> aiTurnTraces = baselineEntry.Runs[0].AiTurnTraces;
            _test.True(aiTurnTraces.Count > 0, "simulation run 应收集到至少 1 条 AI turn trace。");
            if (aiTurnTraces.Count > 0)
            {
                BattleAiTurnTraceProjection firstTrace = aiTurnTraces[0];
                _test.True(firstTrace.ActionTraces.Count > 0, "AI turn trace 应包含候选动作 trace 列表。");
                _test.True(firstTrace.ScoreInput != null, "AI turn trace 应包含最终选择的评分摘要。");
                _test.True(firstTrace.DecisionTargetSnapshots.Count > 0, "AI turn trace 应包含决策目标执行前快照。");
                _test.True(firstTrace.ExecutionResult != null, "AI turn trace 应包含命令执行结果摘要。");
                _test.True(
                    firstTrace.ExecutionResult != null
                        && firstTrace.ExecutionResult.UnitResults.Count > 0,
                    "AI turn trace 执行结果应包含单位前后资源变化。"
                );
            }
        }

        _test.True(
            GetInt(baselineEntry.Summary.SkillUsageTotals, "archer_suppressive_fire") > 0,
            "baseline profile 面对成线目标时应允许 AI 使用 archer_suppressive_fire。"
        );
        _test.Eq(
            GetInt(patchedEntry.Summary.SkillUsageTotals, "archer_suppressive_fire"),
            0,
            "高 stamina patch 后，AI 不应再使用 archer_suppressive_fire。"
        );
        _test.True(
            GetInt(patchedEntry.Summary.SkillUsageTotals, "archer_pinning_shot") > 0,
            "压制射击被资源阻断后，AI 应回落到 archer_pinning_shot。"
        );
    }

    private static BattleSimScenarioDef BuildReadyQueueScenario()
    {
        var scenario = new BattleSimScenarioDef
        {
            scenario_id = "simulation_ready_queue_regression",
            display_name = "Simulation Ready Queue Regression",
            map_size = new Vector2I(5, 3),
            timeline_ticks_per_step = 1,
            tu_per_tick = 5,
            max_iterations = 4,
            manual_policy = "wait",
            trace_enabled = true,
            seeds = new[] { 707 },
            ally_units = new GArray { BuildReadyQueueUnit("zz_player_dummy", "玩家木桩", "player", "manual", new Vector2I(4, 1)) },
            enemy_units = new GArray
            {
                BuildReadyQueueUnit("aa_hostile_one", "敌方一号", "hostile", "ai", new Vector2I(0, 1)),
                BuildReadyQueueUnit("ab_hostile_two", "敌方二号", "hostile", "ai", new Vector2I(1, 1)),
            },
        };
        return scenario;
    }

    private static BattleSimUnitSpec BuildReadyQueueUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        StringName controlMode,
        Vector2I coord
    )
    {
        return new BattleSimUnitSpec
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = controlMode,
            coord = coord,
            action_threshold = 5,
            current_hp = 30,
            current_ap = 1,
            base_attributes = BuildBaseAttributes(10, 10, 10, 10, 10, 10),
            attribute_overrides = new GDictionary
            {
                ["hp_max"] = 30,
                ["action_points"] = 1,
                ["action_threshold"] = 5,
                ["armor_ac_bonus"] = 4,
            },
        };
    }

    private static BattleSimScenarioDef BuildScenario()
    {
        return new BattleSimScenarioDef
        {
            scenario_id = "simulation_regression_archer",
            display_name = "Simulation Regression Archer",
            map_size = new Vector2I(7, 5),
            timeline_ticks_per_step = 1,
            tu_per_tick = 5,
            max_iterations = 40,
            manual_policy = "wait",
            seeds = new[] { 101, 102 },
            ally_units = new GArray
            {
                BuildManualUnit("player_a", "玩家A", new Vector2I(4, 2)),
                BuildManualUnit("player_b", "玩家B", new Vector2I(5, 2)),
            },
            enemy_units = new GArray
            {
                BuildEnemyArcher("mist_harrier_sim", "雾沼猎压者", new Vector2I(1, 2)),
            },
        };
    }

    private static BattleSimUnitSpec BuildManualUnit(StringName unitId, string displayName, Vector2I coord)
    {
        return new BattleSimUnitSpec
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = "player",
            control_mode = "manual",
            coord = coord,
            current_hp = 80,
            current_ap = 2,
            base_attributes = BuildBaseAttributes(10, 10, 12, 10, 10, 10),
            attribute_overrides = new GDictionary
            {
                ["hp_max"] = 80,
                ["action_points"] = 2,
                ["armor_ac_bonus"] = 8,
            },
        };
    }

    private static BattleSimUnitSpec BuildEnemyArcher(StringName unitId, string displayName, Vector2I coord)
    {
        return new BattleSimUnitSpec
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = "hostile",
            control_mode = "ai",
            ai_brain_id = "ranged_suppressor",
            ai_state_id = "pressure",
            coord = coord,
            current_hp = 60,
            current_mp = 20,
            current_stamina = 20,
            current_ap = 2,
            skill_ids = new GArray { "archer_suppressive_fire", "archer_pinning_shot" },
            skill_level_map = new GDictionary
            {
                ["archer_suppressive_fire"] = 1,
                ["archer_pinning_shot"] = 1,
            },
            base_attributes = BuildBaseAttributes(10, 12, 12, 14, 10, 10),
            attribute_overrides = new GDictionary
            {
                ["hp_max"] = 60,
                ["mp_max"] = 20,
                ["stamina_max"] = 20,
                ["action_points"] = 2,
                ["attack_bonus"] = 6,
                ["armor_ac_bonus"] = 5,
            },
        };
    }

    private static GDictionary BuildBaseAttributes(
        int strength,
        int agility,
        int constitution,
        int perception,
        int intelligence,
        int willpower
    )
    {
        return new GDictionary
        {
            ["strength"] = strength,
            ["agility"] = agility,
            ["constitution"] = constitution,
            ["perception"] = perception,
            ["intelligence"] = intelligence,
            ["willpower"] = willpower,
        };
    }

    private static BattleSimProfileDef BuildBaselineProfile()
    {
        return new BattleSimProfileDef
        {
            profile_id = "baseline",
            display_name = "Baseline",
        };
    }

    private static BattleSimProfileDef BuildSuppressiveFireBlockedProfile()
    {
        return new BattleSimProfileDef
        {
            profile_id = "pinning_only",
            display_name = "Pinning Only",
            override_patches = new GArray
            {
                new GDictionary
                {
                    ["target_type"] = "skill",
                    ["target_id"] = "archer_suppressive_fire",
                    ["path"] = "combat_profile.stamina_cost",
                    ["value"] = 999,
                },
            },
        };
    }

    private static GArray GetArray(GDictionary source, string key)
    {
        return source != null
            && source.ContainsKey(key)
            && source[key].VariantType == Variant.Type.Array
            ? source[key].AsGodotArray()
            : new GArray();
    }

    private static GDictionary GetDict(
        object rawValue,
        string key,
        bool treatValueAsSelf = false
    )
    {
        if (treatValueAsSelf)
        {
            if (rawValue is GDictionary directDictionary)
                return directDictionary;
            if (rawValue is Variant directVariant && directVariant.VariantType == Variant.Type.Dictionary)
                return directVariant.AsGodotDictionary();
            return new GDictionary();
        }
        if (rawValue is GDictionary source)
            return GetDict(source, key);
        return new GDictionary();
    }

    private static GDictionary GetDict(GDictionary source, string key)
    {
        return source != null
            && source.ContainsKey(key)
            && source[key].VariantType == Variant.Type.Dictionary
            ? source[key].AsGodotDictionary()
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

    private static int GetInt(GDictionary source, string key, int fallback = 0)
    {
        return source != null && source.ContainsKey(key) ? source[key].AsInt32() : fallback;
    }

    private static int GetInt(IReadOnlyDictionary<string, int> source, string key, int fallback = 0)
    {
        return source != null && source.TryGetValue(key, out int value) ? value : fallback;
    }

}
