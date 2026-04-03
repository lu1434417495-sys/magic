class_name TagRequirement
extends Resource

const SKILL_STATE_LEARNED: StringName = &"learned"
const SKILL_STATE_CORE: StringName = &"core"
const SKILL_STATE_CORE_MAX: StringName = &"core_max"

const ORIGIN_FILTER_ANY: StringName = &"any"
const ORIGIN_FILTER_UNMERGED_ONLY: StringName = &"unmerged_only"
const ORIGIN_FILTER_MERGED_ONLY: StringName = &"merged_only"

const SELECTION_ROLE_ASSIGNED_CORE: StringName = &"assigned_core"
const SELECTION_ROLE_QUALIFIER: StringName = &"qualifier"

@export var tag: StringName = &""
@export var count := 1
@export var skill_state: StringName = SKILL_STATE_CORE_MAX
@export var origin_filter: StringName = ORIGIN_FILTER_ANY
@export var selection_role: StringName = SELECTION_ROLE_ASSIGNED_CORE


func get_normalized_skill_state() -> StringName:
	match skill_state:
		SKILL_STATE_LEARNED, SKILL_STATE_CORE, SKILL_STATE_CORE_MAX:
			return skill_state
		_:
			return SKILL_STATE_CORE_MAX


func get_normalized_origin_filter() -> StringName:
	match origin_filter:
		ORIGIN_FILTER_ANY, ORIGIN_FILTER_UNMERGED_ONLY, ORIGIN_FILTER_MERGED_ONLY:
			return origin_filter
		_:
			return ORIGIN_FILTER_ANY


func get_normalized_selection_role() -> StringName:
	match selection_role:
		SELECTION_ROLE_QUALIFIER, SELECTION_ROLE_ASSIGNED_CORE:
			return selection_role
		_:
			return SELECTION_ROLE_ASSIGNED_CORE
