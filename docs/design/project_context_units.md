# 当前 Godot 项目的上下文装载单元

更新日期：`2026-06-02`

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
- 负责：启动页、世界预设入口、存档列表、显示设置、建卡窗口到 `GameSession` 的入口；建卡窗口的 race/subrace 候选缓存消费 `CharacterCreationIdentityOptionService` 的 typed list 结果；`DisplaySettingsService` 是 plain C# typed settings service，分辨率选项与保存/加载请求使用 `DisplaySettings` / `ResolutionOption`，Godot Dictionary/Array 不再作为显示设置服务边界；`WorldPresetRegistry` 是 plain C# static preset catalog，正式读取面返回 typed `WorldPresetInfo`，Godot Dictionary/Array 只作为 UI/headless 投影，不再作为 Godot 注册类暴露。
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
- 负责：active save、slot meta、save payload、save index、全局内容注册表、world-level 装备实例 ID、battle save lock；`TrueRandomSeedService` 是 plain C# static helper，不再作为 Godot `RefCounted` / `GlobalClass` 暴露。
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
- 负责：世界生成、据点服务注入、遭遇锚点、挂载子地图事件；`WorldMapSpawnSystem` 是 plain C# generator service，通过 Godot Resource 配置读取内容后输出世界数据 Dictionary 边界，不再作为 Godot 注册类暴露；`WildEncounterGrowthSystem` 是 plain C# growth helper，正式入口消费 typed `IEnumerable<EncounterAnchorData>` 与 `IReadOnlyDictionary<StringName, WildEncounterRosterDef>`，不再从 `world_data` / roster Godot Dictionary 回读。
- 适合：世界生成规则、起始遭遇、据点/设施生成、mounted submap 事件。
- 邻接单元：CU-02、CU-03、CU-05、CU-06、CU-20。
- 不带：UI 窗口和战斗展示，除非任务要求进入场景。

### CU-05 世界网格与迷雾基础设施

- 文件：
  - `scripts/systems/world/WorldMapGridSystem.cs`
  - `scripts/systems/world/WorldMapFogSystem.cs`
  - `scripts/systems/world/WorldMapFogFactionState.cs`
  - `scripts/utils/WorldMapCellData.cs`
  - `scripts/utils/VisionSourceData.cs`
- 负责：世界网格、坐标、迷雾、视野来源；`WorldMapGridSystem` 是普通 C# 网格/footprint 计算器，`WorldMapFogSystem` 是普通 C# 迷雾状态 service，`WorldMapCellData` / `VisionSourceData` 是普通 C# 数据对象，不再作为 Godot `RefCounted`/`GlobalClass` 或 GDScript preload 边界；迷雾视野重建输入使用 typed `IEnumerable<VisionSourceData>`，paid reveal 返回 typed `List<Vector2I>`，持久化 `GDictionary` 只保留在 save/world-data 边界，需要 Godot Array 时只在调用边界投影。
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
  - `scripts/systems/game_runtime/IGameRuntimeSnapshotSource.cs`
  - `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs`
  - `scripts/systems/game_runtime/GameRuntimeCommandLogger.cs`
  - `scripts/systems/game_runtime/GameRuntimeBattleWritebackService.cs`
  - `scripts/systems/game_runtime/GameRuntimeBattleLootCommitService.cs`
  - `scripts/systems/game_runtime/GameRuntimeCharacterInfoBuilder.cs`
  - `scripts/systems/world/WorldMapDataContext.cs`
  - `scripts/systems/world/WorldTimeSystem.cs`
  - `scripts/systems/game_runtime/WorldMapRuntimeProxy.cs`
  - `scripts/systems/game_runtime/WorldMapSystem.cs`
  - `scripts/systems/settlement/*.cs`
  - `scripts/ui/RuntimeLogDock.cs`
  - `scripts/ui/SubmapEntryWindow.cs`
  - `scripts/utils/TrueRandomSeedService.cs`
  - `assets/main/basic_map/log.png`
- 负责：C# `GameRuntimeFacade` 持有 world/battle 模式、modal 状态、battle start context、据点/仓库/队伍/奖励/任务命令分发、headless snapshot 组织、战后回写与持久化边界；战后本地回写先物化 `BattleLocalWritebackResult`，正式结算链不再从回写 Dictionary 读取 `ok`；战后回写和仓库事务回滚使用 `PartyState.duplicate_state()` 做运行时深拷贝，不通过 `from_dict(to_dict())` 借存档 schema 克隆；battle loot commit 正式链使用 `BattleLootCommitResult` 聚合提交状态、日志和 snapshot，公开边界再组装 `Dictionary`；battle selection state 是 plain C# 状态对象，目标坐标/单位队列使用 `List<Vector2I>` / `List<StringName>`，只有 facade/UI 边界投影 Godot Array；command logger 是 plain C# helper，active command scope 留在 logger 内部 typed 对象，facade 不再持有公开 Dictionary scope；party / warehouse / quest / reward flow command handlers 是 plain C# runtime helpers，命令结果、奖励窗口与窗口 payload 的 Godot Dictionary/Array 只保留在 facade/UI 边界；snapshot builder 是 plain C# headless/UI 投影 helper，通过 `IGameRuntimeSnapshotSource` 消费 typed runtime source，Godot Dictionary/Array 只作为外层 snapshot/text 边界；character info builder 是 plain C# UI payload helper，人物信息 section 的 Godot Dictionary/Array 只作为 facade/UI 展示边界；`WorldMapRuntimeProxy` 是 plain C# scene adapter，负责把场景层命令/getter 转发到 runtime，只有 adapter 方法签名保留 Godot Dictionary/Array UI 边界；settlement handler 在正式执行链直接消费 typed `SettlementServiceResult`，`FromDictionary` 只保留给公开 Dictionary 边界，并用 typed validation / persist result / service metadata / stagecoach destination / runtime command result snapshot 处理正式命令流，把 pending character reward 物化委托给 `CharacterManagementModule`；shop service buy/sell 返回 typed trade result，商店窗口正式路径消费 typed `WarehouseInventoryEntry` 后再投影公开 Dictionary；forge service 正式路径使用 typed warehouse/party 参数构建窗口与执行配方，仓库预览/提交消费 typed `WarehouseBatchSwapResult`，配方物品校验用 typed result，misfortune forge guidance 通知经 `FateRuntimeModule` 解析为 `MisfortuneForgeGuidanceInput` 与 typed item-def index 后进入 service，公开 Dictionary 只留在 runtime adapter；research service 内部使用 typed member availability 与 typed service metadata 后再投影窗口 payload；quest command handler 从 `CharacterManagementModule` 消费 typed submit/claim result，并通过 typed `QuestDef` 读取任务定义字段；`WorldTimeSystem` 是 plain C# step/day 计算器，`world_step` 的 Godot Dictionary 读写保留在 `GameRuntimeFacade` / plain `WorldMapDataContext` 边界；GDScript/UI 边界通过 `is_world_coord_visible()` 查询迷雾，不把 plain `WorldMapFogSystem` 当 Godot API 返回给脚本；`WorldMapDataContext` 是 plain C# world-context helper，持有 active world 同步、子地图进出、迷雾存取、据点状态 `WorldMapSettlementStateData`、据点/NPC/事件/遭遇锚点 typed 查询与运行时 schema 校验；`WorldMapSystem.cs` 只作为 Godot 场景脚本层。
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
- 负责：队伍共享背包、堆叠/容量、物品/配方定义、技能书跨表校验、装备实例、装备/卸装、物品使用、装备掉落基础服务；`EquipmentRules` 是 plain static C# slot helper，内部 slot 顺序和存在性检查使用普通 `string[]` / `HashSet<string>`，正式 C# 调用使用 `GetAllSlotIdsTyped()` / `NormalizeSlotIdsTyped()`，公开查询结果再投影为 `StringName` / Godot Array；`EquipmentDurabilityRules` 是 plain static C# durability helper，不再作为 Godot `RefCounted` / `GlobalClass` 或 Godot Dictionary 规则表暴露；`EquipmentState` 内部装备槽持有 `Dictionary<StringName, EquipmentEntryState>`，正式 C# 调用使用 `GetEntrySlotIdsTyped()` / `GetFilledSlotIdsTyped()` / `GetOccupiedSlotIdsForEntryTyped()` / `SetEquippedEntryTyped()`，Godot Array 方法只作为 GDS/UI/serializer 边界投影，不要恢复 Godot Dictionary / `AsGodotObject()` entry 归一化或让生产 C# 回读 Godot Array；`EquipmentEntryState` 是 plain C# entry DTO，`occupied_slot_ids` 使用 `List<StringName>`，只在 `to_dict()` / UI/headless/battle event 边界投影 Godot Array；`WarehouseState` 当前仍保留旧 GDS 入口需要的 Godot Array 字段，但正式 C# 读侧应走 `GetStacksTyped()` / `GetEquipmentInstancesTyped()` / `GetNonEmptyStacksTyped()` / `GetNonEmptyEquipmentInstancesTyped()`，写侧应走 `AddStack()` / `AddEquipmentInstance()` / `RemoveStackAt()` / `RemoveEquipmentInstanceAt()` / `ReplaceStacks()` / `ReplaceEquipmentInstances()`，不要在 validator、runtime projection 或 service 逻辑里直接遍历或改写公开数组字段；`WarehouseStateItemValidator` 是 plain static C# warehouse-state validator，正式入口消费 `IReadOnlyDictionary<StringName, ItemDef>` 并返回 `List<string>`，通过 `WarehouseState` typed read-side 读取 stack / equipment instance，保持原索引错误文本，不要恢复 Godot Dictionary item-def 表或 `AsGodotObject()` 读取；`SkillBookItemFactory` / `SkillBookItemContentValidator` 是 plain static C# skill-book helpers，正式入口消费 typed `Dictionary<StringName, SkillDef/ItemDef>` 并返回 typed map / `List<string>`，不要恢复 Godot Dictionary 业务表、GDS preload、`RefCounted` 或 `GlobalClass`；`PartyWarehouseService` add/preview item 与 add equipment instance 的正式路径返回 `WarehouseAddItemResult`，allocated equipment ids 在内部保持 `List<string>`，公开 Godot API 再投影 Dictionary/Array；remove item / remove equipment instance 正式路径返回 `WarehouseRemoveItemResult`，batch swap 事务不要回读公开 Dictionary；装备实例索引 helper 返回 `List<int>`；容量计算遍历 `PartyState.get_member_states()`，不要回到 `member_states.Values` / `AsGodotObject()`；item-def setup 边界立即索引为 `Dictionary<StringName, ItemDef>`，服务内部不要持有 Godot Dictionary item cache；GDS `Callable` 装备实例 allocator 只在公开 setup 边界适配为 typed `Func<StringName>`；batch swap 的 Godot 数组入口只在边界解析为 typed `WarehouseBatchItemEntry` / `WarehouseBatchSwapResult`，装备和仓库内部 preview/commit 应直接传 typed batch entries，纯 item-id 的正式 C# 调用应传 `IReadOnlyList<StringName>` / `List<StringName>`，不要恢复 `Variant.From(...).AsGodotArray()` 往返或在任务提交/领奖路径构造 `GStringNameArray`；库存展示先生成 typed `WarehouseInventoryEntry`，公开 `get_inventory_entries()` 再投影 Godot Dictionary，内部事务不再恢复 untyped entry loop 或显式 `Variant` 解析；`PartyEquipmentService` 是 plain C# service，不再作为 Godot `RefCounted` / `GlobalClass` 或 GDS preload 边界；setup 边界立即索引为 `Dictionary<StringName, ItemDef>`，装备 preview 内部使用 typed result / `List<string>` blockers / `List<StringName>` occupied slots / displaced entry，并通过 `EquipmentRequirement.CheckResult()` 消费需求检查；`PartyItemUseService` 是 plain C# service，不再作为 Godot `RefCounted` / `GlobalClass` 或 GDS preload 边界；setup 边界立即物化 `Dictionary<StringName, ItemDef/SkillDef>`，正式使用路径返回 typed `PartyItemUseResult` 并通过 typed `WarehouseRemoveItemResult` 消费库存，只在公开 `use_item()` / options 边界投影或解析 Godot Dictionary。
- 迁移备注：`EquipmentRequirement` 本体是 `.tres` 装备需求资源，保留 `Resource` / `[GlobalClass]`；`EquipmentRequirementCheckResult` 是运行时 result，blockers 保持 `IReadOnlyList<string>` / `List<string>`，公开 `Check()` 才投影 Godot Dictionary/Array。
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
- 负责：`PartyState`、成员状态、技能/职业进度、属性快照、成就/任务/信仰状态、角色奖励 payload；`PartyState` 及其成员/进度/任务/奖励嵌套状态提供 typed `duplicate_state()`，供 runtime 回写、事务回滚等正式代码深拷贝使用；正式服务需要遍历成员时走 `PartyState.get_member_states()`。
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
  - `scripts/systems/progression/ProgressionContentBundleAdapter.cs`
  - `scripts/systems/progression/RacialSkillGrantService.cs`
  - `scripts/systems/progression/AgeStageResolver.cs`
  - `scripts/systems/progression/MisfortuneBlackOmenService.cs`
  - `scripts/systems/progression/QuestProgressService.cs`
  - `scripts/systems/progression/FaithService.cs`
  - `scripts/systems/progression/LevelGrowthEvaluationService.cs`
  - `scripts/systems/progression/LevelGrowthTriggerResult.cs`
  - `scripts/systems/progression/PracticeGrowthService.cs`
  - `scripts/systems/progression/PracticeSkillLearnStatus.cs`
  - `scripts/systems/progression/QuestCommandResultData.cs`
  - `scripts/systems/progression/AgeStageResolution.cs`
  - `scripts/systems/progression/AttributeGrowthResult.cs`
  - `scripts/systems/attributes/AttributeSourceContext.cs`
