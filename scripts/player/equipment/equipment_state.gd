class_name EquipmentState
extends RefCounted

const EQUIPMENT_STATE_SCRIPT = preload("res://scripts/player/equipment/equipment_state.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")

## 字段说明：入口槽位到装备条目的映射。
## 值格式：{ "item_id": String, "occupied_slot_ids": Array[String], "instance_id": String }
## 入口槽位是放入装备时点击的槽位；occupied_slot_ids 是该装备实际锁定的所有槽位。
var equipped_slots: Dictionary = {}


## 读取任意槽位（入口槽或被占用槽）上的物品 ID；无装备时返回 &""。
func get_equipped_item_id(slot_id: StringName) -> StringName:
	var normalized := ProgressionDataUtils.to_string_name(slot_id)
	if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(normalized):
		return &""
	var entry_slot := get_entry_slot_for_slot(normalized)
	if entry_slot == &"":
		return &""
	var entry: Variant = equipped_slots.get(entry_slot)
	if entry is not Dictionary:
		return &""
	return ProgressionDataUtils.to_string_name(entry.get("item_id", ""))


## 读取任意槽位上装备的实例 ID；无装备或无实例 ID 时返回 &""。
func get_equipped_instance_id(slot_id: StringName) -> StringName:
	var normalized := ProgressionDataUtils.to_string_name(slot_id)
	var entry_slot := get_entry_slot_for_slot(normalized)
	if entry_slot == &"":
		return &""
	var entry: Variant = equipped_slots.get(entry_slot)
	if entry is not Dictionary:
		return &""
	return ProgressionDataUtils.to_string_name(entry.get("instance_id", ""))


## 写入一条装备条目（Phase 2 主写入接口）。
## entry_slot_id：入口槽；occupied_slot_ids：该装备实际占用的所有槽（含入口槽）。
## instance_id：装备实例 ID，空字符串表示无实例（旧存档兼容）。
## 传入空 item_id 等同于清除该入口槽。
func set_equipped_entry(entry_slot_id: StringName, item_id: StringName, occupied: Array[StringName], instance_id: StringName = &"") -> bool:
	var norm_entry := ProgressionDataUtils.to_string_name(entry_slot_id)
	var norm_item := ProgressionDataUtils.to_string_name(item_id)
	if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(norm_entry):
		return false
	if norm_item == &"":
		equipped_slots.erase(norm_entry)
		return true

	var occ_validated: Array[StringName] = []
	for s in occupied:
		var ns := ProgressionDataUtils.to_string_name(s)
		if EQUIPMENT_RULES_SCRIPT.is_valid_slot(ns) and not occ_validated.has(ns):
			occ_validated.append(ns)
	if occ_validated.is_empty():
		occ_validated.append(norm_entry)

	equipped_slots[norm_entry] = {
		"item_id": String(norm_item),
		"occupied_slot_ids": ProgressionDataUtils.string_name_array_to_string_array(occ_validated),
		"instance_id": String(instance_id),
	}
	return true


## 兼容接口：单槽装备，occupied_slot_ids 默认为 [slot_id]，instance_id 默认空。
func set_equipped_item(slot_id: StringName, item_id: StringName) -> bool:
	var norm_slot := ProgressionDataUtils.to_string_name(slot_id)
	if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(norm_slot):
		return false
	if ProgressionDataUtils.to_string_name(item_id) == &"":
		equipped_slots.erase(norm_slot)
		return true
	var occ: Array[StringName] = [norm_slot]
	return set_equipped_entry(norm_slot, ProgressionDataUtils.to_string_name(item_id), occ)


## 清除包含 slot_id 的入口条目（无论 slot_id 是入口槽还是被占用槽）。
func clear_slot(slot_id: StringName) -> void:
	var entry_slot := get_entry_slot_for_slot(ProgressionDataUtils.to_string_name(slot_id))
	if entry_slot != &"":
		equipped_slots.erase(entry_slot)


## 弹出入口槽的装备条目并以 EquipmentInstanceState 返回；同时从 equipped_slots 中移除该条目。
## 若无条目或 item_id 为空则返回 null。若存档无 instance_id，自动生成新 ID。
func pop_equipped_instance(entry_slot_id: StringName):
	var norm_entry := ProgressionDataUtils.to_string_name(entry_slot_id)
	var entry: Variant = equipped_slots.get(norm_entry)
	if entry is not Dictionary:
		return null
	var item_id := ProgressionDataUtils.to_string_name(entry.get("item_id", ""))
	if item_id == &"":
		equipped_slots.erase(norm_entry)
		return null
	var instance_id := ProgressionDataUtils.to_string_name(entry.get("instance_id", ""))
	equipped_slots.erase(norm_entry)

	var inst = EQUIPMENT_INSTANCE_STATE_SCRIPT.new()
	inst.instance_id = instance_id if instance_id != &"" else EQUIPMENT_INSTANCE_STATE_SCRIPT.generate_id()
	inst.item_id = item_id
	return inst


