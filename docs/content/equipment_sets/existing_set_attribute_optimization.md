# 既有传奇套装成员属性与旧效果迁移审计（单品配置冻结版）

> 状态：Content audit / planned；不代表 ItemDef、TraitDef、新套装阈值或战斗能力已经落地。
>
> 基线：2026-08-08 当前 checkout；当前实现 owner 以 C# runtime、typed Resource 投影和现有测试为准。
>
> 目标：沿用龙鳞套装的审计方法，记录全部既有单件/旧套装属性未来可迁移到的当前 owner；本轮只重设计套装奖励层，不回写单件配置。原特殊效果保留真实触发粒度，不用删效果来规避运行时缺口。

## 1. 配置冻结边界

本审计只处理成员属性与旧效果迁移，不重做任何单品配置。以下内容全部冻结：

- 原 100 个套装编号、中文名与英文名、排列顺序。
- 原 1000 个物品块及其 item_id、display_name、描述、装备槽位、equipment_type_id、armor tag、max_dex_bonus、价格和获取叙述。
- 原 set tag；不新增 theme_id、gear_set_id、family、tier 或获取阶段字段。
- 原单件特殊效果和 special_effect_id 的机制语义。当前 ABI 不支持的效果标记 runtime gap，不删除、不降级成无关静态属性。

旧阈值数量、顺序与奖励不在冻结范围内。旧 2/4/6 件效果是新设计的迁移输入：招牌机制必须有明确去向，但允许调整到新的中间门槛或 10 件封顶，并允许按当前系统重定数值、次数与 typed owner。

本文件审计的只有两类内容：

1. 原单件 attribute_modifiers 与旧阈值效果可迁移到的当前字段、typed owner、叠加规则与合法量纲。
2. 能由同档证据证明的单件属性数值异常只记录为风险；本轮不修改原值，也不按轻/中/重甲模板一刀切。

原文权威关系保持不变：

- 物品成员和单件叙述：本目录 23 份 sets_*.md。
- 新套装门槛与奖励：[existing_set_bonus_redesign.md](existing_set_bonus_redesign.md) 及其三份逐套文档。
- 旧阈值与旧阈值特殊效果迁移来源：set_bonus_design.md。
- 龙鳞套的精确触发合同：dragon_scale_set_full_landing.md。
- 套装 21–30 物品文档内的“设计预留”不形成第二套阈值；阈值仍只以 set_bonus_design.md 为准。

## 2. 审计结果

| 项目 | 当前文档事实 | 属性优化结论 |
|---|---:|---|
| 套装标题 | 100 | 全部保留 |
| 物品块 | 1000 | 全部保留原 ID、槽位与 set tag |
| 单件属性条目 | 1824 | 逐条由本文件的确定性规则覆盖 |
| 单件属性 ID | 58 种 | 540 条可直接进入当前静态属性管线；1284 条必须改名或迁移 owner |
| 直接静态字段 | 540 | armor_ac_bonus 416、attack_bonus 105、dodge_bonus 19 |
| 资源字段旧名 | 229 | max_hp 70、max_mana 159 |
| 技能/工具检定伪属性 | 718 | 保留精确检定语义，禁止粗暴折算成六维 |
| 抗性伪属性 | 96 | 从无 consumer 的数值改为 Trait 离散 mitigation tier |
| 豁免伪属性 | 65 | 迁到 Trait ability save；fear 单独走 save-tag owner |
| 条件战斗伪属性 | 150 | 移动、先攻、施法命中/DC、暴击范围等迁到 typed query/status |
| set tag 唯一值 | 99 | Set 11 与 Set 95 的 star_weaver_set 冲突原样保留，见第 8 节 |

attribute_id 非空并不等于属性有效。当前 AttributeService 会把未知 ID 放进 snapshot，但如果战斗、世界或 UI 没有 consumer，它只是一项无人读取的数值。本覆盖以“真实 consumer”而不是“能加载”为合格标准。

## 3. 龙鳞套提供的设计纪律

所有既有套装采用龙鳞方案已经明确的六条纪律：

1. 套装是 10 级以后的高阶装备，数值要与同获取档位同类装备比较，不能拿创建角色或低级敌人当平衡基线。
2. 单件、低阈值和高阈值是独立来源；语义相近也不静默删减。数值抗性按最强 tier 合并，不能让两份 half 叠成四分之一。
3. 附加伤害按每个真实主直接伤害 segment 查询。多段技能逐段触发，不按 cast 去重；extra_damage_segments、装备生成伤害、自伤、反射、地形 tick 不递归触发。
4. 条件命中、施法命中、豁免 DC、暴击阈值、移动力与抗性不伪装成无条件 AttributeModifier。
5. 内容加载、Resource 校验或 runner PASS 都不等于可玩套装；正式落地必须覆盖 membership、换装、preview、AI、UI、存档与 focused regression。
6. 缺少获取档位或同档目标曲线时不制造“精确平衡”结论；本审计记录 owner、非法量纲和可证明的离群值，但配置冻结期间不执行单件修正。

## 4. 当前系统 owner

| 属性机制 | 当前 owner | 本覆盖的使用方式 | 状态 |
|---|---|---|---|
| HP、MP、六维、全局攻击 | ItemDef.attribute_modifiers → PartyEquipmentService → AttributeService | 使用 hp_max、mp_max、strength/agility/constitution/perception/intelligence/willpower、attack_bonus | 已实现 |
| AC component | AttributeContentRules + AttributeService | 护甲用 armor_ac_bonus，闪避用 dodge_bonus，饰品护身用 deflection_bonus | 已实现 |
| ability save | ItemDef.trait_ids → TraitDef.save_bonus_entries → BattleTraitPassiveProjectionService | save(strength/agility/constitution/intelligence/willpower,+N) | 已实现 |
| 伤害抗性 | TraitDef.damage_resistance_entries → BattleUnitDamageResistanceState | res(tag,half/immune/double) | 已实现 |
| fear 等 save tag | BattleSaveResolver 的 status/save-tag 路径 | save_tag(frightened,+N) | 缺装备/套装 trait producer |
| 条件攻击与附伤 | typed equipment ability attack/damage query | 按 target tag、攻击种类、来源和真实伤害段查询 | 部分实现；通用 segment query 尚缺 |
| 移动力 | BattleStatusEffectState.move_point_capacity_delta | 常驻装备 passive status；不把尺数直接写入 AttributeSnapshot | owner 已有，装备常驻投影需补齐 |
| 施法命中 | typed attack-check query | spell_hit(+N)，只匹配施法来源 | 缺通用内容入口 |
| 施法豁免 DC | outgoing save-DC query | spell_save_dc(+N) | 尚未实现；不得改成 spell_proficiency_bonus |
| 暴击范围 | attack critical-threshold query | crit_threshold_delta=-1 | 尚未实现；不得替换成每战一次自动暴击 |
| 先攻 | battle-start action-progress producer | battle_start_progress(+N) | 尚缺 OnBattleStart owner |
| 技能/工具检定 | typed utility-check query | check(check_id,+N) | 尚未实现 |
| 套装阈值 | 正式 membership snapshot + derived threshold source | 累计阈值，来源可随换装/损坏刷新 | 尚未实现 |

