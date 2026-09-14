# 龙鳞铠甲套装完整落地方案

> 状态：已落地（2026-08-18）。三阶段全部完成，全文保留作为设计与验收记录；当前实现口径以 `docs/design/progression/equipment_sets.md` 与 `docs/design/battle/equipment_ability_runtime.md` 为准。实际落地摘要见 §15 末尾。
>
> 审计基线：2026-08-16 当前 checkout（`codex/tactical-skills-world-quest`，HEAD `07c3bd37`）。初版基线为 2026-08-05；2026-08-12 凤凰涅槃套装（phoenix_rebirth）作为第一个完整套装落地，通用套装系统随之建成并写入 `docs/design/progression/equipment_sets.md`（Current / Implemented）。本次修订把方案 rebase 到该已落地系统之上。
>
> 数值状态：元素抗性按当前离散 mitigation tier 体系；旧内容片段中的 `15/5/10` 不进入运行时。套装统一属于角色 10 级以后的高阶装备，部分套装可进入 20 级档；龙鳞套的具体获取档位与同档龙类目标仍需在内容冻结前明确。AC 与命中加值必须按 10/15/20 级目标审计，不能使用初始角色或 4–5 级幼龙定性。
>
> 本文定义的是一次完整交付，不是静态属性 MVP。四件物品、单件能力、2 件套、4 件套、真实敌方 producer、战斗换装、AI/preview、UI、存档和回归全部通过后，才能宣称龙鳞套装已落地。

## 1. 结论

首个套装已由凤凰涅槃完成，通用套装基础设施（authoring Resource、registry/snapshot、评估服务、属性投影、battle source、周期 usage、换装重算、UI 摘要、回归）均已落地，见 `docs/design/progression/equipment_sets.md`。

**龙鳞铠甲（Dragon Scale）是第二个完整套装，也是第一个需要扩展战斗 ABI 的套装。** 它的价值不在重建套装系统，而在用真实内容驱动四个可复用的通用边界落地：

- 条件性 mitigation tier（龙息半伤，§8.3）。
- 按每次实际主直接伤害结算加骰（§8.4）。
- 按 save tag 的永久性豁免加值与 canonical 龙威 save tag（§8.5）。
- 通用 attack-hit reaction 与 opt-in source-bound buff 清除（§8.6/§8.7）。

已有 `red_dragon`、白龙幼体、绿龙幼体及六种 `dragon_breath_*` 技能，可以用真实内容验证对龙条件和吐息减伤；但当前没有任何恐惧 producer，龙威技能与其 AI 使用是本切片必须新增的（§8.5）。

完成它以后，后续套装不需要退回按 `skill_id`、`item_id` 或 `special_effect_id` 写分支。

旧的 [gear_set_system.md](gear_set_system.md) 仍可作为早期需求来源，但其中 GDScript registry、按 tag occurrence 计数、弱 `Dictionary` 输出和 opaque `special_effect_ids` 不是当前 C# typed runtime 的实现蓝图。

## 2. 内容权威与冲突裁决

内容来源：

- 四件物品、基础属性和单件特殊效果：`docs/content/equipment_sets/sets_06_to_10.md:224-331`。
- 2 件套和 4 件套阈值效果：`docs/content/equipment_sets/set_bonus_design.md:254-280`。

采用以下明确口径。

### 2.1 保留全部单件与套装效果

“完整套装”包含：

1. 四件真实 `ItemDef` 资源及其基础属性。
2. 四条“特殊效果（设计预留）”。
3. 2 件套全部效果。
4. 4 件套全部效果。
5. 能实际触发龙威和吐息的敌人、技能与 AI action。

单件效果和套装效果即使语义相近，也不静默删除：

- 龙鳞头盔对 dragon 攻击检定 `+2`，与 4 件套的 `+2` 数值相加。
- 龙鳞护手的每次近战武器命中对 dragon `+1D4`，与 4 件套每次主直接伤害结算的 `+1D4` 数值相加；完整套装的近战单段合计为 `+2D4`。
- 龙鳞胫甲的 fear save `+3` 与 2 件套的 fear save `+3` 按 `add` 叠加；完整套装对 `frightened` 和 `dragon_frightful_presence` 的 trait 加值都是 `+6`。
- 龙鳞胫甲与 2 件套都可提供 dragon frightful presence 免疫；免疫是布尔语义，不重复增强。
- 龙鳞胸甲与 4 件套都提供 dragon breath 的 `half` mitigation tier；同一 tier 的多个运行时来源不会叠成四分之一伤害。

这会使完整套装成为高阶屠龙装备。不能通过漏实现效果来“平衡”，也不能把已经能从当前离散数值体系证明失真的数值推迟到实现后再处理；数值合同必须先通过本节的静态基线审计，再在功能完成后用独立战斗模拟微调。

### 2.2 详细阈值规则覆盖主题文案

`sets_06_to_10.md:228` 的主题文案写成“免疫对应龙种吐息伤害”，但 `set_bonus_design.md:273` 的详细规则是减免 50%。

采用详细规则：**获得 `half` mitigation tier，不获得 `immune`**。

### 2.3 非 canonical 字段的归一化

内容片段不是可以直接复制的当前 Resource：

- `max_hp` 必须改为现有 canonical 属性 `hp_max`。
- `resistance_fire/cold/lightning +N` 当前没有 AttributeSnapshot consumer，`N` 也不是当前抗性 schema 的合法量纲。旧内容中的 `15/5/10` 直接废止，不换算为百分比，也不换算为固定 DR。注意：其他套装设计稿中仍大量存在这类无 consumer 字段（如 `sets_01_to_05.md`、`sets_16_to_20_armor.md`），照搬会静默失效，后续套装落地时同样必须归一化。
- 当前正向“抗性”的 canonical 内容口径是对应 damage tag 的 `half` mitigation tier；只有文案明确写“免疫”时才使用 `immune`，弱点使用 `double`。单件和 2 件套的无条件抗性必须写入 trait 的 `damage_resistance_entries`。
- `cold` 对应当前 damage tag `freeze`。
- `saving_throw_fear +3` 定义为 fear save tag 加值，不伪装成普通 attribute。
- `base_price` 必须拆成当前 `ItemContentRegistry` 要求的买入/卖出价格字段（`buy_price` / `sell_price`）。

当前伤害管线中的抗性、固定减伤和豁免顺序是（实测 `BattleDamageResolver.DamageOutcome.cs:248-310`、`BattleDamageResolver.SaveBranch.cs:68-80`）：

```text
基础骰与加值
→ offense multiplier
→ mitigation tier（immune / half / double）
→ 固定 DR
→ 成功豁免的 half
→ 护盾与 HP 应用
```

`BattleUnitDamageResistanceState` 每个 damage tag 只保存一个 tier。race/subrace 按既有覆盖顺序写入，trait 被动只在候选 tier 更强时覆盖，当前强度序是 `immune > half > normal > double`（`BattleTraitPassiveProjectionService.cs:148-159`）。伤害结算时，unit resistance、状态和本方案新增的条件性装备 tier 一起参与最终选择：`immune` 优先，`half` 与 `double` 抵消，多份 `half` 仍只结算一次减半（`BattleDamageResolver.Mitigation.cs:96-110`）。

当前 `red_dragon` 的两种 fire breath 都是固定 `power=12`。头盔的 `fire=half` 会把 tier-adjusted damage 变成 `6`；豁免失败最终为 `6`，豁免成功再减半为 `3`。完整套装面对 fire breath 仍是 `6/3`，因为头盔、胸甲、2 件套和吐息条件来源的多份 `half` 不重复乘算。满套额外提供的是元素覆盖面、poison/acid 龙息的条件抗性和其他屠龙能力，不是更深一档的 fire 减伤。

### 2.4 TU 原生持续时间口径

战斗持续时间只使用 **TU**，不映射现实秒、分钟或帧数：

- `BattleTimelineState.TuGranularity=5`（`scripts/systems/battle/core/BattleTimelineState.cs:9`）只定义时间线的最小推进粒度。
- `BattleTimelineDriver.ApplyTimelineStep()`（`scripts/systems/battle/runtime/BattleTimelineDriver.cs:168`）按 TU 推进状态持续时间、施法进度和行动进度。
- `GameRuntimeFacade.BattleAutoAdvanceTickMsec=1000`（`scripts/systems/game_runtime/GameRuntimeFacade.cs:16`）只是外层自动调用的调度节奏；手动单位正在行动、模态状态或冻结状态都会阻止时间线推进，因此它不是游戏规则中的秒表。

