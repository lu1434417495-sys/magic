class_name BattleAiMutationGuard
extends RefCounted

const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")

const MAX_REPORTED_VIOLATIONS := 64
const ALLOWED_ACTIVE_UNIT_FIELDS := {
	"ai_brain_id": true,
	"ai_state_id": true,
}
const ALLOWED_ACTIVE_BLACKBOARD_KEYS := {
	"last_brain_id": true,
	"last_state_id": true,
	"last_action_id": true,
	"last_reason_text": true,
	"last_transition_previous_state_id": true,
	"last_transition_state_id": true,
	"last_transition_rule_id": true,
	"last_transition_reason": true,
	"turn_decision_count": true,
}
const STATE_FIELD_NAMES := [
	"battle_id",
	"seed",
	"attack_roll_nonce",
	"phase",
	"map_size",
	"world_coord",
	"encounter_anchor_id",
	"terrain_profile_id",
	"attack_disadvantage_tags",
	"ally_unit_ids",
	"enemy_unit_ids",
	"active_unit_id",
	"winner_faction_id",
	"log_entries",
	"report_entries",
	"promotion_queue",
	"modal_state",
	"layered_barrier_fields",
]
const UNIT_FIELD_NAMES := [
	"unit_id",
	"source_member_id",
	"enemy_template_id",
	"display_name",
	"faction_id",
	"control_mode",
	"ai_brain_id",
	"ai_state_id",
	"ai_blackboard",
	"coord",
	"body_size",
	"body_size_category",
	"footprint_size",
	"occupied_coords",
	"is_alive",
	"current_hp",
	"current_mp",
	"current_stamina",
	"current_aura",
	"current_ap",
	"current_move_points",
	"unlocked_combat_resource_ids",
	"stamina_recovery_progress",
	"is_resting",
	"has_taken_action_this_turn",
	"has_moved_this_turn",
	"can_use_locked_move_points_this_turn",
	"current_shield_hp",
	"shield_max_hp",
	"shield_duration",
	"shield_family",
	"shield_source_unit_id",
	"shield_source_skill_id",
	"shield_params",
	"action_progress",
	"action_threshold",
	"known_active_skill_ids",
	"known_skill_level_map",
	"known_skill_lock_hit_bonus_map",
	"movement_tags",
	"vision_tags",
	"proficiency_tags",
	"save_advantage_tags",
	"damage_resistances",
	"race_trait_ids",
	"subrace_trait_ids",
	"ascension_trait_ids",
	"bloodline_trait_ids",
	"versatility_pick",
	"weapon_profile_kind",
	"weapon_item_id",
	"weapon_profile_type_id",
	"weapon_family",
	"weapon_current_grip",
	"weapon_attack_range",
	"weapon_one_handed_dice",
	"weapon_two_handed_dice",
	"weapon_is_versatile",
	"weapon_uses_two_hands",
	"weapon_physical_damage_tag",
	"cooldowns",
	"last_turn_tu",
	"combo_state",
	"per_battle_charges",
	"per_turn_charges",
	"per_turn_charge_limits",
	"fumble_protection_used",
]

var _before_raw: Dictionary = {}
var _before_stable: Dictionary = {}
var _active_unit_id: StringName = &""


func capture(context) -> bool:
	if context == null or context.state == null or context.unit_state == null:
		return false
	_active_unit_id = context.unit_state.unit_id
	_before_raw = _capture_raw_snapshot(context)
	_before_stable = _to_stable_value(_before_raw)
	return true


func validate_and_restore(context) -> Array[String]:
	if _before_raw.is_empty() or context == null or context.state == null:
		return []
	var after_raw := _capture_raw_snapshot(context)
	var after_stable: Dictionary = _to_stable_value(after_raw)
	var expected_stable: Dictionary = _before_stable.duplicate(true)
	_apply_allowed_ai_bookkeeping(expected_stable, after_stable)
	var violations: Array[String] = []
	_collect_diffs(expected_stable, after_stable, "ai_decision", violations)
	if violations.is_empty():
		return []
	# diff 在 MAX_REPORTED_VIOLATIONS 处短路；提醒 caller 报告可能不完整，避免真 bug 排在 cap 之后被吞。
	if violations.size() >= MAX_REPORTED_VIOLATIONS:
		violations.append(
			"(report capped at %d violations; additional differences may exist)" % MAX_REPORTED_VIOLATIONS
		)
	_restore_raw_snapshot(context, _before_raw)
	return violations


