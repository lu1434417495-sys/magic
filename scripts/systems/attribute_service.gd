class_name AttributeService
extends RefCounted

const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const DERIVED_ATTRIBUTE_RULE_SCRIPT = preload("res://scripts/player/progression/derived_attribute_rule.gd")

const HP_MAX: StringName = &"hp_max"
const MP_MAX: StringName = &"mp_max"
const STAMINA_MAX: StringName = &"stamina_max"
const ACTION_POINTS: StringName = &"action_points"
const PHYSICAL_ATTACK: StringName = &"physical_attack"
const MAGIC_ATTACK: StringName = &"magic_attack"
const PHYSICAL_DEFENSE: StringName = &"physical_defense"
const MAGIC_DEFENSE: StringName = &"magic_defense"
const HIT_RATE: StringName = &"hit_rate"
const EVASION: StringName = &"evasion"
const CRIT_RATE: StringName = &"crit_rate"
const CRIT_DAMAGE: StringName = &"crit_damage"
const SPEED: StringName = &"speed"
const FIRE_RESISTANCE: StringName = &"fire_resistance"
const BLEED_RESISTANCE: StringName = &"bleed_resistance"
const FREEZE_RESISTANCE: StringName = &"freeze_resistance"
const LIGHTNING_RESISTANCE: StringName = &"lightning_resistance"
const POISON_RESISTANCE: StringName = &"poison_resistance"
const NEGATIVE_ENERGY_RESISTANCE: StringName = &"negative_energy_resistance"

const RESOURCE_ATTRIBUTE_IDS := [
	HP_MAX,
	MP_MAX,
	STAMINA_MAX,
	ACTION_POINTS,
]

const COMBAT_ATTRIBUTE_IDS := [
	PHYSICAL_ATTACK,
	MAGIC_ATTACK,
	PHYSICAL_DEFENSE,
	MAGIC_DEFENSE,
	HIT_RATE,
	EVASION,
	CRIT_RATE,
	CRIT_DAMAGE,
	SPEED,
]

const RESISTANCE_ATTRIBUTE_IDS := [
	FIRE_RESISTANCE,
	BLEED_RESISTANCE,
	FREEZE_RESISTANCE,
	LIGHTNING_RESISTANCE,
	POISON_RESISTANCE,
	NEGATIVE_ENERGY_RESISTANCE,
]

var _player_progress: PlayerProgress
var _skill_defs: Dictionary = {}
var _profession_defs: Dictionary = {}
var _equipment_state = null
var _passive_state = null
var _temporary_effects = null
var _derived_rules: Dictionary = {}


func _init() -> void:
	_derived_rules = _build_default_rules()


func setup(
	player_progress: PlayerProgress,
	skill_defs: Variant = null,
	profession_defs: Variant = null,
	equipment_state: Variant = null,
	passive_state: Variant = null,
	temporary_effects: Variant = null
) -> void:
	_player_progress = player_progress
	_skill_defs = _index_skill_defs(skill_defs)
	_profession_defs = _index_profession_defs(profession_defs)
	_equipment_state = equipment_state
	_passive_state = passive_state
	_temporary_effects = temporary_effects


func get_base_value(attribute_id: StringName) -> int:
	var base_attributes := _get_base_attributes()
	if base_attributes == null:
		return 0
	return base_attributes.get_attribute_value(attribute_id)


func get_total_value(attribute_id: StringName) -> int:
	return get_snapshot().get_value(attribute_id)


func get_action_points() -> int:
	return get_total_value(ACTION_POINTS)


func get_resistance_value(attribute_id: StringName) -> int:
	return get_total_value(attribute_id)


