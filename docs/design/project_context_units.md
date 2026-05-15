# 当前 Godot 项目的上下文装载单元

更新日期：`2026-05-16`

## 使用规则

- 这份文档只用于给 agent / 开发者划分文件读取范围，不是系统设计说明。
- 先按“推荐装载组合”匹配任务；没有命中时，从“单元总览”选择 1 个桥接单元、1 到 2 个叶子单元，再补 1 个测试 / headless 辅助单元。
- 优先读取“文件”列表；只有任务明确跨边界时，才按“邻接单元”补读。
- 遇到 save/schema/历史 payload 兼容问题时，不要自行添加迁移、别名、fallback 或旧格式支持，先向用户确认。
- 常规全量测试不包含 battle simulation、balance simulation、benchmark 或交互式 REPL。

## 全局排除

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

## 关键桥接链

```text
LoginScreen -> GameSession
GameSession -> GameRuntimeFacade -> WorldMapRuntimeProxy -> WorldMapSystem
GameRuntimeFacade -> BattleSessionFacade -> BattleRuntimeModule
GameRuntimeFacade -> CharacterManagementModule -> Progression / Equipment / Attribute services
BattleMapPanel -> BattleBoard2D -> BattleBoardController
HeadlessGameTestSession -> GameSession + GameRuntimeFacade -> GameTextCommandRunner
```

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
  - `scenes/ui/character_creation_window.tscn`
  - `scripts/ui/character_creation_window.gd`
  - `scripts/utils/display_settings_service.gd`
  - `scripts/utils/world_preset_registry.gd`
- 负责：启动页、世界预设入口、存档列表、显示设置、建卡窗口到 `GameSession` 的入口。
- 适合：登录流程、建卡 UI、save list、世界预设入口、显示设置。
- 邻接单元：CU-02、CU-03、CU-14。
- 不带：CU-06、CU-18，除非任务进入世界或战斗场景。

### CU-02 GameSession、存档、序列化、全局内容缓存

- 文件：
  - `scripts/systems/persistence/*.gd`
  - `scripts/systems/progression/racial_skill_grant_service.gd`
  - `scripts/utils/true_random_seed_service.gd`
  - `scripts/player/progression/*content_registry.gd`
  - `scripts/systems/battle/core/special_profiles/battle_special_profile_registry.gd`
  - `scripts/systems/battle/core/special_profiles/battle_special_profile_manifest_validator.gd`
  - `scripts/player/warehouse/item_content_registry.gd`
  - `scripts/player/warehouse/recipe_content_registry.gd`
  - `scripts/enemies/enemy_content_registry.gd`
- 负责：active save、slot meta、save payload、save index、全局内容注册表、world-level 装备实例 ID、battle save lock。
- 适合：save payload、slot meta、active world 生命周期、序列化严格校验、内容注册表接入。
- 邻接单元：CU-01、CU-03、CU-04、CU-10、CU-11、CU-13、CU-20、CU-21。
- 不带：世界地图渲染、战斗棋盘展示。

### CU-03 世界配置资源与预设数据

- 文件：
  - `scripts/utils/world_map_generation_config.gd`
  - `scripts/utils/world_map_settlement_bundle.gd`
  - `scripts/utils/world_map_settlement_name_pool.gd`
  - `scripts/utils/world_map_wild_spawn_bundle.gd`
  - `scripts/utils/world_map_content_validator.gd`
  - `scripts/utils/settlement_config.gd`
  - `scripts/utils/settlement_distribution_rule.gd`
  - `scripts/utils/facility_config.gd`
  - `scripts/utils/facility_slot_config.gd`
  - `scripts/utils/facility_npc_config.gd`
  - `scripts/utils/weighted_facility_entry.gd`
  - `scripts/utils/wild_spawn_rule.gd`
  - `data/configs/world_map/*.tres`
  - `data/configs/world_map/shared/*.tres`
- 负责：world preset、world generation config、settlement bundle、facility、wild spawn bundle 的静态数据。
- 适合：新增/调整世界预设、设施分布、野外遭遇配置、世界内容校验。
- 邻接单元：CU-01、CU-02、CU-04。
- 不带：runtime 场景接线、battle runtime。

### CU-04 世界生成、据点服务注入、遭遇锚点

