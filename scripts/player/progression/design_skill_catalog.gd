## 文件说明：该脚本负责集中注册按设计文档保留的技能定义，并承接少量兼容当前运行时的特殊技能构造。
## 审查重点：重点核对技能集合是否仅来自设计文档、字段映射是否稳定，以及兼容技能是否明确标注过渡语义。
## 备注：这里不实现技能真实新逻辑，只在定义层保留文档字段与当前运行时可识别的占位表达。

class_name DesignSkillCatalog
extends RefCounted

const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const AttributeModifier = preload("res://scripts/player/progression/attribute_modifier.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")


func register_warrior_skills(register_skill: Callable) -> void:
	register_skill.call(_build_warrior_heavy_strike_skill())
	register_skill.call(_build_warrior_sweeping_slash_skill())
	register_skill.call(_build_warrior_piercing_thrust_skill())
	register_skill.call(_build_warrior_guard_break_skill())
	register_skill.call(_build_warrior_execution_cleave_skill())
	register_skill.call(_build_warrior_jump_slash_skill())
	register_skill.call(_build_warrior_backstep_skill())
	register_skill.call(_build_warrior_guard_skill())
	register_skill.call(_build_warrior_shield_wall_skill())
	register_skill.call(_build_warrior_battle_recovery_skill())
	register_skill.call(_build_warrior_shield_bash_skill())
	register_skill.call(_build_warrior_taunt_skill())
	register_skill.call(_build_warrior_war_cry_skill())
	register_skill.call(_build_warrior_true_dragon_slash_skill())
	register_skill.call(_build_warrior_combo_strike_skill())
	register_skill.call(_build_warrior_aura_slash_skill())
	register_skill.call(_build_warrior_whirlwind_slash_skill())
	register_skill.call(_build_saint_blade_combo_skill())