func get_snapshot() -> AttributeSnapshot:
	var snapshot: AttributeSnapshot = ATTRIBUTE_SNAPSHOT_SCRIPT.new()
	var modifier_entries: Array = _collect_all_modifier_entries()
	var resolved_base_values := _resolve_base_attribute_values(modifier_entries)

	for attribute_id in PlayerBaseAttributes.BASE_ATTRIBUTE_IDS:
		snapshot.set_value(attribute_id, int(resolved_base_values.get(attribute_id, 0)))

	for attribute_id in _get_known_non_base_attribute_ids():
		var derived_value := _get_persistent_base_value(attribute_id)
		if _derived_rules.has(attribute_id):
			var rule: DerivedAttributeRule = _derived_rules.get(attribute_id) as DerivedAttributeRule
			if rule != null:
				derived_value += rule.evaluate(resolved_base_values)

		snapshot.set_value(attribute_id, _apply_modifier_pipeline(attribute_id, derived_value, modifier_entries))

	for attribute_id in _get_additional_attribute_ids(modifier_entries):
		if snapshot.has_value(attribute_id):
			continue
		snapshot.set_value(
			attribute_id,
			_apply_modifier_pipeline(attribute_id, _get_persistent_base_value(attribute_id), modifier_entries)
		)

	return snapshot


func apply_permanent_attribute_change(attribute_id: StringName, delta: int) -> bool:
	var base_attributes := _get_base_attributes()
	if base_attributes == null:
		return false

	base_attributes.set_attribute_value(attribute_id, base_attributes.get_attribute_value(attribute_id) + delta)
	return true


func _get_base_attributes() -> PlayerBaseAttributes:
	if _player_progress == null:
		return null
	return _player_progress.base_attributes


func _index_skill_defs(skill_defs: Variant) -> Dictionary:
	var indexed_defs: Dictionary = {}

	if skill_defs is Dictionary:
		for key in skill_defs.keys():
			var skill_def = skill_defs[key]
			if skill_def is SkillDef:
				var indexed_id: StringName = skill_def.skill_id if skill_def.skill_id != &"" else ProgressionDataUtils.to_string_name(key)
				indexed_defs[indexed_id] = skill_def
	elif skill_defs is Array:
		for skill_def in skill_defs:
			if skill_def is SkillDef and skill_def.skill_id != &"":
				indexed_defs[skill_def.skill_id] = skill_def

	return indexed_defs


func _index_profession_defs(profession_defs: Variant) -> Dictionary:
	var indexed_defs: Dictionary = {}

	if profession_defs is Dictionary:
		for key in profession_defs.keys():
			var profession_def = profession_defs[key]
			if profession_def is ProfessionDef:
				var indexed_id: StringName = profession_def.profession_id if profession_def.profession_id != &"" else ProgressionDataUtils.to_string_name(key)
				indexed_defs[indexed_id] = profession_def
	elif profession_defs is Array:
		for profession_def in profession_defs:
			if profession_def is ProfessionDef and profession_def.profession_id != &"":
				indexed_defs[profession_def.profession_id] = profession_def

	return indexed_defs


func _resolve_base_attribute_values(modifier_entries: Array) -> Dictionary:
	var resolved_values: Dictionary = {}
	for attribute_id in PlayerBaseAttributes.BASE_ATTRIBUTE_IDS:
		resolved_values[attribute_id] = _apply_modifier_pipeline(attribute_id, get_base_value(attribute_id), modifier_entries)
	return resolved_values


func _collect_all_modifier_entries() -> Array:
	var entries: Array = []
	_append_profession_modifier_entries(entries)
	_append_skill_modifier_entries(entries)
	_append_external_modifier_entries(entries, _equipment_state, &"equipment")
	_append_external_modifier_entries(entries, _passive_state, &"passive")
	_append_external_modifier_entries(entries, _temporary_effects, &"temporary")
	return entries


