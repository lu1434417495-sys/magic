## 文件说明：该脚本属于显示设置服务相关的服务脚本，集中维护设置路径等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name DisplaySettingsService
extends RefCounted

const SETTINGS_PATH := "user://display_settings.cfg"
const DEFAULT_WINDOWED_RESOLUTION := Vector2i(1280, 720)
const COMMON_RESOLUTIONS := [
	Vector2i(1280, 720),
	Vector2i(1366, 768),
	Vector2i(1600, 900),
	Vector2i(1920, 1080),
	Vector2i(2560, 1440),
	Vector2i(3840, 2160),
]

## 字段说明：记录设置路径，供运行时加载场景、资源或存档文件时直接使用。
var _settings_path := SETTINGS_PATH


func _init(settings_path: String = SETTINGS_PATH) -> void:
	_settings_path = settings_path


func list_resolution_options() -> Array[Dictionary]:
	var options: Array[Dictionary] = []
	for resolution in COMMON_RESOLUTIONS:
		options.append({
			"label": _format_resolution_label(resolution),
			"size": resolution,
		})
	return options


func get_default_settings() -> Dictionary:
	return {
		"resolution": DEFAULT_WINDOWED_RESOLUTION,
		"fullscreen": false,
	}


func load_settings() -> Dictionary:
	var config := ConfigFile.new()
	var load_error := config.load(_settings_path)
	if load_error != OK:
		return get_default_settings()

	var width := int(config.get_value("display", "width", DEFAULT_WINDOWED_RESOLUTION.x))
	var height := int(config.get_value("display", "height", DEFAULT_WINDOWED_RESOLUTION.y))
	return normalize_settings({
		"resolution": Vector2i(width, height),
		"fullscreen": bool(config.get_value("display", "fullscreen", false)),
	})


func load_and_apply(window: Window = null) -> Dictionary:
	var settings := load_settings()
	return apply_settings(settings, window)


func save_settings(settings: Dictionary) -> int:
	var normalized := normalize_settings(settings)
	var config := ConfigFile.new()
	config.set_value("display", "width", normalized.resolution.x)
	config.set_value("display", "height", normalized.resolution.y)
	config.set_value("display", "fullscreen", normalized.fullscreen)
	return config.save(_settings_path)


func apply_settings(settings: Dictionary, window: Window = null) -> Dictionary:
	var normalized := normalize_settings(settings)
	var target_window := _resolve_window(window)
	if target_window == null:
		return normalized
	
	_apply_content_resolution(target_window, normalized.resolution)
	target_window.mode = Window.MODE_WINDOWED
	target_window.size = normalized.resolution
	if normalized.fullscreen:
		target_window.mode = Window.MODE_FULLSCREEN
	return normalized


func normalize_settings(settings: Dictionary) -> Dictionary:
	return {
		"resolution": normalize_resolution(settings.get("resolution", DEFAULT_WINDOWED_RESOLUTION)),
		"fullscreen": bool(settings.get("fullscreen", false)),
	}


func normalize_resolution(value) -> Vector2i:
	var candidate := DEFAULT_WINDOWED_RESOLUTION
	if value is Vector2i:
		candidate = value
	elif value is Vector2:
		candidate = Vector2i(int(round(value.x)), int(round(value.y)))
	elif value is Dictionary:
		candidate = Vector2i(
			int(value.get("x", DEFAULT_WINDOWED_RESOLUTION.x)),
			int(value.get("y", DEFAULT_WINDOWED_RESOLUTION.y))
		)

	if COMMON_RESOLUTIONS.has(candidate):
		return candidate
	return DEFAULT_WINDOWED_RESOLUTION


func describe_settings(settings: Dictionary) -> String:
	var normalized := normalize_settings(settings)
	return "分辨率 %s | 全屏 %s" % [
		_format_resolution_label(normalized.resolution),
		"开启" if normalized.fullscreen else "关闭",
	]


func _resolve_window(window: Window = null) -> Window:
	if window != null:
		return window
	var tree := Engine.get_main_loop() as SceneTree
	if tree == null:
		return null
	return tree.root


func _apply_content_resolution(target_window: Window, resolution: Vector2i) -> void:
	if resolution.x <= 0 or resolution.y <= 0:
		return
	target_window.content_scale_size = resolution


func _format_resolution_label(resolution: Vector2i) -> String:
	return "%d x %d" % [resolution.x, resolution.y]
