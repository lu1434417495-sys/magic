using Godot;
using System.Collections.Generic;

public sealed class DisplaySettingsService
{
    internal const string SettingsPath = "user://display_settings.cfg";
    internal static readonly Vector2I DefaultWindowedResolution = new(1280, 720);
    internal static readonly IReadOnlyList<Vector2I> CommonResolutions = new Vector2I[]
    {
        new(1280, 720),
        new(1366, 768),
        new(1600, 900),
        new(1920, 1080),
        new(2560, 1440),
        new(3840, 2160),
    };

    public readonly record struct DisplaySettings(Vector2I Resolution, bool Fullscreen);
    public readonly record struct ResolutionOption(string Label, Vector2I Size);

    private string _settingsPath = SettingsPath;

    public DisplaySettingsService() { }

    public DisplaySettingsService(string settingsPath)
    {
        Setup(settingsPath);
    }

    public void Setup(string settingsPath)
    {
        _settingsPath = string.IsNullOrEmpty(settingsPath) ? SettingsPath : settingsPath;
    }

    public IReadOnlyList<ResolutionOption> ListResolutionOptions()
    {
        var options = new List<ResolutionOption>(CommonResolutions.Count);
        foreach (Vector2I resolution in CommonResolutions)
        {
            options.Add(new ResolutionOption(FormatResolutionLabel(resolution), resolution));
        }
        return options;
    }

    public DisplaySettings GetDefaultSettings()
    {
        return new DisplaySettings(DefaultWindowedResolution, false);
    }

    public DisplaySettings LoadSettings()
    {
        var config = new ConfigFile();
        Error loadError = config.Load(_settingsPath);
        if (loadError != Error.Ok)
        {
            return GetDefaultSettings();
        }
        int width = (int)config.GetValue("display", "width", DefaultWindowedResolution.X);
        int height = (int)config.GetValue("display", "height", DefaultWindowedResolution.Y);
        return NormalizeSettings(
            new DisplaySettings(
                new Vector2I(width, height),
                (bool)config.GetValue("display", "fullscreen", false)
            )
        );
    }

    public DisplaySettings LoadAndApply(Window window = null)
    {
        return ApplySettings(LoadSettings(), window);
    }

    public Error SaveSettings(DisplaySettings settings)
    {
        DisplaySettings normalized = NormalizeSettings(settings);
        var config = new ConfigFile();
        Vector2I resolution = normalized.Resolution;
        config.SetValue("display", "width", resolution.X);
        config.SetValue("display", "height", resolution.Y);
        config.SetValue("display", "fullscreen", normalized.Fullscreen);
        return config.Save(_settingsPath);
    }

    public DisplaySettings ApplySettings(DisplaySettings settings, Window window = null)
    {
        DisplaySettings normalized = NormalizeSettings(settings);
        Window targetWindow = ResolveWindow(window);
        if (targetWindow == null)
        {
            return normalized;
        }
        Vector2I resolution = normalized.Resolution;
        ApplyContentResolution(targetWindow, resolution);
        targetWindow.Mode = Window.ModeEnum.Windowed;
        targetWindow.Size = resolution;
        if (normalized.Fullscreen)
        {
            targetWindow.Mode = Window.ModeEnum.Fullscreen;
        }
        return normalized;
    }

    public DisplaySettings NormalizeSettings(DisplaySettings settings)
    {
        return new DisplaySettings(NormalizeResolution(settings.Resolution), settings.Fullscreen);
    }

    private static Vector2I NormalizeResolution(Vector2I candidate)
    {
        foreach (Vector2I resolution in CommonResolutions)
        {
            if (resolution == candidate)
            {
                return candidate;
            }
        }
        return DefaultWindowedResolution;
    }

    public string DescribeSettings(DisplaySettings settings)
    {
        DisplaySettings normalized = NormalizeSettings(settings);
        return $"分辨率 {FormatResolutionLabel(normalized.Resolution)} | 全屏 {(normalized.Fullscreen ? "开启" : "关闭")}";
    }

    private static Window ResolveWindow(Window window)
    {
        if (window != null)
        {
            return window;
        }
        return Engine.GetMainLoop() is SceneTree tree ? tree.Root : null;
    }

    private static void ApplyContentResolution(Window targetWindow, Vector2I resolution)
    {
        if (resolution.X <= 0 || resolution.Y <= 0)
        {
            return;
        }
        targetWindow.ContentScaleSize = resolution;
    }

    private static string FormatResolutionLabel(Vector2I resolution)
    {
        return $"{resolution.X} x {resolution.Y}";
    }

}
