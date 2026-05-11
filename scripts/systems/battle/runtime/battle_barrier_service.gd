class_name BattleBarrierService
extends RefCounted

const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleSaveResolver = preload("res://scripts/systems/battle/rules/battle_save_resolver.gd")
const BattleEffectCategoryResolver = preload("res://scripts/systems/battle/rules/battle_effect_category_resolver.gd")
const BattleBarrierGeometryService = preload("res://scripts/systems/battle/runtime/battle_barrier_geometry_service.gd")
const BattleBarrierOutcomeResolver = preload("res://scripts/systems/battle/runtime/battle_barrier_outcome_resolver.gd")
const BattleBarrierInstanceState = preload("res://scripts/systems/battle/core/battle_barrier_instance_state.gd")
const BarrierContentRegistry = preload("res://scripts/player/progression/barrier_content_registry.gd")
const BarrierProfileDef = preload("res://scripts/player/progression/barrier_profile_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const DEFAULT_DURATION_TU := 120
const DEFAULT_SAVE_DC := 16

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null

var _content_registry := BarrierContentRegistry.new()
var _category_resolver := BattleEffectCategoryResolver.new()
var _geometry_service := BattleBarrierGeometryService.new()
var _outcome_resolver := BattleBarrierOutcomeResolver.new()


func setup(runtime) -> void:
	_runtime = runtime
	_outcome_resolver.setup(runtime)


func dispose() -> void:
	_outcome_resolver.dispose()
	_runtime = null


func apply_layered_barrier_effect(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_def: CombatEffectDef,
	batch: BattleEventBatch
) -> Dictionary:
	var result := {
		"applied": false,
		"barrier_instance_id": &"",
		"log_lines": [],
	}
	if _runtime == null or _runtime._state == null or source_unit == null or effect_def == null:
		return result
	var params: Dictionary = effect_def.params.duplicate(true) if effect_def.params != null else {}
	var profile_id := ProgressionDataUtils.to_string_name(params.get("profile_id", ""))
	var profile := _content_registry.get_profile_def(profile_id)
	if profile == null:
		return result

	var anchor_unit := target_unit if target_unit != null else source_unit
	var radius_cells := maxi(int(params.get("radius_cells", profile.radius_cells)), 1)
	var area_pattern := ProgressionDataUtils.to_string_name(params.get("area_pattern", profile.area_pattern))
	if area_pattern == &"":
		area_pattern = profile.area_pattern
	var duration_tu := int(effect_def.duration_tu)
	if duration_tu <= 0:
		duration_tu = maxi(int(params.get("duration_tu", profile.duration_tu)), 0)
	if duration_tu <= 0:
		duration_tu = DEFAULT_DURATION_TU
	var save_dc := _resolve_barrier_save_dc(source_unit, effect_def, params)
	var instance_id := _build_barrier_instance_id(source_unit, skill_def, profile)
	var instance := BattleBarrierInstanceState.new()
	instance.barrier_instance_id = instance_id
	instance.profile_id = profile.profile_id
	instance.display_name = profile.display_name
	instance.source_unit_id = source_unit.unit_id
	instance.source_skill_id = skill_def.skill_id if skill_def != null else &""
	instance.anchor_mode = profile.anchor_mode
	instance.anchor_coord = anchor_unit.coord
	instance.radius_cells = radius_cells
	instance.area_pattern = area_pattern
	instance.remaining_tu = duration_tu
	instance.created_tu = _get_current_tu()
	instance.save_dc = save_dc
	instance.catch_all_projected_effects = profile.catch_all_projected_effects
	instance.layers = _build_layers(profile, save_dc)

	var barrier := instance.to_runtime_dict()
	_get_barrier_store()[instance_id] = barrier
	_append_changed_coords(batch, _get_barrier_coords(barrier))
	var line := "%s 创造%s，固定在 (%d, %d)，半径 %d 格。" % [
		source_unit.display_name,
		_get_barrier_label(barrier),
		anchor_unit.coord.x,
		anchor_unit.coord.y,
		radius_cells,
	]
	_append_log(batch, line)
	result["applied"] = true
	result["barrier_instance_id"] = instance_id
	result["log_lines"] = [line]
	return result


func advance_barrier_durations(elapsed_tu: int, batch: BattleEventBatch) -> void:
	if _runtime == null or _runtime._state == null or elapsed_tu <= 0:
		return
	var store := _get_barrier_store()
	var expired_ids: Array[StringName] = []
	for barrier_key in _sorted_barrier_keys():
		var barrier: Dictionary = store.get(barrier_key, {})
		if barrier.is_empty():
			continue
		var remaining := int(barrier.get("remaining_tu", 0)) - elapsed_tu
		barrier["remaining_tu"] = remaining
		store[barrier_key] = barrier
		if remaining <= 0:
			expired_ids.append(barrier_key)
	for barrier_id in expired_ids:
		var barrier: Dictionary = store.get(barrier_id, {})
		_append_changed_coords(batch, _get_barrier_coords(barrier))
		store.erase(barrier_id)
		_append_log(batch, "%s %s 消散。" % [_get_barrier_label(barrier), String(barrier_id)])


func resolve_unit_boundary_crossing(
	unit_state: BattleUnitState,
	from_coord: Vector2i,
	to_coord: Vector2i,
	batch: BattleEventBatch
) -> Dictionary:
	var result := {
		"blocked": false,
		"applied": false,
	}
	if _runtime == null or _runtime._state == null or unit_state == null or not unit_state.is_alive:
		return result
	for barrier_key in _sorted_barrier_keys():
		var barrier: Dictionary = _get_barrier_store().get(barrier_key, {})
		if barrier.is_empty() or _is_barrier_creator(unit_state, barrier):
			continue
		var barrier_coords := _get_barrier_coords(barrier)
		var from_footprint: Array = _runtime._grid_service.get_footprint_coords(from_coord, unit_state.footprint_size)
		var to_footprint: Array = _runtime._grid_service.get_footprint_coords(to_coord, unit_state.footprint_size)
		var transition: Dictionary = _geometry_service.classify_footprint_transition(_runtime._state, from_footprint, to_footprint, barrier_coords)
		if not bool(transition.get("crosses_boundary", false)):
			continue
		var passage_result := _apply_barrier_passage(unit_state, barrier, batch)
		result["applied"] = bool(result["applied"]) or bool(passage_result.get("applied", false))
		if bool(passage_result.get("stopped", false)):
			result["blocked"] = true
			return result
	return result


func resolve_skill_barrier_interaction(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch
) -> Dictionary:
	if source_unit == null or target_unit == null:
		return {"blocked": false, "applied": false}
	return _resolve_projected_effect_barrier_interaction(
		source_unit,
		target_unit.coord,
		target_unit.display_name,
		skill_def,
		effect_defs,
		batch
	)


func resolve_ground_barrier_interaction(
	source_unit: BattleUnitState,
	target_coord: Vector2i,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch
) -> Dictionary:
	return _resolve_projected_effect_barrier_interaction(
		source_unit,
		target_coord,
		"(%d, %d)" % [target_coord.x, target_coord.y],
		skill_def,
		effect_defs,
		batch
	)


func _resolve_projected_effect_barrier_interaction(
	source_unit: BattleUnitState,
	target_coord: Vector2i,
	target_label: String,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch
) -> Dictionary:
	var result := {
		"blocked": false,
		"applied": false,
	}
	if _runtime == null or _runtime._state == null or source_unit == null:
		return result
	for barrier_key in _sorted_barrier_keys():
		var barrier: Dictionary = _get_barrier_store().get(barrier_key, {})
		if barrier.is_empty():
			continue
		if not _projected_effect_crosses_barrier(source_unit.coord, target_coord, barrier):
			continue
		var active_layer := _get_active_layer(barrier)
		if active_layer.is_empty():
			continue
		if _skill_breaks_layer(skill_def, active_layer):
			_break_active_layer(barrier_key, barrier, active_layer, batch)
			result["blocked"] = true
			result["applied"] = true
			return result
		if _skill_breaks_any_remaining_layer(skill_def, barrier):
			_append_log(batch, "%s 试图破解%s，但必须先处理外层 %s。" % [
				source_unit.display_name,
				_get_barrier_label(barrier),
				_get_layer_label(active_layer),
			])
			result["blocked"] = true
			result["applied"] = true
			return result
		var categories: Array[StringName] = _category_resolver.resolve_categories(skill_def, effect_defs)
		var blocking_layer := _find_first_blocking_layer(barrier, categories)
		if blocking_layer.is_empty() and bool(barrier.get("catch_all_projected_effects", false)):
			blocking_layer = active_layer
		if blocking_layer.is_empty():
			continue
		_append_log(batch, "%s 的 %s 被%s的 %s 阻挡，无法影响 %s。" % [
			source_unit.display_name,
			skill_def.display_name if skill_def != null else "效果",
			_get_barrier_label(barrier),
			_get_layer_label(blocking_layer),
			target_label,
		])
		result["blocked"] = true
		result["applied"] = true
		return result
	return result


func _apply_barrier_passage(unit_state: BattleUnitState, barrier: Dictionary, batch: BattleEventBatch) -> Dictionary:
	var result := {
		"applied": false,
		"stopped": false,
	}
	if unit_state == null or barrier.is_empty():
		return result
	_append_log(batch, "%s 穿过%s，依次承受未破除的色层。" % [unit_state.display_name, _get_barrier_label(barrier)])
	for layer_variant in barrier.get("layers", []):
		var layer: Dictionary = layer_variant if layer_variant is Dictionary else {}
		if layer.is_empty() or bool(layer.get("broken", false)):
			continue
		var layer_result := _outcome_resolver.apply_passage_outcomes(unit_state, barrier, layer, batch)
		result["applied"] = true
		if bool(layer_result.get("stopped", false)) or not unit_state.is_alive:
			result["stopped"] = true
			return result
	return result


func _break_active_layer(
	barrier_key: StringName,
	barrier: Dictionary,
	active_layer: Dictionary,
	batch: BattleEventBatch
) -> void:
	var layer_id := ProgressionDataUtils.to_string_name(active_layer.get("layer_id", ""))
	var layers: Array = barrier.get("layers", [])
	for index in range(layers.size()):
		var layer: Dictionary = layers[index] if layers[index] is Dictionary else {}
		if ProgressionDataUtils.to_string_name(layer.get("layer_id", "")) != layer_id:
			continue
		layer["broken"] = true
		layers[index] = layer
		break
	barrier["layers"] = layers
	_get_barrier_store()[barrier_key] = barrier
	_append_changed_coords(batch, _get_barrier_coords(barrier))
	_append_log(batch, "%s 的 %s 被破解。" % [_get_barrier_label(barrier), _get_layer_label(active_layer)])


func _resolve_barrier_save_dc(source_unit: BattleUnitState, effect_def: CombatEffectDef, params: Dictionary) -> int:
	var resolved_dc := BattleSaveResolver._resolve_save_dc(source_unit, effect_def)
	if resolved_dc > 0:
		return resolved_dc
	var param_dc := int(params.get("save_dc", DEFAULT_SAVE_DC))
	return maxi(param_dc, 1)


func _build_barrier_instance_id(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	profile: BarrierProfileDef
) -> StringName:
	var source_id := String(source_unit.unit_id if source_unit != null else "unknown")
	var skill_id := String(skill_def.skill_id if skill_def != null else profile.profile_id)
	return StringName("%s:%s:%d:%d" % [
		skill_id,
		source_id,
		_get_current_tu(),
		_get_barrier_store().size() + 1,
	])


func _build_layers(profile: BarrierProfileDef, save_dc: int) -> Array[Dictionary]:
	var layers: Array[Dictionary] = []
	for layer_def in profile.get_ordered_layers():
		if layer_def == null:
			continue
		layers.append(layer_def.to_runtime_dict(save_dc))
	return layers


func _skill_breaks_layer(skill_def: SkillDef, layer: Dictionary) -> bool:
	if skill_def == null:
		return false
	for raw_id in layer.get("breaker_skill_ids", []):
		if ProgressionDataUtils.to_string_name(raw_id) == skill_def.skill_id:
			return true
	return false


func _skill_breaks_any_remaining_layer(skill_def: SkillDef, barrier: Dictionary) -> bool:
	if skill_def == null:
		return false
	for layer_variant in barrier.get("layers", []):
		var layer: Dictionary = layer_variant if layer_variant is Dictionary else {}
		if layer.is_empty() or bool(layer.get("broken", false)):
			continue
		if _skill_breaks_layer(skill_def, layer):
			return true
	return false


func _find_first_blocking_layer(barrier: Dictionary, categories: Array[StringName]) -> Dictionary:
	var category_lookup: Dictionary = {}
	for category in categories:
		category_lookup[category] = true
	for layer_variant in barrier.get("layers", []):
		var layer: Dictionary = layer_variant if layer_variant is Dictionary else {}
		if layer.is_empty() or bool(layer.get("broken", false)):
			continue
		for raw_category in layer.get("blocked_categories", []):
			var category := ProgressionDataUtils.to_string_name(raw_category)
			if category_lookup.has(category):
				return layer
	return {}


func _get_active_layer(barrier: Dictionary) -> Dictionary:
	for layer_variant in barrier.get("layers", []):
		var layer: Dictionary = layer_variant if layer_variant is Dictionary else {}
		if not layer.is_empty() and not bool(layer.get("broken", false)):
			return layer
	return {}


func _projected_effect_crosses_barrier(source_coord: Vector2i, target_coord: Vector2i, barrier: Dictionary) -> bool:
	return _geometry_service.line_crosses_barrier_area(
		_runtime._state if _runtime != null else null,
		source_coord,
		target_coord,
		_get_barrier_coords(barrier)
	)


func _is_coord_inside_barrier(coord: Vector2i, barrier: Dictionary) -> bool:
	return _geometry_service.coord_inside_barrier(coord, _get_barrier_coords(barrier))


func _get_barrier_coords(barrier: Dictionary) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	if _runtime == null or _runtime._state == null or barrier.is_empty():
		return coords
	var anchor: Vector2i = barrier.get("anchor_coord", Vector2i.ZERO)
	var pattern := ProgressionDataUtils.to_string_name(barrier.get("area_pattern", "diamond"))
	var radius := maxi(int(barrier.get("radius_cells", 0)), 0)
	return _runtime._grid_service.get_area_coords(_runtime._state, anchor, pattern, radius)


func _is_barrier_creator(unit_state: BattleUnitState, barrier: Dictionary) -> bool:
	return unit_state != null and unit_state.unit_id == ProgressionDataUtils.to_string_name(barrier.get("source_unit_id", ""))


func _get_barrier_store() -> Dictionary:
	if _runtime == null or _runtime._state == null:
		return {}
	if _runtime._state.layered_barrier_fields == null:
		_runtime._state.layered_barrier_fields = {}
	return _runtime._state.layered_barrier_fields


func _sorted_barrier_keys() -> Array[StringName]:
	var keys: Array[StringName] = []
	for key_text in ProgressionDataUtils.sorted_string_keys(_get_barrier_store()):
		keys.append(StringName(key_text))
	return keys


func _get_current_tu() -> int:
	if _runtime == null or _runtime._state == null or _runtime._state.timeline == null:
		return 0
	return int(_runtime._state.timeline.current_tu)


func _get_barrier_label(barrier: Dictionary) -> String:
	return String(barrier.get("display_name", barrier.get("profile_id", "屏障")))


func _get_layer_label(layer: Dictionary) -> String:
	return String(layer.get("display_name", layer.get("layer_id", "屏障层")))


func _append_changed_coords(batch: BattleEventBatch, coords: Array[Vector2i]) -> void:
	if _runtime == null or batch == null:
		return
	_runtime._append_changed_coords(batch, coords)


func _append_log(batch: BattleEventBatch, line: String) -> void:
	if batch == null or line.is_empty():
		return
	batch.log_lines.append(line)
