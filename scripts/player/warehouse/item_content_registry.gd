## 文件说明：该脚本属于物品内容注册表相关的注册表脚本，集中维护物品定义集合、校验错误列表等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name ItemContentRegistry
extends RefCounted

const ITEM_CONFIG_DIRECTORY := "res://data/configs/items"
## 模板资源目录；目录不存在时视为没有模板，跳过扫描而不是报错（项目早期或非装备类工程可能不需要模板）。
const ITEM_TEMPLATE_DIRECTORY := "res://data/configs/items_templates"
const ITEM_DEF_SCRIPT = preload("res://scripts/player/warehouse/item_def.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const ATTRIBUTE_MODIFIER_SCRIPT = preload("res://scripts/player/progression/attribute_modifier.gd")
const WEAPON_PROFILE_SCRIPT = preload("res://scripts/player/warehouse/weapon_profile_def.gd")

## 字段说明：缓存物品定义集合字典，集中保存可按键查询的运行时数据。
var _item_defs: Dictionary = {}
## 字段说明：缓存模板 ItemDef 集合，仅用于注册阶段的模板合并，不对外暴露。
var _template_defs: Dictionary = {}
## 字段说明：缓存模板的合并产物，避免多次走链；键是模板 item_id，值是合并后的 ItemDef。
var _resolved_template_cache: Dictionary = {}
## 字段说明：收集配置校验阶段发现的错误信息，便于启动时统一报告和定位问题。
var _validation_errors: Array[String] = []


func _init(auto_rebuild: bool = true) -> void:
	if auto_rebuild:
		rebuild()


func rebuild() -> void:
	rebuild_from_directories([ITEM_CONFIG_DIRECTORY], [ITEM_TEMPLATE_DIRECTORY])


func rebuild_from_directories(item_directories: Array, template_directories: Array = []) -> void:
	_item_defs.clear()
	_template_defs.clear()
	_resolved_template_cache.clear()
	_validation_errors.clear()
	for template_directory in template_directories:
		var template_path := String(template_directory)
		if template_path.is_empty():
			continue
		_scan_template_directory(template_path)
	_resolve_all_templates()
	for item_directory in item_directories:
		var item_path := String(item_directory)
		if item_path.is_empty():
			continue
		_scan_directory(item_path)


func get_item_defs() -> Dictionary:
	return _item_defs.duplicate()


func validate() -> Array[String]:
	return _validation_errors.duplicate()


func _scan_template_directory(directory_path: String) -> void:
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(directory_path)):
		return

	var directory := DirAccess.open(directory_path)
	if directory == null:
		_validation_errors.append("ItemContentRegistry could not open templates %s." % directory_path)
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
			_scan_template_directory(entry_path)
			continue
		if not entry_name.ends_with(".tres") and not entry_name.ends_with(".res"):
			continue
		_register_template_resource(entry_path)
	directory.list_dir_end()


func _register_template_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load item template %s." % resource_path)
		return
	if resource.get_script() != ITEM_DEF_SCRIPT:
		_validation_errors.append("Item template %s is not an ItemDef." % resource_path)
		return

	var template_def: ItemDef = resource
	if template_def.item_id == &"":
		_validation_errors.append("Item template %s is missing item_id." % resource_path)
		return
	if _template_defs.has(template_def.item_id):
		_validation_errors.append("Duplicate item template id: %s" % String(template_def.item_id))
		return
	_template_defs[template_def.item_id] = template_def


