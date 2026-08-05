# 龙鳞铠甲套装完整落地方案

> 状态：Proposal，尚未实现。
>
> 审计基线：2026-08-05 当前 checkout。
>
> 数值状态：元素抗性已按当前离散 mitigation tier 体系修订；旧内容片段中的 `15/5/10` 不进入运行时。套装统一属于角色 10 级以后的高阶装备，部分套装可进入 20 级档；龙鳞套的具体获取档位与同档龙类目标仍需在内容冻结前明确。AC 与命中加值必须按 10/15/20 级目标审计，不能使用初始角色或 4–5 级幼龙定性。
>
> 本文定义的是一次完整交付，不是静态属性 MVP。四件物品、单件能力、2 件套、4 件套、真实敌方 producer、战斗换装、AI/preview、UI、存档和回归全部通过后，才能宣称龙鳞套装已落地。

## 1. 结论

首个完整落地套装选择 **龙鳞铠甲（Dragon Scale）**。

它不是当前代码里改动最少的静态套装，但仍是最适合作为第一条完整切片的套装：

- 已有 `red_dragon`、白龙幼体、绿龙幼体及六种 `dragon_breath_*` 技能，可以用真实内容验证对龙条件和吐息减伤。
- 现有 trait 被动投影已经支持 `TraitDamageResistanceEntryDef(damage_tag, mitigation_tier)` → `BattleUnitDamageResistanceState` 的无条件元素抗性；现有装备能力框架也支持 `creature_type_tags contains dragon`、攻击检定加值、武器命中加骰、固定伤害减免、装备授予技能、治疗、状态栈消费和 `per_world_day` 次数。
- 缺口集中在可复用的通用边界：套装阈值 source、条件性 mitigation tier、按 save tag 的加值、按每次实际主直接伤害结算加骰、非物理装备实例的每日用量 owner。
- 完成它以后，后续套装不需要退回按 `skill_id`、`item_id` 或 `special_effect_id` 写分支。

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
- `resistance_fire/cold/lightning +N` 当前没有 AttributeSnapshot consumer，`N` 也不是当前抗性 schema 的合法量纲。旧内容中的 `15/5/10` 直接废止，不换算为百分比，也不换算为固定 DR。
- 当前正向“抗性”的 canonical 内容口径是对应 damage tag 的 `half` mitigation tier；只有文案明确写“免疫”时才使用 `immune`，弱点使用 `double`。单件和 2 件套的无条件抗性必须写入 trait 的 `damage_resistance_entries`。
- `cold` 对应当前 damage tag `freeze`。
- `saving_throw_fear +3` 定义为 fear save tag 加值，不伪装成普通 attribute。
- `base_price` 必须拆成当前 `ItemContentRegistry` 要求的买入/卖出价格字段。

当前伤害管线中的抗性、固定减伤和豁免顺序是：

```text
基础骰与加值
→ offense multiplier
→ mitigation tier（immune / half / double）
→ 固定 DR
→ 成功豁免的 half
→ 护盾与 HP 应用
```

`BattleUnitDamageResistanceState` 每个 damage tag 只保存一个 tier。race/subrace 按既有覆盖顺序写入，trait 被动只在候选 tier 更强时覆盖，当前强度序是 `immune > half > normal > double`。伤害结算时，unit resistance、状态和本方案新增的条件性装备 tier 一起参与最终选择：`immune` 优先，`half` 与 `double` 抵消，多份 `half` 仍只结算一次减半。

当前 `red_dragon` 的两种 fire breath 都是固定 `power=12`。头盔的 `fire=half` 会把 tier-adjusted damage 变成 `6`；豁免失败最终为 `6`，豁免成功再减半为 `3`。完整套装面对 fire breath 仍是 `6/3`，因为头盔、胸甲、2 件套和吐息条件来源的多份 `half` 不重复乘算。满套额外提供的是元素覆盖面、poison/acid 龙息的条件抗性和其他屠龙能力，不是更深一档的 fire 减伤。

### 2.4 TU 原生持续时间口径

战斗持续时间只使用 **TU**，不映射现实秒、分钟或帧数：

- `BattleTimelineState.TuGranularity=5` 只定义时间线的最小推进粒度。
- `BattleTimelineDriver.ApplyTimelineStep()` 按 TU 推进状态持续时间、施法进度和行动进度。
- `GameRuntimeFacade.BattleAutoAdvanceTickMsec=1000` 只是外层自动调用的调度节奏；手动单位正在行动、模态状态或冻结状态都会阻止时间线推进，因此它不是游戏规则中的秒表。

龙血沸腾采用 `duration_tu=300`。这是一个纯玩法数值：以当前角色默认 `action_threshold=30`（`AttributeService.DEFAULT_CHARACTER_ACTION_THRESHOLD`）为内部参照，长度等于 10 个基础行动进度阈值。不同单位的行动阈值和时间修正可以不同，所以 300 TU 是全局时间线窗口，不表示现实 1 分钟，也不保证穿戴者恰好行动 10 次。

