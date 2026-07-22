# 任务发布体验扩展提案：双板 UI 与 NPC 动态存在

> 状态：**未实现提案**。本文档承载从 `docs/content/quests/quest_provider_assignment.md` 拆出的、当前代码尚未支持的设计。
> 现状事实以 `quest_provider_assignment.md` 与 `docs/design/world/world_map_module.md`、`docs/design/world/settlement_module.md` 为准。

---

## 一、悬赏板专属交互能力

### 1.1 批量勾选接取

- 悬赏板列表支持多选，一键批量接取。
- 现状：两个板共用 `ShopWindow` 复用窗口（`_open_contract_board_modal`），单条接取，无多选能力。
- 依赖：需要先拆分双板 UI（见 §二），或扩展 ShopWindow 支持多选模式。

### 1.2 危险度星级

- 悬赏条目展示 1-5 星危险度，从敌人模板推算。
- 现状：`QuestDef` 无危险度字段，window data 无星级，无任何推算逻辑。
- 待定：推算规则（按敌方模板等级/数量/精英标记加权）需要单独设计。

### 1.3 推导式上板过滤（备选方向）

原设计曾设想运行时推导上板归属：

```csharp
bool ShouldShowOnBountyBoard(QuestDef quest)
{
    return quest.is_repeatable
        && quest.tags.Contains("bounty")
        && quest.accept_requirements.Count == 0
        && quest.description.Length < 200
        && quest.accept_confirmation_text == "";
}
```

- 现状：代码采用**声明式**——任务数据显式声明 `provider_kind` + `listing_channels`，运行时严格匹配，不做推导。
- 若未来想减少数据声明负担，可引入推导作为**编写期检查工具**（validator 警告声明与推导不一致），不建议替换运行时声明式契约。

---

## 二、双板 UI 分离

- 现状：`service_contract_board` 与 `service_bounty_registry` 共用 `scenes/ui/shop_window.tscn`（`ContractBoardServiceModal`），仅 `_build_contract_board_entry` 在数据层按 interaction_id 分别过滤。
- 提案：悬赏板独立场景，列表式浏览（名称 + 星级 + 奖励），点击即接无确认；委托板保留详情面板式交互。
- 收益：两块板的交互差异（§一）有承载处；悬赏板"纯工具"定位在 UI 上成立。

---

## 三、NPC 实体动态存在机制

### 3.1 五种存在类型

| 存在类型 | 触发条件 | 实现状态 |
|---------|---------|---------|
| 常驻NPC | 据点/地图中固定位置 | ✅ 已实现（`FacilityNpcDefinition` + `WorldMapSpawnSystem` 生成期静态放置） |
| 条件出现NPC | 完成前置任务后出现 | ❌ 未实现 |
| 战斗后NPC | 特定战斗胜利后出现 | ❌ 未实现（战斗地图无任何 NPC 概念） |
| 隐藏NPC | 需要特定物品/条件才能看到 | ❌ 未实现 |
| 随机遭遇NPC | 世界地图移动时概率出现 | ❌ 未实现 |

条件类型设想：`quest_completed` / `quest_active` / `world_step_min` / `settlement_rank_min`。

示例：
- 帝国信使（完成Q101后出现）
- 濒死的龙裔（击败Q141区域敌人后出现在战场边缘）
- 只有持有 `dead_road_lantern` 才能看到的幽灵卓尔
- 迷路商人、逃兵（世界地图随机遭遇）

### 3.2 接取后 NPC 反应

- 接取任务后 NPC 可能消失、改变位置、或记住玩家的选择。
- 现状：NPC 实体与任务系统零耦合（`WorldMapSpawnSystem` 不读 PartyState），无任何"任务状态 → NPC 实体"的反向通道。

### 3.3 架构缺口

当前缺少一个 **NPC 实体生命周期服务**：读取 PartyState / 世界状态，决定 NPC 的可见性、位置与交互可用性，并在世界地图运行时动态增删 NPC 实体。这是本提案唯一需要新增模块的部分，落地前需要单独的设计文档（归属 `docs/design/world/` 评审后进入）。

---

## 四、Tag 语义消费

以下 tag 在 `quest_provider_assignment.md` §8.3 中定义但当前无任何消费方：

- `"delivery"` / `"escort"` — 委托板细分品类
- `"npc_quest"` — 标记 NPC 剧情任务（当前由 `provider_kind == "npc"` 显式表达，tag 冗余）
- `"hidden"` — 隐藏任务，配合 §3.1 隐藏 NPC 机制

