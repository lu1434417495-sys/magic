## 文件说明：该脚本属于遭遇编队构建器相关的构建脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name EncounterRosterBuilder
extends RefCounted

const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle_unit_state.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

var _wild_encounter_rosters: Dictionary = {}


func setup(wild_encounter_rosters: Dictionary = {}) -> void:
	_wild_encounter_rosters = wild_encounter_rosters if wild_encounter_rosters != null else {}


func build_enemy_units(encounter_anchor, source: Dictionary = {}):
	var build_context: Dictionary = source if not _looks_like_skill_def_dict(source) else {}
	var skill_defs: Dictionary = source if _looks_like_skill_def_dict(source) else build_context.get("skill_defs", {})
	var enemy_templates: Dictionary = build_context.get("enemy_templates", {})
	var enemy_ai_brains: Dictionary = build_context.get("enemy_ai_brains", {})
	var encounter_roster = _resolve_wild_encounter_roster(encounter_anchor, build_context)
	if encounter_roster != null:
		return _build_profile_enemy_units(
			encounter_anchor,
			encounter_roster,
			skill_defs,
			enemy_templates,
			enemy_ai_brains,
			build_context
		)
	var template = _resolve_enemy_template(encounter_anchor, build_context, enemy_templates)
	if template != null:
		return _build_template_enemy_units(encounter_anchor, template, skill_defs, enemy_ai_brains, build_context)
	return _build_fallback_enemy_units(encounter_anchor, skill_defs, build_context)


func build_loot_entries(encounter_anchor, source: Dictionary = {}) -> Array:
	var build_context: Dictionary = source if not _looks_like_skill_def_dict(source) else {}
	var encounter_roster = _resolve_wild_encounter_roster(encounter_anchor, build_context)
	if encounter_roster == null:
		return []
	return _build_loot_entries_from_roster(encounter_roster)


func _looks_like_skill_def_dict(source: Dictionary) -> bool:
	for value in source.values():
		if value == null:
			continue
		return value is SkillDef
	return false


func _resolve_enemy_template(encounter_anchor, build_context: Dictionary, enemy_templates: Dictionary):
	if enemy_templates == null or enemy_templates.is_empty():
		return null
	var candidate_ids: Array[StringName] = []
	var explicit_template_id := ProgressionDataUtils.to_string_name(build_context.get("monster_template_id", ""))
	if explicit_template_id != &"":
		candidate_ids.append(explicit_template_id)
	if encounter_anchor != null and encounter_anchor.enemy_roster_template_id != &"":
		candidate_ids.append(encounter_anchor.enemy_roster_template_id)
	for candidate_id in candidate_ids:
		if candidate_id != &"" and enemy_templates.has(candidate_id):
			return enemy_templates.get(candidate_id)
	return null


func _resolve_wild_encounter_roster(encounter_anchor, build_context: Dictionary):
	if _wild_encounter_rosters == null or _wild_encounter_rosters.is_empty():
		return null
	var candidate_ids: Array[StringName] = []
	var explicit_profile_id := ProgressionDataUtils.to_string_name(build_context.get("encounter_profile_id", ""))
	if explicit_profile_id != &"":
		candidate_ids.append(explicit_profile_id)
	if encounter_anchor != null and encounter_anchor.encounter_profile_id != &"":
		candidate_ids.append(encounter_anchor.encounter_profile_id)
	for candidate_id in candidate_ids:
		if candidate_id != &"" and _wild_encounter_rosters.has(candidate_id):
			return _wild_encounter_rosters.get(candidate_id)
	return null


func _build_loot_entries_from_roster(encounter_roster) -> Array:
	if encounter_roster == null:
		return []
	return _build_formal_loot_entries(
		encounter_roster.get_drop_entries() if encounter_roster.has_method("get_drop_entries") else [],
		&"encounter_roster",
		encounter_roster.profile_id,
		String(encounter_roster.display_name)
	)


func _build_formal_loot_entries(
	drop_entries_variant: Variant,
	drop_source_kind: StringName,
	drop_source_id: StringName,
	drop_source_label: String
) -> Array:
	var loot_entries: Array = []
	if drop_entries_variant is not Array:
		return loot_entries
	for entry_variant in drop_entries_variant:
		if entry_variant is not Dictionary:
			continue
		var entry_data := entry_variant as Dictionary
		var drop_id := ProgressionDataUtils.to_string_name(entry_data.get("drop_id", ""))
		var drop_type := ProgressionDataUtils.to_string_name(entry_data.get("drop_type", ""))
		var item_id := ProgressionDataUtils.to_string_name(entry_data.get("item_id", ""))
		var quantity := maxi(int(entry_data.get("quantity", 0)), 0)
		if drop_id == &"" or drop_type == &"" or item_id == &"" or quantity <= 0:
			continue
		loot_entries.append({
			"drop_type": String(drop_type),
			"drop_source_kind": String(drop_source_kind),
			"drop_source_id": String(drop_source_id),
			"drop_source_label": drop_source_label,
			"drop_entry_id": String(drop_id),
			"item_id": String(item_id),
			"quantity": quantity,
		})
	return loot_entries


