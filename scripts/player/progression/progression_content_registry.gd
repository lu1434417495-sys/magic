class_name ProgressionContentRegistry
extends RefCounted

const HP_MAX: StringName = &"hp_max"

var _skill_defs: Dictionary = {}
var _profession_defs: Dictionary = {}
var _validation_errors: Array[String] = []


func _init() -> void:
	rebuild()


func rebuild() -> void:
	_skill_defs.clear()
	_profession_defs.clear()
	_validation_errors.clear()

	_register_seed_melee_skills()
	_register_warrior_content()
	_validation_errors.append_array(_collect_validation_errors())


func get_skill_defs() -> Dictionary:
	return _skill_defs


func get_profession_defs() -> Dictionary:
	return _profession_defs


func get_bundle() -> Dictionary:
	return {
		"skill_defs": _skill_defs,
		"profession_defs": _profession_defs,
	}


func validate() -> Array[String]:
	return _validation_errors.duplicate()


func _register_seed_melee_skills() -> void:
	_register_skill(
		_build_skill(
			&"basic_sword",
			"基础剑术",
			"战士的基础近战技巧，强调稳定挥砍与步伐控制。",
			&"active",
			3,
			[35, 55, 80],
			[&"melee", &"sword", &"weapon"],
			&"book",
			[],
			[&"training", &"battle"]
		)
	)
	_register_skill(
		_build_skill(
			&"basic_axe",
			"基础斧术",
			"更强调力量传导与破甲手感的基础近战技能。",
			&"active",
			3,
			[40, 60, 85],
			[&"melee", &"axe", &"weapon"],
			&"book",
			[],
			[&"training", &"battle"]
		)
	)
	_register_skill(
		_build_skill(
			&"basic_spear",
			"基础枪术",
			"偏向距离控制和刺击节奏的基础近战技能。",
			&"active",
			3,
			[35, 60, 90],
			[&"melee", &"spear", &"weapon"],
			&"book",
			[],
			[&"training", &"battle"]
		)
	)
	_register_skill(
		_build_skill(
			&"basic_mace",
			"基础锤术",
			"偏向冲击和破势的基础近战技能。",
			&"active",
			3,
			[40, 65, 90],
			[&"melee", &"mace", &"weapon"],
			&"book",
			[],
			[&"training", &"battle"]
		)
	)
	_register_skill(
		_build_skill(
			&"hybrid_poleblade",
			"复合刃枪式",
			"由近战技能融合后形成的复合兵器技巧，用于验证融合技能仍可保留近战标签。",
			&"active",
			3,
			[45, 70, 95],
			[&"melee", &"fusion", &"weapon"],
			&"book",
			[],
			[&"training", &"battle"]
		)
	)
	_register_skill(
		_build_skill(
			&"warrior_basic_physique_i",
			"战士被动 I：基础体魄",
			"常规战士训练带来的体魄提升。",
			&"passive",
			1,
			[],
			[&"warrior", &"passive"],
			&"profession",
			[],
			[],
			[
				_build_modifier(HP_MAX, 10),
				_build_modifier(PlayerBaseAttributes.STRENGTH, 1),
			]
		)
	)
	_register_skill(
		_build_skill(
			&"warrior_basic_footwork_i",
			"战士被动 II：基础步法",
			"训练后的移动和调整能力提升。",
			&"passive",
			1,
			[],
			[&"warrior", &"passive"],
			&"profession",
			[],
			[],
			[
				_build_modifier(HP_MAX, 5),
				_build_modifier(PlayerBaseAttributes.AGILITY, 1),
			]
		)
	)
	_register_skill(
		_build_skill(
			&"warrior_weapon_familiarity_i",
			"战士被动 III：武器熟悉",
			"随着实战经验增长，战士对常见兵器的掌控更稳定。",
			&"passive",
			1,
			[],
			[&"warrior", &"passive"],
			&"profession",
			[],
			[],
			[
				_build_modifier(HP_MAX, 10),
				_build_modifier(PlayerBaseAttributes.STRENGTH, 1),
			]
		)
	)
	_register_skill(
		_build_skill(
			&"warrior_battle_adaptation_i",
			"战士被动 IV：战斗适应",
			"战场应变和站位调整能力继续提升。",
			&"passive",
			1,
			[],
			[&"warrior", &"passive"],
			&"profession",
			[],
			[],
			[
				_build_modifier(HP_MAX, 5),
				_build_modifier(PlayerBaseAttributes.AGILITY, 1),
			]
		)
	)
	_register_skill(
		_build_skill(
			&"warrior_veteran_body_i",
			"战士被动 V：老兵之躯",
			"形成稳定体能底盘与基础近战素养的高级被动。",
			&"passive",
			1,
			[],
			[&"warrior", &"passive"],
			&"profession",
			[],
			[],
			[
				_build_modifier(HP_MAX, 10),
				_build_modifier(PlayerBaseAttributes.STRENGTH, 1),
				_build_modifier(PlayerBaseAttributes.AGILITY, 1),
			]
		)
	)


