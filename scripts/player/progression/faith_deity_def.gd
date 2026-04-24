## 文件说明：该脚本属于信仰神祇定义相关的资源脚本，集中维护 deity 唯一标识、展示信息与 rank 配置集合。
## 审查重点：重点核对 deity_id、rank 配置是否连续且不重复，以及后续服务读取入口是否稳定。
## 备注：当前只承载静态配置，不处理运行时供奉或 UI 行为。

class_name FaithDeityDef
extends Resource

const FaithRankDef = preload("res://scripts/player/progression/faith_rank_def.gd")

@export var deity_id: StringName = &""
@export var display_name: String = ""
@export var facility_id: StringName = &""
@export var service_type_label: String = ""
@export var power_domain_tags: Array[StringName] = []
@export var rank_progress_stat_id: StringName = &""
@export var rank_defs: Array[FaithRankDef] = []


func get_rank_def(rank_index: int) -> FaithRankDef:
	for rank_def in rank_defs:
		if rank_def != null and rank_def.rank_index == rank_index:
			return rank_def
	return null


func get_max_rank() -> int:
	var max_rank := 0
	for rank_def in rank_defs:
		if rank_def == null:
			continue
		max_rank = maxi(max_rank, rank_def.rank_index)
	return max_rank


func validate() -> Array[String]:
	var errors: Array[String] = []
	if deity_id == &"":
		errors.append("Faith deity config is missing deity_id.")
	if display_name.is_empty():
		errors.append("Faith deity %s is missing display_name." % String(deity_id))
	if rank_progress_stat_id == &"":
		errors.append("Faith deity %s is missing rank_progress_stat_id." % String(deity_id))

	var seen_ranks: Dictionary = {}
	for rank_def in rank_defs:
		if rank_def == null:
			errors.append("Faith deity %s contains a null rank_def." % String(deity_id))
			continue
		if seen_ranks.has(rank_def.rank_index):
			errors.append(
				"Faith deity %s declares duplicate rank %d." % [
					String(deity_id),
					rank_def.rank_index,
				]
			)
			continue
		seen_ranks[rank_def.rank_index] = true
		for rank_error in rank_def.validate():
			errors.append("Faith deity %s: %s" % [String(deity_id), rank_error])
		if rank_progress_stat_id != &"" and not _has_rank_progress_reward(rank_def):
			errors.append(
				"Faith deity %s rank %d is missing rank progress reward %s." % [
					String(deity_id),
					rank_def.rank_index,
					String(rank_progress_stat_id),
				]
			)

	var expected_rank := 1
	var max_rank := get_max_rank()
	while expected_rank <= max_rank:
		if not seen_ranks.has(expected_rank):
			errors.append(
				"Faith deity %s is missing rank %d." % [
					String(deity_id),
					expected_rank,
				]
			)
		expected_rank += 1

	return errors


func _has_rank_progress_reward(rank_def: FaithRankDef) -> bool:
	if rank_def == null or rank_progress_stat_id == &"":
		return false
	for reward_entry in rank_def.reward_entries:
		if reward_entry is not Dictionary:
			continue
		var reward_data := reward_entry as Dictionary
		if ProgressionDataUtils.to_string_name(reward_data.get("entry_type", "")) != &"attribute_delta":
			continue
		if ProgressionDataUtils.to_string_name(reward_data.get("target_id", "")) == rank_progress_stat_id:
			return true
	return false