## 5. 58 种单件属性的未来确定性迁移（本轮不回写）

> 本节是后续单件 typed projection 的审计结果，不属于本轮 set-bonus 设计授权。表中的“改名/改为/迁到”只说明若未来单独获准迁移时的唯一合法 owner；当前 `sets_*.md` 与资源配置保持零修改。

只要原物品块中的 item_id、槽位和数值不变，下表就能唯一确定 1824 条单件属性的优化结果；因此无需改写 1000 个配置块。

### 5.1 直接静态与旧名

| 原字段 | 数量 | 优化结果 |
|---|---:|---|
| armor_ac_bonus | 416 | 400 个 armor 槽继续使用原值；16 个 accessory 槽改用 deflection_bonus。除第 6 节列出的离群值外数值不变 |
| dodge_bonus | 19 | 保留原值和 ItemDef owner |
| attack_bonus | 105 | 保留全局攻击语义和原值；原特殊效果中的条件攻击不并入这里 |
| max_hp | 70 | 改名 hp_max，正负值均保留 |
| max_mana | 159 | 改名 mp_max，原值保留 |
| strength_bonus | 3 | 改为 strength |
| dexterity_bonus | 2 | 改为 agility |
| constitution_bonus | 8 | 改为 constitution |
| intelligence_bonus | 4 | 改为 intelligence |
| wisdom_bonus | 3 | 按当前六维明确映射为 willpower |
| charisma_bonus | 6 | 按当前六维明确映射为 willpower |

### 5.2 抗性与豁免

| 原字段 | 数量 | 优化结果 |
|---|---:|---|
| resistance_fire / cold / lightning / acid / poison | 53 | Trait res(fire/freeze/lightning/acid/poison,half) |
| resistance_necrotic / radiant / force / psychic / thunder | 41 | Trait res(negative_energy/radiant/force/psychic/thunder,half) |
| resistance_slashing / piercing | 2 | Trait res(physical_slash/physical_pierce,half) |
| saving_throw_strength | 10 | Trait save(strength,+N) |
| saving_throw_dexterity | 2 | Trait save(agility,+N) |
| saving_throw_constitution | 29 | Trait save(constitution,+N) |
| saving_throw_intelligence | 2 | Trait save(intelligence,+N) |
| saving_throw_wisdom / charisma | 21 | Trait save(willpower,+N)；两个来源仍保留各自 provenance |
| saving_throw_fear | 1 | save_tag(frightened,+3)，并保持原 dragon frightful presence 免疫为独立布尔来源 |

旧 resistance_* 的 +5/+10/+15/+20/+25 不是百分比、固定 DR 或当前合法 tier，全部废止旧量纲。正向“抗性”统一为 half；原文明确写“免疫”时另加 immune，明确弱点时使用 double。重复 half 只取一个 half。

### 5.3 技能、工具与条件战斗字段

| 原字段组 | 数量 | 优化结果 |
|---|---:|---|
| acrobatics、animal_handling、arcana、athletics、deception、grapple、history、insight、intimidation、investigation、leadership、medicine、nature、perception、performance、persuasion、religion、sleight_of_hand、stealth、survival 的 *_bonus | 718 | check(原 check_id,+N)，数值与语义都保留；等待 typed utility-check owner |
| movement_speed | 102 | +5/+10→move_point_capacity_delta +1；+15→+2；-5→-1 |
| initiative_bonus | 6 | battle_start_progress(+N)，保留原数值 |
| spell_attack_bonus | 23 | spell_hit(+N)，按施法来源限制 |
| spell_dc / spell_dc_bonus | 15 | spell_save_dc(+N)，不得写成会被等级计算覆盖的 spell_proficiency_bonus |
| spell_damage_fire | 2 | 对 qualifying fire spell 主直接 segment 增加原 flat 值；防 equipment-origin 递归 |
| critical_threat_range | 1 | 常驻 crit_threshold_delta=-1 |
| attack_bonus_undead | 1 | typed target-tag attack bonus；只匹配 canonical undead |

技能检定不折成基础属性。为了避免同一套 10 个槽位把同一检定堆到 +7/+9，单件 check bonus 采用 same set + same check 取最高值；阈值提供的 check bonus 是独立来源，达到阈值后再相加。这个叠加策略属于未来 utility-check owner 的合同，不修改任何物品成员。

## 6. 单件数值审计决定（配置冻结）

### 6.1 保留项

- 套装 1–99 的护甲 AC chassis 全部保留。轻/中/重甲标签不能单独证明主题化的 ±1 AC 是错误，且原文未给出完整获取档位。
- hp_max、mp_max、全局 attack_bonus、dodge_bonus、六维和技能检定 bonus 保留原数值。
- 单件静态数值保留，只有字段名、owner 和非法量纲按第 5 节迁移；旧阈值数值仅作新奖励曲线的基线证据。
- 饰品上的 armor_ac_bonus 当前保持冻结。若未来单独授权 typed migration，15 件 +1 应投影为 deflection_bonus +1，acc_magnet_master_cloak 的 +2 应收敛为 deflection_bonus +1；本轮不回写。

### 6.2 可证明的离群项（本轮不修正）

创世神兵四件护甲原 AC 为 3/10/2/2，总计 +17；即使不计 2 件套的额外 +2，也比龙鳞、大地守护者、圣光使者的高阶重甲 +11 高 6 点，比铁壁要塞 +12 高 5 点。这个差值已经不是主题化的 ±1，而会单独改变约 25%–30% 的 D20 命中区间。

