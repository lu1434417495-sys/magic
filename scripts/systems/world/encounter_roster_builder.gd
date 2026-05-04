## 文件说明：该脚本属于遭遇编队构建器相关的构建脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name EncounterRosterBuilder
extends RefCounted

const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const ENEMY_TEMPLATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_template_def.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const UNIT_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_progress.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const BASIC_ATTACK_SKILL_ID: StringName = &"basic_attack"
const DROP_TYPE_ITEM: StringName = &"item"
const DROP_TYPE_RANDOM_EQUIPMENT: StringName = &"random_equipment"

const DROP_DEFINITION_REQUIRED_FIELDS := [
	"drop_entry_id",
	"drop_type",
	"item_id",
	"quantity",
]
const FORMAL_LOOT_ENTRY_REQUIRED_FIELDS := [
	"drop_type",
	"drop_source_kind",
	"drop_source_id",
	"drop_source_label",
	"drop_entry_id",
	"item_id",
	"quantity",
]

var _wild_encounter_rosters: Dictionary = {}
var _enemy_templates: Dictionary = {}


func setup(wild_encounter_rosters: Dictionary = {}, enemy_templates: Dictionary = {}) -> void:
	_wild_encounter_rosters = wild_encounter_rosters if wild_encounter_rosters != null else {}
	_enemy_templates = enemy_templates if enemy_templates != null else {}


func build_enemy_units(encounter_anchor, source: Dictionary = {}):
	var build_context: Dictionary = source if not _looks_like_skill_def_dict(source) else {}
	var skill_defs: Dictionary = source if _looks_like_skill_def_dict(source) else build_context.get("skill_defs", {})
	var enemy_templates: Dictionary = build_context.get("enemy_templates", {})
	var enemy_ai_brains: Dictionary = build_context.get("enemy_ai_brains", {})
	var encounter_roster = _resolve_wild_encounter_roster(encounter_anchor)
	if encounter_roster != null:
		return _build_profile_enemy_units(
			encounter_anchor,
			encounter_roster,
			skill_defs,
			enemy_templates,
			enemy_ai_brains,
			build_context
		)
	var template = _resolve_enemy_template(encounter_anchor, enemy_templates)
	if template != null:
		return _build_template_enemy_units(encounter_anchor, template, skill_defs, enemy_ai_brains, build_context)
	return _build_fallback_enemy_units(encounter_anchor, skill_defs, build_context)


func build_loot_entries(encounter_anchor, source: Dictionary = {}) -> Array:
	var build_context: Dictionary = source if not _looks_like_skill_def_dict(source) else {}
	var enemy_templates: Dictionary = build_context.get("enemy_templates", _enemy_templates if _enemy_templates != null else {})
	var encounter_roster = _resolve_wild_encounter_roster(encounter_anchor)
	if encounter_roster == null:
		var template = _resolve_enemy_template(encounter_anchor, enemy_templates)
		if template == null:
			return []
		var enemy_count := maxi(int(build_context.get("enemy_unit_count", template.enemy_count)), 1)
		return _build_preview_loot_entries_from_template(
			template,
			enemy_count,
			&"enemy_template",
			template.template_id,
			String(template.display_name)
		)
	return _build_preview_loot_entries_from_roster(encounter_anchor, encounter_roster, enemy_templates, build_context)


func _looks_like_skill_def_dict(source: Dictionary) -> bool:
	for value in source.values():
		if value == null:
			continue
		return value is SkillDef
	return false


func _resolve_enemy_template(encounter_anchor, enemy_templates: Dictionary):
	if enemy_templates == null or enemy_templates.is_empty():
		return null
	var candidate_ids: Array[StringName] = []
	if encounter_anchor != null and encounter_anchor.enemy_roster_template_id != &"":
		candidate_ids.append(encounter_anchor.enemy_roster_template_id)
	for candidate_id in candidate_ids:
		if candidate_id != &"" and enemy_templates.has(candidate_id):
			return enemy_templates.get(candidate_id)
	return null


