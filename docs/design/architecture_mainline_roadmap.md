# Magic 架构主线路线图

更新日期：`2026-04-16`

## Summary

- 当前阶段不以“尽快形成可游玩纵切”为第一目标，而以“先稳定主干架构、共享 contract、内容扩展方式”为第一目标。
- 本文定义当前项目的**主执行顺序**；[`playable_vertical_slice_roadmap.md`](./playable_vertical_slice_roadmap.md) 退为下游 feature backlog，用于架构主线稳定后的整体验证。
- 本路线图默认继续沿用当前正式 owner：
  - `GameSession` 负责持久化、存档版本和内容缓存。
  - `GameRuntimeFacade` 负责 world / battle / modal 编排。
  - `BattleSessionFacade` 负责 battle session 读写与战后回写桥接。
  - `GameRuntimeSettlementCommandHandler` 负责聚落动作执行与结果归并。
  - `CharacterManagementModule` 负责角色成长、成就与待领奖励队列。
  - `PartyWarehouseService / PartyEquipmentService` 负责共享仓库与装备基础流转。

## Problem

- 当前仓库的主运行链已经从单一大脚本拆成多层 bridge + leaf service，但共享 contract 仍未完全定型。
- 多个未来必做系统仍缺正式 schema：
  - `QuestDef / QuestState`
  - `RecipeDef`
  - battle 结算结果与掉落 contract
  - 物品价格 / 标签 / 配方过滤 schema
  - 内容资源化后的 registry 校验边界
- 如果在 schema 未稳定前优先堆玩法纵切，后续最容易返工的不是 UI，而是：
  - `PartyState` 升级
  - save/load 兼容
  - headless snapshot / text command surface
  - battle -> settlement -> reward -> persistence 的跨域链路

## Current Ownership

### 当前正式 owner

- `GameSession`
  - save/load、save version、world/party 持久化、内容注册表缓存。
- `GameRuntimeFacade`
  - 世界移动、battle/world 切换、modal 真相源、统一持久化桥接。
- `BattleSessionFacade`
  - battle start / tick / resolve / end 的 session 控制面。
- `GameRuntimeSettlementCommandHandler`
  - 据点 action payload、service dispatch、商店 / 驿站 modal 分流、奖励归并。
- `GameRuntimeSnapshotBuilder`
  - headless snapshot schema 组织。
- `CharacterManagementModule`
  - progression、achievement、pending character reward 队列。
- `PartyWarehouseService`
  - 容量、堆叠、实例装备入仓与回仓。
- `PartyEquipmentService`
  - 装备 / 卸装、装备资格校验、角色属性快照联动。
- `BattleRuntimeModule`
  - battle phase、command dispatch、event batch、post-battle reward 产出。
- `BattleChargeResolver / BattleTerrainEffectSystem / BattleRatingSystem / BattleUnitFactory`
  - 已是 battle runtime 下的正式 sidecar，不应再回塞进单一 battle 大模块。

### 当前明确缺失的正式 schema

- `PartyState`
  - 已有 `gold`、`pending_character_rewards`、`warehouse_state`、`active_quests`、`completed_quest_ids`。
  - 任务字段已入 schema，但尚未接到正式任务流程、save/snapshot 收口与 headless 指令域。
- `ItemDef`
  - 已有 `base_price`、独立配置字段 `buy_price` / `sell_price`，以及运行时 `get_buy_price()/get_sell_price()`。
  - 已有 `tags` / `crafting_groups` / `quest_groups` schema。
- battle resolution
  - 当前正式产出统一为 `pending_character_rewards`。
  - 尚无正式 `loot / overflow / encounter_result` contract。
- content registry
  - `ItemContentRegistry` 已扫描资源目录。
  - profession / enemy / 部分 skill 仍依赖硬编码注册或 spec-provider。
 - `QuestDef / QuestState / RecipeDef`
  - `QuestDef` schema 已接入 registry，`QuestState` 已接入 `PartyState` / save / headless。
  - settlement / battle 已有最小 quest progress 事件主链，但仍未接入正式任务板、奖励结算与完整玩法闭环。

## Goals

- 先把“谁拥有什么状态、通过什么 contract 交接”稳定下来。
- 把未来高频迭代内容迁到资源或声明式 spec，使 registry 回到“扫描 / 校验 / 索引”职责。
- 让 battle、settlement、warehouse、progression、headless 在同一套 schema 上工作。
- 把后续纵切实现变成“填充稳定架构”的工作，而不是“边做玩法边反向定义架构”。