这能证明它是离群项，但用户已冻结全部单品配置，因此本轮不修改这些数值：

| item_id | 冻结原值 | 本轮处理 |
|---|---:|---|
| armor_creation_divine_head | armor_ac_bonus +3 | 保持原样；记录 balance blocker |
| armor_creation_divine_body | armor_ac_bonus +10 | 保持原样；连同单件全伤害免疫记录为 balance blocker |
| armor_creation_divine_hands | armor_ac_bonus +2 | 保持原样；记录 balance blocker |
| armor_creation_divine_feet | armor_ac_bonus +2 | 保持原样；记录 balance blocker |

创世神兵的全部单件属性与十系抗性保持冻结；旧 2/4 件奖励和创世之力作为新奖励链的机制输入保留，但解锁位置与数值由 Set 100 的新设计决定。新套装层只能避免继续追加防御，不能消除已冻结单件造成的失衡。若未来要修正上述单件，必须另获明确配置修改授权。

## 7. 新阈值计数范围

每套原有 10 件成员全部参与计数，不再只统计四件护甲或六件饰品。正式定义由套装侧显式保存 10 个既有 `item_id`；不新增或修改物品字段，也不只凭 tag 判断身份。

门槛结构逐套选择，使用 4 或 5 个递增门槛并以 10 件封顶，详见 [重设计总则](existing_set_bonus_redesign.md) 与三份逐套设计。正式 runtime 必须以有效装备 instance 去重；损坏失效、战斗换装移除或多槽位占用都不能重复贡献。

## 8. 配置冲突与共同 runtime gap

### 8.1 Set 11 / Set 95 冲突

Set 11“星辰织者”和 Set 95“星辰编织者”原样共用 star_weaver_set；四个 armor_star_weaver_* 物品 ID 也发生碰撞。本任务禁止改配置，因此本文件不重命名、不拆 tag，也不偷偷选择其中一套。

结果是：

- 两套的属性优化都可以记录。
- 在身份冲突解除前，Set 95 不能宣称可注册、可正确计数或已落地。
- 未来若要落地，必须由用户另行授权配置消歧；兼容别名或按显示名猜测都不是可接受方案。

### 8.2 全套装共同缺口

当前仓库没有正式 GearSet definition、membership registry、threshold evaluator 或 derived threshold battle source。所有 2/4/6 件效果仍是 planned。正式交付至少需要：

1. 配置冻结条件下可验证的 membership 与 contribution scope snapshot。
2. 累计阈值、有效装备、损坏和战斗换装刷新。
3. 阈值静态属性、Trait、装备能力和授予 SkillDef 的统一 provenance/lifecycle。
4. environment、target tag、damage origin、usage scope 和 save-tag 的 typed facts。
5. preview、AI、UI、save/writeback 与真实 producer。
6. 每套围绕原特殊效果的 focused regression；Resource 可加载不能替代这些验证。

## 9. 旧 2/4/6 件效果迁移清单（非最终门槛）

> 本节保留旧效果的 typed owner 审计，便于确认招牌机制没有在重设计中丢失。下表里的“2件/4件/6件”只表示旧来源位置，不是新的最终门槛或数值权威；最终设计以三份 `existing_set_bonus_redesign_*` 文档为准。

记号：

- check(x,+N)：保留原检定语义的 typed utility bonus。
- res(x,half)：Trait damage_resistance_entries。
- save(x,+N)：Trait save_bonus_entries；save_tag(x,+N) 是按状态 tag 的豁免加值。
- move(+N)：BattleStatusEffectState.move_point_capacity_delta。
- spell_hit / spell_save_dc / crit_threshold_delta / battle_start_progress：第 4 节所列 typed query 或 producer。
- “保留 effect_id”表示完整保留原效果全文；不是只保留一个字符串，也不是声称现有 runtime 已经支持。

### 9.1 套装 1–10：原护甲 2/4 件阈值

| # | 原套装 / tag | 属性覆盖 | 原特殊机制保留与 owner |
|---:|---|---|---|
| 1 | 晨光圣骑士 / dawn_paladin_set | 2件：res(radiant,half)、save(willpower,+1)；4件：armor_ac_bonus +1 | 完整保留黎明/邪恶目标条件与 dawn_paladin_martyr_light 的低血团队临时 HP；需要 dawn、alignment/creature tag、HP 阈值、团队范围和每战 usage |
| 2 | 暗影刺客 / shadow_assassin_set | 2件：check(stealth,+3)，暗光额外 +2；4件：move(+1) | 完整保留 shadow_assassin_intangible 的隐匿/隐形首个近战段 +2D6 negative_energy 和击杀后隐藏；需要 light、hidden、OnKill 与伤害段 owner |
| 3 | 霜冻守望者 / frost_warden_set | 2件：res(freeze,half)；4件：armor_ac_bonus +1 | 完整保留冰地形通行、近战 +1D6 freeze、火伤转换与 frost_warden_breath；需要 terrain、incoming-damage rewrite、锥形技能和 daily usage |
| 4 | 烈焰法师 / flame_mage_set | 2件：res(fire,half)、spell_hit(+1，仅 fire spell)；4件：mp_max +10 | 完整保留 flame_mage_phoenix、致命拦截、火法术后的下一段附伤与安全位置传送；单件与阈值凤凰来源不合并 |
| 5 | 大地守护者 / earth_warden_set | 2件：save(strength,+2)，静止时 AC +2；4件：hp_max +10 | 完整保留 earth_warden_body 的强制位移免疫、受击反制与大地束缚；需要 stationary、incoming-hit、terrain/control owner |
| 6 | 风暴行者 / storm_walker_set | 2件：res(lightning,half)、battle_start_progress(+3)；4件：move(+1) | 完整保留 storm_walker_avatar 的近战雷伤、蓄电/释放与风暴条件加值；storm 需要正式环境注入，蓄电需要 source-bound state |
| 7 | 亡灵收割者 / death_reaper_set | 2件：res(negative_energy,half)，对 undead 攻击 +2；4件：hp_max +10 | 完整保留 death_reaper_harvest 的击杀治疗、低血目标附伤与死亡之握；需要 undead tag、HP 百分比、OnKill 和防递归伤害 |
| 8 | 龙鳞铠甲 / dragon_scale_set | 2件：res(fire,half)、save_tag(frightened,+3)、dragon frightful presence immune；4件：对 dragon 攻击 +2 | 完整保留 dragon_slayer_oath，并严格采用第 10 节合同：每个真实主直接 segment +1D4、dragon breath half、龙血沸腾；不得按 cast 去重或漏掉单件效果 |
| 9 | 铁壁要塞 / iron_bulwark_set | 2件：hp_max +10；4件：armor_ac_bonus +1 | 完整保留 iron_bulwark_fortress 的邻近盟友 AC、强制位移/prone 免疫、前方伤害分担与暴击降级；需要 adjacency、facing、reaction 与 mitigation query |
| 10 | 古代帝王 / ancient_emperor_set | 2件：check(persuasion,+3)，对较低 CR 目标攻击 +2；4件：willpower +1 | 完整保留 ancient_emperor_majesty 的邻域 AC 压制、低 CR 附伤、帝王敕令和故土条件；需要 CR、country/environment 与 aura owner |