func _register_warrior_content() -> void:
	var unlock_requirement := ProfessionPromotionRequirement.new()
	unlock_requirement.required_tag_rules = [
		_build_tag_requirement(
			&"melee",
			3,
			TagRequirement.SKILL_STATE_LEARNED,
			TagRequirement.ORIGIN_FILTER_UNMERGED_ONLY,
			TagRequirement.SELECTION_ROLE_QUALIFIER
		),
		_build_tag_requirement(
			&"melee",
			1,
			TagRequirement.SKILL_STATE_CORE_MAX,
			TagRequirement.ORIGIN_FILTER_UNMERGED_ONLY,
			TagRequirement.SELECTION_ROLE_ASSIGNED_CORE
		),
	]
	unlock_requirement.assigned_core_must_be_subset_of_qualifiers = true

	var rank_requirements: Array[ProfessionRankRequirement] = []
	for target_rank in range(2, 6):
		var rank_requirement := ProfessionRankRequirement.new()
		rank_requirement.target_rank = target_rank
		rank_requirement.required_tag_rules = [
			_build_tag_requirement(
				&"melee",
				target_rank,
				TagRequirement.SKILL_STATE_CORE_MAX,
				TagRequirement.ORIGIN_FILTER_UNMERGED_ONLY,
				TagRequirement.SELECTION_ROLE_ASSIGNED_CORE
			),
		]
		rank_requirements.append(rank_requirement)

	var profession_def := ProfessionDef.new()
	profession_def.profession_id = &"warrior"
	profession_def.display_name = "战士"
	profession_def.description = "门槛不高的通用近战职业。战士等级只提供基础体能与战斗素养，具体风格由玩家后续学习和融合出的技能体系决定。"
	profession_def.max_rank = 5
	profession_def.unlock_requirement = unlock_requirement
	profession_def.rank_requirements = rank_requirements
	profession_def.granted_skills = [
		_build_granted_skill(&"warrior_basic_physique_i", 1),
		_build_granted_skill(&"warrior_basic_footwork_i", 2),
		_build_granted_skill(&"warrior_weapon_familiarity_i", 3),
		_build_granted_skill(&"warrior_battle_adaptation_i", 4),
		_build_granted_skill(&"warrior_veteran_body_i", 5),
	]

	_register_profession(profession_def)


func _build_skill(
	skill_id: StringName,
	display_name: String,
	description: String,
	skill_type: StringName,
	max_level: int,
	mastery_curve_values: Array,
	tags: Array[StringName],
	learn_source: StringName,
	learn_requirements: Array[StringName],
	mastery_sources: Array[StringName],
	attribute_modifiers: Array[AttributeModifier] = []
) -> SkillDef:
	var skill_def := SkillDef.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = display_name
	skill_def.description = description
	skill_def.skill_type = skill_type
	skill_def.max_level = max_level
	skill_def.mastery_curve = _build_mastery_curve(mastery_curve_values)
	skill_def.tags = tags.duplicate()
	skill_def.learn_source = learn_source
	skill_def.learn_requirements = learn_requirements.duplicate()
	skill_def.mastery_sources = mastery_sources.duplicate()
	skill_def.attribute_modifiers = attribute_modifiers.duplicate()
	return skill_def


