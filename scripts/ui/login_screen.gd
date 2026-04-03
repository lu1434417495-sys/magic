extends Control

@export_file("*.tscn") var start_scene_path: String
@export_file("*.tres") var generation_config_path: String

@onready var start_button: Button = %StartButton
@onready var status_label: Label = %StatusLabel

var _is_transitioning := false


func _ready() -> void:
	start_button.pressed.connect(_on_start_button_pressed)
	start_button.grab_focus()
	status_label.text = "点击“开始游戏”进入主场景。"


func _unhandled_input(event: InputEvent) -> void:
	if _is_transitioning:
		return

	if event is InputEventKey and event.pressed and not event.echo:
		var key_event := event as InputEventKey
		if key_event.keycode == KEY_ENTER or key_event.keycode == KEY_KP_ENTER or key_event.keycode == KEY_SPACE:
			var viewport := get_viewport()
			if viewport != null:
				viewport.set_input_as_handled()
			_start_game()


func _on_start_button_pressed() -> void:
	_start_game()


func _start_game() -> void:
	if _is_transitioning:
		return

	if start_scene_path.is_empty():
		_show_error("未配置开始场景。")
		return
	if generation_config_path.is_empty():
		_show_error("未配置大地图生成配置。")
		return

	_is_transitioning = true
	start_button.disabled = true
	status_label.text = "正在加载世界并进入游戏..."

	var session_error := GameSession.ensure_world_ready(generation_config_path)
	if session_error != OK:
		_is_transitioning = false
		start_button.disabled = false
		_show_error("世界加载失败，请检查持久化数据或配置。")
		push_error("Failed to prepare persistent world state. Error code: %s" % session_error)
		return

	var change_error := get_tree().change_scene_to_file(start_scene_path)
	if change_error != OK:
		_is_transitioning = false
		start_button.disabled = false
		_show_error("进入游戏失败，请检查场景路径。")
		push_error("Failed to change scene to %s. Error code: %s" % [start_scene_path, change_error])


func _show_error(message: String) -> void:
	status_label.text = message
