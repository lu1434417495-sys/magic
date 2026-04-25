# 当前 Godot 项目的最优上下文单元

更新日期：`2026-04-26`

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
  - `data/configs/enemies/**/*.tres`
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
      -> TrueRandomSeedService
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
              -> TrueRandomSeedService
          -> GameRuntimeBattleSelection
          -> GameRuntimeBattleSelectionState
          -> LowLuckEventService
              -> CharacterManagementModule
              -> BattleFateEventBus
          -> FortunaGuidanceService
              -> CharacterManagementModule
              -> BattleFateEventBus
          -> MisfortuneGuidanceService
              -> CharacterManagementModule
              -> BattleRuntimeModule
          -> GameRuntimeSettlementCommandHandler
              -> TrueRandomSeedService
              -> SettlementShopService
                  -> TrueRandomSeedService
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
  -> BattleFateAttackRules
  -> BattleSkillResolutionRules
  -> BattleSpawnReachabilityService
  -> BattleTerrainEffectSystem
  -> BattleRatingSystem
  -> BattleUnitFactory
  -> BattleState / BattleUnitState / BattleCellState / BattleTimelineState / BattleTerrainEffectState
  -> BattleGridService / BattleEdgeService / BattleDamageResolver / BattleHitResolver / BattleAiService / BattleAiScoreService
  -> BattleTerrainRules / BattleTerrainTopologyService
  -> BattleTerrainGenerator / EncounterRosterBuilder
  -> TrueRandomSeedService

