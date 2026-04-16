# Magic 可玩纵切路线图

更新日期：`2026-04-16`

## Summary

- 当前项目的核心循环已经通了，但”聚落服务”和”内容宽度”仍不足以支撑持续游玩。
- 这份路线图采用”可玩纵切优先”策略：先补足探索、补给、成长、掉落、再出发的闭环，再处理大规模资源迁移与表现打磨。
- 实施顺序固定为：
  1. 聚落服务运行化
  2. 内容宽度补齐
  3. 装备、掉落、经济闭环
  4. 数据驱动迁移与遗留清理
- 本方案默认继续沿用当前主干：
  - `GameSession` 负责内容缓存与持久化
  - `GameRuntimeFacade` 负责世界态 / 战斗态 / modal 编排
  - `CharacterManagementModule` 负责角色成长与奖励入账
  - `PartyWarehouseService` 负责共享仓库

## 当前实施状态（2026-04-16）

### Phase 1 聚落服务

| 服务 | interaction_script_id | 状态 |
| --- | --- | --- |
| 仓储 | `party_warehouse` | ✅ 已落地 |
| 休整（篝火） | `service_rest_basic` | ✅ 已落地（免费，恢复 30% HP） |
| 休整（旅店整备） | `service_rest_full` | ✅ 已落地（50 金，全量恢复 HP/MP，推进世界时间） |
| 补给/商店 | `service_basic_supply` 等 | ✅ 已落地（SettlementShopService） |
| 驿站传送 | `service_stagecoach` 等 | ✅ 已落地（按格收费，已访问据点可选） |
| 乡野传闻 | `service_village_rumor` | ✅ 已落地（免费揭雾） |
| 情报网络 | `service_intel_network` | ✅ 已落地（50 金，大范围揭雾） |
| 锻造/配方合成 | — | ❌ 未实现（`RecipeDef` schema 已新增，但执行器不存在） |
| 委托/任务 | `service_contract_board` 等 | ⚠️ 部分接通（`QuestDef` / `QuestState` / `PartyState` 任务字段已新增，headless 可接取/推进/完成，battle/settlement 已能自动推进；正式任务板与奖励流程仍未实现） |
| 研究 | — | ❌ 未实现（无独立服务入口，奖励队列链路已具备） |

第二批服务（`service_repair_gear`、`service_join_guild`、`service_identify_relic` 等 17 项）仍在 `UNIMPLEMENTED_INTERACTION_IDS` 中，本轮不开工，保持锁定态。

### Phase 2 内容宽度

| 类别 | 当前 | 目标 |
| --- | --- | --- |
| 单手武器 | 1（bronze_sword） | ≥4 类 |
| 双手武器 | 1（iron_greatsword） | — |
| 防具 | 1（leather_jerkin） | 3 槽 |
| 饰品 | 1（scout_charm） | — |
| 消耗品 | 1（healing_herb） | ≥4 类 |
| 材料 | 1（iron_ore） | ≥6 类 |
| 任务物品 | 0 | ≥3 类 |
| 敌人模板 | 不足（未达 8 个） | 8 个，覆盖 4 类职责 |
| 战斗地形 profile | 2（default / canyon） | 补 “狭道突击” / “守点推进” |

`ItemDef` 字段缺口：`buy_price` / `sell_price` 目前只有 `base_price` 一个字段；`tags` 字段不存在。这两项需要在 Phase 2 补齐才能支撑商店差价、配方过滤和任务条件引用。

### Phase 3 装备/掉落/经济闭环

- ❌ 战斗结算后无掉落池、无物品入仓链路（`EncounterRosterBuilder` 没有 drop pool）。
- ❌ 仓库满时掉落无显式提示。
- ❌ 锻造配方消耗/产出未实现。
- ❌ 任务完成条件（提交物品、击败指定敌人、执行据点动作）未实现。

### Phase 4 数据驱动迁移

- ❌ 职业定义仍在 `ProgressionContentRegistry` 硬编码。
- ❌ 技能定义仍在 `DesignSkillCatalog` 硬编码。
- ❌ 敌人模板仍在 `EnemyContentRegistry` 硬编码。

### 战斗系统技术欠债

这些缺口不属于路线图四个 Phase 的内容宽度/服务任务，但会在 Phase 3 之前构成玩法质量瓶颈：

