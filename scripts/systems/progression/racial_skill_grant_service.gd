## 文件说明：集中处理身份来源技能的补授与孤儿授予回收。
## 审查重点：重点核对身份内容桶读取、授予来源 key、职业授予保留规则以及刷新成长运行时状态的时机。
## 备注：该服务不持有队伍状态；调用方负责传入当前内容 bundle 与技能/职业定义。

class_name RacialSkillGrantService
extends RefCounted

const PROGRESSION_SERVICE_SCRIPT = preload("res://scripts/systems/progression/progression_service.gd")


static func backfill_party(
	party_state,
	content_bundle: Dictionary,
	skill_defs: Dictionary,
	profession_defs: Dictionary,
	progression_service_factory: Callable = Callable()
) -> bool:
	if party_state == null:
		return false

	var changed := false
	for member_state in party_state.member_states.values():
		changed = backfill_member(
			member_state,
			content_bundle,
			skill_defs,
			profession_defs,
			progression_service_factory
		) or changed
	return changed


static func revoke_orphan_party(
	party_state,
	content_bundle: Dictionary,
	skill_defs: Dictionary,
	profession_defs: Dictionary,
	progression_service_factory: Callable = Callable()
) -> bool:
	if party_state == null:
		return false

	var changed := false
	for member_state in party_state.member_states.values():
		changed = revoke_orphan_member(
			member_state,
			content_bundle,
			skill_defs,
			profession_defs,
			progression_service_factory
		) or changed
	return changed


static func backfill_member(
	member_state,
	content_bundle: Dictionary,
	skill_defs: Dictionary,
	profession_defs: Dictionary,
	progression_service_factory: Callable = Callable()
) -> bool:
	if member_state == null or member_state.progression == null:
		return false

	var grant_entries := collect_member_racial_grant_entries(member_state, content_bundle)
	if grant_entries.is_empty():
		return false

	var progression_service: ProgressionService = _build_progression_service(
		member_state.progression,
		skill_defs,
		profession_defs,
		progression_service_factory
	)
	var changed := false
	for grant_entry in grant_entries:
		var grant := grant_entry.get("grant") as RacialGrantedSkill
		var source_type := ProgressionDataUtils.to_string_name(grant_entry.get("source_type", ""))
		var source_id := ProgressionDataUtils.to_string_name(grant_entry.get("source_id", ""))
		if progression_service.grant_racial_skill(grant, source_type, source_id):
			changed = true
	return changed


static func revoke_orphan_member(
	member_state,
	content_bundle: Dictionary,
	skill_defs: Dictionary,
	profession_defs: Dictionary,
	progression_service_factory: Callable = Callable()
) -> bool:
	if member_state == null or member_state.progression == null:
		return false

	var active_grant_lookup := collect_active_identity_grant_lookup(member_state, content_bundle)
	var skill_ids_to_remove: Array[StringName] = []
	for skill_key in ProgressionDataUtils.sorted_string_keys(member_state.progression.skills):
		var skill_id := StringName(skill_key)
		var skill_progress: Variant = member_state.progression.get_skill_progress(skill_id)
		if skill_progress == null:
			continue
		var source_type := ProgressionDataUtils.to_string_name(skill_progress.granted_source_type)
		if not is_racial_granted_source_type(source_type):
			continue
		var source_id := ProgressionDataUtils.to_string_name(skill_progress.granted_source_id)
		if active_grant_lookup.has(identity_grant_key(source_type, source_id, skill_id)):
			continue
		if skill_progress.profession_granted_by != &"":
			continue
		skill_ids_to_remove.append(skill_id)

	if skill_ids_to_remove.is_empty():
		return false
	for skill_id in skill_ids_to_remove:
		member_state.progression.remove_skill_progress(skill_id)

	var progression_service: ProgressionService = _build_progression_service(
		member_state.progression,
		skill_defs,
		profession_defs,
		progression_service_factory
	)
	progression_service.refresh_runtime_state()
	return true


