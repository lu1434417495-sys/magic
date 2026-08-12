# 既有套装奖励重设计 68–100（十件累计阈值）

> 状态：`Content redesign / planned`。
>
> 本文是设计覆盖，不代表 `GearSetDef`、阈值 Resource、typed definition、战斗能力、预览、AI、UI、存档或回归测试已经落地。
>
> 基线：2026-08-08 当前 checkout。原始单件、旧阈值与运行时 owner 仍分别以 `sets_*.md`、`set_bonus_design.md`、当前 C# 源码和 `docs/design/` 为准。

## 1. 目标与冻结边界

本文只重设计套装奖励层，覆盖套装 68–100。采用龙鳞套装的 typed membership、累计阈值、真实伤害段、usage provenance 和预览/AI 对等纪律，但不把所有套装机械地改成同一组阈值。

以下内容冻结，不在本文修改：

- 33 个套装的编号、名称、十件成员、槽位、`item_id`、`display_name`、set tag、价格和单件属性。
- 任何单件描述中的特殊能力、次数和代价。本文只声明套装奖励如何与这些能力叠加、互斥或共享 usage。
- 原 `set_bonus_design.md` 文本。Set 79 的损坏触发文本在本文给出规范设计值，但不回写原文。
- Set 95 与 Set 11 的 `star_weaver_set` / `armor_star_weaver_*` 冲突。本文不猜测重命名、alias 或兼容迁移。

套装成员的未来唯一真相必须是套装侧显式 `member_item_ids[10]`；不得为本轮设计修改 `ItemDef`。tag 只保留搜索和文案用途，不参与计数。阈值按 `member_item_ids` 与有效、未损坏、实际装备的唯一 entry 求交集计数，换装、损坏、修复和多槽占用都必须触发重新求值。

## 2. 输入与价格校准

设计输入：

- `docs/content/equipment_sets/set_bonus_design.md:1859-2694`：中央 2/4 奖励和终极机制。
- `sets_61_to_70*.md`、`sets_71_to_80*.md`、`sets_81_to_90*.md`、`sets_91_to_100*.md`：十件成员、单件能力、价格和成员文档中的旧“设计预留”。
- `existing_set_attribute_optimization.md`：当前合法字段、离散 mitigation tier、同字段最高值和 typed owner 审计。
- `docs/proposals/inventory/dragon_scale_set_full_landing.md`：membership、threshold source、usage、TU 和伤害段合同。
- `PartyEquipmentService.BuildAttributeModifiersTyped()`、`ItemDefinition`、装备能力 runtime 与 battle damage/status owner：当前实现能力边界。

价格只用于同带宽强度校准，不据此宣称完成数值平衡：

| 价格带 | 套装 | 十件总价 | 设计约束 |
|---|---|---:|---|
| 中高阶传奇 | 68–90 | 125,000–142,000 | 低档建立主题，8/9 件形成完整循环，10 件才给原终极机制 |
| 高魔传奇 | 91–93、98–99 | 167,000 | 允许高魔能力，但延后到 3/6/9/10，10 件必须有明确代价或次数 |
| 高阶主题传奇 | 94–97 | 151,000 | 死亡、命运、梦境等强控制必须有 save、TU、usage 和复活边界 |
| 极端离群 | 100 | 257,000 | 单件全伤害免疫已使整体平衡不可判断；套装奖励不得继续增加防御 |

## 3. 拓扑选择

| 拓扑 | 使用套装 | 选择理由 |
|---|---|---|
| 稳步循环 `2/4/6/8/10` | 69、73、78、80、84 | 锈层、气功、傀儡、水域和磁力均需要逐级增加一个可复用循环部件 |
| 蜕变 `3/5/7/10` | 74、75、77、83 | 飞行、妖狐、阿修罗和相位都是形态逐步完成，不宜 2 件即获得核心变身 |
| 仪式/领域 `2/5/8/10` | 68、76、87、89、93、94、97 | 先建立仪式资源或识别，再开放领域，最终在 10 件完成仪式终局 |
| 堡垒 `4/6/8/10` | 81 | 单件蒸汽重甲已很强，套装从 4 件才启动压力循环，避免早期静态堆叠 |
| 节奏/诡术 `2/3/6/9/10` | 70、71、72、79、82、85、86、88、90 | 需要早期小节奏点、6 件核心手段、9 件准备态，10 件只升级同一终结技 |
| 高魔 `3/6/9/10` | 91、92、95、96、98、99、100 | 高阶规则改写集中在较高阈值，避免 2 件取得创造、抹除、回溯或因果控制 |

所有拓扑均为累计阈值，且恰有 4 或 5 档；10 件必有 capstone。

## 4. 共通运行合同

### 4.1 数值与成长

- 同一套内的成长字段必须声明 `replacement_group`。更高阈值替换同组较低值，不累加。例如 `attack_bonus +1` → `+2` 的最终值是 `+2`。
- 不同来源仍按各自 owner 合并；单件静态属性不因套装阈值激活而消失。
- 同一 mitigation tag 采用 `immune > half > normal > double` 的最强 tier；两个 `half` 不叠为四分之一。
- `cold` 统一投影为 `freeze`，`necrotic` 统一投影为 `negative_energy`。
- TU 是全局时间线玩法值，不是现实秒/分钟换算。本文使用 30/60/90/120/180/300 TU，均满足 5 TU 粒度。

### 4.2 伤害与防递归

“每次命中附伤”只在每个真实 `primary direct-damage segment` 上查询；多段技能逐段触发，不按 cast 去重。以下来源一律排除：

- `extra_damage_segments`；
- DOT、upkeep、terrain/zone tick；
- reflection、self-damage、equipment-generated damage；
- 由套装或装备触发的 skill/action 再次触发自身；
- 未知 `DamageOriginKind`，必须 fail closed。

反射伤害统一标记 `reflection`，不能触发击中、受伤、击杀、吸血、附伤或另一次反射。一次攻击若有多个“升级为暴击”来源，结果仍只是一枚暴击覆盖。

### 4.3 Usage 与换装

- 套装主动默认使用 `(gear_set_id, threshold_id, ability_id, period_kind, period_index)` 独立记账。
- 只有逐套“单件交互与 usage”明确写出 `shared_usage_group` 时，单件与套装能力才共享次数；否则语义相近也各自独立。
- `per_battle` 在正式 battle lifetime 内复位；`per_world_day` 由世界日 owner 复位。移除再装备不退款、不重置。
- 高阈值失活时，已产生的定时状态按其 `source_lifetime_policy` 处理：本文主动领域默认持续到既定 TU 到期；只依赖常驻件数的被动立即撤销。

### 4.4 Typed owner 图例

| 缩写 | owner / 状态 |
|---|---|
| `ATTR` | `AttributeService` 及 AC component，可承载合法静态属性 |
| `TRAIT` | `TraitDef` save / damage resistance / passive status |
| `EA` | typed equipment-ability trigger/fact/condition/action；需新增 `GearSetThreshold` source kind |
| `STATUS` | `BattleStatusEffectState` / semantic table / timeline expiry |
| `SUMMON` | typed summon payload、召唤 roster、指令和数量预算 |
| `ZONE` | delayed area、temporary edge/terrain、移动领域和逐 TU 调度 |
| `DURABILITY` | 装备实例耐久、破损、修复；当前无完整正式 owner |
| `REVIVE` | 死亡、尸体、复活等级和封锁；需要正式跨路径规则 |
| `WORLD` | 环境、材质、天气、可恢复地形或世界物体 |
| `TIME` | intended action、状态/位置/资源原子回滚；当前无通用 owner |
| `GEARSET` | authoring、registry、membership、evaluator、derived source、refresh、preview、AI、UI、usage/save；整体未落地 |

以下每套的 `typed owner / gap` 只列 `GEARSET` 之外的增量；所有套装都隐含 `GEARSET`。

## 68 乌鸦领主 / `raven_lord_set`

拓扑：仪式/领域 `2/5/8/10`。先获得死亡观察能力，再积累灵魂讯息，10 件才完成死亡鸦群仪式。校准：十件总价 130,000；不再给新的击杀治疗，避免复制披风、夺魂戒和徽章的三份治疗。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 鸦眼 | `check(perception,+3)`、`check(insight,+2)`。 | 常驻；两项是独立阈值来源。 | utility check query 缺口。 |
| 5 | 临终讯号 | 可感知 6 格内 HP ≤ 25% 的敌方单位；对该类目标 `attack_roll_bonus +1`，`replacement_group=raven_finisher_attack`。 | HP ratio 在每次正式攻击查询时读取；preview/AI 使用同一 fact。 | `EA` 的 `target_hp_ratio` fact 与感知投影。 |
| 8 | 灵魂回声 | 穿戴者亲自击杀非召唤敌人时获得 1 层 `raven_soul`，最多 3 层；下一次由穿戴者造成的 qualifying 主直接伤害段消耗 1 层并追加 `1D6 negative_energy`。 | 每个击杀事件最多 1 层；层持续到战斗结束；追加段遵守第 4.2 节。 | `EA` + `STATUS` stack state；击杀 provenance。 |
| 10 | 死亡鸦群 | 授予 `raven_flock_death`：指定 12 格内可见敌人，持续 90 TU、每 30 TU 结算一次。每次目标进行 DC16 agility save；失败受 `3D8 physical_slash + 1D8 negative_energy` 并 `blinded` 30 TU，成功半伤且不致盲。鸦群击杀目标时施加 `soul_consumed`：阻止 `revivify` 及同级复活，高阶复活仍可用。持续期间授予 1 AP 的转移目标动作，转移不重置剩余 TU。 | 2 AP；`per_world_day=1`；固定 3 次 tick。 | `EA` + `ZONE` + `REVIVE`；scheduled target、retarget、save 与尸体规则均缺 formal set 内容。 |

单件交互与 usage：68.5、68.7、68.10 的击杀治疗各按自身来源独立结算；8件阈值不增加治疗。68.9 的死神乌鸦与10件阈值次数独立。鸦群伤害是 equipment/set-generated origin，不能生成 `raven_soul` 或触发夺魂戒附伤。

落地缺口：除共同 `GEARSET` 外，需要濒死单位感知、击杀 provenance、定时目标型召唤伤害、转移目标、尸体复活等级和 preview/AI 期望值。

## 69 锈刃骑士 / `rust_blade_set`

拓扑：稳步循环 `2/4/6/8/10`。锈层必须先有防护、再有单体施加、命中成长、层数强化，最后才扩散成锈疫。校准：十件总价 140,000；所有单件锈蚀统一读一份装备实例状态，避免平行的 -1/-2/-3 计数器。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 抗蚀甲胄 | `armor_ac_bonus +1`；`res(acid,half)`。 | 常驻；mitigation 按最强 tier。 | `ATTR` + `TRAIT`。 |
| 4 | 锈痕 | 穿戴者以近战武器攻击命中后，有 20% 概率对目标本次使用的 metal 武器施加 1 层 `rust`；无合格武器时改选 body 槽 metal 护甲。每层令武器攻击 -1 或护甲 AC -1，最多 3 层。 | 每次攻击只掷 1 次，不按伤害段重复；持续至 repair，若 `DURABILITY` 尚未落地则最迟战斗结束清除。`replacement_group=rust_proc_chance`。 | `EA` equipment selection + `DURABILITY`。 |
| 6 | 锈刃熟练 | `attack_bonus +1`，`replacement_group=rust_attack_growth`。 | 常驻。 | `ATTR`。 |
| 8 | 深层腐蚀 | `attack_bonus +2` 替换 6 件值；锈痕概率提高到 35%，替换 4 件的 20%。处于 3 层 `rust` 的构造体额外承受 `incoming_damage_multiplier +20%`，仅对穿戴者的 qualifying 主直接段。 | 常驻查询；不改变 stack cap。 | `ATTR` + `EA` conditional damage query。 |
| 10 | 锈疫 | 授予 `rust_plague`：以 8 格内一点创建半径 4 格区域，持续 90 TU。区域生成时对敌方每件合格 metal 武器和 body 护甲施加 2 层 `rust`，之后每 30 TU +1 层。装备首次达到 3 层时进行一次 50% break outcome：非传奇装备变为 broken；传奇/indestructible 装备只在 30 TU 内失效其攻击或 AC component。构造体在首次应用时另受 `4D8 acid`，DC17 constitution save 半伤。 | 2 AP；`per_world_day=1`；同一装备只进行一次 break outcome。 | `ZONE` + `DURABILITY` + `EA` outcome table；永久 writeback 是 blocker。 |

单件交互与 usage：69.1、69.5、69.7、69.9、69.10 全部写同一 `rust_equipment_state`，层数上限 3，取最强当前惩罚，不创建独立锈层；任何正式 repair 清空该实例全部锈层。各单件主动次数与 `rust_plague` 独立。

落地缺口：需要 enemy equipment instance、metal tag、耐久/破损/修复、传奇豁免和世界侧永久写回；在这些 owner 落地前不得把“碎裂”退化成永久 AC 字段修改。

## 70 镜中恶魔 / `mirror_demon_set`