func _resolve_wild_encounter_roster(encounter_anchor):
	if _wild_encounter_rosters == null or _wild_encounter_rosters.is_empty():
		return null
	var candidate_ids: Array[StringName] = []
	if encounter_anchor != null and encounter_anchor.encounter_profile_id != &"":
		candidate_ids.append(encounter_anchor.encounter_profile_id)
	for candidate_id in candidate_ids:
		if candidate_id != &"" and _wild_encounter_rosters.has(candidate_id):
			return _wild_encounter_rosters.get(candidate_id)
	return null

func _build_preview_loot_entries_from_roster(
	encounter_anchor,
	encounter_roster,
	enemy_templates: Dictionary,
	build_context: Dictionary
) -> Array:
	if encounter_roster == null or enemy_templates == null or enemy_templates.is_empty():
		return []
	var growth_stage := maxi(
		int(build_context.get(
			"growth_stage",
			encounter_anchor.growth_stage if encounter_anchor != null else 0
		)),
		0
	)
	var aggregated_entries: Dictionary = {}
	var ordered_keys: Array[String] = []
	for entry_variant in encounter_roster.get_stage_unit_entries(growth_stage):
		if entry_variant is not Dictionary:
			continue
		var unit_entry := entry_variant as Dictionary
		var template_id := ProgressionDataUtils.to_string_name(unit_entry.get("template_id", ""))
		if template_id == &"" or not enemy_templates.has(template_id):
			continue
		var template = enemy_templates.get(template_id)
		var unit_count := maxi(int(unit_entry.get("count", 1)), 1)
		var preview_entries := _build_preview_loot_entries_from_template(
			template,
			unit_count,
			&"encounter_roster",
			encounter_roster.profile_id,
			String(encounter_roster.display_name)
		)
		_merge_preview_loot_entries(aggregated_entries, ordered_keys, preview_entries)
	return _preview_entry_map_to_array(aggregated_entries, ordered_keys)


func _build_preview_loot_entries_from_template(
	template,
	unit_count: int,
	drop_source_kind: StringName,
	drop_source_id: StringName,
	drop_source_label: String
) -> Array:
	if template == null:
		return []
	var drop_entries_variant: Variant = template.get_drop_entries() if template.has_method("get_drop_entries") else []
	var formal_entries := _build_formal_loot_entries(
		drop_entries_variant,
		drop_source_kind,
		drop_source_id,
		drop_source_label
	)
	var multiplied_entries: Array = []
	var multiplier := maxi(unit_count, 1)
	for formal_entry_variant in formal_entries:
		if formal_entry_variant is not Dictionary:
			continue
		var formal_entry := (formal_entry_variant as Dictionary).duplicate(true)
		if not formal_entry.has("quantity") or formal_entry["quantity"] is not int:
			return []
		formal_entry["quantity"] = int(formal_entry["quantity"]) * multiplier
		multiplied_entries.append(formal_entry)
	return multiplied_entries


func _build_formal_loot_entries(
	drop_entries_variant: Variant,
	drop_source_kind: StringName,
	drop_source_id: StringName,
	drop_source_label: String
) -> Array:
	var loot_entries: Array = []
	var normalized_source_kind := _strict_string_name_value(drop_source_kind)
	var normalized_source_id := _strict_string_name_value(drop_source_id)
	var normalized_source_label := drop_source_label.strip_edges()
	if normalized_source_kind == &"" or normalized_source_id == &"" or normalized_source_label.is_empty():
		return loot_entries
	if drop_entries_variant is not Array:
		return loot_entries
	for entry_variant in drop_entries_variant:
		if entry_variant is not Dictionary:
			return []
		var entry_data := entry_variant as Dictionary
		var parsed_entry := _parse_drop_definition(entry_data)
		if parsed_entry.is_empty():
			return []
		loot_entries.append({
			"drop_type": String(parsed_entry["drop_type"]),
			"drop_source_kind": String(normalized_source_kind),
			"drop_source_id": String(normalized_source_id),
			"drop_source_label": normalized_source_label,
			"drop_entry_id": String(parsed_entry["drop_entry_id"]),
			"item_id": String(parsed_entry["item_id"]),
			"quantity": int(parsed_entry["quantity"]),
		})
	return loot_entries


