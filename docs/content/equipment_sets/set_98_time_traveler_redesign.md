# Set 98 时间旅者：单件与套装重设计

> 状态：Content design / proposal。取代 `sets_91_to_100.md:735-830`、`sets_91_to_100_accessories.md:955-1085` 的单件特殊效果文本，与 `existing_set_bonus_redesign_68_100.md:565-578` 的套装奖励链配套。
>
> **冻结解除范围**：用户在 2026-08-12 定向授权本套解除 `existing_set_bonus_redesign.md:13-18` 的单件冻结，仅限 Set 98 的十件。`item_id`、槽位、`equipment_type_id`、tag、价格、单件 `attribute_modifiers` 数值仍然不动；本轮改的是**距离单位、时间单位、单件特殊效果规则与表现**。其余 99 套不受影响。

## 1. 本轮三项修改目标

1. 全部距离改用**地格**，全部时长改用 **TU**，全部行动资源改用 **AP**；D&D 残留记号（尺、回合、action/bonus action、法术位、借机攻击、concentration）一律不进入正式规则。
2. 98.3 手套的「老化 / 恢复物体」给出可判定规则与表现方案。
3. 98.7 加速之戒：功能加强为可选层数的 AP 借贷，代价从"aging 1 年（无游戏效果）"改为三层真实代价。

## 2. 单位换算与记号剥离

### 2.1 换算表

| 旧记号 | 正式值 | 依据 |
|---|---|---|
| 5 / 10 / 15 / 20 / 30 / 60 尺 | 1 / 2 / 3 / 4 / 6 / 12 格 | `existing_set_bonus_redesign_01_34.md:107` |
| 1 / 2 回合 | 30 / 60 TU | `AttributeService.DEFAULT_CHARACTER_ACTION_THRESHOLD = 30` |
| 1 分钟 | 300 TU | 同上，10 次默认 activation |
| `movement_speed +15` | `move_point_capacity_delta +3` | `existing_set_bonus_redesign_01_34.md:107` |
| `initiative_bonus +4` | `battle_start_progress +4`（**记号，非 owner**——见 §2.4） | `action_threshold` 语义 |
| 法术位 | MP | 本作无法术位 |

### 2.2 直接删除的记号

- **concentration**：98.5 披风原文"不需要 concentration"删除；本作 haste 类效果本就不占 concentration。
- **action / bonus action**：全部改写为明确 AP 成本；`0 AP` 只表示 reaction 时序。
- **98.1 条目内的"2件套 / 4件套（设计预留）"整段删除**：套装奖励层已由 `existing_set_bonus_redesign_68_100.md` 的 3/6/9/10 持有，单件文档不再重复持有阈值。

### 2.3 保留但尚未落地的记号：opportunity attack

`grep -i opportunity scripts` 当前零命中，本作**尚未**实现借机攻击。但该机制已确认为后续要加入的系统，因此本设计**保留**相关条目，按 planned owner 处理，不做删除也不做等价替换。

同一记号在既有设计文档中已被广泛使用（`existing_set_bonus_redesign_01_34.md` 9 处、`existing_set_bonus_redesign_35_67.md` 2 处，覆盖 Set 2 / 9 / 13 / 22 等），说明它是跨套装的共同前置，不是 Set 98 的局部问题。

**Set 98 对该 owner 的最低契约**（供未来实现时对齐）：

1. 免疫必须挂在**移动方**，表达为"本次移动不触发任何敌方 opportunity attack"的移动侧标记（如 `opportunity_attack_immune` 状态或移动请求上的 suppression flag）。
2. **不得**用给相邻敌人施加 `reaction_lock` 来实现——`reaction_lock` 作用在反应方且会连带封掉该敌人的其它反应，语义完全不同。
3. 免疫的作用域必须是"一次移动"或"一段明确 TU 窗口"，不是"一次攻击"或"一个回合"。
4. 在该 owner 落地前，98.4「时间滑行」的免疫部分保持 planned，其余部分（`move_point_capacity_delta`、穿过占用格）可以先行实现。

### 2.4 属性 id 实测：设计文档的"冻结属性"多数是空转的

2026-08-12 实测：`ItemContentRegistry` 对 `attribute_modifiers[].attribute_id` **只校验非空，没有白名单**（`ItemContentRegistry.cs:438`）。未知 id 会正常加载、正常存档、**什么都不做**——不是构建失败，是静默失效。`AttributeService.CanWriteCustomStat` 要求 key 预先存在于 `custom_stats`，所以它连自定义统计都建不起来。