拓扑：节奏/诡术 `2/3/6/9/10`。2–3 件只提供读镜和施法节奏，6 件开放受限复制，9 件建立镜域，10 件升级为镜界崩塌。校准：十件总价 140,000；任意复制和反射必须 fail closed。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 镜心 | `check(deception,+3)`；`save(willpower,+2)`。 | 常驻。 | utility check query + `TRAIT` save。 |
| 3 | 折光施法 | `spell_hit +1`，`replacement_group=mirror_spell_hit_growth`。 | 只匹配 spell-origin attack check。 | `EA` outgoing spell-hit query。 |
| 6 | 受限复制 | 观察 12 格内敌人实际完成一次非传奇主动 skill/equipment action 后，可复制其 definition 60 TU，期间最多使用 1 次；使用穿戴者自己的攻击值与 save DC。禁止复制 summon、gear-set、copy、reflection、time rollback、revive、world mutation、legendary 和无法由 canonical executor 执行的 action。 | reaction 时序、0 AP；`per_world_day=1`；`shared_usage_group=mirror_copy_daily`。 | `EA` action-observed event、definition whitelist、临时 granted action；大 ABI gap。 |
| 9 | 镜域 | 以自身为中心半径 3 格，持续 90 TU。区域内敌人对穿戴者的 directed attack `attack_roll_penalty -2`；穿戴者每 30 TU 至多一次可花 1 AP 与自己的镜像占位交换，最大 4 格，落点必须 canonical-valid。 | 1 AP 开启；`per_battle=1`；移动不重置领域中心，领域跟随穿戴者。 | `ZONE` + `EA` attack query + safe swap。 |
| 10 | 镜界崩塌 | 镜域升级：每个攻击来源每 30 TU 的第一次 directed weapon hit 有 50%、directed spell hit 有 30% 触发反射；触发时取消该次命中，并以 `reflection` origin 向来源造成其 canonical would-be final damage 的 50%（向下取整，最少 1）。每次穿戴者激活时可花 1 AP 强制与领域内一个敌人原子换位，目标 DC17 willpower save 抵抗。领域结束时敌人受 `2D8 psychic`，DC17 willpower save 半伤。 | 升级同一个 `mirror_domain_per_battle`，不增加第二次开启次数；持续仍为 90 TU。 | `EA` pre-damage transaction + `ZONE` + atomic swap；preview 必须计算 would-be damage 而不提交。 |

单件交互与 usage：70.7 复制戒与6件阈值共用 `mirror_copy_daily`，任一使用即消耗当日次数。一次 incoming event 最多被一个反射来源取消；按 `capstone → 单件主动 → 单件被动` 顺序求值，成功后停止后续反射。反射 origin 不再触发任何装备或套装反应。

落地缺口：需要动作观察白名单、临时 action definition、canonical would-be damage、非递归反射、镜像占位和双方原子换位；这些不能用字符串 skill ID 分支实现。

## 71 武士之魂 / `samurai_spirit_set`

拓扑：节奏/诡术 `2/3/6/9/10`。武士先建立心法，再扩展攻击曲线，9 件取得蓄势，10 件只升级同一次居合而不额外赠送第二个终结技。校准：十件总价 142,000；保留原“整次行动准备 → 下一击 → 行动锁”语义。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 武士心 | `attack_bonus +1`，`replacement_group=samurai_attack_growth`；`save(willpower,+2)`。 | 常驻。 | `ATTR` + `TRAIT`。 |
| 3 | 不动心 | 对 `frightened`、`charmed` 获得 save immunity。 | 常驻；不等于免疫所有 willpower save。 | `TRAIT` save-immunity tags。 |
| 6 | 剑豪 | `attack_bonus +2` 替换 2 件值；近战武器 `crit_threshold_delta=-1`。 | 常驻，只匹配 melee weapon attack。 | `ATTR` + `EA` critical-threshold query gap。 |
| 9 | 居合蓄势 | 授予 `iaijutsu_prepare`：获得 `iaijutsu_ready` 60 TU，并在结算后立即结束当前 activation；尚未花费的 AP 随 activation 结束而失效。下一次近战武器攻击获得 advantage；命中时追加 `2D8 physical_slash`，随后消耗状态。 | 2 AP；`per_battle=1`；`shared_usage_group=samurai_iaijutsu_battle`；60 TU 未使用则失效。 | `EA` granted action + activation end + `STATUS` one-shot attack modifier。 |
| 10 | 一闪 | 升级同一 `iaijutsu_ready`：下一击命中后升级为 critical；若目标在该击结算前 HP < 50%，追加伤害改为 `3D10 physical_slash`。无论命中与否，攻击结算后穿戴者获得 `action_lock` 30 TU。 | 不增加 usage；仍消耗 9 件的同一次蓄势。 | `EA` critical override + HP fact + `STATUS` action lock。 |

单件交互与 usage：71.7 的自动命中/暴击次数独立；若与一闪用于同一攻击，命中和 critical 都是幂等结果，伤害骰不因两份 critical override 再翻倍。71.2 必杀斩仍按单件自身 usage，不能把“本战斗受伤总量”复制进一闪。

落地缺口：需要 activation end、跨 TU one-shot 状态、melee-only critical query、命中后 critical override 和结束后的行动锁；AI 必须把准备 activation 的机会成本纳入评分。

## 72 忍者之影 / `ninja_shadow_set`

拓扑：节奏/诡术 `2/3/6/9/10`。3 件取得烟步，6 件建立可防御的分身，9/10 件逐步升级同步攻击和死亡烟。校准：十件总价 130,000；克隆数量和同步攻击都有硬上限。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 无影 | `check(stealth,+3)`、`check(acrobatics,+2)`。 | 常驻。 | utility check query。 |
| 3 | 烟步 | 传送至 4 格内 canonical-valid 落点，并在原地创建半径 2 格烟区 60 TU；穿戴者获得 invisibility 30 TU，首次攻击/施法后提前结束。 | 1 AP；`per_battle=1`；烟区刷新而不叠加。 | `EA` teleport + `ZONE` visibility。 |
| 6 | 影分身 | 召唤 2 个分身，持续 60 TU；每个 1 HP、AC 等于召唤时穿戴者 AC，不能主动攻击。directed attack 命中穿戴者前随机选择一个存活分身代受该次命中，分身随后消散。 | 2 AP；`per_battle=1`；`shared_usage_group=ninja_shadow_clone_battle`。 | `SUMMON`/proxy target、snapshot AC、随机 outcome。 |
| 9 | 同步暗杀 | 分身数替换为 3；穿戴者每 30 TU 第一次 qualifying weapon hit 后，恰有 1 个存活分身对同目标造成该主段 canonical final damage 的 50%，标记 equipment-origin。分身消散时生成半径 1 格烟区 30 TU。 | 升级 6 件同一次召唤；同步伤害每 30 TU 最多一次。 | `EA` damage fact + `SUMMON` + `ZONE`；防递归。 |
| 10 | 影军 | 分身数替换为 `1D4+1`，持续替换为 90 TU；每 30 TU 最多 2 个分身各进行一次 50% 同步伤害。分身被摧毁时，1 格内敌人进行 DC16 constitution save，失败 `blinded` 30 TU。 | 不增加第二次召唤 usage；数量生成后冻结。 | `SUMMON` 数量/性能预算 + `EA` scheduled echo + `STATUS`。 |

单件交互与 usage：72.10 徽章的友军镜像和 Set 分身次数独立，但同一穿戴者最多 5 个 active clone/proxy；超过时先移除最旧者。72.9 烟雾弹与烟步次数独立，同格烟区只刷新到较长 remaining TU，不叠加命中惩罚。

落地缺口：需要 proxy target selection、召唤物快照、同步伤害 fact、烟区视野和 clone cap；preview/AI 必须显示预期分身数而不在预览中消耗随机数。

## 73 少林武僧 / `shaolin_monk_set`

拓扑：稳步循环 `2/4/6/8/10`。从徒手命中、魔法徒手、气层循环到佛掌终结，适合五档连续成长。校准：十件总价 125,000；单件已有 `1D10` 徒手骰，套装不得把它降回 `1D8`。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 禅武 | `unarmed_hit +2`，`replacement_group=shaolin_unarmed_hit_growth`；`save(constitution,+2)`。 | 常驻，只匹配 unarmed attack。 | `EA` attack query + `TRAIT`。 |
| 4 | 金刚拳 | 徒手攻击视为 magical；每次自身 activation 最多一次，可花 1 AP 进行一记 bonus unarmed attack。徒手基础骰取所有来源中的最高骰，不覆盖 73.3/73.7 的 `1D10`。 | activation budget=1。 | `EA` source tagging + immediate attack/action budget。 |
| 6 | 气劲 | `unarmed_hit +3` 替换 2 件值；`armor_ac_bonus +1`；每个 qualifying 徒手主直接段追加 `1D4 force`。 | 常驻；追加段遵守第 4.2 节。 | `ATTR` + `EA` segment query。 |
| 8 | 点穴循环 | 徒手攻击命中后获得 1 层 `qi`, 最多 3 层，每次攻击最多 1 层。达到 3 层时下一次徒手命中自动消耗全部层，追加 `2D6 force`；目标 DC16 constitution save，失败 `stunned` 30 TU。 | 层持续到战斗结束；一次命中最多触发一次点穴。 | `EA` hit event + `STATUS` stack/control。 |
| 10 | 如来神掌 | 6 格锥形；敌人 DC18 agility save。失败受 `5D10 force + 3D10 radiant`、`prone` 且 `stunned` 30 TU；成功半伤且无控制。`undead` 或 `demon` 目标另受 `2D10 radiant`。施放后穿戴者 90 TU 内 `armor_ac_bonus +2` 且对 `negative_energy`、`poison` 为 immune。 | 2 AP；`per_world_day=1`；套装伤害不生成 `qi`。 | 正式 SkillDef + `EA` granted action、混合段、cone、save/control。 |

单件交互与 usage：所有徒手基础骰只取最高值；73.7 铁指环与8件阈值的 stun 各自有 save/usage。任一 activation 内由单件或套装生成的 bonus unarmed attack 合计最多 1 次，避免无限 immediate-attack 链。

落地缺口：需要 typed unarmed source、基础骰合并、activation bonus-attack budget、qi stack、混合伤害与目标 tag；AI 需估算三层点穴的未来收益。

## 74 天狗面具 / `tengu_mask_set`

拓扑：蜕变 `3/5/7/10`。三件适应山风，五件进入飞行形态，七件形成掠袭循环，十件才展开完整天狗风暴。校准：十件总价 130,000；单件滑翔继续有效，套装飞行只扩展而不叠加速度倍数。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 3 | 山风之眼 | `move_point_capacity_delta +1`；`check(perception,+2)`。 | 常驻；移动值不与本套更高档重复。`replacement_group=tengu_move_growth`。 | `STATUS` passive move + utility check query。 |
| 5 | 风翼 | 免疫 falling damage；激活后获得 flying movement 60 TU，飞行 move capacity 为当前地面 capacity +2。 | 1 AP；`per_battle=1`；结束时若无合法落点，使用 canonical nearest-safe landing。 | flying movement/landing gap。 |
| 7 | 掠风斩 | `attack_bonus +1`。一次 activation 内移动至少 3 格后，下一次近战武器命中追加 `2D6 physical_slash`；目标 DC16 strength save，失败被推离 2 格。 | 每次 activation 最多一次；30 TU 内未命中则失效。 | `ATTR` + `EA` movement-distance fact + forced move。 |
| 10 | 天狗风暴 | 6 格锥形；敌人 DC17 strength save，失败受 `4D8 physical_blunt`、推离 3 格并 `prone`，成功半伤且不移动。施放后风翼刷新为 90 TU；期间每次 activation 首次穿越敌人相邻格时，对该敌人造成 `2D6 physical_slash`，每目标每 30 TU 一次。若战场具有 canonical `forest` tag，锥形目标另受 `2D8 lightning`。 | 2 AP；`per_world_day=1`；路径伤害为 equipment-origin。 | 正式 SkillDef + flying/path query + `WORLD` terrain tag。 |

单件交互与 usage：74.2/74.5 的滑翔与5件阈值的 falling immunity 幂等；74.7、74.10 和3件阈值的移动增量按各自合法来源相加，但任何“×3 移动”只由主动动作决定，不能乘算。多个 push 来源在同一 hit 上只执行距离最大的一个。

落地缺口：需要 flight、nearest-safe landing、activation movement distance、路径邻接去重、forest typed tag 和 forced-move preview。

## 75 九尾妖狐 / `nine_tailed_fox_set`

