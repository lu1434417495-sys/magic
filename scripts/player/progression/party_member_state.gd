## 文件说明：该脚本属于队伍成员状态相关的状态数据脚本，集中维护成员唯一标识、显示名称、阵营唯一标识等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name PartyMemberState
extends RefCounted

const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const UNIT_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_progress.gd")
const EQUIPMENT_STATE_SCRIPT = preload("res://scripts/player/equipment/equipment_state.gd")

## 字段说明：记录成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var member_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
var display_name: String = ""
## 字段说明：记录阵营唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var faction_id: StringName = &"player"
## 字段说明：记录头像唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var portrait_id: StringName = &""
## 字段说明：记录成长，会参与成长规则判定、序列化和界面展示。
var progression = UNIT_PROGRESS_SCRIPT.new()
## 字段说明：缓存装备状态字典，集中保存可按键查询的运行时数据。
var equipment_state = EQUIPMENT_STATE_SCRIPT.new()
## 字段说明：记录控制模式，用于在不同处理分支之间切换规则或交互方式。
var control_mode: StringName = &"manual"
## 字段说明：记录当前生命值，会参与成长规则判定、序列化和界面展示。
var current_hp := 1
## 字段说明：记录当前法力值，会参与成长规则判定、序列化和界面展示。
var current_mp := 0
## 字段说明：记录体型尺寸，用于布局、碰撞、绘制或程序化生成时的尺寸计算。
var body_size := 1


func to_dict() -> Dictionary:
	return {
		"member_id": String(member_id),
		"display_name": display_name,
		"faction_id": String(faction_id),
		"portrait_id": String(portrait_id),
		"progression": progression.to_dict() if progression != null else {},
		"equipment_state": equipment_state.to_dict() if equipment_state is Object and equipment_state.has_method("to_dict") else {},
		"control_mode": String(control_mode),
		"current_hp": current_hp,
		"current_mp": current_mp,
		"body_size": body_size,
	}


static func from_dict(data: Dictionary):
	if data.is_empty():
		return null
	var progression_data: Variant = data.get("progression", null)
	var equipment_state_data: Variant = data.get("equipment_state", null)
	if progression_data is not Dictionary or equipment_state_data is not Dictionary:
		return null
	var member_state := PARTY_MEMBER_STATE_SCRIPT.new()
	member_state.member_id = ProgressionDataUtils.to_string_name(data.get("member_id", ""))
	member_state.display_name = String(data.get("display_name", ""))
	member_state.faction_id = ProgressionDataUtils.to_string_name(data.get("faction_id", "player"))
	member_state.portrait_id = ProgressionDataUtils.to_string_name(data.get("portrait_id", ""))
	member_state.progression = UNIT_PROGRESS_SCRIPT.from_dict(progression_data)
	member_state.equipment_state = EQUIPMENT_STATE_SCRIPT.from_dict(equipment_state_data)
	member_state.control_mode = ProgressionDataUtils.to_string_name(data.get("control_mode", "manual"))
	member_state.current_hp = int(data.get("current_hp", 1))
	member_state.current_mp = int(data.get("current_mp", 0))
	member_state.body_size = maxi(int(data.get("body_size", 1)), 1)

	if member_state.progression == null or member_state.equipment_state == null:
		return null
	if member_state.progression.unit_id == &"":
		member_state.progression.unit_id = member_state.member_id
	if member_state.progression.display_name.is_empty():
		member_state.progression.display_name = member_state.display_name

	return member_state
