## 文件说明：该脚本负责集中注册按设计文档保留的技能定义，并承接少量兼容当前运行时的特殊技能构造。
## 审查重点：重点核对技能集合是否仅来自设计文档、字段映射是否稳定，以及兼容技能是否明确标注过渡语义。
## 备注：这里不实现技能真实新逻辑，只在定义层保留文档字段与当前运行时可识别的占位表达。

class_name DesignSkillCatalog
extends RefCounted

const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const AttributeModifier = preload("res://scripts/player/progression/attribute_modifier.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")
const DESIGN_SKILL_CATALOG_MAGE_SPECS_SCRIPT = preload("res://scripts/player/progression/design_skill_catalog_mage_specs.gd")
const LEGACY_GROUND_VARIANT_PATTERNS := {
	&"single": &"single",
	&"line2": &"line2",
	&"square2": &"square2",
	&"unordered": &"unordered",
}


func register_mage_skills(register_skill: Callable) -> void:
	_register_skill_specs(DESIGN_SKILL_CATALOG_MAGE_SPECS_SCRIPT.get_specs(), register_skill)


func _register_skill_specs(specs: Array[Dictionary], register_skill: Callable) -> void:
	for spec in specs:
		var skill_def := _build_skill_from_catalog_spec(spec)
		if skill_def != null:
			register_skill.call(skill_def)


func _build_skill_from_catalog_spec(spec: Dictionary) -> SkillDef:
	var kind := ProgressionDataUtils.to_string_name(spec.get("kind", "active"))
	match kind:
		&"active", &"special", &"cast_variant", &"cast_variant_hint", &"ground_variant":
			var combat_profile := _build_combat_profile_from_catalog_spec(spec)
			var skill_def := _build_skill(
				ProgressionDataUtils.to_string_name(spec.get("skill_id", "")),
				String(spec.get("display_name", "")),
				String(spec.get("description", "")),
				&"active",
				int(spec.get("max_level", 3)),
				spec.get("mastery_curve", [28, 46, 72]),
				ProgressionDataUtils.to_string_name_array(spec.get("tags", [])),
				ProgressionDataUtils.to_string_name(spec.get("learn_source", "book")),
				ProgressionDataUtils.to_string_name_array(spec.get("learn_requirements", [])),
				ProgressionDataUtils.to_string_name_array(spec.get("mastery_sources", [])),
				[],
				combat_profile
			)
			_apply_skill_spec_overrides(skill_def, spec)
			return skill_def
		_:
			return null


func _build_combat_profile_from_catalog_spec(spec: Dictionary) -> CombatSkillDef:
	var costs: Dictionary = spec.get("costs", {})
	var targeting: Dictionary = spec.get("targeting", {})
	var custom: Dictionary = spec.get("custom", {})
	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = ProgressionDataUtils.to_string_name(spec.get("skill_id", ""))
	combat_profile.target_mode = ProgressionDataUtils.to_string_name(targeting.get("target_mode", "unit"))
	combat_profile.target_team_filter = ProgressionDataUtils.to_string_name(targeting.get("target_team_filter", "enemy"))
	combat_profile.range_pattern = ProgressionDataUtils.to_string_name(targeting.get("range_pattern", "single"))
	combat_profile.range_value = int(targeting.get("range_value", costs.get("range_value", 0)))
	combat_profile.area_pattern = ProgressionDataUtils.to_string_name(targeting.get("area_pattern", "single"))
	combat_profile.area_value = int(targeting.get("area_value", 0))
	combat_profile.ap_cost = int(costs.get("ap_cost", 0))
	combat_profile.mp_cost = int(costs.get("mp_cost", 0))
	combat_profile.stamina_cost = int(costs.get("stamina_cost", 0))
	combat_profile.aura_cost = int(costs.get("aura_cost", 0))
	combat_profile.cooldown_tu = int(costs.get("cooldown_tu", 0))
	combat_profile.hit_rate = int(costs.get("hit_rate", 0))
	combat_profile.target_selection_mode = ProgressionDataUtils.to_string_name(targeting.get("target_selection_mode", "single_unit"))
	combat_profile.min_target_count = int(targeting.get("min_target_count", 1))
	combat_profile.max_target_count = int(targeting.get("max_target_count", combat_profile.min_target_count))
	combat_profile.selection_order_mode = ProgressionDataUtils.to_string_name(targeting.get("selection_order_mode", "stable"))
	combat_profile.effect_defs = _build_effect_defs_from_catalog_spec(spec.get("effect_defs", spec.get("effects", [])))
	_append_cast_variants_from_catalog_specs(combat_profile, spec.get("cast_variants", []))
	if custom is Dictionary:
		combat_profile.ai_tags = ProgressionDataUtils.to_string_name_array(custom.get("ai_tags", []))
		_append_cast_variants_from_catalog_specs(combat_profile, custom.get("cast_variants", []))
	_canonicalize_shared_variant_effect_defs(combat_profile)
	return combat_profile


