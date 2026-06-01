using System.Collections.Generic;
using Godot;

public partial class run_display_settings_service_regression : SceneTree
{
    private const string TEMP_SETTINGS_PATH = "user://display_settings_service_regression.cfg";

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestSettingsRoundTrip();
        TestSettingsNormalizeToKnownResolution();

        if (_failures.Count == 0)
        {
            GD.Print("Display settings service regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Display settings service regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestSettingsRoundTrip()
    {
        CleanupFile(TEMP_SETTINGS_PATH);
        var service = new DisplaySettingsService(TEMP_SETTINGS_PATH);
        var expectedSettings = new DisplaySettingsService.DisplaySettings(
            new Vector2I(1920, 1080),
            true
        );

        Error saveError = service.SaveSettings(expectedSettings);
        AssertEq(saveError, Error.Ok, "显示设置服务应能写入临时配置文件。");

        DisplaySettingsService.DisplaySettings loadedSettings = service.LoadSettings();
        AssertEq(
            loadedSettings.Resolution,
            new Vector2I(1920, 1080),
            "显示设置 round-trip 后应保留分辨率。"
        );
        AssertEq(
            loadedSettings.Fullscreen,
            true,
            "显示设置 round-trip 后应保留全屏开关。"
        );
        AssertTrue(
            service.DescribeSettings(loadedSettings).Contains("1920 x 1080"),
            "显示设置描述应继续包含归一化分辨率。"
        );

        CleanupFile(TEMP_SETTINGS_PATH);
    }

    private void TestSettingsNormalizeToKnownResolution()
    {
        var service = new DisplaySettingsService(TEMP_SETTINGS_PATH);
        DisplaySettingsService.DisplaySettings normalized = service.NormalizeSettings(
            new DisplaySettingsService.DisplaySettings(new Vector2I(111, 222), true)
        );

        AssertEq(
            normalized.Resolution,
            DisplaySettingsService.DEFAULT_WINDOWED_RESOLUTION,
            "未知分辨率应归一化到默认窗口分辨率。"
        );
        AssertTrue(normalized.Fullscreen, "归一化未知分辨率不应丢失全屏开关。");

        IReadOnlyList<DisplaySettingsService.ResolutionOption> options =
            service.ListResolutionOptions();
        AssertTrue(options.Count > 0, "显示设置服务应继续提供常见分辨率选项。");
        AssertEq(
            options[0].Size,
            DisplaySettingsService.DEFAULT_WINDOWED_RESOLUTION,
            "首个显示设置选项应继续是默认分辨率。"
        );
    }

    private static void CleanupFile(string virtualPath)
    {
        if (string.IsNullOrEmpty(virtualPath))
            return;
        string absolutePath = ProjectSettings.GlobalizePath(virtualPath);
        if (FileAccess.FileExists(absolutePath))
            DirAccess.RemoveAbsolute(absolutePath);
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
