# 当前 Godot 项目的最优上下文单元

更新日期：`2026-04-17`

## 目的

- 这份文档按“真相源 + 运行期职责 + 改动边界”切分当前仓库。
- 它不是目录导览，而是给 agent / 开发者做装载决策用的：改一个需求时，应该一起读哪些文件，哪些不要顺手混进来。
- 本次刷新基于当前仓库实际文件树与入口脚本，覆盖登录壳、世界预设与存档、世界/战斗双态 runtime、共享仓库与装备基础流转、角色成长/成就、战斗棋盘主链、敌方内容注册表、headless 文本指令链，以及现有自动化回归。

## 读取范围

- 主运行链：
  - `project.godot`
  - `scenes/main/*.tscn`
  - `scenes/ui/*.tscn`
  - `scenes/common/*.tscn`
  - `scripts/systems/*.gd`
  - `scripts/ui/*.gd`
  - `scripts/player/equipment/*.gd`
  - `scripts/player/progression/*.gd`
  - `scripts/player/warehouse/*.gd`
  - `scripts/enemies/*.gd`
  - `scripts/enemies/actions/*.gd`
  - `scripts/utils/*.gd`
- 数据与内容：
  - `data/configs/world_map/*.tres`
  - `data/configs/items/*.tres`
  - `data/configs/skills/*.tres`
  - `data/configs/professions/*.tres`
  - `data/saves/fixed_test_world_save.dat`
  - `assets/main/battle/terrain/canyon/*.png`
- 自动化与辅助：
  - `tests/equipment/*.gd`
  - `tests/warehouse/*.gd`
  - `tests/battle_runtime/*.gd`
  - `tests/progression/*.gd`
  - `tests/text_runtime/*.gd`
  - `tests/text_runtime/scenarios/*.txt`
- 不作为核心运行时单元：
  - `.godot/`
  - 所有 `.uid`
  - `prompts/`
  - `example/`
  - `.vscode/`
  - `scenes/main/game_placeholder.tscn`
  - `battle_board_canyon_capture.png`
  - `login_screen_capture.png`
  - `world_map_capture.png`
  - `world_map_flow_capture.png`

## 当前主干关系

```text
LoginScreen
  -> WorldPresetPicker / SaveList / DisplaySettings
  -> GameSession

GameSession
  -> SaveSerializer
  -> GameLogService
  -> WorldPresetRegistry
  -> WorldMapSpawnSystem
  -> ProgressionSerialization
  -> ProgressionContentRegistry
      -> SkillContentRegistry
      -> ProfessionContentRegistry
  -> ItemContentRegistry
  -> EnemyContentRegistry

WorldMapSystem
  -> WorldMapRuntimeProxy
      -> GameRuntimeFacade
          -> BattleSessionFacade
          -> GameRuntimeBattleSelection
          -> GameRuntimeBattleSelectionState
          -> GameRuntimeSettlementCommandHandler
              -> SettlementShopService
          -> GameRuntimeWarehouseHandler
          -> GameRuntimePartyCommandHandler
          -> GameRuntimeRewardFlowHandler
          -> GameRuntimeSnapshotBuilder
          -> WorldMapGridSystem / WorldMapFogSystem
          -> PartyWarehouseService / PartyEquipmentService
          -> CharacterManagementModule
          -> BattleRuntimeModule
          -> BattleHudAdapter
          -> GameTextSnapshotRenderer

PartyEquipmentService
  -> EquipmentState / EquipmentRules
  -> PartyWarehouseService

CharacterManagementModule
  -> PartyEquipmentService
  -> ProgressionService / ProfessionRuleService / ProfessionAssignmentService / SkillMergeService
  -> AttributeService
  -> achievement progress
  -> PendingCharacterReward queue

BattleRuntimeModule
  -> BattleChargeResolver
  -> BattleRepeatAttackResolver
  -> BattleTerrainEffectSystem
  -> BattleRatingSystem
  -> BattleUnitFactory
  -> BattleState / BattleUnitState / BattleCellState / BattleTimelineState / BattleTerrainEffectState
  -> BattleGridService / BattleEdgeService / BattleDamageResolver / BattleHitResolver / BattleAiService / BattleAiScoreService
  -> BattleTerrainRules / BattleTerrainTopologyService
  -> BattleTerrainGenerator / EncounterRosterBuilder

BattleAiService
  -> BattleAiContext / BattleAiScoreInput / BattleAiScoreService
  -> EnemyTemplateDef / EnemyAiBrainDef / EnemyAiStateDef
  -> enemy actions/*

BattleMapPanel
  -> BattleHudAdapter
  -> BattleBoard2D
      -> BattleBoardController
      -> BattleBoardPropCatalog / BattleBoardProp

HeadlessGameTestSession
  -> GameSession
  -> GameRuntimeFacade
  -> GameTextCommandRunner
      -> GameTextCommandResult
```

## 切分原则

- 先找真相源，再找桥接层，再找展示层。
- 当前最重的桥接单元已经从单一 `WorldMapSystem` 变成三层：
  - `GameSession` 负责全局持久化与内容缓存。
  - `GameRuntimeFacade` 负责世界/战斗/奖励/窗口状态的核心运行时。
  - `WorldMapRuntimeProxy` 负责给场景层暴露稳定的命令 / 读取表面，并在命令后统一触发渲染回调。
  - `WorldMapSystem` 主要承担场景节点接线、输入捕获、窗口信号回调与 UI 渲染同步，不再对外暴露 runtime 命令 / snapshot 透传接口。
- `BattleMapPanel -> BattleBoard2D -> BattleBoardController` 仍是当前正式战斗展示主链。
- `PartyState.pending_character_rewards` 是当前唯一正式奖励队列；旧 `pending_mastery_rewards` 兼容桥已移除。
- 共享仓库现在是独立单元，不再只是队伍窗口的附属功能。
- `PartyEquipmentService` 已经是正式 bridge，不应再把“装备基础流转”误判为纯 UI 或纯仓库任务。
- 敌方模板与 AI brain 已经是单独的内容单元，不应再塞进 battle runtime 文件里顺手改。
- headless / text runtime 已经是正式辅助链路，不是临时测试脚本，也不是主启动链或正式玩家 UI。
- 当前自动化覆盖已经扩展到：
  - 装备种子内容与装备 / 卸装回归
  - 共享仓库规则
  - battle runtime smoke 与 battle board 渲染契约
  - progression / achievement / reward queue
  - text command headless 回归

## 单元总览

### CU-01 登录壳、世界预设、存档选择、显示设置

- 文件：
  - `project.godot`
  - `scenes/main/login_screen.tscn`
  - `scripts/ui/login_screen.gd`
  - `scenes/ui/world_preset_picker_window.tscn`
  - `scripts/ui/world_preset_picker_window.gd`
  - `scenes/ui/save_list_window.tscn`
  - `scripts/ui/save_list_window.gd`
  - `scenes/ui/display_settings_window.tscn`
  - `scripts/ui/display_settings_window.gd`
  - `scripts/utils/display_settings_service.gd`
  - `scripts/utils/world_preset_registry.gd`
  - `data/saves/fixed_test_world_save.dat`
- 真相源：
  - 启动页当前交互状态。
  - 世界预设列表与 bundled save 入口。
  - `user://display_settings.cfg`。
- 主要职责：
  - 选择新世界、现有存档、固定测试存档。
  - 应用与保存显示设置。
  - 调用 `GameSession` 后切场景到 `world_map.tscn`。
- 邻接单元：
  - CU-02
  - CU-03
- 适合任务：
  - 改登录流程。
  - 改 save list 展示。
  - 改世界预设入口。
  - 改显示设置。
- 不要顺手带上：
  - `scripts/systems/world_map_system.gd`
  - `scripts/systems/game_runtime_facade.gd`