### 9.2 套装 11–20：原饰品 2/4/6 件阈值

旧方案中的这些阈值只统计六个饰品槽，同 tag 的四件护甲不贡献旧 2/4/6；新方案已废止这项计数限制，十件既有成员全部参与，具体门槛以逐套重设计为准。

| # | 原套装 / tag | 属性覆盖 | 原特殊机制保留与 owner |
|---:|---|---|---|
| 11 | 星辰织者 / star_weaver_set | 2件：battle_start_progress(+2)、夜间 check(perception,+2)；4件：无静态替代；6件：夜间攻击/豁免 +1、盟友 battle_start_progress(+1) | 完整保留预见、致命伤 20% 保留 1 HP、月度重掷与星辰光环；需要 night、lethal intercept、calendar、roll transaction；与 Set 95 的身份冲突见第 8 节 |
| 12 | 毒蛇之吻 / vipers_kiss_set | 2件：res(poison,half)，非魔法 poison/disease immune；4件：无静态替代；6件：无静态替代 | 完整保留近战 +1D4 poison、poisoned、三次混合毒、毒雾、毒伤转治疗和对中毒目标附伤；需要 status、aura、damage replacement |
| 13 | 北风旅者 / northwind_traveler_set | 2件：move(+1)，困难地形免疫；4件、6件：无静态替代 | 完整保留擒抱/束缚下移动、传送、旅行速度、水面行走、坠落免疫和飞行；需要 world traversal、flight/fall 与 usage owner |
| 14 | 虚空行者 / void_walker_set | 2件：res(force,half)，空间锁定免疫；4件、6件：无静态替代 | 完整保留两次传送、20% 虚空闪避、穿墙/虚体、真实视觉和使用代价；需要 phase、targetability、illusion 与 post-use save |
| 15 | 自然之语 / natures_whisper_set | 2件：无静态替代；4件：自然环境 check +1、move(+1)；6件：无静态替代 | 完整保留动物/植物关系、召唤、自然治疗、静止回复与植物指挥；需要 world relation、environment、summon 和 stationary |
| 16 | 深渊凝视 / abyss_gazer_set | 2件：blind immune；4件：对 aberration/eldritch 攻击 +2；6件：敌方攻击压制 aura | 完整保留识破幻象、恐惧凝视、水下呼吸、深渊召唤与长休代价；需要 creature tags、vision、aura stacks、summon/rest owner |
| 17 | 圣光使者 / lightbringer_set | 2件：frightened immune；4件：对 undead/demon 攻击 +2；6件：盟友 negative_energy/poison 固定 DR 5 | 完整保留治疗触摸、目标附加 radiant、圣光爆发、治疗与驱散 darkness；需要 demon/fiend tag、光照、复合 AoE 和队伍 aura |
| 18 | 永恒学徒 / eternal_apprentice_set | 2件：五类知识 check +3；4件：mp_max +15；6件：无静态替代 | 完整保留学习/准备时间、知识检定自动成功、临时技能/法术替换、材料减耗和全语言阅读；需要 progression、language、temporary grant |
| 19 | 血月猎人 / blood_moon_hunter_set | 2件：tracking check +3，对 shapechanger 感知 +5；4件：对 shapechanger 攻击 +3；6件：血月属性检定 +1 | 完整保留 silver 附伤、再生抑制、血月加伤与 lycanthropy 压制；shapechanger/werewolf、silver、moon phase 目前没有完整 producer |
| 20 | 锈蚀齿轮 / rusted_gear_set | 2件：artisan check +3；4件：对 construct 攻击 +2；6件：construct 形态 armor_ac_bonus +2、move(-1) | 完整保留机器语言、构装控制、形态免疫、快速制作与 10% 半机械复活/constitution -1；需要 tool/crafting、construct control、lethal/persistence |

### 9.3 套装 21–40：原护甲 2/4 件阈值