func _append_cast_variants_from_catalog_specs(combat_profile: CombatSkillDef, variant_specs: Variant) -> void:
	if combat_profile == null or variant_specs is not Array:
		return
	for cast_variant_spec in variant_specs:
		if cast_variant_spec is not Dictionary:
			continue
		combat_profile.cast_variants.append(_build_cast_variant_from_catalog_spec(cast_variant_spec))


func _build_effect_defs_from_catalog_spec(effect_specs: Array) -> Array[CombatEffectDef]:
	var effect_defs: Array[CombatEffectDef] = []
	for effect_spec_variant in effect_specs:
		if effect_spec_variant is not Dictionary:
			continue
		var effect_spec: Dictionary = effect_spec_variant
		var kind := ProgressionDataUtils.to_string_name(effect_spec.get("kind", ""))
		var effect_def: CombatEffectDef = null
		match kind:
			&"damage":
				effect_def = _build_damage_effect(
					int(effect_spec.get("power", 0)),
					ProgressionDataUtils.to_string_name(effect_spec.get("scaling_attribute_id", "physical_attack")),
					ProgressionDataUtils.to_string_name(effect_spec.get("defense_attribute_id", "physical_defense")),
					ProgressionDataUtils.to_string_name(effect_spec.get("resistance_attribute_id", "")),
					ProgressionDataUtils.to_string_name(effect_spec.get("effect_target_team_filter", ""))
				)
			&"heal":
				effect_def = _build_heal_effect(
					int(effect_spec.get("power", 0)),
					ProgressionDataUtils.to_string_name(effect_spec.get("effect_target_team_filter", ""))
				)
			&"status":
				effect_def = _build_status_effect(
					ProgressionDataUtils.to_string_name(effect_spec.get("status_id", "")),
					int(effect_spec.get("duration", effect_spec.get("params", {}).get("duration", 0))),
					int(effect_spec.get("power", 1)),
					ProgressionDataUtils.to_string_name(effect_spec.get("effect_target_team_filter", ""))
				)
			&"terrain_replace":
				effect_def = _build_terrain_replace_effect(
					ProgressionDataUtils.to_string_name(effect_spec.get("terrain_replace_to", effect_spec.get("terrain", "")))
				)
			&"height_delta":
				effect_def = _build_height_delta_effect(int(effect_spec.get("height_delta", effect_spec.get("delta", 0))))
			&"forced_move":
				effect_def = _build_forced_move_effect(
					int(effect_spec.get("forced_move_distance", effect_spec.get("distance", 0))),
					ProgressionDataUtils.to_string_name(effect_spec.get("forced_move_mode", effect_spec.get("mode", "retreat")))
				)
			&"special":
				var params: Dictionary = effect_spec.get("params", {})
				effect_def = _build_special_effect(
					ProgressionDataUtils.to_string_name(effect_spec.get("effect_type", "")),
					params.duplicate(true),
					int(effect_spec.get("power", 0))
				)
			_:
				continue
		if effect_def == null:
			continue
		if effect_spec.has("bonus_condition"):
			effect_def.bonus_condition = ProgressionDataUtils.to_string_name(effect_spec.get("bonus_condition", ""))
		if effect_spec.has("damage_ratio_percent"):
			effect_def.damage_ratio_percent = int(effect_spec.get("damage_ratio_percent", 100))
		if effect_spec.has("forced_move_distance"):
			effect_def.forced_move_distance = int(effect_spec.get("forced_move_distance", 0))
		if effect_spec.has("forced_move_mode"):
			effect_def.forced_move_mode = ProgressionDataUtils.to_string_name(effect_spec.get("forced_move_mode", ""))
		if effect_spec.has("tick_effect_type"):
			effect_def.tick_effect_type = ProgressionDataUtils.to_string_name(effect_spec.get("tick_effect_type", ""))
		if effect_spec.has("terrain_effect_id"):
			effect_def.terrain_effect_id = ProgressionDataUtils.to_string_name(effect_spec.get("terrain_effect_id", ""))
		if effect_spec.has("trigger_event"):
			effect_def.trigger_event = ProgressionDataUtils.to_string_name(effect_spec.get("trigger_event", ""))
		if effect_spec.has("duration_tu"):
			effect_def.duration_tu = int(effect_spec.get("duration_tu", 0))
		if effect_spec.has("tick_interval_tu"):
			effect_def.tick_interval_tu = int(effect_spec.get("tick_interval_tu", 0))
		if effect_spec.has("stack_behavior"):
			effect_def.stack_behavior = ProgressionDataUtils.to_string_name(effect_spec.get("stack_behavior", "refresh"))
		if effect_spec.has("stack_limit"):
			effect_def.stack_limit = int(effect_spec.get("stack_limit", 0))
		if effect_spec.has("effect_target_team_filter"):
			effect_def.effect_target_team_filter = ProgressionDataUtils.to_string_name(effect_spec.get("effect_target_team_filter", ""))
		effect_defs.append(effect_def)
	return effect_defs