### CU-02 GameSession、存档、序列化、全局内容缓存

- 文件：
  - `scripts/systems/game_session.gd`
  - `scripts/systems/game_log_service.gd`
  - `scripts/systems/progression_serialization.gd`
  - `scripts/systems/save_serializer.gd`
- 真相源：
  - 当前 active save id / path / meta。
  - 当前 generation config path / object。
  - 当前 `world_data`、玩家坐标、玩家 faction。
  - 当前 `PartyState`。
  - 缓存后的 `skill_defs` / `profession_defs` / `achievement_defs` / `item_defs` / `recipe_defs` / `enemy_templates` / `enemy_ai_brains`。
- 主要职责：
  - 创建新存档、读取现有存档、读取 bundled save。
  - 管理 `user://saves/index.dat` 与 slot payload，当前全局 `SAVE_VERSION = 5`。
  - 统一持久化 `world_data + party_state`。
  - `GameLogService` 现在作为开发侧 sidecar 挂在 `GameSession` 下，负责维护内存 ring buffer 并把结构化运行日志追加到 `user://logs/*.jsonl`；它不是存档真相源，也不参与 `SAVE_VERSION`。
  - `SaveSerializer` 负责 save payload、V5 解码、save meta、world/party 归一化和 save index 的编码格式。
  - 提供 progression / item / recipe / enemy 内容注册表的统一访问口。
  - 提供战斗中的 save lock。
- 邻接单元：
  - CU-01
  - CU-03
  - CU-04
  - CU-10
  - CU-11
  - CU-13
  - CU-20
  - CU-21
- 适合任务：
  - 改 save payload。
  - 改 slot meta。
  - 改 active world 生命周期。
  - 改 serialization / legacy 兼容。
  - 改内容注册表接入。
- 不要顺手带上：
  - `scripts/ui/world_map_view.gd`
  - `scripts/ui/battle_board_controller.gd`

### CU-03 世界配置资源与预设数据

- 文件：
  - `scripts/utils/world_map_generation_config.gd`
  - `scripts/utils/settlement_config.gd`
  - `scripts/utils/settlement_distribution_rule.gd`
  - `scripts/utils/facility_config.gd`
  - `scripts/utils/facility_slot_config.gd`
  - `scripts/utils/facility_npc_config.gd`
  - `scripts/utils/weighted_facility_entry.gd`
  - `scripts/utils/wild_spawn_rule.gd`
  - `data/configs/world_map/test_world_map_config.tres`
  - `data/configs/world_map/small_world_map_config.tres`
  - `data/configs/world_map/medium_world_map_config.tres`
  - `data/configs/world_map/demo_world_map_config.tres`
- 真相源：
  - 世界尺寸、chunk、玩家视野、程序化生成开关。
  - 据点模板、设施模板、设施槽位、服务 NPC、野外遭遇规则。
- 主要职责：
  - 定义 world spawn 输入资源。
  - 为登录预设与 `GameSession` 提供 generation config。
- 邻接单元：
  - CU-01
  - CU-02
  - CU-04
- 适合任务：
  - 新增世界规模。
  - 改据点模板与设施组合。
  - 改野怪分布规则。
- 不要顺手带上：
  - `scripts/ui/world_map_view.gd`
  - `scripts/systems/battle_runtime_module.gd`

### CU-04 世界生成、据点服务注入、遭遇锚点

- 文件：
  - `scripts/systems/world_map_spawn_system.gd`
  - `scripts/systems/encounter_anchor_data.gd`
  - `scripts/utils/world_event_config.gd`
  - `scripts/utils/mounted_submap_config.gd`
  - 依赖 CU-03 资源定义
- 真相源：
  - `world_data` 的生成结构。
  - settlements / world_npcs / encounter_anchors / world_events / mounted_submaps / player_start_* 的输出形状。
- 主要职责：
  - 生成固定或程序化据点。
  - 生成设施、服务 NPC、available services。
  - 注入兜底的共享仓库服务 `interaction_script_id = "party_warehouse"`。
  - 生成玩家开局位置、遭遇锚点、世界事件与挂载子地图定义。
- 邻接单元：
  - CU-02
  - CU-03
  - CU-05
  - CU-06
- 适合任务：
  - 改世界生成结构。
  - 改据点服务注入规则。
  - 改起始据点 / 起始遭遇保障。
- 不要顺手带上：
  - `scripts/ui/party_warehouse_window.gd`
  - `scripts/ui/battle_board_2d.gd`

### CU-05 世界网格与迷雾基础设施

- 文件：
  - `scripts/systems/world_map_grid_system.gd`
  - `scripts/systems/world_map_fog_system.gd`
  - `scripts/utils/world_map_cell_data.gd`
  - `scripts/utils/vision_source_data.gd`
- 真相源：
  - 大地图格子尺寸、占格、walkable。
  - faction 级 visible / explored / unexplored。
- 主要职责：
  - 提供世界地图空间层 API。
  - 为 world move、选中、迷雾刷新提供纯逻辑支持。
- 邻接单元：
  - CU-04
  - CU-06
  - CU-07
- 适合任务：
  - 改迷雾判定。
  - 改世界 footprint。
  - 改坐标与边界逻辑。
- 不要顺手带上：
  - `scripts/ui/settlement_window.gd`
  - `scripts/systems/battle_runtime_module.gd`

### CU-06 世界/战斗运行时总编排与场景适配

- 文件：
  - `scripts/systems/settlement_forge_service.gd`
  - `scenes/main/world_map.tscn`
  - `scenes/ui/submap_entry_window.tscn`
  - `scripts/systems/game_runtime_facade.gd`
  - `scripts/systems/battle_session_facade.gd`
  - `scripts/systems/game_runtime_battle_selection.gd`
  - `scripts/systems/game_runtime_battle_selection_state.gd`
  - `scripts/systems/game_runtime_settlement_command_handler.gd`
  - `scripts/systems/game_runtime_warehouse_handler.gd`
  - `scripts/systems/game_runtime_snapshot_builder.gd`
  - `scripts/systems/world_map_runtime_proxy.gd`
  - `scripts/systems/world_map_system.gd`
  - `scripts/ui/submap_entry_window.gd`
- 真相源：
  - 当前 world / battle 模式。
  - 当前 modal 互斥状态。
  - 当前 battle 技能选择、目标缓存、待处理奖励、待处理晋升。
  - 当前 headless snapshot 的结构化输出。
  - 当前场景节点与 runtime 的同步方式。