本切片不得新增 `SecondsToTu()`、`MinutesToTu()` 或任何现实时间换算 owner。`tests/battle_runtime/runtime/run_ravenplume_weapon_ability_regression.cs:302` 原先把 60 TU 写成“1 分钟”，本次方案修订已同步改为“持续时间应为 60 TU”；鸦羽的数值仍是 60 TU，但不再声明现实时间含义，龙鳞套也不依赖该数值。

### 2.5 10 级以后数值基线

套装不会作为低级装备投放。龙鳞套按角色 10 级以后的高阶/传奇装备审计；4–5 级幼龙、角色创建生命值和普通铁制装备只能验证机制，不能作为强度判定基线。龙鳞套究竟属于 10–12、15–17 还是 18–20 级档，仍需由获取内容明确。

当前角色成长与装备尺度如下：

- 基础 AC 是 `8`。龙鳞四件 AC 合计 `+11`，agility `12` 且受 `max_dex_bonus=1` 限制时最终 AC 为 `20`。现有传奇重甲方案中，大地守护者与圣光使者同为 AC `+11`，铁壁要塞达到 `+12`，所以不能按普通铁甲基线把龙鳞削成低阶护甲。
- full BAB 按总职业阶数的 `4/8` 成长，10/15/20 级分别为 `+5/+7/+10`；three-quarter 为 `+3/+5/+7`，half 为 `+2/+3/+5`。旧样例“10 级 full BAB = +10”不成立。
- 不计种族、技能百分比和装备，10 级角色在常见职业/体质组合下的期望 HP 约为 `54–162`，20 级约为 `99–292`。胸甲 `hp_max +10` 在 10 级约占 `6%–19%`，20 级约占 `3%–10%`，属于正常固定增益。
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

重复攻击已有的分段伤害倍率继续作用于本段套装骰；例如额外的终结段若有更高倍率，仍由 canonical 伤害管线统一处理。这个数值低于专用屠龙武器每次命中的 `3D6`/`2D8` 先例，保留叠加空间而不让护甲取代武器定位。

因此本方案保留 AC `+11`、`hp_max +10`、离散 `half` 与 300 TU 主动；把完整近战单段由原 `+3D6` 降为 `+2D4`。峰值攻击 `+9` 等同档龙类曲线补齐后再冻结，不能使用当前幼龙或红龙直接削值。

## 3. 最终行为合同

### 3.1 四件物品

| 物品 | 基础属性 | 单件能力 | 实现口径 |
|---|---|---|---|
| `armor_dragon_scale_head` | AC `+2` | `fire=half`；对 dragon 攻击检定 `+2` | AC 走现有 modifier；抗性走 trait `damage_resistance_entries`；攻击条件走 binding |
| `armor_dragon_scale_body` | AC `+7`；`hp_max +10` | `fire/freeze/lightning=half`；dragon breath 获得 `half` tier | 无条件抗性走 trait；条件性吐息 tier 走 binding；`hp_max` 换装下降时沿现有规则 clamp 当前 HP |
| `armor_dragon_scale_hands` | AC `+1`；全局 `attack_bonus +2` | 每次近战武器命中 dragon 时额外 `+1D4`，伤害 tag 继承主武器伤害 | 复用现有 weapon-hit bonus dice；重复/连锁攻击逐次查询 |
| `armor_dragon_scale_feet` | AC `+1` | fear save `+3`；免疫 dragon frightful presence | 新增 save-tag bonus producer；免疫只匹配 canonical 龙威 tag |

装备损坏到无效状态或从 battle-local equipment view 移除时，不再贡献套装件数和单件能力。多占位装备按一个 `EquipmentEntry` 计算一次。

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

新增装备授予技能，使用独立 ID，例如：

```text
equipment_dragon_scale_dragon_blood_boil
```

不能复用现有 `warrior_dragon_blood_boil`，该技能已有不同职业语义。

行为：

- 自身目标，消耗 1 AP，不消耗 MP/体力。
- `per_world_day`，每个穿戴者每天最多 1 次。
- 持续 300 TU。
- 持续期内，对 dragon 的攻击检定额外 `+3`；与单件头盔和 4 件套加值相加。
- 状态初始带 3 层治疗 charge。
- 每次真实攻击检定命中后，自身恢复 `1D6 HP` 并消耗 1 层；任意目标都可触发，豁免型伤害不算“命中”。
- 命中后即消费 charge；即使目标最终被完全减伤或使用者已满血，也不返还该层。
- miss、preview、AI 估值和无执行的 command validation 不消费 charge。
- 4 件套 source 在战斗中失效时，立即移除该 buff；当天次数不返还，同日重新穿齐也不能再次使用。

## 4. 方案比较

