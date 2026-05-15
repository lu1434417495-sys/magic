## 文件说明：该脚本属于登录界面相关的界面脚本，集中维护开始场景路径、开始按钮、测试按钮等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

extends Control

const WORLD_PRESET_REGISTRY_SCRIPT = preload("res://scripts/utils/world_preset_registry.gd")
const WORLD_PRESET_PICKER_WINDOW_SCRIPT = preload("res://scripts/ui/world_preset_picker_window.gd")
const SAVE_LIST_WINDOW_SCRIPT = preload("res://scripts/ui/save_list_window.gd")
const DISPLAY_SETTINGS_WINDOW_SCRIPT = preload("res://scripts/ui/display_settings_window.gd")
const DISPLAY_SETTINGS_SERVICE_SCRIPT = preload("res://scripts/utils/display_settings_service.gd")
const CHARACTER_CREATION_WINDOW_SCRIPT = preload("res://scripts/ui/character_creation_window.gd")

const PENDING_START_TYPE_PRESET: StringName = &"preset"

const TEST_PRESET_ID := &"test"
const DEFAULT_START_PRESET_ID := &"small"

## 字段说明：在编辑器中配置开始场景路径，运行时会据此加载场景、资源、配置文件或存档模板。
@export_file("*.tscn") var start_scene_path: String

## 字段说明：缓存开始按钮节点，供用户进入正式游戏流程。
@onready var start_button: Button = %StartButton
## 字段说明：缓存测试按钮节点，供快速进入测试预设。
@onready var test_button: Button = %TestButton
## 字段说明：缓存加载按钮节点，供用户进入存档加载流程。
@onready var load_button: Button = %LoadButton
## 字段说明：缓存设置按钮节点，供打开显示设置窗口。
@onready var settings_button: Button = %SettingsButton
## 字段说明：缓存状态提示标签节点，用于向玩家展示当前操作结果、错误原因或下一步引导。
@onready var status_label: Label = %StatusLabel
## 字段说明：缓存世界预设选择窗口节点，负责承载新开局预设选择流程。
@onready var world_preset_picker_window: WORLD_PRESET_PICKER_WINDOW_SCRIPT = $WorldPresetPickerWindow
## 字段说明：缓存存档列表窗口节点，负责展示可读档的存档条目。
@onready var save_list_window: SAVE_LIST_WINDOW_SCRIPT = $SaveListWindow
## 字段说明：缓存显示设置窗口节点，负责承载分辨率和窗口模式的交互流程。
@onready var display_settings_window: DISPLAY_SETTINGS_WINDOW_SCRIPT = $DisplaySettingsWindow
## 字段说明：缓存建卡窗口节点，负责承载主角姓名输入、属性掷骰与 reroll 流程。
@onready var character_creation_window: CHARACTER_CREATION_WINDOW_SCRIPT = $CharacterCreationWindow

## 字段说明：用于标记当前是否处于切换流程状态，避免在不合适的时机重复触发流程，作为界面刷新、输入处理和窗口联动的重要依据。
var _is_transitioning := false
## 字段说明：缓存显示设置服务实例，负责读取、归一化和应用显示配置。
var _display_settings_service: DisplaySettingsService = null
## 字段说明：缓存当前生效的显示设置字典，供窗口回填和重新应用时复用。
var _display_settings: Dictionary = {}
## 字段说明：缓存当前等待建卡完成的入口类型，用于分发后续开档调用。
var _pending_start_type: StringName = &""
## 字段说明：缓存选择地图流程中等待建卡的预设 id，供建卡确认后传入 create_new_save；测试地图也使用同一条预设生成链。
var _pending_preset_id: StringName = &""


func _ready() -> void:
	_display_settings_service = DISPLAY_SETTINGS_SERVICE_SCRIPT.new()
	_display_settings = _display_settings_service.load_and_apply(get_window())

	start_button.pressed.connect(_on_start_button_pressed)
	test_button.pressed.connect(_on_test_button_pressed)
	load_button.pressed.connect(_on_load_button_pressed)
	settings_button.pressed.connect(_on_settings_button_pressed)
	world_preset_picker_window.preset_confirmed.connect(_on_world_preset_confirmed)
	world_preset_picker_window.cancelled.connect(_on_world_preset_picker_cancelled)
	save_list_window.save_load_requested.connect(_on_save_load_requested)
	save_list_window.closed.connect(_on_save_list_closed)
	display_settings_window.settings_apply_requested.connect(_on_display_settings_apply_requested)
	display_settings_window.cancelled.connect(_on_display_settings_cancelled)
	display_settings_window.configure_options(_display_settings_service.list_resolution_options())
	character_creation_window.character_confirmed.connect(_on_character_creation_confirmed)
	character_creation_window.cancelled.connect(_on_character_creation_cancelled)
	_configure_character_creation_window()
	start_button.grab_focus()
	_show_idle_status()


