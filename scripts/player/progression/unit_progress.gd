## 文件说明：该脚本属于单位进度相关的业务脚本，集中维护版本、单位唯一标识、显示名称等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name UnitProgress
extends RefCounted

const UNIT_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_progress.gd")
const UNIT_REPUTATION_STATE_SCRIPT = preload("res://scripts/player/progression/unit_reputation_state.gd")
const UNIT_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_skill_progress.gd")
const UNIT_PROFESSION_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_profession_progress.gd")
const ACHIEVEMENT_PROGRESS_STATE_SCRIPT = preload("res://scripts/player/progression/achievement_progress_state.gd")

const COMBAT_RESOURCE_HP: StringName = &"hp"
const COMBAT_RESOURCE_STAMINA: StringName = &"stamina"
const COMBAT_RESOURCE_MP: StringName = &"mp"
const COMBAT_RESOURCE_AURA: StringName = &"aura"
const TO_DICT_FIELDS: Array[String] = [
	"version",
	"unit_id",
	"display_name",
	"character_level",
	"unit_base_attributes",
	"reputation_state",
	"skills",
	"professions",
	"known_knowledge_ids",
	"active_core_skill_ids",
	"attribute_growth_progress",
	"achievement_progress",
	"pending_profession_choices",
	"blocked_relearn_skill_ids",
	"merged_skill_source_map",
	"unlocked_combat_resource_ids",
	"active_level_trigger_core_skill_id",
	"locked_level_trigger_skill_ids",
]
const DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS: Array[StringName] = [
	COMBAT_RESOURCE_HP,
	COMBAT_RESOURCE_STAMINA,
]
const VALID_COMBAT_RESOURCE_IDS: Array[StringName] = [
	COMBAT_RESOURCE_HP,
	COMBAT_RESOURCE_STAMINA,
	COMBAT_RESOURCE_MP,
	COMBAT_RESOURCE_AURA,
]

## 字段说明：记录版本，会参与成长规则判定、序列化和界面展示。
var version := 1
## 字段说明：记录单位唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var unit_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
var display_name: String = ""
## 字段说明：记录角色等级，会参与成长规则判定、序列化和界面展示。
var character_level := 0
## 字段说明：保存单位基础属性集合，便于顺序遍历、批量展示、批量运算和整体重建。
var unit_base_attributes: UnitBaseAttributes = UnitBaseAttributes.new()
## 字段说明：记录声望状态，会参与成长规则判定、序列化和界面展示。
var reputation_state = UNIT_REPUTATION_STATE_SCRIPT.new()
## 字段说明：缓存技能集合字典，集中保存可按键查询的运行时数据。
var skills: Dictionary = {}
## 字段说明：缓存职业集合字典，集中保存可按键查询的运行时数据。
var professions: Dictionary = {}
## 字段说明：保存已知知识标识列表，便于批量遍历、交叉查找和界面展示。
var known_knowledge_ids: Array[StringName] = []
## 字段说明：保存激活核心技能标识列表，便于批量遍历、交叉查找和界面展示。
var active_core_skill_ids: Array[StringName] = []
## 字段说明：按基础属性缓存满级技能提供的成长进度，达到阈值后由规则服务转化为基础属性点。
var attribute_growth_progress: Dictionary = {}
## 字段说明：缓存成就进度集合字典，集中保存可按键查询的运行时数据。
var achievement_progress: Dictionary = {}
## 字段说明：保存待处理职业候选项，便于顺序遍历、批量展示、批量运算和整体重建。
var pending_profession_choices: Array[PendingProfessionChoice] = []
## 字段说明：保存被阻止重学的技能标识列表，便于批量遍历、交叉查找和界面展示。
var blocked_relearn_skill_ids: Array[StringName] = []
## 字段说明：按键缓存已合并技能来源映射表，便于在较多对象中快速定位目标并减少重复遍历。
var merged_skill_source_map: Dictionary = {}
## 字段说明：记录角色已正式解锁并可在战斗 HUD 展示的战斗资源。
var unlocked_combat_resource_ids: Array[StringName] = DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS.duplicate()
## 字段说明：记录当前唯一具有升级触发资格的核心技能，空字符串表示尚无激活。
var active_level_trigger_core_skill_id: StringName = &""
## 字段说明：记录已触发过等级提升并被锁定的核心技能列表，锁定后不再具有升级触发资格。
var locked_level_trigger_skill_ids: Array[StringName] = []


