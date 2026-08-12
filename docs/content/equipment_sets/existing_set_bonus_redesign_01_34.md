# 既有传奇套装 1–34 Set Bonus Layer 重设计

> **状态：Planned / content redesign。** 本文只重新设计套装加成层，不代表资源、运行时、预览、AI、UI、存档或测试已经落地。
>
> **配置冻结。** `sets_*.md` 中 1000 件既有单件的 `item_id`、成员归属、装备槽、set tag、价格、单件属性、trait 与单件特殊效果全部保持原样；本文不覆盖、不迁移也不“顺手修正”任何单件字段。旧 `set_bonus_design.md` 的 2/4 或 2/4/6 阈值被本文的 1–34 新阈值方案取代，但原文继续作为主题与历史意图证据。
>
> **阻断项。** Set 11 与 Set 95 仍共用 `star_weaver_set` 并复用四个护甲 `item_id`。在获得独立配置修改授权前，本文不猜测应该改哪一套、也不提供兼容别名；Set 11 的数值仅是阻断后的候选设计，不得注册为正式内容。

## 1. 设计输入与冻结边界

本轮通读并交叉校准以下当前 checkout 文档：

- `sets_01_to_05.md`、`sets_01_to_05_accessories.md`
- `sets_06_to_10.md`、`sets_06_to_10_accessories.md`
- `sets_11_to_15_armor.md`、`sets_16_to_20_armor.md`、`sets_11_to_20_accessories.md`
- `sets_21_to_30_armor.md`、`sets_21_to_30_accessories.md`
- `sets_31_to_40_armor.md`、`sets_31_to_40_accessories.md` 中 Set 31–34
- `set_bonus_design.md`
- `existing_set_attribute_optimization.md` 中单件字段迁移、Dragon Scale 合同与当前 owner 审计

冻结不等于认可旧阈值。每套仍有 10 个固定槽位：`head/body/hands/feet/cloak/necklace/ring_1/ring_2/special_trinket/badge`；正式 membership **只**读取套装侧 `GearSetDefinition.member_item_ids[10]` 的显式既有 ID 清单。ItemDef 上的既有 tag 只用于作者搜索、审计和 UI 检索，不参与正式计数，也不要求新增 `gear_set_id`。新阈值只统计当前角色实际装备、ID 位于该清单且 placement 通过 `ItemDefinition` / `EquipmentRules` 复核的唯一成员；套装不另存 per-member 槽位镜像。重复 item instance、物品不允许的 entry slot、与 canonical footprint 不一致的占用槽位或同一 ID 的重复投影均不得重复计数。附录 A 固定列出全部 34×10 个 ID。

Set 21–30 护甲文档内的“2件套/4件套（设计预留）”与中央表互相矛盾。本文将它们视为历史主题证据，不把二者并行落地；新 set-bonus layer 是唯一候选阈值权威。

## 2. 主题驱动拓扑

不再把所有套装机械地写成 `2/4/6/8/10`。每套按其战斗叙事选择下列闭集拓扑，且 10 件必有 capstone：

| 拓扑 | 阈值 | 用途 |
|---|---|---|
| 稳步循环 | 2/4/6/8/10 | 吸血、施毒、标记、击杀等持续建立循环的套装 |
| 蜕变 | 3/5/7/10 | 穿戴者逐步获得另一种生物或元素形态 |
| 仪式/领域 | 2/5/8/10 | 晨光、王域、森林、圣光、风水等环境或团队领域 |
| 堡垒 | 4/6/8/10 | 前几件不应轻易获得完整防御闭环的重甲套装 |
| 节奏/诡术 | 2/3/6/9/10 | 依赖移动、隐匿、舞步、射击节奏或伏击窗口 |
| 终极高魔 | 3/6/9/10 | 凤凰、命运、深渊、知识等高魔机制，减少零散小档位 |

所有阈值均为“至少 N 件”，并累计保留更低档的不同职责；只有同一 `replacement_group` 内的数值按最高档替换，绝不把同组的骰数、概率、半径、次数或 TU 相加。

## 3. 同档价格与静态底盘校准

下表是冻结单件的文档合计，不是新 modifier。`AC/攻/HP/MP/移` 分别是十件单件当前文档中相关静态值的简单合计；为避免把历史尺单位带入新设计，摘要中的移动值已仅作展示地按 `5 尺 = 1 格` 换算成格，这不修改冻结 ItemDef。抗性、豁免、属性与技能检定另见原文。价格只用于识别同档离群，不按金币线性换算伤害。

| # | 套装 | 十件总价 | 冻结静态摘要 | Set-bonus 预算姿态 |
|---:|---|---:|---|---|
| 1 | 晨光圣骑士 | 132000 | AC11、攻2、MP20、移1格、radiant 10 | 保守；团队救援重于常驻输出 |
| 2 | 暗影刺客 | 116000 | AC6、dodge2、Dex1、移2格、极高 stealth | 标准；强度放在条件窗口 |
| 3 | 霜冻守望者 | 121000 | AC10、攻2、HP10、移1格、cold 30 | 保守；附伤使用替换组 |
| 4 | 烈焰法师 | 105000 | AC5、MP45、移2格、法术命中2、fire 25 | 标准；高魔爆发由次数约束 |
| 5 | 大地守护者 | 125000 | AC12、攻1、HP15、Con/Str 各1 | 保守；避免再堆常驻 AC |
| 6 | 风暴行者 | 123500 | AC9、攻2、移3格、lightning 30 | 保守；蓄电需要真实受伤 |
| 7 | 亡灵收割者 | 112500 | AC8、HP10、MP25、necrotic 30 | 标准 |
| 8 | 龙鳞铠甲 | 142000 | AC12、攻3、HP20、fear save3 | 严格保守；沿用高阶 Dragon Scale 合同 |
| 9 | 铁壁要塞 | 126000 | AC13、HP40、Con1 | 严格保守；领域减伤有频率门槛 |
| 10 | 古代帝王 | 150000 | AC11、攻1、Cha2、移1格、极高 persuasion | 严格保守；强度依赖 CR/阵营/领域 |
| 11 | 星辰织者 | 120000 | AC5、攻1、MP30、移1格 | 标准，但 P0 阻断注册 |
| 12 | 毒蛇之吻 | 118000 | AC7、poison 35、stealth6 | 标准；毒骰与毒雾均最高档替换 |
| 13 | 北风旅者 | 114000 | AC6、攻1、移4格、cold 20 | 标准；机动不直接叠伤害 |
| 14 | 虚空行者 | 118000 | AC5、攻1、MP35、force 25 | 标准；完整虚体只在 10 件 |
| 15 | 自然之语 | 114000 | AC6、攻1、HP10、移1格、nature12 | 轻度补强；以召唤/领域而非平面属性补强 |
| 16 | 深渊凝视 | 123000 | AC9、HP5、MP15、psychic25 | 保守；召唤与 aura 受上限约束 |
| 17 | 圣光使者 | 129000 | AC11、攻2、HP10、移1格、双元素抗性 | 保守；团队 DR 只在高档领域 |
| 18 | 永恒学徒 | 114000 | AC5、MP30、移1格、arcana10、history8 | 轻度补强；强度放在一次性准备/复制 |
| 19 | 血月猎人 | 123000 | AC9、攻1、移2格、追踪底盘高 | 保守；shapechanger 条件换取较高上限 |
| 20 | 锈蚀齿轮 | 119000 | AC10、攻1、HP10、移1格、Str1 | 保守；构装免疫与复起延后至 capstone |
| 21 | 银月游侠 | 127000 | AC9、攻2、dodge2、移2格 | 保守；齐射限制目标数与次数 |
| 22 | 沙漠蝎刺 | 116000 | AC10、攻1、移2格、fire25 | 标准 |
| 23 | 丛林猎豹 | 121000 | AC8、攻1、移2格、dodge1 | 标准；额外攻击有链上限 |
| 24 | 高山雄鹰 | 121000 | AC9、攻1、移3格、感知高 | 标准；双倍仅作用基础武器骰 |
| 25 | 海盗船长 | 116000 | AC9、HP20、移2格、社交高 | 标准；主要预算给团队号令 |
| 26 | 赏金猎人 | 116000 | AC9、HP15、移2格、调查高 | 标准；处决按目标记忆与战斗上限 |
| 27 | 瘟疫医生 | 121000 | AC9、HP10、移1格、medicine12、poison20 | 标准；传播设每 tick 新目标上限 |
| 28 | 月影舞者 | 116000 | AC7、MP35、移3格、performance12 | 标准；反击与隐匿共享节奏锁 |
| 29 | 血骑士 | 121000 | AC9、攻2、Con2、HP20、移2格、necrotic20 | 保守；吸血和逐段附伤均设段上限 |
| 30 | 幽灵/幻影骑士 | 132000 | AC9、攻2、HP15、MP20、移2格 | 保守；统一“幽影骑术→幻影形态” |
| 31 | 风语者 | 99000 | AC5、MP35、移5格 | 轻度补强；补领域，不再加大量移动 |
| 32 | 水波行者 | 99000 | AC6、MP35、移2格、cold15 | 轻度补强；水域条件换取上限 |
| 33 | 地震先知 | 103000 | AC9、攻2、HP10、双 save2 | 保守；低价格不抵消高战斗静态 |
| 34 | 夜幕吟游诗人 | 99000 | AC5、MP50、performance13 | 标准；已有高 MP/技能底盘，爆发受日用量限制 |

## 4. 通用精确合同

### 4.1 Replacement group

- 同类静态值使用稳定组名，例如 `dragon_target_attack`、`blood_lifesteal_bp`、`frost_melee_die`。单件、低阈值、高阈值同时存在时只取数值最高者；相同 mitigation tier 重复 `half` 仍只是 `half`。
- 同一触发上的伤害骰、治疗倍率、概率、半径、目标数、charge 数和持续 TU 必须各自声明 replacement group。高档写的是“替换后总值”，不是在低档基础上再加一次。
- 不同职责可以累计，例如“抗性”和“主动领域”并非同组；但两个来源都试图在同一次致死、miss、kill 或 roll 上改写结果时，每个事件最多选择一个胜出反应。

### 4.2 真实多段与 origin 防递归

本文的“qualifying primary direct segment”必须满足：

1. 有封闭且已知的 `DamageOriginKind`；未知值 fail closed。
2. 是原攻击/技能的真实主直接伤害段，且由穿戴者发起。
3. 排除 `extra_damage_segments`、装备/set-bonus 生成伤害、触发技能、DOT/upkeep、地形 tick、反射、自伤与过量伤害。
4. 不按 cast、skill、effect ordinal、target 或 event batch 跨段去重；重复攻击、随机链和多目标的每个真实段分别判定。
5. 吸血只读每段 mitigation 后的实际 HP 损失；preview/AI 只估值，不消费 usage、charge 或改写 canonical state。

### 4.3 Usage 与换装

- `per_battle` 用量归 battle unit 的 set source；`per_world_day` / `per_world_month` 归角色持久 `GearSetUsageState`，换下、换回、存取仓库、战斗重建或换同 ID 实例都不得刷新。
- 单件 usage 默认仍由原 item instance/source 独立拥有。只有各套“单件交互”明确指定 `shared_usage_key` 时才共享；共享池只消费实际胜出的效果。
- 失去阈值立即撤销静态派生源和未开始的授权技能；已经提交的独立投射物/地形按其正式生命周期结束。变身、光环、charge 与尚未结算的反应在阈值破坏时立即清理。

### 4.4 Grid 距离与 AP 合同

