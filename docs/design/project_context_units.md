# 当前 Godot 项目的上下文装载单元

更新日期：`2026-07-24`

## 文档定位

- 这份文档只用于划分“改一类问题时应该先读哪些文件”。
- 这不是系统设计说明，也不是实现细节、迁移状态、测试清单或变更日志。
- 这里描述的是项目架构边界、职责归属、依赖关系和推荐读图组合。
- 具体规则、数值、运行时细节、回归入口，放在 `docs/design/<system>/` 下对应的当前实现文档、源码或测试目录内。
- `docs/proposals/`、`docs/discussions/`、`docs/reviews/` 和 `docs/archive/` 都不代表当前实现。

## 使用规则

- 优先按“推荐装载组合”选读。
- 没有命中时，从“单元总览”中选择 1 个桥接单元，再补 1 到 2 个叶子单元。
- 先读“文件”列表；只有确认跨边界时，才补读“邻接单元”。
- 需要方法链、数据形状或聚焦回归时，再读对应 CU 的“细节文档”；当前实现文档总索引见 `docs/design/README.md`。
- 方案设计只能作为意图输入。若细节文档与 `docs/proposals/` 中的计划冲突，以当前源码、测试和 `docs/design/` 为准。
- 涉及 save/schema/兼容策略时，先结合 `AGENTS.md` 的兼容性约束判断，不自行扩散到无关单元。
- 常规回归不默认包含 battle simulation、balance simulation、benchmark 或交互式工具入口。

## 全局架构纪律

以下是贯穿全项目的通用纪律，各单元不再逐条重复；字段级 owner、方法名与回归入口一律放源码、当前实现文档与 `tests/`，迁移状态只放 proposal/plan/archive。

- 运行时业务态由 plain C# typed owner（DTO / 服务 / typed 集合）承载；`Godot.Collections.Dictionary` / `Array` 只在 save/schema、UI/window、资源导入、Godot API 这些边界短暂投影，不作为长期真相源。
- runtime helper / service 默认是 plain C# `IDisposable`，不用 `RefCounted` / `GlobalClass` 或 GodotObject validity/dispose 生命周期（少数声明 Godot Signal 的除外）。
- fixed schema 名称（枚举式固定值）优先由 enum/typed 规则拥有，不恢复 public GD helper 或字符串白名单；正式内容 key 是 `StringName`，不从 string key 或 value 内 id 回建索引。
- 静态内容（`.tres`）经各 `*ContentRegistry` 载入、校验并投影为 typed 定义，再由进程级 `ContentSnapshot` 冻结发布；session、catalog、runtime 与 BattleSim 只借用 typed definition 索引，不回读 authored Resource 或弱类型 content payload。

## 全局排除

- `.godot/`
- 所有 `.uid`
- `prompts/`
- `example/`
- `.vscode/`
- 捕获图、临时导出物、个人分析包

## 架构分层

```text
启动与入口
  CU-01 登录壳 / 世界预设 / 存档选择

持久化与静态内容
  CU-02 Save / Session / Registry
  CU-03 世界配置
  CU-13 Progression 内容定义
  CU-20 敌方与 AI 内容定义

世界侧运行时
  CU-04 世界生成
  CU-05 世界网格与迷雾
  CU-06 Runtime 总编排
  CU-07 世界地图渲染
  CU-08 据点窗口

角色与仓库侧
  CU-09 队伍管理窗口
  CU-10 背包 / 装备 / 物品
  CU-11 队伍与成员状态模型
  CU-12 CharacterManagement 桥接
  CU-14 Progression 规则与属性服务

战斗侧
  CU-15 战斗运行时总编排
  CU-16 战斗规则 / AI / 伤害
  CU-17 地形 / roster / prop 注入
  CU-18 战斗展示

验证与自动化
  CU-19 回归与截图辅助
  CU-21 Headless runtime / 文本命令 / 快照
```

## 关键桥接链

```text
LoginScreen -> GameSession
ApplicationLifetimeCoordinator -> ProcessContentHost -> immutable ContentSnapshot
ContentSnapshot -> GameSession -> GameRoot -> GameContentCatalog -> typed definition indices
GameSession -> GameRuntimeFacade -> WorldMapRuntimeProxy -> WorldMapSystem
GameRuntimeFacade -> BattleSessionFacade -> BattleRuntimeModule
GameRuntimeFacade -> CharacterManagementModule -> Progression / Equipment / Attribute services
WorldMapSystem -> BattleMapPanel -> BattleBoard2D -> BattleBoardController
HeadlessGameTestSession -> GameSession + GameRuntimeFacade -> GameTextCommandRunner
```

## 单元总览

### CU-01 登录壳、世界预设、存档选择、显示设置

- 文件：
  - `project.godot`
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
  - `scripts/systems/content/world/WorldPresetRegistry.cs`
- 负责：启动入口、世界预设入口、存档选择、显示设置、建卡入口。
- 适合：开始菜单、建卡 UI、预设入口、存档列表、显示设置。
- 邻接单元：CU-02、CU-03、CU-14。

### CU-02 GameSession、存档、序列化、进程内容快照

- 文件：
  - `project.godot`
  - `scripts/systems/lifecycle/*.cs`
  - `scripts/systems/persistence/*.cs`（save schema 版本号唯一权威：`SaveSchemaVersions.cs`）
  - `scripts/systems/content/ProcessContentHost.cs`
  - `scripts/systems/content/ContentSnapshot.cs`
  - `scripts/systems/content/ContentSnapshotBuilder.cs`
  - `scripts/systems/content/BarrierSkillContentValidator.cs`
  - `scripts/systems/content/IContentResourceLoader.cs`
  - `scripts/systems/content/EngineAssetResolver.cs`
  - `scripts/systems/content/GameRoot.cs`
  - `scripts/systems/content/GameContentCatalog.cs`
  - `scripts/systems/content/skills/*.cs`
  - `scripts/systems/content/world/*.cs`
  - `scripts/player/progression/*ContentRegistry.cs`
  - `scripts/player/warehouse/*ContentRegistry.cs`
  - `scripts/enemies/EnemyContentRegistry.cs`
  - `scripts/enemies/EnemyContentSeed.cs`
  - `scripts/enemies/definitions/*.cs`
  - `scripts/systems/battle/core/special_profiles/BattleSpecialProfileRegistry.cs`
  - `scripts/utils/GodotObjectOwnership.cs`
  - `scripts/utils/NativeLeaseScope.cs`
  - `scripts/utils/GodotProjectionLease.cs`
  - `scripts/utils/RuntimePlainPayload.cs`
  - `scripts/utils/GodotObjectLifecycle.cs`
  - `scripts/utils/GameLog.cs`
  - `scripts/utils/GameLogSinks.cs`
  - `tests/runtime/lifecycle/*.cs`
- 细节文档：
  - `docs/design/platform/godotsharp_lifecycle.md`
- 负责：application shutdown、active save、slot meta、save payload/index、进程内容构建与全局会话边界。
- 内容快照边界：`ApplicationLifetimeCoordinator` 拥有唯一 `ProcessContentHost`；host 在启动期按 canonical path 加载 authored Resource，`ContentSnapshotBuilder` 在同一个同步构建作用域内完成 registry 校验与 typed 投影，成功后 seal 一个跨 session 复用的 immutable `ContentSnapshot`。敌方模板、AI brain/action graph、wild encounter roster、正式 `BattleEncounterDefinition` 与 BattleSim profile 和其他静态内容一起发布为 definition 索引；`BattleEncounterDefinition` 汇合 roster、objective 和 success/failure/draw 世界处理，world anchor 只持有其 id。`EnemyContentRegistry` / `BattleEncounterContentRegistry` 只存在于该构建作用域。`GameSession`、`GameRoot`、`GameContentCatalog`、world/battle runtime 与 BattleSim 不持有 raw registry 或 authored Resource mirror；内容读入口不设 legacy catalog，production lifecycle 的 legacy debt 必须保持为零。`EngineAssetResolver` 独立借用 scene/texture/audio 等 engine asset，quiescing 后拒绝新解析。
- 生命周期边界：`ApplicationLifetimeCoordinator` 是进程内 shutdown state、owner drain、finalizer barrier 与最终 `SceneTree.Quit` 的唯一 owner，拥有并关闭唯一 `ProcessContentHost`，按 Runtime、Session 阶段关闭顶层 participant；退出后的 stderr、进程返回码与 GodotSharp fatal marker 由 CU-19 的外层 runner 判定，不回写进程内 report。`GameSession` 只登记为 snapshot borrower，关闭时先解绑 `GameRoot` / `GameContentCatalog` 再注销 borrower，不重建任何 content registry。`NativeLeaseScope` 显式拥有 runtime 创建的 pathless native wrapper，`GodotProjectionLease` 显式拥有短期 Godot collection 投影并只弱登记 borrowed child；两者都通过 lifecycle audit 记录 owner/domain，不遍历对象图。`WorldMapSystem` / `HeadlessGameTestSession` 作为 Runtime participant 登记到 coordinator 并关闭各自持有的 runtime graph，`GameSession` 作为 Session participant 关闭 session graph，子服务由这些顶层 owner 递归释放而不独立注册。`GameSession` 是会话根、持有 `GameRoot`；`GameContentCatalog` 是正式内容类型的组合根读入口，借用 process snapshot 并带 revision，生命周期绑定 owning `GameRoot`（root dispose 后 catalog 失效）。
- 持久化边界：`SaveRepository` 拥有底层 save 文件 IO，`GameSession` 拥有 active save / schema / meta / index 归并；save/slot-index 的 session cache 与读回结果保持 plain C# graph，写入时才创建 Request-domain `GodotProjectionLease`，每个 nested collection 由同一 lease 显式拥有；`FileAccess` / `DirAccess` 由 Request-domain `NativeLeaseScope` 拥有，并在 remove/rename 前显式关闭文件句柄。`GetVar(false)` 结果必须在 file/Variant 仍存活时立即还原为 plain/typed state，不让 raw Godot payload 逃逸。`world_data` 的 runtime owner 是 `WorldRuntimeData`，只在 save payload 入口/出口投影。子 payload 破坏性 schema 变化时同步升级 owning save version，且只接受当前版本、不做 legacy 兼容迁移。
- 持久化性能边界：runtime 内部提交以 typed `PartyState` / `WorldRuntimeData` 的 detached plain snapshot 进入 trusted serializer path，避免已经由 canonical owner 保证有效的状态再次往返 Godot collection；公开 Dictionary 写入口、磁盘读回与 schema decode 仍执行严格归一化和校验。save schema/version 不因这条内部快路径改变。
- 日志边界：C# 诊断统一构造 typed `GameLogRecord` 并由 `GameLog` 负责等级过滤、单行格式化和线程安全的 sink 分发；`ConsoleLogSink` 是结构化日志的唯一进程输出实现，`GameSessionLogSink` 只把同一条记录复制到 owning `GameSession` 的 `GameLogService`，供 `RuntimeLogDock`、snapshot 和可选 JSONL 使用。会话/玩法 feed 事件直接调用 typed `GameSession.RecordLogEvent(...)`，避免再次经过全局分发；生命周期报告和测试协议等必须保持精确文本的机器可读输出统一经过 `ConsoleProcessOutput`，不混入结构化 sink。
- 物品内容边界：`ItemContentRegistry` / `RecipeContentRegistry` 只在同步加载与校验阶段持有 authored `ItemDef` / `RecipeDef`，随后立即投影为递归只读的 `ItemDefinition` / `RecipeDefinition`。`GameSession`、`GameContentCatalog`、runtime、battle、settlement 与 UI 只借用 definition 索引，不保留 raw registry mirror，也不把 definition 回投为 Godot Dictionary。
- 适合：save schema、序列化、内容接入、全局注册表问题。
- 邻接单元：CU-01、CU-03、CU-04、CU-10、CU-11、CU-13、CU-20、CU-21。

#### Quest Content（任务内容）

- Source: `data/configs/quests/*.tres`
- Loader: `QuestContentRegistry` (called from `ProgressionContentRegistry.Build`)
- Authoring schema owner: `QuestDef`; runtime value owner: `QuestDefinition`
- Validator: `QuestContentValidator`
- Accept requirement evaluator: `QuestAcceptRequirementEvaluator`
- Recommended reads before changes:
  - `scripts/player/progression/QuestDef.cs`
  - `scripts/player/progression/QuestContentRegistry.cs`
  - `scripts/player/progression/ProgressionContentRegistry.cs`
  - `scripts/player/progression/QuestContentValidator.cs`
  - `scripts/player/progression/QuestProviderContentRules.cs`
  - `scripts/systems/progression/QuestAcceptRequirementEvaluator.cs`

### CU-03 世界配置资源与预设数据

- 文件：
  - `scripts/utils/WorldMap*Config.cs`
  - `scripts/utils/Settlement*.cs`
  - `scripts/utils/Facility*.cs`
  - `scripts/utils/WildSpawnRule.cs`
  - `scripts/systems/content/world/*.cs`
  - `scripts/systems/world/*Definition.cs`
  - `scripts/systems/content/ContentSnapshot.cs`
  - `scripts/systems/content/ContentSnapshotBuilder.cs`
  - `data/configs/world_map/*.tres`
  - `data/configs/world_map/shared/*.tres`
- 细节文档：
  - `docs/design/world/world_map_module.md`
