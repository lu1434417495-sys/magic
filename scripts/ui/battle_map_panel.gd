class_name BattleMapPanel
extends Control

signal battle_cell_clicked(coord: Vector2i)
signal movement_reset_requested
signal resolve_requested

const BATTLE_MAP_GENERATION_SYSTEM_SCRIPT = preload("res://scripts/systems/battle_map_generation_system.gd")

@onready var encounter_label: Label = $Panel/MarginContainer/Layout/Header/HeaderText/EncounterLabel
@onready var movement_label: Label = $Panel/MarginContainer/Layout/MovementLabel
@onready var summary_label: Label = $Panel/MarginContainer/Layout/SummaryLabel
@onready var battle_map_view: BattleMapView = $Panel/MarginContainer/Layout/BattleMapView
@onready var hint_label: Label = $Panel/MarginContainer/Layout/HintLabel
@onready var reset_movement_button: Button = $Panel/MarginContainer/Layout/Header/ButtonRow/ResetMovementButton
@onready var resolve_button: Button = $Panel/MarginContainer/Layout/Header/ButtonRow/ResolveBattleButton


func _ready() -> void:
	visible = false
	battle_map_view.cell_clicked.connect(_on_battle_map_view_cell_clicked)
	reset_movement_button.pressed.connect(_on_reset_movement_button_pressed)
	resolve_button.pressed.connect(_on_resolve_button_pressed)
	hint_label.text = "方向键/WASD 在战斗格上移动，点击格子查看信息，R 重置当前回合行动点，Enter 可直接结束本次遭遇。"


func show_battle(battle_state: Dictionary, selected_coord: Vector2i) -> void:
	visible = true
	refresh(battle_state, selected_coord)


func refresh(battle_state: Dictionary, selected_coord: Vector2i) -> void:
	var monster: Dictionary = battle_state.get("monster", {})
	var monster_name: String = monster.get("display_name", "野怪")
	var map_size: Vector2i = battle_state.get("size", Vector2i.ZERO)
	var terrain_counts: Dictionary = battle_state.get("terrain_counts", {})

	encounter_label.text = "%s  |  战场 %dx%d  |  种子 %d" % [
		monster_name,
		map_size.x,
		map_size.y,
		int(battle_state.get("seed", 0)),
	]
	movement_label.text = "行动点：%d / %d  |  敌方位置：%s  |  玩家位置：%s" % [
		int(battle_state.get("remaining_move", 0)),
		int(battle_state.get("max_move", 0)),
		_format_coord(battle_state.get("enemy_coord", Vector2i.ZERO)),
		_format_coord(battle_state.get("player_coord", Vector2i.ZERO)),
	]
	summary_label.text = "陆地 %d  |  森林 %d  |  水域 %d  |  泥沼 %d  |  地刺 %d" % [
		int(terrain_counts.get(BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_LAND, 0)),
		int(terrain_counts.get(BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_FOREST, 0)),
		int(terrain_counts.get(BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_WATER, 0)),
		int(terrain_counts.get(BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_MUD, 0)),
		int(terrain_counts.get(BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_SPIKE, 0)),
	]
	battle_map_view.configure(battle_state, selected_coord)


func hide_battle() -> void:
	visible = false


func _on_battle_map_view_cell_clicked(coord: Vector2i) -> void:
	battle_cell_clicked.emit(coord)


func _on_reset_movement_button_pressed() -> void:
	movement_reset_requested.emit()


func _on_resolve_button_pressed() -> void:
	resolve_requested.emit()


func _format_coord(coord: Vector2i) -> String:
	return "(%d, %d)" % [coord.x, coord.y]