func register_archer_skills(register_skill: Callable) -> void:
	register_skill.call(_build_active_skill(&"archer_aimed_shot", "精准射击", "稳住呼吸后射出高精度一箭；若目标本回合尚未行动，则暴击率额外 `+15%`。", [&"archer", &"ranged", &"bow", &"output"], 5, &"unit", &"enemy", &"single", 0, 1, 0, 1, 0, 1, 10, [_build_damage_effect(13, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_archer_armor_piercer_skill())
	register_skill.call(_build_active_skill(&"archer_heartseeker", "追心箭", "若目标已带有 `标记`，本次伤害改为 `150%`；若因此击杀，则返还 `1` 点 `stamina`。", [&"archer", &"ranged", &"bow", &"output"], 5, &"unit", &"enemy", &"single", 0, 1, 0, 1, 0, 2, 5, [_build_damage_effect(12, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_long_draw", "满弦狙击", "高蓄力狙杀技；若本回合已移动，则伤害降为 `135%`。", [&"archer", &"ranged", &"bow", &"output"], 6, &"unit", &"enemy", &"single", 0, 2, 0, 2, 0, 3, 15, [_build_damage_effect(16, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_split_bolt", "裂风重矢", "粗重箭矢沿直线贯穿最多 `2` 个敌人，适合打站位重叠目标。", [&"archer", &"ranged", &"bow", &"output"], 5, &"ground", &"enemy", &"line", 1, 2, 0, 2, 0, 2, -5, [_build_damage_effect(14, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_execution_arrow", "猎手终结", "根据目标已损失生命提高伤害；目标生命越低，终结收益越高。", [&"archer", &"ranged", &"bow", &"output"], 5, &"unit", &"enemy", &"single", 0, 2, 0, 2, 0, 3, 0, [_build_damage_effect(14, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_double_nock", "双弦连射", "连续射出两箭；第一箭未命中时不触发第二箭。", [&"archer", &"ranged", &"bow", &"output"], 4, &"unit", &"enemy", &"single", 0, 2, 0, 1, 0, 2, 0, [_build_damage_effect(7, &"physical_attack", &"physical_defense"), _build_damage_effect(7, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_far_horizon", "天际远射", "超远距离重箭；若自身处于 `预瞄` 或高地，则忽略命中惩罚。", [&"archer", &"ranged", &"bow", &"output"], 7, &"unit", &"enemy", &"single", 0, 2, 0, 0, 1, 3, -10, [_build_damage_effect(15, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_skirmish_step", "游击步", "立即移动最多 `2` 格并获得 `预瞄`，是弓箭手的基础转线手段。", [&"archer", &"ranged", &"bow", &"mobility"], 0, &"unit", &"ally", &"self", 0, 1, 0, 1, 0, 1, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_backstep_shot", "后跃射", "先攻击再后撤 `2` 格；若后撤路径被阻挡，则只结算攻击。", [&"archer", &"ranged", &"bow", &"mobility"], 4, &"unit", &"enemy", &"single", 0, 1, 0, 1, 0, 2, 5, [_build_damage_effect(10, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_sidewind_slide", "侧滑换位", "攻击前或攻击后允许横向移动 `1` 格，用于避开直线冲锋或重构夹角。", [&"archer", &"ranged", &"bow", &"mobility"], 4, &"unit", &"enemy", &"single", 0, 1, 0, 1, 0, 2, 0, [_build_damage_effect(10, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_running_shot", "奔袭射击", "若本回合先移动至少 `3` 格，则取消命中惩罚，并额外施加 `标记`。", [&"archer", &"ranged", &"bow", &"mobility"], 4, &"unit", &"enemy", &"single", 0, 1, 0, 1, 0, 1, -5, [_build_damage_effect(11, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_grapple_redeploy", "索钩转位", "牵引至高地、障碍物边缘或己方布置点附近；转位后下一个弓技射程 `+1`。", [&"archer", &"ranged", &"bow", &"mobility"], 4, &"unit", &"ally", &"self", 0, 1, 0, 1, 0, 3, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_evasive_roll", "翻滚卸力", "移动 `2` 格并获得直到回合结束的 `闪避 +20%`，适合脱离贴身威胁。", [&"archer", &"ranged", &"bow", &"mobility"], 0, &"unit", &"ally", &"self", 0, 1, 0, 1, 0, 2, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_highground_claim", "抢高位", "向指定高低差合法格位移动最多 `3` 格；若落点为高地，额外获得 `预瞄`。", [&"archer", &"ranged", &"bow", &"mobility"], 3, &"unit", &"ally", &"self", 0, 2, 0, 2, 0, 3, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_hunter_feint", "猎步佯退", "轻伤试探并附加 `标记`；命中后可免费移动 `1` 格。", [&"archer", &"ranged", &"bow", &"mobility"], 4, &"unit", &"enemy", &"single", 0, 1, 0, 1, 0, 2, 0, [_build_damage_effect(8, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_archer_pinning_shot_skill())
	register_skill.call(_build_active_skill(&"archer_tendon_splitter", "断筋箭", "命中后施加 `断筋`；对依赖冲锋和突进的敌人压制力很强。", [&"archer", &"ranged", &"bow", &"control"], 5, &"unit", &"enemy", &"single", 0, 1, 0, 1, 0, 2, -5, [_build_damage_effect(9, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_disrupting_arrow", "扰咒箭", "命中后令目标下一次技能消耗 `+1`，并使其施法类动作命中 `-15%`。", [&"archer", &"ranged", &"bow", &"control"], 5, &"unit", &"enemy", &"single", 0, 1, 0, 1, 0, 3, 5, [_build_damage_effect(8, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_flash_whistle", "炫目鸣镝", "爆出刺耳鸣响，范围内敌人获得 `命中 -15%` 且失去反击资格，持续 `1` 回合。", [&"archer", &"ranged", &"bow", &"control"], 5, &"ground", &"enemy", &"radius", 1, 2, 0, 2, 0, 3, 0, [_build_damage_effect(7, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_tripwire_arrow", "绊索箭", "在直线 `3` 格上布置绊索带；首个穿过的敌人立即停止移动并获得 `压制`。", [&"archer", &"ranged", &"bow", &"control"], 4, &"ground", &"enemy", &"line", 1, 2, 0, 1, 0, 2, 0, [_build_damage_effect(6, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_shield_breaker", "破盾箭", "对处于格挡、护卫或防御姿态的目标追加 `+25%` 伤害，并移除其防御姿态。", [&"archer", &"ranged", &"bow", &"control"], 5, &"unit", &"enemy", &"single", 0, 1, 0, 1, 0, 2, -5, [_build_damage_effect(10, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_fearsignal_shot", "惊禽哨箭", "扇形压制射击，命中敌人获得 `压制`；若目标本就带有 `标记`，额外后退 `1` 格。", [&"archer", &"ranged", &"bow", &"control"], 5, &"ground", &"enemy", &"cone", 1, 2, 0, 2, 0, 3, 0, [_build_damage_effect(8, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_harrier_mark", "猎印追缉", "纯功能技能，给目标施加强化版 `标记` `2` 回合；所有友军远程攻击对此目标命中 `+10%`。", [&"archer", &"ranged", &"bow", &"control"], 5, &"unit", &"enemy", &"single", 0, 1, 0, 1, 0, 2, 10, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_archer_multishot_skill())
	register_skill.call(_build_active_skill(&"archer_arrow_rain", "箭雨", "标准范围火力；命中的敌人额外获得 `压制`，适合拆散抱团站位。", [&"archer", &"ranged", &"bow", &"aoe"], 5, &"ground", &"enemy", &"radius", 1, 2, 0, 2, 0, 3, -10, [_build_damage_effect(8, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_fan_volley", "扇幕齐射", "中距离扇面清线技，对近距离多前排组合威胁较高。", [&"archer", &"ranged", &"bow", &"aoe"], 4, &"ground", &"enemy", &"cone", 1, 2, 0, 2, 0, 2, 0, [_build_damage_effect(9, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_suppressive_fire", "压制射击", "对一条直线区域持续覆盖；被命中的敌人获得 `压制`，该路径在下回合前移动成本 `+1`。", [&"archer", &"ranged", &"bow", &"aoe"], 5, &"ground", &"enemy", &"line", 1, 2, 0, 2, 0, 3, 0, [_build_damage_effect(7, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_breach_barrage", "贯阵齐发", "高阶穿阵技，直线上的每命中一个敌人，下一名目标额外承受 `+10%` 伤害。", [&"archer", &"ranged", &"bow", &"aoe"], 6, &"ground", &"enemy", &"line", 1, 2, 0, 2, 1, 4, -10, [_build_damage_effect(12, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_blast_arrow", "爆裂箭", "在落点爆开冲击；对轻型掩体、召唤物或低护甲后排有较高压制价值。", [&"archer", &"ranged", &"bow", &"aoe"], 5, &"ground", &"enemy", &"radius", 1, 2, 0, 0, 1, 3, 0, [_build_damage_effect(10, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_hunting_grid", "狩猎网阵", "在区域内撒布索绳与短箭；敌人首次离开区域时再受一次 `50%` 伤害并获得 `断筋`。", [&"archer", &"ranged", &"bow", &"aoe"], 4, &"ground", &"enemy", &"radius", 2, 3, 0, 0, 1, 4, 0, [_build_damage_effect(6, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"archer_killing_field", "猎场封锁", "招牌领域技；区域持续 `2` 回合，敌人每次在其中结束行动时受到追击伤害并自动附加 `标记`。", [&"archer", &"ranged", &"bow", &"aoe"], 5, &"ground", &"enemy", &"radius", 2, 3, 0, 0, 2, 5, -10, [_build_damage_effect(7, &"physical_attack", &"physical_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))


func register_mage_skills(register_skill: Callable) -> void:
	register_skill.call(_build_mage_fireball_skill())
	register_skill.call(_build_active_skill(&"mage_cinder_bolt", "余烬飞弹", "低耗起手法术；命中后施加 `灼烧`，用于给后续火焰连段打底。", [&"mage", &"magic", &"fire"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 0, 10, [_build_damage_effect(10, &"magic_attack", &"magic_defense", &"fire_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_flame_spear", "炎枪术", "火焰沿直线贯穿最多 `2` 名敌人；若路径上存在 `mud`，额外留下持续 `1` 回合的燃烧地格。", [&"mage", &"magic", &"fire"], 5, &"ground", &"enemy", &"line", 1, 2, 2, 0, 0, 1, 0, [_build_damage_effect(12, &"magic_attack", &"magic_defense", &"fire_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_searing_orb", "灼热法珠", "轻量范围技；中心目标若已 `寒蚀`，则本次额外触发一次 `40%` 爆燃伤害。", [&"mage", &"magic", &"fire"], 4, &"ground", &"enemy", &"radius", 1, 2, 1, 0, 0, 1, 0, [_build_damage_effect(10, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_burning_hands", "焚掌喷流", "近距离扇形压制；对贴脸敌人清场效率高，适合法师被近战压迫时反打。", [&"mage", &"magic", &"fire"], 0, &"ground", &"enemy", &"cone", 1, 2, 1, 0, 0, 1, 5, [_build_damage_effect(11, &"magic_attack", &"magic_defense", &"fire_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_ember_mine", "灰烬雷", "在指定地格埋下火种；首个踩入的敌人受到伤害并获得 `灼烧`。", [&"mage", &"magic", &"fire"], 4, &"ground", &"enemy", &"single", 0, 2, 1, 0, 0, 2, 0, [_build_damage_effect(9, &"magic_attack", &"magic_defense", &"fire_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_ashen_mark", "烬痕烙印", "轻伤并施加强化版 `灼烧`；目标承受下一次火焰技能时额外受到 `+20%` 伤害。", [&"mage", &"magic", &"fire"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 1, 10, [_build_damage_effect(7, &"magic_attack", &"magic_defense", &"fire_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_molten_burst", "熔爆术", "对已带 `灼烧` 的敌人追加一次 `50%` 火焰余震；适合作为火系中段爆发点。", [&"mage", &"magic", &"fire"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(12, &"magic_attack", &"magic_defense", &"fire_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_inferno_pillar", "炽柱爆发", "从落点向一条直线喷发火柱；命中的敌人若本回合已行动，则额外施加 `staggered`。", [&"mage", &"magic", &"fire"], 5, &"ground", &"enemy", &"line", 1, 2, 2, 0, 0, 2, -5, [_build_damage_effect(14, &"magic_attack", &"magic_defense", &"fire_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_fire_wall", "火墙术", "生成持续 `2` 回合的火墙；穿越或在其上结束行动的敌人承受火焰伤害并获得 `灼烧`。", [&"mage", &"magic", &"fire"], 4, &"ground", &"enemy", &"line", 1, 2, 2, 0, 0, 3, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"fire_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_comet_drop", "焰星坠", "高阶落点爆破技；中心敌人额外承受 `+25%` 伤害。", [&"mage", &"magic", &"fire"], 5, &"ground", &"enemy", &"radius", 2, 3, 3, 0, 0, 3, -10, [_build_damage_effect(15, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_phoenix_arc", "凤焰弧", "火焰掠过路径时优先打击血量最低的目标；若击杀，施法者获得 `咏唱`。", [&"mage", &"magic", &"fire"], 5, &"ground", &"enemy", &"line", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(12, &"magic_attack", &"magic_defense", &"fire_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_scorch_wave", "灼流波", "中距离扇面清场技；对 `blinded` 目标额外 `+15%` 伤害。", [&"mage", &"magic", &"fire"], 4, &"ground", &"enemy", &"cone", 1, 2, 2, 0, 0, 1, 0, [_build_damage_effect(10, &"magic_attack", &"magic_defense", &"fire_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_delayed_fireball", "延爆火球", "建议使用 `cast_variants` 实现即时引爆与延后一回合引爆两种形态；延后形态额外施加 `灼烧`。", [&"mage", &"magic", &"fire"], 5, &"ground", &"enemy", &"radius", 2, 3, 3, 0, 0, 4, 0, [_build_damage_effect(17, &"magic_attack", &"magic_defense", &"fire_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_sunflare_bomb", "日耀焚弹", "火焰与强光并发；命中的敌人获得 `blinded`，是火系招牌清场与拆阵技能。", [&"mage", &"magic", &"fire"], 5, &"ground", &"enemy", &"radius", 2, 3, 3, 0, 1, 4, 0, [_build_damage_effect(12, &"magic_attack", &"magic_defense", &"fire_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_mage_ice_lance_skill())
	register_skill.call(_build_active_skill(&"mage_frost_bolt", "霜击术", "低耗冰系基础法术；命中施加 `寒蚀`，给队友近战和弓箭补伤口。", [&"mage", &"magic", &"ice"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 0, 10, [_build_damage_effect(9, &"magic_attack", &"magic_defense", &"freeze_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_rime_burst", "冻雾爆", "落点扩散冰雾；区域内敌人移动力 `-1`，持续 `1` 回合。", [&"mage", &"magic", &"ice"], 4, &"ground", &"enemy", &"radius", 1, 2, 1, 0, 0, 1, 0, [_build_damage_effect(10, &"magic_attack", &"magic_defense", &"freeze_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_glacier_edge", "冰棱穿刺", "冰棱从地面连续突起；若目标站在 `mud` 中，则额外施加 `浸湿`。", [&"mage", &"magic", &"ice"], 5, &"ground", &"enemy", &"line", 1, 2, 2, 0, 0, 1, 0, [_build_damage_effect(12, &"magic_attack", &"magic_defense", &"freeze_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_snowblind", "雪幕致盲", "冰晶碎雪扰乱视线；命中敌人获得 `blinded` 与 `寒蚀`。", [&"mage", &"magic", &"ice"], 4, &"ground", &"enemy", &"cone", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"freeze_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_hail_storm", "冰雹阵", "小范围多段落冰；对轻甲后排与密集站位都具备稳定压制。", [&"mage", &"magic", &"ice"], 5, &"ground", &"enemy", &"radius", 2, 3, 2, 0, 0, 2, -5, [_build_damage_effect(11, &"magic_attack", &"magic_defense", &"freeze_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_cryo_lock", "寒锁术", "冰锁缠身；目标获得 `rooted`，并在下次承受火焰伤害时额外受到 `+20%` 蒸腾爆裂。", [&"mage", &"magic", &"ice"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 2, 5, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"freeze_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_permafrost_field", "永冻地面", "生成持续 `2` 回合的冰面领域；敌人穿过区域时移动成本 `+1`，回合结束再承受一次寒伤。", [&"mage", &"magic", &"ice"], 4, &"ground", &"enemy", &"radius", 2, 3, 2, 0, 0, 3, 0, [_build_damage_effect(7, &"magic_attack", &"magic_defense", &"freeze_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_shatter_lance", "碎晶枪", "对 `frozen`、`shocked` 或 `浸湿` 目标造成额外伤害，是典型元素联动终结技。", [&"mage", &"magic", &"ice"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 2, 10, [_build_damage_effect(13, &"magic_attack", &"magic_defense", &"freeze_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_mirror_frost", "镜霜折射", "获得 `咏唱`，并令下一个冰系技能额外命中一名相邻目标。", [&"mage", &"magic", &"ice"], 0, &"unit", &"ally", &"self", 0, 1, 1, 0, 0, 2, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_cold_snap", "霜爆回响", "若区域内存在带 `寒蚀` 的敌人，则对其追加一次 `50%` 碎裂伤害。", [&"mage", &"magic", &"ice"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(10, &"magic_attack", &"magic_defense", &"freeze_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_frozen_step", "凝冰移形", "短距位移最多 `2` 格，并在起点与终点之间留下持续 `1` 回合的冰面。", [&"mage", &"magic", &"ice"], 0, &"unit", &"ally", &"self", 0, 1, 1, 0, 0, 2, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_glacial_prison", "冰牢术", "以冰棱封住目标；优先施加 `rooted`，若目标已 `寒蚀` 则改为 `frozen 1`。", [&"mage", &"magic", &"ice"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 3, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"freeze_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_whiteout", "暴雪迷界", "范围内敌人命中 `-15%`，远程反击失效，持续 `1` 回合。", [&"mage", &"magic", &"ice"], 5, &"ground", &"enemy", &"radius", 2, 3, 3, 0, 0, 3, 0, [_build_damage_effect(9, &"magic_attack", &"magic_defense", &"freeze_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_absolute_zero", "绝零坍缩", "冰系招牌终极技；对已 `浸湿` 的目标优先结算 `frozen`。", [&"mage", &"magic", &"ice"], 5, &"ground", &"enemy", &"radius", 2, 3, 3, 0, 1, 4, -10, [_build_damage_effect(17, &"magic_attack", &"magic_defense", &"freeze_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_mage_chain_lightning_skill())
	register_skill.call(_build_active_skill(&"mage_spark_javelin", "电矛术", "低耗雷系点射；命中后施加 `感电`。", [&"mage", &"magic", &"lightning"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 0, 5, [_build_damage_effect(10, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_static_field", "静电场", "形成短时电场；区域内敌人获得 `shocked`，适合为连锁闪电铺路。", [&"mage", &"magic", &"lightning"], 4, &"ground", &"enemy", &"radius", 1, 2, 1, 0, 0, 1, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_thunder_lance", "惊雷贯枪", "直线穿透雷击；对大型单位或成排敌人收益明显。", [&"mage", &"magic", &"lightning"], 5, &"ground", &"enemy", &"line", 1, 2, 2, 0, 0, 1, 0, [_build_damage_effect(12, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_ball_lightning", "球形闪电", "生成持续 `2` 回合的游走电球；每回合电击附近敌人并施加 `感电`。", [&"mage", &"magic", &"lightning"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(10, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_voltage_hook", "伏电牵引", "用电流把目标拉近 `1` 格；若目标已 `shocked`，则再额外 `staggered 1`。", [&"mage", &"magic", &"lightning"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 2, 5, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_storm_call", "唤雷术", "从空中落下直击雷；对高地或空旷地格额外 `+10%` 伤害。", [&"mage", &"magic", &"lightning"], 5, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(12, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_arc_net", "电网术", "在一条直线上挂起电网；首个穿过的敌人被 `rooted` 并附加 `感电`。", [&"mage", &"magic", &"lightning"], 4, &"ground", &"enemy", &"line", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_overcharge", "过载灌注", "强化下一个雷系技能：伤害 `+20%`，若触发连锁则额外多跳一次。", [&"mage", &"magic", &"lightning"], 0, &"unit", &"ally", &"self", 0, 1, 1, 0, 0, 2, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_thunderclap", "雷暴震响", "近身反制型法术；命中的敌人获得 `staggered`，适合拆贴身战士的节奏。", [&"mage", &"magic", &"lightning"], 0, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(10, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_conductive_mark", "导电印记", "纯联动技能；目标在 `2` 回合内承受的第一个雷系技能必定附加 `shocked`。", [&"mage", &"magic", &"lightning"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 1, 10, [_build_damage_effect(6, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_plasma_beam", "等离子光束", "高穿透直线法术；若路径上单位已 `感电`，则本次对其无视部分抗性。", [&"mage", &"magic", &"lightning"], 6, &"ground", &"enemy", &"line", 1, 3, 3, 0, 0, 3, -5, [_build_damage_effect(14, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_tempest_ring", "风暴环", "以自身为中心向外释放电弧；对试图包围法师的敌人压制力极强。", [&"mage", &"magic", &"lightning"], 0, &"ground", &"enemy", &"radius", 2, 3, 2, 0, 0, 3, 0, [_build_damage_effect(9, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_skybreaker", "天穹裂雷", "大范围高爆发雷击；若区域内至少有一个 `浸湿` 目标，则全体额外获得 `感电`。", [&"mage", &"magic", &"lightning"], 6, &"ground", &"enemy", &"radius", 2, 3, 3, 0, 1, 4, -10, [_build_damage_effect(16, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_storm_nexus", "暴风核心", "生成持续 `2` 回合的雷暴核心；每回合自动对最近的 `2` 名敌人触发小型连锁闪击。", [&"mage", &"magic", &"lightning"], 5, &"ground", &"enemy", &"radius", 2, 3, 3, 0, 1, 4, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_mage_fossil_to_mud_alias())
	register_skill.call(_build_active_skill(&"mage_stone_spike", "岩刺术", "沿直线突起石刺；对被 `rooted` 或站在 `mud` 中的目标额外伤害。", [&"mage", &"magic", &"earth"], 4, &"ground", &"enemy", &"line", 1, 2, 1, 0, 0, 1, 0, [_build_damage_effect(10, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_quicksand", "流沙陷区", "生成小型流沙区；敌人首次进入时立即停止移动并获得 `寒蚀` 或 `浸湿` 中的任一软控标签。", [&"mage", &"magic", &"earth"], 4, &"ground", &"enemy", &"radius", 1, 2, 1, 0, 0, 2, 0, [_build_damage_effect(6, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_rock_armor", "岩甲术", "给友军施加 `法盾` 与物理减伤，适合保护前排或炮台召唤物。", [&"mage", &"magic", &"earth"], 3, &"unit", &"ally", &"single", 0, 1, 1, 0, 0, 2, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_boulder_throw", "巨岩投射", "朴素但扎实的土石高伤单体技；命中后附带小幅击退。", [&"mage", &"magic", &"earth"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 2, 0, [_build_damage_effect(13, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_fault_line", "地裂线", "沿直线撕开地面；命中的敌人获得 `staggered`，可与弓手远程集火配合。", [&"mage", &"magic", &"earth"], 5, &"ground", &"enemy", &"line", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(12, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_obsidian_shards", "黑曜石碎群", "对护甲型单位更有效；若目标带有 `armor_break`，则额外追加一次 `40%` 割裂伤害。", [&"mage", &"magic", &"earth"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(12, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_earthen_grasp", "地脉束缚", "从地面伸出岩手束缚目标；命中后施加 `rooted`。", [&"mage", &"magic", &"earth"], 4, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 1, 5, [_build_damage_effect(8, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_mud_wave", "泥潮术", "向前推送泥流；若前方已有 `mud` 地格，则范围 `+1`。", [&"mage", &"magic", &"earth"], 4, &"ground", &"enemy", &"cone", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_rampart_raise", "升墙术", "建议做成 `cast_variants`，提供“短墙 / 单柱高地 / 双格掩体”三种形态，用于切线和保后排。", [&"mage", &"magic", &"earth"], 4, &"ground", &"enemy", &"line", 1, 2, 2, 0, 0, 3, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_crystal_pike", "晶簇突刺", "在落点形成高密度水晶刺群；对 `frozen` 目标追加一次碎裂伤害。", [&"mage", &"magic", &"earth"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(12, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_gravity_sink", "重压塌陷", "以重力挤压区域；命中的敌人移动力 `-1`，并更容易被战士冲锋定住。", [&"mage", &"magic", &"earth"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(11, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_terra_pulse", "地脉脉冲", "以自身为中心扩散地脉震波；可把靠近的敌人推出关键格位。", [&"mage", &"magic", &"earth"], 0, &"ground", &"enemy", &"radius", 2, 3, 2, 0, 0, 3, 0, [_build_damage_effect(10, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_sandstorm", "沙暴术", "低伤高扰乱法术；区域内敌人获得 `blinded`，远程单位尤其难受。", [&"mage", &"magic", &"earth"], 5, &"ground", &"enemy", &"radius", 2, 3, 2, 0, 0, 3, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_meteor_swarm", "陨星雨", "火土复合型禁咒；落点中心额外降低地形高度或生成燃烧残坑。", [&"mage", &"magic", &"earth"], 6, &"ground", &"enemy", &"radius", 2, 3, 3, 0, 2, 5, -10, [_build_damage_effect(18, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_arcane_missile", "奥术飞弹", "适合作为法师的稳定多目标点射基准；单次施法锁定 `2-3` 个敌人依次命中。", [&"mage", &"magic", &"arcane"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 1, 15, [_build_damage_effect(7, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"multi_unit", 2, 3, &"manual", &"book"))
	register_skill.call(_build_active_skill(&"mage_mana_burst", "法涌爆裂", "纯奥术单体爆发；若自身处于 `咏唱`，则本次不消耗冷却。", [&"mage", &"magic", &"arcane"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 1, 0, [_build_damage_effect(12, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_force_lance", "力场矛", "奥术直刺；对 `armor_break` 目标额外 `+20%` 伤害。", [&"mage", &"magic", &"arcane"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 1, 5, [_build_damage_effect(12, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_arcane_orbit", "轨道法珠", "在自身周围生成 `2` 枚法珠；首次受到近战接触时自动反击。", [&"mage", &"magic", &"arcane"], 0, &"unit", &"ally", &"self", 0, 2, 2, 0, 0, 2, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_spellbreaker", "裂咒术", "拆除目标身上的一个增益或法盾；对带护盾的施法敌人压制力很强。", [&"mage", &"magic", &"arcane"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 2, 10, [_build_damage_effect(9, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_null_zone", "禁魔区", "区域内敌人下一个技能消耗 `+1`，并使施法类动作命中 `-15%`。", [&"mage", &"magic", &"arcane"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 3, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_phase_beam", "相位射线", "穿透直线法术；若路径上存在召唤物或掩体，则优先无视其阻挡。", [&"mage", &"magic", &"arcane"], 6, &"ground", &"enemy", &"line", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(14, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_runic_trap", "符印陷阱", "在指定地格布置奥术印记；被触发时爆发奥术伤害并施加 `奥脆`。", [&"mage", &"magic", &"arcane"], 4, &"ground", &"enemy", &"single", 0, 2, 1, 0, 0, 2, 0, [_build_damage_effect(10, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_arcane_echo", "奥术回响", "令下一个单体法术以 `50%` 威力再触发一次；适合配合高价值点杀术。", [&"mage", &"magic", &"arcane"], 0, &"unit", &"ally", &"self", 0, 2, 2, 0, 0, 3, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_prism_spray", "棱镜喷射", "扇形混合伤害技；随机附带 `blinded`、`奥脆` 或 `感电` 之一。", [&"mage", &"magic", &"arcane"], 4, &"ground", &"enemy", &"cone", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(11, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_seal_of_binding", "束缚印", "在目标身上刻下奥术符印；目标下一个位移或冲锋类动作会直接失败。", [&"mage", &"magic", &"arcane"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 2, 5, [_build_damage_effect(7, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_mirror_shard", "棱镜碎反", "命中后可对与其相邻的另一目标折射一次 `60%` 奥术伤害。", [&"mage", &"magic", &"arcane"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 2, 0, [_build_damage_effect(10, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_ether_pull", "以太牵引", "轻伤并把目标拉近或推远 `1` 格；用于把敌人拖进 `fire_wall`、`mud` 或 `null_zone`。", [&"mage", &"magic", &"arcane"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 1, 10, [_build_damage_effect(6, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_starfall_gate", "星门陨落", "以短暂星门投放奥术碎星；若目标带有 `奥脆`，则额外返还 `1` 点 `mp`。", [&"mage", &"magic", &"arcane"], 5, &"ground", &"enemy", &"radius", 1, 3, 3, 0, 0, 3, 0, [_build_damage_effect(12, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_arcane_cataclysm", "奥能崩解", "奥术系高阶领域爆发；命中的敌人统一获得 `奥脆`，方便后续整队压制。", [&"mage", &"magic", &"arcane"], 5, &"ground", &"enemy", &"radius", 2, 3, 3, 0, 1, 4, -10, [_build_damage_effect(16, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_blur", "幻身术", "获得直到下回合开始的 `闪避 +20%`，是法师基础自保技。", [&"mage", &"magic", &"illusion"], 0, &"unit", &"ally", &"self", 0, 1, 1, 0, 0, 1, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_mirror_image", "镜影术", "生成镜像分身吸收攻击；优先用于应对高爆发远程或刺客切入。", [&"mage", &"magic", &"illusion"], 0, &"unit", &"ally", &"self", 0, 2, 2, 0, 0, 3, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_phantasmal_blade", "幻刃诱杀", "若目标已 `blinded` 或 `staggered`，则本次改为 `140%` 伤害。", [&"mage", &"magic", &"illusion"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 1, 10, [_build_damage_effect(12, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_glitter_dust", "炫尘术", "扬起闪耀粉尘；命中敌人获得 `blinded`，并揭示隐身或潜伏单位。", [&"mage", &"magic", &"illusion"], 4, &"ground", &"enemy", &"radius", 1, 2, 1, 0, 0, 1, 0, [_build_damage_effect(7, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_color_spray", "七色炫光", "近距离扇面幻光压制；随机使敌人 `blinded`、`staggered` 或 `感电`。", [&"mage", &"magic", &"illusion"], 0, &"ground", &"enemy", &"cone", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(9, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_sleep_dust", "沉眠粉雾", "低伤纯控制；优先延后低意志目标的下一次行动。", [&"mage", &"magic", &"illusion"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 3, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_fear_whisper", "惊惧耳语", "命中后使目标后退 `1` 格并命中 `-10%`，适合拆近战突脸节奏。", [&"mage", &"magic", &"illusion"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 2, 5, [_build_damage_effect(6, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_mislead", "误导术", "为友军制造短暂伪像，令敌方下一次单体技能有概率转空。", [&"mage", &"magic", &"illusion"], 3, &"unit", &"ally", &"single", 0, 1, 1, 0, 0, 2, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_invisibility", "隐形术", "直到攻击前提升闪避与走位安全性；适合保法师或辅助突击职业切入。", [&"mage", &"magic", &"illusion"], 3, &"unit", &"ally", &"single", 0, 2, 2, 0, 0, 3, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_hallucinatory_field", "蜃景场", "区域内敌人命中 `-15%`，移动成本 `+1`，对 AI 路径选择也应造成扰乱。", [&"mage", &"magic", &"illusion"], 4, &"ground", &"enemy", &"radius", 2, 3, 2, 0, 0, 3, 0, [_build_damage_effect(5, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_memory_spike", "记忆针刺", "干扰咒式记忆；目标下一次技能消耗 `+1`。", [&"mage", &"magic", &"illusion"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 2, 10, [_build_damage_effect(8, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_silence_orb", "寂声法球", "区域内敌方施法类技能命中 `-20%`，并更难触发高阶连锁。", [&"mage", &"magic", &"illusion"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 3, 0, [_build_damage_effect(6, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_confusion_wave", "乱心波", "扇形精神扰动；命中的敌人下个回合优先攻击最近目标或失去最佳技能选择。", [&"mage", &"magic", &"illusion"], 4, &"ground", &"enemy", &"cone", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_phantom_cage", "幻牢术", "把目标封在幻象牢笼里；目标获得 `rooted`，并失去反击资格。", [&"mage", &"magic", &"illusion"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 2, 0, [_build_damage_effect(7, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_mass_hypnosis", "群体催眠", "高阶群控；优先压制低生命或低意志的后排单位。", [&"mage", &"magic", &"illusion"], 5, &"ground", &"enemy", &"radius", 2, 3, 3, 0, 1, 4, -5, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_summon_familiar", "召唤魔宠", "召唤小型辅助单位；提供视野、补刀或触发器效果。", [&"mage", &"magic", &"summon"], 0, &"unit", &"ally", &"self", 0, 1, 1, 0, 0, 3, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_ember_wisp", "余烬灵", "召出火焰精灵；每回合优先攻击最近敌人并施加 `灼烧`。", [&"mage", &"magic", &"summon"], 4, &"ground", &"enemy", &"single", 0, 2, 2, 0, 0, 3, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"fire_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_frost_sprite", "霜灵", "召唤擅长减速和冻结补刀的小型冰灵。", [&"mage", &"magic", &"summon"], 4, &"ground", &"enemy", &"single", 0, 2, 2, 0, 0, 3, 0, [_build_damage_effect(6, &"magic_attack", &"magic_defense", &"freeze_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_storm_sprite", "雷灵", "召唤会自动施加 `感电` 的雷灵，适合作为链式闪电的跳板。", [&"mage", &"magic", &"summon"], 4, &"ground", &"enemy", &"single", 0, 2, 2, 0, 0, 3, 0, [_build_damage_effect(7, &"magic_attack", &"magic_defense", &"lightning_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_arcane_turret", "奥术炮台", "固定炮台型召唤物；优先攻击带 `奥脆` 的目标。", [&"mage", &"magic", &"summon"], 4, &"ground", &"enemy", &"single", 0, 2, 2, 0, 0, 3, 0, [_build_damage_effect(9, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_runic_golem", "符文傀儡", "较硬的召唤前排；适合给法师争取安全施法位。", [&"mage", &"magic", &"summon"], 3, &"ground", &"enemy", &"single", 0, 3, 3, 0, 0, 4, 0, [_build_damage_effect(10, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_crystal_sentinel", "晶盾守卫", "召唤偏护卫型的水晶构装体，可为相邻友军提供减伤。", [&"mage", &"magic", &"summon"], 3, &"ground", &"enemy", &"single", 0, 3, 3, 0, 0, 4, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_spell_totem", "咒能图腾", "图腾周围友军获得 `咏唱` 或小幅 `mp` 回复；适合阵地战。", [&"mage", &"magic", &"summon"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 3, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_mana_leech", "蚀法孢子", "召唤会吸取敌方法力与法盾的小型孢子体，主打消耗战。", [&"mage", &"magic", &"summon"], 4, &"ground", &"enemy", &"single", 0, 2, 2, 0, 0, 3, 0, [_build_damage_effect(6, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_shadow_double", "阴影分身", "复制施法者的上一个单体技能一次低配版本，适合作为节奏延伸器。", [&"mage", &"magic", &"summon"], 0, &"unit", &"ally", &"self", 0, 2, 2, 0, 0, 3, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"negative_energy_resistance")], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_elemental_fusion", "元素合灵", "吞并一个现存元素召唤物，为自身提供对应元素强化 `2` 回合。", [&"mage", &"magic", &"summon"], 0, &"unit", &"ally", &"self", 0, 2, 2, 0, 0, 3, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_living_meteor", "活化陨核", "召出会缓慢滚动的熔核；沿途对敌人造成火土混合伤害。", [&"mage", &"magic", &"summon"], 4, &"ground", &"enemy", &"radius", 1, 3, 3, 0, 0, 4, 0, [_build_damage_effect(11, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_warding_skull", "守望颅骨", "侦测型召唤物；揭示潜行单位，并在敌人接近时发射诅咒光束。", [&"mage", &"magic", &"summon"], 4, &"ground", &"enemy", &"single", 0, 2, 2, 0, 0, 3, 0, [_build_damage_effect(7, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_astral_page", "星界书页", "召唤漂浮书页协助施法；使相邻友军首次施法消耗 `-1 mp`。", [&"mage", &"magic", &"summon"], 4, &"ground", &"enemy", &"single", 0, 2, 2, 0, 0, 3, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_grand_conjuration", "大型召唤", "建议做成 `cast_variants`，在傀儡、元素兽、图腾组三类大型召唤间切换。", [&"mage", &"magic", &"summon"], 4, &"ground", &"enemy", &"radius", 1, 3, 3, 0, 1, 5, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_magic_shield", "魔力护盾", "为自身施加基础 `法盾`，是法师站场的最小保障。", [&"mage", &"magic", &"support"], 0, &"unit", &"ally", &"self", 0, 1, 1, 0, 0, 1, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_prismatic_barrier", "棱彩屏障", "给友军附加多属性抗性，适合保前排或防敌方法师爆发。", [&"mage", &"magic", &"support"], 3, &"unit", &"ally", &"single", 0, 2, 2, 0, 0, 2, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_blink", "闪现术", "立即位移最多 `3` 格；不穿重障碍，但可快速脱离近战。", [&"mage", &"magic", &"support"], 0, &"unit", &"ally", &"self", 0, 1, 1, 0, 0, 1, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_dimension_swap", "移形换位", "与友军或敌方互换站位；把目标换进 `mud`、火墙或包夹点的收益很高。", [&"mage", &"magic", &"support"], 4, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 2, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_time_dilate", "缓时术", "让目标时间流速迟缓；移动力 `-1`、命中 `-10%`，持续 `2` 回合。", [&"mage", &"magic", &"support"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 2, 5, [_build_damage_effect(6, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_haste_stream", "急速流", "赋予友军短时加速；非常适合战士冲锋和弓手转线。", [&"mage", &"magic", &"support"], 3, &"unit", &"ally", &"single", 0, 2, 2, 0, 0, 2, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_spellward", "法术结界", "在地面建立小型结界；范围内友军承受的法术伤害降低。", [&"mage", &"magic", &"support"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 3, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_mana_font", "法泉术", "生成持续 `2` 回合的法泉；友军在其中结束行动时回复少量 `mp`。", [&"mage", &"magic", &"support"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 3, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_dispel_wave", "驱散波", "同时清理敌方增益与友方减益，是中后期泛用解场技。", [&"mage", &"magic", &"support"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(5, &"magic_attack", &"magic_defense")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_levitate", "浮空术", "使目标短时无视 `mud`、地刺和轻度高差；非常适合辅助重装前排。", [&"mage", &"magic", &"support"], 3, &"unit", &"ally", &"single", 0, 1, 1, 0, 0, 2, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_portal_step", "门径跨越", "建议做成双点 `cast_variant`；在两格之间建立短时门户，供一名友军穿越。", [&"mage", &"magic", &"support"], 4, &"ground", &"enemy", &"single", 0, 2, 2, 0, 0, 3, 0, [], [28, 46, 72], 3, &"coord_pair", 2, 2, &"stable", &"book"))
	register_skill.call(_build_mage_temporal_rewind_skill())
	register_skill.call(_build_active_skill(&"mage_force_wall", "力场墙", "生成持续 `2` 回合的不可穿越力场；专门用来切割阵型。", [&"mage", &"magic", &"support"], 4, &"ground", &"enemy", &"line", 1, 2, 2, 0, 0, 3, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_arcane_aegis", "奥术圣域", "范围内友军获得 `法盾`、净化一层负面效果，并提高法术命中。", [&"mage", &"magic", &"support"], 4, &"ground", &"enemy", &"radius", 1, 3, 3, 0, 0, 4, 0, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_time_stop", "瞬时停界", "法师获得一次极短额外操作窗口；建议只允许移动、布阵或释放低阶技能，避免破坏节奏上限。", [&"mage", &"magic", &"support"], 0, &"unit", &"ally", &"self", 0, 3, 3, 0, 1, 5, 0, [], [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_shadow_bolt", "暗蚀飞弹", "死灵系基础点杀术；命中后施加 `奥脆`。", [&"mage", &"magic", &"necromancy"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 0, 5, [_build_damage_effect(10, &"magic_attack", &"magic_defense", &"negative_energy_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_life_drain", "生命虹吸", "造成伤害并按比例回复自身生命，是法师少数能自我续航的术式。", [&"mage", &"magic", &"necromancy"], 4, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 1, 0, [_build_damage_effect(10, &"magic_attack", &"magic_defense", &"negative_energy_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_bone_chill", "骨寒术", "目标获得 `寒蚀` 与治疗效率下降效果，对奶量体系很克制。", [&"mage", &"magic", &"necromancy"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 1, 10, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"negative_energy_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_hex_of_frailty", "衰弱诅咒", "降低目标攻击与防御，持续 `2` 回合；是小队集火的泛用前置。", [&"mage", &"magic", &"necromancy"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 1, 5, [_build_damage_effect(5, &"magic_attack", &"magic_defense", &"negative_energy_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_death_mark", "死兆印记", "纯功能收割印记；目标生命越低，承受的法术伤害越高。", [&"mage", &"magic", &"necromancy"], 5, &"unit", &"enemy", &"single", 0, 1, 1, 0, 0, 2, 10, [], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_soul_chain", "魂锁术", "连接直线上的至多 `2` 名敌人；其中一人受伤时另一人承受部分牵连伤害。", [&"mage", &"magic", &"necromancy"], 5, &"ground", &"enemy", &"line", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"negative_energy_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_grave_mist", "墓雾", "范围内敌人攻击下降、命中下降，并更容易被幻术控制。", [&"mage", &"magic", &"necromancy"], 4, &"ground", &"enemy", &"radius", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"negative_energy_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_black_flame", "冥火术", "施加不可被普通净化立刻清除的暗焰 `灼烧`；对高回复敌人特别克制。", [&"mage", &"magic", &"necromancy"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 2, 0, [_build_damage_effect(12, &"magic_attack", &"magic_defense", &"negative_energy_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_wither_field", "枯朽领域", "持续 `2` 回合削弱区域内敌人的生命回复与防御。", [&"mage", &"magic", &"necromancy"], 4, &"ground", &"enemy", &"radius", 2, 3, 2, 0, 0, 3, 0, [_build_damage_effect(7, &"magic_attack", &"magic_defense", &"negative_energy_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_mage_death_reap_skill())
	register_skill.call(_build_mage_spell_disjunction_skill())
	register_skill.call(_build_active_skill(&"mage_void_spike", "虚蚀尖刺", "奥术与虚无混合的穿刺线伤；对法盾和召唤物有额外克制。", [&"mage", &"magic", &"necromancy"], 5, &"ground", &"enemy", &"line", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(13, &"magic_attack", &"magic_defense", &"negative_energy_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_banshee_wail", "女妖哀嚎", "近距离群体压制；命中的敌人获得 `staggered`，并优先后退。", [&"mage", &"magic", &"necromancy"], 0, &"ground", &"enemy", &"cone", 1, 2, 2, 0, 0, 2, 0, [_build_damage_effect(10, &"magic_attack", &"magic_defense", &"negative_energy_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_soul_cage", "灵魂囚笼", "禁止目标在 `2` 回合内获得治疗或复活类收益；对精英和首领战很有价值。", [&"mage", &"magic", &"necromancy"], 5, &"unit", &"enemy", &"single", 0, 2, 2, 0, 0, 3, 0, [_build_damage_effect(8, &"magic_attack", &"magic_defense", &"negative_energy_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_active_skill(&"mage_apocalypse_word", "终焉言灵", "对低生命目标拥有显著斩杀提升；适合作为死灵系大招收尾。", [&"mage", &"magic", &"necromancy"], 5, &"ground", &"enemy", &"radius", 2, 3, 3, 0, 1, 5, -10, [_build_damage_effect(18, &"magic_attack", &"magic_defense", &"negative_energy_resistance")], [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
	register_skill.call(_build_death_reap_skill(&"death_reap"))
	register_skill.call(_build_spell_disjunction_skill(&"spell_disjunction"))

func _build_active_skill(
	skill_id: StringName,
	display_name: String,
	description: String,
	tags: Array[StringName],
	range_value: int,
	target_mode: StringName,
	target_team_filter: StringName,
	area_pattern: StringName,
	area_value: int,
	ap_cost: int,
	mp_cost: int,
	stamina_cost: int,
	aura_cost: int,
	cooldown_tu: int,
	hit_rate: int,
	effect_defs: Array,
	mastery_curve_values: Array[int],
	max_level: int,
	target_selection_mode: StringName = &"single_unit",
	min_target_count: int = 1,
	max_target_count: int = 1,
	selection_order_mode: StringName = &"stable",
	learn_source: StringName = &"book"
) -> SkillDef:
	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = target_mode
	combat_profile.target_team_filter = target_team_filter
	combat_profile.range_pattern = &"single"
	combat_profile.range_value = maxi(range_value, 0)
	combat_profile.area_pattern = area_pattern
	combat_profile.area_value = maxi(area_value, 0)
	combat_profile.ap_cost = maxi(ap_cost, 0)
	combat_profile.mp_cost = maxi(mp_cost, 0)
	combat_profile.stamina_cost = maxi(stamina_cost, 0)
	combat_profile.aura_cost = maxi(aura_cost, 0)
	combat_profile.cooldown_tu = maxi(cooldown_tu, 0)
	combat_profile.hit_rate = hit_rate
	combat_profile.target_selection_mode = target_selection_mode
	combat_profile.min_target_count = maxi(min_target_count, 1)
	combat_profile.max_target_count = maxi(max_target_count, combat_profile.min_target_count)
	combat_profile.selection_order_mode = selection_order_mode
	combat_profile.effect_defs.clear()
	for effect_def in _filter_effect_defs(effect_defs):
		combat_profile.effect_defs.append(effect_def)
	return _build_skill(skill_id, display_name, description, &"active", max_level, mastery_curve_values, tags, learn_source, [], [], [], combat_profile)


func _build_passive_skill(
	skill_id: StringName,
	display_name: String,
	description: String,
	tags: Array[StringName],
	attribute_modifiers: Array = [],
	learn_source: StringName = &"book"
) -> SkillDef:
	var modifiers: Array[AttributeModifier] = []
	for modifier_variant in attribute_modifiers:
		var modifier := modifier_variant as AttributeModifier
		if modifier != null:
			modifiers.append(modifier)
	return _build_skill(skill_id, display_name, description, &"passive", 1, [], tags, learn_source, [], [], modifiers, null)


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
	attribute_modifiers: Array,
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
	skill_def.learn_source = learn_source
	skill_def.combat_profile = combat_profile
	skill_def.tags.clear()
	for tag in tags:
		skill_def.tags.append(tag)
	skill_def.learn_requirements.clear()
	for skill_id_variant in learn_requirements:
		skill_def.learn_requirements.append(skill_id_variant)
	skill_def.mastery_sources.clear()
	for mastery_source in mastery_sources:
		skill_def.mastery_sources.append(mastery_source)
	skill_def.attribute_modifiers.clear()
	for modifier_variant in attribute_modifiers:
		var modifier := modifier_variant as AttributeModifier
		if modifier != null:
			skill_def.attribute_modifiers.append(modifier)
	return skill_def


func _build_mastery_curve(values: Array) -> PackedInt32Array:
	var curve := PackedInt32Array()
	for value in values:
		curve.append(int(value))
	return curve


func _build_damage_effect(
	power: int,
	scaling_attribute_id: StringName = &"physical_attack",
	defense_attribute_id: StringName = &"physical_defense",
	resistance_attribute_id: StringName = &"",
	target_team_filter: StringName = &""
) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"damage"
	effect_def.power = maxi(power, 0)
	effect_def.scaling_attribute_id = scaling_attribute_id
	effect_def.defense_attribute_id = defense_attribute_id
	if resistance_attribute_id != &"":
		effect_def.resistance_attribute_id = resistance_attribute_id
	if target_team_filter != &"":
		effect_def.effect_target_team_filter = target_team_filter
	return effect_def


func _build_heal_effect(power: int, target_team_filter: StringName = &"") -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"heal"
	effect_def.power = maxi(power, 0)
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
	effect_def.power = maxi(power, 1)
	if duration > 0:
		effect_def.params = {"duration": duration}
	if target_team_filter != &"":
		effect_def.effect_target_team_filter = target_team_filter
	return effect_def


func _build_special_effect(effect_type: StringName, params: Dictionary = {}, power: int = 0) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = effect_type
	effect_def.power = power
	effect_def.params = params.duplicate(true)
	return effect_def


func _build_terrain_replace_effect(terrain: StringName) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"terrain_replace"
	effect_def.terrain_replace_to = terrain
	return effect_def


func _build_height_delta_effect(delta: int) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"height_delta"
	effect_def.height_delta = delta
	return effect_def


func _build_modifier(attribute_id: StringName, value: int) -> AttributeModifier:
	var modifier := AttributeModifier.new()
	modifier.attribute_id = attribute_id
	modifier.mode = AttributeModifier.MODE_FLAT
	modifier.value = value
	return modifier


func _filter_effect_defs(effect_defs: Array) -> Array[CombatEffectDef]:
	var results: Array[CombatEffectDef] = []
	for effect_variant in effect_defs:
		var effect_def := effect_variant as CombatEffectDef
		if effect_def != null:
			results.append(effect_def)
	return results


func _build_cast_variant(
	variant_id: StringName,
	display_name: String,
	description: String,
	min_skill_level: int,
	footprint_pattern: StringName,
	required_coord_count: int,
	allowed_base_terrains: Array[StringName],
	effect_defs: Array
) -> CombatCastVariantDef:
	var cast_variant := CombatCastVariantDef.new()
	cast_variant.variant_id = variant_id
	cast_variant.display_name = display_name
	cast_variant.description = description
	cast_variant.min_skill_level = min_skill_level
	cast_variant.target_mode = &"ground"
	cast_variant.footprint_pattern = footprint_pattern
	cast_variant.required_coord_count = required_coord_count
	cast_variant.allowed_base_terrains.clear()
	for terrain_id in allowed_base_terrains:
		cast_variant.allowed_base_terrains.append(terrain_id)
	cast_variant.effect_defs.clear()
	for effect_def in _filter_effect_defs(effect_defs):
		cast_variant.effect_defs.append(effect_def)
	return cast_variant


func _build_ground_variant_skill(
	skill_id: StringName,
	display_name: String,
	description: String,
	tags: Array[StringName],
	range_value: int,
	ap_cost: int,
	mp_cost: int,
	stamina_cost: int,
	aura_cost: int,
	cooldown_tu: int,
	hit_rate: int,
	cast_variants: Array,
	learn_source: StringName = &"book"
) -> SkillDef:
	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"ground"
	combat_profile.target_team_filter = &"enemy"
	combat_profile.range_pattern = &"single"
	combat_profile.range_value = maxi(range_value, 0)
	combat_profile.area_pattern = &"single"
	combat_profile.area_value = 0
	combat_profile.ap_cost = maxi(ap_cost, 0)
	combat_profile.mp_cost = maxi(mp_cost, 0)
	combat_profile.stamina_cost = maxi(stamina_cost, 0)
	combat_profile.aura_cost = maxi(aura_cost, 0)
	combat_profile.cooldown_tu = maxi(cooldown_tu, 0)
	combat_profile.hit_rate = hit_rate
	combat_profile.cast_variants.clear()
	for cast_variant_variant in cast_variants:
		var cast_variant := cast_variant_variant as CombatCastVariantDef
		if cast_variant != null:
			combat_profile.cast_variants.append(cast_variant)
	return _build_skill(skill_id, display_name, description, &"active", 3, [28, 46, 72], tags, learn_source, [], [], [], combat_profile)


func _build_archer_multishot_skill() -> SkillDef:
	var cast_variant := _build_cast_variant(
		&"multishot_volley",
		"连珠箭",
		"依次锁定 2 到 3 个不同敌方目标格，并按选择顺序逐个结算射击。",
		0,
		&"unordered",
		3,
		[],
		[_build_damage_effect(8)]
	)
	var skill_def := _build_ground_variant_skill(
		&"archer_multishot",
		"连珠箭",
		"一次选择多个敌方单位依次射击。当前仍沿用兼容期的地面多点协议表达多目标锁定。",
		[&"archer", &"ranged", &"bow", &"aoe"],
		5,
		2,
		0,
		2,
		0,
		2,
		-5,
		[cast_variant]
	)
	if skill_def.combat_profile != null:
		skill_def.combat_profile.target_selection_mode = &"multi_unit"
		skill_def.combat_profile.min_target_count = 2
		skill_def.combat_profile.max_target_count = 3
		skill_def.combat_profile.selection_order_mode = &"manual"
		skill_def.combat_profile.ai_tags = [&"multi_target", &"focus_fire"]
	return skill_def


func _build_mage_chain_lightning_skill() -> SkillDef:
	var skill_def := _build_active_skill(
		&"mage_chain_lightning",
		"链式闪击",
		"按文档改为单体锁定的链式闪电定义，并保留可被当前运行时识别的首段伤害与感电效果。",
		[&"mage", &"magic", &"lightning"],
		5,
		&"unit",
		&"enemy",
		&"single",
		0,
		2,
		2,
		0,
		0,
		2,
		0,
		[
			_build_damage_effect(12, &"magic_attack", &"magic_defense", &"lightning_resistance"),
			_build_status_effect(&"shocked", 1, 1),
			_build_special_effect(&"chain_damage", {
				"chain_shape": "square",
				"base_chain_radius": 1,
				"wet_chain_radius": 2,
				"bonus_terrain_effect_id": "wet",
				"prevent_repeat_target": true,
			}),
		],
		[28, 46, 72],
		3
	)
	if skill_def.combat_profile != null:
		skill_def.combat_profile.ai_tags = [&"chain", &"aoe"]
	return skill_def


func _build_mage_fireball_skill() -> SkillDef:
	return _build_active_skill(
		&"mage_fireball",
		"火球术",
		"保留现有基础定位的稳定范围法术；兼容期内继续用可运行的菱形小范围表达。",
		[&"mage", &"magic", &"fire"],
		4,
		&"ground",
		&"enemy",
		&"diamond",
		1,
		2,
		2,
		0,
		0,
		2,
		0,
		[_build_damage_effect(13, &"magic_attack", &"magic_defense", &"fire_resistance")],
		[28, 46, 72],
		3
	)


func _build_archer_armor_piercer_skill() -> SkillDef:
	return _build_active_skill(
		&"archer_armor_piercer",
		"破甲箭",
		"命中后附加破甲，作为后续集火开口。",
		[&"archer", &"ranged", &"bow", &"output"],
		5,
		&"unit",
		&"enemy",
		&"single",
		0,
		1,
		0,
		1,
		0,
		2,
		0,
		[
			_build_damage_effect(11, &"physical_attack", &"physical_defense"),
			_build_status_effect(&"armor_break", 2, 1),
		],
		[28, 46, 72],
		3
	)


func _build_archer_pinning_shot_skill() -> SkillDef:
	return _build_active_skill(
		&"archer_pinning_shot",
		"钉射",
		"单体伤害并施加 pinned。",
		[&"archer", &"ranged", &"bow", &"control"],
		4,
		&"unit",
		&"enemy",
		&"single",
		0,
		1,
		0,
		1,
		0,
		2,
		0,
		[
			_build_damage_effect(10, &"physical_attack", &"physical_defense"),
			_build_status_effect(&"pinned", 1, 1),
		],
		[28, 46, 72],
		3
	)


func _build_mage_ice_lance_skill() -> SkillDef:
	return _build_active_skill(
		&"mage_ice_lance",
		"冰枪术",
		"远距离点杀并施加 frozen。",
		[&"mage", &"magic", &"ice"],
		5,
		&"unit",
		&"enemy",
		&"single",
		0,
		1,
		1,
		0,
		0,
		1,
		0,
		[
			_build_damage_effect(11, &"magic_attack", &"magic_defense", &"freeze_resistance"),
			_build_status_effect(&"frozen", 1, 1),
		],
		[28, 46, 72],
		3
	)


func _build_mage_temporal_rewind_skill() -> SkillDef:
	return _build_active_skill(
		&"mage_temporal_rewind",
		"时序回溯",
		"回退目标到上次受击前的状态片段。兼容期内先落成友军单体回复技能。",
		[&"mage", &"magic", &"support"],
		3,
		&"unit",
		&"ally",
		&"single",
		0,
		2,
		3,
		0,
		0,
		4,
		0,
		[_build_heal_effect(12, &"ally")],
		[28, 46, 72],
		3
	)


func _build_mage_fossil_to_mud_alias() -> SkillDef:
	var cast_variants := [
		_build_cast_variant(&"mud_single", "单格泥沼", "将单格平地或地刺改为泥沼。", 0, &"single", 1, [&"land", &"spike"], [_build_terrain_replace_effect(&"mud")]),
		_build_cast_variant(&"lower_single_1", "单格降一层", "将单格平地或地刺降低 1 层。", 1, &"single", 1, [&"land", &"spike"], [_build_height_delta_effect(-1)]),
		_build_cast_variant(&"lower_line2_1", "双格各降一层", "将两个正交连续地格各降低 1 层。", 3, &"line2", 2, [&"land", &"spike"], [_build_height_delta_effect(-1)]),
		_build_cast_variant(&"mud_square2", "二乘二泥沼", "将 2x2 地格整体改为泥沼。", 5, &"square2", 4, [&"land", &"spike"], [_build_terrain_replace_effect(&"mud")]),
	]
	var skill_def := _build_ground_variant_skill(
		&"mage_fossil_to_mud",
		"化石为泥",
		"法师地形改造技能的文档前缀别名，当前继续沿用既有 ground + cast_variants 兼容表达。",
		[&"mage", &"magic", &"earth", &"terrain"],
		3,
		1,
		2,
		0,
		0,
		1,
		0,
		cast_variants
	)
	skill_def.max_level = 5
	skill_def.mastery_curve = _build_mastery_curve([20, 30, 45, 60, 80])
	return skill_def


func _build_death_reap_skill(skill_id: StringName) -> SkillDef:
	var skill_def := _build_active_skill(
		skill_id,
		"死亡收割",
		"高耗魔单体收割技，命中后可记录击杀返 AP 与免费移动额度的定义语义。",
		[&"mage", &"magic", &"necromancy", &"finisher"],
		5,
		&"unit",
		&"enemy",
		&"single",
		0,
		2,
		3,
		0,
		0,
		3,
		0,
		[
			_build_damage_effect(14, &"magic_attack", &"magic_defense", &"negative_energy_resistance"),
			_build_special_effect(&"on_kill_gain_resources", {
				"ap_gain": 1,
				"free_move_points_gain": 2,
				"grant_scope": "current_turn",
				"stack_on_multiple_kills": true,
				"require_target_defeated_by_same_skill": true,
			}),
		],
		[28, 46, 72],
		3
	)
	return skill_def


func _build_mage_death_reap_skill() -> SkillDef:
	return _build_death_reap_skill(&"mage_death_reap")


func _build_spell_disjunction_skill(skill_id: StringName) -> SkillDef:
	var skill_def := _build_active_skill(
		skill_id,
		"裂解术",
		"单体法术伤害附带装备破坏定义语义；当前运行时仅稳定执行首段法术伤害。",
		[&"mage", &"magic", &"arcane", &"breaker"],
		5,
		&"unit",
		&"enemy",
		&"single",
		0,
		2,
		3,
		0,
		0,
		3,
		0,
		[
			_build_damage_effect(11, &"magic_attack", &"magic_defense"),
			_build_special_effect(&"break_equipment_on_hit", {
				"base_break_chance": 35,
				"max_broken_items": 1,
				"slot_weight_map": {
					"main_hand": 30,
					"off_hand": 20,
					"head": 20,
					"body": 10,
					"accessory_1": 10,
					"accessory_2": 10,
				},
				"slot_break_chance_map": {
					"main_hand": 1.0,
					"off_hand": 0.9,
					"head": 0.85,
					"body": 0.6,
					"accessory_1": 0.7,
					"accessory_2": 0.7,
				},
				"require_damage_applied": true,
			}),
		],
		[28, 46, 72],
		3
	)
	return skill_def


func _build_mage_spell_disjunction_skill() -> SkillDef:
	return _build_spell_disjunction_skill(&"mage_spell_disjunction")


func _build_warrior_heavy_strike_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_heavy_strike",
		"重击",
		"150% 伤害并附带轻度破甲。",
		[&"warrior", &"melee", &"output"],
		1,
		&"unit",
		&"enemy",
		&"single",
		0,
		2,
		0,
		1,
		0,
		1,
		0,
		[
			_build_damage_effect(15),
			_build_status_effect(&"armor_break", 1, 1),
		],
		[24, 40, 62],
		3
	)


func _build_warrior_sweeping_slash_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_sweeping_slash",
		"横扫",
		"前方范围横扫。",
		[&"warrior", &"melee", &"aoe"],
		1,
		&"ground",
		&"enemy",
		&"cone",
		1,
		2,
		0,
		1,
		0,
		1,
		0,
		[_build_damage_effect(12)],
		[24, 40, 62],
		3
	)


func _build_warrior_piercing_thrust_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_piercing_thrust",
		"穿刺",
		"直线穿透的突刺技能。",
		[&"warrior", &"melee", &"thrust"],
		2,
		&"ground",
		&"enemy",
		&"line",
		2,
		2,
		0,
		1,
		0,
		1,
		0,
		[_build_damage_effect(12)],
		[24, 40, 62],
		3
	)


func _build_warrior_guard_break_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_guard_break",
		"裂甲斩",
		"用于撕开防御姿态的定点技能。",
		[&"warrior", &"melee", &"breaker"],
		1,
		&"unit",
		&"enemy",
		&"single",
		0,
		2,
		0,
		1,
		0,
		2,
		0,
		[
			_build_damage_effect(13),
			_build_status_effect(&"armor_break", 2, 1),
		],
		[28, 46, 72],
		3
	)


func _build_warrior_execution_cleave_skill() -> SkillDef:
	var effect := _build_damage_effect(16)
	effect.bonus_condition = &"target_low_hp"
	effect.damage_ratio_percent = 180
	return _build_active_skill(
		&"warrior_execution_cleave",
		"断头斩",
		"对低血目标有更高收益的斩杀技。",
		[&"warrior", &"melee", &"finisher"],
		1,
		&"unit",
		&"enemy",
		&"single",
		0,
		2,
		0,
		2,
		0,
		2,
		0,
		[effect],
		[28, 46, 72],
		3
	)


func _build_warrior_jump_slash_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_jump_slash",
		"跳斩",
		"位移后落地造成小范围伤害。",
		[&"warrior", &"melee", &"mobility"],
		3,
		&"ground",
		&"enemy",
		&"radius",
		1,
		2,
		0,
		2,
		0,
		2,
		0,
		[
			_build_damage_effect(12),
			_build_special_effect(&"forced_move", {"mode": "jump"}),
		],
		[28, 46, 72],
		3
	)


func _build_warrior_backstep_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_backstep",
		"后撤步",
		"后退并提升闪避。",
		[&"warrior", &"melee", &"mobility"],
		0,
		&"unit",
		&"ally",
		&"self",
		0,
		1,
		0,
		1,
		0,
		1,
		0,
		[_build_status_effect(&"evasion_up", 1, 1)],
		[24, 40, 62],
		3
	)


func _build_warrior_guard_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_guard",
		"格挡",
		"显著降低承受伤害。",
		[&"warrior", &"melee", &"defense"],
		0,
		&"unit",
		&"ally",
		&"self",
		0,
		1,
		0,
		1,
		0,
		2,
		0,
		[
			_build_status_effect(&"guarding", 1, 1),
			_build_status_effect(&"damage_reduction_up", 1, 1),
		],
		[24, 40, 62],
		3
	)


func _build_warrior_shield_wall_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_shield_wall",
		"护盾墙",
		"为自身周围友军提供群体减伤。",
		[&"warrior", &"melee", &"defense"],
		0,
		&"ground",
		&"ally",
		&"radius",
		1,
		2,
		0,
		2,
		0,
		2,
		0,
		[_build_status_effect(&"damage_reduction_up", 1, 1, &"ally")],
		[28, 46, 72],
		3
	)


func _build_warrior_battle_recovery_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_battle_recovery",
		"战斗回复",
		"稳定恢复自身生命。",
		[&"warrior", &"melee", &"recovery"],
		0,
		&"unit",
		&"ally",
		&"self",
		0,
		1,
		0,
		1,
		0,
		2,
		0,
		[_build_heal_effect(10, &"ally")],
		[24, 40, 62],
		3
	)


func _build_warrior_shield_bash_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_shield_bash",
		"盾击",
		"近身控制技。",
		[&"warrior", &"melee", &"control", &"shield"],
		1,
		&"unit",
		&"enemy",
		&"single",
		0,
		1,
		0,
		1,
		0,
		2,
		0,
		[
			_build_damage_effect(11),
			_build_status_effect(&"staggered", 1, 1),
		],
		[24, 40, 62],
		3
	)


func _build_warrior_taunt_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_taunt",
		"挑衅",
		"迫使敌人优先关注自己。",
		[&"warrior", &"melee", &"control"],
		3,
		&"unit",
		&"enemy",
		&"single",
		0,
		1,
		0,
		1,
		0,
		2,
		0,
		[_build_status_effect(&"taunted", 1, 1)],
		[24, 40, 62],
		3
	)


func _build_warrior_war_cry_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_war_cry",
		"战吼",
		"为近身盟友提供攻击增益。",
		[&"warrior", &"melee", &"support"],
		0,
		&"ground",
		&"ally",
		&"radius",
		1,
		2,
		0,
		1,
		0,
		2,
		0,
		[_build_status_effect(&"attack_up", 1, 1, &"ally")],
		[24, 40, 62],
		3
	)


func _build_warrior_true_dragon_slash_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_true_dragon_slash",
		"真龙斩",
		"高代价直线终极范围技能。",
		[&"warrior", &"melee", &"aoe", &"finisher"],
		4,
		&"ground",
		&"enemy",
		&"line",
		4,
		3,
		0,
		2,
		1,
		3,
		0,
		[_build_damage_effect(18)],
		[30, 50, 78],
		3
	)


func _build_warrior_combo_strike_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_combo_strike",
		"连击",
		"圣剑连斩的来源技能之一。",
		[&"warrior", &"melee", &"combo"],
		1,
		&"unit",
		&"enemy",
		&"single",
		0,
		1,
		0,
		1,
		0,
		1,
		0,
		[
			_build_damage_effect(10),
			_build_status_effect(&"staggered", 1, 1),
		],
		[24, 40, 62],
		5
	)


func _build_warrior_aura_slash_skill() -> SkillDef:
	return _build_active_skill(
		&"warrior_aura_slash",
		"斗气斩",
		"圣剑连斩的来源技能之一。",
		[&"warrior", &"melee", &"aura"],
		3,
		&"unit",
		&"enemy",
		&"single",
		0,
		2,
		0,
		0,
		1,
		2,
		0,
		[_build_damage_effect(12)],
		[24, 40, 62],
		5
	)


func _build_warrior_whirlwind_slash_skill() -> SkillDef:
	var charge_effect := _build_special_effect(&"charge", {
		"skill_id": "warrior_whirlwind_slash",
		"base_distance": 3,
		"distance_by_level": {"1": 4, "3": 5, "5": 6},
		"collision_base_damage": 10,
		"collision_size_gap_damage": 10,
	})
	var path_step_aoe := _build_special_effect(&"path_step_aoe", {
		"step_shape": "diamond",
		"step_radius": 1,
		"allow_repeat_hits_across_steps": true,
		"apply_on_successful_step_only": true,
	}, 10)
	var cast_variant := _build_cast_variant(&"whirlwind_charge", "旋风斩", "兼容期内仍沿用冲锋选点，但额外挂出路径 AOE 的定义语义。", 0, &"single", 1, [], [charge_effect, path_step_aoe])
	var skill_def := _build_ground_variant_skill(&"warrior_whirlwind_slash", "旋风斩", "冲锋与旋斩合并后的升级技能。", [&"warrior", &"melee", &"aoe", &"mobility"], 6, 2, 0, 2, 1, 3, 0, [cast_variant])
	skill_def.learn_requirements = [&"charge", &"warrior_combo_strike"]
	return skill_def


func _build_saint_blade_combo_skill() -> SkillDef:
	var skill_def := _build_active_skill(&"saint_blade_combo", "圣剑连斩", "连击与斗气斩的复合升级技能。当前仅在定义层记录循环追击与非破坏式合并语义。", [&"warrior", &"melee", &"combo", &"aura"], 1, &"unit", &"enemy", &"single", 0, 2, 0, 0, 1, 3, 0, [
		_build_damage_effect(12),
		_build_special_effect(&"repeat_attack_until_fail", {
			"same_target_only": true,
			"base_hit_rate": 0,
			"follow_up_hit_rate_penalty": 10,
			"follow_up_damage_multiplier": 2,
			"follow_up_cost_multiplier": 2,
			"cost_resource": "aura",
			"stop_on_miss": true,
			"stop_on_insufficient_resource": true,
			"stop_on_target_down": true,
			"consume_cost_on_attempt": true,
			"damage_multiplier_stage": "pre_resistance",
		}),
	], [30, 50, 78], 3)
	skill_def.unlock_mode = &"composite_upgrade"
	skill_def.knowledge_requirements = [&"compania_family_legacy"]
	skill_def.skill_level_requirements = {
		"warrior_combo_strike": 5,
		"warrior_aura_slash": 5,
	}
	skill_def.achievement_requirements = [&"six_hit_combo"]
	skill_def.upgrade_source_skill_ids = [&"warrior_combo_strike", &"warrior_aura_slash"]
	skill_def.retain_source_skills_on_unlock = true
	skill_def.core_skill_transition_mode = &"replace_sources_with_result"
	return skill_def
