## 文件说明：该脚本属于信仰服务相关的服务脚本，集中维护神祇配置扫描、供奉升阶校验与奖励排队。
## 审查重点：重点核对配置装载边界、升阶条件判定顺序，以及 pending reward 写入是否保持单步稳定。
## 备注：Fortuna rank 1~5 现在通过 fortune_marked + 四个 guidance achievement 串联；fortune_marked 由 FortuneService 写入，guidance 由 FortunaGuidanceService 写入。

class_name FaithService
extends RefCounted

const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const FAITH_DEITY_DEF_TYPE = preload("res://scripts/player/progression/faith_deity_def.gd")
const FAITH_RANK_DEF_TYPE = preload("res://scripts/player/progression/faith_rank_def.gd")
const FAITH_DEITY_DEF_SCRIPT = preload("res://scripts/player/progression/faith_deity_def.gd")
const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/pending_character_reward.gd")
const PENDING_CHARACTER_REWARD_ENTRY_SCRIPT = preload("res://scripts/systems/pending_character_reward_entry.gd")
const PartyState = PARTY_STATE_SCRIPT
const PartyMemberState = PARTY_MEMBER_STATE_SCRIPT
const UnitBaseAttributes = UNIT_BASE_ATTRIBUTES_SCRIPT
const FaithDeityDef = FAITH_DEITY_DEF_TYPE
const FaithRankDef = FAITH_RANK_DEF_TYPE
const PendingCharacterReward = PENDING_CHARACTER_REWARD_SCRIPT
const PendingCharacterRewardEntry = PENDING_CHARACTER_REWARD_ENTRY_SCRIPT

const CONFIG_DIRECTORY := "res://data/configs/faith"
const SOURCE_TYPE_FAITH_RANK_REWARD: StringName = &"faith_rank_reward"
const FAITH_LUCK_BONUS_STAT_ID: StringName = &"faith_luck_bonus"

var _faith_deity_defs: Dictionary = {}
var _validation_errors: Array[String] = []


func _init(faith_deity_defs: Dictionary = {}) -> void:
	if faith_deity_defs.is_empty():
		rebuild()
		return
	setup(faith_deity_defs)


func setup(faith_deity_defs: Dictionary = {}) -> void:
	_faith_deity_defs.clear()
	_validation_errors.clear()

	for key in faith_deity_defs.keys():
		var deity_def = faith_deity_defs[key]
		if deity_def is FaithDeityDef:
			var resolved_deity: FaithDeityDef = deity_def as FaithDeityDef
			if resolved_deity.deity_id == &"":
				continue
			_faith_deity_defs[resolved_deity.deity_id] = resolved_deity

	_validation_errors.append_array(_collect_validation_errors())


func rebuild() -> void:
	_faith_deity_defs.clear()
	_validation_errors.clear()
	_scan_directory(CONFIG_DIRECTORY)
	_validation_errors.append_array(_collect_validation_errors())


func get_faith_deity_defs() -> Dictionary:
	return _faith_deity_defs


func get_faith_deity_def(deity_id: StringName) -> FaithDeityDef:
	return _faith_deity_defs.get(deity_id) as FaithDeityDef


func validate() -> Array[String]:
	return _validation_errors.duplicate()


func execute_devotion(party_state: PartyState, member_id: StringName, deity_id: StringName) -> Dictionary:
	var result := {
		"ok": false,
		"error_code": "",
		"member_id": String(member_id),
		"deity_id": String(deity_id),
		"current_rank": 0,
		"target_rank": 0,
		"gold_spent": 0,
		"pending_reward": {},
		"missing_custom_stat_id": "",
		"missing_achievement_id": "",
	}
	if party_state == null or member_id == &"" or deity_id == &"":
		result["error_code"] = "invalid_request"
		return result

	var member_state := party_state.get_member_state(member_id) as PartyMemberState
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		result["error_code"] = "member_not_found"
		return result

	var deity_def := get_faith_deity_def(deity_id)
	if deity_def == null:
		result["error_code"] = "deity_not_found"
		return result

	var current_rank := get_current_rank(party_state, member_id, deity_id, deity_def)
	result["current_rank"] = current_rank
	if current_rank >= deity_def.get_max_rank():
		result["error_code"] = "max_rank_reached"
		return result

	var next_rank := deity_def.get_rank_def(current_rank + 1)
	if next_rank == null:
		result["error_code"] = "missing_rank_def"
		return result
	result["target_rank"] = next_rank.rank_index

	if not party_state.can_afford(next_rank.required_gold):
		result["error_code"] = "insufficient_gold"
		return result
	if int(member_state.progression.character_level) < next_rank.required_level:
		result["error_code"] = "level_too_low"
		return result
	if not _meets_placeholder_requirements(member_state, next_rank, result):
		return result

	if not party_state.spend_gold(next_rank.required_gold):
		result["error_code"] = "insufficient_gold"
		return result

	_ensure_writable_reward_attribute_seeds(member_state, next_rank)
	var reward := _build_rank_reward(member_state, deity_def, next_rank)
	if reward == null or reward.is_empty():
		party_state.add_gold(next_rank.required_gold)
		result["error_code"] = "invalid_rank_reward"
		return result

	party_state.enqueue_pending_character_reward(reward)
	result["ok"] = true
	result["gold_spent"] = next_rank.required_gold
	result["pending_reward"] = reward.to_dict()
	return result