func _unhandled_input(event: InputEvent) -> void:
	if _is_transitioning or _is_modal_open():
		return
	if event is not InputEventKey:
		return

	var key_event := event as InputEventKey
	if not key_event.pressed or key_event.echo:
		return
	if key_event.keycode != KEY_ENTER and key_event.keycode != KEY_KP_ENTER and key_event.keycode != KEY_SPACE:
		return

	var viewport := get_viewport()
	if viewport != null:
		viewport.set_input_as_handled()
	_open_start_game_picker()


func _on_start_button_pressed() -> void:
	_open_start_game_picker()


func _on_test_button_pressed() -> void:
	if _is_transitioning:
		return
	if not _validate_start_scene_path():
		return
	_open_character_creation_for(PENDING_START_TYPE_PRESET, TEST_PRESET_ID)


func _on_load_button_pressed() -> void:
	if _is_transitioning:
		return
	if not _validate_start_scene_path():
		return
	var game_session = _get_game_session()
	if game_session == null:
		_show_error("未找到 GameSession。")
		return

	var save_slots: Array[Dictionary] = game_session.list_save_slots()
	save_list_window.show_window(save_slots)
	if save_slots.is_empty():
		status_label.text = "当前没有可加载的存档。可以先点击“进入游戏”或“测试地图”创建新存档。"
	else:
		status_label.text = "请选择一个已有存档继续加载游戏。"


func _on_settings_button_pressed() -> void:
	if _is_transitioning:
		return
	display_settings_window.show_window(_display_settings)
	status_label.text = "请选择游戏分辨率，并按需切换全屏模式。"


func _open_start_game_picker() -> void:
	if _is_transitioning:
		return
	if not _validate_start_scene_path():
		return

	var presets: Array[Dictionary] = []
	for preset_data in WORLD_PRESET_REGISTRY_SCRIPT.list_presets():
		if StringName(String(preset_data.get("preset_id", ""))) == TEST_PRESET_ID:
			continue
		presets.append(preset_data)

	if presets.is_empty():
		_show_error("当前没有可用的正式世界预设。")
		return

	world_preset_picker_window.show_window(presets, DEFAULT_START_PRESET_ID)
	status_label.text = "请选择正式世界类型。"


func _on_world_preset_confirmed(preset_id: StringName) -> void:
	_open_character_creation_for(PENDING_START_TYPE_PRESET, preset_id)


func _on_world_preset_picker_cancelled() -> void:
	_show_idle_status()


func _open_character_creation_for(start_type: StringName, preset_id: StringName) -> void:
	if not _can_open_character_creation():
		_pending_start_type = &""
		_pending_preset_id = &""
		return
	_pending_start_type = start_type
	_pending_preset_id = preset_id
	_configure_character_creation_window()
	character_creation_window.show_window()
	status_label.text = "请输入主角姓名并掷出六项属性。"


func _can_open_character_creation() -> bool:
	var game_session = _get_game_session()
	if game_session == null:
		_show_error("未找到 GameSession。")
		return false
	if game_session.has_method("refresh_content_validation_snapshot"):
		var snapshot: Dictionary = game_session.refresh_content_validation_snapshot()
		if not bool(snapshot.get("ok", false)):
			_show_error("内容校验失败，无法开始建卡。请查看日志并修正配置。")
			return false
	if game_session.has_method("is_content_validation_ok") and not game_session.is_content_validation_ok():
		_show_error("内容校验失败，无法开始建卡。请查看日志并修正配置。")
		return false
	return true


func _on_character_creation_confirmed(payload: Dictionary) -> void:
	var start_type := _pending_start_type
	var preset_id := _pending_preset_id
	_pending_start_type = &""
	_pending_preset_id = &""

	match start_type:
		PENDING_START_TYPE_PRESET:
			_start_preset(preset_id, payload)
		_:
			_show_idle_status()


func _on_character_creation_cancelled() -> void:
	_pending_start_type = &""
	_pending_preset_id = &""
	_show_idle_status()


