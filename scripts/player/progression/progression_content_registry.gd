## 文件说明：该脚本属于成长内容注册表相关的注册表脚本，集中维护技能定义集合、职业定义集合、成就定义集合等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name ProgressionContentRegistry
extends RefCounted

const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const DESIGN_SKILL_CATALOG_SCRIPT = preload("res://scripts/player/progression/design_skill_catalog.gd")
const SKILL_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/skill_content_registry.gd")
const PROFESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/profession_content_registry.gd")
const AchievementDef = preload("res://scripts/player/progression/achievement_def.gd")
const AchievementRewardDef = preload("res://scripts/player/progression/achievement_reward_def.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")

const HP_MAX: StringName = &"hp_max"
const VALID_SKILL_TYPES := {
	&"active": true,
	&"passive": true,
}
const VALID_LEARN_SOURCES := {
	&"book": true,
	&"profession": true,
}
const VALID_UNLOCK_MODES := {
	&"standard": true,
	&"composite_upgrade": true,
}
const VALID_CORE_SKILL_TRANSITION_MODES := {
	&"inherit": true,
	&"replace_sources_with_result": true,
}

## 字段说明：缓存技能定义集合字典，集中保存可按键查询的运行时数据。
var _skill_defs: Dictionary = {}
## 字段说明：缓存职业定义集合字典，集中保存可按键查询的运行时数据。
var _profession_defs: Dictionary = {}
## 字段说明：缓存成就定义集合字典，集中保存可按键查询的运行时数据。
var _achievement_defs: Dictionary = {}
## 字段说明：缓存任务定义集合字典，集中保存可按键查询的运行时数据。
var _quest_defs: Dictionary = {}
## 字段说明：记录技能内容注册表，会参与运行时状态流转、系统协作和静态校验。
var _skill_content_registry = SKILL_CONTENT_REGISTRY_SCRIPT.new()
## 字段说明：记录职业内容注册表，会参与运行时状态流转、系统协作和静态校验。
var _profession_content_registry = PROFESSION_CONTENT_REGISTRY_SCRIPT.new()
## 字段说明：收集配置校验阶段发现的错误信息，便于启动时统一报告和定位问题。
var _validation_errors: Array[String] = []
## 字段说明：记录已由资源注册表提供的技能主键，供迁移期间的兼容桥跳过重复 code seed。
var _resource_skill_ids: Dictionary = {}


func _init() -> void:
	rebuild()


func rebuild() -> void:
	_skill_defs.clear()
	_profession_defs.clear()
	_achievement_defs.clear()
	_quest_defs.clear()
	_validation_errors.clear()
	_resource_skill_ids.clear()

	_skill_content_registry.rebuild()
	_skill_defs = _skill_content_registry.get_skill_defs().duplicate()
	for skill_key in _skill_defs.keys():
		_resource_skill_ids[StringName(skill_key)] = true
	_register_seed_melee_skills()
	_register_warrior_maneuver_catalog()
	_register_archer_skill_catalog()
	_register_mage_skill_catalog()
	_validation_errors.append_array(_skill_content_registry.validate())
	# Profession seed ownership lives in resource files under data/configs/professions.
	_profession_content_registry.setup(_skill_defs)
	_profession_defs = _profession_content_registry.get_profession_defs()
	_register_seed_achievements()
	_register_seed_quests()
	_validation_errors.append_array(_profession_content_registry.validate())
	_validation_errors.append_array(_collect_validation_errors())


func get_skill_defs() -> Dictionary:
	return _skill_defs


func get_profession_defs() -> Dictionary:
	return _profession_defs


func get_achievement_defs() -> Dictionary:
	return _achievement_defs


func get_quest_defs() -> Dictionary:
	return _quest_defs


func get_bundle() -> Dictionary:
	return {
		"skill_defs": _skill_defs,
		"profession_defs": _profession_defs,
		"achievement_defs": _achievement_defs,
		"quest_defs": _quest_defs,
	}


func validate() -> Array[String]:
	var errors := _validation_errors.duplicate()
	for validation_error in _skill_content_registry.validate():
		if not errors.has(validation_error):
			errors.append(validation_error)
	for validation_error in _profession_content_registry.validate():
		if not errors.has(validation_error):
			errors.append(validation_error)
	for validation_error in _collect_validation_errors():
		if not errors.has(validation_error):
			errors.append(validation_error)
	return errors