拓扑：蜕变 `3/5/7/10`。先建立魅惑心智，再取得幻步，七件进入妖狐形态，十件以九尾天火完成蜕变。校准：十件总价 135,000；镜像、狐火和形态均设 active cap，避免与多件单品无限并行。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 3 | 妖狐心 | `willpower +2`；`check(deception,+3)`。 | 常驻。 | `ATTR` + utility check query。 |
| 5 | 幻尾步 | 传送至 6 格内 canonical-valid 落点，并留下 1 个幻尾分身 60 TU；分身 1 HP、AC 等于施放时穿戴者 AC，只能代受一次 directed hit。 | 1 AP；`per_battle=1`；active 幻尾 cap=1。 | teleport + proxy target + snapshot AC。 |
| 7 | 妖狐化身 | 90 TU 内 `armor_ac_bonus +1`、`move_point_capacity_delta +2`；每个 qualifying weapon/spell 主直接段追加 `1D6 fire`。 | 1 AP；`per_battle=1`；`replacement_group=fox_avatar_power`。 | `STATUS` transform + `EA` segment query。 |
| 10 | 九尾天火 | 6 格锥形；敌人 DC18 agility save，失败受 `6D10 fire + 3D10 force` 并获得 `foxfire_burn` 90 TU，成功半伤且不燃烧。burn 每 30 TU 造成 `1D6 fire`。施放后妖狐化身刷新至 90 TU并升级为 `armor_ac_bonus +2`、移动 +2、qualifying 段 `+1D10 fire`，替换 7 件值。 | 2 AP；`per_world_day=1`；burn/追加段均不得递归。 | 正式 SkillDef + cone/mixed damage + `STATUS` DOT + transform replacement。 |

单件交互与 usage：75.7 的真实幻象次数独立，但穿戴者 active proxy 总 cap=2，超出时移除最旧者。75.9 狐尾形态和 Set 妖狐化身可同时有 usage，但同字段只取 `fox_avatar_power` 中最高值；两份 fire append 若都是 qualifying 独立来源可各产生一段，且都受第 4.2 节排除规则。

落地缺口：需要 transform replacement、proxy cap、混合锥形、burn upkeep、safe teleport 和 AI 对形态剩余 TU 的估值。

## 76 阴阳师 / `onmyoji_set`

拓扑：仪式/领域 `2/5/8/10`。两件只建立术式知识，五件签订一只基础式神，八件强化契约，十件才展开百鬼夜行。校准：十件总价 135,000；原文仅明确 4 个基础式神和“12 种”概念，缺失的 8 种必须先创作正式内容。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 阴阳学 | `check(arcana,+3)`、`check(religion,+2)`。 | 常驻。 | utility check query。 |
| 5 | 四象式神 | 从火鼠、水虎、风狸、雷犬中选择 1 只召唤：HP 20、AC 14、attack +5、主攻击 `1D8+2` 对应 `fire/freeze/physical_slash/lightning`；可执行 attack 或 help。 | 2 AP；持续 180 TU；`per_world_day=1`；`shared_usage_group=onmyoji_set_ritual_day`。 | `SUMMON` roster/choice/command；需四个正式 template。 |
| 8 | 强化契约 | `mp_max +20`。式神替换为 HP 25、AC 15、attack +6、主攻击 `1D10+3`；可同时选择 2 只不同基础式神，花 1 AP 指挥两只执行 attack 或 help。 | 升级同一次召唤；持续仍为 180 TU。 | `ATTR` + `SUMMON` multi-command/cap。 |
| 10 | 百鬼夜行 | 从 12 个显式 `shikigami_template_id` 中无放回随机 `1D4+1` 只，持续 300 TU；基础数值同 8 件，每种另有一个 formal typed element/control action。花 1 AP 可统一指定同一目标或分别指定合法目标。式神被摧毁时，对 1 格内敌人造成 `2D8` 对应元素伤害，equipment-origin，不触发式神自身 on-hit/on-death。 | 2 AP；沿用 `onmyoji_set_ritual_day`，不增加第二次当日 usage；随机结果提交后冻结。 | `SUMMON` 内容/随机/群体命令/性能预算；缺 8 个 template 是内容 blocker。 |

单件交互与 usage：76.6 项链、76.9 卷轴与套装 ritual 次数独立；同一 owner 的 active shikigami 总 cap=6，超过时按最旧召唤顺序移除。单件的“辅助法术”必须由 template 的正式 skill_id 定义，不能从描述字符串推导。

落地缺口：除 summon runtime 外，必须先补齐 12 个稳定 template ID、元素/控制 skill、基础认知、群体命令和随机 preview；在 roster 不完整时10件阈值不得注册。

## 77 夜叉 / `yaksha_set`

拓扑：蜕变 `3/5/7/10`。三件建立力量，五件在低血进入怒势，七件完成战鬼体魄，十件才付出自伤与疲劳进入阿修罗形态。校准：十件总价 142,000；低血攻击成长采用替换组，不把 +2/+2 与全局 +2 无上限相加。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 3 | 夜叉血 | `strength +2`；`check(intimidation,+3)`。 | 常驻。 | `ATTR` + utility check query。 |
| 5 | 血怒 | HP < 50% 时 `attack_roll_bonus +1`；HP < 25% 时替换为 `+2`，不再叠加前一档。`replacement_group=yaksha_bloodied_attack`。 | 每次攻击查询当前 HP ratio。 | `EA` HP-ratio conditional attack query。 |
| 7 | 战鬼体魄 | `attack_bonus +2`；`hp_max +20`。首次激活阈值增加的 20 max HP 不同时治疗；失活时 current HP clamp 到新 max。 | 常驻 derived source，换装立即刷新。 | `ATTR` + deterministic HP clamp。 |
| 10 | 阿修罗形态 | 300 TU 内 body size +1、`strength +4`（最终值 cap 24）、移动 +1；每个 qualifying melee 主直接段追加 `2D8 physical_blunt + 1D8 negative_energy`。每次 melee hit 可令目标 DC17 strength save，失败 `grappled` 30 TU。穿戴者每 30 TU 受到 `1D6 negative_energy` self-damage；结束获得 1 层 `exhaustion`，直到 long rest。 | 1 AP；`per_world_day=1`；自伤不能触发受伤反应/吸血。 | transform/body size、segment query、grapple、scheduled self-damage、world-rest exhaustion。 |

单件交互与 usage：77.5 的击杀攻击层与 `yaksha_bloodied_attack` 是不同条件来源，可以相加，但前者仍受自身 +3 cap。77.9 战鬼觉醒与阿修罗形态 usage 独立；同字段取阿修罗形态值，两个形态不会把 strength +4 叠成 +8。

落地缺口：需要 HP-ratio query、body-size override、strength cap、命中后 grapple、不会触发反应的 self-damage 和跨世界日 exhaustion 清理。

## 78 机关傀儡师 / `karakuri_puppeteer_set`

拓扑：稳步循环 `2/4/6/8/10`。知识 → 陷阱 → 单傀儡 → 双傀儡/换位 → 完整剧场，每档都增加一个可操作部件。校准：十件总价 135,000；傀儡和单件召唤共享 active cap，不共享 usage。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 机关术 | `check(sleight_of_hand,+3)`、`check(investigation,+2)`。 | 常驻。 | utility check query。 |
| 4 | 伏机关 | 在 4 格内合法空格放置一个陷阱，180 TU 内首次敌人进入即触发并消失：毒针 `2D6 physical_pierce` + DC16 constitution save，失败 `poisoned` 60 TU；闪光 DC16 constitution save，失败 `blinded` 30 TU；绳索 DC16 agility save，失败 `restrained` 30 TU。 | 1 AP；`per_battle=2`；active trap cap=2。 | `ZONE` cell trigger + typed outcome choice + control save。 |
| 6 | 单傀儡 | 召唤 1 个机关傀儡：HP 35、AC 16、attack +6、攻击 `2D8+3 physical_blunt`，持续 180 TU；花 1 AP 指挥 attack、guard 或 move。 | 2 AP；`per_world_day=1`；`shared_usage_group=puppet_theater_day`。 | `SUMMON` template/command。 |
| 8 | 双线操演 | `mp_max +15`；傀儡数替换为 2。每 30 TU 一次，花 1 AP 可与任一傀儡原子换位，双方落点都必须合法。 | 升级同一次 `puppet_theater_day`；持续仍为 180 TU。 | `ATTR` + multi-summon + atomic swap。 |
| 10 | 傀儡剧场 | 两傀儡替换为 HP 45、AC 17、持续 300 TU。1 AP 同时指挥二者，各自选择：shield（incoming-hit reaction 时序、0 AP，吸收下一次 directed hit，最多 20 damage）、charge（直线 4 格，命中 `3D8 physical_blunt` 并推 2 格）、self-destruct（毁灭自身，对半径 2 格敌人 `4D8 fire`，DC17 agility save 半伤）。 | 不增加第二次当日 usage；每个傀儡每次 command 只执行一种模式。 | `SUMMON` + reaction shield + forced move + AoE；性能/AI gap。 |

单件交互与 usage：78.9 单件傀儡、78.7 控制的构造体与套装傀儡次数独立，但套装 owner 的 active puppet cap=2；超过时先移除最旧套装傀儡。78.8 额外动作不能绕过“每 30 TU 每傀儡一次 command”的预算。

落地缺口：需要格上陷阱、召唤模板、群体 command budget、原子换位、shield damage fact、自爆防递归和 AI 多体规划。

## 79 雷神之鼓 / `raijin_drum_set`

拓扑：节奏/诡术 `2/3/6/9/10`。二件打底、三件建立雷鸣节拍、六件提高施法命中、九件形成重复命中节奏，十件奏响三拍终曲。校准：十件总价 135,000。原中央 2 件触发文本在 `set_bonus_design.md:2141` 损坏；本文规范 tag 唯一为 `raijin_drum_set`，不修改原文。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 雷鼓心 | `check(performance,+3)`；`save(constitution,+2)`。 | 常驻。 | utility check query + `TRAIT`。 |
| 3 | 雷鸣拍 | 每个 qualifying weapon/spell 主直接段追加 `1D6 lightning`，`replacement_group=raijin_lightning_append`。 | 遵守第 4.2 节。 | `EA` segment query。 |
| 6 | 召雷者 | `spell_hit +1`，只匹配 spell-origin attack check。 | 常驻。 | outgoing spell-hit query。 |
| 9 | 三连拍 | 穿戴者每 30 TU 第一次以 lightning 主段命中获得 1 层 `thunder_beat`，最多 3 层；达到 3 层时下一次 lightning hit 消耗全部层，目标 DC16 constitution save，失败 `stunned` 30 TU。 | 每 30 TU 最多加 1 层；层到战斗结束。 | `EA` damage-tag hit fact + `STATUS` stack/control。 |
| 10 | 雷神鼓终曲 | 持续 90 TU，每 30 TU 生成 `1D4+1` 道雷击；每道指定 8 格内敌人，造成 `3D10 lightning`，DC17 agility save 半伤。同一目标在同一终曲内第二次及以后被命中时另受 `2D10 lightning`，并 DC17 constitution save，失败 `stunned` 30 TU；每目标每个 tick 最多一次额外伤害/眩晕。持续期间友方对 lightning/thunder 为 immune，`attack_bonus +1`。 | 2 AP；`per_world_day=1`；固定 3 个 tick，随机 bolt 数每 tick 提交后冻结。 | `ZONE` scheduled multi-target roll + ally `TRAIT` aura + `STATUS`。 |

单件交互与 usage：79.7 戒指的 `1D6 lightning` 与3件阈值同属 `raijin_lightning_append`，只取一个 `1D6`，不产生两段。其他单件雷击次数独立。终曲伤害是 set-generated，不生成 `thunder_beat`，也不触发戒指附伤。

落地缺口：需要规范 tag 校验、每 tick 随机 bolt、同目标命中计数、友军临时 immunity aura、scheduled preview 和多目标 AI 分配。

## 80 河童铠甲 / `kappa_armor_set`

拓扑：稳步循环 `2/4/6/8/10`。先得到水域防护，再取得水域行动、力量成长、抽取，十件才开放归零昏迷和拖拽。校准：十件总价 130,000；水下移动和治疗已有多件来源，套装不再添加被动每 activation 治疗。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 河甲 | `armor_ac_bonus +1`；`check(athletics,+3)`。 | 常驻。 | `ATTR` + utility check query。 |
| 4 | 水陆两栖 | 可水下呼吸、水面行走；处于 water terrain 时 `move_point_capacity_delta +2`，`replacement_group=kappa_water_move`。 | 常驻环境 query。 | `WORLD` water tag + movement mode。 |
| 6 | 水盘之力 | `strength +1`。 | 常驻。 | `ATTR`。 |
| 8 | 水盘抽取 | 指定相邻敌人，DC16 constitution save；失败受 `2D8 negative_energy` 并失去 `1D2 strength`，穿戴者获得等量 strength，双方变化持续 90 TU、每方 cap 3；成功半伤且不转移。 | 2 AP；`per_world_day=1`；`shared_usage_group=kappa_drain_day`。 | ability-state modifier + timed stat transfer。 |
| 10 | 河童吸精 | 升级同一动作：伤害 `3D8 negative_energy`，抽取 `1D4 strength`，双方 cap 5，DC17。目标 strength 降至 0 时 `prone` 且 `unconscious` 60 TU；若双方都在 water terrain，失败目标还被向穿戴者拖拽最多 2 格。 | 不增加第二次当日 usage；forced move 逐格校验。 | timed stat drain/transfer、zero-ability semantic、water forced move。 |

单件交互与 usage：80.5 披风和其他水中治疗各自独立；`kappa_water_move` 与单件水中移动取最高 set-family 增量，不把“×3 冲刺”乘入被动。单件力量增益和套装抽取可相加，但最终 strength 仍受正式能力上限规则。

