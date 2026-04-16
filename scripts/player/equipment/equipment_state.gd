class_name EquipmentState
extends RefCounted

const EQUIPMENT_STATE_SCRIPT = preload("res://scripts/player/equipment/equipment_state.gd")
const EQUIPMENT_ENTRY_STATE_SCRIPT = preload("res://scripts/player/equipment/equipment_entry_state.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")

## 字段说明：入口槽位到装备条目的映射。
## 运行时内部使用 EquipmentEntryState；序列化时只接受当前 equipped_slots 包装格式。
var equipped_slots: Dictionary = {}
var _slot_to_entry_slot: Dictionary = {}


## 读取任意槽位（入口槽或被占用槽）上的物品 ID；无装备时返回 &""。
func get_equipped_item_id(slot_id: StringName) -> StringName:
	var entry: Variant = get_entry_for_slot(slot_id)
	return entry.item_id if entry != null else &""


## 读取任意槽位上装备的实例 ID；无装备或无实例 ID 时返回 &""。
func get_equipped_instance_id(slot_id: StringName) -> StringName:
	var entry: Variant = get_entry_for_slot(slot_id)
	return entry.instance_id if entry != null else &""


func get_entry(entry_slot_id: StringName):
	var normalized := ProgressionDataUtils.to_string_name(entry_slot_id)
	if not equipped_slots.has(normalized):
		return null
	return _normalize_entry_variant(equipped_slots.get(normalized), normalized)


func get_entry_for_slot(slot_id: StringName):
	var entry_slot := get_entry_slot_for_slot(slot_id)
	if entry_slot == &"":
		return null
	return get_entry(entry_slot)


func get_occupied_slot_ids_for_entry(entry_slot_id: StringName) -> Array[StringName]:
	var entry = get_entry(entry_slot_id)
	return entry.occupied_slot_ids.duplicate() if entry != null else []


## 写入一条装备条目（Phase 2 主写入接口）。
## entry_slot_id：入口槽；occupied_slot_ids：该装备实际占用的所有槽（含入口槽）。
## instance_id：装备实例 ID，空字符串表示无实例。
## 传入空 item_id 等同于清除该入口槽。
func set_equipped_entry(entry_slot_id: StringName, item_id: StringName, occupied: Array[StringName], instance_id: StringName = &"") -> bool:
	var norm_entry := ProgressionDataUtils.to_string_name(entry_slot_id)
	var norm_item := ProgressionDataUtils.to_string_name(item_id)
	if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(norm_entry):
		return false
	if norm_item == &"":
		clear_entry_slot(norm_entry)
		return true

	var entry = EQUIPMENT_ENTRY_STATE_SCRIPT.new()
	entry.item_id = norm_item
	entry.instance_id = ProgressionDataUtils.to_string_name(instance_id)
	entry.occupied_slot_ids = _normalize_occupied_slot_ids(norm_entry, occupied)
	_store_entry(norm_entry, entry)
	return true


## 清除包含 slot_id 的入口条目（无论 slot_id 是入口槽还是被占用槽）。
func clear_slot(slot_id: StringName) -> void:
	var entry_slot := get_entry_slot_for_slot(ProgressionDataUtils.to_string_name(slot_id))
	if entry_slot != &"":
		clear_entry_slot(entry_slot)


func clear_entry_slot(entry_slot_id: StringName) -> void:
	var normalized := ProgressionDataUtils.to_string_name(entry_slot_id)
	var entry = get_entry(normalized)
	if entry != null:
		for occupied_slot_id in entry.occupied_slot_ids:
			if _slot_to_entry_slot.get(occupied_slot_id, &"") == normalized:
				_slot_to_entry_slot.erase(occupied_slot_id)
	equipped_slots.erase(normalized)


## 弹出入口槽的装备条目并以 EquipmentInstanceState 返回；同时从 equipped_slots 中移除该条目。
## 若无条目或 item_id 为空则返回 null。若存档无 instance_id，自动生成新 ID。
func pop_equipped_instance(entry_slot_id: StringName):
	var norm_entry := ProgressionDataUtils.to_string_name(entry_slot_id)
	var entry = get_entry(norm_entry)
	if entry == null:
		return null
	if entry.item_id == &"":
		clear_entry_slot(norm_entry)
		return null
	clear_entry_slot(norm_entry)

	var inst = EQUIPMENT_INSTANCE_STATE_SCRIPT.new()
	inst.instance_id = entry.instance_id if entry.instance_id != &"" else EQUIPMENT_INSTANCE_STATE_SCRIPT.generate_id()
	inst.item_id = entry.item_id
	return inst


## 返回 slot_id 所属入口槽；若该槽未被任何条目占用则返回 &""。
func get_entry_slot_for_slot(slot_id: StringName) -> StringName:
	var normalized := ProgressionDataUtils.to_string_name(slot_id)
	if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(normalized):
		return &""
	return ProgressionDataUtils.to_string_name(_slot_to_entry_slot.get(normalized, &""))