## Non-Goals

- 本文不以短期“可玩性反馈”作为首要排序依据。
- 本文不要求当前阶段完成完整任务闭环、经济闭环或长时游玩 loop。
- 本文不优先处理表现升级、音频、动画、复杂数值平衡。

## Options

### 方案 A：继续以 playable vertical slice 为主线

- 优点：
  - 反馈快，容易看到 loop 成型。
- 主要失败模式：
  - 在 quest / loot / forge / reward / save 之间提前固化临时 contract。
  - `PartyState` 和 headless snapshot 容易多次返工。

### 方案 B：可玩纵切与架构主线并行推进

- 优点：
  - 有中短期反馈，同时不完全停下架构。
- 主要失败模式：
  - 一旦 feature 优先级高于 contract，架构工作会退化为被动补洞。

### 方案 C：先做架构主线，再做纵切实现

- 优点：
  - 最符合当前初期开发目标。
  - 最能降低 schema、save、headless、registry 的反复返工成本。
- 主要失败模式：
  - 早期缺少显性玩法反馈，要求团队接受“先打地基”的节奏。

## Recommended Design

- 选择 **方案 C**。
- 现阶段的主执行顺序改为：
  1. owner 固化与边界冻结
  2. 共享 contract / schema 定型
  3. 内容资源化与校验管线
  4. 核心运行时管线补全
  5. 系统实现
  6. 纵切验证
- 执行方式采用“gate + parallel tracks”，而不是把所有 phase 当成单线程串行任务。
- [`playable_vertical_slice_roadmap.md`](./playable_vertical_slice_roadmap.md) 的定位调整为：
  - 架构主线稳定后的 feature 接入顺序参考
  - 下游验证目标
  - 不是当前第一优先级的排期依据

## Parallel Execution Model

### 调度原则

- `Phase 0` 仍然是唯一必须先完成的冻结门；之后不再按单线程 phase 执行，而是按“共享 gate + 多泳道任务包”推进。
- 每个任务包都必须明确：
  - state lives where
  - logic lives where
  - snapshot comes from where
  - save/load compatibility lives where
- 真正需要串行的只有共享 choke point；其余内容优先拆到 leaf service、content registry、设计文档或测试入口并行落地。

### 共享写锁面

- 以下表面不适合多人同时改：
  - `scripts/player/progression/party_state.gd`
  - `scripts/systems/save_serializer.gd`
  - `scripts/systems/game_runtime_snapshot_builder.gd`
  - `scripts/systems/game_runtime_facade.gd` 的正式对外 contract
- 任务拆分时，优先让并行任务围绕 leaf owner 展开；涉及这些共享表面的任务，必须先冻结 contract 再串行合并。

### Gate 定义

- `Gate 0`
  - owner freeze 完成，owner / sidecar / compatibility layer 文档口径冻结。
- `Gate 1`
  - canonical contracts 冻结，`SettlementServiceResult`、`BattleResolutionResult`、`QuestDef / QuestState`、`RecipeDef`、`ItemDef` 扩展字段有正式命名和兼容策略。
- `Gate 2`
  - state model stabilization 完成，`PartyState`、`WarehouseState`、战后提交 state、聚落 runtime state 有稳定升级口径。
- `Gate 3`
  - runtime pipeline completion 完成，settlement / battle / warehouse / progression / headless 都已接到正式 contract。
- `Gate 4`
  - system implementation 主链完成，可以做纵切验证与 economy tuning。

### 并行任务包