现状：tags 仅作元数据，validator 不校验取值，过滤逻辑不读 tags。若要启用，需先定义消费方（validator 规则或运行时行为），再回填数据。

---

## 五、与原设计决策的关系

`quest_provider_assignment.md` §10 的五条关键设计决策中：

- 决策 1（悬赏板=纯工具）、2（剧情任务不上板）、4（委托板缓冲）：已由声明式 provider 模型落地，不依赖本提案。
- 决策 3（NPC 位置即叙事）、5（动态出现=世界感）：**依赖本提案 §三落地后才成立**。

---

## 六、Codex 检视意见（2026-07-19）

> 检视人：Codex  
> 检视性质：本节是 Codex 基于当前代码与内容配置给出的独立可行性检视意见，不属于原提案设计正文，不表示方案已经批准或实现。  
> 检视范围：任务板 UI、批量接取事务、危险度推算、NPC 运行时表示、状态触发、交互、存档与验证边界。

### 6.1 总体结论

本提案的产品方向成立，但当前文本尚不具备整体直接实施条件：

- §一、§二及 §四可以增量落地。其中双板 UI 分离可行性高，危险度星级与批量接取可行性中等至中高。
- §三把据点设施服务 NPC、物理世界 NPC、战斗后事件和随机遭遇合并为同一套生命周期，低估了数据定义、交互、运行时 mutation、索引同步和存档改造范围，必须先拆分设计。
- “只新增一个 NPC 实体生命周期服务”这一判断不成立。生命周期服务可以作为协调者，但不是唯一需要新增或修改的所有者。

因此，Codex 对本提案的总体判断是：**有条件可行；任务板部分可进入细化，NPC 动态存在部分需先补架构设计。**

### 6.2 分项判断

| 功能 | 可行性 | 检视意见 |
|---|---|---|
| 双板 UI 分离 | 高 | 需要独立场景、window data、modal kind 与 action contract，不只是更换显示样式。 |
| 危险度星级 | 中高 | 敌人模板已有等级、数量、精英 rank、派生生命与攻击等输入，但公式和无法推导时的 fallback 尚未定义。 |
| 批量接取 | 中 | UI 多选本身不难，主要风险是批量校验、原子提交、单次持久化和失败回滚。 |
| 推导式上板过滤 | 运行时低；validator 高 | 应保留 `provider_kind + listing_channels` 作为运行时真相；推导只能作为编写期弱提示。 |
| 据点服务 NPC 条件显隐 | 中高 | 可先在现有 `bound_service_npcs` / `available_services` 投影边界实现，不需要物理移动 NPC。 |
| 物理世界 NPC 动态增删、移动 | 可行但高风险 | 缺少 authored presence 定义、任务交互、正式 mutation API、坐标索引同步和独立生命周期状态。 |
| 战斗后 NPC | 世界地图方案中等；战斗场景方案低 | 推荐战斗胜利后在原遭遇的世界坐标生成 NPC 或世界事件；战斗地图内 NPC 应单独立项。 |
| 隐藏 NPC | 中 | 可见性、地图绘制、坐标点击索引和文本快照必须使用同一判定，不能只在 UI 层隐藏。 |
| 随机遭遇 NPC | 中低 | 需要确定性随机、稳定实例 ID、冷却/消费状态和存档，避免读档重掷与重复生成。 |
| Tag 消费 | validator/UI 高；行为真相低 | `hidden` 不应越权控制 NPC presence；`npc_quest` 与 `provider_kind == "npc"` 形成重复真相。 |

### 6.3 落地前必须修正的设计问题

