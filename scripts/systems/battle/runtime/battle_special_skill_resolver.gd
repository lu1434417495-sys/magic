class_name BattleSpecialSkillResolver
extends RefCounted

const LOW_LUCK_RELIC_RULES_SCRIPT = preload("res://scripts/systems/fate/low_luck_relic_rules.gd")
const MISFORTUNE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/fate/misfortune_service.gd")

const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")

const BODY_SIZE_RULES_SCRIPT = preload("res://scripts/systems/progression/body_size_rules.gd")

const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")

const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const BodySizeRules = BODY_SIZE_RULES_SCRIPT

const BODY_SIZE_CATEGORY_OVERRIDE_EFFECT_TYPE: StringName = &"body_size_category_override"

const LAYERED_BARRIER_EFFECT_TYPE: StringName = &"layered_barrier"

const STATUS_MARKED: StringName = &"marked"

const STATUS_GUARDING: StringName = &"guarding"

const STATUS_VAJRA_BODY: StringName = &"vajra_body"

const STATUS_BLACK_STAR_BRAND_NORMAL: StringName = &"black_star_brand_normal"

const STATUS_BLACK_STAR_BRAND_ELITE: StringName = &"black_star_brand_elite"

const STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW: StringName = &"black_star_brand_elite_guard_window"

const STATUS_CROWN_BREAK_BROKEN_FANG: StringName = &"crown_break_broken_fang"

const STATUS_CROWN_BREAK_BROKEN_HAND: StringName = &"crown_break_broken_hand"

const STATUS_CROWN_BREAK_BLINDED_EYE: StringName = &"crown_break_blinded_eye"

const BLACK_CONTRACT_PUSH_SKILL_ID: StringName = &"black_contract_push"

const DOOM_SHIFT_SKILL_ID: StringName = &"doom_shift"

const BLACK_CROWN_SEAL_SKILL_ID: StringName = MISFORTUNE_SERVICE_SCRIPT.BLACK_CROWN_SEAL_SKILL_ID

const BLACK_STAR_BRAND_SKILL_ID: StringName = MISFORTUNE_SERVICE_SCRIPT.BLACK_STAR_BRAND_SKILL_ID

const CROWN_BREAK_SKILL_ID: StringName = MISFORTUNE_SERVICE_SCRIPT.CROWN_BREAK_SKILL_ID

const DOOM_SENTENCE_SKILL_ID: StringName = MISFORTUNE_SERVICE_SCRIPT.DOOM_SENTENCE_SKILL_ID

const BLACK_STAR_BRAND_DURATION_TU = 60

const DOOM_SHIFT_SELF_DEBUFF_DURATION_TU = 60

const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"

const BOSS_TARGET_STAT_ID: StringName = &"boss_target"

const STATUS_PARAM_BODY_SIZE_CATEGORY_OVERRIDE = "body_size_category_override"

const STATUS_PARAM_PREVIOUS_BODY_SIZE_CATEGORY = "previous_body_size_category"

const FORCED_MOVE_INVALID_SCORE := -999999

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


func _is_unit_valid_for_effect(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	target_team_filter: StringName
) -> bool:
	if _runtime == null:
		return false
	return _runtime._is_unit_valid_for_effect(source_unit, target_unit, target_team_filter)

func _apply_skill_mastery_grant(unit_state: BattleUnitState, grant: Dictionary, batch: BattleEventBatch) -> void:
	if _runtime == null:
		return
	_runtime._apply_skill_mastery_grant(unit_state, grant, batch)

func _append_changed_coords(batch: BattleEventBatch, coords: Array[Vector2i]) -> void:
	if _runtime == null:
		return
	_runtime._append_changed_coords(batch, coords)

func _append_changed_unit_id(batch: BattleEventBatch, unit_id: StringName) -> void:
	if _runtime == null:
		return
	_runtime._append_changed_unit_id(batch, unit_id)

func _append_changed_unit_coords(batch: BattleEventBatch, unit_state: BattleUnitState) -> void:
	if _runtime == null:
		return
	_runtime._append_changed_unit_coords(batch, unit_state)