- 主要职责：
  - `GameRuntimeFacade` 持有真正的世界/战斗/奖励/仓库/窗口运行时状态。
  - `GameRuntimeFacade` 现在还持有 root world / active submap 的切换状态、进入确认提示和返回栈，并负责把 `_world_data` 的根世界结构映射成当前激活地图视图。
  - `BattleSessionFacade` 承载开战、battle tick / resolve、batch 同步、战斗输入桥接、战斗只读查询与战后回写，作为 `GameRuntimeFacade -> BattleRuntimeModule` 的 battle session 控制器；战斗开始/结束链路优先通过 `GameRuntimeFacade` 的显式 battle-support helper 协调，不再散写 runtime 私有字段。
  - `GameRuntimeBattleSelectionState` 持有战斗技能选择、目标队列与选中格等运行时状态。
  - `GameRuntimeBattleSelection` 承载战斗技能选择、目标队列与相关只读查询逻辑，并通过 `GameRuntimeBattleSelectionState` 读写状态；技能选择与目标队列的运行时读写优先通过 `GameRuntimeFacade` 的显式 selection helper / state accessor 协调，不再直接散写 runtime 私有字段。
  - `GameRuntimeSettlementCommandHandler` 承载据点动作 payload 组装、据点动作执行、任务板 / 商店 / 驿站 / forge modal 分流、据点状态回写与角色奖励 payload 归并；据点窗口状态、任务板 / 商店 / forge / 驿站窗口状态、据点反馈文本、默认交互成员和据点成功动作后的奖励 / 持久化链路优先通过 `GameRuntimeFacade` 的显式 settlement helper / state accessor 协调，不再直接散写 runtime 私有字段。
  - 任务板 `submit_item` 正式目标由 `GameRuntimeSettlementCommandHandler` 只负责解析活跃 objective / provider payload，并委托 `GameRuntimeFacade -> CharacterManagementModule` 执行正式仓库扣除；不要把任务缴纳扣库规则塞回据点窗口或 handler 本体。
  - `SettlementShopService` 承载商店库存生成、买卖结算和 shop runtime state 刷新；不要把商店定价和库存更新逻辑回塞到 `WorldMapSystem` 或 `SettlementWindow`。
  - `SettlementForgeService` 承载 `RecipeDef` 装载、设施标签校验以及通过 `PartyWarehouseService` 的原子扣料 / 入仓；不要把 forge 规则回塞到 `GameRuntimeSettlementCommandHandler` 或 `GameRuntimeFacade`。
  - `GameRuntimeWarehouseHandler` 承载共享仓库窗口数据、默认目标成员解析和 `warehouse` 命令处理；仓库窗口状态、当前入口标签、默认目标成员和仓库持久化链路的运行时读写优先通过 `GameRuntimeFacade` 的显式 warehouse helper / state accessor 协调，不再直接散写 runtime 私有字段。
  - `GameRuntimePartyCommandHandler` 承载队伍管理窗口打开、成员选择、队长切换、编成提交，以及装备 / 卸装与队伍持久化回写；队伍窗口、当前选中成员和队伍持久化链路的运行时读写优先通过 `GameRuntimeFacade` 的显式 party helper / state accessor 协调，不再直接散写 runtime 私有字段。
  - `GameRuntimeRewardFlowHandler` 承载角色奖励确认、晋升选择、reward/promotion modal 编排与待领奖励呈现时机；奖励 modal 需要继续避让据点任务板 / 商店 / forge / 驿站等互斥窗口，相关运行时读写优先通过 `GameRuntimeFacade` 的显式 reward helper / state accessor 协调，不再直接散写 runtime 私有字段。
  - `GameRuntimeSnapshotBuilder` 负责组织 headless 结构化快照，并通过 `GameRuntimeFacade` 的显式只读接口取数。
  - 初始化 `WorldMapGridSystem`、`WorldMapFogSystem`、`PartyWarehouseService`、`CharacterManagementModule`、`BattleRuntimeModule`。
  - `GameRuntimeFacade` 继续处理世界移动、battle/world 总编排、模态状态真相源与统一持久化桥接，不再直接承载队伍命令或奖励确认流程细节。
  - 正式 `command_*` 结果日志、battle batch 战报镜像、以及供 headless / agent 消费的最近日志快照，现在从这里汇总到 `GameSession -> GameLogService`；不要把日志真相源塞回 `WorldMapSystem`。
  - mounted submap 进入确认、battle start 确认、active map 切换、任意点击返回和 submap player coord 持久化，都属于这里的正式运行时主链。
  - 维护 headless 可消费的结构化 snapshot 与状态文本。
  - `WorldMapRuntimeProxy` 负责给 `WorldMapSystem` 提供唯一正式的命令 / 读取接口，并在命令执行后统一触发渲染回调。
  - `WorldMapSystem` 负责场景树接线、输入捕获、窗口信号绑定，并把 runtime 状态渲染到 `WorldMapView` / `BattleMapPanel` / 各 modal；据点侧现在正式包含 `SettlementWindow`、复用 `ShopWindow` shell 的 `contract_board` / `shop` / forge entry-list modal，以及 `StagecoachWindow` 服务窗口，`SubmapEntryWindow` 继续作为通用确认窗承接 submap 进入确认与 battle start 确认；场景层不再承担 `command_*` / snapshot 对外透传。
- 邻接单元：
  - CU-02
  - CU-04
  - CU-05
  - CU-07
  - CU-08
  - CU-09
  - CU-10
  - CU-12
  - CU-18
  - CU-21
- 适合任务：
  - 改 world / battle 模式切换。
  - 改 modal 互斥。
  - 改奖励 / 仓库 / 晋升入口接线。
  - 改 scene adapter 与 runtime 的同步。
  - 改 headless snapshot 结构。
- 不要把它当成：
  - 世界生成本体。
  - 仓库规则本体。
  - 战斗 renderer 本体。

### CU-07 世界地图渲染叶子单元

- 文件：
  - `scripts/ui/world_map_view.gd`
- 真相源：
  - 当前视口内 world map 的绘制结果。
- 主要职责：
  - 格子背景、迷雾、据点、NPC、遭遇点、世界事件、玩家、选中框绘制。
  - 鼠标坐标到 world coord 的映射。
- 邻接单元：
  - CU-05
  - CU-06
- 适合任务：
  - 改大地图视觉。
  - 改点击命中。
  - 改玩家贴图与相机感受。
- 不要顺手带上：
  - `scripts/systems/game_session.gd`
  - `scripts/systems/party_warehouse_service.gd`

### CU-08 据点窗口与人物信息窗口

- 文件：
  - `scenes/ui/settlement_window.tscn`
  - `scripts/ui/settlement_window.gd`
  - `scenes/ui/shop_window.tscn`
  - `scripts/ui/shop_window.gd`
  - `scenes/ui/stagecoach_window.tscn`
  - `scripts/ui/stagecoach_window.gd`
  - `scenes/ui/character_info_window.tscn`
  - `scripts/ui/character_info_window.gd`
- 真相源：
  - `SettlementWindow` 的当前展示态。
  - `ShopWindow` / forge entry-list / `StagecoachWindow` 的当前展示态。
  - 人物信息窗口上下文。
  - 当前 scene 层据点按钮与关闭信号的发射边界。
- 主要职责：
  - 展示设施、服务、服务 NPC、服务按钮，以及据点成员选择 / 成本 / 状态摘要。
  - 展示商店买卖条目与驿站路线条目，并把选中的结算参数回传给 runtime。
  - 展示 world / battle 单位详情。
  - 发出据点服务按钮、商店买卖请求、驿站出发请求和关闭窗口信号，由 runtime handler 执行真正的据点动作。
- 邻接单元：
  - CU-04
  - CU-06
  - CU-09
  - CU-12
- 适合任务：
  - 改据点窗口字段。
  - 改服务按钮与 payload。
  - 改人物详情展示。
- 不要顺手带上：
  - `scripts/systems/battle_runtime_module.gd`
  - `scripts/systems/progression_service.gd`

### CU-09 队伍管理、成就摘要、转职、角色奖励窗口层

- 文件：
  - `scenes/ui/party_management_window.tscn`
  - `scripts/ui/party_management_window.gd`
  - `scenes/ui/promotion_choice_window.tscn`
  - `scripts/ui/promotion_choice_window.gd`
  - `scenes/ui/mastery_reward_window.tscn`
  - `scripts/ui/mastery_reward_window.gd`
- 真相源：
  - 各窗口自身的选中态与展示态。
- 主要职责：
  - 展示 active / reserve roster、leader 切换。
  - 展示成员职业、技能、成就摘要。
  - 展示待转职选择。
  - 展示通用 `PendingCharacterReward` 队列。
  - 只发出动作，不负责真正改 `PartyState` / progression。
- 邻接单元：
  - CU-06
  - CU-10
  - CU-11
  - CU-12
- 适合任务：
  - 改队伍管理 UI。
  - 改成就摘要展示。
  - 改转职 / 奖励弹窗文案与交互。