- 负责：world preset、世界生成配置、据点/设施/野外遭遇的静态资源与内容校验。
- 边界：`WorldMap*Config` / settlement / facility / wild-spawn Resource 只属于 process host 的同步 authoring/load 阶段；`WorldGenerationDefinition.FromResource(...)` 在 seal 前递归 canonical-load mounted submap 与 formal default bundle，检测 canonical path cycle，并生成完整 immutable definition graph。`WorldMapContentValidator` 的正式入口只校验 typed graph；`GameSession` 保存 generation path/id 并借用 snapshot 中的 definition，`WorldMapDataContext`、spawn/runtime/facade/UI 不在运行期加载或保留 raw world Resource。
- 适合：世界预设、设施分布、遭遇配置、世界内容校验。
- 邻接单元：CU-01、CU-02、CU-04。

### CU-04 世界生成、据点服务注入、遭遇锚点

- 文件：
  - `scripts/systems/world/WorldMapSpawnSystem.cs`
  - `scripts/systems/world/WorldMapSpawnProjection.cs`
  - `scripts/systems/world/WorldMapSettlementStateData.cs`
  - `scripts/systems/world/EncounterAnchorData.cs`
  - `scripts/systems/world/WorldMapResourceNodeData.cs`
  - `scripts/systems/world/WildEncounterGrowthSystem.cs`
  - `scripts/utils/WorldEventConfig.cs`
  - `scripts/utils/MountedSubmapConfig.cs`
- 细节文档：
  - `docs/design/world/world_map_module.md`
  - `docs/design/world/settlement_module.md`
- 负责：世界生成、据点注入、挂载子地图事件、遭遇锚点、野外资源点、世界初始状态。
- 边界：`WorldMapSpawnSystem.WorldBuildData` 与各 spawn instance data 是世界生成阶段的 typed 真相源；`WorldMapSpawnProjection` 通过 `WorldMapSettlementStateData.Create(...)` 生成符合当前完整 schema 的据点默认状态，再一次构造正式递归 plain C# world graph，settlement、NPC、anchor、resource node、event 与 mounted submap 不先经过 Godot collection。只有同步 Godot API/schema consumer 调用 `ProjectLease(...)` 创建 Request-domain 短租约。
- 适合：世界生成规则、起始遭遇、据点生成、submap 入口。
- 邻接单元：CU-02、CU-03、CU-05、CU-06、CU-20。

### CU-05 世界网格与迷雾基础设施

- 文件：
  - `scripts/systems/world/WorldMapGridSystem.cs`
  - `scripts/systems/world/WorldMapFogSystem.cs`
  - `scripts/systems/world/WorldMapFogFactionState.cs`
  - `scripts/utils/WorldMapCellData.cs`
  - `scripts/utils/VisionSourceData.cs`
- 负责：世界格网、坐标、视野来源、迷雾状态。
- 边界：`WorldMapFogSystem` 以 CLR Dictionary 管理 faction state，`WorldMapFogFactionState` 以 `HashSet<Vector2I>` 持有 visible/explored 坐标；persistent revision 跟踪需要持久化的 fog graph 变化，普通可见性重建不把 fog snapshot 写回 world owner。`WorldMapDataContext` 以 owner/map/revision gate 在 save、地图切换和事务边界才调用 `BuildPersistentStatePlain()`，并与 `WorldRuntimeData` 的 fog canonical state/save snapshot 保持递归 plain；事务回滚必须从 root owner 重新加载 active context/fog。Godot Array/Dictionary 只在 fog 的同步 setup/load adapter 或 CU-06 world projection 边界短暂出现，不能缓存回 fog system。
- 适合：世界移动判定、迷雾刷新、地图 cell 数据。
- 邻接单元：CU-04、CU-06、CU-07。

### CU-06 世界/战斗运行时总编排与场景适配

- 文件：
  - `scenes/main/world_map.tscn`
  - `scripts/systems/game_runtime/*.cs`
  - `scripts/systems/world/WorldMapDataContext.cs`
  - `scripts/systems/world/WorldRuntimeData.cs`
  - `scripts/systems/world/WorldMapSettlementStateData.cs`
  - `scripts/systems/world/WorldMapDataProjection.cs`
  - `scripts/systems/world/WorldTimeSystem.cs`
  - `scripts/systems/game_runtime/WorldMapSystem.cs`
  - `scripts/systems/settlement/*.cs`
  - `scripts/ui/RuntimeLogDock.cs`
  - `scripts/ui/SubmapEntryWindow.cs`
  - `scripts/ui/NpcQuestOfferDialog.cs`
  - `scenes/ui/npc_quest_offer_dialog.tscn`
- 细节文档：
  - `docs/design/world/world_map_module.md`
  - `docs/design/world/settlement_module.md`
  - `docs/design/battle/runtime_module.md`
- 负责：world/battle 切换、窗口互斥、命令编排、场景同步、战后回写、运行时总入口。
- 边界：`GameRuntimeFacade` 通过当前 root/session 解析 `GameContentCatalog`，只在仍绑定当前 session（`IsBoundToSession`）时复用，用 revision 校验有效性。`WorldMapSystem` 是 coordinator 登记的顶层 Runtime participant：shutdown 先断开 scene/UI borrower，再关闭 `WorldMapRuntimeProxy` 与它拥有的 `GameRuntimeFacade`；facade 递归关闭 battle/world 子服务，但不拥有 application shutdown state。headless 路径由 CU-21 的 `HeadlessGameTestSession` 承担同级 participant 职责。`BattleSessionFacade` 注入的 `IBattleSeedSource` 只用于 battle map / terrain seed 的确定性组合与测试 seam，production 默认仍为 `TrueRandomBattleSeedSource`；命中、伤害、豁免、随机目标等 combat RNG 不消费这个 seed，必须保持 `TrueRandomSeedService` 的独立随机。世界运行态 owner 是 `WorldRuntimeData`（settlement / submap / encounter anchors / world events / fog 等先进 typed owner），其 canonical save graph 由 plain `BuildSaveSnapshotPlain()` 生成，Godot API/window/proxy 仍经 `WorldMapDataProjection` 做短期投影。headless/runtime snapshot 的 canonical graph 由 `GameRuntimeSnapshotBuilder.BuildHeadlessSnapshotPlain()` 生成；`IGameRuntimeSnapshotSource` 只暴露 detached plain facts 或 borrowed typed domain state，不暴露 Godot collection，facade/proxy 的 Godot collection 消费者只在同步边界持有整根 Request-domain projection lease。pending battle context、encounter loot、runtime log、settlement/window snapshot 与 promotion prompt 都以 detached plain graph 跨模块传递；`GameRuntimeFacade` 在 clear 前复制 pending prompt，reward flow 与 `WorldMapRuntimeProxy` 直接消费 plain snapshot，结构化诊断经 CU-02 的 `GameLog` sink 只进入 session 一次，command log 等 gameplay feed 直接记录 typed session event，相关 Godot API 只在同步调用内创建短租约。跨 party/world/coord 的提交统一走 `RuntimeTransaction` stage + `CommitRuntimeState`；`PartyState` 替换必须保持 session/runtime/services canonical root 一致，root 替换走 `RuntimeTransaction` / facade rebind。命令输入以 typed request（`SettlementActionRequest` / `PromotionSelectionData` / `PartyItemUseOptions` 等）为正式边界。
- 世界事务与物化边界：active submap 的完整 typed map state 与 fog 只在 save、切图、战斗结算、事务捕获和 teardown 等持久化边界 splice 回 root owner，不能把 active submap payload 当成 root rollback snapshot。`RuntimeTransaction` 的 mutation flags 只决定需要捕获和恢复的 rollback scope；serializer 写入的是 total snapshot，所以每次 commit 都必须先 stage party/world/coord 全部 canonical owner。settlement 奖励、任务事件和成员成就等间接副作用也必须纳入 party rollback scope。
- 据点状态边界：`WorldMapSettlementRecordData` 持有完整不可变 `WorldMapSettlementStateData`，其商店子树由 `SettlementShopStateData` / `SettlementShopStockEntryData` typed 持有；每个商店独立拥有自身的 seed、刷新步数和库存，据点层不保存共享镜像。`WorldRuntimeData` 只替换完整聚合，单字段更新走 `With*`。`SettlementShopService` 接收当前 world step 的显式参数并返回更新后的完整状态；world step 与 modal feedback 不写入持久化 `settlement_state`。该子 schema 精确校验并归属当前顶层 save version；只接受当前精确 schema，不做旧版迁移、缺字段补齐或额外字段透传。
- 适合：runtime 接线、模式切换、世界场景同步、据点/仓库/奖励/任务命令入口。
- 邻接单元：CU-02、CU-04、CU-05、CU-07、CU-08、CU-09、CU-10、CU-12、CU-15、CU-18、CU-21。

#### Settlement Runtime Commands（据点运行时命令）

