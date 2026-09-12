using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_map_panel_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private static readonly PackedScene BattleMapPanelScene = GD.Load<PackedScene>(
        "res://scenes/ui/battle_map_panel.tscn"
    );

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        try
        {
            await TestBattleMapPanelAppliesFormalSnapshot();
            await TestScaledViewportAndSkillHitTargets();
            await TestBattleMapPanelAppliesCommandDock();
            await TestBattleMapPanelViewportControlsAndFateRow();
            await TestBattleMapPanelRevealUsesDetachedSnapshotAndCancelsCleanly();
        }
        catch (System.Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        finally
        {
            RequestTestExit(_test.Finish("Battle map panel schema regression"));
        }
    }

    private async System.Threading.Tasks.Task TestBattleMapPanelRevealUsesDetachedSnapshotAndCancelsCleanly()
    {
        var panel = BattleMapPanelScene.Instantiate<BattleMapPanel>();
        Root.AddChild(panel);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        var loadingEvents = new List<(bool IsLoading, float Progress)>();
        int readySignalCount = 0;
        void OnLoadingStateChanged(bool isLoading, float progress) =>
            RecordLoadingEvent(isLoading, progress);
        void RecordLoadingEvent(bool isLoading, float progress)
        {
            loadingEvents.Add((isLoading, progress));
            if (!isLoading && Mathf.IsEqualApprox(progress, 100.0f))
            {
                readySignalCount += 1;
            }
        }
        panel.battle_loading_state_changed += OnLoadingStateChanged;

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

        string projectedAllyName = ally.display_name;
        ShowPendingBattle(panel, fixture.State);
        _test.True(
            panel.Visible && panel.IsLoadingBattle() && panel.GetLoadingProgress() > 0.0f,
            "首次 ShowBattle 应公开进入可见 loading 状态。"
        );
        ally.display_name = "mutated_after_snapshot";
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.Eq(
            panel.unit_name_label.Text,
            projectedAllyName,
            "异步 reveal 应应用调用时生成的 detached HUD snapshot，而不是随后改写的 live unit。"
        );

        panel.HideBattle();
        _test.False(panel.Visible, "HideBattle 应立即隐藏 panel。");
        _test.False(panel.IsLoadingBattle(), "HideBattle 应取消进行中的 reveal。");
        _test.Eq(panel.GetLoadingProgress(), 0.0f, "HideBattle 应把 loading progress 归零。");
        _test.Eq(panel.unit_name_label.Text, "待命", "HideBattle 应恢复占位 HUD，而非保留上场快照。");
        _test.True(
            loadingEvents.Count > 0
                && !loadingEvents[^1].IsLoading
                && Mathf.IsEqualApprox(loadingEvents[^1].Progress, 0.0f),
            "HideBattle 应通过公开 loading signal 发布取消和归零。"
        );

        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.False(panel.Visible, "已取消的异步 reveal 不应在后续 frame 重新显示 panel。");
        _test.Eq(
            panel.unit_name_label.Text,
            "待命",
            "已取消的异步 reveal 不应在后续 frame 写回旧 HUD snapshot。"
        );

        ShowPendingBattle(panel, fixture.State);
        _test.True(panel.IsLoadingBattle(), "再次 ShowBattle 应启动新的 reveal 生命周期。");
        int readySignalCountBeforeExit = readySignalCount;
        panel.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.Eq(
            readySignalCount,
            readySignalCountBeforeExit,
            "_ExitTree 应静默失效等待中的 reveal，不得在节点退出后发布迟到的 ready 信号。"
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
        BattleHudObjectiveProgressSnapshot objectiveProgress = null,
        IEnumerable<BattleHudSkillSlotSnapshot> skillSlots = null
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
            skillSlots ?? Array.Empty<BattleHudSkillSlotSnapshot>(),
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

    private async System.Threading.Tasks.Task TestScaledViewportAndSkillHitTargets()
    {
        Vector2I originalSize = Root.Size;
        Vector2I originalContentSize = Root.ContentScaleSize;
        Window.ContentScaleModeEnum originalMode = Root.ContentScaleMode;
        var panel = BattleMapPanelScene.Instantiate<BattleMapPanel>();
        try
        {
            Root.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
            new DisplaySettingsService().ApplySettings(new(new Vector2I(3840, 2160), false), Root);
            Root.AddChild(panel);
            panel.Visible = true;
            panel._apply_snapshot(BuildSnapshot(skillSlots: new[]
            {
                new BattleHudSkillSlotSnapshot(0, true),
                new BattleHudSkillSlotSnapshot(4, false, displayName: "测试技能", shortName: "测试", hotkey: "5", accentColor: Colors.Purple),
                new BattleHudSkillSlotSnapshot(5, true),
            }));
            for (int i = 0; i < 5; i++)
                await ToSignal(this, SignalName.ProcessFrame);
            _test.Eq(Root.ContentScaleSize, new Vector2I(1920, 1080), "4K 应保持可读的逻辑 UI 尺寸。");
            _test.Eq(panel.skill_grid.GetChildCount(), 1, "空槽不应占据操作区。");
            Control slot = panel.skill_grid.GetChild<Control>(0);
            ColorRect glow = null;
            BattleSkillSlotButton button = null;
            foreach (Node node in slot.FindChildren("*", "Control", true, false))
            {
                if (node is ColorRect rect && rect.Name == "FateGlow") glow = rect;
                if (node is BattleSkillSlotButton target) button = target;
            }
            _test.True(glow != null && glow.Size.Y <= 3.1f, "技能色条必须保持底部细线，不能覆盖图标。");
            _test.True(glow != null && glow.GetGlobalRect().End.Y <= slot.GetGlobalRect().End.Y + 1, "色条应位于槽内。");
            int selectedIndex = -1;
            panel.battle_skill_slot_selected += index => selectedIndex = index;
            var wait = new E2eWait(this);
            var input = new E2eInputDriver(this, wait);
            await input.ClickAsync(button);
            _test.Eq(selectedIndex, 4, "隐藏空槽后，真实鼠标点击仍应提交原始技能索引。");

            Control host = panel.GetNode<Control>("%MapViewportHost");
            SubViewport map = panel.map_viewport_container.GetNode<SubViewport>("MapSubViewport");
            Vector2 scale = Root.GetStretchTransform().Scale;
            _test.True(Mathf.Abs(map.Size.X - host.Size.X * scale.X) <= 1, "地图渲染宽度应使用物理像素。");
            _test.True(Mathf.Abs(panel.map_viewport_container.GetGlobalRect().Size.X - host.Size.X) <= 1, "地图显示和鼠标命中范围应与逻辑宿主一致。");
            _test.True(panel.map_frame.GetGlobalRect().End.Y <= panel.bottom_panel.GlobalPosition.Y, "操作区不得覆盖地图可点击范围。");
            BattleUnitState ally = BattleTestFixture.BuildUnit("scaled_ally", "player", Vector2I.Zero);
            BattleUnitState enemy = BattleTestFixture.BuildUnit("scaled_enemy", "enemy", new Vector2I(2, 0));
            using (BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
                "scaled_hit_test", new Vector2I(3, 2), new[] { ally }, new[] { enemy }))
            {
                ShowPendingBattle(panel, fixture.State);
                for (int i = 0; i < 8; i++) await ToSignal(this, SignalName.ProcessFrame);
                Vector2I clickedCoord = new(-1, -1);
                Vector2I expectedCoord = new(1, 1);
                panel.battle_cell_clicked += coord => clickedCoord = coord;
                await input.ClickAtAsync(panel.map_viewport_container, panel._battle_board.CoordToViewportPosition(expectedCoord));
                _test.Eq(clickedCoord, expectedCoord, "4K 地图缩放后，真实点击必须命中原来的格子。");
                new DisplaySettingsService().ApplySettings(new(new Vector2I(1280, 720), false), Root);
                for (int i = 0; i < 5; i++) await ToSignal(this, SignalName.ProcessFrame);
                _test.True(new Rect2(Vector2.Zero, map.Size).HasPoint(panel._battle_board.CoordToViewportPosition(Vector2I.Zero)),
                    "从 4K 缩回 720p 后焦点单位应继续位于视口内。");
                panel.HideBattle();
            }
            new DisplaySettingsService().ApplySettings(new(new Vector2I(1280, 720), false), Root);
            for (int i = 0; i < 5; i++) await ToSignal(this, SignalName.ProcessFrame);
            _test.True(Mathf.Abs(map.Size.X - host.Size.X) <= 1, "缩回 720p 后地图不能保留 4K 最小尺寸。");
        }
        finally
        {
            panel.QueueFree();
            await ToSignal(this, SignalName.ProcessFrame);
            Root.ContentScaleMode = originalMode;
            Root.ContentScaleSize = originalContentSize;
            Root.Size = originalSize;
        }
    }
}
