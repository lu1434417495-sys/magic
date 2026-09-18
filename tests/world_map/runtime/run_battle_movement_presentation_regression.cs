using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

// Controlled battle setup, followed by real HUD input and per-frame visual checks.
// This is scene integration, not a complete application/player-journey E2E.
public partial class run_battle_movement_presentation_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private WorldMapSystem _world;
    private GameSession _session;
    private E2eWait _wait;
    private E2eInputDriver _input;
    private BattleState _state;
    private BattleUnitState _mover;
    private Vector2I _start;
    private Vector2I _target;
    private GameRuntimeFacade Runtime => _world._runtime;
    private BattleMapPanel Panel => _world.battle_map_panel;
    private BattleBoard2D Board => Panel._battle_board;

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        try
        {
            _wait = new E2eWait(this);
            _input = new E2eInputDriver(this, _wait);
            var coordinator = Root.GetNode<ApplicationLifetimeCoordinator>("ApplicationLifetimeCoordinator");
            await coordinator.CloseSessionAsync(Root.GetNode<GameSession>("GameSession"));
            string saveDirectory = $"user://test_battle_movement/{Guid.NewGuid():N}";
            _session = GameSessionTestFactory.CreateForCoordinatorAttachment(
                new GameSessionPersistenceOptions(saveDirectory, $"{saveDirectory}/index.dat"));
            Root.AddChild(_session);
            _test.Eq((Error)_session.StartNewGame("test"),
                Error.Ok, "创建测试世界。");
            _world = EngineAssetAccess.ResolveCodeAssetBorrowed<PackedScene>(
                "res://scenes/main/world_map.tscn").Instantiate<WorldMapSystem>();
            Root.AddChild(_world);
            await _wait.FramesAsync(2);
            Runtime.StartBattle(Runtime.GetActiveWorldRuntimeData().EncounterAnchors
                .First(anchor => anchor.encounter_kind == "single"));
            await _wait.UntilAsync(() => Runtime.GetBattleState()?.IsEmpty() == false, 600, "战斗生成");
            _world.SetProcess(false);
            _state = Runtime.GetBattleState();
            _mover = _state.GetUnit(_state.GetAllyUnitIdsTyped()[0]);
            Runtime.CommandConfirmBattleStartTyped();
            _start = new(_state.map_size.X / 2 - 2, _state.map_size.Y / 2);
            _target = _start + new Vector2I(3, 0);

            foreach (Vector2I size in new[] { new Vector2I(1280, 720), new(3840, 2160) })
            {
                new DisplaySettingsService().ApplySettings(new(size, false), Root);
                PrepareTurn();
                await RenderBoard();
                await TestMoveThroughHud(size);
            }
            await TestPlaybackCancellationAndBatchOrder();
        }
        catch (Exception exception) { _test.Fail($"Movement presentation: {exception}"); }
        finally
        {
            if (_input != null) await _input.ReleaseAllAsync();
            _world?.QueueFree();
            if (_wait != null) await _wait.FramesAsync(2);
            if (_session != null && !_session.IsClosed)
                _test.Eq((Error)_session.ClearPersistedGame(), Error.Ok, "清理本次测试独享的存档目录。");
            RequestTestExit(_test.Finish("Battle movement presentation regression"));
        }
    }

    private void PrepareTurn()
    {
        foreach (BattleCellState cell in _state.Cells())
        {
            cell.SetBaseHeight(4);
            cell.SetHeightOffset(0);
            cell.SetTerrain("land");
            cell.prop_ids.Clear();
        }
        // A two-level ledge forces a detour; both shortest routes contain turns.
        _state.GetCell(_start + Vector2I.Right).SetBaseHeight(6);
        _state.RebuildCellColumns();
        _state.MarkRuntimeEdgesDirty();
        _state.PhaseKind = BattlePhaseKind.UnitActing;
        _state.active_unit_id = _mover.unit_id;
        _state.timeline.frozen = false;
        _mover.control_mode = "manual";
        _mover.battle_sprite_asset_id = "battle.unit.player.warrior";
        _mover.ResetTurnStateForTurnStartTyped();
        _mover.SetCurrentMovePoints(10);
        _mover.SetCurrentAp(2);
        _test.True(Runtime.GetBattleRuntime()._grid_service.PlaceUnit(_state, _mover, _start, true),
            "移动者放在测试起点。");
        ((IGameRuntimeBattleSessionPort)Runtime).SetBattleSelectedCoord(_start);
        Runtime.CommandBattleClearSkillTyped();
        Runtime.RefreshBattleRuntimeState();
    }

    private async Task RenderBoard()
    {
        _world.RenderFromRuntime(false);
        await _wait.UntilAsync(() => Panel.IsBattleRenderContentReady() && !Panel.IsLoadingBattle(),
            600, "棋盘显示");
        await _wait.FramesAsync(4);
    }

    private async Task TestMoveThroughHud(Vector2I size)
    {
        BattleMovePathResult preview = Runtime.GetBattleRuntime()._movement_service
            .ResolveMovePathResultTyped(_mover, _target);
        _test.True(preview.Allowed && preview.Path.Count > 4, "目标必须通过带转弯的多格路线可达。");
        if (!preview.Allowed) throw new InvalidOperationException(preview.Message);
        Vector2[] anchors = preview.Path.Select(coord => Board._get_coord_anchor(coord)
            + Board._render_profile.unit_anchor_bias).ToArray();
        Node2D token = Board.unit_layer.GetNode<Node2D>(_mover.unit_id.ToString());
        await SaveCapture($"start_{size.X}x{size.Y}");
        Vector2 click = Board.CoordToViewportPosition(_target)
            + new Vector2(0, Board._render_profile.tile_half_size.Y * 0.6f * Board.GetCameraZoom());
        Task input = _input.ClickAtAsync(Panel.map_viewport_container, click);
        await _wait.UntilAsync(() => Panel.IsMovementPlaying, 120, "真实鼠标触发逐格移动");
        _test.Eq(_mover.GetAnchorCoord(), _target, "运行时仍同步完成正式规则结算。");
        _test.False(token.Position.IsEqualApprox(anchors[^1]), "播放开始时人物不能直接出现在终点。");
        _test.Eq(Board._pending_snapshot.GetUnit(_mover.unit_id).AnchorCoord, _start,
            "移动期间棋盘保留命令前的 detached 快照。");
        int tu = _state.timeline.current_tu;
        _test.False(_world._runtime_proxy.Advance(10), "播放中暂停自动时间推进。");
        _test.Eq(_state.timeline.current_tu, tu, "播放不能消耗额外 TU。");
        _test.False(_world._runtime_proxy.CommandBattleWaitOrResolve().Ok, "播放期间阻止连续行动。");
        _world.RenderFromRuntime(false);
        _test.True(Panel.IsMovementPlaying, "普通刷新不能截断路径。");

        var reached = new HashSet<int> { 0 };
        bool sawInterpolation = false;
        bool capturedMiddle = false;
        for (int frame = 0; Panel.IsMovementPlaying && frame < 900; frame++)
        {
            Vector2 position = token.Position;
            int atAnchor = Array.FindIndex(anchors, anchor => anchor.DistanceTo(position) < 0.05f);
            if (atAnchor >= 0) reached.Add(atAnchor);
            else sawInterpolation = true;
            bool onPath = Enumerable.Range(1, anchors.Length - 1).Any(index =>
                Geometry2D.GetClosestPointToSegment(position, anchors[index - 1], anchors[index])
                    .DistanceTo(position) < 0.05f);
            _test.True(onPath, "每一帧人物必须位于真实路径的相邻格线段上，不能穿过拐角。");
            if (!capturedMiddle && reached.Contains(2))
            {
                capturedMiddle = true;
                await SaveCapture($"middle_{size.X}x{size.Y}");
            }
            await _wait.NextFrameAsync();
        }
        await input;
        _test.False(Panel.IsMovementPlaying, "路径必须完成并释放输入门禁。");
        _test.True(sawInterpolation, "格子之间应有连续可见的位置变化。");
        for (int index = 1; index < anchors.Length - 1; index++)
            _test.True(reached.Contains(index), $"必须依次经过中间格 {preview.Path[index]}。");
        Node2D finalToken = Board.unit_layer.GetNode<Node2D>(_mover.unit_id.ToString());
        _test.True(finalToken.Position.IsEqualApprox(anchors[^1]), "最终人物与正式落点一致。");
        _test.Eq(Board._pending_snapshot.GetUnit(_mover.unit_id).AnchorCoord, _target,
            "播放完成后自动刷新最终快照。");
        _test.Eq(_mover.GetCurrentMovePoints(), 10 - preview.Cost, "移动力只支付一次正式费用。");
        await SaveCapture($"finished_{size.X}x{size.Y}");
        ConsoleProcessOutput.WriteStandard($"Movement HUD {size}: {string.Join(" -> ", preview.Path)}; intermediate cells={reached.Count - 1}");
    }

    private async Task TestPlaybackCancellationAndBatchOrder()
    {
        PrepareTurn();
        await RenderBoard();
        Vector2I middle = _start + Vector2I.Down;
        var paths = new[]
        {
            new BattleMovementPresentation(_mover.unit_id, new[] { _start, middle }),
            new BattleMovementPresentation(_mover.unit_id, new[] { middle, middle + Vector2I.Right }),
        };
        _test.True(Panel.PlayMovements(paths), "同一 batch 的多条路径可以顺序播放。");
        Node2D token = Board.unit_layer.GetNode<Node2D>(_mover.unit_id.ToString());
        for (int frame = 0; frame < 3; frame++) Board._controller.AdvanceMovement(10);
        _test.True(Panel.IsMovementPlaying, "大帧耗时也不能跳过整条批次。");
        _test.Eq(token.GetMeta("board_coord").AsVector2I(), middle, "第二段必须从第一段终点开始。");
        Panel.HideBattle();
        _test.False(Panel.IsMovementPlaying, "隐藏棋盘清理播放与节点引用。");
        await RenderBoard();
        _test.False(Panel.IsMovementPlaying, "重入棋盘不能重播旧路径。");
        Panel.PlayMovements(paths);
        Board.Configure(new BattleBoardSnapshotBuilder().Build(_state), _start);
        _test.False(Panel.IsMovementPlaying, "直接重新配置棋盘取消旧播放。");
        Panel.PlayMovements(paths);
        _world.QueueFree();
        await _wait.FramesAsync(2);
        _world = null;
    }

    private async Task SaveCapture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        string directory = System.Environment.GetEnvironmentVariable("MAGIC_BATTLE_MOVEMENT_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        System.IO.Directory.CreateDirectory(directory);
        using Image image = Root.GetTexture().GetImage();
        _test.Eq(image.SavePng(System.IO.Path.Combine(directory, name + ".png")), Error.Ok,
            "保存原生移动画面。");
    }
}