func _build_profile_enemy_units(
	encounter_anchor,
	encounter_roster,
	skill_defs: Dictionary,
	enemy_templates: Dictionary,
	enemy_ai_brains: Dictionary,
	build_context: Dictionary
) -> Array:
	var growth_stage := maxi(
		int(build_context.get(
			"growth_stage",
			encounter_anchor.growth_stage if encounter_anchor != null else 0
		)),
		0
	)
	var enemy_units: Array = []
	var next_unit_index := 0
	for entry_variant in encounter_roster.get_stage_unit_entries(growth_stage):
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		var template_id := ProgressionDataUtils.to_string_name(entry.get("template_id", ""))
		if template_id == &"":
			continue
		var template = enemy_templates.get(template_id)
		if template == null:
			continue
		var unit_count := maxi(int(entry.get("count", 1)), 1)
		var built_units := _build_units_from_template(
			encounter_anchor,
			template,
			skill_defs,
			enemy_ai_brains,
			next_unit_index,
			unit_count,
			String(entry.get("display_name", "")),
			true
		)
		next_unit_index += built_units.size()
		enemy_units.append_array(built_units)
	if not enemy_units.is_empty():
		return enemy_units
	var template = _resolve_enemy_template(encounter_anchor, build_context, enemy_templates)
	if template != null:
		return _build_template_enemy_units(encounter_anchor, template, skill_defs, enemy_ai_brains, build_context)
	return _build_fallback_enemy_units(encounter_anchor, skill_defs, build_context)


func _build_template_enemy_units(
	encounter_anchor,
	template,
	skill_defs: Dictionary,
	enemy_ai_brains: Dictionary,
	build_context: Dictionary
) -> Array:
	var enemy_count := maxi(int(build_context.get("enemy_unit_count", template.enemy_count)), 1)
	var fallback_display_name := "敌人"
	if template != null and not String(template.display_name).is_empty():
		fallback_display_name = template.display_name
	elif encounter_anchor != null and not String(encounter_anchor.display_name).is_empty():
		fallback_display_name = encounter_anchor.display_name
	var display_name := String(build_context.get("monster_display_name", fallback_display_name))
	return _build_units_from_template(
		encounter_anchor,
		template,
		skill_defs,
		enemy_ai_brains,
		0,
		enemy_count,
		display_name,
		false
	)


func _build_units_from_template(
	encounter_anchor,
	template,
	skill_defs: Dictionary,
	enemy_ai_brains: Dictionary,
	start_index: int,
	unit_count: int,
	display_name_override: String,
	use_numeric_suffix: bool
) -> Array:
	var enemy_units: Array = []
	var resolved_unit_count := maxi(unit_count, 1)
	var base_display_name := display_name_override
	if base_display_name.is_empty():
		base_display_name = String(template.display_name if template != null else "")
	if base_display_name.is_empty():
		base_display_name = encounter_anchor.display_name if encounter_anchor != null else "敌人"
	var brain = enemy_ai_brains.get(template.brain_id)
	for local_index in range(resolved_unit_count):
		var global_index := start_index + local_index
		var unit_state := BATTLE_UNIT_STATE_SCRIPT.new()
		unit_state.unit_id = _build_enemy_unit_id(encounter_anchor, global_index)
		unit_state.display_name = _resolve_enemy_unit_display_name(base_display_name, local_index, resolved_unit_count, use_numeric_suffix)
		unit_state.faction_id = encounter_anchor.faction_id if encounter_anchor != null and encounter_anchor.faction_id != &"" else &"hostile"
		unit_state.control_mode = &"ai"
		unit_state.ai_brain_id = template.brain_id
		unit_state.ai_state_id = template.get_initial_state_id(brain)
		unit_state.ai_blackboard = {}
		unit_state.body_size = maxi(int(template.body_size), 1)
		unit_state.refresh_footprint()
		unit_state.attribute_snapshot = _build_enemy_snapshot_from_template(template)
		unit_state.current_hp = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)
		unit_state.current_mp = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX)
		unit_state.current_stamina = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX)
		unit_state.current_ap = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS)
		unit_state.known_active_skill_ids = template.skill_ids.duplicate()
		if unit_state.known_active_skill_ids.is_empty():
			unit_state.known_active_skill_ids = _pick_default_enemy_skill_ids(skill_defs)
		for skill_id in unit_state.known_active_skill_ids:
			var normalized_skill_id := StringName(String(skill_id))
			var configured_level := int(template.skill_level_map.get(normalized_skill_id, 1))
			unit_state.known_skill_level_map[normalized_skill_id] = maxi(configured_level, 1)
		enemy_units.append(unit_state)
	return enemy_units


