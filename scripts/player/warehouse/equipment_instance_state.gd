## 文件说明：装备实例状态，表达一件已实例化装备的唯一身份与可变属性。
## 审查重点：instance_id 生成必须在本文件内完成；其余字段均为可序列化的值类型。
## 备注：current_durability 为 -1 表示当前无耐久数据（Phase 2 阶段占位）。

class_name EquipmentInstanceState
extends RefCounted

const SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")

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
## 字段说明：装备品质层级；旧存档缺字段时回退为 COMMON。
var rarity: int = RarityTier.COMMON
## 字段说明：当前剩余耐久；-1 表示耐久功能尚未启用。
var current_durability: int = -1
## 字段说明：护甲磨损累计进度；0.0 为未磨损（阈值由规则层决定）。
var armor_wear_progress: float = 0.0
## 字段说明：武器磨耗累计进度；0.0 为未磨耗（阈值由规则层决定）。
var weapon_wear_progress: float = 0.0


## 创建新实例并分配唯一 ID。
static func create(p_item_id: StringName) -> EquipmentInstanceState:
	var inst := SCRIPT.new()
	inst.instance_id = generate_id()
	inst.item_id = ProgressionDataUtils.to_string_name(p_item_id)
	return inst


## 生成唯一实例 ID；基于微秒时间戳与随机数，适合单机游戏环境。
static func generate_id() -> StringName:
	return StringName("eq_%d_%d" % [Time.get_ticks_usec(), randi()])


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
	var inst := SCRIPT.new()
	if data is not Dictionary:
		return inst
	inst.instance_id = ProgressionDataUtils.to_string_name(data.get("instance_id", ""))
	inst.item_id = ProgressionDataUtils.to_string_name(data.get("item_id", ""))
	inst.rarity = normalize_rarity(data.get("rarity", RarityTier.COMMON))
	inst.current_durability = int(data.get("current_durability", -1))
	inst.armor_wear_progress = float(data.get("armor_wear_progress", 0.0))
	inst.weapon_wear_progress = float(data.get("weapon_wear_progress", 0.0))
	return inst


static func normalize_rarity(value: Variant) -> int:
	var normalized := int(value)
	if normalized < RarityTier.COMMON or normalized > RarityTier.LEGENDARY:
		return RarityTier.COMMON
	return normalized