- 文件：
  - `docs/design/settlement.md`
  - `scripts/systems/world/world_map_spawn_system.gd`
  - `scripts/systems/world/encounter_anchor_data.gd`
  - `scripts/utils/true_random_seed_service.gd`
  - `scripts/utils/world_event_config.gd`
  - `scripts/utils/mounted_submap_config.gd`
  - CU-03 的配置资源
- 负责：世界生成、据点服务注入、遭遇锚点、挂载子地图事件。
- 适合：世界生成规则、起始遭遇、据点/设施生成、mounted submap 事件。
- 邻接单元：CU-02、CU-03、CU-05、CU-06、CU-20。
- 不带：UI 窗口和战斗展示，除非任务要求进入场景。

### CU-05 世界网格与迷雾基础设施

- 文件：
  - `scripts/systems/world/world_map_grid_system.gd`
  - `scripts/systems/world/world_map_fog_system.gd`
  - `scripts/utils/world_map_cell_data.gd`
  - `scripts/utils/vision_source_data.gd`
- 负责：世界网格、坐标、迷雾、视野来源。
- 适合：world move 判定、迷雾刷新、地图 cell 数据。
- 邻接单元：CU-04、CU-06、CU-07。
- 不带：据点窗口、战斗 runtime。

### CU-06 世界/战斗运行时总编排与场景适配

- 文件：
  - `scenes/main/world_map.tscn`
  - `scenes/ui/runtime_log_dock.tscn`
  - `scenes/ui/submap_entry_window.tscn`
  - `scripts/systems/game_runtime/*.gd`
  - `scripts/systems/world/world_map_data_context.gd`
  - `scripts/systems/world/world_time_system.gd`
  - `scripts/systems/settlement/*.gd`
  - `scripts/ui/runtime_log_dock.gd`
  - `scripts/ui/submap_entry_window.gd`
  - `scripts/utils/true_random_seed_service.gd`
  - `assets/main/basic_map/log.png`
- 负责：world/battle 模式、modal 状态、场景到 runtime 的命令/读取桥、据点/仓库/队伍/奖励/任务命令分发、headless snapshot 组织、战后回写与持久化边界。
- 适合：world/battle 切换、窗口互斥、runtime 接线、场景同步、battle loading、reward/party/warehouse/settlement 命令入口。
- 邻接单元：CU-02、CU-04、CU-05、CU-07、CU-08、CU-09、CU-10、CU-12、CU-18、CU-21。
- 不带：世界生成本体、仓库规则本体、battle renderer 本体。

### CU-07 世界地图渲染叶子单元

- 文件：
  - `scripts/ui/world_map_view.gd`
  - `assets/main/basic_map/village_dark.png`
- 负责：大地图绘制、世界事件图标、点击/选中表现。
- 适合：地图视觉、cell 绘制、图标、submap 返回提示。
- 邻接单元：CU-05、CU-06。
- 不带：战斗棋盘、存档序列化。

### CU-08 据点窗口与人物信息窗口

- 文件：
  - `scenes/ui/settlement_window.tscn`
  - `scripts/ui/settlement_window.gd`
  - `scenes/ui/shop_window.tscn`
  - `scripts/ui/shop_window.gd`
  - `scenes/ui/character_info_window.tscn`
  - `scripts/ui/character_info_window.gd`
- 负责：据点服务窗口、商店/任务板/forge shell、人物信息窗口展示。
- 适合：据点 UI、服务反馈、人物信息 section 展示。
- 邻接单元：CU-06、CU-12、CU-14。
- 不带：服务规则本体、世界生成。

### CU-09 队伍管理、成就摘要、转职、角色奖励窗口层

- 文件：
  - `scenes/ui/party_management_window.tscn`
  - `scripts/ui/party_management_window.gd`
  - `scenes/ui/promotion_choice_window.tscn`
  - `scripts/ui/promotion_choice_window.gd`
  - `scenes/ui/mastery_reward_window.tscn`
  - `scripts/ui/mastery_reward_window.gd`
  - `assets/main/basic_map/log.png`
