# 当前 Godot 项目的最优上下文单元

更新日期：`2026-04-13`

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
  -> WorldPresetRegistry
  -> WorldMapSpawnSystem
  -> ProgressionSerialization
  -> ProgressionContentRegistry
  -> ItemContentRegistry
  -> EnemyContentRegistry

WorldMapSystem
  -> GameRuntimeFacade
      -> WorldMapGridSystem / WorldMapFogSystem
      -> SettlementWindowSystem
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
  -> BattleState / BattleUnitState / BattleCellState / BattleTimelineState / BattleTerrainEffectState
  -> BattleGridService / BattleEdgeService / BattleDamageResolver / BattleAiService
  -> BattleTerrainGenerator / EncounterRosterBuilder

BattleAiService
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
- 当前最重的桥接单元已经从单一 `WorldMapSystem` 变成两层：
  - `GameSession` 负责全局持久化与内容缓存。
  - `GameRuntimeFacade` 负责世界/战斗/奖励/窗口状态的核心运行时。
  - `WorldMapSystem` 主要承担场景节点接线与 UI 渲染同步。
- `BattleMapPanel -> BattleBoard2D -> BattleBoardController` 仍是当前正式战斗展示主链。
- `PartyState.pending_character_rewards` 是当前正式奖励队列；`pending_mastery_rewards` 仍然存在，但只作为兼容输入 / 过渡 payload。
- 共享仓库现在是独立单元，不再只是队伍窗口的附属功能。
- `PartyEquipmentService` 已经是正式 bridge，不应再把“装备基础流转”误判为纯 UI 或纯仓库任务。
- 敌方模板与 AI brain 已经是单独的内容单元，不应再塞进 battle runtime 文件里顺手改。
- headless / text runtime 已经是正式辅助链路，不是临时测试脚本。
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
  - `scripts/systems/progression_serialization.gd`
- 真相源：
  - 当前 active save id / path / meta。
  - 当前 generation config path / object。
  - 当前 `world_data`、玩家坐标、玩家 faction。
  - 当前 `PartyState`。
  - 缓存后的 `skill_defs` / `profession_defs` / `achievement_defs` / `item_defs` / `enemy_templates` / `enemy_ai_brains`。
- 主要职责：
  - 创建新存档、读取现有存档、读取 bundled save。
  - 管理 `user://saves/index.dat` 与 slot payload，当前全局 `SAVE_VERSION = 5`。
  - 统一持久化 `world_data + party_state`。
  - 提供 progression / item / enemy 内容注册表的统一访问口。
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
  - 依赖 CU-03 资源定义
- 真相源：
  - `world_data` 的生成结构。
  - settlements / world_npcs / encounter_anchors / player_start_* 的输出形状。
- 主要职责：
  - 生成固定或程序化据点。
  - 生成设施、服务 NPC、available services。
  - 注入兜底的共享仓库服务 `interaction_script_id = "party_warehouse"`。
  - 生成玩家开局位置与遭遇锚点。
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
  - `scenes/main/world_map.tscn`
  - `scripts/systems/game_runtime_facade.gd`
  - `scripts/systems/world_map_system.gd`
- 真相源：
  - 当前 world / battle 模式。
  - 当前 modal 互斥状态。
  - 当前 battle 技能选择、目标缓存、待处理奖励、待处理晋升。
  - 当前 headless snapshot 的结构化输出。
  - 当前场景节点与 runtime 的同步方式。
- 主要职责：
  - `GameRuntimeFacade` 持有真正的世界/战斗/奖励/仓库/窗口运行时状态。
  - 初始化 `WorldMapGridSystem`、`WorldMapFogSystem`、`SettlementWindowSystem`、`PartyWarehouseService`、`CharacterManagementModule`、`BattleRuntimeModule`。
  - 处理世界移动、开战、战后回写、存档落盘。
  - 维护 headless 可消费的结构化 snapshot 与状态文本。
  - `WorldMapSystem` 负责场景树接线、窗口信号绑定、把 runtime 状态渲染到 `WorldMapView` / `BattleMapPanel` / 各 modal。
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
  - 格子背景、迷雾、据点、NPC、遭遇点、玩家、选中框绘制。
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
  - `scripts/systems/settlement_window_system.gd`
  - `scenes/ui/character_info_window.tscn`
  - `scripts/ui/character_info_window.gd`
- 真相源：
  - settlement id -> window_data 转换结果。
  - 据点窗口当前展示态。
  - 人物信息窗口上下文。
- 主要职责：
  - 展示设施、服务、服务 NPC、服务按钮。
  - 展示 world / battle 单位详情。
  - 执行据点动作并返回 message + 兼容形状的 `pending_mastery_rewards` payload。
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
  - `scripts/player/warehouse/warehouse_state.gd`
  - `scripts/player/warehouse/warehouse_stack_state.gd`
  - `scripts/systems/party_warehouse_service.gd`
  - `scripts/systems/party_equipment_service.gd`
  - `scenes/ui/party_warehouse_window.tscn`
  - `scripts/ui/party_warehouse_window.gd`
  - `data/configs/items/bronze_sword.tres`
  - `data/configs/items/leather_jerkin.tres`
  - `data/configs/items/scout_charm.tres`
  - `data/configs/items/healing_herb.tres`
  - `data/configs/items/iron_ore.tres`
