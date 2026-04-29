## 文件说明：该脚本属于战斗边缘面状态相关的状态数据脚本，集中维护边方向、高度差、落差面和人工边特征等顶层字段。
## 审查重点：重点核对边唯一键、阻挡语义、渲染层语义以及运行时解析结果是否仍然可靠。
## 备注：该对象是运行时派生缓存，不作为地图 authoring 的唯一来源；高度变化后应重新生成。

class_name BattleEdgeFaceState
extends RefCounted

const FEATURE_NONE := &"none"
const FEATURE_WALL := &"wall"
const RENDER_NONE := &"none"
const RENDER_WALL := &"wall"

## 字段说明：记录边的源格坐标，用于查表、绘制和运行时判定。
var origin_coord: Vector2i = Vector2i.ZERO
## 字段说明：记录边的目标格坐标，用于查表、绘制和运行时判定。
var neighbor_coord: Vector2i = Vector2i.ZERO
## 字段说明：记录边方向，仅允许 east 或 south，便于复用统一缓存键。
var direction: Vector2i = Vector2i.RIGHT
## 字段说明：记录源格当前高度，用于渲染落差面或人工边特征。
var from_height := 0
## 字段说明：记录目标格当前高度，用于渲染落差面或移动判定。
var to_height := 0
## 字段说明：记录边两侧高度差，用于移动、部署和多格占位校验。
var height_difference := 0
## 字段说明：记录该边需要绘制的落差层数，等价于旧 cliff 渲染的跨度。
var drop_layers := 0
## 字段说明：记录该边真正暴露出来的堆叠层级列表，供真堆叠 cell 渲染逐层拼接侧面。
var drop_face_layer_heights: Array[int] = []
## 字段说明：记录该边的人工特征类型，用于统一承载 wall/door/cover 等未来扩展。
var feature_kind: StringName = FEATURE_NONE
## 字段说明：记录该边人工特征的渲染类型，便于 authoring 与运行时渲染松耦合。
var feature_render_kind: StringName = RENDER_NONE
## 字段说明：记录人工特征绘制层数，当前墙体固定为 1，后续可扩展。
var feature_layers := 0
## 字段说明：用于标记该边特征是否阻挡移动，供寻路、部署和运行时动作共用。
var feature_blocks_move := false
## 字段说明：用于标记该边特征是否阻挡多格单位占位，供 footprint 校验共用。
var feature_blocks_occupancy := false
## 字段说明：用于标记该边特征是否阻挡视线，供未来 LOS 或投射物规则复用。
var feature_blocks_los := false
## 字段说明：记录交互语义，便于 future door/breakable edge 规则直接读取 runtime cache。
var feature_interaction_kind: StringName = &"none"
## 字段说明：记录特征子状态标签，便于区分 open/closed 等运行时 authoring 结果。
var feature_state_tag: StringName = &""


func has_drop_face() -> bool:
	return drop_layers > 0 or not drop_face_layer_heights.is_empty()


func has_feature_face() -> bool:
	return feature_kind != FEATURE_NONE and feature_render_kind != RENDER_NONE and feature_layers > 0


func has_any_face() -> bool:
	return has_drop_face() or has_feature_face()


func blocks_move() -> bool:
	return feature_blocks_move


func blocks_occupancy() -> bool:
	return feature_blocks_occupancy