- 负责：队伍窗口、成员选择、成就摘要、转职选择、角色奖励确认。
- 适合：队伍编成 UI、角色奖励弹窗、转职 UI、装备摘要展示。
- 邻接单元：CU-06、CU-10、CU-11、CU-12、CU-14。
- 不带：battle board、世界生成。

### CU-10 队伍共享背包、物品定义与装备基础流转

- 文件：
  - `scripts/player/equipment/*.gd`
  - `scripts/player/warehouse/*.gd`
  - `scripts/systems/inventory/*.gd`
  - `scripts/systems/persistence/game_session.gd`
  - `scripts/systems/persistence/save_serializer.gd`
  - `scenes/ui/party_warehouse_window.tscn`
  - `scripts/ui/party_warehouse_window.gd`
  - `data/configs/items/*.tres`
  - `data/configs/items_templates/*.tres`
  - `data/configs/recipes/*.tres`
- 负责：队伍共享背包、堆叠/容量、物品/配方定义、装备实例、装备/卸装、物品使用、装备掉落基础服务。
- 适合：堆叠规则、容量规则、物品内容、装备实例、基础装备流转、仓库窗口。
- 邻接单元：CU-02、CU-06、CU-09、CU-11、CU-12、CU-19、CU-21。
- 不带：battle runtime，除非是战斗内换装、装备损坏或战后回写。

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
  - `scripts/systems/progression/character_progression_delta.gd`
  - `scripts/systems/progression/pending_character_reward*.gd`
- 负责：`PartyState`、成员状态、技能/职业进度、属性快照、成就/任务/信仰状态、角色奖励 payload。
- 适合：party schema、角色状态字段、奖励队列、成长状态序列化。
- 邻接单元：CU-02、CU-09、CU-10、CU-12、CU-13、CU-14、CU-19。
- 不带：UI 或 battle runtime，除非字段已经投影到对应层。

### CU-12 CharacterManagement、成就记录、奖励归并桥

- 文件：
  - `scripts/systems/progression/character_management_module.gd`
  - `scripts/systems/progression/passive_source_context.gd`
  - `scripts/systems/progression/bloodline_apply_service.gd`
  - `scripts/systems/progression/ascension_apply_service.gd`
  - `scripts/systems/progression/stage_advancement_apply_service.gd`
  - `scripts/systems/progression/racial_skill_grant_service.gd`
  - `scripts/systems/progression/age_stage_resolver.gd`
  - `scripts/systems/progression/misfortune_black_omen_service.gd`
  - `scripts/systems/progression/quest_progress_service.gd`
  - `scripts/systems/progression/faith_service.gd`
  - `scripts/systems/progression/level_growth_evaluation_service.gd`
  - `scripts/systems/progression/practice_growth_service.gd`
  - `scripts/systems/attributes/attribute_source_context.gd`
- 负责：角色管理门面、奖励归并、成就/任务进度、身份应用、属性上下文、装备/技能/成长桥接。
- 适合：奖励入账、成就记录、任务推进、身份刷新、角色信息摘要、功法学习/同轨替换、跨系统成长接线。
- 邻接单元：CU-06、CU-08、CU-09、CU-10、CU-11、CU-13、CU-14、CU-15、CU-19。
- 不带：展示层，除非任务是窗口 payload。

### CU-13 progression 内容定义、条件模型、seed 内容

- 文件：
  - `scripts/player/progression/*_def.gd`
  - `scripts/player/progression/*_requirement.gd`
  - `scripts/player/progression/*content_registry.gd`
  - `scripts/player/progression/progression_content_registry.gd`
  - `scripts/player/progression/progression_data_utils.gd`
  - `scripts/player/progression/*content_rules.gd`
  - `scripts/player/progression/*content_validator.gd`
  - `data/configs/skills/*.tres`
  - `data/configs/professions/*.tres`
  - `data/configs/races/*.tres`
  - `data/configs/subraces/*.tres`
  - `data/configs/race_traits/*.tres`
  - `data/configs/age_profiles/*.tres`
  - `data/configs/bloodlines/*.tres`
  - `data/configs/ascensions/*.tres`
  - `data/configs/stage_advancements/*.tres`
  - `data/configs/barriers/*.tres`
  - `data/configs/faith/*.tres`
