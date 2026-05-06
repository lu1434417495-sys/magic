class_name BloodlineApplyService
extends RefCounted

var _content_bundle: Dictionary = {}


func setup(content_bundle: Dictionary = {}) -> void:
	_content_bundle = content_bundle if content_bundle != null else {}


func apply_bloodline(member_state: PartyMemberState, bloodline_id: StringName, bloodline_stage_id: StringName) -> bool:
	if member_state == null or bloodline_id == &"" or bloodline_stage_id == &"":
		return false
	var bloodline_def := _get_content_def("bloodline_defs", "bloodline", bloodline_id) as BloodlineDef
	var stage_def := _get_content_def("bloodline_stage_defs", "bloodline_stage", bloodline_stage_id) as BloodlineStageDef
	if not _is_valid_bloodline_stage_pair(bloodline_def, stage_def, bloodline_id, bloodline_stage_id):
		return false

	member_state.bloodline_id = bloodline_id
	member_state.bloodline_stage_id = bloodline_stage_id
	return true


func revoke_bloodline(member_state: PartyMemberState) -> bool:
	if member_state == null:
		return false
	if member_state.bloodline_id == &"" and member_state.bloodline_stage_id == &"":
		return false
	member_state.bloodline_id = &""
	member_state.bloodline_stage_id = &""
	return true


func _is_valid_bloodline_stage_pair(
	bloodline_def: BloodlineDef,
	stage_def: BloodlineStageDef,
	bloodline_id: StringName,
	bloodline_stage_id: StringName
) -> bool:
	if bloodline_def == null or stage_def == null:
		return false
	if bloodline_def.bloodline_id != bloodline_id:
		return false
	if stage_def.stage_id != bloodline_stage_id:
		return false
	if stage_def.bloodline_id != bloodline_id:
		return false
	return bloodline_def.stage_ids.has(bloodline_stage_id)


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