- 新 set-bonus layer 的范围、半径、高度差、推拉、传送和 move capacity 全部使用**地格**。历史文档只在输入校准时按 `5 尺 = 1 格` 换算；正式 definition 中不保存尺、不保存隐式换算倍率。
- 规范示例：历史 5/10/15/20/30/60 尺分别落为 1/2/3/4/6/12 格；历史 movement +5/+10/+20 分别落为 `move_capacity +1/+2/+4 格`；30 尺 flight 落为 `flight_move_capacity = 6 格`。
- 主动技能必须声明 AP：轻量标记/姿态 1 AP，形态/号令 2 AP，重型 AoE、领域和召唤 3 AP。自动触发、on-hit/kill/lethal 与文中 reaction 触发时序为 0 AP；reaction 只表示事件窗口，不是 D&D 支付单位，但仍须消耗正式 reaction opportunity（若该触发声明需要）。
- “每个 attack command”指同一次正式命令派生的真实攻击集合，只用于次数归组；不得据此跨 segment 去重。immediate weapon attack 明确写是否额外支付 AP，不能使用 `bonus action` 含混表达。
- 除文中明确写为固定自检 `DC15` 的形态副作用外，所有由套装产生的目标 save 都必须引用正式 `SkillDefinition`，使用当前系统 `save_dc_mode = caster_spell`、`save_dc = 0`，由 `BattleSaveResolver` 解析为 `8 + source ability modifier + proficiency bonus`。source ability 固定在 `GearSetDefinition.save_dc_source_ability`，不得由具体角色职业或描述文本猜测；目标 save ability/tag 仍以各档写出的 Strength/Agility/Constitution/Willpower 等为准。
- source ability 分组：Strength＝Sets 5/33；Agility＝2/13/22/23/28/30；Constitution＝3/8/9/12/29/32；Perception＝19/21/24/26；Intelligence＝4/14/18/20/27；Willpower＝1/6/7/10/11/15/16/17/25/31/34。即使某套当前档没有敌方 save，也必须冻结该字段，避免以后能力升级另造 DC 规则。

### 4.5 Typed owner 与当前共同缺口

建议的最小持久边界：

- authoring：`GearSetDef` → plain `GearSetDefinition`，阈值由 `GearSetThresholdDefinition` 持有，拓扑是闭集 `GearSetTopologyKind`；`GearSetDefinition.member_item_ids` 必须恰好 10 个且互异，并同时持有每个 ID 的预期 slot。
- 内容构建：`GearSetContentRegistry` 在 `ContentSnapshotBuilder` 中验证显式 member ID、唯一 slot、阈值、replacement group 和 ability 引用；tag 只作为审计提示，绝不能作为 runtime membership fallback，battle/runtime 不接触 raw Resource。
- 角色状态：`PartyMemberState` 持有按 `(set_id, usage_key)` 索引的 `GearSetUsageStateCollection`；不把套装用量绑在某一件可替换 item 上。
- 战斗投影：`GearSetRuntimeService` 只生成稳定 `set_bonus:{set_id}:{threshold}` 派生源。静态属性/trait 进入正式 attribute/resistance/save owner；反应与动作复用 typed equipment-ability definition/action；AoE、召唤、形态和领域引用正式 `SkillDefinition` / status / area 定义。
- 写回：战斗结束由现有 character battle writeback 边界原子提交持久 usage；preview/AI 使用 detached 只读副本。

当前代码只有逐 item attribute modifier 聚合，没有 set membership、threshold、持久 usage、换装刷新、set-derived source、完整环境 producer、领域/形态、UI/headless 或 focused regression。下文每套列出的 owner 是建议 owner，不是已存在声明。

当前证据：`scripts/systems/inventory/PartyEquipmentService.cs:226-239` 只遍历每件 `ItemDefinition.GetAttributeModifiersTyped()`；`scripts/systems/progression/CharacterManagementModule.cs:527-537` 消费这份逐件结果与 trait modifier，没有 GearSet 聚合入口。`docs/design/project_context_units.md:316-350,448-450` 仍正确描述 CU-10 与 typed equipment-ability 边界；本文是 planned 内容，不改变已落地 ownership，因此当前不修改 context index。未来 GearSet runtime 真正落地时再更新对应 read-set 和 owner 链。

## 5. Set 1–10

### Set 1 晨光圣骑士（`dawn_paladin_set`）

- **拓扑：仪式/领域 `2/5/8/10`。** 晨光和殉道是团队领域，不应在四件时直接完成复活式救援；冻结底盘 AC11/攻2/总价132000，采用保守预算。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `radiant=half`；`saving_throw_willpower +1`。相同 `half` 不重复。 |
| 5 | **黎明守望**：2 格 aura；自身与盟友对 `frightened/charmed` 豁免 +1。若正式环境是 dawn，aura 内对 fiend/undead 的攻击检定再 +1。 |
| 8 | **殉道救援**：自动触发 0 AP，每场战斗 1 次；自身或 2 格内盟友在一次结算后首次降至 30% HP 以下时，为该范围内至多 4 名友方各授予 `8 + max(wisdom_modifier,0)` 临时 HP；同一次 HP transition 只触发一次。 |
| 10 | **晨光化身**：主动 2 AP，`per_world_day 1`，300 TU，3 格 aura；对 fiend/undead 的攻击加值替换为 +2，每个 qualifying primary direct segment 额外 +1D6 radiant；启动时至多 4 名友方各获得 12 临时 HP。 |

- **单件交互**：胸甲 20% HP 的“殉道者之光”和 8 件效果进入 `dawn_martyr_rescue` 事件互斥组；同一 HP transition 只结算数值较高者，仅胜出来源消费自己的 usage。饰品的黎明攻击/豁免 aura 与套装 aura 进入对应 replacement group，不相加；饰品恐惧主动保留独立日用量。
- **Typed owner / gap**：threshold modifier/trait + environment-conditioned aura + HP transition reaction + per-day form。缺 dawn、creature/alignment、团队 aura、临时 HP、持久 usage、preview/AI/UI/save。

### Set 2 暗影刺客（`shadow_assassin_set`）

- **拓扑：节奏/诡术 `2/3/6/9/10`。** 低档建立潜行和走位，真正“无形”留到满套；AC6 的低防底盘允许条件爆发，但不允许脱离隐匿条件常驻生效。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `check(stealth,+2)`；dim/dark 环境再 +1，使用 `shadow_stealth_bonus` 最高档，不与同类单件值相加。 |
| 3 | 每个自身行动第一次从敌人威胁边缘移出时，可忽略 1 次 opportunity attack；120 TU 冷却。 |
| 6 | hidden/invisible 时，每个 attack command 的第一个 qualifying melee primary direct segment 额外 +1D6 negative_energy；效果提交后解除由该来源提供的 hidden。 |
| 9 | 击杀敌人后，若当前位置具备正式 cover 或 dim/dark，可立即进入 hidden；`shadow_reentry` 120 TU 冷却，不能在无掩体明处凭空隐身。 |
| 10 | **无形刺客**：主动 2 AP，每场战斗 1 次，180 TU；移动可穿过单位占用格但不得结束于非法格，不触发 opportunity attack；隐匿首段骰替换为 +2D6 negative_energy，击杀可按 9 件规则重新 hidden。 |

- **单件交互**：所有“隐匿/隐形首击”伤害进入 `shadow_hidden_opener_die`，只取最高骰；单件击杀隐藏与 9/10 件共享 `shadow_reentry` 冷却，不生成两次隐藏。单件主动隐形仍保留自己的 usage，但同一时刻只保留剩余 TU 最长的 hidden/invisible 状态。
- **Typed owner / gap**：light/cover/hidden facts、weapon range type、damage origin、kill reaction、movement edge/OA 和 form status；当前均无完整 set source 贯通。

### Set 3 霜冻守望者（`frost_warden_set`）

- **拓扑：蜕变 `3/5/7/10`。** 从耐寒、覆霜、元素改写逐步进入霜龙形态；AC10/攻2/cold30 已偏高，附伤不与护手重复累加。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 3 | `freeze=half`；忽略 ice/snow difficult terrain 的移动加耗。 |
| 5 | qualifying melee primary direct segment 额外 +1D4 freeze，`frost_melee_die` replacement group。 |
| 7 | 收到 primary direct fire segment 时，在 mitigation 前把该段原始数值的 50% 改写为 freeze、余下 50% 保持 fire；两段分别走正式 mitigation，不能再次被此规则拆分。 |
| 10 | **霜龙披覆**：主动 2 AP，`per_world_day 1`，300 TU；AC +1，`frost_melee_die` 替换为 +1D6；每 120 TU 最多使 1 个受该附伤的目标获得 slow 60 TU。形态期间可主动 2 AP、1 次释放 3 格锥形 4D8 freeze，Agility save half，失败 slow 60 TU。 |

- **单件交互**：护手近战 freeze 骰与 5/10 件同组最高，不相加；胸甲 fire→freeze 改写与 7 件同组，只执行一次。任何单件霜息与满套霜息共享 `frost_breath:per_world_day:1`，满套只升级范围/骰/TU，不增加次数。
- **Typed owner / gap**：terrain、incoming segment rewrite、mitigation ordering、cone skill、slow status、persistent daily usage。

### Set 4 烈焰法师（`flame_mage_set`）

- **拓扑：终极高魔 `3/6/9/10`。** 火法连锁逐步蓄热，凤凰复生只存在于 10 件；低总价与 AC5 允许较强爆发，但 MP45 和大量单件主动要求严格次数。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 3 | `fire=half`；仅对带正式 fire damage tag 的 spell，`spell_hit +1`。 |
| 6 | **余烬接力**：直接 fire spell 造成实际 HP 伤害后，下一次在 120 TU 内施放的直接 fire spell，其每个 qualifying primary direct fire segment 额外 +1D4 fire；状态不叠层，新触发只刷新至 120 TU。 |
| 9 | **焰流回收**：一次直接 fire spell 对至少 2 个敌人造成实际 HP 伤害后恢复 8 MP；180 TU 冷却、每场最多 2 次；preview 不恢复。 |
| 10 | **凤凰复生**：自动触发 0 AP，`per_world_day 1`；normal lethal damage 结算时、execute 与禁止复活规则之后仍允许复起才触发。恢复 `clamp(round(hp_max*20%),10,30)` HP，移动到 3 格内最近合法安全格，并对原位置 3 格内敌人造成 3D6 fire（Agility save half）。 |

- **单件交互**：胸甲随机凤凰与 10 件共用 `phoenix_rebirth` 日用量和 lethal priority；满套确定性版本替换单件概率版本，绝不连续复活。单件“下一次火法附伤”和 6 件进入 `flame_followup_die`，只取最高骰；其他火焰主动保留各自 item usage。
- **Typed owner / gap**：spell/damage tag query、跨施法状态、multi-target count、MP mutation、lethal ordering、安全落点、写回与 AI preview。

### Set 5 大地守护者（`earth_warden_set`）

- **拓扑：堡垒 `4/6/8/10`。** AC12/HP15 的冻结底盘已很厚，套装层不在 2 件继续堆防御，必须投入四件才建立扎根。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 4 | `hp_max +10`；自上次自身行动起未发生位移时，`earth_stationary_ac` +1。 |
| 6 | `saving_throw_strength +2`；forced movement 成功距离减半（向下到合法格），对 prone 的 save 获得 advantage；不是免疫。 |
| 8 | **震反**：reaction 时序、0 AP；受到 melee primary direct segment 的实际 HP 伤害后，令攻击者攻击检定 -2，60 TU；`earth_counter` 120 TU 冷却。 |
| 10 | **山岳堡垒**：主动 2 AP，每场战斗 1 次，300 TU；AC +2（替换 stationary +1）、forced movement 与 prone immune；2 格盟友对 physical primary direct segment 获得固定 DR3（每段至少保留 1 伤害）。期间可按共享日用量主动 2 AP 释放一次“大地束缚”：3 格区域，Strength save，失败 restrained 60 TU。 |

- **单件交互**：单件 stationary AC 与套装 AC 同组最高；单件 forced-move immunity 若已生效，优先于 6 件减半。任何同名“大地束缚”与 10 件授权共享 `earth_grasp:per_world_day:1`，满套不增加次数；单件受击反制和 8 件每次 incoming hit 只允许一个 reaction 胜出。
- **Typed owner / gap**：stationary fact、forced-move commit、prone save、incoming-hit reaction、directionless DR、area restraint、usage owner。