| 方案 | 优点 | 致命问题 | 结论 |
|---|---|---|---|
| 沿用 tag + GDScript registry | 表面改动少 | tag 是自由文本；多占位/重复 tag 易误计；绕开 process snapshot；特殊效果仍要 runtime 分支 | 拒绝 |
| `GearSetDef.member_item_ids` 为唯一成员真相 | 套装文件集中 | 新增物品要改共享套装文件；成员关系远离物品；容易成为 merge hotspot | 可行但不选 |
| `ItemDef.gear_set_id` + typed `GearSetDef.thresholds` | 成员关系唯一；物品自描述；snapshot 可生成双向索引；适合校验与 UI | 需要新增 registry/snapshot surface | **采用** |
| 把套装能力锚定头盔或胸甲实例 | 可复用现有 equipment usage | 换锚点会刷新/搬走每日次数；删除锚点会错误移除整套 source | 拒绝 |
| 每件装备各挂一份 2/4 件能力 | 不需 derived source | 满套重复触发四次；无法表达唯一阈值 owner | 拒绝 |

## 5. 内容模型与注册链

### 5.1 Authoring Resource

新增：

```text
GearSetDef
- gear_set_id: StringName
- display_name: string
- description: string
- thresholds: Array<GearSetThresholdDef>

GearSetThresholdDef
- threshold_id: StringName
- required_piece_count: int
- display_name: string
- description: string
- attribute_modifiers: Array<AttributeModifierDef>
- granted_trait_ids: Array<StringName>
```

`GearSetThresholdDef` **只**通过 `granted_trait_ids` 暴露战斗能力，不增加 `skill_id`、`granted_action_ids` 或其他直接授予动作字段。主动技能的唯一链路是：

```text
active threshold
→ granted trait
→ 允许 source_kind=gear_set_threshold 的 equipment ability binding
→ EquipmentGrantedActionDef
→ equipment_dragon_scale_dragon_blood_boil
```

这样 threshold 只负责组合和 provenance，动作的条件、次数、payload 与 preview/AI 支持仍由现有 typed equipment-ability ABI 负责。

`ItemDef` 新增唯一可空字段：

```text
gear_set_id: StringName
```

约束：

- `gear_set_id` 只允许具体、不可堆叠、可装备物品声明。
- item template 禁止声明 `gear_set_id`，避免整个模板族隐式成为套装件。
- `GearSetDef` 不再维护第二份 `member_item_ids`。
- snapshot builder 从 `ItemDefinition.GearSetId` 生成只读 `gear_set_id -> item_ids` 和 `item_id -> gear_set_id` 索引。
- `tags=["dragon_scale_set"]` 可保留用于搜索/文案，但不参与计数和运行时规则。

### 5.2 Immutable definitions

新增：

- `GearSetDefinition`
- `GearSetThresholdDefinition`
- `GearSetContentRegistry`

接入：

- `ProcessContentHost`
- `ContentSnapshotBuilder`
- `ContentSnapshot`
- `GameContentCatalog`
- `SyntheticContentSnapshotFactory`

当前 owner：

- `scripts/systems/content/ProcessContentHost.cs:79-190`
- `scripts/systems/content/ContentSnapshotBuilder.cs:30-154`
- `scripts/systems/content/ContentSnapshot.cs:6-114`
- `scripts/systems/content/GameContentCatalog.cs:60-177`
- `tests/shared/SyntheticContentSnapshotFactory.cs:30-110`

生产 runtime 只消费 immutable definitions，不加载 raw `GearSetDef`。

### 5.3 内容校验

registry/snapshot build 必须拒绝：

- 空或重复的 set/threshold ID。
- 非正数、重复、非严格递增的阈值。
- 阈值大于该套装可达的有效装备件数。
- item 引用不存在的 set。
- template、stackable item、非 equipment item 声明 set。
- 同一 item 多套装归属。
- threshold 试图通过 trait 之外的字段直接授予 skill/action。
- 不存在的 trait、binding 或 `EquipmentGrantedActionDef`。
- binding 不显式允许 `gear_set_threshold` source，或 source kind 与 threshold 来源不匹配。
- modifier ID、mode、值域或 consumer 不存在。
- trait 的 save tag 不属于 `BattleSaveContentRules` 的 closed domain，或 save-tag bonus 使用非法 stack mode/非正数 bonus。
- Dragon 四件没有分别落到 head/body/hands/feet。
- 4 件阈值在合法槽位组合中不可达。

## 6. 唯一套装计算 owner

新增纯函数：

```text
GearSetEvaluationService.Evaluate(
    EquipmentState equipment,
    IReadOnlyDictionary<StringName, ItemDefinition> items,
    IReadOnlyDictionary<StringName, GearSetDefinition> sets
) -> GearSetEvaluationSnapshot
```

返回 typed DTO：

```text
GearSetEvaluationSnapshot
- ActiveSets
- AttributeModifiers
- DerivedTraitInstances

GearSetActivationSummary
- GearSetId
- DisplayName
- EquippedPieceCount
- TotalPieceCount
- ContributingItemInstanceIds
- Thresholds

GearSetThresholdStatus
- ThresholdId
- RequiredPieceCount
- IsActive
- DisplayName
- Description
```