- 真相源：
  - 物品定义与 `item_id -> ItemDef`。
  - 仓库堆栈状态。
  - 装备槽位状态与固定槽位规则。
  - 当前容量 / 已用 / 超容规则。
- 主要职责：
  - 按堆栈管理共享仓库。
  - 用全队 `storage_space` 统计容量。
  - `preview_add_item` / `add_item` / `remove_item`。
  - 处理仓库与装备槽位之间的基础装备 / 卸装事务。
  - 仓库列表、详情、丢弃单件 / 全部。
- 说明：
  - 当前装备仍以静态 `item_id` 流转为基线；延后的耐久与实例化草案见 `docs/design/equipment_durability_plan.md`。
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
  - `scripts/systems/pending_mastery_reward.gd`
  - `scripts/systems/pending_mastery_reward_entry.gd`
- 真相源：
  - `PartyState` 本体。
  - 每个成员的 progression / achievements / 当前资源。
  - 正式角色奖励队列 `pending_character_rewards`。
  - legacy mastery reward 兼容结构。
- 主要职责：
  - 定义 party / member / progression / reward 数据模型。
  - 负责模型级 `to_dict` / `from_dict`。
  - 当前 `PartyState.version = 2`，包含 `warehouse_state` 与 `pending_character_rewards`。
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
- 真相源：
  - battle 与 party/progression 之间的桥接规则。
- 主要职责：
  - 从 `PartyState` 构建 `BattleUnitState`。
  - 基于当前装备结果刷新 attribute snapshot 与战斗单位技能表。
  - 记录成就事件。
  - 生成与应用 `PendingCharacterReward`。
  - 兼容 `PendingMasteryReward -> PendingCharacterReward` 的归一化。
  - 处理 profession promotion、战后 hp/mp/ko 回写。
  - 未来若存在战斗内装备损坏或耐久归零后的属性变化，也必须通过这里把 party 与 battle 单位重新对齐。
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
- 不要顺手带上：
  - `scripts/ui/world_map_view.gd`
  - `scripts/ui/battle_board_2d.gd`

### CU-13 progression 内容定义、条件模型、seed 内容

- 文件：
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
  - `scripts/player/progression/attribute_modifier.gd`
  - `scripts/player/progression/derived_attribute_rule.gd`
  - `scripts/player/progression/achievement_def.gd`
  - `scripts/player/progression/achievement_reward_def.gd`
  - `scripts/player/progression/progression_content_registry.gd`
  - `scripts/player/progression/progression_data_utils.gd`
- 真相源：
  - 技能、职业、条件、修正器、achievement 的静态定义与 seed 内容。
- 主要职责：
  - 定义 progression 语义，不直接执行业务流程。
  - 注册当前职业原型、战斗技能、施法变体、achievement seed 内容。
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
  - 统计战斗评分并产出兼容形状的 post-battle mastery reward。
  - 消费 `GameSession` 提供的 skill defs、enemy templates、enemy AI brains。
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
  - `scripts/systems/battle_grid_service.gd`
  - `scripts/systems/battle_edge_service.gd`
  - `scripts/systems/battle_damage_resolver.gd`
  - `scripts/systems/battle_ai_context.gd`
  - `scripts/systems/battle_ai_decision.gd`
  - `scripts/systems/battle_ai_service.gd`
- 真相源：
  - battle cells / columns / units / occupancy / height / terrain effect / edge feature / timeline。
- 主要职责：
  - 提供 battle 纯规则层 API。
  - 处理 footprint、移动、墙边 / 高差 / 占位规则。
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
  - `scripts/systems/battle_state_factory.gd`
  - `scripts/utils/battle_board_prop_catalog.gd`
- 真相源：
  - battle terrain profile id。
  - encounter -> enemy units / map / prop_ids 的构建规则。
- 主要职责：
  - 生成 `default` / `canyon` 地形。
  - 维护 canyon 的高度、terrain、wall、spawn、objective marker / tent / torch / spike barricade。
  - 提供 `BattleStateFactory` 这类 sidecar / fallback builder。
  - 维护 prop id 枚举与排序优先级。
- 说明：
  - 当前正式 battle start 由 `BattleRuntimeModule.start_battle()` 调 `BattleTerrainGenerator.generate()`。
  - `battle_state_factory.gd` 仍在仓库中，但更偏 sidecar / fallback。
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
  - `tests/battle_runtime/run_battle_runtime_smoke.gd`
  - `tests/battle_runtime/run_battle_board_regression.gd`
  - `tests/battle_runtime/capture_canyon_battle_board.gd`
  - `tests/progression/run_progression_tests.gd`
  - `tests/text_runtime/run_text_command_regression.gd`
  - `tests/text_runtime/run_text_command_script.gd`
  - `tests/text_runtime/run_text_command_repl.gd`
  - `tests/text_runtime/scenarios/smoke_startup.txt`
