using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class run_battle_player_sprite_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        BattleBoard2D board = null;
        BattleState state = null;
        try
        {
            state = BuildState();
            var builder = new BattleBoardSnapshotBuilder();
            BattleBoardRenderSnapshot snapshot = builder.Build(state);
            foreach (string kind in new[] { "warrior", "mage", "archer" })
            {
                _test.Eq(snapshot.GetUnit(kind).BattleSpriteAssetId,
                    new StringName($"battle.unit.player.{kind}"), "Party loadout selects basic artwork.");
                _test.Eq(state.GetUnit(kind).battle_sprite_asset_id, new StringName(""),
                    "Rendering must not write inferred artwork into canonical state.");
            }
            _test.Eq(snapshot.GetUnit("enemy").BattleSpriteAssetId, new StringName(""),
                "A non-party unit without authored artwork keeps its glyph.");

            board = EngineAssetAccess.ResolveCodeAssetBorrowed<PackedScene>(
                "res://scenes/ui/battle_board_2d.tscn").Instantiate<BattleBoard2D>();
            Root.AddChild(board);
            await Frames(2);
            board.SetViewportSize(new Vector2(1280, 720));
            board.Configure(snapshot, new Vector2I(4, 3), Array.Empty<Vector2I>(),
                Array.Empty<Vector2I>(), "single_unit", 1, 1, new Dictionary<Vector2I, string>());
            await Frames(3);
            foreach (string kind in new[] { "warrior", "mage", "archer" })
                AssertSprite(board, kind);

            string captureDirectory = OS.GetEnvironment("MAGIC_BATTLE_SPRITE_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(captureDirectory) && DisplayServer.GetName() != "headless")
            {
                System.IO.Directory.CreateDirectory(captureDirectory);
                foreach (Vector2I size in new[] { new Vector2I(1280, 720), new Vector2I(3840, 2160) })
                {
                    Root.ContentScaleSize = Vector2I.Zero;
                    Root.Size = size;
                    board.SetViewportSize(size);
                    await Frames(8);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using Image capture = Root.GetTexture().GetImage();
                    _test.Eq(capture.SavePng(System.IO.Path.Combine(captureDirectory,
                        $"battle_players_{size.X}x{size.Y}.png")), Error.Ok, "Native screenshot saves.");
                }
            }

            BattleUnitState warrior = state.GetUnit("warrior");
            SetWeapon(warrior, "staff");
            BattleBoardUnitUpdateSnapshot update = builder.BuildUnitUpdate(state, new[] { warrior.unit_id });
            BattleBoardRenderSnapshot updated = snapshot.ApplyUnitUpdate(update);
            _test.Eq(snapshot.GetUnit("warrior").BattleSpriteAssetId,
                new StringName("battle.unit.player.warrior"), "Published snapshot remains detached.");
            _test.Eq(updated.GetUnit("warrior").BattleSpriteAssetId,
                new StringName("battle.unit.player.mage"), "Equipment delta changes artwork.");
            board.RefreshUnits(update);
            _test.True(ReferenceEquals(board.unit_layer.GetNode<Sprite2D>("warrior/UnitSprite").Texture,
                EngineAssetAccess.ResolveContentAssetBorrowed<Texture2D>("battle.unit.player.mage")),
                "Equipment delta reaches the actual board sprite.");

            warrior.battle_sprite_asset_id = "battle.unit.enemy.wolf";
            _test.Eq(builder.Build(state).GetUnit("warrior").BattleSpriteAssetId,
                new StringName("battle.unit.enemy.wolf"), "Explicit artwork wins over loadout defaults.");
            await AssertDirectionalSprites(board, state, builder, captureDirectory);
        }
        catch (Exception exception)
        {
            _test.Fail($"Player sprite regression exception: {exception}");
        }
        finally
        {
            if (board != null)
            {
                board.QueueFree();
                await Frames(1);
            }
            if (state != null)
                BattleTestFixture.DisposeBattleState(state);
            RequestTestExit(_test.Finish("Battle player sprite regression"));
        }
    }

    private void AssertSprite(BattleBoard2D board, string unitId)
    {
        Node2D token = board.unit_layer.GetNode<Node2D>(unitId);
        Sprite2D sprite = token.GetNodeOrNull<Sprite2D>("UnitSprite");
        _test.True(sprite?.Texture != null, "Party member renders real Texture2D artwork.");
        _test.False(token.GetNode<Label>("UnitGlyphLabel").Visible, "Artwork replaces the glyph.");
        if (sprite?.Texture == null)
            return;
        using Image image = sprite.Texture.GetImage();
        if (image.IsCompressed())
            image.Decompress();
        _test.True(image.GetPixel(0, 0).A == 0 && image.GetPixel(image.GetWidth() / 2,
            image.GetHeight() / 2).A > 0.9f, "Sprite has transparent surroundings and an opaque body.");
        _test.True(token.GetNode<Control>("HealthBarRoot").Position.Y < sprite.Position.Y,
            "Health bar stays above the character's body.");
    }

    private async Task AssertDirectionalSprites(BattleBoard2D board, BattleState state,
        BattleBoardSnapshotBuilder builder, string captureDirectory)
    {
        string[] kinds = { "warrior", "mage", "archer" };
        StringName[] unitIds = { "warrior", "mage", "archer" };
        foreach (string direction in new[] { "front_left", "front_right", "back_left", "back_right" })
        {
            foreach (string kind in kinds)
                state.GetUnit(kind).battle_sprite_asset_id = $"battle.unit.player.{kind}.{direction}";
            board.RefreshUnits(builder.BuildUnitUpdate(state, unitIds));
            await Frames(2);
            foreach (string kind in kinds)
            {
                AssertSprite(board, kind);
                Node2D token = board.unit_layer.GetNode<Node2D>(kind);
                Sprite2D sprite = token.GetNode<Sprite2D>("UnitSprite");
                Texture2D texture = sprite.Texture;
                _test.True(texture is AtlasTexture,
                    "Each authored direction resolves a single atlas frame on the actual board.");
                _test.True(ReferenceEquals(texture, EngineAssetAccess.ResolveContentAssetBorrowed<Texture2D>(
                    state.GetUnit(kind).battle_sprite_asset_id)), "Direction asset delta reaches the sprite.");
                _test.Eq(texture.GetSize(), new Vector2(768, 1024),
                    "Directional frames expose the same logical canvas.");
                Vector2 displayedFoot = sprite.Position
                    + (new Vector2(384, 940) - texture.GetSize() * 0.5f) * sprite.Scale;
                _test.True(displayedFoot.DistanceTo(new Vector2(0,
                    -BattleBoardRenderProfile.DEFAULT_UNIT_ANCHOR_BIAS().Y)) < 1,
                    "The shared directional foot pivot stays on the ground ring after each delta.");
            }
            if (string.IsNullOrEmpty(captureDirectory) || DisplayServer.GetName() == "headless")
                continue;
            foreach (Vector2I size in new[] { new Vector2I(1280, 720), new Vector2I(3840, 2160) })
            {
                Root.ContentScaleSize = Vector2I.Zero;
                Root.Size = size;
                board.SetViewportSize(size);
                await Frames(8);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image capture = Root.GetTexture().GetImage();
                _test.Eq(capture.SavePng(System.IO.Path.Combine(captureDirectory,
                    $"battle_players_{direction}_{size.X}x{size.Y}.png")), Error.Ok,
                    "Directional sprite native screenshot saves.");
            }
        }
    }

    private BattleState BuildState()
    {
        var state = new BattleState { battle_id = "player_sprites", map_size = new Vector2I(9, 7),
            terrain_profile_id = "canyon", phase = "unit_acting", active_unit_id = "warrior" };
        var cells = new List<BattleCellState>();
        for (int y = 0; y < 7; y++)
        for (int x = 0; x < 9; x++)
            cells.Add(new BattleCellState { coord = new Vector2I(x, y),
                base_terrain = y == 0 ? "forest" : x == 8 ? "water" : "land" });
        state.SetCells(cells);
        var grid = new BattleGridService();
        var units = new[] { ("warrior", "战士", "sword", new Vector2I(3, 4)),
            ("mage", "法师", "staff", new Vector2I(4, 3)),
            ("archer", "弓箭手", "bow", new Vector2I(5, 2)),
            ("enemy", "敌", "unarmed", new Vector2I(7, 4)) };
        foreach ((string id, string name, string family, Vector2I coord) in units)
        {
            var unit = new BattleUnitState { unit_id = id, source_member_id = id == "enemy" ? "" : id,
                display_name = name, faction_id = id == "enemy" ? "hostile" : "player" }
                .WithCombatResourcesForTest(hp: 40, mp: 20, stamina: 20, ap: 2, isAlive: true);
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 40);
            SetWeapon(unit, family);
            state.SetUnit(unit);
            if (id == "enemy") state.enemy_unit_ids.Add(id);
            else state.ally_unit_ids.Add(id);
            _test.True(grid.PlaceUnit(state, unit, coord, true), "Fixture placement succeeds.");
        }
        return state;
    }

    private static void SetWeapon(BattleUnitState unit, StringName family) =>
        unit.ApplyWeaponProjectionTyped(new WeaponProjection {
            weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
            weapon_family = family, weapon_profile_type_id = family, weapon_range_type = "melee" });

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }
}
