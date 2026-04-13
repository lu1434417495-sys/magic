## 文件说明：该脚本属于战斗棋盘场景装饰物相关的界面脚本，集中维护场景装饰物唯一标识、变体随机种子、需要交互形状等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name BattleBoardProp
extends Node2D

const BattleBoardPropCatalog = preload("res://scripts/utils/battle_board_prop_catalog.gd")

## 字段说明：记录场景装饰物唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var prop_id: StringName = BattleBoardPropCatalog.PROP_TENT
## 字段说明：记录变体随机种子，作为界面刷新、输入处理和窗口联动的重要依据。
var _variant_seed := 0
## 字段说明：用于标记后续流程是否需要补做交互形状，从而延后昂贵或依赖性较强的操作，作为界面刷新、输入处理和窗口联动的重要依据。
var _needs_interaction_shape := false

## 字段说明：缓存交互范围节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var interaction_area: Area2D = %InteractionArea
## 字段说明：缓存交互形状节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var interaction_shape: CollisionShape2D = %InteractionShape


func _ready() -> void:
	_apply_interaction_state()
	queue_redraw()


func configure(new_prop_id: StringName, variant_seed: int = 0, needs_interaction_shape: bool = false) -> void:
	prop_id = new_prop_id
	_variant_seed = variant_seed
	_needs_interaction_shape = needs_interaction_shape
	_apply_interaction_state()
	queue_redraw()


func _draw() -> void:
	match prop_id:
		BattleBoardPropCatalog.PROP_SPIKE_BARRICADE:
			_draw_spike_barricade()
		BattleBoardPropCatalog.PROP_OBJECTIVE_MARKER:
			_draw_objective_marker()
		BattleBoardPropCatalog.PROP_TORCH:
			_draw_torch()
		_:
			_draw_tent()


func _apply_interaction_state() -> void:
	if interaction_area == null or interaction_shape == null:
		return
	interaction_area.monitoring = _needs_interaction_shape
	interaction_area.monitorable = _needs_interaction_shape
	interaction_shape.disabled = not _needs_interaction_shape


func _draw_spike_barricade() -> void:
	var wood_dark := Color(0.22, 0.11, 0.06, 0.96)
	var wood_mid := Color(0.46, 0.26, 0.15, 0.98)
	var brace_y := -6.0
	var points: Array[Vector2] = [
		Vector2(-16.0, 0.0),
		Vector2(-8.0, -24.0),
		Vector2(0.0, -4.0),
		Vector2(9.0, -27.0),
		Vector2(17.0, -2.0),
	]
	for index in range(points.size()):
		var tip: Vector2 = points[index]
		var sway := _signed_offset(index + 1, 2.0)
		draw_line(Vector2(tip.x + sway, 0.0), Vector2(tip.x, tip.y), wood_mid, 4.0, true)
		draw_circle(Vector2(tip.x, tip.y), 2.6, wood_dark)
	draw_line(Vector2(-18.0, brace_y), Vector2(18.0, brace_y - 2.0), wood_dark, 3.0, true)


func _draw_objective_marker() -> void:
	var pole_dark := Color(0.22, 0.12, 0.08, 0.98)
	var pole_light := Color(0.58, 0.38, 0.22, 0.98)
	var cloth_main := Color(0.9, 0.72, 0.3, 0.96)
	var cloth_shadow := Color(0.7, 0.44, 0.16, 0.96)
	draw_line(Vector2(0.0, 0.0), Vector2(0.0, -34.0), pole_light, 5.0, true)
	draw_line(Vector2(0.0, 0.0), Vector2(0.0, -34.0), pole_dark, 1.2, true)
	draw_polygon(
		PackedVector2Array([
			Vector2(0.0, -32.0),
			Vector2(18.0, -28.0 + _signed_offset(3, 1.5)),
			Vector2(11.0, -14.0),
			Vector2(0.0, -18.0),
		]),
		PackedColorArray([cloth_main, cloth_main, cloth_shadow, cloth_shadow])
	)
	draw_circle(Vector2(0.0, -36.0), 4.0, Color(0.96, 0.88, 0.58, 0.98))


func _draw_tent() -> void:
	var canvas_main := Color(0.72, 0.58, 0.38, 0.96)
	var canvas_shadow := Color(0.46, 0.32, 0.18, 0.98)
	var trim := Color(0.28, 0.17, 0.1, 0.96)
	var width := 15.0 + _ratio(5) * 5.0
	draw_polygon(
		PackedVector2Array([
			Vector2(-width, 0.0),
			Vector2(0.0, -22.0),
			Vector2(width, 0.0),
		]),
		PackedColorArray([canvas_shadow, canvas_main, canvas_shadow])
	)
	draw_line(Vector2(0.0, -22.0), Vector2(0.0, 0.0), trim, 2.0, true)
	draw_line(Vector2(-width - 2.0, 0.0), Vector2(width + 2.0, 0.0), trim, 3.0, true)


func _draw_torch() -> void:
	var pole := Color(0.34, 0.18, 0.1, 0.98)
	var ember := Color(0.98, 0.48, 0.12, 0.92)
	var flame := Color(1.0, 0.78, 0.32, 0.84)
	draw_line(Vector2(0.0, 0.0), Vector2(0.0, -22.0), pole, 4.0, true)
	draw_circle(Vector2(_signed_offset(7, 1.5), -25.0), 5.0 + _ratio(8) * 2.0, ember)
	draw_circle(Vector2(_signed_offset(9, 1.0), -28.0), 3.6 + _ratio(10) * 1.5, flame)


func _signed_offset(salt: int, magnitude: float) -> float:
	return magnitude if _stable_hash(salt) % 2 == 0 else -magnitude


func _ratio(salt: int) -> float:
	return float(_stable_hash(salt) % 1000) / 1000.0


func _stable_hash(salt: int) -> int:
	var hash_value := int(_variant_seed) * 1103515245 + salt * 12345
	return absi(hash_value)
