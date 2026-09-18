using System;
using System.IO;
using System.Threading.Tasks;
using Godot;

// Temporary native capture collector; archived outside the compile tree after use.
public partial class capture_world_party_marker : E2eSceneTree
{
    private protected override string ScenarioLabel => "World party marker native capture";

    private protected override async Task RunScenarioAsync()
    {
        RequireIsolatedUserData();
        var display = new DisplaySettingsService();
        display.SaveSettings(new(new Vector2I(1280, 720), false));
        WorldMapSystem world = await CreateTestGameThroughUiAsync("旅途");
        await Wait.FramesAsync(8);
        string output = OS.GetEnvironment("MAGIC_PARTY_CAPTURE_OUTPUT");
        Directory.CreateDirectory(output);
        string phase = OS.GetEnvironment("MAGIC_PARTY_CAPTURE_PHASE");
        foreach (Vector2I resolution in new[] { new Vector2I(1280, 720), new Vector2I(3840, 2160) })
        {
            display.ApplySettings(new(resolution, false), Root);
            await Wait.UntilAsync(() => Root.Size == resolution, 120, "native capture resolution");
            await Wait.FramesAsync(8);
            await Capture(output, $"{phase}_{resolution.X}x{resolution.Y}.png", resolution);
        }
        Vector2I before = world._runtime.GetPlayerCoord();
        await Input.TapKeyAsync(Key.D);
        await Wait.FramesAsync(8);
        Test.True(world._runtime.GetPlayerCoord() != before, "Movement input advances the party coordinate.");
        Test.True(world.world_map_view.IsVisibleInTree(), "World map remains visible after movement.");
        await Capture(output, $"{phase}_moved_3840x2160.png", new Vector2I(3840, 2160));
    }

    private async Task Capture(string output, string name, Vector2I expected)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image frame = Root.GetTexture().GetImage();
        Test.Eq(frame.GetSize(), expected, "Capture has native pixel dimensions.");
        Test.Eq(frame.SavePng(Path.Combine(output, name)), Error.Ok, "Native frame saved.");
    }
}