func set_skill_progress(skill_progress) -> void:
	if skill_progress == null:
		return

	skills[skill_progress.skill_id] = skill_progress
	if not skill_progress.merged_from_skill_ids.is_empty():
		remember_merge_sources(skill_progress.skill_id, skill_progress.merged_from_skill_ids)
	sync_active_core_skill_ids()


func get_skill_progress(skill_id: StringName):
	return skills.get(skill_id)


func remove_skill_progress(skill_id: StringName) -> void:
	skills.erase(skill_id)
	sync_active_core_skill_ids()


func set_profession_progress(profession_progress) -> void:
	if profession_progress == null:
		return
	professions[profession_progress.profession_id] = profession_progress


func get_profession_progress(profession_id: StringName):
	return professions.get(profession_id)


func set_achievement_progress_state(progress_state) -> void:
	if progress_state == null or progress_state.achievement_id == &"":
		return
	achievement_progress[progress_state.achievement_id] = progress_state


func get_achievement_progress_state(achievement_id: StringName):
	return achievement_progress.get(achievement_id)


func has_knowledge(knowledge_id: StringName) -> bool:
	return knowledge_id != &"" and known_knowledge_ids.has(knowledge_id)


func learn_knowledge(knowledge_id: StringName) -> bool:
	if knowledge_id == &"":
		return false
	if has_knowledge(knowledge_id):
		return false
	known_knowledge_ids.append(knowledge_id)
	return true


func sync_active_core_skill_ids() -> void:
	var next_core_skill_ids: Array[StringName] = []
	for key in ProgressionDataUtils.sorted_string_keys(skills):
		var skill_id = StringName(key)
		var skill_progress = get_skill_progress(skill_id)
		if skill_progress == null:
			continue
		if skill_progress.is_learned and skill_progress.is_core:
			next_core_skill_ids.append(skill_id)
	active_core_skill_ids = next_core_skill_ids


func is_skill_relearn_blocked(skill_id: StringName) -> bool:
	return blocked_relearn_skill_ids.has(skill_id)


func block_skill_relearn(skill_id: StringName) -> void:
	if blocked_relearn_skill_ids.has(skill_id):
		return
	blocked_relearn_skill_ids.append(skill_id)


func remember_merge_sources(skill_id: StringName, source_skill_ids: Array[StringName]) -> void:
	var deduped_sources: Array[StringName] = []
	var seen_sources: Dictionary = {}
	for source_skill_id in source_skill_ids:
		if source_skill_id == skill_id:
			continue
		if seen_sources.has(source_skill_id):
			continue
		seen_sources[source_skill_id] = true
		deduped_sources.append(source_skill_id)

	merged_skill_source_map[skill_id] = deduped_sources.duplicate()
	var skill_progress = get_skill_progress(skill_id)
	if skill_progress != null:
		skill_progress.merged_from_skill_ids = deduped_sources.duplicate()


func get_merged_source_skill_ids(skill_id: StringName) -> Array[StringName]:
	if merged_skill_source_map.has(skill_id):
		return ProgressionDataUtils.to_string_name_array(merged_skill_source_map.get(skill_id, []))

	var skill_progress = get_skill_progress(skill_id)
	if skill_progress != null and not skill_progress.merged_from_skill_ids.is_empty():
		return skill_progress.merged_from_skill_ids.duplicate()

	return []


func get_merged_source_skill_ids_recursive(skill_id: StringName) -> Array[StringName]:
	var ordered_results: Array[StringName] = []
	var visited: Dictionary = {}
	for source_skill_id in get_merged_source_skill_ids(skill_id):
		_append_recursive_merge_source(source_skill_id, ordered_results, visited)
	return ordered_results


