## 文件说明：该脚本属于世界预设注册表相关的注册表脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name WorldPresetRegistry
extends RefCounted

const DEFAULT_PRESET_ID := &"test"

const _PRESETS := [
	{
		"preset_id": "test",
		"display_name": "测试",
		"size_label": "200 x 200",
		"generation_config_path": "res://data/configs/world_map/test_world_map_config.tres",
	},
	{
		"preset_id": "ashen_intersection",
		"display_name": "灰烬交界",
		"size_label": "100 x 100",
		"generation_config_path": "res://data/configs/world_map/ashen_intersection_world_map_config.tres",
	},
	{
		"preset_id": "small",
		"display_name": "小型",
		"size_label": "1000 x 1000",
		"generation_config_path": "res://data/configs/world_map/small_world_map_config.tres",
	},
	{
		"preset_id": "medium",
		"display_name": "中型",
		"size_label": "1500 x 1500",
		"generation_config_path": "res://data/configs/world_map/medium_world_map_config.tres",
	},
	{
		"preset_id": "giant",
		"display_name": "巨型",
		"size_label": "2000 x 2000",
		"generation_config_path": "res://data/configs/world_map/demo_world_map_config.tres",
	},
]


static func get_default_preset_id() -> StringName:
	return DEFAULT_PRESET_ID


static func list_presets() -> Array[Dictionary]:
	var presets: Array[Dictionary] = []
	for preset_data in _PRESETS:
		presets.append(_normalize_preset(preset_data))
	return presets


static func get_preset(preset_id: StringName) -> Dictionary:
	for preset_data in _PRESETS:
		if String(preset_data.get("preset_id", "")) == String(preset_id):
			return _normalize_preset(preset_data)
	return {}


static func get_preset_for_generation_config(generation_config_path: String) -> Dictionary:
	for preset_data in _PRESETS:
		if String(preset_data.get("generation_config_path", "")) == generation_config_path:
			return _normalize_preset(preset_data)
	return {}


static func get_fallback_preset_name(generation_config_path: String) -> String:
	var preset := get_preset_for_generation_config(generation_config_path)
	if not preset.is_empty():
		return String(preset.get("display_name", "世界"))
	var file_name := generation_config_path.get_file().get_basename()
	return file_name if not file_name.is_empty() else "世界"


static func _normalize_preset(source: Dictionary) -> Dictionary:
	return {
		"preset_id": StringName(String(source.get("preset_id", ""))),
		"display_name": String(source.get("display_name", "")),
		"size_label": String(source.get("size_label", "")),
		"generation_config_path": String(source.get("generation_config_path", "")),
	}
