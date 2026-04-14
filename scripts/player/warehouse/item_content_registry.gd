## 文件说明：该脚本属于物品内容注册表相关的注册表脚本，集中维护物品定义集合、校验错误列表等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name ItemContentRegistry
extends RefCounted

const ITEM_CONFIG_DIRECTORY := "res://data/configs/items"
const ITEM_DEF_SCRIPT = preload("res://scripts/player/warehouse/item_def.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")

## 字段说明：缓存物品定义集合字典，集中保存可按键查询的运行时数据。
var _item_defs: Dictionary = {}
## 字段说明：收集配置校验阶段发现的错误信息，便于启动时统一报告和定位问题。
var _validation_errors: Array[String] = []


func _init() -> void:
	rebuild()


func rebuild() -> void:
	_item_defs.clear()
	_validation_errors.clear()
	_scan_directory(ITEM_CONFIG_DIRECTORY)


func get_item_defs() -> Dictionary:
	return _item_defs


func validate() -> Array[String]:
	return _validation_errors.duplicate()


func _scan_directory(directory_path: String) -> void:
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(directory_path)):
		_validation_errors.append("ItemContentRegistry could not find %s." % directory_path)
		return

	var directory := DirAccess.open(directory_path)
	if directory == null:
		_validation_errors.append("ItemContentRegistry could not open %s." % directory_path)
		return

	directory.list_dir_begin()
	while true:
		var entry_name := directory.get_next()
		if entry_name.is_empty():
			break
		if entry_name == "." or entry_name == "..":
			continue

		var entry_path := "%s/%s" % [directory_path, entry_name]
		if directory.current_is_dir():
			_scan_directory(entry_path)
			continue
		if not entry_name.ends_with(".tres") and not entry_name.ends_with(".res"):
			continue
		_register_item_resource(entry_path)
	directory.list_dir_end()


func _register_item_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load item config %s." % resource_path)
		return
	if resource.get_script() != ITEM_DEF_SCRIPT:
		_validation_errors.append("Item config %s is not an ItemDef." % resource_path)
		return

	var item_def = resource
	if item_def.item_id == &"":
		_validation_errors.append("Item config %s is missing item_id." % resource_path)
		return
	if _item_defs.has(item_def.item_id):
		_validation_errors.append("Duplicate item_id registered: %s" % String(item_def.item_id))
		return
	if item_def.is_stackable and int(item_def.max_stack) <= 0:
		_validation_errors.append("Item %s must have max_stack >= 1." % String(item_def.item_id))
		return
	if item_def.item_category == ITEM_DEF_SCRIPT.ITEM_CATEGORY_SKILL_BOOK and item_def.granted_skill_id == &"":
		_validation_errors.append("Skill book item %s must declare granted_skill_id." % String(item_def.item_id))
		return
	if item_def.has_equipment_category():
		if item_def.is_stackable or item_def.get_effective_max_stack() != 1:
			_validation_errors.append("Equipment item %s must be non-stackable." % String(item_def.item_id))
			return
		if item_def.equipment_slot_ids.is_empty():
			_validation_errors.append("Equipment item %s must declare at least one slot." % String(item_def.item_id))
			return
		for raw_slot_id in item_def.equipment_slot_ids:
			if EQUIPMENT_RULES_SCRIPT.is_valid_slot(ProgressionDataUtils.to_string_name(raw_slot_id)):
				continue
			_validation_errors.append(
				"Equipment item %s declares invalid slot %s." % [String(item_def.item_id), String(raw_slot_id)]
			)
			return
		if not item_def.has_valid_equipment_type():
			_validation_errors.append(
				"Equipment item %s must declare equipment_type_id as weapon, armor, or accessory." % String(item_def.item_id)
			)
			return

		if not item_def.occupied_slot_ids.is_empty():
			if item_def.equipment_slot_ids.size() != 1:
				_validation_errors.append(
					"Equipment item %s declares occupied_slot_ids but equipment_slot_ids must be exactly 1 entry slot." % String(item_def.item_id)
				)
				return
			for raw_slot_id in item_def.occupied_slot_ids:
				if EQUIPMENT_RULES_SCRIPT.is_valid_slot(ProgressionDataUtils.to_string_name(raw_slot_id)):
					continue
				_validation_errors.append(
					"Equipment item %s declares invalid occupied_slot %s." % [String(item_def.item_id), String(raw_slot_id)]
				)
				return

	_item_defs[item_def.item_id] = item_def
