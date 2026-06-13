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
        TestHeadlessBattleEquipmentHelpersStayTypedInternally();
        await TestDisposeClearsBattleSaveLockOnSharedGameSession();
        await TestBuildSnapshotDoesNotRebuildMissingSaveIndex();
        TestTypedEnemyCatalogRejectsStringKeyOnlyEntries();
        await TestFacadeBattleSetupUsesTypedEnemyCatalogs();

        Quit(_test.Finish("Headless game test session regression"));
    }

    private void TestHeadlessBattleEquipmentHelpersStayTypedInternally()
    {
        MethodInfo resolveEquipment = typeof(HeadlessGameTestSession).GetMethod(
            "ResolveBattleBackpackEquipmentInstance",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo findReport = typeof(HeadlessGameTestSession).GetMethod(
            "FindLastChangeEquipmentReport",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        MethodInfo buildBattleStartDiagnostic = typeof(HeadlessGameTestSession).GetMethod(
            "BuildBattleStartDiagnostic",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        MethodInfo createNewGameTyped = typeof(HeadlessGameTestSession).GetMethod(
            "CreateNewGameTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo loadGameTyped = typeof(HeadlessGameTestSession).GetMethod(
            "LoadGameTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo ensureWorldLoadedTyped = typeof(HeadlessGameTestSession).GetMethod(
            "EnsureWorldLoadedTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo setPartyStorageCapacityTyped = typeof(HeadlessGameTestSession).GetMethod(
            "SetPartyStorageCapacityTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo startBattleByKindTyped = typeof(HeadlessGameTestSession).GetMethod(
            "StartBattleByKindTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo finishActiveBattleTyped = typeof(HeadlessGameTestSession).GetMethod(
            "FinishActiveBattleTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo runtimeBattleWaitOrResolveTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandBattleWaitOrResolveTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo changeBattleEquipmentTyped = typeof(HeadlessGameTestSession).GetMethod(
            "ChangeBattleEquipmentTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        PropertyInfo runtimeOutcomeCode = typeof(GameRuntimeFacade.RuntimeCommandResult).GetProperty(
            "Code",
            BindingFlags.Instance | BindingFlags.Public
        );
        PropertyInfo sessionOutcomeCode = typeof(HeadlessGameTestSession.SessionCommandOutcome).GetProperty(
            "Code",
            BindingFlags.Instance | BindingFlags.Public
        );
        MethodInfo getWorldEncounterAnchorsTyped = typeof(HeadlessGameTestSession).GetMethod(
            "GetWorldEncounterAnchorsTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo changeBattleEquipment = typeof(HeadlessGameTestSession).GetMethod(
            "change_battle_equipment",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[]
            {
                typeof(StringName),
                typeof(StringName),
                typeof(StringName),
                typeof(StringName),
                typeof(GDictionary),
            },
            modifiers: null
        );
        _test.True(
            resolveEquipment != null
                && resolveEquipment.ReturnType != typeof(GDictionary),
            "HeadlessGameTestSession 换装实例解析 helper 不应继续返回 GDictionary。"
        );
        _test.True(
            findReport != null
                && findReport.ReturnType != typeof(GDictionary)
                && findReport.GetParameters()[0].ParameterType != typeof(Godot.Collections.Array),
            "HeadlessGameTestSession change-equipment report helper 不应继续以 GArray/GDictionary 作为内部 contract。"
        );
        _test.True(
            buildBattleStartDiagnostic != null
                && buildBattleStartDiagnostic.GetParameters()[3].ParameterType
                    == typeof(IReadOnlyDictionary<string, object>),
            "HeadlessGameTestSession battle-start diagnostic 应直接消费 typed context，不应回读 GDictionary。"
        );
        _test.True(
            createNewGameTyped != null
                && createNewGameTyped.ReturnType
                    == typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession new-game helper 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.True(
            loadGameTyped != null
                && loadGameTyped.ReturnType
                    == typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession load-game helper 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.True(
            ensureWorldLoadedTyped != null
                && ensureWorldLoadedTyped.ReturnType
                    == typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession world-load gate 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.True(
            setPartyStorageCapacityTyped != null
                && setPartyStorageCapacityTyped.ReturnType
                    == typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession storage-capacity helper 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.True(
            startBattleByKindTyped != null
                && startBattleByKindTyped.ReturnType
                    == typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession start-battle helper 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.True(
            finishActiveBattleTyped != null
                && finishActiveBattleTyped.ReturnType
                    == typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession finish-battle helper 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.True(
            runtimeBattleWaitOrResolveTyped != null
                && runtimeBattleWaitOrResolveTyped.ReturnType
                    == typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade battle wait/resolve helper 应提供 typed runtime outcome，避免 session/runner 回读 GDictionary。"
        );
        _test.True(
            changeBattleEquipmentTyped != null
                && changeBattleEquipmentTyped.ReturnType
                    == typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession battle-equipment helper 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            runtimeOutcomeCode?.PropertyType,
            typeof(GameRuntimeFacade.RuntimeCommandCode),
            "GameRuntimeFacade.RuntimeCommandResult 应提供统一的 enum code。"
        );
        _test.Eq(
            sessionOutcomeCode?.PropertyType,
            typeof(GameRuntimeFacade.RuntimeCommandCode),
            "HeadlessGameTestSession.SessionCommandOutcome 应透传统一的 enum code。"
        );
        _test.True(
            getWorldEncounterAnchorsTyped != null
                && getWorldEncounterAnchorsTyped.ReturnType
                    == typeof(IReadOnlyList<EncounterAnchorData>),
            "HeadlessGameTestSession world-data encounter 读取应先停留在 typed anchor list，不应继续回读 GArray。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "ReadArray",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留通用 GArray 读取 helper。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "ReadStringName",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留无调用方的 GDictionary StringName 读取 helper。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "ReadString",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留仅供本地 report 解析使用的 GDictionary string 读取 helper。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "ResultOk",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留仅供本地 report 解析使用的 GDictionary ok 读取 helper。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "ReadExactBool",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留仅供本地 report 解析使用的 GDictionary bool 读取 helper。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "SessionOutcomeFromDictionary",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留只给 battle wait/resolve 结果回读服务的 GDictionary helper。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "TryRead",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留通用 GDictionary key lookup helper。"
        );
        _test.True(
            changeBattleEquipment == null,
            "HeadlessGameTestSession.change_battle_equipment 不应继续保留 GDictionary options overload。"
        );

        HeadlessGameTestSession session = new();
        session.initialize();
        try
        {
            object missingPresetOutcome = createNewGameTyped?.Invoke(
                session,
                new object[] { new StringName("missing_headless_preset") }
            );
            _test.Eq(
                (GameRuntimeFacade.RuntimeCommandCode?)sessionOutcomeCode?.GetValue(missingPresetOutcome),
                GameRuntimeFacade.RuntimeCommandCode.NotFound,
                "HeadlessGameTestSession.CreateNewGameTyped 缺失预设时应返回 enum NotFound code。"
            );
        }
        finally
        {
            session.Dispose(true);
        }
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
}