| 缺口 | 说明 |
| --- | --- |
| 命中模型 | 当前仍是 `hit_rate - evasion` 百分比口径；应切到 BAB + 负 AC + d20 双缩放体系 |
| 冷却/耐力循环 | `cooldown_tu` / `stamina_cost` 字段存在，但运行时扣费与回合推进闭环未完成 |
| Aura 独立资源 | 无独立 Aura 资源链，当前 aura_cost 字段悬空 |
| 状态效果语义表 | `status_effects` 是松散字典，无统一状态语义与持续时间管理 |
| 范围图形 | `line / cone / radius / self` 模式不完整 |
| AI 技能评分 | AI 选技能不走评分模型，仅走顺序优先 |

## 当前仓库事实

- 聚落服务配置已经存在于 `data/configs/world_map/*.tres`，`service_type`、`interaction_script_id`、设施与 NPC 绑定关系已经接通。
- `GameRuntimeSettlementCommandHandler` 已是聚落动作正式分发入口（原 `_execute_settlement_action` 占位已替换为真实服务分发）。
- 休整、商店、驿站、迷雾揭示均已落地真实状态变化；锻造、任务、研究三项仍无系统支撑。
- `CharacterManagementModule.record_achievement_event(...)` 已经支持据点行为成就。
- `PartyMemberState.current_hp/current_mp` 会持久化到世界态，休整已基于此实现真实恢复。
- `ItemContentRegistry` 已经采用资源扫描模式；敌人、职业、技能仍主要依赖硬编码注册。
- `PartyState` 已有 `gold`、`active_quests`、`completed_quest_ids` 字段，但任务流程仍未接入。
- `QuestDef` / `QuestState` / `RecipeDef` schema 已存在；其中 `QuestDef` 已接入 registry，settlement / battle 已能产出最小 quest progress 事件，但完整玩法链仍未闭环。

## 目标与非目标

### 目标

- 让玩家在当前世界地图中获得完整的“据点服务价值”。
- 让战斗产出、聚落消耗、角色成长、仓库管理形成稳定闭环。
- 把内容扩展成本从“继续硬编码”切到“可配置扩张”。

### 非目标

- 本轮不做多人 / 联机。
- 本轮不做完整城内地图。
- 本轮不做音频、特效、角色动画主导的表现升级。
- 本轮不做复杂经济、随机商店、装备词缀、状态异常、组合技、动态难度缩放。

## Phase 1：聚落服务运行化

### 实施目标

- 把当前 `_execute_settlement_action(...)` 的占位成功文案替换成正式服务分发。
- 保持所有聚落动作继续走 `GameRuntimeFacade` 主链，不在 UI 层直接写业务。

### 设计结论

- 新增统一的聚落服务执行层，建议放在 `scripts/systems/`，由 `GameRuntimeFacade._on_settlement_action_requested()` 调用。
- 所有服务返回统一结果结构：
  - `success: bool`
  - `message: String`
  - `state_changes: Dictionary`
  - `pending_character_rewards: Array`
  - `inventory_changes: Array`
- `settlement_action_completed` 的 `subject_id` 固定使用 `action_id`，不改成 `service_type`，以保持现有成就和 headless 回归稳定。

### 首批必须落地的服务

- `仓储` ✅ 已落地
  - 沿用 `interaction_script_id = "party_warehouse"`，逻辑入口已收口到 `GameRuntimeSettlementCommandHandler`。
- `休整` ✅ 已落地
  - `service_rest_basic`：免费，恢复全队约 30% HP。
  - `service_rest_full`：消耗 50 金，全量恢复全队 HP/MP，推进世界时间 1 步。
- `补给` ✅ 已落地
  - `SettlementShopService` 已实现商店买卖；库存来自物品注册表。
  - 驿站传送（`service_stagecoach`）同期落地。
- `锻造` ❌ 待实现
  - `RecipeDef` 不存在；`service_repair_gear` 在 `UNIMPLEMENTED_INTERACTION_IDS`。
  - 落地需求：新增配方资源 `RecipeDef`，实现"消耗材料 -> 产出成品入仓"执行器，挂接到 `GameRuntimeSettlementCommandHandler`。
- `委托/任务` ❌ 待实现（Phase 1 最大阻塞项）
  - `QuestDef` 资源不存在；`PartyState` 中没有 `active_quests` / `completed_quest_ids`。
  - `service_contract_board` / `service_bounty_registry` 在 `UNIMPLEMENTED_INTERACTION_IDS`。
  - 落地需求：新增 `QuestDef` 资源 + `PartyState` 任务字段 + 静态任务板 modal + 接取/完成/领奖链路。