- 不要顺手带上：
  - `scripts/systems/battle_grid_service.gd`
  - `scripts/systems/world_map_spawn_system.gd`

### CU-10 共享仓库、物品定义与装备基础流转

- 文件：
  - `scripts/player/equipment/equipment_rules.gd`
  - `scripts/player/equipment/equipment_state.gd`
  - `scripts/player/warehouse/item_def.gd`
  - `scripts/player/warehouse/item_content_registry.gd`
  - `scripts/player/warehouse/recipe_def.gd`
  - `scripts/player/warehouse/recipe_content_registry.gd`
  - `scripts/player/warehouse/warehouse_state.gd`
  - `scripts/player/warehouse/warehouse_stack_state.gd`
  - `scripts/systems/party_warehouse_service.gd`
  - `scripts/systems/party_equipment_service.gd`
  - `scenes/ui/party_warehouse_window.tscn`
  - `scripts/ui/party_warehouse_window.gd`
  - `data/configs/recipes/*.tres`
  - `data/configs/items/bronze_sword.tres`
  - `data/configs/items/militia_axe.tres`
  - `data/configs/items/watchman_mace.tres`
  - `data/configs/items/scout_dagger.tres`
  - `data/configs/items/leather_cap.tres`
  - `data/configs/items/leather_jerkin.tres`
  - `data/configs/items/scout_charm.tres`
  - `data/configs/items/healing_herb.tres`
  - `data/configs/items/bandage_roll.tres`
  - `data/configs/items/travel_ration.tres`
  - `data/configs/items/torch_bundle.tres`
  - `data/configs/items/antidote_herb.tres`
  - `data/configs/items/iron_ore.tres`
  - `data/configs/items/beast_hide.tres`
  - `data/configs/items/hardwood_lumber.tres`
  - `data/configs/items/linen_cloth.tres`
- 真相源：
  - 物品定义与 `item_id -> ItemDef`。
  - 配方定义与 `recipe_id -> RecipeDef`。
  - 仓库堆栈状态。
  - 装备槽位状态与固定槽位规则。
  - 当前容量 / 已用 / 超容规则。
- 主要职责：
  - 按堆栈管理共享仓库。
  - 用全队 `storage_space` 统计容量。
  - `RecipeContentRegistry` 负责扫描 / 校验 `RecipeDef`，供据点 forge 流读取。
  - `preview_add_item` / `add_item` / `remove_item`。
  - 处理仓库与装备槽位之间的基础装备 / 卸装事务。
  - 仓库列表、详情、丢弃单件 / 全部。
- 说明：
  - 当前装备仍以静态 `item_id` 流转为基线；统一设计与延后的耐久 / 实例化边界见 `docs/design/equipment_system_plan.md`。
  - 如果任务进入“装备实例化 / 耐久 / 战斗内装备损坏”，不要把它当成 CU-10 单独任务，至少还要补 CU-11、CU-12、CU-15、CU-16。
- 邻接单元：
  - CU-02
  - CU-09
  - CU-06
  - CU-11
  - CU-12
  - CU-19
  - CU-21
- 适合任务：
  - 改堆叠规则。
  - 改容量规则。
  - 增减物品内容。
  - 改基础装备 / 卸装与仓库联动。
  - 改仓库窗口。
- 不要顺手带上：
  - `scripts/systems/battle_runtime_module.gd`
  - `scripts/ui/world_map_view.gd`

### CU-11 队伍与角色成长运行时数据模型

- 文件：
  - `scripts/player/progression/party_state.gd`
  - `scripts/player/progression/party_member_state.gd`
  - `scripts/player/progression/unit_progress.gd`
  - `scripts/player/progression/unit_skill_progress.gd`
  - `scripts/player/progression/unit_profession_progress.gd`
  - `scripts/player/progression/unit_reputation_state.gd`
  - `scripts/player/progression/unit_base_attributes.gd`
  - `scripts/player/progression/attribute_snapshot.gd`
  - `scripts/player/progression/pending_profession_choice.gd`
  - `scripts/player/progression/achievement_progress_state.gd`
  - `scripts/systems/character_progression_delta.gd`
  - `scripts/systems/pending_character_reward.gd`
  - `scripts/systems/pending_character_reward_entry.gd`
- 真相源：
  - `PartyState` 本体。
  - 每个成员的 progression / achievements / 当前资源。
  - 正式角色奖励队列 `pending_character_rewards`。
  - 正式任务状态 `active_quests` / `claimable_quests` / `completed_quest_ids`。
  - 正式角色奖励结构。
- 主要职责：
  - 定义 party / member / progression / reward 数据模型。
  - 负责模型级 `to_dict` / `from_dict`。
  - 当前 `PartyState.version = 3`，包含 `warehouse_state`、`pending_character_rewards`、`active_quests`、`claimable_quests` 与 `completed_quest_ids`。
  - 若后续落地耐久 / 装备实例化，schema 升级会优先发生在本单元与 CU-10，不会只改 battle runtime。
- 邻接单元：
  - CU-02
  - CU-09
  - CU-10
  - CU-12
  - CU-13
  - CU-14
- 适合任务：
  - 改 party schema。
  - 改 achievement progress 或 reward queue 字段。
  - 改角色基础属性存储。
- 不要顺手带上：
  - `scripts/ui/battle_board_controller.gd`
  - 登录壳

### CU-12 CharacterManagement、成就记录、奖励归并桥

- 文件：
  - `scripts/systems/character_management_module.gd`
  - `scripts/systems/quest_progress_service.gd`
- 真相源：
  - battle / settlement / headless 与 party/progression / quest state 之间的桥接规则。
- 主要职责：
  - 提供 `PartyMemberState`、attribute snapshot 与 progression 查询，作为 `BattleUnitFactory` 构建 / 刷新战斗单位时的角色侧桥接。
  - 记录成就事件。
  - 生成与应用 `PendingCharacterReward`。
  - 统一使用 `PendingCharacterReward`。
  - 通过 `QuestProgressService` 接受 `quest_progress_events`，维护 `PartyState.active_quests` / `claimable_quests` / `completed_quest_ids`，并把事件上下文写入 `QuestState.last_progress_context`。
  - `submit_item` 正式任务目标的仓库扣除与进度推进也归这里协调：先按 objective 需求预览共享仓库可提交数量，再通过 `PartyWarehouseService` 做原子扣除，只有 quest progress 成功时才提交扣除结果。
  - 处理 profession promotion、战后 hp/mp/ko 回写。
  - 未来若存在战斗内装备损坏或耐久归零后的属性变化，也必须通过这里提供的角色状态 / attribute snapshot，让 `BattleUnitFactory` 把 party 与 battle 单位重新对齐。
- 邻接单元：
  - CU-06
  - CU-09
  - CU-10
  - CU-11
  - CU-13
  - CU-14
  - CU-15
- 适合任务：
  - 改战斗后成长回写。
  - 改 achievement unlock 流程。
  - 改角色奖励队列。
  - 改任务进度事件归并与 quest state 迁移。
- 不要顺手带上：
  - `scripts/ui/world_map_view.gd`
  - `scripts/ui/battle_board_2d.gd`

### CU-13 progression 内容定义、条件模型、seed 内容