落地缺口：需要 water terrain fact、呼吸/水面 movement mode、临时能力值 drain/transfer、能力归零语义和水中逐格拖拽。

## 81 蒸汽骑士 / `steam_knight_set`

拓扑：堡垒 `4/6/8/10`。四件才启动套装底盘，六件建立热压输出，八件完成攻击 chassis，十件以明确自伤和冷却换取过载。校准：十件总价 142,000；单件已有额外动作与蒸汽超载，必须共享 overdrive usage。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 4 | 蒸汽骨架 | `strength +1`；`armor_ac_bonus +1`。 | 常驻。 | `ATTR`。 |
| 6 | 热压循环 | `move_point_capacity_delta +1`；每个 qualifying melee weapon 主直接段追加 `1D4 fire`，`replacement_group=steam_heat_append`。 | 常驻；追加段遵守第 4.2 节。 | passive move + `EA` segment query。 |
| 8 | 红线校准 | `attack_bonus +2`；受到自身以外的 fire damage 后，下一次 melee weapon attack `attack_roll_bonus +1`，30 TU 内有效。 | 条件加值每 30 TU 最多生成一次，使用后消耗。 | `ATTR` + `EA` incoming-damage fact/one-shot status。 |
| 10 | 蒸汽过载 | 90 TU 内每次自身 activation 开始获得 +2 temporary AP，只能用于 move 或 basic weapon attack；移动 +2；qualifying melee 主段的热压追加替换为 `2D6 fire`。每 30 TU 穿戴者受到 `1D6 fire` self-damage，该伤害忽略来自本套与共享 overdrive 单件的 fire mitigation。结束后 `action_lock` 30 TU。若过载 self-damage 使穿戴者 HP 降至 0，立刻对半径 2 格所有单位造成 `4D8 fire`，DC17 agility save 半伤。 | 1 AP；`per_world_day=1`；`shared_usage_group=steam_overdrive_daily`。 | `EA` AP restriction + `STATUS` + scheduled self-damage + lethal explosion。 |

单件交互与 usage：81.7 齿轮戒、81.9 微型蒸汽机与10件阈值共用 `steam_overdrive_daily`；任一使用后其余来源当日不可再启动 overdrive。81.5/81.10 的蒸汽区独立，但同格同 tick 只各结算自身正式来源，不复制6件阈值的热压追加。

落地缺口：需要 temporary AP 的用途白名单、共享 usage、self-damage mitigation bypass、结束 action lock 和致命伤害前后的单次爆炸提交；AI 必须计入过载自杀风险。

## 82 发条之心 / `clockwork_heart_set`

拓扑：节奏/诡术 `2/3/6/9/10`。先攻与校准是早期齿轮，九件建立时间领域，十件才改变双方 AP 节奏。校准：十件总价 140,000；时间齿轮单件与套装同名能力共享 usage，避免一天开启两个领域。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 精密发条 | `battle_start_progress +3`；`check(investigation,+2)`。 | battle start 与 utility query。 | OnBattleStart progress producer + utility check gap。 |
| 3 | 校准模式 | 战斗开始选择 attack/save/check 之一，120 TU 内对应 d20 `roll_bonus +2`；同一时刻只能有一个模式。 | battle-start 自动时序、0 AP；`per_battle=1`；AI 在开始时按期望收益选择。 | `EA` battle-start choice + `STATUS` typed roll category。 |
| 6 | 齿轮施法 | `spell_hit +1`，只匹配 spell-origin attack。 | 常驻。 | outgoing spell-hit query。 |
| 9 | 时间齿轮领域 | 放置一个 gear core（HP 20、AC 16），生成半径 3 格领域 60 TU。友军 `attack_roll_bonus +1`、移动 +1；敌军 `attack_roll_penalty -1`、移动 -1。 | 2 AP；`per_world_day=1`；`shared_usage_group=clockwork_time_gear_daily`。 | `ZONE` + battle object + ally/enemy conditional modifiers。 |
| 10 | 同步失衡 | 领域持续替换为 90 TU；友军每次 activation 开始获得 +1 temporary AP、移动 +2、攻击 +2；敌军每次 activation 开始失去 1 current AP（最低保留 1）、移动 capacity 减半且攻击 rolls disadvantage。相邻单位可花 2 AP 进行 DC18 `check(arcana)`，成功摧毁 gear core 并结束领域；其他 dispel 不生效。 | 升级 9 件同一次领域，不增加 usage。 | `ZONE` + AP cadence + typed utility interaction/object lifetime。 |

单件交互与 usage：82.9 时间齿轮与套装领域共用 `clockwork_time_gear_daily`。82.6 额外 activation、82.8 自身回溯不共享该次数，但 temporary AP 每 activation 仍最多从本领域取得 +1，不能因额外 activation 重复初始化同一 activation。

落地缺口：需要 battle-start choice、gear core object、范围跟踪、AP 上下限、动作 activation identity 和可交互 Arcana 检定；preview/AI 必须展示 core 可被摧毁后的期望持续时间。

## 83 以太行者 / `aether_walker_set`

拓扑：蜕变 `3/5/7/10`。三件感知以太，五件短暂相位，七件完成相位后的施法循环，十件撕开以太风暴。校准：十件总价 140,000；相位不是“免疫一切”，force、psychic 和明确可跨位面的效果仍可命中。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 3 | 以太共鸣 | `check(arcana,+3)`；`save(agility,+2)`。 | 常驻。 | utility check query + `TRAIT`。 |
| 5 | 相位步 | 进入 incorporeal 30 TU：可穿过非 barrier 实体格，不能进行 physical attack；免疫非魔法 physical damage，但 force、psychic、spell 和跨位面标签仍正常结算。结束时可在起点 6 格内选择 canonical-valid 落点；无合法落点则回到起点最近合法格。 | 1 AP；`per_world_day=2`；`shared_usage_group=aether_phase_daily`。 | phase layer、targetability、wall traversal、safe return。 |
| 7 | 以太余辉 | `spell_hit +1`。每次相位结束后 60 TU 内，第一次 qualifying spell 主直接段追加 `1D6 force`。 | 每次 phase 最多一次；使用后消耗。 | spell-hit query + one-shot segment modifier。 |
| 10 | 以太风暴 | 指定 6 格内一点创建半径 3 格裂隙 90 TU。生成时敌人 DC17 willpower save；失败受 `4D10 force + 2D10 psychic` 并 `disoriented` 60 TU，成功半伤无状态。之后每 30 TU，区域内所有单位受 `2D10 force`，DC17 agility save 半伤。穿戴者可每 30 TU 花 1 AP 传送至裂隙内合法格。结束时仍在区域内的每个单位独立进行 20% outcome roll，命中者被移出相位并 `stunned` 30 TU，随后回到最近合法格。 | 2 AP；`per_world_day=1`；固定 3 tick。 | `ZONE` + mixed damage + phase/remove-return + random outcome。 |

单件交互与 usage：83.7 相位戒、83.9 以太罗盘与5件阈值次数独立，但相位状态取不可叠加的最高 targetability/mitigation 规则；重复进入只刷新较长 remaining TU。10件阈值的 tick 是 set-generated，不消费7件阈值的以太余辉。

落地缺口：需要 phase targetability matrix、安全出现位置、裂隙区域、全单位 tick、短暂位面移出和随机 preview；barrier 是否可穿必须由统一 edge face 决定。

## 84 磁力大师 / `magnet_master_set`

拓扑：稳步循环 `2/4/6/8/10`。感知 → 单目标极性 → 攻击 chassis → 磁荷 → 三分支磁极反转。校准：十件总价 135,000；单件披风“金属武器自动 miss”已是局部平衡离群，但不在本文件改写。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 磁场感知 | `save(strength,+2)`、`check(investigation,+2)`；感知 6 格内带 canonical metal tag 的物品/装备；对穿 metal body armor 的目标 `attack_roll_bonus +1`。 | 常驻 query。 | `TRAIT` + utility check + equipment-tag fact。 |
| 4 | 极性拨动 | 对 6 格内持有/穿着 metal 装备的单位执行 pull 或 push 3 格，DC16 strength save 抵抗；逐格遇阻即停止，不穿越 barrier。 | 1 AP；`per_battle=2`。 | forced movement + metal equipment selection。 |
| 6 | 磁化武装 | `attack_bonus +1`；对 metal-armored 目标的条件加值替换为 `+2`，`replacement_group=magnet_metal_attack`。 | 常驻。 | `ATTR` + conditional attack query。 |
| 8 | 磁荷 | 每次穿戴者命中带 metal 装备的目标后施加 1 层 `magnetic_charge`，最多 3 层，每次攻击最多 1 层。达到 3 层时目标当前 metal weapon 被 disarmed；无武器则 body armor `armor_ac_bonus -2` 60 TU，并清空层数。 | 每目标每 30 TU 最多触发一次三层效果。 | `EA` hit/equipment fact + `STATUS` + disarm/AC component override。 |
| 10 | 磁极反转 | 选择一项：吸引——半径 6 格内合格敌人 DC17 strength save，失败向中心移动 3 格、`prone`，到中心 1 格内再受 `3D10 physical_blunt`；排斥——半径 2 格内合格敌人失败后推离 4 格并 disarm，撞击障碍受 `2D10 physical_blunt`；粉碎——指定 6 格内一件 metal weapon/body armor，对其 durability 造成 `4D10 force`，并进行 30% broken outcome。传奇/indestructible 装备不永久 broken，只失效 30 TU。 | 2 AP；`per_world_day=1`；每次只选一项。 | forced move + `DURABILITY` + equipment targeting/outcome table。 |

单件交互与 usage：所有 pull/push 在同一事件只执行距离最大的一个；84.2/84.5 的偏转与套装无共享次数。84.6、84.9 和10件阈值的 AC/破损都写同一 equipment instance 状态，不能生成平行的“磁损”字段。

落地缺口：需要 metal closed tag、敌方装备实例、disarm、临时 AC component override、durability/broken/repair 和 forced-move collision damage。

## 85 光子剑士 / `photon_swordsman_set`

拓扑：节奏/诡术 `2/3/6/9/10`。二件打底、三件光子化、六件完成攻击曲线、九件试作一次光速突袭，十件把同一 usage 升级为三连斩。校准：十件总价 140,000；三连斩是三次真实攻击 transaction，不是一段三倍伤害。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 光子校准 | `attack_bonus +1`，`replacement_group=photon_attack_growth`；`agility +1`。 | 常驻。 | `ATTR`。 |
| 3 | 光子武装 | equipped weapon 视为 magical；每个 qualifying weapon 主直接段追加 `1D6 radiant`，`replacement_group=photon_radiant_append`。 | 常驻；不改变武器原 damage tag。 | effective weapon trait + segment query。 |
| 6 | 光速剑技 | `attack_bonus +2` 替换 2 件值；melee weapon `crit_threshold_delta=-1`。 | 常驻。 | `ATTR` + critical-threshold query。 |
| 9 | 光速突袭 | 传送至 6 格内敌人相邻合法格，进行 1 次 canonical basic weapon attack；目标不能对本次移动/攻击使用 reaction，攻击忽略 half/three-quarter cover，但仍要求 line of effect。 | 2 AP；`per_world_day=1`；`shared_usage_group=photon_light_speed_slash_daily`。 | teleport + immediate weapon attack + reaction/cover suppression。 |
| 10 | 光速三连斩 | 升级同一动作：在 6 格范围内依次选择最多 3 个合法目标（可重复），每次先传送到相邻合法格再进行一记独立 basic weapon attack；三次攻击都禁 reaction、忽略 half/three-quarter cover。全部完成后传送至 6 格内任一合法格。 | 不增加 usage；任一步无合法落点时跳过该目标，不回退已提交攻击。 | canonical multi-attack/teleport transaction + safe landing + AI sequence search。 |

单件交互与 usage：85.3/85.9 的光子剑和3件阈值的武器光子化取正式 effective weapon 定义；同名 radiant append 只取 `photon_radiant_append` 最高值。三连斩由套装 granted action 生成，按第 4.2 节不触发套装/装备附伤，仍保留武器自身基础命中、暴击和伤害。

落地缺口：需要 effective weapon mutation-free projection、cover/reaction suppression、三段 canonical attack+teleport 序列、失败步提交规则和 AI 路径搜索。

## 86 音波战士 / `sonic_warrior_set`

