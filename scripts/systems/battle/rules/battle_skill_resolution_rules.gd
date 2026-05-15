class_name BattleSkillResolutionRules
extends RefCounted

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const BattleSaveResolver = preload("res://scripts/systems/battle/rules/battle_save_resolver.gd")
const BattleTargetTeamRules = preload("res://scripts/systems/battle/rules/battle_target_team_rules.gd")

const BLACK_CONTRACT_PUSH_SKILL_ID: StringName = &"black_contract_push"
const FATE_PREVIEW_MODE_NONE: StringName = &""
const FATE_PREVIEW_MODE_STANDARD: StringName = &"standard"
const FATE_PREVIEW_MODE_FORCE_HIT_NO_CRIT: StringName = &"force_hit_no_crit"


func build_skill_resolution_policy(
	skill_def: SkillDef,
	active_unit: BattleUnitState,
	skill_variant_id: StringName = &"",
	target_unit_ids_variant = [],
	target_unit: BattleUnitState = null
) -> Dictionary:
	var target_unit_ids := normalize_target_unit_ids(target_unit_ids_variant)
	var routes_to_unit_targeting := should_route_skill_command_to_unit_targeting(skill_def, target_unit_ids)
	var variant_error_message := get_skill_variant_command_error_message(
		skill_def,
		active_unit,
		skill_variant_id,
		routes_to_unit_targeting
	)
	var unit_cast_variant := resolve_unit_cast_variant(skill_def, active_unit, skill_variant_id)
	var ground_cast_variant := resolve_ground_cast_variant(skill_def, active_unit, skill_variant_id)
	var command_cast_variant := resolve_command_route_cast_variant(
		skill_def,
		active_unit,
		skill_variant_id,
		routes_to_unit_targeting
	)
	var unit_execution_cast_variant := command_cast_variant if routes_to_unit_targeting else unit_cast_variant
	var execution_cast_variant := unit_execution_cast_variant if routes_to_unit_targeting else command_cast_variant
	var effect_defs: Array[CombatEffectDef] = []
	if not variant_error_message.is_empty():
		effect_defs = []
	elif routes_to_unit_targeting:
		effect_defs = collect_unit_skill_effect_defs(skill_def, unit_execution_cast_variant, active_unit)
	else:
		effect_defs = collect_ground_unit_effect_defs(skill_def, ground_cast_variant, active_unit)
	var uses_fate_attack := routes_to_unit_targeting and should_resolve_unit_skill_as_fate_attack(
		active_unit,
		target_unit,
		skill_def,
		effect_defs
	)
	var force_hit_no_crit := uses_fate_attack and is_force_hit_no_crit_skill(skill_def)
	var fate_preview_mode := FATE_PREVIEW_MODE_NONE
	if uses_fate_attack:
		fate_preview_mode = FATE_PREVIEW_MODE_FORCE_HIT_NO_CRIT if force_hit_no_crit else FATE_PREVIEW_MODE_STANDARD
	return {
		"target_unit_ids": target_unit_ids,
		"unit_cast_variant": unit_cast_variant,
		"ground_cast_variant": ground_cast_variant,
		"command_cast_variant": command_cast_variant,
		"unit_execution_cast_variant": unit_execution_cast_variant,
		"execution_cast_variant": execution_cast_variant,
		"routes_to_unit_targeting": routes_to_unit_targeting,
		"variant_error_message": variant_error_message,
		"variant_allowed": variant_error_message.is_empty(),
		"effect_defs": effect_defs.duplicate(),
		"uses_fate_attack": uses_fate_attack,
		"force_hit_no_crit": force_hit_no_crit,
		"fate_preview_mode": fate_preview_mode,
	}