龙血沸腾采用 `duration_tu=180`。这是一个纯玩法数值：以当前默认行动阈值 `BaseActionThresholdTu=40`（`scripts/player/progression/ActionCadenceContentRules.cs:12`，2026-08 已从 30 调整为 40）为内部参照，约等于 4.5 个基础行动进度阈值。不同单位的行动阈值和时间修正可以不同，所以 180 TU 是全局时间线窗口，不表示现实时间，也不保证穿戴者恰好行动若干次。

本切片不得新增 `SecondsToTu()`、`MinutesToTu()` 或任何现实时间换算 owner。`tests/battle_runtime/runtime/run_ravenplume_weapon_ability_regression.cs:310-311` 已使用“持续时间应为 60 TU”口径，不再声明现实时间含义；龙鳞套不依赖该数值。

### 2.5 10 级以后数值基线

套装不会作为低级装备投放。龙鳞套按角色 10 级以后的高阶/传奇装备审计；4–5 级幼龙、角色创建生命值和普通铁制装备只能验证机制，不能作为强度判定基线。龙鳞套究竟属于 10–12、15–17 还是 18–20 级档，仍需由获取内容明确。

当前角色成长与装备尺度如下：

- 基础 AC 是 `8`（`AttributeService.BASE_ARMOR_CLASS`）。龙鳞四件 AC 合计 `+11`，agility `12` 且受 `max_dex_bonus=1` 限制时最终 AC 为 `20`。现有传奇重甲方案中，大地守护者与圣光使者同为 AC `+11`，铁壁要塞达到 `+12`，所以不能按普通铁甲基线把龙鳞削成低阶护甲。
- full BAB 按 `Σ(rank × 4) / 8` 取整成长（`AttributeSnapshot.cs:144-157`），10/15/20 级分别为 `+5/+7/+10`；three-quarter 为 `+3/+5/+7`，half 为 `+2/+3/+5`。旧样例“10 级 full BAB = +10”不成立。
- 不计种族、技能百分比和装备，10 级角色在常见职业/体质组合下的期望 HP 大致为 `46–152`（低体质法师偏低、高体质狂战接近上限），20 级约为 `80–298`。胸甲 `hp_max +10` 在 10 级约占 `7%–22%`，20 级约占 `3%–13%`，属于正常固定增益。
- 当前已落地龙类只有 4 级白龙幼体、5 级绿龙幼体和 10 级红龙；没有 15 或 20 级龙类。红龙本身还掉落龙鳞材料，因此成套角色压制当前红龙只说明装备能克制已过阶段内容，不能证明套装超模。
- 当前 `dragon_breath_*` 六种吐息仍是固定 `power=12`。它们可验证 `half` 的结算顺序，但不能代表 15–20 级吐息伤害。

高阶龙尚未落地，不能先凭 BAB 单独指定一个看似精确的终局 AC。正式命中验收必须先定义“同等级、未穿龙鳞套角色”的完整基线攻击值：

```text
baseline_attack
= BAB
+ 同档普通装备 attack_bonus
+ 本次技能 attack_roll_bonus
+ 该档位预期常驻修正
```

同档龙类 AC 应使该 baseline 的常规命中率落在 `45%–55%`，也就是所需 D20 点数约为 `10–12`。在不触及自然 1/20 边界时，龙鳞常态 `+6` 将它提高到 `75%–85%`，龙血沸腾峰值 `+9` 提高到 `90%–95%`。这是高强度屠龙专精，但仍保留 baseline 的角色差异；最终是否调整必须等 10/15/20 级的普通装备攻击曲线与同档龙类 AC、攻击、吐息一起确定后再审计。

多段攻击不设置每次施放触发上限，也不按 cast、event batch、skill 或 effect ordinal 去重。平衡改由降低单段骰值完成：

| 场景 | 追加伤害 | 未计各段倍率的期望 |
|---|---:|---:|
| 护手单件近战命中 dragon | `1D4` | `2.5` |
| 四件套每次主直接伤害结算 | `1D4` | `2.5` |
| 满套近战单段 | `2D4` | `5` |
| 满套三段全部命中 | `6D4` | `15` |
| 满套九段全部命中 | `18D4` | `45` |

重复攻击已有的分段伤害倍率继续作用于本段套装骰；例如额外的终结段若有更高倍率，仍由 canonical 伤害管线统一处理。这个数值低于专用屠龙武器每次命中的 `3D6`/`2D8` 先例（`docs/content/weapons/by_family/swords_01.md:832`、`axes_01.md:404`；2D8 已有落地实现 `data/configs/equipment_abilities/dragonbone_pack.tres:86-98`），保留叠加空间而不让护甲取代武器定位。

因此本方案保留 AC `+11`、`hp_max +10`、离散 `half` 与 180 TU 主动；把完整近战单段由原 `+3D6` 降为 `+2D4`。峰值攻击 `+9` 等同档龙类曲线补齐后再冻结，不能使用当前幼龙或红龙直接削值。

## 3. 最终行为合同

### 3.1 四件物品

| 物品 | 基础属性 | 单件能力 | 实现口径 |
|---|---|---|---|
| `armor_dragon_scale_head` | AC `+2` | `fire=half`；对 dragon 攻击检定 `+2` | AC 走现有 modifier；抗性走 trait `damage_resistance_entries`；攻击条件走 binding |
| `armor_dragon_scale_body` | AC `+7`；`hp_max +10` | `fire/freeze/lightning=half`；dragon breath 获得 `half` tier | 无条件抗性走 trait；条件性吐息 tier 走 binding；`hp_max` 换装下降时沿现有规则 clamp 当前 HP |
| `armor_dragon_scale_hands` | AC `+1`；全局 `attack_bonus +2` | 每次近战武器命中 dragon 时额外 `+1D4`，伤害 tag 继承主武器伤害 | 复用现有 weapon-hit bonus dice；重复/连锁攻击逐次查询 |
| `armor_dragon_scale_feet` | AC `+1` | fear save `+3`；免疫 dragon frightful presence | 新增 save-tag bonus producer；免疫只匹配 canonical 龙威 tag |

装备损坏到无效状态或从 battle-local equipment view 移除时，不再贡献套装件数和单件能力（沿用已落地口径：耐久 `<= 0` 不计件数，`GearSetEvaluationService.cs:263`）。多占位装备按一个 `EquipmentEntry` 计算一次。

### 3.2 2 件套：龙之威慑

任意两件有效龙鳞装备：

- `fire=half`。
- fear save `+3`。
- 免疫 dragon frightful presence。

阈值是累计的；4 件时仍保留 2 件套效果。胫甲与 2 件套都激活时，两个 `add` 来源分别保留并合计为 fear save `+6`；龙威 immunity 仍只产生一次布尔结果。

### 3.3 4 件套：屠龙者之誓

四件全部有效：

- 对 `creature_type=dragon` 的攻击检定 `+2`。
- 对 dragon 的每次实际主直接伤害结算额外 `+1D4`。
- 受到 dragon 的 breath weapon 时，该伤害 segment 获得 `half` mitigation tier。
- 获得每日一次主动技能“龙血沸腾”。

“每次实际主直接伤害结算额外 `+1D4`”采用以下可测试定义：

- 适用于武器攻击、非武器法术攻击和豁免型直接伤害。
- 粒度是“每次实际执行主 `CombatEffectDefinition`、每个目标一次”。同一个 effect 被 fixed repeat、repeat-until-fail、random chain 或其他多段攻击重新执行时，每个成功进入伤害结算的段都再次触发。
- 不按 cast、event batch、skill id、`SourceEffectOrdinal` 或目标做跨段去重，也不设置每次施放触发上限；`SourceEffectOrdinal` 只保留 provenance。
- `extra_damage_segments` 不再次触发。
- timeline/upkeep、地形 tick、反射伤害、装备能力产生的二次伤害或触发技能、自伤不触发。
- 额外骰继承当前主 effect 的 canonical damage tag，进入本段相同的 pre-resistance multiplier、save 和 mitigation 管线；`CriticalHit` 可供条件判断，但不会让套装 `1D4` 再额外掷一次。
- equipment bonus 自身不得递归再次查询套装加骰；降低骰值不能代替结构化的 origin 防递归。

这与当前只在 `AttackSuccess && includesWeaponDamage` 时调用的 `CollectBonusDamageDiceOnHit()` 不同；后者不能直接冒充完整实现。