func _build_cast_variant_from_catalog_spec(spec: Dictionary) -> CombatCastVariantDef:
	var cast_variant := CombatCastVariantDef.new()
	var raw_target_mode := ProgressionDataUtils.to_string_name(spec.get("target_mode", ""))
	var footprint_pattern := ProgressionDataUtils.to_string_name(spec.get("footprint_pattern", ""))
	cast_variant.variant_id = ProgressionDataUtils.to_string_name(spec.get("variant_id", ""))
	cast_variant.display_name = String(spec.get("display_name", ""))
	cast_variant.description = String(spec.get("description", ""))
	cast_variant.min_skill_level = int(spec.get("min_skill_level", 0))
	cast_variant.target_mode = _resolve_cast_variant_target_mode(raw_target_mode)
	cast_variant.footprint_pattern = _resolve_cast_variant_footprint_pattern(raw_target_mode, footprint_pattern)
	cast_variant.required_coord_count = int(spec.get("required_coord_count", 1))
	cast_variant.allowed_base_terrains = ProgressionDataUtils.to_string_name_array(spec.get("allowed_base_terrains", []))
	cast_variant.effect_defs = _build_effect_defs_from_catalog_spec(spec.get("effect_defs", spec.get("effects", [])))
	return cast_variant


func _resolve_cast_variant_target_mode(raw_target_mode: StringName) -> StringName:
	if LEGACY_GROUND_VARIANT_PATTERNS.has(raw_target_mode):
		return &"ground"
	return raw_target_mode if raw_target_mode != &"" else &"ground"


func _resolve_cast_variant_footprint_pattern(
	raw_target_mode: StringName,
	explicit_footprint_pattern: StringName
) -> StringName:
	if explicit_footprint_pattern != &"":
		return explicit_footprint_pattern
	if LEGACY_GROUND_VARIANT_PATTERNS.has(raw_target_mode):
		return LEGACY_GROUND_VARIANT_PATTERNS[raw_target_mode]
	return &"single"


func _canonicalize_shared_variant_effect_defs(combat_profile: CombatSkillDef) -> void:
	if combat_profile == null or combat_profile.cast_variants.is_empty() or combat_profile.effect_defs.is_empty():
		return
	var shared_effect_defs := _duplicate_effect_defs(combat_profile.effect_defs)
	for variant_index in range(combat_profile.cast_variants.size()):
		var cast_variant := combat_profile.cast_variants[variant_index] as CombatCastVariantDef
		if cast_variant == null:
			continue
		var merged_effect_defs := _duplicate_effect_defs(shared_effect_defs)
		merged_effect_defs.append_array(_duplicate_effect_defs(cast_variant.effect_defs))
		cast_variant.effect_defs = merged_effect_defs
	combat_profile.effect_defs.clear()


func _duplicate_effect_defs(effect_defs: Array) -> Array[CombatEffectDef]:
	var duplicates: Array[CombatEffectDef] = []
	for effect_variant in effect_defs:
		var effect_def := effect_variant as CombatEffectDef
		if effect_def == null:
			continue
		duplicates.append(effect_def.duplicate(true) as CombatEffectDef)
	return duplicates