1. **解决悬赏板主交互冲突。** §1.1 要求“多选后一键批量接取”，§二又要求“点击即接”。两者不能同时占用普通条目点击。应明确为“勾选框 + 接取所选”，或取消批量模式。
2. **定义批量接取事务语义。** 当前 `GameRuntimeQuestCommandHandler.CommandAcceptQuestTyped()` 每次只接受一个任务，并在状态变更后立即持久化。直接循环会出现部分成功与重复保存。建议基于同一 `PartyState` 快照整体校验，在副本上完成全部变更，只提交和持久化一次，失败时回滚。
3. **冻结危险度公式。** 至少定义多目标聚合、目标数量、空 `target_id`、非战斗目标、精英权重、1–5 星阈值和 fallback。建议由纯 `QuestDangerRatingResolver` 生成展示字段，并允许作者提供 override，不把派生星级写入存档。
4. **不要用启发式推导替代 provider 契约。** 当前已有合法悬赏并非全部可重复、也并非全部没有前置条件；按 §1.3 示例推导会误判现有内容。即使作为 validator，也应避免把该示例当成普适规则。
5. **区分两类 NPC 所有者。** `FacilityNpcDefinition` 表示设施内服务 NPC，具有服务与交互 ID，但没有物理世界坐标；顶层 `world_npcs` 具有坐标，但当前没有任务交互 ID。`local_slot_id` 只是实例标识组成部分，不是地图坐标。因此 §3.1 中“常驻 NPC 固定位置已实现”的表述只对静态服务绑定或通用地图标记部分成立，不代表可交互物理 NPC 已完整落地。
6. **补齐 NPC presence 数据与运行时边界。** 至少需要稳定 `presence_id`、placement kind、宿主据点/设施或世界坐标、interaction ID、类型化条件、transition、persistence policy，以及内容快照、validator、运行时 mutation、坐标索引刷新和交互桥。
7. **区分可重算状态与历史状态。** `quest_active`、世界步数和物品持有等条件可从当前状态重算；玩家选择、一次性消费、重定位、随机结果和特定战斗事实必须按稳定 `presence_id` 独立持久化，不能只保存在当前可见的 `world_npcs` 列表中。
8. **澄清任务完成语义。** 当前 `PartyState.HasCompletedQuest()` 表示奖励已领取；目标完成但未领奖的任务位于 claimable 状态。NPC 条件应显式区分 `quest_active`、`quest_claimable` 与 `quest_rewarded`，避免“完成任务后出现”产生歧义。
9. **修正据点条件命名。** 当前运行时只有静态 settlement tier，没有通用的 settlement rank 成长状态。若无新增成长设计，应把 `settlement_rank_min` 改为 `settlement_tier_min`。
10. **先确定真实悬赏板入口。** 当前任务板代码和测试支持 `service_bounty_registry`，但现有世界配置没有对应的实际服务绑定。双板 UI 落地前必须决定它挂在哪个现有设施/服务 NPC 上。若保持不新增设施的约束，应复用当前据点模板已经保证生成的设施，而不是新增设施、槽位或地图格。

### 6.4 建议拆分与实施顺序

1. **P0：修订提案契约。** 解决点击/多选冲突，冻结危险度公式、任务状态语义、NPC placement、条件集合和存档策略。
2. **P1：独立悬赏窗口与危险度展示。** 先保留单任务接取，并补上真实世界内容入口。
3. **P2：原子批量接取。** 新增正式批量命令，整体校验、单次持久化并覆盖失败回滚。
4. **P3：据点服务 NPC 条件可用性。** 仅实现设施服务 NPC 的确定性出现、隐藏和接取后消失，复用现有 NPC 任务交互。
5. **P4：物理世界 NPC 生命周期。** 增加 authored presence、交互、运行时增删/移动、索引同步和独立存档状态。
6. **P5：战后与随机 NPC。** 先实现战斗结束后的世界地图事件，再实现确定性随机遭遇；战斗场景内 NPC 另行设计。

### 6.5 建议验证门槛

实现时至少应覆盖以下回归：

- 危险度的多目标、空目标、精英权重、fallback 与星级截断；
- 批量接取的去重、整体校验、原子性、只持久化一次及失败回滚；
- NPC condition evaluator 的表驱动测试；
- 任务接受/达成/领奖、世界步、物品变化和战斗结果触发后的 presence 重算；
- NPC 出现、消失、移动后列表、坐标索引、绘制与点击结果一致；
- 消费、选择、移动和随机状态的存档往返与加载幂等性。

在上述契约冻结之前，不建议把 §三整体升级为已批准的 `docs/design/world/` 架构；可以先把任务板 UI 与据点服务 NPC 可用性作为独立、较小的交付面推进。

### 6.6 Codex 补充解决方案（检视意见）

> 本节继续属于 **Codex 的独立检视意见**，用于回答 §6.3 所列问题应如何解决；它不是已经批准或已经实现的项目设计。建议先评审本节契约，再把获批部分拆入正式设计文档和实施任务。

#### 6.6.1 先冻结状态所有权