| # | 原套装 / tag | 优化后的阈值属性 | 原阈值特殊效果 |
|---:|---|---|---|
| 21 | 银月游侠 / silvermoon_ranger_set | 2件：ranged_hit +1、check(perception,+2)；4件：ranged_hit +2、crit_threshold_delta -1 | 完整保留 silvermoon_volley；需要 night、邻接目标、每战次数和真实 ranged source |
| 22 | 沙漠蝎 / desert_scorpion_set | 2件：res(fire,half)、check(stealth,+2)；4件：attack_bonus +1 | 完整保留 scorpion_sting；需要 hidden opener、poison segment 和 poisoned save |
| 23 | 丛林黑豹 / jungle_panther_set | 2件：move(+1)、check(stealth,+2)；4件：attack_bonus +1、move(+1) | 完整保留 panther_pounce；需要移动距离、追加真实攻击、bleed 与 bonus-action owner |
| 24 | 高地雄鹰 / highland_eagle_set | 2件：check(perception,+3)、check(investigation,+2)；4件：ranged_hit +2 | 完整保留 eagle_dive；需要高度差、伤害骰倍率和每战次数 |
| 25 | 海盗船长 / pirate_captain_set | 2件：check(athletics,+3)、res(acid,half)；4件：willpower +2 | 完整保留 captain_command；需要队友范围、下一次攻击 source 和额外攻击动作 |
| 26 | 赏金猎人 / bounty_hunter_set | 2件：check(survival,+3)、check(perception,+2)；4件：attack_bonus +1 | 完整保留 bounty_execute；需要目标 HP 百分比与 per-target/per-battle memory |
| 27 | 瘟疫医生 / plague_doctor_set | 2件：res(poison,half)、check(medicine,+3)；4件：save(constitution,+2) | 完整保留 plague_spread；需要感染、传播半径、持续 tick 与疾病 taxonomy |
| 28 | 月影舞者 / moonshadow_dancer_set | 2件：check(acrobatics,+3)、check(stealth,+2)；4件：attack_bonus +1、armor_ac_bonus +1 | 完整保留 moonshadow_finale；需要 night、incoming miss、reaction attack 与 auto-hide |
| 29 | 血骑士 / blood_knight_set | 2件：hp_max +15、save(constitution,+2)；4件：attack_bonus +2 | 完整保留 blood_sacrifice；吸血必须按实际 HP 伤害计算，排除过量伤害并防递归 |
| 30 | 幻影骑士 / phantom_rider_set | 2件：move(+2)、save(agility,+2)；4件：attack_bonus +1 | 完整保留 phantom_charge；需要 mounted、移动距离、相位、push/prone 和每战次数 |
| 31 | 风语者 / wind_whisperer_set | 2件：move(+1)、check(perception,+2)；4件：armor_ac_bonus +1、move(+1) | 完整保留 wind_storm；需要风域、远程劣势、区域移动与 TU duration |
| 32 | 水波行者 / wave_walker_set | 2件：armor_ac_bonus +1、check(athletics,+3)；4件：res(freeze,half)、save(strength,+2) | 完整保留 tidal_control；需要 water/underwater、锥形、推离和 prone |
| 33 | 地震先知 / earthquake_seer_set | 2件：save(agility,+2)、check(perception,+2)；4件：armor_ac_bonus +1、save(strength,+2) | 完整保留 seismic_wave；需要 stationary/quake、区域控制和地形破坏 |
| 34 | 夜幕吟游诗人 / night_bard_set | 2件：check(performance,+3)、check(persuasion,+2)；4件：mp_max +15 | 完整保留 night_finale；需要 night、歌曲 aura、charm/psychic 和队伍增益 |
| 35 | 白鸦信使 / white_raven_set | 2件：check(perception,+3)、check(investigation,+2)；4件：armor_ac_bonus +1、move(+1) | 完整保留 raven_flock；需要遮蔽/距离限制、传送次数和 signal/letter world owner |
| 36 | 红狐盗贼 / red_fox_set | 2件：check(stealth,+3)、check(sleight_of_hand,+2)；4件：armor_ac_bonus +1、agility +1 | 完整保留 fox_illusion；需要命中重定向、1 HP illusion、诱饵仇恨和 world lock/trap |
| 37 | 青蛇刺客 / green_viper_set | 2件：res(poison,half)、check(stealth,+2)；4件：crit_threshold_delta -1、attack_bonus +1 | 完整保留 viper_kiss；隐藏首击自动暴击、毒伤和冷却均保留，不以限次暴击替代常驻 crit owner |
| 38 | 金蝶幻术师 / golden_butterfly_set | 2件：check(deception,+3)、check(performance,+2)；4件：mp_max +20 | 完整保留 butterfly_dream；需要 illusion entity、区域状态、友军 AC 与逐 tick 治疗 |
| 39 | 紫晶心灵师 / amethyst_psychic_set | 2件：res(psychic,half)、check(insight,+3)；4件：mp_max +20、spell_hit(+1) | 完整保留 psychic_storm；需要 concentration interruption、队伍 immunity 与 spell-source query |
| 40 | 黑铁咒术师 / black_iron_warlock_set | 2件：check(arcana,+3)、save(willpower,+2)；4件：mp_max +20、spell_hit(+1) | 完整保留 contract_backlash；需要契约状态、damage-reduced fact、治疗与可解除的持久 AC 变化 |

### 9.4 套装 41–70：原护甲 2/4 件阈值

