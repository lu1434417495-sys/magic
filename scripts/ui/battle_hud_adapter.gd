## 文件说明：该脚本属于战斗战斗界面适配相关的适配脚本，集中维护队列就绪查找表等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name BattleHudAdapter
extends RefCounted

const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BATTLE_GRID_SERVICE_SCRIPT = preload("res://scripts/systems/battle_grid_service.gd")
const BATTLE_HIT_RESOLVER_SCRIPT = preload("res://scripts/systems/battle_hit_resolver.gd")
const BATTLE_SKILL_RESOLUTION_RULES_SCRIPT = preload("res://scripts/systems/battle_skill_resolution_rules.gd")
const BATTLE_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle_range_service.gd")
const FATE_ATTACK_FORMULA_SCRIPT = preload("res://scripts/systems/fate_attack_formula.gd")
const BattleTerrainRules = preload("res://scripts/systems/battle_terrain_rules.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const QUEUE_ENTRY_LIMIT := 7
const SKILL_GRID_SIZE := 20
const TARGET_SELECTION_MULTI_UNIT := &"multi_unit"

## 字段说明：按键缓存队列就绪查找表，便于在较多对象中快速定位目标并减少重复遍历。
var _queue_ready_lookup: Dictionary = {}
var _grid_service = BATTLE_GRID_SERVICE_SCRIPT.new()
var _hit_resolver = BATTLE_HIT_RESOLVER_SCRIPT.new()
var _skill_resolution_rules = BATTLE_SKILL_RESOLUTION_RULES_SCRIPT.new()


func build_snapshot(
	battle_state: BattleState,
	selected_coord: Vector2i,
	selected_skill_id: StringName = &"",
	selected_skill_name: String = "",
	selected_skill_variant_name: String = "",
	selected_skill_target_coords: Array[Vector2i] = [],
	selected_skill_required_coord_count: int = 0,
	selected_skill_target_unit_ids: Array[StringName] = [],
	selected_skill_variant_id: StringName = &""
) -> Dictionary:
	if battle_state == null:
		return {}

	var active_unit := battle_state.units.get(battle_state.active_unit_id) as BattleUnitState
	var selected_cell := battle_state.cells.get(selected_coord) as BattleCellState
	var selected_unit := _get_unit_at_coord(battle_state, selected_coord)
	var focus_unit := selected_unit if selected_unit != null else active_unit
	var selected_target_count := selected_skill_target_coords.size()
	var selection_info := _build_skill_target_selection_info(
		battle_state,
		active_unit,
		selected_skill_id,
		selected_target_count
	)
	var hit_preview := _build_selected_skill_hit_preview(
		battle_state,
		active_unit,
		selected_coord,
		selected_skill_id,
		selected_skill_target_coords,
		selected_skill_target_unit_ids,
		selected_skill_variant_id
	)
	var fate_preview := _build_selected_skill_fate_preview(
		battle_state,
		active_unit,
		selected_coord,
		selected_skill_id,
		selected_skill_target_coords,
		selected_skill_target_unit_ids,
		selected_skill_variant_id
	)
	var preview_tooltip_text := _build_selected_skill_preview_tooltip(hit_preview, fate_preview)

	return {
		"header_title": "战斗地图",
		"header_subtitle": _build_header_subtitle(battle_state, active_unit),
		"round_badge": _build_round_badge(battle_state),
		"mode_text": _format_control_mode(active_unit.control_mode if active_unit != null else &"manual"),
		"queue_entries": _build_queue_entries(battle_state),
		"focus_unit": _build_focus_unit_snapshot(focus_unit, battle_state),
		"skill_title": _build_skill_title(selected_skill_name, selected_skill_variant_name),
		"skill_subtitle": _build_skill_subtitle(
			active_unit,
			selected_skill_name,
			selected_skill_variant_name,
			selected_target_count,
			selected_skill_required_coord_count,
			selection_info,
			hit_preview
		),
		"skill_slots": _build_skill_slots(active_unit, selected_skill_id),
		"tile_text": _build_tile_text(selected_coord, selected_cell, selected_unit),
		"selected_skill_hit_preview_text": String(hit_preview.get("summary_text", "")),
		"selected_skill_hit_badge_text": _build_selected_skill_hit_badge_text(hit_preview),
		"selected_skill_hit_stage_rates": (hit_preview.get("stage_hit_rates", []) as Array).duplicate(true),
		"selected_skill_fate_preview_text": String(fate_preview.get("summary_text", "")),
		"selected_skill_fate_badges": (fate_preview.get("badges", []) as Array).duplicate(true),
		"selected_skill_preview_tooltip_text": preview_tooltip_text,
		"selected_skill_target_selection_mode": String(selection_info.get("selection_mode", &"single_unit")),
		"selected_skill_target_min_count": int(selection_info.get("min_target_count", 1)),
		"selected_skill_target_max_count": int(selection_info.get("max_target_count", 1)),
		"selected_skill_target_count": selected_target_count,
		"selected_skill_confirm_ready": bool(selection_info.get("confirm_ready", false)),
		"selected_skill_auto_cast_ready": bool(selection_info.get("auto_cast_ready", false)),
	}


func _build_header_subtitle(battle_state: BattleState, active_unit: BattleUnitState) -> String:
	return "阶段 %s  |  友军 %d  |  敌军 %d  |  当前 %s" % [
		_format_phase(battle_state.phase),
		battle_state.ally_unit_ids.size(),
		battle_state.enemy_unit_ids.size(),
		_format_unit_name(active_unit, "无"),
	]


func _build_round_badge(battle_state: BattleState) -> String:
	if battle_state.timeline == null:
		return "TU --\nREADY 0"
	return "TU %d\nREADY %d" % [
		int(battle_state.timeline.current_tu),
		battle_state.timeline.ready_unit_ids.size(),
	]


func _build_queue_entries(battle_state: BattleState) -> Array[Dictionary]:
	var queue_entries: Array[Dictionary] = []
	if battle_state == null:
		return queue_entries

	_queue_ready_lookup.clear()
	if battle_state.timeline != null:
		for unit_id in battle_state.timeline.ready_unit_ids:
			_queue_ready_lookup[unit_id] = true

	var ordered_ids: Array[StringName] = []
	var seen_ids: Dictionary = {}
	if _is_living_unit(battle_state, battle_state.active_unit_id):
		ordered_ids.append(battle_state.active_unit_id)
		seen_ids[battle_state.active_unit_id] = true

	var ready_unit_ids: Array[StringName] = battle_state.timeline.ready_unit_ids if battle_state.timeline != null else []
	for ready_unit_id in ready_unit_ids:
		if seen_ids.has(ready_unit_id) or not _is_living_unit(battle_state, ready_unit_id):
			continue
		ordered_ids.append(ready_unit_id)
		seen_ids[ready_unit_id] = true

	var remaining_units: Array[BattleUnitState] = []
	for unit_variant in battle_state.units.values():
		var unit_state := unit_variant as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		if seen_ids.has(unit_state.unit_id):
			continue
		remaining_units.append(unit_state)
	remaining_units.sort_custom(_queue_candidate_before)

	for unit_state in remaining_units:
		ordered_ids.append(unit_state.unit_id)

	var total_entries := mini(ordered_ids.size(), QUEUE_ENTRY_LIMIT)
	for index in range(total_entries):
		var unit_id := ordered_ids[index]
		var unit_state := battle_state.units.get(unit_id) as BattleUnitState
		if unit_state == null:
			continue
		var portrait_data := _build_portrait_data(unit_state, battle_state)
		queue_entries.append({
			"slot_index": index + 1,
			"name": _format_unit_name(unit_state, "单位"),
			"glyph": portrait_data.get("glyph", "?"),
			"portrait_key": portrait_data.get("portrait_key", ""),
			"primary_color": portrait_data.get("primary_color", Color(0.62, 0.47, 0.32, 1.0)),
			"secondary_color": portrait_data.get("secondary_color", Color(0.2, 0.12, 0.08, 1.0)),
			"edge_color": portrait_data.get("edge_color", Color(0.93, 0.77, 0.5, 1.0)),
			"hp_ratio": _get_ratio(unit_state.current_hp, _get_snapshot_value(unit_state, &"hp_max", 1)),
			"hp_text": "HP %d/%d" % [unit_state.current_hp, _get_snapshot_value(unit_state, &"hp_max", 1)],
			"ap_text": "AP %d / 行动 %d" % [unit_state.current_ap, int(unit_state.current_move_points)],
			"is_active": unit_id == battle_state.active_unit_id,
			"is_ready": _queue_ready_lookup.has(unit_id),
			"is_enemy": battle_state.enemy_unit_ids.has(unit_id),
		})

	if ordered_ids.size() > QUEUE_ENTRY_LIMIT:
		queue_entries.append({
			"is_overflow": true,
			"overflow_text": "+%d" % [ordered_ids.size() - QUEUE_ENTRY_LIMIT],
		})

	return queue_entries


func _build_focus_unit_snapshot(unit_state: BattleUnitState, battle_state: BattleState) -> Dictionary:
	if unit_state == null:
		return {
			"name": "待命",
			"role_text": "未选中单位",
			"resource_info": _build_resource_info(null),
			"glyph": "?",
			"portrait_key": "",
			"primary_color": Color(0.42, 0.3, 0.22, 1.0),
			"secondary_color": Color(0.16, 0.1, 0.07, 1.0),
			"edge_color": Color(0.88, 0.72, 0.48, 1.0),
			"hp_current": 0,
			"hp_max": 1,
			"mp_current": 0,
			"mp_max": 1,
			"ap_current": 0,
			"ap_max": 1,
			"move_current": 0,
			"move_max": BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN,
		}

	var portrait_data := _build_portrait_data(unit_state, battle_state)
	var hp_max := _get_snapshot_value(unit_state, &"hp_max", maxi(unit_state.current_hp, 1))
	var mp_max := _get_snapshot_value(unit_state, &"mp_max", maxi(unit_state.current_mp, 0))
	var ap_max := _get_snapshot_value(unit_state, &"action_points", maxi(unit_state.current_ap, 1))
	var move_max := BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN
	return {
		"name": _format_unit_name(unit_state, "单位"),
		"role_text": _build_focus_role_text(unit_state, battle_state),
		"resource_info": _build_resource_info(unit_state),
		"glyph": portrait_data.get("glyph", "?"),
		"portrait_key": portrait_data.get("portrait_key", ""),
		"primary_color": portrait_data.get("primary_color", Color(0.62, 0.47, 0.32, 1.0)),
		"secondary_color": portrait_data.get("secondary_color", Color(0.2, 0.12, 0.08, 1.0)),
		"edge_color": portrait_data.get("edge_color", Color(0.93, 0.77, 0.5, 1.0)),
		"hp_current": unit_state.current_hp,
		"hp_max": maxi(hp_max, 1),
		"mp_current": unit_state.current_mp,
		"mp_max": maxi(mp_max, 1),
		"ap_current": unit_state.current_ap,
		"ap_max": maxi(ap_max, 1),
		"move_current": int(unit_state.current_move_points),
		"move_max": move_max,
	}


func _build_resource_info(unit_state: BattleUnitState) -> Dictionary:
	var hp_current := int(unit_state.current_hp) if unit_state != null else 0
	var mp_current := int(unit_state.current_mp) if unit_state != null else 0
	var stamina_current := int(unit_state.current_stamina) if unit_state != null else 0
	var aura_current := int(unit_state.current_aura) if unit_state != null else 0
	var ap_current := int(unit_state.current_ap) if unit_state != null else 0
	var move_current := int(unit_state.current_move_points) if unit_state != null else 0
	var hp_max := _get_snapshot_value(unit_state, &"hp_max", maxi(hp_current, 1))
	var mp_max := _get_snapshot_value(unit_state, &"mp_max", maxi(mp_current, 0))
	var stamina_max := _get_snapshot_value(unit_state, &"stamina_max", maxi(stamina_current, 0))
	var aura_max := _get_snapshot_value(unit_state, &"aura_max", maxi(aura_current, 0))
	var ap_max := _get_snapshot_value(unit_state, &"action_points", maxi(ap_current, 1))
	var move_max := BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN
	return {
		"hp": {
			"current": hp_current,
			"max": maxi(hp_max, 1),
			"ratio": _get_ratio(hp_current, hp_max),
			"label": "HP",
		},
		"mp": {
			"current": mp_current,
			"max": maxi(mp_max, 1),
			"ratio": _get_ratio(mp_current, mp_max),
			"label": "MP",
		},
		"stamina": {
			"current": stamina_current,
			"max": maxi(stamina_max, 1),
			"ratio": _get_ratio(stamina_current, stamina_max),
			"label": "ST",
		},
		"aura": {
			"current": aura_current,
			"max": maxi(aura_max, 1),
			"ratio": _get_ratio(aura_current, aura_max),
			"label": "AU",
		},
		"ap": {
			"current": ap_current,
			"max": maxi(ap_max, 1),
			"ratio": _get_ratio(ap_current, ap_max),
			"label": "AP",
		},
		"move": {
			"current": move_current,
			"max": move_max,
			"ratio": _get_ratio(move_current, move_max),
			"label": "MOVE",
		},
	}


func _build_focus_role_text(unit_state: BattleUnitState, battle_state: BattleState) -> String:
	var faction_text := "敌方" if battle_state.enemy_unit_ids.has(unit_state.unit_id) else "我方"
	return "%s  ·  %s  ·  体型 %d" % [
		faction_text,
		_format_control_mode(unit_state.control_mode),
		maxi(unit_state.body_size, 1),
	]


func _build_skill_title(selected_skill_name: String, selected_skill_variant_name: String) -> String:
	if selected_skill_name.is_empty():
		return "技能矩阵"
	if selected_skill_variant_name.is_empty():
		return selected_skill_name
	return "%s · %s" % [selected_skill_name, selected_skill_variant_name]


func _build_skill_subtitle(
	active_unit: BattleUnitState,
	selected_skill_name: String,
	selected_skill_variant_name: String,
	selected_count: int,
	required_count: int,
	selection_info: Dictionary,
	hit_preview: Dictionary = {}
) -> String:
	if active_unit == null:
		return "无可行动单位"
	if selected_skill_name.is_empty():
		return "当前单位 %s  ·  已装备技能 %d" % [
			_format_unit_name(active_unit, "单位"),
			active_unit.known_active_skill_ids.size(),
		]
	if bool(selection_info.get("is_multi_unit", false)):
		var min_target_count := int(selection_info.get("min_target_count", 1))
		var max_target_count := int(selection_info.get("max_target_count", maxi(required_count, 1)))
		if selected_count <= 0:
			return "当前技能 %s  ·  左键逐个点选目标单位" % _build_skill_title(selected_skill_name, selected_skill_variant_name)
		if selected_count < min_target_count:
			return "当前技能 %s  ·  已锁定 %d 个目标，仍未达到最少 %d 个，继续点选" % [
				_build_skill_title(selected_skill_name, selected_skill_variant_name),
				selected_count,
				min_target_count,
			]
		if selected_count < max_target_count:
			return "当前技能 %s  ·  已锁定 %d 个目标，最少 %d / 最多 %d 个，已满足最小数量，可点击自己或空地确认；继续点选将自动施放" % [
				_build_skill_title(selected_skill_name, selected_skill_variant_name),
				selected_count,
				min_target_count,
				max_target_count,
			]
		return "当前技能 %s  ·  已锁定 %d 个目标，已达到上限 %d 个，将自动施放" % [
			_build_skill_title(selected_skill_name, selected_skill_variant_name),
			selected_count,
			max_target_count,
		]
	var hit_preview_text := String(hit_preview.get("summary_text", ""))
	if not hit_preview_text.is_empty():
		return "当前技能 %s  ·  %s" % [
			_build_skill_title(selected_skill_name, selected_skill_variant_name),
			hit_preview_text,
		]
	if required_count <= 1:
		return "当前技能 %s  ·  左键选择目标格释放" % _build_skill_title(selected_skill_name, selected_skill_variant_name)
	return "当前技能 %s  ·  选点 %d/%d" % [
		_build_skill_title(selected_skill_name, selected_skill_variant_name),
		selected_count,
		required_count,
	]


func _build_skill_slots(active_unit: BattleUnitState, selected_skill_id: StringName) -> Array[Dictionary]:
	var skill_slots: Array[Dictionary] = []
	var skill_defs: Dictionary = _get_skill_defs()
	if active_unit != null:
		for index in range(mini(active_unit.known_active_skill_ids.size(), SKILL_GRID_SIZE)):
			var skill_id: StringName = active_unit.known_active_skill_ids[index]
			var skill_def = skill_defs.get(skill_id)
			var display_name := _get_skill_display_name(skill_def, skill_id)
			var icon_key := _get_skill_icon_key(skill_def, skill_id)
			var accent_color := _build_skill_color(icon_key, display_name)
			var slot_state := _build_skill_slot_state(active_unit, skill_def, skill_id)
			skill_slots.append({
				"index": index,
				"is_empty": false,
				"display_name": display_name,
				"short_name": _build_skill_short_name(display_name),
				"hotkey": str(index + 1) if index < 9 else "",
				"footer_text": String(slot_state.get("footer_text", "")),
				"is_selected": skill_id == selected_skill_id,
				"is_disabled": bool(slot_state.get("is_disabled", false)),
				"accent_color": accent_color,
				"accent_dark": accent_color.darkened(0.48),
				"edge_color": accent_color.lightened(0.16),
				"cooldown": int(slot_state.get("cooldown", 0)),
				"disabled_reason": String(slot_state.get("disabled_reason", "")),
			})
	for index in range(skill_slots.size(), SKILL_GRID_SIZE):
		skill_slots.append({
			"index": index,
			"is_empty": true,
		})
	return skill_slots


func _build_skill_slot_state(active_unit: BattleUnitState, skill_def, skill_id: StringName) -> Dictionary:
	var combat_profile = skill_def.combat_profile if skill_def != null else null
	var costs := _get_effective_skill_costs(active_unit, skill_def)
	var ap_cost := int(costs.get("ap_cost", combat_profile.ap_cost if combat_profile != null else 0))
	var mp_cost := int(costs.get("mp_cost", combat_profile.mp_cost if combat_profile != null else 0))
	var stamina_cost := int(costs.get("stamina_cost", combat_profile.stamina_cost if combat_profile != null else 0))
	var aura_cost := int(costs.get("aura_cost", combat_profile.aura_cost if combat_profile != null else 0))
	var cooldown := int(active_unit.cooldowns.get(skill_id, 0)) if active_unit != null else 0
	if cooldown > 0:
		return {
			"footer_text": "CD %d" % cooldown,
			"is_disabled": true,
			"cooldown": cooldown,
			"disabled_reason": "冷却中（%d）" % cooldown,
		}
	if active_unit != null:
		if active_unit.current_ap < ap_cost:
			return {
				"footer_text": "AP不足",
				"is_disabled": true,
				"cooldown": cooldown,
				"disabled_reason": "AP不足",
			}
		if active_unit.current_mp < mp_cost:
			return {
				"footer_text": "MP不足",
				"is_disabled": true,
				"cooldown": cooldown,
				"disabled_reason": "法力不足",
			}
		if active_unit.current_stamina < stamina_cost:
			return {
				"footer_text": "ST不足",
				"is_disabled": true,
				"cooldown": cooldown,
				"disabled_reason": "体力不足",
			}
		if active_unit.current_aura < aura_cost:
			return {
				"footer_text": "AU不足",
				"is_disabled": true,
				"cooldown": cooldown,
				"disabled_reason": "斗气不足",
			}
	return {
		"footer_text": _build_skill_footer(ap_cost, mp_cost, stamina_cost, aura_cost, cooldown),
		"is_disabled": false,
		"cooldown": cooldown,
		"disabled_reason": "",
	}


func _build_skill_footer(ap_cost: int, mp_cost: int, stamina_cost: int, aura_cost: int, cooldown: int) -> String:
	if cooldown > 0:
		return "CD %d" % cooldown
	var parts: PackedStringArray = []
	if ap_cost > 0:
		parts.append("AP %d" % ap_cost)
	if mp_cost > 0:
		parts.append("MP %d" % mp_cost)
	if stamina_cost > 0:
		parts.append("ST %d" % stamina_cost)
	if aura_cost > 0:
		parts.append("AU %d" % aura_cost)
	if not parts.is_empty():
		return " ".join(parts)
	return "READY"


func _build_tile_text(selected_coord: Vector2i, selected_cell: BattleCellState, selected_unit: BattleUnitState) -> String:
	return "地格 %s  ·  %s  ·  高度 %d  ·  占位 %s" % [
		_format_coord(selected_coord),
		_format_terrain_name(selected_cell),
		int(selected_cell.current_height) if selected_cell != null else 0,
		_format_unit_name(selected_unit, "无"),
	]


func _build_portrait_data(unit_state: BattleUnitState, battle_state: BattleState) -> Dictionary:
	var portrait_key := ""
	if unit_state != null and unit_state.source_member_id != &"":
		var member_state = _get_party_member_state(unit_state.source_member_id)
		if member_state != null:
			portrait_key = String(member_state.portrait_id)
	if portrait_key.is_empty() and unit_state != null:
		portrait_key = String(unit_state.unit_id)

	var is_enemy := battle_state.enemy_unit_ids.has(unit_state.unit_id) if battle_state != null and unit_state != null else false
	var palette := _build_portrait_palette(portrait_key, is_enemy)
	return {
		"portrait_key": portrait_key,
		"glyph": _build_unit_glyph(unit_state),
		"primary_color": palette.get("primary_color", Color(0.62, 0.47, 0.32, 1.0)),
		"secondary_color": palette.get("secondary_color", Color(0.2, 0.12, 0.08, 1.0)),
		"edge_color": palette.get("edge_color", Color(0.93, 0.77, 0.5, 1.0)),
	}


func _build_portrait_palette(portrait_key: String, is_enemy: bool) -> Dictionary:
	var normalized_key := portrait_key.to_lower()
	if normalized_key.contains("sword"):
		return {
			"primary_color": Color(0.28, 0.55, 0.85, 1.0),
			"secondary_color": Color(0.1, 0.18, 0.32, 1.0),
			"edge_color": Color(0.96, 0.83, 0.54, 1.0),
		}
	if normalized_key.contains("axe"):
		return {
			"primary_color": Color(0.78, 0.34, 0.22, 1.0),
			"secondary_color": Color(0.28, 0.09, 0.05, 1.0),
			"edge_color": Color(0.98, 0.77, 0.44, 1.0),
		}
	if normalized_key.contains("spear"):
		return {
			"primary_color": Color(0.24, 0.72, 0.53, 1.0),
			"secondary_color": Color(0.07, 0.2, 0.14, 1.0),
			"edge_color": Color(0.96, 0.85, 0.52, 1.0),
		}

	var hash_value: int = abs(normalized_key.hash())
	var hue: float = float(hash_value % 360) / 360.0
	var base: Color = Color.from_hsv(hue, 0.72 if is_enemy else 0.46, 0.82 if is_enemy else 0.88, 1.0)
	return {
		"primary_color": base,
		"secondary_color": base.darkened(0.62),
		"edge_color": Color(0.9, 0.46, 0.3, 1.0) if is_enemy else Color(0.94, 0.79, 0.5, 1.0),
	}


func _build_skill_color(icon_key: String, display_name: String) -> Color:
	var normalized_key := icon_key.to_lower()
	if normalized_key.contains("sword"):
		return Color(0.98, 0.84, 0.36, 1.0)
	if normalized_key.contains("axe"):
		return Color(0.96, 0.42, 0.24, 1.0)
	if normalized_key.contains("spear"):
		return Color(0.34, 0.82, 0.7, 1.0)
	if normalized_key.contains("charge"):
		return Color(0.99, 0.67, 0.19, 1.0)
	if normalized_key.contains("mud") or normalized_key.contains("fossil"):
		return Color(0.78, 0.58, 0.28, 1.0)

	var hash_source: String = "%s_%s" % [icon_key, display_name]
	var hash_value: int = abs(hash_source.hash())
	var hue: float = float(hash_value % 360) / 360.0
	return Color.from_hsv(hue, 0.7, 0.92, 1.0)


func _build_unit_glyph(unit_state: BattleUnitState) -> String:
	if unit_state == null:
		return "?"
	var display_name := _format_unit_name(unit_state, "?")
	return display_name.substr(0, 1)


func _build_skill_short_name(display_name: String) -> String:
	if display_name.is_empty():
		return "--"
	return display_name.substr(0, mini(display_name.length(), 2))


func _get_skill_display_name(skill_def, skill_id: StringName) -> String:
	if skill_def != null and not String(skill_def.display_name).is_empty():
		return String(skill_def.display_name)
	return String(skill_id)


func _get_skill_icon_key(skill_def, skill_id: StringName) -> String:
	if skill_def != null and skill_def.icon_id != &"":
		return String(skill_def.icon_id)
	return String(skill_id)


func _get_skill_defs() -> Dictionary:
	var session = _get_game_session()
	return session.call("get_skill_defs") if session != null else {}


func _build_selected_skill_hit_preview(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	selected_coord: Vector2i,
	selected_skill_id: StringName,
	selected_skill_target_coords: Array[Vector2i],
	selected_skill_target_unit_ids: Array[StringName],
	selected_skill_variant_id: StringName
) -> Dictionary:
	if battle_state == null or active_unit == null or selected_skill_id == &"":
		return {}
	var skill_def = _get_skill_defs().get(selected_skill_id)
	if skill_def == null or skill_def.combat_profile == null:
		return {}
	var target_unit := _resolve_selected_skill_preview_target_unit(
		battle_state,
		active_unit,
		selected_coord,
		selected_skill_target_coords,
		selected_skill_target_unit_ids,
		skill_def
	)
	if target_unit == null:
		return {}
	var resolution_policy := _skill_resolution_rules.build_skill_resolution_policy(
		skill_def,
		active_unit,
		selected_skill_variant_id,
		selected_skill_target_unit_ids,
		target_unit
	)
	if not bool(resolution_policy.get("routes_to_unit_targeting", false)):
		return {}
	var effect_defs_variant = resolution_policy.get("effect_defs", [])
	var effect_defs: Array[CombatEffectDef] = []
	if effect_defs_variant is Array:
		for effect_def_variant in effect_defs_variant:
			var effect_def := effect_def_variant as CombatEffectDef
			if effect_def != null:
				effect_defs.append(effect_def)
	var repeat_attack_effect := _skill_resolution_rules.find_repeat_attack_effect(effect_defs)
	if repeat_attack_effect == null:
		if not bool(resolution_policy.get("uses_fate_attack", false)):
			return {}
		return _hit_resolver.build_skill_attack_preview(
			battle_state,
			active_unit,
			target_unit,
			skill_def,
			bool(resolution_policy.get("force_hit_no_crit", false))
		)
	return _hit_resolver.build_repeat_attack_preview(
		battle_state,
		active_unit,
		target_unit,
		skill_def,
		repeat_attack_effect
	)


func _build_selected_skill_fate_preview(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	selected_coord: Vector2i,
	selected_skill_id: StringName,
	selected_skill_target_coords: Array[Vector2i],
	selected_skill_target_unit_ids: Array[StringName],
	selected_skill_variant_id: StringName
) -> Dictionary:
	if battle_state == null or active_unit == null or selected_skill_id == &"":
		return {}
	var skill_def = _get_skill_defs().get(selected_skill_id)
	if skill_def == null or skill_def.combat_profile == null:
		return {}
	var target_unit := _resolve_selected_skill_preview_target_unit(
		battle_state,
		active_unit,
		selected_coord,
		selected_skill_target_coords,
		selected_skill_target_unit_ids,
		skill_def
	)
	if target_unit == null:
		return {}
	var resolution_policy := _skill_resolution_rules.build_skill_resolution_policy(
		skill_def,
		active_unit,
		selected_skill_variant_id,
		selected_skill_target_unit_ids,
		target_unit
	)
	if not bool(resolution_policy.get("uses_fate_attack", false)):
		return {}
	var preview_mode := StringName(resolution_policy.get("fate_preview_mode", &""))
	if preview_mode == BATTLE_SKILL_RESOLUTION_RULES_SCRIPT.FATE_PREVIEW_MODE_FORCE_HIT_NO_CRIT:
		return _build_force_hit_no_crit_fate_preview()
	return _build_standard_fate_preview(battle_state, active_unit, target_unit)


func _build_standard_fate_preview(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	target_unit: BattleUnitState
) -> Dictionary:
	if battle_state == null or active_unit == null or target_unit == null:
		return {}

	var effective_luck := _get_effective_luck(active_unit)
	var is_disadvantage := battle_state.is_attack_disadvantage(active_unit, target_unit)
	var crit_gate_die := FATE_ATTACK_FORMULA_SCRIPT.calc_crit_gate_die_size(effective_luck, is_disadvantage)
	var fumble_low_end := FATE_ATTACK_FORMULA_SCRIPT.calc_fumble_low_end(effective_luck)
	var crit_threshold := FATE_ATTACK_FORMULA_SCRIPT.calc_crit_threshold(
		_get_hidden_luck_at_birth(active_unit),
		_get_faith_luck_bonus(active_unit)
	)
	var mercy_active := effective_luck <= -5 and is_disadvantage
	var badges: Array[Dictionary] = [
		{
			"text": "劣势" if is_disadvantage else "未陷劣势",
			"tone": &"warning" if is_disadvantage else &"calm",
			"tooltip_text": "当前命中与命运骰按%s口径结算。" % ("劣势取低" if is_disadvantage else "正常单骰"),
		},
		{
			"text": "暴击门 d%d" % crit_gate_die,
			"tone": &"gate",
			"tooltip_text": "命运暴击门尺寸：d%d。" % crit_gate_die,
		},
		{
			"text": "大失败 1" if fumble_low_end <= 1 else "大失败 1-%d" % fumble_low_end,
			"tone": &"danger",
			"tooltip_text": "当前大失败区间：1-%d。" % fumble_low_end,
		},
	]
	var detail_lines: PackedStringArray = [
		"命运判定概览",
		"状态：%s" % ("劣势中" if is_disadvantage else "未陷劣势"),
		"暴击门：d%d" % crit_gate_die,
		"大失败：1-%d" % fumble_low_end,
	]
	if crit_gate_die == 20:
		var high_threat_text := "高位大成功 %d-20" % crit_threshold
		badges.append({
			"text": high_threat_text,
			"tone": &"high_threat",
			"tooltip_text": "当前高位大成功区间：%d-20。" % crit_threshold,
		})
		detail_lines.append("高位大成功：%d-20" % crit_threshold)
	if mercy_active:
		badges.append({
			"text": "命运的怜悯",
			"tone": &"mercy",
			"tooltip_text": "effective_luck<=-5 且处于劣势时，暴击门只额外放大一档。",
		})
		detail_lines.append("命运的怜悯：已生效")

	return {
		"summary_text": _build_fate_preview_summary_text(badges),
		"tooltip_text": "\n".join(detail_lines),
		"badges": badges,
		"is_disadvantage": is_disadvantage,
		"effective_luck": effective_luck,
		"crit_gate_die": crit_gate_die,
		"fumble_low_end": fumble_low_end,
		"crit_threshold": crit_threshold,
		"mercy_active": mercy_active,
	}


func _build_force_hit_no_crit_fate_preview() -> Dictionary:
	var badges: Array[Dictionary] = [
		{
			"text": "必定命中",
			"tone": &"calm",
			"tooltip_text": "这次攻击不会再进行命中骰判定，直接视为命中。",
		},
		{
			"text": "禁暴击",
			"tone": &"warning",
			"tooltip_text": "这次攻击不会触发暴击。",
		},
		{
			"text": "摆幅压低",
			"tone": &"gate",
			"tooltip_text": "这次攻击的命运摆幅已被压低，不再展示标准 crit/fumble 区间。",
		},
	]
	return {
		"summary_text": _build_fate_preview_summary_text(badges),
		"tooltip_text": "命运判定概览\n状态：强制命中\n暴击：已封锁\n说明：这次攻击不再走标准命中/暴击/大失败骰。",
		"badges": badges,
		"force_hit_no_crit": true,
	}


func _resolve_selected_skill_preview_target_unit(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	selected_coord: Vector2i,
	selected_skill_target_coords: Array[Vector2i],
	selected_skill_target_unit_ids: Array[StringName],
	skill_def
) -> BattleUnitState:
	for target_unit_id in selected_skill_target_unit_ids:
		var queued_target := battle_state.units.get(target_unit_id) as BattleUnitState
		if _can_preview_skill_target_unit(active_unit, queued_target, skill_def):
			return queued_target
	for target_coord in selected_skill_target_coords:
		var queued_coord_target := _get_unit_at_coord(battle_state, target_coord)
		if _can_preview_skill_target_unit(active_unit, queued_coord_target, skill_def):
			return queued_coord_target
	var focused_target := _get_unit_at_coord(battle_state, selected_coord)
	if _can_preview_skill_target_unit(active_unit, focused_target, skill_def):
		return focused_target
	return null


func _can_preview_skill_target_unit(active_unit: BattleUnitState, target_unit: BattleUnitState, skill_def) -> bool:
	if active_unit == null or target_unit == null or skill_def == null or skill_def.combat_profile == null:
		return false
	if not target_unit.is_alive:
		return false
	if target_unit.unit_id == active_unit.unit_id:
		return false
	if not _get_skill_cast_block_reason(active_unit, skill_def).is_empty():
		return false
	if not _skill_target_filter_matches_unit(active_unit, target_unit, skill_def.combat_profile.target_team_filter):
		return false
	return _grid_service.get_distance_between_units(active_unit, target_unit) <= _get_effective_skill_range(active_unit, skill_def)


func _build_fate_preview_summary_text(badges: Array[Dictionary]) -> String:
	var parts: PackedStringArray = []
	for badge in badges:
		parts.append(String(badge.get("text", "")))
	return "  ·  ".join(parts)


func _build_selected_skill_preview_tooltip(hit_preview: Dictionary, fate_preview: Dictionary) -> String:
	var sections: PackedStringArray = []
	var hit_preview_text := String(hit_preview.get("summary_text", ""))
	if not hit_preview_text.is_empty():
		sections.append(hit_preview_text)
	var fate_tooltip_text := String(fate_preview.get("tooltip_text", ""))
	if not fate_tooltip_text.is_empty():
		sections.append(fate_tooltip_text)
	return "\n\n".join(sections)


func _build_selected_skill_hit_badge_text(hit_preview: Dictionary) -> String:
	if hit_preview.is_empty():
		return ""
	var success_rate := int(hit_preview.get("success_rate_percent", -1))
	if success_rate < 0:
		success_rate = int(hit_preview.get("hit_rate_percent", -1))
	if success_rate < 0:
		var stage_rates := hit_preview.get("stage_hit_rates", []) as Array
		if not stage_rates.is_empty():
			success_rate = int(stage_rates[0])
	if success_rate < 0:
		return ""
	return "命中 %d%%" % clampi(success_rate, 0, 100)


func _get_hidden_luck_at_birth(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return int(unit_state.attribute_snapshot.get_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH))


func _get_faith_luck_bonus(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return int(unit_state.attribute_snapshot.get_value(UNIT_BASE_ATTRIBUTES_SCRIPT.FAITH_LUCK_BONUS))


func _get_effective_luck(unit_state: BattleUnitState) -> int:
	return clampi(
		_get_hidden_luck_at_birth(unit_state) + _get_faith_luck_bonus(unit_state),
		UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MIN,
		UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MAX
	)


func _build_skill_target_selection_info(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	selected_skill_id: StringName,
	selected_count: int
) -> Dictionary:
	var default_info := {
		"selection_mode": &"single_unit",
		"is_multi_unit": false,
		"min_target_count": 1,
		"max_target_count": maxi(selected_count, 1),
		"confirm_ready": false,
		"auto_cast_ready": false,
	}
	if battle_state == null or active_unit == null or selected_skill_id == &"":
		return default_info
	var skill_def = _get_skill_defs().get(selected_skill_id)
	if skill_def == null or skill_def.combat_profile == null:
		return default_info
	var combat_profile = skill_def.combat_profile
	var selection_mode := StringName(combat_profile.target_selection_mode)
	if selection_mode == &"":
		selection_mode = &"single_unit"
	var min_target_count := maxi(int(combat_profile.min_target_count), 1)
	var max_target_count := maxi(int(combat_profile.max_target_count), min_target_count)
	var is_multi_unit := selection_mode == TARGET_SELECTION_MULTI_UNIT
	var confirm_ready := is_multi_unit and selected_count >= min_target_count and selected_count < max_target_count
	var auto_cast_ready := is_multi_unit and selected_count >= max_target_count
	return {
		"selection_mode": selection_mode,
		"is_multi_unit": is_multi_unit,
		"min_target_count": min_target_count,
		"max_target_count": max_target_count,
		"confirm_ready": confirm_ready,
		"auto_cast_ready": auto_cast_ready,
	}


func _get_skill_cast_block_reason(active_unit: BattleUnitState, skill_def) -> String:
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return "技能或目标无效。"
	var combat_profile = skill_def.combat_profile
	var costs := _get_effective_skill_costs(active_unit, skill_def)
	var cooldown := int(active_unit.cooldowns.get(skill_def.skill_id, 0))
	if cooldown > 0:
		return "%s 仍在冷却中（%d）。" % [skill_def.display_name, cooldown]
	if active_unit.current_ap < int(costs.get("ap_cost", combat_profile.ap_cost)):
		return "AP不足，无法施放该技能。"
	if active_unit.current_mp < int(costs.get("mp_cost", combat_profile.mp_cost)):
		return "法力不足，无法施放该技能。"
	if active_unit.current_stamina < int(costs.get("stamina_cost", combat_profile.stamina_cost)):
		return "体力不足，无法施放该技能。"
	if active_unit.current_aura < int(costs.get("aura_cost", combat_profile.aura_cost)):
		return "斗气不足，无法施放该技能。"
	return ""


func _get_effective_skill_costs(active_unit: BattleUnitState, skill_def) -> Dictionary:
	if skill_def == null or skill_def.combat_profile == null:
		return {}
	var skill_level := _get_unit_skill_level(active_unit, skill_def.skill_id)
	return skill_def.combat_profile.get_effective_resource_costs(skill_level)


func _get_unit_skill_level(active_unit: BattleUnitState, skill_id: StringName) -> int:
	if active_unit == null or skill_id == &"":
		return 0
	if active_unit.known_skill_level_map.has(skill_id):
		return int(active_unit.known_skill_level_map.get(skill_id, 0))
	return 1 if active_unit.known_active_skill_ids.has(skill_id) else 0


func _skill_target_filter_matches_unit(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	target_team_filter: StringName
) -> bool:
	if active_unit == null or target_unit == null:
		return false
	var is_same_unit := active_unit.unit_id == target_unit.unit_id
	var is_same_faction := String(active_unit.faction_id) == String(target_unit.faction_id)
	match target_team_filter:
		&"enemy":
			return not is_same_faction
		&"ally":
			return is_same_faction
		&"self":
			return is_same_unit
		&"", &"any":
			return true
		_:
			return true


func _get_effective_skill_range(active_unit: BattleUnitState, skill_def) -> int:
	return BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_range(active_unit, skill_def)


func _resolve_base_skill_range(active_unit: BattleUnitState, skill_def) -> int:
	return BATTLE_RANGE_SERVICE_SCRIPT.resolve_base_skill_range(active_unit, skill_def)


func _is_weapon_range_skill(skill_def) -> bool:
	return BATTLE_RANGE_SERVICE_SCRIPT.is_weapon_range_skill(skill_def)


func _get_weapon_attack_range(active_unit: BattleUnitState) -> int:
	return BATTLE_RANGE_SERVICE_SCRIPT.get_weapon_attack_range(active_unit)


func _skill_has_tag(skill_def, expected_tag: StringName) -> bool:
	if skill_def == null or expected_tag == &"":
		return false
	for tag in skill_def.tags:
		if ProgressionDataUtils.to_string_name(tag) == expected_tag:
			return true
	return false


func _get_party_member_state(member_id: StringName):
	var session = _get_game_session()
	if session == null or member_id == &"":
		return null
	return session.call("get_party_member_state", member_id)


func _get_game_session():
	var main_loop := Engine.get_main_loop()
	if main_loop is SceneTree:
		var scene_tree := main_loop as SceneTree
		return scene_tree.root.get_node_or_null("GameSession")
	return null


func _queue_candidate_before(a: BattleUnitState, b: BattleUnitState) -> bool:
	if a == null:
		return false
	if b == null:
		return true

	var a_ready := _queue_ready_lookup.has(a.unit_id)
	var b_ready := _queue_ready_lookup.has(b.unit_id)
	if a_ready != b_ready:
		return a_ready and not b_ready
	if a.action_progress != b.action_progress:
		return a.action_progress > b.action_progress
	if a.current_ap != b.current_ap:
		return a.current_ap > b.current_ap
	return String(a.unit_id) < String(b.unit_id)


func _is_living_unit(battle_state: BattleState, unit_id: StringName) -> bool:
	if battle_state == null or unit_id == &"":
		return false
	var unit_state := battle_state.units.get(unit_id) as BattleUnitState
	return unit_state != null and unit_state.is_alive


func _get_snapshot_value(unit_state: BattleUnitState, attribute_id: StringName, fallback: int) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return fallback
	return int(unit_state.attribute_snapshot.get_value(attribute_id))


func _get_ratio(current_value: int, max_value: int) -> float:
	return clampf(float(current_value) / float(maxi(max_value, 1)), 0.0, 1.0)


func _get_unit_at_coord(battle_state: BattleState, coord: Vector2i) -> BattleUnitState:
	if battle_state == null:
		return null
	for unit_variant in battle_state.units.values():
		var unit_state := unit_variant as BattleUnitState
		if unit_state != null and unit_state.is_alive and unit_state.occupies_coord(coord):
			return unit_state
	return null


func _format_phase(phase: StringName) -> String:
	var phase_text := String(phase)
	if phase_text.is_empty():
		return "无"
	return phase_text.capitalize().replace("_", " ")


func _format_control_mode(control_mode: StringName) -> String:
	match control_mode:
		&"manual":
			return "手动"
		&"ai":
			return "自动"
		_:
			return String(control_mode) if control_mode != &"" else "手动"


func _format_coord(coord: Vector2i) -> String:
	return "(%d, %d)" % [coord.x, coord.y]


func _format_unit_name(unit_state: BattleUnitState, fallback_text: String) -> String:
	if unit_state == null:
		return fallback_text
	if not unit_state.display_name.is_empty():
		return unit_state.display_name
	return String(unit_state.unit_id)


func _format_terrain_name(cell: BattleCellState) -> String:
	if cell == null:
		return "无"
	return BattleTerrainRules.get_display_name(cell.base_terrain)