- 文件：
  - `scripts/player/progression/skill_content_registry.gd`
  - `scripts/player/progression/design_skill_catalog.gd`
  - `scripts/player/progression/design_skill_catalog_warrior_specs.gd`
  - `scripts/player/progression/design_skill_catalog_archer_specs.gd`
  - `scripts/player/progression/design_skill_catalog_mage_specs.gd`
  - `scripts/player/progression/skill_def.gd`
  - `scripts/player/progression/combat_skill_def.gd`
  - `scripts/player/progression/combat_cast_variant_def.gd`
  - `scripts/player/progression/combat_effect_def.gd`
  - `scripts/player/progression/profession_def.gd`
  - `scripts/player/progression/tag_requirement.gd`
  - `scripts/player/progression/attribute_requirement.gd`
  - `scripts/player/progression/reputation_requirement.gd`
  - `scripts/player/progression/profession_promotion_requirement.gd`
  - `scripts/player/progression/profession_rank_requirement.gd`
  - `scripts/player/progression/profession_rank_gate.gd`
  - `scripts/player/progression/profession_granted_skill.gd`
  - `scripts/player/progression/profession_active_condition.gd`
  - `scripts/player/progression/profession_promotion_record.gd`
  - `scripts/player/progression/profession_content_registry.gd`
  - `scripts/player/progression/attribute_modifier.gd`
  - `scripts/player/progression/derived_attribute_rule.gd`
  - `scripts/player/progression/achievement_def.gd`
  - `scripts/player/progression/achievement_reward_def.gd`
  - `scripts/player/progression/quest_def.gd`
  - `scripts/player/progression/progression_content_registry.gd`
  - `scripts/player/progression/progression_data_utils.gd`
  - `data/configs/skills/*.tres`
  - `data/configs/professions/*.tres`
- 真相源：
  - 技能、职业、条件、修正器、achievement、quest 的静态定义与 seed 内容。
- 主要职责：
  - 定义 progression 语义，不直接执行业务流程。
  - `SkillContentRegistry` 负责扫描 `data/configs/skills/*.tres`，并报告 skill_id、嵌套战斗资源结构与基础 schema 相关的静态错误。
  - `DesignSkillCatalog` 现在通过声明式 spec provider 装载 warrior / archer / mage 技能目录；在 skill seed 未全量迁到资源前，它仍是 `ProgressionContentRegistry` 的最小兼容桥。
  - `ProfessionContentRegistry` 负责扫描 `data/configs/professions/*.tres`，并报告 profession_id、技能/职业引用与 rank requirement 相关的静态错误。
  - `ProgressionContentRegistry` 负责聚合 skill/profession registry、补齐未迁移 skill seed 的 code fallback，并汇总 skill/profession registry 的静态校验结果。
  - 做静态内容校验。
- 邻接单元：
  - CU-02
  - CU-11
  - CU-12
  - CU-14
  - CU-15
- 适合任务：
  - 新技能。
  - 新职业。
  - 新成就。
  - 改 requirement / reward 语义。
- 不要顺手带上：
  - 各类窗口脚本。
  - `scripts/systems/world_map_system.gd`

### CU-14 progression 规则与属性服务

- 文件：
  - `scripts/systems/progression_service.gd`
  - `scripts/systems/profession_rule_service.gd`
  - `scripts/systems/profession_assignment_service.gd`
  - `scripts/systems/skill_merge_service.gd`
  - `scripts/systems/attribute_service.gd`
- 真相源：
  - progression 规则执行结果。
  - attribute modifier / snapshot 计算结果。
- 主要职责：
  - 学习技能与知识。
  - 授予 mastery、重算角色等级。
  - 判断职业解锁 / 升级 / active 状态。
  - 技能合并与核心技能分配。
  - 派生属性计算。
- 邻接单元：
  - CU-11
  - CU-12
  - CU-13
  - CU-15
- 适合任务：
  - 改属性公式。
  - 改职业规则。
  - 改技能合并。
- 不要顺手带上：
  - `scripts/ui/battle_map_panel.gd`
  - `scripts/ui/login_screen.gd`

### CU-15 战斗运行时总编排

- 文件：
  - `scripts/systems/battle_runtime_module.gd`
  - `scripts/systems/battle_charge_resolver.gd`
  - `scripts/systems/battle_repeat_attack_resolver.gd`
  - `scripts/systems/battle_terrain_effect_system.gd`
  - `scripts/systems/battle_rating_system.gd`
  - `scripts/systems/battle_unit_factory.gd`
  - `scripts/systems/battle_command.gd`
  - `scripts/systems/battle_preview.gd`
  - `scripts/systems/battle_event_batch.gd`
- 真相源：
  - 当前 `BattleState` 生命周期。
  - 当前 active unit / phase / modal。
  - preview、event batch、battle rating 统计、post-battle reward 产出。
- 主要职责：
  - 开战、推进时间轴、接手 AI / manual command。
  - 预览与执行移动、单体技能、地面技能、charge、terrain effect。
  - `BattleChargeResolver` 负责冲锋路径推演、受阻停步、碰撞推挤、陷阱触发与路径 AOE。
  - `BattleRepeatAttackResolver` 负责 `repeat_attack_until_fail` 的逐段执行、资源消耗与日志归并，并把命中判定委托给 `BattleHitResolver`。
  - `BattleTerrainEffectSystem` 负责 timed terrain effect 的写入、推进与 tick 结算。
  - `BattleRatingSystem` 负责战斗评分统计、标签与结算奖励映射。
  - `BattleUnitFactory` 负责正式友军 / 敌军单位构建、战斗单位刷新桥接与 terrain 数据装配。
  - 统计战斗评分并产出 canonical post-battle character reward。
  - 消费 `GameSession` 提供的 skill defs、enemy templates、enemy AI brains。
  - `BattleRuntimeModule` 现在会把 `EncounterRosterBuilder` 提供的正式掉落条目原样写入 `BattleResolutionResult.loot_entries`，供战后结算直接消费稳定 drop identity。
  - `BattleRuntimeModule.start_battle()` 现在统一通过 `BattleUnitFactory` 构建友军 / 敌军 / terrain，`CharacterManagementModule` 不再直接产出 `BattleUnitState`。
  - 若以后实现“近战命中驱动的装备耐久 / 装备损坏”，这里是命中触发点，但正式状态写回仍要经过 CU-10 / CU-11 / CU-12。
- 邻接单元：
  - CU-12
  - CU-13
  - CU-14
  - CU-16
  - CU-17
  - CU-18
  - CU-20
- 适合任务：
  - 改指令生命周期。
  - 改地面技能流程。
  - 改战斗评分奖励。
  - 改 AI / 手动切换时序。
- 不要顺手带上：
  - 登录壳。
  - 共享仓库窗口。

### CU-16 战斗状态模型、边规则、伤害、AI 规则层

- 文件：
  - `scripts/systems/battle_state.gd`
  - `scripts/systems/battle_timeline_state.gd`
  - `scripts/systems/battle_unit_state.gd`
  - `scripts/systems/battle_cell_state.gd`
  - `scripts/systems/battle_terrain_effect_state.gd`
  - `scripts/systems/battle_edge_face_state.gd`
  - `scripts/systems/battle_edge_feature_state.gd`
  - `scripts/systems/battle_terrain_rules.gd`
  - `scripts/systems/battle_terrain_topology_service.gd`
  - `scripts/systems/battle_grid_service.gd`
  - `scripts/systems/battle_edge_service.gd`
  - `scripts/systems/battle_damage_resolver.gd`
  - `scripts/systems/battle_status_semantic_table.gd`
  - `scripts/systems/battle_hit_resolver.gd`
  - `scripts/systems/battle_ai_context.gd`
  - `scripts/systems/battle_ai_score_input.gd`
  - `scripts/systems/battle_ai_score_service.gd`
  - `scripts/systems/battle_ai_decision.gd`
  - `scripts/systems/battle_ai_service.gd`
- 真相源：
  - battle cells / columns / units / occupancy / height / terrain effect / edge feature / timeline。
  - per-unit 移动标签与地形基础规则的组合判定。
