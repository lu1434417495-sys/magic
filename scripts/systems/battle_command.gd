## 文件说明：该脚本属于战斗指令相关的业务脚本，集中维护指令类型、单位唯一标识、技能唯一标识等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleCommand
extends RefCounted

const TYPE_MOVE: StringName = &"move"
const TYPE_SKILL: StringName = &"skill"
const TYPE_WAIT: StringName = &"wait"

## 字段说明：记录指令类型，用于区分不同规则、资源类别或行为分支。
var command_type: StringName = &""
## 字段说明：记录单位唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var unit_id: StringName = &""
## 字段说明：记录技能唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var skill_id: StringName = &""
## 字段说明：记录技能变体唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var skill_variant_id: StringName = &""
## 字段说明：记录目标单位唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var target_unit_id: StringName = &""
## 字段说明：记录目标坐标，用于定位对象、绘制内容或执行网格计算。
var target_coord: Vector2i = Vector2i(-1, -1)
## 字段说明：保存目标坐标列表，供范围判定、占位刷新、批量渲染或目标选择复用。
var target_coords: Array[Vector2i] = []


func is_move() -> bool:
	return command_type == TYPE_MOVE


func is_skill() -> bool:
	return command_type == TYPE_SKILL


func is_wait() -> bool:
	return command_type == TYPE_WAIT