func sync_default_combat_resource_unlocks() -> void:
	for resource_id in DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS:
		unlock_combat_resource(resource_id)


func has_combat_resource_unlocked(resource_id: StringName) -> bool:
	return unlocked_combat_resource_ids.has(resource_id)


func unlock_combat_resource(resource_id: StringName) -> bool:
	if resource_id == &"":
		return false
	if not VALID_COMBAT_RESOURCE_IDS.has(resource_id):
		return false
	if unlocked_combat_resource_ids.has(resource_id):
		return false
	unlocked_combat_resource_ids.append(resource_id)
	return true


func _append_recursive_merge_source(
	source_skill_id: StringName,
	ordered_results: Array[StringName],
	visited: Dictionary
) -> void:
	if visited.has(source_skill_id):
		return

	for nested_source_id in get_merged_source_skill_ids(source_skill_id):
		_append_recursive_merge_source(nested_source_id, ordered_results, visited)

	if visited.has(source_skill_id):
		return

	visited[source_skill_id] = true
	ordered_results.append(source_skill_id)


func to_dict() -> Dictionary:
	sync_active_core_skill_ids()
	sync_default_combat_resource_unlocks()

	var skills_data: Dictionary = {}
	for key in ProgressionDataUtils.sorted_string_keys(skills):
		var skill_id = StringName(key)
		var skill_progress = get_skill_progress(skill_id)
		if skill_progress != null:
			skills_data[key] = skill_progress.to_dict()

	var professions_data: Dictionary = {}
	for key in ProgressionDataUtils.sorted_string_keys(professions):
		var profession_id = StringName(key)
		var profession_progress = get_profession_progress(profession_id)
		if profession_progress != null:
			professions_data[key] = profession_progress.to_dict()

	var pending_choices_data: Array[Dictionary] = []
	for pending_choice in pending_profession_choices:
		if pending_choice != null:
			pending_choices_data.append(pending_choice.to_dict())

	var achievement_progress_data: Dictionary = {}
	for key in ProgressionDataUtils.sorted_string_keys(achievement_progress):
		var achievement_id = StringName(key)
		var progress_state = get_achievement_progress_state(achievement_id)
		if progress_state != null:
			achievement_progress_data[key] = progress_state.to_dict()

	return {
		"version": version,
		"unit_id": String(unit_id),
		"display_name": display_name,
		"character_level": character_level,
		"unit_base_attributes": unit_base_attributes.to_dict() if unit_base_attributes != null else {},
		"reputation_state": reputation_state.to_dict() if reputation_state != null else {},
		"skills": skills_data,
		"professions": professions_data,
		"known_knowledge_ids": ProgressionDataUtils.string_name_array_to_string_array(known_knowledge_ids),
		"active_core_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(active_core_skill_ids),
		"attribute_growth_progress": ProgressionDataUtils.string_name_int_map_to_string_dict(attribute_growth_progress),
		"achievement_progress": achievement_progress_data,
		"pending_profession_choices": pending_choices_data,
		"blocked_relearn_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(blocked_relearn_skill_ids),
		"merged_skill_source_map": ProgressionDataUtils.string_name_array_map_to_string_dict(merged_skill_source_map),
		"unlocked_combat_resource_ids": ProgressionDataUtils.string_name_array_to_string_array(unlocked_combat_resource_ids),
		"active_level_trigger_core_skill_id": String(active_level_trigger_core_skill_id),
		"locked_level_trigger_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(locked_level_trigger_skill_ids),
	}


