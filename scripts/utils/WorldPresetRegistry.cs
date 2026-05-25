using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class WorldPresetRegistry : RefCounted
{
	public static readonly StringName DEFAULT_PRESET_ID = new("test");

	private static readonly WorldPresetData[] Presets =
	{
		new("test", "测试", "200 x 200", "res://data/configs/world_map/test_world_map_config.tres"),
		new("ashen_intersection", "灰烬交界", "100 x 100", "res://data/configs/world_map/ashen_intersection_world_map_config.tres"),
		new("small", "小型", "1000 x 1000", "res://data/configs/world_map/small_world_map_config.tres"),
		new("medium", "中型", "1500 x 1500", "res://data/configs/world_map/medium_world_map_config.tres"),
		new("giant", "巨型", "2000 x 2000", "res://data/configs/world_map/demo_world_map_config.tres"),
	};

	public static StringName get_default_preset_id()
	{
		return DEFAULT_PRESET_ID;
	}

	public static Godot.Collections.Array<GDictionary> list_presets()
	{
		var presets = new Godot.Collections.Array<GDictionary>();
		foreach (var preset in Presets)
		{
			presets.Add(NormalizePreset(preset));
		}
		return presets;
	}

	public static GDictionary get_preset(StringName preset_id)
	{
		var id = preset_id.ToString();
		foreach (var preset in Presets)
		{
			if (preset.PresetId == id)
			{
				return NormalizePreset(preset);
			}
		}
		return new GDictionary();
	}

	public static GDictionary get_preset_for_generation_config(string generation_config_path)
	{
		foreach (var preset in Presets)
		{
			if (preset.GenerationConfigPath == generation_config_path)
			{
				return NormalizePreset(preset);
			}
		}
		return new GDictionary();
	}

	public static string get_fallback_preset_name(string generation_config_path)
	{
		var preset = get_preset_for_generation_config(generation_config_path);
		if (preset.Count > 0 && preset.ContainsKey("display_name"))
		{
			var displayName = preset["display_name"].ToString();
			if (!string.IsNullOrEmpty(displayName))
			{
				return displayName;
			}
		}
		var fileName = GetBaseName(GetFileName(generation_config_path));
		return string.IsNullOrEmpty(fileName) ? "世界" : fileName;
	}

	private static GDictionary NormalizePreset(WorldPresetData preset)
	{
		return new GDictionary
		{
			["preset_id"] = new StringName(preset.PresetId),
			["display_name"] = preset.DisplayName,
			["size_label"] = preset.SizeLabel,
			["generation_config_path"] = preset.GenerationConfigPath,
		};
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

	private readonly struct WorldPresetData
	{
		public readonly string PresetId;
		public readonly string DisplayName;
		public readonly string SizeLabel;
		public readonly string GenerationConfigPath;

		public WorldPresetData(string presetId, string displayName, string sizeLabel, string generationConfigPath)
		{
			PresetId = presetId;
			DisplayName = displayName;
			SizeLabel = sizeLabel;
			GenerationConfigPath = generationConfigPath;
		}
	}
}