func _register_seed_melee_skills() -> void:
	_register_skill(
		_build_skill(
			&"charge",
			"冲锋",
			"朝四个正交方向发起位移。基础距离 3，1 级为 4，3 级为 5，5 级为 6；途中会被陷阱和挡路单位打断。",
			&"active",
			5,
			[20, 35, 55, 80, 110],
			[&"melee", &"mobility", &"charge"],
			&"book",
			[],
			[&"training", &"battle"],
			[],
			_build_charge_combat_profile(&"charge")
		)
	)


func _register_warrior_maneuver_catalog() -> void:
	DESIGN_SKILL_CATALOG_SCRIPT.new().register_warrior_skills(Callable(self, "_register_skill"))


func _register_archer_skill_catalog() -> void:
	DESIGN_SKILL_CATALOG_SCRIPT.new().register_archer_skills(Callable(self, "_register_skill"))


func _register_mage_skill_catalog() -> void:
	DESIGN_SKILL_CATALOG_SCRIPT.new().register_mage_skills(Callable(self, "_register_skill"))


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
	attribute_modifiers: Array[AttributeModifier] = [],
	combat_profile: CombatSkillDef = null,
	icon_id: StringName = &""
) -> SkillDef:
	var skill_def := SkillDef.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = display_name
	skill_def.icon_id = icon_id if icon_id != &"" else skill_id
	skill_def.description = description
	skill_def.skill_type = skill_type
	skill_def.max_level = max_level
	skill_def.mastery_curve = _build_mastery_curve(mastery_curve_values)
	skill_def.tags = tags.duplicate()
	skill_def.learn_source = learn_source
	skill_def.learn_requirements = learn_requirements.duplicate()
	skill_def.mastery_sources = mastery_sources.duplicate()
	skill_def.attribute_modifiers = attribute_modifiers.duplicate()
	skill_def.combat_profile = combat_profile
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


func _build_damage_effect(
	power: int,
	scaling_attribute_id: StringName = &"physical_attack",
	defense_attribute_id: StringName = &"",
	resistance_attribute_id: StringName = &"",
	target_team_filter: StringName = &""
) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"damage"
	effect_def.power = power
	effect_def.scaling_attribute_id = scaling_attribute_id
	if defense_attribute_id != &"":
		effect_def.defense_attribute_id = defense_attribute_id
	if resistance_attribute_id != &"":
		effect_def.resistance_attribute_id = resistance_attribute_id
	if target_team_filter != &"":
		effect_def.effect_target_team_filter = target_team_filter
	return effect_def


func _build_heal_effect(power: int, target_team_filter: StringName = &"") -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"heal"
	effect_def.power = power
	if target_team_filter != &"":
		effect_def.effect_target_team_filter = target_team_filter
	return effect_def


func _build_status_effect(
	status_id: StringName,
	duration: int,
	power: int = 1,
	target_team_filter: StringName = &""
) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"status"
	effect_def.status_id = status_id
	effect_def.power = power
	if target_team_filter != &"":
		effect_def.effect_target_team_filter = target_team_filter
	if duration > 0:
		effect_def.params = {
			"duration": duration,
		}
	return effect_def


func _build_unit_combat_profile(
	skill_id: StringName,
	range_value: int,
	ap_cost: int,
	target_team_filter: StringName,
	effect_defs: Array[CombatEffectDef],
	mp_cost: int = 0,
	stamina_cost: int = 0,
	cooldown_tu: int = 0
) -> CombatSkillDef:
	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"unit"
	combat_profile.target_team_filter = target_team_filter
	combat_profile.range_pattern = &"single"
	combat_profile.range_value = range_value
	combat_profile.area_pattern = &"single"
	combat_profile.area_value = 0
	combat_profile.requires_los = false
	combat_profile.ap_cost = ap_cost
	combat_profile.mp_cost = maxi(mp_cost, 0)
	combat_profile.stamina_cost = maxi(stamina_cost, 0)
	combat_profile.cooldown_tu = maxi(cooldown_tu, 0)
	combat_profile.effect_defs = effect_defs.duplicate()
	return combat_profile