func _apply_skill_spec_overrides(skill_def: SkillDef, spec: Dictionary) -> void:
	if skill_def == null:
		return
	if spec.has("unlock_mode"):
		skill_def.unlock_mode = ProgressionDataUtils.to_string_name(spec.get("unlock_mode", "standard"))
	if spec.has("knowledge_requirements"):
		skill_def.knowledge_requirements = ProgressionDataUtils.to_string_name_array(spec.get("knowledge_requirements", []))
	if spec.has("skill_level_requirements"):
		skill_def.skill_level_requirements = spec.get("skill_level_requirements", {}).duplicate(true)
	if spec.has("achievement_requirements"):
		skill_def.achievement_requirements = ProgressionDataUtils.to_string_name_array(spec.get("achievement_requirements", []))
	if spec.has("upgrade_source_skill_ids"):
		skill_def.upgrade_source_skill_ids = ProgressionDataUtils.to_string_name_array(spec.get("upgrade_source_skill_ids", []))
	if spec.has("retain_source_skills_on_unlock"):
		skill_def.retain_source_skills_on_unlock = bool(spec.get("retain_source_skills_on_unlock", true))
	if spec.has("core_skill_transition_mode"):
		skill_def.core_skill_transition_mode = ProgressionDataUtils.to_string_name(spec.get("core_skill_transition_mode", "inherit"))

func _build_active_skill(
	skill_id: StringName,
	display_name: String,
	description: String,
	tags: Array[StringName],
	range_value: int,
	target_mode: StringName,
	target_team_filter: StringName,
	area_pattern: StringName,
	area_value: int,
	ap_cost: int,
	mp_cost: int,
	stamina_cost: int,
	aura_cost: int,
	cooldown_tu: int,
	hit_rate: int,
	effect_defs: Array,
	mastery_curve_values: Array[int],
	max_level: int,
	target_selection_mode: StringName = &"single_unit",
	min_target_count: int = 1,
	max_target_count: int = 1,
	selection_order_mode: StringName = &"stable",
	learn_source: StringName = &"book"
) -> SkillDef:
	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = target_mode
	combat_profile.target_team_filter = target_team_filter
	combat_profile.range_pattern = &"single"
	combat_profile.range_value = maxi(range_value, 0)
	combat_profile.area_pattern = area_pattern
	combat_profile.area_value = maxi(area_value, 0)
	combat_profile.ap_cost = maxi(ap_cost, 0)
	combat_profile.mp_cost = maxi(mp_cost, 0)
	combat_profile.stamina_cost = maxi(stamina_cost, 0)
	combat_profile.aura_cost = maxi(aura_cost, 0)
	combat_profile.cooldown_tu = maxi(cooldown_tu, 0)
	combat_profile.hit_rate = hit_rate
	combat_profile.target_selection_mode = target_selection_mode
	combat_profile.min_target_count = maxi(min_target_count, 1)
	combat_profile.max_target_count = maxi(max_target_count, combat_profile.min_target_count)
	combat_profile.selection_order_mode = selection_order_mode
	combat_profile.effect_defs.clear()
	for effect_def in _filter_effect_defs(effect_defs):
		combat_profile.effect_defs.append(effect_def)
	return _build_skill(skill_id, display_name, description, &"active", max_level, mastery_curve_values, tags, learn_source, [], [], [], combat_profile)


func _build_skill(
	skill_id: StringName,
	display_name: String,
	description: String,
	skill_type: StringName,
	max_level: int,
	mastery_curve_values: Array,
	tags: Array[StringName],
	learn_source: StringName,
	learn_requirements: Array[StringName],
	mastery_sources: Array[StringName],
	attribute_modifiers: Array,
	combat_profile: CombatSkillDef = null,
	icon_id: StringName = &""
) -> SkillDef:
	var skill_def := SkillDef.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = display_name
	skill_def.icon_id = icon_id if icon_id != &"" else skill_id
	skill_def.description = description
	skill_def.skill_type = skill_type
	skill_def.max_level = max_level
	skill_def.mastery_curve = _build_mastery_curve(mastery_curve_values)
	skill_def.learn_source = learn_source
	skill_def.combat_profile = combat_profile
	skill_def.tags.clear()
	for tag in tags:
		skill_def.tags.append(tag)
	skill_def.learn_requirements.clear()
	for skill_id_variant in learn_requirements:
		skill_def.learn_requirements.append(skill_id_variant)
	skill_def.mastery_sources.clear()
	for mastery_source in mastery_sources:
		skill_def.mastery_sources.append(mastery_source)
	skill_def.attribute_modifiers.clear()
	for modifier_variant in attribute_modifiers:
		var modifier := modifier_variant as AttributeModifier
		if modifier != null:
			skill_def.attribute_modifiers.append(modifier)
	return skill_def


func _build_mastery_curve(values: Array) -> PackedInt32Array:
	var curve := PackedInt32Array()
	for value in values:
		curve.append(int(value))
	return curve