### Set 6 风暴行者（`storm_walker_set`）

- **拓扑：蜕变 `3/5/7/10`。** 从耐雷到蓄电再到风暴化身；高移动、攻2、lightning30 的底盘要求 charge 必须来自真实受伤而不是自我零伤刷取。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 3 | `lightning=half`；`battle_start_progress +3`。 |
| 5 | qualifying melee primary direct segment 额外 +1D4 lightning，`storm_melee_die` 最高档。 |
| 7 | 受到实际 lightning HP 伤害后获得 1 `storm_charge`，每个 incoming event 最多 1 层、最多 3 层。一次 melee attack command 首次造成实际伤害后可消耗 1 层，使该 command 的每个 qualifying melee primary segment 额外 +1D6 lightning。 |
| 10 | **风暴化身**：主动 2 AP，`per_world_day 1`，300 TU；启动获得 3 charge、`move_capacity +2 格`；基础 `storm_melee_die` 替换为 +1D6。2 格内每个敌人每 120 TU 最多受到一次 1D6 lightning aura 伤害；storm 环境中 wearer 攻击/save +2。 |

- **单件交互**：所有蓄电来源写入同一 `storm_charge`，不得并行建多个三层池；护手/单件近战雷骰和 5/10 件取最高。单件风暴主动若名为 Storm Avatar，则与 10 件共享日用量；其他独立闪电技能保留 item usage，但其装备生成伤害不能反向充能或触发附伤。
- **Typed owner / gap**：weather producer、actual-damage fact、charge state、per-command segment association、moving aura、origin recursion guard。

### Set 7 亡灵收割者（`death_reaper_set`）

- **拓扑：稳步循环 `2/4/6/8/10`。** 击杀治疗、低血猎杀、标记和收割形态天然构成逐档循环；底盘中等，允许条件较窄时获得较高终点。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `negative_energy=half`；对 undead 攻击 +1，`death_target_attack` 最高档。 |
| 4 | 击杀 living 或 undead 敌人后恢复 `1D6 + max(wisdom_modifier,0)` HP；`death_kill_heal` 120 TU 冷却。 |
| 6 | 目标在该段结算前 HP <50% 时，每个 qualifying melee primary direct segment 额外 +1D4 negative_energy。 |
| 8 | **收割标记**：主动 1 AP，每场战斗 1 次，选择 6 格内目标，持续 180 TU；目标受到的治疗减半，wearer 对其攻击 +1；同一 wearer 只能有 1 个活动标记。 |
| 10 | **收割者显形**：主动 2 AP，`per_world_day 1`，300 TU；低血附伤替换为 +1D8；2 格内敌人首次进入及之后每 120 TU 至多一次进行 Willpower save，失败 frightened 60 TU。形态内可主动 2 AP、1 次对 6 格内目标施放 4D10 negative_energy“死亡之握”，Constitution save half，失败再拉近 2 格并 frightened 60 TU。 |

- **单件交互**：单件击杀治疗与 4 件进入 `death_kill_heal_amount`，一次 kill 只结算最高治疗；单件低血附伤与 6/10 件同组。任何同名“死亡之握”共享 `death_grip:per_world_day:1`；饰品主动吸取/治疗仍是独立 item usage，但装备生成伤害不触发 6/10 件。
- **Typed owner / gap**：creature kind、kill attribution、pre-segment HP、target mark、heal modifier、fear aura、pull、per-day form。

### Set 8 龙鳞铠甲（`dragon_scale_set`）

- **拓扑：蜕变 `3/5/7/10`。** 十件逐步让屠龙者获得龙威、吐息适应与龙血沸腾；这是 10 级以后乃至 20 级段的高阶套装，但冻结底盘 AC12/攻3/HP20/总价142000，常驻加成必须保守。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 3 | `fire=half`；`saving_throw_fear +3`；对 dragon `frightful_presence` immune。 |
| 5 | 对 dragon 攻击 +2，`dragon_target_attack` 最高档；受到 dragon breath weapon 时，对该 breath 的对应元素取得 `half` mitigation tier，相同 half 不重复。 |
| 7 | **龙鳞护阵**：2 格盟友对 dragon 的 frightful presence save +2；wearer 每场第一次在 dragon breath 后仍受到实际 HP 伤害时获得 15 临时 HP，并对该 breath source 的下一次攻击 +1（120 TU 内）。 |
| 10 | **屠龙者之誓 / 龙血沸腾**：wearer 对 dragon 的每个 qualifying primary direct segment 额外 +1D4，不限 melee、weapon、ranged 或 spell，只要求该原始段满足正式主直接来源合同；冻结护手的 +1D4 仍只限 melee weapon。两者在 qualifying melee weapon 同段可按已批准的不同来源相加为 2D4，远程/法术主段只有 set 来源 1D4。主动 2 AP、`per_world_day 1` 启动龙血沸腾 300 TU：对 dragon 攻击总值替换为 +3，并获得 3 个治疗 charge；每次真实 weapon hit 造成实际 HP 伤害后治疗 1D6 并消耗 1 层。 |

- **单件交互**：头盔/项链等对 dragon 攻击均进入 `dragon_target_attack`，只取最高；胸甲与 5 件 breath half 不乘算；胫甲和 3 件 fear save 明确保留原文批准的 `add`，因此十件时可为 +6，但 immunity 仍是布尔值。仅护手 +1D4 与 10 件 +1D4 允许相加；所有其他逐段骰走 replacement group。斗篷反射龙息的独立日用量不与 half 共享。
- **Origin 约束**：set 来源只要求 dragon 目标与已知 qualifying primary direct segment，不限攻击载体；护手来源另要求 melee weapon。重复攻击、随机链、多目标和法术多段逐真实段触发，不按 cast 去重；明确排除 extra segment、DOT/upkeep、terrain、reflection、自伤、装备/套装生成伤害与 trigger skill。
- **Typed owner / gap**：dragon/breath/fear typed facts、conditional mitigation、per-segment damage、team aura、daily charge、save/writeback、preview/AI/UI/headless。Set 8 数值不能在 set membership 尚未落地时宣称可玩。

### Set 9 铁壁要塞（`iron_bulwark_set`）

- **拓扑：堡垒 `4/6/8/10`。** 冻结底盘已达 AC13/HP40，套装强度必须来自明确的站位和反应窗口，不能再给无条件常驻大额 AC/HP。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 4 | `hp_max +10`；对 forced movement/prone 的 save +2。 |
| 6 | 自上次自身行动起没有位移时，AC +1 且 forced movement/prone immune；一旦提交自愿或强制位移立即失效，直到再次满足 stationary。 |
| 8 | **坚守阵地**：主动 1 AP，持续到下一次自身行动开始；下一次来自正式前方扇区的 direct damage event 在 reaction 时序触发、0 AP：wearer 各 primary segment 伤害减半，2 格内至多 3 名盟友同事件各减 25%。120 TU 冷却。 |
| 10 | **移动要塞**：主动 2 AP，每场战斗 1 次，300 TU；2 格 aura 内盟友 AC +1、forced movement/prone immune。wearer 每 60 TU 第一次前方 direct damage event 自动使用 8 件减伤且不消耗 reaction opportunity；收到 critical hit 时有 50% 将其降为 normal hit，每个 hit 只掷一次。 |

- **单件交互**：单件 stationary AC、盾墙 aura 与 6/10 件分别进入 `bulwark_stationary_ac` / `bulwark_aura_ac`，只取最高；单件前方减伤与 8/10 件一次 incoming event 仅允许一个 mitigation reaction。所有暴击降级概率进入 `bulwark_crit_downgrade_bp`，最高 50%，不得分别掷多次。单件“掩护盟友”的独立日用量不被套装消费。
- **Typed owner / gap**：stationary/movement commit、facing arc、per-segment mitigation、adjacency aura、critical override、reaction/cooldown、AI 位置估值。

### Set 10 古代帝王（`ancient_emperor_set`）

- **拓扑：仪式/领域 `2/5/8/10`。** 帝王权威以目标 CR、臣属 aura 和敕令逐步扩张；十件总价150000且静态社交极高，因此所有战斗收益都有目标或领域条件。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `check(persuasion,+3)`；对 challenge rating 严格低于 wearer effective level 的目标攻击 +1。 |
| 5 | **威仪**：2 格敌方 aura；符合低 CR 条件的敌人 AC -1。多名帝王对同一目标不叠加，`emperor_enemy_ac_penalty` 最高取 -1。 |
| 8 | **帝王敕令**：主动 1 AP，每场战斗 1 次；3 格内至多 4 名盟友下一次在 120 TU 内的攻击 +2；范围内敌人进行 Willpower save，失败 frightened 60 TU。 |
| 10 | **王域**：主动 2 AP，`per_world_day 1`，300 TU，4 格 aura；友方攻击/save +1；低 CR 敌人的 AC penalty 替换为 -2；wearer 对低 CR 目标的每个 qualifying primary direct segment 额外 +1D6 radiant。 |

- **单件交互**：所有“对低 CR 攻击”与敌方 AC 压制进入各自 replacement group；不会因戒指、护甲与套装重复而突破 +2/-2。单件玉玺“临时能力”与 8 件号令若都授权额外攻击，同一单位同一行动窗口最多获得 1 次额外攻击；各自 usage 独立，只有胜出授权消费。社交自动成功仍由单件自己的日用量拥有。
- **Typed owner / gap**：effective level/CR typed query、aura stacking、ally next-attack grant、fear save、country/world authority 与持久领域 usage。

## 6. Set 11–20

### Set 11 星辰织者（`star_weaver_set`，P0 阻断）

- **拓扑：终极高魔 `3/6/9/10`。** 命运干预不适合拆成大量小档；但 Set 11/95 身份冲突未解决前，`GearSetDefinition` 不得注册这套候选。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 3 | `battle_start_progress +2`；night 环境下 `check(perception,+2)`。 |
| 6 | **命运微调**：reaction 时序、0 AP，每场战斗 1 次；可见友方完成 attack/save roll 后、结果提交前令其重掷，必须接受新结果；不能重掷 outcome table、伤害骰或已被其他命运来源改写的 roll。 |
| 9 | **星辰光环**：night 环境下 3 格友方 attack/save +1、`battle_start_progress +1`；同名 aura 只取最高。 |
| 10 | **命运织幕**：主动 2 AP，`per_world_day 1`，300 TU，启动获得 3 fate charges。每个 attack/save roll 最多消费 1 层：友方取两次结果较高、敌方取较低；normal lethal 时可消费剩余 2 层使 wearer 保留 1 HP。execute、禁止复活和已被其他 lethal interceptor 消费的事件不能使用。 |

- **单件交互**：望远镜的自动 miss/自动命中、单件/月度重掷与套装 charge 都进入 `fate_roll_transaction`；每个 roll 只允许一个 source 胜出且只消费胜出 source。月度单件 usage 与满套日用量不合并。任何“保留 1 HP”进入统一 lethal priority，单件概率与 10 件确定性不能连续触发。
- **Typed owner / gap**：P0 member identity、night、roll transaction、lethal ordering、calendar/month usage、intent preview、AI。身份未解决前状态一律 `blocked-planned`。

### Set 12 毒蛇之吻（`vipers_kiss_set`）