func _build_ground_aoe_combat_profile(
	skill_id: StringName,
	range_value: int,
	ap_cost: int,
	target_team_filter: StringName,
	area_pattern: StringName,
	area_value: int,
	effect_defs: Array[CombatEffectDef],
	mp_cost: int = 0,
	stamina_cost: int = 0,
	cooldown_tu: int = 0
) -> CombatSkillDef:
	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"ground"
	combat_profile.target_team_filter = target_team_filter
	combat_profile.range_pattern = &"single"
	combat_profile.range_value = range_value
	combat_profile.area_pattern = area_pattern
	combat_profile.area_value = area_value
	combat_profile.requires_los = false
	combat_profile.ap_cost = ap_cost
	combat_profile.mp_cost = maxi(mp_cost, 0)
	combat_profile.stamina_cost = maxi(stamina_cost, 0)
	combat_profile.cooldown_tu = maxi(cooldown_tu, 0)
	combat_profile.effect_defs = effect_defs.duplicate()
	return combat_profile


func _build_basic_melee_combat_profile(skill_id: StringName, range_value: int, power: int) -> CombatSkillDef:
	var effect_def: CombatEffectDef = CombatEffectDef.new()
	effect_def.effect_type = &"damage"
	effect_def.power = power
	effect_def.scaling_attribute_id = &"physical_attack"

	var combat_profile: CombatSkillDef = CombatSkillDef.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"unit"
	combat_profile.target_team_filter = &"enemy"
	combat_profile.range_pattern = &"single"
	combat_profile.range_value = range_value
	combat_profile.area_pattern = &"single"
	combat_profile.area_value = 0
	combat_profile.requires_los = false
	combat_profile.ap_cost = 1
	combat_profile.cooldown_tu = 0
	combat_profile.effect_defs = [effect_def]
	return combat_profile


func _build_ground_variant_combat_profile(
	skill_id: StringName,
	range_value: int,
	ap_cost: int,
	cast_variants: Array[CombatCastVariantDef],
	target_team_filter: StringName = &"any",
	mp_cost: int = 0,
	stamina_cost: int = 0,
	cooldown_tu: int = 0
) -> CombatSkillDef:
	var combat_profile: CombatSkillDef = CombatSkillDef.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"ground"
	combat_profile.target_team_filter = target_team_filter
	combat_profile.range_pattern = &"single"
	combat_profile.range_value = range_value
	combat_profile.area_pattern = &"single"
	combat_profile.area_value = 0
	combat_profile.requires_los = false
	combat_profile.ap_cost = ap_cost
	combat_profile.mp_cost = maxi(mp_cost, 0)
	combat_profile.stamina_cost = maxi(stamina_cost, 0)
	combat_profile.cooldown_tu = maxi(cooldown_tu, 0)
	combat_profile.cast_variants = cast_variants.duplicate()
	return combat_profile


func _build_charge_combat_profile(skill_id: StringName) -> CombatSkillDef:
	var charge_effect := CombatEffectDef.new()
	charge_effect.effect_type = &"charge"
	charge_effect.params = {
		"skill_id": skill_id,
		"base_distance": 3,
		"distance_by_level": {
			"1": 4,
			"3": 5,
			"5": 6,
		},
		"collision_base_damage": 10,
		"collision_size_gap_damage": 10,
	}

	var cast_variant := _build_cast_variant(
		&"charge_line",
		"直线冲锋",
		"选择同一行或同一列的目标格，沿该方向逐格冲锋。",
		0,
		&"single",
		1,
		[],
		[charge_effect]
	)

	return _build_ground_variant_combat_profile(skill_id, 6, 1, [cast_variant])


func _build_cast_variant(
	variant_id: StringName,
	display_name: String,
	description: String,
	min_skill_level: int,
	footprint_pattern: StringName,
	required_coord_count: int,
	allowed_base_terrains: Array[StringName],
	effect_defs: Array[CombatEffectDef]
) -> CombatCastVariantDef:
	var cast_variant := CombatCastVariantDef.new()
	cast_variant.variant_id = variant_id
	cast_variant.display_name = display_name
	cast_variant.description = description
	cast_variant.min_skill_level = min_skill_level
	cast_variant.target_mode = &"ground"
	cast_variant.footprint_pattern = footprint_pattern
	cast_variant.required_coord_count = required_coord_count
	cast_variant.allowed_base_terrains = allowed_base_terrains.duplicate()
	cast_variant.effect_defs = effect_defs.duplicate()
	return cast_variant


func _build_terrain_replace_effect(terrain: StringName) -> CombatEffectDef:
	var effect_def: CombatEffectDef = CombatEffectDef.new()
	effect_def.effect_type = &"terrain_replace"
	effect_def.terrain_replace_to = terrain
	return effect_def


func _build_height_delta_effect(height_delta: int) -> CombatEffectDef:
	var effect_def: CombatEffectDef = CombatEffectDef.new()
	effect_def.effect_type = &"height_delta"
	effect_def.height_delta = height_delta
	return effect_def