规则：

1. 遍历 `EquipmentState.GetEntrySlotIdsTyped()`，不遍历 filled slots。
2. 每个有效 `EquipmentEntry` 只计算一次。
3. 从 immutable `ItemDefinition.GearSetId` 归组。
4. 按 `required_piece_count <= count` 累计激活阈值。
5. 输出按 set ID、threshold count、threshold ID 稳定排序。
6. 不缓存可变 `EquipmentState`，不读全局 PartyState。
7. 不保存计数和激活阈值；换装时从当前 view 重算。

现有 `EquipmentState.GetEntrySlotIdsTyped()` 位于 `scripts/player/equipment/EquipmentState.cs:132-142`，已经提供正确的 entry 粒度。

### 6.1 属性投影

把 `PartyEquipmentService.BuildAttributeModifiersTyped()` 演进为统一的 typed projection：

```text
BuildAttributeProjectionTyped(equipmentView)
-> EquipmentAttributeProjection
   - ItemModifiers
   - GearSetModifiers
   - GearSetSummary
```

套装修正进入 `AttributeSourceContext.gear_set_attribute_modifiers` 独立通道，保留 set/threshold provenance，不能伪装成普通 equipment modifier。

当前接点：

- `scripts/systems/inventory/PartyEquipmentService.cs:226-417`
- `scripts/systems/attributes/AttributeSourceContext.cs:4-28`
- `scripts/systems/attributes/AttributeService.cs:620-677`
- `scripts/systems/progression/CharacterManagementModule.cs:494-543`

装备 requirement preview 继续使用“移除旧件、尚未放入新件”的 view，禁止新套装奖励反过来自我满足穿戴条件。

## 7. Battle source 与 trait 投影

### 7.1 新 source kind

新增：

```text
TraitSourceKind.GearSetThreshold
EquipmentAbilitySourceKind.PlayerPersistentGearSetThreshold
```

每个激活阈值、每个 granted trait 只生成一个 stable source：

```text
gear_set::<unit_id>::<gear_set_id>::<threshold_id>::<trait_id>
```

`BattleEquipmentAbilitySourceState` 当前字段强制假设 source 是物理装备：

- `EquipmentDefId`
- `SourceEquipmentInstanceId`

当前 schema 在 `scripts/systems/battle/core/BattleEquipmentAbilitySourceState.cs:8-153`。

应把 source identity 演进为 discriminated typed schema：

```text
source_kind
effective_instance_key
source_definition_id   # item_id 或 gear_set_id
source_instance_id     # equipment instance_id 或 threshold_id
ability_ids
```

物理装备、敌方 battle-only 装备和 gear-set threshold 分别校验合法字段组合。直接升级所有生产者、codec 和测试，不增加旧字段 alias 或双读 fallback。

### 7.2 投影路径

`BattleEquipmentAbilityProjectionService` 新增 gear-set 分支：

1. 读取 unit 的 battle-local `EquipmentState`。
2. 调用同一个 `GearSetEvaluationService`。
3. 把 active threshold 的 traits 作为 `GearSetThreshold` effective traits。
4. 通过现有 binding matcher 匹配声明允许 `gear_set_threshold` 的 bindings。
5. 生成唯一 ability source 和 temporal modifier。

不能：

- 伪造 `SourceEquipmentInstanceId`。
- 把四件装备各投影一份阈值能力。
- 在 battle runtime 判断 `dragon_scale_set`、具体 item ID 或 binding ID。

当前物理装备投影位于 `scripts/systems/battle/runtime/BattleEquipmentAbilityProjectionService.cs:35-125`。

### 7.3 战斗内换装

`BattleChangeEquipmentResolver` 完成换装后必须在同一 candidate state 中原子重算：

- 属性与 HP 上限。
- active thresholds。
- effective traits。
- equipment ability sources。
- granted action availability。
- source-bound buff 清理。

当前刷新/HP clamp 主链：

- `scripts/systems/battle/runtime/BattleUnitFactory.cs:394-458`
- `scripts/systems/battle/runtime/BattleChangeEquipmentResolver.cs:229-287`

失败的换装不能留下半套属性、stale source 或已删除的 buff。

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

现有龙鳞战斧内容已经同时证明两条链路：`fivefold_scales` trait 通过五个 `damage_resistance_entries` 授予 fire/lightning/poison/acid/freeze 的 `half`，equipment ability pack 则使用 `amount=2` 的固定 DR。两者是独立系统。该内容不提供套装阈值、条件性龙息 tier、任意直接伤害或套装级 usage owner。

### 8.2 无条件元素抗性：直接复用现有 tier owner

当前 authoring schema 已足够：

```text
TraitDamageResistanceEntryDef
- damage_tag
- mitigation_tier
```

