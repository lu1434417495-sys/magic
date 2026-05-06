class_name RaceDef
extends Resource

@export var race_id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""

@export var age_profile_id: StringName = &""
@export var default_subrace_id: StringName = &""
@export var subrace_ids: Array[StringName] = []

@export var body_size_category: StringName = &"medium"
@export var base_speed: int = 6

@export var attribute_modifiers: Array[AttributeModifier] = []
@export var trait_ids: Array[StringName] = []
@export var racial_granted_skills: Array[RacialGrantedSkill] = []
@export var proficiency_tags: Array[StringName] = []
@export var vision_tags: Array[StringName] = []
@export var save_advantage_tags: Array[StringName] = []
@export var damage_resistances: Dictionary = {}
@export var dialogue_tags: Array[StringName] = []
@export var racial_trait_summary: Array[String] = []