func _parse_drop_definition(entry_data: Dictionary) -> Dictionary:
	if entry_data.has("drop_id"):
		return {}
	if entry_data.size() != DROP_DEFINITION_REQUIRED_FIELDS.size():
		return {}
	for field_name in DROP_DEFINITION_REQUIRED_FIELDS:
		if not entry_data.has(field_name):
			return {}
	var drop_entry_id := _strict_string_name_value(entry_data["drop_entry_id"])
	var drop_type := _strict_string_name_value(entry_data["drop_type"])
	var item_id := _strict_string_name_value(entry_data["item_id"])
	if drop_entry_id == &"" or item_id == &"":
		return {}
	if drop_type != DROP_TYPE_ITEM and drop_type != DROP_TYPE_RANDOM_EQUIPMENT:
		return {}
	if entry_data["quantity"] is not int:
		return {}
	var quantity := int(entry_data["quantity"])
	if quantity <= 0:
		return {}
	return {
		"drop_entry_id": drop_entry_id,
		"drop_type": drop_type,
		"item_id": item_id,
		"quantity": quantity,
	}


func _strict_string_name_value(value: Variant) -> StringName:
	if value is StringName:
		return value if value != &"" else &""
	if value is String:
		var text := (value as String).strip_edges()
		if text.is_empty():
			return &""
		return StringName(text)
	return &""


func _strict_string_value(value: Variant) -> String:
	if value is not String:
		return ""
	return (value as String).strip_edges()


func _merge_preview_loot_entries(target_entries: Dictionary, ordered_keys: Array[String], preview_entries: Array) -> void:
	for preview_entry_variant in preview_entries:
		if preview_entry_variant is not Dictionary:
			target_entries.clear()
			ordered_keys.clear()
			return
		var preview_entry := preview_entry_variant as Dictionary
		var parsed_entry := _parse_formal_loot_entry(preview_entry)
		if parsed_entry.is_empty():
			target_entries.clear()
			ordered_keys.clear()
			return
		var drop_type := String(parsed_entry["drop_type"])
		var item_id := String(parsed_entry["item_id"])
		var quantity := int(parsed_entry["quantity"])
		var entry_key := "%s|%s" % [String(drop_type), String(item_id)]
		if not target_entries.has(entry_key):
			target_entries[entry_key] = parsed_entry.duplicate(true)
			target_entries[entry_key]["drop_entry_id"] = "%s_%s_%s" % [
				String(parsed_entry["drop_source_kind"]),
				String(parsed_entry["drop_source_id"]),
				String(item_id),
			]
			ordered_keys.append(entry_key)
			continue
		var merged_entry: Dictionary = target_entries.get(entry_key, {}) as Dictionary
		if not merged_entry.has("quantity") or merged_entry["quantity"] is not int:
			target_entries.clear()
			ordered_keys.clear()
			return
		merged_entry["quantity"] = int(merged_entry["quantity"]) + quantity
		target_entries[entry_key] = merged_entry


