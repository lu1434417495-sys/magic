using System.Collections.Generic;
using Godot;

public partial class run_display_settings_service_regression : SceneTree
{
    private const string TEMP_SETTINGS_PATH = "user://display_settings_service_regression.cfg";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(exitCode);
    }

    private int Run()
    {
        TestSettingsRoundTrip();
        TestSettingsNormalizeToKnownResolution();
        TestMalformedSettingsFallbackToDefaults();

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

    private void TestMalformedSettingsFallbackToDefaults()
    {
        CleanupFile(TEMP_SETTINGS_PATH);
        var config = new ConfigFile();
        config.SetValue("display", "width", "1920");
        config.SetValue("display", "height", false);
        config.SetValue("display", "fullscreen", "true");
        _test.Eq(
            config.Save(TEMP_SETTINGS_PATH),
            Error.Ok,
            "应能写入显示设置类型错误夹具。"
        );

        var service = new DisplaySettingsService(TEMP_SETTINGS_PATH);
        DisplaySettingsService.DisplaySettings loaded = service.LoadSettings();

        _test.Eq(
            loaded.Resolution,
            DisplaySettingsService.DefaultWindowedResolution,
            "类型错误的宽高配置应回退到默认分辨率而不是崩溃。"
        );
        _test.Eq(loaded.Fullscreen, false, "类型错误的全屏配置应回退到默认值。");
        CleanupFile(TEMP_SETTINGS_PATH);
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