func _append_profession_modifier_entries(entries: Array) -> void:
	if _player_progress == null:
		return

	for profession_key in _player_progress.professions.keys():
		var profession_id := ProgressionDataUtils.to_string_name(profession_key)
		var profession_progress := _player_progress.get_profession_progress(profession_id)
		if profession_progress == null:
			continue
		if profession_progress.rank <= 0:
			continue
		if not profession_progress.is_active or profession_progress.is_hidden:
			continue

		var profession_def := _profession_defs.get(profession_id) as ProfessionDef
		if profession_def == null:
			continue

		_append_modifier_entries(entries, profession_def.attribute_modifiers, &"profession", profession_id, profession_progress.rank)


func _append_skill_modifier_entries(entries: Array) -> void:
	if _player_progress == null:
		return

	for skill_key in _player_progress.skills.keys():
		var skill_id := ProgressionDataUtils.to_string_name(skill_key)
		var skill_progress := _player_progress.get_skill_progress(skill_id)
		if skill_progress == null or not skill_progress.is_learned:
			continue
		if not _is_skill_modifier_active(skill_progress):
			continue

		var skill_def := _skill_defs.get(skill_id) as SkillDef
		if skill_def == null:
			continue

		var effective_rank := maxi(skill_progress.skill_level, 1)
		_append_modifier_entries(entries, skill_def.attribute_modifiers, &"skill", skill_id, effective_rank)


func _is_skill_modifier_active(skill_progress: PlayerSkillProgress) -> bool:
	if skill_progress == null:
		return false
	if skill_progress.profession_granted_by == &"":
		return true
	if _player_progress == null:
		return false

	var profession_progress := _player_progress.get_profession_progress(skill_progress.profession_granted_by)
	if profession_progress == null:
		return false
	return profession_progress.is_active and not profession_progress.is_hidden and profession_progress.rank > 0


func _append_external_modifier_entries(entries: Array, state: Variant, default_source_type: StringName) -> void:
	if state == null:
		return

	if state is Array:
		_append_modifier_entries(entries, state, default_source_type, default_source_type, 1)
		return

	if state is Dictionary:
		if state.get("attribute_modifiers", null) is Array:
			_append_modifier_entries(
				entries,
				state.get("attribute_modifiers", []),
				default_source_type,
				ProgressionDataUtils.to_string_name(state.get("source_id", default_source_type)),
				int(state.get("rank", 1))
			)
			return

		for key in state.keys():
			var modifiers = state[key]
			if modifiers is Array:
				_append_modifier_entries(entries, modifiers, default_source_type, ProgressionDataUtils.to_string_name(key), 1)
		return

	if state.has_method("get_attribute_modifiers"):
		var source_id := default_source_type
		if state.has_method("get_source_id"):
			source_id = ProgressionDataUtils.to_string_name(state.call("get_source_id"))
		_append_modifier_entries(entries, state.call("get_attribute_modifiers"), default_source_type, source_id, 1)


func _append_modifier_entries(
	entries: Array,
	modifiers: Variant,
	source_type: StringName,
	source_id: StringName,
	rank: int
) -> void:
	if modifiers is not Array:
		return

	for modifier in modifiers:
		if modifier is not AttributeModifier:
			continue
		if modifier.attribute_id == &"":
			continue

		entries.append({
			"attribute_id": modifier.attribute_id,
			"mode": modifier.mode,
			"value": modifier.get_value_for_rank(rank),
			"source_type": source_type if source_type != &"" else modifier.source_type,
			"source_id": source_id if source_id != &"" else modifier.source_id,
		})


func _get_persistent_base_value(attribute_id: StringName) -> int:
	var base_attributes := _get_base_attributes()
	if base_attributes == null:
		return 0
	return base_attributes.get_attribute_value(attribute_id)