## 返回 slot_id 所属入口槽；若该槽未被任何条目占用则返回 &""。
func get_entry_slot_for_slot(slot_id: StringName) -> StringName:
	var normalized := ProgressionDataUtils.to_string_name(slot_id)
	# 直接作为入口槽存在
	if equipped_slots.has(normalized):
		var entry: Variant = equipped_slots[normalized]
		if entry is Dictionary and entry.get("item_id", "") != "":
			return normalized
	# 作为被占用槽存在于某个入口条目中
	for entry_slot in equipped_slots.keys():
		var entry: Variant = equipped_slots[entry_slot]
		if entry is not Dictionary:
			continue
		var occ: Variant = entry.get("occupied_slot_ids", [])
		if occ is not Array:
			continue
		for raw_occ in occ:
			if ProgressionDataUtils.to_string_name(raw_occ) == normalized:
				return ProgressionDataUtils.to_string_name(entry_slot)
	return &""


## 返回所有入口槽 ID（按 EquipmentRules 槽位顺序）。用于属性结算，避免双手武器重复计算。
func get_entry_slot_ids() -> Array[StringName]:
	var result: Array[StringName] = []
	for slot_id in EQUIPMENT_RULES_SCRIPT.get_all_slot_ids():
		if not equipped_slots.has(slot_id):
			continue
		var entry: Variant = equipped_slots[slot_id]
		if entry is Dictionary and entry.get("item_id", "") != "":
			result.append(slot_id)
	return result


## 返回所有被占用的槽位 ID（包含非入口被占用槽）。用于"此槽是否被锁定"判断。
func get_filled_slot_ids() -> Array[StringName]:
	var result: Array[StringName] = []
	for entry_slot in get_entry_slot_ids():
		var entry: Variant = equipped_slots.get(entry_slot)
		if entry is not Dictionary:
			continue
		var occ: Variant = entry.get("occupied_slot_ids", [])
		if occ is not Array:
			continue
		for raw_occ in occ:
			var ns := ProgressionDataUtils.to_string_name(raw_occ)
			if not result.has(ns):
				result.append(ns)
	return result


## 返回当前装备的条目数（双手武器算 1 件）。
func get_equipped_count() -> int:
	return get_entry_slot_ids().size()


func duplicate_state() -> EquipmentState:
	return EQUIPMENT_STATE_SCRIPT.from_dict(to_dict())


func to_dict() -> Dictionary:
	var slot_data: Dictionary = {}
	for entry_slot in get_entry_slot_ids():
		var entry: Variant = equipped_slots.get(entry_slot)
		if entry is Dictionary:
			slot_data[String(entry_slot)] = entry.duplicate()
	return {
		"equipped_slots": slot_data,
	}


static func from_dict(data: Variant) -> EquipmentState:
	var state := EQUIPMENT_STATE_SCRIPT.new()
	if data is not Dictionary:
		return state

	# 兼容旧格式：顶层 dict 直接是槽位映射（无 "equipped_slots" 包装）
	var slot_data: Variant = data.get("equipped_slots", data)
	if slot_data is not Dictionary:
		return state

	for key in slot_data.keys():
		var slot_id := ProgressionDataUtils.to_string_name(key)
		if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(slot_id):
			continue

		var raw_value: Variant = slot_data.get(key, "")
		var item_id := &""
		var occupied: Array[StringName] = []
		var instance_id := &""

		if raw_value is Dictionary:
			item_id = ProgressionDataUtils.to_string_name(raw_value.get("item_id", ""))
			var raw_occ: Variant = raw_value.get("occupied_slot_ids", [])
			if raw_occ is Array:
				for raw_s in raw_occ:
					var ns := ProgressionDataUtils.to_string_name(raw_s)
					if EQUIPMENT_RULES_SCRIPT.is_valid_slot(ns):
						occupied.append(ns)
			instance_id = ProgressionDataUtils.to_string_name(raw_value.get("instance_id", ""))
		else:
			item_id = ProgressionDataUtils.to_string_name(raw_value)

		if item_id == &"":
			continue
		if occupied.is_empty():
			occupied.append(slot_id)

		state.set_equipped_entry(slot_id, item_id, occupied, instance_id)

	return state
