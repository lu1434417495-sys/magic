class_name StageAdvancementApplyService
extends RefCounted

var _content_bundle: Dictionary = {}


func setup(content_bundle: Dictionary = {}) -> void:
	_content_bundle = content_bundle if content_bundle != null else {}


func add_stage_advancement_modifier(member_state: PartyMemberState, modifier_id: StringName) -> bool:
	if member_state == null or modifier_id == &"":
		return false
	var modifier := _get_content_def("stage_advancement_defs", "stage_advancement", modifier_id) as StageAdvancementModifier
	if modifier == null or modifier.modifier_id != modifier_id:
		return false
	if not _modifier_applies_to_member(modifier, member_state):
		return false
	if member_state.active_stage_advancement_modifier_ids.has(modifier_id):
		return false
	member_state.active_stage_advancement_modifier_ids.append(modifier_id)
	return true


func remove_stage_advancement_modifier(member_state: PartyMemberState, modifier_id: StringName) -> bool:
	if member_state == null or modifier_id == &"":
		return false
	if not member_state.active_stage_advancement_modifier_ids.has(modifier_id):
		return false
	member_state.active_stage_advancement_modifier_ids.erase(modifier_id)
	return true


func _modifier_applies_to_member(modifier: StageAdvancementModifier, member_state: PartyMemberState) -> bool:
	if modifier == null or member_state == null:
		return false
	if not modifier.applies_to_race_ids.is_empty() and not modifier.applies_to_race_ids.has(member_state.race_id):
		return false
	if not modifier.applies_to_subrace_ids.is_empty() and not modifier.applies_to_subrace_ids.has(member_state.subrace_id):
		return false
	if not modifier.applies_to_bloodline_ids.is_empty() and not modifier.applies_to_bloodline_ids.has(member_state.bloodline_id):
		return false
	if not modifier.applies_to_ascension_ids.is_empty() and not modifier.applies_to_ascension_ids.has(member_state.ascension_id):
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
