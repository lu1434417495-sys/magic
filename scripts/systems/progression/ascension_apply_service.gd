class_name AscensionApplyService
extends RefCounted

var _content_bundle: Dictionary = {}


func setup(content_bundle: Dictionary = {}) -> void:
	_content_bundle = content_bundle if content_bundle != null else {}


func apply_ascension(
	member_state: PartyMemberState,
	ascension_id: StringName,
	ascension_stage_id: StringName,
	current_world_step: int
) -> bool:
	if member_state == null or ascension_id == &"" or ascension_stage_id == &"" or current_world_step < 0:
		return false
	var ascension_def := _get_content_def("ascension_defs", "ascension", ascension_id) as AscensionDef
	var stage_def := _get_content_def("ascension_stage_defs", "ascension_stage", ascension_stage_id) as AscensionStageDef
	if not _is_valid_ascension_stage_pair(ascension_def, stage_def, ascension_id, ascension_stage_id):
		return false
	if not _member_matches_allowed_identity(member_state, ascension_def):
		return false

	if member_state.original_race_id_before_ascension == &"":
		member_state.original_race_id_before_ascension = member_state.race_id
	member_state.ascension_id = ascension_id
	member_state.ascension_stage_id = ascension_stage_id
	member_state.ascension_started_at_world_step = current_world_step
	return true


func revoke_ascension(member_state: PartyMemberState, restore_original_race: bool = true) -> bool:
	if member_state == null:
		return false
	if member_state.ascension_id == &"" \
			and member_state.ascension_stage_id == &"" \
			and member_state.ascension_started_at_world_step == -1 \
			and member_state.original_race_id_before_ascension == &"":
		return false
	if restore_original_race and member_state.original_race_id_before_ascension != &"":
		member_state.race_id = member_state.original_race_id_before_ascension
	member_state.ascension_id = &""
	member_state.ascension_stage_id = &""
	member_state.ascension_started_at_world_step = -1
	member_state.original_race_id_before_ascension = &""
	return true


func _is_valid_ascension_stage_pair(
	ascension_def: AscensionDef,
	stage_def: AscensionStageDef,
	ascension_id: StringName,
	ascension_stage_id: StringName
) -> bool:
	if ascension_def == null or stage_def == null:
		return false
	if ascension_def.ascension_id != ascension_id:
		return false
	if stage_def.stage_id != ascension_stage_id:
		return false
	if stage_def.ascension_id != ascension_id:
		return false
	return ascension_def.stage_ids.has(ascension_stage_id)


func _member_matches_allowed_identity(member_state: PartyMemberState, ascension_def: AscensionDef) -> bool:
	if ascension_def == null or member_state == null:
		return false
	if not ascension_def.allowed_race_ids.is_empty() and not ascension_def.allowed_race_ids.has(member_state.race_id):
		return false
	if not ascension_def.allowed_subrace_ids.is_empty() and not ascension_def.allowed_subrace_ids.has(member_state.subrace_id):
		return false
	if not ascension_def.allowed_bloodline_ids.is_empty() and not ascension_def.allowed_bloodline_ids.has(member_state.bloodline_id):
		return false
	return true


func _get_content_def(primary_bucket: String, alias_bucket: String, entry_id: StringName):
	if entry_id == &"":
		return null
	var bucket := _get_content_bucket(primary_bucket, alias_bucket)
	return bucket.get(entry_id)


func _get_content_bucket(primary_bucket: String, alias_bucket: String) -> Dictionary:
	var bucket_variant: Variant = _content_bundle.get(primary_bucket, {})
	if bucket_variant is Dictionary:
		return bucket_variant
	bucket_variant = _content_bundle.get(alias_bucket, {})
	if bucket_variant is Dictionary:
		return bucket_variant
	return {}
