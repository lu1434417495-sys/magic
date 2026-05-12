class_name BattleSpecialProfileManifest
extends Resource

@export var profile_id: StringName = &""
@export var schema_version: int = 1
@export var owning_skill_ids: Array[StringName] = []
@export var runtime_resolver_id: StringName = &""
@export var profile_resource: Resource = null
@export var runtime_read_policy: StringName = &"forbidden"
@export var presentation_metadata: Dictionary = {}
@export var required_regression_tests: Array[String] = []
@export var deferred_capabilities: Array[Dictionary] = []
@export var sunset_warning_date: String = ""
@export var sunset_hard_block_date: String = ""