- **拓扑：稳步循环 `2/4/6/8/10`。** 先防毒、再施毒、利用 poisoned、展开毒雾，最终让毒伤反转；骰数、aura 半径和转化率全部使用最高档替换。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `poison=half`；对 nonmagical poison/disease status immune，不把 magical poison damage 误判为 status immunity。 |
| 4 | 每个 qualifying melee primary direct segment 额外 +1D4 poison；每个 attack command 第一个造成实际 poison HP 伤害的目标进行 Constitution save，失败 poisoned 60 TU。 |
| 6 | 对已 poisoned 目标的 qualifying primary direct segment 再额外 +1D4 poison；该目标加成是独立条件组，但同组高档只取最高。 |
| 8 | **毒雾**：主动 3 AP，`per_world_day 1`，300 TU，wearer 周围 2 格 aura；敌人首次进入及之后每 120 TU 至多一次进行 Constitution save，失败 poisoned 60 TU 并受到 1D6 poison。 |
| 10 | **万毒化身**：主动 3 AP，升级同一日用量至 3 格 aura、poison immune；`viper_melee_die` 替换为 +1D6，poisoned-target die 替换为 +1D6。原本会造成的 poison 段归零后，按被该 immunity 阻止的数值治疗 50%，每段最多 10 HP；装备生成毒伤不得治疗。 |

- **单件交互**：单件命中毒骰与 4/10 件进入 `viper_melee_die`；单件毒雾与 8/10 件共享 `viper_toxic_aura:per_world_day:1`，范围/骰/TU 取最高。单件“毒伤转治疗”与 10 件进入 `viper_poison_conversion_bp`，最高 50%，每段只计算一次。不同主动投毒技能保留自己的 item usage。
- **Typed owner / gap**：poison damage 与 poisoned/disease status taxonomy、per-command target gate、moving aura、prevented-damage fact、heal cap、origin guard。

### Set 13 北风旅者（`northwind_traveler_set`）

- **拓扑：节奏/诡术 `2/3/6/9/10`。** 主题核心是不断摆脱束缚并改变路径，完整飞行与团队自由只在 10 件；冻结移动20要求避免常驻速度继续膨胀。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `move_capacity +1 格`；忽略非魔法 difficult terrain 的额外移动成本。 |
| 3 | grabbed/restrained 时仍可用一半 movement，并对 escape check 获得 advantage；不能穿过本来非法的 edge。 |
| 6 | 每 120 TU 1 次，在一次自愿移动累计至少 2 格后，可追加 1 格合法位移且不触发 opportunity attack。 |
| 9 | **北风闪步**：reaction 时序、0 AP，每场战斗 2 次；成为攻击目标后、命中检定前，传送至 3 格内合法可见格；若无合法格不消费次数。攻击随后按新位置重新验证 range/line。 |
| 10 | **自由之风**：主动 2 AP，`per_world_day 1`，300 TU；获得 `flight_move_capacity = 6 格`，grabbed/restrained 与 falling damage immune；2 格盟友 `move_capacity +2 格` 且 falling damage immune。 |

- **单件交互**：所有 movement bonus 与飞行速度按 `northwind_move` / `northwind_flight_speed` 最高值，不把“翻倍”与平面值反复乘算。任何单件 flight 与 10 件共享 `northwind_flight:per_world_day:1`，满套升级团队 aura 而不增加一次飞行。传送单件 usage 与 9 件独立，但同一 incoming attack 只能选择一次位置改写。
- **Typed owner / gap**：world/battle traversal、grab/restrain escape、edge/path/OA、targeted pre-hit teleport、flight/fall、team movement aura。

### Set 14 虚空行者（`void_walker_set`）

- **拓扑：蜕变 `3/5/7/10`。** 从空间锚定、闪避到局部相位，最终完整虚体；force25/MP35 底盘使完整免疫必须附带来源限制和代价。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 3 | `force=half`；对敌方 forced teleport/banish displacement immune，自愿传送不受影响。 |
| 5 | **虚空闪步**：reaction 时序、0 AP，每场战斗 2 次；成为 direct attack 目标后、roll 前传送至 2 格内合法格，随后重验攻击。无合法格不消费。 |
| 7 | 使用传送后进入 phase 30 TU：可在移动中穿过单位与一条可穿越墙 edge，但不得结束于非法格；期间 nonmagical physical primary direct damage 获得 `half`。每 120 TU 最多进入一次 phase。 |
| 10 | **虚空化身**：主动 2 AP，`per_world_day 1`，300 TU；nonmagical physical primary direct damage `immune`，true sight 12 格，可按 7 件规则穿墙。每 120 TU 结束时进行 Willpower DC15；失败 confused 60 TU。形态中可主动 2 AP、1 次对 2 格内目标施放 3D10 force“虚无之触”，Constitution save half，失败使其一种 command category 锁定 60 TU。 |

- **单件交互**：单件 20% void dodge 与 5 件传送在每个 incoming attack 的 `void_avoidance` 选择窗只允许一个；若选择传送则不再掷 dodge。单件虚无之触与 10 件共享 `void_touch:per_world_day:1`，只取较高骰/持续。所有 phase/ethereal 状态按剩余 TU 最长保留，不叠加 mitigation。
- **Typed owner / gap**：pre-hit relocation transaction、barrier/edge phase、targetability、damage material/origin、true sight、command category lock、post-use save。

### Set 15 自然之语（`natures_whisper_set`）

- **拓扑：仪式/领域 `2/5/8/10`。** 自然关系、治疗、召唤最终汇入森林领域；较弱战斗底盘允许以受控召唤和区域持续力补强。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `check(animal_handling,+3)`；未被 wearer 或友方伤害的普通 animal 初始态度不低于 neutral。 |
| 5 | natural environment 中 `move_capacity +1 格`、忽略 plant difficult terrain；若连续 120 TU 未移动且未受实际 HP 伤害，恢复 2 HP，每 120 TU 最多一次。 |
| 8 | **翠绿仪式**：主动 3 AP，`per_world_day 1`，300 TU；召唤 1 个正式 `nature_spirit` 模板，完全遵守 summon cognition、数量与指令规则；wearer 同时最多 1 个该来源召唤物。 |
| 10 | **森林领域**：主动 3 AP，升级同一 `verdant_rite` 日用量；不再额外增加使用次数。4 格区域持续 300 TU：至多 2 个 nature spirit；友方每 120 TU 首次在区域开始自身行动时恢复 1D6 HP，敌方区域为 difficult terrain，首次进入时 Strength save，失败 restrained 60 TU。 |

- **单件交互**：单件 stationary regeneration 与 5 件进入 `nature_stationary_regen`，只取最高恢复量/最短合法 cadence 中设计指定的一档，不并行 tick。单件自然召唤与 8/10 件若引用同一 nature spirit，进入共享 `verdant_rite` 日用量和总召唤上限 2；其他正式模板保留自己的 usage，但所有装备/set summon 合计仍受性能上限。
- **Typed owner / gap**：world relation、environment、stationary/no-damage clock、summon templates/cognition/control、moving/placed area、periodic heal、plant restraint。

### Set 16 深渊凝视（`abyss_gazer_set`）

- **拓扑：终极高魔 `3/6/9/10`。** 识破、凝视、压制 aura 最终才打开深渊召唤；AC9/psychic25 要求召唤和 aura 都有硬上限。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 3 | blinded immune；对 illusion/disguise 的识破 check advantage。 |
| 6 | 对 aberration/eldritch 攻击 +2；主动 1 AP、每场战斗 1 次凝视 3 格内目标，Willpower save，失败 frightened 60 TU。 |
| 9 | **深渊压迫**：2 格敌方 aura，攻击 -1。来自多名 wearer 的同类 penalty 对单目标总计最多 -2；离开 aura 立即重算而非保留 stale modifier。 |
| 10 | **凝视深渊**：主动 3 AP，`per_world_day 1`，300 TU；召唤 1 个正式 abyss aberration 模板，基础 cognition 必须显式声明，wearer 同时最多 1 个。形态期间 true sight 6 格、water breathing；结束时 Constitution DC15，失败 `hp_max -10`，持续到一次正式 long rest。 |

- **单件交互**：单件恐惧凝视与 6 件一次 target/event 只执行一个 save；各自 usage 独立。任何 abyss summon 与 10 件共享 `abyss_summon:per_world_day:1` 和 `max_summons=1`，不能先单件召唤再由满套召第二个。识破/true sight 取感知范围最高，不累加范围。
- **Typed owner / gap**：vision/illusion、creature tags、stack-capped aura、summon/control、rest-persistent penalty、world water traversal、save/writeback。

### Set 17 圣光使者（`lightbringer_set`）

- **拓扑：仪式/领域 `2/5/8/10`。** 先建立抗恐与对邪恶职责，再由一次圣光爆发升级为持续团队领域；AC11/攻2/双抗底盘采用保守团队预算。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | frightened immune；`radiant=half`。 |
| 5 | 对 undead/fiend 攻击 +2；主动 1 AP、每场战斗 1 次触摸 1 格内友方，治疗 `2D8 + max(wisdom_modifier,0)` 并移除 poisoned 或 frightened 中 1 个。 |
| 8 | **圣光爆发**：主动 3 AP，`per_world_day 1`，3 格半径；敌人 4D6 radiant、Agility save half，失败 blinded 60 TU；至多 4 名友方各治疗 2D8。 |
| 10 | **光之领域**：主动 3 AP，升级同一 `lightburst` 日用量；启动时执行 8 件爆发，随后 3 格 aura 持续 300 TU：友方对 negative_energy/poison primary segment 获得固定 DR5（每段至少 1），attack/save +1；区域内正式 magical darkness 在能力差异允许时被 dispel。 |

- **单件交互**：单件治疗触摸仍保留 item usage；同一治疗 command 不重复结算 set heal。单件 radiant hit bonus 和套装对邪恶加成属于不同维度，可同时满足，但所有逐段 radiant 骰在 `lightbringer_radiant_die` 中最高取一档。单件光爆与 8/10 件共享 `lightburst:per_world_day:1`。
- **Typed owner / gap**：fiend/undead、typed cleanse、mixed hostile/friendly AoE、blind、damage-tag DR、darkness dispel、team aura、daily upgrade。

### Set 18 永恒学徒（`eternal_apprentice_set`）

- **拓扑：终极高魔 `3/6/9/10`。** 学习、准备、临时技能替换和能力复制是内容/运行时边界问题，不适合伪装成静态属性；AC5 的低战斗底盘允许一次性高策略上限。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 3 | arcana/history/investigation/nature/religion 各 `check +3`；这些同类知识 bonus 取最高，不与单件重复相加。 |
| 6 | `mp_max +15`；非战斗学习与 mundane crafting 的时间和非稀有材料消耗各降 25%，结果分别向上取整，不影响任务门槛、稀有核心或 skill mastery gate。 |
| 9 | **临时准备**：battle start 选择、0 AP，每场战斗 1 次；从角色已经正式学会但未装备的技能中选择 1 个，替换一个已装备技能 300 TU；不改变学习、mastery、冷却或持久 loadout，替换前后的 cooldown 独立保存并恢复。 |
| 10 | **无限研习**：`per_world_day 1`；观察敌人本场成功使用的 1 个可复制 active skill 后，生成一次性临时授权，使用 wearer 自身 attack/DC，基础 damage/heal 为原 definition 的 100%，并支付被复制 skill definition 的原始 AP；禁止复制 summon capstone、lethal rewrite、装备能力、脚本特例或需要缺失资源的技能，战斗结束失效。 |

- **单件交互**：模仿戒指与 10 件共享 `temporary_skill_copy:per_world_day:1`，满套把 75% 系数替换为 100%，不额外复制一次。无限之书的“学习/记忆/预言/治愈”保留自己的 item usage；若选择复制类分支，同一观察事件最多生成一个授权。单件语言阅读不与知识 check 数值相加。
- **Typed owner / gap**：progression known/equipped distinction、temporary grant snapshot、cooldown restoration、copy allowlist、definition dependency、world crafting、AI skill valuation/save exclusion。

### Set 19 血月猎人（`blood_moon_hunter_set`）