func _capture_raw_snapshot(context) -> Dictionary:
	var state = context.state
	return {
		"state_fields": _capture_field_map(state, STATE_FIELD_NAMES),
		"timeline": _capture_timeline(state.timeline),
		"party_backpack_view": _clone_restore_value(state.party_backpack_view),
		"cells": _clone_cell_dict(state.cells),
		"cell_columns": BATTLE_CELL_STATE_SCRIPT.clone_columns(state.cell_columns),
		"units": _capture_units(state.units),
		"skill_defs": context.skill_defs.duplicate() if context.skill_defs is Dictionary else {},
	}


func _capture_field_map(source, field_names: Array) -> Dictionary:
	var result: Dictionary = {}
	if source == null:
		return result
	for field_name in field_names:
		result[field_name] = _clone_restore_value(source.get(field_name))
	return result


func _capture_timeline(timeline) -> Dictionary:
	# 用 to_dict() 覆盖 BattleTimelineState 的全部可序列化字段，避免白名单漏字段；
	# 老实现只 capture 4 个字段，timeline 其余状态被 AI 改了不会检测到。
	if timeline == null:
		return {}
	if timeline.has_method("to_dict"):
		return timeline.to_dict()
	return {
		"current_tu": int(timeline.current_tu),
		"tu_per_tick": int(timeline.tu_per_tick),
		"frozen": bool(timeline.frozen),
		"ready_unit_ids": timeline.ready_unit_ids.duplicate(),
	}


func _clone_cell_dict(cells: Dictionary) -> Dictionary:
	# 静默 drop 缺 duplicate_cell 的格子会让 snapshot 残缺；restore 阶段
	# `state.cells = _clone_cell_dict(snapshot)` 又会把整个 dict 替换掉，导致真实战斗格丢失。
	# 这里保留所有 coord，对无法深拷贝的 cell 走通用 fallback。
	var result: Dictionary = {}
	for coord in cells.keys():
		var cell = cells.get(coord)
		if cell != null and cell is Object and cell.has_method("duplicate_cell"):
			result[coord] = cell.duplicate_cell()
		else:
			result[coord] = _clone_restore_value(cell)
	return result


