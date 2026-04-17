class_name EnemyContentSeed
extends Resource

const EnemyAiBrainDef = preload("res://scripts/enemies/enemy_ai_brain_def.gd")
const EnemyTemplateDef = preload("res://scripts/enemies/enemy_template_def.gd")
const WildEncounterRosterDef = preload("res://scripts/enemies/wild_encounter_roster_def.gd")

@export var enemy_ai_brains: Array[EnemyAiBrainDef] = []
@export var enemy_templates: Array[EnemyTemplateDef] = []
@export var wild_encounter_rosters: Array[WildEncounterRosterDef] = []