func _apply_on_kill_gain_resources_effects(
	source_unit: BattleUnitState,
	defeated_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch
) -> void:
	if source_unit == null or defeated_unit == null or skill_def == null or batch == null:
		return
	if defeated_unit.is_alive:
		return
	for effect_def in effect_defs:
		if effect_def == null or effect_def.effect_type != &"on_kill_gain_resources":
			continue
		var params = effect_def.params if effect_def.params != null else {}
		var ap_gain = maxi(int(params.get("ap_gain", 0)), 0)
		var free_move_points_gain = maxi(int(params.get("free_move_points_gain", 0)), 0)
		if ap_gain <= 0 and free_move_points_gain <= 0:
			continue
		if ap_gain > 0:
			source_unit.current_ap += ap_gain
		if free_move_points_gain > 0:
			source_unit.current_move_points += free_move_points_gain
			source_unit.can_use_locked_move_points_this_turn = true
		_append_changed_unit_id(batch, source_unit.unit_id)
		var gain_parts: Array[String] = []
		if ap_gain > 0:
			gain_parts.append("恢复 %d AP" % ap_gain)
		if free_move_points_gain > 0:
			gain_parts.append("获得 %d 点普通移动力并可在行动后移动" % free_move_points_gain)
		batch.log_lines.append("%s 击倒 %s，触发 %s：%s。" % [
			source_unit.display_name,
			defeated_unit.display_name,
			skill_def.display_name if not skill_def.display_name.is_empty() else String(skill_def.skill_id),
			"，".join(gain_parts),
		])

func _apply_unit_skill_special_effects(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch,
	forced_move_context: Dictionary = {}
) -> Dictionary:
	var result = {
		"applied": false,
		"moved_steps": 0,
		"status_effect_ids": [],
		"log_lines": [],
	}
	if active_unit == null or skill_def == null:
		return result
	if _is_black_star_brand_skill(skill_def.skill_id):
		return _apply_black_star_brand_effect(active_unit, target_unit)
	if _is_doom_shift_skill(skill_def.skill_id):
		return _apply_doom_shift_effect(active_unit, target_unit, batch)
	if effect_defs.is_empty():
		return result

	var seen_forced_move_effects: Dictionary = {}
	for effect_def in effect_defs:
		if effect_def == null:
			continue
		if effect_def.effect_type == LAYERED_BARRIER_EFFECT_TYPE:
			var barrier_result = _runtime._layered_barrier_service.apply_layered_barrier_effect(
				active_unit,
				target_unit if target_unit != null else active_unit,
				skill_def,
				effect_def,
				batch
			) if _runtime._layered_barrier_service != null else {}
			if bool(barrier_result.get("applied", false)):
				result["applied"] = true
			continue
		if effect_def.effect_type == BODY_SIZE_CATEGORY_OVERRIDE_EFFECT_TYPE:
			var body_size_result = _apply_body_size_category_override_effect(active_unit, target_unit if target_unit != null else active_unit, effect_def, batch)
			if bool(body_size_result.get("applied", false)):
				result["applied"] = true
				for status_id in body_size_result.get("status_effect_ids", []):
					if not result["status_effect_ids"].has(status_id):
						result["status_effect_ids"].append(status_id)
				for log_line in body_size_result.get("log_lines", []):
					result["log_lines"].append(String(log_line))
			continue
		if effect_def.effect_type != &"forced_move":
			continue
		var forced_move_instance_id := effect_def.get_instance_id()
		if seen_forced_move_effects.has(forced_move_instance_id):
			continue
		seen_forced_move_effects[forced_move_instance_id] = true
		var move_target = target_unit if target_unit != null else active_unit
		var moved_steps = _apply_forced_move_effect(active_unit, move_target, effect_def, batch, forced_move_context)
		if moved_steps > 0:
			result["applied"] = true
			result["moved_steps"] = maxi(int(result.get("moved_steps", 0)), moved_steps)
	return result

