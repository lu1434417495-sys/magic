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
	return _from_dict(data, false, SAVE_PAYLOAD_LABEL)


static func from_transient_loot_dict(data: Variant) -> EquipmentInstanceState:
	return _from_dict(data, true, TRANSIENT_LOOT_PAYLOAD_LABEL)


static func get_payload_validation_error(data: Variant, allow_empty_instance_id: bool = false) -> String:
	return _get_payload_validation_error(data, allow_empty_instance_id, SAVE_PAYLOAD_LABEL)


static func _from_dict(
	data: Variant,
	allow_empty_instance_id: bool,
	payload_label: String
) -> EquipmentInstanceState:
	var validation_error := _get_payload_validation_error(data, allow_empty_instance_id, payload_label)
	if not validation_error.is_empty():
		push_error(validation_error)
		return null
	var payload := data as Dictionary
	var inst := SCRIPT.new()
	inst.instance_id = ProgressionDataUtils.to_string_name(payload["instance_id"])
	inst.item_id = ProgressionDataUtils.to_string_name(payload["item_id"])
	inst.rarity = int(payload["rarity"])
	inst.current_durability = int(payload["current_durability"])
	inst.armor_wear_progress = float(payload["armor_wear_progress"])
	inst.weapon_wear_progress = float(payload["weapon_wear_progress"])
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
	if payload.size() != required_fields.size():
		return "Corrupt %s: expected exactly current equipment instance fields." % payload_label
	for key_variant in payload.keys():
		if key_variant is not String or not required_fields.has(key_variant):
			return "Corrupt %s: unsupported field '%s'." % [payload_label, String(key_variant)]
	var instance_id_variant: Variant = payload["instance_id"]
	if not _is_string_name_payload_value(instance_id_variant):
		return "Corrupt %s: instance_id must be String or StringName." % payload_label
	var item_id_variant: Variant = payload["item_id"]
	if not _is_string_name_payload_value(item_id_variant):
		return "Corrupt %s: item_id must be String or StringName." % payload_label
	var rarity_variant: Variant = payload["rarity"]
	if rarity_variant is not int:
		return "Corrupt %s: rarity must be int." % payload_label
	var current_durability_variant: Variant = payload["current_durability"]
	if current_durability_variant is not int:
		return "Corrupt %s: current_durability must be int." % payload_label
	var armor_wear_progress_variant: Variant = payload["armor_wear_progress"]
	if armor_wear_progress_variant is not float:
		return "Corrupt %s: armor_wear_progress must be float." % payload_label
	var weapon_wear_progress_variant: Variant = payload["weapon_wear_progress"]
	if weapon_wear_progress_variant is not float:
		return "Corrupt %s: weapon_wear_progress must be float." % payload_label

	var instance_id := ProgressionDataUtils.to_string_name(instance_id_variant)
	var item_id := ProgressionDataUtils.to_string_name(item_id_variant)
	var rarity_value := int(rarity_variant)
	var current_durability := int(current_durability_variant)
	var armor_wear_progress := float(armor_wear_progress_variant)
	var weapon_wear_progress := float(weapon_wear_progress_variant)
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
	if current_durability < -1:
		return "Corrupt %s: invalid current_durability %d for instance '%s'." % [
			payload_label,
			current_durability,
			String(instance_id),
		]
	if armor_wear_progress < 0.0:
		return "Corrupt %s: armor_wear_progress must be non-negative for instance '%s'." % [
			payload_label,
			String(instance_id),
		]
	if weapon_wear_progress < 0.0:
		return "Corrupt %s: weapon_wear_progress must be non-negative for instance '%s'." % [
			payload_label,
			String(instance_id),
		]
	return ""


static func is_valid_rarity(value: int) -> bool:
	return value >= RarityTier.COMMON and value <= RarityTier.LEGENDARY


static func _is_string_name_payload_value(value: Variant) -> bool:
	var value_type := typeof(value)
	return value_type == TYPE_STRING or value_type == TYPE_STRING_NAME
