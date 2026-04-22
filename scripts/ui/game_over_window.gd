class_name GameOverWindow
extends Control

signal return_requested

@onready var title_label: Label = $Shade/CenterContainer/Panel/MarginContainer/Layout/TitleLabel
@onready var description_label: Label = $Shade/CenterContainer/Panel/MarginContainer/Layout/DescriptionLabel
@onready var return_button: Button = $Shade/CenterContainer/Panel/MarginContainer/Layout/ReturnButton


func _ready() -> void:
	hide_window()
	return_button.focus_mode = Control.FOCUS_ALL
	return_button.pressed.connect(_on_return_button_pressed)


func show_window(context: Dictionary) -> void:
	visible = true
	title_label.text = String(context.get("title", "Game Over"))
	description_label.text = String(context.get("description", "主角已阵亡，本次旅程结束。"))
	return_button.text = String(context.get("confirm_text", "返回标题"))
	return_button.grab_focus()


func hide_window() -> void:
	visible = false
	title_label.text = ""
	description_label.text = ""
	return_button.text = "返回标题"


func _on_return_button_pressed() -> void:
	return_requested.emit()


func _unhandled_input(event: InputEvent) -> void:
	if not visible or event == null:
		return
	if event.is_action_pressed("ui_accept"):
		get_viewport().set_input_as_handled()
		return_requested.emit()