| # | 原套装 / tag | 优化后的阈值属性 | 原阈值特殊效果 |
|---:|---|---|---|
| 41 | 太阳圣骑士 / solar_paladin_set | 2件：res(radiant,half)、save(willpower,+2)；4件：armor_ac_bonus +1、attack_bonus +1 | 完整保留 solar_judgment；需要 day、evil、darkness dispel、击杀恐惧与主伤害段附伤 |
| 42 | 虚空守护者 / void_warden_set | 2件：res(force,half)、armor_ac_bonus +1；4件：spell_hit(+1) | 完整保留 void_devour；需要传送/强制移动 immunity、随机 canonical 属性减益和持续裂隙 |
| 43 | 自然守护者 / nature_warden_set | 2件：res(poison,half)、check(animal_handling,+3)；4件：mp_max +15、save(constitution,+2) | 完整保留 nature_wrath 的山崩、缠绕、兽群三分支；需要 environment、trap、召唤与 world flora/fauna |
| 44 | 符文铁匠 / rune_smith_set | 2件：check(arcana,+3)、check(investigation,+2)；4件：spell_hit(+1)、armor_ac_bonus +1 | 完整保留 rune_storm；需要装备目标临时铭刻、混合伤害、双 save 与跨战 duration |
| 45 | 灵魂收割者 / soul_reaper_set | 2件：res(negative_energy,half)、check(religion,+3)；4件：attack_bonus +1 | 完整保留 soul_harvest；需要 OnKill 灵魂层、爆发、临时 hp_max 损伤与 soul provenance |
| 46 | 时间行者 / chrono_walker_set | 2件：battle_start_progress(+3)、check(arcana,+2)；4件：move(+2) | 完整保留 time_rift 的加速、减速、回溯三分支；需要行动类别限制与上回合开始快照 |
| 47 | 梦境编织者 / dream_weaver_set | 2件：check(insight,+3)、check(deception,+2)；4件：mp_max +20 | 完整保留 nightmare_fall；需要重复 save 失败计数、unconscious 唤醒条件和 dream world owner |
| 48 | 深渊潜行者 / abyss_stalker_set | 2件：res(psychic,half)、check(stealth,+2)；4件：attack_bonus +2 | 完整保留 abyss_devour；需要水压/暗光、形态自检、范围脉冲、恐惧/魅惑 immunity |
| 49 | 星辰观测者 / star_gazer_set | 2件：check(arcana,+3)、check(perception,+2)；4件：spell_hit(+1)、mp_max +15 | 完整保留 star_fall；需要 night/sky、延迟 telegraph、混合伤害和可破坏地形 |
| 50 | 混沌使者 / chaos_herald_set | 2件：save(willpower,+3)、check(arcana,+2)；4件：spell_hit(+1) | 完整保留 chaos_storm；每个目标、每次 tick 独立掷结果，需要可复现 RNG 与 outcome router |
| 51 | 雷神之甲 / thunder_god_set | 2件：res(lightning,half)、save(agility,+2)；4件：attack_bonus +2 | 完整保留 thunder_wrath；需要 storm、装备过载、主目标/溅射双 save 与 scheduled lightning |
| 52 | 美杜莎之凝视 / medusa_gaze_set | 2件：res(poison,half)、check(intimidation,+3)；4件：armor_ac_bonus +1 | 完整保留 petrify_gaze；需要 gaze/facing、分阶段石化、回合末重掷与 restoration cleanse |
| 53 | 凤凰重生 / phoenix_rebirth_set | 历史输入（已取代）：2件 res(fire,half)、save(constitution,+2)；4件 hp_max +20 | 已按 3/5/7/10 落地；fatal 来源各用独立 usage，凤凰蛋为 `per_world_month=1`，不使用共享 usage 或跨战 24 小时 owner；见 [当前实现](../../design/progression/equipment_sets.md) |
| 54 | 塞壬之歌 / siren_song_set | 2件：check(performance,+3)、check(persuasion,+2)；4件：mp_max +15 | 完整保留 siren_dirge；需要 audibility、水域/船只、强制趋近、友军 aura 与逐 tick 治疗 |
| 55 | 狼人诅咒 / werewolf_curse_set | 2件：attack_bonus +1、check(perception,+2)；4件：strength +2、agility +1 | 完整保留 werewolf_rage；需要 moon phase、shapechange、禁法、nearest-unit 强制选敌与 silver weakness |
| 56 | 影舞者 / shadow_dancer_set | 2件：check(stealth,+3)、check(acrobatics,+2)；4件：attack_bonus +1、crit_threshold_delta -1 | 完整保留 shadow_finale；需要 shadow cell、sneak provenance、OnKill 连续传送/攻击与上限 |
| 57 | 水晶先知 / crystal_seer_set | 2件：check(arcana,+3)、check(insight,+2)；4件：mp_max +20 | 完整保留 crystal_storm；需要敌方意图、反射、任意重掷、光照/透明材质与物体破坏 |
| 58 | 霜巨人 / frost_giant_set | 2件：res(freeze,half)、strength +1；4件：attack_bonus +2、armor_ac_bonus +1 | 完整保留 glacier_collapse；需要温度、冰地形、freeze+physical_blunt 双段和束缚状态 |
| 59 | 木乃伊诅咒 / mummy_curse_set | 2件：res(negative_energy,half)、save(constitution,+2)；4件：attack_bonus +1 | 完整保留 mummy_decay；需要 curse/disease、hp_max 损伤、沙形态与复活限制 |
| 60 | 龙骑士 / dragon_rider_set | 2件：res(fire,half)、check(intimidation,+3)；4件：armor_ac_bonus +2、attack_bonus +1 | 完整保留 dragon_rider_descent；需要元素选择持久化、骑乘、飞行、高度与可选龙息 |
| 61 | 血肉编织者 / flesh_weaver_set | 2件：check(medicine,+3)、res(negative_energy,half)；4件：hp_max +20 | 完整保留 flesh_puppet；需要尸体派生 summon、致命拦截、damage transfer 与 source recursion guard |
| 62 | 骸骨领主 / bone_lord_set | 2件：armor_ac_bonus +1、res(physical_pierce,half)；4件：save(willpower,+2) | 完整保留 bone_legion；需要亡灵控制、召唤数量/性能预算和群体命令 |
| 63 | 荆棘女王 / thorn_queen_set | 2件：armor_ac_bonus +1、res(physical_pierce,half)；4件：mp_max +15 | 完整保留 thorn_throne；需要移动领域、反伤防递归、击杀扩张和植物 world owner |
| 64 | 灰烬行者 / ash_walker_set | 2件：res(fire,half)、save(constitution,+2)；4件：attack_bonus +1 | 完整保留 ash_storm；需要 fire/ash/lava terrain、动作类别限制、视距与致命重生 |
| 65 | 迷雾行者 / mist_walker_set | 2件：check(stealth,+3)、save(agility,+2)；4件：attack_bonus +1、crit_threshold_delta -1 | 完整保留 mist_reaper；需要雾中可见性/隐形、连续驻留计数、场内传送与每回合附伤 |
| 66 | 铁处女 / iron_maiden_set | 2件：armor_ac_bonus +2、res(physical_blunt,half)；4件：armor_ac_bonus +1 | 完整保留 iron_spike；需要 stationary、受击 reaction、武器卡住/拔出与痛苦伤害递归边界 |
| 67 | 蜘蛛女王 / spider_queen_set | 2件：check(stealth,+3)、res(poison,half)；4件：attack_bonus +1 | 完整保留 spider_web；需要蛛网格、攀墙/天花板、restrained、传送和召唤性能预算 |
| 68 | 乌鸦领主 / raven_lord_set | 2件：check(perception,+3)、check(insight,+2)；4件：attack_bonus +1 | 完整保留 raven_flock_death；需要濒死感知、scheduled summon damage、吞魂与复活限制 |
| 69 | 锈刃骑士 / rust_blade_set | 2件：armor_ac_bonus +1、res(acid,half)；4件：attack_bonus +2 | 完整保留 rust_plague；需要装备选择、锈层、durability、AC/命中减益与永久碎裂 |
| 70 | 镜中恶魔 / mirror_demon_set | 2件：check(deception,+3)、save(willpower,+2)；4件：spell_hit(+1) | 完整保留 mirror_shatter；任意能力复制、攻击/法术反射和双方原子换位仍是明确 ABI gap |

### 9.5 套装 71–100：原护甲 2/4 件阈值

