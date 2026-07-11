using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class WorldPresetRegistry
{
    private static readonly StringName DefaultPresetId = new("test");

    private static readonly WorldPresetInfo[] Presets =
    {
        new("test", "测试", "200 x 200", "res://data/configs/world_map/test_world_map_config.tres"),
        new(
            "ashen_intersection",
            "灰烬交界",
            "100 x 100",
            "res://data/configs/world_map/ashen_intersection_world_map_config.tres"
        ),
        new(
            "small",
            "小型",
            "1000 x 1000",
            "res://data/configs/world_map/small_world_map_config.tres"
        ),
        new(
            "medium",
            "中型",
            "1500 x 1500",
            "res://data/configs/world_map/medium_world_map_config.tres"
        ),
        new(
            "giant",
            "巨型",
            "2000 x 2000",
            "res://data/configs/world_map/demo_world_map_config.tres"
        ),
    };

    internal sealed class WorldPresetInfo
    {
        public StringName PresetId { get; }
        public string DisplayName { get; }
        public string SizeLabel { get; }
        public string GenerationConfigPath { get; }

        internal WorldPresetInfo(
            string presetId,
            string displayName,
            string sizeLabel,
            string generationConfigPath
        )
        {
            PresetId = new StringName(presetId ?? "");
            DisplayName = displayName ?? "";
            SizeLabel = sizeLabel ?? "";
            GenerationConfigPath = generationConfigPath ?? "";
        }

        internal GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["preset_id"] = PresetId,
                ["display_name"] = DisplayName,
                ["size_label"] = SizeLabel,
                ["generation_config_path"] = GenerationConfigPath,
            };
        }
    }

    internal static StringName GetDefaultPresetId()
    {
        return DefaultPresetId;
    }

    internal static IReadOnlyList<WorldPresetInfo> ListPresetsTyped() => Presets;

    internal static bool TryGetPresetTyped(StringName presetId, out WorldPresetInfo preset)
    {
        foreach (var candidate in Presets)
        {
            if (candidate.PresetId == presetId)
            {
                preset = candidate;
                return true;
            }
        }
        preset = null;
        return false;
    }

    internal static GDictionary GetPreset(StringName preset_id)
    {
        return TryGetPresetTyped(preset_id, out WorldPresetInfo preset)
            ? preset.ToDictionary()
            : new GDictionary();
    }

    internal static bool TryGetPresetForGenerationConfigTyped(
        string generationConfigPath,
        out WorldPresetInfo preset
    )
    {
        foreach (var candidate in Presets)
        {
            if (candidate.GenerationConfigPath == generationConfigPath)
            {
                preset = candidate;
                return true;
            }
        }
        preset = null;
        return false;
    }

    internal static GDictionary GetPresetForGenerationConfig(string generation_config_path)
    {
        return TryGetPresetForGenerationConfigTyped(generation_config_path, out WorldPresetInfo preset)
            ? preset.ToDictionary()
            : new GDictionary();
    }

    internal static string GetFallbackPresetName(string generation_config_path)
    {
        if (
            TryGetPresetForGenerationConfigTyped(
                generation_config_path,
                out WorldPresetInfo preset
            )
            && !string.IsNullOrEmpty(preset.DisplayName)
        )
            return preset.DisplayName;
        var fileName = GetBaseName(GetFileName(generation_config_path));
        return string.IsNullOrEmpty(fileName) ? "世界" : fileName;
    }

    private static string GetFileName(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }
        var normalized = path.Replace('\\', '/');
        var separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex >= 0 ? normalized[(separatorIndex + 1)..] : normalized;
    }

    private static string GetBaseName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }
        var dotIndex = fileName.LastIndexOf('.');
        return dotIndex > 0 ? fileName[..dotIndex] : fileName;
    }

}