| ID | 任务包 | 主要 owner / 文件范围 | 前置 gate / 依赖 | 并行说明 |
| --- | --- | --- | --- | --- |
| `G0` | Owner freeze 文档包 | `docs/design/architecture_mainline_roadmap.md`、`docs/design/project_context_units.md` | 无 | 单线程完成；完成后解锁全部后续任务。 |
| `C1` | Settlement contract 包 | CU-06 / CU-12；`GameRuntimeSettlementCommandHandler`、`CharacterManagementModule`、建议新增 `docs/design/core_runtime_contracts.md` | `G0` | 可与 `C2`、`C3` 并行；避免直接改 `PartyState`。 |
| `C2` | Battle resolution contract 包 | CU-15 / CU-20；`BattleSessionFacade`、`BattleRuntimeModule`、`EnemyContentRegistry`、建议新增 `docs/design/battle_resolution_contract.md` | `G0` | 可与 `C1`、`C3` 并行；不要提前落正式 loot 提交逻辑。 |
| `C3` | Item / recipe schema 包 | CU-10 / CU-13；`item_def.gd`、`PartyWarehouseService`、建议新增 `RecipeDef` 文档或资源 schema 文档 | `G0` | 可与 `C1`、`C2` 并行；避免修改 battle / quest 主链。 |
| `C4` | Quest schema 包 | CU-11 / CU-12；`party_state.gd`、`GameSession`、`SaveSerializer` 的 quest 字段设计 | `G0`，建议在 `C1`、`C2` 的事件字段命名初稿明确后冻结 | 与 `C1`、`C2` 设计联动，但实现上尽量只落 schema，不落完整 quest 流程。 |
| `C5` | Save / snapshot compatibility 包 | CU-02 / CU-21；`save_serializer.gd`、`game_runtime_snapshot_builder.gd`、`game_text_snapshot_renderer.gd` | `C1`~`C4` 字段命名冻结 | 这是共享 choke point 协调包，不建议与任何 `party_state` 升级任务同时写。 |
| `S1` | Party / quest state 稳定包 | CU-02 / CU-11；`party_state.gd`、`progression_serialization.gd`、`game_session.gd` | `C4`、`C5` | 与 `S2`、`S3`、`S4` 可并行，但独占 `party_state.gd`。 |
| `S2` | Warehouse / equipment state 稳定包 | CU-10 / CU-11；`warehouse_state.gd`、`equipment_state.gd`、`party_warehouse_service.gd`、`party_equipment_service.gd` | `C3` | 可与 `S1`、`S3`、`S4` 并行；不要改 quest/save 版本面。 |
| `S3` | Settlement runtime state 稳定包 | CU-06 / CU-08；`GameRuntimeFacade`、`GameRuntimeSettlementCommandHandler`、据点 modal runtime state | `C1` | 可与 `S2`、`S4` 并行；仅通过显式 helper 扩 runtime surface。 |
| `S4` | Battle resolution staging / commit state 包 | CU-06 / CU-15；`BattleSessionFacade`、`BattleRuntimeModule`、`GameRuntimeFacade` battle 回写边界 | `C2`、`C5` | 可与 `S2`、`S3` 并行；不要与 `C5` 同时改 snapshot surface。 |
| `R1` | Profession / skill 资源化包 | CU-13 / CU-14；`progression_content_registry.gd`、profession/skill spec provider | `Gate 1` | 与 `R2`、`R3` 并行；尽量不触 runtime 主链。 |
| `R2` | Enemy / encounter 资源化包 | CU-17 / CU-20；`enemy_content_registry.gd`、`encounter_roster_builder.gd` | `C2` | 可与 `R1`、`R3` 并行；避免同时改 battle resolution 提交逻辑。 |
| `R3` | Recipe 资源化包 | CU-10 / CU-13；`RecipeDef` 资源、registry 校验入口 | `C3`、`S2` | 可与 `R1`、`R2` 并行；只接 item / warehouse schema。 |
| `R4` | Quest 资源化包 | CU-11 / CU-13；`QuestDef`、registry 校验、quest seed 内容 | `C4`、`S1` | 尽量在 quest state 版本策略稳定后再开工；与 `R3` 可以并行。 |
| `P1` | Progression event pipeline 包 | CU-12 / CU-21；`CharacterManagementModule`、事件 payload -> reward queue / quest progress 归并 | `C1`、`C2`、`C4` | 这是 settlement / battle / quest 共用基础管线，建议优先于 `P2`、`P3`。 |
| `P2` | Settlement pipeline 包 | CU-06 / CU-08；service request -> service result -> persistence -> reward/quest follow-up | `S3`、`P1` | 可与 `P3`、`P4` 并行；不要把 domain rule 塞回 `WorldMapSystem`。 |
| `P3` | Battle resolution pipeline 包 | CU-06 / CU-15；battle end -> resolution result -> party/world commit -> reward/loot follow-up | `S4`、`P1` | 可与 `P2`、`P4` 并行；避免直接散写 `pending_character_rewards`。 |
| `P4` | Warehouse mutation pipeline 包 | CU-10；统一入仓 / 出仓 / overflow / batch swap 结果结构 | `S2`、`C3` | 可与 `P2`、`P3` 并行；是 forge / loot 的共享下游。 |
| `P5` | Headless contract pipeline 包 | CU-21 / CU-19；text command / snapshot / expect 走正式 contract | `C5`、`P1`、`P2`、`P3`、`P4` | 独占 snapshot / text schema 收口；最好在其他 pipeline 都有稳定字段后合并。 |
| `F1` | Research 系统实现包 | CU-08 / CU-12；reward queue 驱动的轻量系统 | `P1`、`P2` | 可与 `F2`、`F3` 并行；不依赖 quest 主链。 |
| `F2` | Forge 系统实现包 | CU-08 / CU-10；`RecipeDef + warehouse mutation + item schema` | `R3`、`P4` | 可与 `F1`、`F3` 并行；经济数值先最小闭环，不先做 tuning。 |
| `F3` | Battle result / loot 实现包 | CU-15 / CU-10；`BattleResolutionResult` 正式掉落与 overflow 接入 | `R2`、`P3`、`P4` | 可与 `F1`、`F2` 并行；不要反向扩散临时字段到 save/snapshot。 |
| `F4` | Quest 系统实现包 | CU-04 / CU-06 / CU-12 / CU-21；quest 接取、推进、奖励、存档、headless | `R4`、`P1`、`P2`、`P3`、`P5` | 这是最重整合包，建议最后单开。 |
| `F5` | Economy tuning 包 | item price、loot、forge、quest reward 的数值联调 | `F2`、`F3`、`F4` | 最后执行；只做 balance，不再改核心 schema。 |
| `V1` | Contract / registry / snapshot 回归包 | CU-19 / CU-21；contract regression、registry validation、snapshot regression | `Gate 3` 起持续运行 | 可作为独立验证泳道并行跟进，但字段冻结点必须跟 gate 对齐。 |
| `V2` | Vertical slice 验证包 | `playable_vertical_slice_roadmap.md` 对应 feature 组合场景 | `Gate 4` | 最终整体验证，不再承担主架构定型职责。 |