- `研究` ❌ 待实现
  - 无独立研务入口；`PendingCharacterReward` 奖励队列链路已具备，研究服务只需新增 `interaction_script_id` 和执行器。
  - 落地需求：分配 `interaction_script_id`，在执行器中构造技能/知识奖励并推入 `pending_character_rewards`。

### 第二批保留锁定态的服务

以下 17 项在 `UNIMPLEMENTED_INTERACTION_IDS` 中，本轮不做真实系统，UI 保持锁定态（不能伪装成可成功办理）：

`service_repair_gear`、`service_contract_board`、`service_join_guild`、`service_identify_relic`、`service_bounty_registry`、`service_recruit_specialist`、`service_issue_regional_edict`、`service_unlock_archive`、`service_diplomatic_clearance`、`service_amnesty_review`、`service_elite_recruitment`、`service_master_reforge`、`service_respecialize_build`、`service_manage_reputation`、`service_open_trade_route`、`service_legend_contracts`、`service_hire_expert`

### Phase 1 完成标准

- 锻造、任务、研究三项服务均已落地真实状态变化。
- 任务系统：接取 → 推进 → 完成 → 领奖闭环在 headless 文本回归中可验证。
- 奖励和持久化继续走现有 runtime 主链（不新增奖励弹窗）。
- 现有 `settlement action service:warehouse` 文本回归仍然通过。

## Phase 2：内容宽度补齐

### 实施目标

- 先补“足够支撑聚落与战斗闭环”的内容量，而不是一次性铺满全部内容目录。

### 物品内容目标

- 正式物品至少补到以下最小集合：
  - 单手武器 4 类
  - 防具 3 槽
  - 消耗品 4 类
  - 材料 6 类
  - 任务物品 3 类
- `ItemDef` 字段现状与待补项：
  - `item_category`（misc / equipment / skill_book）✅ 已有
  - `equipment_slot_ids` ✅ 已有
  - `attribute_modifiers` ✅ 已有
  - `base_price` ✅ 已有（商店当前用此字段生成买卖价）
  - `buy_price` / `sell_price` ❌ 待拆分（当前商店差价依赖 `base_price` 推算，需要独立字段才能支撑配方引用和任务条件）
  - `tags` ❌ 待添加（配方过滤、任务条件、掉落池筛选均需此字段）

### 敌人与遭遇目标

- 敌人模板至少补到 8 个，覆盖 4 类职责：
  - 近战冲锋
  - 前排承伤
  - 远程压制
  - 治疗/控制
- 本阶段继续沿用现有 AI brain 结构，不引入新 AI 框架。
- 世界遭遇规则固定使用“据点 tier -> 敌人池 -> 掉落池”的静态映射，不做动态难度。

### 战斗场景目标

- 不做大量手工关卡。
- 先补 2 个具有明确玩法目的的地形 profile：
  - 狭道突击
  - 守点推进
- 继续通过现有地形生成器生成地图，并挂接不同 prop 组合与敌人 roster。

### Phase 2 完成标准

- 世界探索中能稳定遇到不止两类敌人。
- 商店、锻造、掉落、任务都能引用到同一批正式物品定义。
- 至少两种战斗 profile 在视觉和站位意图上可区分。

## Phase 3：装备、掉落、经济闭环

### 实施目标

- 把 `equipment_state` 从占位字典变成正式可用系统。
- 让战斗产出、聚落消费、人物成长进入同一资源循环。

### 设计结论

- 装备槽位固定为：
  - `main_hand`
  - `off_hand`
  - `head`
  - `body`
  - `accessory`
- 装备效果只支持基础属性修正，不做词缀、套装、耐久。
- 货币本轮固定为单一 `gold`，放在 `PartyState` 持久化。

### 必须落地的闭环

- 战斗结算支持掉落入仓。
- 仓库空间不足时，掉落不能静默丢失，必须在结算结果中显式提示未装下内容。
- `补给` 支持购买。
- `锻造` 支持材料消耗和成品产出。
- `任务` 支持以下完成条件：
  - 提交指定物品
  - 击败指定敌人模板
  - 执行指定据点动作

### 奖励类型边界

- 任务奖励只支持：
  - 金币
  - 物品
  - 知识 / 技能
  - 熟练度 / 属性奖励
- 所有成长型奖励继续走 `PendingCharacterReward`，不新增专用奖励容器。

### Phase 3 完成标准

- 玩家可以通过战斗获得材料。
- 可以在据点购买 / 制作 / 装备物品。
- 装备属性会影响角色快照，并进入后续战斗。
- 至少存在一条完整链路：
  - 战斗掉落材料 -> 锻造装备 -> 装备提升 -> 再次战斗更强。

