## 文件说明：该脚本属于战斗格子状态相关的状态数据脚本，集中维护坐标、基础地形、基础高度等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleCellState
extends RefCounted

const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle_cell_state.gd")
const BATTLE_TERRAIN_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle_terrain_effect_state.gd")
const BattleTerrainEffectState = preload("res://scripts/systems/battle_terrain_effect_state.gd")
const BattleEdgeFeatureState = preload("res://scripts/systems/battle_edge_feature_state.gd")
const TERRAIN_LAND := &"land"
const TERRAIN_FOREST := &"forest"
const TERRAIN_WATER := &"water"
const TERRAIN_MUD := &"mud"
const TERRAIN_SPIKE := &"spike"
const MIN_RUNTIME_HEIGHT := -5
const MAX_RUNTIME_HEIGHT := 8

## 字段说明：记录对象当前使用的网格坐标，供绘制、寻路或占位计算使用。
var coord: Vector2i = Vector2i.ZERO
## 字段说明：记录该格子在同一列中的真实堆叠层级，供真堆叠地形数据和暴露侧面计算使用。
var stack_layer := 0
## 字段说明：记录基础地形，会参与运行时状态流转、系统协作和存档恢复。
var base_terrain: StringName = &"land"
## 字段说明：记录基础高度，会参与运行时状态流转、系统协作和存档恢复。
var base_height := 0
## 字段说明：记录高度偏移，会参与运行时状态流转、系统协作和存档恢复。
var height_offset := 0
## 字段说明：记录当前高度，会参与运行时状态流转、系统协作和存档恢复。
var current_height := 0
## 字段说明：用于标记可通行当前是否成立或生效，供脚本后续分支判断使用，会参与运行时状态流转、系统协作和存档恢复。
var passable := true
## 字段说明：记录移动消耗，会参与运行时状态流转、系统协作和存档恢复。
var move_cost := 1
## 字段说明：记录占用者单位唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var occupant_unit_id: StringName = &""
## 字段说明：保存场景装饰物标识列表，便于批量遍历、交叉查找和界面展示。
var prop_ids: Array[StringName] = []
## 字段说明：保存地形效果标识列表，便于批量遍历、交叉查找和界面展示。
var terrain_effect_ids: Array[StringName] = []
## 字段说明：保存计时地形效果集合，便于顺序遍历、批量展示、批量运算和整体重建。
var timed_terrain_effects: Array[BattleTerrainEffectState] = []
## 字段说明：记录东侧边缘 authoring 特征，作为统一 edge-face 系统的 source-of-truth。
var edge_feature_east: BattleEdgeFeatureState = BattleEdgeFeatureState.make_none()
## 字段说明：记录南侧边缘 authoring 特征，作为统一 edge-face 系统的 source-of-truth。
var edge_feature_south: BattleEdgeFeatureState = BattleEdgeFeatureState.make_none()


func clear_occupant() -> void:
	occupant_unit_id = &""


func recalculate_runtime_values() -> void:
	current_height = clampi(base_height + height_offset, MIN_RUNTIME_HEIGHT, MAX_RUNTIME_HEIGHT)
	stack_layer = current_height
	passable = base_terrain != TERRAIN_WATER
	move_cost = 2 if base_terrain == TERRAIN_MUD or base_terrain == TERRAIN_SPIKE else 1


func set_base_terrain(terrain: StringName) -> void:
	base_terrain = terrain
	recalculate_runtime_values()


func set_height_offset(offset: int) -> void:
	height_offset = offset
	recalculate_runtime_values()


func get_edge_feature(direction: Vector2i) -> BattleEdgeFeatureState:
	match direction:
		Vector2i.RIGHT:
			return edge_feature_east
		Vector2i.DOWN:
			return edge_feature_south
		_:
			return null


func set_edge_feature(direction: Vector2i, feature_state: BattleEdgeFeatureState) -> void:
	var normalized_feature := _normalize_edge_feature(feature_state)
	match direction:
		Vector2i.RIGHT:
			edge_feature_east = normalized_feature
		Vector2i.DOWN:
			edge_feature_south = normalized_feature


func clear_edge_feature(direction: Vector2i) -> void:
	set_edge_feature(direction, BattleEdgeFeatureState.make_none())


func duplicate_cell() -> BattleCellState:
	var cloned := BATTLE_CELL_STATE_SCRIPT.new()
	cloned.coord = coord
	cloned.stack_layer = stack_layer
	cloned.base_terrain = base_terrain
	cloned.base_height = base_height
	cloned.height_offset = height_offset
	cloned.current_height = current_height
	cloned.passable = passable
	cloned.move_cost = move_cost
	cloned.occupant_unit_id = occupant_unit_id
	cloned.prop_ids = prop_ids.duplicate()
	cloned.terrain_effect_ids = terrain_effect_ids.duplicate()
	cloned.timed_terrain_effects = BATTLE_TERRAIN_EFFECT_STATE_SCRIPT.duplicate_array(timed_terrain_effects)
	cloned.edge_feature_east = _normalize_edge_feature(edge_feature_east)
	cloned.edge_feature_south = _normalize_edge_feature(edge_feature_south)
	return cloned


