using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_longsword_3v3_mastery_analysis : SceneTree
{
    private const int MaxIdleLoops = 25;
    private const string ScenarioPath =
        "res://data/configs/battle_sim/scenarios/longsword_3v3_mirror_simulation.tres";

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        long startSeed = ReadLongEnvironment("START_SEED", 0);
        int runCount = ReadIntEnvironment("COUNT", 1000);
        string outputPath = ReadStringEnvironment("OUTPUT_FILE", "");
        bool progressEnabled = ReadBoolEnvironment("PROGRESS", string.IsNullOrEmpty(outputPath));

        BattleSimScenarioDef scenarioDef = ResourceLoader.Load<BattleSimScenarioDef>(ScenarioPath);
        if (scenarioDef == null)
        {
            GD.PushError("[ERROR] Failed to load scenario");
            return 1;
        }

        var contentProvider = new BattleSimContentProvider();
        var overrideApplier = new BattleSimOverrideApplier();
        var terrainGenerator = new BattleTerrainGenerator();

        try
        {
            BattleSimOverrideApplyResult overrides = overrideApplier.ApplyProfileTyped(
                contentProvider.GetSkillDefsTyped(),
                contentProvider.GetEnemyAiBrainsTyped(),
                new BattleSimProfileDef
                {
                    profile_id = "baseline",
                    display_name = "Baseline",
                }
            );
            var rng = new RandomNumberGenerator { Seed = (ulong)Math.Max(startSeed, 1) };

            int totalChargeAttempts = 0;
            int totalChargeSuccesses = 0;
            int totalHeavyAttempts = 0;
            int totalHeavySuccesses = 0;
            int totalChargeMastery = 0;
            int totalHeavyMastery = 0;
            int endedCount = 0;
            int totalIterations = 0;
            int totalTimelineSteps = 0;
            int heavyMasteryNonzeroCount = 0;

            ulong startTime = Time.GetTicksMsec();

            for (int runIndex = 0; runIndex < runCount; runIndex++)
            {
                long seed = rng.Randi();
                var gateway = new TestMasteryGateway();
                GDictionary result = RunSingleSimulation(
                    scenarioDef,
                    overrides,
                    contentProvider,
                    terrainGenerator,
                    gateway,
                    seed
                );
                GDictionary metrics = GetDict(result, "metrics");
                GDictionary factions = GetDict(metrics, "factions");

                foreach (Variant factionKey in factions.Keys)
                {
                    GDictionary factionData = GetDict(factions, factionKey);
                    GDictionary skillAttempts = GetDict(factionData, "skill_attempt_counts");
                    GDictionary skillSuccesses = GetDict(factionData, "skill_success_counts");
                    totalChargeAttempts += GetInt(skillAttempts, "charge");
                    totalChargeSuccesses += GetInt(skillSuccesses, "charge");
                    totalHeavyAttempts += GetInt(skillAttempts, "warrior_heavy_strike");
                    totalHeavySuccesses += GetInt(skillSuccesses, "warrior_heavy_strike");
                }

                totalChargeMastery += gateway.ChargeMastery;
                totalHeavyMastery += gateway.HeavyMastery;
                if (ReadExactBool(result, "battle_ended"))
                    endedCount++;
                totalIterations += GetInt(result, "iterations");
                totalTimelineSteps += GetInt(result, "timeline_steps");
                if (gateway.HeavyMastery > 0)
                    heavyMasteryNonzeroCount++;

                if (progressEnabled && string.IsNullOrEmpty(outputPath) && (runIndex + 1) % 10 == 0)
                {
                    double elapsed = (Time.GetTicksMsec() - startTime) / 1000.0;
                    GD.Print(
                        $"[Progress] {runIndex + 1}/{runCount} ({elapsed:F1}s, {(runIndex + 1) / Math.Max(elapsed, 0.001):F2} runs/s)"
                    );
                }
            }

            double elapsedTotal = (Time.GetTicksMsec() - startTime) / 1000.0;
            double n = Math.Max(runCount, 1);
            var report = new GDictionary
            {
                ["batch_id"] = startSeed,
                ["run_count"] = runCount,
                ["elapsed_seconds"] = elapsedTotal,
                ["ended_count"] = endedCount,
                ["avg_iterations"] = totalIterations / n,
                ["avg_timeline_steps"] = totalTimelineSteps / n,
                ["charge"] = new GDictionary
                {
                    ["attempts"] = totalChargeAttempts,
                    ["successes"] = totalChargeSuccesses,
                    ["mastery"] = totalChargeMastery,
                    ["avg_attempts_per_run"] = totalChargeAttempts / n,
                    ["avg_mastery_per_run"] = totalChargeMastery / n,
                    ["avg_mastery_per_person"] = totalChargeMastery / n / 6.0,
                },
                ["warrior_heavy_strike"] = new GDictionary
                {
                    ["attempts"] = totalHeavyAttempts,
                    ["successes"] = totalHeavySuccesses,
                    ["mastery"] = totalHeavyMastery,
                    ["avg_attempts_per_run"] = totalHeavyAttempts / n,
                    ["avg_mastery_per_run"] = totalHeavyMastery / n,
                    ["avg_mastery_per_person"] = totalHeavyMastery / n / 6.0,
                    ["nonzero_runs"] = heavyMasteryNonzeroCount,
                },
            };

            if (string.IsNullOrEmpty(outputPath))
            {
                GD.Print(Json.Stringify(report, "\t"));
            }
            else
            {
                WriteJsonFile(outputPath, report);
            }

            return 0;
        }
        finally
        {
            DisposeObjects(scenarioDef, terrainGenerator, contentProvider);
        }
    }

    private static GDictionary RunSingleSimulation(
        BattleSimScenarioDef scenarioDef,
        BattleSimOverrideApplyResult overrides,
        BattleSimContentProvider contentProvider,
        BattleTerrainGenerator terrainGenerator,
        TestMasteryGateway gateway,
        long seed
    )
    {
        var runtime = new BattleRuntimeModule();
        BattleState state = null;
        EncounterAnchorData encounterAnchor = null;
        try
        {
            bool useFormalTerrain = scenarioDef != null && scenarioDef.use_formal_terrain_generation;
            runtime.setup(
                gateway,
                overrides.SkillDefs,
                contentProvider.GetEnemyTemplatesTyped(),
                overrides.EnemyAiBrains,
                null,
                default,
                new System.Collections.Generic.Dictionary<StringName, ItemDef>(),
                useFormalTerrain ? null : terrainGenerator
            );
            runtime.SetAiTraceEnabled(false);
            runtime.SetAiScoreProfile(overrides.AiScoreProfile);

            encounterAnchor = new EncounterAnchorData
            {
                entity_id = scenarioDef != null && scenarioDef.scenario_id != ""
                    ? scenarioDef.scenario_id
                    : "battle_sim",
                display_name = scenarioDef != null && !string.IsNullOrEmpty(scenarioDef.display_name)
                    ? scenarioDef.display_name
                    : scenarioDef?.scenario_id.ToString() ?? "battle_sim",
                faction_id = "hostile",
                world_coord = Vector2I.Zero,
                region_tag = "simulation",
            };

            state = runtime.StartBattle(encounterAnchor, seed, scenarioDef.BuildStartContext());

            BattleSimExecutionLoopResult loopResult = new BattleSimExecutionLoop().Run(
                runtime,
                state,
                scenarioDef,
                MaxIdleLoops
            );
            return new GDictionary
            {
                ["battle_ended"] = state != null && state.phase == "battle_ended",
                ["winner_faction_id"] = state != null ? state.winner_faction_id.ToString() : "",
                ["iterations"] = loopResult.iterations,
                ["timeline_steps"] = loopResult.timeline_steps,
                ["metrics"] = BattleMetricsProjection.Project(runtime.GetBattleMetricsTyped()),
            };
        }
        finally
        {
            runtime.dispose();
            state?.Dispose();
            encounterAnchor?.Dispose();
        }
    }

    private sealed class TestMasteryGateway : IBattleRuntimeCharacterGateway
    {
        public int ChargeMastery { get; private set; }
        public int HeavyMastery { get; private set; }

        public PartyState GetPartyState() => null;

        public IReadOnlyDictionary<StringName, ItemDef> GetItemDefsTyped() =>
            new Dictionary<StringName, ItemDef>();

        public bool HasItemDefCatalog() => false;

        public ItemDef GetItemDef(StringName item_id) => null;

        public PartyMemberState GetMemberState(StringName member_id) => null;

        public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
            StringName member_id,
            EquipmentState equipment_view
        ) => null;

        public WeaponProjection GetMemberWeaponProjectionForEquipmentViewTyped(
            StringName member_id,
            EquipmentState equipment_view
        ) => new();

        public PassiveSourceContext BuildPassiveSourceContext(
            StringName member_id,
            UnitProgress progression_state
        ) => null;

        public CharacterProgressionDelta PromoteProfession(
            StringName member_id,
            StringName profession_id,
            GDictionary selection
        ) => new() { member_id = member_id };

        public void CommitBattleResources(
            StringName member_id,
            int current_hp,
            int current_mp,
            int current_aura
        ) { }

        public void CommitBattleDeath(StringName member_id) { }

        public int FlushAfterBattle() => (int)Error.Ok;

        public CharacterProgressionDelta GrantBattleMastery(
            StringName member_id,
            StringName skill_id,
            int amount
        )
        {
            return RecordGrant(member_id, skill_id, amount, "battle");
        }

        public CharacterProgressionDelta GrantSkillMasteryFromSource(
            StringName member_id,
            StringName skill_id,
            int amount,
            StringName source_type,
            string source_label,
            string reason_text,
            bool emit_achievement_event
        )
        {
            return RecordGrant(member_id, skill_id, amount, source_type);
        }

        public GStringNameArray RecordAchievementEvent(
            StringName member_id,
            StringName event_type,
            int amount
        )
        {
            return RecordAchievementEvent(member_id, event_type, amount, "", new GDictionary());
        }

        public GStringNameArray RecordAchievementEvent(
            StringName member_id,
            StringName event_type,
            int amount,
            StringName subject_id,
            GDictionary meta
        ) => new();

        public PendingCharacterReward BuildPendingSkillMasteryReward(
            StringName member_id,
            StringName source_type,
            string source_label,
            IEnumerable<PendingCharacterRewardEntry> entry_options,
            string summary_text
        ) => null;

        private CharacterProgressionDelta RecordGrant(
            StringName memberId,
            StringName skillId,
            int amount,
            StringName sourceType
        )
        {
            if (skillId == "charge")
                ChargeMastery += amount;
            else if (skillId == "warrior_heavy_strike")
                HeavyMastery += amount;

            var delta = new CharacterProgressionDelta { member_id = memberId };
            delta.AddMasteryChange(
                new CharacterMasteryChangeFact(skillId, "", amount, sourceType, "", "")
            );
            return delta;
        }
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
            GD.PushError($"[ERROR] Failed to write: {absolutePath}");
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
        string value = OS.GetEnvironment(key).StripEdges().ToLowerInvariant();
        return value switch
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