完整套装的近战命中同时获得护手 `1D4` 和四件套 `1D4`，合计 `2D4`。三连击全部命中时触发三次、共 `6D4`；九段全部命中时触发九次、共 `18D4`。如果技能还有独立的第十个终结攻击，该攻击也单独触发，并继续服从自己的分段倍率。

### 3.4 龙血沸腾

新增装备授予技能，使用独立 ID：

```text
equipment_dragon_scale_dragon_blood_boil
```

不能复用现有 `warrior_dragon_blood_boil`（`data/configs/skills/warrior_dragon_blood_boil.tres`，战士职业技能，“2 回合每损失 10% HP 获得斗气但受治疗 -15%”），两者同名不同义。

行为：

- 自身目标，消耗 1 AP，不消耗 MP/体力。
- `per_world_day`，每个锚点实例每天最多 1 次（usage 语义见 §9）。
- 持续 180 TU。
- 持续期内，对 dragon 的攻击检定额外 `+3`；与单件头盔和 4 件套加值相加。
- 状态初始带 3 层治疗 charge。
- 每次真实攻击检定命中后，自身恢复 `1D6 HP` 并消耗 1 层；任意目标都可触发，豁免型伤害不算“命中”。
- 命中后即消费 charge；即使目标最终被完全减伤或使用者已满血，也不返还该层。
- miss、preview、AI 估值和无执行的 command validation 不消费 charge。
- 4 件套 source 在战斗中失效时，立即移除该 buff（经 §8.7 的 opt-in 清除）；当天次数不返还，同日重新穿齐也不能再次使用。

## 4. 架构裁决（2026-08-16 已定）

初版 §4 的方案比较基于 2026-08-05 基线，其结论与 2026-08-12 凤凰涅槃落地的系统相反。经评审裁决，本切片**沿用已落地范式**，不重做基础设施：

| 裁决点 | 结论 | 理由 |
|---|---|---|
| 成员归属模型 | **set 侧 `GearSetDef.member_item_ids` 为唯一成员真相**；不引入 `ItemDef.gear_set_id` 与双向索引 | 已落地并被 `docs/design/progression/equipment_sets.md`、context units、phoenix 内容与回归制度化；集中校验（成员存在性、阈值可达性、anchor ⊆ members）天然需要全集视图；每套装一个 .tres，无共享 merge hotspot；改为 ItemDef 字段是无行为收益的全链路 churn |
| 每日/周期 usage owner | **锚点装备实例的 `ability_usage_periods`**（`UsageAnchorItemId` 配置，回退首件有效成员） | 已落地、有回归、随实例既有 schema 序列化，无存档变更；battle writeback/preview/AI 统一走实例 id 路径。语义为“次数跟装备走”，见 §9 的合同与已知边界 |
| source 失效 buff 清理 | **opt-in**：authoring 增加 `remove_on_source_deactivated` 标记，仅声明该标记的 buff 在 source 失效时清除 | 凤凰祝福“source 移除后经 `BattleStatusDerived` 独立存活”是刻意设计并有回归锁死；全局清除会推翻已验收语义。龙血沸腾单独声明 opt-in |

旧方案比较表中“沿用 tag + GDScript registry”“把套装能力锚定某件实例作为成员关系载体”“每件装备各挂一份阈值能力”仍维持拒绝结论，理由不变。

## 5. 内容模型与注册链（已落地 + 本切片增量）

### 5.1 已落地 schema

authoring Resource（`scripts/player/progression/gear_sets/`）：

```text
GearSetDef
- gear_set_id: StringName
- display_name / description
- member_item_ids: Array<StringName>        # 唯一成员真相
- usage_anchor_item_id: StringName          # 周期 usage 锚点，须为成员
- thresholds: Array<GearSetThresholdDef>

GearSetThresholdDef
- threshold_id: StringName
- required_piece_count: int
- display_name / description
- mandatory_member_item_ids: Array<StringName>
- attribute_modifiers: Array<AttributeModifierDef>
- granted_trait_ids: Array<StringName>
```

`GearSetThresholdDef` **只**通过 `attribute_modifiers` 与 `granted_trait_ids` 暴露战斗能力，无 `skill_id`、`granted_action_ids` 或其他直接授予动作字段（schema 级保证）。主动技能的唯一链路是：

```text
active threshold
→ granted trait
→ 允许 source_kind=gear_set_threshold 的 equipment ability binding
→ EquipmentGrantedActionDef
→ equipment_dragon_scale_dragon_blood_boil
```

已落地校验（`GearSetContentRegistry.cs`）：空/重复 set/threshold ID、非正/非严格递增阈值、阈值超过成员数、成员引用不存在的 item、stackable/非装备成员、usage anchor 必须是成员、mandatory 成员 ⊆ 成员表且不超阈值、不存在 trait、threshold trait 必须允许 `gear_set_threshold` source、直接属性与 granted trait 属性的 `attribute_id` 冲突拒绝、`per_world_day` action 在满套前阈值要求 anchor 为 mandatory。

### 5.2 本切片需要补齐的 registry 校验

> 状态（2026-08-16）：以下通用校验已落地于 `GearSetContentRegistry.ValidateTyped`（跨 set 成员唯一性、binding source-kind 匹配、modifier 属性域、锚点唯一获取约束），回归见 `tests/equipment/run_gear_set_evaluation_regression.cs`。剩余项：Dragon 四件 slot 覆盖与 4 件阈值可达性属于内容级校验，随龙鳞内容切片落地。

当前 registry 尚缺以下校验，随龙鳞内容一并补齐（通用能力，非龙鳞专用分支）：

- 跨 set 成员唯一性：同一 item 归属多个套装时拒绝（当前无全局去重）。
- binding / `EquipmentGrantedActionDef` 存在性：threshold trait 引用的 binding 与 granted action 悬空时拒绝（当前 binding 仅在 `GrantsPerWorldDayAction` 扫描中间接出现）。
- binding 不显式允许 `gear_set_threshold` source 时拒绝。
- modifier 值域与 consumer 存在性校验。
- 套装成员物品的获取唯一性约束或“同 id 多实例”防护（配合 §9 锚点账本边界）。
- Dragon 四件分别落到 head/body/hands/feet、4 件阈值在合法槽位组合中可达（内容级校验）。

### 5.3 接入链（已落地，无需改动）

- `ProcessContentHost.cs:294-296` → `ContentSnapshotBuilder`（构造 registry `:33`、`ValidateTyped` `:93-100`、seal 前加载）。
- `ContentSnapshot.cs:31` / `GameContentCatalog.cs:182`（`GetGearSetDefinitionsTyped()`）。
- `tests/shared/SyntheticContentSnapshotFactory.cs:65`。

生产 runtime 只消费 immutable definitions，不加载 raw `GearSetDef`。龙鳞内容只需新增 `data/configs/gear_sets/dragon_scale_set.tres` 并过 registry 校验。

## 6. 计数与属性投影（已落地）

唯一套装计算 owner 已存在：`GearSetEvaluationService.Evaluate(EquipmentState, items, sets) -> GearSetEvaluationSnapshot`（`scripts/systems/inventory/GearSetEvaluationService.cs:135-139`），typed DTO 含 `ActiveSets / AttributeModifiers / DerivedTraitInstances`、`GearSetActivationSummary`、`GearSetThresholdStatus`。

已落地规则：

1. 遍历 `EquipmentState.GetEntrySlotIdsTyped()`（`EquipmentState.cs:132-142`），entry 粒度。
2. 每个有效 `EquipmentEntry` 只计算一次；耐久 `<= 0` 不计；同 item_id 去重。
3. 从 set 侧 `MemberItemIds` 归组（不是 ItemDef 字段）。
4. entry slot 必须在 item 允许槽位内，且实际占用槽位等于 canonical footprint，否则不计。
5. 按 `required_piece_count <= count` 累计激活阈值；`mandatory_member_item_ids` 参与激活判定。
6. 输出稳定排序；纯静态函数，不缓存可变 `EquipmentState`，不读全局 PartyState；换装时从当前 view 重算。

属性投影（已落地，形态与初版提案不同）：`CharacterManagementModule` 把 `gearSetEvaluation.AttributeModifiers` 合并进 `AttributeSourceContext.equipment_state`（`CharacterManagementModule.cs:566-587`），provenance 由 `GearSetDefinition.FromResource` 强制盖戳 `source_type="gear_set"` / `source_id="gear_set::<set>::<threshold>"`（`GearSetDefinition.cs:126-135`），有回归锁定。**不新增独立 `gear_set_attribute_modifiers` 通道**；provenance 靠 source 标签区分，不伪装成普通 equipment modifier。