func _build_mastery_curve(values: Array) -> PackedInt32Array:
	var curve := PackedInt32Array()
	for value in values:
		curve.append(int(value))
	return curve


func _build_modifier(attribute_id: StringName, value: int) -> AttributeModifier:
	var modifier := AttributeModifier.new()
	modifier.attribute_id = attribute_id
	modifier.mode = AttributeModifier.MODE_FLAT
	modifier.value = value
	return modifier


func _build_tag_requirement(
	tag: StringName,
	count: int,
	skill_state: StringName,
	origin_filter: StringName,
	selection_role: StringName
) -> TagRequirement:
	var requirement := TagRequirement.new()
	requirement.tag = tag
	requirement.count = count
	requirement.skill_state = skill_state
	requirement.origin_filter = origin_filter
	requirement.selection_role = selection_role
	return requirement


func _build_granted_skill(skill_id: StringName, unlock_rank: int) -> ProfessionGrantedSkill:
	var granted_skill := ProfessionGrantedSkill.new()
	granted_skill.skill_id = skill_id
	granted_skill.unlock_rank = unlock_rank
	granted_skill.skill_type = &"passive"
	return granted_skill


func _register_skill(skill_def: SkillDef) -> void:
	if skill_def == null or skill_def.skill_id == &"":
		_validation_errors.append("Encountered a skill definition without a skill_id.")
		return
	if _skill_defs.has(skill_def.skill_id):
		_validation_errors.append("Duplicate skill_id registered: %s" % String(skill_def.skill_id))
		return
	_skill_defs[skill_def.skill_id] = skill_def


func _register_profession(profession_def: ProfessionDef) -> void:
	if profession_def == null or profession_def.profession_id == &"":
		_validation_errors.append("Encountered a profession definition without a profession_id.")
		return
	if _profession_defs.has(profession_def.profession_id):
		_validation_errors.append("Duplicate profession_id registered: %s" % String(profession_def.profession_id))
		return
	_profession_defs[profession_def.profession_id] = profession_def


func _collect_validation_errors() -> Array[String]:
	var errors: Array[String] = []

	for profession_key in ProgressionDataUtils.sorted_string_keys(_profession_defs):
		var profession_id := StringName(profession_key)
		var profession_def := _profession_defs.get(profession_id) as ProfessionDef
		if profession_def == null:
			continue

		for granted_skill in profession_def.granted_skills:
			if granted_skill == null:
				continue
			if not _skill_defs.has(granted_skill.skill_id):
				errors.append(
					"Profession %s grants missing skill %s." % [String(profession_id), String(granted_skill.skill_id)]
				)

		var expected_rank := 2
		while expected_rank <= profession_def.max_rank:
			if profession_def.get_rank_requirement(expected_rank) == null:
				errors.append(
					"Profession %s is missing a rank requirement for rank %d." % [String(profession_id), expected_rank]
				)
			expected_rank += 1

		if profession_def.unlock_requirement != null:
			for tag_rule in profession_def.unlock_requirement.required_tag_rules:
				_append_invalid_tag_rule_errors(errors, profession_id, tag_rule, "unlock")

		for rank_requirement in profession_def.rank_requirements:
			if rank_requirement == null:
				continue
			for tag_rule in rank_requirement.required_tag_rules:
				_append_invalid_tag_rule_errors(
					errors,
					profession_id,
					tag_rule,
					"rank_%d" % rank_requirement.target_rank
				)

	return errors


func _append_invalid_tag_rule_errors(
	errors: Array[String],
	profession_id: StringName,
	tag_rule: TagRequirement,
	context_label: String
) -> void:
	if tag_rule == null:
		return
	if tag_rule.tag == &"":
		errors.append(
			"Profession %s has an empty tag requirement in %s." % [String(profession_id), String(context_label)]
		)
	if tag_rule.count <= 0:
		errors.append(
			"Profession %s has a non-positive tag count in %s for tag %s." % [
				String(profession_id),
				String(context_label),
				String(tag_rule.tag),
			]
		)