- **拓扑：蜕变 `3/5/7/10`。** 追踪、银伤、抑制再生最终进入血月猎杀形态；攻击收益严格绑定 shapechanger，避免高静态底盘在所有敌人上泛化。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 3 | 对 shapechanger 的 tracking check +3、perception +3；正式 blood moon 环境下 Strength/Agility check +1。 |
| 5 | 对 shapechanger 攻击 +2；每个对 shapechanger 的 qualifying weapon primary direct segment 额外 +1D4 radiant，视为 silver-tagged 以供目标规则查询。 |
| 7 | 受到 5 件银伤的目标，其 regeneration/heal-over-time 被 suppress 120 TU；命中只刷新，不叠层。wearer 的 lycanthropy negative status 在装备阈值存在时 suppressed，但不从存档删除。 |
| 10 | **血月猎形**：主动 2 AP，`per_world_day 1`，300 TU；Strength/Agility check bonus 替换为 +2，对 shapechanger 攻击替换为 +3，银伤骰替换为 +1D8；正式 blood moon 下启动不消费额外次数，也不延长 300 TU。 |

- **单件交互**：所有 silver/shapechanger 逐段骰进入 `bloodmoon_silver_die`；不与护手等相同来源相加。单件追踪、再生抑制和 lycanthropy suppress 与套装共享状态/replacement group，命中只刷新至较长 TU。任何单件血月形态与 10 件共享 `bloodmoon_form:per_world_day:1`。
- **Typed owner / gap**：shapechanger/werewolf、silver damage provenance、moon phase、regeneration suppression、persistent curse projection、form usage。

### Set 20 锈蚀齿轮（`rusted_gear_set`）

- **拓扑：蜕变 `3/5/7/10`。** 从工匠/机器关系逐步替换肉身；构装免疫和致死复起不能继续在 6 件前置。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 3 | artisan/investigation check +3；可理解正式 machine/construct protocol，但不自动控制目标。 |
| 5 | 对 construct 攻击 +2；每 120 TU 1 次，可主动 1 AP 修复 1 格内 construct/object `1D8 + max(intelligence_modifier,0)` durability/HP；不能治疗普通生物。 |
| 7 | **构装外壳**：主动 2 AP，每场战斗 1 次，180 TU；AC +1，poison/disease status immune，`move_capacity -1 格`；普通 healing 对 wearer 仅有 50%，repair/heal-with-construct-tag 正常。 |
| 10 | **齿轮化身**：主动 2 AP，`per_world_day 1`，300 TU；AC bonus 替换为 +2，poison/disease/bleed immune，`move_capacity -1 格`；每 120 TU 自动 self-repair 1D8，最多 3 次。形态内第一次 normal lethal 且仍允许复起时，终止形态并恢复 1 HP，Constitution -1 至正式 long rest；execute 不被拦截。 |

- **单件交互**：任何单件 construct form 与 7/10 件共享 `construct_form:per_world_day:1` 时取最高 AC/免疫/TU，不并行两种形态；若原单件是 per-battle，则只在它实际胜出时消费自己的 pool。单件 10% 半机械复起与 10 件进入统一 lethal group，满套确定性版本替代概率版本且同一事件只触发一次。修理效果只取每次 command 最高值。
- **Typed owner / gap**：tool/crafting、construct relation/control、repair vs heal taxonomy、form cognition/status、periodic repair、lethal priority、long-rest penalty/writeback。

## 7. Set 21–30

本节明确废止 `sets_21_to_30_armor.md` 内历史“2件套/4件套（设计预留）”作为并行阈值源。它们只用于理解银月、沙漠、丛林、高山、海盗、悬赏、瘟疫、月影、血骑士与幽灵骑士的主题；正式候选只使用下列拓扑。

### Set 21 银月游侠（`silvermoon_ranger_set`）

- **拓扑：节奏/诡术 `2/3/6/9/10`。** 感知、射距、月标与齐射按远程节奏展开；AC9/攻2/dodge2 的高底盘要求齐射限制目标数和每战次数。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | ranged weapon attack +1；`check(perception,+2)`。 |
| 3 | ranged weapon 的 normal/max range 各 +3 格；night 环境下攻击忽略 dim-light concealment penalty，但不获得 true sight。 |
| 6 | ranged weapon attack 替换为 +2；ranged weapon `crit_threshold_delta -1`，不影响 spell 或非武器投射物。 |
| 9 | **月之标记**：on-hit 自动触发 0 AP，每 120 TU 1 次；ranged weapon hit 后标记目标 60 TU；wearer 对该目标下一次 qualifying ranged primary direct segment 额外 +1D6 radiant，命中后消费 mark。wearer 同时最多 1 个 mark。 |
| 10 | **银月齐射**：on-hit 选择、0 AP，每场战斗 2 次；一次 ranged weapon attack 命中后，在原攻击全部主段结算完毕后，选择主目标 1 格内至多 2 个其他敌人，各造成 1D8 radiant。night 环境替换为 2D8；这是 set-generated damage，不触发任何 on-primary-segment 附伤。 |

- **单件交互**：单件远程攻击、射程、crit threshold 与 2/3/6 件分别进入对应 replacement group；不会相加突破 +2/-1。单件隐形箭/无声足迹保留原 usage。任何同名齐射与 10 件共享 `silvermoon_volley:per_battle:2`；一次原始命中只创建一个齐射 batch。
- **Typed owner / gap**：weapon_range_type、night/light、range profile、target mark、adjacency target selection、post-damage command、origin exclusion、AI multi-target estimate。

### Set 22 沙漠蝎刺（`desert_scorpion_set`）

- **拓扑：节奏/诡术 `2/3/6/9/10`。** 沙地隐匿建立毒刺窗口，满套才出现麻痹式终结；中央“沙漠蝎”与成员“沙漠蝎刺”的显示名漂移不得转化为第二套 ID。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `fire=half`；desert/sand 环境 `check(stealth,+2)`。 |
| 3 | hidden 时每个 attack command 的第一个 qualifying melee primary direct segment 额外 +1D4 poison；目标 Constitution save，失败 poisoned 60 TU。 |
| 6 | attack +1；`scorpion_opener_die` 替换为 +1D6；在 sand terrain 移动不留可追踪足迹且忽略其 difficult terrain。 |
| 9 | 对 poisoned 目标 attack +1；击中后可移动 1 格且不触发该目标的 opportunity attack，120 TU 冷却。 |
| 10 | **蝎王毒刺**：on-hit 选择、0 AP，每场战斗 2 次；hidden melee hit 的首个 qualifying segment 把 opener 总骰替换为 +2D6 poison。目标 Constitution save；失败 poisoned 120 TU，失败差值 ≥5 时另 paralyzed 30 TU。 |

- **单件交互**：所有“隐藏首段毒伤”进入 `scorpion_opener_die`，最高取 2D6；单件 poisoned/paralyzed 与 3/10 件一次命中只进行一次 save，取最高 DC/持续。若单件蝎刺也有次数，则与 10 件共享 `scorpion_sting:per_battle:2`；普通投毒主动保留 item usage。
- **Typed owner / gap**：desert/sand producer、hidden opener、per-command first segment、poison status/save margin、paralyzed semantic、movement/no-track world projection。

### Set 23 丛林猎豹（`jungle_panther_set`）

- **拓扑：节奏/诡术 `2/3/6/9/10`。** 通过移动距离启动扑击，再把击杀转成有限连猎；不会在四件直接授予完整额外攻击循环。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `move_capacity +1 格`；jungle/forest 环境 `check(stealth,+2)`。 |
| 3 | 本 command 在命中前自愿移动至少 2 格时，第一个 qualifying melee primary direct segment 额外 +1D4 slashing。 |
| 6 | move capacity 总替换为 +2 格、attack +1；一次 melee hit 后可向合法格移动 1 格，不触发该目标 opportunity attack，120 TU 冷却。 |
| 9 | **黑豹扑袭**：自动判定 0 AP，每 120 TU 1 次；本 command 命中前直线移动至少 4 格后，命中追加 1D8 slashing，并施加 bleed：1D6 physical_slashing upkeep，120 TU、每 60 TU tick，最多 1 层。 |
| 10 | **猎豹连猎**：主动 1 AP，每场战斗 1 次，180 TU；crit/kill 后可移动最多 2 格并对不同目标发动一次 0 AP immediate melee weapon attack；一次 activation 最多 3 次额外攻击、同一目标最多 1 次，额外攻击不能再次触发本链。 |

- **单件交互**：单件爪击/流血与 3/9 件使用 `panther_pounce_damage` / `panther_bleed` 最高值，不能同时生成两份 bleed tick。任何单件 disengage movement 与 6 件同一 hit 只提交一次移动。单件额外攻击和 10 件链每个 kill/crit 只选一个 immediate-attack source。
- **Typed owner / gap**：committed path distance、straight-line query、OA-safe move、bleed typed upkeep、crit/kill chain、target memory、extra-attack recursion cap。

### Set 24 高山雄鹰（`highland_eagle_set`）

- **拓扑：节奏/诡术 `2/3/6/9/10`。** 射程和高度优势逐步累积，双倍基础武器骰只在满套的有限俯冲窗口出现；中央/成员中文名漂移不产生别名 membership。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `check(perception,+3)`；ranged weapon normal/max range +3 格。 |
| 3 | wearer 高于目标至少 2 格时，ranged weapon attack +1；falling damage 减半。 |
| 6 | ranged weapon attack 总替换为 +2；忽略 half cover，但不忽略 full cover 或 barrier。 |
| 9 | **鹰眼标记**：主动 1 AP，每场战斗 1 次，标记 12 格内可见目标 300 TU；对其忽略 three-quarters cover，且满足 2 格高度差时每个 qualifying ranged weapon primary segment +1D4 piercing。wearer 同时 1 个 mark。 |
| 10 | **高空俯击**：随 ranged weapon attack 声明、无额外 AP，每场战斗 2 次；wearer 必须高于目标至少 2 格。命中后只把该攻击的 base weapon dice 乘 2；属性、trait、额外段、mark、暴击与装备伤害都不乘。与 critical 同时发生时分别基于原始 base dice 计算，不做指数乘算。 |

- **单件交互**：单件远射/高地 attack 与阈值取最高；单件俯冲或基础骰倍率与 10 件进入 `eagle_base_weapon_dice_multiplier`，最高 x2。鹰眼 mark 若已有单件来源则复用同一 target mark，刷新 TU 但不刷新各自 usage；同一攻击不允许两个“俯击”消费。
- **Typed owner / gap**：height/elevation、weapon base dice decomposition、cover/barrier、mark lifecycle、critical multiplier ordering、AI reachable-height planning。

### Set 25 海盗船长（`pirate_captain_set`）

- **拓扑：仪式/领域 `2/5/8/10`。** 航海生存、登船稳定、号令最终升格为船长领域；战斗预算主要给队伍，不复制饰品中的大量社交/海上主动。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `check(athletics,+3)`；`acid=half`；正式 ship/water terrain 上 movement penalty 减半。 |
| 5 | 2 格盟友对 forced movement/prone save +2；wearer climb/swim move capacity 等于 land move capacity，但不授予水下呼吸。 |
| 8 | **船长号令**：主动 1 AP，每场战斗 1 次；3 格内至多 4 名盟友下一次在 120 TU 内的 weapon attack +2；wearer 获得一次 120 TU 内可用的 0 AP immediate melee weapon attack，命中为 1D8 + Strength modifier slashing。 |
| 10 | **升旗领域**：主动 3 AP，`per_world_day 1`，300 TU，4 格 aura；友方 attack/save +1、`move_capacity +2 格`、每 120 TU 第一次自愿移动不触发 opportunity attack；每名友方每 120 TU 的第一个 qualifying weapon primary segment 额外 +1D4 slashing。 |

- **单件交互**：单件船长号令与 8 件共享 `captain_command:per_battle:1`；同一 ally next-attack buff 只取最高。玉玺/徽记若授权额外攻击，与 8 件同一行动窗最多保留一个 extra attack。海怪召唤和航海主动保留 item usage，但所有 summon 遵守总数预算。
- **Typed owner / gap**：ship/water environment、swim/climb movement、team aura、next-attack grant、bonus attack、per-ally cadence、world/day usage。