func to_dict() -> Dictionary:
	return {
		"coord": coord,
		"stack_layer": stack_layer,
		"base_terrain": String(base_terrain),
		"base_height": base_height,
		"height_offset": height_offset,
		"current_height": current_height,
		"passable": passable,
		"move_cost": move_cost,
		"occupant_unit_id": String(occupant_unit_id),
		"prop_ids": _string_name_array_to_strings(prop_ids),
		"terrain_effect_ids": _string_name_array_to_strings(terrain_effect_ids),
		"timed_terrain_effects": BATTLE_TERRAIN_EFFECT_STATE_SCRIPT.to_dict_array(timed_terrain_effects),
		"edge_feature_east": edge_feature_east.to_dict() if edge_feature_east != null else {},
		"edge_feature_south": edge_feature_south.to_dict() if edge_feature_south != null else {},
	}


static func from_dict(data: Dictionary) -> BattleCellState:
	var cell_state := BATTLE_CELL_STATE_SCRIPT.new()
	cell_state.coord = data.get("coord", Vector2i.ZERO)
	cell_state.stack_layer = int(data.get("stack_layer", int(data.get("current_height", cell_state.base_height))))
	cell_state.base_terrain = StringName(String(data.get("base_terrain", "land")))
	cell_state.base_height = int(data.get("base_height", 0))
	cell_state.height_offset = int(data.get("height_offset", 0))
	cell_state.current_height = int(data.get("current_height", cell_state.base_height))
	cell_state.passable = bool(data.get("passable", true))
	cell_state.move_cost = int(data.get("move_cost", 1))
	cell_state.occupant_unit_id = StringName(String(data.get("occupant_unit_id", "")))
	cell_state.prop_ids = _strings_to_string_name_array(data.get("prop_ids", []))
	cell_state.terrain_effect_ids = _strings_to_string_name_array(data.get("terrain_effect_ids", []))
	cell_state.timed_terrain_effects = BATTLE_TERRAIN_EFFECT_STATE_SCRIPT.from_dict_array(data.get("timed_terrain_effects", []))
	cell_state.edge_feature_east = _normalize_edge_feature(BattleEdgeFeatureState.from_dict(data.get("edge_feature_east", {})))
	cell_state.edge_feature_south = _normalize_edge_feature(BattleEdgeFeatureState.from_dict(data.get("edge_feature_south", {})))
	return cell_state


static func _string_name_array_to_strings(values: Array[StringName]) -> Array[String]:
	var results: Array[String] = []
	for value in values:
		results.append(String(value))
	return results


static func _strings_to_string_name_array(values: Variant) -> Array[StringName]:
	var results: Array[StringName] = []
	if values is Array:
		for value in values:
			results.append(StringName(String(value)))
	return results


static func _normalize_edge_feature(feature_state: BattleEdgeFeatureState) -> BattleEdgeFeatureState:
	if feature_state == null:
		return BattleEdgeFeatureState.make_none()
	return feature_state.duplicate_feature()


static func build_columns_from_surface_cells(surface_cells: Dictionary) -> Dictionary:
	var columns: Dictionary = {}
	for coord_variant in surface_cells.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		var surface_cell := surface_cells.get(coord_variant) as BattleCellState
		if surface_cell == null:
			continue
		columns[coord] = build_stacked_cells_from_surface_cell(surface_cell)
	return columns


static func clone_columns(columns: Dictionary) -> Dictionary:
	var cloned: Dictionary = {}
	for coord_variant in columns.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		var column_variant: Variant = columns.get(coord_variant, [])
		var cloned_column: Array[BattleCellState] = []
		if column_variant is Array:
			for layer_variant in column_variant:
				var layer_cell := layer_variant as BattleCellState
				if layer_cell == null:
					continue
				cloned_column.append(layer_cell.duplicate_cell())
		cloned[coord] = cloned_column
	return cloned


static func build_stacked_cells_from_surface_cell(surface_cell: BattleCellState) -> Array[BattleCellState]:
	var column: Array[BattleCellState] = []
	if surface_cell == null:
		return column
	var top_layer := int(surface_cell.current_height)
	if top_layer >= 0:
		for layer in range(0, top_layer):
			var support_cell := BATTLE_CELL_STATE_SCRIPT.new()
			support_cell.coord = surface_cell.coord
			support_cell.stack_layer = layer
			support_cell.base_terrain = surface_cell.base_terrain
			support_cell.base_height = layer
			support_cell.height_offset = 0
			support_cell.current_height = layer
			support_cell.passable = false
			support_cell.move_cost = 1
			support_cell.occupant_unit_id = &""
			support_cell.prop_ids = []
			support_cell.terrain_effect_ids = []
			support_cell.timed_terrain_effects = []
			column.append(support_cell)
	var top_cell := surface_cell.duplicate_cell()
	top_cell.coord = surface_cell.coord
	top_cell.stack_layer = top_layer
	top_cell.current_height = top_layer
	column.append(top_cell)
	return column
