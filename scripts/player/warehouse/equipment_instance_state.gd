## 文件说明：装备实例状态，表达一件已实例化装备的唯一身份与可变属性。
## 审查重点：instance_id 必须由 world-level 分配器传入；其余字段均为可序列化的值类型。
## 备注：current_durability 为 -1 表示当前无耐久数据（Phase 2 阶段占位）。

class_name EquipmentInstanceState
extends RefCounted

const SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const SAVE_PAYLOAD_LABEL := "save equipment instance payload"
const TRANSIENT_LOOT_PAYLOAD_LABEL := "transient loot equipment instance payload"

enum RarityTier {
	COMMON,
	UNCOMMON,
	RARE,
	EPIC,
	LEGENDARY,
}

## 字段说明：装备实例唯一标识，创建后不可变更。
var instance_id: StringName = &""
## 字段说明：对应物品定义的 item_id，用于查找静态属性。
var item_id: StringName = &""
## 字段说明：装备品质层级。
var rarity: int = RarityTier.COMMON
## 字段说明：当前剩余耐久；-1 表示耐久功能尚未启用。
var current_durability: int = -1
## 字段说明：护甲磨损累计进度；0.0 为未磨损（阈值由规则层决定）。
var armor_wear_progress: float = 0.0
## 字段说明：武器磨耗累计进度；0.0 为未磨耗（阈值由规则层决定）。
var weapon_wear_progress: float = 0.0


## 创建新实例；instance_id 由持有 world_data 的服务路径分配。
static func create(p_item_id: StringName, p_instance_id: StringName = &"") -> EquipmentInstanceState:
	var inst := SCRIPT.new()
	inst.instance_id = ProgressionDataUtils.to_string_name(p_instance_id)
	inst.item_id = ProgressionDataUtils.to_string_name(p_item_id)
	return inst


static func format_instance_id(serial: int) -> StringName:
	return StringName("eq_%06d" % maxi(int(serial), 1))


static func format_preview_instance_id(serial: int) -> StringName:
	return StringName("__preview_eq_%06d" % maxi(int(serial), 1))


func to_dict() -> Dictionary:
	return {
		"instance_id": String(instance_id),
		"item_id": String(item_id),
		"rarity": rarity,
		"current_durability": current_durability,
		"armor_wear_progress": armor_wear_progress,
		"weapon_wear_progress": weapon_wear_progress,
	}


static func from_dict(data: Variant) -> EquipmentInstanceState:
	return _from_dict(data, false, true, SAVE_PAYLOAD_LABEL)


static func from_transient_loot_dict(data: Variant) -> EquipmentInstanceState:
	return _from_dict(data, true, false, TRANSIENT_LOOT_PAYLOAD_LABEL)


static func get_payload_validation_error(data: Variant, allow_empty_instance_id: bool = false) -> String:
	return _get_payload_validation_error(data, allow_empty_instance_id, SAVE_PAYLOAD_LABEL)


static func _from_dict(
	data: Variant,
	allow_empty_instance_id: bool,
	crash_on_invalid: bool,
	payload_label: String
) -> EquipmentInstanceState:
	var validation_error := _get_payload_validation_error(data, allow_empty_instance_id, payload_label)
	if not validation_error.is_empty():
		push_error(validation_error)
		if crash_on_invalid:
			_crash_due_to_corrupt_save(validation_error)
		return null
	var payload := data as Dictionary
	var inst := SCRIPT.new()
	inst.instance_id = ProgressionDataUtils.to_string_name(payload.get("instance_id", ""))
	inst.item_id = ProgressionDataUtils.to_string_name(payload.get("item_id", ""))
	inst.rarity = int(payload.get("rarity", RarityTier.COMMON))
	inst.current_durability = int(payload.get("current_durability", -1))
	inst.armor_wear_progress = float(payload.get("armor_wear_progress", 0.0))
	inst.weapon_wear_progress = float(payload.get("weapon_wear_progress", 0.0))
	return inst


static func _get_payload_validation_error(
	data: Variant,
	allow_empty_instance_id: bool,
	payload_label: String
) -> String:
	if data is not Dictionary:
		return "Corrupt %s: expected Dictionary, got %s." % [payload_label, type_string(typeof(data))]
	var payload := data as Dictionary
	var required_fields := [
		"instance_id",
		"item_id",
		"rarity",
		"current_durability",
		"armor_wear_progress",
		"weapon_wear_progress",
	]
	for field_name in required_fields:
		if not payload.has(field_name):
			return "Corrupt %s: missing required field '%s'." % [payload_label, field_name]
	var instance_id := ProgressionDataUtils.to_string_name(payload.get("instance_id", ""))
	var item_id := ProgressionDataUtils.to_string_name(payload.get("item_id", ""))
	var rarity_value := int(payload.get("rarity", RarityTier.COMMON))
	if instance_id == &"" and not allow_empty_instance_id:
		return "Corrupt %s: instance_id is required." % payload_label
	if item_id == &"":
		return "Corrupt %s: item_id is required for instance '%s'." % [payload_label, String(instance_id)]
	if not is_valid_rarity(rarity_value):
		return "Corrupt %s: invalid rarity %d for instance '%s'." % [
			payload_label,
			rarity_value,
			String(instance_id),
		]
	return ""


static func _crash_due_to_corrupt_save(message: String) -> void:
	if OS.has_method("crash"):
		OS.call("crash", message)
	var main_loop := Engine.get_main_loop()
	if main_loop != null and main_loop.has_method("quit"):
		main_loop.call("quit", 1)


static func is_valid_rarity(value: int) -> bool:
	return value >= RarityTier.COMMON and value <= RarityTier.LEGENDARY