装备 requirement preview 继续使用“移除旧件、尚未放入新件”的 view，禁止新套装奖励反过来自我满足穿戴条件。

## 7. Battle source 与换装（已落地）

### 7.1 source kind 与 identity

已落地：

- `TraitSourceKind.GearSetThreshold`（`scripts/player/progression/TraitContentRules.cs:69`，字符串 `gear_set_threshold`）。
- `EquipmentAbilitySourceKind.PlayerPersistentGearSetThreshold`（`scripts/systems/battle/core/BattleEquipmentAbilitySourceState.cs:13`）。
- source schema 为 `source_kind + effective_instance_key + equipment_def_id + source_equipment_instance_id + ability_ids` 的 discriminated 形态，codec 严格（字段数、按 kind 校验：gear-set source 要求锚点实例 id 非空）。

stable source key：`gear_set::<gear_set_id>::<threshold_id>::<trait_id>`（`GearSetEvaluationService.cs:218-229`）。**保留现有字段名**，不做初版提案的 `source_definition_id`/`source_instance_id` 改名。

### 7.2 投影路径

已落地路径（与初版提案的 battle 内评估不同，效果等价）：

1. world 侧 `CharacterTraitService.CollectGearSetThresholdTraits`（`scripts/systems/progression/CharacterTraitService.cs:179-209`）调用 `GearSetEvaluationService`，把 active threshold 的 traits 作为 `GearSetThreshold` effective traits。
2. battle 建厂时经 effective trait 投影进入 unit。
3. `BattleEquipmentAbilityProjectionService` 在既有循环中接受 `GearSetThreshold` kind、匹配声明允许 `gear_set_threshold` 的 bindings、盖戳 `PlayerPersistentGearSetThreshold`、生成唯一 ability source 与 temporal modifier（锚定到 anchor 实例 id）。

约束保持：不把四件装备各投影一份阈值能力；不在 battle runtime 判断 `dragon_scale_set`、具体 item ID 或 binding ID。锚点实例不在 battle-local EquipmentState 中时该 source 直接丢弃。

### 7.3 战斗内换装

已落地：`BattleChangeEquipmentResolver` 先在 duplicate view 上 apply（失败不落状态），成功后 `RefreshEquipmentProjection`（`BattleUnitFactory.cs:394-461`）在同一 candidate state 中原子重算：属性快照、effective traits（含套装）、equipment ability sources、target-mark 清理、trait charge reconcile、武器投影、已知技能、passive statuses；HP/MP/体力/灵气只在新上限变小时向下 clamp。

**唯一缺口（已补齐）**：source-bound buff 清理原先只覆盖 target marks，没有“清除由失效 gear-set source 施加的 status”的通用路径——已由 §8.7 的 opt-in 机制落地（2026-08-16，`run_equipment_source_bound_status_cleanup_regression.cs`）。失败的换装不能留下半套属性、stale source 或已删除的 buff。

## 8. 战斗 ABI 缺口

### 8.1 当前可直接复用

| 效果 | 现有能力 |
|---|---|
| 无条件元素抗性 | trait `damage_resistance_entries` → `BattleUnitDamageResistanceState` |
| 固定 DR（与抗性 tier 是不同系统） | `damage_reduction(amount, damage_tags)` |
| 对 dragon 攻击加值 | `creature_type_tags contains dragon` + `attack_roll_bonus` |
| 护手近战 `+1D4` | 现有 weapon-hit `add_damage_dice` |
| 每日授予技能 | `EquipmentGrantedActionDef` + `PerWorldDay` |
| `1D6` 治疗 | `HealActionPayloadDef` |
| 三次上限 | status stacks + `ConsumeStatusStacksActionPayloadDef` |
| `has_status` 条件 | `BattleEquipmentAbilityConditionEvaluator` |

现有龙鳞战斧内容已经同时证明两条链路：`fivefold_scales` trait（`data/configs/traits/weapon_battleaxe_dragon_scale_fivefold_scales.tres`）通过五个 `damage_resistance_entries` 授予 fire/lightning/poison/acid/freeze 的 `half`，equipment ability pack（`data/configs/equipment_abilities/battleaxe_dragon_scale_pack.tres:137-141`）使用 `amount=2` 的固定 DR。两者是独立系统。该内容不提供套装阈值、条件性龙息 tier、任意直接伤害加骰或套装级 usage owner。

### 8.2 无条件元素抗性：直接复用现有 tier owner（已验证）

authoring schema 已足够：

```text
TraitDamageResistanceEntryDef
- damage_tag
- mitigation_tier
```

`TraitContentRegistry.cs:213-254` 已校验 damage tag、tier closed domain 和同一 trait 内重复 tag；`BattleTraitPassiveProjectionService.cs:115-160` 已把有效装备 trait 投影到 `BattleUnitDamageResistanceState`，并使用 `immune > half > normal > double` 的 stronger-only 规则。正式执行、canonical preview 和 AI（经 canonical preview 间接）已经共同读取同一 mitigation 路径，不需要新增百分比 action、query、事件字段或存档字段。

Dragon Scale 新增三个无条件 trait：

- 头盔：`fire=half`。
- 胸甲：`fire=half`、`freeze=half`、`lightning=half`。
- 2 件套 threshold trait：`fire=half`。

相同 tag 的多份静态 trait 最终仍只在 unit map 中保留一个最强 tier，不累加、不连续减半。当前 map 和伤害报告只暴露 `damage_resistance_<tag>` 这一 canonical source，不保存头盔/胸甲/阈值的逐 trait provenance；套装 UI 仍可从 gear-set evaluator 分别显示单件与阈值来源。精确的静态抗性来源追踪属于通用 resistance owner 的独立增强，不是龙鳞套正确结算所需的新接口。

### 8.3 条件性 mitigation tier（真实缺口）

当前 `damage_reduction` 只返回固定整数 Amount：

- `EquipmentAbilityAuthoringDefs.cs:240-246`
- `scripts/systems/battle/core/BattleEquipmentAbilityReactionContracts.cs`
- `BattleDamageResolver.DtoHelpers.cs:139-191`

`IBattleEquipmentDamageQuery` 当前只有 4 个 query（`CollectBonusDamageDiceOnHit`、`ResolveDamageRollModeOverride`、`CollectDamageReductions`、`CollectMitigationAuras`）。`CollectMitigationAuras` 虽返回 tier，但 context 只有 `TargetUnit/BattleState/DamageTag`，无 SourceUnit/SkillId/SaveTag/EffectCategories/DamageOriginKind，不能冒充本需求。

新增到 `IBattleEquipmentDamageQuery`：

```text
CollectMitigationTiers(BattleEquipmentAbilityMitigationTierContext)
-> IReadOnlyList<BattleEquipmentAbilityMitigationTierResult>

Context:
- SourceUnit
- TargetUnit
- BattleState
- SkillId
- SaveTag
- EffectCategories
- DamageTag
- DamageOriginKind

Result:
- BindingId
- ActionId
- MitigationTier
- Label
```

新增 typed action，例如：

```text
grant_mitigation_tier
- target_selector
- mitigation_tier
- damage_tags
- label
```

Dragon Scale 条件：

- holder 的 source active。
- attacker `creature_type_tags contains dragon`。
- `save_tag == dragon_breath` 或 resolved category 包含 `breath_weapon`。
- 当前 damage tag 是 fire/freeze/poison/acid/lightning。

返回 `half`，并在 `ResolveMitigationTierResult()`（`BattleDamageResolver.Mitigation.cs:18-115`）选最终 tier 之前与 status/unit resistance source 一起聚合。这样保持当前规则：immune 优先，half 与 double 抵消，多份 half 不重复乘算。

`DamageResolutionContext` 已持有 `SkillId`（`DamageResolutionContext.cs:16`），但当前 reduction context 会丢失它（`DtoHelpers.cs:160-169`）；必须贯通正式执行和 preview，不按 skill ID 猜测吐息。

### 8.4 每次主直接伤害结算 `+1D4`（真实缺口）

当前 `CollectBonusDamageDiceOnHit()` 在以下条件前不会运行：

```text
AttackSuccess == true
&& resultIncludesWeaponDamage
```