拓扑：节奏/诡术 `2/3/6/9/10`。低档建立音刃，九件完成三拍共振，十件才释放穿墙超声波。校准：十件总价 135,000；单件共鸣戒与套装附伤共享 replacement family，不叠成两段 `1D6`。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 音刃 | `attack_bonus +1`，`replacement_group=sonic_attack_growth`；`check(performance,+2)`。 | 常驻。 | `ATTR` + utility check query。 |
| 3 | 共鸣段 | 每个 qualifying melee weapon/spell 主直接段追加 `1D6 thunder`，`replacement_group=sonic_thunder_append`。 | 遵守第 4.2 节。 | `EA` segment query。 |
| 6 | 战斗共振 | `attack_bonus +2` 替换 2 件值。 | 常驻。 | `ATTR`。 |
| 9 | 三拍破频 | 每 30 TU 对同一目标首次造成 thunder 主伤害时施加 1 层 `resonance`，最多 3 层。达到 3 层时下一次 thunder hit 消耗全部层；目标 DC16 constitution save，失败 `deafened` 60 TU 且 `stunned` 30 TU。非魔法 glass/crystal/ceramic battle object 在三层触发时直接 broken。 | 每目标每 30 TU 最多加 1 层；层持续 90 TU并按再次命中刷新。 | `EA` damage-tag fact + `STATUS` + `WORLD` material object。 |
| 10 | 超声波 | 以自身为中心半径 4 格；所有单位 DC17 constitution save。失败受 `5D8 thunder + 3D8 force`、`deafened` 60 TU、`stunned` 30 TU；成功半伤并 `deafened` 30 TU。非魔法 glass/crystal/ceramic object broken。line of effect 被墙阻断的目标仍可命中，但伤害骰减半且不施加 stunned；完全密封且具 `soundproof` edge tag 的屏障阻断。 | 2 AP；`per_world_day=1`。 | 正式 SkillDef + material query + barrier acoustic rule + mixed damage/control。 |

单件交互与 usage：86.7 戒指和3件阈值同属 `sonic_thunder_append`，只取一段 `1D6`。86.8 silence zone 可阻断带 verbal requirement 的技能，但不能反向取消已经生成的超声波 transaction。超声波 set-origin 不生成 resonance 层。

落地缺口：需要 damage-tag 层数、材质 object、acoustic edge face、穿墙半伤、soundproof tag 和跨屏障 preview；不能用“墙后”字符串或距离近似。

## 87 重力行者 / `gravity_walker_set`

拓扑：仪式/领域 `2/5/8/10`。二件适应重力，五件改变自身方向，八件放置小型重力井，十件升级为完整三拍领域。校准：十件总价 140,000；多个单件重力井只刷新/取最强场值，不乘算减速。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 引力适应 | `save(strength,+2)`、`check(acrobatics,+3)`；免疫 falling damage。 | 常驻。 | `TRAIT` + utility check + fall rule。 |
| 5 | 重力转向 | 60 TU 内可在 wall/ceiling surface 行走，move capacity -1；每 30 TU 可花 1 AP 改变一次重力方向。效果结束时使用 canonical safe fall/landing。 | 1 AP 开启；`per_battle=1`。 | movement mode、surface/edge、safe landing。 |
| 8 | 重力种子 | 指定 6 格内一点创建半径 2 格领域 60 TU。敌人移动 -1；每 30 TU 受 `1D8 force`，DC16 strength save 半伤；跨越领域边界的 ranged attack `attack_roll_penalty -1`。 | 2 AP；`per_world_day=1`；`shared_usage_group=gravity_well_daily`。 | `ZONE` scheduled tick + boundary-crossing attack query。 |
| 10 | 重力井 | 领域半径替换为 3 格、持续 90 TU；敌人 move capacity 减半、jump capacity=0，每 30 TU 受 `2D10 force`，DC17 strength save 半伤；进入或离开的 ranged attack rolls disadvantage。穿戴者每 30 TU 可花 1 AP 令领域内一敌人 DC16 strength save，失败 `prone`。结束时敌人受 `3D10 force`，DC17 strength save 半伤；失败者再被推离中心 2 格。 | 升级同一次 `gravity_well_daily`，不增加 usage。 | moving/anchored `ZONE`、ranged path intersection、forced move。 |

单件交互与 usage：87.2、87.6 的重力井与套装次数独立；重叠区域对 move/jump/ranged 只取最强限制，各来源伤害 tick 仍按自己的 schedule 结算。87.9 瞬发坍缩独立，不延长套装领域。

落地缺口：需要 wall/ceiling movement、jump capacity、攻击路径穿越领域判定、领域重叠规则、结束爆发和安全 forced move。

## 88 量子幽灵 / `quantum_ghost_set`

拓扑：节奏/诡术 `2/3/6/9/10`。两件知识、三件不确定态、六件施法、九件区域坍缩，十件才追加安全定位与偷袭。校准：十件总价 142,000；save-only AoE 不伪装成“攻击 miss”。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 不确定性 | `check(stealth,+3)`、`check(arcana,+2)`。 | 常驻。 | utility check query。 |
| 3 | 量子态 | 30 TU 内，每次 directed attack 命中前有 50% roll gate 使其 miss；对 agility save-only AoE 改为 save advantage，不进行 miss roll。穿戴者自己的 attack rolls disadvantage。 | 1 AP；`per_world_day=2`；`shared_usage_group=quantum_state_daily`。 | directed-hit gate + save-only distinction + `STATUS`。 |
| 6 | 坍缩施法 | `spell_hit +1`。 | 常驻。 | outgoing spell-hit query。 |
| 9 | 量子坍缩 | 指定 8 格内半径 4 格区域，每个敌人独立掷 `1D4`：1=`4D10 force`；2=随机传送最多 3 格至合法格，若无合法格则留在原地并受 `2D6 physical_blunt`；3=attack/save rolls disadvantage 30 TU；4=`2D10 psychic`。 | 2 AP；`per_world_day=1`；每目标 outcome 提交后冻结。 | `EA` outcome table + random safe teleport + AoE target snapshot。 |
| 10 | 确定存在 | 量子坍缩全部结算后，穿戴者选择区域内一个 canonical-valid 格传送，并对相邻一敌人进行一次 immediate basic weapon attack：attack advantage，且 `sneak_attack_context=true`。无合法位置时跳过该追加步骤，坍缩本身不回退。 | 升级同一次坍缩，不增加 usage。 | atomic post-AoE teleport + immediate attack + sneak context。 |

单件交互与 usage：88.5 披风与3件阈值次数独立，但同一 incoming directed hit 只进行一次最高 50% 的 quantum miss gate；88.2 的 25% 不再追加第二次 roll。9件阈值的 outcome 与88.9骰子独立。10件阈值的 immediate attack 是 equipment-generated，不触发装备/套装附伤。

落地缺口：需要 directed attack 与 save-only AoE 的正式分类、每目标独立随机、mutation-free preview、随机合法落点和 post-effect commit 顺序。

## 89 辐射行者 / `radiation_walker_set`

拓扑：仪式/领域 `2/5/8/10`。二件只建立合法 mitigation，五件产生衰变光环，八件形成辐射病，十件以会伤及友军和自身的临界质量收束。校准：十件总价 142,000；不使用无 owner 的“radiation immunity”替代 radiant/negative_energy 规则。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 辐射适应 | `res(radiant,half)`、`res(negative_energy,half)`；对 `poisoned` 和 disease tag immune。 | 常驻，mitigation 取最强 tier。 | `TRAIT` damage/save/status immunity。 |
| 5 | 衰变光环 | 以自身为中心 1 格的敌方 aura；敌人每 30 TU 受 `1D6 radiant`。`replacement_group=radiation_decay_aura`。 | 常驻跟随；进入时不立即追加一次 tick，按全局 30 TU schedule。 | following `ZONE` + scheduled enemy filter。 |
| 8 | 辐射病原 | `attack_bonus +1`。每 30 TU 第一次由穿戴者的 radiant/negative_energy qualifying 主段命中时，施加 1 层 `radiation_sickness`，最多 3 层；每层令 `hp_max -2`，持续 90 TU并刷新。current HP 随 max clamp，不在状态结束时治疗。 | 每目标每 30 TU 最多 1 层。 | `ATTR` + `EA` damage-tag fact + timed hp_max state。 |
| 10 | 临界质量 | 半径 4 格内所有单位，包括友军和穿戴者，进行 DC18 constitution save。失败受 `6D10 radiant + 4D10 negative_energy` 并获得强 radiation sickness 90 TU：每 30 TU `1D8 negative_energy`、`hp_max -5`；成功半伤并获得同状态 30 TU。非魔法 wood/leather/cloth object decay；metal equipment 获得 1 层 `rust`。施放者随后获得 1 层 `exhaustion` 至 long rest。 | 2 AP；`per_world_day=1`；自身只由2件阈值的 two half tiers减伤，不额外免疫。 | mixed AoE + `STATUS` upkeep/maxHP + `WORLD` material + `DURABILITY` + exhaustion。 |

单件交互与 usage：89.5、89.10 与5件阈值同属 `radiation_decay_aura`，同一目标同一 30 TU 只承受最高的 `1D6` aura tick；89.9 辐射风暴次数独立。强 radiation sickness 替换普通层参数，不与三层 `hp_max -6` 相加。

落地缺口：需要 following aura、typed disease immunity、hp_max 状态、全体含施放者 targeting、材质衰变、rust instance state、exhaustion 和对友伤的 AI 约束。

## 90 赛博行者 / `cyber_walker_set`

拓扑：节奏/诡术 `2/3/6/9/10`。侦查 → 弱点锁定 → 攻击 chassis → 预测过载 → 完整系统控制。校准：十件总价 142,000；`electronic_device`、`construct`、`mechanical_component` 必须是封闭 typed 分类，不能靠名称猜测。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 神经链接 | `check(investigation,+3)`、`check(sleight_of_hand,+2)`。 | 常驻。 | utility check query。 |
| 3 | 弱点扫描 | 感知 6 格内 canonical device/construct/mechanical target；对这些目标 `attack_roll_bonus +2`。 | 每次 target query 读取 closed classification。 | `EA` target-class fact + presentation。 |
| 6 | 战斗处理器 | `attack_bonus +1`，`replacement_group=cyber_attack_growth`。 | 常驻。 | `ATTR`。 |
| 9 | 预测过载 | 60 TU 内每 30 TU 对穿戴者的第一次 directed attack rolls disadvantage；穿戴者攻击忽略 half/three-quarter cover，但不能穿透 full cover/opaque barrier。`attack_bonus +2` 替换 6 件值。 | 1 AP；`per_world_day=1`；`shared_usage_group=cyber_overload_daily`。 | pre-attack cadence state + cover query + `ATTR`。 |
| 10 | 系统过载 | 持续替换为 90 TU；每个敌方来源每 30 TU 对穿戴者的第一次 directed attack 自动 miss。每 30 TU 穿戴者可花 1 AP hack 6 格内一个 construct/mechanical target，DC16 intelligence save；失败受控 30 TU，立即选择对其友方进行一次 basic attack，或 self-destruct 造成 `3D10 force` 并结束控制。过载结束后穿戴者 30 TU 内所有 d20 rolls disadvantage。 | 不增加第二次 usage；每目标同一时刻只能有一个 cyber control。 | auto-miss gate + immediate enemy action/control + reboot `STATUS`。 |

单件交互与 usage：90.7 黑客戒与套装 hack 次数独立，但同一目标不能被两份 cyber control 重复提交；后到来源只刷新较长剩余 TU。90.8/90.9 的免伤能力不改变9/10件阈值的 incoming attack cadence。所有 cover 忽略仍要求 line of effect。

落地缺口：需要 target closed classification、per-source/per-30-TU incoming gate、cover/line-of-effect 区分、敌方 immediate attack/self-destruct、控制冲突和 AI 的 reboot 代价。

## 91 创世泰坦 / `creation_titan_set`

拓扑：高魔 `3/6/9/10`。三件只建立泰坦体魄，六件创造单一临时物，九件完成战斗 chassis，十件才改变区域地形。校准：十件总价 167,000；临时造物不能成为套装成员、仓库物品或永久世界资产。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 3 | 原始体魄 | `strength +2`；`constitution +1`。 | 常驻。 | `ATTR`。 |
| 6 | 造物之手 | 选择一种临时造物，持续 180 TU且同一时刻最多 1 件：weapon construct（attack +1，基础伤害 `1D8+2 force`）；armor shell（`deflection_bonus +2`）；tool construct（选择一个 utility check，`check +3`）；barrier（相邻 edge，高 1 格，HP 30、AC 15）。 | 2 AP；`per_world_day=1`；造物离场即销毁，不写仓库。 | `EA` granted construct/effective equipment + barrier + utility choice。 |
| 9 | 泰坦武装 | `attack_bonus +2`；`armor_ac_bonus +2`。造物持续替换为 300 TU；barrier HP 替换为 40、AC 16。 | 静态值常驻；造物升级不增加 usage。 | `ATTR` + creation definition replacement。 |
| 10 | 世界重塑 | 指定 6 格内半径 4 格，选择一种，持续 300 TU并到期恢复：升起——创建高地，区域内友方 ranged attacks advantage、敌方 move capacity 减半，友方不额外获得 AC；陷落——敌人 DC18 agility save，失败受 `3D10 physical_blunt`、`prone` 并落入低地，成功仅移动减半 30 TU；壁垒——创建环形 wall，HP 50、AC 17，按真实 edge face 分割内外，不授予“免疫远程攻击”。 | 2 AP；`per_world_day=1`；每次只选一项。 | `WORLD` recoverable terrain + `ZONE` + barrier/height/line-of-effect。 |

单件交互与 usage：91.3、91.7 的创造次数与6件阈值独立；所有临时物使用 `temporary_creation` provenance，不参与 `member_item_ids`、装备阈值、掉落或出售。91.9 微型世界与10件阈值重叠时，地形 edge 由统一优先级求值，到期分别恢复各自改动。