static func collect_member_racial_grant_entries(member_state, content_bundle: Dictionary) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	if member_state == null:
		return entries

	var race_def := _get_content_def(content_bundle, "race_defs", "race", member_state.race_id) as RaceDef
	if race_def != null:
		_append_racial_grant_entries(
			entries,
			race_def.racial_granted_skills,
			UnitSkillProgress.GRANTED_SOURCE_RACE,
			member_state.race_id
		)

	var subrace_def := _get_content_def(content_bundle, "subrace_defs", "subrace", member_state.subrace_id) as SubraceDef
	if subrace_def != null:
		_append_racial_grant_entries(
			entries,
			subrace_def.racial_granted_skills,
			UnitSkillProgress.GRANTED_SOURCE_SUBRACE,
			member_state.subrace_id
		)

	if member_state.bloodline_id != &"":
		var bloodline_def := _get_content_def(content_bundle, "bloodline_defs", "bloodline", member_state.bloodline_id) as BloodlineDef
		if bloodline_def != null:
			_append_racial_grant_entries(
				entries,
				bloodline_def.racial_granted_skills,
				UnitSkillProgress.GRANTED_SOURCE_BLOODLINE,
				member_state.bloodline_id
			)
	if member_state.bloodline_stage_id != &"":
		var bloodline_stage_def := _get_content_def(content_bundle, "bloodline_stage_defs", "bloodline_stage", member_state.bloodline_stage_id) as BloodlineStageDef
		if bloodline_stage_def != null:
			_append_racial_grant_entries(
				entries,
				bloodline_stage_def.racial_granted_skills,
				UnitSkillProgress.GRANTED_SOURCE_BLOODLINE,
				member_state.bloodline_stage_id
			)

	if member_state.ascension_id != &"":
		var ascension_def := _get_content_def(content_bundle, "ascension_defs", "ascension", member_state.ascension_id) as AscensionDef
		if ascension_def != null:
			_append_racial_grant_entries(
				entries,
				ascension_def.racial_granted_skills,
				UnitSkillProgress.GRANTED_SOURCE_ASCENSION,
				member_state.ascension_id
			)
	if member_state.ascension_stage_id != &"":
		var ascension_stage_def := _get_content_def(content_bundle, "ascension_stage_defs", "ascension_stage", member_state.ascension_stage_id) as AscensionStageDef
		if ascension_stage_def != null:
			_append_racial_grant_entries(
				entries,
				ascension_stage_def.racial_granted_skills,
				UnitSkillProgress.GRANTED_SOURCE_ASCENSION,
				member_state.ascension_stage_id
			)

	return entries


static func collect_active_identity_grant_lookup(member_state, content_bundle: Dictionary) -> Dictionary:
	var lookup: Dictionary = {}
	if member_state == null:
		return lookup
	for grant_entry in collect_member_racial_grant_entries(member_state, content_bundle):
		var grant := grant_entry.get("grant") as RacialGrantedSkill
		if grant == null or grant.skill_id == &"":
			continue
		var source_type := ProgressionDataUtils.to_string_name(grant_entry.get("source_type", ""))
		var source_id := ProgressionDataUtils.to_string_name(grant_entry.get("source_id", ""))
		if source_type == &"" or source_id == &"":
			continue
		lookup[identity_grant_key(source_type, source_id, grant.skill_id)] = true
	return lookup


static func identity_grant_key(source_type: StringName, source_id: StringName, skill_id: StringName) -> String:
	return "%s:%s:%s" % [String(source_type), String(source_id), String(skill_id)]


static func is_racial_granted_source_type(source_type: StringName) -> bool:
	return source_type == UnitSkillProgress.GRANTED_SOURCE_RACE \
		or source_type == UnitSkillProgress.GRANTED_SOURCE_SUBRACE \
		or source_type == UnitSkillProgress.GRANTED_SOURCE_ASCENSION \
		or source_type == UnitSkillProgress.GRANTED_SOURCE_BLOODLINE


static func _append_racial_grant_entries(
	entries: Array[Dictionary],
	granted_skills: Array,
	source_type: StringName,
	source_id: StringName
) -> void:
	if source_id == &"":
		return
	for grant in granted_skills:
		if grant == null:
			continue
		entries.append({
			"grant": grant,
			"source_type": source_type,
			"source_id": source_id,
		})


static func _get_content_def(content_bundle: Dictionary, primary_bucket: String, alias_bucket: String, entry_id: StringName):
	if entry_id == &"":
		return null
	var bucket := _get_content_bucket(content_bundle, primary_bucket, alias_bucket)
	return bucket.get(entry_id)


static func _get_content_bucket(content_bundle: Dictionary, primary_bucket: String, alias_bucket: String) -> Dictionary:
	var bucket_variant: Variant = content_bundle.get(primary_bucket, {})
	if bucket_variant is Dictionary:
		return bucket_variant
	bucket_variant = content_bundle.get(alias_bucket, {})
	if bucket_variant is Dictionary:
		return bucket_variant
	return {}


static func _build_progression_service(
	progression_state,
	skill_defs: Dictionary,
	profession_defs: Dictionary,
	progression_service_factory: Callable
) -> ProgressionService:
	if progression_service_factory.is_valid():
		var custom_service: Variant = progression_service_factory.call(progression_state)
		if custom_service is ProgressionService:
			return custom_service

	var progression_service: ProgressionService = PROGRESSION_SERVICE_SCRIPT.new()
	progression_service.setup(progression_state, skill_defs, profession_defs)
	return progression_service
