using System;
using System.Collections.Generic;
using Godot;

public partial class run_quest_accept_encounter_binding_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestBoundAcceptSpawnsBeforeAcceptAndCommitsPartyWithWorld();
        TestSpawnFailureRollsBackBeforeAccept();
        TestFailedAcceptRemovesAddedAnchorAndRollsBack();
        TestExistingAnchorIsReusedWithoutDuplicateRemoval();
        TestActiveStableAnchorWithDifferentGrowthStageIsRejected();
        TestClearedStableAnchorIsRebuilt();
        TestCommitFailureRollsBackPartyAndWorld();
        TestPlacementPrefersSouthAndIsDeterministic();
        TestWorldOwnerAddsAndIndexesEncounterAnchor();

        RequestTestExit(_test.Finish("Quest accept encounter binding regression"));
    }

    private void TestBoundAcceptSpawnsBeforeAcceptAndCommitsPartyWithWorld()
    {
        QuestDefinition definition = BuildQuestDefinition();
        var port = new RecordingQuestCommandPort(definition);
        var handler = new GameRuntimeQuestCommandHandler();
        handler.Setup(port);
        try
        {
            RuntimeCommandResult result = handler.CommandAcceptQuestTyped(
                definition.QuestId
            );

            _test.True(result.Ok, $"绑定遭遇的任务应可接取。message={result.Message}");
            _test.Eq(
                string.Join(",", port.Operations),
                "capture,spawn,accept,commit",
                "接取链必须先添加遭遇，再写入任务状态，最后一次提交。"
            );
            _test.True(
                port.CapturedTransaction?.PersistPartyState ?? false,
                "接取事务必须包含 party。"
            );
            _test.True(
                port.CapturedTransaction?.PersistWorldData ?? false,
                "绑定遭遇的接取事务必须包含 world。"
            );
            _test.Eq(
                port.SpawnProfileId,
                new StringName("farm_wolves"),
                "接取链应使用 objective 投影出的 encounter_profile_id。"
            );
            _test.Eq(
                port.SpawnDisplayName,
                "村南受袭农田",
                "接取链应使用 objective 投影出的 encounter_display_name。"
            );
            _test.Eq(
                port.SpawnGrowthStage,
                1,
                "接取链应把 objective 的 encounter_growth_stage 传给同一遭遇模板。"
            );
            _test.Eq(
                port.SpawnAnchorId,
                new StringName("quest_farmers_plea_encounter"),
                "任务遭遇 anchor id 必须由 quest_id 稳定推导。"
            );
        }
        finally
        {
            handler.Dispose();
        }
    }

    private void TestSpawnFailureRollsBackBeforeAccept()
    {
        QuestDefinition definition = BuildQuestDefinition();
        var port = new RecordingQuestCommandPort(definition)
        {
            SpawnSucceeds = false,
        };
        var handler = new GameRuntimeQuestCommandHandler();
        handler.Setup(port);
        try
        {
            RuntimeCommandResult result = handler.CommandAcceptQuestTyped(
                definition.QuestId
            );

            _test.False(result.Ok, "任务遭遇生成失败时接取命令必须失败。");
            _test.Eq(
                string.Join(",", port.Operations),
                "capture,spawn,rollback",
                "遭遇生成失败必须回滚已捕获的 party+world 事务，且不得写入任务状态。"
            );
        }
        finally
        {
            handler.Dispose();
        }
    }

    private void TestFailedAcceptRemovesAddedAnchorAndRollsBack()
    {
        QuestDefinition definition = BuildQuestDefinition();
        var port = new RecordingQuestCommandPort(definition)
        {
            AcceptSucceeds = false,
        };
        var handler = new GameRuntimeQuestCommandHandler();
        handler.Setup(port);
        try
        {
            RuntimeCommandResult result = handler.CommandAcceptQuestTyped(
                definition.QuestId
            );

            _test.False(result.Ok, "任务状态写入失败时接取命令必须失败。");
            _test.Eq(
                string.Join(",", port.Operations),
                "capture,spawn,accept,remove,rollback",
                "任务状态写入失败时必须移除刚添加的 anchor 并回滚，不得提交。"
            );
            _test.Eq(
                port.RemovedAnchorId,
                new StringName("quest_farmers_plea_encounter"),
                "失败清理必须命中本次稳定 anchor id。"
            );
        }
        finally
        {
            handler.Dispose();
        }
    }

    private void TestExistingAnchorIsReusedWithoutDuplicateRemoval()
    {
        QuestDefinition definition = BuildQuestDefinition();
        var port = new RecordingQuestCommandPort(definition)
        {
            SpawnAsExisting = true,
        };
        var handler = new GameRuntimeQuestCommandHandler();
        handler.Setup(port);
        try
        {
            RuntimeCommandResult result = handler.CommandAcceptQuestTyped(
                definition.QuestId
            );

            _test.True(result.Ok, "同 profile 的 stable anchor 已存在时应复用并继续接取。");
            _test.Eq(
                string.Join(",", port.Operations),
                "capture,spawn,accept,commit",
                "复用已有 anchor 不得执行 remove 或第二次添加。"
            );
        }
        finally
        {
            handler.Dispose();
        }
    }

    private void TestClearedStableAnchorIsRebuilt()
    {
        var runtime = new GameRuntimeFacade();
        try
        {
            runtime._grid_system.Setup(new Vector2I(8, 8), Vector2I.One);
            runtime._player_coord = new Vector2I(4, 4);
            runtime._world_map_data_context.BindRootWorldData(WorldRuntimeData.Empty());
            runtime.SetBattleEncounterDefinitionForTests(
                new BattleEncounterDefinition(
                    "farm_wolves",
                    "村南受袭农田",
                    "farm_wolves_roster",
                    BattleEliminationObjectiveDefinition.Instance,
                    new BattleEncounterWorldResolutionDefinition(
                        BattleWorldResolutionMode.Clear,
                        BattleWorldResolutionMode.Preserve,
                        BattleWorldResolutionMode.Preserve,
                        0
                    )
                )
            );
            EncounterAnchorData cleared = BuildAnchor(
                "quest_farmers_plea_encounter",
                new Vector2I(4, 5)
            );
            cleared.is_cleared = true;
            _test.True(
                runtime._world_map_data_context.TryAddEncounterAnchor(cleared),
                "前置条件：应能放入已清除的 stable anchor。"
            );

            QuestAcceptEncounterSpawnResult spawn =
                ((IGameRuntimeQuestCommandPort)runtime).TryAddQuestAcceptEncounter(
                    "farmers_plea",
                    "farm_wolves",
                    "村南受袭农田",
                    1
                );
            EncounterAnchorData rebuilt =
                runtime._world_map_data_context.GetEncounterAnchorById(
                    "quest_farmers_plea_encounter"
                );

            _test.True(spawn.Ok && spawn.Added, "已清除的同 profile anchor 必须重建，不能复用。");
            _test.True(rebuilt != null, "重建后 stable anchor 必须仍可按 id 查询。");
            _test.False(rebuilt?.is_cleared ?? true, "重建的任务遭遇必须恢复为未清除状态。");
            _test.Eq(rebuilt?.growth_stage ?? -1, 1, "重建锚点应使用任务绑定的 growth stage。");
            _test.Eq(
                rebuilt?.world_coord ?? new Vector2I(-1, -1),
                new Vector2I(4, 5),
                "旧锚点移除后应确定性复用发布者正南空格。"
            );
            _test.Eq(
                runtime._world_map_data_context.GetActiveEncounterAnchors().Count,
                1,
                "重建不得留下 cleared 重复锚点。"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestActiveStableAnchorWithDifferentGrowthStageIsRejected()
    {
        var runtime = new GameRuntimeFacade();
        try
        {
            runtime._grid_system.Setup(new Vector2I(8, 8), Vector2I.One);
            runtime._player_coord = new Vector2I(4, 4);
            runtime._world_map_data_context.BindRootWorldData(WorldRuntimeData.Empty());
            runtime.SetBattleEncounterDefinitionForTests(
                new BattleEncounterDefinition(
                    "farm_wolves",
                    "村南受袭农田",
                    "farm_wolves_roster",
                    BattleEliminationObjectiveDefinition.Instance,
                    new BattleEncounterWorldResolutionDefinition(
                        BattleWorldResolutionMode.Clear,
                        BattleWorldResolutionMode.Preserve,
                        BattleWorldResolutionMode.Preserve,
                        0
                    )
                )
            );
            EncounterAnchorData active = BuildAnchor(
                "quest_farmers_plea_encounter",
                new Vector2I(4, 5)
            );
            _test.True(
                runtime._world_map_data_context.TryAddEncounterAnchor(active),
                "前置条件：应能放入 stage 0 的活动 stable anchor。"
            );

            QuestAcceptEncounterSpawnResult spawn =
                ((IGameRuntimeQuestCommandPort)runtime).TryAddQuestAcceptEncounter(
                    "farmers_plea",
                    "farm_wolves",
                    "村南受袭农田",
                    1
                );
            EncounterAnchorData preserved =
                runtime._world_map_data_context.GetEncounterAnchorById(
                    "quest_farmers_plea_encounter"
                );

            _test.False(spawn.Ok, "活动 stable anchor 的 stage 不同时不得静默复用。");
            _test.False(spawn.Added, "stage 冲突失败不得报告已添加新锚点。");
            _test.True(
                spawn.Message.Contains("growth stage", StringComparison.Ordinal),
                $"stage 冲突应返回明确错误。message={spawn.Message}"
            );
            _test.False(preserved?.is_cleared ?? true, "拒绝后原锚点必须仍处于活动状态。");
            _test.Eq(preserved?.growth_stage ?? -1, 0, "拒绝后必须保留原活动锚点的 stage。");
            _test.Eq(
                preserved?.world_coord ?? new Vector2I(-1, -1),
                new Vector2I(4, 5),
                "拒绝 stage 冲突不得移动原活动锚点。"
            );
            _test.Eq(
                runtime._world_map_data_context.GetActiveEncounterAnchors().Count,
                1,
                "拒绝 stage 冲突不得删除或复制原活动锚点。"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestCommitFailureRollsBackPartyAndWorld()
    {
        QuestDefinition definition = BuildQuestDefinition();
        var port = new RecordingQuestCommandPort(definition)
        {
            CommitSucceeds = false,
        };
        var handler = new GameRuntimeQuestCommandHandler();
        handler.Setup(port);
        try
        {
            RuntimeCommandResult result = handler.CommandAcceptQuestTyped(
                definition.QuestId
            );

            _test.False(result.Ok, "party+world 提交失败时接取命令必须失败。");
            _test.Eq(
                string.Join(",", port.Operations),
                "capture,spawn,accept,commit,rollback",
                "提交失败必须回滚同一 party+world 事务。"
            );
        }
        finally
        {
            handler.Dispose();
        }
    }

    private void TestPlacementPrefersSouthAndIsDeterministic()
    {
        var grid = new WorldMapGridSystem();
        grid.Setup(new Vector2I(4, 4), new Vector2I(4, 4));
        Vector2I center = new(6, 6);

        bool found = QuestAcceptEncounterPlacement.TryFindAvailableCoord(
            grid,
            center,
            _ => true,
            out Vector2I first
        );
        _test.True(found, "发布者附近存在空格时应能找到任务遭遇位置。");
        _test.Eq(first, new Vector2I(6, 7), "确定性放置应优先选择发布者正南一格。");

        bool fallbackFound = QuestAcceptEncounterPlacement.TryFindAvailableCoord(
            grid,
            center,
            coord => coord != new Vector2I(6, 7),
            out Vector2I fallback
        );
        _test.True(fallbackFound, "正南被占用时应继续寻找同圈空格。");
        _test.Eq(
            fallback,
            new Vector2I(5, 7),
            "同一输入与占用集合必须得到稳定的西南候选。"
        );
    }

    private void TestWorldOwnerAddsAndIndexesEncounterAnchor()
    {
        var context = new WorldMapDataContext();
        try
        {
            context.BindRootWorldData(WorldRuntimeData.Empty());
            EncounterAnchorData anchor = BuildAnchor(
                "quest_farmers_plea_encounter",
                new Vector2I(3, 4)
            );

            _test.True(
                context.TryAddEncounterAnchor(anchor),
                "WorldMapDataContext 应通过 typed world owner 添加合法 anchor。"
            );
            _test.Eq(
                context.GetEncounterAnchorAt(new Vector2I(3, 4))?.entity_id
                    ?? new StringName(""),
                new StringName("quest_farmers_plea_encounter"),
                "添加后必须立即重建 coord lookup。"
            );
            _test.False(
                context.TryAddEncounterAnchor(
                    BuildAnchor("another_anchor", new Vector2I(3, 4))
                ),
                "同一 world coord 不得添加第二个 encounter anchor。"
            );
            _test.False(
                context.TryAddEncounterAnchor(
                    BuildAnchor("quest_farmers_plea_encounter", new Vector2I(4, 4))
                ),
                "同一 stable entity_id 不得添加第二个 encounter anchor。"
            );
        }
        finally
        {
            context.Dispose();
        }
    }

    private static QuestDefinition BuildQuestDefinition() =>
        new(
            "farmers_plea",
            "农夫的恳求",
            "测试接取遭遇绑定。",
            "npc_village_chief",
            Array.Empty<StringName>(),
            Array.Empty<QuestAcceptRequirementDefinition>(),
            new[]
            {
                new QuestObjectiveDefinition(
                    "defeat_wolves",
                    "defeat_enemy_in_single_battle",
                    "wolf_pack",
                    5,
                    "farm_wolves",
                    "村南受袭农田",
                    1
                ),
            },
            Array.Empty<QuestRewardDefinition>(),
            false,
            "npc",
            new[] { new StringName("npc_offer") },
            "",
            "",
            "",
            ""
        );

    private static EncounterAnchorData BuildAnchor(StringName anchorId, Vector2I coord) =>
        new()
        {
            entity_id = anchorId,
            display_name = "村南受袭农田",
            world_coord = coord,
            faction_id = "hostile",
            region_tag = "",
            vision_range = 1,
            is_cleared = false,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            encounter_profile_id = "farm_wolves",
            growth_stage = 0,
            suppressed_until_step = 0,
        };

    private sealed class RecordingQuestCommandPort : IGameRuntimeQuestCommandPort
    {
        private readonly QuestCommandDefData _definition;

        internal RecordingQuestCommandPort(QuestDefinition definition)
        {
            _definition = QuestCommandDefData.FromQuestDefinition(definition);
        }

        internal List<string> Operations { get; } = new();
        internal bool AcceptSucceeds { get; set; } = true;
        internal bool SpawnSucceeds { get; set; } = true;
        internal bool SpawnAsExisting { get; set; }
        internal bool CommitSucceeds { get; set; } = true;
        internal RuntimeTransaction CapturedTransaction { get; private set; }
        internal StringName SpawnProfileId { get; private set; } = "";
        internal string SpawnDisplayName { get; private set; } = "";
        internal int SpawnGrowthStage { get; private set; }
        internal StringName SpawnAnchorId { get; private set; } = "";
        internal StringName RemovedAnchorId { get; private set; } = "";

        public bool IsAvailable() => true;

        public QuestCommandDefData GetQuestCommandDefData(StringName questId) =>
            _definition;

        public QuestCommandStateData GetQuestCommandStateData(StringName questId) =>
            default;

        public int GetWorldStep() => 1;

        public string GetItemDisplayName(StringName itemId) => itemId.ToString();

        public bool AcceptQuestAndSyncParty(StringName questId, bool allowReaccept)
        {
            Operations.Add("accept");
            return AcceptSucceeds;
        }

        public RuntimeTransactionRollbackState CaptureQuestAcceptRollbackState(
            RuntimeTransaction transaction
        )
        {
            Operations.Add("capture");
            CapturedTransaction = transaction;
            return null;
        }

        public QuestAcceptEncounterSpawnResult TryAddQuestAcceptEncounter(
            StringName questId,
            StringName encounterProfileId,
            string encounterDisplayName,
            int encounterGrowthStage
        )
        {
            Operations.Add("spawn");
            SpawnProfileId = encounterProfileId;
            SpawnDisplayName = encounterDisplayName;
            SpawnGrowthStage = encounterGrowthStage;
            SpawnAnchorId = QuestAcceptEncounterPlacement.BuildStableAnchorId(questId);
            if (!SpawnSucceeds)
                return QuestAcceptEncounterSpawnResult.Failure("spawn failed");
            return SpawnAsExisting
                ? QuestAcceptEncounterSpawnResult.ExistingAnchor(SpawnAnchorId)
                : QuestAcceptEncounterSpawnResult.AddedAnchor(SpawnAnchorId);
        }

        public void RemoveQuestAcceptEncounter(StringName encounterAnchorId)
        {
            Operations.Add("remove");
            RemovedAnchorId = encounterAnchorId;
        }

        public RuntimeCommitResult CommitQuestAcceptTransaction(
            RuntimeTransaction transaction
        )
        {
            Operations.Add("commit");
            return CommitSucceeds
                ? new RuntimeCommitResult()
                : new RuntimeCommitResult { CommitError = (int)Error.Failed };
        }

        public void RollbackQuestAcceptTransaction(
            RuntimeTransaction transaction,
            RuntimeTransactionRollbackState rollbackState
        )
        {
            Operations.Add("rollback");
        }

        public QuestProgressApplyResultData ApplyDirectQuestProgressAndSyncParty(
            StringName questId,
            StringName objectiveId,
            int progressDelta,
            QuestProgressCommandPayloadData progressPayload
        ) => new();

        public bool CompleteQuestAndSyncParty(StringName questId) => false;

        public QuestSubmitItemResultData SubmitItemObjectiveAndSyncParty(
            StringName questId,
            StringName objectiveId
        ) => QuestSubmitItemResultData.Failed("unused");

        public QuestClaimResultData ClaimQuestRewardAndSyncParty(StringName questId) =>
            QuestClaimResultData.Failed("unused");

        public Error PersistQuestPartyState() => Error.Ok;

        public void UpdateStatus(string message) { }
    }
}