`TraitContentRegistry` 已校验 damage tag、tier closed domain 和同一 trait 内重复 tag；`BattleTraitPassiveProjectionService` 已把有效装备 trait 投影到 `BattleUnitDamageResistanceState`，并使用 `immune > half > normal > double` 的 stronger-only 规则。正式执行、canonical preview 和 AI 估值已经共同读取 `BattleUnitState.TryGetDamageResistanceTyped()`，不需要新增百分比 action、query、事件字段或存档字段。

Dragon Scale 新增三个无条件 trait：

- 头盔：`fire=half`。
- 胸甲：`fire=half`、`freeze=half`、`lightning=half`。
- 2 件套 threshold trait：`fire=half`。

相同 tag 的多份静态 trait 最终仍只在 unit map 中保留一个最强 tier，不累加、不连续减半。当前 map 和伤害报告只暴露 `damage_resistance_<tag>` 这一 canonical source，不保存头盔/胸甲/阈值的逐 trait provenance；套装 UI 仍可从 gear-set evaluator 分别显示单件与阈值来源。精确的静态抗性来源追踪属于通用 resistance owner 的独立增强，不是龙鳞套正确结算所需的新接口。

### 8.3 条件性 mitigation tier

当前 `damage_reduction` 只返回固定整数 Amount：

- `EquipmentAbilityAuthoringDefs.cs:231-237`
- `BattleEquipmentAbilityReactionContracts.cs:111-117`
- `BattleDamageResolver.DtoHelpers.cs:139-190`

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

返回 `half`，并在 `ResolveMitigationTierResult()` 选最终 tier 之前与 status/unit resistance source 一起聚合。这样保持当前规则：immune 优先，half 与 double 抵消，多份 half 不重复乘算。

`DamageResolutionContext` 已持有 `SkillId`，但当前 reduction context 会丢失它；必须贯通正式执行和 preview，不按 skill ID 猜测吐息。

### 8.4 每次主直接伤害结算 `+1D4`

当前 `CollectBonusDamageDiceOnHit()` 在以下条件前不会运行：

```text
AttackSuccess == true
&& resultIncludesWeaponDamage
```

见 `BattleDamageResolver.Dice.cs:82-112`。因此它不覆盖纯法术、豁免伤害和非武器 attack spell。

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

`DamageOriginKind` 是 closed typed domain，必须能 fail-closed 地区分正常主技能/攻击伤害与 timeline/upkeep、terrain、reflection、self damage、equipment bonus、equipment direct reaction、equipment trigger-skill/immediate-attack 等排除来源。不得通过具体 skill、item、binding id 或当前调用深度猜测 origin。

`AddDamageDiceActionPayloadDef` 新增 closed mode：

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

### 8.5 fear save bonus 与龙威免疫

当前有两类相关 consumer：

- `BattleSaveResolver.cs:226-229,403-473,772-797`
- `BattleUnitSaveModifierState` 已持有 unit-level save ability bonus 与 immunity tags。
- `BattleStatusEffectState.save_bonus_by_tag`
- `save_immunity_tags`

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

`BattleSaveContentRules` 是 save tag 的唯一 closed-domain owner。新增：

```text
BattleSaveTagKind.DragonFrightfulPresence
dragon_frightful_presence
```

必须同时完成 `StringName` 常量、`ToSaveTagKind()`、`ToStringName()`、`IsValidSaveTag()` 和 `IsControlSaveTag()` 双向映射；龙威导致 `frightened`，因此它属于 control save tag。`SaveTagListContentRules`、trait/skill/enemy validator 都复用该 owner，不建立第二份 whitelist。

再新增真实技能：

```text
dragon_frightful_presence
- source: dragon
- save_tag: dragon_frightful_presence
- save failure: frightened
```

红龙 template 和 `dragon_tyrant` AI 必须实际使用该技能。胫甲与 2 件套的 immunity 只包含 `dragon_frightful_presence`，不能用 `frightened` 粗暴免疫所有恐惧。fear `+3` trait 同时覆盖通用 `frightened` 和 `dragon_frightful_presence` save tag。

### 8.6 通用 attack-hit reaction

当前 after-hit sink 在 `!ResultIncludesWeaponDamage(result)` 时直接返回，见 `BattleDamageResolver.cs:722-740`。因此龙血沸腾不能完整覆盖非武器法术攻击命中。

新增 typed trigger：

```text
on_attack_hit / after_hit
```

它在真实攻击检定成功后每目标触发一次，不要求 weapon damage；携带 skill/effect origin 和 event batch。龙血沸腾使用：

1. `has_status(dragon_blood_boil)` 条件。
2. 消耗 1 层自身 status。
3. 自身治疗 `1D6`。

旧 `on_hit` 保持 weapon-hit 语义，避免无意扩大现有武器内容。

### 8.7 source-bound buff 生命周期

buff status 必须保存：

- source kind。
- effective source key。
- binding/granted action provenance。
- `remove_on_source_deactivated=true`。

