## 文件说明：装备掉落服务，当前只承载稀有度主公式与正式掉落入口占位。
## 审查重点：drop_luck 不得在此处二次 clamp；3d6 稀有度阈值必须与装备实例枚举保持一致。
## 备注：正式掉落表内容与触发编排留给后续 story，本文件先固定最小可调用主路径。

class_name EquipmentDropService
extends RefCounted

const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")

var _rng: Variant = null


func _init(rng: Variant = null) -> void:
	if rng != null and rng.has_method("randi_range"):
		_rng = rng
		return
	var fallback_rng := RandomNumberGenerator.new()
	fallback_rng.randomize()
	_rng = fallback_rng


func roll_drops(drop_table_id: StringName, drop_luck: int) -> Array:
	_assert_drop_luck_in_range(drop_luck)
	var normalized_drop_table_id := ProgressionDataUtils.to_string_name(drop_table_id)
	if normalized_drop_table_id == &"":
		return []
	# 正式掉落表内容在后续 story 落地；当前先保留稳定入口。
	return []


func roll_drop_rarity(drop_luck: int) -> int:
	_assert_drop_luck_in_range(drop_luck)
	return _resolve_rarity_from_score(_roll_3d6() + drop_luck)


func roll_item_instances(item_id: StringName, quantity: int, drop_luck: int) -> Array:
	_assert_drop_luck_in_range(drop_luck)
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var resolved_quantity := maxi(int(quantity), 0)
	if normalized_item_id == &"" or resolved_quantity <= 0:
		return []
	var instances: Array = []
	for _index in range(resolved_quantity):
		var instance = EQUIPMENT_INSTANCE_STATE_SCRIPT.create(normalized_item_id)
		instance.rarity = roll_drop_rarity(drop_luck)
		instances.append(instance)
	return instances


func _roll_3d6() -> int:
	return int(_rng.randi_range(1, 6)) + int(_rng.randi_range(1, 6)) + int(_rng.randi_range(1, 6))


static func _resolve_rarity_from_score(rarity_score: int) -> int:
	if rarity_score >= 18:
		return EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.LEGENDARY
	if rarity_score >= 16:
		return EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.EPIC
	if rarity_score >= 13:
		return EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.RARE
	if rarity_score >= 10:
		return EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.UNCOMMON
	return EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.COMMON


static func _assert_drop_luck_in_range(drop_luck: int) -> void:
	assert(
		drop_luck >= UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MIN
			and drop_luck <= UNIT_BASE_ATTRIBUTES_SCRIPT.DROP_LUCK_MAX,
		"EquipmentDropService expects caller-clamped drop_luck in [-6, +5]."
	)