- 主要职责：
  - equipment：
    - 装备种子内容校验
    - 装备 / 卸装与共享仓库联动
    - 装备属性进入角色快照
    - `PartyState` round-trip 后保留装备状态
  - warehouse：
    - 旧存档兼容
    - 堆叠 / 容量 / 超容规则
    - save round-trip
    - 队伍入口与据点入口打开共享仓库
  - battle runtime smoke：
    - terrain effect 时序
    - 真堆叠列移动规则
    - 墙 / 高差阻挡
    - move command 扣 AP 与占位更新
    - height delta 与 column cache 同步
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
    - legacy mastery reward 兼容转换
  - text runtime：
    - `game new/load`
    - `world/party/settlement/warehouse/battle/reward/promotion/close/snapshot/expect`
    - headless snapshot 与文本快照稳定性
  - capture：
    - 导出 `battle_board_canyon_capture.png` 做人工验收
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
  - 各状态下的 action 顺序、目标选择与距离策略。
- 主要职责：
  - 注册当前种子敌方模板与 AI brain。
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
- 真相源：
  - headless 模式下的 session / world loaded / runtime snapshot 语义。
  - 文本命令域、参数格式、expect 断言语法。
  - 文本快照渲染格式。
- 主要职责：
  - 用 `HeadlessGameTestSession` 在无 UI 环境挂起 `GameSession + GameRuntimeFacade`。
  - 用 `GameTextCommandRunner` 执行命令与断言。
  - 用 `GameTextCommandResult` 输出可读结果。
  - 用 `GameTextSnapshotRenderer` 渲染稳定文本快照。
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
  - 如果改 save 兼容，补 CU-02
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
  - 如果只是继续优化设计口径，可先对照 `docs/design/equipment_durability_plan.md`

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

### 只改 canyon 地形、战斗 props、fallback build

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

### 只改 save payload、party schema、reward queue 兼容

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
- `scripts/systems/battle_state_factory.gd`
  - 文件仍在仓库，但不在主 battle start 路径上，更像 sidecar / fallback builder。
- `scripts/systems/settlement_window_system.gd`
  - 仍返回 `pending_mastery_rewards` 形状；正式运行时会再归并进 `pending_character_rewards`。
- `scripts/systems/game_runtime_facade.gd`
  - 也保留了 `_extract_pending_mastery_rewards()` / `_enqueue_pending_mastery_rewards()` 这种兼容命名，但实际入队的是 `pending_character_rewards`。
- `scripts/systems/battle_runtime_module.gd`
  - 结算后仍通过 `consume_pending_mastery_rewards()` 吐出兼容奖励数组，再由 runtime / character bridge 归并进正式奖励队列。
- `tests/progression/`
  - 旧 `helpers/` + `cases/` 目录已经移除；当前只保留单 runner 模式。
- 结论：
  - world / battle 运行时已经拆成 `GameRuntimeFacade` 与 `WorldMapSystem` 两层。
  - battle renderer 主线已经完成换代。
  - reward queue 主线已经统一，但旧 payload 名字仍在多个边界层残留。

## 不推荐的切法

- 不要把 `GameSession`、`GameRuntimeFacade`、`CharacterManagementModule`、`BattleRuntimeModule`、`BattleBoardController` 一次性全装，除非任务确实跨越“登录 -> 世界 -> 战斗 -> 战后成长 -> 存档 -> 文本测试”整条链。
- 不要在只改共享仓库时把 `battle_runtime_module.gd`、`battle_board_2d.gd` 一起带上。
- 不要在只改 battle HUD / TileMap 时把 `progression_service.gd`、`party_warehouse_service.gd` 一起带上。
- 不要在只改 achievement / reward queue 时默认把 `battle_board_controller.gd` 一起带上。
- 不要把 `WorldMapSystem` 当成当前唯一运行时真相源；核心状态现在主要落在 `GameRuntimeFacade`。
- 不要把 `pending_mastery_rewards` 误认为当前正式奖励真相源。

## 结论

- 当前最关键的桥接单元是：
  - CU-02 `GameSession`
  - CU-06 `GameRuntimeFacade + WorldMapSystem`
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
  - `pending_mastery_rewards -> pending_character_rewards` 兼容桥
  - `GameRuntimeFacade` 与 `WorldMapSystem` 的状态同步
  - world / battle 模式切换后的统一持久化
  - `terrain_profile_id / prop_ids / board renderer` 三者一致性
- 后续做 agent 化拆分时，最稳的装载方式仍然是：
  - 1 个桥接单元
  - 加 1 到 2 个叶子单元
  - 再加 1 个当前真实存在的测试 / headless 辅助单元
