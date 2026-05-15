## 文件说明：该脚本属于成长内容注册表相关的注册表脚本，集中维护技能定义集合、职业定义集合、成就定义集合等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name ProgressionContentRegistry
extends RefCounted

const SKILL_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/skill_content_registry.gd")
const PROFESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/profession_content_registry.gd")
const RACE_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/race_content_registry.gd")
const SUBRACE_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/subrace_content_registry.gd")
const RACE_TRAIT_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/race_trait_content_registry.gd")
const AGE_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/age_content_registry.gd")
const BLOODLINE_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/bloodline_content_registry.gd")
const ASCENSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/ascension_content_registry.gd")
const STAGE_ADVANCEMENT_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/stage_advancement_content_registry.gd")
const ATTRIBUTE_GROWTH_CONTENT_RULES = preload("res://scripts/player/progression/attribute_growth_content_rules.gd")
const BODY_SIZE_CONTENT_RULES = preload("res://scripts/player/progression/body_size_content_rules.gd")
const DAMAGE_TAG_CONTENT_RULES = preload("res://scripts/player/progression/damage_tag_content_rules.gd")
const PENDING_CHARACTER_REWARD_CONTENT_RULES = preload("res://scripts/player/progression/pending_character_reward_content_rules.gd")
const TRAIT_TRIGGER_CONTENT_RULES = preload("res://scripts/player/progression/trait_trigger_content_rules.gd")
const AchievementDef = preload("res://scripts/player/progression/achievement_def.gd")
const AchievementRewardDef = preload("res://scripts/player/progression/achievement_reward_def.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const BodySizeRules = BODY_SIZE_CONTENT_RULES

const HP_MAX: StringName = &"hp_max"
const VALID_SKILL_TYPES := {
	&"active": true,
	&"passive": true,
}
const VALID_LEARN_SOURCES := {
	&"book": true,
	&"innate": true,
	&"player": true,
	&"profession": true,
	&"race": true,
	&"subrace": true,
	&"ascension": true,
	&"bloodline": true,
}
const VALID_UNLOCK_MODES := {
	&"standard": true,
	&"composite_upgrade": true,
}
const VALID_CORE_SKILL_TRANSITION_MODES := {
	&"inherit": true,
	&"replace_sources_with_result": true,
}
const PRACTICE_TRACK_TAGS := [&"meditation", &"cultivation"]
const VALID_PRACTICE_TIERS := {
	&"basic": true,
	&"intermediate": true,
	&"advanced": true,
	&"ultimate": true,
}
const VALID_DAMAGE_TAGS := DAMAGE_TAG_CONTENT_RULES.VALID_DAMAGE_TAGS
const VALID_MITIGATION_TIERS := DAMAGE_TAG_CONTENT_RULES.VALID_MITIGATION_TIERS
const BODY_SIZE_TINY := BodySizeRules.BODY_SIZE_TINY
const BODY_SIZE_SMALL := BodySizeRules.BODY_SIZE_SMALL
const BODY_SIZE_MEDIUM := BodySizeRules.BODY_SIZE_MEDIUM
const BODY_SIZE_LARGE := BodySizeRules.BODY_SIZE_LARGE
const BODY_SIZE_HUGE := BodySizeRules.BODY_SIZE_HUGE
const BODY_SIZE_GARGANTUAN := BodySizeRules.BODY_SIZE_GARGANTUAN
const BODY_SIZE_BOSS := BodySizeRules.BODY_SIZE_BOSS
const VALID_BODY_SIZES := BodySizeRules.VALID_BODY_SIZES

## 字段说明：缓存技能定义集合字典，集中保存可按键查询的运行时数据。
var _skill_defs: Dictionary = {}
## 字段说明：缓存职业定义集合字典，集中保存可按键查询的运行时数据。
var _profession_defs: Dictionary = {}
## 字段说明：缓存成就定义集合字典，集中保存可按键查询的运行时数据。
var _achievement_defs: Dictionary = {}
## 字段说明：缓存任务定义集合字典，集中保存可按键查询的运行时数据。
var _quest_defs: Dictionary = {}
var _race_defs: Dictionary = {}
var _subrace_defs: Dictionary = {}
var _race_trait_defs: Dictionary = {}
var _age_profile_defs: Dictionary = {}
var _bloodline_defs: Dictionary = {}
var _bloodline_stage_defs: Dictionary = {}
var _ascension_defs: Dictionary = {}
var _ascension_stage_defs: Dictionary = {}
var _stage_advancement_defs: Dictionary = {}
## 字段说明：记录技能内容注册表，会参与运行时状态流转、系统协作和静态校验。
var _skill_content_registry = SKILL_CONTENT_REGISTRY_SCRIPT.new()
## 字段说明：记录职业内容注册表，会参与运行时状态流转、系统协作和静态校验。
var _profession_content_registry = PROFESSION_CONTENT_REGISTRY_SCRIPT.new()
var _race_content_registry = RACE_CONTENT_REGISTRY_SCRIPT.new()
var _subrace_content_registry = SUBRACE_CONTENT_REGISTRY_SCRIPT.new()
var _race_trait_content_registry = RACE_TRAIT_CONTENT_REGISTRY_SCRIPT.new()
var _age_content_registry = AGE_CONTENT_REGISTRY_SCRIPT.new()
var _bloodline_content_registry = BLOODLINE_CONTENT_REGISTRY_SCRIPT.new()
var _ascension_content_registry = ASCENSION_CONTENT_REGISTRY_SCRIPT.new()
var _stage_advancement_content_registry = STAGE_ADVANCEMENT_CONTENT_REGISTRY_SCRIPT.new()
## 字段说明：收集配置校验阶段发现的错误信息，便于启动时统一报告和定位问题。
var _validation_errors: Array[String] = []
var _quest_registration_errors: Array[String] = []


func _init() -> void:
	rebuild()


func rebuild() -> void:
	_skill_defs.clear()
	_profession_defs.clear()
	_achievement_defs.clear()
	_quest_defs.clear()
	_quest_registration_errors.clear()
	_race_defs.clear()
	_subrace_defs.clear()
	_race_trait_defs.clear()
	_age_profile_defs.clear()
	_bloodline_defs.clear()
	_bloodline_stage_defs.clear()
	_ascension_defs.clear()
	_ascension_stage_defs.clear()
	_stage_advancement_defs.clear()
	_validation_errors.clear()

	_skill_content_registry.rebuild()
	_skill_defs = _skill_content_registry.get_skill_defs().duplicate()
	# Official profession skill seeds now live in SkillDef resources under data/configs/skills.
	_validation_errors.append_array(_skill_content_registry.validate())
	# Profession seed ownership lives in resource files under data/configs/professions.
	_profession_content_registry.setup(_skill_defs)
	_profession_defs = _profession_content_registry.get_profession_defs()
	_race_content_registry.rebuild()
	_race_defs = _race_content_registry.get_race_defs().duplicate()
	_subrace_content_registry.rebuild()
	_subrace_defs = _subrace_content_registry.get_subrace_defs().duplicate()
	_race_trait_content_registry.rebuild()
	_race_trait_defs = _race_trait_content_registry.get_race_trait_defs().duplicate()
	_age_content_registry.rebuild()
	_age_profile_defs = _age_content_registry.get_age_profile_defs().duplicate()
	_bloodline_content_registry.rebuild()
	_bloodline_defs = _bloodline_content_registry.get_bloodline_defs().duplicate()
	_bloodline_stage_defs = _bloodline_content_registry.get_bloodline_stage_defs().duplicate()
	_ascension_content_registry.rebuild()
	_ascension_defs = _ascension_content_registry.get_ascension_defs().duplicate()
	_ascension_stage_defs = _ascension_content_registry.get_ascension_stage_defs().duplicate()
	_stage_advancement_content_registry.rebuild()
	_stage_advancement_defs = _stage_advancement_content_registry.get_stage_advancement_defs().duplicate()
	_register_seed_achievements()
	_register_seed_quests()
	_validation_errors.append_array(_profession_content_registry.validate())
	_validation_errors.append_array(_race_content_registry.validate())
	_validation_errors.append_array(_subrace_content_registry.validate())
	_validation_errors.append_array(_race_trait_content_registry.validate())
	_validation_errors.append_array(_age_content_registry.validate())
	_validation_errors.append_array(_bloodline_content_registry.validate())
	_validation_errors.append_array(_ascension_content_registry.validate())
	_validation_errors.append_array(_stage_advancement_content_registry.validate())
	_validation_errors.append_array(_collect_validation_errors())


func get_skill_defs() -> Dictionary:
	return _skill_defs.duplicate()


func get_profession_defs() -> Dictionary:
	return _profession_defs.duplicate()


func get_achievement_defs() -> Dictionary:
	return _achievement_defs.duplicate()


func get_quest_defs() -> Dictionary:
	return _quest_defs.duplicate()


func get_quest_registration_errors() -> Array[String]:
	return _quest_registration_errors.duplicate()


func get_race_defs() -> Dictionary:
	return _race_defs.duplicate()


func get_subrace_defs() -> Dictionary:
	return _subrace_defs.duplicate()


func get_race_trait_defs() -> Dictionary:
	return _race_trait_defs.duplicate()


func get_age_profile_defs() -> Dictionary:
	return _age_profile_defs.duplicate()


func get_bloodline_defs() -> Dictionary:
	return _bloodline_defs.duplicate()


func get_bloodline_stage_defs() -> Dictionary:
	return _bloodline_stage_defs.duplicate()


func get_ascension_defs() -> Dictionary:
	return _ascension_defs.duplicate()


func get_ascension_stage_defs() -> Dictionary:
	return _ascension_stage_defs.duplicate()


func get_stage_advancement_defs() -> Dictionary:
	return _stage_advancement_defs.duplicate()


func get_bundle() -> Dictionary:
	return {
		"skill_defs": _skill_defs.duplicate(),
		"profession_defs": _profession_defs.duplicate(),
		"achievement_defs": _achievement_defs.duplicate(),
		"quest_defs": _quest_defs.duplicate(),
		"race": _race_defs.duplicate(),
		"subrace": _subrace_defs.duplicate(),
		"race_trait": _race_trait_defs.duplicate(),
		"age_profile": _age_profile_defs.duplicate(),
		"bloodline": _bloodline_defs.duplicate(),
		"bloodline_stage": _bloodline_stage_defs.duplicate(),
		"ascension": _ascension_defs.duplicate(),
		"ascension_stage": _ascension_stage_defs.duplicate(),
		"stage_advancement": _stage_advancement_defs.duplicate(),
		"race_defs": _race_defs.duplicate(),
		"subrace_defs": _subrace_defs.duplicate(),
		"race_trait_defs": _race_trait_defs.duplicate(),
		"age_profile_defs": _age_profile_defs.duplicate(),
		"bloodline_defs": _bloodline_defs.duplicate(),
		"bloodline_stage_defs": _bloodline_stage_defs.duplicate(),
		"ascension_defs": _ascension_defs.duplicate(),
		"ascension_stage_defs": _ascension_stage_defs.duplicate(),
		"stage_advancement_defs": _stage_advancement_defs.duplicate(),
	}


func validate() -> Array[String]:
	var errors := _validation_errors.duplicate()
	_append_registry_validation_errors(errors, _skill_content_registry)
	_append_registry_validation_errors(errors, _profession_content_registry)
	_append_registry_validation_errors(errors, _race_content_registry)
	_append_registry_validation_errors(errors, _subrace_content_registry)
	_append_registry_validation_errors(errors, _race_trait_content_registry)
	_append_registry_validation_errors(errors, _age_content_registry)
	_append_registry_validation_errors(errors, _bloodline_content_registry)
	_append_registry_validation_errors(errors, _ascension_content_registry)
	_append_registry_validation_errors(errors, _stage_advancement_content_registry)
	for validation_error in _collect_validation_errors():
		if not errors.has(validation_error):
			errors.append(validation_error)
	return errors


func _append_registry_validation_errors(errors: Array[String], registry) -> void:
	if registry == null or not registry.has_method("validate"):
		return
	for validation_error in registry.validate():
		if not errors.has(validation_error):
			errors.append(validation_error)


func _register_seed_achievements() -> void:
	_register_achievement(
		_build_achievement(
			&"battle_won_first",
			"首战归来",
			"亲自完成一次战斗胜利，证明自己已经能从正式交战中平安归来。",
			&"battle_won",
			&"",
			1,
			[
				_build_achievement_reward(
					AchievementRewardDef.TYPE_ATTRIBUTE_DELTA,
					HP_MAX,
					"生命上限",
					8,
					"首战后的胆气与耐力提升。"
				),
			]
		)
	)


func _register_seed_quests() -> void:
	_register_quest(_build_quest(
		&"contract_manual_drill",
		"训练记录",
		"在训练场完成两次记录，用于验证任务命令与状态推进链。",
		&"service_contract_board",
		[
			{
				"objective_id": "train_once",
				"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
				"target_id": "service:training",
				"target_value": 2,
			},
		],
		[
			{"reward_type": QuestDef.REWARD_GOLD, "amount": 30},
		]
	))
	_register_quest(_build_quest(
		&"contract_settlement_warehouse",
		"据点仓储巡查",
		"前往据点服务台完成一次仓储交接。",
		&"service_contract_board",
		[
			{
				"objective_id": "warehouse_visit",
				"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
				"target_id": "service:warehouse",
				"target_value": 1,
			},
		],
		[
			{"reward_type": QuestDef.REWARD_GOLD, "amount": 60},
		]
	))
	_register_quest(_build_quest(
		&"contract_first_hunt",
		"首轮狩猎",
		"击败任意一组敌对遭遇，证明队伍已具备外出作战能力。",
		&"service_contract_board",
		[
			{
				"objective_id": "defeat_enemy_once",
				"objective_type": QuestDef.OBJECTIVE_DEFEAT_ENEMY,
				"target_id": "",
				"target_value": 1,
			},
		],
		[
			{"reward_type": QuestDef.REWARD_GOLD, "amount": 80},
		]
	))
	_register_quest(_build_quest(
		&"contract_regional_bounty",
		"地区悬赏",
		"由悬赏署单独发放的区域通缉，用来验证多 provider 任务板的过滤边界。",
		&"service_bounty_registry",
		[
			{
				"objective_id": "defeat_enemy_once",
				"objective_type": QuestDef.OBJECTIVE_DEFEAT_ENEMY,
				"target_id": "",
				"target_value": 1,
			},
		],
		[
			{"reward_type": QuestDef.REWARD_GOLD, "amount": 120},
		],
		[
			&"contract",
			&"bounty",
		]
	))
	_register_achievement(
		_build_achievement(
			&"settlement_wayfarer",
			"行路借火",
			"在据点完成一次事务，学会把旅途见闻整理成可反复回想的经验。",
			&"settlement_action_completed",
			&"",
			1,
			[
				_build_achievement_reward(
					AchievementRewardDef.TYPE_KNOWLEDGE_UNLOCK,
					&"wayfarer_notes",
					"旅途见闻",
					1,
					"据点经历转化成了可保留的见闻。"
				),
			]
		)
	)
	_register_achievement(
		_build_achievement(
			&"enemy_defeated_apprentice",
			"开刃",
			"累计击倒 3 名敌人，开始掌握主动突进的节奏。",
			&"enemy_defeated",
			&"",
			3,
			[
				_build_achievement_reward(
					AchievementRewardDef.TYPE_SKILL_UNLOCK,
					&"charge",
					"冲锋",
					1,
					"连战后的脚步更敢向前。"
				),
			]
		)
	)
	_register_achievement(
		_build_achievement(
			&"near_death_unbroken",
			"濒死未倒",
			"在生命低于三分之一时承受重击仍存活，证明自身已经能在生死边缘守住形神。",
			&"near_death_unbroken_manual",
			&"",
			1,
			[]
		)
	)
	_register_achievement(
		_build_achievement(
			&"warrior_heavy_strike_practice",
			"重击热身",
			"累计施放 5 次重击，挥砍节奏进一步稳定。",
			&"skill_used",
			&"warrior_heavy_strike",
			5,
			[
				_build_achievement_reward(
					AchievementRewardDef.TYPE_SKILL_MASTERY,
					&"warrior_heavy_strike",
					"重击",
					10,
					"熟能生巧。"
				),
			]
		)
	)
	_register_achievement(
		_build_achievement(
			&"profession_promoted_first",
			"迈向正职",
			"完成首次职业晋升，体魄和力量都得到巩固。",
			&"profession_promoted",
			&"",
			1,
			[
				_build_achievement_reward(
					AchievementRewardDef.TYPE_ATTRIBUTE_DELTA,
					UnitBaseAttributes.STRENGTH,
					"力量",
					1,
					"正式晋升让动作更加扎实。"
				),
				_build_achievement_reward(
					AchievementRewardDef.TYPE_ATTRIBUTE_DELTA,
					HP_MAX,
					"生命上限",
					5,
					"长期训练开始反映到体魄上。"
				),
			]
		)
	)
	_register_achievement(
		_build_achievement(
			&"skill_learned_guard_break",
			"添一门手段",
			"学会裂甲斩，开始愿意把近战手段拓展到不同战术用途。",
			&"skill_learned",
			&"warrior_guard_break",
			1,
			[
				_build_achievement_reward(
					AchievementRewardDef.TYPE_ATTRIBUTE_DELTA,
					UnitBaseAttributes.PERCEPTION,
					"感知",
					1,
					"换用不同兵器后，对出手距离和节奏的判断更敏锐。"
				),
			]
		)
	)
	_register_achievement(
		_build_achievement(
			&"knowledge_learned_field_manual",
			"把见闻记下来",
			"学会《野外手册》，开始把零散经历整理成能反复调用的知识。",
			&"knowledge_learned",
			&"field_manual",
			1,
			[
				_build_achievement_reward(
					AchievementRewardDef.TYPE_ATTRIBUTE_DELTA,
					UnitBaseAttributes.WILLPOWER,
					"意志",
					1,
					"把经验写成规则后，行动会更有把握。"
				),
			]
		)
	)
	_register_achievement(
		_build_achievement(
			&"skill_mastery_charge_stride",
			"冲锋起步",
			"累计获得 20 点冲锋熟练度，开始掌握直线突进的起手节奏。",
			&"skill_mastery_gained",
			&"charge",
			20,
			[
				_build_achievement_reward(
					AchievementRewardDef.TYPE_ATTRIBUTE_DELTA,
					UnitBaseAttributes.AGILITY,
					"敏捷",
					1,
					"反复练习冲锋后，脚步转换更利落。"
				),
			]
		)
	)
	_register_achievement(
		_build_achievement(
			&"fortuna_guidance_true",
			"Fortuna Guidance I",
			"已被 Fortuna 标记后，再次对 elite 或 boss 触发一次劣势大成功。",
			&"fortuna_guidance_true_manual",
			&"",
			1,
			[]
		)
	)
	_register_achievement(
		_build_achievement(
			&"fortuna_guidance_devout",
			"Fortuna Guidance II",
			"已信 Fortuna 的角色在低血且承受强 debuff 的逆境中活下来并赢下战斗。",
			&"fortuna_guidance_devout_manual",
			&"",
			1,
			[]
		)
	)
	_register_achievement(
		_build_achievement(
			&"fortuna_guidance_exalted",
			"Fortuna Guidance III",
			"已信 Fortuna 的角色用高位威胁区间而非门骰，对 elite 或 boss 打出一次大成功。",
			&"fortuna_guidance_exalted_manual",
			&"",
			1,
			[]
		)
	)
	_register_achievement(
		_build_achievement(
			&"fortuna_guidance_blessed",
			"Fortuna Guidance IV",
			"完成一个章节且无人永久死亡，并且该角色在本章内至少经历过一次 Fortuna 相关战斗事件。",
			&"fortuna_guidance_blessed_manual",
			&"",
			1,
			[]
		)
	)
	_register_achievement(
		_build_achievement(
			&"misfortune_guidance_true",
			"Misfortune Guidance I",
			"已被黑冕标记后，成功用 Misfortune 的封印链终结一次 elite 或 boss。",
			&"misfortune_guidance_true_manual",
			&"",
			1,
			[]
		)
	)
	_register_achievement(
		_build_achievement(
			&"misfortune_guidance_devout",
			"Misfortune Guidance II",
			"同一战斗内曾遭遇大失败或强 debuff，随后再用封印链赢下 elite 或 boss。",
			&"misfortune_guidance_devout_manual",
			&"",
			1,
			[]
		)
	)
	_register_achievement(
		_build_achievement(
			&"misfortune_guidance_exalted",
			"Misfortune Guidance III",
			"把同一战斗中未用完的 calamity 结算成 shard，并用固定黑冕材料打造第一件黑暗装备。",
			&"misfortune_guidance_exalted_manual",
			&"",
			1,
			[]
		)
	)
	_register_achievement(
		_build_achievement(
			&"misfortune_guidance_blessed",
			"Misfortune Guidance IV",
			"用 doom_sentence 的宣判击杀完成一次 boss 终结。",
			&"misfortune_guidance_blessed_manual",
			&"",
			1,
			[]
		)
	)


func _build_achievement(
	achievement_id: StringName,
	display_name: String,
	description: String,
	event_type: StringName,
	subject_id: StringName,
	threshold: int,
	rewards: Array[AchievementRewardDef]
) -> AchievementDef:
	var achievement := AchievementDef.new()
	achievement.achievement_id = achievement_id
	achievement.display_name = display_name
	achievement.description = description
	achievement.event_type = event_type
	achievement.subject_id = subject_id
	achievement.threshold = threshold
	achievement.rewards = rewards.duplicate()
	return achievement


func _build_quest(
	quest_id: StringName,
	display_name: String,
	description: String,
	provider_interaction_id: StringName,
	objective_defs: Array,
	reward_entries: Array,
	tags: Array[StringName] = []
) -> QuestDef:
	var quest_def := QuestDef.new()
	quest_def.quest_id = quest_id
	quest_def.display_name = display_name
	quest_def.description = description
	quest_def.provider_interaction_id = provider_interaction_id
	var typed_tags: Array[StringName] = []
	for tag in tags:
		typed_tags.append(tag)
	quest_def.tags = typed_tags
	var typed_objective_defs: Array[Dictionary] = []
	for objective_variant in objective_defs:
		if objective_variant is Dictionary:
			typed_objective_defs.append((objective_variant as Dictionary).duplicate(true))
	quest_def.objective_defs = typed_objective_defs
	var typed_reward_entries: Array[Dictionary] = []
	for reward_variant in reward_entries:
		if reward_variant is Dictionary:
			typed_reward_entries.append((reward_variant as Dictionary).duplicate(true))
	quest_def.reward_entries = typed_reward_entries
	return quest_def


func _build_achievement_reward(
	reward_type: StringName,
	target_id: StringName,
	target_label: String,
	amount: int,
	reason_text: String = ""
) -> AchievementRewardDef:
	var reward := AchievementRewardDef.new()
	reward.reward_type = reward_type
	reward.target_id = target_id
	reward.target_label = target_label
	reward.amount = amount
	reward.reason_text = reason_text
	return reward


func _register_achievement(achievement_def: AchievementDef) -> void:
	if achievement_def == null or achievement_def.achievement_id == &"":
		_validation_errors.append("Encountered an achievement definition without an achievement_id.")
		return
	if _achievement_defs.has(achievement_def.achievement_id):
		_validation_errors.append("Duplicate achievement_id registered: %s" % String(achievement_def.achievement_id))
		return
	_achievement_defs[achievement_def.achievement_id] = achievement_def


func _register_quest(quest_def: QuestDef) -> void:
	if quest_def == null or quest_def.quest_id == &"":
		_quest_registration_errors.append("Encountered a quest definition without a quest_id.")
		return
	if _quest_defs.has(quest_def.quest_id):
		_quest_registration_errors.append("Duplicate quest_id registered: %s" % String(quest_def.quest_id))
		return
	_quest_defs[quest_def.quest_id] = quest_def


func _collect_validation_errors() -> Array[String]:
	var errors: Array[String] = []

	for skill_key in ProgressionDataUtils.sorted_string_keys(_skill_defs):
		var skill_id := StringName(skill_key)
		var skill_def := _skill_defs.get(skill_id) as SkillDef
		_append_invalid_skill_errors(errors, skill_id, skill_def)

	for achievement_key in ProgressionDataUtils.sorted_string_keys(_achievement_defs):
		var achievement_id := StringName(achievement_key)
		var achievement_def := _achievement_defs.get(achievement_id) as AchievementDef
		_append_invalid_achievement_errors(errors, achievement_id, achievement_def)

	_append_identity_phase2_validation_errors(errors)

	return errors


func _append_identity_phase2_validation_errors(errors: Array[String]) -> void:
	_append_global_stage_id_errors(errors)

	for race_key in ProgressionDataUtils.sorted_string_keys(_race_defs):
		var race_id := StringName(race_key)
		_append_race_phase2_errors(errors, race_id, _race_defs.get(race_id) as RaceDef)

	for subrace_key in ProgressionDataUtils.sorted_string_keys(_subrace_defs):
		var subrace_id := StringName(subrace_key)
		_append_subrace_phase2_errors(errors, subrace_id, _subrace_defs.get(subrace_id) as SubraceDef)

	for trait_key in ProgressionDataUtils.sorted_string_keys(_race_trait_defs):
		var trait_id := StringName(trait_key)
		_append_race_trait_phase2_errors(errors, trait_id, _race_trait_defs.get(trait_id) as RaceTraitDef)

	for profile_key in ProgressionDataUtils.sorted_string_keys(_age_profile_defs):
		var profile_id := StringName(profile_key)
		_append_age_profile_phase2_errors(errors, profile_id, _age_profile_defs.get(profile_id) as AgeProfileDef)

	for bloodline_key in ProgressionDataUtils.sorted_string_keys(_bloodline_defs):
		var bloodline_id := StringName(bloodline_key)
		_append_bloodline_phase2_errors(errors, bloodline_id, _bloodline_defs.get(bloodline_id) as BloodlineDef)

	for stage_key in ProgressionDataUtils.sorted_string_keys(_bloodline_stage_defs):
		var stage_id := StringName(stage_key)
		_append_bloodline_stage_phase2_errors(errors, stage_id, _bloodline_stage_defs.get(stage_id) as BloodlineStageDef)

	for ascension_key in ProgressionDataUtils.sorted_string_keys(_ascension_defs):
		var ascension_id := StringName(ascension_key)
		_append_ascension_phase2_errors(errors, ascension_id, _ascension_defs.get(ascension_id) as AscensionDef)

	for stage_key in ProgressionDataUtils.sorted_string_keys(_ascension_stage_defs):
		var stage_id := StringName(stage_key)
		_append_ascension_stage_phase2_errors(errors, stage_id, _ascension_stage_defs.get(stage_id) as AscensionStageDef)

	for modifier_key in ProgressionDataUtils.sorted_string_keys(_stage_advancement_defs):
		var modifier_id := StringName(modifier_key)
		_append_stage_advancement_phase2_errors(
			errors,
			modifier_id,
			_stage_advancement_defs.get(modifier_id) as StageAdvancementModifier
		)


func _append_race_phase2_errors(errors: Array[String], race_id: StringName, race_def: RaceDef) -> void:
	if race_def == null:
		return
	var owner_label := "Race %s" % String(race_id)
	_append_body_size_category_error(errors, owner_label, "body_size_category", race_def.body_size_category, false)
	_append_damage_resistance_errors(errors, owner_label, race_def.damage_resistances)
	_append_trait_reference_errors(errors, owner_label, race_def.trait_ids, "trait_ids")
	_append_racial_granted_skill_reference_errors(errors, owner_label, race_def.racial_granted_skills, &"race")

	if race_def.age_profile_id != &"" and not _age_profile_defs.has(race_def.age_profile_id):
		errors.append("%s references missing age_profile %s." % [owner_label, String(race_def.age_profile_id)])

	if race_def.default_subrace_id != &"":
		if not _subrace_defs.has(race_def.default_subrace_id):
			errors.append("%s references missing default_subrace %s." % [owner_label, String(race_def.default_subrace_id)])
		elif not race_def.subrace_ids.has(race_def.default_subrace_id):
			errors.append(
				"%s default_subrace %s must be listed in subrace_ids." % [
					owner_label,
					String(race_def.default_subrace_id),
				]
			)

	for subrace_id in race_def.subrace_ids:
		if subrace_id == &"":
			continue
		var subrace_def := _subrace_defs.get(subrace_id) as SubraceDef
		if subrace_def == null:
			errors.append("%s references missing subrace %s." % [owner_label, String(subrace_id)])
			continue
		if subrace_def.parent_race_id != race_id:
			errors.append(
				"%s subrace %s parent_race_id must be %s, got %s." % [
					owner_label,
					String(subrace_id),
					String(race_id),
					String(subrace_def.parent_race_id),
				]
			)


func _append_subrace_phase2_errors(errors: Array[String], subrace_id: StringName, subrace_def: SubraceDef) -> void:
	if subrace_def == null:
		return
	var owner_label := "Subrace %s" % String(subrace_id)
	_append_body_size_category_error(errors, owner_label, "body_size_category_override", subrace_def.body_size_category_override, true)
	_append_damage_resistance_errors(errors, owner_label, subrace_def.damage_resistances)
	_append_trait_reference_errors(errors, owner_label, subrace_def.trait_ids, "trait_ids")
	_append_racial_granted_skill_reference_errors(errors, owner_label, subrace_def.racial_granted_skills, &"subrace")

	if subrace_def.parent_race_id == &"":
		return
	var parent_race := _race_defs.get(subrace_def.parent_race_id) as RaceDef
	if parent_race == null:
		errors.append("%s references missing parent_race %s." % [owner_label, String(subrace_def.parent_race_id)])
		return
	if not parent_race.subrace_ids.has(subrace_id):
		errors.append(
			"%s parent_race %s must list this subrace in subrace_ids." % [
				owner_label,
				String(subrace_def.parent_race_id),
			]
		)


func _append_race_trait_phase2_errors(errors: Array[String], trait_id: StringName, trait_def: RaceTraitDef) -> void:
	if trait_def == null:
		return
	var trigger_type := ProgressionDataUtils.to_string_name(trait_def.trigger_type)
	if trigger_type == &"" or trigger_type == TRAIT_TRIGGER_CONTENT_RULES.TRIGGER_PASSIVE:
		return
	if not TRAIT_TRIGGER_CONTENT_RULES.has_dispatch_for_trait_trigger(trait_id, trigger_type):
		errors.append(
			"RaceTrait %s trigger_type %s has no TraitTriggerHooks dispatch." % [
				String(trait_id),
				String(trigger_type),
			]
		)


func _append_age_profile_phase2_errors(errors: Array[String], profile_id: StringName, profile_def: AgeProfileDef) -> void:
	if profile_def == null:
		return
	var owner_label := "AgeProfile %s" % String(profile_id)
	if profile_def.race_id != &"":
		var race_def := _race_defs.get(profile_def.race_id) as RaceDef
		if race_def == null:
			errors.append("%s references missing race %s." % [owner_label, String(profile_def.race_id)])
		elif race_def.age_profile_id != profile_id:
			errors.append(
				"%s race %s must reference this profile as age_profile_id." % [
					owner_label,
					String(profile_def.race_id),
				]
			)
	if profile_def.stage_rules.is_empty():
		errors.append("%s must declare at least one stage_rules entry." % owner_label)

	var stage_ids := _collect_age_profile_stage_ids(profile_def)
	for stage_id in profile_def.creation_stage_ids:
		if stage_id != &"" and not stage_ids.has(stage_id):
			errors.append("%s creation_stage_ids references missing stage %s." % [owner_label, String(stage_id)])
	for stage_key_variant in profile_def.default_age_by_stage.keys():
		var stage_id := _strict_to_string_name(stage_key_variant)
		if stage_id != &"" and not stage_ids.has(stage_id):
			errors.append("%s default_age_by_stage references missing stage %s." % [owner_label, String(stage_id)])
	for stage_rule in profile_def.stage_rules:
		if stage_rule == null:
			continue
		_append_trait_reference_errors(
			errors,
			"%s stage %s" % [owner_label, String(stage_rule.stage_id)],
			stage_rule.trait_ids,
			"trait_ids"
		)


func _append_bloodline_phase2_errors(errors: Array[String], bloodline_id: StringName, bloodline_def: BloodlineDef) -> void:
	if bloodline_def == null:
		return
	var owner_label := "Bloodline %s" % String(bloodline_id)
	_append_trait_reference_errors(errors, owner_label, bloodline_def.trait_ids, "trait_ids")
	_append_racial_granted_skill_reference_errors(errors, owner_label, bloodline_def.racial_granted_skills, &"bloodline")
	for stage_id in bloodline_def.stage_ids:
		if stage_id == &"":
			continue
		var stage_def := _bloodline_stage_defs.get(stage_id) as BloodlineStageDef
		if stage_def == null:
			errors.append("%s references missing bloodline_stage %s." % [owner_label, String(stage_id)])
			continue
		if stage_def.bloodline_id != bloodline_id:
			errors.append(
				"%s stage %s bloodline_id must be %s, got %s." % [
					owner_label,
					String(stage_id),
					String(bloodline_id),
					String(stage_def.bloodline_id),
				]
			)


func _append_bloodline_stage_phase2_errors(errors: Array[String], stage_id: StringName, stage_def: BloodlineStageDef) -> void:
	if stage_def == null:
		return
	var owner_label := "BloodlineStage %s" % String(stage_id)
	_append_trait_reference_errors(errors, owner_label, stage_def.trait_ids, "trait_ids")
	_append_racial_granted_skill_reference_errors(errors, owner_label, stage_def.racial_granted_skills, &"bloodline")
	if stage_def.bloodline_id == &"":
		return
	var bloodline_def := _bloodline_defs.get(stage_def.bloodline_id) as BloodlineDef
	if bloodline_def == null:
		errors.append("%s references missing bloodline %s." % [owner_label, String(stage_def.bloodline_id)])
		return
	if not bloodline_def.stage_ids.has(stage_id):
		errors.append(
			"%s bloodline %s must list this stage in stage_ids." % [
				owner_label,
				String(stage_def.bloodline_id),
			]
		)


func _append_ascension_phase2_errors(errors: Array[String], ascension_id: StringName, ascension_def: AscensionDef) -> void:
	if ascension_def == null:
		return
	var owner_label := "Ascension %s" % String(ascension_id)
	_append_trait_reference_errors(errors, owner_label, ascension_def.trait_ids, "trait_ids")
	_append_racial_granted_skill_reference_errors(errors, owner_label, ascension_def.racial_granted_skills, &"ascension")
	_append_id_reference_errors(errors, owner_label, ascension_def.allowed_race_ids, "allowed_race_ids", _race_defs, "race")
	_append_id_reference_errors(errors, owner_label, ascension_def.allowed_subrace_ids, "allowed_subrace_ids", _subrace_defs, "subrace")
	_append_id_reference_errors(errors, owner_label, ascension_def.allowed_bloodline_ids, "allowed_bloodline_ids", _bloodline_defs, "bloodline")

	for stage_id in ascension_def.stage_ids:
		if stage_id == &"":
			continue
		var stage_def := _ascension_stage_defs.get(stage_id) as AscensionStageDef
		if stage_def == null:
			errors.append("%s references missing ascension_stage %s." % [owner_label, String(stage_id)])
			continue
		if stage_def.ascension_id != ascension_id:
			errors.append(
				"%s stage %s ascension_id must be %s, got %s." % [
					owner_label,
					String(stage_id),
					String(ascension_id),
					String(stage_def.ascension_id),
				]
			)


func _append_ascension_stage_phase2_errors(errors: Array[String], stage_id: StringName, stage_def: AscensionStageDef) -> void:
	if stage_def == null:
		return
	var owner_label := "AscensionStage %s" % String(stage_id)
	_append_body_size_category_error(errors, owner_label, "body_size_category_override", stage_def.body_size_category_override, true)
	_append_trait_reference_errors(errors, owner_label, stage_def.trait_ids, "trait_ids")
	_append_racial_granted_skill_reference_errors(errors, owner_label, stage_def.racial_granted_skills, &"ascension")
	if stage_def.ascension_id == &"":
		return
	var ascension_def := _ascension_defs.get(stage_def.ascension_id) as AscensionDef
	if ascension_def == null:
		errors.append("%s references missing ascension %s." % [owner_label, String(stage_def.ascension_id)])
		return
	if not ascension_def.stage_ids.has(stage_id):
		errors.append(
			"%s ascension %s must list this stage in stage_ids." % [
				owner_label,
				String(stage_def.ascension_id),
			]
		)


func _append_stage_advancement_phase2_errors(
	errors: Array[String],
	modifier_id: StringName,
	modifier: StageAdvancementModifier
) -> void:
	if modifier == null:
		return
	var owner_label := "StageAdvancement %s" % String(modifier_id)
	if not StageAdvancementModifier.VALID_TARGET_AXES.has(modifier.target_axis):
		errors.append("%s uses unsupported target_axis %s." % [owner_label, String(modifier.target_axis)])
	_append_id_reference_errors(errors, owner_label, modifier.applies_to_race_ids, "applies_to_race_ids", _race_defs, "race")
	_append_id_reference_errors(errors, owner_label, modifier.applies_to_subrace_ids, "applies_to_subrace_ids", _subrace_defs, "subrace")
	_append_id_reference_errors(errors, owner_label, modifier.applies_to_bloodline_ids, "applies_to_bloodline_ids", _bloodline_defs, "bloodline")
	_append_id_reference_errors(errors, owner_label, modifier.applies_to_ascension_ids, "applies_to_ascension_ids", _ascension_defs, "ascension")
	_append_stage_advancement_max_stage_error(errors, owner_label, modifier)


func _append_stage_advancement_max_stage_error(
	errors: Array[String],
	owner_label: String,
	modifier: StageAdvancementModifier
) -> void:
	if modifier.max_stage_id == &"":
		return
	match modifier.target_axis:
		StageAdvancementModifier.TARGET_AXIS_BLOODLINE:
			if not _bloodline_stage_defs.has(modifier.max_stage_id):
				errors.append("%s max_stage_id references missing bloodline_stage %s." % [owner_label, String(modifier.max_stage_id)])
		StageAdvancementModifier.TARGET_AXIS_DIVINE:
			if not _ascension_stage_defs.has(modifier.max_stage_id):
				errors.append("%s max_stage_id references missing ascension_stage %s." % [owner_label, String(modifier.max_stage_id)])
		_:
			var known_stage_ids := _collect_known_identity_stage_ids()
			if not known_stage_ids.has(modifier.max_stage_id):
				errors.append("%s max_stage_id references missing stage %s." % [owner_label, String(modifier.max_stage_id)])


func _append_global_stage_id_errors(errors: Array[String]) -> void:
	var stage_sources: Dictionary = {}
	for stage_key in ProgressionDataUtils.sorted_string_keys(_bloodline_stage_defs):
		_append_global_stage_id(errors, stage_sources, StringName(stage_key), "bloodline_stage")
	for stage_key in ProgressionDataUtils.sorted_string_keys(_ascension_stage_defs):
		_append_global_stage_id(errors, stage_sources, StringName(stage_key), "ascension_stage")


func _append_global_stage_id(
	errors: Array[String],
	stage_sources: Dictionary,
	stage_id: StringName,
	stage_source: String
) -> void:
	if stage_id == &"":
		return
	if stage_sources.has(stage_id):
		errors.append(
			"Stage id %s must be globally unique across bloodline_stage and ascension_stage; declared by %s and %s." % [
				String(stage_id),
				String(stage_sources.get(stage_id, "")),
				stage_source,
			]
		)
		return
	stage_sources[stage_id] = stage_source


func _append_trait_reference_errors(
	errors: Array[String],
	owner_label: String,
	trait_ids: Array[StringName],
	field_label: String
) -> void:
	for trait_id in trait_ids:
		if trait_id == &"":
			continue
		if not _race_trait_defs.has(trait_id):
			errors.append("%s %s references missing trait %s." % [owner_label, field_label, String(trait_id)])


func _append_racial_granted_skill_reference_errors(
	errors: Array[String],
	owner_label: String,
	granted_skills: Array[RacialGrantedSkill],
	expected_learn_source: StringName
) -> void:
	for index in range(granted_skills.size()):
		var granted_skill := granted_skills[index] as RacialGrantedSkill
		if granted_skill == null or granted_skill.skill_id == &"":
			continue
		var skill_def := _skill_defs.get(granted_skill.skill_id) as SkillDef
		if skill_def == null:
			errors.append(
				"%s racial_granted_skills[%d] references missing skill %s." % [
					owner_label,
					index,
					String(granted_skill.skill_id),
				]
			)
			continue
		if skill_def.learn_source != expected_learn_source:
			errors.append(
				"%s racial_granted_skills[%d] skill %s learn_source must be %s, got %s." % [
					owner_label,
					index,
					String(granted_skill.skill_id),
					String(expected_learn_source),
					String(skill_def.learn_source),
				]
			)
		if int(granted_skill.minimum_skill_level) > int(skill_def.max_level):
			errors.append(
				"%s racial_granted_skills[%d] skill %s minimum_skill_level must be <= max_level %d." % [
					owner_label,
					index,
					String(granted_skill.skill_id),
					int(skill_def.max_level),
				]
			)


func _append_id_reference_errors(
	errors: Array[String],
	owner_label: String,
	values: Array[StringName],
	field_label: String,
	target_defs: Dictionary,
	target_label: String
) -> void:
	for value_id in values:
		if value_id == &"":
			continue
		if not target_defs.has(value_id):
			errors.append(
				"%s %s references missing %s %s." % [
					owner_label,
					field_label,
					target_label,
					String(value_id),
				]
			)


func _append_damage_resistance_errors(errors: Array[String], owner_label: String, damage_resistances: Dictionary) -> void:
	for key_variant in damage_resistances.keys():
		var damage_tag := _strict_to_string_name(key_variant)
		if damage_tag == &"":
			errors.append("%s damage_resistances key %s must be a non-empty String or StringName." % [owner_label, str(key_variant)])
			continue
		if not DAMAGE_TAG_CONTENT_RULES.is_valid_damage_tag(damage_tag):
			errors.append("%s damage_resistances references unsupported damage tag %s." % [owner_label, String(damage_tag)])
		var mitigation_tier := _strict_to_string_name(damage_resistances.get(key_variant, null))
		if mitigation_tier == &"":
			errors.append(
				"%s damage_resistances[%s] must be a non-empty String or StringName." % [
					owner_label,
					String(damage_tag),
				]
			)
			continue
		if not DAMAGE_TAG_CONTENT_RULES.is_valid_mitigation_tier(mitigation_tier):
			errors.append(
				"%s damage_resistances[%s] uses unsupported mitigation tier %s." % [
					owner_label,
					String(damage_tag),
					String(mitigation_tier),
				]
			)


func _append_body_size_error(
	errors: Array[String],
	owner_label: String,
	field_label: String,
	value: Variant,
	allow_zero: bool
) -> void:
	if typeof(value) != TYPE_INT:
		errors.append("%s %s must be an int body_size value." % [owner_label, field_label])
		return
	var size_value := int(value)
	if size_value == 0:
		if not allow_zero:
			errors.append("%s %s must be a non-zero body_size." % [owner_label, field_label])
		return
	if not VALID_BODY_SIZES.has(size_value):
		errors.append(
			"%s %s uses unsupported body_size %d." % [
				owner_label,
				field_label,
				size_value,
			]
		)


func _append_body_size_category_error(
	errors: Array[String],
	owner_label: String,
	field_label: String,
	value: Variant,
	allow_empty: bool
) -> void:
	if typeof(value) != TYPE_STRING_NAME:
		errors.append("%s %s must be a StringName body_size_category." % [owner_label, field_label])
		return
	var category := StringName(value)
	if category == &"":
		if not allow_empty:
			errors.append("%s %s must be a non-empty body_size_category." % [owner_label, field_label])
		return
	if not BodySizeRules.is_valid_body_size_category(category):
		errors.append(
			"%s %s uses unsupported body_size_category %s." % [
				owner_label,
				field_label,
				String(category),
			]
		)


func _collect_age_profile_stage_ids(profile_def: AgeProfileDef) -> Dictionary:
	var stage_ids: Dictionary = {}
	if profile_def == null:
		return stage_ids
	for stage_rule in profile_def.stage_rules:
		if stage_rule != null and stage_rule.stage_id != &"":
			stage_ids[stage_rule.stage_id] = true
	return stage_ids


func _collect_known_identity_stage_ids() -> Dictionary:
	var stage_ids: Dictionary = {}
	for profile_key in ProgressionDataUtils.sorted_string_keys(_age_profile_defs):
		var profile_def := _age_profile_defs.get(StringName(profile_key)) as AgeProfileDef
		for stage_id in _collect_age_profile_stage_ids(profile_def).keys():
			stage_ids[stage_id] = true
	for stage_key in ProgressionDataUtils.sorted_string_keys(_bloodline_stage_defs):
		stage_ids[StringName(stage_key)] = true
	for stage_key in ProgressionDataUtils.sorted_string_keys(_ascension_stage_defs):
		stage_ids[StringName(stage_key)] = true
	return stage_ids


func _strict_to_string_name(value: Variant) -> StringName:
	if typeof(value) != TYPE_STRING and typeof(value) != TYPE_STRING_NAME:
		return &""
	var normalized := String(value).strip_edges()
	if normalized.is_empty():
		return &""
	return StringName(normalized)


func _append_invalid_skill_errors(
	errors: Array[String],
	skill_id: StringName,
	skill_def: SkillDef
) -> void:
	if skill_def == null:
		return
	if not VALID_SKILL_TYPES.has(skill_def.skill_type):
		errors.append("Skill %s uses unsupported skill_type %s." % [String(skill_id), String(skill_def.skill_type)])
	if not VALID_LEARN_SOURCES.has(skill_def.learn_source):
		errors.append("Skill %s uses unsupported learn_source %s." % [String(skill_id), String(skill_def.learn_source)])
	if not VALID_UNLOCK_MODES.has(skill_def.unlock_mode):
		errors.append("Skill %s uses unsupported unlock_mode %s." % [String(skill_id), String(skill_def.unlock_mode)])
	if not VALID_CORE_SKILL_TRANSITION_MODES.has(skill_def.core_skill_transition_mode):
		errors.append(
			"Skill %s uses unsupported core_skill_transition_mode %s." % [
				String(skill_id),
				String(skill_def.core_skill_transition_mode),
			]
		)
	if skill_def.max_level < 0 and skill_def.dynamic_max_level_stat_id == &"":
		errors.append("Skill %s must have max_level >= 0." % String(skill_id))
	if skill_def.non_core_max_level < 0:
		errors.append("Skill %s non_core_max_level must be >= 0." % String(skill_id))
	if skill_def.non_core_max_level > skill_def.max_level and skill_def.max_level >= 0 and skill_def.dynamic_max_level_stat_id == &"":
		errors.append("Skill %s non_core_max_level must be <= max_level." % String(skill_id))
	if skill_def.mastery_curve.size() != skill_def.max_level and skill_def.max_level >= 0 and skill_def.dynamic_max_level_stat_id == &"":
		errors.append("Skill %s mastery_curve size must match max_level." % String(skill_id))
	_append_dynamic_max_level_errors(errors, skill_id, skill_def)
	_append_practice_skill_errors(errors, skill_id, skill_def)
	_append_skill_attribute_growth_errors(errors, skill_id, skill_def)

	_append_skill_requirement_errors(errors, skill_id, skill_def.learn_requirements, "learn_requirements")
	_append_skill_level_requirement_errors(errors, skill_id, skill_def.skill_level_requirements)
	_append_attribute_requirement_errors(errors, skill_id, skill_def.attribute_requirements)
	_append_skill_requirement_errors(errors, skill_id, skill_def.upgrade_source_skill_ids, "upgrade_source_skill_ids")
	for achievement_id in skill_def.achievement_requirements:
		if achievement_id == &"":
			errors.append("Skill %s has an empty achievement requirement." % String(skill_id))

	if skill_def.unlock_mode == &"composite_upgrade" and skill_def.upgrade_source_skill_ids.is_empty():
		errors.append("Skill %s is composite_upgrade but missing upgrade_source_skill_ids." % String(skill_id))


func _append_practice_skill_errors(
	errors: Array[String],
	skill_id: StringName,
	skill_def: SkillDef
) -> void:
	var track_count := 0
	for track_tag in PRACTICE_TRACK_TAGS:
		if skill_def.tags.has(track_tag):
			track_count += 1

	if track_count == 0:
		if skill_def.practice_tier != &"":
			errors.append("Skill %s practice_tier requires meditation or cultivation tag." % String(skill_id))
		return

	if track_count != 1:
		errors.append("Skill %s must use exactly one practice track tag." % String(skill_id))
	if skill_def.tags.size() != 1:
		errors.append("Skill %s practice tags must be exclusive; tags must contain only meditation or cultivation." % String(skill_id))
	if not VALID_PRACTICE_TIERS.has(skill_def.practice_tier):
		errors.append("Skill %s practice_tier must be one of basic, intermediate, advanced, ultimate." % String(skill_id))


func _append_dynamic_max_level_errors(
	errors: Array[String],
	skill_id: StringName,
	skill_def: SkillDef
) -> void:
	var has_dynamic_stat := skill_def.dynamic_max_level_stat_id != &""
	if not has_dynamic_stat:
		if skill_def.dynamic_max_level_base != 0:
			errors.append("Skill %s dynamic_max_level_base requires dynamic_max_level_stat_id." % String(skill_id))
		if skill_def.dynamic_max_level_per_stat != 0:
			errors.append("Skill %s dynamic_max_level_per_stat requires dynamic_max_level_stat_id." % String(skill_id))
		return

	if skill_def.dynamic_max_level_base <= 0:
		errors.append("Skill %s dynamic_max_level_base must be >= 1." % String(skill_id))
	if skill_def.dynamic_max_level_per_stat == 0:
		errors.append("Skill %s dynamic_max_level_per_stat must not be 0 when dynamic_max_level_stat_id is set." % String(skill_id))


func _append_skill_attribute_growth_errors(
	errors: Array[String],
	skill_id: StringName,
	skill_def: SkillDef
) -> void:
	if skill_def.attribute_growth_progress.is_empty() and skill_def.growth_tier == &"":
		return
	if not ATTRIBUTE_GROWTH_CONTENT_RULES.is_valid_growth_tier(skill_def.growth_tier):
		errors.append("Skill %s uses unsupported growth_tier %s." % [String(skill_id), String(skill_def.growth_tier)])
		return

	var progress_total := 0
	for attribute_key in skill_def.attribute_growth_progress.keys():
		var attribute_id := ProgressionDataUtils.to_string_name(attribute_key)
		var amount := int(skill_def.attribute_growth_progress.get(attribute_key, 0))
		if not ATTRIBUTE_GROWTH_CONTENT_RULES.is_valid_attribute_id(attribute_id):
			errors.append("Skill %s attribute_growth_progress references invalid attribute %s." % [String(skill_id), String(attribute_id)])
		if amount <= 0:
			errors.append("Skill %s attribute_growth_progress for %s must be > 0." % [String(skill_id), String(attribute_id)])
		progress_total += amount

	var expected_total := ATTRIBUTE_GROWTH_CONTENT_RULES.get_tier_budget(skill_def.growth_tier)
	if progress_total != expected_total:
		errors.append(
			"Skill %s attribute_growth_progress total must equal %d for growth_tier %s." % [
				String(skill_id),
				expected_total,
				String(skill_def.growth_tier),
			]
		)


func _append_skill_requirement_errors(
	errors: Array[String],
	skill_id: StringName,
	requirement_ids: Array[StringName],
	context_label: String
) -> void:
	for required_skill_id in requirement_ids:
		if required_skill_id == &"":
			errors.append("Skill %s has an empty skill reference in %s." % [String(skill_id), context_label])
			continue
		if not _skill_defs.has(required_skill_id):
			errors.append(
				"Skill %s references missing skill %s in %s." % [
					String(skill_id),
					String(required_skill_id),
					context_label,
				]
			)


func _append_skill_level_requirement_errors(
	errors: Array[String],
	skill_id: StringName,
	skill_level_requirements: Dictionary
) -> void:
	for skill_key_variant in skill_level_requirements.keys():
		var required_skill_id := ProgressionDataUtils.to_string_name(skill_key_variant)
		if required_skill_id == &"":
			errors.append("Skill %s has an empty skill_id in skill_level_requirements." % String(skill_id))
			continue
		if not _skill_defs.has(required_skill_id):
			errors.append(
				"Skill %s references missing skill %s in skill_level_requirements." % [
					String(skill_id),
					String(required_skill_id),
				]
			)
		var required_level := int(skill_level_requirements[skill_key_variant])
		if required_level <= 0:
			errors.append(
				"Skill %s requires non-positive level %d for %s in skill_level_requirements." % [
					String(skill_id),
					required_level,
					String(required_skill_id),
				]
			)


func _append_attribute_requirement_errors(
	errors: Array[String],
	skill_id: StringName,
	attribute_requirements: Dictionary
) -> void:
	for attribute_key_variant in attribute_requirements.keys():
		var attribute_id := ProgressionDataUtils.to_string_name(attribute_key_variant)
		if attribute_id == &"":
			errors.append("Skill %s has an empty attribute_id in attribute_requirements." % String(skill_id))
			continue
		if not UnitBaseAttributes.get_all_base_attribute_ids().has(attribute_id):
			errors.append(
				"Skill %s references unsupported attribute %s in attribute_requirements." % [
					String(skill_id),
					String(attribute_id),
				]
			)
		var required_value := int(attribute_requirements[attribute_key_variant])
		if required_value <= 0:
			errors.append(
				"Skill %s requires non-positive value %d for %s in attribute_requirements." % [
					String(skill_id),
					required_value,
					String(attribute_id),
				]
			)


func _append_invalid_achievement_errors(
	errors: Array[String],
	achievement_id: StringName,
	achievement_def: AchievementDef
) -> void:
	if achievement_def == null:
		return
	if achievement_def.event_type == &"":
		errors.append("Achievement %s is missing event_type." % String(achievement_id))
	if achievement_def.threshold <= 0:
		errors.append("Achievement %s must have a positive threshold." % String(achievement_id))

	for reward in achievement_def.rewards:
		if reward == null:
			continue
		if reward.reward_type == &"":
			errors.append("Achievement %s has a reward without reward_type." % String(achievement_id))
		if reward.target_id == &"":
			errors.append("Achievement %s has a reward without target_id." % String(achievement_id))
		if reward.amount == 0:
			errors.append("Achievement %s has a zero-amount reward for %s." % [
				String(achievement_id),
				String(reward.target_id),
			])
		if reward.reward_type != &"" and not PENDING_CHARACTER_REWARD_CONTENT_RULES.is_supported_entry_type(reward.reward_type):
			errors.append(
				"Achievement %s uses unsupported reward_type %s." % [
					String(achievement_id),
					String(reward.reward_type),
				]
			)
			continue
		match reward.reward_type:
			AchievementRewardDef.TYPE_SKILL_UNLOCK, AchievementRewardDef.TYPE_SKILL_MASTERY:
				if not _skill_defs.has(reward.target_id):
					errors.append(
						"Achievement %s references missing skill %s." % [String(achievement_id), String(reward.target_id)]
					)
			AchievementRewardDef.TYPE_ATTRIBUTE_DELTA:
				pass
			PENDING_CHARACTER_REWARD_CONTENT_RULES.ENTRY_ATTRIBUTE_PROGRESS:
				if not PENDING_CHARACTER_REWARD_CONTENT_RULES.is_valid_attribute_progress_target(reward.target_id):
					errors.append(
						"Achievement %s attribute_progress reward references unsupported attribute %s." % [
							String(achievement_id),
							String(reward.target_id),
						]
					)
			AchievementRewardDef.TYPE_KNOWLEDGE_UNLOCK:
				pass
