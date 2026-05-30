# 当前 Godot 项目的上下文装载单元

更新日期：`2026-05-30`

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
WorldMapSystem -> BattleMapPanel -> BattleHudAdapter
BattleMapPanel -> BattleBoard2D -> BattleBoardController
HeadlessGameTestSession -> GameSession + GameRuntimeFacade -> GameTextCommandRunner
```

## 单元总览

### CU-01 登录壳、世界预设、存档选择、显示设置

- 文件：
  - `project.godot`
  - `magic.csproj`
  - `scenes/main/login_screen.tscn`
  - `scripts/ui/LoginScreen.cs`
  - `scenes/ui/world_preset_picker_window.tscn`
  - `scripts/ui/WorldPresetPickerWindow.cs`
  - `scenes/ui/save_list_window.tscn`
  - `scripts/ui/SaveListWindow.cs`
  - `scenes/ui/display_settings_window.tscn`
  - `scripts/ui/DisplaySettingsWindow.cs`
  - `scenes/ui/character_creation_window.tscn`
  - `scripts/ui/CharacterCreationWindow.cs`
  - `scripts/utils/DisplaySettingsService.cs`
  - `scripts/utils/WorldPresetRegistry.cs`
- 负责：启动页、世界预设入口、存档列表、显示设置、建卡窗口到 `GameSession` 的入口。
- 适合：登录流程、建卡 UI、save list、世界预设入口、显示设置。
- 邻接单元：CU-02、CU-03、CU-14。
- 不带：CU-06、CU-18，除非任务进入世界或战斗场景。

### CU-02 GameSession、存档、序列化、全局内容缓存

- 文件：
  - `scripts/systems/persistence/GameSession.cs`
  - `scripts/systems/persistence/FileIOCoordinator.cs`
  - `scripts/systems/persistence/GameLogService.cs`
  - `scripts/systems/persistence/ProgressionSerialization.cs`
  - `scripts/systems/persistence/SaveSerializer.cs`
  - `scripts/systems/progression/RacialSkillGrantService.cs`
  - `scripts/utils/TrueRandomSeedService.cs`
  - `scripts/player/progression/*content_registry.gd`
  - `scripts/player/progression/*ContentRegistry.cs`
  - `scripts/systems/battle/core/special_profiles/BattleSpecialProfileRegistry.cs`
  - `scripts/systems/battle/core/special_profiles/BattleSpecialProfileManifestValidator.cs`
  - `scripts/player/warehouse/ItemContentRegistry.cs`
  - `scripts/player/warehouse/RecipeContentRegistry.cs`
  - `scripts/player/warehouse/skill_book_item_content_validator.gd`
  - `scripts/enemies/EnemyContentRegistry.cs`
  - `scripts/enemies/EnemyContentSeed.cs`
- 负责：active save、slot meta、save payload、save index、全局内容注册表、world-level 装备实例 ID、battle save lock。
- 适合：save payload、slot meta、active world 生命周期、序列化严格校验、内容注册表接入。
- 邻接单元：CU-01、CU-03、CU-04、CU-10、CU-11、CU-13、CU-20、CU-21。
- 不带：世界地图渲染、战斗棋盘展示。

### CU-03 世界配置资源与预设数据

- 文件：
  - `scripts/utils/WorldMapGenerationConfig.cs`
  - `scripts/utils/WorldMapSettlementBundle.cs`
  - `scripts/utils/WorldMapSettlementNamePool.cs`
  - `scripts/utils/WorldMapWildSpawnBundle.cs`
  - `scripts/utils/WorldMapContentValidator.cs`
  - `scripts/utils/SettlementConfig.cs`
  - `scripts/utils/SettlementDistributionRule.cs`
  - `scripts/utils/FacilityConfig.cs`
  - `scripts/utils/FacilitySlotConfig.cs`
  - `scripts/utils/FacilityNpcConfig.cs`
  - `scripts/utils/WeightedFacilityEntry.cs`
  - `scripts/utils/WildSpawnRule.cs`
  - `data/configs/world_map/*.tres`
  - `data/configs/world_map/shared/*.tres`
- 负责：world preset、world generation config、settlement bundle、facility、wild spawn bundle 的静态数据。
- 适合：新增/调整世界预设、设施分布、野外遭遇配置、世界内容校验。
- 邻接单元：CU-01、CU-02、CU-04。
- 不带：runtime 场景接线、battle runtime。

### CU-04 世界生成、据点服务注入、遭遇锚点

- 文件：
  - `docs/design/settlement.md`
  - `scripts/systems/world/WorldMapSpawnSystem.cs`
  - `scripts/systems/world/EncounterAnchorData.cs`
  - `scripts/systems/world/WildEncounterGrowthSystem.cs`
  - `scripts/utils/TrueRandomSeedService.cs`
  - `scripts/utils/WorldEventConfig.cs`
  - `scripts/utils/MountedSubmapConfig.cs`
  - CU-03 的配置资源
- 负责：世界生成、据点服务注入、遭遇锚点、挂载子地图事件。
- 适合：世界生成规则、起始遭遇、据点/设施生成、mounted submap 事件。
- 邻接单元：CU-02、CU-03、CU-05、CU-06、CU-20。
- 不带：UI 窗口和战斗展示，除非任务要求进入场景。

### CU-05 世界网格与迷雾基础设施

- 文件：
  - `scripts/systems/world/WorldMapGridSystem.cs`
  - `scripts/systems/world/WorldMapFogSystem.cs`
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
  - `scripts/systems/game_runtime/BattleSessionFacade.cs`
  - `scripts/systems/game_runtime/GameRuntimeFacade.cs`
  - `scripts/systems/game_runtime/GameRuntimeBattleSelection.cs`
  - `scripts/systems/game_runtime/GameRuntimeBattleSelectionState.cs`
  - `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
  - `scripts/systems/game_runtime/GameRuntimeWarehouseHandler.cs`
  - `scripts/systems/game_runtime/GameRuntimePartyCommandHandler.cs`
  - `scripts/systems/game_runtime/GameRuntimeRewardFlowHandler.cs`
  - `scripts/systems/game_runtime/GameRuntimeQuestCommandHandler.cs`
  - `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs`
  - `scripts/systems/game_runtime/GameRuntimeCommandLogger.cs`
  - `scripts/systems/game_runtime/GameRuntimeBattleWritebackService.cs`
  - `scripts/systems/game_runtime/GameRuntimeBattleLootCommitService.cs`
  - `scripts/systems/game_runtime/GameRuntimeCharacterInfoBuilder.cs`
  - `scripts/systems/world/WorldMapDataContext.cs`
  - `scripts/systems/world/WorldTimeSystem.cs`
  - `scripts/systems/game_runtime/WorldMapRuntimeProxy.cs`
  - `scripts/systems/game_runtime/WorldMapSystem.cs`
  - `scripts/systems/settlement/*.gd`
  - `scripts/systems/settlement/SettlementServiceResult.cs`
  - `scripts/ui/RuntimeLogDock.cs`
  - `scripts/ui/SubmapEntryWindow.cs`
  - `scripts/utils/TrueRandomSeedService.cs`
  - `assets/main/basic_map/log.png`
- 负责：C# `GameRuntimeFacade` 持有 world/battle 模式、modal 状态、battle start context、据点/仓库/队伍/奖励/任务命令分发、headless snapshot 组织、战后回写与持久化边界；`WorldMapSystem.cs` 只作为场景适配层。
- 适合：world/battle 切换、窗口互斥、runtime 接线、场景同步、battle loading、reward/party/warehouse/settlement 命令入口。
- 邻接单元：CU-02、CU-04、CU-05、CU-07、CU-08、CU-09、CU-10、CU-12、CU-15、CU-18、CU-21。
- 不带：世界生成本体、仓库规则本体、battle renderer 本体。

### CU-07 世界地图渲染叶子单元

- 文件：
  - `scripts/ui/WorldMapView.cs`
  - `assets/main/basic_map/village_dark.png`
- 负责：大地图绘制、世界事件图标、点击/选中表现。
- 适合：地图视觉、cell 绘制、图标、submap 返回提示。
- 邻接单元：CU-05、CU-06。
- 不带：战斗棋盘、存档序列化。

### CU-08 据点窗口与人物信息窗口

- 文件：
  - `scenes/ui/settlement_window.tscn`
  - `scripts/ui/SettlementWindow.cs`
  - `scripts/ui/PartyMemberOptionUtils.cs`
  - `scenes/ui/shop_window.tscn`
  - `scripts/ui/ShopWindow.cs`
  - `scenes/ui/character_info_window.tscn`
  - `scripts/ui/CharacterInfoWindow.cs`
- 负责：据点服务窗口、商店/任务板/forge shell、人物信息窗口展示。
- 适合：据点 UI、服务反馈、人物信息 section 展示。
- 邻接单元：CU-06、CU-12、CU-14。
- 不带：服务规则本体、世界生成。

### CU-09 队伍管理、成就摘要、转职、角色奖励窗口层

- 文件：
  - `scenes/ui/party_management_window.tscn`
  - `scripts/ui/PartyManagementWindow.cs`
  - `scenes/ui/promotion_choice_window.tscn`
  - `scripts/ui/PromotionChoiceWindow.cs`
  - `scenes/ui/mastery_reward_window.tscn`
  - `scripts/ui/MasteryRewardWindow.cs`
  - `assets/main/basic_map/log.png`
- 负责：队伍窗口、成员选择、成就摘要、转职选择、角色奖励确认。
- 适合：队伍编成 UI、角色奖励弹窗、转职 UI、装备摘要展示。
- 邻接单元：CU-06、CU-10、CU-11、CU-12、CU-14。
- 不带：battle board、世界生成。

### CU-10 队伍共享背包、物品定义与装备基础流转

- 文件：
  - `scripts/player/equipment/*.gd`
  - `scripts/player/equipment/*.cs`
  - `scripts/player/warehouse/*.gd`
  - `scripts/systems/inventory/*.gd`
  - `scripts/systems/inventory/*.cs`
  - `scripts/systems/persistence/GameSession.cs`
  - `scripts/systems/persistence/SaveSerializer.cs`
  - `scenes/ui/party_warehouse_window.tscn`
  - `scripts/ui/PartyWarehouseWindow.cs`
  - `data/configs/items/*.tres`
  - `data/configs/items_templates/*.tres`
  - `data/configs/recipes/*.tres`
- 负责：队伍共享背包、堆叠/容量、物品/配方定义、技能书跨表校验、装备实例、装备/卸装、物品使用、装备掉落基础服务。
- 适合：堆叠规则、容量规则、物品内容、装备实例、基础装备流转、仓库窗口。
- 邻接单元：CU-02、CU-06、CU-09、CU-11、CU-12、CU-19、CU-21。
- 不带：battle runtime，除非是战斗内换装、装备损坏或战后回写。

### CU-11 队伍与角色成长运行时数据模型

- 文件：
  - `scripts/player/progression/PartyState.cs`
  - `scripts/player/progression/PartyMemberState.cs`
  - `scripts/player/progression/UnitProgress.cs`
  - `scripts/player/progression/UnitSkillProgress.cs`
  - `scripts/player/progression/UnitProfessionProgress.cs`
  - `scripts/player/progression/UnitReputationState.cs`
  - `scripts/player/progression/UnitBaseAttributes.cs`
  - `scripts/player/progression/AttributeSnapshot.cs`
  - `scripts/player/progression/pending_profession_choice.gd`
  - `scripts/player/progression/AchievementProgressState.cs`
  - `scripts/player/progression/QuestState.cs`
  - `scripts/player/progression/FaithDeityDef.cs`
  - `scripts/player/progression/FaithRankDef.cs`
  - `scripts/systems/progression/CharacterProgressionDelta.cs`
  - `scripts/systems/progression/PendingCharacterReward.cs`
  - `scripts/systems/progression/PendingCharacterRewardEntry.cs`
- 负责：`PartyState`、成员状态、技能/职业进度、属性快照、成就/任务/信仰状态、角色奖励 payload。
- 适合：party schema、角色状态字段、奖励队列、成长状态序列化。
- 邻接单元：CU-02、CU-09、CU-10、CU-12、CU-13、CU-14、CU-19。
- 不带：UI 或 battle runtime，除非字段已经投影到对应层。

### CU-12 CharacterManagement、成就记录、奖励归并桥

- 文件：
  - `scripts/systems/progression/CharacterManagementModule.cs`
  - `scripts/systems/progression/PassiveSourceContext.cs`
  - `scripts/systems/progression/BloodlineApplyService.cs`
  - `scripts/systems/progression/AscensionApplyService.cs`
  - `scripts/systems/progression/StageAdvancementApplyService.cs`
  - `scripts/systems/progression/RacialSkillGrantService.cs`
  - `scripts/systems/progression/AgeStageResolver.cs`
  - `scripts/systems/progression/misfortune_black_omen_service.gd`
  - `scripts/systems/progression/QuestProgressService.cs`
  - `scripts/systems/progression/FaithService.cs`
  - `scripts/systems/progression/LevelGrowthEvaluationService.cs`
  - `scripts/systems/progression/PracticeGrowthService.cs`
  - `scripts/systems/attributes/AttributeSourceContext.cs`
- 负责：角色管理门面、奖励归并、成就/任务进度、身份应用、属性上下文、装备/技能/成长桥接。
- 适合：奖励入账、成就记录、任务推进、身份刷新、角色信息摘要、功法学习/同轨替换、跨系统成长接线。
- 邻接单元：CU-06、CU-08、CU-09、CU-10、CU-11、CU-13、CU-14、CU-15、CU-19。
- 不带：展示层，除非任务是窗口 payload。

### CU-13 progression 内容定义、条件模型、seed 内容

- 文件：
  - `scripts/player/progression/*Def.cs`
  - `scripts/player/progression/*Requirement.cs`
  - `scripts/player/progression/*ContentRegistry.cs`
  - `scripts/player/progression/*ContentRules.cs`
  - `scripts/player/progression/*_def.gd`
  - `scripts/player/progression/*_requirement.gd`
  - `scripts/player/progression/*content_registry.gd`
  - `scripts/player/progression/progression_content_registry.gd`
  - `scripts/player/progression/ProgressionDataUtils.cs`
  - `scripts/player/progression/BarrierContentRegistry.cs`
  - `scripts/player/progression/identity_content_registry_base.gd`
  - `scripts/player/progression/*content_rules.gd`
  - `scripts/player/progression/BattleExecuteContentRules.cs`
  - `scripts/player/progression/CombatTargetTeamContentRules.cs`
  - `scripts/player/progression/*content_validator.gd`
  - `scripts/player/progression/QuestContentValidator.cs`
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
  - `scripts/systems/progression/ProgressionService.cs`
  - `scripts/systems/progression/ProfessionRuleService.cs`
  - `scripts/systems/progression/ProfessionAssignmentService.cs`
  - `scripts/systems/progression/SkillMergeService.cs`
  - `scripts/systems/progression/SkillEffectiveMaxLevelRules.cs`
  - `scripts/systems/progression/AttributeGrowthService.cs`
  - `scripts/systems/progression/LevelGrowthEvaluationService.cs`
  - `scripts/systems/progression/PracticeGrowthService.cs`
  - `scripts/systems/progression/SkillLevelDescriptionFormatter.cs`
  - `scripts/systems/progression/CharacterCreationService.cs`
  - `scripts/systems/progression/CharacterCreationIdentityOptionService.cs`
  - `scripts/systems/progression/IdentityPayloadValidator.cs`
  - `scripts/systems/progression/BodySizeRules.cs`
  - `scripts/systems/progression/AgeStageResolver.cs`
  - `scripts/systems/attributes/AttributeService.cs`
  - `scripts/systems/attributes/AttributeSourceContext.cs`
- 负责：成长规则、职业规则、技能合成、属性快照、建卡、建卡身份候选、身份 payload 校验、体型、年龄阶段。
- 适合：成长公式、属性公式、职业/技能规则、功法同轨替换规则、建卡规则、建卡身份候选、身份 payload 校验、体型派生。
- 邻接单元：CU-01、CU-09、CU-11、CU-12、CU-13、CU-15、CU-19。
- 不带：内容资源，除非规则和 seed 内容同时变化。

### CU-15 战斗运行时总编排

- 文件：
  - `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
  - `scripts/systems/battle/runtime/BattleChargeResolver.cs`
  - `scripts/systems/battle/runtime/BattleMagicBacklashResolver.cs`
  - `scripts/systems/battle/runtime/TraitTriggerHooks.cs`
  - `scripts/systems/battle/fate/*.gd`
  - `scripts/systems/battle/fate/FateRuntimeModule.cs`
  - `scripts/systems/battle/fate/FortuneService.cs`
  - `scripts/systems/fate/LowLuckRelicRules.cs`
  - `scripts/systems/game_runtime/BattleSessionFacade.cs`
  - `scripts/systems/game_runtime/GameRuntimeFacade.cs`
  - `scripts/systems/battle/core/BattleCommand.cs`
  - `scripts/systems/battle/core/BattlePreview.cs`
  - `scripts/systems/battle/core/BattleEventBatch.cs`
  - `scripts/systems/battle/core/BattleResolutionResult.cs`
  - `scripts/systems/battle/core/BattleLootConstants.cs`
  - `scripts/systems/battle/core/BattleCommonSkillOutcome.cs`
  - `scripts/systems/battle/core/BattleBarrier*.cs`
  - `scripts/systems/battle/core/battle_special_profile*.gd`
  - `scripts/systems/battle/core/special_profiles/*.gd`
  - `scripts/systems/battle/core/meteor_swarm/*.cs`
  - `scripts/systems/battle/rules/BattleSkillResolutionRules.cs`
  - `scripts/systems/battle/rules/BattleTargetTeamRules.cs`
  - `scripts/systems/battle/rules/BattleDamageResolver.cs`
  - `scripts/systems/battle/rules/BattleSaveResolver.cs`
  - `scripts/systems/battle/rules/BattleDamagePreviewRangeService.cs`
  - `scripts/systems/battle/rules/BattleRangeService.cs`
  - `scripts/systems/battle/rules/BattleEquipmentRequirementRules.cs`
  - `scripts/systems/battle/rules/BattleReportFormatter.cs`
  - `scripts/systems/battle/terrain/battle_terrain_effect_system.gd`
  - `scripts/systems/battle/ai/BattleAiActionAssembler.cs`
  - `scripts/systems/battle/ai/BattleAiContext.cs`
  - `scripts/systems/battle/ai/BattleAiRuntimeActionPlan.cs`
  - `scripts/systems/battle/ai/BattleAiService.cs`
  - `scripts/systems/battle/ai/BattleAiScoreService*.cs`
  - `scripts/systems/battle/ai/BattleAiMutationGuard.cs`
  - `scripts/systems/battle/ai/BattleAiSkillAffordanceClassifier.cs`
  - `scripts/systems/battle/sim/*.gd`
  - `scripts/systems/battle/sim/*.cs`
  - `data/configs/skill_special_profiles/**/*.tres`
  - `data/configs/barriers/*.tres`
- 负责：开战、时间轴、命令 preview/issue、技能执行、战斗内换装、loot、评分、fate、battle-local 状态、simulation runner。
- 适合：指令生命周期、战斗流程、战斗结算、特殊技能 profile、战斗内装备事务、AI/手动时序、模拟链路。
- 邻接单元：CU-02、CU-10、CU-11、CU-12、CU-13、CU-14、CU-16、CU-17、CU-18、CU-20、CU-21。
- 不带：登录壳、共享背包窗口、battle board 展示，除非任务涉及界面。

### CU-16 战斗状态模型、边规则、伤害、AI 规则层

- 文件：
  - `docs/design/ai_candidate_evaluation_pipeline.md`
  - `docs/design/ai_readonly_candidate_pipeline_consensus_2026-05-19.md`
  - `scripts/systems/battle/core/BattleState.cs`
  - `scripts/systems/battle/core/BattleTimelineState.cs`
  - `scripts/systems/battle/core/BattleUnitConstants.cs`
  - `scripts/systems/battle/core/BattleUnitState.cs`
  - `scripts/systems/battle/core/BattleCellState.cs`
  - `scripts/systems/battle/core/BattleStatusEffectState.cs`
  - `scripts/systems/battle/core/BattleBarrier*.cs`
  - `scripts/systems/battle/core/BattleEdge*.cs`
  - `scripts/systems/battle/core/BattleEdgeFaceState.cs`
  - `scripts/systems/battle/core/BattleAttackRollModifierBundle.cs`
  - `scripts/systems/battle/core/BattleAttackRollModifierSpec.cs`
  - `scripts/systems/battle/core/AttackCheckInput.cs`
  - `scripts/systems/battle/core/AttackRollResult.cs`
  - `scripts/systems/battle/core/AttackContext.cs`
  - `scripts/systems/battle/core/AttackTraitTriggerResult.cs`
  - `scripts/systems/battle/core/AttackResolutionMetadata.cs`
  - `scripts/systems/battle/core/BattleRepeatAttackStageSpec.cs`
  - `scripts/systems/battle/core/BattleAttackCheckPolicyContext.cs`
  - `scripts/systems/battle/terrain/BattleTerrainRules.cs`
  - `scripts/systems/battle/terrain/BattleTerrainTopologyService.cs`
  - `scripts/systems/battle/terrain/BattleGridService.cs`
  - `scripts/systems/battle/terrain/BattleGridDistanceService.cs`
  - `scripts/systems/battle/terrain/BattleEdgeService.cs`
  - `scripts/systems/battle/terrain/BattleTerrainEffectState.cs`
  - `scripts/systems/battle/fate/BattleFateEventBus.cs`
  - `scripts/systems/battle/fate/BattleFateAttackRules.cs`
  - `scripts/systems/battle/fate/FateAttackFormula.cs`
  - `scripts/systems/battle/rules/BattleDamageResolver.cs`
  - `scripts/systems/battle/rules/BattleDeathResolutionRules.cs`
  - `scripts/systems/battle/rules/BattleEffectCategoryResolver.cs`
  - `scripts/systems/battle/rules/BattleExecutionRules.cs`
  - `scripts/systems/battle/rules/BattleDamagePreviewRangeService.cs`
  - `scripts/systems/battle/rules/BattleStatusModifierRules.cs`
  - `scripts/systems/battle/rules/BattleStatusSemanticTable.cs`
  - `scripts/systems/battle/rules/BattleTargetTeamRules.cs`
  - `scripts/systems/battle/rules/BattleHitResolver.cs`
  - `scripts/systems/battle/rules/BattleAttackCheckPolicyService.cs`
  - `scripts/systems/battle/rules/BattleRangeService.cs`
  - `scripts/systems/battle/rules/BattleEquipmentRequirementRules.cs`
  - `scripts/systems/battle/ai/*.cs`
  - `scripts/systems/battle/ai/BattleAiRuntimeActionPlan.cs`
  - `scripts/systems/battle/_interop/*.cs`
  - `scripts/systems/battle/ai/BattleAiActionIntent.cs`
  - `scripts/systems/battle/ai/BattleAiCandidateEvaluationService.cs`
  - `scripts/systems/battle/ai/BattleAiContext.cs`
  - `scripts/systems/battle/ai/BattleAiDecision.cs`
  - `scripts/systems/battle/ai/BattleAiDecisionEngine.cs`
  - `scripts/systems/battle/ai/BattleAiMoveToRangeCandidateEvaluator.cs`
  - `scripts/systems/battle/ai/BattleAiMutationGuard.cs`
  - `scripts/systems/battle/ai/BattleAiQueryService.cs`
  - `scripts/systems/battle/ai/BattleAiService.cs`
  - `scripts/systems/battle/ai/BattleAiScoreService*.cs`
  - `scripts/systems/battle/ai/BattleAiScoreContextAdapter.cs`
  - `scripts/systems/battle/ai/BattleAiTypedActionHelper.cs`
  - `scripts/systems/battle/ai/BattleAiUnitSkillCandidateEvaluator.cs`
  - `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
  - `scripts/systems/battle/runtime/BattleChargeResolver.cs`
  - `scripts/systems/battle/runtime/BattleMovementService.cs`
  - `scripts/systems/battle/runtime/BattleMovementQueryService.cs`
  - `scripts/systems/battle/runtime/BattleRepeatAttackResolver.cs`
  - `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`
  - `scripts/systems/battle/runtime/BattleBarrierGeometryService.cs`
  - `scripts/systems/battle/runtime/BattleSkillOutcomeCommitter.cs`
  - `scripts/systems/battle/runtime/BattleSpecialProfileCommitAdapter.cs`
  - `scripts/systems/battle/runtime/BattleGroundEffectService.cs`
  - `scripts/systems/battle/runtime/BattleTargetCollectionService.cs`
  - `scripts/systems/battle/runtime/BattleShieldService.cs`
  - `scripts/systems/battle/runtime/BattleMetricsCollector.cs`
  - `scripts/systems/battle/runtime/BattleRatingSystem.cs`
  - `scripts/systems/battle/runtime/BattleContributionEvent.cs`
  - `scripts/systems/battle/runtime/BattleContributionEventBuilder.cs`
  - `scripts/systems/battle/runtime/BattleContributionLedger.cs`
  - `scripts/systems/battle/terrain/BattleVirtualBoardOverlay.cs`
  - `scripts/systems/battle/runtime/TraitTriggerHooks.cs`
  - `scripts/player/progression/combat_effect_def.gd`
  - `scripts/enemies/actions/*.cs`
  - `scripts/systems/battle/core/WeaponDice.cs`
  - `scripts/systems/battle/core/WeaponProjection.cs`
  - `scripts/player/warehouse/WeaponProfileDef.cs`
  - `scripts/player/warehouse/WeaponDamageDiceDef.cs`
- 负责：BattleState 数据模型、terrain/edge/grid 规则、伤害/即死/死亡链、命中/豁免/状态语义、状态数值倍率、AI 评分、只读 candidate request/query/evaluator 管线、AI fail-loud/runtime fault 策略、AI state transition resolver、decision state patch/提交边界、runtime action plan 与技能 affordance 分类。
- 适合：战斗规则、伤害、即死判定、死亡来源优先级、命中、AI 评分、AI 状态转移、AI 行动生成、只读 AI candidate 管线、AI runtime fault/fail-loud 处理、AI 决策提交、terrain effect、状态语义、武器射程规则。
- 关系提示：AI damage scoring 必须通过 `BattleDamageResolver.preview_damage_sequence()` 读取正式伤害、save 分支、护盾吸收与稳定击杀口径；不要回退到 `BattleDamagePreviewRangeService.cs` 的范围估算。C# 内部闭环优先通过 `BattleTypedEnums.cs` 把 Godot 边界 `StringName` 解析成 enum，再进入评分、范围、目标过滤等 typed 分支；跨 GDScript/Resource/存档的 ID 字段仍保持 `StringName`。敌方 AI brain 的 C# `states` / `transition_rules` 属性不要按 `get_states()` / `get_transition_rules()` 方法调用；GDScript 边界优先读 `get_resolved_states()` 或原始属性。AI action 的 `.gd` 文件作为 C# action wrapper 的按路径加载实现脚本使用，不再声明同名 `class_name`。AI preview / score / candidate / move-cost 回调是 C# delegate 闭环，不要恢复 `Callable` provider 桥。地面范围技能同时存在实际威胁距离与 AI 站位距离合同：`BattleRangeService.get_effective_skill_threat_range()` 对齐 `battle_grid_service.get_area_coords()` 的最远可命中格，`get_effective_skill_distance_contract_range()` 供 `UseGroundSkillAction` 等站位合同使用。死亡律令 / execute 的分支合同由 `BattleExecutionRules.build_execute_plan()` 产出，`BattleDamageResolver` 只负责按 plan 执行 save、裂魂状态、穿盾即死与死亡来源标记。战斗评分不再消费 source-only 聚合伤害；正式路径应通过逐目标 `BattleContributionEvent` 快照进入 `BattleContributionLedger`，再由 `BattleRatingSystem` 归约正向评分、友伤、治疗敌方和友方击倒字段。`BattleBarrierService` 持有 `BarrierContentRegistry` 的运行时实例并负责释放；屏障运行时路径不要绕回 `GodotObject.Get` / `.Call()` 读取 `BattleRuntimeModule`。`BattleMovementService` 的移动执行、路径和 move-cost 热路径应保持 `BattleRuntimeModule` / `BattleGridService` / delegate 强类型闭环，不要恢复 `Callable` 或 grid `.Call()`。`BattleGroundEffectService` 与 `BattleMovementService` 的屏障交互调用应直接走 `BattleLayeredBarrierService` 强类型方法，不要恢复 `Resolve*Barrier*` 的 Godot `.Call()`；地面落点验证需要 cell 信息时优先用 `BattleGridService` 的 primitive 查询，避免把大量 `BattleCellState` wrapper 带出 grid 服务。
- 邻接单元：CU-13、CU-15、CU-17、CU-18、CU-20。
- 不带：战斗流程 sidecar，除非规则改动需要执行链验证。

### CU-17 战斗地形 profile、敌人 roster、prop 注入

- 文件：
  - `scripts/systems/battle/terrain/BattleTerrainGenerator.cs`
  - `scripts/systems/world/EncounterRosterBuilder.cs`
  - `scripts/systems/world/WildEncounterGrowthSystem.cs`
  - `scripts/utils/BattleBoardPropCatalog.cs`
  - `data/configs/enemies/rosters/*.tres`
  - `assets/main/battle/terrain/canyon/*.png`
- 负责：battle terrain 生成、roster 装配、prop catalog 注入。
- 适合：canyon 地形、spawn/roster、战斗 props、terrain profile。
- 邻接单元：CU-15、CU-16、CU-18、CU-20。
- 不带：HUD/棋盘渲染，除非 prop 或 terrain 视觉也变化。

### CU-18 战斗展示主链

- 文件：
  - `scenes/ui/battle_map_panel.tscn`
  - `scripts/ui/BattleMapPanel.cs`
  - `scripts/ui/BattleSkillSlotButton.cs`
  - `scripts/ui/BattleHoverPreviewOverlay.cs`
  - `scripts/systems/battle/presentation/BattleHudAdapter.cs`
  - `scenes/ui/battle_board_2d.tscn`
  - `scripts/ui/BattleBoard2D.cs`
  - `scripts/ui/BattleBoardRenderProfile.cs`
  - `scripts/ui/BattleBoardController.cs`
  - `scenes/common/battle_board_prop.tscn`
  - `scripts/ui/BattleBoardProp.cs`
  - `assets/main/battle/terrain/canyon/*.png`
- 负责：battle HUD、棋盘绘制、TileMap/prop/unit 渲染、相机、hover/overlay 展示；`WorldMapSystem` 向 `BattleMapPanel` 注入 typed runtime context，`BattleMapPanel` 再交给 `BattleHudAdapter`，不要恢复 Callable/provider/metadata 桥。
- 适合：battle HUD、棋盘、TileMap、相机、目标浮标、视觉层级。
- 邻接单元：CU-06、CU-15、CU-16、CU-17、CU-19、CU-20。
- 不带：progression、仓库规则，除非展示字段来自这些系统。

### CU-19 自动化回归与截图辅助

- 文件：
  - `tests/run_regression_suite.py`
  - `tests/shared/*.gd`
  - `tests/shared/*.cs`
  - `tests/equipment/run_*.gd`
  - `tests/warehouse/run_*.gd`
  - `tests/battle_runtime/**/*.gd`
  - `tests/battle_runtime/**/run_*.cs`
  - `tests/progression/**/*.gd`
  - `tests/runtime/**/*.gd`
  - `tests/text_runtime/**/*.gd`
  - `tests/world_map/**/*.gd`
  - `tests/battle_runtime/benchmarks/profile_seeds*.json`
  - `scripts/dev_tools/*.gd`
  - `tools/build_battle_sim_analysis_packet.py`
  - `tools/character_creation_reroll_simulation.gd`
  - `.codex/skills/battle-sim-analysis/SKILL.md`
- 执行约束：
  - 常规测试优先用 `python tests/run_regression_suite.py` 或相关 `godot --headless --script tests/.../run_*.gd` / `run_*.cs`。
  - 默认不要运行 `tests/battle_runtime/simulation/*`、`tests/battle_runtime/benchmarks/*`、`tests/text_runtime/tools/*`。
  - 只有用户明确要求 battle simulation、数值模拟、AI 对战模拟或平衡分析时，才运行 simulation / benchmark 入口。
- 负责：headless 回归、schema/runtime contract、测试 fixture、截图/签名辅助、文本命令回归、AI function-level profiling helper 与 profile gate 校验。
- 适合：为任意运行时改动补测试、定位回归入口、截图验收、AI hotspot/profile gate 工具链。
- 邻接单元：按业务域补 CU-10、CU-12、CU-15、CU-17、CU-18、CU-21 等。

### CU-20 敌方模板、AI brain、行动定义种子内容

- 文件：
  - `scripts/enemies/*.gd`
  - `scripts/enemies/AiActionTrace.cs`
  - `scripts/enemies/AiCandidateSummary.cs`
  - `scripts/enemies/AiCommandSummary.cs`
  - `scripts/enemies/EnemyAiActionHelper.cs`
  - `scripts/enemies/EnemyAiGenerationSlotDef.cs`
  - `scripts/enemies/EnemyAiBrainDef.cs`
  - `scripts/enemies/EnemyAiStateDef.cs`
  - `scripts/enemies/EnemyAiTargetSelectorRules.cs`
  - `scripts/enemies/EnemyAiTransitionRuleDef.cs`
  - `scripts/enemies/EnemyAiTransitionConditionDef.cs`
  - `scripts/enemies/EnemyContentSeed.cs`
  - `scripts/enemies/WildEncounterRosterDef.cs`
  - `scripts/enemies/actions/*.cs`
  - `scripts/systems/world/EncounterRosterBuilder.cs`
  - `scripts/player/warehouse/item_def.gd`
  - `scripts/player/warehouse/WeaponProfileDef.cs`
  - `scripts/player/warehouse/WeaponDamageDiceDef.cs`
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
  - `scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs`
  - `scripts/systems/game_runtime/headless/GameTextCommandRunner.cs`
  - `scripts/systems/game_runtime/headless/game_text_command_result.gd`
  - `scripts/utils/GameTextSnapshotRenderer.cs`
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