落地缺口：需要临时 effective item、造物销毁、typed barrier/height、可逆地形 diff、区域恢复和 world/battle 分界；不得直接修改 TileMap 后依赖场景重载恢复。

## 92 虚空吞噬者 / `void_devourer_set`

拓扑：高魔 `3/6/9/10`。三件适应虚空，六件以击杀积累饱食，九件获得可控侵蚀，十件才触及存在抹消。校准：十件总价 167,000；绝对复活封锁保留为明确高风险 gap，不伪装成普通 death flag。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 3 | 虚空胃 | `res(force,half)`；`save(constitution,+2)`。 | 常驻。 | `TRAIT`。 |
| 6 | 饱食 | 穿戴者亲自击杀非召唤敌人时恢复 `1D10 HP` 并获得 1 层 `void_satiety`，最多 5 层；每层令穿戴者每个 qualifying 主直接段追加 `+1 negative_energy` flat damage。 | 每个击杀最多 1 层；层到战斗结束；equipment/set-generated kill 不触发。 | `EA` kill provenance + heal + segment flat modifier。 |
| 9 | 虚空侵蚀 | `attack_bonus +2`。达到 5 层饱食后，下一次 qualifying hit 消耗全部层，使目标 `hp_max -10` 90 TU；current HP clamp，不在恢复 max 时治疗。 | 每次五层循环一次；对同目标只刷新 90 TU，不叠加 -10。 | `ATTR` + timed hp_max state。 |
| 10 | 存在抹消 | 指定 12 格内可见敌人，DC18 constitution save。失败受 `8D10 negative_energy` 并从现实移出 30 TU：不可行动、选中、受伤或治疗，但自身已有 status/upkeep 继续按 TU 计时；成功受 `4D10 negative_energy` 且 `hp_max -10` 至 long rest。被移出期间若因既有 upkeep 归零，施加 `existence_erased`，禁止包括 wish 在内的全部复活。返回时使用原格或最近合法格。 | 2 AP；`per_world_day=1`。 | remove/return timeline + `REVIVE` absolute prohibition + safe placement；高风险规则 blocker。 |

单件交互与 usage：92.8 湮灭戒、92.9 虚空之眼与套装抹消各自独立；所有“无法复活”必须进入同一 `ReviveEligibilityResult`，取最严格正式来源，不能用描述文本判断。单件吸血和6件阈值的击杀治疗可各自结算，但同一击杀事件各来源最多一次。

落地缺口：需要 removed-unit 继续计时、不可选中 roster、hp_max clamp、绝对复活禁令、合法返回格和 preview 对期间 DOT 死亡概率的计算。

## 93 圣光仲裁者 / `holy_arbiter_set`

拓扑：仪式/领域 `2/5/8/10`。二件建立审判感知，五件锁定罪恶，八件形成律法光环，十件执行最终审判。校准：十件总价 167,000；`alignment_kind` 必须成为 typed fact，否则本套核心条件不得注册。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 审判之眼 | `res(radiant,half)`；`check(insight,+3)`；感知 6 格内单位的 `alignment_kind`，对 evil 目标 `attack_roll_bonus +1`，`replacement_group=holy_evil_attack`。 | 常驻 target query。 | `TRAIT` + utility check + alignment fact blocker。 |
| 5 | 定罪印 | 对 8 格内 evil 敌人施加 `condemned` 60 TU：其 supernatural/spell-like action 的 save DC/attack rolls -1，且穿戴者对其条件攻击加值替换为 +2。 | 1 AP；`per_battle=1`；DC17 willpower save 抵抗。 | `EA` action-category modifier + `STATUS`。 |
| 8 | 律法光环 | `attack_bonus +2`；`armor_ac_bonus +1`。2 格内友方对 `frightened`、`charmed` saves advantage；不授予免疫。 | 常驻 following aura。 | `ATTR` + `ZONE` ally save modifier。 |
| 10 | 最终审判 | 指定 12 格内 visible evil 目标，DC18 willpower save。失败受 `6D10 radiant + 4D10 force` 并 `condemned` 60 TU：不能使用 supernatural/spell-like action，attack rolls disadvantage；成功半伤且 condemned 30 TU。持续期间 4 格内友方对 frightened/charmed immune。目标在 condemned 期间死亡时，低于 `resurrection` 等级的复活被拒绝。 | 2 AP；`per_world_day=1`。 | mixed damage + action lock + ally aura + `REVIVE` rank rule。 |

单件交互与 usage：93.6/93.7 的审判次数独立；同一目标的 `condemned` 取最强 lock 和最长 remaining TU，不叠加多个攻击劣势。单件 good/evil 路径必须消费与套装同一 `alignment_kind`，不得各自维护名单。

落地缺口：alignment/faction 事实、supernatural/spell-like action category、条件 aura、复活等级和 UI 对“为何不可用”的解释全部缺 formal owner。

## 94 死亡使者 / `death_herald_set`

拓扑：仪式/领域 `2/5/8/10`。二件读生命比例，五件写入名单首位，八件强化并允许一次转移，十件完成三名名单和复活裁定。校准：十件总价 151,000；自动标记使用稳定规则，AI/玩家界面可在开战提交前预览。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 丧钟 | `res(negative_energy,half)`；`check(religion,+3)`；可读取 6 格内可感知单位的 HP ratio；对 HP < 25% 敌人 `attack_roll_bonus +2`。 | 常驻 target query。 | `TRAIT` + utility/HP fact。 |
| 5 | 名单首位 | battle start 标记一个可见敌人 90 TU；玩家在开战提交时选择，AI 默认选择 max_hp 最高者并以 unit_id 稳定打破平局。对标记目标攻击 +1，qualifying hit 追加 `1D6 negative_energy`。若 90 TU 内未击杀，标记结束且目标恢复 `2D8 HP`。 | battle-start 自动时序、0 AP；`per_battle=1`；`shared_usage_group=death_list_battle`。 | OnBattleStart selection + timed mark + failure heal。 |
| 8 | 收割优先级 | `attack_bonus +2`。标记攻击加值替换为 +3，追加替换为 `2D6 negative_energy`；若目标在期限内死亡，可立即选择下一名敌人并重新获得 90 TU，最多转移 1 次。 | 升级同一名单；set-generated append 不触发击杀附伤。 | `ATTR` + mark transfer/state。 |
| 10 | 死亡名单 | 成功转移上限替换为 2 次，即每场最多标记 3 名敌人。所有在标记有效时被击杀的目标获得 `death_list_soul_claimed`，拒绝 `raise_dead` 及同级复活，`resurrection` 及更高等级仍可用。每次未按期击杀都单独触发 `2D8` 缓刑治疗并结束整个名单。 | 不增加第二份 battle usage。 | multi-target mark ledger + `REVIVE` rank rule。 |

单件交互与 usage：94.5/94.7/94.10 的击杀治疗独立；94.9 即死与名单只通过正式 death event 相交。名单附伤遵守第 4.2 节，不能使自己再次生成附伤。低血条件加值与名单加值来源不同，可以相加。

落地缺口：需要 battle-start 玩家选择/AI稳定选择、mark deadline、转移 ledger、失败治疗、死亡 provenance 和复活等级；换装失去 10 件时不得删除已写入尸体的 soul claim。

## 95 星辰编织者 / `star_weaver_set` — P0 身份阻塞

拓扑蓝图：高魔 `3/6/9/10`。该拓扑仅用于评审，不得生成 Resource 或注册 runtime。Set 95 在 `sets_91_to_100.md:429-516` 声明的四个 `armor_star_weaver_*` 与 Set 11 的 `sets_11_to_15_armor.md:20-102` 重复，两套又共用 `star_weaver_set`；在用户授权身份消歧前，任何 membership/evaluator 都无法证明在计算哪一套。校准：按 Set 95 原十件总价 151,000 记录，但不能宣称可获取或可平衡。

| 阈值 | 名称 | 准确效果（BLOCKED blueprint） | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 3 | 星线 | `check(arcana,+3)`、`check(insight,+2)`；夜间 `check(divination,+3)` 并显示 canonical celestial state。 | 常驻；依赖 world time/celestial facts。 | `WORLD` + utility check；P0 identity。 |
| 6 | 星力储备 | `mp_max +20`；每个 qualifying radiant/force spell 主段追加 `1D4 radiant`，`replacement_group=star_thread_append`。 | 常驻；遵守第 4.2 节。 | `ATTR` + segment query；P0 identity。 |
| 9 | 星兆护线 | 为 8 格内一个友方施加 60 TU 星兆：第一次 directed attack hit 自动 miss，或第一次失败 save 自动成功，二者中先发生者消耗状态。 | 1 AP；`per_world_day=1`；`shared_usage_group=star_tapestry_daily`。 | pre-hit/save transaction；P0 identity。 |
| 10 | 命运织锦 | 消耗同一当日 usage，改为选择一项：重织——在 roll committed、effect 尚未提交时重掷一次 attack/save/damage roll并强制使用新结果；切断——终止目标一个 nonlegendary active beneficial status；编织——为友方 60 TU 内分别提供第一次 directed hit miss和第一次 failed save success，两项各消耗一次。 | 切断/编织为 2 AP；重织为 reaction 时序、0 AP；`per_world_day=1`，不因 9 件再多一次。 | roll transaction/status termination/protection；P0 identity blocker。 |

单件交互与 usage：95.6 命运星项链和 blueprint 的 `star_tapestry_daily` 次数独立；若未来用户要求合并，必须另行授权，本文不猜。95.1–95.10 仍按原 ID 列入附录，重复 ID 不做 suffix、display-name 或路径推断。

落地缺口：首要缺口是 P0 identity；其后仍需 celestial world fact、divination check、roll-before-effect commit、buff dispel rank 和一次性 hit/save 防护。P0 未解除前禁止创建测试来“证明”Set 95 可计数。

## 96 命运编织者 / `fate_weaver_set`

拓扑：高魔 `3/6/9/10`。三件读因果，六件取得一次强制改骰，九件建立可观测因果束缚，十件才把未来行动预支并反向控制。校准：十件总价 151,000；所有改骰必须发生在 effect commit 前，反射必须标记非递归 origin。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 3 | 因果线 | `check(insight,+3)`、`check(persuasion,+2)`。 | 常驻。 | utility check query。 |
| 6 | 改写线头 | 可见敌人的一次 attack roll 或 save roll 已掷出、effect 尚未提交时，强制重掷并使用新结果。 | reaction 时序、0 AP；`per_world_day=1`；套装独立 usage key。 | roll transaction interception。 |
| 9 | 因果束缚 | 指定 8 格内敌人，DC17 willpower save；失败获得 `causal_bound` 60 TU。其下一次完成的 attack/spell/move action 记录 category，并在完成后锁定同 category 30 TU；期间其对穿戴者造成的 final HP damage 有 25% 以 reflection origin 回到自身。 | 2 AP；`per_world_day=1`；`shared_usage_group=causal_inversion_daily`。 | action-category observation/lock + nonrecursive damage reflection。 |
| 10 | 因果倒置 | 升级 causal_bound：目标下一次声明 attack/spell/move 时，该 action 立即在当前 event window 执行，由穿戴者在原 action 的合法 target/direction 集合中选择；资源、AP、次数照常由目标支付。该 action 标记 consumed，不在目标下一 activation 再执行；随后同 category 锁定 30 TU。反射比例替换为 50%。 | 不增加第二次 `causal_inversion_daily` usage；无合法目标时 action 仍被预支并消耗。 | deferred/early action transaction、foreign target control、resource commit；重大 ABI gap。 |

单件交互与 usage：96.3 手套的三次、96.7 命运戒的一次和6件阈值的一次保留各自原 usage，不共享次数；但同一个 roll transaction 最多被任一来源重掷一次，后续改骰请求拒绝。96.5 命运披风的行动控制与 causal inversion usage 独立，但同一目标同一 event window 最多被一个 foreign-control source 接管。

落地缺口：需要 roll-before-effect hook、动作 category、外部控制下的合法目标枚举、提前执行与原 activation 去重、资源原子提交及 reflection provenance。

## 97 梦境之主 / `dream_lord_set`

