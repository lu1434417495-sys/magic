using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_equipment_movement_trail_overlay_regression : LifecycleTestSceneTree
{
    private static readonly PackedScene BattleBoardScene = GD.Load<PackedScene>(
        "res://scenes/ui/battle_board_2d.tscn"
    );
    private static readonly StringName FireStepOverlayId =
        "phoenix_rebirth_fire_step";
    private static readonly StringName FlameChargeOverlayId =
        "phoenix_rebirth_flame_charge_trail";
    private static readonly StringName UnknownOverlayId =
        "fixture_unmapped_equipment_trail";
    private static readonly Vector2I FireStepCoord = new(0, 0);
    private static readonly Vector2I FlameChargeCoord = new(1, 0);
    private static readonly Vector2I UnknownCoord = new(2, 0);

    private readonly TestHarness _test = new();

    public override async void _Initialize()
    {
        BattleState state = null;
        BattleBoard2D board = null;
        try
        {
            AssertFormalOverlaySourceSpecs();
            state = BuildState();
            BattleBoardRenderSnapshot snapshot = new BattleBoardSnapshotBuilder().Build(state);
            AssertSnapshotCarriesActiveTrailOverlays(snapshot);

            board = BattleBoardScene.Instantiate<BattleBoard2D>();
            Root.AddChild(board);
            await ProcessFrames(1);
            board.Configure(
                snapshot,
                new Vector2I(-1, -1),
                Array.Empty<Vector2I>(),
                Array.Empty<Vector2I>(),
                "single_unit",
                1,
                1,
                new Dictionary<Vector2I, string>()
            );
            await ProcessFrames(1);

            AssertRenderedOverlay(board, FireStepOverlayId, FireStepCoord, "火步");
            AssertRenderedOverlay(
                board,
                FlameChargeOverlayId,
                FlameChargeCoord,
                "火焰冲锋"
            );
            _test.Eq(
                board._controller._get_source_id(UnknownOverlayId, UnknownCoord),
                -1,
                "未映射 trail id 不应被伪装成已注册 overlay source。"
            );
            _test.Eq(
                GetOverlayLayer(board, UnknownCoord)?.GetCellSourceId(UnknownCoord) ?? -1,
                -1,
                "未映射危险格应保持未绘制，证明正式 source 映射是可杀回归前置。"
            );
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(board))
            {
                board.QueueFree();
                await ProcessFrames(1);
            }
            BattleTestFixture.DisposeBattleState(state);
            RequestTestExit(_test.Finish("Equipment movement trail overlay regression"));
        }
    }

    private void AssertFormalOverlaySourceSpecs()
    {
        BattleBoardRenderProfile profile =
            BattleBoardRenderProfile.ForTerrainProfileId("default");
        AssertFormalOverlaySourceSpec(profile, FireStepOverlayId, "火步");
        AssertFormalOverlaySourceSpec(profile, FlameChargeOverlayId, "火焰冲锋");
    }

    private void AssertFormalOverlaySourceSpec(
        BattleBoardRenderProfile profile,
        StringName overlayId,
        string label
    )
    {
        BattleBoardTileSourceSpec sourceSpec = profile
            .GetSourceSpecs()
            .FirstOrDefault(candidate => candidate?.Key == overlayId);
        _test.True(sourceSpec != null, $"{label} trail 应进入正式 render source spec 表。");
        if (sourceSpec == null)
            return;
        _test.Eq(
            sourceSpec.LayerRole,
            BattleBoardRenderProfile.LAYER_ROLE_OVERLAY(),
            $"{label} trail 应由 overlay layer owner 渲染。"
        );
        _test.True(
            sourceSpec.AllowGeneratedFallback,
            $"{label} trail 在无专用美术时应生成可见 fallback，而不是静默丢格。"
        );
    }

    private void AssertSnapshotCarriesActiveTrailOverlays(
        BattleBoardRenderSnapshot snapshot
    )
    {
        _test.True(snapshot != null, "危险格应能投影为 battle board snapshot。");
        if (snapshot == null)
            return;
        _test.True(
            snapshot.GetCell(FireStepCoord)?.TerrainOverlayIds.Contains(FireStepOverlayId)
                == true,
            "火步危险格应把 render_overlay_id 投影到 presentation snapshot。"
        );
        _test.True(
            snapshot
                    .GetCell(FlameChargeCoord)
                    ?.TerrainOverlayIds.Contains(FlameChargeOverlayId)
                == true,
            "火焰冲锋危险格应把 render_overlay_id 投影到 presentation snapshot。"
        );
    }

    private void AssertRenderedOverlay(
        BattleBoard2D board,
        StringName overlayId,
        Vector2I coord,
        string label
    )
    {
        _test.True(board?._controller != null, $"{label} trail 应有正式 board controller owner。");
        if (board?._controller == null || board.overlay_layers.Count == 0)
            return;

        TileMapLayer overlayLayer = GetOverlayLayer(board, coord);
        _test.True(overlayLayer != null, $"{label}危险格应解析到对应高度 overlay layer。");
        if (overlayLayer == null)
            return;

        int sourceId = board._controller._get_source_id(overlayId, coord);
        _test.True(sourceId >= 0, $"{label} trail id 应解析为正式 TileSet source。");
        _test.Eq(
            overlayLayer.GetCellSourceId(coord),
            sourceId,
            $"{label}危险格应实际写入对应高度 OverlayH 层，而不只是存在配置。"
        );

        TileSetAtlasSource atlasSource =
            overlayLayer.TileSet?.GetSource(sourceId) as TileSetAtlasSource;
        _test.True(atlasSource?.Texture != null, $"{label} trail source 应持有可见纹理。");
        _test.True(
            atlasSource?.GetTileData(Vector2I.Zero, 0) != null,
            $"{label} trail source 应创建可绘制 atlas tile。"
        );
        Image renderedImage = atlasSource?.Texture?.GetImage();
        try
        {
            _test.True(
                renderedImage != null && !renderedImage.IsEmpty(),
                $"{label} trail source 应能读取非空像素数据。"
            );
            if (renderedImage != null && !renderedImage.IsEmpty())
            {
                Color centerPixel = renderedImage.GetPixel(
                    renderedImage.GetWidth() / 2,
                    renderedImage.GetHeight() / 2
                );
                _test.True(
                    centerPixel.A > 0.05f,
                    $"{label}危险格中心像素应可见，不能注册为全透明占位。"
                );
            }
        }
        finally
        {
            renderedImage?.Dispose();
        }
    }

    private static TileMapLayer GetOverlayLayer(BattleBoard2D board, Vector2I coord)
    {
        BattleBoardCellSnapshot cell = board?._controller?._snapshot?.GetCell(coord);
        if (cell == null || board.overlay_layers.Count == 0)
            return null;
        int heightIndex = cell.Height - BattleBoardRenderProfile.MinimumHeight;
        return board.overlay_layers[heightIndex];
    }

    private static BattleState BuildState()
    {
        BattleState state = BattleTestFixture.BuildFlatState(
            "equipment_movement_trail_overlay",
            new Vector2I(3, 1)
        );
        state.terrain_profile_id = "default";
        state.GetCell(FireStepCoord).timed_terrain_effects.Add(
            BuildTrailEffect("fixture_fire_step", FireStepOverlayId)
        );
        state.GetCell(FlameChargeCoord).timed_terrain_effects.Add(
            BuildTrailEffect("fixture_flame_charge", FlameChargeOverlayId)
        );
        state.GetCell(UnknownCoord).timed_terrain_effects.Add(
            BuildTrailEffect("fixture_unknown_trail", UnknownOverlayId)
        );
        return state;
    }

    private static BattleTerrainEffectState BuildTrailEffect(
        StringName effectId,
        StringName overlayId
    ) =>
        new()
        {
            field_instance_id = $"field.{effectId}",
            effect_id = effectId,
            effect_type = "damage",
            render_overlay_id = overlayId,
            overlay_priority = 100,
            source_unit_id = "fixture_phoenix_source",
            source_skill_id = "fixture_equipment_movement_trail",
            target_team_filter = "any",
            damage_tag = "fire",
            remaining_tu = 60,
            stack_behavior = "refresh",
        };

    private async Task ProcessFrames(int count)
    {
        for (int frame = 0; frame < count; frame++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }
}
