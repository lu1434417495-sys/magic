using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_map_panel_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private static readonly PackedScene BattleMapPanelScene = GD.Load<PackedScene>(
        "res://scenes/ui/battle_map_panel.tscn"
    );

    public override async void _Initialize()
    {
        await TestBattleMapPanelAppliesFormalSnapshot();
        await TestBattleMapPanelAppliesCommandDock();
        await TestBattleMapPanelViewportControlsAndFateRow();
        await TestBattleMapPanelReleasesPendingBattlePayload();
        RequestTestExit(_test.Finish("Battle map panel schema regression"));
    }

    private async System.Threading.Tasks.Task TestBattleMapPanelReleasesPendingBattlePayload()
    {
        var panel = BattleMapPanelScene.Instantiate<BattleMapPanel>();
        Root.AddChild(panel);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        BattleUnitState ally = BattleTestFixture.BuildUnit(
            "panel_pending_ally",
            "player",
            new Vector2I(0, 0)
        );
        BattleUnitState enemy = BattleTestFixture.BuildUnit(
            "panel_pending_enemy",
            "enemy",
            new Vector2I(2, 0)
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "panel_pending_payload",
            new Vector2I(3, 2),
            new[] { ally },
            new[] { enemy }
        );

        ShowPendingBattle(panel, fixture.State);
        for (
            int frame = 0;
            frame < 5 && ReadPrivateField<bool>(panel, "_has_pending_show_battle_payload");
            frame++
        )
        {
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        }
        AssertPendingBattlePayloadCleared(panel, "应用完成后");
        ShowPendingBattle(panel, fixture.State);
        AssertPendingBattlePayloadCleared(panel, "同一 battle reveal 更新后");

        panel.HideBattle();
        ShowPendingBattle(panel, fixture.State);
        BattleBoardRenderSnapshot pendingSnapshot =
            ReadPrivateField<BattleBoardRenderSnapshot>(panel, "_pending_board_snapshot");
        _test.True(
            pendingSnapshot != null
                && pendingSnapshot.BattleId == fixture.State.battle_id
                && pendingSnapshot.MapSize == fixture.State.map_size,
            "HideBattle 前应持有 detached board snapshot。"
        );
        panel.HideBattle();
        AssertPendingBattlePayloadCleared(panel, "HideBattle 后");

        ShowPendingBattle(panel, fixture.State);
        panel.UpdateHoverPreview(
            fixture.State,
            Vector2I.Zero,
            Array.Empty<Vector2I>(),
            "",
            ""
        );
        _test.Eq(
            ReadPrivateField<Vector2I>(panel, "_hover_preview_coord"),
            Vector2I.Zero,
            "_ExitTree 前 hover preview 只应保留展示坐标。"
        );
        int revealTicketBeforeExit = ReadPrivateField<int>(panel, "_battle_reveal_ticket");
        panel.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        AssertPendingBattlePayloadCleared(panel, "_ExitTree 后");
        _test.Eq(
            ReadPrivateField<Vector2I>(panel, "_hover_preview_coord"),
            BattleMapPanel.INVALID_HOVER_COORD(),
            "_ExitTree 应清空 hover preview 展示坐标。"
        );
        _test.True(
            ReadPrivateField<int>(panel, "_battle_reveal_ticket") > revealTicketBeforeExit,
            "_ExitTree 应使仍在等待的 battle reveal ticket 失效。"
        );
        _test.Eq(
            ReadPrivateField<float>(panel, "_battle_loading_progress"),
            0.0f,
            "_ExitTree 应在不发布 UI signal 的情况下归零 loading progress。"
        );
    }

    private void AssertPendingBattlePayloadCleared(BattleMapPanel panel, string stage)
    {
        _test.False(
            ReadPrivateField<bool>(panel, "_has_pending_show_battle_payload"),
            $"{stage}不应保留 pending payload flag。"
        );
        _test.True(
            ReadPrivateField<BattleBoardRenderSnapshot>(panel, "_pending_board_snapshot") == null,
            $"{stage}不应保留 pending board snapshot。"
        );
        _test.True(
            ReadPrivateField<BattleHudSnapshot>(panel, "_pending_hud_snapshot") == null,
            $"{stage}不应保留 pending HUD snapshot。"
        );
    }

    private static void ShowPendingBattle(BattleMapPanel panel, BattleState state) =>
        panel.ShowBattle(
            state,
            Vector2I.Zero,
            "",
            "",
            "",
            Array.Empty<Vector2I>(),
            Array.Empty<Vector2I>(),
            0,
            Array.Empty<StringName>(),
            ""
        );

    private static T ReadPrivateField<T>(BattleMapPanel panel, string fieldName)
    {
        FieldInfo field =
            typeof(BattleMapPanel).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic
            )
            ?? throw new MissingFieldException(typeof(BattleMapPanel).FullName, fieldName);
        return (T)field.GetValue(panel);
    }

    private async System.Threading.Tasks.Task TestBattleMapPanelViewportControlsAndFateRow()
    {
        var panel = BattleMapPanelScene.Instantiate<BattleMapPanel>();
        Root.AddChild(panel);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.Eq(
            panel.fate_badge_row.GetParent().Name.ToString(),
            "TopLayoutVbox",
            "FateBadgeRow 应上移为 TopBar 下的独立条（B3）。"
        );

        var zoomLabel = panel.GetNodeOrNull<Label>(
            "HudRoot/TopBar/TopLayoutVbox/TopRow/RightCell/ZoomChip/ZoomValueLabel"
        );
        _test.True(zoomLabel != null, "TopBar 右格应有缩放指示 ZoomChip（C5）。");
        _test.True(
            zoomLabel.Text.StartsWith("×"),
            $"缩放指示应显示当前缩放倍率，actual={zoomLabel.Text}"
        );

        var resetButton = panel.GetNodeOrNull<Button>(
            "HudRoot/TopBar/TopLayoutVbox/TopRow/RightCell/ResetViewButton"
        );
        _test.True(resetButton != null, "TopBar 右格应有重置视角按钮（C5）。");
        _test.False(resetButton.Disabled, "重置视角按钮应始终可用（本地视口操作）。");
        resetButton.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.True(
            zoomLabel.Text.StartsWith("×"),
            "点击重置视角后缩放指示应仍为倍率格式。"
        );

        panel.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private async System.Threading.Tasks.Task TestBattleMapPanelAppliesCommandDock()
    {
        var panel = BattleMapPanelScene.Instantiate<BattleMapPanel>();
        Root.AddChild(panel);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        panel._apply_snapshot(
            BuildSnapshot(
                selectedSkillVariantName: "重击形态",
                selectionMode: "multi_unit",
                selectedTargetCount: 2,
                selectedTargetMaxCount: 3,
                confirmReady: true,
                hintText: "继续点选目标，还需 1 个；Esc 取消",
                recentLogLines: new[] { "甲 命中 乙", "乙 倒地" },
                commandDock: new BattleHudCommandDockSnapshot(true, true, true, false)
            )
        );
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(panel.resolve_button.Disabled, "resolve_enabled=true 时结算按钮应可用。");
        _test.True(
            panel.resolve_button.HasThemeColorOverride("font_color"),
            "selected_skill_confirm_ready=true 时结算按钮应高亮（font_color override）。"
        );
        _test.False(panel.clear_skill_button.Disabled, "clear_skill_enabled=true 时取消按钮应可用。");
        _test.False(panel.prev_variant_button.Disabled, "prev_variant_enabled=true 时上一形态按钮应可用。");
        _test.True(panel.next_variant_button.Disabled, "next_variant_enabled=false 时下一形态按钮应置灰。");
        _test.Eq(panel.variant_name_label.Text, "重击形态", "VariantNameLabel 应显示 selected_skill_variant_name。");
        _test.Eq(panel.command_summary_label.Text, "已选 2/3 目标", "CommandSummaryLabel 应汇总多目标进度。");
        _test.Eq(panel.hint_label.Text, "继续点选目标，还需 1 个；Esc 取消", "HintLabel 应显示 hint_text。");
        _test.Eq(panel.log_label.Text, "甲 命中 乙\n乙 倒地", "LogLabel 应换行拼接 recent_battle_log_lines。");

        panel.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private async System.Threading.Tasks.Task TestBattleMapPanelAppliesFormalSnapshot()
    {
        var panel = BattleMapPanelScene.Instantiate<BattleMapPanel>();
        Root.AddChild(panel);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        BattleHudObjectiveProgressSnapshot objectiveProgress =
            BuildEscapeObjectiveProgress();
        panel._apply_snapshot(
            BuildSnapshot(
                roundBadge: new BattleHudRoundBadgeSnapshot("TU 12", "READY 3"),
                focusUnit: new BattleHudFocusUnitSnapshot(
                    "见习战士",
                    "玩家前排",
                    EmptyResourceInfo(),
                    "W",
                    "warrior",
                    new Color(0.2f, 0.2f, 0.2f, 1.0f),
                    Colors.Black,
                    new Color(0.9f, 0.5f, 0.2f, 1.0f),
                    18,
                    30,
                    6,
                    10,
                    0,
                    1,
                    0,
                    1,
                    0,
                    2,
                    3,
                    5,
                    StatusEffects: new[]
                    {
                        new BattleHudStatusEffectSnapshot(
                            "poisoned", "中毒", 2, 30, true, "中毒 · 减益 · 层数 2 · 剩余 30 TU"
                        ),
                        new BattleHudStatusEffectSnapshot(
                            "attack_up", "攻击提升", 1, -1, false, "攻击提升 · 增益"
                        ),
                    }
                ),
                skillSubtitle: "预计命中率 75%",
                tooltipText: "需要掷出 6+",
                objectiveProgress: objectiveProgress
            )
        );
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(panel.Visible, "仅应用 snapshot 不应强制改变 BattleMapPanel 可见性。");
        _test.Eq((int)panel.hp_bar.Value, 18, "BattleMapPanel 应应用 formal focus_unit.hp_current。");
        _test.Eq((int)panel.hp_bar.MaxValue, 30, "BattleMapPanel 应应用 formal focus_unit.hp_max。");
        _test.Eq((int)panel.mp_bar.Value, 6, "BattleMapPanel 应应用 formal focus_unit.mp_current。");
        _test.Eq((int)panel.mp_bar.MaxValue, 10, "BattleMapPanel 应应用 formal focus_unit.mp_max。");
        _test.True(panel.objective_status_label.Visible, "有效目标进度应显示 ObjectiveStatusLabel。");
        _test.True(
            panel.objective_status_label.Text.Contains("逃离战场", StringComparison.Ordinal)
                && panel.objective_status_label.Text.Contains("已到达 1/1", StringComparison.Ordinal),
            $"ObjectiveStatusLabel 应显示逃离目标及进度，actual={panel.objective_status_label.Text}"
        );

        var statusRow = panel.GetNodeOrNull<HFlowContainer>(
            "HudRoot/BottomPanel/BottomBand/UnitCard/CardLayout/InfoColumn/StatusBadgeRow"
        );
        _test.True(statusRow != null, "UnitCard InfoColumn 内应存在 StatusBadgeRow。");
        _test.True(statusRow.Visible, "focus_unit 有状态效果时 StatusBadgeRow 应可见。");
        _test.Eq(statusRow.GetChildCount(), 2, "StatusBadgeRow 应为每个状态效果渲染一个徽章。");
        var firstBadgeLabel = statusRow.GetChild(0).GetChild(0).GetChild<Label>(0);
        _test.Eq(firstBadgeLabel.Text, "中毒×2 30TU", "状态徽章应拼接 label×层数 + 剩余TU。");

        panel._apply_snapshot(BuildSnapshot(roundBadge: new BattleHudRoundBadgeSnapshot("TU 13", "READY 1")));
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.False(statusRow.Visible, "focus_unit 无状态效果时 StatusBadgeRow 应隐藏。");

        panel.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private static BattleHudSnapshot BuildSnapshot(
        BattleHudRoundBadgeSnapshot roundBadge = null,
        BattleHudFocusUnitSnapshot focusUnit = null,
        string skillSubtitle = "",
        string tooltipText = "",
        string selectedSkillVariantName = "",
        string selectionMode = "single_unit",
        int selectedTargetCount = 0,
        int selectedTargetMaxCount = 1,
        bool confirmReady = false,
        string hintText = "",
        IEnumerable<string> recentLogLines = null,
        BattleHudCommandDockSnapshot commandDock = null,
        BattleHudObjectiveProgressSnapshot objectiveProgress = null
    ) =>
        new(
            "战斗地图",
            "",
            roundBadge ?? new BattleHudRoundBadgeSnapshot("TU --", "READY 0"),
            "手动",
            Array.Empty<BattleHudQueueEntrySnapshot>(),
            focusUnit,
            "技能矩阵",
            selectedSkillVariantName,
            skillSubtitle,
            Array.Empty<BattleHudSkillSlotSnapshot>(),
            "",
            "",
            BattlePresentationPayload.Empty,
            "",
            Array.Empty<int>(),
            "",
            0,
            0,
            BattlePresentationPayload.Empty,
            "",
            "",
            Array.Empty<BattleHudFateBadgeSnapshot>(),
            tooltipText,
            selectionMode,
            1,
            selectedTargetMaxCount,
            selectedTargetCount,
            confirmReady,
            false,
            commandDock ?? BattleHudCommandDockSnapshot.Empty,
            hintText,
            recentLogLines ?? Array.Empty<string>(),
            new BattleHudEquipmentPanelSnapshot(
                "", "", "", "", 0, false, "", null, null, ""
            ),
            objectiveProgress: objectiveProgress
        );

    private BattleHudObjectiveProgressSnapshot BuildEscapeObjectiveProgress()
    {
        BattleUnitState ally = BattleTestFixture.BuildUnit(
            "panel_escape_ally",
            "player",
            new Vector2I(3, 1)
        );
        ally.source_member_id = "panel_escape_member";
        BattleUnitState enemy = BattleTestFixture.BuildUnit(
            "panel_escape_enemy",
            "enemy",
            new Vector2I(1, 1)
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "panel_escape_objective",
            new Vector2I(4, 3),
            new[] { ally },
            new[] { enemy }
        );
        _test.True(
            fixture.State.InitializeObjective(
                new BattleEscapeObjectiveDefinition(
                    "right_exit",
                    BattleMapEdge.Right,
                    1
                )
            ),
            "BattleMapPanel objective fixture should initialize."
        );
        return new BattleHudObjectiveProgressSnapshot(
            new BattleStateReadView(fixture.State).ObjectiveProgress
        );
    }

    private static BattleHudResourceInfoSnapshot EmptyResourceInfo()
    {
        BattleHudResourceLineSnapshot line = new(0, 1, 0.0f, "", true);
        return new BattleHudResourceInfoSnapshot(line, line, line, line, line, line);
    }
}