- 负责：技能、职业、身份、血脉、升华、阶段进阶、成就、任务等静态内容与跨表校验。
- 适合：新增/改技能、职业、身份内容、条件模型、功法 tag / practice_tier schema、静态内容引用校验。
- 邻接单元：CU-02、CU-11、CU-12、CU-14、CU-15、CU-16、CU-19。
- 不带：运行时服务，除非内容改动需要验证行为。

### CU-14 progression 规则与跨系统属性服务

- 文件：
  - `scripts/systems/progression/progression_service.gd`
  - `scripts/systems/progression/profession_rule_service.gd`
  - `scripts/systems/progression/profession_assignment_service.gd`
  - `scripts/systems/progression/skill_merge_service.gd`
  - `scripts/systems/progression/skill_effective_max_level_rules.gd`
  - `scripts/systems/progression/level_growth_evaluation_service.gd`
  - `scripts/systems/progression/practice_growth_service.gd`
  - `scripts/systems/progression/skill_level_description_formatter.gd`
  - `scripts/systems/progression/attribute_growth_service.gd`
  - `scripts/systems/progression/character_creation_service.gd`
  - `scripts/systems/progression/character_creation_identity_option_service.gd`
  - `scripts/systems/progression/identity_payload_validator.gd`
  - `scripts/systems/progression/body_size_rules.gd`
  - `scripts/systems/progression/age_stage_resolver.gd`
  - `scripts/systems/attributes/attribute_service.gd`
  - `scripts/systems/attributes/attribute_source_context.gd`
- 负责：成长规则、职业规则、技能合成、属性快照、建卡、建卡身份候选、身份 payload 校验、体型、年龄阶段。
- 适合：成长公式、属性公式、职业/技能规则、功法同轨替换规则、建卡规则、建卡身份候选、身份 payload 校验、体型派生。
- 邻接单元：CU-01、CU-09、CU-11、CU-12、CU-13、CU-15、CU-19。
- 不带：内容资源，除非规则和 seed 内容同时变化。

### CU-15 战斗运行时总编排

- 文件：
  - `scripts/systems/battle/runtime/*.gd`
  - `scripts/systems/battle/fate/*.gd`
  - `scripts/systems/fate/low_luck_relic_rules.gd`
  - `scripts/systems/game_runtime/battle_session_facade.gd`
  - `scripts/systems/game_runtime/game_runtime_facade.gd`
  - `scripts/systems/battle/core/battle_command.gd`
  - `scripts/systems/battle/core/battle_preview.gd`
  - `scripts/systems/battle/core/battle_event_batch.gd`
  - `scripts/systems/battle/core/battle_resolution_result.gd`
  - `scripts/systems/battle/core/battle_loot_constants.gd`
  - `scripts/systems/battle/core/battle_common_skill_outcome.gd`
  - `scripts/systems/battle/core/battle_barrier*.gd`
  - `scripts/systems/battle/core/battle_special_profile*.gd`
  - `scripts/systems/battle/core/special_profiles/*.gd`
  - `scripts/systems/battle/core/meteor_swarm/*.gd`
  - `scripts/systems/battle/rules/battle_skill_resolution_rules.gd`
  - `scripts/systems/battle/rules/battle_target_team_rules.gd`
  - `scripts/systems/battle/rules/battle_save_resolver.gd`
  - `scripts/systems/battle/rules/battle_damage_preview_range_service.gd`
  - `scripts/systems/battle/rules/battle_range_service.gd`
  - `scripts/systems/battle/rules/battle_report_formatter.gd`
  - `scripts/systems/battle/terrain/battle_terrain_effect_system.gd`
  - `scripts/systems/battle/ai/battle_ai_action_assembler.gd`
  - `scripts/systems/battle/ai/battle_ai_runtime_action_plan.gd`
  - `scripts/systems/battle/ai/battle_ai_skill_affordance_classifier.gd`
  - `scripts/systems/battle/sim/*.gd`
  - `data/configs/skill_special_profiles/**/*.tres`
  - `data/configs/barriers/*.tres`