### 推荐并行编组

- 第 1 组：`G0`
- 第 2 组：`C1` + `C2` + `C3`
- 第 3 组：`C4` + `C5`
- 第 4 组：`S1` + `S2` + `S3` + `S4` + `R1` + `R2`
- 第 5 组：`R3` + `R4` + `P1`
- 第 6 组：`P2` + `P3` + `P4`
- 第 7 组：`P5` + `V1`
- 第 8 组：`F1` + `F2` + `F3`
- 第 9 组：`F4`
- 第 10 组：`F5` + `V2`

### 最小可启动并行切片

- 如果当前只能拉 3 个人并行，最稳的起手不是按 `Phase 1` 平铺，而是：
  - 1 人做 `C1` settlement / progression contract
  - 1 人做 `C2` battle resolution / drop identity contract
  - 1 人做 `C3` item / recipe schema
- 这三包完成后，再由 1 人统一收口 `C5` save / snapshot compatibility，避免多人同时改共享 choke point。

## Mainline Phases

### Phase 0：Owner Freeze

#### 目标

- 把当前正式 owner、桥接层、sidecar、兼容层写死到设计文档里，避免后续 feature 改动再次漂移。

#### 交付

- 冻结以下边界：
  - `GameSession` 不承载玩法流程，只承载持久化与内容缓存。
  - `GameRuntimeFacade` 不回收已拆出的 settlement / warehouse / reward / snapshot 细节。
  - `WorldMapSystem` 保持 scene adapter / input bridge，不重新变回 runtime 真相源。
  - `CharacterManagementModule` 不重新吸收 battle unit factory 责任。
  - `BattleRuntimeModule` 继续只保留 battle 核心编排，不回收已拆出的 charge / rating / terrain effect / unit factory。

#### 验收标准

- 文档层明确“owner / sidecar / compatibility layer”的正式定义。
- 后续新系统接入必须显式声明 state lives where、logic lives where、snapshot comes from where。

### Phase 1：Canonical Contracts

#### 目标

- 先定义正式共享 contract，而不是直接写玩法实现。

#### 必须定型的 contract

- `SettlementServiceResult`
  - 建议字段：
    - `success`
    - `message`
    - `persist_party_state`
    - `persist_world_data`
    - `persist_player_coord`
    - `inventory_delta`
    - `gold_delta`
    - `pending_character_rewards`
    - `quest_progress_events`
    - `service_side_effects`
