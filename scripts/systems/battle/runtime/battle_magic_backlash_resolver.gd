class_name BattleMagicBacklashResolver
extends RefCounted

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")


func should_resolve_spell_control(skill_def: SkillDef) -> bool:
	return skill_def != null \
		and skill_def.combat_profile != null \
		and skill_def.combat_profile.has_spell_fate_control()


func apply_spell_control_after_cost(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	skill_level: int,
	spent_mp: int,
	control_metadata: Dictionary,
	batch: BattleEventBatch = null
) -> Dictionary:
	var result := {
		"skip_effects": false,
		"backlash_triggered": false,
		"fumble_protected": false,
		"mp_refund": 0,
		"extra_mp_drained": 0,
		"spell_control": control_metadata.duplicate(true),
	}
	if source_unit == null or skill_def == null or skill_def.combat_profile == null or control_metadata.is_empty():
		return result

	if bool(control_metadata.get("reverse_fate_downgraded", false)):
		_append_log(batch, "%s 的逆命护符压住了失控征兆，法术仍按原轨迹释放。" % _unit_label(source_unit))
		return result

	if bool(control_metadata.get("critical_hit", false)):
		var refund := _apply_spell_critical_bonus(source_unit, skill_def, spent_mp)
		result["mp_refund"] = refund
		if refund > 0:
			_append_log(batch, "%s 的魔力回路大成功，返还 %d 点法力。" % [_unit_label(source_unit), refund])
		return result

	if not bool(control_metadata.get("critical_fail", false)):
		return result

	var protection_limit := skill_def.combat_profile.get_fumble_protection_limit(skill_level)
	var protection_used := _get_fumble_protection_used(source_unit, skill_def.skill_id)
	if protection_used < protection_limit:
		_set_fumble_protection_used(source_unit, skill_def.skill_id, protection_used + 1)
		var drained := _apply_fumble_protection_mp_drain(source_unit, skill_def, spent_mp)
		result["skip_effects"] = true
		result["fumble_protected"] = true
		result["extra_mp_drained"] = drained
		_append_log(batch, "%s 压制了魔力大失败，本场 %s 保护次数 %d/%d，额外吞噬 %d 点法力。" % [
			_unit_label(source_unit),
			_skill_label(skill_def),
			protection_used + 1,
			protection_limit,
			drained,
		])
		return result

	result["backlash_triggered"] = true
	_append_log(batch, "%s 的魔力控制大失败，法术落点开始偏移。" % _unit_label(source_unit))
	return result


func build_ground_backlash_target_coords(
	skill_def: SkillDef,
	target_coords: Array[Vector2i],
	state: BattleState,
	grid_service,
	control_context: Dictionary
) -> Dictionary:
	var result := {
		"target_coords": target_coords.duplicate(),
		"backlash_triggered": bool(control_context.get("backlash_triggered", false)),
		"original_target_coord": Vector2i(-1, -1),
		"resolved_target_coord": Vector2i(-1, -1),
		"offset_delta": Vector2i.ZERO,
		"backlash_offset_fallback": false,
	}
	if not bool(result.get("backlash_triggered", false)):
		return result
	if skill_def == null or skill_def.combat_profile == null:
		return result
	if not skill_def.combat_profile.uses_ground_anchor_drift_backlash():
		return result
	if state == null or grid_service == null or target_coords.size() != 1:
		result["backlash_offset_fallback"] = true
		return result

	var radius := maxi(int(skill_def.combat_profile.backlash_offset_radius), 0)
	var original_coord := target_coords[0]
	result["original_target_coord"] = original_coord
	if radius <= 0:
		result["resolved_target_coord"] = original_coord
		result["backlash_offset_fallback"] = true
		return result

	var candidates := _collect_ground_anchor_drift_candidates(state, grid_service, original_coord, radius)
	if candidates.is_empty():
		result["resolved_target_coord"] = original_coord
		result["backlash_offset_fallback"] = true
		return result

	var picked_index := TRUE_RANDOM_SEED_SERVICE_SCRIPT.randi_range(0, candidates.size() - 1)
	var resolved_coord: Vector2i = candidates[picked_index]
	result["target_coords"] = [resolved_coord]
	result["resolved_target_coord"] = resolved_coord
	result["offset_delta"] = resolved_coord - original_coord
	return result


