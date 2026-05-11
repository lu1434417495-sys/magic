## 文件说明：该脚本属于配方内容注册表相关的注册表脚本，集中维护配方定义集合、校验错误列表等顶层字段。
## 审查重点：重点核对配方主键、输入输出列表长度、设施标签和物品引用校验是否保持稳定。
## 备注：当前注册表只负责扫描、校验和索引，不承担实际重铸/制作执行逻辑。

class_name RecipeContentRegistry
extends RefCounted

const RECIPE_CONFIG_DIRECTORY := "res://data/configs/recipes"
const RECIPE_DEF_SCRIPT = preload("res://scripts/player/warehouse/recipe_def.gd")

## 字段说明：缓存配方定义集合字典，集中保存可按键查询的运行时数据。
var _recipe_defs: Dictionary = {}
## 字段说明：收集配置校验阶段发现的错误信息，便于启动时统一报告和定位问题。
var _validation_errors: Array[String] = []
## 字段说明：缓存物品定义集合，供输入输出引用校验使用。
var _item_defs: Dictionary = {}


func _init(item_defs: Dictionary = {}) -> void:
	setup(item_defs)


func setup(item_defs: Dictionary = {}) -> void:
	_item_defs = item_defs if item_defs != null else {}
	rebuild()


func rebuild() -> void:
	_recipe_defs.clear()
	_validation_errors.clear()
	_scan_directory(RECIPE_CONFIG_DIRECTORY)


func get_recipe_defs() -> Dictionary:
	return _recipe_defs.duplicate()


func validate() -> Array[String]:
	return _validation_errors.duplicate()


func _scan_directory(directory_path: String) -> void:
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(directory_path)):
		_validation_errors.append("RecipeContentRegistry could not find %s." % directory_path)
		return

	var directory := DirAccess.open(directory_path)
	if directory == null:
		_validation_errors.append("RecipeContentRegistry could not open %s." % directory_path)
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
		_register_recipe_resource(entry_path)
	directory.list_dir_end()


func _register_recipe_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load recipe config %s." % resource_path)
		return
	if resource.get_script() != RECIPE_DEF_SCRIPT:
		_validation_errors.append("Recipe config %s is not a RecipeDef." % resource_path)
		return

	var recipe_def = resource
	if recipe_def == null:
		_validation_errors.append("Recipe config %s failed to cast to RecipeDef." % resource_path)
		return
	if recipe_def.recipe_id == &"":
		_validation_errors.append("Recipe config %s is missing recipe_id." % resource_path)
		return
	if _recipe_defs.has(recipe_def.recipe_id):
		_validation_errors.append("Duplicate recipe_id registered: %s" % String(recipe_def.recipe_id))
		return
	if recipe_def.input_item_ids.is_empty():
		_validation_errors.append("Recipe %s must declare at least one input item." % String(recipe_def.recipe_id))
		return
	if recipe_def.input_item_ids.size() != recipe_def.input_item_quantities.size():
		_validation_errors.append(
			"Recipe %s input_item_ids size must match input_item_quantities size." % String(recipe_def.recipe_id)
		)
		return
	if recipe_def.output_item_id == &"":
		_validation_errors.append("Recipe %s is missing output_item_id." % String(recipe_def.recipe_id))
		return
	if int(recipe_def.output_quantity) <= 0:
		_validation_errors.append("Recipe %s must have output_quantity >= 1." % String(recipe_def.recipe_id))
		return
	if recipe_def.required_facility_tags.is_empty():
		_validation_errors.append(
			"Recipe %s must declare at least one required_facility_tag." % String(recipe_def.recipe_id)
		)
		return

	var facility_tag_set: Dictionary = {}
	for raw_facility_tag in recipe_def.required_facility_tags:
		var facility_tag := ProgressionDataUtils.to_string_name(raw_facility_tag)
		if facility_tag == &"":
			_validation_errors.append(
				"Recipe %s declares an empty required_facility_tag." % String(recipe_def.recipe_id)
			)
			return
		if facility_tag_set.has(facility_tag):
			_validation_errors.append(
				"Recipe %s declares duplicate required_facility_tag %s." % [String(recipe_def.recipe_id), String(facility_tag)]
			)
			return
		facility_tag_set[facility_tag] = true

	for input_index in range(recipe_def.input_item_ids.size()):
		var input_item_id := ProgressionDataUtils.to_string_name(recipe_def.input_item_ids[input_index])
		var input_quantity := int(recipe_def.input_item_quantities[input_index])
		if input_item_id == &"":
			_validation_errors.append("Recipe %s declares an empty input_item_id." % String(recipe_def.recipe_id))
			return
		if input_quantity <= 0:
			_validation_errors.append(
				"Recipe %s input quantity for %s must be >= 1." % [String(recipe_def.recipe_id), String(input_item_id)]
			)
			return
		if not _item_defs.is_empty() and not _item_defs.has(input_item_id):
			_validation_errors.append(
				"Recipe %s references missing input item %s." % [String(recipe_def.recipe_id), String(input_item_id)]
			)
			return

	if not _item_defs.is_empty() and not _item_defs.has(recipe_def.output_item_id):
		_validation_errors.append(
			"Recipe %s references missing output item %s." % [String(recipe_def.recipe_id), String(recipe_def.output_item_id)]
		)
		return

	_recipe_defs[recipe_def.recipe_id] = recipe_def