### Set 26 赏金猎人（`bounty_hunter_set`）

- **拓扑：稳步循环 `2/4/6/8/10`。** 感知、标记、压制治疗、低血追猎最后才形成处决；每个目标和每场战斗都有独立记忆，不能靠反复换标刷新。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `check(survival,+3)`、`check(perception,+2)`。 |
| 4 | **赏金标记**：主动 1 AP，标记 6 格内可见目标 300 TU；attack +1。wearer 同时最多 1 个 set mark，每场最多应用 2 次。 |
| 6 | 对标记目标每个 attack command 的第一个 qualifying primary direct segment +1D4 physical_pierce；目标 healing received -25%。 |
| 8 | 标记目标在 segment 前 HP <30% 时，对其 attack bonus 替换为 +2，附伤替换为 +1D6；每个目标每 120 TU 最多领取一次该附伤。 |
| 10 | **死亡悬赏**：随攻击声明、无额外 AP，每场最多 2 次、同一 target 1 次；对 HP <30% 的标记目标声明攻击时，attack bonus 替换为 +3，命中的第一个 qualifying segment 附加 +2D6 negative_energy。若该攻击击杀目标，可把剩余 mark TU 转移给 6 格内另一可见敌人，转移 0 AP，但不刷新 set 的应用/处决次数。 |

- **单件交互**：单件 24 小时 bounty mark 与 set mark 复用同一 typed target-mark family；同时存在时显示剩余 TU/世界时长较长者，但各 source usage 不刷新。所有低血攻击/附伤进入 `bounty_execute_attack` / `bounty_execute_die`，一次 segment 只取最高。单件束缚/追踪工具仍独立。
- **Typed owner / gap**：world-duration 与 battle-TU mark、HP precondition、heal modifier、per-target usage、mark transfer、kill attribution、persistent bounty UI/save。

### Set 27 瘟疫医生（`plague_doctor_set`）

- **拓扑：仪式/领域 `2/5/8/10`。** 医疗与感染是同一知识的两面，满套把可控毒雾升级为有限传播的疫区；不能让每 tick 无上限复制目标。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `poison=half`；`check(medicine,+3)`。 |
| 5 | **治疗性接触**：主动 1 AP，作用于 1 格内友方；治疗 2D6 并移除 poison 或 disease 中 1 个。每个 target `per_world_day 1`，不能作用于 wearer 主动制造的召唤物以刷资源。 |
| 8 | **瘟疫云**：主动 3 AP，`per_world_day 1`，300 TU，固定 3 格半径区域；友方 immune。敌人首次进入及每 120 TU 至多一次 Constitution save，失败 poisoned 60 TU 并受到 1D6 poison。 |
| 10 | **受控大疫**：主动 3 AP，升级同一 `plague_cloud` 日用量为 4 格半径；失败者另获 infection 360 TU，每 120 TU 受到 1D6 poison。每个 infection tick 最多向 1 格内 1 个尚未感染的敌人传播，进行独立 Constitution save；每个原 infection 总计最多传播 2 次，不形成装备伤害递归。 |

- **单件交互**：单件触摸治疗/伤害不与 5 件同一 command 重复结算，各自 usage 独立。所有 plague infection 使用 `plague_infection` replacement group；同一目标只保留威力最高、剩余 TU 最长的一份，不并行 tick。单件毒雾与 8/10 件共享 `plague_cloud:per_world_day:1`。
- **Typed owner / gap**：disease taxonomy、per-target world usage、placed area、periodic status、bounded spread graph、friendly immunity、origin/recursion、save/writeback。

### Set 28 月影舞者（`moonshadow_dancer_set`）

- **拓扑：节奏/诡术 `2/3/6/9/10`。** 舞步从移动防御进入 miss 反应、月下反击和持续终舞；不会在四件同时给 attack、AC、反击、隐匿完整闭环。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `check(acrobatics,+3)`、`check(stealth,+2)`。 |
| 3 | 本 command 自愿移动至少 2 格后 AC +1，持续到下一次自身行动开始；`moonshadow_motion_ac` 不叠层。 |
| 6 | reaction 时序、0 AP；每 120 TU 第一次敌方 melee attack miss wearer 后，可移动 1 格至合法格且不触发该敌人的 opportunity attack。 |
| 9 | **月下回舞**：reaction 时序、0 AP，night 环境下每场战斗 2 次；敌方 melee attack miss 后发动一次 immediate melee weapon attack，命中额外 +1D8 radiant；不与 6 件移动同时选取。 |
| 10 | **月影终舞**：主动 2 AP，`per_world_day 1`，300 TU；`move_capacity +2 格`、不触发 opportunity attack；每次 qualifying melee hit 后可移动 1 格。每 60 TU 第一次敌方 miss 可在具备 cover 或 dim/dark 时 hidden，并恢复 1 个已消耗的 9 件回舞次数，但整个 activation 最多恢复 1 次。 |

- **单件交互**：单件移动 AC 与 3 件最高取一档；任何 miss reaction 在“移动、回击、单件闪避”中只选一个并只消费胜出 source。单件自动隐藏和 10 件共享 `moonshadow_hide` 60 TU cadence，不能在明处产生 hidden。舞蹈攻击造成的迷惑 save 独立，不与 radiant 附伤合并。
- **Typed owner / gap**：committed movement、incoming miss event、reaction choice、night/cover/hidden、OA-free steps、usage restoration cap、AI counterattack risk。

### Set 29 血骑士（`blood_knight_set`）

- **拓扑：稳步循环 `2/4/6/8/10`。** 体魄、吸血率、濒死逐段爆发和确定性血契逐档成长。所有比例、段治疗上限和低血骰都用 replacement group；这是对成员文档与中央表冲突的唯一新候选。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `hp_max +15`；`saving_throw_constitution +2`。 |
| 4 | qualifying primary direct damage segment 造成实际 HP 损失后治疗其 10%，向下取整，每段最多 8 HP；不足 1 不治疗。 |
| 6 | `blood_lifesteal_bp` 替换为 15%，每段治疗上限替换为 12 HP。 |
| 8 | 吸血替换为 20%，每段上限 16 HP；该段结算前 wearer HP <50% 时，每个 qualifying melee primary direct segment 额外 +1D4 negative_energy。 |
| 10 | 吸血替换为 25%，每段上限 20 HP；wearer HP <25% 时吸血替换为 30%，低血附伤替换为 +2D4。自动触发 0 AP，每场战斗 1 次、lethal priority 100：normal lethal 后若仍允许复起，恢复 `clamp(round(hp_max*10%),10,20)` HP，并在下一次自身行动开始获得 +1 AP；execute 不被拦截。 |

- **单件交互**：所有 percentage lifesteal 对同一段只取 `blood_lifesteal_bp` 最高值，不把戒指、护甲、套装分别治疗；固定治疗、主动治疗与击杀治疗仍可独立，但冻结单件若禁止 normal healing，该禁令继续生效，lifesteal 仅在原文允许时作为非 normal-heal 来源。单件 25% 随机复起与 10 件共享 `blood_revival:per_battle:1`，确定性 capstone 替代概率版；换装不刷新。
- **Origin 约束**：吸血与低血附伤只认已知主直接段；排除 overkill、extra segment、DOT/upkeep、terrain、reflection、自伤、装备/set-generated 和 trigger skill。低血判断在每段结算前独立快照，真实多段不得按 cast 去重。
- **Typed owner / gap**：actual HP damage、heal basis points/cap、pre-segment HP、lethal ordering、next-turn AP、per-battle persistent source、preview/AI mutation safety。

### Set 30 幻影骑士（`phantom_rider_set`；成员文档名“幽灵骑士”）

- **拓扑：蜕变 `3/5/7/10`。** 用“幽影骑术→相位冲锋→幻影骑士形态”统一中央坐骑和成员幽灵主题；不创建第二个兼容名称或 tag。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 3 | `move_capacity +2 格`；`saving_throw_agility +2`；mounted 时 mount 同步获得该 move-capacity bonus，但不重复叠给 rider。 |
| 5 | 本 command 在命中前自愿移动至少 3 格时，第一个 qualifying melee weapon primary segment +1D6 physical_pierce；120 TU 冷却。 |
| 7 | **相位冲锋**：主动 1 AP，每场战斗 2 次；下次最多 4 格移动可穿过单位占用格和一条允许 phase 的 edge，不触发 opportunity attack 且不能结束在非法格。随后 melee hit 使目标 Strength save，失败 push 1 格并 prone 60 TU。 |
| 10 | **幻影骑士化身**：主动 2 AP，`per_world_day 1`，300 TU；move-capacity bonus 替换为 +4 格，7 件 phase move 每 120 TU 可用且不消耗 7 件次数。当前 command 已移动至少 2 格时，直到下一次自身行动开始对 nonmagical physical primary direct damage 获得 `half`。形态内前两次满足 3 格的 charge 把 base weapon dice x2；extra dice/segment 不乘。 |

- **单件交互**：单件 ghost/phantom form 与 10 件共享 `phantom_form:per_world_day:1`，取最高 movement/mitigation/TU；单件冲锋与 5/7/10 件在每次 attack 只选一个 charge damage/base-dice multiplier。分身等独立单件主动保留自己的 usage，但 clone 攻击不能触发 wearer 的 charge 或逐段附伤。
- **Typed owner / gap**：mounted rider/mount source、path distance、phase edge、OA、push/prone、base weapon dice multiplier、form mitigation、中文名漂移的展示决策。

## 8. Set 31–34

### Set 31 风语者（`wind_whisperer_set`）

- **拓扑：仪式/领域 `2/5/8/10`。** 总价99000、AC5 的低防底盘允许较早获得风行工具，但冻结 movement25 已很高，满套补领域控制而非继续线性堆速度。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `move_capacity +1 格`；`check(perception,+2)`。 |
| 5 | ranged weapon range +3 格且忽略 wind penalty；falling damage immune，非魔法 difficult terrain 移动加耗减半。 |
| 8 | **风暴领域**：主动 3 AP，`per_world_day 1`，300 TU，wearer 周围 3 格 aura；敌人 `move_capacity -1 格`、ranged attack -2；友方忽略非魔法 difficult terrain。 |
| 10 | **风之化身**：主动 3 AP，升级同一 `wind_domain` 日用量为 4 格；wearer AC +2、move-capacity bonus 总值替换为 +4 格；友方 ranged attack +1。敌人首次进入及每 120 TU 至多一次 Strength save，失败沿远离 wearer 的合法方向 push 1 格。 |

- **单件交互**：所有 movement/AC/range/wind-penalty 数值最高取一档。单件 Wind Storm 与 8/10 件共享 `wind_domain:per_world_day:1`，满套升级同一次领域；单件风刃每日次数保持独立，其 set-generated/装备伤害不能触发其他逐段奖励。
- **Typed owner / gap**：weather/wind、moving aura、range penalty、fall/difficult terrain、directional push、domain upgrade、AI positioning。

### Set 32 水波行者（`wave_walker_set`）

- **拓扑：仪式/领域 `2/5/8/10`。** 由水下生存进入潮汐主动，再把同一日用量升级成海域领域；低价格底盘可在正式 water 条件下获得较高上限。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | AC +1；`check(athletics,+3)`。 |
| 5 | `freeze=half`；`saving_throw_strength +2`；可水下呼吸，swim movement 等于 land movement。 |
| 8 | **潮汐冲击**：主动 3 AP，`per_world_day 1`，3 格锥形 4D8 physical_blunt，Strength save half；失败 push 2 格并 prone 60 TU。正式 underwater 环境伤害替换为 6D8。 |
| 10 | **海潮领域**：主动 3 AP，升级同一 `tidal_control` 日用量；启动仍可释放一次 8 件锥形，随后 wearer 周围 4 格持续 300 TU：友方 water breathing、`move_capacity +2 格`；敌方 movement cost x2。underwater 时友方 attack/save +1。 |