拓扑：仪式/领域 `2/5/8/10`。二件进入梦境语义，五件施加单体睡眠，八件展开短梦域，十件升级为持续梦魇和队伍资源循环。校准：十件总价 151,000；当前项目以 MP 而非法术位为战斗资源，原“恢复 1 级法术位”明确重设计为一次 `5 MP`。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 2 | 入梦者 | `check(arcana,+3)`、`check(deception,+2)`；对 sleep/dream-tag effects immune；可读取 6 格内睡眠单位的 dream summary，但不自动获得隐藏事实。 | 常驻；world 情报由正式 dream projection 给出。 | `TRAIT` immunity + dream information owner。 |
| 5 | 梦触 | 4 格内目标 DC16 willpower save；失败 `asleep` 60 TU，受到 direct damage 后提前醒来；dream/sleep immune 目标不受影响。 | 1 AP；`per_battle=1`。 | `STATUS` sleep semantic + wake-on-damage。 |
| 8 | 梦境领域 | 以 6 格内一点创建半径 4 格领域 90 TU。敌人每 30 TU 进行 DC17 willpower save，失败 30 TU 内 attack/perception rolls disadvantage、move capacity 减半；友方在区域内对 frightened/charmed immune。穿戴者每 30 TU 可花 1 AP 将自己或一个友方传送到区域内合法格。`mp_max +25`。 | 2 AP；`per_world_day=1`；`shared_usage_group=dream_domain_daily`。 | `ZONE` scheduled save + ally aura + safe teleport + `ATTR`。 |
| 10 | 噩梦降临 | 领域持续替换为 300 TU。失败敌人的每 30 TU 第一次 targeted attack 另进行 50% misidentification roll；命中时改以攻击范围内最近的其他合法单位为目标，平局按 unit_id，可能攻击友方。每个友方每 30 TU 选择恢复 `1D8 HP` 或 `5 MP`；每名友方每个领域最多选择一次 MP，之后只能治疗。 | 升级同一 `dream_domain_daily`，不增加 usage；随机提交后冻结。 | foreign retarget、ally periodic choice、MP cap ledger、long `ZONE`。 |

单件交互与 usage：97.5 披风、97.10 徽章的 sleep/charm immunity 与2/8件阈值幂等。单件 sleep、coma 和套装 asleep 必须分别使用正式 status semantic；coma 不因普通 damage 醒来。多个 dream zone 重叠时每单位每 30 TU 只做一次最强 misidentification roll。

落地缺口：需要 dream summary、wake-on-damage、scheduled per-unit saves、foreign target retarget、每盟友 MP 选择 ledger 和领域内 safe teleport；AI 需避免在友军密集时误伤。

## 98 时间旅者 / `time_traveler_set`

拓扑：高魔 `3/6/9/10`。三件感知时间，六件观察 intended action，九件只回溯自身，十件才扩展至附近队伍。校准：十件总价 167,000；usage、消耗品、装备耐久、行动进度和世界状态永不回滚，避免资源复制。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 3 | 时流感知 | `battle_start_progress +3`；`check(history,+3)`。 | battle start / 常驻 check。 | OnBattleStart progress + utility check。 |
| 6 | 未来残影 | 30 TU 内显示所有当前可见敌人的 canonical intended action category、目标和路径；敌方重规划时更新投影，但不锁死其决策。`move_point_capacity_delta +2`。 | 1 AP；`per_battle=1`。 | intended-action read model + presentation/AI parity + passive move。 |
| 9 | 自身回溯 | 每次自身 activation 开始捕获 detached checkpoint。下一 activation 前可恢复自己的 HP、MP、position 和 battle status 集合至 checkpoint；不恢复 death state、AP、action progress、ability/gear-set usage、消耗品、装备状态或随机数流。位置被占时取最近合法格。 | reaction 时序、0 AP；`per_world_day=1`；`shared_usage_group=time_rewind_daily`。 | `TIME` checkpoint/detached snapshot/atomic restore。 |
| 10 | 同伴回溯 | checkpoint 同时捕获当时 6 格内友方。触发时对仍存活且仍在 battle roster 的捕获成员按 unit_id 稳定顺序原子恢复 HP、MP、position 和 battle statuses；敌人不变，死亡单位不复活。位置冲突使用最近合法格。恢复后这些单位 30 TU 内 attack rolls 和 saves `+2`。仍不恢复任何 usage/消耗/行动进度；另写入 world-time 24 小时 lock。 | 升级同一次 `time_rewind_daily`，不增加 usage。 | multi-unit `TIME` transaction + placement reservation + world-time lock。 |

单件交互与 usage：98.6 时间核心与98.9沙漏的回溯和套装共用 `time_rewind_daily`；任一使用后全部当日不可再回溯。98.7 额外 activation 和98.8停滞不回滚、不退款。checkpoint 不保存 authored Resource 或 raw Godot collection。

落地缺口：需要 canonical intended action、detached checkpoint、状态白名单/排除清单、多单位位置预留、原子恢复、24 小时 world lock、preview 和 save/writeback；这是高风险跨系统实现。

## 99 宇宙吞噬者 / `cosmic_devourer_set`

拓扑：高魔 `3/6/9/10`。三件适应宇宙力，六件吞噬形成星层，九件生成小型奇点，十件升级为三拍黑洞和终局爆发。校准：十件总价 167,000；五件单品已有黑洞拉拽，重叠区域必须共享移动限制而非重复位移。

| 阈值 | 名称 | 准确效果 | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 3 | 星噬体 | `res(force,half)`；`save(constitution,+2)`。 | 常驻。 | `TRAIT`。 |
| 6 | 星层 | 亲自击杀非召唤敌人时恢复 `2D8 HP` 并获得 1 层 `star_layer`，最多 3 层；每层令每个 qualifying 主直接段追加 `1D6 force`。 | 每击杀最多 1 层；层至战斗结束；遵守第 4.2 节。 | kill provenance + heal + segment query。 |
| 9 | 奇点种子 | `attack_bonus +2`。指定 6 格内一点创建半径 2 格奇点 60 TU；每 30 TU 敌人受 `2D10 force`，DC17 strength save，失败向中心移动 2 格并 `prone`，成功半伤不移动；跨区域 ranged attack rolls disadvantage。 | 2 AP；`per_world_day=1`；`shared_usage_group=cosmic_black_hole_daily`。 | `ZONE` scheduled damage/pull + ranged path query。 |
| 10 | 黑洞降生 | 奇点半径替换为 3 格、持续 90 TU、tick 伤害 `3D10 force`、save DC18。结束时中心 1 格内所有单位受 `4D10 force + 2D10 radiant`，DC17 agility save 半伤。持续期间每 30 TU 一次，穿戴者可花 1 AP 对中心 1 格内敌人施加 `3D8 force`（DC18 constitution save 半伤），自身恢复 `3D8 HP` 并获得 1 星层；同一目标每个黑洞最多被吸收一次。 | 升级同一次 `cosmic_black_hole_daily`；不增加 usage。 | moving/anchored `ZONE` + absorb ledger + mixed explosion。 |

单件交互与 usage：99.2、99.5–99.9 的 pull 与套装黑洞在同一 tick 只提交距离最大的一个 forced move；各来源伤害仍独立。套装黑洞/吸收伤害是 set-generated，不能触发6件阈值的击杀、星层附伤或再次吸收。

落地缺口：需要区域路径判定、重叠 forced-move arbitration、中心范围、per-target absorb ledger、scheduled explosion 和对友军终爆风险的 AI 评分。

## 100 创世神 / `creation_divine_set` — 平衡阻塞

拓扑蓝图：高魔 `3/6/9/10`。十件总价 257,000，但 `sets_91_to_100.md:970-987` 的 `armor_creation_divine_body` 单件描述已经无条件“免疫所有 damage”，其 AC chassis 也显著离群。在单件配置冻结前提下，没有任何套装奖励数值能让整套变得可平衡；本节只能设计**非防御向**奖励，不能宣称整体平衡、可玩或可落地。

| 阈值 | 名称 | 准确效果（provisional） | 触发与次数 / TU | typed owner / gap |
|---:|---|---|---|---|
| 3 | 创世知识 | `check(arcana,+3)`；`mp_max +15`，`replacement_group=creation_divine_mp_growth`。 | 常驻；不增加 AC、save、抗性、免疫、治疗或临时 HP。 | utility check + `ATTR`。 |
| 6 | 神言 | `attack_bonus +2`；`mp_max +30` 替换 3 件值；`spell_hit +1`。 | 常驻；仍不增加防御。 | `ATTR` + spell-hit query。 |
| 9 | 创世胚种 | 召唤 1 个 battle-only `genesis_avatar`：HP 40、AC 16、attack +7、攻击 `3D8 force`，持续 180 TU；花 1 AP 指挥 attack 或 move。结束后穿戴者获得 1 层 exhaustion 至 long rest。 | 2 AP；`per_world_day=1`；`shared_usage_group=creation_force_daily`。 | `SUMMON` formal template + world-rest exhaustion。 |
| 10 | 创世之力 | 消耗同一 usage，选择一项：创生——召唤 3 个 genesis avatar，单体改为 HP 35、AC 16、attack +8、`3D8 force`，持续 180 TU，1 AP 同时指挥；毁灭——12 格内目标 DC20 constitution save，失败受 `10D10 radiant + 5D10 force` 并移出现实 30 TU，成功半伤且 `stunned` 30 TU，移出不施加绝对复活禁令；重塑——指定半径 6 格，选择地裂（敌人 DC19 agility save，失败 `4D10 physical_blunt`、`prone`，区域 move -2 持续 180 TU）或元素涌泉（选择 fire/freeze/lightning，半径内敌人每 30 TU `3D10` 对应伤害，DC19 agility save 半伤，持续 90 TU）。任一分支结束/提交后穿戴者获得 2 层 exhaustion，替换 9 件的一层。 | 3 AP；`per_world_day=1`；不增加第二次 `creation_force_daily` usage。 | `SUMMON` / remove-return / `WORLD` recoverable terrain / `ZONE` + exhaustion。 |

单件交互与 usage：100.2 的全伤害免疫原样冻结并标为 balance blocker；本文所有阈值都不再给防御或治疗。100.7–100.9 的单件创造/毁灭/世界能力次数独立，但所有 battle-only creation 不能进入仓库、阈值计数或持久世界资产。10件阈值的“毁灭”分支与 Set 92 使用同一 remove/return owner，但明确不继承绝对复活封锁。

落地缺口：在用户另行授权处理单件全伤害免疫前，禁止把本表标为 balanced 或 production-ready。即使解除 blocker，仍需要召唤 template、群体 command、exhaustion、remove-return、可逆地形、元素领域和高代价 AI 评分。

## 5. 实施顺序与验收边界

推荐最小耐久切片：

1. 新增套装侧 authoring/definition，逐套显式保存附录中的 `member_item_ids[10]`；加载期拒绝空、重复、非装备和跨套重复成员。Set 95 必须在这一步 fail closed。
2. 建立 immutable registry、membership snapshot、累计 threshold evaluator、`GearSetThreshold` derived source、换装/损坏刷新和 `replacement_group`。
3. 先落 `ATTR` / `TRAIT` / 已有 `EA` 能承载的静态与简单条件档；再落 shared usage、状态和领域。
4. `DURABILITY`、`REVIVE`、`WORLD`、`TIME` 及 foreign action control 分别作为独立实现切片，不把缺失能力塞进 GearSet evaluator。
5. 最后接 preview、AI、UI、headless snapshot、usage save/writeback；在这些消费者齐全前文档始终保持 planned。

最低有效回归必须验证真实行为，而不是扫描文档文本：

- 每套恰有十个显式成员，按有效 equipment entry 去重；Set 95 以重复 ID/tag P0 明确失败。
- 每种拓扑的阈值边界、累计激活、降档撤销、损坏/修复和换装重算。
- `replacement_group` 最高档替换而不相加；mitigation tier 取最强。
- shared usage 在单件/套装任一入口消耗后另一入口拒绝；移除再装备不退款。
- 多段主直接伤害逐段触发；extra/DOT/terrain/reflection/self/equipment/unknown origin 不触发。
- preview/AI 不消费 usage、随机数、状态层、耐久或时间 checkpoint，且和正式执行输出同一 expected definition。
- 领域按绝对 TU 到期；格范围、forced move、barrier、safe landing 与 line of effect 走 canonical battle owner。
- Set 76 的十二式神内容未齐时注册失败；Set 100 的 balance blocker 在内容报告和 UI 中保持可见。

`docs/design/project_context_units.md` 仍可作为架构装载索引；本文没有落地新的 owner，因此本轮不修改它。正式实现若新增 GearSet registry/evaluator/source/save/UI 链，再更新 CU-02、CU-10、CU-13、CU-15、CU-16、CU-19 的推荐读取关系。

## 附录 A：冻结的 `member_item_ids[10]`

顺序固定为 `head, body, hands, feet, cloak, necklace, ring_1, ring_2, special_trinket, badge`。以下清单由原 `sets_*.md` 逐项提取；实现不得用命名前缀或 tag 重新推导。

