class_name SubraceDef
extends Resource

@export var subrace_id: StringName = &""
@export var parent_race_id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""

@export_enum("none:0", "small:1", "medium:2", "large:3", "huge:4") var body_size_override: int = 0
@export var speed_bonus: int = 0

@export var attribute_modifiers: Array[AttributeModifier] = []
@export var trait_ids: Array[StringName] = []
@export var racial_granted_skills: Array[RacialGrantedSkill] = []
@export var proficiency_tags: Array[StringName] = []
@export var vision_tags: Array[StringName] = []
@export var save_advantage_tags: Array[StringName] = []
@export var damage_resistances: Dictionary = {}
@export var dialogue_tags: Array[StringName] = []
@export var racial_trait_summary: Array[String] = []