- 负责：开战、时间轴、命令 preview/issue、技能执行、战斗内换装、loot、评分、fate、battle-local 状态、simulation runner。
- 适合：指令生命周期、战斗流程、战斗结算、特殊技能 profile、战斗内装备事务、AI/手动时序、模拟链路。
- 邻接单元：CU-02、CU-10、CU-11、CU-12、CU-13、CU-14、CU-16、CU-17、CU-18、CU-20、CU-21。
- 不带：登录壳、共享背包窗口、battle board 展示，除非任务涉及界面。

### CU-16 战斗状态模型、边规则、伤害、AI 规则层

- 文件：
  - `scripts/systems/battle/core/battle_state.gd`
  - `scripts/systems/battle/core/battle_timeline_state.gd`
  - `scripts/systems/battle/core/battle_unit_state.gd`
  - `scripts/systems/battle/core/battle_cell_state.gd`
  - `scripts/systems/battle/core/battle_status_effect_state.gd`
  - `scripts/systems/battle/core/battle_barrier*.gd`
  - `scripts/systems/battle/core/battle_edge*.gd`
  - `scripts/systems/battle/core/battle_attack*.gd`
  - `scripts/systems/battle/core/battle_repeat_attack_stage_spec.gd`
  - `scripts/systems/battle/terrain/battle_terrain_rules.gd`
  - `scripts/systems/battle/terrain/battle_terrain_topology_service.gd`
  - `scripts/systems/battle/terrain/battle_grid_service.gd`
  - `scripts/systems/battle/terrain/battle_edge_service.gd`
  - `scripts/systems/battle/terrain/battle_terrain_effect_state.gd`
  - `scripts/systems/battle/fate/battle_fate_event_bus.gd`
  - `scripts/systems/battle/fate/battle_fate_attack_rules.gd`
  - `scripts/systems/battle/fate/fate_attack_formula.gd`
  - `scripts/systems/battle/rules/battle_damage_resolver.gd`
  - `scripts/systems/battle/rules/battle_damage_preview_range_service.gd`
  - `scripts/systems/battle/rules/battle_status_semantic_table.gd`
  - `scripts/systems/battle/rules/battle_target_team_rules.gd`
  - `scripts/systems/battle/rules/battle_hit_resolver.gd`
  - `scripts/systems/battle/rules/battle_attack_check_policy_service.gd`
  - `scripts/systems/battle/rules/battle_range_service.gd`
  - `scripts/systems/battle/ai/*.gd`
  - `scripts/systems/battle/runtime/trait_trigger_hooks.gd`
  - `scripts/player/progression/combat_effect_def.gd`
  - `scripts/enemies/actions/*.gd`
  - `scripts/player/warehouse/weapon_profile_def.gd`
  - `scripts/player/warehouse/weapon_damage_dice_def.gd`
- 负责：BattleState 数据模型、terrain/edge/grid 规则、伤害/命中/豁免/状态语义、AI 评分、决策输入、AI state transition resolver、runtime action plan 与技能 affordance 分类。
- 适合：战斗规则、伤害、命中、AI 评分、AI 状态转移、AI 行动生成、terrain effect、状态语义、武器射程规则。
- 邻接单元：CU-13、CU-15、CU-17、CU-18、CU-20。
- 不带：战斗流程 sidecar，除非规则改动需要执行链验证。

### CU-17 战斗地形 profile、敌人 roster、prop 注入

- 文件：
  - `scripts/systems/battle/terrain/battle_terrain_generator.gd`
  - `scripts/systems/world/encounter_roster_builder.gd`
  - `scripts/systems/world/wild_encounter_growth_system.gd`
  - `scripts/utils/battle_board_prop_catalog.gd`
  - `data/configs/enemies/rosters/*.tres`
  - `assets/main/battle/terrain/canyon/*.png`
- 负责：battle terrain 生成、roster 装配、prop catalog 注入。
- 适合：canyon 地形、spawn/roster、战斗 props、terrain profile。
- 邻接单元：CU-15、CU-16、CU-18、CU-20。
- 不带：HUD/棋盘渲染，除非 prop 或 terrain 视觉也变化。

### CU-18 战斗展示主链

- 文件：
  - `scenes/ui/battle_map_panel.tscn`
  - `scripts/ui/battle_map_panel.gd`
  - `scripts/systems/battle/presentation/battle_hud_adapter.gd`
  - `scenes/ui/battle_board_2d.tscn`
  - `scripts/ui/battle_board_2d.gd`
  - `scripts/ui/battle_board_render_profile.gd`
  - `scripts/ui/battle_board_controller.gd`
  - `scenes/common/battle_board_prop.tscn`
  - `scripts/ui/battle_board_prop.gd`
  - `assets/main/battle/terrain/canyon/*.png`