后续实现应先固定以下“唯一真相”，避免 UI、存档和世界运行时各自维护一份状态：

| 状态 | 唯一 owner | 是否持久化 |
|---|---|---|
| 任务 provider、listing channel、危险度 override | `QuestDef` / `QuestDefinition` 内容定义 | 否 |
| 已接、可领奖、已领奖任务 | `PartyState` | 是 |
| 据点设施 NPC 的作者绑定 | `FacilityNpcConfig` / `FacilityNpcDefinition` | 内容数据，不进存档 |
| 据点服务 NPC 当前是否可见、可用 | `SettlementServiceAvailabilityResolver` 的派生结果 | 否 |
| 物理世界 NPC 的 authored presence | 新增 `WorldNpcPresenceDefinition` | 内容数据，不进存档 |
| 一次性消费、选择、重定位、随机结果、战后激活 | `NpcPresenceRuntimeState`，按稳定 `presence_id` 索引 | 是 |
| 当前动态世界 NPC actor | presence definition + runtime state 的运行时投影 | 不作为独立历史真相 |
| 悬赏板勾选项 | `BountyBoardWindow` 本地 UI 状态 | 否 |

`WorldMapSpawnSystem` 继续只基于内容和地图种子生成初始世界，不读取 `PartyState`。运行期条件由 resolver 读取只读上下文计算；UI 只能提交请求，不能直接改任务或 NPC 集合。

#### 6.6.2 独立悬赏板及交互契约

悬赏板不复用契约板的 modal/panel/submission 类型，新增完整类型链：

- `BountyBoardWindow.cs` 与 `bounty_board_window.tscn`；
- `BountyBoardWindowData`、`BountyBoardEntryData`；
- `RuntimeModalKind.BountyBoard`；
- `SettlementPanelKind.BountyBoard`；
- `SettlementSubmissionSource.BountyBoard`。

交互统一为：

1. 点击普通条目只聚焦并展示详情；
2. 只有可接任务显示勾选框；
3. 使用明确的“接取所选”按钮提交；
4. 锁定、进行中、可领奖、已完成条目不显示可用勾选框，并给出原因；
5. V1 不提供“点击整行立即接取”。如果以后需要快捷接取，应增加独立按钮，不能复用条目点击。

建议 DTO 至少包含：

```text
BountyBoardWindowData
- settlement_id
- action_id
- provider_interaction_id
- title
- feedback_text
- entries
- selected_quest_ids
- max_batch_size

BountyBoardEntryData
- quest_id
- display_name
- objective_summary
- reward_label
- danger_stars
- danger_source          # override / derived / unrated
- state_id               # available / locked / active / claimable / rewarded
- is_batch_eligible
- disabled_reason
- lock_reason_id
```

提交 payload 固定为：

```text
submission_source = "bounty_board"
operation = "accept_selected"
provider_interaction_id = "service_bounty_registry"
quest_ids = [ ... ]
```

窗口数据和命令校验都读取同一个纯投影/策略 owner，例如 `QuestBoardEntryProjectionService` 与 `QuestAcceptPolicyEvaluator`；不能让 UI 自己复刻前置条件或 provider 判断。

#### 6.6.3 原子批量接取

新增正式 typed contract：

```text
QuestBatchAcceptRequestData
QuestBatchAcceptResultData
GameRuntimeQuestCommandHandler.CommandAcceptQuestBatchTyped(...)
GameRuntimeFacade.CommandAcceptQuestBatchTyped(...)
```

现有单任务接取改为调用“一项的批量接取”，防止两套规则漂移。批量命令遵守以下合同：

1. `quest_ids` 数量限制为 1–32，拒绝空 ID 和重复 ID；
2. 所有 ID 必须属于当前打开的悬赏板上下文，并匹配 `service_bounty_registry`、provider 和 listing channel；
3. 所有资格与前置条件基于**同一份批前 `PartyState` 快照**判断，批内任务 A 不得临时解锁任务 B；
4. 任一任务校验失败时不执行任何 mutation，并返回逐项错误；
5. 全部校验通过后，在一个 `RuntimeTransaction` 中捕获 rollback state、`MarkPartyChanged()`、执行全部任务 mutation，只 `Commit()` / 持久化一次；
6. mutation 或持久化失败时恢复整份 Party/Session 状态，结果中的 accepted IDs 为空；
7. 成功后才关闭或刷新窗口，不允许 UI 先乐观移除条目。