- 主要职责：
  - 提供 battle 纯规则层 API。
  - 处理 footprint、移动、墙边 / 高差 / 占位规则。
  - `BattleTerrainRules` 负责 `land / shallow_water / flowing_water / deep_water / mud / spike` 的基础通行与显示语义。
  - `BattleTerrainTopologyService` 负责按局部连通分量把水体重分类为 `shallow_water / flowing_water / deep_water`，供地形变化后的运行时修复复用。
- `BattleStatusSemanticTable` 负责 `status_effects` 的正式 stack / duration / tick 语义表，并作为 `BattleDamageResolver + BattleRuntimeModule` 的共享状态语义真相源；当前已正式覆盖 `burning / slow / staggered` 与首批常驻 buff/debuff 的统一 turn-end 持续时间口径（如 `attack_up / archer_pre_aim / pinned / taunted`）。
  - `BattleHitResolver` 负责当前命中率合成、deterministic 命中掷骰，以及 repeat-attack 的正式命中口径。
  - `BattleAiScoreService` 负责构造技能候选的正式评分输入包（命中收益、目标数量、资源消耗、站位目标），并通过 `BattleAiContext` 提供给 `BattleAiService / enemy actions` 共享读取；不要再把 skill score input 散写回各个 action。
  - 处理伤害、状态、AI 决策上下文与产出。
- 邻接单元：
  - CU-15
  - CU-17
  - CU-18
  - CU-20
- 适合任务：
  - 改地格规则。
  - 改边缘阻挡 / 墙 / 高差逻辑。
  - 改碰撞 / 伤害 / 行动点。
  - 改 AI 规则。
  - 改单位状态字段。
- 不要顺手带上：
  - `scripts/ui/party_management_window.gd`
  - `scripts/systems/world_map_spawn_system.gd`

### CU-17 战斗地形 profile、敌人 roster、prop 注入

- 文件：
  - `scripts/systems/battle_terrain_generator.gd`
  - `scripts/systems/encounter_roster_builder.gd`
  - `scripts/utils/battle_board_prop_catalog.gd`
- 真相源：
  - battle terrain profile id。
  - encounter -> enemy units / map / prop_ids / canonical loot entries 的构建规则。
- 主要职责：
  - 生成 `default` / `canyon` 地形。
  - 维护 canyon 的高度、terrain、wall、spawn、objective marker / tent / torch / spike barricade。
  - 生成阶段会先标记水体区域，再通过 `BattleTerrainTopologyService` 归类成 `shallow_water / flowing_water / deep_water`。
  - 为 roster 型遭遇生成稳定 `drop_source_id + drop_entry_id` 命名的正式 loot entries，供 battle resolution 直接消费。
  - 维护 prop id 枚举与排序优先级。
- 说明：
  - 当前正式 battle start 由 `BattleRuntimeModule.start_battle()` 调 `BattleTerrainGenerator.generate()`。
- 邻接单元：
  - CU-15
  - CU-16
  - CU-18
  - CU-19
  - CU-20
- 适合任务：
  - 改 canyon profile。
  - 改战斗出生点与 prop 注入。
  - 清理旧 / 新 battle build 边界。
- 不要顺手带上：
  - `scripts/ui/save_list_window.gd`
  - `scripts/systems/party_warehouse_service.gd`

### CU-18 战斗展示主链

- 文件：
  - `scenes/ui/battle_map_panel.tscn`
  - `scripts/ui/battle_map_panel.gd`
  - `scripts/ui/battle_hud_adapter.gd`
  - `scenes/ui/battle_board_2d.tscn`
  - `scripts/ui/battle_board_2d.gd`
  - `scripts/ui/battle_board_controller.gd`
  - `scenes/common/battle_board_prop.tscn`
  - `scripts/ui/battle_board_prop.gd`
  - `assets/main/battle/terrain/canyon/*.png`
- 真相源：
  - 当前 battle HUD 展示态。
  - 当前 TileMap board 的渲染结果、镜头位置、缩放和平移状态。
- 主要职责：
  - `BattleMapPanel` 负责 HUD 容器、按钮、viewport 事件转发。
  - `BattleHudAdapter` 把 `BattleState` 转成 HUD snapshot。
  - `BattleBoard2D` 负责 viewport 坐标、缩放、拖拽、focus。
  - `BattleBoardController` 负责填充 TileMap layers、marker、prop、unit 排序。
  - `BattleBoardProp` 负责 prop scene 的简易绘制。
  - canyon PNG 缺失时，controller 会回退到程序生成 tile。
- 邻接单元：
  - CU-06
  - CU-15
  - CU-16
  - CU-17
  - CU-19
- 适合任务：
  - 改 battle HUD。
  - 改 board 点击 / 缩放 / 平移。
  - 改 tile / cliff / overlay / prop / unit 排序。
  - 改 canyon 战斗视觉。
- 不要默认带上：
  - `scripts/systems/game_session.gd`
  - 登录壳

### CU-19 自动化回归与截图辅助

- 文件：
  - `tests/equipment/run_party_equipment_regression.gd`
  - `tests/warehouse/run_party_warehouse_regression.gd`
  - `tests/warehouse/run_item_recipe_schema_regression.gd`
  - `tests/world_map/runtime/run_settlement_forge_service_regression.gd`
  - `tests/battle_runtime/run_battle_runtime_smoke.gd`
  - `tests/battle_runtime/run_battle_board_regression.gd`
  - `tests/battle_runtime/run_battle_resolution_contract_regression.gd`
  - `tests/battle_runtime/run_status_effect_semantics_regression.gd`
  - `tests/battle_runtime/capture_canyon_battle_board.gd`
  - `tests/progression/run_progression_tests.gd`
  - `tests/progression/run_quest_schema_regression.gd`
  - `tests/runtime/run_game_log_service_regression.gd`
  - `tests/runtime/run_game_runtime_quest_progress_regression.gd`
  - `tests/runtime/run_game_runtime_snapshot_builder_regression.gd`
  - `tests/runtime/run_settlement_service_result_regression.gd`
  - `tests/text_runtime/run_text_command_regression.gd`
  - `tests/text_runtime/run_text_command_script.gd`
  - `tests/text_runtime/run_text_command_repl.gd`
  - `tests/text_runtime/scenarios/smoke_startup.txt`
  - `tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.gd`
- 主要职责：
  - equipment：
    - 装备种子内容校验
    - 装备 / 卸装与共享仓库联动
    - 装备属性进入角色快照
    - `PartyState` round-trip 后保留装备状态
  - warehouse：
    - 当前 schema 严格校验
    - 堆叠 / 容量 / 超容规则
    - save round-trip
    - 队伍入口与据点入口打开共享仓库
  - battle runtime smoke：
    - terrain effect 时序
    - 真堆叠列移动规则
    - 墙 / 高差阻挡
    - move command 扣 AP 与占位更新
    - height delta 与 column cache 同步
  - battle status semantics：
    - `status_effects` 的 stack / duration / tick 语义回归
    - `burning / slow / staggered` 的正式 runtime 口径
    - battle smoke 对 self-buff / target-debuff 的持续时间与 `BattleUnitState` 序列化回归
  - battle resolution contract：
    - `BattleResolutionResult` round-trip
    - battle end 构造 canonical result
    - `BattleSessionFacade -> GameRuntimeFacade` 的 canonical handoff
    - canonical battle resolution result handoff
  - battle board：
    - canyon 生成稳定性
    - layer / cliff / prop / unit 排序契约
    - `InputLayer.local_to_map()` 点击映射
    - 同 state 渲染签名稳定
  - progression：
    - achievement registry seed 校验
    - profession / combat skill seed 内容
    - member-scoped achievement progress
    - pending character reward 顺序与序列化
    - character reward 序列化与展示
  - runtime：
    - `GameLogService` ring buffer 与 jsonl 追加写盘
    - `SettlementServiceResult` canonical 契约
    - battle / quest runtime 桥的最小回写链
    - runtime snapshot / text snapshot 的日志分段稳定性
  - text runtime：
    - `game new/load`
    - `world/party/settlement/warehouse/battle/reward/promotion/close/snapshot/expect`
    - headless snapshot 与文本快照稳定性
  - capture：
    - 导出 `battle_board_canyon_capture.png` 做人工验收
  - world_map runtime：
    - `GameRuntimeSettlementCommandHandler` 的据点动作分发
    - canonical settlement result / quest progress / modal 路由