gear-set source 刷新时清理由该 source 创建的 status。不得仅靠 status ID 全局删除，也不得让同名其他来源互相覆盖。

## 9. 每日用量 owner、存档与 writeback

### 9.1 为什么不能绑定某件装备

现有非 per-battle usage 通过 `SourceEquipmentInstanceId` 查找具体实例并写入：

- `scripts/systems/battle/runtime/EquipmentAbilityUsageRuntime.cs:68-88`
- `scripts/systems/battle/runtime/EquipmentAbilityUsageRuntime.cs:582-640`

套装 threshold 没有唯一物理实例。若选择胸甲或头盔作锚点：

- 同日换掉锚点会错误恢复次数。
- 把锚点转给另一成员会搬走次数。
- 锚点损坏会错误删除整个 set 的 usage owner。

### 9.2 推荐 owner

在每个成员自己的 `EquipmentState` 顶层新增：

```text
GearSetAbilityUsagePeriodState
- GearSetId
- ThresholdId
- BindingId
- GrantedActionId
- PeriodKind
- PeriodIndex
- UsedCount
```

唯一 key：

```text
(gear_set_id, threshold_id, binding_id, granted_action_id, period_kind, period_index)
```

语义：

- 每个穿戴者、每个套装主动、每个世界日独立。
- 同一成员卸下再穿回不会重置。
- 套装转给另一成员后，另一成员使用自己的当日额度。
- 阈值失效不删除当天 usage。

持久化边界分三层：

1. **世界存档**：成员的 `EquipmentState` 只保存已消费 usage；套装件数和 active threshold 始终派生，不写入 `PartyState`。
2. **单场战斗生命周期**：derived gear-set source、龙血沸腾 buff 和三层治疗 charge 必须完整进入 battle-local `BattleUnitState` 的 duplicate、preview candidate、AI snapshot 与 mutation-exact，直到到期、source 失效或战斗 teardown。它们不是“无需保存的临时变量”。
3. **战斗结束 writeback**：只把装备变化和 `EquipmentState` usage 原子写回世界成员；derived source、buff 和 charge 不复制到 `PartyState`，战斗 teardown 后丢弃。写回失败必须整体 rollback，不能留下已消费 usage 或半套装备。

当前系统没有战中存档恢复合同，因此本切片不新增 battle-state 世界存档 codec；若未来增加战中存档，必须另行把这些 battle-local owner 纳入严格 codec，不能从 Party 装备重新猜测剩余 buff/charge。

`EquipmentState` 是最小且正确的 owner，因为 battle-local view 已经随战斗 candidate 原子写回成员：

- clone candidate：`GameRuntimeBattleWritebackService.cs:182-214`
- equipment writeback：`GameRuntimeBattleWritebackService.cs:271-307`

必须同步扩展：

- `EquipmentState.DuplicateState()` 和 mutation-exact。
- 严格 `ToDictionary()` / `FromDictionary()`。
- `PartyState.SaveSnapshot`。
- battle/AI mutation snapshot。
- ability availability、commit 和 remaining-use projection。

世界日 index 继续使用 `EquipmentAbilityUsageRuntime.ResolvePeriodIndex()`，不另算日历。

### 9.3 SaveVersion 与兼容边界

当前审计基线的 `SaveVersion` 是 18，但 `SaveSchemaVersions.cs` 已有用户未提交改动。实现时应在实际 landing baseline 上增加下一版本，不能在提案里假定仍必然是 18。

这是严格 schema 变更：

- 不加兼容迁移时，旧存档没有顶层 set usage key，会被拒绝。
- 若希望旧存档继续加载，需要 version-gated migration，把缺失 usage 初始化为空；副作用是旧存档中的玩家会获得当天一次使用机会。

按照仓库兼容政策，本文不擅自批准兼容逻辑。实现前必须由用户明确选择；未批准时按新版本严格读取，不添加 alias、fallback 或宽松缺省。

## 10. UI、headless 与可观察性

统一 typed snapshot：

```text
GearSetSummary
- GearSetId
- DisplayName
- EquippedPieceCount
- TotalPieceCount
- Thresholds
- GrantedActions

GearSetThresholdSummary
- ThresholdId
- RequiredPieceCount
- IsActive
- DisplayName
- Description

GearSetGrantedActionSummary
- GrantedActionId
- IsAvailable
- RemainingUses
- DisabledReason
```

接入面：

- Party 装备页：`PartyManagementWindow.cs:656-737`
- runtime facade equipment entry：`GameRuntimeFacade.cs:872-900`
- snapshot interface：`IGameRuntimeSnapshotSource.cs:39-42`
- member snapshot：`GameRuntimeSnapshotBuilder.cs:491-545`
- battle HUD：`BattleHudAdapter.cs:927-973`
- test stub：`tests/shared/SnapshotTestRuntime.cs:77-85`

要求：