应从当前单任务路径抽出“不自行保存”的底层 accept mutation，由批量命令协调事务；禁止简单循环调用当前会逐项保存的 `CommandAcceptQuestTyped()`。

#### 6.6.4 危险度星级

新增纯 `QuestDangerRatingResolver`，返回：

```text
QuestDangerRatingResult
- is_rated
- stars
- source                 # override / derived / unrated
- missing_target_ids
```

`QuestDef` / `QuestDefinition` 新增 `danger_tier_override`：`0` 表示自动计算，`1..5` 表示作者覆盖。派生值只用于投影，不写入 `PartyState` 或存档。

V1 只对可解析的 `defeat_enemy` 目标自动计算，建议采用：

```text
objective_threat =
    max(creature_level + 1, 1)
    * rank_weight
    * sqrt(max(target_value, 1))
    * sqrt(max(enemy_count, 1))

rank_weight:
    normal = 1.0
    elite  = 1.5
    boss   = 2.5

quest_threat = sum(objective_threat)
```

其中 `target_value` 表示整项任务要击败的总量，`enemy_count` 只用平方根表达同场压力，不能再线性相乘造成重复计数。V1 不额外叠加派生 HP/攻击力，避免与等级、rank 重复放大。1–5 星阈值和 rank 权重集中在 `QuestDangerRatingPolicy` 中，用当前内容做一次离线标定，UI 不得硬编码。

多目标按 threat 求和；没有 override 且目标为空、模板不存在或属于无法推导的非战斗目标时显示“未评级”，不能静默当作 1 星。validator 应拒绝 override 超出 `0..5`，并对上板但无法解析危险度的任务报错，要求补目标或显式 override。

#### 6.6.5 Provider、listing 与 tag

运行时真相继续是显式的 `provider_kind + listing_channels + interaction_id`：

- 不使用描述长度、是否可重复、是否存在前置任务等启发式自动决定上板；
- 启发式最多作为作者工具的 warning，不能修改内容或运行时归类；
- validator 校验 provider、listing 和真实服务入口一致；
- 悬赏板条目禁止携带逐项确认文本，批量操作只使用一次总确认；
- `delivery`、`escort` 等 tag 只作视觉分类；
- `hidden` 只影响任务日志展示，不能控制 NPC presence；
- `npc_quest` 与 `provider_kind == "npc"` 重复，标记为 deprecated，迁移完成后移除。

#### 6.6.6 据点服务 NPC 的条件显隐

这是第一阶段应交付的 NPC 能力，不需要物理世界 actor。给 `FacilityNpcConfig` / `FacilityNpcDefinition` 增加可选的：

```text
presence_id
availability_conditions[]
unavailable_behavior      # hidden / disabled
```

没有这些字段的旧 NPC 保持永久可用。V1 条件采用同一列表全部满足（AND），单个条件可 `negate`，只支持明确的类型：

```text
quest_active(quest_id)
quest_claimable(quest_id)
quest_rewarded(quest_id)
warehouse_item_count_min(item_id, quantity)
world_step_min(value)
settlement_tier_min(value)
settlement_is_player_start(expected)
```

这里的 `quest_rewarded` 对应当前 `PartyState.HasCompletedQuest()`；不再使用含义不清的 `quest_completed`，也不引入当前不存在的 `settlement_rank_min`。

新增纯 `SettlementServiceAvailabilityResolver`。打开据点窗口时，它对 authored `bound_service_npcs` 计算一次结果，并由同一结果同时构建 `service_npcs` 与 `available_services`。服务 action 提交时必须再次调用同一 resolver 校验，阻止过期窗口或伪造 payload 调用已经隐藏的服务。

这类显隐完全由当前任务、仓库、世界步和据点状态重算，因此不写存档、不增删 `world_npcs`。例如“接取后消失”应定义为 NPC 仅在目标任务既非 active、非 claimable、也非 rewarded 时可见；接取事务成功后重建据点投影即可。

#### 6.6.7 真实悬赏入口与 NPC 放置

在“不新增设施、设施槽位或地图格”的约束下，建议先落以下内容：