## 返回所有入口槽 ID（按 EquipmentRules 槽位顺序）。用于属性结算，避免双手武器重复计算。
func get_entry_slot_ids() -> Array[StringName]:
	var result: Array[StringName] = []
	for slot_id in EQUIPMENT_RULES_SCRIPT.get_all_slot_ids():
		var entry = get_entry(slot_id)
		if entry != null and not entry.is_empty():
			result.append(slot_id)
	return result


## 返回所有被占用的槽位 ID（包含非入口被占用槽）。用于"此槽是否被锁定"判断。
func get_filled_slot_ids() -> Array[StringName]:
	var result: Array[StringName] = []
	for slot_id in EQUIPMENT_RULES_SCRIPT.get_all_slot_ids():
		if get_entry_slot_for_slot(slot_id) != &"":
			result.append(slot_id)
	return result


## 返回当前装备的条目数（双手武器算 1 件）。
func get_equipped_count() -> int:
	return get_entry_slot_ids().size()


func duplicate_state() -> EquipmentState:
	return EQUIPMENT_STATE_SCRIPT.from_dict(to_dict())


func to_dict() -> Dictionary:
	var slot_data: Dictionary = {}
	for entry_slot in get_entry_slot_ids():
		var entry = get_entry(entry_slot)
		if entry != null:
			slot_data[String(entry_slot)] = entry.to_dict()
	return {
		"equipped_slots": slot_data,
	}


static func from_dict(data: Variant) -> EquipmentState:
	if data is not Dictionary:
		return null
	var slot_data: Variant = data.get("equipped_slots", null)
	if slot_data is not Dictionary:
		return null

	var state := EQUIPMENT_STATE_SCRIPT.new()

	for key in slot_data.keys():
		var slot_id := ProgressionDataUtils.to_string_name(key)
		if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(slot_id):
			continue

		var entry = EQUIPMENT_ENTRY_STATE_SCRIPT.from_dict(slot_data.get(key), slot_id)
		if entry == null or entry.is_empty():
			continue
		state._store_entry(slot_id, entry)

	return state


func _normalize_entry_variant(entry_variant: Variant, entry_slot_id: StringName):
	if entry_variant is Object and entry_variant.has_method("to_dict") and entry_variant.has_method("is_empty"):
		return entry_variant
	var entry = EQUIPMENT_ENTRY_STATE_SCRIPT.from_dict(entry_variant, entry_slot_id)
	if entry == null or entry.is_empty():
		equipped_slots.erase(entry_slot_id)
		_rebuild_slot_lookup()
		return null
	equipped_slots[entry_slot_id] = entry
	_register_entry_slots(entry_slot_id, entry)
	return entry


func _normalize_occupied_slot_ids(entry_slot_id: StringName, occupied: Array[StringName]) -> Array[StringName]:
	var validated: Array[StringName] = []
	for raw_slot_id in occupied:
		var slot_id := ProgressionDataUtils.to_string_name(raw_slot_id)
		if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(slot_id):
			continue
		if validated.has(slot_id):
			continue
		validated.append(slot_id)
	if validated.is_empty():
		validated.append(entry_slot_id)
	elif not validated.has(entry_slot_id):
		validated.insert(0, entry_slot_id)
	return validated


func _store_entry(entry_slot_id: StringName, entry) -> void:
	var normalized_entry_slot := ProgressionDataUtils.to_string_name(entry_slot_id)
	clear_entry_slot(normalized_entry_slot)
	var normalized_entry = _normalize_entry_variant(entry, normalized_entry_slot)
	if normalized_entry == null:
		return
	for occupied_slot_id in normalized_entry.occupied_slot_ids:
		var existing_entry_slot := ProgressionDataUtils.to_string_name(_slot_to_entry_slot.get(occupied_slot_id, &""))
		if existing_entry_slot != &"" and existing_entry_slot != normalized_entry_slot:
			clear_entry_slot(existing_entry_slot)
	equipped_slots[normalized_entry_slot] = normalized_entry
	_register_entry_slots(normalized_entry_slot, normalized_entry)


func _register_entry_slots(entry_slot_id: StringName, entry) -> void:
	if entry == null:
		return
	_slot_to_entry_slot[entry_slot_id] = entry_slot_id
	for occupied_slot_id in entry.occupied_slot_ids:
		_slot_to_entry_slot[occupied_slot_id] = entry_slot_id


func _rebuild_slot_lookup() -> void:
	_slot_to_entry_slot.clear()
	for entry_slot_variant in equipped_slots.keys():
		var entry_slot_id := ProgressionDataUtils.to_string_name(entry_slot_variant)
		var entry = _normalize_entry_variant(equipped_slots.get(entry_slot_variant), entry_slot_id)
		if entry == null:
			continue
		_register_entry_slots(entry_slot_id, entry)
