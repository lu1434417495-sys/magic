using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class run_quest_progress_service_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestFormalProgressEventSchema();
        TestSingleBattleDefeatObjectiveRequiresOneBattleEvent();
        TestNormalDefeatObjectiveStillAccumulatesAcrossBattles();
        TestDirectRecordProgressMovesCompletedQuestToClaimable();
        TestAuthoredFailurePolicyProjection();
        TestFailureTransitionsAndRestartPolicy();
        TestStringKeyOnlyQuestDefsAreRejected();
        TestMissingObjectiveTargetValueDoesNotDefaultToOne();
        TestAcceptEventRejectsNegativeWorldStep();

        RequestTestExit(_test.Finish("Quest progress service regression"));
    }

    private void TestFormalProgressEventSchema()
    {
        QuestDef questDef = BuildQuestDef(
            "contract_formal_progress_event",
            "正式进度事件",
            "train_once",
            QuestDef.ToStringName(QuestObjectiveKind.SettlementAction),
            "service:training",
            2
        );
        PartyState partyState = new();
        CharacterManagementModule manager = BuildManager(partyState, questDef);

        _test.True(manager.AcceptQuest(questDef.quest_id, 1), "测试任务应可被正式接取。");
        QuestState activeQuest = partyState.GetActiveQuestState(questDef.quest_id);
        _test.True(activeQuest != null, "接取后应存在 active quest。");
        if (activeQuest == null)
            return;

        int badEventIndex = 0;
        foreach (GDictionary badEvent in BuildBadProgressEvents(questDef.quest_id))
        {
            badEventIndex++;
            GDictionary summary = QuestProgressResultProjection.Project(
                manager.ApplyQuestProgressEventsTyped(new[] { QuestProgressEvent(badEvent) })
            );
            _test.Eq(
                SummaryCount(summary, "progressed_quest_ids"),
                0,
                $"坏 quest progress event #{badEventIndex} 不应推进任务：{Json.Stringify(badEvent)}"
            );
        }
        _test.Eq(
            partyState.GetActiveQuestState(questDef.quest_id)?.GetObjectiveProgress(
                "train_once"
            ) ?? -1,
            0,
            "amount / 缺 event_type / 字符串字段 / 缺 world_step / 负 world_step 不应被兼容成任务进度。"
        );
        _test.True(!partyState.HasClaimableQuest(questDef.quest_id), "坏 progress event 不应把任务推进到 claimable。");

        GDictionary formalSummary = QuestProgressResultProjection.Project(
            manager.ApplyQuestProgressEventsTyped(
                new[]
                {
                    QuestProgressEvent(
                        new GDictionary
                        {
                            ["event_type"] = "progress",
                            ["quest_id"] = questDef.quest_id.ToString(),
                            ["objective_id"] = "train_once",
                            ["progress_delta"] = 1,
                            ["world_step"] = 3,
                        }
                    ),
                }
            )
        );
        _test.Eq(SummaryCount(formalSummary, "progressed_quest_ids"), 1, "正式 progress_delta 应能推进任务。");
        _test.Eq(
            partyState.GetActiveQuestState(questDef.quest_id)?.GetObjectiveProgress(
                "train_once"
            ) ?? -1,
            1,
            "直接 quest progress event 应从 QuestDef 读取 target_value。"
        );
        _test.True(!partyState.HasClaimableQuest(questDef.quest_id), "未达到 QuestDef target_value 前不应完成。");

        GDictionary matchedSummary = QuestProgressResultProjection.Project(
            manager.ApplyQuestProgressEventsTyped(
                new[]
                {
                    QuestProgressEvent(
                        new GDictionary
                        {
                            ["event_type"] = "progress",
                            ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.SettlementAction).ToString(),
                            ["target_id"] = "service:training",
                            ["progress_delta"] = 1,
                            ["world_step"] = 4,
                        }
                    ),
                }
            )
        );
        _test.Eq(SummaryCount(matchedSummary, "progressed_quest_ids"), 1, "按 objective_type/target_id 匹配的正式事件应推进任务。");
        _test.True(partyState.HasClaimableQuest(questDef.quest_id), "达到正式 objective target_value 后任务应进入 claimable。");
    }

    private void TestSingleBattleDefeatObjectiveRequiresOneBattleEvent()
    {
        QuestDef questDef = BuildQuestDef(
            "folk_single_battle_wolves",
            "单场荒狼",
            "defeat_wolves",
            QuestDef.ToStringName(QuestObjectiveKind.DefeatEnemyInSingleBattle),
            "wolf_pack",
            5
        );
        QuestDefinition questDefinition = TestProgressionDefinitionProjection.Quest(questDef);
        PartyState partyState = new();
        QuestProgressService service = BuildQuestProgressService(
            partyState,
            questDefinition
        );

        _test.True(service.AcceptQuest(questDefinition.QuestId, 1), "单场击败任务应可接取。");
        _test.False(
            service.RecordProgress(questDefinition.QuestId, "defeat_wolves", 5),
            "RecordProgress 直接入口不得绕过单场战斗事件约束。"
        );

        service.ApplyQuestProgressEventsTyped(
            new[] { BuildWolfDefeatProgressEvent(5, 2, "") }
        );
        _test.Eq(
            partyState
                .GetActiveQuestState(questDefinition.QuestId)
                ?.GetObjectiveProgress("defeat_wolves") ?? -1,
            0,
            "缺少 encounter_id 的击败事件不得推进单场战斗目标。"
        );

        service.ApplyQuestProgressEventsTyped(
            new[]
            {
                BuildWolfDefeatProgressEvent(
                    5,
                    3,
                    "wrong_event_type_battle",
                    QuestDef.ToStringName(QuestObjectiveKind.DefeatEnemyInSingleBattle)
                ),
            }
        );
        _test.Eq(
            partyState
                .GetActiveQuestState(questDefinition.QuestId)
                ?.GetObjectiveProgress("defeat_wolves") ?? -1,
            0,
            "单场目标只应匹配现有 defeat_enemy 聚合事件，不匹配同名 objective type 事件。"
        );

        service.ApplyQuestProgressEventsTyped(
            new[] { BuildWolfDefeatProgressEvent(2, 4, "wolf_battle_a") }
        );
        service.ApplyQuestProgressEventsTyped(
            new[] { BuildWolfDefeatProgressEvent(3, 5, "wolf_battle_b") }
        );
        _test.Eq(
            partyState
                .GetActiveQuestState(questDefinition.QuestId)
                ?.GetObjectiveProgress("defeat_wolves") ?? -1,
            0,
            "两场分别击败 2 和 3 只荒狼不得跨战累计为 5。"
        );
        _test.False(
            partyState.HasClaimableQuest(questDefinition.QuestId),
            "未在单场达到 5 只前任务不得进入 claimable。"
        );

        service.ApplyQuestProgressEventsTyped(
            new[]
            {
                QuestProgressEvent(
                    new GDictionary
                    {
                        ["event_type"] = "progress",
                        ["objective_type"] = QuestDef
                            .ToStringName(QuestObjectiveKind.DefeatEnemy)
                            .ToString(),
                        ["target_id"] = "wolf_pack",
                        ["progress_delta"] = 1,
                        ["target_value"] = 1,
                        ["world_step"] = 6,
                        ["encounter_id"] = "wolf_override_battle",
                        ["encounter_kind"] = "single",
                    }
                ),
            }
        );
        _test.Eq(
            partyState
                .GetActiveQuestState(questDefinition.QuestId)
                ?.GetObjectiveProgress("defeat_wolves") ?? -1,
            0,
            "事件携带的较小 target_value 不得覆盖单场任务正式配置的 5 只。"
        );

        service.ApplyQuestProgressEventsTyped(
            new[] { BuildWolfDefeatProgressEvent(5, 7, "wolf_battle_c") }
        );
        _test.True(
            partyState.HasClaimableQuest(questDefinition.QuestId),
            "单个正式战斗事件击败 5 只荒狼时任务应进入 claimable。"
        );
        _test.Eq(
            partyState
                .GetClaimableQuestState(questDefinition.QuestId)
                ?.GetObjectiveProgress("defeat_wolves") ?? -1,
            5,
            "满足单场目标时应一次写满正式 target_value。"
        );
    }

    private void TestNormalDefeatObjectiveStillAccumulatesAcrossBattles()
    {
        QuestDef questDef = BuildQuestDef(
            "bounty_accumulating_wolves",
            "累计荒狼",
            "defeat_wolves",
            QuestDef.ToStringName(QuestObjectiveKind.DefeatEnemy),
            "wolf_pack",
            5
        );
        QuestDefinition questDefinition = TestProgressionDefinitionProjection.Quest(questDef);
        PartyState partyState = new();
        QuestProgressService service = BuildQuestProgressService(
            partyState,
            questDefinition
        );

        _test.True(service.AcceptQuest(questDefinition.QuestId, 1), "普通击败任务应可接取。");
        service.ApplyQuestProgressEventsTyped(
            new[] { BuildWolfDefeatProgressEvent(2, 2, "normal_wolf_battle_a") }
        );
        _test.Eq(
            partyState
                .GetActiveQuestState(questDefinition.QuestId)
                ?.GetObjectiveProgress("defeat_wolves") ?? -1,
            2,
            "普通 defeat_enemy 目标仍应记录第一场的击败数量。"
        );

        service.ApplyQuestProgressEventsTyped(
            new[] { BuildWolfDefeatProgressEvent(3, 3, "normal_wolf_battle_b") }
        );
        _test.True(
            partyState.HasClaimableQuest(questDefinition.QuestId),
            "普通 defeat_enemy 目标应保持跨战累计行为。"
        );
        _test.Eq(
            partyState
                .GetClaimableQuestState(questDefinition.QuestId)
                ?.GetObjectiveProgress("defeat_wolves") ?? -1,
            5,
            "普通 defeat_enemy 的 2+3 应累计至正式 target_value。"
        );
    }

    private void TestDirectRecordProgressMovesCompletedQuestToClaimable()
    {
        QuestDefinition questDef = new(
            "contract_direct_progress",
            "直接进度入口",
            "",
            "service_contract_board",
            System.Array.Empty<StringName>(),
            System.Array.Empty<QuestAcceptRequirementDefinition>(),
            new[]
            {
                new QuestObjectiveDefinition(
                    "train_once",
                    QuestDef.ToStringName(QuestObjectiveKind.SettlementAction),
                    "service:training",
                    1
                ),
            },
            System.Array.Empty<QuestRewardDefinition>(),
            false,
            "service_contract_board",
            new[] { new StringName("contract_board") },
            "",
            "",
            "",
            ""
        );
        PartyState partyState = new();
        QuestProgressService service = new();
        service.Setup(
            partyState,
            new Dictionary<StringName, QuestDefinition>
            {
                [questDef.QuestId] = questDef,
            }
        );

        _test.True(service.AcceptQuest(questDef.QuestId, 1), "直接进度回归应先接取任务。");
        _test.True(
            service.RecordProgress(questDef.QuestId, "train_once", 1),
            "直接进度入口达到目标时应成功完成迁移。"
        );
        _test.True(
            !partyState.HasActiveQuest(questDef.QuestId),
            "直接进度完成后不能把 completed QuestState 留在 active_quests。"
        );
        _test.True(
            partyState.HasClaimableQuest(questDef.QuestId),
            "直接进度完成后应把任务迁入 claimable_quests。"
        );
        _test.Eq(
            partyState.GetClaimableQuestState(questDef.QuestId)?.status_id ?? new StringName(""),
            QuestState.ToStringName(QuestStatusKind.Completed),
            "claimable 任务应保留 completed 状态。"
        );
    }

    private void TestFailureTransitionsAndRestartPolicy()
    {
        QuestDefinition terminalQuest = BuildFailureQuestDefinition(
            "contract_terminal_failure",
            false
        );
        QuestDefinition restartableQuest = BuildFailureQuestDefinition(
            "contract_restartable_failure",
            true
        );
        PartyState partyState = new();
        QuestProgressService service = new();
        service.Setup(
            partyState,
            new Dictionary<StringName, QuestDefinition>
            {
                [terminalQuest.QuestId] = terminalQuest,
                [restartableQuest.QuestId] = restartableQuest,
            }
        );

        _test.True(service.AcceptQuest(terminalQuest.QuestId, 1), "terminal 失败任务应可初次接取。");
        _test.True(
            service.FailQuest(
                new QuestFailureRequest(
                    terminalQuest.QuestId,
                    5,
                    "protected_target_lost"
                )
            ),
            "active terminal 任务应能原子迁移到 failed。"
        );
        _test.False(partyState.HasActiveQuest(terminalQuest.QuestId), "失败后不得残留 active。");
        _test.True(partyState.HasFailedQuest(terminalQuest.QuestId), "失败任务应进入 failed 集合。");
        _test.False(
            service.RecordProgress(terminalQuest.QuestId, "survive", 1, 1),
            "failed 任务不得继续推进。"
        );
        _test.False(service.CompleteQuest(terminalQuest.QuestId, 6), "failed 任务不得完成。");
        _test.False(service.ClaimReward(terminalQuest.QuestId), "failed 任务不得领奖。");
        _test.False(
            service.AcceptQuest(terminalQuest.QuestId, 7, true),
            "terminal failure 不得借 allow_reaccept 绕过失败策略。"
        );

        _test.True(
            service.AcceptQuest(restartableQuest.QuestId, 10),
            "restartable 失败任务应可初次接取。"
        );
        _test.True(
            service.RecordProgress(restartableQuest.QuestId, "survive", 1, 2),
            "失败前应能记录目标进度。"
        );
        _test.True(
            service.FailQuest(
                new QuestFailureRequest(
                    restartableQuest.QuestId,
                    12,
                    "deadline_expired"
                )
            ),
            "restartable 任务应能进入 failed。"
        );
        _test.True(
            service.AcceptQuest(restartableQuest.QuestId, 15),
            "restartable failure 应独立于 is_repeatable 允许重新接取。"
        );
        _test.False(
            partyState.HasFailedQuest(restartableQuest.QuestId),
            "重新接取后旧 failed 状态应被原子移除。"
        );
        QuestState restarted = partyState.GetActiveQuestState(restartableQuest.QuestId);
        _test.True(restarted != null && restarted.IsActive(), "重新接取后应创建新的 active 状态。");
        if (restarted != null)
        {
            _test.Eq(restarted.GetObjectiveProgress("survive"), 0, "重新接取必须清空旧目标进度。");
            _test.Eq(restarted.accepted_at_world_step, 15, "重新接取应记录新的接取时间。");
            _test.Eq(restarted.failed_at_world_step, -1, "新 active 状态不得继承失败时间。");
            _test.Eq(
                restarted.failure_reason_id,
                new StringName(""),
                "新 active 状态不得继承失败原因。"
            );
        }
    }

    private void TestAuthoredFailurePolicyProjection()
    {
        QuestDef questDef = BuildQuestDef(
            "contract_authored_restartable_failure",
            "可重启失败策略",
            "survive",
            QuestDef.ToStringName(QuestObjectiveKind.SettlementAction),
            "service:survive",
            2
        );
        questDef.failure_policy = "restartable";

        QuestDefinition definition = TestProgressionDefinitionProjection.Quest(questDef);
        _test.True(
            definition.CanRestartAfterFailure,
            "Authored restartable failure_policy must project to the runtime semantic flag."
        );
    }

    private static QuestDefinition BuildFailureQuestDefinition(
        StringName questId,
        bool canRestartAfterFailure
    )
    {
        return new QuestDefinition(
            questId,
            questId.ToString(),
            "",
            "service_contract_board",
            System.Array.Empty<StringName>(),
            System.Array.Empty<QuestAcceptRequirementDefinition>(),
            new[]
            {
                new QuestObjectiveDefinition(
                    "survive",
                    QuestDef.ToStringName(QuestObjectiveKind.SettlementAction),
                    "service:survive",
                    2
                ),
            },
            System.Array.Empty<QuestRewardDefinition>(),
            false,
            "service_contract_board",
            new[] { new StringName("contract_board") },
            "",
            "",
            "",
            "",
            canRestartAfterFailure: canRestartAfterFailure
        );
    }

    private void TestStringKeyOnlyQuestDefsAreRejected()
    {
        QuestDef questDef = BuildQuestDef(
            "contract_string_key_progress",
            "旧 String key 进度",
            "train_once",
            QuestDef.ToStringName(QuestObjectiveKind.SettlementAction),
            "service:training",
            1
        );
        PartyState partyState = new();
        GDictionary questDefs = new();
        questDefs[questDef.quest_id.ToString()] = questDef;
        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, ProfessionDefinition>(),
            new Dictionary<StringName, AchievementDefinition>(),
            new Dictionary<StringName, ItemDefinition>(),
            ProjectQuestDefs(questDefs),
            true,
            null,
            null
        );

        _test.True(
            !manager.AcceptQuest(questDef.quest_id, 1),
            "String key-only quest_def 不应被 QuestProgressService 恢复。"
        );
        _test.True(
            !partyState.HasActiveQuest(questDef.quest_id),
            "String key-only quest_def accept 失败后不应写入 active_quests。"
        );
    }

    private void TestMissingObjectiveTargetValueDoesNotDefaultToOne()
    {
        QuestDefinition questDef = new(
            "contract_missing_target_value",
            "缺目标值",
            "",
            "service_contract_board",
            System.Array.Empty<StringName>(),
            System.Array.Empty<QuestAcceptRequirementDefinition>(),
            new[]
            {
                new QuestObjectiveDefinition(
                    "bad_target",
                    QuestDef.ToStringName(QuestObjectiveKind.SettlementAction),
                    "service:bad",
                    0
                ),
            },
            System.Array.Empty<QuestRewardDefinition>(),
            false,
            "service_contract_board",
            new[] { new StringName("contract_board") },
            "",
            "",
            "",
            ""
        );
        PartyState partyState = new();
        CharacterManagementModule manager = BuildManager(partyState, questDef);

        _test.True(manager.AcceptQuest(questDef.QuestId, 5), "缺 target_value 的坏夹具仍可用于验证 service 拒绝进度事件。");
        manager.ApplyQuestProgressEventsTyped(
            new[]
            {
                QuestProgressEvent(
                    new GDictionary
                    {
                        ["event_type"] = "progress",
                        ["quest_id"] = questDef.QuestId.ToString(),
                        ["objective_id"] = "bad_target",
                        ["progress_delta"] = 1,
                        ["world_step"] = 6,
                    }
                ),
            }
        );
        QuestState questState = partyState.GetActiveQuestState(questDef.QuestId);
        _test.True(questState != null, "缺 target_value 任务应保持 active。");
        if (questState != null)
            _test.Eq(questState.GetObjectiveProgress("bad_target"), 0, "缺正式 target_value 时不应按默认 1 推进任务。");
    }

    private void TestAcceptEventRejectsNegativeWorldStep()
    {
        QuestDef questDef = BuildQuestDef(
            "contract_negative_step_accept",
            "负时间戳接取事件",
            "train_once",
            QuestDef.ToStringName(QuestObjectiveKind.SettlementAction),
            "service:training",
            1
        );
        PartyState partyState = new();
        CharacterManagementModule manager = BuildManager(partyState, questDef);

        foreach (int badWorldStep in new[] { -1, -5 })
        {
            GDictionary summary = QuestProgressResultProjection.Project(
                manager.ApplyQuestProgressEventsTyped(
                    new[]
                    {
                        QuestProgressEvent(
                            new GDictionary
                            {
                                ["event_type"] = "accept",
                                ["quest_id"] = questDef.quest_id.ToString(),
                                ["world_step"] = badWorldStep,
                            }
                        ),
                    }
                )
            );
            _test.Eq(
                SummaryCount(summary, "accepted_quest_ids"),
                0,
                $"负 world_step accept 事件不应接取任务：world_step={badWorldStep}"
            );
        }
        _test.True(
            !partyState.HasActiveQuest(questDef.quest_id),
            "负 world_step accept 事件不应写入 active_quests（避免坏时间戳进入存档）。"
        );

        GDictionary formalSummary = QuestProgressResultProjection.Project(
            manager.ApplyQuestProgressEventsTyped(
                new[]
                {
                    QuestProgressEvent(
                        new GDictionary
                        {
                            ["event_type"] = "accept",
                            ["quest_id"] = questDef.quest_id.ToString(),
                            ["world_step"] = 2,
                        }
                    ),
                }
            )
        );
        _test.Eq(SummaryCount(formalSummary, "accepted_quest_ids"), 1, "正式 accept 事件应能接取任务。");
        _test.Eq(
            partyState.GetActiveQuestState(questDef.quest_id)?.accepted_at_world_step ?? -1,
            2,
            "正式 accept 事件应记录接取时间。"
        );
    }

    private static CharacterManagementModule BuildManager(PartyState partyState, QuestDef questDef)
    {
        return BuildManager(
            partyState,
            TestProgressionDefinitionProjection.Quest(questDef)
        );
    }

    private static CharacterManagementModule BuildManager(
        PartyState partyState,
        QuestDefinition questDef
    )
    {
        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, ProfessionDefinition>(),
            new Dictionary<StringName, AchievementDefinition>(),
            new Dictionary<StringName, ItemDefinition>(),
            new Dictionary<StringName, QuestDefinition>
            {
                [questDef.QuestId] = questDef,
            }
        );
        return manager;
    }

    private static QuestProgressService BuildQuestProgressService(
        PartyState partyState,
        QuestDefinition questDefinition
    )
    {
        QuestProgressService service = new();
        service.Setup(
            partyState,
            new Dictionary<StringName, QuestDefinition>
            {
                [questDefinition.QuestId] = questDefinition,
            }
        );
        return service;
    }

    private static QuestProgressService.QuestProgressEventData BuildWolfDefeatProgressEvent(
        int progressDelta,
        int worldStep,
        StringName encounterId
    ) =>
        BuildWolfDefeatProgressEvent(
            progressDelta,
            worldStep,
            encounterId,
            QuestDef.ToStringName(QuestObjectiveKind.DefeatEnemy)
        );

    private static QuestProgressService.QuestProgressEventData BuildWolfDefeatProgressEvent(
        int progressDelta,
        int worldStep,
        StringName encounterId,
        StringName objectiveType
    ) =>
        QuestProgressService.QuestProgressEventData.CreateProgressByObjectiveTarget(
            objectiveType,
            "wolf_pack",
            progressDelta,
            worldStep,
            "wolf_pack",
            encounterId,
            "single"
        );

    private static Dictionary<StringName, QuestDefinition> ProjectQuestDefs(
        GDictionary questDefs
    )
    {
        Dictionary<StringName, QuestDefinition> result = new();
        if (questDefs == null)
            return result;
        foreach (Variant rawKey in questDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName questId = rawKey.AsStringName();
            if (questId == "")
                continue;
            if (questDefs[rawKey].AsGodotObject() is QuestDef questDef)
                result[questId] = TestProgressionDefinitionProjection.Quest(questDef);
        }
        return result;
    }

    private static QuestProgressService.QuestProgressEventData QuestProgressEvent(
        GDictionary eventData
    ) => QuestProgressService.QuestProgressEventData.FromDictionary(eventData);

    private static QuestDef BuildQuestDef(
        string questId,
        string displayName,
        string objectiveId,
        StringName objectiveType,
        string targetId,
        int targetValue
    )
    {
        QuestDef questDef = new()
        {
            quest_id = questId,
            display_name = displayName,
            provider_kind = "service_contract_board",
            provider_interaction_id = "service_contract_board",
            listing_channels = new Godot.Collections.Array<StringName> { "contract_board" },
            failure_policy = "terminal",
        };
        GDictionary objectiveDef = new()
        {
            ["objective_id"] = objectiveId,
            ["objective_type"] = objectiveType,
            ["target_id"] = targetId,
            ["target_value"] = targetValue,
        };
        questDef.objective_defs.Add(objectiveDef);
        return questDef;
    }

    private static GArray BuildBadProgressEvents(StringName questId) =>
        new()
        {
            new GDictionary
            {
                ["event_type"] = "progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["world_step"] = 2,
                ["amount"] = 1,
            },
            new GDictionary
            {
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = 1,
                ["world_step"] = 2,
            },
            new GDictionary
            {
                ["event_type"] = "progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = "1",
                ["world_step"] = 2,
            },
            new GDictionary
            {
                ["event_type"] = "progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = 1,
                ["world_step"] = 2,
                ["target_value"] = "2",
            },
            new GDictionary
            {
                ["event_type"] = "progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = 1,
            },
            new GDictionary
            {
                ["event_type"] = "progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = 1,
                ["world_step"] = "2",
            },
            new GDictionary
            {
                ["event_type"] = "legacy_progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = 1,
                ["world_step"] = 2,
            },
            new GDictionary
            {
                ["event_type"] = "progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = 1,
                ["world_step"] = -1,
            },
            new GDictionary
            {
                ["event_type"] = "progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = 1,
                ["world_step"] = -5,
            },
        };

    private static int SummaryCount(GDictionary summary, string key)
    {
        if (summary == null || !summary.ContainsKey(key))
            return 0;
        Variant value = summary[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray().Count : 0;
    }


}
