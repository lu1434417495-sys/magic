using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_headless_game_test_session_regression : LifecycleTestSceneTree
{
    private const string SaveIndexPath = "user://saves/index.dat";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(RunAsync);
    }

    private async void RunAsync()
    {
        await TestCoordinatorlessHostUsesExplicitNoncanonicalOwnedGameSession();
        await TestDisposeClearsBattleSaveLockOnSharedGameSession();
        await TestOwnedGameSessionDisposeRemovesLogSink();
        await TestBuildSnapshotDoesNotRebuildMissingSaveIndex();
        TestSyntheticEnemyDefinitionsUseTypedKeys();
        await TestFacadeBattleSetupUsesSyntheticEnemyDefinitions();

        RequestTestExit(_test.Finish("Headless game test session regression"));
    }

    private async Task TestCoordinatorlessHostUsesExplicitNoncanonicalOwnedGameSession()
    {
        ApplicationLifetimeCoordinator coordinator =
            Root.GetNodeOrNull<ApplicationLifetimeCoordinator>(
                "ApplicationLifetimeCoordinator"
            );
        GameSession canonicalGameSession = Root.GetNodeOrNull<GameSession>("GameSession");
        _test.True(
            coordinator != null && canonicalGameSession != null,
            "Coordinator-less headless 回归前置：autoload owners 应存在。"
        );
        if (coordinator == null || canonicalGameSession == null)
            return;

        StringName originalCoordinatorName = coordinator.Name;
        StringName originalGameSessionName = canonicalGameSession.Name;
        var session = new HeadlessGameTestSession();
        GameSession ownedGameSession = GameSessionTestFactory.CreateSynthetic(
            session,
            null
        );
        try
        {
            coordinator.Name = "ApplicationLifetimeCoordinatorHiddenForHeadlessRegression";
            canonicalGameSession.Name = "CanonicalGameSessionHiddenForHeadlessRegression";

            bool initialized = true;
            try
            {
                session.initialize();
            }
            catch (Exception exception)
            {
                initialized = false;
                _test.Fail(
                    $"Coordinator-less headless host 应能初始化 owned GameSession。exception={exception}"
                );
            }

            ownedGameSession = session.GetGameSessionTyped();
            _test.True(initialized, "Coordinator-less headless host 初始化不应抛异常。");
            _test.True(
                ownedGameSession != null && GodotObject.IsInstanceValid(ownedGameSession),
                "Coordinator-less headless host 应创建有效的 owned GameSession。"
            );
            if (ownedGameSession != null && GodotObject.IsInstanceValid(ownedGameSession))
            {
                _test.True(
                    !ReferenceEquals(ownedGameSession, canonicalGameSession),
                    "Coordinator-less headless host 应使用显式绑定的 owned GameSession。"
                );
                _test.True(
                    ownedGameSession.Name != "GameSession",
                    "显式 owned GameSession 必须保持 noncanonical identity。"
                );
                _test.True(
                    Root.GetNodeOrNull<GameSession>("GameSession") == null,
                    "Coordinator-less owned GameSession 不应占用 canonical root path。"
                );
            }
        }
        finally
        {
            session.Dispose(false);
            if (GodotObject.IsInstanceValid(coordinator))
                coordinator.Name = originalCoordinatorName;
            if (GodotObject.IsInstanceValid(canonicalGameSession))
                canonicalGameSession.Name = originalGameSessionName;
            await WaitFrame();
        }

        _test.True(
            ownedGameSession == null || !GodotObject.IsInstanceValid(ownedGameSession),
            "Coordinator-less owned GameSession 应由 headless host dispose。"
        );
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

        LifecycleAuditSnapshot sessionLifecycleBaseline =
            LifecycleAuditRegistry.Shared.CaptureSnapshot();
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

        BattleRuntimeModule battleRuntime = session
            .GetRuntimeFacadeTyped()
            ?.GetBattleRuntime();
        _test.True(battleRuntime != null, "borrowed context 回归前置：battle runtime 应存在。");
        if (battleRuntime == null)
        {
            session.Dispose(true);
            await CleanupSharedGameSession(sharedGameSession);
            return;
        }

        LifecycleAuditSnapshot borrowedFailureBaseline =
            LifecycleAuditRegistry.Shared.CaptureSnapshot();
        var invalidContext = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["battle_party"] = 1,
        };
        bool borrowedFailureThrown;
        using (
            GodotProjectionLease<GDictionary> invalidContextLease =
                RuntimePlainPayload.ProjectDictionaryLease(
                    invalidContext,
                    "headless-battle-start-invalid-context",
                    LifetimeDomain.Request,
                    "run_headless_game_test_session_regression.invalid_context"
                )
        )
        {
            LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            _test.Eq(
                active.ActiveOwnerCount,
                borrowedFailureBaseline.ActiveOwnerCount + 1,
                "malformed borrowed context lease 应精确拥有一个 root container。"
            );
            _test.Eq(
                active.ActiveLeaseCount,
                borrowedFailureBaseline.ActiveLeaseCount + 1,
                "malformed borrowed context 应只登记一个 caller lease。"
            );
            _test.Eq(
                active.ActiveScopeCount,
                borrowedFailureBaseline.ActiveScopeCount,
                "malformed borrowed context 不应登记额外 native scope。"
            );
            _test.Eq(
                active.ActiveContentBorrowerCount,
                borrowedFailureBaseline.ActiveContentBorrowerCount,
                "malformed borrowed context 不应登记 content borrower。"
            );
            borrowedFailureThrown = Throws<InvalidOperationException>(
                () =>
                    battleRuntime.StartBattleBorrowingContext(
                        null,
                        1,
                        BattleEliminationObjectiveDefinition.Instance,
                        invalidContextLease.Value
                    )
            );
            LifecycleAuditSnapshot afterThrow =
                LifecycleAuditRegistry.Shared.CaptureSnapshot();
            _test.Eq(
                afterThrow.ActiveOwnerCount,
                active.ActiveOwnerCount,
                "borrowed context 抛错后 caller lease 应在 using 内保持唯一 owner。"
            );
            _test.Eq(
                afterThrow.ActiveLeaseCount,
                active.ActiveLeaseCount,
                "borrowed context 抛错后 caller lease 应在 using 内保持 active。"
            );
        }
        _test.True(
            borrowedFailureThrown,
            "非空 malformed borrowed battle context 应在进入 runtime 后稳定拒绝。"
        );
        AssertAuditBaseline(
            borrowedFailureBaseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "borrowed battle context throw"
        );
        AssertSafetyCounters(
            borrowedFailureBaseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "borrowed battle context throw",
            includeKnownSuppressBaseline: true
        );
        _test.True(
            Throws<ArgumentNullException>(
                () =>
                    battleRuntime.StartBattleBorrowingContext(
                        null,
                        1,
                        BattleEliminationObjectiveDefinition.Instance,
                        null
                    )
            ),
            "borrowed battle context 入口应独立拒绝 null。"
        );
        AssertAuditBaseline(
            borrowedFailureBaseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "borrowed battle context null guard"
        );

        LifecycleAuditSnapshot borrowedSuccessBaseline =
            LifecycleAuditRegistry.Shared.CaptureSnapshot();
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
        AssertBattleRuntimeScopesDrained(
            borrowedSuccessBaseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "borrowed battle context return"
        );
        AssertSafetyCounters(
            borrowedSuccessBaseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "borrowed battle context return",
            includeKnownSuppressBaseline: false
        );

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
        AssertAuditBaseline(
            sessionLifecycleBaseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "headless session dispose"
        );

        await CleanupSharedGameSession(sharedGameSession);
    }

    private async Task TestOwnedGameSessionDisposeRemovesLogSink()
    {
        int sinkCountBefore = GetGameLogSinkCount();
        GameSession ownedGameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot(
            "OwnedHeadlessGameSessionForDisposeRegression"
        );
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

        using GodotProjectionLease<GDictionary> snapshotLease = session.BuildSnapshotLease();
        GDictionary snapshot = snapshotLease.Value;
        _test.True(
            snapshot.TryGetValue("session", out Variant sessionValue)
                && sessionValue.VariantType == Variant.Type.Dictionary,
            "Headless snapshot 应仍返回 session 字段。"
        );
        GDictionary mutatedStatus = new() { ["view"] = "mutated", ["text"] = "mutated" };
        snapshot["status"] = mutatedStatus;
        GDictionary mutatedSession = snapshot["session"].AsGodotDictionary();
        mutatedSession["world_loaded"] = false;
        using GodotProjectionLease<GDictionary> secondSnapshotLease =
            session.BuildSnapshotLease();
        GDictionary secondSnapshot = secondSnapshotLease.Value;
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

    private void TestSyntheticEnemyDefinitionsUseTypedKeys()
    {
        StringName templateId = "headless_synthetic_enemy_template";
        StringName rosterId = "headless_synthetic_enemy_roster";
        EnemyTemplateDef template = TestResourceOwnership.Own(
            new EnemyTemplateDef
            {
                template_id = templateId,
                display_name = "Synthetic Enemy",
                brain_id = "melee_aggressor",
            },
            "headless.synthetic_legacy.template"
        );
        WildEncounterRosterDef roster = TestResourceOwnership.Own(
            new WildEncounterRosterDef
            {
                profile_id = rosterId,
                display_name = "Synthetic Roster",
                initial_stage = 0,
                growth_step_interval = 1,
                stages = new Godot.Collections.Array<WildEncounterRosterStageDef>
                {
                    new WildEncounterRosterStageDef
                    {
                        stage = 0,
                        unit_entries = new Godot.Collections.Array<WildEncounterRosterUnitEntryDef>
                        {
                            new WildEncounterRosterUnitEntryDef
                            {
                                template_id = templateId,
                                count = 1,
                            },
                        },
                    },
                },
            },
            "headless.synthetic_legacy.roster"
        );
        ContentSnapshot processSnapshot = GameSessionTestFactory.GetProcessSnapshot();
        EnemyTemplateDefinition templateDefinition = template.ToDefinition(
            processSnapshot.Items
        );
        WildEncounterRosterDefinition rosterDefinition = roster.ToDefinition();
        using GameSession session = GameSessionTestFactory.CreateSyntheticFromProcessSnapshot(
            seed =>
            {
                seed.Quests = new Dictionary<StringName, QuestDefinition>();
                seed.WorldGenerations = new Dictionary<string, WorldGenerationDefinition>(
                    StringComparer.Ordinal
                );
                seed.EnemyTemplates = new Dictionary<StringName, EnemyTemplateDefinition>(
                    seed.EnemyTemplates
                )
                {
                    [templateId] = templateDefinition,
                };
                seed.EncounterRosters =
                    new Dictionary<StringName, WildEncounterRosterDefinition>(
                        seed.EncounterRosters
                    )
                    {
                        [rosterId] = rosterDefinition,
                    };
            }
        );

        _test.True(
            session.GetEnemyTemplateDefinitions().ContainsKey(templateId),
            "synthetic enemy template definition 应通过 typed StringName key 暴露。"
        );
        _test.True(
            session.GetEncounterRosterDefinitions().ContainsKey(rosterId),
            "synthetic encounter roster definition 应通过 typed StringName key 暴露。"
        );
    }

    private async Task TestFacadeBattleSetupUsesSyntheticEnemyDefinitions()
    {
        StringName templateId = "synthetic_facade_template";
        StringName brainId = "synthetic_facade_brain";
        EnemyAiBrainDef brain = TestResourceOwnership.Own(new EnemyAiBrainDef
        {
            brain_id = brainId,
            default_state_id = "engage",
            states = new Godot.Collections.Array<EnemyAiStateDef>
            {
                new EnemyAiStateDef { state_id = "engage" }
            },
        }, "headless.synthetic_facade.brain");
        EnemyTemplateDef template = TestResourceOwnership.Own(new EnemyTemplateDef
        {
            template_id = templateId,
            display_name = "Synthetic Enemy",
            brain_id = brainId,
            enemy_count = 1,
        }, "headless.synthetic_facade.template");

        ContentSnapshot processSnapshot = GameSessionTestFactory.GetProcessSnapshot();
        EnemyAiBrainDefinition brainDefinition = brain.ToDefinition();
        EnemyTemplateDefinition templateDefinition = template.ToDefinition(
            processSnapshot.Items
        );

        HeadlessGameTestSession session = new();
        GameSessionTestFactory.CreateSynthetic(
            session,
            seed =>
            {
                seed.EnemyBrains = new Dictionary<StringName, EnemyAiBrainDefinition>(
                    seed.EnemyBrains
                )
                {
                    [brainId] = brainDefinition,
                };
                seed.EnemyTemplates = new Dictionary<StringName, EnemyTemplateDefinition>(
                    seed.EnemyTemplates
                )
                {
                    [templateId] = templateDefinition,
                };
            }
        );
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
                runtime._battle_runtime.GetEnemyTemplateIndexTyped().ContainsKey(templateId),
                "GameRuntimeFacade.setup 应消费 synthetic enemy template definition index。"
            );
            _test.True(
                runtime._battle_runtime.GetEnemyAiBrainIndexTyped().ContainsKey(brainId),
                "GameRuntimeFacade.setup 应消费 synthetic enemy brain definition index。"
            );
        }
        finally
        {
            session.Dispose(true);
            await WaitFrame();
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

    private void AssertAuditBaseline(
        LifecycleAuditSnapshot expected,
        LifecycleAuditSnapshot actual,
        string label
    )
    {
        _test.Eq(actual.ActiveOwnerCount, expected.ActiveOwnerCount, $"{label}: owner baseline");
        _test.Eq(actual.ActiveLeaseCount, expected.ActiveLeaseCount, $"{label}: lease baseline");
        _test.Eq(actual.ActiveScopeCount, expected.ActiveScopeCount, $"{label}: scope baseline");
        _test.Eq(
            actual.ActiveContentBorrowerCount,
            expected.ActiveContentBorrowerCount,
            $"{label}: borrower baseline"
        );
    }

    private void AssertBattleRuntimeScopesDrained(
        LifecycleAuditSnapshot expected,
        LifecycleAuditSnapshot actual,
        string label
    )
    {
        _test.Eq(actual.ActiveOwnerCount, expected.ActiveOwnerCount, $"{label}: owner baseline");
        _test.Eq(actual.ActiveLeaseCount, expected.ActiveLeaseCount, $"{label}: lease baseline");
        _test.Eq(
            actual.ActiveScopeCount,
            expected.ActiveScopeCount,
            $"{label}: typed AI action plans retain no native battle scopes"
        );
        _test.Eq(
            actual.ActiveContentBorrowerCount,
            expected.ActiveContentBorrowerCount,
            $"{label}: borrower baseline"
        );
    }

    private void AssertSafetyCounters(
        LifecycleAuditSnapshot expected,
        LifecycleAuditSnapshot actual,
        string label,
        bool includeKnownSuppressBaseline
    )
    {
        _test.Eq(actual.EscapedCount, expected.EscapedCount, $"{label}: escaped baseline");
        _test.Eq(actual.UnknownCount, expected.UnknownCount, $"{label}: unknown baseline");
        _test.Eq(actual.ViolationCount, expected.ViolationCount, $"{label}: violation baseline");
        _test.Eq(
            actual.QuarantineCount,
            expected.QuarantineCount,
            $"{label}: quarantine baseline"
        );
        if (includeKnownSuppressBaseline)
        {
            _test.Eq(
                actual.NormalPhaseSuppressCount,
                expected.NormalPhaseSuppressCount,
                $"{label}: normal-phase suppress baseline"
            );
        }
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private static int GetGameLogSinkCount()
    {
        return GameLog.SinkCount;
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