func _parse_formal_loot_entry(entry_data: Dictionary) -> Dictionary:
	if entry_data.has("drop_id"):
		return {}
	if entry_data.size() != FORMAL_LOOT_ENTRY_REQUIRED_FIELDS.size():
		return {}
	for field_name in FORMAL_LOOT_ENTRY_REQUIRED_FIELDS:
		if not entry_data.has(field_name):
			return {}
	var drop_type := _strict_string_name_value(entry_data["drop_type"])
	var drop_source_kind := _strict_string_name_value(entry_data["drop_source_kind"])
	var drop_source_id := _strict_string_name_value(entry_data["drop_source_id"])
	var drop_source_label := _strict_string_value(entry_data["drop_source_label"])
	var drop_entry_id := _strict_string_name_value(entry_data["drop_entry_id"])
	var item_id := _strict_string_name_value(entry_data["item_id"])
	if drop_type == &"" or drop_source_kind == &"" or drop_source_id == &"":
		return {}
	if drop_source_label.is_empty() or drop_entry_id == &"" or item_id == &"":
		return {}
	if entry_data["quantity"] is not int:
		return {}
	var quantity := int(entry_data["quantity"])
	if quantity <= 0:
		return {}
	return {
		"drop_type": String(drop_type),
		"drop_source_kind": String(drop_source_kind),
		"drop_source_id": String(drop_source_id),
		"drop_source_label": drop_source_label,
		"drop_entry_id": String(drop_entry_id),
		"item_id": String(item_id),
		"quantity": quantity,
	}


func _preview_entry_map_to_array(target_entries: Dictionary, ordered_keys: Array[String]) -> Array:
	var preview_entries: Array = []
	for entry_key in ordered_keys:
		var preview_entry: Dictionary = target_entries.get(entry_key, {}) as Dictionary
		if preview_entry is Dictionary and not (preview_entry as Dictionary).is_empty():
			preview_entries.append((preview_entry as Dictionary).duplicate(true))
	return preview_entries


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
			build_context,
			next_unit_index,
			unit_count,
			String(entry.get("display_name", "")),
			true
		)
		next_unit_index += built_units.size()
		enemy_units.append_array(built_units)
	if not enemy_units.is_empty():
		return enemy_units
	var template = _resolve_enemy_template(encounter_anchor, enemy_templates)
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
	return _build_units_from_template(
		encounter_anchor,
		template,
		skill_defs,
		enemy_ai_brains,
		build_context,
		0,
		enemy_count,
		fallback_display_name,
		false
	)


func _build_units_from_template(
	encounter_anchor,
	template,
	skill_defs: Dictionary,
	enemy_ai_brains: Dictionary,
	build_context: Dictionary,
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
		unit_state.enemy_template_id = template.template_id if template != null else &""
		unit_state.display_name = _resolve_enemy_unit_display_name(base_display_name, local_index, resolved_unit_count, use_numeric_suffix)
		unit_state.faction_id = encounter_anchor.faction_id if encounter_anchor != null and encounter_anchor.faction_id != &"" else &"hostile"
		unit_state.control_mode = &"ai"
		unit_state.ai_brain_id = template.brain_id
		unit_state.ai_state_id = template.get_initial_state_id(brain)
		unit_state.ai_blackboard = {}
		unit_state.body_size = maxi(int(template.body_size), 1)
		unit_state.refresh_footprint()
		unit_state.action_threshold = int(template.action_threshold)
		_apply_enemy_weapon_projection(unit_state, template)
		unit_state.attribute_snapshot = _build_enemy_snapshot_from_template(
			template,
			encounter_anchor,
			global_index,
			build_context
		)
		unit_state.current_hp = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)
		unit_state.current_mp = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX)
		unit_state.current_stamina = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX)
		unit_state.current_ap = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS)
		unit_state.current_move_points = BATTLE_UNIT_STATE_SCRIPT.DEFAULT_MOVE_POINTS_PER_TURN
		unit_state.known_active_skill_ids = template.skill_ids.duplicate()
		if unit_state.known_active_skill_ids.is_empty():
			unit_state.known_active_skill_ids = _pick_default_enemy_skill_ids(skill_defs)
		_ensure_basic_attack_skill(unit_state, skill_defs)
		for skill_id in unit_state.known_active_skill_ids:
			var normalized_skill_id := StringName(String(skill_id))
			var configured_level := int(template.skill_level_map.get(normalized_skill_id, 1))
			unit_state.known_skill_level_map[normalized_skill_id] = maxi(configured_level, 1)
		_sync_enemy_unlocked_resources(unit_state, skill_defs)
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