- 说明：
  - `capture_canyon_battle_board.gd` 只是截图辅助，不是断言型回归。
  - progression 测试现在是单 runner 脚本，不再走旧的 `helpers/` + `cases/` 目录。
- 邻接单元：
  - CU-10
  - CU-15
  - CU-17
  - CU-18
  - CU-21

### CU-20 敌方模板、AI brain、行动定义种子内容

- 文件：
  - `scripts/enemies/enemy_content_registry.gd`
  - `scripts/enemies/enemy_template_def.gd`
  - `scripts/enemies/wild_encounter_roster_def.gd`
  - `scripts/enemies/enemy_ai_brain_def.gd`
  - `scripts/enemies/enemy_ai_state_def.gd`
  - `scripts/enemies/enemy_ai_action.gd`
  - `scripts/enemies/actions/move_to_range_action.gd`
  - `scripts/enemies/actions/retreat_action.gd`
  - `scripts/enemies/actions/use_charge_action.gd`
  - `scripts/enemies/actions/use_ground_skill_action.gd`
  - `scripts/enemies/actions/use_unit_skill_action.gd`
  - `scripts/enemies/actions/wait_action.gd`
- 真相源：
  - `enemy_template_id -> EnemyTemplateDef`
  - `brain_id -> EnemyAiBrainDef`
  - `encounter_profile_id -> WildEncounterRosterDef`
  - 各状态下的 action 顺序、目标选择与距离策略。
- 主要职责：
  - 注册当前种子敌方模板、wild encounter roster、AI brain 与 roster drop schema。
  - 定义近战压迫型、远程控制型等默认行为树形态。
  - 为 `BattleRuntimeModule / BattleAiService` 提供静态敌方内容。
- 邻接单元：
  - CU-02
  - CU-15
  - CU-16
  - CU-17
- 适合任务：
  - 新敌人模板。
  - 改敌人技能表。
  - 改 AI state / action 顺序。
  - 改 target selector / distance 策略。
- 不要顺手带上：
  - `scripts/ui/*`
  - `scripts/player/warehouse/*`

### CU-21 Headless runtime、文本命令与快照渲染

- 文件：
  - `scripts/systems/headless_game_test_session.gd`
  - `scripts/systems/game_text_command_runner.gd`
  - `scripts/systems/game_text_command_result.gd`
  - `scripts/utils/game_text_snapshot_renderer.gd`
  - `tests/text_runtime/*.gd`
  - `tests/text_runtime/README.md`
- 真相源：
  - headless 模式下的 session / world loaded / runtime snapshot 语义。
  - 文本命令域、参数格式、expect 断言语法。
  - 文本快照渲染格式。
- 定位：
  - 开发 / 自动化辅助链路。
- 主要职责：
  - 用 `HeadlessGameTestSession` 在无 UI 环境挂起 `GameSession + GameRuntimeFacade`。
  - 用 `GameTextCommandRunner` 执行命令与断言。
  - 用 `GameTextCommandResult` 输出可读结果。
  - 用 `GameTextSnapshotRenderer` 渲染稳定文本快照；当前快照已正式包含 `logs` 段和 `[LOG]` 文本分段，以及据点 `contract_board` / forge modal 的 `[CONTRACT_BOARD]` / `[FORGE]` 分段，供自动化 / agent / 人工排障读取最近运行日志与服务窗口状态。
  - 当前还覆盖 mounted submap 的确认进入、返回原坐标和 active map snapshot / 文本命令。
  - 当前文本命令域已包含最小 `quest accept/progress/complete` 调试接口；party 快照与文本快照会稳定暴露 `party.quests` / `[QUEST]` 分段，其中 quest state 至少区分 `active_quest_ids` / `claimable_quest_ids` / `completed_quest_ids`，并可验证 settlement / battle 自动 quest progress 结果。
  - 为回归、调试、agent 驱动提供非 UI 入口，但不参与正式启动链。
- 邻接单元：
  - CU-02
  - CU-06
  - CU-10
  - CU-15
  - CU-19
- 适合任务：
  - 新增 headless 指令域。
  - 改 snapshot schema。
  - 改 REPL / 脚本执行 / expect 断言。
  - 为 agent 或自动化增加纯文本回归入口。
- 不要把它当成：
  - 主界面或登录入口。
  - 正式玩家 UI。
  - 世界生成真相源。

## 推荐装载组合

### 只改开始菜单、预设、显示设置

- 必带：
  - CU-01
  - CU-02
- 按需补：
  - 改预设配置补 CU-03

### 只改世界生成、设施服务、起始遭遇

- 必带：
  - CU-03
  - CU-04
- 按需补：
  - 改迷雾或 world move 判定补 CU-05
  - 改 world_data 落盘补 CU-02
  - 改挂载子地图入口或世界事件确认链补 CU-06 / CU-21

### 只改 world / battle runtime 接线、窗口互斥、场景同步

- 必带：
  - CU-06
- 按需补：
  - 改大地图绘制补 CU-07
  - 改 battle 展示补 CU-18
  - 改 headless snapshot 补 CU-21

### 只改大地图迷雾、选中、渲染

- 必带：
  - CU-05
  - CU-06
  - CU-07
- 说明：
  - 如果改的是世界事件图标、submap 返回提示或点击返回表现，仍按这组读取。

### 只改据点服务、人物信息、服务反馈

- 必带：
  - CU-06
  - CU-08
- 按需补：
  - 如果服务会发奖励或记成就，补 CU-12
  - 如果服务项来自 world spawn，补 CU-04

### 只改队伍编成、成就摘要、转职或角色奖励弹窗

- 必带：
  - CU-06
  - CU-09
  - CU-11
- 按需补：
  - 真正改成长规则时补 CU-12 / CU-14

### 只改共享仓库规则、物品内容、装备基础流转、仓库窗口

- 必带：
  - CU-10
  - CU-11
  - CU-19
- 按需补：
  - 如果改队伍管理里的装备摘要展示，补 CU-09
  - 如果改 runtime 接线，补 CU-06
  - 如果改 save schema 或严格校验链，补 CU-02
  - 如果改 headless 文本流，补 CU-21

### 只做装备耐久、装备实例化前置或战斗内装备损坏

- 必带：
  - CU-10
  - CU-11
  - CU-12
  - CU-15
  - CU-16
  - CU-19
- 按需补：
  - 如果改 runtime 场景接线或持久化落盘，补 CU-06
  - 如果改文本快照或 headless 指令断言，补 CU-21
  - 如果只是继续优化设计口径，可先对照 `docs/design/equipment_system_plan.md`

### 只改角色成长、成就、奖励归并

- 必带：
  - CU-11
  - CU-12
  - CU-13
  - CU-14
- 按需补：
  - 如果来源是战斗事件，补 CU-15
  - 如果要跑回归，补 CU-19

### 只改敌方模板、敌方技能表、AI brain

- 必带：
  - CU-20
  - CU-16
- 按需补：
  - 如果改开战装配与 roster，补 CU-15 / CU-17

### 只改战斗规则、伤害、AI、terrain effect

- 必带：
  - CU-15
  - CU-16