func normalize_target_unit_ids(target_unit_ids_variant) -> Array[StringName]:
	var target_unit_ids: Array[StringName] = []
	if target_unit_ids_variant is not Array:
		return target_unit_ids
	var seen_ids: Dictionary = {}
	for target_unit_id_variant in target_unit_ids_variant:
		var target_unit_id := ProgressionDataUtils.to_string_name(target_unit_id_variant)
		if target_unit_id == &"" or seen_ids.has(target_unit_id):
			continue
		seen_ids[target_unit_id] = true
		target_unit_ids.append(target_unit_id)
	return target_unit_ids


func should_route_skill_command_to_unit_targeting(skill_def: SkillDef, target_unit_ids: Array[StringName]) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	if not target_unit_ids.is_empty():
		return true
	if ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_selection_mode) == &"random_chain":
		return true
	return skill_def.combat_profile.target_mode == &"unit"


func get_skill_variant_command_error_message(
	skill_def: SkillDef,
	active_unit: BattleUnitState,
	skill_variant_id: StringName = &"",
	routes_to_unit_targeting: bool = false
) -> String:
	if skill_def == null or skill_def.combat_profile == null:
		return "技能或目标无效。"
	if skill_def.combat_profile.cast_variants.is_empty():
		return "技能形态无效或尚未解锁。" if skill_variant_id != &"" else ""
	var skill_level := _get_unit_skill_level(active_unit, skill_def.skill_id)
	var unlocked_variants := skill_def.combat_profile.get_unlocked_cast_variants(skill_level)
	var matching_mode_variants: Array[CombatCastVariantDef] = []
	var expected_target_mode := get_command_route_cast_variant_target_mode(skill_def, routes_to_unit_targeting)
	for cast_variant in unlocked_variants:
		if cast_variant != null and get_cast_variant_target_mode(skill_def, cast_variant) == expected_target_mode:
			matching_mode_variants.append(cast_variant)
	if skill_variant_id == &"":
		if matching_mode_variants.size() > 1:
			return "技能形态不明确。"
		if matching_mode_variants.is_empty():
			return "技能形态无效或尚未解锁。"
		return ""
	for cast_variant in matching_mode_variants:
		if cast_variant != null and cast_variant.variant_id == skill_variant_id:
			return ""
	return "技能形态无效或尚未解锁。"


func should_resolve_unit_skill_as_fate_attack(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef]
) -> bool:
	if active_unit == null or target_unit == null or skill_def == null or skill_def.combat_profile == null:
		return false
	if active_unit.faction_id == target_unit.faction_id:
		return false
	if effect_defs.is_empty():
		return false
	for effect_def in effect_defs:
		if effect_def == null or effect_def.effect_type != &"damage":
			continue
		if _effect_has_save(effect_def):
			continue
		if not is_unit_valid_for_effect(active_unit, target_unit, resolve_effect_target_filter(skill_def, effect_def)):
			continue
		return true
	return false