- 68 `raven_lord_set`: [`armor_raven_lord_head`, `armor_raven_lord_body`, `armor_raven_lord_hands`, `armor_raven_lord_feet`, `acc_raven_lord_cloak`, `acc_raven_lord_necklace`, `acc_raven_lord_ring_1`, `acc_raven_lord_ring_2`, `acc_raven_lord_trinket`, `acc_raven_lord_badge`]
- 69 `rust_blade_set`: [`armor_rust_blade_head`, `armor_rust_blade_body`, `armor_rust_blade_hands`, `armor_rust_blade_feet`, `acc_rust_blade_cloak`, `acc_rust_blade_necklace`, `acc_rust_blade_ring_1`, `acc_rust_blade_ring_2`, `acc_rust_blade_trinket`, `acc_rust_blade_badge`]
- 70 `mirror_demon_set`: [`armor_mirror_demon_head`, `armor_mirror_demon_body`, `armor_mirror_demon_hands`, `armor_mirror_demon_feet`, `acc_mirror_demon_cloak`, `acc_mirror_demon_necklace`, `acc_mirror_demon_ring_1`, `acc_mirror_demon_ring_2`, `acc_mirror_demon_trinket`, `acc_mirror_demon_badge`]
- 71 `samurai_spirit_set`: [`armor_samurai_spirit_head`, `armor_samurai_spirit_body`, `armor_samurai_spirit_hands`, `armor_samurai_spirit_feet`, `acc_samurai_spirit_cloak`, `acc_samurai_spirit_necklace`, `acc_samurai_spirit_ring_1`, `acc_samurai_spirit_ring_2`, `acc_samurai_spirit_trinket`, `acc_samurai_spirit_badge`]
- 72 `ninja_shadow_set`: [`armor_ninja_shadow_head`, `armor_ninja_shadow_body`, `armor_ninja_shadow_hands`, `armor_ninja_shadow_feet`, `acc_ninja_shadow_cloak`, `acc_ninja_shadow_necklace`, `acc_ninja_shadow_ring_1`, `acc_ninja_shadow_ring_2`, `acc_ninja_shadow_trinket`, `acc_ninja_shadow_badge`]
- 73 `shaolin_monk_set`: [`armor_shaolin_monk_head`, `armor_shaolin_monk_body`, `armor_shaolin_monk_hands`, `armor_shaolin_monk_feet`, `acc_shaolin_monk_cloak`, `acc_shaolin_monk_necklace`, `acc_shaolin_monk_ring_1`, `acc_shaolin_monk_ring_2`, `acc_shaolin_monk_trinket`, `acc_shaolin_monk_badge`]
- 74 `tengu_mask_set`: [`armor_tengu_mask_head`, `armor_tengu_mask_body`, `armor_tengu_mask_hands`, `armor_tengu_mask_feet`, `acc_tengu_mask_cloak`, `acc_tengu_mask_necklace`, `acc_tengu_mask_ring_1`, `acc_tengu_mask_ring_2`, `acc_tengu_mask_trinket`, `acc_tengu_mask_badge`]
- 75 `nine_tailed_fox_set`: [`armor_nine_tailed_fox_head`, `armor_nine_tailed_fox_body`, `armor_nine_tailed_fox_hands`, `armor_nine_tailed_fox_feet`, `acc_nine_tailed_fox_cloak`, `acc_nine_tailed_fox_necklace`, `acc_nine_tailed_fox_ring_1`, `acc_nine_tailed_fox_ring_2`, `acc_nine_tailed_fox_trinket`, `acc_nine_tailed_fox_badge`]
- 76 `onmyoji_set`: [`armor_onmyoji_head`, `armor_onmyoji_body`, `armor_onmyoji_hands`, `armor_onmyoji_feet`, `acc_onmyoji_cloak`, `acc_onmyoji_necklace`, `acc_onmyoji_ring_1`, `acc_onmyoji_ring_2`, `acc_onmyoji_trinket`, `acc_onmyoji_badge`]
- 77 `yaksha_set`: [`armor_yaksha_head`, `armor_yaksha_body`, `armor_yaksha_hands`, `armor_yaksha_feet`, `acc_yaksha_cloak`, `acc_yaksha_necklace`, `acc_yaksha_ring_1`, `acc_yaksha_ring_2`, `acc_yaksha_trinket`, `acc_yaksha_badge`]
- 78 `karakuri_puppeteer_set`: [`armor_karakuri_puppeteer_head`, `armor_karakuri_puppeteer_body`, `armor_karakuri_puppeteer_hands`, `armor_karakuri_puppeteer_feet`, `acc_karakuri_puppeteer_cloak`, `acc_karakuri_puppeteer_necklace`, `acc_karakuri_puppeteer_ring_1`, `acc_karakuri_puppeteer_ring_2`, `acc_karakuri_puppeteer_trinket`, `acc_karakuri_puppeteer_badge`]
- 79 `raijin_drum_set`: [`armor_raijin_drum_head`, `armor_raijin_drum_body`, `armor_raijin_drum_hands`, `armor_raijin_drum_feet`, `acc_raijin_drum_cloak`, `acc_raijin_drum_necklace`, `acc_raijin_drum_ring_1`, `acc_raijin_drum_ring_2`, `acc_raijin_drum_trinket`, `acc_raijin_drum_badge`]
- 80 `kappa_armor_set`: [`armor_kappa_armor_head`, `armor_kappa_armor_body`, `armor_kappa_armor_hands`, `armor_kappa_armor_feet`, `acc_kappa_armor_cloak`, `acc_kappa_armor_necklace`, `acc_kappa_armor_ring_1`, `acc_kappa_armor_ring_2`, `acc_kappa_armor_trinket`, `acc_kappa_armor_badge`]
- 81 `steam_knight_set`: [`armor_steam_knight_head`, `armor_steam_knight_body`, `armor_steam_knight_hands`, `armor_steam_knight_feet`, `acc_steam_knight_cloak`, `acc_steam_knight_necklace`, `acc_steam_knight_ring_1`, `acc_steam_knight_ring_2`, `acc_steam_knight_trinket`, `acc_steam_knight_badge`]
- 82 `clockwork_heart_set`: [`armor_clockwork_heart_head`, `armor_clockwork_heart_body`, `armor_clockwork_heart_hands`, `armor_clockwork_heart_feet`, `acc_clockwork_heart_cloak`, `acc_clockwork_heart_necklace`, `acc_clockwork_heart_ring_1`, `acc_clockwork_heart_ring_2`, `acc_clockwork_heart_trinket`, `acc_clockwork_heart_badge`]
- 83 `aether_walker_set`: [`armor_aether_walker_head`, `armor_aether_walker_body`, `armor_aether_walker_hands`, `armor_aether_walker_feet`, `acc_aether_walker_cloak`, `acc_aether_walker_necklace`, `acc_aether_walker_ring_1`, `acc_aether_walker_ring_2`, `acc_aether_walker_trinket`, `acc_aether_walker_badge`]
- 84 `magnet_master_set`: [`armor_magnet_master_head`, `armor_magnet_master_body`, `armor_magnet_master_hands`, `armor_magnet_master_feet`, `acc_magnet_master_cloak`, `acc_magnet_master_necklace`, `acc_magnet_master_ring_1`, `acc_magnet_master_ring_2`, `acc_magnet_master_trinket`, `acc_magnet_master_badge`]
- 85 `photon_swordsman_set`: [`armor_photon_swordsman_head`, `armor_photon_swordsman_body`, `armor_photon_swordsman_hands`, `armor_photon_swordsman_feet`, `acc_photon_swordsman_cloak`, `acc_photon_swordsman_necklace`, `acc_photon_swordsman_ring_1`, `acc_photon_swordsman_ring_2`, `acc_photon_swordsman_trinket`, `acc_photon_swordsman_badge`]
- 86 `sonic_warrior_set`: [`armor_sonic_warrior_head`, `armor_sonic_warrior_body`, `armor_sonic_warrior_hands`, `armor_sonic_warrior_feet`, `acc_sonic_warrior_cloak`, `acc_sonic_warrior_necklace`, `acc_sonic_warrior_ring_1`, `acc_sonic_warrior_ring_2`, `acc_sonic_warrior_trinket`, `acc_sonic_warrior_badge`]
- 87 `gravity_walker_set`: [`armor_gravity_walker_head`, `armor_gravity_walker_body`, `armor_gravity_walker_hands`, `armor_gravity_walker_feet`, `acc_gravity_walker_cloak`, `acc_gravity_walker_necklace`, `acc_gravity_walker_ring_1`, `acc_gravity_walker_ring_2`, `acc_gravity_walker_trinket`, `acc_gravity_walker_badge`]
- 88 `quantum_ghost_set`: [`armor_quantum_ghost_head`, `armor_quantum_ghost_body`, `armor_quantum_ghost_hands`, `armor_quantum_ghost_feet`, `acc_quantum_ghost_cloak`, `acc_quantum_ghost_necklace`, `acc_quantum_ghost_ring_1`, `acc_quantum_ghost_ring_2`, `acc_quantum_ghost_trinket`, `acc_quantum_ghost_badge`]
- 89 `radiation_walker_set`: [`armor_radiation_walker_head`, `armor_radiation_walker_body`, `armor_radiation_walker_hands`, `armor_radiation_walker_feet`, `acc_radiation_walker_cloak`, `acc_radiation_walker_necklace`, `acc_radiation_walker_ring_1`, `acc_radiation_walker_ring_2`, `acc_radiation_walker_trinket`, `acc_radiation_walker_badge`]
- 90 `cyber_walker_set`: [`armor_cyber_walker_head`, `armor_cyber_walker_body`, `armor_cyber_walker_hands`, `armor_cyber_walker_feet`, `acc_cyber_walker_cloak`, `acc_cyber_walker_necklace`, `acc_cyber_walker_ring_1`, `acc_cyber_walker_ring_2`, `acc_cyber_walker_trinket`, `acc_cyber_walker_badge`]
- 91 `creation_titan_set`: [`armor_creation_titan_head`, `armor_creation_titan_body`, `armor_creation_titan_hands`, `armor_creation_titan_feet`, `acc_creation_titan_cloak`, `acc_creation_titan_necklace`, `acc_creation_titan_ring_1`, `acc_creation_titan_ring_2`, `acc_creation_titan_trinket`, `acc_creation_titan_badge`]
- 92 `void_devourer_set`: [`armor_void_devourer_head`, `armor_void_devourer_body`, `armor_void_devourer_hands`, `armor_void_devourer_feet`, `acc_void_devourer_cloak`, `acc_void_devourer_necklace`, `acc_void_devourer_ring_1`, `acc_void_devourer_ring_2`, `acc_void_devourer_trinket`, `acc_void_devourer_badge`]
- 93 `holy_arbiter_set`: [`armor_holy_arbiter_head`, `armor_holy_arbiter_body`, `armor_holy_arbiter_hands`, `armor_holy_arbiter_feet`, `acc_holy_arbiter_cloak`, `acc_holy_arbiter_necklace`, `acc_holy_arbiter_ring_1`, `acc_holy_arbiter_ring_2`, `acc_holy_arbiter_trinket`, `acc_holy_arbiter_badge`]
- 94 `death_herald_set`: [`armor_death_herald_head`, `armor_death_herald_body`, `armor_death_herald_hands`, `armor_death_herald_feet`, `acc_death_herald_cloak`, `acc_death_herald_necklace`, `acc_death_herald_ring_1`, `acc_death_herald_ring_2`, `acc_death_herald_trinket`, `acc_death_herald_badge`]
- 95 `star_weaver_set` **P0**: [`armor_star_weaver_head`, `armor_star_weaver_body`, `armor_star_weaver_hands`, `armor_star_weaver_feet`, `acc_star_weaver_cloak`, `acc_star_weaver_necklace`, `acc_star_weaver_ring_1`, `acc_star_weaver_ring_2`, `acc_star_weaver_trinket`, `acc_star_weaver_badge`]。前四个 ID 与 Set 11 重复，tag 也与 Set 11 重复。
- 96 `fate_weaver_set`: [`armor_fate_weaver_head`, `armor_fate_weaver_body`, `armor_fate_weaver_hands`, `armor_fate_weaver_feet`, `acc_fate_weaver_cloak`, `acc_fate_weaver_necklace`, `acc_fate_weaver_ring_1`, `acc_fate_weaver_ring_2`, `acc_fate_weaver_trinket`, `acc_fate_weaver_badge`]
- 97 `dream_lord_set`: [`armor_dream_lord_head`, `armor_dream_lord_body`, `armor_dream_lord_hands`, `armor_dream_lord_feet`, `acc_dream_lord_cloak`, `acc_dream_lord_necklace`, `acc_dream_lord_ring_1`, `acc_dream_lord_ring_2`, `acc_dream_lord_trinket`, `acc_dream_lord_badge`]
- 98 `time_traveler_set`: [`armor_time_traveler_head`, `armor_time_traveler_body`, `armor_time_traveler_hands`, `armor_time_traveler_feet`, `acc_time_traveler_cloak`, `acc_time_traveler_necklace`, `acc_time_traveler_ring_1`, `acc_time_traveler_ring_2`, `acc_time_traveler_trinket`, `acc_time_traveler_badge`]
- 99 `cosmic_devourer_set`: [`armor_cosmic_devourer_head`, `armor_cosmic_devourer_body`, `armor_cosmic_devourer_hands`, `armor_cosmic_devourer_feet`, `acc_cosmic_devourer_cloak`, `acc_cosmic_devourer_necklace`, `acc_cosmic_devourer_ring_1`, `acc_cosmic_devourer_ring_2`, `acc_cosmic_devourer_trinket`, `acc_cosmic_devourer_badge`]
- 100 `creation_divine_set`: [`armor_creation_divine_head`, `armor_creation_divine_body`, `armor_creation_divine_hands`, `armor_creation_divine_feet`, `acc_creation_divine_cloak`, `acc_creation_divine_necklace`, `acc_creation_divine_ring_1`, `acc_creation_divine_ring_2`, `acc_creation_divine_trinket`, `acc_creation_divine_badge`]