- 负责：battle HUD、棋盘绘制、TileMap/prop/unit 渲染、相机、hover/overlay 展示。
- 适合：battle HUD、棋盘、TileMap、相机、目标浮标、视觉层级。
- 邻接单元：CU-06、CU-15、CU-16、CU-17、CU-19、CU-20。
- 不带：progression、仓库规则，除非展示字段来自这些系统。

### CU-19 自动化回归与截图辅助

- 文件：
  - `tests/run_regression_suite.py`
  - `tests/shared/*.gd`
  - `tests/equipment/run_*.gd`
  - `tests/warehouse/run_*.gd`
  - `tests/battle_runtime/**/*.gd`
  - `tests/progression/**/*.gd`
  - `tests/runtime/**/*.gd`
  - `tests/text_runtime/**/*.gd`
  - `tests/world_map/**/*.gd`
  - `tools/build_battle_sim_analysis_packet.py`
  - `tools/character_creation_reroll_simulation.gd`
  - `.codex/skills/battle-sim-analysis/SKILL.md`
- 执行约束：
  - 常规测试优先用 `python tests/run_regression_suite.py` 或相关 `godot --headless --script tests/.../run_*.gd`。
  - 默认不要运行 `tests/battle_runtime/simulation/*`、`tests/battle_runtime/benchmarks/*`、`tests/text_runtime/tools/*`。
  - 只有用户明确要求 battle simulation、数值模拟、AI 对战模拟或平衡分析时，才运行 simulation / benchmark 入口。
- 负责：headless 回归、schema/runtime contract、测试 fixture、截图/签名辅助、文本命令回归。
- 适合：为任意运行时改动补测试、定位回归入口、截图验收。
- 邻接单元：按业务域补 CU-10、CU-12、CU-15、CU-17、CU-18、CU-21 等。

### CU-20 敌方模板、AI brain、行动定义种子内容

- 文件：
  - `scripts/enemies/*.gd`
  - `scripts/enemies/enemy_ai_generation_slot_def.gd`
  - `scripts/enemies/enemy_ai_transition_rule_def.gd`
  - `scripts/enemies/enemy_ai_transition_condition_def.gd`
  - `scripts/enemies/actions/*.gd`
  - `scripts/systems/world/encounter_roster_builder.gd`
  - `scripts/player/warehouse/item_def.gd`
  - `scripts/player/warehouse/weapon_profile_def.gd`
  - `scripts/player/warehouse/weapon_damage_dice_def.gd`
  - `data/configs/enemies/enemy_content_seed.tres`
  - `data/configs/enemies/brains/*.tres`
  - `data/configs/enemies/templates/*.tres`
  - `data/configs/enemies/rosters/*.tres`
  - 改 `attack_equipment_item_id` 时按需读取 `data/configs/items/*.tres`
- 负责：敌方模板、AI brain/state/action/generation slot/transition rule、wild encounter roster、敌方攻击装备和掉落静态内容。
- 适合：新敌人、敌方棋盘贴图、敌人技能表、AI state transition、AI action 顺序、generation slot、target selector、distance 策略。
- 邻接单元：CU-02、CU-10、CU-15、CU-16、CU-17、CU-18。
- 不带：玩家 UI、仓库规则实现，除非新增装备引用或展示字段。

### CU-21 Headless runtime、文本命令与快照渲染

- 文件：
  - `scripts/systems/game_runtime/headless/headless_game_test_session.gd`
  - `scripts/systems/game_runtime/headless/game_text_command_runner.gd`
  - `scripts/systems/game_runtime/headless/game_text_command_result.gd`
  - `scripts/utils/game_text_snapshot_renderer.gd`
  - `tests/text_runtime/commands/run_*.gd`
  - `tests/text_runtime/headless/run_*.gd`
  - `tests/text_runtime/tools/run_*.gd`
  - `tests/text_runtime/README.md`