BattleAiService
  -> BattleAiContext / BattleAiScoreInput / BattleAiScoreService
  -> EnemyTemplateDef / EnemyAiBrainDef / EnemyAiStateDef
  -> enemy actions/*

BattleMapPanel
  -> BattleHudAdapter
      -> BattleSkillResolutionRules
      -> BattleHitResolver
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
- 真相源：
  - 启动页当前交互状态。
  - 世界预设列表与新世界入口。
  - `user://display_settings.cfg`。
- 主要职责：
  - 选择新世界、现有存档、测试世界。
  - `测试地图` 入口和正式世界预设一样走 `GameSession.create_new_save()`，使用 `test_world_map_config.tres` 重新生成程序化测试世界。
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
  - `scripts/utils/true_random_seed_service.gd`
- 真相源：
  - 当前 active save id / path / meta。
  - 当前 generation config path / object。
  - 当前 `world_data`、玩家坐标、玩家 faction。
  - 旧存档或外部写入缺失 `world_data.map_seed` 时，`SaveSerializer.normalize_world_data()` 会通过 `TrueRandomSeedService` 补运行时地图 seed。
  - 当前 `PartyState`。
  - 缓存后的 `skill_defs` / `profession_defs` / `achievement_defs` / `item_defs` / `recipe_defs` / `enemy_templates` / `enemy_ai_brains`。
- 主要职责：
  - 创建新存档、读取现有存档。
  - 管理 `user://saves/index.dat` 与 slot payload，当前全局 `SAVE_VERSION = 5`。
  - 统一持久化 `world_data + party_state`。
  - `GameLogService` 现在作为开发侧 sidecar 挂在 `GameSession` 下，负责维护内存 ring buffer 并把结构化运行日志追加到 `user://logs/*.jsonl`；create/load/unload world 会显式轮转日志会话边界。它不是存档真相源，也不参与 `SAVE_VERSION`。
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
  - `scripts/utils/world_map_settlement_bundle.gd`
  - `scripts/utils/world_map_settlement_name_pool.gd`
  - `scripts/utils/world_map_wild_spawn_bundle.gd`
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
  - `data/configs/world_map/shared/main_world_default_settlement_bundle.tres`
  - `data/configs/world_map/shared/main_world_settlement_name_pool.tres`
  - `data/configs/world_map/shared/main_world_town_name_pool.tres`
  - `data/configs/world_map/shared/main_world_city_name_pool.tres`
  - `data/configs/world_map/shared/main_world_capital_name_pool.tres`
  - `data/configs/world_map/shared/main_world_metropolis_name_pool.tres`
  - `data/configs/world_map/shared/main_world_default_wild_spawn_bundle.tres`
- 真相源：
  - `demo/test/small/medium` 这类通用主世界预设只持有世界尺寸、chunk、玩家视野、程序化生成开关、程序化野怪 chunk 抽签分母、起始野外遭遇保底与数量参数。
  - 通用主世界的据点模板、设施模板、设施槽位、服务 NPC 模板由 `main_world_default_settlement_bundle.tres` 持有；默认据点实例展示名由 `main_world_settlement_name_pool.tres` 持有，`template_town` / `template_city` / `template_capital` / `template_metropolis` 分别额外优先使用 `main_world_town_name_pool.tres`、`main_world_city_name_pool.tres`、`main_world_capital_name_pool.tres`、`main_world_metropolis_name_pool.tres`；野外遭遇规则由 `main_world_default_wild_spawn_bundle.tres` 持有。
  - 世界据点通过 `SettlementConfig.tier = WORLD_STRONGHOLD` 标记，不再单独拆资源。
  - `ashen_intersection` 这类主题化世界仍可在各自 `tres` 内持有专属模板；运行时实例 id 由世界生成阶段分配。
- 主要职责：
  - 定义 world spawn 输入资源。
  - 区分“主世界参数模板”和“共享主世界内容模板”的持有边界。
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
  - `docs/design/settlement.md`
  - `scripts/systems/world_map_spawn_system.gd`
  - `scripts/systems/encounter_anchor_data.gd`
  - `scripts/utils/true_random_seed_service.gd`
  - `scripts/utils/world_event_config.gd`
  - `scripts/utils/mounted_submap_config.gd`
  - 依赖 CU-03 资源定义
- 真相源：
  - `world_data` 的生成结构。
  - `world_data.map_seed` 是当前地图实例运行时随机种子记录；正式生成时由 `TrueRandomSeedService` 通过 Godot `Crypto.generate_random_bytes()` 分配，而不是直接使用 `.tres` 配置里的固定 `seed`。
  - settlements / world_npcs / encounter_anchors / world_events / mounted_submaps / player_start_* 的输出形状。
  - settlement / facility / service npc 的实例 id 与 `template_id` 绑定关系。
  - 据点实例内部对象图与字段解释见 `docs/design/settlement.md` 第 3 节。
- 主要职责：
  - 生成固定或程序化据点。
  - 在 `inject_default_main_world_content = true` 时，把共享主世界据点/设施包、默认据点名称池、town / city / capital 专用名称池与共享野怪规则包分别注入到生成链里，再叠加当前 world config 的局部内容。
  - 按模板生成据点实例、设施实例、服务 NPC 实例与 `available_services` 绑定。
  - 注入兜底的共享仓库服务 `interaction_script_id = "party_warehouse"`。
  - 生成玩家开局位置、遭遇锚点、世界事件与挂载子地图定义；程序化野怪锚点密度由 `WorldMapGenerationConfig.procedural_wild_spawn_chunk_chance_denominator` 与 `WildSpawnRule.density_per_chunk` 共同控制。
  - 据点名称池洗牌、程序化遭遇筛选、默认据点遭遇补位等地图域随机 seed 都从 `TrueRandomSeedService` 取值；配置资源里的 `seed` 只保留为编辑器字段，不再作为正式地图随机入口。
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
  - `scripts/systems/settlement_shop_service.gd`
  - `scripts/systems/settlement_research_service.gd`
  - `scripts/systems/settlement_service_context.gd`
  - `scripts/systems/settlement_service_result.gd`
  - `scripts/systems/world_time_system.gd`
  - `scripts/utils/true_random_seed_service.gd`
  - `scenes/main/world_map.tscn`
  - `scenes/ui/runtime_log_dock.tscn`
  - `scenes/ui/submap_entry_window.tscn`
  - `scripts/systems/game_runtime_facade.gd`
  - `scripts/systems/world_map_data_context.gd`
  - `scripts/systems/battle_session_facade.gd`
  - `scripts/systems/game_runtime_battle_selection.gd`
  - `scripts/systems/game_runtime_battle_selection_state.gd`
  - `scripts/systems/game_runtime_settlement_command_handler.gd`
  - `scripts/systems/game_runtime_warehouse_handler.gd`
  - `scripts/systems/game_runtime_party_command_handler.gd`
  - `scripts/systems/game_runtime_reward_flow_handler.gd`
  - `scripts/systems/game_runtime_snapshot_builder.gd`
  - `scripts/systems/world_map_runtime_proxy.gd`
  - `scripts/systems/world_map_system.gd`
  - `scripts/ui/runtime_log_dock.gd`
  - `scripts/ui/submap_entry_window.gd`
  - `assets/main/basic_map/log.png`
- 真相源：
  - 当前 world / battle 模式。
  - 当前 modal 互斥状态。
  - 当前 battle 技能选择、目标缓存、待处理奖励、待处理晋升。
  - 当前 headless snapshot 的结构化输出。
  - 当前场景节点与 runtime 的同步方式。
- 主要职责：
  - `GameRuntimeFacade` 持有真正的世界/战斗/奖励/仓库/窗口运行时编排状态。
  - `WorldMapDataContext` 是正式的 world-data context owner：持有 root world / active map data、坐标索引查表、active map 标识 / 展示名，以及 mounted submap generation config cache，并负责把根世界结构映射成当前激活地图视图。
  - `GameRuntimeFacade` 继续编排 mounted submap 进入确认、battle start 确认、active map 切换、返回栈和统一持久化，但不再直接持有整组 world-data cache 字段。
  - `BattleSessionFacade` 承载开战、battle tick / resolve、batch 同步、战斗输入桥接、战斗只读查询与战后回写，作为 `GameRuntimeFacade -> BattleRuntimeModule` 的 battle session 控制器；战斗开始/结束链路优先通过 `GameRuntimeFacade` 的显式 battle-support helper 协调，不再散写 runtime 私有字段；terrain 生成未就绪时，`GameRuntimeFacade` 还负责挂起 pending request、维持 `battle_loading` modal，并在 world 模式下持续重试直至拿到正式 battle state。
  - `BattleSessionFacade.build_battle_seed()` 现在为每次开战调用 `TrueRandomSeedService` 分配 battle map seed；战斗内命中 d20、crit gate 与 disadvantage 底层骰也逐次调用 `TrueRandomSeedService.randi_range()`，`BattleState.attack_roll_nonce` 只记录消耗次数，不再参与复现骰序。
  - 进入战斗地图后的 TU 暂停口径只认 `BattleState.modal_state` 这类 battle 内流程状态；`character_info` 这类 runtime overlay 只阻断战斗输入，不暂停 TU；`reward` / `game_over` 属于战后流程，不再纳入 battle TU 判定域。
  - 技能命令的最终 `preview-first` 放行口径由 `BattleRuntimeModule.issue_command()` 持有；`BattleSessionFacade.issue_battle_command()` 只负责转发命令、应用 batch，并在技能确认执行后处理 selection target 清理等 session 侧副作用。
  - `GameRuntimeBattleSelectionState` 持有战斗技能选择、目标队列与选中格等运行时状态。
  - `GameRuntimeBattleSelection` 承载战斗技能选择、目标队列与相关只读查询逻辑，并通过 `GameRuntimeBattleSelectionState` 读写状态；技能选择与目标队列的运行时读写优先通过 `GameRuntimeFacade` 的显式 selection helper / state accessor 协调，不再直接散写 runtime 私有字段。
  - `GameRuntimeSettlementCommandHandler` 承载据点动作 payload 组装、据点动作执行、任务板 / 商店 / 驿站 / forge modal 分流、据点状态回写与角色奖励 payload 归并；据点窗口状态、任务板 / 商店 / forge / 驿站窗口状态、据点反馈文本、默认交互成员和据点成功动作后的奖励 / 持久化链路优先通过 `GameRuntimeFacade` 的显式 settlement helper / state accessor 协调，不再直接散写 runtime 私有字段。
  - 任务板 `submit_item` 正式目标由 `GameRuntimeSettlementCommandHandler` 只负责解析活跃 objective / provider payload，并委托 `GameRuntimeFacade -> CharacterManagementModule` 执行正式仓库扣除；不要把任务缴纳扣库规则塞回据点窗口或 handler 本体。
  - `GameRuntimeSettlementCommandHandler` 创建缺失 settlement state 时也通过 `TrueRandomSeedService` 补 `shop_inventory_seed`，避免地图运行时新状态继续落固定 seed。
  - `SettlementShopService` 承载商店库存生成、买卖结算和 shop runtime state 刷新；商店库存 `seed` 由 `TrueRandomSeedService` 分配并随 shop state 持久化，不再用 settlement/shop/world_step 哈希派生；不要把商店定价和库存更新逻辑回塞到 `WorldMapSystem` 或 `SettlementWindow`。
  - `SettlementForgeService` 承载 `RecipeDef` 装载、设施标签校验以及通过 `PartyWarehouseService` 的原子扣料 / 入仓；不要把 forge 规则回塞到 `GameRuntimeSettlementCommandHandler` 或 `GameRuntimeFacade`。
  - `GameRuntimeWarehouseHandler` 承载共享仓库窗口数据、默认目标成员解析和 `warehouse` 命令处理；仓库窗口状态、当前入口标签、默认目标成员和仓库持久化链路的运行时读写优先通过 `GameRuntimeFacade` 的显式 warehouse helper / state accessor 协调，不再直接散写 runtime 私有字段。
  - `GameRuntimeFacade` 现在也持有“普通战 calamity 结算碎片”的 chapter 级持久化闸门：`BattleRuntimeModule` 只产出 raw `calamity_conversion` / fate fixed loot 条目，是否受本章普通战上限约束、以及上限计数落在 `PartyState.fate_run_flags` 的哪几个槽位，统一由 facade 在 battle loot commit 分流时裁切与记账；不要把 chapter cap 逻辑塞回 battle runtime 或仓库服务。
  - `GameRuntimePartyCommandHandler` 承载队伍管理窗口打开、成员选择、队长切换、编成提交，以及装备 / 卸装与队伍持久化回写；队伍窗口、当前选中成员和队伍持久化链路的运行时读写优先通过 `GameRuntimeFacade` 的显式 party helper / state accessor 协调，不再直接散写 runtime 私有字段。
  - `GameRuntimeRewardFlowHandler` 承载角色奖励确认、晋升选择、reward/promotion modal 编排与待领奖励呈现时机；奖励 modal 需要继续避让据点任务板 / 商店 / forge / 驿站等互斥窗口，相关运行时读写优先通过 `GameRuntimeFacade` 的显式 reward helper / state accessor 协调，不再直接散写 runtime 私有字段。
  - `GameRuntimeSnapshotBuilder` 负责组织 headless 结构化快照，并通过 `GameRuntimeFacade` 的显式只读接口取数。
  - 初始化 `WorldMapGridSystem`、`WorldMapFogSystem`、`PartyWarehouseService`、`CharacterManagementModule`、`BattleRuntimeModule`。
  - `GameRuntimeFacade` 继续处理世界移动、battle/world 总编排、模态状态真相源与统一持久化桥接，不再直接承载队伍命令或奖励确认流程细节。
  - 战后“真实死亡”与主角死亡后的 `game_over` modal 归 `GameRuntimeFacade` 判定；不要把主角死亡判定或标题返回流程塞进 `BattleRuntimeModule` 或 `WorldMapView`。
  - 正式 `command_*` 结果日志、battle batch 战报镜像、以及供 headless / agent 消费的最近日志快照，现在从这里汇总到 `GameSession -> GameLogService`；不要把日志真相源塞回 `WorldMapSystem`。
  - mounted submap 进入确认、battle start 确认、active map 切换、任意点击返回和 submap player coord 持久化，都属于这里的正式运行时主链。
  - 维护 headless 可消费的结构化 snapshot 与状态文本。
  - `WorldMapRuntimeProxy` 负责给 `WorldMapSystem` 提供唯一正式的命令 / 读取接口，并在命令执行后统一触发渲染回调。
  - `WorldMapSystem` 负责场景树接线、输入捕获、窗口信号绑定，并把 runtime 状态渲染到 `WorldMapView` / `BattleMapPanel` / `RuntimeLogDock` / 各 modal；右侧日志框与地图视口的响应式布局也归这里维护，日志框宽度锁定、高度随窗口高度自适应，世界态会让 `MapViewport` 避让日志框，战斗态则恢复 `MapViewport` 全宽并让日志框覆盖在战斗地图上；世界态底部 `BottomActionBar` / `PartyButton` 只是打开队伍窗口的场景入口，真正命令仍走 `WorldMapRuntimeProxy.command_open_party()`；`RuntimeLogDock` 只负责自身九宫格纹理、边距、字体和日志内容呈现；据点侧现在正式包含 `SettlementWindow`、复用 `ShopWindow` shell 的 `contract_board` / `shop` / forge / `stagecoach` entry-list modal，`SubmapEntryWindow` 继续作为通用确认窗承接 submap 进入确认、battle start 确认与主角死亡后的返回标题入口；战斗键盘输入也归这里集中分流：方向键发战斗移动命令，WASD 只做战斗镜头平移，Enter 执行等待 / 结算，Space 复位 battle focus；场景层不再承担 `command_*` / snapshot 对外透传，并且需要在 `battle_loading` modal 下维持根层 `BattleLoadingOverlay` 可见。
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
  - `scenes/ui/character_info_window.tscn`
  - `scripts/ui/character_info_window.gd`
- 真相源：
  - `SettlementWindow` 的当前展示态。
  - 复用 `ShopWindow` shell 的 shop / forge / stagecoach entry-list modal 的当前展示态。
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
  - `assets/main/basic_map/log.png`
  - `scenes/ui/promotion_choice_window.tscn`
  - `scripts/ui/promotion_choice_window.gd`
  - `scenes/ui/mastery_reward_window.tscn`
  - `scripts/ui/mastery_reward_window.gd`
- 真相源：
  - 各窗口自身的选中态与展示态。
- 主要职责：
  - 展示 active / reserve roster、leader 切换。
  - `PartyManagementWindow` 现在是“人物管理”窗口：用九宫格外框、响应式内容边距和列表 / 概览 / 详情 tabs 展示成员信息。
  - 展示成员概览、最终属性快照、装备槽、已学技能、职业进度与成就摘要；显示数据由 `WorldMapSystem` 注入的 item / skill / profession defs 与本地临时 `AttributeService` snapshot 生成。
  - 展示待转职选择。
  - 展示通用 `PendingCharacterReward` 队列。
  - 只发出动作，不负责真正改 `PartyState` / progression。
- 邻接单元：
  - CU-06
  - CU-10
  - CU-11
  - CU-12
  - CU-13
  - CU-14
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
  - `data/configs/items_templates/*.tres`
  - `scripts/systems/equipment_drop_service.gd`
  - `scripts/systems/party_warehouse_service.gd`
  - `scripts/systems/party_equipment_service.gd`
  - `scripts/systems/party_item_use_service.gd`
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
  - 武器 `ItemDef.weapon_attack_range` 是武器攻击范围真相源；战斗内武器技能射程从装备进入角色 attribute snapshot 后读取，不应在技能资源里另配攻击距离。
  - `EquipmentDropService` 当前持有装备掉落稀有度公式：`3d6 + drop_luck`。
  - 当前容量 / 已用 / 超容规则。
- 主要职责：
  - 按堆栈管理共享仓库。
  - 用全队 `storage_space` 统计容量。
  - `ItemContentRegistry` 在注册阶段先扫描 `data/configs/items_templates`，再扫描 `data/configs/items`；实例若声明 `base_item_id` 会沿模板链合并（标量空回退、tags/modifiers 合并去重、equipment_slot_ids 覆盖、attribute_modifiers 深拷贝并把 source_id 重写为最终 item_id），合并产物才进入 `_item_defs`。模板自身不暴露给运行时；循环或缺失模板在校验阶段报错。
  - `RecipeContentRegistry` 负责扫描 / 校验 `RecipeDef`，供据点 forge 流读取。
  - `EquipmentDropService` 负责把调用方已 clamp 的 `drop_luck` 映射为正式装备稀有度；当前不持有掉落表内容，也不二次 clamp。
  - 战斗内随机装备掉落现在由 `BattleRuntimeModule` 在敌人死亡瞬间调用 `EquipmentDropService` 预 roll，`GameRuntimeFacade` 只负责把已解析好的 `equipment_instance` 写入共享仓库。
  - `preview_add_item` / `add_item` / `remove_item`。
  - 处理仓库与装备槽位之间的基础装备 / 卸装事务。
  - 维护武器攻击范围等装备侧战斗属性，并通过 `AttributeService` 写入角色属性快照供战斗读取。
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
  - `scripts/player/progression/quest_state.gd`
  - `scripts/player/progression/faith_deity_def.gd`
  - `scripts/player/progression/faith_rank_def.gd`
  - `scripts/systems/character_progression_delta.gd`
  - `scripts/systems/pending_character_reward.gd`
  - `scripts/systems/pending_character_reward_entry.gd`
- 真相源：
  - `PartyState` 本体。
  - 每个成员的 progression / achievements / 当前资源。
  - `PartyState.main_character_member_id` 与 `PartyMemberState.is_dead`。
  - 正式角色奖励队列 `pending_character_rewards`。
  - 正式任务状态 `active_quests` / `claimable_quests` / `completed_quest_ids`。
  - 正式角色奖励结构。
  - `PartyMemberState -> UnitBaseAttributes.get_drop_luck()` 是正式掉落幸运值来源；当前 battle loot 改为按击杀者逐次读取，`PartyState` 不再持有整场战斗共用的掉落承担者字段。
  - `UnitBaseAttributes.ACTION_THRESHOLD` / `AttributeService.ACTION_THRESHOLD` 是友军行动阈值的角色侧属性入口；角色快照缺省值为 `30 TU`，战斗单位不再只吃 battle 层默认值。
- 主要职责：
  - 定义 party / member / progression / reward 数据模型。
  - 负责模型级 `to_dict` / `from_dict`。
  - 当前 `PartyState.version = 3`，包含 `main_character_member_id`、`warehouse_state`、`pending_character_rewards`、`active_quests`、`claimable_quests`、`completed_quest_ids` 与通用周目剧情去重字典 `meta_flags`；`PartyMemberState` 额外持有 `is_dead`。
  - `PartyState` 现在不再保存任何“整场战斗共用掉落承担者”字段；战斗掉落 lucky source 的时点与归属完全下放到 battle runtime。
  - `FaithDeityDef` / `FaithRankDef` 持有 faith 阵营与阶级静态内容，与 quest / fate sidecar 的运行时读写共用这层数据模型。
  - `main_character_member_id` 现在是严格合同字段，不再对缺失/坏引用做 leader 或 roster fallback；旧 shape / 坏存档会在反序列化阶段直接失败。
  - `active_quests` / `claimable_quests` / `completed_quest_ids` 现在是互斥 bucket：同一 quest 只能存在于其中一个；模型 API 会主动迁移 bucket，反序列化遇到重叠坏 shape 会直接失败。
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
  - `scripts/systems/misfortune_black_omen_service.gd`
  - `scripts/systems/quest_progress_service.gd`
  - `scripts/systems/faith_service.gd`
- 真相源：
  - battle / settlement / headless 与 party/progression / quest state 之间的桥接规则。
- 主要职责：
  - 提供 `PartyMemberState`、attribute snapshot 与 progression 查询，作为 `BattleUnitFactory` 构建 / 刷新战斗单位时的角色侧桥接。
  - 记录成就事件。
  - 直接解锁一次性 achievement，并把无奖励的剧情门票稳定写进 `UnitProgress.achievement_progress`。
  - 生成与应用 `PendingCharacterReward`。
  - 统一使用 `PendingCharacterReward`。
  - `MisfortuneBlackOmenService` 现在持有 `doom_marked` 的受控写入口与默认黑兆 hook 样板；黑兆事件脚本应通过这里把 `doom_marked = 1` 写回角色，而不是复用 `hidden_luck_at_birth` 的 protected custom-stat 白名单链路。
  - `MisfortuneBlackOmenService` 还承接 `dead_road_lantern_black_omen_path` 这类“路径已命中黑兆”的后端 hook；亡途灯笼只在这里受控写入 `doom_marked`，路径显隐判断则继续由 `LowLuckRelicRules` 统一封装，UI 展示留给专门界面单元。
  - 通过 `QuestProgressService` 接受 `quest_progress_events`，维护 `PartyState.active_quests` / `claimable_quests` / `completed_quest_ids`，并把事件上下文写入 `QuestState.last_progress_context`。
  - `submit_item` 正式任务目标的仓库扣除与进度推进也归这里协调：先按 objective 需求预览共享仓库可提交数量，再通过 `PartyWarehouseService` 做原子扣除，只有 quest progress 成功时才提交扣除结果。
  - 处理 profession promotion、战后 hp/mp/真实死亡回写，以及死亡成员装备回收。
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
  - `SkillContentRegistry` 负责扫描 `data/configs/skills/*.tres`，并报告 skill_id、嵌套战斗资源结构与基础 schema 相关的静态错误；warrior / archer / mage 正式技能 seed 现在都以这些 SkillDef 资源为真相源，`DesignSkillCatalog` 代码承载路径已全部下线。
  - `CombatSkillDef` 的命中修正字段是 `attack_roll_bonus`；`CombatEffectDef` 不再持有攻击 / 防御属性缩放字段，伤害类型通过 `damage_tag` 表达，完全免疫、减半与易伤等承伤档位通过状态参数 `mitigation_tier` 表达，实际命中与伤害公式归 CU-16 执行。
  - `ProfessionContentRegistry` 负责扫描 `data/configs/professions/*.tres`，并报告 profession_id、技能/职业引用与 rank requirement 相关的静态错误。
  - `ProgressionContentRegistry` 负责聚合 skill/profession registry、补齐剩余未迁移的 code fallback（当前主要只剩 `charge`），并汇总 skill/profession registry 的静态校验结果。
  - Fortuna guidance 这类剧情门票 achievement 现在允许无奖励定义；正式效果由 rank gate 或剧情脚本消费，不强制进入 reward queue。
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
  - `scripts/systems/attribute_growth_service.gd`
  - `scripts/systems/character_creation_service.gd`
- 真相源：
  - progression 规则执行结果。
  - attribute modifier / snapshot 计算结果。
  - 建卡 reroll 次数到 `hidden_luck_at_birth` 的烘焙规则。
- 主要职责：
  - 学习技能与知识。
  - 授予 mastery、重算角色等级。
  - 判断职业解锁 / 升级 / active 状态。
  - 技能合并与核心技能分配。
  - 派生属性计算。
  - 满级核心技能提供的基础属性成长进度累计与转化。
  - `AttributeService` 的战斗属性真相源是 `attack_bonus`、单一展示用 `armor_class`，以及内部 AC 组件 `armor_ac_bonus / shield_ac_bonus / dodge_bonus / deflection_bonus`；旧物理 / 魔法攻击、防御、命中、闪避属性不再进入正式快照。
  - `AttributeService` 会把 `action_threshold` 纳入角色 attribute snapshot，默认角色行动阈值为 `30 TU`，并按 `5 TU` 粒度归一，供战斗时间轴直接消费。
  - `CharacterCreationService` 持有建卡 reroll → `hidden_luck_at_birth` 的纯函数映射，以及通过 `AttributeService` 写回受保护 custom stat 的最小烘焙入口。
- 邻接单元：
  - CU-11
  - CU-12
  - CU-13
  - CU-15
- 适合任务：
  - 改属性公式。
  - 改建卡出生 luck 烘焙规则。
  - 改职业规则。
  - 改技能合并。
- 不要顺手带上：
  - `scripts/ui/battle_map_panel.gd`
  - `scripts/ui/login_screen.gd`

### CU-15 战斗运行时总编排

- 文件：
  - `scripts/systems/battle_runtime_module.gd`
  - `scripts/systems/battle_session_facade.gd`
  - `scripts/systems/battle_resolution_result.gd`
  - `scripts/systems/fortune_service.gd`
  - `scripts/systems/misfortune_service.gd`
  - `scripts/systems/fortuna_guidance_service.gd`
  - `scripts/systems/misfortune_guidance_service.gd`
  - `scripts/systems/low_luck_event_service.gd`
  - `scripts/systems/low_luck_relic_rules.gd`
  - `scripts/systems/battle_charge_resolver.gd`
  - `scripts/systems/battle_repeat_attack_resolver.gd`
  - `scripts/systems/battle_skill_resolution_rules.gd`
  - `scripts/systems/battle_spawn_reachability_service.gd`
  - `scripts/systems/battle_target_collection_service.gd`
  - `scripts/systems/battle_terrain_effect_system.gd`
  - `scripts/systems/battle_rating_system.gd`
  - `scripts/systems/battle_report_formatter.gd`
  - `scripts/systems/battle_unit_factory.gd`
  - `scripts/systems/battle_unit_factory_runtime.gd`
  - `scripts/utils/true_random_seed_service.gd`
  - `scripts/systems/battle_command.gd`
  - `scripts/systems/battle_preview.gd`
  - `scripts/systems/battle_event_batch.gd`
  - `scripts/systems/battle_sim_runner.gd`
  - `scripts/systems/battle_sim_report_builder.gd`
  - `scripts/systems/battle_sim_override_applier.gd`
  - `scripts/systems/battle_sim_scenario_def.gd`
  - `scripts/systems/battle_sim_profile_def.gd`
  - `scripts/systems/battle_sim_unit_spec.gd`
- 真相源：
  - 当前 `BattleState` 生命周期。
  - 当前 active unit / phase / modal。
  - preview、event batch、battle rating 统计、战斗内技能熟练度条件触发与 post-battle reward 产出。
  - battle simulation 的 AI turn trace、全单位 battle metrics、profile override 与 report schema。
- 主要职责：
  - 开战、推进时间轴、接手 AI / manual command。
  - 战斗时间轴的行动进度按 `tu_per_tick` 固定累加到 `BattleUnitState.action_progress`；实际入队阈值必须由每个 `BattleUnitState.action_threshold` 持有，`BattleTimelineState` 不再保存全局行动阈值。
  - 预览与执行移动、单体技能、地面技能、charge、terrain effect。
  - `BattleRuntimeModule.issue_command()` 现在是 `TYPE_SKILL` 的唯一正式 `preview-first` 门禁；无论来自玩家、AI、facade 还是测试，只要命令真正落到 runtime，都先复用 `preview_command()` 的技能合法性口径。
  - `BattleChargeResolver` 负责冲锋路径推演、受阻停步、碰撞推挤、陷阱触发与路径 AOE。
  - `BattleRepeatAttackResolver` 负责 `repeat_attack_until_fail` 的逐段执行、资源消耗与日志归并，并把命中判定委托给 `BattleHitResolver`。
  - `BattleTerrainEffectSystem` 负责 timed terrain effect 的写入、推进与 tick 结算。
  - `BattleState.log_entries` 现在由 runtime 作为 battle 内正式日志 ring buffer 维护，按 `10000` 条与 `10 MiB` 双阈值自动淘汰最旧记录；不要再把 battle log 上限控制散写到 HUD 或 facade。
  - `BattleRatingSystem` 负责战斗评分统计、标签与结算奖励映射。
  - `BattleRuntimeModule` 负责把技能配置里的 `mastery_sources` 接到正式战斗事件：主动技能成功施放走 `battle`，`金刚不坏` 这类被动受击技能按 `heavy_hit_taken` / `max_damage_die_taken` / `elite_or_boss_damage_taken` 等条件即时写入角色成长，并通过 `BattleUnitFactory.refresh_known_skills()` 回灌当前战斗单位。
  - `BattleUnitFactory` 负责正式友军 / 敌军单位构建、战斗单位刷新桥接与 terrain 数据装配。
  - `BattleUnitFactory` 构建 / 刷新友军单位时从角色 attribute snapshot 读取 `action_threshold` 写入 `BattleUnitState.action_threshold`；正式主角初始默认值为 `30 TU`。
  - `BattleUnitFactory` 构建单位时继续消费 `attack_bonus`、`armor_class` 与 AC 组件属性；UI / snapshot 对外显示单一 AC，内部组件只服务装备、状态与后续规则扩展。
  - 武器技能射程由 `BattleRuntimeModule` / selection / HUD 从 `BattleUnitState.attribute_snapshot.weapon_attack_range` 读取；近战武器技能没有该属性时只保留 1 格保底，弓类旧夹具无该属性时暂时回退技能配置，正式武器内容应补 `ItemDef.weapon_attack_range`。
  - `BattleUnitFactory` 不再为 `map_size` / `cells` / spawn payload 手工拼 fallback 地图；battle terrain 正式输入只认 `battle_map_size`，`map_size` 旧 key 已废弃且不会再透传给正式 generator，`ally_spawns` / `enemy_spawns` 仅作为 generator 结果的显式覆写。
  - `BattleUnitFactoryRuntime` 是 `BattleUnitFactory` 读取角色网关、skill defs、terrain generator、grid service 与最小地表高度的显式 bridge 契约；不要再把任意 runtime 对象直接塞给 `BattleUnitFactory.setup()`。
  - `BattleSpawnReachabilityService` 负责开战摆放后的敌方出生可达性验收：每个已摆放敌方单位必须能按 `BattleGridService` 的正式 footprint / 高差 / 墙 / 地形规则抵达至少一个可攻击玩家单位的位置；`BattleRuntimeModule.start_battle()` 在非显式 `enemy_units` 的正式生成链里，于 terrain + unit placement 后调用它，失败时用稳定 terrain seed 偏移重试，最终仍失败才返回空 battle state 交给现有 battle loading/failure 链路处理。手工 `enemy_units` 夹具默认不走该验收，可用 context `validate_spawn_reachability = true` 强制启用；该服务只判断位置与技能目标可达性，不把当前 MP/stamina 等资源不足误判成地图生成失败。
  - `BattleRuntimeModule` 现在会维护 `_ai_turn_traces` 与 `_battle_metrics` 两套正式 sidecar：前者记录 AI 每回合候选动作与最终选择，后者覆盖全单位 / 全阵营的行动、施法、伤害、治疗、击杀统计；做数值实验时不要再靠日志字符串反推。
  - `BattleUnitState` 现在把“普通移动预算”和“技能 AP”拆成两条资源线：`current_move_points` 负责普通移动消耗，`current_ap` 继续只服务技能/攻击等行动；turn start 重置与 snapshot/HUD 展示都必须同步两者，不能再把普通移动回写成 AP 消耗。
  - `BattleRuntimeModule` 现在还负责把 `BattleDamageResolver` 返回的 source-side status effect 一并写进 batch，并处理血债披肩的“队友倒地返 1 AP”副作用；preview 路径必须用克隆单位，避免提前消耗黑星楔钉这类 battle-local 首次触发锁。
  - `BattleSkillResolutionRules` 现在是 battle skill 变体解析、unit/ground 路由与 fate 接入 policy 的共享真相源；`BattleRuntimeModule` 与 `BattleHudAdapter` 都必须复用它，不要再在 runtime / HUD 各写一套 skill fate 判定。
  - `FortuneService` 现在作为 `BattleRuntimeModule` 持有的战斗 sidecar，订阅 `BattleFateEventBus` 的 `critical_success_under_disadvantage` 事件，负责按 per-run 尝试锁与二次确认规则写入 `PartyState.fate_run_flags` / `fortune_marked`；不要把 Fortuna 标记逻辑塞回 `GameRuntimeFacade` 或 UI。
  - `MisfortuneService` 现在作为 `BattleRuntimeModule` 持有的 battle-local sidecar，订阅 `critical_fail / ordinary_miss` fate payload，并接收 runtime 的强 debuff / 相邻队友倒地 / 低血结束回合 / boss 相变显式 hook，维护 `calamity_by_member_id`、`reverse_fortune` 与 `black_star_brand` 的“每战首次免费 / 后续消耗 1 calamity”施法锁；这些数据不进入永久存档，只按需进入 battle snapshot。
  - `LowLuckEventService` 现在作为 `GameRuntimeFacade` 持有的剧情 sidecar，订阅 `hardship_survival / critical_fail` fate payload，并在据点休整与 battle resolution 边界把 low luck 专属事件翻译成 fixed loot / pending reward，同时把周目去重写进 `PartyState.meta_flags`；不要把这类剧情奖励再塞回 drop_luck 或命运属性写口。
  - FATE_11 固定低 luck 奖励池（逆命护符 / 黑星楔钉 / 血债披肩 / 亡途灯笼）现在也归 `LowLuckEventService` 编排：统一产出 `drop_type=item` + `drop_source_kind=low_luck_event` 的固定 loot，不参与随机装备掉落 roll，共享 flag / path helper 收口在 `LowLuckRelicRules`。
  - `FortunaGuidanceService` 现在作为 `GameRuntimeFacade` 持有的 guidance sidecar，先于 `FortuneService` 绑定同一条 `BattleFateEventBus`，把 rare fate payload 与 battle/chapter 结束回调翻译成一次性 guidance achievement；不要回退到计数型 achievement event。
  - `MisfortuneGuidanceService` 现在作为 `GameRuntimeFacade` 持有的 guidance sidecar，消费 `BattleRuntimeModule` 已有的 calamity/reason 状态、seal/boss 结算结果以及 forge canonical result，把 `misfortune_guidance_*` 写成一次性 achievement；不要再额外增加 farm 型计数事件。
  - `BattleSimRunner` 负责按 scenario/profile 批量起 battle runtime、驱动 manual policy、应用 skill/brain/action/score-profile override，并输出 JSON report + turn-trace JSONL。
  - 统计战斗评分并产出 canonical post-battle character reward。
  - 消费 `GameSession` 提供的 skill defs、enemy templates、enemy AI brains。
  - `BattleRuntimeModule` 现在会在敌方单位死亡时，按 `BattleUnitState.enemy_template_id -> EnemyTemplateDef.drop_entries` 逐次生成 loot：固定材料直接累积到 `_active_loot_entries`，随机装备按击杀者 `drop_luck` 立刻 roll 成 `equipment_instance`；`BattleResolutionResult.loot_entries` 只承接这些已解析好的 per-kill 掉落与 fate bonus loot。普通战未消耗 calamity 仍先产出 raw `calamity_conversion` 条目，chapter 级普通战 shard 上限不在 runtime 内裁切。
  - `BattleUnitState.enemy_template_id` 现在是 battle 内掉落归因的正式 bridge；如果改敌方构建链路，必须保证这个字段仍能稳定映射回 `EnemyTemplateDef`。
  - `GameRuntimeFacade` 不再决定随机装备该读谁的 luck；它只负责提交 battle runtime 已经解析好的 fixed item / `equipment_instance` / 仍需 chapter-cap 裁切的固定 fate loot。
  - `BattleRuntimeModule.start_battle()` 现在统一通过 `BattleUnitFactory` 构建友军 / 敌军 / terrain，并在实际摆放后通过 `BattleSpawnReachabilityService` 验证敌方出生点最终能抵达攻击玩家的位置；`CharacterManagementModule` 不再直接产出 `BattleUnitState`。
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
  - `scripts/systems/battle_status_effect_state.gd`
  - `scripts/systems/battle_terrain_effect_state.gd`
  - `scripts/systems/battle_edge_face_state.gd`
  - `scripts/systems/battle_edge_feature_state.gd`
  - `scripts/systems/battle_terrain_rules.gd`
  - `scripts/systems/battle_terrain_topology_service.gd`
  - `scripts/systems/battle_grid_service.gd`
  - `scripts/systems/battle_edge_service.gd`
  - `scripts/systems/battle_fate_event_bus.gd`
  - `scripts/systems/battle_fate_attack_rules.gd`
  - `scripts/systems/fate_attack_formula.gd`
  - `scripts/systems/battle_damage_resolver.gd`
  - `scripts/systems/battle_status_semantic_table.gd`
  - `scripts/systems/battle_hit_resolver.gd`
  - `scripts/utils/true_random_seed_service.gd`
  - `scripts/systems/battle_ai_context.gd`
  - `scripts/systems/battle_ai_score_input.gd`
  - `scripts/systems/battle_ai_score_profile.gd`
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
  - `BattleDamageResolver` 现已正式切到“攻击侧倍率一次聚合取整 -> `IMMUNE / HALF / NORMAL / DOUBLE` -> 固定值减伤 -> 护盾后吸收”的 M1 流水线；元素抗性不再是人物主属性派生值，完全免疫、减半与易伤一律通过状态参数 `mitigation_tier` 结算。`damage_reduction_up`、`guarding` 不再按旧百分比链解释。`black_star_brand_elite` 的“禁暴击 / 首次受击穿透部分格挡”也在这层消费，不要把这一击穿透逻辑回写到 skill resource。
  - `BattleDamageResolver` 不再从攻击 / 防御属性计算基础伤害；技能伤害以 `CombatEffectDef.power` 为基础，再进入倍率、承伤档位、固定减伤、护盾流水线，伤害分类由 `damage_tag` 提供。
  - `BattleDamageResolver` 现在还消费 `LowLuckRelicRules` 的 battle-local 遗物逻辑：逆命护符会把首次 `critical_fail` 降级为普通 miss 并施加 2 回合输出下降，黑星楔钉会在首击忽视部分 guard 后于未击杀时给自己挂 1 回合 exposed，血债披肩会在低血时减伤；这些 battle-local 首次触发锁不要散写到 skill / item resource。
- `BattleFateAttackRules` 现在是 battle attack roll 语义的共享 helper，负责统一“命中线 / 封暴击状态 / d20 是否命中”的纯判定；`BattleDamageResolver` 与 `BattleHitResolver` 都必须复用它，避免 runtime 执行与 preview/AI 评分再次分叉。
- `BattleDamageResolver` 现在还持有 battle-local `BattleFateEventBus`，并在攻击结算后派发只读 fate payload（如 `critical_fail / critical_success_under_disadvantage / ordinary_miss / hardship_survival`）；`FortuneService` 已通过该 payload 订阅 Fortuna 标记逻辑，后续 Faith / 剧情系统继续沿用这条 bus，而不是拿运行时对象直接改命运属性。
  - `BattleDamageResolver` 现在还会额外派发 `high_threat_critical_hit`，并在 payload 中补出低血 hardship / 强 debuff 快照，供 `FortunaGuidanceService` 直接判定 devout / exalted 条件，而不是另起 battle 计数器。
  - `BattleDamageResolver` 的攻击结算骰不接受测试注入 RNG，也不从 `BattleState.seed` 派生；crit gate、命中 d20 与 disadvantage 的第二颗底层骰都会逐次调用 `TrueRandomSeedService.randi_range()`。
  - `resolve_fall_damage()` / `resolve_collision_damage()` 现在也复用同一 damage contract，正式返回 `damage`、`shield_absorbed`、`shield_broken` 与 `damage_events`；不要再把环境伤害 helper 当成只回裸整数的旧接口。
  - `BattleUnitState` 现在直接持有 `current_shield_hp / shield_max_hp / shield_duration / shield_family / shield_source_*` 等护盾字段；`BattleRuntimeModule` 负责技能写入 / 刷新护盾；护盾等效果骰逐次调用 `TrueRandomSeedService.randi_range()`，不再维护效果骰游标或可复现种子；`BattleDamageResolver` 只负责消费护盾。
- `BattleHitResolver` 负责当前命中率合成、真随机命中掷骰，以及普通单体攻击 / repeat-attack 的 fate-aware 成功率预览口径；当前命中线为 `d20 + attack_bonus + skill/situational bonus >= target armor_class`，高位大成功自动命中和低幸运门骰带来的额外成功率也在这里统一并入 HUD / AI 评分，不要再让 UI 或 AI 各自重算。
  - `BattleAiScoreService` 现在同时负责技能候选与 move / retreat / wait 的正式评分输入包，并通过 `BattleAiScoreProfile` 承接权重、bucket priority 与 action base score；不要再把局部 magic number 散写回各个 action。
  - `UseChargeAction` 现在按“位移型技能”参与评分，并按实际冲锋位移长度抬高基础分，避免在普通移动改为消耗 `current_move_points` 后被一步走位稳定压掉。
  - `BattleAiContext` 同时负责暴露 battle 内强制目标语义（当前是 `taunted`），`BattleAiService` 的状态解析与 `EnemyAiAction` 的正式 target selector 必须共用这条入口，不能各自散写一套目标改写逻辑。
  - `BattlePreview` 对会位移施法者的技能会补出 `resolved_anchor_coord`；`BattleChargeResolver.validate_charge_command()` 与 AI 评分都必须共用这条正式 preview 真相源，而不是再按目标格手推“理论落点”。
  - `BattlePreview` 对移动类指令现在还会补出 `move_cost`；`MoveToRangeAction / RetreatAction / WaitAction` 的正式 score input 必须共用这条 runtime preview 口径，而不是再各自手写距离分。
  - `BattleAiContext / EnemyAiAction / BattleAiService` 现在共同维护 action trace：每个 action 至少要能解释 blocked reason、preview reject、top candidates 与最终 chosen decision，供后续自动分析改 skill 数值和 AI 评分逻辑。
  - `UseUnitSkillAction / UseGroundSkillAction` 现在必须显式声明 `desired_min_distance / desired_max_distance / distance_reference`；`BattleAiScoreService` 不再对缺省距离语义做“越近越好”的隐式补全，带内站位默认等价，战术距离只允许由 action/content 明确表达。
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
  - `scripts/systems/wild_encounter_growth_system.gd`
  - `scripts/utils/battle_board_prop_catalog.gd`
- 真相源：
  - battle terrain profile id。
  - encounter -> enemy units / map / prop_ids / loot 预览摘要的构建规则。
- 主要职责：
  - 生成 `default` / `canyon` 地形。
  - 维护 canyon 的高度、terrain、wall、spawn、objective marker / tent / torch / spike barricade。
  - 生成阶段会先标记水体区域，再通过 `BattleTerrainTopologyService` 归类成 `shallow_water / flowing_water / deep_water`。
  - 为 roster / template 型遭遇生成基于 `EnemyTemplateDef.drop_entries` 的 loot 预览摘要，供 headless / 测试快速判断该遭遇是否有正式战利品。
  - `EncounterRosterBuilder` 现在只负责“预览摘要”和“敌方单位构建”，不再生产 battle resolution 直接消费的正式掉落条目。
  - 维护 prop id 枚举与排序优先级。
- 说明：
  - 当前正式 battle start 由 `BattleRuntimeModule.start_battle()` 调 `BattleTerrainGenerator.generate()`；`GameRuntimeFacade` 会在 world 模式下持有 pending battle-start request，并在 generator 返回空结果时维持 `battle_loading` modal、持续重试，直到拿到正式 terrain 数据。
  - `WildEncounterRosterDef` 现在只描述成长阶段、单位编成和压制参数；不要再把掉落写回 roster 资源。
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
  - `BattleMapPanel` 负责 HUD 容器、技能槽、viewport 事件转发、战斗首帧加载状态与黑底子视口；内置移动复位 / 变体切换 / 清技能 / 结算按钮已下线，完整战斗日志滚动窗现在由根层共享的 `RuntimeLogDock` 承接。
  - `BattleHudAdapter` 把 `BattleState` 转成 HUD snapshot，并复用 `BattleSkillResolutionRules` 产出技能 fate / variant 相关预览；当前 focus/queue/resource snapshot 会同时暴露 `AP` 与 `行动`（`move_current/move_max`）两套资源，但不再生成旧 hint / command / battle-log 文本字段。
  - `BattleBoard2D` 负责 viewport 坐标、滚轮缩放、中键拖拽、键盘平移和 focus；键盘平移入口由 `WorldMapSystem -> BattleMapPanel.pan_battle_camera() -> BattleBoard2D.pan_viewport_direction()` 串接。
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
  - `tests/equipment/run_equipment_drop_service_regression.gd`
  - `tests/warehouse/run_party_warehouse_regression.gd`
  - `tests/warehouse/run_item_recipe_schema_regression.gd`
  - `tests/warehouse/run_item_template_inheritance_regression.gd`
  - `tests/world_map/runtime/run_settlement_forge_service_regression.gd`
  - `tests/battle_runtime/run_battle_runtime_smoke.gd`
  - `tests/battle_runtime/run_battle_runtime_ai_regression.gd`
  - `tests/battle_runtime/run_battle_board_regression.gd`
  - `tests/battle_runtime/run_battle_spawn_reachability_regression.gd`
  - `tests/battle_runtime/run_battle_resolution_contract_regression.gd`
  - `tests/battle_runtime/run_battle_state_disadvantage_regression.gd`
  - `tests/battle_runtime/run_battle_unit_factory_regression.gd`
  - `tests/battle_runtime/run_battle_skill_protocol_regression.gd`
  - `tests/battle_runtime/run_battle_ui_regression.gd`
  - `tests/battle_runtime/run_battle_loot_drop_luck_regression.gd`
  - `tests/battle_runtime/run_battle_6v40_headless_benchmark.gd`
  - `tests/battle_runtime/run_battle_panel_full_refresh_benchmark.gd`
  - `tests/battle_runtime/run_status_effect_semantics_regression.gd`
  - `tests/battle_runtime/run_warrior_skill_semantics_regression.gd`
  - `tests/battle_runtime/run_warrior_advanced_skill_regression.gd`
  - `tests/battle_runtime/run_archer_skill_semantics_regression.gd`
  - `tests/battle_runtime/run_fate_attack_formula_regression.gd`
  - `tests/battle_runtime/run_fate_calamity_drop_regression.gd`
  - `tests/battle_runtime/run_fate_low_luck_tactical_skills_regression.gd`
  - `tests/battle_runtime/run_low_luck_relic_regression.gd`
  - `tests/battle_runtime/run_misfortune_service_regression.gd`
  - `tests/battle_runtime/run_black_star_brand_regression.gd`
  - `tests/battle_runtime/run_crown_break_regression.gd`
  - `tests/battle_runtime/run_doom_sentence_regression.gd`
  - `tests/battle_runtime/run_game_runtime_battle_selection_regression.gd`
  - `tests/battle_runtime/run_save_index_resilience_regression.gd`
  - `tests/battle_runtime/run_wild_encounter_regression.gd`
  - `tests/battle_runtime/run_battle_simulation_regression.gd`
  - `tests/battle_runtime/run_battle_ai_vs_ai_simulation_regression.gd`
  - `tests/battle_runtime/run_battle_balance_simulation.gd`
  - `tests/battle_runtime/capture_canyon_battle_board.gd`
  - `tests/progression/run_progression_tests.gd`
  - `tests/progression/run_character_creation_service_regression.gd`
  - `tests/progression/run_quest_schema_regression.gd`
  - `tests/progression/run_skill_schema_regression.gd`
  - `tests/progression/run_profession_schema_regression.gd`
  - `tests/progression/run_luck_getter_regression.gd`
  - `tests/progression/run_protected_custom_stat_regression.gd`
  - `tests/progression/run_party_state_fate_regression.gd`
  - `tests/progression/run_fortune_service_regression.gd`
  - `tests/progression/run_fortuna_guidance_regression.gd`
  - `tests/progression/run_misfortune_black_omen_regression.gd`
  - `tests/progression/run_misfortune_guidance_regression.gd`
  - `tests/progression/run_low_luck_event_service_regression.gd`
  - `tests/progression/run_faith_service_regression.gd`
  - `tests/runtime/run_game_log_service_regression.gd`
  - `tests/runtime/run_game_runtime_quest_progress_regression.gd`
  - `tests/runtime/run_game_runtime_reward_flow_regression.gd`
  - `tests/runtime/run_game_runtime_snapshot_builder_regression.gd`
  - `tests/runtime/run_bootstrap_session_regression.gd`
  - `tests/runtime/run_battle_permadeath_regression.gd`
  - `tests/runtime/run_save_serializer_quest_round_trip_regression.gd`
  - `tests/runtime/content_validation_runner.gd`
  - `tests/runtime/run_resource_validation_regression.gd`
  - `tests/runtime/run_settlement_service_result_regression.gd`
  - `tests/text_runtime/run_text_command_regression.gd`
  - `tests/text_runtime/run_text_command_parse_regression.gd`
  - `tests/text_runtime/run_headless_game_test_session_regression.gd`
  - `tests/text_runtime/run_submap_text_command_regression.gd`
  - `tests/text_runtime/run_text_save_load_regression.gd`
  - `tests/text_runtime/run_validation_text_surface_regression.gd`
  - `tests/text_runtime/run_text_command_script.gd`
  - `tests/text_runtime/run_text_command_repl.gd`
  - `tests/text_runtime/scenarios/smoke_startup.txt`
  - `tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.gd`
  - `tests/world_map/runtime/run_game_runtime_party_command_handler_regression.gd`
  - `tests/world_map/runtime/run_game_runtime_reward_flow_handler_regression.gd`
  - `tests/world_map/runtime/run_world_map_battle_loading_overlay_regression.gd`
  - `tests/world_map/runtime/run_world_map_battle_start_confirm_regression.gd`
  - `tests/world_map/runtime/run_world_map_input_routing_regression.gd`
  - `tests/world_map/runtime/run_world_map_settlement_entry_regression.gd`
  - `tests/world_map/runtime/run_world_map_system_surface_regression.gd`
  - `tests/world_map/runtime/run_world_map_view_color_config_regression.gd`
  - `tests/world_map/runtime/run_world_submap_regression.gd`
  - `tools/build_battle_sim_analysis_packet.py`
  - `tools/character_creation_reroll_simulation.gd`
  - `.codex/skills/battle-sim-analysis/SKILL.md`
- 执行约束：
  - 常规“全量测试”只覆盖确定性 headless 回归 / smoke / schema / runtime contract；不要把数值模拟、平衡模拟或交互式开发入口混进默认测试集合。
  - `tests/battle_runtime/run_battle_simulation_regression.gd`、`tests/battle_runtime/run_battle_ai_vs_ai_simulation_regression.gd` 与 `tests/battle_runtime/run_battle_balance_simulation.gd` 属于 battle simulation / balance analysis 入口，不作为常规测试用例；只有用户明确要求 battle simulation、数值模拟、AI 对战模拟或平衡分析时才运行。
  - `tests/text_runtime/run_text_command_repl.gd` 是开发 REPL，不作为自动化全量测试入口。
- 主要职责：
  - equipment：
    - 装备种子内容校验
    - 装备 / 卸装与共享仓库联动
    - 装备属性进入角色快照
    - `PartyState` round-trip 后保留装备状态
    - `EquipmentDropService` 的 3d6 掉落稀有度阈值与 `drop_luck` 极值边界
  - warehouse：
    - 当前 schema 严格校验
    - 堆叠 / 容量 / 超容规则
    - save round-trip
    - 队伍入口与据点入口打开共享仓库
  - battle runtime smoke：
    - terrain effect 时序
    - 真堆叠列移动规则
    - 墙 / 高差阻挡
    - move command 扣行动点（`current_move_points`）且不误扣 AP，并完成占位更新
    - height delta 与 column cache 同步
  - battle spawn reachability：
    - `BattleSpawnReachabilityService` 脚本存在性
    - 深水完全隔断时敌方出生点判定 invalid 并返回可定位 details
    - 平地直连时敌方出生点判定 valid
  - battle status semantics：
    - `status_effects` 的 stack / duration / tick 语义回归
    - `burning / slow / staggered` 的正式 runtime 口径
    - battle smoke 对 self-buff / target-debuff 的持续时间与 `BattleUnitState` 序列化回归
  - battle simulation：
    - scenario/profile 批量模拟链路
    - AI turn trace / chosen score input / comparison report 稳定性
    - skill / AI profile override 对最终 summary 的可观测差异
    - ally/enemy 双边 AI 对战场景回归
    - full report -> low-token analysis packet 导出
    - focus trace 切片与 LLM handoff 工作流
  - battle resolution contract：
    - `BattleResolutionResult` round-trip
    - battle end 构造 canonical result
    - `BattleSessionFacade -> GameRuntimeFacade` 的 canonical handoff
    - canonical battle resolution result handoff
    - per-kill fixed loot / `equipment_instance` 的 battle-end 提交链路
  - battle board：
    - canyon 生成稳定性
    - layer / cliff / prop / unit 排序契约
    - `InputLayer.local_to_map()` 点击映射
    - 同 state 渲染签名稳定
  - progression：
    - achievement registry seed 校验
    - 建卡 reroll -> `hidden_luck_at_birth` 档位边界与溢出回退
    - profession / combat skill seed 内容
    - member-scoped achievement progress
    - pending character reward 顺序与序列化
    - character reward 序列化与展示
  - runtime：
    - `GameLogService` ring buffer 与 jsonl 追加写盘
    - `SettlementServiceResult` canonical 契约
    - battle / quest runtime 桥的最小回写链
    - runtime snapshot / text snapshot 的日志分段稳定性
  - resource validation runner：
    - 统一汇总 profession / skill / enemy / item / recipe / quest 的静态校验输出
    - 覆盖 missing id / duplicate id / invalid reference 三类夹具回归
    - 输出按 domain 分组，便于 headless 回归与本地排障直接复用
  - text runtime：
    - `game new/load`
    - `world/party/settlement/warehouse/battle/reward/promotion/close/snapshot/expect`
    - headless snapshot 与文本快照稳定性
    - `validation` 结构化快照 / `[VALIDATION]` 文本分段，以及不依赖日志抓取的校验失败断言面
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
  - `scripts/enemies/enemy_content_seed.gd`
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
  - `data/configs/enemies/enemy_content_seed.tres`
  - `data/configs/enemies/brains/*.tres`
  - `data/configs/enemies/templates/*.tres`
  - `data/configs/enemies/rosters/*.tres`
- 真相源：
  - `enemy_template_id -> EnemyTemplateDef`
  - `brain_id -> EnemyAiBrainDef`
  - `encounter_profile_id -> WildEncounterRosterDef`
  - `EnemyTemplateDef.drop_entries` 作为敌方单位 per-kill 掉落配置真相源。
  - 各状态下的 action 顺序、目标选择与距离策略。
- 主要职责：
  - 读取 `data/configs/enemies/enemy_content_seed.tres`，把正式 enemy template / AI brain / wild encounter roster 的资源清单装配进 registry。
  - 兼容测试夹具通过目录扫描临时覆写 seed 输入，但正式运行链不再把敌方 seed 硬编码在 registry 脚本内。
  - 在 registry 阶段校验 template -> brain、template.drop_entries、roster stage -> template 等静态引用。
  - `EnemyTemplateDef` 同时承担“这个敌人怎么打”和“这个敌人死后掉什么”的静态作者职责；`WildEncounterRosterDef` 只负责回答“这一场会出现哪些敌人、各几只”。
  - `EnemyTemplateDef` 现在同时拥有敌方六维来源：非野兽模板必须显式提供 `base_attribute_overrides`，带 `beast` 标签的模板在 `EncounterRosterBuilder` 入场时按 deterministic `5D3-1` 生成六维，然后再叠加 `attribute_overrides` 写出正式战斗面板。
  - `EnemyTemplateDef.attribute_overrides` 继续使用 `attack_bonus`、`armor_class` 与可选 AC 组件属性描述战斗面板；不要再写旧攻击 / 防御 / 命中 / 闪避属性。
  - 定义近战压迫型、远程控制型等默认行为树形态。
  - 为 `BattleRuntimeModule / BattleAiService` 提供静态敌方内容。
  - `EnemyAiAction` 的 `nearest_enemy / lowest_hp_enemy` 等正式 target selector 只负责排序候选，不拥有独立的 taunt 语义真相源；battle 内强制目标一律从 `BattleAiContext` 读取。
  - `UseUnitSkillAction` 的 `distance_reference` 当前支持 `target_unit / enemy_frontline`；`UseGroundSkillAction` 当前支持 `target_coord / enemy_frontline`。不要再依赖 score service 默认距离带推断站位。
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
  - 用 `GameTextSnapshotRenderer` 渲染稳定文本快照；当前快照已正式包含 `validation` 段与 `[VALIDATION]` 文本分段、`logs` 段与 `[LOG]` 文本分段、`game_over` 段与 `[GAME_OVER]` 文本分段，以及据点 `contract_board` / forge modal 的 `[CONTRACT_BOARD]` / `[FORGE]` 分段，供自动化 / agent / 人工排障读取结构化校验摘要、最近运行日志、主角死亡上下文与服务窗口状态。
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
| `R03` | `scripts/systems/world_map_system.gd` / `scripts/ui/shop_window.gd` / `scripts/ui/settlement_window.gd` | 旧 UI payload `cost_text` alias；正式字段只保留 `cost_label` |
| `R04` | `scripts/systems/battle_state_factory.gd` | 旧 `BattleStateFactory` sidecar / fallback builder |
| `R05` | `scripts/systems/settlement_window_system.gd` / `scenes/main/world_map.tscn` | 旧 `SettlementWindowSystem` sidecar 与场景节点 |
| `R06` | `scripts/systems/save_serializer.gd` | 旧 save meta 默认值回填、旧 `Vector2i` 表达（`Vector2` / `Array`）与旧 save index entry 形状 |
| `R07` | `scripts/systems/game_session.gd` | 旧单文件存档 `user://world_map_state.dat` 清理入口 |
| `R08` | `scripts/systems/attribute_service.gd` / battle skill 与 enemy/item configs | 旧 `physical_attack / magic_attack / physical_defense / magic_defense / hit_rate / evasion` 战斗属性 schema |
| `R09` | `scripts/systems/game_runtime_facade.gd` / text runtime log snapshots | 旧命令日志 `context.after` 后态 alias，正式字段只保留 `context.runtime` |
| `R08` | `scripts/systems/game_session.gd` | 旧仓库装备堆叠格式的显式拒绝兼容分支 |
| `R09` | `scripts/player/progression/party_state.gd` | 旧 `PartyState` 缺字段补全与 `version` 下限归一 |
| `R10` | `scripts/player/equipment/equipment_state.gd` / `scripts/player/equipment/equipment_entry_state.gd` | 旧顶层槽位映射、`set_equipped_item()` 与缺 `occupied_slot_ids` 装备数据 |
| `R11` | `scripts/systems/world_map_fog_system.gd` | 旧 fog `Dictionary -> WorldMapFogFactionState` 自动迁移 |
| `R12` | `scripts/systems/save_serializer.gd` | `world_preset_name` 缺失时按 `generation_config_path` 回填 fallback preset name |
| `R13` | `scripts/enemies/enemy_content_registry.gd` / `scripts/enemies/enemy_template_def.gd` | 敌人模板 `alias_ids` / `display_name` alias 查找 |
| `R14` | `scripts/systems/encounter_roster_builder.gd` | `monster_name` / `monster_display_name` 作为候选模板 ID 的兼容查找 |
| `R15` | `scripts/systems/battle_terrain_rules.gd` | legacy `water -> deep_water` 归一映射 |
| `R16` | `scripts/systems/battle_ai_service.gd` | 缺少 brain 时的旧版 AI 决策 `_choose_legacy_command()` |
| `R17` | `scripts/systems/battle_terrain_generator.gd` / `scripts/systems/game_runtime_facade.gd` | terrain 生成返回空结果时保留 `battle_loading` pending 并持续重试 |
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
  - CU-06 `GameRuntimeFacade + WorldMapDataContext + WorldMapRuntimeProxy + WorldMapSystem`
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