真实合法的属性 id 只有：`hp_max`、`character_hp_max_percent_bonus`、`mp_max`、`stamina_max`、`stamina_recovery_percent_bonus`、`aura_max`、`action_points`、`action_threshold`、`armor_class`、`armor_ac_bonus`、`shield_ac_bonus`、`dodge_bonus`、`deflection_bonus`、`natural_armor_ac_bonus`、`armor_max_dex_bonus`、`attack_bonus`、`weapon_attack_range`、六个属性调整值、`base_attack_bonus`、`spell_proficiency_bonus`（`AttributeService.cs:37-68`）。参照：已落地的凤凰套十件只用了 4 个 id。

Set 98 十件的实测结果：

| 设计属性 | 状态 |
|---|---|
| `armor_ac_bonus` / `dodge_bonus` / `attack_bonus` | ✅ 合法，原样落地 |
| `max_mana +20` | ✅ 改写为 `mp_max +20` |
| `insight_bonus` / `history_bonus` / `arcana_bonus` / `sleight_of_hand_bonus` | ❌ 无 owner，属于 `check(...)` 缺口 |
| `initiative_bonus +4` | ❌ 无 owner；`battle_start_progress` 全仓零命中 |
| `movement_speed +15` | ❌ 属性层不存在；只能由装备能力包授予 `move_point_capacity_delta` |
| `spell_dc +1` | ❌ 无 owner |

**本轮不做数值替代。** 用 `action_threshold -5` 顶替 `initiative_bonus +4` 会把"一次性先攻加值"变成"永久 20% 行动速度"，是量级不同的静默增强，须另行授权。因此披风、项链、减速之戒、徽章四件当前落地为**零属性**，待 owner 补齐后回填。

这不是 Set 98 的局部问题：`battle_start_progress` 在三份重设计文档出现 6 次、属性审计 8 次；`check(...)` 出现 **58 次，覆盖 44 套**。补这两个 owner 的收益远大于继续推 `TIME`。

## 3. 可用 owner 盘点（本设计的地基）

| 能力 | owner | 状态 |
|---|---|---|
| `time_stasis` / `time_slow` | `BattleStatusSemanticTable:181-182`，`TemporalStatusContentRules` | 已落地；`time_stasis` 为 `CleanseProtected`，只能走 temporal release |
| `temporal` save tag | `BattleSaveTagKind.Temporal` | 已落地 |
| AP 增减 | `modify_action_points`，四模式：`add_base_action_points` / `subtract_current_action_points` / `restore_current_action_points_capped` / `set_next_turn_ap_to_zero` | 已落地，`sands_time_pack.tres` 已在用 |
| 装备耐久战斗内损毁 | `equipment_durability_damage` + `BattleEquipmentDurabilityResolver`；归零走 `ClearEntrySlot` | 已落地，含 rarity 过滤、slot 权重、AI intent、`equipment_durability_events` |
| 行动/吟唱进度改写 | `EquipmentTemporalProgressModifierDef`（save_dc + success/failure_rate_percent） | 已落地 |
| 真实衰老 | `PartyMemberState.age_years`、`natural_age_stage_id`、`AgeStageRule.AttributeModifiers`（经 `AttributeService` 生效） | 已落地 |
| 装备耐久修复 | 仅世界侧 `service_repair_gear` | 战斗内**无** owner，见 §4.2 |
| 状态回溯 checkpoint | 无 | `TIME` 仍是缺口，见 §7 |

## 4. 98.3 时间旅者手套：老化 / 恢复

保留 `armor_time_traveler_hands`、hands 槽、20,000、`AC+1 / sleight_of_hand+2`。旧文的"每日三次改变时间流速"与"bonus action 老化或恢复物体"合并为一条双向能力。

### 4.1 战斗内「时间之手」

1 AP，`per_battle = 2`，两个方向共用同一次数池 `shared_usage_key = time_hand_touch`。目标必须在 **1 格内**。

**老化（对敌）**：指定目标一件已装备物品，DC16 constitution save（`temporal` tag，DC 由 `save_dc_mode = caster_spell` 解析）。

- 失败：`equipment_durability_damage`，`durability_loss = 40`，`max_damaged_items = 1`（V1 硬限制），`max_target_rarity = 3`
- **传奇（rarity 4）免疫耐久损失**，改为 60 TU 内失效该物品的 AC component 或攻击加值（择一，按物品类型；临时状态，不写回耐久）
- 耐久归零：复用现有 `BattleEquipmentDurabilityResolver` 的 `ClearEntrySlot` —— 物品销毁并卸下，不新增销毁语义
- 成功：无效果，仍消耗次数