见 `BattleDamageResolver.Dice.cs:82-112`（gate 在 `:91-99`）与调用方 `BattleDamageResolver.DamageOutcome.cs:175-183`（weapon gate 为 `!DicePoolRollIsEmpty(weaponRoll)`）。因此它不覆盖纯法术、豁免伤害和非武器 attack spell。

将 `IBattleEquipmentDamageQuery` 增加通用 query：

```text
CollectBonusDamageDiceForEffect(
    BattleEquipmentAbilityDirectDamageContext
)

Context:
- SourceUnit
- TargetUnit
- BattleState
- SkillId
- SaveTag
- EffectCategories
- PrimaryDamageTag
- DamageOriginKind
- SourceEffectOrdinal
- IsMainDirectEffect
- IncludesWeaponDamage
- HasAttackCheck
- AttackSucceeded
- CriticalHit
```

query 是纯读取：canonical resolver 每次准备结算一个主 `Damage` effect 时调用一次，不保存 cast/event 去重集合。`HasAttackCheck && !AttackSucceeded` 时不触发；没有攻击检定的豁免型主直接伤害仍可触发。fixed repeat、repeat-until-fail 和 random chain 每个独立段都会重新进入 resolver，因此自然逐段查询。`SourceEffectOrdinal` 只用于 provenance 和诊断，不是跨段唯一键。

`DamageOriginKind` 是新增的 closed typed domain，必须能 fail-closed 地区分正常主技能/攻击伤害与 timeline/upkeep、terrain、reflection、self damage、equipment bonus、equipment direct reaction、equipment trigger-skill/immediate-attack 等排除来源。不得通过具体 skill、item、binding id 或当前调用深度猜测 origin。

`AddDamageDiceActionPayloadDef`（`EquipmentAbilityAuthoringDefs.cs:146-160`）新增 closed mode：

```text
damage_type_mode = explicit | inherit_primary
```

Dragon 4 件套使用 `inherit_primary`。validator 要求：

- `explicit` 必须提供合法 `damage_type`。
- `inherit_primary` 禁止同时提供冲突的显式 damage tag。
- origin 必须是 main direct effect。
- equipment bonus/extra segment 不得递归。
- 四件套 payload 固定为 `dice_count=1, dice_sides=4`；`CriticalHit` 可参与条件，但不会复制这颗装备骰。

现有 weapon-hit query 保持原语义，护手通过它为每次成功的近战武器攻击追加 `1D4`；新 direct-effect query 只承载四件套的通用 `1D4`，不通过删掉 weapon gate 来意外扩宽所有旧装备能力。两条 query 在同一近战主伤害段都命中时数值相加为 `2D4`。

### 8.5 fear save bonus 与龙威免疫（真实缺口）

当前有两类相关 consumer：

- `BattleSaveResolver.cs:226-229`（总加值公式）、`:405-478`（save tag state 收集）、`:774-802`（status save bonus，Math.Max 语义）。
- `BattleUnitSaveModifierState` 已持有 unit-level save ability bonus 与 immunity tags（`BattleUnitSaveModifierState.cs:107-112`，无 BonusByTag）。
- `BattleStatusEffectState.save_bonus_by_tag`、`save_immunity_tags`。

其中 status 的 `save_bonus_by_tag` 按既有 `Math.Max` 语义处理临时状态，不能拿它承载两个应相加的永久 trait。新增 trait authoring：

```text
TraitSaveTagBonusEntryDef
- save_tag
- bonus
- stack_mode: add | highest

TraitSaveTagBonusEntryDefinition
```

`stack_mode` 使用 closed enum/converter，规则唯一且确定：

- `add`：同一 save tag 的所有 `add` 贡献用 checked integer 加法求和。
- `highest`：同一 save tag 的所有 `highest` 贡献只取最高正值。
- 同时存在两类来源时，最终 trait bonus 为 `add` 总和加 `highest` 最高值。
- registry 要求 `bonus > 0`，并拒绝单个 trait 内重复的 `(save_tag, stack_mode)`。

Dragon 胫甲与 2 件套两个条目都声明 `stack_mode=add`，所以两者同时有效时得到 `+6`，不能被 `Math.Max` 吞掉一份。

把条目加入 `TraitDef` / `TraitDefinition`。`BattleTraitPassiveProjectionService` 在 effective traits 刷新时统一聚合，扩展 `BattleUnitSaveModifierState`、read view 和 mutation snapshot，新增 typed `BonusByTag`；它与 `BonusByAbility` 一样属于 battle-local unit owner，不伪装成 passive status。`BattleSaveResolver` 的总加值合同是：

```text
ability save bonus
+ unit BonusByTag（全部 trait 按上述 stack_mode 聚合）
+ transient status save bonus（保持现有 Math.Max 语义）
```

正式执行、preview 和 AI 都从该 read view 读取。`DuplicateState()`、raw capture/restore、AI stable projection 与 mutation-exact 必须覆盖 `BonusByTag`。不能把 fear 写成普通 attribute 后假设 `BattleSaveResolver` 会读取，也不能改变现有临时 status 的 max 叠加语义。

`BattleSaveContentRules` 是 save tag 的唯一 closed-domain owner（`scripts/player/progression/BattleSaveContentRules.cs`；enum 在 `:18-42`，`frightened` 已是 control tag）。新增：

```text
BattleSaveTagKind.DragonFrightfulPresence
dragon_frightful_presence
```

必须同时完成 `StringName` 常量、`ToSaveTagKind()`（`:136-181`）、`ToStringName()`（`:221-248`）、`IsValidSaveTag()`（`:109-110`）和 `IsControlSaveTag()`（`:115-122`）双向映射；龙威导致 `frightened`，因此它属于 control save tag。`SaveTagListContentRules`（复用 `IsValidSaveTag`，无第二份 whitelist）、trait/skill/enemy validator 都复用该 owner。

再新增真实技能：

```text
dragon_frightful_presence
- source: dragon
- save_tag: dragon_frightful_presence
- save failure: frightened
```

红龙 template 和 `dragon_tyrant` AI 必须实际使用该技能（当前 `dragon_tyrant.tres` 的 engage/pressure 动作表无任何恐惧 producer；“免疫龙威”在落地前没有对抗对象）。胫甲与 2 件套的 immunity 只包含 `dragon_frightful_presence`，不能用 `frightened` 粗暴免疫所有恐惧。fear `+3` trait 同时覆盖通用 `frightened` 和 `dragon_frightful_presence` save tag。

### 8.6 通用 attack-hit reaction（真实缺口）

当前 after-hit sink 在 `!ResultIncludesWeaponDamage(result)` 时直接返回，见 `BattleDamageResolver.cs:793-801`。因此龙血沸腾不能完整覆盖非武器法术攻击命中。

新增 typed trigger：

```text
on_attack_hit / after_hit
```

它在真实攻击检定成功后每目标触发一次，不要求 weapon damage；携带 skill/effect origin 和 event batch。龙血沸腾使用：

1. `has_status(dragon_blood_boil)` 条件。
2. 消耗 1 层自身 status。
3. 自身治疗 `1D6`。

旧 `on_hit` 保持 weapon-hit 语义，避免无意扩大现有武器内容。

### 8.7 opt-in source-bound buff 清除（战斗 ABI 已落地 2026-08-16；龙血沸腾内容接入待后续切片）

现状：`BattleStatusEffectState`（`scripts/systems/battle/core/BattleStatusEffectState.cs:110-177`）没有 source kind / effective source key / binding provenance 字段；全仓无 `remove_on_source_deactivated`。已落地的两套相邻机制：

1. target mark 的 `RemoveOnSourceMissing` 清理（`BattleEquipmentTargetMarkResolver.cs:618-658`，挂接在换装重投影后）。
2. `BattleStatusDerived` source 让声明 `ActivationStatusId` 的 buff 在源装备卸下后独立存活（`BattleEquipmentAbilityRuntimeService.cs:3465-3494`）——这是凤凰祝福的刻意设计，有回归锁定，不得改变。

新增 opt-in 机制：

- authoring 增加 `remove_on_source_deactivated: bool`（默认 `false`）。
- status 投影时记录 provenance：source kind、effective source key、binding/granted action id。
- source 刷新时只清理**声明了该标记**且 provenance 匹配失效 source 的 status；不得仅靠 status ID 全局删除，也不得让同名其他来源互相覆盖。
- 该字段属于 battle-local 状态：`DuplicateState()`、preview candidate、AI stable projection 与 mutation-exact 必须覆盖；不进入世界存档。

