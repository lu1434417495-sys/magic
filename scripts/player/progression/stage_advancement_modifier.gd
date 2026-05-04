class_name StageAdvancementModifier
extends Resource

const TARGET_AXIS_FULL: StringName = &"full"
const TARGET_AXIS_PHYSICAL: StringName = &"physical"
const TARGET_AXIS_MENTAL: StringName = &"mental"
const TARGET_AXIS_BLOODLINE: StringName = &"bloodline"
const TARGET_AXIS_DIVINE: StringName = &"divine"
const TARGET_AXIS_MARTIAL: StringName = &"martial"
const TARGET_AXIS_DOMAIN: StringName = &"domain"

const VALID_TARGET_AXES: Array[StringName] = [
	TARGET_AXIS_FULL,
	TARGET_AXIS_PHYSICAL,
	TARGET_AXIS_MENTAL,
	TARGET_AXIS_BLOODLINE,
	TARGET_AXIS_DIVINE,
	TARGET_AXIS_MARTIAL,
	TARGET_AXIS_DOMAIN,
]

@export var modifier_id: StringName = &""
@export var display_name: String = ""

@export var target_axis: StringName = TARGET_AXIS_FULL
@export var stage_offset: int = 1
@export var max_stage_id: StringName = &""

@export var applies_to_race_ids: Array[StringName] = []
@export var applies_to_subrace_ids: Array[StringName] = []
@export var applies_to_bloodline_ids: Array[StringName] = []
@export var applies_to_ascension_ids: Array[StringName] = []

@export var grants_attributes: bool = true
@export var grants_traits: bool = false
@export var grants_body_size_change: bool = false