- `BattleResolutionResult`
  - 建议字段：
    - `winner_faction_id`
    - `encounter_resolution`
    - `loot_entries`
    - `overflow_entries`
    - `pending_character_rewards`
    - `quest_progress_events`
    - `world_mutations`
    - `party_resource_commit`
- `QuestDef / QuestState`
  - 明确：
    - 接取条件
    - 目标条件
    - 状态机
    - 可序列化进度
    - 奖励 payload 形状
- `RecipeDef`
  - 明确：
    - 输入材料
    - 输出成品
    - 所需 service / facility tag
    - 失败原因
- `ItemDef` 扩展 schema
  - 明确是否新增：
    - `buy_price`
    - `sell_price`
    - `tags`
    - `crafting_groups`
    - `quest_groups`
- enemy identity / drop profile
  - 明确掉落归属依据：
    - `enemy_template_id`
    - `encounter_profile_id`
    - `settlement tier`
    - 或 battle profile

#### 约束

- 所有 contract 都要先考虑：
  - save/load round-trip
  - headless snapshot / text command
  - `StringName` key 稳定性
  - legacy payload 兼容策略

#### 验收标准

- 所有跨系统写入都能通过命名稳定的 payload 表达，不再依赖隐式字段拼接。

### Phase 2：State Model Stabilization

#### 目标

- 在正式做 feature 前，把最可能升级的 runtime state 稳下来。

#### 重点 state

- `PartyState`
  - 新增 quest 所需字段与版本升级策略。
- `WarehouseState`
  - 明确 loot overflow、craft output、quest turn-in 的统一写入口。
- `EquipmentState`
  - 维持 entry-based 模型，不再倒退成 slot->item 占位字典。
- battle result state
  - 明确“战斗内临时结果”和“战后正式提交结果”的边界。
- settlement state
  - 明确 shop / stagecoach / research / forge / contract board 的 runtime state shape。

#### 验收标准

- `PartyState`、quest state、warehouse state、battle resolution state 都有明确版本升级口径。
- 文本快照与结构化 snapshot 的字段来源稳定。

### Phase 3：Content Resourceization

#### 目标

- 把高频扩展内容迁到资源或声明式 spec，使 registry 回到基础设施职责。

#### 推荐顺序

1. `professions`
2. `enemy templates / ai brains / encounter rosters`
3. `skills`
4. `recipes`
5. `quests`

#### 原则

- registry 负责：
  - 目录扫描
  - schema 校验
  - id 去重
  - 非法引用报告
- 内容本体不继续留在大型注册脚本里硬编码。
- 允许保留桥接层，但最终真相源只保留资源或声明式 spec 一份。

#### 验收标准

- 新增一个职业、敌人、配方、任务，不需要改 runtime 主流程脚本。

### Phase 4：Runtime Pipeline Completion

#### 目标

- 把未来 feature 会依赖的几条主链补成正式管线。

#### 必须补齐的主链

- settlement pipeline
  - service request -> service result -> persistence -> reward/quest follow-up
- battle resolution pipeline
  - battle end -> resolution result -> party/world commit -> reward/loot follow-up
- warehouse mutation pipeline
  - 入仓 / 出仓 / overflow / batch swap 的统一结果结构
- progression event pipeline
  - battle / settlement / quest / research / forge 统一产出 progression events
- headless contract pipeline
  - text command / snapshot / expect 读取正式 contract，而不是读取临时字段

#### 验收标准

- 这几条主链中的任意一步新增字段，不需要在 5 个以上文件里手工散写拼接逻辑。

### Phase 5：System Implementation

#### 目标

- 在稳定 owner + contract + schema 的前提下，再实现具体系统。

#### 推荐实现顺序

1. `research`
  - 原因：最轻，主要依赖 reward queue contract。
2. `forge`
  - 原因：依赖 `RecipeDef + warehouse mutation + item schema`。
3. `battle result / loot`
  - 原因：依赖 `BattleResolutionResult`。
4. `quest`
  - 原因：最重，跨 settlement / battle / world / save / headless。
5. `economy tuning`
  - 原因：应建立在 item price / loot / forge / quest reward 都有正式 schema 后。

#### 约束

- quest 不作为早期最小实现的前置条件。
- economy balance 不先于 price schema / drop schema 定型。

