using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class run_battle_board_native_lease_regression : LifecycleTestSceneTree
{
    private static readonly PackedScene BattleBoardScene = GD.Load<PackedScene>(
        "res://scenes/ui/battle_board_2d.tscn"
    );

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        BattleBoard2D board = null;
        BattleState renderState = null;
        LifecycleAuditSnapshot baseline = null;
        try
        {
            baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            AssertPartialConstructionFailureReturnsToBaseline(baseline);

            board = BattleBoardScene.Instantiate<BattleBoard2D>();
            Root.AddChild(board);
            await ProcessFrames(1);

            BattleBoardController controller = board._controller;
            TileSet firstTileSet = controller?._tile_set;
            AssertGenerationActive(baseline, board, "初始 bind");
            AssertResourceOwnership(
                controller,
                baseline,
                expectedImageCount: 2,
                expectedImageTextureCount: 3,
                expectedStyleBoxCount: 0
            );

            renderState = BuildStyledRenderState();
            Vector2I badgeCoord = new(1, 0);
            var hitBadges = new Dictionary<Vector2I, string> { [badgeCoord] = "命中 75%" };
            var snapshotBuilder = new BattleBoardSnapshotBuilder();
            BattleBoardRenderSnapshot renderSnapshot = snapshotBuilder.Build(renderState);
            board.Configure(
                renderSnapshot,
                Vector2I.Zero,
                Array.Empty<Vector2I>(),
                new[] { badgeCoord },
                "single_unit",
                1,
                1,
                hitBadges
            );
            _test.Eq(
                board.unit_layer?.GetChildCount() ?? 0,
                1,
                "style probe 应实际绘制一个带 health bar 的单位。"
            );
            _test.True(
                board.target_highlight_layer?.GetNodeOrNull<Control>("HitBadge_1_0") != null,
                "style probe 应实际绘制 target hit badge。"
            );
            int styledOwnerCount = controller.RenderOwnerCount;
            ulong firstUnitTokenId = board.unit_layer.GetChild<Node2D>(0).GetInstanceId();
            int renderedTopCellCount = controller._count_rendered_top_cells();
            BattleUnitState styledUnit = renderState.GetUnit(renderState.active_unit_id);
            styledUnit.SetCurrentHp(17);
            BattleBoardUnitUpdateSnapshot targetedUpdate = snapshotBuilder.BuildUnitUpdate(
                renderState,
                new[] { styledUnit.unit_id }
            );
            renderSnapshot = renderSnapshot.ApplyUnitUpdate(targetedUpdate);
            controller.RefreshUnits(renderSnapshot, targetedUpdate.RequestedUnitIds);
            _test.Eq(
                board.unit_layer.GetChildCount(),
                1,
                "unit delta 应保持未增生的单位 token 数量。"
            );
            _test.Ne(
                board.unit_layer.GetChild<Node2D>(0).GetInstanceId(),
                firstUnitTokenId,
                "unit delta 应只替换目标单位 token。"
            );
            _test.Eq(
                controller._count_rendered_top_cells(),
                renderedTopCellCount,
                "unit delta 不得清空或重铺地形 TileMap。"
            );
            _test.Eq(
                controller.RenderOwnerCount,
                styledOwnerCount,
                "unit delta 不得创建新的 render-generation native owner。"
            );
            ulong targetedUnitTokenId = board.unit_layer.GetChild<Node2D>(0).GetInstanceId();
            BattleBoardUnitUpdateSnapshot fullUnitUpdate = snapshotBuilder.BuildUnitUpdate(
                renderState,
                System.Array.Empty<StringName>()
            );
            renderSnapshot = renderSnapshot.ApplyUnitUpdate(fullUnitUpdate);
            controller.RefreshUnits(renderSnapshot, fullUnitUpdate.RequestedUnitIds);
            _test.Ne(
                board.unit_layer.GetChild<Node2D>(0).GetInstanceId(),
                targetedUnitTokenId,
                "空 changed ids 应按 contract 刷新全部单位 token 的 active styling。"
            );
            _test.Eq(
                controller._count_rendered_top_cells(),
                renderedTopCellCount,
                "refresh-all-units 仍不得清空或重铺地形 TileMap。"
            );
            AssertResourceOwnership(
                controller,
                baseline,
                expectedImageCount: 2,
                expectedImageTextureCount: 3,
                expectedStyleBoxCount: 2
            );
            for (int redrawIndex = 0; redrawIndex < 4; redrawIndex++)
            {
                board.Configure(
                    renderSnapshot,
                    Vector2I.Zero,
                    Array.Empty<Vector2I>(),
                    new[] { badgeCoord },
                    "single_unit",
                    1,
                    1,
                    hitBadges
                );
                _test.Eq(
                    controller.RenderOwnerCount,
                    styledOwnerCount,
                    $"重复 redraw/configure 不得新增 StyleBox owner：{redrawIndex}"
                );
            }
            AssertDirectPngAndMissingSourceFallbacks(controller);
            AssertResourceOwnership(
                controller,
                baseline,
                expectedImageCount: 4,
                expectedImageTextureCount: 5,
                expectedStyleBoxCount: 2
            );

            board.ClearBoard();
            BattleTestFixture.DisposeBattleState(renderState);
            renderState = null;
            _test.False(board._is_bound, "ClearBoard 后场景 wrapper 应标记为未绑定。");
            _test.False(controller.HasLayersBound(), "Clear 应清空 controller 的借用 layer 字段。");
            AssertAuditBaseline(baseline, "Clear");

            board.Configure(
                null,
                new Vector2I(-1, -1),
                Array.Empty<Vector2I>(),
                Array.Empty<Vector2I>()
            );
            await ProcessFrames(1);
            AssertGenerationActive(baseline, board, "Clear 后重新 bind");
            _test.True(
                board._controller._tile_set != null
                    && !ReferenceEquals(board._controller._tile_set, firstTileSet),
                "Clear 后重新配置应创建新的 render generation TileSet。"
            );

            controller.Dispose();
            controller.Dispose();
            AssertAuditBaseline(baseline, "double Dispose");
            _test.True(
                Throws<ObjectDisposedException>(
                    () =>
                        controller.Configure(
                            null,
                            new Vector2I(-1, -1),
                            Array.Empty<Vector2I>(),
                            "single_unit",
                            1,
                            1,
                            new Dictionary<Vector2I, string>()
                        )
                ),
                "Dispose 后 controller 应保持终止态并拒绝重新配置。"
            );
            AssertAuditBaseline(baseline, "终止态异常");

            board.QueueFree();
            board = null;
            await ProcessFrames(2);
            AssertAuditBaseline(baseline, "手动 Dispose 后场景退出");

            BattleBoard2D exitTreeBoard = BattleBoardScene.Instantiate<BattleBoard2D>();
            Root.AddChild(exitTreeBoard);
            await ProcessFrames(1);
            AssertGenerationActive(baseline, exitTreeBoard, "退出树 probe");
            exitTreeBoard.QueueFree();
            await ProcessFrames(2);
            AssertAuditBaseline(baseline, "_ExitTree Dispose");
        }
        catch (Exception exception)
        {
            _test.Fail(exception.ToString());
        }
        finally
        {
            if (board != null && GodotObject.IsInstanceValid(board))
                board.QueueFree();
            await ProcessFrames(2);
            if (renderState != null)
            {
                BattleTestFixture.DisposeBattleState(renderState);
                renderState = null;
            }
            if (baseline != null)
                AssertAuditBaseline(baseline, "runner finally");
            RequestTestExit(_test.Finish("Battle board native lease regression"));
        }
    }

    private void AssertGenerationActive(
        LifecycleAuditSnapshot baseline,
        BattleBoard2D board,
        string label
    )
    {
        LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.True(board != null && board._is_bound, $"{label}: board 应已绑定。");
        _test.True(
            board?._controller?.HasLayersBound() == true,
            $"{label}: controller 应持有有效的借用 layer。"
        );
        _test.Eq(
            active.ActiveScopeCount,
            baseline.ActiveScopeCount + 1,
            $"{label}: 每个 render generation 应只有一个 SceneTree native scope。"
        );
        _test.True(
            active.ActiveOwnerCount > baseline.ActiveOwnerCount,
            $"{label}: pathless render resources 应被 native scope 持有。"
        );
        _test.Eq(
            active.ActiveOwnerCount - baseline.ActiveOwnerCount,
            board._controller.RenderOwnerCount,
            $"{label}: audit owner delta 应精确等于当前 render generation 的 owner 数。"
        );
        _test.Eq(
            active.QuarantineCount,
            baseline.QuarantineCount,
            $"{label}: 正常生产路径不得进入 quarantine。"
        );
    }

    private void AssertResourceOwnership(
        BattleBoardController controller,
        LifecycleAuditSnapshot baseline,
        int expectedImageCount,
        int expectedImageTextureCount,
        int expectedStyleBoxCount
    )
    {
        _test.True(controller != null, "board 应创建 controller。");
        if (controller == null)
            return;

        _test.True(controller._tile_set != null, "bind 应创建 TileSet。");
        _test.True(
            controller._tile_set != null
                && string.IsNullOrEmpty(controller._tile_set.ResourcePath)
                && controller.OwnsRenderResource(controller._tile_set),
            "pathless TileSet 应由 render generation lease 持有。"
        );

        var tileSets = new HashSet<TileSet>();
        if (controller._tile_set != null)
            tileSets.Add(controller._tile_set);
        foreach (BattleBoardController.TileSetCacheEntry cacheEntry in controller._tileset_cache.Values)
        {
            if (cacheEntry?.TileSet != null)
                tileSets.Add(cacheEntry.TileSet);
        }

        Texture2D generatedTexture = null;
        Texture2D pathBackedTexture = null;
        foreach ((string cacheKey, Texture2D texture) in controller._texture_cache)
        {
            if (texture == null)
                continue;
            if (cacheKey.StartsWith("__generated_", StringComparison.Ordinal))
                generatedTexture ??= texture;
            if (!string.IsNullOrEmpty(texture.ResourcePath))
                pathBackedTexture ??= texture;
            AssertTextureOwnership(controller, texture, $"texture cache:{cacheKey}");
        }
        _test.True(
            generatedTexture != null
                && string.IsNullOrEmpty(generatedTexture.ResourcePath)
                && controller.OwnsRenderResource(generatedTexture),
            "generated pathless ImageTexture 应由 render generation lease 持有。"
        );
        _test.True(
            pathBackedTexture != null
                && GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(
                    pathBackedTexture
                )
                && !GodotWrapperOwnershipRegistry.IsOwnedTransient(pathBackedTexture),
            "ResourceLoader 返回的 path-backed texture 应登记为借用内容。"
        );

        int tileSetCount = 0;
        int atlasSourceCount = 0;
        int imageCount = 0;
        int imageTextureCount = 0;
        int styleBoxCount = 0;
        foreach (IDisposable wrapper in controller.SnapshotOwnedRenderResources())
        {
            _test.True(wrapper is Resource, "BattleBoard render lease 只能持有 pathless Resource。");
            if (wrapper is not Resource resource)
                continue;
            _test.True(
                string.IsNullOrEmpty(resource.ResourcePath),
                $"render lease 不得持有 path-backed Resource：{resource.ResourcePath}"
            );
            _test.True(
                controller.OwnsRenderResource(resource),
                $"scope snapshot 中的 {resource.GetType().Name} 必须由当前 generation 持有。"
            );
            switch (resource)
            {
                case TileSet:
                    tileSetCount++;
                    break;
                case TileSetAtlasSource:
                    atlasSourceCount++;
                    break;
                case Image:
                    imageCount++;
                    break;
                case ImageTexture:
                    imageTextureCount++;
                    break;
                case StyleBoxFlat:
                    styleBoxCount++;
                    break;
                default:
                    _test.Fail(
                        $"BattleBoard render lease 出现未声明的资源类型：{resource.GetType().Name}"
                    );
                    break;
            }
        }
        int expectedAtlasSourceCount = 0;
        foreach (TileSet tileSet in tileSets)
            expectedAtlasSourceCount += tileSet.GetSourceCount();
        _test.Eq(tileSetCount, tileSets.Count, "当前 generation 应持有全部 cached TileSet。");
        _test.Eq(
            atlasSourceCount,
            expectedAtlasSourceCount,
            "当前 generation 应持有全部 TileSetAtlasSource。"
        );
        _test.Eq(
            imageCount,
            expectedImageCount,
            "当前 generation 应持有所有独立或共享的 generated Image。"
        );
        _test.Eq(
            imageTextureCount,
            expectedImageTextureCount,
            "当前 generation 应持有每个生成贴图对应的 ImageTexture。"
        );
        _test.Eq(
            styleBoxCount,
            expectedStyleBoxCount,
            "当前 generation 应只持有有限的 lazy-cached StyleBoxFlat。"
        );

        foreach (TileSet tileSet in tileSets)
        {
            _test.True(
                string.IsNullOrEmpty(tileSet.ResourcePath)
                    && controller.OwnsRenderResource(tileSet),
                "cache 中的每个 pathless TileSet 都必须由当前 generation 持有。"
            );
            for (int sourceIndex = 0; sourceIndex < tileSet.GetSourceCount(); sourceIndex++)
            {
                int sourceId = tileSet.GetSourceId(sourceIndex);
                TileSetAtlasSource source = tileSet.GetSource(sourceId) as TileSetAtlasSource;
                _test.True(
                    source != null
                        && string.IsNullOrEmpty(source.ResourcePath)
                        && controller.OwnsRenderResource(source),
                    $"TileSet source {sourceId} 必须由当前 generation 持有。"
                );
                if (source?.Texture != null)
                    AssertTextureOwnership(controller, source.Texture, $"atlas source:{sourceId}");
            }
        }

        LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(
            active.ActiveOwnerCount - baseline.ActiveOwnerCount,
            controller.RenderOwnerCount,
            "audit owner delta 应精确等于当前 Board generation 快照数。"
        );
        _test.Eq(
            controller.SnapshotOwnedRenderResources().Count,
            controller.RenderOwnerCount,
            "render scope 快照数应与 owner count 精确一致。"
        );
        _test.Eq(
            controller.RenderOwnerCount,
            tileSetCount
                + atlasSourceCount
                + imageCount
                + imageTextureCount
                + styleBoxCount,
            "当前 generation 的每个 owner 都应属于声明的 render resource 类型。"
        );
    }

    private void AssertTextureOwnership(
        BattleBoardController controller,
        Texture2D texture,
        string label
    )
    {
        if (string.IsNullOrEmpty(texture.ResourcePath))
        {
            _test.True(
                controller.OwnsRenderResource(texture),
                $"{label}: pathless texture 必须由当前 generation 持有。"
            );
            return;
        }
        _test.True(
            GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(texture)
                && !controller.OwnsRenderResource(texture),
            $"{label}: path-backed texture 必须登记为借用内容。"
        );
    }

    private void AssertDirectPngAndMissingSourceFallbacks(BattleBoardController controller)
    {
        const string directPngPath = "user://battle_board_native_lease_direct.png";
        using (Image fixture = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8))
        {
            fixture.Fill(Colors.White);
            _test.Eq(fixture.SavePng(directPngPath), Error.Ok, "direct PNG fixture 应能写入 user://。");
        }

        try
        {
            Texture2D directTexture = controller._load_texture_from_png(directPngPath);
            _test.True(
                directTexture != null
                    && string.IsNullOrEmpty(directTexture.ResourcePath)
                    && controller.OwnsRenderResource(directTexture),
                "无 import 的 direct PNG fallback ImageTexture 应且只应由当前 render lease 持有。"
            );

            var missingSpec = new BattleBoardTileSourceSpec(
                "missing_probe",
                Array.Empty<string>(),
                BattleBoardRenderProfile.LAYER_ROLE_TOP(),
                new Vector2I(8, 4),
                new Vector2I(8, 4),
                Vector2I.Zero,
                Vector2I.Zero,
                allowGeneratedFallback: true
            );
            Texture2D missingTexture = controller._build_missing_source_texture(
                new StringName("missing_probe"),
                missingSpec
            );
            _test.True(
                missingTexture != null
                    && string.IsNullOrEmpty(missingTexture.ResourcePath)
                    && controller.OwnsRenderResource(missingTexture),
                "缺图 source fallback 的 Image/ImageTexture 应由当前 render lease 持有。"
            );
        }
        finally
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(directPngPath));
        }
    }

    private static BattleState BuildStyledRenderState()
    {
        BattleState state = BattleTestFixture.BuildFlatState(
            "battle_board_native_lease_style_probe",
            new Vector2I(2, 2)
        );
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            "style_probe_unit",
            "player",
            Vector2I.Zero,
            currentHp: 25
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { unit },
            Array.Empty<BattleUnitState>()
        );
        return state;
    }

    private void AssertPartialConstructionFailureReturnsToBaseline(
        LifecycleAuditSnapshot baseline
    )
    {
        var controller = new BattleBoardController();
        var noLayers = Array.Empty<TileMapLayer>();
        var inputLayer = new TileMapLayer();
        var topBeforeFailure = new TileMapLayer();
        var invalidMiddleLayer = new TileMapLayer();
        var topAfterFailure = new TileMapLayer();
        using var sentinelTileSet = new TileSet();
        topAfterFailure.TileSet = sentinelTileSet;
        invalidMiddleLayer.Free();
        bool constructionThrew = Throws<Exception>(
            () =>
                controller.BindLayers(
                    inputLayer,
                    new[] { topBeforeFailure, invalidMiddleLayer, topAfterFailure },
                    noLayers,
                    noLayers,
                    noLayers,
                    noLayers,
                    noLayers,
                    noLayers,
                    null,
                    null,
                    null
                )
        );
        _test.True(
            constructionThrew,
            "render generation 部分构造后遇到中间无效 top layer 应抛异常。"
        );
        if (!constructionThrew)
        {
            try
            {
                controller.Clear();
            }
            catch
            {
                // The assertions below verify that best-effort cleanup still drained the lease.
            }
        }
        _test.True(inputLayer.TileSet == null, "异常清理应继续解绑 input layer。");
        _test.True(topBeforeFailure.TileSet == null, "异常清理应解绑无效 layer 之前的 top layer。");
        _test.True(topAfterFailure.TileSet == null, "异常清理应越过无效 layer 继续解绑后续 top layer。");
        AssertAuditBaseline(baseline, "partial-construction exception");

        inputLayer.Free();
        topBeforeFailure.Free();
        topAfterFailure.Free();

        var validLayer = new TileMapLayer();
        try
        {
            controller.BindLayers(
                validLayer,
                noLayers,
                noLayers,
                noLayers,
                noLayers,
                noLayers,
                noLayers,
                noLayers,
                null,
                null,
                null
            );
            LifecycleAuditSnapshot rebound = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            _test.Eq(
                rebound.ActiveScopeCount,
                baseline.ActiveScopeCount + 1,
                "partial-construction cleanup 后 controller 应能开启新的 render generation。"
            );
            controller.Clear();
            AssertAuditBaseline(baseline, "partial-construction rebind Clear");
            controller.Dispose();
            controller.Dispose();
        }
        catch (Exception exception)
        {
            _test.Fail($"partial-construction cleanup 后应能 rebind/Clear/Dispose：{exception}");
        }
        finally
        {
            try
            {
                controller.Dispose();
            }
            catch (Exception exception)
            {
                _test.Fail($"partial-construction finally Dispose 失败：{exception}");
            }
            if (GodotObject.IsInstanceValid(validLayer))
                validLayer.Free();
        }
        AssertAuditBaseline(baseline, "partial-construction final Dispose");
    }

    private void AssertAuditBaseline(LifecycleAuditSnapshot baseline, string label)
    {
        LifecycleAuditSnapshot actual = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(actual.ActiveOwnerCount, baseline.ActiveOwnerCount, $"{label}: owner 应回到 baseline。");
        _test.Eq(actual.ActiveScopeCount, baseline.ActiveScopeCount, $"{label}: scope 应回到 baseline。");
        _test.Eq(actual.ActiveLeaseCount, baseline.ActiveLeaseCount, $"{label}: lease 应保持 baseline。");
        _test.Eq(actual.QuarantineCount, baseline.QuarantineCount, $"{label}: quarantine 计数不得增长。");
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

    private async Task ProcessFrames(int count)
    {
        for (int index = 0; index < count; index++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }
}