- 负责：角色管理门面、奖励归并、成就/任务进度、任务进度事件 typed 解析、任务定义正式 `StringName` key 查找、quest submit-item / reward claim 的 typed preview/result 物化，公开 GDictionary 边界只由 typed result `ToDictionary()` 组装，quest result 内部 id 集合使用 `List<StringName>`，只在边界投影 Godot Array；pending character reward 条目 typed 解析与聚合、属性成长 typed result 入账、active level trigger typed result 入账；`FaithService` 是 plain C# faith rank service，deity 索引使用 `Dictionary<StringName, FaithDeityDef>`，devotion 返回 `FaithDevotionResult`，rank reward 配置通过 `FaithRankRewardEntrySpec` typed 投影进入 pending reward，不要恢复 `RefCounted`/`GlobalClass`、GDS preload、snake_case API 或 Dictionary result；`BloodlineApplyService`、`AscensionApplyService`、`StageAdvancementApplyService`、`LevelGrowthEvaluationService`、`PracticeGrowthService` 与 `MisfortuneBlackOmenService` 是 plain C# service，不再作为 Godot `RefCounted`/`GlobalClass` 或 GDScript preload 边界，黑兆 hook 使用 typed `MisfortuneBlackOmenHookPayload` / `MisfortuneBlackOmenResult`，物品定义状态使用 `Dictionary<StringName, ItemDef>`；身份 apply 服务通过 `ProgressionContentBundleAdapter` 在 setup 边界把内容包转换为 `Dictionary<StringName, Def>` 后执行；技能定义缓存使用 `Dictionary<StringName, SkillDef>`，Godot Dictionary 只作为 setup 适配输入；practice track 集合使用 `HashSet<StringName>`，practice skill learn status / daily practice growth typed result 入账；有效年龄阶段 typed resolution 入账；`AgeStageResolver` 与 `RacialSkillGrantService` 是 plain static C# helper，阶段进阶输入消费 `IEnumerable<StageAdvancementModifier>`，身份技能 grant entry 与 active grant lookup 使用 typed DTO / `HashSet<string>`，`CharacterManagementModule` 收集 active stage modifiers 时使用 `List<StageAdvancementModifier>`，不再作为 Godot Array 业务边界；技能/职业/成就/物品/身份定义 typed index 查询，构建 `AttributeSourceContext` 时传递 typed skill/profession maps、装备属性 modifier list 与 stage modifier list；`PassiveSourceContext` 是 plain C# DTO，只携带 battle passive 投影实际消费的成员、进度与身份 Def 引用，通过显式 `IBattleRuntimeCharacterGateway` 实现进入 battle factory；战斗运行时通过 `IBattleRuntimeCharacterGateway.get_item_def()` 读取物品定义，身份内容 resolver 从内容包边界直接返回具体 Def 类型、身份应用、属性上下文、装备/技能/成长桥接。
- 适合：奖励入账、成就记录、任务推进、身份刷新、角色信息摘要、功法学习/同轨替换、跨系统成长接线。
- 邻接单元：CU-06、CU-08、CU-09、CU-10、CU-11、CU-13、CU-14、CU-15、CU-19。
- 不带：展示层，除非任务是窗口 payload。

### CU-13 progression 内容定义、条件模型、seed 内容

- 文件：
  - `scripts/player/progression/*Def.cs`
  - `scripts/player/progression/*Requirement.cs`
  - `scripts/player/progression/CombatSkillResourceCosts.cs`
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
- 负责：技能、职业、身份、血脉、升华、阶段进阶、成就、任务等静态内容与跨表校验；content validation runner 重载 fixture 目录时走 registry 公开目录加载入口（`load_from_directory()` / identity 多目录 `load_from_directories()`），不要在 GDScript 测试中清空或扫描 C# registry 私有 backing 字段；combat-targeting、target-team、damage-tag、battle-save、execute、attribute-growth、body-size、quest-provider、trait-trigger、skill-level-description 与 pending-character-reward content rule helper 是 plain static C#，合法集合使用 `HashSet<StringName>` / `IReadOnlySet<StringName>` / typed map，不再作为 Godot `RefCounted`/`GlobalClass` 或 Godot Dictionary 规则表暴露；`ProgressionDataUtils.sorted_string_keys()` 是 C# 内部 typed helper，返回 `List<string>`，`to_string_name_int_dictionary()` 返回 `Dictionary<StringName,int>`，不要恢复 Godot Array 返回值或 GDScript 直调边界；skill-level-description 校验核心通过 `CollectValidationErrors()` 返回 `List<string>`，不要恢复 Godot Array 追加式入口；`CombatEffectDef` 直接拥有正式 effect bool、基础骰、属性缩放骰面公式、追加伤害骰、DR bypass、低血阈值、击杀资源收益与状态语义字段（`counts_as_debuff_*` / `lock_counterattack` / `lock_crit` / `main_skill_lock_other_debuff_count`），`SkillContentRegistry` 必须拒绝这些字段继续写在 `params` 里；装备耐久伤害 effect 参数先物化 `EquipmentDurabilityDamageValidationParameters`，内容校验不要通过宽松 `DictBool` 把非 bool 当成 `require_damage_applied`。
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
  - `scripts/systems/progression/AttributeGrowthResult.cs`
  - `scripts/systems/progression/LevelGrowthEvaluationService.cs`
  - `scripts/systems/progression/LevelGrowthTriggerResult.cs`
  - `scripts/systems/progression/PracticeGrowthService.cs`
  - `scripts/systems/progression/PracticeSkillLearnStatus.cs`
  - `scripts/systems/progression/AgeStageResolution.cs`
  - `scripts/systems/progression/SkillLevelDescriptionFormatter.cs`
  - `scripts/systems/progression/CharacterCreationService.cs`
  - `scripts/systems/progression/CharacterCreationIdentityOptionService.cs`
  - `scripts/systems/progression/IdentityPayloadValidator.cs`
  - `scripts/player/progression/BodySizeContentRules.cs`
  - `scripts/systems/progression/AgeStageResolver.cs`
  - `scripts/systems/attributes/AttributeService.cs`
  - `scripts/systems/attributes/AttributeSourceContext.cs`
- 负责：成长规则、职业规则、技能合成、属性成长 typed result、active level trigger typed result、practice skill learn status、有效年龄阶段 typed resolution、属性快照、建卡、建卡身份候选、身份 payload 校验、体型、年龄阶段；`SkillEffectiveMaxLevelRules`、`AgeStageResolver`、`RacialSkillGrantService`、`CharacterCreationIdentityOptionService` 与 `IdentityPayloadValidator` 是 plain static C# 规则/helper，不再作为 Godot `RefCounted`/`GlobalClass` 或 GDScript preload 边界；`BodySizeContentRules` 是 plain static C# 体型规则表，category/body-size/footprint 集合使用 `IReadOnlySet` / `IReadOnlyDictionary`，C# 正式路径直接调用它，旧 `BodySizeRules` Godot wrapper 已移除，GDS 回归不得再 preload 它；建卡身份候选与身份 payload 校验内部使用 `Dictionary<StringName, Def>`、`HashSet<StringName>`、`IReadOnlyList<StringName>` / `IReadOnlyList<string>`，Godot Dictionary 内容源只在 content bundle / registry 边界通过 `ProgressionContentBundleAdapter` 物化；`AttributeGrowthService`、`LevelGrowthEvaluationService`、`PracticeGrowthService`、`ProfessionAssignmentService`、`ProfessionRuleService` 与 `SkillMergeService` 是 plain C# service，不再作为 Godot `RefCounted`/`GlobalClass` 或 GDScript preload 边界，年龄阶段 resolver、level trigger skill-def 缓存、practice skill-def 缓存、profession assignment skill/profession-def 缓存、profession rule skill/profession-def 缓存、skill merge skill-def 缓存、practice track 集合、profession accepted tag 去重、profession rule 候选 skill id 与 preview assignment 集合、racial grant entry/lookup、merge source id 规范化使用 C# typed 集合；`SkillLevelDescriptionFormatter` 是 plain static C# formatter，内部 effect/字段遍历使用 C# typed 集合，Godot Dictionary 只保留为 `.tres` 等级描述变量和 UI runtime context 投影；`AttributeSourceContext` 是 plain C# DTO，不再作为 Godot `RefCounted`/`GlobalClass` 暴露；`AttributeService` 的 source context、skill/profession def 缓存、equipment/passive/temporary modifier state 与 modifier pipeline 使用 typed `Dictionary` / `List` / record entry，Godot Dictionary/Array 只在 setup、setter 与永久属性 source context 边界解析；永久属性变更在入口处把 Godot source context 物化为 typed source，保护自定义属性写入判断不要在正式流程里回读 Dictionary bool。
- 适合：成长公式、属性公式、职业/技能规则、功法同轨替换规则、建卡规则、建卡身份候选、身份 payload 校验、体型派生。
- 邻接单元：CU-01、CU-09、CU-11、CU-12、CU-13、CU-15、CU-19。
- 不带：内容资源，除非规则和 seed 内容同时变化。

### CU-15 战斗运行时总编排

