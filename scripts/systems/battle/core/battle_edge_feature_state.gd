## 文件说明：该脚本属于战斗边缘特征 authoring 状态相关的状态数据脚本，集中维护边特征类型、阻挡规则、渲染语义和交互语义。
## 审查重点：重点核对特征默认值、预设工厂、序列化兼容以及运行时消费的字段语义是否仍然可靠。
## 备注：该对象属于 authoring/source-of-truth 数据，会被 edge service 解析成 runtime edge-face cache。

class_name BattleEdgeFeatureState
extends RefCounted

const BATTLE_EDGE_FEATURE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_edge_feature_state.gd")

const FEATURE_NONE := &"none"
const FEATURE_WALL := &"wall"
const FEATURE_LOW_WALL := &"low_wall"
const FEATURE_DOOR := &"door"
const FEATURE_GATE := &"gate"

const RENDER_NONE := &"none"
const RENDER_WALL := &"wall"

const INTERACT_NONE := &"none"
const INTERACT_TOGGLE := &"toggle"
const INTERACT_BREAK := &"break"
const SCHEMA_FIELDS: Array[String] = [
	"feature_kind",
	"render_kind",
	"render_layers",
	"blocks_move",
	"blocks_occupancy",
	"blocks_los",
	"interaction_kind",
	"state_tag",
]

## 字段说明：记录 authoring 特征类型，作为规则、渲染和交互层的上层语义入口。
var feature_kind: StringName = FEATURE_NONE
## 字段说明：记录该特征使用的渲染样式，供 edge-face 渲染层决定贴图来源。
var render_kind: StringName = RENDER_NONE
## 字段说明：记录该特征渲染层数，当前墙体固定为 1，未来可扩展为更高立面。
var render_layers := 0
## 字段说明：用于标记该特征是否阻挡移动，供寻路、部署和 runtime action 共用。
var blocks_move := false
## 字段说明：用于标记该特征是否阻挡占位，供 footprint 边检查共用。
var blocks_occupancy := false
## 字段说明：用于标记该特征是否阻挡视线，供未来 LOS、投射物或掩体规则复用。
var blocks_los := false
## 字段说明：记录交互语义，供未来开门、破坏或脚本驱动边特征变化使用。
var interaction_kind: StringName = INTERACT_NONE
## 字段说明：记录可选的 authoring 标签，便于策划区分 door_open/door_closed 等子状态。
var state_tag: StringName = &""


func is_empty() -> bool:
	return feature_kind == FEATURE_NONE and render_kind == RENDER_NONE and render_layers <= 0


func duplicates_render_of(other_feature_kind: StringName) -> bool:
	return render_kind == other_feature_kind


func duplicate_feature() -> BattleEdgeFeatureState:
	return from_dict(to_dict())


func to_dict() -> Dictionary:
	return {
		"feature_kind": String(feature_kind),
		"render_kind": String(render_kind),
		"render_layers": render_layers,
		"blocks_move": blocks_move,
		"blocks_occupancy": blocks_occupancy,
		"blocks_los": blocks_los,
		"interaction_kind": String(interaction_kind),
		"state_tag": String(state_tag),
	}


static func from_dict(data: Variant) -> BattleEdgeFeatureState:
	if data is not Dictionary:
		return null
	var feature_dict := data as Dictionary
	if not _has_exact_schema_fields(feature_dict):
		return null
	var feature_kind: Variant = feature_dict["feature_kind"]
	var render_kind: Variant = feature_dict["render_kind"]
	var render_layers: Variant = feature_dict["render_layers"]
	var blocks_move_value: Variant = feature_dict["blocks_move"]
	var blocks_occupancy_value: Variant = feature_dict["blocks_occupancy"]
	var blocks_los_value: Variant = feature_dict["blocks_los"]
	var interaction_kind: Variant = feature_dict["interaction_kind"]
	var state_tag: Variant = feature_dict["state_tag"]
	if not _is_non_empty_string_like(feature_kind):
		return null
	if not _is_non_empty_string_like(render_kind):
		return null
	if not _is_non_empty_string_like(interaction_kind):
		return null
	if not _is_string_like(state_tag):
		return null
	if render_layers is not int or render_layers < 0:
		return null
	if blocks_move_value is not bool or blocks_occupancy_value is not bool or blocks_los_value is not bool:
		return null
	var feature_state := BATTLE_EDGE_FEATURE_STATE_SCRIPT.new()
	feature_state.feature_kind = StringName(feature_kind)
	feature_state.render_kind = StringName(render_kind)
	feature_state.render_layers = render_layers
	feature_state.blocks_move = blocks_move_value
	feature_state.blocks_occupancy = blocks_occupancy_value
	feature_state.blocks_los = blocks_los_value
	feature_state.interaction_kind = StringName(interaction_kind)
	feature_state.state_tag = StringName(state_tag)
	return feature_state


static func make_none() -> BattleEdgeFeatureState:
	return BATTLE_EDGE_FEATURE_STATE_SCRIPT.new()


static func make_wall() -> BattleEdgeFeatureState:
	var feature_state := BATTLE_EDGE_FEATURE_STATE_SCRIPT.new()
	feature_state.feature_kind = FEATURE_WALL
	feature_state.render_kind = RENDER_WALL
	feature_state.render_layers = 1
	feature_state.blocks_move = true
	feature_state.blocks_occupancy = true
	feature_state.blocks_los = true
	return feature_state


static func make_low_wall() -> BattleEdgeFeatureState:
	var feature_state := BATTLE_EDGE_FEATURE_STATE_SCRIPT.new()
	feature_state.feature_kind = FEATURE_LOW_WALL
	feature_state.render_kind = RENDER_WALL
	feature_state.render_layers = 1
	feature_state.blocks_move = false
	feature_state.blocks_occupancy = false
	feature_state.blocks_los = false
	return feature_state


static func make_toggle_door(is_open: bool = false) -> BattleEdgeFeatureState:
	var feature_state := BATTLE_EDGE_FEATURE_STATE_SCRIPT.new()
	feature_state.feature_kind = FEATURE_DOOR
	feature_state.render_kind = RENDER_WALL if not is_open else RENDER_NONE
	feature_state.render_layers = 1 if not is_open else 0
	feature_state.blocks_move = not is_open
	feature_state.blocks_occupancy = not is_open
	feature_state.blocks_los = not is_open
	feature_state.interaction_kind = INTERACT_TOGGLE
	feature_state.state_tag = &"open" if is_open else &"closed"
	return feature_state


static func _has_exact_schema_fields(feature_dict: Dictionary) -> bool:
	if feature_dict.size() != SCHEMA_FIELDS.size():
		return false
	for key_variant in feature_dict.keys():
		if key_variant is not String:
			return false
		if not SCHEMA_FIELDS.has(key_variant):
			return false
	for field in SCHEMA_FIELDS:
		if not feature_dict.has(field):
			return false
	return true


static func _is_string_like(value: Variant) -> bool:
	return value is String or value is StringName


static func _is_non_empty_string_like(value: Variant) -> bool:
	if not _is_string_like(value):
		return false
	return not String(value).is_empty()