func _apply_doom_shift_effect(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	batch: BattleEventBatch
) -> Dictionary:
	var result = {
		"applied": false,
		"moved_steps": 0,
		"status_effect_ids": [],
		"log_lines": [],
	}
	if _runtime._state == null or active_unit == null or target_unit == null:
		return result
	if target_unit.unit_id == active_unit.unit_id:
		return result
	if not _swap_unit_positions(active_unit, target_unit, batch):
		return result
	_set_runtime_status_effect(
		active_unit,
		STATUS_MARKED,
		DOOM_SHIFT_SELF_DEBUFF_DURATION_TU,
		active_unit.unit_id,
		1,
		{"counts_as_debuff": true}
	)
	_append_changed_unit_id(batch, active_unit.unit_id)
	result["applied"] = true
	result["log_lines"] = [
		"%s 先承受 marked，再与 %s 交换位置。" % [active_unit.display_name, target_unit.display_name],
	]
	return result

func _swap_unit_positions(
	first_unit: BattleUnitState,
	second_unit: BattleUnitState,
	batch: BattleEventBatch
) -> bool:
	if _runtime._state == null or first_unit == null or second_unit == null:
		return false
	if first_unit.unit_id == second_unit.unit_id:
		return false
	var first_previous_coords = first_unit.occupied_coords.duplicate()
	var second_previous_coords = second_unit.occupied_coords.duplicate()
	var first_coord = first_unit.coord
	var second_coord = second_unit.coord
	_runtime._grid_service.clear_unit_occupancy(_runtime._state, first_unit)
	_runtime._grid_service.clear_unit_occupancy(_runtime._state, second_unit)
	var can_swap = _runtime._grid_service.can_place_unit(_runtime._state, first_unit, second_coord, true) \
		and _runtime._grid_service.can_place_unit(_runtime._state, second_unit, first_coord, true)
	if not can_swap:
		_runtime._grid_service.set_occupants(_runtime._state, first_previous_coords, first_unit.unit_id)
		_runtime._grid_service.set_occupants(_runtime._state, second_previous_coords, second_unit.unit_id)
		return false
	_runtime._grid_service.place_unit(_runtime._state, first_unit, second_coord, true)
	_runtime._grid_service.place_unit(_runtime._state, second_unit, first_coord, true)
	_append_changed_coords(batch, first_previous_coords)
	_append_changed_coords(batch, second_previous_coords)
	_append_changed_unit_coords(batch, first_unit)
	_append_changed_unit_coords(batch, second_unit)
	_append_changed_unit_id(batch, first_unit.unit_id)
	_append_changed_unit_id(batch, second_unit.unit_id)
	return true

func _apply_black_star_brand_effect(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState
) -> Dictionary:
	var result = {
		"applied": false,
		"moved_steps": 0,
		"status_effect_ids": [],
		"log_lines": [],
	}
	if active_unit == null or target_unit == null:
		return result
	_clear_black_star_brand_statuses(target_unit)
	if _is_black_star_brand_elite_target(target_unit):
		_set_runtime_status_effect(target_unit, STATUS_BLACK_STAR_BRAND_ELITE, BLACK_STAR_BRAND_DURATION_TU, active_unit.unit_id)
		_set_runtime_status_effect(
			target_unit,
			STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW,
			BLACK_STAR_BRAND_DURATION_TU,
			active_unit.unit_id
		)
		result["status_effect_ids"] = [STATUS_BLACK_STAR_BRAND_ELITE, STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW]
		result["log_lines"] = [
			"%s 被施加黑星烙印：暴击失效、命中下降，且第一次受击会被穿透部分格挡。" % target_unit.display_name,
		]
	else:
		_set_runtime_status_effect(target_unit, STATUS_BLACK_STAR_BRAND_NORMAL, BLACK_STAR_BRAND_DURATION_TU, active_unit.unit_id)
		target_unit.erase_status_effect(STATUS_GUARDING)
		result["status_effect_ids"] = [STATUS_BLACK_STAR_BRAND_NORMAL]
		result["log_lines"] = [
			"%s 被施加黑星烙印：无法反击，且无法进入格挡。" % target_unit.display_name,
		]
	result["applied"] = true
	return result

func _set_runtime_status_effect(
	unit_state: BattleUnitState,
	status_id: StringName,
	duration_tu: int,
	source_unit_id: StringName = &"",
	power: int = 1,
	params: Dictionary = {}
) -> void:
	if unit_state == null or status_id == &"":
		return
	var status_entry = BattleStatusEffectState.new()
	status_entry.status_id = status_id
	status_entry.source_unit_id = source_unit_id
	status_entry.power = maxi(power, 1)
	status_entry.stacks = 1
	status_entry.duration = maxi(duration_tu, -1)
	status_entry.params = params.duplicate(true)
	unit_state.set_status_effect(status_entry)

