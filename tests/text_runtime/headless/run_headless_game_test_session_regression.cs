using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class HeadlessGameTestSessionRegressionGcGuard
{
    private const long StartupNoGcRegionSizeBytes = 512L * 1024L * 1024L;

    // This runner loads enough C# script-backed resources before _Initialize that
    // Mono finalizers can run during Godot script loading and touch released handles.
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Start()
    {
        foreach (string arg in System.Environment.GetCommandLineArgs())
        {
            if (
                !string.IsNullOrEmpty(arg)
                && arg.Contains(
                    "run_headless_game_test_session_regression",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                GC.TryStartNoGCRegion(StartupNoGcRegionSizeBytes);
                return;
            }
        }
    }
}

public partial class run_headless_game_test_session_regression : SceneTree
{
    private const string SaveIndexPath = "user://saves/index.dat";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(RunAsync));
    }

    private async void RunAsync()
    {
        GodotSharpCleanup.CollectPendingFinalizers();
        await TestDisposeClearsBattleSaveLockOnSharedGameSession();
        await TestOwnedGameSessionDisposeRemovesLogSink();
        await TestBuildSnapshotDoesNotRebuildMissingSaveIndex();
        TestTypedEnemyCatalogRejectsStringKeyOnlyEntries();
        await TestFacadeBattleSetupUsesTypedEnemyCatalogs();

        Quit(_test.Finish("Headless game test session regression"));
    }

    private async Task TestDisposeClearsBattleSaveLockOnSharedGameSession()
    {
        GameSession sharedGameSession = Root.GetNodeOrNull<GameSession>("GameSession");
        _test.True(
            sharedGameSession != null,
            "Headless session 回归前置：SceneTree 应提供共享 GameSession。"
        );
        if (sharedGameSession == null)
            return;

        sharedGameSession.ClearPersistedGame();
        await WaitFrame();

        HeadlessGameTestSession session = new();
        session.initialize();

        HeadlessGameTestSession.SessionCommandOutcome createResult = session.CreateNewGameTyped(
            "test"
        );
        _test.True(
            createResult.Ok,
            $"Headless session 应能在共享 GameSession 上创建测试世界。message={createResult.Message}"
        );
        if (!createResult.Ok)
        {
            await CleanupSharedGameSession(sharedGameSession);
            return;
        }

        HeadlessGameTestSession.SessionCommandOutcome battleResult = session.StartBattleByKindTyped(
            EncounterAnchorData.ToStringName(EncounterAnchorKind.Single)
        );
        _test.True(
            battleResult.Ok,
            $"Headless session 应能启动单体遭遇战。message={battleResult.Message}"
        );
        if (!battleResult.Ok)
        {
            session.Dispose(true);
            await CleanupSharedGameSession(sharedGameSession);
            return;
        }

        _test.True(
            sharedGameSession.IsBattleSaveLocked(),
            "进入 headless 战斗后 GameSession 应持有 battle save lock。"
        );

        session.Dispose(true);
        await WaitFrame();

        _test.True(
            !sharedGameSession.IsBattleSaveLocked(),
            "Headless session dispose 后应清掉 GameSession 的 battle save lock。"
        );
        _test.True(
            session.GetRuntimeFacade() == null,
            "Headless session dispose 后不应继续保留 runtime facade。"
        );

        await CleanupSharedGameSession(sharedGameSession);
    }

    private async Task TestOwnedGameSessionDisposeRemovesLogSink()
    {
        int sinkCountBefore = GetGameLogSinkCount();
        GameSession ownedGameSession = new() { Name = "OwnedHeadlessGameSessionForDisposeRegression" };
        Root.AddChild(ownedGameSession);
        await WaitFrame();

        _test.Eq(
            GetGameLogSinkCount(),
            sinkCountBefore + 1,
            "创建 owned GameSession 时应注册一个 GameLog sink。"
        );

        HeadlessGameTestSession session = new();
        SetPrivateField(session, "_gameSession", ownedGameSession);
        SetPrivateField(session, "_ownsGameSession", true);

        try
        {
            session.Dispose(false);
            await WaitFrame();

            _test.Eq(
                GetGameLogSinkCount(),
                sinkCountBefore,
                "Headless owned GameSession dispose 应通过 GameSession.Dispose 移除 GameLog sink。"
            );
            _test.True(
                !GodotObject.IsInstanceValid(ownedGameSession),
                "Headless owned GameSession dispose 后应释放 owned native node。"
            );
        }
        finally
        {
            if (GodotObject.IsInstanceValid(ownedGameSession))
                ownedGameSession.QueueFree();
        }
    }

    private async Task TestBuildSnapshotDoesNotRebuildMissingSaveIndex()
    {
        GameSession sharedGameSession = Root.GetNodeOrNull<GameSession>("GameSession");
        _test.True(
            sharedGameSession != null,
            "Headless snapshot 只读回归前置：SceneTree 应提供共享 GameSession。"
        );
        if (sharedGameSession == null)
            return;

        sharedGameSession.ClearPersistedGame();
        await WaitFrame();

        HeadlessGameTestSession session = new();
        session.initialize();
        HeadlessGameTestSession.SessionCommandOutcome createResult = session.CreateNewGameTyped(
            "test"
        );
        _test.True(
            createResult.Ok,
            "Headless snapshot 只读回归前置：应能创建测试世界。"
        );
        if (!createResult.Ok)
        {
            session.Dispose(true);
            await CleanupSharedGameSession(sharedGameSession);
            return;
        }

        RemoveUserFileIfExists(SaveIndexPath);
        _test.True(
            !FileAccess.FileExists(ProjectSettings.GlobalizePath(SaveIndexPath)),
            "Headless snapshot 只读回归前置：index.dat 应已被删除。"
        );

        GDictionary snapshot = session.BuildSnapshot();
        _test.True(
            snapshot.TryGetValue("session", out Variant sessionValue)
                && sessionValue.VariantType == Variant.Type.Dictionary,
            "Headless snapshot 应仍返回 session 字段。"
        );
        GDictionary mutatedStatus = new() { ["view"] = "mutated", ["text"] = "mutated" };
        snapshot["status"] = mutatedStatus;
        GDictionary mutatedSession = snapshot["session"].AsGodotDictionary();
        mutatedSession["world_loaded"] = false;
        GDictionary secondSnapshot = session.BuildSnapshot();
        _test.True(
            DictString(secondSnapshot["status"].AsGodotDictionary(), "view", "")
                != "mutated",
            "build_snapshot() 不应把外部替换 projection status 的修改写回 owner 组装态。"
        );
        _test.True(
            DictBool(secondSnapshot["session"].AsGodotDictionary(), "world_loaded", true)
                != false,
            "build_snapshot() 不应把外部修改 nested session projection 写回 owner 组装态。"
        );
        _test.True(
            !FileAccess.FileExists(ProjectSettings.GlobalizePath(SaveIndexPath)),
            "build_snapshot() 不应通过 list_save_slots() 或恢复流程重建 index.dat。"
        );

        session.Dispose(true);
        await CleanupSharedGameSession(sharedGameSession);
    }

    private void TestTypedEnemyCatalogRejectsStringKeyOnlyEntries()
    {
        GameSession sharedGameSession = Root.GetNodeOrNull<GameSession>("GameSession");
        _test.True(
            sharedGameSession != null,
            "Headless enemy typed catalog 回归前置：SceneTree 应提供共享 GameSession。"
        );
        if (sharedGameSession == null)
            return;

        StringName templateId = "headless_string_key_enemy_template";
        StringName rosterId = "headless_string_key_enemy_roster";
        EnemyTemplateDef template = new()
        {
            template_id = templateId,
            display_name = "String Key Enemy",
            brain_id = "melee_aggressor",
        };
        WildEncounterRosterDef roster = new()
        {
            profile_id = rosterId,
            display_name = "String Key Roster",
            initial_stage = 0,
            growth_step_interval = 1,
            stages = new Godot.Collections.Array<WildEncounterRosterStageDef>
            {
                new WildEncounterRosterStageDef
                {
                    stage = 0,
                    unit_entries = new Godot.Collections.Array<WildEncounterRosterUnitEntryDef>
                    {
                        new WildEncounterRosterUnitEntryDef { template_id = templateId, count = 1 }
                    }
                }
            }
        };

        _test.True(
            sharedGameSession.InstallTestContentDefStringKey(
                "enemy_template",
                templateId.ToString(),
                template
            ) == (int)Error.Ok,
            "String-key-only enemy template fixture 应能注入 shared GameSession。"
        );
        _test.True(
            sharedGameSession.InstallTestContentDefStringKey(
                "wild_encounter_roster",
                rosterId.ToString(),
                roster
            ) == (int)Error.Ok,
            "String-key-only wild encounter roster fixture 应能注入 shared GameSession。"
        );

        _test.True(
            !sharedGameSession.GetEnemyTemplatesTyped().ContainsKey(templateId),
            "typed enemy template catalog 不应恢复 string-key-only template。"
        );
        _test.True(
            !sharedGameSession.GetWildEncounterRostersTyped().ContainsKey(rosterId),
            "typed wild encounter roster catalog 不应恢复 string-key-only roster。"
        );
    }

    private async Task TestFacadeBattleSetupUsesTypedEnemyCatalogs()
    {
        GameSession sharedGameSession = Root.GetNodeOrNull<GameSession>("GameSession");
        _test.True(
            sharedGameSession != null,
            "battle runtime facade regression 前置：SceneTree 应提供共享 GameSession。"
        );
        if (sharedGameSession == null)
            return;

        sharedGameSession.ClearPersistedGame();
        await WaitFrame();

        StringName templateId = "string_key_facade_template";
        StringName brainId = "string_key_facade_brain";
        StringName itemId = "string_key_facade_item";
        EnemyAiBrainDef brain = new()
        {
            brain_id = brainId,
            default_state_id = "engage",
            states = new Godot.Collections.Array<EnemyAiStateDef>
            {
                new EnemyAiStateDef { state_id = "engage" }
            }
        };
        EnemyTemplateDef template = new()
        {
            template_id = templateId,
            display_name = "String Key Enemy",
            brain_id = brainId,
            enemy_count = 1,
        };
        ItemDef item = new() { item_id = itemId, display_name = "String Key Item" };

        _test.True(
            sharedGameSession.InstallTestContentDefStringKey(
                "enemy_ai_brain",
                brainId.ToString(),
                brain
            ) == (int)Error.Ok,
            "应能注入 string-key-only enemy_ai_brain fixture。"
        );
        _test.True(
            sharedGameSession.InstallTestContentDefStringKey(
                "enemy_template",
                templateId.ToString(),
                template
            ) == (int)Error.Ok,
            "应能注入 string-key-only enemy_template fixture。"
        );
        _test.True(
            sharedGameSession.InstallTestContentDefStringKey("item", itemId.ToString(), item)
                == (int)Error.Ok,
            "应能注入 string-key-only item fixture。"
        );
        _test.True(
            !sharedGameSession.GetEnemyAiBrainsTyped().ContainsKey(brainId),
            "typed enemy_ai_brains getter 应过滤 string-key-only fixture。"
        );
        _test.True(
            !sharedGameSession.GetEnemyTemplatesTyped().ContainsKey(templateId),
            "typed enemy_templates getter 应过滤 string-key-only fixture。"
        );
        _test.True(
            !sharedGameSession.GetItemDefsTyped().ContainsKey(itemId),
            "typed item_defs getter 应过滤 string-key-only fixture。"
        );

        HeadlessGameTestSession session = new();
        session.initialize();
        try
        {
            HeadlessGameTestSession.SessionCommandOutcome createResult =
                session.CreateNewGameTyped("test");
            _test.True(
                createResult.Ok,
                $"headless session 应能创建测试世界。message={createResult.Message}"
            );
            if (!createResult.Ok)
                return;

            GameRuntimeFacade runtime = session.GetRuntimeFacadeTyped();
            _test.True(runtime != null, "battle runtime facade regression 应拿到 typed runtime。");
            if (runtime == null)
                return;

            _test.True(
                runtime._battle_runtime.GetEnemyTemplateIndexTyped().ContainsKey("wolf_pack"),
                "BattleRuntimeModule typed template index 应继续保留正式 template。"
            );
            _test.True(
                runtime._battle_runtime.GetEnemyAiBrainIndexTyped().ContainsKey("melee_aggressor"),
                "BattleRuntimeModule typed brain index 应继续保留正式 brain。"
            );
            _test.True(
                !runtime._battle_runtime.GetEnemyTemplateIndexTyped().ContainsKey(templateId),
                "GameRuntimeFacade.setup 不应把 string-key-only enemy template 恢复进 battle runtime typed index。"
            );
            _test.True(
                !runtime._battle_runtime.GetEnemyAiBrainIndexTyped().ContainsKey(brainId),
                "GameRuntimeFacade.setup 不应把 string-key-only enemy brain 恢复进 battle runtime typed index。"
            );
            _test.True(
                !runtime._battle_runtime.BuildItemDefIndexSnapshotTyped().ContainsKey(itemId),
                "GameRuntimeFacade.setup 不应把 string-key-only item 恢复进 battle runtime typed item index。"
            );
            _test.True(
                !runtime._battle_runtime._enemy_templates.ContainsKey(templateId.ToString()),
                "GameRuntimeFacade.setup 不应把 string-key-only enemy template 投影进 battle runtime public catalog。"
            );
            _test.True(
                !runtime._battle_runtime._enemy_ai_brains.ContainsKey(brainId.ToString()),
                "GameRuntimeFacade.setup 不应把 string-key-only enemy brain 投影进 battle runtime public catalog。"
            );
            _test.True(
                !runtime._battle_runtime.BuildItemDefIndexSnapshotTyped().ContainsKey(itemId),
                "GameRuntimeFacade.setup 不应把 string-key-only item 投影进 battle runtime typed item index。"
            );
        }
        finally
        {
            session.Dispose(true);
            await CleanupSharedGameSession(sharedGameSession);
        }
    }

    private async Task CleanupSharedGameSession(GameSession sharedGameSession)
    {
        if (sharedGameSession == null || !GodotObject.IsInstanceValid(sharedGameSession))
            return;
        sharedGameSession.ClearPersistedGame();
        await WaitFrame();
    }

    private async Task WaitFrame()
    {
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private static void RemoveUserFileIfExists(string path)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        if (FileAccess.FileExists(absolutePath))
            DirAccess.RemoveAbsolute(absolutePath);
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback) =>
        dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsBool() : fallback;

    private static string DictString(GDictionary dictionary, string key, string fallback) =>
        dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsString() : fallback;

    private static int GetGameLogSinkCount()
    {
        FieldInfo field = typeof(GameLog).GetField(
            "_sinks",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        return field?.GetValue(null) is System.Collections.ICollection sinks ? sinks.Count : -1;
    }

    private static void SetPrivateField<T>(T target, string fieldName, object value)
    {
        FieldInfo field = typeof(T).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        field?.SetValue(target, value);
    }
}
