## 文件说明：该脚本属于信仰阶位定义相关的资源脚本，集中维护阶位编号、金币与等级门槛、占位入门条件和奖励条目。
## 审查重点：重点核对 rank 索引顺序、占位条件字段以及奖励条目 shape 是否保持稳定。
## 备注：本阶段承载 Fortuna / Misfortune 的 rank gate；fortune_marked 由 FortuneService 写入，Fortuna guidance achievement 由 FortunaGuidanceService 写入。

class_name FaithRankDef
extends Resource


@export var rank_index := 1
@export var rank_name: String = ""
@export var required_gold := 0
@export var required_level := 0
@export var required_custom_stat_id: StringName = &""
@export var required_custom_stat_min_value := 0
@export var required_achievement_id: StringName = &""
@export var reward_entries: Array[Dictionary] = []


func has_custom_stat_requirement() -> bool:
	return required_custom_stat_id != &"" and required_custom_stat_min_value > 0


func has_achievement_requirement() -> bool:
	return required_achievement_id != &""


func validate() -> Array[String]:
	var errors: Array[String] = []
	if rank_index <= 0:
		errors.append("Faith rank must have rank_index >= 1.")
	if rank_name.is_empty():
		errors.append("Faith rank %d is missing rank_name." % rank_index)
	if required_gold < 0:
		errors.append("Faith rank %d uses negative required_gold %d." % [rank_index, required_gold])
	if required_level < 0:
		errors.append("Faith rank %d uses negative required_level %d." % [rank_index, required_level])
	if has_custom_stat_requirement() and has_achievement_requirement():
		errors.append("Faith rank %d should not mix custom stat and achievement placeholder gates." % rank_index)
	if required_custom_stat_id == &"" and required_custom_stat_min_value != 0:
		errors.append("Faith rank %d sets required_custom_stat_min_value without required_custom_stat_id." % rank_index)
	if reward_entries.is_empty():
		errors.append("Faith rank %d must define at least one reward entry." % rank_index)
	for reward_entry in reward_entries:
		if reward_entry is not Dictionary:
			errors.append("Faith rank %d contains a non-dictionary reward entry." % rank_index)
			continue
		var reward_data := reward_entry as Dictionary
		var entry_type := ProgressionDataUtils.to_string_name(reward_data.get("entry_type", ""))
		var target_id := ProgressionDataUtils.to_string_name(reward_data.get("target_id", ""))
		var amount := int(reward_data.get("amount", 0))
		if entry_type == &"" or target_id == &"" or amount == 0:
			errors.append("Faith rank %d contains an invalid reward entry." % rank_index)
	return errors