func _effect_has_save(effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	var save_dc_mode := ProgressionDataUtils.to_string_name(effect_def.save_dc_mode)
	return save_dc_mode == BattleSaveResolver.SAVE_DC_MODE_CASTER_SPELL or effect_def.save_dc > 0


func is_force_hit_no_crit_skill(skill_def: SkillDef) -> bool:
	return skill_def != null and ProgressionDataUtils.to_string_name(skill_def.skill_id) == BLACK_CONTRACT_PUSH_SKILL_ID


func resolve_ground_cast_variant(
	skill_def: SkillDef,
	active_unit: BattleUnitState,
	skill_variant_id: StringName = &""
) -> CombatCastVariantDef:
	if skill_def == null or skill_def.combat_profile == null:
		return null
	if skill_def.combat_profile.cast_variants.is_empty():
		return _build_implicit_ground_cast_variant(skill_def) \
			if skill_def.combat_profile.target_mode == &"ground" and skill_variant_id == &"" else null

	var skill_level := _get_unit_skill_level(active_unit, skill_def.skill_id)
	var unlocked_variants := skill_def.combat_profile.get_unlocked_cast_variants(skill_level)
	if unlocked_variants.is_empty():
		return null
	if skill_variant_id == &"":
		var ground_variants: Array[CombatCastVariantDef] = []
		for cast_variant in unlocked_variants:
			if cast_variant != null and get_cast_variant_target_mode(skill_def, cast_variant) == &"ground":
				ground_variants.append(cast_variant)
		return ground_variants[0] if ground_variants.size() == 1 else null

	for cast_variant in unlocked_variants:
		if cast_variant != null \
			and cast_variant.variant_id == skill_variant_id \
			and get_cast_variant_target_mode(skill_def, cast_variant) == &"ground":
			return cast_variant
	return null


func resolve_unit_cast_variant(
	skill_def: SkillDef,
	active_unit: BattleUnitState,
	skill_variant_id: StringName = &""
) -> CombatCastVariantDef:
	if skill_def == null or skill_def.combat_profile == null:
		return null
	if skill_def.combat_profile.cast_variants.is_empty():
		return null

	var skill_level := _get_unit_skill_level(active_unit, skill_def.skill_id)
	var unlocked_variants := skill_def.combat_profile.get_unlocked_cast_variants(skill_level)
	if unlocked_variants.is_empty():
		return null
	if skill_variant_id == &"":
		var unit_variants: Array[CombatCastVariantDef] = []
		for cast_variant in unlocked_variants:
			if cast_variant != null and get_cast_variant_target_mode(skill_def, cast_variant) == &"unit":
				unit_variants.append(cast_variant)
		return unit_variants[0] if unit_variants.size() == 1 else null

	for cast_variant in unlocked_variants:
		if cast_variant != null \
			and cast_variant.variant_id == skill_variant_id \
			and get_cast_variant_target_mode(skill_def, cast_variant) == &"unit":
			return cast_variant
	return null


func resolve_command_route_cast_variant(
	skill_def: SkillDef,
	active_unit: BattleUnitState,
	skill_variant_id: StringName = &"",
	routes_to_unit_targeting: bool = false
) -> CombatCastVariantDef:
	var target_mode := get_command_route_cast_variant_target_mode(skill_def, routes_to_unit_targeting)
	match target_mode:
		&"unit":
			return resolve_unit_cast_variant(skill_def, active_unit, skill_variant_id)
		&"ground":
			return resolve_ground_cast_variant(skill_def, active_unit, skill_variant_id)
		_:
			return null


func get_command_route_cast_variant_target_mode(
	skill_def: SkillDef,
	routes_to_unit_targeting: bool = false
) -> StringName:
	if skill_def == null or skill_def.combat_profile == null:
		return &""
	if not routes_to_unit_targeting:
		return &"ground"
	return skill_def.combat_profile.target_mode


func get_cast_variant_target_mode(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> StringName:
	if cast_variant == null:
		return &""
	if cast_variant.target_mode != &"":
		return cast_variant.target_mode
	if skill_def != null and skill_def.combat_profile != null:
		return skill_def.combat_profile.target_mode
	return &""


func collect_unit_skill_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	var effect_defs: Array[CombatEffectDef] = []
	var skill_level := _get_unit_skill_level(active_unit, skill_def.skill_id if skill_def != null else &"")
	if skill_def != null and skill_def.combat_profile != null:
		for effect_def in skill_def.combat_profile.effect_defs:
			if effect_def != null and _is_effect_unlocked_for_skill_level(effect_def, skill_level, active_unit != null):
				effect_defs.append(effect_def)
	if cast_variant != null:
		for effect_def in cast_variant.effect_defs:
			if effect_def != null and _is_effect_unlocked_for_skill_level(effect_def, skill_level, active_unit != null):
				effect_defs.append(effect_def)
	return effect_defs


func collect_ground_unit_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	var effect_defs: Array[CombatEffectDef] = []
	for effect_def in collect_ground_effect_defs(skill_def, cast_variant, active_unit):
		if is_unit_effect(effect_def):
			effect_defs.append(effect_def)
	return effect_defs


func collect_ground_terrain_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	var effect_defs: Array[CombatEffectDef] = []
	for effect_def in collect_ground_effect_defs(skill_def, cast_variant, active_unit):
		if is_terrain_effect(effect_def):
			effect_defs.append(effect_def)
	return effect_defs


func collect_ground_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	var effect_defs: Array[CombatEffectDef] = []
	var skill_level := _get_unit_skill_level(active_unit, skill_def.skill_id if skill_def != null else &"")
	if skill_def != null and skill_def.combat_profile != null:
		for effect_def in skill_def.combat_profile.effect_defs:
			if effect_def != null and _is_effect_unlocked_for_skill_level(effect_def, skill_level, active_unit != null):
				effect_defs.append(effect_def)
	if cast_variant != null:
		for effect_def in cast_variant.effect_defs:
			if effect_def != null and _is_effect_unlocked_for_skill_level(effect_def, skill_level, active_unit != null):
				effect_defs.append(effect_def)
	return effect_defs


func find_repeat_attack_effect(effect_defs: Array[CombatEffectDef]) -> CombatEffectDef:
	for effect_def in effect_defs:
		if effect_def != null and effect_def.effect_type == &"repeat_attack_until_fail":
			return effect_def
	return null


func is_unit_effect(effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	return effect_def.effect_type == &"damage" \
		or effect_def.effect_type == &"chain_damage" \
		or effect_def.effect_type == &"equipment_durability_damage" \
		or effect_def.effect_type == &"dispel_magic" \
		or effect_def.effect_type == &"heal" \
		or effect_def.effect_type == &"stamina_restore" \
		or effect_def.effect_type == &"shield" \
		or effect_def.effect_type == &"layered_barrier" \
		or effect_def.effect_type == &"on_kill_gain_resources" \
		or effect_def.effect_type == &"status" \
		or effect_def.effect_type == &"apply_status" \
		or effect_def.effect_type == &"body_size_category_override" \
		or effect_def.effect_type == &"forced_move" \
		or effect_def.effect_type == &"execute"


func is_terrain_effect(effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	return effect_def.effect_type == &"terrain" \
		or effect_def.effect_type == &"terrain_replace" \
		or effect_def.effect_type == &"terrain_replace_to" \
		or effect_def.effect_type == &"height" \
		or effect_def.effect_type == &"height_delta" \
		or effect_def.effect_type == &"terrain_effect" \
		or effect_def.effect_type == &"edge_clear"


func resolve_effect_target_filter(skill_def: SkillDef, effect_def: CombatEffectDef) -> StringName:
	return BattleTargetTeamRules.resolve_effect_target_filter(skill_def, effect_def)


func is_unit_valid_for_effect(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	target_team_filter: StringName
) -> bool:
	return BattleTargetTeamRules.is_unit_valid_for_filter(source_unit, target_unit, target_team_filter)


func _get_unit_skill_level(active_unit: BattleUnitState, skill_id: StringName) -> int:
	if active_unit == null or skill_id == &"":
		return 0
	return maxi(int(active_unit.known_skill_level_map.get(skill_id, 0)), 0)


func _is_effect_unlocked_for_skill_level(
	effect_def: CombatEffectDef,
	skill_level: int,
	should_filter: bool
) -> bool:
	if effect_def == null:
		return false
	if not should_filter:
		return true
	var min_level := maxi(int(effect_def.min_skill_level), 0)
	var max_level := int(effect_def.max_skill_level)
	if skill_level < min_level:
		return false
	return max_level < 0 or skill_level <= max_level


func _build_implicit_ground_cast_variant(skill_def: SkillDef) -> CombatCastVariantDef:
	var cast_variant := CombatCastVariantDef.new()
	cast_variant.variant_id = &""
	cast_variant.display_name = ""
	cast_variant.target_mode = &"ground"
	cast_variant.footprint_pattern = &"single"
	cast_variant.required_coord_count = 1
	cast_variant.effect_defs = skill_def.combat_profile.effect_defs.duplicate()
	return cast_variant