func _on_save_load_requested(save_id: String) -> void:
	if _is_transitioning:
		return

	_set_transition_state(true)
	status_label.text = "正在加载存档并进入游戏..."

	var game_session = _get_game_session()
	if game_session == null:
		_set_transition_state(false)
		_show_error("未找到 GameSession。")
		return
	var load_error: int = int(game_session.load_save(save_id))
	if load_error != OK:
		_set_transition_state(false)
		if load_error == ERR_INVALID_DATA:
			_show_error("该存档数据不完整或版本不匹配，当前版本无法加载。")
		else:
			_show_error("加载存档失败，请检查存档数据。")
		push_error("Failed to load save slot %s. Error code: %s" % [save_id, load_error])
		return

	_change_to_start_scene()


func _on_save_list_closed() -> void:
	_show_idle_status()


func _on_display_settings_apply_requested(settings: Dictionary) -> void:
	_display_settings = _display_settings_service.apply_settings(settings, get_window())
	var save_error: int = _display_settings_service.save_settings(_display_settings)
	if save_error == OK:
		status_label.text = "显示设置已应用：%s。" % _display_settings_service.describe_settings(_display_settings)
	else:
		status_label.text = "显示设置已应用，但本地保存失败。"
	start_button.grab_focus()


func _on_display_settings_cancelled() -> void:
	start_button.grab_focus()
	_show_idle_status()


func _start_preset(preset_id: StringName, character_creation_payload: Dictionary = {}) -> void:
	if _is_transitioning:
		return
	if not _validate_start_scene_path():
		return

	var preset_data := WORLD_PRESET_REGISTRY_SCRIPT.get_preset(preset_id)
	if preset_data.is_empty():
		_show_error("未找到对应的世界预设。")
		return

	var generation_config_path := String(preset_data.get("generation_config_path", ""))
	if generation_config_path.is_empty():
		_show_error("世界预设缺少生成配置路径。")
		return

	_set_transition_state(true)
	var preset_name := String(preset_data.get("display_name", "世界"))
	status_label.text = "正在创建 %s 存档并进入游戏..." % preset_name

	var session_error: int = _create_save_for_preset(preset_id, character_creation_payload)
	if session_error != OK:
		_set_transition_state(false)
		_show_error("世界创建失败，请检查持久化目录或配置。")
		push_error("Failed to create save for preset %s. Error code: %s" % [preset_id, session_error])
		return

	_change_to_start_scene()


func _create_save_for_preset(preset_id: StringName, character_creation_payload: Dictionary = {}) -> int:
	var preset_data := WORLD_PRESET_REGISTRY_SCRIPT.get_preset(preset_id)
	if preset_data.is_empty():
		return ERR_DOES_NOT_EXIST

	var generation_config_path := String(preset_data.get("generation_config_path", ""))
	if generation_config_path.is_empty():
		return ERR_INVALID_DATA

	var game_session = _get_game_session()
	if game_session == null:
		return ERR_UNCONFIGURED

	return int(game_session.create_new_save(
		generation_config_path,
		preset_id,
		String(preset_data.get("display_name", "世界")),
		character_creation_payload
	))


func _change_to_start_scene() -> void:
	var change_error := get_tree().change_scene_to_file(start_scene_path)
	if change_error == OK:
		return

	_set_transition_state(false)
	_show_error("进入游戏失败，请检查场景路径。")
	push_error("Failed to change scene to %s. Error code: %s" % [start_scene_path, change_error])


func _set_transition_state(in_progress: bool) -> void:
	_is_transitioning = in_progress
	start_button.disabled = in_progress
	test_button.disabled = in_progress
	load_button.disabled = in_progress
	settings_button.disabled = in_progress


func _validate_start_scene_path() -> bool:
	if start_scene_path.is_empty():
		_show_error("未配置开始场景。")
		return false
	return true


func _is_modal_open() -> bool:
	return world_preset_picker_window.visible or save_list_window.visible or display_settings_window.visible or character_creation_window.visible


func _show_idle_status() -> void:
	status_label.text = "点击“进入游戏”创建正式世界，点击“加载存档”继续已有进度，或点击“测试地图”创建测试世界。"


func _show_error(message: String) -> void:
	status_label.text = message


func _configure_character_creation_window() -> void:
	var game_session = _get_game_session()
	if game_session == null or not game_session.has_method("get_progression_content_registry"):
		return
	character_creation_window.set_progression_content_registry(game_session.get_progression_content_registry())


func _get_game_session():
	var tree := get_tree()
	if tree == null or tree.root == null:
		return null
	return tree.root.get_node_or_null("GameSession")
