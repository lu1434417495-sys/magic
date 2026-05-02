## 文件说明：该脚本属于遭遇锚点数据相关的数据对象脚本，集中维护实体唯一标识、显示名称、世界坐标等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name EncounterAnchorData
extends RefCounted

const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")
const ENCOUNTER_KIND_SINGLE: StringName = &"single"
const ENCOUNTER_KIND_SETTLEMENT: StringName = &"settlement"
const REQUIRED_SERIALIZED_FIELDS := [
	"entity_id",
	"display_name",
	"world_coord",
	"faction_id",
	"enemy_roster_template_id",
	"region_tag",
	"vision_range",
	"is_cleared",
	"encounter_kind",
	"encounter_profile_id",
	"growth_stage",
	"suppressed_until_step",
]

## 字段说明：记录实体唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var entity_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
var display_name: String = ""
## 字段说明：记录对象在世界地图中的坐标，供探索定位、遭遇生成和存档恢复复用。
var world_coord: Vector2i = Vector2i.ZERO
## 字段说明：记录阵营唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var faction_id: StringName = &"hostile"
## 字段说明：记录敌方编队模板唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var enemy_roster_template_id: StringName = &""
## 字段说明：记录区域标签，会参与运行时状态流转、系统协作和存档恢复。
var region_tag: StringName = &""
## 字段说明：记录视野范围，用于可见性、判定半径、生成条件或技能作用范围计算。
var vision_range := 0
## 字段说明：用于标记当前是否处于已清除状态，避免在不合适的时机重复触发流程，会参与运行时状态流转、系统协作和存档恢复。
var is_cleared := false
## 字段说明：记录遭遇对象类别，用于区分一次性单体野怪与可成长的聚落类野怪。
var encounter_kind: StringName = ENCOUNTER_KIND_SINGLE
## 字段说明：记录聚落类遭遇的编队配置标识，供混编规则与成长逻辑查表。
var encounter_profile_id: StringName = &""
## 字段说明：记录当前聚落类遭遇的成长阶段，供世界推进与战斗编队构建复用。
var growth_stage := 0
## 字段说明：记录聚落类遭遇被压制到何时恢复增长，单位为世界 step。
var suppressed_until_step := 0


func to_dict() -> Dictionary:
	return {
		"entity_id": String(entity_id),
		"display_name": display_name,
		"world_coord": world_coord,
		"faction_id": String(faction_id),
		"enemy_roster_template_id": String(enemy_roster_template_id),
		"region_tag": String(region_tag),
		"vision_range": vision_range,
		"is_cleared": is_cleared,
		"encounter_kind": String(encounter_kind),
		"encounter_profile_id": String(encounter_profile_id),
		"growth_stage": growth_stage,
		"suppressed_until_step": suppressed_until_step,
	}


static func from_dict(data: Variant):
	if data is not Dictionary:
		return null
	var payload := data as Dictionary
	if not _has_exact_serialized_fields(payload):
		return null

	var entity_id_value = _parse_string_name_field(payload["entity_id"], false)
	var faction_id_value = _parse_string_name_field(payload["faction_id"], false)
	var enemy_roster_template_id_value = _parse_string_name_field(payload["enemy_roster_template_id"], true)
	var region_tag_value = _parse_string_name_field(payload["region_tag"], true)
	var encounter_kind_value = _parse_string_name_field(payload["encounter_kind"], false)
	var encounter_profile_id_value = _parse_string_name_field(payload["encounter_profile_id"], true)
	if (
		entity_id_value == null
		or faction_id_value == null
		or enemy_roster_template_id_value == null
		or region_tag_value == null
		or encounter_kind_value == null
		or encounter_profile_id_value == null
	):
		return null
	if not _is_valid_encounter_kind(encounter_kind_value):
		return null
	if payload["display_name"] is not String:
		return null
	var display_name_value := String(payload["display_name"])
	if display_name_value.strip_edges().is_empty():
		return null
	if payload["world_coord"] is not Vector2i:
		return null
	if payload["vision_range"] is not int or int(payload["vision_range"]) < 0:
		return null
	if payload["is_cleared"] is not bool:
		return null
	if payload["growth_stage"] is not int or int(payload["growth_stage"]) < 0:
		return null
	if payload["suppressed_until_step"] is not int or int(payload["suppressed_until_step"]) < 0:
		return null

	var encounter_anchor := ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = entity_id_value
	encounter_anchor.display_name = display_name_value
	encounter_anchor.world_coord = payload["world_coord"]
	encounter_anchor.faction_id = faction_id_value
	encounter_anchor.enemy_roster_template_id = enemy_roster_template_id_value
	encounter_anchor.region_tag = region_tag_value
	encounter_anchor.vision_range = int(payload["vision_range"])
	encounter_anchor.is_cleared = bool(payload["is_cleared"])
	encounter_anchor.encounter_kind = encounter_kind_value
	encounter_anchor.encounter_profile_id = encounter_profile_id_value
	encounter_anchor.growth_stage = int(payload["growth_stage"])
	encounter_anchor.suppressed_until_step = int(payload["suppressed_until_step"])
	return encounter_anchor


static func _has_exact_serialized_fields(payload: Dictionary) -> bool:
	if payload.size() != REQUIRED_SERIALIZED_FIELDS.size():
		return false
	for field_name in REQUIRED_SERIALIZED_FIELDS:
		if not payload.has(field_name):
			return false
	return true


static func _parse_string_name_field(value: Variant, allow_empty: bool):
	var value_type := typeof(value)
	if value_type != TYPE_STRING and value_type != TYPE_STRING_NAME:
		return null
	var text := String(value).strip_edges()
	if text.is_empty() and not allow_empty:
		return null
	return StringName(text)


static func _is_valid_encounter_kind(value: StringName) -> bool:
	return value == ENCOUNTER_KIND_SINGLE or value == ENCOUNTER_KIND_SETTLEMENT
