class_name AttributeSourceContext
extends RefCounted

var unit_progress: UnitProgress = null
var skill_defs: Dictionary = {}
var profession_defs: Dictionary = {}

var race_def: RaceDef = null
var subrace_def: SubraceDef = null
var age_stage_rule: AgeStageRule = null
var age_stage_source_type: StringName = &""
var age_stage_source_id: StringName = &""
var bloodline_def: BloodlineDef = null
var bloodline_stage_def: BloodlineStageDef = null
var ascension_def: AscensionDef = null
var ascension_stage_def: AscensionStageDef = null
var versatility_pick: StringName = &""

var equipment_state = null
var passive_state = null
var temporary_effects = null
var stage_advancement_modifiers: Array = []