| # | 原套装 / tag | 优化后的阈值属性 | 原阈值特殊效果 |
|---:|---|---|---|
| 71 | 武士之魂 / samurai_spirit_set | 2件：attack_bonus +1、save(willpower,+2)；4件：attack_bonus +2、crit_threshold_delta -1 | 完整保留 iaijutsu 的跨回合准备、优势、自动暴击、低血附伤和 action lock |
| 72 | 忍者之影 / ninja_shadow_set | 2件：check(stealth,+3)、check(acrobatics,+2)；4件：attack_bonus +1 | 完整保留 shadow_clone；需要随机召唤数、真假识别、同步攻击和死亡爆烟 |
| 73 | 少林武僧 / shaolin_monk_set | 2件：unarmed_hit +2、save(constitution,+2)；4件：unarmed_hit +2、armor_ac_bonus +1 | 完整保留 buddha_palm；需要徒手来源、bonus 徒手、alignment 条件、混合伤害和控制 |
| 74 | 天狗面具 / tengu_mask_set | 2件：move(+1)、check(perception,+2)；4件：attack_bonus +1 | 完整保留 tengu_storm；需要飞行、路径伤害、推离、山林环境和落雷 |
| 75 | 九尾妖狐 / nine_tailed_fox_set | 2件：willpower +2、check(deception,+3)；4件：mp_max +20 | 完整保留 nine_tail_fire；需要锥形混合伤害、burn、形态、移动与逐段火伤 |
| 76 | 阴阳师 / onmyoji_set | 2件：check(arcana,+3)、check(religion,+2)；4件：mp_max +20 | 完整保留 hyakki_yako；十二种式神、随机候选、统一指挥和死亡爆炸都必须真实创作 |
| 77 | 夜叉 / yaksha_set | 2件：strength +2、check(intimidation,+3)；4件：attack_bonus +2、hp_max +20 | 完整保留 asura_form；需要体型、属性上限突破、命中后 grapple、自伤与 exhaustion |
| 78 | 机关傀儡师 / karakuri_puppeteer_set | 2件：check(sleight_of_hand,+3)、check(investigation,+2)；4件：mp_max +15 | 完整保留 puppet_theater；需要 summon、完全指挥、护盾/突刺/自爆与原子换位 |
| 79 | 雷神之鼓 / raijin_drum_set | 2件：check(performance,+3)、save(constitution,+2)；4件：spell_hit(+1) | 完整保留 thunder_drum；需要连续三回合多道雷击、重复命中 stun 与友军 immunity aura |
| 80 | 河童铠甲 / kappa_armor_set | 2件：armor_ac_bonus +1、check(athletics,+3)；4件：strength +1 | 完整保留 kappa_drain；需要水下移动、能力值 drain/transfer、归零昏迷与拖拽 |
| 81 | 蒸汽骑士 / steam_knight_set | 2件：strength +1、armor_ac_bonus +1；4件：attack_bonus +2 | 完整保留 steam_overload；需要受限额外 action、附伤、自伤、冷却 action lock 与濒死爆炸 |
| 82 | 发条之心 / clockwork_heart_set | 2件：battle_start_progress(+3)、check(investigation,+2)；4件：spell_hit(+1) | 完整保留 time_gear；需要动作类别限制、加速/减速、可被奥术检定破坏的状态 |
| 83 | 以太行者 / aether_walker_set | 2件：check(arcana,+3)、save(agility,+2)；4件：spell_hit(+1) | 完整保留 aether_storm；需要相位层、穿墙、不可选中、裂隙传送与随机位面移出 |
| 84 | 磁力大师 / magnet_master_set | 2件：save(strength,+2)、check(investigation,+2)；4件：attack_bonus +1 | 完整保留 magnetic_flip；需要金属 tag、push/pull、disarm、装备耐久和永久碎裂 |
| 85 | 光子剑士 / photon_swordsman_set | 2件：attack_bonus +1、agility +1；4件：attack_bonus +2、crit_threshold_delta -1 | 完整保留 light_speed_slash；需要三次真实攻击、cover/reaction 抑制、前后传送 |
| 86 | 音波战士 / sonic_warrior_set | 2件：attack_bonus +1、check(performance,+2)；4件：attack_bonus +1 | 完整保留 ultra_sonic；需要穿墙半伤、物体材质破坏、deafened/stunned 与混合伤害 |
| 87 | 重力行者 / gravity_walker_set | 2件：save(strength,+2)、check(acrobatics,+3)；4件：attack_bonus +1 | 完整保留 gravity_well；需要墙面行走、坠落规则、移动区域、光线路径与 push/prone |
| 88 | 量子幽灵 / quantum_ghost_set | 2件：check(stealth,+3)、check(arcana,+2)；4件：spell_hit(+1) | 完整保留 quantum_collapse；需要每目标独立 outcome、save-only AoE 闪避与安全随机落点 |
| 89 | 辐射行者 / radiation_walker_set | 2件：res(radiant,half)、res(negative_energy,half)；4件：attack_bonus +1 | 完整保留 critical_mass；需要跟随 aura、疾病、hp_max 损伤、物质衰变与 exhaustion |
| 90 | 赛博行者 / cyber_walker_set | 2件：check(investigation,+3)、check(sleight_of_hand,+2)；4件：attack_bonus +2 | 完整保留 system_overload；需要设备感知、首击 miss、cover 忽略、敌方行动控制与自毁 |
| 91 | 创世泰坦 / creation_titan_set | 2件：strength +2、constitution +1；4件：attack_bonus +2、armor_ac_bonus +2 | 完整保留 world_remake；需要临时物品/屏障和可恢复的战场/世界地形修改 |
| 92 | 虚空吞噬者 / void_devourer_set | 2件：res(force,half)、save(constitution,+2)；4件：attack_bonus +2 | 完整保留 void_erasure；需要仍计时的现实移出、hp_max 损伤、存在删除与复活封锁 |
| 93 | 圣光仲裁者 / holy_arbiter_set | 2件：res(radiant,half)、check(insight,+3)；4件：attack_bonus +2、armor_ac_bonus +1 | 完整保留 final_judgment；需要 alignment、能力封锁、友军 aura 与复活限制 |
| 94 | 死亡使者 / death_herald_set | 2件：res(negative_energy,half)、check(religion,+3)；4件：attack_bonus +2 | 完整保留 death_list；需要 OnBattleStart 自动标记、三回合窗口、转移、缓刑治疗与复活限制 |
| 95 | 星辰编织者 / star_weaver_set | 2件：check(arcana,+3)、check(insight,+2)；4件：mp_max +20 | 完整保留 fate_tapestry；重织、切断和命运护甲均保留。因第 8 节 P0 身份冲突，当前不得宣称可注册 |
| 96 | 命运编织者 / fate_weaver_set | 2件：check(insight,+3)、check(persuasion,+2)；4件：spell_hit(+1) | 完整保留 causal_inversion；任意重掷、行动预支/控制和通用伤害反射都是 ABI gap |
| 97 | 梦境之主 / dream_lord_set | 2件：check(arcana,+3)、check(deception,+2)；4件：mp_max +25 | 完整保留 dream_lord_nightmare；需要 sleep/dream、误认友敌、法术资源恢复和领域传送 |
| 98 | 时间旅者 / time_traveler_set | 2件：battle_start_progress(+3)、check(history,+3)；4件：move(+2) | 完整保留 time_rewind；需要 intended-action query 与友方 HP/位置/资源/消耗品的选择性回滚 |
| 99 | 宇宙吞噬者 / cosmic_devourer_set | 2件：res(force,half)、save(constitution,+2)；4件：attack_bonus +2 | 完整保留 black_hole；需要 scheduled moving area、拉拽/伤害、远程劣势、爆炸和中心吸收 |
| 100 | 创世神 / creation_divine_set | 2件：armor_ac_bonus +2、save(willpower,+3)；4件：attack_bonus +2、mp_max +30 | 完整记录 creation_force 的创生、毁灭、重塑三分支；单件 AC、十系 half 与原免疫全部冻结，Set 100 因此保持 balance blocker |

