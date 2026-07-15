using System.Threading.Tasks;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_modal_window_shell_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    private static readonly PackedScene CharacterInfoWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/character_info_window.tscn"
    );
    private static readonly PackedScene MasteryRewardWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/mastery_reward_window.tscn"
    );
    private static readonly PackedScene SubmapEntryWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/submap_entry_window.tscn"
    );

    public override async void _Initialize()
    {
        await TestEscapeClosesCharacterInfoWindow();
        await TestEscapeDoesNotCloseMasteryRewardWindow();
        await TestSubmapEntryEscapeHonorsDismissFlag();
        RequestTestExit(_test.Finish("Modal window shell regression"));
    }

    private async Task TestEscapeClosesCharacterInfoWindow()
    {
        var window = CharacterInfoWindowScene.Instantiate<CharacterInfoWindow>();
        Root.AddChild(window);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        bool closedEmitted = false;
        window.closed += () => closedEmitted = true;
        window.ShowCharacter(
            new GDictionary
            {
                ["display_name"] = "测试角色",
                ["meta_label"] = "测试单位",
                ["status_label"] = "",
                ["sections"] = new Godot.Collections.Array
                {
                    new GDictionary
                    {
                        ["title"] = "基础概览",
                        ["entries"] = new Godot.Collections.Array
                        {
                            new GDictionary { ["label"] = "职业", ["value"] = "测试" },
                        },
                    },
                },
            }
        );
        _test.True(window.Visible, "ShowCharacter 后窗口应可见。");

        window._UnhandledInput(MakeEscape());
        _test.False(window.Visible, "Esc 应关闭人物信息窗（ModalWindowShell 通道）。");
        _test.True(closedEmitted, "Esc 关闭应发 closed 信号，与点遮罩/关闭按钮一致。");

        window.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private async Task TestEscapeDoesNotCloseMasteryRewardWindow()
    {
        var window = MasteryRewardWindowScene.Instantiate<MasteryRewardWindow>();
        Root.AddChild(window);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        window.ShowReward(new PendingCharacterReward { member_name = "测试成员" });
        _test.True(window.Visible, "ShowReward 后窗口应可见。");

        window._UnhandledInput(MakeEscape());
        _test.True(window.Visible, "奖励窗必须显式确认，Esc 不应关闭。");

        window.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private async Task TestSubmapEntryEscapeHonorsDismissFlag()
    {
        var window = SubmapEntryWindowScene.Instantiate<SubmapEntryWindow>();
        Root.AddChild(window);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        int cancelledCount = 0;
        window.cancelled += () => cancelledCount += 1;

        window.ShowPrompt(new GDictionary { ["dismiss_on_shade"] = false });
        window._UnhandledInput(MakeEscape());
        _test.True(window.Visible, "dismiss_on_shade=false 时 Esc 不应关闭确认提示。");
        _test.Eq(cancelledCount, 0, "被禁止的 Esc 不应发 cancelled 信号。");

        window.HideWindow();
        window.ShowPrompt(new GDictionary());
        window._UnhandledInput(MakeEscape());
        _test.False(window.Visible, "默认 prompt 下 Esc 应关闭窗口。");
        _test.Eq(cancelledCount, 1, "Esc 关闭应发一次 cancelled 信号。");

        window.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private static InputEventKey MakeEscape() =>
        new()
        {
            Keycode = Key.Escape,
            Pressed = true,
            Echo = false,
        };
}
