using System.Collections.Generic;
using Godot;

public partial class run_display_settings_service_regression : SceneTree
{
    private const string TEMP_SETTINGS_PATH = "user://display_settings_service_regression.cfg";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestSettingsRoundTrip();
        TestSettingsNormalizeToKnownResolution();

        return _test.Finish("Display settings service regression");
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
        _test.Eq(saveError, Error.Ok, "显示设置服务应能写入临时配置文件。");

        DisplaySettingsService.DisplaySettings loadedSettings = service.LoadSettings();
        _test.Eq(
            loadedSettings.Resolution,
            new Vector2I(1920, 1080),
            "显示设置 round-trip 后应保留分辨率。"
        );
        _test.Eq(
            loadedSettings.Fullscreen,
            true,
            "显示设置 round-trip 后应保留全屏开关。"
        );
        _test.True(!string.IsNullOrEmpty(service.DescribeSettings(loadedSettings)), "显示设置描述应可生成。");

        CleanupFile(TEMP_SETTINGS_PATH);
    }

    private void TestSettingsNormalizeToKnownResolution()
    {
        var service = new DisplaySettingsService(TEMP_SETTINGS_PATH);
        DisplaySettingsService.DisplaySettings normalized = service.NormalizeSettings(
            new DisplaySettingsService.DisplaySettings(new Vector2I(111, 222), true)
        );

        _test.Eq(
            normalized.Resolution,
            DisplaySettingsService.DefaultWindowedResolution,
            "未知分辨率应归一化到默认窗口分辨率。"
        );
        _test.True(normalized.Fullscreen, "归一化未知分辨率不应丢失全屏开关。");

        IReadOnlyList<DisplaySettingsService.ResolutionOption> options =
            service.ListResolutionOptions();
        _test.True(options.Count > 0, "显示设置服务应继续提供常见分辨率选项。");
        _test.Eq(
            options[0].Size,
            DisplaySettingsService.DefaultWindowedResolution,
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
}