龙血沸腾声明 `remove_on_source_deactivated=true`：4 件套 source 失效时立即移除 buff。凤凰祝福不声明，维持现状。

## 9. 每日用量 owner、存档与 writeback（已落地范式 + 语义合同）

### 9.1 usage owner：锚点实例账本

沿用已落地方案：非 per-battle usage 通过 `SourceEquipmentInstanceId` 查找锚点实例并写入其 `ability_usage_periods`（`EquipmentAbilityUsageRuntime.cs:621-639`；账本状态定义在 `scripts/player/warehouse/EquipmentInstanceState.cs:62`）。锚点由 `usage_anchor_item_id` 配置、回退首件有效成员（`GearSetEvaluationService.cs:187-192`）。世界日 index 继续使用 `EquipmentAbilityUsageRuntime.ResolvePeriodIndex()`（`:772-779`），不另算日历。

**语义合同：次数跟装备走，不跟人走。**

- 每个锚点实例、每个世界日独立；卸下再穿回同一实例不重置。
- 套装转给另一成员后，该成员看到的是装备剩余次数（装备的魔法充能语义）。
- 阈值失效（如锚点耐久归零）关闭能力，但不删除账本；修复后恢复。
- source 失效当天次数不返还，同日重新穿齐不能再次使用。

**已知边界**：玩家持有两件同 id 的锚点物品（不同实例）时，换上另一件会得到该实例的空账本，相当于当天多用一次。缓解措施：§5.2 的 registry 校验约束套装锚点成员的获取唯一性；龙鳞套的获取内容按高阶稀少装备设计。

### 9.2 持久化边界

三层边界：

1. **世界存档**：usage 随锚点实例的 `ability_usage_periods` 进入既有 `equipment_state` 序列化路径（`PartyState.SaveSnapshot.cs:386`）。**无新增顶层 set usage key，无 SaveVersion bump**；`SaveVersion` 保持 18，本切片无旧档兼容问题。套装件数和 active threshold 始终派生，不写入 `PartyState`。
2. **单场战斗生命周期**：derived gear-set source、龙血沸腾 buff（含 §8.7 provenance）和三层治疗 charge 必须完整进入 battle-local `BattleUnitState` 的 duplicate、preview candidate、AI snapshot 与 mutation-exact，直到到期、source 失效或战斗 teardown。它们不是“无需保存的临时变量”。
3. **战斗结束 writeback**：沿用现有原子链路（clone candidate `GameRuntimeBattleWritebackService.cs:196`、equipment writeback `:271-298`、失败不 apply）。derived source、buff 和 charge 不复制到 `PartyState`，战斗 teardown 后丢弃。

当前系统没有战中存档恢复合同，因此本切片不新增 battle-state 世界存档 codec；若未来增加战中存档，必须另行把这些 battle-local owner 纳入严格 codec，不能从 Party 装备重新猜测剩余 buff/charge。

## 10. UI、headless 与可观察性（已落地 2026-08-16）

已落地：

- Party 装备页套装摘要：`PartyManagementWindow.cs:730`（`_append_gear_set_summary`），经 `CharacterManagementModule.EvaluateGearSets` 与共享投影 `GameRuntimeCharacterInfoBuilder.BuildGearSetEntries`（`:306`），显示 `N/M件`、每阈值 `[已激活]/[未激活]` 与描述；有 UI 回归（`tests/world_map/ui/run_party_management_window_regression.cs`）。
- 战斗人物信息：`BuildBattleCharacterGearSetEntries`（`:286`）使用 battle-local `unit.GetEquipmentView()`，不复用入场前 Party snapshot。
- granted action 可见性：typed DTO `GearSetGrantedActionSummary`（GrantedActionId/SkillId/DisplayName/UsagePeriodKind/MaxUsesPerPeriod/IsAvailable/RemainingUses/DisabledReason）与共享投影 `GearSetGrantedActionProjection`（`scripts/systems/game_runtime/GearSetGrantedActionProjection.cs`），沿 active threshold → trait → binding → granted action 链，持久周期剩余次数读锚点实例 `ability_usage_periods`；Party 页传世界装备视图，battle 侧传 battle-local view。
- headless snapshot 链接入：`IGameRuntimeSnapshotSource` 新增 `GetMemberGearSetEvaluationTyped` / `GetMemberGearSetGrantedActionSummariesTyped`，`GameRuntimeSnapshotBuilder` 输出 party 成员 `gear_sets` 字段，`BattleHudAdapter`/`BattleHudSnapshot` 输出 `gear_set_summaries`（件数 N/M、阈值激活态、granted actions 含 remaining uses 与 disabled reason），稳定排序；`tests/shared/SnapshotTestRuntime` 实现同一 typed 投影。回归：`run_party_management_window_regression.cs`、`run_dragon_scale_dragon_blood_boil_regression.cs`、`run_game_runtime_snapshot_builder_regression.cs`。

要求保持：

- Party UI 消费当前 CharacterManagement equipment view；Battle HUD 消费 battle-local unit equipment view。
- 显示 `2/4`、已激活/未激活阈值、缺少件数、每日技能剩余次数和 disabled reason。
- 伤害报告继续区分 mitigation tier 与固定 DR。静态抗性沿用 `damage_resistance_<tag>` source；条件性 dragon-breath tier 保留 binding/action provenance，因此可以区分胸甲与 4 件套的条件来源，但不虚构头盔/胸甲/2 件套的静态逐 trait provenance。
- 不向 UI 输出 raw Resource 或 `Array<Dictionary>`。

## 11. Preview、AI 与 reaction ordering

完整实现必须保证：

- preview 调用与正式执行相同的 set evaluator、attack bonus、逐次主直接伤害加骰、`BattleUnitDamageResistanceState`、条件性 mitigation tier 和 save modifier owner。
- preview、hover、AI scoring 不消费每日次数或三层治疗 charge。
- battle-local clone、AI stable projection 与 mutation guard 覆盖锚点实例 usage、derived source、`BonusByTag`、§8.7 status provenance、龙血沸腾 buff 和 charge。
- `add_damage_dice` / 新 per-main-direct-effect handler 显式声明 preview 与 AI support；当前 built-in spec 默认不包含这项能力，不能只因为 runner PASS 就假定已覆盖。AI 期望值按预计实际主伤害结算次数累计，不把多段技能固定算成一次。
- AI 能看见龙血沸腾的剩余次数、180 TU buff、对 dragon 的期望攻击收益和最多三次 `1D6` 治疗。

同步结算顺序：

```text
set source / trait refresh
→ attack-check modifier query
→ attack resolution
→ 每个实际主 direct damage effect 查询并合入 bonus dice
→ mitigation tier
→ fixed DR
→ save resolution
→ shield / HP commit
→ successful-attack-hit reactions（治疗并消费一层）
→ 后续 kill / nested reactions
```

新增 query 只能读状态；所有消费和 status 变更留在 canonical commit/reaction sink。不得让 query、preview 或 AI evaluator 发生写入。

## 12. End-to-end ABI 矩阵

