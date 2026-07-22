using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Godot;

public abstract partial class E2eSceneTree : LifecycleTestSceneTree
{
    private const string MainSceneSetting = "application/run/main_scene";
    private const string IsolatedUserDataEnvironment = "MAGIC_E2E_ISOLATED_USER_DATA";
    private const string IsolatedUserDataRootEnvironment = "MAGIC_E2E_USER_DATA_ROOT";
    private const string DeterministicRandomSeedEnvironment = "MAGIC_E2E_RANDOM_SEED";
    private const int DefaultSceneTimeoutFrames = 600;
    private const int DefaultUiTimeoutFrames = 600;
    private const int WorldReadyTimeoutFrames = 1800;

    private readonly TestHarness _test = new();
    private E2eWait _wait;
    private E2eInputDriver _input;

    private protected TestHarness Test => _test;
    private protected E2eWait Wait => _wait;
    private protected E2eInputDriver Input => _input;
    private protected virtual string ScenarioLabel => GetType().Name;

    public sealed override void _Initialize()
    {
        _wait = new E2eWait(this);
        _input = new E2eInputDriver(this, _wait);
        RunAfterProcessStartup(RunScenario);
    }

    private async void RunScenario()
    {
        try
        {
            ConfigureDeterministicRandomIfRequested();
            await RunScenarioAsync();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unhandled E2E exception ({exception.GetType().Name}): {exception.Message}"
            );
        }
        finally
        {
            try
            {
                await _input.ReleaseAllAsync();
            }
            catch (Exception exception)
            {
                _test.Fail(
                    $"Synthetic input cleanup failed ({exception.GetType().Name}): {exception.Message}"
                );
            }

            RequestTestExit(_test.Finish(ScenarioLabel));
        }
    }

    private protected abstract Task RunScenarioAsync();

    private protected string ProjectMainScenePath
    {
        get
        {
            string path = ProjectSettings.GetSetting(MainSceneSetting, "").AsString().Trim();
            if (path.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Project setting {MainSceneSetting} does not name a main scene."
                );
            }
            return path;
        }
    }

    private protected async Task<TScene> LoadProjectMainSceneAsync<TScene>(
        int maxFrames = DefaultSceneTimeoutFrames
    )
        where TScene : Node
    {
        string mainScenePath = ProjectMainScenePath;
        Node currentScene = CurrentScene;
        if (
            currentScene == null
            || !GodotObject.IsInstanceValid(currentScene)
            || !string.Equals(currentScene.SceneFilePath, mainScenePath, StringComparison.Ordinal)
        )
        {
            Error error = ChangeSceneToFile(mainScenePath);
            if (error != Error.Ok)
            {
                throw new InvalidOperationException(
                    $"Failed to load project main scene {mainScenePath}. Error={error}."
                );
            }
        }

        return await WaitForCurrentSceneAsync<TScene>(
            $"project main scene {mainScenePath} to become ready",
            maxFrames
        );
    }

    private protected Task<TScene> WaitForCurrentSceneAsync<TScene>(
        string description,
        int maxFrames = DefaultSceneTimeoutFrames
    )
        where TScene : Node
    {
        return _wait.UntilValueAsync(
            () => CurrentScene as TScene,
            scene =>
                scene != null
                && GodotObject.IsInstanceValid(scene)
                && scene.IsInsideTree()
                && scene.IsNodeReady(),
            maxFrames,
            description
        );
    }

    private protected async Task<WorldMapSystem> CreateTestGameThroughUiAsync(
        string displayName
    )
    {
        RequireIsolatedUserData();
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Character display name is required.", nameof(displayName));
        if (displayName.Length > 24)
            throw new ArgumentOutOfRangeException(nameof(displayName), "Character display name exceeds the UI limit.");

        LoginScreen login = await LoadProjectMainSceneAsync<LoginScreen>();
        await WaitForClickableAsync(login.test_button, "login TestButton");
        await _input.ClickAsync(login.test_button);

        CharacterCreationWindow creationWindow = login.character_creation_window;
        await _wait.UntilAsync(
            () =>
                IsLiveVisible(creationWindow)
                && IsLiveVisible(creationWindow.name_phase)
                && creationWindow.name_input != null
                && creationWindow.name_input.HasFocus(),
            DefaultUiTimeoutFrames,
            "character-creation name phase"
        );

        await _input.TypeTextAsync(displayName);
        await _input.TapKeyAsync(Key.Enter);
        await _wait.UntilAsync(
            () =>
                IsLiveVisible(creationWindow.attribute_phase)
                && IsClickable(creationWindow.confirm_button),
            DefaultUiTimeoutFrames,
            "character-creation attribute phase"
        );
        await _input.ClickAsync(creationWindow.confirm_button);

        await WaitForFocusedButtonAsync(
            creationWindow.race_next_button,
            "character-creation race next button"
        );
        await _input.TapKeyAsync(Key.Enter);

        await WaitForFocusedButtonAsync(
            creationWindow.age_next_button,
            "character-creation age next button"
        );
        await _input.TapKeyAsync(Key.Enter);

        await WaitForFocusedButtonAsync(
            creationWindow.final_confirm_button,
            "character-creation final confirm button"
        );
        await _input.TapKeyAsync(Key.Enter);

        WorldMapSystem worldMap = await WaitForCurrentSceneAsync<WorldMapSystem>(
            "new test game to enter the world-map scene",
            WorldReadyTimeoutFrames
        );
        await _wait.UntilAsync(
            () =>
                GodotObject.IsInstanceValid(worldMap)
                && worldMap._runtime != null
                && worldMap._game_session != null
                && worldMap.world_map_view != null
                && worldMap.battle_map_panel != null,
            WorldReadyTimeoutFrames,
            "world-map runtime and primary UI bindings"
        );
        return worldMap;
    }

    private protected void RequireIsolatedUserData()
    {
        string expectedRoot = OS.GetEnvironment(IsolatedUserDataRootEnvironment).Trim();
        if (
            !string.Equals(
                OS.GetEnvironment(IsolatedUserDataEnvironment),
                "1",
                StringComparison.Ordinal
            )
            || !Path.IsPathFullyQualified(expectedRoot)
        )
        {
            throw new InvalidOperationException(
                $"Save E2E flows require {IsolatedUserDataEnvironment}=1 and an absolute {IsolatedUserDataRootEnvironment}."
            );
        }

        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedRoot));
        string actualUserData = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(OS.GetUserDataDir())
        );
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;
        bool isInsideSandbox =
            string.Equals(actualUserData, normalizedRoot, comparison)
            || actualUserData.StartsWith(rootPrefix, comparison);
        if (!isInsideSandbox)
        {
            throw new InvalidOperationException(
                $"Godot user data is outside the E2E sandbox. actual={actualUserData} expected_root={normalizedRoot}"
            );
        }
    }

    private void ConfigureDeterministicRandomIfRequested()
    {
        string rawSeed = OS.GetEnvironment(DeterministicRandomSeedEnvironment).Trim();
        if (rawSeed.Length == 0)
            return;

        RequireIsolatedUserData();
        if (
            !long.TryParse(
                rawSeed,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long seed
            )
            || seed <= 0
        )
        {
            throw new InvalidOperationException(
                $"{DeterministicRandomSeedEnvironment} must be a positive base-10 integer."
            );
        }

        TrueRandomSeedService.ConfigureDeterministicForTests(seed);
    }

    private async Task WaitForClickableAsync(Control control, string description)
    {
        await _wait.UntilAsync(
            () => IsClickable(control),
            DefaultUiTimeoutFrames,
            description
        );
    }

    private async Task WaitForFocusedButtonAsync(Button button, string description)
    {
        await _wait.UntilAsync(
            () => IsClickable(button) && button.HasFocus(),
            DefaultUiTimeoutFrames,
            description
        );
    }

    private static bool IsClickable(Control control)
    {
        if (!IsLiveVisible(control))
            return false;
        if (control is BaseButton { Disabled: true })
            return false;
        Rect2 rect = control.GetGlobalRect();
        return rect.Size.X > 0.0f && rect.Size.Y > 0.0f;
    }

    private static bool IsLiveVisible(CanvasItem item)
    {
        return item != null
            && GodotObject.IsInstanceValid(item)
            && item.IsInsideTree()
            && item.IsVisibleInTree();
    }
}