static func from_dict(data: Dictionary):
	if not _has_exact_fields(data, TO_DICT_FIELDS):
		return null
	var unit_base_attributes_data: Variant = data.get("unit_base_attributes", null)
	var reputation_state_data: Variant = data.get("reputation_state", null)
	var skills_data: Variant = data.get("skills", null)
	var professions_data: Variant = data.get("professions", null)
	var known_knowledge_ids_variant: Variant = data.get("known_knowledge_ids", null)
	var active_core_skill_ids_variant: Variant = data.get("active_core_skill_ids", null)
	var attribute_growth_progress_variant: Variant = data.get("attribute_growth_progress", null)
	var achievement_progress_data: Variant = data.get("achievement_progress", null)
	var pending_choices_data: Variant = data.get("pending_profession_choices", null)
	var blocked_relearn_skill_ids_variant: Variant = data.get("blocked_relearn_skill_ids", null)
	var merged_skill_source_map_variant: Variant = data.get("merged_skill_source_map", null)
	var unlocked_resources_variant: Variant = data.get("unlocked_combat_resource_ids", null)
	var active_level_trigger_core_skill_id_variant: Variant = data.get("active_level_trigger_core_skill_id", null)
	var locked_level_trigger_skill_ids_variant: Variant = data.get("locked_level_trigger_skill_ids", null)
	if unit_base_attributes_data is not Dictionary:
		return null
	if reputation_state_data is not Dictionary:
		return null
	if skills_data is not Dictionary:
		return null
	if professions_data is not Dictionary:
		return null
	if known_knowledge_ids_variant is not Array:
		return null
	if active_core_skill_ids_variant is not Array:
		return null
	if attribute_growth_progress_variant is not Dictionary:
		return null
	if achievement_progress_data is not Dictionary:
		return null
	if pending_choices_data is not Array:
		return null
	if blocked_relearn_skill_ids_variant is not Array:
		return null
	if merged_skill_source_map_variant is not Dictionary:
		return null
	if unlocked_resources_variant is not Array:
		return null
	if locked_level_trigger_skill_ids_variant is not Array:
		return null
	var version_variant: Variant = data.get("version", null)
	if version_variant is not int or int(version_variant) != 1:
		return null
	var parsed_unit_id = _parse_required_string_name(data.get("unit_id", null))
	if parsed_unit_id == null:
		return null
	var display_name_variant: Variant = data.get("display_name", null)
	if display_name_variant is not String:
		return null
	var parsed_display_name := String(display_name_variant)
	if parsed_display_name.strip_edges().is_empty():
		return null
	var character_level_variant: Variant = data.get("character_level", null)
	if character_level_variant is not int or int(character_level_variant) < 0:
		return null
	var parsed_known_knowledge_ids = _parse_unique_string_name_array(known_knowledge_ids_variant)
	if parsed_known_knowledge_ids == null:
		return null
	var parsed_active_core_skill_ids = _parse_unique_string_name_array(active_core_skill_ids_variant)
	if parsed_active_core_skill_ids == null:
		return null
	var parsed_attribute_growth_progress = _parse_nonnegative_int_map(attribute_growth_progress_variant)
	if parsed_attribute_growth_progress == null:
		return null
	var parsed_blocked_relearn_skill_ids = _parse_unique_string_name_array(blocked_relearn_skill_ids_variant)
	if parsed_blocked_relearn_skill_ids == null:
		return null
	var parsed_merged_skill_source_map = _parse_string_name_array_map(merged_skill_source_map_variant)
	if parsed_merged_skill_source_map == null:
		return null
	var parsed_unlocked_resources = _parse_unique_string_name_array(unlocked_resources_variant)
	if parsed_unlocked_resources == null:
		return null
	var parsed_active_level_trigger_core_skill_id = _parse_optional_string_name(active_level_trigger_core_skill_id_variant)
	if parsed_active_level_trigger_core_skill_id == null:
		return null
	var parsed_locked_level_trigger_skill_ids = _parse_unique_string_name_array(locked_level_trigger_skill_ids_variant)
	if parsed_locked_level_trigger_skill_ids == null:
		return null
	for resource_id in parsed_unlocked_resources:
		if not VALID_COMBAT_RESOURCE_IDS.has(resource_id):
			return null
	for default_resource_id in DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS:
		if not parsed_unlocked_resources.has(default_resource_id):
			return null

	var progress := UNIT_PROGRESS_SCRIPT.new()
	progress.version = int(version_variant)
	progress.unit_id = parsed_unit_id
	progress.display_name = parsed_display_name
	progress.character_level = int(character_level_variant)
	progress.unit_base_attributes = UnitBaseAttributes.from_dict(unit_base_attributes_data)
	progress.reputation_state = UNIT_REPUTATION_STATE_SCRIPT.from_dict(reputation_state_data)
	if progress.unit_base_attributes == null or progress.reputation_state == null:
		return null
	progress.known_knowledge_ids = parsed_known_knowledge_ids
	progress.attribute_growth_progress = parsed_attribute_growth_progress
	progress.blocked_relearn_skill_ids = parsed_blocked_relearn_skill_ids
	progress.merged_skill_source_map = parsed_merged_skill_source_map
	progress.unlocked_combat_resource_ids = parsed_unlocked_resources
	progress.active_level_trigger_core_skill_id = parsed_active_level_trigger_core_skill_id
	progress.locked_level_trigger_skill_ids = parsed_locked_level_trigger_skill_ids
	progress.sync_default_combat_resource_unlocks()

	for key in skills_data.keys():
		var skill_id := ProgressionDataUtils.to_string_name(key)
		if skill_id == &"" or progress.skills.has(skill_id):
			return null
		var skill_progress_payload: Variant = skills_data[key]
		if skill_progress_payload is not Dictionary:
			return null
		var skill_progress = UNIT_SKILL_PROGRESS_SCRIPT.from_dict(skill_progress_payload)
		if skill_progress == null or skill_progress.skill_id == &"" or skill_progress.skill_id != skill_id:
			return null
		progress.skills[skill_progress.skill_id] = skill_progress
		if not skill_progress.merged_from_skill_ids.is_empty():
			progress.merged_skill_source_map[skill_progress.skill_id] = skill_progress.merged_from_skill_ids.duplicate()

	if not _has_valid_level_trigger_state(progress):
		return null

	for key in professions_data.keys():
		var profession_id := ProgressionDataUtils.to_string_name(key)
		if profession_id == &"" or progress.professions.has(profession_id):
			return null
		var profession_progress_payload: Variant = professions_data[key]
		if profession_progress_payload is not Dictionary:
			return null
		var profession_progress = UNIT_PROFESSION_PROGRESS_SCRIPT.from_dict(profession_progress_payload)
		if profession_progress == null or profession_progress.profession_id == &"" or profession_progress.profession_id != profession_id:
			return null
		progress.professions[profession_progress.profession_id] = profession_progress

	for key in achievement_progress_data.keys():
		var achievement_id := ProgressionDataUtils.to_string_name(key)
		if achievement_id == &"" or progress.achievement_progress.has(achievement_id):
			return null
		var achievement_progress_payload: Variant = achievement_progress_data[key]
		if achievement_progress_payload is not Dictionary:
			return null
		var progress_state = ACHIEVEMENT_PROGRESS_STATE_SCRIPT.from_dict(achievement_progress_payload)
		if progress_state == null or progress_state.achievement_id == &"" or progress_state.achievement_id != achievement_id:
			return null
		progress.achievement_progress[progress_state.achievement_id] = progress_state

	for pending_choice_data in pending_choices_data:
		if pending_choice_data is not Dictionary:
			return null
		var pending_choice = PendingProfessionChoice.from_dict(pending_choice_data)
		if pending_choice == null:
			return null
		progress.pending_profession_choices.append(pending_choice)

	progress.active_core_skill_ids = parsed_active_core_skill_ids
	progress.sync_active_core_skill_ids()
	return progress