func _clear_black_star_brand_statuses(unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	unit_state.erase_status_effect(STATUS_BLACK_STAR_BRAND_NORMAL)
	unit_state.erase_status_effect(STATUS_BLACK_STAR_BRAND_ELITE)
	unit_state.erase_status_effect(STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW)

func _is_black_star_brand_elite_target(unit_state: BattleUnitState) -> bool:
	return _is_elite_or_boss_target(unit_state)

func _is_elite_or_boss_target(unit_state: BattleUnitState) -> bool:
	return unit_state != null \
		and unit_state.attribute_snapshot != null \
		and int(unit_state.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 0

func _is_boss_target(unit_state: BattleUnitState) -> bool:
	return unit_state != null \
		and unit_state.attribute_snapshot != null \
		and (
			int(unit_state.attribute_snapshot.get_value(BOSS_TARGET_STAT_ID)) > 0
			or int(unit_state.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 1
		)

func _is_black_star_brand_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == BLACK_STAR_BRAND_SKILL_ID

func _is_black_contract_push_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == BLACK_CONTRACT_PUSH_SKILL_ID

func _is_doom_shift_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == DOOM_SHIFT_SKILL_ID

func _is_black_crown_seal_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == BLACK_CROWN_SEAL_SKILL_ID

func _clear_crown_break_seal_statuses(unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	unit_state.erase_status_effect(STATUS_CROWN_BREAK_BROKEN_FANG)
	unit_state.erase_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND)
	unit_state.erase_status_effect(STATUS_CROWN_BREAK_BLINDED_EYE)

func _is_crown_break_target_eligible(active_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	return target_unit != null \
		and _is_unit_valid_for_effect(active_unit, target_unit, &"enemy") \
		and target_unit.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE)

func _is_crown_break_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == CROWN_BREAK_SKILL_ID

func _is_doom_sentence_target_eligible(active_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	return target_unit != null \
		and _is_unit_valid_for_effect(active_unit, target_unit, &"enemy") \
		and _is_elite_or_boss_target(target_unit)

func _is_black_crown_seal_target_eligible(active_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	return target_unit != null \
		and _is_unit_valid_for_effect(active_unit, target_unit, &"enemy") \
		and _is_boss_target(target_unit)

func _is_doom_sentence_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == DOOM_SENTENCE_SKILL_ID

func _apply_forced_move_effect(
	source_unit: BattleUnitState,
	unit_state: BattleUnitState,
	effect_def: CombatEffectDef,
	batch: BattleEventBatch,
	forced_move_context: Dictionary = {}
) -> int:
	if _runtime._state == null or unit_state == null or effect_def == null:
		return 0
	var move_distance = maxi(int(effect_def.forced_move_distance), 0)
	if move_distance <= 0:
		return 0
	if _blocks_enemy_forced_move(source_unit, unit_state):
		if batch != null:
			batch.log_lines.append("%s 稳如金刚，未被强制位移。" % unit_state.display_name)
		return 0

	var mode = effect_def.forced_move_mode
	if mode == &"":
		return 0
	if mode == &"jump" or mode == &"blink":
		# 地面位移由 battle_ground_effect_service 在 precast 阶段处理；
		# 这里不做逐格推动，避免落地后再被推一格。
		return 0

	var moved_steps = 0
	for _step in range(move_distance):
		var next_coord = _pick_forced_move_coord(unit_state, mode, source_unit, forced_move_context)
		if next_coord == Vector2i(-1, -1) or next_coord == unit_state.coord:
			break
		if not _runtime._grid_service.can_traverse(_runtime._state, unit_state.coord, next_coord, unit_state):
			break
		var barrier_result: Dictionary = _runtime._layered_barrier_service.resolve_unit_boundary_crossing(unit_state, unit_state.coord, next_coord, batch) if _runtime._layered_barrier_service != null else {}
		if bool(barrier_result.get("blocked", false)) or not unit_state.is_alive:
			break
		var previous_coords = unit_state.occupied_coords.duplicate()
		if not _runtime._grid_service.move_unit(_runtime._state, unit_state, next_coord):
			break
		moved_steps += 1
		_append_changed_coords(batch, previous_coords)
		_append_changed_unit_coords(batch, unit_state)
		_append_changed_unit_id(batch, unit_state.unit_id)
	return moved_steps

func _apply_body_size_category_override_effect(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	effect_def: CombatEffectDef,
	batch: BattleEventBatch
) -> Dictionary:
	var result = {
		"applied": false,
		"status_effect_ids": [],
		"log_lines": [],
	}
	if _runtime._state == null or target_unit == null or effect_def == null:
		return result
	var status_id = ProgressionDataUtils.to_string_name(effect_def.status_id)
	var target_category = ProgressionDataUtils.to_string_name(effect_def.body_size_category)
	if status_id == &"" or not BodySizeRules.is_valid_body_size_category(target_category):
		return result
	var duration_tu = maxi(int(effect_def.duration_tu), 0)
	if duration_tu <= 0:
		return result

	var existing_entry = target_unit.get_status_effect(status_id) as BattleStatusEffectState
	var restore_category = target_unit.body_size_category
	if existing_entry != null and existing_entry.params != null:
		var existing_restore_category = ProgressionDataUtils.to_string_name(existing_entry.params.get(STATUS_PARAM_PREVIOUS_BODY_SIZE_CATEGORY, ""))
		if BodySizeRules.is_valid_body_size_category(existing_restore_category):
			restore_category = existing_restore_category

	var previous_category = target_unit.body_size_category
	var previous_body_size = int(target_unit.body_size)
	var previous_footprint = target_unit.footprint_size
	var previous_coords = target_unit.occupied_coords.duplicate()
	_runtime._grid_service.clear_unit_occupancy(_runtime._state, target_unit)
	target_unit.set_body_size_category(target_category)
	if not _runtime._grid_service.can_place_footprint(_runtime._state, target_unit.coord, target_unit.footprint_size, target_unit.unit_id, target_unit):
		target_unit.body_size_category = previous_category
		target_unit.body_size = previous_body_size
		target_unit.footprint_size = previous_footprint
		target_unit.occupied_coords = previous_coords
		_runtime._grid_service.set_occupants(_runtime._state, target_unit.occupied_coords, target_unit.unit_id)
		result["log_lines"].append("%s 周围空间不足，无法改变体型。" % target_unit.display_name)
		return result
	_runtime._grid_service.set_occupants(_runtime._state, target_unit.occupied_coords, target_unit.unit_id)

	var status_params = effect_def.params.duplicate(true) if effect_def.params != null else {}
	status_params[STATUS_PARAM_BODY_SIZE_CATEGORY_OVERRIDE] = String(target_category)
	status_params[STATUS_PARAM_PREVIOUS_BODY_SIZE_CATEGORY] = String(restore_category)
	_set_runtime_status_effect(
		target_unit,
		status_id,
		duration_tu,
		source_unit.unit_id if source_unit != null else &"",
		maxi(int(effect_def.power), 1),
		status_params
	)
	_append_changed_coords(batch, previous_coords)
	_append_changed_unit_coords(batch, target_unit)
	_append_changed_unit_id(batch, target_unit.unit_id)
	result["applied"] = true
	result["status_effect_ids"] = [status_id]
	result["log_lines"].append("%s 的体型临时变为 %s。" % [target_unit.display_name, String(target_category)])
	return result

func _blocks_enemy_forced_move(source_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	if source_unit == null or target_unit == null:
		return false
	if source_unit.unit_id == target_unit.unit_id:
		return false
	if String(source_unit.faction_id) == String(target_unit.faction_id):
		return false
	var status_entry = target_unit.get_status_effect(STATUS_VAJRA_BODY)
	if status_entry == null or status_entry.params == null:
		return false
	return bool(status_entry.params.get("forced_move_immune", false))

func _record_vajra_body_mastery_from_incoming_damage(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	result: Dictionary,
	batch: BattleEventBatch = null
) -> void:
	var grant = _runtime._skill_mastery_service.build_vajra_body_mastery_grant(
		source_unit,
		target_unit,
		skill_def,
		result,
		_runtime._skill_defs
	)
	_apply_skill_mastery_grant(target_unit, grant, batch)

func _pick_forced_move_coord(
	unit_state: BattleUnitState,
	mode: StringName,
	source_unit: BattleUnitState = null,
	forced_move_context: Dictionary = {}
) -> Vector2i:
	if _runtime._state == null or unit_state == null:
		return Vector2i(-1, -1)
	unit_state.refresh_footprint()
	var best_coord = Vector2i(-1, -1)
	var best_score = FORCED_MOVE_INVALID_SCORE
	for direction in [Vector2i.UP, Vector2i.LEFT, Vector2i.RIGHT, Vector2i.DOWN]:
		var candidate_coord: Vector2i = unit_state.coord + direction
		if not _runtime._grid_service.can_traverse(_runtime._state, unit_state.coord, candidate_coord, unit_state):
			continue
		var candidate_score = _score_forced_move_coord(unit_state, candidate_coord, mode, source_unit, forced_move_context)
		if candidate_score <= FORCED_MOVE_INVALID_SCORE:
			continue
		if candidate_score > best_score or (candidate_score == best_score and (best_coord == Vector2i(-1, -1) or candidate_coord.y < best_coord.y or (candidate_coord.y == best_coord.y and candidate_coord.x < best_coord.x))):
			best_score = candidate_score
			best_coord = candidate_coord
	return best_coord

func _score_forced_move_coord(
	unit_state: BattleUnitState,
	candidate_coord: Vector2i,
	mode: StringName,
	source_unit: BattleUnitState = null,
	forced_move_context: Dictionary = {}
) -> int:
	if _runtime._state == null or unit_state == null:
		return FORCED_MOVE_INVALID_SCORE
	if mode == &"wind_push":
		return _score_wind_push_coord(unit_state, candidate_coord, source_unit, forced_move_context)
	var hostile_units = _collect_hostile_units_for(unit_state)
	var closest_hostile_distance = 0
	if not hostile_units.is_empty():
		closest_hostile_distance = 999999
		for hostile_unit in hostile_units:
			closest_hostile_distance = mini(closest_hostile_distance, _runtime._grid_service.get_distance(candidate_coord, hostile_unit.coord))
	var score = closest_hostile_distance * 100
	score -= _runtime._grid_service.get_distance(unit_state.coord, candidate_coord) * 10
	score -= candidate_coord.y * 2 + candidate_coord.x
	if mode == &"evasive":
		score += 5
	return score

func _score_wind_push_coord(
	unit_state: BattleUnitState,
	candidate_coord: Vector2i,
	source_unit: BattleUnitState,
	forced_move_context: Dictionary = {}
) -> int:
	var push_direction := _resolve_forced_move_direction(unit_state, source_unit, forced_move_context)
	if push_direction == Vector2i.ZERO:
		return FORCED_MOVE_INVALID_SCORE
	var step_delta := candidate_coord - unit_state.coord
	if _dot_vector2i(step_delta, push_direction) <= 0:
		return FORCED_MOVE_INVALID_SCORE
	var current_projection := _dot_vector2i(unit_state.coord, push_direction)
	var candidate_projection := _dot_vector2i(candidate_coord, push_direction)
	return (candidate_projection - current_projection) * 1000 - candidate_coord.y * 2 - candidate_coord.x

func _dot_vector2i(first: Vector2i, second: Vector2i) -> int:
	return first.x * second.x + first.y * second.y

func _resolve_forced_move_direction(
	unit_state: BattleUnitState,
	source_unit: BattleUnitState,
	forced_move_context: Dictionary = {}
) -> Vector2i:
	if forced_move_context != null:
		var direction_variant = forced_move_context.get("direction", Vector2i.ZERO)
		if direction_variant is Vector2i:
			var context_direction := _normalize_axis_direction(direction_variant)
			if context_direction != Vector2i.ZERO:
				return context_direction
	if source_unit != null and unit_state != null:
		return _normalize_axis_direction(unit_state.coord - source_unit.coord)
	return Vector2i.ZERO

func _normalize_axis_direction(direction: Vector2i) -> Vector2i:
	if direction == Vector2i.ZERO:
		return Vector2i.ZERO
	var abs_x := absi(direction.x)
	var abs_y := absi(direction.y)
	if abs_x >= abs_y and abs_x > 0:
		return Vector2i(1 if direction.x > 0 else -1, 0)
	if abs_y > 0:
		return Vector2i(0, 1 if direction.y > 0 else -1)
	return Vector2i.ZERO

func _collect_hostile_units_for(unit_state: BattleUnitState) -> Array[BattleUnitState]:
	var hostile_units: Array[BattleUnitState] = []
	if _runtime._state == null or unit_state == null:
		return hostile_units
	for other_unit_variant in _runtime._state.units.values():
		var other_unit = other_unit_variant as BattleUnitState
		if other_unit == null or other_unit.unit_id == unit_state.unit_id or not other_unit.is_alive:
			continue
		if String(other_unit.faction_id) == String(unit_state.faction_id):
			continue
		hostile_units.append(other_unit)
	return hostile_units

func _handle_adjacent_ally_defeat(defeated_unit: BattleUnitState) -> void:
	if _runtime._state == null or defeated_unit == null:
		return
	if defeated_unit.is_alive or defeated_unit.source_member_id == &"":
		return
	if not _runtime.has_method("handle_misfortune_trigger"):
		return
	var adjacent_allies = _collect_adjacent_living_allies(defeated_unit)
	if adjacent_allies.is_empty():
		return
	_runtime.handle_misfortune_trigger(
		MISFORTUNE_SERVICE_SCRIPT.CALAMITY_REASON_ADJACENT_ALLY_DEFEATED,
		{
			"defeated_unit": defeated_unit,
			"adjacent_units": adjacent_allies,
		}
	)

func _handle_low_luck_relic_ally_defeat(defeated_unit: BattleUnitState, batch: BattleEventBatch = null) -> void:
	if _runtime._state == null or defeated_unit == null or defeated_unit.is_alive:
		return
	for unit_variant in _runtime._state.units.values():
		var candidate = unit_variant as BattleUnitState
		if candidate == null or not candidate.is_alive:
			continue
		if candidate.unit_id == defeated_unit.unit_id:
			continue
		if candidate.faction_id != defeated_unit.faction_id:
			continue
		if not LOW_LUCK_RELIC_RULES_SCRIPT.unit_has_flag(candidate, LOW_LUCK_RELIC_RULES_SCRIPT.ATTR_BLOOD_DEBT_SHAWL):
			continue
		candidate.current_ap += LOW_LUCK_RELIC_RULES_SCRIPT.BLOOD_DEBT_ALLY_DOWN_AP_GAIN
		_append_changed_unit_id(batch, candidate.unit_id)
		if batch != null:
			batch.log_lines.append("%s 目睹队友倒地，血债披肩返还 %d 点行动点。" % [
				candidate.display_name,
				LOW_LUCK_RELIC_RULES_SCRIPT.BLOOD_DEBT_ALLY_DOWN_AP_GAIN,
			])

func _collect_adjacent_living_allies(defeated_unit: BattleUnitState) -> Array[BattleUnitState]:
	var adjacent_allies: Array[BattleUnitState] = []
	if defeated_unit == null:
		return adjacent_allies
	defeated_unit.refresh_footprint()
	for unit_variant in _runtime._state.units.values():
		var candidate = unit_variant as BattleUnitState
		if candidate == null or not candidate.is_alive:
			continue
		if candidate.unit_id == defeated_unit.unit_id:
			continue
		if candidate.faction_id != defeated_unit.faction_id or candidate.source_member_id == &"":
			continue
		candidate.refresh_footprint()
		if _are_units_adjacent(candidate, defeated_unit):
			adjacent_allies.append(candidate)
	return adjacent_allies

func _are_units_adjacent(first_unit: BattleUnitState, second_unit: BattleUnitState) -> bool:
	if first_unit == null or second_unit == null:
		return false
	for first_coord in first_unit.occupied_coords:
		for second_coord in second_unit.occupied_coords:
			if absi(first_coord.x - second_coord.x) + absi(first_coord.y - second_coord.y) == 1:
				return true
	return false