**恢复（对友）**：指定 1 格内友方一件已装备物品，`durability +40`，不超过 `GetMaxDurabilityForRarity(rarity)`。已销毁物品直接 NoOp（与 resolver 现有 `already_destroyed` 分支同语义），不复原。

### 4.2 需要新增的唯一战斗 action

现有 `equipment_durability_damage` 的 validator 要求 `durability_loss` 为正，无法表达修复。需新增镜像 action `equipment_durability_restore`：

```
payload: target_selector, durability_restore(>0), max_restored_items = 1, target_slots[], slot_weights[]
规则: after = min(before + restore, max_for_rarity); before <= 0 → NoOp("already_destroyed")
事件: 复用 equipment_durability_events，durability_loss 记为负值或新增 restore 字段
```

这是本设计**唯一**必须新增的战斗 action kind，payload 与现有损毁 action 同形，成本可控。在它落地前，98.3 的恢复方向保持 planned，老化方向可以先行。

### 4.3 世界内「岁月之触」

`per_world_month = 1`，在世界地图/队伍界面使用，二选一：

- **催老**：目标队友 `age_years += 5`，重算 `natural_age_stage_id`
- **回春**：目标队友 `age_years -= 5`，下限为该种族 `young_adult_age`，重算 `natural_age_stage_id`

属性后果**不新增通道**，完全由既有 `AgeStageRule.AttributeModifiers` 经 `AttributeService` 产生。跨越 `middle_age` / `old_age` / `venerable_age` 时必须弹出属性变更摘要；`age_years` 进入 `max_natural_age - 5` 区间时必须显式警告且要求二次确认。

物体方向：也可改为将仓库内一件耐久受损装备恢复至满，等价于免除一次 `service_repair_gear` 花费。这让"恢复物体"在战斗外有真实经济价值。

### 4.4 表现方案

| 场合 | 表现 |
|---|---|
| 老化命中 | 目标装备图标叠加锈蚀/裂纹遮罩（按材质分金属锈、皮革裂、木质朽三套贴图），durability 数值滚动下降，飘字「老化 −40」；驱动源是已有的 `equipment_durability_events`（含 `durability_before` / `durability_after`），不需要新事件流 |
| 装备销毁 | 遮罩推进至全灰，图标碎裂并从槽位滑出，战斗日志记一行「<物品> 在时间中朽坏」 |
| 传奇免疫 | 遮罩只覆盖一半并回弹，飘字「传奇之物抗拒时间」，同时挂上失效 component 的状态图标 |
| 恢复 | 同一遮罩反向褪去，飘字「恢复 +40」，图标短暂泛出时间金色描边 |
| 世界内催老/回春 | 角色年龄字段变化 + 沿用现有 stage 变更展示；跨阶段时立绘走既有年龄阶段素材 |
| 手套自身 | 指尖「时间针」常亮微光；触碰瞬间掌心「时间印」在目标格留下齿轮状余痕，60 TU 后淡出（纯表现，无判定） |

## 5. 98.7 加速之戒：时间借贷

保留 `acc_time_traveler_ring_1`、ring_1 槽、8,000、`attack_bonus +1`。

### 5.1 功能（加强）

主动「时间借贷」：**0 AP**，`per_battle = 1`（原为每日一次）。使用时选择借 1 层或 2 层：

| 借贷层数 | 立即收益 | 结算代价 |
|---:|---|---|
| 1 层 | `add_base_action_points` +1×基础 AP（默认 +2） | 下一次自身 activation 该回合 0 AP |
| 2 层 | `add_base_action_points` +2×基础 AP（默认 +4） | 接下来**两次**自身 activation 各 0 AP |

层数选择在使用时提交后冻结。收益走已落地的 `add_base_action_points`（`sands_time_pack.tres` 同款）。

### 5.2 代价（三层，全部有 owner）

**代价一 · 战斗内时间债**

施加 `temporal_debt` 状态，层数 = 借贷层数。每次自身 activation 开始时消费 1 层，该次 activation 执行 `set_next_turn_ap_to_zero`。

- `temporal_debt` 在 `BattleStatusSemanticTable` 中登记为 `Harmful = true`、**`CleanseProtected = true`**、`DispellableHarmful = false`
- 理由：与 `time_stasis` 同待遇。若允许普通净化清除，借贷就是白拿；只有正式 temporal release 能免债，且那本身是稀缺资源

**代价二 · 赖账封堵**

战斗结束时仍有剩余债务层数 → 写回持久 `temporal_debt_carried`（`PartyMemberState` 字段，进存档）。下一场战斗的前 N 次 activation 各 0 AP，N = 结转层数。