func _build_enemy_snapshot_from_template(
	template,
	encounter_anchor,
	unit_index: int,
	build_context: Dictionary
):
	var base_attributes := _resolve_enemy_base_attributes(template, encounter_anchor, unit_index, build_context)
	var unit_progress = UNIT_PROGRESS_SCRIPT.new()
	for attribute_id in UNIT_BASE_ATTRIBUTES_SCRIPT.BASE_ATTRIBUTE_IDS:
		unit_progress.unit_base_attributes.set_attribute_value(attribute_id, int(base_attributes.get(attribute_id, 0)))
	var attribute_service = ATTRIBUTE_SERVICE_SCRIPT.new()
	attribute_service.setup(unit_progress)
	var snapshot = attribute_service.get_snapshot()
	var stats: Dictionary = template.attribute_overrides if template != null else {}
	_apply_enemy_attribute_overrides(snapshot, stats)
	return snapshot


func _apply_enemy_weapon_projection(unit_state: BattleUnitState, template) -> void:
	if unit_state == null:
		return
	var projection: Dictionary = template.get_weapon_projection() if template != null and template.has_method("get_weapon_projection") else {}
	if projection.is_empty():
		unit_state.clear_weapon_projection()
		return
	unit_state.apply_weapon_projection(projection)


func _resolve_enemy_base_attributes(
	template,
	encounter_anchor,
	unit_index: int,
	build_context: Dictionary
) -> Dictionary:
	var resolved: Dictionary = {}
	var configured: Dictionary = {}
	if template != null and template.has_method("get_base_attribute_overrides"):
		configured = template.get_base_attribute_overrides()
	for attribute_id in UNIT_BASE_ATTRIBUTES_SCRIPT.BASE_ATTRIBUTE_IDS:
		resolved[attribute_id] = int(configured.get(attribute_id, 0))
	return resolved


func _apply_enemy_attribute_overrides(snapshot, stats: Dictionary) -> void:
	if snapshot == null:
		return
	for raw_key in stats.keys():
		var attribute_id := ProgressionDataUtils.to_string_name(raw_key)
		if attribute_id == &"":
			continue
		var value := int(stats.get(raw_key, 0))
		match attribute_id:
			ATTRIBUTE_SERVICE_SCRIPT.HP_MAX:
				value = maxi(value, 1)
			ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX:
				value = maxi(value, 0)
			ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS:
				value = maxi(value, 1)
		snapshot.set_value(attribute_id, value)


func _build_fallback_enemy_units(encounter_anchor, skill_defs: Dictionary, build_context: Dictionary) -> Array:
	var enemy_units: Array = []
	var enemy_count := maxi(
		int(build_context.get("enemy_unit_count", 2 if encounter_anchor != null and encounter_anchor.enemy_roster_template_id != &"" else 1)),
		1
	)
	var default_skill_ids: Array[StringName] = _pick_default_enemy_skill_ids(skill_defs)
	var fallback_stamina_max := _resolve_basic_attack_stamina_cost(skill_defs)
	for index in range(enemy_count):
		var unit_state := BATTLE_UNIT_STATE_SCRIPT.new()
		unit_state.unit_id = _build_enemy_unit_id(encounter_anchor, index)
		unit_state.display_name = encounter_anchor.display_name if encounter_anchor != null and index == 0 else "%s·从属%d" % [
			encounter_anchor.display_name if encounter_anchor != null else "敌人",
			index + 1,
		]
		unit_state.faction_id = encounter_anchor.faction_id if encounter_anchor != null else &"hostile"
		unit_state.control_mode = &"ai"
		unit_state.body_size = BattleUnitState.BODY_SIZE_MEDIUM
		unit_state.refresh_footprint()
		unit_state.attribute_snapshot = _build_enemy_snapshot(index, fallback_stamina_max)
		unit_state.current_hp = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)
		unit_state.current_mp = 0
		unit_state.current_stamina = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX)
		unit_state.current_ap = unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS)
		unit_state.current_move_points = BATTLE_UNIT_STATE_SCRIPT.DEFAULT_MOVE_POINTS_PER_TURN
		unit_state.set_unarmed_weapon_projection()
		unit_state.action_threshold = BATTLE_UNIT_STATE_SCRIPT.DEFAULT_ACTION_THRESHOLD
		unit_state.known_active_skill_ids = default_skill_ids.duplicate()
		_ensure_basic_attack_skill(unit_state, skill_defs)
		for skill_id in unit_state.known_active_skill_ids:
			unit_state.known_skill_level_map[skill_id] = 1
		_sync_enemy_unlocked_resources(unit_state, skill_defs)
		enemy_units.append(unit_state)
	return enemy_units


