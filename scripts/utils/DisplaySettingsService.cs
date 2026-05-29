using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;

[GlobalClass]
public partial class DisplaySettingsService : RefCounted
{
    public const string SETTINGS_PATH = "user://display_settings.cfg";
    public static readonly Vector2I DEFAULT_WINDOWED_RESOLUTION = new(1280, 720);
    public static readonly Vector2I[] COMMON_RESOLUTIONS =
    {
        new(1280, 720),
        new(1366, 768),
        new(1600, 900),
        new(1920, 1080),
        new(2560, 1440),
        new(3840, 2160),
    };

    private string _settings_path = SETTINGS_PATH;

    public DisplaySettingsService() { }

    public DisplaySettingsService(string settings_path)
    {
        _settings_path = settings_path;
    }

    public void setup(string settings_path)
    {
        _settings_path = string.IsNullOrEmpty(settings_path) ? SETTINGS_PATH : settings_path;
    }

    public GDictionaryArray list_resolution_options()
    {
        var options = new GDictionaryArray();
        foreach (Vector2I resolution in COMMON_RESOLUTIONS)
        {
            options.Add(
                new GDictionary
                {
                    ["label"] = FormatResolutionLabel(resolution),
                    ["size"] = resolution,
                }
            );
        }
        return options;
    }

    public GDictionary get_default_settings()
    {
        return new GDictionary
        {
            ["resolution"] = DEFAULT_WINDOWED_RESOLUTION,
            ["fullscreen"] = false,
        };
    }

    public GDictionary load_settings()
    {
        var config = new ConfigFile();
        Error loadError = config.Load(_settings_path);
        if (loadError != Error.Ok)
        {
            return get_default_settings();
        }
        int width = (int)config.GetValue("display", "width", DEFAULT_WINDOWED_RESOLUTION.X);
        int height = (int)config.GetValue("display", "height", DEFAULT_WINDOWED_RESOLUTION.Y);
        return normalize_settings(
            new GDictionary
            {
                ["resolution"] = new Vector2I(width, height),
                ["fullscreen"] = (bool)config.GetValue("display", "fullscreen", false),
            }
        );
    }

    public GDictionary load_and_apply(Window window = null)
    {
        return apply_settings(load_settings(), window);
    }

    public Error save_settings(GDictionary settings)
    {
        GDictionary normalized = normalize_settings(settings);
        var config = new ConfigFile();
        Vector2I resolution = normalized["resolution"].AsVector2I();
        config.SetValue("display", "width", resolution.X);
        config.SetValue("display", "height", resolution.Y);
        config.SetValue("display", "fullscreen", normalized["fullscreen"].AsBool());
        return config.Save(_settings_path);
    }

    public GDictionary apply_settings(GDictionary settings, Window window = null)
    {
        GDictionary normalized = normalize_settings(settings);
        Window targetWindow = ResolveWindow(window);
        if (targetWindow == null)
        {
            return normalized;
        }
        Vector2I resolution = normalized["resolution"].AsVector2I();
        ApplyContentResolution(targetWindow, resolution);
        targetWindow.Mode = Window.ModeEnum.Windowed;
        targetWindow.Size = resolution;
        if (normalized["fullscreen"].AsBool())
        {
            targetWindow.Mode = Window.ModeEnum.Fullscreen;
        }
        return normalized;
    }

    public GDictionary normalize_settings(GDictionary settings)
    {
        settings ??= new GDictionary();
        Vector2I resolution = GdInterop.GetVector2I(settings, "resolution", DEFAULT_WINDOWED_RESOLUTION);
        return new GDictionary
        {
            ["resolution"] = NormalizeResolution(resolution),
            ["fullscreen"] = GdInterop.GetBool(settings, "fullscreen"),
        };
    }

    private static Vector2I NormalizeResolution(Vector2I candidate)
    {
        foreach (Vector2I resolution in COMMON_RESOLUTIONS)
        {
            if (resolution == candidate)
            {
                return candidate;
            }
        }
        return DEFAULT_WINDOWED_RESOLUTION;
    }

    public string describe_settings(GDictionary settings)
    {
        GDictionary normalized = normalize_settings(settings);
        return $"分辨率 {FormatResolutionLabel(normalized["resolution"].AsVector2I())} | 全屏 {(normalized["fullscreen"].AsBool() ? "开启" : "关闭")}";
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