## 10. 龙鳞套必须保留的行为合同

套装 8 不只是一行属性映射，它是所有套装后续落地的行为基准。下表的 2/4 件行记录旧来源，实际解锁位置由新的 Set 8 奖励链决定；伤害段粒度、单件来源独立性和递归排除不得改变：

| 来源 | 优化后合同 |
|---|---|
| 头盔 | armor_ac_bonus +2；res(fire,half)；对 dragon 攻击 +2 |
| 胸甲 | armor_ac_bonus +7；hp_max +10；res(fire/freeze/lightning,half)；dragon breath 获得 half |
| 护手 | armor_ac_bonus +1；全局 attack_bonus +2；每次近战武器命中 dragon 的真实主段 +1D4 |
| 胫甲 | armor_ac_bonus +1；save_tag(frightened,+3)；dragon frightful presence immune |
| 2件 | res(fire,half)；save_tag(frightened,+3)；dragon frightful presence immune |
| 4件 | 对 dragon 攻击 +2；每个 qualifying 主直接伤害 segment +1D4；dragon breath half；授予龙血沸腾 |
| 满套近战 | 护手 +1D4 与 4件 +1D4 同段相加为 2D4；三段全部命中为 6D4，九段全部命中为 18D4 |
| 龙血沸腾 | per_world_day 1；300 TU；对 dragon 攻击额外 +3；3 个命中治疗 charge，每次真实攻击命中治疗 1D6 并消耗 1 层 |

额外伤害查询必须 fail closed：

- 只处理有明确 DamageOriginKind 的主直接段。
- extra_damage_segments、timeline/upkeep、地形 tick、反射、装备能力生成伤害、触发技能和自伤均排除。
- 不按 cast、skill、effect ordinal、target 或 event batch 跨段去重。
- preview/AI 只读取，不消费 usage/charge，不改真实状态。

## 11. 原特殊效果的统一落地分类

逐套表中的“完整保留”按以下方式进入后续实现，不使用 opaque special_effect_id 字符串分支：

| 类别 | 典型套装 | 当前落点 |
|---|---|---|
| 静态属性与资源 | 全部套装 | AttributeModifier；当前已实现 |
| 抗性、豁免、免疫 | 龙鳞、霜巨人、圣光、毒蛇等 | TraitDef；ability save/resistance 已实现，save-tag producer 仍缺 |
| 目标/来源条件命中 | 屠龙、亡灵、shapechanger、远程、徒手、施法 | equipment attack-check query；部分可复用，tag 与 source coverage 需补齐 |
| 每段附伤、吸血、反伤 | 龙鳞、血骑士、毒蛇、荆棘等 | damage segment query/reaction；必须带 origin、防递归、真实 HP 伤害 |
| 状态、领域、光环 | 瘟疫、迷雾、梦境、重力等 | equipment ability + SkillDef + status/area；移动 aura 与跨 tick state 需补齐 |
| 召唤与指挥 | 自然、骸骨、式神、傀儡等 | 现有 summon/action 可复用；模板、数量预算和完整控制仍需逐套验证 |
| 环境与世界能力 | 黎明、夜间、风暴、森林、水域、月相等 | BattleEnvironmentSnapshot 目前只有 night 等少量真实 producer；其余必须由 world/session 注入 |
| 飞行、穿墙、水面/墙面行走 | 北风、虚空、天狗、重力等 | 战斗位移和 world traversal 仍缺统一 owner |
| 重掷、预见、回溯、复制 | 星辰、命运、时间、镜中恶魔等 | 需要 command/result transaction 或 typed snapshot/rollback；当前不能用静态属性替代 |
| 致死、复活限制、存在抹消 | 凤凰、木乃伊、死亡使者、虚空吞噬者等 | 需要 lethal lifecycle、revive eligibility 与持久写回；当前仍是高风险 gap |

## 12. 验收门槛

这份文档只完成“属性设计优化”，不把 planned 说成 landed。后续资源/运行时落地时必须同时满足：

1. 1000 个原 item_id、槽位、set tag、价格、描述与单件属性零漂移；第 5/6 节只记录未来 typed projection 与风险，不授权回写单品配置。
2. 100 个原套装编号、名称与 10 件成员零漂移；新门槛必须与逐套重设计文档一致，不再要求旧 2/4/6 标题和顺序零漂移。
3. 58 种旧单件字段全部有唯一 owner；禁止保留无人读取的兼容 alias。
4. 原单件与阈值特殊效果逐项有 typed producer、consumer、preview/AI 行为和 focused regression。
5. Set 11/95 冲突在获得单独配置修改授权前保持 P0 blocker。
6. 同档战斗模拟只在获取等级、普通装备曲线和真实敌人曲线齐备后用于微调；不得用资源加载成功或低级敌人结果宣称平衡完成。