## Phase 4：数据驱动迁移与遗留清理

### 实施目标

- 将未来最常迭代的内容从硬编码注册迁移到资源文件。
- 清理当前已脱离主运行链的旧实现和目录悬空状态。

### 迁移顺序

1. `professions` 迁到 `data/configs/professions/`
2. `skills` 迁到 `data/configs/skills/`
3. `enemy templates / ai brains` 迁到新的敌人配置目录

### 迁移规则

- registry 保留“扫描、校验、索引”职责。
- 具体内容定义不再留在 `ProgressionContentRegistry` 与 `EnemyContentRegistry` 中硬编码。
- 迁移期间允许保留兼容桥接层，但最终真相源必须只有资源文件一份。

### 遗留清理目标

- `battle_map_view.gd` 明确标记为 legacy，并在确认无引用后删除。
- `assets/sprites/`、`assets/fonts/`、`scenes/enemies/`、`scenes/levels/` 补正式内容落点。
- Prop 相关保持当前 `battle_board_prop` 方案，只补 canyon 正式资源与排序契约，不做单独的大迁移项目。

### Phase 4 完成标准

- 职业、技能、敌人三类内容都能通过配置扩容。
- 至少有一套资源校验流程覆盖缺失 id、重复 id、非法引用。
- 旧渲染遗留从正式运行链中彻底退出。

## Public Interfaces

- `PartyState`
  - `gold: int` ✅ 已有
  - `active_quests: Array` ✅ 已有（任务流程仍待接入）
  - `completed_quest_ids: Array[StringName]` ✅ 已有（任务流程仍待接入）
- `ItemDef`
  - `item_category`（替代原设计的 `item_type`）✅ 已有
  - `equipment_slot_ids` / `occupied_slot_ids` ✅ 已有
  - `attribute_modifiers` ✅ 已有
  - `base_price` ✅ 已有
  - `buy_price` / `sell_price` ✅ 已有
  - `tags` ✅ 已有
- `RecipeDef` 资源（锻造配方）✅ schema 已新增
- `QuestDef` 资源与任务状态对象 ✅ schema 与 seed 内容已新增
- 聚落服务统一执行接口：`execute(settlement_id, action_id, payload) -> Dictionary` ✅ 已通过 `GameRuntimeSettlementCommandHandler.execute_settlement_action()` 落地

## Test Plan

### 文本 / headless 回归

- `休整` 能恢复受伤角色的 `current_hp/current_mp`
- `补给` 购买后金币减少、物品入仓
- `锻造` 消耗材料并产生成品
- `任务` 能接取、完成、领奖
- `研究` 能排入奖励队列，并在 modal 关闭后展示

### 仓库回归

- 掉落入仓与容量不足提示
- 商店购买入仓
- 锻造成品入仓
- 装备穿脱与仓库联动

### progression 回归

- 装备属性进入角色属性快照
- 研究解锁技能 / 知识后成就推进仍正常
- 据点服务动作继续触发 `settlement_action_completed`

### battle/runtime 回归

- 新敌人模板可进入正式战斗
- 新掉落池与战斗结算一致
- 新战斗 profile 能通过现有 battle smoke / board 契约

### 资源校验

- 缺失 id
- 重复 id
- 非法 `equip_slot`
- 非法配方引用
- 非法任务目标引用

## 执行顺序约束

- 必须先做 Phase 1，再做 Phase 3；不能在没有真实聚落服务的情况下直接做经济闭环。
- Phase 2 可以在 Phase 1 后并行推进，但正式接入 runtime 只能以 Phase 1 的服务需求为准。
- Phase 4 不得提前插队成主线，否则会拖慢可玩性验证。

## Assumptions

- 本路线图的唯一优先级是”尽快形成一个可持续游玩的单机版本”。
- 表现层升级、多人、复杂系统设计都不作为当前是否开工的前置条件。
- 聚落仍采用窗口交付，不进入城内可行走地图。
- Phase 1 首批服务已从原计划的 5 项扩展为 6 项（仓储 + 休整 + 补给 + 锻造 + 任务 + 研究），其余 17 项保持锁定态。
- 战斗命中模型切换（BAB + 负 AC + d20）不强制纳入任一 Phase，但应在 Phase 3 前完成，否则经济闭环数值无法稳定校准。
- 信仰系统（`faith_system_plan.md`）、装备耐久（`equipment_durability_plan.md`）、D&D 武器系统（`dnd_weapon_system_initial_plan.md`）均为计划内但本轮不开工的系统，不影响四个 Phase 的执行顺序。
