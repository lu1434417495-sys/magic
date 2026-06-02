using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_game_runtime_snapshot_builder_regression : SceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        TestSnapshotBuilderMatchesFacadeOutputs();
        TestTextSnapshotRedactsHostLogPaths();
        TestSnapshotBuilderExposesPartyQuestSnapshot();
        TestSnapshotBuilderExposesMemberProgressionSnapshot();
        TestSnapshotBuilderRejectsLegacyQuestContainerShapes();
        TestTextSnapshotRequiresExplicitQuestStageId();
        TestTextSnapshotRejectsLegacyWindowAndReportFields();
        TestSnapshotBuilderCrossReferencesQuestItemsInTextSnapshot();
        TestSnapshotBuilderExposesContractBoardModalSnapshot();
        TestSnapshotBuilderExposesForgeModalSnapshot();
        TestSnapshotBuilderExposesGenericForgeModalSnapshot();
        TestSnapshotBuilderRequiresPanelKindForForgeModal();
        TestSnapshotBuilderExposesGameOverSnapshot();
        TestSnapshotBuilderExposesBattleLootSnapshot();
        TestSnapshotBuilderOmitsLootSectionWhenEmpty();

        if (_failures.Count == 0)
        {
            GD.Print("Game runtime snapshot builder regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Game runtime snapshot builder regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestSnapshotBuilderMatchesFacadeOutputs()
    {
        GameSession gameSession = CreateTestSession();
        if (gameSession == null)
            return;

        var facade = new GameRuntimeFacade();
        try
        {
            facade.setup(gameSession);
            var builder = new GameRuntimeSnapshotBuilder();
            builder.Setup(facade);

            GDictionary facadeSnapshot = facade.build_headless_snapshot();
            GDictionary builderSnapshot = builder.BuildHeadlessSnapshot();
            string facadeText = facade.build_text_snapshot();
            string builderText = builder.BuildTextSnapshot();

            AssertEq(
                Json.Stringify(builderSnapshot),
                Json.Stringify(facadeSnapshot),
                "Snapshot builder 输出应与 facade.build_headless_snapshot() 保持一致。"
            );
            AssertEq(
                builderText,
                facadeText,
                "Snapshot builder 文本快照应与 facade.build_text_snapshot() 保持一致。"
            );
            AssertTrue(!string.IsNullOrEmpty(builderText), "Snapshot builder 文本快照不应为空。");
            AssertTrue(builderSnapshot.ContainsKey("logs"), "运行时快照应包含日志段。");
            GDictionary logs = Dict(builderSnapshot, "logs");
            AssertEq(StringValue(logs, "file_path"), "", "运行时快照默认不应暴露日志文件路径。");
            AssertFalse(BoolValue(logs, "file_output_enabled", true), "运行时日志文件输出默认应关闭。");
            AssertTrue(ArrayValue(logs, "entries").Count > 0, "运行时快照应包含最近日志条目。");
            AssertTrue(builderText.Contains("[LOG]"), "运行时文本快照应包含日志分段。");

            builder.Dispose();
        }
        finally
        {
            facade.dispose();
            CleanupTestSession(gameSession);
        }
    }

    private void TestTextSnapshotRedactsHostLogPaths()
    {
        string memoryTextSnapshot = GameTextSnapshotRenderer.render_full_snapshot(
            new GDictionary
            {
                ["logs"] = new GDictionary
                {
                    ["file_path"] = "",
                    ["virtual_path"] = "",
                    ["entry_count"] = 1,
                    ["buffer_limit"] = 3,
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["seq"] = 1,
                            ["level"] = "info",
                            ["domain"] = "session",
                            ["event_id"] = "session.memory_only",
                            ["message"] = "memory only",
                        },
                    },
                },
            }
        );
        AssertTrue(memoryTextSnapshot.Contains("[LOG]"), "内存日志仍应渲染 LOG 分段。");
        AssertFalse(memoryTextSnapshot.Contains("file_name="), "内存日志文本快照不应渲染文件名。");
        AssertFalse(memoryTextSnapshot.Contains("file_path="), "文本快照不应继续渲染绝对 file_path 标签。");
        AssertFalse(memoryTextSnapshot.Contains("virtual_path="), "文本快照不应继续渲染 virtual_path 标签。");

        const string filePath = "C:/tmp/magic/session_redaction.jsonl";
        const string virtualPath = "user://logs/session_redaction.jsonl";
        string fileTextSnapshot = GameTextSnapshotRenderer.render_full_snapshot(
            new GDictionary
            {
                ["logs"] = new GDictionary
                {
                    ["file_path"] = filePath,
                    ["virtual_path"] = virtualPath,
                    ["entry_count"] = 0,
                    ["buffer_limit"] = 3,
                    ["entries"] = new GArray(),
                },
            }
        );
        AssertTrue(
            fileTextSnapshot.Contains("file_name=session_redaction.jsonl"),
            "文本快照应只渲染稳定日志文件名。"
        );
        AssertFalse(fileTextSnapshot.Contains(filePath), "文本快照不应泄漏宿主绝对日志路径。");
        AssertFalse(fileTextSnapshot.Contains(virtualPath), "文本快照不应泄漏 session 级虚拟日志路径。");
    }

    private void TestSnapshotBuilderExposesPartyQuestSnapshot()
    {
        var runtime = new SnapshotTestRuntime();
        var partyState = new PartyState();
        var questState = new QuestState { quest_id = "contract_wolf_pack" };
        questState.mark_accepted(12);
        questState.record_objective_progress(
            "defeat_wolves",
            2,
            3,
            new GDictionary { ["enemy_template_id"] = "wolf_raider" }
        );
        questState.record_objective_progress(
            "defeat_wolves",
            2,
            3,
            new GDictionary { ["enemy_template_id"] = "wolf_raider" }
        );
        questState.record_objective_progress(
            "report_back",
            1,
            1,
            new GDictionary { ["settlement_id"] = "spring_village_01" }
        );
        var claimableQuest = new QuestState { quest_id = "contract_settlement_warehouse" };
        claimableQuest.mark_accepted(9);
        claimableQuest.mark_completed(15);
        partyState.active_quests = new Godot.Collections.Array<QuestState> { questState };
        partyState.claimable_quests = new Godot.Collections.Array<QuestState> { claimableQuest };
        partyState.completed_quest_ids = new GStringNameArray { "contract_intro" };
        runtime.PartyState = partyState;

        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        GDictionary snapshot = builder.BuildHeadlessSnapshot();
        string textSnapshot = builder.BuildTextSnapshot();
        builder.Dispose();

        GDictionary questsSnapshot = Dict(Dict(snapshot, "party"), "quests");
        AssertFalse(
            BoolValue(Dict(snapshot, "world"), "player_visible_on_map", true),
            "快照应暴露世界地图人物显隐状态。"
        );
        AssertTrue(
            textSnapshot.Contains("player_visible_on_map=false"),
            "文本快照应渲染世界地图人物显隐状态。"
        );
        AssertTrue(questsSnapshot.Count > 0, "当 PartyState 暴露 quest schema 时，headless snapshot 应在 party 段包含 quests。");
        AssertStringListEq(
            StringList(ArrayValue(questsSnapshot, "active_quest_ids")),
            new[] { "contract_wolf_pack" },
            "active_quest_ids 应稳定暴露当前激活任务 ID。"
        );
        AssertStringListEq(
            StringList(ArrayValue(questsSnapshot, "claimable_quest_ids")),
            new[] { "contract_settlement_warehouse" },
            "claimable_quest_ids 应稳定暴露待领奖励任务 ID。"
        );
        AssertStringListEq(
            StringList(ArrayValue(questsSnapshot, "completed_quest_ids")),
            new[] { "contract_intro" },
            "completed_quest_ids 应稳定暴露已完成任务 ID。"
        );

        GArray activeQuests = ArrayValue(questsSnapshot, "active_quests");
        GArray claimableQuests = ArrayValue(questsSnapshot, "claimable_quests");
        AssertEq(activeQuests.Count, 1, "active_quests 应保留当前任务详情。");
        AssertEq(claimableQuests.Count, 1, "claimable_quests 应保留待领奖励任务详情。");
        if (activeQuests.Count > 0)
        {
            GDictionary questEntry = activeQuests[0].AsGodotDictionary();
            AssertEq(StringValue(questEntry, "quest_id"), "contract_wolf_pack", "任务快照应保留 quest_id。");
            AssertEq(StringValue(questEntry, "stage_id"), "active", "激活任务快照应标记 active stage。");
            AssertEq(IntValue(questEntry, "accepted_at_world_step", -1), 12, "任务快照应保留接取时间。");
            AssertEq(
                IntValue(Dict(questEntry, "objective_progress"), "defeat_wolves", 0),
                3,
                "任务快照应保留封顶后的目标进度。"
            );
            AssertEq(
                StringValue(Dict(questEntry, "last_progress_context"), "settlement_id"),
                "spring_village_01",
                "任务快照应保留最近进度上下文。"
            );
        }
        if (claimableQuests.Count > 0)
        {
            GDictionary claimableEntry = claimableQuests[0].AsGodotDictionary();
            AssertEq(
                StringValue(claimableEntry, "quest_id"),
                "contract_settlement_warehouse",
                "待领奖励任务快照应保留 quest_id。"
            );
            AssertEq(StringValue(claimableEntry, "stage_id"), "claimable", "待领奖励任务快照应标记 claimable stage。");
            AssertEq(IntValue(claimableEntry, "completed_at_world_step", -1), 15, "待领奖励任务快照应保留完成时间。");
        }
        AssertTrue(textSnapshot.Contains("[QUEST]"), "文本快照应在 PartyState 含任务时渲染 QUEST 分段。");
        AssertTrue(textSnapshot.Contains("active_quest_ids=contract_wolf_pack"), "文本快照应渲染激活任务 ID。");
        AssertTrue(
            textSnapshot.Contains("claimable_quest_ids=contract_settlement_warehouse"),
            "文本快照应渲染待领奖励任务 ID。"
        );
        AssertTrue(textSnapshot.Contains("completed_quest_ids=contract_intro"), "文本快照应渲染完成任务 ID。");
        AssertTrue(textSnapshot.Contains("quest=contract_wolf_pack | stage=active"), "文本快照应渲染激活任务明细。");
        AssertTrue(
            textSnapshot.Contains("quest=contract_settlement_warehouse | stage=claimable"),
            "文本快照应渲染待领奖励任务明细。"
        );
    }

    private void TestSnapshotBuilderExposesMemberProgressionSnapshot()
    {
        var partyState = new PartyState
        {
            leader_member_id = "player_sword_01",
            active_member_ids = new GStringNameArray { "player_sword_01" },
        };

        var memberState = new PartyMemberState
        {
            member_id = "player_sword_01",
            display_name = "剑士",
            current_aura = 2,
        };
        memberState.progression.unit_id = memberState.member_id;
        memberState.progression.unlocked_combat_resource_ids = new GStringNameArray
        {
            "hp",
            "stamina",
            "mp",
            "aura",
        };
        memberState.progression.active_level_trigger_core_skill_id = "warrior_heavy_strike";
        memberState.progression.locked_level_trigger_skill_ids = new GStringNameArray { "mage_blink" };
        memberState.progression.blocked_relearn_skill_ids = new GStringNameArray { "old_focus" };

        var coreSkill = new UnitSkillProgress
        {
            skill_id = "warrior_heavy_strike",
            is_learned = true,
            skill_level = 3,
            is_core = true,
            assigned_profession_id = "warrior",
            is_level_trigger_active = true,
        };
        memberState.progression.set_skill_progress(coreSkill);

        var lockedSkill = new UnitSkillProgress
        {
            skill_id = "mage_blink",
            is_learned = true,
            skill_level = 1,
            is_level_trigger_locked = true,
            core_max_growth_claimed = true,
        };
        memberState.progression.set_skill_progress(lockedSkill);

        var profession = new UnitProfessionProgress
        {
            profession_id = "warrior",
            rank = 2,
            is_active = true,
            core_skill_ids = new GStringNameArray { "warrior_heavy_strike" },
            granted_skill_ids = new GStringNameArray { "warrior_guard_break" },
        };
        memberState.progression.set_profession_progress(profession);
        partyState.set_member_state(memberState);

        var runtime = new SnapshotTestRuntime { PartyState = partyState };
        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        GDictionary snapshot = builder.BuildHeadlessSnapshot();
        string textSnapshot = builder.BuildTextSnapshot();
        builder.Dispose();

        GDictionary memberSnapshot = FindMemberSnapshot(snapshot, "player_sword_01");
        AssertTrue(memberSnapshot.Count > 0, "member progression 回归前置：应能找到主角成员快照。");
        if (memberSnapshot.Count == 0)
            return;

        AssertStringListEq(
            StringList(ArrayValue(memberSnapshot, "unlocked_combat_resource_ids")),
            new[] { "aura", "hp", "mp", "stamina" },
            "成员快照应稳定暴露已解锁战斗资源。"
        );
        AssertStringListEq(
            StringList(ArrayValue(memberSnapshot, "active_core_skill_ids")),
            new[] { "warrior_heavy_strike" },
            "成员快照应暴露激活核心技能列表。"
        );
        AssertEq(
            StringValue(memberSnapshot, "active_level_trigger_core_skill_id"),
            "warrior_heavy_strike",
            "成员快照应暴露 active level trigger。"
        );
        AssertStringListEq(
            StringList(ArrayValue(memberSnapshot, "locked_level_trigger_skill_ids")),
            new[] { "mage_blink" },
            "成员快照应暴露 locked trigger 技能。"
        );
        AssertStringListEq(
            StringList(ArrayValue(memberSnapshot, "blocked_relearn_skill_ids")),
            new[] { "old_focus" },
            "成员快照应暴露 blocked relearn 技能。"
        );
        AssertEq(ArrayValue(memberSnapshot, "skill_entries").Count, 2, "成员快照应暴露 learned skill 详情。");
        AssertEq(ArrayValue(memberSnapshot, "profession_entries").Count, 1, "成员快照应暴露 profession 详情。");
        AssertTrue(textSnapshot.Contains("member_skill=player_sword_01 | warrior_heavy_strike | lv=3"), "文本快照应渲染技能等级。");
        AssertTrue(textSnapshot.Contains("member_profession=player_sword_01 | warrior | rank=2"), "文本快照应渲染职业 rank。");
    }

    private void TestSnapshotBuilderRejectsLegacyQuestContainerShapes()
    {
        Type partyReturnType = typeof(IGameRuntimeSnapshotSource)
            .GetMethod(nameof(IGameRuntimeSnapshotSource.get_party_state), BindingFlags.Public | BindingFlags.Instance)
            ?.ReturnType;
        AssertEq(
            partyReturnType,
            typeof(PartyState),
            "Snapshot source 不应再允许 object/Variant party_state 形态进入 snapshot builder。"
        );

        var partyState = new PartyState();
        var missingIdQuest = new QuestState();
        missingIdQuest.mark_accepted(1);
        partyState.active_quests = new Godot.Collections.Array<QuestState> { missingIdQuest };
        GDictionary questSnapshot = BuildPartyQuestSnapshot(partyState);

        AssertEq(ArrayValue(questSnapshot, "active_quest_ids").Count, 0, "缺 quest_id 的任务不应进入 active_quest_ids。");
        AssertEq(ArrayValue(questSnapshot, "active_quests").Count, 0, "缺必需字段的任务条目不应进入任务明细。");
    }

    private void TestTextSnapshotRequiresExplicitQuestStageId()
    {
        var snapshot = new GDictionary
        {
            ["party"] = new GDictionary
            {
                ["quests"] = new GDictionary
                {
                    ["active_quest_ids"] = new GArray
                    {
                        "contract_missing_stage",
                        "contract_numeric_stage",
                        "contract_empty_stage",
                    },
                    ["claimable_quest_ids"] = new GArray { "contract_valid_stage" },
                    ["completed_quest_ids"] = new GArray(),
                    ["active_quests"] = new GArray
                    {
                        BuildQuestSnapshotPayload("contract_missing_stage", null),
                        BuildQuestSnapshotPayload("contract_numeric_stage", 1),
                        BuildQuestSnapshotPayload("contract_empty_stage", ""),
                    },
                    ["claimable_quests"] = new GArray
                    {
                        BuildQuestSnapshotPayload("contract_valid_stage", "claimable", "completed", 2),
                    },
                },
            },
        };
        string textSnapshot = GameTextSnapshotRenderer.render_full_snapshot(snapshot);

        AssertTrue(
            textSnapshot.Contains("active_quest_ids=contract_missing_stage contract_numeric_stage contract_empty_stage"),
            "文本快照应保留 quest ID 汇总。"
        );
        AssertTrue(textSnapshot.Contains("quest=contract_valid_stage | stage=claimable"), "文本快照应渲染显式 stage_id 的任务明细。");
        AssertFalse(textSnapshot.Contains("quest=contract_missing_stage"), "缺 stage_id 的任务明细不应按 active 兜底渲染。");
        AssertFalse(textSnapshot.Contains("quest=contract_numeric_stage"), "非字符串 stage_id 的任务明细不应渲染。");
        AssertFalse(textSnapshot.Contains("quest=contract_empty_stage"), "空 stage_id 的任务明细不应渲染。");
    }

    private void TestTextSnapshotRejectsLegacyWindowAndReportFields()
    {
        var snapshot = new GDictionary
        {
            ["shop"] = new GDictionary
            {
                ["visible"] = true,
                ["window_data"] = new GDictionary
                {
                    ["title"] = "旧商店字段",
                    ["settlement_id"] = "settlement_legacy_schema",
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["entry_id"] = "old_shop_entry_id",
                            ["state_label"] = "状态：旧字段",
                            ["cost_label"] = "价格：旧字段",
                        },
                    },
                },
            },
            ["contract_board"] = new GDictionary
            {
                ["visible"] = true,
                ["window_data"] = new GDictionary
                {
                    ["title"] = "旧任务板字段",
                    ["settlement_id"] = "settlement_legacy_schema",
                    ["interaction_script_id"] = "old_contract_provider",
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["entry_id"] = "old_contract_entry_id",
                            ["quest_id"] = "contract_current_id",
                            ["state_label"] = "状态：旧字段",
                            ["cost_label"] = "奖励：旧字段",
                        },
                    },
                },
            },
            ["stagecoach"] = new GDictionary
            {
                ["visible"] = true,
                ["window_data"] = new GDictionary
                {
                    ["title"] = "旧驿站字段",
                    ["settlement_id"] = "settlement_legacy_schema",
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["entry_id"] = "old_stagecoach_entry_id",
                            ["state_label"] = "状态：旧字段",
                            ["cost_label"] = "车费：旧字段",
                        },
                    },
                },
            },
            ["forge"] = new GDictionary
            {
                ["visible"] = true,
                ["window_data"] = new GDictionary
                {
                    ["title"] = "旧锻造字段",
                    ["settlement_id"] = "settlement_legacy_schema",
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["entry_id"] = "old_forge_entry_id",
                            ["state_label"] = "状态：旧字段",
                            ["cost_label"] = "材料：旧字段",
                        },
                    },
                },
            },
            ["battle"] = new GDictionary
            {
                ["active"] = true,
                ["report_entry_count"] = 2,
                ["report_entries"] = new GArray
                {
                    new GDictionary
                    {
                        ["entry_type"] = "change_equipment",
                        ["ok"] = true,
                        ["reason_id"] = "equip",
                        ["current_ap"] = 9,
                        ["text"] = "old entry_type equipment report",
                    },
                    new GDictionary
                    {
                        ["type"] = "change_equipment",
                        ["entry_type"] = "change_equipment",
                        ["ok"] = true,
                        ["reason_id"] = "equip",
                        ["unit_id"] = "player_sword_01",
                        ["target_unit_id"] = "player_sword_01",
                        ["slot_id"] = "head",
                        ["item_id"] = "leather_cap",
                        ["instance_id"] = "eq_legacy_schema",
                        ["ap_before"] = 4,
                        ["current_ap"] = 2,
                        ["text"] = "formal type without formal operation or ap_after",
                    },
                },
            },
            ["reward"] = new GDictionary
            {
                ["visible"] = true,
                ["remaining_count"] = 1,
                ["reward"] = new GDictionary
                {
                    ["reward_id"] = "reward_legacy_schema",
                    ["member_id"] = "player_sword_01",
                    ["member_name"] = "剑士",
                    ["source_label"] = "测试",
                    ["summary_text"] = "旧奖励字段",
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["entry_type"] = "attribute_delta",
                            ["target_label"] = "old_reward_target_label",
                            ["amount"] = 1,
                            ["reason_text"] = "旧字段奖励",
                        },
                    },
                },
            },
        };
        string textSnapshot = GameTextSnapshotRenderer.render_full_snapshot(snapshot);

        AssertTrue(textSnapshot.Contains("provider_interaction_id="), "缺 provider_interaction_id 时文本快照只应渲染空正式字段。");
        AssertFalse(textSnapshot.Contains("old_contract_provider"), "旧 interaction_script_id 不应回填 provider_interaction_id。");
        AssertFalse(textSnapshot.Contains("old_shop_entry_id"), "shop 条目缺 display_name 时不应回退 entry_id。");
        AssertFalse(textSnapshot.Contains("old_contract_entry_id"), "contract board 条目缺 display_name 时不应回退 entry_id。");
        AssertFalse(textSnapshot.Contains("old_stagecoach_entry_id"), "stagecoach 条目缺 display_name 时不应回退 entry_id。");
        AssertFalse(textSnapshot.Contains("old_forge_entry_id"), "forge 条目缺 display_name 时不应回退 entry_id。");
        AssertFalse(textSnapshot.Contains("old entry_type equipment report"), "缺 type 的 change_equipment 战报不应靠 entry_type 渲染为有效装备战报。");
        AssertTrue(
            textSnapshot.Contains("report=change_equipment | ok=true | error= | op= | unit=player_sword_01"),
            "正式 type 存在但缺 operation 时不应从 reason_id 回填 op。"
        );
        AssertTrue(textSnapshot.Contains("ap=4>0"), "正式 type 存在但缺 ap_after 时不应从 current_ap 回填 AP。");
        AssertFalse(textSnapshot.Contains("op=equip"), "旧 reason_id 不应回填 change_equipment operation。");
        AssertFalse(textSnapshot.Contains("ap=4>2"), "旧 current_ap 不应回填 change_equipment ap_after。");
        AssertTrue(textSnapshot.Contains("entry=attribute_delta |  | amount=1"), "奖励条目缺 target_id 时应渲染空正式目标字段。");
        AssertFalse(textSnapshot.Contains("old_reward_target_label"), "奖励条目缺 target_id 时不应回退 target_label。");
    }

    private void TestSnapshotBuilderCrossReferencesQuestItemsInTextSnapshot()
    {
        var runtime = new SnapshotTestRuntime();
        var partyState = new PartyState();
        var questState = new QuestState { quest_id = "contract_archive_delivery" };
        questState.mark_accepted(7);
        questState.record_objective_progress(
            "deliver_dispatch",
            1,
            1,
            new GDictionary { ["item_id"] = "sealed_dispatch", ["submitted_quantity"] = 1 }
        );
        partyState.active_quests = new Godot.Collections.Array<QuestState> { questState };
        runtime.PartyState = partyState;
        runtime.ActiveModalId = "warehouse";
        runtime.WarehouseWindowData = new GDictionary
        {
            ["title"] = "共享仓库",
            ["entries"] = new GArray
            {
                BuildWarehouseEntry("sealed_dispatch", 1),
                BuildWarehouseEntry("bandit_insignia", 3),
                BuildWarehouseEntry("moonfern_sample", 2),
            },
        };

        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        GDictionary snapshot = builder.BuildHeadlessSnapshot();
        string textSnapshot = builder.BuildTextSnapshot();
        builder.Dispose();

        GDictionary warehouseSnapshot = Dict(snapshot, "warehouse");
        List<string> warehouseEntryIds = ExtractWindowEntryValueStrings(
            ArrayValue(Dict(warehouseSnapshot, "window_data"), "entries"),
            "item_id"
        );

        AssertTrue(BoolValue(warehouseSnapshot, "visible"), "任务物品交叉引用回归中仓库快照应保持可见。");
        AssertStringListEq(
            warehouseEntryIds,
            new[] { "sealed_dispatch", "bandit_insignia", "moonfern_sample" },
            "仓库快照应稳定暴露正式任务物品条目。"
        );
        AssertTrue(
            textSnapshot.Contains("context=item_id=sealed_dispatch submitted_quantity=1"),
            "文本快照应在 QUEST 分段保留任务物品上下文。"
        );
        AssertTrue(textSnapshot.Contains("entry=sealed_dispatch | qty=1 | total=1 | stackable=true"), "文本快照应在 WAREHOUSE 分段渲染封缄急件。");
        AssertTrue(textSnapshot.Contains("entry=bandit_insignia | qty=3 | total=3 | stackable=true"), "文本快照应在 WAREHOUSE 分段渲染匪徒纹章。");
        AssertTrue(textSnapshot.Contains("entry=moonfern_sample | qty=2 | total=2 | stackable=true"), "文本快照应在 WAREHOUSE 分段渲染月蕨样本。");
    }

    private void TestSnapshotBuilderExposesContractBoardModalSnapshot()
    {
        var runtime = new SnapshotTestRuntime
        {
            ActiveModalId = "contract_board",
            ContractBoardWindowData = new GDictionary
            {
                ["title"] = "春泉村 · 任务板",
                ["settlement_id"] = "spring_village_01",
                ["provider_interaction_id"] = "service_contract_board",
                ["entries"] = new GArray
                {
                    BuildWindowEntry("contract_first_hunt", "contract_first_hunt", "首轮狩猎", "状态：可查看", "奖励：80 金"),
                    BuildWindowEntry("contract_manual_drill", "contract_manual_drill", "训练记录", "状态：可查看", "奖励：30 金"),
                },
            },
        };

        GDictionary snapshot = BuildSnapshot(runtime, out string textSnapshot);
        GDictionary contractBoardSnapshot = Dict(snapshot, "contract_board");
        List<string> entryIds = ExtractWindowEntryValueStrings(
            ArrayValue(Dict(contractBoardSnapshot, "window_data"), "entries"),
            "quest_id"
        );

        AssertTrue(BoolValue(contractBoardSnapshot, "visible"), "contract board modal 激活时快照应暴露 contract_board.visible。");
        AssertEq(
            StringValue(Dict(contractBoardSnapshot, "window_data"), "provider_interaction_id"),
            "service_contract_board",
            "contract board 快照应保留当前 provider_interaction_id。"
        );
        AssertStringListEq(
            entryIds,
            new[] { "contract_first_hunt", "contract_manual_drill" },
            "contract board 快照应稳定暴露当前任务板条目列表。"
        );
        AssertTrue(textSnapshot.Contains("[CONTRACT_BOARD]"), "文本快照应渲染 CONTRACT_BOARD 分段。");
        AssertTrue(textSnapshot.Contains("provider_interaction_id=service_contract_board"), "文本快照应渲染当前任务板 provider_interaction_id。");
        AssertTrue(textSnapshot.Contains("首轮狩猎"), "文本快照应渲染首轮狩猎条目。");
        AssertTrue(textSnapshot.Contains("训练记录"), "文本快照应渲染训练记录条目。");
    }

    private void TestSnapshotBuilderExposesForgeModalSnapshot()
    {
        var runtime = new SnapshotTestRuntime
        {
            ActiveModalId = "forge",
            ForgeWindowData = new GDictionary
            {
                ["title"] = "灰烬镇 · 大师重铸",
                ["settlement_id"] = "forge_town",
                ["entries"] = new GArray
                {
                    new GDictionary
                    {
                        ["display_name"] = "大师重铸：铁制大剑",
                        ["state_label"] = "状态：可重铸",
                        ["cost_label"] = "材料：1 件 青铜短剑、2 件 铁矿石",
                    },
                },
            },
        };

        GDictionary snapshot = BuildSnapshot(runtime, out string textSnapshot);
        AssertTrue(BoolValue(Dict(snapshot, "forge"), "visible"), "forge modal 激活时快照应暴露 forge.visible。");
        AssertEq(
            StringValue(Dict(Dict(snapshot, "forge"), "window_data"), "title"),
            "灰烬镇 · 大师重铸",
            "forge 快照应保留窗口标题。"
        );
        AssertTrue(textSnapshot.Contains("[FORGE]"), "文本快照应渲染 FORGE 分段。");
        AssertTrue(textSnapshot.Contains("大师重铸：铁制大剑"), "文本快照应渲染 forge 配方名称。");
    }

    private void TestSnapshotBuilderExposesGenericForgeModalSnapshot()
    {
        var runtime = new SnapshotTestRuntime
        {
            ActiveModalId = "forge",
            ActiveShopContext = new GDictionary
            {
                ["title"] = "灰烬镇 · 熔炉整备",
                ["settlement_id"] = "forge_town",
                ["panel_kind"] = "forge",
                ["submission_source"] = "forge",
                ["entries"] = new GArray
                {
                    new GDictionary
                    {
                        ["entry_id"] = "forge:temper_edge",
                        ["display_name"] = "刃口淬火",
                        ["state_label"] = "状态：可执行",
                        ["cost_label"] = "材料：1 件 铁矿石、1 件 皮革护衣",
                    },
                },
            },
        };

        GDictionary snapshot = BuildSnapshot(runtime, out string textSnapshot);
        AssertEq(
            StringValue(Dict(Dict(snapshot, "forge"), "window_data"), "title"),
            "灰烬镇 · 熔炉整备",
            "通用 forge modal 应从共享窗口上下文进入 forge 快照。"
        );
        AssertTrue(textSnapshot.Contains("刃口淬火"), "文本快照应渲染通用 forge 条目名称。");
        AssertEq(Dict(Dict(snapshot, "shop"), "window_data").Count, 0, "forge panel_kind 不应继续出现在 shop 快照中。");
    }

    private void TestSnapshotBuilderRequiresPanelKindForForgeModal()
    {
        var runtime = new SnapshotTestRuntime
        {
            ActiveModalId = "forge",
            ActiveShopContext = new GDictionary
            {
                ["title"] = "旧 forge 来源",
                ["settlement_id"] = "forge_town",
                ["submission_source"] = "forge",
                ["entries"] = new GArray(),
            },
        };

        GDictionary snapshot = BuildSnapshot(runtime, out _);
        AssertEq(
            Dict(Dict(snapshot, "forge"), "window_data").Count,
            0,
            "只有 submission_source=forge 的旧窗口上下文不应再被识别为 forge modal。"
        );
    }

    private void TestSnapshotBuilderExposesGameOverSnapshot()
    {
        var runtime = new SnapshotTestRuntime
        {
            ActiveModalId = "game_over",
            GameOverContext = new GDictionary
            {
                ["title"] = "Game Over",
                ["description"] = "主角已阵亡，本次旅程结束。",
                ["confirm_text"] = "返回标题",
                ["main_character_member_id"] = "player_sword_01",
                ["main_character_name"] = "剑士",
                ["main_character_dead"] = true,
            },
        };

        GDictionary snapshot = BuildSnapshot(runtime, out string textSnapshot);
        GDictionary gameOverSnapshot = Dict(snapshot, "game_over");
        AssertEq(StringValue(gameOverSnapshot, "title"), "Game Over", "game_over 快照应暴露标题。");
        AssertEq(StringValue(gameOverSnapshot, "main_character_member_id"), "player_sword_01", "game_over 快照应暴露主角成员 ID。");
        AssertTrue(BoolValue(gameOverSnapshot, "main_character_dead"), "game_over 快照应标记主角死亡。");
        AssertTrue(textSnapshot.Contains("[GAME_OVER]"), "文本快照应渲染 GAME_OVER 分段。");
        AssertTrue(textSnapshot.Contains("main_character_member_id=player_sword_01"), "文本快照应渲染主角成员 ID。");
        AssertTrue(textSnapshot.Contains("main_character_dead=true"), "文本快照应渲染主角死亡标记。");
    }

    private void TestSnapshotBuilderExposesBattleLootSnapshot()
    {
        var runtime = new SnapshotTestRuntime
        {
            LastBattleLootSnapshot = new GDictionary
            {
                ["battle_name"] = "荒狼巢穴",
                ["winner_faction_id"] = "player",
                ["loot_entries"] = new GArray { new GDictionary { ["item_id"] = "beast_hide", ["quantity"] = 2 } },
                ["loot_entry_count"] = 1,
                ["loot_summary_text"] = "兽皮 x2",
                ["overflow_entries"] = new GArray { new GDictionary { ["item_id"] = "beast_hide", ["quantity"] = 1 } },
                ["overflow_entry_count"] = 1,
                ["overflow_summary_text"] = "兽皮 x1",
            },
        };

        GDictionary snapshot = BuildSnapshot(runtime, out string textSnapshot);
        GDictionary lootSnapshot = Dict(snapshot, "loot");
        AssertEq(StringValue(lootSnapshot, "battle_name"), "荒狼巢穴", "loot 快照应保留最近一次战斗名称。");
        AssertEq(StringValue(lootSnapshot, "loot_summary_text"), "兽皮 x2", "loot 快照应暴露稳定 loot 摘要。");
        AssertEq(StringValue(lootSnapshot, "overflow_summary_text"), "兽皮 x1", "loot 快照应暴露稳定 overflow 摘要。");
        AssertTrue(textSnapshot.Contains("[LOOT]"), "文本快照应为最近一次战斗掉落渲染 LOOT 分段。");
        AssertTrue(textSnapshot.Contains("loot_summary=兽皮 x2"), "文本快照应渲染 loot 摘要。");
        AssertTrue(textSnapshot.Contains("overflow_summary=兽皮 x1"), "文本快照应渲染 overflow 摘要。");
    }

    private void TestSnapshotBuilderOmitsLootSectionWhenEmpty()
    {
        GDictionary snapshot = BuildSnapshot(new SnapshotTestRuntime(), out string textSnapshot);
        AssertEq(Dict(snapshot, "loot").Count, 0, "没有最近掉落时 headless snapshot 不应强行生成 loot 段。");
        AssertFalse(textSnapshot.Contains("[LOOT]"), "没有最近掉落时文本快照不应插入 LOOT 分段。");
        AssertTrue(
            textSnapshot.IndexOf("[BATTLE]", StringComparison.Ordinal) >= 0
                && textSnapshot.IndexOf("[REWARD]", StringComparison.Ordinal)
                    > textSnapshot.IndexOf("[BATTLE]", StringComparison.Ordinal),
            "没有 loot 时既有 BATTLE -> REWARD 顺序应保持稳定。"
        );
    }

    private GameSession CreateTestSession()
    {
        var gameSession = new GameSession();
        int createError = gameSession.create_new_save(TestWorldConfig);
        AssertEq(createError, (int)Error.Ok, "GameSession 应能基于测试世界配置创建新存档。");
        if (createError != (int)Error.Ok)
        {
            CleanupTestSession(gameSession);
            return null;
        }
        return gameSession;
    }

    private static void CleanupTestSession(GameSession gameSession)
    {
        if (gameSession == null)
            return;
        gameSession.clear_persisted_game();
        gameSession.Free();
    }

    private static GDictionary BuildSnapshot(
        IGameRuntimeSnapshotSource runtime,
        out string textSnapshot
    )
    {
        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        GDictionary snapshot = builder.BuildHeadlessSnapshot();
        textSnapshot = builder.BuildTextSnapshot();
        builder.Dispose();
        return snapshot;
    }

    private static GDictionary BuildPartyQuestSnapshot(PartyState partyState)
    {
        var runtime = new SnapshotTestRuntime { PartyState = partyState };
        GDictionary snapshot = BuildSnapshot(runtime, out _);
        return Dict(Dict(snapshot, "party"), "quests");
    }

    private static GDictionary FindMemberSnapshot(GDictionary snapshot, string memberId)
    {
        foreach (Variant memberValue in ArrayValue(Dict(snapshot, "party"), "members"))
        {
            if (memberValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary member = memberValue.AsGodotDictionary();
            if (StringValue(member, "member_id") == memberId)
                return member;
        }
        return new GDictionary();
    }

    private static GDictionary BuildQuestSnapshotPayload(
        string questId,
        object stageId,
        string statusId = "active",
        int completedAtWorldStep = -1
    )
    {
        var payload = new GDictionary
        {
            ["quest_id"] = questId,
            ["status_id"] = statusId,
            ["objective_progress"] = new GDictionary(),
            ["accepted_at_world_step"] = 1,
            ["completed_at_world_step"] = completedAtWorldStep,
            ["reward_claimed_at_world_step"] = -1,
            ["last_progress_context"] = new GDictionary(),
        };
        if (stageId is string stageString)
            payload["stage_id"] = stageString;
        else if (stageId is int stageInt)
            payload["stage_id"] = stageInt;
        else if (stageId is StringName stageStringName)
            payload["stage_id"] = stageStringName;
        return payload;
    }

    private static GDictionary BuildWarehouseEntry(string itemId, int quantity)
    {
        return new GDictionary
        {
            ["item_id"] = itemId,
            ["quantity"] = quantity,
            ["total_quantity"] = quantity,
            ["is_stackable"] = true,
            ["stack_limit"] = 20,
            ["storage_mode"] = "stack",
        };
    }

    private static GDictionary BuildWindowEntry(
        string entryId,
        string questId,
        string displayName,
        string stateLabel,
        string costLabel
    )
    {
        return new GDictionary
        {
            ["entry_id"] = entryId,
            ["quest_id"] = questId,
            ["display_name"] = displayName,
            ["state_label"] = stateLabel,
            ["cost_label"] = costLabel,
        };
    }

    private static List<string> ExtractWindowEntryValueStrings(GArray entries, string key)
    {
        var result = new List<string>();
        foreach (Variant entryValue in entries)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                continue;
            result.Add(StringValue(entryValue.AsGodotDictionary(), key));
        }
        return result;
    }

    private static List<string> StringList(GArray values)
    {
        var result = new List<string>();
        foreach (Variant value in values)
            result.Add(value.AsString());
        return result;
    }

    private static GDictionary Dict(GDictionary source, string key)
    {
        if (
            source != null
            && source.ContainsKey(key)
            && source[key].VariantType == Variant.Type.Dictionary
        )
            return source[key].AsGodotDictionary();
        return new GDictionary();
    }

    private static GArray ArrayValue(GDictionary source, string key)
    {
        if (source != null && source.ContainsKey(key) && source[key].VariantType == Variant.Type.Array)
            return source[key].AsGodotArray();
        return new GArray();
    }

    private static string StringValue(GDictionary source, string key, string fallback = "")
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        if (value.VariantType == Variant.Type.Nil)
            return fallback;
        return value.AsString();
    }

    private static int IntValue(GDictionary source, string key, int fallback = 0)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    private static bool BoolValue(GDictionary source, string key, bool fallback = false)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }

    private void AssertStringListEq(
        IEnumerable<string> actual,
        IEnumerable<string> expected,
        string message
    )
    {
        string actualText = string.Join(",", actual);
        string expectedText = string.Join(",", expected);
        if (actualText != expectedText)
            _failures.Add($"{message} | actual=[{actualText}] expected=[{expectedText}]");
    }

}