func _build_timed_terrain_effect(
	terrain_effect_id: StringName,
	power: int,
	duration_tu: int,
	tick_interval_tu: int,
	tick_effect_type: StringName = &"damage",
	status_id: StringName = &"",
	target_team_filter: StringName = &"",
	scaling_attribute_id: StringName = &"physical_attack",
	defense_attribute_id: StringName = &"physical_defense",
	resistance_attribute_id: StringName = &""
) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"terrain_effect"
	effect_def.terrain_effect_id = terrain_effect_id
	effect_def.tick_effect_type = tick_effect_type
	effect_def.power = power
	effect_def.duration_tu = maxi(duration_tu, 0)
	effect_def.tick_interval_tu = maxi(tick_interval_tu, 1)
	effect_def.stack_behavior = &"refresh"
	if target_team_filter != &"":
		effect_def.effect_target_team_filter = target_team_filter
	if tick_effect_type == &"damage":
		effect_def.scaling_attribute_id = scaling_attribute_id
		effect_def.defense_attribute_id = defense_attribute_id
		if resistance_attribute_id != &"":
			effect_def.resistance_attribute_id = resistance_attribute_id
	if status_id != &"":
		effect_def.status_id = status_id
		effect_def.params = {
			"duration": 1,
		}
	return effect_def


func _register_skill(skill_def: SkillDef) -> void:
	if skill_def == null or skill_def.skill_id == &"":
		_validation_errors.append("Encountered a skill definition without a skill_id.")
		return
	if _skill_defs.has(skill_def.skill_id):
		if _resource_skill_ids.has(skill_def.skill_id):
			return
		_validation_errors.append("Duplicate skill_id registered: %s" % String(skill_def.skill_id))
		return
	_skill_defs[skill_def.skill_id] = skill_def


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
		_validation_errors.append("Encountered a quest definition without a quest_id.")
		return
	if _quest_defs.has(quest_def.quest_id):
		_validation_errors.append("Duplicate quest_id registered: %s" % String(quest_def.quest_id))
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

	for quest_key in ProgressionDataUtils.sorted_string_keys(_quest_defs):
		var quest_id := StringName(quest_key)
		var quest_def := _quest_defs.get(quest_id) as QuestDef
		_append_invalid_quest_errors(errors, quest_id, quest_def)

	return errors


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
	if skill_def.max_level <= 0:
		errors.append("Skill %s must have max_level >= 1." % String(skill_id))
	if skill_def.mastery_curve.size() != skill_def.max_level:
		errors.append("Skill %s mastery_curve size must match max_level." % String(skill_id))

	_append_skill_requirement_errors(errors, skill_id, skill_def.learn_requirements, "learn_requirements")
	_append_skill_level_requirement_errors(errors, skill_id, skill_def.skill_level_requirements)
	_append_skill_requirement_errors(errors, skill_id, skill_def.upgrade_source_skill_ids, "upgrade_source_skill_ids")
	for achievement_id in skill_def.achievement_requirements:
		if achievement_id == &"":
			errors.append("Skill %s has an empty achievement requirement." % String(skill_id))

	if skill_def.unlock_mode == &"composite_upgrade" and skill_def.upgrade_source_skill_ids.is_empty():
		errors.append("Skill %s is composite_upgrade but missing upgrade_source_skill_ids." % String(skill_id))


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
	if achievement_def.rewards.is_empty():
		errors.append("Achievement %s must define at least one reward." % String(achievement_id))

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
		match reward.reward_type:
			AchievementRewardDef.TYPE_SKILL_UNLOCK, AchievementRewardDef.TYPE_SKILL_MASTERY:
				if not _skill_defs.has(reward.target_id):
					errors.append(
						"Achievement %s references missing skill %s." % [String(achievement_id), String(reward.target_id)]
					)
			AchievementRewardDef.TYPE_ATTRIBUTE_DELTA:
				pass
			AchievementRewardDef.TYPE_KNOWLEDGE_UNLOCK:
				pass
			_:
				errors.append(
					"Achievement %s uses unsupported reward_type %s." % [
						String(achievement_id),
						String(reward.reward_type),
					]
				)


func _append_invalid_quest_errors(
	errors: Array[String],
	quest_id: StringName,
	quest_def: QuestDef
) -> void:
	if quest_def == null:
		return
	for validation_error in quest_def.validate_schema():
		errors.append("Quest %s: %s" % [String(quest_id), validation_error])
