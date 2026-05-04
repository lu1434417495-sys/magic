class_name AgeStageRule
extends Resource

@export var stage_id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""

@export var attribute_modifiers: Array[AttributeModifier] = []
@export var trait_ids: Array[StringName] = []
@export var trait_summary: Array[String] = []
@export var selectable_in_creation: bool = true
@export var reachable_by_aging: bool = true
