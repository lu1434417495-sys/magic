using System.Threading.Tasks;
using Godot;

public partial class run_character_creation_window_lifecycle_regression : SceneTree
{
    private static readonly PackedScene CharacterCreationWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/character_creation_window.tscn"
    );

    private readonly TestHarness _test = new();

    public override async void _Initialize()
    {
        await TestBorrowedProgressionRegistrySurvivesWindowLifetime();
        Quit(_test.Finish("Character creation window lifecycle regression"));
    }

    private async Task TestBorrowedProgressionRegistrySurvivesWindowLifetime()
    {
        CharacterCreationWindow window = await CreateWindow();
        ProgressionContentRegistry fallbackRegistry = window.ProgressionContentRegistryForTests;
        var borrowedRegistry = new ProgressionContentRegistry();

        try
        {
            _test.True(
                window.OwnsProgressionContentRegistryForTests,
                "CharacterCreationWindow 默认 progression registry 应为 window-owned fallback。"
            );
            _test.True(
                !fallbackRegistry.IsDisposedForTests,
                "默认 fallback progression registry 在替换前不应被 dispose。"
            );
            _test.True(
                window.HasLiveRngForTests,
                "CharacterCreationWindow 应持有自建 RNG。"
            );

            window.SetProgressionContentRegistry(borrowedRegistry);

            _test.True(
                ReferenceEquals(window.ProgressionContentRegistryForTests, borrowedRegistry),
                "绑定 session registry 后窗口应使用注入的 borrowed registry。"
            );
            _test.False(
                window.OwnsProgressionContentRegistryForTests,
                "注入的 progression registry 应保持 caller-owned。"
            );
            _test.True(
                fallbackRegistry.IsDisposedForTests,
                "替换为 borrowed registry 时应 dispose 旧 window-owned fallback registry。"
            );

            window.QueueFree();
            await ProcessFrames(1);

            _test.False(
                borrowedRegistry.IsDisposedForTests,
                "窗口退出不应释放 borrowed session progression registry。"
            );
            _test.False(
                window.HasLiveRngForTests,
                "窗口退出应释放自建 RNG。"
            );
        }
        finally
        {
            if (window != null && GodotObject.IsInstanceValid(window))
                window.QueueFree();
            if (borrowedRegistry != null && GodotObject.IsInstanceValid(borrowedRegistry))
                borrowedRegistry.Dispose();
        }
    }

    private async Task<CharacterCreationWindow> CreateWindow()
    {
        var window = CharacterCreationWindowScene.Instantiate<CharacterCreationWindow>();
        Root.AddChild(window);
        await ProcessFrames(1);
        return window;
    }

    private async Task ProcessFrames(int count)
    {
        for (int index = 0; index < count; index++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }
}