func _build_damage_effect(
	power: int,
	scaling_attribute_id: StringName = &"physical_attack",
	defense_attribute_id: StringName = &"physical_defense",
	resistance_attribute_id: StringName = &"",
	target_team_filter: StringName = &""
) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"damage"
	effect_def.power = maxi(power, 0)
	effect_def.scaling_attribute_id = scaling_attribute_id
	effect_def.defense_attribute_id = defense_attribute_id
	if resistance_attribute_id != &"":
		effect_def.resistance_attribute_id = resistance_attribute_id
	if target_team_filter != &"":
		effect_def.effect_target_team_filter = target_team_filter
	return effect_def


func _build_heal_effect(power: int, target_team_filter: StringName = &"") -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"heal"
	effect_def.power = maxi(power, 0)
	if target_team_filter != &"":
		effect_def.effect_target_team_filter = target_team_filter
	return effect_def


func _build_status_effect(
	status_id: StringName,
	duration: int,
	power: int = 1,
	target_team_filter: StringName = &""
) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"status"
	effect_def.status_id = status_id
	effect_def.power = maxi(power, 1)
	if duration > 0:
		effect_def.params = {"duration": duration}
	if target_team_filter != &"":
		effect_def.effect_target_team_filter = target_team_filter
	return effect_def


func _build_special_effect(effect_type: StringName, params: Dictionary = {}, power: int = 0) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = effect_type
	effect_def.power = power
	effect_def.params = params.duplicate(true)
	return effect_def


func _build_forced_move_effect(distance: int, mode: StringName = &"retreat") -> CombatEffectDef:
	var effect_def := _build_special_effect(&"forced_move", {
		"distance": maxi(distance, 0),
		"mode": String(mode),
	})
	effect_def.forced_move_distance = maxi(distance, 0)
	effect_def.forced_move_mode = mode
	return effect_def


func _build_terrain_replace_effect(terrain: StringName) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"terrain_replace"
	effect_def.terrain_replace_to = terrain
	return effect_def


func _build_height_delta_effect(delta: int) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"height_delta"
	effect_def.height_delta = delta
	return effect_def


func _filter_effect_defs(effect_defs: Array) -> Array[CombatEffectDef]:
	var results: Array[CombatEffectDef] = []
	for effect_variant in effect_defs:
		var effect_def := effect_variant as CombatEffectDef
		if effect_def != null:
			results.append(effect_def)
	return results


func _build_cast_variant(
	variant_id: StringName,
	display_name: String,
	description: String,
	min_skill_level: int,
	footprint_pattern: StringName,
	required_coord_count: int,
	allowed_base_terrains: Array[StringName],
	effect_defs: Array
) -> CombatCastVariantDef:
	var cast_variant := CombatCastVariantDef.new()
	cast_variant.variant_id = variant_id
	cast_variant.display_name = display_name
	cast_variant.description = description
	cast_variant.min_skill_level = min_skill_level
	cast_variant.target_mode = &"ground"
	cast_variant.footprint_pattern = footprint_pattern
	cast_variant.required_coord_count = required_coord_count
	cast_variant.allowed_base_terrains.clear()
	for terrain_id in allowed_base_terrains:
		cast_variant.allowed_base_terrains.append(terrain_id)
	cast_variant.effect_defs.clear()
	for effect_def in _filter_effect_defs(effect_defs):
		cast_variant.effect_defs.append(effect_def)
	return cast_variant


func _build_ground_variant_skill(
	skill_id: StringName,
	display_name: String,
	description: String,
	tags: Array[StringName],
	range_value: int,
	ap_cost: int,
	mp_cost: int,
	stamina_cost: int,
	aura_cost: int,
	cooldown_tu: int,
	hit_rate: int,
	cast_variants: Array,
	learn_source: StringName = &"book"
) -> SkillDef:
	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"ground"
	combat_profile.target_team_filter = &"enemy"
	combat_profile.range_pattern = &"single"
	combat_profile.range_value = maxi(range_value, 0)
	combat_profile.area_pattern = &"single"
	combat_profile.area_value = 0
	combat_profile.ap_cost = maxi(ap_cost, 0)
	combat_profile.mp_cost = maxi(mp_cost, 0)
	combat_profile.stamina_cost = maxi(stamina_cost, 0)
	combat_profile.aura_cost = maxi(aura_cost, 0)
	combat_profile.cooldown_tu = maxi(cooldown_tu, 0)
	combat_profile.hit_rate = hit_rate
	combat_profile.cast_variants.clear()
	for cast_variant_variant in cast_variants:
		var cast_variant := cast_variant_variant as CombatCastVariantDef
		if cast_variant != null:
			combat_profile.cast_variants.append(cast_variant)
	return _build_skill(skill_id, display_name, description, &"active", 3, [28, 46, 72], tags, learn_source, [], [], [], combat_profile)






