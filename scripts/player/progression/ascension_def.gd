class_name AscensionDef
extends Resource

@export var ascension_id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""

@export var stage_ids: Array[StringName] = []
@export var trait_ids: Array[StringName] = []
@export var racial_granted_skills: Array[RacialGrantedSkill] = []
@export var allowed_race_ids: Array[StringName] = []
@export var allowed_subrace_ids: Array[StringName] = []
@export var allowed_bloodline_ids: Array[StringName] = []
@export var trait_summary: Array[String] = []

@export var replaces_age_growth: bool = false
@export var suppresses_original_race_traits: bool = false