- Party UI 消费当前 CharacterManagement equipment view。
- Battle HUD 消费 battle-local unit equipment view，不能复用入场前 Party snapshot。
- 显示 `2/4`、已激活/未激活阈值、缺少件数、每日技能剩余次数和 disabled reason。
- 伤害报告继续区分 mitigation tier 与固定 DR。静态抗性沿用 `damage_resistance_<tag>` source；条件性 dragon-breath tier 保留 binding/action provenance，因此可以区分胸甲与 4 件套的条件来源，但不虚构头盔/胸甲/2 件套的静态逐 trait provenance。
- 不向 UI 输出 raw Resource 或 `Array<Dictionary>`。

## 11. Preview、AI 与 reaction ordering

完整实现必须保证：

- preview 调用与正式执行相同的 set evaluator、attack bonus、逐次主直接伤害加骰、`BattleUnitDamageResistanceState`、条件性 mitigation tier 和 save modifier owner。
- preview、hover、AI scoring 不消费每日次数或三层治疗 charge。
- battle-local clone、AI stable projection 与 mutation guard 覆盖新增 `EquipmentState` 顶层 usage、derived source、`BonusByTag`、龙血沸腾 buff 和 charge。
- `add_damage_dice` / 新 per-main-direct-effect handler 显式声明 preview 与 AI support；当前 built-in spec 默认不包含这项能力，不能只因为 runner PASS 就假定已覆盖。AI 期望值按预计实际主伤害结算次数累计，不把多段技能固定算成一次。
- AI 能看见龙血沸腾的剩余次数、300 TU buff、对 dragon 的期望攻击收益和最多三次 `1D6` 治疗。

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

| 层 | 当前 owner | 当前状态 | 完整落地要求 |
|---|---|---|---|
| 物品 authoring | `ItemDef` | 无 set ID；四件资源不存在 | `gear_set_id` + 四件真实资源 |
| 套装 authoring | 无 | 仅 Markdown | `GearSetDef` / threshold Resource |
| 内容注册 | `ProcessContentHost` / `ContentSnapshotBuilder` | 无 gear-set registry | typed registry、交叉校验、seal 前加载 |
| immutable snapshot | `ContentSnapshot` / `GameContentCatalog` | 无 set index | definitions + 双向 membership index |
| 计数 | 无唯一 owner | 旧提案按 tag | `GearSetEvaluationService` 按 entry 纯计算 |
| 属性 | `PartyEquipmentService` / `AttributeService` | 只有 item modifier | set modifier 独立 provenance |
| trait/source | effective traits / equipment ability source | 只支持物理装备/敌方装备 | `GearSetThreshold` derived source |
| 攻击加值 | equipment attack query | 已有 | 复用，新增 set source |
| 单件近战加骰 | weapon-hit damage query | 已有 | 复用 |
| 每次主直接伤害结算加骰 | 无 | 当前仅 weapon hit | per-main-direct-effect context、inherit-primary、typed origin 防递归；不做 cast/event 去重 |
| 无条件元素抗性 | `TraitDamageResistanceEntryDef` / `BattleUnitDamageResistanceState` | 已支持离散 tier 与装备 trait 投影 | 单件/阈值 trait 写 `half`；复用 stronger-only map 与统一 resolver |
| 龙息半伤 | mitigation tier owner | 无条件性 equipment tier query | `CollectMitigationTiers()` |
| fear +3 | `BattleUnitSaveModifierState` / `BattleSaveResolver` | 只有 ability bonus 与 transient status max | typed `BonusByTag`、`add/highest` 聚合；完整套装 `+6` |
| 龙威免疫 | save immunity | 可按 tag 免疫，producer tag 缺 | canonical `dragon_frightful_presence` + 真龙技能 |
| 每日主动 | granted action | 绑定物理实例 usage | threshold → trait → binding → action；EquipmentState 顶层 set usage |
| 三次治疗 | heal/status consume | weapon after-hit 受限 | 通用 successful attack-hit trigger |
| 换装刷新 | change-equipment candidate | 不认识 set | 原子重算、清 buff、HP clamp |
| save/writeback | EquipmentState / PartyState | 无 set usage schema | version bump、strict codec、rollback |
| UI/headless | party/battle snapshots | 只显示 slots | typed set summary 与 remaining use |
| preview/AI | canonical preview + handler specs | direct add dice support 不完整 | parity、expected value、mutation guard |

## 13. 文件落点

预计新增：

- `scripts/player/progression/gear_sets/GearSetDef.cs`
- `scripts/player/progression/gear_sets/GearSetThresholdDef.cs`
- `scripts/player/progression/gear_sets/GearSetDefinition.cs`
- `scripts/player/progression/gear_sets/GearSetContentRegistry.cs`
- `scripts/systems/inventory/GearSetEvaluationService.cs`
- `data/configs/gear_sets/dragon_scale_set.tres`
- 四件 `data/configs/items/armor_dragon_scale_*.tres`
- 单件/阈值 traits 与 equipment ability pack
- `data/configs/skills/equipment_dragon_scale_dragon_blood_boil.tres`
- `data/configs/skills/dragon_frightful_presence.tres`