1. **主世界起始村庄：**复用模板保证存在的 `village_hearth`，在其 `bound_service_npcs` 中绑定一个新的悬赏登记员（建议 ID `npc_bounty_clerk`），交互使用 `service_bounty_registry`，本地实例 ID 可用 `hearth_bounty_registry`。再用 `settlement_is_player_start == true` 保证只在玩家起始据点出现。
2. **主世界城市扩展：**如需多城入口，再把同一服务绑定到现有 `guild_hall`；这不阻塞起始村庄版本。
3. **Ashen 内容：**若该内容也需要悬赏入口，复用模板实际保证生成的 `cathedral_choir`（较早阶段确有需要时可评估 `bonfire_sanctum`），不要使用虽然已定义但模板未选中的 `covenant_hall`。

`local_slot_id` 只是 NPC 实例标识的一部分，不是地图坐标。上述方案只能叙述为“NPC 在现有设施中提供服务”；如果设计要求 NPC 站在村口、移动或占据独立世界格，必须进入 §6.6.8 的物理世界 NPC 阶段。

#### 6.6.8 物理世界 NPC 的正式边界

物理世界 NPC 使用独立 authored 类型 `WorldNpcPresenceDefinition`，至少包含：

```text
presence_id               # 稳定内容 ID、存档键
npc_template_id
display_name
faction_id
placement_kind            # fixed_coord / near_settlement / captured_event_coord / near_player
placement_args
interaction_script_id
availability_conditions[]
persistence_policy        # derived / stateful / one_shot
transitions[]
```

内容快照和 validator 负责唯一 ID、引用、transition 目标和 placement 参数。运行时职责按组合拆分：

```text
WorldNpcPresenceResolver   # 纯计算 desired stage / presence
WorldNpcPlacementResolver  # 确定性解析合法坐标
NpcPresenceReconciler      # 比较 desired 与 actual，产出最小变更
WorldNpcInteractionRouter  # 按 interaction_script_id 路由交互
```

不要创建一个同时读取全局状态、掷随机、写存档、改地图和开 UI 的生命周期“神服务”。建议给 `WorldRuntimeData` / `WorldMapDataContext` 增加正式批量入口：

```text
ApplyWorldNpcMutationBatch(adds, removes, moves)
```

批量入口先验证 `presence_id`、`entity_id`、边界和坐标占用，全部合法后一次应用，再统一同步 typed payload 并重建 `_worldNpcByCoord`、`_worldNpcByEntityId`、`_worldNpcByPresenceId`。不得继续依赖字典后写覆盖坐标冲突。

`WorldMapNpcData` 至少补 `presence_id` 与 `interaction_script_id`。地图绘制、坐标点击、文本快照和交互路由全部读取同一 active actor 集合；隐藏必须从 actor 集合和坐标索引同时移除，不能只在 `WorldMapView` 中跳过绘制。

动态 actor 本身是运行时投影：建议把现有静态 `world_npcs` 与 transient dynamic actors 在只读 active view 中合并。存档保存 authored 之外的 lifecycle delta，不把动态 actor 列表再保存成第二份可编辑真相。

物理坐标采用确定性放置：排除据点 footprint、NPC、遭遇锚点、资源和事件占用，按固定邻格顺序或稳定的 `map_seed + presence_id` 哈希选择。首次成功坐标写入 runtime state；没有合法格时保存 `pending_placement` 和诊断原因，下次 reconcile 重试，不能生成重复 NPC。

#### 6.6.9 生命周期状态与存档兼容

新增世界所有的 typed `NpcPresenceRuntimeState`，不要写入 `PartyState.meta_flags`、`fate_run_flags` 或任意字符串字典。最小字段建议为：

```text
presence_id
state_id                  # default / activated / consumed / relocated / expired / pending_placement
resolved_map_id
resolved_coord
source_fact_id
choice_id
activated_at_step
expires_at_step
cooldown_until_step
last_roll_window
```

以下状态可重算，不保存：任务 active/claimable/rewarded、当前物品数、当前世界步、settlement tier、当前 visibility。以下历史必须保存：一次性消费、玩家选择、显式重定位、特定战斗激活、随机结果/坐标/冷却。

存档接入必须同时修改 `WorldRuntimeData.DuplicateState()`、plain snapshot、`FromDictionary()`、`SaveSerializer` 允许字段和 nested validation。当前存档使用严格版本 13：如果 v13 已经是兼容合同，应升级到 v14，并在严格版本拒绝之前显式迁移 `13 -> 14`，为旧档补空的 `npc_presence_states`；不能只改版本号。如果 v13 尚未发布，也至少把该字段定义为缺失时默认为空并增加回归。