func _apply_modifier_pipeline(attribute_id: StringName, base_value: int, modifier_entries: Array) -> int:
	var flat_delta := 0
	var percent_delta := 0

	for entry in modifier_entries:
		if ProgressionDataUtils.to_string_name(entry.get("attribute_id", "")) != attribute_id:
			continue

		var value := int(entry.get("value", 0))
		var mode := ProgressionDataUtils.to_string_name(entry.get("mode", "flat"))
		if mode == AttributeModifier.MODE_PERCENT:
			percent_delta += value
		else:
			flat_delta += value

	var result := base_value + flat_delta
	if percent_delta != 0:
		result = int(floor(float(result) * float(100 + percent_delta) / 100.0))

	return _clamp_attribute_value(attribute_id, result)


func _clamp_attribute_value(attribute_id: StringName, value: int) -> int:
	if RESISTANCE_ATTRIBUTE_IDS.has(attribute_id):
		return clampi(value, 0, 95)

	match attribute_id:
		HP_MAX:
			return maxi(value, 1)
		MP_MAX, STAMINA_MAX:
			return maxi(value, 0)
		ACTION_POINTS:
			return maxi(value, 1)
		HIT_RATE, EVASION:
			return clampi(value, 0, 100)
		CRIT_RATE:
			return clampi(value, 0, 100)
		CRIT_DAMAGE:
			return maxi(value, 100)
		SPEED:
			return maxi(value, 1)
		_:
			return value


func _get_known_non_base_attribute_ids() -> Array[StringName]:
	var result: Array[StringName] = []

	for attribute_id in RESOURCE_ATTRIBUTE_IDS:
		result.append(attribute_id)
	for attribute_id in COMBAT_ATTRIBUTE_IDS:
		result.append(attribute_id)
	for attribute_id in RESISTANCE_ATTRIBUTE_IDS:
		result.append(attribute_id)

	return result


func _get_additional_attribute_ids(modifier_entries: Array) -> Array[StringName]:
	var result: Array[StringName] = []
	var seen: Dictionary = {}
	var known_attribute_ids := _get_known_non_base_attribute_ids()

	for attribute_id in PlayerBaseAttributes.BASE_ATTRIBUTE_IDS:
		known_attribute_ids.append(attribute_id)
		seen[attribute_id] = true
	for attribute_id in known_attribute_ids:
		seen[attribute_id] = true

	var base_attributes := _get_base_attributes()
	if base_attributes != null:
		for key in base_attributes.custom_stats.keys():
			var attribute_id := ProgressionDataUtils.to_string_name(key)
			if seen.has(attribute_id):
				continue
			seen[attribute_id] = true
			result.append(attribute_id)

	for entry in modifier_entries:
		var attribute_id := ProgressionDataUtils.to_string_name(entry.get("attribute_id", ""))
		if attribute_id == &"" or seen.has(attribute_id):
			continue
		seen[attribute_id] = true
		result.append(attribute_id)

	return result


