using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_chronicle_window_presentation_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private static readonly string[] Scenes =
    {
        "party_management_window", "party_warehouse_window", "settlement_window", "shop_window",
        "character_info_window", "contingency_setup_window", "promotion_choice_window", "mastery_reward_window",
        "bounty_board_window", "npc_quest_offer_dialog", "submap_entry_window", "save_list_window",
        "world_preset_picker_window", "display_settings_window",
    };

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        try
        {
            Root.ContentScaleSize = new Vector2I(1280, 720);
            Root.Size = new Vector2I(1280, 720);
            foreach (string scene in Scenes)
                await CheckWindowPresentation(scene);
            await CheckCharacterDetailsUseAvailableWidth();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        finally
        {
            RequestTestExit(_test.Finish("Chronicle window presentation regression"));
        }
    }

    private async Task Frames(int count = 6)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private async Task CheckWindowPresentation(string scene)
    {
        var window = GD.Load<PackedScene>($"res://scenes/ui/{scene}.tscn").Instantiate<ModalWindowShell>();
        Root.AddChild(window);
        window.Show();
        await Frames();
        var panel = window.GetNode<PanelContainer>("CenterContainer/Panel");
        var decoration = panel.GetNodeOrNull<ChronicleWindowDecoration>("WindowDecoration");
        _test.True(decoration != null, $"{scene}: authored artwork should load on the modal instance.");
        if (decoration != null)
        {
            var art = decoration.GetNode<TextureRect>("HeaderVignette");
            _test.True(panel.GetGlobalRect().Encloses(art.GetGlobalRect()), $"{scene}: illustration must stay inside the modal.");
            _test.True(art.Size.Y <= 100 && art.Size.X <= 320, $"{scene}: art occupies only a small header vignette.");
            _test.Eq(decoration.MouseFilter, Control.MouseFilterEnum.Ignore, $"{scene}: decoration must not consume clicks.");
            _test.Eq(art.MouseFilter, Control.MouseFilterEnum.Ignore, $"{scene}: artwork must not consume clicks.");
        }
        _test.True(new Rect2(Vector2.Zero, window.Size).Encloses(panel.GetGlobalRect()), $"{scene}: modal fits the 720p viewport.");
        window.Hide();
        _test.Eq(panel.Modulate.A, 1.0f, $"{scene}: canceling entrance restores panel opacity.");
        window.Show();
        window.QueueFree();
        await Frames();
    }

    private async Task CheckCharacterDetailsUseAvailableWidth()
    {
        var window = GD.Load<PackedScene>("res://scenes/ui/character_info_window.tscn").Instantiate<CharacterInfoWindow>();
        Root.AddChild(window);
        window.ShowCharacter(new GameRuntimeCharacterInfoContext(GameRuntimeCharacterInfoSource.World, "旅者", "", "",
            new[] { new GameRuntimeCharacterInfoSection("基本信息", new[] { GameRuntimeCharacterInfoEntry.Pair("身份", "普通人类旅者") }) }));
        await Frames();
        Label value = window.FindChildren("*", "Label", true, false).OfType<Label>().Single(label => label.Text == "普通人类旅者");
        _test.True(value.Size.X > 180, "Character values use the details column instead of wrapping one character per line.");
        _test.True(value.Size.Y < 60, "A short character value stays on a readable horizontal line.");
        window.QueueFree();
        await Frames();
    }
}