| 层 | 当前 owner | 当前状态（2026-08-16 核实） | 龙鳞落地要求 |
|---|---|---|---|
| 物品 authoring | `ItemDef` | 四件资源不存在；ItemDef 无 set 字段（按裁决保持） | 四件真实资源 + `member_item_ids` 引用 |
| 套装 authoring | `GearSetDef` / threshold Resource | **已落地**（含 anchor/mandatory 字段） | 新增 `dragon_scale_set.tres` |
| 内容注册 | `GearSetContentRegistry` / `ContentSnapshotBuilder` | **已落地**；校验缺口见 §5.2 | 补齐跨 set 唯一性、binding/action 存在性、modifier 值域校验 |
| immutable snapshot | `ContentSnapshot` / `GameContentCatalog` | **已落地** | 无改动 |
| 计数 | `GearSetEvaluationService` | **已落地**（entry 纯计算、耐久/槽位/footprint 规则） | 无改动 |
| 属性 | `PartyEquipmentService` / `AttributeService` | **已落地**（source-tagged 合并进 `equipment_state`） | 无改动 |
| trait/source | effective traits / equipment ability source | **已落地**（`GearSetThreshold` / `PlayerPersistentGearSetThreshold`，锚点实例） | 无改动 |
| 攻击加值 | equipment attack query | 已有 | 复用，新增 set source 内容 |
| 单件近战加骰 | weapon-hit damage query | 已有 | 复用 |
| 每次主直接伤害结算加骰 | 无 | 当前仅 weapon hit | per-main-direct-effect context、inherit-primary、typed origin 防递归；不做 cast/event 去重 |
| 无条件元素抗性 | `TraitDamageResistanceEntryDef` / `BattleUnitDamageResistanceState` | **已落地**（离散 tier、stronger-only、装备 trait 投影） | 单件/阈值 trait 写 `half` |
| 龙息半伤 | mitigation tier owner | 无条件性 equipment tier query | `CollectMitigationTiers()` |
| fear +3 | `BattleUnitSaveModifierState` / `BattleSaveResolver` | 只有 ability bonus 与 transient status max | typed `BonusByTag`、`add/highest` 聚合；完整套装 `+6` |
| 龙威免疫 | save immunity | 可按 tag 免疫，producer 不存在 | canonical `dragon_frightful_presence` + 真龙技能 + AI 使用 |
| 每日主动 | granted action + 锚点实例账本 | **已落地**（凤凰回归锁定） | threshold → trait → binding → action 内容 |
| 三次治疗 | heal/status consume | weapon after-hit 受限 | 通用 successful attack-hit trigger |
| source-bound buff | target-mark 清理 + `BattleStatusDerived` 续命 | opt-in 清除不存在 | `remove_on_source_deactivated` + status provenance |
| 换装刷新 | change-equipment candidate | **已落地**（原子重算、HP clamp、target mark 清理、§8.7 opt-in status 清理） | — |
| save/writeback | 实例 `ability_usage_periods` / `PartyState` | **已落地**（无 schema 变更，SaveVersion 保持 18） | 无改动 |
| UI/headless | party/battle snapshots | Party 页与战斗人物信息摘要**已落地**；granted action/headless 链未建 | granted action summary + headless snapshot 接入 |
| preview/AI | canonical preview + handler specs | direct add dice support 不完整 | parity、expected value、mutation guard |

## 13. 文件落点

预计新增（内容）：

- `data/configs/gear_sets/dragon_scale_set.tres`
- 四件 `data/configs/items/armor_dragon_scale_*.tres`
- 单件/阈值 traits（`data/configs/traits/`）与 equipment ability pack（`data/configs/equipment_abilities/`）
- `data/configs/skills/equipment_dragon_scale_dragon_blood_boil.tres`
- `data/configs/skills/dragon_frightful_presence.tres`

预计新增（通用 ABI，非龙鳞专用分支）：

- `TraitSaveTagBonusEntryDef` / definition 与 registry 校验。
- `IBattleEquipmentDamageQuery.CollectMitigationTiers` 与 context/result DTO、`grant_mitigation_tier` action。
- `IBattleEquipmentDamageQuery.CollectBonusDamageDiceForEffect` 与 `BattleEquipmentAbilityDirectDamageContext`、`DamageOriginKind` closed domain、`AddDamageDiceActionPayloadDef.damage_type_mode`。
- `on_attack_hit` trigger。
- `remove_on_source_deactivated` authoring 字段与 status provenance。
- `GearSetGrantedActionSummary` 投影。

预计修改的 owner 组：

- `GearSetContentRegistry`（§5.2 校验补齐）。
- `TraitDef` / `TraitDefinition` / `BattleSaveContentRules`（新 save tag）。
- `BattleUnitSaveModifierState` / read view / `BattleUnitState`、`BattleTraitPassiveProjectionService`、`BattleSaveResolver`（`BonusByTag`）。
- `BattleDamageResolver`（新 query 挂点、mitigation 聚合、after-hit sink）、preview、AI handler specs。
- `BattleUnitFactory` / `BattleChangeEquipmentResolver`（opt-in buff 清理挂接）。
- headless snapshot 链（`IGameRuntimeSnapshotSource`、`GameRuntimeSnapshotBuilder`、`BattleHudAdapter`、`SnapshotTestRuntime`）。
- 红龙 template 与 `dragon_tyrant` AI（使用 `dragon_frightful_presence`）。

实现完成后才更新：

- `docs/design/battle/equipment_ability_runtime.md`
- `docs/design/progression/equipment_sets.md`（补龙鳞套与新增 ABI 的当前口径）
- `docs/design/project_context_units.md` 的内容、装备、属性、战斗、UI、存档和测试 context units

提案阶段不能把未落地关系写进 `docs/design/`。

## 14. 回归矩阵

### 14.1 内容与计算（大部分已有凤凰范式回归，补龙鳞特化）

已有可复用范式：`tests/equipment/run_gear_set_evaluation_regression.cs`（阈值激活、anchor 回退、耐久/重复/槽位/footprint 不计数、属性 provenance、trait 投影、`GearSetThreshold`/`PlayerPersistentGearSetThreshold` source）。

本切片新增：

- §5.2 新校验的拒绝用例：跨 set 成员重复、悬空 binding/action、非法 modifier 值域、binding 未允许 `gear_set_threshold`。
- 四件资源的 slot、价格、`hp_max`、traits、modifier。
- 龙鳞 0/1/2/3/4 件累计阈值。
- requirement preview 不被待装备物品的新 set bonus 自我满足。
- evaluator 不缓存、不修改 Resource 或输入 `EquipmentState`。

### 14.2 单件效果

- 头盔 `fire=half` 与 dragon/non-dragon 攻击加值。
- 胸甲 `fire/freeze/lightning=half`；dragon breath / 非 breath；dragon / 非 dragon source。
- 三个抗性 trait 拒绝空/非法 damage tag、非法 mitigation tier 和单 trait 重复 tag；非匹配 damage tag 不减伤。
- 护手只在每次近战武器命中 dragon 时 `+1D4`；重复/连锁攻击逐段命中、逐段查询。
- 胫甲对普通 fear `+3`；对 dragon frightful presence `+3` 且免疫；不免疫普通 fear。
- `dragon_frightful_presence` 的 enum/StringName 双向映射、valid/control 分类，以及所有 validator 对非法 tag 的拒绝。
- `add`、`highest`、两类混合、非法 mode、非正数 bonus 和单 trait 重复 key。

### 14.3 2/4 件效果

- 0 件/仅胫甲/仅 2 件阈值/胫甲加 2 件阈值的 fear trait bonus 分别为 `0/+3/+3/+6`，同时覆盖 `frightened` 与 `dragon_frightful_presence`。
- 2 件套 `fire=half`、fear bonus、龙威免疫；trait bonus 与既有 transient status max 彼此相加，status 内部语义不变。
- 头盔、胸甲和 2 件套的多份 `fire=half` 在 unit resistance map 中不叠加；卸下装备或跨过阈值后重新投影，不能留下 stale tier。
- 4 件对 dragon `+2`，非 dragon 不加。
- 四件套 `+1D4` 分别覆盖 weapon、非武器 attack spell、save direct damage。
- multi-effect、multi-target、fixed repeat、repeat-until-fail、random chain 的每次实际主伤害结算分别触发；同一 batch、skill 和 `SourceEffectOrdinal` 再次执行也不得被去重。
- 三次近战命中时，四件套触发 `3D4`；同时装备护手时两来源合计 `6D4`。
- 九次近战命中时，四件套触发 `9D4`；同时装备护手时两来源合计 `18D4`。另有独立终结攻击时再触发一次，并服从该段倍率。
- miss 段不触发；save-only 主直接伤害不因没有 attack check 被误判为 miss。
- 主 effect 带任意数量的 `extra_damage_segments` 仍只查询一次；DOT、terrain、reflection、自伤、equipment bonus、equipment direct reaction、equipment trigger-skill/immediate-attack 都不得触发或递归。
- 额外骰继承主 damage tag，并进入相同的分段倍率、save 和 mitigation；主伤害 critical 不额外复制套装骰。
- execute、preview 与 AI 对实际段数、命中概率和分段倍率的解释一致；query 全程只读。

### 14.4 龙息顺序

- fire/freeze/poison/acid/lightning 五种 breath。
- dragon 普通元素法术不触发。
- 非 dragon 的 breath 不触发。
- half、double 相互抵消。
- immune 优先于 half。
- 多份 half 不变成 quarter。
- tier 在固定 DR 前执行，固定 DR 在成功豁免减半前执行。
- 当前 `power=12` fire breath 在满套下，失败豁免为 `6`、成功豁免为 `3`；头盔单件也是 `6/3`，证明多份 `half` 不重复乘算。
- 用带固定 DR 的 synthetic case 锁定 `tier → fixed DR → save` 的整数顺序；`immune` tier 仍可归零。
- 报告保留最终 tier、条件性 tier sources 和固定 DR sources；静态 resistance source 沿用当前 tag 级标识。
- execute/preview/AI 结果一致。