- 按需补：
  - 如果改 terrain profile / spawn / prop 注入，补 CU-17
  - 如果改 enemy static content，补 CU-20
  - 如果改展示反馈，补 CU-18

### 只改 canyon 地形、战斗 props、battle build

- 必带：
  - CU-17
  - CU-18
  - CU-19
- 按需补：
  - 如果改真正 battle start 装配，补 CU-15

### 只改 battle HUD、棋盘、TileMap 渲染、相机

- 必带：
  - CU-18
  - CU-19
- 按需补：
  - 改取数字段补 CU-15 / CU-16
  - 改 prop ids 与 terrain profile 衔接补 CU-17

### 只改 save payload、party schema、reward queue 严格校验

- 必带：
  - CU-02
  - CU-11
- 按需补：
  - 仓库字段补 CU-10
  - reward 归并逻辑补 CU-12

### 只改 headless 文本命令、快照、REPL 或脚本化回归

- 必带：
  - CU-21
  - CU-19
- 按需补：
  - 改 runtime schema 补 CU-06
  - 改具体业务领域补对应叶子单元

## 当前 sidecar / 兼容层 / 漂移点

- `scripts/ui/battle_map_view.gd`
  - 这个旧 battle renderer 已经从仓库移除；如果你只看到残留 `.uid` 或旧文档引用，可以直接忽略。
- `scripts/systems/battle_map_generation_system.gd`
  - 这个旧生成脚本也已从仓库移除；不要再把它当成默认 battle 入口。
- `tests/progression/`
  - 旧 `helpers/` + `cases/` 目录已经移除；当前只保留单 runner 模式。
- 结论：
  - world / battle 运行时已经拆成 `GameRuntimeFacade`、`WorldMapRuntimeProxy` 与 `WorldMapSystem` 三层。
  - battle renderer 主线已经完成换代。
  - reward queue 主线与边界命名已经统一到 `pending_character_rewards`。

### 兼容项清单（2026-04-17）

当前主线已不再保留历史存档、legacy alias、旧 payload 或旧运行时 fallback 兼容项。

#### 当前仍生效的兼容项

- 无。

#### 已移除的兼容项

| ID | 位置 | 已移除内容 |
| --- | --- | --- |
| `R01` | `scripts/player/progression/party_state.gd` / `scripts/systems/battle_resolution_result.gd` / `scripts/systems/settlement_service_result.gd` | `pending_mastery_rewards -> pending_character_rewards` 全链路兼容桥 |
| `R02` | `scripts/systems/pending_mastery_reward.gd` / `scripts/systems/pending_mastery_reward_entry.gd` | 旧 `PendingMasteryReward*` 类型 |
| `R03` | `scripts/systems/world_map_system.gd` / `scripts/ui/shop_window.gd` / `scripts/ui/stagecoach_window.gd` / `scripts/ui/settlement_window.gd` | 旧 UI payload `cost_text` alias；正式字段只保留 `cost_label` |
| `R04` | `scripts/systems/battle_state_factory.gd` | 旧 `BattleStateFactory` sidecar / fallback builder |
| `R05` | `scripts/systems/settlement_window_system.gd` / `scenes/main/world_map.tscn` | 旧 `SettlementWindowSystem` sidecar 与场景节点 |
| `R06` | `scripts/systems/save_serializer.gd` | 旧 save meta 默认值回填、旧 `Vector2i` 表达（`Vector2` / `Array`）与旧 save index entry 形状 |
| `R07` | `scripts/systems/game_session.gd` | 旧单文件存档 `user://world_map_state.dat` 清理入口 |
| `R08` | `scripts/systems/game_session.gd` | 旧仓库装备堆叠格式的显式拒绝兼容分支 |
| `R09` | `scripts/player/progression/party_state.gd` | 旧 `PartyState` 缺字段补全与 `version` 下限归一 |
| `R10` | `scripts/player/equipment/equipment_state.gd` / `scripts/player/equipment/equipment_entry_state.gd` | 旧顶层槽位映射、`set_equipped_item()` 与缺 `occupied_slot_ids` 装备数据 |
| `R11` | `scripts/systems/world_map_fog_system.gd` | 旧 fog `Dictionary -> WorldMapFogFactionState` 自动迁移 |
| `R12` | `scripts/systems/save_serializer.gd` | `world_preset_name` 缺失时按 `generation_config_path` 回填 fallback preset name |
| `R13` | `scripts/enemies/enemy_content_registry.gd` / `scripts/enemies/enemy_template_def.gd` | 敌人模板 `alias_ids` / `display_name` alias 查找 |
| `R14` | `scripts/systems/encounter_roster_builder.gd` | `monster_name` / `monster_display_name` 作为候选模板 ID 的兼容查找 |
| `R15` | `scripts/systems/battle_terrain_rules.gd` | legacy `water -> deep_water` 归一映射 |
| `R16` | `scripts/systems/battle_ai_service.gd` | 缺少 brain 时的旧版 AI 决策 `_choose_legacy_command()` |
| `R17` | `scripts/systems/battle_terrain_generator.gd` / `scripts/systems/battle_unit_factory.gd` | terrain profile 未识别或生成失败时的 fallback 地图 |
| `R18` | `scripts/systems/battle_session_facade.gd` | 缺少 `BattleResolutionResult` 时按 `runtime_state` 临时拼 fallback result |

#### 使用建议

- 当前如果读到旧 schema、旧 alias 或缺失正式 battle result，应直接视为无效输入或配置错误，而不是继续做隐式迁移。

## 不推荐的切法

- 不要把 `GameSession`、`GameRuntimeFacade`、`CharacterManagementModule`、`BattleRuntimeModule`、`BattleBoardController` 一次性全装，除非任务确实跨越“登录 -> 世界 -> 战斗 -> 战后成长 -> 存档 -> 文本测试”整条链。
- 不要在只改共享仓库时把 `battle_runtime_module.gd`、`battle_board_2d.gd` 一起带上。
- 不要在只改 battle HUD / TileMap 时把 `progression_service.gd`、`party_warehouse_service.gd` 一起带上。
- 不要在只改 achievement / reward queue 时默认把 `battle_board_controller.gd` 一起带上。
- 不要把 `WorldMapSystem` 当成当前唯一运行时真相源；核心状态现在主要落在 `GameRuntimeFacade`，场景侧命令 / 读取边界则落在 `WorldMapRuntimeProxy`。
- 不要把旧奖励设计文档里的 `pending_mastery_rewards` 示例误认为当前正式奖励真相源。

## 结论

- 当前最关键的桥接单元是：
  - CU-02 `GameSession`
  - CU-06 `GameRuntimeFacade + WorldMapRuntimeProxy + WorldMapSystem`
  - CU-12 `CharacterManagementModule`
  - CU-15 `BattleRuntimeModule`
- 当前最适合独立处理的叶子单元是：
  - CU-07 世界地图渲染
  - CU-08 据点与人物信息窗口
  - CU-09 队伍管理 / 转职 / 角色奖励窗口
  - CU-10 共享仓库 / 装备基础流转
  - CU-14 progression 规则服务
  - CU-16 战斗规则层
  - CU-18 battle 展示主链
  - CU-20 敌方静态内容
  - CU-21 headless 文本链
- 现在最容易失控的同步点是：
  - `PartyState` 字段升级
  - `warehouse_state / equipment_state / battle refresh` 三者同步
  - `GameRuntimeFacade`、`WorldMapRuntimeProxy` 与 `WorldMapSystem` 的状态同步
  - world / battle 模式切换后的统一持久化
  - `terrain_profile_id / prop_ids / board renderer` 三者一致性
- 后续做 agent 化拆分时，最稳的装载方式仍然是：
  - 1 个桥接单元
  - 加 1 到 2 个叶子单元
  - 再加 1 个当前真实存在的测试 / headless 辅助单元
