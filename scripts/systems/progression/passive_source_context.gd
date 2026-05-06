class_name PassiveSourceContext
extends RefCounted

var member_state: PartyMemberState = null
var unit_progress: UnitProgress = null
var skill_progress_by_id: Dictionary = {}

var race_def: RaceDef = null
var subrace_def: SubraceDef = null
var trait_defs: Dictionary = {}
var bloodline_def: BloodlineDef = null
var bloodline_stage_def: BloodlineStageDef = null
var ascension_def: AscensionDef = null
var ascension_stage_def: AscensionStageDef = null
var stage_advancement_modifiers: Array = []