static func _parse_required_string_name(value: Variant):
	var value_type := typeof(value)
	if value_type != TYPE_STRING and value_type != TYPE_STRING_NAME:
		return null
	var parsed_value := ProgressionDataUtils.to_string_name(value)
	if parsed_value == &"":
		return null
	return parsed_value


static func _has_exact_fields(data: Dictionary, expected_fields: Array[String]) -> bool:
	if data.size() != expected_fields.size():
		return false
	for field_name in expected_fields:
		if not data.has(field_name):
			return false
	return true


static func _parse_optional_string_name(value: Variant):
	var value_type := typeof(value)
	if value_type != TYPE_STRING and value_type != TYPE_STRING_NAME:
		return null
	return ProgressionDataUtils.to_string_name(value)


static func _parse_unique_string_name_array(values: Array):
	var parsed_values: Array[StringName] = []
	var seen_values: Dictionary = {}
	for raw_value in values:
		var parsed_value = _parse_required_string_name(raw_value)
		if parsed_value == null or seen_values.has(parsed_value):
			return null
		seen_values[parsed_value] = true
		parsed_values.append(parsed_value)
	return parsed_values


static func _parse_nonnegative_int_map(values: Dictionary):
	var parsed_values: Dictionary = {}
	var seen_keys: Dictionary = {}
	for raw_key in values.keys():
		var parsed_key = _parse_required_string_name(raw_key)
		if parsed_key == null or seen_keys.has(parsed_key):
			return null
		var raw_value: Variant = values[raw_key]
		if raw_value is not int or int(raw_value) < 0:
			return null
		seen_keys[parsed_key] = true
		parsed_values[parsed_key] = int(raw_value)
	return parsed_values