func _capture_units(units: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	for unit_id in units.keys():
		var unit = units.get(unit_id)
		if unit == null:
			continue
		# 在 capture 之前先把裸 dict 形态的 status_effects 物化成 BattleStatusEffectState 实例。
		# 否则 AI 内部首次调 BattleUnitState.get_status_effect(id) 会做 lazy materialization
		# 把 dict 替换成实例，那次写回会被 guard 误判为 AI 改了状态而触发 rollback。
		_materialize_lazy_status_effects(unit)
		result[unit_id] = {
			"unit_ref": unit,
			"fields": _capture_field_map(unit, UNIT_FIELD_NAMES),
			"attribute_snapshot_values": unit.attribute_snapshot.get_all_values() if unit.attribute_snapshot != null else {},
			"equipment_view": _clone_restore_value(unit.equipment_view),
			"status_effects": _clone_status_effects(unit.status_effects),
		}
	return result


## 把 unit.status_effects 中所有非实例形态（裸 Dictionary / 反序列化中间态）就地转换为
## BattleStatusEffectState 实例，使 AI 决策路径的 get_status_effect 不再触发写回。
## 与 BattleUnitState.get_status_effect 的 lazy 转换逻辑保持一致。
func _materialize_lazy_status_effects(unit) -> void:
	if unit == null or unit.status_effects == null:
		return
	var stale_keys: Array = []
	for status_id_variant in unit.status_effects.keys():
		var effect = unit.status_effects.get(status_id_variant)
		if effect is BATTLE_STATUS_EFFECT_STATE_SCRIPT:
			continue
		var effect_state = BATTLE_STATUS_EFFECT_STATE_SCRIPT.from_dict(effect)
		if effect_state == null or effect_state.is_empty():
			stale_keys.append(status_id_variant)
		else:
			unit.status_effects[status_id_variant] = effect_state
	for stale_key in stale_keys:
		unit.status_effects.erase(stale_key)


func _clone_status_effects(status_effects: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	for status_id in status_effects.keys():
		var effect = status_effects.get(status_id)
		# BattleUnitState.status_effects 允许 BattleStatusEffectState 实例和裸 Dictionary 共存；
		# has_method 前必须先判 Object，否则 Dictionary 形态会直接 crash。
		if effect != null and effect is Object and effect.has_method("duplicate_state"):
			result[status_id] = effect.duplicate_state()
		else:
			result[status_id] = _clone_restore_value(effect)
	return result


func _clone_restore_value(value):
	if value == null:
		return null
	if value is Dictionary:
		return value.duplicate(true)
	if value is Array:
		return value.duplicate(true)
	if value is Object:
		if value.has_method("duplicate_state"):
			return value.duplicate_state()
		if value is Resource:
			return value.duplicate(true)
		# Object 既无 duplicate_state 也不是 Resource 时无法深拷贝，
		# 返回原引用意味着 snapshot 与 live state 共享同一对象——AI 改它内部状态时
		# 比较和 restore 都会失效。把这条路径暴露出来，让缺 duplicate_state 的新类型尽早补上。
		push_warning(
			"BattleAiMutationGuard cannot deep-clone Object of type %s; mutation detection on its internals may misfire." \
				% value.get_class()
		)
	return value


func _restore_raw_snapshot(context, snapshot: Dictionary) -> void:
	var state = context.state
	if state == null:
		return
	_restore_field_map(state, snapshot.get("state_fields", {}))
	_restore_timeline(state, snapshot.get("timeline", {}))
	# 只要 snapshot 里捕获过 party_backpack_view 就要还原，不能因为"当前 state 已被 AI 清成 null"
	# 就跳过还原——那正是 mutation guard 应该撤销的越权写入。
	if snapshot.has("party_backpack_view"):
		state.party_backpack_view = _clone_restore_value(snapshot.get("party_backpack_view"))
	state.cells = _clone_cell_dict(snapshot.get("cells", {}))
	state.cell_columns = BATTLE_CELL_STATE_SCRIPT.clone_columns(snapshot.get("cell_columns", {}))
	state.units = _restore_units(snapshot.get("units", {}))
	context.skill_defs = snapshot.get("skill_defs", {}).duplicate()


func _restore_field_map(target, fields: Dictionary) -> void:
	if target == null:
		return
	for field_name in fields.keys():
		target.set(field_name, _clone_restore_value(fields.get(field_name)))


func _restore_timeline(state, timeline_snapshot: Dictionary) -> void:
	# 复用 BattleTimelineState.from_dict() 完成完整反序列化，覆盖与 to_dict() 对称的全部字段；
	# fallback 路径手工还原 4 个字段以兼容 to_dict 不可用的旧 timeline 实现。
	if timeline_snapshot.is_empty():
		state.timeline = null
		return
	var rebuilt = BATTLE_TIMELINE_STATE_SCRIPT.from_dict(timeline_snapshot)
	if rebuilt != null:
		state.timeline = rebuilt
		return
	var timeline = state.timeline
	if timeline == null:
		timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
	timeline.current_tu = int(timeline_snapshot.get("current_tu", 0))
	timeline.tu_per_tick = int(timeline_snapshot.get("tu_per_tick", BATTLE_TIMELINE_STATE_SCRIPT.TU_GRANULARITY))
	timeline.frozen = bool(timeline_snapshot.get("frozen", false))
	var ready_unit_ids: Array[StringName] = []
	for raw_unit_id in timeline_snapshot.get("ready_unit_ids", []):
		ready_unit_ids.append(ProgressionDataUtils.to_string_name(raw_unit_id))
	timeline.ready_unit_ids = ready_unit_ids
	state.timeline = timeline


func _restore_units(unit_snapshots: Dictionary) -> Dictionary:
	var restored_units: Dictionary = {}
	for unit_id in unit_snapshots.keys():
		var unit_snapshot: Dictionary = unit_snapshots.get(unit_id, {})
		var unit = unit_snapshot.get("unit_ref")
		if unit == null:
			continue
		_restore_field_map(unit, unit_snapshot.get("fields", {}))
		unit.attribute_snapshot = ATTRIBUTE_SNAPSHOT_SCRIPT.new()
		for raw_attribute_id in unit_snapshot.get("attribute_snapshot_values", {}).keys():
			unit.attribute_snapshot.set_value(
				ProgressionDataUtils.to_string_name(raw_attribute_id),
				int(unit_snapshot["attribute_snapshot_values"].get(raw_attribute_id, 0))
			)
		unit.equipment_view = _clone_restore_value(unit_snapshot.get("equipment_view"))
		unit.status_effects = _clone_status_effects(unit_snapshot.get("status_effects", {}))
		restored_units[unit_id] = unit
	return restored_units


func _apply_allowed_ai_bookkeeping(expected_stable: Dictionary, after_stable: Dictionary) -> void:
	var expected_units: Dictionary = expected_stable.get("units", {})
	var after_units: Dictionary = after_stable.get("units", {})
	# units dict 的 key 经过 _stable_key 标准化，StringName 被加上类型前缀，
	# 这里的 lookup key 必须按同一规则构造，否则永远 miss、bookkeeping 整段失效。
	var active_key := _stable_key(_active_unit_id)
	if not expected_units.has(active_key) or not after_units.has(active_key):
		return
	var expected_unit: Dictionary = expected_units.get(active_key, {})
	var after_unit: Dictionary = after_units.get(active_key, {})
	var expected_fields: Dictionary = expected_unit.get("fields", {})
	var after_fields: Dictionary = after_unit.get("fields", {})
	for field_name in ALLOWED_ACTIVE_UNIT_FIELDS.keys():
		if after_fields.has(field_name):
			expected_fields[field_name] = after_fields.get(field_name)
	var expected_blackboard: Dictionary = expected_fields.get("ai_blackboard", {})
	var after_blackboard: Dictionary = after_fields.get("ai_blackboard", {})
	for key in ALLOWED_ACTIVE_BLACKBOARD_KEYS.keys():
		if after_blackboard.has(key):
			expected_blackboard[key] = after_blackboard.get(key)
		elif expected_blackboard.has(key):
			expected_blackboard.erase(key)
	expected_fields["ai_blackboard"] = expected_blackboard
	expected_unit["fields"] = expected_fields
	expected_units[active_key] = expected_unit
	expected_stable["units"] = expected_units


func _to_stable_value(value):
	if value == null:
		return null
	if value is StringName:
		return String(value)
	if value is Dictionary:
		var result: Dictionary = {}
		for key in value.keys():
			if _stable_key(key) == "unit_ref":
				continue
			result[_stable_key(key)] = _to_stable_value(value.get(key))
		return result
	if value is Array:
		var result_array: Array = []
		for item in value:
			result_array.append(_to_stable_value(item))
		return result_array
	if value is Object:
		if value.has_method("to_dict"):
			return _to_stable_value(value.to_dict())
		if value.has_method("get_instance_id"):
			return int(value.get_instance_id())
	return value


func _stable_key(key) -> String:
	# 防止非字符串键和字符串键碰撞（反例：Vector2i(1, 2) 与字符串 "1,2" 都映射到 "1,2"），
	# 但 String / StringName 保持透明形态，避免破坏代码内对原 key 名的直接引用
	# （ALLOWED_ACTIVE_*, "unit_ref" 等常量、调用方 dict.get(plain_string) 等）。
	if key is String:
		return key
	if key is StringName:
		return String(key)
	if key is Vector2i:
		return "Vector2i(%d,%d)" % [key.x, key.y]
	# 其余非字符串类型（int / float / Vector2 / 自定义 Resource 当 key 等）一律加 typeof 前缀，
	# 避免它们被 str() 后撞进字符串名字空间。
	return "type%d(%s)" % [typeof(key), str(key)]


func _collect_diffs(expected, actual, path: String, violations: Array[String]) -> void:
	if violations.size() >= MAX_REPORTED_VIOLATIONS:
		return
	if expected is Dictionary and actual is Dictionary:
		_collect_dictionary_diffs(expected, actual, path, violations)
		return
	if expected is Array and actual is Array:
		_collect_array_diffs(expected, actual, path, violations)
		return
	if expected != actual:
		violations.append("%s changed from %s to %s" % [path, str(expected), str(actual)])


func _collect_dictionary_diffs(expected: Dictionary, actual: Dictionary, path: String, violations: Array[String]) -> void:
	for key in expected.keys():
		if violations.size() >= MAX_REPORTED_VIOLATIONS:
			return
		var child_path := "%s.%s" % [path, String(key)]
		if not actual.has(key):
			violations.append("%s was removed" % child_path)
			continue
		_collect_diffs(expected.get(key), actual.get(key), child_path, violations)
	for key in actual.keys():
		if violations.size() >= MAX_REPORTED_VIOLATIONS:
			return
		if expected.has(key):
			continue
		violations.append("%s.%s was added with %s" % [path, String(key), str(actual.get(key))])


func _collect_array_diffs(expected: Array, actual: Array, path: String, violations: Array[String]) -> void:
	if expected.size() != actual.size():
		violations.append("%s size changed from %d to %d" % [path, expected.size(), actual.size()])
		return
	for index in range(expected.size()):
		if violations.size() >= MAX_REPORTED_VIOLATIONS:
			return
		_collect_diffs(expected[index], actual[index], "%s[%d]" % [path, index], violations)
