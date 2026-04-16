class_name BattleRatingSystem
extends RefCounted

const BATTLE_RATING_SOURCE_TYPE: StringName = &"battle_rating"

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null


func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null


func initialize_battle_rating_stats() -> void:
	if not _has_runtime():
		return
	_runtime._battle_rating_stats.clear()
	_runtime._pending_post_battle_character_rewards.clear()
	if _runtime._state == null:
		return

	for ally_unit_id in _runtime._state.ally_unit_ids:
		var unit_state := _runtime._state.units.get(ally_unit_id) as BattleUnitState
		if unit_state == null:
			continue
		if unit_state.control_mode != &"manual":
			continue
		if unit_state.source_member_id == &"":
			continue
		_runtime._battle_rating_stats[unit_state.source_member_id] = {
			"member_id": unit_state.source_member_id,
			"member_name": unit_state.display_name if not unit_state.display_name.is_empty() else String(unit_state.source_member_id),
			"cast_counts": {},
			"successful_skill_count": 0,
			"total_damage_done": 0,
			"total_healing_done": 0,
			"kill_count": 0,
		}


func record_skill_success(active_unit: BattleUnitState, skill_id: StringName) -> void:
	if not _has_runtime():
		return
	var stats := _get_battle_rating_stats(active_unit)
	if stats.is_empty() or skill_id == &"":
		return

	var cast_counts: Dictionary = stats.get("cast_counts", {})
	cast_counts[skill_id] = int(cast_counts.get(skill_id, 0)) + 1
	stats["cast_counts"] = cast_counts
	stats["successful_skill_count"] = int(stats.get("successful_skill_count", 0)) + 1
	_runtime._battle_rating_stats[active_unit.source_member_id] = stats


func record_skill_effect_result(active_unit: BattleUnitState, damage: int, healing: int, kill_count: int) -> void:
	if not _has_runtime():
		return
	var stats := _get_battle_rating_stats(active_unit)
	if stats.is_empty():
		return

	stats["total_damage_done"] = int(stats.get("total_damage_done", 0)) + maxi(damage, 0)
	stats["total_healing_done"] = int(stats.get("total_healing_done", 0)) + maxi(healing, 0)
	stats["kill_count"] = int(stats.get("kill_count", 0)) + maxi(kill_count, 0)
	_runtime._battle_rating_stats[active_unit.source_member_id] = stats


func record_enemy_defeated_achievement(source_unit: BattleUnitState, target_unit: BattleUnitState) -> void:
	if not _has_runtime():
		return
	var character_gateway: Object = _runtime._character_gateway
	if source_unit == null or target_unit == null or character_gateway == null:
		return
	if source_unit.source_member_id == &"":
		return
	if String(target_unit.faction_id) == String(source_unit.faction_id):
		return
	if not character_gateway.has_method("record_achievement_event"):
		return
	character_gateway.record_achievement_event(source_unit.source_member_id, &"enemy_defeated", 1)


func record_battle_won_achievements() -> void:
	if not _has_runtime():
		return
	var character_gateway: Object = _runtime._character_gateway
	if _runtime._state == null or _runtime._state.winner_faction_id != &"player" or character_gateway == null:
		return
	if not character_gateway.has_method("record_achievement_event"):
		return

	for ally_unit_id in _runtime._state.ally_unit_ids:
		var unit_state := _runtime._state.units.get(ally_unit_id) as BattleUnitState
		if unit_state == null or unit_state.source_member_id == &"":
			continue
		character_gateway.record_achievement_event(unit_state.source_member_id, &"battle_won", 1)


