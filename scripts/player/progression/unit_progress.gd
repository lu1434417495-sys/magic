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
	}


static func from_dict(data: Dictionary):
	var unlocked_resources_variant: Variant = data.get("unlocked_combat_resource_ids", null)
	if unlocked_resources_variant is not Array:
		return null

	var progress := UNIT_PROGRESS_SCRIPT.new()
	progress.version = int(data.get("version", 1))
	progress.unit_id = ProgressionDataUtils.to_string_name(data.get("unit_id", ""))
	progress.display_name = String(data.get("display_name", ""))
	progress.character_level = int(data.get("character_level", 0))
	progress.unit_base_attributes = UnitBaseAttributes.from_dict(data.get("unit_base_attributes", {}))
	progress.reputation_state = UNIT_REPUTATION_STATE_SCRIPT.from_dict(data.get("reputation_state", {}))
	progress.known_knowledge_ids = ProgressionDataUtils.to_string_name_array(data.get("known_knowledge_ids", []))
	progress.attribute_growth_progress = ProgressionDataUtils.to_string_name_int_map(data.get("attribute_growth_progress", {}))
	progress.blocked_relearn_skill_ids = ProgressionDataUtils.to_string_name_array(data.get("blocked_relearn_skill_ids", []))
	progress.merged_skill_source_map = ProgressionDataUtils.to_string_name_array_map(data.get("merged_skill_source_map", {}))
	progress.unlocked_combat_resource_ids = []
	for resource_id in ProgressionDataUtils.to_string_name_array(unlocked_resources_variant):
		progress.unlock_combat_resource(resource_id)
	progress.sync_default_combat_resource_unlocks()

	var skills_data: Variant = data.get("skills", {})
	if skills_data is Dictionary:
		for key in skills_data.keys():
			var skill_progress = UNIT_SKILL_PROGRESS_SCRIPT.from_dict(skills_data[key])
			if skill_progress.skill_id == &"":
				skill_progress.skill_id = ProgressionDataUtils.to_string_name(key)
			progress.skills[skill_progress.skill_id] = skill_progress
			if not skill_progress.merged_from_skill_ids.is_empty():
				progress.merged_skill_source_map[skill_progress.skill_id] = skill_progress.merged_from_skill_ids.duplicate()

	var professions_data: Variant = data.get("professions", {})
	if professions_data is Dictionary:
		for key in professions_data.keys():
			var profession_progress = UNIT_PROFESSION_PROGRESS_SCRIPT.from_dict(professions_data[key])
			if profession_progress.profession_id == &"":
				profession_progress.profession_id = ProgressionDataUtils.to_string_name(key)
			progress.professions[profession_progress.profession_id] = profession_progress

	var achievement_progress_data: Variant = data.get("achievement_progress", {})
	if achievement_progress_data is Dictionary:
		for key in achievement_progress_data.keys():
			var progress_state = ACHIEVEMENT_PROGRESS_STATE_SCRIPT.from_dict(achievement_progress_data[key])
			if progress_state.achievement_id == &"":
				progress_state.achievement_id = ProgressionDataUtils.to_string_name(key)
			progress.achievement_progress[progress_state.achievement_id] = progress_state

	var pending_choices_data: Variant = data.get("pending_profession_choices", [])
	if pending_choices_data is Array:
		for pending_choice_data in pending_choices_data:
			if pending_choice_data is Dictionary:
				progress.pending_profession_choices.append(PendingProfessionChoice.from_dict(pending_choice_data))

	progress.active_core_skill_ids = ProgressionDataUtils.to_string_name_array(data.get("active_core_skill_ids", []))
	progress.sync_active_core_skill_ids()
	return progress