- Contract board modal: `GameRuntimeSettlementCommandHandler` + `ShopWindow`
- NPC quest offer modal: `GameRuntimeSettlementCommandHandler` -> `NpcQuestOfferWindowData` -> `NpcQuestOfferDialog`, wired by `WorldMapSystem`
- Accept availability: `QuestAcceptRequirementEvaluator` invoked by handler (contract board and NPC quest offer)
- Confirmation state: modal context `pending_confirmation_quest_id/text/source` (contract board and NPC quest offer)
- 物理拆分（零行为变化）：主 handler 保留命令分发/校验/payload/持久化/facade 桥接与对外 internal 委托；合同板+悬赏板(WIP)在 `GameRuntimeContractBoardCommandHandler.cs`、NPC 任务面板在 `GameRuntimeNpcQuestOfferCommandHandler.cs`、商店/锻造/驿站窗口在 `GameRuntimeServiceWindowCommandHandler.cs`、据点窗口数据构建在 `GameRuntimeSettlementWindowDataBuilder.cs`（主 handler 构造函数统一接线）。
- Recommended reads before changes:
  - `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
  - `scripts/systems/game_runtime/GameRuntimeContractBoardCommandHandler.cs`
  - `scripts/systems/game_runtime/GameRuntimeNpcQuestOfferCommandHandler.cs`
  - `scripts/systems/game_runtime/GameRuntimeServiceWindowCommandHandler.cs`
  - `scripts/systems/game_runtime/GameRuntimeSettlementWindowDataBuilder.cs`
  - `scripts/player/progression/QuestProviderContentRules.cs`
  - `scripts/systems/settlement/NpcQuestOfferWindowData.cs`
  - `scripts/ui/NpcQuestOfferDialog.cs`
  - `scenes/ui/npc_quest_offer_dialog.tscn`
  - `scripts/systems/game_runtime/WorldMapSystem.cs`
  - `scripts/ui/ShopWindow.cs`

### CU-07 世界地图渲染叶子单元

- 文件：
  - `scripts/ui/WorldMapView.cs`
  - `assets/main/basic_map/*.png`
- 负责：大地图绘制、据点/事件/遭遇/NPC/资源点图标、选中反馈、点击表现。
- 适合：地图视觉、事件图标、地图交互表现。
- 邻接单元：CU-05、CU-06。

### CU-08 据点窗口与人物信息窗口

- 文件：
  - `scenes/ui/settlement_window.tscn`
  - `scripts/ui/SettlementWindow.cs`
  - `scenes/ui/shop_window.tscn`
  - `scripts/ui/ShopWindow.cs`
  - `scripts/systems/settlement/SettlementPanelKind.cs`
  - `scripts/systems/settlement/SettlementSubmissionSource.cs`
  - `scenes/ui/character_info_window.tscn`
  - `scripts/ui/CharacterInfoWindow.cs`
- 细节文档：
  - `docs/design/world/settlement_module.md`
- 负责：据点服务窗口、商店窗口、人物信息展示。
- 适合：据点 UI、服务反馈、人物信息展示。
- 邻接单元：CU-06、CU-12、CU-14。

### CU-09 队伍管理、成就摘要、转职、角色奖励窗口层

- 文件：
  - `scenes/ui/party_management_window.tscn`
  - `scripts/ui/PartyManagementWindow.cs`
  - `scenes/ui/contingency_setup_window.tscn`
  - `scripts/ui/ContingencySetupWindow.cs`
  - `scenes/ui/promotion_choice_window.tscn`
  - `scripts/ui/PromotionChoiceWindow.cs`
  - `scenes/ui/mastery_reward_window.tscn`
  - `scripts/ui/MasteryRewardWindow.cs`
- 负责：队伍编成、转职选择、奖励弹窗、角色摘要展示、触发术 setup 窗口。
- 边界：窗口只渲染当前状态并通过 signal 提交意图，不直接改 `PartyMemberState`；`PromotionChoiceWindow` 直接读取递归 plain promotion prompt，并把 choice/selection 保存在 CLR graph 中。卡片构建只在同步 `SelectionCardBuilder.BuildCard(...)` 调用期间创建 Request-domain projection lease；selection signal 也只在同步 subscriber 回调内投影，当前 `WorldMapSystem` consumer 必须在回调返回前转成 `PromotionSelectionData`，wrapper 不得逃逸到 deferred connection 或下一帧。
- 适合：队伍窗口、转职 UI、触发术 setup UI、角色奖励弹窗。
- 邻接单元：CU-06、CU-10、CU-11、CU-12、CU-14。

### CU-10 队伍共享背包、物品定义与装备基础流转

- 文件：
  - `scripts/player/equipment/*.cs`
  - `scripts/player/warehouse/WarehouseState.cs`
  - `scripts/player/warehouse/WarehouseStackState.cs`
  - `scripts/player/warehouse/TraitRollGroupDef.cs`
  - `scripts/player/warehouse/TraitRollGroupEntryDef.cs`
  - `scripts/player/warehouse/ItemDefinition.cs`
  - `scripts/player/warehouse/RecipeDefinition.cs`
  - `scripts/player/warehouse/TraitRollGroupDefinition.cs`
  - `scripts/player/warehouse/TraitRollGroupEntryDefinition.cs`
  - `scripts/player/warehouse/WeaponProfileDefinition.cs`
  - `scripts/player/warehouse/WeaponDamageDiceDefinition.cs`
  - `scripts/player/warehouse/ItemContentRegistry.cs`
  - `scripts/player/warehouse/RecipeContentRegistry.cs`
  - `scripts/systems/content/ContentSnapshot.cs`
  - `scripts/player/equipment/EquipmentRequirementDefinition.cs`
  - `scripts/player/equipment/EquipmentAttributeRequirementDefinition.cs`
  - `scripts/player/warehouse/ItemTraitContentValidator.cs`
  - `scripts/systems/inventory/*.cs`
  - `scenes/ui/party_warehouse_window.tscn`
  - `scripts/ui/PartyWarehouseWindow.cs`
  - `data/configs/items/*.tres`
  - `data/configs/items_templates/*.tres`
  - `data/configs/recipes/*.tres`
- 细节文档：
  - `docs/design/inventory/warehouse_equipment_module.md`
  - `docs/design/battle/weapon_dice_and_equipment.md`
  - `docs/design/battle/equipment_ability_runtime.md`
- 负责：共享背包、堆叠/容量、物品与配方定义、装备实例、装备/卸装、物品使用。
- 边界：`WarehouseState` / `EquipmentState` / `EquipmentInstanceState` 家族是 plain C# runtime/save owner。`ItemDef` / `RecipeDef`、weapon profile/dice、trait-roll group 与 equipment requirement 是 `.tres` authoring schema；`ItemContentRegistry` / `RecipeContentRegistry` 仅在 `ContentSnapshotBuilder` 的同步构建作用域内借用 raw Resource，并把模板合并、技能书生成、武器属性与装备需求投影为递归只读 `ItemDefinition` / `RecipeDefinition`。process snapshot seal 后，session、runtime、battle、settlement 与 UI 只借用同一个 definition 索引，不创建 registry mirror、Resource duplicate 或 Godot Dictionary 回投；无效 nested authoring 节点按索引化内容路径立即失败。物品内容（category / equipment type / 伤害标签 / 固定 trait ids / trait roll groups）跨 trait 规则由 `ItemTraitContentValidator` 校验；装备 trait mint 时机由 `EquipmentRules` / `EquipmentTraitRollService` 拥有——仓库分配稳定装备 instance id 后才生成 `equipment_roll` trait，不在 transient drop 阶段或已入库实例上重 roll。
- 适合：物品内容、装备流转、仓库规则、仓库窗口。
- 邻接单元：CU-02、CU-06、CU-09、CU-11、CU-12、CU-19、CU-21。

### CU-11 队伍与角色成长运行时数据模型

- 文件：
  - `scripts/player/progression/PartyState.cs`
  - `scripts/player/progression/PartyState.SaveSnapshot.cs`
  - `scripts/player/progression/PartyMemberStateCollection.cs`
  - `scripts/player/progression/PartyMemberState.cs`
  - `scripts/player/progression/Contingency*State.cs`
  - `scripts/player/progression/TraitInstanceState.cs`
  - `scripts/player/progression/TraitInstanceCollection.cs`
  - `scripts/player/progression/AttributeSnapshot.cs`
  - `scripts/player/progression/Unit*.cs`
  - `scripts/player/progression/UnitCustomStatMap.cs`
  - `scripts/player/progression/UnitReputationMap.cs`
  - `scripts/player/progression/AchievementProgressState.cs`
  - `scripts/player/progression/QuestState.cs`
  - `scripts/player/progression/QuestObjectiveProgressState.cs`
  - `scripts/player/progression/QuestProgressContext.cs`
  - `scripts/player/progression/PendingCharacterReward*.cs`
  - `scripts/systems/progression/CharacterProgressionDelta.cs`
- 细节文档：
  - `docs/design/progression/character_module.md`
  - `docs/design/progression/trait_system.md`
  - `docs/design/progression/fate_runtime.md`
- 负责：队伍状态、成员状态、成长状态、任务状态、角色奖励载体。
- 边界：`PartyState.member_states` owner 是 `PartyMemberStateCollection`；`PartyMemberState` 持有 `ContingencyMatrixSetupState`（只承载战斗外 setup / 充能 / 消耗回写事实，不承载战斗内 release queue / hook）。其余成长、任务、奖励状态都是 plain C# runtime/save DTO；`PendingCharacterReward` / `PendingCharacterRewardEntry` 与其 payload codec 同归 `scripts/player/progression/` 的 Party save graph owner，奖励生成与应用逻辑仍留在 progression service。`PartyState.BuildSaveSnapshotPlain()` 是 Party save graph 的 canonical 序列化源，Godot Dictionary 边界投影也从该 plain snapshot 构建，避免并行 schema 源。角色 trait 与装备 roll trait 的持久实例边界是 `TraitInstanceState` / `TraitInstanceCollection`：成员实例只承载 `character` source、装备实例只承载 `equipment_roll` source，反序列化按严格字段集与 source kind 校验。
- 适合：party schema、角色状态字段、奖励队列、成长状态序列化。
- 邻接单元：CU-02、CU-09、CU-10、CU-12、CU-13、CU-14、CU-19。

### CU-12 CharacterManagement、成就记录、奖励归并桥

- 文件：
  - `scripts/systems/progression/CharacterManagementModule.cs`
  - `scripts/systems/progression/PartyContingencySetupService.cs`
  - `scripts/systems/progression/ContingencySetupMutationResult.cs`
  - `scripts/systems/progression/CharacterBattleWritebackService.cs`
  - `scripts/systems/progression/*ApplyService.cs`
  - `scripts/systems/progression/QuestProgressService.cs`
  - `scripts/systems/progression/FaithService.cs`
  - `scripts/systems/progression/LevelGrowth*.cs`
  - `scripts/systems/progression/PracticeGrowthService.cs`
  - `scripts/systems/attributes/AttributeSourceContext.cs`
  - `scripts/systems/attributes/AttributePermanentChangeSource.cs`
- 细节文档：
  - `docs/design/progression/character_module.md`
  - `docs/design/progression/trait_system.md`
  - `docs/design/progression/faith_system.md`
  - `docs/design/progression/fate_runtime.md`
- 负责：角色管理门面、奖励归并、成就/任务推进、身份与成长桥接。
- 边界：`PartyContingencySetupService` 是世界侧 contingency setup save/charge/clear/status mutation owner；setup 模板是内容，authored 在 `data/configs/contingency_templates/*.tres`（`ContingencySetupTemplateDef`，经 `ContingencyTemplateContentRegistry` 载入），充能材料与预留 MP 公式的单一出处是 `ContingencyContentRules`。`CharacterBattleWritebackService` 拥有战斗结束后 consumed setup 与战后 HP/MP/death/装备回收/roster 移除的写回。`CharacterManagementModule` 经 `ProgressionServiceFactory` 构建 transient `ProgressionService` 图，是门面而非规则宿主。
- 适合：奖励入账、任务推进、成就记录、跨系统成长接线。
- 邻接单元：CU-06、CU-08、CU-09、CU-10、CU-11、CU-13、CU-14、CU-15、CU-19。

### CU-13 progression 内容定义、条件模型、seed 内容

- 文件：
  - `scripts/player/progression/*Def.cs`
  - `scripts/player/progression/*Requirement.cs`
  - `scripts/player/progression/*ContentRegistry.cs`
  - `scripts/player/progression/*ContentRules.cs`
  - `scripts/player/progression/QuestContentValidator.cs`
  - `scripts/systems/progression/*ContentValidator.cs`
  - `scripts/systems/content/ContentSnapshot.cs`
  - `scripts/systems/content/ContentSnapshotBuilder.cs`
  - `scripts/systems/content/skills/BattleAttackRollModifierSpec.cs`
  - `data/configs/skills/*.tres`
  - `data/configs/professions/*.tres`
  - `data/configs/races/*.tres`
  - `data/configs/subraces/*.tres`
  - `data/configs/traits/*.tres`
  - `data/configs/bloodlines/*.tres`
  - `data/configs/ascensions/*.tres`
  - `data/configs/stage_advancements/*.tres`
  - `data/configs/barriers/*.tres`
  - `data/configs/barrier_layers/**/*.tres`
  - `data/configs/faith/*.tres`
  - `data/configs/quests/*.tres`
- 细节文档：
  - `docs/design/progression/trait_system.md`
  - `docs/design/progression/faith_system.md`
  - `docs/design/battle/skill_runtime.md`
  - `docs/design/battle/equipment_ability_runtime.md`
- 负责：技能、职业、种族、通用 trait、任务、血脉、升华、信仰等静态内容与内容校验。
- 边界：`TraitDef` / `TraitContentRegistry` / `data/configs/traits/*.tres` 是通用 trait authoring 边界，source scope、effect/stack/charge/roll schema、typed passive 投影字段（如 save advantage tags、damage resistance entries、`passive_status_effects`）等固定值由 `TraitContentRules` / registry 校验（trait effect 配置须显式 typed 字段）。progression、identity、quest、faith、barrier 与 contingency registry 只在 `ContentSnapshotBuilder` 的同步加载作用域内读取 authored Resource，并立即投影为递归只读的 `*Definition`；跨表校验完成后 registry 释放 raw 引用，process host seal 一个共享 snapshot。`BarrierSkillContentValidator` 在 seal 前用 projected definition 索引双向校验 `layered_barrier` 的 profile 引用与屏障层 breaker skill 引用。`ProgressionContentRegistry`、`GameSession`、`GameContentCatalog`、character/runtime/UI 与 battle service 不保存、重建或回投这些 raw Resource；session A/B 必须复用同一 epoch 与 definition object graph。`BattleBarrierService` 通过 catalog 注入 `BarrierProfileDefinition` 索引，`FaithService` 通过构造函数注入 `FaithDeityDefinition` 索引。跨表校验只消费已经投影出的 definition 索引；raw `SkillDef` 和其他 authored Resource 只在资源加载、字段校验、`*Definition.FromResource(...)` 这类明确 Resource-boundary 触达。只供通用运行时内部结算引用、不得进入任何学习路径的技能使用 `learn_source = internal`，不能借用 `innate` 表达隐藏性。
- 屏障层复用边界：可被多个 `BarrierProfileDef` 组合使用的 canonical `BarrierLayerDef` 放在 `data/configs/barrier_layers/`，由 profile 通过外部 Resource 引用；完整屏障与单层技能不得各复制一份阻挡类别、破解技能或穿越结果。
- CombatEffect 内容边界：多伤害段与目标分类倍率必须走 `CombatDamageSegmentDef` / `CombatTargetDamageMultiplierRuleDef` → `CombatEffectDefinition.ExtraDamageSegments` / `TargetDamageMultiplierRules` typed 投影；来源绑定的武器额外骰走 `CombatEffectDef.source_bound_weapon_bonus_damage_dice_*` → `BattleStatusEffectState`，由 `BattleDamageResolver` 按状态 `source_unit_id` 与真实武器伤害结算，不在 `params` 或技能 id 文本里塞 ad-hoc 结构；资源校验由 `SkillContentRegistry` 负责。
- 攻击检定修正契约边界：`BattleAttackRollModifierSpec` 是 skill content-definition owner 下的 plain C# 数据契约，只负责 typed 字段、枚举映射、克隆和字典编解码；`CombatEffectDef`、projected definition、terrain/equipment/runtime 与 AI 共享该契约，但战斗筛选、叠加和生效规则仍由 battle rules/runtime owner 执行。
- 物理拆分（零行为变化，2026-07-19/20）：`SkillContentRegistry` 的 combat profile/effect 校验拆分为 `SkillCombatProfileValidator`（`TypedEffectParamTargets` 映射表随迁）、伤害效果/耐久/路径 AoE 校验拆分为 `SkillDamageEffectValidator`、execute/graded-save/temporal/save-bonus 校验拆分为 `SkillExecuteEffectValidator`（`GradedSaveExecuteParamKeys` 随迁），registry 本体只保留加载/索引/技能级校验与共享参数读取 helper；`EquipmentAbilityContentRegistry` 的逐 payload 校验拆分为 `EquipmentAbilityPayloadValidators`、binding/reaction 级校验拆分为 `EquipmentAbilityBindingValidator`、Resource→Definition 投影与枚举解析拆分为 `EquipmentAbilityDefinitionProjection`，registry 本体只保留 Rebuild 编排与索引；新类均只存在于同步构建作用域，已登记生命周期 boundary 白名单。
- CombatSkill 边界：`CombatSkillDef.attack_resolution_mode`（authoring）与 `SkillDefinition.AttackResolutionModeKind`（runtime DTO）唯一由 `BattleSkillResolutionRules` 解释为 direct effect / fate attack / force-hit-no-crit。装备必中主动技能应配 `attack_resolution_mode = &"direct_effect"`，再由 combat effect 的 target/status requirement 与 `BattleDamageResolver` 决定是否生效，不在装备能力 service、技能 id、状态 params 里硬编码“必中”。
- 装备能力内容边界：`EquipmentAbilityContentRegistry` 在加载期把 authored pack/binding/payload Resource 投影为 plain definitions，并以 `ItemDefinition` 索引校验来源物品；battle/runtime 不接受 raw `ItemDef`。召唤、消费召唤物、按召唤物数量/距离产生攻击检定修正必须走 `SummonUnitsActionPayloadDef` / `ConsumeSummonedUnitsActionPayloadDef` / `SummonedUnitAttackRollModifierActionPayloadDef` 与 `summoned_unit_count` fact；附近生物/友军计数走 `nearby_unit_count` / `nearby_ally_count` fact；击杀后的额外武器攻击走 `ImmediateWeaponAttackActionPayloadDef`；命中后升级为暴击走 `CriticalHitOverrideActionPayloadDef`；不进入技能可用性列表的装备内部结算走 `TriggerSkillActionPayloadDef`，仍引用正式 `SkillDef` 并复用技能效果、豁免、范围和死亡规则；AP 变动与下一行动回合 AP 清零走 `ModifyActionPointsActionPayloadDef`；装备期间影响行动/读条进度倍率的被动走 binding 级 `EquipmentTemporalProgressModifierDef`；装备触发的固定伤害减免走 `DamageReductionActionPayloadDef`；按实际伤害等 fact 计算治疗走 `HealFromFactActionPayloadDef`；状态栈消费走 `ConsumeStatusStacksActionPayloadDef`；随机分支走 `EquipmentOutcomeTableDef` / `EquipmentOutcomeEntryDef`，仍由分支内 typed action payload 表达效果；持久成长计数由 `ModifyAbilityStateActionPayloadDef` 写入，计数同步出的保存状态由 `EquipmentAbilityStateSchemaDef` 的同步字段声明；临时战场边特征/裂隙走 `ApplyEdgeFeatureActionPayloadDef` 的 from/to selector、`duration_tu`、edge feature 字段与 `max_active_edges`；武器近/远程分类条件读 `weapon_range_type` fact。资源只声明 binding/state/source/target 关系，不在技能 id 或武器 id 文本中推导召唤物、附近单位、追击目标、时间进度、伤害减免、伤害转治疗、状态栈消费、随机分支、临时边或成长状态键。
- 属性内容规则边界：`AttributeContentRules` 是 armor/shield/dodge/deflection/natural-armor 五种 AC component id、typed kind、双向映射、只读顺序与 membership 的唯一 owner；装备能力 authoring 校验和属性、world、battle 消费者共同依赖该 content-definition 规则，不在 `AttributeService` 或 validator 内复制白名单。
- 装备能力状态/后置动作边界：状态锁定（如 counterattack/guard/dodge bonus）、强制位移免疫等由 `ApplyStatusActionPayloadDef` typed 字段投影到 runtime status；`after_skill` / `after_damage` 的直接伤害、治疗、清状态、按 fact 治疗、状态栈消费等通用动作继续消费 typed action payload；有时限的 typed target mark 自己持有剩余 TU，并随目标的个人状态时间线推进，因此多个装备来源标记同一目标时不会互相刷新到期时钟；同一 `mirror_status_id` 只做聚合展示，选择剩余时间最长的 mark，当前镜像来源被消费、替换、到期或移除后从剩余 mark 重建，最后一份 mark 消失时才删除；到期由 `on_target_mark_expired/after_status_expired` 分发一次反应，`expired_target_mark_matches` fact 校验来源装备实例、binding、state key 与目标，随后统一清理 mark；授予技能可用性条件消费同一 fact query，生命百分比用 `hp_percent_bp`（basis points，0-10000）；授予技能结果条件走 `skill_damaged_target_count` / `skill_killed_target_count` / `skill_hp_damage_dealt` / `skill_moved_target_count` / `skill_unmoved_target_count` fact，伤害后动作可读 `hp_damage` fact，on-kill 条件可读 `kill_source_is_attack` / `kill_source_equipment_instance_matches` / `kill_source_binding_matches` 这类通用击杀来源 fact，不从授予技能 id 或武器 id 推导特例。
- 装备能力来源生命周期边界：`BattleUnitFactory.RefreshEquipmentProjection(...)` 重建 `equipment_ability_sources` 后，由 `BattleEquipmentAbilityRuntimeService`（经其持有的 `BattleEquipmentTargetMarkResolver`）按来源单位、装备实例和 binding 清理已经失去投影来源且声明 `remove_on_source_missing` 的 typed target mark；这类来源移除只重建或删除镜像状态，不分发自然到期反应。正式换装直接消费刷新结果；装备耐久摧毁则由 `BattleDamageResolver.ResolveEffects/ResolveAttackEffects` 在完整效果结算后统一触发刷新，因此普通技能、冲锋/连击、ground effect、内部技能与装备立即攻击共享同一清理边界；可用的 `BattleEventBatch` 同步记录受影响的来源/目标单位。
- 装备能力 fact 来源隔离边界：`status_stacks` 可通过 `EquipmentAbilityFactQueryDef.require_source_unit_match` 要求目标状态的 `source_unit_id` 与当前装备能力来源单位一致；不匹配时返回 0。需要来源私有的命中、追加骰、消费或击杀条件时复用该 fact，不按具体状态或武器 id 增加运行时分支。
- 装备投射类别边界：普通远程武器伤害在运行时具有基线 `nonmagical_missile` 类别；如果正式技能已经显式声明 `nonmagical_missile` 或 `magical_missile`，则以该声明为准，显式魔法投射不得同时补成非魔法投射。可能随远程武器命中穿越屏障的毒素、石化等附加类别由 reaction 的 `projected_effect_categories` 显式声明，`EquipmentAbilityContentRegistry` 在加载期校验它覆盖可从 typed damage/save payload 推导出的类别；运行时不从 payload、tag 或具体武器 id 兜底推导。
- 适合：新增或修改 progression 内容、条件模型、静态内容引用。
- 邻接单元：CU-02、CU-11、CU-12、CU-14、CU-15、CU-16、CU-19。

### CU-14 progression 规则与跨系统属性服务

- 文件：
  - `scripts/systems/progression/ProgressionService.cs`
  - `scripts/systems/progression/Profession*.cs`
  - `scripts/systems/progression/SkillMergeService.cs`
  - `scripts/systems/progression/AttributeGrowthService.cs`
  - `scripts/systems/progression/CharacterCreationService.cs`
  - `scripts/systems/progression/CharacterCreationIdentityOptionService.cs`
  - `scripts/player/progression/AttributeContentRules.cs`
  - `scripts/systems/attributes/AttributeService.cs`
  - `scripts/systems/attributes/AttributePermanentChangeSource.cs`
- 细节文档：
  - `docs/design/progression/character_module.md`
  - `docs/design/progression/trait_system.md`
  - `docs/design/progression/faith_system.md`
  - `docs/design/progression/fate_runtime.md`
- 负责：成长公式、职业规则、技能规则、建卡规则、属性计算。
- 边界：promotion 规则输入由 `PromotionSelectionData` 承载，永久属性写入授权由 `AttributePermanentChangeSource` 承载（protected custom stat 仅 CharacterCreation 或显式授权 StoryScript 可写）。默认建档角色不预置职业进度或职业核心技能；`RandomStartingSkillResourceSupportService` 在随机起始技能为耗蓝技能时原子授予 0 级 `basic_meditation`、解锁 MP，并把初始 `mp_max/current_mp` 设为 `0–40` 闭区间内的独立随机值，法术消耗不构成法力池下限。`ProgressionService` 的 selection、tag deficit、dedupe、preview 与 rollback 全部使用 `List` / `HashSet` / typed CLR Dictionary；顺序由 List 保留，HashSet 只做 membership，持久字段显式写入 `StringNameList`，业务算法和回滚快照不创建 Godot Array/Dictionary。`ProgressionService` 的手动学习入口拒绝 `internal` 以及职业/身份授予来源；内部 SkillDef 只能由明确持有其定义的运行时服务直接结算。`AttributeService` / inventory / trait / character-management 运行时只消费 plain `AttributeModifierDefinition`、CLR-backed `DerivedAttributeRule` 与 `AttributeContentRules` 固定属性契约；authored `AttributeModifier : Resource` 只在内容投影输入端存在，不由运行时动态创建。generic trait 属性入口是 `trait_attribute_modifiers`（保留 modifier 自身 source_type/source_id，不并入 equipment/passive 默认来源）；基础六维派生的 `*_modifier` 可被 modifier overlay 叠加但不回写基础属性或成长事实；AC component 的集合与 membership 由 `AttributeContentRules` 拥有，`AttributeService` 只负责汇总计算；BAB 直接消费 `ProfessionBaseAttackProgression`。
- 适合：成长规则、属性公式、建卡候选、职业/技能规则。
- 邻接单元：CU-01、CU-09、CU-11、CU-12、CU-13、CU-15、CU-19。

### CU-15 战斗运行时总编排

- 文件：
  - `scripts/systems/battle/runtime/*.cs`
  - `scripts/systems/battle/fate/*.cs`
  - `scripts/systems/battle/core/BattleCommand.cs`
  - `scripts/systems/battle/core/BattlePreview.cs`
  - `scripts/systems/battle/core/BattlePreviewProjection.cs`
  - `scripts/systems/battle/core/AutoCastRequest.cs`
  - `scripts/systems/battle/core/Contingency*.cs`
  - `scripts/systems/battle/core/BattleChangeFlags.cs`
  - `scripts/systems/battle/core/BattleObjectiveRuntimeContracts.cs`
  - `scripts/systems/battle/core/BattleObjectiveRuntimeCodec.cs`
  - `scripts/systems/battle/core/BattleObjectiveRuntimeStateFactory.cs`
  - `scripts/systems/battle/core/BattleObjectiveProgressSnapshot.cs`
  - `scripts/systems/battle/core/BattleScenarioActorSpawnRequest.cs`
  - `scripts/systems/battle/core/BattleEventBatch.cs`
  - `scripts/systems/battle/core/BattleEventBatchProjection.cs`
  - `scripts/systems/battle/rules/Battle*.cs`
  - `scripts/systems/battle/runtime/IBattleContingencyRuntimePort.cs`
  - `scripts/systems/battle/runtime/BattleContingencyBridgeService.cs`
  - `scripts/systems/battle/runtime/BattleContingencySystem.cs`
  - `scripts/systems/battle/runtime/ContingencyTargetResolverService.cs`
  - `scripts/systems/battle/runtime/BattleObjectiveEvaluationService.cs`
  - `scripts/systems/battle/runtime/BattleRuntimeModule.Objectives.cs`
  - `scripts/systems/game_runtime/BattleSessionFacade.cs`
  - `scripts/systems/battle/sim/*.cs`
- 细节文档：
  - `docs/design/battle/runtime_module.md`
  - `docs/design/battle/objective_runtime.md`
  - `docs/design/battle/skill_runtime.md`
  - `docs/design/battle/equipment_ability_runtime.md`
  - `docs/design/progression/fate_runtime.md`
- 负责：开战、时间轴、命令 preview/issue、技能执行、typed objective 求值、原子终局、战斗结算与战斗内运行时编排。
- Objective 初始化边界：Elimination、Boss、Rescue、Escape、Escort、Defense、Intercept、NodeOperation、Control 共享同一原子终局管线；具体 objective runtime 必须在地形格、双方单位和 scenario actor 放置完成后绑定。Boss/Intercept 由 roster actor id 在敌方 active index 唯一解析目标 unit 并冻结初始持久队员；Rescue/Escort/Defense 由 encounter `scenario_actors` 以 enemy template 只读投影构建 `source_member_id == ""` 的 battle-only 友方 AI 单位，并按类型化入口边/纵深放置，objective 再以稳定 actor id 唯一绑定。Rescue 只允许初始持久队员相邻交互，Escort/Intercept 目标自动寻路到类型化出口并在阻路时等待重寻路，Defense 冻结开战 TU/deadline 并要求目标存活到时。Escape 由类型化 map edge/depth 解析冻结出口格，以正式 footprint 地形/内部阻断边规则验证全体必需队员可同时无重叠落位；Escort/Intercept 同样验证目标完整 footprint 可进入出口。NodeOperation 为任意正数个 authored node 冻结唯一、可通行且初始化时未占用的实际坐标，只允许初始持久队员在同格或相邻格以 typed Interact 消耗 1 AP 推进完成位。Control 冻结任意正数个互不重叠的可通行边缘区域，验证双方至少一个完整 footprint 合法落点，并由 timeline driver 按每个独占区与 `tu_delta` 推进双方分数；争夺/中立不计分，同刻达标为 Draw。地形相关绑定失败先参加既有 terrain/placement 重试；每轮 failure snapshot 取最新 attempt，终端绑定或普通布阵穷尽必须清 pending loading、释放 battle save lock，延迟终端失败还要执行 canonical world-sync flush，且不得回退为歼灭。只有显式声明空地形代表异步 pending 的生成器可跨 frame 重试。
- 物理拆分与 owner 收敛（2026-07-19/21/24）：`BattleSkillExecutionOrchestrator` 的技能预览链拆分为 `BattleSkillPreviewService`、目标校验（含死者目标/execute/体型门槛）拆分为 `BattleSkillTargetValidationService`、链式伤害拆分为 `BattleChainDamageService`、随机链目标抽样拆分为 `BattleRandomChainSkillService`；orchestrator 持有这四个子 service，子 service 弱借用 runtime，并在 teardown 时断开 owner/sibling borrower。`BattleRuntimeModule` 第一批拆出 spawn 放置（`BattleSpawnPlacementService`）、特殊技能门禁与状态写入（`BattleSpecialSkillGateService`）、移动与强制位移（`BattleMovementCommandService`）、metrics/报告/effect origin（`BattleMetricsReportService`），第二批拆出 AI 决策绑定（`BattleAiDecisionBindingService`）、contingency 桥（`BattleContingencyBridgeService`）与只读命令 preview/entry 校验（`BattleCommandPreviewService`）。七个 module-owned service 都弱借用 module，并由 owner-local `BattleRuntimeModuleBorrowerSet` 作为唯一有序组合源正序绑定、逆序解绑；原 `BattleTimelineStatusBridgeService` 因没有独立状态或 capability 已删除，纯转发直接归回既有 owner。teardown 先关闭 AI callback consumer，再断开该 borrower set，最后释放底层 sidecar。`BattleGroundEffectService` 持有的 coord/relocation/validation 子服务同样由 parent 正序接线、逆序断开 runtime/owner/sibling borrower。主文件保留生命周期、命令入口与真正的跨 owner 编排，不再拥有 AI/timeline/contingency 的具体桥接实现。装备技能 usage 与 granted-skill reaction 的真实提交归 `BattleSkillExecutionOrchestrator`，preview service 不执行提交副作用；回合开始的 contingency/auto-cast 编排仍归 module/timeline owner，metrics service 只记录指标。装备规则依赖由 `BattleRuntimeModule.BindEquipmentRulePorts()` 集中装配：`BattleEquipmentAttackModifierResolver` 显式实现 `IBattleEquipmentAttackCheckQuery` / `IBattleEquipmentDamageQuery`，`BattleEquipmentAbilityRuntimeService` 显式实现 `IBattleEquipmentCombatReactionSink`；rules 不引用 module/concrete runtime，getter 不执行 setup/rebind，teardown 先解绑 query/sink consumer 再释放 provider。
- Contingency capability 边界：`BattleContingencySystem` 只弱借用 `IBattleContingencyRuntimePort`；该端口由 `BattleContingencyBridgeService` 实现，集中提供当前 state/grid/skill 查询、玩家学习来源复核、source-event 编号、同步 auto-cast 与 owner overlay 刷新。system 不再反向依赖 `BattleRuntimeModule`，但 auto-cast 仍在原调用栈内使用同一个 `BattleEventBatch` 直接进入 orchestrator，保留递归 reaction 的可见顺序与 effect-origin scope；module teardown 在解绑 bridge borrower 前先清除此端口。
- Timeline/status owner 边界：`BattleTimelineDriver` 真实拥有 timeline phase、current TU、`tu_per_tick`、ready unit、action threshold、stamina 与 Control objective 分数推进；`BattleRuntimeSkillTurnResolver` 真实拥有 cooldown anchor、turn timer、状态周期 tick/duration/turn-start 规则，并按状态应用时的 current TU 初始化 `next_tick_at_tu`。`BattleRuntimeModule.MarkAppliedStatusesForTurnTiming(...)` 只保留“先初始化 tick anchor，再通知 Fate”的跨 owner 编排。timeline step 继续按 current TU → Control 区域归属/计分 → 状态 tick/duration → 场地时效 → pending cast → ready collection/sort 推进；unit activation 继续按 trait turn-start → cooldown/turn timer → metrics/contingency/sequential auto-cast → AP/移动点重置 → turn-start status/control 推进。
- 桥接：通用 trait 进战斗为 `IBattleRuntimeCharacterGateway.BuildEffectiveTraitProjectionForEquipmentView(...)` → `BattleUnitFactory` → `BattleUnitState.effective_trait_instances/effective_trait_ids`（以 battle-local `EquipmentState` 重算，战斗规则不反查 trait/item catalog 或角色装备源），再由 `BattleTraitPassiveProjectionService` 将 trait typed passive 投影到 `BattleUnitState.save_advantage_tags/damage_resistances/save_bonus_by_ability/status_effects`，其中 trait 被动状态使用 `trait_passive_status` source layer 以便刷新装备投影时清理重建。装备能力进战斗为 `GameContentCatalog.GetEquipmentAbilityBindingDefinitionsTyped/GetTraitDefsTyped` → `BattleRuntimeModule` typed 索引 → `BattleEquipmentAbilityProjectionService` → `BattleUnitState.equipment_ability_sources`；binding 级时间进度倍率配置同时投影为 `BattleUnitState.temporal_progress_modifiers` 供 timeline/casting runtime 读取；角色装备武器的 `range_type` 通过 `WeaponProjection` 进入 `BattleUnitState.weapon_range_type` 供装备能力 fact 读取；敌人经 `EncounterRosterBuilder` 投 battle-only source；战斗规则只从 `BattleUnitState` 读装备能力源、时间进度 modifier、武器 range type 与 `creature_type_tags`。装备能力召唤的战斗内单位由 `BattleEquipmentSummonResolver`（`BattleEquipmentAbilityRuntimeService` 持有的 summon 职责拆分类）创建为 battle-only `BattleUnitState`，可从 `SummonUnitsActionPayloadDef` 投影基础技能与天生武器，占用 `BattleState`/grid 并通过 `BattleAiBlackboard` 记录来源单位、装备实例、binding、state key 与过期 TU。
- 装备能力运行时动作边界：`BattleEquipmentAbilityRuntimeService` 解析 before-hit / after-hit / after-skill / after-kill / after-damage / after-attack-check / after-status-expired 的 typed action payload 并写入 `BattleEventBatch` / `BattleState`，包括强制暴击来源、内部 SkillDef 结算、直接伤害、治疗、按 fact 治疗、清状态/target mark、状态栈消费、授予技能、召唤单位、召唤物消费、临时边特征、持久装备 counter 修改、持久状态同步与立即武器追击；其中召唤/召唤物消费职责拆分为 `BattleEquipmentSummonResolver`、target mark 生命周期职责拆分为 `BattleEquipmentTargetMarkResolver`、条件/fact 求值（condition group、compare_fact、nearby 计数、typed fact 读取）拆分为 `BattleEquipmentAbilityConditionEvaluator`、反应动作执行拆分为 `BattleEquipmentStatusActionResolver`（apply/clear status、状态栈消费）、`BattleEquipmentSkillTriggerActionResolver`（trigger_skill 内部技能）、`BattleEquipmentAreaActionResolver`（schedule area/terrain/edge feature）与 `BattleEquipmentDirectEffectActionResolver`（伤害/治疗/AP/耐久/add_damage_dice）、能力状态机拆分为 `BattleEquipmentAbilityStateResolver`（modify_ability_state、derived state 同步、once-scope/per-battle charge、persistent counter、state schema 查询）、攻击修正收集拆分为 `BattleEquipmentAttackModifierResolver`（attack roll/defense 修正、强制暴击、投射类别、bonus damage dice 收集、damage roll mode、固定减伤、loot 倍率），全部由主服务持有并在 `Setup` 相互接线（evaluator 与 executor 经主服务注入所需 resolver 引用），主服务保留事件入口、`ResolveActions` 编排、roll gate 与对外 internal 委托入口；有时限 target mark 的剩余 TU 存在 `BattleEquipmentTargetMarkState`，由 `BattleRuntimeSkillTurnResolver` 与目标状态时钟同步推进。`BattleAttackCheckPolicyService` 把强制暴击及来源装备实例/binding/action 写入本次 `AttackCheckInput`，未命中不升级；预览只有在 `CritLocked`/force-hit-no-crit 均未生效时才公开“命中后必定暴击”，命中后的击杀归因沿该来源进入 on-kill；`BattleDamageResolver` 在真实武器攻击检定提交后分发 after-attack-check，并把声明合并的内部技能结果并回父攻击；`BattleSkillExecutionOrchestrator` 负责装备技能 entry revalidation、usage commit，并向装备能力 runtime 提交通用目标结果摘要和 `BattleKillProvenance`；`BattleCommandPreviewService` 只做只读 preview/entry 校验。装备授予技能入口取 binding 固定 `skill_level` 与角色已学同名技能等级的较高值；来源绑定武器追加伤害通过伤害事件携带来源技能 id，只有角色已学该技能时才向该技能熟练度写入，不按具体武器分支。
- 强制暴击击杀归因边界：`BattleKillProvenance` 只在最终 `AttackEffectResolutionResult.CriticalHit` 为真时保留 forced-critical 的装备实例/binding/action；禁暴击或其他后续规则把结果降为普通命中时，击杀按实际主手攻击归因，不得误触发依赖 forced-critical binding 的 on-kill 反应。装备能力发起的立即武器攻击可以提供外层 provenance 作为 fallback：最终强制暴击来源优先覆盖它，未形成强制暴击时仍保留外层 binding/action 的 on-kill 语义。
- 装备能力 AP 恢复边界：`ModifyActionPointsActionPayloadDef.mode = restore_current_action_points_capped` 可在 after-kill 等通用 action 阶段恢复指定单位的当前 AP，并以该单位 `attribute_snapshot[action_points]` 为正常上限；已达到或超过上限时不增加也不反向降低。触发次数限制若需要必须由内容显式声明，运行时不默认附加 once-per-turn。
- 边界：`BattleEventBatch` / `BattlePreview` 的 canonical state 与 report facts 保持 plain C# 只读视图；只有同步 Godot 消费边界通过 Request-domain `BattleEventBatchProjection` / `BattlePreviewProjection` 构建短期 root lease，damage/range/save-branch 等 nested collection 由同一 lease 在创建时显式拥有。objective mutation 以完整 command、timeline step、start-confirm reaction 或 promotion choice 为根，嵌套同步反应只标 dirty；最外层成功结束后统一 flush，并最多锁存一个含 objective mode/outcome/end reason/decision TU 的 `BattleFinalDecision`。`winner_faction_id` 与 `encounter_resolution` 仅是只读投影。battle lifetime 内创建的 pathless native wrapper 由 Battle-domain `NativeLeaseScope` / projection lease 显式拥有，不能逃逸到 session 或下一场战斗。headless 等 caller 已持有 context projection lease 时，`BattleRuntimeModule.StartBattleBorrowingContext(...)` 只在同步 start 调用期间 borrow，不重复 claim 或保存 raw context，并必须显式传入从正式 encounter 解析的 objective；原有 `StartBattle(...)` 仍接管 unowned raw context，并由 start scope 关闭。`BattleRuntimeModule.Dispose()` 按 decision/context → action plan → AI/service sidecar → content borrower/index → state/topology → owned terrain 的顺序 best-effort 关闭，先断开 borrower 再释放 owner，首个异常保留堆栈重抛且重复关闭幂等；content rebind 也先清旧 borrower/action plan，失败时不保留半绑定状态。读条 / pending cast 是 runtime-only battle state（不进 save），由 `BattleCastingTimeService` / `BattleTimelineDriver` / `BattleRuntimeSkillTurnResolver` / `BattleSkillExecutionOrchestrator` 协作，manual cancel 走 typed command path。标准地面 AoE 的预览、手动施法、`AutoCastRequest` 与 pending cast 在 unit/terrain effect 消费前共用同一个屏障地格裁剪上下文；单位命中按裁剪后占用格相交，地形效果消费独立的裁剪结果，Contingency 只接收裁剪后可见地格与实际单位。冲锋自身和被推单位在每步位移前复用单位边界穿越结算，路径步 AoE 以当前路径锚点逐格裁剪且预览只读；重复攻击在共享 resolver 内统一检查，连锁伤害保留实际跳跃起点；`meteor_swarm` 作为垂直坠落灾害显式豁免水平投射屏障。contingency 战斗侧由 `BattleContingencySystem` 从 persistent setup 初始化、经 `ContingencyTargetResolverService` 解析目标、排队执行 `AutoCastRequest`；consumed 单一真相是 `BattleUnitState.MarkContingencySetupConsumed`，写回契约是 `IBattleRuntimeCharacterGateway.Validate/CommitContingencyConsumedSetups`，失败随 finalization rollback。temporal 状态族（`time_stasis` / `time_slow` / `time_reverberation`）与装备投影出的时间进度倍率规则归属 `BattleTemporalStatusService`，由 timeline / casting / turn-resolver / grid 协作执行；下一回合 AP 清零这类 turn-start AP 规则归属 `BattleStatusSemanticTable` + `BattleRuntimeSkillTurnResolver`，不在具体武器技能里硬编码。
- 事件增量边界：batch 在写入 changed units/coords/log/report、timeline、phase、modal 等事实时同步维护 typed `BattleChangeFlags`；多段命令通过 `MergeFrom(...)` 聚合后再生成一次 presentation delta，不能只靠集合是否为空判断是否有更新。
- 屏障与装备桥接边界：`BattleEquipmentAbilityRuntimeService` 只有在本次 effect 含武器伤害、能力来源匹配当前主手、`BattleUnitState.weapon_range_type` 与正式 `ItemDefinition` 都确认为 `ranged` 时，才把普通远程武器伤害的 `nonmagical_missile` 基线类别和 `on_hit/after_hit` 的 `projected_effect_categories` 合入 `BattleBarrierService` 的 unit effect 分类；技能、effect 或装备反应已显式声明 `magical_missile` / `nonmagical_missile` 时不重复添加基线类别。近战武器在读取任何装备附加投射类别之前即被硬排除，非当前主手、非武器伤害与 terrain effect 也一律不继承，preview 与 commit 共用同一只读收集逻辑。单位技能预览对手动排序/重复目标在一次 command 内共享 detached barrier preview session，以本地破层模拟保持逐槽顺序且不写真实 store；随机链继续保留真实抽样候选池，并单独记录经屏障过滤的 impact candidates，breaker 首击被挡后的候选只有在同一随机链仍存在可达后续命中时才进入 impact 集合。AI 的 unit/multi/random-chain fast preview 只在场上存在 layered barrier 时委托 canonical `PreviewCommand`，不得在 evaluator 内复制色层或装备类别规则；候选池与候选组上限只统计 canonical preview 后仍可影响目标的候选，不能让被屏障完全阻挡的前排目标耗尽上限。
- 适合：战斗流程、战斗结算、特殊技能流程、战斗内事务。
- 邻接单元：CU-02、CU-10、CU-11、CU-12、CU-13、CU-14、CU-16、CU-17、CU-18、CU-20、CU-21。

### CU-16 战斗状态模型、边规则、伤害、AI 规则层

- 文件：
  - `scripts/systems/battle/core/*.cs`
  - `scripts/systems/battle/terrain/Battle*.cs`
  - `scripts/systems/battle/rules/*.cs`
  - `scripts/systems/battle/rules/DamageApplicationProjection.cs`
  - `scripts/systems/battle/rules/IBattleDamageApplicationHook.cs`
  - `scripts/systems/battle/ai/*.cs`
  - `scripts/enemies/definitions/*.cs`
  - `scripts/enemies/actions/*.cs`
  - `scripts/systems/battle/sim/BattleSimContentProvider.cs`
  - `scripts/systems/battle/sim/BattleSimFormalCombatFixture.cs`
  - `scripts/systems/battle/sim/BattleSimProfileDefinition.cs`
  - `scripts/systems/battle/sim/BattleSimOverride*.cs`
  - `scripts/player/progression/CombatEffectDef.cs`
  - `scripts/player/progression/SkillDefinition.cs`
  - `scripts/player/warehouse/Weapon*.cs`
- 细节文档：
  - `docs/design/battle/skill_runtime.md`
  - `docs/design/battle/equipment_ability_runtime.md`
  - `docs/design/battle/weapon_dice_and_equipment.md`
  - `docs/design/battle/ai_score_parameters.md`
  - `docs/design/progression/fate_runtime.md`
- 负责：BattleState、地形/边规则、伤害、命中、状态语义、AI definition 执行、评分与决策规则，以及 BattleSim typed override。
- AI definition/runtime 边界：CU-20 的 authored `EnemyAiBrainDef` / `EnemyAiAction` / `BattleAiScoreProfile` 只负责资源 schema 与加载期校验；进入 runtime 的正式输入是 `EnemyAiBrainDefinition`、具体 `*ActionDefinition` 与 `BattleAiScoreProfileDefinition`。`BattleAiActionAssembler` 从 definition graph 构建 managed `BattleAiRuntimeActionPlan`，各 `BattleAi*ActionEvaluator` 拥有实际决策算法；authoring Resource 不执行 battle 行为，也不通过 duplicate、instance id 或动态属性回到 runtime。一次 AI 选择的 decision lifetime 以 `BattleAiDecisionResult` 为交付边界：结果只交出 deep-copied decision/command/score/trace，capture 的 `finally` 立即 clear context，之后不保留 state、plan、score profile 或 nested Godot collection alias；mutation guard 使用 typed `BattleAiMutationSnapshot` 覆盖 unit/status/cell/terrain/barrier/blackboard、target mark、temporary edge 与 allocator 等完整权威面，在决策边界比较后只报告差异并失败退出，不承担 gameplay 状态恢复。技能与 barrier profile definition index 都防御性复制并只暴露只读视图，guard 按 canonical key、成员与引用 identity 校验；`SkillDefinition` 及其 combat/variant/effect 嵌套 definition 必须防御性冻结公开构造输入，不能把调用方 mutable collection 或 modifier alias 留进 runtime graph。action plan generation 在 clear/rebind/dispose 时显式关闭。
- AI mutation guard 失败边界：`FullSnapshotDiagnostic` 是显式测试/诊断断言，不是事务；一次 AI 决策无论正常返回还是 evaluator 异常退出，都在 context 清理前比较完整权威 stable projection。正常返回后发现非豁免差异时立即记录并抛出 `BattleAiMutationViolationException`；异常退出且存在差异时同样抛出 guard exception，并把 evaluator 原异常保存在 `InnerException`，没有差异则原样重抛 evaluator 异常。失败 battle fixture 直接废弃，所有路径都不执行状态回滚。report 与 promotion queue 的所有键、嵌套值和类型身份都进入比较；可读 diff 的数量上限不能反向限制检测面。snapshot 层只保存 stable baseline，不保存整图恢复副本；owner-local mutation-exact capture seam 仅用于保留 canonical key、顺序、重复项、非法哨兵、null 与类型身份的检测精度。
- AI 性能与生命周期边界：production `BattleAiService` 默认关闭全状态 mutation snapshot；`FullSnapshotDiagnostic` 只作为显式正确性诊断 lane，并由真实 scorer/action 回归覆盖。`BattleMovementQueryService` 的 pure topology/path cache 可在同一 battle epoch 和 state/grid/delegate owner 身份不变时跨 decision 复用；epoch 或 owner 变化立即丢弃，`DisposeRuntime()` 只结束当前 battle cache，`Dispose()` 是不可逆的终止边界。AI 的 layered-barrier 评分从 decision context 借用 immutable `BarrierProfileDefinition` 索引，并结合 `BattleBarrierStore`、单位位置、现存层数与剩余 TU 生成 detached tactical projection；评分不按具体技能 id 分支，decision clear 后不保留 profile/state 引用。单位 `coord` 与 `body_size/body_size_category` 是 authoritative geometry；`footprint_size/occupied_coords` 由 `BattleUnitState` setter 及 `BattleState.SetUnit/SetUnits/SetUnitsFromDictionary` admission 写入口同步维护并随 owner 写提交推进 movement geometry revision。攻击、邻接、AI、grid、UI 等读路径只消费稳定投影，不执行 refresh/修复；clone、snapshot 与 load 对不一致投影 fail-fast。
- Objective AI 边界：AI 只读 `BattleObjectiveProgressSnapshot` 或 typed runtime facts。Boss/Intercept 模式在强制目标规则之后把正式目标 unit 排在首位但保留其他合法目标；Defense 模式同样让敌方优先选择受保护 unit，而防守目标自身固定等待；Escape 模式让自动控制的必需队员复用 `BattleMovementQueryService` 求到任一合法出口 anchor 的实际路径，允许绕障所需的等距步骤，无路时回退常规战斗 AI，完整进入后才等待其他队员；Rescue 的 battle-only 目标在 secured 前固定等待交互；Escort/Intercept 目标只执行到正式出口的寻路/等待，不借用模板 brain 转入普通攻击；NodeOperation 的初始持久队员相邻时提交 Interact，否则向任一未完成节点的合法 anchor 寻路，无路时回退常规战斗；Control 优先向敌占、中立、争夺区域移动，身处争夺区时回退常规战斗，己方占领区内可守点。mutation stable projection 必须覆盖每个已实现 objective subtype，并包含 Rescue 的 mutable secured bit、Escort/Intercept 的冻结出口事实、Defense 的 start/deadline TU、NodeOperation 的逐节点坐标/完成位与 Control 的冻结区域/双方分数。
- BattleSim override 边界：`BattleSimContentProvider` 与 `BattleSimFormalCombatFixture` 只借用 process snapshot 的 typed definitions；formal fixture 为每次 roster/runtime 复制自身需要的 skill、profession、achievement、item、trait 与 identity 索引，不重建 `ProgressionContentRegistry` / `ItemContentRegistry`。`BattleSimOverrideApplier` 复制 definition 索引，并以新的 `SkillDefinition` / `EnemyAiBrainDefinition` / `EnemyAiActionDefinition` / `BattleAiScoreProfileDefinition` 值表达本次模拟 patch。override 结果只属于该次 simulation，不改写 process snapshot，也不对 authored Resource 调用 `Duplicate` / `Set`。
- 战斗规则边界：伤害上下文正式链是 `DamageResolutionContext`，写回前 hook 边界是 `DamageApplicationProjection` / `IBattleDamageApplicationHook`（hook / release suppression 由战斗 runtime 的 contingency owner 管理，不入 damage payload / AI 评分 / save schema）。`BattleUnitState` 状态效果正式源是 `BattleStatusEffectCollection`，状态上的一次性攻击优势、攻击/豁免加值由 `BattleStatusEffectState` typed 字段声明，真实攻击检定消耗归 `BattleDamageResolver`，真实豁免消耗归 `BattleSaveResolver`，preview/probability 不消费；execute effect 的 `soul_fracture_duration_tu = 0` 明确表示不施加灵魂裂隙，正数才生成该状态；typed save / damage mitigation 真相源是 `save_advantage_tags` / `damage_resistances`；技能的 `requires_weapon` 效果要求 `equipped` 武器投影，若技能同时带 `melee` 标签，则 `BattleRangeService` 进一步要求该投影的 `weapon_range_type == melee`，武器家族门禁只负责已装备与家族匹配，不替代近战类型判断；layered barrier 真相源是 `BattleBarrierStore` / `BattleBarrierInstanceState`，`BattleBarrierService` 同时拥有单位边界穿越、投射效果显式起点检查、投射地面效果的逐格 allowed/blocked 裁剪、当前层破解提交、只读指定锚点预览判定与 special profile 豁免；效果服务不得按单位锚点或地形格再次复制屏障规则。临时边特征真相源是 `BattleTemporaryEdgeFeatureState`，由 `BattleEdgeService` 叠加进 runtime edge face，移动/占位/寻路仍消费统一 edge face。`BattleStatusSemanticTable` 拥有通用状态语义（如 `paralyzed` 的行动、移动与 pending cast 阻断），不在具体武器能力中硬编码。`BattleFateEventBus` 事件 surface 是 typed `BattleFateEventPayload`，misfortune 直接触发用 `MisfortuneTriggerRequest`。fixed schema 名称（combat resource id / damage tag / mitigation tier / target-team filter / save tag/ability / forced-move mode 等）由 enum/typed utility 解析。
- 伤害应用生命周期边界：`DamageApplicationInput` / `DamageApplicationProjection` / hook context/result 与 `BattleDamagePreviewResult` 是 plain typed value，shield/hp/fatal preview、hook 和业务结果不长期保存 Godot Dictionary。仍要求 Dictionary 的同步 adapter 在入口立即归一化；caller 调用 `DamageApplicationInput.ToDictionaryLease()`，由该方法从 `BuildSnapshotPlain()` 创建 Request-domain projection lease，并在调用返回后关闭。preview/result payload 直接从 plain damage event 构造，不缓存 wrapper 到 `BattleDamageResolver`、event batch 或下一次效果结算。
- 伤害段边界：`BattleDamageResolver` 负责把 `CombatEffectDefinition.ExtraDamageSegments` 结算为同一次 damage effect 下的额外 `DamageEventResult`；额外段复用该 effect 的 save result 与目标倍率规则，但不继承武器骰、暴击额外骰或装备追加骰。目标分类倍率只读 `BattleUnitState.creature_type_tags`，不回查敌人模板或物品/trait catalog。
- 装备伤害骰边界：装备能力 `add_damage_dice` 与状态来源绑定的武器额外骰由 `BattleDamageResolver` 统一结算；来源绑定骰只在状态 `source_unit_id` 匹配攻击者且本次 effect 实际包含武器伤害时触发，并入 bonus damage dice 以复用暴击额外骰路径；装备能力骰数可由通用 fact（如装备能力状态 fact）按 authoring 公式放大，具体武器的成长键仍只在 `.tres`；`subtract=true` 的骰只扣减匹配主伤害标签的本次基础伤害，不生成负数额外伤害段。
- 桥接：装备能力的命中检定加值、优势、防御组件调整、命中后强制暴击与召唤物数量/距离修正由 `BattleEquipmentAttackModifierResolver` 从 `BattleUnitState.equipment_ability_sources`、`BattleUnitState.attribute_snapshot`、typed target mark 和 battle-only 召唤单位 blackboard 收集，经 `IBattleEquipmentAttackCheckQuery` 注入 `BattleAttackCheckPolicyService`，再由 `BattleHitResolver` 生成本次 `AttackCheckInput`；bonus dice、damage roll mode 与固定减伤经 `IBattleEquipmentDamageQuery` 注入 `BattleDamageResolver`，真实 after-attack-check/after-hit/hit-received/damage-applied 与耐久投影刷新经 `IBattleEquipmentCombatReactionSink` 同步提交。三个端口不提供 state getter，状态只来自 `BattleAttackCheckPolicyContext` / `DamageResolutionContext` / reaction context；反应 context/result plain DTO 归属 battle core，嵌套伤害仍保留当前同步调用栈。命中检定加值与强制暴击可用 `require_weapon_damage` 限定只作用于含武器伤害的攻击定义，也可通过 `attribute_modifier_id` 从使用者属性快照读取动态调整值。忽略 AC component 等规则只调整本次目标 AC，不改写目标 `attribute_snapshot[armor_class]`。召唤单位是否为 summoned 的规则只读 `BattleAiBlackboard` / 状态效果，不从武器内容表反查。
- 目标过滤边界：死者单位默认不可被 unit skill 选中；只有 ally/self 且 effect 列表含 `Heal` / `HealFatal` 这类复活治疗语义时，由 `BattleSkillExecutionOrchestrator` 显式放开 dead target，不为具体装备技能 id 建特例。orchestrator 主文件的技能预览链（`BattleSkillPreviewService`）、目标校验（`BattleSkillTargetValidationService`）、链式伤害（`BattleChainDamageService`）与随机链抽样（`BattleRandomChainSkillService`）已物理拆分为四个弱借用 `BattleRuntimeModule` 的 service；orchestrator 在 `Setup` 中接线，在 `DisposeRuntime` 中同时断开 runtime、owner 与 sibling borrower，对外 internal 入口由 orchestrator 保留窄委托。
- 适合：战斗规则、伤害链、AI 行为、状态语义、目标过滤、射程规则。
- 邻接单元：CU-13、CU-15、CU-17、CU-18、CU-20。

### CU-17 战斗地形 profile、敌人 roster、prop 注入

- 文件：
  - `scripts/enemies/WildEncounterRoster*.cs`
  - `scripts/systems/battle/content/BattleEncounter*.cs`
  - `scripts/systems/battle/content/Battle*ObjectiveDef.cs`
  - `scripts/systems/battle/content/BattleScenarioActorDef.cs`
  - `scripts/systems/battle/objectives/BattleEncounterDefinition.cs`
  - `scripts/systems/battle/terrain/BattleTerrainGenerator.cs`
  - `scripts/systems/world/EncounterRosterBuilder.cs`
  - `scripts/systems/world/WildEncounterGrowthSystem.cs`
  - `scripts/utils/BattleBoardPropCatalog.cs`
  - `data/configs/enemies/rosters/*.tres`
  - `data/configs/battle_encounters/*.tres`
  - `assets/main/battle/terrain/canyon/*.png`
- 负责：正式 battle encounter、战斗地形生成、wild encounter roster 装配、prop 注入。
- 边界：`BattleEncounterDef` 是 roster/objective/world resolution 的遭遇级 authoring owner；anchor 只持 `encounter_profile_id`，不得持有或回退到旧 `enemy_roster_template_id`。`WildEncounterRosterDef` / stage / unit entry 只属于 authoring 与 snapshot 投影边界；`EncounterRosterBuilder`、`WildEncounterGrowthSystem` 与 battle runtime 只消费 `BattleEncounterDefinition` / `WildEncounterRosterDefinition` / `EnemyTemplateDefinition` / `EnemyAiBrainDefinition`，并生成 battle-only plain state，不保留 authored Resource。
- Objective 内容边界：Boss/Intercept authoring 引用 roster entry 的稳定 `actor_id`，非空 actor 在每个 stage 必须唯一且 `count == 1`，并投影为 battle-only `encounter_actor_id`，不替换运行时 unit id；managed definition 省略 actor 时必须规范化为空 `StringName`，不能把 null 带入 battle payload。Rescue/Escort/Defense authoring 引用 encounter 自有 `scenario_actor.actor_id`；scenario actor 必须引用正式 enemy template，并配置稳定入口 zone id、类型化边和纵深。Defense 还声明正数且按 5 TU 对齐的相对 `duration_tu`，不复用 actor 字段表达静态节点或波次。Escape/Escort/Intercept 出口同样只配置稳定 zone id、类型化边和纵深，不配置依赖地图尺寸的裸坐标。NodeOperation authoring 由 objective 自有的 `operation_nodes` 声明任意正数个稳定 node id、显示名、zone id、类型化边和纵深，拒绝空字段及重复 node id，不借用 scenario actor/unit 表示静态节点。Control authoring 由 `control_zones` 声明任意正数个稳定 zone id、显示名、类型化边和纵深，并声明按 5 TU 对齐的正数目标分；运行时再拒绝实际地图上重叠或双方无完整 footprint 合法落点的区域。默认世界随机权重与 canonical encounter seed 分开决策。
- 适合：canyon 地形、spawn/roster、战斗 props、地形 profile。
- 邻接单元：CU-15、CU-16、CU-18、CU-20。

### CU-18 战斗展示主链

- 文件：
  - `scenes/ui/battle_map_panel.tscn`
  - `scripts/ui/BattleMapPanel.cs`
  - `scripts/systems/battle/presentation/BattleHudAdapter.cs`
  - `scripts/systems/battle/presentation/BattleHudSnapshot.cs`
  - `scripts/systems/battle/core/BattleObjectiveProgressSnapshot.cs`
  - `scripts/systems/battle/presentation/BattleHoverSnapshot.cs`
  - `scripts/systems/game_runtime/BattlePresentationDelta*.cs`
  - `scenes/ui/battle_board_2d.tscn`
  - `scripts/ui/BattleBoard2D.cs`
  - `scripts/ui/BattleUiTheme.cs`
  - `scripts/ui/BattleBoardRenderProfile.cs`
  - `scripts/ui/BattleBoardController.cs`
  - `scenes/common/battle_board_prop.tscn`
  - `scripts/ui/BattleBoardProp.cs`
- 负责：battle HUD、棋盘绘制、单位/prop 渲染、相机、overlay、hover 展示。
- 边界：`BattleHudAdapter` 把 runtime facts 转成 detached `BattleHudSnapshot` / `BattleHoverSnapshot`，包括从 `BattleBarrierStore` 投影出的 active layered-barrier 摘要和从 `BattleObjectiveProgressSnapshot` 投影出的目标标题/进度；`BattleMapPanel` 只渲染 snapshot，不下转 mutable objective state。Escape/Escort/Intercept 的冻结出口格、NodeOperation 的未完成节点与 Control 的全部占领格由 `BattleBoardController` 以独立 marker source 可视化；Rescue HUD 提示持久队员移动到目标相邻位置并点击交互；Defense HUD 显示受保护单位和当前/开始/截止/剩余 TU；NodeOperation HUD/快照显示逐节点坐标、完成位和完成数；Control HUD/快照显示双方分数、目标分与逐区域归属。规则真相仍在 objective runtime。UI 不解析或长期保存 Godot collection，也不拥有命中、伤害、射程或目标合法性计算。`BattleMapPanel` 对 pathless shader material 使用 scene-domain lease，并先清空 `TextureRect` 的 material/texture borrower 再关闭 owner；path-backed shader/texture/scene 始终借用。`BattleBoardController` 每次 bind 持有一个 render-generation `NativeLeaseScope`，只拥有 pathless `TileSet` / atlas / `Image` / `ImageTexture` / style box，`Clear()`、rebind 与 `BattleBoard2D._ExitTree()` 都先清 borrower 再幂等关闭该 lease。`BattleBoardProp` 为按需创建的 pathless `CircleShape2D` 建立独立 SceneTree-domain lease，离树时先禁用 area、清 `CollisionShape2D.Shape` borrower，再关闭 shape owner；展示主链不再启用 production quarantine，lease owner/scope 计数在 clear/rebind/exit 后回到调用前向量。
- 展示增量边界：advance、命令、cancel 与多段 tick 统一生成 typed `BattlePresentationDelta`。log-only 只刷新日志文本；unit-state delta 只替换目标 token，并在没有 log/full-board fact 时跳过 runtime log 全表扫描；timeline 变化可刷新全部 unit token但不重建 TileMap；placement/full-board 才走保守全刷新。hover 每次输入最多计算一次 preview，并只在同一 selected-preview key 命中时复用缓存，cache miss 回退普通 overlay。
- 适合：battle HUD、棋盘视觉、TileMap、相机、目标浮标。
- 邻接单元：CU-06、CU-15、CU-16、CU-17、CU-19、CU-20。

### CU-19 自动化回归与截图辅助

- 文件：
  - `.github/workflows/ci.yml`
  - `tests/run_regression_suite.py`
  - `tests/tooling/test_run_regression_suite.py`
  - `tests/run_e2e_suite.py`
  - `tests/tooling/test_run_e2e_suite.py`
  - `tests/e2e/**/*`
  - `tests/shared/*`
  - `tests/shared/LifecycleTestSceneTree.cs`
  - `tests/shared/TestExitCoordinator.cs`
  - `tests/shared/LifecycleMeasurementBarrier.cs`
  - `tests/shared/TestResourceOwnership.cs`
  - `scripts/utils/TrueRandomSeedService.cs`
- 细节文档：
  - `docs/design/platform/godotsharp_lifecycle.md`
  - `tests/shared/TestContentResourceLoader.cs`
  - `tests/shared/TestWorldGenerationDefinitionFactory.cs`
  - `tests/shared/TestSkillDefinitionProjection.cs`
  - `tests/equipment/*`
  - `tests/warehouse/*`
  - `tests/battle_runtime/**/*`
  - `tests/progression/**/*`
  - `tests/runtime/**/*`
  - `tests/text_runtime/**/*`
  - `tests/world_map/**/*`
  - `scripts/dev_tools/*.cs`
  - `tools/*.py`
  - `tools/*.gd`
  - `tools/architecture/**`
- 负责：headless 回归、application E2E、contract 验证、fixture、截图/签名辅助。
- application E2E 边界：`tests/e2e` 从 `project.godot` 的正式 main scene 启动，键盘/action 经 `Input.ParseInputEvent`、指针经 `Viewport.PushInput`，不直接调用 UI callback 或 gameplay command。`run_e2e_suite.py` 串行编排独立 Godot 进程，为每个 scenario group 创建隔离的 XDG/AppData；建档与冷启动加载只在同组共享该临时 `user://`。只有显式声明 seed 且通过隔离目录校验的 scenario 才能在 main scene 启动前启用 `TrueRandomSeedService` 的 internal 确定性测试流；该 seam 只固定随机序列，不注入命令或强制结果，未启用时 production 仍走无锁 crypto 路径。普通 `run_regression_suite.py` 明确排除 E2E，退出仍复用 `LifecycleTestSceneTree` 到 coordinator 的正式 shutdown pipeline。
- 边界：`TestHarness.Finish(...)` 只冻结断言并生成 `TestResult`；C# runner 统一继承 `LifecycleTestSceneTree`，先由 `TestResourceOwnership.Close()` 关闭当前测试显式拥有的 authored/pathless fixture wrapper，再由 `TestExitCoordinator` 把结果提交给 `ApplicationLifetimeCoordinator`，owner teardown、production finalizer barrier 与最终退出均由同一 shutdown pipeline 负责。`LifecycleMeasurementBarrier` 只服务同进程 soak 的周期量测，是测试代码中唯一允许直接执行 GC/finalizer drain 的位置，不替代 process shutdown barrier。外层 `run_regression_suite.py --lifecycle-correctness` 拥有 post-exit correctness 判定：保留调用者的发现、筛选、并发与超时设置，为每个子进程强制 strict/trace、固定零 retry，并把 GodotSharp fatal marker 或 shutdown report 的非零 `legacy_debt` 独立于普通输出错误判为失败；unsafe/resource 输出保持可见，不设宽泛 shutdown-log 豁免。累计 cleanup/boundary gate 要求 production 中 `RuntimeStateLifecycle`、reflection graph walker、strong-wrapper sink、quarantine、直接 wrapper suppress、raw authored Resource runtime signature、不透明 Godot runtime storage 与 legacy Enemy/AI catalog 全部为零；同步 authoring/asset/ownership 边界只能按 exact owner/member 放行，同时约束 coordinator 是唯一 production Quit/GC barrier、测试 runner 无 local Quit/GC 且 Request/Battle/SceneTree lease/scope active vector 回到调用前。确定性 lifecycle soak 单进程执行 110 周期，记录逐周期 owner/root/lease 完整向量、activity 增量与 managed/private memory 统计。CI 只运行一次 `--lifecycle-correctness` strict full suite；cleanup/boundary gate 由 routine discovery 纳入同一次 full suite，不在其前另跑专用 lifecycle 命令。GodotSharp 生命周期或退出顺序改动必须同时读取 lifecycle architecture spec、`LifecycleTestSceneTree`、`TestExitCoordinator`、`LifecycleMeasurementBarrier`、两个 `run_runtime_lifecycle_*` gate、runner tooling regression 与 CI 接线。fixture 只验证业务 runtime 时优先用 definition/CLR builder；需要验证 authored schema 时由 `TestResourceOwnership` 明确拥有 Resource；一般 path-backed fixture 经 `TestContentResourceLoader` 以 `CacheMode.IgnoreDeep` 加载并在 loader/registry 作用域关闭，world fixture 复用 `TestWorldGenerationDefinitionFactory`。正式 `SkillDef` fixture 经 `TestSkillDefinitionProjection` 加载、登记 borrowed content 并立即投影为 `SkillDefinition`，不把 raw authored Resource 传入业务服务。
- 架构依赖门禁边界：`tools/architecture/Magic.ArchitectureAnalyzers` 按实际源码路径与少量 mixed-file symbol override 分层，配置、partial owner 与未分类源码均 fail closed；`layer_baseline.json` 只允许精确 `(rule, source symbol, target symbol)` 旧债。`magic.csproj` 以 analyzer 引用和 `AdditionalFiles` 正式执行门禁，CI 独立运行 analyzer 合成编译测试；完整 SARIF/确定性 JSON 清单仍通过外置 `Magic.ArchitectureInventory.targets` opt-in 生成，不进入 Godot 游戏全量回归。
- 慢例 fixture 边界：同一 runner 内反复构建独立业务 runtime、但只读正式静态内容时，在 coordinator `_Ready()` 完成后的首个 `ProcessFrame` 借用进程级 immutable `ContentSnapshot`，不为每个 case 重建 content registry；runner 使用强类型 C# 事件的一次性回调，不使用字符串 deferred method dispatch。每个 case 仍独立创建并释放 `PartyState`、`CharacterManagementModule`、`BattleRuntimeModule` 与 battle state，不能共享可变运行态换取速度。
- 输出协议边界：C# runner、benchmark、capture 和交互工具不直接调用 `GD.Print*` / `GD.Push*` 或散落的 `Console.Write*`。断言与结构化诊断走 `GameLog`；PASS/FAIL、shutdown report、外层 runner marker、交互式 REPL 等要求原样保留或供机器解析的行统一走 `ConsoleProcessOutput`，其文本不进入 session sink，也不受结构化日志格式或等级过滤影响。
- 性能回归边界：performance baseline/benchmark 是 opt-in 诊断入口，不进入 routine full suite；正式比较必须区分完整战斗基线与 bounded diagnostic，不能用 iteration-budget 提前结束的样本覆盖 formal baseline。
- 适合：补回归、跑局部验证、定位改动影响面。
- 邻接单元：按业务域补 CU-10、CU-12、CU-15、CU-17、CU-18、CU-21。

### CU-20 敌方模板、AI brain/action、roster 与 BattleSim 内容

- 文件：
  - `scripts/enemies/*.cs`
  - `scripts/enemies/actions/*.cs`
  - `scripts/enemies/definitions/*.cs`
  - `scripts/systems/battle/ai/BattleAiScoreProfile.cs`
  - `scripts/systems/battle/sim/BattleSimProfileDef.cs`
  - `scripts/systems/battle/sim/BattleSimProfileDefinition.cs`
  - `scripts/systems/battle/sim/BattleSimOverridePatchDefinition.cs`
  - `scripts/systems/battle/sim/BattleSimScenarioDef.cs`
  - `scripts/systems/battle/sim/BattleSimScenarioDefinition.cs`
  - `scripts/systems/battle/sim/BattleSimUnitSpec.cs`
  - `scripts/systems/battle/sim/BattleSimUnitDefinition.cs`
  - `scripts/systems/battle/sim/BattleSimExecutionLoop.cs`
  - `scripts/systems/battle/sim/BattleSimTerminationKind.cs`
  - `scripts/systems/battle/sim/BattleSimRunReport.cs`
  - `scripts/systems/battle/sim/BattleSimProfileSummary.cs`
  - `scripts/systems/battle/sim/BattleSimReportBuilder.cs`
  - `scripts/systems/battle/sim/BattleSimScenarioReport.cs`
  - `scripts/systems/battle/sim/BattleSimRunner.cs`
  - `scripts/systems/battle/sim/BattleSimReportFileWriter.cs`
  - `scripts/systems/battle/sim/BattleSim*Projection.cs`
  - `scripts/systems/content/ContentSnapshot.cs`
  - `scripts/systems/content/ContentSnapshotBuilder.cs`
  - `scripts/systems/world/EncounterRosterBuilder.cs`
  - `data/configs/enemies/enemy_content_seed.tres`
  - `data/configs/enemies/brains/*.tres`
  - `data/configs/enemies/templates/*.tres`
  - `data/configs/enemies/rosters/*.tres`
  - `data/configs/battle_sim/profiles/*.tres`
  - `data/configs/battle_sim/scenarios/*.tres`
- 细节文档：
  - `docs/design/battle/ai_score_parameters.md`
  - `docs/design/battle/balance_simulation.md`
- 负责：敌方模板、AI brain/state/action、generation slot、transition rule、wild encounter roster，以及 BattleSim profile/scenario/unit 的 authoring schema、加载期校验与 immutable definition 投影。
- 敌方内容边界：`EnemyContentSeed`、`EnemyTemplateDef`、`EnemyAiBrainDef`、各 action Resource 与 `WildEncounterRosterDef` 只由 `ProcessContentHost` 作为 canonical authored roots 持有，并且只在同步加载/校验阶段供 registry 读取；`EnemyContentRegistry.ProjectDefinitions(...)` 在 snapshot seal 前递归投影 `EnemyTemplateDefinition`、`EnemyAiBrainDefinition`、具体 `*ActionDefinition`、generation/transition definitions 与 `WildEncounterRosterDefinition`。`ContentSnapshot` 冻结这些索引后，session、catalog、world、battle 与 headless runtime 只借用同一 definition graph，不存在 raw enemy catalog 或 session 级 registry mirror。
- AI authoring 边界：`EnemyAiActionDefinition.FromResource(...)` 是 action Resource 到具体 definition 类型的唯一分派点；`EnemyAiBrainDefinition` 冻结 state/action/generation/transition 与 score profile 图。新 action 类型需要同时检查 authoring schema、definition 投影以及 CU-16 的 assembler/evaluator/dispatch，但实际战斗算法只属于 CU-16，不能放回 Resource 类。
- BattleSim profile 边界：`BattleSimProfileDef` 及其弱类型 `override_patches` 只在加载入口转换为 `BattleSimProfileDefinition` / `BattleSimOverridePatchDefinition`；正式 profile 随 process snapshot 发布，simulation runtime 与 report 只传递 definition。具体 patch 的 typed copy-on-write 规则归 CU-16。
- BattleSim scenario/unit 边界：`BattleSimScenarioDef` / `BattleSimUnitSpec` 只属于同步 `.tres` authoring/import；入口立即调用 `ToDefinition()`，深拷贝并冻结为 `BattleSimScenarioDefinition` / `BattleSimUnitDefinition`。runner、execution loop、report/file/trace projection 与 benchmark 在投影后只持有 definition；每次 run 从单位 definition 重建独立 `BattleUnitState`。path-backed scenario 由 `ResourceLoader` 缓存管理，benchmark 只丢弃局部 borrower，不手工 `Dispose()` / `Free()`。
- BattleSim 结果有效性边界：`BattleSimExecutionLoop` 是单局终止类别的唯一 owner，明确区分 battle ended、idle stall、iteration budget exhausted 与 invalid runtime；run/report projection 保留所有尝试供诊断，但 `BattleSimReportBuilder` 的正常胜率、均值、技能/action/faction totals 只消费 battle-ended runs。`run_count` 表示尝试数，`completed_run_count` 才是统计分母；任何 unfinished run 都使 scenario `is_complete=false`，正式 balance CLI 写完诊断报告后以非零退出。
- BattleSim 文件输出边界：`BattleSimRunner` 只编排报告生成并在成功后公布路径，`BattleSimReportFileWriter` 是 report、trace 与 trace summary 的唯一写盘 owner；它为每批输出分配同秒不冲突的唯一名称，检查打开、逐次写入与 flush 结果，失败时恢复报告输出状态并清理本批次残缺文件。
- 跨表校验边界：敌方内容需要 skill/item catalog 时消费加载边界已经投影好的 `SkillDefinition` / `ItemDefinition` 索引，不由 `EnemyTemplateDef` 提供 raw `SkillDef` / `ItemDef` 到 runtime 的投影。AI brain/action 校验负责不随单位等级变化的 action kind、target route、selection mode 与 cast-option 形状契约；`EnemyTemplateDef` 再以自身 `skill_ids` / `skill_level_map` 对实际会使用的 action-skill 对执行等级校验，确认该等级已有匹配变体，且 unit 命令的 base + variant 有效效果能被正式 unit execution pipeline 接受。unit effect 可执行集合由内容定义层的 `scripts/systems/content/skills/BattleUnitSkillDefinitionExecutionRules.cs` 唯一声明，内容校验与正式 orchestrator 共享该谓词，避免两套 allow-list 漂移。
- 适合：新敌人、敌方技能表、AI 状态与动作、roster 内容、BattleSim profile/scenario/unit authoring。
- 邻接单元：CU-02、CU-10、CU-15、CU-16、CU-17、CU-18。

### CU-21 Headless runtime、文本命令与快照渲染

- 文件：
  - `scripts/systems/game_runtime/headless/*.cs`
  - `scripts/utils/GameTextSnapshotRenderer.cs`
  - `tests/text_runtime/commands/run_contingency_text_commands_regression.cs`
  - `tests/text_runtime/commands/run_*.cs`
  - `tests/text_runtime/headless/run_*.cs`
  - `tests/text_runtime/tools/run_*.cs`
  - `tests/text_runtime/README.md`
- 负责：无 UI session、文本命令、expect 断言、文本/结构化快照。
- 边界：`HeadlessGameTestSession` 持有 typed `GameSession` / plain C# `GameRuntimeFacade`，并作为顶层 Runtime participant 登记到 `ApplicationLifetimeCoordinator`；shutdown 时先卸载 world/runtime graph，只对通过 `BindOwnedGameSessionForTests(...)` 显式注入的 test-owned session 执行 `GameSession.Dispose()`，canonical autoload session 由 coordinator 的 Session 阶段关闭。headless C# runner 与其他回归一样通过共享 `LifecycleTestSceneTree` / `TestExitCoordinator` 委托同一 application shutdown pipeline，外层进程结果再由 CU-19 lifecycle correctness profile 判定，headless session 不自行执行 GC、retry 或 post-exit 日志过滤。session/runtime 的结构化 snapshot 保持递归 plain C# graph，`GameTextSnapshotRenderer` 直接读取 plain facts；`GameTextCommandResult` 也只保存 managed snapshot/assertion facts，`SnapshotTyped` 与 `AssertionFactsTyped` 每次都返回隔离的 deep copy。只有需要 Godot collection 的同步测试/API 边界才创建整根 projection lease，并由同一 lease 递归拥有 nested collection。`GameTextCommandRunner` 的核心命令直接走 typed `GameRuntimeFacade` gateway；battle snapshot 的 contingency surface 包括 `battle.contingency` sidecar snapshot、unit overlay 的 contingency 字段与 `battle.report_entries` 结构化条目，objective surface 由 `battle.objective` 输出 mode、Boss target、必需队员、出口、节点、占领区/双方分数与当前进度。
- 适合：headless 指令域、snapshot schema、REPL/脚本回归、agent 自动化入口。
- 邻接单元：CU-02、CU-06、CU-10、CU-15、CU-16、CU-19、CU-20。

## 推荐装载组合

### 只改开始菜单、预设、显示设置

- 必带：CU-01、CU-02
- 按需补：CU-03、CU-14

### 只改世界生成、设施服务、起始遭遇

- 必带：CU-03、CU-04
- 按需补：CU-02、CU-05、CU-06、CU-20、CU-21

### 只改 world / battle runtime 接线、窗口互斥、场景同步

- 必带：CU-06
- 按需补：CU-07、CU-08、CU-09、CU-15、CU-18、CU-21

### 只改大地图迷雾、选中、渲染

- 必带：CU-05、CU-06、CU-07
- 按需补：CU-04

### 只改据点服务、人物信息、服务反馈

- 必带：CU-06、CU-08
- 按需补：CU-04、CU-12、CU-14

### 只改队伍编成、转职或角色奖励弹窗

- 必带：CU-06、CU-09、CU-11
- 按需补：CU-10、CU-12、CU-14

### 只改共享背包、物品内容、装备基础流转、仓库窗口

- 必带：CU-10、CU-11、CU-19
- 按需补：CU-02、CU-06、CU-09、CU-12、CU-21

### 只做装备耐久、装备实例化前置或战斗内装备损坏

- 必带：CU-10、CU-11、CU-12、CU-15、CU-16、CU-19
- 按需补：CU-06、CU-21

### 只改装备能力框架、内容 ABI、validator、战斗投影或装备授予技能

- 必带：CU-10、CU-13、CU-15、CU-16、CU-19
- 实现必读：`docs/design/battle/equipment_ability_runtime.md`、`docs/design/battle/skill_runtime.md`
- 按需补：CU-06、CU-11、CU-12、CU-18、CU-20、CU-21

### 只改角色成长、成就、奖励归并

- 必带：CU-11、CU-12、CU-13、CU-14
- 按需补：CU-09、CU-15、CU-19

### 只改敌方模板、敌方技能表、AI brain/action 或 roster 内容

- 必带：CU-20、CU-02
- AI action、transition、score 或 evaluator 必补：CU-16
- 按需补：CU-10（item/weapon 投影）、CU-15（battle 接线）、CU-17（roster/growth）、CU-18（展示）、CU-19（回归）

### 只改 BattleSim profile、scenario/unit definition、override 或 simulation content provider

- 必带：CU-02、CU-16、CU-20
- 实现必读：`docs/design/battle/balance_simulation.md`
- 按需补：CU-15、CU-17、CU-19

### 只改战斗规则、伤害、AI、terrain effect

- 必带：CU-15、CU-16
- AI definition/evaluator 改动必补：CU-20；若触及 process snapshot 或 BattleSim profile，再补 CU-02
- 按需补：CU-13、CU-17、CU-18、CU-19

### 只改战斗地形、props、battle build

- 必带：CU-17、CU-18、CU-19
- 按需补：CU-15、CU-20

### 只改 battle HUD、棋盘、TileMap、相机

- 必带：CU-18、CU-19
- 按需补：CU-15、CU-16、CU-17

### 只改 save payload、party schema、reward queue

- 必带：CU-02、CU-11
- 按需补：CU-10、CU-12

### 只改 headless 文本命令、快照、REPL 或脚本化回归

- 必带：CU-21、CU-19
- 按需补：CU-06，以及对应业务单元

### 只改正式启动、跨场景 UI 或 user data E2E

- 必带：CU-01、CU-02、CU-06、CU-19
- E2E 必读：`project.godot`、`tests/e2e/README.md`、`tests/e2e/shared/*`、`tests/run_e2e_suite.py`
- 按需补：被覆盖用户旅程对应的业务单元；battle 入口补 CU-15、CU-17、CU-18

### 只改 GodotSharp 生命周期、内容 owner、projection lease 或退出屏障

- 必带：CU-02、CU-19
- 实现必读：`docs/design/platform/godotsharp_lifecycle.md`
- 启动必读：`project.godot`
- 退出/量测必读：`tests/shared/LifecycleTestSceneTree.cs`、`tests/shared/TestExitCoordinator.cs`、`tests/shared/LifecycleMeasurementBarrier.cs`
- 验收必读：`tests/runtime/validation/run_runtime_lifecycle_*.cs`、`tests/runtime/lifecycle/*.cs`、`tests/run_regression_suite.py`、`tests/tooling/test_run_regression_suite.py`、`.github/workflows/ci.yml`
- 按需补：CU-06、CU-15、CU-16、CU-18、CU-20、CU-21；涉及敌方/AI/BattleSim 内容快照时同时读取 CU-16 与 CU-20

## 不推荐的切法

- 不要把这份文档写成实现迁移备忘录。
- 不要在单元描述里记录具体 typed 改造状态、字段级约束或 API 细节。
- 不要在这里维护具体回归脚本名单；测试入口留给对应 `tests/` 目录和 README。
- 不要把单个运行时 helper 当成独立架构层；优先按系统边界读图。
- 不要一次性装载 CU-02、CU-06、CU-12、CU-15、CU-18，除非任务确实跨越存档、世界、角色、战斗和展示整条链。