预计修改的 owner 组：

- content host/snapshot/catalog/test factory。
- `ItemDef` / `ItemDefinition` / `ItemContentRegistry`。
- `EquipmentState` / `PartyState.SaveSnapshot` / save version。
- `PartyEquipmentService` / `AttributeSourceContext` / CharacterManagement。
- trait source rules、effective trait projection、equipment ability binding matcher/source codec。
- `TraitSaveTagBonusEntryDef` / definition / registry 与 `BattleSaveContentRules`。
- `BattleUnitSaveModifierState` / read view / `BattleUnitState`、`BattleTraitPassiveProjectionService`。
- equipment damage query、`BattleSaveResolver`、hit reaction sink。
- `BattleDamageResolver`、preview、AI handler specs。
- `BattleUnitFactory`、`BattleChangeEquipmentResolver`、battle writeback。
- Party UI、battle HUD、headless snapshots。
- red dragon template 与 `dragon_tyrant` AI。

实现完成后才更新：

- `docs/design/battle/equipment_ability_runtime.md`
- 新建 `docs/design/progression/gear_set_system.md`
- `docs/design/project_context_units.md` 的内容、装备、属性、战斗、UI、存档和测试 context units

提案阶段不能把未落地关系写进 `docs/design/`。

## 14. 回归矩阵

### 14.1 内容与计算

- set/threshold ID、引用、排序、不可达阈值、template/stackable item 拒绝。
- threshold 只能经 trait → binding → `EquipmentGrantedActionDef` 授予动作；直接 skill/action 字段、source kind 不匹配和悬空 action 引用均拒绝。
- 四件资源的 slot、价格、`hp_max`、traits、modifier。
- 0/1/2/3/4 件累计阈值。
- 多占位装备只计一个 entry。
- 损坏、移除、替换后即时重算。
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

- 只有 4 件时可见；每天一次。
- 从应用时的 timeline TU 起精确持续 300 TU，到期边界失效；测试只推进 TU，不断言现实毫秒/分钟换算。
- 对 dragon 攻击 `+3`，对非 dragon 不加。
- 前三次真实攻击命中各治疗 `1D6`，第四次不触发。
- miss/save-only/preview/AI 不消费。
- 满血或最终零伤害的成功命中仍消费一层。
- 同日卸下重穿不恢复次数。
- source 丢失立即清 buff，不返还次数。
- 次日恢复。
- 转给另一成员使用该成员自己的额度。

### 14.6 存档、换装和 UI

- EquipmentState strict round-trip、重复 key/负数/非法 period 拒绝。
- battle end writeback 成功与失败 rollback。
- SaveVersion 新格式 round-trip；兼容策略批准后再增加旧版本 fixture。
- battle-local duplicate/preview/AI mutation-exact 保留 derived source、`BonusByTag`、buff 与剩余 charge；世界 `PartyState` round-trip 只包含 usage，不包含这些 battle-local owner。
- battle change equipment 跨越 2/4 阈值时属性、source、技能和 buff 原子刷新。
- Party UI 与 battle HUD 分别消费正确 view。
- headless snapshot 稳定排序、disabled reason、remaining uses。
- AI mutation-exact 覆盖 usage、derived source 和 nested list。

普通 focused regressions 与数值 battle simulation 分开运行，simulation 不混入 routine suite。但在开始实现前，必须先完成 AC `+11`、峰值攻击 `+9`、满套近战单段 `+2D4` 及三段/九段累计伤害、`half` 抗性覆盖面的静态审计；功能完成后再单独跑 simulation 微调，不能把明显的离散尺度错误留到实现后才发现。

## 15. 实施顺序与完成门槛

实施可分为四个提交阶段，但它们不是可删减的 MVP：

1. typed 内容模型、snapshot、计数、属性与 UI summary。
2. derived gear-set source、EquipmentState usage owner、save/writeback。
3. Dragon 四件资源、无条件抗性 traits、阈值 traits、per-main-direct-effect damage query、条件性 mitigation tier、fear producer、龙血沸腾。
4. 换装生命周期、preview/AI、真实 dragon producer、全部回归与 current design 文档。

功能在第四阶段验收前保持未完成状态。以下任一项缺失，都不能在 current design 或 PR 说明中写“套装系统/龙鳞套装已完整落地”：

- 用 tag 代替 typed membership。
- 只做静态 modifier。
- 用 `special_effect_id` 或具体 set/item/skill ID 分支。
- 把每日次数挂某件锚点装备。
- 没有真实 frightful presence producer。
- 四件套 `+1D4` 仍只覆盖武器，或重复/连锁攻击没有按每次实际主伤害结算触发。
- 保留无 consumer 的 `resistance_* +N`，或把它解释成固定 DR/百分比，而不是 canonical mitigation tier。
- 龙息 50% 被写成 fixed DR。
- preview/AI/save/writeback/UI 任一链路缺失。
