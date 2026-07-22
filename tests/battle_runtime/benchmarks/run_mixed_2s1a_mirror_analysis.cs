using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_mixed_2s1a_mirror_analysis : LifecycleTestSceneTree
{
    private const int MaxIdleLoops = 25;
    private const string ScenarioPath =
        "res://data/configs/battle_sim/scenarios/mixed_2sword_1arch_mirror_simulation.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        ProcessFrame += RunOnFirstProcessFrame;
    }

    private void RunOnFirstProcessFrame()
    {
        ProcessFrame -= RunOnFirstProcessFrame;
        RunDeferred();
    }

    private void RunDeferred()
    {
        int exitCode = Run();
        RequestTestExit(_test.Finish("Mixed 2s1a mirror analysis", exitCode));
    }

    private int Run()
    {
        long startSeed = OS.HasEnvironment("START_SEED")
            ? ReadLongEnvironment("START_SEED", 1)
            : TrueRandomSeedService.GenerateSeed();
        string startSeedSource = OS.HasEnvironment("START_SEED") ? "environment" : "true_random";
        int runCount = ReadIntEnvironment("COUNT", 10);
        string outputPath = ReadStringEnvironment("OUTPUT_FILE", "");
        bool progressEnabled = ReadBoolEnvironment("PROGRESS", string.IsNullOrEmpty(outputPath));

        BattleSimScenarioDef scenarioResource =
            ResourceLoader.Load<BattleSimScenarioDef>(ScenarioPath);
        if (scenarioResource == null)
        {
            ConsoleProcessOutput.WriteFailure("Failed to load scenario");
            return 1;
        }
        BattleSimScenarioDefinition scenarioDefinition = scenarioResource.ToDefinition();
        scenarioResource = null;

        ContentSnapshot contentSnapshot = GameSessionTestFactory.GetProcessSnapshot();
        var contentProvider = new BattleSimContentProvider(contentSnapshot);
        var overrideApplier = new BattleSimOverrideApplier();
        var terrainGenerator = new BattleTerrainGenerator();

        try
        {
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
                contentProvider.GetSkillDefinitionsTyped();
            IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates =
                contentProvider.GetEnemyTemplatesTyped();
            IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains =
                contentProvider.GetEnemyAiBrainsTyped();

            BattleSimOverrideApplyResult overrides = overrideApplier.ApplyProfileTyped(
                skillDefinitions,
                enemyAiBrains,
                contentProvider.GetBattleSimProfilesTyped()["baseline"]
            );
            BattleSimFormalRosterOptionsData rosterOptions = BuildRosterOptionsFromEnvironment();
            var rng = new RuntimeRandom(Math.Max(startSeed, 1L));

            int totalChargeAttempts = 0;
            int totalChargeSuccesses = 0;
            int totalHeavyAttempts = 0;
            int totalHeavySuccesses = 0;
            int totalAimedAttempts = 0;
            int totalAimedSuccesses = 0;
            int totalMultishotAttempts = 0;
            int totalMultishotSuccesses = 0;
            int totalBasicAttempts = 0;
            int totalBasicSuccesses = 0;
            int totalChargeMastery = 0;
            int totalHeavyMastery = 0;
            int totalAimedMastery = 0;
            int totalMultishotMastery = 0;
            int totalBasicMastery = 0;
            int endedCount = 0;
            int idleStallCount = 0;
            int iterationBudgetExhaustedCount = 0;
            int invalidRuntimeCount = 0;
            int totalIterations = 0;
            int totalTimelineSteps = 0;
            int totalWinsPlayer = 0;
            int totalWinsHostile = 0;
            int totalDraws = 0;

            ulong startTime = Time.GetTicksMsec();

            for (int runIndex = 0; runIndex < runCount; runIndex++)
            {
                long seed = rng.Randi();
                var fixture = BuildFormalFixture(
                    scenarioDefinition,
                    overrides,
                    contentSnapshot,
                    rosterOptions
                );
                try
                {
                    GDictionary result = RunSingleSimulation(
                        scenarioDefinition,
                        overrides,
                        enemyTemplates,
                        terrainGenerator,
                        fixture,
                        seed
                    );
                    if (ReadExactBool(result, "battle_ended"))
                    {
                        GDictionary metrics = GetDict(result, "metrics");
                        GDictionary factions = GetDict(metrics, "factions");

                        foreach (Variant factionKey in factions.Keys)
                        {
                            GDictionary factionData = GetDict(factions, factionKey);
                            GDictionary skillAttempts = GetDict(
                                factionData,
                                "skill_attempt_counts"
                            );
                            GDictionary skillSuccesses = GetDict(
                                factionData,
                                "skill_success_counts"
                            );
                            totalChargeAttempts += GetInt(skillAttempts, "charge");
                            totalChargeSuccesses += GetInt(skillSuccesses, "charge");
                            totalHeavyAttempts += GetInt(
                                skillAttempts,
                                "warrior_heavy_strike"
                            );
                            totalHeavySuccesses += GetInt(
                                skillSuccesses,
                                "warrior_heavy_strike"
                            );
                            totalAimedAttempts += GetInt(skillAttempts, "archer_aimed_shot");
                            totalAimedSuccesses += GetInt(
                                skillSuccesses,
                                "archer_aimed_shot"
                            );
                            totalMultishotAttempts += GetInt(
                                skillAttempts,
                                "archer_multishot"
                            );
                            totalMultishotSuccesses += GetInt(
                                skillSuccesses,
                                "archer_multishot"
                            );
                            totalBasicAttempts += GetInt(skillAttempts, "basic_attack");
                            totalBasicSuccesses += GetInt(skillSuccesses, "basic_attack");
                        }

                        totalChargeMastery += fixture.charge_mastery;
                        totalHeavyMastery += fixture.heavy_mastery;
                        totalAimedMastery += fixture.aimed_mastery;
                        totalMultishotMastery += fixture.multishot_mastery;
                        totalBasicMastery += fixture.basic_mastery;
                        endedCount++;
                        string winner = GetString(result, "winner_faction_id");
                        if (winner == "player")
                            totalWinsPlayer++;
                        else if (winner == "hostile")
                            totalWinsHostile++;
                        else
                            totalDraws++;

                        totalIterations += GetInt(result, "iterations");
                        totalTimelineSteps += GetInt(result, "timeline_steps");
                    }
                    else if (ReadExactBool(result, "idle_stall"))
                        idleStallCount++;
                    else if (ReadExactBool(result, "iteration_budget_exhausted"))
                        iterationBudgetExhaustedCount++;
                    else
                        invalidRuntimeCount++;

                    if (progressEnabled && string.IsNullOrEmpty(outputPath))
                    {
                        double elapsed = (Time.GetTicksMsec() - startTime) / 1000.0;
                        ConsoleProcessOutput.WriteStandard(
                            $"[Progress] {runIndex + 1}/{runCount} ({elapsed:F1}s, {(runIndex + 1) / Math.Max(elapsed, 0.001):F2} runs/s)"
                        );
                    }
                }
                finally
                {
                    fixture.Dispose();
                }
            }

            double elapsedTotal = (Time.GetTicksMsec() - startTime) / 1000.0;
            double n = Math.Max(endedCount, 1);
            var report = new GDictionary
            {
                ["batch_id"] = startSeed,
                ["start_seed"] = startSeed,
                ["start_seed_source"] = startSeedSource,
                ["run_count"] = runCount,
                ["elapsed_seconds"] = elapsedTotal,
                ["ended_count"] = endedCount,
                ["unfinished_count"] = idleStallCount
                    + iterationBudgetExhaustedCount
                    + invalidRuntimeCount,
                ["idle_stall_count"] = idleStallCount,
                ["iteration_budget_exhausted_count"] = iterationBudgetExhaustedCount,
                ["invalid_runtime_count"] = invalidRuntimeCount,
                ["avg_iterations"] = totalIterations / n,
                ["avg_timeline_steps"] = totalTimelineSteps / n,
                ["win_rate"] = new GDictionary
                {
                    ["player"] = totalWinsPlayer,
                    ["hostile"] = totalWinsHostile,
                    ["draw"] = totalDraws,
                },
                ["charge"] = BuildSkillSummary(
                    totalChargeAttempts,
                    totalChargeSuccesses,
                    totalChargeMastery,
                    n
                ),
                ["warrior_heavy_strike"] = BuildSkillSummary(
                    totalHeavyAttempts,
                    totalHeavySuccesses,
                    totalHeavyMastery,
                    n
                ),
                ["archer_aimed_shot"] = BuildSkillSummary(
                    totalAimedAttempts,
                    totalAimedSuccesses,
                    totalAimedMastery,
                    n
                ),
                ["archer_multishot"] = BuildSkillSummary(
                    totalMultishotAttempts,
                    totalMultishotSuccesses,
                    totalMultishotMastery,
                    n
                ),
                ["basic_attack"] = BuildSkillSummary(
                    totalBasicAttempts,
                    totalBasicSuccesses,
                    totalBasicMastery,
                    n
                ),
            };

            if (string.IsNullOrEmpty(outputPath))
            {
                ConsoleProcessOutput.WriteStandard(Json.Stringify(report, "\t"));
            }
            else
            {
                WriteJsonFile(outputPath, report);
            }

            return endedCount == runCount ? 0 : 2;
        }
        finally
        {
            DisposeObjects(terrainGenerator, contentProvider);
        }
    }

    private static GDictionary BuildSkillSummary(
        int attempts,
        int successes,
        int mastery,
        double runCount
    )
    {
        return new GDictionary
        {
            ["attempts"] = attempts,
            ["successes"] = successes,
            ["mastery"] = mastery,
            ["avg_attempts_per_run"] = attempts / runCount,
            ["avg_mastery_per_run"] = mastery / runCount,
        };
    }

    private static BattleSimFormalCombatFixture BuildFormalFixture(
        BattleSimScenarioDefinition scenarioDefinition,
        BattleSimOverrideApplyResult overrides,
        ContentSnapshot contentSnapshot,
        BattleSimFormalRosterOptionsData rosterOptions
    )
    {
        var fixture = new BattleSimFormalCombatFixture();
        fixture.SetupContent(contentSnapshot, overrides.SkillDefinitions);
        if (
            !fixture.BuildRoster(scenarioDefinition.ScenarioId, rosterOptions)
        )
            ConsoleProcessOutput.WriteFailure($"Unsupported formal battle sim roster: {scenarioDefinition.ScenarioId}");
        return fixture;
    }

    private static GDictionary RunSingleSimulation(
        BattleSimScenarioDefinition scenarioDefinition,
        BattleSimOverrideApplyResult overrides,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates,
        BattleTerrainGenerator terrainGenerator,
        BattleSimFormalCombatFixture fixture,
        long seed
    )
    {
        var runtime = new BattleRuntimeModule();
        BattleState state = null;
        EncounterAnchorData encounterAnchor = null;
        try
        {
            bool useFormalTerrain = scenarioDefinition.UseFormalTerrainGeneration;
            runtime.setup(
                fixture,
                overrides.SkillDefinitions,
                enemyTemplates,
                overrides.EnemyAiBrains,
                null,
                default,
                fixture.GetItemDefsTyped(),
                useFormalTerrain ? null : terrainGenerator
            );
            runtime.SetAiTraceEnabled(false);
            runtime.SetAiScoreProfile(overrides.AiScoreProfile);

            encounterAnchor = new EncounterAnchorData
            {
                entity_id = scenarioDefinition.ScenarioId != ""
                    ? scenarioDefinition.ScenarioId
                    : "battle_sim",
                display_name = !string.IsNullOrEmpty(scenarioDefinition.DisplayName)
                    ? scenarioDefinition.DisplayName
                    : scenarioDefinition.ScenarioId.ToString(),
                faction_id = "hostile",
                world_coord = Vector2I.Zero,
                region_tag = "simulation",
            };

            using GodotProjectionLease<GDictionary> baseContextLease =
                scenarioDefinition.BuildStartContextLease();
            using GodotProjectionLease<GDictionary> contextLease =
                fixture.BuildRuntimeContextLease(runtime, baseContextLease.Value);
            GDictionary context = contextLease.Value;
            state = runtime.StartBattleBorrowingContext(
                encounterAnchor,
                seed,
                BattleEliminationObjectiveDefinition.Instance,
                context
            );
            fixture.ApplyStartedBattleMetadata(state);

            BattleSimExecutionLoopResult loopResult = new BattleSimExecutionLoop().Run(
                runtime,
                state,
                scenarioDefinition,
                MaxIdleLoops
            );
            return new GDictionary
            {
                ["battle_ended"] =
                    loopResult.termination_kind == BattleSimTerminationKind.BattleEnded,
                ["idle_stall"] =
                    loopResult.termination_kind == BattleSimTerminationKind.IdleStall,
                ["iteration_budget_exhausted"] =
                    loopResult.termination_kind
                    == BattleSimTerminationKind.IterationBudgetExhausted,
                ["winner_faction_id"] = state != null ? state.winner_faction_id.ToString() : "",
                ["iterations"] = loopResult.iterations,
                ["timeline_steps"] = loopResult.timeline_steps,
                ["metrics"] = BattleMetricsProjection.Project(runtime.GetBattleMetricsTyped()),
            };
        }
        finally
        {
            runtime.dispose();
            BattleTestFixture.DisposeBattleState(state);
        }
    }

    private static BattleSimFormalRosterOptionsData BuildRosterOptionsFromEnvironment()
    {
        StringName mainCharacterMemberId = "";
        StringName leaderMemberId = "";
        int mainCharacterRerollCount = 0;
        if (OS.HasEnvironment("MAIN_CHARACTER_MEMBER_ID"))
        {
            string memberId = OS.GetEnvironment("MAIN_CHARACTER_MEMBER_ID").StripEdges();
            if (!string.IsNullOrEmpty(memberId))
            {
                mainCharacterMemberId = new StringName(memberId);
            }
        }
        if (OS.HasEnvironment("LEADER_MEMBER_ID"))
        {
            string leaderId = OS.GetEnvironment("LEADER_MEMBER_ID").StripEdges();
            if (!string.IsNullOrEmpty(leaderId))
            {
                leaderMemberId = new StringName(leaderId);
            }
        }
        if (OS.HasEnvironment("MAIN_CHARACTER_REROLL_COUNT"))
        {
            mainCharacterRerollCount = ReadIntEnvironment("MAIN_CHARACTER_REROLL_COUNT", 0);
        }
        return new BattleSimFormalRosterOptionsData
        {
            MainCharacterMemberId = mainCharacterMemberId,
            LeaderMemberId = leaderMemberId,
            MainCharacterRerollCount = mainCharacterRerollCount,
        };
    }

    private static void WriteJsonFile(string outputPath, GDictionary report)
    {
        string absolutePath = outputPath.StartsWith("res://") || outputPath.StartsWith("user://")
            ? ProjectSettings.GlobalizePath(outputPath)
            : outputPath;
        string directory = absolutePath.GetBaseDir();
        if (!string.IsNullOrEmpty(directory))
            DirAccess.MakeDirRecursiveAbsolute(directory);
        using FileAccess file = FileAccess.Open(absolutePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            ConsoleProcessOutput.WriteFailure($"Failed to write: {absolutePath}");
            return;
        }
        file.StoreString(Json.Stringify(report, "\t"));
    }

    private static int ReadIntEnvironment(string key, int fallback)
    {
        return OS.HasEnvironment(key) && int.TryParse(OS.GetEnvironment(key), out int value)
            ? value
            : fallback;
    }

    private static long ReadLongEnvironment(string key, long fallback)
    {
        return OS.HasEnvironment(key) && long.TryParse(OS.GetEnvironment(key), out long value)
            ? value
            : fallback;
    }

    private static string ReadStringEnvironment(string key, string fallback)
    {
        return OS.HasEnvironment(key) ? OS.GetEnvironment(key) : fallback;
    }

    private static bool ReadBoolEnvironment(string key, bool fallback)
    {
        if (!OS.HasEnvironment(key))
            return fallback;
        return StringToBool(OS.GetEnvironment(key), fallback);
    }

    private static bool StringToBool(string value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        return value.StripEdges().ToLowerInvariant() switch
        {
            "1" => true,
            "0" => false,
            "true" => true,
            "false" => false,
            "yes" => true,
            "no" => false,
            _ => fallback,
        };
    }

    private static GDictionary GetDict(GDictionary source, object key)
    {
        return TryRead(source, key, out Variant value) && value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static int GetInt(GDictionary source, object key)
    {
        return TryRead(source, key, out Variant value) && value.VariantType == Variant.Type.Int
            ? value.AsInt32()
            : 0;
    }

    private static string GetString(GDictionary source, object key)
    {
        if (!TryRead(source, key, out Variant value))
            return "";
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }

    private static bool ReadExactBool(GDictionary source, object key)
    {
        return TryRead(source, key, out Variant value) && value.VariantType == Variant.Type.Bool
            && value.AsBool();
    }

    private static bool TryRead(GDictionary source, object key, out Variant value)
    {
        value = default;
        if (source == null || key == null)
            return false;
        Variant variantKey = key switch
        {
            Variant direct => direct,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            _ => default,
        };
        if (variantKey.VariantType == Variant.Type.Nil)
            return false;
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return value.VariantType != Variant.Type.Nil;
        }
        if (variantKey.VariantType == Variant.Type.String)
        {
            StringName stringNameKey = variantKey.AsString();
            if (source.ContainsKey(stringNameKey))
            {
                value = source[stringNameKey];
                return value.VariantType != Variant.Type.Nil;
            }
        }
        else if (variantKey.VariantType == Variant.Type.StringName)
        {
            string stringKey = variantKey.AsStringName().ToString();
            if (source.ContainsKey(stringKey))
            {
                value = source[stringKey];
                return value.VariantType != Variant.Type.Nil;
            }
        }
        return false;
    }

    private static void DisposeObjects(params object[] objects)
    {
        foreach (object obj in objects)
        {
            if (obj is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
