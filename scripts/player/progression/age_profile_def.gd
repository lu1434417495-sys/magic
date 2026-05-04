class_name AgeProfileDef
extends Resource

@export var profile_id: StringName = &""
@export var race_id: StringName = &""

@export var child_age: int = 0
@export var teen_age: int = 12
@export var young_adult_age: int = 16
@export var adult_age: int = 18
@export var middle_age: int = 35
@export var old_age: int = 53
@export var venerable_age: int = 70
@export var max_natural_age: int = 90

@export var stage_rules: Array[AgeStageRule] = []
@export var creation_stage_ids: Array[StringName] = []
@export var default_age_by_stage: Dictionary = {}