### 14.5 龙血沸腾

- 只有 4 件时可见；每个锚点实例每天一次。
- 从应用时的 timeline TU 起精确持续 180 TU，到期边界失效；测试只推进 TU，不断言现实毫秒/分钟换算。
- 对 dragon 攻击 `+3`，对非 dragon 不加。
- 前三次真实攻击命中各治疗 `1D6`，第四次不触发。
- miss/save-only/preview/AI 不消费。
- 满血或最终零伤害的成功命中仍消费一层。
- 同日卸下重穿同一锚点实例不恢复次数；source 丢失立即清 buff（opt-in），不返还次数。
- 次日恢复。
- 转给另一成员使用该装备自身剩余额度（次数跟装备走）。
- opt-in 清除不影响既有 `BattleStatusDerived` 续命语义：凤凰祝福类回归必须继续 PASS。

### 14.6 存档、换装和 UI

- 锚点实例 usage 随既有 `equipment_state` 序列化 strict round-trip（复用现有实例 codec 回归）；无 SaveVersion 变更。
- battle end writeback 成功与失败 rollback（沿用现有链路回归）。
- battle-local duplicate/preview/AI mutation-exact 保留 derived source、`BonusByTag`、status provenance、buff 与剩余 charge；世界 `PartyState` round-trip 只包含实例 usage，不包含这些 battle-local owner。
- battle change equipment 跨越 2/4 阈值时属性、source、技能和 opt-in buff 原子刷新。
- Party UI 与 battle HUD 分别消费正确 view。
- headless snapshot 稳定排序、disabled reason、remaining uses。
- AI mutation-exact 覆盖实例 usage、derived source 和 nested list。

普通 focused regressions 与数值 battle simulation 分开运行，simulation 不混入 routine suite。但在开始实现前，必须先完成 AC `+11`、峰值攻击 `+9`、满套近战单段 `+2D4` 及三段/九段累计伤害、`half` 抗性覆盖面的静态审计（§2.5 已完成静态部分）；功能完成后再单独跑 simulation 微调，不能把明显的离散尺度错误留到实现后才发现。

## 15. 实施顺序与完成门槛

基础设施（authoring、registry、snapshot、计数、属性、battle source、周期 usage、换装重算、UI 摘要）已随凤凰涅槃落地并有回归。本切片剩余工作分为三个提交阶段，但它们不是可删减的 MVP：

1. 通用战斗 ABI：条件性 mitigation tier（§8.3）、per-main-direct-effect 加骰（§8.4）、`TraitSaveTagBonusEntryDef` 与 `BonusByTag`（§8.5 前半）、`on_attack_hit` trigger（§8.6）、opt-in source-bound buff 清除（§8.7）、registry 校验补齐（§5.2）。全部带 execute/preview/AI parity 与 mutation-exact 回归。
2. 龙鳞内容：四件物品、无条件抗性 traits、阈值 traits、ability pack、`dragon_scale_set.tres`、`equipment_dragon_scale_dragon_blood_boil`、`dragon_frightful_presence` save tag 与技能；granted action summary 与 headless snapshot 接入。
3. 真实 producer 与验收：红龙 template 与 `dragon_tyrant` AI 使用龙威、全部 §14 回归、换装生命周期验收、current design 文档更新（`equipment_sets.md`、`equipment_ability_runtime.md`、context units）。

功能在第三阶段验收前保持未完成状态。以下任一项缺失，都不能在 current design 或 PR 说明中写“龙鳞套装已完整落地”：

- 用 tag 代替 typed membership。
- 只做静态 modifier。
- 用 `special_effect_id` 或具体 set/item/skill ID 分支。
- 没有真实 frightful presence producer（技能 + 红龙使用 + AI action 三者齐备）。
- 四件套 `+1D4` 仍只覆盖武器，或重复/连锁攻击没有按每次实际主伤害结算触发。
- 保留无 consumer 的 `resistance_* +N`，或把它解释成固定 DR/百分比，而不是 canonical mitigation tier。
- 龙息 50% 被写成 fixed DR。
- preview/AI/save/writeback/UI 任一链路缺失。
- 为龙鳞改动已落地的凤凰语义（成员模型、锚点账本、祝福续命），或让凤凰回归失败。

### 实际落地摘要（2026-08-18）

阶段 1 新增通用战斗 ABI（均非龙鳞专用分支）：`IBattleEquipmentDamageQuery.CollectMitigationTiers` 与 `grant_mitigation_tier` action（条件性 mitigation tier）；`CollectBonusDamageDiceForEffect` 与 `BattleEquipmentAbilityDirectDamageContext`、`BattleDamageOriginKind` closed domain、`AddDamageDiceActionPayloadDef.damage_type_mode`（每次主直接伤害结算加骰）；`TraitSaveTagBonusEntryDef` 与 `BattleUnitSaveModifierState.BonusByTag`（add/highest 聚合的永久 save tag 加值）；typed trigger `on_attack_hit`（通用 attack-hit reaction）；`ApplyStatusActionPayloadDef.remove_on_source_deactivated` 与 status provenance（opt-in source-bound buff 清除）；`GearSetGrantedActionSummary` 投影与 headless snapshot 接入；`BattleSaveContentRules` 新增 canonical `dragon_frightful_presence`（control tag）。

阶段 2 内容：四件 `armor_dragon_scale_*`、六个单件/阈值 trait、`dragon_scale_set.tres`、`dragon_scale_set_pack.tres`、`equipment_dragon_scale_dragon_blood_boil`、`dragon_frightful_presence` 技能。

阶段 3 真实 producer：`red_dragon` 模板携带 1 级 `dragon_frightful_presence`；`dragon_tyrant` brain 在 engage（`dragon_frightful_presence_sweep`，minimum_hit_count=2）与 pressure（`dragon_frightful_presence_point_blank`，minimum_hit_count=1）各新增一条自身中心 ground action（desired 距离 0..0、target_coord，与 range 0 + radius 4 的技能形态一致）。龙威技能的 `learn_source` 由 `subrace` 改为 `internal`：身份技能（race/subrace/ascension/bloodline）需要单位初始化 racial charge，而模板敌人没有该初始化路径，保留 `subrace` 会导致 `racial_skill_charge_uninitialized` 阻断施放；`internal` 是敌人专属技能的既有约定（如 `wolf_alpha_dominance_howl`）。

回归 runner 清单：

- 阶段 1 ABI：`tests/battle_runtime/runtime/run_equipment_conditional_mitigation_tier_regression.cs`、`run_equipment_direct_effect_bonus_dice_regression.cs`、`run_equipment_attack_hit_reaction_regression.cs`、`run_trait_save_tag_bonus_regression.cs`、`run_equipment_source_bound_status_cleanup_regression.cs`、`tests/equipment/run_gear_set_evaluation_regression.cs`。
- 阶段 2/3 龙鳞：`tests/battle_runtime/runtime/run_dragon_scale_set_regression.cs`、`run_dragon_scale_dragon_blood_boil_regression.cs`（含成员间转移装备后次数跟装备走的用例）、`tests/battle_runtime/ai/run_dragon_frightful_presence_regression.cs`（模板/brain 接线、AI 门槛、真实施放 + 免疫矩阵 + 普通恐惧对照）。
- 邻居回归：`run_enemy_template_runtime_start_regression.cs`（红龙 spawn 技能表锁定龙威）等 enemy/AI 回归。

已知边界：

- per-battle 次数在套装摘要投影中报满次数，权威可用性以 `BattleSkillAvailabilityService` 为准（§10 既有口径）。
- 文本渲染器（headless `GameTextSnapshotRenderer`）未渲染 `gear_sets` 字段；套装 surface 只进结构化 snapshot（party 成员 `gear_sets` 与 `battle.hud.gear_set_summaries`）。
- 幼龙（白龙/绿龙幼体）未获得龙威：内容文档无幼龙龙威依据，只有 10 级红龙持有该技能。
- 既有遗留（非本切片引入）：`dragon_breath_*` 六种吐息同样是 `learn_source=subrace` 的身份技能，模板敌人没有 racial charge 初始化路径，当前同样被 `racial_skill_charge_uninitialized` 阻断（红龙现有回归只靠 basic_attack 兜底通过）；修复吐息需要独立的内容或运行时决策，不在本切片范围。