func _build_enemy_snapshot(index: int, fallback_stamina_max: int = 0):
	var snapshot = ATTRIBUTE_SNAPSHOT_SCRIPT.new()
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 26 + index * 6)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, 0)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, maxi(fallback_stamina_max, 0))
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS, 1)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 4 + index)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 12 + index)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_AC_BONUS, 0)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SHIELD_AC_BONUS, 0)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DODGE_BONUS, 0)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DEFLECTION_BONUS, 0)
	return snapshot


func _pick_default_enemy_skill_ids(skill_defs: Dictionary) -> Array[StringName]:
	var preferred_skill_ids: Array[StringName] = [
		BASIC_ATTACK_SKILL_ID,
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


func _ensure_basic_attack_skill(unit_state, skill_defs: Dictionary) -> void:
	if unit_state == null or not skill_defs.has(BASIC_ATTACK_SKILL_ID):
		return
	if not unit_state.known_active_skill_ids.has(BASIC_ATTACK_SKILL_ID):
		unit_state.known_active_skill_ids.append(BASIC_ATTACK_SKILL_ID)


func _resolve_basic_attack_stamina_cost(skill_defs: Dictionary) -> int:
	var skill_def := skill_defs.get(BASIC_ATTACK_SKILL_ID) as SkillDef
	if skill_def == null or skill_def.combat_profile == null:
		return 0
	var costs: Dictionary = skill_def.combat_profile.get_effective_resource_costs(1)
	return maxi(int(costs.get("stamina_cost", skill_def.combat_profile.stamina_cost)), 0)


func _sync_enemy_unlocked_resources(unit_state, skill_defs: Dictionary) -> void:
	if unit_state == null:
		return
	unit_state.sync_default_combat_resource_unlocks()
	var mp_max := 0
	var aura_max := 0
	if unit_state.attribute_snapshot != null:
		mp_max = int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX))
		aura_max = int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX))
	if int(unit_state.current_mp) > 0 or mp_max > 0:
		unit_state.unlock_combat_resource(BATTLE_UNIT_STATE_SCRIPT.COMBAT_RESOURCE_MP)
	if int(unit_state.current_aura) > 0 or aura_max > 0:
		unit_state.unlock_combat_resource(BATTLE_UNIT_STATE_SCRIPT.COMBAT_RESOURCE_AURA)
	for skill_id in unit_state.known_active_skill_ids:
		var skill_def := skill_defs.get(skill_id) as SkillDef
		if skill_def == null or skill_def.combat_profile == null:
			continue
		var skill_level := maxi(int(unit_state.known_skill_level_map.get(skill_id, 1)), 1)
		var costs: Dictionary = skill_def.combat_profile.get_effective_resource_costs(skill_level)
		if int(costs.get("mp_cost", 0)) > 0:
			unit_state.unlock_combat_resource(BATTLE_UNIT_STATE_SCRIPT.COMBAT_RESOURCE_MP)
		if int(costs.get("aura_cost", 0)) > 0:
			unit_state.unlock_combat_resource(BATTLE_UNIT_STATE_SCRIPT.COMBAT_RESOURCE_AURA)