### Phase 6：Vertical Slice Validation

#### 目标

- 这时才用纵切验证架构是否支撑一个完整体验 loop。

#### 纵切的定位

- 验证：
  - battle result contract 是否够用
  - settlement service contract 是否够用
  - content registry 是否能支撑扩容
  - snapshot / save / reward / progression 是否一致
- 不再承担“反向决定主架构”的职责。

## Cross-Cutting Invariants

- 不把 domain rule 塞回 `GameRuntimeFacade` 或 `WorldMapSystem`。
- 不在 UI 节点中直接写正式 party/world/battle state。
- 所有新增正式状态都必须给出：
  - save/load 口径
  - snapshot 口径
  - headless 命令可见性
  - 资源校验口径
- 所有新增内容都优先走资源或声明式 spec，而不是新增硬编码注册分支。
- `pending_character_rewards` 是正式奖励真相源。

## Relationship With Existing Docs

### 与 `playable_vertical_slice_roadmap.md`

- 保留为：
  - feature backlog
  - 纵切验证目标
  - 下游接入顺序参考
- 不再作为当前主执行顺序。

### 与 `module_refactor_plan.md`

- 保留为：
  - 已识别的腐化问题与历史拆分 rationale
  - 对当前已完成拆分的背景说明
- 当前主线不再以“大规模继续拆分脚本”为唯一目标，而以“稳定 contract / schema / pipeline”为主。

### 与 `project_context_units.md`

- 当前上下文单元划分仍有效。
- 若后续新增 quest / recipe / battle resolution contract 文档并改变核心 read-set，再更新该文档。

## Minimal Slice

- 当前最小交付不是“做成可玩 loop”，而是“做成稳定 contract 包”：
  - `SettlementServiceResult`
  - `BattleResolutionResult`
  - `QuestDef / QuestState`
  - `RecipeDef`
  - `ItemDef` 扩展 schema
  - content registry 资源化边界与校验策略

## Files To Change

### 本文落地后的优先文档

- `docs/design/architecture_mainline_roadmap.md`（本文）
- `docs/design/playable_vertical_slice_roadmap.md`
  - 后续建议补一段“当前为 feature backlog，不再是主线排期”。
- 建议新增：
  - `docs/design/core_runtime_contracts.md`
  - `docs/design/battle_resolution_contract.md`
  - `docs/design/content_resource_migration_plan.md`

### 未来实现时优先涉及的 runtime 文件

- `scripts/systems/game_session.gd`
- `scripts/systems/save_serializer.gd`
- `scripts/systems/game_runtime_facade.gd`
- `scripts/systems/battle_session_facade.gd`
- `scripts/systems/game_runtime_settlement_command_handler.gd`
- `scripts/systems/game_runtime_snapshot_builder.gd`
- `scripts/player/progression/party_state.gd`
- `scripts/player/warehouse/item_def.gd`
- `scripts/enemies/enemy_content_registry.gd`
- `scripts/player/progression/progression_content_registry.gd`

## Tests To Add Or Run

### 架构主线阶段优先补的测试

- contract regression
  - settlement service result schema
  - battle resolution result schema
  - quest state serialization
  - recipe schema validation
  - item schema validation
- registry validation
  - 缺失 id
  - 重复 id
  - 非法引用
  - 非法 slot / tag / profile
- snapshot regression
  - 结构化 snapshot 与文本快照覆盖新的正式字段

### 当前已有回归中最相关的入口

- `tests/warehouse/run_party_warehouse_regression.gd`
- `tests/battle_runtime/run_battle_runtime_smoke.gd`
- `tests/battle_runtime/run_wild_encounter_regression.gd`
- `tests/progression/run_progression_tests.gd`
- `tests/text_runtime/run_text_command_regression.gd`

## Execution Constraints

- 不在 quest schema 定型前实现完整 quest 闭环。
- 不在 battle resolution contract 定型前实现正式 loot / overflow。
- 不在 item price / tags schema 定型前做大规模 economy tuning。
- 不在 registry 资源化边界明确前继续扩写大块硬编码 seed 内容。

## Project Context Units Impact

- 本文不改 runtime ownership，只重排当前设计优先级。
- `docs/design/project_context_units.md` 当前仍可视为有效，不需要在本任务中同步修改。
- 若后续新增 quest / recipe / battle resolution 的核心上下文单元，再一并更新。