func _resolve_all_templates() -> void:
	for template_id in _template_defs.keys():
		if _resolved_template_cache.has(template_id):
			continue
		var template_def: ItemDef = _template_defs[template_id]
		var visited: Array[StringName] = []
		var resolved := resolve_with_template_chain(template_def, _template_defs, visited, _resolved_template_cache, _validation_errors)
		if resolved != null:
			_resolved_template_cache[template_id] = resolved


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

	var raw_def: ItemDef = resource
	if raw_def.item_id == &"":
		_validation_errors.append("Item config %s is missing item_id." % resource_path)
		return
	if _template_defs.has(raw_def.item_id):
		_validation_errors.append("Item config %s reuses template id %s; templates and instances must use distinct ids." % [resource_path, String(raw_def.item_id)])
		return

	var visited: Array[StringName] = []
	var item_def: ItemDef = resolve_with_template_chain(raw_def, _template_defs, visited, _resolved_template_cache, _validation_errors)
	if item_def == null:
		return

	var item_tags: Array[StringName] = item_def.get_tags()
	var item_crafting_groups: Array[StringName] = item_def.get_crafting_groups()
	if _item_defs.has(item_def.item_id):
		_validation_errors.append("Duplicate item_id registered: %s" % String(item_def.item_id))
		return
	if item_def.is_stackable and int(item_def.max_stack) <= 0:
		_validation_errors.append("Item %s must have max_stack >= 1." % String(item_def.item_id))
		return
	if int(item_def.base_price) > 0 and item_def.sellable and int(item_def.buy_price) <= 0:
		_validation_errors.append("Sellable item %s must declare explicit buy_price." % String(item_def.item_id))
		return
	if int(item_def.base_price) > 0 and item_def.sellable and int(item_def.sell_price) <= 0:
		_validation_errors.append("Sellable item %s must declare explicit sell_price." % String(item_def.item_id))
		return
	if item_tags.has(&"material") and item_crafting_groups.is_empty():
		_validation_errors.append(
			"Material item %s must declare at least one crafting_group." % String(item_def.item_id)
		)
		return
	if item_tags.has(&"quest_item") and item_def.get_quest_groups().is_empty():
		_validation_errors.append(
			"Quest item %s must declare at least one quest_group." % String(item_def.item_id)
		)
		return
	if item_def.get_item_category_normalized() == ITEM_DEF_SCRIPT.ITEM_CATEGORY_SKILL_BOOK and item_def.granted_skill_id == &"":
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
		if item_def.is_weapon():
			var weapon_profile = item_def.get("weapon_profile")
			if weapon_profile == null:
				_validation_errors.append("Weapon item %s must declare weapon_profile." % String(item_def.item_id))
				return
			if _get_weapon_profile(item_def) == null:
				_validation_errors.append("Weapon item %s must declare weapon_profile as WeaponProfileDef." % String(item_def.item_id))
				return
			if item_tags.has(&"melee") and item_def.get_weapon_attack_range() <= 0:
				_validation_errors.append(
					"Melee weapon item %s must declare weapon_profile.attack_range >= 1." % String(item_def.item_id)
				)
				return
			if item_tags.has(&"melee") and item_def.get_weapon_physical_damage_tag() == &"":
				_validation_errors.append(
					"Melee weapon item %s must declare one valid weapon_profile.damage_tag." % String(item_def.item_id)
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


## 模板继承解析。给定一个 ItemDef（可能是 instance、也可能是另一个模板），沿 base_item_id 向上合并并返回新 ItemDef。
## visited 沿继承链记录已访问 item_id，用于循环检测。
## cache 可以为空字典；若非空且命中模板 id，则跳过重新合并（避免共享父模板被多次解析）。
## errors 收集失败信息；返回 null 表示链路解析失败。
static func resolve_with_template_chain(
	item_def: ItemDef,
	template_defs: Dictionary,
	visited: Array,
	cache: Dictionary,
	errors: Array
) -> ItemDef:
	if item_def == null:
		errors.append("resolve_with_template_chain received null item_def.")
		return null

	var current_id := item_def.item_id
	if visited.has(current_id):
		var chain_text := str(visited) + " -> " + String(current_id)
		errors.append("Item template inheritance cycle detected at %s (chain: %s)." % [String(current_id), chain_text])
		return null

	if item_def.base_item_id == &"":
		return item_def

	var template_id: StringName = item_def.base_item_id
	if not template_defs.has(template_id):
		errors.append("Item %s references missing template %s." % [String(current_id), String(template_id)])
		return null

	var resolved_template: ItemDef = null
	if cache.has(template_id):
		resolved_template = cache[template_id]
	else:
		var next_visited := visited.duplicate()
		next_visited.append(current_id)
		var template_source: ItemDef = template_defs[template_id]
		resolved_template = resolve_with_template_chain(template_source, template_defs, next_visited, cache, errors)
		if resolved_template != null:
			cache[template_id] = resolved_template
	if resolved_template == null:
		return null

	return merge_with_template(resolved_template, item_def)


## 模板合并的纯函数实现。template 已经是合并后的产物（不再有 base_item_id）；instance 是原始资源。
## 字段处理三类：
##   1) 标量（字符串/StringName/数值/Resource）：instance 非空覆盖，空回退模板。0 与空串视为"未填"。
##   2) bool 与默认非零数值（is_stackable/sellable/max_stack）：无法区分"未填"和"显式填了默认值"，instance 始终覆盖。
##   3) 数组：tags / crafting_groups / quest_groups / attribute_modifiers 为可加成数组，模板与 instance 合并去重；
##      equipment_slot_ids / occupied_slot_ids 为结构性数组，instance 非空覆盖、空回退模板（不能合并以免出现重复槽位）。
## weapon_profile 是武器运行时真相源；合并只委托 WeaponProfileDef，不在 ItemDef 上保留裸字段规则。
## max_dex_bonus 使用 -1 表示未填，0 是有效上限，因此合并时以 >= 0 作为 instance 覆盖条件。
## attribute_modifiers 在合并时深拷贝并把 source_id 重写为最终 item_id，避免多个实例共享 source 导致结算覆盖。
static func merge_with_template(template: ItemDef, instance: ItemDef) -> ItemDef:
	var merged: ItemDef = ITEM_DEF_SCRIPT.new()

	merged.item_id = instance.item_id
	merged.base_item_id = &""

	merged.display_name = instance.display_name if instance.display_name != "" else template.display_name
	merged.description = instance.description if instance.description != "" else template.description
	merged.icon = instance.icon if instance.icon != "" else template.icon
	merged.equipment_type_id = instance.equipment_type_id if instance.equipment_type_id != &"" else template.equipment_type_id
	merged.set("weapon_profile", WEAPON_PROFILE_SCRIPT.merge(_get_weapon_profile(template), _get_weapon_profile(instance)))
	merged.granted_skill_id = instance.granted_skill_id if instance.granted_skill_id != &"" else template.granted_skill_id
	merged.item_category = instance.item_category if instance.item_category != &"" else template.item_category

	merged.base_price = int(instance.base_price) if int(instance.base_price) != 0 else int(template.base_price)
	merged.buy_price = int(instance.buy_price) if int(instance.buy_price) != 0 else int(template.buy_price)
	merged.sell_price = int(instance.sell_price) if int(instance.sell_price) != 0 else int(template.sell_price)
	merged.max_dex_bonus = int(instance.max_dex_bonus) if int(instance.max_dex_bonus) >= 0 else int(template.max_dex_bonus)

	# 默认非空字段：is_stackable(true) / sellable(true) / max_stack(99)。
	# 这些字段的默认值本身有业务含义，无法区分"未填"与"显式填默认"，所以始终使用 instance 值，模板不参与。
	merged.is_stackable = instance.is_stackable
	merged.max_stack = instance.max_stack
	merged.sellable = instance.sellable

	merged.equipment_slot_ids = _duplicate_string_array(instance.equipment_slot_ids) if not instance.equipment_slot_ids.is_empty() else _duplicate_string_array(template.equipment_slot_ids)
	merged.occupied_slot_ids = _duplicate_string_array(instance.occupied_slot_ids) if not instance.occupied_slot_ids.is_empty() else _duplicate_string_array(template.occupied_slot_ids)

	merged.equip_requirement = instance.equip_requirement if instance.equip_requirement != null else template.equip_requirement

	merged.tags = _merge_string_name_array(template.tags, instance.tags)
	merged.crafting_groups = _merge_string_name_array(template.crafting_groups, instance.crafting_groups)
	merged.quest_groups = _merge_string_name_array(template.quest_groups, instance.quest_groups)

	merged.attribute_modifiers = _merge_attribute_modifiers(template.attribute_modifiers, instance.attribute_modifiers, instance.item_id)

	return merged


static func _merge_string_name_array(template_values: Array, instance_values: Array) -> Array[StringName]:
	var seen: Dictionary = {}
	var merged: Array[StringName] = []
	for value in template_values:
		var normalized := ProgressionDataUtils.to_string_name(value)
		if normalized == &"" or seen.has(normalized):
			continue
		seen[normalized] = true
		merged.append(normalized)
	for value in instance_values:
		var normalized := ProgressionDataUtils.to_string_name(value)
		if normalized == &"" or seen.has(normalized):
			continue
		seen[normalized] = true
		merged.append(normalized)
	return merged


static func _duplicate_string_array(source_values: Array) -> Array[String]:
	var copied: Array[String] = []
	for value in source_values:
		copied.append(String(value))
	return copied


static func _get_weapon_profile(item_def: ItemDef):
	if item_def == null:
		return null
	var profile = item_def.get("weapon_profile")
	if profile == null:
		return null
	if profile is Object and profile.get_script() == WEAPON_PROFILE_SCRIPT:
		return profile
	return null


static func _merge_attribute_modifiers(template_mods: Array, instance_mods: Array, final_item_id: StringName) -> Array[AttributeModifier]:
	var merged: Array[AttributeModifier] = []
	for source_mod in template_mods:
		if source_mod == null:
			continue
		var copy: AttributeModifier = source_mod.duplicate(true)
		copy.source_id = final_item_id
		merged.append(copy)
	for source_mod in instance_mods:
		if source_mod == null:
			continue
		var copy: AttributeModifier = source_mod.duplicate(true)
		copy.source_id = final_item_id
		merged.append(copy)
	return merged