static func _parse_string_name_array_map(values: Dictionary):
	var parsed_values: Dictionary = {}
	var seen_keys: Dictionary = {}
	for raw_key in values.keys():
		var parsed_key = _parse_required_string_name(raw_key)
		if parsed_key == null or seen_keys.has(parsed_key):
			return null
		var raw_values: Variant = values[raw_key]
		if raw_values is not Array:
			return null
		var parsed_array = _parse_unique_string_name_array(raw_values)
		if parsed_array == null:
			return null
		seen_keys[parsed_key] = true
		parsed_values[parsed_key] = parsed_array
	return parsed_values


static func _has_valid_level_trigger_state(progress) -> bool:
	if progress == null:
		return false

	var active_skill_id: StringName = progress.active_level_trigger_core_skill_id
	var active_flag_count := 0
	var active_flag_skill_id: StringName = &""
	var locked_flag_lookup: Dictionary = {}

	for raw_skill_id in progress.skills.keys():
		var skill_id := ProgressionDataUtils.to_string_name(raw_skill_id)
		var skill_progress = progress.get_skill_progress(skill_id)
		if skill_progress == null:
			return false

		if bool(skill_progress.is_level_trigger_active):
			active_flag_count += 1
			active_flag_skill_id = skill_id
			if bool(skill_progress.is_level_trigger_locked):
				return false

		if bool(skill_progress.is_level_trigger_locked):
			locked_flag_lookup[skill_id] = true
			if bool(skill_progress.is_level_trigger_active):
				return false
			if not bool(skill_progress.is_learned) or not bool(skill_progress.is_core):
				return false

	if active_flag_count > 1:
		return false

	if active_skill_id == &"":
		if active_flag_count != 0:
			return false
	else:
		var active_skill_progress = progress.get_skill_progress(active_skill_id)
		if active_skill_progress == null:
			return false
		if active_flag_count != 1 or active_flag_skill_id != active_skill_id:
			return false
		if not bool(active_skill_progress.is_learned) or not bool(active_skill_progress.is_core):
			return false
		if bool(active_skill_progress.is_level_trigger_locked):
			return false
		if progress.locked_level_trigger_skill_ids.has(active_skill_id):
			return false

	var locked_list_lookup: Dictionary = {}
	for locked_skill_id in progress.locked_level_trigger_skill_ids:
		if locked_skill_id == &"" or locked_list_lookup.has(locked_skill_id):
			return false
		var locked_skill_progress = progress.get_skill_progress(locked_skill_id)
		if locked_skill_progress == null:
			return false
		if not bool(locked_skill_progress.is_learned) or not bool(locked_skill_progress.is_core):
			return false
		if bool(locked_skill_progress.is_level_trigger_active):
			return false
		if not bool(locked_skill_progress.is_level_trigger_locked):
			return false
		locked_list_lookup[locked_skill_id] = true

	if locked_list_lookup.size() != locked_flag_lookup.size():
		return false
	for locked_skill_id in locked_flag_lookup.keys():
		if not locked_list_lookup.has(locked_skill_id):
			return false

	return true