func finalize_battle_rating_rewards() -> void:
	if not _has_runtime():
		return
	_runtime._pending_post_battle_character_rewards.clear()
	if _runtime._state == null or _runtime._character_gateway == null:
		return
	if not _runtime._character_gateway.has_method("build_pending_skill_mastery_reward"):
		return

	var player_victory: bool = _runtime._state.winner_faction_id == &"player"
	for stats_variant in _runtime._battle_rating_stats.values():
		if stats_variant is not Dictionary:
			continue
		var stats: Dictionary = stats_variant
		var score := calculate_battle_rating_score(stats, player_victory)
		var mastery_amount := resolve_battle_rating_mastery_amount(score)
		if mastery_amount <= 0:
			continue
		var cast_counts: Dictionary = stats.get("cast_counts", {})
		if cast_counts.is_empty():
			continue

		var member_id := ProgressionDataUtils.to_string_name(stats.get("member_id", ""))
		if member_id == &"":
			continue
		var member_name := String(stats.get("member_name", member_id))
		var rating_label := resolve_battle_rating_label(score)
		var reward_entries: Array[Dictionary] = []
		for skill_key in cast_counts.keys():
			var skill_id := ProgressionDataUtils.to_string_name(skill_key)
			if skill_id == &"" or int(cast_counts.get(skill_key, 0)) <= 0:
				continue
			reward_entries.append({
				"entry_type": "skill_mastery",
				"target_id": String(skill_id),
				"target_label": "",
				"amount": mastery_amount,
				"reason_text": "战斗评分 %d · %s" % [score, rating_label],
			})
		if reward_entries.is_empty():
			continue

		var reward = _runtime._character_gateway.build_pending_skill_mastery_reward(
			member_id,
			BATTLE_RATING_SOURCE_TYPE,
			"战斗结算",
			reward_entries,
			"在战斗中，%s%s。评分 %d。" % [
				member_name,
				_resolve_battle_rating_summary_suffix(score),
				score,
			]
		)
		if reward != null and not reward.is_empty():
			_runtime._pending_post_battle_character_rewards.append(reward)


func calculate_battle_rating_score(stats: Dictionary, player_victory: bool) -> int:
	var successful_skill_count := int(stats.get("successful_skill_count", 0))
	var total_damage_done := int(stats.get("total_damage_done", 0))
	var total_healing_done := int(stats.get("total_healing_done", 0))
	var kill_count := int(stats.get("kill_count", 0))
	var member_id := ProgressionDataUtils.to_string_name(stats.get("member_id", ""))
	var survived := false
	if _has_runtime() and _runtime._state != null and member_id != &"":
		var unit_state := _find_unit_by_member_id(member_id)
		survived = unit_state != null and unit_state.is_alive

	var score := 0
	if successful_skill_count > 0:
		score += 1
	score += mini(successful_skill_count, 3)
	if total_damage_done > 0 or total_healing_done > 0:
		score += 1
	if kill_count > 0:
		score += 1
	if player_victory:
		score += 1
	if survived:
		score += 1
	return score


func resolve_battle_rating_mastery_amount(score: int) -> int:
	if score >= 6:
		return 6
	if score >= 4:
		return 4
	if score >= 2:
		return 2
	return 0


func resolve_battle_rating_label(score: int) -> String:
	return _resolve_battle_rating_summary_suffix(score)


func _get_battle_rating_stats(active_unit: BattleUnitState) -> Dictionary:
	if not _has_runtime():
		return {}
	if active_unit == null or active_unit.source_member_id == &"":
		return {}
	var stats_variant = _runtime._battle_rating_stats.get(active_unit.source_member_id, {})
	return stats_variant.duplicate(true) if stats_variant is Dictionary else {}


func _find_unit_by_member_id(member_id: StringName) -> BattleUnitState:
	if not _has_runtime() or _runtime._state == null:
		return null
	for unit_state_data in _runtime._state.units.values():
		var unit_state := unit_state_data as BattleUnitState
		if unit_state != null and unit_state.source_member_id == member_id:
			return unit_state
	return null


func _resolve_battle_rating_summary_suffix(score: int) -> String:
	if score >= 6:
		return "若有所悟"
	if score >= 4:
		return "渐入佳境"
	if score >= 2:
		return "有所体会"
	return "尚需磨炼"


func _has_runtime() -> bool:
	return _runtime != null
