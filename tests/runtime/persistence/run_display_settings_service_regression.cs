using System.Collections.Generic;
using Godot;

public partial class run_display_settings_service_regression : LifecycleTestSceneTree
{
    private const string TEMP_SETTINGS_PATH = "user://display_settings_service_regression.cfg";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestResult exitCode = Run();
        RequestTestExit(exitCode);
    }

    private TestResult Run()
    {
        TestSettingsRoundTrip();
        TestSettingsNormalizeToKnownResolution();
        TestMalformedSettingsFallbackToDefaults();
        TestFullscreenFollowsSystemResolution();

        return _test.Finish("Display settings service regression");
    }

    private void TestSettingsRoundTrip()
    {
        CleanupFile(TEMP_SETTINGS_PATH);
        var service = new DisplaySettingsService(TEMP_SETTINGS_PATH);
        _test.True(service.LoadSettings().Fullscreen, "没有显示配置时应默认全屏启动。");
        var expectedSettings = new DisplaySettingsService.DisplaySettings(
            new Vector2I(1920, 1080),
            false
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
            false,
            "显示设置 round-trip 后应保留全屏开关。"
        );
        _test.True(!string.IsNullOrEmpty(service.DescribeSettings(loadedSettings)), "显示设置描述应可生成。");

        CleanupFile(TEMP_SETTINGS_PATH);
    }

    private void TestSettingsNormalizeToKnownResolution()
    {
        var service = new DisplaySettingsService(TEMP_SETTINGS_PATH);
        DisplaySettingsService.DisplaySettings normalized = service.NormalizeSettings(
            new DisplaySettingsService.DisplaySettings(new Vector2I(111, 222), false)
        );

        _test.Eq(
            normalized.Resolution,
            DisplaySettingsService.DefaultWindowedResolution,
            "未知分辨率应归一化到默认窗口分辨率。"
        );
        _test.False(normalized.Fullscreen, "归一化未知分辨率不应改变窗口模式。");

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
        using var config = new ConfigFile();
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
            service.GetSystemResolution(),
            "类型错误的配置应使用系统分辨率全屏。"
        );
        _test.Eq(loaded.Fullscreen, true, "类型错误的全屏配置应回退到全屏默认值。");
        CleanupFile(TEMP_SETTINGS_PATH);
    }

    private void TestFullscreenFollowsSystemResolution()
    {
        CleanupFile(TEMP_SETTINGS_PATH);
        Vector2I screenSize = new(3440, 1440);
        var service = new DisplaySettingsService(TEMP_SETTINGS_PATH, () => screenSize);
        _test.Eq(service.LoadSettings(), new DisplaySettingsService.DisplaySettings(screenSize, true), "首次启动应选择系统分辨率全屏，支持常见列表外的显示器。");
        _test.True(System.Linq.Enumerable.Any(service.ListResolutionOptions(), option => option.Size == screenSize), "显示设置应能呈现当前系统分辨率。");
        _test.Eq(service.SaveSettings(new DisplaySettingsService.DisplaySettings(new Vector2I(1280, 720), true)), Error.Ok, "全屏配置应可保存。");
        screenSize = new Vector2I(2560, 1440);
        _test.Eq(service.LoadSettings().Resolution, screenSize, "全屏读档应重新查询系统分辨率，不锁定上一次的分辨率。");
        screenSize = new Vector2I(3840, 2160);
        _test.Eq(service.NormalizeSettings(new DisplaySettingsService.DisplaySettings(new Vector2I(1920, 1080), true)).Resolution, screenSize, "全屏应用应跟随当前系统分辨率。");
        screenSize = Vector2I.Zero;
        _test.Eq(service.GetDefaultSettings().Resolution, DisplaySettingsService.DefaultWindowedResolution, "无物理屏幕的 headless runner 仍应获得合法尺寸。");
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