func _build_default_rules() -> Dictionary:
	var rules: Dictionary = {}

	rules[HP_MAX] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		HP_MAX,
		60,
		{
			PlayerBaseAttributes.CONSTITUTION: 8,
			PlayerBaseAttributes.STRENGTH: 2,
		},
		1,
		1
	)
	rules[MP_MAX] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		MP_MAX,
		30,
		{
			PlayerBaseAttributes.INTELLIGENCE: 6,
			PlayerBaseAttributes.WILLPOWER: 4,
		},
		1,
		0
	)
	rules[STAMINA_MAX] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		STAMINA_MAX,
		40,
		{
			PlayerBaseAttributes.CONSTITUTION: 5,
			PlayerBaseAttributes.STRENGTH: 2,
			PlayerBaseAttributes.AGILITY: 2,
		},
		1,
		0
	)
	rules[ACTION_POINTS] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		ACTION_POINTS,
		6,
		{
			PlayerBaseAttributes.AGILITY: 2,
			PlayerBaseAttributes.PERCEPTION: 1,
			PlayerBaseAttributes.WILLPOWER: 1,
		},
		6,
		1
	)
	rules[PHYSICAL_ATTACK] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		PHYSICAL_ATTACK,
		4,
		{
			PlayerBaseAttributes.STRENGTH: 8,
			PlayerBaseAttributes.CONSTITUTION: 2,
		},
		4,
		0
	)
	rules[MAGIC_ATTACK] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		MAGIC_ATTACK,
		4,
		{
			PlayerBaseAttributes.INTELLIGENCE: 8,
			PlayerBaseAttributes.WILLPOWER: 2,
		},
		4,
		0
	)
	rules[PHYSICAL_DEFENSE] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		PHYSICAL_DEFENSE,
		3,
		{
			PlayerBaseAttributes.CONSTITUTION: 8,
			PlayerBaseAttributes.STRENGTH: 2,
		},
		4,
		0
	)
	rules[MAGIC_DEFENSE] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		MAGIC_DEFENSE,
		3,
		{
			PlayerBaseAttributes.WILLPOWER: 8,
			PlayerBaseAttributes.INTELLIGENCE: 2,
		},
		4,
		0
	)
	rules[HIT_RATE] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		HIT_RATE,
		70,
		{
			PlayerBaseAttributes.PERCEPTION: 6,
			PlayerBaseAttributes.AGILITY: 2,
		},
		4,
		0,
		100
	)
	rules[EVASION] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		EVASION,
		5,
		{
			PlayerBaseAttributes.AGILITY: 8,
			PlayerBaseAttributes.PERCEPTION: 2,
		},
		4,
		0,
		100
	)
	rules[CRIT_RATE] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		CRIT_RATE,
		5,
		{
			PlayerBaseAttributes.PERCEPTION: 4,
			PlayerBaseAttributes.AGILITY: 2,
		},
		6,
		0,
		100
	)
	rules[CRIT_DAMAGE] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		CRIT_DAMAGE,
		150,
		{
			PlayerBaseAttributes.STRENGTH: 2,
			PlayerBaseAttributes.INTELLIGENCE: 2,
		},
		4,
		100
	)
	rules[SPEED] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		SPEED,
		10,
		{
			PlayerBaseAttributes.AGILITY: 8,
			PlayerBaseAttributes.PERCEPTION: 2,
		},
		4,
		1
	)
	rules[FIRE_RESISTANCE] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		FIRE_RESISTANCE,
		0,
		{
			PlayerBaseAttributes.CONSTITUTION: 2,
			PlayerBaseAttributes.WILLPOWER: 1,
		},
		3,
		0,
		95
	)
	rules[BLEED_RESISTANCE] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		BLEED_RESISTANCE,
		0,
		{
			PlayerBaseAttributes.CONSTITUTION: 2,
			PlayerBaseAttributes.WILLPOWER: 1,
		},
		2,
		0,
		95
	)
	rules[FREEZE_RESISTANCE] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		FREEZE_RESISTANCE,
		0,
		{
			PlayerBaseAttributes.CONSTITUTION: 1,
			PlayerBaseAttributes.WILLPOWER: 2,
		},
		2,
		0,
		95
	)
	rules[LIGHTNING_RESISTANCE] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		LIGHTNING_RESISTANCE,
		0,
		{
			PlayerBaseAttributes.WILLPOWER: 2,
			PlayerBaseAttributes.INTELLIGENCE: 1,
		},
		2,
		0,
		95
	)
	rules[POISON_RESISTANCE] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		POISON_RESISTANCE,
		0,
		{
			PlayerBaseAttributes.CONSTITUTION: 2,
			PlayerBaseAttributes.PERCEPTION: 1,
		},
		2,
		0,
		95
	)
	rules[NEGATIVE_ENERGY_RESISTANCE] = DERIVED_ATTRIBUTE_RULE_SCRIPT.new(
		NEGATIVE_ENERGY_RESISTANCE,
		0,
		{
			PlayerBaseAttributes.WILLPOWER: 2,
			PlayerBaseAttributes.INTELLIGENCE: 1,
		},
		2,
		0,
		95
	)

	return rules