逃跑、撤退、战斗异常结束、读档均不清账。这一条堵死"借完就跑"的套利。

**代价三 · 真实衰老**

每次使用 `age_years += 2 × 借贷层数`（借 1 层 +2 年，借 2 层 +4 年）。

后果完全由既有 `AgeStageRule.AttributeModifiers` 产生：累积使用会真实推入 `middle_age` / `old_age` / `venerable`，吃到该阶段的属性修正，并向 `max_natural_age` 逼近。使用前若本次衰老会跨越阶段边界，必须弹出确认。

**"aging 1 年（外观变化，无游戏效果）"这句从此删除。**

### 5.3 三个时间尺度

短期借到 AP（一次爆发），中期赔回整个 activation（战斗节奏），长期赔上寿命与属性（角色生涯）。三者分别落在 `modify_action_points`、`temporal_debt` 状态、`age_years` 三个已存在的 owner 上，没有一个需要新建系统。这也贴合原文叙事："加速不是作弊——它只是利用了时间的缝隙"「但加速是有代价的——每次使用，佩戴者会老化」。

## 6. 其余八件的单位换算与规则收敛

| # | 名称 | 重设计后的单件特殊效果 |
|---|---|---|
| 98.1 | 头冠 | **时间之眼**（常驻）：显示 6 格内所有单位距下次 activation 的剩余 TU，以及正在吟唱技能的剩余 cast TU（owner：`action_threshold` + `BattlePendingCastState` / `BattleCastingTimeService`）；感知 6 格内时间异常。删除原「2件套/4件套（设计预留）」整段 |
| 98.2 | 长袍 | **时间领域**：1 AP，`per_world_day = 1`，以自身为心半径 3 格、300 TU：友方 `action_threshold −5`、敌方 `action_threshold +5`（尊重 `ACTION_THRESHOLD_GRANULARITY = 5`）。**时间回避**：每 30 TU 最多一次，针对穿戴者的命中攻击有 25% 转为 miss，`replacement_group = time_traveler_evade` |
| 98.3 | 手套 | 见 §4 |
| 98.4 | 便鞋 | `movement_speed 15` → `move_point_capacity_delta +3`。**时间步**：1 AP，`per_battle = 1`，传送至 12 格内任意合法格。**时间滑行**：1 AP，`per_battle = 1`，60 TU 内额外 `move_point_capacity_delta +6`，移动时可穿过被单位占据的格（终点仍须合法），且该窗口内的移动不触发任何敌方 opportunity attack（planned owner，见 §2.3） |
| 98.5 | 披风 | `initiative_bonus +4` → `battle_start_progress +4`。**时间扭曲**：1 AP，`per_world_day = 1`，二选一：3 格内敌人 DC18 constitution（`temporal`）失败施加 `time_slow` 60 TU；或 3 格内友方 `add_base_action_points` +基础 AP（本次 activation） |
| 98.6 | 项链 | **时间回溯（单件）**：0 AP reaction 时序，`per_world_day = 1`，`shared_usage_group = time_rewind_daily`。自身或 2 格内友方恢复至该单位上次 activation 开始时的 HP / MP / battle status，位置不变。依赖 `TIME` checkpoint，与套装 9 件同一实现切片 |
| 98.8 | 减速之戒 | **时间停滞**：1 AP，`per_world_day = 1`，3 格内敌人 DC18 constitution（`temporal`），失败施加 `time_stasis` 30 TU |
| 98.9 | 沙漏 | 1 AP，`per_world_day = 1`，四选一：自身 `add_base_action_points` +基础 AP；4 格内敌人 `time_slow` 60 TU（DC17 constitution）；**倒流**——回溯至 2 次 activation 前，共享 `time_rewind_daily`；3 格内敌人 `time_stasis` 30 TU（DC18 constitution）。"沙子有限"落成 UI：沙漏图标按当日剩余次数显示沙量 |
| 98.10 | 徽章 | 常驻光环：2 格内友方 `battle_start_progress +1`（战斗开始结算一次）。**时间领域**：1 AP，`per_world_day = 1`，半径 2 格、60 TU，友方 `attack_roll_bonus +1`、敌方 `attack_roll_bonus −1`，`replacement_group = time_domain_attack`（与 98.2 长袍领域同组，只取最高档） |

## 7. 与套装奖励链的接口

`existing_set_bonus_redesign_68_100.md:565-578` 的 3/6/9/10 阈值链**不改数值**，但新增三条接口约束：

