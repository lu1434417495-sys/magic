using System;
using System.Threading.Tasks;
using Godot;

public partial class run_cold_boot_e2e : E2eSceneTree
{
    private protected override string ScenarioLabel => "E2E cold boot to login";

    private protected override async Task RunScenarioAsync()
    {
        LoginScreen login = await LoadProjectMainSceneAsync<LoginScreen>();
        await Wait.NextFrameAsync();

        AssertCanonicalApplicationOwners(login);
        AssertLoginSurfaceIsReady(login);
    }

    private void AssertCanonicalApplicationOwners(LoginScreen login)
    {
        var coordinator = Root.GetNodeOrNull<ApplicationLifetimeCoordinator>(
            "ApplicationLifetimeCoordinator"
        );
        GameSession gameSession = Root.GetNodeOrNull<GameSession>("GameSession");

        Test.True(coordinator != null, "Cold boot should create the canonical lifetime coordinator.");
        Test.True(gameSession != null, "Cold boot should create the canonical GameSession autoload.");
        Test.True(
            gameSession != null && gameSession.IsContentValidationOk(),
            "Cold boot should bind a valid process content snapshot to GameSession."
        );
        Test.True(ReferenceEquals(CurrentScene, login), "LoginScreen should be SceneTree.CurrentScene.");
        Test.Eq(
            login.SceneFilePath,
            ProjectMainScenePath,
            "The E2E boot path should come from project.godot's main-scene setting."
        );
    }

    private void AssertLoginSurfaceIsReady(LoginScreen login)
    {
        Button startButton = login.GetNodeOrNull<Button>("%StartButton");
        Button loadButton = login.GetNodeOrNull<Button>("%LoadButton");
        Button testButton = login.GetNodeOrNull<Button>("%TestButton");
        Button settingsButton = login.GetNodeOrNull<Button>("%SettingsButton");
        Label statusLabel = login.GetNodeOrNull<Label>("%StatusLabel");

        Test.True(login.IsVisibleInTree(), "LoginScreen should be visible after cold boot.");
        AssertAvailableButton(startButton, "StartButton");
        AssertAvailableButton(loadButton, "LoadButton");
        AssertAvailableButton(testButton, "TestButton");
        AssertAvailableButton(settingsButton, "SettingsButton");
        Test.True(
            statusLabel != null && statusLabel.IsVisibleInTree(),
            "StatusLabel should be visible after cold boot."
        );
        Test.True(
            statusLabel != null && !string.IsNullOrWhiteSpace(statusLabel.Text),
            "StatusLabel should describe the available login actions."
        );
        Test.True(startButton != null && startButton.HasFocus(), "StartButton should own initial UI focus.");
        Test.False(
            IsVisible(login.world_preset_picker_window)
                || IsVisible(login.save_list_window)
                || IsVisible(login.display_settings_window)
                || IsVisible(login.character_creation_window),
            "Cold boot should not leave a login modal open."
        );
        Test.True(
            !string.IsNullOrWhiteSpace(login.start_scene_path)
                && ResourceLoader.Exists(login.start_scene_path, "PackedScene"),
            "LoginScreen should point to an existing gameplay scene."
        );
    }

    private void AssertAvailableButton(Button button, string label)
    {
        Test.True(button != null, $"{label} should exist on the real login scene.");
        Test.True(button != null && button.IsVisibleInTree(), $"{label} should be visible.");
        Test.False(button == null || button.Disabled, $"{label} should be enabled.");
    }

    private static bool IsVisible(CanvasItem item) =>
        item != null && GodotObject.IsInstanceValid(item) && item.IsVisibleInTree();
}