func _resolve_enemy_unit_display_name(base_display_name: String, local_index: int, unit_count: int, use_numeric_suffix: bool) -> String:
	if unit_count <= 1:
		return base_display_name
	if use_numeric_suffix:
		return "%s·%d" % [base_display_name, local_index + 1]
	return base_display_name if local_index == 0 else "%s·从属%d" % [base_display_name, local_index + 1]


func _build_enemy_unit_id(encounter_anchor, index: int) -> StringName:
	var anchor_id := String(encounter_anchor.entity_id) if encounter_anchor != null else "wild"
	return StringName("%s_%02d" % [anchor_id, index + 1])


func _build_enemy_snapshot_from_template(template):
	var snapshot = ATTRIBUTE_SNAPSHOT_SCRIPT.new()
	var stats: Dictionary = template.attribute_overrides if template != null else {}
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, maxi(int(stats.get("hp_max", 24)), 1))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, maxi(int(stats.get("mp_max", 0)), 0))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, maxi(int(stats.get("stamina_max", 0)), 0))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS, maxi(int(stats.get("action_points", 1)), 1))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.PHYSICAL_ATTACK, int(stats.get("physical_attack", 8)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.PHYSICAL_DEFENSE, int(stats.get("physical_defense", 3)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MAGIC_ATTACK, int(stats.get("magic_attack", 0)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MAGIC_DEFENSE, int(stats.get("magic_defense", 1)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.FIRE_RESISTANCE, int(stats.get("fire_resistance", 0)))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SPEED, int(stats.get("speed", 9)))
	return snapshot


func _build_fallback_enemy_units(encounter_anchor, skill_defs: Dictionary, build_context: Dictionary) -> Array:
	var enemy_units: Array = []
	var enemy_count := maxi(
		int(build_context.get("enemy_unit_count", 2 if encounter_anchor != null and encounter_anchor.enemy_roster_template_id != &"" else 1)),
		1
	)
	var default_skill_ids: Array[StringName] = _pick_default_enemy_skill_ids(skill_defs)
	for index in range(enemy_count):
		var unit_state := BATTLE_UNIT_STATE_SCRIPT.new()
		unit_state.unit_id = _build_enemy_unit_id(encounter_anchor, index)
		unit_state.display_name = encounter_anchor.display_name if encounter_anchor != null and index == 0 else "%s·从属%d" % [
			encounter_anchor.display_name if encounter_anchor != null else "敌人",
			index + 1,
		]
		unit_state.faction_id = encounter_anchor.faction_id if encounter_anchor != null else &"hostile"
		unit_state.control_mode = &"ai"
		unit_state.body_size = 1
		unit_state.refresh_footprint()
		unit_state.attribute_snapshot = _build_enemy_snapshot(index)
		unit_state.current_hp = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)
		unit_state.current_mp = 0
		unit_state.current_stamina = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX)
		unit_state.current_ap = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS)
		unit_state.known_active_skill_ids = default_skill_ids.duplicate()
		for skill_id in unit_state.known_active_skill_ids:
			unit_state.known_skill_level_map[skill_id] = 1
		enemy_units.append(unit_state)
	return enemy_units


func _build_enemy_snapshot(index: int):
	var snapshot = ATTRIBUTE_SNAPSHOT_SCRIPT.new()
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 26 + index * 6)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, 0)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, 0)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS, 1)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.PHYSICAL_ATTACK, 8 + index * 2)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.PHYSICAL_DEFENSE, 3 + index)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MAGIC_ATTACK, 0)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MAGIC_DEFENSE, 1)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SPEED, 9 + index)
	return snapshot


func _pick_default_enemy_skill_ids(skill_defs: Dictionary) -> Array[StringName]:
	var preferred_skill_ids: Array[StringName] = [
		&"warrior_heavy_strike",
		&"warrior_combo_strike",
		&"warrior_guard_break",
	]
	for preferred_skill_id in preferred_skill_ids:
		if _is_valid_enemy_combat_skill(skill_defs.get(preferred_skill_id) as SkillDef):
			return [preferred_skill_id]

	for skill_key in ProgressionDataUtils.sorted_string_keys(skill_defs):
		var skill_id := StringName(skill_key)
		if _is_valid_enemy_combat_skill(skill_defs.get(skill_id) as SkillDef):
			return [skill_id]

	return []


func _is_valid_enemy_combat_skill(skill_def: SkillDef) -> bool:
	if skill_def == null:
		return false
	if skill_def.skill_type != &"active":
		return false
	if not skill_def.can_use_in_combat():
		return false
	if skill_def.combat_profile == null:
		return false
	if skill_def.combat_profile.target_mode != &"unit":
		return false
	return skill_def.combat_profile.target_team_filter == &"enemy"