- **单件交互**：水下 AC/attack/movement 与阈值按各自 replacement group 最高取值，“移动翻倍”只对原始 land movement 计算一次。任何单件潮汐锥形与 8/10 件共享 `tidal_control:per_world_day:1`；手套水刃的三次日用量独立且不能反向触发逐段 set bonus。
- **Typed owner / gap**：water/underwater environment、breathing/traversal、cone/forced move/prone、movement cost aura、conditional dice replacement、world/battle parity。

### Set 33 地震先知（`earthquake_seer_set`）

- **拓扑：堡垒 `4/6/8/10`。** 虽总价103000，但冻结底盘有 AC9/攻2/双 save2；套装层以扎根和有限地震 pulse 为主，不能因价格低再堆常驻战斗属性。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 4 | AC +1；`saving_throw_strength +2`；站在可传导地面时 tremorsense 6 格。 |
| 6 | 自上次自身行动起未移动时 forced movement/prone immune；离地、飞行或进入不可传导 surface 时 tremorsense 与扎根立即失效。 |
| 8 | **地震踏击**：主动 2 AP，加入共享 `seismic_stomp` 日用池；2 格半径，3D8 physical_blunt，Agility save half，失败 prone 60 TU；区域变为 difficult terrain 180 TU。 |
| 10 | **地震领域**：主动 3 AP，`per_world_day 1`，300 TU，4 格固定区域；wearer tremorsense 替换为 12 格，友方 forced movement/prone immune。启动时执行一次升级踏击 4D8；失败者 prone 60 TU，失败差值 ≥5 再 stunned 30 TU；object/structure 只对该 4D8 取得 x2。 |

- **单件交互**：头盔 tremorsense、胸甲 stationary AC、靴子 prone immunity 与 4/6 件最高取值，不并行。护手/靴子/套装踏击共用 `seismic_stomp:per_world_day:3` 总池，每次只选一个版本且消费 1 层；10 件启动踏击也消费 1 层，无剩余层时只建立领域而不踏击。
- **Typed owner / gap**：ground/surface conductivity、stationary/airborne、tremorsense、shared cross-source usage、area terrain mutation、save margin stun、object damage。

### Set 34 夜幕吟游诗人（`night_bard_set`）

- **拓扑：节奏/诡术 `2/3/6/9/10`。** 技能检定、静步、音符积累、夜曲 aura 最终进入终焉曲；MP50/performance13 已很高，capstone 由日用量和 save 限制。

| 阈值 | 准确 set-bonus layer |
|---:|---|
| 2 | `check(performance,+3)`、`check(persuasion,+2)`。 |
| 3 | night 环境 `check(stealth,+2)`；自愿移动不产生普通 footstep sound，但不等于 invisible、pass-without-trace 或无法被其他感官追踪。 |
| 6 | 每个自身已提交 command 最多获得 1 `night_note`：成功 performance-based skill、造成实际 thunder/psychic HP 伤害或使敌人 charmed 时获得；最多 3 层、180 TU，刷新但不逐层独立计时。主动 1 AP 消耗 3 层，令 2 格内友方下一次 120 TU 内的 attack/save +2。 |
| 9 | **夜幕合奏**：主动 2 AP，每场战斗 1 次，180 TU，2 格 aura；友方 `move_capacity +2 格`、attack +1；敌人首次进入时 Willpower save，失败 charmed 60 TU，同一 activation 每目标只检定一次。 |
| 10 | **终焉夜曲**：主动 3 AP，`per_world_day 1`，3 格半径；敌人 4D8 psychic，Willpower save half，失败 charmed 120 TU；至多 4 名友方获得 `move_capacity +2 格`、attack +1，300 TU。施放后获得 3 notes，但本次终曲自身不能用这些 notes 再增强自己。 |

- **单件交互**：单件夜间 charisma/stealth、静步、音波与套装同类属性/状态取最高，不把“无声”升级为免疫所有追踪。单件音波主动 usage 独立；若同一音波同时满足 note 的多个条件仍只得 1 层。单件歌曲 aura 与 9/10 件 attack/movement replacement group 最高取一档；同一目标一次进入只做一个 charm save。
- **Typed owner / gap**：night/audibility、performance source、bounded note state、charm immunity/save、team aura、mixed hostile/friendly finale、daily usage、AI note planning。

## 附录 A：`GearSetDefinition.member_item_ids[10]` 冻结清单

正式计数必须逐 ID 对照本表；表内顺序固定为 `head/body/hands/feet/cloak/necklace/ring_1/ring_2/special_trinket/badge`。不得读取 tag 推断成员，不得要求修改 ItemDef，也不得把显示名漂移转换成兼容别名。

