class_name AscensionStageDef
extends Resource

@export var stage_id: StringName = &""
@export var ascension_id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""

@export var attribute_modifiers: Array[AttributeModifier] = []
@export var trait_ids: Array[StringName] = []
@export var racial_granted_skills: Array[RacialGrantedSkill] = []
@export_enum("none:0", "small:1", "medium:2", "large:3", "huge:4") var body_size_override: int = 0
@export var trait_summary: Array[String] = []