1. **回溯不清偿时间债**。9/10 件的 checkpoint 恢复不移除 `temporal_debt`、不退还 `temporal_debt_carried`、不回滚 `age_years`。这是原则"usage、消耗品、装备耐久、行动进度和世界状态永不回滚"的直接延伸——现在该清单显式追加 `age_years` 与 `temporal_debt`。
2. **回溯不修复耐久**。被 98.3 老化损失的耐久、被销毁的装备均不随回溯恢复；装备状态本就在排除清单内。
3. **共享次数已定**：98.6 项链、98.9 沙漏倒流、套装 9/10 件回溯共用 `time_rewind_daily`，任一入口消耗后当日全部拒绝。

## 8. Owner 状态汇总

| 需求 | 状态 |
|---|---|
| `time_stasis` / `time_slow` / `temporal` save tag | 已落地，直接引用 |
| `add_base_action_points` / `set_next_turn_ap_to_zero` | 已落地，直接引用 |
| `equipment_durability_damage` + 销毁 | 已落地，直接引用 |
| `age_years` + `AgeStageRule.AttributeModifiers` | 已落地，需新增"世界侧主动改年龄"的调用入口与确认 UI |
| `equipment_durability_restore` action | **需新增**（§4.2），单一小切片 |
| `temporal_debt` 状态 + 语义表行 | **需新增**，一行 `BattleStatusDescriptor` |
| `temporal_debt_carried` 持久字段 | **需新增**，`PartyMemberState` 字段 + 存档（涉及 SaveVersion） |
| `action_threshold` 领域式增减 | 需确认是否已有区域来源；否则为新增 |
| opportunity attack + 移动侧免疫 | **仍是缺口**，已确认为后续要加入的系统；只阻塞 98.4 时间滑行的免疫部分（§2.3） |
| `TIME` checkpoint / 原子恢复 | **仍是缺口**，98.6 与套装 9/10 件共同阻塞 |

本轮把 Set 98 的缺口从"整条链依赖不存在的 `TIME` 子系统"收敛为两个点：**回溯类效果依赖 `TIME`**，**98.4 滑行的免疫部分依赖 opportunity attack**。后者是跨套装共同前置、已确认要加入系统，且只影响 98.4 的一个子句。十件中八件、四档阈值中的前两档现在都可以在已落地 owner 上实现。

## 9. 验收回归清单

1. 十件配置快照：`item_id` / 槽位 / tag / 价格 / `attribute_modifiers` 与冻结前逐字段一致（本轮只改描述与特殊效果）。
2. 98.3 老化：非传奇装备耐久扣减、归零销毁并卸下、传奇走 component 失效分支、save 成功仍消耗次数、老化与恢复共享 `time_hand_touch` 次数池。
3. 98.3 恢复：不超过 rarity 上限、`already_destroyed` NoOp、`per_battle` 与老化互斥消耗。
4. 98.7：借 1 层 / 2 层的 AP 收益与 `temporal_debt` 层数一致；`temporal_debt` 不可被普通净化清除；战斗结束结转 `temporal_debt_carried` 并在下一场生效；逃跑与读档不清账；`age_years` 真实增加并在跨阶段时产生属性变化。
5. 回溯不清债、不回滚耐久、不回滚 `age_years`；`time_rewind_daily` 三入口互斥。
6. preview / AI 读取上述效果时不消耗次数、不掷随机、不写回 `age_years` 或耐久。
7. 全部距离按格、时长按 TU 结算；资源文本中不得再出现"尺""回合""bonus action"。
8. opportunity attack 落地后补测：98.4 时间滑行窗口内的移动不被任何敌人借机攻击；窗口结束后恢复正常触发；免疫只作用于穿戴者本人的移动，不影响敌人对其他单位的反应。

## 10. 待决策

- **平衡**：98.4 时间步/滑行、98.7 借贷从 `per_world_day` 改为 `per_battle`，实际强度上升。十件总价仍为 167,000 未动，需按 §附录 B 第 8 条用正式获取等级与真实敌人曲线跑模拟后再定档。
- **`temporal_debt_carried` 与存档**：新增持久字段会触发 SaveVersion bump，按现行开发期策略是弃档；若此时已接近发布需先备迁移链。
- **世界侧改年龄的权限边界**：催老/回春作用于队友是否需要对方同意、能否对 NPC 使用、是否与任务时间线冲突，尚未决定。
- **`action_threshold` 区域增减**是否已有 owner 需实测确认；若无，98.2 与 98.10 的领域档要么等该 owner，要么退化为 `attack_roll_bonus` 型领域。
