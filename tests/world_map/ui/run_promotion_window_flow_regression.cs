using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

/// <summary>Scene integration with accelerated training fixtures, then real viewport input.</summary>
public partial class run_promotion_window_flow_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        WorldMapSystem world = null;
        var wait = new E2eWait(this);
        var input = new E2eInputDriver(this, wait);
        try
        {
            Root.ContentScaleSize = new Vector2I(1280, 720);
            Root.Size = new Vector2I(1280, 720);
            var session = Root.GetNode<GameSession>("GameSession");
            _test.Eq(session.CreateNewSave("test"), (int)Error.Ok, "Create isolated scene fixture.");
            world = GD.Load<PackedScene>("res://scenes/main/world_map.tscn").Instantiate<WorldMapSystem>();
            Root.AddChild(world);
            await wait.UntilAsync(() => world._runtime != null && world.party_button.Size.X > 0, 120, "world scene readiness");
            var manager = world._runtime.GetCharacterManagement();
            foreach (StringName id in new StringName[] { "warrior_guard", "warrior_heavy_strike", "warrior_backstep" })
            {
                if (manager.GetMemberState("player_sword_01").progression.GetSkillProgress(id)?.is_learned != true)
                    _test.True(manager.LearnSkill("player_sword_01", id), $"Learn {id} through the production owner.");
                if (id != "warrior_backstep") manager.GrantBattleMastery("player_sword_01", id, 100000);
            }
            await wait.UntilAsync(() => world.promotion_choice_window.Visible, 60, "new eligibility opens promotion automatically");
            _test.Eq(manager.GetMemberState("player_sword_01").progression.character_level, 0,
                "The automatic prompt waits for player confirmation.");
            await Capture(world, "promotion-auto-720.png");
            await input.ClickAsync(world.promotion_choice_window.GetNode<Button>("%CancelButton"));
            await wait.FramesAsync(5);
            _test.False(world.promotion_choice_window.Visible, "Dismissed opportunities do not immediately reopen.");
            _test.True(world.promotion_reminder_button.Visible && !world.promotion_reminder_button.Disabled,
                "A persistent clickable reminder remains after dismissal.");
            await Capture(world, "promotion-reminder-720.png");
            await input.ClickAsync(world.party_button);
            await wait.UntilAsync(() => world.party_management_window.Visible, 60, "party window");
            _test.False(world.party_management_window.promotion_button.Disabled, "A trained character has a visible promotion entry.");
            await Capture(world, "party-720.png");
            await input.ClickAsync(world.party_management_window.promotion_button);
            await wait.UntilAsync(() => world.promotion_choice_window.Visible, 60, "promotion window");
            var window = world.promotion_choice_window;
            _test.False(world.party_management_window.Visible, "Party and promotion windows are mutually exclusive.");
            _test.True(new Rect2(Vector2.Zero, Root.ContentScaleSize).Encloses(window.GetNode<Control>("CenterContainer/Panel").GetGlobalRect()),
                "Multiple growth choices remain inside a 720p viewport.");
            await Capture(world, "promotion-720.png");
            await input.ClickAsync(window.GetNode<Button>("%CancelButton"));
            _test.Eq(world._runtime.GetActiveModalKind(), RuntimeModalKind.None, "Actual defer button returns to the world.");
            _test.Eq(world._runtime.GetCharacterManagement().GetMemberState("player_sword_01").progression.character_level, 0,
                "Deferring does not consume a skill or change level.");
            await input.ClickAsync(world.promotion_reminder_button);
            await wait.UntilAsync(() => window.Visible, 60, "persistent reminder reopens promotion");
            await input.ClickAsync(window.GetNode<Button>("%CancelButton"));
            await input.TapKeyAsync(Key.G);
            await wait.UntilAsync(() => window.Visible, 60, "G reopens promotion");
            if (DisplayServer.GetName() != "headless" && !string.IsNullOrEmpty(OS.GetEnvironment("MAGIC_PROMOTION_CAPTURE_DIR")))
            {
                Root.ContentScaleSize = new Vector2I(3840, 2160);
                Root.Size = new Vector2I(3840, 2160);
                await wait.FramesAsync(4);
                await Capture(world, "promotion-2160.png");
            }
            await input.ClickAsync(window.GetNode<Button>("%ConfirmButton"));
            await wait.UntilAsync(() => manager.GetMemberState("player_sword_01").progression.character_level == 1,
                60, "promotion confirmed; a newly eligible next rank may open automatically");
            var progress = world._runtime.GetCharacterManagement().GetMemberState("player_sword_01").progression;
            _test.Eq(progress.character_level, 1, "Actual confirm button submits a complete request through the scene, proxy and runtime.");
            _test.Eq(progress.GetUsedGrowthTriggerIds().Count, 1, "One click consumes exactly one growth opportunity.");

            // Prepare a battle through the runtime; the new notification is still driven by
            // production progression and exercised with actual viewport input below.
            for (int i = 0; i < 20; i++)
            {
                if (world._runtime.GetActiveModalKind() == RuntimeModalKind.Promotion)
                    world._runtime_proxy.CommandCloseActiveModal();
                else if (world._runtime.GetActiveModalKind() == RuntimeModalKind.Reward)
                    world._runtime_proxy.CommandConfirmPendingReward();
                else break;
            }
            Root.ContentScaleSize = new Vector2I(1280, 720);
            Root.Size = new Vector2I(1280, 720);
            var encounter = world._runtime.GetActiveWorldRuntimeData().EncounterAnchors
                .First(entry => entry.encounter_kind == "single");
            world._runtime.StartBattle(encounter);
            world.RenderFromRuntime(true);
            await wait.UntilAsync(() => world._runtime.IsBattleActive(), 600, "battle runtime readiness");
            await wait.UntilAsync(() => world.battle_map_panel.Visible && !world.battle_map_panel.IsLoadingBattle()
                && !world.battle_loading_overlay.Visible, 1200, "battle viewport readiness");
            if (world._runtime.GetActiveModalKind() == RuntimeModalKind.BattleStartConfirm)
                world._runtime_proxy.CommandConfirmBattleStart();
            manager.GrantBattleMastery("player_sword_01", "warrior_backstep", 100000);
            await wait.UntilAsync(() => window.Visible, 60, "new battle opportunity opens automatically");
            _test.True(world._runtime.GetBattleState().timeline.frozen, "Rendered battle prompt freezes time.");
            await Capture(world, "promotion-auto-battle-720.png");
            await input.ClickAsync(window.GetNode<Button>("%CancelButton"));
            await wait.UntilAsync(() => !window.Visible && !world.promotion_reminder_button.Disabled,
                60, "battle defer resumes input");
            _test.True(world.promotion_reminder_button.Visible && !world.promotion_reminder_button.Disabled,
                "The reminder is visible and interactive over the battle map.");
            await Capture(world, "promotion-reminder-battle-720.png");
            await input.ClickAsync(world.promotion_reminder_button);
            await wait.UntilAsync(() => window.Visible, 60, "battle reminder reopens promotion");
        }
        catch (Exception error) { _test.Fail(error.ToString()); }
        finally
        {
            await input.ReleaseAllAsync();
            if (world != null) { world.QueueFree(); await wait.NextFrameAsync(); }
            RequestTestExit(_test.Finish("Promotion window flow regression"));
        }
    }

    private async Task Capture(WorldMapSystem world, string name)
    {
        string directory = OS.GetEnvironment("MAGIC_PROMOTION_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory) || DisplayServer.GetName() == "headless") return;
        System.IO.Directory.CreateDirectory(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image screenshot = world.GetViewport().GetTexture().GetImage();
        _test.Eq(screenshot.SavePng(System.IO.Path.Combine(directory, name)), Error.Ok, "Capture real viewport.");
    }
}