加载顺序固定为：加载内容定义 → 迁移/读取 lifecycle state → 重建 transient dynamic actors → 重建坐标索引 → 显示地图。未知 `presence_id` 不投影并记录结构化诊断；同一状态重复加载和 reconcile 必须幂等。加载不得补掷随机或补算过去的世界步。

#### 6.6.10 重算触发与事务顺序

新增显式原因枚举，例如：

```text
NpcPresenceChangeKind
- QuestState
- Inventory
- WorldStep
- BattleOutcome
- InteractionChoice
- Load
```

禁止在 `_Process()` 中轮询。只在以下提交边界触发：任务接取/进度/领奖、仓库 mutation、世界步推进、战斗结算、NPC 选择提交，以及存档加载或地图绑定。

对于纯派生的据点显隐，事务成功后重建投影即可，不写世界状态。对于会产生历史的物理 NPC transition，统一流程为：

1. `RuntimeTransaction` 捕获 Party 与 World rollback state；
2. 执行业务 mutation；
3. 更新 `NpcPresenceRuntimeState`，并在事务内验证目标 actor mutation；
4. 标记变化并只持久化一次；
5. 持久化失败时同时恢复 Party、World、presence state 和 actor/index；
6. 成功后才刷新窗口和地图表现。

批量接取无论包含多少任务，都只在外层事务结束后重算一次。重算失败时保留上一份有效投影并记录错误，不能把所有 NPC 意外清空。

#### 6.6.11 战后 NPC、随机 NPC 与交互反应

**战后 NPC** 的语义固定为“胜利后在世界地图出现”，不在已经结束的战斗场景生成。接入战斗结算时：

1. 移除遭遇锚点前捕获 `encounter_id` 和原世界坐标；
2. 处理战利品和任务进度；
3. 胜利时在当前总事务中写入 presence activation，实例键使用稳定的 `presence_id + encounter_id`；
4. 原坐标被占用时按固定顺序寻找邻近格，仍失败则保存 `pending_placement`；
5. 在 `_materialize_active_world_state_to_root` / 最终持久化前完成状态写入；
6. 重复结算同一 encounter 必须幂等，写盘失败则随现有结算事务整体回滚。

**随机 NPC** 由独立 `WorldNpcEncounterDirector` 只在新世界步/时间窗口推进，不由通用 resolver 掷随机。建议规则字段为 `interval_steps`、`spawn_chance_permyriad`、`duration_steps`、`cooldown_steps` 和候选 placement；使用项目内固定哈希：

```text
window = world_step / interval_steps
roll = StableHash64(map_seed, presence_id, window) % 10000
instance_id = "random:" + presence_id + ":" + window
```

禁止使用全局 RNG、`.NET GetHashCode()` 或加载时重掷。“生成”和“不生成”的窗口结果都要记录；V1 每条规则最多一个活动实例，避免无界状态增长。

**交互反应** 使用 typed `NpcReactionActionKind`（如 `Hide`、`Consume`、`Relocate`、`RememberChoice`），由 lifecycle mutation 在事务内执行；UI 或交互脚本不能直接增删世界 NPC 列表。

#### 6.6.12 分阶段落地与验收

建议把 §6.4 进一步收敛为以下可验收切片：

| 阶段 | 交付结果 | 必须通过的关键门槛 |
|---|---|---|
| P1 | 独立悬赏 modal/window/snapshot，真实 `service_bounty_registry` 内容入口，单任务接取，危险度展示 | 两种任务板类型和提交源不混用；现有契约板回归不变；未评级与 override 正确 |
| P2 | 原子批量接取 | 批前快照校验、去重、顺序无关、只保存一次；任一失败和持久化失败均零部分成功 |
| P3 | 据点服务 NPC 条件显隐 | `service_npcs`/`available_services` 同源；提交二次校验；接取、claimable、领奖、仓库、世界步变化后结果一致 |
| P4 | 物理 actor、typed mutation、交互、lifecycle 存档 | 增删移动后列表/索引/绘制/点击一致；深复制、存档迁移、往返和重复 reconcile 幂等 |
| P5 | 战后及确定性随机 NPC | 胜负分支、重复结算、坐标冲突、相同 seed、加载不重掷及写盘失败回滚均有回归 |

每个阶段都应先运行 `dotnet build magic.csproj`，再执行该阶段的 focused headless regression。P3 完成即可交付“不新增设施”的教程/任务 NPC 需求；P4、P5 不应反向阻塞 P1–P3。