func get_current_rank(
	party_state: PartyState,
	member_id: StringName,
	deity_id: StringName,
	deity_def: FaithDeityDef = null
) -> int:
	if deity_def == null:
		deity_def = get_faith_deity_def(deity_id)
	if deity_def == null:
		return 0
	if party_state == null:
		return 0

	var member_state := party_state.get_member_state(member_id) as PartyMemberState
	if member_state == null:
		return 0

	var rank_progress_stat_id := _resolve_rank_progress_stat_id(deity_def)
	var applied_rank := maxi(_get_custom_stat_value(member_state, rank_progress_stat_id), 0)
	var pending_rank := _count_pending_rank_rewards(party_state, member_id, deity_id, rank_progress_stat_id)
	return clampi(applied_rank + pending_rank, 0, deity_def.get_max_rank())


func _scan_directory(directory_path: String) -> void:
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(directory_path)):
		_validation_errors.append("FaithService could not find %s." % directory_path)
		return

	var directory := DirAccess.open(directory_path)
	if directory == null:
		_validation_errors.append("FaithService could not open %s." % directory_path)
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
		_register_deity_resource(entry_path)
	directory.list_dir_end()


func _register_deity_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load faith config %s." % resource_path)
		return
	if resource.get_script() != FAITH_DEITY_DEF_SCRIPT:
		_validation_errors.append("Faith config %s is not a FaithDeityDef." % resource_path)
		return

	var deity_def := resource as FaithDeityDef
	if deity_def == null:
		_validation_errors.append("Faith config %s failed to cast to FaithDeityDef." % resource_path)
		return
	if deity_def.deity_id == &"":
		_validation_errors.append("Faith config %s is missing deity_id." % resource_path)
		return
	if _faith_deity_defs.has(deity_def.deity_id):
		_validation_errors.append("Duplicate faith deity_id registered: %s" % String(deity_def.deity_id))
		return

	_faith_deity_defs[deity_def.deity_id] = deity_def


func _collect_validation_errors() -> Array[String]:
	var errors: Array[String] = []
	for deity_key in ProgressionDataUtils.sorted_string_keys(_faith_deity_defs):
		var deity_id := StringName(deity_key)
		var deity_def := _faith_deity_defs.get(deity_id) as FaithDeityDef
		if deity_def == null:
			continue
		errors.append_array(deity_def.validate())
	return errors


func _meets_placeholder_requirements(
	member_state: PartyMemberState,
	rank_def: FaithRankDef,
	result: Dictionary
) -> bool:
	if rank_def.has_custom_stat_requirement():
		var current_value := _get_custom_stat_value(member_state, rank_def.required_custom_stat_id)
		if current_value < rank_def.required_custom_stat_min_value:
			result["error_code"] = "custom_stat_requirement_unmet"
			result["missing_custom_stat_id"] = String(rank_def.required_custom_stat_id)
			return false
	if rank_def.has_achievement_requirement():
		if not _is_achievement_unlocked(member_state, rank_def.required_achievement_id):
			result["error_code"] = "achievement_requirement_unmet"
			result["missing_achievement_id"] = String(rank_def.required_achievement_id)
			return false
	return true


func _get_custom_stat_value(member_state: PartyMemberState, stat_id: StringName) -> int:
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return 0
	return member_state.progression.unit_base_attributes.get_attribute_value(stat_id)


