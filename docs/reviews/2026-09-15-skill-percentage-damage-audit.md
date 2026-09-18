# 技能百分比增伤专项清点

日期：2026-09-15。范围：当前工作树 `data/configs/json/skills/` 的29个正式JSON、706个唯一技能ID及文件内模板；同时核对正式伤害倍率消费者。按技能ID计数，同技能不同等级、多个效果不重复计数。

用户重申不采用这类百分比增伤设计；霜爆回响的新50%方案已撤回。此次仅清点并修正提案状态，没有修改生产技能或运行时代码。

## 1. 明确写有50%加伤的文案：9个

口径包括直接提高50%、改为150%以及追加一次50%伤害；不包括反伤、伤害转移、减伤、减疗、吸血、生命阈值和概率。

| 技能 | ID | 文案中的50% | 当前配置与代码核对 |
|---|---|---|---|
| 霜爆回响 | `mage_cold_snap` | 寒蚀目标追加50%碎裂伤害 | 冰伤加高等级slow，没有寒蚀条件或额外50%伤害 |
| 熔爆术 | `mage_molten_burst` | 灼烧目标追加50%火焰余震 | 火伤加高等级burning，没有目标已有灼烧的加伤条件 |
| 追心箭 | `archer_heartseeker` | 标记目标伤害改为150% | 普通穿刺伤害；标记仅走全局通用1.1倍规则，没有该技能专属1.5倍 |
| 断崖重击 | `warrior_cliff_heavy_strike` | 目标身后有障碍/边缘时额外50% | 武器伤害加额外骰，没有位置条件倍率 |
| 斗气强化攻击 | `warrior_qi_empower_strike` | 下一击伤害+50% | 没有effect_defs，也没有特殊结算profile；模板只提供等级上限 |
| 血纹标记 | `warrior_blood_mark` | 目标受到流血伤害+50% | 没有effect_defs，也没有特殊结算profile；模板只提供等级上限 |
| 虎神附体 | `warrior_tiger_god_possession` | 流血伤害+50% | 实际施加power=2的attack_up，提供通用20%伤害倍率；没有流血专属50% |
| 奥术回响 | `mage_arcane_echo` | 下一单体法术以50%威力再触发一次 | 实际施加attack_roll_bonus_up，没有法术重放 |
| 狩猎网阵 | `archer_hunting_grid` | 离开区域时再受50%伤害 | 仅一次范围穿刺伤害，没有区域离开触发 |

前7个是直接加伤或条件追加，后2个是50%重放/区域追伤。这9项文案承诺的50%机制均未在当前配置与对应代码中实现；这不表示技能本身没有其他效果，也不表示全系统没有百分比增伤。对这9个ID的生产C#搜索没有命中技能专属实现。

## 2. 通用百分比增伤配置来源：64个

此处统计能提供相应倍率效果的技能，不统计所有可能受益的攻击技能。各组没有技能ID交叉。

| 当前入口 | 唯一技能数 | 正式伤害规则 |
|---|---:|---|
| `marked`状态 | 50 | 对标记目标的伤害系数乘1.10；与状态power无关 |
| `attack_up`状态 | 9 | 伤害系数乘`1 + 0.10 × max(power, 1)`；当前配置为10%/20%/30% |
| `archer_pre_aim`状态 | 2 | 持有者伤害系数乘1.15 |
| `damage_ratio_percent > 100` | 2 | 星刃终裁：条件满足时伤害段乘3.20；意志穿透：目标有护盾时其1D8力场段乘1.40，不是整个技能伤害+40% |
| `target_damage_multiplier_rules` | 1 | 装备技能星坠：建筑、非魔法且非建筑的构造物乘2.00 |
| 合计 | **64** | 正式通用增伤配置来源 |

9个attack_up来源：`warrior_berserk`、`warrior_frenzy_mode`、`warrior_scale_crack`、`warrior_scale_crack_open`、`warrior_tactical_command`、`warrior_tiger_god_possession`、`warrior_vanguard_pardon`、`warrior_vanguard_pardon_skill`、`warrior_war_cry_v2`。同名技能按不同ID分别计数。

2个archer_pre_aim来源：`archer_highground_claim`、`archer_skirmish_step`。50个marked来源及每个效果的配置指针、等级窗口见证据JSON。

64不是所有可能产生倍率的机制总数：本口径没有把武器骰倍数、连射段倍率、暴击、百分比反伤、装备能力和特殊profile在运行时注入的倍率混入。当前技能JSON未显式配置`damage_ratio_percent=150`；不能仅据此推断游戏中绝无1.5倍伤害。

## 3. 证据与验证界限

- [逐技能证据与源文件SHA256](evidence/2026-09-15-skill-percentage-damage-audit.json)：706个ID去重校验、9个文案候选、64个通用来源、每个效果的配置和文件路径。
- [正式伤害倍率组合](../../scripts/systems/battle/rules/BattleDamageResolver.SaveBranch.cs:121)：条件倍率、attack_up、archer_pre_aim、marked和目标种类倍率。
- [倍率字段换算](../../scripts/systems/battle/rules/BattleDamageResolver.DtoHelpers.cs:302)：`DamageRatioPercent / 100.0`。
- [条件判定](../../scripts/systems/battle/rules/BattleDamageBonusConditionRules.cs:16)：当前支持低血、减益数量、种类、硬控、护盾；没有指定状态条件。
- [状态强度与运行时倍率](../../scripts/systems/battle/rules/BattleDamageResolver.Effects.cs:737)：power及额外状态倍率的消费方式。

本次是静态正式配置清点和消费者代码核对，没有运行Godot场景、内容加载回归、全套回归、BattleSim或CI，不将统计结果描述为运行测试PASS。文案与效果的细节不一致仍需逐技能重新设计，不能直接按数量批量替换。
