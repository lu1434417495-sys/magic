class_name BloodlineStageDef
extends Resource

@export var stage_id: StringName = &""
@export var bloodline_id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""

@export var attribute_modifiers: Array[AttributeModifier] = []
@export var trait_ids: Array[StringName] = []
@export var racial_granted_skills: Array[RacialGrantedSkill] = []
@export var trait_summary: Array[String] = []