- 文件：
  - `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
  - `scripts/systems/battle/runtime/BattleTimelineDriver.cs`
  - `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`
  - `scripts/systems/battle/runtime/BattleChargeResolver.cs`
  - `scripts/systems/battle/runtime/BattleGroundSkillValidationResult.cs`
  - `scripts/systems/battle/runtime/BattleUnitSkillValidationResult.cs`
  - `scripts/systems/battle/runtime/BattleGroundEffectApplicationResult.cs`
  - `scripts/systems/battle/runtime/BattleMagicBacklashResult.cs`
  - `scripts/systems/battle/runtime/BattleTargetCollectionResult.cs`
  - `scripts/systems/battle/runtime/BattleMagicBacklashResolver.cs`
  - `scripts/systems/battle/runtime/TraitTriggerHooks.cs`
  - `scripts/systems/battle/fate/*.gd`
  - `scripts/systems/battle/fate/FateRuntimeModule.cs`
  - `scripts/systems/battle/fate/FortuneService.cs`
  - `scripts/systems/battle/fate/MisfortuneService.cs`
  - `scripts/systems/battle/fate/MisfortuneSkillCastResult.cs`
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
  - `scripts/systems/battle/ai/BattleAiScoreOrdering.cs`
  - `scripts/systems/battle/ai/BattleAiScoreService*.cs`
  - `scripts/systems/battle/ai/BattleAiMutationGuard.cs`
  - `scripts/systems/battle/ai/BattleAiSkillAffordanceClassifier.cs`
  - `scripts/systems/battle/sim/*.gd`
  - `scripts/systems/battle/sim/*.cs`
  - `data/configs/skill_special_profiles/**/*.tres`
  - `data/configs/barriers/*.tres`
- 负责：开战、时间轴、命令 preview/issue、技能执行、战斗内换装、loot、评分、fate、battle-local 状态、simulation runner；时间轴驱动正式路径通过 typed status tick / turn-control / trait dispatch result 判定 changed、defeat source、skip turn 与 AI override，公开 Dictionary 方法只保留给 Godot/GDScript 合同；`LowLuckRelicRules` 是 plain static C# relic/fate rules 与常量 holder，不再作为 Godot `RefCounted` / `GlobalClass` 或 GDScript preload 边界；`BattleSaveResolver` 是 plain static C# save resolver，save tag/mode 内容常量归 `BattleSaveContentRules`，正式路径通过 `BattleSaveContext` 传 skill 与 roll override，`BattleSaveSource` / `BattleSaveResult` / `BattleSaveProbabilityResult` 的 Godot Dictionary/Array 只通过 internal `ToDictionary()` 汇入 damage/runtime 公开边界，DTO public API 不暴露 Godot Dictionary/Array/Variant；不再暴露 GDScript preload / snake_case Dictionary API；隐藏路径可见 tag 使用 `IReadOnlySet<StringName>`。
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
  - `scripts/systems/battle/core/AttackPreviewData.cs`
  - `scripts/systems/battle/core/AttackCheckInput.cs`
  - `scripts/systems/battle/core/AttackRollResult.cs`
  - `scripts/systems/battle/core/AttackContext.cs`
  - `scripts/systems/battle/core/AttackTraitTriggerResult.cs`
  - `scripts/systems/battle/core/AttackResolutionMetadata.cs`
  - `scripts/systems/battle/core/AttackEffectResolutionResult.cs`
  - `scripts/systems/battle/core/CombatResourceKind.cs`
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
  - `scripts/systems/battle/rules/BattleDamagePreviewResult.cs`
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
  - `scripts/systems/battle/_interop/BattleTypedEnums.cs`
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
  - `scripts/systems/battle/runtime/BattleMovePathResult.cs`
  - `scripts/systems/battle/runtime/BattleMovementService.cs`
  - `scripts/systems/battle/runtime/BattleMovementQueryService.cs`
  - `scripts/systems/battle/runtime/BattleRepeatAttackResolver.cs`
  - `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`
  - `scripts/systems/battle/runtime/BattleGroundSkillValidationResult.cs`
  - `scripts/systems/battle/runtime/BattleUnitSkillValidationResult.cs`
  - `scripts/systems/battle/runtime/BattleGroundEffectApplicationResult.cs`
  - `scripts/systems/battle/runtime/BattleMagicBacklashResult.cs`
  - `scripts/systems/battle/runtime/BattleTargetCollectionResult.cs`
  - `scripts/systems/battle/runtime/BattleBarrierGeometryService.cs`
  - `scripts/systems/battle/runtime/BattleBarrierService.cs`
  - `scripts/systems/battle/runtime/BattleBarrierOutcomeResolver.cs`
  - `scripts/systems/battle/runtime/BattleSkillOutcomeCommitter.cs`
  - `scripts/systems/battle/runtime/BattleSpecialProfileCommitAdapter.cs`
  - `scripts/systems/battle/runtime/BattleGroundEffectService.cs`
  - `scripts/systems/battle/runtime/BattleTargetCollectionService.cs`
  - `scripts/systems/battle/runtime/BattleShieldService.cs`
  - `scripts/systems/battle/runtime/BattleMetricsCollector.cs`
  - `scripts/systems/battle/runtime/BattleRatingSystem.cs`
  - `scripts/systems/battle/runtime/BattleSkillMasteryService.cs`
  - `scripts/systems/battle/runtime/BattleSkillMasteryGrant.cs`
  - `scripts/systems/battle/runtime/BattleContributionEvent.cs`
  - `scripts/systems/battle/runtime/BattleContributionEventBuilder.cs`
  - `scripts/systems/battle/runtime/BattleContributionLedger.cs`
  - `scripts/systems/battle/terrain/BattleVirtualBoardOverlay.cs`
  - `scripts/systems/battle/runtime/TraitTriggerHooks.cs`
  - `scripts/player/progression/combat_effect_def.gd`
  - `scripts/enemies/actions/*.cs`
  - `scripts/systems/battle/core/WeaponDice.cs`
  - `scripts/systems/battle/core/WeaponProjection.cs`
  - `scripts/systems/battle/core/meteor_swarm/MeteorSwarmNumericSummary.cs`
  - `scripts/player/warehouse/WeaponProfileDef.cs`
  - `scripts/player/warehouse/WeaponDamageDiceDef.cs`
  - `scripts/utils/GodotVariantReadExtensions.cs`
- 负责：BattleState 数据模型、terrain/edge/grid 规则、伤害/即死/死亡链、命中/豁免/状态语义、状态数值倍率、AI 评分、只读 candidate request/query/evaluator 管线、AI fail-loud/runtime fault 策略、AI state transition resolver、decision state patch/提交边界、runtime action plan 与技能 affordance 分类；AI candidate request / evaluation service / move-to-range evaluator 是 plain C# typed payload/service/helper，正式路径不保留任意 metadata / Dictionary unknown key 往返。
- AI query 边界：`BattleAiQueryService` 是 sealed plain C# query helper，不再作为 `RefCounted` / `GlobalClass` 或 GDScript preload 边界；正式入口使用 `Setup()` 和 PascalCase typed query 方法，skill records 从 `BattleRuntimeModule.GetSkillDefIndexTyped()` 注入的 `IReadOnlyDictionary<StringName, SkillDef>` 构建，fail-loud metadata 使用 typed `Dictionary<string,string>`，action-score callback 的 Godot Dictionary metadata 只作为 score-input 外层投影边界。不要恢复 `setup()` / `get_*()` / `build_*()` snake_case API、`get_skill_record()` Dictionary 投影、skill-score callback、`GDictionary` alias 或 public Godot Dictionary/Array 查询方法。
- AI score service / context 适配边界：`BattleAiScoreService` 是 sealed plain C# score assembly helper，不再继承 `RefCounted` 或注册 `GlobalClass`，正式入口使用 internal `Setup()` / `SetProfile()` / `GetProfile()` / `GetBucketPriority()` / `BuildActionScoreInput()` / `BuildSkillScoreInput()`；`BattleAiScoreContextAdapter` 是 `internal sealed` plain C# helper，正式入口使用 `Setup()` / `BuildActionScoreInput()` / `BuildSkillScoreInput()`；`BattleAiScoreInput` 是 sealed plain C# DTO，不再作为 Godot `RefCounted` / `GlobalClass` 或 GDScript preload 边界，fingerprint、ordering facts 和 Godot Dictionary projection 只作为 internal C# helper；旧 `run_battle_runtime_ai_regression.gd` 不得再直接 `preload()` / `new()` score input，相关 ordering / ground-control 断言迁到 C# runner。内部 skill-def lookup 使用 `Dictionary<StringName, SkillDef>`，`IBattleAiScoreContext.skill_defs` 的显式 Godot Dictionary 投影仅保留给 score service 当前 score-input assembly 边界。不要恢复 score input / score service / adapter 的 `RefCounted`/`GlobalClass`、public `skill_defs`、`setup()` / `set_profile()` / `get_profile()` / `get_bucket_priority()` / `build_action_score_input()` / `build_skill_score_input()` / `_resolve_estimated_hit_rate_percent()` / `seal()` / `is_sealed()` / `matches_sealed_fingerprint()` / `to_move_to_range_ordering_facts()` / `to_dict()` snake_case public API，或在 adapter 内部恢复 `Variant` key 循环。score service 的 C# 正式 skill-score 入口使用 typed `IReadOnlyList<CombatEffectDef>` effect list；Godot Array effect_defs 只能作为旧 context/callback 入口的薄适配并立即解码，后续应继续把 score metadata 收敛为 typed metadata DTO；`BattleAiContext.merge_current_action_metadata()` 合并 typed runtime metadata 时必须保留 action_base_score / position_objective_kind 等 score metadata 顶层输入，不要把 score-service 输入截断成 runtime metadata；`WaitAction` 的 active-rest profile 是私有 typed C# 状态，trace/score metadata 只在外层边界投影。
- AI decision service/engine 边界：`BattleAiService` / `BattleAiDecisionEngine` 是 sealed plain C# helper，不再继承 `RefCounted`、注册 `GlobalClass` 或公开 `setup()` / `choose_command()` / `get_score_service()` / `choose_command_impl()` / `is_better_score_input()` 这类 GDScript-style API；`BattleAiService.Setup()` 从 `BattleRuntimeModule` 的 typed enemy brain index 接收 `IReadOnlyDictionary<StringName, EnemyAiBrainDef>`，服务内部不再保留 enemy brain `GDictionary` 或 string-key fallback。`BattleAiDecisionEngine` 决策 trace / runtime metadata 的 Godot Dictionary 投影仍只作为 trace/score-service 外层边界，正式 brain lookup 不要回读 Dictionary。
- 适合：战斗规则、伤害、即死判定、死亡来源优先级、命中、AI 评分、AI 状态转移、AI 行动生成、只读 AI candidate 管线、AI runtime fault/fail-loud 处理、AI 决策提交、terrain effect、状态语义、武器射程规则。
- 关系提示：AI damage scoring 必须通过 `BattleDamageResolver.preview_damage_sequence()` 读取正式伤害、save 分支、护盾吸收与稳定击杀口径；不要回退到 `BattleDamagePreviewRangeService.cs` 的范围估算。C# 内部闭环优先通过 `BattleTypedEnums.cs` 把 Godot 边界 `StringName` 解析成 enum，再进入评分、范围、目标过滤等 typed 分支；跨 GDScript/Resource/存档的 ID 字段仍保持 `StringName`。攻击命中输入、投掷结果、攻击 metadata、spell-control metadata 与重复攻击 stage 结果在 C# 闭环内分别使用 `AttackCheckInput`、`AttackRollResult`、`AttackResolutionMetadata`、`BattleSpellControlMetadata`、`AttackEffectResolutionResult`；不要在这些边界恢复字典 payload，旧 resolver 字典只允许在尚未改造的 Godot/report 入口被一次性读取。`FateAttackFormula` 与 `BattleFateAttackRules` 是 plain static C# helper，正式路径只调用 PascalCase API，测试用 `IRollSource` 注入掷骰；暴击锁读取 typed `BattleStatusEffectState.lock_crit`，不要恢复 RefCounted/GlobalClass、snake_case GDS wrapper、GDScript preload 或 status `params.lock_crit`。重复攻击 stage spec 是 C# 值类型，资源类型与每段资源消耗归属 `BattleRepeatAttackStageSpec.cost_resource_kind` / `stage_resource_cost`，不要放回 `AttackCheckInput` 或改回 `RefCounted` payload。敌方 AI brain 的 C# `states` / `transition_rules` 属性不要按 `get_states()` / `get_transition_rules()` 方法调用；GDScript 边界优先读 `get_resolved_states()` 或原始属性。AI action 的 `.gd` 文件作为 C# action wrapper 的按路径加载实现脚本使用，不再声明同名 `class_name`。AI runtime action plan 的 metadata 真相源是 typed `RuntimeActionMetadata` map；正式路径判断 generated / identity 时不要通过 `get_action_metadata()` 回读 Dictionary。`BattleAiSkillAffordanceClassifier` 是 plain C# service，正式路径只消费 `ClassifySkill()` 与 typed `BattleAiSkillAffordanceRecord`；不要恢复 `RefCounted`/`GlobalClass` 或 GDScript-style `classify_skill()` Dictionary API。`BattleAiStateResolver` 是 plain C# service，正式路径只消费 `ResolveTyped()` 与 typed `TransitionResult`；`BattleAiDecision` 是 sealed plain C# DTO，内部持有 typed transition / state patch，transition Godot Dictionary 只在 `BattleAiContext.build_turn_trace()` 输出边界投影。不要恢复 `RefCounted`/`GlobalClass`、GDScript-style `resolve()` Dictionary API、resolver 内 `GDictionary` / `GArray` alias、decision `transition` / `trace_counters` / `state_patch` Godot Dictionary mirror 或 transition DTO `ToDictionary()`。`BattleAiActionIntent` / `BattleAiSafetyGate` / `BattleAiScoreOrdering` 是 plain static C# helper，正式路径使用 PascalCase API（`IsValid()` / `DefaultFromSlotRole()` / `GetRejectionReason()` / `IsEligible()` / `IsBetter()`）；不要恢复 `RefCounted`/`GlobalClass` 或 `INTENT_*()`、`is_valid()`、`default_from_slot_role()`、`is_eligible()`、`get_rejection_reason()`、`is_better()` 这类 GDScript-style API。`BattleAiCandidateRequest` / `BattleAiCandidateEvaluationService` / `BattleAiMoveToRangeCandidateEvaluator` 是 plain C# typed request/service/helper，正式入口使用 `TryValidateMoveToRange()` / `Evaluate()` / `EvaluateMoveToRangeRequest()`；不要恢复 `RefCounted`/`GlobalClass`、`PathSearchBudget` / `TacticalParams` / `RuntimeMetadata` Godot Dictionary 属性、`RequireValidPayload()`、`setup()`、`evaluate()`、`evaluate_move_to_range_request()` 或 `register_evaluator()` snake_case API。`BattleAiDecisionCommitter` 是 plain static C# commit helper，正式入口使用 `Commit()` 并消费 `BattleAiDecision.StatePatch` / typed transition；不要恢复 `RefCounted`/`GlobalClass`、`commit()` / `build_state_patch()` / `validate_state_patch()` snake_case API 或从 Godot Dictionary patch/transition 回读提交状态。`BattleAiFailurePolicy` 是 plain static C# fail-loud policy，事件历史使用 `BattleAiFailureEvent` 与 `IReadOnlyDictionary<string, string>` metadata；`BattleAiPayloadGuard` 是 plain static boundary helper，只在 Godot payload 检查边界把 `GDictionary` metadata 投影为 typed string map，policy 内不要恢复 `RefCounted`/`GlobalClass`、`LastEvent`/`Events` Godot Dictionary/Array 或 Dictionary metadata 回读。`BattleAiUnitSnapshot` / `BattleAiUnitBlackboardSnapshot` 是 plain C# snapshot DTO，业务集合使用 `List<Vector2I>`、`List<StringName>`、`Dictionary<StringName,int>` 与固定 blackboard 字段，正式创建走 `FromUnit()`，`ToPayload()` 是唯一 Godot Dictionary/Array 投影；不要恢复 `RefCounted`/`GlobalClass`、`from_unit()` / `to_payload()` 兼容别名、`Godot.Collections.Array<BattleAiUnitSnapshot>` 查询结果或 snapshot 内部 Godot Dictionary/Array 状态。AI preview / score / candidate / move-cost 回调是 C# delegate 闭环，不要恢复 `Callable` provider 桥。`MoveToRangeAction` 构造 candidate request、读取 movement path budget 和正式路径查询结果时应走 `BattleAiCandidateRequest.SetMoveToRangeSections()`、`BattleMovementQueryService.TryBuildPathSearchBudgetTyped()` 与 `BattleGridService.resolve_unit_move_path_typed()`，不要恢复 `path_search_budget` / `tactical_params` / `runtime_metadata` / path result 的正式路径字典往返。`BattleRangeService` 是 plain static C# helper，正式路径只调用 PascalCase API，公开武器家族集合参数使用 `IEnumerable<StringName>`；地面范围技能同时存在实际威胁距离与 AI 站位距离合同：`BattleRangeService.GetEffectiveSkillThreatRange()` 对齐 `battle_grid_service.get_area_coords()` 的最远可命中格，`BattleRangeService.GetEffectiveSkillDistanceContractRange()` 供 `UseGroundSkillAction` 等站位合同使用，不要恢复 RefCounted/GlobalClass、snake_case API 或 Godot Dictionary/Array/Variant 公开参数。死亡律令 / execute 的分支合同由 `BattleExecutionRules.BuildExecutePlan()` 产出 `BattleExecutePlan`，`BattleDamageResolver` 只负责按 typed plan 执行 save、裂魂状态、穿盾即死与死亡来源标记。战斗评分不再消费 source-only 聚合伤害；正式路径应通过逐目标 `BattleContributionEvent` typed 快照进入 `BattleContributionLedger`，再由 `BattleRatingSystem` 归约正向评分、友伤、治疗敌方和友方击倒字段；贡献事件 DTO 使用 PascalCase 属性，只有 `ToDictionary()` / `ToDictionaryArray()` 在公开边界投影，正式记录入口使用 `RecordContributionFromUnits()` / `RecordBattleContributionResult()`。`BattleRuntimeModule` 的角色网关只通过 `GetCharacterGatewayTyped()` 进入正式路径。special profile 提交通用结果的目标结算与状态计时使用 `BattleCommonSkillTargetResult` / typed status map，`BattleSkillOutcomeCommitter` 不再回读 target result Dictionary。`BattleBarrierService` 持有 `BarrierContentRegistry` 的运行时实例并负责释放；屏障运行时路径不要绕回 `GodotObject.Get` / `.Call()` 读取 `BattleRuntimeModule`。`BattleUnitFactory` 只绑定 `BattleRuntimeModule` 和 `IBattleRuntimeCharacterGateway`，不要恢复 fake runtime / raw GodotObject 属性读取；passive 状态投影消费 plain `PassiveSourceContext` 与 runtime typed skill-def index，`PassiveStatusOrchestrator` / `RaceTraitResolver` / `AscensionTraitResolver` / `SkillPassiveResolver` 是 plain static helper，不再作为 Godot 注册类或 GDS preload 边界。`BattleMovementService` 的移动执行、路径和 move-cost 热路径应保持 `BattleRuntimeModule` / `BattleGridService` / delegate 强类型闭环，移动执行结果用 `BattleValidatedMoveExecutionResult`，不要恢复 `Callable`、grid `.Call()` 或执行结果 Dictionary 自读。`BattleGroundEffectService` 与 `BattleMovementService` 的屏障交互调用应直接走 `BattleLayeredBarrierService` 强类型方法，不要恢复 `Resolve*Barrier*` 的 Godot `.Call()`；地面落点验证需要 cell 信息时优先用 `BattleGridService` 的 primitive 查询，避免把大量 `BattleCellState` wrapper 带出 grid 服务。`GodotVariantReadExtensions.cs` 是删除 `GdInterop` 后的临时迁移 shim，只能作为待清理边界读法定位入口，新正式逻辑不要新增依赖。
- special profile gate 边界：`BattleSpecialProfileGate` 是 plain C# runtime gate，不再继承 `RefCounted` / 注册 `GlobalClass` 或作为 GDScript preload 服务；正式入口使用 `Setup()` / `PreflightSkill()` / `PreviewSkill()` / `CanExecuteSkill()`，`Setup()` 只在 registry snapshot 边界一次性解析成 typed profile map。`BattleSpecialProfileGateResult` 使用 PascalCase typed fields 与 `Dictionary<string, object>` debug details，Godot Dictionary/Array/Variant 只通过 internal `ToDictionary()` 汇入 Godot 边界投影，DTO public API 不暴露 Godot payload；不要恢复 `setup()` / `preview_skill()` / `can_execute_skill()` snake_case API、内部 Godot Dictionary snapshot/debug state 或 GDScript manifest gate runner。
- AI unit snapshot 投影边界：`BattleAiUnitSnapshot.ToPayload()` / `BattleAiUnitBlackboardSnapshot.ToPayload()` 只作为 internal Godot Dictionary/Array 边界投影，public API 不暴露 Godot Dictionary/Array/Variant；正式 AI 查询、candidate 和 move-to-range 路径消费 typed snapshot 字段。
- AI fail-loud metadata 边界：`BattleAiPayloadGuard` public API 只接收 typed `IReadOnlyDictionary<string,string>` metadata；Godot Dictionary metadata overload 只保留为 internal payload-check 边界并立即投影为 typed string map，`BattleAiFailurePolicy` 不回读 Dictionary。
- AI score context adapter 边界：`BattleAiScoreContextAdapter` 只在 `IBattleAiScoreContext.skill_defs` 显式接口上保留给 `BattleAiScoreService` 的 Godot Dictionary 投影，内部 skill lookup 使用 typed `Dictionary<StringName, SkillDef>`；score service 的 score metadata / effect defs 仍是 internal assembly 边界，不是 public API。不要恢复 `GDictionary` / `GArray` alias、public `skill_defs`、score service `RefCounted`/`GlobalClass` 或 string/StringName 双 key fallback。
- AI runtime action plan 边界：`BattleAiRuntimeActionPlan` 只暴露 typed action list、typed runtime metadata 和 typed skill-affordance records；Godot Array/Dictionary 投影只允许留在 `BattleAiContext` / decision trace 等外层适配面。不要恢复 plan 内部的 `GDictionary` / `GArray` alias、`GetActionsArray()`、`GetActionMetadataDictionary()`、`GetSkillAffordanceRecordDictionary()` 或 metadata `ToDictionary()` 投影方法。
- AI skill affordance 边界：`BattleAiSkillAffordanceRecord` 是 plain typed DTO，正式路径只通过 typed field/List 传递；`get_skill_affordance_record()` 的 Godot Dictionary 只在 `BattleAiContext` 公开边界投影。`EnemyAiGenerationSlotDef` 匹配技能 affordance 时使用 typed `MatchesAffordance(record, family)`；不要恢复 record 的 `FromDictionary()` / `ToDictionary()` / typed-key schema helper、slot 的 `matches_affordance(Dictionary)` wrapper，或在 assembler/runtime plan 内把 skill affordance record 退回 Godot Dictionary。
- enemy AI transition schema 边界：`EnemyAiTransitionRuleDef` / `EnemyAiTransitionConditionDef` 作为 `.tres` Resource 保留 Godot export Array，但 schema 校验内部使用 typed `HashSet<StringName>` / `List<string>` 与 PascalCase internal API；`BattleAiStateResolver` 和 runtime plan 只调用 `AppliesToState()` / `ValidateSchema()` / `ToSignature()` 与 typed predicate properties。不要恢复 `VALID_PREDICATES()` Godot Dictionary 表、condition `to_trace_dict()`、rule/condition public `validate_schema(Dictionary)`、`applies_to_state()` 或 `to_signature()` snake_case API。
- terrain timed tick 处理伤害结果和战报 summary 时先物化 typed snapshot（`TerrainDamageEffectResult` / `TerrainDamageSummary`），日志、贡献与击杀记录不要在正式循环里反复读 summary Dictionary bool。
- `BattleRepeatAttackResolver` 进入连斩正式循环前先用 `RepeatAttackRuntimeParameters` 解析停止条件与后续段伤害倍率；miss / target down 分支不要在循环里直接回读 repeat effect params bool。
- `BattleHitResolver` 的 repeat attack 命中检查复用 `BattleRepeatAttackStageSpec` 计算基础命中与追击惩罚；不要在 hit resolver 里重新解析 `exponential_penalty` 或 penalty-free params。
- `BattleGroundEffectService` 判断地面效果是否按武器攻击结算时先通过 `GroundEffectRuntimeParameters` 解析 `resolve_as_weapon_attack`；wind-push 的 affected/moved/recursion 查重状态使用 C# `HashSet<StringName>` / `HashSet<ulong>`，强制位移方向在正式 ground / special 执行链中用 `BattleForcedMoveContext` 传递，Godot Dictionary 只保留在 `_collect_*` / `_try_*` / `_apply_*` 调试或公开边界；地面技能执行链不要直接扫描 effect params bool。
- `BattleRuntimeModule` 的击倒、开战、结算 options 与 AI trace snapshot 由 `BattleRuntimeDictionaryOptions` 在入口处读取；runtime 正式流程不要散落 `Variant.AsBool()`。
- `BattleRuntimeSkillTurnResolver` 和 fate attack rules 的反击锁、暴击锁、主技能锁与 debuff 计数读取 `BattleStatusEffectState` typed 字段（`lock_counterattack` / `lock_crit` / `main_skill_lock_other_debuff_count` / `counts_as_debuff_*`）；不要恢复 status params helper 或从 `params` 字典读取这些状态语义。
- `BattleAiContext` / `BattleAiRuntimeActionPlan` / `BattleAiCandidateRequest` 的 runtime metadata 只接受固定 typed schema；不要恢复 `RuntimeMetadataValue`、unknown extra field、任意 `WriteToDictionary` 或 score projection object cache。
- `BattleAiMutationGuard` 只用显式 snapshot 容器检测和恢复 AI 决策副作用；AI blackboard、layered barrier、report/promotion 使用固定字段快照；layered barrier snapshot 通过 `BattleBarrierInstanceState.FromRuntimeDict()` 与 PascalCase typed DTO 字段物化，不要恢复 `from_runtime_dict()`、snake_case 字段、Godot Array/Dictionary 业务集合或 `RefCounted` / `GlobalClass` 屏障 state；`BattleUnitState` 正式 schema 不再包含 `shield_params` / `combo_state`；不要恢复 `RestorableValue` / `RestorableKey` / `Variant` / `GodotObject` 快照层，也不要为未知字典项做保真回写。
- `BattleSpawnReachabilityService` 是 plain C# service，不再继承 `RefCounted` / 注册 `GlobalClass` 或提供 GDScript `validate_state()` / Godot Dictionary wrapper；正式入口消费 `IReadOnlyDictionary<StringName, SkillDef>` 与 typed `BattleSpawnReachabilityOptions`，结果通过 `BattleSpawnReachabilityResult` / `BattleSpawnReachabilityUnitResult` 暴露 typed invalid unit ids 和 details，Godot Dictionary/Array 只通过 internal `ToDictionary()` 汇入 battle start failure 公开投影。
- damage preview / AI save scoring 使用 `BattleSaveProbabilityResult`、`BattleDamagePreviewResult`、typed preview save estimate 与 AI `DamagePreviewSnapshot`；公开 `preview_damage_effect()` / `preview_damage_sequence()` 只投影 Dictionary，AI 正式评分必须调用 typed preview，不要把 save probability、preview save estimate 或 damage preview 组装成 Dictionary 后再在正式路径回读 `has_save` / `immune` / `stable_lethal` / lethal 分支 bool。
- AI chain damage 评分路径使用 `ChainDamageParameters` 解析 `CombatEffectDef.@params`，目标 BFS 与半径判定不要在正式循环里直接回读 `prevent_repeat_target` / chain radius Dictionary bool。
- AI runtime action plan 对 authored/generated action 直接写入 typed `RuntimeActionMetadata`；自动生成 action 不要先构造 metadata Dictionary 再回读 `generated` / identity bool。
- `BattleRuntimeModule` 持有 AI action plan 时使用 C# typed map，正式 AI 上下文直接读取 `BattleAiRuntimeActionPlan`。
- 战斗 loot 生成随机装备实例时通过 plain C# `EquipmentDropService.RollItemInstances()` 消费 typed `List<EquipmentInstanceState>`。
- `BattleAiActionAssembler` / `BattleAiRuntimeActionPlan` 是 plain sealed C# 边界，不再继承 `GodotObject` / `RefCounted` 或注册 `GlobalClass`；正式入口使用 `BuildUnitActionPlan()`、`SetSource()`、`AddStateActions()`、`GetActions()`、`HasState()`、`IsStaleFor()`，不要恢复 `build_unit_action_plan()`、`set_source()`、`add_state_actions()`、`get_actions()`、`has_state()`、`is_stale_for()` 等 GDScript-style API。
- AI action assembler 与 `BattleAiRuntimeActionPlan` 之间直接传递 typed `BattleAiSkillAffordanceRecord`；不要恢复 `set_skill_affordance_record(Dictionary)` 或通过 Godot Dictionary skill-def index 构建正式 runtime action plan。
- 陨星雨 preview runtime 先构建 `MeteorSwarmNumericSummary` / `MeteorSwarmHostileTerrainConsequence` typed summary，AI 评分读取 typed facts；meteor damage effect 直接写 `CombatEffectDef.dice_count/dice_sides/pre_resistance_damage_multiplier`，不要恢复 `params.dice_count` / `params.dice_sides` / `params.runtime_pre_resistance_damage_multiplier`；`target_numeric_summary` / `friendly_fire_numeric_summary` 仅作为 UI/GDScript 公开 Dictionary 投影。
- `BattleDamagePreviewRangeService` 是 plain static C# preview helper，不再作为 `RefCounted` / `GlobalClass` 或 GDScript preload 边界；C# 内部只调用 `BuildSkillDamagePreview()` 并消费 typed `SkillDamagePreview` / `DamageEffectRange`，基础伤害骰读取 `CombatEffectDef.dice_count/dice_sides/dice_bonus`，damage range 明细只通过 internal `ToDictionary()` 在 HUD/runtime wrapper 边界投影 Godot Array，DTO public API 不暴露 Godot Dictionary/Array/Variant；AI fallback damage 估算不要调用 Godot Dictionary preview API 后再读 `has_damage` / `add_weapon_dice`。`WeaponDice` / `WeaponProjection` 是 plain C# DTO，`WeaponDice.FromDictionary()` / `WeaponDice.FromResource()` / `WeaponDice.ToDictionary()` / `WeaponProjection.ToDictionary()` 只作为 internal 武器投影边界转换，DTO public API 不暴露 Godot Dictionary/Array/Variant 或 Resource；不要恢复 `RefCounted` / `GlobalClass`、`from_dict()` / `from_resource()` / `to_dict()` / `is_empty()` snake_case DTO API 或 Godot 注册类。
- `BattleRangeService` 的 effect 武器标签/武器需求判定通过 `CombatEffectDef.requires_weapon` / `use_weapon_physical_damage_tag` typed 字段；射程、武器门槛与目标距离合同不要在正式分支直接读对应 `params` bool。
- `BattleAttackCheckPolicyContext`、`BattleAttackRollModifierSpec`、`BattleAttackRollModifierBundle` 与 `BattleAttackCheckPolicyService` 是 plain C# policy/modifier 族；policy service 正式 API 使用 `Setup()` / `BuildAttackContext()` / `BuildAttackCheck()` / `BuildAttackPreview()` / `BuildRepeatAttackStageContext()` / `BuildFateAwareRepeatAttackStageHitCheck()` / `ResolveStackedSpecs()`，stacking 输入输出使用 `IReadOnlyList<BattleAttackRollModifierSpec>` / `List<BattleAttackRollModifierSpec>`，bundle breakdown 暴露 typed `IReadOnlyList`，`AttackPreviewData` 内部 modifier breakdown 保存为 typed `List<BattleAttackRollModifierSpec>`，`AttackRollModifierBreakdown` 只返回 Godot Array 投影。`BattleAttackRollModifierSpec` / `BattleAttackRollModifierBundle` 的 Dictionary schema 转换 helper 只作为 internal 边界，DTO public API 不暴露 Godot Dictionary/Array/Variant。不要恢复 `RefCounted`/`GlobalClass`、GDS preload regression、snake_case policy API、Godot Array 作为 stacking 业务集合、public Godot payload DTO API，或在 policy service / `AttackPreviewData` 内部直接保存 preview breakdown Godot Array；policy service 单格读取应走 `BattleState.TryGetCellTyped()`。Godot Dictionary/Array 只保留为 accuracy modifier schema 与 preview breakdown 的外层 payload 投影。
- `BattleEffectCategoryResolver` 是 plain static C# effect-category helper，正式 API 接收 `IEnumerable<CombatEffectDef>` 并返回 `IReadOnlyList<StringName>`；`BattleBarrierService` 只在 Godot Array 入口一次性解析 effect defs，不要恢复 resolver 实例、`RefCounted`/`GlobalClass`、GDS preload regression 或 `params.barrier_categories` 旧读法。
- `BattleBarrierService` / `BattleLayeredBarrierService` 是 plain C# runtime service，不再继承 `RefCounted` 或注册 `GlobalClass`；`BattleRuntimeModule` 直接持有并调用 `Setup()` / `Dispose()`，不要恢复 GDS preload 边界、`GodotObject.Get` / `.Call()` 或 service 继承壳的 Godot 注册。
- `BattleTargetTeamRules` 是 plain static C# target-filter helper，正式路径使用 `ResolveEffectTargetFilter()` / `IsUnitValidForFilter()` / `IsBeneficialFilter()` / `IsEnemyFilter()`；不要恢复 `RefCounted`/`GlobalClass`、snake_case GDS wrapper 或 GDS preload 回归。
- `BattleSkillResolutionRules` 是 plain sealed C# skill-route/effect-selection helper，正式 API 使用 `BuildSkillResolutionPolicy()` / `Resolve*CastVariant()` / `Collect*EffectDefs()` / `ShouldResolveUnitSkillAsFateAttack()`，policy 内部持有 `IReadOnlyList<StringName>` / `IReadOnlyList<CombatEffectDef>`；Godot Dictionary/Array 只在 internal `BattleSkillResolutionPolicy.ToDictionary()` 边界投影，不要恢复 `RefCounted`/`GlobalClass`、`build_skill_resolution_policy()` snake_case API、Godot Array 业务集合、public Godot 投影 API 或让 HUD/runtime 从 policy Dictionary 回读。
- `BattleEquipmentRequirementRules` 是 plain static C# 装备需求规则 helper；盾牌需求调用点传入 `IReadOnlyDictionary<StringName, ItemDef>`，`BattleRuntimeModule.BuildItemDefIndexSnapshotTyped()` 只在 runtime item_defs 边界即时投影 Godot Dictionary，不要把 GDictionary/RefCounted/GlobalClass 恢复进规则层。
- `BattleSpecialSkillResolver` 不应在正式代码中使用 `Variant` / `GodotObject`；forced move 免疫读取 `BattleStatusEffectState.forced_move_immune`，方向上下文通过 `BattleForcedMoveContext` 进入正式选点/评分路径，击杀资源收益读取 `CombatEffectDef.ap_gain/free_move_points_gain`；敌方强制位移阻挡判定不要在正式逻辑里直接读状态 params bool。
- `FortuneService` 是 plain C# mark service，fate bus Dictionary 解析只在 `FateRuntimeModule` adapter，service 消费 `FortuneMarkEventInput`，确认骰通过 `FateAttackFormula.IRollSource` typed 注入；`FateRuntimeModule` 必须保持 Fortuna guidance 事件订阅先于 Fortune mark adapter，避免同一事件先授予 `fortune_marked` 再误解锁 guidance_true。不要恢复 FortuneService 的 `RefCounted`/`GlobalClass`、GDS preload、snake_case API、Dictionary payload 入口或旧 rng test hook。
- `FortunaGuidanceService` 是 plain C# guidance service，fate bus / chapter Dictionary 解析只在 `FateRuntimeModule` adapter，service 消费 `FortunaGuidanceEventInput` / `FortunaChapterCompletionInput` 并返回 C# `List<StringName>`；不要恢复 Fortuna guidance 的 `RefCounted`/`GlobalClass`、GDS preload、snake_case API 或 Dictionary payload 解析。
- `MisfortuneService` 的每战使用标记、已处理失败单位与成员 reason flags 是 C# `HashSet` / `Dictionary` 运行时状态，并通过 typed calamity snapshot 给 guidance 读取；`adjacent_ally_defeated` 通过 `HandleTrigger` 进入相邻队友倒地 reason 链；`MisfortuneGuidanceService` 是 plain C# guidance service，战斗/forge 解锁返回 C# `List<StringName>`，forge 输入使用 `MisfortuneForgeGuidanceInput` 和 `IReadOnlyDictionary<StringName, ItemDef>`；不要恢复为 Godot `RefCounted`/`GlobalClass`、GDS preload 边界、Godot Dictionary bool map、`DictBool` forge result 解析或 `GodotObject` item-def 中间模型。
- `LowLuckEventService` 是 plain C# low-luck 事件服务，不再作为 Godot `RefCounted` / `GlobalClass` 或 fate bus 订阅者；`FateRuntimeModule` 是 Godot Dictionary / `BattleState` / settlement context 的唯一适配层，服务本体消费 `LowLuckFateEventPayload`、`LowLuckBattleResolutionInput`、`LowLuckSettlementActionInput` 并返回 `LowLuckEventResult` / typed loot entries，battleId -> memberId 追踪表保持 C# `Dictionary<StringName, HashSet<StringName>>`；事件 payload bool 只在边界读取一次，不要恢复 GDScript preload、snake_case API、Dictionary result 或内部 Dictionary bool map。
- low-luck relic 的战斗规则直接调用 `LowLuckRelicRules.UnitHasFlag()` / `SnapshotHasFlag()` 与常量；不要恢复 snake_case GDS wrapper，也不要用 `BattleUnitState.to_dict()` -> `from_dict()` 克隆运行时单位来测试或传递破绽状态。
- equipment durability damage effect 在 `BattleDamageResolver` 内使用 typed `EquipmentDurabilityDamageEffectResult` 聚合是否有事件、耐久损失、销毁和 save result；对外事件 payload 才投影 Dictionary。
- `BattleDamageResolver` 的正式伤害应用、execute 分支、damage outcome、damage context、trait trigger 与 dice roll 分别使用 `AppliedDamageResult` / `ExecuteEffectResult` / `DamageOutcomeResult` / `DamageApplicationInput` / `DamageResolutionContext` / `TraitTriggerResultSnapshot` / `DicePoolRollResult`；预览、结算、即死、staged execute 和治疗/耐力骰不要先生成 Dictionary 再用 `DictBool` / `DictInt` 回读本地结果；治疗/体力恢复的属性缩放骰直接消费 `CombatEffectDef.dice_count/dice_sides_base/dice_sides_per_*_mod`，不要把 `power` 当骰子数量或恢复 `params.base_sides`。
- `BattleExecutionRules` 是 plain static C# execute helper，不再继承 `RefCounted` 或注册 `GlobalClass`；正式入口使用 `BattleExecutionRuleParams`、`BattleExecutePlan` 与 `BattleExecuteSoulFractureParams`，调用 `ResolveThreshold()` / `BuildExecutePlan()` / `ResolveNonLethalDamage()` / `IsBossTarget()` / `IsEliteOrBossTarget()`，不要恢复 GDScript-style snake_case API 或让 execute plan 通过 Godot Dictionary 往返。
- `BattleStatusSemanticTable` 是 plain static C# 状态语义表，正式 API 使用 `GetSemantic()` / `MergeStatus()` / `Get*()` / `Is*()` PascalCase 方法，状态模板通过 `BattleStatusSemantic` 值类型表达；不要恢复 `RefCounted`/`GlobalClass`、GDS preload regression、`get_semantic()` Dictionary payload、`merge_status(_typed)` snake_case 入口或 `advance_timeline_duration()` Dictionary wrapper。`CombatEffectDef.@params` / `BattleStatusEffectState.params` 仍只作为尚未迁完的资源/状态参数边界，不能把语义表本身退回 Godot Dictionary。
- `BattleStatusModifierRules` 是 plain static C# 状态倍率 helper，正式路径通过 `BattleUnitState.GetStatusEffectsTyped()` 读取状态集合，并通过 `BattleStatusEffectState.TryGetIntParam()` 读取固定 int 参数；不要恢复 `RefCounted`/`GlobalClass`、snake_case API、直接遍历 `status_effects.Values` 或在规则 helper 内使用 `GDictionary`。
- `BattleDeathResolutionRules` 是 plain static C# 死亡来源规则 helper，死亡来源和优先级使用 `DeathResolutionContext` 在正式路径传递；Godot damage event payload 只在 `BattleDamageResolver` 边界写入 `death_source` / `death_source_priority`，不要恢复规则层 `GDictionary` 读法或 `RefCounted`/`GlobalClass`。
- `BattleDamageResolver` 的武器伤害标签、附加武器骰、基础骰、追加伤害骰、DR bypass、低血阈值、驱散、装备耐久损伤和 staged execute 分支直接消费 `CombatEffectDef` typed 字段；剩余 `params` 只服务尚未迁移的专用 effect schema，不要新增通用 bool/dice Dictionary 读法。
- `BattleShieldService` 的屏障 HP 骰同样读取 `CombatEffectDef.dice_count/dice_sides/dice_bonus` 或属性缩放骰面公式，正式执行链的护盾 roll cache 使用 C# `Dictionary<long,int>`，Godot `shield_roll_context` 只在公开 `_apply_*` / `_resolve_*` 边界读写；属性缩放护盾不要把 `power` 当骰子数量，也不要恢复 `params.dice_*` / `params.base_sides` 作为正式配置入口。
- `BattleReportFormatter` 是 plain sealed C# formatter，不再继承 `RefCounted` 或注册 `GlobalClass`；Godot Dictionary/Array formatter 方法只作为 internal runtime/report wrapper 边界，public API 不暴露 Godot Dictionary/Array/Variant；正式入口使用 typed attack metadata 与 typed `AttackEffectResolutionResult` 重载，legacy report payload 只在尚未迁完的 wrapper 边界一次性读取，不要恢复 GDScript-style snake_case formatter API。伤害日志路径先构建 `DamageResultSummary`，其内部标签集合使用 C# `List<string>`，只在 internal `ToDictionary()` 投影边界转 Godot Array；攻击报告从 `BattleDamageResolver` 正式路径接收 `AttackResolutionMetadata`，日志文本、吸收原因、伤害倍率后缀与攻击原因判定不要在正式路径反复读 summary / attackResult Dictionary bool。
- damage dice event 聚合由 `DamageOutcomeResult` / `DamageApplicationInput` / `AppliedDamageResult.DamageDiceEvent` 携带 `DamageDiceEventSnapshot`，正式伤害应用和 `resolve_effects()` / 环境伤害聚合不要在本地 result 或 aggregate loop 内直接回读 damage event Dictionary bool；公开 preview/旧 payload 包装可在边界扫描。
- spell-control 检查入口在正式路径使用 `SpellControlCheckContext` / `BattleState + skill_id` 重载，命中解析优先调用 `BattleHitResolver.resolve_spell_control_metadata_typed()` 并消费 `BattleSpellControlMetadata` typed 字段构造 fate event payload/tag；`resolve_spell_control_metadata()` 的 Dictionary payload 只在 Godot/report 边界一次性物化，不要在 `BattleDamageResolver` 正式派发路径恢复 `DictBool` / `DictInt` 回读，也不要恢复 `BattleSpellControlMetadata.Payload` / `BattleSpellControlResult.SpellControl` Dictionary 状态。
- trait trigger 的 savage attacks 条件通过 `SavageAttacksContext` 物化 attack context，暴击与濒死触发在 `BattleDamageResolver` 正式路径直接消费 typed `AttackTraitTriggerResult` / `TraitTriggerResultSnapshot`；不要把 `critical_hit` / `add_weapon_dice` / `projected_hp` 组装成 Dictionary 后再回读 bool 结果。
- chain damage 半径/防重复目标和 spell-control backlash flags 在 `BattleSkillExecutionOrchestrator` 内通过 `ChainDamageParameters` / `BattleSpellControlResult` 物化；BFS、随机链候选池与连锁伤害目标池使用 typed unit list，正式分支不要直接回读 params/context bool，`BattleRuntimeModule` 的公开 Dictionary 参数只作边界转换。backlash ground drift 目标坐标在 `BattleMagicBacklashResolver.build_ground_backlash_target_coords_result()` 和 `BattleGroundBacklashTargetResult` 内使用 `IReadOnlyList<Vector2I>` / typed list，Godot Array 只在公开 wrapper 或 `ToDictionary()` 投影边界出现。
- skill mastery 的公开 Dictionary result 入口只在边界物化 `SkillMasteryResultSnapshot`；主动熟练度事件缓存使用 `SkillMasteryResolutionEvent`，guard grant 与金刚不坏受击 grant 的正式路径直接消费 `AttackEffectResolutionResult`，不要在正式 helper 内回读 result Dictionary bool。
- 熟练度 grant 的正式运行时应用使用 `BattleSkillMasteryGrant`，last-stand mastery 队列在 `BattleDamageResolver` 内持有 typed grant；guard grant 构建和 last-stand flush 后不要在 `BattleRuntimeModule` 内从 Dictionary 回读 `allow_unlocks` / `record_near_death_unbroken_manual`。
- `BattleRatingSystem` 的运行时统计以 `BattleRatingMemberStats` typed map 为真相源；技能成功、贡献归约、评分和奖励生成不要回到字段字典读写，`get_battle_rating_stats()` 仅作为展示/测试投影。
- `BattleMetricsCollector` / `BattleMetricsState` / `BattleMetricEntry` 是 plain C# metrics 运行时统计边界，`BattleRuntimeModule._battle_metrics` 持有 typed state；entry/state 的 `ToDictionary()` 只作为 internal 投影 helper，`BattleRuntimeModule.get_battle_metrics()` 才是公开 Godot Dictionary 边界。不要恢复 collector 的 `RefCounted`/`GlobalClass`、snake_case API、内部 Godot Dictionary 累积状态、public Godot Dictionary/Array DTO API，或让公开 metrics Dictionary 反向污染运行时统计。
- `BattleRuntimeModule.start_battle()` 的正式启动流程先把单位列表物化为 typed unit array，AI brain 读取走 runtime typed index；校验、摆放和 AI plan 构建不要回到通用对象读取 helper。
- 冲锋执行链直接消费 `BattleGroundSkillValidationResult` 的 typed direction / distance；Dictionary 校验结果只通过 internal `BattleGroundSkillValidationResult.FromDictionary()` 在 string-key 边界物化，不要在 `BattleSkillExecutionOrchestrator` 和 `BattleChargeResolver` 之间做 Dictionary 往返；`BattleTargetCollectionService` 是 plain C# typed target 收集服务，正式入口只用 `CollectCombatProfileTargetCoords()`，目标坐标/目标单位集合使用 `IEnumerable<Vector2I>` / `IEnumerable<BattleUnitState>`；`BattleTerrainTopologyService` 是 plain C# typed 水域拓扑服务，正式入口只用 `ReclassifyAllWaterTerrain()` / `ReclassifyWaterTerrainNearCoords()`，返回 `BattleTerrainTopologyChange` typed result，地形应用方直接消费 result 属性；`BattleUnitSkillValidationResult` / `BattleGroundSkillValidationResult` / `BattleTargetCollectionResult` 的 Godot Array/Dictionary 只通过 internal `To*Array()` / `ToDictionary()` 边界投影，正式路径消费 typed list / record 字段，DTO public API 不暴露 Godot Dictionary/Array/Variant。不要恢复 target collection service 的 `RefCounted`/`GlobalClass`、`collect_combat_profile_target_coords()` GDScript wrapper 或公开 Godot Array/Dictionary 入参；不要恢复 terrain topology service 的 `RefCounted`/`GlobalClass`、`reclassify_*` snake_case API、cell Dictionary 入参或 change Dictionary/Array 输出。
- `BattleGroundUnitEffectsResult` / `BattleGroundTerrainEffectsResult` / `BattleGroundWindPushResult` / `BattleShieldApplyResult` 是 plain C# result DTO，Godot Dictionary/Array 只通过 internal `ToDictionary()` 汇入公开 shield/ground wrapper 边界，DTO public API 不暴露 Godot Dictionary/Array/Variant；不要恢复 `GDictionary` / `GArray` alias 或让正式路径回读这些投影。
- `BattleMovePathResult` / `BattleValidatedMoveExecutionResult` 的路径真相源是 C# `IReadOnlyList<Vector2I>` / `List<Vector2I>`，`BattleMovePathTreeResult` 的 cost/previous/steps 保持 typed map；Godot Array/Dictionary 只在 internal `ToDictionary()` helper 或 movement/grid wrapper 边界投影，DTO public API 不暴露 Godot Dictionary/Array/Variant，AI move-to-range 正式路径不要恢复 Godot Array path 入参。
- 冲锋 path-step AOE 在进入命中循环前解析 `ChargePathStepAoeParameters`；循环内不要反复从 effect params 读取 `allow_repeat_hits_across_steps` / `resolve_as_weapon_attack` bool，武器攻击命中后的熟练度记录应消费 typed `AttackEffectResolutionResult`。
- `BattleBarrierGeometryService` 是 plain static C# 几何 helper，正式 API 使用 `ClassifyFootprintTransition()` / `LineCrossesBarrierArea()` / `CoordInsideBarrier()`，输入集合为 `IEnumerable<Vector2I>`，返回 `BattleBarrierFootprintTransition`；不要恢复 `RefCounted`/`GlobalClass`、GDS preload regression、`classify_footprint_transition()` Dictionary wrapper 或 Godot Array 作为几何业务集合。`BattleBarrierInstanceState` / `BattleBarrierLayerState` / `BattleBarrierOutcomeState` 与 `BattleBarrierOutcomeResolver` 是 plain C# typed runtime state/resolver，正式字段使用 PascalCase 与 C# list view；不要恢复 `RefCounted`/`GlobalClass`、snake_case DTO API、Godot Array 业务集合或 passage outcome Dictionary wrapper。barrier outcome 的公开 Dictionary 投影只作 Godot 边界包装；正式移动穿越与层效果路径使用 `BattleBarrierFootprintTransition`、`BattleBarrierPassageResult`、`BattleBarrierOutcomeResult`。
- AI blackboard 的正式运行标记（如 madness targeting、meteor protected ally、low-luck used flags）归属 `BattleAiBlackboard` typed 字段；Dictionary-like 写入口只接受严格 bool，AI 目标过滤、meteor 评分、黑星楔钉触发不要通过 Dictionary bool key 回读。
- special profile 提交边界：`BattleSpecialProfileCommitAdapter` 是 plain C# typed commit adapter，不再作为 `RefCounted` / `GlobalClass` 或 GDScript preload 边界；正式入口使用 `Setup()` / `Dispose()` / `CommitMeteorSwarmResult()`，直接从 `MeteorSwarmCommitResult` typed state 构造 `BattleCommonSkillOutcome`，不要恢复 `setup()` / `dispose()` / `commit_meteor_swarm_result()` snake_case API、`MeteorSwarmCommitResult.to_common_outcome_payload()`、提交 payload Dictionary 或 adapter 内 typed -> Dictionary -> typed 往返。
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
  - `tests/equipment/run_*.cs`
  - `tests/warehouse/run_*.gd`
  - `tests/battle_runtime/**/*.gd`
  - `tests/battle_runtime/**/run_*.cs`
  - `tests/progression/**/*.gd`
  - `tests/runtime/**/*.gd`
  - `tests/runtime/**/*.cs`
  - `tests/text_runtime/**/*.gd`
  - `tests/world_map/**/*.gd`
  - `tests/world_map/**/*.cs`
  - `tests/battle_runtime/benchmarks/profile_seeds*.json`
  - `scripts/dev_tools/*.gd`
  - `tools/build_battle_sim_analysis_packet.py`
  - `tools/character_creation_reroll_simulation.gd`
  - `.codex/skills/battle-sim-analysis/SKILL.md`
- 执行约束：
  - 常规测试优先用 `python tests/run_regression_suite.py` 或相关 `godot --headless --script tests/.../run_*.gd` / `run_*.cs`。
  - 默认不要运行 `tests/battle_runtime/simulation/*`、`tests/battle_runtime/benchmarks/*`、`tests/text_runtime/tools/*`。
  - 只有用户明确要求 battle simulation、数值模拟、AI 对战模拟或平衡分析时，才运行 simulation / benchmark 入口。
- 负责：headless 回归、schema/runtime contract、测试 fixture、截图/签名辅助、文本命令回归、AI function-level profiling helper 与 profile gate 校验；`GameTextSnapshotRenderer` 是 plain C# static snapshot renderer，不再作为 Godot `RefCounted` / GDScript preload 边界；装备基础回归入口是 `tests/equipment/run_party_equipment_regression.cs`，不要恢复为 GDScript preload 调用 `PartyEquipmentService`；世界生成 shared-content 回归入口是 `tests/world_map/runtime/run_world_map_shared_content_injection_regression.cs`，不要恢复为通过 GDScript preload 实例化 `WorldMapGridSystem`；fate attack formula 回归入口是 `tests/battle_runtime/fate/run_fate_attack_formula_regression.cs`，不要恢复为 GDScript preload 调用 `FateAttackFormula`；fate attack crit-lock / status typed field 回归入口是 `tests/battle_runtime/rules/run_status_effect_typed_fields_regression.cs` 与 `tests/battle_runtime/rules/run_battle_rule_status_param_schema_regression.cs`，不要恢复为 GDScript preload 调用 `BattleFateAttackRules` 或从 status params 读取 `lock_crit`；effect category resolver 回归入口是 `tests/battle_runtime/rules/run_battle_effect_category_resolver_contract_regression.cs`，不要恢复为 GDScript preload 调用 `BattleEffectCategoryResolver` 或读取 `params.barrier_categories`；execute rules 合同回归入口是 `tests/battle_runtime/rules/run_battle_execution_rules_contract_regression.cs`，不要恢复 `BattleExecutionRules` 的 `RefCounted`/`GlobalClass`、snake_case API 或 execute plan Dictionary 往返；execute effect 回归入口是 `tests/battle_runtime/runtime/run_battle_execute_effect_regression.cs`，不要恢复为 GDScript preload 调用 `BattleDamageResolver` 或只通过 Dictionary 检查死亡来源；black star brand、doom sentence 与 fate calamity drop runtime 回归入口分别是 `tests/battle_runtime/fate/run_black_star_brand_regression.cs`、`tests/battle_runtime/fate/run_doom_sentence_regression.cs` 和 `tests/battle_runtime/fate/run_fate_calamity_drop_regression.cs`，不要恢复为 GDScript preload 调用 `BattleRuntimeModule.setup()` / `GameRuntimeFacade` / `BattleLootConstants`、不要用 status Dictionary 直接写 fixture 状态，也不要用 `BattleUnitState.to_dict()` -> `from_dict()` 克隆测试状态；battle loot drop luck 回归入口是 `tests/battle_runtime/runtime/run_battle_loot_drop_luck_regression.cs`，不要恢复为 GDScript duck-typed `EquipmentDropService` spy 或 GDScript preload 调用 `GameRuntimeFacade` / `BattleRuntimeModule` 强类型边界；FaithService 回归入口是 `tests/progression/fate/run_faith_service_regression.cs`，不要恢复为 GDScript preload 调用 `FaithService`、snake_case API 或 devotion Dictionary result；FortuneService 回归入口是 `tests/progression/fate/run_fortune_service_regression.cs`，不要恢复为 GDScript preload 调用 `FortuneService` 或旧 rng test hook；Fortuna / Misfortune guidance 回归入口分别是 `tests/progression/fate/run_fortuna_guidance_regression.cs` 与 `tests/progression/fate/run_misfortune_guidance_regression.cs`，不要恢复为 GDScript preload 调用 `FortunaGuidanceService` / `MisfortuneGuidanceService` / `BattleLootConstants` 或 duck-typed RefCounted battle gateway；LowLuckEventService 回归入口是 `tests/progression/fate/run_low_luck_event_service_regression.cs`，low-luck relic 回归入口是 `tests/battle_runtime/fate/run_low_luck_relic_regression.cs`，不要恢复为 GDScript preload 调用 `LowLuckEventService` / `LowLuckRelicRules`、snake_case API、Dictionary result 或 C# interface setup。
- 状态语义回归入口是 `tests/battle_runtime/rules/run_status_effect_semantics_regression.cs`；不要恢复为 GDScript preload 调用 `BattleStatusSemanticTable`、snake_case API 或语义 Dictionary payload。
- barrier architecture 回归入口是 `tests/battle_runtime/runtime/run_barrier_architecture_contract_regression.cs`；不要恢复为 GDScript runner、只扫描 `.gd` 的合同、屏障 state/service `RefCounted`/`GlobalClass` 或 snake_case runtime dict API。
- barrier geometry 回归入口是 `tests/battle_runtime/runtime/run_barrier_geometry_contract_regression.cs`；不要恢复为 GDScript preload 调用 `BattleBarrierGeometryService`、snake_case API 或 geometry Dictionary result。
- prismatic sphere 行为回归入口是 `tests/battle_runtime/runtime/run_prismatic_sphere_regression.cs`；不要恢复为 GDScript preload 调用 `BattleRuntimeModule.setup()`、不要在测试 fixture 里直接把 barrier layer 当匿名 Dictionary 业务状态读写。
- battle metrics collector 回归入口是 `tests/battle_runtime/runtime/run_battle_metrics_collector_regression.cs`；不要恢复 `BattleMetricsCollector` 的 GDScript preload/`RefCounted`/`GlobalClass`、内部 metrics Godot Dictionary 状态或公开 metrics Dictionary 回读。
- battle target collection service 回归入口是 `tests/battle_runtime/runtime/run_battle_target_collection_service_regression.cs`；不要恢复 `BattleTargetCollectionService` 的 `RefCounted`/`GlobalClass`、GDScript-style `collect_combat_profile_target_coords()` wrapper、`BattleTargetCollectionResult` 的 public Godot Array/Dictionary 投影 API 或 Godot Array/Dictionary 公开参数。
- battle terrain topology service 回归入口是 `tests/battle_runtime/terrain/run_battle_terrain_topology_service_regression.cs`；不要恢复 `BattleTerrainTopologyService` 的 `RefCounted`/`GlobalClass`、GDScript-style `reclassify_*` API、Godot Dictionary cell 入参或 Godot Array/Dictionary change result。
- battle validation result 投影回归入口是 `tests/battle_runtime/runtime/run_battle_validation_result_projection_regression.cs`；不要恢复 `BattleUnitSkillValidationResult` / `BattleGroundSkillValidationResult` / `BattleTargetCollectionResult` 的 GDictionary alias、StringName-key fallback、public Godot Dictionary/Array 投影 API 或旧 `TargetCoordsArray()` / `ToGodotCoords()` 投影 API。
- battle ground/shield result 投影由 `tests/battle_runtime/runtime/run_battle_ground_effect_typed_sets_regression.cs` 与 `tests/battle_runtime/runtime/run_battle_shield_service_typed_context_regression.cs` 覆盖；不要恢复 ground effect / shield result DTO 的 `GDictionary` / `GArray` alias、public Godot Dictionary/Array 投影 API、roll context `Variant.From` key 读取或正式路径投影回读。
- battle move path result 投影回归入口是 `tests/battle_runtime/runtime/run_battle_move_path_result_projection_regression.cs`；不要恢复 `BattleMovePathResult.Path` / `BattleValidatedMoveExecutionResult.ExecutedPath` 的 Godot Array 业务状态、`GDictionary` / `GVector2IArray` result alias 或 AI move-to-range Godot Array path 入参。
- battle spawn reachability 回归入口是 `tests/battle_runtime/runtime/run_battle_spawn_reachability_regression.cs`；不要恢复 `BattleSpawnReachabilityService` 的 `RefCounted`/`GlobalClass`、GDScript preload 回归、`validate_state()` / `ValidateState()` Dictionary wrapper、Godot Dictionary skill-def 入参或 result DTO 的 public Godot Dictionary/Array 投影 API，业务断言应直接消费 typed result。
- magic backlash 回归入口是 `tests/battle_runtime/skills/run_magic_backlash_regression.cs`，命中 BAB / spell-control roll 回归入口是 `tests/battle_runtime/rules/run_battle_hit_resolver_bab_regression.cs`；不要恢复 `BattleSpellControlMetadata.Payload`、`BattleSpellControlResult.SpellControl` Godot Dictionary、`resolve_spell_control_metadata()` 正式路径回读或 backlash drift 结果中的 Godot Array 业务状态。
- damage preview range 回归入口是 `tests/battle_runtime/rules/run_battle_damage_preview_range_contract_regression.cs`；不要恢复 `BattleDamagePreviewRangeService` 的 `RefCounted`/`GlobalClass`、GDScript preload runner、`build_skill_damage_preview()` / `build_skill_damage_preview_typed()` snake_case API、Godot Array typed 入口、`SkillDamagePreview` / `DamageEffectRange` public Godot 投影 API，或 `WeaponDice` / `WeaponProjection` 的 public Godot payload/Resource conversion API、Godot 注册 DTO 形态。
- attack roll modifier 回归入口是 `tests/battle_runtime/rules/run_attack_roll_modifier_bundle_regression.cs`；不要恢复 `BattleAttackRollModifierSpec` / `BattleAttackRollModifierBundle` 的 public Godot Dictionary/Array/Variant DTO API、Godot Array stacking 业务集合或 GDScript preload runner。
- battle range service 合同回归入口是 `tests/battle_runtime/rules/run_battle_range_service_contract_regression.cs`；不要恢复 `BattleRangeService` 的 `RefCounted`/`GlobalClass`、GDScript preload/smoke 直连、snake_case API 或 Godot Dictionary/Array/Variant 公开参数。
- skill resolution rules 回归入口是 `tests/battle_runtime/rules/run_battle_skill_resolution_rules_regression.cs`；不要恢复 `BattleSkillResolutionRules` / `BattleSkillResolutionPolicy` 的 `RefCounted`/`GlobalClass`、`build_skill_resolution_policy()` snake_case API、Godot Array policy 业务状态、public Godot Dictionary/Array 投影 API，或让 HUD/runtime 从 policy Dictionary 回读正式字段。
- battle report formatter 合同回归入口是 `tests/battle_runtime/rules/run_battle_report_formatter_contract_regression.cs`；不要恢复 `BattleReportFormatter` 的 `RefCounted`/`GlobalClass`、snake_case formatter API、public Godot Dictionary/Array/Variant API、`GDictionary` / `GArray` test alias 或 formatter 内部 Godot Array 标签状态。
- battle save resolver 回归入口是 `tests/battle_runtime/runtime/run_battle_save_resolver_regression.cs`，damage preview contract 回归入口是 `tests/battle_runtime/rules/run_battle_damage_resolver_preview_contract_regression.cs`；不要恢复 `BattleSaveResolver` 的 `RefCounted`/`GlobalClass`、GDScript preload runner、`resolve_save()` / `resolve_save_result()` / `estimate_save_success_probability()` snake_case Dictionary API、save tag/mode mirror 常量方法、`BattleSave*` result DTO public Godot 投影 API、`GDictionary` / `GArray` alias、string/StringName 双 key fallback，或让 save resolver 从 Godot Dictionary context 回读正式字段。
- meteor swarm manifest gate 回归入口是 `tests/battle_runtime/runtime/run_meteor_swarm_manifest_gate_regression.cs`；不要恢复 GDScript preload runner、旧 `BattleSpecialProfileGateResult` snake_case/Godot Dictionary debug surface 或 public Godot Dictionary/Array/Variant 投影 API。
- meteor swarm commit payload boundary 回归入口是 `tests/battle_runtime/runtime/run_meteor_swarm_commit_payload_boundary_regression.cs`；不要恢复 GDScript preload runner、`BattleSpecialProfileCommitAdapter` 的 `RefCounted`/`GlobalClass`/snake_case API、`MeteorSwarmCommitResult.to_common_outcome_payload()` 或提交 payload Dictionary 回读。
- resource validation 回归入口是 `tests/runtime/validation/run_resource_validation_regression.gd` / `tests/runtime/validation/content_validation_runner.gd`；fixture 目录重载应调用 registry 公开目录加载 API，enemy invalid coverage 包含 `tests/fixtures/enemy_content/invalid_roster_initial_stage/` 与 `tests/fixtures/enemy_content/invalid_skill_level_map/`，不要恢复 runner 对 C# registry 私有字段或 `_scan_directory()` 的直接访问。
- enemy template runtime start 回归入口是 `tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs`；不要把敌方模板 stable id / 初始 AI 状态 / 初始技能 / stamina 池或 `BattleUnitFactory` 禁止 fallback enemy 的断言恢复到 `run_battle_runtime_ai_regression.gd` 的 GDScript `BattleRuntimeModule.setup()` / `start_battle()` preload 路径。
- enemy AI generation slot schema/content 回归入口是 `tests/battle_runtime/ai/run_enemy_ai_generation_slots_schema_regression.cs` 与 `tests/battle_runtime/ai/run_enemy_ai_generation_slots_content_regression.cs`；不要恢复为 GDScript preload 调用 `EnemyAiGenerationSlotDef` / `EnemyContentRegistry` / action resources，也不要恢复 `EnemyAiTargetSelectorRules` 的 Godot Dictionary/Array wrapper 或 `EnemyAiGenerationSlotDef.VALID_*()` Dictionary validation tables。
- battle AI action intent / safety gate 回归入口是 `tests/battle_runtime/ai/run_battle_ai_action_intent_safety_gate_regression.cs`；不要恢复 `BattleAiActionIntent` / `BattleAiSafetyGate` 的 GDScript-style snake_case API、`RefCounted` 或 `GlobalClass`。
- battle AI score ordering 回归入口是 `tests/battle_runtime/ai/run_battle_ai_score_ordering_regression.cs`；不要恢复 `BattleAiScoreOrdering` 的 GDScript-style `is_better()` API、`RefCounted` 或 `GlobalClass`。
- battle AI role-threat / lethal-threat scoring 回归入口是 `tests/battle_runtime/ai/run_battle_ai_role_threat_scoring_regression.cs`；不要把 role-threat target priority、lethal threat target count 或 low-HP formal bonus 参数断言恢复到 `run_battle_runtime_ai_regression.gd` 的 GDScript score-service preload 路径。
- battle AI failure policy 回归入口是 `tests/battle_runtime/ai/run_battle_ai_failure_policy_regression.cs`；不要恢复 `BattleAiFailurePolicy` 的 `RefCounted`/`GlobalClass`、Godot Dictionary/Array event state、Dictionary metadata 入参或旧 GDScript preload 回归。
- battle AI mutation guard / decision commit 回归入口是 `tests/battle_runtime/ai/run_battle_ai_mutation_guard_regression.cs`；不要恢复 `BattleAiDecisionCommitter` 的 `RefCounted`/`GlobalClass`、GDScript preload 回归、snake_case API 或从 Godot Dictionary state patch 回读提交状态。
- battle AI query service 回归入口是 `tests/battle_runtime/ai/run_battle_ai_query_service_regression.cs`；不要恢复 `BattleAiQueryService` 的 `RefCounted`/`GlobalClass`、GDScript-style public query API、`get_skill_record()` Dictionary 投影、skill-score callback 或从 Godot Dictionary skill defs 构建正式 skill records。
- battle AI score context adapter 回归入口是 `tests/battle_runtime/ai/run_battle_ai_score_context_adapter_regression.cs`；不要恢复 `BattleAiScoreContextAdapter` 的 `RefCounted`/`GlobalClass`、public `skill_defs`、snake_case API、`Variant` key 循环或把 live `SkillDef` 留在 score input 出口。
- battle AI save probability scoring 回归入口是 `tests/battle_runtime/ai/run_battle_ai_score_save_probability_regression.cs`；不要恢复 GDScript preload runner、score service `build_skill_score_input()` GDS API 或把 effect_defs 主入口退回 Godot Array。
- battle AI score input metrics 回归入口是 `tests/battle_runtime/ai/run_battle_ai_score_input_metrics_regression.cs`；不要把空地控场 target count / ground control score、地面技能资源与站位指标、友伤地面有效目标数、链闪友伤预估或 fate-aware repeat attack 成功率断言恢复到 `run_battle_runtime_ai_regression.gd` 的 GDScript `build_skill_score_input()` 路径。
- battle AI score selection 回归入口是 `tests/battle_runtime/ai/run_battle_ai_score_selection_regression.cs`；不要把 melee 后声明高分技能选择、ranged 单体技能压过单体范围技能、unit skill 多目标 hit payoff 选择断言恢复到 `run_battle_runtime_ai_regression.gd` 的 GDScript `build_skill_score_input()` 路径。
- battle AI enemy template runtime 回归入口是 `tests/battle_runtime/ai/run_battle_ai_enemy_template_runtime_regression.cs`；不要把正式模板 start_battle stable id、wolf stamina、fallback enemy 禁止、pressure 技能 probe、depleted basic_attack fallback 或 canonical template id 断言恢复到 `run_battle_runtime_ai_regression.gd` 的 GDScript `BattleRuntimeModule.setup/start_battle` 路径。
- battle AI unit snapshot 回归入口是 `tests/battle_runtime/ai/run_battle_ai_unit_snapshot_regression.cs`；不要恢复 `BattleAiPayloadGuard` / `BattleAiUnitSnapshot` / `BattleAiUnitBlackboardSnapshot` 的 `RefCounted`/`GlobalClass`、public Godot Dictionary/Array/Variant API、snapshot 内部 Godot Dictionary/Array 状态或 `Godot.Collections.Array<BattleAiUnitSnapshot>` 查询结果。
- enemy AI transition schema 回归入口是 `tests/battle_runtime/ai/run_enemy_ai_transition_schema_regression.cs`；不要恢复 GDScript preload runner、`EnemyAiTransitionConditionDef.VALID_PREDICATES()` Godot Dictionary 表、condition `to_trace_dict()`、rule/condition public `validate_schema(Dictionary)` 或 transition rule/condition snake_case API。
- battle AI state resolver 回归入口是 `tests/battle_runtime/ai/run_battle_ai_state_resolver_regression.cs`；不要恢复 `BattleAiStateResolver` 的 `RefCounted`/`GlobalClass`、GDScript preload 回归、`resolve()` Dictionary API、`GDictionary` / `GArray` alias 或 transition DTO `ToDictionary()` 投影。
- battle AI skill affordance classifier 回归入口是 `tests/battle_runtime/ai/run_battle_ai_skill_affordance_classifier_regression.cs`；不要恢复 `BattleAiSkillAffordanceClassifier` 的 `RefCounted`/`GlobalClass`、GDScript preload 回归、`classify_skill()` Dictionary API、`BattleAiSkillAffordanceRecord` 的 Dictionary conversion API 或 `EnemyAiGenerationSlotDef.matches_affordance(Dictionary)` wrapper。
- battle AI action assembler plan 回归入口是 `tests/battle_runtime/ai/run_battle_ai_action_assembler_plan_regression.cs`；不要恢复 `BattleAiActionAssembler` 的 `RefCounted`/`GlobalClass`、GDScript preload 回归、`build_unit_action_plan()` snake_case API、Godot Dictionary skill-def 入参或 `GodotObject.Set()` 属性写法。
- battle AI runtime action plan 回归入口是 `tests/battle_runtime/ai/run_battle_ai_runtime_action_plan_regression.cs`；不要恢复 `BattleAiRuntimeActionPlan` 的 `RefCounted`/`GlobalClass`、GDScript preload 回归、public Godot Array/Dictionary mirror state 或 `set_source()` / `get_actions()` / `get_action_metadata()` snake_case API。
- battle AI trace summary 回归入口是 `tests/battle_runtime/ai/run_battle_ai_trace_summary_regression.cs`；action trace 运行时状态走 `AiActionTrace` / `AiCandidateSummary` / `AiCommandSummary` typed DTO，`EnemyAiActionHelper` 是 internal static helper，`BattleAiContext` 内部持有 typed trace list，仅 `action_traces` / turn trace 输出时投影 Godot Dictionary/Array；不要恢复这些 trace DTO/helper 的 `RefCounted`/`GlobalClass`、public Godot 集合字段、public Godot Dictionary/Array 投影 API、`EnemyAiActionHelper` public GDScript-style API、trace 状态 Dictionary 往返，或 `to_dict()` / `from_command()` / `is_empty()` snake_case API。
- battle AI unit skill candidate evaluator 回归入口是 `tests/battle_runtime/ai/run_battle_ai_unit_skill_candidate_evaluator_regression.cs`；不要恢复 `BattleAiUnitSkillCandidateEvaluator` 的 `RefCounted`/`GlobalClass` 或 `evaluate()` snake_case API。
- move-to-range candidate request/progress 回归入口是 `tests/battle_runtime/ai/run_move_to_range_progress_regression.cs`；不要恢复 `BattleAiCandidateRequest` / `BattleAiCandidateEvaluationService` / `BattleAiMoveToRangeCandidateEvaluator` 的 `RefCounted`/`GlobalClass`、GDScript preload 回归、Godot Dictionary section 属性或 snake_case evaluator API。
- 适合：为任意运行时改动补测试、定位回归入口、截图验收、AI hotspot/profile gate 工具链。
- 邻接单元：按业务域补 CU-10、CU-12、CU-15、CU-17、CU-18、CU-21 等。

### CU-20 敌方模板、AI brain、行动定义种子内容

- 文件：
  - `scripts/enemies/*.gd`
  - `scripts/enemies/AiActionTrace.cs`
  - `scripts/enemies/AiCandidateSummary.cs`
  - `scripts/enemies/AiCommandSummary.cs`
  - `scripts/enemies/TraceDictionaryProjection.cs`
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
- 负责：敌方模板、AI brain/state/action/generation slot/transition rule、wild encounter roster、敌方攻击装备和掉落静态内容；`EnemyAiTargetSelectorRules` 是 plain static C# selector helper，固定 selector ID 用 `HashSet<string>` / `StringComparer.Ordinal` 校验，`EnemyAiGenerationSlotDef` 仍是 `.tres` Resource 但 generation slot schema 枚举表不再公开为 Godot Dictionary，runtime action plan 使用 typed string slot signature 而不是 Dictionary signature 中间态，generation slot affordance 匹配只消费 typed `BattleAiSkillAffordanceRecord`；transition rule/condition 仍是 `.tres` Resource，但 validation predicate/state 表在内部使用 typed `HashSet`，trace 走 `BattleAiStateResolver.TransitionConditionTrace` typed DTO；`AiActionTrace` / `AiCandidateSummary` / `AiCommandSummary` 是 plain sealed C# trace DTO，内部集合使用 `List<T>` / `Dictionary<string,T>`，`EnemyAiActionHelper` 是 internal static typed trace helper，action / evaluator / `BattleAiContext` 不再把 action trace 作为 Godot Dictionary 业务状态往返，只在 internal `ToDictionary()` trace 投影边界通过 `TraceDictionaryProjection` 输出 Godot Dictionary/Array，不要恢复 `RefCounted`、`GlobalClass`、public Godot 集合字段、public Godot 投影 API、helper public snake_case API 或 `to_dict()` / `from_command()` / `is_empty()` snake_case API。
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