func append_ground_backlash_log(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	drift_context: Dictionary,
	batch: BattleEventBatch
) -> void:
	if batch == null or not bool(drift_context.get("backlash_triggered", false)):
		return
	var original_coord: Vector2i = drift_context.get("original_target_coord", Vector2i(-1, -1))
	var resolved_coord: Vector2i = drift_context.get("resolved_target_coord", original_coord)
	if bool(drift_context.get("backlash_offset_fallback", false)) or original_coord == resolved_coord:
		_append_log(batch, "%s 的 %s 未找到可偏移落点，失控魔力仍在原地爆发。" % [
			_unit_label(source_unit),
			_skill_label(skill_def),
		])
		return
	_append_log(batch, "%s 的 %s 从 (%d, %d) 偏移到 (%d, %d)。" % [
		_unit_label(source_unit),
		_skill_label(skill_def),
		original_coord.x,
		original_coord.y,
		resolved_coord.x,
		resolved_coord.y,
	])


func _apply_spell_critical_bonus(source_unit: BattleUnitState, skill_def: SkillDef, spent_mp: int) -> int:
	if source_unit == null or skill_def == null or skill_def.combat_profile == null:
		return 0
	if skill_def.combat_profile.spell_critical_mode != &"mp_refund":
		return 0
	var refund_percent := skill_def.combat_profile.get_spell_critical_mp_refund_percent()
	if refund_percent <= 0 or spent_mp <= 0:
		return 0
	var refund := int(round(float(spent_mp) * float(refund_percent) / 100.0))
	refund = clampi(maxi(refund, 1), 0, spent_mp)
	var mp_max := _get_mp_max(source_unit)
	if mp_max > 0:
		refund = mini(refund, maxi(mp_max - int(source_unit.current_mp), 0))
	if refund <= 0:
		return 0
	source_unit.current_mp += refund
	return refund


func _apply_fumble_protection_mp_drain(source_unit: BattleUnitState, skill_def: SkillDef, spent_mp: int) -> int:
	if source_unit == null or skill_def == null or skill_def.combat_profile == null:
		return 0
	var drain_percent := skill_def.combat_profile.get_fumble_protection_extra_mp_percent()
	if drain_percent <= 0 or spent_mp <= 0:
		return 0
	var drain := int(round(float(spent_mp) * float(drain_percent) / 100.0))
	drain = maxi(drain, 1)
	drain = mini(drain, maxi(int(source_unit.current_mp), 0))
	source_unit.current_mp = maxi(int(source_unit.current_mp) - drain, 0)
	return drain


func _collect_ground_anchor_drift_candidates(
	state: BattleState,
	grid_service,
	original_coord: Vector2i,
	radius: int
) -> Array[Vector2i]:
	var candidates: Array[Vector2i] = []
	for y in range(original_coord.y - radius, original_coord.y + radius + 1):
		for x in range(original_coord.x - radius, original_coord.x + radius + 1):
			var candidate := Vector2i(x, y)
			if candidate == original_coord:
				continue
			if maxi(absi(candidate.x - original_coord.x), absi(candidate.y - original_coord.y)) > radius:
				continue
			if not grid_service.is_inside(state, candidate):
				continue
			candidates.append(candidate)
	candidates.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		return a.y < b.y or (a.y == b.y and a.x < b.x)
	)
	return candidates


func _get_fumble_protection_used(source_unit: BattleUnitState, skill_id: StringName) -> int:
	if source_unit == null or skill_id == &"":
		return 0
	return maxi(int(source_unit.fumble_protection_used.get(skill_id, 0)), 0)


func _set_fumble_protection_used(source_unit: BattleUnitState, skill_id: StringName, value: int) -> void:
	if source_unit == null or skill_id == &"":
		return
	source_unit.fumble_protection_used[skill_id] = maxi(value, 0)


func _get_mp_max(source_unit: BattleUnitState) -> int:
	if source_unit == null or source_unit.attribute_snapshot == null:
		return 0
	return maxi(int(source_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX)), 0)


func _append_log(batch: BattleEventBatch, message: String) -> void:
	if batch == null or message.is_empty():
		return
	batch.log_lines.append(message)


func _unit_label(source_unit: BattleUnitState) -> String:
	if source_unit == null or source_unit.display_name.is_empty():
		return "施法者"
	return source_unit.display_name


func _skill_label(skill_def: SkillDef) -> String:
	if skill_def == null:
		return "法术"
	if not skill_def.display_name.strip_edges().is_empty():
		return skill_def.display_name
	if skill_def.skill_id != &"":
		return String(skill_def.skill_id)
	return "法术"