- 负责：无 UI session、文本命令、expect 断言、结构化/文本快照、agent 自动化入口。
- 适合：新增 headless 指令域、改 snapshot schema、改 REPL/脚本执行/expect、为 agent 增加文本回归入口。
- 邻接单元：CU-02、CU-06、CU-10、CU-15、CU-16、CU-19、CU-20。
- 不带：正式玩家 UI、主启动链、世界生成真相源。

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
- 按需补：
  - 改世界事件图标、submap 返回提示或点击返回表现仍按这组读取

### 只改据点服务、人物信息、服务反馈

- 必带：
  - CU-06
  - CU-08
- 按需补：
  - 服务会发奖励或记成就补 CU-12
  - 服务项来自 world spawn 补 CU-04

### 只改队伍编成、成就摘要、转职或角色奖励弹窗

- 必带：
  - CU-06
  - CU-09
  - CU-11
- 按需补：
  - 真正改成长规则时补 CU-12 / CU-14

### 只改队伍共享背包规则、物品内容、装备基础流转、仓库窗口

- 必带：
  - CU-10
  - CU-11
  - CU-19
- 按需补：
  - 改队伍管理里的装备摘要展示补 CU-09
  - 改 runtime 接线补 CU-06
  - 改 save schema 或严格校验链补 CU-02
  - 改 headless 文本流补 CU-21

### 只做装备耐久、装备实例化前置或战斗内装备损坏

- 必带：
  - CU-10
  - CU-11
  - CU-12
  - CU-15
  - CU-16
  - CU-19
- 按需补：
  - 改 runtime 场景接线或持久化落盘补 CU-06
  - 改文本快照或 headless 指令断言补 CU-21
  - 只优化设计口径时先读 `docs/design/equipment_system_plan.md`

### 只改角色成长、成就、奖励归并

- 必带：
  - CU-11
  - CU-12
  - CU-13
  - CU-14
- 按需补：
  - 来源是战斗事件补 CU-15
  - 要跑回归补 CU-19

### 只改敌方模板、敌方技能表、AI brain

- 必带：
  - CU-20
  - CU-16
- 按需补：
  - 改非野兽模板的 `attack_equipment_item_id` 或新增引用物品补 CU-10
  - 改开战装配与 roster 补 CU-15 / CU-17

### 只改战斗规则、伤害、AI、terrain effect

- 必带：
  - CU-15
  - CU-16
- 按需补：
  - 改 terrain profile / spawn / prop 注入补 CU-17
  - 改 enemy static content 补 CU-20
  - 改展示反馈补 CU-18

### 只改特殊技能 profile / 陨星雨结算

- 必带：
  - CU-15
  - CU-16
  - CU-19
- 按需补：
  - 改棋盘 overlay / HUD payload 补 CU-18
  - 改 AI 使用策略或 action 过滤补 CU-20
  - 改 terrain profile / prop 衔接补 CU-17
  - 改 GameSession 内容校验快照补 CU-02

### 只改 canyon 地形、战斗 props、battle build

- 必带：
  - CU-17
  - CU-18
  - CU-19
- 按需补：
  - 改真正 battle start 装配补 CU-15

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
  - 队伍共享背包 / `warehouse_state` 字段补 CU-10
  - reward 归并逻辑补 CU-12

### 只改 headless 文本命令、快照、REPL 或脚本化回归

- 必带：
  - CU-21
  - CU-19
- 按需补：
  - 改 runtime schema 补 CU-06
  - 改具体业务领域补对应叶子单元

## 不推荐的切法

- 不要把 CU-02、CU-06、CU-12、CU-15、CU-18 一次性全装，除非任务确实跨越登录、世界、战斗、战后成长、存档和文本测试整条链。
- 不要在只改队伍共享背包时默认带 CU-15 或 CU-18。
- 不要在只改 battle HUD / TileMap 时默认带 CU-12 或 CU-10。
- 不要在只改 achievement / reward queue 时默认带 CU-18。
- 不要把 `WorldMapSystem` 当成唯一运行时真相源；核心状态在 `GameRuntimeFacade`，场景侧命令 / 读取边界在 `WorldMapRuntimeProxy`。
- 不要把旧奖励设计文档里的 `pending_mastery_rewards` 示例当作当前正式奖励真相源。