| Set | `member_item_ids[10]` |
|---:|---|
| 1 | `armor_dawn_paladin_helm`<br>`armor_dawn_paladin_body`<br>`armor_dawn_paladin_hands`<br>`armor_dawn_paladin_feet`<br>`acc_dawn_paladin_cloak_1`<br>`acc_dawn_paladin_necklace_1`<br>`acc_dawn_paladin_ring_1_1`<br>`acc_dawn_paladin_ring_2_1`<br>`acc_dawn_paladin_trinket_1`<br>`acc_dawn_paladin_badge_1` |
| 2 | `armor_shadow_assassin_head`<br>`armor_shadow_assassin_body`<br>`armor_shadow_assassin_hands`<br>`armor_shadow_assassin_feet`<br>`acc_shadow_assassin_cloak_2`<br>`acc_shadow_assassin_necklace_2`<br>`acc_shadow_assassin_ring_1_2`<br>`acc_shadow_assassin_ring_2_2`<br>`acc_shadow_assassin_trinket_2`<br>`acc_shadow_assassin_badge_2` |
| 3 | `armor_frost_warden_head`<br>`armor_frost_warden_body`<br>`armor_frost_warden_hands`<br>`armor_frost_warden_feet`<br>`acc_frost_warden_cloak_3`<br>`acc_frost_warden_necklace_3`<br>`acc_frost_warden_ring_1_3`<br>`acc_frost_warden_ring_2_3`<br>`acc_frost_warden_trinket_3`<br>`acc_frost_warden_badge_3` |
| 4 | `armor_flame_mage_head`<br>`armor_flame_mage_body`<br>`armor_flame_mage_hands`<br>`armor_flame_mage_feet`<br>`acc_flame_mage_cloak_4`<br>`acc_flame_mage_necklace_4`<br>`acc_flame_mage_ring_1_4`<br>`acc_flame_mage_ring_2_4`<br>`acc_flame_mage_trinket_4`<br>`acc_flame_mage_badge_4` |
| 5 | `armor_earth_warden_head`<br>`armor_earth_warden_body`<br>`armor_earth_warden_hands`<br>`armor_earth_warden_feet`<br>`acc_earth_warden_cloak_5`<br>`acc_earth_warden_necklace_5`<br>`acc_earth_warden_ring_1_5`<br>`acc_earth_warden_ring_2_5`<br>`acc_earth_warden_trinket_5`<br>`acc_earth_warden_badge_5` |
| 6 | `armor_storm_walker_head`<br>`armor_storm_walker_body`<br>`armor_storm_walker_hands`<br>`armor_storm_walker_feet`<br>`acc_storm_walker_cloak_6`<br>`acc_storm_walker_necklace_6`<br>`acc_storm_walker_ring_1_6`<br>`acc_storm_walker_ring_2_6`<br>`acc_storm_walker_trinket_6`<br>`acc_storm_walker_badge_6` |
| 7 | `armor_death_reaper_head`<br>`armor_death_reaper_body`<br>`armor_death_reaper_hands`<br>`armor_death_reaper_feet`<br>`acc_death_reaper_cloak_7`<br>`acc_death_reaper_necklace_7`<br>`acc_death_reaper_ring_1_7`<br>`acc_death_reaper_ring_2_7`<br>`acc_death_reaper_trinket_7`<br>`acc_death_reaper_badge_7` |
| 8 | `armor_dragon_scale_head`<br>`armor_dragon_scale_body`<br>`armor_dragon_scale_hands`<br>`armor_dragon_scale_feet`<br>`acc_dragon_scale_cloak_8`<br>`acc_dragon_scale_necklace_8`<br>`acc_dragon_scale_ring_1_8`<br>`acc_dragon_scale_ring_2_8`<br>`acc_dragon_scale_trinket_8`<br>`acc_dragon_scale_badge_8` |
| 9 | `armor_iron_bulwark_head`<br>`armor_iron_bulwark_body`<br>`armor_iron_bulwark_hands`<br>`armor_iron_bulwark_feet`<br>`acc_iron_bulwark_cloak_9`<br>`acc_iron_bulwark_necklace_9`<br>`acc_iron_bulwark_ring_1_9`<br>`acc_iron_bulwark_ring_2_9`<br>`acc_iron_bulwark_trinket_9`<br>`acc_iron_bulwark_badge_9` |
| 10 | `armor_ancient_emperor_head`<br>`armor_ancient_emperor_body`<br>`armor_ancient_emperor_hands`<br>`armor_ancient_emperor_feet`<br>`acc_ancient_emperor_cloak_10`<br>`acc_ancient_emperor_necklace_10`<br>`acc_ancient_emperor_ring_1_10`<br>`acc_ancient_emperor_ring_2_10`<br>`acc_ancient_emperor_trinket_10`<br>`acc_ancient_emperor_badge_10` |
| 11 | `armor_star_weaver_head`<br>`armor_star_weaver_body`<br>`armor_star_weaver_hands`<br>`armor_star_weaver_feet`<br>`acc_star_weaver_11_cloak`<br>`acc_star_weaver_11_necklace`<br>`acc_star_weaver_11_ring_1`<br>`acc_star_weaver_11_ring_2`<br>`acc_star_weaver_11_trinket`<br>`acc_star_weaver_11_badge` |
| 12 | `armor_vipers_kiss_head`<br>`armor_vipers_kiss_body`<br>`armor_vipers_kiss_hands`<br>`armor_vipers_kiss_feet`<br>`acc_vipers_kiss_cloak`<br>`acc_vipers_kiss_necklace`<br>`acc_vipers_kiss_ring_1`<br>`acc_vipers_kiss_ring_2`<br>`acc_vipers_kiss_trinket`<br>`acc_vipers_kiss_badge` |
| 13 | `armor_northwind_traveler_head`<br>`armor_northwind_traveler_body`<br>`armor_northwind_traveler_hands`<br>`armor_northwind_traveler_feet`<br>`acc_northwind_traveler_cloak`<br>`acc_northwind_traveler_necklace`<br>`acc_northwind_traveler_ring_1`<br>`acc_northwind_traveler_ring_2`<br>`acc_northwind_traveler_trinket`<br>`acc_northwind_traveler_badge` |
| 14 | `armor_void_walker_head`<br>`armor_void_walker_body`<br>`armor_void_walker_hands`<br>`armor_void_walker_feet`<br>`acc_void_walker_cloak`<br>`acc_void_walker_necklace`<br>`acc_void_walker_ring_1`<br>`acc_void_walker_ring_2`<br>`acc_void_walker_trinket`<br>`acc_void_walker_badge` |
| 15 | `armor_natures_whisper_head`<br>`armor_natures_whisper_body`<br>`armor_natures_whisper_hands`<br>`armor_natures_whisper_feet`<br>`acc_natures_whisper_cloak`<br>`acc_natures_whisper_necklace`<br>`acc_natures_whisper_ring_1`<br>`acc_natures_whisper_ring_2`<br>`acc_natures_whisper_trinket`<br>`acc_natures_whisper_badge` |
| 16 | `armor_abyss_gazer_head`<br>`armor_abyss_gazer_body`<br>`armor_abyss_gazer_hands`<br>`armor_abyss_gazer_feet`<br>`acc_abyss_gazer_cloak`<br>`acc_abyss_gazer_necklace`<br>`acc_abyss_gazer_ring_1`<br>`acc_abyss_gazer_ring_2`<br>`acc_abyss_gazer_trinket`<br>`acc_abyss_gazer_badge` |
| 17 | `armor_lightbringer_head`<br>`armor_lightbringer_body`<br>`armor_lightbringer_hands`<br>`armor_lightbringer_feet`<br>`acc_lightbringer_cloak`<br>`acc_lightbringer_necklace`<br>`acc_lightbringer_ring_1`<br>`acc_lightbringer_ring_2`<br>`acc_lightbringer_trinket`<br>`acc_lightbringer_badge` |
| 18 | `armor_eternal_apprentice_head`<br>`armor_eternal_apprentice_body`<br>`armor_eternal_apprentice_hands`<br>`armor_eternal_apprentice_feet`<br>`acc_eternal_apprentice_cloak`<br>`acc_eternal_apprentice_necklace`<br>`acc_eternal_apprentice_ring_1`<br>`acc_eternal_apprentice_ring_2`<br>`acc_eternal_apprentice_trinket`<br>`acc_eternal_apprentice_badge` |
| 19 | `armor_blood_moon_hunter_head`<br>`armor_blood_moon_hunter_body`<br>`armor_blood_moon_hunter_hands`<br>`armor_blood_moon_hunter_feet`<br>`acc_blood_moon_hunter_cloak`<br>`acc_blood_moon_hunter_necklace`<br>`acc_blood_moon_hunter_ring_1`<br>`acc_blood_moon_hunter_ring_2`<br>`acc_blood_moon_hunter_trinket`<br>`acc_blood_moon_hunter_badge` |
| 20 | `armor_rusted_gear_head`<br>`armor_rusted_gear_body`<br>`armor_rusted_gear_hands`<br>`armor_rusted_gear_feet`<br>`acc_rusted_gear_cloak`<br>`acc_rusted_gear_necklace`<br>`acc_rusted_gear_ring_1`<br>`acc_rusted_gear_ring_2`<br>`acc_rusted_gear_trinket`<br>`acc_rusted_gear_badge` |
| 21 | `armor_silvermoon_ranger_head`<br>`armor_silvermoon_ranger_body`<br>`armor_silvermoon_ranger_hands`<br>`armor_silvermoon_ranger_feet`<br>`acc_silvermoon_ranger_cloak`<br>`acc_silvermoon_ranger_necklace`<br>`acc_silvermoon_ranger_ring_1`<br>`acc_silvermoon_ranger_ring_2`<br>`acc_silvermoon_ranger_trinket`<br>`acc_silvermoon_ranger_badge` |
| 22 | `armor_desert_scorpion_head`<br>`armor_desert_scorpion_body`<br>`armor_desert_scorpion_hands`<br>`armor_desert_scorpion_feet`<br>`acc_desert_scorpion_cloak`<br>`acc_desert_scorpion_necklace`<br>`acc_desert_scorpion_ring_1`<br>`acc_desert_scorpion_ring_2`<br>`acc_desert_scorpion_trinket`<br>`acc_desert_scorpion_badge` |
| 23 | `armor_jungle_panther_head`<br>`armor_jungle_panther_body`<br>`armor_jungle_panther_hands`<br>`armor_jungle_panther_feet`<br>`acc_jungle_panther_cloak`<br>`acc_jungle_panther_necklace`<br>`acc_jungle_panther_ring_1`<br>`acc_jungle_panther_ring_2`<br>`acc_jungle_panther_trinket`<br>`acc_jungle_panther_badge` |
| 24 | `armor_highland_eagle_head`<br>`armor_highland_eagle_body`<br>`armor_highland_eagle_hands`<br>`armor_highland_eagle_feet`<br>`acc_highland_eagle_cloak`<br>`acc_highland_eagle_necklace`<br>`acc_highland_eagle_ring_1`<br>`acc_highland_eagle_ring_2`<br>`acc_highland_eagle_trinket`<br>`acc_highland_eagle_badge` |
| 25 | `armor_pirate_captain_head`<br>`armor_pirate_captain_body`<br>`armor_pirate_captain_hands`<br>`armor_pirate_captain_feet`<br>`acc_pirate_captain_cloak`<br>`acc_pirate_captain_necklace`<br>`acc_pirate_captain_ring_1`<br>`acc_pirate_captain_ring_2`<br>`acc_pirate_captain_trinket`<br>`acc_pirate_captain_badge` |
| 26 | `armor_bounty_hunter_head`<br>`armor_bounty_hunter_body`<br>`armor_bounty_hunter_hands`<br>`armor_bounty_hunter_feet`<br>`acc_bounty_hunter_cloak`<br>`acc_bounty_hunter_necklace`<br>`acc_bounty_hunter_ring_1`<br>`acc_bounty_hunter_ring_2`<br>`acc_bounty_hunter_trinket`<br>`acc_bounty_hunter_badge` |
| 27 | `armor_plague_doctor_head`<br>`armor_plague_doctor_body`<br>`armor_plague_doctor_hands`<br>`armor_plague_doctor_feet`<br>`acc_plague_doctor_cloak`<br>`acc_plague_doctor_necklace`<br>`acc_plague_doctor_ring_1`<br>`acc_plague_doctor_ring_2`<br>`acc_plague_doctor_trinket`<br>`acc_plague_doctor_badge` |
| 28 | `armor_moonshadow_dancer_head`<br>`armor_moonshadow_dancer_body`<br>`armor_moonshadow_dancer_hands`<br>`armor_moonshadow_dancer_feet`<br>`acc_moonshadow_dancer_cloak`<br>`acc_moonshadow_dancer_necklace`<br>`acc_moonshadow_dancer_ring_1`<br>`acc_moonshadow_dancer_ring_2`<br>`acc_moonshadow_dancer_trinket`<br>`acc_moonshadow_dancer_badge` |
| 29 | `armor_blood_knight_head`<br>`armor_blood_knight_body`<br>`armor_blood_knight_hands`<br>`armor_blood_knight_feet`<br>`acc_blood_knight_cloak`<br>`acc_blood_knight_necklace`<br>`acc_blood_knight_ring_1`<br>`acc_blood_knight_ring_2`<br>`acc_blood_knight_trinket`<br>`acc_blood_knight_badge` |
| 30 | `armor_phantom_rider_head`<br>`armor_phantom_rider_body`<br>`armor_phantom_rider_hands`<br>`armor_phantom_rider_feet`<br>`acc_phantom_rider_cloak`<br>`acc_phantom_rider_necklace`<br>`acc_phantom_rider_ring_1`<br>`acc_phantom_rider_ring_2`<br>`acc_phantom_rider_trinket`<br>`acc_phantom_rider_badge` |
| 31 | `armor_wind_whisperer_head`<br>`armor_wind_whisperer_body`<br>`armor_wind_whisperer_hands`<br>`armor_wind_whisperer_feet`<br>`acc_wind_whisperer_cloak`<br>`acc_wind_whisperer_necklace`<br>`acc_wind_whisperer_ring_1`<br>`acc_wind_whisperer_ring_2`<br>`acc_wind_whisperer_trinket`<br>`acc_wind_whisperer_badge` |
| 32 | `armor_wave_walker_head`<br>`armor_wave_walker_body`<br>`armor_wave_walker_hands`<br>`armor_wave_walker_feet`<br>`acc_wave_walker_cloak`<br>`acc_wave_walker_necklace`<br>`acc_wave_walker_ring_1`<br>`acc_wave_walker_ring_2`<br>`acc_wave_walker_trinket`<br>`acc_wave_walker_badge` |
| 33 | `armor_earthquake_seer_head`<br>`armor_earthquake_seer_body`<br>`armor_earthquake_seer_hands`<br>`armor_earthquake_seer_feet`<br>`acc_earthquake_seer_cloak`<br>`acc_earthquake_seer_necklace`<br>`acc_earthquake_seer_ring_1`<br>`acc_earthquake_seer_ring_2`<br>`acc_earthquake_seer_trinket`<br>`acc_earthquake_seer_badge` |
| 34 | `armor_night_bard_head`<br>`armor_night_bard_body`<br>`armor_night_bard_hands`<br>`armor_night_bard_feet`<br>`acc_night_bard_cloak`<br>`acc_night_bard_necklace`<br>`acc_night_bard_ring_1`<br>`acc_night_bard_ring_2`<br>`acc_night_bard_trinket`<br>`acc_night_bard_badge` |

### Set 11 P0 身份说明

附录只记录 Set 11 当前文档自身的 10 个既有 ID，不代表冲突已经解决。Set 95 仍复用 `star_weaver_set`，并复用 `armor_star_weaver_head/body/hands/feet` 四个 ID；因此正式 `GearSetContentRegistry` 必须在 Set 11/95 两份显式 member 清单跨集合校验时 fail fast。不得退回 tag 计数、不得按文档顺序“取前十件”、不得猜测显示名来消歧。

冲突证据：Set 11 护甲 ID 位于 `sets_11_to_15_armor.md:20,45,71,95`，tag 位于 `:27,52,78,102`；Set 95 复用 ID 位于 `sets_91_to_100.md:432,458,481,503`，tag 位于 `:439,465,488,510`。两套饰品也共享 tag：`sets_11_to_20_accessories.md:23,45,67,88,109,130` 与 `sets_91_to_100_accessories.md:563,585,607,628,649,670`。

## 附录 B：拓扑总览与落地门槛

| 拓扑 | Sets |
|---|---|
| 稳步循环 `2/4/6/8/10` | 7、12、26、29 |
| 蜕变 `3/5/7/10` | 3、6、8、14、19、20、30 |
| 仪式/领域 `2/5/8/10` | 1、10、15、17、25、27、31、32 |
| 堡垒 `4/6/8/10` | 5、9、33 |
| 节奏/诡术 `2/3/6/9/10` | 2、13、21、22、23、24、28、34 |
| 终极高魔 `3/6/9/10` | 4、11、16、18 |

资源/运行时落地前必须同时满足：

1. 34 份 `GearSetDefinition.member_item_ids` 各恰好 10 个唯一 ID，并验证预期十槽；全局 member ID 不得跨不同 set 重复。Set 11/95 冲突未解决时内容构建必须失败。
2. 对 1000 件冻结单件做字段快照回归：ID、slot、tag、价格、单件 attribute/trait/special/usage 零漂移；本文件不得成为修改单件配置的授权。
3. 每套阈值只从显式 member ID 计数；tag 搜索结果与正式计数不一致时报告审计警告，但绝不 fallback。
4. 每个 replacement group 在资源加载期验证闭集、单位和“最高档替换”方向；骰、概率、半径、次数、TU 与 basis points 不得混入同组。
5. 每个 damage/heal/lethal/roll reaction 都有真实 Act、正式 fact、可观察 oracle、失败分支与 origin/recursion regression；不能用资源文本扫描代替行为测试。
6. `per_battle`、`per_world_day`、`per_world_month`、shared usage、换装破阈值、存档重载、战斗写回、preview/AI 只读分别有 focused regression。
7. 领域、形态、召唤、飞行、相位、环境、世界能力必须验证正式 producer/consumer；缺 owner 时保持 planned，不能以描述或 UI 文案宣称 landed。
8. 数值模拟必须使用正式获取等级、同价/同静态底盘角色和真实敌人曲线；普通 runner PASS 或低级敌人结果不能证明平衡完成。

## Claim status

- `GS01-34-MEMBERS`：文档候选已固定显式 10-member 清单；Set 11 因跨套 ID/tag 冲突为 blocked，其他清单仍需未来 registry/resource 校验落地。
- `GS01-34-TOPOLOGY`：planned content redesign；本文给出每套主题驱动 4/5 档拓扑与 10 件 capstone，尚无 Resource。
- `GS01-34-RUNTIME`：not landed；当前逐 item attribute 聚合不能执行 membership、threshold、usage、replacement、领域、形态或逐段合同。
- `GS01-34-BALANCE`：document-calibrated；已按冻结十件总价/静态底盘保守、标准或补强分档，但尚未通过正式同级战斗模拟。
