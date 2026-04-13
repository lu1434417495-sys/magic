# Magic 可玩纵切路线图

更新日期：`2026-04-13`

## Summary

- 当前项目的核心循环已经通了，但“聚落服务”和“内容宽度”仍不足以支撑持续游玩。
- 这份路线图采用“可玩纵切优先”策略：先补足探索、补给、成长、掉落、再出发的闭环，再处理大规模资源迁移与表现打磨。
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

## 当前仓库事实

- 聚落服务配置已经存在于 `data/configs/world_map/*.tres`，`service_type`、`interaction_script_id`、设施与 NPC 绑定关系已经接通。
- `GameRuntimeFacade._on_settlement_action_requested()` 已经是聚落动作正式入口。
- `party_warehouse` 已经是现有唯一真正落地的聚落服务，说明“聚落按钮 -> runtime -> modal / 持久化”链路可复用。
- `CharacterManagementModule.record_achievement_event(...)` 已经支持据点行为成就。
- `PartyMemberState.current_hp/current_mp` 会持久化到世界态，因此“休整”可以做成真实恢复服务。
- `ItemContentRegistry` 已经采用资源扫描模式；敌人、职业、技能仍主要依赖硬编码注册。

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

- `仓储`
  - 继续沿用 `interaction_script_id = "party_warehouse"`。
  - 但逻辑入口收口到统一服务分发层，不再长期保留特判分叉。
- `休整`
  - 恢复全队 `current_hp/current_mp` 到属性上限。
  - 不做按床位、按时间、按伤势分级的复杂规则。
- `补给`
  - 提供基础购买。
  - 只卖首批正式物品，不做随机库存刷新。
- `锻造`
  - 支持固定配方：消耗材料，产生成品入仓。
  - 不做失败率、品质、词缀。
- `委托/任务`
  - 先做静态任务板。
  - 支持接取、完成、领奖。
- `研究`
  - 发放知识、技能或熟练度奖励。
  - 统一复用 `PendingCharacterReward` 队列，不新增第二套奖励弹窗。

### 第二批保留锁定态的服务

- `交易`
- `政务`
- `传送`
- `部署`
- `治理`

这些服务本轮不做真实系统，但 UI 必须显示为明确锁定态或未开放态，不能继续伪装成可成功办理的事务。

### Phase 1 完成标准

- 从聚落点击首批服务后，至少有一类真实状态变化发生：
  - 角色资源恢复
  - 金币变化
  - 仓库物品变化
  - 奖励队列变化
  - 任务状态变化
- 奖励和持久化都继续走现有 runtime 主链。
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
- `ItemDef` 需要扩展最小字段：
  - `item_type`
  - `equip_slot`
  - `buy_price`
  - `sell_price`
  - `attribute_modifiers`
  - `tags`

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
  - 新增 `gold: int`
  - 新增 `active_quests: Array`
  - 新增 `completed_quest_ids: Array[StringName]`
- `ItemDef`
  - 新增 `item_type`
  - 新增 `equip_slot`
  - 新增 `buy_price`
  - 新增 `sell_price`
  - 新增 `attribute_modifiers`
  - 新增 `tags`
- 新增 `QuestDef` 资源与任务状态对象。
- 聚落服务统一执行接口固定为：
  - `execute(settlement_id, action_id, payload) -> Dictionary`

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

- 本路线图的唯一优先级是“尽快形成一个可持续游玩的单机版本”。
- 表现层升级、多人、复杂系统设计都不作为当前是否开工的前置条件。
- 聚落仍采用窗口交付，不进入城内可行走地图。
- 本轮只要求 5 个真实服务落地，其余服务保持锁定态即可。