func _is_achievement_unlocked(member_state: PartyMemberState, achievement_id: StringName) -> bool:
	if achievement_id == &"" or member_state == null or member_state.progression == null:
		return false
	var progress_state = member_state.progression.get_achievement_progress_state(achievement_id)
	return progress_state != null and bool(progress_state.is_unlocked)


func _count_pending_rank_rewards(
	party_state: PartyState,
	member_id: StringName,
	deity_id: StringName,
	rank_progress_stat_id: StringName
) -> int:
	if party_state == null or member_id == &"" or deity_id == &"" or rank_progress_stat_id == &"":
		return 0
	var pending_bonus := 0
	for reward in party_state.pending_character_rewards:
		if reward == null:
			continue
		if reward.member_id != member_id:
			continue
		if reward.source_type != SOURCE_TYPE_FAITH_RANK_REWARD or reward.source_id != deity_id:
			continue
		for entry in reward.entries:
			if entry == null:
				continue
			if entry.entry_type == &"attribute_delta" and entry.target_id == rank_progress_stat_id:
				pending_bonus += int(entry.amount)
	return pending_bonus


func _ensure_writable_reward_attribute_seeds(member_state: PartyMemberState, rank_def: FaithRankDef) -> void:
	if member_state == null or rank_def == null:
		return
	for reward_entry_variant in rank_def.reward_entries:
		if reward_entry_variant is not Dictionary:
			continue
		var reward_data := reward_entry_variant as Dictionary
		if ProgressionDataUtils.to_string_name(reward_data.get("entry_type", "")) != &"attribute_delta":
			continue
		var attribute_id := ProgressionDataUtils.to_string_name(reward_data.get("target_id", ""))
		_ensure_writable_custom_stat_seed(member_state, attribute_id)


func _ensure_writable_custom_stat_seed(member_state: PartyMemberState, stat_id: StringName) -> void:
	if stat_id == &"" or UnitBaseAttributes.BASE_ATTRIBUTE_IDS.has(stat_id):
		return
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return
	var custom_stats: Dictionary = member_state.progression.unit_base_attributes.custom_stats
	if custom_stats.has(stat_id):
		return
	custom_stats[stat_id] = member_state.progression.unit_base_attributes.get_attribute_value(stat_id)


func _resolve_rank_progress_stat_id(deity_def: FaithDeityDef) -> StringName:
	if deity_def == null or deity_def.rank_progress_stat_id == &"":
		return FAITH_LUCK_BONUS_STAT_ID
	return deity_def.rank_progress_stat_id


func _build_rank_reward(
	member_state: PartyMemberState,
	deity_def: FaithDeityDef,
	rank_def: FaithRankDef
) -> PendingCharacterReward:
	if member_state == null or deity_def == null or rank_def == null:
		return null

	var reward := PENDING_CHARACTER_REWARD_SCRIPT.new()
	reward.reward_id = _build_reward_id(member_state.member_id, deity_def.deity_id, rank_def.rank_index)
	reward.member_id = member_state.member_id
	reward.member_name = member_state.display_name if not member_state.display_name.is_empty() else String(member_state.member_id)
	reward.source_type = SOURCE_TYPE_FAITH_RANK_REWARD
	reward.source_id = deity_def.deity_id
	reward.source_label = deity_def.display_name if not deity_def.display_name.is_empty() else String(deity_def.deity_id)
	reward.summary_text = "%s 晋升为 %s" % [reward.source_label, rank_def.rank_name]

	var normalized_entries: Array[PendingCharacterRewardEntry] = []
	for reward_entry_variant in rank_def.reward_entries:
		var reward_entry = PENDING_CHARACTER_REWARD_ENTRY_SCRIPT.from_variant(reward_entry_variant)
		if reward_entry == null or reward_entry.is_empty():
			continue
		if reward_entry.reason_text.is_empty():
			reward_entry.reason_text = reward.summary_text
		if reward_entry.target_label.is_empty():
			reward_entry.target_label = String(reward_entry.target_id)
		normalized_entries.append(reward_entry)

	reward.entries = normalized_entries
	return reward if not reward.is_empty() else null


func _build_reward_id(member_id: StringName, deity_id: StringName, rank_index: int) -> StringName:
	return ProgressionDataUtils.to_string_name(
		"%s_%s_rank_%d_%d" % [
			String(member_id),
			String(deity_id),
			rank_index,
			Time.get_ticks_usec(),
		]
	)
