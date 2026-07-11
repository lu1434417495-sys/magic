using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public partial class capture_canyon_battle_board : LifecycleTestSceneTree
{
    private static readonly PackedScene BattleBoardScene = GD.Load<PackedScene>(
        "res://scenes/ui/battle_board_2d.tscn"
    );

    private static readonly Vector2I ViewportSize = new(1280, 720);
    private static readonly Vector2I TestMapSize = new(19, 11);
    private static readonly Vector2I TestWorldCoord = new(7, 11);
    private const int TestSeed = 424242;
    private const int MaxReadyFrames = 24;
    private const string OutputPath = "res://battle_board_canyon_capture.png";
    private const string HeadlessSignatureOutputPath =
        "res://battle_board_canyon_capture.signature.txt";

    private readonly BattleGridService _gridService = new();
    private readonly TestHarness _test = new();

    public override async void _Initialize()
    {
        int exitCode = await Run();
        RequestTestExit(_test.Finish("Canyon battle board capture", exitCode));
    }

    private async Task<int> Run()
    {
        GDictionary layout = BuildCanyonLayout();
        BattleState state = BuildState(layout);
        Root.Size = ViewportSize;

        var background = new ColorRect
        {
            Color = new Color(0.12f, 0.08f, 0.06f),
            Size = ViewportSize,
        };
        Root.AddChild(background);

        BattleBoard2D board = BattleBoardScene.Instantiate<BattleBoard2D>();
        Root.AddChild(board);
        await ProcessFrames(1);

        Vector2I selectedCoord = DictVector2I(layout, "player_coord");
        board.SetViewportSize(ViewportSize);
        board.Configure(
            state,
            selectedCoord,
            new GVector2IArray(),
            new GVector2IArray(),
            "single_unit",
            1,
            1,
            new GDictionary()
        );
        if (!await WaitForBoardRenderReady(board))
        {
            GD.PushError("Battle board capture did not reach render-ready state before screenshot.");
            return 1;
        }
        if (!ValidateUnitPlacement(state, "ally_capture", DictVector2I(layout, "player_coord"), "ally_capture"))
            return 1;
        if (!ValidateUnitPlacement(state, "enemy_capture", DictVector2I(layout, "enemy_coord"), "enemy_capture"))
            return 1;

        if (DisplayServer.GetName() == "headless")
        {
            Error signatureError = SaveHeadlessBoardSignature(board);
            if (signatureError != Error.Ok)
            {
                GD.PushError("Failed to save battle board headless signature.");
                return 1;
            }
            GD.Print(
                $"Saved battle board headless signature to {ProjectSettings.GlobalizePath(HeadlessSignatureOutputPath)}"
            );
            return 0;
        }

        Image image = Root.GetTexture().GetImage();
        string outputPath = ProjectSettings.GlobalizePath(OutputPath);
        Error saveError = image.SavePng(outputPath);
        if (saveError != Error.Ok)
        {
            GD.PushError($"Failed to save battle board capture: {outputPath}");
            return 1;
        }
        GD.Print($"Saved battle board capture to {outputPath}");
        return 0;
    }

    private GDictionary BuildCanyonLayout()
    {
        var generator = new BattleTerrainGenerator();
        return generator.GenerateTyped(
            BuildEncounterAnchor(),
            TestSeed,
            new GDictionary
            {
                ["world_coord"] = TestWorldCoord,
                ["world_seed"] = TestSeed,
                ["battle_terrain_profile"] = "canyon",
                ["battle_map_size"] = TestMapSize,
            }
        );
    }

    private static EncounterAnchorData BuildEncounterAnchor() =>
        new()
        {
            entity_id = "battle_board_capture",
            display_name = "测试遭遇",
            faction_id = "hostile",
            region_tag = "canyon",
            world_coord = TestWorldCoord,
            encounter_kind = "single",
        };

    private BattleState BuildState(GDictionary layout)
    {
        var state = new BattleState
        {
            battle_id = "battle_board_capture",
            seed = TestSeed,
            map_size = DictVector2I(layout, "map_size"),
            world_coord = TestWorldCoord,
            terrain_profile_id = DictStringName(layout, "terrain_profile_id", "default"),
            ally_unit_ids = new GStringNameArray(),
            enemy_unit_ids = new GStringNameArray(),
        };
        state.SetCellsFromDictionary(CloneCells(DictDict(layout, "cells")));
        BattleUnitState ally = BuildUnit("ally_capture", "队员", "player");
        BattleUnitState enemy = BuildUnit("enemy_capture", "敌人", "hostile");
        RegisterAndPlace(state, ally, DictVector2I(layout, "player_coord"), false);
        RegisterAndPlace(state, enemy, DictVector2I(layout, "enemy_coord"), true);
        state.active_unit_id = ally.unit_id;
        return state;
    }

    private static BattleUnitState BuildUnit(StringName unitId, string displayName, StringName factionId)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            current_hp = 12,
            current_mp = 4,
            current_stamina = 8,
            current_ap = 1,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 12);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 4);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 8);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 1);
        unit.RefreshFootprint();
        return unit;
    }

    private void RegisterAndPlace(BattleState state, BattleUnitState unit, Vector2I coord, bool enemy)
    {
        state.SetUnit(unit);
        if (enemy)
            state.enemy_unit_ids.Add(unit.unit_id);
        else
            state.ally_unit_ids.Add(unit.unit_id);
        _gridService.PlaceUnit(state, unit, coord, true);
    }

    private static GDictionary CloneCells(GDictionary cells)
    {
        var cloned = new GDictionary();
        foreach (Variant coordValue in cells.Keys)
        {
            if (coordValue.VariantType != Variant.Type.Vector2I)
                continue;
            if (BattleCellState.TryReadCellPayload(cells[coordValue], out BattleCellState cell) && cell != null)
                cloned[coordValue.AsVector2I()] = cell.DuplicateCell().ToDictionary();
        }
        return cloned;
    }

    private async Task<bool> WaitForBoardRenderReady(BattleBoard2D board)
    {
        if (board == null)
            return false;
        if (board.IsRenderContentReady())
            return true;
        for (int frame = 0; frame < MaxReadyFrames; frame++)
        {
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
            if (board.IsRenderContentReady())
                return true;
        }
        return false;
    }

    private async Task ProcessFrames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private bool ValidateUnitPlacement(
        BattleState state,
        StringName unitId,
        Vector2I expectedCoord,
        string label
    )
    {
        BattleUnitState unit = state.GetUnit(unitId);
        if (unit == null || unit.coord != expectedCoord)
        {
            GD.PushError($"{label} should be anchored at {expectedCoord} before capture.");
            return false;
        }
        BattleCellState cell = state.GetCell(expectedCoord);
        BattleUnitState occupant =
            cell != null && !string.IsNullOrEmpty(cell.occupant_unit_id.ToString())
                ? state.GetUnit(cell.occupant_unit_id)
                : null;
        if (occupant == null || occupant.unit_id != unitId)
        {
            GD.PushError($"{label} should occupy {expectedCoord} before capture.");
            return false;
        }
        return true;
    }

    private static Error SaveHeadlessBoardSignature(BattleBoard2D board)
    {
        using FileAccess file = FileAccess.Open(
            ProjectSettings.GlobalizePath(HeadlessSignatureOutputPath),
            FileAccess.ModeFlags.Write
        );
        if (file == null)
            return FileAccess.GetOpenError();
        file.StoreString(string.Join("\n", CaptureBoardSignature(board)));
        return Error.Ok;
    }

    private static List<string> CaptureBoardSignature(BattleBoard2D board)
    {
        var lines = new List<string> { "signature_version|battle_board_canyon|4" };
        BattleBoardRenderProfile profile = board._render_profile;
        if (profile != null)
        {
            lines.Add(
                $"render_profile|{profile.terrain_profile_id}|{profile.render_profile_id}|height_step={profile.visual_height_step:F2}|asset_dir={profile.asset_dir}"
            );
        }
        lines.Add(CaptureSummary(board));
        foreach (string layerName in BuildLayerNames())
        {
            TileMapLayer layer = board.GetNodeOrNull<TileMapLayer>(layerName);
            if (layer == null)
                continue;
            List<Vector2I> usedCells = new();
            foreach (Vector2I coord in layer.GetUsedCells())
                usedCells.Add(coord);
            usedCells.Sort((left, right) => left.Y == right.Y ? left.X.CompareTo(right.X) : left.Y.CompareTo(right.Y));
            foreach (Vector2I coord in usedCells)
                lines.Add($"{layerName}|{coord.X},{coord.Y}|{layer.GetCellSourceId(coord)}");
        }
        AppendNodeLayer(lines, board.prop_layer, "prop");
        AppendNodeLayer(lines, board.unit_layer, "unit");
        return lines;
    }

    private static string CaptureSummary(BattleBoard2D board)
    {
        int topCells = 0;
        int minTopHeight = int.MaxValue;
        int maxTopHeight = int.MinValue;
        int faceCells = 0;
        for (int height = 0; height <= 8; height++)
        {
            TileMapLayer top = board.GetNodeOrNull<TileMapLayer>($"TopH{height}");
            int usedTop = top?.GetUsedCells().Count ?? 0;
            if (usedTop > 0)
            {
                minTopHeight = Mathf.Min(minTopHeight, height);
                maxTopHeight = Mathf.Max(maxTopHeight, height);
                topCells += usedTop;
            }
            foreach (string prefix in new[] { "EdgeDropEastH", "EdgeDropSouthH", "WallEastH", "WallSouthH" })
            {
                TileMapLayer face = board.GetNodeOrNull<TileMapLayer>($"{prefix}{height}");
                faceCells += face?.GetUsedCells().Count ?? 0;
            }
        }
        if (minTopHeight == int.MaxValue)
        {
            minTopHeight = -1;
            maxTopHeight = -1;
        }
        return $"v2_capture_summary|top_height_range={minTopHeight}..{maxTopHeight}|top_cells={topCells}|face_cells={faceCells}|props={board.prop_layer?.GetChildCount() ?? 0}|units={board.unit_layer?.GetChildCount() ?? 0}";
    }

    private static void AppendNodeLayer(List<string> lines, Node2D layer, string prefix)
    {
        if (layer == null)
            return;
        foreach (Node child in layer.GetChildren())
        {
            lines.Add(
                $"{prefix}|{child.Name}|{child.GetMeta("board_coord", Vector2I.Zero)}|{(child is Node2D node2D ? node2D.ZIndex : 0)}"
            );
        }
    }

    private static List<string> BuildLayerNames()
    {
        var names = new List<string>();
        AddLayerNames(names, "TopH", 0, 8);
        AddLayerNames(names, "EdgeDropEastH", 1, 8);
        AddLayerNames(names, "EdgeDropSouthH", 1, 8);
        AddLayerNames(names, "WallEastH", 0, 8);
        AddLayerNames(names, "WallSouthH", 0, 8);
        AddLayerNames(names, "OverlayH", 0, 8);
        AddLayerNames(names, "MarkerH", 0, 8);
        return names;
    }

    private static void AddLayerNames(List<string> names, string prefix, int start, int end)
    {
        for (int height = start; height <= end; height++)
            names.Add($"{prefix}{height}");
    }

    private static GDictionary DictDict(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) && dict[key].VariantType == Variant.Type.Dictionary
            ? dict[key].AsGodotDictionary()
            : new GDictionary();

    private static Vector2I DictVector2I(GDictionary dict, Variant key, Vector2I fallback = default) =>
        dict != null && dict.ContainsKey(key) && dict[key].VariantType == Variant.Type.Vector2I
            ? dict[key].AsVector2I()
            : fallback;

    private static StringName DictStringName(GDictionary dict, Variant key, StringName fallback = default)
    {
        if (dict == null || !dict.ContainsKey(key))
            return fallback;
        Variant value = dict[key];
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => fallback,
        };
    }
}
